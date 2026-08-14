using UnityEngine;

public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameFlowManager existingFlow = Object.FindFirstObjectByType<GameFlowManager>();
        if (existingFlow != null)
        {
            if (Object.FindFirstObjectByType<GameAudioManager>() == null)
            {
                existingFlow.gameObject.AddComponent<GameAudioManager>();
            }

            return;
        }

        CreateCamera();
        GameObject flow = new GameObject("GameFlowManager");
        Object.DontDestroyOnLoad(flow);
        flow.AddComponent<GameAudioManager>();
        flow.AddComponent<GameFlowManager>();
    }

    private static Camera CreateCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.backgroundColor = Theme.Sky;
        return camera;
    }
}
