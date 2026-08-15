using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 状态机单一入口 + 持有 shop/settings 开关（解决 R3：唯一来源）。
// 自身不再画 UI（R1：所有 UI 走 UIBase / UGUI）。
public class GameFlowManager : MonoBehaviour
{
    public enum GameState { MainMenu, LevelSelect, LevelIntro, Playing, EndlessFailure, Result }

    private enum LevelIntroPhase { None, FadeToBlack, PromptHold, Reveal }

    private const float EndlessFailureDelay = 1f;
    private const float LevelIntroFadeDuration = 0.35f;
    private const float LevelIntroHoldDuration = 0.60f;
    private const float LevelIntroRevealDuration = 1.20f;

    private GameState state = GameState.MainMenu;
    private LevelId selectedLevel = LevelId.SimpleSmall;

    private float elapsedTime;
    private float timeLimitSeconds;
    private float endlessFailureElapsed;
    private float levelIntroPhaseElapsed;
    private bool resultSucceeded = true;
    private LevelIntroPhase levelIntroPhase;

    // R3：shopOpen / settingsOpen 唯一来源。HUD/Shop/Settings 都从这里读。
    public bool ShopOpen { get; private set; }
    public bool SettingsOpen { get; private set; }

    private readonly int[] upgradeLevels = new int[UpgradeCatalog.All.Length];

    private WindForm currentWindForm = WindForm.Downburst;
    private UpgradeInheritance inheritedUpgrades = UpgradeInheritance.None;

    private WindBlower windBlower;

    private UIRouter router;
    private MainMenuUI mainMenu;
    private LevelSelectUI levelSelect;
    private HudUI hud;
    private ShopUI shop;
    private SettingsUI settings;
    private ResultUI result;
    private LevelIntroUI levelIntro;

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
        levelIntro  = AttachUI<LevelIntroUI>(canvasRoot, "LevelIntro");

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
            () => FormatHudTime(),
            () => selectedLevel == LevelId.TimedChallenge,
            () => selectedLevel == LevelId.Endless,
            () => LevelLoader.Current != null ? LevelLoader.Current.EndlessSurvivalRatio : 0f,
            () => state == GameState.EndlessFailure);
        shop.Bind(
            kind => TryBuyUpgrade(kind),
            () => TryBuyNextWindForm(),
            () => CloseShop(),
            kind => BuildUpgradeTitle(kind),
            kind => BuildUpgradeInfo(kind),
            kind => CanUseShopCard(kind),
            kind => BuildUpgradePrice(kind),
            kind => BuildUpgradeLevel(kind),
            () => FormatCoins(RiverCollector.CoinCount),
            () => BuildShopHeader(),
            () => BuildShopFormLabel(),
            () => BuildFormPurchaseLabel(),
            () => CanBuyNextWindForm());
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
            () => RiverCollector.SessionLeafCount.ToString(),
            () => selectedLevel == LevelId.TimedChallenge,
            () => selectedLevel == LevelId.Endless);

        router.Register(mainMenu, levelSelect, hud, result);
        router.Show(UIRouter.State.MainMenu);

        // 浮层默认隐藏
        shop.Hide();
        settings.Hide();
        levelIntro.Hide();
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
        if (state == GameState.LevelIntro)
        {
            Time.timeScale = 0f;
            LevelLoader.Tick(0f);
            AdvanceLevelIntro(Time.unscaledDeltaTime);
        }
        else if (state == GameState.Playing)
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
                LevelLoader.Tick(deltaTime);
                if (LevelLoader.IsReady) elapsedTime += deltaTime;
            }

            if (LevelLoader.IsReady)
            {
                if (LevelLoader.IsGameplayClear()) EndGame(true);
                else if (IsTimedChallengeFailed()) EndGame(false);
                else if (IsEndlessChallengeFailed()) BeginEndlessFailure();
            }
        }
        else if (state == GameState.EndlessFailure)
        {
            Time.timeScale = 0f;
            AdvanceEndlessFailure(Time.unscaledDeltaTime);
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

    private string BuildUpgradeTitle(UpgradeKind kind)
    {
        return UpgradeCatalog.GetName(kind);
    }

    private string BuildUpgradeInfo(UpgradeKind kind)
    {
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

    private string BuildUpgradePrice(UpgradeKind kind)
    {
        int level = upgradeLevels[(int)kind];
        if (UpgradeCatalog.IsMaxLevel(kind, level)) return "MAX";

        return UpgradeCatalog.GetNextCost(currentWindForm, kind, level).ToString();
    }

    private string BuildUpgradeLevel(UpgradeKind kind)
    {
        int level = upgradeLevels[(int)kind];
        return level + "/2";
    }

    private bool CanUseShopCard(UpgradeKind kind)
    {
        int level = upgradeLevels[(int)kind];
        return !UpgradeCatalog.IsMaxLevel(kind, level)
            && RiverCollector.CoinCount >= UpgradeCatalog.GetNextCost(currentWindForm, kind, level);
    }

    private string BuildShopHeader()
    {
        return UpgradeCatalog.GetWindName(currentWindForm, upgradeLevels);
    }

    private string BuildShopFormLabel()
    {
        return UpgradeCatalog.GetFormName(currentWindForm);
    }

    private string BuildFormPurchaseLabel()
    {
        if (!UpgradeCatalog.TryGetNextForm(currentWindForm, out WindForm nextForm))
        {
            return "已达最终形态";
        }

        return "升级为" + UpgradeCatalog.GetFormName(nextForm) + " "
            + UpgradeCatalog.GetFormCost(nextForm);
    }

    private bool CanBuyNextWindForm()
    {
        if (state != GameState.Playing) return false;
        return UpgradeCatalog.TryGetNextForm(currentWindForm, out WindForm nextForm)
            && RiverCollector.CoinCount >= UpgradeCatalog.GetFormCost(nextForm);
    }

    private void StartLevel(LevelId levelId)
    {
        if (state == GameState.LevelIntro) return;

        Time.timeScale = 0f;
        selectedLevel = levelId;
        ShopOpen = false; SettingsOpen = false;
        shop.Hide(); settings.Hide();

        state = GameState.LevelIntro;
        levelIntroPhase = LevelIntroPhase.FadeToBlack;
        levelIntroPhaseElapsed = 0f;
        levelIntro.Begin(GetLevelIntroPrompt(levelId));

        RiverCollector.ResetRun();
        ResetWindRunState();
        SetWindBlower(null);

        LevelRoot loadedLevel = LevelLoader.Load(levelId);
        if (loadedLevel == null)
        {
            levelIntro.Complete();
            levelIntroPhase = LevelIntroPhase.None;
            levelIntroPhaseElapsed = 0f;
            timeLimitSeconds = 0f;
            state = GameState.LevelSelect;
            router.Show(UIRouter.State.LevelSelect);
            Time.timeScale = 1f;
            return;
        }

        timeLimitSeconds = loadedLevel.TimeLimitSeconds;
        SetWindBlower(loadedLevel.WindBlower);
        if (windBlower != null) windBlower.enabled = false;
        ApplyRuntimeUpgrades();
        elapsedTime = 0f;
        endlessFailureElapsed = 0f;
        resultSucceeded = true;
    }

    private void AdvanceLevelIntro(float unscaledDeltaTime)
    {
        if (state != GameState.LevelIntro || levelIntroPhase == LevelIntroPhase.None) return;

        WindBlower introWind = GetWindBlower();
        if (introWind != null) introWind.enabled = false;

        float deltaTime = Mathf.Max(0f, unscaledDeltaTime);
        levelIntroPhaseElapsed += deltaTime;

        if (levelIntroPhase == LevelIntroPhase.FadeToBlack)
        {
            float progress = levelIntroPhaseElapsed / LevelIntroFadeDuration;
            levelIntro.SetFadeToBlackProgress(progress);
            if (progress < 1f) return;

            levelIntroPhase = LevelIntroPhase.PromptHold;
            levelIntroPhaseElapsed = 0f;
            router.Show(UIRouter.State.Playing);
            levelIntro.ShowPrompt();
            return;
        }

        if (levelIntroPhase == LevelIntroPhase.PromptHold)
        {
            if (levelIntroPhaseElapsed < LevelIntroHoldDuration || !LevelLoader.IsReady) return;

            levelIntroPhase = LevelIntroPhase.Reveal;
            levelIntroPhaseElapsed = 0f;
            levelIntro.SetRevealProgress(0f);
            return;
        }

        float revealProgress = levelIntroPhaseElapsed / LevelIntroRevealDuration;
        levelIntro.SetRevealProgress(revealProgress);
        if (revealProgress < 1f || !LevelLoader.IsReady) return;

        levelIntro.Complete();
        levelIntroPhase = LevelIntroPhase.None;
        levelIntroPhaseElapsed = 0f;
        state = GameState.Playing;

        WindBlower current = GetWindBlower();
        if (current != null) current.enabled = true;
        Time.timeScale = 1f;
    }

    private static string GetLevelIntroPrompt(LevelId levelId)
    {
        switch (levelId)
        {
            case LevelId.SimpleSmall:
                return "把落叶吹进河里";
            case LevelId.TimedChallenge:
                return "三分限时\n把更多的叶子吹入河里吧";
            case LevelId.Endless:
                return "不要让落叶条掉空";
            default:
                return string.Empty;
        }
    }

    private void EndGame(bool success)
    {
        Time.timeScale = 0f;
        levelIntro.Complete();
        levelIntroPhase = LevelIntroPhase.None;
        levelIntroPhaseElapsed = 0f;
        ShopOpen = false; SettingsOpen = false;
        shop.Hide(); settings.Hide();
        resultSucceeded = success;

        state = GameState.Result;
        router.Show(UIRouter.State.Result);
    }

    private void BeginEndlessFailure()
    {
        if (state != GameState.Playing || selectedLevel != LevelId.Endless) return;

        Time.timeScale = 0f;
        ShopOpen = false;
        SettingsOpen = false;
        shop.Hide();
        settings.Hide();
        endlessFailureElapsed = 0f;
        resultSucceeded = false;

        WindBlower current = GetWindBlower();
        if (current != null) current.enabled = false;

        state = GameState.EndlessFailure;
    }

    private void AdvanceEndlessFailure(float unscaledDeltaTime)
    {
        if (state != GameState.EndlessFailure) return;

        endlessFailureElapsed += Mathf.Max(0f, unscaledDeltaTime);
        if (endlessFailureElapsed >= EndlessFailureDelay) EndGame(false);
    }

    private void ReturnToLevelSelect()
    {
        Time.timeScale = 1f;
        levelIntro.Complete();
        levelIntroPhase = LevelIntroPhase.None;
        levelIntroPhaseElapsed = 0f;
        ShopOpen = false; SettingsOpen = false;
        shop.Hide(); settings.Hide();

        LevelLoader.Unload();
        windBlower = null;
        timeLimitSeconds = 0f;
        endlessFailureElapsed = 0f;
        resultSucceeded = true;
        state = GameState.LevelSelect;
        router.Show(UIRouter.State.LevelSelect);
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        levelIntro.Complete();
        levelIntroPhase = LevelIntroPhase.None;
        levelIntroPhaseElapsed = 0f;
        ShopOpen = false; SettingsOpen = false;
        shop.Hide(); settings.Hide();

        LevelLoader.Unload();
        windBlower = null;
        timeLimitSeconds = 0f;
        endlessFailureElapsed = 0f;
        resultSucceeded = true;
        state = GameState.MainMenu;
        router.Show(UIRouter.State.MainMenu);
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
            inheritedUpgrades);

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
        windBlower = blower;
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    private string FormatHudTime()
    {
        if ((state == GameState.LevelIntro || state == GameState.Playing) && timeLimitSeconds > 0f)
        {
            return FormatTime(Mathf.Max(0f, timeLimitSeconds - elapsedTime));
        }

        return FormatTime(elapsedTime);
    }

    private void ResetWindRunState()
    {
        currentWindForm = WindForm.Downburst;
        inheritedUpgrades = UpgradeInheritance.None;
        System.Array.Clear(upgradeLevels, 0, upgradeLevels.Length);
    }

    private void TryBuyNextWindForm()
    {
        if (state != GameState.Playing) return;

        if (!UpgradeCatalog.TryGetNextForm(currentWindForm, out WindForm nextForm))
        {
            return;
        }

        int cost = UpgradeCatalog.GetFormCost(nextForm);
        if (!RiverCollector.TrySpendCoins(cost)) return;

        ApplyWindFormAdvance(nextForm);
    }

    private void ApplyWindFormAdvance(WindForm nextForm)
    {
        for (int i = 0; i < UpgradeCatalog.All.Length; i++)
        {
            UpgradeKind kind = UpgradeCatalog.All[i];
            if (upgradeLevels[(int)kind] > 0)
            {
                inheritedUpgrades |= GetInheritanceFlag(kind);
            }
        }

        currentWindForm = nextForm;
        System.Array.Clear(upgradeLevels, 0, upgradeLevels.Length);

        ApplyRuntimeUpgrades();
    }

    private static UpgradeInheritance GetInheritanceFlag(UpgradeKind kind)
    {
        return (UpgradeInheritance)(1 << (int)kind);
    }

    private bool IsTimedChallengeFailed()
    {
        return state == GameState.Playing
            && timeLimitSeconds > 0f
            && elapsedTime >= timeLimitSeconds;
    }

    private bool IsEndlessChallengeFailed()
    {
        return state == GameState.Playing
            && selectedLevel == LevelId.Endless
            && LevelLoader.Current != null
            && LevelLoader.Current.IsEndlessSurvivalDepleted;
    }

    private static string FormatCoins(float coins)
    {
        return UpgradeCatalog.FormatNumber(coins);
    }
}
