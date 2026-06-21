#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonBuilder.Editor
{
    public static class ResourceNodePlacer
    {
        private const string PrefabRoot = "Assets/_Game/Generated/Prefabs/Harvesting";

        [MenuItem("Dungeon Builder/Place Missing Resource Nodes")]
        public static void PlaceMissingNodes()
        {
            var nodesParent = GameObject.Find("DB_ResourceNodes");
            if (nodesParent == null)
            {
                Debug.LogError("[ResourceNodePlacer] 'DB_ResourceNodes' not found in scene. Open SampleScene first.");
                return;
            }

            RemoveMisplacedCrystalNode(nodesParent.transform);

            // Khu trung (X: 33–47, Y: 22–36)
            PlaceNodes(nodesParent.transform, $"{PrefabRoot}/DB_Copper.prefab", new Vector3[]
            {
                new(34, 29, 0), new(38, 32, 0), new(42, 22, 0),
                new(44, 34, 0), new(47, 27, 0), new(35, 36, 0),
            });

            PlaceNodes(nodesParent.transform, $"{PrefabRoot}/DB_IronNode.prefab", new Vector3[]
            {
                new(33, 25, 0), new(37, 22, 0), new(40, 35, 0),
                new(43, 26, 0), new(46, 33, 0), new(34, 33, 0),
            });

            // Khu hiếm (X: 33–46, Y: 63–78)
            PlaceNodes(nodesParent.transform, $"{PrefabRoot}/DB_BlueGemNode.prefab", new Vector3[]
            {
                new(37, 77, 0), new(42, 63, 0), new(46, 70, 0),
                new(33, 63, 0), new(40, 73, 0),
            });

            PlaceNodes(nodesParent.transform, $"{PrefabRoot}/DB_PurleGem.prefab", new Vector3[]
            {
                new(35, 77, 0), new(39, 67, 0), new(43, 78, 0),
                new(46, 65, 0), new(33, 70, 0),
            });

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[ResourceNodePlacer] Done. Ctrl+S to save the scene.");
        }

        private static void RemoveMisplacedCrystalNode(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (!child.name.Contains("Crystal")) continue;
                if (Mathf.Abs(child.localPosition.x - 4f) > 0.1f ||
                    Mathf.Abs(child.localPosition.y - 2f) > 0.1f) continue;

                Undo.DestroyObjectImmediate(child.gameObject);
                Debug.Log("[ResourceNodePlacer] Removed misplaced CrystalNode at (4, 2, 0).");
                return;
            }
        }

        private static void PlaceNodes(Transform parent, string prefabPath, Vector3[] positions)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[ResourceNodePlacer] Prefab not found: {prefabPath}");
                return;
            }

            if (AlreadyPlaced(parent, prefab))
            {
                Debug.Log($"[ResourceNodePlacer] Skipped {prefab.name} — already placed.");
                return;
            }

            foreach (var pos in positions)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(go, $"Place {prefab.name}");
                go.transform.SetParent(parent);
                go.transform.localPosition = pos;
            }

            Debug.Log($"[ResourceNodePlacer] Placed {positions.Length}x {prefab.name}.");
        }

        private static bool AlreadyPlaced(Transform parent, GameObject prefab)
        {
            foreach (Transform child in parent)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == prefab)
                    return true;
            }
            return false;
        }
    }
}
#endif
