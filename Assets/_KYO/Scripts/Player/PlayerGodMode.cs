using UnityEngine;

public class PlayerGodMode : MonoBehaviour
{
    public float godModeRadius = 10f;
    public float pushForce = 10f;
    public LayerMask enemyLayer;
    public string enemyTag = "Enemy";

    private bool isGodModeActive = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F11))
        {
            isGodModeActive = !isGodModeActive;
            Debug.Log("God Mode " + (isGodModeActive ? "ON" : "OFF"));
        }

        if (isGodModeActive)
        {
            PushEnemiesAway();
        }
    }

    void PushEnemiesAway()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, godModeRadius, enemyLayer);

        foreach (Collider enemy in enemies)
        {
            if (enemy.CompareTag(enemyTag))
            {
                Rigidbody rb = enemy.attachedRigidbody;
                if (rb != null)
                {
                    Vector3 pushDir = (enemy.transform.position - transform.position).normalized;
                    rb.AddForce(pushDir * pushForce, ForceMode.Force);
                }
                else
                {
                    // Rigidbody 없을 경우 위치 직접 조정
                    Vector3 pushDir = (enemy.transform.position - transform.position).normalized;
                    enemy.transform.position += pushDir * Time.deltaTime * pushForce;
                }
            }
        }
    }
}
