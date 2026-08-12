using UnityEngine;

public struct WindRuntimeValues
{
    public WindShape Shape;
    public float Power;
    public float Radius;
    public float Length;
    public float StartWidth;
    public float EndWidth;
    public int MaxTargets;
    public float Interval;
    public float SurfaceLift;
    public float TornadoInwardRatio;
    public float TornadoSpinRatio;

    public float QueryRadius
    {
        get
        {
            return Shape == WindShape.Surface
                ? Mathf.Max(Length, EndWidth)
                : Radius;
        }
    }
}
