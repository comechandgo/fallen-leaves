using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Full-screen level intro overlay. The prompt writes only to stencil; the black
// image renders everywhere except the written glyphs, revealing gameplay below.
public sealed class LevelIntroUI : UIBase
{
    public const float InitialPromptScale = 1f;
    public const float FinalPromptScale = 24f;
    public const float BlackFadeStart = 0.85f;

    private const int StencilReference = 1;

    private Text promptLabel;
    private Image blackout;
    private Material stencilWriterMaterial;
    private Material inverseStencilMaterial;

    public string PromptText => promptLabel != null ? promptLabel.text : string.Empty;
    public float PromptScale => promptLabel != null ? promptLabel.rectTransform.localScale.x : InitialPromptScale;
    public float BlackAlpha => blackout != null ? blackout.color.a : 0f;
    public bool PromptVisible => promptLabel != null && promptLabel.enabled;

    protected override void Build()
    {
        CreateMaterials();

        promptLabel = CreateText(transform, string.Empty, 120, TextAnchor.MiddleCenter, Color.white);
        promptLabel.name = "StencilPrompt";
        promptLabel.raycastTarget = false;
        promptLabel.lineSpacing = 1f;
        promptLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        promptLabel.verticalOverflow = VerticalWrapMode.Overflow;
        promptLabel.material = stencilWriterMaterial;
        promptLabel.enabled = false;

        ResetPromptTransform();

        blackout = CreateImageStretch(transform, "Blackout", string.Empty);
        blackout.sprite = null;
        blackout.color = new Color(0f, 0f, 0f, 0f);
        blackout.raycastTarget = true;
        blackout.material = inverseStencilMaterial;
    }

    public void Begin(string prompt)
    {
        base.Show();

        promptLabel.text = prompt ?? string.Empty;
        promptLabel.enabled = false;
        ResetPromptTransform();
        promptLabel.SetAllDirty();
        SetBlackAlpha(0f);
    }

    public void SetFadeToBlackProgress(float progress)
    {
        promptLabel.enabled = false;
        promptLabel.rectTransform.localScale = Vector3.one * InitialPromptScale;
        SetBlackAlpha(Smooth01(progress));
    }

    public void ShowPrompt()
    {
        promptLabel.enabled = true;
        ResetPromptTransform();
        promptLabel.SetAllDirty();
        SetBlackAlpha(1f);
    }

    public void SetRevealProgress(float progress)
    {
        float clamped = Mathf.Clamp01(progress);
        float accelerated = clamped * clamped * clamped;
        float scale = Mathf.LerpUnclamped(InitialPromptScale, FinalPromptScale, accelerated);

        promptLabel.enabled = true;
        promptLabel.rectTransform.localScale = Vector3.one * scale;

        float blackAlpha = 1f;
        if (clamped > BlackFadeStart)
        {
            float fadeProgress = (clamped - BlackFadeStart) / (1f - BlackFadeStart);
            blackAlpha = 1f - Smooth01(fadeProgress);
        }

        SetBlackAlpha(blackAlpha);
    }

    public void Complete()
    {
        if (promptLabel != null)
        {
            promptLabel.enabled = false;
            ResetPromptTransform();
        }

        SetBlackAlpha(0f);
        Hide();
    }

    private void CreateMaterials()
    {
        Shader shader = Shader.Find("UI/Default");
        if (shader == null)
        {
            Debug.LogError("LevelIntroUI requires the built-in UI/Default shader.");
            return;
        }

        stencilWriterMaterial = new Material(shader)
        {
            name = "LevelIntro_StencilWriter",
            hideFlags = HideFlags.HideAndDontSave
        };
        ConfigureStencil(stencilWriterMaterial, CompareFunction.Always, StencilOp.Replace, 0, true);

        inverseStencilMaterial = new Material(shader)
        {
            name = "LevelIntro_InverseStencil",
            hideFlags = HideFlags.HideAndDontSave
        };
        ConfigureStencil(inverseStencilMaterial, CompareFunction.NotEqual, StencilOp.Keep,
            (int)ColorWriteMask.All, false);
    }

    private static void ConfigureStencil(
        Material material,
        CompareFunction comparison,
        StencilOp operation,
        int colorMask,
        bool alphaClip)
    {
        material.SetInt("_Stencil", StencilReference);
        material.SetInt("_StencilComp", (int)comparison);
        material.SetInt("_StencilOp", (int)operation);
        material.SetInt("_StencilReadMask", 255);
        material.SetInt("_StencilWriteMask", 255);
        material.SetInt("_ColorMask", colorMask);
        material.SetInt("_UseUIAlphaClip", alphaClip ? 1 : 0);
        if (alphaClip) material.EnableKeyword("UNITY_UI_ALPHACLIP");
        else material.DisableKeyword("UNITY_UI_ALPHACLIP");
    }

    private void SetBlackAlpha(float alpha)
    {
        if (blackout == null) return;

        Color color = blackout.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = Mathf.Clamp01(alpha);
        blackout.color = color;
    }

    private void ResetPromptTransform()
    {
        if (promptLabel == null) return;

        RectTransform promptRect = promptLabel.rectTransform;
        Stretch(promptRect);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.offsetMin = new Vector2(160f, 80f);
        promptRect.offsetMax = new Vector2(-160f, -80f);
        promptRect.localScale = Vector3.one * InitialPromptScale;
        promptRect.localRotation = Quaternion.identity;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void OnDestroy()
    {
        if (stencilWriterMaterial != null) Destroy(stencilWriterMaterial);
        if (inverseStencilMaterial != null) Destroy(inverseStencilMaterial);
    }
}
