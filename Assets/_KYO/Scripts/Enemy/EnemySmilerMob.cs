using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class EnemySmilerMob : EnemyController
{
    [SerializeField] private float chaseDistance = 7f; // 추격 시작 거리
    [SerializeField] private float lostDistance = 15f;  // 플레이어를 놓치는 거리
    private bool playerisdead = false;

    private bool isChasing = false; // 현재 추격 중인지 여부
    private EnemyPlayerAttack enemyAttack; // 공격 스크립트 참조

    // 컴포넌트를 가져오기 위해 Awake를 재정의.
    protected override void Awake()
    {
        base.Awake(); // 부모 클래스의 Awake를 호출하여 NavMeshAgent 등을 설정합니다.
        animator = GetComponentInChildren<Animator>(); // 애니메이터를 찾습니다.
        agent.acceleration = 30f; // NavMeshAgent의 가속도를 30으로 설정

        // 공격 스크립트 컴포넌트를 가져옴
        enemyAttack = GetComponent<EnemyPlayerAttack>();
    }

    protected virtual void OnEnable()
    {
        // PlayerMove 컴포넌트를 가진 오브젝트를 찾아 플레이어로 설정
        PlayerMove playerObject = FindObjectOfType<PlayerMove>();
        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        // GameObject가 다시 활성화될 때 에이전트가 정지되지 않도록
        if (agent != null)
        {
            agent.isStopped = false;
        }

        // 적이 이동 상태에 있었다면 해당 루틴을 다시 시작.
        
        switch (currState)
        {
            case State.Walk:
                ChangeState(State.Walk); // WalkRoutine 다시 시작
                break;
            case State.Chase:
                ChangeState(State.Chase); // ChaseRoutine 다시 시작
                break;
            case State.Wait:
                ChangeState(State.Wait); // WaitRoutine 다시 시작
                break;
            // Idle 상태의 경우 이미 정지되어 있으므로 별도의 조치 X.
        }
    }
    

    protected override bool CanChasePlayer()
    {
        float distance = Vector3.Distance(transform.position, player.position);


        if (!isChasing) // 추격중이 아닐때
        {
            if (distance <= chaseDistance) // 플레이어랑 거리가 되면 추격
            {
                //print($"{distance} 추격 시작");
                isChasing = true;
            }
        }
        else // If currently chasing
        {
            if (distance > lostDistance) // 추격이 끊기는 거리
            {
                //print($"{distance} 추격 종료");
                isChasing = false;
            }
        }

        // 애니메이터 파라미터 업데이트
        if (animator != null)
        {
            animator.SetBool("isChasing", isChasing);
        }


        return isChasing;
    }

    // "Parameter 'Speed' does not exist" 오류를 막기 위해 재정의
    protected override IEnumerator IdleRoutine()
    {
        agent.isStopped = true;
        if (animator != null)
        {
            animator.SetBool("isChasing", false); // isChasing을 false로 
            // animator.SetFloat("Speed", 0); 
        }
        yield return new WaitForSeconds(idleDuration);
        ChangeState(State.Walk);
    }

    protected override void Update()
    {
        if (player == null) return;

        // 플레이어를 바라보게 하되, Quad가 위아래로 기울어지지 않도록 y축은 고정
        Vector3 targetPosition = player.position;
        targetPosition.y = transform.position.y;
        transform.LookAt(targetPosition);

        // 플레이어와의 거리에 따라 속도 조절
        if (currState == State.Chase) // 추격 상태일 때만 속도 조절
        {
            float distance = Vector3.Distance(transform.position, player.position);
            float targetSpeed;

            if (distance >= 28f)
            {
                targetSpeed = 8f; // 장거리 최대 속도
            }
            else // distance < 30f
            {
                targetSpeed = 3.5f; // 근거리 최소 속도
            }

            // 속도를 부드럽게 보간
            agent.speed = Mathf.Lerp(agent.speed, targetSpeed, 5f * Time.deltaTime);
        }
    }

    // 플레이어와 충돌 시 공격 스크립트
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !playerisdead )
        {
            playerisdead = true;
            enemyAttack.InitiateAttack(other.gameObject);
        }
    }
}
