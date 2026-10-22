
using UnityEngine;
using UnityEngine.UI;

public class HealthNew : MonoBehaviour
{
    [Header("Pizza HP")]
    [Tooltip("จำนวนแฉกเลือดทั้งหมด")]
    [SerializeField] private int maxSlices = 6;

    [Tooltip("จำนวนแฉกเลือดเริ่มต้น")]
    [SerializeField] private int startingSlices = 6;

    [Tooltip("Image ของพิซซ่า 6 แฉก เรียงตามลำดับที่ต้องการให้หาย")]
    [SerializeField] private Image[] pizzaSlices = new Image[6];

    [Header("Fall Damage")]
    [Tooltip("ความสูงที่ตกได้โดยไม่เสียเลือด")]
    [SerializeField] private float safeFallHeight = 4f;

    [Tooltip("ระยะตกส่วนเกินที่ทำให้เสียเลือดเพิ่ม 1 แฉก")]
    [SerializeField] private float fallHeightPerSlice = 3f;

    [Tooltip("จำนวนแฉกสูงสุดที่เสียจากการตกหนึ่งครั้ง")]
    [SerializeField] private int maxSlicesLostPerFall = 6;

    [Tooltip("Layer ที่ไม่คิดดาเมจ")]
    [SerializeField] private LayerMask ignoreDamageLayers;

    [Tooltip("ความเร็วกระแทกที่ปลอดภัย")]
    [SerializeField] private float safeImpactSpeed = 10f;

    [Tooltip("ความเร็วกระแทกส่วนเกินต่อเลือดที่เสีย 1 แฉก")]
    [SerializeField] private float impactSpeedPerSlice = 8f;

    [Tooltip("จำนวนแฉกสูงสุดที่เสียจากการชนหนึ่งครั้ง")]
    [SerializeField] private int maxImpactSlices = 2;

    [Tooltip("ความเร็วแนวดิ่งขั้นต่ำเมื่อชนกำแพง")]
    [SerializeField] private float minVerticalSpeedForWallDamage = 1f;

    [Header("Legacy Damage Compatibility")]
    [Tooltip("สเกล HP เดิม ใช้แปลงดาเมจ float จากสคริปต์เก่า")]
    [SerializeField] private float legacyFullHP = 100f;

    [SerializeField] private GlassBreakEffect glassBreakEffect;

    private Rigidbody2D rb;
    private MouseController mouseController;
    private ShellWallBounce shellWallBounce;

    private int currentSlices;
    private bool isDead;
    private bool isTrackingFall;
    private float highestFallY;

    public bool IsDead => isDead;
    public int CurrentSlices => currentSlices;
    public int MaxSlices => maxSlices;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mouseController = GetComponent<MouseController>();
        shellWallBounce = GetComponent<ShellWallBounce>();

        maxSlices = Mathf.Max(1, maxSlices);
        startingSlices = Mathf.Clamp(startingSlices, 0, maxSlices);
        maxSlicesLostPerFall = Mathf.Clamp(maxSlicesLostPerFall, 1, maxSlices);
        maxImpactSlices = Mathf.Max(1, maxImpactSlices);

        safeFallHeight = Mathf.Max(0f, safeFallHeight);
        fallHeightPerSlice = Mathf.Max(0.1f, fallHeightPerSlice);
        impactSpeedPerSlice = Mathf.Max(0.1f, impactSpeedPerSlice);
        legacyFullHP = Mathf.Max(1f, legacyFullHP);

        currentSlices = startingSlices;

        UpdatePizzaUI();

        if (currentSlices <= 0)
            Die();
    }

    private void Update()
    {
        if (isDead || rb == null)
            return;

        bool grounded = IsGrounded();

        // เริ่มเก็บความสูงเมื่อตัวละครออกจากพื้น
        if (!grounded)
        {
            if (!isTrackingFall)
            {
                isTrackingFall = true;
                highestFallY = rb.position.y;
            }

            // บันทึกจุดสูงสุดระหว่างลอยอยู่ รวมถึงจุดสูงสุดของการกระโดด
            highestFallY = Mathf.Max(highestFallY, rb.position.y);
        }
    }

    private bool IsGrounded()
    {
        return mouseController != null && mouseController.isGrounded;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead || rb == null)
            return;

        bool hitFloor = false;
        bool hitCeiling = false;
        bool hitWall = false;

        // จัด Normal ให้ชี้จากพื้นผิวเข้าหาตัว Tumi
        foreach (ContactPoint2D contact in collision.contacts)
        {
            Vector2 normal = GetNormalTowardsPlayer(contact);

            if (normal.y > 0.5f)
                hitFloor = true;
            else if (normal.y < -0.5f)
                hitCeiling = true;
            else if (Mathf.Abs(normal.x) > 0.5f)
                hitWall = true;
        }

        int otherLayer = collision.gameObject.layer;

        bool ignoreDamage =
            (ignoreDamageLayers.value & (1 << otherLayer)) != 0;

        // ชนพื้น: คำนวณดาเมจจากความสูงที่ตกจริง
        if (hitFloor)
        {
            if (!ignoreDamage)
                ApplyFallDamage();

            // จบการนับการตกเมื่อแตะพื้น
            isTrackingFall = false;
            return;
        }

        if (ignoreDamage)
            return;

        // กำแพงและเพดานใช้ความเร็วกระแทก
        Vector2 impactVelocity = collision.relativeVelocity;
        float impactSpeed;

        if (hitCeiling)
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

        int slicesLost = Mathf.CeilToInt(
            excessSpeed / impactSpeedPerSlice
        );

        slicesLost = Mathf.Clamp(
            slicesLost,
            1,
            maxImpactSlices
        );

        TakeDamageSlices(slicesLost);
    }

    private Vector2 GetNormalTowardsPlayer(ContactPoint2D contact)
    {
        Vector2 normal = contact.normal.normalized;
        Vector2 toPlayer = rb.worldCenterOfMass - contact.point;

        if (Vector2.Dot(normal, toPlayer) < 0f)
            normal = -normal;

        return normal;
    }

    private void ApplyFallDamage()
    {
        if (!isTrackingFall || rb == null)
            return;

        // ระยะตกจากจุดสูงสุดถึงจุดก่อนลงพื้น
        float fallDistance = Mathf.Max(
            0f,
            highestFallY - rb.position.y
        );

        isTrackingFall = false;

        // ยังอยู่ในระยะปลอดภัย
        if (fallDistance <= safeFallHeight)
            return;

        float excessHeight = fallDistance - safeFallHeight;

        // คำนวณจำนวนแฉกตามระยะตกส่วนเกิน
        int slicesLost = Mathf.CeilToInt(
            excessHeight / fallHeightPerSlice
        );

        slicesLost = Mathf.Clamp(
            slicesLost,
            1,
            maxSlicesLostPerFall
        );

        TakeDamageSlices(slicesLost);
    }

    // ใช้หักเลือดโดยระบุจำนวนแฉกโดยตรง
    public void TakeDamageSlices(int amount)
    {
        if (isDead || amount <= 0)
            return;

        // ตรวจ Shell ก่อนลดเลือด
        if (shellWallBounce != null &&
            shellWallBounce.TryBlockDamage())
        {
            return;
        }

        int previousSlices = currentSlices;

        int newSlices = Mathf.Clamp(
            currentSlices - amount,
            0,
            maxSlices
        );

        // แตกเฉพาะแฉกที่กำลังจะเสียไป
        if (glassBreakEffect != null && pizzaSlices != null)
        {
            for (int i = newSlices;
                 i < previousSlices && i < pizzaSlices.Length;
                 i++)
            {
                Image slice = pizzaSlices[i];

                if (slice != null && slice.gameObject.activeInHierarchy)
                    glassBreakEffect.Break(slice);
            }
        }

        currentSlices = newSlices;

        UpdatePizzaUI();

        if (currentSlices <= 0)
            Die();
    }

    // รองรับสคริปต์เดิมที่ส่งดาเมจเป็น float
    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f)
            return;

        float hpPerSlice = legacyFullHP / maxSlices;

        int slicesLost = Mathf.Max(
            1,
            Mathf.RoundToInt(damage / hpPerSlice)
        );

        TakeDamageSlices(slicesLost);
    }

    // ใช้เติมเลือดเป็นจำนวนแฉก
    public void HealSlices(int amount)
    {
        if (isDead || amount <= 0)
            return;

        currentSlices = Mathf.Clamp(
            currentSlices + amount,
            0,
            maxSlices
        );

        UpdatePizzaUI();
    }

    // รองรับสคริปต์เดิมที่ฮีลด้วยค่า float
    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        float hpPerSlice = legacyFullHP / maxSlices;

        int slicesRecovered = Mathf.Max(
            1,
            Mathf.RoundToInt(amount / hpPerSlice)
        );

        HealSlices(slicesRecovered);
    }

    private void UpdatePizzaUI()
    {
        if (pizzaSlices == null)
            return;

        for (int i = 0; i < pizzaSlices.Length; i++)
        {
            Image slice = pizzaSlices[i];

            if (slice == null)
                continue;

            // ซ่อนแฉกตามลำดับเมื่อเลือดลด
            slice.gameObject.SetActive(i < currentSlices);
        }
    }

    private void Die()
    {
        isDead = true;
        currentSlices = 0;
        UpdatePizzaUI();
    }
}
