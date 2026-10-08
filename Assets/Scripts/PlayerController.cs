using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    private enum JumpState
    {
        Grounded,//ติดดิน
        Charging,//ชาร์จ
        Jumping//กำลังกระโดด
    }

    private JumpState jumpState = JumpState.Grounded;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Jump")]
    public float minJumpForce = 5f;
    public float maxJumpForce = 12f;
    public float maxChargeTime = 1f;
    public float jumpForwardForce = 8f;

    [Header("Flip")]
    public SpriteRenderer playerSprite;
    public float facingDirection = 1f;

    [Header("Jump Charge UI")]
    public Scrollbar chargeBar;
    public Image chargeBarHandle;
    public Gradient chargeColor;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    public bool isGrounded;
    private float jumpChargeTime;
    private bool hasLeftGround;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (chargeBar != null)
        {
            chargeBar.value = 0f;
            chargeBar.size = 0f;
        }

        if (chargeBarHandle != null)
        {
            chargeBarHandle.color = chargeColor.Evaluate(0f);
        }
    }

    private void Update()
    {
        CheckGround();

        if (jumpState == JumpState.Jumping)
        {
           
            if (!isGrounded)
            {
                hasLeftGround = true;
            }

            if (hasLeftGround && isGrounded)
            {
                jumpState = JumpState.Grounded;
                hasLeftGround = false;
            }
        }

        if (jumpState == JumpState.Charging)
        {
            
            jumpChargeTime += Time.deltaTime;

            
            jumpChargeTime = Mathf.Clamp(
                jumpChargeTime,
                0f,
                maxChargeTime
            );

            float chargePercent = jumpChargeTime / maxChargeTime;

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
    }

    private void FixedUpdate()
    {
        Move();

        if (jumpState == JumpState.Jumping)
        {
            rb.AddForce(Vector2.down * 40f, ForceMode2D.Force);
            //rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.25f);

        }
    }

    private void Move()
    {
        if (jumpState == JumpState.Jumping)
        {
            return;
        }

        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed,rb.linearVelocity.y);

        if (moveInput.x > 0)
        {
            
            facingDirection = 1f;

            if (playerSprite != null)
            {
                playerSprite.flipX = false;
            }
        }

        else if (moveInput.x < 0)
        {
            
            facingDirection = -1f;

            if (playerSprite != null)
            {
                playerSprite.flipX = true;
            }
        }
    }

    private void CheckGround()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position,groundCheckRadius,groundLayer);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void Jump()
    {

        float chargePercent = jumpChargeTime / maxChargeTime;

        float jumpForce = Mathf.Lerp(minJumpForce,maxJumpForce,chargePercent);

        jumpState = JumpState.Jumping;

        hasLeftGround = false;

        rb.linearVelocity = new Vector2(facingDirection * jumpForwardForce,jumpForce);

        jumpChargeTime = 0f;

        if (chargeBar != null)
        {
            chargeBar.value = 0f;
            chargeBar.size = 0f;
        }

        if (chargeBarHandle != null && chargeColor != null)
        {
            chargeBarHandle.color = chargeColor.Evaluate(0f);
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {

        if (context.started &&jumpState == JumpState.Grounded)
        {
            jumpState = JumpState.Charging;

            jumpChargeTime = 0f;

            if (chargeBar != null)
            {
                chargeBar.value = 0f;
                chargeBar.size = 0f;
            }

            if (chargeBarHandle != null && chargeColor != null)
            {
                chargeBarHandle.color = chargeColor.Evaluate(0f);
            }
        }

        if (context.canceled && jumpState == JumpState.Charging)
        {
            Jump();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.DrawWireSphere(groundCheck.position,groundCheckRadius);
    }
}