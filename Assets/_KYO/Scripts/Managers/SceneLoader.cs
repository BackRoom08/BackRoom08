using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private string bootstrapSceneName = "Bootstrap"; // 전역 시스템 담긴 씬(선택)
    [SerializeField] private float minLoadingTime = 0.5f; // 로딩화면 최소 노출

    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadSceneAdditive(string sceneName)
    {
        StartCoroutine(CoLoad(sceneName));
    }

    IEnumerator CoLoad(string sceneName)
    {
        UIManager.Instance.ShowLoading(true);
        UIManager.Instance.SetLoadingProgress(0f);

        // 다음 씬 로드
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        op.allowSceneActivation = false;

        float elapsed = 0f;
        while (op.progress < 0.9f) // 0.9 ~ ready
        {
            elapsed += Time.unscaledDeltaTime;
            UIManager.Instance.SetLoadingProgress(op.progress);
            yield return null;
        }

        // 최소 노출 시간 보정
        while (elapsed < minLoadingTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 활성화
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 활성 씬 지정
        var loaded = SceneManager.GetSceneByName(sceneName);
        SceneManager.SetActiveScene(loaded);

        // 이전 씬 언로드 (부트스트랩은 유지)
        for (int i = SceneManager.sceneCount - 1; i >= 0; --i)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != sceneName && s.isLoaded && s.name != bootstrapSceneName)
                yield return SceneManager.UnloadSceneAsync(s);
        }

        // 로딩 완료
        UIManager.Instance.SetLoadingProgress(1f);
        UIManager.Instance.ShowLoading(false);

        // (옵션) 로딩 종료 후 커서/타임스케일은 UIManager가 관리
    }
}