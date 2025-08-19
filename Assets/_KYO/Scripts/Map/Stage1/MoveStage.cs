using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MoveStage : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {//플레이어면 아래 코루틴 실행
            StartCoroutine(HandleSceneTransition());
        }

    }
    IEnumerator HandleSceneTransition()
    {
        // UI Canvas에서 PlayerInventory 찾기
        PlayerInventory inventory = GameObject.Find("Canvas")
            ?.transform.Find("P_PlayerUI/P_InventoryController")
            ?.GetComponent<PlayerInventory>();
        //캔버스를 찾고 경로를 따라서 PlayerInventory 컴포넌트를 가져옴
        if (inventory == null || inventory.items == null)
        {
            yield break; //템창이나 아이템리스트가 null이면 종료
        }

        // 인벤토리 저장
        GameManager.Instance.SaveInventory(inventory.items.ToList()); //게임매니저 싱글톤을 통해 저장
        GameManager.Instance.SavePlayerDataToFile();                //플레이어 데이터를 파일로 저장

        yield return new WaitForSeconds(1f); // 저장 1초 대기

        // 씬 전환
        SceneLoader.Instance.LoadSceneAdditive("Stage2", true);
    }
}
