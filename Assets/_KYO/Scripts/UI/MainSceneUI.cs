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
        SceneLoader.Instance.LoadSceneAdditive("Stage1", true);
    }

    void OnSettingBtnClick()
    {
        //print("setting");
        UIManager.Instance.ToggleSettings();
    }

    void OnQuitBtnClick()
    {
        print("quit");
    }
}