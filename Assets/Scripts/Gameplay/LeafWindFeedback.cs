using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeafWindFeedback : MonoBehaviour
{
    [SerializeField] private Transform windDeform;
    [SerializeField] private Transform spriteVisual;

    [Header("Wind squash")]
    [SerializeField, Range(0.8f, 1f)] private float compressedScale = 0.94f;
    [SerializeField, Range(1f, 1.2f)] private float expandedScale = 1.03f;
    [SerializeField, Range(1f, 1.1f)] private float reboundScale = 1.015f;
    [SerializeField, Min(0.01f)] private float peakTime = 0.04f;
    [SerializeField, Min(0.02f)] private float reboundTime = 0.12f;
    [SerializeField, Min(0.03f)] private float totalDuration = 0.22f;

    private float elapsed;

    public void Configure(Transform deform, Transform visual)
    {
        windDeform = deform;
        spriteVisual = visual;
        ResetPose();
        enabled = false;
    }

    private void Awake()
    {
        EnsureReferences();
        ResetPose();
        enabled = false;
    }

    public void Play(Vector2 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        EnsureReferences();
        if (windDeform == null || spriteVisual == null)
        {
            return;
        }

        Vector3 localDirection3 = transform.InverseTransformVector(worldDirection.normalized);
        Vector2 localDirection = new Vector2(localDirection3.x, localDirection3.y).normalized;
        float angle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;

        windDeform.localRotation = Quaternion.Euler(0f, 0f, angle);
        spriteVisual.localRotation = Quaternion.Euler(0f, 0f, -angle);
        windDeform.localScale = Vector3.one;
        spriteVisual.localScale = Vector3.one;

        elapsed = 0f;
        enabled = true;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        ApplyPose(elapsed);
    }

    private void ApplyPose(float time)
    {
        if (windDeform == null || spriteVisual == null)
        {
            enabled = false;
            return;
        }

        float safePeakTime = Mathf.Max(0.01f, peakTime);
        float safeReboundTime = Mathf.Max(safePeakTime, reboundTime);
        float safeDuration = Mathf.Max(safeReboundTime, totalDuration);

        if (time >= safeDuration)
        {
            ResetPose();
            enabled = false;
            return;
        }

        Vector2 peakScale = new Vector2(compressedScale, expandedScale);
        Vector2 rebound = new Vector2(reboundScale, 2f - reboundScale);
        Vector2 currentScale;

        if (time <= safePeakTime)
        {
            float rate = Smooth01(time / safePeakTime);
            currentScale = Vector2.LerpUnclamped(Vector2.one, peakScale, rate);
        }
        else if (time <= safeReboundTime)
        {
            float rate = Smooth01((time - safePeakTime) / Mathf.Max(0.0001f, safeReboundTime - safePeakTime));
            currentScale = Vector2.LerpUnclamped(peakScale, rebound, rate);
        }
        else
        {
            float rate = Smooth01((time - safeReboundTime) / Mathf.Max(0.0001f, safeDuration - safeReboundTime));
            currentScale = Vector2.LerpUnclamped(rebound, Vector2.one, rate);
        }

        windDeform.localScale = new Vector3(currentScale.x, currentScale.y, 1f);
    }

    private void EnsureReferences()
    {
        if (windDeform == null)
        {
            windDeform = transform.Find("WindDeform");
        }

        if (spriteVisual == null && windDeform != null)
        {
            spriteVisual = windDeform.Find("SpriteVisual");
        }
    }

    private void ResetPose()
    {
        if (windDeform != null)
        {
            windDeform.localPosition = Vector3.zero;
            windDeform.localRotation = Quaternion.identity;
            windDeform.localScale = Vector3.one;
        }

        if (spriteVisual != null)
        {
            spriteVisual.localPosition = Vector3.zero;
            spriteVisual.localRotation = Quaternion.identity;
            spriteVisual.localScale = Vector3.one;
        }

        elapsed = 0f;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
