using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(RiverWaterMask))]
public sealed class RiverImagePiece : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private RiverWaterMask waterMask;
    [SerializeField] private Vector2 entryAnchor = new Vector2(-5f, 0f);
    [SerializeField] private Vector2 exitAnchor = new Vector2(5f, 0f);
    [SerializeField, Min(0.01f)] private float nativeWaterWidth = 4f;

    public Vector2 EntryAnchor => entryAnchor;
    public Vector2 ExitAnchor => exitAnchor;
    public float NativeWaterWidth => nativeWaterWidth;
    public Vector2 WorldEntry => transform.TransformPoint(entryAnchor);
    public Vector2 WorldExit => transform.TransformPoint(exitAnchor);

    public void Configure(SpriteRenderer renderer, RiverWaterMask mask)
    {
        targetRenderer = renderer;
        waterMask = mask;
        ScanWaterAnchors();
    }

    public void SetAnchors(Vector2 entry, Vector2 exit, float waterWidth)
    {
        entryAnchor = entry;
        exitAnchor = exit;
        nativeWaterWidth = Mathf.Max(0.01f, waterWidth);
    }

    public bool ScanWaterAnchors()
    {
        ResolveReferences();
        if (targetRenderer == null || targetRenderer.sprite == null || waterMask == null) return false;

        if (!TryFindEdgeWater(true, out float entryU, out float entryCenter, out float entryWidth)
            || !TryFindEdgeWater(false, out float exitU, out float exitCenter, out float exitWidth))
        {
            Bounds fallbackBounds = targetRenderer.sprite.bounds;
            entryAnchor = new Vector2(fallbackBounds.min.x, 0f);
            exitAnchor = new Vector2(fallbackBounds.max.x, 0f);
            nativeWaterWidth = Mathf.Max(0.01f, fallbackBounds.size.y * 0.4f);
            return false;
        }

        Bounds bounds = targetRenderer.sprite.bounds;
        entryAnchor = new Vector2(
            Mathf.Lerp(bounds.min.x, bounds.max.x, entryU),
            Mathf.Lerp(bounds.min.y, bounds.max.y, entryCenter));
        exitAnchor = new Vector2(
            Mathf.Lerp(bounds.min.x, bounds.max.x, exitU),
            Mathf.Lerp(bounds.min.y, bounds.max.y, exitCenter));
        nativeWaterWidth = Mathf.Max(0.01f, (entryWidth + exitWidth) * 0.5f * bounds.size.y);
        return true;
    }

    private bool TryFindEdgeWater(bool left, out float u, out float center, out float width)
    {
        const int horizontalSteps = 18;
        const int verticalSteps = 256;
        for (int x = 0; x < horizontalSteps; x++)
        {
            float edgeDistance = 0.01f + x * (0.13f / (horizontalSteps - 1));
            float sampleU = left ? edgeDistance : 1f - edgeDistance;
            float minV = 1f;
            float maxV = 0f;
            bool found = false;

            for (int y = 0; y <= verticalSteps; y++)
            {
                float v = y / (float)verticalSteps;
                if (!waterMask.IsWaterAtUv(new Vector2(sampleU, v))) continue;
                found = true;
                minV = Mathf.Min(minV, v);
                maxV = Mathf.Max(maxV, v);
            }

            if (!found || maxV - minV < 0.02f) continue;
            u = sampleU;
            center = (minV + maxV) * 0.5f;
            width = maxV - minV;
            return true;
        }

        u = left ? 0f : 1f;
        center = 0.5f;
        width = 0f;
        return false;
    }

    private void ResolveReferences()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (waterMask == null) waterMask = GetComponent<RiverWaterMask>();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 entry = transform.TransformPoint(entryAnchor);
        Vector3 exit = transform.TransformPoint(exitAnchor);
        Gizmos.color = new Color(0.2f, 0.95f, 1f, 0.95f);
        Gizmos.DrawLine(entry, exit);
        Gizmos.DrawSphere(entry, 0.45f);
        Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.95f);
        Gizmos.DrawSphere(exit, 0.45f);
    }
}
