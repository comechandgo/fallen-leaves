using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class WaterFlowLine : MonoBehaviour
{
    private RiverWaterMask waterMask;
    private Rect spawnBounds;
    private Vector2 flowDirection = Vector2.right;
    private float speed = 2f;
    private float lifetime = 6f;
    private float age;

    public void Configure(RiverWaterMask mask, Rect bounds, Vector2 direction, float flowSpeed, float flowLifetime)
    {
        waterMask = mask;
        spawnBounds = bounds;
        flowDirection = direction.sqrMagnitude <= 0.001f ? Vector2.right : direction.normalized;
        speed = Mathf.Max(0.01f, flowSpeed);
        lifetime = Mathf.Max(0.1f, flowLifetime);
        Respawn(Random.value * lifetime);
    }

    private void Update()
    {
        transform.position += (Vector3)(flowDirection * (speed * Time.deltaTime));
        age += Time.deltaTime;

        if (age >= lifetime || waterMask == null || !waterMask.ContainsWater(transform.position))
        {
            Respawn(0f);
        }
    }

    private void Respawn(float startingAge)
    {
        age = startingAge;

        for (int i = 0; i < 120; i++)
        {
            Vector2 position = new Vector2(
                Random.Range(spawnBounds.xMin, spawnBounds.xMax),
                Random.Range(spawnBounds.yMin, spawnBounds.yMax)
            );

            if (waterMask != null && !waterMask.ContainsWater(position))
            {
                continue;
            }

            transform.position = new Vector3(position.x, position.y, 0f);
            break;
        }

        float angle = Mathf.Atan2(flowDirection.y, flowDirection.x) * Mathf.Rad2Deg + Random.Range(-8f, 8f);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
