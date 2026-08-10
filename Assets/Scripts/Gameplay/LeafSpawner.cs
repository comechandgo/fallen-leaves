using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeafSpawner : MonoBehaviour
{
    [SerializeField] private LeafLifecycle leafPrefab;
    [SerializeField] private LeafSpawnArea[] spawnAreas = new LeafSpawnArea[0];
    [SerializeField] private Transform leafContainer;

    private readonly HashSet<LeafLifecycle> activeLeaves = new HashSet<LeafLifecycle>();
    private LevelRoot owner;

    public int ActiveCount => activeLeaves.Count;

    public void Configure(LeafLifecycle prefab, LeafSpawnArea[] areas, Transform container)
    {
        leafPrefab = prefab;
        spawnAreas = areas ?? new LeafSpawnArea[0];
        leafContainer = container;
    }

    public void Initialize(LevelRoot level)
    {
        owner = level;
        activeLeaves.Clear();

        if (spawnAreas == null || spawnAreas.Length == 0)
        {
            spawnAreas = GetComponentsInChildren<LeafSpawnArea>(true);
        }

        if (leafContainer == null)
        {
            Transform existing = transform.Find("Leaves");
            if (existing != null)
            {
                leafContainer = existing;
            }
            else
            {
                GameObject created = new GameObject("Leaves");
                created.transform.SetParent(transform, false);
                leafContainer = created.transform;
            }
        }
    }

    public int Spawn(int requestedCount)
    {
        if (requestedCount <= 0) return 0;
        if (leafPrefab == null || spawnAreas == null || spawnAreas.Length == 0)
        {
            Debug.LogError($"LeafSpawner on {name} is missing its prefab or spawn areas.", this);
            return 0;
        }

        Physics2D.SyncTransforms();
        int spawned = 0;

        for (int i = 0; i < requestedCount; i++)
        {
            if (!TryFindSpawnPosition(out Vector2 position)) continue;

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
                $"{name} spawned {spawned}/{requestedCount} leaves because no valid positions remained.",
                this);
        }

        return spawned;
    }

    internal void Register(LeafLifecycle leaf)
    {
        if (leaf != null) activeLeaves.Add(leaf);
    }

    internal void Unregister(LeafLifecycle leaf)
    {
        if (leaf != null) activeLeaves.Remove(leaf);
    }

    private bool TryFindSpawnPosition(out Vector2 position)
    {
        int start = Random.Range(0, spawnAreas.Length);
        for (int i = 0; i < spawnAreas.Length; i++)
        {
            LeafSpawnArea area = spawnAreas[(start + i) % spawnAreas.Length];
            if (area != null && area.TryGetRandomPosition(out position)) return true;
        }

        position = default;
        return false;
    }

    private void OnDisable()
    {
        activeLeaves.Clear();
        owner = null;
    }
}
