using UnityEngine;
using UnityEngine.UI;

// 结算：使用结算素材，含用时、本局金币、总金币和三个跳转按钮。
public class ResultUI : UIBase
{
    private System.Action onReplay;
    private System.Action onLevelSelect;
    private System.Action onBackToMenu;
    private System.Func<string> timeProvider;
    private System.Func<string> sessionCoinProvider;
    private System.Func<string> totalCoinProvider;
    private System.Func<bool> successProvider;

    private Image successImage;
    private Text resultTitleLabel;
    private Text timeLabel;
    private Text sessionLabel;
    private Text totalLabel;

    public void Bind(System.Action onReplay, System.Action onLevelSelect, System.Action onBackToMenu,
                     System.Func<string> timeProvider,
                     System.Func<string> sessionCoinProvider,
                     System.Func<string> totalCoinProvider,
                     System.Func<bool> successProvider)
    {
        this.onReplay = onReplay;
        this.onLevelSelect = onLevelSelect;
        this.onBackToMenu = onBackToMenu;
        this.timeProvider = timeProvider;
        this.sessionCoinProvider = sessionCoinProvider;
        this.totalCoinProvider = totalCoinProvider;
        this.successProvider = successProvider;
    }

    protected override void Build()
    {
        CreateFrostedBackdrop(transform);

        RectTransform safe = CreateSafeArea(transform);
        Image panel = CreateImage(safe, "ResultPanel", "ggj/结算/弹窗.png", new Vector2(0.5f, 0.50f), new Vector2(1120f, 362f), false);
        Transform panelRoot = panel.transform;

        successImage = CreateImage(panelRoot, "Success", "ggj/结算/imh_清理成功.png", new Vector2(0.5f, 0.82f), new Vector2(450f, 106f));
        resultTitleLabel = CreateText(panelRoot, "挑战失败", 48, TextAnchor.MiddleCenter, Theme.TextDark);
        RectTransform titleRect = resultTitleLabel.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.68f);
        titleRect.anchorMax = new Vector2(1f, 0.96f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
        resultTitleLabel.enabled = false;

        CreateImage(panelRoot, "TimeIcon", "ggj/结算/icon_沙漏.png", new Vector2(0.31f, 0.56f), new Vector2(32f, 42f));
        CreateImage(panelRoot, "GoldIcon", "ggj/结算/gold_绿.png", new Vector2(0.31f, 0.42f), new Vector2(34f, 34f));

        timeLabel = MakeInfo(panelRoot, 0.56f);
        sessionLabel = MakeInfo(panelRoot, 0.42f);
        totalLabel = MakeInfo(panelRoot, 0.30f);

        CreateImageButton(panelRoot, "Replay", "", new Vector2(0.30f, 0.10f), new Vector2(205f, 70f),
            () => onReplay?.Invoke(), "ggj/结算/再来一次正常.png", "ggj/结算/再来一次悬浮.png", "ggj/结算/再来一次按下.png");
        CreateImageButton(panelRoot, "LevelSelect", "", new Vector2(0.50f, 0.10f), new Vector2(205f, 70f),
            () => onLevelSelect?.Invoke(), "ggj/结算/关卡选择正常.png", "ggj/结算/关卡选择悬浮.png", "ggj/结算/关卡选择按下.png");
        CreateImageButton(panelRoot, "MainMenu", "", new Vector2(0.70f, 0.10f), new Vector2(205f, 70f),
            () => onBackToMenu?.Invoke(), "ggj/结算/返回主界面正常.png", "ggj/结算/返回主界面悬浮.png", "ggj/结算/返回主界面按下.png");
    }

    private static Text MakeInfo(Transform parent, float topRatio)
    {
        Text t = CreateText(parent, "", 24, TextAnchor.MiddleLeft, Theme.TextDark);
        RectTransform r = t.rectTransform;
        r.anchorMin = new Vector2(0.34f, topRatio - 0.06f);
        r.anchorMax = new Vector2(0.76f, topRatio + 0.06f);
        r.offsetMin = r.offsetMax = Vector2.zero;
        return t;
    }

    private void Update()
    {
        if (!IsVisible) return;
        bool success = successProvider == null || successProvider();
        if (successImage != null) successImage.enabled = success;
        if (resultTitleLabel != null) resultTitleLabel.enabled = !success;

        if (timeLabel    != null && timeProvider    != null) timeLabel.text    = "用时："    + timeProvider();
        if (sessionLabel != null && sessionCoinProvider != null) sessionLabel.text = "本局金币：" + sessionCoinProvider();
        if (totalLabel   != null && totalCoinProvider   != null) totalLabel.text   = "总金币："  + totalCoinProvider();
    }
}
