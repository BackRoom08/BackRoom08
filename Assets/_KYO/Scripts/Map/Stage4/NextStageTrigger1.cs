using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NextStageTrigger1 : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        //print("OnCollisionEnter");
        SceneLoader.Instance.LoadSceneAdditive("EndingScene", true);
    }
}

