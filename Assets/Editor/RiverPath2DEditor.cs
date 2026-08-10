using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RiverPath2D))]
public sealed class RiverPath2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);
        if (!GUILayout.Button("Rebuild River Preview")) return;

        RiverPath2D river = (RiverPath2D)target;
        Undo.RecordObject(river, "Rebuild River Preview");
        river.RebuildVisual();
        EditorUtility.SetDirty(river);
    }

    private void OnSceneGUI()
    {
        RiverPath2D river = (RiverPath2D)target;
        Transform transform = river.transform;

        for (int i = 0; i < river.ControlPointCount; i++)
        {
            Vector3 worldPoint = transform.TransformPoint(river.GetControlPoint(i));
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(worldPoint, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck()) continue;

            Undo.RecordObject(river, "Move River Control Point");
            river.SetControlPoint(i, transform.InverseTransformPoint(moved));
            river.RebuildVisual();
            EditorUtility.SetDirty(river);
        }
    }
}
