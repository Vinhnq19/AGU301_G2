using DungeonBuilder.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.EditorTools
{
    /// <summary>
    /// Tool dựng sẵn 3 slider âm lượng (Master/BGM/SFX) vào SettingPanel prefab
    /// và gán chúng vào SettingsPanelController. Chạy qua menu Tools.
    /// </summary>
    public static class SettingsPanelSetup
    {
        private const string PrefabPath = "Assets/_Game/Generated/Prefabs/UI/SettingPanel.prefab";

        [MenuItem("Tools/Setup Settings Panel Sliders")]
        public static void Setup()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Container là object cha của 2 nút hiện có
                Transform resumeBtn = FindDeep(root.transform, "ResumeBtn");
                if (resumeBtn == null)
                {
                    Debug.LogError("[SettingsPanelSetup] Không tìm thấy ResumeBtn trong prefab.");
                    return;
                }
                Transform panel = resumeBtn.parent;

                // Lấy font từ text có sẵn để label đồng bộ với UI hiện tại
                TMP_FontAsset font = null;
                var existingText = root.GetComponentInChildren<TextMeshProUGUI>(true);
                if (existingText != null) font = existingText.font;

                Slider master = CreateSliderRow(panel, "MasterVolumeSlider", "Master", -70f, font);
                Slider bgm = CreateSliderRow(panel, "BGMVolumeSlider", "Music", -140f, font);
                Slider sfx = CreateSliderRow(panel, "SFXVolumeSlider", "SFX", -210f, font);

                var controller = panel.GetComponent<SettingsPanelController>();
                if (controller == null) controller = panel.gameObject.AddComponent<SettingsPanelController>();

                var so = new SerializedObject(controller);
                so.FindProperty("masterVolumeSlider").objectReferenceValue = master;
                so.FindProperty("bgmVolumeSlider").objectReferenceValue = bgm;
                so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfx;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[SettingsPanelSetup] Đã thêm 3 slider âm lượng và gán vào SettingsPanelController.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Slider CreateSliderRow(Transform panel, string name, string label, float y, TMP_FontAsset font)
        {
            // Idempotent: nếu đã có thì tái sử dụng
            Transform existing = panel.Find(name);
            if (existing != null) return existing.GetComponentInChildren<Slider>(true);

            var row = new GameObject(name, typeof(RectTransform));
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.SetParent(panel, false);
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(480f, 40f);
            rowRect.anchoredPosition = new Vector2(0f, y);

            // Label bên trái
            var labelGO = new GameObject("Label", typeof(RectTransform));
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.SetParent(rowRect, false);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(140f, 0f);
            labelRect.anchoredPosition = Vector2.zero;

            var text = labelGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            if (font != null) text.font = font;

            // Slider bên phải, dùng control mặc định của Unity
            var resources = new DefaultControls.Resources
            {
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            };
            GameObject sliderGO = DefaultControls.CreateSlider(resources);
            sliderGO.name = "Slider";
            var sliderRect = sliderGO.GetComponent<RectTransform>();
            sliderRect.SetParent(rowRect, false);
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.offsetMin = new Vector2(150f, -10f);
            sliderRect.offsetMax = new Vector2(0f, 10f);

            var slider = sliderGO.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
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
    }
}
