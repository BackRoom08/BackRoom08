using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightListOnOff : MonoBehaviour
{
    [SerializeField] private List<GameObject> lightList = new List<GameObject>();
    
    public void SetLights(bool isOn)
    {
        foreach (GameObject obj in lightList)
        {
            if (obj != null) 
                obj.SetActive(isOn);
        }
    }
}
