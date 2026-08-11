using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GroundTilemapGenerator))]
public sealed class GroundTilemapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);
        GroundTilemapGenerator generator = (GroundTilemapGenerator)target;
        generator.CountTiles(out int greenCount, out int yellowCount);
        EditorGUILayout.HelpBox(
            $"Saved tiles: green {greenCount}, yellow {yellowCount}. Rebuilding changes the saved prefab Tilemap.",
            MessageType.Info);

        if (!GUILayout.Button("Rebuild Mixed Ground Tilemap")) return;

        Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Rebuild Ground Tilemap");
        generator.Rebuild();
        EditorUtility.SetDirty(generator);
    }
}
