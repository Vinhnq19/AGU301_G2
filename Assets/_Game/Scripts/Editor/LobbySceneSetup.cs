using DungeonBuilder.Networking.Lobby;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonBuilder.EditorTools
{
    /// <summary>
    /// Them nut "Back to Menu" vao LobbyScene bang cach nhan ban DisconnectButton
    /// (giu nguyen style/skin co san) roi gan vao LobbyView.
    /// </summary>
    public static class LobbySceneSetup
    {
        private const string ScenePath = "Assets/Scenes/LobbyScene.unity";

        [MenuItem("Tools/Setup Lobby Back To Menu Button")]
        public static void Setup()
        {
            Scene previousActive = SceneManager.GetActiveScene();
            bool wasAlreadyOpen = previousActive.path == ScenePath;

            Scene scene = wasAlreadyOpen
                ? previousActive
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                var view = Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include);
                if (view == null)
                {
                    Debug.LogError("[LobbySceneSetup] Khong tim thay LobbyView trong scene.");
                    return;
                }

                var so = new SerializedObject(view);
                var backBtnProp = so.FindProperty("_backToMenuButton");
                if (backBtnProp.objectReferenceValue != null)
                {
                    Debug.Log("[LobbySceneSetup] Da co Back to Menu button, bo qua.");
                    return;
                }

                var disconnectBtnProp = so.FindProperty("_disconnectButton");
                var disconnectButton = disconnectBtnProp.objectReferenceValue as Button;
                if (disconnectButton == null)
                {
                    Debug.LogError("[LobbySceneSetup] Khong tim thay DisconnectButton de nhan ban.");
                    return;
                }

                GameObject clone = Object.Instantiate(disconnectButton.gameObject, disconnectButton.transform.parent);
                clone.name = "BackToMenuButton";

                var rect = clone.GetComponent<RectTransform>();
                var sourceRect = disconnectButton.GetComponent<RectTransform>();
                rect.anchorMin = sourceRect.anchorMin;
                rect.anchorMax = sourceRect.anchorMax;
                rect.pivot = sourceRect.pivot;
                rect.sizeDelta = sourceRect.sizeDelta;
                rect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -60f);

                var image = clone.GetComponent<Image>();
                if (image != null) image.color = new Color(0.35f, 0.35f, 0.4f, 1f);

                var label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = "Back to Menu";

                // Clone giu nguyen onClick persistent calls (rong) tu Disconnect, khong can xoa gi them.

                var newButton = clone.GetComponent<Button>();
                backBtnProp.objectReferenceValue = newButton;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[LobbySceneSetup] Da them nut Back to Menu va gan vao LobbyView.");
            }
            finally
            {
                if (!wasAlreadyOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
