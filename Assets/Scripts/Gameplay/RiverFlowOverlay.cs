using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RiverWaterMask), typeof(SpriteRenderer))]
public sealed class RiverFlowOverlay : MonoBehaviour
{
    [SerializeField, Min(1)] private int lineCount = 36;
    [SerializeField] private Vector2 flowDirection = new Vector2(1f, 0.28f);
    [SerializeField] private Vector2 lineLengthRange = new Vector2(6f, 14f);
    [SerializeField] private Vector2 lineThicknessRange = new Vector2(0.25f, 0.55f);
    [SerializeField] private Vector2 lineAlphaRange = new Vector2(0.14f, 0.26f);

    private Transform runtimeRoot;
    private Texture2D generatedTexture;
    private Sprite generatedSprite;

    public int LineCount => lineCount;

    public void Configure(int count)
    {
        lineCount = Mathf.Max(1, count);
    }

    private void Awake()
    {
        if (Application.isPlaying) BuildRuntimeLines();
    }

    private void BuildRuntimeLines()
    {
        RiverWaterMask waterMask = GetComponent<RiverWaterMask>();
        SpriteRenderer riverRenderer = GetComponent<SpriteRenderer>();
        if (waterMask == null || riverRenderer == null || riverRenderer.sprite == null) return;

        RiverSpriteShapeAdapter spriteShapeAdapter = GetComponent<RiverSpriteShapeAdapter>();
        if (spriteShapeAdapter != null)
        {
            spriteShapeAdapter.BuildRuntimeShape();
            riverRenderer.enabled = true;
        }

        LevelRoot level = GetComponentInParent<LevelRoot>();
        Transform parent = level != null ? level.transform : transform.parent;
        GameObject root = new GameObject("RiverFlowOverlayRuntime");
        root.transform.SetParent(parent, false);
        runtimeRoot = root.transform;

        generatedSprite = CreateFlowLineSprite();
        Bounds bounds = GetFlowBounds(riverRenderer);
        Rect spawnBounds = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        Vector2 direction = GetFlowDirection();
        MapPrototypeGizmos prototype = GetComponentInParent<MapPrototypeGizmos>();
        Vector2[] route = prototype != null ? prototype.RiverPoints : null;

        for (int i = 0; i < lineCount; i++)
        {
            GameObject line = new GameObject($"FlowLine_{i + 1}");
            line.transform.SetParent(runtimeRoot, false);

            SpriteRenderer renderer = line.AddComponent<SpriteRenderer>();
            renderer.sprite = generatedSprite;
            renderer.color = new Color(0.78f, 0.94f, 1f, Random.Range(lineAlphaRange.x, lineAlphaRange.y));
            renderer.sortingLayerID = riverRenderer.sortingLayerID;
            renderer.sortingOrder = riverRenderer.sortingOrder + 2;
            ScaleToWorldSize(renderer, new Vector2(
                Random.Range(lineLengthRange.x, lineLengthRange.y),
                Random.Range(lineThicknessRange.x, lineThicknessRange.y)));

            WaterFlowLine flow = line.AddComponent<WaterFlowLine>();
            if (route != null && route.Length > 1)
                flow.Configure(waterMask, spawnBounds, route, Random.Range(2.2f, 4.2f), Random.Range(4.5f, 8f));
            else
                flow.Configure(waterMask, spawnBounds, direction, Random.Range(2.2f, 4.2f), Random.Range(4.5f, 8f));
        }
    }

    private Bounds GetFlowBounds(SpriteRenderer fallbackRenderer)
    {
        return fallbackRenderer.bounds;
    }

    private Vector2 GetFlowDirection()
    {
        RiverImagePiece riverPiece = GetComponent<RiverImagePiece>();
        if (riverPiece != null)
        {
            Vector2 direction = riverPiece.WorldExit - riverPiece.WorldEntry;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction.normalized;
            }
        }

        return flowDirection.sqrMagnitude > 0.001f ? flowDirection.normalized : Vector2.right;
    }

    private Sprite CreateFlowLineSprite()
    {
        const int width = 96;
        const int height = 18;
        generatedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "GeneratedFlowLineTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float v = y / (float)(height - 1);
                float center = 0.5f + Mathf.Sin(u * Mathf.PI * 2.2f) * 0.18f;
                float distance = Mathf.Abs(v - center);
                float taper = Mathf.SmoothStep(0f, 0.2f, u) * (1f - Mathf.SmoothStep(0.78f, 1f, u));
                float alpha = Mathf.Clamp01(1f - distance / 0.16f) * taper;
                generatedTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        generatedTexture.Apply(false);
        Sprite sprite = Sprite.Create(
            generatedTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            32f);
        sprite.name = "GeneratedFlowLine";
        return sprite;
    }

    private static void ScaleToWorldSize(SpriteRenderer renderer, Vector2 worldSize)
    {
        Vector2 spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.001f, spriteSize.x),
            worldSize.y / Mathf.Max(0.001f, spriteSize.y),
            1f);
    }

    private void OnDestroy()
    {
        if (runtimeRoot != null) Destroy(runtimeRoot.gameObject);
        if (generatedSprite != null) Destroy(generatedSprite);
        if (generatedTexture != null) Destroy(generatedTexture);
    }
}
