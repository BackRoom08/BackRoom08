using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBasic : EnemyController
{
    /*public float predictTime = 1.5f; // 예측할 시간(초)

    protected override void Move()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // 1. 추격 범위면 플레이어 "앞"을 예측해서 막는다
        if (distToPlayer < chaseRange)
        {
            print("gogo");
            agent.isStopped = false;
            agent.speed = runSpeed;

            // 플레이어 이동 방향 예측
            Rigidbody playerRb = player.GetComponent<Rigidbody>(); // 또는 플레이어 이동 스크립트에서 velocity 값
            Vector3 playerVelocity = Vector3.zero;
            if (playerRb != null) 
                playerVelocity = playerRb.velocity;

            // 예측 위치 계산: 현재 위치 + 방향*속도*예측시간
            Vector3 predictedPosition = player.position + playerVelocity * predictTime;

            // 예측 위치로 이동
            agent.SetDestination(predictedPosition);

            // 애니메이션 세팅
            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);
        }
        else
        {
            // 평상시엔 부모 이동 패턴 유지
            base.Move();
        }
    }*/
    
}
