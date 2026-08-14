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
    [SerializeField, Range(0f, 1f)] private float treeSpawnRatio = 0.7f;
    [SerializeField, Min(1)] private int initialSpawnPerFrame = 64;
    [SerializeField] private Transform leafContainer;

    private readonly HashSet<LeafLifecycle> activeLeaves = new HashSet<LeafLifecycle>();
    private readonly Stack<LeafLifecycle> pooledLeaves = new Stack<LeafLifecycle>();
    private readonly List<LeafDropSource> treeSources = new List<LeafDropSource>(16);
    private readonly Collider2D[] blockerHits = new Collider2D[96];
    private ContactFilter2D blockerFilter;
    private bool filterReady;
    private int treeQuotaRemainderUnits;
    private int pendingInitialLeaves;
    private int initialSpawnTarget;

    public int ActiveCount => activeLeaves.Count;
    public Rect WalkableBounds => walkableBounds;
    public float Clearance => clearance;
    public bool IsReady { get; private set; } = true;
    public int TreeSourceCount => treeSources.Count;

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
        pooledLeaves.Clear();
        treeSources.Clear();
        if (level != null) treeSources.AddRange(level.GetComponentsInChildren<LeafDropSource>(true));
        treeQuotaRemainderUnits = 0;
        pendingInitialLeaves = 0;
        initialSpawnTarget = 0;
        IsReady = true;
        EnsureContainer();
        EnsureFilter();
    }

    public void BeginInitialSpawn(int requestedCount)
    {
        initialSpawnTarget = Mathf.Max(0, requestedCount);
        pendingInitialLeaves = initialSpawnTarget;
        IsReady = pendingInitialLeaves == 0;
    }

    public void TickInitialSpawn()
    {
        if (IsReady) return;

        int batch = Mathf.Min(Mathf.Max(1, initialSpawnPerFrame), pendingInitialLeaves);
        Spawn(batch);
        pendingInitialLeaves -= batch;

        if (pendingInitialLeaves > 0) return;

        IsReady = true;
        if (activeLeaves.Count < initialSpawnTarget)
        {
            Debug.LogWarning(
                $"{name} completed initial leaf fill with {activeLeaves.Count}/{initialSpawnTarget} leaves. " +
                $"Valid positions were exhausted after checking {treeSources.Count} tree sources, map bounds, water, and obstacles.",
                this);
        }
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

        const int quotaUnits = 1000;
        int treeRatioUnits = Mathf.Clamp(Mathf.RoundToInt(treeSpawnRatio * quotaUnits), 0, quotaUnits);
        int exactTreeQuotaUnits = requestedCount * treeRatioUnits + treeQuotaRemainderUnits;
        int requestedNearTrees = Mathf.Clamp(exactTreeQuotaUnits / quotaUnits, 0, requestedCount);
        treeQuotaRemainderUnits = exactTreeQuotaUnits % quotaUnits;
        int requestedOrdinary = requestedCount - requestedNearTrees;

        int spawnedNearTrees = SpawnNearTrees(requestedNearTrees);
        int treeFallbacks = requestedNearTrees - spawnedNearTrees;
        int spawnedOrdinary = SpawnOrdinary(requestedOrdinary + treeFallbacks);
        int spawned = spawnedNearTrees + spawnedOrdinary;

        if (spawned < requestedCount)
        {
            Debug.LogWarning(
                $"{name} spawned {spawned}/{requestedCount} leaves " +
                $"(tree target {requestedNearTrees}, tree actual {spawnedNearTrees}, ordinary actual {spawnedOrdinary}). " +
                "No valid positions remained inside the safe map bounds after excluding water, obstacles, tree trunks, and tree influence from ordinary samples.",
                this);
        }

        return spawned;
    }

    public bool TryGetRandomPosition(out Vector2 position)
    {
        if (treeSources.Count > 0 && Random.value < treeSpawnRatio && TryGetTreePosition(out position))
        {
            return true;
        }

        return TryGetOrdinaryPosition(out position);
    }

    internal void Register(LeafLifecycle leaf)
    {
        if (leaf != null) activeLeaves.Add(leaf);
    }

    internal void Unregister(LeafLifecycle leaf)
    {
        if (leaf != null) activeLeaves.Remove(leaf);
    }

    internal void ReturnToPool(LeafLifecycle leaf)
    {
        if (leaf == null) return;

        Windable windable = leaf.GetComponent<Windable>();
        if (windable != null) windable.PrepareForPool();

        LeafWindFeedback feedback = leaf.GetComponent<LeafWindFeedback>();
        if (feedback != null) feedback.ResetFeedback();

        leaf.gameObject.SetActive(false);
        pooledLeaves.Push(leaf);
    }

    private int SpawnNearTrees(int requestedCount)
    {
        int spawned = 0;
        for (int i = 0; i < requestedCount; i++)
        {
            if (!TryGetTreePosition(out Vector2 position)) continue;
            SpawnLeaf(position, true);
            spawned++;
        }
        return spawned;
    }

    private int SpawnOrdinary(int requestedCount)
    {
        int spawned = 0;
        for (int i = 0; i < requestedCount; i++)
        {
            if (!TryGetOrdinaryPosition(out Vector2 position)) continue;
            SpawnLeaf(position, false);
            spawned++;
        }
        return spawned;
    }

    private void SpawnLeaf(Vector2 position, bool nearTree)
    {
        LeafLifecycle instance;
        if (pooledLeaves.Count > 0)
        {
            instance = pooledLeaves.Pop();
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.gameObject.SetActive(true);
        }
        else
        {
            instance = Instantiate(leafPrefab, position, Quaternion.identity, leafContainer);
            instance.name = "Leaf";
        }

        LeafAppearance appearance = instance.GetComponent<LeafAppearance>();
        if (appearance != null) appearance.Randomize();

        Windable windable = instance.GetComponent<Windable>();
        if (windable != null) windable.ResetForSpawn();

        instance.Bind(this, nearTree);
    }

    private bool TryGetTreePosition(out Vector2 position)
    {
        if (treeSources.Count == 0)
        {
            position = default;
            return false;
        }

        for (int attempt = 0; attempt < maxAttemptsPerLeaf; attempt++)
        {
            LeafDropSource source = SelectWeightedTreeSource();
            if (source == null) break;

            Vector2 candidate = source.GetRandomPosition();
            if (!IsInsideSafeBounds(candidate) || IsBlocked(candidate)) continue;

            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    private bool TryGetOrdinaryPosition(out Vector2 position)
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
            if (IsInsideAnyTreeInfluence(candidate) || IsBlocked(candidate)) continue;

            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    private LeafDropSource SelectWeightedTreeSource()
    {
        float totalWeight = 0f;
        for (int i = 0; i < treeSources.Count; i++)
        {
            LeafDropSource source = treeSources[i];
            if (source != null && source.isActiveAndEnabled) totalWeight += source.InfluenceAreaWeight;
        }

        if (totalWeight <= 0f) return null;

        float selection = Random.value * totalWeight;
        LeafDropSource fallback = null;
        for (int i = 0; i < treeSources.Count; i++)
        {
            LeafDropSource source = treeSources[i];
            if (source == null || !source.isActiveAndEnabled) continue;
            fallback = source;
            selection -= source.InfluenceAreaWeight;
            if (selection <= 0f) return source;
        }

        return fallback;
    }

    private bool IsInsideAnyTreeInfluence(Vector2 position)
    {
        for (int i = 0; i < treeSources.Count; i++)
        {
            LeafDropSource source = treeSources[i];
            if (source != null && source.isActiveAndEnabled && source.Contains(position)) return true;
        }
        return false;
    }

    private bool IsInsideSafeBounds(Vector2 position)
    {
        return position.x >= walkableBounds.xMin + clearance
            && position.x <= walkableBounds.xMax - clearance
            && position.y >= walkableBounds.yMin + clearance
            && position.y <= walkableBounds.yMax - clearance;
    }

    private bool IsBlocked(Vector2 position)
    {
        int count = Physics2D.OverlapCircle(position, clearance, blockerFilter, blockerHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = blockerHits[i];
            if (hit == null) continue;

            if (hit.name == "SpawnExclusion" && hit.GetComponentInParent<LeafDropSource>() != null)
            {
                continue;
            }

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
        pooledLeaves.Clear();
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
