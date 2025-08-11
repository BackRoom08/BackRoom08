using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoiseEmitter : MonoBehaviour
{
    [Tooltip("AI 인식 세기. 값이 클수록 더 멀리서도 들림 (UI 볼륨과 무관)")] public float baseLoudness = 1.0f;
    [Tooltip("AI가 인식 가능한 최대 거리(미터). BaseLoudness와 별개로 거리 제한")] public float baseRange = 12f;
    
    [Tooltip("실제로 소리를 재생할 AudioSource 컴포넌트 (플레이어 귀에 들림)")] public AudioSource sfxSource;
    [Tooltip("재생할 오디오 클립 (발소리, 숨소리, 총소리 등)")] public AudioClip footstepClip;

    // 애니메이션 이벤트나 코드에서 호출
    public void EmitFootstep(float speedMul, float surfaceMul)
    {
        // 1) AI 인식용 브로드캐스트 (UI 볼륨과 무관)
        var e = new NoiseEvent {
            Position   = transform.position,
            Loudness   = baseLoudness * speedMul * surfaceMul,
            MaxRange   = baseRange   * Mathf.Sqrt(speedMul * surfaceMul),
            Instigator = gameObject
        };
        NoiseSystem.Emit(e);

        // 2) 실제 플레이어 귀에 들릴 소리 재생 (UI 볼륨의 영향 받음)
        if (sfxSource && footstepClip)
        {
            sfxSource.PlayOneShot(footstepClip);
        }
    }

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.V))
            GetComponent<NoiseEmitter>()?.EmitFootstep(1f, 1f); // 수동 발소리
    }
}


