using TMPro;
using UnityEngine;
using UnityEngine.UI;
// TMP 쓰면 using TMPro;

public class SettingUI : MonoBehaviour
{
    public GameSetting data;
    
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider sensSlider;
    
    public TMP_Text languageValueText;
    public TMP_Text screenModeValueText;

    // 순환 후보들 (표시용 텍스트와 내부코드 매핑)
    readonly string[] langCodes = { "ko", "en" };
    readonly string[] langTexts = { "한국어", "English" };

    readonly string[] modeCodes = { "fullscreen", "windowed" };
    readonly string[] modeTexts = { "전체 화면", "창 모드" };

    int langIndex  = 0;
    int modeIndex  = 0;

    void OnEnable(){
        // 패널이 켜질 때 최신 값으로 UI 갱신
        RefreshFromData();
    }

    public void RefreshFromData()
    {
        if (data == null) return;

        // 슬라이더
        if (masterSlider) masterSlider.SetValueWithoutNotify(data.masterVolume);
        if (bgmSlider)    bgmSlider.SetValueWithoutNotify(data.bgmVolume);
        if (sfxSlider)    sfxSlider.SetValueWithoutNotify(data.sfxVolume);
        if (sensSlider)   sensSlider.SetValueWithoutNotify(data.mouseSensitivity);

        // 인덱스 동기화
        langIndex = IndexOf(langCodes, data.language);
        if (langIndex < 0) langIndex = 0;

        modeIndex = IndexOf(modeCodes, data.screenMode);
        if (modeIndex < 0) modeIndex = 0;

        UpdateLangLabel();
        //UpdateModeLabel();
    }

    // 슬라이더 연결
    public void OnMasterChanged(float v){ data.masterVolume = v; }
    public void OnBgmChanged(float v){    data.bgmVolume    = v; }
    public void OnSfxChanged(float v){    data.sfxVolume    = v; }
    public void OnSensChanged(float v){   data.mouseSensitivity = v; }

    // 언어 버튼 연결
    public void OnClickLangLeft(){  langIndex = (langIndex - 1 + langCodes.Length) % langCodes.Length; UpdateLangLabel(); }
    public void OnClickLangRight(){ langIndex = (langIndex + 1) % langCodes.Length; UpdateLangLabel(); }

    void UpdateLangLabel(){
        data.language = langCodes[langIndex];
        if (languageValueText) languageValueText.text = langTexts[langIndex];
        // 실제 로컬라이즈 시스템 연동은 프로젝트에 맞춰 별도 적용하세요.
    }

    // 화면 버튼 연결
    public void OnClickModeLeft(){  modeIndex = (modeIndex - 1 + modeCodes.Length) % modeCodes.Length; UpdateModeAndApply(); }
    public void OnClickModeRight(){ modeIndex = (modeIndex + 1) % modeCodes.Length; UpdateModeAndApply(); }

    void UpdateModeAndApply(){
        data.screenMode = modeCodes[modeIndex];
        if (screenModeValueText) screenModeValueText.text = modeTexts[modeIndex];
        ApplyScreenMode(data.screenMode);
    }

    // 적용 + 저장 (적용 버튼)
    public void ApplyAndSave()
    {
        // 오디오 적용(간단 버전) — 프로젝트 믹서 쓰면 거기에 매핑
        AudioListener.volume = data.masterVolume;

        // TODO: BGM/SFX는 AudioMixer 파라미터 또는 각 AudioSource에 반영
        // ex) mixer.SetFloat("BGMdB", Linear01ToDb(data.bgmVolume));

        // 화면 모드는 이미 즉시 반영; 안전하게 한 번 더
        ApplyScreenMode(data.screenMode);

        data.Save();
    }

    void ApplyScreenMode(string code)
    {
        // 현재 해상도 유지한 채 전환
        int w = Screen.currentResolution.width;
        int h = Screen.currentResolution.height;

        if (code == "fullscreen")
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow; // 보더리스 권장
            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            Screen.fullScreen = true;
        }
        else // windowed
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            // 적당한 창 크기로 열고 싶으면 여기서 w,h를 줄이세요 (예: 1600x900)
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            Screen.fullScreen = false;
        }
    }

    int IndexOf(string[] arr, string code){
        for (int i=0;i<arr.Length;i++) if (arr[i]==code) return i;
        return -1;
    }

    // 필요 시 dB 변환 유틸(오디오 믹서용)
    public static float Linear01ToDb(float v){
        if (v <= 0.0001f) return -80f;
        return Mathf.Log10(v)*20f;
    }
}
