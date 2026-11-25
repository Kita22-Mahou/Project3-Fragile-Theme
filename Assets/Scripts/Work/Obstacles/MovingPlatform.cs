using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Path")]
    [Tooltip("แต่ละ Step เป็นระยะเคลื่อนที่จากจุดก่อนหน้า สามารถกำหนดได้ทั้ง X และ Y")]
    [SerializeField]
    private Vector2[] movementSteps =
    {
        new Vector2(5f, 0f),
        new Vector2(0f, 3f),
        new Vector2(-5f, 0f),
        new Vector2(0f, -3f)
    };

    [Header("Movement Settings")]
    [Min(0f)]
    [SerializeField] private float moveSpeed = 3f;

    [Min(0f)]
    [SerializeField] private float waitTime = 0f;

    [SerializeField] private bool loopMovement = true;

    [SerializeField] private bool isFlipSprite = false;
    [SerializeField] private bool triggerWhenPlayerTouches = true;
    [Min(0f)]
    [SerializeField] private float playerTriggerDuration = 1f;

    private Rigidbody2D rb;
    private Collider2D platformCollider;
    private SpriteRenderer sprite;

    // เก็บ Collider ของ Player ที่กำลังสัมผัส เพื่อไม่เริ่ม Trigger ซ้ำ
    // จนกว่า Player จะแยกออกจาก Platform จริง ๆ
    private readonly HashSet<Collider2D> playerContactColliders = new HashSet<Collider2D>();
    private readonly List<Collider2D> contactsToRemove = new List<Collider2D>();

    // เก็บรูปทรงเดิมเพื่อสะท้อน Collider กลับไปมาโดยไม่เพี้ยนสะสม
    private bool originalSpriteFlipX;
    private Vector2 originalColliderOffset;
    private Vector2[][] originalPolygonPaths;
    private Vector2[] originalEdgePoints;

    private readonly List<Vector2> pathPoints = new List<Vector2>();

    private int targetIndex = 1;
    // 1 = เดินไปตามลำดับ Step, -1 = ย้อนกลับตาม Step เดิม
    private int pathDirection = 1;

    private float waitTimer;
    private float playerTriggerTimer;
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

        // เริ่มต้นเป็น Collider ปกติ และจะเปลี่ยนเป็น Trigger เมื่อ Player สัมผัสเท่านั้น
        platformCollider.isTrigger = false;

        BuildMovementPath();
    }

    private void BuildMovementPath()
    {
        pathPoints.Clear();
        pathPoints.Add(rb.position);

        if (movementSteps != null)
        {
            Vector2 point = rb.position;

            for (int i = 0; i < movementSteps.Length; i++)
            {
                Vector2 step = movementSteps[i];

                // ข้าม Step ที่ไม่มีระยะเคลื่อนที่
                if (step.sqrMagnitude < 0.000001f)
                    continue;

                point += step;
                pathPoints.Add(point);
            }
        }

        targetIndex = 1;
        pathDirection = 1;
        isMoving = pathPoints.Count > 1;

        if (isMoving)
            ApplyDirectionFlip(pathPoints[1] - pathPoints[0]);
        else
            RestoreOriginalFlip();
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

    private void ApplyDirectionFlip(Vector2 movementDirection)
    {
        if (!isFlipSprite || Mathf.Abs(movementDirection.x) < 0.0001f)
            return;

        bool flip = movementDirection.x < 0f;
        float xSign = flip ? -1f : 1f;

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

    private void RestoreOriginalFlip()
    {
        if (!isFlipSprite)
            return;

        if (sprite != null)
            sprite.flipX = originalSpriteFlipX;

        platformCollider.offset = originalColliderOffset;

        PolygonCollider2D polygon = platformCollider as PolygonCollider2D;
        if (polygon != null && originalPolygonPaths != null)
        {
            for (int i = 0; i < originalPolygonPaths.Length; i++)
                polygon.SetPath(i, originalPolygonPaths[i]);
        }

        EdgeCollider2D edge = platformCollider as EdgeCollider2D;
        if (edge != null && originalEdgePoints != null)
            edge.points = originalEdgePoints;
    }

    private void FixedUpdate()
    {
        CleanupSeparatedPlayerContacts();

        // นับเวลา Trigger แยกจากการเคลื่อนที่: Platform จะไม่หยุดระหว่างนี้
        if (playerTriggerTimer > 0f)
        {
            playerTriggerTimer = Mathf.Max(0f, playerTriggerTimer - Time.fixedDeltaTime);

            if (playerTriggerTimer <= 0f)
                platformCollider.isTrigger = false;
        }

        if (!isMoving || targetIndex < 0 || targetIndex >= pathPoints.Count)
            return;

        if (waitTimer > 0f)
        {
            waitTimer = Mathf.Max(0f, waitTimer - Time.fixedDeltaTime);
            return;
        }

        Vector2 destination = pathPoints[targetIndex];
        Vector2 nextPosition = Vector2.MoveTowards(
            rb.position,
            destination,
            Mathf.Max(0f, moveSpeed) * Time.fixedDeltaTime
        );

        rb.MovePosition(nextPosition);

        if (Vector2.Distance(nextPosition, destination) < 0.01f)
        {
            rb.MovePosition(destination);
            ReachWaypoint();
        }
    }

    private void ReachWaypoint()
    {
        waitTimer = Mathf.Max(0f, waitTime);

        if (pathPoints.Count <= 1)
        {
            isMoving = false;
            return;
        }

        int nextIndex = targetIndex + pathDirection;

        // ถึงปลายทางด้านใดด้านหนึ่งแล้ว ให้ย้อนกลับตาม Waypoint เดิม
        if (nextIndex >= pathPoints.Count || nextIndex < 0)
        {
            if (!loopMovement)
            {
                isMoving = false;
                return;
            }

            pathDirection *= -1;
            nextIndex = targetIndex + pathDirection;
        }

        targetIndex = nextIndex;

        // พลิก Sprite และ Collider ตามทิศทางแนวนอนของช่วงถัดไป
        if (targetIndex >= 0 && targetIndex < pathPoints.Count)
        {
            Vector2 nextDelta = pathPoints[targetIndex] - rb.position;
            ApplyDirectionFlip(nextDelta);
        }
    }

    private void HandlePlayerTouch(Collider2D other)
    {
        if (other == null || !triggerWhenPlayerTouches)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        bool isNewPlayerContact = playerContactColliders.Add(other);
        if (!isNewPlayerContact || playerTriggerTimer > 0f)
            return;

        float triggerDuration = Mathf.Max(0f, playerTriggerDuration);
        if (triggerDuration <= 0f)
            return;


        playerTriggerTimer = triggerDuration;
        platformCollider.isTrigger = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandlePlayerTouch(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandlePlayerTouch(other);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        RemoveContactIfSeparated(collision.collider);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        RemoveContactIfSeparated(other);
    }

    private void CleanupSeparatedPlayerContacts()
    {
        contactsToRemove.Clear();

        foreach (Collider2D contact in playerContactColliders)
        {
            if (contact == null)
            {
                contactsToRemove.Add(contact);
                continue;
            }

            ColliderDistance2D distance = platformCollider.Distance(contact);
            if (distance.distance > 0.02f)
                contactsToRemove.Add(contact);
        }

        for (int i = 0; i < contactsToRemove.Count; i++)
            playerContactColliders.Remove(contactsToRemove[i]);
    }

    private void RemoveContactIfSeparated(Collider2D other)
    {
        if (other == null || platformCollider == null)
            return;

        ColliderDistance2D distance = platformCollider.Distance(other);
        if (distance.distance > 0.02f)
            playerContactColliders.Remove(other);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = Application.isPlaying && rb != null
            ? rb.position
            : (Vector2)transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin, 0.15f);

        if (movementSteps == null)
            return;

        Vector2 previous = origin;

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