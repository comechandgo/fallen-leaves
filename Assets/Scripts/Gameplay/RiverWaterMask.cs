using UnityEngine;

[DisallowMultipleComponent]
public sealed class RiverWaterMask : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Texture2D sourceTexture;
    [SerializeField, Range(0f, 1f)] private float minBlue = 0.56f;
    [SerializeField, Range(0f, 1f)] private float minGreen = 0.42f;
    [SerializeField, Range(0f, 1f)] private float minBlueBiasOverRed = 0.11f;

    public void Configure(SpriteRenderer renderer, Texture2D texture)
    {
        targetRenderer = renderer;
        sourceTexture = texture;
    }

    public bool ContainsWater(Vector2 worldPosition)
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (targetRenderer == null || targetRenderer.sprite == null)
        {
            return false;
        }

        if (sourceTexture == null)
        {
            sourceTexture = targetRenderer.sprite.texture;
        }

        if (sourceTexture == null || !TryWorldToUv(worldPosition, out Vector2 uv))
        {
            return false;
        }

        return IsWaterColor(sourceTexture.GetPixelBilinear(uv.x, uv.y));
    }

    private bool TryWorldToUv(Vector2 worldPosition, out Vector2 uv)
    {
        Bounds spriteBounds = targetRenderer.sprite.bounds;
        Vector2 local = targetRenderer.transform.InverseTransformPoint(worldPosition);

        float u = Mathf.InverseLerp(spriteBounds.min.x, spriteBounds.max.x, local.x);
        float v = Mathf.InverseLerp(spriteBounds.min.y, spriteBounds.max.y, local.y);
        uv = new Vector2(u, v);

        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }

    private bool IsWaterColor(Color color)
    {
        if (color.a <= 0.05f)
        {
            return false;
        }

        return color.b >= minBlue
            && color.g >= minGreen
            && color.b - color.r >= minBlueBiasOverRed;
    }
}
