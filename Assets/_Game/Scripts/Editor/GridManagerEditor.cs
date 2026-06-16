using DungeonBuilder.Building;
using UnityEditor;
using UnityEngine;

namespace DungeonBuilder.Editor
{
    /// <summary>
    /// Custom Editor cho GridManager.
    /// Tự động snap các Predefined Spot Transform về giao điểm lưới gần nhất
    /// mỗi khi chúng được di chuyển trong Scene View.
    /// Hỗ trợ đầy đủ Undo/Redo.
    /// </summary>
    [CustomEditor(typeof(GridManager))]
    public sealed class GridManagerEditor : UnityEditor.Editor
    {
        private GridManager _gridManager;

        private void OnEnable()
        {
            _gridManager = (GridManager)target;
        }

        private void OnSceneGUI()
        {
            if (_gridManager == null) return;

            SerializedObject so = new SerializedObject(_gridManager);
            SerializedProperty spotsProp = so.FindProperty("_predefinedSpotTransforms");
            SerializedProperty originProp = so.FindProperty("_origin");
            SerializedProperty cellSizeProp = so.FindProperty("_cellSize");

            if (spotsProp == null) return;

            Vector3 origin   = originProp.vector3Value;
            float   cellSize = cellSizeProp.floatValue;
            if (cellSize <= 0f) cellSize = 1f;

            bool anyChanged = false;

            for (int i = 0; i < spotsProp.arraySize; i++)
            {
                SerializedProperty element = spotsProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue is not Transform spot) continue;

                EditorGUI.BeginChangeCheck();

                // Vẽ handle di chuyển trong Scene
                Vector3 oldPos = spot.position;
                Vector3 newPos = Handles.PositionHandle(oldPos, Quaternion.identity);

                if (EditorGUI.EndChangeCheck())
                {
                    // Snap vị trí mới về giao điểm lưới gần nhất
                    Vector3 local   = newPos - origin;
                    int     gx      = Mathf.RoundToInt(local.x / cellSize);
                    int     gy      = Mathf.RoundToInt(local.y / cellSize);
                    Vector3 snapped = origin + new Vector3(gx * cellSize, gy * cellSize, oldPos.z);

                    Undo.RecordObject(spot, "Move Grid Spot");
                    spot.position = snapped;
                    EditorUtility.SetDirty(spot.gameObject);
                    anyChanged = true;
                }

                // Vẽ nhãn index để dễ nhận biết trong Scene
                Handles.Label(spot.position + Vector3.up * (cellSize * 0.6f),
                    $"Spot {i}",
                    new GUIStyle { normal = { textColor = Color.cyan }, fontSize = 11 });
            }

            if (anyChanged)
            {
                SceneView.RepaintAll();
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Kéo thả các Spot trong Scene View để tự động snap về giao điểm lưới gần nhất.\n" +
                "Hỗ trợ Undo/Redo (Ctrl+Z).\n" +
                "Gizmo: Xanh = đúng lưới | Đỏ = đang lệch.",
                MessageType.Info);

            // Nút snap toàn bộ một lần
            if (GUILayout.Button("⚡ Snap All Spots to Grid Now"))
            {
                SnapAllSpots();
            }
        }

        private void SnapAllSpots()
        {
            SerializedObject so = new SerializedObject(_gridManager);
            SerializedProperty spotsProp   = so.FindProperty("_predefinedSpotTransforms");
            SerializedProperty originProp  = so.FindProperty("_origin");
            SerializedProperty cellSizeProp = so.FindProperty("_cellSize");

            if (spotsProp == null) return;

            Vector3 origin   = originProp.vector3Value;
            float   cellSize = cellSizeProp.floatValue;
            if (cellSize <= 0f) cellSize = 1f;

            int snapped = 0;
            for (int i = 0; i < spotsProp.arraySize; i++)
            {
                SerializedProperty element = spotsProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue is not Transform spot) continue;

                Vector3 local    = spot.position - origin;
                int     gx       = Mathf.RoundToInt(local.x / cellSize);
                int     gy       = Mathf.RoundToInt(local.y / cellSize);
                Vector3 snappedP = origin + new Vector3(gx * cellSize, gy * cellSize, spot.position.z);

                if (Vector3.Distance(spot.position, snappedP) > 0.001f)
                {
                    Undo.RecordObject(spot, "Snap All Grid Spots");
                    spot.position = snappedP;
                    EditorUtility.SetDirty(spot.gameObject);
                    snapped++;
                }
            }

            if (snapped > 0)
            {
                Debug.Log($"[GridManagerEditor] Snapped {snapped} spots to grid.");
                SceneView.RepaintAll();
            }
            else
            {
                Debug.Log("[GridManagerEditor] All spots are already aligned to grid.");
            }
        }
    }
}
