using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skip : MonoBehaviour
{
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneLoader.Instance.LoadSceneAdditive("StartScene", true);
        }
    }
}
