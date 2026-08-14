using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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

        SerializedProperty entries = new SerializedObject(catalog).FindProperty("entries");
        Check(entries != null && entries.arraySize == 3, "LevelCatalog contains exactly three modes");
        Check(catalog.GetPrefab((LevelId)1) == null, "Retired serialized LevelId 1 is not registered");

        ValidateTreeCursorFadeAssets();
        ValidateLeafPrefab(catalog.GetPrefab(LevelId.SimpleSmall));

        ValidateLevel(catalog, LevelId.SimpleSmall, 2560, 0f, false);
        ValidateLevel(catalog, LevelId.TimedChallenge, 1920, 180f, false);
        ValidateLevel(catalog, LevelId.Endless, 2080, 0f, true);

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

        Check(leafPrefab.GetComponentInChildren<SpriteRenderer>(true) != null, "Leaf.prefab has SpriteVisual renderer");
        Check(leafPrefab.GetComponent<Rigidbody2D>() != null, "Leaf.prefab has Rigidbody2D");
        Check(leafPrefab.GetComponent<Collider2D>() != null, "Leaf.prefab has Collider2D");
        Check(leafPrefab.GetComponent<Windable>() != null, "Leaf.prefab has Windable");
        Check(leafPrefab.GetComponentInChildren<YSort>(true) != null, "Leaf.prefab SpriteVisual has YSort");
        Check(leafPrefab.GetComponent<LeafAppearance>() != null, "Leaf.prefab has LeafAppearance");
        Check(leafPrefab.GetComponent<LeafWindFeedback>() != null, "Leaf.prefab has directional wind feedback");

        Transform windDeform = leafPrefab.transform.Find("WindDeform");
        Transform spriteVisual = windDeform != null ? windDeform.Find("SpriteVisual") : null;
        Check(windDeform != null && spriteVisual != null, "Leaf.prefab separates physics root and deformable SpriteVisual");
        Check(spriteVisual != null && spriteVisual.GetComponent<SpriteRenderer>() != null,
            "Leaf.prefab keeps the renderer on SpriteVisual");

        Rigidbody2D prefabBody = leafPrefab.GetComponent<Rigidbody2D>();
        Check(prefabBody != null && Mathf.Approximately(prefabBody.drag, 0f),
            "Leaf.prefab disables built-in linear drag in favour of ground damping");

        LeafAppearance appearance = leafPrefab.GetComponent<LeafAppearance>();
        SerializedObject serializedAppearance = new SerializedObject(appearance);
        SerializedProperty sprites = serializedAppearance.FindProperty("sprites");
        Check(sprites != null && sprites.arraySize == 4, "Leaf.prefab references four leaf sprites");
        SerializedProperty widthRange = serializedAppearance.FindProperty("widthRange");
        SerializedProperty heightRange = serializedAppearance.FindProperty("heightRange");
        Check(widthRange != null && Vector2.Distance(widthRange.vector2Value, new Vector2(0.99f, 1.38f)) < 0.0001f,
            "Leaf.prefab width range is scaled up by one and a half");
        Check(heightRange != null && Vector2.Distance(heightRange.vector2Value, new Vector2(0.84f, 1.26f)) < 0.0001f,
            "Leaf.prefab height range is scaled up by one and a half");

        ValidateLeafDamping();
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

        RiverCollector.ResetRun();
        System.Diagnostics.Stopwatch loadTimer = System.Diagnostics.Stopwatch.StartNew();
        LevelRoot root = LevelLoader.Load(id);
        loadTimer.Stop();
        Check(root != null, $"{id}: LevelLoader instantiated the prefab");
        if (root == null) return;
        CompleteInitialSpawn(root);
        report.Add($"INFO: {id} loaded {expectedInitialLeaves} leaves in {loadTimer.ElapsedMilliseconds} ms");

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
        Check(wind != null && Mathf.Approximately(wind.Radius, 2f),
            $"{id}: initial WindBlower radius is doubled to 2 metres");

        ValidateGround(root, id);
        ValidateWater(root, id);
        ValidateTreeCursorFade(root, id);
        ValidateCamera(root, id);
        ValidateSpawnedLeaves(root, id, expectedInitialLeaves);

        if (id == LevelId.SimpleSmall)
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
        Check(pieces.Length == 1, $"{id}: contains exactly one whole-river image instance");
        if (pieces.Length != 1) return;

        RiverImagePiece piece = pieces[0];
        RiverWaterMask mask = piece.GetComponent<RiverWaterMask>();
        SpriteRenderer renderer = piece.GetComponent<SpriteRenderer>();
        BoxCollider2D collider = piece.GetComponent<BoxCollider2D>();
        RiverFlowOverlay flow = piece.GetComponent<RiverFlowOverlay>();
        Check(piece.name == "MainRiver_Whole", $"{id}: whole-river instance has the expected name");
        Check(mask != null
            && piece.GetComponent<RiverCollector>() != null
            && collider != null
            && flow != null,
            $"{id}: whole river has mask, collider, flow, and collector");
        Check(piece.GetComponent<RiverSpriteShapeAdapter>() == null,
            $"{id}: whole river does not use the retired segment seam adapter");
        Check(piece.GetComponent<Collider2D>() != null && collider.isTrigger,
            $"{id}: whole-river BoxCollider2D is a trigger");
        Check(piece.gameObject.layer == LayerMask.NameToLayer("River"),
            $"{id}: whole river uses the River layer");
        Check(renderer != null && renderer.sprite != null && renderer.sprite.texture.isReadable,
            $"{id}: whole river references a readable sprite texture");
        if (renderer != null && renderer.sprite != null)
        {
            Check(renderer.sprite.texture.width == 6240 && renderer.sprite.texture.height == 538,
                $"{id}: whole-river texture retains its full 6240x538 resolution");
        }
        Check(flow != null && flow.LineCount == 36,
            $"{id}: whole river uses the reduced 36-line flow overlay");
        RiverCollector wholeCollector = piece.GetComponent<RiverCollector>();
        if (wholeCollector != null)
        {
            Check(Mathf.Approximately(GetPrivateField<float>(wholeCollector, "collectorMargin"), 0f),
                $"{id}: whole-river collection follows only the true blue mask without a hidden bank margin");
        }
        if (mask != null && renderer != null)
        {
            Check(TryFindMaskPoint(mask, renderer, true, out _),
                $"{id}: whole-river sprite contains blue pixels accepted by the gameplay mask");
            Check(TryFindMaskPoint(mask, renderer, false, out Vector2 nonWaterPoint)
                && !mask.IntersectsCircle(nonWaterPoint, 0f),
                $"{id}: sand, grass, or transparent sprite pixels are rejected by the gameplay mask");
        }
        Check(Vector2.Distance(piece.WorldEntry, prototype.RiverPoints[0]) < 0.1f,
            $"{id}: river begins at the TMJ southwest endpoint");
        Check(Vector2.Distance(piece.WorldExit, prototype.RiverPoints[prototype.RiverPoints.Length - 1]) < 0.1f,
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
        Check(oldTree != null && Vector2.Distance(oldTree.position, new Vector2(48f, 34f)) < 0.01f,
            $"{id}: OldTree landmark is centered at (48, 34)");

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

        Check(Mathf.Approximately(camera.orthographicSize, 10f), $"{id}: camera starts at orthographic size 10");

        GameCameraController controller = camera.GetComponent<GameCameraController>();
        Check(controller != null, $"{id}: camera bounds controller is present");
        Check(camera.GetComponent<TreeCursorFadeController>() != null,
            $"{id}: camera tree cursor fade controller is present");
        if (controller != null)
        {
            Check(Mathf.Approximately(GetPrivateField<float>(controller, "zoomSpeed"), 1f),
                $"{id}: each mouse-wheel notch changes the target size by 1");
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

    private static void ValidateTreeCursorFadeAssets()
    {
        const string materialPath = "Assets/Art/Gameplay/Materials/TreeCursorFade.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Check(material != null, "TreeCursorFade shared material is loadable");
        if (material == null) return;

        Check(material.shader != null && material.shader.name == "FallenLeaves/TreeCursorFade",
            "TreeCursorFade material uses the tree-only cursor fade shader");
        Check(Mathf.Approximately(material.GetFloat("_InnerRadius"), 35f)
            && Mathf.Approximately(material.GetFloat("_OuterRadius"), 60f)
            && Mathf.Approximately(material.GetFloat("_MinimumOpacity"), 0.25f),
            "TreeCursorFade material keeps the 35/60 pixel radii and 25 percent center opacity");

        bool allTreesUseSharedMaterial = true;
        bool allTreesAreDropSources = true;
        for (int i = 1; i <= 9; i++)
        {
            GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/Gameplay/Props/Tree_{i:00}.prefab");
            SpriteRenderer renderer = tree != null ? tree.GetComponent<SpriteRenderer>() : null;
            allTreesUseSharedMaterial &= renderer != null && renderer.sharedMaterial == material;
            LeafDropSource source = tree != null ? tree.GetComponent<LeafDropSource>() : null;
            CapsuleCollider2D trunk = tree != null
                ? tree.transform.Find("TreeTrunkCollider")?.GetComponent<CapsuleCollider2D>()
                : null;
            allTreesAreDropSources &= source != null
                && trunk != null
                && trunk.isTrigger
                && trunk.gameObject.layer == LayerMask.NameToLayer("Obstacle");
        }
        Check(allTreesUseSharedMaterial, "All nine tree prefabs use the same cursor fade material");
        Check(allTreesAreDropSources, "All nine tree prefabs provide a movable leaf source and trunk-only spawn exclusion");

        bool reedsExcludeTreeMaterial = true;
        for (int i = 1; i <= 2; i++)
        {
            GameObject reed = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Prefabs/Gameplay/Props/Reed_{i:00}.prefab");
            SpriteRenderer renderer = reed != null ? reed.GetComponent<SpriteRenderer>() : null;
            reedsExcludeTreeMaterial &= renderer != null && renderer.sharedMaterial != material;
        }
        Check(reedsExcludeTreeMaterial, "Non-tree decoration prefabs do not use the tree cursor fade material");
    }

    private static void ValidateTreeCursorFade(LevelRoot root, LevelId id)
    {
        Material sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Art/Gameplay/Materials/TreeCursorFade.mat");
        if (sharedMaterial == null) return;

        bool treeMaterialsCorrect = true;
        bool otherMaterialsUntouched = true;
        ValidatePlacedObjectMaterials(root, "Obstacles", prototype.Obstacles, sharedMaterial,
            ref treeMaterialsCorrect, ref otherMaterialsUntouched);
        ValidatePlacedObjectMaterials(root, "Decorations", prototype.Decorations, sharedMaterial,
            ref treeMaterialsCorrect, ref otherMaterialsUntouched);
        ValidatePlacedObjectMaterials(root, "Landmarks", prototype.Landmarks, sharedMaterial,
            ref treeMaterialsCorrect, ref otherMaterialsUntouched);

        Check(treeMaterialsCorrect, $"{id}: every placed tree inherits the shared cursor fade material");
        Check(otherMaterialsUntouched, $"{id}: non-tree placed objects exclude the cursor fade material");

        LeafSpawner spawner = root.GetComponentInChildren<LeafSpawner>(true);
        Check(spawner != null && spawner.TreeSourceCount == 11,
            $"{id}: LeafSpawner discovers all 11 trees across obstacle, decoration, and landmark groups");
        ValidateMovableTreeSource(root, "Obstacles", id);
        ValidateMovableTreeSource(root, "Decorations", id);
        ValidateMovableTreeSource(root, "Landmarks", id);
    }

    private static void ValidateMovableTreeSource(LevelRoot root, string groupName, LevelId id)
    {
        Transform group = root.transform.Find(groupName);
        LeafDropSource source = group != null ? group.GetComponentInChildren<LeafDropSource>(true) : null;
        Check(source != null, $"{id}: {groupName} contains a component-marked leaf source");
        if (source == null) return;

        Transform sourceTransform = source.transform;
        Vector3 originalPosition = sourceTransform.position;
        Vector3 originalScale = sourceTransform.localScale;
        Vector2 originalCenter = source.DropCenter;
        float originalRadius = source.InfluenceRadius;
        Vector3 move = new Vector3(originalRadius * 2f + 14f, 7f, 0f);

        sourceTransform.position += move;
        Physics2D.SyncTransforms();

        Vector2 actualCenterDelta = source.DropCenter - originalCenter;
        Vector2 actualTreeDelta = sourceTransform.position - originalPosition;
        Check(Vector2.Distance(actualCenterDelta, actualTreeDelta) < 0.05f,
            $"{id}: moving a {groupName} tree moves its leaf source without stored map coordinates " +
            $"(tree delta {actualTreeDelta}, source delta {actualCenterDelta})");
        Check(!source.Contains(originalCenter),
            $"{id}: a moved {groupName} tree no longer influences its old position");

        sourceTransform.localScale = originalScale * 1.25f;
        Physics2D.SyncTransforms();

        SpriteRenderer renderer = source.GetComponent<SpriteRenderer>();
        float expectedRadius = renderer != null
            ? Mathf.Clamp(renderer.bounds.size.x * 0.65f + 2f, 5f, 12f)
            : 0f;
        Check(Mathf.Abs(source.InfluenceRadius - expectedRadius) < 0.01f,
            $"{id}: scaling a {groupName} tree recomputes the 0.65-width influence radius");

        sourceTransform.position = originalPosition;
        sourceTransform.localScale = originalScale;
        Physics2D.SyncTransforms();
    }

    private static void ValidatePlacedObjectMaterials(
        LevelRoot root,
        string groupName,
        TiledMapPrototypeImporter.MapObject[] placements,
        Material treeMaterial,
        ref bool treeMaterialsCorrect,
        ref bool otherMaterialsUntouched)
    {
        Transform group = root.transform.Find(groupName);
        if (group == null)
        {
            treeMaterialsCorrect = false;
            otherMaterialsUntouched = false;
            return;
        }

        for (int i = 0; i < placements.Length; i++)
        {
            Transform instance = group.Find(placements[i].Name);
            SpriteRenderer renderer = instance != null ? instance.GetComponent<SpriteRenderer>() : null;
            bool isTree = placements[i].PrefabKey.StartsWith("Tree_", StringComparison.Ordinal);
            if (isTree)
                treeMaterialsCorrect &= renderer != null && renderer.sharedMaterial == treeMaterial;
            else
                otherMaterialsUntouched &= renderer != null && renderer.sharedMaterial != treeMaterial;
        }
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
        int nearTreeCount = 0;

        for (int i = 0; i < leaves.Length; i++)
        {
            LeafLifecycle leaf = leaves[i];
            if (leaf.gameObject.layer != leafLayer
                || leaf.GetComponentInChildren<SpriteRenderer>(true) == null
                || leaf.GetComponent<Rigidbody2D>() == null
                || leaf.GetComponent<Collider2D>() == null
                || leaf.GetComponent<Windable>() == null
                || leaf.GetComponentInChildren<YSort>(true) == null
                || leaf.GetComponent<LeafWindFeedback>() == null)
            {
                componentsCorrect = false;
            }

            SpriteRenderer renderer = leaf.GetComponentInChildren<SpriteRenderer>(true);
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
                if (width < 0.989f || width > 1.381f || height < 0.839f || height > 1.261f)
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
            if (leaf.SpawnedNearTree) nearTreeCount++;
        }

        Check(componentsCorrect, $"{id}: every spawned leaf preserves the prefab component set and Leaf layer");
        Check(sizesCorrect, $"{id}: every spawned leaf uses the one-and-a-half world-size range");
        Check(outsideCount == 0, $"{id}: all sampled leaves keep one metre from map boundaries");
        Check(blockedCount == 0, $"{id}: sampled leaves keep one metre from obstacles and water");
        Check(nearTreeCount == Mathf.FloorToInt(expectedCount * 0.7f),
            $"{id}: exactly 70% of the initial leaves use tree-weighted spawn sampling ({nearTreeCount}/{expectedCount})");

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
            if (hit.name == "SpawnExclusion" && hit.GetComponentInParent<LeafDropSource>() != null) continue;
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
                $"{id}: whole river collects and unregisters exactly one leaf");
        }

        if (pieces.Length == 1)
        {
            RiverCollector collector = pieces[0].GetComponent<RiverCollector>();
            RiverWaterMask mask = pieces[0].GetComponent<RiverWaterMask>();
            SpriteRenderer renderer = pieces[0].GetComponent<SpriteRenderer>();
            LeafLifecycle dryLeaf = FindUncollectedLeaf(root);
            Vector2 dryPoint = Vector2.zero;
            bool foundDryPoint = mask != null && renderer != null
                && TryFindMaskPoint(mask, renderer, false, out dryPoint);
            int dryCountBefore = root.ActiveLeafCount;
            float dryCoinsBefore = RiverCollector.SessionCoins;
            bool dryCollected = foundDryPoint && CollectAtWaterPoint(dryLeaf, collector, dryPoint);
            Check(foundDryPoint
                && !dryCollected
                && root.ActiveLeafCount == dryCountBefore
                && Mathf.Approximately(RiverCollector.SessionCoins, dryCoinsBefore),
                $"{id}: whole-river sand, grass, and transparent regions cannot collect leaves");
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

        LeafSpawner spawner = root.GetComponentInChildren<LeafSpawner>(true);
        LeafLifecycle pooledLeaf = FindUncollectedLeaf(root);
        int activeBeforePoolCheck = root.ActiveLeafCount;
        bool pooled = CollectAtWaterPoint(pooledLeaf, pieces[0].GetComponent<RiverCollector>(), FindWaterPoint(pieces[0]));
        int respawned = spawner != null ? spawner.Spawn(1) : 0;
        Windable reusedWindable = pooledLeaf != null ? pooledLeaf.GetComponent<Windable>() : null;
        Rigidbody2D reusedBody = pooledLeaf != null ? pooledLeaf.GetComponent<Rigidbody2D>() : null;
        Check(pooled
            && respawned == 1
            && pooledLeaf != null
            && pooledLeaf.gameObject.activeSelf
            && reusedWindable != null
            && !reusedWindable.IsCollected
            && reusedBody != null
            && reusedBody.simulated
            && reusedBody.velocity == Vector2.zero
            && root.ActiveLeafCount == activeBeforePoolCheck,
            $"{id}: collected leaves are pooled and reused with clean lifecycle and rigidbody state");
    }

    private static void ValidateEndlessRules(LevelRoot root, LevelId id)
    {
        Check(root.EndlessSpawnBatch == 32, $"{id}: endless batch size is 32");
        Check(Mathf.Approximately(root.EndlessSpawnInterval, 1.8f), $"{id}: endless interval is 1.8 seconds");
        Check(root.EndlessMaxLeaves == 4160, $"{id}: endless maximum is 4160 leaves");
        Check(Mathf.Approximately(root.EndlessSurvivalMaximum, 100f)
            && Mathf.Approximately(root.EndlessSurvivalInitial, 100f)
            && Mathf.Approximately(root.EndlessSurvivalPerLeaf, 8f)
            && Mathf.Approximately(root.EndlessSurvivalBaseDrain, 2f)
            && Mathf.Approximately(root.EndlessSurvivalStageSeconds, 60f)
            && Mathf.Approximately(root.EndlessSurvivalStageMultiplier, 1.3f)
            && Mathf.Approximately(root.EndlessSurvivalValue, 100f),
            $"{id}: survival rules use 100/100/+8/-2/60/x1.3 and start full");

        RiverImagePiece piece = root.GetComponentInChildren<RiverImagePiece>(true);
        RiverCollector collector = piece != null ? piece.GetComponent<RiverCollector>() : null;
        int collectedEventTotal = 0;
        Action<int> countCollected = count => collectedEventTotal += count;
        RiverCollector.LeavesCollected += countCollected;

        root.Tick(4f);
        Check(Mathf.Approximately(root.EndlessSurvivalValue, 92f)
            && Mathf.Approximately(root.CurrentEndlessDrainPerSecond, 2f),
            $"{id}: survival drains by exactly 2 points per second in stage zero");

        LeafLifecycle recoveryLeaf = FindUncollectedLeaf(root);
        bool recoveryCollected = CollectAtWaterPoint(recoveryLeaf, collector, FindWaterPoint(piece));
        Check(recoveryCollected
            && collectedEventTotal == 1
            && Mathf.Approximately(root.EndlessSurvivalValue, 100f),
            $"{id}: one collected leaf emits one event, restores 8 points, and caps at 100");
        RiverCollector.LeavesCollected -= countCollected;

        root.RestoreEndlessSurvival(100);
        root.Tick(56f);
        Check(Mathf.Approximately(root.EndlessSurvivalValue, 0f)
            && Mathf.Approximately(root.CurrentEndlessDrainPerSecond, 2.6f),
            $"{id}: reaching 60 seconds advances drain to 2.6 per second");
        root.RestoreEndlessSurvival(1);
        Check(!root.IsEndlessSurvivalDepleted && Mathf.Approximately(root.EndlessSurvivalValue, 8f),
            $"{id}: a same-frame leaf recovery clears the pending depleted state before flow failure checks");
        root.RestoreEndlessSurvival(100);
        root.Tick(60f);
        Check(Mathf.Approximately(root.CurrentEndlessDrainPerSecond, 3.38f),
            $"{id}: reaching 120 seconds advances drain to 3.38 per second");
        root.RestoreEndlessSurvival(100);
        root.Tick(60f);
        Check(Mathf.Approximately(root.CurrentEndlessDrainPerSecond, 4.394f),
            $"{id}: reaching 180 seconds advances drain to 4.394 per second");

        int before = root.ActiveLeafCount;
        root.Tick(root.EndlessSpawnInterval);
        Check(root.ActiveLeafCount == before + root.EndlessSpawnBatch, $"{id}: one interval spawns one batch");

        int remainingBatches = Mathf.CeilToInt(
            (root.EndlessMaxLeaves - root.ActiveLeafCount) / (float)root.EndlessSpawnBatch);
        for (int i = 0; i < remainingBatches + 2; i++)
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

        ValidateLevelSelectUI(flow);
        ValidateUpgradePrices();

        InvokePrivate(flow, "StartLevel", LevelId.SimpleSmall);
        LevelRoot firstSimple = LevelLoader.Current;
        CompleteInitialSpawn(firstSimple);
        if (firstSimple != null) unloadedLevels.Add(firstSimple);

        Check(firstSimple != null && firstSimple.Id == LevelId.SimpleSmall,
            "GameFlow starts SimpleSmall through LevelLoader");
        Check(GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.Playing,
            "GameFlow enters Playing state after level selection");
        Check(GetPrivateField<UIRouter>(flow, "router").Current == UIRouter.State.Playing,
            "UIRouter shows the gameplay HUD after level selection");
        if (firstSimple == null) return;

        CheckRunReset(flow, firstSimple, "Starting a level");
        ValidateHudTimer(flow, false, "00:00");
        ValidateCoinGainFeedback(flow, firstSimple);
        ValidateWindBlow(firstSimple);
        Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Downburst,
            "Blowing leaves never advances the wind form");

        CollectCoins(firstSimple, 49);
        InvokePrivate(flow, "TryBuyNextWindForm");
        Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Downburst
            && Mathf.Approximately(RiverCollector.CoinCount, 49f),
            "49 coins cannot buy Surface wind");

        CollectCoins(firstSimple, 1);
        InvokePrivate(flow, "TryBuyNextWindForm");
        Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Surface
            && Mathf.Approximately(RiverCollector.CoinCount, 0f)
            && Mathf.Approximately(RiverCollector.SessionCoins, 50f)
            && RiverCollector.SessionLeafCount == 50
            && GetPrivateField<UpgradeInheritance>(flow, "inheritedUpgrades") == UpgradeInheritance.None
            && AreUpgradeLevelsZero(flow),
            "Buying Surface without branch upgrades applies no inheritance and needs no selection");

        WindBlower blower = firstSimple.WindBlower;
        Check(blower != null
            && blower.Shape == WindShape.Surface
            && Mathf.Approximately(blower.BaseWind, 2.2f)
            && Mathf.Approximately(blower.SurfaceLength, 18f)
            && Mathf.Approximately(blower.SurfaceStartWidth, 6f)
            && Mathf.Approximately(blower.SurfaceEndWidth, 10f)
            && blower.MaxTargetsPerBlow == 24,
            "A form purchase applies the unchanged Surface base values");

        SetPrivateField(flow, "elapsedTime", 999f);
        InvokePrivate(flow, "Update");
        Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Surface,
            "Elapsed time never advances the wind form");

        CollectCoins(firstSimple, 10);
        InvokePrivate(flow, "TryBuyUpgrade", UpgradeKind.WindPower);
        int[] levels = GetPrivateField<int[]>(flow, "upgradeLevels");
        Check(levels[(int)UpgradeKind.WindPower] == 1
            && Mathf.Approximately(RiverCollector.CoinCount, 0f),
            "Surface Wind Power level one costs 10 coins");

        CollectCoins(firstSimple, 299);
        InvokePrivate(flow, "TryBuyNextWindForm");
        Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Surface
            && Mathf.Approximately(RiverCollector.CoinCount, 299f),
            "299 coins cannot buy Tornado wind");

        CollectCoins(firstSimple, 1);
        InvokePrivate(flow, "TryBuyNextWindForm");
        Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Tornado
            && GetPrivateField<UpgradeInheritance>(flow, "inheritedUpgrades") == UpgradeInheritance.WindPower
            && AreUpgradeLevelsZero(flow)
            && Mathf.Approximately(RiverCollector.CoinCount, 0f)
            && Mathf.Approximately(RiverCollector.SessionCoins, 360f)
            && RiverCollector.SessionLeafCount == 360,
            "A purchased Wind Power branch is inherited automatically when buying Tornado");
        Check(blower != null
            && blower.Shape == WindShape.Tornado
            && Mathf.Approximately(blower.BaseWind, 5f * 1.15f)
            && Mathf.Approximately(blower.Radius, 12f)
            && blower.MaxTargetsPerBlow == 100,
            "Automatic single-branch inheritance applies the Wind Power bonus once");
        InvokePrivate(flow, "TryBuyNextWindForm");
        Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Tornado,
            "Wind forms can only be bought in sequence and stop at Tornado");

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
        ResultUI result = GetPrivateField<ResultUI>(flow, "result");
        InvokePrivate(result, "Update");
        Check(GetPrivateField<Text>(result, "sessionLabel").text == "本局金币：360"
            && GetPrivateField<Text>(result, "remainingLabel").text == "剩余金币：0"
            && !GetPrivateField<Text>(result, "leafLabel").enabled
            && Vector2.Distance(GetPrivateField<RectTransform>(result, "panelRect").sizeDelta,
                new Vector2(1120f, 362f)) < 0.01f,
            "Non-timed result keeps the three-row layout and hides the leaf total");

        InvokePrivate(flow, "StartLevel", LevelId.SimpleSmall);
        LevelRoot replayedSimple = LevelLoader.Current;
        CompleteInitialSpawn(replayedSimple);
        if (replayedSimple != null) unloadedLevels.Add(replayedSimple);
        Check(replayedSimple != null && replayedSimple != firstSimple,
            "Replay replaces the old level with a fresh prefab instance");
        CheckRunReset(flow, replayedSimple, "Replay");

        if (replayedSimple != null)
        {
            CollectCoins(replayedSimple, 60);
            InvokePrivate(flow, "ToggleShop");
            InvokePrivate(flow, "TryBuyUpgrade", UpgradeKind.WindPower);
            InvokePrivate(flow, "TryBuyUpgrade", UpgradeKind.WindArea);
            InvokePrivate(flow, "TryBuyNextWindForm");

            Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Surface
                && GetPrivateField<UpgradeInheritance>(flow, "inheritedUpgrades")
                    == (UpgradeInheritance.WindPower | UpgradeInheritance.WindArea)
                && flow.ShopOpen
                && shop.IsVisible
                && AreUpgradeLevelsZero(flow)
                && replayedSimple.WindBlower != null
                && Mathf.Approximately(replayedSimple.WindBlower.SurfaceLength, 18f * 1.12f)
                && Mathf.Approximately(replayedSimple.WindBlower.BaseWind, 2.2f * 1.15f)
                && replayedSimple.WindBlower.MaxTargetsPerBlow == 24,
                "All purchased Downburst branches inherit immediately while the shop stays open");

            CollectCoins(replayedSimple, 320);
            InvokePrivate(flow, "TryBuyUpgrade", UpgradeKind.WindPower);
            InvokePrivate(flow, "TryBuyUpgrade", UpgradeKind.WindPulse);
            InvokePrivate(flow, "TryBuyNextWindForm");
            Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Tornado
                && GetPrivateField<UpgradeInheritance>(flow, "inheritedUpgrades")
                    == (UpgradeInheritance.WindPower | UpgradeInheritance.WindArea | UpgradeInheritance.WindPulse)
                && AreUpgradeLevelsZero(flow)
                && replayedSimple.WindBlower.Shape == WindShape.Tornado
                && Mathf.Approximately(replayedSimple.WindBlower.BaseWind, 5f * 1.15f)
                && Mathf.Approximately(replayedSimple.WindBlower.Radius, 12f * 1.12f)
                && replayedSimple.WindBlower.MaxTargetsPerBlow == 120,
                "Inherited branches persist across forms, new branches merge in, and repeated Power does not stack");

            InvokePrivate(flow, "ToggleSettings");
            Check(flow.SettingsOpen && !flow.ShopOpen && settings.IsVisible && !shop.IsVisible,
                "Automatic inheritance leaves normal shop and Settings controls available");
            InvokePrivate(flow, "ToggleSettings");

            ClearRegisteredLeaves(replayedSimple);
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
        CompleteInitialSpawn(timed);
        if (timed != null) unloadedLevels.Add(timed);
        CheckRunReset(flow, timed, "Changing levels");
        ValidateHudTimer(flow, true, "03:00");

        if (timed != null)
        {
            CollectCoins(timed, 350);
            InvokePrivate(flow, "TryBuyNextWindForm");
            Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Surface
                && Mathf.Approximately(RiverCollector.CoinCount, 300f),
                "The first step of a direct 350-coin form purchase always buys Surface");
            InvokePrivate(flow, "TryBuyNextWindForm");
            Check(GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Tornado
                && Mathf.Approximately(RiverCollector.CoinCount, 0f)
                && Mathf.Approximately(RiverCollector.SessionCoins, 350f)
                && RiverCollector.SessionLeafCount == 350,
                "350 collected leaves remain recorded after buying Surface and Tornado");

            InvokePrivate(flow, "EndGame", false);
            InvokePrivate(result, "Update");
            Check(GetPrivateField<Text>(result, "leafLabel").enabled
                && GetPrivateField<Text>(result, "leafLabel").text == "最终获得树叶：350"
                && Vector2.Distance(GetPrivateField<RectTransform>(result, "panelRect").sizeDelta,
                    new Vector2(1120f, 430f)) < 0.01f,
                "Timed failure result shows the independent leaf total in the four-row layout");
            InvokePrivate(flow, "EndGame", true);
            InvokePrivate(result, "Update");
            Check(GetPrivateField<Text>(result, "leafLabel").enabled
                && GetPrivateField<Text>(result, "leafLabel").text == "最终获得树叶：350",
                "Timed success result also keeps the final leaf total visible");
        }
        Check(1920 - UpgradeCatalog.GetFormCost(WindForm.Surface)
                - UpgradeCatalog.GetFormCost(WindForm.Tornado) == 1570,
            "A 1920-leaf timed run leaves at most 1570 coins after both form purchases");

        InvokePrivate(flow, "StartLevel", LevelId.Endless);
        LevelRoot endless = LevelLoader.Current;
        CompleteInitialSpawn(endless);
        if (endless != null) unloadedLevels.Add(endless);
        CheckRunReset(flow, endless, "Starting Endless");
        ValidateEndlessHud(flow);

        if (endless != null)
        {
            float valueBeforePause = endless.EndlessSurvivalValue;
            float elapsedBeforePause = GetPrivateField<float>(flow, "elapsedTime");
            InvokePrivate(flow, "ToggleShop");
            InvokePrivate(flow, "Update");
            Check(Mathf.Approximately(endless.EndlessSurvivalValue, valueBeforePause)
                && Mathf.Approximately(GetPrivateField<float>(flow, "elapsedTime"), elapsedBeforePause),
                "Endless survival and score time stop while the shop is open");
            InvokePrivate(flow, "ToggleShop");

            endless.Tick(50f);
            InvokePrivate(flow, "Update");
            Check(GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.EndlessFailure
                && Mathf.Approximately(Time.timeScale, 0f)
                && !endless.WindBlower.enabled
                && GetPrivateField<UIRouter>(flow, "router").Current == UIRouter.State.Playing,
                "An empty endless bar immediately freezes physics and input while keeping the HUD visible");

            float lockedTime = GetPrivateField<float>(flow, "elapsedTime");
            InvokePrivate(flow, "AdvanceEndlessFailure", 0.99f);
            Check(GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.EndlessFailure
                && Mathf.Approximately(GetPrivateField<float>(flow, "elapsedTime"), lockedTime),
                "Endless failure remains on the HUD at 0.99 seconds without extending score time");
            InvokePrivate(flow, "AdvanceEndlessFailure", 0.01f);
            InvokePrivate(result, "Update");
            Check(GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.Result
                && GetPrivateField<UIRouter>(flow, "router").Current == UIRouter.State.Result
                && GetPrivateField<Text>(result, "resultTitleLabel").text == "挑战结束"
                && GetPrivateField<Text>(result, "timeLabel").text == "坚持时间：" + FormatTimeForValidation(lockedTime)
                && GetPrivateField<Text>(result, "leafLabel").text == "入河树叶：0"
                && GetPrivateField<Text>(result, "sessionLabel").text == "本局金币：0"
                && GetPrivateField<Text>(result, "remainingLabel").text == "剩余金币：0"
                && Vector2.Distance(GetPrivateField<RectTransform>(result, "panelRect").sizeDelta,
                    new Vector2(1120f, 430f)) < 0.01f,
                "Endless waits a full unscaled second, then shows the four requested result rows without a best score");
        }

        InvokePrivate(flow, "StartLevel", LevelId.TimedChallenge);
        LevelRoot timedReplay = LevelLoader.Current;
        CompleteInitialSpawn(timedReplay);
        if (timedReplay != null) unloadedLevels.Add(timedReplay);
        CheckRunReset(flow, timedReplay, "Restarting TimedChallenge");
        SetPrivateField(flow, "elapsedTime", 180f);
        HudUI timedHud = GetPrivateField<HudUI>(flow, "hud");
        InvokePrivate(timedHud, "Update");
        bool timerReachedZero = GetPrivateField<Text>(timedHud, "timeLabel").text == "00:00";
        InvokePrivate(flow, "Update");
        InvokePrivate(result, "Update");
        Check(GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.Result
            && !GetPrivateField<bool>(flow, "resultSucceeded")
            && timerReachedZero
            && GetPrivateField<Text>(result, "timeLabel").text == "用时：03:00"
            && GetPrivateField<Text>(result, "leafLabel").text == "最终获得树叶：0",
            "TimedChallenge reaches 00:00, fails after 180 seconds, and reports zero collected leaves");

        InvokePrivate(flow, "ReturnToMainMenu");
        Check(LevelLoader.Current == null
            && GetPrivateField<GameFlowManager.GameState>(flow, "state") == GameFlowManager.GameState.MainMenu
            && GetPrivateField<UIRouter>(flow, "router").Current == UIRouter.State.MainMenu,
            "Returning to main menu unloads gameplay and restores main UI state");
    }

    private static void ValidateUpgradePrices()
    {
        Check(UpgradeCatalog.GetFormCost(WindForm.Downburst) == 0
            && UpgradeCatalog.GetFormCost(WindForm.Surface) == 50
            && UpgradeCatalog.GetFormCost(WindForm.Tornado) == 300,
            "Wind form prices are 0/50/300 by target form");

        int[][] expected =
        {
            new[] { 5, 15 },
            new[] { 10, 30 },
            new[] { 20, 60 }
        };

        bool pricesMatch = true;
        for (int form = 0; form < expected.Length; form++)
        {
            for (int kind = 0; kind < UpgradeCatalog.All.Length; kind++)
            {
                pricesMatch &= UpgradeCatalog.GetNextCost((WindForm)form, (UpgradeKind)kind, 0) == expected[form][0];
                pricesMatch &= UpgradeCatalog.GetNextCost((WindForm)form, (UpgradeKind)kind, 1) == expected[form][1];
            }
        }

        Check(pricesMatch, "All three branches share 5/15, 10/30, and 20/60 costs by form");
    }

    private static void CheckRunReset(GameFlowManager flow, LevelRoot root, string context)
    {
        WindBlower blower = root != null ? root.WindBlower : null;
        Check(root != null
            && Mathf.Approximately(RiverCollector.CoinCount, 0f)
            && Mathf.Approximately(RiverCollector.SessionCoins, 0f)
            && RiverCollector.SessionLeafCount == 0
            && GetPrivateField<WindForm>(flow, "currentWindForm") == WindForm.Downburst
            && GetPrivateField<UpgradeInheritance>(flow, "inheritedUpgrades") == UpgradeInheritance.None
            && AreUpgradeLevelsZero(flow)
            && blower != null
            && blower.Shape == WindShape.Downburst
            && Mathf.Approximately(blower.BaseWind, 1f)
            && Mathf.Approximately(blower.Radius, 6f)
            && blower.MaxTargetsPerBlow == 10,
            $"{context} resets coins, leaves, form, branches, inheritance, and runtime wind values");
    }

    private static void ValidateHudTimer(GameFlowManager flow, bool timed, string expectedText)
    {
        HudUI hud = GetPrivateField<HudUI>(flow, "hud");
        InvokePrivate(hud, "Update");
        RectTransform rect = GetPrivateField<RectTransform>(hud, "timeRect");
        Image background = GetPrivateField<Image>(hud, "timeBackground");
        Image icon = GetPrivateField<Image>(hud, "timeIcon");
        Text label = GetPrivateField<Text>(hud, "timeLabel");

        Color expectedLabelColor = Theme.TextDark;
        expectedLabelColor.a = 0.85f;
        bool layoutMatches = Vector2.Distance(rect.anchorMin, new Vector2(0.5f, 1f)) < 0.01f
            && Vector2.Distance(rect.anchorMax, new Vector2(0.5f, 1f)) < 0.01f
            && Vector2.Distance(rect.pivot, new Vector2(0.5f, 1f)) < 0.01f
            && Vector2.Distance(rect.sizeDelta, new Vector2(240f, 76f)) < 0.01f
            && Vector2.Distance(rect.anchoredPosition, new Vector2(0f, -52f)) < 0.01f
            && background.sprite != null
            && background.sprite.name == "常用_正常"
            && background.color == new Color(1f, 1f, 1f, 0.70f)
            && icon != null
            && icon.sprite != null
            && icon.sprite.name == "icon_沙漏"
            && Vector2.Distance(icon.rectTransform.sizeDelta, new Vector2(32f, 42f)) < 0.01f
            && Vector2.Distance(icon.rectTransform.anchoredPosition, new Vector2(-68f, 0f)) < 0.01f
            && icon.color == new Color(1f, 1f, 1f, 0.85f)
            && Vector2.Distance(label.rectTransform.offsetMin, new Vector2(76f, 0f)) < 0.01f
            && Vector2.Distance(label.rectTransform.offsetMax, new Vector2(-18f, 0f)) < 0.01f
            && label.fontSize == 42
            && label.fontStyle == FontStyle.Bold
            && label.color == expectedLabelColor;

        Check(layoutMatches && label.text == expectedText,
            timed
                ? "Timed HUD uses the unified, faded, centered hourglass timer style"
                : "Non-timed HUD uses the unified, faded, centered hourglass timer style");
    }

    private static void ValidateEndlessHud(GameFlowManager flow)
    {
        HudUI hud = GetPrivateField<HudUI>(flow, "hud");
        InvokePrivate(hud, "Update");
        RectTransform timeRect = GetPrivateField<RectTransform>(hud, "timeRect");
        GameObject survivalBar = GetPrivateField<GameObject>(hud, "survivalBar");
        RectTransform fillRect = GetPrivateField<RectTransform>(hud, "survivalFillRect");
        Text timeLabel = GetPrivateField<Text>(hud, "timeLabel");

        Check(survivalBar != null
            && survivalBar.activeSelf
            && Vector2.Distance(survivalBar.GetComponent<RectTransform>().sizeDelta, new Vector2(210f, 22f)) < 0.01f
            && Vector2.Distance(survivalBar.GetComponent<RectTransform>().anchoredPosition, new Vector2(-132f, -120f)) < 0.01f
            && Vector2.Distance(fillRect.sizeDelta, new Vector2(204f, 16f)) < 0.01f
            && Vector2.Distance(timeRect.sizeDelta, new Vector2(210f, 42f)) < 0.01f
            && Vector2.Distance(timeRect.anchoredPosition, new Vector2(-132f, -70f)) < 0.01f
            && timeLabel.text == "00:00",
            "Endless HUD stacks coins, elapsed time, and an unnamed 210x22 full survival bar at top right");
    }

    private static string FormatTimeForValidation(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int remainder = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:00}:{remainder:00}";
    }

    private static void ValidateCoinGainFeedback(GameFlowManager flow, LevelRoot root)
    {
        HudUI hud = GetPrivateField<HudUI>(flow, "hud");
        RiverImagePiece piece = root.GetComponentInChildren<RiverImagePiece>(true);
        RiverCollector collector = piece != null ? piece.GetComponent<RiverCollector>() : null;
        LeafLifecycle[] leaves = root.GetComponentsInChildren<LeafLifecycle>(true);
        RectTransform icon = GetPrivateField<RectTransform>(hud, "coinIconRect");
        Text label = GetPrivateField<Text>(hud, "coinLabel");
        Check(hud != null && collector != null && leaves.Length >= 4 && icon != null && label != null,
            "Coin feedback validation has the HUD targets, collector, and leaves");
        if (hud == null || collector == null || leaves.Length < 4 || icon == null || label == null) return;

        int eventCount = 0;
        float gainedTotal = 0f;
        Action<float> countGain = amount =>
        {
            eventCount++;
            gainedTotal += amount;
        };
        RiverCollector.CoinsGained += countGain;

        Vector2 waterPoint = FindWaterPoint(piece);
        LeafLifecycle first = FindUncollectedLeaf(root);
        bool firstCollected = CollectAtWaterPoint(first, collector, waterPoint);
        Check(firstCollected
            && eventCount == 1
            && Mathf.Approximately(gainedTotal, 1f)
            && GetPrivateField<bool>(hud, "coinPopPlaying")
            && Mathf.Approximately(GetPrivateField<float>(hud, "coinPopElapsed"), 0f),
            "A successful collection emits one gain event and starts the HUD coin pop");

        InvokePrivate(hud, "ApplyCoinPopPose", 0.06f);
        Check(ApproximatelyUniformScale(icon, 1.30f) && ApproximatelyUniformScale(label.rectTransform, 1.30f),
            "Coin icon and number reach the 1.3x pop peak together");

        LeafLifecycle second = FindUncollectedLeaf(root);
        bool secondCollected = CollectAtWaterPoint(second, collector, waterPoint);
        Check(secondCollected
            && eventCount == 2
            && Mathf.Approximately(GetPrivateField<float>(hud, "coinPopStartScale"), 1.30f)
            && Mathf.Approximately(GetPrivateField<float>(hud, "coinPopElapsed"), 0f),
            "A rapid second gain restarts smoothly from the current pop scale");

        InvokePrivate(hud, "ApplyCoinPopPose", 0.13f);
        Check(ApproximatelyUniformScale(icon, 0.96f) && ApproximatelyUniformScale(label.rectTransform, 0.96f),
            "Coin icon and number reach the shared 0.96x rebound");
        InvokePrivate(hud, "ApplyCoinPopPose", 0.22f);
        Check(!GetPrivateField<bool>(hud, "coinPopPlaying")
            && icon.localScale == Vector3.one
            && label.rectTransform.localScale == Vector3.one,
            "Coin pop finishes at the exact resting scale");

        int eventsBeforeSpend = eventCount;
        bool spent = RiverCollector.TrySpendCoins(1f);
        Check(spent
            && eventCount == eventsBeforeSpend
            && !GetPrivateField<bool>(hud, "coinPopPlaying"),
            "Spending coins does not emit a gain event or restart the pop");

        LeafLifecycle hiddenLeaf = FindUncollectedLeaf(root);
        InvokePrivate(hud, "HandleCoinsGained", 1f);
        InvokePrivate(hud, "ApplyCoinPopPose", 0.06f);
        hud.Hide();
        bool hiddenCollected = CollectAtWaterPoint(hiddenLeaf, collector, waterPoint);
        Check(hiddenCollected
            && eventCount == eventsBeforeSpend + 1
            && !GetPrivateField<bool>(hud, "coinPopPlaying")
            && icon.localScale == Vector3.one
            && label.rectTransform.localScale == Vector3.one,
            "Hiding the HUD resets its pop and ignores gains while hidden");

        hud.Show();
        int eventsBeforeDuplicate = eventCount;
        CollectAtWaterPoint(hiddenLeaf, collector, waterPoint);
        Check(eventCount == eventsBeforeDuplicate && !GetPrivateField<bool>(hud, "coinPopPlaying"),
            "A duplicate collection does not emit another gain event");

        RiverCollector.ResetRun();
        Check(eventCount == eventsBeforeDuplicate
            && !GetPrivateField<bool>(hud, "coinPopPlaying")
            && icon.localScale == Vector3.one
            && label.rectTransform.localScale == Vector3.one,
            "Resetting the run does not emit a gain event or leave a pop pose");
        RiverCollector.CoinsGained -= countGain;
    }

    private static bool ApproximatelyUniformScale(RectTransform rect, float expected)
    {
        return rect != null
            && Mathf.Abs(rect.localScale.x - expected) < 0.001f
            && Mathf.Abs(rect.localScale.y - expected) < 0.001f
            && Mathf.Abs(rect.localScale.z - expected) < 0.001f;
    }

    private static bool AreUpgradeLevelsZero(GameFlowManager flow)
    {
        int[] levels = GetPrivateField<int[]>(flow, "upgradeLevels");
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != 0) return false;
        }

        return true;
    }

    private static void ValidateLevelSelectUI(GameFlowManager flow)
    {
        LevelSelectUI levelSelect = GetPrivateField<LevelSelectUI>(flow, "levelSelect");
        UIRouter router = GetPrivateField<UIRouter>(flow, "router");
        Check(levelSelect != null, "GameFlow created the level selection UI");
        if (levelSelect == null) return;

        levelSelect.Show();
        Transform safe = levelSelect.transform.Find("SafeArea");
        Check(safe != null, "Level selection UI contains its safe area");
        if (safe != null)
        {
            Check(safe.GetComponentsInChildren<Button>(true).Length == 4,
                "Level selection UI contains one back button and exactly three mode cards");
            ValidateLevelCardAnchor(safe, "SimpleSmall", 0.20f);
            ValidateLevelCardAnchor(safe, "TimedChallenge", 0.50f);
            ValidateLevelCardAnchor(safe, "Endless", 0.80f);
        }

        router.Show(UIRouter.State.MainMenu);
    }

    private static void ValidateLevelCardAnchor(Transform safe, string cardName, float expectedX)
    {
        RectTransform card = safe.Find(cardName) as RectTransform;
        Check(card != null, $"Level selection UI contains the {cardName} card");
        Check(card != null
            && Mathf.Approximately(card.anchorMin.x, expectedX)
            && Mathf.Approximately(card.anchorMax.x, expectedX)
            && Mathf.Approximately(card.anchorMin.y, 0.46f)
            && Mathf.Approximately(card.anchorMax.y, 0.46f)
            && Vector2.Distance(card.sizeDelta, new Vector2(300f, 386f)) < 0.01f,
            $"{cardName} uses the expected centered three-card layout");
    }

    private static void ValidateWindBlow(LevelRoot root)
    {
        WindBlower blower = root.WindBlower;
        Windable leaf = root.GetComponentInChildren<Windable>(true);
        Check(blower != null && leaf != null, "Wind validation has an active WindBlower and leaf");
        if (blower == null || leaf == null) return;

        Rigidbody2D[] bodies = root.GetComponentsInChildren<Rigidbody2D>(true);
        InvokePrivate(
            blower,
            "Blow",
            leaf.Position - Vector2.right * (blower.Radius * 0.5f),
            Vector2.right);

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

        ValidateWindLineEffect(blower);
        ValidateLeafWindFeedback(leaf);
    }

    private static void ValidateWindLineEffect(WindBlower blower)
    {
        WindEffectSpawner spawner = blower.GetComponent<WindEffectSpawner>();
        Check(spawner != null, "WindBlower creates the procedural wind line effect");
        if (spawner == null) return;
        Check(GetPrivateField<float>(blower, "windEffectInterval") <= 0.18f,
            "Wind line refresh cadence is shorter than its hold time for continuous input");

        Vector2 center = new Vector2(3f, -2f);
        const float radius = 12f;
        const float surfaceLength = 18f;
        const float surfaceStartWidth = 6f;
        const float surfaceEndWidth = 10f;

        spawner.Play(
            WindShape.Downburst,
            center,
            Vector2.up,
            radius,
            surfaceLength,
            surfaceStartWidth,
            surfaceEndWidth);

        LineRenderer[] lines = spawner.GetComponentsInChildren<LineRenderer>(true);
        Check(lines.Length == 4 && CountEnabledLines(lines) == 4,
            "Downburst uses exactly four reusable open line arcs");

        Material sharedMaterial = lines.Length > 0 ? lines[0].sharedMaterial : null;
        bool renderSettingsCorrect = lines.Length == 4 && sharedMaterial != null;
        for (int i = 0; i < lines.Length; i++)
        {
            renderSettingsCorrect &= !lines[i].loop
                && lines[i].sortingLayerName == "Object"
                && lines[i].sortingOrder == 50
                && lines[i].sharedMaterial == sharedMaterial
                && lines[i].widthMultiplier >= 0.025f
                && lines[i].widthMultiplier <= 0.075f;
        }
        Check(renderSettingsCorrect,
            "Wind lines share one material, use the Object layer, and keep the clamped thin width");
        ValidateWindLineWidths(spawner);
        Check(AreCircleLinesInside(lines, center, radius, 4),
            "Downburst arcs remain inside the gameplay radius");
        Check(spawner.GetComponentInChildren<SpriteRenderer>(true) == null
            && spawner.GetComponentInChildren<WindEffectFramePlayer>(true) == null,
            "Runtime wind visuals contain no sprite sheet renderer or frame player");

        int warmedChildCount = spawner.transform.childCount;
        LineRenderer[] warmedLines = (LineRenderer[])lines.Clone();

        spawner.Play(
            WindShape.Surface,
            center,
            Vector2.right,
            radius,
            surfaceLength,
            surfaceStartWidth,
            surfaceEndWidth);
        lines = spawner.GetComponentsInChildren<LineRenderer>(true);
        Check(CountEnabledLines(lines) == 4
            && AreSurfaceLinesInside(
                lines,
                center,
                Vector2.right,
                surfaceLength,
                surfaceStartWidth,
                surfaceEndWidth),
            "Surface wind uses four directional streams inside its trapezoid");

        spawner.Play(
            WindShape.Tornado,
            center,
            Vector2.up,
            radius,
            surfaceLength,
            surfaceStartWidth,
            surfaceEndWidth);
        lines = spawner.GetComponentsInChildren<LineRenderer>(true);
        Check(CountEnabledLines(lines) == 3 && AreCircleLinesInside(lines, center, radius, 3),
            "Tornado switches cleanly to three open spiral lines inside its radius");

        bool reusedObjects = spawner.transform.childCount == warmedChildCount && lines.Length == warmedLines.Length;
        for (int i = 0; i < lines.Length && reusedObjects; i++)
        {
            reusedObjects &= Array.IndexOf(warmedLines, lines[i]) >= 0
                && lines[i].sharedMaterial == sharedMaterial;
        }
        Check(reusedObjects,
            "Changing and replaying wind forms reuses the warmed GameObjects and shared material");

        SetPrivateField(spawner, "lastPlayTime", Time.time - 0.31f);
        InvokePrivate(spawner, "Update");
        Check(CountEnabledLines(lines) == 0 && !GetPrivateField<bool>(spawner, "effectVisible"),
            "Wind lines finish the 0.18 second hold and 0.12 second fade without residue");
    }

    private static void ValidateWindLineWidths(WindEffectSpawner spawner)
    {
        Camera camera = Camera.main;
        Check(camera != null && camera.orthographic,
            "Wind line width validation has the orthographic gameplay camera");
        if (camera == null || !camera.orthographic) return;

        float originalSize = camera.orthographicSize;
        float[] sizes = { 5f, 10f, 20f };
        float previousWidth = 0f;
        bool widthsCorrect = true;

        for (int i = 0; i < sizes.Length; i++)
        {
            camera.orthographicSize = sizes[i];
            float width = (float)InvokePrivate(spawner, "GetWorldLineWidth");
            float expected = Mathf.Clamp(
                sizes[i] * 2f / Mathf.Max(1f, camera.pixelHeight) * 1.25f,
                0.025f,
                0.075f);
            widthsCorrect &= Mathf.Abs(width - expected) < 0.0001f
                && width >= previousWidth;
            previousWidth = width;
        }

        camera.orthographicSize = originalSize;
        Check(widthsCorrect,
            "Wind line width stays near 1.25 pixels at camera zoom 5, 10, and 20 within its world-width clamps");
    }

    private static int CountEnabledLines(LineRenderer[] lines)
    {
        int enabledCount = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].enabled) enabledCount++;
        }

        return enabledCount;
    }

    private static bool AreCircleLinesInside(
        LineRenderer[] lines,
        Vector2 center,
        float radius,
        int enabledLineCount)
    {
        float maxDistance = radius + 0.001f;
        int checkedLines = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].enabled) continue;
            checkedLines++;

            for (int point = 0; point < lines[i].positionCount; point++)
            {
                if (Vector2.Distance(center, lines[i].GetPosition(point)) > maxDistance)
                {
                    return false;
                }
            }
        }

        return checkedLines == enabledLineCount;
    }

    private static bool AreSurfaceLinesInside(
        LineRenderer[] lines,
        Vector2 center,
        Vector2 direction,
        float length,
        float startWidth,
        float endWidth)
    {
        direction.Normalize();
        Vector2 side = new Vector2(-direction.y, direction.x);

        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].enabled) continue;

            for (int point = 0; point < lines[i].positionCount; point++)
            {
                Vector2 offset = (Vector2)lines[i].GetPosition(point) - center;
                float forward = Vector2.Dot(offset, direction);
                if (forward < -0.001f || forward > length + 0.001f)
                {
                    return false;
                }

                float rate = length <= 0.0001f ? 0f : Mathf.Clamp01(forward / length);
                float halfWidth = Mathf.Lerp(startWidth, endWidth, rate) * 0.5f;
                if (Mathf.Abs(Vector2.Dot(offset, side)) > halfWidth + 0.001f)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void ValidateLeafDamping()
    {
        Vector2 diagonal = new Vector2(3f, 4f);
        Vector2 dampedDiagonal = InvokeLeafDamping(diagonal, 0.02f);
        float cross = diagonal.x * dampedDiagonal.y - diagonal.y * dampedDiagonal.x;
        Check(Mathf.Abs(cross) < 0.0001f && Vector2.Dot(diagonal, dampedDiagonal) > 0f,
            "Leaf damping preserves the current movement direction");

        float highSpeedLoss = 8f - InvokeLeafDamping(Vector2.right * 8f, 0.02f).magnitude;
        float lowSpeedLoss = 1f - InvokeLeafDamping(Vector2.right, 0.02f).magnitude;
        Check(highSpeedLoss > lowSpeedLoss * 4f,
            "Leaf damping removes proportionally more speed from a fast-moving leaf");

        Vector2 simulatedVelocity = Vector2.right * 8f;
        float elapsed = 0f;
        while (simulatedVelocity.sqrMagnitude > 0f && elapsed < 3f)
        {
            simulatedVelocity = InvokeLeafDamping(simulatedVelocity, 0.02f);
            elapsed += 0.02f;
        }
        Check(elapsed >= 1f && elapsed <= 2f,
            $"Leaf damping stops an 8 m/s leaf in the intended 1-2 second window ({elapsed:0.00}s)");

        Check(InvokeLeafDamping(Vector2.right * 0.08f, 0.02f) == Vector2.zero,
            "Leaf damping snaps movement at the 0.08 m/s stop threshold");
    }

    private static Vector2 InvokeLeafDamping(Vector2 velocity, float deltaTime)
    {
        MethodInfo method = typeof(Windable).GetMethod(
            "CalculateDampedVelocity",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(nameof(Windable), "CalculateDampedVelocity");
        return (Vector2)method.Invoke(null, new object[] { velocity, deltaTime, 1.5f, 0.30f, 0.08f });
    }

    private static void ValidateLeafWindFeedback(Windable leaf)
    {
        LeafWindFeedback feedback = leaf.GetComponent<LeafWindFeedback>();
        Rigidbody2D body = leaf.GetComponent<Rigidbody2D>();
        Transform windDeform = leaf.transform.Find("WindDeform");
        Transform spriteVisual = windDeform != null ? windDeform.Find("SpriteVisual") : null;
        Check(feedback != null && body != null && windDeform != null && spriteVisual != null,
            "Wind feedback validation has the complete leaf hierarchy");
        if (feedback == null || body == null || windDeform == null || spriteVisual == null) return;

        body.velocity = Vector2.zero;
        SetPrivateField(leaf, "lastWindPushTime", -999f);
        bool firstPush = leaf.TryPushByWind(Vector2.right, 1f, 0.5f);
        Check(firstPush && feedback.enabled, "A successful wind push starts leaf squash feedback");

        InvokePrivate(feedback, "ApplyPose", 0.04f);
        Vector3 peakScale = windDeform.localScale;
        Vector2 deformWorldAxis = windDeform.TransformVector(Vector3.right).normalized;
        Check(Vector2.Dot(deformWorldAxis, Vector2.right) > 0.999f,
            "Leaf squash axis aligns with the world-space wind direction");
        Check(Mathf.Abs(peakScale.x - 0.94f) < 0.001f && Mathf.Abs(peakScale.y - 1.03f) < 0.001f,
            "Leaf wind feedback reaches the configured subtle squash peak");

        SetPrivateField(feedback, "elapsed", 0.1f);
        bool cooldownPush = leaf.TryPushByWind(Vector2.up, 1f, 0.5f);
        Check(!cooldownPush && Mathf.Approximately(GetPrivateField<float>(feedback, "elapsed"), 0.1f),
            "A wind push rejected by cooldown does not restart leaf feedback");

        InvokePrivate(feedback, "ApplyPose", 0.22f);
        Check(!feedback.enabled
            && windDeform.localScale == Vector3.one
            && windDeform.localRotation == Quaternion.identity
            && spriteVisual.localScale == Vector3.one
            && spriteVisual.localRotation == Quaternion.identity,
            "Leaf wind feedback restores the exact resting transform and disables itself");
    }

    private static void CollectCoins(LevelRoot root, int count)
    {
        RiverImagePiece piece = root.GetComponentInChildren<RiverImagePiece>(true);
        RiverCollector collector = piece != null ? piece.GetComponent<RiverCollector>() : null;
        LeafLifecycle[] leaves = root.GetComponentsInChildren<LeafLifecycle>(true);
        Check(collector != null && leaves.Length >= count, "Coin setup has enough leaves and a river collector");
        if (collector == null) return;

        Vector2 waterPoint = FindWaterPoint(piece);
        float currentBefore = RiverCollector.CoinCount;
        float sessionBefore = RiverCollector.SessionCoins;
        int leavesBefore = RiverCollector.SessionLeafCount;

        int collected = 0;
        for (int i = 0; i < leaves.Length && collected < count; i++)
        {
            Windable windable = leaves[i].GetComponent<Windable>();
            if (windable == null || windable.IsCollected) continue;
            if (CollectAtWaterPoint(leaves[i], collector, waterPoint)) collected++;
        }

        Check(collected == count
            && Mathf.Approximately(RiverCollector.CoinCount - currentBefore, count)
            && Mathf.Approximately(RiverCollector.SessionCoins - sessionBefore, count)
            && RiverCollector.SessionLeafCount - leavesBefore == count,
            $"Collecting {count} leaves grants exactly {count} current coins, earned coins, and run leaves");
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
            if (windable != null
                && !windable.IsCollected
                && GetPrivateField<bool>(leaves[i], "registered"))
            {
                return leaves[i];
            }
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

    private static bool TryFindMaskPoint(
        RiverWaterMask mask,
        SpriteRenderer renderer,
        bool expectedWater,
        out Vector2 point)
    {
        Bounds localBounds = renderer.sprite.bounds;
        const int xSteps = 180;
        const int ySteps = 48;
        for (int y = 0; y <= ySteps; y++)
        {
            for (int x = 0; x <= xSteps; x++)
            {
                Vector2 local = new Vector2(
                    Mathf.Lerp(localBounds.min.x, localBounds.max.x, x / (float)xSteps),
                    Mathf.Lerp(localBounds.min.y, localBounds.max.y, y / (float)ySteps));
                point = renderer.transform.TransformPoint(local);
                if (mask.ContainsWater(point) == expectedWater) return true;
            }
        }

        point = Vector2.zero;
        return false;
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

    private static void CompleteInitialSpawn(LevelRoot root)
    {
        if (root == null) return;

        int safety = 100;
        while (!root.IsReady && safety-- > 0)
        {
            root.Tick(0f);
        }

        Check(root.IsReady, $"{root.Id}: initial leaf spawning completes before gameplay validation");
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
