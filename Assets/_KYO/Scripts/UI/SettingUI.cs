using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.Audio;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class SettingUI : MonoBehaviour
{
    public GameSetting data;
    
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Slider sensSlider;
    
    public TMP_Text languageValueText;
    public TMP_Text screenModeValueText;

    [SerializeField] private AudioMixer audioMixer;
    const string PARAM_MASTER = "MasterVol";
    const string PARAM_BGM    = "BgmVol";
    const string PARAM_SFX    = "SfxVol";
    
    // 순환 후보들 (표시용 텍스트와 내부코드 매핑)
    readonly string[] langCodes = { "ko", "en" };
    readonly string[] langTexts = { "한국어", "English" };

    readonly string[] modeCodes = { "fullscreen", "windowed" };
    readonly string[] modeTexts = { "전체 화면", "창 모드" };

    [SerializeField] int langIndex  = 0;
    int modeIndex  = 0;

    void OnEnable(){
        // 패널이 켜질 때 최신 값으로 UI 갱신
        RefreshFromData();
        if (audioMixer)
        {
            audioMixer.SetFloat(PARAM_MASTER, Linear01ToDb(data.masterVolume));
            audioMixer.SetFloat(PARAM_BGM, Linear01ToDb(data.bgmVolume));
            audioMixer.SetFloat(PARAM_SFX, Linear01ToDb(data.sfxVolume));
        }
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
    public void OnSensChanged(float v){   data.mouseSensitivity = v; }

    static float Linear01ToDb(float v) => (v <= 0.0001f) ? -80f : Mathf.Log10(Mathf.Clamp01(v)) * 20f;

    public void OnMasterChanged(float v){
        data.masterVolume = v;
        audioMixer.SetFloat(PARAM_MASTER, Linear01ToDb(v));
    }
    public void OnBgmChanged(float v){
        data.bgmVolume = v;
        audioMixer.SetFloat(PARAM_BGM, Linear01ToDb(v));
    }
    public void OnSfxChanged(float v){
        data.sfxVolume = v;
        audioMixer.SetFloat(PARAM_SFX, Linear01ToDb(v));
    }
    
    // 언어 버튼 연결
    public void OnClickLangLeft(){  langIndex = (langIndex - 1 + langCodes.Length) % langCodes.Length; UpdateLangLabel(); }
    public void OnClickLangRight(){ langIndex = (langIndex + 1) % langCodes.Length; UpdateLangLabel(); }

    async void UpdateLangLabel(){
        data.language = langCodes[langIndex];
        if (languageValueText) languageValueText.text = langTexts[langIndex];
       
        // 언어 변경
        await LocalizationSettings.InitializationOperation.Task;
        var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(data.language));
        if (locale != null) LocalizationSettings.SelectedLocale = locale;
    }

    // 화면 버튼 연결
    public void OnClickModeLeft(){  modeIndex = (modeIndex - 1 + modeCodes.Length) % modeCodes.Length; UpdateModeAndApply(); }
    public void OnClickModeRight(){ modeIndex = (modeIndex + 1) % modeCodes.Length; UpdateModeAndApply(); }

    void UpdateModeAndApply(){
        data.screenMode = modeCodes[modeIndex];
        if (screenModeValueText) screenModeValueText.text = modeTexts[modeIndex];
        ApplyScreenMode(data.screenMode);
    }
    
    public void ApplyAndSave()
    {
        //AudioListener.volume = data.masterVolume;

        // TODO: BGM/SFX는 AudioMixer 파라미터 또는 각 AudioSource에 반영
        // ex) mixer.SetFloat("BGMdB", Linear01ToDb(data.bgmVolume));

        // 화면 모드는 이미 즉시 반영; 안전하게 한 번 더
        ApplyScreenMode(data.screenMode);

        data.Save();
    }

    void ApplyScreenMode(string code)
    {
        // 해상도 값 
        int w = Screen.currentResolution.width;
        int h = Screen.currentResolution.height;

        if (code == "fullscreen")
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            Screen.fullScreen = true;
        }
        else
        {
            Screen.fullScreenMode = FullScreenMode.Windowed;
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            Screen.fullScreen = false;
        }
    }

    int IndexOf(string[] arr, string code){
        for (int i=0;i<arr.Length;i++) if (arr[i]==code) return i;
        return -1;
    }
    
}
