using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterMoveState
{
    Idle,   // 멈춤 
    Walk, // 걷기
    Run, // 뛰기
    Crouch, // 앉기 - 플레이어
    Jump, // 점프 - 플레이어
    Exhaustion,  // 탈진 ( 스테미너 일정 수치 이상 ) - 플레이어
    Meet,// 조우
    Chase,// 플레이어추적중사운드

}

[DisallowMultipleComponent]
public class StateNoiseEmitter : MonoBehaviour
{
    [SerializeField] private AudioSource stepSource;   // 걷기 루프
    [SerializeField] private AudioSource breathSource; // 호흡 루프
    
    [SerializeField, Tooltip("걷는 상태(발소리)")]      private AudioClip walkClip;
    [SerializeField, Tooltip("뛰는 상태(숨소리)")]      private AudioClip breathRunClip;
    [SerializeField, Tooltip("탈진 상태(숨소리)")]      private AudioClip staminaExhaustionClip;
    [SerializeField, Tooltip("에너미_조우 소리")]      private AudioClip enemyMeetPlayerClip;
    [SerializeField, Tooltip("에너미_추격 소리")]      private AudioClip enemyChaseClip;
    
    [SerializeField, Range(0f,1f)] private float masterVolume = 0.5f;
    
    [SerializeField, Tooltip("인식 세기 베이스")] private float baseLoudness = 1.0f;
    [SerializeField, Tooltip("인식 반경 베이스(미터)")] private float baseRange = 12f;
    [SerializeField, Tooltip("걷기 세기 배수")] private float walkLoudMul = 1.0f;
    [SerializeField, Tooltip("뛰기(호흡) 세기 배수")] private float runBreathLoudMul = 1.2f;
    [SerializeField, Tooltip("탈진(호흡) 세기 배수")] private float exhaustLoudMul = 1.8f;
    [SerializeField, Range(0,1f), Tooltip("호흡 반경 감쇄")] public float breathRangeMul = 0.75f;
    
    bool  isActive = false;
    float currentLoudMul = 1f;
    float currentRangeMul = 1f;
    
    // 중복 방지
    [SerializeField] AudioClip currentClip;
    [SerializeField] CharacterMoveState currentState = CharacterMoveState.Idle;

    [SerializeField, Tooltip("사운드 인식 끄기(사운드 인식이 필요없다면 꼭 false로)")]
    private bool loudEnabled = false;
    
    void Awake()
    {
        if (!stepSource)   stepSource   = gameObject.AddComponent<AudioSource>();
        if (!breathSource) breathSource = gameObject.AddComponent<AudioSource>();
        if (breathSource == stepSource) breathSource = gameObject.AddComponent<AudioSource>();

        foreach (var s in new[]{stepSource, breathSource})
        {
            s.loop = true; s.playOnAwake = false; s.pitch = 1f; s.volume = masterVolume;
        }
    }
    
    
    // SetState 추가 할시 아래 코드 3줄 추가해서 원하는값 대입 필요
    // 1. Enum 값추가
    // 2. AudioClip 변수 추가해서 원하는값 넣기
    // 3. SetState 함수에 스위치문 추가
    // 4. 아래값 추가 ( PlayLoop의 가운데값 클립만 바꾸면 됨)
    // PlayLoop(stepSource, 추가한 AudioClip 클립, ref currentClip);
    // isActive = true;
    // currentLoudMul  = walkLoudMul;
    // 해당하는 스크립트에서 SetState호출
    // 호출할시 
    // private StateNoiseEmitter noise; 변수 선언
    // 만든 이넘값 넣어서 호출
    // noise.SetState(CharacterMoveState.만든 이넘(Enum));
    
    // 사운드 필요한곳에서 호출
    public void SetState(CharacterMoveState state)
    {
        if(state == currentState) return;
        //print("Current state: " + currentState + " state : " + state);
        currentState = state;
        // 초기화
        StopLoop(stepSource,   ref currentClip);
        StopLoop(breathSource, ref currentClip);
        isActive = false;
        
        switch (currentState)
        {
            case CharacterMoveState.Idle:
                //print("Idle");
                break;
            case CharacterMoveState.Walk:
                //print("Walk");
                PlayLoop(stepSource, walkClip, ref currentClip);
                isActive = true;
                currentLoudMul  = walkLoudMul;
                currentRangeMul = 1f;
                break;

            case CharacterMoveState.Run:
                //print("Run");
                PlayLoop(breathSource, breathRunClip, ref currentClip);
                isActive = true;
                currentLoudMul  = runBreathLoudMul;
                currentRangeMul = breathRangeMul;
                break;

            case CharacterMoveState.Exhaustion:
                //print("Exhaustion");
                PlayLoop(breathSource, staminaExhaustionClip, ref currentClip);
                isActive = true;
                currentLoudMul  = exhaustLoudMul;
                currentRangeMul = breathRangeMul;
                break;
            case CharacterMoveState.Meet:
                
                break;

            case CharacterMoveState.Chase:
                PlayLoop(breathSource, enemyChaseClip, ref currentClip);
                isActive = true;
                currentLoudMul = exhaustLoudMul;
                currentRangeMul = breathRangeMul;
                break;
            
            // 여기 위에 추가 케이스 만들어서
            default:
                break;
        }
    }

    void Update()
    {
        // 사운드 인식 알림
        if (!loudEnabled || !isActive) return;
        
        EmitNoise(currentLoudMul, currentRangeMul);
    }
    
    void PlayLoop(AudioSource src, AudioClip clip, ref AudioClip current)
    {
        if (!src || !clip) return;
        if (current == clip && src.isPlaying) return;
        current = clip; src.clip = clip; src.Play();
    }
    void StopLoop(AudioSource src, ref AudioClip current)
    {
        current = null; if (src && src.isPlaying) src.Stop(); if (src) src.clip = null;
    }
    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        if (stepSource) stepSource.volume = masterVolume;
        if (breathSource) breathSource.volume = masterVolume;
    }
    
    void EmitNoise(float loudMul, float rangeMul)
    {
        float loud  = baseLoudness * Mathf.Max(0.01f, loudMul);
        float range = baseRange    * Mathf.Sqrt(Mathf.Max(0.01f, loudMul)) * Mathf.Max(0.01f, rangeMul);

        NoiseSystem.Emit(new NoiseEvent {
            Position   = transform.position,
            Loudness   = loud,
            MaxRange   = range,
            Instigator = gameObject
        });
    }
}