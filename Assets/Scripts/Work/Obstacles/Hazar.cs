using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Hazard : MonoBehaviour
{

    [SerializeField] private float respawnDelay = 1f;

    [SerializeField] private float reactivationCooldown = 1.25f;

    private readonly HashSet<MouseController> playersProcessing = new HashSet<MouseController>();


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
        MouseController player = other.GetComponentInParent<MouseController>();

        if (player == null || !playersProcessing.Add(player))
        {
            return;
        }

        HealthNew health = player.GetComponent<HealthNew>();

        if (health != null)
        {
            // ใช้ระบบตายเดียวกับตอน HP หมด
            // HealthNew จะรอ respawnDelay ก่อนเรียก Checkpoint
            health.DieFromHazard();
        }
        else
        {
            // สำรองกรณีตัวละครไม่มี HealthNew
            player.movementLocked = true;

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            StartCoroutine(RespawnWithoutHealth(player.transform));
        }

        StartCoroutine(ReleaseCooldown(player));
    }


    private IEnumerator RespawnWithoutHealth(Transform player)
    {
        yield return new WaitForSecondsRealtime(respawnDelay);

        if (player != null)
        {
            bool respawned = Checkpoint.RespawnPlayer(player);

            if (!respawned)
            {
                Debug.LogWarning("ไม่มี Checkpoint ที่บันทึกไว้");
            }
        }
    }


    private IEnumerator ReleaseCooldown(MouseController player)
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(respawnDelay, reactivationCooldown)
        );

        if (player != null)
        {
            playersProcessing.Remove(player);
        }
    }
}