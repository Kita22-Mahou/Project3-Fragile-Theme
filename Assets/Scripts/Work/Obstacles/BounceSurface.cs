using UnityEngine;

public class BounceSurface : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Jump King Wall Bounce")]
    [Tooltip("1 = สะท้อนด้วยความเร็วเดิม")]
    [SerializeField] private float bounceMultiplier = 1f;

    [Tooltip("ความเร็วเด้งออกจากกำแพงขั้นต่ำ")]
    [SerializeField] private float minimumBounceSpeed = 5f;

    [Tooltip("ความเร็วชนขั้นต่ำที่จะทำให้เด้ง")]
    [SerializeField] private float minimumImpactSpeed = 0.5f;

    [Tooltip("ระยะขยับออกจากกำแพงหลังชน")]
    [SerializeField] private float separationDistance = 0.03f;

    private Collider2D surfaceCollider;

    private void Awake()
    {
        surfaceCollider = GetComponent<Collider2D>();

        if (surfaceCollider == null)
        {
            Debug.LogError(
                "BounceSurface ต้องมี Collider2D",
                this
            );

            enabled = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D playerCollider = FindPlayerCollider(collision);

        if (playerCollider == null)
            return;

        Rigidbody2D playerRb = playerCollider.attachedRigidbody;

        if (playerRb == null)
            return;

        // ตำแหน่งผู้เล่นเทียบกับตัวกำแพง
        Vector2 toPlayer =
            playerRb.worldCenterOfMass -
            (Vector2)surfaceCollider.bounds.center;

        Vector2 wallNormal = Vector2.zero;
        float strongestSide = 0f;

        // ตรวจเฉพาะการชนด้านข้าง
        foreach (ContactPoint2D contact in collision.contacts)
        {
            Vector2 normal = contact.normal.normalized;

            // ไม่รับการชนด้านบนหรือด้านล่าง
            if (Mathf.Abs(normal.x) < 0.7f ||
                Mathf.Abs(normal.x) <= Mathf.Abs(normal.y))
            {
                continue;
            }

            // ให้ Normal ชี้ออกจากกำแพงเข้าหาผู้เล่น
            if (Vector2.Dot(normal, toPlayer) < 0f)
            {
                normal = -normal;
            }

            if (Mathf.Abs(normal.x) > strongestSide)
            {
                strongestSide = Mathf.Abs(normal.x);
                wallNormal = normal;
            }
        }

        if (wallNormal == Vector2.zero)
            return;

        wallNormal.Normalize();

        // ความเร็วของผู้เล่น ณ ตอนชน
        Vector2 velocity = playerRb.linearVelocity;

        // ใช้ความเร็วสัมพัทธ์ช่วยตรวจจับแรงปะทะ
        float relativeImpactSpeed = Mathf.Abs(
            Vector2.Dot(collision.relativeVelocity, wallNormal)
        );

        float playerImpactSpeed = Mathf.Abs(
            Vector2.Dot(velocity, wallNormal)
        );

        float impactSpeed = Mathf.Max(
            relativeImpactSpeed,
            playerImpactSpeed
        );

        // ชนเบาเกินไปจะไม่เด้ง
        if (impactSpeed < minimumImpactSpeed)
            return;

        // เก็บความเร็วตามแนวสัมผัสไว้
        // สำหรับกำแพงตั้งฉาก ความเร็วแกน Y จะยังคงเดิม
        float normalVelocity = Vector2.Dot(velocity, wallNormal);

        Vector2 tangentVelocity =
            velocity - normalVelocity * wallNormal;

        // สะท้อนออกจากกำแพง
        float bounceSpeed = Mathf.Max(
            impactSpeed * bounceMultiplier,
            minimumBounceSpeed
        );

        Vector2 bounceVelocity =
            tangentVelocity + wallNormal * bounceSpeed;

        // ขยับออกจากกำแพงเล็กน้อย ป้องกันการติดขอบ
        playerRb.position += wallNormal * separationDistance;

        // แจ้ง MouseController ว่ามีแรงเด้งจากภายนอก
        MouseController controller =
            playerRb.GetComponent<MouseController>();

        if (controller == null)
        {
            controller = playerCollider.GetComponent<MouseController>();
        }

        if (controller != null)
        {
            controller.NotifyExternalBounce();
        }

        // ใช้ความเร็วเด้งหลังแจ้ง Controller
        playerRb.linearVelocity = bounceVelocity;
    }

    private Collider2D FindPlayerCollider(Collision2D collision)
    {
        if (IsPlayerLayer(collision.collider))
            return collision.collider;

        if (IsPlayerLayer(collision.otherCollider))
            return collision.otherCollider;

        return null;
    }

    private bool IsPlayerLayer(Collider2D col)
    {
        if (col == null)
            return false;

        bool colliderLayerMatches =
            (playerLayer.value & (1 << col.gameObject.layer)) != 0;

        Rigidbody2D body = col.attachedRigidbody;

        bool bodyLayerMatches = body != null &&
            (playerLayer.value & (1 << body.gameObject.layer)) != 0;

        return colliderLayerMatches || bodyLayerMatches;
    }
}