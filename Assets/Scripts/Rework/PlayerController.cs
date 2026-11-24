using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using Unity.VisualScripting;

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
    [SerializeField] private bool isTempAiming;
    [SerializeField] public bool isInShell;
    [SerializeField] private bool isPredict;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckX = 0.2f;
    [SerializeField] private float groundCheckY = 0.2f;
    [SerializeField] private bool groundDetected = false;
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
    [SerializeField] public bool canTempAim;
    [SerializeField] private bool isWantSlowParry = false;

    [Header("In Shell Bubble Visual")]
    [Tooltip("Sprite ฟองอากาศที่จะแสดงคลุมตัว Tumi ระหว่างอยู่ในสถานะ InShell")]
    [SerializeField] private Sprite inShellBubbleSprite;
    [Tooltip("ขนาดของฟองอากาศ ปรับให้คลุมตัวละครพอดี")]
    [SerializeField] private Vector2 inShellBubbleScale = new Vector2(1.5f, 1.5f);
    [Tooltip("ตำแหน่งของฟองอากาศเทียบกับตัว Tumi")]
    [SerializeField] private Vector2 inShellBubbleOffset = Vector2.zero;
    [Tooltip("ค่าลำดับการวาดเทียบกับ Sprite ของผู้เล่น (ค่าบวก = วาดด้านหน้า)")]
    [SerializeField] private int inShellBubbleSortingOrderOffset = 1;

    private SpriteRenderer inShellBubbleRenderer;
    private bool showParryBubbleUntilGrounded;
    private bool hasLeftGroundAfterParry;

    [Header("Predict Impact Setting")]
    [SerializeField] private float predictTimeMax;
    [SerializeField] private float predictTime;
    [SerializeField] private LayerMask predictLayer;
    [SerializeField] private Collider2D bounceTarget;
    [SerializeField] private Vector2 bounceNormal;
    [SerializeField] private Vector2 bouncePoint;
    [SerializeField] private bool isSlowMotion;
    [SerializeField] private bool isTakeDamage = false;
    [SerializeField] public bool isBounceSuccessed = false;
    private CircleCollider2D playerCollider;

    [Header("Time Scale Setting")]
    [SerializeField] private float originalTimeScale;
    [SerializeField] private float slowTimeScale;


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

    private Vector2 hitPoint;
    private Vector2 hitNormal;
    private bool hasHit;

    [Header("Health Integration")]
    public bool movementLocked = false;
    public float jumpPowerMultiplier = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<CircleCollider2D>();
        jumpTrajectory = GetComponent<JumpTrajectory>();
        healthNew = GetComponent<HealthNew>();

        GameObject cameraObject = GameObject.FindGameObjectWithTag("Camera");

        if (cameraObject != null)
        {
            mainCamera = cameraObject.GetComponent<Camera>();
        }

        AirAimingMaxChargeTime = maxChargeTime / 3f;
        NormalMaxChargeTime = maxChargeTime;

        SetupInShellBubbleVisual();
        Grounded();

    }

    private void SetupInShellBubbleVisual()
    {
        if (inShellBubbleSprite == null)
        {
            return;
        }

        GameObject bubbleObject = new GameObject("InShellBubbleVisual");
        bubbleObject.transform.SetParent(transform, false);
        bubbleObject.transform.localPosition = inShellBubbleOffset;
        bubbleObject.transform.localRotation = Quaternion.identity;
        bubbleObject.transform.localScale = new Vector3(
            inShellBubbleScale.x,
            inShellBubbleScale.y,
            1f
        );

        inShellBubbleRenderer = bubbleObject.AddComponent<SpriteRenderer>();
        inShellBubbleRenderer.sprite = inShellBubbleSprite;
        inShellBubbleRenderer.enabled = false;

        // วาดฟองอากาศให้อยู่ด้านหน้าของ Sprite ผู้เล่น
        if (playerSprite != null)
        {
            inShellBubbleRenderer.sortingLayerID = playerSprite.sortingLayerID;
            inShellBubbleRenderer.sortingOrder =
                playerSprite.sortingOrder + inShellBubbleSortingOrderOffset;
        }
    }

    private void SetInShellBubbleVisible(bool visible)
    {
        if (inShellBubbleRenderer != null)
        {
            inShellBubbleRenderer.enabled = visible;
        }
    }

    // เรียกจาก HealthNew เฉพาะเมื่อ Parry สำเร็จขณะอยู่ใน InShell
    public void NotifyParrySuccess()
    {
        if (!isInShell)
            return;

        showParryBubbleUntilGrounded = true;
        hasLeftGroundAfterParry = !isOnGround;
        SetInShellBubbleVisible(true);
    }

    private void HideParryBubbleAfterLanding()
    {
        showParryBubbleUntilGrounded = false;
        hasLeftGroundAfterParry = false;
        SetInShellBubbleVisible(false);
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
            if (isJumpedFromGround && canAirJump && isJumped && !canTempAim)
            {
                //Debug.Log("Air Aiming");
                isAirAiming = true;
            }

            if (!isTempAiming && canTempAim && !isOnGround)
            {
                isTempAiming = true;
            }
        }
        if (Input.GetMouseButtonUp(0) && (isAiming || isAirAiming || isTempAiming))           // ปล่อยเพื่อ Jump
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

            if (isTempAiming)
            {
                isTempAiming = false;
                Jump();

                canTempAim = false;
            }
        }

        //เล็งเมื่อกำลังกด mouse และครบเงื่อนไข
        if (isAiming || isAirAiming || isTempAiming)
        {
            Aiming();
        }


        // กด SpaceBar เพื่อเข้า Shell
        if (Input.GetMouseButtonUp(1))
        {
            StartCoroutine(ShellState());
        }

        Grounded();

        //ตัวเปลี่ยนให้เป็น OnGround state
        //if (isOnGround && !isAiming)
        //{
        //    jumpState = JumpState.OnGround;
        //}

        float normalX = Mathf.Abs(bounceNormal.x);
        float normalY = Mathf.Abs(bounceNormal.y);

        float threshold = 0.001f;

        if (normalY > normalX && normalY > threshold)
        {
            // ชนพื้นหรือเพดาน
            if (Mathf.Abs(rb.linearVelocityY) > healthNew.safeImpactSpeed)
            {
                isTakeDamage = true;
            }
            else
            {
                isTakeDamage = false;
            }
        }
        else if (normalX > normalY && normalX > threshold)
        {
            // ชนกำแพง
            if (Mathf.Abs(rb.linearVelocityX) > healthNew.safeImpactSpeed)
            {
                isTakeDamage = true;
            }
            else
            {
                isTakeDamage = false;
            }
        }
        else
        {
            isTakeDamage = false;
        }

        // เช็คเงื่อนไขการเข้า Slow Prediction
        if ((bounceTarget != null || isPredict) && isTakeDamage)
        {
            //Debug.Log("in");
            if (isWantSlowParry)
            {
                isPredict = true;
            }
        }
        else
        {
            if (isWantSlowParry)
            {
                isPredict = false;
            }
        }

        // ทำ slow motion ตอน AirAiming และ Predict เท่านั้น
        if (isAirAiming || isPredict || isTempAiming)
        {
            MakeSlowMotion();
        }
        else if (!isAirAiming && !isPredict && !isTempAiming)
        {
            StopSlowMotion();
        }
        PredictImpact();

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
            groundDetected = false;
            return;
        }

        // Reset ก่อนตรวจจับทุกครั้ง
        groundDetected = false;

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

                //Debug.Log("in");
                groundDetected = true;
            }

            if (groundDetected) break;
        }

        // หลัง Parry สำเร็จ ต้องรอให้ Tumi กลับมาลงพื้นก่อนซ่อน Bubble
        if (showParryBubbleUntilGrounded &&
            (!groundDetected || rb.linearVelocity.sqrMagnitude > 0.0001f))
        {
            hasLeftGroundAfterParry = true;
        }

        //if (groundDetected)
        //{
        //    isOnGround = true;
        //    jumpState = JumpState.OnGround;
        //}

        if (!groundDetected && rb.linearVelocity.magnitude > 0f)
        {
            isOnGround = false;
            jumpState = JumpState.InAir;
            return;
        }

        //Debug.Log("in");

        if (isJumped && rb.linearVelocity.magnitude > 0.001f)
            return;

        isOnGround = true;

        if (showParryBubbleUntilGrounded && hasLeftGroundAfterParry && groundDetected)
        {
            HideParryBubbleAfterLanding();
        }

        isJumped = false;
        isJumpedFromGround = false;
        jumpState = JumpState.OnGround;

        canTempAim = false;

        if (isAiming && !groundDetected)
        {
            isAiming = false;
            jumpChargeTime = 0f;
            Time.timeScale = 1f;
            maxChargeTime = NormalMaxChargeTime;
            HideJumpTrajectory();
        }

        if (isAirAiming)
        {
            isAirAiming = false;
            jumpChargeTime = 0f;
            Time.timeScale = 1f;
            maxChargeTime = NormalMaxChargeTime;
            HideJumpTrajectory();
        }

        if (isTempAiming)
        {
            isTempAiming = false;
            jumpChargeTime = 0f;
            Time.timeScale = 1f;
            maxChargeTime = NormalMaxChargeTime;
            HideJumpTrajectory();
        }

        if (!isAiming)
            jumpState = JumpState.OnGround;
    }

    // เล็งแล้ว update ตำแหน่งของ mouse
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
        if (jumpState != JumpState.InAir)
            yield break;

        // เข้า InShell แต่ยังไม่แสดง Bubble จนกว่า Parry จะสำเร็จ
        jumpState = JumpState.InShell;
        isInShell = true;

        yield return new WaitForSeconds(inShellTime);

        // ออกจาก InShell; ถ้า Parry สำเร็จ Bubble จะคงอยู่จนกว่าจะลงพื้น
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
        if (bounceTarget != null)
        {
            if (bounceTarget.gameObject.CompareTag("Hazard"))
            {
                canBounce = true;
                return;
            }
        }

        if (Mathf.Abs(rb.linearVelocityX) > healthNew.safeImpactSpeed && Mathf.Abs(bounceNormal.x) > Mathf.Abs(bounceNormal.y)
            || Mathf.Abs(rb.linearVelocityY) > healthNew.safeImpactSpeed && Mathf.Abs(bounceNormal.y) > Mathf.Abs(bounceNormal.x))
        {
            canBounce = true;
        }
        else
        {
            canBounce = false;
        }
    }
    #endregion

    private void PredictImpact()
    {
        //Debug.Log("Predicting");
        Vector2 velocity = rb.linearVelocity;

        // ถ้า Player อยู่นิ่ง ไม่ต้องทำ
        if (velocity.magnitude <= 0f)
        {
            bounceTarget = null;
            bounceNormal = Vector2.zero;
            predictTime = 0.025f;
            hasHit = false;
            return;
        }

        // ค่อยๆตรวจจากเริ่ม กันการตรวจเลย
        if (predictTime < predictTimeMax)
        {
            predictTime += Time.deltaTime;
        }

        // Gravity จริงที่ Rigidbody ได้รับ
        Vector2 gravity = Physics2D.gravity * rb.gravityScale;

        // เวลาที่ต้องการคาดการณ์ล่วงหน้า
        float time = predictTime;

        // คำนวณระยะการเคลื่อนที่ในอนาคต โดยคิด Gravity
        Vector2 displacement = velocity * time + 0.5f * gravity * time * time;

        // ทิศทางที่คาดว่าจะเคลื่อนที่ไป
        Vector2 direction = displacement.normalized;

        // ระยะทางที่คาดว่าจะเคลื่อนที่ไป
        float castDistance = displacement.magnitude;

        // Radius ของ CircleCollider
        float radius = playerCollider.radius *
                      Mathf.Max(
                          transform.lossyScale.x,
                          transform.lossyScale.y
                      );

        // จุดเริ่มต้นที่ตำแหน่ง Collider
        Vector2 castOrigin = playerCollider.bounds.center;

        RaycastHit2D hit = Physics2D.CircleCast(
            castOrigin,
            radius,
            direction,
            castDistance,
            predictLayer
        );

        // เก็บข้อมูลพื้นผิวที่กำลังจะชน
        //parryNormal = hit.normal;
        if (hit.collider != null)
        {
            bouncePoint = hit.point;
            bounceTarget = hit.collider;
            bounceNormal = hit.normal;

            hitPoint = hit.point;
            hitNormal = hit.normal;
            hasHit = true;

            //Debug.Log("Hit Normal: " + hit.normal);
            //Debug.Log("Bounce Normal: " + bounceNormal);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        bounceNormal = Vector2.zero;
        bounceTarget = null;
    }

    private void MakeSlowMotion()
    {
        if (isSlowMotion)
            return;

        //Debug.Log("Slow Motion");
        isSlowMotion = true;

        maxChargeTime = AirAimingMaxChargeTime;
        Time.timeScale = slowTimeScale;
    }

    private void StopSlowMotion()
    {
        if (!isSlowMotion)
            return;

        //Debug.Log("Stop Slow Motion");
        isSlowMotion = false;

        maxChargeTime = NormalMaxChargeTime;
        Time.timeScale = originalTimeScale;
    }

    #region Jump Trajectory

    private void UpdateJumpTrajectory()
    {
        if (jumpTrajectory == null)
            return;

        jumpTrajectory.UpdateTrajectory(
            isAiming || isAirAiming || isTempAiming,
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
        isInShell = false;
        HideParryBubbleAfterLanding();

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

    private void OnDrawGizmosSelected()
    {
        if (groundCheckPoint == null) return;

        Vector3 origin1 = groundCheckPoint.position;
        Vector3 origin2 = groundCheckPoint.position + new Vector3(-groundCheckX, 0, 0);
        Vector3 origin3 = groundCheckPoint.position + new Vector3(groundCheckX, 0, 0);

        // Center Ray
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin1, Vector3.down * groundCheckY);

        // Left Ray
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin2, Vector3.down * groundCheckY);

        // Right Ray
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin3, Vector3.down * groundCheckY);

        if (rb == null || playerCollider == null)
        {
            rb = GetComponent<Rigidbody2D>();
            playerCollider = GetComponent<CircleCollider2D>();
            //Debug.Log("No rb");
            return;
        }

        Vector2 velocity = rb.linearVelocity;

        if (velocity.magnitude <= 0f)
            return;

        Vector2 gravity = Physics2D.gravity * rb.gravityScale;

        float time = predictTime;

        // คำนวณระยะการเคลื่อนที่ในอนาคต โดยคิด Gravity
        Vector2 displacement =
            velocity * time +
            0.5f * gravity * time * time;

        if (displacement.magnitude <= 0.001f)
            return;

        // ทิศทาง CircleCast
        Vector2 direction = displacement.normalized;

        // ระยะทาง CircleCast
        float castDistance = displacement.magnitude;

        // Radius ของ CircleCollider
        float radius = playerCollider.radius *
                      Mathf.Max(
                          transform.lossyScale.x,
                          transform.lossyScale.y
                      );

        // จุดเริ่มต้น
        Vector2 castOrigin = playerCollider.bounds.center;

        // จุดสิ้นสุด
        Vector2 castEnd = castOrigin + direction * castDistance;

        // สี Gizmos
        Gizmos.color = isSlowMotion ? Color.green : Color.yellow;

        // วาดวงกลมจุดเริ่มต้น
        Gizmos.DrawWireSphere(castOrigin, radius);

        // วาดวงกลมจุดสิ้นสุด
        Gizmos.DrawWireSphere(castEnd, radius);

        // วาดเส้น CircleCast
        Gizmos.DrawLine(castOrigin, castEnd);


        // ถ้า CircleCast ชน
        if (hasHit)
        {
            // จุดที่ชน
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(hitPoint, 0.08f);

            // เส้น Normal
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(hitPoint, hitNormal * 1f);

            // วาดเส้นขีดเล็ก ๆ ที่ปลาย Normal
            Vector2 normalEnd = hitPoint + hitNormal;

            Gizmos.DrawSphere(normalEnd, 0.04f);
        }
    }
}
