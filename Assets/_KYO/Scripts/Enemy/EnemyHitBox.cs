using UnityEngine;

public class EnemyHitBox : MonoBehaviour
{
    [SerializeField] private int mentalDamage = 50;
    private EnemyEyeLightMob enemyplayerattack;

    private void Awake()
    {
        enemyplayerattack = GetComponentInParent<EnemyEyeLightMob>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerStatus status = other.GetComponent<PlayerStatus>();
            if (status != null)
            {
                status.HealMentalHP(-mentalDamage);
               // Debug.Log($"정신력 {mentalDamage} 감소!");

                if (status.currentMentalHP <= 0)
                {
                    enemyplayerattack.InitiateAttack(other.gameObject);
                }
            }
        }
    }
}

