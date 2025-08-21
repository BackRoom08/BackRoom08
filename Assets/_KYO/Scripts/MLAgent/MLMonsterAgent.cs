// MLMonsterAgent.cs (HL + Continuous, Interrupt-aware Investigate)
// - 순찰/수색 중이라도 '플레이어가 보이거나' '새 소리 발생' 시 즉시 인터럽트(우선순위 전환)
// - 수색 플랜: [소리 지점] -> [스윕1] -> [스윕2] (반경 investigateRadius, 개수 investigateSweepCount)
// - 평소엔 상태 진입 시 1회 목표만 찍고 도착/스턱/상태변경 전까지 유지
// ※ Behavior Parameters: Observation=12, Continuous=2, Discrete=[5]

using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

// ── 상태 ──
public enum MonsterMode
{
    Idle,         // 대기
    Patrol,       // 순찰(목표 1회 고정 이동)
    Investigate,  // 수색(소리/POI로 고정 이동, 소리 스윕)
    Stalk,        // 몰래 응시(정지 2초로 스택)
    Chase,        // 추격(래치)
    Ambush,       // 매복(예측 차단)
    HitIdle       // 타격 후 정지
}
public interface IMonsterStatus { MonsterMode Mode { get; } }

[RequireComponent(typeof(NavMeshAgent))]
public class MLMonsterAgent : Agent, IAIMonsterHearing, IMonsterStatus
{
    // ===== Refs =====
    [Header("Refs")]
    public Transform player;
    public Transform eye;
    [Tooltip("몬스터 시야를 가리는 레이어(플레이어/몬스터 제외 권장)")]
    public LayerMask occlusionMask;
    [Tooltip("플레이어의 눈(없으면 player 사용)")]
    public Transform playerEye;
    [Tooltip("플레이어 시야 가림(비우면 occlusionMask 사용)")]
    public LayerMask playerOcclusionMask;
    private NavMeshAgent agent;

    [Tooltip("발전기 포인트 배열(루트가 지정되면 자동 채움)")]
    public Transform[] generatorPOIs;
    [Tooltip("순찰용 수색 포인트 배열(루트가 지정되면 자동 채움)")]
    public Transform[] wanderPoints;

    [Header("Point Roots (optional)")]
    public Transform generatorRoot;   // 부모 아래 자식들을 자동 수집
    public Transform searchPointRoot; // 부모 아래 자식들을 자동 수집

    // ===== Vision =====
    [Header("Vision")]
    public float viewDistance = 40f;
    [Range(0,180f)] public float viewHalfAngle = 70f;

    [Header("Player Detection (들킴 판정)")]
    public float playerViewDistance = 40f;
    [Range(0,180f)] public float playerViewHalfAngle = 60f;

    // ===== Move/Speed =====
    [Header("Speeds")]
    public float runSpeed = 4f;       // 기본 4
    public float buffedRunSpeed = 6f; // 버프 6
    public float rotateSpeed = 120f;

    [Header("Chase/Search")]
    public float localMoveRadius = 3f;
    public float investigateHoldTime = 2f;
    public float catchDistance = 1.4f;
    public float episodeTime = 80f;
    public float pathEvalInterval = 0.5f;

    [Header("Ambush")]
    public float ambushLeadSeconds = 1.5f;

    // ===== Look-Stack / Buff =====
    [Header("Look-Stack")]
    public float stareStillRequiredSeconds = 2f;
    public float stillSpeedThreshold = 0.05f;
    public float stareStackPerSec = 1f;
    public float stareStackThreshold = 30f;
    public float buffDuration = 40f;

    [Header("Hit/Idle After Hit")]
    public float idleAfterHitSeconds = 5f;
    float hitIdleTimer;

    [Header("Damage")]
    public float baseAttackDamage = 10f;
    public float buffedAttackDamage = 18f;
    public float currentAttackDamage = 10f;

    [Header("Animation (옵션)")]
    public Animator anim;
    public string speedParam = "Speed";
    public string chaseBool = "IsChasing";
    public string investigateBool = "IsInvestigate";

    // ===== Patrol / Wander =====
    [Header("Patrol / Wander")]
    public Transform patrolCenter;                 // 비우면 스폰 기준
    public float patrolRadius = 30f;
    [Range(0f,1f)] public float patrolPoiRatio = 0.7f; // 발전기 우선(70%)
    public float patrolArriveTolerance = 1.2f;
    public float patrolMinClearance = 0.6f;
    public int   patrolRandomSamples = 16;
    public float patrolStuckSpeedEps = 0.05f;
    public float patrolStuckTime = 1.0f;

    // ===== Sound memory =====
    Vector3 lastHeardPos;
    float   lastHeardPower;
    float   lastHeardTime;

    // ===== Internals =====
    float investigateUntil;
    bool  investigateArrivedGiven;

    float lastPathLen = -1f;
    float pathEvalTimer;
    float epTimer;

    // 응시/버프/정지
    float stareStack;
    bool  buffActive;
    float buffEndTime;
    float stillTimer;

    // 시야 보조
    Vector3 lastSeenPlayerPos;
    float   lastSeenPlayerTime;

    // 플레이어 속도 추정
    Vector3 prevPlayerPos;
    float   prevPlayerPosTime;

    [Header("Stalk vs Chase Signals")]
    public float playerSpeedNormMax = 7f;
    float estPlayerSpeed = 0f;

    // ── 상태 & 전이 플래그 ──
    enum HL { Stalk=0, Chase=1, Investigate=2, Patrol=3, Ambush=4 }
    [Header("Runtime Status (ReadOnly)")]
    [SerializeField] MonsterMode _mode = MonsterMode.Idle;
    public MonsterMode Mode => _mode;
    void SetMode(MonsterMode m) => _mode = m;

    HL _lastHL = (HL)(-1); // 직전 HL
    bool _enteredPatrol, _enteredInvestigate;

    // ── Patrol 내부 상태 ──
    Vector3 _patrolTarget;
    bool    _patrolHasTarget = false;
    float   _patrolStuckTimer = 0f;
    Vector3 _spawnPoint;

    // ── Investigate 플랜(소리 지점 + 스윕 2곳) ──
    [Header("Investigate Sweep")]
    public float investigateRadius = 30f;      // 스윕 반경
    [Range(1,4)] public int investigateSweepCount = 2; // 스윕 지점 수
    public float sweepMinRadius = 12f;         // 스윕 최소 반경
    List<Vector3> _investPoints = new List<Vector3>(); // 0: 소리 지점, 1..: 스윕
    int _investIndex = -1;                     // 현재 목표 인덱스
    bool _investActive = false;

    // 애니 파라미터 캐싱
    bool _hasSpeed, _hasChase, _hasInvestigate;

    // ── Chase 래치(플레이어용 추격 신호) ──
    [Header("Chase Latch (export to player)")]
    public float chaseAcquireTime = 0.2f;
    public float chaseReleaseTime = 0.8f;
    public float chaseMemoryTime  = 2.5f;
    public float chaseNoiseMaxDistance = 12f;
    public float chaseMaxExportDistance = 55f;
    bool chaseLatched = false;
    float chaseOnTimer = 0f, chaseOffTimer = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!eye) eye = transform;
        if (playerOcclusionMask.value == 0) playerOcclusionMask = occlusionMask;

        agent.updateRotation = false;
        agent.autoRepath = true;

        currentAttackDamage = baseAttackDamage;

        RebuildPointArraysIfNeeded();
    }

    void Start()
    {
        if (anim)
        {
            _hasSpeed       = anim.parameters.Any(p => p.name == speedParam && p.type == AnimatorControllerParameterType.Float);
            _hasChase       = anim.parameters.Any(p => p.name == chaseBool  && p.type == AnimatorControllerParameterType.Bool);
            _hasInvestigate = anim.parameters.Any(p => p.name == investigateBool && p.type == AnimatorControllerParameterType.Bool);
        }
    }

    void RebuildPointArraysIfNeeded()
    {
        if (generatorRoot)
        {
            var list = new List<Transform>();
            for (int i = 0; i < generatorRoot.childCount; i++)
            {
                var c = generatorRoot.GetChild(i);
                if (c && c.gameObject.activeInHierarchy) list.Add(c);
            }
            if (list.Count > 0) generatorPOIs = list.ToArray();
        }

        if (searchPointRoot)
        {
            var list = new List<Transform>();
            for (int i = 0; i < searchPointRoot.childCount; i++)
            {
                var c = searchPointRoot.GetChild(i);
                if (c && c.gameObject.activeInHierarchy) list.Add(c);
            }
            if (list.Count > 0) wanderPoints = list.ToArray();
        }
    }

    public override void OnEpisodeBegin()
    {
        Vector3 m = transform.position + Random.insideUnitSphere * 12f; m.y = 0f;
        Vector3 p = (player ? player.position : transform.position) + Random.insideUnitSphere * 12f; p.y = 0f;

        var mPos = SampleNav(m);
        var pPos = SampleNav(p);

        agent.ResetPath();
        agent.Warp(mPos);
        if (player) player.position = pPos;

        _spawnPoint = transform.position;
        _patrolHasTarget = false;
        _patrolStuckTimer = 0f;

        ResetInvestigatePlan();

        lastHeardPos   = agent.transform.position;
        lastHeardPower = 0f;
        lastHeardTime  = -999f;

        lastPathLen = GetPathLength(agent.transform.position, player ? player.position : agent.transform.position);
        pathEvalTimer = 0f;
        epTimer = 0f;

        hitIdleTimer = 0f;

        stareStack = 0f;
        buffActive = false;
        buffEndTime = 0f;
        stillTimer = 0f;

        lastSeenPlayerPos = transform.position;
        lastSeenPlayerTime = -999f;

        if (player)
        {
            prevPlayerPos = player.position;
            prevPlayerPosTime = Time.time;
        }

        currentAttackDamage = baseAttackDamage;

        chaseLatched = false; chaseOnTimer = chaseOffTimer = 0f;

        RebuildPointArraysIfNeeded();

        SetMode(MonsterMode.Patrol);
        _lastHL = (HL)(-1);
    }

    // 외부 HearingSensor에서 호출
    public void OnHearNoise(Vector3 pos, float perceived, NoiseEvent raw)
    {
        lastHeardPos   = pos;
        lastHeardPower = Mathf.Clamp01(perceived);
        lastHeardTime  = Time.time;

        // ★ 새로운 수색 플랜을 즉시 구성(소리 → 스윕 두 곳)
        BuildInvestigatePlan(pos);
    }

    // ===== Observations (12-dim) =====
    public override void CollectObservations(VectorSensor sensor)
    {
        bool iSeePlayer   = CanSeePlayer(out _);
        bool playerSeesMe = PlayerCanSeeMe();

        sensor.AddObservation(iSeePlayer ? 1f : 0f);
        sensor.AddObservation(playerSeesMe ? 1f : 0f);

        Vector3 toP = player ? (player.position - transform.position) : Vector3.zero; toP.y = 0f;
        Vector2 toPdir = toP.sqrMagnitude > 1e-6f ? new Vector2(toP.x, toP.z).normalized : Vector2.zero;
        sensor.AddObservation(toPdir);
        sensor.AddObservation(Mathf.Clamp01(toP.magnitude/50f));

        Vector3 toN = lastHeardPos - transform.position; toN.y = 0f;
        Vector2 toNdir = toN.sqrMagnitude > 1e-6f ? new Vector2(toN.x, toN.z).normalized : Vector2.zero;
        sensor.AddObservation(toNdir);
        sensor.AddObservation(Mathf.Clamp01((Time.time - lastHeardTime)/6f));
        sensor.AddObservation(Mathf.Clamp01(lastHeardPower));

        sensor.AddObservation(Mathf.Clamp01(stareStack / Mathf.Max(0.001f, stareStackThreshold)));

        sensor.AddObservation(Mathf.Clamp01(estPlayerSpeed / Mathf.Max(0.001f, playerSpeedNormMax)));
        sensor.AddObservation(buffActive ? 1f : 0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float dt = Time.deltaTime;

        // 히트 후 강제 정지
        if (hitIdleTimer > 0f)
        {
            hitIdleTimer -= dt;
            agent.isStopped = true;
            AddReward(-0.04f * dt);
            SetMode(MonsterMode.HitIdle);
            UpdateAnimatorFlags(false, false);
            return;
        }

        // 버프 만료
        if (buffActive && Time.time >= buffEndTime)
        {
            buffActive = false;
            currentAttackDamage = baseAttackDamage;
            stareStack = 0f;
        }

        // 인지 판정
        bool iSeePlayer   = CanSeePlayer(out Vector3 ppos);
        bool playerSeesMe = PlayerCanSeeMe();
        bool hasRecentNoise = (Time.time - lastHeardTime) < 6f;

        if (iSeePlayer) { lastSeenPlayerPos = ppos; lastSeenPlayerTime = Time.time; }

        // 플레이어 속도 추정
        Vector3 playerVel = Vector3.zero;
        if (player)
        {
            float pdt = Mathf.Max(1e-4f, Time.time - prevPlayerPosTime);
            playerVel = (player.position - prevPlayerPos) / pdt;
            estPlayerSpeed = new Vector3(playerVel.x, 0f, playerVel.z).magnitude;
        }

        // 정지 판정
        bool actuallyStill =
            agent.isStopped ||
            agent.velocity.magnitude <= stillSpeedThreshold ||
            (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.05f);

        if (actuallyStill) stillTimer += dt; else stillTimer = 0f;

        // === High-level action & enter flags ===
        int hlRaw = actions.DiscreteActions.Length > 0 ? actions.DiscreteActions[0] : 1; // 기본 Chase
        HL hl = (HL)Mathf.Clamp(hlRaw, 0, 4);
        _enteredPatrol      = (hl == HL.Patrol      && _lastHL != HL.Patrol);
        _enteredInvestigate = (hl == HL.Investigate && _lastHL != HL.Investigate);

        // ----- 스택 처리 -----
        bool canStack = (hl == HL.Stalk) && iSeePlayer && !playerSeesMe && (stillTimer >= stareStillRequiredSeconds);
        if (canStack)
        {
            stareStack += stareStackPerSec * dt;
            AddReward(+0.04f * dt);
            if (!buffActive && stareStack >= stareStackThreshold)
            {
                buffActive = true;
                buffEndTime = Time.time + buffDuration;
                currentAttackDamage = buffedAttackDamage;
                AddReward(+0.6f);
            }
        }

        // ── ★ 인터럽트 우선순위 ───────────────────────────
        // 1) 플레이어가 보이면 무조건 추격
        if (iSeePlayer)
        {
            Vector3 baseTarget = ppos;
            MoveTo(baseTarget, buffActive ? buffedRunSpeed : runSpeed, actions);
            SetMode(MonsterMode.Chase);
            DoCommon(dt, iSeePlayer);
            _lastHL = hl;
            if (player) { prevPlayerPos = player.position; prevPlayerPosTime = Time.time; }
            return;
        }

        // 2) 수색 플랜이 활성화돼 있으면(새 소리 등) → Investigate 강행
        if (_investActive)
        {
            RunInvestigatePlan(dt, actions);
            SetMode(MonsterMode.Investigate);
            DoCommon(dt, iSeePlayer);
            _lastHL = hl;
            if (player) { prevPlayerPos = player.position; prevPlayerPosTime = Time.time; }
            return;
        }
        // ────────────────────────────────────────────────

        // === 이동/행동 (인터럽트 없을 때만 HL 수행) ===
        switch (hl)
        {
            case HL.Stalk:
            {
                agent.isStopped = true;
                Vector3 look = (lastSeenPlayerPos - transform.position); look.y = 0f;
                if (look.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), rotateSpeed * dt);
                if (playerSeesMe) AddReward(-0.08f * dt);
                break;
            }
            case HL.Chase:
            {
                Vector3 baseTarget = (Time.time - lastSeenPlayerTime < 5f) ? lastSeenPlayerPos :
                                     (hasRecentNoise ? lastHeardPos : transform.position + transform.forward * 2f);
                MoveTo(baseTarget, buffActive ? buffedRunSpeed : runSpeed, actions);
                if (!buffActive && estPlayerSpeed >= agent.speed - 0.2f)
                    AddReward(-0.02f * dt);
                SetMode(MonsterMode.Chase);
                break;
            }
            case HL.Investigate:
            {
                // HL에서 Investigate를 선택했지만, 소리 플랜이 없으면 근접 POI/포인트를 1회만 고정
                if (_enteredInvestigate && !_investActive)
                {
                    Vector3 seed;
                    if (!TryGetNearestValidPoint(generatorPOIs, transform.position, out seed))
                        if (!TryGetNearestValidPoint(wanderPoints, transform.position, out seed))
                            seed = lastHeardPos;
                    BuildInvestigatePlan(seed);
                }
                RunInvestigatePlan(dt, actions);
                SetMode(MonsterMode.Investigate);
                break;
            }
            case HL.Patrol:
            {
                bool arrived = !agent.pathPending && agent.hasPath &&
                               agent.remainingDistance <= Mathf.Max(patrolArriveTolerance, agent.stoppingDistance + 0.05f);

                bool verySlow = agent.velocity.sqrMagnitude < (patrolStuckSpeedEps * patrolStuckSpeedEps);
                bool isPartialPath = agent.pathStatus == NavMeshPathStatus.PathPartial;
                if (verySlow || isPartialPath) _patrolStuckTimer += dt; else _patrolStuckTimer = 0f;

                if (_enteredPatrol || !_patrolHasTarget || arrived || _patrolStuckTimer >= patrolStuckTime)
                {
                    _patrolTarget = PickPatrolTarget();
                    _patrolHasTarget = true;
                    _patrolStuckTimer = 0f;
                }

                MoveTo(_patrolTarget, buffActive ? buffedRunSpeed : runSpeed, actions);
                if (arrived) AddReward(+0.01f * dt);

                Transform poi = GetNearestPOI(transform.position);
                if (poi && Vector3.SqrMagnitude(transform.position - poi.position) < 3f*3f)
                    AddReward(+0.01f * dt);

                SetMode(MonsterMode.Patrol);
                break;
            }
            case HL.Ambush:
            default:
            {
                Vector3 predict = (player ? player.position + playerVel * ambushLeadSeconds : lastSeenPlayerPos);
                MoveTo(predict, buffActive ? buffedRunSpeed : runSpeed, actions);
                if (!PlayerCanSeeMe()) AddReward(+0.02f * dt);
                SetMode(MonsterMode.Ambush);
                break;
            }
        }

        DoCommon(dt, iSeePlayer);
        _lastHL = hl;
        if (player) { prevPlayerPos = player.position; prevPlayerPosTime = Time.time; }
    }

    // 공통 보상/이벤트/종료 처리
    void DoCommon(float dt, bool iSeePlayer)
    {
        AddReward(-0.02f * dt);
        if (iSeePlayer && Mode == MonsterMode.Chase) AddReward(+0.03f * dt);

        pathEvalTimer += dt;
        if (pathEvalTimer >= pathEvalInterval)
        {
            float curLen = GetPathLength(transform.position, player ? player.position : transform.position);
            if (lastPathLen > 0f && curLen + 0.2f < lastPathLen) AddReward(+0.02f);
            lastPathLen = curLen;
            pathEvalTimer = 0f;
        }

        agent.stoppingDistance = catchDistance * 0.8f;
        if (player && Vector3.Distance(transform.position, player.position) <= catchDistance)
        {
            AddReward(buffActive ? +1.5f : +1.0f);
            hitIdleTimer = idleAfterHitSeconds;
            agent.ResetPath();
            agent.isStopped = true;
            SetMode(MonsterMode.HitIdle);
            UpdateAnimatorFlags(false, false);
            return;
        }

        epTimer += dt;
        if (epTimer >= episodeTime)
        {
            AddReward(-0.2f);
            EndEpisode();
            return;
        }

        UpdateExportMode(iSeePlayer, (Time.time - lastHeardTime) < 6f, (HL)0, dt); // HL은 중요치 않음(래치용)
        bool animChase = (_mode == MonsterMode.Chase);
        bool animInvestigate = (_mode == MonsterMode.Investigate);
        UpdateAnimatorFlags(animChase && iSeePlayer, animInvestigate);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = (Input.GetKey(KeyCode.D) ? 1 : 0) + (Input.GetKey(KeyCode.A) ? -1 : 0);
        ca[1] = (Input.GetKey(KeyCode.W) ? 1 : 0) + (Input.GetKey(KeyCode.S) ? -1 : 0);

        var da = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.Alpha1)) da[0] = (int)HL.Stalk;
        else if (Input.GetKey(KeyCode.Alpha2)) da[0] = (int)HL.Chase;
        else if (Input.GetKey(KeyCode.Alpha3)) da[0] = (int)HL.Investigate;
        else if (Input.GetKey(KeyCode.Alpha4)) da[0] = (int)HL.Patrol;
        else if (Input.GetKey(KeyCode.Alpha5)) da[0] = (int)HL.Ambush;
        else da[0] = (int)HL.Patrol;
    }

    // ===== Investigate 플랜 =====
    void ResetInvestigatePlan()
    {
        _investPoints.Clear();
        _investIndex = -1;
        _investActive = false;
        investigateArrivedGiven = false;
        investigateUntil = 0f;
    }

    void BuildInvestigatePlan(Vector3 center)
    {
        ResetInvestigatePlan();

        // 0) 소리 지점
        if (TrySampleSafe(center, out var p0)) _investPoints.Add(p0);
        else _investPoints.Add(center);

        // 1..N) 스윕 포인트(반경 sweepMinRadius ~ investigateRadius)
        for (int i = 0; i < investigateSweepCount; i++)
        {
            for (int t = 0; t < 10; t++)
            {
                float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float r   = Random.Range(sweepMinRadius, investigateRadius);
                Vector3 cand = center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                if (TrySampleSafe(cand, out var ps))
                {
                    _investPoints.Add(ps);
                    break;
                }
            }
        }

        _investIndex = 0;
        _investActive = true;
    }

    void RunInvestigatePlan(float dt, ActionBuffers actions)
    {
        if (!_investActive || _investIndex < 0 || _investIndex >= _investPoints.Count)
        { ResetInvestigatePlan(); return; }

        Vector3 target = _investPoints[_investIndex];
        float dist2 = (transform.position - target).sqrMagnitude;

        if (_investIndex == 0 && dist2 <= 1f * 1f)
        {
            // 소리 지점 도착 → 잠깐 둘러보기
            if (!investigateArrivedGiven)
            {
                AddReward(+0.2f);
                investigateArrivedGiven = true;
                investigateUntil = Time.time + investigateHoldTime;
            }
            agent.isStopped = true;
            transform.Rotate(0f, rotateSpeed * dt, 0f);

            if (Time.time >= investigateUntil && investigateUntil > 0f)
            {
                _investIndex++; // 다음 스윕으로
            }
        }
        else
        {
            MoveTo(target, buffActive ? buffedRunSpeed : runSpeed, actions);
            if (dist2 <= 1.0f * 1.0f)
            {
                _investIndex++;
            }
        }

        if (_investIndex >= _investPoints.Count)
        {
            // 플랜 종료
            ResetInvestigatePlan();
        }
    }

    // ===== Utils =====
    bool LineOfSightClear(Vector3 from, Vector3 to, LayerMask mask)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        dir /= dist;
        return !Physics.Raycast(from, dir, dist, mask, QueryTriggerInteraction.Ignore);
    }

    bool CanSeePlayer(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (!player) return false;

        Vector3 v = player.position - eye.position;
        float d = v.magnitude;
        if (d > viewDistance) return false;

        Vector3 f = transform.forward; f.y = 0f;
        Vector3 flat = v; flat.y = 0f;
        if (Vector3.Angle(f, flat) > viewHalfAngle) return false;

        if (!LineOfSightClear(eye.position, player.position, occlusionMask))
            return false;

        pos = player.position;
        return true;
    }

    bool PlayerCanSeeMe()
    {
        if (!player) return false;
        Transform pEye = playerEye ? playerEye : player;

        Vector3 v = transform.position - pEye.position;
        float d = v.magnitude;
        if (d > playerViewDistance) return false;

        Vector3 pf = pEye.forward; pf.y = 0f;
        Vector3 flat = v; flat.y = 0f;
        if (Vector3.Angle(pf, flat) > playerViewHalfAngle) return false;

        var mask = (playerOcclusionMask.value == 0 ? occlusionMask : playerOcclusionMask);
        if (!LineOfSightClear(pEye.position, transform.position, mask))
            return false;

        return true;
    }

    void MoveTo(Vector3 baseTarget, float speed, ActionBuffers actions)
    {
        // 연속 오프셋(로컬)
        float ax = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float az = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        Vector3 worldOffset = transform.TransformDirection(new Vector3(ax, 0f, az)) * localMoveRadius;
        Vector3 final = baseTarget + worldOffset;

        agent.isStopped = false;
        agent.speed = speed;
        SetDestination(final);

        if (agent.hasPath)
        {
            Vector3 to = agent.steeringTarget - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 1e-6f)
            {
                var rot = Quaternion.LookRotation(to, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, rot, agent.angularSpeed * Time.deltaTime);
            }
        }
    }

    Transform GetNearestPOI(Vector3 from)
    {
        if (generatorPOIs == null || generatorPOIs.Length == 0) return null;
        float best = float.PositiveInfinity;
        Transform bestT = null;
        foreach (var t in generatorPOIs)
        {
            if (!t) continue;
            float d = (t.position - from).sqrMagnitude;
            if (d < best) { best = d; bestT = t; }
        }
        return bestT;
    }

    bool TrySampleSafe(Vector3 p, out Vector3 safe)
    {
        safe = p;
        if (NavMesh.SamplePosition(p, out var hit, 2.0f, NavMesh.AllAreas))
        {
            if (!IsNearWall(hit.position)) { safe = hit.position; return true; }
        }
        return false;
    }

    bool TryGetNearestValidPoint(Transform[] points, Vector3 from, out Vector3 pos)
    {
        pos = Vector3.zero;
        if (points == null || points.Length == 0) return false;

        float best = float.PositiveInfinity;
        Vector3 bestPos = Vector3.zero;
        foreach (var t in points)
        {
            if (!t) continue;
            if (TrySampleSafe(t.position, out var s))
            {
                float d = (s - from).sqrMagnitude;
                if (d < best) { best = d; bestPos = s; }
            }
        }
        if (best < float.PositiveInfinity) { pos = bestPos; return true; }
        return false;
    }

    bool TryPickRandomValidPoint(Transform[] points, out Vector3 pos)
    {
        pos = Vector3.zero;
        if (points == null || points.Length == 0) return false;

        int tries = Mathf.Min(points.Length, 12);
        for (int k = 0; k < tries; k++)
        {
            var t = points[Random.Range(0, points.Length)];
            if (!t) continue;
            if (TrySampleSafe(t.position, out var s)) { pos = s; return true; }
        }
        return false;
    }

    // 벽 이격 체크
    bool IsNearWall(Vector3 p)
    {
        return Physics.CheckSphere(p + Vector3.up * 0.4f, patrolMinClearance, occlusionMask, QueryTriggerInteraction.Ignore);
    }

    Vector3 PickPatrolTarget()
    {
        bool havePOI   = (generatorPOIs != null && generatorPOIs.Length > 0);
        bool choosePOI = havePOI && (Random.value < patrolPoiRatio);

        if (choosePOI)
        {
            if (TryPickRandomValidPoint(generatorPOIs, out var pos)) return pos;
        }
        if (TryPickRandomValidPoint(wanderPoints, out var pos2)) return pos2;

        return RandomPointInPatrolArea();
    }

    Vector3 RandomPointInPatrolArea()
    {
        Vector3 c = patrolCenter ? patrolCenter.position : _spawnPoint;
        for (int i = 0; i < patrolRandomSamples; i++)
        {
            Vector3 cand = c + Random.insideUnitSphere * patrolRadius; cand.y = c.y;
            if (NavMesh.SamplePosition(cand, out var hit, 2.5f, NavMesh.AllAreas))
            {
                if (!IsNearWall(hit.position)) return hit.position;
            }
        }
        return c;
    }

    void SetDestination(Vector3 world)
    {
        if (NavMesh.SamplePosition(world, out var hit, 2f, NavMesh.AllAreas))
        {
            if (!agent.hasPath || (agent.destination - hit.position).sqrMagnitude > 0.04f)
                agent.SetDestination(hit.position);
        }
    }

    Vector3 SampleNav(Vector3 world) =>
        NavMesh.SamplePosition(world, out var hit, 3f, NavMesh.AllAreas) ? hit.position : world;

    float GetPathLength(Vector3 start, Vector3 end)
    {
        if (!NavMesh.SamplePosition(start, out var s, 2f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(end,   out var e, 2f, NavMesh.AllAreas))
            return 9999f;

        var path = new NavMeshPath();
        NavMesh.CalculatePath(s.position, e.position, NavMesh.AllAreas, path);
        if (path.status != NavMeshPathStatus.PathComplete) return 9999f;

        float len = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return len;
    }

    void UpdateAnimatorFlags(bool chasing, bool investigating)
    {
        if (!anim) return;
        float spd = agent ? agent.velocity.magnitude : 0f;
        if (_hasSpeed)       anim.SetFloat(speedParam, spd);
        if (_hasChase)       anim.SetBool(chaseBool, chasing);
        if (_hasInvestigate) anim.SetBool(investigateBool, investigating);
    }

    // ── Chase 래치 & Mode 내보내기 ──
    bool HasChaseSignal(bool iSeePlayer, bool hasRecentNoise)
    {
        if (iSeePlayer) return true;
        if (Time.time - lastSeenPlayerTime < chaseMemoryTime) return true;
        if (hasRecentNoise && Vector3.Distance(transform.position, lastHeardPos) <= chaseNoiseMaxDistance) return true;
        return false;
    }

    void UpdateExportMode(bool iSeePlayer, bool hasRecentNoise, HL hl, float dt)
    {
        bool rawIntent = (Mode == MonsterMode.Chase || hl == HL.Chase || hl == HL.Ambush);
        bool signal    = HasChaseSignal(iSeePlayer, hasRecentNoise);
        bool tooFar    = player ? Vector3.Distance(transform.position, player.position) > chaseMaxExportDistance : true;

        if (rawIntent && signal && !tooFar)
        {   chaseOnTimer  += dt; chaseOffTimer = 0f; if (!chaseLatched && chaseOnTimer >= chaseAcquireTime) chaseLatched = true; }
        else
        {   chaseOnTimer   = 0f; chaseOffTimer += dt; if (chaseLatched && chaseOffTimer >= chaseReleaseTime) chaseLatched = false; }

        if (hitIdleTimer > 0f)                SetMode(MonsterMode.HitIdle);
        else if (chaseLatched)                SetMode(MonsterMode.Chase);
        else if (Mode == MonsterMode.Stalk)   SetMode(MonsterMode.Stalk);
        else if (Mode == MonsterMode.Investigate) { /* 그대로 */ }
        else if (Mode == MonsterMode.Ambush)  { /* 그대로 */ }
        else if (Mode == MonsterMode.Patrol)  { /* 그대로 */ }
    }
}
