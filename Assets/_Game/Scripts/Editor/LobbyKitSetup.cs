using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonBuilder.EditorTools
{
    /// <summary>
    /// Reskin cac nut trong LobbyScene bang bo sprite pixel-art moi (Assets/Sprite/UI/Main_menu.png).
    /// LobbyPanel dang la landscape (872x600) trong khi cac panel trong bo kit deu la portrait,
    /// nen chi doi sprite nut, khong ep panel nen theo kit de tranh bi meo hinh.
    /// </summary>
    public static class LobbyKitSetup
    {
        private const string ScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string MainMenuSpriteGuid = "dab728ac2dc04654984b5f75e8c3f726";

        // 4 bien the thanh nut trong (khong chu) cua bo kit
        private const long BarVariant1FileId = -3581412217481141546; // Main_menu_2
        private const long BarVariant2FileId = -1175416069785852829; // Main_menu_3
        private const long BarVariant3FileId = 2885058273434057715;  // Main_menu_4
        private const long BarVariant4FileId = -46564867914866660;   // Main_menu_5

        private static readonly Vector2 ButtonSize = new(220f, 62f);
        private const float LabelFontSize = 26f;

        [MenuItem("Tools/Setup Lobby Kit Skin")]
        public static void Setup()
        {
            Scene previousActive = SceneManager.GetActiveScene();
            bool wasAlreadyOpen = previousActive.path == ScenePath;

            Scene scene = wasAlreadyOpen
                ? previousActive
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                Sprite variant1 = LoadSprite(BarVariant1FileId);
                Sprite variant2 = LoadSprite(BarVariant2FileId);
                Sprite variant3 = LoadSprite(BarVariant3FileId);
                Sprite variant4 = LoadSprite(BarVariant4FileId);
                if (variant1 == null || variant2 == null || variant3 == null || variant4 == null)
                {
                    Debug.LogError("[LobbyKitSetup] Khong load duoc sprite tu Main_menu.png.");
                    return;
                }

                ResizeButton("HostButton", variant1);
                ResizeButton("JoinButton", variant2);
                ResizeButton("StartButton", variant3);
                ResizeButton("DisconnectButton", variant4);
                ResizeButton("BackToMenuButton", variant4);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[LobbyKitSetup] Da reskin cac nut trong LobbyScene bang bo sprite pixel-art moi.");
            }
            finally
            {
                if (!wasAlreadyOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ResizeButton(string gameObjectName, Sprite sprite)
        {
            Button button = FindButtonByName(gameObjectName);
            if (button == null)
            {
                Debug.LogWarning($"[LobbyKitSetup] Khong tim thay nut '{gameObjectName}'.");
                return;
            }

            var image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;

            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = ButtonSize;

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.fontSize = LabelFontSize;
            }
        }

        private static Button FindButtonByName(string gameObjectName)
        {
            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button.gameObject.name == gameObjectName)
                {
                    return button;
                }
            }
            return null;
        }

        private static Sprite LoadSprite(long internalFileId)
        {
            string path = AssetDatabase.GUIDToAssetPath(MainMenuSpriteGuid);
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj is Sprite sprite &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long localId) &&
                    localId == internalFileId)
                {
                    return sprite;
                }
            }
            return null;
        }
    }
}
