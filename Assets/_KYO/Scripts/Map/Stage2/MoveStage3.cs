using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
public class MoveStage3 : MonoBehaviour
{
    private bool isTransitioning = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isTransitioning) return;

        if (other.CompareTag("Player"))
        {
            isTransitioning = true;
            StartCoroutine(HandleSceneTransition());
        }
    }

    IEnumerator HandleSceneTransition()
    {
        // UI Canvas에서 PlayerInventory 찾기
        PlayerInventory inventory = GameObject.Find("Canvas")
            ?.transform.Find("P_PlayerUI/P_InventoryController")
            ?.GetComponent<PlayerInventory>();

        if (inventory == null || inventory.items == null)
        {
            Debug.LogWarning("인벤토리 또는 아이템 리스트가 null입니다. 씬 전환 중단.");
            yield break;
        }

        // 유효한 아이템만 저장
        var validItems = inventory.items.Where(item => item != null).ToList();
        GameManager.Instance.SaveInventory(validItems);

        int selectedIndex = inventory.GetSelectedIndex();
        if (selectedIndex >= 0)
        {
            GameManager.Instance.PlayerData.selectedInventoryIndex = selectedIndex;
        }

        GameManager.Instance.SavePlayerDataToFile();

        yield return new WaitForSeconds(0.5f); // 저장 안정화 대기

        // Additive 방식으로 Stage3 씬 로드
        SceneLoader.Instance.LoadSceneAdditive("Stage3", true);

        // Stage3 씬이 완전히 로드될 때까지 대기
        yield return new WaitUntil(() => SceneManager.GetSceneByName("Stage3").isLoaded);

        // Stage3 씬을 활성화 (선택 사항)
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Stage3"));

        // 데이터 복원
        GameManager.Instance.ApplyPlayerDataToPlayer();

        Debug.Log("Stage3 씬 로드 및 인벤토리 복원 완료");
    }
}





