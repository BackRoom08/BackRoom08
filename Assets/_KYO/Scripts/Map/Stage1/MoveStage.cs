using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveStage : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SceneLoader.Instance.LoadSceneAdditive("Stage3", true);
        }

    }

}
