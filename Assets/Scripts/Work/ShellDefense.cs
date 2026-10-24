
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ShellDefense : MonoBehaviour
{
    [Header("Shell Charges")]
    // จำนวนครั้งสูงสุดที่ใช้ Shell Bounce ได้
    // เริ่มเกมจะมี Shell ตามจำนวนนี้ เช่น 3 = ใช้ได้ 3 ครั้ง
    [SerializeField] private int maxShellUses = 3;


    [Header("Protection")]
    // ระยะเวลาที่ระบบรอให้ผู้เล่นชนพื้นผิวหลังจากกด Space
    // หากหมดเวลาก่อนชน จะเสีย Shell ไปโดยไม่ได้เด้ง
    [SerializeField] private float protectionDuration = 0.75f;


    [Header("Bounce Settings")]
    // ตัวคูณความเร็วเด้ง ยิ่งมากยิ่งเด้งแรง
    // เช่น 1.1 = ใช้ความเร็วขาเข้าคูณ 1.1 ก่อนจำกัดความเร็ว
    [SerializeField] private float bounceMultiplier = 1.1f;

    // ความเร็วเด้งขั้นต่ำ เพื่อให้เด้งออกจากพื้นผิวได้ชัดเจน
    // เพิ่มค่านี้หากรู้สึกว่าเด้งเบาเกินไป
    [SerializeField] private float minimumBounceSpeed = 16f;

    // ความเร็วเด้งสูงสุด ป้องกันไม่ให้ตัวละครพุ่งเร็วเกินไป
    // ลดค่านี้หากรู้สึกว่าเด้งแรงหรือไกลเกินไป
    [SerializeField] private float maximumBounceSpeed = 22f;

    // สัดส่วนการเด้งขึ้นด้านบนขั้นต่ำ ตั้งแต่ 0 ถึง 1
    // 0.65 = บังคับให้ทิศเด้งมีองค์ประกอบขึ้นด้านบนอย่างน้อยตามสัดส่วนนี้
    // เพิ่มค่านี้เพื่อเน้นการเด้งขึ้น ลดค่านี้เพื่อให้ทิศทางแนวราบมีผลมากขึ้น
    [Range(0f, 1f)]
    [SerializeField] private float minimumUpwardRatio = 0.65f;

    // ระยะเวลาที่ปิด MouseController หลังเด้ง
    // ช่วยไม่ให้ระบบเดินเขียนทับความเร็วเด้งทันที
    // ลดค่านี้หากรู้สึกว่าควบคุมตัวละครกลับมาช้าเกินไป
    [SerializeField] private float controlLockDuration = 0.12f;


    [Header("Activation")]
    // ความเร็วขั้นต่ำที่ใช้แยกว่าตัวละครกำลังเคลื่อนที่หรือยืนนิ่ง
    // หากสัมผัสพื้นและความเร็วต่ำกว่าค่านี้ จะไม่ใช้ Shell
    // เพิ่มค่านี้เพื่อให้ต้องเคลื่อนที่เร็วขึ้นก่อนจึงใช้ Shell ได้
    [SerializeField] private float minimumMovementToActivate = 0.5f;

    [Header("Shell UI Icons")]
    [SerializeField] private Image[] shellUseIcons = new Image[3];

    private Rigidbody2D rb;
    private MouseController mouseController;

    private int remainingUses;
    private float protectionTimer;

    private bool isProtecting;
    private bool hasBounced;

    private Vector2 velocityBeforePhysics;

    private readonly System.Collections.Generic.HashSet<Collider2D>
        groundContacts =
            new System.Collections.Generic.HashSet<Collider2D>();

    private Coroutine controlLockCoroutine;
    private Coroutine endProtectionCoroutine;

    private bool controllerWasEnabled;

    public int RemainingUses => remainingUses;
    public int MaxShellUses => maxShellUses;
    public bool IsProtecting => isProtecting;

    private void Awake()
    {
        maxShellUses = Mathf.Max(0, maxShellUses);
        protectionDuration = Mathf.Max(0.05f, protectionDuration);

        bounceMultiplier = Mathf.Max(0f, bounceMultiplier);
        minimumBounceSpeed = Mathf.Max(0f, minimumBounceSpeed);

        maximumBounceSpeed = Mathf.Max(minimumBounceSpeed,maximumBounceSpeed);

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
            ActivateShellBounce();

        //ยกเลิกการป้องกัน
        if (isProtecting)
        {
            protectionTimer -= Time.deltaTime;

            if (protectionTimer <= 0f)
                EndProtection();
        }
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        velocityBeforePhysics = rb.linearVelocity;
    }

    private void ActivateShellBounce()
    {
        if (remainingUses <= 0 || rb == null || isProtecting)
            return;

        // ไม่ใช้ Shell หากยืนเฉย ๆ บนพื้น
        bool standingStill =
            HasGroundContact() &&
            rb.linearVelocity.magnitude < minimumMovementToActivate;

        if (standingStill)
            return;

        remainingUses--;

        isProtecting = true;
        hasBounced = false;
        protectionTimer = protectionDuration;

        UpdateShellIcons();
    }

    private bool HasGroundContact()
    {
        groundContacts.RemoveWhere(
            col => col == null || !col.enabled
        );

        return groundContacts.Count > 0;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TrackGroundContact(collision);
        TryBounce(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TrackGroundContact(collision);
        TryBounce(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider != null)
            groundContacts.Remove(collision.collider);
    }

    private Vector2 GetNormalTowardsPlayer(ContactPoint2D contact)
    {
        Vector2 normal = contact.normal.normalized;
        Vector2 directionToPlayer = rb.worldCenterOfMass - contact.point;

        if (Vector2.Dot(normal, directionToPlayer) < 0f) normal = -normal;

        return normal;
    }

    private void TrackGroundContact(Collision2D collision)
    {
        if (rb == null || collision.collider == null)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 normal = GetNormalTowardsPlayer(collision.GetContact(i));

            if (normal.y > 0.5f)
            {
                groundContacts.Add(collision.collider);
                return;
            }
        }
    }

    private void TryBounce(Collision2D collision)
    {
        if (!isProtecting || hasBounced || rb == null)
            return;

        if (collision.contactCount == 0)
            return;

        Vector2 incomingVelocity = velocityBeforePhysics;
        Vector2 bestNormal = Vector2.zero;
        float bestImpact = float.NegativeInfinity;

        // เลือกผิวที่เหมาะสมที่สุดสำหรับการสะท้อน
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            Vector2 normal = GetNormalTowardsPlayer(contact);

            float impact = -Vector2.Dot(incomingVelocity, normal);

            if (impact > bestImpact)
            {
                bestImpact = impact;
                bestNormal = normal;
            }
        }

        if (bestNormal == Vector2.zero)
            return;

        // กรณีแทบไม่มีความเร็ว ให้ผลักออกจากผิวโดยตรง
        if (incomingVelocity.sqrMagnitude < 0.01f ||
            Vector2.Dot(incomingVelocity, bestNormal) >= -0.1f)
        {
            incomingVelocity = -bestNormal * minimumBounceSpeed;
        }

        Vector2 reflectedDirection = Vector2.Reflect(
            incomingVelocity.normalized,
            bestNormal
        ).normalized;

        // ชนพื้นหรือกำแพงให้เด้งขึ้น
        // ชนใต้เพดานให้เด้งออกจากผิว
        if (bestNormal.y > -0.5f)
        {
            float upward = Mathf.Clamp01(Mathf.Max(Mathf.Abs(reflectedDirection.y),minimumUpwardRatio ));

            reflectedDirection = new Vector2(reflectedDirection.x,upward).normalized;
        }

        float launchSpeed = Mathf.Clamp(incomingVelocity.magnitude * bounceMultiplier, minimumBounceSpeed, maximumBounceSpeed);

        Vector2 outgoingVelocity = reflectedDirection * launchSpeed;

        rb.position += bestNormal * 0.03f;
        rb.linearVelocity = outgoingVelocity;

        hasBounced = true;

        LockControlBriefly();

        // ให้ HealthPoint มีเวลาตรวจป้องกันดาเมจในรอบชนนี้
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

    private void LockControlBriefly()
    {
        if (mouseController == null || !mouseController.enabled)
            return;

        controllerWasEnabled = true;
        mouseController.enabled = false;

        if (controlLockCoroutine != null)
            StopCoroutine(controlLockCoroutine);

        controlLockCoroutine = StartCoroutine(RestoreControlAfterBounce());
    }

    private IEnumerator RestoreControlAfterBounce()
    {
        yield return new WaitForSeconds(controlLockDuration);

        if (mouseController != null && controllerWasEnabled)
            mouseController.enabled = true;

        controllerWasEnabled = false;
        controlLockCoroutine = null;
    }

    // HealthPoint เรียกก่อนหัก HP
    public bool TryBlockDamage()
    {
        return isProtecting;
    }

    // ใช้เติม Shell เมื่อถึง Checkpoint ในอนาคต
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
        if (controlLockCoroutine != null)
        {
            StopCoroutine(controlLockCoroutine);
            controlLockCoroutine = null;

            if (mouseController != null && controllerWasEnabled)
                mouseController.enabled = true;

            controllerWasEnabled = false;
        }

        if (endProtectionCoroutine != null)
        {
            StopCoroutine(endProtectionCoroutine);
            endProtectionCoroutine = null;
        }

        isProtecting = false;
    }
}
