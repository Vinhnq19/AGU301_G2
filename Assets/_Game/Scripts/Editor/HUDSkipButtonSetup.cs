using DungeonBuilder.UI.HUD;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonBuilder.EditorTools
{
    /// <summary>
    /// Them nut "Skip" vao HUD (canh CountdownText) va gan vao HUDView._skipButton.
    /// </summary>
    public static class HUDSkipButtonSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Setup HUD Skip Button")]
        public static void Setup()
        {
            Scene previousActive = SceneManager.GetActiveScene();
            bool wasAlreadyOpen = previousActive.path == ScenePath;

            Scene scene = wasAlreadyOpen
                ? previousActive
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                var view = Object.FindFirstObjectByType<HUDView>(FindObjectsInactive.Include);
                if (view == null)
                {
                    Debug.LogError("[HUDSkipButtonSetup] Khong tim thay HUDView trong scene.");
                    return;
                }

                var so = new SerializedObject(view);
                var skipBtnProp = so.FindProperty("_skipButton");
                if (skipBtnProp.objectReferenceValue != null)
                {
                    Debug.Log("[HUDSkipButtonSetup] Da co Skip button, bo qua.");
                    return;
                }

                var countdownProp = so.FindProperty("_countdownText");
                var countdownText = countdownProp.objectReferenceValue as TMP_Text;
                if (countdownText == null)
                {
                    Debug.LogError("[HUDSkipButtonSetup] Khong tim thay CountdownText de dinh vi.");
                    return;
                }

                RectTransform countdownRect = countdownText.GetComponent<RectTransform>();
                Transform parent = countdownRect.parent;

                var buttonGO = new GameObject("SkipButton", typeof(RectTransform));
                var rect = buttonGO.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = countdownRect.anchorMin;
                rect.anchorMax = countdownRect.anchorMax;
                rect.pivot = countdownRect.pivot;
                rect.sizeDelta = new Vector2(120f, 32f);
                rect.anchoredPosition = countdownRect.anchoredPosition + new Vector2(0f, -40f);

                var image = buttonGO.AddComponent<Image>();
                image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
                image.type = Image.Type.Sliced;
                image.color = new Color(0.85f, 0.55f, 0.1f, 1f);

                var button = buttonGO.AddComponent<Button>();
                button.targetGraphic = image;

                var labelGO = new GameObject("Text (TMP)", typeof(RectTransform));
                var labelRect = labelGO.GetComponent<RectTransform>();
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;
                labelRect.anchoredPosition = Vector2.zero;

                var label = labelGO.AddComponent<TextMeshProUGUI>();
                label.text = "SKIP";
                label.fontSize = 18f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                if (countdownText is TextMeshProUGUI countdownTmp)
                {
                    label.font = countdownTmp.font;
                }

                skipBtnProp.objectReferenceValue = button;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[HUDSkipButtonSetup] Da them nut Skip va gan vao HUDView.");
            }
            finally
            {
                if (!wasAlreadyOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
