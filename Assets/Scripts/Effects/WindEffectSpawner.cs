using UnityEngine;
using UnityEngine.Rendering;

public sealed class WindEffectSpawner : MonoBehaviour
{
    private const int MaxLineCount = 4;
    private const int DownburstPointCount = 18;
    private const int SurfacePointCount = 20;
    private const int TornadoPointCount = 30;

    private const string SortingLayerName = "Object";
    private const int SortingOrder = 50;

    private const float PeakAlpha = 0.24f;
    private const float TargetPixelWidth = 1.25f;
    private const float MinWorldWidth = 0.025f;
    private const float MaxWorldWidth = 0.075f;
    private const float HoldDuration = 0.18f;
    private const float FadeDuration = 0.12f;

    private const float DownburstMinRadiusRatio = 0.25f;
    private const float DownburstMaxRadiusRatio = 0.90f;
    private const float TornadoMaxRadiusRatio = 0.86f;

    private static readonly Color WarmWhite = new Color32(0xF3, 0xE4, 0xC2, 0xFF);
    private static readonly int TintPropertyId = Shader.PropertyToID("_Color");
    private static readonly float[] SurfaceSideFactors = { -0.70f, -0.23f, 0.23f, 0.70f };

    private LineRenderer[] lines;
    private MaterialPropertyBlock[] propertyBlocks;
    private Material lineMaterial;
    private Camera cachedCamera;

    private bool effectVisible;
    private WindShape activeForm;
    private Vector2 activeCenter;
    private Vector2 activeDirection = Vector2.up;
    private float activeRadius;
    private float activeSurfaceLength;
    private float activeSurfaceStartWidth;
    private float activeSurfaceEndWidth;
    private float lastPlayTime;

    public void Play(
        WindShape form,
        Vector2 center,
        Vector2 direction,
        float radius,
        float surfaceLength,
        float surfaceStartWidth,
        float surfaceEndWidth)
    {
        if (!EnsureLines())
        {
            return;
        }

        if (effectVisible && activeForm != form)
        {
            HideAllLines();
        }

        activeForm = form;
        activeCenter = center;
        activeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        activeRadius = Mathf.Max(0.1f, radius);
        activeSurfaceLength = Mathf.Max(0.1f, surfaceLength);
        activeSurfaceStartWidth = Mathf.Max(0.1f, surfaceStartWidth);
        activeSurfaceEndWidth = Mathf.Max(0.1f, surfaceEndWidth);
        lastPlayTime = Time.time;
        effectVisible = true;

        RenderLines(Time.time, 1f);
    }

    public void StopActiveEffect()
    {
        effectVisible = false;
        HideAllLines();
    }

    private void Update()
    {
        if (!effectVisible)
        {
            return;
        }

        float elapsedSincePlay = Time.time - lastPlayTime;
        float visibility = 1f;

        if (elapsedSincePlay > HoldDuration)
        {
            float fadeProgress = (elapsedSincePlay - HoldDuration) / FadeDuration;
            if (fadeProgress >= 1f)
            {
                StopActiveEffect();
                return;
            }

            visibility = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
        }

        RenderLines(Time.time, visibility);
    }

    private bool EnsureLines()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("Wind line effect requires the built-in Sprites/Default shader.", this);
                return false;
            }

            lineMaterial = new Material(shader)
            {
                name = "WindLineMaterial (Runtime)",
                hideFlags = HideFlags.DontSave
            };
        }

        if (lines == null || lines.Length != MaxLineCount)
        {
            lines = new LineRenderer[MaxLineCount];
            propertyBlocks = new MaterialPropertyBlock[MaxLineCount];
        }

        for (int i = 0; i < MaxLineCount; i++)
        {
            if (lines[i] == null)
            {
                lines[i] = CreateLine(i);
            }

            if (propertyBlocks[i] == null)
            {
                propertyBlocks[i] = new MaterialPropertyBlock();
            }
        }

        return true;
    }

    private LineRenderer CreateLine(int index)
    {
        GameObject lineObject = new GameObject("WindLine_" + (index + 1).ToString("00"));
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 0;
        line.numCornerVertices = 2;
        line.generateLightingData = false;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = lineMaterial;
        line.colorGradient = CreateLineGradient();
        line.sortingLayerName = SortingLayerName;
        line.sortingOrder = SortingOrder;
        line.enabled = false;
        return line;
    }

    private static Gradient CreateLineGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.92f, 0.18f),
                new GradientAlphaKey(1f, 0.50f),
                new GradientAlphaKey(0.92f, 0.82f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private void RenderLines(float animationTime, float visibility)
    {
        float width = GetWorldLineWidth();

        switch (activeForm)
        {
            case WindShape.Surface:
                RenderSurface(animationTime, visibility, width);
                SetUnusedLinesVisible(4);
                break;

            case WindShape.Tornado:
                RenderTornado(animationTime, visibility, width);
                SetUnusedLinesVisible(3);
                break;

            default:
                RenderDownburst(animationTime, visibility, width);
                SetUnusedLinesVisible(4);
                break;
        }
    }

    private void RenderDownburst(float animationTime, float visibility, float width)
    {
        float expansionTime = animationTime * 0.62f;

        for (int lineIndex = 0; lineIndex < 4; lineIndex++)
        {
            float phase = Mathf.Repeat(expansionTime + lineIndex * 0.25f, 1f);
            float easedPhase = Mathf.SmoothStep(0f, 1f, phase);
            float lineRadius = activeRadius * Mathf.Lerp(
                DownburstMinRadiusRatio,
                DownburstMaxRadiusRatio,
                easedPhase);
            float arcCenter = lineIndex * 90f + animationTime * 8f;
            float arcSpan = 64f + Mathf.Sin(animationTime * 1.7f + lineIndex) * 8f;
            float startAngle = (arcCenter - arcSpan * 0.5f) * Mathf.Deg2Rad;
            float spanRadians = arcSpan * Mathf.Deg2Rad;

            LineRenderer line = lines[lineIndex];
            line.positionCount = DownburstPointCount;
            for (int pointIndex = 0; pointIndex < DownburstPointCount; pointIndex++)
            {
                float rate = pointIndex / (float)(DownburstPointCount - 1);
                float angle = startAngle + spanRadians * rate;
                float ripple = 1f + Mathf.Sin(rate * Mathf.PI * 2f + lineIndex) * 0.012f;
                float radius = lineRadius * ripple;
                line.SetPosition(
                    pointIndex,
                    new Vector3(
                        activeCenter.x + Mathf.Cos(angle) * radius,
                        activeCenter.y + Mathf.Sin(angle) * radius,
                        0f));
            }

            float phaseOpacity = Mathf.Sin(phase * Mathf.PI);
            ApplyLineVisual(lineIndex, width, visibility * phaseOpacity);
        }
    }

    private void RenderSurface(float animationTime, float visibility, float width)
    {
        Vector2 forwardDirection = activeDirection;
        Vector2 sideDirection = new Vector2(-forwardDirection.y, forwardDirection.x);
        float segmentLength = activeSurfaceLength * 0.42f;
        float flowTime = animationTime * 0.42f;

        for (int lineIndex = 0; lineIndex < 4; lineIndex++)
        {
            float phase = Mathf.Repeat(flowTime + lineIndex * 0.23f, 1f);
            float visibleStart = phase * (activeSurfaceLength - segmentLength);
            float visibleEnd = visibleStart + segmentLength;

            LineRenderer line = lines[lineIndex];
            line.positionCount = SurfacePointCount;
            for (int pointIndex = 0; pointIndex < SurfacePointCount; pointIndex++)
            {
                float rate = pointIndex / (float)(SurfacePointCount - 1);
                float distance = Mathf.Lerp(visibleStart, visibleEnd, rate);
                float areaRate = distance / activeSurfaceLength;
                float halfWidth = Mathf.Lerp(
                    activeSurfaceStartWidth,
                    activeSurfaceEndWidth,
                    areaRate) * 0.5f;
                float sideOffset = halfWidth * SurfaceSideFactors[lineIndex];
                float bend = Mathf.Sin(
                    rate * Mathf.PI * 1.25f + animationTime * 2.1f + lineIndex * 1.4f);
                sideOffset += bend * halfWidth * 0.045f * Mathf.Sin(rate * Mathf.PI);

                Vector2 point = activeCenter
                    + forwardDirection * distance
                    + sideDirection * sideOffset;
                line.SetPosition(pointIndex, new Vector3(point.x, point.y, 0f));
            }

            float phaseOpacity = Mathf.Sin(phase * Mathf.PI);
            ApplyLineVisual(lineIndex, width, visibility * phaseOpacity);
        }
    }

    private void RenderTornado(float animationTime, float visibility, float width)
    {
        float rotation = animationTime * 74f * Mathf.Deg2Rad;

        for (int lineIndex = 0; lineIndex < 3; lineIndex++)
        {
            float inwardPulse = (TornadoMaxRadiusRatio - 0.04f)
                + (Mathf.Sin(animationTime * 2.2f + lineIndex * 1.7f) * 0.5f + 0.5f) * 0.04f;
            float baseAngle = rotation + lineIndex * (Mathf.PI * 2f / 3f);

            LineRenderer line = lines[lineIndex];
            line.positionCount = TornadoPointCount;
            for (int pointIndex = 0; pointIndex < TornadoPointCount; pointIndex++)
            {
                float rate = pointIndex / (float)(TornadoPointCount - 1);
                float radius = activeRadius * Mathf.Lerp(0.13f, inwardPulse, Mathf.Pow(rate, 0.92f));
                float angle = baseAngle
                    + rate * Mathf.PI * 2.30f
                    + Mathf.Sin(rate * Mathf.PI) * 0.08f;
                line.SetPosition(
                    pointIndex,
                    new Vector3(
                        activeCenter.x + Mathf.Cos(angle) * radius,
                        activeCenter.y + Mathf.Sin(angle) * radius,
                        0f));
            }

            float lineOpacity = 0.82f + Mathf.Sin(animationTime * 1.5f + lineIndex) * 0.12f;
            ApplyLineVisual(lineIndex, width, visibility * lineOpacity);
        }
    }

    private void ApplyLineVisual(int index, float width, float opacity)
    {
        LineRenderer line = lines[index];
        line.widthMultiplier = width;
        line.enabled = true;

        MaterialPropertyBlock block = propertyBlocks[index];
        block.Clear();
        block.SetColor(
            TintPropertyId,
            new Color(WarmWhite.r, WarmWhite.g, WarmWhite.b, PeakAlpha * Mathf.Clamp01(opacity)));
        line.SetPropertyBlock(block);
    }

    private float GetWorldLineWidth()
    {
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null || !cachedCamera.orthographic)
        {
            return 0.04f;
        }

        float worldPerPixel = cachedCamera.orthographicSize * 2f / Mathf.Max(1f, cachedCamera.pixelHeight);
        return Mathf.Clamp(worldPerPixel * TargetPixelWidth, MinWorldWidth, MaxWorldWidth);
    }

    private void SetUnusedLinesVisible(int usedLineCount)
    {
        for (int i = usedLineCount; i < lines.Length; i++)
        {
            lines[i].enabled = false;
        }
    }

    private void HideAllLines()
    {
        if (lines == null)
        {
            return;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] != null)
            {
                lines[i].enabled = false;
            }
        }
    }

    private void OnDisable()
    {
        StopActiveEffect();
    }

    private void OnDestroy()
    {
        if (lineMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(lineMaterial);
        }
        else
        {
            DestroyImmediate(lineMaterial);
        }
    }
}
