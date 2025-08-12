using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public struct NoiseEvent
{
    public Vector3 Position;     // 소리 발생 위치
    public float Loudness;       // 기본 소리 세기(1.0 = 기준)
    public float MaxRange;       // 최대 감지 거리(미터)
    public GameObject Instigator;// 소리 낸 주체
}

public class NoiseSystem : MonoBehaviour
{
    public static event Action<NoiseEvent> OnNoise;

    public static void Emit(NoiseEvent e)
    {
        OnNoise?.Invoke(e);
    }
}

