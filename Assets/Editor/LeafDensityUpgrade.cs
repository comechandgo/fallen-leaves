using UnityEditor;
using UnityEngine;

public static class LeafDensityUpgrade
{
    private const string TreePrefabRoot = "Assets/Prefabs/Gameplay/Props";
    private const string LevelPrefabRoot = "Assets/Prefabs/Levels";

    [MenuItem("Tools/Fallen Leaves/Apply High Density Leaf Upgrade")]
    public static void RunMenu()
    {
        Apply();
        EditorUtility.DisplayDialog(
            "High Density Leaves",
            "Updated the nine tree prefabs and the three level rule sets without rebuilding the map layout.",
            "OK");
    }

    public static void RunBatch()
    {
        Apply();
    }

    private static void Apply()
    {
        for (int i = 1; i <= 9; i++) UpgradeTree($"{TreePrefabRoot}/Tree_{i:00}.prefab");

        UpgradeLevel($"{LevelPrefabRoot}/Level_SimpleSmall.prefab", 2560, 0, 1.8f, 260);
        UpgradeLevel($"{LevelPrefabRoot}/Level_TimedChallenge.prefab", 1920, 0, 1.8f, 260);
        UpgradeLevel($"{LevelPrefabRoot}/Level_Endless.prefab", 2080, 32, 1.8f, 4160);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("LEAF_DENSITY_UPGRADE_SUCCESS");
    }

    private static void UpgradeTree(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
                throw new System.InvalidOperationException($"{path} has no configured SpriteRenderer.");

            LeafDropSource source = root.GetComponent<LeafDropSource>();
            if (source == null) source = root.AddComponent<LeafDropSource>();
            source.Configure(renderer);

            Transform trunkTransform = root.transform.Find("TreeTrunkCollider");
            GameObject trunkObject;
            if (trunkTransform == null)
            {
                trunkObject = new GameObject("TreeTrunkCollider");
                trunkObject.transform.SetParent(root.transform, false);
            }
            else
            {
                trunkObject = trunkTransform.gameObject;
            }

            trunkObject.layer = LayerMask.NameToLayer("Obstacle");
            CapsuleCollider2D trunk = trunkObject.GetComponent<CapsuleCollider2D>();
            if (trunk == null) trunk = trunkObject.AddComponent<CapsuleCollider2D>();
            Bounds bounds = renderer.sprite.bounds;
            trunk.isTrigger = true;
            trunk.direction = CapsuleDirection2D.Vertical;
            trunk.size = new Vector2(bounds.size.x * 0.28f, bounds.size.y * 0.24f);
            trunk.offset = new Vector2(bounds.center.x, bounds.min.y + bounds.size.y * 0.22f);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeLevel(
        string path,
        int initialLeafCount,
        int endlessSpawnBatch,
        float endlessSpawnInterval,
        int endlessMaxLeaves)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            LevelRoot level = root.GetComponent<LevelRoot>();
            if (level == null) throw new System.InvalidOperationException($"{path} has no LevelRoot.");

            SerializedObject serializedLevel = new SerializedObject(level);
            serializedLevel.FindProperty("initialLeafCount").intValue = initialLeafCount;
            serializedLevel.FindProperty("endlessSpawnBatch").intValue = endlessSpawnBatch;
            serializedLevel.FindProperty("endlessSpawnInterval").floatValue = endlessSpawnInterval;
            serializedLevel.FindProperty("endlessMaxLeaves").intValue = endlessMaxLeaves;
            serializedLevel.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
