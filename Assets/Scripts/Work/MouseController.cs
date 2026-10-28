using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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

    [SerializeField] private Animator animator;

    // MOVEMENT
    [Header("Movement")]
    [Tooltip("เปิดหรือปิดการเดินซ้าย-ขวา")]
    [SerializeField] private bool enableHorizontalMovement = true;
    [SerializeField] private float moveSpeed = 10f;

    private float moveInputX;
    private float SlowMoveSpeedMultiplier = 1f;
    private float SlowJumpSpeedMultiplier = 1f;

    private readonly HashSet<SlowObstacle> activeSlowObstacle =
        new HashSet<SlowObstacle>();

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

    // จำว่าการชาร์จจากพื้นเริ่มตอนอยู่บนพื้นจริง
    private bool groundJumpStartedOnGround;

    // AIR DOUBLE JUMP
    [Header("Air Double Jump")]
    [Tooltip("เปิดหรือปิดการกระโดดครั้งที่สองกลางอากาศ")]
    [SerializeField] private bool enableAirDoubleJump = true;

    private bool airJumpAvailable = true;

    // ป้องกัน Ground Check รีเซ็ตสถานะทันทีหลังเด้ง
    [Header("External Bounce")]
    [Tooltip("ระยะเวลาที่ละเว้นการตรวจพื้นหลังจากเด้ง")]
    [SerializeField] private float externalBounceGroundIgnoreDuration = 0.25f;

    private float externalBounceGroundIgnoreTimer;

    // SLOW FALL
    [Header("Slow Fall")]
    [Tooltip("เปิดหรือปิดระบบชะลอการตกด้วยคลิกขวา")]
    [SerializeField] private bool enableSlowFall = true;
    [SerializeField] private float slowFallSpeed = 2f;

    private bool isSlowingFall;

    // LAUNCH SETTINGS
    [Header("Launch Settings")]
    [SerializeField] private bool stopBeforeJump = true;

    private bool stopBeforeLaunch;
    private bool launchOnNextFixedUpdate;
    private Vector2 pendingLaunchVelocity;

    // MOUSE AIM
    [Header("Mouse Aim")]
    [SerializeField] private float minAimAngle = 15f;
    [SerializeField] private float maxAimAngle = 165f;

    private Vector2 aimDirection;

    // JUMP TRAJECTORY
    [Header("Jump Trajectory")]
    [SerializeField] private GameObject trajectoryPoint;
    [SerializeField] private GameObject trajectoryDotPrefab;
    [SerializeField] private int trajectoryDotCount = 36;
    [SerializeField] private float trajectoryTimeStep = 0.05f;

    private Transform[] trajectoryDots;

    // FLIP
    [Header("Flip")]
    [SerializeField] private SpriteRenderer playerSprite;
    public float facingDirection = 1f;

    // POWER UI
    [Header("Power UI")]
    [SerializeField] private Scrollbar chargeBar;
    [SerializeField] private Image chargeBarHandle;
    [SerializeField] private Gradient chargeColor;
    private Collider2D currentGroundCollider;
    private Vector2 groundSurfaceNormal = Vector2.up;

    [HideInInspector]
    public bool movementLocked;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private Camera mainCamera;

    private HealthNew healthNew;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

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

        AirAimingMaxChargeTime = maxChargeTime / 2f;
        NormalMaxChargeTime = maxChargeTime;

        mainCamera = Camera.main;
        aimDirection = GetDefaultAimDirection();

        CheckGround();
        CreateTrajectoryDots();
        ResetPowerBar();
    }

    private void Update()
    {
        moveInputX = enableHorizontalMovement
            ? Input.GetAxisRaw("Horizontal")
            : 0f;

        // นับเวลาที่ต้องละเว้น Ground Check หลังเด้ง
        if (externalBounceGroundIgnoreTimer > 0f)
        {
            externalBounceGroundIgnoreTimer = Mathf.Max(
                0f,
                externalBounceGroundIgnoreTimer - Time.deltaTime
            );
        }

        CheckGround();
        UpdateJumpState();

        if (jumpState == JumpState.AirAiming)
        {
            Time.timeScale = 0.5f;
            maxChargeTime = AirAimingMaxChargeTime;
        }
        else
        {
            Time.timeScale = 1f;
            maxChargeTime = NormalMaxChargeTime;
        }

        if (jumpState == JumpState.Grounded)
        {
            UpdateFacingDirection();
        }

        // หยุดควบคุมเมื่อระบบอื่นล็อกผู้เล่น
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

        // AIR DOUBLE JUMP
        // คลิกขวาเริ่มชาร์จกระโดดครั้งที่สองขณะลอยอยู่
        if (Input.GetMouseButtonDown(1)
            && enableAirDoubleJump
            && !isGrounded
            && airJumpAvailable
            && jumpState == JumpState.Jumping)
        {
            BeginAirAim();
        }

        // ระบบชะลอการตกด้วยคลิกขวา
        isSlowingFall =
            enableSlowFall &&
            Input.GetMouseButton(1) &&
            !isGrounded &&
            (jumpState == JumpState.Jumping ||
             jumpState == JumpState.AirAiming);

        // เริ่มชาร์จกระโดดจากพื้นด้วยคลิกซ้าย
        if (Input.GetMouseButtonDown(0)
            && isGrounded
            && jumpState == JumpState.Grounded)
        {
            BeginGroundAim();
        }

        // จัดการการชาร์จกระโดดจากพื้นและ Air Double Jump
        if (jumpState == JumpState.Aiming ||
            jumpState == JumpState.AirAiming)
        {
            bool isAirAiming = jumpState == JumpState.AirAiming;

            bool isCharging = isAirAiming
                ? Input.GetMouseButton(1)
                : Input.GetMouseButton(0);

            // การชาร์จกระโดดจากพื้นต้องเริ่มจากพื้น
            // และต้องยังอยู่บนพื้นขณะชาร์จ
            if (!isAirAiming &&
                (!groundJumpStartedOnGround || !isGrounded))
            {
                CancelJumpCharge();
            }
            else if (isCharging)
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
        ChangeAnimation();
    }

    private void FixedUpdate()
    {
        // หยุดความเร็วก่อนพุ่งหนึ่งจังหวะฟิสิกส์
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
            return;

        if (jumpState == JumpState.Jumping)
        {
            maxChargeTime = NormalMaxChargeTime;
            return;
        }

        if (jumpState == JumpState.AirAiming)
        {
            maxChargeTime = AirAimingMaxChargeTime;
            return;
        }

        // ระหว่างเล็ง หรือไม่ได้กดเดิน: ไม่แตะความเร็วแกน X
        // ปล่อยให้ Physics Material 2D (Friction) เป็นตัวตัดสินว่าหยุดหรือไถล
        if (jumpState == JumpState.Aiming || Mathf.Abs(moveInputX) < 0.01f)
            return;

        rb.linearVelocity = new Vector2(
            moveInputX * moveSpeed * SlowMoveSpeedMultiplier,
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

    #endregion

    #region Ground Check

    private void CheckGround()
    {
        // หลังเด้งให้ละเว้น Ground Check ชั่วคราว
        if (externalBounceGroundIgnoreTimer > 0f || groundCheck == null)
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
    }

    private void UpdateJumpState()
    {
        // ถ้าพื้นหายระหว่างชาร์จจากพื้น ให้ยกเลิกทันที
        if (jumpState == JumpState.Aiming && !isGrounded)
        {
            CancelJumpCharge();
        }

        // ตรวจการออกจากพื้นและการลงพื้น
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

                // ได้สิทธิ์ Double Jump ใหม่เมื่อลงพื้นจริง
                airJumpAvailable = true;

                isSlowingFall = false;
                groundJumpStartedOnGround = false;
            }
        }

        // ยกเลิก Air Double Jump หากกลับมาแตะพื้นระหว่างชาร์จ
        if (jumpState == JumpState.AirAiming && isGrounded)
        {
            jumpState = JumpState.Grounded;
            hasLeftGround = false;
            airJumpAvailable = true;
            isSlowingFall = false;
            jumpChargeTime = 0f;
            groundJumpStartedOnGround = false;

            ResetPowerBar();
            HideJumpTrajectory();
        }
    }

    #endregion

    #region Jump Aiming

    private void BeginGroundAim()
    {
        if (!isGrounded || jumpState != JumpState.Grounded)
            return;

        groundJumpStartedOnGround = true;
        jumpState = JumpState.Aiming;
        jumpChargeTime = 0f;
        aimDirection = GetDefaultAimDirection();

        ResetPowerBar();
    }

    private void BeginAirAim()
    {
        // Double Jump ต้องมีสิทธิ์เหลือ
        if (!enableAirDoubleJump ||
            !airJumpAvailable ||
            isGrounded ||
            jumpState != JumpState.Jumping)
        {
            return;
        }

        groundJumpStartedOnGround = false;
        jumpState = JumpState.AirAiming;

        // ใช้สิทธิ์เมื่อเริ่มชาร์จ Double Jump
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
            return;

        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

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
        ).magnitude * SlowJumpSpeedMultiplier;
    }

    private void Jump()
    {
        bool wasAirAiming = jumpState == JumpState.AirAiming;

        // กระโดดจากพื้นได้เฉพาะเมื่อการชาร์จเริ่มบนพื้น
        if (!wasAirAiming &&
            (!groundJumpStartedOnGround || !isGrounded))
        {
            CancelJumpCharge();
            return;
        }

        float launchSpeed = GetLaunchSpeed();

        pendingLaunchVelocity = aimDirection * launchSpeed;

        groundJumpStartedOnGround = false;
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

        // เล่นเสียงเฉพาะการกระโดดจากพื้น
        if (!wasAirAiming)
        {
            GameAudio.Instance?.PlayGroundJump();
        }

        ResetPowerBar();
        HideJumpTrajectory();
    }

    private void CancelJumpCharge()
    {
        groundJumpStartedOnGround = false;
        jumpChargeTime = 0f;

        if (isGrounded)
        {
            jumpState = JumpState.Grounded;
            hasLeftGround = false;
        }
        else
        {
            // ยังคงใช้ Double Jump ได้ถ้าสิทธิ์เหลือ
            jumpState = JumpState.Jumping;
            hasLeftGround = true;
        }

        ResetPowerBar();
        HideJumpTrajectory();
    }

    private void CancelAirAim()
    {
        jumpState = isGrounded
            ? JumpState.Grounded
            : JumpState.Jumping;

        // ยกเลิกการเล็งกลางอากาศ ให้ลองใหม่ได้
        if (!isGrounded)
        {
            airJumpAvailable = true;
            hasLeftGround = true;
        }

        groundJumpStartedOnGround = false;
        isSlowingFall = false;
        jumpChargeTime = 0f;

        ResetPowerBar();
        HideJumpTrajectory();
    }

    #endregion

    #region Slow Obstacles

    public void EnterSlowObstacle(SlowObstacle obstacle)
    {
        if (obstacle == null)
            return;

        activeSlowObstacle.Add(obstacle);
        RefreshStickyModifiers();
    }

    public void ExitSlowObstacle(SlowObstacle obstacle)
    {
        if (obstacle == null)
            return;

        activeSlowObstacle.Remove(obstacle);
        RefreshStickyModifiers();
    }

    private void RefreshStickyModifiers()
    {
        SlowMoveSpeedMultiplier = 1f;
        SlowJumpSpeedMultiplier = 1f;

        foreach (SlowObstacle obstacle in activeSlowObstacle)
        {
            if (obstacle == null)
                continue;

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
            chargeBarHandle.color =
                chargeColor.Evaluate(chargePercent);
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

        if (jumpState != JumpState.Aiming &&
            jumpState != JumpState.AirAiming)
        {
            HideJumpTrajectory();
            return;
        }

        float launchSpeed = GetLaunchSpeed();

        Vector2 startPosition = trajectoryPoint != null
            ? trajectoryPoint.transform.position
            : rb.position;

        Vector2 initialVelocity = aimDirection * launchSpeed;

        float gravityY =
            Physics2D.gravity.y * rb.gravityScale;

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
            return;

        foreach (Transform dot in trajectoryDots)
        {
            if (dot != null)
                dot.gameObject.SetActive(false);
        }
    }

    #endregion

    #region External Bounce

    // เรียกจาก BounceSurface หรือระบบเด้งภายนอก
    public void NotifyExternalBounce()
    {
        // ยกเลิกการชาร์จกระโดดจากพื้น
        groundJumpStartedOnGround = false;

        // ยกเลิกคำสั่งกระโดดที่รอทำงาน
        stopBeforeLaunch = false;
        launchOnNextFixedUpdate = false;
        pendingLaunchVelocity = Vector2.zero;

        // เปลี่ยนเป็นสถานะกลางอากาศ
        jumpState = JumpState.Jumping;
        hasLeftGround = true;

        // ป้องกัน Ground Check รีเซ็ตสถานะทันทีหลังเด้ง
        externalBounceGroundIgnoreTimer =
            externalBounceGroundIgnoreDuration;

        isGrounded = false;
        currentGroundCollider = null;

        moveInputX = 0f;
        isSlowingFall = false;
        jumpChargeTime = 0f;

        // สำคัญ:
        // ไม่เปลี่ยน airJumpAvailable ที่นี่
        // ถ้ายังไม่ได้ใช้ Double Jump จะยังมีสิทธิ์
        // ถ้าใช้ไปแล้ว จะไม่ได้สิทธิ์ใหม่จากการเด้ง

        ResetPowerBar();
        HideJumpTrajectory();
    }

    #endregion

    #region Mouse Position

    private Vector2 GetMouseWorldPosition()
    {
        if (mainCamera == null)
            return rb.position;

        Vector3 screenPosition = Input.mousePosition;

        screenPosition.z =
            -mainCamera.transform.position.z;

        Vector3 worldPosition =
            mainCamera.ScreenToWorldPoint(screenPosition);

        return new Vector2(
            worldPosition.x,
            worldPosition.y
        );
    }

    #endregion

    #region Gizmos and Respawn

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.red;

        Gizmos.DrawWireCube(
            groundCheck.position,
            groundCheckSize
        );
    }

    public void ResetAfterRespawn()
    {
        groundJumpStartedOnGround = false;
        jumpState = JumpState.Grounded;
        jumpChargeTime = 0f;
        hasLeftGround = false;

        // กลับมาเริ่มรอบใหม่เมื่อ Respawn
        airJumpAvailable = true;

        isSlowingFall = false;

        stopBeforeLaunch = false;
        launchOnNextFixedUpdate = false;
        pendingLaunchVelocity = Vector2.zero;

        externalBounceGroundIgnoreTimer = 0f;

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

    void ChangeAnimation()
    {
        if (jumpState == JumpState.Grounded)
        {
            animator.Play("Player idle");
        }
        else if (jumpState == JumpState.Aiming)
        {
            animator.Play("PlayerAiming");
        }
        else if (jumpState == JumpState.Jumping)
        {
            animator.Play("PlayerInAir");
        }
    }
}