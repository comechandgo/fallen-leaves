using UnityEngine;
using UnityEngine.UI;

// 顶部 HUD：设置入口 + 商店入口 + 金币 + 时间。
public class HudUI : UIBase
{
    private const float CoinPopPeakTime = 0.06f;
    private const float CoinPopReboundTime = 0.13f;
    private const float CoinPopDuration = 0.22f;
    private const float CoinPopPeakScale = 1.30f;
    private const float CoinPopReboundScale = 0.96f;
    private const float TimeBackgroundAlpha = 0.70f;
    private const float TimeContentAlpha = 0.85f;
    private const float SurvivalPopDuration = 0.18f;
    private const float SurvivalBarWidth = 420f;
    private const float SurvivalBarHeight = 32f;
    private const float SurvivalPopOffset = 6f;
    private const float SurvivalTrackAlpha = 0.82f;

    private static readonly Vector2 SurvivalBarPosition = new Vector2(0f, -12f);
    private static readonly Color SurvivalNormal = Theme.LeafAmber;
    private static readonly Color SurvivalTrack = new Color(Theme.Paper.r, Theme.Paper.g, Theme.Paper.b, SurvivalTrackAlpha);
    private static readonly Color SurvivalLow = new Color(0.91f, 0.25f, 0.20f, 1f);
    private static Sprite survivalPillSprite;

    private System.Action onToggleSettings;
    private System.Action onToggleShop;
    private System.Func<string> coinProvider;
    private System.Func<string> timeProvider;
    private System.Func<bool> timedModeProvider;
    private System.Func<bool> endlessModeProvider;
    private System.Func<float> survivalRatioProvider;
    private System.Func<bool> endlessFailureProvider;

    private Text coinLabel;
    private RectTransform coinIconRect;
    private Text timeLabel;
    private Text controlHintLabel;
    private RectTransform timeRect;
    private Image timeBackground;
    private Image timeIcon;
    private GameObject survivalBar;
    private RectTransform survivalBarRect;
    private Image survivalBackground;
    private RectTransform survivalFillRect;
    private Image survivalFill;
    private bool? endlessStyleApplied;
    private bool coinEventsSubscribed;
    private bool coinPopPlaying;
    private float coinPopElapsed;
    private float coinPopStartScale = 1f;
    private float survivalPopElapsed = SurvivalPopDuration;

    public void Bind(System.Action toggleSettings, System.Action toggleShop,
                     System.Func<string> coinProvider, System.Func<string> timeProvider,
                     System.Func<bool> timedModeProvider,
                     System.Func<bool> endlessModeProvider,
                     System.Func<float> survivalRatioProvider,
                     System.Func<bool> endlessFailureProvider)
    {
        onToggleSettings = toggleSettings;
        onToggleShop = toggleShop;
        this.coinProvider = coinProvider;
        this.timeProvider = timeProvider;
        this.timedModeProvider = timedModeProvider;
        this.endlessModeProvider = endlessModeProvider;
        this.survivalRatioProvider = survivalRatioProvider;
        this.endlessFailureProvider = endlessFailureProvider;
    }

    protected override void Build()
    {
        RectTransform safe = CreateSafeArea(transform);

        controlHintLabel = CreateText(
            safe,
            "左键吹树叶\n右键移动地图",
            36,
            TextAnchor.MiddleLeft,
            Theme.TextDark);
        controlHintLabel.name = "ControlHint";
        controlHintLabel.raycastTarget = false;
        RectTransform controlHintRect = controlHintLabel.rectTransform;
        controlHintRect.anchorMin = controlHintRect.anchorMax = new Vector2(0f, 0.5f);
        controlHintRect.pivot = new Vector2(0f, 0.5f);
        controlHintRect.anchoredPosition = Vector2.zero;
        controlHintRect.sizeDelta = new Vector2(360f, 110f);

        CreateImageButton(safe, "Pause", "", new Vector2(0f, 1f), new Vector2(120f, 69f),
            () => onToggleSettings?.Invoke(), "ggj/局内/设置暂停_正常.png", null, "ggj/局内/设置暂停_按下.png", new Vector2(0f, 1f));

        CreateImageButton(safe, "Shop", "", new Vector2(1f, 1f), new Vector2(120f, 69f),
            () => onToggleShop?.Invoke(), "ggj/局内/商店正常.png", null, "ggj/局内/商店按下.png", new Vector2(1f, 1f));

        GameObject coinBox = CreatePanel(safe, "CoinBox", new Vector2(1f, 1f), new Vector2(210f, 54f));
        coinBox.GetComponent<Image>().sprite = RuntimeArt.LoadSprite("ggj/局内/常用_正常.png");
        coinBox.GetComponent<Image>().color = Color.white;
        RectTransform coinRect = coinBox.GetComponent<RectTransform>();
        coinRect.anchoredPosition = new Vector2(-132f, -8f);

        Image coinIcon = CreateImage(coinBox.transform, "GoldIcon", "ggj/局内/gold.png", new Vector2(0f, 0.5f), new Vector2(32f, 32f), true, new Vector2(0f, 0.5f));
        coinIconRect = coinIcon.rectTransform;
        coinLabel = CreateText(coinBox.transform, "0", 22, TextAnchor.MiddleCenter, Theme.TextLight);
        Stretch(coinLabel.rectTransform);
        coinLabel.rectTransform.offsetMin = new Vector2(38f, 0f);

        GameObject timeBox = CreatePanel(safe, "Time", new Vector2(1f, 1f), new Vector2(154f, 42f));
        timeBackground = timeBox.GetComponent<Image>();
        timeRect = timeBox.GetComponent<RectTransform>();
        timeRect.anchoredPosition = new Vector2(-132f, -70f);
        timeLabel = CreateText(timeBox.transform, "00:00", 22, TextAnchor.MiddleCenter, Theme.TextLight);
        Stretch(timeLabel.rectTransform);
        timeIcon = CreateImage(timeBox.transform, "TimeIcon", "ggj/结算/icon_沙漏.png",
            new Vector2(0.5f, 0.5f), new Vector2(32f, 42f));

        survivalBar = CreatePanel(safe, "EndlessSurvivalBar", new Vector2(0.5f, 1f), new Vector2(SurvivalBarWidth, SurvivalBarHeight));
        survivalBarRect = survivalBar.GetComponent<RectTransform>();
        survivalBarRect.pivot = new Vector2(0.5f, 1f);
        survivalBarRect.anchoredPosition = SurvivalBarPosition;
        survivalBackground = survivalBar.GetComponent<Image>();
        survivalBackground.sprite = GetSurvivalPillSprite();
        survivalBackground.type = Image.Type.Sliced;
        survivalBackground.color = SurvivalTrack;
        survivalBackground.raycastTarget = false;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(survivalBar.transform, false);
        survivalFillRect = fillObject.GetComponent<RectTransform>();
        survivalFillRect.anchorMin = survivalFillRect.anchorMax = new Vector2(0f, 0.5f);
        survivalFillRect.pivot = new Vector2(0f, 0.5f);
        survivalFillRect.anchoredPosition = Vector2.zero;
        survivalFillRect.sizeDelta = new Vector2(SurvivalBarWidth, SurvivalBarHeight);
        survivalFill = fillObject.GetComponent<Image>();
        survivalFill.sprite = survivalBackground.sprite;
        survivalFill.type = Image.Type.Sliced;
        survivalFill.color = SurvivalNormal;
        survivalFill.raycastTarget = false;

        ApplyTimeStyle(true);
    }

    private void Update()
    {
        if (!IsVisible) return;
        UpdateCoinPop(Time.unscaledDeltaTime);
        UpdateSurvivalBar(Time.unscaledDeltaTime);
        ApplyTimeStyle();
        if (coinLabel != null && coinProvider != null)
        {
            string s = coinProvider();
            if (coinLabel.text != s) coinLabel.text = s;
        }
        if (timeLabel != null && timeProvider != null)
        {
            string s = timeProvider();
            if (timeLabel.text != s) timeLabel.text = s;
        }
    }

    public override void Show()
    {
        base.Show();
        SubscribeCoinEvents();
    }

    public override void Hide()
    {
        UnsubscribeCoinEvents();
        ResetCoinPop();
        base.Hide();
    }

    private void OnDestroy()
    {
        UnsubscribeCoinEvents();
        ResetCoinPop();
    }

    private void SubscribeCoinEvents()
    {
        if (coinEventsSubscribed) return;
        RiverCollector.CoinsGained += HandleCoinsGained;
        RiverCollector.LeavesCollected += HandleLeavesCollected;
        coinEventsSubscribed = true;
    }

    private void UnsubscribeCoinEvents()
    {
        if (!coinEventsSubscribed) return;
        RiverCollector.CoinsGained -= HandleCoinsGained;
        RiverCollector.LeavesCollected -= HandleLeavesCollected;
        coinEventsSubscribed = false;
    }

    private void HandleCoinsGained(float amount)
    {
        if (amount <= 0f || !IsVisible || coinIconRect == null || coinLabel == null) return;

        coinPopStartScale = coinIconRect.localScale.x;
        coinPopElapsed = 0f;
        coinPopPlaying = true;
    }

    private void HandleLeavesCollected(int count)
    {
        if (count <= 0 || !IsVisible || endlessModeProvider == null || !endlessModeProvider()) return;
        survivalPopElapsed = 0f;
    }

    private void UpdateCoinPop(float deltaTime)
    {
        if (!coinPopPlaying) return;
        coinPopElapsed += Mathf.Max(0f, deltaTime);
        ApplyCoinPopPose(coinPopElapsed);
    }

    private void ApplyCoinPopPose(float time)
    {
        if (coinIconRect == null || coinLabel == null)
        {
            coinPopPlaying = false;
            return;
        }

        float scale;
        if (time <= CoinPopPeakTime)
        {
            float rate = Smooth01(time / CoinPopPeakTime);
            scale = Mathf.LerpUnclamped(coinPopStartScale, CoinPopPeakScale, rate);
        }
        else if (time <= CoinPopReboundTime)
        {
            float rate = Smooth01((time - CoinPopPeakTime) / (CoinPopReboundTime - CoinPopPeakTime));
            scale = Mathf.LerpUnclamped(CoinPopPeakScale, CoinPopReboundScale, rate);
        }
        else if (time < CoinPopDuration)
        {
            float rate = Smooth01((time - CoinPopReboundTime) / (CoinPopDuration - CoinPopReboundTime));
            scale = Mathf.LerpUnclamped(CoinPopReboundScale, 1f, rate);
        }
        else
        {
            ResetCoinPop();
            return;
        }

        SetCoinPopScale(scale);
    }

    private void ResetCoinPop()
    {
        coinPopPlaying = false;
        coinPopElapsed = 0f;
        coinPopStartScale = 1f;
        SetCoinPopScale(1f);
    }

    private void SetCoinPopScale(float scale)
    {
        Vector3 value = Vector3.one * scale;
        if (coinIconRect != null) coinIconRect.localScale = value;
        if (coinLabel != null) coinLabel.rectTransform.localScale = value;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void ApplyTimeStyle(bool force = false)
    {
        if (timeRect == null || timeBackground == null || timeLabel == null || timeIcon == null) return;
        bool endless = endlessModeProvider != null && endlessModeProvider();
        if (!force && endlessStyleApplied.HasValue && endlessStyleApplied.Value == endless) return;
        endlessStyleApplied = endless;

        if (survivalBar != null) survivalBar.SetActive(endless);

        if (endless)
        {
            timeRect.anchorMin = timeRect.anchorMax = Vector2.one;
            timeRect.pivot = Vector2.one;
            timeRect.sizeDelta = new Vector2(210f, 42f);
            timeRect.anchoredPosition = new Vector2(-132f, -70f);
            timeBackground.sprite = null;
            timeBackground.color = new Color(0.10f, 0.10f, 0.08f, 0.70f);

            RectTransform compactIconRect = timeIcon.rectTransform;
            compactIconRect.anchorMin = compactIconRect.anchorMax = new Vector2(0f, 0.5f);
            compactIconRect.pivot = new Vector2(0f, 0.5f);
            compactIconRect.sizeDelta = new Vector2(22f, 29f);
            compactIconRect.anchoredPosition = new Vector2(12f, 0f);
            timeIcon.color = new Color(1f, 1f, 1f, TimeContentAlpha);

            timeLabel.rectTransform.offsetMin = new Vector2(42f, 0f);
            timeLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
            timeLabel.fontSize = 22;
            timeLabel.fontStyle = FontStyle.Normal;
            timeLabel.color = Theme.TextLight;
            return;
        }

        timeRect.anchorMin = timeRect.anchorMax = new Vector2(0.5f, 1f);
        timeRect.pivot = new Vector2(0.5f, 1f);
        timeRect.sizeDelta = new Vector2(240f, 76f);
        timeRect.anchoredPosition = new Vector2(0f, -52f);
        timeBackground.sprite = RuntimeArt.LoadSprite("ggj/局内/常用_正常.png");
        timeBackground.color = new Color(1f, 1f, 1f, TimeBackgroundAlpha);

        RectTransform iconRect = timeIcon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(32f, 42f);
        iconRect.anchoredPosition = new Vector2(-68f, 0f);
        timeIcon.color = new Color(1f, 1f, 1f, TimeContentAlpha);

        timeLabel.rectTransform.offsetMin = new Vector2(76f, 0f);
        timeLabel.rectTransform.offsetMax = new Vector2(-18f, 0f);
        timeLabel.fontSize = 42;
        timeLabel.fontStyle = FontStyle.Bold;
        Color labelColor = Theme.TextDark;
        labelColor.a = TimeContentAlpha;
        timeLabel.color = labelColor;
    }

    private void UpdateSurvivalBar(float deltaTime)
    {
        if (survivalBar == null || survivalBarRect == null || survivalFill == null || survivalFillRect == null) return;

        bool endless = endlessModeProvider != null && endlessModeProvider();
        if (!endless)
        {
            survivalBar.SetActive(false);
            return;
        }

        if (!survivalBar.activeSelf) survivalBar.SetActive(true);
        float ratio = survivalRatioProvider != null ? Mathf.Clamp01(survivalRatioProvider()) : 0f;
        survivalFillRect.sizeDelta = new Vector2(SurvivalBarWidth * ratio, SurvivalBarHeight);

        bool failing = endlessFailureProvider != null && endlessFailureProvider();
        Color color;
        if (failing)
        {
            bool bright = Mathf.FloorToInt(Time.unscaledTime / 0.1f) % 2 == 0;
            color = SurvivalLow;
            color.a = bright ? 1f : 0.25f;
            survivalBackground.color = new Color(SurvivalLow.r, SurvivalLow.g, SurvivalLow.b, bright ? 0.9f : 0.25f);
        }
        else if (ratio <= 0.25f)
        {
            color = SurvivalLow;
            color.a = Mathf.Lerp(0.45f, 1f, Mathf.PingPong(Time.unscaledTime * 2f, 1f));
        }
        else
        {
            color = SurvivalNormal;
        }

        if (!failing) survivalBackground.color = SurvivalTrack;

        survivalFill.color = color;
        survivalPopElapsed = Mathf.Min(SurvivalPopDuration, survivalPopElapsed + Mathf.Max(0f, deltaTime));
        float popRate = survivalPopElapsed / SurvivalPopDuration;
        float popOffset = Mathf.Sin(popRate * Mathf.PI) * SurvivalPopOffset;
        survivalBarRect.anchoredPosition = SurvivalBarPosition + Vector2.up * popOffset;
    }

    private static Sprite GetSurvivalPillSprite()
    {
        if (survivalPillSprite != null) return survivalPillSprite;

        const int width = 64;
        const int height = 32;
        const float radius = height * 0.5f;
        const float segmentHalfLength = width * 0.5f - radius;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "EndlessSurvivalPillTexture";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            float localY = y + 0.5f - height * 0.5f;
            for (int x = 0; x < width; x++)
            {
                float localX = x + 0.5f - width * 0.5f;
                float closestX = Mathf.Clamp(localX, -segmentHalfLength, segmentHalfLength);
                float distance = Mathf.Sqrt(Mathf.Pow(localX - closestX, 2f) + localY * localY) - radius;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        survivalPillSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        survivalPillSprite.name = "EndlessSurvivalPill";
        survivalPillSprite.hideFlags = HideFlags.HideAndDontSave;
        return survivalPillSprite;
    }
}
