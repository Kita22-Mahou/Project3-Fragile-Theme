
using UnityEngine;
using UnityEngine.UI;

public class ShellWallBounce : MonoBehaviour
{
    [Header("Shell Charges")]
    [SerializeField] private int maxShellUses = 3;
    [SerializeField] private Image[] shellUseIcons = new Image[3];

    [Header("Protection")]
    [SerializeField] private float protectionDuration = 0.75f;

    [Header("Wall Bounce")]
    [SerializeField] private float horizontalBounceMultiplier = 1.63f;
    [SerializeField] private float minimumHorizontalBounceSpeed = 12f;
    [SerializeField] private float upwardBounceHeight = 3f;
    [SerializeField] private float upwardBounceMultiplier = 1.1f;

    [Header("Vertical Bounce")]
    [SerializeField] private float verticalBounceMultiplier = 1.1f;
    [SerializeField] private float minimumVerticalBounceSpeed = 16f;
    [SerializeField] private float maximumBounceSpeed = 30f;

    [Header("Collision Detection")]
    [SerializeField] private float minimumImpactSpeed = 2.35f;

    [Range(0.1f, 1f)]
    [SerializeField] private float wallNormalThreshold = 0.7f;

    [Range(0.1f, 1f)]
    [SerializeField] private float verticalNormalThreshold = 0.7f;

    [SerializeField] private LayerMask bounceableLayers;

    [Header("Control Lock")]
    [SerializeField] private float controlLockDuration = 0.12f;
    [SerializeField] private float maxControlLockDuration = 2f;

    private Rigidbody2D rb;
    private MouseController mouseController;

    private int remainingUses;
    private float protectionTimer;

    private bool isProtecting;
    private bool hasBounced;
    private bool protectDamageUntilLanding;
    private bool hasLeftGroundAfterBounce;

    private bool controlLocked;
    private bool previousMovementLocked;
    private float controlLockStartTime;

    private Vector2 velocityBeforePhysics;

    public int RemainingUses => remainingUses;
    public int MaxShellUses => maxShellUses;
    public bool IsProtecting => isProtecting || protectDamageUntilLanding;

    private void Awake()
    {
        maxShellUses = Mathf.Max(0, maxShellUses);
        protectionDuration = Mathf.Max(0.05f, protectionDuration);

        horizontalBounceMultiplier = Mathf.Max(0f, horizontalBounceMultiplier);
        minimumHorizontalBounceSpeed = Mathf.Max(0f, minimumHorizontalBounceSpeed);
        upwardBounceHeight = Mathf.Max(0f, upwardBounceHeight);
        upwardBounceMultiplier = Mathf.Max(0f, upwardBounceMultiplier);
        verticalBounceMultiplier = Mathf.Max(0f, verticalBounceMultiplier);
        minimumVerticalBounceSpeed = Mathf.Max(0f, minimumVerticalBounceSpeed);

        maximumBounceSpeed = Mathf.Max(
            minimumVerticalBounceSpeed,
            maximumBounceSpeed
        );

        maxControlLockDuration = Mathf.Max(
            controlLockDuration,
            maxControlLockDuration
        );

        rb = GetComponent<Rigidbody2D>();
        mouseController = GetComponent<MouseController>();

        remainingUses = maxShellUses;

        if (rb != null)
            velocityBeforePhysics = rb.linearVelocity;

        UpdateShellIcons();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            ActivateShell();

        UpdateProtectionTimer();
        UpdateLandingProtection();
        UpdateControlLock();
    }

    private void FixedUpdate()
    {
        if (rb != null)
            velocityBeforePhysics = rb.linearVelocity;
    }

    private bool IsGrounded()
    {
        return mouseController != null && mouseController.isGrounded;
    }

    private void ActivateShell()
    {
        if (rb == null || remainingUses <= 0 || IsProtecting)
            return;

        // ไม่ใช้ Shell ขณะยืนอยู่นิ่งบนพื้น
        if (IsGrounded() &&
            rb.linearVelocity.magnitude < 0.5f)
        {
            return;
        }

        remainingUses--;

        isProtecting = true;
        hasBounced = false;
        protectionTimer = protectionDuration;

        UpdateShellIcons();
    }

    private void UpdateProtectionTimer()
    {
        if (!isProtecting)
            return;

        protectionTimer -= Time.deltaTime;

        if (protectionTimer <= 0f)
            EndProtection();
    }

    private void UpdateLandingProtection()
    {
        if (!protectDamageUntilLanding)
            return;

        // รอให้ Tumi ออกจากพื้นหลังจากสะท้อนสำเร็จก่อน
        if (!IsGrounded())
        {
            hasLeftGroundAfterBounce = true;
        }
        // เมื่อกลับมาลงพื้น จึงยกเลิกการป้องกัน
        else if (hasLeftGroundAfterBounce)
        {
            protectDamageUntilLanding = false;
            hasLeftGroundAfterBounce = false;
        }
    }

    private void UpdateControlLock()
    {
        if (!controlLocked)
            return;

        float lockedTime = Time.time - controlLockStartTime;

        bool reachedMaxTime =
            lockedTime >= maxControlLockDuration;

        bool canUnlockOnGround =
            lockedTime >= controlLockDuration && IsGrounded();

        if (reachedMaxTime || canUnlockOnGround)
            UnlockControl();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryBounce(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryBounce(collision);
    }

    private bool IsBounceable(GameObject obj)
    {
        if (obj == null)
            return false;

        return (bounceableLayers.value & (1 << obj.layer)) != 0;
    }

    // หัน Normal ให้ออกจากผิวเข้าหาตัว Tumi
    private Vector2 GetSurfaceNormal(ContactPoint2D contact)
    {
        Vector2 normal = contact.normal.normalized;

        Vector2 toPlayer =
            rb.worldCenterOfMass - contact.point;

        if (Vector2.Dot(normal, toPlayer) < 0f)
            normal = -normal;

        return normal;
    }

    private void TryBounce(Collision2D collision)
    {
        if (!isProtecting || hasBounced || rb == null)
            return;

        if (!IsBounceable(collision.gameObject))
            return;

        if (collision.contactCount == 0)
            return;

        Vector2 incoming = velocityBeforePhysics;

        if (incoming.sqrMagnitude < 0.01f)
            incoming = rb.linearVelocity;

        if (incoming.sqrMagnitude < 0.01f)
            return;

        Vector2 bestNormal = Vector2.zero;
        float strongestImpact = 0f;

        // เลือกจุดสัมผัสที่กำลังพุ่งเข้าชนมากที่สุด
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            Vector2 normal = GetSurfaceNormal(contact);

            float impact = -Vector2.Dot(incoming, normal);

            if (impact > strongestImpact)
            {
                strongestImpact = impact;
                bestNormal = normal;
            }
        }

        if (bestNormal == Vector2.zero ||
            strongestImpact < minimumImpactSpeed)
        {
            return;
        }

        bool hitWall =
            Mathf.Abs(bestNormal.x) >= wallNormalThreshold &&
            Mathf.Abs(bestNormal.x) > Mathf.Abs(bestNormal.y);

        bool hitHorizontalSurface =
            Mathf.Abs(bestNormal.y) >= verticalNormalThreshold &&
            Mathf.Abs(bestNormal.y) >= Mathf.Abs(bestNormal.x);

        Vector2 outgoing;

        if (hitWall)
        {
            outgoing = CalculateWallBounce(incoming, bestNormal);
        }
        else if (hitHorizontalSurface)
        {
            outgoing = CalculateVerticalBounce(incoming, bestNormal);
        }
        else
        {
            return;
        }

        if (outgoing.sqrMagnitude < 0.01f)
            return;

        // ขยับออกจากผิวเล็กน้อยเพื่อลดการติด Collider
        rb.position += bestNormal * 0.04f;
        rb.linearVelocity = outgoing;

        hasBounced = true;

        // ป้องกันดาเมจต่อจนกว่าจะลงพื้นหลังจากเด้ง
        protectDamageUntilLanding = true;
        hasLeftGroundAfterBounce = false;

        LockControl();
    }

    private Vector2 CalculateWallBounce(
        Vector2 incoming,
        Vector2 wallNormal)
    {
        // เด้งออกจากผนังในแนวนอน
        float horizontalSpeed = Mathf.Max(
            Mathf.Abs(incoming.x) * horizontalBounceMultiplier,
            minimumHorizontalBounceSpeed
        );

        float outgoingX =
            Mathf.Sign(wallNormal.x) * horizontalSpeed;

        float outgoingY = incoming.y;

        // หากกำลังตกชนผนัง ให้คำนวณแรงเด้งขึ้นจาก Gravity จริง
        if (incoming.y < 0f)
        {
            float gravity = Mathf.Abs(
                Physics2D.gravity.y * rb.gravityScale
            );

            float speedForTargetHeight = Mathf.Sqrt(
                2f * gravity * upwardBounceHeight
            );

            outgoingY = Mathf.Max(
                speedForTargetHeight,
                Mathf.Abs(incoming.y) * upwardBounceMultiplier
            );
        }
        else if (incoming.y > 0f)
        {
            // กำลังขึ้นชนผนัง ให้รักษาความเร็วขึ้นไว้
            outgoingY = incoming.y * upwardBounceMultiplier;
        }

        // จำกัดความเร็วแนวตั้งก่อนคำนวณพื้นที่ความเร็วแนวนอน
        float maxVerticalSpeed = maximumBounceSpeed * 0.8f;

        outgoingY = Mathf.Clamp(
            outgoingY,
            -maxVerticalSpeed,
            maxVerticalSpeed
        );

        float horizontalSpeedBudget = Mathf.Sqrt(
            Mathf.Max(
                0f,
                maximumBounceSpeed * maximumBounceSpeed -
                outgoingY * outgoingY
            )
        );

        outgoingX = Mathf.Sign(wallNormal.x) * Mathf.Min(
            Mathf.Abs(outgoingX),
            horizontalSpeedBudget
        );

        return new Vector2(outgoingX, outgoingY);
    }

    private Vector2 CalculateVerticalBounce(
        Vector2 incoming,
        Vector2 surfaceNormal)
    {
        // สะท้อนตามมุมจริงและรักษาความเร็วแนวนอน
        Vector2 reflected = Vector2.Reflect(
            incoming,
            surfaceNormal.normalized
        );

        if (reflected.sqrMagnitude < 0.001f)
            return surfaceNormal.normalized * minimumVerticalBounceSpeed;

        float speed = Mathf.Clamp(
            reflected.magnitude * verticalBounceMultiplier,
            minimumVerticalBounceSpeed,
            maximumBounceSpeed
        );

        return reflected.normalized * speed;
    }

    private void LockControl()
    {
        if (mouseController == null || controlLocked)
            return;

        previousMovementLocked = mouseController.movementLocked;

        mouseController.movementLocked = true;

        controlLocked = true;
        controlLockStartTime = Time.time;
    }

    private void UnlockControl()
    {
        if (mouseController != null)
            mouseController.movementLocked = previousMovementLocked;

        controlLocked = false;
    }

    private void EndProtection()
    {
        isProtecting = false;
        hasBounced = false;
        protectionTimer = 0f;
    }

    // HealthPoint ต้องเรียกก่อนหัก HP
    public bool TryBlockDamage()
    {
        return IsProtecting;
    }

    // เตรียมไว้ใช้เติม Shell ที่ Checkpoint
    public void RefillShells()
    {
        remainingUses = maxShellUses;
        UpdateShellIcons();
    }

    private void UpdateShellIcons()
    {
        if (shellUseIcons == null)
            return;

        for (int i = 0; i < shellUseIcons.Length; i++)
        {
            Image icon = shellUseIcons[i];

            if (icon != null)
                icon.gameObject.SetActive(i < remainingUses);
        }
    }

    private void OnDisable()
    {
        UnlockControl();

        isProtecting = false;
        protectDamageUntilLanding = false;
        hasLeftGroundAfterBounce = false;
    }
}
