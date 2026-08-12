using UnityEngine;
using UnityEngine.UI;

// 风形态 + 三条升级线面板。
public class ShopUI : UIBase
{
    private const string BuyNormalSpritePath = "ggj/通用/常用_正常.png";
    private const string BuyHoverSpritePath = "ggj/通用/常用_悬浮.png";
    private const string BuyPressedSpritePath = "ggj/通用/常用_按下.png";
    private const string BuyNormalGoldPath = "ggj/通用/gold/gold白.png";
    private const string BuyPressedGoldPath = "ggj/通用/gold/gold.png";
    private const string CoinBadgeGoldPath = "ggj/通用/gold/gold.png";

    private const string PowerIconPath = "ggj/道具强化/icon_wind_power.png";
    private const string AreaIconPath = "ggj/道具强化/icon_wind_area.png";
    private const string PulseIconPath = "ggj/道具强化/icon_wind_pulse.png";
    private const string DownburstFormPath = "ggj/道具强化/wind_form_downburst.png";
    private const string SurfaceFormPath = "ggj/道具强化/wind_form_surface.png";
    private const string TornadoFormPath = "ggj/道具强化/wind_form_tornado.png";

    private static readonly Color PriceColor = new Color(0.45f, 0.30f, 0.14f);
    private static readonly Color PriceDisabledColor = new Color(0.45f, 0.30f, 0.14f, 0.45f);
    private static readonly Color BadgeColor = new Color(0.62f, 0.39f, 0.16f);

    private System.Action<UpgradeKind> onBuy;
    private System.Action onClose;
    private System.Func<UpgradeKind, string> titleProvider;
    private System.Func<UpgradeKind, string> infoProvider;
    private System.Func<UpgradeKind, bool> canBuyProvider;
    private System.Func<UpgradeKind, string> priceProvider;
    private System.Func<UpgradeKind, string> levelProvider;
    private System.Func<string> coinProvider;
    private System.Func<string> headerProvider;
    private System.Func<string> formProvider;
    private System.Func<bool> canCloseProvider;

    private readonly Text[] titleLabels = new Text[UpgradeCatalog.All.Length];
    private readonly Text[] infoLabels = new Text[UpgradeCatalog.All.Length];
    private readonly Text[] priceLabels = new Text[UpgradeCatalog.All.Length];
    private readonly Text[] levelLabels = new Text[UpgradeCatalog.All.Length];
    private readonly Button[] buyButtons = new Button[UpgradeCatalog.All.Length];
    private readonly Image[] buyGoldIcons = new Image[UpgradeCatalog.All.Length];

    private Text coinLabel;
    private Text windNameLabel;
    private Text formLabel;
    private Image formImage;
    private Button closeButton;
    private string currentFormImagePath;

    public void Bind(
        System.Action<UpgradeKind> onBuy,
        System.Action onClose,
        System.Func<UpgradeKind, string> titleProvider,
        System.Func<UpgradeKind, string> infoProvider,
        System.Func<UpgradeKind, bool> canBuyProvider,
        System.Func<UpgradeKind, string> priceProvider,
        System.Func<UpgradeKind, string> levelProvider,
        System.Func<string> coinProvider,
        System.Func<string> headerProvider,
        System.Func<string> formProvider,
        System.Func<bool> canCloseProvider)
    {
        this.onBuy = onBuy;
        this.onClose = onClose;
        this.titleProvider = titleProvider;
        this.infoProvider = infoProvider;
        this.canBuyProvider = canBuyProvider;
        this.priceProvider = priceProvider;
        this.levelProvider = levelProvider;
        this.coinProvider = coinProvider;
        this.headerProvider = headerProvider;
        this.formProvider = formProvider;
        this.canCloseProvider = canCloseProvider;
    }

    protected override void Build()
    {
        FrostedBackdrop backdrop = CreateFrostedBackdrop(transform);
        Button blankClose = backdrop.gameObject.AddComponent<Button>();
        blankClose.transition = Selectable.Transition.None;
        blankClose.onClick.AddListener(() =>
        {
            if (canCloseProvider == null || canCloseProvider())
            {
                onClose?.Invoke();
            }
        });

        RectTransform safe = CreateSafeArea(transform);
        Image panel = CreateImage(safe, "UpgradePanel", "ggj/道具强化/弹窗.png", new Vector2(0.5f, 0.52f), new Vector2(1260f, 640f), false);
        panel.color = Color.white;

        Transform panelRoot = panel.transform;

        Image titlePlate = CreateImage(panelRoot, "WindNamePlate", BuyNormalSpritePath, new Vector2(0.62f, 0.84f), new Vector2(560f, 70f), false);
        titlePlate.raycastTarget = false;
        windNameLabel = CreateText(titlePlate.transform, "迷你弱小清风下沉风", 27, TextAnchor.MiddleCenter, Theme.TextDark);
        Stretch(windNameLabel.rectTransform);
        windNameLabel.raycastTarget = false;

        formImage = CreateImage(panelRoot, "WindFormImage", DownburstFormPath, new Vector2(0.16f, 0.53f), new Vector2(245f, 245f), true);
        formImage.raycastTarget = false;

        formLabel = CreateText(panelRoot, "下沉风", 26, TextAnchor.MiddleCenter, Theme.TextDark);
        RectTransform formRect = formLabel.rectTransform;
        formRect.anchorMin = new Vector2(0.055f, 0.20f);
        formRect.anchorMax = new Vector2(0.265f, 0.30f);
        formRect.offsetMin = formRect.offsetMax = Vector2.zero;

        Text formTitle = CreateText(panelRoot, "风形态", 22, TextAnchor.MiddleCenter, Theme.TextDark);
        RectTransform formTitleRect = formTitle.rectTransform;
        formTitleRect.anchorMin = new Vector2(0.055f, 0.72f);
        formTitleRect.anchorMax = new Vector2(0.265f, 0.80f);
        formTitleRect.offsetMin = formTitleRect.offsetMax = Vector2.zero;

        CreateCoinBadge(panelRoot);
        closeButton = CreateImageButton(panelRoot, "Close", "", new Vector2(0.91f, 0.86f), new Vector2(82f, 47f),
            () => onClose?.Invoke(), "ggj/道具强化/返回正常.png", null, "ggj/道具强化/返回按下.png");

        float[] cardX = { 0.39f, 0.61f, 0.83f };

        for (int i = 0; i < UpgradeCatalog.All.Length; i++)
        {
            CreateUpgradeCard(panelRoot, UpgradeCatalog.All[i], i, cardX[i]);
        }
    }

    private void CreateCoinBadge(Transform parent)
    {
        Image badge = CreateImage(parent, "CoinBadge", BuyNormalSpritePath, new Vector2(0.14f, 0.86f), new Vector2(215f, 70f), false);
        Transform badgeRoot = badge.transform;

        Image goldIcon = CreateImage(badgeRoot, "GoldIcon", CoinBadgeGoldPath, new Vector2(0.22f, 0.5f), new Vector2(34f, 34f));
        goldIcon.raycastTarget = false;

        coinLabel = CreateText(badgeRoot, "0", 28, TextAnchor.MiddleLeft, PriceColor);
        coinLabel.raycastTarget = false;
        coinLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        coinLabel.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform coinRect = coinLabel.rectTransform;
        coinRect.anchorMin = new Vector2(0.38f, 0f);
        coinRect.anchorMax = new Vector2(0.94f, 1f);
        coinRect.offsetMin = coinRect.offsetMax = Vector2.zero;
    }

    private void CreateUpgradeCard(Transform parent, UpgradeKind kind, int index, float x)
    {
        Image card = CreateImage(parent, kind.ToString(), "ggj/道具强化/选项背景.png", new Vector2(x, 0.43f), new Vector2(232f, 330f), false);
        Transform cardRoot = card.transform;

        Image iconBg = CreateImage(cardRoot, "IconBg", "ggj/道具强化/icon_bg.png", new Vector2(0.50f, 0.79f), new Vector2(82f, 82f));
        iconBg.raycastTarget = false;

        Image icon = CreateImage(iconBg.transform, "Icon", GetIconPath(kind), new Vector2(0.5f, 0.5f), new Vector2(58f, 58f));
        icon.raycastTarget = false;

        Text level = CreateText(cardRoot, "0/2", 16, TextAnchor.MiddleCenter, BadgeColor);
        levelLabels[index] = level;
        RectTransform levelRect = level.rectTransform;
        levelRect.anchorMin = new Vector2(0.66f, 0.83f);
        levelRect.anchorMax = new Vector2(0.94f, 0.92f);
        levelRect.offsetMin = levelRect.offsetMax = Vector2.zero;

        Text title = CreateText(cardRoot, UpgradeCatalog.GetName(kind), 21, TextAnchor.MiddleCenter, Theme.TextDark);
        titleLabels[index] = title;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.08f, 0.64f);
        titleRect.anchorMax = new Vector2(0.92f, 0.74f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

        Text info = CreateText(cardRoot, "", 16, TextAnchor.MiddleCenter, Theme.TextDark);
        RectTransform infoRect = info.rectTransform;
        infoRect.anchorMin = new Vector2(0.08f, 0.30f);
        infoRect.anchorMax = new Vector2(0.92f, 0.61f);
        infoRect.offsetMin = infoRect.offsetMax = Vector2.zero;
        infoLabels[index] = info;

        Button buyButton = CreateImageButton(cardRoot, "Buy", "", new Vector2(0.5f, 0.15f), new Vector2(132f, 45f),
            () => onBuy?.Invoke(kind), BuyNormalSpritePath, BuyHoverSpritePath, BuyPressedSpritePath);
        buyButtons[index] = buyButton;

        Image goldIcon = CreateImage(buyButton.transform, "GoldIcon", BuyNormalGoldPath, new Vector2(0.34f, 0.5f), new Vector2(28f, 28f));
        goldIcon.raycastTarget = false;
        buyGoldIcons[index] = goldIcon;

        Text price = CreateText(buyButton.transform, "0", 19, TextAnchor.MiddleLeft, PriceColor);
        price.raycastTarget = false;
        price.horizontalOverflow = HorizontalWrapMode.Overflow;
        price.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform priceRect = price.rectTransform;
        priceRect.anchorMin = new Vector2(0.48f, 0f);
        priceRect.anchorMax = new Vector2(0.94f, 1f);
        priceRect.offsetMin = priceRect.offsetMax = Vector2.zero;
        priceLabels[index] = price;

        ButtonStateImage iconState = buyButton.gameObject.AddComponent<ButtonStateImage>();
        iconState.Init(
            goldIcon,
            RuntimeArt.LoadSprite(BuyNormalGoldPath),
            RuntimeArt.LoadSprite(BuyNormalGoldPath),
            RuntimeArt.LoadSprite(BuyPressedGoldPath));
    }

    private void Update()
    {
        if (!IsVisible) return;

        UpdateText(coinLabel, coinProvider);
        UpdateText(windNameLabel, headerProvider);
        UpdateText(formLabel, formProvider);
        UpdateFormImage();
        UpdateCloseButton();

        for (int i = 0; i < UpgradeCatalog.All.Length; i++)
        {
            UpgradeKind kind = UpgradeCatalog.All[i];

            if (titleLabels[i] != null && titleProvider != null)
            {
                string title = titleProvider(kind);
                if (titleLabels[i].text != title) titleLabels[i].text = title;
            }

            if (infoLabels[i] != null && infoProvider != null)
            {
                string text = infoProvider(kind);
                if (infoLabels[i].text != text) infoLabels[i].text = text;
            }

            if (levelLabels[i] != null && levelProvider != null)
            {
                string level = levelProvider(kind);
                if (levelLabels[i].text != level) levelLabels[i].text = level;
            }

            string price = priceProvider != null ? priceProvider(kind) : "";
            if (priceLabels[i] != null && priceLabels[i].text != price)
            {
                priceLabels[i].text = price;
            }

            bool showCoin = price != "选择" && price != "MAX";
            if (buyGoldIcons[i] != null)
            {
                buyGoldIcons[i].enabled = showCoin;
            }

            if (priceLabels[i] != null)
            {
                priceLabels[i].alignment = showCoin ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                priceLabels[i].rectTransform.anchorMin = showCoin ? new Vector2(0.48f, 0f) : Vector2.zero;
                priceLabels[i].rectTransform.anchorMax = showCoin ? new Vector2(0.94f, 1f) : Vector2.one;
                priceLabels[i].rectTransform.offsetMin = priceLabels[i].rectTransform.offsetMax = Vector2.zero;
            }

            UpdateButtonState(i, kind);
        }
    }

    private void UpdateCloseButton()
    {
        if (closeButton == null || canCloseProvider == null) return;

        bool canClose = canCloseProvider();
        closeButton.interactable = canClose;
        if (closeButton.targetGraphic != null)
        {
            closeButton.targetGraphic.color = canClose ? Color.white : Theme.ButtonImageDisabled;
        }
    }

    private void UpdateButtonState(int index, UpgradeKind kind)
    {
        if (buyButtons[index] == null || canBuyProvider == null) return;

        bool canBuy = canBuyProvider(kind);
        buyButtons[index].interactable = canBuy;
        if (buyButtons[index].targetGraphic != null)
        {
            buyButtons[index].targetGraphic.color = canBuy ? Color.white : Theme.ButtonImageDisabled;
        }
        if (priceLabels[index] != null)
        {
            priceLabels[index].color = canBuy ? PriceColor : PriceDisabledColor;
        }
    }

    private void UpdateFormImage()
    {
        if (formImage == null || formProvider == null) return;

        string nextPath = GetFormImagePath(formProvider());
        if (currentFormImagePath == nextPath) return;

        currentFormImagePath = nextPath;
        formImage.sprite = RuntimeArt.LoadSprite(nextPath);
    }

    private static void UpdateText(Text label, System.Func<string> provider)
    {
        if (label == null || provider == null) return;

        string value = provider();
        if (label.text != value) label.text = value;
    }

    private static string GetIconPath(UpgradeKind kind)
    {
        switch (kind)
        {
            case UpgradeKind.WindPower: return PowerIconPath;
            case UpgradeKind.WindArea: return AreaIconPath;
            case UpgradeKind.WindPulse: return PulseIconPath;
            default: return PowerIconPath;
        }
    }

    private static string GetFormImagePath(string formText)
    {
        if (!string.IsNullOrEmpty(formText))
        {
            if (formText.Contains("龙卷风")) return TornadoFormPath;
            if (formText.Contains("面风")) return SurfaceFormPath;
        }

        return DownburstFormPath;
    }
}
