
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    private enum JumpState
    {
        Grounded,
        Charging,
        Jumping
    }

    private JumpState jumpState = JumpState.Grounded;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float moveInputX;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private bool isGrounded;

    [Header("Jump")]
    [SerializeField] private float coyoteDragDownAmount;
    [SerializeField] private float minJumpForce = 5f;
    [SerializeField] private float maxJumpForce = 12f;
    [SerializeField] private float maxChargeTime = 0.6f;
    [SerializeField] private float jumpForwardForce = 8f;
    [SerializeField] private float jumpChargeTime;
    [SerializeField] private bool hasLeftGround;

    [Header("Jump Trajectory")]
    [SerializeField] private GameObject trajectoryPoint;
    [SerializeField] private GameObject trajectoryDotPrefab;
    [SerializeField] private int trajectoryDotCount = 36;
    [SerializeField] private float trajectoryTimeStep = 0.05f;
    [SerializeField] private Transform[] trajectoryDots;
    

    [Header("Flip")]
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private float facingDirection = 1f;

    [Header("Jump Charge UI")]
    [SerializeField] private Scrollbar chargeBar;
    [SerializeField] private Image chargeBarHandle;
    [SerializeField] private Gradient chargeColor;

    private Rigidbody2D rb;

    #region Event System
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        CreateTrajectoryDots();
        ResetChargeBar();
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

        ChargeJump();
        //CoyoteDragDown();
        UpdateJumpTrajectory();
    }

    private void FixedUpdate()
    {
        Move();
    }
    #endregion

    #region Movement
    private void Move()
    {
        if (jumpState == JumpState.Jumping)
            return;

        if (jumpState == JumpState.Charging)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(moveInputX * moveSpeed,rb.linearVelocity.y);
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

        isGrounded = Physics2D.OverlapCircle(groundCheck.position,groundCheckRadius,groundLayer);
    }
    #endregion

    #region Jump Movement
    private void CoyoteDragDown()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * coyoteDragDownAmount);
    }

    private void Jump()
    {
        float chargePercent = maxChargeTime > 0f
            ? Mathf.Clamp01(jumpChargeTime / maxChargeTime)
            : 1f;

        float jumpForce = Mathf.Lerp(minJumpForce,maxJumpForce,chargePercent);

        jumpState = JumpState.Jumping;
        hasLeftGround = false;

        rb.linearVelocity = new Vector2(facingDirection * jumpForwardForce, jumpForce);

        jumpChargeTime = 0f;
        ResetChargeBar();
    }

    private void ChargeJump()
    {
        if (jumpState != JumpState.Jumping)
            UpdateFacingDirection();

        if (Input.GetKeyDown(KeyCode.Space) &&
            isGrounded &&
            jumpState == JumpState.Grounded)
        {
            jumpState = JumpState.Charging;
            jumpChargeTime = 0f;
            ResetChargeBar();
        }

        // ชาร์จกระโดด
        if (jumpState == JumpState.Charging)
        {
            if (Input.GetKey(KeyCode.Space))
            {
                jumpChargeTime += Time.deltaTime;
                jumpChargeTime = Mathf.Clamp(jumpChargeTime, 0f, maxChargeTime);

                float chargePercent = maxChargeTime > 0f
                    ? Mathf.Clamp01(jumpChargeTime / maxChargeTime)
                    : 1f;

                if (chargeBar != null)
                {
                    chargeBar.value = 0f;
                    chargeBar.size = chargePercent;
                }

                if (chargeBarHandle != null && chargeColor != null)
                    chargeBarHandle.color = chargeColor.Evaluate(chargePercent);
            }
            else
            {
                Jump();
            }
        }
    }

    private void ResetChargeBar()
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

    #region Trajectory Line
    // สร้างจุดวิถีกระโดด
    private void CreateTrajectoryDots()
    {
        if (trajectoryDotPrefab == null)
            return;

        trajectoryDots = new Transform[trajectoryDotCount];

        for (int i = 0; i < trajectoryDots.Length; i++)
        {
            GameObject dot = Instantiate(trajectoryDotPrefab);
            dot.transform.SetParent(trajectoryPoint.transform, false);
            trajectoryDots[i] = dot.transform;
            dot.SetActive(false);
        }
    }

    private void UpdateJumpTrajectory()
    {
        if (trajectoryDots == null)
            return;

        if (jumpState != JumpState.Charging)
        {
            HideJumpTrajectory();
            return;
        }

        float chargePercent = maxChargeTime > 0f
            ? Mathf.Clamp01(jumpChargeTime / maxChargeTime)
            : 1f;

        float jumpForce = Mathf.Lerp( minJumpForce, maxJumpForce,chargePercent);

        Vector2 startPosition = trajectoryPoint.transform.position;

        Vector2 initialVelocity = new Vector2(
            facingDirection * jumpForwardForce,
            jumpForce
        );

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
    #endregion

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