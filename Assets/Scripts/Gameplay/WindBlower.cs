using UnityEngine;
using UnityEngine.EventSystems;

public class WindBlower : MonoBehaviour
{
    [Header("形态")]
    [SerializeField] private WindShape windShape = WindShape.Downburst;
    [SerializeField, Min(0.1f)] private float surfaceLength = 18f;
    [SerializeField, Min(0.1f)] private float surfaceStartWidth = 6f;
    [SerializeField, Min(0.1f)] private float surfaceEndWidth = 10f;
    [SerializeField, Min(0.01f)] private float minDragDistance = 0.05f;

    [Header("判定")]
    [SerializeField, Min(0.1f)] private float radius = 2f;
    [SerializeField, Range(0.1f, 1f)] private float innerRatio = 0.2f;
    [SerializeField, Range(0.1f, 1f)] private float middleRatio = 0.5f;

    [SerializeField] private LayerMask windableLayer;
    [SerializeField, Min(1)] private int queryCapacity = 256;
    [SerializeField, Min(256)] private int maximumQueryCapacity = 8192;
    [SerializeField, Min(0.01f)] private float blowInterval = 0.08f;
    [SerializeField, Min(0.01f)] private float perLeafCooldown = 0.5f;
    [SerializeField, Min(1)] private int maxTargetsPerBlow = 10;

    [Header("风力")]
    [SerializeField, Min(0f)] private float baseWind = 1f;
    [SerializeField, Min(0.1f)] private float speedScale = 6f;
    [SerializeField, Range(0f, 0.75f)] private float surfaceLiftRatio = 0.32f;
    [SerializeField, Range(0f, 1f)] private float tornadoInwardRatio = 0.55f;
    [SerializeField, Range(0.1f, 1.5f)] private float tornadoSpinRatio = 1f;

    [Header("显示")]
    [SerializeField, Min(0.01f)] private float windEffectInterval = 0.16f;

    private Camera mainCamera;
    private Collider2D[] hits;
    private ContactFilter2D windableFilter;
    private readonly System.Collections.Generic.List<WindCandidate> candidates = new System.Collections.Generic.List<WindCandidate>(256);
    private float nextBlowTime;
    private Vector2 lastCenter;
    private bool hasLastCenter;
    private Vector2 lastPointerWorld;
    private bool hasLastPointerWorld;
    private Vector2 lastEffectPointerWorld;
    private bool hasLastEffectPointerWorld;
    private Vector2 lastEffectDirection = Vector2.up;

    private WindEffectSpawner windEffectSpawner;
    private float nextWindEffectTime;

    public float Radius => radius;
    public float BaseWind => baseWind;
    public int MaxTargetsPerBlow => maxTargetsPerBlow;
    public float BlowInterval => blowInterval;
    public WindShape Shape => windShape;
    public float SurfaceLength => surfaceLength;
    public float SurfaceStartWidth => surfaceStartWidth;
    public float SurfaceEndWidth => surfaceEndWidth;

    public void ConfigureLayer(int layerMask)
    {
        windableLayer = layerMask;
    }

    public void ApplyUpgradeValues(WindRuntimeValues values)
    {
        bool shapeChanged = windShape != values.Shape;
        windShape = values.Shape;
        baseWind = Mathf.Max(0f, values.Power);
        radius = Mathf.Max(0.1f, values.Radius);
        surfaceLength = Mathf.Max(0.1f, values.Length);
        surfaceStartWidth = Mathf.Max(0.1f, values.StartWidth);
        surfaceEndWidth = Mathf.Max(0.1f, values.EndWidth);
        maxTargetsPerBlow = Mathf.Max(1, values.MaxTargets);
        blowInterval = Mathf.Max(0.01f, values.Interval);
        perLeafCooldown = Mathf.Max(0.01f, values.Interval);
        surfaceLiftRatio = Mathf.Clamp(values.SurfaceLift, 0f, 0.75f);
        tornadoInwardRatio = Mathf.Clamp01(values.TornadoInwardRatio);
        tornadoSpinRatio = Mathf.Clamp(values.TornadoSpinRatio, 0.1f, 1.5f);

        if (shapeChanged)
        {
            hasLastPointerWorld = false;
            hasLastEffectPointerWorld = false;
            lastEffectDirection = Vector2.up;
            windEffectSpawner?.StopActiveEffect();
        }
    }

    public void ApplyUpgradeValues(float windValue, float windRadius, int maxTargets)
    {
        windShape = WindShape.Downburst;
        baseWind = Mathf.Max(0f, windValue);
        radius = Mathf.Max(0.1f, windRadius);
        maxTargetsPerBlow = Mathf.Max(1, maxTargets);
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        hits = new Collider2D[queryCapacity];

        windableFilter = new ContactFilter2D();

        CacheWindEffectSpawner();
    }

    private void CacheWindEffectSpawner()
    {
        windEffectSpawner = GetComponent<WindEffectSpawner>();

        if (windEffectSpawner == null)
        {
            windEffectSpawner = gameObject.AddComponent<WindEffectSpawner>();
        }
    }

    private void Update()
    {
        UpdateWindEffect();

        if (Time.time < nextBlowTime)
        {
            return;
        }

        if (TryGetWindInput(out Vector2 center, out Vector2 direction))
        {
            Blow(center, direction);
            nextBlowTime = Time.time + blowInterval;
        }
    }

    private void UpdateWindEffect()
    {
        if (!TryGetPointerWorld(out Vector2 center))
        {
            hasLastEffectPointerWorld = false;
            return;
        }

        if (windShape != WindShape.Surface)
        {
            lastEffectPointerWorld = center;
            hasLastEffectPointerWorld = true;
            TryPlayWindEffect(center, Vector2.up);
            return;
        }

        if (!hasLastEffectPointerWorld)
        {
            lastEffectPointerWorld = center;
            hasLastEffectPointerWorld = true;
            return;
        }

        Vector2 delta = center - lastEffectPointerWorld;
        if (delta.sqrMagnitude >= minDragDistance * minDragDistance)
        {
            lastEffectDirection = delta.normalized;
            lastEffectPointerWorld = center;
            TryPlayWindEffect(center, lastEffectDirection);
        }
    }

    private bool TryGetWindInput(out Vector2 center, out Vector2 direction)
    {
        if (!TryGetPointerWorld(out center))
        {
            hasLastPointerWorld = false;
            direction = Vector2.zero;
            return false;
        }

        if (windShape != WindShape.Surface)
        {
            hasLastPointerWorld = true;
            lastPointerWorld = center;
            direction = Vector2.zero;
            return true;
        }

        if (!hasLastPointerWorld)
        {
            hasLastPointerWorld = true;
            lastPointerWorld = center;
            direction = Vector2.zero;
            return false;
        }

        Vector2 delta = center - lastPointerWorld;
        lastPointerWorld = center;

        if (delta.sqrMagnitude < minDragDistance * minDragDistance)
        {
            direction = Vector2.zero;
            return false;
        }

        direction = delta.normalized;
        return true;
    }

    private bool TryGetPointerWorld(out Vector2 world)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            world = default;
            return false;
        }

        if (Input.GetMouseButton(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                world = default;
                return false;
            }

            world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            return true;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    world = default;
                    return false;
                }

                world = mainCamera.ScreenToWorldPoint(touch.position);
                return true;
            }
        }

        world = default;
        return false;
    }

    private int Blow(Vector2 center, Vector2 windDirection)
    {
        int layerMask = windableLayer.value == 0 ? ~0 : windableLayer.value;
        windableFilter.SetLayerMask(layerMask);

        float queryRadius = GetQueryRadius();
        int count = QueryWindables(center, queryRadius);

        lastCenter = center;
        hasLastCenter = true;

        TryPlayWindEffect(center, windDirection);

        candidates.Clear();
        for (int i = 0; i < count; i++)
        {
            Windable windable = hits[i].GetComponentInParent<Windable>();
            if (windable == null)
            {
                continue;
            }

            if (TryCreateCandidate(windable, center, windDirection, out WindCandidate candidate))
            {
                candidates.Add(candidate);
            }
        }

        candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        int pushed = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            WindCandidate candidate = candidates[i];
            if (candidate.Windable.TryPushByWind(candidate.Direction, candidate.RingSpeed, perLeafCooldown))
            {
                pushed++;
                if (pushed >= maxTargetsPerBlow) break;
            }
        }

        if (pushed > 0) GameAudioManager.PlayLeafRustle();
        return pushed;
    }

    private int QueryWindables(Vector2 center, float queryRadius)
    {
        if (hits == null || hits.Length == 0)
        {
            hits = new Collider2D[Mathf.Max(1, queryCapacity)];
        }

        int count;
        while (true)
        {
            count = Physics2D.OverlapCircle(center, queryRadius, windableFilter, hits);
            if (count < hits.Length || hits.Length >= maximumQueryCapacity) return count;

            int nextCapacity = Mathf.Min(hits.Length * 2, maximumQueryCapacity);
            hits = new Collider2D[nextCapacity];
            queryCapacity = nextCapacity;
        }
    }

    private void TryPlayWindEffect(Vector2 center, Vector2 windDirection)
    {
        if (Time.time < nextWindEffectTime)
        {
            return;
        }

        if (windEffectSpawner == null)
        {
            CacheWindEffectSpawner();
        }

        if (windEffectSpawner == null)
        {
            return;
        }

        windEffectSpawner.Play(
            windShape,
            center,
            GetEffectDirection(windDirection),
            radius,
            surfaceLength,
            surfaceStartWidth,
            surfaceEndWidth);

        nextWindEffectTime = Time.time + windEffectInterval;
    }

    private Vector2 GetEffectDirection(Vector2 windDirection)
    {
        if (windShape == WindShape.Surface && windDirection.sqrMagnitude > 0.0001f)
        {
            return windDirection.normalized;
        }

        return Vector2.up;
    }

    private bool TryCreateCandidate(
        Windable windable,
        Vector2 center,
        Vector2 windDirection,
        out WindCandidate candidate)
    {
        Vector2 offset = windable.Position - center;

        switch (windShape)
        {
            case WindShape.Surface:
                return TryCreateSurfaceCandidate(windable, offset, windDirection, out candidate);

            case WindShape.Tornado:
                return TryCreateTornadoCandidate(windable, offset, out candidate);

            default:
                return TryCreateDownburstCandidate(windable, offset, out candidate);
        }
    }

    private bool TryCreateDownburstCandidate(
        Windable windable,
        Vector2 offset,
        out WindCandidate candidate)
    {
        float distance = offset.magnitude;
        if (distance <= 0.0001f || distance > radius)
        {
            candidate = default;
            return false;
        }

        candidate = new WindCandidate(windable, offset, distance, GetRingSpeed(distance, radius));
        return true;
    }

    private bool TryCreateSurfaceCandidate(
        Windable windable,
        Vector2 offset,
        Vector2 windDirection,
        out WindCandidate candidate)
    {
        if (windDirection.sqrMagnitude <= 0.0001f)
        {
            candidate = default;
            return false;
        }

        float forward = Vector2.Dot(offset, windDirection);
        if (forward < 0f || forward > surfaceLength)
        {
            candidate = default;
            return false;
        }

        Vector2 sideDirection = new Vector2(-windDirection.y, windDirection.x);
        float side = Mathf.Abs(Vector2.Dot(offset, sideDirection));
        float halfWidth = GetSurfaceHalfWidth(forward);

        if (side > halfWidth)
        {
            candidate = default;
            return false;
        }

        float rate = surfaceLength <= 0.0001f ? 0f : forward / surfaceLength;
        float speed = baseWind * speedScale * Mathf.Lerp(1f, 0.65f, rate);

        Vector2 liftedDirection = GetSurfaceLiftedDirection(windDirection);
        candidate = new WindCandidate(windable, liftedDirection, forward, speed);
        return true;
    }

    private bool TryCreateTornadoCandidate(
        Windable windable,
        Vector2 offset,
        out WindCandidate candidate)
    {
        float distance = offset.magnitude;
        if (distance <= 0.0001f || distance > radius)
        {
            candidate = default;
            return false;
        }

        Vector2 outward = offset / distance;
        Vector2 inward = -outward;
        Vector2 tangent = new Vector2(-offset.y, offset.x).normalized * tornadoSpinRatio;
        Vector2 swirl = (tangent + inward * tornadoInwardRatio).normalized;
        float centerPull = Mathf.Lerp(1.2f, 0.75f, distance / Mathf.Max(0.0001f, radius));

        candidate = new WindCandidate(windable, swirl, distance, GetRingSpeed(distance, radius) * centerPull);
        return true;
    }

    private Vector2 GetSurfaceLiftedDirection(Vector2 windDirection)
    {
        if (surfaceLiftRatio <= 0f || windDirection.sqrMagnitude <= 0.0001f)
        {
            return windDirection;
        }

        Vector2 direction = windDirection.normalized;
        Vector2 lifted = direction + Vector2.up * surfaceLiftRatio;
        return lifted.sqrMagnitude <= 0.0001f ? direction : lifted.normalized;
    }

    private float GetSurfaceHalfWidth(float forwardDistance)
    {
        float rate = surfaceLength <= 0.0001f ? 0f : Mathf.Clamp01(forwardDistance / surfaceLength);
        return Mathf.Lerp(surfaceStartWidth, surfaceEndWidth, rate) * 0.5f;
    }

    private float GetQueryRadius()
    {
        return windShape == WindShape.Surface
            ? Mathf.Max(surfaceLength, surfaceEndWidth)
            : radius;
    }

    private float GetRingSpeed(float distance, float effectiveRadius)
    {
        float rate = distance / Mathf.Max(0.0001f, effectiveRadius);
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
