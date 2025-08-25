using UnityEngine;
using System;

public class Generator : MonoBehaviour
{
    [Tooltip("혼자서 0→1 채우는 데 걸리는 시간(초)")]
    public float secondsForOneWorker = 10f;

    [Header("Who can work")]
    [Tooltip("작업을 허용할 태그(예: Player). 비워두면 모두 허용.")]
    public string allowedTag = "Player";

    [Header("Auto release")]
    [Tooltip("작업자가 이 거리보다 멀어지면 자동으로 작업 해제")]
    public float autoReleaseDistance = 2.2f;

    [Range(0, 1f)] public float progress;
    public bool IsCompleted => progress >= 1f;
    public bool IsSoundActive { get; private set; }

    GameObject _worker;
    public StateNoiseEmitter noise;

    // 완료 시 한 번만 호출되는 이벤트
    public event Action<Generator> OnCompleted;
    bool _completedEventSent;

    void Awake()
    {
        noise = GetComponent<StateNoiseEmitter>();
    }

    void Update()
    {
        IsSoundActive = (_worker != null) && !IsCompleted;

        // ★ 작업자 안전 감시: 멀어지면 자동 해제
        if (_worker != null && !IsCompleted)
        {
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = _worker.transform.position; b.y = 0f;
            if (!_worker.activeInHierarchy ||
                (a - b).sqrMagnitude > autoReleaseDistance * autoReleaseDistance)
            {
                ForceStop();
            }
        }

        if (_worker == null || IsCompleted)
        {
            // 완료 시 1회만 이벤트 발생
            if (IsCompleted && !_completedEventSent)
            {
                _completedEventSent = true;
                IsSoundActive = false;
                _worker = null;
                if (noise) noise.SetState(CharacterMoveState.Idle);
                OnCompleted?.Invoke(this);
            }
            return;
        }

        float delta = Time.deltaTime / Mathf.Max(0.1f, secondsForOneWorker);
        progress = Mathf.Clamp01(progress + delta);
    }

    public bool TryBeginWork(GameObject actor)
    {
        if (IsCompleted) return false;
        if (!IsAllowedWorker(actor)) return false;
        if (_worker != null && _worker != actor) return false;

        _worker = actor;
        if (noise) noise.SetState(CharacterMoveState.Walk);
        return true;
    }

    public void EndWork(GameObject actor)
    {
        if (_worker == actor) _worker = null;
        if (_worker == null)
        {
            IsSoundActive = false;
            if (noise) noise.SetState(CharacterMoveState.Idle);
        }
    }

    // 강제 중지(거리 초과/비활성 등)
    public void ForceStop()
    {
        _worker = null;
        IsSoundActive = false;
        if (noise) noise.SetState(CharacterMoveState.Idle);
    }

    // 그룹에서 다시 돌리기 위해 호출할 초기화 함수
    public void ResetGenerator()
    {
        progress = 0f;
        _worker = null;
        IsSoundActive = false;
        _completedEventSent = false;     // 이벤트 다시 쏠 수 있게 리셋
        if (noise) noise.SetState(CharacterMoveState.Idle);
    }

    bool IsAllowedWorker(GameObject actor)
    {
        if (string.IsNullOrEmpty(allowedTag)) return true;
        return actor != null && actor.CompareTag(allowedTag);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = IsCompleted ? Color.cyan : (IsSoundActive ? Color.yellow : Color.gray);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.25f, 0.5f);
    }
#endif
}
