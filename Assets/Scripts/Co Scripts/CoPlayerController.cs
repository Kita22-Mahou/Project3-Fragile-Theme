using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using static UnityEngine.UI.Image;

public class CoPlayerController : MonoBehaviour
{
    public enum JumpState
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
    [SerializeField] public bool canAirJump = true;
    private float AirAimingMaxChargeTime;
    private float NormalMaxChargeTime;

    [Header("In Shell Setting")]
    [SerializeField] private float inShellTime = 1;

    [Header("Mouse Aim Setting")]
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

    [HideInInspector]
    private Rigidbody2D rb;
    private Camera mainCamera;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        mainCamera = Camera.main;

        AirAimingMaxChargeTime = maxChargeTime / 2f;
        NormalMaxChargeTime = maxChargeTime;

        Grounded();
        CreateTrajectoryDots();
    }

    void Start()
    {

    }


    void Update()
    {
        // การกด mouse
        if (Input.GetMouseButtonDown(0))          // กดเพื่อ Aim
        {
            //Debug.Log("Click");

            // เล็งจากพื้น
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
            Time.timeScale = 0.5f;
            maxChargeTime = AirAimingMaxChargeTime;
        }
        else
        {
            Time.timeScale = 1f;
            maxChargeTime = NormalMaxChargeTime;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame) ////////////////////////////////////////
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

        ChangeAnimation();
        UpdateJumpTrajectory();
        //UpdateFacingDirection();
    }

    // ตัวตรวจ Ground
    public void Grounded()
    {
        bool groundDetected =
        Physics2D.Raycast(groundCheckPoint.position, Vector2.down, groundCheckY, groundLayer)
        || Physics2D.Raycast(groundCheckPoint.position + new Vector3(groundCheckX, 0, 0), Vector2.down, groundCheckY, groundLayer)
        || Physics2D.Raycast(groundCheckPoint.position - new Vector3(groundCheckX, 0, 0), Vector2.down, groundCheckY, groundLayer);

        // ถ้ากระโดดไปแล้ว และ อยู่ในอากาศ = false
        if (isJumped && jumpState == JumpState.InAir)
        {
            isOnGround = false;
        }

        // ถ้าตรวจเจอพื้น และ ความเร็วน้อยมากๆ = true
        if (groundDetected && rb.linearVelocityY <= 0.01f)
        {
            isOnGround = true;
            isJumped = false;

            isJumpedFromGround = false;
        }
    }

    #region Aim Mechanic
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
        //Debug.Log("in JUMP");

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

        return new Vector2(
            jumpForwardForce,
            jumpForce
        ).magnitude * 1;//SlowJumpSpeedMultiplier;
    }

    private float GetChargePercent()
    {
        return maxChargeTime > 0f
            ? Mathf.Clamp01(jumpChargeTime / maxChargeTime)
            : 1f;
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

        if (!isAiming && !isAirAiming)
        {
            HideJumpTrajectory();
            return;
        }

        float launchSpeed = GetJumpPower();

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


    private IEnumerator ShellState()
    {
        jumpState = JumpState.InShell;
        isInShell = true;

        yield return new WaitForSeconds(inShellTime);

        jumpState = JumpState.InAir;
        isInShell = false;
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
}
