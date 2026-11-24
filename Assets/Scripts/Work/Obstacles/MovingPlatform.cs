using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class MovingPlatform2D : MonoBehaviour
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

    [Header("Player Hit Pause")]
    [Tooltip("เมื่อ Player สัมผัส Platform ให้หยุดการเคลื่อนที่ชั่วคราว")]
    [SerializeField] private bool pauseWhenPlayerTouches = true;

    [Tooltip("ระยะเวลาที่ Platform หยุดหลัง Player สัมผัส")]
    [Min(0f)]
    [SerializeField] private float playerHitPauseDuration = 1f;

    [Header("Player Carry")]
    [SerializeField] private float topTolerance = 0.15f;

    private Rigidbody2D rb;
    private Collider2D platformCollider;
    private SpriteRenderer sprite;
    private PlayerController standingPlayer;
    private readonly HashSet<Collider2D> playerContactColliders = new HashSet<Collider2D>();

    // เก็บค่าเดิมเพื่อให้สามารถสะท้อน Collider กลับไปมาได้โดยไม่เพี้ยนสะสม
    private bool originalSpriteFlipX;
    private Vector2 originalColliderOffset;
    private Vector2[][] originalPolygonPaths;
    private Vector2[] originalEdgePoints;

    private Vector2 leftPoint;
    private Vector2 rightPoint;
    private int direction = 1; // 1 = ขวา, -1 = ซ้าย
    private float waitTimer;
    private float playerHitPauseTimer;
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
        // หยุดทั้ง Platform และการพาผู้เล่น ขณะอยู่ในช่วง Hit Pause
        if (playerHitPauseTimer > 0f)
        {
            playerHitPauseTimer = Mathf.Max(0f, playerHitPauseTimer - Time.fixedDeltaTime);

            // ครบเวลาหยุดแล้ว เปลี่ยน Collider กลับเป็น Solid ก่อนให้แท่นเคลื่อนที่ต่อ
            if (playerHitPauseTimer <= 0f)
                platformCollider.isTrigger = false;

            return;
        }

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

        // ตรวจว่าผู้เล่นยังยืนอยู่ด้านบนของแท่น
        if (playerCollider.bounds.min.y < platformCollider.bounds.max.y - topTolerance)
            return;

        // delta ของแท่นเป็นแนวนอนเท่านั้น
        playerRb.MovePosition(playerRb.position + delta);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        PlayerController player = collision.collider.GetComponentInParent<PlayerController>();

        if (player != null && collision.collider != null)
        {
            bool isNewPlayerContact = playerContactColliders.Add(collision.collider);

            // หยุดหนึ่งครั้งเมื่อเริ่มสัมผัส ไม่ต่อเวลาใหม่ทุก Physics Frame
            if (isNewPlayerContact && pauseWhenPlayerTouches && playerHitPauseTimer <= 0f)
            {
                float pauseDuration = Mathf.Max(0f, playerHitPauseDuration);

                if (pauseDuration > 0f)
                {
                    playerHitPauseTimer = pauseDuration;

                    // เปลี่ยนเป็น Trigger ระหว่างหยุดเพราะผู้เล่นชน
                    platformCollider.isTrigger = true;
                }
                else
                {
                    // ถ้าไม่ได้ตั้งเวลาหยุด ให้ Collider คงเป็น Solid
                    platformCollider.isTrigger = false;
                }
            }
        }

        CheckPlayerOnPlatform(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckPlayerOnPlatform(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        PlayerController player = collision.collider.GetComponentInParent<PlayerController>();

        if (collision.collider != null)
            playerContactColliders.Remove(collision.collider);

        if (player == standingPlayer && !HasContactFromPlayer(player))
            standingPlayer = null;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        playerContactColliders.Remove(other);

        if (player == standingPlayer && !HasContactFromPlayer(player))
            standingPlayer = null;
    }

    private bool HasContactFromPlayer(PlayerController player)
    {
        if (player == null)
            return false;

        foreach (Collider2D contactCollider in playerContactColliders)
        {
            if (contactCollider != null &&
                contactCollider.GetComponentInParent<PlayerController>() == player)
                return true;
        }

        return false;
    }

    private void CheckPlayerOnPlatform(Collision2D collision)
    {
        PlayerController player = collision.collider.GetComponentInParent<PlayerController>();

        if (player == null)
            return;

        Collider2D playerCollider = collision.collider;
        bool onTop = playerCollider.bounds.min.y >= platformCollider.bounds.max.y - topTolerance;

        if (onTop)
            standingPlayer = player;
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
