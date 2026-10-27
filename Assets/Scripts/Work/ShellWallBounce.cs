using UnityEngine;
using UnityEngine.UI;

public class ShellWallBounce : MonoBehaviour
{
    [Header("Shell Charges")]
    [SerializeField] private int maxShellUses = 3;
    [SerializeField] private Image[] shellUseIcons = new Image[3];

    [Header("Shell Protection")]
    [SerializeField] private float protectionDuration = 0.75f;

    [Header("Automatic Wall Bounce")]
    [Tooltip("เลือก Layer ของกำแพงที่เด้งได้")]
    [SerializeField] private LayerMask wallBounceLayers;

    [Tooltip("แรงสะท้อนแนวนอน")]
    [SerializeField] private float horizontalBounceMultiplier = 1.63f;

    [SerializeField] private float minimumHorizontalBounceSpeed = 12f;

    [Tooltip("ความสูงเป้าหมายเมื่อชนกำแพงขณะกำลังตก")]
    [SerializeField] private float upwardBounceHeight = 3f;

    [SerializeField] private float upwardBounceMultiplier = 1.1f;

    [Tooltip("ระยะเวลาขั้นต่ำระหว่างการเด้งกำแพง")]
    [SerializeField] private float wallBounceCooldown = 0.12f;

    [Header("Shell Floor Bounce")]
    [Tooltip("Layer ของพื้นที่ Shell สามารถเด้งได้")]
    [SerializeField] private LayerMask bounceableLayers;

    [SerializeField] private float verticalBounceMultiplier = 1.1f;
    [SerializeField] private float minimumVerticalBounceSpeed = 16f;
    [SerializeField] private float maximumBounceSpeed = 30f;

    [Header("Collision Detection")]
    [SerializeField] private float minimumImpactSpeed = 2.35f;

    [Range(0.1f, 1f)]
    [SerializeField] private float wallNormalThreshold = 0.7f;

    [Range(0.1f, 1f)]
    [SerializeField] private float verticalNormalThreshold = 0.7f;

    [Header("Control Lock")]
    [SerializeField] private float controlLockDuration = 0.12f;
    [SerializeField] private float maxControlLockDuration = 2f;

    private Rigidbody2D rb;
    private MouseController mouseController;

    private int remainingUses;
    private float protectionTimer;
    private float lastWallBounceTime = float.NegativeInfinity;

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

    public bool IsProtecting =>
        isProtecting || protectDamageUntilLanding;

    // ให้ BounceSurface ตรวจสอบว่า ShellWallBounce
    // รับผิดชอบเด้งกำแพง Layer นี้อยู่หรือไม่
    public bool UsesAutomaticWallBounceForLayer(int layer)
    {
        return enabled &&
               (wallBounceLayers.value & (1 << layer)) != 0;
    }

    private void Awake()
    {
        maxShellUses = Mathf.Max(0, maxShellUses);
        protectionDuration = Mathf.Max(0.05f, protectionDuration);

        horizontalBounceMultiplier =
            Mathf.Max(0f, horizontalBounceMultiplier);

        minimumHorizontalBounceSpeed =
            Mathf.Max(0f, minimumHorizontalBounceSpeed);

        upwardBounceHeight = Mathf.Max(0f, upwardBounceHeight);
        upwardBounceMultiplier = Mathf.Max(0f, upwardBounceMultiplier);

        verticalBounceMultiplier =
            Mathf.Max(0f, verticalBounceMultiplier);

        minimumVerticalBounceSpeed =
            Mathf.Max(0f, minimumVerticalBounceSpeed);

        maximumBounceSpeed = Mathf.Max(
            minimumVerticalBounceSpeed,
            maximumBounceSpeed
        );

        wallBounceCooldown = Mathf.Max(0f, wallBounceCooldown);

        maxControlLockDuration = Mathf.Max(
            controlLockDuration,
            maxControlLockDuration
        );

        rb = GetComponent<Rigidbody2D>();
        mouseController = GetComponent<MouseController>();

        if (rb == null)
        {
            Debug.LogError(
                "ShellWallBounce requires Rigidbody2D.",
                this
            );

            enabled = false;
            return;
        }

        remainingUses = maxShellUses;
        velocityBeforePhysics = rb.linearVelocity;

        UpdateShellIcons();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ActivateShell();
        }

        UpdateProtectionTimer();
        UpdateLandingProtection();
        UpdateControlLock();
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            velocityBeforePhysics = rb.linearVelocity;
        }
    }

    private bool IsGrounded()
    {
        return mouseController != null &&
               mouseController.isGrounded;
    }

    // --------------------------------------------------
    // SPACEBAR SHELL PROTECTION
    // --------------------------------------------------

    private void ActivateShell()
    {
        if (rb == null ||
            remainingUses <= 0 ||
            IsProtecting)
        {
            return;
        }

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
        {
            EndProtection();
        }
    }

    private void UpdateLandingProtection()
    {
        if (!protectDamageUntilLanding)
            return;

        if (!IsGrounded())
        {
            hasLeftGroundAfterBounce = true;
        }
        else if (hasLeftGroundAfterBounce)
        {
            protectDamageUntilLanding = false;
            hasLeftGroundAfterBounce = false;
        }
    }

    // --------------------------------------------------
    // COLLISION
    // --------------------------------------------------

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // กำแพงเด้งอัตโนมัติ ไม่เกี่ยวกับ Spacebar
        bool wallBounced = TryAutomaticWallBounce(collision);

        // ถ้าไม่ได้เด้งจากกำแพง ให้ตรวจ Shell กับพื้น
        if (!wallBounced)
        {
            TryShellFloorBounce(collision);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Shell เด้งกับพื้นเท่านั้น
        TryShellFloorBounce(collision);
    }

    private bool IsLayerInMask(GameObject obj, LayerMask mask)
    {
        if (obj == null)
            return false;

        return (mask.value & (1 << obj.layer)) != 0;
    }

    // --------------------------------------------------
    // AUTOMATIC WALL BOUNCE
    // --------------------------------------------------

    private bool TryAutomaticWallBounce(Collision2D collision)
    {
        if (rb == null ||
            collision.contactCount == 0)
        {
            return false;
        }

        // ตรวจ Layer ของกำแพง ไม่ต้องใช้ Spacebar
        if (!IsLayerInMask(collision.gameObject, wallBounceLayers))
        {
            return false;
        }

        Vector2 incoming = velocityBeforePhysics;

        if (incoming.sqrMagnitude < 0.01f)
        {
            incoming = rb.linearVelocity;
        }

        Vector2 wallNormal = Vector2.zero;
        float strongestImpact = 0f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);

            Vector2 normal = GetSurfaceNormal(contact);

            // รับเฉพาะการชนด้านข้าง
            if (Mathf.Abs(normal.x) < wallNormalThreshold ||
                Mathf.Abs(normal.x) <= Mathf.Abs(normal.y))
            {
                continue;
            }

            // ต้องกำลังเคลื่อนที่เข้าหากำแพง
            float impact = -Vector2.Dot(incoming, normal);

            if (impact > strongestImpact)
            {
                strongestImpact = impact;
                wallNormal = normal;
            }
        }

        if (wallNormal == Vector2.zero ||
            strongestImpact < minimumImpactSpeed)
        {
            return false;
        }

        // ป้องกันการเด้งซ้ำถี่เกินไป
        if (Time.time - lastWallBounceTime < wallBounceCooldown)
        {
            return false;
        }

        Vector2 outgoing = CalculateWallBounce(
            incoming,
            wallNormal
        );

        if (outgoing.sqrMagnitude < 0.01f)
        {
            return false;
        }

        lastWallBounceTime = Time.time;

        // ขยับออกจากกำแพงและกำหนดแรงเด้ง
        rb.position += wallNormal * 0.04f;
        rb.linearVelocity = outgoing;

        // แจ้ง MouseController ว่าเกิดการเด้งจากภายนอก
        // ไม่รีเซ็ตสิทธิ์ Double Jump
        if (mouseController != null)
        {
            mouseController.NotifyExternalBounce();
        }

        return true;
    }

    private Vector2 CalculateWallBounce(
        Vector2 incoming,
        Vector2 wallNormal)
    {
        float horizontalSpeed = Mathf.Max(
            Mathf.Abs(incoming.x) * horizontalBounceMultiplier,
            minimumHorizontalBounceSpeed
        );

        float outgoingX =
            Mathf.Sign(wallNormal.x) * horizontalSpeed;

        float outgoingY = incoming.y;

        // หากกำลังตกชนกำแพง ให้เด้งขึ้น
        if (incoming.y < 0f)
        {
            float gravity = Mathf.Abs(
                Physics2D.gravity.y * rb.gravityScale
            );

            float speedForHeight = Mathf.Sqrt(
                2f * gravity * upwardBounceHeight
            );

            outgoingY = Mathf.Max(
                speedForHeight,
                Mathf.Abs(incoming.y) * upwardBounceMultiplier
            );
        }
        else if (incoming.y > 0f)
        {
            // หากกำลังขึ้น ให้รักษาความเร็วขึ้นไว้
            outgoingY =
                incoming.y * upwardBounceMultiplier;
        }

        outgoingY = Mathf.Clamp(
            outgoingY,
            -maximumBounceSpeed * 0.8f,
            maximumBounceSpeed * 0.8f
        );

        float horizontalBudget = Mathf.Sqrt(
            Mathf.Max(
                0f,
                maximumBounceSpeed * maximumBounceSpeed -
                outgoingY * outgoingY
            )
        );

        outgoingX = Mathf.Sign(wallNormal.x) * Mathf.Min(
            Mathf.Abs(outgoingX),
            horizontalBudget
        );

        return new Vector2(outgoingX, outgoingY);
    }

    // --------------------------------------------------
    // SPACEBAR SHELL FLOOR BOUNCE
    // --------------------------------------------------

    private void TryShellFloorBounce(Collision2D collision)
    {
        // ต้องกด Spacebar เพื่อเปิด Shell ก่อน
        if (!isProtecting ||
            hasBounced ||
            rb == null)
        {
            return;
        }

        // รับเฉพาะ Layer ที่อนุญาตให้ Shell เด้ง
        if (!IsLayerInMask(collision.gameObject, bounceableLayers))
        {
            return;
        }

        if (collision.contactCount == 0)
            return;

        Vector2 incoming = velocityBeforePhysics;

        if (incoming.sqrMagnitude < 0.01f)
        {
            incoming = rb.linearVelocity;
        }

        Vector2 floorNormal = Vector2.zero;
        float strongestImpact = 0f;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);

            Vector2 normal = GetSurfaceNormal(contact);

            // รับเฉพาะพื้นด้านล่างผู้เล่น ไม่ใช่กำแพงหรือเพดาน
            if (normal.y < verticalNormalThreshold ||
                normal.y <= Mathf.Abs(normal.x))
            {
                continue;
            }

            float impact = -Vector2.Dot(incoming, normal);

            if (impact > strongestImpact)
            {
                strongestImpact = impact;
                floorNormal = normal;
            }
        }

        if (floorNormal == Vector2.zero ||
            strongestImpact < minimumImpactSpeed)
        {
            return;
        }

        Vector2 outgoing = CalculateVerticalBounce(
            incoming,
            floorNormal
        );

        if (outgoing.sqrMagnitude < 0.01f)
            return;

        rb.position += floorNormal * 0.04f;
        rb.linearVelocity = outgoing;

        hasBounced = true;

        // คงการป้องกันดาเมจจนกว่าจะลงพื้นหลังจากเด้ง
        protectDamageUntilLanding = true;
        hasLeftGroundAfterBounce = false;

        if (mouseController != null)
        {
            mouseController.NotifyExternalBounce();
        }

        LockControl();
    }

    private Vector2 CalculateVerticalBounce(
        Vector2 incoming,
        Vector2 surfaceNormal)
    {
        Vector2 reflected = Vector2.Reflect(
            incoming,
            surfaceNormal.normalized
        );

        if (reflected.sqrMagnitude < 0.001f)
        {
            return surfaceNormal.normalized *
                   minimumVerticalBounceSpeed;
        }

        float speed = Mathf.Clamp(
            reflected.magnitude * verticalBounceMultiplier,
            minimumVerticalBounceSpeed,
            maximumBounceSpeed
        );

        return reflected.normalized * speed;
    }

    // --------------------------------------------------
    // CONTACT NORMAL
    // --------------------------------------------------

    private Vector2 GetSurfaceNormal(ContactPoint2D contact)
    {
        Vector2 normal = contact.normal.normalized;

        Vector2 toPlayer =
            rb.worldCenterOfMass - contact.point;

        if (Vector2.Dot(normal, toPlayer) < 0f)
        {
            normal = -normal;
        }

        return normal;
    }

    // --------------------------------------------------
    // CONTROL LOCK (SHELL BOUNCE ONLY)
    // --------------------------------------------------

    private void UpdateControlLock()
    {
        if (!controlLocked)
            return;

        float lockedTime = Time.time - controlLockStartTime;

        if (lockedTime >= controlLockDuration ||
            lockedTime >= maxControlLockDuration)
        {
            UnlockControl();
        }
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
        {
            mouseController.movementLocked = previousMovementLocked;
        }

        controlLocked = false;
    }

    // --------------------------------------------------
    // DAMAGE PROTECTION
    // --------------------------------------------------

    private void EndProtection()
    {
        isProtecting = false;
        hasBounced = false;
        protectionTimer = 0f;
    }

    // HealthNew เรียกก่อนหัก HP
    public bool TryBlockDamage()
    {
        return IsProtecting;
    }

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
            {
                icon.gameObject.SetActive(i < remainingUses);
            }
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