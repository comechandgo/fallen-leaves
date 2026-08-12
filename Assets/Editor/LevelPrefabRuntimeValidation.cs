using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class LevelPrefabRuntimeValidation
{
    private const string RunningKey = "FallenLeaves.LevelPrefabRuntimeValidation.Running";
    private const string ReportFileName = "level-prefab-runtime-validation-report.txt";

    private static readonly List<string> report = new List<string>();
    private static readonly List<LevelRoot> unloadedLevels = new List<LevelRoot>();
    private static bool hooksAttached;
    private static bool validationQueued;
    private static bool failed;
    private static int startFrame;
    private static int validationFrame;
    private static int stage;
    private static TiledMapPrototypeImporter.Layout prototype;

    static LevelPrefabRuntimeValidation()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;

        InitializeReport();
        AttachHooks();
        EditorApplication.delayCall += ResumeAfterDomainReload;
    }

    public static void RunBatch()
    {
        SessionState.SetBool(RunningKey, true);
        InitializeReport();
        AttachHooks();

        if (EditorApplication.isPlaying)
        {
            QueueValidation();
        }
        else
        {
            EditorApplication.EnterPlaymode();
        }
    }

    private static void InitializeReport()
    {
        report.Clear();
        unloadedLevels.Clear();
        failed = false;
        stage = 0;
        validationQueued = false;
        prototype = null;
        report.Add($"Fallen Leaves level prefab runtime validation - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.Add($"Unity: {Application.unityVersion}");
        report.Add(string.Empty);
    }

    private static void AttachHooks()
    {
        if (hooksAttached) return;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        hooksAttached = true;
    }

    private static void DetachHooks()
    {
        if (!hooksAttached) return;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.update -= RunValidationUpdate;
        hooksAttached = false;
        validationQueued = false;
    }

    private static void ResumeAfterDomainReload()
    {
        if (EditorApplication.isPlaying) QueueValidation();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
    {
        if (stateChange == PlayModeStateChange.EnteredPlayMode)
        {
            QueueValidation();
        }
    }

    private static void QueueValidation()
    {
        if (validationQueued) return;
        validationQueued = true;
        startFrame = Time.frameCount;
        EditorApplication.update += RunValidationUpdate;
    }

    private static void RunValidationUpdate()
    {
        try
        {
            if (stage == 0)
            {
                if (Time.frameCount <= startFrame) return;
                ValidateAllLevels();
                validationFrame = Time.frameCount;
                stage = 1;
                return;
            }

            if (stage == 1)
            {
                if (Time.frameCount <= validationFrame) return;
                ValidateUnloadCleanup();
                Complete();
            }
        }
        catch (Exception exception)
        {
            Fail($"Unhandled validation exception: {exception}");
            Complete();
        }
    }

    private static void ValidateAllLevels()
    {
        prototype = TiledMapPrototypeImporter.LoadAndValidate();
        Check(prototype != null, "Tiled map prototype parses during runtime validation");
        LevelCatalog catalog = Resources.Load<LevelCatalog>("LevelCatalog");
        Check(catalog != null, "Resources/LevelCatalog.asset is loadable");
        if (catalog == null) return;

        ValidateLeafPrefab(catalog.GetPrefab(LevelId.SimpleSmall));

        ValidateLevel(catalog, LevelId.SimpleSmall, 160, 0f, false);
        ValidateLevel(catalog, LevelId.ClassicLarge, 260, 0f, false);
        ValidateLevel(catalog, LevelId.TimedChallenge, 120, 180f, false);
        ValidateLevel(catalog, LevelId.Endless, 130, 0f, true);

        LevelLoader.Unload();
        Check(LevelLoader.Current == null, "LevelLoader clears Current immediately on unload");
        ValidateGameFlow();
    }

    private static void ValidateLeafPrefab(LevelRoot simplePrefab)
    {
        Check(simplePrefab != null, "SimpleSmall catalog prefab exists");
        if (simplePrefab == null) return;

        LeafSpawner spawner = simplePrefab.GetComponentInChildren<LeafSpawner>(true);
        SerializedObject serializedSpawner = spawner != null ? new SerializedObject(spawner) : null;
        SerializedProperty leafPrefabProperty = serializedSpawner?.FindProperty("leafPrefab");
        LeafLifecycle leafPrefab = leafPrefabProperty?.objectReferenceValue as LeafLifecycle;

        Check(leafPrefab != null, "LeafSpawner references Leaf.prefab");
        if (leafPrefab == null) return;

        Check(leafPrefab.GetComponent<SpriteRenderer>() != null, "Leaf.prefab has SpriteRenderer");
        Check(leafPrefab.GetComponent<Rigidbody2D>() != null, "Leaf.prefab has Rigidbody2D");
        Check(leafPrefab.GetComponent<Collider2D>() != null, "Leaf.prefab has Collider2D");
        Check(leafPrefab.GetComponent<Windable>() != null, "Leaf.prefab has Windable");
        Check(leafPrefab.GetComponent<YSort>() != null, "Leaf.prefab has YSort");
        Check(leafPrefab.GetComponent<LeafAppearance>() != null, "Leaf.prefab has LeafAppearance");

        LeafAppearance appearance = leafPrefab.GetComponent<LeafAppearance>();
        SerializedObject serializedAppearance = new SerializedObject(appearance);
        SerializedProperty sprites = serializedAppearance.FindProperty("sprites");
        Check(sprites != null && sprites.arraySize == 4, "Leaf.prefab references four leaf sprites");
        SerializedProperty widthRange = serializedAppearance.FindProperty("widthRange");
        SerializedProperty heightRange = serializedAppearance.FindProperty("heightRange");
        Check(widthRange != null && Vector2.Distance(widthRange.vector2Value, new Vector2(0.66f, 0.92f)) < 0.0001f,
            "Leaf.prefab width range is scaled to one tenth");
        Check(heightRange != null && Vector2.Distance(heightRange.vector2Value, new Vector2(0.56f, 0.84f)) < 0.0001f,
            "Leaf.prefab height range is scaled to one tenth");
    }

    private static void ValidateLevel(
        LevelCatalog catalog,
        LevelId id,
        int expectedInitialLeaves,
        float expectedTimeLimit,
        bool expectedEndless)
    {
        LevelRoot prefab = catalog.GetPrefab(id);
        Check(prefab != null, $"{id}: catalog prefab exists");
        if (prefab == null) return;

        RiverCollector.ResetSession();
        LevelRoot root = LevelLoader.Load(id);
        Check(root != null, $"{id}: LevelLoader instantiated the prefab");
        if (root == null) return;

        unloadedLevels.Add(root);
        Check(root.Id == id, $"{id}: instantiated LevelId matches catalog key");
        Check(root.InitialLeafCount == expectedInitialLeaves, $"{id}: initial leaf setting is {expectedInitialLeaves}");
        Check(Mathf.Approximately(root.TimeLimitSeconds, expectedTimeLimit), $"{id}: time limit is {expectedTimeLimit:0.##} seconds");
        Check(root.Endless == expectedEndless, $"{id}: endless rule matches expected value");
        Check(Physics2D.gravity == Vector2.zero, $"{id}: runtime 2D gravity is disabled");
        Check(root.MapBounds == prototype.Bounds, $"{id}: uses the 120x90 TMJ bounds");
        Check(Vector2.Distance(root.CameraStart, prototype.CameraStart) < 0.01f, $"{id}: stores the TMJ CameraStart");

        MapPrototypeGizmos metadata = root.GetComponent<MapPrototypeGizmos>();
        Check(metadata != null && metadata.SourceSha256 == prototype.SourceSha256,
            $"{id}: records the authoritative TMJ SHA-256");
        Check(root.transform.Find("BoundaryArt") == null, $"{id}: contains no old mountain boundary art");
        Check(root.transform.Find("LeafSpawner/LeafSpawnArea") == null, $"{id}: contains no fixed LeafSpawnArea");
        ValidateGroup(root.transform, "Obstacles", prototype.Obstacles.Length, id);
        ValidateGroup(root.transform, "Decorations", prototype.Decorations.Length, id);
        ValidateGroup(root.transform, "Landmarks", prototype.Landmarks.Length, id);

        WindBlower wind = root.WindBlower;
        Check(wind != null && Vector2.Distance(wind.transform.position, prototype.WindStart) < 0.01f,
            $"{id}: WindBlower starts at the TMJ WindStart");
        Check(wind != null && Mathf.Approximately(wind.Radius, 1f),
            $"{id}: initial WindBlower radius is scaled to one tenth");

        ValidateGround(root, id);
        ValidateWater(root, id);
        ValidateCamera(root, id);
        ValidateSpawnedLeaves(root, id, expectedInitialLeaves);

        if (id == LevelId.ClassicLarge)
        {
            ValidateRiverCollection(root, id);
        }

        if (expectedEndless)
        {
            ValidateEndlessRules(root, id);
        }
        else
        {
            ClearRegisteredLeaves(root);
            Check(root.ActiveLeafCount == 0, $"{id}: clearing leaves reduces the level counter to zero");
            Check(root.IsGameplayClear, $"{id}: non-endless level reports completion at zero leaves");
        }

        int activeRoots = UnityEngine.Object.FindObjectsByType<LevelRoot>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None).Length;
        Check(activeRoots == 1, $"{id}: only one active level root exists after loading");
    }

    private static void ValidateGround(LevelRoot root, LevelId id)
    {
        GroundTilemapGenerator generator = root.GetComponentInChildren<GroundTilemapGenerator>(true);
        Tilemap tilemap = root.GetComponentInChildren<Tilemap>(true);
        Check(generator != null, $"{id}: GroundTilemapGenerator exists");
        Check(tilemap != null && tilemap.GetUsedTilesCount() > 0, $"{id}: ground Tilemap contains generated tiles");
        if (generator == null || tilemap == null) return;

        Check(generator.GreenTile != null && generator.GreenTile.name == "GroundGreen",
            $"{id}: mixed ground references GroundGreen");
        Check(generator.YellowTile != null && generator.YellowTile.name == "GroundYellow",
            $"{id}: mixed ground references GroundYellow");
        generator.CountTiles(out int greenCount, out int yellowCount);
        Check(greenCount > 0 && yellowCount > 0 && Mathf.Abs(greenCount - yellowCount) <= 1,
            $"{id}: ground is 50/50 green and yellow ({greenCount}/{yellowCount})");

        float overallGreen = greenCount / (float)Mathf.Max(1, greenCount + yellowCount);
        for (int i = 0; i < prototype.Regions.Length; i++)
        {
            TiledMapPrototypeImporter.Region region = prototype.Regions[i];
            float regionGreen = generator.GetGreenRatio(region.Bounds);
            if (region.RegionId == "Meadow")
                Check(regionGreen < overallGreen, $"{id}: Meadow is yellow-biased ({regionGreen:P0})");
            else
                Check(regionGreen > overallGreen, $"{id}: {region.RegionId} is green-biased ({regionGreen:P0})");
        }

        Bounds localBounds = tilemap.localBounds;
        Vector3 worldMin = tilemap.transform.TransformPoint(localBounds.min);
        Vector3 worldMax = tilemap.transform.TransformPoint(localBounds.max);
        Rect map = root.MapBounds;
        bool covers = worldMin.x <= map.xMin + 0.01f
            && worldMin.y <= map.yMin + 0.01f
            && worldMax.x >= map.xMax - 0.01f
            && worldMax.y >= map.yMax - 0.01f;
        Check(covers, $"{id}: generated ground covers the full map bounds");
    }

    private static void ValidateWater(LevelRoot root, LevelId id)
    {
        RiverImagePiece[] pieces = root.GetComponentsInChildren<RiverImagePiece>(true);
        Check(pieces.Length == 6, $"{id}: contains exactly six river image pieces");
        if (pieces.Length != 6) return;

        int art01 = 0;
        int art02 = 0;
        int art03 = 0;
        bool componentsCorrect = true;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (pieces[i].name.Contains("RiverArt_01")) art01++;
            if (pieces[i].name.Contains("RiverArt_02")) art02++;
            if (pieces[i].name.Contains("RiverArt_03")) art03++;
            if (pieces[i].GetComponent<RiverWaterMask>() == null
                || pieces[i].GetComponent<RiverCollector>() == null
                || pieces[i].GetComponent<Collider2D>() == null
                || pieces[i].GetComponent<RiverFlowOverlay>() == null)
                componentsCorrect = false;

            if (i > 0)
                Check(Vector2.Distance(pieces[i - 1].WorldExit, pieces[i].WorldEntry) <= 1.1f,
                    $"{id}: river pieces {i} and {i + 1} overlap without a route gap");
        }

        Check(art01 == 2 && art02 == 2 && art03 == 2,
            $"{id}: uses each RiverArt image exactly twice");
        Check(componentsCorrect, $"{id}: every river image piece has mask, collider, flow, and collector");
        Check(Vector2.Distance(pieces[0].WorldEntry, prototype.RiverPoints[0]) < 0.05f,
            $"{id}: river begins at the TMJ southwest endpoint");
        Check(Vector2.Distance(pieces[pieces.Length - 1].WorldExit, prototype.RiverPoints[prototype.RiverPoints.Length - 1]) < 0.05f,
            $"{id}: river ends at the TMJ northeast endpoint");

        Transform lake = root.transform.Find("Water/Lake_" + prototype.Lake.Name);
        Check(lake != null && Vector2.Distance(lake.position, prototype.Lake.Position) < 0.01f,
            $"{id}: Pond_01 lake is centered at the TMJ lake position");
        PolygonCollider2D lakeCollider = lake != null ? lake.GetComponent<PolygonCollider2D>() : null;
        Check(lakeCollider != null && lakeCollider.isTrigger, $"{id}: lake has an independent ellipse collection trigger");
        if (lakeCollider != null)
        {
            Check(Mathf.Abs(lakeCollider.bounds.size.x - prototype.Lake.Size.x) < 0.05f
                && Mathf.Abs(lakeCollider.bounds.size.y - prototype.Lake.Size.y) < 0.05f,
                $"{id}: lake trigger matches the TMJ 18x11 metre ellipse");
        }

        Transform oldTree = root.transform.Find("Landmarks/OldTree");
        if (id == LevelId.ClassicLarge)
        {
            Check(oldTree != null && root.MapBounds.Contains(oldTree.position),
                $"{id}: manually adjusted OldTree landmark remains inside the map");
        }
        else
        {
            Check(oldTree != null && Vector2.Distance(oldTree.position, new Vector2(48f, 34f)) < 0.01f,
                $"{id}: OldTree landmark is centered at (48, 34)");
        }

        RiverCollector[] collectors = root.GetComponentsInChildren<RiverCollector>(true);
        bool layersCorrect = true;
        int riverLayer = LayerMask.NameToLayer("River");
        for (int i = 0; i < collectors.Length; i++)
        {
            if (collectors[i].enabled && collectors[i].gameObject.layer != riverLayer)
            {
                layersCorrect = false;
                break;
            }
        }
        Check(layersCorrect, $"{id}: enabled water collectors use the River layer");
    }

    private static void ValidateCamera(LevelRoot root, LevelId id)
    {
        Camera camera = Camera.main;
        Check(camera != null && camera.orthographic, $"{id}: orthographic main camera is configured");
        if (camera == null) return;

        Check(Mathf.Approximately(camera.orthographicSize, 12f), $"{id}: camera starts at orthographic size 12");

        GameCameraController controller = camera.GetComponent<GameCameraController>();
        Check(controller != null, $"{id}: camera bounds controller is present");
        if (controller != null)
        {
            Check(Mathf.Approximately(GetPrivateField<float>(controller, "minSize"), 5f),
                $"{id}: camera minimum orthographic size is 5");
            Check(Mathf.Approximately(GetPrivateField<float>(controller, "maxSize"), 20f),
                $"{id}: camera maximum orthographic size is 20");
        }

        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        Vector2 expected = new Vector2(
            Mathf.Clamp(root.CameraStart.x, root.MapBounds.xMin + halfWidth, root.MapBounds.xMax - halfWidth),
            Mathf.Clamp(root.CameraStart.y, root.MapBounds.yMin + halfHeight, root.MapBounds.yMax - halfHeight));
        Vector2 cameraCenter = camera.transform.position;
        Check(Vector2.Distance(cameraCenter, expected) < 0.05f, $"{id}: camera starts at the clamped TMJ CameraStart");
        const float landscapeAspect = 16f / 9f;
        Check(20f * 2f * landscapeAspect < root.MapBounds.width
            && 20f * 2f < root.MapBounds.height,
            $"{id}: 16:9 maximum zoom cannot reveal the full 120x90 map");
    }

    private static void ValidateSpawnedLeaves(LevelRoot root, LevelId id, int expectedCount)
    {
        LeafLifecycle[] leaves = root.GetComponentsInChildren<LeafLifecycle>(true);
        Check(root.ActiveLeafCount == expectedCount, $"{id}: registered {expectedCount} active leaves");
        Check(leaves.Length == expectedCount, $"{id}: instantiated {expectedCount} Leaf.prefab instances");

        int leafLayer = LayerMask.NameToLayer("Leaf");
        bool componentsCorrect = true;
        bool sizesCorrect = true;
        int outsideCount = 0;
        int blockedCount = 0;

        for (int i = 0; i < leaves.Length; i++)
        {
            LeafLifecycle leaf = leaves[i];
            if (leaf.gameObject.layer != leafLayer
                || leaf.GetComponent<SpriteRenderer>() == null
                || leaf.GetComponent<Rigidbody2D>() == null
                || leaf.GetComponent<Collider2D>() == null
                || leaf.GetComponent<Windable>() == null
                || leaf.GetComponent<YSort>() == null)
            {
                componentsCorrect = false;
            }

            SpriteRenderer renderer = leaf.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
            {
                sizesCorrect = false;
            }
            else
            {
                Vector3 scale = leaf.transform.lossyScale;
                Vector2 spriteSize = renderer.sprite.bounds.size;
                float width = spriteSize.x * Mathf.Abs(scale.x);
                float height = spriteSize.y * Mathf.Abs(scale.y);
                if (width < 0.659f || width > 0.921f || height < 0.559f || height > 0.841f)
                {
                    sizesCorrect = false;
                }
            }

            Vector2 position = leaf.transform.position;
            Rect safeBounds = new Rect(
                root.MapBounds.xMin + 1f,
                root.MapBounds.yMin + 1f,
                root.MapBounds.width - 2f,
                root.MapBounds.height - 2f);
            if (!safeBounds.Contains(position)) outsideCount++;
            if (IsBlockedSpawnPosition(position)) blockedCount++;
        }

        Check(componentsCorrect, $"{id}: every spawned leaf preserves the prefab component set and Leaf layer");
        Check(sizesCorrect, $"{id}: every spawned leaf uses the one-tenth world-size range");
        Check(outsideCount == 0, $"{id}: all sampled leaves keep one metre from map boundaries");
        Check(blockedCount == 0, $"{id}: sampled leaves keep one metre from obstacles and water");

        if (id == LevelId.SimpleSmall && leaves.Length > 0)
        {
            int before = root.ActiveLeafCount;
            leaves[0].MarkCollected();
            leaves[0].MarkCollected();
            Check(root.ActiveLeafCount == before - 1, $"{id}: duplicate leaf unregistration only decrements once");
        }
    }

    private static bool IsBlockedSpawnPosition(Vector2 position)
    {
        int mask = LayerMask.GetMask("Obstacle", "River");
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, 1f, mask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            RiverWaterMask waterMask = hit.GetComponentInParent<RiverWaterMask>();
            if (waterMask != null && !waterMask.IntersectsCircle(position, 1f)) continue;
            return true;
        }

        return false;
    }

    private static void ValidateRiverCollection(LevelRoot root, LevelId id)
    {
        RiverImagePiece[] pieces = root.GetComponentsInChildren<RiverImagePiece>(true);
        float coinsBefore = RiverCollector.SessionCoins;
        for (int i = 0; i < pieces.Length; i++)
        {
            LeafLifecycle leaf = FindUncollectedLeaf(root);
            RiverCollector collector = pieces[i].GetComponent<RiverCollector>();
            int countBefore = root.ActiveLeafCount;
            bool collected = CollectAtWaterPoint(leaf, collector, FindWaterPoint(pieces[i]));
            Check(collected && root.ActiveLeafCount == countBefore - 1,
                $"{id}: RiverArt piece {i + 1} collects and unregisters exactly one leaf");
        }

        Transform lake = root.transform.Find("Water/Lake_" + prototype.Lake.Name);
        LeafLifecycle lakeLeaf = FindUncollectedLeaf(root);
        RiverCollector lakeCollector = lake != null ? lake.GetComponent<RiverCollector>() : null;
        int lakeCountBefore = root.ActiveLeafCount;
        bool lakeCollected = CollectAtWaterPoint(lakeLeaf, lakeCollector, prototype.Lake.Position);
        Check(lakeCollected && root.ActiveLeafCount == lakeCountBefore - 1,
            $"{id}: Pond_01 lake collects and unregisters exactly one leaf");
        Check(RiverCollector.SessionCoins >= coinsBefore + pieces.Length + 1,
            $"{id}: every water collection awards session coins");
    }

    private static void ValidateEndlessRules(LevelRoot root, LevelId id)
    {
        Check(root.EndlessSpawnBatch == 8, $"{id}: endless batch size is 8");
        Check(Mathf.Approximately(root.EndlessSpawnInterval, 1.8f), $"{id}: endless interval is 1.8 seconds");
        Check(root.EndlessMaxLeaves == 260, $"{id}: endless maximum is 260 leaves");

        int before = root.ActiveLeafCount;
        root.Tick(root.EndlessSpawnInterval);
        Check(root.ActiveLeafCount == before + root.EndlessSpawnBatch, $"{id}: one interval spawns one batch");

        for (int i = 0; i < 40; i++)
        {
            root.Tick(root.EndlessSpawnInterval);
        }

        Check(root.ActiveLeafCount == root.EndlessMaxLeaves, $"{id}: repeated spawning stops at the configured maximum");
        Check(!root.IsGameplayClear, $"{id}: endless mode never auto-completes");
    }

    private static void ValidateGameFlow()
    {
        GameFlowManager flow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        Check(flow != null, "GameBootstrap created the single GameFlowManager");
        if (flow == null) return;

        InvokePrivate(flow, "StartLevel", LevelId.ClassicLarge);
        LevelRoot firstClassic = LevelLoader.Current;
        if (firstClassic != null) unloadedLevels.Add(firstClassic);

        Check(firstClassic != null && firstClassic.Id == LevelId.ClassicLarge,
            "GameFlow starts ClassicLarge through LevelLoader");
        Check(GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.Playing,
            "GameFlow enters Playing state after level selection");
        Check(GetPrivateField<UIRouter>(flow, "router").Current == UIRouter.State.Playing,
            "UIRouter shows the gameplay HUD after level selection");
        if (firstClassic == null) return;

        ValidateWindBlow(firstClassic);
        AwardUpgradeCoins(firstClassic, 40);

        int[] levels = GetPrivateField<int[]>(flow, "upgradeLevels");
        for (int i = 0; i < UpgradeCatalog.All.Length; i++)
        {
            InvokePrivate(flow, "TryBuyUpgrade", UpgradeCatalog.All[i]);
        }

        bool allUpgraded = true;
        for (int i = 0; i < UpgradeCatalog.All.Length; i++)
        {
            if (levels[i] != 1)
            {
                allUpgraded = false;
                break;
            }
        }
        Check(allUpgraded, "Shop purchase path upgrades all four upgrade categories");

        WindBlower blower = firstClassic.WindBlower;
        Check(blower != null
            && Mathf.Approximately(blower.BaseWind, 2f)
            && Mathf.Approximately(blower.Radius, 1.5f)
            && blower.MaxTargetsPerBlow == 20,
            "Wind upgrades are applied to the active level prefab instance");
        ValidateUpgradedLeafValue(firstClassic);

        InvokePrivate(flow, "ToggleShop");
        ShopUI shop = GetPrivateField<ShopUI>(flow, "shop");
        Check(flow.ShopOpen && shop.IsVisible, "Shop toggle opens the shop overlay");

        InvokePrivate(flow, "ToggleSettings");
        SettingsUI settings = GetPrivateField<SettingsUI>(flow, "settings");
        Check(flow.SettingsOpen && !flow.ShopOpen && settings.IsVisible && !shop.IsVisible,
            "Pause settings and shop overlays remain mutually exclusive");
        InvokePrivate(flow, "Update");
        Check(Mathf.Approximately(Time.timeScale, 0f), "Pause settings stop gameplay time");

        InvokePrivate(flow, "ToggleSettings");
        InvokePrivate(flow, "Update");
        Check(!flow.SettingsOpen && Mathf.Approximately(Time.timeScale, 1f), "Closing settings resumes gameplay time");

        InvokePrivate(flow, "EndGame", true);
        Check(GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.Result,
            "Successful completion enters Result state");
        Check(GetPrivateField<UIRouter>(flow, "router").Current == UIRouter.State.Result,
            "UIRouter shows the result panel");

        InvokePrivate(flow, "StartLevel", LevelId.ClassicLarge);
        LevelRoot replayedClassic = LevelLoader.Current;
        if (replayedClassic != null) unloadedLevels.Add(replayedClassic);
        Check(replayedClassic != null && replayedClassic != firstClassic,
            "Replay replaces the old level with a fresh prefab instance");
        Check(replayedClassic != null
            && replayedClassic.WindBlower != null
            && Mathf.Approximately(replayedClassic.WindBlower.BaseWind, 2f)
            && Mathf.Approximately(replayedClassic.WindBlower.Radius, 1.5f),
            "Purchased upgrades persist across replay and apply to the new WindBlower");

        if (replayedClassic != null)
        {
            ClearRegisteredLeaves(replayedClassic);
            InvokePrivate(flow, "Update");
            Check(GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.Result
                && GetPrivateField<bool>(flow, "resultSucceeded"),
                "GameFlow detects zero registered leaves and completes the level successfully");
        }

        InvokePrivate(flow, "ReturnToLevelSelect");
        Check(LevelLoader.Current == null
            && GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.LevelSelect
            && GetPrivateField<UIRouter>(flow, "router").Current == UIRouter.State.LevelSelect,
            "Returning to level select unloads gameplay and switches UI state");

        InvokePrivate(flow, "StartLevel", LevelId.TimedChallenge);
        LevelRoot timed = LevelLoader.Current;
        if (timed != null) unloadedLevels.Add(timed);
        SetPrivateField(flow, "elapsedTime", 180f);
        InvokePrivate(flow, "Update");
        Check(GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.Result
            && !GetPrivateField<bool>(flow, "resultSucceeded"),
            "TimedChallenge fails through GameFlow after 180 seconds");

        InvokePrivate(flow, "ReturnToMainMenu");
        Check(LevelLoader.Current == null
            && GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.MainMenu
            && GetPrivateField<UIRouter>(flow, "router").Current == UIRouter.State.MainMenu,
            "Returning to main menu unloads gameplay and restores main UI state");
    }

    private static void ValidateWindBlow(LevelRoot root)
    {
        WindBlower blower = root.WindBlower;
        Windable leaf = root.GetComponentInChildren<Windable>(true);
        Check(blower != null && leaf != null, "Wind validation has an active WindBlower and leaf");
        if (blower == null || leaf == null) return;

        Rigidbody2D[] bodies = root.GetComponentsInChildren<Rigidbody2D>(true);
        InvokePrivate(blower, "Blow", leaf.Position - Vector2.right * (blower.Radius * 0.5f));

        bool pushed = false;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].velocity.sqrMagnitude > 0.0001f)
            {
                pushed = true;
                break;
            }
        }
        Check(pushed, "WindBlower still applies impulse to spawned Leaf.prefab instances");
    }

    private static void AwardUpgradeCoins(LevelRoot root, int count)
    {
        RiverImagePiece piece = root.GetComponentInChildren<RiverImagePiece>(true);
        RiverCollector collector = piece != null ? piece.GetComponent<RiverCollector>() : null;
        LeafLifecycle[] leaves = root.GetComponentsInChildren<LeafLifecycle>(true);
        Check(collector != null && leaves.Length >= count, "Coin setup has enough leaves and a river collector");
        if (collector == null) return;

        Vector2 waterPoint = FindWaterPoint(piece);

        int collected = 0;
        for (int i = 0; i < leaves.Length && collected < count; i++)
        {
            Windable windable = leaves[i].GetComponent<Windable>();
            if (windable == null || windable.IsCollected) continue;
            if (CollectAtWaterPoint(leaves[i], collector, waterPoint)) collected++;
        }

        Check(collected == count && RiverCollector.CoinCount >= 32f,
            "River collection supplies enough coins for one level of every upgrade");
    }

    private static void ValidateUpgradedLeafValue(LevelRoot root)
    {
        RiverImagePiece piece = root.GetComponentInChildren<RiverImagePiece>(true);
        RiverCollector collector = piece != null ? piece.GetComponent<RiverCollector>() : null;
        LeafLifecycle[] leaves = root.GetComponentsInChildren<LeafLifecycle>(true);
        LeafLifecycle uncollected = null;

        for (int i = 0; i < leaves.Length; i++)
        {
            Windable windable = leaves[i].GetComponent<Windable>();
            if (windable != null && !windable.IsCollected)
            {
                uncollected = leaves[i];
                break;
            }
        }

        Check(collector != null && uncollected != null, "Leaf value validation has an uncollected leaf");
        if (collector == null || uncollected == null) return;

        float before = RiverCollector.SessionCoins;
        CollectAtWaterPoint(uncollected, collector, FindWaterPoint(piece));
        Check(Mathf.Approximately(RiverCollector.SessionCoins - before, 1.5f),
            "Leaf value upgrade changes subsequent collection rewards");
    }

    private static void ValidateGroup(Transform root, string groupName, int expectedCount, LevelId id)
    {
        Transform group = root.Find(groupName);
        Check(group != null && group.childCount == expectedCount,
            $"{id}: {groupName} contains {expectedCount} TMJ objects");
    }

    private static LeafLifecycle FindUncollectedLeaf(LevelRoot root)
    {
        LeafLifecycle[] leaves = root.GetComponentsInChildren<LeafLifecycle>(true);
        for (int i = 0; i < leaves.Length; i++)
        {
            Windable windable = leaves[i].GetComponent<Windable>();
            if (windable != null && !windable.IsCollected) return leaves[i];
        }
        return null;
    }

    private static bool CollectAtWaterPoint(LeafLifecycle leaf, RiverCollector collector, Vector2 waterPoint)
    {
        if (leaf == null || collector == null) return false;
        Rigidbody2D body = leaf.GetComponent<Rigidbody2D>();
        if (body != null) body.position = waterPoint;
        leaf.transform.position = waterPoint;
        Physics2D.SyncTransforms();
        Windable windable = leaf.GetComponent<Windable>();
        collector.SendMessage("OnTriggerEnter2D", leaf.GetComponent<Collider2D>(), SendMessageOptions.RequireReceiver);
        return windable != null && windable.IsCollected;
    }

    private static Vector2 FindWaterPoint(RiverImagePiece piece)
    {
        if (piece == null) return Vector2.zero;
        RiverWaterMask mask = piece.GetComponent<RiverWaterMask>();
        SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
        if (mask == null || renderer == null) return (piece.WorldEntry + piece.WorldExit) * 0.5f;

        Bounds bounds = renderer.bounds;
        const int steps = 24;
        for (int y = 0; y <= steps; y++)
        {
            for (int x = 0; x <= steps; x++)
            {
                Vector2 sample = new Vector2(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, x / (float)steps),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, y / (float)steps));
                if (mask.ContainsWater(sample)) return sample;
            }
        }

        return (piece.WorldEntry + piece.WorldExit) * 0.5f;
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(target.GetType().Name, methodName);
        return method.Invoke(target, arguments);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void ClearRegisteredLeaves(LevelRoot root)
    {
        LeafLifecycle[] leaves = root.GetComponentsInChildren<LeafLifecycle>(true);
        for (int i = 0; i < leaves.Length; i++)
        {
            leaves[i].MarkCollected();
        }
    }

    private static void ValidateUnloadCleanup()
    {
        Check(LevelLoader.Current == null, "No current level remains one frame after unload");

        bool allDestroyed = true;
        for (int i = 0; i < unloadedLevels.Count; i++)
        {
            if (unloadedLevels[i] != null)
            {
                allDestroyed = false;
                break;
            }
        }
        Check(allDestroyed, "All previously loaded level roots are destroyed after switching/unloading");

        int levelRoots = UnityEngine.Object.FindObjectsByType<LevelRoot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length;
        int leaves = UnityEngine.Object.FindObjectsByType<LeafLifecycle>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None).Length;
        Check(levelRoots == 0, "No active or inactive LevelRoot objects remain after unload");
        Check(leaves == 0, "No active or inactive spawned leaves remain after unload");
    }

    private static void Check(bool condition, string description)
    {
        if (condition)
        {
            report.Add($"PASS: {description}");
            return;
        }

        Fail(description);
    }

    private static void Fail(string description)
    {
        failed = true;
        report.Add($"FAIL: {description}");
        Debug.LogError($"Level prefab runtime validation failed: {description}");
    }

    private static void Complete()
    {
        report.Add(string.Empty);
        report.Add(failed ? "RESULT: FAILED" : "RESULT: SUCCESS");

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string reportDirectory = Path.GetFullPath(Path.Combine(projectRoot, "..", "logs"));
        Directory.CreateDirectory(reportDirectory);
        string reportPath = Path.Combine(reportDirectory, ReportFileName);
        File.WriteAllLines(reportPath, report);

        Debug.Log($"Level prefab runtime validation completed. Report: {reportPath}");
        SessionState.EraseBool(RunningKey);
        DetachHooks();

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(failed ? 1 : 0);
        }
        else
        {
            EditorApplication.ExitPlaymode();
        }
    }
}
