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

    // MOVEMENT
    [Header("Movement")]
    [Tooltip("เปิดหรือปิดการเดินซ้าย-ขวา")]
    [SerializeField] private bool enableHorizontalMovement = true;
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

    // จำว่าการชาร์จพื้นเริ่มจากตอนยืนบนพื้นเท่านั้น
    private bool groundJumpStartedOnGround;

    // AIR DOUBLE JUMP
    [Header("Air Double Jump")]
    [Tooltip("เปิดหรือปิดการกระโดดครั้งที่สองกลางอากาศ")]
    [SerializeField] private bool enableAirDoubleJump = true;

    private bool airJumpAvailable = true;

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

    [Header("Slippery Surface")]
    [SerializeField] private float slipperyFrictionThreshold = 0.15f;
    [SerializeField] private float slipperyAcceleration = 18f;
    [SerializeField] private float slipperyDeceleration = 2f;
    [SerializeField] private float maxSlipperySpeed = 14f;
    [SerializeField] private float slopeNormalYThreshold = 0.98f;

    private Collider2D currentGroundCollider;
    private Vector2 groundSurfaceNormal = Vector2.up;

    [HideInInspector]
    public bool movementLocked;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private Camera mainCamera;

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
        // คลิกขวาเริ่มชาร์จกระโดดครั้งที่สองได้ขณะลอยอยู่
        // ไม่จำเป็นต้องกระโดดครั้งแรกสำเร็จก่อน
        if (Input.GetMouseButtonDown(1)
            && enableAirDoubleJump
            && !isGrounded
            && airJumpAvailable
            && jumpState == JumpState.Jumping)
        {
            BeginAirAim();
        }

        // ตรวจการกดคลิกขวาค้างกลางอากาศ
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

            // สำคัญ: ถ้าเป็นการชาร์จจากพื้น ต้องยังอยู่บนพื้น
            // หากพื้นหายไป UpdateJumpState จะยกเลิกการชาร์จแล้ว
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

        // พื้นลื่นต้องทำงานก่อนตรวจสถานะเล็ง
        // เพื่อไม่ให้การเล็งสั่งหยุดความเร็วแนวนอน
        if (isGrounded && IsOnSlipperySurface())
        {
            MoveOnSlipperySurface(jumpState != JumpState.Aiming);
            return;
        }

        // พื้นปกติยังหยุดเดินระหว่างเล็ง
        if (jumpState == JumpState.Aiming)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // การเดินปกติ
        rb.linearVelocity = new Vector2(
            moveInputX * moveSpeed * SlowMoveSpeedMultiplier,
            rb.linearVelocity.y
        );
    }

    private bool IsOnSlipperySurface()
    {
        if (currentGroundCollider == null)
        {
            return false;
        }

        PhysicsMaterial2D material = currentGroundCollider.sharedMaterial;

        if (material == null && currentGroundCollider.attachedRigidbody != null)
        {
            material = currentGroundCollider.attachedRigidbody.sharedMaterial;
        }

        return material != null &&
               material.friction <= slipperyFrictionThreshold;
    }

    private void MoveOnSlipperySurface(bool allowInput = true)
    {
        float deltaTime = Time.fixedDeltaTime;
        float velocityX = rb.linearVelocity.x;

        float maxSpeed = Mathf.Max(
            0.1f,
            maxSlipperySpeed * SlowMoveSpeedMultiplier
        );

        // เร่งความเร็วด้วยปุ่มเดินเฉพาะตอนที่อนุญาต
        if (allowInput && Mathf.Abs(moveInputX) > 0.01f)
        {
            velocityX += moveInputX
                       * slipperyAcceleration
                       * SlowMoveSpeedMultiplier
                       * deltaTime;

            velocityX = Mathf.Clamp(
                velocityX,
                -maxSpeed,
                maxSpeed
            );
        }
        // พื้นราบจะค่อย ๆ ชะลอ แต่ไม่หยุดทันที
        else if (groundSurfaceNormal.y >= slopeNormalYThreshold)
        {
            velocityX = Mathf.MoveTowards(
                velocityX,
                0f,
                slipperyDeceleration * deltaTime
            );
        }

        // อย่าตั้งความเร็ว X เป็นศูนย์ตอนเล็ง
        // ปล่อยให้ฟิสิกส์จัดการการเคลื่อนที่บนพื้นเอียง
        rb.linearVelocity = new Vector2(
            velocityX,
            rb.linearVelocity.y
        );
    }

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
            currentGroundCollider = null;
            groundSurfaceNormal = Vector2.up;
            return;
        }

        currentGroundCollider = Physics2D.OverlapBox(
            groundCheck.position,
            groundCheckSize,
            0f,
            groundLayer
        );

        isGrounded = currentGroundCollider != null;
        groundSurfaceNormal = Vector2.up;

        if (!isGrounded)
        {
            return;
        }

        Vector2 rayOrigin = (Vector2)groundCheck.position + Vector2.up * 0.25f;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            0.6f,
            groundLayer
        );

        if (hit.collider != null)
        {
            currentGroundCollider = hit.collider;
            groundSurfaceNormal = hit.normal;
        }
    }

    private void UpdateJumpState()
    {
        // ถ้าพื้นหายขณะชาร์จจากพื้น ให้ยกเลิกทันที
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
        // ห้ามเริ่มชาร์จหากไม่ได้อยู่บนพื้น
        if (!isGrounded || jumpState != JumpState.Grounded)
        {
            return;
        }

        groundJumpStartedOnGround = true;
        jumpState = JumpState.Aiming;
        jumpChargeTime = 0f;
        aimDirection = GetDefaultAimDirection();

        ResetPowerBar();
    }

    private void BeginAirAim()
    {
        // Air Double Jump ทำงานแยกจากการชาร์จกระโดดพื้น
        if (!enableAirDoubleJump ||
            !airJumpAvailable ||
            isGrounded ||
            jumpState != JumpState.Jumping)
        {
            return;
        }

        groundJumpStartedOnGround = false;
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

        aimDirection = new Vector2(
            Mathf.Cos(radians),
            Mathf.Sin(radians)
        ).normalized;

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
        // และผู้เล่นยังยืนอยู่บนพื้นขณะปล่อยคลิก
        if (!wasAirAiming &&
            (!groundJumpStartedOnGround || !isGrounded))
        {
            CancelJumpCharge();
            return;
        }

        // Air Double Jump ไม่ใช้เงื่อนไขของการกระโดดจากพื้น
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

        ResetPowerBar();
        HideJumpTrajectory();
    }

    private void CancelJumpCharge()
    {
        // ยกเลิกเฉพาะการกระโดดที่ชาร์จจากพื้น
        groundJumpStartedOnGround = false;
        jumpChargeTime = 0f;

        if (isGrounded)
        {
            jumpState = JumpState.Grounded;
            hasLeftGround = false;
        }
        else
        {
            // เปลี่ยนเป็นสถานะกลางอากาศ เพื่อให้ Air Double Jump
            // ยังสามารถเริ่มทำงานด้วยคลิกขวาได้
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

        // หากยกเลิก Air Aim กลางอากาศ ให้ลองใหม่ได้
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
        groundJumpStartedOnGround = false;
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

    #region Gizmos and Respawn

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
        groundJumpStartedOnGround = false;
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