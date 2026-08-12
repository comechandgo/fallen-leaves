using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class WindEffectFramePlayer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Sprite[] frames;
    private int index;
    private float timer;
    private float frameInterval;
    private bool destroyOnEnd;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0)
        {
            Finish();
            return;
        }

        timer += Time.deltaTime;

        if (timer < frameInterval)
        {
            return;
        }

        timer -= frameInterval;
        index++;

        if (index >= frames.Length)
        {
            Finish();
            return;
        }

        spriteRenderer.sprite = frames[index];
    }

    public void Play(Sprite[] newFrames, float fps, bool newDestroyOnEnd)
    {
        frames = newFrames;
        destroyOnEnd = newDestroyOnEnd;
        index = 0;
        timer = 0f;
        frameInterval = 1f / Mathf.Max(1f, fps);

        if (frames == null || frames.Length == 0)
        {
            Finish();
            return;
        }

        spriteRenderer.sprite = frames[0];
    }

    private void Finish()
    {
        if (destroyOnEnd)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
