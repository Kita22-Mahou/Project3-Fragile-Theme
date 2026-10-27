using System.Collections;
using UnityEngine;

public class TitleLogoAnimator : MonoBehaviour
{
    [Header("Intro (เด้งตอนเปิดซีน)")]
    [SerializeField] float introDuration = 0.7f;
    [SerializeField] float introStartScale = 0.6f;

    [Header("Idle (เด้งต่อเนื่อง)")]
    [SerializeField] float pulseAmount = 0.05f;
    [SerializeField] float pulseSpeed = 3f;

    Vector3 baseScale;
    bool introDone;

    void Awake() => baseScale = transform.localScale;

    void OnEnable() => StartCoroutine(Intro());

    void OnDisable() => transform.localScale = baseScale;

    IEnumerator Intro()
    {
        introDone = false;
        float t = 0f;
        while (t < introDuration)
        {
            t += Time.unscaledDeltaTime;
            float e = EaseOutBack(Mathf.Clamp01(t / introDuration));
            transform.localScale = baseScale * Mathf.LerpUnclamped(introStartScale, 1f, e);
            yield return null;
        }
        transform.localScale = baseScale;
        introDone = true;
    }

    void Update()
    {
        if (!introDone) return;
        float s = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * s;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
    }
}