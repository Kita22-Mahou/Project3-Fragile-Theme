
using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class MovingPlatform2D : MonoBehaviour
{
    // =========================================================
    // MOVEMENT PATH
    // =========================================================

    [Header("Movement Path")]

    [Tooltip("ระยะเคลื่อนที่ในแต่ละช่วง วัดจากจุดก่อนหน้า")]
    [SerializeField]
    private Vector2[] movementSteps =
    {
        new Vector2(-5f, 0f),
        new Vector2(0f, 3f),
        new Vector2(5f, 0f)
    };

    [Header("Movement Settings")]

    [SerializeField] private float moveSpeed = 3f;

    [SerializeField] private float waitTime = 0.5f;

    [Tooltip("เปิดเพื่อให้แท่นย้อนกลับตามเส้นทางเดิม")]
    [SerializeField] private bool loopMovement = true;


    // =========================================================
    // PLAYER CARRY
    // =========================================================

    [Header("Player Carry")]

    [SerializeField] private float topTolerance = 0.15f;


    // =========================================================
    // COMPONENTS
    // =========================================================

    private Rigidbody2D rb;
    private Collider2D platformCollider;
    private MouseController standingPlayer;
    private SpriteRenderer sprite;

    [SerializeField] private bool isFlipSprite = false;

    // =========================================================
    // PATH STATE
    // =========================================================

    private Vector2[] pathPoints;

    private int targetIndex = 1;
    private int pathDirection = 1;

    private float waitTimer;

    private bool isMoving = true;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        platformCollider = GetComponent<Collider2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        sprite = GetComponent<SpriteRenderer>();

        BuildPath();
    }


    private void BuildPath()
    {
        Vector2 startPosition = rb.position;

        int stepCount = movementSteps != null ? movementSteps.Length : 0;

        pathPoints = new Vector2[stepCount + 1];
        pathPoints[0] = startPosition;

        for (int i = 0; i < stepCount; i++)
        {
            pathPoints[i + 1] = pathPoints[i] + movementSteps[i];
        }

        targetIndex = pathPoints.Length > 1 ? 1 : 0;
        pathDirection = 1;
        isMoving = pathPoints.Length > 1;

    }

    private void FixedUpdate()
    {
        if (!isMoving || pathPoints == null || targetIndex < 0 || targetIndex >= pathPoints.Length)
        {
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector2 destination = pathPoints[targetIndex];

        Vector2 oldPosition = rb.position;

        Vector2 nextPosition = Vector2.MoveTowards(
            oldPosition,
            destination,
            moveSpeed * Time.fixedDeltaTime
        );

        Vector2 delta = nextPosition - oldPosition;

        rb.MovePosition(nextPosition);

        CarryPlayer(delta);

        if (Vector2.Distance(nextPosition, destination) < 0.01f)
        {
            rb.MovePosition(destination);
            ReachWaypoint();
        }
    }

    private void ReachWaypoint()
    {
        waitTimer = Mathf.Max(0f, waitTime);

        targetIndex += pathDirection;

        if (targetIndex >= pathPoints.Length || targetIndex < 0)
        {
            if (!loopMovement)
            {
                isMoving = false;
                return;
            }

            // flip sprite
            if (isFlipSprite)
            {
                if (sprite.flipX)
                {
                    sprite.flipX = false;
                }
                else if (!sprite.flipX)
                {
                    sprite.flipX = true;
                }
            }

                // กลับทิศและวิ่งย้อนตามเส้นทางเดิมrite.flipX = false;
                pathDirection *= -1;
            targetIndex += pathDirection * 2;
        }
    }

    private void CarryPlayer(Vector2 delta)
    {
        if (standingPlayer == null || standingPlayer.movementLocked || delta.sqrMagnitude < 0.000001f)
        {
            return;
        }

        Rigidbody2D playerRb = standingPlayer.GetComponent<Rigidbody2D>();
        Collider2D playerCollider = standingPlayer.GetComponent<Collider2D>();

        if (playerRb == null || playerCollider == null)
        {
            return;
        }

        // ไม่บังคับพาผู้เล่นที่กำลังพุ่งขึ้น
        if (playerRb.linearVelocity.y > 0.1f)
        {
            return;
        }

        if (playerCollider.bounds.min.y < platformCollider.bounds.max.y - topTolerance)
        {
            return;
        }

        playerRb.MovePosition(playerRb.position + delta);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckPlayerOnPlatform(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckPlayerOnPlatform(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        MouseController player = collision.collider.GetComponentInParent<MouseController>();

        if (player == standingPlayer)
        {
            standingPlayer = null;
        }
    }

    private void CheckPlayerOnPlatform(Collision2D collision)
    {
        MouseController player = collision.collider.GetComponentInParent<MouseController>();

        if (player == null)
        {
            return;
        }

        Collider2D playerCollider = collision.collider;

        bool onTop = playerCollider.bounds.min.y >= platformCollider.bounds.max.y - topTolerance;

        if (onTop)
        {
            standingPlayer = player;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 position = Application.isPlaying && rb != null ? rb.position : (Vector2)transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(position, 0.15f);

        if (movementSteps == null)
        {
            return;
        }

        Vector2 previous = position;

        for (int i = 0; i < movementSteps.Length; i++)
        {
            Vector2 next = previous + movementSteps[i];

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(previous, next);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(next, 0.15f);

            previous = next;
        }
    }
}
