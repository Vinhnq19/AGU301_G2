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

            // Dim background
            var bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);

            // Scrollable inner panel
            var panel = CreateScrollablePanel(root.transform, out var contentTr);

            // ── Title ──
            var titleText = AddLabel(contentTr, "TitleText", "Victory! Core Defended!", 26f, Color.yellow, FontStyles.Bold);

            // ── Combat stats ──
            AddSectionHeader(contentTr, "── Combat ──");
            var enemiesText       = AddLabel(contentTr, "EnemiesKilledText",  "Enemies Killed: 0",   16f, Color.white, FontStyles.Normal);
            var bossesText        = AddLabel(contentTr, "BossesKilledText",   "Bosses Killed: 0",    16f, Color.white, FontStyles.Normal);
            var wavesText         = AddLabel(contentTr, "WavesCompletedText", "Waves Completed: 0",  16f, Color.white, FontStyles.Normal);
            var towersText        = AddLabel(contentTr, "TowersBuiltText",    "Towers Built: 0",     16f, Color.white, FontStyles.Normal);

            // ── Resources ──
            AddSectionHeader(contentTr, "── Resources ──");
            var woodText        = AddLabel(contentTr, "WoodText",        "Wood: 0",         14f, Color.white, FontStyles.Normal);
            var stoneText       = AddLabel(contentTr, "StoneText",       "Stone: 0",        14f, Color.white, FontStyles.Normal);
            var oreText         = AddLabel(contentTr, "OreText",         "Ore: 0",          14f, Color.white, FontStyles.Normal);
            var ironText        = AddLabel(contentTr, "IronText",        "Iron: 0",         14f, Color.white, FontStyles.Normal);
            var copperText      = AddLabel(contentTr, "CopperText",      "Copper: 0",       14f, Color.white, FontStyles.Normal);
            var crystalText     = AddLabel(contentTr, "CrystalText",     "Crystal: 0",      14f, Color.white, FontStyles.Normal);
            var blueGemsText    = AddLabel(contentTr, "BlueGemsText",    "Blue Gems: 0",    14f, Color.white, FontStyles.Normal);
            var purpleGemsText  = AddLabel(contentTr, "PurpleGemsText",  "Purple Gems: 0",  14f, Color.white, FontStyles.Normal);
            var tokenText       = AddLabel(contentTr, "TokenText",       "Token: 0",        14f, Color.white, FontStyles.Normal);
            var coinText        = AddLabel(contentTr, "CoinText",        "Coin: 0",         14f, Color.white, FontStyles.Normal);

            // ── Skills ──
            AddSectionHeader(contentTr, "── Skills ──");
            var miningSkillText  = AddLabel(contentTr, "MiningSkillText",  "Mining Skill Lv: 1",  14f, new Color(0.6f, 0.9f, 1f), FontStyles.Normal);
            var forgingSkillText = AddLabel(contentTr, "ForgingSkillText", "Forging Skill Lv: 1", 14f, new Color(0.6f, 0.9f, 1f), FontStyles.Normal);

            // ── Return button (outside scroll, pinned to bottom of panel) ──
            var btn = AddReturnButton(panel.transform);

            // Wire all SerializedFields on GameResultView
            var so = new SerializedObject(view);
            so.FindProperty("_titleText").objectReferenceValue          = titleText;
            so.FindProperty("_returnButton").objectReferenceValue       = btn;
            so.FindProperty("_enemiesKilledText").objectReferenceValue  = enemiesText;
            so.FindProperty("_bossesKilledText").objectReferenceValue   = bossesText;
            so.FindProperty("_wavesCompletedText").objectReferenceValue = wavesText;
            so.FindProperty("_towersBuiltText").objectReferenceValue    = towersText;
            so.FindProperty("_woodText").objectReferenceValue           = woodText;
            so.FindProperty("_stoneText").objectReferenceValue          = stoneText;
            so.FindProperty("_oreText").objectReferenceValue            = oreText;
            so.FindProperty("_ironText").objectReferenceValue           = ironText;
            so.FindProperty("_copperText").objectReferenceValue         = copperText;
            so.FindProperty("_crystalText").objectReferenceValue        = crystalText;
            so.FindProperty("_blueGemsText").objectReferenceValue       = blueGemsText;
            so.FindProperty("_purpleGemsText").objectReferenceValue     = purpleGemsText;
            so.FindProperty("_tokenText").objectReferenceValue          = tokenText;
            so.FindProperty("_coinText").objectReferenceValue           = coinText;
            so.FindProperty("_miningSkillText").objectReferenceValue    = miningSkillText;
            so.FindProperty("_forgingSkillText").objectReferenceValue   = forgingSkillText;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabOutputPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GameResultPrefabSetup] Prefab saved to " + PrefabOutputPath);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static GameObject CreateScrollablePanel(Transform parent, out Transform contentTransform)
        {
            // Outer panel card
            var panel = new GameObject("Panel");
            panel.transform.SetParent(parent, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.05f);
            panelRect.anchorMax = new Vector2(0.8f, 0.95f);
            panelRect.sizeDelta = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // ScrollRect fills top portion, leaving room for button at bottom
            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(panel.transform, false);
            var scrollRect_rt = scrollGO.AddComponent<RectTransform>();
            scrollRect_rt.anchorMin = new Vector2(0f, 0.12f);
            scrollRect_rt.anchorMax = Vector2.one;
            scrollRect_rt.sizeDelta = Vector2.zero;
            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpRect = viewportGO.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            viewportGO.AddComponent<RectMask2D>();
            scroll.viewport = vpRect;

            // Content with VerticalLayoutGroup + ContentSizeFitter
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(12, 12, 12, 12);

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = contentRect;
            contentTransform = contentGO.transform;
            return panel;
        }

        private static Button AddReturnButton(Transform panel)
        {
            var btnGO = new GameObject("ReturnButton");
            btnGO.transform.SetParent(panel, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.1f, 0.01f);
            btnRect.anchorMax = new Vector2(0.9f, 0.11f);
            btnRect.sizeDelta = Vector2.zero;
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.45f, 0.8f);
            var btn = btnGO.AddComponent<Button>();

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(btnGO.transform, false);
            var lblRect = labelGO.AddComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.sizeDelta = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "Về Lobby";
            tmp.fontSize = 18f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;

            return btn;
        }

        private static TextMeshProUGUI AddLabel(Transform parent, string goName, string text, float size, Color color, FontStyles style)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();   // VerticalLayoutGroup drives size
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        private static void AddSectionHeader(Transform parent, string text)
        {
            AddLabel(parent, "Header_" + text.Replace(" ", ""), text, 12f, new Color(0.55f, 0.55f, 0.55f), FontStyles.Normal);
        }
    }
}
