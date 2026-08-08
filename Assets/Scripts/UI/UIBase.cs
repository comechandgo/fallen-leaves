using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public abstract class UIBase : MonoBehaviour
{
    protected static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    protected static readonly Vector2 SafeMargin = new Vector2(138f, 64f);

    private const string DefaultFontResourcePath = "Fonts/CangErYuMoW05-2";
    private static Font defaultFont;

    protected CanvasGroup group;
    private bool built;

    public bool IsVisible => group != null && group.alpha > 0.01f;

    protected virtual void Awake()
    {
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            Stretch(rect);
        }

        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;
    }

    protected abstract void Build();

    public virtual void Show()
    {
        // Awake 在 AddComponent 时立即执行，group 一定非空；不能用 group==null 判断。
        // 用 built 标志保证 Build 只跑一次。
        if (!built) { Build(); built = true; }
        group.alpha = 1f;
        group.blocksRaycasts = true;
        group.interactable = true;

        FrostedBackdrop[] backdrops = GetComponentsInChildren<FrostedBackdrop>(true);
        for (int i = 0; i < backdrops.Length; i++)
        {
            backdrops[i].Refresh();
        }
    }

    public virtual void Hide()
    {
        if (group == null) return;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    protected static GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        return go;
    }

    protected static Text CreateText(Transform parent, string content, int fontSize, TextAnchor align, Color color)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Font GetDefaultFont()
    {
        if (defaultFont != null) return defaultFont;

        defaultFont = Resources.Load<Font>(DefaultFontResourcePath);
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null) defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return defaultFont;
    }

    protected static Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 size, UnityAction onClick)
    {
        GameObject go = new GameObject("Button_" + label,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = Theme.ButtonBg;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();

        ColorBlock colors = btn.colors;
        colors.normalColor      = Theme.ButtonBg;
        colors.highlightedColor = Theme.ButtonHot;
        colors.pressedColor     = Theme.ButtonDown;
        colors.selectedColor    = Theme.ButtonHot;
        colors.disabledColor    = Theme.ButtonDisabled;
        colors.fadeDuration     = 0.12f;
        btn.colors = colors;

        if (onClick != null) btn.onClick.AddListener(onClick);

        Text txt = CreateText(go.transform, label, 22, TextAnchor.MiddleCenter, Theme.ButtonText);
        Stretch(txt.rectTransform);
        return btn;
    }

    protected static RectTransform CreateSafeArea(Transform parent, string name = "SafeArea")
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        Stretch(rect);
        rect.offsetMin = SafeMargin;
        rect.offsetMax = -SafeMargin;
        return rect;
    }

    protected static Image CreateImageStretch(Transform parent, string name, string spritePath, bool preserveAspect = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        Stretch(rect);

        Image image = go.GetComponent<Image>();
        image.sprite = RuntimeArt.LoadSprite(spritePath);
        image.preserveAspect = preserveAspect;
        image.color = image.sprite == null ? Theme.PanelShadow : Color.white;
        return image;
    }

    protected static Image CreateImage(Transform parent, string name, string spritePath, Vector2 anchor, Vector2 size, bool preserveAspect = true, Vector2? pivot = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = RuntimeArt.LoadSprite(spritePath);
        image.preserveAspect = preserveAspect;
        image.color = image.sprite == null ? Theme.PanelBg : Color.white;
        return image;
    }

    protected static Button CreateImageButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchor,
        Vector2 size,
        UnityAction onClick,
        string normalPath,
        string hoverPath = null,
        string pressedPath = null,
        Vector2? pivot = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = RuntimeArt.LoadSprite(normalPath);
        image.preserveAspect = true;
        image.color = image.sprite == null ? Theme.ButtonBg : Color.white;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        Sprite hover = string.IsNullOrEmpty(hoverPath) ? null : RuntimeArt.LoadSprite(hoverPath);
        Sprite pressed = string.IsNullOrEmpty(pressedPath) ? null : RuntimeArt.LoadSprite(pressedPath);

        if (hover != null || pressed != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = hover != null ? hover : image.sprite,
                selectedSprite = hover != null ? hover : image.sprite,
                pressedSprite = pressed != null ? pressed : image.sprite,
                disabledSprite = image.sprite
            };
        }

        if (onClick != null) button.onClick.AddListener(onClick);

        if (!string.IsNullOrEmpty(label))
        {
            Text text = CreateText(go.transform, label, 20, TextAnchor.MiddleCenter, Theme.ButtonText);
            Stretch(text.rectTransform);
        }

        return button;
    }

    protected static FrostedBackdrop CreateFrostedBackdrop(Transform parent)
    {
        GameObject go = new GameObject("FrostedBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(FrostedBackdrop));
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();
        Stretch(go.GetComponent<RectTransform>());
        return go.GetComponent<FrostedBackdrop>();
    }

    protected static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
