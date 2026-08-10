using UnityEngine;

// 水面 UV 沿河流动画。支持 MeshRenderer 和 LineRenderer。
public class WaterFlow : MonoBehaviour
{
    [SerializeField] private float speed = 0.18f;

    private Material runtimeMaterial;

    private void Awake()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null) return;
        // 复制一份材质，避免污染共享资源 / 跨场景残留。
        runtimeMaterial = new Material(renderer.sharedMaterial);
        renderer.material = runtimeMaterial;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }

    private void Update()
    {
        if (runtimeMaterial == null) return;
        Vector2 offset = runtimeMaterial.mainTextureOffset;
        offset.x = (Time.time * speed) % 1f;
        runtimeMaterial.mainTextureOffset = offset;
    }
}
