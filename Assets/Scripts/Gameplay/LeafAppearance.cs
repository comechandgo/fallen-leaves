using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Windable))]
public sealed class LeafAppearance : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite[] sprites = new Sprite[0];
    [SerializeField] private Vector2 widthRange = new Vector2(0.99f, 1.38f);
    [SerializeField] private Vector2 heightRange = new Vector2(0.84f, 1.26f);
    [SerializeField] private Vector2 weightRange = new Vector2(0.45f, 1.05f);

    public void Configure(
        Sprite[] leafSprites,
        SpriteRenderer renderer,
        Vector2 widths,
        Vector2 heights,
        Vector2 weights)
    {
        sprites = leafSprites ?? new Sprite[0];
        targetRenderer = renderer;
        widthRange = widths;
        heightRange = heights;
        weightRange = weights;
    }

    public void Randomize()
    {
        SpriteRenderer renderer = targetRenderer;
        if (renderer == null) renderer = GetComponentInChildren<SpriteRenderer>(true);
        targetRenderer = renderer;
        if (renderer == null)
        {
            Debug.LogError($"LeafAppearance on {name} is missing its SpriteVisual renderer.", this);
            return;
        }
        if (sprites != null && sprites.Length > 0)
        {
            renderer.sprite = sprites[Random.Range(0, sprites.Length)];
        }

        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Vector2 worldSize = new Vector2(
            Random.Range(Mathf.Min(widthRange.x, widthRange.y), Mathf.Max(widthRange.x, widthRange.y)),
            Random.Range(Mathf.Min(heightRange.x, heightRange.y), Mathf.Max(heightRange.x, heightRange.y)));

        if (renderer.sprite != null)
        {
            Vector2 spriteSize = renderer.sprite.bounds.size;
            transform.localScale = new Vector3(
                worldSize.x / Mathf.Max(0.001f, spriteSize.x),
                worldSize.y / Mathf.Max(0.001f, spriteSize.y),
                1f);
        }

        float weight = Random.Range(
            Mathf.Min(weightRange.x, weightRange.y),
            Mathf.Max(weightRange.x, weightRange.y));

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.mass = weight;

        GetComponent<Windable>().Configure(weight);

        YSort sort = renderer.GetComponent<YSort>();
        if (sort != null)
        {
            sort.Configure("Actor", 1000, worldSize.y * 0.5f, true);
        }
    }
}
