using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GroundTilemapGenerator))]
public sealed class GroundTilemapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);
        if (!GUILayout.Button("Rebuild Ground Tilemap")) return;

        GroundTilemapGenerator generator = (GroundTilemapGenerator)target;
        Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Rebuild Ground Tilemap");
        generator.Rebuild();
        EditorUtility.SetDirty(generator);
    }
}
