
using UnityEngine;
using System.Collections;

public class BounceSurface : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Bounce Settings")]
    [SerializeField] private float bounceMultiplier = 1.2f;
    [SerializeField] private float minimumBounceSpeed = 15f;

    private float controlLockDuration = 0.15f;

    private Collider2D surfaceCollider;
    private MouseController mouseController;
    private Coroutine unlockCoroutine;

    private void Awake()
    {
        surfaceCollider = GetComponent<Collider2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D playerCollider = FindPlayerCollider(collision);

        if (playerCollider == null)
            return;

        Rigidbody2D playerRb = playerCollider.attachedRigidbody;

        if (playerRb == null)
            return;

        // หาตำแหน่งของ Tumi เทียบกับพื้น
        Vector2 toPlayer =
            playerRb.worldCenterOfMass - (Vector2)surfaceCollider.bounds.center;

        ContactPoint2D sideContact = new ContactPoint2D();
        bool foundSide = false;
        float strongestSide = 0f;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            Vector2 normal = contact.normal;

            // รับเฉพาะการชนด้านข้าง
            if (Mathf.Abs(normal.x) < 0.7f ||
                Mathf.Abs(normal.x) <= Mathf.Abs(normal.y))
                continue;

            // หัน Normal ออกจากพื้นเข้าหาตัวผู้เล่น
            if (Vector2.Dot(normal, toPlayer) < 0f)
                normal = -normal;

            if (Mathf.Abs(normal.x) > strongestSide)
            {
                strongestSide = Mathf.Abs(normal.x);
                sideContact = contact;
                foundSide = true;
            }
        }

        if (!foundSide)
        {
            return;
        }

        Vector2 normalOut = sideContact.normal;

        if (Vector2.Dot(normalOut, toPlayer) < 0f)
            normalOut = -normalOut;

        normalOut.Normalize();

        // ใช้ความเร็วสัมพัทธ์ช่วยหาความเร็วขณะพุ่งเข้าหาพื้น
        Vector2 relativeVelocity = collision.relativeVelocity;
        float impactSpeed = Mathf.Abs(
            Vector2.Dot(relativeVelocity, normalOut)
        );

        float currentNormalSpeed = Mathf.Abs(
            Vector2.Dot(playerRb.linearVelocity, normalOut)
        );

        impactSpeed = Mathf.Max(
            impactSpeed,
            currentNormalSpeed
        );

        impactSpeed = Mathf.Max(impactSpeed, 0.1f);

        // เก็บความเร็วตามแนวสัมผัสไว้ เพื่อรักษามุมการเคลื่อนที่
        Vector2 currentVelocity = playerRb.linearVelocity;

        Vector2 tangentVelocity =
            currentVelocity -
            Vector2.Dot(currentVelocity, normalOut) * normalOut;

        Vector2 incomingVelocity =
            tangentVelocity - normalOut * impactSpeed;

        Vector2 reflectedVelocity = Vector2.Reflect(
            incomingVelocity,
            normalOut
        );

        reflectedVelocity *= bounceMultiplier;

        float outwardSpeed = Vector2.Dot(
            reflectedVelocity,
            normalOut
        );

        if (outwardSpeed < minimumBounceSpeed)
        {
            reflectedVelocity += normalOut *
                (minimumBounceSpeed - outwardSpeed);
        }

        playerRb.position += normalOut * 0.03f;

        playerRb.linearVelocity = reflectedVelocity;

        // ป้องกัน MouseController เขียนทับความเร็วเด้ง
        TemporarilyLockControl(playerRb);

    }

    private Collider2D FindPlayerCollider(Collision2D collision)
    {
        if (IsPlayerLayer(collision.collider))
            return collision.collider;

        if (IsPlayerLayer(collision.otherCollider))
            return collision.otherCollider;

        return null;
    }

    private bool IsPlayerLayer(Collider2D col)
    {
        if (col == null)
            return false;

        // รองรับ Collider ที่อยู่บน GameObject ลูก
        bool colliderLayerMatches =
            (playerLayer.value & (1 << col.gameObject.layer)) != 0;

        Rigidbody2D body = col.attachedRigidbody;

        bool bodyLayerMatches = body != null &&
            (playerLayer.value & (1 << body.gameObject.layer)) != 0;

        return colliderLayerMatches || bodyLayerMatches;
    }

    private void TemporarilyLockControl(Rigidbody2D playerRb)
    {
        MouseController controller =
            playerRb.GetComponent<MouseController>();

        if (controller == null || !controller.enabled)
            return;

        mouseController = controller;
        mouseController.enabled = false;

        if (unlockCoroutine != null)
            StopCoroutine(unlockCoroutine);

        unlockCoroutine = StartCoroutine(RestoreControl());
    }

    private IEnumerator RestoreControl()
    {
        yield return new WaitForSeconds(controlLockDuration);

        if (mouseController != null)
            mouseController.enabled = true;

        mouseController = null;
        unlockCoroutine = null;
    }
}
