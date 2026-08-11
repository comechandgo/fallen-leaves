using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MapPrototypeGizmos : MonoBehaviour
{
    [Serializable]
    public struct Region
    {
        public string Id;
        public Rect Bounds;

        public Region(string id, Rect bounds)
        {
            Id = id;
            Bounds = bounds;
        }
    }

    [SerializeField] private string sourceAsset;
    [SerializeField] private string sourceSha256;
    [SerializeField] private Vector2[] riverPoints = Array.Empty<Vector2>();
    [SerializeField] private Region[] regions = Array.Empty<Region>();
    [SerializeField] private Vector2 cameraStart;
    [SerializeField] private Vector2 windStart;

    public string SourceAsset => sourceAsset;
    public string SourceSha256 => sourceSha256;
    public Vector2[] RiverPoints => riverPoints;
    public Region[] Regions => regions;
    public Vector2 CameraStart => cameraStart;
    public Vector2 WindStart => windStart;

    public void Configure(
        string source,
        string sha256,
        Vector2[] route,
        Region[] mapRegions,
        Vector2 camera,
        Vector2 wind)
    {
        sourceAsset = source;
        sourceSha256 = sha256;
        riverPoints = route ?? Array.Empty<Vector2>();
        regions = mapRegions ?? Array.Empty<Region>();
        cameraStart = camera;
        windStart = wind;
    }

    private void OnDrawGizmosSelected()
    {
        if (regions != null)
        {
            for (int i = 0; i < regions.Length; i++)
            {
                float hue = regions.Length > 0 ? i / (float)regions.Length : 0f;
                Gizmos.color = Color.HSVToRGB(hue, 0.45f, 0.95f);
                Gizmos.DrawWireCube(regions[i].Bounds.center, regions[i].Bounds.size);
            }
        }

        if (riverPoints != null && riverPoints.Length > 1)
        {
            Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.9f);
            for (int i = 1; i < riverPoints.Length; i++) Gizmos.DrawLine(riverPoints[i - 1], riverPoints[i]);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(cameraStart, 0.8f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(windStart, 0.8f);
    }
}
