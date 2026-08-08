using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class RuntimeArt
{
    private const string ArtRoot = "WindArt";

    private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

    public static Texture2D LoadTexture(string relativePath)
    {
        string key = Normalize(relativePath);
        if (Textures.TryGetValue(key, out Texture2D cached)) return cached;

        string fullPath = ResolvePath(key);
        if (!File.Exists(fullPath)) return null;

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = Path.GetFileNameWithoutExtension(key);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (!texture.LoadImage(File.ReadAllBytes(fullPath)))
        {
            Object.Destroy(texture);
            return null;
        }

        Textures[key] = texture;
        return texture;
    }

    public static Sprite LoadSprite(string relativePath, float pixelsPerUnit = 100f)
    {
        string key = Normalize(relativePath) + "|" + pixelsPerUnit.ToString("0.##");
        if (Sprites.TryGetValue(key, out Sprite cached)) return cached;

        Texture2D texture = LoadTexture(relativePath);
        if (texture == null) return null;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );
        sprite.name = Path.GetFileNameWithoutExtension(relativePath);

        Sprites[key] = sprite;
        return sprite;
    }

    private static string ResolvePath(string relativePath)
    {
        string platformPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        string streamingPath = Path.Combine(Application.streamingAssetsPath, ArtRoot, platformPath);
        if (File.Exists(streamingPath)) return streamingPath;

        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", platformPath));
    }

    private static string Normalize(string relativePath)
    {
        return relativePath.Replace('\\', '/').TrimStart('/');
    }
}
