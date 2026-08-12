using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 状态机单一入口 + 持有 shop/settings 开关（解决 R3：唯一来源）。
// 自身不再画 UI（R1：所有 UI 走 UIBase / UGUI）。
public class GameFlowManager : MonoBehaviour
{
    public enum GameState { MainMenu, LevelSelect, Playing, Result }

    private GameState state = GameState.MainMenu;
    private LevelId selectedLevel = LevelId.SimpleSmall;

    private float elapsedTime;
    private float timeLimitSeconds;
    private bool resultSucceeded = true;

    // R3：shopOpen / settingsOpen 唯一来源。HUD/Shop/Settings 都从这里读。
    public bool ShopOpen { get; private set; }
    public bool SettingsOpen { get; private set; }

    private readonly int[] upgradeLevels = new int[UpgradeCatalog.All.Length];
    private readonly List<UpgradeKind> inheritanceChoices = new List<UpgradeKind>(3);

    private WindForm currentWindForm = WindForm.Downburst;
    private WindForm pendingWindForm = WindForm.Downburst;
    private UpgradeKind? currentInheritance;
    private float windMomentum;
    private bool choosingInheritance;

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
            kind => HandleShopCard(kind),
            () => TryCloseShop(),
            kind => BuildUpgradeTitle(kind),
            kind => BuildUpgradeInfo(kind),
            kind => CanUseShopCard(kind),
            kind => BuildUpgradePrice(kind),
            kind => BuildUpgradeLevel(kind),
            () => FormatCoins(RiverCollector.CoinCount),
            () => BuildShopHeader(),
            () => BuildShopFormLabel(),
            () => !choosingInheritance);
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
                LevelLoader.Tick(deltaTime);
                TickWindFormUnlock();
            }

            if (LevelLoader.IsGameplayClear()) EndGame(true);
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

    private void TryCloseShop()
    {
        if (choosingInheritance) return;
        CloseShop();
    }

    private void CloseShop()
    {
        if (choosingInheritance) return;

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
        if (state != GameState.Playing || choosingInheritance) return;

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

    private string BuildUpgradeTitle(UpgradeKind kind)
    {
        return choosingInheritance
            ? "继承" + UpgradeCatalog.GetName(kind)
            : UpgradeCatalog.GetName(kind);
    }

    private string BuildUpgradeInfo(UpgradeKind kind)
    {
        if (choosingInheritance)
        {
            return BuildInheritanceInfo(kind);
        }

        int level = upgradeLevels[(int)kind];
        string current = UpgradeCatalog.GetValueText(currentWindForm, kind, level);

        if (UpgradeCatalog.IsMaxLevel(kind, level))
        {
            return "当前：" + current + "\n已满级";
        }

        string nextName = UpgradeCatalog.GetNextStepName(currentWindForm, kind, level);
        string next = UpgradeCatalog.GetNextValueText(currentWindForm, kind, level);
        return "当前：" + current + "\n下级：" + nextName + " " + next;
    }

    private string BuildInheritanceInfo(UpgradeKind kind)
    {
        switch (kind)
        {
            case UpgradeKind.WindPower:
                return "新形态基础风力 +15%";
            case UpgradeKind.WindArea:
                return "新形态基础尺寸 +12%";
            case UpgradeKind.WindPulse:
                return "新形态基础风载 +20%";
            default:
                return "";
        }
    }

    private string BuildUpgradePrice(UpgradeKind kind)
    {
        if (choosingInheritance) return "选择";

        int level = upgradeLevels[(int)kind];
        if (UpgradeCatalog.IsMaxLevel(kind, level)) return "MAX";

        return UpgradeCatalog.GetNextCost(currentWindForm, kind, level).ToString();
    }

    private string BuildUpgradeLevel(UpgradeKind kind)
    {
        if (choosingInheritance)
        {
            return inheritanceChoices.Contains(kind) ? "可继承" : "不可选";
        }

        int level = upgradeLevels[(int)kind];
        return level + "/2";
    }

    private bool CanUseShopCard(UpgradeKind kind)
    {
        if (choosingInheritance)
        {
            return inheritanceChoices.Contains(kind);
        }

        int level = upgradeLevels[(int)kind];
        return !UpgradeCatalog.IsMaxLevel(kind, level)
            && RiverCollector.CoinCount >= UpgradeCatalog.GetNextCost(currentWindForm, kind, level);
    }

    private string BuildShopHeader()
    {
        return choosingInheritance
            ? "选择继承强化 → " + UpgradeCatalog.GetFormName(pendingWindForm)
            : UpgradeCatalog.GetWindName(currentWindForm, upgradeLevels);
    }

    private string BuildShopFormLabel()
    {
        return choosingInheritance
            ? "形态升级 " + UpgradeCatalog.GetFormName(pendingWindForm)
            : UpgradeCatalog.GetFormName(currentWindForm);
    }

    private void StartLevel(LevelId levelId)
    {
        Time.timeScale = 1f;
        selectedLevel = levelId;
        ShopOpen = false; SettingsOpen = false;
        shop.Hide(); settings.Hide();

        RiverCollector.ResetSession();
        ResetWindRunState();
        SetWindBlower(null);

        LevelRoot loadedLevel = LevelLoader.Load(levelId);
        if (loadedLevel == null)
        {
            timeLimitSeconds = 0f;
            state = GameState.LevelSelect;
            router.Show(UIRouter.State.LevelSelect);
            return;
        }

        timeLimitSeconds = loadedLevel.TimeLimitSeconds;
        SetWindBlower(loadedLevel.WindBlower);
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

        LevelLoader.Unload();
        windBlower = null;
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

        LevelLoader.Unload();
        windBlower = null;
        timeLimitSeconds = 0f;
        resultSucceeded = true;
        state = GameState.MainMenu;
        router.Show(UIRouter.State.MainMenu);
    }

    private void HandleShopCard(UpgradeKind kind)
    {
        if (choosingInheritance)
        {
            ConfirmInheritance(kind);
            return;
        }

        TryBuyUpgrade(kind);
    }

    private void TryBuyUpgrade(UpgradeKind kind)
    {
        int index = (int)kind;
        int level = upgradeLevels[index];
        if (UpgradeCatalog.IsMaxLevel(kind, level)) return;

        int cost = UpgradeCatalog.GetNextCost(currentWindForm, kind, level);
        if (!RiverCollector.TrySpendCoins(cost)) return;

        upgradeLevels[index] = UpgradeCatalog.ClampLevel(kind, level + 1);
        ApplyRuntimeUpgrades();
    }

    private void ApplyRuntimeUpgrades()
    {
        WindBlower b = GetWindBlower();
        if (b == null) return;

        WindRuntimeValues values = UpgradeCatalog.GetRuntimeValues(
            currentWindForm,
            upgradeLevels,
            currentInheritance);

        b.ApplyUpgradeValues(values);
    }

    private WindBlower GetWindBlower()
    {
        WindBlower current = LevelLoader.CurrentWindBlower;
        if (windBlower != current)
        {
            SetWindBlower(current);
        }

        return windBlower;
    }

    private void SetWindBlower(WindBlower blower)
    {
        if (windBlower != null)
        {
            windBlower.OnTargetsPushed -= HandleWindTargetsPushed;
        }

        windBlower = blower;

        if (windBlower != null)
        {
            windBlower.OnTargetsPushed += HandleWindTargetsPushed;
        }
    }

    private void HandleWindTargetsPushed(WindBlowResult result)
    {
        windMomentum += Mathf.Max(0, result.PushedCount)
            * Mathf.Max(0f, result.Power)
            * Mathf.Clamp(result.Interval, 0.08f, 0.5f)
            * 10f;
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

    private void ResetWindRunState()
    {
        currentWindForm = WindForm.Downburst;
        pendingWindForm = WindForm.Downburst;
        currentInheritance = null;
        windMomentum = 0f;
        choosingInheritance = false;
        inheritanceChoices.Clear();
        System.Array.Clear(upgradeLevels, 0, upgradeLevels.Length);
    }

    private void TickWindFormUnlock()
    {
        if (choosingInheritance) return;

        if (!UpgradeCatalog.TryGetNextForm(currentWindForm, out WindForm nextForm))
        {
            return;
        }

        bool reachedMomentum = windMomentum >= UpgradeCatalog.GetWindMomentumTarget(nextForm);
        bool reachedFallback = elapsedTime >= UpgradeCatalog.GetFallbackUnlockSeconds(nextForm);

        if (!reachedMomentum && !reachedFallback)
        {
            return;
        }

        BeginWindFormAdvance(nextForm);
    }

    private void BeginWindFormAdvance(WindForm nextForm)
    {
        pendingWindForm = nextForm;
        BuildInheritanceChoices();

        if (inheritanceChoices.Count == 1)
        {
            ApplyWindFormAdvance(nextForm, inheritanceChoices[0]);
            return;
        }

        choosingInheritance = true;
        ShopOpen = true;

        if (SettingsOpen)
        {
            SettingsOpen = false;
            settings.Hide();
        }

        shop.Show();
    }

    private void BuildInheritanceChoices()
    {
        inheritanceChoices.Clear();

        int highest = -1;
        for (int i = 0; i < UpgradeCatalog.All.Length; i++)
        {
            UpgradeKind kind = UpgradeCatalog.All[i];
            int level = upgradeLevels[(int)kind];

            if (level > highest)
            {
                highest = level;
                inheritanceChoices.Clear();
                inheritanceChoices.Add(kind);
            }
            else if (level == highest)
            {
                inheritanceChoices.Add(kind);
            }
        }
    }

    private void ConfirmInheritance(UpgradeKind kind)
    {
        if (!inheritanceChoices.Contains(kind)) return;

        ApplyWindFormAdvance(pendingWindForm, kind);
    }

    private void ApplyWindFormAdvance(WindForm nextForm, UpgradeKind inheritance)
    {
        currentWindForm = nextForm;
        currentInheritance = inheritance;
        windMomentum = 0f;
        choosingInheritance = false;
        inheritanceChoices.Clear();
        System.Array.Clear(upgradeLevels, 0, upgradeLevels.Length);

        ShopOpen = false;
        shop.Hide();

        ApplyRuntimeUpgrades();
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
