using System.Collections;
using UnityEngine;

public class MoveStage : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 플레이어가 트리거에 닿으면 씬 전환 코루틴 실행
            StartCoroutine(HandleSceneTransition());
        }
    }

    IEnumerator HandleSceneTransition()
    {
        // 씬 전환 전 약간의 대기 (선택사항)
        yield return new WaitForSeconds(1f);

        // Stage2 씬 로드
        SceneLoader.Instance.LoadSceneAdditive("Stage2", true);
    }
}
