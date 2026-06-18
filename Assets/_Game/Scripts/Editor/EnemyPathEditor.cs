using DungeonBuilder.Enemy;
using UnityEditor;
using UnityEngine;

namespace DungeonBuilder.Editor
{
    [CustomEditor(typeof(EnemyPath))]
    public class EnemyPathEditor : UnityEditor.Editor
    {
        private EnemyPath _path;

        private void OnEnable()
        {
            _path = (EnemyPath)target;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Các công cụ tiện ích để tạo nhanh đường đi:", MessageType.Info);

            if (GUILayout.Button("Thêm Waypoint mới vào cuối"))
            {
                AddNewWaypoint();
            }

            if (GUILayout.Button("Xoá toàn bộ Waypoint"))
            {
                if (EditorUtility.DisplayDialog("Xác nhận", "Bạn có chắc muốn xoá toàn bộ Waypoint của đường đi này?", "Xoá", "Huỷ"))
                {
                    ClearWaypoints();
                }
            }
        }

        private void OnSceneGUI()
        {
            if (_path == null || _path.Waypoints == null) return;

            Transform[] waypoints = _path.Waypoints;

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;

                // 1. Vẽ vị trí và nhãn
                Handles.color = Color.cyan;
                Vector3 pos = waypoints[i].position;
                
                // Vẽ một khối hộp nhỏ làm điểm đánh dấu
                float handleSize = HandleUtility.GetHandleSize(pos) * 0.15f;
                Handles.CubeHandleCap(0, pos, Quaternion.identity, handleSize, EventType.Repaint);
                
                // Ghi tên Waypoint
                Handles.Label(pos + Vector3.up * 0.3f, $"WP {i}", new GUIStyle() { normal = { textColor = Color.yellow }, fontSize = 12, fontStyle = FontStyle.Bold });

                // 2. Vẽ Position Handle để kéo thả dễ dàng mà không cần chọn child object
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(waypoints[i], "Move Waypoint");
                    waypoints[i].position = newPos;
                }

                // 3. Vẽ đường nối (Line) tới điểm tiếp theo
                if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
                {
                    Handles.color = Color.green;
                    Handles.DrawDottedLine(pos, waypoints[i + 1].position, 4f);
                    
                    // Vẽ hướng đi (mũi tên nhỏ)
                    Vector3 dir = (waypoints[i + 1].position - pos).normalized;
                    Vector3 midPoint = Vector3.Lerp(pos, waypoints[i + 1].position, 0.5f);
                    if (dir != Vector3.zero)
                    {
                        Handles.ArrowHandleCap(0, midPoint - dir * 0.5f, Quaternion.LookRotation(dir), handleSize * 5f, EventType.Repaint);
                    }
                }
            }
        }

        private void AddNewWaypoint()
        {
            Undo.RecordObject(_path, "Add Waypoint");

            GameObject wpObj = new GameObject($"Waypoint_{_path.Waypoints?.Length ?? 0}");
            wpObj.transform.SetParent(_path.transform);

            // Nếu đã có điểm trước đó, spawn ở điểm cuối cùng. Nếu chưa có, spawn tại chính EnemyPath.
            if (_path.Waypoints != null && _path.Waypoints.Length > 0 && _path.Waypoints[_path.Waypoints.Length - 1] != null)
            {
                wpObj.transform.position = _path.Waypoints[_path.Waypoints.Length - 1].position + Vector3.right * 1f; // Dịch sang phải 1 đơn vị
            }
            else
            {
                wpObj.transform.position = _path.transform.position;
            }

            Undo.RegisterCreatedObjectUndo(wpObj, "Add Waypoint");

            SerializedObject so = new SerializedObject(_path);
            SerializedProperty waypointsProp = so.FindProperty("_waypoints");
            
            waypointsProp.arraySize++;
            waypointsProp.GetArrayElementAtIndex(waypointsProp.arraySize - 1).objectReferenceValue = wpObj.transform;
            
            so.ApplyModifiedProperties();
            
            // Chọn luôn waypoint mới để người dùng thao tác
            Selection.activeGameObject = wpObj;
        }

        private void ClearWaypoints()
        {
            Undo.RecordObject(_path, "Clear Waypoints");

            if (_path.Waypoints != null)
            {
                foreach (Transform wp in _path.Waypoints)
                {
                    if (wp != null)
                    {
                        Undo.DestroyObjectImmediate(wp.gameObject);
                    }
                }
            }

            SerializedObject so = new SerializedObject(_path);
            SerializedProperty waypointsProp = so.FindProperty("_waypoints");
            waypointsProp.ClearArray();
            so.ApplyModifiedProperties();
        }
    }
}
