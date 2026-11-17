using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// Turtle Shell Bounce
// กด Space ตอนกำลังตก/พุ่งชน -> เปิดการป้องกันดาเมจ ถ้าชนภายในเวลาที่กำหนดจะสะท้อนออกจากผิว
// ใช้ได้จนกว่า Shell จะหมด
public class ShellDefense : MonoBehaviour
{
    [Header("Shell Charges")]
    // จำนวน Shell ตอนเริ่มเกม เช่น 3 = 3/3
    [SerializeField] private int maxShellUses = 3;


    [Header("Protection")]
    // เวลาที่รอให้ชนหลังกด Space
    // หากหมดเวลาก่อนชน จะเสีย Shell ไปโดยไม่ได้เด้ง
    [SerializeField] private float protectionDuration = 0.75f;


    [Header("Bounce Settings")]
    // ตัวคูณความเร็วเด้ง (1.1 = ใช้ความเร็วขาเข้า x 1.1 ก่อนจำกัดความเร็ว)
    [SerializeField] private float bounceMultiplier = 1.1f;

    // ความเร็วเด้งขั้นต่ำ เพิ่มค่านี้หากรู้สึกว่าเด้งเบาเกินไป
    [SerializeField] private float minimumBounceSpeed = 16f;

    // ความเร็วเด้งสูงสุด ลดค่านี้หากรู้สึกว่าเด้งแรงเกินไป
    [SerializeField] private float maximumBounceSpeed = 22f;

    // เวลาขั้นต่ำที่ล็อกการเดินของ MouseController หลังเด้ง (วินาที)
    // หลังจากนั้นจะล็อกต่อจนกว่าจะลงพื้น กันไม่ให้ระบบเดินเขียนทับความเร็วเด้ง
    [SerializeField] private float controlLockDuration = 0.12f;

    // เวลาล็อกสูงสุด กันกรณีที่ไม่เคยแตะพื้น
    [SerializeField] private float maxControlLockDuration = 2f;

    // ช่วงเวลา (วินาที) ที่จำความเร็วก่อนชนไว้ใช้คำนวณมุมสะท้อน
    // ทำให้กด Space หลังชนเล็กน้อย (ขณะยังแนบผิวอยู่) ก็ยังสะท้อนตามมุมที่พุ่งเข้ามาจริง
    [SerializeField] private float impactMemoryTime = 0.3f;


    [Header("Debug")]
    // เปิดเพื่อพิมพ์ความเร็วขาเข้า / normal / ความเร็วขาออก ลง Console ทุกครั้งที่เด้ง
    [SerializeField] private bool debugBounce = false;


    [Header("Activation")]
    // ความเร็วขั้นต่ำที่ต้องมีจึงจะกด Space ใช้ Shell ได้
    // กันการเสีย Shell ตอนยืนนิ่ง ๆ
    [SerializeField] private float minimumMovementToActivate = 0.5f;


    [Header("Shell UI Icons")]
    [SerializeField] private Image[] shellUseIcons = new Image[3];

    private Rigidbody2D rb;
    private MouseController mouseController;
    private HealthNew health;

    private int remainingUses;
    private float protectionTimer;

    private bool isProtecting;
    private bool hasBounced;

    // ความเร็วล่าสุดตอนที่ยังไม่แตะอะไรเลย (ใช้เป็นความเร็วขาเข้าคำนวณมุมสะท้อน)
    private Vector2 approachVelocity;
    private float approachTime;

    // Collider ที่กำลังสัมผัสอยู่ ใช้บอกว่าตอนนี้ "ยังไม่ชน" หรือไม่
    private readonly System.Collections.Generic.HashSet<Collider2D> touching =
        new System.Collections.Generic.HashSet<Collider2D>();

    private Coroutine endProtectionCoroutine;

    private bool controlLocked;
    private float controlLockStartTime;

    public int RemainingUses => remainingUses;
    public int MaxShellUses => maxShellUses;
    public bool IsProtecting => isProtecting;

    private void Awake()
    {
        maxShellUses = Mathf.Max(0, maxShellUses);
        protectionDuration = Mathf.Max(0.05f, protectionDuration);

        bounceMultiplier = Mathf.Max(0f, bounceMultiplier);
        minimumBounceSpeed = Mathf.Max(0f, minimumBounceSpeed);
        maximumBounceSpeed = Mathf.Max(minimumBounceSpeed, maximumBounceSpeed);
        maxControlLockDuration = Mathf.Max(controlLockDuration, maxControlLockDuration);

        remainingUses = maxShellUses;

        rb = GetComponent<Rigidbody2D>();
        mouseController = GetComponent<MouseController>();
        health = GetComponent<HealthNew>();

        if (rb != null)
        {
            approachVelocity = rb.linearVelocity;
            approachTime = Time.time;
        }

        UpdateShellIcons();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            ActivateShellBounce();

        if (isProtecting)
        {
            protectionTimer -= Time.deltaTime;

            if (protectionTimer <= 0f)
                EndProtection();
        }

        // ปลดล็อกการเดินเมื่อลงพื้น (หลังพ้นเวลาขั้นต่ำ) หรือครบเวลาสูงสุด
        if (controlLocked)
        {
            float lockedFor = Time.time - controlLockStartTime;

            if (lockedFor >= maxControlLockDuration ||
                (lockedFor >= controlLockDuration && mouseController != null && mouseController.isGrounded))
            {
                UnlockControl();
            }
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        touching.RemoveWhere(col => col == null || !col.enabled);

        // บันทึกความเร็วเฉพาะตอนที่ยังไม่ได้สัมผัสอะไร
        // พอชนแล้ว Physics จะหักล้างความเร็วทิ้ง จึงต้องจำค่าก่อนชนไว้ใช้
        if (touching.Count == 0)
        {
            approachVelocity = rb.linearVelocity;
            approachTime = Time.time;
        }
    }

    private void ActivateShellBounce()
    {
        if (remainingUses <= 0 || rb == null || isProtecting)
            return;

        // ไม่ใช้ Shell หากแทบไม่เคลื่อนที่
        if (rb.linearVelocity.magnitude < minimumMovementToActivate)
            return;

        remainingUses--;

        isProtecting = true;
        hasBounced = false;
        protectionTimer = protectionDuration;

        UpdateShellIcons();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider != null)
            touching.Add(collision.collider);

        TryBounce(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.collider != null)
            touching.Add(collision.collider);

        TryBounce(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider != null)
            touching.Remove(collision.collider);
    }

    // ให้ normal ชี้ออกจากผิวมาทางผู้เล่นเสมอ
    private Vector2 GetNormalTowardsPlayer(ContactPoint2D contact)
    {
        Vector2 normal = contact.normal.normalized;
        Vector2 directionToPlayer = rb.worldCenterOfMass - contact.point;

        if (Vector2.Dot(normal, directionToPlayer) < 0f)
            normal = -normal;

        return normal;
    }

    private void TryBounce(Collision2D collision)
    {
        if (!isProtecting || hasBounced || rb == null)
            return;

        if (collision.contactCount == 0)
            return;

        // ใช้ความเร็วก่อนชนที่จำไว้ ถ้าเพิ่งชนไม่นาน (impactMemoryTime)
        // ถ้าแนบผิวมานานแล้ว ใช้ความเร็วปัจจุบันแทน
        Vector2 incomingVelocity =
            (Time.time - approachTime <= impactMemoryTime)
                ? approachVelocity
                : rb.linearVelocity;
        Vector2 bestNormal = Vector2.zero;
        float bestImpact = float.NegativeInfinity;

        // เลือกผิวที่ชนแรงที่สุดเป็นตัวสะท้อน
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = GetNormalTowardsPlayer(collision.GetContact(i));
            float impact = -Vector2.Dot(incomingVelocity, normal);

            if (impact > bestImpact)
            {
                bestImpact = impact;
                bestNormal = normal;
            }
        }

        if (bestNormal == Vector2.zero)
            return;

        // แทบไม่มีความเร็วเข้าหาผิว -> ผลักออกจากผิวตรง ๆ
        if (incomingVelocity.sqrMagnitude < 0.01f ||
            Vector2.Dot(incomingVelocity, bestNormal) >= -0.1f)
        {
            incomingVelocity = -bestNormal * minimumBounceSpeed;
        }

        // สะท้อนตามมุมชนจริง: เพดานเด้งลง, พื้นเด้งขึ้น, ผนังเด้งกลับ
        Vector2 reflectedDirection = Vector2.Reflect(
            incomingVelocity.normalized,
            bestNormal
        ).normalized;

        float launchSpeed = Mathf.Clamp(
            incomingVelocity.magnitude * bounceMultiplier,
            minimumBounceSpeed,
            maximumBounceSpeed
        );

        if (debugBounce)
        {
            Debug.Log(
                $"[ShellDefense] incoming={incomingVelocity} normal={bestNormal} " +
                $"outgoing={reflectedDirection * launchSpeed} surface={collision.collider.name}"
            );
        }

        // ดันออกจากผิวเล็กน้อยกันตัวละครจมผิว
        rb.position += bestNormal * 0.03f;
        rb.linearVelocity = reflectedDirection * launchSpeed;

        hasBounced = true;

        LockControl();

        // ให้ HealthPoint มีเวลาตรวจ TryBlockDamage ในรอบชนนี้ก่อนปิดการป้องกัน
        if (endProtectionCoroutine != null)
            StopCoroutine(endProtectionCoroutine);

        endProtectionCoroutine = StartCoroutine(EndProtectionAfterCollision());
    }

    private IEnumerator EndProtectionAfterCollision()
    {
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

    // ล็อกเฉพาะการเดิน (Move) ของ MouseController ไม่ปิดทั้งสคริปต์
    // เพื่อให้ ground check และ jump state ยังทำงานต่อได้ตามปกติ
    private void LockControl()
    {
        if (mouseController == null)
            return;

        mouseController.movementLocked = true;
        controlLocked = true;
        controlLockStartTime = Time.time;
    }

    private void UnlockControl()
    {
        // ถ้าตายอยู่ HealthNew เป็นคนล็อกการเดิน ห้ามปลดล็อกแทน
        if (mouseController != null && (health == null || !health.IsDead))
            mouseController.movementLocked = false;

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

            if (icon == null)
                continue;

            icon.gameObject.SetActive(i < remainingUses);
        }
    }

    private void OnDisable()
    {
        UnlockControl();
        touching.Clear();

        if (endProtectionCoroutine != null)
        {
            StopCoroutine(endProtectionCoroutine);
            endProtectionCoroutine = null;
        }

        isProtecting = false;
    }
}
