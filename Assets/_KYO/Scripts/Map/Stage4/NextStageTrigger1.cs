using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NextStageTrigger1 : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        SceneLoader.Instance.LoadSceneAdditive("EndingScene", true);
    }
}

