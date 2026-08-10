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

    private const int LeafLayer = 8;
    private const int ObstacleLayer = 9;
    private const int RiverLayer = 10;

    private static readonly string[] GameplayArtFiles =
    {
        "ggj/通用/叶子1.png",
        "ggj/通用/叶子2.png",
        "ggj/通用/叶子3.png",
        "ggj/通用/叶子4.png",
        "ggj/通用/草地绿.png",
        "ggj/通用/草地黄.png",
        "ggj/通用/石头1.png",
        "ggj/通用/石头2.png",
        "ggj/通用/石头3.png",
        "ggj/通用/石头4.png",
        "ggj/通用/石头5.png",
        "ggj/通用/石头6.png",
        "ggj/通用/石头7.png",
        "ggj/通用/石头8.png",
        "ggj/通用/石头9.png",
        "ggj/通用/石头10.png",
        "ggj/通用/长条石头1.png",
        "ggj/通用/长条石头2.png",
        "ggj/通用/长条石头3.png",
        "ggj/通用/芦苇1.png",
        "ggj/通用/芦苇2.png",
        "ggj地图补充/山1.png",
        "ggj地图补充/山2.png",
        "ggj地图补充/树1.png",
        "ggj地图补充/树2.png",
        "ggj地图补充/树3.png",
        "ggj地图补充/树4.png",
        "ggj地图补充/树5.png",
        "ggj地图补充/树6.png",
        "ggj地图补充/树7.png",
        "ggj地图补充/树8.png",
        "ggj地图补充/树9.png",
        "ggj地图补充/河1.png",
        "ggj地图补充/河2.png",
        "ggj地图补充/河3.png",
        "ggj地图补充/湖1.png",
        "ggj地图补充/湖2.png",
        "ggj地图补充/湖3.png"
    };

    [MenuItem("Tools/Fallen Leaves/Migrate Level Prefabs")]
    public static void RunMenu()
    {
        try
        {
            MigrationResult result = RunMigration(false);
            EditorUtility.DisplayDialog(
                "Fallen Leaves Migration",
                result.Success
                    ? $"Migration completed.\nGenerated levels: {result.GeneratedLevels}\nSkipped levels: {result.SkippedLevels}\nReport: {result.ReportPath}"
                    : $"Migration failed. See Console and report:\n{result.ReportPath}",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Fallen Leaves Migration", exception.Message, "OK");
        }
    }

    [MenuItem("Tools/Fallen Leaves/Force Rebuild Level Prefabs")]
    public static void RunForceMenu()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Force rebuild level prefabs?",
            "This overwrites all four authored level prefabs. Base art and source files are not deleted.",
            "Rebuild",
            "Cancel");

        if (!confirmed) return;
        RunMigration(true);
    }

    public static void RunBatch()
    {
        MigrationResult result = RunMigration(false);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Level prefab migration failed. Report: {result.ReportPath}");
        }
    }

    private static MigrationResult RunMigration(bool forceLevelRebuild)
    {
        List<string> report = new List<string>
        {
            $"Fallen Leaves level prefab migration - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Unity: {Application.unityVersion}",
            $"Force level rebuild: {forceLevelRebuild}",
            string.Empty
        };

        MigrationResult result = new MigrationResult();

        try
        {
            EnsureFolders();
            CopyAndImportGameplayArt(report);

            Tile greenTile = CreateOrUpdateTile(
                $"{TileRoot}/GroundGreen.asset",
                LoadGameplaySprite("ggj/通用/草地绿.png"));
            Tile yellowTile = CreateOrUpdateTile(
                $"{TileRoot}/GroundYellow.asset",
                LoadGameplaySprite("ggj/通用/草地黄.png"));

            Texture2D waterTexture = CreateOrUpdateWaterTexture();
            Material bankMaterial = CreateOrUpdateMaterial(
                $"{MaterialRoot}/RiverBank.mat",
                LoadGameplayTexture("ggj/通用/草地黄.png"),
                Theme.Bank);
            Material waterMaterial = CreateOrUpdateMaterial(
                $"{MaterialRoot}/RiverWater.mat",
                waterTexture,
                Color.white);

            Dictionary<string, GameObject> prefabs = CreateBasePrefabs(bankMaterial, waterMaterial, report);
            LevelSpec[] specs = CreateLevelSpecs(greenTile, yellowTile);
            Dictionary<LevelId, LevelRoot> levels = new Dictionary<LevelId, LevelRoot>();

            for (int i = 0; i < specs.Length; i++)
            {
                bool skipped;
                LevelRoot level = CreateLevelPrefab(
                    specs[i],
                    prefabs,
                    bankMaterial,
                    waterMaterial,
                    forceLevelRebuild,
                    out skipped);

                levels[specs[i].Id] = level;
                if (skipped) result.SkippedLevels++;
                else result.GeneratedLevels++;
            }

            LevelCatalog catalog = CreateOrUpdateCatalog(levels);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ValidateMigration(catalog, report);
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
            Debug.LogException(exception);
            result.Success = false;
        }

        result.ReportPath = WriteReport(report);
        if (!result.Success) throw new InvalidOperationException($"Migration failed. See {result.ReportPath}");

        Debug.Log($"Level prefab migration completed. Report: {result.ReportPath}");
        return result;
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
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static void CopyAndImportGameplayArt(List<string> report)
    {
        int copied = 0;
        int reused = 0;

        for (int i = 0; i < GameplayArtFiles.Length; i++)
        {
            string relative = GameplayArtFiles[i];
            string sourceAssetPath = SourceArtRoot + relative;
            string destinationAssetPath = GameplayArtRoot + relative;
            EnsureAssetFolder(Path.GetDirectoryName(destinationAssetPath).Replace('\\', '/'));

            string sourceAbsolute = ToAbsolutePath(sourceAssetPath);
            string destinationAbsolute = ToAbsolutePath(destinationAssetPath);
            if (!File.Exists(sourceAbsolute))
            {
                throw new FileNotFoundException("Missing source gameplay art", sourceAssetPath);
            }

            if (!File.Exists(destinationAbsolute))
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

        for (int i = 0; i < GameplayArtFiles.Length; i++)
        {
            string relative = GameplayArtFiles[i];
            string destinationAssetPath = GameplayArtRoot + relative;
            TextureImporter importer = AssetImporter.GetAtPath(destinationAssetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"TextureImporter unavailable for {destinationAssetPath}");
            }

            bool riverMask = relative.StartsWith("ggj地图补充/河", StringComparison.Ordinal);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.isReadable = riverMask;
            importer.textureCompression = riverMask
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.Compressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        report.Add($"Gameplay art copied: {copied}");
        report.Add($"Gameplay art reused: {reused}");
    }

    private static Tile CreateOrUpdateTile(string assetPath, Sprite sprite)
    {
        if (sprite == null) throw new InvalidOperationException($"Cannot create Tile without Sprite: {assetPath}");

        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, assetPath);
        }

        tile.sprite = sprite;
        tile.color = Color.white;
        tile.colliderType = Tile.ColliderType.None;
        tile.flags = TileFlags.LockColor;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static Texture2D CreateOrUpdateWaterTexture()
    {
        string path = $"{MaterialRoot}/GeneratedWaterTexture.asset";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int width = 192;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "GeneratedWaterTexture",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width;
                float v = y / (float)height;
                float edge = Mathf.Min(v, 1f - v) * 2f;
                float wave = Mathf.Sin(u * Mathf.PI * 8f + v * 2f) * 0.5f + 0.5f;
                float foam = Mathf.SmoothStep(0f, 0.18f, 1f - edge) * 0.35f;
                float tone = Mathf.Lerp(0.85f, 1f, wave);
                texture.SetPixel(x, y, Color.Lerp(Theme.WaterFoam, Theme.Water, Mathf.Clamp01(tone - foam)));
            }
        }

        texture.Apply(false, false);
        AssetDatabase.CreateAsset(texture, path);
        return texture;
    }

    private static Material CreateOrUpdateMaterial(string path, Texture texture, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Sprites/Default shader is unavailable.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.mainTexture = texture;
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Dictionary<string, GameObject> CreateBasePrefabs(
        Material bankMaterial,
        Material waterMaterial,
        List<string> report)
    {
        Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        prefabs["Leaf"] = CreateLeafPrefab();
        prefabs["BoundaryWall"] = CreateBoundaryWallPrefab();
        prefabs["WindBlower"] = CreateWindBlowerPrefab();
        prefabs["RiverPath"] = CreateRiverPathPrefab(bankMaterial, waterMaterial);

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
            prefabs[key] = CreateDecorationPrefab(key, $"ggj地图补充/树{i}.png");
        }

        for (int i = 1; i <= 2; i++)
        {
            string key = $"Mountain_{i:00}";
            prefabs[key] = CreateMountainPrefab(key, $"ggj地图补充/山{i}.png");
        }

        for (int i = 1; i <= 3; i++)
        {
            string pondKey = $"Pond_{i:00}";
            prefabs[pondKey] = CreatePondPrefab(pondKey, $"ggj地图补充/湖{i}.png");

            string riverKey = $"RiverArt_{i:00}";
            prefabs[riverKey] = CreateRiverArtPrefab(riverKey, $"ggj地图补充/河{i}.png");
        }

        report.Add($"Base prefabs created or updated: {prefabs.Count}");
        return prefabs;
    }

    private static GameObject CreateLeafPrefab()
    {
        GameObject root = new GameObject("Leaf");
        root.layer = LeafLayer;

        Sprite[] sprites =
        {
            LoadGameplaySprite("ggj/通用/叶子1.png"),
            LoadGameplaySprite("ggj/通用/叶子2.png"),
            LoadGameplaySprite("ggj/通用/叶子3.png"),
            LoadGameplaySprite("ggj/通用/叶子4.png")
        };

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites[0];
        renderer.sortingLayerName = "Actor";

        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.drag = 0.55f;
        body.angularDrag = 1.2f;
        body.mass = 0.75f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
        collider.radius = 0.42f;

        Windable windable = root.AddComponent<Windable>();
        windable.Configure(0.75f);

        YSort sort = root.AddComponent<YSort>();
        sort.Configure("Actor", 1000, 3f, true);

        LeafAppearance appearance = root.AddComponent<LeafAppearance>();
        appearance.Configure(
            sprites,
            new Vector2(6.6f, 9.2f),
            new Vector2(5.6f, 8.4f),
            new Vector2(0.45f, 1.05f));
        root.AddComponent<LeafLifecycle>();

        return SaveBasePrefab(root, $"{PrefabRoot}/Leaves/Leaf.prefab");
    }

    private static GameObject CreateBoundaryWallPrefab()
    {
        GameObject root = new GameObject("BoundaryWall");
        root.layer = ObstacleLayer;
        root.AddComponent<BoxCollider2D>().size = Vector2.one;
        return SaveBasePrefab(root, $"{PrefabRoot}/Systems/BoundaryWall.prefab");
    }

    private static GameObject CreateWindBlowerPrefab()
    {
        GameObject root = new GameObject("WindBlower");
        WindBlower blower = root.AddComponent<WindBlower>();
        blower.ConfigureLayer(1 << LeafLayer);
        return SaveBasePrefab(root, $"{PrefabRoot}/Systems/WindBlower.prefab");
    }

    private static GameObject CreateRiverPathPrefab(Material bankMaterial, Material waterMaterial)
    {
        GameObject root = new GameObject("RiverPath");
        RiverPath2D river = root.AddComponent<RiverPath2D>();

        LineRenderer bank = CreateLineRenderer(root.transform, "Bank", bankMaterial, 2);
        LineRenderer water = CreateLineRenderer(root.transform, "Water", waterMaterial, 3);
        river.Configure(
            new[] { new Vector2(-10f, 0f), new Vector2(10f, 0f) },
            20f,
            bankMaterial,
            waterMaterial,
            bank,
            water);

        return SaveBasePrefab(root, $"{PrefabRoot}/Water/RiverPath.prefab");
    }

    private static LineRenderer CreateLineRenderer(Transform parent, string name, Material material, int order)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        LineRenderer line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.sortingLayerName = "Ground";
        line.sortingOrder = order;
        line.useWorldSpace = false;
        line.textureMode = LineTextureMode.Tile;
        return line;
    }

    private static GameObject CreateObstaclePrefab(string name, string spritePath)
    {
        GameObject root = CreateSpriteRoot(name, spritePath, "Actor", 1000);
        root.layer = ObstacleLayer;

        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        PolygonCollider2D collider = root.AddComponent<PolygonCollider2D>();
        collider.pathCount = 1;
        collider.SetPath(0, CreateEllipsePath(renderer.sprite.bounds.size, 0.94f, 0.82f, 14));

        YSort sort = root.AddComponent<YSort>();
        sort.Configure("Actor", 1000, renderer.sprite.bounds.extents.y, false);
        return SaveBasePrefab(root, $"{PrefabRoot}/Props/{name}.prefab");
    }

    private static GameObject CreateDecorationPrefab(string name, string spritePath)
    {
        GameObject root = CreateSpriteRoot(name, spritePath, "Actor", 960);
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        YSort sort = root.AddComponent<YSort>();
        sort.Configure("Actor", 960, renderer.sprite.bounds.extents.y, false);
        return SaveBasePrefab(root, $"{PrefabRoot}/Props/{name}.prefab");
    }

    private static GameObject CreateMountainPrefab(string name, string spritePath)
    {
        GameObject root = CreateSpriteRoot(name, spritePath, "Background", 4);
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

    private static GameObject CreateRiverArtPrefab(string name, string spritePath)
    {
        GameObject root = CreateSpriteRoot(name, spritePath, "Ground", 2);
        root.layer = RiverLayer;

        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = renderer.sprite.bounds.size;

        RiverWaterMask mask = root.AddComponent<RiverWaterMask>();
        mask.Configure(renderer, renderer.sprite.texture);
        RiverCollector collector = root.AddComponent<RiverCollector>();
        collector.SetWaterMask(mask);
        return SaveBasePrefab(root, $"{PrefabRoot}/Water/{name}.prefab");
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

    private static Vector2[] CreateEllipsePath(Vector2 spriteSize, float widthRatio, float heightRatio, int segments)
    {
        Vector2[] points = new Vector2[Mathf.Max(8, segments)];
        float radiusX = spriteSize.x * widthRatio * 0.5f;
        float radiusY = spriteSize.y * heightRatio * 0.5f;

        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.PI * 2f;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        return points;
    }

    private static LevelRoot CreateLevelPrefab(
        LevelSpec spec,
        Dictionary<string, GameObject> prefabs,
        Material bankMaterial,
        Material waterMaterial,
        bool force,
        out bool skipped)
    {
        string path = $"{LevelPrefabRoot}/Level_{spec.Id}.prefab";
        LevelRoot existing = AssetDatabase.LoadAssetAtPath<LevelRoot>(path);
        if (existing != null && !force)
        {
            skipped = true;
            return existing;
        }

        skipped = false;
        GameObject rootObject = new GameObject($"Level_{spec.Id}");
        LevelRoot levelRoot = rootObject.AddComponent<LevelRoot>();

        GroundTilemapGenerator ground = CreateGround(rootObject.transform, spec.Bounds, spec.GroundTile);
        CreateBoundaries(rootObject.transform, spec.Bounds, prefabs["BoundaryWall"]);
        CreateBoundaryArt(rootObject.transform, spec.Bounds, prefabs);
        CreateWater(rootObject.transform, spec, prefabs, bankMaterial, waterMaterial);
        CreatePlacedObjects(rootObject.transform, "Obstacles", spec.Obstacles, prefabs);
        CreatePlacedObjects(rootObject.transform, "Decorations", spec.Decorations, prefabs);

        WindBlower windBlower = InstantiatePrefab(prefabs["WindBlower"], rootObject.transform).GetComponent<WindBlower>();
        LeafSpawner spawner = CreateLeafSpawner(rootObject.transform, spec.Bounds, prefabs["Leaf"]);

        levelRoot.Configure(
            spec.Id,
            spec.Bounds,
            spec.InitialCameraSize,
            spec.MinCameraSize,
            spec.MaxCameraSize,
            spec.InitialLeafCount,
            spec.TimeLimitSeconds,
            spec.Endless,
            spec.EndlessSpawnBatch,
            spec.EndlessSpawnInterval,
            spec.EndlessMaxLeaves,
            ground,
            spawner,
            windBlower);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(rootObject, path);
        UnityEngine.Object.DestroyImmediate(rootObject);
        if (saved == null) throw new InvalidOperationException($"Failed to save level prefab {path}");
        return saved.GetComponent<LevelRoot>();
    }

    private static GroundTilemapGenerator CreateGround(Transform parent, Rect bounds, TileBase tile)
    {
        GameObject gridObject = new GameObject("GroundGrid");
        gridObject.transform.SetParent(parent, false);
        Grid grid = gridObject.AddComponent<Grid>();

        GameObject tilemapObject = new GameObject("GroundTilemap");
        tilemapObject.transform.SetParent(gridObject.transform, false);
        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();
        GroundTilemapGenerator generator = tilemapObject.AddComponent<GroundTilemapGenerator>();
        generator.Configure(bounds, tile, tilemap, grid);
        generator.Rebuild();
        return generator;
    }

    private static void CreateBoundaries(Transform parent, Rect bounds, GameObject wallPrefab)
    {
        GameObject group = new GameObject("Boundaries");
        group.transform.SetParent(parent, false);
        const float thickness = 4f;

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

    private static void CreateBoundaryArt(Transform parent, Rect bounds, Dictionary<string, GameObject> prefabs)
    {
        GameObject group = new GameObject("BoundaryArt");
        group.transform.SetParent(parent, false);

        PlaceVisual(group.transform, prefabs["Mountain_01"], "NorthMountain",
            new Vector2(bounds.center.x, bounds.yMax + 18f),
            new Vector2(bounds.width, 70f),
            0f,
            false);
        PlaceVisual(group.transform, prefabs["Mountain_02"], "SouthMountain",
            new Vector2(bounds.center.x, bounds.yMin - 18f),
            new Vector2(bounds.width, 64f),
            180f,
            false);
    }

    private static void CreateWater(
        Transform parent,
        LevelSpec spec,
        Dictionary<string, GameObject> prefabs,
        Material bankMaterial,
        Material waterMaterial)
    {
        GameObject group = new GameObject("Water");
        group.transform.SetParent(parent, false);

        if (!string.IsNullOrEmpty(spec.RiverArtKey))
        {
            GameObject river = InstantiatePrefab(prefabs[spec.RiverArtKey], group.transform);
            river.name = "RiverArt";
            SpriteRenderer renderer = river.GetComponent<SpriteRenderer>();
            Vector2 fittedSize = GetAspectFitSize(spec.Bounds.size, renderer.sprite.bounds.size);
            ScaleToWorldSize(river, fittedSize);
            river.transform.localPosition = spec.Bounds.center;
        }
        else
        {
            GameObject riverObject = InstantiatePrefab(prefabs["RiverPath"], group.transform);
            riverObject.name = "RiverPath";
            RiverPath2D river = riverObject.GetComponent<RiverPath2D>();
            LineRenderer bank = riverObject.transform.Find("Bank").GetComponent<LineRenderer>();
            LineRenderer water = riverObject.transform.Find("Water").GetComponent<LineRenderer>();
            river.Configure(spec.RiverPoints, spec.RiverWidth, bankMaterial, waterMaterial, bank, water);
        }

        for (int i = 0; i < spec.Ponds.Length; i++)
        {
            PondPlacement pond = spec.Ponds[i];
            PlaceVisual(group.transform, prefabs[pond.PrefabKey], $"Pond_{i + 1}", pond.Position, pond.Size, 0f, false);
        }
    }

    private static void CreatePlacedObjects(
        Transform parent,
        string groupName,
        PropPlacement[] placements,
        Dictionary<string, GameObject> prefabs)
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent, false);

        for (int i = 0; i < placements.Length; i++)
        {
            PropPlacement placement = placements[i];
            PlaceVisual(
                group.transform,
                prefabs[placement.PrefabKey],
                placement.Name,
                placement.Position,
                placement.Size,
                placement.Rotation,
                true);
        }
    }

    private static GameObject PlaceVisual(
        Transform parent,
        GameObject prefab,
        string name,
        Vector2 position,
        Vector2 size,
        float rotation,
        bool updateYSort)
    {
        GameObject instance = InstantiatePrefab(prefab, parent);
        instance.name = name;
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        ScaleToWorldSize(instance, size);

        if (updateYSort)
        {
            YSort sort = instance.GetComponent<YSort>();
            SpriteRenderer renderer = instance.GetComponent<SpriteRenderer>();
            if (sort != null && renderer != null)
            {
                sort.Configure(renderer.sortingLayerName, renderer.sortingOrder, size.y * 0.5f, false);
            }
        }

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

    private static LeafSpawner CreateLeafSpawner(Transform parent, Rect bounds, GameObject leafPrefab)
    {
        GameObject root = new GameObject("LeafSpawner");
        root.transform.SetParent(parent, false);
        LeafSpawner spawner = root.AddComponent<LeafSpawner>();

        GameObject areaObject = new GameObject("LeafSpawnArea");
        areaObject.transform.SetParent(root.transform, false);
        areaObject.transform.localPosition = bounds.center;
        BoxCollider2D areaCollider = areaObject.AddComponent<BoxCollider2D>();
        areaCollider.isTrigger = true;
        areaCollider.size = new Vector2(
            Mathf.Max(1f, bounds.width - 12f),
            Mathf.Max(1f, bounds.height - 12f));
        LeafSpawnArea area = areaObject.AddComponent<LeafSpawnArea>();
        area.Configure(areaCollider, (1 << ObstacleLayer) | (1 << RiverLayer), 0.65f, 220);

        GameObject leafContainer = new GameObject("Leaves");
        leafContainer.transform.SetParent(root.transform, false);

        spawner.Configure(
            leafPrefab.GetComponent<LeafLifecycle>(),
            new[] { area },
            leafContainer.transform);
        return spawner;
    }

    private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null) throw new InvalidOperationException($"Failed to instantiate prefab {prefab.name}");
        return instance;
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
            new LevelCatalog.Entry { Id = LevelId.ClassicLarge, Prefab = levels[LevelId.ClassicLarge] },
            new LevelCatalog.Entry { Id = LevelId.TimedChallenge, Prefab = levels[LevelId.TimedChallenge] },
            new LevelCatalog.Entry { Id = LevelId.Endless, Prefab = levels[LevelId.Endless] }
        };
        catalog.Configure(entries);
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void ValidateMigration(LevelCatalog catalog, List<string> report)
    {
        List<string> errors = new List<string>();

        for (int i = 0; i < GameplayArtFiles.Length; i++)
        {
            if (LoadGameplaySprite(GameplayArtFiles[i]) == null)
            {
                errors.Add($"Missing imported Sprite: {GameplayArtFiles[i]}");
            }
        }

        LevelId[] ids =
        {
            LevelId.SimpleSmall,
            LevelId.ClassicLarge,
            LevelId.TimedChallenge,
            LevelId.Endless
        };

        for (int i = 0; i < ids.Length; i++)
        {
            LevelRoot prefab = catalog.GetPrefab(ids[i]);
            if (prefab == null)
            {
                errors.Add($"Catalog is missing {ids[i]}");
                continue;
            }

            GroundTilemapGenerator ground = prefab.GetComponentInChildren<GroundTilemapGenerator>(true);
            LeafSpawner spawner = prefab.GetComponentInChildren<LeafSpawner>(true);
            LeafSpawnArea area = prefab.GetComponentInChildren<LeafSpawnArea>(true);
            WindBlower blower = prefab.GetComponentInChildren<WindBlower>(true);
            Tilemap tilemap = prefab.GetComponentInChildren<Tilemap>(true);

            if (ground == null) errors.Add($"{ids[i]} has no GroundTilemapGenerator");
            if (spawner == null) errors.Add($"{ids[i]} has no LeafSpawner");
            if (area == null) errors.Add($"{ids[i]} has no LeafSpawnArea");
            if (blower == null) errors.Add($"{ids[i]} has no WindBlower");
            if (tilemap == null || tilemap.GetUsedTilesCount() == 0) errors.Add($"{ids[i]} has an empty Tilemap");
        }

        report.Add($"Validation errors: {errors.Count}");
        for (int i = 0; i < errors.Count; i++) report.Add("- " + errors[i]);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Migration validation found {errors.Count} error(s). {string.Join(" | ", errors)}");
        }
    }

    private static Sprite LoadGameplaySprite(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(GameplayArtRoot + relativePath);
    }

    private static Texture2D LoadGameplayTexture(string relativePath)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(GameplayArtRoot + relativePath);
    }

    private static Vector2 GetAspectFitSize(Vector2 targetSize, Vector2 spriteSize)
    {
        float spriteAspect = spriteSize.x / Mathf.Max(0.001f, spriteSize.y);
        float width = targetSize.x;
        float height = width / spriteAspect;
        if (height > targetSize.y)
        {
            height = targetSize.y;
            width = height * spriteAspect;
        }

        return new Vector2(width, height);
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
        string reportPath = Path.Combine(reportDirectory, "level-prefab-migration-report.txt");
        File.WriteAllLines(reportPath, lines.ToArray());
        return reportPath;
    }

    private static LevelSpec[] CreateLevelSpecs(TileBase greenTile, TileBase yellowTile)
    {
        return new[]
        {
            CreateSimpleSmallSpec(greenTile),
            CreateClassicLargeSpec(yellowTile),
            CreateTimedChallengeSpec(greenTile),
            CreateEndlessSpec(yellowTile)
        };
    }

    private static LevelSpec CreateSimpleSmallSpec(TileBase groundTile)
    {
        Rect bounds = new Rect(-125f, -125f, 250f, 250f);
        return new LevelSpec
        {
            Id = LevelId.SimpleSmall,
            Bounds = bounds,
            GroundTile = groundTile,
            InitialLeafCount = 160,
            RiverWidth = 58f,
            InitialCameraSize = 34f,
            MinCameraSize = 22f,
            MaxCameraSize = 66f,
            RiverArtKey = "RiverArt_02",
            RiverPoints = new Vector2[0],
            Ponds = new[]
            {
                new PondPlacement("Pond_01", new Vector2(74f, -16f), new Vector2(42f, 22f)),
                new PondPlacement("Pond_02", new Vector2(70f, -72f), new Vector2(42f, 24f)),
                new PondPlacement("Pond_03", new Vector2(25f, -92f), new Vector2(34f, 24f))
            },
            Obstacles = new[]
            {
                new PropPlacement("StoneA", "Stone_01", new Vector2(-96f, -76f), new Vector2(34f, 24f), 35f),
                new PropPlacement("StoneB", "Stone_02", new Vector2(-69f, -62f), new Vector2(34f, 22f), 12f),
                new PropPlacement("StoneC", "Stone_03", new Vector2(-45f, -86f), new Vector2(36f, 24f), -8f)
            },
            Decorations = new[]
            {
                new PropPlacement("ReedA", "Reed_01", new Vector2(91f, -71f), new Vector2(13f, 16f), 0f),
                new PropPlacement("TreeA", "Tree_01", new Vector2(92f, 88f), new Vector2(18f, 17f), 0f),
                new PropPlacement("TreeB", "Tree_02", new Vector2(-95f, 86f), new Vector2(15f, 22f), 0f)
            }
        };
    }

    private static LevelSpec CreateClassicLargeSpec(TileBase groundTile)
    {
        Rect bounds = new Rect(-170f, -135f, 340f, 270f);
        return new LevelSpec
        {
            Id = LevelId.ClassicLarge,
            Bounds = bounds,
            GroundTile = groundTile,
            InitialLeafCount = 260,
            RiverWidth = 28f,
            InitialCameraSize = 42f,
            MinCameraSize = 28f,
            MaxCameraSize = 86f,
            RiverPoints = new[]
            {
                new Vector2(bounds.xMin - 42f, bounds.yMin + 72f),
                new Vector2(bounds.xMin + 42f, bounds.yMin + 55f),
                new Vector2(bounds.xMin + 98f, bounds.yMin + 62f),
                new Vector2(bounds.xMin + 136f, bounds.yMin + 102f),
                new Vector2(bounds.xMin + 202f, bounds.yMin + 116f),
                new Vector2(bounds.xMin + 260f, bounds.yMin + 102f),
                new Vector2(bounds.xMax + 42f, bounds.yMin + 122f)
            },
            Ponds = new[]
            {
                new PondPlacement("Pond_03", new Vector2(-124f, -116f), new Vector2(46f, 28f)),
                new PondPlacement("Pond_01", new Vector2(124f, -104f), new Vector2(56f, 30f))
            },
            Obstacles = new[]
            {
                new PropPlacement("StoneA", "LongStone_02", new Vector2(-132f, 70f), new Vector2(48f, 22f), -8f),
                new PropPlacement("StoneB", "LongStone_03", new Vector2(58f, 76f), new Vector2(52f, 22f), 8f),
                new PropPlacement("StoneC", "Stone_05", new Vector2(142f, 46f), new Vector2(42f, 28f), -10f),
                new PropPlacement("StoneD", "LongStone_01", new Vector2(-28f, 24f), new Vector2(62f, 18f), 5f)
            },
            Decorations = new[]
            {
                new PropPlacement("TreeA", "Tree_03", new Vector2(-138f, 88f), new Vector2(23f, 22f), 0f),
                new PropPlacement("TreeB", "Tree_04", new Vector2(136f, 92f), new Vector2(21f, 24f), 0f),
                new PropPlacement("TreeC", "Tree_05", new Vector2(-130f, -100f), new Vector2(21f, 26f), 0f),
                new PropPlacement("ReedA", "Reed_02", new Vector2(147f, -98f), new Vector2(16f, 18f), 0f),
                new PropPlacement("ReedB", "Reed_01", new Vector2(-96f, -105f), new Vector2(14f, 17f), 0f)
            }
        };
    }

    private static LevelSpec CreateTimedChallengeSpec(TileBase groundTile)
    {
        Rect bounds = new Rect(-125f, -95f, 250f, 190f);
        return new LevelSpec
        {
            Id = LevelId.TimedChallenge,
            Bounds = bounds,
            GroundTile = groundTile,
            InitialLeafCount = 120,
            RiverWidth = 18f,
            InitialCameraSize = 30f,
            MinCameraSize = 18f,
            MaxCameraSize = 58f,
            TimeLimitSeconds = 180f,
            RiverPoints = new[]
            {
                new Vector2(bounds.xMin - 32f, bounds.yMax + 36f),
                new Vector2(bounds.xMin + 18f, bounds.yMax - 8f),
                new Vector2(bounds.xMin + 8f, bounds.yMin + 62f),
                new Vector2(bounds.xMin + 34f, bounds.yMin - 24f),
                new Vector2(bounds.xMin + 112f, bounds.yMin - 20f),
                new Vector2(bounds.xMin + 162f, bounds.yMin + 54f),
                new Vector2(bounds.xMin + 152f, bounds.yMax + 38f)
            },
            Ponds = new PondPlacement[0],
            Obstacles = new[]
            {
                new PropPlacement("StoneA", "LongStone_02", new Vector2(-80f, 54f), new Vector2(54f, 18f), -8f),
                new PropPlacement("StoneB", "LongStone_03", new Vector2(94f, 58f), new Vector2(52f, 18f), 12f),
                new PropPlacement("StoneC", "Stone_10", new Vector2(-18f, -44f), new Vector2(34f, 26f), -12f),
                new PropPlacement("StoneD", "Stone_07", new Vector2(82f, -52f), new Vector2(34f, 24f), 0f)
            },
            Decorations = new[]
            {
                new PropPlacement("TreeA", "Tree_06", new Vector2(-98f, 68f), new Vector2(18f, 22f), 0f),
                new PropPlacement("TreeB", "Tree_07", new Vector2(108f, 70f), new Vector2(20f, 18f), 0f),
                new PropPlacement("ReedA", "Reed_01", new Vector2(112f, 36f), new Vector2(12f, 15f), 0f)
            }
        };
    }

    private static LevelSpec CreateEndlessSpec(TileBase groundTile)
    {
        Rect bounds = new Rect(-150f, -120f, 300f, 240f);
        return new LevelSpec
        {
            Id = LevelId.Endless,
            Bounds = bounds,
            GroundTile = groundTile,
            InitialLeafCount = 130,
            RiverWidth = 22f,
            InitialCameraSize = 38f,
            MinCameraSize = 24f,
            MaxCameraSize = 72f,
            Endless = true,
            EndlessSpawnBatch = 8,
            EndlessSpawnInterval = 1.8f,
            EndlessMaxLeaves = 260,
            RiverPoints = new[]
            {
                new Vector2(bounds.xMin - 42f, bounds.yMax - 26f),
                new Vector2(bounds.xMin + 20f, bounds.yMax - 62f),
                new Vector2(bounds.xMin + 78f, bounds.yMax - 70f),
                new Vector2(bounds.xMin + 118f, bounds.yMax - 36f),
                new Vector2(bounds.xMin + 150f, bounds.yMax - 8f),
                new Vector2(bounds.xMin + 204f, bounds.yMax - 28f),
                new Vector2(bounds.xMax + 42f, bounds.yMax - 56f)
            },
            Ponds = new[]
            {
                new PondPlacement("Pond_01", new Vector2(0f, -4f), new Vector2(58f, 30f))
            },
            Obstacles = new PropPlacement[0],
            Decorations = new[]
            {
                new PropPlacement("TreeA", "Tree_08", new Vector2(-122f, 88f), new Vector2(22f, 20f), 0f),
                new PropPlacement("TreeB", "Tree_09", new Vector2(126f, 82f), new Vector2(24f, 20f), 0f),
                new PropPlacement("ReedA", "Reed_02", new Vector2(22f, 4f), new Vector2(15f, 18f), 0f),
                new PropPlacement("ReedB", "Reed_01", new Vector2(-28f, -6f), new Vector2(14f, 17f), 0f)
            }
        };
    }

    private sealed class LevelSpec
    {
        public LevelId Id;
        public Rect Bounds;
        public TileBase GroundTile;
        public int InitialLeafCount;
        public float RiverWidth;
        public float InitialCameraSize;
        public float MinCameraSize;
        public float MaxCameraSize;
        public float TimeLimitSeconds;
        public bool Endless;
        public int EndlessSpawnBatch;
        public float EndlessSpawnInterval = 1.8f;
        public int EndlessMaxLeaves = 260;
        public string RiverArtKey;
        public Vector2[] RiverPoints = new Vector2[0];
        public PondPlacement[] Ponds = new PondPlacement[0];
        public PropPlacement[] Obstacles = new PropPlacement[0];
        public PropPlacement[] Decorations = new PropPlacement[0];
    }

    private readonly struct PondPlacement
    {
        public readonly string PrefabKey;
        public readonly Vector2 Position;
        public readonly Vector2 Size;

        public PondPlacement(string prefabKey, Vector2 position, Vector2 size)
        {
            PrefabKey = prefabKey;
            Position = position;
            Size = size;
        }
    }

    private readonly struct PropPlacement
    {
        public readonly string Name;
        public readonly string PrefabKey;
        public readonly Vector2 Position;
        public readonly Vector2 Size;
        public readonly float Rotation;

        public PropPlacement(string name, string prefabKey, Vector2 position, Vector2 size, float rotation)
        {
            Name = name;
            PrefabKey = prefabKey;
            Position = position;
            Size = size;
            Rotation = rotation;
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
