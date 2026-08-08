using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class FrostedBackdrop : MonoBehaviour
{
    [SerializeField, Min(64)] private int captureWidth = 320;
    [SerializeField, Range(0.2f, 1f)] private float tintAlpha = 0.82f;

    private RawImage image;
    private Texture2D capturedTexture;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        Color color = Theme.FrostTint;
        color.a = tintAlpha;
        image.color = color;
    }

    public void Refresh()
    {
        if (image == null) image = GetComponent<RawImage>();

        Camera camera = Camera.main;
        if (camera == null)
        {
            image.texture = RuntimeArt.LoadTexture("ggj/暂停/bg_blak.png");
            return;
        }

        int width = Mathf.Max(64, captureWidth);
        int height = Mathf.Max(36, Mathf.RoundToInt(width / Mathf.Max(0.01f, camera.aspect)));
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 16, RenderTextureFormat.ARGB32);

        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;

        Texture2D nextTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        nextTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        nextTexture.Apply(false);
        nextTexture.filterMode = FilterMode.Bilinear;

        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(renderTexture);

        if (capturedTexture != null) Destroy(capturedTexture);
        capturedTexture = nextTexture;
        image.texture = capturedTexture;
    }

    private void OnDestroy()
    {
        if (capturedTexture != null) Destroy(capturedTexture);
    }
}
