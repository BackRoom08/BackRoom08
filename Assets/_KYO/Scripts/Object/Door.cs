using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour, IInteractable
{

    [SerializeField][Tooltip("열고닫을 애니메이션")] private Animator animator;
    [SerializeField, Tooltip("문 열리고 닫힌 상태")] private bool isDoorOpen = false;
    private PlayerHide playerHide; // PlayerHide 스크립트 참조

    //사운드 관련
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    private AudioSource audioSource;


    private void Start()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // UIManager에서 SFX 믹서 그룹을 가져와서 설정
        if (UIManager.Instance != null && UIManager.Instance.sfxGroup != null)
        {
            audioSource.outputAudioMixerGroup = UIManager.Instance.sfxGroup;
        }

        // 시작할 때 문의 상태를 애니메이터와 동기화
        animator.SetBool("isOpen", isDoorOpen);

        // 자식 오브젝트에서 PlayerHide 컴포넌트 찾기
        playerHide = GetComponentInChildren<PlayerHide>();
    }

    public void ToggleDoor()
    {
        isDoorOpen = !isDoorOpen;
        animator.SetBool("isOpen", isDoorOpen);

        //  소리 재생
        if (audioSource != null)
        {
            // 문이 열렸을 때
            if (isDoorOpen && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }
            // 문이 닫혔을 때
            else if (!isDoorOpen && closeSound != null)
            {
                audioSource.PlayOneShot(closeSound);
            }
        }
    }

       
    public void Interact()
    {
        ToggleDoor();
        if (playerHide != null)
        {
            playerHide.HidePlayer(isDoorOpen);
        }
    }

    

}
