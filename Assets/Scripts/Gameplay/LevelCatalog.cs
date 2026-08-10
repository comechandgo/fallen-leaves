using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Fallen Leaves/Level Catalog")]
public sealed class LevelCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public LevelId Id;
        public LevelRoot Prefab;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    public LevelRoot GetPrefab(LevelId id)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Id == id) return entries[i].Prefab;
        }

        return null;
    }

    public bool TryGetPrefab(LevelId id, out LevelRoot prefab)
    {
        prefab = GetPrefab(id);
        return prefab != null;
    }

    public void Configure(Entry[] configuredEntries)
    {
        entries = configuredEntries ?? Array.Empty<Entry>();
    }
}
