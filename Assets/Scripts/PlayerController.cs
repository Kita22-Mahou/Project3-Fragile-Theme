
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
    public float moveSpeed = 5f;
    private float moveInputX;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    public bool isGrounded;

    [Header("Jump")]
    public float minJumpForce = 5f;
    public float maxJumpForce = 12f;
    public float maxChargeTime = 0.6f;
    public float jumpForwardForce = 8f;
    private float jumpChargeTime;
    private bool hasLeftGround;

    [Header("Jump Trajectory")]
    public GameObject trajectoryDotPrefab;
    public int trajectoryDotCount = 36;
    public float trajectoryTimeStep = 0.05f;
    private Transform[] trajectoryDots;

    [Header("Flip")]
    public SpriteRenderer playerSprite;
    public float facingDirection = 1f;

    [Header("Jump Charge UI")]
    public Scrollbar chargeBar;
    public Image chargeBarHandle;
    public Gradient chargeColor;

    private Rigidbody2D rb;

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
                jumpChargeTime = Mathf.Clamp(jumpChargeTime,0f,maxChargeTime);

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

        UpdateJumpTrajectory();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void Move()
    {
        if (jumpState == JumpState.Jumping)
            return;

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

        if (jumpState != JumpState.Charging)
        {
            HideJumpTrajectory();
            return;
        }

        float chargePercent = maxChargeTime > 0f
            ? Mathf.Clamp01(jumpChargeTime / maxChargeTime)
            : 1f;

        float jumpForce = Mathf.Lerp( minJumpForce, maxJumpForce,chargePercent);

        Vector2 startPosition = rb.position;

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