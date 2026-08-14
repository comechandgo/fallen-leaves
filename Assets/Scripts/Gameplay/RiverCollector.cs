using UnityEngine;

public class RiverCollector : MonoBehaviour
{
    public static event System.Action<float> CoinsGained;
    public static event System.Action<int> LeavesCollected;

    public static float CoinCount { get; private set; }
    public static float SessionCoins { get; private set; }
    public static int SessionLeafCount { get; private set; }

    public int CollectedCount { get; private set; }
    [SerializeField] private RiverWaterMask waterMask;
    [SerializeField, Min(0f)] private float collectorMargin;

    private void Awake()
    {
        if (waterMask == null)
        {
            waterMask = GetComponent<RiverWaterMask>();
        }
    }

    public void SetWaterMask(RiverWaterMask mask, float margin = 0f)
    {
        waterMask = mask;
        collectorMargin = Mathf.Max(0f, margin);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCoins()
    {
        CoinsGained = null;
        LeavesCollected = null;
        CoinCount = 0;
        SessionCoins = 0;
        SessionLeafCount = 0;
    }

    /// <summary>清空一局内的可消费金币与累计获得金币。</summary>
    public static void ResetRun()
    {
        CoinCount = 0;
        SessionCoins = 0;
        SessionLeafCount = 0;
    }

    public static bool TrySpendCoins(float amount)
    {
        if (CoinCount < amount)
        {
            return false;
        }

        CoinCount -= amount;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        Windable windable = other.GetComponentInParent<Windable>();
        if (windable == null)
        {
            return;
        }

        if (waterMask != null && !waterMask.IntersectsCircle(windable.Position, collectorMargin))
        {
            return;
        }

        if (!windable.TryCollect())
        {
            return;
        }

        CollectedCount++;
        CoinCount += 1f;
        SessionCoins += 1f;
        SessionLeafCount++;
        LeavesCollected?.Invoke(1);
        CoinsGained?.Invoke(1f);

        LeafLifecycle lifecycle = windable.GetComponent<LeafLifecycle>();
        if (lifecycle != null)
        {
            lifecycle.Recycle();
            return;
        }

        Destroy(windable.gameObject);
    }
}
