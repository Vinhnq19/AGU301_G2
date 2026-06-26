using DungeonBuilder.UI.GameResult;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.Editor
{
    public static class GameResultPrefabSetup
    {
        private const string PrefabOutputPath = "Assets/_Game/Generated/Prefabs/UI/GameResultPanel.prefab";

        [MenuItem("Tools/GameResult/Create GameResult Panel Prefab")]
        public static void CreateGameResultPanel()
        {
            var root = new GameObject("GameResultPanel");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            root.AddComponent<CanvasGroup>();
            var view = root.AddComponent<GameResultView>();

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.75f);
            bgImage.raycastTarget = true;

            // Inner panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.25f, 0.15f);
            panelRect.anchorMax = new Vector2(0.75f, 0.85f);
            panelRect.sizeDelta = Vector2.zero;
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            var vLayout = panel.AddComponent<VerticalLayoutGroup>();
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.spacing = 8f;
            vLayout.padding = new RectOffset(20, 20, 20, 20);

            // Title
            var titleText = CreateLabel(panel.transform, "TitleText", "Victory! Core Defended!", 28f, Color.yellow, FontStyles.Bold);

            // Stats group header
            CreateLabel(panel.transform, "StatsHeader", "── Stats ──", 14f, new Color(0.7f, 0.7f, 0.7f), FontStyles.Normal);

            // Stat rows
            var enemiesText      = CreateLabel(panel.transform, "EnemiesKilledText",  "Enemies Killed: 0",    16f, Color.white, FontStyles.Normal);
            var bossesText       = CreateLabel(panel.transform, "BossesKilledText",   "Bosses Killed: 0",     16f, Color.white, FontStyles.Normal);
            var wavesText        = CreateLabel(panel.transform, "WavesCompletedText", "Waves Completed: 0",   16f, Color.white, FontStyles.Normal);
            var towersText       = CreateLabel(panel.transform, "TowersBuiltText",    "Towers Built: 0",      16f, Color.white, FontStyles.Normal);
            var resourcesText    = CreateLabel(panel.transform, "ResourcesText",      "Resources:\n  -",      14f, new Color(0.8f, 0.95f, 0.8f), FontStyles.Normal);
            resourcesText.enableWordWrapping = true;
            var skillsText       = CreateLabel(panel.transform, "SkillsText",         "Skills:\n  -",         14f, new Color(0.8f, 0.9f, 1f), FontStyles.Normal);

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(panel.transform, false);
            var spacerRect = spacer.AddComponent<RectTransform>();
            spacerRect.sizeDelta = new Vector2(0f, 10f);

            // Return button
            var btnGO = new GameObject("ReturnButton");
            btnGO.transform.SetParent(panel.transform, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(0f, 44f);
            var btnImage = btnGO.AddComponent<Image>();
            btnImage.color = new Color(0.15f, 0.45f, 0.8f);
            var btn = btnGO.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.25f, 0.6f, 1f);
            btnColors.pressedColor = new Color(0.1f, 0.3f, 0.6f);
            btn.colors = btnColors;

            var btnLabel = new GameObject("Label");
            btnLabel.transform.SetParent(btnGO.transform, false);
            var btnLabelRect = btnLabel.AddComponent<RectTransform>();
            btnLabelRect.anchorMin = Vector2.zero;
            btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.sizeDelta = Vector2.zero;
            var btnText = btnLabel.AddComponent<TextMeshProUGUI>();
            btnText.text = "Về Lobby";
            btnText.fontSize = 18f;
            btnText.color = Color.white;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.fontStyle = FontStyles.Bold;

            // Wire all refs into GameResultView
            var so = new SerializedObject(view);
            so.FindProperty("_titleText").objectReferenceValue           = titleText;
            so.FindProperty("_returnButton").objectReferenceValue        = btn;
            so.FindProperty("_enemiesKilledText").objectReferenceValue   = enemiesText;
            so.FindProperty("_bossesKilledText").objectReferenceValue    = bossesText;
            so.FindProperty("_wavesCompletedText").objectReferenceValue  = wavesText;
            so.FindProperty("_towersBuiltText").objectReferenceValue     = towersText;
            so.FindProperty("_resourcesText").objectReferenceValue       = resourcesText;
            so.FindProperty("_skillsText").objectReferenceValue          = skillsText;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabOutputPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GameResultPrefabSetup] Prefab created at " + PrefabOutputPath);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, Color color, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, fontSize * 1.6f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
