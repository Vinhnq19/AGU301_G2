using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonBuilder.EditorTools
{
    /// <summary>
    /// Reskin cac nut trong LobbyScene bang bo sprite pixel-art moi (Assets/Sprite/UI/Main_menu.png),
    /// va doi nen LobbyPanel sang panel cua Levels.png (ti le 116:134, gan vuong hon Main_menu de
    /// giam do lech so voi noi dung landscape). Panel duoc RESIZE theo dung ti le goc cua sprite
    /// (khong dung Sliced/stretch) de khong bi meo hinh.
    /// </summary>
    public static class LobbyKitSetup
    {
        private const string ScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string MainMenuSpriteGuid = "dab728ac2dc04654984b5f75e8c3f726";
        private const string LevelsSpriteGuid = "911bec1281ce8674699987c92008174c";

        // 4 bien the thanh nut trong (khong chu) cua bo kit
        private const long BarVariant1FileId = -3581412217481141546; // Main_menu_2
        private const long BarVariant2FileId = -1175416069785852829; // Main_menu_3
        private const long BarVariant3FileId = 2885058273434057715;  // Main_menu_4
        private const long BarVariant4FileId = -46564867914866660;   // Main_menu_5

        private const long PanelSpriteFileId = -4329440586233989439; // Levels_0: panel trong, 116x134

        private static readonly Vector2 ButtonSize = new(220f, 62f);
        private const float LabelFontSize = 26f;

        // Kich thuoc panel duoc tinh theo dung ti le 116:134 cua sprite goc (khong meo),
        // chon width du rong de chua het noi dung hien co (~823 wide x ~560 tall).
        private static readonly Vector2 PanelSize = new(900f, 900f * 134f / 116f);

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

                Sprite panelSprite = LoadSprite(PanelSpriteFileId, LevelsSpriteGuid);
                if (panelSprite == null)
                {
                    Debug.LogError("[LobbyKitSetup] Khong load duoc sprite panel tu Levels.png.");
                }
                else
                {
                    ApplyPanelSkin(panelSprite);
                }

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

        private static void ApplyPanelSkin(Sprite panelSprite)
        {
            GameObject panelGO = GameObject.Find("LobbyPanel");
            if (panelGO == null)
            {
                Debug.LogWarning("[LobbyKitSetup] Khong tim thay LobbyPanel.");
                return;
            }

            var image = panelGO.GetComponent<Image>();
            if (image == null)
            {
                Debug.LogWarning("[LobbyKitSetup] LobbyPanel khong co Image component.");
                return;
            }

            image.sprite = panelSprite;
            image.type = Image.Type.Simple;

            var rect = panelGO.GetComponent<RectTransform>();
            rect.sizeDelta = PanelSize;
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

        private static Sprite LoadSprite(long internalFileId, string textureGuid = MainMenuSpriteGuid)
        {
            string path = AssetDatabase.GUIDToAssetPath(textureGuid);
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
