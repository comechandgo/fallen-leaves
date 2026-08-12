using UnityEngine;

[CreateAssetMenu(menuName = "Wind/Wind Effect Library")]
public sealed class WindEffectLibrary : ScriptableObject
{
    [Header("下沉风")]
    public Sprite[] downburstFrames;

    [Header("面风")]
    public Sprite[] surfaceFrames;

    [Header("龙卷风")]
    public Sprite[] tornadoFrames;

    [Header("播放速度")]
    public float downburstFps = 12f;
    public float surfaceFps = 12f;
    public float tornadoFps = 12f;

    public Sprite[] GetFrames(WindShape form)
    {
        switch (form)
        {
            case WindShape.Downburst:
                return downburstFrames;

            case WindShape.Surface:
                return surfaceFrames;

            case WindShape.Tornado:
                return tornadoFrames;

            default:
                return downburstFrames;
        }
    }

    public float GetFps(WindShape form)
    {
        switch (form)
        {
            case WindShape.Downburst:
                return downburstFps;

            case WindShape.Surface:
                return surfaceFps;

            case WindShape.Tornado:
                return tornadoFps;

            default:
                return 12f;
        }
    }
}
