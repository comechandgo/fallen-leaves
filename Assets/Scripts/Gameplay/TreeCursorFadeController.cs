using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class TreeCursorFadeController : MonoBehaviour
{
    private static readonly int CursorScreenPositionId = Shader.PropertyToID("_TreeCursorScreenPosition");
    private static readonly int FadeEnabledId = Shader.PropertyToID("_TreeCursorFadeEnabled");

    private bool hasFocus = true;

    private void OnEnable()
    {
        hasFocus = Application.isFocused;
        UpdateShaderState();
    }

    private void LateUpdate()
    {
        UpdateShaderState();
    }

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
        if (!focused) DisableFade();
    }

    private void OnDisable()
    {
        DisableFade();
    }

    private void UpdateShaderState()
    {
        Vector3 mousePosition = Input.mousePosition;
        bool insideGameView = mousePosition.x >= 0f
            && mousePosition.y >= 0f
            && mousePosition.x < Screen.width
            && mousePosition.y < Screen.height;
        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool enabled = hasFocus && Application.isFocused && insideGameView && !overUi;

        Shader.SetGlobalVector(CursorScreenPositionId, new Vector4(mousePosition.x, mousePosition.y, 0f, 0f));
        Shader.SetGlobalFloat(FadeEnabledId, enabled ? 1f : 0f);
    }

    private static void DisableFade()
    {
        Shader.SetGlobalFloat(FadeEnabledId, 0f);
    }
}
