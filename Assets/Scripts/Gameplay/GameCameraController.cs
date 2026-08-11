using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Camera))]
public class GameCameraController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float panSpeed = 1f;
    [SerializeField, Min(0.1f)] private float panSmoothing = 14f;
    [SerializeField, Min(0.1f)] private float zoomSpeed = 12f;
    [SerializeField, Min(0.1f)] private float zoomSmoothing = 12f;
    [SerializeField, Min(1f)] private float minSize = 24f;
    [SerializeField, Min(1f)] private float maxSize = 78f;

    private Camera targetCamera;
    private Rect bounds;
    private bool hasBounds;
    private Vector3 targetPosition;
    private Vector3 lastMouseScreen;
    private float targetSize;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetPosition = transform.position;
        targetSize = targetCamera.orthographicSize;
    }

    public void SetBounds(Rect newBounds)
    {
        SetBounds(newBounds, newBounds.center, minSize, maxSize, targetCamera != null ? targetCamera.orthographicSize : minSize);
    }

    public void SetBounds(Rect newBounds, float newMinSize, float newMaxSize, float initialSize)
    {
        SetBounds(newBounds, newBounds.center, newMinSize, newMaxSize, initialSize);
    }

    public void SetBounds(
        Rect newBounds,
        Vector2 initialPosition,
        float newMinSize,
        float newMaxSize,
        float initialSize)
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        bounds = newBounds;
        float maxSizeByBounds = Mathf.Min(
            bounds.height * 0.5f,
            bounds.width / (2f * Mathf.Max(0.01f, targetCamera.aspect))
        ) - 0.25f;

        maxSize = Mathf.Max(1f, Mathf.Min(newMaxSize, maxSizeByBounds));
        minSize = Mathf.Clamp(newMinSize, 1f, maxSize);
        targetSize = Mathf.Clamp(initialSize, minSize, maxSize);
        targetCamera.orthographicSize = targetSize;
        targetPosition = new Vector3(initialPosition.x, initialPosition.y, -10f);
        hasBounds = true;
        ClampTarget();
        transform.position = targetPosition;
    }


    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        HandlePan();
        HandleZoom();
        ApplySmoothMotion();
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(1))
        {
            lastMouseScreen = Input.mousePosition;
        }

        if (Input.GetMouseButton(1))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector3 mouseScreen = Input.mousePosition;
            Vector3 delta = mouseScreen - lastMouseScreen;
            float worldPerPixel = targetSize * 2f / Mathf.Max(1f, Screen.height);

            targetPosition -= new Vector3(delta.x, delta.y, 0f) * worldPerPixel * panSpeed;
            targetPosition.z = -10f;
            lastMouseScreen = mouseScreen;
            ClampTarget();
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.01f)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        targetSize = Mathf.Clamp(targetSize - scroll * zoomSpeed, minSize, maxSize);
        ClampTarget();
    }

    private void ApplySmoothMotion()
    {
        float dt = Time.unscaledDeltaTime;
        float panLerp = 1f - Mathf.Exp(-panSmoothing * dt);
        float zoomLerp = 1f - Mathf.Exp(-zoomSmoothing * dt);

        transform.position = Vector3.Lerp(transform.position, targetPosition, panLerp);
        targetCamera.orthographicSize = Mathf.Lerp(targetCamera.orthographicSize, targetSize, zoomLerp);
    }

    private void ClampTarget()
    {
        if (!hasBounds)
        {
            return;
        }

        float halfHeight = targetSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        float minX = bounds.xMin + halfWidth;
        float maxX = bounds.xMax - halfWidth;
        float minY = bounds.yMin + halfHeight;
        float maxY = bounds.yMax - halfHeight;

        targetPosition.x = minX > maxX ? bounds.center.x : Mathf.Clamp(targetPosition.x, minX, maxX);
        targetPosition.y = minY > maxY ? bounds.center.y : Mathf.Clamp(targetPosition.y, minY, maxY);
        targetPosition.z = -10f;
    }
}
