using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private enum JumpState
    {
        OnGround,
        Aiming,
        InAir,
        InShell
    }

    [SerializeField] private JumpState jumpState = JumpState.OnGround;

    [SerializeField] private Animator animator;

    [Header("Checks")]
    [SerializeField] private bool isOnGround;
    [SerializeField] private bool isAiming;
    [SerializeField] private bool isJumped;
    [SerializeField] private bool isAirAiming;
    [SerializeField] public bool isInShell;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckX = 0.2f;
    [SerializeField] private float groundCheckY = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Setting")]
    [SerializeField] private float minJumpForce = 10f;
    [SerializeField] private float maxJumpForce = 26f;
    [SerializeField] private float jumpForwardForce = 14f;
    [SerializeField] private float maxChargeTime = 0.7f;
    [SerializeField] private float jumpChargeTime;
    private Vector2 launchVelocity;

    [SerializeField] private bool isJumpedFromGround = false;

    [Header("Air Jump Setting")]
    [SerializeField] public bool canAirJump = false;

    private float AirAimingMaxChargeTime;
    private float NormalMaxChargeTime;

    [Header("In Shell Setting")]
    [SerializeField] private float inShellTime = 1;
    [SerializeField] private bool canBounce;

    [Header("Mouse Aim Setting")]
    [SerializeField] private float minAimAngle = 15f;
    [SerializeField] private float maxAimAngle = 165f;
    private Vector2 aimDirection;

    private JumpTrajectory jumpTrajectory;

    [Header("Flip")]
    [SerializeField] private SpriteRenderer playerSprite;
    public float facingDirection = 1f;

    [HideInInspector]
    private Rigidbody2D rb;
    private Camera mainCamera;
    private HealthNew healthNew;

    [Header("Health Integration")]
    public bool movementLocked = false;
    public float jumpPowerMultiplier = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        jumpTrajectory = GetComponent<JumpTrajectory>();
        healthNew = GetComponent<HealthNew>();

        mainCamera = Camera.main;

        AirAimingMaxChargeTime = maxChargeTime / 3f;
        NormalMaxChargeTime = maxChargeTime;

        Grounded();
        
    }

    void Start()
    {

    }


    void Update()
    {
        // ล็อกการควบคุมระหว่างตายหรือกำลัง Respawn
        if (movementLocked)
        {
            Time.timeScale = 1f;

            isAiming = false;
            isAirAiming = false;
            jumpChargeTime = 0f;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            HideJumpTrajectory();
            return;
        }

        // การกด mouse
        if (Input.GetMouseButtonDown(0))          // กดเพื่อ Aim
        {
            //Debug.Log("Click");

            if (!isJumpedFromGround && isOnGround && !isInShell)
            {

                if (isInShell)
                    return;

                //Debug.Log("Aiming");

                if (!isOnGround || isJumped)
                    return;

                jumpState = JumpState.Aiming;
                isAiming = true;
            }

            // เล็งกลางอากาศ
            if (isJumpedFromGround && canAirJump && isJumped)
            {
                //Debug.Log("Air Aiming");
                isAirAiming = true;
            }
        }
        if (Input.GetMouseButtonUp(0) && (isAiming || isAirAiming))           // ปล่อยเพื่อ Jump
        {
            //Debug.Log("UnClick");

            // กระโดดจากพื้น
            if (!isJumpedFromGround && isAiming)
            {
                if (!isOnGround)
                    return;

                if (!isJumpedFromGround)
                    isJumpedFromGround = true;

                isAiming = false;
                Jump();
            }

            // กระโดดกลางอากาศ
            if (isJumpedFromGround && canAirJump && isAirAiming)
            {
                isAirAiming = false;
                Jump();

                canAirJump = false;
            }
        }

        //เล็งเมื่อกำลังกด mouse และครบเงื่อนไข
        if (isAiming || isAirAiming)
        {
            Aiming();
        }

        // เมื่อ Air Aiming จะทำให้เวลาช้าลง
        if (isAirAiming)
        {
            Time.timeScale = 0.33f;
            maxChargeTime = AirAimingMaxChargeTime;
        }
        else
        {
            Time.timeScale = 1f;
            maxChargeTime = NormalMaxChargeTime;
        }

        // กด SpaceBar เพื่อเข้า Shell
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("In Shell!!");
            StartCoroutine(ShellState());
        }

        Grounded();

        //ตัวเปลี่ยนให้เป็น OnGround state
        if (isOnGround && !isAiming)
        {
            jumpState = JumpState.OnGround;
        }

        CanBounceCheck();
        ChangeAnimation();
        UpdateJumpTrajectory();
        //UpdateFacingDirection();
    }

    public void Grounded()
    {
        if (groundCheckPoint == null || rb == null)
        {
            isOnGround = false;
            return;
        }

        bool groundDetected = false;

        for (int i = -1; i <= 1; i++)
        {
            Vector2 origin = (Vector2)groundCheckPoint.position
                + Vector2.right * (i * groundCheckX)
                + Vector2.up * 0.05f;

            foreach (RaycastHit2D hit in Physics2D.RaycastAll(
                origin, Vector2.down, groundCheckY, groundLayer))
            {
                if (hit.collider == null ||
                    hit.collider.attachedRigidbody == rb ||
                    hit.normal.y < 0.55f)
                    continue;

                groundDetected = true;
                break;
            }

            if (groundDetected) break;
        }

        if (!groundDetected || rb.linearVelocityY > 0.01f)
        {
            isOnGround = false;
            return;
        }

        isOnGround = true;
        isJumped = false;
        isJumpedFromGround = false;

        if (isAirAiming)
        {
            isAirAiming = false;
            jumpChargeTime = 0f;
            Time.timeScale = 1f;
            maxChargeTime = NormalMaxChargeTime;
            HideJumpTrajectory();
        }

        if (!isAiming)
            jumpState = JumpState.OnGround;
    }

    // 1 เล็งแล้ว update ตำแหน่งของ mouse
    public void Aiming()
    {
        //Debug.Log("Charging!!");
        jumpChargeTime += Time.deltaTime;
        jumpChargeTime = Mathf.Clamp(
                    jumpChargeTime,
                    0f,
                    maxChargeTime
                );

        UpdateAimFromMouse();
    }

    #region Aim Mechanic
    //เอาตำแหน่ง mouse
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


    //update ตำแหน่ง mouse
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
    #endregion

    #region Jump Mechanic
    // 2 คำนวณ ทิศทาง แรง แล้วกระโดด
    private void Jump()
    {
        float jumpPower = GetJumpPower();
        launchVelocity = aimDirection * jumpPower;

        jumpState = JumpState.InAir;
        rb.linearVelocity = launchVelocity;
        isJumped = true;
        jumpChargeTime = 0f;
    }

    private float GetJumpPower()
    {
        float jumpForce = Mathf.Lerp(
            minJumpForce,
            maxJumpForce,
            GetChargePercent()
        );

        float basePower = new Vector2(
            jumpForwardForce,
            jumpForce
        ).magnitude;

        return basePower * jumpPowerMultiplier;
    }

    private float GetChargePercent()
    {
        return maxChargeTime > 0f
            ? Mathf.Clamp01(jumpChargeTime / maxChargeTime)
            : 1f;
    }
    #endregion

    #region Shell State  |  Bounce Mechanic
    private IEnumerator ShellState()
    {
        jumpState = JumpState.InShell;
        isInShell = true;

        yield return new WaitForSeconds(inShellTime);

        jumpState = JumpState.InAir;
        isInShell = false;
    }

    private void CanBounceCheck()
    {
        if (jumpState != JumpState.InAir)
        {
            canBounce = false;
            return;
        }

        float speed = Mathf.Abs(rb.linearVelocity.magnitude);
        if (speed > healthNew.safeImpactSpeed)
        {
            canBounce = true;
        }
        else
        {
            canBounce = false;
        }
    }
    #endregion

    #region Jump Trajectory

    private void UpdateJumpTrajectory()
    {
        if (jumpTrajectory == null)
            return;

        jumpTrajectory.UpdateTrajectory(
            isAiming || isAirAiming,
            aimDirection,
            GetJumpPower()
        );
    }

    private void HideJumpTrajectory()
    {
        if (jumpTrajectory != null)
            jumpTrajectory.Hide();
    }
    #endregion 

    #region other
    // For Air Jump Item
    public bool RestoreAirJump()
    {
        if (movementLocked || canAirJump)
        {
            return false;
        }

        // ต้องอยู่กลางอากาศหลังจากกระโดดจากพื้นแล้ว
        if (!isJumped || !isJumpedFromGround || isOnGround)
        {
            return false;
        }

        canAirJump = true;

        Debug.Log("Air Jump Restored!");

        return true;
    }

    //เปลี่ยนให้ player หันไปตาม mouse
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

    void ChangeAnimation()
    {
        if (jumpState == JumpState.OnGround)
        {
            animator.Play("Player idle");
        }
        else if (jumpState == JumpState.Aiming)
        {
            animator.Play("PlayerAiming");
        }
        else if (jumpState == JumpState.InAir)
        {
            animator.Play("PlayerInAir");
        }
    }

    // เรียกจาก HealthNew หลังเกิดใหม่
    public void ResetAfterRespawn()
    {
        movementLocked = false;

        jumpState = JumpState.OnGround;

        isOnGround = false;
        isAiming = false;
        isJumped = false;
        isAirAiming = false;

        isJumpedFromGround = false;
        canAirJump = true;

        jumpChargeTime = 0f;
        jumpPowerMultiplier = 1f;

        aimDirection = Vector2.up;

        Time.timeScale = 1f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        HideJumpTrajectory();
    }
    #endregion
}
