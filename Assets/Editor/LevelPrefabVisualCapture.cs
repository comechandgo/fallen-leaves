using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelPrefabVisualCapture
{
    private const string LevelPath = "Assets/Prefabs/Levels/Level_SimpleSmall.prefab";

    [MenuItem("Tools/Fallen Leaves/Capture Level Prefab Overview")]
    public static void RunMenu()
    {
        string path = CaptureOverview();
        EditorUtility.DisplayDialog("Level overview captured", path, "OK");
    }

    public static void RunBatch()
    {
        string path = CaptureOverview();
        Debug.Log($"Level prefab overview captured: {path}");
    }

    private static string CaptureOverview()
    {
        Scene previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LevelRoot prefab = AssetDatabase.LoadAssetAtPath<LevelRoot>(LevelPath);
        if (prefab == null) throw new FileNotFoundException("Missing generated level prefab", LevelPath);
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
            string outputPath = Path.Combine(outputDirectory, "level-prefab-overview.png");
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
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
}
