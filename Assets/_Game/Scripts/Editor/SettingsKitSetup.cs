using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.EditorTools
{
    /// <summary>
    /// Reskin SettingPanel.prefab bang bo sprite pixel-art moi.
    ///
    /// QUAN TRONG ve cau truc prefab (de tranh lap lai loi cu):
    /// - "SettingPanel" la ROOT THAT cua prefab (m_Father: 0), full-screen (anchor 0,0-1,1,
    ///   sizeDelta {0,0}), la LOP PHU MO DEN (Color {0,0,0,0.392}) dung de dim man hinh phia sau
    ///   popup. KHONG duoc doi sprite/size/color cua node nay -> neu doi se bien lop phu mo thanh
    ///   1 tam anh day dac, meo hinh, trong nhu "loi" full-screen (day chinh la bug ban dau).
    /// - "Setting Popup" la CON cua root, center-anchor, 600x500 -> day moi la CAI CARD THAT SU
    ///   hien thi Title/Slider/Nut. Day la node can doi sprite nen + resize.
    ///
    /// - Nen panel: Settings_2 (panel da ve san header "SETTINGS", icon loa/not nhac, khung SAVE/DECLINE,
    ///   100x150) resize dung ti le -> khong meo.
    /// - Nut Resume/Return: thanh trong tu Main_menu.png, dat de len dung vi tri khung SAVE/DECLINE da ve
    ///   san tren sprite (nut opaque nen che het chu SAVE/DECLINE ve san, chi con chu that cua minh).
    /// - 2/3 slider (Master, BGM) dat de len dung vi tri 2 thanh truot da ve san (icon loa/not nhac);
    ///   slider con lai (SFX) dat vao vi tri hang "Full Screen" (khong dung den vi game khong co tinh nang do).
    /// </summary>
    public static class SettingsKitSetup
    {
        private const string PrefabPath = "Assets/_Game/Generated/Prefabs/UI/SettingPanel.prefab";

        private const string SettingsSpriteGuid = "fc68b7a8f6188f2479d45c4968cb8ff7";
        private const string MainMenuSpriteGuid = "dab728ac2dc04654984b5f75e8c3f726";

        private const long PanelSpriteFileId = -5339635570483323969;  // Settings_2: panel day du (header/icon/SAVE/DECLINE), 100x150
        private const long ResumeBarFileId = -3581412217481141546;    // Main_menu_2
        private const long ReturnBarFileId = -46564867914866660;      // Main_menu_5

        // 500 x 750 = dung ti le 100:150 cua Settings_2
        private static readonly Vector2 PanelSize = new(500f, 750f);
        private static readonly Vector2 ButtonSize = new(320f, 70f);

        private static readonly Color TrackColor = new(0.77f, 0.60f, 0.42f, 1f); // nau nhat (tan)
        private static readonly Color FillColor = new(0.30f, 0.69f, 0.31f, 1f);  // xanh la cua kit

        // Gia tri goc cua lop phu mo den (root "SettingPanel"), de phuc hoi neu bi doi nham truoc do.
        private static readonly Color DimmerColor = new(0f, 0f, 0f, 0.392f);

        [MenuItem("Tools/Setup Settings Kit Skin")]
        public static void Setup()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Phuc hoi root ve dung vai tro lop phu mo den full-screen (khong phai card).
                var rootImage = root.GetComponent<Image>();
                if (rootImage != null)
                {
                    rootImage.color = DimmerColor;
                    rootImage.type = Image.Type.Sliced;
                }
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = Vector2.zero;

                Transform panel = FindDeep(root.transform, "Setting Popup");
                if (panel == null)
                {
                    Debug.LogError("[SettingsKitSetup] Khong tim thay node Setting Popup.");
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

                // Settings_2 da co san chu "SETTINGS" ve trong header -> an title rieng cua minh de
                // khong bi chong chu.
                Transform title = FindDeep(panel, "SettingTitle");
                if (title != null)
                {
                    title.gameObject.SetActive(false);
                }

                // Can slider vao dung vi tri da ve san tren sprite (icon loa/not nhac).
                // SFX khong co hang ve san rieng -> dat vao vi tri hang "Full Screen" (khong dung den).
                MoveRow(panel, "MasterVolumeSlider", new Vector2(20f, 275f), 380f);
                MoveRow(panel, "BGMVolumeSlider", new Vector2(20f, 187f), 380f);
                MoveRow(panel, "SFXVolumeSlider", new Vector2(20f, 112f), 380f);

                TintSlider(panel, "MasterVolumeSlider");
                TintSlider(panel, "BGMVolumeSlider");
                TintSlider(panel, "SFXVolumeSlider");

                // Can nut de len dung khung SAVE/DECLINE da ve san (nut opaque che het chu ve san).
                ReskinButton(panel, "ResumeBtn", resumeSprite, new Vector2(0f, -206f));
                ReskinButton(panel, "ReturnToLobbyBtn", returnSprite, new Vector2(0f, -275f));

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[SettingsKitSetup] Da reskin SettingPanel bang bo sprite pixel-art moi.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void MoveRow(Transform panel, string rowName, Vector2 position, float width = 400f)
        {
            Transform row = FindDeep(panel, rowName);
            if (row == null)
            {
                Debug.LogWarning($"[SettingsKitSetup] Khong tim thay '{rowName}'.");
                return;
            }

            var rect = row.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 40f);
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
