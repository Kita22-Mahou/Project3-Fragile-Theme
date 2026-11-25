using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D.Animation;

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

    [SerializeField] private float hazardDamage = 20;

    [SerializeField] private float damagePerSpeed = 2f;

    [SerializeField] private float maxCollisionDamage = 20f;

    // ดาเมจที่ต่ำกว่าค่านี้ถือเป็นแค่การกระทบเบา ๆ จะไม่หัก HP
    [SerializeField] private float minCollisionDamage = 1f;

    [SerializeField] private Transform particleSpawnPoint;

    [SerializeField] private GameObject lowHurt;
    [SerializeField] private GameObject midHurt;
    [SerializeField] private GameObject highHurt;

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

    [Header("Shell HP UI (5 Phases)")]
    [SerializeField] private Image shellImage;

    [Tooltip("Phase 1: UI_Shell (กระดองปกติ)")]
    [SerializeField] private Sprite fullShell;

    [Tooltip("Phase 2: UI_Shell_0")]
    [SerializeField] private Sprite crackedShell;

    [Tooltip("Phase 3: UI_Shell_1")]
    [SerializeField] private Sprite brokenShell2;

    [Tooltip("Phase 4: UI_Shell_2")]
    [SerializeField] private Sprite brokenShell1;

    [Tooltip("Phase 5: UI_Shell_3 (เสียหายมากที่สุด)")]
    [SerializeField] private Sprite brokenShellFinal;

    [Header("Shell UI Damage Thresholds (% Damage Taken)")]
    [Tooltip("เมื่อเสีย HP ถึงค่านี้ จะเปลี่ยนเป็น Phase 2")]
    [Range(0f, 100f)]
    [SerializeField] private float shellPhase2AtDamagePercent = 20f;

    [Tooltip("เมื่อเสีย HP ถึงค่านี้ จะเปลี่ยนเป็น Phase 3")]
    [Range(0f, 100f)]
    [SerializeField] private float shellPhase3AtDamagePercent = 40f;

    [Tooltip("เมื่อเสีย HP ถึงค่านี้ จะเปลี่ยนเป็น Phase 4")]
    [Range(0f, 100f)]
    [SerializeField] private float shellPhase4AtDamagePercent = 60f;

    [Tooltip("เมื่อเสีย HP ถึงค่านี้ จะเปลี่ยนเป็น Phase 5")]
    [Range(0f, 100f)]
    [SerializeField] private float shellPhase5AtDamagePercent = 80f;

    [Header("Phase 5 Critical Effect")]
    [SerializeField] private bool phase5ShakeEnabled = true;
    [Tooltip("ระยะสั่นของกระดอง UI หน่วยเป็น UI units")]
    [SerializeField] private float phase5ShakeAmount = 3f;
    [Tooltip("ความเร็วการสั่น")]
    [SerializeField] private float phase5ShakeSpeed = 25f;
    [SerializeField] private bool phase5BlinkEnabled = true;
    [Tooltip("ความเร็วการกระพริบ")]
    [SerializeField] private float phase5BlinkSpeed = 4f;
    [Range(0f, 1f)]
    [Tooltip("ค่า Alpha ต่ำสุดตอนกระพริบ: 0 = หายไป, 1 = ไม่โปร่งใส")]
    [SerializeField] private float phase5MinAlpha = 0.2f;

    private RectTransform shellImageRect;
    private Vector2 shellImageOriginalPosition;
    private UnityEngine.Color shellImageOriginalColor = UnityEngine.Color.white;
    private bool shellPhase5EffectActive;

    [Header("Damage Hit Effect")]
    [SerializeField] private bool damageHitShakeEnabled = true;
    [Tooltip("ระยะเวลาที่ UI กระดองสั่นหลังได้รับดาเมจ")]
    [SerializeField] private float damageHitShakeDuration = 0.22f;
    [Tooltip("ระยะการสั่นของ UI กระดอง")]
    [SerializeField] private float damageHitShakeAmount = 6f;
    [Tooltip("ความเร็วการสั่นหลังได้รับดาเมจ")]
    [SerializeField] private float damageHitShakeSpeed = 45f;

    [SerializeField] private bool damageRedFlashEnabled = true;
    [Tooltip("ระยะเวลาที่ UI กระดองเปลี่ยนเป็นสีแดง")]
    [SerializeField] private float damageRedFlashDuration = 0.2f;
    [Range(0f, 1f)]
    [Tooltip("ความเข้มของสีแดง: 0 = ไม่เปลี่ยนสี, 1 = แดงเต็มที่")]
    [SerializeField] private float damageRedFlashStrength = 0.85f;
    [SerializeField] private UnityEngine.Color damageRedFlashColor = new UnityEngine.Color(1f, 0.1f, 0.1f, 1f);

    private float damageHitShakeTimer;
    private float damageRedFlashTimer;

    [Header("Sprite Resolver (HP Damage Stages)")]
    [Tooltip("Sprite Resolver ของกระดอง (ลากจาก Bone Shell มาใส่)")]
    [SerializeField] private SpriteResolver shellSpriteResolver;

    [Tooltip("Sprite Resolver ของใบหน้า (ลากจาก Bone Face มาใส่)")]
    [SerializeField] private SpriteResolver faceSpriteResolver;

    [Tooltip("ใส่ Label จาก Sprite Library ตามลำดับ: 0=ปกติ, 1=เริ่มเสียหาย, 2=เสียหายขั้น 2, 3=เสียหายขั้น 3")]
    [SerializeField]
    private string[] shellResolverLabels =
    {
        "Shell Normal",
        "",
        "",
        ""
    };

    [Tooltip("ใส่ Label จาก Sprite Library ตามลำดับ: 0=ปกติ, 1=เริ่มเสียหาย, 2=เสียหายขั้น 2, 3=เสียหายขั้น 3")]
    [SerializeField]
    private string[] faceResolverLabels =
    {
        "Face Normal",
        "",
        "",
        ""
    };

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


    [Header("Sprite Resolver HP Thresholds (% Damage Taken)")]
    [Range(0f, 100f)]
    [SerializeField] private float crackAtDamagePercent = 10f;

    [Range(0f, 100f)]
    [SerializeField] private float breakOneSectionAtPercent = 33f;

    [Range(0f, 100f)]
    [SerializeField] private float breakTwoSectionsAtPercent = 66f;

    private int currentSpriteStage = -1;

    private ShellDefense shellDefense;

    private Rigidbody2D rb;

    private PlayerController playerController;

    [Header("Damage Bubble Effect")]
    [Tooltip("Sprite ฟองที่อนุญาตให้สุ่มสร้างเมื่อได้รับดาเมจ ใส่เฉพาะ Sprite ที่ต้องการใช้")]
    [SerializeField] private Sprite[] bubbleSprites;

    [Tooltip("จำนวนฟองที่สร้างต่อการได้รับดาเมจหนึ่งครั้ง")]
    [SerializeField] private int bubbleSpawnCount = 1;

    [Range(0.05f, 1f)]
    [Tooltip("ขนาดฟองเทียบกับขนาด Shell HP")]
    [SerializeField] private float bubbleSizeMultiplier = 0.28f;

    [Tooltip("ระยะทางที่ฟองลอยขึ้น (หน่วย UI)")]
    [SerializeField] private float bubbleFloatDistance = 50f;

    [Tooltip("เวลาที่ใช้ลอยขึ้น")]
    [SerializeField] private float bubbleFloatDuration = 0.8f;

    [Tooltip("ระยะเบี่ยงด้านข้างแบบสุ่ม (หน่วย UI)")]
    [SerializeField] private float bubbleFloatSideDrift = 12f;

    [Tooltip("ระยะสุ่มตำแหน่งเริ่มต้นรอบ ๆ Shell HP (หน่วย UI)")]
    [SerializeField] private float bubbleSpawnSpread = 10f;

    [Header("Shell HP Break Effect")]
    [Tooltip("Sprite เศษกระดอง 3 แบบจากภาพที่ส่งมา")]
    [SerializeField] private Sprite[] shellFragmentSprites;

    [SerializeField] private int shellBreakShardCount = 6;
    [SerializeField] private float shellBreakShardSpeed = 180f;
    [SerializeField] private float shellBreakEffectDuration = 0.45f;

    [Tooltip("แรงโน้มถ่วงของเศษกระดอง UI")]
    [SerializeField] private float shellBreakGravity = 360f;

    [Range(0.1f, 0.8f)]
    [Tooltip("ขนาดเศษกระดองเทียบกับขนาด Shell HP")]
    [SerializeField] private float shellFragmentSizeMultiplier = 0.32f;

    private readonly List<GameObject> activeDamageEffectObjects = new List<GameObject>();


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

        CacheShellImageDefaults();

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

        // Keep the five Shell UI phase thresholds in ascending order.
        shellPhase2AtDamagePercent = Mathf.Clamp(shellPhase2AtDamagePercent, 0f, 100f);
        shellPhase3AtDamagePercent = Mathf.Clamp(shellPhase3AtDamagePercent, shellPhase2AtDamagePercent, 100f);
        shellPhase4AtDamagePercent = Mathf.Clamp(shellPhase4AtDamagePercent, shellPhase3AtDamagePercent, 100f);
        shellPhase5AtDamagePercent = Mathf.Clamp(shellPhase5AtDamagePercent, shellPhase4AtDamagePercent, 100f);

        UpdateSpriteResolverByHP();

        UpdateShellUI();

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

        speedLinear = rb.linearVelocity;
        speedMagnitude = rb.linearVelocity.magnitude;

        UpdateIllness();
        UpdateIllness();
        UpdateShellPhase5Effect();
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

            //contacts.Add(contact);
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
        if (hitFloor /*&& impactSpeedMagnitude > safeImpactSpeed*/) // Floor Bounce
        {
            //Debug.Log("Bounce Up");
            bouncePosX = 0;
            bouncePosY = 1;
        }
        else if (hitLeftWall /*&& impactSpeedMagnitude > safeImpactSpeed*/) // WallLeft Bounce
        {
            //Debug.Log("Bounce Right");
            bouncePosX = 1;
            bouncePosY = 0;
        }
        else if (hitRightWall /*&& impactSpeedMagnitude > safeImpactSpeed*/) // WallRight Bounce
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

        // Hazard Bounce & Take damage
        if (collision.gameObject.CompareTag("Hazard"))
        {
            if (playerController.isInShell)
            {
                Debug.Log("Parry!");
                playerController.canTempAim = true;
                playerController.isBounceSuccessed = true;
                playerController.NotifyParrySuccess();
                //Debug.Log($"{playerController.canTempAim}");
                ShellBounce();
                return;
            }

            Debug.Log("Hurt");
            HurtBounce();
            TakeDamage(hazardDamage);
            return;
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

        if (playerController.isInShell)
        {
            playerController.canTempAim = true;
            playerController.isBounceSuccessed = true;
            playerController.NotifyParrySuccess();
            //Debug.Log($"{playerController.canTempAim}");
            ShellBounce();
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

    private void ShellBounce()
    {
        Vector2 bounceForce = new Vector2(bouncePosX * bounceWallPower, bouncePosY * bounceFloorPower);
        rb.linearVelocity = bounceForce;
    }

    private void HurtBounce()
    {
        Vector2 bounceForce = new Vector2(bouncePosX * bounceWallPower, bouncePosY * bounceFloorPower);
        rb.linearVelocity = bounceForce;
    }

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

        if (damage <= 5)
        {
            Instantiate(lowHurt, particleSpawnPoint);
        }
        else if (damage > 5 && damage <= 10)
        {
            Instantiate(midHurt, particleSpawnPoint);
        }
        else if (damage > 10)
        {
            Instantiate(highHurt, particleSpawnPoint);
        }

            currentHP = Mathf.Clamp(currentHP - damage, 0f, maxHP);
        TriggerShellDamageEffect();
        UpdateSpriteResolverByHP();

        if (playSound)
        {
            GameAudio.Instance?.PlayDamage();
        }

        // Red Vignette: แถบแดง 4 ด้านรอบขอบจอ จางหายใน 0.5 วินาที
        if (flashScreen)
        {
            DamageVignette.Flash();
        }

        UpdateShellUI();

        // Spawn the floating bubble and Shell HP fragments for every actual hit.
        SpawnBubbleDamageParticle();
        SpawnShellBreakEffect();

        Debug.Log($"HP: {currentHP:F1} / {maxHP:F1}");

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        currentHP = Mathf.Clamp(currentHP + amount, 0f, maxHP);
        UpdateSpriteResolverByHP();

        UpdateShellUI();
    }

    private void UpdateShellUI()
    {
        if (shellImage == null)
            return;

        // Convert current HP into percentage of damage taken.
        float damagePercent = maxHP > 0f
            ? (1f - currentHP / maxHP) * 100f
            : 100f;

        // Five phases: UI_Shell, UI_Shell_0, UI_Shell_1, UI_Shell_2, UI_Shell_3.
        Sprite targetSprite;

        if (damagePercent >= shellPhase5AtDamagePercent)
            targetSprite = brokenShellFinal;
        else if (damagePercent >= shellPhase4AtDamagePercent)
            targetSprite = brokenShell1;
        else if (damagePercent >= shellPhase3AtDamagePercent)
            targetSprite = brokenShell2;
        else if (damagePercent >= shellPhase2AtDamagePercent)
            targetSprite = crackedShell;
        else
            targetSprite = fullShell;

        // If the selected Sprite has not been assigned, keep the current UI Sprite.
        if (targetSprite != null && shellImage.sprite != targetSprite)
            shellImage.sprite = targetSprite;

        // Shake and blink the Shell UI only while it is in Phase 5.
        SetShellPhase5Effect(damagePercent >= shellPhase5AtDamagePercent);
    }

    private void CacheShellImageDefaults()
    {
        if (shellImage == null)
            return;

        shellImageRect = shellImage.rectTransform;
        shellImageOriginalPosition = shellImageRect.anchoredPosition;
        shellImageOriginalColor = shellImage.color;
    }

    private void SetShellPhase5Effect(bool active)
    {
        if (shellPhase5EffectActive == active)
            return;

        shellPhase5EffectActive = active;

        if (!active)
            RestoreShellImageDefaults();
    }

    private void TriggerShellDamageEffect()
    {
        if (shellImage == null)
            return;

        if (damageHitShakeEnabled)
            damageHitShakeTimer = Mathf.Max(0f, damageHitShakeDuration);

        if (damageRedFlashEnabled)
            damageRedFlashTimer = Mathf.Max(0f, damageRedFlashDuration);
    }

    private void UpdateShellPhase5Effect()
    {
        if (shellImage == null)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;

        // Damage effects count down independently from the Phase 5 critical effect.
        damageHitShakeTimer = Mathf.Max(0f, damageHitShakeTimer - deltaTime);
        damageRedFlashTimer = Mathf.Max(0f, damageRedFlashTimer - deltaTime);

        bool damageShakeActive = damageHitShakeEnabled && damageHitShakeTimer > 0f;
        bool damageFlashActive = damageRedFlashEnabled && damageRedFlashTimer > 0f;

        if (!shellPhase5EffectActive && !damageShakeActive && !damageFlashActive)
        {
            RestoreShellImageDefaults();
            return;
        }

        // Combine Phase 5 vibration and the short hit reaction without fighting
        // over the same RectTransform position.
        Vector2 totalOffset = Vector2.zero;

        if (shellPhase5EffectActive && phase5ShakeEnabled)
        {
            float shakeTime = time * Mathf.Max(0f, phase5ShakeSpeed);
            totalOffset += new Vector2(
                Mathf.Sin(shakeTime * 1.7f) * phase5ShakeAmount,
                Mathf.Cos(shakeTime * 2.3f) * phase5ShakeAmount * 0.7f
            );
        }

        if (damageShakeActive)
        {
            float shakeProgress = damageHitShakeDuration > 0f
                ? Mathf.Clamp01(damageHitShakeTimer / damageHitShakeDuration)
                : 0f;
            float hitShakeTime = time * Mathf.Max(0f, damageHitShakeSpeed);

            // Fast, slightly irregular shake that fades out over the hit duration.
            totalOffset += new Vector2(
                Mathf.Sin(hitShakeTime * 1.9f) * damageHitShakeAmount * shakeProgress,
                Mathf.Cos(hitShakeTime * 2.7f) * damageHitShakeAmount * 0.65f * shakeProgress
            );
        }

        if (shellImageRect != null)
            shellImageRect.anchoredPosition = shellImageOriginalPosition + totalOffset;

        // Keep Phase 5 blinking while mixing in the temporary red damage tint.
        float alphaMultiplier = 1f;
        if (shellPhase5EffectActive && phase5BlinkEnabled)
        {
            float blink = Mathf.PingPong(time * Mathf.Max(0f, phase5BlinkSpeed), 1f);
            alphaMultiplier = Mathf.Lerp(phase5MinAlpha, 1f, blink);
        }

        UnityEngine.Color finalColor = shellImageOriginalColor;

        if (damageFlashActive)
        {
            float flashProgress = damageRedFlashDuration > 0f
                ? Mathf.Clamp01(damageRedFlashTimer / damageRedFlashDuration)
                : 0f;
            float redStrength = damageRedFlashStrength * flashProgress;
            finalColor = UnityEngine.Color.Lerp(
                shellImageOriginalColor,
                damageRedFlashColor,
                redStrength
            );
        }

        finalColor.a = shellImageOriginalColor.a * alphaMultiplier;
        shellImage.color = finalColor;
    }

    private void RestoreShellImageDefaults()
    {
        if (shellImageRect != null)
            shellImageRect.anchoredPosition = shellImageOriginalPosition;

        if (shellImage != null)
            shellImage.color = shellImageOriginalColor;
    }

    private void OnDisable()
    {
        shellPhase5EffectActive = false;
        damageHitShakeTimer = 0f;
        damageRedFlashTimer = 0f;
        RestoreShellImageDefaults();

        for (int i = activeDamageEffectObjects.Count - 1; i >= 0; i--)
        {
            if (activeDamageEffectObjects[i] != null)
                Destroy(activeDamageEffectObjects[i]);
        }
        activeDamageEffectObjects.Clear();
    }

    private void UpdateSpriteResolverByHP()
    {
        float damagePercent = maxHP > 0f
            ? (1f - currentHP / maxHP) * 100f
            : 100f;

        int newStage = 0;

        if (damagePercent >= crackAtDamagePercent)
            newStage = 1;

        if (damagePercent >= breakOneSectionAtPercent)
            newStage = 2;

        if (damagePercent >= breakTwoSectionsAtPercent)
            newStage = 3;

        if (newStage == currentSpriteStage)
            return;

        currentSpriteStage = newStage;

        ApplySpriteResolverLabel(shellSpriteResolver, shellResolverLabels, newStage);
        ApplySpriteResolverLabel(faceSpriteResolver, faceResolverLabels, newStage);
    }

    private void ApplySpriteResolverLabel(
        SpriteResolver resolver,
        string[] labels,
        int stage)
    {
        if (resolver == null || labels == null || stage < 0 || stage >= labels.Length)
            return;

        string label = labels[stage];

        // เว้น Label ว่างไว้ได้ หากยังไม่ต้องการเปลี่ยน Sprite ใน Stage นั้น
        if (string.IsNullOrWhiteSpace(label))
            return;

        string category = resolver.GetCategory();

        if (string.IsNullOrWhiteSpace(category))
            return;

        // เปลี่ยนเฉพาะ Label โดยใช้ Category เดิมของ Sprite Resolver
        resolver.SetCategoryAndLabel(category, label);
    }

    private void SpawnBubbleDamageParticle()
    {
        // Bubble uses only the Sprites assigned in the Inspector array.
        // Empty or null entries are ignored; no external prefab is instantiated.
        if (shellImage == null || bubbleSprites == null || bubbleSprites.Length == 0)
            return;

        RectTransform shellRect = shellImage.rectTransform;
        RectTransform parentRect = shellRect.parent as RectTransform;
        if (parentRect == null)
            return;

        int validSpriteCount = 0;
        for (int i = 0; i < bubbleSprites.Length; i++)
        {
            if (bubbleSprites[i] != null)
                validSpriteCount++;
        }

        if (validSpriteCount == 0)
            return;

        Vector2 centerAnchor = (shellRect.anchorMin + shellRect.anchorMax) * 0.5f;
        Vector2 centerOffset = new Vector2(
            (0.5f - shellRect.pivot.x) * shellRect.rect.width,
            (0.5f - shellRect.pivot.y) * shellRect.rect.height
        );
        Vector2 shellCenter = shellRect.anchoredPosition + centerOffset;

        for (int i = 0; i < Mathf.Max(0, bubbleSpawnCount); i++)
        {
            Sprite bubbleSprite = GetRandomAssignedBubbleSprite(validSpriteCount);
            if (bubbleSprite == null)
                continue;

            GameObject bubbleObject = new GameObject(
                "DamageBubble",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            bubbleObject.transform.SetParent(parentRect, false);
            bubbleObject.transform.SetAsLastSibling();

            RectTransform bubbleRect = bubbleObject.GetComponent<RectTransform>();
            Image bubbleImage = bubbleObject.GetComponent<Image>();

            bubbleRect.anchorMin = centerAnchor;
            bubbleRect.anchorMax = centerAnchor;
            bubbleRect.pivot = new Vector2(0.5f, 0.5f);
            bubbleRect.localScale = Vector3.one;

            float baseSize = Mathf.Max(
                8f,
                Mathf.Min(shellRect.rect.width, shellRect.rect.height) * bubbleSizeMultiplier
            );
            float spriteAspect = bubbleSprite.rect.width / Mathf.Max(1f, bubbleSprite.rect.height);
            float bubbleWidth = baseSize;
            float bubbleHeight = bubbleWidth / Mathf.Max(0.01f, spriteAspect);
            if (bubbleHeight > baseSize)
            {
                bubbleHeight = baseSize;
                bubbleWidth = bubbleHeight * spriteAspect;
            }

            bubbleRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);
            bubbleRect.anchoredPosition = shellCenter + Random.insideUnitCircle * bubbleSpawnSpread;
            bubbleRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));

            bubbleImage.sprite = bubbleSprite;
            bubbleImage.preserveAspect = true;
            bubbleImage.raycastTarget = false;
            bubbleImage.color = UnityEngine.Color.white;

            Vector2 startPosition = bubbleRect.anchoredPosition;
            Vector2 endPosition = startPosition
                + Vector2.up * bubbleFloatDistance
                + Vector2.right * Random.Range(-bubbleFloatSideDrift, bubbleFloatSideDrift);

            activeDamageEffectObjects.Add(bubbleObject);
            StartCoroutine(AnimateBubbleFloat(
                bubbleObject,
                bubbleRect,
                bubbleImage,
                startPosition,
                endPosition,
                Mathf.Max(0.01f, bubbleFloatDuration)
            ));
        }
    }

    private Sprite GetRandomAssignedBubbleSprite(int validSpriteCount)
    {
        if (bubbleSprites == null || validSpriteCount <= 0)
            return null;

        int selectedValidIndex = Random.Range(0, validSpriteCount);
        for (int i = 0; i < bubbleSprites.Length; i++)
        {
            if (bubbleSprites[i] == null)
                continue;

            if (selectedValidIndex == 0)
                return bubbleSprites[i];

            selectedValidIndex--;
        }

        return null;
    }

    private IEnumerator AnimateBubbleFloat(
        GameObject bubbleObject,
        RectTransform bubbleRect,
        Image bubbleImage,
        Vector2 startPosition,
        Vector2 endPosition,
        float duration)
    {
        float elapsed = 0f;
        UnityEngine.Color startColor = bubbleImage.color;

        while (elapsed < duration && bubbleObject != null && bubbleRect != null && bubbleImage != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);

            bubbleRect.anchoredPosition = Vector2.LerpUnclamped(
                startPosition,
                endPosition,
                easedProgress
            );

            UnityEngine.Color color = startColor;
            color.a = 1f - progress;
            bubbleImage.color = color;
            yield return null;
        }

        if (bubbleObject != null)
        {
            activeDamageEffectObjects.Remove(bubbleObject);
            Destroy(bubbleObject);
        }
    }

    private void SpawnShellBreakEffect()
    {
        if (shellImage == null || shellFragmentSprites == null || shellFragmentSprites.Length == 0)
            return;

        RectTransform shellRect = shellImage.rectTransform;
        RectTransform parentRect = shellRect.parent as RectTransform;
        if (parentRect == null)
            return;

        int fragmentCount = Mathf.Max(1, shellBreakShardCount);
        Vector2 centerAnchor = (shellRect.anchorMin + shellRect.anchorMax) * 0.5f;
        Vector2 centerOffset = new Vector2(
            (0.5f - shellRect.pivot.x) * shellRect.rect.width,
            (0.5f - shellRect.pivot.y) * shellRect.rect.height
        );
        Vector2 spawnPosition = shellRect.anchoredPosition + centerOffset;
        float maxWidth = Mathf.Max(8f, shellRect.rect.width * shellFragmentSizeMultiplier);
        float maxHeight = Mathf.Max(8f, shellRect.rect.height * shellFragmentSizeMultiplier);
        float angleStep = 360f / fragmentCount;
        float startAngle = Random.Range(0f, 360f);

        for (int i = 0; i < fragmentCount; i++)
        {
            Sprite fragmentSprite = shellFragmentSprites[Random.Range(0, shellFragmentSprites.Length)];
            if (fragmentSprite == null)
                continue;

            GameObject fragmentObject = new GameObject(
                "ShellHP_Fragment",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            fragmentObject.transform.SetParent(parentRect, false);
            fragmentObject.transform.SetAsLastSibling();

            RectTransform fragmentRect = fragmentObject.GetComponent<RectTransform>();
            Image fragmentImage = fragmentObject.GetComponent<Image>();

            // Use a single center anchor so fragments keep the Shell HP's screen position.
            fragmentRect.anchorMin = centerAnchor;
            fragmentRect.anchorMax = centerAnchor;
            fragmentRect.pivot = new Vector2(0.5f, 0.5f);
            fragmentRect.anchoredPosition = spawnPosition;
            fragmentRect.localScale = Vector3.one;

            float spriteAspect = fragmentSprite.rect.width / Mathf.Max(1f, fragmentSprite.rect.height);
            float fragmentWidth = maxWidth;
            float fragmentHeight = fragmentWidth / Mathf.Max(0.01f, spriteAspect);
            if (fragmentHeight > maxHeight)
            {
                fragmentHeight = maxHeight;
                fragmentWidth = fragmentHeight * spriteAspect;
            }
            fragmentRect.sizeDelta = new Vector2(fragmentWidth, fragmentHeight);
            fragmentRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f));

            fragmentImage.sprite = fragmentSprite;
            fragmentImage.preserveAspect = true;
            fragmentImage.raycastTarget = false;
            fragmentImage.color = UnityEngine.Color.white;

            float angle = (startAngle + angleStep * i + Random.Range(-16f, 16f)) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 velocity = direction * shellBreakShardSpeed * Random.Range(0.75f, 1.25f);
            float rotationSpeed = Random.Range(-540f, 540f);

            activeDamageEffectObjects.Add(fragmentObject);
            StartCoroutine(AnimateShellFragment(
                fragmentRect,
                fragmentImage,
                fragmentObject,
                velocity,
                rotationSpeed
            ));
        }
    }

    private IEnumerator AnimateShellFragment(
        RectTransform fragmentRect,
        Image fragmentImage,
        GameObject fragmentObject,
        Vector2 velocity,
        float rotationSpeed)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, shellBreakEffectDuration);
        UnityEngine.Color startColor = fragmentImage.color;

        while (elapsed < duration && fragmentRect != null && fragmentImage != null)
        {
            float deltaTime = Time.unscaledDeltaTime;
            elapsed += deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            velocity += Vector2.down * shellBreakGravity * deltaTime;
            fragmentRect.anchoredPosition += velocity * deltaTime;
            fragmentRect.Rotate(0f, 0f, rotationSpeed * deltaTime);

            UnityEngine.Color color = startColor;
            color.a = 1f - progress;
            fragmentImage.color = color;

            yield return null;
        }

        if (fragmentObject != null)
        {
            activeDamageEffectObjects.Remove(fragmentObject);
            Destroy(fragmentObject);
        }
    }

    public void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHP = 0f;
        UpdateShellUI();
        GameAudio.Instance?.PlayDeath();

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
        damageHitShakeTimer = 0f;
        damageRedFlashTimer = 0f;
        UpdateSpriteResolverByHP();

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

        UpdateShellUI();
    }


    public void ResetHealth()
    {
        ResetAfterRespawn();
    }

    //private List<ContactPoint2D> contacts = new List<ContactPoint2D>();
    //private void OnDrawGizmos()
    //{
    //    foreach (ContactPoint2D contact in contacts)
    //    {
    //        Vector2 normal = GetNormalTowardsPlayer(contact);

    //        // จุดสัมผัส
    //        Gizmos.color = UnityEngine.Color.red;
    //        Gizmos.DrawSphere(contact.point, 0.08f);

    //        // ทิศทาง Normal
    //        //Gizmos.color = UnityEngine.Color.green;
    //        //Gizmos.DrawLine(
    //        //    contact.point,
    //        //    contact.point + normal * 0.5f
    //        //);
    //    }
    //}
}