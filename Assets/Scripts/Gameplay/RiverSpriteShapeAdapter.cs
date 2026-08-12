using UnityEngine;
using UnityEngine.U2D;

[DisallowMultipleComponent]
[RequireComponent(typeof(RiverImagePiece))]
public sealed class RiverSpriteShapeAdapter : MonoBehaviour
{
    private const string FillTextureResourcePath = "RiverSpriteShapeFill";
    private const int SortingOrderOffset = 1;

    [SerializeField] private RiverImagePiece riverPiece;
    [SerializeField] private Texture2D fillTexture;
    [SerializeField, Range(0.65f, 1.25f)] private float widthScale = 0.92f;
    [SerializeField, Range(0f, 0.5f)] private float sidePadding = 0.18f;
    [SerializeField] private bool hideSourceRenderer = true;

    private GameObject shapeObject;
    private SpriteShape spriteShapeProfile;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            BuildRuntimeShape();
        }
    }

    public void BuildRuntimeShape()
    {
        if (shapeObject != null)
        {
            return;
        }

        if (riverPiece == null)
        {
            riverPiece = GetComponent<RiverImagePiece>();
        }

        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();
        if (riverPiece == null || sourceRenderer == null)
        {
            return;
        }

        if (fillTexture == null)
        {
            fillTexture = Resources.Load<Texture2D>(FillTextureResourcePath);
        }

        shapeObject = new GameObject("RiverSpriteShapeRuntime");
        shapeObject.layer = gameObject.layer;
        shapeObject.transform.SetParent(transform, false);

        SpriteShapeController controller = shapeObject.AddComponent<SpriteShapeController>();
        SpriteShapeRenderer renderer = shapeObject.GetComponent<SpriteShapeRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder + SortingOrderOffset;
            renderer.color = new Color(1f, 1f, 1f, 0.92f);
            if (sourceRenderer.sharedMaterial != null)
            {
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }
        }

        spriteShapeProfile = ScriptableObject.CreateInstance<SpriteShape>();
        spriteShapeProfile.name = "RuntimeRiverSpriteShape";
        spriteShapeProfile.fillTexture = fillTexture;
        controller.spriteShape = spriteShapeProfile;
        controller.splineDetail = (int)QualityDetail.High;
        controller.fillPixelsPerUnit = 48f;
        controller.worldSpaceUVs = true;

        ConfigureSpline(controller.spline, riverPiece.EntryAnchor, riverPiece.ExitAnchor, riverPiece.NativeWaterWidth);
        controller.RefreshSpriteShape();

        if (hideSourceRenderer)
        {
            sourceRenderer.enabled = false;
        }
    }

    private void ConfigureSpline(Spline spline, Vector2 entry, Vector2 exit, float nativeWidth)
    {
        spline.Clear();
        spline.isOpenEnded = false;

        Vector2 path = exit - entry;
        if (path.sqrMagnitude <= 0.001f)
        {
            path = Vector2.right;
        }

        Vector2 forward = path.normalized;
        Vector2 side = new Vector2(-forward.y, forward.x);
        float width = Mathf.Max(0.2f, nativeWidth * widthScale);
        float padding = width * sidePadding;

        Vector2 p0 = entry + side * (width * 0.5f + padding);
        Vector2 p1 = exit + side * (width * 0.5f);
        Vector2 p2 = exit - side * (width * 0.5f);
        Vector2 p3 = entry - side * (width * 0.5f + padding);

        spline.InsertPointAt(0, p0);
        spline.InsertPointAt(1, p1);
        spline.InsertPointAt(2, p2);
        spline.InsertPointAt(3, p3);

        for (int i = 0; i < spline.GetPointCount(); i++)
        {
            spline.SetTangentMode(i, ShapeTangentMode.Broken);
            spline.SetLeftTangent(i, Vector3.zero);
            spline.SetRightTangent(i, Vector3.zero);
            spline.SetHeight(i, width);
        }

        Vector3 tangent = forward * path.magnitude * 0.28f;
        spline.SetRightTangent(0, tangent);
        spline.SetLeftTangent(1, -tangent);
        spline.SetRightTangent(2, -tangent);
        spline.SetLeftTangent(3, tangent);
    }

    private void OnDestroy()
    {
        if (spriteShapeProfile != null)
        {
            Destroy(spriteShapeProfile);
        }
    }
}
