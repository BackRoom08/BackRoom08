using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEyeLightMob : EnemyController
{
    [SerializeField] private float chaseDistance = 150f; // 추격 시작 거리
    [SerializeField] private float lostDistance = 151f;  // 플레이어를 놓치는 거리
   
    private bool isChasing = false; // 현재 추격 중인지 여부
    private EnemyPlayerAttack enemyAttack; // 공격 스크립트 참조

    private bool isStunned = false;         // 현재 기절 상태여부
    private bool canBeStunned = true;       // 기절 가능한 상태여부
    private float stunDuration = 2f;        // 기절 지속 시간
    private float stunCooldown = 5f;        // 기절 후 쿨타임


    // 컴포넌트를 가져오기 위해 Awake를 재정의.
    protected override void Awake()
    {
        base.Awake(); // 부모 클래스의 Awake를 호출하여 NavMeshAgent 등을 설정합니다.
        animator = GetComponentInChildren<Animator>(); // 애니메이터를 찾습니다.
        agent.acceleration = 30f; // NavMeshAgent의 가속도를 30으로 설정

        //GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        //if (playerObj != null)
        //    player = playerObj.transform;

        // 공격 스크립트 컴포넌트를 가져옴
        enemyAttack = GetComponent<EnemyPlayerAttack>();
    }


    protected override void Update()
    {
        if (player == null || isStunned) return;

        base.Update();
    }
    // 플레이어와 충돌 시 공격 스크립트
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            enemyAttack.InitiateAttack(other.gameObject);
        }
    }

    public void OnFlashHit()
    { 
        if (!canBeStunned || isStunned)
        {
            return;
        }
        StartCoroutine(StunRoutine());
    }


    private IEnumerator StunRoutine()
    {
        isStunned = true; //기절상태가 되고
        canBeStunned = false; //기절면역상태가 됨

        agent.isStopped = true; //멈춤
       // animator.SetFloat("Speed", 0);
       // animator.SetTrigger("Stun");

        yield return new WaitForSeconds(stunDuration);

        isStunned = false; //기절이 풀리고
        agent.isStopped = false; //움직일수있게됨

        // 기절이 끝나면 상태 복귀: 추격 중이었다면 다시 추격
        if (Vector3.Distance(transform.position, player.position) < chaseRange && CanChasePlayer() && !isPlayerInHide)
        {
            ChangeState(State.Chase);
        }
        else
        {
            ChangeState(State.Idle);
        }

        // 쿨타임 시작
        yield return new WaitForSeconds(stunCooldown);
        canBeStunned = true; //일정시간이 지나면 다시 기절이 걸림
    }

    // 추격 중에 호출되는 함수 (부모에서 호출됨)
    protected override void OnChasePlayer()
    {
        if (isStunned)
        {
            agent.isStopped = true;
        }
    }

    // 기절 중에는 추격 불가
    protected override bool CanChasePlayer()
    {
        return !isStunned;
    }
}
