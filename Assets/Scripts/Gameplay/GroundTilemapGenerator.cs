using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
public sealed class GroundTilemapGenerator : MonoBehaviour
{
    [Serializable]
    public struct RegionHint
    {
        public string Id;
        public Rect Bounds;
        [Range(-1f, 1f)] public float GreenBias;

        public RegionHint(string id, Rect bounds, float greenBias)
        {
            Id = id;
            Bounds = bounds;
            GreenBias = Mathf.Clamp(greenBias, -1f, 1f);
        }
    }

    private readonly struct CellScore
    {
        public readonly int Index;
        public readonly float Score;

        public CellScore(int index, float score)
        {
            Index = index;
            Score = score;
        }
    }

    [SerializeField] private Rect mapBounds = new Rect(-60f, -45f, 120f, 90f);
    [SerializeField] private TileBase greenTile;
    [SerializeField] private TileBase yellowTile;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Grid grid;
    [SerializeField] private int seed = 12090;
    [SerializeField, Min(1f)] private float patchWorldSize = 26f;
    [SerializeField] private RegionHint[] regionHints = Array.Empty<RegionHint>();

    public Rect MapBounds => mapBounds;
    public TileBase GreenTile => greenTile;
    public TileBase YellowTile => yellowTile;
    public int Seed => seed;
    public float PatchWorldSize => patchWorldSize;
    public IReadOnlyList<RegionHint> RegionHints => regionHints;

    private void Awake()
    {
        EnsureBuilt();
    }

    public void Configure(
        Rect bounds,
        TileBase green,
        TileBase yellow,
        Tilemap targetTilemap,
        Grid targetGrid,
        int patternSeed,
        float patchSize,
        RegionHint[] hints)
    {
        mapBounds = bounds;
        greenTile = green;
        yellowTile = yellow;
        tilemap = targetTilemap;
        grid = targetGrid;
        seed = patternSeed;
        patchWorldSize = Mathf.Max(1f, patchSize);
        regionHints = hints ?? Array.Empty<RegionHint>();
    }

    public void EnsureBuilt()
    {
        ResolveReferences();
        if (tilemap != null && tilemap.GetUsedTilesCount() == 0) Rebuild();
    }

    public void Rebuild()
    {
        ResolveReferences();
        if (tilemap == null || grid == null || greenTile == null || yellowTile == null)
        {
            Debug.LogWarning($"Cannot rebuild ground on {name}: Grid, Tilemap, or one of the grass Tiles is missing.", this);
            return;
        }

        Vector2 tileSize = ResolveTileWorldSize();
        tileSize.x = Mathf.Max(0.01f, tileSize.x);
        tileSize.y = Mathf.Max(0.01f, tileSize.y);

        grid.cellSize = new Vector3(tileSize.x, tileSize.y, 1f);
        grid.transform.localPosition = new Vector3(mapBounds.xMin, mapBounds.yMin, 0f);

        int columns = Mathf.Max(1, Mathf.CeilToInt(mapBounds.width / tileSize.x));
        int rows = Mathf.Max(1, Mathf.CeilToInt(mapBounds.height / tileSize.y));
        int total = columns * rows;

        List<CellScore> scores = new List<CellScore>(total);
        float seedX = seed * 0.01371f;
        float seedY = seed * 0.02117f;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int index = y * columns + x;
                Vector2 center = new Vector2(
                    mapBounds.xMin + (x + 0.5f) * tileSize.x,
                    mapBounds.yMin + (y + 0.5f) * tileSize.y);

                float lowFrequency = Mathf.PerlinNoise(
                    center.x / patchWorldSize + seedX,
                    center.y / patchWorldSize + seedY);
                float broadFrequency = Mathf.PerlinNoise(
                    center.x / (patchWorldSize * 1.9f) - seedY,
                    center.y / (patchWorldSize * 1.9f) + seedX);
                float score = lowFrequency * 0.72f + broadFrequency * 0.28f;

                for (int i = 0; i < regionHints.Length; i++)
                {
                    if (regionHints[i].Bounds.Contains(center)) score += regionHints[i].GreenBias;
                }

                // Stable tie breaker: exact 50/50 selection must not depend on List.Sort implementation details.
                score += index * 0.000001f;
                scores.Add(new CellScore(index, score));
            }
        }

        scores.Sort((a, b) =>
        {
            int scoreOrder = b.Score.CompareTo(a.Score);
            return scoreOrder != 0 ? scoreOrder : a.Index.CompareTo(b.Index);
        });

        bool[] greenCells = new bool[total];
        int targetGreen = (total + 1) / 2;
        for (int i = 0; i < targetGreen; i++) greenCells[scores[i].Index] = true;

        tilemap.ClearAllTiles();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int index = y * columns + x;
                Vector3Int cell = new Vector3Int(x, y, 0);
                tilemap.SetTile(cell, greenCells[index] ? greenTile : yellowTile);
                tilemap.SetTransformMatrix(cell, Matrix4x4.identity);
            }
        }

        tilemap.CompressBounds();

        TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = "Ground";
            renderer.sortingOrder = 0;
        }
    }

    public void CountTiles(out int greenCount, out int yellowCount)
    {
        ResolveReferences();
        greenCount = 0;
        yellowCount = 0;
        if (tilemap == null) return;

        BoundsInt bounds = tilemap.cellBounds;
        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            TileBase tile = tilemap.GetTile(position);
            if (tile == greenTile) greenCount++;
            else if (tile == yellowTile) yellowCount++;
        }
    }

    public float GetGreenRatio(Rect worldRegion)
    {
        ResolveReferences();
        if (tilemap == null || grid == null) return 0f;

        int greenCount = 0;
        int total = 0;
        BoundsInt bounds = tilemap.cellBounds;
        foreach (Vector3Int position in bounds.allPositionsWithin)
        {
            Vector3 world = new Vector3(
                mapBounds.xMin + (position.x + 0.5f) * grid.cellSize.x,
                mapBounds.yMin + (position.y + 0.5f) * grid.cellSize.y,
                0f);
            if (!worldRegion.Contains(world)) continue;

            TileBase tile = tilemap.GetTile(position);
            if (tile == null) continue;
            total++;
            if (tile == greenTile) greenCount++;
        }

        return total > 0 ? greenCount / (float)total : 0f;
    }

    private void ResolveReferences()
    {
        if (tilemap == null) tilemap = GetComponent<Tilemap>();
        if (grid == null) grid = GetComponentInParent<Grid>();
    }

    private Vector2 ResolveTileWorldSize()
    {
        if (greenTile is Tile green && green.sprite != null) return green.sprite.bounds.size;
        if (yellowTile is Tile yellow && yellow.sprite != null) return yellow.sprite.bounds.size;
        return Vector2.one * 10f;
    }
}
