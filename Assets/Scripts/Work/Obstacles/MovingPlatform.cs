using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class MovingPlatform : MonoBehaviour
{
    [Header("Horizontal Movement")]
    [Tooltip("ระยะที่แท่นเคลื่อนจากจุดเริ่มต้นไปทางซ้าย")]
    [SerializeField] private float leftDistance = 5f;

    [Tooltip("ระยะที่แท่นเคลื่อนจากจุดเริ่มต้นไปทางขวา")]
    [SerializeField] private float rightDistance = 5f;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float waitTime = 0.5f;

    [Tooltip("เปิดไว้เพื่อให้แท่นเคลื่อนที่ซ้าย-ขวาซ้ำไปเรื่อย ๆ")]
    [SerializeField] private bool loopMovement = true;

    [Header("Sprite & Collider Flip")]
    [Tooltip("พลิก Sprite และรูปทรง Collider ตามทิศทางการเคลื่อนที่")]
    [SerializeField] private bool isFlipSprite = false;

    private Rigidbody2D rb;
    private Collider2D platformCollider;
    private SpriteRenderer sprite;
    private MouseController standingPlayer;
    private bool originalSpriteFlipX;
    private Vector2 originalColliderOffset;
    private Vector2[][] originalPolygonPaths;
    private Vector2[] originalEdgePoints;

    private Vector2 leftPoint;
    private Vector2 rightPoint;
    private int direction = 1;
    private float waitTimer;
    private bool isMoving;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        platformCollider = GetComponent<Collider2D>();
        sprite = GetComponent<SpriteRenderer>();

        originalSpriteFlipX = sprite != null && sprite.flipX;
        originalColliderOffset = platformCollider.offset;
        CacheColliderGeometry();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        Vector2 startPosition = rb.position;
        leftPoint = startPosition + Vector2.left * Mathf.Abs(leftDistance);
        rightPoint = startPosition + Vector2.right * Mathf.Abs(rightDistance);

        direction = 1;
        ApplyDirectionFlip();
        isMoving = rightDistance > 0.01f || leftDistance > 0.01f;
    }

    private void CacheColliderGeometry()
    {
        PolygonCollider2D polygon = platformCollider as PolygonCollider2D;
        if (polygon != null)
        {
            originalPolygonPaths = new Vector2[polygon.pathCount][];

            for (int i = 0; i < polygon.pathCount; i++)
                originalPolygonPaths[i] = polygon.GetPath(i);

            return;
        }

        EdgeCollider2D edge = platformCollider as EdgeCollider2D;
        if (edge != null)
            originalEdgePoints = edge.points;
    }

    private void ApplyDirectionFlip()
    {
        if (!isFlipSprite)
            return;

        bool flip = direction < 0;
        float xSign = flip ? -1f : 1f;

        // SpriteRenderer.flipX พลิกเฉพาะภาพ ส่วน Collider จะสะท้อนรูปทรงแยกต่างหาก
        if (sprite != null)
            sprite.flipX = originalSpriteFlipX ^ flip;

        // Collider ทั่วไป เช่น Box/Circle/Capsule ใช้การสะท้อน Offset
        platformCollider.offset = new Vector2(
            originalColliderOffset.x * xSign,
            originalColliderOffset.y
        );

        // PolygonCollider2D ต้องสะท้อนจุดของแต่ละ Path ด้วย
        PolygonCollider2D polygon = platformCollider as PolygonCollider2D;
        if (polygon != null && originalPolygonPaths != null)
        {
            for (int pathIndex = 0; pathIndex < originalPolygonPaths.Length; pathIndex++)
            {
                Vector2[] mirroredPoints = (Vector2[])originalPolygonPaths[pathIndex].Clone();

                for (int pointIndex = 0; pointIndex < mirroredPoints.Length; pointIndex++)
                    mirroredPoints[pointIndex].x *= xSign;

                polygon.SetPath(pathIndex, mirroredPoints);
            }

            return;
        }

        // EdgeCollider2D ใช้การสะท้อนจุดของเส้น Collider
        EdgeCollider2D edge = platformCollider as EdgeCollider2D;
        if (edge != null && originalEdgePoints != null)
        {
            Vector2[] mirroredPoints = (Vector2[])originalEdgePoints.Clone();

            for (int i = 0; i < mirroredPoints.Length; i++)
                mirroredPoints[i].x *= xSign;

            edge.points = mirroredPoints;
        }
    }

    private void FixedUpdate()
    {
        if (!isMoving)
            return;

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector2 destination = direction > 0 ? rightPoint : leftPoint;
        Vector2 oldPosition = rb.position;
        Vector2 nextPosition = Vector2.MoveTowards(
            oldPosition,
            destination,
            Mathf.Max(0f, moveSpeed) * Time.fixedDeltaTime
        );

        Vector2 delta = nextPosition - oldPosition;

        rb.MovePosition(nextPosition);
        CarryPlayer(delta);

        if (Vector2.Distance(nextPosition, destination) < 0.01f)
        {
            rb.MovePosition(destination);
            ReachEndpoint();
        }
    }

    private void ReachEndpoint()
    {
        waitTimer = Mathf.Max(0f, waitTime);

        if (direction > 0)
        {
            // ถึงด้านขวาแล้ว เปลี่ยนไปด้านซ้าย
            direction = -1;
        }
        else
        {
            // ถึงด้านซ้ายแล้ว ถ้าไม่วนซ้ำให้หยุด
            if (!loopMovement)
            {
                isMoving = false;
                return;
            }

            direction = 1;
        }

        ApplyDirectionFlip();
    }

    private void CarryPlayer(Vector2 delta)
    {
        if (standingPlayer == null || standingPlayer.movementLocked || delta.sqrMagnitude < 0.000001f)
            return;

        Rigidbody2D playerRb = standingPlayer.GetComponent<Rigidbody2D>();
        Collider2D playerCollider = standingPlayer.GetComponent<Collider2D>();

        if (playerCollider == null)
            playerCollider = standingPlayer.GetComponentInChildren<Collider2D>();

        if (playerRb == null || playerCollider == null)
            return;

        // ไม่พาผู้เล่นที่กำลังพุ่งขึ้น
        if (playerRb.linearVelocity.y > 0.1f)
            return;

        // delta ของแท่นเป็นแนวนอนเท่านั้น
        playerRb.MovePosition(playerRb.position + delta);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        MouseController player = collision.collider.GetComponentInParent<MouseController>();

        if (player == standingPlayer)
            standingPlayer = null;
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 startPosition = Application.isPlaying && rb != null
            ? rb.position
            : (Vector2)transform.position;

        Vector2 left = startPosition + Vector2.left * Mathf.Abs(leftDistance);
        Vector2 right = startPosition + Vector2.right * Mathf.Abs(rightDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPosition, 0.15f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(left, right);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(left, 0.15f);
        Gizmos.DrawWireSphere(right, 0.15f);
    }
}
