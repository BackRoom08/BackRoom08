using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameSetting", menuName = "Scriptable Objects/GameSetting")]
public class GameSetting : ScriptableObject
{
    [Tooltip("전체 볼륨"), Range(0f, 1f)] public float masterVolume = 1f;
    [Tooltip("배경 음악"), Range(0f, 1f)] public float bgmVolume = 1f;
    [Tooltip("효과음"), Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("마우스 감도"), Range(0f, 2f)] public float mouseSensitivity = 1f;
    [Tooltip("화면 설정")]public string screenMode = "FullScreen";  // 전촤화면, 창모드
    [Tooltip("언어")]public string language = "ko";   // ko, english
    const string K = "SET_";

    public void Load(){
        masterVolume     = PlayerPrefs.GetFloat(K+"master", masterVolume);
        bgmVolume        = PlayerPrefs.GetFloat(K+"bgm", bgmVolume);
        sfxVolume        = PlayerPrefs.GetFloat(K+"sfx", sfxVolume);
        mouseSensitivity = PlayerPrefs.GetFloat(K+"sens", mouseSensitivity);
        language         = PlayerPrefs.GetString(K+"lang", language);
        screenMode       = PlayerPrefs.GetString(K+"screen", screenMode);
    }
    public void Save(){
        PlayerPrefs.SetFloat(K+"master", masterVolume);
        PlayerPrefs.SetFloat(K+"bgm", bgmVolume);
        PlayerPrefs.SetFloat(K+"sfx", sfxVolume);
        PlayerPrefs.SetFloat(K+"sens", mouseSensitivity);
        PlayerPrefs.SetString(K+"lang", language);
        PlayerPrefs.SetString(K+"screen", screenMode);
        PlayerPrefs.Save();
    }
}