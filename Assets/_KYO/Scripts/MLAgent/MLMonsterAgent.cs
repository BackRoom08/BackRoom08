using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public enum CandidateKind
{
    LastSeen, Noise, Gen_Ahead, Gen_Near, ExitDoor, PatrolNext, AmbushMid,
    PlayerNow, PlayerLead // 시야일 때 플레이어 현재/예측 지점
}

[RequireComponent(typeof(NavMeshAgent))]
public class MLMonsterAgent : Agent, IMonsterStatus, IAIMonsterHearing
{
    // ───────────────────────────── References ─────────────────────────────
    [Header("References")]
    [SerializeField] Transform player;               // 플레이어 Transform
    [SerializeField] Transform generatorsRoot;       // 발전기들의 부모(자식들을 수집)
    [SerializeField] Transform exitDoor;             // 탈출문 Transform(선택)
    [SerializeField] LayerMask losMask = ~0;         // 시야(Line Of Sight) 차단용 레이어 마스크
    [SerializeField] Animator animator;              // 몬스터 애니메이터(선택)

    // ───────────────────────────── Move / Locomotion ─────────────────────────────
    [Header("Move / Locomotion")]
    [SerializeField] float walkSpeed = 2.5f;         // 순찰/이동 기본 속도
    [SerializeField] float runSpeed = 4.0f;          // 추격·수색 기본 주행 속도
    [SerializeField] float runSpeedEmpowered = 5.5f; // 강화 상태에서의 주행 속도
    [SerializeField] float attackRange = 2.0f;       // 근접 공격 유효 거리(단위: m)
    [SerializeField] float repathCooldown = 0.35f;   // 경로 재계산 최소 간격(초)
    [SerializeField] float patrolReachRadius = 3f;   // 순찰포인트 도착 판정 반경(단위: m)

    // ───────────────────────────── Vision ─────────────────────────────
    [Header("Vision (FOV & Distance)")]
    [SerializeField] float viewDistance = 40f;       // 최초 인식 최대 거리
    [SerializeField] float viewAngleDeg = 120f;      // FOV(시야각, 전체 각도)
    [SerializeField] float minChaseTime = 1.0f;      // Chase 진입 후 최소 유지 시간(초)
    [SerializeField] float graceAfterLost = 1.5f;    // 시야 잃은 뒤 유예 시간(추격 유지)
    [SerializeField] float loseByDistance = 120f;    // 너무 멀어지면 추격 해제되는 거리
    [SerializeField] float reSeeDistance = 36f;      // 재인식 허용 거리(깜빡임 방지)

    // ───────────────────────────── Stalk / Empower ─────────────────────────────
    [Header("Stalk / Empower Rules")]
    [SerializeField] float stillNeeded = 2f;         // Stalk 중 ‘정지 유지’가 필요한 최소 시간(초)
    [SerializeField] float stalkNeed = 30f;          // 강화 진입에 필요한 누적 스택 시간(초)
    [SerializeField] float empowerDur = 40f;         // 강화 상태 유지 시간(초)
    [SerializeField] float stalkMinDist = 12f;       // Stalk이 가능한 거리 하한
    [SerializeField] float stalkMaxDist = 50f;       // Stalk이 가능한 거리 상한
    [SerializeField] float fovCos = 0.6f;            // “플레이어가 나를 보는지” 판정 임계(코사인 값)

    // ───────────────────────────── Observation / Action ─────────────────────────────
    [Header("Observation / Action Shape")]
    [SerializeField] int targetChoiceK = 8;          // 후보 포인트 K(관측 크기 = 14 + 2K, K=8 → 30)

    // ───────────────────────────── Investigate (Noise) ─────────────────────────────
    [Header("Investigate (Noise)")]
    [SerializeField] float recentNoiseWindow = 0.8f;     // “최근 소리”로 간주할 시간 창(초)
    [SerializeField] float invArriveRadius = 5f;         // 소리 앵커 도착 판정 반경(링 스캔 시작)
    [SerializeField] float invScanRingRadius = 7.0f;     // 도착 후 원형 스캔 반경
    [SerializeField] int   invScanPoints = 3;            // 링 위 스캔 포인트 개수
    [SerializeField] float invScanHold = 0.9f;           // 각 스캔 포인트에서 머무는 최소 시간(초)
    [SerializeField] float invGiveUpTime = 4.0f;         // 수색 포기(타임아웃) 시간(초)
    [SerializeField] float noiseClusterRadius = 1.5f;    // 연속 소리 묶음으로 볼 최대 거리(클러스터 반경)
    [SerializeField, Range(0,1)] float noiseEmaAlpha = 0.35f; // 소리 앵커 보정(E.M.A. 알파)
    [SerializeField] bool invLockUntilArrive = true;     // 앵커 도착 전까지 목표 고정(흔들림 억제)

    // ───────────────────────────── Stability ─────────────────────────────
    [Header("Stability")]
    [SerializeField] float modeMinHold = 0.6f;           // 모드 변경 최소 유지 시간(초)
    [SerializeField] float goalMinHold = 0.25f;          // 이동 목표 변경 최소 유지 시간(초)

    // ───────────────────────────── Objective Lock ─────────────────────────────
    [Header("Objective Lock (Generators/Exit)")]
    [SerializeField] bool  objectiveLockEnabled = true;  // 발전기/문 목표 고정 시스템 사용 여부
    [SerializeField] float objectiveMinHold = 1.2f;      // (참고) 목표 최소 유지에 활용되는 시간 값
    [SerializeField] float objectiveGiveUpTime = 12f;    // 목표에 너무 오래 매달리면 포기(초)
    [SerializeField] float objectiveArriveRadius = 2.5f; // 목표 도착 판정 반경
    [SerializeField] float nearObjectiveHoldRad = 4f;    // 목표 근접 시 잠깐 더 고정하는 반경

    // ───────────────────────────── Gating ─────────────────────────────
    [Header("Event-Forced Gating")]
    [SerializeField] bool forceInvestigateOnNoise = true; // ‘안 보임 + 최근 소리’면 Investigate 강제
    [SerializeField] bool forceSightModeGate = true;      // 보이면 Stalk/Chase로 강제 게이팅
    [SerializeField] float stalkRingSlack = 0.5f;         // Stalk 링 경계 여유(경계 떨림 방지)

    [Header("Sight Overrides")]
    [SerializeField] bool sightBreaksLocks = true;        // 보이면 Investigate/Objective 락 해제
    [SerializeField] bool clearNoiseOnSight = true;       // 보이면 소리 앵커 무시(초기화)

    // ───────────────────────────── Rewards ─────────────────────────────
    [Header("Training Rewards (only during training)")]
    [SerializeField] bool  trainingRewards = true;         // 보상 로깅/학습용 on/off
    [SerializeField] float rewardSeeLOSPerSec = 0.001f;    // 플레이어를 보고 있을 때 초당 보상
    [SerializeField] float rewardStalkPerSec  = 0.003f;    // Stalk 스택 중 초당 보상
    [SerializeField] float rewardStackFull    = 0.1f;      // 스택 충족(강화 진입) 보상
    [SerializeField] float rewardInvestigateArrive = 0.02f;// 소리 앵커 도착 보상
    [SerializeField] float rewardRediscover   = 0.05f;     // 새 단서(소리 등) 발견 보상
    [SerializeField] float rewardKill         = 1.0f;      // 처치 보상
    [SerializeField] float penaltyIdleSpinPerSec = -0.001f;// 빈 정지/빙글빙글 패널티
    [SerializeField] float rewardApproachNoise = 0.002f;   // 소리 앵커에 가까워지면 차등 보상
    [SerializeField] float rewardApproachObjective = 0.0015f; // 목표(발전기/문)에 접근 보상

    // ───────────────────────────── Inspector Debug ─────────────────────────────
    [Header("Inspector Debug (Runtime)")]
    [SerializeField] MonsterMode modeDebug;                // 현재 모드 미러
    [SerializeField] bool isEmpoweredInspector;            // 강화 여부 미러
    [SerializeField] float stalkStackRemainingInspector;   // 강화까지 남은 시간(초) 미러
    [SerializeField] string investigateSubState;           // Investigate 세부 상태 표시

    // ───────────────────────────── 내부 상태 ─────────────────────────────
    NavMeshAgent agent;                                   // 내비 메시 에이전트 핸들
    readonly List<Transform> generatorPoints = new();     // 발전기 위치들(순찰/후보)
    readonly List<Transform> patrolPoints = new();        // 순찰 후보(발전기 + 문)
    int patrolIndex;                                      // 현재 순찰 인덱스
    float lastSetDestTime;                                // 마지막 경로설정 시간(쿨다운용)
    Vector3 currentGoal;                                  // 현재 움직일 목표 지점

    // 소리 메모리/앵커
    Vector3 lastNoisePos;                                 // 마지막으로 들은 소리 위치
    float lastNoisePower;                                 // 마지막 소리의 강도(0~1)
    float lastNoiseTime;                                  // 마지막 소리 시간
    const float noiseMemory = 6f;                         // 소리 기억 유지 시간(초)
    Vector3 noiseAnchor;                                  // 수색 기준 앵커(EMA)
    float noiseAnchorTime;                                // 앵커 갱신 시간
    bool hasNoiseAnchor;                                  // 앵커 보유 여부

    // Investigate 로컬 탐색
    Vector3 invCommittedGoal;                             // 도중 흔들림 방지용 고정 목표
    float   invCommittedAt;                               // 그 목표로 고정된 시각
    int     invScanIdx;                                   // 링 스캔 현재 인덱스
    float   invScanCommittedAt;                           // 현재 링 포인트에 머문 시작시각
    float   invStartedAt;                                 // Investigate 진입 시각

    // 추격 점착
    float   chaseStartTime = -999f;                       // Chase 시작 시각
    float   lastSeenTime  = -999f;                        // 마지막으로 플레이어 본 시각
    Vector3 lastSeenPos;                                   // 마지막으로 플레이어 본 위치

    // 주시/강화
    float   stillTimer;                                   // ‘정지 유지’ 타이머(스택 조건)
    [SerializeField] float stareStack;                    // 현재 연속 Stalk 스택(초)
    [SerializeField] float stalkStackTotal;               // 누적 스택(초, 리셋 안 함)
    float   stalkStackBest;                               // 최고 연속 스택(디버깅용)
    bool    isEmpowered;                                  // 강화 상태 여부
    float   empowerRemain;                                // 강화 남은 시간(초)

    // 플레이어 속도 추정
    Vector3 prevPlayerPos;                                // 이전 프레임 플레이어 위치
    Vector3 playerVel;                                    // 추정 플레이어 속도

    // 커밋(흔들림 방지)
    MonsterMode committedMode = MonsterMode.Idle;         // 최근 커밋된 모드
    float      committedModeAt;                           // 모드 커밋 시각
    Vector3    committedGoal;                             // 최근 커밋된 목표 지점
    float      committedGoalAt;                           // 목표 커밋 시각

    // Objective Lock
    bool         objectiveLocked;                         // 목표 락 활성화 여부
    CandidateKind objectiveKind;                          // 락의 종류(발전기/문 등)
    Vector3      objectiveGoal;                           // 락된 목표 지점
    float        objectiveLockedAt;                       // 락 시작 시각
    float        prevNoiseDist = -1f, prevObjectiveDist = -1f; // 접근 보상용 이전 거리

    // IMonsterStatus (외부 노출용)
    public MonsterMode CurrentMode { get; private set; } = MonsterMode.Idle; // 현재 모드
    public bool  IsEmpowered => isEmpowered;            // 강화 여부(읽기 전용)
    public float StareStack  => stareStack;             // 현재 연속 스택(초) 읽기 전용
    public float EmpowerRemain => empowerRemain;        // 강화 잔여시간(초) 읽기 전용

    // 후보 구조체(목표 선택 풀)
    struct Candidate
    {
        public CandidateKind kind;   // 후보의 종류(소리/플레이어/발전기/문/순찰 등)
        public Vector3 pos;          // 후보 좌표
        public float hScore;         // 휴리스틱 점수(정렬/필터링용)
        public Transform t;          // 원본 트랜스폼(있으면)
    }
    readonly List<Candidate> candidates = new();         // 현재 프레임 후보들


    // ───────────────────────────── Unity / MLAgents ─────────────────────────────
    public override void Initialize()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        CachePoints();

        patrolIndex = 0;
        if (patrolPoints.Count > 0) currentGoal = patrolPoints[0].position;
        if (player) prevPlayerPos = player.position;
        invStartedAt = -999f;
    }

    void CachePoints()
    {
        generatorPoints.Clear();
        if (generatorsRoot)
        {
            for (int i = 0; i < generatorsRoot.childCount; i++)
            {
                var t = generatorsRoot.GetChild(i);
                if (t && t.gameObject.activeInHierarchy) generatorPoints.Add(t);
            }
        }
        patrolPoints.Clear();
        patrolPoints.AddRange(generatorPoints);
        if (exitDoor) patrolPoints.Add(exitDoor);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) 플레이어 상대 방향/거리/LOS  → 2 + 1 + 1 = 4
        Vector3 toP = Vector3.zero; float dist = 999f;
        if (player) { toP = player.position - transform.position; dist = toP.magnitude; }
        Vector3 dir = (dist > 0.001f) ? toP / Mathf.Max(1f, dist) : Vector3.zero;
        sensor.AddObservation(new Vector2(dir.x, dir.z));
        sensor.AddObservation(Mathf.Clamp01(dist / 40f));
        sensor.AddObservation(HasVisualOnPlayerStrict(out _) ? 1f : 0f);

        // 2) 플레이어가 나를 보는 정도(0~1) → 1
        float pf = 0f;
        if (player)
        {
            Vector3 toMe = (transform.position - player.position).normalized;
            pf = Mathf.Max(0f, Vector3.Dot(player.forward, toMe));
        }
        sensor.AddObservation(pf);

        // 3) 소리 기억(상대 좌표 3 + 파워 1 + 최근성 1) → 5
        float noiseAge = Mathf.Clamp01((Time.time - lastNoiseTime) / noiseMemory);
        sensor.AddObservation(new Vector3(lastNoisePos.x - transform.position.x, 0f, lastNoisePos.z - transform.position.z));
        sensor.AddObservation(lastNoisePower);
        sensor.AddObservation(1f - noiseAge);

        // 4) 내 상태 (모드 1 + 강화 1 + 스택N 1 + 강화남은N 1) → 4
        sensor.AddObservation((int)CurrentMode);
        sensor.AddObservation(isEmpowered ? 1f : 0f);
        sensor.AddObservation(Mathf.Clamp01(stareStack / stalkNeed));
        sensor.AddObservation(Mathf.Clamp01(empowerRemain / empowerDur));
        // subtotal = 14

        // 5) 고정 K개 후보 상대좌표(각 2) → 2*K
        for (int i = 0; i < targetChoiceK; i++)
        {
            Vector3 rel = Vector3.zero;
            if (i < patrolPoints.Count)
            {
                rel = patrolPoints[i].position - transform.position;
                rel.y = 0f;
            }
            sensor.AddObservation(new Vector2(rel.x, rel.z));
        }
        // total = 14 + 2*K
    }

    // 보이면: 링 안 & 들키지 않으면 Stalk, 아니면 Chase
    MonsterMode DecideSightMode(float dist)
    {
        bool inRing = dist >= (stalkMinDist - 0.5f) && dist <= (stalkMaxDist + 0.5f);
        bool playerFacesMe = false;
        if (player)
        {
            Vector3 toMe = (transform.position - player.position).normalized;
            playerFacesMe = Vector3.Dot(player.forward, toMe) > fovCos;
        }
        return (inRing && !playerFacesMe) ? MonsterMode.Stalk : MonsterMode.Chase;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 플레이어 속도 추정
        if (player)
        {
            playerVel = (player.position - prevPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f);
            prevPlayerPos = player.position;
        }

        var d = actions.DiscreteActions;
        var c = actions.ContinuousActions;
        MonsterMode policyMode = (MonsterMode)Mathf.Clamp(d[0], 0, 5);
        int policyIdx = Mathf.Clamp(d.Length > 1 ? d[1] : 0, 0, Mathf.Max(0, targetChoiceK - 1));
        Vector2 offset = c.Length >= 2 ? new Vector2(c[0], c[1]) : Vector2.zero;

        bool nowSee = HasVisualOnPlayerStrict(out float dist);
        bool freshNoise = (Time.time - lastNoiseTime) < recentNoiseWindow;

        // 즉시 공격
        if (CanMeleeKill()) { DoMelee(); if (trainingRewards) AddReward(rewardKill); return; }

        // 강화면 Chase
        if (isEmpowered) policyMode = MonsterMode.Chase;

        // 시야가 보이면 조사/목표 락 해제 + Stalk/Chase로 전환
        if (nowSee)
        {
            if (sightBreaksLocks)
            {
                ObjectiveUnlock();
                invCommittedGoal = Vector3.zero;
                if (clearNoiseOnSight) hasNoiseAnchor = false;
            }
            policyMode = DecideSightMode(dist);
            committedMode = policyMode;
            committedModeAt = Time.time;
        }
        else
        {
            // 안 보이면서 최근 소리면 Investigate
            if (forceInvestigateOnNoise && freshNoise)
                policyMode = MonsterMode.Investigate;
        }

        if (nowSee && (committedMode == MonsterMode.Investigate || objectiveLocked))
            ObjectiveUnlock();

        // 모드 최소 유지
        MonsterMode finalMode = policyMode;
        if (Time.time - committedModeAt < modeMinHold) finalMode = committedMode;
        else if (finalMode != committedMode) { committedMode = finalMode; committedModeAt = Time.time; }

        // Chase 점착
        if (finalMode == MonsterMode.Chase)
        {
            if (chaseStartTime < 0f) chaseStartTime = Time.time;
            if (nowSee) { lastSeenTime = Time.time; if (player) lastSeenPos = player.position; }
            bool hold = (Time.time - chaseStartTime) < minChaseTime || (Time.time - lastSeenTime) <= graceAfterLost;
            if (hold) finalMode = MonsterMode.Chase;
            if (dist > loseByDistance) finalMode = MonsterMode.Investigate;
        }
        else chaseStartTime = -999f;

        // Investigate 진입 초기화
        if (CurrentMode != MonsterMode.Investigate && finalMode == MonsterMode.Investigate)
        {
            invStartedAt = Time.time;
            invScanIdx = 0; invScanCommittedAt = 0f; invCommittedAt = 0f;
            invCommittedGoal = Vector3.zero;
        }

        CurrentMode = finalMode;

        // 후보 생성 & 선택
        BuildCandidates(nowSee, dist);
        if (candidates.Count == 0) return;
        int idx = Mathf.Clamp(policyIdx, 0, candidates.Count - 1);
        var chosen = candidates[idx];
        Vector3 goal = chosen.pos;

        // Objective Lock
        bool isObjective = (chosen.kind == CandidateKind.Gen_Near || chosen.kind == CandidateKind.Gen_Ahead || chosen.kind == CandidateKind.ExitDoor);
        if (objectiveLockEnabled)
        {
            if (objectiveLocked)
            {
                float dObj = Vector3.Distance(transform.position, objectiveGoal);
                bool arrived = dObj <= objectiveArriveRadius;
                bool timeout = (Time.time - objectiveLockedAt) > objectiveGiveUpTime;

                if (nowSee || freshNoise || arrived || timeout) ObjectiveUnlock();
                else
                {
                    if (dObj <= nearObjectiveHoldRad) committedGoalAt = Time.time + goalMinHold;
                    goal = objectiveGoal;
                    if (trainingRewards && prevObjectiveDist >= 0f) AddReward(rewardApproachObjective * (prevObjectiveDist - dObj));
                    prevObjectiveDist = dObj;
                }
            }
            else if (isObjective && !nowSee && !freshNoise)
            {
                ObjectiveLock(chosen.kind, chosen.pos);
                goal = objectiveGoal;
            }
        }

        // Investigate: 도착 전 고정, 도착 후 링 스캔
        if (CurrentMode == MonsterMode.Investigate && !nowSee)
        {
            if (invLockUntilArrive && invCommittedGoal != Vector3.zero && !ArrivedXZ(invCommittedGoal, invArriveRadius) && !freshNoise)
                goal = invCommittedGoal;
            else { invCommittedGoal = goal; invCommittedAt = Time.time; }

            if (hasNoiseAnchor && HasLOSToPoint(noiseAnchor, true))
            {
                float dA = Vector3.Distance(transform.position, noiseAnchor);
                if (!HasVisualOnPlayerStrict(out _) && dA <= invArriveRadius)
                {
                    if ((Time.time - invScanCommittedAt) > invScanHold)
                    {
                        invScanIdx = (invScanIdx + 1) % Mathf.Max(3, invScanPoints);
                        invScanCommittedAt = Time.time;
                    }
                    float ang = (360f / Mathf.Max(3, invScanPoints)) * invScanIdx * Mathf.Deg2Rad;
                    Vector3 o = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * invScanRingRadius;
                    goal = noiseAnchor + o;

                    if ((Time.time - invStartedAt) > invGiveUpTime) hasNoiseAnchor = false;
                }
            }
        }

        // 목표 최소 유지
        if (Time.time - committedGoalAt < goalMinHold) goal = committedGoal;
        else if ((committedGoal - goal).sqrMagnitude > 1.0f) { committedGoal = goal; committedGoalAt = Time.time; }

        // Continuous 오프셋
        float offsetScale = (CurrentMode == MonsterMode.Investigate) ? 0.4f : 1.5f;
        goal += new Vector3(offset.x, 0f, offset.y) * offsetScale;

        // 이동/애니/스택
        ApplyLocomotion(goal);
        UpdateEmpowerAndStack(nowSee);
        if (animator) animator.SetFloat("Speed", agent.velocity.magnitude);

        // ── 인스펙터 디버그 미러링 ──
        modeDebug = CurrentMode;
        isEmpoweredInspector = isEmpowered;
        stalkStackRemainingInspector = Mathf.Max(0f, stalkNeed - stareStack);
        if (CurrentMode == MonsterMode.Investigate)
        {
            bool arrived = hasNoiseAnchor && Vector3.Distance(transform.position, noiseAnchor) <= invArriveRadius;
            bool seeP = HasVisualOnPlayerStrict(out _);
            investigateSubState = (!arrived || seeP) ? "GoToAnchor" : "RingScan";
        }
        else investigateSubState = "-";

        // 보상(학습 시)
        if (trainingRewards)
        {
            if (nowSee) AddReward(rewardSeeLOSPerSec * Time.deltaTime);
            if (agent.velocity.sqrMagnitude < 0.01f && CurrentMode == MonsterMode.Patrol)
                AddReward(penaltyIdleSpinPerSec * Time.deltaTime);

            if (hasNoiseAnchor && (Time.time - lastNoiseTime) < noiseMemory)
            {
                float dNoise = Vector3.Distance(transform.position, noiseAnchor);
                if (prevNoiseDist >= 0f) AddReward(rewardApproachNoise * (prevNoiseDist - dNoise));
                prevNoiseDist = dNoise;
            }
            else prevNoiseDist = -1f;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        var c = actionsOut.ContinuousActions;
        d[0] = (int)MonsterMode.Patrol;
        d[1] = patrolIndex % Mathf.Max(1, targetChoiceK);
        c[0] = 0f; c[1] = 0f;
    }

    // ───────────────────────────── 목표 고정/해제 ─────────────────────────────
    void ObjectiveLock(CandidateKind kind, Vector3 goal)
    {
        objectiveLocked = true;
        objectiveKind = kind;
        objectiveGoal = goal;
        objectiveLockedAt = Time.time;
        committedGoal = goal; committedGoalAt = Time.time + goalMinHold;
        prevObjectiveDist = Vector3.Distance(transform.position, objectiveGoal);
    }
    void ObjectiveUnlock()
    {
        objectiveLocked = false;
        prevObjectiveDist = -1f;
    }

    // ───────────────────────────── 후보 생성 ─────────────────────────────
    void BuildCandidates(bool nowSee, float dist)
    {
        candidates.Clear();

        // 시야면 플레이어 현재/예측 지점 우선 추가
        if (nowSee && player)
        {
            Vector3 lead = player.position;
            float leadT = Mathf.Clamp(dist / Mathf.Max(1f, runSpeed), 0.2f, 0.8f);
            if (playerVel.sqrMagnitude > 0.01f) lead += playerVel * leadT;

            candidates.Add(new Candidate { kind = CandidateKind.PlayerLead, pos = lead,           hScore = 0.95f });
            candidates.Add(new Candidate { kind = CandidateKind.PlayerNow,  pos = player.position, hScore = 1.00f });
        }

        // 최근 본 위치
        if ((Time.time - lastSeenTime) <= graceAfterLost)
            candidates.Add(new Candidate{ kind=CandidateKind.LastSeen, pos=lastSeenPos, hScore=0.7f });

        bool invMode = (CurrentMode == MonsterMode.Investigate);
        bool anchorValid = hasNoiseAnchor && ((Time.time - noiseAnchorTime) < noiseMemory);

        if (invMode && anchorValid)
        {
            float dA = Vector3.Distance(transform.position, noiseAnchor);
            bool seeAnchor = HasLOSToPoint(noiseAnchor, true);

            if (!seeAnchor || dA > invArriveRadius)
            {
                candidates.Add(new Candidate{ kind=CandidateKind.Noise, pos=noiseAnchor, hScore=0.95f });
                candidates.Add(new Candidate{ kind=CandidateKind.Noise, pos=noiseAnchor + new Vector3( 1.5f,0,0), hScore=0.78f });
                candidates.Add(new Candidate{ kind=CandidateKind.Noise, pos=noiseAnchor + new Vector3(-1.5f,0,0), hScore=0.78f });
            }
            else if (!nowSee && dA <= invArriveRadius)
            {
                for (int i=0;i<Mathf.Max(3,invScanPoints);i++)
                {
                    float ang = (360f/Mathf.Max(3,invScanPoints)) * i * Mathf.Deg2Rad;
                    Vector3 o = new Vector3(Mathf.Cos(ang),0f,Mathf.Sin(ang)) * invScanRingRadius;
                    candidates.Add(new Candidate{ kind=CandidateKind.Noise, pos=noiseAnchor + o, hScore=0.85f });
                }
                candidates.Add(new Candidate{ kind=CandidateKind.Noise, pos=noiseAnchor, hScore=0.6f });
            }
            else
            {
                candidates.Add(new Candidate{ kind=CandidateKind.Noise, pos=noiseAnchor, hScore=0.9f });
            }
        }
        else
        {
            // 최근 소리
            if ((Time.time - lastNoiseTime) < noiseMemory)
            {
                float age = Mathf.Clamp01((Time.time - lastNoiseTime) / noiseMemory);
                candidates.Add(new Candidate{ kind=CandidateKind.Noise, pos=lastNoisePos, hScore=Mathf.Lerp(0.8f,0.2f,age) });
            }

            // 발전기들
            foreach (var g in generatorPoints)
            {
                if (!g) continue;
                float score = 0.6f;
                if (player)
                {
                    Vector3 dir = (playerVel.sqrMagnitude>0.01f? playerVel.normalized : player.forward);
                    float align = Mathf.Max(0f, Vector3.Dot(dir, (g.position - player.position).normalized));
                    score += align*0.15f;
                }
                candidates.Add(new Candidate{ kind=CandidateKind.Gen_Near, pos=g.position, hScore=score, t=g });
            }

            // 탈출문
            if (exitDoor)
            {
                float s = 0.55f;
                if (player)
                {
                    Vector3 dir = (playerVel.sqrMagnitude>0.01f? playerVel.normalized : player.forward);
                    s += Mathf.Max(0f, Vector3.Dot(dir, (exitDoor.position - player.position).normalized))*0.15f;
                }
                candidates.Add(new Candidate{ kind=CandidateKind.ExitDoor, pos=exitDoor.position, hScore=s, t=exitDoor });
            }

            // 다음 순찰 포인트
            if (patrolPoints.Count>0)
            {
                var next = patrolPoints[patrolIndex % patrolPoints.Count];
                candidates.Add(new Candidate{ kind=CandidateKind.PatrolNext, pos=next.position, hScore=0.15f, t=next });
            }

            // 길목(중간지점)
            if (player)
            {
                Vector3 mid = Vector3.Lerp(transform.position, player.position, 0.5f);
                candidates.Add(new Candidate{ kind=CandidateKind.AmbushMid, pos=mid, hScore=0.5f });
            }
        }

        // K개 제한(가까운 후보 우선)
        if (candidates.Count > targetChoiceK)
        {
            candidates.Sort((a,b)=>
                (a.pos - transform.position).sqrMagnitude.CompareTo((b.pos - transform.position).sqrMagnitude));
            candidates.RemoveRange(targetChoiceK, candidates.Count - targetChoiceK);
        }
    }

    // ───────────────────────────── 이동/언스턱 ─────────────────────────────
    void ApplyLocomotion(Vector3 goal)
    {
        currentGoal = goal;

        float baseRun = isEmpowered ? runSpeedEmpowered : runSpeed;
        float speed = (CurrentMode==MonsterMode.Chase || CurrentMode==MonsterMode.Investigate || isEmpowered) ? baseRun : walkSpeed;
        agent.speed = speed;

        bool stop = (CurrentMode==MonsterMode.Stalk && InStalkHoldRangeToStack());
        agent.isStopped = stop;

        if (!stop && Time.time - lastSetDestTime > repathCooldown)
        {
            if ((agent.destination - goal).sqrMagnitude > 1.0f)
            {
                if (TrySetDestinationSmart(goal))
                {
                    lastSetDestTime = Time.time;
                }
                else
                {
                    if (CurrentMode == MonsterMode.Investigate) hasNoiseAnchor = false;
                }
            }
        }

        // 순찰: 도착하면 다음
        if (CurrentMode==MonsterMode.Patrol && patrolPoints.Count>0)
        {
            var t = patrolPoints[patrolIndex % patrolPoints.Count];
            if (Vector3.SqrMagnitude(t.position - transform.position) <= patrolReachRadius*patrolReachRadius)
                patrolIndex = (patrolIndex + 1) % patrolPoints.Count;
        }

        // Investigate 도착 보상
        if (CurrentMode==MonsterMode.Investigate && (Time.time - lastNoiseTime) < noiseMemory)
        {
            if (Vector3.SqrMagnitude(noiseAnchor - transform.position) <= 4f && trainingRewards)
                AddReward(rewardInvestigateArrive);
        }
    }

    /// 경로 언스턱: goal → 바로 경로 / 근처 NavMesh 스냅 / Investigate 링 포인트 / 발전기·문 폴백
    bool TrySetDestinationSmart(Vector3 goal)
    {
        var path = new NavMeshPath();

        if (NavMesh.CalculatePath(transform.position, goal, NavMesh.AllAreas, path) &&
            path.status == NavMeshPathStatus.PathComplete)
        { agent.SetPath(path); return true; }

        if (NavMesh.SamplePosition(goal, out var hit, 2.5f, NavMesh.AllAreas))
        {
            if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            { agent.SetPath(path); return true; }
        }

        if (CurrentMode == MonsterMode.Investigate && hasNoiseAnchor)
        {
            int N = Mathf.Max(4, invScanPoints);
            for (int i = 0; i < N; i++)
            {
                float ang = (360f / N) * i * Mathf.Deg2Rad;
                Vector3 o = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * invScanRingRadius;
                Vector3 alt = noiseAnchor + o;
                if (NavMesh.SamplePosition(alt, out hit, 2.0f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete)
                { agent.SetPath(path); return true; }
            }
        }

        Transform fb = null; float bd = float.PositiveInfinity;
        foreach (var t in generatorPoints)
        {
            if (!t) continue;
            float d = (t.position - transform.position).sqrMagnitude;
            if (d < bd) { bd = d; fb = t; }
        }
        if (!fb && exitDoor) fb = exitDoor;

        if (fb &&
            NavMesh.CalculatePath(transform.position, fb.position, NavMesh.AllAreas, path) &&
            path.status == NavMeshPathStatus.PathComplete)
        { agent.SetPath(path); return true; }

        return false;
    }

    // ───────────────────────────── 시야/전투/스택 ─────────────────────────────
    bool HasVisualOnPlayerStrict(out float dist)
    {
        dist = 999f; 
        if (!player) return false;

        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 tgt = player.position   + Vector3.up * 1.6f;
        Vector3 v   = tgt - eye; 
        dist        = v.magnitude;

        // 거리 게이트: 최근에 봤으면 재인식 거리를 사용
        float maxD = (Time.time - lastSeenTime) < 0.2f ? viewDistance : reSeeDistance;
        if (dist > maxD) return false;

        // 추격 중/최근에 본 경우는 FOV 완화(깜빡임 방지)
        float halfFov = viewAngleDeg * 0.5f;
        if (CurrentMode == MonsterMode.Chase || (Time.time - lastSeenTime) < 0.5f)
            halfFov = Mathf.Min(89f, halfFov + 20f); // 최대 ~180 미만까지 완화
        float need = Mathf.Cos(halfFov * Mathf.Deg2Rad);

        if (Vector3.Dot(transform.forward, v.normalized) < need) 
            return false;

        // 레이마스크에서 플레이어/자기자신 레이어 제외
        int mask = losMask;
        mask &= ~(1 << gameObject.layer);
        mask &= ~(1 << player.gameObject.layer);

        // 시야 차폐 검사: 플레이어가 아닌 물체에 부딪히면 가려진 것
        if (Physics.Raycast(eye, v.normalized, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    bool HasLOSToPoint(Vector3 p, bool requireFOV = true)
    {
        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 v   = (p + Vector3.up * 1.6f) - eye;
        float d     = v.magnitude;

        if (requireFOV)
        {
            float halfFov = viewAngleDeg * 0.5f;
            if (CurrentMode == MonsterMode.Chase || (Time.time - lastSeenTime) < 0.5f)
                halfFov = Mathf.Min(89f, halfFov + 20f);
            float need = Mathf.Cos(halfFov * Mathf.Deg2Rad);
            if (Vector3.Dot(transform.forward, v.normalized) < need) 
                return false;
        }

        // 플레이어/자기자신 레이어 제외
        int mask = losMask;
        mask &= ~(1 << gameObject.layer);
        if (player) mask &= ~(1 << player.gameObject.layer);

        if (Physics.Raycast(eye, v.normalized, out var hit, d, mask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }


    bool CanMeleeKill()
    {
        if (!player) return false;
        Vector3 to = player.position - transform.position; to.y = 0f;
        if (to.magnitude > attackRange) return false;
        if (!HasVisualOnPlayerStrict(out _)) return false;
        return true;
    }

    void DoMelee()
    {
        agent.ResetPath();
        // TODO: 실제 대미지 시스템과 연결
    }

    void UpdateEmpowerAndStack(bool iSee)
    {
        // 정지 시간 누적(주시 스택 조건용)
        if (agent.velocity.sqrMagnitude < 0.05f) stillTimer += Time.deltaTime;
        else stillTimer = 0f;

        // 최근 본 시간/위치 갱신
        if (iSee) { lastSeenTime = Time.time; if (player) lastSeenPos = player.position; }

        // 플레이어가 나를 정면으로 보는가(들킴 여부)
        bool playerFacesMe = false;
        if (player)
        {
            Vector3 toMe = (transform.position - player.position).normalized;
            playerFacesMe = Vector3.Dot(player.forward, toMe) > fovCos;
        }
        bool undetected = !playerFacesMe;

        // ★ 스택 누적만 함: 조건 만족 시에만 +Δt, 조건을 못 채우면 값 유지(감소/리셋 없음)
        if (CurrentMode == MonsterMode.Stalk &&
            stillTimer >= stillNeeded &&
            iSee && undetected &&
            InStalkHoldRangeToStack())
        {
            stareStack = Mathf.Min(stalkNeed, stareStack + Time.deltaTime);
            if (trainingRewards) AddReward(rewardStalkPerSec * Time.deltaTime);
        }

        // ★ 예전 코드에서의 리셋 제거:
        // else if (playerFacesMe) stareStack = 0f;  // ← 이 줄은 삭제(더 이상 리셋하지 않음)

        // 스택 완료 → 강화 시작(로그 출력)
        if (!isEmpowered && stareStack >= stalkNeed)
        {
            isEmpowered = true;
            empowerRemain = empowerDur;
            if (trainingRewards) AddReward(rewardStackFull);
            print("강화모드 진입");
        }

        // 강화 시간 흐름(끝나면 일반모드로, 다음 사이클을 위해 스택 초기화)
        if (isEmpowered)
        {
            empowerRemain -= Time.deltaTime;
            if (empowerRemain <= 0f)
            {
                isEmpowered = false;
                stareStack = 0f; // ← 강화 사이클을 끝내고 다음 사이클 준비. 유지 원하면 이 줄 주석 처리.
                print("일반모드 진입");
            }
        }
    }


    bool InStalkHoldRangeToStack()
    {
        if (!player) return false;
        Vector3 to = player.position - transform.position; to.y = 0f;
        float d = to.magnitude;

        bool iSee = HasVisualOnPlayerStrict(out _);
        Vector3 toMe = (transform.position - player.position).normalized;
        bool playerFacesMe = Vector3.Dot(player.forward, toMe) > fovCos;

        return iSee && !playerFacesMe && d >= stalkMinDist && d <= stalkMaxDist;
    }

    // ───────────────────────────── Noise ─────────────────────────────
    public void OnHearNoise(Vector3 pos, float perceived, NoiseEvent e)
    {
        lastNoisePos = pos;

        // 플레이어 소리 약간 가중
        bool fromPlayer = e.Instigator &&
                          (e.Instigator == player?.gameObject || e.Instigator.CompareTag("Player"));
        lastNoisePower = Mathf.Clamp01(perceived * (fromPlayer ? 1.2f : 1.0f));

        lastNoiseTime = Time.time;

        // 소리 앵커 스무딩(클러스터 반경 내면 EMA)
        if (!hasNoiseAnchor || (Time.time - noiseAnchorTime) > invGiveUpTime ||
            Vector3.Distance(pos, noiseAnchor) > noiseClusterRadius)
        {
            noiseAnchor = pos; hasNoiseAnchor = true;
        }
        else noiseAnchor = Vector3.Lerp(noiseAnchor, pos, noiseEmaAlpha);

        noiseAnchorTime = Time.time;
        prevNoiseDist = Vector3.Distance(transform.position, noiseAnchor);

        if (trainingRewards) AddReward(rewardRediscover * 0.2f);
    }

    // ───────────────────────────── Utils ─────────────────────────────
    bool ArrivedXZ(Vector3 target, float tol)
    {
        if (target == Vector3.zero) return false;
        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = target; b.y = 0f;
        return (a - b).sqrMagnitude <= tol * tol;
    }
}
