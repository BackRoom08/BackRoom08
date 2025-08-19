using UnityEngine;

public class Teleport : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint; // 시작 위치

    private void OnTriggerEnter(Collider other)
    {
        // 플레이어 태그 확인
        if (other.CompareTag("Player"))
        {
            // 플레이어의 CharacterController 컴포넌트 가져오기
            CharacterController cc = other.GetComponent<CharacterController>();
            if (cc != null)
            {
                // CharacterController를 잠시 비활성화하고 위치 이동 (안 하면 충돌 문제 생길 수 있음)
                cc.enabled = false;
                other.transform.position = respawnPoint.position;
                cc.enabled = true;
            }
            else
            {
                other.transform.position = respawnPoint.position;
            }

        }
    }
}
