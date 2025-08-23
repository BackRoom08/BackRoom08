// TestPlayerMove.cs — 도망 트리거 보정:
// 1) 도착 시에는 "추격 중"만으로는 다시 도망 가지 않음.
//    → 추격 중 AND (시야 내 OR 근접) 일 때만 연속 도망.
// 2) 평소엔 근접/시야만으로도 도망 시작(즉시 위협 회피).

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

    // ── 런타임 ──
    public MoveState State { get; private set; } = MoveState.Scan;

    NavMeshAgent _agent;
    Generator _targetGen;
    Vector3 _spawn, _roamTarget;
    float _searchTimer;

    float _workLookTimer, _workTargetYaw;

    List<Transform> _anchors = new List<Transform>();
    Transform _currentAnchor = null;
    int _lastAnchorIndex = -1;

    float _fleeEnterTime;
    bool  _muteNoise;

    float _stuckTimer = 0f;

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

        // ── 위협 판정 ──
        bool dangerNearOrSeen = NearOrSeen();         // 근접 OR 시야
        bool chasing          = IsMonsterChasing();   // 몬스터 외부 상태(Chase 래치)

        // ▶ 도망 시작 조건
        //    - 근접/시야 위협이면 즉시 도망
        //    - (원한다면) chasing && NearOrSeen 만으로 제한하려면 아래 한 줄로 바꿔도 됨.
        //      bool mustFlee = chasing && dangerNearOrSeen;
        bool mustFlee = dangerNearOrSeen;

        if (State != MoveState.Flee && mustFlee)
        {
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

    // ── 도망 상태 틱 ──
    void FleeTick(float dt)
    {
        if (!_muteNoise && Time.time - _fleeEnterTime >= fleeSilenceDelay)
            _muteNoise = true;

        // 스턱 → 다른 앵커
        bool movingSlow = _agent.velocity.sqrMagnitude < stuckSpeedEps * stuckSpeedEps;
        bool farFromDest = _agent.hasPath && _agent.remainingDistance > (anchorArriveTol + 0.4f);
        if (movingSlow && farFromDest) _stuckTimer += dt; else _stuckTimer = 0f;
        if (_stuckTimer >= stuckCheckTime) { _stuckTimer = 0f; HopToAnotherAnchor(); }

        // ▶ 도착 시 판정 변경:
        //    예전: IsMonsterChasing() 이면 무조건 다음 앵커로 도망
        //    지금: 추격 중 AND (시야 OR 근접) 일 때만 계속 도망, 아니면 복귀
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
        if ((_targetGen == null || _targetGen.IsCompleted) && _searchTimer <= 0f)
        {
            _searchTimer = searchInterval;
            _targetGen = FindNearestGenerator();
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
        return _monsterStatus != null && _monsterStatus.Mode == MonsterMode.Chase;
    }

    bool NearOrSeen()
    {
        if (!monster || !playerEye) return false;

        // 거리
        if (DistanceToMonster() <= nearFleeRadius) return true;

        // 시야
        Vector3 from = playerEye.position;
        Vector3 to   = monster.position;
        Vector3 v    = to - from;

        if (v.magnitude > seeFleeViewDist) return false;

        Vector3 fwd  = playerEye.forward; fwd.y = 0f;
        Vector3 flat = v; flat.y = 0f;
        if (Vector3.Angle(fwd, flat) > playerFOVHalfAngle) return false;

        // 가림물 체크(맞으면 위협 아님)
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

    Generator FindNearestGenerator()
    {
        Generator[] gens = GameObject.FindObjectsOfType<Generator>();
        Generator best = null; float bestD = float.MaxValue;
        foreach (var g in gens)
        {
            if (!g || g.IsCompleted) continue;
            float d = SqrXZ(transform.position, g.transform.position);
            if (d < bestD) { bestD = d; best = g; }
        }
        return best;
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
