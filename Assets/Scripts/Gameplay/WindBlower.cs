using UnityEngine;
using UnityEngine.EventSystems;

public class WindBlower : MonoBehaviour
{
    [Header("判定")]
    [SerializeField, Min(0.1f)] private float radius = 2f;
    [SerializeField, Range(0.1f, 1f)] private float innerRatio = 0.2f;
    [SerializeField, Range(0.1f, 1f)] private float middleRatio = 0.5f;

    [SerializeField] private LayerMask windableLayer;
    [SerializeField, Min(1)] private int queryCapacity = 256;
    [SerializeField, Min(0.01f)] private float blowInterval = 0.08f;
    [SerializeField, Min(0.01f)] private float perLeafCooldown = 0.5f;
    [SerializeField, Min(1)] private int maxTargetsPerBlow = 10;

    [Header("风力")]
    [SerializeField, Min(0f)] private float baseWind = 1f;
    [SerializeField, Min(0.1f)] private float speedScale = 6f;

    [Header("显示")]
    [SerializeField] private bool showWindRings = true;

    private const int RingSegmentCount = 96;

    private Camera mainCamera;
    private Collider2D[] hits;
    private ContactFilter2D windableFilter;
    private readonly System.Collections.Generic.List<WindCandidate> candidates = new System.Collections.Generic.List<WindCandidate>(256);
    private float nextBlowTime;
    private Vector2 lastCenter;
    private bool hasLastCenter;

    private LineRenderer innerRing;
    private LineRenderer middleRing;
    private LineRenderer outerRing;
    private float ringHideTime;

    public float Radius => radius;
    public float BaseWind => baseWind;
    public int MaxTargetsPerBlow => maxTargetsPerBlow;

    public void ConfigureLayer(int layerMask)
    {
        windableLayer = layerMask;
    }

    public void ApplyUpgradeValues(float windValue, float windRadius, int maxTargets)
    {
        baseWind = Mathf.Max(0f, windValue);
        radius = Mathf.Max(0.1f, windRadius);
        maxTargetsPerBlow = Mathf.Max(1, maxTargets);
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        hits = new Collider2D[queryCapacity];

        windableFilter = new ContactFilter2D();

        CreateWindRings();
    }

    private void Update()
    {
        UpdateRingVisibility();

        if (Time.time < nextBlowTime)
        {
            return;
        }

        if (TryGetWindCenter(out Vector2 center))
        {
            Blow(center);
            nextBlowTime = Time.time + blowInterval;
        }
    }

    private bool TryGetWindCenter(out Vector2 center)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            center = default;
            return false;
        }

        if (Input.GetMouseButton(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                center = default;
                return false;
            }

            center = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            return true;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    center = default;
                    return false;
                }

                center = mainCamera.ScreenToWorldPoint(touch.position);
                return true;
            }
        }


        center = default;
        return false;
    }

    private void Blow(Vector2 center)
    {
        int layerMask = windableLayer.value == 0 ? ~0 : windableLayer.value;
        windableFilter.SetLayerMask(layerMask);

        int count = Physics2D.OverlapCircle(center, radius, windableFilter, hits);

        lastCenter = center;
        hasLastCenter = true;

        ShowWindRings(center);

        candidates.Clear();
        for (int i = 0; i < count; i++)
        {
            Windable windable = hits[i].GetComponentInParent<Windable>();
            if (windable == null)
            {
                continue;
            }

            Vector2 direction = windable.Position - center;
            float distance = direction.magnitude;
            if (distance > radius)
            {
                continue;
            }

            candidates.Add(new WindCandidate(windable, direction, distance, GetRingSpeed(distance)));
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        int pushed = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            WindCandidate candidate = candidates[i];
            if (candidate.Windable.TryPushByWind(candidate.Direction, candidate.RingSpeed, perLeafCooldown))
            {
                pushed++;
                if (pushed >= maxTargetsPerBlow) return;
            }
        }
    }

    private float GetRingSpeed(float distance)
    {
        float rate = distance / radius;
        float scaledBase = baseWind * speedScale;

        if (rate <= innerRatio)
        {
            return scaledBase;
        }

        if (rate <= middleRatio)
        {
            return scaledBase * 0.6f;
        }

        return scaledBase * 0.15f;
    }

    private void CreateWindRings()
    {
        if (!showWindRings)
        {
            return;
        }

        innerRing = CreateRing("InnerWindRing", Theme.WindInner);
        middleRing = CreateRing("MiddleWindRing", Theme.WindMiddle);
        outerRing = CreateRing("OuterWindRing", Theme.WindOuter);
    }

    private LineRenderer CreateRing(string objectName, Color color)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(transform);

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = RingSegmentCount;
        ring.startWidth = 0.04f;
        ring.endWidth = 0.04f;
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.startColor = color;
        ring.endColor = color;
        ring.sortingLayerName = "Foreground";
        ring.sortingOrder = 20;
        ring.enabled = false;

        return ring;
    }

    private void ShowWindRings(Vector2 center)
    {
        if (!showWindRings)
        {
            return;
        }

        SetRing(innerRing, center, radius * innerRatio);
        SetRing(middleRing, center, radius * middleRatio);
        SetRing(outerRing, center, radius);

        SetRingsVisible(true);

        ringHideTime = Time.time + 0.18f;
    }

    private void SetRing(LineRenderer ring, Vector2 center, float ringRadius)
    {
        if (ring == null)
        {
            return;
        }

        for (int i = 0; i < RingSegmentCount; i++)
        {
            float angle = i / (float)RingSegmentCount * Mathf.PI * 2f;

            Vector3 position = new Vector3(
                center.x + Mathf.Cos(angle) * ringRadius,
                center.y + Mathf.Sin(angle) * ringRadius,
                0f
            );

            ring.SetPosition(i, position);
        }
    }

    private void UpdateRingVisibility()
    {
        if (showWindRings && hasLastCenter && Time.time > ringHideTime)
        {
            SetRingsVisible(false);
        }
    }

    private void SetRingsVisible(bool visible)
    {
        if (innerRing != null)
        {
            innerRing.enabled = visible;
        }

        if (middleRing != null)
        {
            middleRing.enabled = visible;
        }

        if (outerRing != null)
        {
            outerRing.enabled = visible;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 center = hasLastCenter ? lastCenter : (Vector2)transform.position;

        Gizmos.DrawWireSphere(center, radius);
        Gizmos.DrawWireSphere(center, radius * innerRatio);
        Gizmos.DrawWireSphere(center, radius * middleRatio);
    }

}

public readonly struct WindCandidate
{
    public readonly Windable Windable;
    public readonly Vector2 Direction;
    public readonly float Distance;
    public readonly float RingSpeed;

    public WindCandidate(Windable windable, Vector2 direction, float distance, float ringSpeed)
    {
        Windable = windable;
        Direction = direction;
        Distance = distance;
        RingSpeed = ringSpeed;
    }
}
