using UnityEngine;

[CreateAssetMenu(menuName = "Wind/Wind Effect Library")]
public sealed class WindEffectLibrary : ScriptableObject
{
    [Header("源图")]
    public Texture2D sourceSheet;

    [Header("下沉风")]
    public Sprite[] downburstFrames;

    [Header("面风")]
    public Sprite[] surfaceFrames;

    [Header("龙卷风")]
    public Sprite[] tornadoFrames;

    [Header("播放速度")]
    public float downburstFps = 12f;
    public float surfaceFps = 12f;
    public float tornadoFps = 12f;

    private const int SheetColumns = 6;
    private const int SheetRows = 3;
    private const byte AlphaCutoff = 36;
    private const float FringeAlphaScale = 0.15f;

    private static readonly RectInt[][] BackupFrameRects =
    {
        new[]
        {
            new RectInt(26, 899, 168, 175),
            new RectInt(212, 876, 204, 264),
            new RectInt(417, 836, 227, 340),
            new RectInt(645, 876, 212, 266),
            new RectInt(858, 877, 205, 233),
            new RectInt(1077, 913, 165, 157)
        },
        new[]
        {
            new RectInt(22, 490, 163, 309),
            new RectInt(193, 447, 202, 364),
            new RectInt(396, 425, 226, 407),
            new RectInt(623, 450, 223, 351),
            new RectInt(857, 446, 174, 330),
            new RectInt(1060, 460, 157, 303)
        },
        new[]
        {
            new RectInt(0, 151, 215, 202),
            new RectInt(216, 113, 201, 256),
            new RectInt(418, 94, 233, 324),
            new RectInt(652, 111, 213, 260),
            new RectInt(866, 121, 198, 251),
            new RectInt(1065, 152, 184, 194)
        }
    };

    [System.NonSerialized] private Sprite[][] generatedFrames;
    [System.NonSerialized] private Texture2D[][] generatedTextures;

    public Sprite[] GetFrames(WindShape form)
    {
        if (sourceSheet != null)
        {
            return GetGeneratedFrames(form);
        }

        switch (form)
        {
            case WindShape.Downburst:
                return downburstFrames;

            case WindShape.Surface:
                return surfaceFrames;

            case WindShape.Tornado:
                return tornadoFrames;

            default:
                return downburstFrames;
        }
    }

    private Sprite[] GetGeneratedFrames(WindShape form)
    {
        int row = Mathf.Clamp((int)form, 0, SheetRows - 1);
        if (generatedFrames == null)
        {
            generatedFrames = new Sprite[SheetRows][];
        }

        if (generatedTextures == null)
        {
            generatedTextures = new Texture2D[SheetRows][];
        }

        if (generatedFrames[row] != null)
        {
            return generatedFrames[row];
        }

        Sprite[] frames = new Sprite[SheetColumns];
        Texture2D[] textures = new Texture2D[SheetColumns];

        for (int column = 0; column < SheetColumns; column++)
        {
            RectInt frameRect = GetFrameRect(row, column);
            Texture2D frameTexture = TryCreateCleanFrameTexture(row, frameRect);

            Sprite sprite;
            if (frameTexture != null)
            {
                sprite = Sprite.Create(
                    frameTexture,
                    new Rect(0f, 0f, frameTexture.width, frameTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                textures[column] = frameTexture;
            }
            else
            {
                sprite = Sprite.Create(
                    sourceSheet,
                    new Rect(frameRect.x, frameRect.y, frameRect.width, frameRect.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            sprite.name = sourceSheet.name + "_" + form + "_" + column.ToString("00");
            frames[column] = sprite;
        }

        generatedFrames[row] = frames;
        generatedTextures[row] = textures;
        return frames;
    }

    private RectInt GetFrameRect(int row, int column)
    {
        if (sourceSheet.width == 1254 && sourceSheet.height == 1254)
        {
            return BackupFrameRects[row][column];
        }

        float cellWidth = sourceSheet.width / (float)SheetColumns;
        float cellHeight = sourceSheet.height / (float)SheetRows;
        float x = Mathf.Round(cellWidth * column);
        float nextX = Mathf.Round(cellWidth * (column + 1));
        float top = Mathf.Round(cellHeight * row);
        float bottom = Mathf.Round(cellHeight * (row + 1));

        return new RectInt(
            Mathf.RoundToInt(x),
            Mathf.RoundToInt(sourceSheet.height - bottom),
            Mathf.RoundToInt(nextX - x),
            Mathf.RoundToInt(bottom - top));
    }

    private Texture2D TryCreateCleanFrameTexture(int row, RectInt frameRect)
    {
        try
        {
            Vector2Int canvasSize = GetFrameCanvasSize(row);
            Color32[] sourcePixels = sourceSheet.GetPixels32();
            Color32[] cleanPixels = new Color32[canvasSize.x * canvasSize.y];

            int offsetX = Mathf.Max(0, (canvasSize.x - frameRect.width) / 2);
            int offsetY = Mathf.Max(0, (canvasSize.y - frameRect.height) / 2);

            for (int y = 0; y < frameRect.height; y++)
            {
                int sourceY = frameRect.y + y;
                int targetY = offsetY + y;
                if (sourceY < 0 || sourceY >= sourceSheet.height || targetY < 0 || targetY >= canvasSize.y)
                {
                    continue;
                }

                for (int x = 0; x < frameRect.width; x++)
                {
                    int sourceX = frameRect.x + x;
                    int targetX = offsetX + x;
                    if (sourceX < 0 || sourceX >= sourceSheet.width || targetX < 0 || targetX >= canvasSize.x)
                    {
                        continue;
                    }

                    Color32 color = CleanWindPixel(sourcePixels[sourceY * sourceSheet.width + sourceX]);
                    cleanPixels[targetY * canvasSize.x + targetX] = color;
                }
            }

            Texture2D texture = new Texture2D(canvasSize.x, canvasSize.y, TextureFormat.RGBA32, false)
            {
                name = sourceSheet.name + "_CleanFrame",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(cleanPixels);
            texture.Apply(false, true);
            return texture;
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private static Color32 CleanWindPixel(Color32 color)
    {
        if (color.a <= AlphaCutoff)
        {
            color.a = 0;
            return color;
        }

        int alpha = Mathf.RoundToInt((color.a - AlphaCutoff) * 255f / (255f - AlphaCutoff));
        int max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        int min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));

        if (max - min > 120 && (color.r > 160 || color.g > 160))
        {
            alpha = Mathf.RoundToInt(alpha * FringeAlphaScale);
        }

        color.a = (byte)Mathf.Clamp(alpha, 0, 255);
        return color;
    }

    private static Vector2Int GetFrameCanvasSize(int row)
    {
        RectInt[] rowRects = BackupFrameRects[Mathf.Clamp(row, 0, BackupFrameRects.Length - 1)];
        int width = 1;
        int height = 1;

        for (int i = 0; i < rowRects.Length; i++)
        {
            width = Mathf.Max(width, rowRects[i].width);
            height = Mathf.Max(height, rowRects[i].height);
        }

        return new Vector2Int(width, height);
    }

    public float GetFps(WindShape form)
    {
        switch (form)
        {
            case WindShape.Downburst:
                return downburstFps;

            case WindShape.Surface:
                return surfaceFps;

            case WindShape.Tornado:
                return tornadoFps;

            default:
                return 12f;
        }
    }
}
