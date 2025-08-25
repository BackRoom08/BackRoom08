using System;
using System.Collections.Generic;
using UnityEngine;

public class ResetGenerator : MonoBehaviour
{
    public int completeStack;
    [Tooltip("문을 열기 위한 타임라인 트리거")]
    public TimeLineStarter doorTrigger;
    
    public int _stack;  // 완료된 개수(스택)

    public LightListOnOff lightListOnOff;


    public void Awake()
    {
        _stack = 0;
    }

    public void AddStack(Generator g)
    {
        _stack++;

        if (_stack >= completeStack)
        {
            if (doorTrigger != null)
                doorTrigger.Interact();

            if (lightListOnOff != null)
                lightListOnOff.SetLights(true);
        }
    }

}
