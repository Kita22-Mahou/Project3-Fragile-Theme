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

    [SerializeField] private float minCollisionDamage = 1f;

    [Header("Wall Collision")]

    [SerializeField] private float minVerticalSpeedForWallDamage = 1f;

    [SerializeField] private bool illnessEnabled = true;

    [SerializeField] private float illnessDamagePerSecond = 1f;

    [SerializeField] private float illnessTickInterval = 1f;

    [SerializeField] private bool shellBlocksIllness = false;

    [SerializeField] private bool illnessPlaysDamageSound = false;

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
            ShellBounce();
            return;
        }

        float excessSpeed = impactSpeedMagnitude - safeImpactSpeed;

        float damage = Mathf.Min(excessSpeed * damagePerSpeed, maxCollisionDamage);

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

        float damagePercent = maxHP > 0f
            ? (1f - currentHP / maxHP) * 100f
            : 100f;

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

        if (targetSprite != null && shellImage.sprite != targetSprite)
            shellImage.sprite = targetSprite;

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

        damageHitShakeTimer = Mathf.Max(0f, damageHitShakeTimer - deltaTime);
        damageRedFlashTimer = Mathf.Max(0f, damageRedFlashTimer - deltaTime);

        bool damageShakeActive = damageHitShakeEnabled && damageHitShakeTimer > 0f;
        bool damageFlashActive = damageRedFlashEnabled && damageRedFlashTimer > 0f;

        if (!shellPhase5EffectActive && !damageShakeActive && !damageFlashActive)
        {
            RestoreShellImageDefaults();
            return;
        }

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

            
            totalOffset += new Vector2(
                Mathf.Sin(hitShakeTime * 1.9f) * damageHitShakeAmount * shakeProgress,
                Mathf.Cos(hitShakeTime * 2.7f) * damageHitShakeAmount * 0.65f * shakeProgress
            );
        }

        if (shellImageRect != null)
            shellImageRect.anchoredPosition = shellImageOriginalPosition + totalOffset;

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

        if (string.IsNullOrWhiteSpace(label))
            return;

        string category = resolver.GetCategory();

        if (string.IsNullOrWhiteSpace(category))
            return;

        resolver.SetCategoryAndLabel(category, label);
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
        yield return new WaitForSecondsRealtime(respawnDelay);

        yield return FadeTo(1f, fadeOutDuration);

        Checkpoint.RespawnPlayer(transform);

        yield return new WaitForSecondsRealtime(blackHoldDuration);

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

    
}