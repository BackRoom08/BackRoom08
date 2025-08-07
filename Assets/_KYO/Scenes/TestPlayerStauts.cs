using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestPlayerStauts : MonoBehaviour
{
    public int sound;
    public GameObject enemy;
    public bool onOff;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            onOff = !onOff; // 토글
            if (enemy != null)
                enemy.GetComponent<EnemyBasicMob>().OnDetected(onOff);
        }
        // enemy.GetComponent<EnemyBasicMob>().OnDetected(onOff);
    }
}
 