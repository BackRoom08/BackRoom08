using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [SerializeField] GameSetting settings;  // 설정 데이터
    public GameSetting Settings { get; }
    
    [SerializeField] GameObject settingsPanel;  // 설정창
    [SerializeField] GameObject loadingPanel;   // 로딩창
    [SerializeField] GameObject deadPanel;   // 죽음창
    [SerializeField] Image deadBgImage; // 사망 UI 배경 (페이드 효과용)
    [SerializeField] Slider loadingBar;  // 로딩창의 로딩바
    
    [SerializeField] SettingUI settingUI;
    
    [SerializeField] Button closeBtn;   // 닫기 버튼
    [SerializeField] Button quiteBtn;   // 게임종료 버튼
    
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
    
    void Start()
    {
        closeBtn.onClick.AddListener(OnClickOpenSettings);
        quiteBtn.onClick.AddListener(QuitGame);
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
        // 조건 현재 플레이씬인지
        var activeScene = SceneManager.GetActiveScene().name;
        
        // 나중에 앤딩씬도 넣어줘야함
        if(activeScene != "StartScene")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            //print("activeScene");
        }
    }
    
    // 플레이어만 멈추는 로직 
    
    // 로딩창 출연
    public void ShowLoading(bool show)
    {
        if (loadingPanel) loadingPanel.SetActive(show);
        if (show)
        {
            PauseGame();
            //print("Pause");
        }           // 로딩 중엔 게임 입력/동작 멈춤
        else
        {
            ResumeGame(); 
            //print("Resume");
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
    
    public void QuitGame()
    {
        Application.Quit();
    }
    
    /// <summary>
    /// 사망 UI를 1초에 걸쳐 서서히 표시합니다.
    /// </summary>
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
            Debug.LogError("Dead Panel 또는 Dead BG Image가 UIManager에 할당되지 않았습니다.");
            return;
        }
        ResumeGame();
        deadPanel.SetActive(false);
    }
    
    private IEnumerator FadeInDeadUI(float duration)
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
}