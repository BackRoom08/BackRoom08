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
}