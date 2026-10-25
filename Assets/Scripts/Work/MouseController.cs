using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SocialPlatforms;

public class MouseController : MonoBehaviour
{
    private enum JumpState
    {
        Grounded,
        Aiming,
        Jumping,
        AirAiming
    }

    private JumpState jumpState = JumpState.Grounded;

    // MOVEMENT
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;

    private float moveInputX;
    private float SlowMoveSpeedMultiplier = 1f;
    private float SlowJumpSpeedMultiplier = 1f;

    private readonly HashSet<SlowObstacle> activeSlowObstacle = new HashSet<SlowObstacle>();

    // GROUND CHECK
    [Header("Ground Check")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(0.5f, 0.2f);
    public float groundCheckRadius = 0.3f;
    public LayerMask groundLayer;
    public bool isGrounded;

    // JUMP
    [Header("Jump")]
    [SerializeField] private float minJumpForce = 10f;
    [SerializeField] private float maxJumpForce = 26f;
    [SerializeField] private float maxChargeTime = 0.7f;
    [SerializeField] private float jumpForwardForce = 14f;

    private float AirAimingMaxChargeTime;
    private float NormalMaxChargeTime;

    private float jumpChargeTime;
    private bool hasLeftGround;


    [Header("Air Double Jump")]
    [Tooltip("เปิดหรือปิดการกระโดดครั้งที่สองกลางอากาศ")]
    [SerializeField] private bool enableAirDoubleJump = true;

    private bool airJumpAvailable = true;

    [Header("Slow Fall")]
    [Tooltip("เปิดหรือปิดระบบชะลอการตกด้วยคลิกขวา")]
    [SerializeField] private bool enableSlowFall = true;

    [Tooltip("ความเร็วตกสูงสุดขณะกดคลิกขวากลางอากาศ")]
    [SerializeField] private float slowFallSpeed = 2f;

    private bool isSlowingFall;

    [Header("Launch Settings")]
    [Tooltip("หยุดความเร็วก่อนพุ่ง 1 จังหวะฟิสิกส์")]
    [SerializeField] private bool stopBeforeJump = true;

    private bool stopBeforeLaunch;
    private bool launchOnNextFixedUpdate;
    private Vector2 pendingLaunchVelocity;

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

    [HideInInspector]
    public bool movementLocked;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private Camera mainCamera;

    private void Awake()
    {

        rb = GetComponent<Rigidbody2D>();

        AirAimingMaxChargeTime = maxChargeTime / 2;
        NormalMaxChargeTime = maxChargeTime;

        if (rb == null)
        {
            Debug.LogError("MouseController requires a Rigidbody2D.", this);
            enabled = false;
            return;
        }

        playerCollider = GetComponent<Collider2D>();

        if (playerCollider == null)
        {
            playerCollider = GetComponentInChildren<Collider2D>();
        }

        mainCamera = Camera.main;
        aimDirection = GetDefaultAimDirection();

        CheckGround();
        CreateTrajectoryDots();
        ResetPowerBar();
    }

    private void Update()
    {
        moveInputX = Input.GetAxisRaw("Horizontal");

        CheckGround();
        UpdateJumpState();

        if (jumpState == JumpState.AirAiming)
        {
            Time.timeScale = 0.5f;
            maxChargeTime = AirAimingMaxChargeTime;
        }
        else if (jumpState != JumpState.AirAiming)
        {
            Time.timeScale = 1;
            maxChargeTime = NormalMaxChargeTime;

        }

        if (jumpState == JumpState.Grounded)
        {
            UpdateFacingDirection();
        }

        // ล็อกการควบคุมจาก ShellWallBounce
        if (movementLocked)
        {
            moveInputX = 0f;
            isSlowingFall = false;

            if (jumpState == JumpState.Aiming)
            {
                CancelJumpCharge();
            }
            else if (jumpState == JumpState.AirAiming)
            {
                CancelAirAim();
            }

            HideJumpTrajectory();
            return;
        }


        // คลิกขวากลางอากาศเพื่อเริ่มเล็ง Air Jump
        if (
            Input.GetMouseButtonDown(1) &&
            enableAirDoubleJump &&
            !isGrounded &&
            airJumpAvailable &&
            jumpState == JumpState.Jumping)
        {
            BeginAirAim();
        }


        // คลิกขวาค้างเพื่อชะลอการตก
        isSlowingFall =
            enableSlowFall &&
            Input.GetMouseButton(1) &&
            !isGrounded &&
            (jumpState == JumpState.Jumping || jumpState == JumpState.AirAiming);


        // คลิกซ้ายเพื่อเริ่มเล็งกระโดดจากพื้น
        if (Input.GetMouseButtonDown(0) && isGrounded && jumpState == JumpState.Grounded)
        {
            BeginGroundAim();
        }


        // เล็งและชาร์จกระโดด
        if (jumpState == JumpState.Aiming || jumpState == JumpState.AirAiming)
        {
            bool isAirAiming = jumpState == JumpState.AirAiming;
            bool isCharging = isAirAiming ? Input.GetMouseButton(1) : Input.GetMouseButton(0);

            if (isCharging)
            {
                jumpChargeTime += Time.deltaTime;
                jumpChargeTime = Mathf.Clamp(jumpChargeTime, 0f, maxChargeTime);

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
        // หยุดความเร็วก่อนพุ่ง 1 จังหวะฟิสิกส์
        if (stopBeforeLaunch)
        {
            rb.linearVelocity = Vector2.zero;
            stopBeforeLaunch = false;
            launchOnNextFixedUpdate = true;
            return;
        }

        // พุ่งในจังหวะฟิสิกส์ถัดไป
        if (launchOnNextFixedUpdate)
        {
            rb.linearVelocity = pendingLaunchVelocity;
            launchOnNextFixedUpdate = false;
            return;
        }

        Move();
    }

    #region Movement

    private void Move()
    {

        if (movementLocked)
        {
            return;
        }

        // ระหว่างพุ่ง ให้ฟิสิกส์ทำงานตามปกติ
        if (jumpState == JumpState.Jumping)
        {
            maxChargeTime = NormalMaxChargeTime;
            //ApplySlowFall();
            return;
        }

        // ระหว่างเล็ง Air Jump ไม่ล็อกแนวนอน
        if (jumpState == JumpState.AirAiming)
        {
            maxChargeTime = AirAimingMaxChargeTime;
            //ApplySlowFall();
            return;
        }

        // หยุดการเดินระหว่างเล็งกระโดดจากพื้น
        if (jumpState == JumpState.Aiming)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // การเดินปกติบนพื้น
        rb.linearVelocity = new Vector2(moveInputX * moveSpeed * SlowMoveSpeedMultiplier, rb.linearVelocity.y);
    }


    // ชะลอเฉพาะความเร็วตกแกน Y
    //private void ApplySlowFall()
    //{
    //    if (!isSlowingFall || rb.linearVelocity.y >= 0f)
    //    {
    //        return;
    //    }

    //    float maxFallSpeed = Mathf.Max(0f, slowFallSpeed);

    //    // จำกัดความเร็วตก โดยไม่เปลี่ยนความเร็วแกน X
    //    if (rb.linearVelocity.y < -maxFallSpeed)
    //    {
    //        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    //    }
    //}


    private void UpdateFacingDirection()
    {
        if (moveInputX > 0f)
        {
            facingDirection = 1f;

            if (playerSprite != null)
            {
                playerSprite.flipX = false;
            }
        }
        else if (moveInputX < 0f)
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
        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer) != null;
    }


    private void UpdateJumpState()
    {
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
                airJumpAvailable = true;
                isSlowingFall = false;
            }
        }

        // หากแตะพื้นระหว่างเล็ง Air Jump ให้ยกเลิกการเล็ง
        if (jumpState == JumpState.AirAiming && isGrounded)
        {
            jumpState = JumpState.Grounded;
            hasLeftGround = false;
            airJumpAvailable = true;
            isSlowingFall = false;
            jumpChargeTime = 0f;

            ResetPowerBar();
            HideJumpTrajectory();
        }
    }

    #endregion

    #region Jump Aiming

    private void BeginGroundAim()
    {
        jumpState = JumpState.Aiming;
        jumpChargeTime = 0f;
        aimDirection = GetDefaultAimDirection();

        ResetPowerBar();
    }


    private void BeginAirAim()
    {
        if (!enableAirDoubleJump || !airJumpAvailable || isGrounded || jumpState != JumpState.Jumping)
        {
            return;
        }

        jumpState = JumpState.AirAiming;
        airJumpAvailable = false;
        jumpChargeTime = 0f;
        hasLeftGround = true;
        aimDirection = GetDefaultAimDirection();

        UpdateAimFromMouse();
        ResetPowerBar();
    }


    private void UpdateAimFromMouse()
    {
        Vector2 direction = GetMouseWorldPosition() - rb.position;

        if (direction.sqrMagnitude < 0.04f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (angle < 0f)
        {
            angle += 360f;
        }

        // จำกัดมุมให้อยู่ในครึ่งวงกลมด้านบน
        if (angle > 180f)
        {
            angle = direction.x >= 0f ? minAimAngle : maxAimAngle;
        }

        angle = Mathf.Clamp(angle, minAimAngle, maxAimAngle);

        float radians = angle * Mathf.Deg2Rad;
        aimDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;

        UpdateAimFacingDirection();
    }


    private void UpdateAimFacingDirection()
    {
        if (Mathf.Abs(aimDirection.x) < 0.01f)
        {
            return;
        }

        facingDirection = Mathf.Sign(aimDirection.x);

        if (playerSprite != null)
        {
            playerSprite.flipX = facingDirection < 0f;
        }
    }


    private Vector2 GetDefaultAimDirection()
    {
        Vector2 direction = new Vector2(facingDirection * jumpForwardForce, minJumpForce);
        return direction.normalized;
    }


    private float GetChargePercent()
    {
        return maxChargeTime > 0f ? Mathf.Clamp01(jumpChargeTime / maxChargeTime) : 1f;
    }


    private float GetLaunchSpeed()
    {
        float jumpForce = Mathf.Lerp(minJumpForce, maxJumpForce, GetChargePercent());
        return new Vector2(jumpForwardForce, jumpForce).magnitude * SlowJumpSpeedMultiplier;
    }


    private void Jump()
    {
        bool wasAirAiming = jumpState == JumpState.AirAiming;
        float launchSpeed = GetLaunchSpeed();

        // บันทึกความเร็วที่จะพุ่ง
        pendingLaunchVelocity = aimDirection * launchSpeed;

        jumpState = JumpState.Jumping;
        hasLeftGround = wasAirAiming;
        isSlowingFall = false;

        if (stopBeforeJump)
        {
            rb.linearVelocity = Vector2.zero;
            stopBeforeLaunch = true;
            launchOnNextFixedUpdate = false;
        }
        else
        {
            stopBeforeLaunch = false;
            launchOnNextFixedUpdate = true;
        }

        jumpChargeTime = 0f;

        ResetPowerBar();
        HideJumpTrajectory();
    }


    private void CancelJumpCharge()
    {
        jumpState = JumpState.Grounded;
        jumpChargeTime = 0f;

        ResetPowerBar();
        HideJumpTrajectory();
    }

   private void CancelAirAim()
    {
        jumpState = isGrounded ? JumpState.Grounded : JumpState.Jumping;

        if (!isGrounded)
        {
            airJumpAvailable = true;
        }

        isSlowingFall = false;
        jumpChargeTime = 0f;

        ResetPowerBar();
        HideJumpTrajectory();
    }

    #endregion

    #region Slow
    public void EnterSlowObstacle(SlowObstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        activeSlowObstacle.Add(obstacle);
        RefreshStickyModifiers();
    }

    public void ExitSlowObstacle(SlowObstacle obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        activeSlowObstacle.Remove(obstacle);
        RefreshStickyModifiers();
    }

    private void RefreshStickyModifiers()
    {
        SlowMoveSpeedMultiplier = 1f;
        SlowJumpSpeedMultiplier = 1f;

        // หากอยู่ในพื้นที่หนืดซ้อนกัน ใช้ค่าที่ช้าที่สุด
        foreach (SlowObstacle obstacle in activeSlowObstacle)
        {
            if (obstacle == null)
            {
                continue;
            }

            SlowMoveSpeedMultiplier = Mathf.Min(
                SlowMoveSpeedMultiplier,
                obstacle.MovementMultiplier
            );

            SlowJumpSpeedMultiplier = Mathf.Min(
                SlowJumpSpeedMultiplier,
                obstacle.JumpMultiplier
            );
        }
    }

#endregion
    #region Power UI
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
            chargeBarHandle.color = chargeColor.Evaluate(chargePercent);
        }
    }


    private void ResetPowerBar()
    {
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

    #endregion

    #region Jump Trajectory

    private void CreateTrajectoryDots()
    {
        if (trajectoryDotPrefab == null)
        {
            return;
        }

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
        {
            return;
        }

        if (jumpState != JumpState.Aiming && jumpState != JumpState.AirAiming)
        {
            HideJumpTrajectory();
            return;
        }

        float launchSpeed = GetLaunchSpeed();

        Vector2 startPosition = trajectoryPoint != null ? trajectoryPoint.transform.position : rb.position;
        Vector2 initialVelocity = aimDirection * launchSpeed;
        float gravityY = Physics2D.gravity.y * rb.gravityScale;

        Vector2 previousPoint = startPosition;

        for (int i = 0; i < trajectoryDots.Length; i++)
        {
            float time = (i + 1) * trajectoryTimeStep;

            Vector2 point = startPosition + initialVelocity * time + Vector2.up * (0.5f * gravityY * time * time);

            trajectoryDots[i].gameObject.SetActive(true);
            trajectoryDots[i].position = point;

            Vector2 segment = point - previousPoint;

            RaycastHit2D hit = Physics2D.Raycast(previousPoint, segment.normalized, segment.magnitude, groundLayer);

            if (hit.collider != null)
            {
                trajectoryDots[i].position = hit.point;

                for (int j = i + 1; j < trajectoryDots.Length; j++)
                {
                    trajectoryDots[j].gameObject.SetActive(false);
                }

                break;
            }

            previousPoint = point;
        }
    }


    private void HideJumpTrajectory()
    {
        if (trajectoryDots == null)
        {
            return;
        }

        foreach (Transform dot in trajectoryDots)
        {
            if (dot != null)
            {
                dot.gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region External Bounce
    public void NotifyExternalBounce()
    {
        stopBeforeLaunch = false;
        launchOnNextFixedUpdate = false;
        pendingLaunchVelocity = Vector2.zero;

        jumpState = JumpState.Jumping;
        hasLeftGround = false;
        moveInputX = 0f;
        isSlowingFall = false;
        jumpChargeTime = 0f;

        ResetPowerBar();
        HideJumpTrajectory();
    }

    #endregion

    #region Mouse Position

    private Vector2 GetMouseWorldPosition()
    {
        if (mainCamera == null)
        {
            return rb.position;
        }

        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = -mainCamera.transform.position.z;

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);

        return new Vector2(worldPosition.x, worldPosition.y);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
    public void ResetAfterRespawn()
    {
        jumpState = JumpState.Grounded;
        jumpChargeTime = 0f;
        hasLeftGround = false;
        airJumpAvailable = true;
        isSlowingFall = false;

        stopBeforeLaunch = false;
        launchOnNextFixedUpdate = false;
        pendingLaunchVelocity = Vector2.zero;

        moveInputX = 0f;
        movementLocked = false;
        aimDirection = GetDefaultAimDirection();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        CheckGround();
        ResetPowerBar();
        HideJumpTrajectory();
    }
    #endregion
}
