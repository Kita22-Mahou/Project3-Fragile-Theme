
using UnityEngine;
using UnityEngine.UI;

public class MouseController : MonoBehaviour
{
    private enum JumpState
    {
        Grounded,
        Aiming,
        Jumping
    }

    private JumpState jumpState = JumpState.Grounded;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    private float moveInputX;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.2f);
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private bool isGrounded;

    [Header("Jump")]
    [SerializeField] private float minJumpForce = 10f;
    [SerializeField] private float maxJumpForce = 26f;
    [SerializeField] private float maxChargeTime = 0.7f;
    [SerializeField] private float jumpForwardForce = 14f;
    private float jumpChargeTime;
    private bool hasLeftGround;

    [Header("Mouse Aim")]
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

    [Header("Power UI")]
    [SerializeField] private Scrollbar chargeBar;
    [SerializeField] private Image chargeBarHandle;
    [SerializeField] private Gradient chargeColor;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private Camera mainCamera;

    #region Event System
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        if (playerCollider == null)
            playerCollider = GetComponentInChildren<Collider2D>();

        mainCamera = Camera.main;
        aimDirection = GetDefaultAimDirection();

        CreateTrajectoryDots();
        ResetPowerBar();
    }

    private void Update()
    {
        moveInputX = Input.GetAxisRaw("Horizontal");
        CheckGround();

        if (jumpState == JumpState.Jumping)
        {
            if (!isGrounded)
                hasLeftGround = true;

            if (hasLeftGround && isGrounded)
            {
                jumpState = JumpState.Grounded;
                hasLeftGround = false;
            }
        }

        if (jumpState == JumpState.Grounded)
            UpdateFacingDirection();

        if (Input.GetMouseButtonDown(0) &&
            isGrounded &&
            jumpState == JumpState.Grounded) //&&
            //IsMouseOverTumi())
        {
            jumpState = JumpState.Aiming;
            jumpChargeTime = 0f;
            aimDirection = GetDefaultAimDirection();
            ResetPowerBar();
        }

        if (jumpState == JumpState.Aiming &&
            Input.GetMouseButtonDown(1))
        {
            CancelJumpCharge();
        }

        // ชาร์จกระโดด
        if (jumpState == JumpState.Aiming)
        {
            if (Input.GetMouseButton(0))
            {
                jumpChargeTime += Time.deltaTime;
                jumpChargeTime = Mathf.Clamp(
                    jumpChargeTime,
                    0f,
                    maxChargeTime
                );

                UpdateAimFromMouse();
                UpdatePowerBar();
            }
            else
            {
                UpdateAimFromMouse();
                Jump();
            }
        }

        UpdateJumpTrajectory();
    }

    private void FixedUpdate()
    {
        Move();
    }
    #endregion

    #region Walk Mechanic
    private void Move()
    {
        if (jumpState == JumpState.Jumping)
            return;

        if (jumpState == JumpState.Aiming)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(moveInputX * moveSpeed, rb.linearVelocity.y);
    }

    private void UpdateFacingDirection()
    {
        if (moveInputX > 0f)
        {
            facingDirection = 1f;

            if (playerSprite != null)
                playerSprite.flipX = false;
        }
        else if (moveInputX < 0f)
        {
            facingDirection = -1f;

            if (playerSprite != null)
                playerSprite.flipX = true;
        }
    }

    private void CheckGround()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapBox(
        groundCheck.position,
        groundCheckSize,
        0f,
        groundLayer
        ) != null;

        //isGrounded = Physics2D.OverlapCircle(
        //    groundCheck.position,
        //    groundCheckRadius,
        //    groundLayer
        //);
    }
    #endregion

    #region Jump Mechanic
    private void UpdateAimFromMouse()
    {
        Vector2 direction = GetMouseWorldPosition() - rb.position;

        if (direction.sqrMagnitude < 0.04f)
            return;

        float angle = Mathf.Atan2(
            direction.y,
            direction.x
        ) * Mathf.Rad2Deg;

        angle = Mathf.Clamp(angle, minAimAngle, maxAimAngle);

        float radians = angle * Mathf.Deg2Rad;

        aimDirection = new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians)
        ).normalized;

        UpdateAimFacingDirection();
    }

    private void UpdateAimFacingDirection()
    {
        if (Mathf.Abs(aimDirection.x) < 0.01f)
            return;

        facingDirection = Mathf.Sign(aimDirection.x);

        if (playerSprite != null)
            playerSprite.flipX = facingDirection < 0f;
    }

    private Vector2 GetDefaultAimDirection()
    {
        Vector2 direction = new Vector2(
            facingDirection * jumpForwardForce,
            minJumpForce
        );

        return direction.normalized;
    }

    private float GetChargePercent()
    {
        return maxChargeTime > 0f
            ? Mathf.Clamp01(jumpChargeTime / maxChargeTime)
            : 1f;
    }

    private float GetLaunchSpeed()
    {
        float jumpForce = Mathf.Lerp(
            minJumpForce,
            maxJumpForce,
            GetChargePercent()
        );

        return new Vector2(
            jumpForwardForce,
            jumpForce
        ).magnitude;
    }

    private void UpdatePowerBar()
    {
        float chargePercent = GetChargePercent();

        if (chargeBar != null)
        {
            chargeBar.value = 0f;
            chargeBar.size = chargePercent;
        }

        if (chargeBarHandle != null && chargeColor != null)
        {
            chargeBarHandle.color =
                chargeColor.Evaluate(chargePercent);
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (mainCamera == null)
            return rb.position;

        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = -mainCamera.transform.position.z;

        Vector3 worldPosition =
            mainCamera.ScreenToWorldPoint(screenPosition);

        return new Vector2(
            worldPosition.x,
            worldPosition.y
        );
    }

    //private bool IsMouseOverTumi()
    //{
    //    if (playerCollider == null || mainCamera == null)
    //        return false;

    //    return playerCollider.OverlapPoint(
    //        GetMouseWorldPosition()
    //    );
    //}

    private void Jump()
    {
        float launchSpeed = GetLaunchSpeed();

        jumpState = JumpState.Jumping;
        hasLeftGround = false;

        rb.linearVelocity = aimDirection * launchSpeed;

        jumpChargeTime = 0f;
        ResetPowerBar();
        HideJumpTrajectory();
    }

    private void ResetPowerBar()
    {
        if (chargeBar != null)
        {
            chargeBar.value = 0f;
            chargeBar.size = 0f;
        }

        if (chargeBarHandle != null && chargeColor != null)
            chargeBarHandle.color = chargeColor.Evaluate(0f);
    }
    #endregion

    #region Jump Trajectory
    // สร้างจุดวิถีกระโดด
    private void CreateTrajectoryDots()
    {
        if (trajectoryDotPrefab == null)
            return;

        trajectoryDots = new Transform[trajectoryDotCount];

        for (int i = 0; i < trajectoryDots.Length; i++)
        {
            GameObject dot = Instantiate(trajectoryDotPrefab);
            trajectoryDots[i] = dot.transform;
            dot.SetActive(false);
        }
    }

    private void UpdateJumpTrajectory()
    {
        if (trajectoryDots == null)
            return;

        if (jumpState != JumpState.Aiming)
        {
            HideJumpTrajectory();
            return;
        }

        float launchSpeed = GetLaunchSpeed();
        Vector2 startPosition = trajectoryPoint.transform.position;
        Vector2 initialVelocity = aimDirection * launchSpeed;
        float gravityY = Physics2D.gravity.y * rb.gravityScale;
        Vector2 previousPoint = startPosition;

        for (int i = 0; i < trajectoryDots.Length; i++)
        {
            float time = (i + 1) * trajectoryTimeStep;

            Vector2 point = startPosition
                + initialVelocity * time
                + Vector2.up * (0.5f * gravityY * time * time);

            trajectoryDots[i].gameObject.SetActive(true);
            trajectoryDots[i].position = point;

            Vector2 segment = point - previousPoint;

            RaycastHit2D hit = Physics2D.Raycast(
                previousPoint,
                segment.normalized,
                segment.magnitude,
                groundLayer
            );

            if (hit.collider != null)
            {
                trajectoryDots[i].position = hit.point;

                for (int j = i + 1; j < trajectoryDots.Length; j++)
                    trajectoryDots[j].gameObject.SetActive(false);

                break;
            }

            previousPoint = point;
        }
    }

    private void HideJumpTrajectory()
    {
        if (trajectoryDots == null)
            return;

        foreach (Transform dot in trajectoryDots)
        {
            if (dot != null)
                dot.gameObject.SetActive(false);
        }
    }


    private void CancelJumpCharge()
    {
        jumpState = JumpState.Grounded;
        jumpChargeTime = 0f;

        ResetPowerBar();
        HideJumpTrajectory();
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        //Gizmos.DrawWireSphere(
        //    groundCheck.position,
        //    groundCheckRadius
        //);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            groundCheck.position,
            groundCheckSize
        );
    }
}
