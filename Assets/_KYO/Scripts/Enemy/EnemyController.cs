using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    [SerializeField] [Tooltip("추격할 플레이어")] protected Transform player;
    [SerializeField] [Tooltip("플레이어를 추격 시작하는 거리")] protected float chaseRange = 10f;
    [SerializeField] [Tooltip("걷기 속도")] protected float walkSpeed = 2f;
    [SerializeField] [Tooltip("뛰기(추격) 속도")] protected float runSpeed = 5f;
    [SerializeField] [Tooltip("멈춤 상태 지속 시간")] private float idleDuration = 2f;
    [SerializeField] [Tooltip("걷기 상태 지속 시간")] private float walkDuration = 4f;
    [SerializeField] [Tooltip("랜덤으로 이동할 최소 거리")] private float minMoveRange = 10f;
    [SerializeField] [Tooltip("랜덤으로 이동할 최대 거리")] private float maxMoveRange = 50f;

    protected NavMeshAgent agent;      // 이동을 담당하는 NavMeshAgent 컴포넌트
    protected Animator animator;        // 애니메이션 제어용 Animator 컴포넌트
    
    private float stateTimer = 0f;   // 상태 전환을 위한 타이머
    private int state = 0;           // 현재 상태 (0: Idle, 1: Walk)


    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        stateTimer = Random.Range(0f, walkDuration);
        agent.isStopped = true;
    }

    protected virtual void Update()
    {
        Move();
    }
    
    // 이동
    protected virtual void Move()
    {
        // 적과 플레이어 위치 거리
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        
        // 애니메이션 Speed값
        float speed = agent.velocity.magnitude;
        animator.SetFloat("Speed", speed);
        
        // === 3. 뛰기(추격) ===
        if (distToPlayer < chaseRange)
        {
            agent.isStopped = false;
            agent.speed = runSpeed;
            agent.SetDestination(player.position);
        }
        else
        {
            // 일반상태(멈춤/걷기 랜덤) =
            stateTimer -= Time.deltaTime;

            if (state == 0) // 멈춤
            {
                agent.isStopped = true;
                if (stateTimer <= 0f)   // 업데이트에서 값이 계속 바뀔수 있으므로 타이머 설정
                {
                    state = 1;
                    stateTimer = walkDuration;
                    // 이동할 랜덤 위치 지정
                    float distance = Random.Range(minMoveRange, maxMoveRange);
                    Vector3 randomDir = Random.insideUnitSphere * distance;
                    randomDir += transform.position;
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(randomDir, out hit, 10f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                }
            }
            else if (state == 1) // 걷기
            {
                agent.isStopped = false;
                agent.speed = walkSpeed;
                if (stateTimer <= 0f || agent.remainingDistance < 0.5f)
                {
                    state = 0;
                    stateTimer = idleDuration;
                }
            }
        }
    }
    
    void Attack()
    {
        animator.SetTrigger("Attack");
    }
}
