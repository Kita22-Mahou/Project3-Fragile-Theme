
using UnityEngine;

public class Obstacles : MonoBehaviour
{
    [Header("Damage")]
    public float damage = 20f;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        DealDamage(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        DealDamage(other);
    }

    private void DealDamage(Collider2D other)
    {
        TumiHealth health = other.GetComponent<TumiHealth>();

        if (health == null)
            health = other.GetComponentInParent<TumiHealth>();

        if (health != null)
        {
            health.TakeDamage(damage);
        }
    }
}
