
using UnityEngine;
using UnityEngine.Rendering;

public class CameraController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Camera Pan")]
    [SerializeField] private float panSpeed = 1f;
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Camera Boundaries")]
    [Header("Camera Boundaries")]
    [SerializeField] private Vector2 minBounds = new Vector2(-20f, -10f);
    [SerializeField] private Vector2 maxBounds = new Vector2(20f, 10f);

    private Camera cam;

    private Vector3 mousePos;
    private Vector3 dragOrigin;
    private Vector3 cameraStartPosition;
    private Vector3 smoothVelocity;

    [SerializeField] private bool isDragging;

    private void Awake()
    {
        cam = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (!isDragging)
        {
            transform.position = player.transform.position;
        }

        CameraPan();
    }

    private void CameraPan()
    {
        if (cam == null)
            return;

        // กด Mouse Middle Button
        if (Input.GetMouseButtonDown(2))
        {
            isDragging = true;

            dragOrigin = GetMouseWorldPosition();
            cameraStartPosition = transform.position;
        }

        // ปล่อย Mouse Middle Button
        if (Input.GetMouseButtonUp(2))
        {
            isDragging = false;
            transform.position = player.transform.position;
        }

        // ขณะกำลังกดเมาส์
        if (isDragging)
        {
            Vector3 currentMousePosition = GetMouseWorldPosition();

            Vector3 difference = dragOrigin - currentMousePosition;

            mousePos = cameraStartPosition + difference * panSpeed;

            // จำกัดขอบเขต
            mousePos.x = Mathf.Clamp(
                mousePos.x,
                player.position.x + minBounds.x,
                player.position.x + maxBounds.x
            );
            mousePos.y = Mathf.Clamp(
                mousePos.y,
                player.position.y + minBounds.y,
                player.position.y + maxBounds.y
            );

            // Smooth Movement
            transform.position = Vector3.SmoothDamp(
                transform.position,
                mousePos,
                ref smoothVelocity,
                smoothTime
            );
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;

        mousePosition.z = -cam.transform.position.z;

        return cam.ScreenToWorldPoint(mousePosition);
    }
    
}
