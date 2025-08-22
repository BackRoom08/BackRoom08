using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] GameSetting settings;  // 설정 데이터
    public GameSetting Settings { get { return settings; } }

    [Header("Panels")]
    [SerializeField] GameObject settingsPanel;  // 설정창(DDOL 권장)
    [SerializeField] GameObject loadingPanel;   // 로딩창(DDOL 권장)
    [SerializeField] GameObject deadPanel;      // 죽음창
    [SerializeField] Image deadBgImage;         // 사망 UI 배경 (페이드 효과용)
    [SerializeField] Slider loadingBar;         // 로딩창의 로딩바

    [Header("Settings UI")]
    [SerializeField] SettingUI settingUI;

    [Header("Buttons")]
    [SerializeField] Button closeBtn;           // 닫기 버튼
    [SerializeField] Button quiteBtn;           // 게임종료 버튼

    public bool IsPaused { get; private set; }  // 설정창 On/Off

    // StartScene에서 설정창을 띄울 때 임시로 숨긴 캔버스들 기록
    readonly List<Canvas> hiddenStartCanvases = new List<Canvas>();

    public AudioMixerGroup bgmGroup;
    public AudioMixerGroup sfxGroup;
    
    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        settings?.Load();
        ShowSettings(false, force: false);
        ShowLoading(false);

        // 씬이 바뀌면 기록 초기화(안전장치)
        SceneManager.activeSceneChanged += (_, __) => hiddenStartCanvases.Clear();
    }

    void Start()
    {
        if (closeBtn) closeBtn.onClick.AddListener(OnClickOpenSettings);
        if (quiteBtn) quiteBtn.onClick.AddListener(QuitGame);
    }

    // 설정창 토글
    public void ToggleSettings()
    {
        ShowSettings(!settingsPanel.activeSelf);
    }

    // 설정창 표시/숨김
    public void ShowSettings(bool show, bool force = false)
    {
        if (settingsPanel == null) return;

        if (show) settingUI?.RefreshFromData();
        settingsPanel.SetActive(show);

        var activeSceneName = SceneManager.GetActiveScene().name;
        bool isStartScene = activeSceneName == "StartScene";

        if (show)
        {
            // StartScene이라면, 그 씬에 소속된(DDOL 아님) 모든 Canvas를 잠시 숨김
            if (isStartScene) HideStartSceneCanvases();

            PauseGame();

            // UI 포커스 보장
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
        else
        {
            // 설정창 닫힐 때 StartScene의 캔버스들 복구
            if (isStartScene) RestoreStartSceneCanvases();

            if (!force) ResumeGame();
        }
    }

    // StartScene에 소속된 Canvas들을 찾아 숨김(설정창은 DDOL이므로 영향 없음)
    void HideStartSceneCanvases()
    {
        hiddenStartCanvases.Clear();

        var activeScene = SceneManager.GetActiveScene();

        // 비활성 포함해 씬 소속 Canvas 전부 탐색
        var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (var canvas in allCanvases)
        {
            // 씬 소속 + 루트가 아니어도 상관 없음(모두 처리)
            if (!canvas || !canvas.gameObject || !canvas.gameObject.scene.IsValid()) continue;
            if (canvas.gameObject.scene != activeScene) continue;

            // 이미 비활성인 것은 제외
            if (!canvas.gameObject.activeSelf) continue;

            // 숨기고 목록에 기록
            canvas.gameObject.SetActive(false);
            hiddenStartCanvases.Add(canvas);
        }
    }

    // 방금 숨겼던 StartScene 캔버스들 복구
    void RestoreStartSceneCanvases()
    {
        foreach (var c in hiddenStartCanvases)
        {
            if (c && c.gameObject) c.gameObject.SetActive(true);
        }
        hiddenStartCanvases.Clear();
    }

    // 게임 멈춤
    public void PauseGame()
    {
        if (IsPaused) return;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        IsPaused = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 게임 재개
    public void ResumeGame()
    {
        if (!IsPaused) return;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;

        var activeScene = SceneManager.GetActiveScene().name;

        // 나중에 엔딩씬 등 추가되면 조건에 포함
        if (activeScene != "StartScene")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // 로딩창 표시
    public void ShowLoading(bool show)
    {
        if (loadingPanel) loadingPanel.SetActive(show);
        if (show)
        {
            PauseGame();          // 로딩 중 입력/동작 멈춤
        }
        else
        {
            ResumeGame();
        }
    }

    public void SetLoadingProgress(float p01)
    {
        if (loadingBar) loadingBar.value = Mathf.Clamp01(p01);
    }

    // 시작씬에서의 버튼 (UI 버튼)
    public void OnClickOpenSettings() => ToggleSettings();
    public void OnClickCloseSettings() => ShowSettings(false);
    public void OnClickApplySettings()
    {
        settings?.Save();
        // 필요한 값들 매핑
    }

    // 게임 종료(에디터/빌드 분기)
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>사망 UI를 1초에 걸쳐 서서히 표시</summary>
    public void ShowDeathUI()
    {
        if (deadPanel == null || deadBgImage == null)
        {
            Debug.LogError("Dead Panel 또는 Dead BG Image가 UIManager에 할당되지 않았습니다.");
            return;
        }
        PauseGame();
        StartCoroutine(FadeInDeadUI(1f));
    }

    public void CloseDeadUI()
    {
        if (deadPanel == null || deadBgImage == null)
        {
            //Debug.LogError("Dead Panel 또는 Dead BG Image가 UIManager에 할당되지 않았습니다.");
            return;
        }
        ResumeGame();
        deadPanel.SetActive(false);
    }

    IEnumerator FadeInDeadUI(float duration)
    {
        deadPanel.SetActive(true);

        Color color = deadBgImage.color;
        color.a = 0f;
        deadBgImage.color = color;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            color.a = Mathf.Clamp01(elapsedTime / duration);
            deadBgImage.color = color;
            yield return null;
        }

        color.a = 1f;
        deadBgImage.color = color;
    }
    
    public void ChangeSound(SoundState soundState, AudioSource audioSource, float soundVolume)
    {
        float masterVo = Settings.masterVolume;
        float bgmVo = Settings.bgmVolume * masterVo;
        float sfxVo = Settings.sfxVolume * masterVo;
        if(soundState == SoundState.SFX)
            audioSource.volume = soundVolume * sfxVo;
        else if(soundState == SoundState.BGM)
            audioSource.volume = soundVolume * bgmVo;
    }
}
