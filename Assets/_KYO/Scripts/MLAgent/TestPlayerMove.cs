using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class TestPlayerMove : MonoBehaviour
{
    public enum MoveState { Idle, Walk, Run, Scan, Flee }

    [Header("Refs")]
    public Animator anim;
    public StateNoiseEmitter noise;
    public Transform playerEye;
    public Transform monster;

    [Header("Monster Status (Enum)")]
    [Tooltip("IMonsterStatus 구현 컴포넌트(예: MLMonsterAgent)를 Drag&Drop")]
    public MonoBehaviour monsterStatusSource;
    IMonsterStatus _monsterStatus;

    [Header("Speeds")]
    public float walkSpeed = 2f;
    public float runSpeed  = 5f;
    public float turnSpeed = 540f;

    [Header("Flee Triggers")]
    public float nearFleeRadius = 10f;          // 근접 판정
    public float seeFleeViewDist = 20f;         // 시야 거리
    [Range(0,180f)] public float playerFOVHalfAngle = 70f;
    public LayerMask playerObstacleMask;

    [Header("Generator Work")]
    public float interactRadius = 1.8f;
    public float searchInterval = 0.5f;
    public float roamRadius = 8f;
    public float arriveTolerance = 0.6f;

    [Header("Work Look (2초마다 90°)")]
    public bool  workLookWhileWorking = true;
    public float workLookInterval = 2.0f;
    public float workLookAngularSpeed = 360f;
    public float workSnapAngle = 90f;

    [Header("Flee Silence")]
    public float fleeSilenceDelay = 3.0f;

    [Header("Run Anchors (RunObj)")]
    public Transform runObj;                     // 도망 목적지 후보들
    public float anchorArriveTol = 0.9f;

    [Header("Anti-Trap")]
    public float minWallClearance = 0.7f;
    public float pushOffWallDistance = 1.2f;
    public float stuckSpeedEps = 0.05f;
    public float stuckCheckTime = 1.0f;

    // ───────── 발전기 선택 다양화/안정화 ─────────
    [Header("Generator Picking (Diversity/Robust)")]
    [Tooltip("최근에 사용한 발전기를 다시 고르지 않기 위한 쿨다운(초)")]
    public float generatorRevisitCooldown = 20f;
    [Tooltip("몬스터 회피 ‘완화’ 반경(이 안쪽은 패널티만). 하드 회피는 아래 값 사용")]
    public float monsterAvoidRadiusAtGen = 12f;
    [Tooltip("몬스터 하드 회피 반경(이 안쪽은 완전 제외)")]
    public float monsterHardAvoidRadius = 6f;
    [Tooltip("몬스터에 가까우면 점수에서 빼는 가중치(1m당)")]
    public float monsterProximityPenaltyPerMeter = 0.6f;
    [Tooltip("상위 몇 개 후보 중 무작위 선택(패턴 고착 방지)")]
    public int generatorTopK = 3;
    [Tooltip("같은 발전기 재선택 페널티(점수 차감)")]
    public float sameGenPenalty = 15f;
    [Tooltip("진행도 가산치(완료에 가까울수록 점수 ↑)")]
    public float progressBias = 12f;
    [Tooltip("점수에 소량의 무작위 흔들림(패턴 고착 방지)")]
    public float pickJitter = 0.25f;
    [Tooltip("후보가 0이면 제약을 풀고 가장 가까운 미완료 발전기로 가는 최소 대기(초)")]
    public float desperateFallbackDelay = 1.0f;

    // ── 런타임 ──
    public MoveState State { get; private set; } = MoveState.Scan;

    NavMeshAgent _agent;
    Generator _targetGen;
    Generator _lastPickedGen;
    readonly Dictionary<Generator, float> _lastPickTime = new();

    Vector3 _spawn, _roamTarget;
    float _searchTimer;

    float _workLookTimer, _workTargetYaw;

    readonly List<Transform> _anchors = new();
    Transform _currentAnchor = null;
    int _lastAnchorIndex = -1;

    float _fleeEnterTime;
    bool  _muteNoise;

    float _stuckTimer = 0f;

    // 일반 이동 언스턱(발전기 향해 가는 중에도 체크)
    float _moveStuckTimer = 0f;
    Vector3 _lastPos;
    int _sameTargetRepathTries = 0;

    // 절박 모드용 타이머
    float _noCandidateSince = -1f;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (!noise) noise = GetComponent<StateNoiseEmitter>();
        _agent.updateRotation = false;
        _agent.autoRepath = true;
        _agent.stoppingDistance = Mathf.Max(0.1f, interactRadius * 0.6f);

        _monsterStatus = monsterStatusSource as IMonsterStatus;
        if (_monsterStatus == null && monsterStatusSource != null)
            Debug.LogWarning($"{name}: monsterStatusSource는 IMonsterStatus를 구현해야 합니다.");
    }

    void Start()
    {
        _spawn = transform.position;
        PickNewRoamPoint();
        _workLookTimer = workLookInterval;
        _workTargetYaw = transform.eulerAngles.y;
        GatherRunAnchors();
        _lastPos = transform.position;
    }

    void GatherRunAnchors()
    {
        _anchors.Clear();
        if (!runObj) return;
        for (int i = 0; i < runObj.childCount; i++)
        {
            var t = runObj.GetChild(i);
            if (t && t.gameObject.activeInHierarchy) _anchors.Add(t);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        bool dangerNearOrSeen = NearOrSeen();       // 근접 OR 시야
        bool chasing          = IsMonsterChasing(); // 몬스터 Chase 상태
        bool mustFlee = dangerNearOrSeen;

        if (State != MoveState.Flee && mustFlee)
        {
            SafeEndWork(); // 도망 전환 시 현재 발전기 작업 종료(소리 끄기)

            _fleeEnterTime = Time.time;
            _muteNoise = false;
            _stuckTimer = 0f;

            if (TryPushOffWall(out var push)) SetDestinationOnNavMesh(push);

            _currentAnchor = PickAnchor(chasing, excludeIndex: _lastAnchorIndex);
            if (_currentAnchor == null)
            {
                Vector3 fallback = ComputeSimpleFleePoint();
                SetDestinationOnNavMesh(fallback);
            }
            else
            {
                _lastAnchorIndex = (_anchors != null) ? _anchors.IndexOf(_currentAnchor) : -1;
                SetDestinationOnNavMesh(_currentAnchor.position);
            }

            State = MoveState.Flee;
        }

        if (State == MoveState.Flee) FleeTick(dt);
        else                         BrainWork(dt);

        MoveUpdate(dt);
        WorkUpdate();
        WorkLookTick(dt);
        UpdateAnimator();
    }

    // 현재 맡고 있는 발전기 작업을 안전하게 종료
    void SafeEndWork()
    {
        if (_targetGen != null)
        {
            _targetGen.EndWork(gameObject);
            _targetGen = null;
        }
    }

    // ── 도망 상태 틱 ──
    void FleeTick(float dt)
    {
        if (!_muteNoise && Time.time - _fleeEnterTime >= fleeSilenceDelay)
            _muteNoise = true;

        bool movingSlow = _agent.velocity.sqrMagnitude < stuckSpeedEps * stuckSpeedEps;
        bool farFromDest = _agent.hasPath && _agent.remainingDistance > (anchorArriveTol + 0.4f);
        if (movingSlow && farFromDest) _stuckTimer += dt; else _stuckTimer = 0f;
        if (_stuckTimer >= stuckCheckTime) { _stuckTimer = 0f; HopToAnotherAnchor(); }

        if (AtFleeGoal())
        {
            if (IsMonsterChasing() && NearOrSeen())
            {
                HopToAnotherAnchor();
            }
            else
            {
                _muteNoise = false;
                State = MoveState.Scan;
                _currentAnchor = null;
            }
        }

        CallNoise(_muteNoise ? CharacterMoveState.Idle : CharacterMoveState.Run);
    }

    bool AtFleeGoal()
    {
        if (_currentAnchor != null) return Arrived(_currentAnchor.position, anchorArriveTol);
        return _agent.hasPath && _agent.remainingDistance <= anchorArriveTol + 0.2f;
    }

    void HopToAnotherAnchor()
    {
        var next = PickAnchor(IsMonsterChasing(), excludeIndex: _lastAnchorIndex);
        if (next != null)
        {
            _currentAnchor = next;
            _lastAnchorIndex = _anchors.IndexOf(_currentAnchor);
            SetDestinationOnNavMesh(_currentAnchor.position);
        }
        else
        {
            Vector3 fallback = ComputeSimpleFleePoint();
            SetDestinationOnNavMesh(fallback);
            _currentAnchor = null;
        }
    }

    // ── 일반 두뇌(발전기 찾기/배회) ──
    void BrainWork(float dt)
    {
        _searchTimer -= dt;

        // 목표 발전기가 없거나 쓸 수 없으면 새로 선택
        if ((_targetGen == null || _targetGen.IsCompleted) && _searchTimer <= 0f)
        {
            _searchTimer = searchInterval;
            SafeEndWork(); // 재선택 전에 반드시 해제
            _targetGen = PickNextGenerator();
            _sameTargetRepathTries = 0;
        }

        // 몬스터 너무 근접한 발전기는 회피(목표 재선택) — 완화된 기준
        if (_targetGen != null && monster)
        {
            float dMon = Vector3.Distance(monster.position, _targetGen.transform.position);
            if (dMon < monsterHardAvoidRadius) // 정말 붙은 수준만 강제 재선택
            {
                SafeEndWork();
                _targetGen = PickNextGenerator(exclude: _targetGen);
                _sameTargetRepathTries = 0;
            }
        }

        // 이동/언스턱: 발전기로 가는 중인데 속도가 너무 느리고 아직 멀면 경로 재계산 → 반복 시 다른 발전기 선택
        if (_targetGen != null)
        {
            float sqToGen = SqrXZ(transform.position, _targetGen.transform.position);
            bool far = sqToGen > interactRadius * interactRadius * 4f;
            bool slow = _agent.velocity.sqrMagnitude < stuckSpeedEps * stuckSpeedEps;

            if (far && slow)
            {
                _moveStuckTimer += Time.deltaTime;
                if (_moveStuckTimer >= stuckCheckTime)
                {
                    _moveStuckTimer = 0f;

                    // 우선 같은 목표로 경로 재설정 시도
                    if (TryPickClear(_targetGen.transform.position, out var clear))
                    {
                        SetDestinationOnNavMesh(clear);
                        _sameTargetRepathTries++;
                    }

                    // 여러 번 재설정해도 발전이 없으면 다른 발전기 선택
                    if (_sameTargetRepathTries >= 2)
                    {
                        SafeEndWork();
                        _targetGen = PickNextGenerator(exclude: _targetGen);
                        _sameTargetRepathTries = 0;
                    }
                }
            }
            else
            {
                _moveStuckTimer = 0f;
                _sameTargetRepathTries = 0;
            }
        }

        if (_targetGen != null)
        {
            SetDestinationOnNavMesh(_targetGen.transform.position);
            CallNoise(CharacterMoveState.Walk);
        }
        else
        {
            if (!_agent.hasPath || Arrived(_roamTarget)) PickNewRoamPoint();
            SetDestinationOnNavMesh(_roamTarget);
            CallNoise(CharacterMoveState.Walk);
        }
    }

    // ── RunObj 앵커 선택 ──
    Transform PickAnchor(bool chasing, int excludeIndex = -1)
    {
        if (_anchors == null || _anchors.Count == 0) return null;

        Vector3 away = Vector3.forward;
        if (monster)
        {
            away = (transform.position - monster.position); away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) away = transform.forward;
            away.Normalize();
        }

        float bestScore = float.NegativeInfinity;
        Transform best = null;

        for (int i = 0; i < _anchors.Count; i++)
        {
            if (i == excludeIndex) continue;
            var a = _anchors[i];
            if (!a) continue;

            if (!TryPickClear(a.position, out var clearPos)) continue;

            float distBoss = monster ? Vector3.Distance(clearPos, monster.position) : 0f;
            float distMe   = Vector3.Distance(clearPos, transform.position);
            float awayDot  = monster ? Vector3.Dot((clearPos - transform.position).normalized, away) : 0f;
            float clearance= Mathf.Min(GetEdgeClearance(clearPos), 2f);

            float score =
                distBoss * 0.7f +
                Mathf.Max(awayDot, 0f) * (chasing ? 4.5f : 3.0f) +
                clearance * 1.6f -
                distMe * 0.15f;

            if (score > bestScore) { bestScore = score; best = a; }
        }

        return best;
    }

    Vector3 ComputeSimpleFleePoint()
    {
        if (!monster) return transform.position;

        if (TryPushOffWall(out var push)) return push;

        Vector3 away = (transform.position - monster.position); away.y = 0f;
        if (away.sqrMagnitude < 1e-6f) away = transform.forward;
        away.Normalize();

        Vector3 raw = monster.position + away * 12f;
        if (TryPickClear(raw, out var pos)) return pos;
        return transform.position + away * 3f;
    }

    // ── Movement / Look / Anim ──
    void MoveUpdate(float dt)
    {
        bool working = _targetGen && !_targetGen.IsCompleted &&
                       SqrXZ(transform.position, _targetGen.transform.position) <= interactRadius * interactRadius &&
                       State != MoveState.Flee;

        if (_agent.hasPath && !working)
        {
            Vector3 to = _agent.steeringTarget - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                var rot = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, turnSpeed * dt);
            }
        }

        _agent.speed = (State == MoveState.Flee || State == MoveState.Run) ? runSpeed : walkSpeed;
        _agent.isStopped = _agent.speed <= 0.01f;

        // 일반 이동 중 “제자리에서 왔다갔다” 완화: 위치 변화 감시
        float movedSqr = (transform.position - _lastPos).sqrMagnitude;
        _lastPos = transform.position;
        if (State != MoveState.Flee && _targetGen != null && movedSqr < 0.0004f)
        {
            _moveStuckTimer += dt;
            if (_moveStuckTimer > stuckCheckTime * 1.5f)
            {
                _moveStuckTimer = 0f;
                // 같은 목표 재경로 → 실패 반복 시 다른 목표
                if (!TryPickClear(_targetGen.transform.position, out var clear))
                {
                    SafeEndWork();
                    _targetGen = PickNextGenerator(exclude: _targetGen);
                }
                else
                {
                    SetDestinationOnNavMesh(clear);
                }
            }
        }
    }

    void WorkLookTick(float dt)
    {
        bool working = _targetGen && !_targetGen.IsCompleted &&
                       SqrXZ(transform.position, _targetGen.transform.position) <= interactRadius * interactRadius &&
                       State != MoveState.Flee;

        if (!workLookWhileWorking || !working) { _workLookTimer = workLookInterval; return; }

        _workLookTimer -= dt;
        if (_workLookTimer <= 0f)
        {
            _workLookTimer = workLookInterval;
            _workTargetYaw = Mathf.Repeat(transform.eulerAngles.y + workSnapAngle, 360f);
        }

        float curYaw = transform.eulerAngles.y;
        float newYaw = Mathf.MoveTowardsAngle(curYaw, _workTargetYaw, workLookAngularSpeed * dt);
        transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
    }

    void WorkUpdate()
    {
        if (_targetGen == null) return;

        float sq = SqrXZ(transform.position, _targetGen.transform.position);

        // 목표 발전기 범위에 들어가면 "최근 사용" 기록(재선택 방지)
        if (sq <= (interactRadius * interactRadius * 1.6f))
        {
            _lastPickedGen = _targetGen;
            _lastPickTime[_targetGen] = Time.time;
        }

        if (sq <= interactRadius * interactRadius && !_targetGen.IsCompleted && State != MoveState.Flee)
            _targetGen.TryBeginWork(gameObject);
        else
            _targetGen.EndWork(gameObject);
    }

    void UpdateAnimator()
    {
        if (!anim) return;
        bool moving = _agent.velocity.sqrMagnitude > 0.05f;
        anim.SetBool("Run",  (State == MoveState.Flee) && moving);
        anim.SetBool("Walk", (State == MoveState.Walk || State == MoveState.Scan) && moving);
    }

    // ── 위협 판정 유틸 ──
    bool IsMonsterChasing()
    {
        return _monsterStatus != null && _monsterStatus.CurrentMode == MonsterMode.Chase;
    }

    bool NearOrSeen()
    {
        if (!monster || !playerEye) return false;

        if (DistanceToMonster() <= nearFleeRadius) return true;

        Vector3 from = playerEye.position;
        Vector3 to   = monster.position;
        Vector3 v    = to - from;

        if (v.magnitude > seeFleeViewDist) return false;

        Vector3 fwd  = playerEye.forward; fwd.y = 0f;
        Vector3 flat = v; flat.y = 0f;
        if (Vector3.Angle(fwd, flat) > playerFOVHalfAngle) return false;

        if (Physics.Raycast(from, v.normalized, out var hit, seeFleeViewDist, playerObstacleMask))
            return false;

        return true;
    }

    bool Arrived(Vector3 target, float tol = -1f)
    {
        float t = (tol > 0f) ? tol : arriveTolerance;
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = target; b.y = 0f;
        return (a - b).sqrMagnitude <= t * t;
    }

    float DistanceToMonster()
    {
        if (!monster) return float.MaxValue;
        return Vector3.Distance(transform.position, monster.position);
    }

    void SetDestinationOnNavMesh(Vector3 world)
    {
        if (NavMesh.SamplePosition(world, out var hit, 2f, NavMesh.AllAreas))
        {
            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                if (!_agent.hasPath || (hit.position - _agent.destination).sqrMagnitude > 0.04f)
                    _agent.SetDestination(hit.position);
            }
        }
    }

    static float SqrXZ(Vector3 a, Vector3 b) { a.y=0; b.y=0; return (a-b).sqrMagnitude; }

    void PickNewRoamPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 c = Random.insideUnitCircle * roamRadius;
            Vector3 cand = _spawn + new Vector3(c.x, 0, c.y);
            if (NavMesh.SamplePosition(cand, out var hit, 2.5f, NavMesh.AllAreas))
            { _roamTarget = hit.position; return; }
        }
        _roamTarget = _spawn;
    }

    // ───────── 발전기 선택 로직 (완화 + 진행도 우선 + 절박 폴백) ─────────
    Generator PickNextGenerator(Generator exclude = null)
    {
        Generator[] gens = GameObject.FindObjectsOfType<Generator>();
        var scored = new List<(Generator g, float score)>();

        foreach (var g in gens)
        {
            if (!g || g.IsCompleted) continue;
            if (exclude != null && g == exclude) continue;

            Vector3 gp = g.transform.position;

            float monDist = monster ? Vector3.Distance(monster.position, gp) : 999f;
            // 하드 회피: 아주 붙어 있으면 제외
            if (monDist < monsterHardAvoidRadius) continue;

            float selfDist = Vector3.Distance(transform.position, gp);

            // 최근 사용 패널티
            float recencyPenalty = 0f;
            if (_lastPickTime.TryGetValue(g, out var t))
            {
                float remain = Mathf.Clamp01((generatorRevisitCooldown - (Time.time - t)) / generatorRevisitCooldown);
                recencyPenalty = remain * sameGenPenalty;
            }
            if (_lastPickedGen != null && g == _lastPickedGen)
                recencyPenalty += sameGenPenalty;

            // 몬스터 근접 패널티(완화): avoidRadius 안쪽이면 거리만큼 감점, 바깥이면 보너스 아님(0)
            float monPenalty = 0f;
            if (monDist < monsterAvoidRadiusAtGen)
                monPenalty = (monsterAvoidRadiusAtGen - monDist) * monsterProximityPenaltyPerMeter;

            // 진행도 가산치: 마무리 우선(더 진행된 것일수록 +)
            float progressBoost = (g.progress) * progressBias;

            float score =
                (-selfDist * 0.35f) // 가까울수록 좋음
                - monPenalty         // 몬스터 가깝다면 감점(완전 제외는 아님)
                - recencyPenalty     // 최근 사용 감점
                + progressBoost      // 진행 많이 된 곳 가산
                + Random.Range(-pickJitter, pickJitter);

            scored.Add((g, score));
        }

        if (scored.Count == 0)
        {
            // 절박 모드: 일정 시간 이상 후보 0이면 모든 제약을 풀고 “가장 가까운 미완료 발전기” 선택
            if (_noCandidateSince < 0f) _noCandidateSince = Time.time;

            if (Time.time - _noCandidateSince >= desperateFallbackDelay)
            {
                _noCandidateSince = -1f;
                Generator nearest = null; float bestD = float.MaxValue;
                foreach (var g in gens)
                {
                    if (!g || g.IsCompleted) continue;
                    float d = Vector3.Distance(transform.position, g.transform.position);
                    if (d < bestD) { bestD = d; nearest = g; }
                }
                return nearest;
            }

            return null;
        }
        else
        {
            _noCandidateSince = -1f;
        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));
        int k = Mathf.Clamp(generatorTopK, 1, scored.Count);
        int idx = Random.Range(0, k);
        var chosen = scored[idx].g;

        _lastPickedGen = chosen;
        _lastPickTime[chosen] = Time.time;

        return chosen;
    }

    void CallNoise(CharacterMoveState st)
    {
        if (_muteNoise) return;
        if (noise != null) noise.SetState(st);
    }

    // ── Anti-Trap ──
    float GetEdgeClearance(Vector3 p)
    {
        if (NavMesh.FindClosestEdge(p, out var edge, NavMesh.AllAreas))
            return edge.distance;
        return Mathf.Infinity;
    }

    bool TryPickClear(Vector3 raw, out Vector3 pos)
    {
        pos = raw;
        if (!NavMesh.SamplePosition(raw, out var hit, 3f, NavMesh.AllAreas))
            return false;

        if (GetEdgeClearance(hit.position) < minWallClearance)
        {
            if (NavMesh.FindClosestEdge(hit.position, out var edge, NavMesh.AllAreas))
            {
                Vector3 nudged = hit.position + edge.normal *
                                 (minWallClearance - edge.distance + 0.2f);
                if (!NavMesh.SamplePosition(nudged, out hit, 2f, NavMesh.AllAreas))
                    return false;
                if (GetEdgeClearance(hit.position) < minWallClearance) return false;
            }
            else return false;
        }

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path))
            return false;
        if (path.status != NavMeshPathStatus.PathComplete) return false;

        pos = hit.position;
        return true;
    }

    bool TryPushOffWall(out Vector3 dest)
    {
        dest = transform.position;
        if (!NavMesh.FindClosestEdge(transform.position, out var edge, NavMesh.AllAreas))
            return false;
        if (edge.distance >= minWallClearance) return false;

        Vector3 dir = (edge.normal + transform.forward).normalized;
        Vector3 raw = transform.position + dir *
                      Mathf.Max(pushOffWallDistance, minWallClearance - edge.distance + 0.5f);

        if (NavMesh.SamplePosition(raw, out var hit, 2.5f, NavMesh.AllAreas))
        { dest = hit.position; return true; }
        return false;
    }
}
