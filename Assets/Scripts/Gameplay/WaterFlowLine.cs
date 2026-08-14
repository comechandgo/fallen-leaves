using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class WaterFlowLine : MonoBehaviour
{
    private RiverWaterMask waterMask;
    private Rect spawnBounds;
    private Vector2 flowDirection = Vector2.right;
    private Vector2[] routePoints;
    private bool routeDriven;
    private float speed = 2f;
    private float lifetime = 6f;
    private float age;
    private SpriteRenderer lineRenderer;

    public void Configure(RiverWaterMask mask, Rect bounds, Vector2 direction, float flowSpeed, float flowLifetime)
    {
        waterMask = mask;
        spawnBounds = bounds;
        routePoints = null;
        routeDriven = false;
        flowDirection = direction.sqrMagnitude <= 0.001f ? Vector2.right : direction.normalized;
        speed = Mathf.Max(0.01f, flowSpeed);
        lifetime = Mathf.Max(0.1f, flowLifetime);
        Respawn(Random.value * lifetime);
    }

    public void Configure(RiverWaterMask mask, Rect bounds, Vector2[] route, float flowSpeed, float flowLifetime)
    {
        waterMask = mask;
        spawnBounds = bounds;
        routePoints = route;
        routeDriven = routePoints != null && routePoints.Length > 1;
        flowDirection = routeDriven ? FindNearestRouteDirection(bounds.center) : Vector2.right;
        speed = Mathf.Max(0.01f, flowSpeed);
        lifetime = Mathf.Max(0.1f, flowLifetime);
        Respawn(Random.value * lifetime);
    }

    private void Update()
    {
        if (routeDriven)
        {
            Vector2 targetDirection = FindNearestRouteDirection(transform.position);
            float turnBlend = 1f - Mathf.Exp(-7f * Time.deltaTime);
            flowDirection = Vector2.Lerp(flowDirection, targetDirection, turnBlend).normalized;
            ApplyRotation();
        }

        age += Time.deltaTime;
        Vector2 nextPosition = (Vector2)transform.position + flowDirection * (speed * Time.deltaTime);

        if (age >= lifetime || waterMask == null || !IsLineInsideWater(nextPosition, flowDirection))
        {
            Respawn(0f);
            return;
        }

        transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);
    }

    private void Respawn(float startingAge)
    {
        age = startingAge;
        if (lineRenderer == null) lineRenderer = GetComponent<SpriteRenderer>();
        bool found = false;

        for (int i = 0; i < 360; i++)
        {
            Vector2 position = new Vector2(
                Random.Range(spawnBounds.xMin, spawnBounds.xMax),
                Random.Range(spawnBounds.yMin, spawnBounds.yMax)
            );

            Vector2 candidateDirection = routeDriven ? FindNearestRouteDirection(position) : flowDirection;
            if (!IsLineInsideWater(position, candidateDirection)) continue;

            transform.position = new Vector3(position.x, position.y, 0f);
            flowDirection = candidateDirection;
            found = true;
            break;
        }

        ApplyRotation();
        if (lineRenderer != null) lineRenderer.enabled = found;
    }

    private Vector2 FindNearestRouteDirection(Vector2 position)
    {
        if (routePoints == null || routePoints.Length < 2) return flowDirection;

        float bestDistance = float.PositiveInfinity;
        Vector2 bestDirection = Vector2.right;
        for (int i = 1; i < routePoints.Length; i++)
        {
            Vector2 start = routePoints[i - 1];
            Vector2 delta = routePoints[i] - start;
            float lengthSquared = delta.sqrMagnitude;
            if (lengthSquared <= 0.0001f) continue;

            float t = Mathf.Clamp01(Vector2.Dot(position - start, delta) / lengthSquared);
            Vector2 nearest = start + delta * t;
            float distance = (position - nearest).sqrMagnitude;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            bestDirection = delta.normalized;
        }

        return bestDirection;
    }

    private void ApplyRotation()
    {
        float angle = Mathf.Atan2(flowDirection.y, flowDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool IsLineInsideWater(Vector2 center, Vector2 direction)
    {
        if (waterMask == null) return false;
        if (lineRenderer == null) lineRenderer = GetComponent<SpriteRenderer>();
        if (lineRenderer == null || lineRenderer.sprite == null) return waterMask.ContainsWater(center);

        Vector2 forward = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        Vector2 perpendicular = new Vector2(-forward.y, forward.x);
        Vector3 scale = transform.lossyScale;
        float halfLength = lineRenderer.sprite.bounds.extents.x * Mathf.Abs(scale.x) * 0.82f;
        float halfThickness = lineRenderer.sprite.bounds.extents.y * Mathf.Abs(scale.y) * 0.55f;

        const int lengthSamples = 9;
        for (int x = 0; x < lengthSamples; x++)
        {
            float along = Mathf.Lerp(-halfLength, halfLength, x / (float)(lengthSamples - 1));
            for (int y = -1; y <= 1; y++)
            {
                Vector2 sample = center + forward * along + perpendicular * (halfThickness * y);
                if (!waterMask.ContainsWater(sample)) return false;
            }
        }

        return true;
    }
}
