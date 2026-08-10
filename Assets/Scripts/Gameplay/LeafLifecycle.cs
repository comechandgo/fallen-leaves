using UnityEngine;

[DisallowMultipleComponent]
public sealed class LeafLifecycle : MonoBehaviour
{
    private LeafSpawner owner;
    private bool registered;

    public void Bind(LeafSpawner spawner)
    {
        if (registered) Unregister();
        owner = spawner;
        registered = owner != null;
        if (registered) owner.Register(this);
    }

    public void MarkCollected()
    {
        Unregister();
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
