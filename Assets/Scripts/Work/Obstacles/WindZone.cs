using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(BoxCollider2D))]
public class WindZone : MonoBehaviour
{
    // =========================================================
    // WIND SETTINGS
    // =========================================================

    [Header("Wind Settings")]

    [SerializeField] private Vector2 firstWindDirection = Vector2.left;

    [SerializeField] private Vector2 secondWindDirection = Vector2.right;

    [Tooltip("อัตราเร่งของลมกลางอากาศ")]
    [SerializeField] private float windAcceleration = 8f;

    [Tooltip("ความเร็วผลักเพิ่มเมื่อผู้เล่นยืนบนพื้น")]
    [SerializeField] private float groundWindPushSpeed = 3f;

    [Tooltip("ระยะเวลาก่อนสลับทิศลม")]
    [SerializeField] private float directionChangeInterval = 2.5f;


    // =========================================================
    // STOP POINT
    // =========================================================

    [Header("Wind Stop Point")]

    [Tooltip("ลาก WindStop ที่เป็นจุดสิ้นสุดจริงมาใส่")]
    [SerializeField] private Transform stopPoint;

    [Tooltip("ทิศที่ผู้เล่นต้องผ่านจุด เช่น ขวา = (1, 0)")]
    [SerializeField] private Vector2 progressDirection = Vector2.right;


    // =========================================================
    // DEBUG
    // =========================================================

    [Header("Debug")]

    [SerializeField] private bool showDebugLog = true;


    // =========================================================
    // STATE
    // =========================================================

    private readonly HashSet<MouseController> playersInside = new HashSet<MouseController>();

    private readonly Dictionary<MouseController, float> previousProgress = new Dictionary<MouseController, float>();

    private int directionIndex;

    private float switchTimer;

    private bool windStopped;

    public Vector2 CurrentWindDirection => GetCurrentDirection();

    public bool IsWindStopped => windStopped;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }


    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;

        windAcceleration = Mathf.Max(0f, windAcceleration);
        groundWindPushSpeed = Mathf.Max(0f, groundWindPushSpeed);
        directionChangeInterval = Mathf.Max(0.1f, directionChangeInterval);

        firstWindDirection = firstWindDirection.normalized;
        secondWindDirection = secondWindDirection.normalized;

        switchTimer = directionChangeInterval;

        if (stopPoint == null)
        {
            Debug.LogWarning("WindZone: ยังไม่ได้กำหนด Stop Point!", this);
        }
    }

    private void Update()
    {
        if (windStopped || playersInside.Count == 0)
        {
            return;
        }

        // ตรวจการข้ามจุดหยุดลมก่อน
        if (HasCrossedStopPoint())
        {
            StopWindPermanently();
            return;
        }

        // สลับทิศทางลมตามเวลา
        switchTimer -= Time.deltaTime;

        if (switchTimer <= 0f)
        {
            directionIndex = 1 - directionIndex;
            switchTimer = directionChangeInterval;

            if (showDebugLog)
            {
                Debug.Log("Wind changed direction: " + GetCurrentDirection(), this);
            }
        }
    }

    private void FixedUpdate()
    {
        if (windStopped || playersInside.Count == 0)
        {
            return;
        }

        Vector2 windDirection = GetCurrentDirection();

        foreach (MouseController player in playersInside)
        {
            if (player == null || player.movementLocked)
            {
                continue;
            }

            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();

            if (playerRb == null)
            {
                continue;
            }

            if (player.isGrounded)
            {
                // บวกความเร็วลมหลัง MouseController คำนวณการเดิน
                float windSpeed = windDirection.x * groundWindPushSpeed;

                playerRb.linearVelocity = new Vector2(
                    playerRb.linearVelocity.x + windSpeed,
                    playerRb.linearVelocity.y
                );
            }
            else
            {
                // กลางอากาศเพิ่มความเร็วจากอัตราเร่งของลม
                Vector2 velocityChange = windDirection * windAcceleration * Time.fixedDeltaTime;

                playerRb.linearVelocity += velocityChange;
            }
        }
    }


    private Vector2 GetCurrentDirection()
    {
        Vector2 direction = directionIndex == 0 ? firstWindDirection : secondWindDirection;

        return direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        MouseController player = other.GetComponentInParent<MouseController>();

        if (player == null || windStopped || !playersInside.Add(player))
        {
            return;
        }

        // จำตำแหน่งเริ่มต้นเมื่อเข้าพื้นที่
        if (stopPoint != null)
        {
            previousProgress[player] = GetProgress(player);
        }

        if (playersInside.Count == 1)
        {
            directionIndex = 0;
            switchTimer = directionChangeInterval;
        }

        if (showDebugLog)
        {
            Debug.Log("Player entered wind zone. Wind is active.", this);
        }
    }


    private void OnTriggerExit2D(Collider2D other)
    {
        MouseController player = other.GetComponentInParent<MouseController>();

        if (player == null)
        {
            return;
        }

        playersInside.Remove(player);
        previousProgress.Remove(player);
    }
    private float GetProgress(MouseController player)
    {
        Vector2 direction = progressDirection.normalized;

        Vector2 offset = (Vector2)player.transform.position - (Vector2)stopPoint.position;

        return Vector2.Dot(offset, direction);
    }


    private bool HasCrossedStopPoint()
    {
        if (stopPoint == null || progressDirection.sqrMagnitude < 0.001f)
        {
            return false;
        }

        foreach (MouseController player in playersInside)
        {
            if (player == null)
            {
                continue;
            }

            float currentProgress = GetProgress(player);

            if (!previousProgress.TryGetValue(player, out float oldProgress))
            {
                previousProgress[player] = currentProgress;
                continue;
            }

            // ต้องเคยอยู่ก่อนจุด แล้วค่อยข้ามไปอีกฝั่ง
            if (oldProgress < 0f && currentProgress >= 0f)
            {
                return true;
            }

            previousProgress[player] = currentProgress;
        }

        return false;
    }


    private void StopWindPermanently()
    {
        windStopped = true;

        playersInside.Clear();
        previousProgress.Clear();

        if (showDebugLog)
        {
            Debug.Log("Wind stopped: player crossed Stop Point.", this);
        }
    }

    private void OnDisable()
    {
        playersInside.Clear();
        previousProgress.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D zoneCollider = GetComponent<BoxCollider2D>();

        if (zoneCollider != null)
        {
            Gizmos.color = Color.cyan;

            Vector3 center = transform.TransformPoint(zoneCollider.offset);
            Vector3 size = Vector3.Scale(zoneCollider.size, transform.lossyScale);

            Gizmos.DrawWireCube(center, size);
        }

        if (stopPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(stopPoint.position, 0.4f);

            Gizmos.DrawLine(
                stopPoint.position,
                stopPoint.position + (Vector3)progressDirection.normalized
            );
        }
    }
}