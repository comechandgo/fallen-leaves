using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeafDropSource : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sourceRenderer;
    [SerializeField, Min(0f)] private float radiusWidthMultiplier = 0.65f;
    [SerializeField, Min(0f)] private float radiusPadding = 2f;
    [SerializeField, Min(0.1f)] private float minimumRadius = 5f;
    [SerializeField, Min(0.1f)] private float maximumRadius = 12f;
    [SerializeField, Range(0f, 0.5f)] private float groundHeightRatio = 0.1f;

    public Vector2 DropCenter
    {
        get
        {
            SpriteRenderer renderer = ResolveRenderer();
            if (renderer == null || renderer.sprite == null) return transform.position;

            Bounds spriteBounds = renderer.sprite.bounds;
            Vector2 localGround = new Vector2(
                spriteBounds.center.x,
                spriteBounds.min.y + spriteBounds.size.y * groundHeightRatio);
            return renderer.transform.TransformPoint(localGround);
        }
    }

    public float InfluenceRadius
    {
        get
        {
            SpriteRenderer renderer = ResolveRenderer();
            float worldWidth = renderer != null ? renderer.bounds.size.x : 0f;
            return Mathf.Clamp(
                worldWidth * radiusWidthMultiplier + radiusPadding,
                Mathf.Min(minimumRadius, maximumRadius),
                Mathf.Max(minimumRadius, maximumRadius));
        }
    }

    public float InfluenceAreaWeight
    {
        get
        {
            float radius = InfluenceRadius;
            return radius * radius;
        }
    }

    public void Configure(SpriteRenderer renderer)
    {
        sourceRenderer = renderer;
    }

    public bool Contains(Vector2 worldPosition)
    {
        float radius = InfluenceRadius;
        return (worldPosition - DropCenter).sqrMagnitude <= radius * radius;
    }

    public Vector2 GetRandomPosition()
    {
        float angle = Random.value * Mathf.PI * 2f;
        float distance = Mathf.Sqrt(Random.value) * InfluenceRadius;
        return DropCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    private SpriteRenderer ResolveRenderer()
    {
        if (sourceRenderer == null) sourceRenderer = GetComponent<SpriteRenderer>();
        return sourceRenderer;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.95f, 0.55f, 0.12f, 0.55f);
        DrawCircle(DropCenter, InfluenceRadius, 48);
    }

    private static void DrawCircle(Vector2 center, float radius, int segments)
    {
        Vector3 previous = center + Vector2.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
