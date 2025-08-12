using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySmilerMob : EnemyController
{
    [SerializeField] private float chaseDistance = 7f; // 추격 시작 거리
    [SerializeField] private float lostDistance = 15f;  // 플레이어를 놓치는 거리

    private bool isChasing = false; // 현재 추격 중인지 여부

    // 컴포넌트를 가져오기 위해 Awake를 재정의합니다.
    protected override void Awake()
    {
        base.Awake(); // 부모 클래스의 Awake를 호출하여 NavMeshAgent 등을 설정합니다.
        animator = GetComponentInChildren<Animator>(); // 애니메이터를 찾습니다.
    }

    protected override bool CanChasePlayer()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        
        if (!isChasing) // 추격중이 아닐때
        {
            if (distance <= chaseDistance) // 플레이어랑 거리가 되면
            {
                print($"{distance} 추격 시작");
                isChasing = true;
            }
        }
        else // If currently chasing
        {
            if (distance > lostDistance) // 추격이 끊기는 거리
            {
                print($"{distance} 추격 종료");
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
        // animator.SetFloat("Speed", 0); // 애니메이터를 사용하지 않으므로 이 줄을 실행하지 않습니다.
        yield return new WaitForSeconds(idleDuration);
        ChangeState(State.Walk);
    }

    protected override void Update()
    {
        // 상속한 Update()는 애니메이터 때문에 실행하지 않게하기
        // Quad가 항상 플레이어를 바라보도록 설정.
        if (player != null)
        {
            // 플레이어를 바라보게 하되, Quad가 위아래로 기울어지지 않도록 y축은 고정
            Vector3 targetPosition = player.position;
            targetPosition.y = transform.position.y;
            transform.LookAt(targetPosition);
        }
    }
}
