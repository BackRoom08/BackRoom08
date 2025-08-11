using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine;

public enum CharacterMoveState
{
    Idle, 
    Walk, 
    Run, 
    Crouch, 
    Jump, 
    Fall
}

[DisallowMultipleComponent]
public class NoiseFromExternalState : MonoBehaviour
{
    [SerializeField, Tooltip("인식 세기 베이스. 클수록 멀리서도 들림")]
    private float baseLoudness = 1.0f;
    [SerializeField, Tooltip("인식 반경 베이스")]
    private float baseRange = 12f;

    // 발 소리
    [SerializeField, Tooltip("걷기 발소리 간격")]
    private float walkStepInterval = 0.5f;
    [SerializeField, Tooltip("달리기 발소리 간격")]
    private float runStepInterval = 0.33f;
    [SerializeField, Tooltip("걷기 세기 계수")]
    private float walkLoudMul = 1.0f;
    [SerializeField, Tooltip("달리기 세기 계수")]
    private float runLoudMul = 1.8f;

    // 숨(호흡) 소리
    [SerializeField, Tooltip("기본 호흡 간격")]
    private float baseBreathInterval = 1.8f;
    [SerializeField, Tooltip("스테미너 수치 조절")]
    private bool heavyWhenStaminaLow = true;
    [SerializeField, Range(0f,1f), Tooltip("스태미너 임계값")]
    private float staminaThreshold = 0.35f;
    [SerializeField, Tooltip("무거운 호흡일 때 간격 배수(작을수록 더 자주)")]
    private float heavyBreathRateMul = 0.6f;
    [SerializeField, Tooltip("무거운 호흡 세기 배수")]
    private float heavyBreathLoudMul = 1.4f;
    [SerializeField, Tooltip("가벼운 호흡 세기 배수")]
    private float lightBreathLoudMul = 0.6f;

    [SerializeField, Tooltip("발소리")] private AudioSource stepSource;
    [SerializeField, Tooltip("호흡")] private AudioSource breathSource;
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private AudioClip breathClip;
    
    private float stamina01 = 1f;                    
    private CharacterMoveState moveState = CharacterMoveState.Idle;
    private bool grounded = true;                    
    
    private float stepTimer;
    private float breathTimer;

    /// <summary>
    /// 플레이어에서 호출 필요
    /// 스태미너/플레이어 상태/땅에 있는지 여부
    /// </summary>
    public void SetExternalState(float stamina, CharacterMoveState state, bool isGrounded)
    {
        stamina01  = Mathf.Clamp01(stamina);
        moveState  = state;
        grounded   = isGrounded;
    }

    private void Awake()
    {
        // 최소 안전 장치: 소스가 비어있으면 같은 소스로 보정
        if (stepSource == null)
        {
            stepSource = GetComponent<AudioSource>();
            if (stepSource == null) stepSource = gameObject.AddComponent<AudioSource>();
        }
        if (breathSource == null) breathSource = stepSource;
    }

    private void Update()
    {
        // 발소리
        if (grounded && (moveState == CharacterMoveState.Walk || moveState == CharacterMoveState.Run))
        {
            float interval = (moveState == CharacterMoveState.Run) ? runStepInterval : walkStepInterval;
            stepTimer += Time.deltaTime;
            if (stepTimer >= interval)
            {
                stepTimer = 0f;
                EmitFootstep(moveState == CharacterMoveState.Run);
            }
        }
        else
        {
            stepTimer = 0f;
        }

        // 호흡
        bool heavy = heavyWhenStaminaLow ? (stamina01 <= staminaThreshold)
                                         : (stamina01 >= staminaThreshold);

        float breathInterval = baseBreathInterval * (heavy ? heavyBreathRateMul : 1f);
        breathTimer += Time.deltaTime;
        if (breathTimer >= breathInterval)
        {
            breathTimer = 0f;
            EmitBreath(heavy);
        }
    }

    /// <summary>발소리: 인식 이벤트 + 실제 재생</summary>
    private void EmitFootstep(bool running)
    {
        float stateMul = running ? runLoudMul : walkLoudMul;
        float loud  = baseLoudness * stateMul;
        float range = baseRange    * Mathf.Sqrt(stateMul);

        // AI 인식(UI 볼륨과 무관)
        NoiseSystem.Emit(new NoiseEvent {
            Position   = transform.position,
            Loudness   = loud,
            MaxRange   = range,
            Instigator = gameObject
        });

        // 실제 소리 재생
        if (footstepClip != null) stepSource.PlayOneShot(footstepClip);
    }

    /// <summary>호흡: 인식 이벤트 + 실제 재생</summary>
    private void EmitBreath(bool heavy)
    {
        float loudMul = heavy ? heavyBreathLoudMul : lightBreathLoudMul;
        float loud  = baseLoudness * loudMul * 0.6f; // 호흡은 기본적으로 더 작게
        float range = baseRange * 0.75f;             // 호흡은 인식 반경 더 작게

        NoiseSystem.Emit(new NoiseEvent {
            Position   = transform.position,
            Loudness   = loud,
            MaxRange   = range,
            Instigator = gameObject
        });

        if (breathClip != null) breathSource.PlayOneShot(breathClip);
    }
}
