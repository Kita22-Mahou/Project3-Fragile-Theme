
using UnityEngine;
using UnityEngine.UI;

public class HealthPoint1 : MonoBehaviour
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

    [Header("Shell HP UI")]
    [Tooltip("เรียงจากกระดองบน กลาง และล่าง")]
    [SerializeField] private Image[] shellGrayOverlays = new Image[3];
    private ShellDefense shellDefense;

    private Rigidbody2D rb;
    private bool isDead;

    public bool IsDead => isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        shellDefense = GetComponent<ShellDefense>();

        maxHP = Mathf.Max(1f, maxHP);
        currentHP = maxHP;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        SetupShellUI();
        UpdateShellUI();
    }

    private void SetupShellUI()
    {
        if (shellGrayOverlays == null)
            return;

        foreach (Image overlay in shellGrayOverlays)
        {
            if (overlay == null)
                continue;

            // ให้ภาพสีเทาเติมจากด้านบนลงด้านล่าง
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Vertical;
            overlay.fillOrigin = (int)Image.OriginVertical.Top;

            overlay.color = Color.gray;
            overlay.fillAmount = 0f;
            overlay.raycastTarget = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead)
            return;

        // ตรวจสอบ Layer ที่ไม่คิดดาเมจ
        int otherLayer = collision.gameObject.layer;

        if ((ignoreDamageLayers.value & (1 << otherLayer)) != 0)
            return;

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

        Vector2 impactVelocity = collision.relativeVelocity;
        float impactSpeed = 0f;

        if (hitFloor)
        {
            impactSpeed = Mathf.Abs(impactVelocity.y);
        }
        else if (hitCeiling)
        {
            impactSpeed = Mathf.Abs(impactVelocity.y);
        }
        else if (hitWall)
        {
            if (Mathf.Abs(impactVelocity.y) <
                minVerticalSpeedForWallDamage)
            {
                return;
            }

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

        //กำลังอยู่ในกระดองหรือไม่
        if (shellDefense != null &&
            shellDefense.TryBlockDamage())
        {
            return;
        }

        currentHP = Mathf.Clamp(
            currentHP - damage,
            0f,
            maxHP
        );

        UpdateShellUI();

        Debug.Log(
            $"Damage: {damage:F1} | HP: {currentHP:F1}/{maxHP:F1}"
        );

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

        UpdateShellUI();
    }

    private void UpdateShellUI()
    {
        if (shellGrayOverlays == null ||
            shellGrayOverlays.Length == 0)
        {
            return;
        }

        // แบ่ง HP เท่า ๆ กันตามจำนวนกระดอง
        float hpPerShell = maxHP / shellGrayOverlays.Length;

        // HP ที่หายไปทั้งหมด
        float lostHP = maxHP - currentHP;

        for (int i = 0; i < shellGrayOverlays.Length; i++)
        {
            Image overlay = shellGrayOverlays[i];

            if (overlay == null)
                continue;

            // กระดองลำดับ 0 คือด้านบน
            float lostInThisShell = Mathf.Clamp(
                lostHP - (i * hpPerShell),
                0f,
                hpPerShell
            );

            // เติมสีเทาจากด้านบนลงด้านล่าง
            overlay.fillAmount = hpPerShell > 0f
                ? lostInThisShell / hpPerShell
                : 0f;
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Tumi กระดองแตก! HP หมดแล้ว");
    }
}
