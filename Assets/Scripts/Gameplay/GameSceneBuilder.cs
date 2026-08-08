using System.Collections.Generic;
using UnityEngine;

public static class GameSceneBuilder
{
    public enum LevelSize
    {
        SimpleSmall = 0,
        ClassicLarge = 1,
        TimedChallenge = 2,
        Endless = 3,

        Small = SimpleSmall,
        Medium = ClassicLarge,
        Large = TimedChallenge
    }

    private const int LeafLayer = 8;
    private const int ObstacleLayer = 9;
    private const int RiverLayer = 10;
    private const float TimedChallengeDuration = 180f;

    private const string BackgroundSortingLayer = "Background";
    private const string GroundSortingLayer = "Ground";
    private const string ActorSortingLayer = "Actor";

    private static readonly string[] LeafSprites =
    {
        "ggj/通用/叶子1.png",
        "ggj/通用/叶子2.png",
        "ggj/通用/叶子3.png",
        "ggj/通用/叶子4.png"
    };

    private static GameObject gameplayRoot;
    private static Sprite squareSprite;
    private static Sprite circleSprite;

    private static Rect mapBounds;
    private static Vector2[] riverPoints;
    private static Vector2[] riverSmoothPoints;
    private static float riverWidth;
    private static LevelConfig currentConfig;
    private static Material bankMaterial;
    private static Material waterMaterial;
    private static Texture2D generatedWaterTexture;
    private static Sprite flowLineSprite;
    private static RiverWaterMask currentRiverMask;
    private static float endlessTimer;
    private static LevelSize currentLevelSize;

    public static void BuildLevel(LevelSize levelSize)
    {
        ClearGameplayObjects();
        ConfigurePhysics();

        currentConfig = GetLevelConfig(levelSize);
        currentLevelSize = levelSize;
        mapBounds = currentConfig.MapBounds;
        riverWidth = currentConfig.RiverWidth;
        riverPoints = currentConfig.RiverPoints;
        currentRiverMask = null;
        endlessTimer = 0f;

        gameplayRoot = new GameObject("GameplayRoot");

        CreateGround();
        CreateBoundsWalls();
        CreateBoundaryArt();
        CreateRiver();
        CreatePonds();
        CreateObstacles();
        CreateDecorations();
        CreateScatters(currentConfig.ScatterCount);
        CreateWindBlower();
        SetupCamera();
    }

    public static void Tick(float deltaTime)
    {
        if (currentLevelSize != LevelSize.Endless || currentConfig == null || !currentConfig.Endless || gameplayRoot == null) return;

        endlessTimer += deltaTime;
        if (endlessTimer < currentConfig.EndlessSpawnInterval) return;
        endlessTimer = 0f;

        int currentLeaves = Object.FindObjectsByType<Windable>(FindObjectsSortMode.None).Length;
        if (currentLeaves >= currentConfig.EndlessMaxScatters) return;

        CreateScatters(currentConfig.EndlessSpawnBatch);
    }

    public static void ClearGameplayObjects()
    {
        if (gameplayRoot != null)
        {
            Object.Destroy(gameplayRoot);
            gameplayRoot = null;
        }

        currentConfig = null;
        currentRiverMask = null;
        currentLevelSize = LevelSize.SimpleSmall;
    }

    public static bool IsGameplayClear()
    {
        if (currentLevelSize == LevelSize.Endless && currentConfig != null && currentConfig.Endless) return false;
        return Object.FindObjectsByType<Windable>(FindObjectsSortMode.None).Length == 0;
    }

    public static float CurrentTimeLimitSeconds => currentConfig != null ? currentConfig.TimeLimitSeconds : 0f;

    private static void ConfigurePhysics()
    {
        Physics2D.gravity = Vector2.zero;
        Physics2D.IgnoreLayerCollision(LeafLayer, LeafLayer, true);
        Physics2D.IgnoreLayerCollision(LeafLayer, ObstacleLayer, false);
    }

    private static LevelConfig GetLevelConfig(LevelSize levelSize)
    {
        switch (levelSize)
        {
            case LevelSize.ClassicLarge:
                return CreateClassicLargeConfig();
            case LevelSize.TimedChallenge:
                return CreateTimedChallengeConfig();
            case LevelSize.Endless:
                return CreateEndlessConfig();
            default:
                return CreateSimpleSmallConfig();
        }
    }

    private static LevelConfig CreateSimpleSmallConfig()
    {
        Rect b = new Rect(-125f, -125f, 250f, 250f);
        return new LevelConfig(
            b,
            160,
            58f,
            34f,
            22f,
            66f,
            false,
            "ggj/通用/草地绿.png",
            "ggj地图补充/河2.png",
            new[]
            {
                new Vector2(b.xMin - 18f, b.yMin + 126f),
                new Vector2(b.xMin + 54f, b.yMin + 146f),
                new Vector2(b.xMin + 116f, b.yMin + 164f),
                new Vector2(b.xMin + 176f, b.yMin + 188f),
                new Vector2(b.xMax + 18f, b.yMin + 214f)
            },
            new[]
            {
                new WaterBody(new Vector2(74f, -16f), new Vector2(42f, 22f), "ggj地图补充/湖1.png"),
                new WaterBody(new Vector2(70f, -72f), new Vector2(42f, 24f), "ggj地图补充/湖2.png"),
                new WaterBody(new Vector2(25f, -92f), new Vector2(34f, 24f), "ggj地图补充/湖3.png")
            },
            new[]
            {
                new StaticObject("StoneA", new Vector2(-96f, -76f), new Vector2(34f, 24f), 35f, "ggj/通用/石头1.png", true),
                new StaticObject("StoneB", new Vector2(-69f, -62f), new Vector2(34f, 22f), 12f, "ggj/通用/石头2.png", true),
                new StaticObject("StoneC", new Vector2(-45f, -86f), new Vector2(36f, 24f), -8f, "ggj/通用/石头3.png", true)
            },
            new[]
            {
                new StaticObject("ReedA", new Vector2(91f, -71f), new Vector2(13f, 16f), 0f, "ggj/通用/芦苇1.png", false),
                new StaticObject("TreeA", new Vector2(92f, 88f), new Vector2(18f, 17f), 0f, "ggj地图补充/树1.png", false),
                new StaticObject("TreeB", new Vector2(-95f, 86f), new Vector2(15f, 22f), 0f, "ggj地图补充/树2.png", false)
            }
        );
    }

    private static LevelConfig CreateClassicLargeConfig()
    {
        Rect b = new Rect(-170f, -135f, 340f, 270f);
        return new LevelConfig(
            b,
            260,
            28f,
            42f,
            28f,
            86f,
            false,
            "ggj/通用/草地黄.png",
            null,
            new[]
            {
                new Vector2(b.xMin - 42f, b.yMin + 72f),
                new Vector2(b.xMin + 42f, b.yMin + 55f),
                new Vector2(b.xMin + 98f, b.yMin + 62f),
                new Vector2(b.xMin + 136f, b.yMin + 102f),
                new Vector2(b.xMin + 202f, b.yMin + 116f),
                new Vector2(b.xMin + 260f, b.yMin + 102f),
                new Vector2(b.xMax + 42f, b.yMin + 122f)
            },
            new[]
            {
                new WaterBody(new Vector2(-124f, -116f), new Vector2(46f, 28f), "ggj地图补充/湖3.png"),
                new WaterBody(new Vector2(124f, -104f), new Vector2(56f, 30f), "ggj地图补充/湖1.png")
            },
            new[]
            {
                new StaticObject("StoneA", new Vector2(-132f, 70f), new Vector2(48f, 22f), -8f, "ggj/通用/长条石头2.png", true),
                new StaticObject("StoneB", new Vector2(58f, 76f), new Vector2(52f, 22f), 8f, "ggj/通用/长条石头3.png", true),
                new StaticObject("StoneC", new Vector2(142f, 46f), new Vector2(42f, 28f), -10f, "ggj/通用/石头5.png", true),
                new StaticObject("StoneD", new Vector2(-28f, 24f), new Vector2(62f, 18f), 5f, "ggj/通用/长条石头1.png", true)
            },
            new[]
            {
                new StaticObject("TreeA", new Vector2(-138f, 88f), new Vector2(23f, 22f), 0f, "ggj地图补充/树3.png", false),
                new StaticObject("TreeB", new Vector2(136f, 92f), new Vector2(21f, 24f), 0f, "ggj地图补充/树4.png", false),
                new StaticObject("TreeC", new Vector2(-130f, -100f), new Vector2(21f, 26f), 0f, "ggj地图补充/树5.png", false),
                new StaticObject("ReedA", new Vector2(147f, -98f), new Vector2(16f, 18f), 0f, "ggj/通用/芦苇2.png", false),
                new StaticObject("ReedB", new Vector2(-96f, -105f), new Vector2(14f, 17f), 0f, "ggj/通用/芦苇1.png", false)
            }
        );
    }

    private static LevelConfig CreateTimedChallengeConfig()
    {
        Rect b = new Rect(-125f, -95f, 250f, 190f);
        return new LevelConfig(
            b,
            120,
            18f,
            30f,
            18f,
            58f,
            false,
            "ggj/通用/草地绿.png",
            null,
            new[]
            {
                new Vector2(b.xMin - 32f, b.yMax + 36f),
                new Vector2(b.xMin + 18f, b.yMax - 8f),
                new Vector2(b.xMin + 8f, b.yMin + 62f),
                new Vector2(b.xMin + 34f, b.yMin - 24f),
                new Vector2(b.xMin + 112f, b.yMin - 20f),
                new Vector2(b.xMin + 162f, b.yMin + 54f),
                new Vector2(b.xMin + 152f, b.yMax + 38f)
            },
            new WaterBody[0],
            new[]
            {
                new StaticObject("StoneA", new Vector2(-80f, 54f), new Vector2(54f, 18f), -8f, "ggj/通用/长条石头2.png", true),
                new StaticObject("StoneB", new Vector2(94f, 58f), new Vector2(52f, 18f), 12f, "ggj/通用/长条石头3.png", true),
                new StaticObject("StoneC", new Vector2(-18f, -44f), new Vector2(34f, 26f), -12f, "ggj/通用/石头10.png", true),
                new StaticObject("StoneD", new Vector2(82f, -52f), new Vector2(34f, 24f), 0f, "ggj/通用/石头7.png", true)
            },
            new[]
            {
                new StaticObject("TreeA", new Vector2(-98f, 68f), new Vector2(18f, 22f), 0f, "ggj地图补充/树6.png", false),
                new StaticObject("TreeB", new Vector2(108f, 70f), new Vector2(20f, 18f), 0f, "ggj地图补充/树7.png", false),
                new StaticObject("ReedA", new Vector2(112f, 36f), new Vector2(12f, 15f), 0f, "ggj/通用/芦苇1.png", false)
            },
            timeLimitSeconds: TimedChallengeDuration
        );
    }

    private static LevelConfig CreateEndlessConfig()
    {
        Rect b = new Rect(-150f, -120f, 300f, 240f);
        return new LevelConfig(
            b,
            130,
            22f,
            38f,
            24f,
            72f,
            true,
            "ggj/通用/草地黄.png",
            null,
            new[]
            {
                new Vector2(b.xMin - 42f, b.yMax - 26f),
                new Vector2(b.xMin + 20f, b.yMax - 62f),
                new Vector2(b.xMin + 78f, b.yMax - 70f),
                new Vector2(b.xMin + 118f, b.yMax - 36f),
                new Vector2(b.xMin + 150f, b.yMax - 8f),
                new Vector2(b.xMin + 204f, b.yMax - 28f),
                new Vector2(b.xMax + 42f, b.yMax - 56f)
            },
            new[]
            {
                new WaterBody(new Vector2(0f, -4f), new Vector2(58f, 30f), "ggj地图补充/湖1.png")
            },
            new StaticObject[0],
            new[]
            {
                new StaticObject("TreeA", new Vector2(-122f, 88f), new Vector2(22f, 20f), 0f, "ggj地图补充/树8.png", false),
                new StaticObject("TreeB", new Vector2(126f, 82f), new Vector2(24f, 20f), 0f, "ggj地图补充/树9.png", false),
                new StaticObject("ReedA", new Vector2(22f, 4f), new Vector2(15f, 18f), 0f, "ggj/通用/芦苇2.png", false),
                new StaticObject("ReedB", new Vector2(-28f, -6f), new Vector2(14f, 17f), 0f, "ggj/通用/芦苇1.png", false)
            },
            8,
            1.8f,
            260
        );
    }

    private static void CreateGround()
    {
        GameObject ground = CreateObject("Ground");
        SpriteRenderer renderer = ground.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeArt.LoadSprite(currentConfig.GroundSpritePath) ?? GetSquareSprite();
        renderer.sortingLayerName = GroundSortingLayer;
        renderer.sortingOrder = 0;
        ground.transform.position = mapBounds.center;
        ScaleRendererToSize(renderer, mapBounds.size);
    }

    private static void CreateBoundsWalls()
    {
        const float thickness = 4f;

        CreateBoundsWall("Wall_Left", new Vector2(mapBounds.xMin - thickness * 0.5f, mapBounds.center.y), new Vector2(thickness, mapBounds.height + thickness * 2f));
        CreateBoundsWall("Wall_Right", new Vector2(mapBounds.xMax + thickness * 0.5f, mapBounds.center.y), new Vector2(thickness, mapBounds.height + thickness * 2f));
        CreateBoundsWall("Wall_Top", new Vector2(mapBounds.center.x, mapBounds.yMax + thickness * 0.5f), new Vector2(mapBounds.width + thickness * 2f, thickness));
        CreateBoundsWall("Wall_Bottom", new Vector2(mapBounds.center.x, mapBounds.yMin - thickness * 0.5f), new Vector2(mapBounds.width + thickness * 2f, thickness));
    }

    private static void CreateBoundsWall(string name, Vector2 position, Vector2 size)
    {
        GameObject wall = CreateObject(name);
        wall.layer = ObstacleLayer;
        wall.transform.position = position;

        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
    }

    private static void CreateBoundaryArt()
    {
        CreateSpriteObject("NorthMountain", "ggj地图补充/山1.png", new Vector2(mapBounds.center.x, mapBounds.yMax + 18f), new Vector2(mapBounds.width, 70f), 0f, BackgroundSortingLayer, 4, false, Color.white);
        CreateSpriteObject("SouthMountain", "ggj地图补充/山2.png", new Vector2(mapBounds.center.x, mapBounds.yMin - 18f), new Vector2(mapBounds.width, 64f), 180f, BackgroundSortingLayer, 4, false, Color.white);
    }

    private static void CreateRiver()
    {
        EnsureSmoothRiverPoints();

        if (CreateRiverArt())
        {
            return;
        }

        CreateRiverStripVisual();
        for (int i = 0; i < riverSmoothPoints.Length - 1; i++)
        {
            CreateRiverCollider(riverSmoothPoints[i], riverSmoothPoints[i + 1]);
        }
    }

    private static bool CreateRiverArt()
    {
        if (string.IsNullOrEmpty(currentConfig.RiverSpritePath)) return false;

        Sprite sprite = RuntimeArt.LoadSprite(currentConfig.RiverSpritePath);
        Texture2D texture = RuntimeArt.LoadTexture(currentConfig.RiverSpritePath);
        if (sprite == null) return false;

        GameObject riverArt = CreateObject("RiverArt");
        riverArt.layer = RiverLayer;
        SpriteRenderer renderer = riverArt.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = GroundSortingLayer;
        renderer.sortingOrder = 2;
        riverArt.transform.position = mapBounds.center;
        ScaleRendererToSize(renderer, GetAspectFitSize(mapBounds.size, sprite.bounds.size));

        BoxCollider2D collider = riverArt.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = sprite.bounds.size;

        RiverWaterMask mask = riverArt.AddComponent<RiverWaterMask>();
        mask.Configure(renderer, texture);
        currentRiverMask = mask;

        RiverCollector collector = riverArt.AddComponent<RiverCollector>();
        collector.SetWaterMask(mask);

        CreateRiverFlowOverlay();
        return true;
    }

    private static void CreateRiverFlowOverlay()
    {
        if (currentRiverMask == null) return;

        GameObject overlayRoot = CreateObject("RiverFlowOverlay");
        Vector2 direction = new Vector2(1f, 0.28f).normalized;

        for (int i = 0; i < 28; i++)
        {
            GameObject line = new GameObject($"FlowLine_{i + 1}");
            line.transform.SetParent(overlayRoot.transform, false);

            SpriteRenderer renderer = line.AddComponent<SpriteRenderer>();
            renderer.sprite = GetFlowLineSprite();
            renderer.color = new Color(0.78f, 0.94f, 1f, Random.Range(0.18f, 0.34f));
            renderer.sortingLayerName = GroundSortingLayer;
            renderer.sortingOrder = 3;
            ScaleRendererToSize(renderer, new Vector2(Random.Range(16f, 34f), Random.Range(1.4f, 2.4f)));

            WaterFlowLine flow = line.AddComponent<WaterFlowLine>();
            flow.Configure(currentRiverMask, mapBounds, direction, Random.Range(1.4f, 3.1f), Random.Range(4.5f, 9f));
        }
    }

    private static void CreateRiverStripVisual()
    {
        GameObject bankObject = CreateObject("RiverBank");
        MeshFilter bankFilter = bankObject.AddComponent<MeshFilter>();
        MeshRenderer bankRenderer = bankObject.AddComponent<MeshRenderer>();
        bankFilter.sharedMesh = CreateRiverStripMesh(riverWidth + 8f, 1f);
        bankRenderer.sharedMaterial = GetBankMaterial();
        bankRenderer.sortingLayerName = GroundSortingLayer;
        bankRenderer.sortingOrder = 2;

        GameObject waterObject = CreateObject("River");
        waterObject.layer = RiverLayer;
        MeshFilter waterFilter = waterObject.AddComponent<MeshFilter>();
        MeshRenderer waterRenderer = waterObject.AddComponent<MeshRenderer>();
        waterFilter.sharedMesh = CreateRiverStripMesh(riverWidth, 1f);
        waterRenderer.sharedMaterial = GetWaterMaterial();
        waterRenderer.sortingLayerName = GroundSortingLayer;
        waterRenderer.sortingOrder = 3;
        waterObject.AddComponent<WaterFlow>();
    }

    private static void CreatePonds()
    {
        for (int i = 0; i < currentConfig.Ponds.Length; i++)
        {
            WaterBody pond = currentConfig.Ponds[i];
            CreateSpriteObject($"Pond_{i + 1}", pond.SpritePath, pond.Position, pond.Size, 0f, GroundSortingLayer, 4, false, Color.white);

            GameObject trigger = CreateObject($"PondCollector_{i + 1}");
            trigger.layer = RiverLayer;
            trigger.transform.position = pond.Position;
            trigger.transform.localScale = new Vector3(pond.Size.x * 0.62f, pond.Size.y * 0.42f, 1f);

            CircleCollider2D collider = trigger.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;
            trigger.AddComponent<RiverCollector>();
        }
    }

    private static void CreateObstacles()
    {
        for (int i = 0; i < currentConfig.Obstacles.Length; i++)
        {
            StaticObject obstacle = currentConfig.Obstacles[i];
            if (!obstacle.Blocks) continue;

            GameObject obj = CreateSpriteObject(obstacle.Name, obstacle.SpritePath, obstacle.Position, obstacle.Size, obstacle.Rotation, ActorSortingLayer, 1000, true, Color.white);
            obj.layer = ObstacleLayer;
            AddStoneCollider(obj, obstacle.Size);
        }
    }

    private static void CreateDecorations()
    {
        for (int i = 0; i < currentConfig.Decorations.Length; i++)
        {
            StaticObject decoration = currentConfig.Decorations[i];
            CreateSpriteObject(decoration.Name, decoration.SpritePath, decoration.Position, decoration.Size, decoration.Rotation, ActorSortingLayer, 960, true, Color.white);
        }
    }

    private static void CreateScatters(int count)
    {
        for (int i = 0; i < count; i++)
        {
            CreateScatter();
        }
    }

    private static void CreateScatter()
    {
        Vector2 position = GetRandomScatterPosition();

        GameObject scatter = CreateObject("Leaf");
        SpriteRenderer renderer = scatter.AddComponent<SpriteRenderer>();
        Rigidbody2D body = scatter.AddComponent<Rigidbody2D>();

        scatter.layer = LeafLayer;
        scatter.transform.position = new Vector3(position.x, position.y, 0f);
        scatter.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        renderer.sprite = RuntimeArt.LoadSprite(LeafSprites[Random.Range(0, LeafSprites.Length)]) ?? GetCircleSprite();
        renderer.color = Color.white;
        renderer.sortingLayerName = ActorSortingLayer;

        Vector2 size = new Vector2(Random.Range(6.6f, 9.2f), Random.Range(5.6f, 8.4f));
        ScaleRendererToSize(renderer, size);

        YSort sort = scatter.AddComponent<YSort>();
        sort.Configure(ActorSortingLayer, 1000, size.y * 0.5f, true);

        float weight = Random.Range(0.45f, 1.05f);
        body.gravityScale = 0f;
        body.drag = Random.Range(0.35f, 0.75f);
        body.angularDrag = 1.2f;
        body.mass = weight;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CircleCollider2D collider = scatter.AddComponent<CircleCollider2D>();
        collider.radius = 0.42f;

        Windable windable = scatter.AddComponent<Windable>();
        windable.Configure(weight);
    }

    private static Vector2 GetRandomScatterPosition()
    {
        for (int i = 0; i < 220; i++)
        {
            Vector2 position = new Vector2(
                Random.Range(mapBounds.xMin + 6f, mapBounds.xMax - 6f),
                Random.Range(mapBounds.yMin + 6f, mapBounds.yMax - 6f)
            );

            if (!IsBlocked(position)) return position;
        }

        return mapBounds.center;
    }

    private static bool IsBlocked(Vector2 position)
    {
        if (riverSmoothPoints == null) EnsureSmoothRiverPoints();

        if (currentRiverMask != null)
        {
            if (currentRiverMask.ContainsWater(position))
            {
                return true;
            }
        }
        else
        {
            for (int i = 0; i < riverSmoothPoints.Length - 1; i++)
            {
                if (DistanceToSegment(position, riverSmoothPoints[i], riverSmoothPoints[i + 1]) < riverWidth * 0.52f)
                {
                    return true;
                }
            }
        }

        for (int i = 0; i < currentConfig.Ponds.Length; i++)
        {
            WaterBody pond = currentConfig.Ponds[i];
            Vector2 delta = position - pond.Position;
            float x = delta.x / Mathf.Max(1f, pond.Size.x * 0.35f);
            float y = delta.y / Mathf.Max(1f, pond.Size.y * 0.25f);
            if (x * x + y * y < 1.2f) return true;
        }

        for (int i = 0; i < currentConfig.Obstacles.Length; i++)
        {
            StaticObject obstacle = currentConfig.Obstacles[i];
            if (!obstacle.Blocks) continue;

            if (Vector2.Distance(position, obstacle.Position) < Mathf.Max(obstacle.Size.x, obstacle.Size.y) * 0.52f + 2f)
            {
                return true;
            }
        }

        return false;
    }

    private static void CreateWindBlower()
    {
        GameObject windObject = CreateObject("WindBlower");
        WindBlower blower = windObject.AddComponent<WindBlower>();
        blower.ConfigureLayer(1 << LeafLayer);
    }

    private static void SetupCamera()
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.transform.position = new Vector3(mapBounds.center.x, mapBounds.center.y, -10f);
        camera.orthographic = true;
        camera.backgroundColor = Theme.Sky;

        GameCameraController controller = camera.GetComponent<GameCameraController>();
        if (controller == null)
        {
            controller = camera.gameObject.AddComponent<GameCameraController>();
        }

        controller.SetBounds(mapBounds, currentConfig.MinCameraSize, currentConfig.MaxCameraSize, currentConfig.InitialCameraSize);
    }

    private static void AddStoneCollider(GameObject obj, Vector2 worldSize)
    {
        PolygonCollider2D collider = obj.AddComponent<PolygonCollider2D>();
        collider.pathCount = 1;
        collider.SetPath(0, CreateLocalEllipsePath(obj.transform, worldSize, 0.94f, 0.82f, 14));
    }

    private static Vector2[] CreateLocalEllipsePath(Transform target, Vector2 worldSize, float widthRatio, float heightRatio, int segments)
    {
        Vector3 scale = target.localScale;
        float localRadiusX = worldSize.x * widthRatio * 0.5f / Mathf.Max(0.001f, Mathf.Abs(scale.x));
        float localRadiusY = worldSize.y * heightRatio * 0.5f / Mathf.Max(0.001f, Mathf.Abs(scale.y));

        Vector2[] points = new Vector2[Mathf.Max(8, segments)];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)points.Length * Mathf.PI * 2f;
            points[i] = new Vector2(Mathf.Cos(angle) * localRadiusX, Mathf.Sin(angle) * localRadiusY);
        }

        return points;
    }

    private static GameObject CreateSpriteObject(
        string objectName,
        string spritePath,
        Vector2 position,
        Vector2 size,
        float rotation,
        string sortingLayer,
        int sortingOrder,
        bool ySort,
        Color color)
    {
        GameObject obj = CreateObject(objectName);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = RuntimeArt.LoadSprite(spritePath) ?? GetCircleSprite();
        renderer.color = color;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = sortingOrder;

        obj.transform.position = new Vector3(position.x, position.y, 0f);
        obj.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        ScaleRendererToSize(renderer, size);

        if (ySort)
        {
            YSort sort = obj.AddComponent<YSort>();
            sort.Configure(sortingLayer, sortingOrder, size.y * 0.5f, false);
        }

        return obj;
    }

    private static void ScaleRendererToSize(SpriteRenderer renderer, Vector2 worldSize)
    {
        if (renderer.sprite == null)
        {
            renderer.transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);
            return;
        }

        Vector2 spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.001f, spriteSize.x),
            worldSize.y / Mathf.Max(0.001f, spriteSize.y),
            1f
        );
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

    private static void EnsureSmoothRiverPoints()
    {
        const int SubdivisionsPerSegment = 8;

        if (riverPoints == null || riverPoints.Length < 2)
        {
            riverSmoothPoints = riverPoints;
            return;
        }

        List<Vector2> result = new List<Vector2>(riverPoints.Length * SubdivisionsPerSegment);
        for (int i = 0; i < riverPoints.Length - 1; i++)
        {
            Vector2 p0 = riverPoints[Mathf.Max(0, i - 1)];
            Vector2 p1 = riverPoints[i];
            Vector2 p2 = riverPoints[i + 1];
            Vector2 p3 = riverPoints[Mathf.Min(riverPoints.Length - 1, i + 2)];

            for (int j = 0; j < SubdivisionsPerSegment; j++)
            {
                float t = j / (float)SubdivisionsPerSegment;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        result.Add(riverPoints[riverPoints.Length - 1]);
        riverSmoothPoints = result.ToArray();
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private static void CreateRiverCollider(Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        GameObject segment = CreateObject("RiverCollider");
        segment.layer = RiverLayer;
        segment.transform.position = (start + end) * 0.5f;
        segment.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        segment.transform.localScale = new Vector3(direction.magnitude, riverWidth * 0.92f, 1f);

        BoxCollider2D collider = segment.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        segment.AddComponent<RiverCollector>();
    }

    private static Mesh CreateRiverStripMesh(float width, float uvTiling)
    {
        Vector2[] line = riverSmoothPoints;
        int n = line.Length;
        Vector3[] vertices = new Vector3[n * 2];
        Vector2[] uvs = new Vector2[n * 2];
        int[] triangles = new int[(n - 1) * 6];

        for (int i = 0; i < n; i++)
        {
            Vector2 forward;
            if (i == 0) forward = (line[1] - line[0]).normalized;
            else if (i == n - 1) forward = (line[n - 1] - line[n - 2]).normalized;
            else forward = (line[i + 1] - line[i - 1]).normalized;

            Vector2 right = new Vector2(-forward.y, forward.x);
            vertices[i * 2] = line[i] - right * width * 0.5f;
            vertices[i * 2 + 1] = line[i] + right * width * 0.5f;

            uvs[i * 2] = new Vector2(i / (float)(n - 1) * uvTiling, 0f);
            uvs[i * 2 + 1] = new Vector2(i / (float)(n - 1) * uvTiling, 1f);
        }

        for (int i = 0; i < n - 1; i++)
        {
            int v0 = i * 2;
            int v1 = i * 2 + 1;
            int v2 = (i + 1) * 2;
            int v3 = (i + 1) * 2 + 1;
            int t = i * 6;
            triangles[t] = v0;
            triangles[t + 1] = v2;
            triangles[t + 2] = v1;
            triangles[t + 3] = v1;
            triangles[t + 4] = v2;
            triangles[t + 5] = v3;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static Material GetBankMaterial()
    {
        if (bankMaterial == null)
        {
            bankMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        bankMaterial.mainTexture = RuntimeArt.LoadTexture("ggj/通用/草地黄.png") ?? GetSolidTexture(Theme.Bank);
        bankMaterial.color = Theme.Bank;
        return bankMaterial;
    }

    private static Material GetWaterMaterial()
    {
        if (waterMaterial == null)
        {
            waterMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        waterMaterial.mainTexture = GetWaterTexture();
        waterMaterial.color = Color.white;
        waterMaterial.mainTextureScale = new Vector2(6f, 1f);
        return waterMaterial;
    }

    private static Texture2D GetSolidTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    private static Texture2D GetWaterTexture()
    {
        if (generatedWaterTexture != null) return generatedWaterTexture;

        const int w = 192, h = 32;
        Texture2D tex = new Texture2D(w, h);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;
                float v = y / (float)h;
                float edge = Mathf.Min(v, 1f - v) * 2f;
                float wave = Mathf.Sin(u * Mathf.PI * 8f + v * 2f) * 0.5f + 0.5f;
                float foam = Mathf.SmoothStep(0.0f, 0.18f, 1f - edge) * 0.35f;
                float tone = Mathf.Lerp(0.85f, 1.0f, wave);
                Color c = Color.Lerp(Theme.WaterFoam, Theme.Water, Mathf.Clamp01(tone - foam));
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        generatedWaterTexture = tex;
        return generatedWaterTexture;
    }

    private static Sprite GetFlowLineSprite()
    {
        if (flowLineSprite != null) return flowLineSprite;

        const int w = 96;
        const int h = 18;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1);
                float v = y / (float)(h - 1);
                float center = 0.5f + Mathf.Sin(u * Mathf.PI * 2.2f) * 0.18f;
                float distance = Mathf.Abs(v - center);
                float taper = Mathf.SmoothStep(0f, 0.2f, u) * (1f - Mathf.SmoothStep(0.78f, 1f, u));
                float alpha = Mathf.Clamp01(1f - distance / 0.16f) * taper;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        flowLineSprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 32f);
        flowLineSprite.name = "GeneratedFlowLine";
        return flowLineSprite;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float t = Vector2.Dot(point - start, segment) / segment.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return Vector2.Distance(point, start + segment * t);
    }

    private static GameObject CreateObject(string objectName)
    {
        GameObject created = new GameObject(objectName);

        if (gameplayRoot != null)
        {
            created.transform.SetParent(gameplayRoot.transform);
        }

        return created;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite == null)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        return squareSprite;
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite == null)
        {
            int size = 32;
            Texture2D texture = new Texture2D(size, size);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size * 0.45f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        return circleSprite;
    }

    private readonly struct WaterBody
    {
        public readonly Vector2 Position;
        public readonly Vector2 Size;
        public readonly string SpritePath;

        public WaterBody(Vector2 position, Vector2 size, string spritePath)
        {
            Position = position;
            Size = size;
            SpritePath = spritePath;
        }
    }

    private readonly struct StaticObject
    {
        public readonly string Name;
        public readonly Vector2 Position;
        public readonly Vector2 Size;
        public readonly float Rotation;
        public readonly string SpritePath;
        public readonly bool Blocks;

        public StaticObject(string name, Vector2 position, Vector2 size, float rotation, string spritePath, bool blocks)
        {
            Name = name;
            Position = position;
            Size = size;
            Rotation = rotation;
            SpritePath = spritePath;
            Blocks = blocks;
        }
    }

    private sealed class LevelConfig
    {
        public readonly Rect MapBounds;
        public readonly int ScatterCount;
        public readonly float RiverWidth;
        public readonly float InitialCameraSize;
        public readonly float MinCameraSize;
        public readonly float MaxCameraSize;
        public readonly bool Endless;
        public readonly string GroundSpritePath;
        public readonly string RiverSpritePath;
        public readonly Vector2[] RiverPoints;
        public readonly WaterBody[] Ponds;
        public readonly StaticObject[] Obstacles;
        public readonly StaticObject[] Decorations;
        public readonly int EndlessSpawnBatch;
        public readonly float EndlessSpawnInterval;
        public readonly int EndlessMaxScatters;
        public readonly float TimeLimitSeconds;

        public LevelConfig(
            Rect mapBounds,
            int scatterCount,
            float riverWidth,
            float initialCameraSize,
            float minCameraSize,
            float maxCameraSize,
            bool endless,
            string groundSpritePath,
            string riverSpritePath,
            Vector2[] riverPoints,
            WaterBody[] ponds,
            StaticObject[] obstacles,
            StaticObject[] decorations,
            int endlessSpawnBatch = 0,
            float endlessSpawnInterval = 0f,
            int endlessMaxScatters = 0,
            float timeLimitSeconds = 0f)
        {
            MapBounds = mapBounds;
            ScatterCount = scatterCount;
            RiverWidth = riverWidth;
            InitialCameraSize = initialCameraSize;
            MinCameraSize = minCameraSize;
            MaxCameraSize = maxCameraSize;
            Endless = endless;
            GroundSpritePath = groundSpritePath;
            RiverSpritePath = riverSpritePath;
            RiverPoints = riverPoints;
            Ponds = ponds;
            Obstacles = obstacles;
            Decorations = decorations;
            EndlessSpawnBatch = endlessSpawnBatch;
            EndlessSpawnInterval = endlessSpawnInterval;
            EndlessMaxScatters = endlessMaxScatters;
            TimeLimitSeconds = timeLimitSeconds;
        }
    }
}
