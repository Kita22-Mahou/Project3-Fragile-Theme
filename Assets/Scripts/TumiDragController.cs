
using UnityEngine;
using UnityEngine.UI;

public class TumiDragController : MonoBehaviour
{
    private enum JumpState
    {
        Grounded,
        Aiming,
        Jumping
    }

    private JumpState jumpState = JumpState.Grounded;

    [Header("Movement")]
    public float moveSpeed = 10f;
    private float moveInputX;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.3f;
    public LayerMask groundLayer;
    public bool isGrounded;

    [Header("Jump")]
    public float minJumpForce = 10f;
    public float maxJumpForce = 26f;
    public float jumpForwardForce = 14f;
    public float maxDragDistance = 3f;
    private float dragPowerPercent;
    private bool hasLeftGround;

    [Header("Jump Trajectory")]
    public GameObject trajectoryDotPrefab;
    public int trajectoryDotCount = 36;
    public float trajectoryTimeStep = 0.05f;
    private Transform[] trajectoryDots;

    [Header("Flip")]
    public SpriteRenderer playerSprite;
    public float facingDirection = 1f;

    [Header("Power UI")]
    public Scrollbar chargeBar;
    public Image chargeBarHandle;
    public Gradient chargeColor;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private Camera mainCamera;

    private Vector2 dragStartMouseWorld;
    private Vector2 aimDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        if (playerCollider == null)
            playerCollider = GetComponentInChildren<Collider2D>();

        mainCamera = Camera.main;

        CreateTrajectoryDots();
        ResetPowerBar();

        aimDirection = GetDefaultAimDirection(minJumpForce);
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
            jumpState == JumpState.Grounded &&
            IsMouseOverTumi())
        {
            jumpState = JumpState.Aiming;
            dragStartMouseWorld = GetMouseWorldPosition();
            dragPowerPercent = 0f;
            aimDirection = GetDefaultAimDirection(minJumpForce);
            ResetPowerBar();
        }

        // กำหนดแรงจากระยะลาก
        if (jumpState == JumpState.Aiming)
        {
            if (Input.GetMouseButton(0))
            {
                UpdateAimFromDrag();
            }
            else
            {
                UpdateAimFromDrag();
                Jump();
            }
        }

        UpdateJumpTrajectory();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void Move()
    {
        if (jumpState != JumpState.Grounded)
            return;

        rb.linearVelocity = new Vector2(
            moveInputX * moveSpeed,
            rb.linearVelocity.y
        );
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

    private void UpdateAimFromDrag()
    {
        Vector2 dragDelta =
            GetMouseWorldPosition() - dragStartMouseWorld;

        float dragDistance = dragDelta.magnitude;

        dragPowerPercent = maxDragDistance > 0f
            ? Mathf.Clamp01(dragDistance / maxDragDistance)
            : 1f;

        if (dragDistance > 0.05f)
        {
            aimDirection = -dragDelta.normalized;

            // บังคับให้วิถีเริ่มพุ่งขึ้น ไม่ยิงลงพื้น
            aimDirection.y = Mathf.Max(aimDirection.y, 0.15f);
            aimDirection.Normalize();

            UpdateAimFacingDirection();
        }

        if (chargeBar != null)
        {
            chargeBar.value = 0f;
            chargeBar.size = dragPowerPercent;
        }

        if (chargeBarHandle != null && chargeColor != null)
        {
            chargeBarHandle.color =
                chargeColor.Evaluate(dragPowerPercent);
        }
    }

    private void UpdateAimFacingDirection()
    {
        if (Mathf.Abs(aimDirection.x) < 0.01f)
            return;

        facingDirection = Mathf.Sign(aimDirection.x);

        if (playerSprite != null)
            playerSprite.flipX = facingDirection < 0f;
    }

    private Vector2 GetDefaultAimDirection(float jumpForce)
    {
        Vector2 direction = new Vector2(
            facingDirection * jumpForwardForce,
            jumpForce
        );

        return direction.normalized;
    }

    private float GetLaunchSpeed(float jumpForce)
    {
        return new Vector2(
            jumpForwardForce,
            jumpForce
        ).magnitude;
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (mainCamera == null)
            return rb.position;

        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = -mainCamera.transform.position.z;

        Vector3 worldPosition =
            mainCamera.ScreenToWorldPoint(screenPosition);

        return new Vector2(worldPosition.x, worldPosition.y);
    }

    private bool IsMouseOverTumi()
    {
        if (playerCollider == null || mainCamera == null)
            return false;

        Vector2 mousePosition = GetMouseWorldPosition();

        return playerCollider.OverlapPoint(mousePosition);
    }

    private void CheckGround()
    {
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );
    }

    private void Jump()
    {
        float jumpForce = Mathf.Lerp(
            minJumpForce,
            maxJumpForce,
            dragPowerPercent
        );

        float launchSpeed = GetLaunchSpeed(jumpForce);

        jumpState = JumpState.Jumping;
        hasLeftGround = false;

        rb.linearVelocity = aimDirection.normalized * launchSpeed;

        dragPowerPercent = 0f;
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

        float jumpForce = Mathf.Lerp(
            minJumpForce,
            maxJumpForce,
            dragPowerPercent
        );

        float launchSpeed = GetLaunchSpeed(jumpForce);

        Vector2 startPosition = rb.position;
        Vector2 initialVelocity = aimDirection.normalized * launchSpeed;
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

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.DrawWireSphere(
            groundCheck.position,
            groundCheckRadius
        );
    }
}