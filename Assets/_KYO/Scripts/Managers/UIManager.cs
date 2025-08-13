using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [SerializeField] GameSetting settings;  // 설정 데이터
    
    
    [SerializeField] GameObject settingsPanel;  // 설정창
    [SerializeField] GameObject loadingPanel;   // 로딩창
    [SerializeField] Slider loadingBar;  // 로딩창의 로딩바
    
    [SerializeField] SettingUI settingUI;
    
    public bool IsPaused { get; private set; }  // 설정창 On/Off

    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        settings?.Load();
        ShowSettings(false, force:false);
        ShowLoading(false);
    }
    
    // 설정창 토글방식으로 띄우기 On/Off
    // 설정창 호출
    public void ToggleSettings()
    {
        ShowSettings(!settingsPanel.activeSelf);
    }

    // 설정창 출현
    public void ShowSettings(bool show, bool force = false)
    {
        if (settingsPanel == null) return;

        if (show) settingUI?.RefreshFromData();
        settingsPanel.SetActive(show);

        if (show)
        {
            PauseGame();
            // UI 포커스
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
        else
        {
            if (!force) ResumeGame();
        }
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
    
    // 게임 재진행
    public void ResumeGame()
    {
        if (!IsPaused) return;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = true;
    }
    
    // 플레이어만 멈추는 로직 
    
    // 로딩창 출연
    public void ShowLoading(bool show)
    {
        if (loadingPanel) loadingPanel.SetActive(show);
        if (show) { PauseGame(); }           // 로딩 중엔 게임 입력/동작 멈춤
        else      { ResumeGame(); }
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
}