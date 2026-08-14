using UnityEngine;

[DisallowMultipleComponent]
public sealed class LevelRoot : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private LevelId levelId;

    [Header("Map")]
    [SerializeField] private Rect mapBounds = new Rect(-50f, -50f, 100f, 100f);
    [SerializeField] private Vector2 cameraStart;
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

    [Header("Endless Survival")]
    [SerializeField, Min(0.01f)] private float endlessSurvivalMaximum = 100f;
    [SerializeField, Min(0f)] private float endlessSurvivalInitial = 100f;
    [SerializeField, Min(0f)] private float endlessSurvivalPerLeaf = 8f;
    [SerializeField, Min(0f)] private float endlessSurvivalBaseDrain = 2f;
    [SerializeField, Min(0.01f)] private float endlessSurvivalStageSeconds = 60f;
    [SerializeField, Min(1f)] private float endlessSurvivalStageMultiplier = 1.3f;

    [Header("Runtime Components")]
    [SerializeField] private GroundTilemapGenerator groundGenerator;
    [SerializeField] private LeafSpawner leafSpawner;
    [SerializeField] private WindBlower windBlower;

    private float endlessTimer;
    private float endlessSurvivalValue;
    private float endlessSurvivalElapsed;
    private bool endlessSurvivalDepleted;
    private bool initialized;

    public LevelId Id => levelId;
    public Rect MapBounds => mapBounds;
    public Vector2 CameraStart => cameraStart;
    public int InitialLeafCount => initialLeafCount;
    public float TimeLimitSeconds => timeLimitSeconds;
    public bool Endless => endless;
    public int EndlessSpawnBatch => endlessSpawnBatch;
    public float EndlessSpawnInterval => endlessSpawnInterval;
    public int EndlessMaxLeaves => endlessMaxLeaves;
    public float EndlessSurvivalMaximum => endlessSurvivalMaximum;
    public float EndlessSurvivalInitial => endlessSurvivalInitial;
    public float EndlessSurvivalPerLeaf => endlessSurvivalPerLeaf;
    public float EndlessSurvivalBaseDrain => endlessSurvivalBaseDrain;
    public float EndlessSurvivalStageSeconds => endlessSurvivalStageSeconds;
    public float EndlessSurvivalStageMultiplier => endlessSurvivalStageMultiplier;
    public float EndlessSurvivalValue => endlessSurvivalValue;
    public float EndlessSurvivalRatio => endless && endlessSurvivalMaximum > 0f
        ? Mathf.Clamp01(endlessSurvivalValue / endlessSurvivalMaximum)
        : 0f;
    public float CurrentEndlessDrainPerSecond => endless
        ? GetEndlessDrainPerSecond(endlessSurvivalElapsed)
        : 0f;
    public bool IsEndlessSurvivalDepleted => endless && IsReady && endlessSurvivalDepleted;
    public WindBlower WindBlower => windBlower;
    public int ActiveLeafCount => leafSpawner != null ? leafSpawner.ActiveCount : 0;
    public bool IsReady => initialized && (leafSpawner == null || leafSpawner.IsReady);
    public bool IsGameplayClear => !endless && IsReady && ActiveLeafCount == 0;

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
            leafSpawner.BeginInitialSpawn(initialLeafCount);
        }

        if (windBlower != null) windBlower.enabled = leafSpawner == null || leafSpawner.IsReady;

        SetupCamera();
        endlessTimer = 0f;
        endlessSurvivalElapsed = 0f;
        endlessSurvivalValue = endless
            ? Mathf.Clamp(endlessSurvivalInitial, 0f, endlessSurvivalMaximum)
            : 0f;
        endlessSurvivalDepleted = endless && endlessSurvivalValue <= 0f;
        initialized = true;
    }

    public void Tick(float deltaTime)
    {
        if (!initialized) return;

        if (leafSpawner != null && !leafSpawner.IsReady)
        {
            leafSpawner.TickInitialSpawn();
            if (!leafSpawner.IsReady) return;
            if (windBlower != null) windBlower.enabled = true;
        }

        if (!endless) return;

        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        DrainEndlessSurvival(safeDeltaTime);

        if (leafSpawner == null) return;

        endlessTimer += safeDeltaTime;
        if (endlessTimer < endlessSpawnInterval) return;

        endlessTimer = 0f;
        int available = Mathf.Max(0, endlessMaxLeaves - leafSpawner.ActiveCount);
        if (available <= 0) return;

        leafSpawner.Spawn(Mathf.Min(endlessSpawnBatch, available));
    }

    public void RestoreEndlessSurvival(int leafCount)
    {
        if (!initialized || !endless || leafCount <= 0 || endlessSurvivalPerLeaf <= 0f) return;

        endlessSurvivalValue = Mathf.Min(
            endlessSurvivalMaximum,
            endlessSurvivalValue + endlessSurvivalPerLeaf * leafCount);
        endlessSurvivalDepleted = endlessSurvivalValue <= 0f;
    }

    public void Configure(
        LevelId id,
        Rect bounds,
        Vector2 initialCameraPosition,
        float cameraInitial,
        float cameraMin,
        float cameraMax,
        int leafCount,
        float timeLimit,
        bool isEndless,
        int spawnBatch,
        float spawnInterval,
        int maxLeaves,
        float survivalMaximum,
        float survivalInitial,
        float survivalPerLeaf,
        float survivalBaseDrain,
        float survivalStageSeconds,
        float survivalStageMultiplier,
        GroundTilemapGenerator ground,
        LeafSpawner spawner,
        WindBlower blower)
    {
        levelId = id;
        mapBounds = bounds;
        cameraStart = initialCameraPosition;
        initialCameraSize = cameraInitial;
        minCameraSize = cameraMin;
        maxCameraSize = cameraMax;
        initialLeafCount = Mathf.Max(0, leafCount);
        timeLimitSeconds = Mathf.Max(0f, timeLimit);
        endless = isEndless;
        endlessSpawnBatch = Mathf.Max(0, spawnBatch);
        endlessSpawnInterval = Mathf.Max(0.05f, spawnInterval);
        endlessMaxLeaves = Mathf.Max(1, maxLeaves);
        endlessSurvivalMaximum = Mathf.Max(0.01f, survivalMaximum);
        endlessSurvivalInitial = Mathf.Clamp(survivalInitial, 0f, endlessSurvivalMaximum);
        endlessSurvivalPerLeaf = Mathf.Max(0f, survivalPerLeaf);
        endlessSurvivalBaseDrain = Mathf.Max(0f, survivalBaseDrain);
        endlessSurvivalStageSeconds = Mathf.Max(0.01f, survivalStageSeconds);
        endlessSurvivalStageMultiplier = Mathf.Max(1f, survivalStageMultiplier);
        groundGenerator = ground;
        leafSpawner = spawner;
        windBlower = blower;
    }

    private void OnEnable()
    {
        RiverCollector.LeavesCollected += RestoreEndlessSurvival;
    }

    private void OnDisable()
    {
        RiverCollector.LeavesCollected -= RestoreEndlessSurvival;
    }

    private void DrainEndlessSurvival(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        float remaining = deltaTime;
        while (remaining > 0f)
        {
            int stage = Mathf.FloorToInt(endlessSurvivalElapsed / endlessSurvivalStageSeconds);
            float nextBoundary = (stage + 1) * endlessSurvivalStageSeconds;
            float segment = Mathf.Min(remaining, Mathf.Max(0.0001f, nextBoundary - endlessSurvivalElapsed));

            endlessSurvivalValue = Mathf.Max(
                0f,
                endlessSurvivalValue - GetEndlessDrainPerSecond(endlessSurvivalElapsed) * segment);
            endlessSurvivalElapsed += segment;
            remaining -= segment;
        }

        endlessSurvivalDepleted = endlessSurvivalValue <= 0f;
    }

    private float GetEndlessDrainPerSecond(float elapsed)
    {
        int stage = Mathf.Max(0, Mathf.FloorToInt(elapsed / endlessSurvivalStageSeconds));
        return endlessSurvivalBaseDrain * Mathf.Pow(endlessSurvivalStageMultiplier, stage);
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

        camera.transform.position = new Vector3(cameraStart.x, cameraStart.y, -10f);
        camera.orthographic = true;
        camera.backgroundColor = Theme.Sky;

        GameCameraController controller = camera.GetComponent<GameCameraController>();
        if (controller == null) controller = camera.gameObject.AddComponent<GameCameraController>();
        if (camera.GetComponent<TreeCursorFadeController>() == null)
            camera.gameObject.AddComponent<TreeCursorFadeController>();
        controller.SetBounds(mapBounds, cameraStart, minCameraSize, maxCameraSize, initialCameraSize);
    }
}
