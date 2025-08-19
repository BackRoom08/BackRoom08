using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables; // PlayableDirector를 위해 추가

public class TimeLineStarter : MonoBehaviour, IInteractable
{
    [Tooltip("실행할 타임라인을 가진 PlayableDirector")]
    public PlayableDirector director;

    public void Interact()
    {
        if (director != null)
        {
            director.Play();
            Destroy(this);
        }
    }
}