using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    [SerializeField] [Tooltip("추격할 플레이어")] protected Transform player;
    [SerializeField] [Tooltip("플레이어를 추격 시작하는 거리")] protected float chaseRange = 20f;
    [SerializeField] [Tooltip("걷기 속도")] protected float walkSpeed = 2f;
    [SerializeField] [Tooltip("뛰기(추격) 속도")] protected float runSpeed = 7f;
    [SerializeField] [Tooltip("멈춤 상태 지속 시간")] protected float idleDuration = 2f;
    [SerializeField] [Tooltip("걷기 상태 지속 시간")] protected float walkDuration = 10f;
    [SerializeField] [Tooltip("랜덤으로 이동할 최소 거리")] protected float minMoveRange = 20f;
    [SerializeField] [Tooltip("랜덤으로 이동할 최대 거리")] protected float maxMoveRange = 70f;
    [SerializeField] [Tooltip("회전하는 최대 속도")] protected float angularSpeed  = 1000f;
    [SerializeField] [Tooltip("가속도(높을수록 즉시 속도가 붙음)")] protected float acceleration  = 50f;

    [SerializeField] [Tooltip("마지막 위치에서 대기할 시간")] private float waitLastPosition = 3f;
    [SerializeField, Tooltip("마지막 위치 오차 범위")] protected float lastPosArriveThreshold = 1.5f;
    protected Vector3 lastPlayerPosition;
    public  bool isPlayerInHide;
    
    protected NavMeshAgent agent;      // 이동을 담당하는 NavMeshAgent 컴포넌트
    protected Animator animator;        // 애니메이션 제어용 Animator 컴포넌트
    
    protected enum State { Idle,
        Walk,
        Chase,
        Wait 
    }  // Idle : 멈춤, Walk : 걷기, Chase : 추격, Wait : 기다림
    [SerializeField]protected State currState = State.Idle;  // 현재 상태
    protected Coroutine stateRoutine;   // 코루틴 값
    protected float curSpeed = 0f; // agent로 이동하는 현재 속도

    private float saveChaseRange = 0f;    // 추적거리 임시저장
    
    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        //stateTimer = Random.Range(0f, walkDuration);
        agent.isStopped = true;
        
        // 방향 회전 속도

        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
        saveChaseRange = chaseRange;

        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
    }

    protected virtual void Start()
    {
        ChangeState(State.Idle);
    }
    
    protected void ChangeState(State newState)
    {
        if (stateRoutine != null)
            StopCoroutine(stateRoutine);
        
        currState = newState;
        switch (newState)
        {
            case State.Idle:
                stateRoutine = StartCoroutine(IdleRoutine());
                //print("Idle");
                break;
            case State.Walk:
                stateRoutine = StartCoroutine(WalkRoutine());
                //print("walk");
                break;
            case State.Chase:
                stateRoutine = StartCoroutine(ChaseRoutine());
                //print("chase");
                break;
            case State.Wait:
                stateRoutine = StartCoroutine(WaitRoutine());
                //print("wait");
                break;
        }
    }
    
    // 멈춤 상태
    protected virtual IEnumerator IdleRoutine()
    {
        agent.isStopped = true;
        animator.SetFloat("Speed", 0);
        yield return new WaitForSeconds(idleDuration);
        ChangeState(State.Walk);
    }

    // 배회(걷기) 상태
    protected virtual IEnumerator WalkRoutine()
    {
        // agent.ResetPath();
        // 랜덤 목적지 설정
        // print("walk");
        float distance = Random.Range(minMoveRange, maxMoveRange);
        Vector3 randomDir = Random.insideUnitSphere * distance;
        randomDir += transform.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDir, out hit, 10f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            //print(hit.position);
        }
        agent.isStopped = false;
        agent.speed = walkSpeed;

        float timer = walkDuration;
        while (timer > 0f)
        {
            // 플레이어 감지 시 즉시 추격 전환
            if (Vector3.Distance(transform.position, player.position) < chaseRange && CanChasePlayer() && !isPlayerInHide)
            {
                ChangeState(State.Chase);
                yield break;
            }
            timer -= Time.deltaTime;
            if (agent.remainingDistance < 0.5f) break;
            yield return null;
        }
        ChangeState(State.Idle);
    }

    // 추격 상태
    protected virtual IEnumerator ChaseRoutine()
    {
        agent.isStopped = false;
        agent.speed = runSpeed;
        while (true)
        {
            if (!CanChasePlayer() || Vector3.Distance(transform.position, player.position) > chaseRange * 1.2f)
            {
                ChangeState(State.Idle);
                yield break;
            }
            // 공격모션 실행시 오류 방지 코드
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.SetDestination(player.position);
            }
            OnChasePlayer();
            yield return null;
        }
    }

    public float arriveTimer = 1f;
    protected virtual IEnumerator WaitRoutine()
    {
        // agent.ResetPath();
        agent.isStopped = false;
        agent.SetDestination(lastPlayerPosition);
        //arriveTimer = 1f;
        float arrTimer = arriveTimer;

        // 마지막 위치까지 이동
        while (Vector3.Distance(transform.position, lastPlayerPosition) > lastPosArriveThreshold)
        {
            // 도착 전에 플레이어가 다시 드러나면 즉시 추격 복귀
            if (!isPlayerInHide)
            {
                ChangeState(State.Chase);
                yield break;
            }
            arrTimer -= Time.deltaTime;
            if (arrTimer <= 0f) // 타임아웃
                break;
            yield return null;
        }
        
        agent.SetDestination(transform.position);
        // 도착 후 대기
        float timer = waitLastPosition;
        while (timer > 0f)
        {
            // 플레이어가 다시 드러나면 추격 복귀
            if (!isPlayerInHide)
            {
                ChangeState(State.Chase);
                yield break;
            }
            timer -= Time.deltaTime;
            yield return null;
        }

        // 대기 끝 → 인식 풀고 배회 상태로 전환
        ChangeState(State.Walk);
    }

    // 감지 여부
    public void OnDetected(bool isDetected)
    {
        isPlayerInHide = isDetected;
        if (isDetected)
        {
            lastPlayerPosition = player.position;
            agent.SetDestination(lastPlayerPosition);
            chaseRange = 0f;
            // 추격 중이면 Wait 상태로 변경
            if (currState == State.Chase)
            {
                // agent.SetDestination(lastPlayerPosition);
                ChangeState(State.Wait);
            }
        }
        else
        {
            chaseRange = saveChaseRange;
        }
    }
    
    protected virtual void Update()
    {
        if (player == null) return;
        
        // 애니메이션 Speed값
        curSpeed = agent.velocity.magnitude;
        animator.SetFloat("Speed", curSpeed);
    }
    
    // 플레이어 추격 판단
    protected virtual bool CanChasePlayer() => true;
    
    // 추격할 떄
    protected virtual void OnChasePlayer() { }
    
    // 공격할 떄
    // 애니메이션 처리 만들어야함
    protected virtual void OnAttack() { }
}
