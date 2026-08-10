using UnityEngine;

[DisallowMultipleComponent]
public sealed class LevelRoot : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private LevelId levelId;

    [Header("Map")]
    [SerializeField] private Rect mapBounds = new Rect(-50f, -50f, 100f, 100f);
    [SerializeField, Min(1f)] private float initialCameraSize = 30f;
    [SerializeField, Min(1f)] private float minCameraSize = 18f;
    [SerializeField, Min(1f)] private float maxCameraSize = 60f;

    [Header("Rules")]
    [SerializeField, Min(0)] private int initialLeafCount = 100;
    [SerializeField, Min(0f)] private float timeLimitSeconds;
    [SerializeField] private bool endless;
    [SerializeField, Min(0)] private int endlessSpawnBatch;
    [SerializeField, Min(0.05f)] private float endlessSpawnInterval = 1.8f;
    [SerializeField, Min(1)] private int endlessMaxLeaves = 260;

    [Header("Runtime Components")]
    [SerializeField] private GroundTilemapGenerator groundGenerator;
    [SerializeField] private LeafSpawner leafSpawner;
    [SerializeField] private WindBlower windBlower;

    private float endlessTimer;
    private bool initialized;

    public LevelId Id => levelId;
    public Rect MapBounds => mapBounds;
    public int InitialLeafCount => initialLeafCount;
    public float TimeLimitSeconds => timeLimitSeconds;
    public bool Endless => endless;
    public int EndlessSpawnBatch => endlessSpawnBatch;
    public float EndlessSpawnInterval => endlessSpawnInterval;
    public int EndlessMaxLeaves => endlessMaxLeaves;
    public WindBlower WindBlower => windBlower;
    public int ActiveLeafCount => leafSpawner != null ? leafSpawner.ActiveCount : 0;
    public bool IsGameplayClear => !endless && initialized && ActiveLeafCount == 0;

    public void InitializeRuntime()
    {
        if (initialized) return;

        if (groundGenerator == null) groundGenerator = GetComponentInChildren<GroundTilemapGenerator>(true);
        if (leafSpawner == null) leafSpawner = GetComponentInChildren<LeafSpawner>(true);
        if (windBlower == null) windBlower = GetComponentInChildren<WindBlower>(true);

        if (groundGenerator != null) groundGenerator.EnsureBuilt();

        Physics2D.SyncTransforms();

        if (leafSpawner != null)
        {
            leafSpawner.Initialize(this);
            leafSpawner.Spawn(initialLeafCount);
        }

        SetupCamera();
        endlessTimer = 0f;
        initialized = true;
    }

    public void Tick(float deltaTime)
    {
        if (!initialized || !endless || leafSpawner == null) return;

        endlessTimer += deltaTime;
        if (endlessTimer < endlessSpawnInterval) return;

        endlessTimer = 0f;
        int available = Mathf.Max(0, endlessMaxLeaves - leafSpawner.ActiveCount);
        if (available <= 0) return;

        leafSpawner.Spawn(Mathf.Min(endlessSpawnBatch, available));
    }

    public void Configure(
        LevelId id,
        Rect bounds,
        float cameraInitial,
        float cameraMin,
        float cameraMax,
        int leafCount,
        float timeLimit,
        bool isEndless,
        int spawnBatch,
        float spawnInterval,
        int maxLeaves,
        GroundTilemapGenerator ground,
        LeafSpawner spawner,
        WindBlower blower)
    {
        levelId = id;
        mapBounds = bounds;
        initialCameraSize = cameraInitial;
        minCameraSize = cameraMin;
        maxCameraSize = cameraMax;
        initialLeafCount = Mathf.Max(0, leafCount);
        timeLimitSeconds = Mathf.Max(0f, timeLimit);
        endless = isEndless;
        endlessSpawnBatch = Mathf.Max(0, spawnBatch);
        endlessSpawnInterval = Mathf.Max(0.05f, spawnInterval);
        endlessMaxLeaves = Mathf.Max(1, maxLeaves);
        groundGenerator = ground;
        leafSpawner = spawner;
        windBlower = blower;
    }

    private void SetupCamera()
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
        if (controller == null) controller = camera.gameObject.AddComponent<GameCameraController>();
        controller.SetBounds(mapBounds, minCameraSize, maxCameraSize, initialCameraSize);
    }
}
