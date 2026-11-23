using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hazard : MonoBehaviour
{
    [Header("Knockback")]
    // แรงเด้งถอยหลังแนวนอน
    [SerializeField] private float knockbackX = 8f;
    [SerializeField] private float knockbackY = 6f;

    private void Reset()
    {
        Collider2D hazardCollider = GetComponent<Collider2D>();

        if (hazardCollider != null)
        {
            hazardCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider2D hazardCollider = GetComponent<Collider2D>();

        if (hazardCollider != null)
        {
            hazardCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ProcessPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // เด้งซ้ำทุกครั้งที่ Physics ตรวจพบว่ายังสัมผัส Hazard อยู่
        ProcessPlayer(other);
    }

    private void ProcessPlayer(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player == null)
        {
            return;
        }

        HealthNew health = player.GetComponent<HealthNew>();

        if (health != null)
        {
            KnockBackPlayer(player);
        }
        else
        {
            player.movementLocked = true;

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private void KnockBackPlayer(PlayerController player)
    {
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            return;
        }

        float directionX = player.transform.position.x >= transform.position.x ? 1f : -1f;

        rb.linearVelocity = new Vector2(directionX * knockbackX, knockbackY);
    }
}
