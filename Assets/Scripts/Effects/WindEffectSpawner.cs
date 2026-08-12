using UnityEngine;

public sealed class WindEffectSpawner : MonoBehaviour
{
    private const string LibraryResourcePath = "WindEffectLibrary";

    private const string SortingLayerName = "Foreground";
    private const int SortingOrder = 200;
    private const float Alpha = 1f;

    private const float DownburstDiameterMultiplier = 1.00f;
    private const float DownburstYOffsetByDiameter = 0.00f;

    private const float SurfaceLengthMultiplier = 1.00f;
    private const float SurfaceWidthMultiplier = 1.00f;
    private const float SurfaceForwardOffsetByLength = 0.50f;

    private const float TornadoDiameterMultiplier = 1.00f;
    private const float TornadoYOffsetByDiameter = 0.00f;

    private WindEffectLibrary library;
    private GameObject activeEffect;
    private WindShape activeForm;

    public void Play(
        WindShape form,
        Vector2 center,
        Vector2 direction,
        float radius,
        float surfaceLength,
        float surfaceStartWidth,
        float surfaceEndWidth)
    {
        if (!TryEnsureLibrary())
        {
            return;
        }

        Sprite[] frames = library.GetFrames(form);

        if (frames == null || frames.Length == 0)
        {
            return;
        }

        if (activeEffect != null)
        {
            if (activeForm == form)
            {
                return;
            }

            Destroy(activeEffect);
        }

        GameObject effect = new GameObject("WindEffect_" + form);
        effect.transform.SetParent(transform, true);
        activeEffect = effect;
        activeForm = form;

        SpriteRenderer spriteRenderer = effect.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingLayerName = SortingLayerName;
        spriteRenderer.sortingOrder = SortingOrder;
        spriteRenderer.color = new Color(1f, 1f, 1f, Alpha);
        spriteRenderer.sprite = frames[0];

        ApplyTransform(
            effect.transform,
            spriteRenderer.sprite,
            form,
            center,
            direction,
            radius,
            surfaceLength,
            surfaceStartWidth,
            surfaceEndWidth);

        WindEffectFramePlayer player = effect.AddComponent<WindEffectFramePlayer>();
        player.Play(frames, library.GetFps(form), true);
    }

    public void StopActiveEffect()
    {
        if (activeEffect != null)
        {
            Destroy(activeEffect);
        }

        activeEffect = null;
    }

    private bool TryEnsureLibrary()
    {
        if (library != null)
        {
            return true;
        }

        library = Resources.Load<WindEffectLibrary>(LibraryResourcePath);
        return library != null;
    }

    private void ApplyTransform(
        Transform effectTransform,
        Sprite sprite,
        WindShape form,
        Vector2 center,
        Vector2 direction,
        float radius,
        float surfaceLength,
        float surfaceStartWidth,
        float surfaceEndWidth)
    {
        Vector2 spriteSize = GetSpriteSize(sprite);

        switch (form)
        {
            case WindShape.Surface:
                ApplySurfaceTransform(
                    effectTransform,
                    spriteSize,
                    center,
                    direction,
                    surfaceLength,
                    surfaceStartWidth,
                    surfaceEndWidth);
                break;

            case WindShape.Tornado:
                ApplyCircleTransform(
                    effectTransform,
                    spriteSize,
                    center,
                    radius,
                    TornadoDiameterMultiplier,
                    TornadoYOffsetByDiameter);
                break;

            default:
                ApplyCircleTransform(
                    effectTransform,
                    spriteSize,
                    center,
                    radius,
                    DownburstDiameterMultiplier,
                    DownburstYOffsetByDiameter);
                break;
        }
    }

    private void ApplyCircleTransform(
        Transform effectTransform,
        Vector2 spriteSize,
        Vector2 center,
        float radius,
        float diameterMultiplier,
        float yOffsetByDiameter)
    {
        float targetDiameter = radius * 2f * diameterMultiplier;

        float scaleX = targetDiameter / Mathf.Max(0.0001f, spriteSize.x);
        float scaleY = targetDiameter / Mathf.Max(0.0001f, spriteSize.y);

        Vector2 offset = Vector2.up * (targetDiameter * yOffsetByDiameter);

        effectTransform.position = new Vector3(center.x + offset.x, center.y + offset.y, 0f);
        effectTransform.rotation = Quaternion.identity;
        effectTransform.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    private void ApplySurfaceTransform(
        Transform effectTransform,
        Vector2 spriteSize,
        Vector2 center,
        Vector2 direction,
        float surfaceLength,
        float surfaceStartWidth,
        float surfaceEndWidth)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();

        float targetLength = surfaceLength * SurfaceLengthMultiplier;
        float targetWidth = Mathf.Max(surfaceStartWidth, surfaceEndWidth) * SurfaceWidthMultiplier;

        float scaleX = targetWidth / Mathf.Max(0.0001f, spriteSize.x);
        float scaleY = targetLength / Mathf.Max(0.0001f, spriteSize.y);

        Vector2 offset = direction * (targetLength * SurfaceForwardOffsetByLength);

        effectTransform.position = new Vector3(center.x + offset.x, center.y + offset.y, 0f);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        effectTransform.rotation = Quaternion.Euler(0f, 0f, angle);
        effectTransform.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    private Vector2 GetSpriteSize(Sprite sprite)
    {
        if (sprite == null)
        {
            return Vector2.one;
        }

        return sprite.bounds.size;
    }
}
