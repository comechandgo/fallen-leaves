using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class LeafSpawnArea : MonoBehaviour
{
    [SerializeField] private Collider2D areaCollider;
    [SerializeField] private LayerMask blockerMask;
    [SerializeField, Min(0f)] private float clearanceRadius = 0.5f;
    [SerializeField, Min(1)] private int maxAttempts = 220;

    private readonly Collider2D[] blockerHits = new Collider2D[64];
    private ContactFilter2D blockerFilter;
    private bool filterReady;

    public void Configure(Collider2D targetArea, LayerMask blockers, float clearance, int attempts)
    {
        areaCollider = targetArea;
        blockerMask = blockers;
        clearanceRadius = Mathf.Max(0f, clearance);
        maxAttempts = Mathf.Max(1, attempts);
        filterReady = false;
    }

    public bool TryGetRandomPosition(out Vector2 position)
    {
        if (areaCollider == null) areaCollider = GetComponent<Collider2D>();
        if (areaCollider == null)
        {
            position = default;
            return false;
        }

        EnsureFilter();
        Bounds bounds = areaCollider.bounds;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y));

            if (!areaCollider.OverlapPoint(candidate)) continue;
            if (IsBlocked(candidate)) continue;

            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    private bool IsBlocked(Vector2 position)
    {
        int count = Physics2D.OverlapCircle(position, clearanceRadius, blockerFilter, blockerHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = blockerHits[i];
            if (hit == null) continue;

            RiverWaterMask waterMask = hit.GetComponentInParent<RiverWaterMask>();
            if (waterMask != null && !waterMask.ContainsWater(position)) continue;

            return true;
        }

        return false;
    }

    private void EnsureFilter()
    {
        if (filterReady) return;

        blockerFilter = new ContactFilter2D();
        blockerFilter.SetLayerMask(blockerMask);
        blockerFilter.useTriggers = true;
        filterReady = true;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D target = areaCollider != null ? areaCollider : GetComponent<Collider2D>();
        if (target == null) return;

        Gizmos.color = new Color(0.35f, 0.9f, 0.45f, 0.35f);
        Bounds bounds = target.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
