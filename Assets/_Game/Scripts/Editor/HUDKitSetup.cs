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

        // 232 x 480 = dung ti le 82:170 cua Main_menu_1 (khong meo)
        private static readonly Vector2 PanelSize = new(232f, 480f);

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

                // Them panel nen phia sau HUDContainer (khong doi cac icon khac).
                // HUDContainer dung GridLayoutGroup (2 cot, cell 50x50, spacing.x=23.2, 10 hang)
                // nen noi dung THAT SU tran ra ngoai RectTransform khai bao (100x100) cua no,
                // bat dau tu MEP TREN cua rect (khong phai tam) va keo dai xuong duoi.
                RectTransform hudRect = hudContainerGO.GetComponent<RectTransform>();
                const float contentWidth = 123.2f;  // 2*50 + 23.2
                const float contentHeight = 500f;   // 10 hang * 50
                float contentTop = hudRect.anchoredPosition.y + hudRect.sizeDelta.y * (1f - hudRect.pivot.y);
                float contentBottom = contentTop - contentHeight;
                float contentCenterY = (contentTop + contentBottom) / 2f;
                float contentCenterX = hudRect.anchoredPosition.x + contentWidth / 2f;

                Transform canvasParent = hudRect.parent;
                Transform existingPanel = canvasParent.Find("HUDPanelBG");
                GameObject panelGO = existingPanel != null ? existingPanel.gameObject : null;
                if (panelGO == null)
                {
                    panelGO = new GameObject("HUDPanelBG", typeof(RectTransform));
                    panelGO.transform.SetParent(canvasParent, false);
                    panelGO.transform.SetAsFirstSibling();
                }

                var panelRect = panelGO.GetComponent<RectTransform>();
                panelRect.anchorMin = hudRect.anchorMin;
                panelRect.anchorMax = hudRect.anchorMax;
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = PanelSize;
                panelRect.anchoredPosition = new Vector2(contentCenterX, contentCenterY);

                var panelImage = panelGO.GetComponent<Image>();
                if (panelImage == null) panelImage = panelGO.AddComponent<Image>();
                panelImage.sprite = panelSprite;
                panelImage.type = Image.Type.Simple;

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
