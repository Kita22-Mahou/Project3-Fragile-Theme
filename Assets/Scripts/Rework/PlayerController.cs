using UnityEngine;
using static UnityEngine.UI.Image;

public class PlayerController : MonoBehaviour
{
    private enum JumpState
    {
        OnGround,
        Aiming,
        InAir,
        AirAiming
    }

    private JumpState jumpState = JumpState.OnGround;

    [SerializeField] private Animator animator;

    [Header("Checks")]
    [SerializeField] private bool isOnGround;
    [SerializeField] private bool isAiming;
    [SerializeField] private bool isJumped;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckX = 0.2f;
    [SerializeField] private float groundCheckY = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Setting")]
    [SerializeField] private float minJumpForce = 10f;
    [SerializeField] private float maxJumpForce = 26f;
    [SerializeField] private float maxChargeTime = 0.7f;
    [SerializeField] private float jumpForwardForce = 14f;

    private float AirAimingMaxChargeTime;
    private float NormalMaxChargeTime;
    private float jumpChargeTime;

    [Header("Mouse Aim Setting")]
    [SerializeField] private float minAimAngle = 15f;
    [SerializeField] private float maxAimAngle = 165f;
    private Vector2 aimDirection;

    [Header("Jump Trajectory")]
    [SerializeField] private GameObject trajectoryPoint;
    [SerializeField] private GameObject trajectoryDotPrefab;
    [SerializeField] private int trajectoryDotCount = 36;
    [SerializeField] private float trajectoryTimeStep = 0.05f;
    private Transform[] trajectoryDots;

    [Header("Flip")]
    [SerializeField] private SpriteRenderer playerSprite;
    public float facingDirection = 1f;

    [HideInInspector]
    private Rigidbody2D rb;
    private Camera mainCamera;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
    }

    void Start()
    {

    }


    void Update()
    {
        // เริ่มเล็ง
        if (Input.GetMouseButtonDown(0) && isOnGround && jumpState == JumpState.OnGround)
        {
            Aiming();
        }
        if (Input.GetMouseButtonUp(0) && isOnGround && jumpState == JumpState.OnGround)
        {
            Jump();
        }
        Grounded();
    }

    public bool Grounded()
    {
        if (jumpState == JumpState.InAir)
            return isOnGround = false;

        if (Physics2D.Raycast(groundCheckPoint.position, Vector2.down, groundCheckY, groundLayer)
            || Physics2D.Raycast(groundCheckPoint.position + new Vector3(groundCheckX, 0, 0), Vector2.down, groundCheckY, groundLayer)
            || Physics2D.Raycast(groundCheckPoint.position + new Vector3(groundCheckX, 0, 0), Vector2.down, groundCheckY, groundLayer))
        {
            return isOnGround = true;
        }
        else
        {
            return isOnGround = false;
        }
    }

    // 1 เล็งแล้ว update ตำแหน่งของ mouse
    public void Aiming()
    {
        if (!isOnGround || isJumped)
            return;

        isAiming = true;

        jumpChargeTime += Time.deltaTime;
        jumpChargeTime = Mathf.Clamp(
                    jumpChargeTime,
                    0f,
                    maxChargeTime
                );

        UpdateAimFromMouse();
    }


    private Vector2 GetDefaultAimDirection()
    {
        Vector2 direction = new Vector2(
            facingDirection * jumpForwardForce,
            minJumpForce
        );

        return direction.normalized;
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (mainCamera == null)
            return rb.position;

        Vector3 screenPosition = Input.mousePosition;

        screenPosition.z = -mainCamera.transform.position.z;

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);

        return new Vector2(
            worldPosition.x,
            worldPosition.y
        );
    }

    private void UpdateAimFromMouse()
    {
        Vector2 direction = GetMouseWorldPosition() - rb.position;

        if (direction.sqrMagnitude < 0.04f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (angle < 0f)
            angle += 360f;

        // จำกัดมุมให้อยู่ในครึ่งวงกลมด้านบน
        if (angle > 180f)
        {
            angle = direction.x >= 0f
                ? minAimAngle
                : maxAimAngle;
        }

        angle = Mathf.Clamp(
            angle,
            minAimAngle,
            maxAimAngle
        );

        float radians = angle * Mathf.Deg2Rad;

        aimDirection = new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians)
        ).normalized;
    }

    private void UpdateFacingDirection()
    {
        Vector2 mousePos = GetMouseWorldPosition();
        if (mousePos.x > 0f)
        {
            facingDirection = 1f;

            if (playerSprite != null)
                playerSprite.flipX = false;
        }
        else if (mousePos.x < 0f)
        {
            facingDirection = -1f;

            if (playerSprite != null)
                playerSprite.flipX = true;
        }
    }

    private void OnDrawGizmos()
    {
        if (groundCheckPoint == null) return;

        Vector3 origin1 = groundCheckPoint.position;
        Vector3 origin2 = groundCheckPoint.position + new Vector3(-groundCheckX, 0, 0);
        Vector3 origin3 = groundCheckPoint.position + new Vector3(groundCheckX, 0, 0);

        // Center Ray
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin1, Vector3.down * groundCheckY);

        // Left Ray
        Gizmos.color = Color.green;
        Gizmos.DrawRay(origin2, Vector3.down * groundCheckY);

        // Right Ray
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(origin3, Vector3.down * groundCheckY);
    }
}
