using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PersistentUIBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoSpawn()
    {
        if (Object.FindObjectOfType<UIManager>() == null) return;
        var prefab = Resources.Load<GameObject>("PersistentUI");
        if(prefab == null) Object.Instantiate(prefab);
    }
    
    
}
