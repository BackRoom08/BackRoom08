using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour, IInteractable
{

    [SerializeField][Tooltip("열고닫을 애니메이션")] private Animator animator;
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
    }

    public void ToggleDoor()
    {
        bool isOpen = animator.GetBool("isOpen");
        animator.SetBool("isOpen", !isOpen );
        //  소리 재생
        if (audioSource != null)
        {
            if (!isOpen && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }
            else if (isOpen && closeSound != null)
            {
                audioSource.PlayOneShot(closeSound);
            }
        }

    }

    public void Interact()
    {
        ToggleDoor();
    }
}
