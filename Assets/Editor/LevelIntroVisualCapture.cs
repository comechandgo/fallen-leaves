using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LevelIntroVisualCapture
{
    private const string RunningKey = "FallenLeaves.LevelIntroVisualCapture.Running";
    private const string FirstResolutionKey = "FallenLeaves.LevelIntroVisualCapture.FirstResolution";
    private const string LastResolutionKey = "FallenLeaves.LevelIntroVisualCapture.LastResolution";
    private const int WaitFrames = 3;

    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1920, 1080)
    };

    private static bool hooksAttached;
    private static bool updateAttached;
    private static int stage;
    private static int resolutionIndex;
    private static int lastResolutionIndex = Resolutions.Length - 1;
    private static int waitUntilFrame;
    private static GameFlowManager flow;
    private static LevelIntroUI intro;
    private static string outputDirectory;
    private static Camera captureCamera;
    private static Camera captureUiCamera;
    private static Canvas captureCanvas;
    private static RenderTexture captureTexture;
    private static RenderTexture originalCameraTarget;
    private static int originalCameraCullingMask;
    private static RenderMode originalCanvasRenderMode;
    private static Camera originalCanvasWorldCamera;
    private static float originalCanvasPlaneDistance;
    private static Transform[] captureUiTransforms;
    private static int[] originalUiLayers;

    static LevelIntroVisualCapture()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        resolutionIndex = Mathf.Clamp(
            SessionState.GetInt(FirstResolutionKey, 0),
            0,
            Resolutions.Length - 1);
        lastResolutionIndex = Mathf.Clamp(
            SessionState.GetInt(LastResolutionKey, Resolutions.Length - 1),
            resolutionIndex,
            Resolutions.Length - 1);
        EnsureOutputDirectory();
        AttachHooks();
        EditorApplication.delayCall += ResumeAfterDomainReload;
    }

    [MenuItem("Tools/Fallen Leaves/Capture Level Intro")]
    public static void RunMenu()
    {
        Begin(0, Resolutions.Length - 1);
    }

    public static void RunBatch()
    {
        Begin(0, Resolutions.Length - 1);
    }

    public static void RunBatch1280()
    {
        Begin(0, 0);
    }

    public static void RunBatch1920()
    {
        Begin(1, 1);
    }

    private static void Begin(int firstResolutionIndex, int finalResolutionIndex)
    {
        resolutionIndex = Mathf.Clamp(firstResolutionIndex, 0, Resolutions.Length - 1);
        lastResolutionIndex = Mathf.Clamp(
            finalResolutionIndex,
            resolutionIndex,
            Resolutions.Length - 1);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetInt(FirstResolutionKey, resolutionIndex);
        SessionState.SetInt(LastResolutionKey, lastResolutionIndex);
        stage = 0;
        flow = null;
        intro = null;
        EnsureOutputDirectory();

        AttachHooks();
        if (EditorApplication.isPlaying) QueueUpdate();
        else EditorApplication.EnterPlaymode();
    }

    private static void ResumeAfterDomainReload()
    {
        if (EditorApplication.isPlaying) QueueUpdate();
    }

    private static void AttachHooks()
    {
        if (hooksAttached) return;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        hooksAttached = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode) QueueUpdate();
    }

    private static void QueueUpdate()
    {
        if (updateAttached) return;
        updateAttached = true;
        waitUntilFrame = Time.frameCount + 1;
        EditorApplication.update += UpdateCapture;
    }

    private static void UpdateCapture()
    {
        if (Time.frameCount < waitUntilFrame) return;

        try
        {
            if (flow == null)
            {
                flow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
                if (flow == null) return;
                flow.enabled = false;
                intro = GetPrivateField<LevelIntroUI>(flow, "levelIntro");
            }

            switch (stage)
            {
                case 0:
                    PrepareResolution();
                    break;
                case 1:
                    PoseBlackout();
                    break;
                case 2:
                    Capture("blackout");
                    break;
                case 3:
                    PosePrompt();
                    break;
                case 4:
                    Capture("prompt");
                    break;
                case 5:
                    PoseMidReveal();
                    break;
                case 6:
                    Capture("mid-reveal");
                    break;
                case 7:
                    PoseComplete();
                    break;
                case 8:
                    Capture("complete");
                    break;
                default:
                    AdvanceResolutionOrComplete();
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Level intro visual capture failed: {exception}");
            Complete(1);
        }
    }

    private static void PrepareResolution()
    {
        if (resolutionIndex > 0)
        {
            InvokePrivate(flow, "ReturnToLevelSelect");
        }

        ReleaseCaptureTarget();
        ConfigureCaptureTarget(Resolutions[resolutionIndex]);
        AdvanceStage(WaitFrames);
    }

    private static void PoseBlackout()
    {
        InvokePrivate(flow, "StartLevel", LevelId.SimpleSmall);
        flow.enabled = false;
        intro.SetFadeToBlackProgress(1f);
        Canvas.ForceUpdateCanvases();
        AdvanceStage(WaitFrames);
    }

    private static void PosePrompt()
    {
        InvokePrivate(flow, "AdvanceLevelIntro", 0.35f);
        Canvas.ForceUpdateCanvases();
        AdvanceStage(WaitFrames);
    }

    private static void PoseMidReveal()
    {
        CompleteInitialSpawn(LevelLoader.Current);
        InvokePrivate(flow, "AdvanceLevelIntro", 0.60f);
        InvokePrivate(flow, "AdvanceLevelIntro", 0f);
        InvokePrivate(flow, "AdvanceLevelIntro", 0.60f);
        Canvas.ForceUpdateCanvases();
        AdvanceStage(WaitFrames);
    }

    private static void PoseComplete()
    {
        InvokePrivate(flow, "AdvanceLevelIntro", 0.70f);
        Canvas.ForceUpdateCanvases();
        AdvanceStage(WaitFrames);
    }

    private static void Capture(string pose)
    {
        Vector2Int requested = Resolutions[resolutionIndex];
        string fileName = $"level-intro-{requested.x}x{requested.y}-{pose}.png";
        string outputPath = Path.Combine(outputDirectory, fileName);

        CaptureToPng(outputPath, requested.x, requested.y);
        Debug.Log($"Level intro visual capture written: {outputPath}");
        AdvanceStage(WaitFrames);
    }

    private static void CaptureToPng(string outputPath, int width, int height)
    {
        if (captureTexture == null || captureTexture.width != width || captureTexture.height != height)
        {
            throw new InvalidOperationException("The offscreen capture target is unavailable or has the wrong size.");
        }

        RenderTexture originalActive = RenderTexture.active;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);

        try
        {
            RenderTexture.active = captureTexture;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply(false);
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = originalActive;
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void ConfigureCaptureTarget(Vector2Int resolution)
    {
        captureCamera = Camera.main;
        captureCanvas = flow != null ? flow.GetComponentInChildren<Canvas>(true) : null;
        if (captureCamera == null || captureCanvas == null)
        {
            throw new InvalidOperationException("The gameplay camera or UI canvas is unavailable for capture.");
        }

        originalCameraTarget = captureCamera.targetTexture;
        originalCameraCullingMask = captureCamera.cullingMask;
        originalCanvasRenderMode = captureCanvas.renderMode;
        originalCanvasWorldCamera = captureCanvas.worldCamera;
        originalCanvasPlaneDistance = captureCanvas.planeDistance;

        captureTexture = new RenderTexture(
            resolution.x,
            resolution.y,
            24,
            RenderTextureFormat.ARGB32)
        {
            name = $"LevelIntroCapture_{resolution.x}x{resolution.y}",
            filterMode = FilterMode.Bilinear
        };
        captureTexture.Create();

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;
        captureUiTransforms = captureCanvas.GetComponentsInChildren<Transform>(true);
        originalUiLayers = new int[captureUiTransforms.Length];
        for (int i = 0; i < captureUiTransforms.Length; i++)
        {
            originalUiLayers[i] = captureUiTransforms[i].gameObject.layer;
            captureUiTransforms[i].gameObject.layer = uiLayer;
        }

        captureCamera.targetTexture = captureTexture;
        captureCamera.cullingMask &= ~(1 << uiLayer);

        GameObject uiCameraObject = new GameObject("LevelIntroCaptureUICamera")
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = uiLayer
        };
        captureUiCamera = uiCameraObject.AddComponent<Camera>();
        captureUiCamera.CopyFrom(captureCamera);
        captureUiCamera.transform.SetPositionAndRotation(
            captureCamera.transform.position,
            captureCamera.transform.rotation);
        captureUiCamera.clearFlags = CameraClearFlags.Depth;
        captureUiCamera.cullingMask = 1 << uiLayer;
        captureUiCamera.depth = captureCamera.depth + 100f;
        captureUiCamera.targetTexture = captureTexture;

        captureCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        captureCanvas.worldCamera = captureUiCamera;
        captureCanvas.planeDistance = Mathf.Max(captureUiCamera.nearClipPlane + 0.1f, 1f);
        Canvas.ForceUpdateCanvases();
    }

    private static void ReleaseCaptureTarget()
    {
        if (captureCamera != null)
        {
            captureCamera.targetTexture = originalCameraTarget;
            captureCamera.cullingMask = originalCameraCullingMask;
        }
        if (captureCanvas != null)
        {
            captureCanvas.renderMode = originalCanvasRenderMode;
            captureCanvas.worldCamera = originalCanvasWorldCamera;
            captureCanvas.planeDistance = originalCanvasPlaneDistance;
        }

        if (captureUiTransforms != null && originalUiLayers != null)
        {
            int count = Mathf.Min(captureUiTransforms.Length, originalUiLayers.Length);
            for (int i = 0; i < count; i++)
            {
                if (captureUiTransforms[i] != null)
                    captureUiTransforms[i].gameObject.layer = originalUiLayers[i];
            }
        }

        if (captureUiCamera != null)
        {
            UnityEngine.Object.DestroyImmediate(captureUiCamera.gameObject);
        }

        if (captureTexture != null)
        {
            captureTexture.Release();
            UnityEngine.Object.DestroyImmediate(captureTexture);
        }

        captureCamera = null;
        captureUiCamera = null;
        captureCanvas = null;
        captureTexture = null;
        originalCameraTarget = null;
        originalCanvasWorldCamera = null;
        captureUiTransforms = null;
        originalUiLayers = null;
    }

    private static void AdvanceResolutionOrComplete()
    {
        resolutionIndex++;
        if (resolutionIndex <= lastResolutionIndex)
        {
            stage = 0;
            waitUntilFrame = Time.frameCount + WaitFrames;
            return;
        }

        InvokePrivate(flow, "ReturnToMainMenu");
        Complete(0);
    }

    private static void AdvanceStage(int frameDelay)
    {
        stage++;
        waitUntilFrame = Time.frameCount + Mathf.Max(1, frameDelay);
    }

    private static void CompleteInitialSpawn(LevelRoot root)
    {
        int safety = 100;
        while (root != null && !root.IsReady && safety-- > 0)
        {
            root.Tick(0f);
        }

        if (root == null || !root.IsReady)
        {
            throw new InvalidOperationException("The level did not finish initial leaf spawning for capture.");
        }
    }

    private static void EnsureOutputDirectory()
    {
        outputDirectory = Path.GetFullPath(Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "..",
            "logs"));
        Directory.CreateDirectory(outputDirectory);
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(target.GetType().Name, methodName);
        return method.Invoke(target, arguments);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
        return (T)field.GetValue(target);
    }

    private static void Complete(int exitCode)
    {
        ReleaseCaptureTarget();
        SessionState.EraseBool(RunningKey);
        SessionState.EraseInt(FirstResolutionKey);
        SessionState.EraseInt(LastResolutionKey);
        if (updateAttached)
        {
            EditorApplication.update -= UpdateCapture;
            updateAttached = false;
        }

        if (hooksAttached)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            hooksAttached = false;
        }

        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        else EditorApplication.ExitPlaymode();
    }
}
