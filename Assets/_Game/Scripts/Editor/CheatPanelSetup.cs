using System.IO;
using DungeonBuilder.UI.Cheat;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.Editor
{
    /// <summary>
    /// Tao prefab CheatPanel (mo bang mat ma trong chat, xem ChatView._cheatCode)
    /// voi style hien dai: nen toi bo goc, accent tim, nut hover doi mau, drop shadow.
    /// Sprite bo goc duoc generate tu dong (9-slice) vao Assets/_Game/Generated/UI/.
    /// Cau truc: root "CheatPanel" (CheatPanelView, luon active) -> child "Panel" (visual, tat/bat).
    /// Run via menu: Tools > Cheat > Create Cheat Panel Prefab
    /// </summary>
    public static class CheatPanelSetup
    {
        private const string PrefabPath = "Assets/_Game/Generated/Prefabs/UI/CheatPanel.prefab";
        private const string RoundedSpritePath = "Assets/_Game/Generated/UI/RoundedRect16.png";

        // Palette (dark modern)
        private static readonly Color PanelBg = new Color32(23, 26, 36, 250);      // #171A24
        private static readonly Color PanelOutline = new Color32(58, 64, 92, 255); // #3A405C
        private static readonly Color Accent = new Color32(124, 108, 255, 255);    // #7C6CFF
        private static readonly Color AccentPressed = new Color32(88, 74, 200, 255);
        private static readonly Color ButtonBg = new Color32(35, 40, 56, 255);     // #232838
        private static readonly Color TextMain = new Color32(232, 234, 246, 255);  // #E8EAF6
        private static readonly Color TextMuted = new Color32(139, 144, 168, 255); // #8B90A8
        private static readonly Color DangerHover = new Color32(255, 92, 92, 255); // #FF5C5C

        [MenuItem("Tools/Cheat/Create Cheat Panel Prefab")]
        public static void CreateCheatPanelPrefab()
        {
            Sprite rounded = GetOrCreateRoundedSprite();

            // Root: khong co visual, giu CheatPanelView luon active de nhan lenh Show() tu ChatView.
            // Stretch full man hinh de con dat duoc cac phan tu neo theo canh/goc man hinh.
            var root = new GameObject("CheatPanel");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var view = root.AddComponent<CheatPanelView>();

            // Visual root (bat/tat ca cum): container full man hinh, khong co graphic.
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);
            var visualRect = visualRoot.AddComponent<RectTransform>();
            visualRect.anchorMin = Vector2.zero;
            visualRect.anchorMax = Vector2.one;
            visualRect.offsetMin = Vector2.zero;
            visualRect.offsetMax = Vector2.zero;

            // Card: panel bo goc + shadow + outline, neo giua canh PHAI man hinh.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(visualRoot.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-20f, 0f);
            panelRect.sizeDelta = new Vector2(430f, 660f);
            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.pixelsPerUnitMultiplier = 1.15f;
            panelImage.color = PanelBg;

            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(PanelOutline.r, PanelOutline.g, PanelOutline.b, 0.35f);
            outline.effectDistance = new Vector2(1f, -1f);

            // Thanh accent mong tren dinh panel.
            var accentBar = new GameObject("AccentBar");
            accentBar.transform.SetParent(panel.transform, false);
            var accentRect = accentBar.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.anchoredPosition = new Vector2(0f, -9f);
            accentRect.sizeDelta = new Vector2(-170f, 5f);
            var accentImage = accentBar.AddComponent<Image>();
            accentImage.sprite = rounded;
            accentImage.type = Image.Type.Sliced;
            accentImage.pixelsPerUnitMultiplier = 8f;
            accentImage.color = Accent;
            accentImage.raycastTarget = false;

            // Title
            var title = CreateLabel(panel.transform, "Title", "CHEAT MENU", 30f, FontStyles.Bold, TextMain);
            title.characterSpacing = 6f;
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -24f);
            titleRect.sizeDelta = new Vector2(0f, 40f);

            var subtitle = CreateLabel(panel.transform, "Subtitle", "developer tools", 15f, FontStyles.Italic, TextMuted);
            var subtitleRect = subtitle.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0f, 1f);
            subtitleRect.anchorMax = new Vector2(1f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.anchoredPosition = new Vector2(0f, -64f);
            subtitleRect.sizeDelta = new Vector2(0f, 22f);

            // Close (x) tron o goc.
            var closeButton = CreateCloseButton(panel.transform, rounded);

            // Button list container.
            var list = new GameObject("Buttons");
            list.transform.SetParent(panel.transform, false);
            var listRect = list.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.offsetMin = new Vector2(26f, 26f);
            listRect.offsetMax = new Vector2(-26f, -100f);
            var layout = list.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;

            CreateSectionLabel(list.transform, "TAI NGUYEN");
            var addBasic = CreateCheatButton(list.transform, rounded, "AddBasicButton", "+500 Wood / Stone / Ore / Crystal");
            var addRare = CreateCheatButton(list.transform, rounded, "AddRareButton", "+500 Copper / Iron / Gems / Coin");

            CreateSectionLabel(list.transform, "NGUOI CHOI");
            var fullHeal = CreateCheatButton(list.transform, rounded, "FullHealButton", "Hoi day mau");
            var kill = CreateCheatButton(list.transform, rounded, "KillButton", "Tu sat (test respawn)", danger: true);

            CreateSectionLabel(list.transform, "WAVE");
            var reloadWaves = CreateCheatButton(list.transform, rounded, "ReloadWavesButton", "Reload Waves (JSON)");
            var (jumpInput, jumpButton) = CreateJumpWaveRow(list.transform, rounded);

            // Wire serialized fields
            var so = new SerializedObject(view);
            so.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeButton;
            so.FindProperty("_addBasicResourcesButton").objectReferenceValue = addBasic;
            so.FindProperty("_addRareResourcesButton").objectReferenceValue = addRare;
            so.FindProperty("_fullHealButton").objectReferenceValue = fullHeal;
            so.FindProperty("_killPlayerButton").objectReferenceValue = kill;
            so.FindProperty("_reloadWavesButton").objectReferenceValue = reloadWaves;
            so.FindProperty("_jumpWaveInput").objectReferenceValue = jumpInput;
            so.FindProperty("_jumpWaveButton").objectReferenceValue = jumpButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Debug.Log("[CheatPanelSetup] CheatPanel prefab created at " + PrefabPath);
        }

        private static Button CreateCloseButton(Transform parent, Sprite rounded)
        {
            var go = new GameObject("CloseButton");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-14f, -14f);
            rect.sizeDelta = new Vector2(38f, 38f);

            var image = go.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.8f;
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.06f);
            colors.highlightedColor = DangerHover;
            colors.pressedColor = new Color(DangerHover.r * 0.7f, DangerHover.g * 0.7f, DangerHover.b * 0.7f, 1f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var label = CreateLabel(go.transform, "Text", "×", 26f, FontStyles.Bold, TextMuted); // ×
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            return button;
        }

        private static void CreateSectionLabel(Transform parent, string text)
        {
            var label = CreateLabel(parent, "Section_" + text, text, 14f, FontStyles.Bold, TextMuted);
            label.characterSpacing = 10f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var rect = label.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 30f);
            var layoutElem = label.gameObject.AddComponent<LayoutElement>();
            layoutElem.minHeight = 30f;
        }

        private static Button CreateCheatButton(Transform parent, Sprite rounded, string name, string label, bool danger = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 56f);
            var layoutElem = go.AddComponent<LayoutElement>();
            layoutElem.minHeight = 56f;

            var image = go.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.6f;
            image.color = Color.white;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = ButtonBg;
            colors.highlightedColor = danger ? DangerHover : Accent;
            colors.pressedColor = danger
                ? new Color(DangerHover.r * 0.65f, DangerHover.g * 0.65f, DangerHover.b * 0.65f, 1f)
                : AccentPressed;
            colors.selectedColor = ButtonBg;
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var text = CreateLabel(go.transform, "Text", label, 18f, FontStyles.Normal, TextMain);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return button;
        }

        /// <summary>
        /// Hàng "Jump to wave": input số bên trái + nút "Jump" bên phải, cùng 1 dòng 56px.
        /// </summary>
        private static (TMP_InputField input, Button button) CreateJumpWaveRow(Transform parent, Sprite rounded)
        {
            var row = new GameObject("JumpWaveRow");
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, 56f);
            var rowLayoutElem = row.AddComponent<LayoutElement>();
            rowLayoutElem.minHeight = 56f;

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.spacing = 10f;

            // --- Input field (TMP) ---
            var inputGo = new GameObject("JumpWaveInput");
            inputGo.transform.SetParent(row.transform, false);
            inputGo.AddComponent<RectTransform>();
            var inputLayoutElem = inputGo.AddComponent<LayoutElement>();
            inputLayoutElem.preferredWidth = 150f;
            inputLayoutElem.minHeight = 56f;

            var inputImage = inputGo.AddComponent<Image>();
            inputImage.sprite = rounded;
            inputImage.type = Image.Type.Sliced;
            inputImage.pixelsPerUnitMultiplier = 1.6f;
            inputImage.color = ButtonBg;

            var inputField = inputGo.AddComponent<TMP_InputField>();
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Viewport + text con theo cấu trúc chuẩn của TMP_InputField.
            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputGo.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(12f, 6f);
            textAreaRect.offsetMax = new Vector2(-12f, -6f);
            textArea.AddComponent<RectMask2D>();

            var placeholder = CreateLabel(textArea.transform, "Placeholder", "wave...", 17f, FontStyles.Italic, TextMuted);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            var placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;

            var inputText = CreateLabel(textArea.transform, "Text", "", 18f, FontStyles.Normal, TextMain);
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            var inputTextRect = inputText.GetComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = Vector2.zero;

            inputField.textViewport = textAreaRect;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;

            // --- Nút Jump ---
            var jumpButton = CreateCheatButton(row.transform, rounded, "JumpWaveButton", "Jump to wave");
            var jumpLayoutElem = jumpButton.GetComponent<LayoutElement>();
            jumpLayoutElem.flexibleWidth = 1f;

            return (inputField, jumpButton);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string content, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>
        /// Generate sprite bo goc trang 64x64 (radius 16px, border 9-slice 20px) neu chua co.
        /// Trang de nhuom mau bang Image.color / Button ColorTint.
        /// </summary>
        private static Sprite GetOrCreateRoundedSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 64;
            const float radius = 16f;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundedRectAlpha(x + 0.5f, y + 0.5f, size, size, radius);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(RoundedSpritePath));
            File.WriteAllBytes(RoundedSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(RoundedSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RoundedSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = new Vector4(20f, 20f, 20f, 20f); // 9-slice
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        }

        /// <summary>Alpha cua pixel tai (px,py) cho rounded rect w*h voi ban kinh goc r (co anti-alias 1px).</summary>
        private static float RoundedRectAlpha(float px, float py, float w, float h, float r)
        {
            float dx = Mathf.Max(Mathf.Max(r - px, px - (w - r)), 0f);
            float dy = Mathf.Max(Mathf.Max(r - py, py - (h - r)), 0f);
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(r - dist + 0.5f);
        }
    }
}
