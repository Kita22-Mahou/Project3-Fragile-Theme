using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ShellWallBounce : MonoBehaviour
{
    [Header("Shell Charges")]
    // จำนวน Shell สูงสุดที่ใช้ได้
    [SerializeField] private int maxShellUses = 3;
    [SerializeField] private Image[] shellUseIcons = new Image[3];

    [Header("Protection")]
    // เวลาที่รอให้ชนผนังหลังจากกด Space
    [SerializeField] private float protectionDuration = 0.75f;

    [Header("Horizontal Bounce")]
    // ตัวคูณแรงเด้งออกจากผนัง
    [SerializeField] private float horizontalBounceMultiplier = 1.63f;

    // ความเร็วแนวนอนขั้นต่ำหลังเด้ง
    [SerializeField] private float minimumHorizontalBounceSpeed = 12f;

    [Header("Vertical Bounce")]
    // ความสูงเป้าหมายเมื่อกำลังตกชนผนัง หน่วย Unity
    [SerializeField] private float upwardBounceHeight = 3f;

    // ตัวคูณความเร็วขึ้นจากการเคลื่อนที่เดิม
    [SerializeField] private float upwardBounceMultiplier = 1.1f;

    [Header("Speed Limit")]
    // จำกัดความเร็วรวมหลังเด้ง
    [SerializeField] private float minimumBounceSpeed = 20f;
    [SerializeField] private float maximumBounceSpeed = 30f;

    [Header("Wall Detection")]
    // ตรวจจับเฉพาะการชนผนังด้านข้าง
    [Range(0.1f, 1f)]
    [SerializeField] private float wallNormalThreshold = 0.7f;

    [Tooltip("เลือก Layer ของผนังที่ให้เด้งได้")]
    [SerializeField] private LayerMask bounceableLayers = ~0;

    [Tooltip("ความเร็วขั้นต่ำที่ต้องพุ่งเข้าหาผนัง")]
    [SerializeField] private float minimumWallImpactSpeed = 2.35f;

    [Header("Control Lock")]
    // ล็อกการเดินชั่วคราวหลังเด้ง
    [SerializeField] private float controlLockDuration = 0.12f;

    // ปลดล็อกอย่างช้าที่สุดหลังเวลานี้
    [SerializeField] private float maxControlLockDuration = 2f;

    private Rigidbody2D rb;
    private MouseController mouseController;

    private int remainingUses;
    private float protectionTimer;

    private bool isProtecting;
    private bool hasBounced;
    private bool controlLocked;
    private bool previousMovementLocked;

    private float controlLockStartTime;
    private Vector2 velocityBeforePhysics;

    private Coroutine endProtectionCoroutine;

    public int RemainingUses => remainingUses;
    public int MaxShellUses => maxShellUses;
    public bool IsProtecting => isProtecting;

    private void Awake()
    {
        maxShellUses = Mathf.Max(0, maxShellUses);
        protectionDuration = Mathf.Max(0.05f, protectionDuration);

        horizontalBounceMultiplier =
            Mathf.Max(0f, horizontalBounceMultiplier);

        upwardBounceMultiplier =
            Mathf.Max(0f, upwardBounceMultiplier);

        minimumHorizontalBounceSpeed =
            Mathf.Max(0f, minimumHorizontalBounceSpeed);

        upwardBounceHeight = Mathf.Max(0f, upwardBounceHeight);

        minimumBounceSpeed = Mathf.Max(0f, minimumBounceSpeed);

        maximumBounceSpeed = Mathf.Max(
            minimumBounceSpeed,
            maximumBounceSpeed
        );

        maxControlLockDuration = Mathf.Max(
            controlLockDuration,
            maxControlLockDuration
        );

        remainingUses = maxShellUses;

        rb = GetComponent<Rigidbody2D>();
        mouseController = GetComponent<MouseController>();

        if (rb != null)
            velocityBeforePhysics = rb.linearVelocity;

        UpdateShellIcons();
    }

    private void Update()
    {
        // กด Space หนึ่งครั้งเพื่อเตรียมเด้ง
        if (Input.GetKeyDown(KeyCode.Space))
            ActivateShell();

        // หมดเวลารอชนแล้ว ยกเลิกการป้องกัน
        if (isProtecting)
        {
            protectionTimer -= Time.deltaTime;

            if (protectionTimer <= 0f)
                EndProtection();
        }

        // ปลดล็อกเมื่อกลับถึงพื้นหลังพ้นเวลาขั้นต่ำ
        // หรือเมื่อครบเวลาล็อกสูงสุด
        if (controlLocked)
        {
            float lockedTime = Time.time - controlLockStartTime;

            bool maxTimeReached =
                lockedTime >= maxControlLockDuration;

            bool canUnlockOnGround =
                lockedTime >= controlLockDuration &&
                mouseController != null &&
                mouseController.isGrounded;

            if (maxTimeReached || canUnlockOnGround)
                UnlockControl();
        }
    }

    private void FixedUpdate()
    {
        if (rb != null)
            velocityBeforePhysics = rb.linearVelocity;
    }

    private void ActivateShell()
    {
        if (rb == null || remainingUses <= 0 || isProtecting)
            return;

        // ไม่ใช้ Shell ตอนยืนนิ่งบนพื้น
        if (mouseController != null &&
            mouseController.isGrounded &&
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryBounceFromWall(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // รองรับกรณีกด Space ขณะสัมผัสผนังอยู่แล้ว
        TryBounceFromWall(collision);
    }

    private bool IsBounceableLayer(GameObject obj)
    {
        if (obj == null)
            return false;

        return (bounceableLayers.value &
                (1 << obj.layer)) != 0;
    }

    // ทำให้ Normal ชี้ออกจากผนังเข้าหาผู้เล่น
    private Vector2 GetWallNormal(ContactPoint2D contact)
    {
        Vector2 normal = contact.normal.normalized;

        Vector2 toPlayer =
            rb.worldCenterOfMass - contact.point;

        if (Vector2.Dot(normal, toPlayer) < 0f)
            normal = -normal;

        return normal;
    }

    private void TryBounceFromWall(Collision2D collision)
    {
        if (!isProtecting || hasBounced || rb == null)
            return;

        if (!IsBounceableLayer(collision.gameObject))
            return;

        if (collision.contactCount == 0)
            return;

        Vector2 incoming = velocityBeforePhysics;

        if (incoming.sqrMagnitude < 0.01f)
            incoming = rb.linearVelocity;

        Vector2 wallNormal = Vector2.zero;
        float strongestImpact = 0f;

        // เลือกจุดสัมผัสของผนังด้านข้างที่ชนจริง
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            Vector2 normal = GetWallNormal(contact);

            // ไม่รับพื้นด้านบนหรือเพดานด้านล่าง
            if (Mathf.Abs(normal.x) < wallNormalThreshold)
                continue;

            if (Mathf.Abs(normal.x) <= Mathf.Abs(normal.y))
                continue;

            // ต้องกำลังพุ่งเข้าหาผนัง
            float impact = -Vector2.Dot(incoming, normal);

            if (impact > strongestImpact)
            {
                strongestImpact = impact;
                wallNormal = normal;
            }
        }

        if (wallNormal == Vector2.zero ||
            strongestImpact < minimumWallImpactSpeed)
        {
            return;
        }

        // ---------------------------------------------
        // 1. คำนวณแรงเด้งแนวนอน
        // ทิศต้องออกจากผนังเสมอ
        // ---------------------------------------------

        float horizontalSpeed = Mathf.Max(
            Mathf.Abs(incoming.x) * horizontalBounceMultiplier,
            minimumHorizontalBounceSpeed
        );

        float horizontalDirection = Mathf.Sign(wallNormal.x);

        float outgoingX =
            horizontalDirection * horizontalSpeed;

        // ---------------------------------------------
        // 2. คำนวณแรงเด้งแนวดิ่ง
        // ---------------------------------------------

        float outgoingY;

        if (incoming.y < 0f)
        {
            // กำลังตกชนผนัง: ใช้ Gravity จริงคำนวณความเร็วขึ้น
            float gravity = Mathf.Abs(
                Physics2D.gravity.y * rb.gravityScale
            );

            // v = sqrt(2gh)
            float speedForTargetHeight = Mathf.Sqrt(
                2f * gravity * upwardBounceHeight
            );

            // เลือกแรงขึ้นที่เหมาะสมระหว่างแรงตกเดิมกับความสูงเป้าหมาย
            outgoingY = Mathf.Max(
                speedForTargetHeight,
                Mathf.Abs(incoming.y) * upwardBounceMultiplier
            );
        }
        else
        {
            // กำลังพุ่งขึ้นชนผนัง:
            // รักษาทิศขึ้นเดิมและเพิ่มความเร็วเล็กน้อย
            outgoingY = Mathf.Max(
                0f,
                incoming.y * upwardBounceMultiplier
            );
        }

        // ---------------------------------------------
        // 3. จำกัดความเร็วรวม
        // คงค่า Y ก่อน แล้วลด X เท่าที่จำเป็น
        // ---------------------------------------------

        outgoingY = Mathf.Min(
            outgoingY,
            maximumBounceSpeed
        );

        float allowedX = Mathf.Sqrt(
            Mathf.Max(
                0f,
                maximumBounceSpeed * maximumBounceSpeed
                    - outgoingY * outgoingY
            )
        );

        outgoingX = horizontalDirection * Mathf.Min(
            Mathf.Abs(outgoingX),
            allowedX
        );

        Vector2 outgoing = new Vector2(
            outgoingX,
            outgoingY
        );

        // ผลักออกจากผนังเล็กน้อย ป้องกันติด Collider
        rb.position += wallNormal * 0.04f;

        // ใช้ความเร็วใหม่ทั้ง X และ Y
        // ไม่เปลี่ยน Gravity Scale = 10
        rb.linearVelocity = outgoing;

        hasBounced = true;

        LockControl();

        if (endProtectionCoroutine != null)
            StopCoroutine(endProtectionCoroutine);

        endProtectionCoroutine =
            StartCoroutine(EndProtectionAfterCollision());
    }

    private IEnumerator EndProtectionAfterCollision()
    {
        // ให้ HealthPoint ตรวจสอบดาเมจก่อนสิ้นสุดการป้องกัน
        yield return new WaitForFixedUpdate();

        EndProtection();
    }

    private void EndProtection()
    {
        isProtecting = false;
        hasBounced = false;
        protectionTimer = 0f;

        endProtectionCoroutine = null;
    }

    private void LockControl()
    {
        if (mouseController == null || controlLocked)
            return;

        previousMovementLocked =
            mouseController.movementLocked;

        mouseController.movementLocked = true;

        controlLocked = true;
        controlLockStartTime = Time.time;
    }

    private void UnlockControl()
    {
        if (mouseController != null)
        {
            mouseController.movementLocked =
                previousMovementLocked;
        }

        controlLocked = false;
    }

    // HealthPoint เรียกก่อนหัก HP
    public bool TryBlockDamage()
    {
        return isProtecting;
    }

    // ใช้เติม Shell เมื่อถึง Checkpoint
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

        if (endProtectionCoroutine != null)
        {
            StopCoroutine(endProtectionCoroutine);
            endProtectionCoroutine = null;
        }

        isProtecting = false;
    }
}
