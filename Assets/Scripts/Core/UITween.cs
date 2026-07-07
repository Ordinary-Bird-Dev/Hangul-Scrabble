using System.Collections;
using UnityEngine;

// Lightweight coroutine tweens used instead of DOTween (not installed).
// All tweens are driven by the MonoBehaviour that StartCoroutine()s them,
// so they stop automatically when their host is destroyed or disabled.
public static class UITween
{
    // Scale punch: 1 -> 1+punch -> 1 with an ease-out settle.
    public static IEnumerator PunchScale(Transform target, float punch = 0.15f, float duration = 0.2f)
    {
        if (target == null) yield break;

        Vector3 baseScale = Vector3.one;
        float half = duration * 0.5f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            if (target == null) yield break;
            target.localScale = baseScale * (1f + punch * EaseOutCubic(t / half));
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            if (target == null) yield break;
            target.localScale = baseScale * (1f + punch * (1f - EaseOutCubic(t / half)));
            yield return null;
        }
        if (target != null) target.localScale = baseScale;
    }

    public static IEnumerator ScaleTo(Transform target, Vector3 to, float duration)
    {
        if (target == null) yield break;

        Vector3 from = target.localScale;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (target == null) yield break;
            target.localScale = Vector3.LerpUnclamped(from, to, EaseOutCubic(t / duration));
            yield return null;
        }
        if (target != null) target.localScale = to;
    }

    public static IEnumerator SlideTo(RectTransform target, Vector2 to, float duration)
    {
        if (target == null) yield break;

        Vector2 from = target.anchoredPosition;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (target == null) yield break;
            target.anchoredPosition = Vector2.LerpUnclamped(from, to, EaseOutCubic(t / duration));
            yield return null;
        }
        if (target != null) target.anchoredPosition = to;
    }

    // Scales the object up while fading a CanvasGroup out, then destroys it.
    public static IEnumerator BurstAndDestroy(GameObject target, float targetScale = 1.8f, float duration = 0.4f)
    {
        if (target == null) yield break;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.AddComponent<CanvasGroup>();

        Vector3 from = target.transform.localScale;
        Vector3 to = from * targetScale;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (target == null) yield break;
            float k = EaseOutCubic(t / duration);
            target.transform.localScale = Vector3.LerpUnclamped(from, to, k);
            group.alpha = 1f - k;
            yield return null;
        }
        if (target != null) Object.Destroy(target);
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }
}
