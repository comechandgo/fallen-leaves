using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeafSpawner : MonoBehaviour
{
    [SerializeField] private LeafLifecycle leafPrefab;
    [SerializeField] private Rect walkableBounds = new Rect(-60f, -45f, 120f, 90f);
    [SerializeField] private LayerMask blockerMask;
    [SerializeField, Min(0f)] private float clearance = 1f;
    [SerializeField, Min(1)] private int maxAttemptsPerLeaf = 260;
    [SerializeField] private Transform leafContainer;

    private readonly HashSet<LeafLifecycle> activeLeaves = new HashSet<LeafLifecycle>();
    private readonly Collider2D[] blockerHits = new Collider2D[96];
    private ContactFilter2D blockerFilter;
    private bool filterReady;

    public int ActiveCount => activeLeaves.Count;
    public Rect WalkableBounds => walkableBounds;
    public float Clearance => clearance;

    public void Configure(
        LeafLifecycle prefab,
        Rect bounds,
        LayerMask blockers,
        float requiredClearance,
        int attempts,
        Transform container)
    {
        leafPrefab = prefab;
        walkableBounds = bounds;
        blockerMask = blockers;
        clearance = Mathf.Max(0f, requiredClearance);
        maxAttemptsPerLeaf = Mathf.Max(1, attempts);
        leafContainer = container;
        filterReady = false;
    }

    public void Initialize(LevelRoot level)
    {
        activeLeaves.Clear();
        EnsureContainer();
        EnsureFilter();
    }

    public int Spawn(int requestedCount)
    {
        if (requestedCount <= 0) return 0;
        if (leafPrefab == null)
        {
            Debug.LogError($"LeafSpawner on {name} is missing Leaf.prefab.", this);
            return 0;
        }

        EnsureContainer();
        EnsureFilter();
        Physics2D.SyncTransforms();
        int spawned = 0;

        for (int i = 0; i < requestedCount; i++)
        {
            if (!TryGetRandomPosition(out Vector2 position)) continue;

            LeafLifecycle instance = Instantiate(leafPrefab, position, Quaternion.identity, leafContainer);
            instance.name = "Leaf";

            LeafAppearance appearance = instance.GetComponent<LeafAppearance>();
            if (appearance != null) appearance.Randomize();

            instance.Bind(this);
            spawned++;
        }

        if (spawned < requestedCount)
        {
            Debug.LogWarning(
                $"{name} spawned {spawned}/{requestedCount} leaves because no valid walkable positions remained.",
                this);
        }

        return spawned;
    }

    public bool TryGetRandomPosition(out Vector2 position)
    {
        EnsureFilter();
        float minX = walkableBounds.xMin + clearance;
        float maxX = walkableBounds.xMax - clearance;
        float minY = walkableBounds.yMin + clearance;
        float maxY = walkableBounds.yMax - clearance;
        if (minX > maxX || minY > maxY)
        {
            position = default;
            return false;
        }

        for (int attempt = 0; attempt < maxAttemptsPerLeaf; attempt++)
        {
            Vector2 candidate = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
            if (IsBlocked(candidate)) continue;

            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    internal void Register(LeafLifecycle leaf)
    {
        if (leaf != null) activeLeaves.Add(leaf);
    }

    internal void Unregister(LeafLifecycle leaf)
    {
        if (leaf != null) activeLeaves.Remove(leaf);
    }

    private bool IsBlocked(Vector2 position)
    {
        int count = Physics2D.OverlapCircle(position, clearance, blockerFilter, blockerHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = blockerHits[i];
            if (hit == null) continue;

            RiverWaterMask waterMask = hit.GetComponentInParent<RiverWaterMask>();
            if (waterMask != null && !waterMask.IntersectsCircle(position, clearance)) continue;
            return true;
        }

        return false;
    }

    private void EnsureContainer()
    {
        if (leafContainer != null) return;
        Transform existing = transform.Find("Leaves");
        if (existing != null)
        {
            leafContainer = existing;
            return;
        }

        GameObject created = new GameObject("Leaves");
        created.transform.SetParent(transform, false);
        leafContainer = created.transform;
    }

    private void EnsureFilter()
    {
        if (filterReady) return;
        blockerFilter = new ContactFilter2D();
        blockerFilter.SetLayerMask(blockerMask);
        blockerFilter.useTriggers = true;
        filterReady = true;
    }

    private void OnDisable()
    {
        activeLeaves.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.9f, 0.45f, 0.35f);
        Rect safe = new Rect(
            walkableBounds.xMin + clearance,
            walkableBounds.yMin + clearance,
            Mathf.Max(0f, walkableBounds.width - clearance * 2f),
            Mathf.Max(0f, walkableBounds.height - clearance * 2f));
        Gizmos.DrawWireCube(safe.center, safe.size);
    }
}
