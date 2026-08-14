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
    [SerializeField, Min(1)] private int settleFrames = 3;

    public Vector2 Position => body != null ? body.position : transform.position;
    public bool IsCollected { get; private set; }

    private float lastWindPushTime = -999f;
    private int stoppedFrameCount;
    private YSort dynamicSort;

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


        dynamicSort = GetComponentInChildren<YSort>(true);
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

        if (body.velocity.sqrMagnitude <= stopSpeed * stopSpeed)
        {
            stoppedFrameCount++;
            if (stoppedFrameCount >= settleFrames) SetAtRest();
        }
        else
        {
            stoppedFrameCount = 0;
        }
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
        IsCollected = false;
        stoppedFrameCount = 0;
        enabled = true;
        body.WakeUp();
        SetDynamicSort(true);
        body.AddForce(direction.normalized * ringSpeed, ForceMode2D.Impulse);
        if (windFeedback == null) windFeedback = GetComponent<LeafWindFeedback>();
        if (windFeedback != null) windFeedback.Play(direction);
        return true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        WakeForPhysics();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (body != null && body.velocity.sqrMagnitude > stopSpeed * stopSpeed) WakeForPhysics();
    }

    public void ResetForSpawn()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (windFeedback == null) windFeedback = GetComponent<LeafWindFeedback>();
        if (dynamicSort == null) dynamicSort = GetComponentInChildren<YSort>(true);

        IsCollected = false;
        lastWindPushTime = -999f;
        stoppedFrameCount = 0;
        if (body != null)
        {
            body.simulated = true;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.Sleep();
        }
        SetDynamicSort(false);
        enabled = false;
    }

    public void PrepareForPool()
    {
        IsCollected = true;
        stoppedFrameCount = 0;
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.Sleep();
            body.simulated = false;
        }
        SetDynamicSort(false);
        enabled = false;
    }

    private void SetAtRest()
    {
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.Sleep();
        }
        SetDynamicSort(false);
        enabled = false;
    }

    private void SetDynamicSort(bool dynamic)
    {
        if (dynamicSort == null) dynamicSort = GetComponentInChildren<YSort>(true);
        if (dynamicSort != null) dynamicSort.SetDynamic(dynamic);
    }

    private void WakeForPhysics()
    {
        stoppedFrameCount = 0;
        enabled = true;
        SetDynamicSort(true);
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
