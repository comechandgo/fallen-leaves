using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelPrefabVisualCapture
{
    private static readonly CaptureSpec[] CaptureSpecs =
    {
        new CaptureSpec("SimpleSmall", "Assets/Prefabs/Levels/Level_SimpleSmall.prefab"),
        new CaptureSpec("TimedChallenge", "Assets/Prefabs/Levels/Level_TimedChallenge.prefab"),
        new CaptureSpec("Endless", "Assets/Prefabs/Levels/Level_Endless.prefab")
    };

    [MenuItem("Tools/Fallen Leaves/Capture Level Prefab Overview")]
    public static void RunMenu()
    {
        string[] paths = CaptureAllOverviews();
        EditorUtility.DisplayDialog("Level overviews captured", string.Join("\n", paths), "OK");
    }

    public static void RunBatch()
    {
        string[] paths = CaptureAllOverviews();
        Debug.Log($"Level prefab overviews captured: {string.Join(", ", paths)}");
    }

    private static string[] CaptureAllOverviews()
    {
        List<string> paths = new List<string>(CaptureSpecs.Length);
        for (int i = 0; i < CaptureSpecs.Length; i++) paths.Add(CaptureOverview(CaptureSpecs[i]));
        return paths.ToArray();
    }

    private static string CaptureOverview(CaptureSpec spec)
    {
        Scene previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LevelRoot prefab = AssetDatabase.LoadAssetAtPath<LevelRoot>(spec.LevelPath);
        if (prefab == null) throw new FileNotFoundException("Missing generated level prefab", spec.LevelPath);
        LevelRoot instance = (PrefabUtility.InstantiatePrefab(prefab.gameObject, previewScene) as GameObject)?.GetComponent<LevelRoot>();
        if (instance == null) throw new IOException("Could not instantiate the generated level prefab for capture.");

        GameObject cameraObject = new GameObject("Overview Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 46f;
        camera.transform.position = new Vector3(0f, 0f, -100f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Theme.Sky;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 200f;

        const int width = 1200;
        const int height = 900;
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = renderTexture;
            renderTexture.Create();
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply(false);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.GetFullPath(Path.Combine(projectRoot, "..", "logs"));
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, $"level-prefab-overview-{spec.Name}.png");
            byte[] png = image.EncodeToPNG();
            File.WriteAllBytes(outputPath, png);
            if (spec.Name == "SimpleSmall")
                File.WriteAllBytes(Path.Combine(outputDirectory, "level-prefab-overview.png"), png);
            return outputPath;
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            renderTexture.Release();
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(instance.gameObject);
        }
    }

    private readonly struct CaptureSpec
    {
        public readonly string Name;
        public readonly string LevelPath;

        public CaptureSpec(string name, string levelPath)
        {
            Name = name;
            LevelPath = levelPath;
        }
    }
}
