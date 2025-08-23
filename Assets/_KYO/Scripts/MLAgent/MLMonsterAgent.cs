// MLMonsterAgent.cs
// - 외부 Noise 시스템(NoiseEvent/NoiseSystem/IAIMonsterHearing)은 그대로 사용
// - HL(Idle/Patrol/Investigate/Stalk/Chase/Ambush) = 정책 선택. 공격 사거리만 하드 인터럽트.
// - 액션 마스킹은 WriteDiscreteActionMask()에서 간단히 처리(브랜치 0 가정)
// - 네비 안정화: goal lock + repath 쿨다운 + 근접 시 오프셋 축소 + 스턱 복구
// - 애니메이터: Speed(float)만 사용
// - enum 중복 제거: MonsterMode 하나만 사용(= 디스크리트 액션 인덱스도 동일)

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators; // for IDiscreteActionMask
using URandom = UnityEngine.Random;

public enum MonsterMode
{
    Idle = 0,
    Patrol = 1,
    Investigate = 2,
    Stalk = 3,
    Chase = 4,
    Ambush = 5
}

public interface IMonsterStatus { MonsterMode Mode { get; } }

[RequireComponent(typeof(NavMeshAgent))]
public class MLMonsterAgent : Agent, IAIMonsterHearing, IMonsterStatus
{
    // ───────────── Refs ─────────────
    [Header("Refs")]
    public Transform player;
    public Transform eye;
    [Tooltip("시야를 가리는 레이어(플레이어/몬스터 제외 권장)")]
    public LayerMask occlusionMask;

    [Header("Player FOV(플레이어가 '날' 볼 수 있는지)")]
    public Transform playerEye;               // 없으면 player 사용
    public LayerMask playerOcclusionMask;     // 비우면 occlusionMask 사용

    NavMeshAgent agent;

    [Header("POI Roots")]
    public Transform generatorRoot, exitDoorRoot, searchPointRoot;
    Transform[] generatorPOIs, exitPOIs, wanderPoints;

    // ───────────── Vision ─────────────
    [Header("Vision")]
    public float viewDistance = 40f;
    [Range(0, 180f)] public float viewHalfAngle = 70f;

    [Header("Player FOV")]
    public float playerViewDistance = 40f;
    [Range(0, 180f)] public float playerViewHalfAngle = 60f;

    // ───────────── Move/Speed ─────────────
    [Header("Speeds")]
    public float normalRunSpeed = 4f;
    public float rotateSpeed = 180f;

    [Header("Ranges & Timers")]
    public float catchDistance = 1.4f;
    public float episodeTime = 120f;
    public float pathEvalInterval = 0.5f;

    // ───────────── Stability ─────────────
    [Header("Stability / Hysteresis")]
    public bool  useActionMask       = true;
    public float goalRetargetDistance = 3.5f;
    public float goalArriveTolerance  = 0.8f;
    public float minRepathInterval    = 0.40f;
    public float minDestinationDelta  = 0.40f;
    public float stuckSpeedEps        = 0.05f;
    public float stuckSeconds         = 1.0f;
    public float hlMinHoldSeconds     = 1.2f;  // HL 최소 유지
    public float nearOffsetCutoffDist = 2.0f;  // 근접 오프셋 축소
    public float chaseStickSeconds    = 1.5f;  // Chase 끈적임

    // ───────────── Hearing memory ─────────────
    [Header("Hearing Memory")]
    public float memoryNoiseSeconds = 6f;    // 소리 기억
    public float memorySightSeconds = 4f;    // 마지막 시야 기억
    public float noiseSmoothTime    = 0.25f; // 소리 필터

    Vector3 lastHeardPos; float lastHeardTime; float lastHeardPower;
    Vector3 _noiseFiltered; bool _noiseInit;

    // ───────────── Investigate ─────────────
    [Header("Investigate Sweep")]
    public float investigateRadius = 30f;
    [Range(0, 4)] public int investigateSweepCount = 2;
    public float sweepMinRadius = 12f;
    public float investigateHoldTime = 2f;

    readonly List<Vector3> investPoints = new();
    int investIndex = -1; bool investActive = false; bool investArrivedGiven = false; float investigateUntil = 0f;

    // ───────────── Patrol ─────────────
    [Header("Patrol")]
    public Transform patrolCenter;
    public float patrolRadius = 30f;
    [Range(0, 1)] public float poiPriority = 0.7f; // 발전기/문 우선 비율
    public int patrolRandomSamples = 16;
    public float wallClearance = 0.6f;

    // ───────────── Animation ─────────────
    [Header("Animation")]
    public Animator anim;
    public string speedParam = "Speed";
    bool hasSpeed;

    // ───────────── Runtime ─────────────
    public MonsterMode Mode => _curMode;
    [SerializeField] MonsterMode _curMode = MonsterMode.Patrol;

    float epTimer, pathEvalTimer, lastPathLen = -1f;

    // 시야/속도 추정
    Vector3 lastSeenPlayerPos; float lastSeenPlayerTime;
    Vector3 prevPlayerPos; float prevPlayerTime;
    Vector3 estPlayerVel; float estPlayerSpeed;
    public float playerSpeedNormMax = 7f;

    // 이동 상태
    public float localMoveRadius = 3f;
    Vector3 _goalBase; bool _hasGoal;
    Vector3 _lastSetDest; float _nextRepathAt;
    float _stuckTimer;
    int _lastHLIndex = -1; float _nextHLAllowSwitchAt;
    float _chaseStickUntil = 0f;

    const float EPS = 1e-6f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!eye) eye = transform;
        if (playerOcclusionMask.value == 0) playerOcclusionMask = occlusionMask;

        agent.updateRotation = true;
        agent.autoRepath     = true;

        RebuildPOIs();
    }

    void Start()
    {
        if (anim)
            hasSpeed = anim.parameters.Any(p => p.name == speedParam && p.type == AnimatorControllerParameterType.Float);

        if (agent && !agent.isOnNavMesh) agent.Warp(SampleNav(transform.position));
    }

    void RebuildPOIs()
    {
        generatorPOIs = CollectChildren(generatorRoot);
        exitPOIs      = CollectChildren(exitDoorRoot);
        wanderPoints  = CollectChildren(searchPointRoot);
    }

    Transform[] CollectChildren(Transform root)
    {
        if (!root) return System.Array.Empty<Transform>();
        var list = new List<Transform>();
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c && c.gameObject.activeInHierarchy) list.Add(c);
        }
        return list.ToArray();
    }

    public override void OnEpisodeBegin()
    {
        RebuildPOIs();

        Vector3 m = transform.position + URandom.insideUnitSphere * 8f; m.y = 0f;
        Vector3 p = (player ? player.position : transform.position) + URandom.insideUnitSphere * 8f; p.y = 0f;

        agent.ResetPath();
        agent.Warp(SampleNav(m));
        if (player) player.position = SampleNav(p);

        epTimer = 0f; pathEvalTimer = 0f; lastPathLen = -1f;

        lastSeenPlayerPos = transform.position; lastSeenPlayerTime = -999f;

        lastHeardPos = transform.position; lastHeardTime = -999f; lastHeardPower = 0f;
        _noiseFiltered = lastHeardPos; _noiseInit = false;

        prevPlayerPos = player ? player.position : transform.position; prevPlayerTime = Time.time;

        _hasGoal = false; _nextRepathAt = 0f; _stuckTimer = 0f; _lastHLIndex = -1; _nextHLAllowSwitchAt = 0f;
        _curMode = MonsterMode.Patrol;
    }

    // ───────────── Hearing (외부 시스템 콜백) ─────────────
    public void OnHearNoise(Vector3 pos, float percevied, NoiseEvent raw)
    {
        lastHeardPos   = pos;
        lastHeardPower = Mathf.Clamp01(percevied);
        lastHeardTime  = Time.time;

        if (!_noiseInit) { _noiseFiltered = pos; _noiseInit = true; }
        BuildInvestigatePlan(pos);
    }

    // ───────────── Observations(=14) ─────────────
    public override void CollectObservations(VectorSensor sensor)
    {
        bool iSee = CanSeePlayer(out _);
        bool seenBy = PlayerCanSeeMe();

        sensor.AddObservation(iSee ? 1f : 0f);                         // 1
        sensor.AddObservation(seenBy ? 1f : 0f);                       // 2

        Vector3 toP = player ? (player.position - transform.position) : Vector3.zero; toP.y = 0f;
        Vector2 toPdir = toP.sqrMagnitude > EPS ? new Vector2(toP.x, toP.z).normalized : Vector2.zero;
        sensor.AddObservation(toPdir);                                  // 3-4
        sensor.AddObservation(Mathf.Clamp01(toP.magnitude / 50f));      // 5

        float angleMeToP = 0f, anglePToMe = 0f;
        if (toP.sqrMagnitude > EPS) angleMeToP = Vector3.Angle(transform.forward, toP.normalized) / 180f;
        if (player)
        {
            Vector3 v = (transform.position - player.position); v.y = 0f;
            if (v.sqrMagnitude > EPS) anglePToMe = Vector3.Angle((playerEye ? playerEye : player).forward, v.normalized) / 180f;
        }
        sensor.AddObservation(angleMeToP);                               // 6
        sensor.AddObservation(anglePToMe);                               // 7

        Vector3 toN = lastHeardPos - transform.position; toN.y = 0f;
        Vector2 toNdir = toN.sqrMagnitude > EPS ? new Vector2(toN.x, toN.z).normalized : Vector2.zero;
        sensor.AddObservation(toNdir);                                   // 8-9
        sensor.AddObservation(Mathf.Clamp01((Time.time - lastHeardTime) / memoryNoiseSeconds)); // 10
        sensor.AddObservation(Mathf.Clamp01(lastHeardPower));            // 11

        sensor.AddObservation(0f);                                       // 12 (stare fraction, 현재 미사용)
        sensor.AddObservation(Mathf.Clamp01(estPlayerSpeed / Mathf.Max(0.001f, playerSpeedNormMax))); // 13
        sensor.AddObservation(0f);                                       // 14 (buffActive? 0)
    }

    // ───────────── Action Mask (브랜치 0 가정) ─────────────
    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (!useActionMask) return;

        // 버전 호환을 위해 ActionSpec 접근 없이 브랜치 0만 마스킹
        Vector3 tmp;
        bool iSee = CanSeePlayer(out tmp);
        bool haveNoise = (Time.time - lastHeardTime) < memoryNoiseSeconds;

        const int branch = 0; // HL 브랜치
        if (!iSee)      actionMask.SetActionEnabled(branch, (int)MonsterMode.Stalk,       false);
        if (!haveNoise) actionMask.SetActionEnabled(branch, (int)MonsterMode.Investigate, false);
        if (!iSee && !haveNoise)
        {
            actionMask.SetActionEnabled(branch, (int)MonsterMode.Chase,  false);
            actionMask.SetActionEnabled(branch, (int)MonsterMode.Ambush, false);
        }
    }

    // ───────────── Main Tick ─────────────
    public override void OnActionReceived(ActionBuffers actions)
    {
        float dt = Time.deltaTime;
        epTimer += dt;

        // 플레이어 속도 추정
        if (player)
        {
            float pdt = Mathf.Max(1e-4f, Time.time - prevPlayerTime);
            estPlayerVel   = (player.position - prevPlayerPos) / pdt;
            estPlayerSpeed = new Vector3(estPlayerVel.x, 0f, estPlayerVel.z).magnitude;
            prevPlayerPos  = player.position; prevPlayerTime = Time.time;
        }

        // 소리 EMA
        if ((Time.time - lastHeardTime) < memoryNoiseSeconds && _noiseInit)
        {
            float a = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, noiseSmoothTime));
            _noiseFiltered = Vector3.Lerp(_noiseFiltered, lastHeardPos, a);
        }

        // 시야 메모리
        bool iSee = CanSeePlayer(out Vector3 ppos);
        if (iSee) { lastSeenPlayerPos = ppos; lastSeenPlayerTime = Time.time; }

        float curSpeed = normalRunSpeed;
        agent.stoppingDistance = 0.1f;

        // 공격 사거리(유일한 하드 인터럽트)
        if (player && Vector3.Distance(transform.position, player.position) <= catchDistance)
        {
            AddReward(+1.0f);
            EndEpisode();
            return;
        }

        // 스턱 관리
        bool movingSlow = agent.velocity.magnitude < stuckSpeedEps;
        _stuckTimer = movingSlow ? _stuckTimer + dt : 0f;

        // === HL 선택(최소 유지 + Chase 끈적임) ===
        int reqHL = actions.DiscreteActions.Length > 0 ? Mathf.Clamp(actions.DiscreteActions[0], 0, 5) : (int)MonsterMode.Patrol;
        if (_lastHLIndex == (int)MonsterMode.Chase && reqHL != (int)MonsterMode.Chase && Time.time < _chaseStickUntil)
            reqHL = (int)MonsterMode.Chase;

        int hl = reqHL;
        if (_lastHLIndex >= 0 && Time.time < _nextHLAllowSwitchAt) hl = _lastHLIndex;

        if (hl != _lastHLIndex)
        {
            if (_lastHLIndex == (int)MonsterMode.Chase) _chaseStickUntil = Time.time + chaseStickSeconds;
            _hasGoal = false; _stuckTimer = 0f; _lastHLIndex = hl;
            _nextHLAllowSwitchAt = Time.time + hlMinHoldSeconds;
        }

        switch ((MonsterMode)hl)
        {
            case MonsterMode.Idle:        agent.isStopped = true; _curMode = MonsterMode.Idle; break;
            case MonsterMode.Patrol:      PatrolTick(curSpeed, actions);      _curMode = MonsterMode.Patrol; break;
            case MonsterMode.Investigate: InvestigateTick(curSpeed, actions);  _curMode = MonsterMode.Investigate; break;
            case MonsterMode.Stalk:       StalkTick(curSpeed, iSee, actions);  _curMode = MonsterMode.Stalk; break;
            case MonsterMode.Chase:       ChaseTick(curSpeed, iSee, actions);  _curMode = MonsterMode.Chase; break;
            case MonsterMode.Ambush:      AmbushTick(curSpeed, actions);       _curMode = MonsterMode.Ambush; break;
        }

        // 공통 보상/관리
        AddReward(-0.01f * dt);

        pathEvalTimer += dt;
        if (pathEvalTimer >= pathEvalInterval)
        {
            float curLen = GetPathLength(transform.position, player ? player.position : transform.position);
            if (lastPathLen > 0f && curLen + 0.2f < lastPathLen) AddReward(+0.02f);
            lastPathLen = curLen; pathEvalTimer = 0f;
        }

        if (anim && hasSpeed)
            anim.SetFloat(speedParam, agent.velocity.magnitude);

        if (epTimer >= episodeTime) { AddReward(-0.2f); EndEpisode(); }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = (Input.GetKey(KeyCode.D) ? 1 : 0) + (Input.GetKey(KeyCode.A) ? -1 : 0);
        ca[1] = (Input.GetKey(KeyCode.W) ? 1 : 0) + (Input.GetKey(KeyCode.S) ? -1 : 0);

        var da = actionsOut.DiscreteActions;
        if      (Input.GetKey(KeyCode.Alpha1)) da[0] = (int)MonsterMode.Idle;
        else if (Input.GetKey(KeyCode.Alpha2)) da[0] = (int)MonsterMode.Patrol;
        else if (Input.GetKey(KeyCode.Alpha3)) da[0] = (int)MonsterMode.Investigate;
        else if (Input.GetKey(KeyCode.Alpha4)) da[0] = (int)MonsterMode.Stalk;
        else if (Input.GetKey(KeyCode.Alpha5)) da[0] = (int)MonsterMode.Chase;
        else if (Input.GetKey(KeyCode.Alpha6)) da[0] = (int)MonsterMode.Ambush;
        else da[0] = (int)MonsterMode.Patrol;
    }

    // ──────────────── Ticks ────────────────
    void PatrolTick(float speed, ActionBuffers a)
    {
        Vector3 baseTarget = PickPatrolTarget();
        MoveToStable(baseTarget, speed, a);
        Transform poi = GetNearestPOI(transform.position, includeExit: true);
        if (poi && Vector3.SqrMagnitude(transform.position - poi.position) < 9f) AddReward(+0.01f);
    }

    void InvestigateTick(float speed, ActionBuffers a)
    {
        if (!investActive && (Time.time - lastHeardTime) < memoryNoiseSeconds) BuildInvestigatePlan(lastHeardPos);
        if (!investActive) { Vector3 seed = transform.position + transform.forward * 5f; BuildInvestigatePlan(seed); }

        Vector3 target = investPoints[investIndex];
        float dist = Vector3.Distance(transform.position, target);

        if (investIndex == 0 && dist <= 1.2f)
        {
            if (!investArrivedGiven) { AddReward(+0.15f); investArrivedGiven = true; investigateUntil = Time.time + investigateHoldTime; }
            Face(transform.position + transform.right, rotateSpeed * Time.deltaTime);
            if (Time.time >= investigateUntil) NextInvestPointOrEnd();
        }
        else
        {
            MoveToStable(target, speed, a);
            if (dist <= 0.8f) NextInvestPointOrEnd();
        }
    }

    void StalkTick(float speed, bool iSeeNow, ActionBuffers a)
    {
        // 간단: 마지막 시야 지점으로 접근
        Vector3 baseTarget =
            (Time.time - lastSeenPlayerTime) < memorySightSeconds ? lastSeenPlayerPos
                                                                  : transform.position + transform.forward * 2f;
        MoveToStable(baseTarget, speed, a);
    }

    void ChaseTick(float speed, bool iSeeNow, ActionBuffers a)
    {
        Vector3 baseTarget =
            iSeeNow ? lastSeenPlayerPos :
            ((Time.time - lastSeenPlayerTime) < memorySightSeconds ? lastSeenPlayerPos :
             ((Time.time - lastHeardTime) < memoryNoiseSeconds ? _noiseFiltered : transform.position + transform.forward * 3f));

        MoveToStable(baseTarget, speed, a);
    }

    void AmbushTick(float speed, ActionBuffers a)
    {
        Vector3 predict = player ? player.position + estPlayerVel * 1.5f : lastSeenPlayerPos;
        MoveToStable(predict, speed, a);
    }

    // ───────────── Investigate utils ─────────────
    void BuildInvestigatePlan(Vector3 center)
    {
        investPoints.Clear();
        investIndex = -1; investActive = false; investArrivedGiven = false; investigateUntil = 0f;

        investPoints.Add(TrySampleSafe(center, out var p0) ? p0 : center);

        for (int i = 0; i < investigateSweepCount; i++)
        {
            for (int t = 0; t < 12; t++)
            {
                float ang = (360f / 12f) * t * Mathf.Deg2Rad;
                float r   = URandom.Range(sweepMinRadius, investigateRadius);
                Vector3 cand = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                if ((cand - transform.position).sqrMagnitude < 36f) continue; // 6m 미만 제외
                if (TrySampleSafe(cand, out var ps)) { investPoints.Add(ps); break; }
            }
        }

        investIndex = 0; investActive = true;

        if (Vector3.SqrMagnitude(transform.position - investPoints[0]) < 4f)
        {
            Vector3 n = center + URandom.insideUnitSphere * 6f; n.y = center.y;
            if (TrySampleSafe(n, out var pn)) investPoints[0] = pn;
        }
    }

    void NextInvestPointOrEnd()
    {
        investIndex++;
        if (investIndex >= investPoints.Count) { investActive = false; investIndex = -1; }
    }

    // ───────────── Navigation core ─────────────
    void MoveToStable(Vector3 baseTarget, float speed, ActionBuffers a)
    {
        if (!_hasGoal) SetGoal(baseTarget);
        else
        {
            float distNew = Vector3.Distance(_goalBase, baseTarget);
            if (distNew > goalRetargetDistance || Arrived(_goalBase) || Stuck()) SetGoal(baseTarget);
        }

        float ax = Mathf.Clamp(a.ContinuousActions[0], -1f, 1f);
        float az = Mathf.Clamp(a.ContinuousActions[1], -1f, 1f);
        if (Mathf.Abs(ax) < 0.05f) ax = 0f; if (Mathf.Abs(az) < 0.05f) az = 0f;

        float distToGoal = Vector3.Distance(transform.position, _goalBase);
        float offScale   = Mathf.Clamp01((distToGoal - nearOffsetCutoffDist) / Mathf.Max(0.001f, localMoveRadius));
        Vector3 offset   = transform.TransformDirection(new Vector3(ax, 0f, az)) * (localMoveRadius * offScale);
        Vector3 final    = _goalBase + offset;

        agent.isStopped = false;
        agent.speed     = speed;

        if (Time.time >= _nextRepathAt || (_lastSetDest - final).sqrMagnitude > (minDestinationDelta * minDestinationDelta))
        {
            SetDestinationSafe(final);
            _lastSetDest  = final;
            _nextRepathAt = Time.time + minRepathInterval;
        }

        if (agent.hasPath)
        {
            Vector3 to = agent.steeringTarget - transform.position; to.y = 0f;
            if (to.sqrMagnitude > EPS)
            {
                var rot = Quaternion.LookRotation(to, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, agent.angularSpeed * Time.deltaTime);
            }
        }

        if (Stuck())
        {
            Vector3 jitter = URandom.insideUnitSphere * 2.5f; jitter.y = 0f;
            Vector3 cand = _goalBase + jitter;
            if (NavMesh.SamplePosition(cand, out var hit, 3f, NavMesh.AllAreas))
            { SetGoal(hit.position); SetDestinationSafe(hit.position); _stuckTimer = 0f; }
        }
    }

    void SetGoal(Vector3 baseTarget) { _goalBase = TrySampleSafe(baseTarget, out var s) ? s : baseTarget; _hasGoal = true; _nextRepathAt = 0f; }
    bool Arrived(Vector3 goal) => Vector3.Distance(transform.position, goal) <= goalArriveTolerance;
    bool Stuck() => _stuckTimer >= stuckSeconds;

    void Face(Vector3 worldPos, float maxDeg)
    {
        Vector3 look = worldPos - transform.position; look.y = 0f;
        if (look.sqrMagnitude > EPS)
        { var rot = Quaternion.LookRotation(look); transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, maxDeg); }
    }

    // ───────────── Common utils ─────────────
    Transform GetNearestPOI(Vector3 from, bool includeExit)
    {
        float best = float.PositiveInfinity; Transform bestT = null;
        void scan(Transform[] arr) { if (arr == null) return; foreach (var t in arr) { if (!t) continue; float d = (t.position - from).sqrMagnitude; if (d < best) { best = d; bestT = t; } } }
        scan(generatorPOIs); if (includeExit) scan(exitPOIs); return bestT;
    }

    Vector3 PickPatrolTarget()
    {
        if (URandom.value < poiPriority)
        {
            if (TryPickRandomValidPoint(generatorPOIs, out var p)) return p;
            if (TryPickRandomValidPoint(exitPOIs, out var e)) return e;
        }
        if (TryPickRandomValidPoint(wanderPoints, out var w)) return w;
        return RandomPointInRadius();
    }

    Vector3 RandomPointInRadius()
    {
        Vector3 c = patrolCenter ? patrolCenter.position : transform.position;
        for (int i = 0; i < patrolRandomSamples; i++)
        {
            Vector3 cand = c + URandom.insideUnitSphere * patrolRadius; cand.y = c.y;
            if (NavMesh.SamplePosition(cand, out var hit, 2.5f, NavMesh.AllAreas))
            { if (!IsNearWall(hit.position)) return hit.position; }
        }
        return c;
    }

    bool TryPickRandomValidPoint(Transform[] points, out Vector3 pos)
    {
        pos = default; if (points == null || points.Length == 0) return false;
        int tries = Mathf.Min(12, points.Length);
        for (int k = 0; k < tries; k++)
        { var t = points[URandom.Range(0, points.Length)]; if (!t) continue; if (TrySampleSafe(t.position, out var s)) { pos = s; return true; } }
        return false;
    }

    bool TrySampleSafe(Vector3 p, out Vector3 safe)
    {
        safe = p;
        if (NavMesh.SamplePosition(p, out var hit, 2.0f, NavMesh.AllAreas))
        { if (!IsNearWall(hit.position)) { safe = hit.position; return true; } }
        return false;
    }

    bool IsNearWall(Vector3 p) => Physics.CheckSphere(p + Vector3.up * 0.4f, wallClearance, occlusionMask, QueryTriggerInteraction.Ignore);

    void SetDestinationSafe(Vector3 world)
    {
        if (NavMesh.SamplePosition(world, out var hit, 2f, NavMesh.AllAreas))
        {
            if (!agent.hasPath || (agent.destination - hit.position).sqrMagnitude > 0.04f)
                agent.SetDestination(hit.position);
        }
    }

    Vector3 SampleNav(Vector3 world) => NavMesh.SamplePosition(world, out var hit, 3f, NavMesh.AllAreas) ? hit.position : world;

    float GetPathLength(Vector3 start, Vector3 end)
    {
        if (!NavMesh.SamplePosition(start, out var s, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(end,   out var e, 2f, NavMesh.AllAreas)) return 9999f;
        var path = new NavMeshPath();
        NavMesh.CalculatePath(s.position, e.position, NavMesh.AllAreas, path);
        if (path.status != NavMeshPathStatus.PathComplete) return 9999f;
        float len = 0f; for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]); return len;
    }

    bool LineOfSightClear(Vector3 from, Vector3 to, LayerMask mask)
    {
        Vector3 dir = to - from; float dist = dir.magnitude; if (dist <= 0.001f) return true; dir /= dist;
        return !Physics.Raycast(from, dir, dist, mask, QueryTriggerInteraction.Ignore);
    }

    bool CanSeePlayer(out Vector3 pos)
    {
        pos = Vector3.zero; if (!player) return false;
        Vector3 target = player.position;
        Vector3 v = target - eye.position;
        if (v.magnitude > viewDistance) return false;
        Vector3 flat = v; flat.y = 0f; Vector3 f = transform.forward; f.y = 0f;
        if (Vector3.Angle(f, flat) > viewHalfAngle) return false;
        if (!LineOfSightClear(eye.position, target, occlusionMask)) return false;
        pos = target; return true;
    }

    bool PlayerSeesPoint(Vector3 worldPoint)
    {
        if (!player) return false;
        Transform pEye = playerEye ? playerEye : player;
        Vector3 v = worldPoint - pEye.position; float d = v.magnitude;
        if (d > playerViewDistance) return false;
        Vector3 flat = v; flat.y = 0f;
        if (flat.sqrMagnitude <= EPS) return true;
        Vector3 pf = pEye.forward; pf.y = 0f;
        if (Vector3.Angle(pf, flat.normalized) > playerViewHalfAngle) return false;
        var mask = (playerOcclusionMask.value == 0 ? occlusionMask : playerOcclusionMask);
        return LineOfSightClear(pEye.position, worldPoint, mask);
    }

    bool PlayerCanSeeMe() => PlayerSeesPoint(transform.position);
}
