using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyEyeLightMob : EnemyController
{
    [SerializeField] private float chaseDistance = 150f; // 추격 시작 거리
    [SerializeField] private float lostDistance = 151f;  // 플레이어를 놓치는 거리
   
    [SerializeField] private GameObject hitBoxObject; // 히트박스 오브젝트
    [SerializeField] private float attackDelay = 0.5f; // 공격 타이밍
    [SerializeField] private float attackCooldown = 1.5f;

    [Tooltip("공격 애니메이션 재생 후 게임오버까지 대기하는 시간")]
    public float attackAnimationDuration = 2f;
    [Tooltip("실행할 공격 애니메이션의 트리거 이름")]
    public string attackTriggerName = "Attack";

    private bool isChasing = false; // 현재 추격 중인지 여부
    private EnemyPlayerAttack enemyAttack; // 공격 스크립트 참조

    private bool isStunned = false;         // 현재 기절 상태여부
    private bool canBeStunned = true;       // 기절 가능한 상태여부
    private float stunDuration = 2f;        // 기절 지속 시간
    private float stunCooldown = 5f;        // 기절 후 쿨타임
    private bool isAttacking = false;
                
    private MonoBehaviour mainAiScript;     // 적의 주 AI 스크립트

    // 컴포넌트를 가져오기 위해 Awake를 재정의.
    protected override void Awake()
    {
        base.Awake(); // 부모 클래스의 Awake를 호출하여 NavMeshAgent 등을 설정합니다.   
        animator = GetComponentInChildren<Animator>();
        agent.acceleration = 30f; // NavMeshAgent의 가속도를 30으로 설정
        // 공격 스크립트 컴포넌트를 가져옴
        enemyAttack = GetComponent<EnemyPlayerAttack>();

        if (hitBoxObject != null)
        {
            hitBoxObject.SetActive(false); // 시작 시 비활성화
        }

        // 이 스크립트를 제외한 다른 MonoBehaviour를 찾아서 AI 스크립트로 간주
        foreach (var script in GetComponents<MonoBehaviour>())
        {
            if (script != this)
            {
                mainAiScript = script;
                break;
            }
        }
    }

    protected override void Update()
    {
        if (player == null || isStunned) return;
        base.Update();
        if (!isAttacking && Vector3.Distance(transform.position, player.position) < 2f)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        agent.isStopped = true;

        animator.SetTrigger("Attack");
        yield return new WaitForSeconds(attackDelay);

        EnableHitBox();
        yield return new WaitForSeconds(0.3f);
        DisableHitBox();

        yield return new WaitForSeconds(attackCooldown);

        agent.enabled = true;
        yield return null; // 한 프레임 대기
        agent.isStopped = false;

        isAttacking = false;
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
        animator.SetFloat("Speed", 0);
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

   
    //히트박스 활성화
    private void EnableHitBox()
    {
        if (hitBoxObject != null)
            hitBoxObject.SetActive(true);
    }

    private void DisableHitBox()
    {
        if (hitBoxObject != null)
            hitBoxObject.SetActive(false);
    }

    public void InitiateAttack(GameObject playerObject)
    {
        // AI 스크립트가 활성화 상태일 때만 실행
        if (mainAiScript != null && mainAiScript.enabled)
        {
            StartCoroutine(AttackCoroutine(playerObject));
        }
    }
    private IEnumerator AttackCoroutine(GameObject playerObject)
    {
        MapManager mapManager = FindObjectOfType<MapManager>();
        if (mapManager == null)
        {
            Debug.LogError("Scene에 MapManager가 없습니다!");
            yield break; // MapManager가 없으면 코루틴 중단
        }

        // 페이드 아웃
        mapManager.FadeOut(0.1f);
        yield return new WaitForSeconds(0.1f);

        // 플레이어 조작 비활성화
        playerObject.GetComponent<PlayerMove>().enabled = false;
        playerObject.GetComponent<ItemPickUp>().enabled = false;

        var cameraScript = playerObject.GetComponentInChildren<NewBehaviourScript>();
        if (cameraScript != null) cameraScript.enabled = false;

        // AI 비활성화
        agent.enabled = false;
        if (mainAiScript != null) mainAiScript.enabled = false;

        // 데스룸으로 순간이동 , 페이드 인
        transform.position = mapManager.enemyDeadRoomPoints.position;
        playerObject.transform.position = mapManager.playerDeadRoomPoint.position;

        transform.LookAt(playerObject.transform);
        // 플레이어가 적을 수평으로만 바라보도록 수정
        Vector3 directionToEnemy = transform.position - playerObject.transform.position;
        directionToEnemy.y = 0; // Y축 값을 0으로 만들어 수평 방향으로 고정
        if (directionToEnemy != Vector3.zero) // 0 벡터가 아닐 때만 회전 적용 (오류 방지)
        {
            playerObject.transform.rotation = Quaternion.LookRotation(directionToEnemy);
        }

        mapManager.FadeIn(1.5f);

        // 애니메이션 실행
        yield return new WaitForSeconds(2f);
        animator.SetTrigger(attackTriggerName);

        //  애니메이션 시간만큼 대기
        yield return new WaitForSeconds(attackAnimationDuration);

        UIManager.Instance.ShowDeathUI();
        Debug.Log("게임 오버 부분 붙여서 넣기 !");
    }
}
