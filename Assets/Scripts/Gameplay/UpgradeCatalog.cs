using UnityEngine;

public enum UpgradeKind
{
    WindPower,
    WindArea,
    WindPulse
}

[System.Flags]
public enum UpgradeInheritance
{
    None = 0,
    WindPower = 1 << 0,
    WindArea = 1 << 1,
    WindPulse = 1 << 2
}

public static class UpgradeCatalog
{
    public static readonly UpgradeKind[] All =
    {
        UpgradeKind.WindPower,
        UpgradeKind.WindArea,
        UpgradeKind.WindPulse
    };

    private static readonly float[][] PowerLevels =
    {
        new[] { 1f, 1.3f, 1.8f },
        new[] { 2.2f, 3f, 4.2f },
        new[] { 5f, 7f, 10f }
    };

    private static readonly float[][] RadiusLevels =
    {
        new[] { 6f, 7.5f, 9f },
        new[] { 0f, 0f, 0f },
        new[] { 12f, 15f, 19f }
    };

    private static readonly float[] SurfaceLengthLevels = { 18f, 21f, 25f };
    private static readonly float[] SurfaceStartWidthLevels = { 6f, 8f, 10f };
    private static readonly float[] SurfaceEndWidthLevels = { 10f, 13f, 16f };

    private static readonly int[][] MaxTargetLevels =
    {
        new[] { 10, 15, 22 },
        new[] { 24, 36, 52 },
        new[] { 100, 150, 220 }
    };

    private static readonly float[][] IntervalLevels =
    {
        new[] { 0.50f, 0.45f, 0.40f },
        new[] { 0.40f, 0.34f, 0.28f },
        new[] { 0.18f, 0.15f, 0.12f }
    };

    private static readonly int[][] CostsByForm =
    {
        new[] { 5, 15 },
        new[] { 10, 30 },
        new[] { 20, 60 }
    };

    // 以“目标形态”为索引；初始下沉风不需要购买。
    private static readonly int[] FormCosts = { 0, 50, 300 };

    private static readonly string[][][] StepNames =
    {
        new[]
        {
            new[] { "凝压", "落地爆发" },
            new[] { "扩散", "外圈补压" },
            new[] { "通脉", "复吹" }
        },
        new[]
        {
            new[] { "风压", "贯流" },
            new[] { "展幅", "满幅" },
            new[] { "连流", "不息面风" }
        },
        new[]
        {
            new[] { "强吸", "撕扯" },
            new[] { "扩环", "天幕" },
            new[] { "载流", "不息涡流" }
        }
    };

    private static readonly string[] ScaleWords = { "迷你", "巨型", "超级" };
    private static readonly string[] PowerWords = { "弱小", "强悍", "无敌" };
    private static readonly string[] PulseWords = { "清风", "狂风", "雷霆" };

    private const float SurfaceLiftRatio = 0.32f;
    private const float TornadoInwardRatio = 0.55f;
    private const float TornadoSpinRatio = 1.00f;

    public static string GetName(UpgradeKind kind)
    {
        switch (kind)
        {
            case UpgradeKind.WindPower: return "风力";
            case UpgradeKind.WindArea: return "风域";
            case UpgradeKind.WindPulse: return "风脉";
            default: return "升级";
        }
    }

    public static string GetFormName(WindForm form)
    {
        switch (form)
        {
            case WindForm.Downburst: return "下沉风";
            case WindForm.Surface: return "面风";
            case WindForm.Tornado: return "龙卷风";
            default: return "风";
        }
    }

    public static bool TryGetNextForm(WindForm current, out WindForm next)
    {
        switch (current)
        {
            case WindForm.Downburst:
                next = WindForm.Surface;
                return true;
            case WindForm.Surface:
                next = WindForm.Tornado;
                return true;
            default:
                next = current;
                return false;
        }
    }

    public static int GetFormCost(WindForm targetForm)
    {
        int index = (int)targetForm;
        return index >= 0 && index < FormCosts.Length ? FormCosts[index] : 0;
    }

    public static int GetMaxLevel(UpgradeKind kind)
    {
        return 3;
    }

    public static bool IsMaxLevel(UpgradeKind kind, int levelIndex)
    {
        return ClampLevel(kind, levelIndex) >= GetMaxLevel(kind) - 1;
    }

    public static int ClampLevel(UpgradeKind kind, int levelIndex)
    {
        if (levelIndex < 0) return 0;
        return levelIndex > 2 ? 2 : levelIndex;
    }

    public static int GetLevel(int[] levels, UpgradeKind kind)
    {
        int index = (int)kind;
        if (levels == null || index < 0 || index >= levels.Length) return 0;
        return ClampLevel(kind, levels[index]);
    }

    public static int GetNextCost(WindForm form, UpgradeKind kind, int levelIndex)
    {
        if (IsMaxLevel(kind, levelIndex)) return 0;
        return CostsByForm[(int)form][ClampLevel(kind, levelIndex)];
    }

    public static string GetNextStepName(WindForm form, UpgradeKind kind, int levelIndex)
    {
        if (IsMaxLevel(kind, levelIndex)) return "满级";
        return StepNames[(int)form][(int)kind][ClampLevel(kind, levelIndex)];
    }

    public static string GetValueText(WindForm form, UpgradeKind kind, int levelIndex)
    {
        int formIndex = (int)form;
        int level = ClampLevel(kind, levelIndex);

        switch (kind)
        {
            case UpgradeKind.WindPower:
                return "风力" + FormatNumber(PowerLevels[formIndex][level]);

            case UpgradeKind.WindArea:
                if (form == WindForm.Surface)
                {
                    return "长" + FormatNumber(SurfaceLengthLevels[level]) + "m，宽"
                        + FormatNumber(SurfaceStartWidthLevels[level]) + "～"
                        + FormatNumber(SurfaceEndWidthLevels[level]) + "m";
                }

                return "半径" + FormatNumber(RadiusLevels[formIndex][level]) + "m";

            case UpgradeKind.WindPulse:
                return "风载" + MaxTargetLevels[formIndex][level]
                    + "，间隔" + IntervalLevels[formIndex][level].ToString("0.00") + "秒";

            default:
                return "";
        }
    }

    public static string GetNextValueText(WindForm form, UpgradeKind kind, int levelIndex)
    {
        if (IsMaxLevel(kind, levelIndex)) return "MAX";
        return GetValueText(form, kind, levelIndex + 1);
    }

    public static WindRuntimeValues GetRuntimeValues(WindForm form, int[] levels, UpgradeInheritance inheritance)
    {
        int formIndex = (int)form;
        int powerLevel = GetLevel(levels, UpgradeKind.WindPower);
        int areaLevel = GetLevel(levels, UpgradeKind.WindArea);
        int pulseLevel = GetLevel(levels, UpgradeKind.WindPulse);

        WindRuntimeValues values = new WindRuntimeValues
        {
            Shape = GetShape(form),
            Power = PowerLevels[formIndex][powerLevel],
            Radius = form == WindForm.Surface ? 0f : RadiusLevels[formIndex][areaLevel],
            Length = form == WindForm.Surface ? SurfaceLengthLevels[areaLevel] : 0f,
            StartWidth = form == WindForm.Surface ? SurfaceStartWidthLevels[areaLevel] : 0f,
            EndWidth = form == WindForm.Surface ? SurfaceEndWidthLevels[areaLevel] : 0f,
            MaxTargets = MaxTargetLevels[formIndex][pulseLevel],
            Interval = IntervalLevels[formIndex][pulseLevel],
            SurfaceLift = form == WindForm.Surface ? SurfaceLiftRatio : 0f,
            TornadoInwardRatio = form == WindForm.Tornado ? TornadoInwardRatio : 0f,
            TornadoSpinRatio = form == WindForm.Tornado ? TornadoSpinRatio : 0f
        };

        ApplyInheritance(ref values, inheritance);
        return values;
    }

    public static string GetWindName(WindForm form, int[] levels)
    {
        int area = GetLevel(levels, UpgradeKind.WindArea);
        int power = GetLevel(levels, UpgradeKind.WindPower);
        int pulse = GetLevel(levels, UpgradeKind.WindPulse);

        return ScaleWords[area] + PowerWords[power] + PulseWords[pulse] + GetFormName(form);
    }

    public static string FormatNumber(float value)
    {
        return value % 1f == 0f ? ((int)value).ToString() : value.ToString("0.0");
    }

    private static WindShape GetShape(WindForm form)
    {
        switch (form)
        {
            case WindForm.Downburst: return WindShape.Downburst;
            case WindForm.Surface: return WindShape.Surface;
            case WindForm.Tornado: return WindShape.Tornado;
            default: return WindShape.Downburst;
        }
    }

    private static void ApplyInheritance(ref WindRuntimeValues values, UpgradeInheritance inheritance)
    {
        if ((inheritance & UpgradeInheritance.WindPower) != 0)
        {
            values.Power *= 1.15f;
        }

        if ((inheritance & UpgradeInheritance.WindArea) != 0)
        {
            if (values.Shape == WindShape.Surface)
            {
                values.Length *= 1.12f;
                values.StartWidth *= 1.12f;
                values.EndWidth *= 1.12f;
            }
            else
            {
                values.Radius *= 1.12f;
            }
        }

        if ((inheritance & UpgradeInheritance.WindPulse) != 0)
        {
            values.MaxTargets = Mathf.RoundToInt(values.MaxTargets * 1.2f);
        }
    }
}
