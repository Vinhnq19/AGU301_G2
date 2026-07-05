using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonBuilder.EditorTools
{
    /// <summary>
    /// Reskin man MenuScene bang bo sprite pixel-art moi (Assets/Sprite/UI/Main_menu.png):
    /// them panel nen go/giay da, doi sprite nut Start/Quit sang cac thanh trong bo kit.
    /// </summary>
    public static class MenuKitSetup
    {
        private const string ScenePath = "Assets/Scenes/MenuScene.unity";
        private const string MainMenuSpriteGuid = "dab728ac2dc04654984b5f75e8c3f726";

        private const long PanelSpriteFileId = -733218593125905604;   // Main_menu_1: blank parchment panel
        private const long StartBarSpriteFileId = -3581412217481141546; // Main_menu_2: blank bar
        private const long QuitBarSpriteFileId = -46564867914866660;    // Main_menu_5: blank bar (variant khac mau)

        [MenuItem("Tools/Setup Menu Kit Skin")]
        public static void Setup()
        {
            Scene previousActive = SceneManager.GetActiveScene();
            bool wasAlreadyOpen = previousActive.path == ScenePath;

            Scene scene = wasAlreadyOpen
                ? previousActive
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                Button startButton = FindButtonByName("Start");
                Button quitButton = FindButtonByName("Quit");
                if (startButton == null || quitButton == null)
                {
                    Debug.LogError("[MenuKitSetup] Khong tim thay nut Start/Quit.");
                    return;
                }

                Sprite panelSprite = LoadSprite(PanelSpriteFileId);
                Sprite startSprite = LoadSprite(StartBarSpriteFileId);
                Sprite quitSprite = LoadSprite(QuitBarSpriteFileId);
                if (panelSprite == null || startSprite == null || quitSprite == null)
                {
                    Debug.LogError("[MenuKitSetup] Khong load duoc sprite tu Main_menu.png.");
                    return;
                }

                Transform parent = startButton.transform.parent;

                if (parent.Find("MenuPanelBG") == null)
                {
                    var panelGO = new GameObject("MenuPanelBG", typeof(RectTransform));
                    var panelRect = panelGO.GetComponent<RectTransform>();
                    panelRect.SetParent(parent, false);
                    panelRect.anchorMin = new Vector2(0f, 0.5f);
                    panelRect.anchorMax = new Vector2(0f, 0.5f);
                    panelRect.pivot = new Vector2(0f, 0.5f);
                    panelRect.sizeDelta = new Vector2(400f, 829f);
                    panelRect.anchoredPosition = new Vector2(280f, -75f);
                    panelGO.transform.SetAsFirstSibling();

                    var panelImage = panelGO.AddComponent<Image>();
                    panelImage.sprite = panelSprite;
                    panelImage.type = Image.Type.Simple;
                }

                ResizeButton(startButton, startSprite, new Vector2(348f, 84f));
                ResizeButton(quitButton, quitSprite, new Vector2(348f, 84f));

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[MenuKitSetup] Da reskin MenuScene bang bo sprite pixel-art moi.");
            }
            finally
            {
                if (!wasAlreadyOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ResizeButton(Button button, Sprite sprite, Vector2 size)
        {
            var image = button.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;

            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.fontSize = 36f;
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
