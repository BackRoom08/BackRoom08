using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 자동 플레이어(싱글 모드, NavMeshAgent 전용)
/// - 발전기 자동 탐색/작업
/// - 주변 스캔(제자리 회전)으로 목표 탐색
/// - 몬스터가 보면 도망(Flee) → 일정 거리/시야차단 되면 다시 발전기 찾기/걷기
/// - 소리: idle/crouch는 무음, 작업 중엔 Walk로 소리 발생(noise.SetState(Walk))
/// - Animator 파라미터: Walk, Run, Crouch, CrouchWalk (기존 그대로 사용)
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class TestPlayerMove : MonoBehaviour
{
    // ▶ Scan/Flee 상태를 추가 (기존 유지)
    public enum MoveState { Idle, Walk, Run, Crouch, Exhaustion, Scan, Flee }

    [Header("Refs")]
    public Animator anim;                 // (선택) 없으면 애니 부분만 건너뜀
    public StateNoiseEmitter noise;       // 필수: 소리 상태 호출용
    public Transform cam;                 // (선택) 앉기 시 카메라 높이 조절

    [Header("Speeds")]
    public float walkSpeed = 2.4f;
    public float runSpeed = 5.2f;
    public float crouchSpeed = 1.6f;
    public float exhaustionSpeed = 1.8f;
    public float rotationSpeed = 540f;    // 초당 회전(deg)

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float runDrainPerSec = 22f;
    public float regenPerSecWalk = 12f;
    public float regenPerSecIdle = 18f;
    public float regenPerSecCrouch = 20f;
    public float staminaHealDelay = 2.0f;     // 달리기 멈춘 뒤 회복 시작 지연
    public float staminaToReEnableRun = 25f;  // 탈진 후 다시 뛸 수 있는 임계치

    [Header("Crouch (Camera)")]
    public float standHeight = 1.7f;
    public float crouchHeight = 1.0f;

    [Header("Generator Work")]
    public float interactRadius = 1.8f;       // 작업 시작/유지 거리
    public float searchInterval = 0.5f;       // 발전기 재탐색 간격
    public float roamRadius = 8f;             // 발전기 없을 때 배회 반경
    public float arriveTolerance = 0.6f;      // 목적지 도착 판정 여유
    public bool preferCrouchWhileRoam = false;

    // ▶ 몬스터 인식/도망 관련
    [Header("Monster Perception (FOV + LOS)")]
    public Transform monster;                 // 몬스터 본체
    public Transform monsterEye;              // 몬스터 눈(없으면 monster)
    public LayerMask obstacleMask;            // 가림체(벽/기둥) 레이어
    public float monsterViewDistance = 22f;
    [Range(0,180f)] public float monsterViewHalfAngle = 60f;

    [Header("Scan & Flee")]
    public float scanDuration = 1.4f;         // 스캔 시간
    public float scanAngularSpeed = 180f;     // 스캔 중 회전 속도
    public float fleeDistance = 10f;          // 1차 도망 목표 반경
    public float fleeResampleWhenClose = 0.8f;// 목표점에 너무 가까우면 재샘플

    // ─────────────────────────────────────────────────────────────────────

    public MoveState CurrentState { get; private set; } = MoveState.Idle;
    public float Stamina => _stamina;
    public Generator CurrentTarget => _targetGen;

    NavMeshAgent _agent;
    Generator _targetGen;
    Vector3 _spawn;
    Vector3 _roamTarget;

    float _searchTimer;
    float _stamina;
    float _healTimer;
    bool  _isRecovering;        // 회복 시작 플래그
    bool  _isWorking;           // 발전기 작업 중
    bool  _isCrouch;            // 앉기 상태

    // ▶ 스캔/도망용 내부 변수
    float _scanTimer; int _scanDir = 1;
    Vector3 _fleeTarget;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (!noise) noise = GetComponent<StateNoiseEmitter>();

        // NavMeshAgent 품질/권장 세팅
        _agent.updatePosition = true;
        _agent.updateRotation = false; // 회전은 우리가 처리(코너 긁힘 감소)
        _agent.autoRepath = true;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        _agent.avoidancePriority = 50;
        _agent.stoppingDistance = Mathf.Max(0.1f, interactRadius * 0.6f);
    }

    void Start()
    {
        _spawn = transform.position;
        _stamina = maxStamina;

        if (cam) cam.localPosition = new Vector3(cam.localPosition.x, standHeight, cam.localPosition.z);
        PickNewRoamPoint();

        // 시작은 스캔으로 자연스럽게
        SetState(MoveState.Scan);
        _scanTimer = scanDuration;
        _scanDir = (Random.value < 0.5f) ? -1 : 1;
    }

    void Update()
    {
        // ▶ 먼저 "몬스터가 날 보는지" 체크해서 상태 전환 트리거
        bool seen = CheckSeenByMonster();

        if (seen && CurrentState != MoveState.Flee)
        {
            // 도망 타깃(시야 끊기는 지점) 계산 후 도망 상태 진입
            _fleeTarget = FindCoverPoint();
            SetDestinationOnNavMesh(_fleeTarget);
            SetState(MoveState.Flee);
        }
        else if (!seen && CurrentState == MoveState.Flee)
        {
            // 안 보이면: 도망 목표에 충분히 근접 시 복귀(스캔 또는 발전기 찾기)
            if (Arrived(_fleeTarget, fleeResampleWhenClose))
                SetState(MoveState.Scan);
        }

        BrainUpdate(Time.deltaTime);          // 목표/작업 판단 (Flee/Scan은 별 분기)
        StateAndStaminaUpdate(Time.deltaTime);// 상태 전환 + 소리 호출 + 스태미너
        MoveUpdate(Time.deltaTime);           // 회전/이동(Agent 경로 사용)
        WorkUpdate();                         // TryBeginWork / EndWork
        UpdateAnimator();                     // 애니 파라미터 갱신
    }

    // ─────────────────────────────────────────────────────────────────────
    // 목표/작업 판단
    void BrainUpdate(float dt)
    {
        // ▶ 도망 중일 땐 발전기 로직 불필요
        if (CurrentState == MoveState.Flee)
        {
            // 계속 보이면 도망 목표를 재샘플하여 더 가리기 좋은 곳으로 갱신
            if (CheckSeenByMonster() && Arrived(_fleeTarget, fleeResampleWhenClose))
            {
                _fleeTarget = FindCoverPoint();
                SetDestinationOnNavMesh(_fleeTarget);
            }
            return;
        }

        // ▶ 스캔 상태: 제자리 회전 + 가벼운 배회 + 주기적 탐색
        if (CurrentState == MoveState.Scan)
        {
            _scanTimer -= dt;

            // 제자리 회전(주변 살피기)
            transform.Rotate(0f, _scanDir * scanAngularSpeed * dt, 0f);

            // 주기적 발전기 찾기
            _searchTimer -= dt;
            if (_searchTimer <= 0f)
            {
                _searchTimer = searchInterval;
                _targetGen = FindNearestGenerator();
                if (_targetGen != null)
                {
                    // 스태미너가 있으면 바로 Run, 없으면 Walk로 출발
                    SetDestinationOnNavMesh(_targetGen.transform.position);
                    SetState(_stamina > 0.01f ? MoveState.Run : MoveState.Walk);
                    return;
                }
            }

            // 배회 포인트로 슬쩍 이동(정체 방지)
            if (!HasPath() || Arrived(_roamTarget))
                PickNewRoamPoint();
            SetDestinationOnNavMesh(_roamTarget);

            // 스캔 시간이 지나도 못 찾으면 방향만 바꿔 반복
            if (_scanTimer <= 0f)
            {
                _scanTimer = scanDuration;
                _scanDir *= -1;
            }
            return;
        }

        // ▶ 그 외(기존 로직): 발전기 중심으로 행동
        _searchTimer -= dt;

        if (_targetGen == null || _targetGen.IsCompleted)
        {
            if (_searchTimer <= 0f)
            {
                _searchTimer = searchInterval;
                _targetGen = FindNearestGenerator();
            }

            // 발전기가 없으면 배회
            if (_targetGen == null)
            {
                if (!HasPath() || Arrived(_roamTarget))
                    PickNewRoamPoint();
                _isWorking = false;

                // 발전기 못 찾으면 스캔으로 전환
                SetState(MoveState.Scan);
                return;
            }
        }

        // 발전기 목적지 설정(항상 NavMesh 위로)
        SetDestinationOnNavMesh(_targetGen.transform.position);

        // 범위 내면 작업 플래그
        float sq = SqrXZ(transform.position, _targetGen.transform.position);
        if (sq <= interactRadius * interactRadius && !_targetGen.IsCompleted)
            _isWorking = true;
        else
            _isWorking = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 상태/스태미너/소리
    void StateAndStaminaUpdate(float dt)
    {
        bool hasGen = _targetGen != null && !_targetGen.IsCompleted;
        bool movingToGen = hasGen && !_isWorking;
        bool agentMoving = _agent.velocity.sqrMagnitude > 0.05f;

        // 상태 결정 (Flee/Scan 우선)
        if (CurrentState == MoveState.Flee)
        {
            SetCrouch(false);
            SetState(MoveState.Flee); // 유지
            // 런 사운드/숨가쁨 처리
            if (noise && _stamina <= maxStamina * noise.breathRangeMul)
                CallNoise(CharacterMoveState.Exhaustion);
            else
                CallNoise(CharacterMoveState.Run);

            // 스태미너 소모
            _stamina -= runDrainPerSec * dt;
            _stamina = Mathf.Clamp(_stamina, 0f, maxStamina);
            _isRecovering = false; _healTimer = 0f;
        }
        else if (CurrentState == MoveState.Scan)
        {
            // 조용히 둘러보기: 앉거나(선호 시) 천천히 걷기
            if (preferCrouchWhileRoam)
            {
                SetCrouch(true);
                SetState(MoveState.Scan); // 유지
                CallNoise(CharacterMoveState.Crouch);
            }
            else
            {
                SetCrouch(false);
                // 제자리 회전 중이지만 약간 이동할 수 있으니 Walk 사운드
                if (agentMoving) { SetState(MoveState.Walk); CallNoise(CharacterMoveState.Walk); }
                else             { SetState(MoveState.Scan); CallNoise(CharacterMoveState.Idle); }
            }
        }
        else if (_isWorking)
        {
            // 작업 중에는 Walk 사운드(요청사항)
            SetCrouch(false);
            SetState(MoveState.Walk);
            CallNoise(CharacterMoveState.Walk);
        }
        else if (movingToGen)
        {
            // 발전기로 이동: 스태미너 있으면 Run
            if (_stamina > 0.01f)
            {
                SetCrouch(false);
                SetState(MoveState.Run);

                if (noise && _stamina <= maxStamina * noise.breathRangeMul)
                    CallNoise(CharacterMoveState.Exhaustion);
                else
                    CallNoise(CharacterMoveState.Run);

                _stamina -= runDrainPerSec * dt;
                _stamina = Mathf.Clamp(_stamina, 0f, maxStamina);
                _isRecovering = false; _healTimer = 0f;
            }
            else
            {
                SetCrouch(false);
                SetState(MoveState.Walk);
                CallNoise(CharacterMoveState.Walk);
            }
        }
        else
        {
            // 배회/대기
            if (agentMoving)
            {
                if (preferCrouchWhileRoam)
                {
                    SetCrouch(true);
                    SetState(MoveState.Crouch);
                    CallNoise(CharacterMoveState.Crouch);
                }
                else
                {
                    SetCrouch(false);
                    SetState(MoveState.Walk);
                    CallNoise(CharacterMoveState.Walk);
                }
            }
            else
            {
                SetCrouch(false);
                SetState(MoveState.Idle);
                CallNoise(CharacterMoveState.Idle);
            }
        }

        // 회복(달리는 중이 아닐 때)
        bool sprintingNow = (CurrentState == MoveState.Run || CurrentState == MoveState.Flee);
        if (!sprintingNow)
        {
            if (!_isRecovering)
            {
                _healTimer += dt;
                if (_healTimer >= staminaHealDelay) _isRecovering = true;
            }
            if (_isRecovering)
            {
                float regen = CurrentState switch
                {
                    MoveState.Crouch => regenPerSecCrouch,
                    MoveState.Walk   => regenPerSecWalk,
                    MoveState.Scan   => regenPerSecIdle,
                    _                => regenPerSecIdle
                };
                _stamina = Mathf.Min(maxStamina, _stamina + regen * dt);
            }
        }

        // 탈진 상태(달리다 0되면 잠깐 느리게)
        if ((CurrentState == MoveState.Run || CurrentState == MoveState.Flee) && _stamina <= 0f)
        {
            SetState(MoveState.Exhaustion);
            CallNoise(CharacterMoveState.Exhaustion);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 이동/회전
    void MoveUpdate(float dt)
    {
        // 목표 없으면 배회 목적지 지정 (Scan일 때는 BrainUpdate에서 처리)
        if ((CurrentState != MoveState.Flee && CurrentState != MoveState.Scan) &&
            (_targetGen == null || _targetGen.IsCompleted))
        {
            SetDestinationOnNavMesh(_roamTarget);
        }

        // 코너 스티어링: steeringTarget을 바라보며 회전
        if (_agent.hasPath)
        {
            Vector3 to = _agent.steeringTarget - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                var rot = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, rotationSpeed * dt);
            }
        }

        // 속도 적용
        float speed = CurrentState switch
        {
            MoveState.Flee       => runSpeed,
            MoveState.Run        => runSpeed,
            MoveState.Crouch     => crouchSpeed,
            MoveState.Exhaustion => exhaustionSpeed,
            MoveState.Walk       => walkSpeed,
            MoveState.Scan       => preferCrouchWhileRoam ? crouchSpeed : walkSpeed,
            _                    => 0f
        };
        _agent.speed = speed;
        _agent.isStopped = speed <= 0.01f;
    }
    
    // 작업 시작/중지
    void WorkUpdate()
    {
        if (_targetGen == null) return;

        float sq = SqrXZ(transform.position, _targetGen.transform.position);
        if (sq <= interactRadius * interactRadius && !_targetGen.IsCompleted && CurrentState != MoveState.Flee)
        {
            _targetGen.TryBeginWork(gameObject); // 작업 중이면 내부에서 유지
        }
        else
        {
            _targetGen.EndWork(gameObject);
        }
    }
    
    // 애니메이션 파라미터
    void UpdateAnimator()
    {
        if (!anim) return;

        bool moving = _agent.velocity.sqrMagnitude > 0.05f;
        anim.SetBool("Crouch", _isCrouch);
        anim.SetBool("CrouchWalk", _isCrouch && moving);
        anim.SetBool("Run", !_isCrouch && (CurrentState == MoveState.Run || CurrentState == MoveState.Flee) && moving);
        anim.SetBool("Walk", !_isCrouch && (CurrentState == MoveState.Walk || CurrentState == MoveState.Scan) && moving);
    }
    
    // Helper
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

    void PickNewRoamPoint()
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 c = Random.insideUnitCircle * roamRadius;
            Vector3 candidate = _spawn + new Vector3(c.x, 0, c.y);
            if (NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas))
            {
                _roamTarget = hit.position;
                return;
            }
        }
        _roamTarget = _spawn; // 실패 시 원점
    }

    void SetDestinationOnNavMesh(Vector3 worldTarget)
    {
        if (NavMesh.SamplePosition(worldTarget, out var hit, 2f, NavMesh.AllAreas))
        {
            // 동일 목적지로 과도 호출 방지
            if (!_agent.hasPath || (_agent.destination - hit.position).sqrMagnitude > 0.04f)
                _agent.SetDestination(hit.position);
        }
    }

    void SetState(MoveState s) => CurrentState = s;

    void SetCrouch(bool on)
    {
        if (_isCrouch == on) return;
        _isCrouch = on;
        if (cam)
        {
            float y = on ? crouchHeight : standHeight;
            cam.localPosition = new Vector3(cam.localPosition.x, y, cam.localPosition.z);
        }
    }

    void CallNoise(CharacterMoveState st)
    {
        if (noise != null) noise.SetState(st);
    }

    static float SqrXZ(Vector3 a, Vector3 b)
    {
        a.y = 0; b.y = 0; return (a - b).sqrMagnitude;
    }

    bool HasPath() => _agent.hasPath && !_agent.pathPending;

    bool Arrived(Vector3 target, float tol = -1f)
    {
        float t = (tol > 0f) ? tol : arriveTolerance;
        float sq = SqrXZ(transform.position, target);
        return sq <= t * t;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 몬스터가 "나를 보고 있는지" (FOV + LOS)
    bool CheckSeenByMonster()
    {
        if (!monster) return false;
        Transform eye = monsterEye ? monsterEye : monster;

        Vector3 from = eye.position;
        Vector3 to   = transform.position;
        Vector3 v    = to - from;

        if (v.magnitude > monsterViewDistance) return false;

        Vector3 fwd = monster.forward; fwd.y = 0f;
        Vector3 flat = v; flat.y = 0f;
        if (Vector3.Angle(fwd, flat) > monsterViewHalfAngle) return false;

        // 가림(LOS) 체크: 벽/기둥에 막히면 못 본 것으로 간주
        if (Physics.Raycast(from, v.normalized, out var hit, monsterViewDistance, obstacleMask))
            return false;

        return true; // 실제로 보고 있음
    }

    // ─────────────────────────────────────────────────────────────────────
    // LOS가 끊기는 "커버 포인트" 찾기 (링 샘플 + NavMesh + 가림 레이캐스트)
    Vector3 FindCoverPoint()
    {
        Vector3 best = transform.position;
        float bestScore = float.NegativeInfinity;
        Transform eye = monsterEye ? monsterEye : monster;

        float baseRadius = Mathf.Max(4f, fleeDistance * 0.6f);
        int   rings = 2;
        int   samplesPerRing = 12;

        for (int r = 0; r < rings; r++)
        {
            float rad = baseRadius + r * 4f;
            for (int i = 0; i < samplesPerRing; i++)
            {
                float ang = (i / (float)samplesPerRing) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                Vector3 candidate = transform.position + dir * rad;

                if (!NavMesh.SamplePosition(candidate, out var hit, 2.5f, NavMesh.AllAreas))
                    continue;

                bool covered = false;
                if (monster)
                {
                    Vector3 from = eye ? eye.position : monster.position;
                    Vector3 to   = hit.position + Vector3.up * 0.2f;
                    Vector3 ray  = (to - from);
                    if (Physics.Raycast(from, ray.normalized, out var h, ray.magnitude, obstacleMask))
                        covered = true;
                }

                float distToMonster = monster ? Vector3.Distance(hit.position, monster.position) : 0f;
                float distFromMe    = Vector3.Distance(hit.position, transform.position);

                // 점수: 가려지면 +, 몬스터와 멀수록 +, 나와 너무 멀면 -
                float score = (covered ? 10f : 0f) + distToMonster * 0.6f - distFromMe * 0.15f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = hit.position;
                }
            }
        }
        return best;
    }
}
