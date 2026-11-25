
using UnityEngine;

public class FlipAnimation : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector3 originalScale;

    [SerializeField] private float flipThreshold = 0.5f;

    private void Start()
    {
        rb = GetComponentInParent<Rigidbody2D>();
        originalScale = transform.localScale;
    }

    private void Update()
    {
        float horizontal = rb.linearVelocityX;

        if (horizontal > flipThreshold)
        {
            transform.localScale = new Vector3(
                Mathf.Abs(originalScale.x),
                originalScale.y,
                originalScale.z
            );
        }
        else if (horizontal < -flipThreshold)
        {
            transform.localScale = new Vector3(
                -Mathf.Abs(originalScale.x),
                originalScale.y,
                originalScale.z
            );
        }
    }
}
