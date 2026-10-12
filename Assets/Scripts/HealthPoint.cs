
using UnityEngine;

public class HealthPoint : MonoBehaviour
{
    [Header("HP / Shell")]
    public float maxHP = 100f;
    public float currentHP = 100f;

    [Header("Collision Damage")]
    [SerializeField] private LayerMask ignoreDamageLayers;
    [SerializeField] private float safeImpactSpeed = 10f;
    [SerializeField] private float damagePerSpeed = 2f;
    [SerializeField] private float maxCollisionDamage = 20f;

    [Header("Wall Collision")]
    [SerializeField] private float minVerticalSpeedForWallDamage = 1f;

    private Rigidbody2D rb;
    private bool isDead;

    public bool IsDead => isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        maxHP = Mathf.Max(1f, maxHP);
        currentHP = maxHP;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead)
            return;

        // ถ้าวัตถุที่ชนอยู่ใน Layer ที่ไม่ต้องการคิดดาเมจ
        int otherLayer = collision.gameObject.layer;

        if ((ignoreDamageLayers.value & (1 << otherLayer)) != 0)
            return;

        //ดูว่ากระแทก พื้น บนหัว หรือกำแพง
        bool hitFloor = false;
        bool hitCeiling = false;
        bool hitWall = false;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
                hitFloor = true;
            else if (contact.normal.y < -0.5f)
                hitCeiling = true;
            else if (Mathf.Abs(contact.normal.x) > 0.5f)
                hitWall = true;
        }

        // ใช้ความเร็วก่อน/ขณะเกิดการชน
        Vector2 impactVelocity = collision.relativeVelocity;

        float impactSpeed = 0f;

        if (hitFloor)
        {
            // ตกลงมากระแทกพื้น
            impactSpeed = Mathf.Abs(impactVelocity.y);
        }
        else if (hitCeiling)
        {
            // กระโดดขึ้นไปกระแทกเพดาน
            impactSpeed = Mathf.Abs(impactVelocity.y);
        }
        else if (hitWall)
        {
            // ชนกำแพงต้องมีความเร็วแนวดิ่งร่วมด้วย
            // ป้องกันการกระโดดไปแนวราบอย่างเดียวแล้วเสีย HP
            if (Mathf.Abs(impactVelocity.y) < minVerticalSpeedForWallDamage)
                return;

            // ใช้ความเร็วแนวราบตอนกระแทกกำแพง
            impactSpeed = Mathf.Abs(impactVelocity.x);
        }
        else
        {
            return;
        }

        if (impactSpeed <= safeImpactSpeed)
            return;

        float excessSpeed = impactSpeed - safeImpactSpeed;

        float damage = Mathf.Min(
            excessSpeed * damagePerSpeed,
            maxCollisionDamage
        );

        TakeDamage(damage);

        Debug.Log(
            $"Collision: Floor={hitFloor}, Ceiling={hitCeiling}, Wall={hitWall}" +
            $" | Velocity={impactVelocity}" +
            $" | Impact Speed={impactSpeed:F2}" +
            $" | Damage={damage:F1}"
        );
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f)
            return;

        currentHP = Mathf.Clamp(
            currentHP - damage,
            0f,
            maxHP
        );

        Debug.Log($"Damage: {damage:F1} | HP: {currentHP:F1}/{maxHP:F1}");

        if (currentHP <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        currentHP = Mathf.Clamp(
            currentHP + amount,
            0f,
            maxHP
        );
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Tumi กระดองแตก! HP หมดแล้ว");
    }
}
