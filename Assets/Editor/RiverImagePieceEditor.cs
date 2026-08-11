using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RiverImagePiece))]
[CanEditMultipleObjects]
public sealed class RiverImagePieceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GUILayout.Space(8f);

        if (GUILayout.Button("Scan Entry / Exit From Water Pixels"))
        {
            foreach (Object selected in targets)
            {
                RiverImagePiece piece = (RiverImagePiece)selected;
                Undo.RecordObject(piece, "Scan River Image Anchors");
                piece.ScanWaterAnchors();
                EditorUtility.SetDirty(piece);
            }
        }

        if (GUILayout.Button("Snap Selected Pieces As Sibling Chain")) SnapSelectedChain();
    }

    private void OnSceneGUI()
    {
        RiverImagePiece piece = (RiverImagePiece)target;
        Transform transform = piece.transform;

        EditorGUI.BeginChangeCheck();
        Vector3 entry = Handles.PositionHandle(piece.WorldEntry, Quaternion.identity);
        Vector3 exit = Handles.PositionHandle(piece.WorldExit, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(piece, "Move River Image Anchor");
        piece.SetAnchors(
            transform.InverseTransformPoint(entry),
            transform.InverseTransformPoint(exit),
            piece.NativeWaterWidth);
        EditorUtility.SetDirty(piece);
    }

    private static void SnapSelectedChain()
    {
        RiverImagePiece[] pieces = Selection.gameObjects
            .Select(gameObject => gameObject.GetComponent<RiverImagePiece>())
            .Where(piece => piece != null)
            .OrderBy(piece => piece.transform.GetSiblingIndex())
            .ToArray();

        if (pieces.Length < 2)
        {
            EditorUtility.DisplayDialog("River image chain", "Select at least two sibling river pieces.", "OK");
            return;
        }

        Undo.RecordObjects(pieces.Select(piece => piece.transform).ToArray(), "Snap River Image Chain");
        for (int i = 1; i < pieces.Length; i++)
        {
            Vector2 offset = pieces[i - 1].WorldExit - pieces[i].WorldEntry;
            pieces[i].transform.position += (Vector3)offset;
            EditorUtility.SetDirty(pieces[i]);
        }
    }
}
