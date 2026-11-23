using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class HealthNew : MonoBehaviour
{
    [Header("Show in inspector")]
    [SerializeField] private Vector2 speedLinear;
    [SerializeField] private float speedMagnitude;

    [Header("HP")]

    public float maxHP = 100f;

    public float currentHP = 100f;

    private bool isDead;

    public bool IsDead => isDead;

    public float CurrentHP => currentHP;

    public float MaxHP => maxHP;

    [Header("Collision Damage")]

    [SerializeField] private LayerMask ignoreDamageLayers;

    public float safeImpactSpeed = 30f;

    [SerializeField] private float damagePerSpeed = 2f;

    [SerializeField] private float maxCollisionDamage = 20f;

    // ดาเมจที่ต่ำกว่าค่านี้ถือเป็นแค่การกระทบเบา ๆ จะไม่หัก HP
    [SerializeField] private float minCollisionDamage = 1f;

    [Header("Wall Collision")]

    [SerializeField] private float minVerticalSpeedForWallDamage = 1f;

    [SerializeField] private bool illnessEnabled = true;

    // ดาเมจต่อวินาทีที่อยู่นอก SaveZone
    [SerializeField] private float illnessDamagePerSecond = 1f;

    // หัก HP ทุกกี่วินาที
    [SerializeField] private float illnessTickInterval = 1f;

    // true = ถ้ากระดองกำลังป้องกันอยู่ (Space) จะกันดาเมจจากอาการป่วยด้วย
    [SerializeField] private bool shellBlocksIllness = false;

    // true = เล่นเสียงโดนดาเมจทุกครั้งที่หัก HP จากอาการป่วย
    [SerializeField] private bool illnessPlaysDamageSound = false;

    // true = จอแดงกะพริบ (Red Vignette) ทุกครั้งที่หัก HP จากอาการป่วย
    [SerializeField] private bool illnessFlashesScreen = true;

    private float illnessTimer;
    private Collider2D bodyCollider;

    [Header("Damage Stage Effects (index = stage 0..3)")]
    [Tooltip("สำรองค่าตัวคูณความเร็วเดินจาก Inspector (PlayerController เวอร์ชันนี้ยังไม่มีระบบเดินให้ปรับ)")]
    [SerializeField] private float[] stageMoveSpeedMultiplier = { 1f, 1f, 0.8f, 0.8f };

    [Tooltip("ตัวคูณแรงกระโดด ตามระดับความเสียหาย (ความสูงกระโดดลดลงตามกำลังสองของค่านี้)")]
    [SerializeField] private float[] stageJumpPowerMultiplier = { 1f, 1f, 0.85f, 0.85f };

    [Header("Shell HP UI")]
    [SerializeField] private Image shellImage;
    [SerializeField] private Sprite fullShell;
    [SerializeField] private Sprite crackedShell;
    [SerializeField] private Sprite brokenShell2;
    [SerializeField] private Sprite brokenShell1;

    [Header("Death Fade")]
    [Tooltip("CanvasGroup ของ Image สีดำเต็มจอ (เว้นว่าง = ไม่ fade)")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float blackHoldDuration = 0.15f;
    [SerializeField] private float fadeInDuration = 0.4f;

    [Header("Wall Bounce")]
    [SerializeField] private int bouncePosX;
    [SerializeField] private int bouncePosY;
    [SerializeField] private float bounceFloorPower;
    [SerializeField] private float bounceWallPower;


    private float crackAtDamagePercent = 10f;
    private float breakOneSectionAtPercent = 33f;
    private float breakTwoSectionsAtPercent = 66f;

    private ShellDefense shellDefense;

    private Rigidbody2D rb;

    private PlayerController playerController;

    [Header("Shell Break Effect")]

    [SerializeField] private int shellBreakShardCount = 12;

    [SerializeField] private float shellBreakShardSpeed = 180f;

    [SerializeField] private float shellBreakEffectDuration = 0.45f;

    private int currentShellStage = -1;

    private static Sprite shellShardSprite;

    [SerializeField] private float respawnDelay = 0.6f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        shellDefense = GetComponent<ShellDefense>();
        playerController = GetComponent<PlayerController>();

        bodyCollider = GetComponent<Collider2D>();

        if (bodyCollider == null)
        {
            bodyCollider = GetComponentInChildren<Collider2D>();
        }

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }

        maxHP = Mathf.Max(1f, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        safeImpactSpeed = Mathf.Max(0f, safeImpactSpeed);
        damagePerSpeed = Mathf.Max(0f, damagePerSpeed);
        maxCollisionDamage = Mathf.Max(0f, maxCollisionDamage);
        minCollisionDamage = Mathf.Max(0f, minCollisionDamage);
        illnessDamagePerSecond = Mathf.Max(0f, illnessDamagePerSecond);
        illnessTickInterval = Mathf.Max(0.05f, illnessTickInterval);
        minVerticalSpeedForWallDamage = Mathf.Max(0f, minVerticalSpeedForWallDamage);

        crackAtDamagePercent = Mathf.Clamp(crackAtDamagePercent, 0f, 100f);
        breakOneSectionAtPercent = Mathf.Clamp(breakOneSectionAtPercent, crackAtDamagePercent, 100f);
        breakTwoSectionsAtPercent = Mathf.Clamp(breakTwoSectionsAtPercent, breakOneSectionAtPercent, 100f);

        //UpdateShellUI();

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Die();
        }

        speedLinear = rb.linearVelocity;
        speedMagnitude = rb.linearVelocity.magnitude;
        UpdateIllness();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead)
        {
            return;
        }

        // ยกเว้น Layer ที่ไม่ต้องการคิดดาเมจ
        int otherLayer = collision.gameObject.layer;

        if ((ignoreDamageLayers.value & (1 << otherLayer)) != 0)
        {
            return;
        }

        bool hitFloor = false;
        bool hitCeiling = false;
        bool hitLeftWall = false;
        bool hitRightWall = false;

        // ตรวจทิศทางการชน
        foreach (ContactPoint2D contact in collision.contacts)
        {

            contacts.Add(contact); // เอาไว้ดูจุดชน ( OnDrawGizmo อยู่ด้านล่างสุด )
            Vector2 normal = GetNormalTowardsPlayer(contact);

            if (normal.y > 0.5f)
            {
                //Debug.Log("Hit Floor");
                hitFloor = true;
            }
            else if (normal.y < -0.5f)
            {
                //Debug.Log("Hit Ceiling");
                hitCeiling = true;
            }
            else if (normal.x > 0.5f)
            {
                //Debug.Log("Hit Wall Left");
                hitLeftWall = true;
            }
            else if (normal.x < -0.5f)
            {
                //Debug.Log("Hit Wall Right");
                hitRightWall = true;
            }
        }

        Vector2 impactVelocity = collision.relativeVelocity;

        float impactSpeed;
        float impactSpeedMagnitude = Mathf.Abs(impactVelocity.magnitude);

        // Bounce Section
        //Debug.Log($"{impactVelocity}");
        if (hitFloor && impactSpeedMagnitude > safeImpactSpeed) // Floor Bounce
        {
            //Debug.Log("Bounce Up");
            bouncePosX = 0;
            bouncePosY = 1;
        }
        else if (hitLeftWall && impactSpeedMagnitude > safeImpactSpeed) // WallLeft Bounce
        {
            //Debug.Log("Bounce Right");
            bouncePosX = 1;
            bouncePosY = 0;
        }
        else if (hitRightWall && impactSpeedMagnitude > safeImpactSpeed) // WallRight Bounce
        {
            //Debug.Log("Bounce Left");
            bouncePosX = -1;
            bouncePosY = 0;
        }
        else
        {
            //Debug.Log("No Bounce");
            bouncePosX = 0;
            bouncePosY = 0;
        }

        // ทำให้ player เด้งเมื่อ isInShell
        if (playerController.isInShell)
        {
            playerController.canAirJump = true;
            ShellBounce();
        }

        // Take Damage Section
        // ชนพื้น
        if (hitFloor)
        {
            impactSpeed = Mathf.Abs(impactVelocity.y);
        }
        // ชนเพดาน
        else if (hitCeiling)
        {
            impactSpeed = Mathf.Abs(impactVelocity.y);
        }
        // ชนกำแพง
        else if (hitLeftWall)
        {
            if (Mathf.Abs(impactVelocity.y) < minVerticalSpeedForWallDamage)
            {
                return;
            }

            impactSpeed = Mathf.Abs(impactVelocity.x);
        }
        else if (hitRightWall)
        {
            if (Mathf.Abs(impactVelocity.y) < minVerticalSpeedForWallDamage)
            {
                return;
            }

            impactSpeed = Mathf.Abs(impactVelocity.x);
        }
        else
        {
            return;
        }

        // ความเร็วที่ปลอดภัยไม่ทำให้เสีย HP
        if (impactSpeed <= safeImpactSpeed)
        {
            return;
        }

        float excessSpeed = impactSpeedMagnitude - safeImpactSpeed;

        float damage = Mathf.Min(excessSpeed * damagePerSpeed, maxCollisionDamage);

        // กระทบเบา ๆ ไม่ถือว่าเป็นความเสียหาย
        if (damage < minCollisionDamage)
        {
            return;
        }

        TakeDamage(damage);
    }

    private Vector2 GetNormalTowardsPlayer(ContactPoint2D contact)
    {
        Vector2 normal = contact.normal.normalized;
        Vector2 toPlayer = rb.worldCenterOfMass - contact.point;

        if (Vector2.Dot(normal, toPlayer) < 0f)
        {
            normal = -normal;
        }

        return normal;
    }

    private void ShellBounce()
    {
        Vector2 bounceForce = new Vector2(bouncePosX * bounceWallPower, bouncePosY * bounceFloorPower);
        rb.linearVelocity = bounceForce;
    }

    // ดาเมจจากการชน (กระดองป้องกันได้)
    public void TakeDamage(float damage)
    {
        ApplyDamage(damage, true, true, true);
    }

    private void ApplyDamage(
        float damage,
        bool canBeBlockedByShell,
        bool playSound,
        bool flashScreen)
    {
        if (isDead || damage <= 0f)
        {
            return;
        }

        // ตรวจสอบระบบป้องกันดาเมจก่อนหัก HP
        if (canBeBlockedByShell && shellDefense != null && shellDefense.TryBlockDamage())
        {
            return;
        }

        currentHP = Mathf.Clamp(currentHP - damage, 0f, maxHP);

        if (playSound)
        {
            GameAudio.Instance?.PlayDamage();
        }

        // Red Vignette: แถบแดง 4 ด้านรอบขอบจอ จางหายใน 0.5 วินาที
        if (flashScreen)
        {
            DamageVignette.Flash();
        }

        //UpdateShellUI();

        Debug.Log($"HP: {currentHP:F1} / {maxHP:F1}");

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    // อาการป่วย: อยู่นอก SaveZone จะเสีย HP เป็นรอบ ๆ
    private void UpdateIllness()
    {
        if (!illnessEnabled || isDead)
        {
            illnessTimer = 0f;
            return;
        }

        Vector2 bodyPoint = bodyCollider != null
            ? (Vector2)bodyCollider.bounds.center
            : (Vector2)transform.position;

        // อยู่ใน SaveZone = ปลอดภัย รีเซ็ตตัวนับ
        if (SaveZone.IsPointInsideAnyZone(bodyPoint))
        {
            illnessTimer = 0f;
            return;
        }

        illnessTimer += Time.deltaTime;

        if (illnessTimer < illnessTickInterval)
        {
            return;
        }

        illnessTimer -= illnessTickInterval;

        ApplyDamage(
            illnessDamagePerSecond * illnessTickInterval,
            shellBlocksIllness,
            illnessPlaysDamageSound,
            illnessFlashesScreen
        );
    }

    /*
    // ระดับความเสียหาย: 0 ปกติ, 1 เริ่มเจ็บ (รอยร้าว), 2 เจ็บมากขึ้น (ช้าลง/กระโดดต่ำ),
    // 3 เจ็บมาก (ม่านดำ), 4 ตาย  ใช้เกณฑ์เดียวกับรูปกระดองใน UpdateShellUI
    public int DamageStage => ComputeDamageStage();
    private int ComputeDamageStage()
    {
        if (currentHP <= 0f)
        {
            return 4;
        }

        float damagePercent = (maxHP - currentHP) / maxHP * 100f;

        if (damagePercent >= breakTwoSectionsAtPercent)
        {
            return 3;
        }

        if (damagePercent >= breakOneSectionAtPercent)
        {
            return 2;
        }

        if (damagePercent >= crackAtDamagePercent)
        {
            return 1;
        }

        return 0;
    }
    private static float GetStageValue(float[] values, int stage)
    {
        if (values == null || values.Length == 0)
        {
            return 1f;
        }

        return values[Mathf.Clamp(stage, 0, values.Length - 1)];
    }

    // รอยร้าว/ม่านดำบนจอ + ความเร็วเดิน/แรงกระโดดตามระดับความเสียหาย
    private void ApplyDamageStageEffects()
    {
        int stage = Mathf.Min(ComputeDamageStage(), 3);
        if (playerController != null)
        {
            playerController.jumpPowerMultiplier =
                GetStageValue(stageJumpPowerMultiplier, stage);
        }
    }
    
    private void UpdateShellUI()
    {
        // เอฟเฟกต์ตามระดับความเสียหาย ทำงานแม้ไม่ได้ผูก shellImage
        ApplyDamageStageEffects();

        if (shellImage == null)
        {
            return;
        }

        float damagePercent = (maxHP - currentHP) / maxHP * 100f;

        int newStage;
        Sprite targetSprite;

        // HP หมด
        if (currentHP <= 0f)
        {
            newStage = 4;
            targetSprite = null;
        }
        // แตกสองส่วน
        else if (damagePercent >= breakTwoSectionsAtPercent)
        {
            newStage = 3;
            targetSprite = brokenShell1;
        }
        // แตกหนึ่งส่วน
        else if (damagePercent >= breakOneSectionAtPercent)
        {
            newStage = 2;
            targetSprite = brokenShell2;
        }
        // เริ่มร้าว
        else if (damagePercent >= crackAtDamagePercent)
        {
            newStage = 1;
            targetSprite = crackedShell;
        }
        // กระดองเต็ม
        else
        {
            newStage = 0;
            targetSprite = fullShell;
        }

        // เล่นเอฟเฟกต์เฉพาะเมื่อระดับความเสียหายเพิ่มขึ้น
        if (currentShellStage >= 0 && newStage > currentShellStage)
        {
            SpawnShellBreakEffect(newStage);
        }

        currentShellStage = newStage;

        shellImage.sprite = targetSprite;
        shellImage.enabled = targetSprite != null;
    }
    */

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        currentHP = Mathf.Clamp(currentHP + amount,0f,maxHP);

        //UpdateShellUI();
    }

    public void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHP = 0f;
        GameAudio.Instance?.PlayDeath();
        //UpdateShellUI();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (playerController != null)
        {
            playerController.movementLocked = true;
        }

        StartCoroutine(RespawnAfterDelay());
    }

    public void DieFromHazard()
    {
        if (isDead)
        {
            return;
        }

        currentHP = 0f;

        Die();
    }


    private IEnumerator RespawnAfterDelay()
    {
        // รอให้เห็นฉากตอนตายสักครู่ก่อนเริ่ม fade
        yield return new WaitForSecondsRealtime(respawnDelay);

        // 1) จอค่อยๆ ดำ
        yield return FadeTo(1f, fadeOutDuration);

        // 2) เกิดใหม่ตอนจอดำ ผู้เล่นจะไม่เห็นการวาร์ป
        Checkpoint.RespawnPlayer(transform);

        yield return new WaitForSecondsRealtime(blackHoldDuration);

        // 3) จอค่อยๆ สว่างกลับมา
        yield return FadeTo(0f, fadeInDuration);
    }


    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeGroup == null)
            yield break;

        fadeGroup.blocksRaycasts = true;

        float startAlpha = fadeGroup.alpha;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(
                startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        fadeGroup.alpha = targetAlpha;
        fadeGroup.blocksRaycasts = targetAlpha > 0.5f;
    }

    public void ResetAfterRespawn()
    {
        currentHP = maxHP;
        isDead = false;
        illnessTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (playerController != null)
        {
            playerController.movementLocked = false;
            playerController.ResetAfterRespawn();
        }

        //UpdateShellUI();
    }


    public void ResetHealth()
    {
        ResetAfterRespawn();
    }

    /*
    private void SpawnShellBreakEffect(int stage)
    {
        RectTransform shellRect = shellImage.rectTransform;
        RectTransform parent = shellRect.parent as RectTransform;

        if (parent == null)
        {
            return;
        }

        Sprite shardSprite = GetShellShardSprite();

        UnityEngine.Color shardColor = stage >= 3
            ? new UnityEngine.Color(0.75f, 0.32f, 0.24f, 1f)
            : new UnityEngine.Color(0.45f, 0.72f, 0.38f, 1f);

        for (int i = 0; i < shellBreakShardCount; i++)
        {
            GameObject shard = new GameObject(
                "ShellBreakShard",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            shard.transform.SetParent(parent, false);
            shard.transform.SetAsLastSibling();

            RectTransform shardRect = shard.GetComponent<RectTransform>();
            Image shardImage = shard.GetComponent<Image>();

            shardRect.anchorMin = shellRect.anchorMin;
            shardRect.anchorMax = shellRect.anchorMax;
            shardRect.pivot = shellRect.pivot;

            shardRect.anchoredPosition =
                shellRect.anchoredPosition + Random.insideUnitCircle * 12f;

            shardRect.sizeDelta = new Vector2(
                Random.Range(4f, 9f),
                Random.Range(7f, 15f)
            );

            shardRect.localRotation =
                Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            shardRect.localScale = Vector3.one;

            shardImage.sprite = shardSprite;
            shardImage.raycastTarget = false;

            // สลับเศษสีทองกับสีของกระดอง
            shardImage.color = Random.value > 0.5f
                ? new UnityEngine.Color(0.82f, 0.66f, 0.38f, 1f)
                : shardColor;

            Vector2 direction = Random.insideUnitCircle.normalized;

            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector2.up;
            }

            Vector2 velocity = direction * Random.Range(
                shellBreakShardSpeed * 0.6f,
                shellBreakShardSpeed * 1.3f
            );

            float rotationSpeed = Random.Range(-500f, 500f);

            StartCoroutine(AnimateShellShard(
                shardRect,
                shardImage,
                velocity,
                rotationSpeed
            ));
        }
    }
    

    private Sprite GetShellShardSprite()
    {
        if (shellShardSprite != null)
        {
            return shellShardSprite;
        }

        Texture2D texture = new Texture2D(
            1,
            1,
            TextureFormat.RGBA32,
            false
        );

        texture.name = "ShellShardTexture";
        texture.SetPixel(0, 0, UnityEngine.Color.white);
        texture.Apply();

        shellShardSprite = Sprite.Create(
            texture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            100f
        );

        shellShardSprite.name = "ShellShardSprite";

        return shellShardSprite;
    }


    private IEnumerator AnimateShellShard(
        RectTransform shardRect,
        Image shardImage,
        Vector2 velocity,
        float rotationSpeed)
    {
        float elapsed = 0f;

        float duration = Mathf.Max(0.05f, shellBreakEffectDuration);

        UnityEngine.Color startColor = shardImage.color;

        while (elapsed < duration && shardRect != null && shardImage != null)
        {
            float deltaTime = Time.unscaledDeltaTime;

            elapsed += deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);

            // กระจายออกแล้วค่อย ๆ ตกลง
            velocity += Vector2.down * 260f * deltaTime;

            shardRect.anchoredPosition += velocity * deltaTime;

            shardRect.Rotate(0f, 0f, rotationSpeed * deltaTime);

            UnityEngine.Color color = startColor;
            color.a = 1f - progress;

            shardImage.color = color;

            yield return null;
        }

        if (shardRect != null)
        {
            Destroy(shardRect.gameObject);
        }
    }
    */

    private List<ContactPoint2D> contacts = new List<ContactPoint2D>();
    private void OnDrawGizmos()
    {
        foreach (ContactPoint2D contact in contacts)
        {
            Vector2 normal = GetNormalTowardsPlayer(contact);

            // จุดสัมผัส
            Gizmos.color = UnityEngine.Color.red;
            Gizmos.DrawSphere(contact.point, 0.08f);

            // ทิศทาง Normal
            //Gizmos.color = UnityEngine.Color.green;
            //Gizmos.DrawLine(
            //    contact.point,
            //    contact.point + normal * 0.5f
            //);
        }
    }
}