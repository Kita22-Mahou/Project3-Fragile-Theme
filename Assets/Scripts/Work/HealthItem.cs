using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HealthItem : MonoBehaviour
{
    [Header("Heal Setting")]
    [SerializeField] private float healAmount = 25f;

    private bool collected = false;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
            return;

        HealthNew health = other.GetComponentInParent<HealthNew>();

        if (health == null || health.IsDead)
            return;

        if (health.CurrentHP >= health.MaxHP)
            return;

        float previousHP = health.CurrentHP;

        health.Heal(healAmount);

        if (health.CurrentHP > previousHP)
        {
            collected = true;

            Destroy(gameObject);
        }
    }
}