using UnityEngine;

public class UIRouter : MonoBehaviour
{
    public enum State { None, MainMenu, LevelSelect, Playing, Result }

    public State Current { get; private set; } = State.None;

    private MainMenuUI mainMenu;
    private LevelSelectUI levelSelect;
    private HudUI hud;
    private ResultUI result;

    public void Register(MainMenuUI a, LevelSelectUI b, HudUI c, ResultUI d)
    {
        mainMenu = a; levelSelect = b; hud = c; result = d;
    }

    public void Show(State target)
    {
        Switch(mainMenu,    target == State.MainMenu);
        Switch(levelSelect, target == State.LevelSelect);
        Switch(hud,         target == State.Playing);
        Switch(result,      target == State.Result);
        Current = target;
    }

    private static void Switch(UIBase panel, bool show)
    {
        if (panel == null) return;
        if (show) panel.Show(); else panel.Hide();
    }
}
