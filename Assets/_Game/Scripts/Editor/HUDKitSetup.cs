using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonBuilder.EditorTools
{
    /// <summary>
    /// Sua icon Wood dang bi broken reference (guid khong ton tai), va them panel nen
    /// phia sau khu tai nguyen (HUDContainer) trong SampleScene.
    ///
    /// Luu y: 9/10 icon con lai (Stone, Iron, Copper, BlueGem, PurpleGem, Coin, Token,
    /// MiningSkill, ForgingSkill) da co san icon rieng phu hop (Assets/Sprite/Resource/,
    /// Dungeon Pack, Bloomseed pack) - KHONG dong den cac icon nay.
    /// </summary>
    public static class HUDKitSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string IconsSpriteGuid = "736e5f24cddccbe4d96f13185a991f8c";
        private const string MainMenuSpriteGuid = "dab728ac2dc04654984b5f75e8c3f726";

        private const long WoodIconFileId = -5297615220031758764;   // Icons_82: wood plank
        private const long PanelSpriteFileId = -733218593125905604; // Main_menu_1: blank parchment panel (82x170)

        // 260 x 539 = dung ti le 82:170 cua Main_menu_1 (khong meo)
        private static readonly Vector2 PanelSize = new(260f, 539f);

        [MenuItem("Tools/Setup HUD Kit Skin")]
        public static void Setup()
        {
            Scene previousActive = SceneManager.GetActiveScene();
            bool wasAlreadyOpen = previousActive.path == ScenePath;

            Scene scene = wasAlreadyOpen
                ? previousActive
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                GameObject hudContainerGO = GameObject.Find("HUDContainer");
                if (hudContainerGO == null)
                {
                    Debug.LogError("[HUDKitSetup] Khong tim thay HUDContainer.");
                    return;
                }

                Sprite woodIcon = LoadSprite(IconsSpriteGuid, WoodIconFileId);
                Sprite panelSprite = LoadSprite(MainMenuSpriteGuid, PanelSpriteFileId);
                if (woodIcon == null || panelSprite == null)
                {
                    Debug.LogError("[HUDKitSetup] Khong load duoc sprite tu bo kit.");
                    return;
                }

                // Sua icon Wood dang bi broken reference
                GameObject woodTitleGO = GameObject.Find("WoodTitle");
                if (woodTitleGO != null)
                {
                    var woodImage = woodTitleGO.GetComponent<Image>();
                    if (woodImage != null)
                    {
                        woodImage.sprite = woodIcon;
                        woodImage.color = Color.white;
                    }
                }
                else
                {
                    Debug.LogWarning("[HUDKitSetup] Khong tim thay WoodTitle.");
                }

                // Them panel nen phia sau HUDContainer (khong doi cac icon khac)
                RectTransform hudRect = hudContainerGO.GetComponent<RectTransform>();
                Transform canvasParent = hudRect.parent;
                if (canvasParent.Find("HUDPanelBG") == null)
                {
                    var panelGO = new GameObject("HUDPanelBG", typeof(RectTransform));
                    var panelRect = panelGO.GetComponent<RectTransform>();
                    panelRect.SetParent(canvasParent, false);
                    panelRect.anchorMin = hudRect.anchorMin;
                    panelRect.anchorMax = hudRect.anchorMax;
                    panelRect.pivot = hudRect.pivot;
                    panelRect.sizeDelta = PanelSize;
                    // Can giua theo truc x quanh vung noi dung thuc te cua grid (~123 wide),
                    // giu nguyen truc y (163) de can giua theo chieu doc.
                    panelRect.anchoredPosition = new Vector2(hudRect.anchoredPosition.x - 68f, hudRect.anchoredPosition.y);
                    panelGO.transform.SetAsFirstSibling();

                    var panelImage = panelGO.AddComponent<Image>();
                    panelImage.sprite = panelSprite;
                    panelImage.type = Image.Type.Simple;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[HUDKitSetup] Da sua icon Wood va them panel nen cho HUD.");
            }
            finally
            {
                if (!wasAlreadyOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static Sprite LoadSprite(string textureGuid, long internalFileId)
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
