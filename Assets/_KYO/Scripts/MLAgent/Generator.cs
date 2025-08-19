using UnityEngine;

/// <summary>
/// 싱글 전용 발전기
/// - 한 명이 작업하면 progress가 0→1로 증가
/// - 작업 중이면 IsSoundActive = true (사운드 시스템은 이 bool만 보고 처리)
/// </summary>
public class Generator : MonoBehaviour
{
    [Tooltip("혼자서 0→1 채우는 데 걸리는 시간(초)")]
    public float secondsForOneWorker = 10f;
    
    [Range(0, 1f)] public float progress;
    public bool IsCompleted => progress >= 1f;
    public bool IsSoundActive { get; private set; }

    GameObject _worker;

    public StateNoiseEmitter noise;
    
    void Awake()
    {
        noise = GetComponent<StateNoiseEmitter>();
    }
    
    void Update()
    {
        IsSoundActive = (_worker != null) && !IsCompleted;

        if (_worker == null || IsCompleted) return;

        float delta = Time.deltaTime / Mathf.Max(0.1f, secondsForOneWorker);
        progress = Mathf.Clamp01(progress + delta);

        if (IsCompleted)
        {
            IsSoundActive = false;
            _worker = null;
            if(noise) noise.SetState(CharacterMoveState.Idle);
            // TODO: 완료 후 처리(문 열림 등)가 필요하면 여기에서 호출
        }
    }

    /// <summary>작업 시작 시도. 성공하면 true</summary>
    public bool TryBeginWork(GameObject actor)
    {
        if (IsCompleted) return false;
        if (_worker != null && _worker != actor) return false; // 이미 작업 중
        _worker = actor;
        
        if(noise) noise.SetState(CharacterMoveState.Walk);
        
        return true;
    }

    /// <summary>작업 종료</summary>
    public void EndWork(GameObject actor)
    {
        if (_worker == actor) _worker = null;
        if (_worker == null)
        {
            IsSoundActive = false;
            if(noise) noise.SetState(CharacterMoveState.Idle);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = IsCompleted ? Color.cyan : (IsSoundActive ? Color.yellow : Color.gray);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.25f, 0.5f);
    }
#endif
}