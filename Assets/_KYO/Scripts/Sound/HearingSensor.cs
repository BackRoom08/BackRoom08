using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HearingSensor : MonoBehaviour
{
    [Tooltip("이 값 이상이면 인식 성공. 값이 낮을수록 작은 소리도 듣지만 오탐지↑")] 
    public float hearThreshold = 0.2f;
    [Tooltip("소리를 가릴 레이어")] public LayerMask occlusionMask;

    void OnEnable()  => NoiseSystem.OnNoise += OnNoiseHeard;
    void OnDisable() => NoiseSystem.OnNoise -= OnNoiseHeard;

    void OnNoiseHeard(NoiseEvent e)
    {
        if (e.Instigator == gameObject) return;

        float dist = Vector3.Distance(transform.position, e.Position);
        if (dist > e.MaxRange) return;

        float perceived = e.Loudness / (1f + Mathf.Pow(dist / (e.MaxRange * 0.5f), 2f));

        if (Physics.Raycast(e.Position + Vector3.up * 0.1f,
                (transform.position + Vector3.up * 1.6f) - (e.Position + Vector3.up * 0.1f),
                out _, dist, occlusionMask))
        {
            perceived *= 0.35f; // 벽 감쇠
        }

        // 사운드 감지
        if (perceived >= hearThreshold)
        {
            // GetComponent<YourAIController>()?.OnHearNoise(e.Position, perceived);
            //print($"[{name}] heard noise at {e.Position} (power {perceived:F2})");
        }
    }
}

