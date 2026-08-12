using UnityEngine;
using UnityEngine.U2D;

[DisallowMultipleComponent]
[RequireComponent(typeof(RiverWaterMask), typeof(SpriteRenderer))]
public sealed class RiverFlowOverlay : MonoBehaviour
{
    [SerializeField, Min(1)] private int lineCount = 28;
    [SerializeField] private Vector2 flowDirection = new Vector2(1f, 0.28f);

    private Transform runtimeRoot;
    private Texture2D generatedTexture;
    private Sprite generatedSprite;

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
        }

        LevelRoot level = GetComponentInParent<LevelRoot>();
        Transform parent = level != null ? level.transform : transform.parent;
        GameObject root = new GameObject("RiverFlowOverlayRuntime");
        root.transform.SetParent(parent, false);
        runtimeRoot = root.transform;

        generatedSprite = CreateFlowLineSprite();
        Bounds bounds = GetFlowBounds(riverRenderer);
        Rect spawnBounds = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        Vector2 direction = flowDirection.sqrMagnitude > 0.001f ? flowDirection.normalized : Vector2.right;

        for (int i = 0; i < lineCount; i++)
        {
            GameObject line = new GameObject($"FlowLine_{i + 1}");
            line.transform.SetParent(runtimeRoot, false);

            SpriteRenderer renderer = line.AddComponent<SpriteRenderer>();
            renderer.sprite = generatedSprite;
            renderer.color = new Color(0.78f, 0.94f, 1f, Random.Range(0.18f, 0.34f));
            renderer.sortingLayerName = "Ground";
            renderer.sortingOrder = 3;
            ScaleToWorldSize(renderer, new Vector2(Random.Range(16f, 34f), Random.Range(1.4f, 2.4f)));

            WaterFlowLine flow = line.AddComponent<WaterFlowLine>();
            flow.Configure(waterMask, spawnBounds, direction, Random.Range(1.4f, 3.1f), Random.Range(4.5f, 9f));
        }
    }

    private Bounds GetFlowBounds(SpriteRenderer fallbackRenderer)
    {
        SpriteShapeRenderer shapeRenderer = GetComponentInChildren<SpriteShapeRenderer>();
        if (shapeRenderer != null)
        {
            return shapeRenderer.bounds;
        }

        return fallbackRenderer.bounds;
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
