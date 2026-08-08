using UnityEngine;
using UnityEngine.UI;

// 关卡选择：使用关卡选择素材拼四个可玩入口。
public class LevelSelectUI : UIBase
{
    private System.Action<GameSceneBuilder.LevelSize> onPick;
    private System.Action onBack;

    public void Init(System.Action<GameSceneBuilder.LevelSize> pickCallback, System.Action backCallback)
    {
        onPick = pickCallback;
        onBack = backCallback;
    }

    protected override void Build()
    {
        CreateImageStretch(transform, "Background", "ggj/关卡选择/bg.png");
        RectTransform safe = CreateSafeArea(transform);

        CreateImageButton(safe, "Back", "", new Vector2(0f, 1f), new Vector2(128f, 38f),
            () => onBack?.Invoke(), "ggj/关卡选择/btn_返回正常.png", null, "ggj/关卡选择/btn_返回按下.png", new Vector2(0f, 1f));

        Text title = CreateText(safe, "选择地图", 30, TextAnchor.MiddleCenter, Theme.TextDark);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.86f);
        titleRect.anchorMax = new Vector2(1f, 0.96f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;

        CreateLevelCard(safe, "SimpleSmall", "ggj/关卡选择/简单小图.png", 0.12f, GameSceneBuilder.LevelSize.SimpleSmall);
        CreateLevelCard(safe, "ClassicLarge", "ggj/关卡选择/经典大图.png", 0.37f, GameSceneBuilder.LevelSize.ClassicLarge);
        CreateLevelCard(safe, "TimedChallenge", "ggj/关卡选择/限时挑战.png", 0.62f, GameSceneBuilder.LevelSize.TimedChallenge);
        CreateLevelCard(safe, "Endless", "ggj/关卡选择/无尽模式.png", 0.87f, GameSceneBuilder.LevelSize.Endless);
    }

    private void CreateLevelCard(Transform parent, string name, string spritePath, float x, GameSceneBuilder.LevelSize levelSize)
    {
        CreateImageButton(parent, name, "", new Vector2(x, 0.46f), new Vector2(300f, 386f),
            () => onPick?.Invoke(levelSize), spritePath);
    }
}
