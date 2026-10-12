
using UnityEngine;
using UnityEngine.UI;

public class TumiHealth : MonoBehaviour
{
    [Header("HP / Shell")]
    public float maxHP = 100f;
    public float currentHP = 100f;

    [Header("Fall Damage")]
    public float safeFallHeight = 14f; //ระยะปลอดภัย
    public float maxFallDamage = 20f;
    private Rigidbody2D rb;
    private MouseController controller;

    [Header("Shell HP UI")]
    [SerializeField] private Image[] shellGrayOverlays = new Image[3];
    [SerializeField] private Text hpText;

    private bool isTrackingFall;
    private float fallStartY;
    private bool isDead;

    public bool IsDead => isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<MouseController>();

        maxHP = Mathf.Max(1f, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        currentHP = maxHP;

        SetupShellUI();
        UpdateHPUI();
    }

    private void FixedUpdate()
    {

        bool isGrounded = Physics2D.OverlapBox(
            controller.groundCheck.position,
            controller.groundCheckSize,
            0f,
            controller.groundLayer
        ) != null;

        if (!isGrounded &&
            !isTrackingFall &&
            rb.linearVelocity.y < -0.01f)
        {
            isTrackingFall = true;
            fallStartY = rb.position.y;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead)
            return;

        // ตรวจสอบว่าชนพื้นจากด้านบน
        bool landedOnTop = false;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                landedOnTop = true;
                break;
            }
        }

        if (!landedOnTop)
            return;

        Obstacles hazard =
            collision.collider.GetComponent<Obstacles>();

        if (hazard == null)
        {
            hazard =
                collision.collider.GetComponentInParent<Obstacles>();
        }

        if (hazard != null)
        {
            isTrackingFall = false;
            return;
        }

        if (!isTrackingFall)
            return;

        // คำนวณระยะตกจริง
        float fallDistance = Mathf.Max(
            0f,
            fallStartY - rb.position.y
        );

        // หักระยะปลอดภัยออก
        float excessHeight = fallDistance - safeFallHeight;

        if (excessHeight > 0f)
        {

            float damage = Mathf.Min(excessHeight,maxFallDamage);

            damage = Mathf.Clamp(damage,0f,maxFallDamage);

            TakeDamage(damage);

            Debug.Log("Fall Distance: " + fallDistance.ToString("F2")+ " | Fall Damage: " + damage.ToString("F1"));
        }
        else
        {
            Debug.Log(
                "ปลอดภัย ระยะตก: "+ fallDistance.ToString("F2"));
        }

        isTrackingFall = false;
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

        Debug.Log("Tumi เสีย HP: " + damage.ToString("F1") + " | HP เหลือ: "+ currentHP.ToString("F1")+ "/" + maxHP);

        if (currentHP <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        currentHP = Mathf.Clamp(currentHP + amount,0f,maxHP);
    }

    private void SetupShellUI()
    {
        if (shellGrayOverlays == null)
            return;

        foreach (Image overlay in shellGrayOverlays)
        {
            if (overlay == null)
                continue;

            // เติมภาพแบบแนวตั้งจากบนลงล่าง
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Vertical;
            overlay.fillOrigin = (int)Image.OriginVertical.Top;

            // สีเทาของส่วนที่เสียเลือด
            overlay.color = Color.gray;

            // เริ่มต้นด้วยกระดองสีปกติทั้งหมด
            overlay.fillAmount = 0f;

            overlay.raycastTarget = false;
        }
    }

    private void UpdateHPUI()
    {
        if (shellGrayOverlays != null &&
            shellGrayOverlays.Length > 0)
        {
            float shellHP = maxHP / shellGrayOverlays.Length;

            // คำนวณเลือดที่หายไปทั้งหมด
            float lostHP = maxHP - currentHP;

            for (int i = 0; i < shellGrayOverlays.Length; i++)
            {
                Image overlay = shellGrayOverlays[i];

                if (overlay == null)
                    continue;

                // ลำดับ i = 0 คือกระดองบน
                // i = 1 คือกระดองกลาง
                // i = 2 คือกระดองล่าง
                float lostInThisShell = Mathf.Clamp(
                    lostHP - (i * shellHP),
                    0f,
                    shellHP
                );

                // เปลี่ยนสัดส่วนเลือดที่เสียให้เป็น Fill Amount
                overlay.fillAmount = shellHP > 0f
                    ? lostInThisShell / shellHP
                    : 0f;
            }
        }

        if (hpText != null)
        {
            hpText.text =
                "Shell: "
                + Mathf.CeilToInt(currentHP)
                + " / "
                + Mathf.CeilToInt(maxHP);
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Tumi กระดองแตก! HP หมดแล้ว");
    }

}
