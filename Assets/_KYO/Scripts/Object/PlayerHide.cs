using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHide : MonoBehaviour
{
    private bool isplayer = false;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
             isplayer = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isplayer = false;
            EnemyBasicMob[] enemies = FindObjectsOfType<EnemyBasicMob>();
            foreach (EnemyBasicMob enemy in enemies)
            {
                enemy.OnDetected(false);
            }
        }
    }

    public void HidePlayer(bool isdooropen)
    {
        if (isplayer && !isdooropen)
        {
            EnemyBasicMob[] enemies = FindObjectsOfType<EnemyBasicMob>();
            foreach (EnemyBasicMob enemy in enemies)
            {
                enemy.OnDetected(true);
            }
        }
        else
        {
            EnemyBasicMob[] enemies = FindObjectsOfType<EnemyBasicMob>();
            foreach (EnemyBasicMob enemy in enemies)
            {
                enemy.OnDetected(false);
            }
        }
    }



}
