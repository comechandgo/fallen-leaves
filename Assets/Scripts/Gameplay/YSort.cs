using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class YSort : MonoBehaviour
{
    [SerializeField] private string sortingLayerName = "Actor";
    [SerializeField] private int baseOrder;
    [SerializeField, Min(0.001f)] private float unitsPerOrder = 0.05f;
    [SerializeField] private float heightOffset;
    [SerializeField] private bool updateEveryFrame = true;

    private Renderer targetRenderer;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        ApplyNow();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame) ApplyNow();
    }

    public void Configure(string layerName, int orderBase, float offset, bool dynamicSort)
    {
        sortingLayerName = layerName;
        baseOrder = orderBase;
        heightOffset = offset;
        updateEveryFrame = dynamicSort;
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        ApplyNow();
    }

    public void ApplyNow()
    {
        if (targetRenderer == null) return;
        targetRenderer.sortingLayerName = sortingLayerName;
        targetRenderer.sortingOrder = ComputeSortingOrder(transform.position.y, baseOrder, unitsPerOrder, heightOffset);
    }

    public void SetDynamic(bool dynamicSort, bool applyImmediately = true)
    {
        updateEveryFrame = dynamicSort;
        if (applyImmediately) ApplyNow();
        enabled = dynamicSort;
    }

    public static int ComputeSortingOrder(float worldY, int orderBase, float unit, float offset)
    {
        return orderBase + Mathf.RoundToInt((-worldY + offset) / Mathf.Max(0.001f, unit));
    }
}
