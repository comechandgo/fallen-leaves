using UnityEngine;
using UnityEngine.UI;

// 结算：使用结算素材，含用时、本局金币、剩余金币、限时模式树叶数和三个跳转按钮。
public class ResultUI : UIBase
{
    private System.Action onReplay;
    private System.Action onLevelSelect;
    private System.Action onBackToMenu;
    private System.Func<string> timeProvider;
    private System.Func<string> sessionCoinProvider;
    private System.Func<string> remainingCoinProvider;
    private System.Func<string> sessionLeafProvider;
    private System.Func<bool> timedModeProvider;
    private System.Func<bool> endlessModeProvider;
    private System.Func<bool> successProvider;

    private RectTransform panelRect;
    private Image successImage;
    private Text resultTitleLabel;
    private Text timeLabel;
    private Text sessionLabel;
    private Text remainingLabel;
    private Text leafLabel;
    private RectTransform timeIconRect;
    private RectTransform goldIconRect;
    private int appliedMode = -1;

    public void Bind(System.Action onReplay, System.Action onLevelSelect, System.Action onBackToMenu,
                     System.Func<string> timeProvider,
                     System.Func<string> sessionCoinProvider,
                     System.Func<string> remainingCoinProvider,
                     System.Func<string> sessionLeafProvider,
                     System.Func<bool> timedModeProvider,
                     System.Func<bool> endlessModeProvider,
                     System.Func<bool> successProvider)
    {
        this.onReplay = onReplay;
        this.onLevelSelect = onLevelSelect;
        this.onBackToMenu = onBackToMenu;
        this.timeProvider = timeProvider;
        this.sessionCoinProvider = sessionCoinProvider;
        this.remainingCoinProvider = remainingCoinProvider;
        this.sessionLeafProvider = sessionLeafProvider;
        this.timedModeProvider = timedModeProvider;
        this.endlessModeProvider = endlessModeProvider;
        this.successProvider = successProvider;
    }

    protected override void Build()
    {
        CreateFrostedBackdrop(transform);

        RectTransform safe = CreateSafeArea(transform);
        Image panel = CreateImage(safe, "ResultPanel", "ggj/结算/弹窗.png", new Vector2(0.5f, 0.50f), new Vector2(1120f, 362f), false);
        panelRect = panel.rectTransform;
        Transform panelRoot = panel.transform;

        successImage = CreateImage(panelRoot, "Success", "ggj/结算/imh_清理成功.png", new Vector2(0.5f, 0.82f), new Vector2(450f, 106f));
        resultTitleLabel = CreateText(panelRoot, "挑战失败", 48, TextAnchor.MiddleCenter, Theme.TextDark);
        RectTransform titleRect = resultTitleLabel.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.68f);
        titleRect.anchorMax = new Vector2(1f, 0.96f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
        resultTitleLabel.enabled = false;

        timeIconRect = CreateImage(panelRoot, "TimeIcon", "ggj/结算/icon_沙漏.png", new Vector2(0.31f, 0.56f), new Vector2(32f, 42f)).rectTransform;
        goldIconRect = CreateImage(panelRoot, "GoldIcon", "ggj/结算/gold_绿.png", new Vector2(0.31f, 0.42f), new Vector2(34f, 34f)).rectTransform;

        timeLabel = MakeInfo(panelRoot, 0.56f);
        sessionLabel = MakeInfo(panelRoot, 0.42f);
        remainingLabel = MakeInfo(panelRoot, 0.30f);
        leafLabel = MakeInfo(panelRoot, 0.25f);

        CreateImageButton(panelRoot, "Replay", "", new Vector2(0.30f, 0.10f), new Vector2(205f, 70f),
            () => onReplay?.Invoke(), "ggj/结算/再来一次正常.png", "ggj/结算/再来一次悬浮.png", "ggj/结算/再来一次按下.png");
        CreateImageButton(panelRoot, "LevelSelect", "", new Vector2(0.50f, 0.10f), new Vector2(205f, 70f),
            () => onLevelSelect?.Invoke(), "ggj/结算/关卡选择正常.png", "ggj/结算/关卡选择悬浮.png", "ggj/结算/关卡选择按下.png");
        CreateImageButton(panelRoot, "MainMenu", "", new Vector2(0.70f, 0.10f), new Vector2(205f, 70f),
            () => onBackToMenu?.Invoke(), "ggj/结算/返回主界面正常.png", "ggj/结算/返回主界面悬浮.png", "ggj/结算/返回主界面按下.png");

        ApplyModeLayout(true);
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
        bool timed = timedModeProvider != null && timedModeProvider();
        bool endless = endlessModeProvider != null && endlessModeProvider();
        ApplyModeLayout();
        bool success = successProvider == null || successProvider();
        if (successImage != null) successImage.enabled = success && !endless;
        if (resultTitleLabel != null)
        {
            resultTitleLabel.enabled = !success || endless;
            resultTitleLabel.text = endless ? "挑战结束" : "挑战失败";
        }

        if (timeLabel != null && timeProvider != null)
        {
            timeLabel.text = (endless ? "坚持时间：" : "用时：") + timeProvider();
        }
        if (sessionLabel != null && sessionCoinProvider != null) sessionLabel.text = "本局金币：" + sessionCoinProvider();
        if (remainingLabel != null && remainingCoinProvider != null)
        {
            remainingLabel.text = "剩余金币：" + remainingCoinProvider();
        }
        if ((timed || endless) && leafLabel != null && sessionLeafProvider != null)
        {
            leafLabel.text = (endless ? "入河树叶：" : "最终获得树叶：") + sessionLeafProvider();
        }
    }

    private void ApplyModeLayout(bool force = false)
    {
        if (panelRect == null || timeLabel == null || sessionLabel == null
            || remainingLabel == null || leafLabel == null) return;

        bool timed = timedModeProvider != null && timedModeProvider();
        bool endless = endlessModeProvider != null && endlessModeProvider();
        int mode = endless ? 2 : (timed ? 1 : 0);
        if (!force && appliedMode == mode) return;
        appliedMode = mode;

        bool fourRows = timed || endless;
        panelRect.sizeDelta = fourRows ? new Vector2(1120f, 430f) : new Vector2(1120f, 362f);
        leafLabel.enabled = fourRows;

        if (endless)
        {
            SetInfoPosition(timeLabel, 0.60f);
            SetInfoPosition(leafLabel, 0.48f);
            SetInfoPosition(sessionLabel, 0.36f);
            SetInfoPosition(remainingLabel, 0.24f);
            SetIconPosition(timeIconRect, 0.60f);
            SetIconPosition(goldIconRect, 0.36f);
            return;
        }

        float timeRatio = timed ? 0.60f : 0.56f;
        float sessionRatio = timed ? 0.48f : 0.42f;
        float remainingRatio = timed ? 0.36f : 0.30f;

        SetInfoPosition(timeLabel, timeRatio);
        SetInfoPosition(sessionLabel, sessionRatio);
        SetInfoPosition(remainingLabel, remainingRatio);
        SetInfoPosition(leafLabel, 0.25f);
        SetIconPosition(timeIconRect, timeRatio);
        SetIconPosition(goldIconRect, sessionRatio);
    }

    private static void SetInfoPosition(Text label, float ratio)
    {
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.34f, ratio - 0.06f);
        rect.anchorMax = new Vector2(0.76f, ratio + 0.06f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetIconPosition(RectTransform rect, float ratio)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.31f, ratio);
    }
}
