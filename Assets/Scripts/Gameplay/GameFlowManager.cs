using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 状态机单一入口 + 持有 shop/settings 开关（解决 R3：唯一来源）。
// 自身不再画 UI（R1：所有 UI 走 UIBase / UGUI）。
public class GameFlowManager : MonoBehaviour
{
    public enum GameState { MainMenu, LevelSelect, Playing, Result }

    private GameState state = GameState.MainMenu;
    private GameSceneBuilder.LevelSize selectedLevel = GameSceneBuilder.LevelSize.SimpleSmall;

    private float elapsedTime;
    private float timeLimitSeconds;
    private bool resultSucceeded = true;

    // R3：shopOpen / settingsOpen 唯一来源。HUD/Shop/Settings 都从这里读。
    public bool ShopOpen { get; private set; }
    public bool SettingsOpen { get; private set; }

    private readonly int[] upgradeLevels = new int[UpgradeCatalog.All.Length];
    private WindBlower windBlower;

    private UIRouter router;
    private MainMenuUI mainMenu;
    private LevelSelectUI levelSelect;
    private HudUI hud;
    private ShopUI shop;
    private SettingsUI settings;
    private ResultUI result;

    private void Awake()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        // 根 Canvas：所有 UI 共享一个 Canvas + Scaler，减少 draw call。
        GameObject canvasRoot = new GameObject("UICanvas");
        canvasRoot.transform.SetParent(transform, false);
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        router = canvasRoot.AddComponent<UIRouter>();

        mainMenu    = AttachUI<MainMenuUI>(canvasRoot, "MainMenu");
        levelSelect = AttachUI<LevelSelectUI>(canvasRoot, "LevelSelect");
        hud         = AttachUI<HudUI>(canvasRoot, "Hud");
        shop        = AttachUI<ShopUI>(canvasRoot, "Shop");
        settings    = AttachUI<SettingsUI>(canvasRoot, "Settings");
        result      = AttachUI<ResultUI>(canvasRoot, "Result");

        mainMenu.Init(
            () => router.Show(UIRouter.State.LevelSelect),
            () => OpenMenuSettings(),
            () => QuitGame());
        levelSelect.Init(
            size => StartLevel(size),
            ()   => router.Show(UIRouter.State.MainMenu));
        hud.Bind(
            () => ToggleSettings(),
            () => ToggleShop(),
            () => FormatCoins(RiverCollector.CoinCount),
            () => FormatHudTime());
        shop.Bind(
            kind => TryBuyUpgrade(kind),
            () => CloseShop(),
            kind => BuildUpgradeInfo(kind),
            kind => CanBuyUpgrade(kind),
            kind => BuildUpgradePrice(kind),
            () => FormatCoins(RiverCollector.CoinCount));
        settings.Bind(
            () => CloseSettings(),
            () => ReturnToLevelSelect(),
            () => ReturnToMainMenu(),
            () => state == GameState.Playing);
        result.Bind(
            () => StartLevel(selectedLevel),
            () => ReturnToLevelSelect(),
            () => ReturnToMainMenu(),
            () => FormatTime(elapsedTime),
            () => FormatCoins(RiverCollector.SessionCoins),
            () => FormatCoins(RiverCollector.CoinCount),
            () => resultSucceeded);

        router.Register(mainMenu, levelSelect, hud, result);
        router.Show(UIRouter.State.MainMenu);

        // 浮层默认隐藏
        shop.Hide();
        settings.Hide();
    }

    private void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystem = new GameObject("EventSystem");
        Object.DontDestroyOnLoad(eventSystem);
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private T AttachUI<T>(GameObject parent, string name) where T : UIBase
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go.AddComponent<T>();
    }

    private void Update()
    {
        if (state == GameState.Playing)
        {
            if (Input.GetKeyDown(KeyCode.Tab))    ToggleShop();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ShopOpen) ToggleShop();
                else ToggleSettings();
            }

            bool uiPaused = SettingsOpen || ShopOpen;
            Time.timeScale = uiPaused ? 0f : 1f;

            if (!uiPaused)
            {
                float deltaTime = Time.unscaledDeltaTime;
                elapsedTime += deltaTime;
                GameSceneBuilder.Tick(deltaTime);
            }

            if (GameSceneBuilder.IsGameplayClear()) EndGame(true);
            else if (IsTimedChallengeFailed()) EndGame(false);
        }
        else
        {
            Time.timeScale = state == GameState.Result ? 0f : 1f;
        }
    }

    private void OpenMenuSettings()
    {
        if (state != GameState.MainMenu) return;
        SettingsOpen = true;
        settings.Show();
    }

    private void CloseSettings()
    {
        SettingsOpen = false;
        settings.Hide();
    }

    private void CloseShop()
    {
        ShopOpen = false;
        shop.Hide();
    }

    private void QuitGame()
    {
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

    private void ToggleShop()
    {
        if (state != GameState.Playing) return;

        ShopOpen = !ShopOpen;
        if (ShopOpen)
        {
            // 打开商店时自动收起设置，互斥显示
            if (SettingsOpen) { SettingsOpen = false; settings.Hide(); }
            shop.Show();
        }
        else
        {
            shop.Hide();
        }
    }

    private void ToggleSettings()
    {
        if (state != GameState.Playing) return;
        SettingsOpen = !SettingsOpen;
        if (SettingsOpen)
        {
            // 打开设置时自动收起商店，互斥显示
            if (ShopOpen) { ShopOpen = false; shop.Hide(); }
            settings.Show();
        }
        else
        {
            settings.Hide();
        }
    }

    private string BuildUpgradeInfo(UpgradeKind kind)
    {
        int level = upgradeLevels[(int)kind];
        string current = UpgradeCatalog.GetValueText(kind, level);
        string next = UpgradeCatalog.GetNextValueText(kind, level);

        if (UpgradeCatalog.IsMaxLevel(kind, level))
        {
            return $"Lv.{level + 1}\n{current}\n已满级";
        }

        return $"Lv.{level + 1}\n{current} -> {next}";
    }

    private string BuildUpgradePrice(UpgradeKind kind)
    {
        int level = upgradeLevels[(int)kind];
        if (UpgradeCatalog.IsMaxLevel(kind, level)) return "MAX";
        return UpgradeCatalog.GetNextCost(kind, level).ToString();
    }

    private bool CanBuyUpgrade(UpgradeKind kind)
    {
        int level = upgradeLevels[(int)kind];
        return !UpgradeCatalog.IsMaxLevel(kind, level)
            && RiverCollector.CoinCount >= UpgradeCatalog.GetNextCost(kind, level);
    }

    private void StartLevel(GameSceneBuilder.LevelSize levelSize)
    {
        Time.timeScale = 1f;
        selectedLevel = levelSize;
        ShopOpen = false; SettingsOpen = false;
        shop.Hide(); settings.Hide();

        ApplyRuntimeUpgrades();
        RiverCollector.ResetSession();
        GameSceneBuilder.BuildLevel(levelSize);
        timeLimitSeconds = GameSceneBuilder.CurrentTimeLimitSeconds;

        windBlower = Object.FindFirstObjectByType<WindBlower>();
        ApplyRuntimeUpgrades();
        elapsedTime = 0f;
        resultSucceeded = true;

        state = GameState.Playing;
        router.Show(UIRouter.State.Playing);
    }

    private void EndGame(bool success)
    {
        Time.timeScale = 0f;
        ShopOpen = false; SettingsOpen = false;
        shop.Hide(); settings.Hide();
        resultSucceeded = success;

        state = GameState.Result;
        router.Show(UIRouter.State.Result);
    }

    private void ReturnToLevelSelect()
    {
        Time.timeScale = 1f;
        ShopOpen = false; SettingsOpen = false;
        shop.Hide(); settings.Hide();

        GameSceneBuilder.ClearGameplayObjects();
        timeLimitSeconds = 0f;
        resultSucceeded = true;
        state = GameState.LevelSelect;
        router.Show(UIRouter.State.LevelSelect);
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        ShopOpen = false; SettingsOpen = false;
        shop.Hide(); settings.Hide();

        GameSceneBuilder.ClearGameplayObjects();
        timeLimitSeconds = 0f;
        resultSucceeded = true;
        state = GameState.MainMenu;
        router.Show(UIRouter.State.MainMenu);
    }

    private void TryBuyUpgrade(UpgradeKind kind)
    {
        int index = (int)kind;
        int level = upgradeLevels[index];
        if (UpgradeCatalog.IsMaxLevel(kind, level)) return;

        if (!RiverCollector.TrySpendCoins(UpgradeCatalog.GetNextCost(kind, level))) return;

        upgradeLevels[index] = UpgradeCatalog.ClampLevel(kind, level + 1);
        ApplyRuntimeUpgrades();
    }

    private void ApplyRuntimeUpgrades()
    {
        RiverCollector.SetLeafValue(UpgradeCatalog.GetValue(UpgradeKind.LeafValue, upgradeLevels[(int)UpgradeKind.LeafValue]));

        WindBlower b = GetWindBlower();
        if (b == null) return;

        b.ApplyUpgradeValues(
            UpgradeCatalog.GetValue(UpgradeKind.BaseWind, upgradeLevels[(int)UpgradeKind.BaseWind]),
            UpgradeCatalog.GetValue(UpgradeKind.WindRadius, upgradeLevels[(int)UpgradeKind.WindRadius]),
            Mathf.RoundToInt(UpgradeCatalog.GetValue(UpgradeKind.MaxTargets, upgradeLevels[(int)UpgradeKind.MaxTargets]))
        );
    }

    private WindBlower GetWindBlower()
    {
        if (windBlower == null) windBlower = Object.FindFirstObjectByType<WindBlower>();
        return windBlower;
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    private string FormatHudTime()
    {
        if (state == GameState.Playing && timeLimitSeconds > 0f)
        {
            return FormatTime(Mathf.Max(0f, timeLimitSeconds - elapsedTime));
        }

        return FormatTime(elapsedTime);
    }

    private bool IsTimedChallengeFailed()
    {
        return state == GameState.Playing
            && timeLimitSeconds > 0f
            && elapsedTime >= timeLimitSeconds;
    }

    private static string FormatCoins(float coins)
    {
        return UpgradeCatalog.FormatNumber(coins);
    }
}
