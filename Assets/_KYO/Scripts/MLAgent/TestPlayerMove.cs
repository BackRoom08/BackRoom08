using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class TestPlayerMove : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    // 상태 정의
    public enum MoveState { Idle, Walk, Run, Crouch, Exhaustion, Scan, Flee }

    // ─────────────────────────────────────────────────────────────────────
    [Header("Refs (레퍼런스)")]
    public Animator anim;                  // 플레이어 애니메이터(없으면 애니 파트 스킵)
    public StateNoiseEmitter noise;        // 발자국/숨소리 등 노이즈 발생기
    public Transform cam;                  // 카메라(앉기 높이 조절용)

    // ─────────────────────────────────────────────────────────────────────
    [Header("Move Speeds (이동 속도)")]
    public float walkSpeed = 2.4f;         // 걷기 속도
    public float runSpeed = 5.2f;          // 뛰기(도망) 속도
    public float crouchSpeed = 1.6f;       // 앉은 이동 속도
    public float exhaustionSpeed = 1.8f;   // 탈진 상태 속도
    public float rotationSpeed = 540f;     // 초당 회전 각속도(도/초)

    // ─────────────────────────────────────────────────────────────────────
    [Header("Stamina (지구력)")]
    public float maxStamina = 100f;        // 최대 지구력
    public float runDrainPerSec = 2f;     // 달리기 중 초당 소모량
    public float regenPerSecWalk = 12f;    // 걷기 중 초당 회복량
    public float regenPerSecIdle = 18f;    // 정지/스캔 중 초당 회복량
    public float regenPerSecCrouch = 20f;  // 앉기 중 초당 회복량
    public float staminaHealDelay = 2.0f;  // 달리기 멈춘 후 회복 시작까지 지연
    public float staminaToReEnableRun = 25f; // (옵션) 탈진 복귀 임계값

    // ─────────────────────────────────────────────────────────────────────
    [Header("Crouch Camera (앉기 카메라 높이)")]
    public float standHeight = 1.7f;       // 서있을 때 카메라 높이
    public float crouchHeight = 1.0f;      // 앉을 때 카메라 높이

    // ─────────────────────────────────────────────────────────────────────
    [Header("Generator Work (발전기 작업)")]
    public float interactRadius = 1.8f;    // 작업 시작/유지 반경
    public float searchInterval = 0.5f;    // 발전기 재탐색 주기(초)
    public float roamRadius = 8f;          // 발전기 없을 때 배회 반경
    public float arriveTolerance = 0.6f;   // 목적지 도착 판정 여유
    public bool preferCrouchWhileRoam = false; // 배회 중 기본적으로 앉을지 여부

    // ─────────────────────────────────────────────────────────────────────
    [Header("Monster Perception (몬스터의 시야 판정)")]
    public Transform monster;              // 몬스터 본체
    public Transform monsterEye;           // 몬스터 시야 기준(없으면 monster)
    public LayerMask obstacleMask;         // 몬스터 LOS(시선) 가리는 레이어(벽/기둥 등)
    public float monsterViewDistance = 22f;// 몬스터가 볼 수 있는 최대 거리
    [Range(0,180f)]
    public float fovHalfAngle = 40f;       // 몬스터 전방 시야 반각(요구: 40°)
    public float proximityAlertRadius = 10f; // 시야 무관 근접 경보 반경(요구: 10m)

    // ─────────────────────────────────────────────────────────────────────
    [Header("Player View Check (플레이어 시야 판정)")]
    public Transform playerEye;            // 플레이어 시야 기준(카메라/머리)
    public float playerViewDistance = 30f; // 플레이어가 몬스터를 감지할 최대 거리
    [Range(0,180f)]
    public float playerFOVHalfAngle = 70f; // 플레이어 전방 시야 반각
    public LayerMask playerObstacleMask;   // 플레이어 LOS 가리는 레이어

    // ─────────────────────────────────────────────────────────────────────
    [Header("Scan & Flee (스캔/도망 동작)")]
    public float scanDuration = 1.4f;      // 스캔 상태에서 회전 유지 시간
    public float scanAngularSpeed = 180f;  // 스캔 상태 회전 속도
    public float fleeDistance = 10f;       // 커버 포인트 기본 반경
    public float fleeResampleWhenClose = 0.8f; // 커버점 근접 시 재샘플 임계

    // ─────────────────────────────────────────────────────────────────────
    [Header("Safe Return (안전 복귀 조건)")]
    public float safeSightlessTime = 2.0f; // 시야 차단 유지 시간(초)
    public float minSafeDistance = 12f;    // 안전 거리(이상일 때 복귀 가능)

    // ─────────────────────────────────────────────────────────────────────
    [Header("Threat Debounce (오탐 방지 디바운스)")]
    public float threatAcquireTime = 0.35f;// 위협 감지 지속 시간(이상) → Flee 진입
    public float threatReleaseTime = 0.50f;// 위협 사라짐 지속 시간(이상) → Flee 해제
    float _threatOnTimer = 0f;             // 위협 on 누적 타이머
    float _threatOffTimer = 0f;            // 위협 off 누적 타이머
    bool _threatLatched = false;           // 디바운스 결과(최종 위협 스위치)

    // ─────────────────────────────────────────────────────────────────────
    [Header("Fast Return From Flee (빠른 복귀)")]
    public float quickLostSightGrace = 0.60f; // 안 보인 지 grace 이상이면 복귀 허용
    public float farSafeDistanceMul = 1.25f;  // 안전거리 배수(멀리 떨어지면 즉시 복귀)

    // ─────────────────────────────────────────────────────────────────────
    [Header("Crouch After Corners (코너 n회 후 앉기 전환)")]
    public int cornersToCrouch = 2;        // 코너를 최소 몇 번 꺾으면 앉기 전환할지(요구: 2)
    public float cornerAngleDeg = 50f;     // 코너 판정 회전각 최소값(도)
    public float cornerMinDist = 1.5f;     // 코너 사이 최소 이동 거리
    int _losCornerCount = 0;               // 현재 누적된 코너 수
    Vector3 _lastCornerPos;                // 마지막 코너로 인정한 위치
    Vector3 _lastMoveDir;                  // 마지막 프레임 이동 방향

    // ─────────────────────────────────────────────────────────────────────
    [Header("Stuck Breaker (스턱 해제)")]
    public float stuckSpeedEps = 0.05f;    // 거의 정지로 간주할 속도
    public float stuckCheckTime = 0.8f;    // 이 시간 이상 정지/막힘이면 조치
    float _stuckTimer = 0f;                // 스턱 누적 타이머

    // ─────────────────────────────────────────────────────────────────────
    // 런타임 공개 속성
    public MoveState CurrentState { get; private set; } = MoveState.Idle;
    public float Stamina => _stamina;
    public Generator CurrentTarget => _targetGen;

    // 내부 상태
    NavMeshAgent _agent;
    Generator _targetGen;
    Vector3 _spawn;                        // 스폰 지점(배회 중심)
    Vector3 _roamTarget;                   // 현재 배회 목적지

    float _searchTimer;                    // 발전기 재탐색 타이머
    float _stamina;                        // 현재 지구력
    float _healTimer;                      // 회복 지연 타이머
    bool  _isRecovering;                   // 회복 시작 여부
    bool  _isWorking;                      // 발전기 작업 중 플래그
    bool  _isCrouch;                       // 앉기 상태

    // 스캔/도망 보조
    float _scanTimer; int _scanDir = 1;    // 스캔 회전 타이머/방향
    Vector3 _fleeTarget;                   // 현재 커버 목적지
    float _lostSightTimer = 0f;            // Flee 중 '안 보임' 누적 시간

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (!noise) noise = GetComponent<StateNoiseEmitter>();

        // NavMesh 에이전트 권장 설정
        _agent.updatePosition = true;
        _agent.updateRotation = false; // 직접 회전(코너 긁힘 감소)
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

        // 시작은 스캔
        SetState(MoveState.Scan);
        _scanTimer = scanDuration;
        _scanDir = (Random.value < 0.5f) ? -1 : 1;

        // 코너 추적 초기화
        _lastCornerPos = transform.position;
        _lastMoveDir   = Vector3.zero;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // ── 원시 위협 신호 계산 ──
        bool closeThreat = IsMonsterVeryClose();   // ≤ 10m 근접 경보
        bool seenByFOV   = IsMonsterSeeingMe();    // 몬스터 시야(40°+LOS)
        bool rawThreat   = closeThreat || seenByFOV;

        // ── 디바운스 처리(오탐 방지) ──
        if (rawThreat) { _threatOnTimer += dt; _threatOffTimer = 0f;
            if (!_threatLatched && _threatOnTimer >= threatAcquireTime) _threatLatched = true;
        } else { _threatOnTimer = 0f; _threatOffTimer += dt;
            if (_threatLatched && _threatOffTimer >= threatReleaseTime) _threatLatched = false;
        }
        bool threat = _threatLatched;

        // ── 상태 전이(Flee 진입/해제) ──
        if (threat && CurrentState != MoveState.Flee)
        {
            _fleeTarget = FindCoverPoint();
            SetDestinationOnNavMesh(_fleeTarget);
            _lostSightTimer = 0f;
            _losCornerCount = 0;                // 새 도망 시작 → 코너 카운터 리셋
            _lastCornerPos  = transform.position;
            _lastMoveDir    = Vector3.zero;
            SetState(MoveState.Flee);
        }
        else if (!threat && CurrentState == MoveState.Flee)
        {
            _lostSightTimer += dt;

            // 멀리 떨어졌거나, 일정 시간 이상 안 보였으면 빠르게 복귀
            if (DistanceToMonster() >= minSafeDistance * farSafeDistanceMul ||
                _lostSightTimer >= quickLostSightGrace)
            {
                _losCornerCount = 0;
                _lastCornerPos  = transform.position;
                _lastMoveDir    = Vector3.zero;
                SetState(MoveState.Scan);
            }
        }

        // 메인 업데이트
        BrainUpdate(dt, seenByFOV);
        StateAndStaminaUpdate(dt, seenByFOV);
        MoveUpdate(dt);
        WorkUpdate();
        UpdateAnimator();
    }

    // ─────────────────────────────────────────────────────────────────────
    // 목표/작업 판단
    void BrainUpdate(float dt, bool seenByFOV)
    {
        if (CurrentState == MoveState.Flee)
        {
            // 보이면(몬스터가 봄) 커버 재샘플
            if (seenByFOV && Arrived(_fleeTarget, fleeResampleWhenClose))
            {
                _fleeTarget = FindCoverPoint();
                SetDestinationOnNavMesh(_fleeTarget);
            }

            // ── 코너 카운팅: '안 보이는' 상태에서 진행 방향이 크게 꺾이면 코너+1 ──
            bool playerSees = PlayerCanSeeMonster(); // 플레이어가 몬스터를 보는지도 반영
            if (!seenByFOV && !playerSees)
            {
                Vector3 vel = _agent.desiredVelocity; vel.y = 0f;
                if (vel.sqrMagnitude > 0.0001f)
                {
                    Vector3 dir = vel.normalized;
                    if (_lastMoveDir == Vector3.zero) _lastMoveDir = dir;

                    float ang = Vector3.Angle(_lastMoveDir, dir);
                    float moved = Vector3.Distance(transform.position, _lastCornerPos);

                    if (ang >= cornerAngleDeg && moved >= cornerMinDist)
                    {
                        _losCornerCount++;
                        _lastCornerPos = transform.position;
                        _lastMoveDir   = dir;
                        // Debug.Log($"Corner++ => #{_losCornerCount}");
                    }
                    else
                    {
                        _lastMoveDir = dir;
                    }
                }
            }
            else
            {
                // 누군가에게라도 보이면 카운트 리셋
                _losCornerCount = 0;
                _lastCornerPos  = transform.position;
                _lastMoveDir    = Vector3.zero;
            }

            // ── 스턱 브레이커 ──
            bool verySlow = _agent.velocity.sqrMagnitude < stuckSpeedEps * stuckSpeedEps;
            bool noPath   = !_agent.hasPath || _agent.pathPending;
            bool nearEnd  = _agent.hasPath && _agent.remainingDistance <= arriveTolerance + 0.2f;

            if (verySlow || noPath || nearEnd) _stuckTimer += dt; else _stuckTimer = 0f;

            if (_stuckTimer >= stuckCheckTime)
            {
                _stuckTimer = 0f;
                var newCover = FindCoverPoint();
                if ((newCover - transform.position).sqrMagnitude > 0.5f * 0.5f)
                    SetDestinationOnNavMesh(newCover);
                else
                {
                    // 정말 갈 곳 없으면 복귀
                    _losCornerCount = 0;
                    _lastCornerPos  = transform.position;
                    _lastMoveDir    = Vector3.zero;
                    SetState(MoveState.Scan);
                }
            }
            return;
        }

        // 스캔: 회전 + 슬쩍 배회 + 발전기 주기 탐색
        if (CurrentState == MoveState.Scan)
        {
            _scanTimer -= dt;
            transform.Rotate(0f, _scanDir * scanAngularSpeed * dt, 0f);

            _searchTimer -= dt;
            if (_searchTimer <= 0f)
            {
                _searchTimer = searchInterval;
                _targetGen = FindNearestGenerator();
                if (_targetGen != null)
                {
                    SetDestinationOnNavMesh(_targetGen.transform.position);
                    SetState(_stamina > 0.01f ? MoveState.Run : MoveState.Walk);
                    return;
                }
            }

            if (!HasPath() || Arrived(_roamTarget)) PickNewRoamPoint();
            SetDestinationOnNavMesh(_roamTarget);

            if (_scanTimer <= 0f) { _scanTimer = scanDuration; _scanDir *= -1; }
            return;
        }

        // 발전기 중심 로직
        _searchTimer -= dt;

        if (_targetGen == null || _targetGen.IsCompleted)
        {
            if (_searchTimer <= 0f)
            {
                _searchTimer = searchInterval;
                _targetGen = FindNearestGenerator();
            }

            if (_targetGen == null)
            {
                if (!HasPath() || Arrived(_roamTarget)) PickNewRoamPoint();
                _isWorking = false;
                SetState(MoveState.Scan);
                return;
            }
        }

        SetDestinationOnNavMesh(_targetGen.transform.position);

        float sq = SqrXZ(transform.position, _targetGen.transform.position);
        _isWorking = (sq <= interactRadius * interactRadius && !_targetGen.IsCompleted);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 상태/스태미너/소리
    void StateAndStaminaUpdate(float dt, bool seenByFOV)
    {
        bool hasGen = _targetGen != null && !_targetGen.IsCompleted;
        bool movingToGen = hasGen && !_isWorking;
        bool agentMoving = _agent.velocity.sqrMagnitude > 0.05f;

        if (CurrentState == MoveState.Flee)
        {
            bool playerSees = PlayerCanSeeMonster();

            if (seenByFOV || playerSees)
            {
                // 누군가에게라도 '보이면' → 전력질주(소리=Run/Exhaustion)
                SetCrouch(false);
                CallNoise((noise && _stamina <= maxStamina * noise.breathRangeMul)
                          ? CharacterMoveState.Exhaustion
                          : CharacterMoveState.Run);
                _stamina = Mathf.Max(0f, _stamina - runDrainPerSec * dt);
                _isRecovering = false; _healTimer = 0f;
            }
            else
            {
                // 안 보임: 코너 n회 전까지는 계속 Run(먼저 거리 벌림)
                if (_losCornerCount < cornersToCrouch)
                {
                    SetCrouch(false);
                    CallNoise((noise && _stamina <= maxStamina * noise.breathRangeMul)
                              ? CharacterMoveState.Exhaustion
                              : CharacterMoveState.Run);
                    _stamina = Mathf.Max(0f, _stamina - runDrainPerSec * dt);
                    _isRecovering = false; _healTimer = 0f;
                }
                else
                {
                    // 코너 n회 달성 후 → 앉은 무음 도망
                    SetCrouch(true);
                    CallNoise(CharacterMoveState.Idle); // idle/crouch = 무음
                }
            }
        }
        else if (CurrentState == MoveState.Scan)
        {
            if (preferCrouchWhileRoam) { SetCrouch(true); CallNoise(CharacterMoveState.Crouch); }
            else                       { SetCrouch(false); CallNoise(agentMoving ? CharacterMoveState.Walk : CharacterMoveState.Idle); }
        }
        else if (_isWorking)
        {
            // 작업 중엔 Walk 소리(요구사항)
            SetCrouch(false);
            SetState(MoveState.Walk);
            CallNoise(CharacterMoveState.Walk);
        }
        else if (movingToGen)
        {
            if (_stamina > 0.01f)
            {
                SetCrouch(false);
                SetState(MoveState.Run);
                CallNoise((noise && _stamina <= maxStamina * noise.breathRangeMul)
                          ? CharacterMoveState.Exhaustion
                          : CharacterMoveState.Run);
                _stamina = Mathf.Max(0f, _stamina - runDrainPerSec * dt);
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
            // 일반 배회
            if (agentMoving)
            {
                if (preferCrouchWhileRoam) { SetCrouch(true);  CallNoise(CharacterMoveState.Crouch); }
                else                        { SetCrouch(false); CallNoise(CharacterMoveState.Walk); }
            }
            else
            {
                SetCrouch(false);
                SetState(MoveState.Idle);
                CallNoise(CharacterMoveState.Idle);
            }
        }

        // 회복(달리지 않을 때)
        bool sprintingNow = (CurrentState == MoveState.Run || (CurrentState == MoveState.Flee && (seenByFOV || PlayerCanSeeMonster() || _losCornerCount < cornersToCrouch)));
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

        // 탈진 진입
        if ((CurrentState == MoveState.Run || (CurrentState == MoveState.Flee && (seenByFOV || PlayerCanSeeMonster() || _losCornerCount < cornersToCrouch))) && _stamina <= 0f)
        {
            SetState(MoveState.Exhaustion);
            CallNoise(CharacterMoveState.Exhaustion);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 이동/회전
    void MoveUpdate(float dt)
    {
        if ((CurrentState != MoveState.Flee && CurrentState != MoveState.Scan) &&
            (_targetGen == null || _targetGen.IsCompleted))
        {
            SetDestinationOnNavMesh(_roamTarget);
        }

        // 부드러운 회전(steeringTarget 바라보기)
        if (_agent.hasPath)
        {
            Vector3 to = _agent.steeringTarget - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                var rot = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, rotationSpeed * dt);
            }
        }

        // 속도 선택(보이면 Run / 안 보이면 코너 n회 전엔 Run, 후엔 Crouch)
        float speed = CurrentState switch
        {
            MoveState.Flee       => ((IsMonsterSeeingMe() || PlayerCanSeeMonster() || _losCornerCount < cornersToCrouch) ? runSpeed : crouchSpeed),
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

    // ─────────────────────────────────────────────────────────────────────
    // 작업 시작/중지
    void WorkUpdate()
    {
        if (_targetGen == null) return;

        float sq = SqrXZ(transform.position, _targetGen.transform.position);
        if (sq <= interactRadius * interactRadius && !_targetGen.IsCompleted && CurrentState != MoveState.Flee)
            _targetGen.TryBeginWork(gameObject);
        else
            _targetGen.EndWork(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 애니메이션 파라미터
    void UpdateAnimator()
    {
        if (!anim) return;

        bool moving = _agent.velocity.sqrMagnitude > 0.05f;
        bool seen   = IsMonsterSeeingMe();

        anim.SetBool("Crouch", _isCrouch);
        anim.SetBool("CrouchWalk", _isCrouch && moving);
        anim.SetBool("Run", !_isCrouch && (CurrentState == MoveState.Run || CurrentState == MoveState.Flee) && seen && moving);
        anim.SetBool("Walk", !_isCrouch && ((CurrentState == MoveState.Walk || CurrentState == MoveState.Scan) || (CurrentState == MoveState.Flee && !seen)) && moving);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helper 함수들
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

    float DistanceToMonster()
    {
        if (!monster) return float.MaxValue;
        return Vector3.Distance(transform.position, monster.position);
    }

    // ── 10m 근접 경보(시야 무관) ──
    bool IsMonsterVeryClose()
    {
        if (!monster) return false;
        return DistanceToMonster() <= proximityAlertRadius;
    }

    // ── 몬스터 시야 판정(전방 40° + LOS) ──
    bool IsMonsterSeeingMe()
    {
        if (!monster) return false;
        Transform eye = monsterEye ? monsterEye : monster;

        Vector3 from = eye.position;
        Vector3 to   = transform.position;
        Vector3 v    = to - from;

        if (v.magnitude > monsterViewDistance) return false;

        Vector3 fwd = monster.forward; fwd.y = 0f;
        Vector3 flat = v; flat.y = 0f;
        if (Vector3.Angle(fwd, flat) > fovHalfAngle) return false;

        // 가림체에 막히면 '안 보임'
        if (Physics.Raycast(from, v.normalized, out var hit, monsterViewDistance, obstacleMask))
            return false;

        return true;
    }

    // ── 플레이어 시야 판정(플레이어가 몬스터를 볼 수 있는가) ──
    bool PlayerCanSeeMonster()
    {
        if (!monster || !playerEye) return false;

        Vector3 from = playerEye.position;
        Vector3 to   = monster.position;
        Vector3 v    = to - from;

        if (v.magnitude > playerViewDistance) return false;

        Vector3 fwd = playerEye.forward; fwd.y = 0f;
        Vector3 flat = v; flat.y = 0f;
        if (Vector3.Angle(fwd, flat) > playerFOVHalfAngle) return false;

        if (Physics.Raycast(from, v.normalized, out var hit, playerViewDistance, playerObstacleMask))
            return false;

        return true;
    }

    // ── LOS가 끊기는 커버 포인트 탐색(링 샘플 + 가림 점수) ──
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

                // 점수: 가려지면 +, 몬스터와 멀수록 +, 너무 멀면 -
                float score = (covered ? 10f : 0f) + distToMonster * 0.6f - distFromMe * 0.15f;

                if (score > bestScore) { bestScore = score; best = hit.position; }
            }
        }
        return best;
    }
}
