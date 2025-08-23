using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum MonsterPhase { Normal, Empowered }

// 기존 구현을 그대로 씁니다.
public interface IMonsterStatus
{
    MonsterMode Mode { get; set; }              // Idle/Patrol/Investigate/Chase 등 기존 값 유지
    Transform Player { get; }
    Vector3 LastKnownPlayerPos { get; set; }
}

// 외부 노출용 상위 모드(기존 것 사용)
public enum MonsterMode { Idle, Patrol, Investigate, Chase }

/// <summary>
/// 데바데식 살인마 하이브리드 AI:
/// - 순찰(발전기/탈출문) → 소리/시야 이벤트로 Investigate/Chase
/// - 플레이어가 '나를 못 볼 때' 가만히 2초 후부터 스톡킹 누적, 30초 누적 시 즉시 강화(40초 지속)
/// - 사운드 끊기면 소리 지점 도착 → 다음 지점 예측(ML or 휴리스틱) → 잠복(Ambush)
/// - IMonsterStatus/사운드시스템은 그대로 사용(구독만)
/// </summary>
public class MLMonsterAgent : MonoBehaviour
{
    [Header("External (existing systems)")]
    [SerializeField] private MonoBehaviour statusProvider; // IMonsterStatus 구현 컴포넌트 Drag
    private IMonsterStatus status;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform eyes;              // 몬스터 시야 기준 (머리 등)
    [SerializeField] private LayerMask sightBlockers;     // 벽 등 차단 레이어

    [Header("Player perception")]
    [SerializeField] private float sightRange = 40f;      // 내가 플레이어를 보는 거리
    [SerializeField] private float mySightHalfAngle = 70f;// 내 정면 반각
    [SerializeField] private Transform playerEyes;        // 플레이어의 시야 기준(없으면 Player transform 사용)
    [SerializeField] private float playerSightHalfAngle = 65f; // 플레이어가 보는 반각(“들키지 않음” 판정용)

    [Header("Patrol Targets (children only)")]
    [SerializeField] private Transform generatorsRoot;    // 자식 = 발전기들
    [SerializeField] private Transform exitsRoot;         // 자식 = 탈출문들
    [SerializeField] private float waypointReachDist = 1.6f;

    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 2f;        // 둘 다 동일
    [SerializeField] private float runSpeedNormal = 4f;   // 일반
    [SerializeField] private float runSpeedEmpowered = 6f;// 강화

    [Header("Attack Power (for your attack system to read)")]
    [SerializeField] private int attackNormal = 1;
    [SerializeField] private int attackEmpowered = 2;
    public int CurrentAttackPower => (phase == MonsterPhase.Empowered) ? attackEmpowered : attackNormal;

    [Header("Stalk / Empower rules")]
    [SerializeField] private float stalkPrimeStillSeconds = 2f;  // 가만히 있어야 하는 최소 시간
    [SerializeField] private float stalkNeedSeconds = 30f;       // 누적 필요 시간
    [SerializeField] private float empoweredDuration = 40f;      // 강화 유지 시간

    [Header("Ambush after sound loss")]
    [SerializeField] private float ambushWaitSeconds = 4f;       // 예측 지점에서 대기
    [SerializeField] private float investigateArriveDist = 2.5f; // 소리 지점 도달 판정

    [Header("(Optional) ML 예측/보조")]
    [SerializeField] private bool useMLSteering = false;         // 조향 보조
    [SerializeField] private float mlForwardScale = 3f;

    // 내부 서브상태(외부 Mode는 기존 값 유지)
    private enum SubState { Idle, Patrol, Investigate, Chase, Stalk, Ambush }
    private SubState sub = SubState.Patrol;

    private MonsterPhase phase = MonsterPhase.Normal;
    private float empoweredTimer;
    private float stalkAccum;            // 누적 스톡킹 시간
    private float stillTimer;            // 정지 유지 시간
    private bool primeReady;             // 2초 정지 달성 여부

    private readonly Queue<Vector3> patrolQueue = new();
    private Vector3 currentTarget;
    private bool hasTarget;

    // 사운드 관련
    private Vector3 lastHeardPos;
    private bool hasHeard;

    void Awake()
    {
        status = statusProvider as IMonsterStatus;
        if (status != null) Debug.LogError("[KillerAIBrain] statusProvider에 IMonsterStatus 구현 컴포넌트를 연결하세요.");

        if (!agent) agent = GetComponent<NavMeshAgent>();
        if (!eyes) eyes = transform;

        BuildPatrolQueue();
        ApplySpeedForState();
    }

    void OnEnable()  { TryHookYourSoundSystem(true); }
    void OnDisable() { TryHookYourSoundSystem(false); }

    void Update()
    {
        if (status == null) return;

        TickEmpowerCycle();         // 강화/일반 전환 타이머
        PerceptionAndSwitch();      // 시야/사운드에 의한 상위 모드 전환

        switch (sub)
        {
            case SubState.Patrol:      PatrolTick();      break;
            case SubState.Investigate: InvestigateTick(); break;
            case SubState.Chase:       ChaseTick();       break;
            case SubState.Stalk:       StalkTick();       break;
            case SubState.Ambush:      AmbushTick();      break;
            case SubState.Idle:        agent.isStopped = false; break;
        }

        if (useMLSteering) ApplyMLSteering();
    }

    #region Perception / Switching
    void PerceptionAndSwitch()
    {
        bool canSee = CanISeePlayer(out float dist);
        bool playerSeesMe = DoesPlayerSeeMe();

        if (canSee)
        {
            status.LastKnownPlayerPos = status.Player.position;

            // 플레이어가 나를 보면 = 들킴 → 추격
            if (playerSeesMe)
            {
                SetState(SubState.Chase, MonsterMode.Chase);
                SetDestination(status.Player.position);
                ResetStalkPriming();
                return;
            }

            // 플레이어에게 '들키지 않음'이면 스톡킹
            SetState(SubState.Stalk, MonsterMode.Chase); // 외부 UI는 Chase로 보여도 됨
            // 스톡킹 중엔 이동 멈추고 바라보기
            agent.isStopped = true;
            Face(status.Player.position);
            return;
        }

        // 보지 못하지만 최근 소리를 들었다면 Investigate 유지
        if (hasHeard && sub != SubState.Chase)
        {
            SetState(SubState.Investigate, MonsterMode.Investigate);
            return;
        }

        // 그 외엔 순찰
        if (sub != SubState.Patrol)
        {
            SetState(SubState.Patrol, MonsterMode.Patrol);
        }
    }

    bool CanISeePlayer(out float dist)
    {
        dist = 9999f;
        if (!status.Player) return false;

        var to = status.Player.position - eyes.position;
        dist = to.magnitude;
        if (dist > sightRange) return false;

        var dir = to / Mathf.Max(dist, 0.0001f);
        if (Vector3.Angle(eyes.forward, dir) > mySightHalfAngle) return false;

        // 차단 체크
        if (Physics.Raycast(eyes.position, dir, out var hit, sightRange, ~0))
        {
            if (((1 << hit.collider.gameObject.layer) & sightBlockers) != 0)
                return false;
        }
        return true;
    }

    bool DoesPlayerSeeMe()
    {
        var pe = playerEyes ? playerEyes : status.Player;
        if (!pe) return false;

        var toMe = transform.position - pe.position;
        if (toMe.sqrMagnitude > sightRange * sightRange) return false;

        var dir = toMe.normalized;
        if (Vector3.Angle(pe.forward, dir) > playerSightHalfAngle) return false;

        // 플레이어 시점에서 차단 체크(간단화)
        if (Physics.Raycast(pe.position, dir, out var hit, sightRange, ~0))
        {
            if (((1 << hit.collider.gameObject.layer) & sightBlockers) != 0)
                return false;
        }
        return true;
    }
    #endregion

    #region Patrol / Investigate / Chase / Stalk / Ambush
    void BuildPatrolQueue()
    {
        var list = new List<Vector3>(32);
        if (generatorsRoot) foreach (Transform t in generatorsRoot) list.Add(t.position);
        if (exitsRoot)      foreach (Transform t in exitsRoot)      list.Add(t.position);

        if (list.Count == 0)
        {
            Debug.LogWarning("[KillerAIBrain] 순찰 대상 없음 (generatorsRoot/exitsRoot 자식 확인)");
            return;
        }

        // 셔플 후 원형 큐
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
        foreach (var p in list) patrolQueue.Enqueue(p);

        SetState(SubState.Patrol, MonsterMode.Patrol);
        AdvancePatrol();
    }

    void AdvancePatrol()
    {
        if (patrolQueue.Count == 0) return;
        var p = patrolQueue.Dequeue();
        patrolQueue.Enqueue(p);
        SetDestination(p);
        agent.speed = walkSpeed; // 걷기
    }

    void PatrolTick()
    {
        if (hasTarget && Reached(currentTarget, waypointReachDist))
            AdvancePatrol();
        MaintainPath();
    }

    void InvestigateTick()
    {
        // 소리 지점으로 이동(걷기)
        agent.speed = walkSpeed;

        if (Reached(currentTarget, investigateArriveDist))
        {
            // 다음 이동 지점 예측 → 잠복
            if (PredictNextPosition(currentTarget, out var ambushPos))
            {
                SetDestination(ambushPos);
                SetState(SubState.Ambush, MonsterMode.Investigate);
                ambushUntil = Time.time + ambushWaitSeconds;
            }
            else
            {
                // 예측 실패 → 순찰 복귀
                hasHeard = false;
                SetState(SubState.Patrol, MonsterMode.Patrol);
                AdvancePatrol();
            }
        }
        MaintainPath();
    }

    void ChaseTick()
    {
        if (!status.Player) return;

        agent.isStopped = false;
        agent.speed = (phase == MonsterPhase.Empowered) ? runSpeedEmpowered : runSpeedNormal;
        SetDestination(status.Player.position);

        // 시야 잃으면 마지막 위치로 수색
        if (!CanISeePlayer(out _))
        {
            SetState(SubState.Investigate, MonsterMode.Investigate);
            SetDestination(status.LastKnownPlayerPos);
            hasHeard = true; // 수색 유지
        }
    }

    void StalkTick()
    {
        // 이동 정지 유지, 플레이어를 응시
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        Face(status.Player.position);

        // 정지 시간 축적(2초)
        if (agent.velocity.sqrMagnitude < 0.01f)
            stillTimer += Time.deltaTime;
        else
            stillTimer = 0f;

        primeReady = stillTimer >= stalkPrimeStillSeconds;

        // 스톡킹 누적(플레이어에게 들키지 않고, 내가 플레이어를 볼 수 있을 때)
        if (primeReady && CanISeePlayer(out _) && !DoesPlayerSeeMe())
        {
            stalkAccum += Time.deltaTime;
            if (stalkAccum >= stalkNeedSeconds && phase == MonsterPhase.Normal)
            {
                EnterEmpowered();
            }
        }
        else
        {
            // 조건이 깨지면 누적은 유지하지만(요구 명세에 '초기화' 언급 없음), 다시 2초 프라임은 필요
            stillTimer = 0f;
            primeReady = false;
        }
    }

    float ambushUntil;
    void AmbushTick()
    {
        agent.speed = walkSpeed;
        MaintainPath();

        // 대기 시간 끝났고, 새 소리/시야 없으면 순찰 복귀
        if (Time.time >= ambushUntil && !hasHeard && !CanISeePlayer(out _))
        {
            SetState(SubState.Patrol, MonsterMode.Patrol);
            AdvancePatrol();
        }
    }
    #endregion

    #region Empower / Stalk helpers
    void EnterEmpowered()
    {
        phase = MonsterPhase.Empowered;
        empoweredTimer = empoweredDuration;
        ApplySpeedForState();
        // 강화는 즉시 켜짐
    }

    void TickEmpowerCycle()
    {
        if (phase == MonsterPhase.Empowered)
        {
            empoweredTimer -= Time.deltaTime;
            if (empoweredTimer <= 0f)
            {
                phase = MonsterPhase.Normal;
                // 스톡 누적은 초기화(원한다면 유지로 바꿀 수 있음)
                stalkAccum = 0f;
                ResetStalkPriming();
                ApplySpeedForState();
            }
        }
    }

    void ResetStalkPriming()
    {
        stillTimer = 0f;
        primeReady = false;
    }
    #endregion

    #region Navigation utils
    void SetDestination(Vector3 pos)
    {
        hasTarget = true;
        currentTarget = pos;
        status.LastKnownPlayerPos = pos;

        if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(pos);

        agent.isStopped = false;
    }

    bool Reached(Vector3 pos, float dist)
    {
        if (agent.pathPending) return false;
        return (agent.destination - transform.position).sqrMagnitude <= dist * dist;
    }

    void MaintainPath()
    {
        if (!agent.hasPath && hasTarget && !agent.pathPending)
            agent.SetDestination(currentTarget);
    }

    void Face(Vector3 worldPos)
    {
        var to = worldPos - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        var rot = Quaternion.LookRotation(to.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, 0.25f);
    }

    void ApplySpeedForState()
    {
        if (sub == SubState.Chase)
            agent.speed = (phase == MonsterPhase.Empowered) ? runSpeedEmpowered : runSpeedNormal;
        else
            agent.speed = walkSpeed;
    }

    void SetState(SubState s, MonsterMode external)
    {
        if (sub == s) return;
        sub = s;
        status.Mode = external;
        ApplySpeedForState();
    }
    #endregion

    #region Prediction (sound loss → next point)
    /// <summary>
    /// 학습/휴리스틱 대체 가능한 예측 훅.
    /// ML-Agents를 쓰면 여기서 에이전트 출력(오프셋 x,z 등)을 받아 worldPos로 변환.
    /// 지금은 간단 휴리스틱: 마지막 소리 지점에서 플레이어 마지막 진행 방향으로 약간 앞, 또는 근처 문/탈출문/발전기 쪽 샘플.
    /// </summary>
    bool PredictNextPosition(Vector3 origin, out Vector3 candidate)
    {
        // 1) 플레이어가 있으면 그쪽 진행 방향 기준으로 오프셋
        if (status.Player)
        {
            var forward = status.Player.forward;
            var test = origin + forward * 6f; // 6m 앞
            if (NavMesh.SamplePosition(test, out var hit, 2f, NavMesh.AllAreas))
            {
                candidate = hit.position;
                return true;
            }
        }
        // 2) 주변 발전기/탈출문 중 가장 가까운 곳
        Vector3 best = Vector3.zero; float bd = float.MaxValue; bool found = false;
        foreach (var p in EnumeratePatrolPoints())
        {
            float d = (p - origin).sqrMagnitude;
            if (d < bd)
            {
                if (NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas))
                {
                    best = hit.position; bd = d; found = true;
                }
            }
        }
        if (found) { candidate = best; return true; }

        candidate = origin;
        return false;
    }

    IEnumerable<Vector3> EnumeratePatrolPoints()
    {
        if (generatorsRoot) foreach (Transform t in generatorsRoot) yield return t.position;
        if (exitsRoot)      foreach (Transform t in exitsRoot)      yield return t.position;
    }
    #endregion

    #region ML 보조 조향(선택)
    void ApplyMLSteering()
    {
        if (!hasTarget) return;
        var to = currentTarget - transform.position; to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;

        var dir = to.normalized;
        var desired = dir * mlForwardScale;
        agent.velocity = Vector3.Lerp(agent.velocity, desired, 0.12f);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.18f);
    }
    #endregion

    #region Sound system hook (use your existing bus)
    // 당신의 사운드 이벤트 버스에 이 메서드만 구독 추가하세요.
    public void OnNoiseHeard(Vector3 worldPos)
    {
        lastHeardPos = worldPos;
        hasHeard = true;
        SetDestination(worldPos);
        SetState(SubState.Investigate, MonsterMode.Investigate);
    }

    void TryHookYourSoundSystem(bool subscribe)
    {
        // 예)
        // if (subscribe)  SoundBus.OnNoise += OnNoiseHeard;
        // else            SoundBus.OnNoise -= OnNoiseHeard;
    }
    #endregion
}
