using UnityEngine;

public class RiverCollector : MonoBehaviour
{
    public static float CoinCount { get; private set; }
    public static float SessionCoins { get; private set; }

    public int CollectedCount { get; private set; }
    private static float leafValue = 1f;
    [SerializeField] private RiverWaterMask waterMask;

    private void Awake()
    {
        if (waterMask == null)
        {
            waterMask = GetComponent<RiverWaterMask>();
        }
    }

    public void SetWaterMask(RiverWaterMask mask)
    {
        waterMask = mask;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCoins()
    {
        CoinCount = 0;
        SessionCoins = 0;
        leafValue = 1f;
    }

    public static void ResetSession()
    {
        SessionCoins = 0;
    }

    public static void SetLeafValue(float value)
    {
        leafValue = Mathf.Max(0f, value);
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

        if (waterMask != null && !waterMask.ContainsWater(windable.Position))
        {
            return;
        }

        if (!windable.TryCollect())
        {
            return;
        }

        CollectedCount++;
        CoinCount += leafValue;
        SessionCoins += leafValue;

        Destroy(windable.gameObject);
    }
}
