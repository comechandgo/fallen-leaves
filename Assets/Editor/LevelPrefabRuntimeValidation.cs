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

        bool expectsGreen = id == LevelId.SimpleSmall || id == LevelId.TimedChallenge;
        string expectedTileName = expectsGreen ? "GroundGreen" : "GroundYellow";
        Check(generator.GroundTile != null && generator.GroundTile.name == expectedTileName,
            $"{id}: ground uses {expectedTileName}");

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
        RiverPath2D path = root.GetComponentInChildren<RiverPath2D>(true);
        RiverWaterMask artMask = root.GetComponentInChildren<RiverWaterMask>(true);

        if (id == LevelId.SimpleSmall)
        {
            Check(path == null, $"{id}: uses authored river art instead of RiverPath2D");
            Check(artMask != null, $"{id}: river art has RiverWaterMask");
            Check(root.GetComponentInChildren<RiverFlowOverlay>(true) != null,
                $"{id}: river art preserves the reusable flow overlay component");
            Check(root.GetComponentsInChildren<WaterFlowLine>(true).Length == 28,
                $"{id}: river art generated all 28 animated flow lines");
            return;
        }

        Check(path != null, $"{id}: RiverPath2D exists");
        if (path == null) return;

        RiverCollector[] collectors = path.GetComponentsInChildren<RiverCollector>(true);
        Check(path.ControlPointCount >= 2, $"{id}: RiverPath2D stores local control points");
        Check(collectors.Length > 0, $"{id}: RiverPath2D generated runtime collection triggers");
        Check(path.GetComponentInChildren<WaterFlow>(true) != null,
            $"{id}: RiverPath2D water preserves UV flow animation");

        bool layersCorrect = true;
        int riverLayer = LayerMask.NameToLayer("River");
        for (int i = 0; i < collectors.Length; i++)
        {
            if (collectors[i].gameObject.layer != riverLayer)
            {
                layersCorrect = false;
                break;
            }
        }
        Check(layersCorrect, $"{id}: generated river triggers use the River layer");
    }

    private static void ValidateCamera(LevelRoot root, LevelId id)
    {
        Camera camera = Camera.main;
        Check(camera != null && camera.orthographic, $"{id}: orthographic main camera is configured");
        if (camera == null) return;

        Vector2 cameraCenter = camera.transform.position;
        Check(Vector2.Distance(cameraCenter, root.MapBounds.center) < 0.01f, $"{id}: camera starts at map center");
        Check(camera.GetComponent<GameCameraController>() != null, $"{id}: camera bounds controller is present");
    }

    private static void ValidateSpawnedLeaves(LevelRoot root, LevelId id, int expectedCount)
    {
        LeafLifecycle[] leaves = root.GetComponentsInChildren<LeafLifecycle>(true);
        Check(root.ActiveLeafCount == expectedCount, $"{id}: registered {expectedCount} active leaves");
        Check(leaves.Length == expectedCount, $"{id}: instantiated {expectedCount} Leaf.prefab instances");

        int leafLayer = LayerMask.NameToLayer("Leaf");
        bool componentsCorrect = true;
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

            Vector2 position = leaf.transform.position;
            if (!root.MapBounds.Contains(position)) outsideCount++;
            if (IsBlockedSpawnPosition(position)) blockedCount++;
        }

        Check(componentsCorrect, $"{id}: every spawned leaf preserves the prefab component set and Leaf layer");
        Check(outsideCount == 0, $"{id}: all sampled leaf positions are inside map bounds");
        Check(blockedCount == 0, $"{id}: sampled leaf positions avoid Obstacle and River layers");

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
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, 0.65f, mask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            RiverWaterMask waterMask = hit.GetComponentInParent<RiverWaterMask>();
            if (waterMask != null && !waterMask.ContainsWater(position)) continue;
            return true;
        }

        return false;
    }

    private static void ValidateRiverCollection(LevelRoot root, LevelId id)
    {
        RiverPath2D path = root.GetComponentInChildren<RiverPath2D>(true);
        RiverCollector collector = path != null ? path.GetComponentInChildren<RiverCollector>(true) : null;
        LeafLifecycle leaf = root.GetComponentInChildren<LeafLifecycle>(true);
        Check(collector != null && leaf != null, $"{id}: collector and leaf exist for collection test");
        if (collector == null || leaf == null) return;

        int countBefore = root.ActiveLeafCount;
        float coinsBefore = RiverCollector.SessionCoins;
        collector.SendMessage("OnTriggerEnter2D", leaf.GetComponent<Collider2D>(), SendMessageOptions.RequireReceiver);

        Check(root.ActiveLeafCount == countBefore - 1, $"{id}: river collection unregisters exactly one leaf");
        Check(RiverCollector.SessionCoins > coinsBefore, $"{id}: river collection still awards session coins");
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
            && Mathf.Approximately(blower.Radius, 15f)
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
            && Mathf.Approximately(replayedClassic.WindBlower.BaseWind, 2f),
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
        InvokePrivate(blower, "Blow", leaf.Position - Vector2.right);

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
        RiverPath2D path = root.GetComponentInChildren<RiverPath2D>(true);
        RiverCollector collector = path != null ? path.GetComponentInChildren<RiverCollector>(true) : null;
        LeafLifecycle[] leaves = root.GetComponentsInChildren<LeafLifecycle>(true);
        Check(collector != null && leaves.Length >= count, "Coin setup has enough leaves and a river collector");
        if (collector == null) return;

        int collected = 0;
        for (int i = 0; i < leaves.Length && collected < count; i++)
        {
            Windable windable = leaves[i].GetComponent<Windable>();
            if (windable == null || windable.IsCollected) continue;
            collector.SendMessage("OnTriggerEnter2D", leaves[i].GetComponent<Collider2D>(), SendMessageOptions.RequireReceiver);
            collected++;
        }

        Check(collected == count && RiverCollector.CoinCount >= 32f,
            "River collection supplies enough coins for one level of every upgrade");
    }

    private static void ValidateUpgradedLeafValue(LevelRoot root)
    {
        RiverPath2D path = root.GetComponentInChildren<RiverPath2D>(true);
        RiverCollector collector = path != null ? path.GetComponentInChildren<RiverCollector>(true) : null;
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
        collector.SendMessage("OnTriggerEnter2D", uncollected.GetComponent<Collider2D>(), SendMessageOptions.RequireReceiver);
        Check(Mathf.Approximately(RiverCollector.SessionCoins - before, 1.5f),
            "Leaf value upgrade changes subsequent collection rewards");
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
