using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gomain : MonoBehaviour
{
    private void OnEnable()
    {
        SceneLoader.Instance.LoadSceneAdditive("StartScene", true);
    }
}
