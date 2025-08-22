using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SoundMonitor : MonoBehaviour
{
    public AudioSource audioSource;
    private AudioMixerGroup mixerGroup;
    
    void Start()
    {
        mixerGroup = UIManager.Instance.sfxGroup;
        if (mixerGroup != null)
            audioSource.outputAudioMixerGroup = mixerGroup;
        
    }
    
}
