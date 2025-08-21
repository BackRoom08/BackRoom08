using UnityEngine;

public class EnemyHitBox : MonoBehaviour
{
    [SerializeField] private int mentalDamage = 50;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerStatus status = other.GetComponent<PlayerStatus>();
            if (status != null)
            {
                status.HealMentalHP(-mentalDamage);
                Debug.Log($"정신력 {mentalDamage} 감소!");
            }
        }
    }
}

