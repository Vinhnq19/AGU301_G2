using DungeonBuilder.Chat;
using DungeonBuilder.UI.Chat;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.Editor
{
    public static class ChatPrefabSetup
    {
        private const string PrefabOutputPath = "Assets/_Game/Generated/Prefabs/UI/";

        [MenuItem("Tools/Chat/Create Chat Prefabs")]
        public static void CreateChatPrefabs()
        {
            // Step 1: create and save item + manager prefabs first so they are importable
            CreateChatMessageItemPrefab();
            CreateChatManagerObjectPrefab();

            // Flush so the asset database can find them by path
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Step 2: create panel and wire the item prefab reference
            var itemPrefab = AssetDatabase.LoadAssetAtPath<ChatMessageItem>(PrefabOutputPath + "ChatMessageItem.prefab");
            CreateChatPanelPrefab(itemPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ChatPrefabSetup] 3 prefabs created in " + PrefabOutputPath);
        }

        private static void CreateChatMessageItemPrefab()
        {
            var root = new GameObject("ChatMessageItem");
            var rectTransform = root.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, 30f);

            var text = root.AddComponent<TextMeshProUGUI>();
            text.fontSize = 14f;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.raycastTarget = false;

            var item = root.AddComponent<ChatMessageItem>();
            var so = new SerializedObject(item);
            so.FindProperty("_text").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabOutputPath + "ChatMessageItem.prefab");
        }

        private static void CreateChatManagerObjectPrefab()
        {
            var root = new GameObject("ChatManager");
            root.AddComponent<NetworkObject>();
            root.AddComponent<ChatManager>();

            SavePrefab(root, PrefabOutputPath + "ChatManagerObject.prefab");
        }

        private static void CreateChatPanelPrefab(ChatMessageItem itemPrefab)
        {
            // Root panel
            var root = new GameObject("ChatPanel");
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(350f, 250f);
            var rootCG = root.AddComponent<CanvasGroup>();
            var chatView = root.AddComponent<ChatView>();

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.4f);
            bgImage.raycastTarget = false;

            // --- Message area ---
            var msgArea = new GameObject("MessageArea");
            msgArea.transform.SetParent(root.transform, false);
            var msgAreaRect = msgArea.AddComponent<RectTransform>();
            msgAreaRect.anchorMin = new Vector2(0f, 0.2f);
            msgAreaRect.anchorMax = new Vector2(1f, 1f);
            msgAreaRect.offsetMin = new Vector2(4f, 4f);
            msgAreaRect.offsetMax = new Vector2(-4f, -4f);

            var scrollRect = msgArea.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 20f;

            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(msgArea.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 0f);
            contentRect.pivot = new Vector2(0.5f, 0f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2f;
            layout.padding = new RectOffset(4, 4, 2, 2);

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            // --- Input area ---
            var inputArea = new GameObject("InputArea");
            inputArea.transform.SetParent(root.transform, false);
            var inputAreaRect = inputArea.AddComponent<RectTransform>();
            inputAreaRect.anchorMin = new Vector2(0f, 0f);
            inputAreaRect.anchorMax = new Vector2(1f, 0.2f);
            inputAreaRect.offsetMin = new Vector2(4f, 4f);
            inputAreaRect.offsetMax = new Vector2(-4f, -4f);

            // TMP_InputField
            var inputFieldGO = new GameObject("InputField");
            inputFieldGO.transform.SetParent(inputArea.transform, false);
            var inputFieldRect = inputFieldGO.AddComponent<RectTransform>();
            inputFieldRect.anchorMin = Vector2.zero;
            inputFieldRect.anchorMax = Vector2.one;
            inputFieldRect.sizeDelta = Vector2.zero;
            var inputBg = inputFieldGO.AddComponent<Image>();
            inputBg.color = new Color(0f, 0f, 0f, 0.6f);
            var tmpInput = inputFieldGO.AddComponent<TMP_InputField>();
            tmpInput.characterLimit = 128;

            // Text Area
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputFieldGO.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(6f, 2f);
            textAreaRect.offsetMax = new Vector2(-6f, -2f);
            textArea.AddComponent<RectMask2D>();

            // Input text
            var inputText = new GameObject("Text");
            inputText.transform.SetParent(textArea.transform, false);
            var inputTextRect = inputText.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.sizeDelta = Vector2.zero;
            var inputTMP = inputText.AddComponent<TextMeshProUGUI>();
            inputTMP.fontSize = 14f;
            inputTMP.color = Color.white;
            inputTMP.enableWordWrapping = false;

            // Placeholder
            var placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(textArea.transform, false);
            var placeholderRect = placeholder.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;
            var placeholderTMP = placeholder.AddComponent<TextMeshProUGUI>();
            placeholderTMP.fontSize = 14f;
            placeholderTMP.color = new Color(1f, 1f, 1f, 0.4f);
            placeholderTMP.text = "Nhan Enter de chat...";
            placeholderTMP.fontStyle = FontStyles.Italic;

            tmpInput.textComponent = inputTMP;
            tmpInput.placeholder = placeholderTMP;
            tmpInput.textViewport = textAreaRect;

            // --- Close (X) button: goc tren phai cua panel ---
            var closeButton = CreateCloseButton(root.transform);

            // Wire ChatView SerializedFields
            var chatViewSO = new SerializedObject(chatView);
            chatViewSO.FindProperty("_scrollRect").objectReferenceValue = scrollRect;
            chatViewSO.FindProperty("_messageContainer").objectReferenceValue = contentRect;
            chatViewSO.FindProperty("_inputField").objectReferenceValue = tmpInput;
            chatViewSO.FindProperty("_panelCanvasGroup").objectReferenceValue = rootCG;
            chatViewSO.FindProperty("_panelRectTransform").objectReferenceValue = rootRect;
            if (itemPrefab != null)
            {
                chatViewSO.FindProperty("_messageItemPrefab").objectReferenceValue = itemPrefab;
            }
            chatViewSO.FindProperty("_closeButton").objectReferenceValue = closeButton;
            chatViewSO.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, PrefabOutputPath + "ChatPanel.prefab");
        }

        /// <summary>Tao nut X dong chat o goc tren phai cua panel. Dung chung cho generator va tool them vao prefab co san.</summary>
        public static Button CreateCloseButton(Transform panelRoot)
        {
            var closeGO = new GameObject("CloseButton");
            closeGO.transform.SetParent(panelRoot, false);
            var closeRect = closeGO.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-2f, -2f);
            closeRect.sizeDelta = new Vector2(22f, 22f);

            var closeImage = closeGO.AddComponent<Image>();
            closeImage.color = new Color(0f, 0f, 0f, 0.6f);

            var button = closeGO.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.8f, 0.2f, 0.2f, 1f);
            colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
            button.colors = colors;

            var labelGO = new GameObject("Text");
            labelGO.transform.SetParent(closeGO.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text = "X";
            labelTMP.fontSize = 14f;
            labelTMP.fontStyle = FontStyles.Bold;
            labelTMP.color = Color.white;
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.raycastTarget = false;

            return button;
        }

        private static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
