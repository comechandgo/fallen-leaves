using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RiverPath2D : MonoBehaviour
{
    [SerializeField] private Vector2[] controlPoints = new Vector2[0];
    [SerializeField, Min(1f)] private float riverWidth = 20f;
    [SerializeField, Range(2, 24)] private int subdivisionsPerSegment = 8;
    [SerializeField] private Material bankMaterial;
    [SerializeField] private Material waterMaterial;
    [SerializeField] private LineRenderer bankLine;
    [SerializeField] private LineRenderer waterLine;

    private Vector2[] smoothPoints = new Vector2[0];
    private Transform runtimeCollectors;

    public int ControlPointCount => controlPoints != null ? controlPoints.Length : 0;
    public float RiverWidth => riverWidth;

    private void Awake()
    {
        RebuildVisual();
        if (Application.isPlaying) BuildRuntimeCollectors();
    }

    private void OnValidate()
    {
        riverWidth = Mathf.Max(1f, riverWidth);
        subdivisionsPerSegment = Mathf.Clamp(subdivisionsPerSegment, 2, 24);
        RebuildVisual();
    }

    public void Configure(
        Vector2[] points,
        float width,
        Material bank,
        Material water,
        LineRenderer bankRenderer,
        LineRenderer waterRenderer)
    {
        controlPoints = points ?? new Vector2[0];
        riverWidth = Mathf.Max(1f, width);
        bankMaterial = bank;
        waterMaterial = water;
        bankLine = bankRenderer;
        waterLine = waterRenderer;
        RebuildVisual();
    }

    public Vector2 GetControlPoint(int index)
    {
        return controlPoints[index];
    }

    public void SetControlPoint(int index, Vector2 point)
    {
        if (controlPoints == null || index < 0 || index >= controlPoints.Length) return;
        controlPoints[index] = point;
    }

    public void RebuildVisual()
    {
        ResolveRenderers();
        smoothPoints = BuildSmoothPoints();

        ConfigureLine(bankLine, riverWidth + 8f, bankMaterial, 2);
        ConfigureLine(waterLine, riverWidth, waterMaterial, 3);
    }

    private void ConfigureLine(LineRenderer line, float width, Material material, int sortingOrder)
    {
        if (line == null) return;

        line.useWorldSpace = false;
        line.loop = false;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Tile;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.widthMultiplier = width;
        line.positionCount = smoothPoints.Length;
        line.sortingLayerName = "Ground";
        line.sortingOrder = sortingOrder;
        if (material != null) line.sharedMaterial = material;

        for (int i = 0; i < smoothPoints.Length; i++)
        {
            line.SetPosition(i, smoothPoints[i]);
        }
    }

    private void ResolveRenderers()
    {
        if (bankLine == null)
        {
            Transform bank = transform.Find("Bank");
            if (bank != null) bankLine = bank.GetComponent<LineRenderer>();
        }

        if (waterLine == null)
        {
            Transform water = transform.Find("Water");
            if (water != null) waterLine = water.GetComponent<LineRenderer>();
        }
    }

    private Vector2[] BuildSmoothPoints()
    {
        if (controlPoints == null || controlPoints.Length < 2)
        {
            return controlPoints ?? new Vector2[0];
        }

        List<Vector2> result = new List<Vector2>(controlPoints.Length * subdivisionsPerSegment);
        for (int i = 0; i < controlPoints.Length - 1; i++)
        {
            Vector2 p0 = controlPoints[Mathf.Max(0, i - 1)];
            Vector2 p1 = controlPoints[i];
            Vector2 p2 = controlPoints[i + 1];
            Vector2 p3 = controlPoints[Mathf.Min(controlPoints.Length - 1, i + 2)];

            for (int j = 0; j < subdivisionsPerSegment; j++)
            {
                float t = j / (float)subdivisionsPerSegment;
                result.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        result.Add(controlPoints[controlPoints.Length - 1]);
        return result.ToArray();
    }

    private void BuildRuntimeCollectors()
    {
        if (smoothPoints == null || smoothPoints.Length < 2) return;

        GameObject collectors = new GameObject("RiverCollectorsRuntime");
        collectors.transform.SetParent(transform, false);
        runtimeCollectors = collectors.transform;

        for (int i = 0; i < smoothPoints.Length - 1; i++)
        {
            Vector2 start = smoothPoints[i];
            Vector2 end = smoothPoints[i + 1];
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= 0.0001f) continue;

            GameObject segment = new GameObject($"Collector_{i + 1}");
            segment.layer = 10;
            segment.transform.SetParent(runtimeCollectors, false);
            segment.transform.localPosition = (start + end) * 0.5f;
            segment.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            BoxCollider2D collider = segment.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(direction.magnitude, riverWidth * 0.92f);
            segment.AddComponent<RiverCollector>();
        }
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void OnDrawGizmosSelected()
    {
        if (controlPoints == null || controlPoints.Length == 0) return;

        Gizmos.color = Theme.Water;
        for (int i = 0; i < controlPoints.Length; i++)
        {
            Vector3 world = transform.TransformPoint(controlPoints[i]);
            Gizmos.DrawSphere(world, 1.2f);
            if (i > 0)
            {
                Gizmos.DrawLine(transform.TransformPoint(controlPoints[i - 1]), world);
            }
        }
    }
}
