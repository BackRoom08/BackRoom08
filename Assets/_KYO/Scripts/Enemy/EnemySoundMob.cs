using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySound : EnemyController
{
    [SerializeField] private float soundRange = 15f;
    private Vector3 lastHeardSound;
    private bool heardSound = false;
    private bool isPlayerCrouch = false;

    // 플레이어가 앉거나 서있을 때 호출
    public void OnPlayerCrouch(bool isCrouch)
    {
        isPlayerCrouch = isCrouch;
    }

    // 소리 발생 시 호출
    public void HearSound(Vector3 soundPos)
    {
        lastHeardSound = soundPos;
        heardSound = true;
    }

    protected override bool CanChasePlayer()
    {
        // 앉아서 이동하면 못 찾음
        if (isPlayerCrouch)
            return false;
        return heardSound;
    }

    protected override IEnumerator WalkRoutine()
    {
        // 배회 기본 구현
        yield return base.WalkRoutine();
        heardSound = false; // 배회 종료 시 소리 초기화
    }

    protected override IEnumerator ChaseRoutine()
    {
        agent.speed = runSpeed;
        agent.angularSpeed = 120f;  // 커브 느리게
        agent.acceleration = 20f;

        while (heardSound)
        {
            // 마지막 들은 소리 위치로 추격
            agent.SetDestination(lastHeardSound);

            // 만약 도착했거나, 일정 반경 안에 플레이어가 있으면 공격
            if (Vector3.Distance(transform.position, lastHeardSound) < 1f)
            {
                heardSound = false; // 소리 위치에 도달하면 추격 중단
                ChangeState(State.Idle);
                yield break;
            }
            yield return null;
        }

        // 추격 조건 끝나면 원래 상태로
        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
        ChangeState(State.Idle);
    }
}
