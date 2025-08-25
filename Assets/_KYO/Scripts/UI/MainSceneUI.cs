using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainScene : MonoBehaviour
{
    // 인스펙터에서 Button 컴포넌트 3개 연결
    public Button playBtn;
    public Button settingBtn;
    public Button quitBtn;
    private bool isClick = false;

    void Start()
    {
        // 각각 버튼에 클릭 이벤트 등록
        playBtn.onClick.AddListener(OnPlayBtnClick);
        settingBtn.onClick.AddListener(OnSettingBtnClick);
        quitBtn.onClick.AddListener(OnQuitBtnClick);
    }

    void OnPlayBtnClick()
    {
        //print("play");
        if (!isClick)
            SceneLoader.Instance.LoadSceneAdditive("Stage1", true);
        isClick = true;
    }

    void OnSettingBtnClick()
    {
        //print("setting");
        UIManager.Instance.ToggleSettings();
    }

    void OnQuitBtnClick()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}