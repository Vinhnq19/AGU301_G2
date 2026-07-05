using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.EditorTools
{
    /// <summary>
    /// Reskin SettingPanel.prefab bang bo sprite pixel-art moi:
    /// - Nen panel: Settings_0 (panel trong co header xanh, 100x150) resize dung ti le -> khong meo.
    /// - Nut Resume/Return: thanh trong tu Main_menu.png (dong bo voi MainMenu/Lobby).
    /// - Slider: giu cau truc Unity, tint mau nau/xanh theo tong cua kit.
    /// Bo cuc doi tu landscape 600x500 sang portrait 500x750 cho khop panel.
    /// </summary>
    public static class SettingsKitSetup
    {
        private const string PrefabPath = "Assets/_Game/Generated/Prefabs/UI/SettingPanel.prefab";

        private const string SettingsSpriteGuid = "fc68b7a8f6188f2479d45c4968cb8ff7";
        private const string MainMenuSpriteGuid = "dab728ac2dc04654984b5f75e8c3f726";

        private const long PanelSpriteFileId = 7886631795410796208;   // Settings_0: panel + header xanh (100x150)
        private const long ResumeBarFileId = -3581412217481141546;    // Main_menu_2
        private const long ReturnBarFileId = -46564867914866660;      // Main_menu_5

        // 500 x 750 = dung ti le 100:150 cua Settings_0
        private static readonly Vector2 PanelSize = new(500f, 750f);
        private static readonly Vector2 ButtonSize = new(340f, 76f);

        private static readonly Color TrackColor = new(0.77f, 0.60f, 0.42f, 1f); // nau nhat (tan)
        private static readonly Color FillColor = new(0.30f, 0.69f, 0.31f, 1f);  // xanh la cua kit

        [MenuItem("Tools/Setup Settings Kit Skin")]
        public static void Setup()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform panel = FindDeep(root.transform, "SettingPanel");
                if (panel == null)
                {
                    Debug.LogError("[SettingsKitSetup] Khong tim thay node SettingPanel.");
                    return;
                }

                Sprite panelSprite = LoadSprite(SettingsSpriteGuid, PanelSpriteFileId);
                Sprite resumeSprite = LoadSprite(MainMenuSpriteGuid, ResumeBarFileId);
                Sprite returnSprite = LoadSprite(MainMenuSpriteGuid, ReturnBarFileId);
                if (panelSprite == null || resumeSprite == null || returnSprite == null)
                {
                    Debug.LogError("[SettingsKitSetup] Khong load duoc sprite tu bo kit.");
                    return;
                }

                // Nen panel: doi sprite + resize dung ti le portrait
                var panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.sprite = panelSprite;
                    panelImage.type = Image.Type.Simple;
                    panelImage.color = Color.white;
                }
                panel.GetComponent<RectTransform>().sizeDelta = PanelSize;

                // Title vao vung header xanh tren cung
                Transform title = FindDeep(panel, "SettingTitle");
                if (title != null)
                {
                    var titleRect = title.GetComponent<RectTransform>();
                    titleRect.anchorMin = new Vector2(0.5f, 1f);
                    titleRect.anchorMax = new Vector2(0.5f, 1f);
                    titleRect.pivot = new Vector2(0.5f, 1f);
                    titleRect.sizeDelta = new Vector2(300f, 50f);
                    titleRect.anchoredPosition = new Vector2(0f, -8f);
                }

                // 3 hang slider o giua panel
                MoveRow(panel, "MasterVolumeSlider", new Vector2(0f, 150f));
                MoveRow(panel, "BGMVolumeSlider", new Vector2(0f, 60f));
                MoveRow(panel, "SFXVolumeSlider", new Vector2(0f, -30f));

                TintSlider(panel, "MasterVolumeSlider");
                TintSlider(panel, "BGMVolumeSlider");
                TintSlider(panel, "SFXVolumeSlider");

                // 2 nut xuong duoi
                ReskinButton(panel, "ResumeBtn", resumeSprite, new Vector2(0f, -150f));
                ReskinButton(panel, "ReturnToLobbyBtn", returnSprite, new Vector2(0f, -250f));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[SettingsKitSetup] Da reskin SettingPanel bang bo sprite pixel-art moi.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void MoveRow(Transform panel, string rowName, Vector2 position)
        {
            Transform row = FindDeep(panel, rowName);
            if (row == null)
            {
                Debug.LogWarning($"[SettingsKitSetup] Khong tim thay '{rowName}'.");
                return;
            }

            var rect = row.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400f, 40f);
            rect.anchoredPosition = position;
        }

        private static void TintSlider(Transform panel, string rowName)
        {
            Transform row = FindDeep(panel, rowName);
            if (row == null) return;

            Transform background = FindDeep(row, "Background");
            if (background != null)
            {
                var img = background.GetComponent<Image>();
                if (img != null) img.color = TrackColor;
            }

            Transform fill = FindDeep(row, "Fill");
            if (fill != null)
            {
                var img = fill.GetComponent<Image>();
                if (img != null) img.color = FillColor;
            }
        }

        private static void ReskinButton(Transform panel, string buttonName, Sprite sprite, Vector2 position)
        {
            Transform btn = FindDeep(panel, buttonName);
            if (btn == null)
            {
                Debug.LogWarning($"[SettingsKitSetup] Khong tim thay '{buttonName}'.");
                return;
            }

            var image = btn.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.color = Color.white;
            }

            var rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ButtonSize;
            rect.anchoredPosition = position;

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.fontSize = 26f;
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
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
