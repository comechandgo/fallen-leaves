using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LevelPrefabLayoutSync
{
    private const string SimplePath = "Assets/Prefabs/Levels/Level_SimpleSmall.prefab";
    private const string TimedPath = "Assets/Prefabs/Levels/Level_TimedChallenge.prefab";
    private const string EndlessPath = "Assets/Prefabs/Levels/Level_Endless.prefab";
    private const string CatalogPath = "Assets/Resources/LevelCatalog.asset";
    private const string ReportFileName = "level-prefab-layout-sync-report.txt";

    [MenuItem("Tools/Fallen Leaves/Sync Other Levels From SimpleSmall")]
    public static void RunMenu()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Sync maps from SimpleSmall?",
            "This replaces the TimedChallenge and Endless map hierarchies with the current SimpleSmall prefab while preserving their mode rules and asset GUIDs.",
            "Sync maps",
            "Cancel");
        if (!confirmed) return;

        try
        {
            string reportPath = SyncAndValidate();
            EditorUtility.DisplayDialog("Level map sync completed", reportPath, "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Level map sync failed", exception.Message, "OK");
        }
    }

    public static void RunBatch()
    {
        string reportPath = SyncAndValidate();
        Debug.Log($"LEVEL_PREFAB_LAYOUT_SYNC_SUCCESS: {reportPath}");
    }

    private static string SyncAndValidate()
    {
        string simpleAbsolute = ToAbsolutePath(SimplePath);
        string timedAbsolute = ToAbsolutePath(TimedPath);
        string endlessAbsolute = ToAbsolutePath(EndlessPath);
        string catalogAbsolute = ToAbsolutePath(CatalogPath);
        EnsureRequiredFile(simpleAbsolute, SimplePath);
        EnsureRequiredFile(timedAbsolute, TimedPath);
        EnsureRequiredFile(endlessAbsolute, EndlessPath);
        EnsureRequiredFile(catalogAbsolute, CatalogPath);

        string simpleHashBefore = ComputeSha256(simpleAbsolute);
        string timedGuidBefore = AssetDatabase.AssetPathToGUID(TimedPath);
        string endlessGuidBefore = AssetDatabase.AssetPathToGUID(EndlessPath);
        if (string.IsNullOrEmpty(timedGuidBefore) || string.IsNullOrEmpty(endlessGuidBefore))
            throw new InvalidOperationException("TimedChallenge or Endless prefab has no asset GUID.");

        ModeRules timedRules = ModeRules.CreateTimed(LoadLevel(TimedPath));
        ModeRules endlessRules = ModeRules.CreateEndless(LoadLevel(EndlessPath));
        string sourceSignature = BuildLayoutSignature(SimplePath);

        Dictionary<string, byte[]> rollbackFiles = new Dictionary<string, byte[]>
        {
            [timedAbsolute] = File.ReadAllBytes(timedAbsolute),
            [endlessAbsolute] = File.ReadAllBytes(endlessAbsolute),
            [catalogAbsolute] = File.ReadAllBytes(catalogAbsolute)
        };

        List<string> report = new List<string>
        {
            $"Fallen Leaves level map sync - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Unity: {Application.unityVersion}",
            $"Source: {SimplePath}",
            $"Source SHA-256 before: {simpleHashBefore}",
            string.Empty
        };

        try
        {
            SaveSourceLayoutAsTarget(TimedPath, timedRules);
            SaveSourceLayoutAsTarget(EndlessPath, endlessRules);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            RebindCatalog();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ValidateGuid(TimedPath, timedGuidBefore);
            ValidateGuid(EndlessPath, endlessGuidBefore);
            ValidateSourceUnchanged(simpleAbsolute, simpleHashBefore);
            ValidateModeRules(LoadLevel(TimedPath), timedRules);
            ValidateModeRules(LoadLevel(EndlessPath), endlessRules);
            ValidateRuntimeReferences(LoadLevel(SimplePath), SimplePath);
            ValidateRuntimeReferences(LoadLevel(TimedPath), TimedPath);
            ValidateRuntimeReferences(LoadLevel(EndlessPath), EndlessPath);

            string timedSignature = BuildLayoutSignature(TimedPath);
            string endlessSignature = BuildLayoutSignature(EndlessPath);
            if (!string.Equals(sourceSignature, timedSignature, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "TimedChallenge map hierarchy signature differs from SimpleSmall. "
                    + DescribeFirstDifference(sourceSignature, timedSignature));
            if (!string.Equals(sourceSignature, endlessSignature, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Endless map hierarchy signature differs from SimpleSmall. "
                    + DescribeFirstDifference(sourceSignature, endlessSignature));

            ValidateCatalog();
            report.Add($"TimedChallenge GUID preserved: {timedGuidBefore}");
            report.Add($"Endless GUID preserved: {endlessGuidBefore}");
            report.Add($"Layout signature SHA-256: {ComputeTextSha256(sourceSignature)}");
            report.Add("TimedChallenge rules: LevelId=2, leaves=1920, time=180, endless=false");
            report.Add("Endless rules: LevelId=3, leaves=2080, batch=32, interval=1.8, max=4160");
            report.Add("LevelCatalog rebound to all three current LevelRoot components.");
            report.Add($"Source SHA-256 after: {ComputeSha256(simpleAbsolute)}");
            report.Add(string.Empty);
            report.Add("RESULT: SUCCESS");
        }
        catch
        {
            foreach (KeyValuePair<string, byte[]> entry in rollbackFiles)
                File.WriteAllBytes(entry.Key, entry.Value);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            throw;
        }

        string reportPath = Path.Combine(
            Directory.GetParent(Application.dataPath).Parent.FullName,
            "logs",
            ReportFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllLines(reportPath, report, new UTF8Encoding(false));
        return reportPath;
    }

    private static void SaveSourceLayoutAsTarget(string targetPath, ModeRules rules)
    {
        GameObject sourceContents = PrefabUtility.LoadPrefabContents(SimplePath);
        try
        {
            sourceContents.name = rules.RootName;
            LevelRoot level = sourceContents.GetComponent<LevelRoot>();
            if (level == null) throw new InvalidOperationException($"{SimplePath} has no LevelRoot component.");

            ApplyModeRules(level, rules);
            RebindRuntimeReferences(level);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(sourceContents, targetPath, out bool success);
            if (!success || saved == null) throw new IOException($"Failed to save synchronized prefab: {targetPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceContents);
        }
    }

    private static void ApplyModeRules(LevelRoot level, ModeRules rules)
    {
        SerializedObject serialized = new SerializedObject(level);
        SetInt(serialized, "levelId", (int)rules.Id);
        SetInt(serialized, "initialLeafCount", rules.InitialLeafCount);
        SetFloat(serialized, "timeLimitSeconds", rules.TimeLimitSeconds);
        SetBool(serialized, "endless", rules.Endless);
        SetInt(serialized, "endlessSpawnBatch", rules.EndlessSpawnBatch);
        SetFloat(serialized, "endlessSpawnInterval", rules.EndlessSpawnInterval);
        SetInt(serialized, "endlessMaxLeaves", rules.EndlessMaxLeaves);
        SetFloat(serialized, "endlessSurvivalMaximum", rules.SurvivalMaximum);
        SetFloat(serialized, "endlessSurvivalInitial", rules.SurvivalInitial);
        SetFloat(serialized, "endlessSurvivalPerLeaf", rules.SurvivalPerLeaf);
        SetFloat(serialized, "endlessSurvivalBaseDrain", rules.SurvivalBaseDrain);
        SetFloat(serialized, "endlessSurvivalStageSeconds", rules.SurvivalStageSeconds);
        SetFloat(serialized, "endlessSurvivalStageMultiplier", rules.SurvivalStageMultiplier);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RebindRuntimeReferences(LevelRoot level)
    {
        GroundTilemapGenerator ground = level.GetComponentInChildren<GroundTilemapGenerator>(true);
        LeafSpawner spawner = level.GetComponentInChildren<LeafSpawner>(true);
        WindBlower blower = level.GetComponentInChildren<WindBlower>(true);
        if (ground == null || spawner == null || blower == null)
            throw new InvalidOperationException($"{level.name} is missing a runtime map component.");

        SerializedObject serialized = new SerializedObject(level);
        SetObject(serialized, "groundGenerator", ground);
        SetObject(serialized, "leafSpawner", spawner);
        SetObject(serialized, "windBlower", blower);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RebindCatalog()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidOperationException($"Missing LevelCatalog: {CatalogPath}");

        LevelCatalog.Entry[] entries =
        {
            new LevelCatalog.Entry { Id = LevelId.SimpleSmall, Prefab = LoadLevel(SimplePath) },
            new LevelCatalog.Entry { Id = LevelId.TimedChallenge, Prefab = LoadLevel(TimedPath) },
            new LevelCatalog.Entry { Id = LevelId.Endless, Prefab = LoadLevel(EndlessPath) }
        };
        catalog.Configure(entries);
        EditorUtility.SetDirty(catalog);
    }

    private static void ValidateCatalog()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null
            || catalog.GetPrefab(LevelId.SimpleSmall) != LoadLevel(SimplePath)
            || catalog.GetPrefab(LevelId.TimedChallenge) != LoadLevel(TimedPath)
            || catalog.GetPrefab(LevelId.Endless) != LoadLevel(EndlessPath))
            throw new InvalidOperationException("LevelCatalog does not reference all synchronized level prefabs.");
    }

    private static void ValidateModeRules(LevelRoot level, ModeRules expected)
    {
        if (level == null
            || level.name != expected.RootName
            || level.Id != expected.Id
            || level.InitialLeafCount != expected.InitialLeafCount
            || !Mathf.Approximately(level.TimeLimitSeconds, expected.TimeLimitSeconds)
            || level.Endless != expected.Endless
            || level.EndlessSpawnBatch != expected.EndlessSpawnBatch
            || !Mathf.Approximately(level.EndlessSpawnInterval, expected.EndlessSpawnInterval)
            || level.EndlessMaxLeaves != expected.EndlessMaxLeaves
            || !Mathf.Approximately(level.EndlessSurvivalMaximum, expected.SurvivalMaximum)
            || !Mathf.Approximately(level.EndlessSurvivalInitial, expected.SurvivalInitial)
            || !Mathf.Approximately(level.EndlessSurvivalPerLeaf, expected.SurvivalPerLeaf)
            || !Mathf.Approximately(level.EndlessSurvivalBaseDrain, expected.SurvivalBaseDrain)
            || !Mathf.Approximately(level.EndlessSurvivalStageSeconds, expected.SurvivalStageSeconds)
            || !Mathf.Approximately(level.EndlessSurvivalStageMultiplier, expected.SurvivalStageMultiplier))
            throw new InvalidOperationException($"{expected.RootName} mode rules were not preserved.");
    }

    private static void ValidateRuntimeReferences(LevelRoot level, string path)
    {
        if (level == null) throw new InvalidOperationException($"Missing LevelRoot: {path}");
        SerializedObject serialized = new SerializedObject(level);
        string[] properties = { "groundGenerator", "leafSpawner", "windBlower" };
        for (int i = 0; i < properties.Length; i++)
        {
            SerializedProperty property = serialized.FindProperty(properties[i]);
            Component component = property != null ? property.objectReferenceValue as Component : null;
            if (component == null || !component.transform.IsChildOf(level.transform))
                throw new InvalidOperationException($"{path} has an invalid internal reference: {properties[i]}");
        }
    }

    private static string BuildLayoutSignature(string prefabPath)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            StringBuilder signature = new StringBuilder(256 * 1024);
            AppendGameObject(contents.transform, contents.transform, true, signature);
            return signature.ToString();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void AppendGameObject(Transform transform, Transform root, bool isRoot, StringBuilder output)
    {
        GameObject gameObject = transform.gameObject;
        output.Append("GO|").Append(GetHierarchyKey(transform, root)).Append('|');
        if (!isRoot) output.Append(gameObject.name);
        output.Append('|').Append(gameObject.activeSelf ? '1' : '0')
            .Append('|').Append(gameObject.layer)
            .Append('|').Append(gameObject.tag)
            .Append('|').Append(Vector3Text(transform.localPosition))
            .Append('|').Append(QuaternionText(transform.localRotation))
            .Append('|').Append(Vector3Text(transform.localScale)).AppendLine();
        if (!isRoot && PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
        {
            UnityEngine.Object nestedSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            output.Append("NESTED|").Append(AssetDatabase.GetAssetPath(nestedSource)).AppendLine();
        }

        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null) throw new MissingReferenceException($"Missing component on {gameObject.name}");
            if (component is Transform) continue;
            if (isRoot && component is LevelRoot) continue;

            output.Append("COMP|").Append(i).Append('|').Append(component.GetType().AssemblyQualifiedName).AppendLine();
            AppendSerializedComponent(component, root, output);
        }

        for (int i = 0; i < transform.childCount; i++)
            AppendGameObject(transform.GetChild(i), root, false, output);
    }

    private static void AppendSerializedComponent(Component component, Transform root, StringBuilder output)
    {
        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property = serialized.GetIterator();
        bool enterChildren = true;
        while (property.Next(enterChildren))
        {
            enterChildren = true;
            if (property.propertyPath == "m_GameObject" || property.propertyPath == "m_Script")
            {
                enterChildren = false;
                continue;
            }
            if (property.propertyPath.EndsWith(".m_FileID", StringComparison.Ordinal)
                || property.propertyPath.EndsWith(".m_PathID", StringComparison.Ordinal))
                continue;
            output.Append("PROP|").Append(property.propertyPath).Append('|').Append(property.propertyType).Append('|');
            AppendPropertyValue(property, root, output);
            output.AppendLine();
        }
    }

    private static void AppendPropertyValue(SerializedProperty property, Transform root, StringBuilder output)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.Character:
            case SerializedPropertyType.LayerMask:
                output.Append(property.longValue);
                break;
            case SerializedPropertyType.Boolean:
                output.Append(property.boolValue ? '1' : '0');
                break;
            case SerializedPropertyType.Float:
                output.Append(property.doubleValue.ToString("R", CultureInfo.InvariantCulture));
                break;
            case SerializedPropertyType.String:
                output.Append(property.stringValue);
                break;
            case SerializedPropertyType.Color:
                output.Append(property.colorValue);
                break;
            case SerializedPropertyType.ObjectReference:
            case SerializedPropertyType.ExposedReference:
                output.Append(ObjectReferenceText(property.objectReferenceValue, root));
                break;
            case SerializedPropertyType.Enum:
                output.Append(property.enumValueIndex);
                break;
            case SerializedPropertyType.Vector2:
                output.Append(Vector2Text(property.vector2Value));
                break;
            case SerializedPropertyType.Vector3:
                output.Append(Vector3Text(property.vector3Value));
                break;
            case SerializedPropertyType.Vector4:
                output.Append(property.vector4Value);
                break;
            case SerializedPropertyType.Rect:
                output.Append(property.rectValue);
                break;
            case SerializedPropertyType.Bounds:
                output.Append(property.boundsValue);
                break;
            case SerializedPropertyType.Quaternion:
                output.Append(QuaternionText(property.quaternionValue));
                break;
            case SerializedPropertyType.Vector2Int:
                output.Append(property.vector2IntValue);
                break;
            case SerializedPropertyType.Vector3Int:
                output.Append(property.vector3IntValue);
                break;
            case SerializedPropertyType.RectInt:
                output.Append(property.rectIntValue);
                break;
            case SerializedPropertyType.BoundsInt:
                output.Append(property.boundsIntValue);
                break;
            case SerializedPropertyType.Hash128:
                output.Append(property.hash128Value);
                break;
        }
    }

    private static string ObjectReferenceText(UnityEngine.Object value, Transform root)
    {
        if (value == null) return "null";
        if (value is GameObject gameObject && (gameObject.transform == root || gameObject.transform.IsChildOf(root)))
            return "internal:" + GetHierarchyKey(gameObject.transform, root) + ":GameObject";
        if (value is Component component && (component.transform == root || component.transform.IsChildOf(root)))
            return "internal:" + GetHierarchyKey(component.transform, root) + ':' + component.GetType().AssemblyQualifiedName;

        string assetPath = AssetDatabase.GetAssetPath(value);
        if (!string.IsNullOrEmpty(assetPath))
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId);
            return $"asset:{guid}:{localId}:{value.GetType().AssemblyQualifiedName}";
        }

        return $"object:{value.GetType().AssemblyQualifiedName}:{value.name}";
    }

    private static string GetHierarchyKey(Transform transform, Transform root)
    {
        if (transform == root) return "$";
        Stack<string> segments = new Stack<string>();
        Transform current = transform;
        while (current != null && current != root)
        {
            segments.Push(current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture) + ":" + current.name);
            current = current.parent;
        }
        return string.Join("/", segments.ToArray());
    }

    private static void ValidateGuid(string assetPath, string expected)
    {
        string actual = AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"Asset GUID changed for {assetPath}: {expected} -> {actual}");
    }

    private static void ValidateSourceUnchanged(string sourceAbsolute, string expectedHash)
    {
        string actualHash = ComputeSha256(sourceAbsolute);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Level_SimpleSmall.prefab was modified during synchronization.");
    }

    private static LevelRoot LoadLevel(string path)
    {
        LevelRoot level = AssetDatabase.LoadAssetAtPath<LevelRoot>(path);
        if (level == null) throw new InvalidOperationException($"Missing LevelRoot prefab: {path}");
        return level;
    }

    private static void SetInt(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = RequiredProperty(serialized, name);
        property.intValue = value;
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = RequiredProperty(serialized, name);
        property.floatValue = value;
    }

    private static void SetBool(SerializedObject serialized, string name, bool value)
    {
        SerializedProperty property = RequiredProperty(serialized, name);
        property.boolValue = value;
    }

    private static void SetObject(SerializedObject serialized, string name, UnityEngine.Object value)
    {
        SerializedProperty property = RequiredProperty(serialized, name);
        property.objectReferenceValue = value;
    }

    private static SerializedProperty RequiredProperty(SerializedObject serialized, string name)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new MissingFieldException(serialized.targetObject.GetType().Name, name);
        return property;
    }

    private static string Vector2Text(Vector2 value)
    {
        return value.x.ToString("R", CultureInfo.InvariantCulture) + ','
            + value.y.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string Vector3Text(Vector3 value)
    {
        return value.x.ToString("R", CultureInfo.InvariantCulture) + ','
            + value.y.ToString("R", CultureInfo.InvariantCulture) + ','
            + value.z.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string QuaternionText(Quaternion value)
    {
        return value.x.ToString("R", CultureInfo.InvariantCulture) + ','
            + value.y.ToString("R", CultureInfo.InvariantCulture) + ','
            + value.z.ToString("R", CultureInfo.InvariantCulture) + ','
            + value.w.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string ComputeSha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string ComputeTextSha256(string value)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string DescribeFirstDifference(string expected, string actual)
    {
        string[] expectedLines = expected.Replace("\r", string.Empty).Split('\n');
        string[] actualLines = actual.Replace("\r", string.Empty).Split('\n');
        int count = Mathf.Min(expectedLines.Length, actualLines.Length);
        for (int i = 0; i < count; i++)
        {
            if (string.Equals(expectedLines[i], actualLines[i], StringComparison.Ordinal)) continue;
            return $"First difference at signature line {i + 1}. Expected [{expectedLines[i]}], actual [{actualLines[i]}].";
        }

        return $"Signature line counts differ: expected {expectedLines.Length}, actual {actualLines.Length}.";
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static void EnsureRequiredFile(string absolutePath, string assetPath)
    {
        if (!File.Exists(absolutePath)) throw new FileNotFoundException("Missing required asset", assetPath);
    }

    private sealed class ModeRules
    {
        public string RootName;
        public LevelId Id;
        public int InitialLeafCount;
        public float TimeLimitSeconds;
        public bool Endless;
        public int EndlessSpawnBatch;
        public float EndlessSpawnInterval;
        public int EndlessMaxLeaves;
        public float SurvivalMaximum;
        public float SurvivalInitial;
        public float SurvivalPerLeaf;
        public float SurvivalBaseDrain;
        public float SurvivalStageSeconds;
        public float SurvivalStageMultiplier;

        public static ModeRules CreateTimed(LevelRoot existing)
        {
            return FromExisting(existing, "Level_TimedChallenge", LevelId.TimedChallenge, 1920, 180f, false, 0, 1.8f, 260);
        }

        public static ModeRules CreateEndless(LevelRoot existing)
        {
            return FromExisting(existing, "Level_Endless", LevelId.Endless, 2080, 0f, true, 32, 1.8f, 4160);
        }

        private static ModeRules FromExisting(
            LevelRoot existing,
            string rootName,
            LevelId id,
            int leaves,
            float timeLimit,
            bool endless,
            int batch,
            float interval,
            int maxLeaves)
        {
            return new ModeRules
            {
                RootName = rootName,
                Id = id,
                InitialLeafCount = leaves,
                TimeLimitSeconds = timeLimit,
                Endless = endless,
                EndlessSpawnBatch = batch,
                EndlessSpawnInterval = interval,
                EndlessMaxLeaves = maxLeaves,
                SurvivalMaximum = existing.EndlessSurvivalMaximum,
                SurvivalInitial = existing.EndlessSurvivalInitial,
                SurvivalPerLeaf = existing.EndlessSurvivalPerLeaf,
                SurvivalBaseDrain = existing.EndlessSurvivalBaseDrain,
                SurvivalStageSeconds = existing.EndlessSurvivalStageSeconds,
                SurvivalStageMultiplier = existing.EndlessSurvivalStageMultiplier
            };
        }
    }
}
