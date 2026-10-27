using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HealthNew : MonoBehaviour
{
    [Header("HP")]

    public float maxHP = 100f;

    public float currentHP = 100f;

    private bool isDead;

    public bool IsDead => isDead;

    public float CurrentHP => currentHP;

    public float MaxHP => maxHP;

    [Header("Collision Damage")]

    [SerializeField] private LayerMask ignoreDamageLayers;

    [SerializeField] private float safeImpactSpeed = 10f;

    [SerializeField] private float damagePerSpeed = 2f;

    [SerializeField] private float maxCollisionDamage = 20f;

    [Header("Wall Collision")]

    [SerializeField] private float minVerticalSpeedForWallDamage = 1f;

    [Header("Shell HP UI")]
    [SerializeField] private Image shellImage;
    [SerializeField] private Sprite fullShell;
    [SerializeField] private Sprite crackedShell;
    [SerializeField] private Sprite brokenShell2;
    [SerializeField] private Sprite brokenShell1;

    private float crackAtDamagePercent = 10f;
    private float breakOneSectionAtPercent = 33f;
    private float breakTwoSectionsAtPercent = 66f;

    private ShellWallBounce shellWallBounce;

    private Rigidbody2D rb;

    private MouseController mouseController;

    [Header("Shell Break Effect")]

    [SerializeField] private int shellBreakShardCount = 12;

    [SerializeField] private float shellBreakShardSpeed = 180f;

    [SerializeField] private float shellBreakEffectDuration = 0.45f;

    private int currentShellStage = -1;

    private static Sprite shellShardSprite;

    private float respawnDelay = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        shellWallBounce = GetComponent<ShellWallBounce>();
        mouseController = GetComponent<MouseController>();

        maxHP = Mathf.Max(1f, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        safeImpactSpeed = Mathf.Max(0f, safeImpactSpeed);
        damagePerSpeed = Mathf.Max(0f, damagePerSpeed);
        maxCollisionDamage = Mathf.Max(0f, maxCollisionDamage);
        minVerticalSpeedForWallDamage = Mathf.Max(0f, minVerticalSpeedForWallDamage);

        crackAtDamagePercent = Mathf.Clamp(crackAtDamagePercent, 0f, 100f);
        breakOneSectionAtPercent = Mathf.Clamp(breakOneSectionAtPercent, crackAtDamagePercent, 100f);
        breakTwoSectionsAtPercent = Mathf.Clamp(breakTwoSectionsAtPercent, breakOneSectionAtPercent, 100f);

        UpdateShellUI();

        if (currentHP <= 0f)
        {
            Die();
        }
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
        bool hitWall = false;

        // ตรวจทิศทางการชน
        foreach (ContactPoint2D contact in collision.contacts)
        {
            Vector2 normal = GetNormalTowardsPlayer(contact);

            if (normal.y > 0.5f)
            {
                hitFloor = true;
            }
            else if (normal.y < -0.5f)
            {
                hitCeiling = true;
            }
            else if (Mathf.Abs(normal.x) > 0.5f)
            {
                hitWall = true;
            }
        }

        Vector2 impactVelocity = collision.relativeVelocity;

        float impactSpeed;


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
        else if (hitWall)
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

        float excessSpeed = impactSpeed - safeImpactSpeed;

        float damage = Mathf.Min(excessSpeed * damagePerSpeed, maxCollisionDamage);

        TakeDamage(damage);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            Die();
        }
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

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f)
        {
            return;
        }

        // ตรวจสอบระบบป้องกันดาเมจก่อนหัก HP
        if (shellWallBounce != null && shellWallBounce.TryBlockDamage())
        {
            return;
        }

        currentHP = Mathf.Clamp(currentHP - damage, 0f, maxHP);
        GameAudio.Instance?.PlayDamage();
        UpdateShellUI();

        Debug.Log($"HP: {currentHP:F1} / {maxHP:F1}");

        if (currentHP <= 0f)
        {
            Die();
        }
    }

    private void UpdateShellUI()
    {
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

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        currentHP = Mathf.Clamp(currentHP + amount, 0f, maxHP);

        UpdateShellUI();
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
        UpdateShellUI();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (mouseController != null)
        {
            mouseController.movementLocked = true;
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

        bool respawned = Checkpoint.RespawnPlayer(transform);
    }

    public void ResetAfterRespawn()
    {
        currentHP = maxHP;
        isDead = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (mouseController != null)
        {
            mouseController.movementLocked = false;
            mouseController.ResetAfterRespawn();
        }

        UpdateShellUI();
    }


    public void ResetHealth()
    {
        ResetAfterRespawn();
    }


    private void SpawnShellBreakEffect(int stage)
    {
        RectTransform shellRect = shellImage.rectTransform;
        RectTransform parent = shellRect.parent as RectTransform;

        if (parent == null)
        {
            return;
        }

        Sprite shardSprite = GetShellShardSprite();

        Color shardColor = stage >= 3
            ? new Color(0.75f, 0.32f, 0.24f, 1f)
            : new Color(0.45f, 0.72f, 0.38f, 1f);

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
                ? new Color(0.82f, 0.66f, 0.38f, 1f)
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
        texture.SetPixel(0, 0, Color.white);
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

        Color startColor = shardImage.color;

        while (elapsed < duration && shardRect != null && shardImage != null)
        {
            float deltaTime = Time.unscaledDeltaTime;

            elapsed += deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);

            // กระจายออกแล้วค่อย ๆ ตกลง
            velocity += Vector2.down * 260f * deltaTime;

            shardRect.anchoredPosition += velocity * deltaTime;

            shardRect.Rotate(0f, 0f, rotationSpeed * deltaTime);

            Color color = startColor;
            color.a = 1f - progress;

            shardImage.color = color;

            yield return null;
        }

        if (shardRect != null)
        {
            Destroy(shardRect.gameObject);
        }
    }
}