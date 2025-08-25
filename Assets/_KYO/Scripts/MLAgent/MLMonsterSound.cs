using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MLMonsterSound : MonoBehaviour
{
    [SerializeField] StateNoiseEmitter stateNoiseEmitter;
    [SerializeField] MLMonsterAgent mLMonsterAgent;
    [SerializeField] AudioSource  audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode  = AudioRolloffMode.Logarithmic;
        audioSource.minDistance  = 2f;
        audioSource.maxDistance  = 40f;
    }
    
    private void Update()
    {
        if(mLMonsterAgent.CurrentMode == MonsterMode.Stalk)
            stateNoiseEmitter.SetState(CharacterMoveState.Idle);
        else
            stateNoiseEmitter.SetState(CharacterMoveState.Walk);
        
    }
}
