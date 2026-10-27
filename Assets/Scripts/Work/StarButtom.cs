using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class StartButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] string nextSceneName = "Game";

    [Header("Hover")]
    [SerializeField] float hoverScale = 1.3f;
    [SerializeField] float lerpSpeed = 12f;

    [Header("Click")]
    [SerializeField] float punchDuration = 0.25f;
    [SerializeField] float punchStrength = 0.2f;

    [Header("Fade (ไม่บังคับ)")]
    [SerializeField] CanvasGroup fadeGroup;
    [SerializeField] float fadeDuration = 0.5f;

    Vector3 baseScale;
    Collider2D col;
    Camera cam;
    bool clicked;

    void Awake()
    {
        baseScale = transform.localScale;
        col = GetComponent<Collider2D>();
        cam = Camera.main;
        if (fadeGroup)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }
    }

    void OnDisable()
    {
        clicked = false;
        transform.localScale = baseScale;
    }

    void Update()
    {
        if (clicked) return;
        Vector3 target = IsHovering() ? baseScale * hoverScale : baseScale;
        transform.localScale = Vector3.Lerp(
            transform.localScale, target, Time.unscaledDeltaTime * lerpSpeed);
    }

    bool IsHovering()
    {
        if (cam == null) return false;

        Vector2 mouse;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current == null) return false;
        mouse = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#else
        mouse = Input.mousePosition;
#endif
        Vector3 world = cam.ScreenToWorldPoint(
            new Vector3(mouse.x, mouse.y, -cam.transform.position.z));
        return col.OverlapPoint(world);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (clicked) return;
        clicked = true;
        StartCoroutine(ClickRoutine());
    }

    IEnumerator ClickRoutine()
    {
        Vector3 from = transform.localScale;
        for (float t = 0f; t < punchDuration;)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / punchDuration);
            float s = 1f - punchStrength * Mathf.Sin(k * Mathf.PI * 2f) * (1f - k);
            transform.localScale = from * s;
            yield return null;
        }
        transform.localScale = from;

        if (fadeGroup)
        {
            fadeGroup.blocksRaycasts = true;
            for (float t = 0f; t < fadeDuration;)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            fadeGroup.alpha = 1f;
        }

        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}