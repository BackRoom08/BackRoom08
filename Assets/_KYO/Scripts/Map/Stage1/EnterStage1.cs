using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnterStage1 : MonoBehaviour, IInteractable
{
    [Tooltip("플레이어")]
    public GameObject player;
    [Tooltip("플레이어가 스폰될 위치")]
    public Transform playerSpawnPoint;
    [Tooltip("튜토리얼 방 자체를 비활성화")]
    public GameObject tutorial;

    private void Start()
    {
        if (!MapManager.IsRegame())
        {
            //print(MapManager.IsRegame());
            gameObject.SetActive(false);
        }
    }
    public void Interact()
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        player.transform.position = playerSpawnPoint.position;

        if (controller != null)
        {
            controller.enabled = true;
        }

        tutorial.SetActive(false);
    }
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
                other.transform.position = playerSpawnPoint.position;
                cc.enabled = true;
            }
            else
            {
                other.transform.position = playerSpawnPoint.position;
            }

            tutorial.SetActive(false);

        }
    }
}