
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class JumpTrajectory : MonoBehaviour
{
    [Header("Jump Trajectory")]
    [SerializeField] private GameObject trajectoryPoint;
    [SerializeField] private GameObject trajectoryDotPrefab;
    [SerializeField] private int trajectoryDotCount = 36;
    [SerializeField] private float trajectoryTimeStep = 0.05f;

    [Header("Trajectory Colors")]
    [SerializeField] private Color earlyColor = Color.green;
    [SerializeField] private Color middleColor = Color.yellow;
    [SerializeField] private Color lateColor = Color.red;
    [SerializeField] private LayerMask collisionLayers;

    private Rigidbody2D rb;

    private Transform[] trajectoryDots;
    private SpriteRenderer[] trajectoryDotRenderers;

    // Timer สำหรับนับเวลาจริงตั้งแต่เริ่มเล็ง
    private float aimStartTime;
    private bool wasAiming;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        CreateTrajectoryDots();
    }

    private void CreateTrajectoryDots()
    {

        trajectoryDotCount = Mathf.Max(1, trajectoryDotCount);

        trajectoryDots = new Transform[trajectoryDotCount];
        trajectoryDotRenderers = new SpriteRenderer[trajectoryDotCount];

        for (int i = 0; i < trajectoryDotCount; i++)
        {
            GameObject dot = Instantiate(trajectoryDotPrefab);

            trajectoryDots[i] = dot.transform;

            trajectoryDotRenderers[i] = dot.GetComponentInChildren<SpriteRenderer>(true);

            dot.SetActive(false);
        }
    }

    private Color GetDotColor(float elapsedTime)
    {
        if (elapsedTime <= 0.4f)
            return earlyColor;

        if (elapsedTime < 0.5f)
        {
            float t = Mathf.InverseLerp(
                0.4f, 0.5f, elapsedTime
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            return Color.Lerp(earlyColor, middleColor, t);
        }

        if (elapsedTime <= 0.8f)
            return middleColor;

        if (elapsedTime < 0.9f)
        {
            float t = Mathf.InverseLerp(
                0.8f, 0.9f, elapsedTime
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            return Color.Lerp(middleColor, lateColor, t);
        }

        return lateColor;
    }

    public void UpdateTrajectory(
        bool isAiming,
        Vector2 aimDirection,
        float launchSpeed)
    {
        if (trajectoryDots == null || rb == null)
            return;

        if (!isAiming)
        {
            Hide();
            return;
        }

        if (!wasAiming)
        {
            aimStartTime = Time.unscaledTime;
            wasAiming = true;
        }

        float elapsedTime = Time.unscaledTime - aimStartTime;

        Color currentColor = GetDotColor(elapsedTime);

        Vector2 startPosition = trajectoryPoint != null
            ? trajectoryPoint.transform.position
            : rb.position;

        Vector2 initialVelocity = aimDirection * launchSpeed;

        float gravityY =
            Physics2D.gravity.y * rb.gravityScale;

        Vector2 previousPoint = startPosition;

        for (int i = 0; i < trajectoryDots.Length; i++)
        {
            float trajectoryTime = (i + 1) * trajectoryTimeStep;

            Vector2 point = startPosition
                + initialVelocity * trajectoryTime
                + Vector2.up * (
                    0.5f * gravityY *
                    trajectoryTime * trajectoryTime
                );

            trajectoryDots[i].gameObject.SetActive(true);
            trajectoryDots[i].position = point;

            if (trajectoryDotRenderers[i] != null)
            {
                trajectoryDotRenderers[i].color = currentColor;
            }

            Vector2 segment = point - previousPoint;

            RaycastHit2D[] hits = Physics2D.RaycastAll(
                previousPoint,
                segment.normalized,
                segment.magnitude,
                collisionLayers
            );

            RaycastHit2D closestHit = default;
            float closestDistance = float.MaxValue;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (hit.collider.attachedRigidbody == rb)
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                }
            }

            if (closestHit.collider != null)
            {
                trajectoryDots[i].position = closestHit.point;

                for (int j = i + 1; j < trajectoryDots.Length; j++)
                {
                    trajectoryDots[j].gameObject.SetActive(false);
                }

                break;
            }

            previousPoint = point;
        }
    }

    public void Hide()
    {
        wasAiming = false;

        if (trajectoryDots == null)
            return;

        foreach (Transform dot in trajectoryDots)
        {
            if (dot != null)
                dot.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        Hide();
    }
}
