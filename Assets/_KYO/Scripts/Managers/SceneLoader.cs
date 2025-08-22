using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private string bootstrapSceneName = "SampleScene";
    [SerializeField] private float minLoadingTime = 2f;      // 로딩 최소 노출
    [SerializeField] private bool autoLoadOnStart = true;
    [SerializeField] private string firstSceneToLoad = "StartScene";

    [Header("Progress Tuning")]
    [SerializeField] [Range(0.1f, 0.9f)]
    private float asyncPortion = 0.30f;                      // 실제 비동기 로딩이 차지할 게이지 비율 (예: 30%)
    [SerializeField] private float smoothFillDuration = 2.5f;// 나머지 70%를 채우는 연출 시간(초)

    public Action<string> OnSceneLoaded;

    private bool isCheck = true;

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
        LoadSceneAdditive(firstSceneToLoad, false);
    }

    public void LoadSceneAdditive(string sceneName, bool showLoading)
    {
        isCheck = false;
        StartCoroutine(CoLoad(sceneName, showLoading));
        MapManager.NewGame();
    }

    IEnumerator CoLoad(string sceneName, bool showLoading)
    {
        Scene existingScene = SceneManager.GetSceneByName(sceneName);
        bool needToUnloadSameScene = existingScene.IsValid() && existingScene.isLoaded;

        if (showLoading && UIManager.Instance)
        {
            UIManager.Instance.ShowLoading(true);
            UIManager.Instance.SetLoadingProgress(0f);
        }

        float visual = 0f;                     // 실제로 보여주는 게이지 값(0~1)
        float elapsed = 0f;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        op.allowSceneActivation = false;

        // 1) 실제 비동기 로딩 구간: 막대 0% ~ asyncPortion(예: 30%)까지만 반영
        while (op.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;

            // op.progress는 0~0.9 범위 → 0~1로 정규화 후 asyncPortion 스케일
            float realNorm = Mathf.InverseLerp(0f, 0.9f, op.progress); // 0~1
            float targetVisual = realNorm * asyncPortion;              // 0~0.30

            // 살짝 매끄럽게(튀는 현상 방지)
            visual = Mathf.MoveTowards(visual, targetVisual, Time.unscaledDeltaTime * 1.0f);

            if (showLoading && UIManager.Instance)
                UIManager.Instance.SetLoadingProgress(visual);

            yield return null;
        }

        // 2) 최소 노출 시간 보정 + 나머지 70% 연출 채우기
        //    남은 최소 노출 시간과 연출 시간 중 더 큰 값을 사용(사용자 체감 품질 유지)
        float remainMin = Mathf.Max(0f, minLoadingTime - elapsed);
        float fakeFillTime = Mathf.Max(smoothFillDuration, remainMin);

        // 현재 visual은 대략 asyncPortion 부근. 여기서 1.0까지 천천히 채움.
        yield return SmoothFill(visual, 1f, fakeFillTime, p =>
        {
            if (showLoading && UIManager.Instance)
                UIManager.Instance.SetLoadingProgress(p);
        });

        // 3) 씬 활성화
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 4) 활성 씬 설정
        var loaded = SceneManager.GetSceneByName(sceneName);
        SceneManager.SetActiveScene(loaded);

        // 5) 이전 씬 언로드(부트스트랩 제외)
        for (int i = SceneManager.sceneCount - 1; i >= 0; --i)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != sceneName && s.isLoaded && s.name != bootstrapSceneName)
                yield return SceneManager.UnloadSceneAsync(s);

            // 겹치는 이름의 씬도 추가로 제거
            if (needToUnloadSameScene && s.name == sceneName && s != loaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        if (showLoading && UIManager.Instance)
        {
            UIManager.Instance.SetLoadingProgress(1f);
            UIManager.Instance.ShowLoading(false);
        }

        OnSceneLoaded?.Invoke(sceneName);
        isCheck = true;
    }

    public void ReloadActiveScene(bool showLoading = true)
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        StartCoroutine(CoReloadActiveScene(activeSceneName, showLoading));
        MapManager.ReGame();
        UIManager.Instance.CloseDeadUI();
    }

    IEnumerator CoReloadActiveScene(string sceneName, bool showLoading)
    {
        if (showLoading && UIManager.Instance)
        {
            UIManager.Instance.ShowLoading(true);
            UIManager.Instance.SetLoadingProgress(0f);
        }

        float visual = 0f;
        float elapsed = 0f;

        // 1) 기존 활성 씬 언로드
        yield return SceneManager.UnloadSceneAsync(sceneName);

        // 2) 다시 로드
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;

            float realNorm = Mathf.InverseLerp(0f, 0.9f, op.progress);
            float targetVisual = realNorm * asyncPortion;

            visual = Mathf.MoveTowards(visual, targetVisual, Time.unscaledDeltaTime * 1.0f);

            if (showLoading && UIManager.Instance)
                UIManager.Instance.SetLoadingProgress(visual);

            yield return null;
        }

        float remainMin = Mathf.Max(0f, minLoadingTime - elapsed);
        float fakeFillTime = Mathf.Max(smoothFillDuration, remainMin);

        yield return SmoothFill(visual, 1f, fakeFillTime, p =>
        {
            if (showLoading && UIManager.Instance)
                UIManager.Instance.SetLoadingProgress(p);
        });

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        var loaded = SceneManager.GetSceneByName(sceneName);
        SceneManager.SetActiveScene(loaded);

        if (showLoading && UIManager.Instance)
        {
            UIManager.Instance.SetLoadingProgress(1f);
            UIManager.Instance.ShowLoading(false);
        }

        OnSceneLoaded?.Invoke(sceneName);
    }

    // unscaledDeltaTime 기준으로 from → to를 duration 동안 선형 증가
    IEnumerator SmoothFill(float from, float to, float duration, Action<float> onUpdate)
    {
        float t = 0f;
        float start = Mathf.Clamp01(from);
        float end = Mathf.Clamp01(to);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
            float v = Mathf.Lerp(start, end, a);
            onUpdate?.Invoke(v);
            yield return null;
        }
        onUpdate?.Invoke(end);
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.LeftControl) && isCheck || Input.GetKey(KeyCode.RightControl) && isCheck)
        {
            if (Input.GetKeyDown(KeyCode.F1)) LoadSceneAdditive("Stage1", true);
            if (Input.GetKeyDown(KeyCode.F2)) LoadSceneAdditive("Stage2", true);
            if (Input.GetKeyDown(KeyCode.F3)) LoadSceneAdditive("Stage3", true);
            if (Input.GetKeyDown(KeyCode.F4)) LoadSceneAdditive("Stage4", true);
            if (Input.GetKeyDown(KeyCode.F5)) LoadSceneAdditive("EndingScene", true);
        }
    }
}
