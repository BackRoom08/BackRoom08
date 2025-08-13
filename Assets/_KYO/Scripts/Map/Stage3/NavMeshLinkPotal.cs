using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class NavMeshLinkPotal : MonoBehaviour
{
    public Transform targetPortal; // 반대편 포탈 위치
    public float cooldownTime = 1f; // 텔레포트 후 쿨타임 (초)

    private void OnTriggerEnter(Collider other)
    {
        // NavMeshAgent가 있는 AI만 포탈 사용
        NavMeshAgent agent = other.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            // 이미 쿨타임 중인 경우 스킵
            if (agent.gameObject.TryGetComponent<PortalCooldown>(out PortalCooldown cooldown))
            {
                if (cooldown.isOnCooldown) return; // 쿨타임 중이면 나감
            }
            else
            {
                cooldown = agent.gameObject.AddComponent<PortalCooldown>();
            }

            // 쿨타임 시작
            cooldown.StartCooldown(cooldownTime);

            // NavMeshAgent의 Warp 사용 → 순간이동 (경로 문제 방지)
            agent.Warp(targetPortal.position);
        }
    }
}

// 쿨타임 관리용 컴포넌트
public class PortalCooldown : MonoBehaviour
{
    public bool isOnCooldown { get; private set; }

    public void StartCooldown(float time)
    {
        if (!isOnCooldown)
        {
            StartCoroutine(CooldownRoutine(time));
        }
    }

    private IEnumerator CooldownRoutine(float time)
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(time);
        isOnCooldown = false;
    }
}