#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using DungeonBuilder.Networking.Lobby;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DungeonBuilder.Editor
{
    /// <summary>
    /// Tool tu dong dung toan bo LobbyScene (hang cho multiplayer LAN):
    /// - NetworkManager + UnityTransport (copy PlayerPrefab + NetworkPrefabsList tu SampleScene).
    /// - LobbyController (NetworkObject), LobbyConnectionService, LobbyLifetimeScope.
    /// - UI day du: ten, IP, cac nut Host/Join/Start/Disconnect, Room ID, Status, danh sach slot.
    /// - LobbySlotItem prefab.
    /// - Wire tat ca references.
    /// - Them ca 2 scene vao Build Settings (Lobby index 0).
    ///
    /// Idempotent: chay lai khong tao trung (LoadOrCreate theo ten GameObject).
    /// </summary>
    public static class LobbySceneSetupTool
    {
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SlotItemPrefabPath = "Assets/_Game/Generated/Prefabs/UI/LobbySlotItem.prefab";

        // Lay tu NetworkManager trong SampleScene.
        private const string PlayerPrefabGuid = "61480295d2e27fd4ca601034180fef36";
        private const string NetworkPrefabsListGuid = "525c28dd23f558c42934fe0fe9defe4d";

        private const string GameSceneName = "SampleScene";

        [MenuItem("Dungeon Builder/Setup Lobby Scene")]
        public static void Setup()
        {
            Scene scene = OpenLobbyScene();

            LobbySlotItem slotItemPrefab = EnsureSlotItemPrefab();

            EnsureNetworkManager();
            LobbyController controller = EnsureLobbyController();
            LobbyConnectionService connection = EnsureConnectionService(controller);
            LobbyView view = EnsureCanvasUI(slotItemPrefab);
            EnsureLifetimeScope(view, controller, connection);

            EnsureBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Don NetworkManager trong SampleScene (vi NM da persist tu LobbyScene).
            RemoveNetworkManagerFromGameScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LobbySceneSetupTool] Setup hoan tat. Mo LobbyScene va Play de test (2 instance/2 may cung wifi).");
        }

        // ------------------------------------------------------------------
        // Scene
        // ------------------------------------------------------------------

        private static Scene OpenLobbyScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == LobbyScenePath && active.isLoaded)
            {
                return active;
            }

            return EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        }

        // ------------------------------------------------------------------
        // NetworkManager + UnityTransport
        // ------------------------------------------------------------------

        private static NetworkManager EnsureNetworkManager()
        {
            NetworkManager nm = Object.FindFirstObjectByType<NetworkManager>();
            if (nm == null)
            {
                var go = new GameObject("NetworkManager");
                nm = go.AddComponent<NetworkManager>();
            }

            UnityTransport transport = nm.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = nm.gameObject.AddComponent<UnityTransport>();
            }

            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");

            // Cau hinh NetworkConfig: transport, player prefab, prefab list, scene management.
            var so = new SerializedObject(nm);
            SerializedProperty config = so.FindProperty("NetworkConfig");

            SetRef(config, "NetworkTransport", transport);
            SetRef(config, "PlayerPrefab", LoadPlayerPrefab());

            SerializedProperty enableScene = config.FindPropertyRelative("EnableSceneManagement");
            if (enableScene != null) enableScene.boolValue = true;

            SerializedProperty autoSpawn = config.FindPropertyRelative("AutoSpawnPlayerPrefabClientSide");
            if (autoSpawn != null) autoSpawn.boolValue = true;

            // Connection approval: code tu bat luc runtime, nhung set san cung khong sao (de false de tranh chan host khong co callback luc start tu Play SampleScene truc tiep).
            SerializedProperty approval = config.FindPropertyRelative("ConnectionApproval");
            if (approval != null) approval.boolValue = false;

            // NetworkPrefabsLists: chi giu DUNG mot list game (DB_NetworkPrefabs).
            // Unity tu them DefaultNetworkPrefabs khi tao NetworkManager -> trung prefab voi list game
            // -> loi "duplicate GlobalObjectIdHash". Vi vay clear het roi gan dung 1 list.
            SerializedProperty prefabs = config.FindPropertyRelative("Prefabs");
            if (prefabs != null)
            {
                SerializedProperty lists = prefabs.FindPropertyRelative("NetworkPrefabsLists");
                if (lists != null)
                {
                    Object listAsset = LoadNetworkPrefabsList();
                    lists.ClearArray();
                    if (listAsset != null)
                    {
                        lists.arraySize = 1;
                        lists.GetArrayElementAtIndex(0).objectReferenceValue = listAsset;
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(nm);
            return nm;
        }

        private static GameObject LoadPlayerPrefab()
        {
            string path = AssetDatabase.GUIDToAssetPath(PlayerPrefabGuid);
            GameObject prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[LobbySceneSetupTool] Khong load duoc PlayerPrefab (guid {PlayerPrefabGuid}). Hay gan tay trong NetworkManager.");
            }

            return prefab;
        }

        private static Object LoadNetworkPrefabsList()
        {
            string path = AssetDatabase.GUIDToAssetPath(NetworkPrefabsListGuid);
            Object list = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Object>(path);
            if (list == null)
            {
                Debug.LogWarning($"[LobbySceneSetupTool] Khong load duoc NetworkPrefabsList (guid {NetworkPrefabsListGuid}). Hay gan tay trong NetworkManager > Prefabs.");
            }

            return list;
        }

        // ------------------------------------------------------------------
        // LobbyController / ConnectionService / LifetimeScope
        // ------------------------------------------------------------------

        private static LobbyController EnsureLobbyController()
        {
            LobbyController controller = Object.FindFirstObjectByType<LobbyController>();
            if (controller == null)
            {
                var go = new GameObject("LobbyController");
                go.AddComponent<NetworkObject>();
                controller = go.AddComponent<LobbyController>();
            }
            else if (controller.GetComponent<NetworkObject>() == null)
            {
                controller.gameObject.AddComponent<NetworkObject>();
            }

            SetSerializedString(controller, "_gameSceneName", GameSceneName);
            return controller;
        }

        private static LobbyConnectionService EnsureConnectionService(LobbyController controller)
        {
            LobbyConnectionService connection = Object.FindFirstObjectByType<LobbyConnectionService>();
            if (connection == null)
            {
                var go = new GameObject("LobbyConnectionService");
                connection = go.AddComponent<LobbyConnectionService>();
            }

            SetSerializedObject(connection, "_lobbyController", controller);
            return connection;
        }

        private static void EnsureLifetimeScope(LobbyView view, LobbyController controller, LobbyConnectionService connection)
        {
            LobbyLifetimeScope scope = Object.FindFirstObjectByType<LobbyLifetimeScope>();
            if (scope == null)
            {
                var go = new GameObject("LobbyLifetimeScope");
                scope = go.AddComponent<LobbyLifetimeScope>();
            }

            SetSerializedObject(scope, "_lobbyView", view);
            SetSerializedObject(scope, "_lobbyController", controller);
            SetSerializedObject(scope, "_connectionService", connection);
        }

        // ------------------------------------------------------------------
        // UI
        // ------------------------------------------------------------------

        private static LobbyView EnsureCanvasUI(LobbySlotItem slotItemPrefab)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas",
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            // Fix scale Canvas (scene tao tay co the bi scale 0 -> UI vo hinh).
            var canvasTransform = canvas.GetComponent<RectTransform>();
            if (canvasTransform.localScale == Vector3.zero)
            {
                canvasTransform.localScale = Vector3.one;
            }

            // EventSystem (chi tao neu chua co; scene cua ban da co san dung Input System UI module).
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            }

            // LobbyView tren Canvas.
            LobbyView view = canvas.GetComponent<LobbyView>();
            if (view == null)
            {
                view = canvas.gameObject.AddComponent<LobbyView>();
            }

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();

            // Cot trai: inputs + buttons. Cot phai: slot list.
            TMP_InputField nameInput = CreateInputField(canvasRect, "PlayerNameInput", "Nhap ten...", new Vector2(-260, 160));
            TMP_InputField ipInput = CreateInputField(canvasRect, "JoinIpInput", "Nhap IP phong (ID) de Join...", new Vector2(-260, 90));

            Button hostBtn = CreateButton(canvasRect, "HostButton", "HOST", new Vector2(-340, 10), new Color(0.2f, 0.5f, 0.9f));
            Button joinBtn = CreateButton(canvasRect, "JoinButton", "JOIN", new Vector2(-180, 10), new Color(0.2f, 0.7f, 0.4f));
            Button startBtn = CreateButton(canvasRect, "StartButton", "START GAME", new Vector2(-260, -60), new Color(0.9f, 0.6f, 0.1f));
            Button disconnectBtn = CreateButton(canvasRect, "DisconnectButton", "DISCONNECT", new Vector2(-260, -130), new Color(0.7f, 0.25f, 0.25f));

            TMP_Text roomIdText = CreateText(canvasRect, "RoomIdText", "Room ID: -", new Vector2(-260, -190), 22, TextAlignmentOptions.Center);
            TMP_Text statusText = CreateText(canvasRect, "StatusText", "", new Vector2(-260, -230), 18, TextAlignmentOptions.Center);

            // Slot list (cot phai).
            RectTransform slotContainer = EnsureSlotContainer(canvasRect);
            CreateText(canvasRect, "SlotTitle", "Nguoi choi trong phong", new Vector2(260, 200), 20, TextAlignmentOptions.Center);

            // Wire LobbyView.
            var so = new SerializedObject(view);
            SetProp(so, "_playerNameInput", nameInput);
            SetProp(so, "_joinIpInput", ipInput);
            SetProp(so, "_hostButton", hostBtn);
            SetProp(so, "_joinButton", joinBtn);
            SetProp(so, "_startButton", startBtn);
            SetProp(so, "_disconnectButton", disconnectBtn);
            SetProp(so, "_roomIdText", roomIdText);
            SetProp(so, "_statusText", statusText);
            SetProp(so, "_slotContainer", slotContainer);
            SetProp(so, "_slotItemPrefab", slotItemPrefab);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            return view;
        }

        private static RectTransform EnsureSlotContainer(RectTransform canvasRect)
        {
            Transform existing = canvasRect.Find("SlotContainer");
            if (existing != null)
            {
                return existing as RectTransform;
            }

            var go = new GameObject("SlotContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(canvasRect, false);
            rect.anchoredPosition = new Vector2(260, 30);
            rect.sizeDelta = new Vector2(300, 320);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.padding = new RectOffset(8, 8, 8, 8);

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rect;
        }

        // ------------------------------------------------------------------
        // LobbySlotItem prefab
        // ------------------------------------------------------------------

        private static LobbySlotItem EnsureSlotItemPrefab()
        {
            EnsureFolder("Assets/_Game/Generated/Prefabs/UI");

            LobbySlotItem existing = AssetDatabase.LoadAssetAtPath<GameObject>(SlotItemPrefabPath)?.GetComponent<LobbySlotItem>();
            if (existing != null)
            {
                return existing;
            }

            // Dung tam trong scene roi save thanh prefab.
            var root = new GameObject("LobbySlotItem", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Image));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(280, 36);

            var bg = root.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.25f);

            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childControlWidth = true;
            hlg.childForceExpandWidth = false;

            TMP_Text indexText = CreateChildText(root.transform, "IndexText", "#1", 18, 40);
            TMP_Text nameText = CreateChildText(root.transform, "NameText", "Player", 18, 200);

            var item = root.AddComponent<LobbySlotItem>();
            var so = new SerializedObject(item);
            SetProp(so, "_indexText", indexText);
            SetProp(so, "_nameText", nameText);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject savedGo = PrefabUtility.SaveAsPrefabAsset(root, SlotItemPrefabPath);
            Object.DestroyImmediate(root);

            return savedGo.GetComponent<LobbySlotItem>();
        }

        // ------------------------------------------------------------------
        // UI builders
        // ------------------------------------------------------------------

        private static TMP_InputField CreateInputField(RectTransform parent, string name, string placeholder, Vector2 pos)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<TMP_InputField>();
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(320, 44);

            go.GetComponent<Image>().color = Color.white;

            var input = go.GetComponent<TMP_InputField>();

            // Text area + placeholder + text.
            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            var taRect = textArea.GetComponent<RectTransform>();
            taRect.SetParent(rect, false);
            StretchFull(taRect, 10, 6);

            TMP_Text placeholderText = CreateChildTMP(textArea.transform, "Placeholder", placeholder, 18, new Color(0.4f, 0.4f, 0.4f));
            TMP_Text inputText = CreateChildTMP(textArea.transform, "Text", "", 18, Color.black);

            StretchFull(placeholderText.rectTransform, 0, 0);
            StretchFull(inputText.rectTransform, 0, 0);

            input.textViewport = taRect;
            input.textComponent = inputText;
            input.placeholder = placeholderText;
            input.targetGraphic = go.GetComponent<Image>();

            return input;
        }

        private static Button CreateButton(RectTransform parent, string name, string label, Vector2 pos, Color color)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<Button>();
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(150, 50);

            go.GetComponent<Image>().color = color;

            TMP_Text text = CreateChildTMP(go.transform, "Label", label, 20, Color.white);
            text.alignment = TextAlignmentOptions.Center;
            StretchFull(text.rectTransform, 0, 0);

            return go.GetComponent<Button>();
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string content, Vector2 pos, float size, TextAlignmentOptions align)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing.GetComponent<TMP_Text>();
            }

            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(340, 40);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.alignment = align;
            text.color = Color.white;
            return text;
        }

        private static TMP_Text CreateChildText(Transform parent, string name, string content, float size, float width)
        {
            TMP_Text text = CreateChildTMP(parent, name, content, size, Color.white);
            var le = text.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            return text;
        }

        private static TMP_Text CreateChildTMP(Transform parent, string name, string content, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            return text;
        }

        private static void StretchFull(RectTransform rect, float padX, float padY)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
        }

        // ------------------------------------------------------------------
        // Build Settings
        // ------------------------------------------------------------------

        private static void EnsureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            scenes.RemoveAll(s => s.path == LobbyScenePath);

            bool hasGame = scenes.Exists(s => s.path == GameScenePath);

            // Lobby luon o index 0.
            scenes.Insert(0, new EditorBuildSettingsScene(LobbyScenePath, true));

            if (!hasGame)
            {
                scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Load SampleScene additively, xoa GameObject NetworkManager (neu con), luu lai, roi unload.
        /// NM da song trong LobbyScene + DontDestroyOnLoad nen SampleScene khong duoc co NM rieng.
        /// </summary>
        private static void RemoveNetworkManagerFromGameScene()
        {
            Scene gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            bool removed = false;

            foreach (GameObject root in gameScene.GetRootGameObjects())
            {
                NetworkManager nm = root.GetComponent<NetworkManager>();
                if (nm != null)
                {
                    Object.DestroyImmediate(root);
                    removed = true;
                }
            }

            if (removed)
            {
                EditorSceneManager.MarkSceneDirty(gameScene);
                EditorSceneManager.SaveScene(gameScene);
                Debug.Log("[LobbySceneSetupTool] Da xoa NetworkManager khoi SampleScene.");
            }

            EditorSceneManager.CloseScene(gameScene, true);
        }

        private static void SetRef(SerializedProperty parent, string relative, Object value)
        {
            SerializedProperty p = parent.FindPropertyRelative(relative);
            if (p != null)
            {
                p.objectReferenceValue = value;
            }
            else
            {
                Debug.LogWarning($"[LobbySceneSetupTool] Khong tim thay property {relative}.");
            }
        }

        private static void SetProp(SerializedObject so, string name, Object value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
            {
                p.objectReferenceValue = value;
            }
            else
            {
                Debug.LogWarning($"[LobbySceneSetupTool] Khong tim thay property {name} tren {so.targetObject.name}.");
            }
        }

        private static void SetSerializedObject(Object target, string propertyName, Object value)
        {
            var so = new SerializedObject(target);
            SetProp(so, propertyName, value);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetSerializedString(Object target, string propertyName, string value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(propertyName);
            if (p != null)
            {
                p.stringValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
#endif
