using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]

    [Tooltip("ตำแหน่งเกิดเหนือธง")]
    [SerializeField] private Vector2 respawnOffset = new Vector2(0f, 0.5f);
    private static Material fireworkMaterial;

    [Header("Flag Visual")]

    [SerializeField] private SpriteRenderer flagSprite;

    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Sprite activeSprite;

    [SerializeField]
    private Color[] fireworkColors =
{
    new Color(1f, 0.25f, 0.3f), // ชมพูแดง
    new Color(1f, 0.85f, 0.2f), // เหลืองทอง
    new Color(0.2f, 0.9f, 1f),  // ฟ้า
    new Color(0.8f, 0.35f, 1f)  // ม่วง
};
    // Checkpoint ล่าสุดที่ผู้เล่นแตะ
    private static Checkpoint activeCheckpoint;

    private static Vector3 checkpointPosition;

    public static bool HasCheckpoint { get; private set; }


    private void Awake()
    {
        if (flagSprite == null)
        {
            flagSprite = GetComponentInChildren<SpriteRenderer>();
        }
    }


    private void Start()
    {
        UpdateVisual(activeCheckpoint == this);
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        // ตรวจสอบว่าผู้เล่นมี MouseController หรือไม่
        MouseController player = other.GetComponentInParent<MouseController>();

        if (player == null)
        {
            return;
        }

        bool isNewCheckpoint = activeCheckpoint != this;

        // บันทึกตำแหน่ง Checkpoint ล่าสุด
        activeCheckpoint = this;

        checkpointPosition = transform.position + (Vector3)respawnOffset;

        HasCheckpoint = true;

        if (isNewCheckpoint)
        {
            GameAudio.Instance?.PlayCheckpoint();

            SpawnFireworks();
        }

        // อัปเดตสีธงทุกอันในฉาก
        Checkpoint[] allFlags = Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Exclude);

        foreach (Checkpoint flag in allFlags)
        {
            flag.UpdateVisual(flag == activeCheckpoint);
        }

        Debug.Log("Checkpoint saved: " + gameObject.name);
    }


    private void UpdateVisual(bool isActive)
    {
        if (flagSprite == null)
            return;

        // เลือกรูปธงตามสถานะ Checkpoint
        Sprite targetSprite = isActive
            ? activeSprite
            : inactiveSprite;

        if (targetSprite != null)
        {
            flagSprite.sprite = targetSprite;
        }

        // ไม่ให้สีเดิมย้อมสี Sprite
        flagSprite.color = Color.white;
    }


    // เรียกจากระบบตายของผู้เล่นเพื่อกลับไปยัง Checkpoint ล่าสุด
    public static bool RespawnPlayer(Transform player)
    {
        if (!HasCheckpoint || player == null)
        {
            return false;
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = checkpointPosition;
        }

        player.position = checkpointPosition;

        HealthNew health = player.GetComponent<HealthNew>();

        if (health != null)
        {
            health.ResetAfterRespawn();
        }
        else
        {
            MouseController controller = player.GetComponent<MouseController>();

            if (controller != null)
            {
                controller.ResetAfterRespawn();
            }
        }

        Physics2D.SyncTransforms();

        return true;
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(transform.position + (Vector3)respawnOffset, 0.2f);
    }

    private void SpawnFireworks()
    {
        if (fireworkColors == null || fireworkColors.Length == 0)
            return;

        Vector3 effectPosition = transform.position + Vector3.up * 0.8f;

        foreach (Color color in fireworkColors)
        {
            CreateFireworkBurst(effectPosition, color);
        }
    }

    // สร้างประกายระเบิดหนึ่งชุด
    private void CreateFireworkBurst(Vector3 position, Color color)
    {
        GameObject effectObject = new GameObject("CheckpointFirework");
        effectObject.transform.position = position;

        ParticleSystem particles =
            effectObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = color;
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;
        main.stopAction = ParticleSystemStopAction.Destroy;

        // ให้ประกายค่อย ๆ จางลง
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particles.colorOverLifetime;

        colorOverLifetime.enabled = true;

        Gradient fadeGradient = new Gradient();

        fadeGradient.SetKeys(
            new GradientColorKey[]
            {
        new GradientColorKey(Color.white, 0f),
        new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
        new GradientAlphaKey(1f, 0f),
        new GradientAlphaKey(1f, 0.6f),
        new GradientAlphaKey(0f, 1f)
            }
        );

        colorOverLifetime.color =
            new ParticleSystem.MinMaxGradient(fadeGradient);

        // ระเบิดกระจายรอบจุดศูนย์กลาง
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        // ปล่อยอนุภาคพร้อมกันเป็นชุด
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        emission.SetBursts(new ParticleSystem.Burst[]
        {
        new ParticleSystem.Burst(0f, 25)
        });

        // แสดงเอฟเฟกต์อยู่ด้านหน้า
        ParticleSystemRenderer particleRenderer =
            particles.GetComponent<ParticleSystemRenderer>();

        Material material = GetFireworkMaterial();

        if (material != null)
        {
            particleRenderer.sharedMaterial = material;
        }

        if (flagSprite != null)
        {
            particleRenderer.sortingLayerID = flagSprite.sortingLayerID;
            particleRenderer.sortingOrder = flagSprite.sortingOrder + 20;
        }
        else
        {
            particleRenderer.sortingOrder = 20;
        }

        particles.Play();
    }
    private Material GetFireworkMaterial()
    {
        if (fireworkMaterial != null)
            return fireworkMaterial;

        // รองรับ Unity URP ก่อน แล้วค่อยลอง Built-in Render Pipeline
        string[] shaderNames =
        {
        "Universal Render Pipeline/Particles/Unlit",
        "Particles/Standard Unlit",
        "Legacy Shaders/Particles/Alpha Blended"
    };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);

            if (shader != null && shader.isSupported)
            {
                fireworkMaterial = new Material(shader);
                fireworkMaterial.name = "CheckpointFireworkMaterial";

                if (fireworkMaterial.HasProperty("_BaseColor"))
                    fireworkMaterial.SetColor("_BaseColor", Color.white);

                if (fireworkMaterial.HasProperty("_Color"))
                    fireworkMaterial.SetColor("_Color", Color.white);

                return fireworkMaterial;
            }
        }

        Debug.LogError(
            "ไม่พบ Shader ที่รองรับสำหรับ Checkpoint Fireworks",
            this
        );

        return null;
    }

}