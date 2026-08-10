using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap), typeof(TilemapRenderer))]
public sealed class GroundTilemapGenerator : MonoBehaviour
{
    [SerializeField] private Rect mapBounds = new Rect(-50f, -50f, 100f, 100f);
    [SerializeField] private TileBase groundTile;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Grid grid;

    public Rect MapBounds => mapBounds;
    public TileBase GroundTile => groundTile;

    private void Awake()
    {
        EnsureBuilt();
    }

    public void Configure(Rect bounds, TileBase tile, Tilemap targetTilemap, Grid targetGrid)
    {
        mapBounds = bounds;
        groundTile = tile;
        tilemap = targetTilemap;
        grid = targetGrid;
    }

    public void EnsureBuilt()
    {
        ResolveReferences();
        if (tilemap != null && tilemap.GetUsedTilesCount() == 0) Rebuild();
    }

    public void Rebuild()
    {
        ResolveReferences();
        if (tilemap == null || grid == null || groundTile == null)
        {
            Debug.LogWarning($"Cannot rebuild ground on {name}: Grid, Tilemap, or Tile is missing.", this);
            return;
        }

        Vector2 tileSize = ResolveTileWorldSize();
        tileSize.x = Mathf.Max(0.01f, tileSize.x);
        tileSize.y = Mathf.Max(0.01f, tileSize.y);

        grid.cellSize = new Vector3(tileSize.x, tileSize.y, 1f);
        grid.transform.position = new Vector3(mapBounds.xMin, mapBounds.yMin, 0f);

        int columns = Mathf.Max(1, Mathf.CeilToInt(mapBounds.width / tileSize.x));
        int rows = Mathf.Max(1, Mathf.CeilToInt(mapBounds.height / tileSize.y));

        tilemap.ClearAllTiles();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), groundTile);
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

    private void ResolveReferences()
    {
        if (tilemap == null) tilemap = GetComponent<Tilemap>();
        if (grid == null) grid = GetComponentInParent<Grid>();
    }

    private Vector2 ResolveTileWorldSize()
    {
        if (groundTile is Tile tile && tile.sprite != null)
        {
            return tile.sprite.bounds.size;
        }

        return Vector2.one * 10f;
    }
}
