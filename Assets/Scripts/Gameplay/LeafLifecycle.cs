using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeafLifecycle : MonoBehaviour
{
    private LeafSpawner owner;
    private bool registered;

    public bool SpawnedNearTree { get; private set; }

    public void Bind(LeafSpawner spawner, bool spawnedNearTree = false)
    {
        if (registered) Unregister();
        owner = spawner;
        SpawnedNearTree = spawnedNearTree;
        registered = owner != null;
        if (registered) owner.Register(this);
    }

    public void MarkCollected()
    {
        Unregister();
    }

    public void Recycle()
    {
        LeafSpawner previousOwner = owner;
        Unregister();

        if (previousOwner != null)
        {
            previousOwner.ReturnToPool(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        Unregister();
    }

    private void Unregister()
    {
        if (!registered) return;

        registered = false;
        LeafSpawner previousOwner = owner;
        owner = null;
        if (previousOwner != null) previousOwner.Unregister(this);
    }
}
