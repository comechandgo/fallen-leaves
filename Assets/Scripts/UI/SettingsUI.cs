using UnityEngine;
using UnityEngine.UI;

// 设置 / 暂停浮层：音量 + 继续 + 关卡选择 + 返回主界面。
public class SettingsUI : UIBase
{
    private System.Action onResume;
    private System.Action onLevelSelect;
    private System.Action onQuitToMenu;
    private System.Func<bool> gameplayProvider;

    private GameObject levelButtonObject;
    private GameObject menuButtonObject;

    public void Bind(
        System.Action onResume,
        System.Action onLevelSelect,
        System.Action onQuitToMenu,
        System.Func<bool> gameplayProvider)
    {
        this.onResume = onResume;
        this.onLevelSelect = onLevelSelect;
        this.onQuitToMenu = onQuitToMenu;
        this.gameplayProvider = gameplayProvider;
    }

    protected override void Build()
    {
        CreateFrostedBackdrop(transform);

        RectTransform safe = CreateSafeArea(transform);
        Image card = CreateImage(safe, "PausePanel", "ggj/暂停/小弹窗.png", new Vector2(0.5f, 0.52f), new Vector2(620f, 390f), false);
        Transform cardRoot = card.transform;

        Text title = CreateText(cardRoot, "设置", 28, TextAnchor.MiddleCenter, Theme.TextDark);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.76f);
        titleRect.anchorMax = new Vector2(1f, 0.92f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

        Text volumeLabel = CreateText(cardRoot, "音量", 20, TextAnchor.MiddleLeft, Theme.TextDark);
        RectTransform labelRect = volumeLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0.18f, 0.56f);
        labelRect.anchorMax = new Vector2(0.34f, 0.66f);
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        Slider slider = CreateVolumeSlider(cardRoot);
        slider.value = AudioListener.volume;
        slider.onValueChanged.AddListener(value => AudioListener.volume = value);

        Button resumeButton = CreateImageButton(cardRoot, "Resume", "", new Vector2(0.5f, 0.44f), new Vector2(88f, 88f),
            () => onResume?.Invoke(), "ggj/暂停/btn_继续.png");
        resumeButton.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

        Button levelButton = CreateImageButton(cardRoot, "LevelSelect", "", new Vector2(0.5f, 0.25f), new Vector2(205f, 70f),
            () => onLevelSelect?.Invoke(), "ggj/暂停/关卡选择正常.png", "ggj/暂停/关卡选择悬浮.png", "ggj/暂停/关卡选择按下.png");
        levelButtonObject = levelButton.gameObject;

        Button menuButton = CreateImageButton(cardRoot, "MainMenu", "", new Vector2(0.5f, 0.12f), new Vector2(205f, 70f),
            () => onQuitToMenu?.Invoke(), "ggj/暂停/返回主界面正常.png", "ggj/暂停/返回主界面悬浮.png", "ggj/暂停/返回主界面按下.png");
        menuButtonObject = menuButton.gameObject;
    }

    private static Slider CreateVolumeSlider(Transform parent)
    {
        GameObject go = new GameObject("VolumeSlider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.36f, 0.57f);
        rect.anchorMax = new Vector2(0.82f, 0.65f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        Image background = CreateSliderImage(go.transform, "Background", Theme.PanelShadow);
        Stretch(background.rectTransform);

        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(6f, 6f);
        fillAreaRect.offsetMax = new Vector2(-6f, -6f);

        Image fill = CreateSliderImage(fillArea.transform, "Fill", Theme.ButtonBg);
        Stretch(fill.rectTransform);

        Image handle = CreateSliderImage(go.transform, "Handle", Theme.LeafGold);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(28f, 28f);

        Slider slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.targetGraphic = handle;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static Image CreateSliderImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private void Update()
    {
        if (!IsVisible) return;

        bool gameplay = gameplayProvider != null && gameplayProvider();
        if (levelButtonObject != null) levelButtonObject.SetActive(gameplay);
        if (menuButtonObject != null) menuButtonObject.SetActive(gameplay);
    }
}
