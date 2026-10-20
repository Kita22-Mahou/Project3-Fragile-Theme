
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

    private ShellWallBounce shellWallBounce;
    private Rigidbody2D rb;
    private bool isDead;

    public bool IsDead => isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        shellWallBounce = GetComponent<ShellWallBounce>();

        maxHP = Mathf.Max(1f, maxHP);
        currentHP = Mathf.Clamp(maxHP, 0f, maxHP);

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

        // ยกเว้น Layer ที่ไม่ต้องการคิดดาเมจ
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
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f)
            return;

        // ตรวจสอบ ShellWallBounce ก่อนหัก HP
        // หากกำลังป้องกันอยู่ จะไม่เสียเลือด
        if (shellWallBounce != null &&
            shellWallBounce.TryBlockDamage())
        {
            return;
        }

        currentHP = Mathf.Clamp(
            currentHP - damage,
            0f,
            maxHP
        );

        UpdateShellUI();

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

        float hpPerShell = maxHP / shellGrayOverlays.Length;
        float lostHP = maxHP - currentHP;

        for (int i = 0; i < shellGrayOverlays.Length; i++)
        {
            Image overlay = shellGrayOverlays[i];

            if (overlay == null)
                continue;

            float lostInThisShell = Mathf.Clamp(
                lostHP - (i * hpPerShell),
                0f,
                hpPerShell
            );

            overlay.fillAmount = hpPerShell > 0f
                ? lostInThisShell / hpPerShell
                : 0f;
        }
    }

    private void Die()
    {
        isDead = true;
    }
}
