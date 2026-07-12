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
    /// Cau truc: root "CheatPanel" (CheatPanelView, luon active) -> child "VisualRoot" (tat/bat).
    ///
    /// Layout: header (status pill HOST/CLIENT + title + close) -> amount chips ->
    /// cac section (TAI NGUYEN / KY NANG / NGUOI CHOI / WAVE) -> footer feedback label.
    /// Run via menu: Tools > Cheat > Create Cheat Panel Prefab
    /// </summary>
    public static class CheatPanelSetup
    {
        private const string PrefabPath = "Assets/_Game/Generated/Prefabs/UI/CheatPanel.prefab";
        private const string RoundedSpritePath = "Assets/_Game/Generated/UI/RoundedRect16.png";

        // Palette (dark, do tuong phan cao — nen sang hon va chu to hon de doc ro
        // o game view nho; truoc day panel bi che "mo va be").
        private static readonly Color PanelBg = new Color32(30, 34, 49, 255);      // #1E2231
        private static readonly Color PanelOutline = new Color32(90, 99, 140, 255); // #5A638C
        private static readonly Color Accent = new Color32(124, 108, 255, 255);    // #7C6CFF
        private static readonly Color AccentPressed = new Color32(88, 74, 200, 255);
        private static readonly Color ButtonBg = new Color32(48, 57, 80, 255);     // #303950
        private static readonly Color TextMain = new Color32(240, 242, 252, 255);  // #F0F2FC
        private static readonly Color TextMuted = new Color32(169, 176, 204, 255); // #A9B0CC
        private static readonly Color TextDark = new Color32(23, 26, 36, 255);     // chu tren pill
        private static readonly Color DangerHover = new Color32(255, 92, 92, 255); // #FF5C5C
        private static readonly Color HostGreen = new Color32(74, 222, 128, 255);

        private const float ButtonHeight = 58f;
        private const float SectionHeight = 30f;
        private const float ChipHeight = 44f;
        private const float ButtonFontSize = 21f;
        private const float SectionFontSize = 16f;

        [MenuItem("Tools/Cheat/Create Cheat Panel Prefab")]
        public static void CreateCheatPanelPrefab()
        {
            Sprite rounded = GetOrCreateRoundedSprite();

            // Root: khong co visual, giu CheatPanelView luon active de nhan lenh Show() tu ChatView.
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
            panelRect.sizeDelta = new Vector2(560f, 930f);
            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = rounded;
            panelImage.type = Image.Type.Sliced;
            panelImage.pixelsPerUnitMultiplier = 1.15f;
            panelImage.color = PanelBg;

            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(PanelOutline.r, PanelOutline.g, PanelOutline.b, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

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

            // Status pill HOST/CLIENT o goc trai tren (runtime doi mau + text).
            var (statusPill, statusText) = CreateStatusPill(panel.transform, rounded);

            // Title + subtitle.
            var title = CreateLabel(panel.transform, "Title", "CHEAT MENU", 34f, FontStyles.Bold, TextMain);
            title.characterSpacing = 6f;
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -22f);
            titleRect.sizeDelta = new Vector2(0f, 44f);

            var subtitle = CreateLabel(panel.transform, "Subtitle", "developer tools — ESC de dong", 16f, FontStyles.Italic, TextMuted);
            var subtitleRect = subtitle.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0f, 1f);
            subtitleRect.anchorMax = new Vector2(1f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.anchoredPosition = new Vector2(0f, -66f);
            subtitleRect.sizeDelta = new Vector2(0f, 22f);

            // Close (x) tron o goc.
            var closeButton = CreateCloseButton(panel.transform, rounded);

            // Content list.
            var list = new GameObject("Content");
            list.transform.SetParent(panel.transform, false);
            var listRect = list.AddComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.offsetMin = new Vector2(26f, 56f);    // chua cho footer feedback
            listRect.offsetMax = new Vector2(-26f, -102f); // chua cho header
            var layout = list.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;

            // --- TAI NGUYEN ---
            CreateSectionLabel(list.transform, "TAI NGUYEN");
            var amountChips = CreateAmountChipsRow(list.transform, rounded);
            var addBasic = CreateCheatButton(list.transform, rounded, "AddBasicButton", "+ Co ban   (Wood / Stone / Ore / Crystal)");
            var addRare = CreateCheatButton(list.transform, rounded, "AddRareButton", "+ Hiem   (Copper / Iron / Gems)");
            var addCurrency = CreateCheatButton(list.transform, rounded, "AddCurrencyButton", "+ Tien te   (Coin / Token)");

            // --- KY NANG ---
            CreateSectionLabel(list.transform, "KY NANG");
            var (miningSkill, forgingSkill) = CreatePairRow(list.transform, rounded,
                ("MiningSkillButton", "+1 Mining Skill", false),
                ("ForgingSkillButton", "+1 Forging Skill", false));

            // --- NGUOI CHOI ---
            CreateSectionLabel(list.transform, "NGUOI CHOI");
            var (fullHeal, revive) = CreatePairRow(list.transform, rounded,
                ("FullHealButton", "Hoi day mau", false),
                ("ReviveButton", "Hoi sinh ngay", false));
            var kill = CreateCheatButton(list.transform, rounded, "KillButton", "Tu sat (test respawn)", danger: true);

            // --- WAVE ---
            var waveInfo = CreateSectionLabelWithInfo(list.transform, "WAVE", "Wave hien tai: --");
            var reloadWaves = CreateCheatButton(list.transform, rounded, "ReloadWavesButton", "Reload Waves (JSON)");
            var (jumpInput, jumpButton) = CreateJumpWaveRow(list.transform, rounded);

            // Footer: feedback label (runtime set text + mau).
            var feedback = CreateLabel(panel.transform, "FeedbackText", "", 18f, FontStyles.Normal, TextMuted);
            var feedbackRect = feedback.GetComponent<RectTransform>();
            feedbackRect.anchorMin = new Vector2(0f, 0f);
            feedbackRect.anchorMax = new Vector2(1f, 0f);
            feedbackRect.pivot = new Vector2(0.5f, 0f);
            feedbackRect.anchoredPosition = new Vector2(0f, 16f);
            feedbackRect.sizeDelta = new Vector2(-40f, 30f);

            // Wire serialized fields
            var so = new SerializedObject(view);
            so.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeButton;
            so.FindProperty("_addBasicResourcesButton").objectReferenceValue = addBasic;
            so.FindProperty("_addRareResourcesButton").objectReferenceValue = addRare;
            so.FindProperty("_addCurrencyButton").objectReferenceValue = addCurrency;
            so.FindProperty("_miningSkillButton").objectReferenceValue = miningSkill;
            so.FindProperty("_forgingSkillButton").objectReferenceValue = forgingSkill;
            so.FindProperty("_fullHealButton").objectReferenceValue = fullHeal;
            so.FindProperty("_reviveButton").objectReferenceValue = revive;
            so.FindProperty("_killPlayerButton").objectReferenceValue = kill;
            so.FindProperty("_reloadWavesButton").objectReferenceValue = reloadWaves;
            so.FindProperty("_jumpWaveInput").objectReferenceValue = jumpInput;
            so.FindProperty("_jumpWaveButton").objectReferenceValue = jumpButton;
            so.FindProperty("_statusPill").objectReferenceValue = statusPill;
            so.FindProperty("_statusText").objectReferenceValue = statusText;
            so.FindProperty("_waveInfoText").objectReferenceValue = waveInfo;
            so.FindProperty("_feedbackText").objectReferenceValue = feedback;

            var chipsProp = so.FindProperty("_amountButtons");
            chipsProp.arraySize = amountChips.Length;
            for (int i = 0; i < amountChips.Length; i++)
            {
                chipsProp.GetArrayElementAtIndex(i).objectReferenceValue = amountChips[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Debug.Log("[CheatPanelSetup] CheatPanel prefab created at " + PrefabPath);
        }

        private static (Image pill, TextMeshProUGUI text) CreateStatusPill(Transform parent, Sprite rounded)
        {
            var go = new GameObject("StatusPill");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(98f, 34f);

            var image = go.AddComponent<Image>();
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2.2f;
            image.color = HostGreen; // runtime doi theo host/client
            image.raycastTarget = false;

            var label = CreateLabel(go.transform, "Text", "HOST", 17f, FontStyles.Bold, TextDark);
            label.characterSpacing = 2f;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            return (image, label);
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
            rect.sizeDelta = new Vector2(44f, 44f);

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

            var label = CreateLabel(go.transform, "Text", "×", 30f, FontStyles.Bold, TextMuted);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;

            return button;
        }

        private static void CreateSectionLabel(Transform parent, string text)
        {
            var label = CreateLabel(parent, "Section_" + text, text, SectionFontSize, FontStyles.Bold, TextMuted);
            label.characterSpacing = 10f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var rect = label.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, SectionHeight);
            var layoutElem = label.gameObject.AddComponent<LayoutElement>();
            layoutElem.minHeight = SectionHeight;
        }

        /// <summary>Section header 2 cot: ten section ben trai + info label ben phai (vd wave hien tai).</summary>
        private static TextMeshProUGUI CreateSectionLabelWithInfo(Transform parent, string text, string info)
        {
            var row = new GameObject("Section_" + text);
            row.transform.SetParent(parent, false);
            var rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, SectionHeight);
            var layoutElem = row.AddComponent<LayoutElement>();
            layoutElem.minHeight = SectionHeight;

            var left = CreateLabel(row.transform, "Name", text, SectionFontSize, FontStyles.Bold, TextMuted);
            left.characterSpacing = 10f;
            left.alignment = TextAlignmentOptions.MidlineLeft;
            var leftRect = left.GetComponent<RectTransform>();
            leftRect.anchorMin = Vector2.zero;
            leftRect.anchorMax = new Vector2(0.4f, 1f);
            leftRect.sizeDelta = Vector2.zero;

            var right = CreateLabel(row.transform, "Info", info, 18f, FontStyles.Normal, TextMain);
            right.alignment = TextAlignmentOptions.MidlineRight;
            var rightRect = right.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.4f, 0f);
            rightRect.anchorMax = Vector2.one;
            rightRect.sizeDelta = Vector2.zero;

            return right;
        }

        /// <summary>Hang chip chon so luong resource: [100][500][1K][5K]. Chip dang chon accent (runtime).</summary>
        private static Button[] CreateAmountChipsRow(Transform parent, Sprite rounded)
        {
            var row = new GameObject("AmountChips");
            row.transform.SetParent(parent, false);
            var rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, ChipHeight);
            var layoutElem = row.AddComponent<LayoutElement>();
            layoutElem.minHeight = ChipHeight;

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 8f;

            string[] labels = { "100", "500", "1K", "5K" };
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var go = new GameObject("Chip_" + labels[i]);
                go.transform.SetParent(row.transform, false);
                go.AddComponent<RectTransform>();

                var image = go.AddComponent<Image>();
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2.2f;
                image.color = ButtonBg; // runtime: chip dang chon doi sang accent

                var button = go.AddComponent<Button>();
                // Chip doi mau NEN (Image.color) bang code -> khong dung ColorTint de khoi de mau runtime.
                button.transition = Selectable.Transition.None;

                var label = CreateLabel(go.transform, "Text", labels[i], 19f, FontStyles.Bold, TextMain);
                var labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;

                buttons[i] = button;
            }

            return buttons;
        }

        /// <summary>Hang 2 nut chia doi chieu ngang (vd Hoi mau | Tu sat).</summary>
        private static (Button left, Button right) CreatePairRow(Transform parent, Sprite rounded,
            (string name, string label, bool danger) a, (string name, string label, bool danger) b)
        {
            var row = new GameObject("Row_" + a.name);
            row.transform.SetParent(parent, false);
            var rect = row.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, ButtonHeight);
            var layoutElem = row.AddComponent<LayoutElement>();
            layoutElem.minHeight = ButtonHeight;

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = 9f;

            var left = CreateCheatButton(row.transform, rounded, a.name, a.label, a.danger, inRow: true);
            var right = CreateCheatButton(row.transform, rounded, b.name, b.label, b.danger, inRow: true);
            return (left, right);
        }

        private static Button CreateCheatButton(Transform parent, Sprite rounded, string name, string label,
            bool danger = false, bool inRow = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, ButtonHeight);
            if (!inRow)
            {
                var layoutElem = go.AddComponent<LayoutElement>();
                layoutElem.minHeight = ButtonHeight;
            }

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
            colors.disabledColor = new Color(ButtonBg.r, ButtonBg.g, ButtonBg.b, 0.4f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var text = CreateLabel(go.transform, "Text", label, ButtonFontSize, FontStyles.Normal, TextMain);
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return button;
        }

        /// <summary>Hang "Jump to wave": input so ben trai + nut "Jump" ben phai, cung 1 dong.</summary>
        private static (TMP_InputField input, Button button) CreateJumpWaveRow(Transform parent, Sprite rounded)
        {
            var row = new GameObject("JumpWaveRow");
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, ButtonHeight);
            var rowLayoutElem = row.AddComponent<LayoutElement>();
            rowLayoutElem.minHeight = ButtonHeight;

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.spacing = 9f;

            // --- Input field (TMP) ---
            var inputGo = new GameObject("JumpWaveInput");
            inputGo.transform.SetParent(row.transform, false);
            inputGo.AddComponent<RectTransform>();
            var inputLayoutElem = inputGo.AddComponent<LayoutElement>();
            inputLayoutElem.preferredWidth = 150f;
            inputLayoutElem.minHeight = ButtonHeight;

            var inputImage = inputGo.AddComponent<Image>();
            inputImage.sprite = rounded;
            inputImage.type = Image.Type.Sliced;
            inputImage.pixelsPerUnitMultiplier = 1.6f;
            inputImage.color = ButtonBg;

            var inputField = inputGo.AddComponent<TMP_InputField>();
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Viewport + text con theo cau truc chuan cua TMP_InputField.
            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputGo.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(12f, 6f);
            textAreaRect.offsetMax = new Vector2(-12f, -6f);
            textArea.AddComponent<RectMask2D>();

            var placeholder = CreateLabel(textArea.transform, "Placeholder", "wave...", 19f, FontStyles.Italic, TextMuted);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            var placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;

            var inputText = CreateLabel(textArea.transform, "Text", "", 21f, FontStyles.Normal, TextMain);
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            var inputTextRect = inputText.GetComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = Vector2.zero;

            inputField.textViewport = textAreaRect;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;

            // --- Nut Jump ---
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
