using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Windable : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField, Min(0.01f)] private float weight = 1f;
    [SerializeField] private LeafWindFeedback windFeedback;

    [Header("Ground movement")]
    [SerializeField, Min(0f)] private float groundFriction = 1.5f;
    [SerializeField, Min(0f)] private float airResistance = 0.30f;
    [SerializeField, Min(0f)] private float stopSpeed = 0.08f;

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
            body.drag = 0f;
            body.mass = Mathf.Max(0.01f, weight);
        }

        if (windFeedback == null)
        {
            windFeedback = GetComponent<LeafWindFeedback>();
        }
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            return;
        }

        body.velocity = CalculateDampedVelocity(
            body.velocity,
            Time.fixedDeltaTime,
            groundFriction,
            airResistance,
            stopSpeed);
    }

    public void Configure(float newWeight)
    {
        weight = Mathf.Max(0.01f, newWeight);
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (body != null) body.mass = weight;
        if (windFeedback == null) windFeedback = GetComponent<LeafWindFeedback>();
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
        if (windFeedback == null) windFeedback = GetComponent<LeafWindFeedback>();
        if (windFeedback != null) windFeedback.Play(direction);
        return true;
    }

    private static Vector2 CalculateDampedVelocity(
        Vector2 velocity,
        float deltaTime,
        float friction,
        float resistance,
        float stoppingSpeed)
    {
        float speed = velocity.magnitude;
        float threshold = Mathf.Max(0f, stoppingSpeed);
        if (speed <= threshold || speed <= 0.0001f)
        {
            return Vector2.zero;
        }

        float deceleration = Mathf.Max(0f, friction) + Mathf.Max(0f, resistance) * speed * speed;
        float nextSpeed = Mathf.Max(0f, speed - deceleration * Mathf.Max(0f, deltaTime));
        if (nextSpeed <= threshold)
        {
            return Vector2.zero;
        }

        return velocity * (nextSpeed / speed);
    }
}
