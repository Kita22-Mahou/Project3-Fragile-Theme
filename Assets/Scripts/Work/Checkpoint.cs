using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]

    [Tooltip("ตำแหน่งเกิดเหนือธง")]
    [SerializeField] private Vector2 respawnOffset = new Vector2(0f, 0.5f);


    [Header("Flag Visual")]

    [SerializeField] private SpriteRenderer flagSprite;

    [SerializeField] private Color inactiveColor = Color.white;

    [SerializeField] private Color activeColor = Color.green;


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

        // บันทึกตำแหน่ง Checkpoint ล่าสุด
        activeCheckpoint = this;

        checkpointPosition = transform.position + (Vector3)respawnOffset;

        HasCheckpoint = true;

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
        if (flagSprite != null)
        {
            flagSprite.color = isActive ? activeColor : inactiveColor;
        }
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
}