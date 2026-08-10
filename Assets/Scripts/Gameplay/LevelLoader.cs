using UnityEngine;

public static class LevelLoader
{
    private const string CatalogResourcePath = "LevelCatalog";

    private static LevelCatalog catalog;
    private static LevelRoot current;

    public static LevelRoot Current => current;
    public static WindBlower CurrentWindBlower => current != null ? current.WindBlower : null;
    public static float CurrentTimeLimitSeconds => current != null ? current.TimeLimitSeconds : 0f;

    public static LevelRoot Load(LevelId id)
    {
        Unload();

        if (catalog == null)
        {
            catalog = Resources.Load<LevelCatalog>(CatalogResourcePath);
        }

        if (catalog == null)
        {
            Debug.LogError($"Missing Resources/{CatalogResourcePath}.asset. Run the level prefab migration tool.");
            return null;
        }

        if (!catalog.TryGetPrefab(id, out LevelRoot prefab) || prefab == null)
        {
            Debug.LogError($"No level prefab is registered for {id}.");
            return null;
        }

        current = Object.Instantiate(prefab);
        current.name = prefab.name;
        current.InitializeRuntime();
        return current;
    }

    public static void Unload()
    {
        if (current == null) return;

        GameObject oldRoot = current.gameObject;
        current = null;
        oldRoot.SetActive(false);
        Object.Destroy(oldRoot);
    }

    public static void Tick(float deltaTime)
    {
        if (current != null) current.Tick(deltaTime);
    }

    public static bool IsGameplayClear()
    {
        return current != null && current.IsGameplayClear;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        catalog = null;
        current = null;
    }
}
