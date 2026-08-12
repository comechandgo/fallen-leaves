using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class WindEffectSheetSlicer : AssetPostprocessor
{
    private const int RowCount = 3;
    private const int ColumnCount = 6;
    private const string TargetFileName = "WindEffectSheet";
    private const string LibraryAssetPath = "Assets/Resources/WindEffectLibrary.asset";

    private static readonly string[] RowNames =
    {
        "Downburst",
        "Surface",
        "Tornado"
    };

    static WindEffectSheetSlicer()
    {
        EditorApplication.delayCall += ReimportExistingSheet;
    }

    private void OnPreprocessTexture()
    {
        if (!IsTargetSheet(assetPath))
        {
            return;
        }

        if (!TryReadPngSize(assetPath, out int width, out int height))
        {
            Debug.LogError("风特效图必须是 PNG：" + assetPath);
            return;
        }

        if (width < ColumnCount || height < RowCount)
        {
            Debug.LogError("风特效图尺寸太小，无法切成 6 列 3 行：" + width + "x" + height);
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 100;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritesheet = BuildSprites(width, height, assetPath).ToArray();
    }

    private void OnPostprocessTexture(Texture2D texture)
    {
        if (!IsTargetSheet(assetPath))
        {
            return;
        }

        string sheetPath = assetPath;
        EditorApplication.delayCall += () => CreateOrUpdateLibrary(sheetPath);
    }

    private static void ReimportExistingSheet()
    {
        string[] guids = AssetDatabase.FindAssets(TargetFileName + " t:Texture2D");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (IsTargetSheet(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return;
            }
        }
    }

    private static bool IsTargetSheet(string path)
    {
        return Path.GetFileNameWithoutExtension(path) == TargetFileName;
    }

    private static bool TryReadPngSize(string path, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!File.Exists(path))
        {
            return false;
        }

        byte[] bytes = File.ReadAllBytes(path);

        if (bytes.Length < 24)
        {
            return false;
        }

        width =
            bytes[16] << 24 |
            bytes[17] << 16 |
            bytes[18] << 8 |
            bytes[19];

        height =
            bytes[20] << 24 |
            bytes[21] << 16 |
            bytes[22] << 8 |
            bytes[23];

        return width > 0 && height > 0;
    }

    private static List<SpriteMetaData> BuildSprites(int textureWidth, int textureHeight, string path)
    {
        List<SpriteMetaData> sprites = new List<SpriteMetaData>();

        string sheetName = Path.GetFileNameWithoutExtension(path);

        for (int row = 0; row < RowCount; row++)
        {
            float topA = textureHeight * row / (float)RowCount;
            float topB = textureHeight * (row + 1) / (float)RowCount;

            float roundedTopA = Mathf.Round(topA);
            float roundedTopB = Mathf.Round(topB);

            float rowHeight = roundedTopB - roundedTopA;
            float y = textureHeight - roundedTopB;

            for (int column = 0; column < ColumnCount; column++)
            {
                float leftA = textureWidth * column / (float)ColumnCount;
                float leftB = textureWidth * (column + 1) / (float)ColumnCount;

                float x = Mathf.Round(leftA);
                float width = Mathf.Round(leftB) - x;

                SpriteMetaData sprite = new SpriteMetaData
                {
                    name = sheetName + "_" + RowNames[row] + "_" + column.ToString("00"),
                    rect = new Rect(x, y, width, rowHeight),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center
                };

                sprites.Add(sprite);
            }
        }

        return sprites;
    }

    private static void CreateOrUpdateLibrary(string sheetPath)
    {
        EnsureResourcesFolder();

        WindEffectLibrary library = AssetDatabase.LoadAssetAtPath<WindEffectLibrary>(LibraryAssetPath);

        if (library == null)
        {
            library = ScriptableObject.CreateInstance<WindEffectLibrary>();
            AssetDatabase.CreateAsset(library, LibraryAssetPath);
        }

        Sprite[] sprites = LoadSprites(sheetPath);

        library.downburstFrames = PickFrames(sprites, "Downburst");
        library.surfaceFrames = PickFrames(sprites, "Surface");
        library.tornadoFrames = PickFrames(sprites, "Tornado");

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }

    private static Sprite[] LoadSprites(string sheetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
        List<Sprite> sprites = new List<Sprite>();

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites.ToArray();
    }

    private static Sprite[] PickFrames(Sprite[] sprites, string rowName)
    {
        List<Sprite> result = new List<Sprite>();
        string key = "_" + rowName + "_";

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i].name.Contains(key))
            {
                result.Add(sprites[i]);
            }
        }

        return result.ToArray();
    }
}
