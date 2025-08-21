using System.Collections;
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
        // 씬 전환 전 약간의 대기 (선택사항)
        yield return new WaitForSeconds(0.5f);

        // Stage3 씬 로드
        SceneLoader.Instance.LoadSceneAdditive("Stage3", true);
        yield return new WaitUntil(() => SceneManager.GetSceneByName("Stage3").isLoaded);

        Debug.Log("Stage3 씬 로드 완료");
    }
}
