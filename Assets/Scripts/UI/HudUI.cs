using UnityEngine;
using UnityEngine.UI;

// 顶部 HUD：设置入口 + 商店入口 + 金币 + 时间。
public class HudUI : UIBase
{
    private System.Action onToggleSettings;
    private System.Action onToggleShop;
    private System.Func<string> coinProvider;
    private System.Func<string> timeProvider;

    private Text coinLabel;
    private Text timeLabel;

    public void Bind(System.Action toggleSettings, System.Action toggleShop,
                     System.Func<string> coinProvider, System.Func<string> timeProvider)
    {
        onToggleSettings = toggleSettings;
        onToggleShop = toggleShop;
        this.coinProvider = coinProvider;
        this.timeProvider = timeProvider;
    }

    protected override void Build()
    {
        RectTransform safe = CreateSafeArea(transform);

        CreateImageButton(safe, "Pause", "", new Vector2(0f, 1f), new Vector2(120f, 69f),
            () => onToggleSettings?.Invoke(), "ggj/局内/设置暂停_正常.png", null, "ggj/局内/设置暂停_按下.png", new Vector2(0f, 1f));

        CreateImageButton(safe, "Shop", "", new Vector2(1f, 1f), new Vector2(120f, 69f),
            () => onToggleShop?.Invoke(), "ggj/局内/商店正常.png", null, "ggj/局内/商店按下.png", new Vector2(1f, 1f));

        GameObject coinBox = CreatePanel(safe, "CoinBox", new Vector2(1f, 1f), new Vector2(210f, 54f));
        coinBox.GetComponent<Image>().sprite = RuntimeArt.LoadSprite("ggj/局内/常用_正常.png");
        coinBox.GetComponent<Image>().color = Color.white;
        RectTransform coinRect = coinBox.GetComponent<RectTransform>();
        coinRect.anchoredPosition = new Vector2(-132f, -8f);

        CreateImage(coinBox.transform, "GoldIcon", "ggj/局内/gold.png", new Vector2(0f, 0.5f), new Vector2(32f, 32f), true, new Vector2(0f, 0.5f));
        coinLabel = CreateText(coinBox.transform, "0", 22, TextAnchor.MiddleCenter, Theme.TextLight);
        Stretch(coinLabel.rectTransform);
        coinLabel.rectTransform.offsetMin = new Vector2(38f, 0f);

        GameObject timeBox = CreatePanel(safe, "Time", new Vector2(1f, 1f), new Vector2(154f, 42f));
        timeBox.GetComponent<Image>().color = Theme.PanelShadow;
        RectTransform timeRect = timeBox.GetComponent<RectTransform>();
        timeRect.anchoredPosition = new Vector2(-132f, -70f);
        timeLabel = CreateText(timeBox.transform, "00:00", 22, TextAnchor.MiddleCenter, Theme.TextLight);
        Stretch(timeLabel.rectTransform);
    }

    private void Update()
    {
        if (!IsVisible) return;
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
}
