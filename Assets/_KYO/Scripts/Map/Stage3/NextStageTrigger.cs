using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NextStageTrigger : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        SceneLoader.Instance.LoadSceneAdditive("Stage1", true);
    }
}

