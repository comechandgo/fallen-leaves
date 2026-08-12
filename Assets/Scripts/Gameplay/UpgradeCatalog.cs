public enum UpgradeKind
{
    LeafValue,
    BaseWind,
    WindRadius,
    MaxTargets
}

public static class UpgradeCatalog
{
    public static readonly UpgradeKind[] All =
    {
        UpgradeKind.LeafValue,
        UpgradeKind.BaseWind,
        UpgradeKind.WindRadius,
        UpgradeKind.MaxTargets
    };

    private static readonly float[] LeafValues = { 1f, 1.5f, 2f, 2.5f, 3f };
    private static readonly float[] BaseWinds = { 1f, 2f, 3f, 4f, 5f };
    private static readonly float[] WindRadii = { 1f, 1.5f, 2f, 2.5f, 3f };
    private static readonly float[] MaxTargets = { 10f, 20f, 30f };

    // 策划案未给价格表，先集中放这里方便调数。
    private static readonly int[] LeafValueCosts = { 6, 10, 14, 18 };
    private static readonly int[] BaseWindCosts = { 8, 14, 20, 28 };
    private static readonly int[] WindRadiusCosts = { 8, 14, 22, 30 };
    private static readonly int[] MaxTargetCosts = { 10, 18 };

    public static string GetName(UpgradeKind kind)
    {
        switch (kind)
        {
            case UpgradeKind.LeafValue: return "单叶价值";
            case UpgradeKind.BaseWind: return "基础风力";
            case UpgradeKind.WindRadius: return "风力范围";
            case UpgradeKind.MaxTargets: return "单次数量";
            default: return "强化";
        }
    }

    public static int GetMaxLevel(UpgradeKind kind)
    {
        return GetValues(kind).Length;
    }

    public static bool IsMaxLevel(UpgradeKind kind, int levelIndex)
    {
        return ClampLevel(kind, levelIndex) >= GetMaxLevel(kind) - 1;
    }

    public static int ClampLevel(UpgradeKind kind, int levelIndex)
    {
        int maxIndex = GetMaxLevel(kind) - 1;
        if (levelIndex < 0) return 0;
        return levelIndex > maxIndex ? maxIndex : levelIndex;
    }

    public static float GetValue(UpgradeKind kind, int levelIndex)
    {
        float[] values = GetValues(kind);
        return values[ClampLevel(kind, levelIndex)];
    }

    public static int GetNextCost(UpgradeKind kind, int levelIndex)
    {
        if (IsMaxLevel(kind, levelIndex)) return 0;
        int[] costs = GetCosts(kind);
        return costs[ClampLevel(kind, levelIndex)];
    }

    public static string GetValueText(UpgradeKind kind, int levelIndex)
    {
        float value = GetValue(kind, levelIndex);
        switch (kind)
        {
            case UpgradeKind.LeafValue: return FormatNumber(value);
            case UpgradeKind.BaseWind: return FormatNumber(value);
            case UpgradeKind.WindRadius: return FormatNumber(value) + "m";
            case UpgradeKind.MaxTargets: return ((int)value).ToString();
            default: return FormatNumber(value);
        }
    }

    public static string GetNextValueText(UpgradeKind kind, int levelIndex)
    {
        if (IsMaxLevel(kind, levelIndex)) return "MAX";
        return GetValueText(kind, levelIndex + 1);
    }

    public static string FormatNumber(float value)
    {
        return value % 1f == 0f ? ((int)value).ToString() : value.ToString("0.0");
    }

    private static float[] GetValues(UpgradeKind kind)
    {
        switch (kind)
        {
            case UpgradeKind.LeafValue: return LeafValues;
            case UpgradeKind.BaseWind: return BaseWinds;
            case UpgradeKind.WindRadius: return WindRadii;
            case UpgradeKind.MaxTargets: return MaxTargets;
            default: return BaseWinds;
        }
    }

    private static int[] GetCosts(UpgradeKind kind)
    {
        switch (kind)
        {
            case UpgradeKind.LeafValue: return LeafValueCosts;
            case UpgradeKind.BaseWind: return BaseWindCosts;
            case UpgradeKind.WindRadius: return WindRadiusCosts;
            case UpgradeKind.MaxTargets: return MaxTargetCosts;
            default: return BaseWindCosts;
        }
    }
}
