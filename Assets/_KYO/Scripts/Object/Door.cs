using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour
{

    [SerializeField][Tooltip("열고닫을 애니메이션")] private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void ToggleDoor()
    {
        bool isOpen = animator.GetBool("isOpen");
        animator.SetBool("isOpen", !isOpen );
    }

}
