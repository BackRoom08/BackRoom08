// MLMonsterAgent.cs

using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

[RequireComponent(typeof(NavMeshAgent))]
public class MLMonsterAgent : Agent, IAIMonsterHearing
{
    [Header("Refs")]
    public Transform player;              // 추격 대상 (콜라이더 없어도 OK)
    public Transform eye;                 // 눈(레이 출발점)
    [Tooltip("시야를 가리는 레이어들만 포함(플레이어 레이어 제외 권장)")]
    public LayerMask occlusionMask;       // 시야 가림 마스크
    private NavMeshAgent agent;

    [Header("Vision")]
    public float viewDistance = 40f;      // 최대 시야거리
    [Range(0,180f)] public float viewHalfAngle = 70f; // 시야 반각

    [Header("Chase/Search")]
    public float localMoveRadius = 3f;       // 연속 액션 오프셋 반경
    public float investigateHoldTime = 2f;   // 소리 지점 도착 후 둘러보기 시간
    public float catchDistance = 1.4f;       // ★ 포착(근접) 거리(콜라이더 없이도 동작)
    public float episodeTime = 80f;          // 에피소드 제한 시간(초)
    public float pathEvalInterval = 0.5f;    // 경로 단축 보상 평가 주기

    [Header("Hit/Idle After Hit")]
    [Tooltip("플레이어 포착 시 제자리 정지 유지 시간(초)")]
    public float idleAfterHitSeconds = 5f;   // 요청 기능
    float hitIdleTimer;                      // 현재 남은 정지 시간

    [Header("Animation (옵션)")]
    public Animator anim;
    public string speedParam = "Speed";
    public string chaseBool = "IsChasing";
    public string investigateBool = "IsInvestigate";

    // ── 소리 기억 ──
    Vector3 lastHeardPos;
    float   lastHeardPower;   // 0~1로 클램프해 사용
    float   lastHeardTime;

    // ── 내부 상태 ──
    float investigateUntil;
    bool  investigateArrivedGiven; // 소리 지점 보상 1회 지급 플래그

    float lastPathLen = -1f;
    float pathEvalTimer;

    float epTimer;

    // 애니 파라미터 존재 캐시
    bool _hasSpeed, _hasChase, _hasInvestigate;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!eye) eye = transform;
        agent.updateRotation = false;  // 회전은 수동제어(모서리 긁힘 방지)
        agent.autoRepath = true;
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

    public override void OnEpisodeBegin()
    {
        // 시작 위치 무작위(맵에 맞게 조절)
        Vector3 m = transform.position + Random.insideUnitSphere * 12f; m.y = 0f;
        Vector3 p = (player ? player.position : transform.position) + Random.insideUnitSphere * 12f; p.y = 0f;

        var mPos = SampleNav(m);
        var pPos = SampleNav(p);

        agent.ResetPath();
        agent.Warp(mPos);                         // NavMesh 상태 깨끗하게 이동
        if (player) player.position = pPos;

        lastHeardPos   = agent.transform.position;
        lastHeardPower = 0f;
        lastHeardTime  = -999f;
        investigateUntil = 0f;
        investigateArrivedGiven = false;

        lastPathLen = GetPathLength(agent.transform.position, player ? player.position : agent.transform.position);
        pathEvalTimer = 0f;
        epTimer = 0f;

        hitIdleTimer = 0f; // 정지 타이머 초기화
    }

    // 외부 HearingSensor에서 호출
    public void OnHearNoise(Vector3 pos, float perceived, NoiseEvent raw)
    {
        lastHeardPos   = pos;
        lastHeardPower = Mathf.Clamp01(perceived);
        lastHeardTime  = Time.time;
        investigateUntil = 0f;            // 새 단서면 조사 타이머 리셋
        investigateArrivedGiven = false;   // 새 단서에 대해 다시 1회 보상 허용
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1) 플레이어 시야/거리/방향
        bool visible = CanSeePlayer(out Vector3 ppos);
        sensor.AddObservation(visible ? 1f : 0f); // (1)

        Vector3 toP = player ? (player.position - transform.position) : Vector3.zero;
        toP.y = 0f;
        Vector2 toPdir = toP.sqrMagnitude > 1e-6f ? new Vector2(toP.x, toP.z).normalized : Vector2.zero;
        sensor.AddObservation(toPdir);                          // (2)
        sensor.AddObservation(Mathf.Clamp01(toP.magnitude/50f));// (1) 맵 크기에 맞춰 스케일

        // 2) 최근 소리 요약
        Vector3 toN = lastHeardPos - transform.position; toN.y = 0f;
        Vector2 toNdir = toN.sqrMagnitude > 1e-6f ? new Vector2(toN.x, toN.z).normalized : Vector2.zero;
        sensor.AddObservation(toNdir);                          // (2)
        sensor.AddObservation(Mathf.Clamp01((Time.time - lastHeardTime) / 6f)); // (1) 경과 시간 정규화
        sensor.AddObservation(Mathf.Clamp01(lastHeardPower));   // (1)
        sensor.AddObservation(Mathf.Clamp01(toN.magnitude/50f));// (1)

        // 3) 현재 경로 길이(정규화)
        float pathLen = GetPathLength(transform.position, player ? player.position : transform.position);
        sensor.AddObservation(Mathf.Clamp01(pathLen / 120f));   // (1)
        // 총 10차원
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float dt = Time.deltaTime;

        // ★ 히트 후 정지 상태 우선 처리 (거리 포착으로만 운용)
        if (hitIdleTimer > 0f)
        {
            hitIdleTimer -= dt;
            agent.isStopped = true;                 // 제자리 정지
            AddReward(-0.01f * dt);                 // 가벼운 시간 페널티(선택)
            UpdateAnimatorFlags(visible:false, investigating:false);
            return; // 정지 중 로직 종료
        }

        // 기본 타겟: 보이면 플레이어, 아니면 최근 소리, 둘 다 없으면 전방
        bool visible = CanSeePlayer(out Vector3 ppos);
        Vector3 baseTarget;
        if (visible) baseTarget = ppos;
        else if (Time.time - lastHeardTime < 6f) baseTarget = lastHeardPos;
        else baseTarget = transform.position + transform.forward * 4f;

        // 연속 액션(오프셋)
        float ax = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float az = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        Vector3 worldOffset = transform.TransformDirection(new Vector3(ax, 0f, az)) * localMoveRadius;
        Vector3 final = baseTarget + worldOffset;
        SetDestination(final);

        // 조사 상태/회전/정지 처리
        bool investigating = (Time.time < investigateUntil) && !visible;
        if (!visible && Time.time - lastHeardTime < 6f)
        {
            // 소리 지점 도착 보상(1회)
            if (!investigateArrivedGiven &&
                (transform.position - lastHeardPos).sqrMagnitude <= 1f * 1f)
            {
                AddReward(+0.2f);
                investigateArrivedGiven = true;
                if (investigateUntil <= 0f)
                    investigateUntil = Time.time + investigateHoldTime;
            }
        }

        if (investigating)
        {
            agent.isStopped = true; // 조사 중에는 제자리 회전만
            transform.Rotate(0f, 120f * dt, 0f);
        }
        else
        {
            agent.isStopped = false;
            if (agent.hasPath)
            {
                Vector3 to = agent.steeringTarget - transform.position; to.y = 0f;
                if (to.sqrMagnitude > 1e-6f)
                {
                    var rot = Quaternion.LookRotation(to, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, rot, agent.angularSpeed * dt);
                }
            }
        }

        // ── 보상 (초당 스케일) ──
        AddReward(-0.02f * dt);           // 시간 페널티
        if (visible) AddReward(+0.05f * dt); // 시야 유지 가점

        // 경로 단축 보상: 0.5초 간격으로 의미 있는 감소만
        pathEvalTimer += dt;
        if (pathEvalTimer >= pathEvalInterval)
        {
            float curLen = GetPathLength(transform.position, player ? player.position : transform.position);
            if (lastPathLen > 0f && curLen + 0.2f < lastPathLen)
                AddReward(+0.05f);
            lastPathLen = curLen;
            pathEvalTimer = 0f;
        }

        // ★ 포착(근접) 시 보상 + 5초 정지 적용 (에피소드 계속 진행)
        agent.stoppingDistance = catchDistance * 0.8f;
        if (player && Vector3.Distance(transform.position, player.position) <= catchDistance)
        {
            AddReward(+1.0f);
            hitIdleTimer = idleAfterHitSeconds; // 5초 정지
            agent.ResetPath();
            agent.isStopped = true;
            UpdateAnimatorFlags(visible:false, investigating:false);
            return;
        }

        // 에피소드 타임아웃(실패 가중)
        epTimer += dt;
        if (epTimer >= episodeTime)
        {
            AddReward(-0.2f);
            EndEpisode();
            return;
        }

        // 애니메이터 플래그
        UpdateAnimatorFlags(visible, investigating);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = (Input.GetKey(KeyCode.D) ? 1 : 0) + (Input.GetKey(KeyCode.A) ? -1 : 0);
        ca[1] = (Input.GetKey(KeyCode.W) ? 1 : 0) + (Input.GetKey(KeyCode.S) ? -1 : 0);
    }

    // ────── 유틸 ──────
    bool CanSeePlayer(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (!player) return false;

        Vector3 v = player.position - eye.position;
        float d = v.magnitude; if (d > viewDistance) return false;

        Vector3 f = transform.forward; f.y = 0f;
        Vector3 flat = v; flat.y = 0f;
        if (Vector3.Angle(f, flat) > viewHalfAngle) return false;

        // 가림 체크(플레이어 레이어는 occlusionMask에서 제외되어 있어야 함)
        if (Physics.Raycast(eye.position, v.normalized, out var hit, d, occlusionMask))
            return false;

        pos = player.position;
        return true;
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

    void UpdateAnimatorFlags(bool visible, bool investigating)
    {
        if (!anim) return;
        float spd = agent ? agent.velocity.magnitude : 0f;
        if (_hasSpeed)       anim.SetFloat(speedParam, spd);
        if (_hasChase)       anim.SetBool(chaseBool, visible);
        if (_hasInvestigate) anim.SetBool(investigateBool, investigating);
    }
}
