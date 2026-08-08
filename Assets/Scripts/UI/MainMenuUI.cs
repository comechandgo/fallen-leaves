using UnityEngine;
using UnityEngine.UI;

// 主菜单：使用开始界面素材拼装首屏。
public class MainMenuUI : UIBase
{
    private System.Action onStart;
    private System.Action onSettings;
    private static Sprite infoButtonSprite;


    public void Init(System.Action startCallback, System.Action settingsCallback, System.Action quitCallback)
    {
        onStart = startCallback;
        onSettings = settingsCallback;
    }

    protected override void Build()
    {
        CreateImageStretch(transform, "Sky", "ggj/开始界面/天空.png");
        CreateBottomStretchImage(transform, "Ground", "ggj/开始界面/img_地面.png", 245f);

        CreateImage(transform, "Tree", "ggj/开始界面/树.png", new Vector2(1f, 1f), new Vector2(1330f, 650f), true, new Vector2(1f, 1f));

        RectTransform safe = CreateSafeArea(transform);
        CreateImage(safe, "Title", "ggj/开始界面/img_秋风落叶.png", new Vector2(0f, 0.30f), new Vector2(740f, 238f), true, new Vector2(0f, 0.5f));

        CreateImage(safe, "LeftLeaf", "ggj/开始界面/左叶.png", new Vector2(0.35f, 0.82f), new Vector2(150f, 195f));
        CreateImage(safe, "MiddleLeaf", "ggj/开始界面/中叶.png", new Vector2(0.58f, 0.72f), new Vector2(110f, 113f));
        CreateImage(safe, "RightLeaf", "ggj/开始界面/右叶.png", new Vector2(0.86f, 0.66f), new Vector2(160f, 150f));

        CreateImageButton(safe, "StartButton", "", new Vector2(1f, 0f), new Vector2(390f, 114f),
            () => onStart?.Invoke(), "ggj/开始界面/进入游戏.png", null, null, new Vector2(1f, 0f));
        CreateInfoButton(safe, new Vector2(0f, 1f), new Vector2(82f, 82f));
        CreateImageButton(safe, "SettingsButton", "", new Vector2(0f, 0.88f), new Vector2(82f, 82f),
            () => onSettings?.Invoke(), "ggj/开始界面/btn_设置.png", null, null, new Vector2(0f, 1f));
    }

    private static Button CreateInfoButton(Transform parent, Vector2 anchor, Vector2 size)
    {
        GameObject go = new GameObject("InfoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = GetInfoButtonSprite();
        image.preserveAspect = true;
        image.color = Color.white;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        Text text = CreateText(go.transform, "i", 48, TextAnchor.MiddleCenter, Color.white);
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        Stretch(text.rectTransform);

        return button;
    }

    private static Image CreateBottomStretchImage(Transform parent, string name, string spritePath, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, height);

        Image image = go.GetComponent<Image>();
        image.sprite = RuntimeArt.LoadSprite(spritePath);
        image.preserveAspect = false;
        image.color = image.sprite == null ? Theme.PanelBg : Color.white;
        return image;
    }

    private static Sprite GetInfoButtonSprite()
    {
        if (infoButtonSprite != null) return infoButtonSprite;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.48f;
        float innerRadius = size * 0.40f;
        Color fill = new Color(0.18f, 0.21f, 0.22f, 0.72f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                Color color = Color.clear;
                if (distance <= outerRadius)
                {
                    color = distance >= innerRadius ? Color.white : fill;
                }

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        infoButtonSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        infoButtonSprite.name = "GeneratedInfoButton";
        return infoButtonSprite;
    }
}
