using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private string bootstrapSceneName = "SampleScene"; // 전역 시스템 담긴 씬(선택)
    [SerializeField] private float minLoadingTime = 2f; // 로딩화면 최소 노출

    [SerializeField] private bool autoLoadOnStart = true;
    [SerializeField] private string firstSceneToLoad = "StartScene";

    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (autoLoadOnStart && !string.IsNullOrEmpty(firstSceneToLoad))
            StartCoroutine(AutoKickoff());
    }


    IEnumerator AutoKickoff()
    {
        yield return null;
        //LoadSceneAdditive(firstSceneToLoad);
        LoadSceneAdditive(firstSceneToLoad, false);
    }

    // 씬 전환 사용
    // 아래 그대로 호출 씬이름만 넣어서
    // SceneLoader.Instance.LoadSceneAdditive("본인 씬", true);
    // 예시
    // SceneLoader.Instance.LoadSceneAdditive("Stage1", true);

    // 씬 전환 로더
    public void LoadSceneAdditive(string sceneName, bool showLoading)
    {
        StartCoroutine(CoLoad(sceneName, showLoading));
        MapManager.Instance.NewGame();
    }

    IEnumerator CoLoad(string sceneName, bool showLoading)
    {
        if (showLoading && UIManager.Instance)
        {
            UIManager.Instance.ShowLoading(true);
            UIManager.Instance.SetLoadingProgress(0f);
        }
        //print("loading");

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
        //print("11111");
        // 최소 노출 시간 보정
        while (elapsed < minLoadingTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        //print("2222");
        // 활성화
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 활성 씬 지정
        var loaded = SceneManager.GetSceneByName(sceneName);
        SceneManager.SetActiveScene(loaded);
        //print("33333");
        // 이전 씬 언로드 (부트스트랩은 유지)
        for (int i = SceneManager.sceneCount - 1; i >= 0; --i)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != sceneName && s.isLoaded && s.name != bootstrapSceneName)
                yield return SceneManager.UnloadSceneAsync(s);
        }
        //print("444444");
        // 로딩 완료
        if (showLoading && UIManager.Instance)
        {
            UIManager.Instance.SetLoadingProgress(1f);
            UIManager.Instance.ShowLoading(false);
        }
        //print("loaded");

        // (옵션) 로딩 종료 후 커서/타임스케일은 UIManager가 관리
    }

    // 씬 다시 로드 (리게임)
    public void ReloadActiveScene(bool showLoading = true)
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        StartCoroutine(CoReloadActiveScene(activeSceneName, showLoading));
        MapManager.Instance.ReGame();
        UIManager.Instance.CloseDeadUI();
    }

    IEnumerator CoReloadActiveScene(string sceneName, bool showLoading)
    {
        if (showLoading && UIManager.Instance)
        {
            UIManager.Instance.ShowLoading(true);
            UIManager.Instance.SetLoadingProgress(0f);
        }

        // 1. 현재 활성 씬 언로드
        yield return SceneManager.UnloadSceneAsync(sceneName);

        // 2. 씬을 추가적으로 다시 로드
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        op.allowSceneActivation = false;

        float elapsed = 0f;
        while (op.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;
            UIManager.Instance.SetLoadingProgress(op.progress);
            yield return null;
        }

        while (elapsed < minLoadingTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 3. 다시 로드된 씬을 활성 씬으로 지정
        var loaded = SceneManager.GetSceneByName(sceneName);
        SceneManager.SetActiveScene(loaded);

        if (showLoading && UIManager.Instance)
        {
            UIManager.Instance.SetLoadingProgress(1f);
            UIManager.Instance.ShowLoading(false);
        }
    }
    private void Update()
    {
        // 테스트용 씬 이동 (Ctrl + F1, F2, ...)
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                LoadSceneAdditive("Stage1", true);
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                LoadSceneAdditive("Stage2", true);
            }
            if (Input.GetKeyDown(KeyCode.F3))
            {
                LoadSceneAdditive("Stage3", true);
            }
            if (Input.GetKeyDown(KeyCode.F4))
            {
                LoadSceneAdditive("Stage4", true);
            }
        }
    }
}