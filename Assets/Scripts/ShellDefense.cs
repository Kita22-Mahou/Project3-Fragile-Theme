
using UnityEngine;
using UnityEngine.UI;

public class ShellDefense : MonoBehaviour
{
    [Header("Shell Uses")]
    [SerializeField] private int maxShellUses = 2;
    [SerializeField] private float shellDuration = 1f;

    [Header("Shell Sprites")]
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite shellSprite;
    private MouseController mouseController;

    [Header("ShellIcons")]
    [SerializeField] private Image[] shellUseIcons = new Image[2];

    private Rigidbody2D rb;

    private int remainingUses;
    private float shellTimer;

    private bool isShelling;
    private bool controllerWasEnabled;

    public int RemainingUses => remainingUses;
    public bool IsShelling => isShelling;

    private void Awake()
    {
        maxShellUses = Mathf.Max(0, maxShellUses);
        shellDuration = Mathf.Max(0.1f, shellDuration);

        // เริ่มเกมด้วยสิทธิ์เข้ากระดองเต็มจำนวน
        remainingUses = maxShellUses;

        rb = GetComponent<Rigidbody2D>();

        if (mouseController == null)
            mouseController = GetComponent<MouseController>();

        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();

        
        if (playerSprite != null && normalSprite == null)
            normalSprite = playerSprite.sprite;
      
        UpdateShellIcons();
    }

    private void Update()
    {
        // กด Shift ซ้ายหรือขวาเพื่อเข้ากระดอง
        if (Input.GetKeyDown(KeyCode.LeftShift) ||
            Input.GetKeyDown(KeyCode.RightShift))
        {
            EnterShell();
        }


        if (isShelling)
        {
            shellTimer -= Time.deltaTime;

            if (shellTimer <= 0f)
                ExitShell();
        }
    }

    private void FixedUpdate()
    {
        if (!isShelling || rb == null)
            return;

        // แต่ยังตกตามแรงโน้มถ่วงได้
        rb.linearVelocity = new Vector2(
            0f,
            rb.linearVelocity.y
        );
    }

    private void EnterShell()
    {
        if (isShelling)
            return;

        if (remainingUses <= 0)
        {
            return;
        }

        
        remainingUses--;

        isShelling = true;
        shellTimer = shellDuration;

        playerSprite.sprite = shellSprite;

        if (mouseController != null)
        {
            controllerWasEnabled = mouseController.enabled;

            if (controllerWasEnabled)
                mouseController.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(
                0f,
                rb.linearVelocity.y
            );
        }

        UpdateShellIcons();
    }

    public bool TryBlockDamage()
    {
        if (!isShelling)
            return false;

        return true;
    }

    private void ExitShell()
    {
        if (!isShelling)
            return;

        isShelling = false;
        shellTimer = 0f;

        if (playerSprite != null && normalSprite != null)
            playerSprite.sprite = normalSprite;

        if (mouseController != null && controllerWasEnabled)
            mouseController.enabled = true;

        controllerWasEnabled = false;

    }

    private void UpdateShellIcons()
    {
        if (shellUseIcons == null)
            return;

        for (int i = 0; i < shellUseIcons.Length; i++)
        {
            Image icon = shellUseIcons[i];

            if (icon == null)
                continue;

            icon.gameObject.SetActive(i < remainingUses);
        }
    }
}
