using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    public float masterVolume = 1.0f;       // 마스터 볼륨
    public float bgmVolume = 1.0f;          // BGM 볼륨
    public float sfxVolume = 1.0f;          // 효과음 볼륨
    public float mouseSensitivity = 1.0f;   // 마우스 감도
    public bool isFullScreen = true;        // 전체화면 여부  
    public int currentLanguageIndex = 0; // 0: 한국어, 1: 영어

    [SerializeField][Tooltip("bgm")] private AudioSource bgmAudioSource;
    [SerializeField][Tooltip("효과음 리스트")] private List<AudioSource> sfxAudioSources;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // 볼륨
    public void SetMasterVolume(float value)
    {
        masterVolume = value;
        ApplyMasterVolume();
    }

    public void SetBGMVolume(float value)
    {
        bgmVolume = value;
        ApplyBGMVolume();
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = value;
        ApplySFXVolume();
    }

    // 마우스 감도
    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = value;
    }

    public void SetFullScreen(bool value)
    {
        isFullScreen = value;
        ApplyFullScreen();
    }
    
    public void ApplyMasterVolume()
    {
        AudioListener.volume = masterVolume;
    }
    
    public void ApplyBGMVolume()
    {
        if(bgmAudioSource != null)
            bgmAudioSource.volume = bgmVolume;
    }

    public void ApplySFXVolume()
    {
        foreach (var sfx in sfxAudioSources)
        {
            if(sfx != null)
                sfx.volume = sfxVolume;
        }
    }

    public void ApplyFullScreen()
    {
        Screen.fullScreen = isFullScreen;
    }
    
}
