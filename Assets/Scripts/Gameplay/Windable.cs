using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Windable : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField, Min(0.01f)] private float weight = 1f;

    public Vector2 Position => body != null ? body.position : transform.position;
    public bool IsCollected { get; private set; }

    private float lastWindPushTime = -999f;

    private void Awake()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (body != null)
        {
            body.gravityScale = 0f;
            body.mass = Mathf.Max(0.01f, weight);
        }
    }

    public void Configure(float newWeight)
    {
        weight = Mathf.Max(0.01f, newWeight);
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (body != null) body.mass = weight;
    }

    public bool TryCollect()
    {
        if (IsCollected)
        {
            return false;
        }

        IsCollected = true;
        return true;
    }

    public bool TryPushByWind(Vector2 direction, float ringSpeed, float cooldown)
    {
        if (body == null || direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        if (Time.time - lastWindPushTime < cooldown)
        {
            return false;
        }

        lastWindPushTime = Time.time;
        body.AddForce(direction.normalized * ringSpeed, ForceMode2D.Impulse);
        return true;
    }
}
