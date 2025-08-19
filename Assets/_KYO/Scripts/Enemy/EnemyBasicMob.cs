using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 기본몹
public class EnemyBasicMob : EnemyController
{
    private bool isChasing; // 추격
    
    protected override void OnChasePlayer()
    {
        if (!isChasing)
        {
            // 예시: 사운드 재생 (한 번만)
            // AudioManager.Play("EnemyChase");
            isChasing = true;
        }
    }

    protected override IEnumerator ChaseRoutine()
    {
        isChasing = false; // 추격 시작마다 리셋
        yield return base.ChaseRoutine();
    }

    protected virtual void OnEnable()
    {
        // PlayerMove 컴포넌트를 가진 오브젝트를 찾아 플레이어로 설정
        PlayerMove playerObject = FindObjectOfType<PlayerMove>();
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }
    }
