
using UnityEngine;
using System.Collections;

public class Decayed : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float timeBeforeBreak = 2f;
    [SerializeField] private float timeBeforeRestore = 3f;

    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float topContactTolerance = 0.25f;

    [Header("Platform Components")]
    [SerializeField] private SpriteRenderer platformSprite;
    [SerializeField] private Collider2D platformCollider;

    private bool isBreaking;
    private bool isBroken;
    private float standTimer;

    private void Awake()
    {
        if (platformCollider == null)
            platformCollider = GetComponent<Collider2D>();

        if (platformSprite == null)
            platformSprite = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (!isBreaking || isBroken)
            return;

        // เริ่มนับแล้วจะนับต่อจนกว่าพื้นจะพัง
        standTimer += Time.deltaTime;

        if (standTimer >= timeBeforeBreak)
            BreakPlatform();
    }

    private bool IsPlayerLayer(Collider2D col)
    {
        if (col == null)
            return false;

        return (playerLayer.value & (1 << col.gameObject.layer)) != 0;
    }

    private Collider2D GetPlayerCollider(Collision2D collision)
    {
        if (collision.collider != null &&
            IsPlayerLayer(collision.collider))
        {
            return collision.collider;
        }

        if (collision.otherCollider != null &&
            IsPlayerLayer(collision.otherCollider))
        {
            return collision.otherCollider;
        }

        return null;
    }

    private bool IsStandingOnTop(
        Collision2D collision,
        Collider2D playerCollider)
    {
        if (platformCollider == null || playerCollider == null)
            return false;

        float platformTop = platformCollider.bounds.max.y;
        float playerBottom = playerCollider.bounds.min.y;

        // ตรวจว่าด้านล่างของ Tumi อยู่ใกล้ขอบบนของพื้น
        if (Mathf.Abs(playerBottom - platformTop) > topContactTolerance)
        {
            return false;
        }

        // ตรวจว่าจุดสัมผัสอยู่บนผิวด้านบนของพื้น
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (Mathf.Abs(contact.point.y - platformTop) <= topContactTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckPlayerStanding(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckPlayerStanding(collision);
    }

    private void CheckPlayerStanding(Collision2D collision)
    {
        if (isBreaking || isBroken)
            return;

        Collider2D playerCollider = GetPlayerCollider(collision);

        if (playerCollider == null)
            return;

        if (!IsStandingOnTop(collision, playerCollider))
            return;

        // เหยียบด้านบนครั้งแรกแล้วเริ่มนับทันที
        isBreaking = true;
        standTimer = 0f;
    }

    private void BreakPlatform()
    {
        isBreaking = false;
        isBroken = true;
        standTimer = 0f;

        // ซ่อนพื้นและปิดการชน
        if (platformSprite != null)
            platformSprite.enabled = false;

        if (platformCollider != null)
            platformCollider.enabled = false;

        StartCoroutine(RestorePlatform());
    }

    private IEnumerator RestorePlatform()
    {
        yield return new WaitForSeconds(timeBeforeRestore);

        // สร้างพื้นกลับมา
        if (platformSprite != null)
            platformSprite.enabled = true;

        if (platformCollider != null)
            platformCollider.enabled = true;

        isBroken = false;
        isBreaking = false;
        standTimer = 0f;

    }

    private void OnDrawGizmosSelected()
    {
        Collider2D col = platformCollider;

        if (col == null)
            col = GetComponent<Collider2D>();

        if (col == null)
            return;

        Gizmos.color = isBroken ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
