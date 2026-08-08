using UnityEngine;
using UnityEngine.UI;

// 商店浮层：四个强化项，数值来自 UpgradeCatalog。
public class ShopUI : UIBase
{
    private const string BuyNormalSpritePath = "ggj/通用/常用_正常.png";
    private const string BuyHoverSpritePath = "ggj/通用/常用_悬浮.png";
    private const string BuyPressedSpritePath = "ggj/通用/常用_按下.png";
    private const string BuyNormalGoldPath = "ggj/通用/gold/gold白.png";
    private const string BuyPressedGoldPath = "ggj/通用/gold/gold.png";
    private const string CoinBadgeGoldPath = "ggj/通用/gold/gold.png";

    private static readonly Color PriceColor = new Color(0.45f, 0.30f, 0.14f);
    private static readonly Color PriceDisabledColor = new Color(0.45f, 0.30f, 0.14f, 0.45f);

    private System.Action<UpgradeKind> onBuy;
    private System.Action onClose;
    private System.Func<UpgradeKind, string> infoProvider;
    private System.Func<UpgradeKind, bool> canBuyProvider;
    private System.Func<UpgradeKind, string> priceProvider;
    private System.Func<string> coinProvider;

    private readonly Text[] infoLabels = new Text[UpgradeCatalog.All.Length];
    private readonly Text[] priceLabels = new Text[UpgradeCatalog.All.Length];
    private readonly Button[] buyButtons = new Button[UpgradeCatalog.All.Length];
    private Text coinLabel;

    public void Bind(
        System.Action<UpgradeKind> onBuy,
        System.Action onClose,
        System.Func<UpgradeKind, string> infoProvider,
        System.Func<UpgradeKind, bool> canBuyProvider,
        System.Func<UpgradeKind, string> priceProvider,
        System.Func<string> coinProvider)
    {
        this.onBuy = onBuy;
        this.onClose = onClose;
        this.infoProvider = infoProvider;
        this.canBuyProvider = canBuyProvider;
        this.priceProvider = priceProvider;
        this.coinProvider = coinProvider;
    }

    protected override void Build()
    {
        CreateFrostedBackdrop(transform);

        RectTransform safe = CreateSafeArea(transform);
        Image panel = CreateImage(safe, "ShopPanel", "ggj/道具强化/弹窗.png", new Vector2(0.5f, 0.52f), new Vector2(1180f, 510f), false);
        panel.color = Color.white;

        Transform panelRoot = panel.transform;
        CreateImage(panelRoot, "Title", "ggj/道具强化/道具强化.png", new Vector2(0.5f, 0.88f), new Vector2(420f, 100f));
        CreateCoinBadge(panelRoot);
        CreateImageButton(panelRoot, "Close", "", new Vector2(0.90f, 0.84f), new Vector2(82f, 47f),
            () => onClose?.Invoke(), "ggj/道具强化/返回正常.png", null, "ggj/道具强化/返回按下.png");

        float[] cardX = { 0.18f, 0.39f, 0.60f, 0.81f };
        for (int i = 0; i < UpgradeCatalog.All.Length; i++)
        {
            CreateUpgradeCard(panelRoot, UpgradeCatalog.All[i], i, cardX[i]);
        }
    }

    private void CreateCoinBadge(Transform parent)
    {
        Image badge = CreateImage(parent, "CoinBadge", BuyNormalSpritePath, new Vector2(0.13f, 0.84f), new Vector2(205f, 70f));
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
        Image card = CreateImage(parent, kind.ToString(), "ggj/道具强化/选项背景.png", new Vector2(x, 0.45f), new Vector2(205f, 266f));
        Transform cardRoot = card.transform;

        CreateImage(cardRoot, "IconBg", "ggj/道具强化/icon_bg.png", new Vector2(0.5f, 0.76f), new Vector2(70f, 70f));
        Text title = CreateText(cardRoot, UpgradeCatalog.GetName(kind), 18, TextAnchor.MiddleCenter, Theme.TextDark);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.08f, 0.62f);
        titleRect.anchorMax = new Vector2(0.92f, 0.72f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

        Text info = CreateText(cardRoot, "", 15, TextAnchor.MiddleCenter, Theme.TextDark);
        RectTransform infoRect = info.rectTransform;
        infoRect.anchorMin = new Vector2(0.08f, 0.31f);
        infoRect.anchorMax = new Vector2(0.92f, 0.60f);
        infoRect.offsetMin = infoRect.offsetMax = Vector2.zero;
        infoLabels[index] = info;

        Button buyButton = CreateImageButton(cardRoot, "Buy", "", new Vector2(0.5f, 0.15f), new Vector2(126f, 43f),
            () => onBuy?.Invoke(kind), BuyNormalSpritePath, BuyHoverSpritePath, BuyPressedSpritePath);
        buyButtons[index] = buyButton;

        Image goldIcon = CreateImage(buyButton.transform, "GoldIcon", BuyNormalGoldPath, new Vector2(0.5f, 0.5f), new Vector2(28f, 28f));
        goldIcon.raycastTarget = false;
        RectTransform goldRect = goldIcon.rectTransform;
        goldRect.anchorMin = goldRect.anchorMax = new Vector2(0.34f, 0.5f);

        Text price = CreateText(buyButton.transform, "0", 18, TextAnchor.MiddleLeft, PriceColor);
        price.raycastTarget = false;
        price.horizontalOverflow = HorizontalWrapMode.Overflow;
        price.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform priceRect = price.rectTransform;
        priceRect.anchorMin = new Vector2(0.48f, 0f);
        priceRect.anchorMax = new Vector2(0.92f, 1f);
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

        if (coinLabel != null && coinProvider != null)
        {
            string coins = coinProvider();
            if (coinLabel.text != coins) coinLabel.text = coins;
        }

        for (int i = 0; i < UpgradeCatalog.All.Length; i++)
        {
            UpgradeKind kind = UpgradeCatalog.All[i];
            if (infoLabels[i] != null && infoProvider != null)
            {
                string text = infoProvider(kind);
                if (infoLabels[i].text != text) infoLabels[i].text = text;
            }

            if (priceLabels[i] != null && priceProvider != null)
            {
                string price = priceProvider(kind);
                if (priceLabels[i].text != price) priceLabels[i].text = price;
            }

            if (buyButtons[i] != null && canBuyProvider != null)
            {
                bool canBuy = canBuyProvider(kind);
                buyButtons[i].interactable = canBuy;
                if (buyButtons[i].targetGraphic != null)
                {
                    buyButtons[i].targetGraphic.color = canBuy ? Color.white : Theme.ButtonImageDisabled;
                }
                if (priceLabels[i] != null)
                {
                    priceLabels[i].color = canBuy ? PriceColor : PriceDisabledColor;
                }
            }
        }
    }
}
