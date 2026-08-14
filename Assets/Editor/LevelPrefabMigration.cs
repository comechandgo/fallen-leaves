using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class LevelPrefabMigration
{
    private const string SourceArtRoot = "Assets/StreamingAssets/WindArt/";
    private const string GameplayArtRoot = "Assets/Art/Gameplay/";
    private const string TileRoot = "Assets/Art/Gameplay/Tiles";
    private const string MaterialRoot = "Assets/Art/Gameplay/Materials";
    private const string PrefabRoot = "Assets/Prefabs/Gameplay";
    private const string LevelPrefabRoot = "Assets/Prefabs/Levels";
    private const string CatalogPath = "Assets/Resources/LevelCatalog.asset";
    private const string RiverMaterialPath = MaterialRoot + "/RiverSoftBlend.mat";
    private const string RiverShaderPath = MaterialRoot + "/RiverSoftBlend.shader";
    private const string TreeMaterialPath = MaterialRoot + "/TreeCursorFade.mat";
    private const string TreeShaderPath = MaterialRoot + "/TreeCursorFade.shader";
    private const string WholeRiverRelativePath = "ggj地图补充/整河.png";
    private const int GroundSeed = 12090;
    private const float GroundPatchWorldSize = 26f;

    private const int LeafLayer = 8;
    private const int ObstacleLayer = 9;
    private const int RiverLayer = 10;

    [MenuItem("Tools/Fallen Leaves/Import Map Prototype")]
    public static void RunMenu()
    {
        RunWithDialog(false);
    }

    [MenuItem("Tools/Fallen Leaves/Force Rebuild Three Levels From TMJ")]
    public static void RunForceMenu()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Force rebuild all three levels?",
            "This replaces every authored level layout with map_120x90_v1.tmj and discards manual prefab layout edits.",
            "Rebuild three levels",
            "Cancel");
        if (!confirmed) return;
        RunWithDialog(true);
    }

    // Kept as the stable batch entry used by the previous migration command.
    public static void RunBatch()
    {
        RunMigration(false);
    }

    public static void RunPrototypeBatch()
    {
        RunMigration(false);
    }

    public static void RunForcePrototypeBatch()
    {
        RunMigration(true);
    }

    private static void RunWithDialog(bool force)
    {
        try
        {
            MigrationResult result = RunMigration(force);
            EditorUtility.DisplayDialog(
                "Fallen Leaves Tiled Import",
                $"Import completed.\nGenerated levels: {result.GeneratedLevels}\nSkipped levels: {result.SkippedLevels}\nReport: {result.ReportPath}",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Fallen Leaves Tiled Import", exception.Message, "OK");
        }
    }

    private static MigrationResult RunMigration(bool forceLevelRebuild)
    {
        List<string> report = new List<string>
        {
            $"Fallen Leaves Tiled map import - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Unity: {Application.unityVersion}",
            $"Source: {TiledMapPrototypeImporter.SourceAssetPath}",
            $"Force level rebuild: {forceLevelRebuild}",
            string.Empty
        };

        MigrationResult result = new MigrationResult();
        try
        {
            // Parse and validate before writing any generated asset.
            TiledMapPrototypeImporter.Layout layout = TiledMapPrototypeImporter.LoadAndValidate();
            report.Add($"Source SHA-256: {layout.SourceSha256}");
            report.Add($"Bounds: {layout.Bounds}");
            report.Add($"River points: {layout.RiverPoints.Length}; width: {layout.RiverWidth:0.##}m");
            report.Add($"Objects: {layout.Obstacles.Length} obstacles, {layout.Decorations.Length} decorations, {layout.Landmarks.Length} landmarks, 1 lake");

            EnsureFolders();
            List<string> gameplayArtFiles = BuildGameplayArtFileList();
            CopyAndImportGameplayArt(gameplayArtFiles, report);

            Tile greenTile = CreateOrUpdateTile(
                $"{TileRoot}/GroundGreen.asset",
                LoadGameplaySprite("ggj/通用/草地绿.png"));
            Tile yellowTile = CreateOrUpdateTile(
                $"{TileRoot}/GroundYellow.asset",
                LoadGameplaySprite("ggj/通用/草地黄.png"));
            CreateOrUpdateRiverMaterial();
            Material treeMaterial = CreateOrUpdateTreeCursorFadeMaterial();

            Dictionary<string, GameObject> prefabs = CreateBasePrefabs(treeMaterial, report);
            ValidatePrefabKeys(layout, prefabs);

            ModeSpec[] modes = CreateModeSpecs();
            Dictionary<LevelId, LevelRoot> levels = new Dictionary<LevelId, LevelRoot>();
            for (int i = 0; i < modes.Length; i++)
            {
                bool skipped;
                LevelRoot level = CreateLevelPrefab(
                    modes[i],
                    layout,
                    prefabs,
                    greenTile,
                    yellowTile,
                    forceLevelRebuild,
                    out skipped);
                levels[modes[i].Id] = level;
                if (skipped) result.SkippedLevels++;
                else result.GeneratedLevels++;
            }

            LevelCatalog catalog = CreateOrUpdateCatalog(levels);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ValidateMigration(catalog, layout, gameplayArtFiles, report);
            report.Add(string.Empty);
            report.Add($"Generated levels: {result.GeneratedLevels}");
            report.Add($"Skipped existing levels: {result.SkippedLevels}");
            report.Add("RESULT: SUCCESS");
            result.Success = true;
        }
        catch (Exception exception)
        {
            report.Add(string.Empty);
            report.Add("RESULT: FAILED");
            report.Add(exception.ToString());
            result.Success = false;
            Debug.LogException(exception);
        }

        result.ReportPath = WriteReport(report);
        if (!result.Success) throw new InvalidOperationException($"Tiled map import failed. See {result.ReportPath}");
        Debug.Log($"Tiled map import completed. Report: {result.ReportPath}");
        return result;
    }

    private static List<string> BuildGameplayArtFileList()
    {
        List<string> files = new List<string>
        {
            "ggj/通用/草地绿.png",
            "ggj/通用/草地黄.png"
        };
        for (int i = 1; i <= 4; i++) files.Add($"ggj/通用/叶子{i}.png");
        for (int i = 1; i <= 10; i++) files.Add($"ggj/通用/石头{i}.png");
        for (int i = 1; i <= 3; i++) files.Add($"ggj/通用/长条石头{i}.png");
        for (int i = 1; i <= 2; i++) files.Add($"ggj/通用/芦苇{i}.png");
        for (int i = 1; i <= 9; i++) files.Add($"ggj地图补充/树{i}.png");
        files.Add(WholeRiverRelativePath);
        for (int i = 1; i <= 3; i++) files.Add($"ggj地图补充/湖{i}.png");
        return files;
    }

    private static void EnsureFolders()
    {
        EnsureAssetFolder(GameplayArtRoot.TrimEnd('/'));
        EnsureAssetFolder(TileRoot);
        EnsureAssetFolder(MaterialRoot);
        EnsureAssetFolder($"{PrefabRoot}/Leaves");
        EnsureAssetFolder($"{PrefabRoot}/Props");
        EnsureAssetFolder($"{PrefabRoot}/Water");
        EnsureAssetFolder($"{PrefabRoot}/Systems");
        EnsureAssetFolder(LevelPrefabRoot);
        EnsureAssetFolder("Assets/Resources");
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized)) return;

        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void CopyAndImportGameplayArt(List<string> files, List<string> report)
    {
        int copied = 0;
        int reused = 0;
        for (int i = 0; i < files.Count; i++)
        {
            string relative = files[i];
            string sourceAssetPath = SourceArtRoot + relative;
            string destinationAssetPath = GameplayArtRoot + relative;
            EnsureAssetFolder(Path.GetDirectoryName(destinationAssetPath).Replace('\\', '/'));

            string sourceAbsolute = ToAbsolutePath(sourceAssetPath);
            string destinationAbsolute = ToAbsolutePath(destinationAssetPath);
            if (!File.Exists(sourceAbsolute)) throw new FileNotFoundException("Missing source gameplay art", sourceAssetPath);
            if (relative == WholeRiverRelativePath)
            {
                File.Copy(sourceAbsolute, destinationAbsolute, true);
                copied++;
            }
            else if (!File.Exists(destinationAbsolute))
            {
                File.Copy(sourceAbsolute, destinationAbsolute, false);
                copied++;
            }
            else
            {
                reused++;
            }
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        for (int i = 0; i < files.Count; i++)
        {
            bool wholeRiver = files[i] == WholeRiverRelativePath;
            ConfigureSpriteImporter(GameplayArtRoot + files[i], wholeRiver, wholeRiver);
        }
        report.Add($"Gameplay sprites: {copied} copied, {reused} reused, {files.Count} configured.");
    }

    private static void ConfigureSpriteImporter(string assetPath, bool riverMaskTexture, bool fullResolution)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException($"Missing TextureImporter for {assetPath}");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = riverMaskTexture;
        importer.textureCompression = riverMaskTexture ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
        if (fullResolution) importer.maxTextureSize = 8192;
        importer.SaveAndReimport();
    }

    private static Tile CreateOrUpdateTile(string path, Sprite sprite)
    {
        if (sprite == null) throw new InvalidOperationException($"Missing ground Sprite for {path}");
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.sprite = sprite;
        tile.color = Color.white;
        tile.transform = Matrix4x4.identity;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static Material CreateOrUpdateRiverMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(RiverShaderPath);
        if (shader == null) shader = Shader.Find("FallenLeaves/RiverSoftBlend");
        if (shader == null) throw new InvalidOperationException($"Missing river blend shader: {RiverShaderPath}");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(RiverMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, RiverMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetFloat("_EndFade", 0.035f);
        material.SetFloat("_SideFade", 0.06f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateTreeCursorFadeMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(TreeShaderPath);
        if (shader == null) shader = Shader.Find("FallenLeaves/TreeCursorFade");
        if (shader == null) throw new InvalidOperationException($"Missing tree cursor fade shader: {TreeShaderPath}");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TreeMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, TreeMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetFloat("_InnerRadius", 35f);
        material.SetFloat("_OuterRadius", 60f);
        material.SetFloat("_MinimumOpacity", 0.25f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Dictionary<string, GameObject> CreateBasePrefabs(Material treeMaterial, List<string> report)
    {
        Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>
        {
            ["Leaf"] = CreateLeafPrefab(),
            ["BoundaryWall"] = CreateBoundaryWallPrefab(),
            ["WindBlower"] = CreateWindBlowerPrefab()
        };

        for (int i = 1; i <= 10; i++)
        {
            string key = $"Stone_{i:00}";
            prefabs[key] = CreateObstaclePrefab(key, $"ggj/通用/石头{i}.png");
        }
        for (int i = 1; i <= 3; i++)
        {
            string key = $"LongStone_{i:00}";
            prefabs[key] = CreateObstaclePrefab(key, $"ggj/通用/长条石头{i}.png");
        }
        for (int i = 1; i <= 2; i++)
        {
            string key = $"Reed_{i:00}";
            prefabs[key] = CreateDecorationPrefab(key, $"ggj/通用/芦苇{i}.png");
        }
        for (int i = 1; i <= 9; i++)
        {
            string key = $"Tree_{i:00}";
            prefabs[key] = CreateTreePrefab(key, $"ggj地图补充/树{i}.png", treeMaterial);
        }
        for (int i = 1; i <= 3; i++)
        {
            string pondKey = $"Pond_{i:00}";
            prefabs[pondKey] = CreatePondPrefab(pondKey, $"ggj地图补充/湖{i}.png");
        }
        prefabs["MainRiverWhole"] = CreateWholeRiverPrefab();

        report.Add($"Base prefabs created or updated: {prefabs.Count}");
        return prefabs;
    }

    private static GameObject CreateLeafPrefab()
    {
        GameObject root = new GameObject("Leaf") { layer = LeafLayer };
        GameObject windDeform = new GameObject("WindDeform") { layer = LeafLayer };
        windDeform.transform.SetParent(root.transform, false);
        GameObject spriteVisual = new GameObject("SpriteVisual") { layer = LeafLayer };
        spriteVisual.transform.SetParent(windDeform.transform, false);

        Sprite[] sprites =
        {
            LoadGameplaySprite("ggj/通用/叶子1.png"),
            LoadGameplaySprite("ggj/通用/叶子2.png"),
            LoadGameplaySprite("ggj/通用/叶子3.png"),
            LoadGameplaySprite("ggj/通用/叶子4.png")
        };

        SpriteRenderer renderer = spriteVisual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites[0];
        renderer.sortingLayerName = "Actor";

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.drag = 0f;
        body.angularDrag = 1.2f;
        body.mass = 0.75f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        root.AddComponent<CircleCollider2D>().radius = 0.42f;

        root.AddComponent<LeafWindFeedback>().Configure(windDeform.transform, spriteVisual.transform);
        root.AddComponent<Windable>().Configure(0.75f);
        spriteVisual.AddComponent<YSort>().Configure("Actor", 1000, 3f, true);
        root.AddComponent<LeafAppearance>().Configure(
            sprites,
            renderer,
            new Vector2(0.99f, 1.38f),
            new Vector2(0.84f, 1.26f),
            new Vector2(0.45f, 1.05f));
        root.AddComponent<LeafLifecycle>();
        return SaveBasePrefab(root, $"{PrefabRoot}/Leaves/Leaf.prefab");
    }

    private static GameObject CreateBoundaryWallPrefab()
    {
        GameObject root = new GameObject("BoundaryWall") { layer = ObstacleLayer };
        root.AddComponent<BoxCollider2D>().size = Vector2.one;
        return SaveBasePrefab(root, $"{PrefabRoot}/Systems/BoundaryWall.prefab");
    }

    private static GameObject CreateWindBlowerPrefab()
    {
        GameObject root = new GameObject("WindBlower");
        root.AddComponent<WindBlower>().ConfigureLayer(1 << LeafLayer);
        return SaveBasePrefab(root, $"{PrefabRoot}/Systems/WindBlower.prefab");
    }

    private static GameObject CreateObstaclePrefab(string name, string spritePath)
    {
        GameObject root = CreateSpriteRoot(name, spritePath, "Actor", 1000);
        root.layer = ObstacleLayer;
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        PolygonCollider2D collider = root.AddComponent<PolygonCollider2D>();
        collider.pathCount = 1;
        collider.SetPath(0, CreateEllipsePath(renderer.sprite.bounds.size, 0.94f, 0.82f, 14));
        root.AddComponent<YSort>().Configure("Actor", 1000, renderer.sprite.bounds.extents.y, false);
        return SaveBasePrefab(root, $"{PrefabRoot}/Props/{name}.prefab");
    }

    private static GameObject CreateDecorationPrefab(string name, string spritePath)
    {
        GameObject root = CreateSpriteRoot(name, spritePath, "Actor", 960);
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        root.AddComponent<YSort>().Configure("Actor", 960, renderer.sprite.bounds.extents.y, false);
        return SaveBasePrefab(root, $"{PrefabRoot}/Props/{name}.prefab");
    }

    private static GameObject CreateTreePrefab(string name, string spritePath, Material material)
    {
        GameObject root = CreateSpriteRoot(name, spritePath, "Actor", 960);
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        renderer.sharedMaterial = material;
        root.AddComponent<YSort>().Configure("Actor", 960, renderer.sprite.bounds.extents.y, false);
        root.AddComponent<LeafDropSource>().Configure(renderer);

        GameObject physical = new GameObject("TreeTrunkCollider") { layer = ObstacleLayer };
        physical.transform.SetParent(root.transform, false);
        CapsuleCollider2D trunk = physical.AddComponent<CapsuleCollider2D>();
        trunk.isTrigger = true;
        Bounds bounds = renderer.sprite.bounds;
        trunk.direction = CapsuleDirection2D.Vertical;
        trunk.size = new Vector2(bounds.size.x * 0.28f, bounds.size.y * 0.24f);
        trunk.offset = new Vector2(bounds.center.x, bounds.min.y + bounds.size.y * 0.22f);
        return SaveBasePrefab(root, $"{PrefabRoot}/Props/{name}.prefab");
    }

    private static GameObject CreatePondPrefab(string name, string spritePath)
    {
        GameObject root = CreateSpriteRoot(name, spritePath, "Ground", 4);
        root.layer = RiverLayer;
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        PolygonCollider2D collider = root.AddComponent<PolygonCollider2D>();
        collider.isTrigger = true;
        collider.pathCount = 1;
        collider.SetPath(0, CreateEllipsePath(renderer.sprite.bounds.size, 0.62f, 0.42f, 24));
        root.AddComponent<RiverCollector>();
        return SaveBasePrefab(root, $"{PrefabRoot}/Water/{name}.prefab");
    }

    private static GameObject CreateWholeRiverPrefab()
    {
        GameObject root = CreateSpriteRoot("MainRiverWhole", WholeRiverRelativePath, "Ground", 2);
        root.layer = RiverLayer;
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = renderer.sprite.bounds.size;

        RiverWaterMask mask = root.AddComponent<RiverWaterMask>();
        mask.Configure(renderer, renderer.sprite.texture);
        RiverImagePiece piece = root.AddComponent<RiverImagePiece>();
        piece.Configure(renderer, mask);
        root.AddComponent<RiverFlowOverlay>().Configure(36);
        root.AddComponent<RiverCollector>().SetWaterMask(mask, 0f);
        return SaveBasePrefab(root, $"{PrefabRoot}/Water/MainRiverWhole.prefab");
    }

    private static GameObject CreateSpriteRoot(string name, string spritePath, string sortingLayer, int sortingOrder)
    {
        Sprite sprite = LoadGameplaySprite(spritePath);
        if (sprite == null) throw new InvalidOperationException($"Missing Sprite for {spritePath}");
        GameObject root = new GameObject(name);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;
        return root;
    }

    private static GameObject SaveBasePrefab(GameObject root, string path)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        if (saved == null) throw new InvalidOperationException($"Failed to save prefab {path}");
        return saved;
    }

    private static void ValidatePrefabKeys(TiledMapPrototypeImporter.Layout layout, Dictionary<string, GameObject> prefabs)
    {
        ValidateObjectKeys(layout.Obstacles, prefabs);
        ValidateObjectKeys(layout.Decorations, prefabs);
        ValidateObjectKeys(layout.Landmarks, prefabs);
        string[] required = { "Leaf", "BoundaryWall", "WindBlower", "Pond_01", "MainRiverWhole" };
        for (int i = 0; i < required.Length; i++)
        {
            if (!prefabs.TryGetValue(required[i], out GameObject prefab) || prefab == null)
                throw new InvalidOperationException($"Missing generated prefab key: {required[i]}");
        }
    }

    private static void ValidateObjectKeys(TiledMapPrototypeImporter.MapObject[] objects, Dictionary<string, GameObject> prefabs)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (!prefabs.TryGetValue(objects[i].PrefabKey, out GameObject prefab) || prefab == null)
                throw new InvalidOperationException($"TMJ object {objects[i].Name} references missing prefabKey {objects[i].PrefabKey}.");
        }
    }

    private static LevelRoot CreateLevelPrefab(
        ModeSpec mode,
        TiledMapPrototypeImporter.Layout layout,
        Dictionary<string, GameObject> prefabs,
        TileBase greenTile,
        TileBase yellowTile,
        bool force,
        out bool skipped)
    {
        string path = $"{LevelPrefabRoot}/Level_{mode.Id}.prefab";
        LevelRoot existing = AssetDatabase.LoadAssetAtPath<LevelRoot>(path);
        if (existing != null && !force)
        {
            skipped = true;
            return existing;
        }

        skipped = false;
        GameObject rootObject = new GameObject($"Level_{mode.Id}");
        LevelRoot levelRoot = rootObject.AddComponent<LevelRoot>();

        GroundTilemapGenerator ground = CreateGround(rootObject.transform, layout, greenTile, yellowTile);
        CreateBoundaries(rootObject.transform, layout.Bounds, prefabs["BoundaryWall"]);
        CreateWater(rootObject.transform, layout, prefabs);
        CreatePlacedObjects(rootObject.transform, "Obstacles", layout.Obstacles, prefabs, true);
        CreatePlacedObjects(rootObject.transform, "Decorations", layout.Decorations, prefabs, false);
        CreatePlacedObjects(rootObject.transform, "Landmarks", layout.Landmarks, prefabs, false);

        WindBlower windBlower = InstantiatePrefab(prefabs["WindBlower"], rootObject.transform).GetComponent<WindBlower>();
        windBlower.name = "WindBlower";
        windBlower.transform.localPosition = layout.WindStart;
        LeafSpawner spawner = CreateLeafSpawner(rootObject.transform, layout, prefabs["Leaf"]);

        MapPrototypeGizmos gizmos = rootObject.AddComponent<MapPrototypeGizmos>();
        MapPrototypeGizmos.Region[] guideRegions = new MapPrototypeGizmos.Region[layout.Regions.Length];
        for (int i = 0; i < guideRegions.Length; i++) guideRegions[i] = new MapPrototypeGizmos.Region(layout.Regions[i].RegionId, layout.Regions[i].Bounds);
        gizmos.Configure(
            TiledMapPrototypeImporter.SourceAssetPath,
            layout.SourceSha256,
            layout.RiverPoints,
            guideRegions,
            layout.CameraStart,
            layout.WindStart);

        levelRoot.Configure(
            mode.Id,
            layout.Bounds,
            layout.CameraStart,
            10f,
            5f,
            20f,
            mode.InitialLeafCount,
            mode.TimeLimitSeconds,
            mode.Endless,
            mode.EndlessSpawnBatch,
            mode.EndlessSpawnInterval,
            mode.EndlessMaxLeaves,
            mode.EndlessSurvivalMaximum,
            mode.EndlessSurvivalInitial,
            mode.EndlessSurvivalPerLeaf,
            mode.EndlessSurvivalBaseDrain,
            mode.EndlessSurvivalStageSeconds,
            mode.EndlessSurvivalStageMultiplier,
            ground,
            spawner,
            windBlower);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(rootObject, path);
        UnityEngine.Object.DestroyImmediate(rootObject);
        if (saved == null) throw new InvalidOperationException($"Failed to save level prefab {path}");
        return saved.GetComponent<LevelRoot>();
    }

    private static GroundTilemapGenerator CreateGround(
        Transform parent,
        TiledMapPrototypeImporter.Layout layout,
        TileBase greenTile,
        TileBase yellowTile)
    {
        GameObject gridObject = new GameObject("GroundGrid");
        gridObject.transform.SetParent(parent, false);
        Grid grid = gridObject.AddComponent<Grid>();

        GameObject tilemapObject = new GameObject("GroundTilemap");
        tilemapObject.transform.SetParent(gridObject.transform, false);
        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();
        GroundTilemapGenerator generator = tilemapObject.AddComponent<GroundTilemapGenerator>();

        GroundTilemapGenerator.RegionHint[] hints = new GroundTilemapGenerator.RegionHint[layout.Regions.Length];
        for (int i = 0; i < hints.Length; i++)
            hints[i] = new GroundTilemapGenerator.RegionHint(layout.Regions[i].RegionId, layout.Regions[i].Bounds, layout.Regions[i].GreenBias);

        generator.Configure(
            layout.Bounds,
            greenTile,
            yellowTile,
            tilemap,
            grid,
            GroundSeed,
            GroundPatchWorldSize,
            hints);
        generator.Rebuild();
        return generator;
    }

    private static void CreateBoundaries(Transform parent, Rect bounds, GameObject wallPrefab)
    {
        GameObject group = new GameObject("Boundaries");
        group.transform.SetParent(parent, false);
        const float thickness = 2f;

        PlaceWall(group.transform, wallPrefab, "Wall_Left",
            new Vector2(bounds.xMin - thickness * 0.5f, bounds.center.y),
            new Vector2(thickness, bounds.height + thickness * 2f));
        PlaceWall(group.transform, wallPrefab, "Wall_Right",
            new Vector2(bounds.xMax + thickness * 0.5f, bounds.center.y),
            new Vector2(thickness, bounds.height + thickness * 2f));
        PlaceWall(group.transform, wallPrefab, "Wall_Top",
            new Vector2(bounds.center.x, bounds.yMax + thickness * 0.5f),
            new Vector2(bounds.width + thickness * 2f, thickness));
        PlaceWall(group.transform, wallPrefab, "Wall_Bottom",
            new Vector2(bounds.center.x, bounds.yMin - thickness * 0.5f),
            new Vector2(bounds.width + thickness * 2f, thickness));
    }

    private static void PlaceWall(Transform parent, GameObject prefab, string name, Vector2 position, Vector2 size)
    {
        GameObject instance = InstantiatePrefab(prefab, parent);
        instance.name = name;
        instance.transform.localPosition = position;
        instance.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    private static void CreateWater(
        Transform parent,
        TiledMapPrototypeImporter.Layout layout,
        Dictionary<string, GameObject> prefabs)
    {
        GameObject group = new GameObject("Water");
        group.transform.SetParent(parent, false);
        CreateWholeRiver(group.transform, layout, prefabs);
        CreateLake(group.transform, layout.Lake, prefabs["Pond_01"]);
    }

    private static void CreateWholeRiver(
        Transform parent,
        TiledMapPrototypeImporter.Layout layout,
        Dictionary<string, GameObject> prefabs)
    {
        GameObject instance = InstantiatePrefab(prefabs["MainRiverWhole"], parent);
        instance.name = "MainRiver_Whole";
        RiverImagePiece piece = instance.GetComponent<RiverImagePiece>();
        if (piece == null) throw new InvalidOperationException("MainRiverWhole has no RiverImagePiece component.");

        FitRiverPiece(
            instance.transform,
            piece,
            layout.RiverPoints[0],
            layout.RiverPoints[layout.RiverPoints.Length - 1]);

        RiverCollector collector = instance.GetComponent<RiverCollector>();
        RiverWaterMask mask = instance.GetComponent<RiverWaterMask>();
        if (collector != null) collector.SetWaterMask(mask, 0f);
    }

    private static void FitRiverPiece(Transform transform, RiverImagePiece piece, Vector2 targetEntry, Vector2 targetExit)
    {
        Vector2 localDelta = piece.ExitAnchor - piece.EntryAnchor;
        Vector2 targetDelta = targetExit - targetEntry;
        if (localDelta.sqrMagnitude < 0.001f || targetDelta.sqrMagnitude < 0.001f)
            throw new InvalidOperationException("River image piece has invalid entry/exit anchors.");

        float scale = targetDelta.magnitude / localDelta.magnitude;
        float rotation = Mathf.Atan2(targetDelta.y, targetDelta.x) * Mathf.Rad2Deg
            - Mathf.Atan2(localDelta.y, localDelta.x) * Mathf.Rad2Deg;
        transform.localScale = new Vector3(scale, scale, 1f);
        transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        Vector2 transformedEntry = (Vector2)(transform.localRotation * (piece.EntryAnchor * scale));
        transform.localPosition = targetEntry - transformedEntry;
    }

    private static void CreateLake(Transform parent, TiledMapPrototypeImporter.Lake lake, GameObject pondPrefab)
    {
        GameObject root = new GameObject("Lake_" + lake.Name) { layer = RiverLayer };
        root.transform.SetParent(parent, false);
        root.transform.localPosition = lake.Position;

        PolygonCollider2D waterCollider = root.AddComponent<PolygonCollider2D>();
        waterCollider.isTrigger = true;
        waterCollider.pathCount = 1;
        waterCollider.SetPath(0, CreateEllipsePath(lake.Size, 1f, 1f, 32));
        root.AddComponent<RiverCollector>();

        GameObject visual = InstantiatePrefab(pondPrefab, root.transform);
        visual.name = "Pond_01_Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        SetLayerRecursively(visual, 0);
        Collider2D[] visualColliders = visual.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < visualColliders.Length; i++) visualColliders[i].enabled = false;
        RiverCollector[] visualCollectors = visual.GetComponentsInChildren<RiverCollector>(true);
        for (int i = 0; i < visualCollectors.Length; i++) visualCollectors[i].enabled = false;

        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != null)
        {
            Vector2 spriteSize = renderer.sprite.bounds.size;
            float scale = Mathf.Min(lake.Size.x / spriteSize.x, lake.Size.y / spriteSize.y);
            visual.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.sortingLayerName = "Ground";
            renderer.sortingOrder = 10;
        }
    }

    private static void CreatePlacedObjects(
        Transform parent,
        string groupName,
        TiledMapPrototypeImporter.MapObject[] placements,
        Dictionary<string, GameObject> prefabs,
        bool obstacleGroup)
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent, false);

        for (int i = 0; i < placements.Length; i++)
        {
            TiledMapPrototypeImporter.MapObject placement = placements[i];
            GameObject instance = InstantiatePrefab(prefabs[placement.PrefabKey], group.transform);
            instance.name = placement.Name;
            instance.transform.localPosition = placement.Position;
            instance.transform.localRotation = Quaternion.Euler(0f, 0f, placement.Rotation);
            ScaleToWorldSize(instance, placement.Size);

            SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
            YSort sort = instance.GetComponent<YSort>();
            if (sort != null && renderer != null)
                sort.Configure(renderer.sortingLayerName, renderer.sortingOrder, placement.Size.y * 0.5f, false);

            if (obstacleGroup && placement.BlocksLeaf) AddObstacleFootprints(instance, placement.PrefabKey, renderer);
        }
    }

    private static void AddObstacleFootprints(GameObject instance, string prefabKey, SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return;
        if (prefabKey.StartsWith("Tree_", StringComparison.Ordinal)) return;

        instance.layer = ObstacleLayer;

        GameObject exclusion = new GameObject("SpawnExclusion") { layer = ObstacleLayer };
        exclusion.transform.SetParent(instance.transform, false);
        BoxCollider2D exclusionCollider = exclusion.AddComponent<BoxCollider2D>();
        exclusionCollider.isTrigger = true;
        exclusionCollider.size = renderer.sprite.bounds.size;
        exclusionCollider.offset = renderer.sprite.bounds.center;

    }

    private static LeafSpawner CreateLeafSpawner(
        Transform parent,
        TiledMapPrototypeImporter.Layout layout,
        GameObject leafPrefab)
    {
        GameObject root = new GameObject("LeafSpawner");
        root.transform.SetParent(parent, false);
        LeafSpawner spawner = root.AddComponent<LeafSpawner>();
        GameObject leafContainer = new GameObject("Leaves");
        leafContainer.transform.SetParent(root.transform, false);
        spawner.Configure(
            leafPrefab.GetComponent<LeafLifecycle>(),
            layout.Bounds,
            (1 << ObstacleLayer) | (1 << RiverLayer),
            layout.ScatterClearance,
            260,
            leafContainer.transform);
        return spawner;
    }

    private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null) throw new InvalidOperationException($"Failed to instantiate prefab {prefab.name}");
        return instance;
    }

    private static void ScaleToWorldSize(GameObject instance, Vector2 worldSize)
    {
        SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return;
        Vector2 spriteSize = renderer.sprite.bounds.size;
        instance.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.001f, spriteSize.x),
            worldSize.y / Mathf.Max(0.001f, spriteSize.y),
            1f);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++) SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
    }

    private static float GetPolylineLength(Vector2[] points)
    {
        float length = 0f;
        for (int i = 1; i < points.Length; i++) length += Vector2.Distance(points[i - 1], points[i]);
        return length;
    }

    private static Vector2 PointAtDistance(Vector2[] points, float distance)
    {
        if (points == null || points.Length == 0) return Vector2.zero;
        if (distance <= 0f) return points[0];
        float remaining = distance;
        for (int i = 1; i < points.Length; i++)
        {
            float segmentLength = Vector2.Distance(points[i - 1], points[i]);
            if (remaining <= segmentLength)
                return Vector2.Lerp(points[i - 1], points[i], segmentLength > 0f ? remaining / segmentLength : 0f);
            remaining -= segmentLength;
        }
        return points[points.Length - 1];
    }

    private static Vector2[] CreateEllipsePath(Vector2 size, float widthRatio, float heightRatio, int segments)
    {
        Vector2[] points = new Vector2[Mathf.Max(8, segments)];
        float radiusX = size.x * widthRatio * 0.5f;
        float radiusY = size.y * heightRatio * 0.5f;
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.PI * 2f;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }
        return points;
    }

    private static LevelCatalog CreateOrUpdateCatalog(Dictionary<LevelId, LevelRoot> levels)
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        LevelCatalog.Entry[] entries =
        {
            new LevelCatalog.Entry { Id = LevelId.SimpleSmall, Prefab = levels[LevelId.SimpleSmall] },
            new LevelCatalog.Entry { Id = LevelId.TimedChallenge, Prefab = levels[LevelId.TimedChallenge] },
            new LevelCatalog.Entry { Id = LevelId.Endless, Prefab = levels[LevelId.Endless] }
        };
        catalog.Configure(entries);
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void ValidateMigration(
        LevelCatalog catalog,
        TiledMapPrototypeImporter.Layout layout,
        List<string> gameplayArtFiles,
        List<string> report)
    {
        List<string> errors = new List<string>();
        Material treeMaterial = AssetDatabase.LoadAssetAtPath<Material>(TreeMaterialPath);
        if (treeMaterial == null
            || treeMaterial.shader == null
            || treeMaterial.shader.name != "FallenLeaves/TreeCursorFade")
        {
            errors.Add("TreeCursorFade material is missing or uses the wrong shader");
        }
        else if (!Mathf.Approximately(treeMaterial.GetFloat("_InnerRadius"), 35f)
            || !Mathf.Approximately(treeMaterial.GetFloat("_OuterRadius"), 60f)
            || !Mathf.Approximately(treeMaterial.GetFloat("_MinimumOpacity"), 0.25f))
        {
            errors.Add("TreeCursorFade material does not use the expected 35/60 pixel radii and 0.25 opacity");
        }

        for (int treeIndex = 1; treeIndex <= 9; treeIndex++)
        {
            GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabRoot}/Props/Tree_{treeIndex:00}.prefab");
            SpriteRenderer renderer = tree != null ? tree.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sharedMaterial != treeMaterial)
                errors.Add($"Tree_{treeIndex:00} does not use the shared TreeCursorFade material");
        }

        for (int reedIndex = 1; reedIndex <= 2; reedIndex++)
        {
            GameObject reed = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabRoot}/Props/Reed_{reedIndex:00}.prefab");
            SpriteRenderer renderer = reed != null ? reed.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sharedMaterial == treeMaterial)
                errors.Add($"Reed_{reedIndex:00} incorrectly uses the TreeCursorFade material");
        }
        for (int i = 0; i < gameplayArtFiles.Count; i++)
        {
            if (LoadGameplaySprite(gameplayArtFiles[i]) == null) errors.Add($"Missing imported Sprite: {gameplayArtFiles[i]}");
        }

        LevelId[] ids = { LevelId.SimpleSmall, LevelId.TimedChallenge, LevelId.Endless };
        for (int i = 0; i < ids.Length; i++)
        {
            LevelRoot prefab = catalog.GetPrefab(ids[i]);
            if (prefab == null)
            {
                errors.Add($"Catalog is missing {ids[i]}");
                continue;
            }

            if (prefab.MapBounds != layout.Bounds) errors.Add($"{ids[i]} has incorrect 120x90 map bounds");
            if (Vector2.Distance(prefab.CameraStart, layout.CameraStart) > 0.01f) errors.Add($"{ids[i]} has incorrect CameraStart");
            MapPrototypeGizmos metadata = prefab.GetComponent<MapPrototypeGizmos>();
            if (metadata == null || metadata.SourceSha256 != layout.SourceSha256) errors.Add($"{ids[i]} has stale/missing TMJ metadata");

            GroundTilemapGenerator ground = prefab.GetComponentInChildren<GroundTilemapGenerator>(true);
            if (ground == null)
            {
                errors.Add($"{ids[i]} has no mixed ground generator");
            }
            else
            {
                ground.CountTiles(out int green, out int yellow);
                if (green == 0 || yellow == 0 || Mathf.Abs(green - yellow) > 1) errors.Add($"{ids[i]} ground is not 50/50 green/yellow");
                float overall = green / (float)Mathf.Max(1, green + yellow);
                for (int regionIndex = 0; regionIndex < layout.Regions.Length; regionIndex++)
                {
                    TiledMapPrototypeImporter.Region region = layout.Regions[regionIndex];
                    float ratio = ground.GetGreenRatio(region.Bounds);
                    if (region.RegionId == "Meadow" && ratio >= overall) errors.Add($"{ids[i]} Meadow is not yellow-biased");
                    if (region.RegionId != "Meadow" && ratio <= overall) errors.Add($"{ids[i]} {region.RegionId} is not green-biased");
                }
            }

            RiverImagePiece[] pieces = prefab.GetComponentsInChildren<RiverImagePiece>(true);
            if (pieces.Length != 1) errors.Add($"{ids[i]} has {pieces.Length} river image pieces instead of 1");
            else
            {
                if (pieces[0].name != "MainRiver_Whole") errors.Add($"{ids[i]} has an unexpected river instance named {pieces[0].name}");
                if (pieces[0].GetComponent<RiverSpriteShapeAdapter>() != null) errors.Add($"{ids[i]} whole river still has a segment seam adapter");
                if (Vector2.Distance(pieces[0].WorldEntry, layout.RiverPoints[0]) > 0.1f) errors.Add($"{ids[i]} whole river entry is not aligned to the TMJ route");
                if (Vector2.Distance(pieces[0].WorldExit, layout.RiverPoints[layout.RiverPoints.Length - 1]) > 0.1f) errors.Add($"{ids[i]} whole river exit is not aligned to the TMJ route");
            }
            if (prefab.transform.Find("BoundaryArt") != null) errors.Add($"{ids[i]} still contains old boundary art");
            if (prefab.transform.Find("LeafSpawner/LeafSpawnArea") != null) errors.Add($"{ids[i]} still contains a fixed LeafSpawnArea");

            Transform water = prefab.transform.Find("Water");
            if (water == null || water.Find("Lake_" + layout.Lake.Name) == null) errors.Add($"{ids[i]} is missing the TMJ lake");
            CheckGroupCount(prefab.transform, "Obstacles", layout.Obstacles.Length, ids[i], errors);
            CheckGroupCount(prefab.transform, "Decorations", layout.Decorations.Length, ids[i], errors);
            CheckGroupCount(prefab.transform, "Landmarks", layout.Landmarks.Length, ids[i], errors);

            WindBlower wind = prefab.GetComponentInChildren<WindBlower>(true);
            if (wind == null || Vector2.Distance(wind.transform.localPosition, layout.WindStart) > 0.01f)
                errors.Add($"{ids[i]} has incorrect WindStart");
            if (prefab.GetComponentInChildren<LeafSpawner>(true) == null) errors.Add($"{ids[i]} has no full-map LeafSpawner");
        }

        report.Add($"Validation errors: {errors.Count}");
        for (int i = 0; i < errors.Count; i++) report.Add("- " + errors[i]);
        if (errors.Count > 0) throw new InvalidOperationException($"Migration validation found {errors.Count} error(s): {string.Join(" | ", errors)}");
    }

    private static void CheckGroupCount(Transform root, string name, int expected, LevelId id, List<string> errors)
    {
        Transform group = root.Find(name);
        if (group == null || group.childCount != expected)
            errors.Add($"{id} {name} count is {(group == null ? 0 : group.childCount)}, expected {expected}");
    }

    private static Sprite LoadGameplaySprite(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(GameplayArtRoot + relativePath);
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    private static string WriteReport(List<string> lines)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string reportDirectory = Path.GetFullPath(Path.Combine(projectRoot, "..", "logs"));
        Directory.CreateDirectory(reportDirectory);
        string reportPath = Path.Combine(reportDirectory, "tiled-map-prototype-migration-report.txt");
        File.WriteAllLines(reportPath, lines.ToArray());
        return reportPath;
    }

    private static ModeSpec[] CreateModeSpecs()
    {
        return new[]
        {
            new ModeSpec(LevelId.SimpleSmall, 2560, 0f, false, 0, 1.8f, 260, 100f, 100f, 8f, 2f, 60f, 1.3f),
            new ModeSpec(LevelId.TimedChallenge, 1920, 180f, false, 0, 1.8f, 260, 100f, 100f, 8f, 2f, 60f, 1.3f),
            new ModeSpec(LevelId.Endless, 2080, 0f, true, 32, 1.8f, 4160, 100f, 100f, 8f, 2f, 60f, 1.3f)
        };
    }

    private readonly struct ModeSpec
    {
        public readonly LevelId Id;
        public readonly int InitialLeafCount;
        public readonly float TimeLimitSeconds;
        public readonly bool Endless;
        public readonly int EndlessSpawnBatch;
        public readonly float EndlessSpawnInterval;
        public readonly int EndlessMaxLeaves;
        public readonly float EndlessSurvivalMaximum;
        public readonly float EndlessSurvivalInitial;
        public readonly float EndlessSurvivalPerLeaf;
        public readonly float EndlessSurvivalBaseDrain;
        public readonly float EndlessSurvivalStageSeconds;
        public readonly float EndlessSurvivalStageMultiplier;

        public ModeSpec(
            LevelId id,
            int initialLeafCount,
            float timeLimitSeconds,
            bool endless,
            int endlessSpawnBatch,
            float endlessSpawnInterval,
            int endlessMaxLeaves,
            float endlessSurvivalMaximum,
            float endlessSurvivalInitial,
            float endlessSurvivalPerLeaf,
            float endlessSurvivalBaseDrain,
            float endlessSurvivalStageSeconds,
            float endlessSurvivalStageMultiplier)
        {
            Id = id;
            InitialLeafCount = initialLeafCount;
            TimeLimitSeconds = timeLimitSeconds;
            Endless = endless;
            EndlessSpawnBatch = endlessSpawnBatch;
            EndlessSpawnInterval = endlessSpawnInterval;
            EndlessMaxLeaves = endlessMaxLeaves;
            EndlessSurvivalMaximum = endlessSurvivalMaximum;
            EndlessSurvivalInitial = endlessSurvivalInitial;
            EndlessSurvivalPerLeaf = endlessSurvivalPerLeaf;
            EndlessSurvivalBaseDrain = endlessSurvivalBaseDrain;
            EndlessSurvivalStageSeconds = endlessSurvivalStageSeconds;
            EndlessSurvivalStageMultiplier = endlessSurvivalStageMultiplier;
        }
    }

    private sealed class MigrationResult
    {
        public bool Success;
        public int GeneratedLevels;
        public int SkippedLevels;
        public string ReportPath;
    }
}
