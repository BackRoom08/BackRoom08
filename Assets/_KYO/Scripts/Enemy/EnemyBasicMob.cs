using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 기본몹
public class EnemyBasicMob : EnemyController
{
    [Header("조우소리 설정")]
    [SerializeField, Tooltip("일회성 사운드 재생용")] protected AudioSource oneShotAudioSource;
    [SerializeField, Tooltip("조우 시 사운드")] protected AudioClip meetClip;

    private bool isChasing; // 추격

    protected override void Awake()
    {
        base.Awake();
        if (oneShotAudioSource)
        {
            // UIManager에서 SFX 믹서 그룹을 가져옴
            if (UIManager.Instance != null && UIManager.Instance.sfxGroup != null)
            {
                oneShotAudioSource.outputAudioMixerGroup = UIManager.Instance.sfxGroup;
            }
        }
    }

    protected override void OnChasePlayer()
    {
        if (!isChasing)
        {
            if (meetClip != null && oneShotAudioSource != null)
            {
                oneShotAudioSource.PlayOneShot(meetClip);
            }
            isChasing = true;
        }
    }

    protected override IEnumerator ChaseRoutine()
    {
        isChasing = false; // 추격 시작마다 리셋
        return base.ChaseRoutine();
    }

    protected virtual void OnEnable()
    {
        // PlayerMove 컴포넌트를 가진 오브젝트를 찾아 플레이어로 설정
        PlayerMove playerObject = FindObjectOfType<PlayerMove>();
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }
}
