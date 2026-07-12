#if UNITY_EDITOR
using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Wave;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cửa sổ thiết kế wave trực quan — thay cho việc sửa tay asset hoặc CSV.
/// Layout master–detail: sidebar trái = danh sách wave (badge boss/lỗi + thanh cường độ),
/// panel phải = chi tiết wave đang chọn với bảng nhóm quái thẳng cột.
/// SAVE (nút hoặc Ctrl+S) = validate → ghi DB_Wave_N.asset + DB_WaveCatalog (giữ GUID).
/// Export CSV / Import CSV là 2 nút riêng, dùng đường dẫn ở ô "CSV" trên thanh công cụ
/// (đường dẫn lưu trong EditorPrefs, mặc định Docs/WaveSheet.csv).
///
/// Menu: Tools > Waves > Wave Designer. Chi tiết pipeline: Docs/WAVE_DESIGN_GUIDE.md.
/// </summary>
public sealed class WaveDesignerWindow : EditorWindow
{
    private const string WaveDataFolder = "Assets/_Game/Generated/Data/WaveData";
    private const string CatalogPath = WaveDataFolder + "/DB_WaveCatalog.asset";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    private const float SidebarWidth = 230f;
    private const float SidebarItemHeight = 52f;
    private const float PathRowHeight = 24f;
    private const string CsvPathPrefKey = "WaveDesigner.CsvPath";

    // ---------------- Palette ----------------

    private static Color Accent => new Color32(124, 108, 255, 255);        // tím — đồng bộ với Cheat Panel
    private static Color BossColor => new Color32(255, 92, 92, 255);
    private static Color PanelBg => EditorGUIUtility.isProSkin ? new Color32(45, 45, 48, 255) : new Color32(214, 214, 214, 255);
    private static Color SidebarBg => EditorGUIUtility.isProSkin ? new Color32(37, 37, 38, 255) : new Color32(200, 200, 200, 255);
    private static Color RowAlt => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.035f) : new Color(0f, 0f, 0f, 0.045f);
    private static Color TextMuted => EditorGUIUtility.isProSkin ? new Color32(155, 160, 175, 255) : new Color32(90, 90, 95, 255);

    /// <summary>Màu nhận diện từng loại quái — dùng ở chấm màu bảng nhóm + sidebar.</summary>
    private static readonly Dictionary<EnemyType, Color> EnemyColors = new()
    {
        { EnemyType.Runner,   new Color32(108, 203, 95, 255) },   // xanh lá
        { EnemyType.Spitter,  new Color32(79, 195, 247, 255) },   // xanh dương
        { EnemyType.Bloater,  new Color32(186, 104, 200, 255) },  // tím
        { EnemyType.RatKing,  new Color32(255, 92, 92, 255) },    // đỏ (boss)
        { EnemyType.Drone,    new Color32(255, 213, 79, 255) },   // vàng
        { EnemyType.Brute,    new Color32(255, 138, 101, 255) },  // cam
        { EnemyType.MinerBug, new Color32(161, 136, 127, 255) },  // nâu
    };

    // ---------------- Draft model ----------------

    private sealed class GroupDraft
    {
        public EnemyType EnemyType = EnemyType.Runner;
        public int Count = 10;
        public float Interval = 1f;
        public int SpawnPoint;
        public int Path;

        public GroupDraft Clone() => (GroupDraft)MemberwiseClone();
    }

    private sealed class WaveDraft
    {
        public float BuildTime = 60f;
        public float CombatTime = 100f;
        public bool IsBoss;
        public List<GroupDraft> Groups = new();

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                foreach (GroupDraft g in Groups) total += g.Count;
                return total;
            }
        }

        public WaveDraft Clone()
        {
            var clone = new WaveDraft { BuildTime = BuildTime, CombatTime = CombatTime, IsBoss = IsBoss };
            foreach (GroupDraft g in Groups) clone.Groups.Add(g.Clone());
            return clone;
        }
    }

    private readonly List<WaveDraft> _waves = new();

    /// <summary>Lỗi validate: WaveIndex = -1 nghĩa là lỗi chung (không thuộc wave nào).</summary>
    private readonly List<(int WaveIndex, string Message)> _issues = new();

    private int _selected;
    private Vector2 _sidebarScroll;
    private Vector2 _detailScroll;
    private bool _dirty;

    // CSV giờ export/import thủ công (tách khỏi Save). _csvExportPending = asset đã Save nhưng sheet chưa cập nhật.
    private string _csvPath;
    private bool _csvExportPending;

    // Thông tin scene phục vụ popup + validate (null nếu scene đang mở không có WaveManager).
    private string[] _spawnPointLabels;
    private string[] _pathLabels;
    private HashSet<EnemyType> _mappedEnemyTypes;

    // Styles dựng lười (IMGUI không cho tạo GUIStyle trước OnGUI đầu tiên).
    private GUIStyle _titleStyle;
    private GUIStyle _subtitleStyle;
    private GUIStyle _sidebarTitleStyle;
    private GUIStyle _sidebarSubStyle;
    private GUIStyle _badgeStyle;
    private GUIStyle _columnHeaderStyle;
    private bool _stylesReady;

    [MenuItem("Tools/Waves/Wave Designer")]
    public static void Open()
    {
        var window = GetWindow<WaveDesignerWindow>("Wave Designer");
        window.minSize = new Vector2(760f, 420f);
        window.LoadFromAssets();
    }

    private void OnEnable()
    {
        // Set trong OnEnable để tab luôn đúng tên kể cả khi window được khôi phục sau domain reload.
        titleContent = new GUIContent("Wave Designer");
        // Chấm "chưa save" trên tab + hộp thoại xác nhận khi đóng có thay đổi (API chuẩn của EditorWindow).
        saveChangesMessage = "Wave Designer đang có thay đổi chưa Save. Save trước khi đóng?";
        _csvPath = EditorPrefs.GetString(CsvPathPrefKey, WaveSheetImporter.DefaultCsvPath);
        RefreshSceneInfo();
        if (_waves.Count == 0)
        {
            LoadFromAssets();
        }
    }

    /// <summary>Được Unity gọi khi user chọn "Save" trong hộp thoại đóng cửa sổ.</summary>
    public override void SaveChanges()
    {
        SaveAll();
        base.SaveChanges();
    }

    private void MarkDirty()
    {
        _dirty = true;
        hasUnsavedChanges = true;
    }

    private void ClearDirty()
    {
        _dirty = false;
        hasUnsavedChanges = false;
    }

    // ---------------- Load / Save ----------------

    private void LoadFromAssets()
    {
        _waves.Clear();
        _issues.Clear();
        ClearDirty();
        _csvExportPending = false; // vừa nạp từ asset — coi như đồng bộ, không nhắc export
        _selected = 0;

        WaveCatalogSO catalog = AssetDatabase.LoadAssetAtPath<WaveCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            _issues.Add((-1, $"Không tìm thấy catalog: {CatalogPath}"));
            return;
        }

        foreach (WaveSO wave in catalog.waves)
        {
            if (wave == null) continue;
            var draft = new WaveDraft
            {
                BuildTime = wave.buildPhaseDuration,
                CombatTime = wave.combatPhaseDuration,
                IsBoss = wave.isBossWave
            };
            foreach (SpawnGroup g in wave.spawnGroups)
            {
                draft.Groups.Add(new GroupDraft
                {
                    EnemyType = g.enemyType,
                    Count = g.count,
                    Interval = g.spawnInterval,
                    SpawnPoint = g.spawnPointIndex,
                    Path = g.pathIndex
                });
            }
            _waves.Add(draft);
        }

        Repaint();
    }

    private void SaveAll()
    {
        Validate();
        if (_issues.Count > 0)
        {
            return; // Lỗi hiển thị trong window, không ghi gì cả.
        }

        WaveCatalogSO catalog = AssetDatabase.LoadAssetAtPath<WaveCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            _issues.Add((-1, $"Không tìm thấy catalog: {CatalogPath}"));
            return;
        }

        int created = 0;
        var newList = new List<WaveSO>();
        for (int i = 0; i < _waves.Count; i++)
        {
            string assetPath = $"{WaveDataFolder}/DB_Wave_{i + 1}.asset";
            WaveSO wave = AssetDatabase.LoadAssetAtPath<WaveSO>(assetPath);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveSO>();
                AssetDatabase.CreateAsset(wave, assetPath);
                created++;
            }

            WaveDraft draft = _waves[i];
            wave.buildPhaseDuration = draft.BuildTime;
            wave.combatPhaseDuration = draft.CombatTime;
            wave.isBossWave = draft.IsBoss;
            wave.spawnGroups = new List<SpawnGroup>();
            foreach (GroupDraft g in draft.Groups)
            {
                wave.spawnGroups.Add(new SpawnGroup
                {
                    enemyType = g.EnemyType,
                    count = g.Count,
                    spawnInterval = g.Interval,
                    spawnPointIndex = g.SpawnPoint,
                    pathIndex = g.Path
                });
            }

            EditorUtility.SetDirty(wave);
            newList.Add(wave);
        }

        catalog.waves = newList;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        // Asset thừa khi số wave giảm: cảnh báo giống importer, không tự xóa file.
        foreach (string guid in AssetDatabase.FindAssets("t:WaveSO", new[] { WaveDataFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("DB_Wave_")
                && int.TryParse(name.Substring("DB_Wave_".Length), out int n)
                && n > _waves.Count)
            {
                Debug.LogWarning($"[WaveDesigner] {path} không còn trong danh sách — đã gỡ khỏi catalog. Xóa asset thủ công nếu đúng ý đồ.");
            }
        }

        ClearDirty();
        _csvExportPending = true; // asset đã đổi — cần bấm Export CSV để cập nhật sheet
        Debug.Log($"[WaveDesigner] Saved {_waves.Count} waves ({created} created). Bấm Export CSV để cập nhật sheet.");
    }

    // ---------------- Validation ----------------

    private void Validate()
    {
        _issues.Clear();

        if (_waves.Count == 0)
        {
            _issues.Add((-1, "Chưa có wave nào."));
        }

        for (int i = 0; i < _waves.Count; i++)
        {
            WaveDraft w = _waves[i];

            if (w.BuildTime <= 0f) _issues.Add((i, "Build time phải > 0."));
            if (w.CombatTime <= 0f) _issues.Add((i, "Combat time phải > 0."));
            if (w.Groups.Count == 0) _issues.Add((i, "Cần ít nhất 1 nhóm quái."));
            if (w.IsBoss && i != _waves.Count - 1)
            {
                // Cảnh báo mềm — vẫn cho save nhưng nhắc (giết boss = thắng ngay).
                Debug.LogWarning($"[WaveDesigner] Wave {i + 1} là boss nhưng không phải wave cuối — giết boss sẽ kết thúc trận ngay.");
            }

            for (int gi = 0; gi < w.Groups.Count; gi++)
            {
                GroupDraft g = w.Groups[gi];
                string gp = $"Nhóm {gi + 1}";
                if (g.Count <= 0) _issues.Add((i, $"{gp}: số lượng phải > 0."));
                if (g.Interval < 0f) _issues.Add((i, $"{gp}: interval phải >= 0."));

                if (_spawnPointLabels != null && (g.SpawnPoint < 0 || g.SpawnPoint >= _spawnPointLabels.Length))
                {
                    _issues.Add((i, $"{gp}: spawnPoint {g.SpawnPoint} ngoài phạm vi (0..{_spawnPointLabels.Length - 1})."));
                }
                if (_pathLabels != null && (g.Path < 0 || g.Path >= _pathLabels.Length))
                {
                    _issues.Add((i, $"{gp}: path {g.Path} ngoài phạm vi (0..{_pathLabels.Length - 1})."));
                }
                if (_mappedEnemyTypes != null && !_mappedEnemyTypes.Contains(g.EnemyType))
                {
                    _issues.Add((i, $"{gp}: '{g.EnemyType}' chưa gắn prefab trên WaveManager — không spawn được."));
                }
            }
        }
    }

    private bool WaveHasIssue(int waveIndex)
    {
        foreach ((int wi, string _) in _issues)
        {
            if (wi == waveIndex) return true;
        }
        return false;
    }

    /// <summary>Đọc tên spawn points / paths / các enemy đã gắn prefab từ WaveManager trong scene đang mở.</summary>
    private void RefreshSceneInfo()
    {
        _spawnPointLabels = null;
        _pathLabels = null;
        _mappedEnemyTypes = null;

        WaveManager manager = FindFirstObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            return;
        }

        var so = new SerializedObject(manager);
        SerializedProperty spawns = so.FindProperty("_spawnPoints");
        SerializedProperty paths = so.FindProperty("_enemyPaths");
        SerializedProperty mappings = so.FindProperty("_enemyPrefabMappings");
        if (spawns == null || paths == null || mappings == null)
        {
            return;
        }

        _spawnPointLabels = new string[spawns.arraySize];
        for (int i = 0; i < spawns.arraySize; i++)
        {
            var t = spawns.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            _spawnPointLabels[i] = $"{i} · {(t != null ? t.name.Replace("Spawn_", "") : "?")}";
        }

        _pathLabels = new string[paths.arraySize];
        for (int i = 0; i < paths.arraySize; i++)
        {
            Object p = paths.GetArrayElementAtIndex(i).objectReferenceValue;
            _pathLabels[i] = $"{i} · {(p != null ? p.name.Replace("Path_", "") : "?")}";
        }

        _mappedEnemyTypes = new HashSet<EnemyType>();
        for (int i = 0; i < mappings.arraySize; i++)
        {
            SerializedProperty element = mappings.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("prefab").objectReferenceValue != null)
            {
                _mappedEnemyTypes.Add((EnemyType)element.FindPropertyRelative("enemyType").intValue);
            }
        }
    }

    // ---------------- GUI ----------------

    private void EnsureStyles()
    {
        if (_stylesReady) return;
        _stylesReady = true;

        _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 17 };
        _subtitleStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, normal = { textColor = TextMuted } };
        _sidebarTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        _sidebarSubStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = TextMuted } };
        _badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        _columnHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = TextMuted } };
    }

    private void OnGUI()
    {
        EnsureStyles();
        HandleShortcuts();

        DrawToolbar();

        float topY = EditorStyles.toolbar.fixedHeight;
        DrawCsvPathRow(new Rect(0f, topY, position.width, PathRowHeight));
        topY += PathRowHeight;

        Rect content = new Rect(0f, topY, position.width, position.height - topY - 22f);
        Rect sidebar = new Rect(content.x, content.y, SidebarWidth, content.height);
        Rect detail = new Rect(content.x + SidebarWidth, content.y, content.width - SidebarWidth, content.height);

        EditorGUI.DrawRect(sidebar, SidebarBg);
        EditorGUI.DrawRect(new Rect(sidebar.xMax - 1f, sidebar.y, 1f, sidebar.height), new Color(0f, 0f, 0f, 0.35f));
        EditorGUI.DrawRect(detail, PanelBg);

        DrawSidebar(sidebar);
        DrawDetail(detail);
        DrawStatusBar(new Rect(0f, position.height - 22f, position.width, 22f));
    }

    private void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.control && e.keyCode == KeyCode.S)
        {
            SaveAll();
            e.Use();
            Repaint();
        }
    }

    // ---------------- Toolbar + status bar ----------------

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Nút Save nhuộm accent khi có thay đổi — đập vào mắt là biết cần bấm.
        Color oldBg = GUI.backgroundColor;
        if (_dirty) GUI.backgroundColor = Accent;
        if (GUILayout.Button(new GUIContent(_dirty ? " Save •" : " Save", "Ghi asset (Ctrl+S). CSV export riêng bằng nút Export CSV."), EditorStyles.toolbarButton, GUILayout.Width(70f)))
        {
            SaveAll();
        }
        GUI.backgroundColor = oldBg;

        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60f)))
        {
            if (!_dirty || EditorUtility.DisplayDialog("Bỏ thay đổi?", "Đang có chỉnh sửa chưa Save. Nạp lại từ asset sẽ mất các thay đổi này.", "Nạp lại", "Hủy"))
            {
                RefreshSceneInfo();
                LoadFromAssets();
            }
        }

        GUILayout.Space(6f);

        if (GUILayout.Button(new GUIContent("Import CSV", "Nạp CSV (theo đường dẫn ô bên dưới) vào asset rồi load lại tool"), EditorStyles.toolbarButton, GUILayout.Width(85f)))
        {
            if (!_dirty || EditorUtility.DisplayDialog("Bỏ thay đổi?", "Đang có chỉnh sửa chưa Save. Import CSV sẽ ghi đè bằng nội dung sheet.", "Import", "Hủy"))
            {
                WaveSheetImporter.Import(_csvPath);
                RefreshSceneInfo();
                LoadFromAssets(); // đặt _csvExportPending = false
            }
        }

        // Export CSV tách riêng khỏi Save — nhuộm vàng khi asset đã Save mà sheet chưa cập nhật.
        if (_csvExportPending) GUI.backgroundColor = new Color32(255, 213, 79, 255);
        if (GUILayout.Button(new GUIContent(_csvExportPending ? "Export CSV •" : "Export CSV", "Ghi asset hiện tại ra CSV (theo đường dẫn ô bên dưới)"), EditorStyles.toolbarButton, GUILayout.Width(90f)))
        {
            ExportCsv();
        }
        GUI.backgroundColor = oldBg;

        GUILayout.FlexibleSpace();

        int totalAll = 0;
        foreach (WaveDraft w in _waves) totalAll += w.TotalEnemies;
        GUILayout.Label($"{_waves.Count} waves · {totalAll} quái tổng", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>Export asset đã Save ra CSV tại _csvPath. Nếu còn thay đổi chưa Save thì hỏi Save trước.</summary>
    private void ExportCsv()
    {
        if (string.IsNullOrEmpty(_csvPath))
        {
            EditorUtility.DisplayDialog("Thiếu đường dẫn", "Chưa đặt đường dẫn CSV. Nhập hoặc bấm Browse ở ô \"CSV\".", "OK");
            return;
        }

        if (_dirty)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Có thay đổi chưa Save",
                "Export đọc từ asset đã Save nên sẽ KHÔNG gồm sửa đổi chưa Save. Bạn muốn làm gì?",
                "Save & Export", "Hủy", "Export bản đã lưu");
            if (choice == 1) return;                 // Hủy
            if (choice == 0)                         // Save & Export
            {
                SaveAll();
                if (_issues.Count > 0) return;       // Save lỗi validate → dừng
            }
            // choice == 2: export asset đã lưu như hiện trạng
        }

        WaveSheetImporter.Export(_csvPath);
        _csvExportPending = false;
    }

    private void DrawCsvPathRow(Rect rect)
    {
        EditorGUI.DrawRect(rect, SidebarBg);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.25f));

        const float pad = 8f, templateW = 74f, browseW = 74f, revealW = 84f, gap = 4f;
        Rect label = new Rect(rect.x + pad, rect.y + 4f, 30f, 16f);
        GUI.Label(label, new GUIContent("CSV", "Đường dẫn file CSV cho Import/Export (lưu trong EditorPrefs)"), EditorStyles.miniBoldLabel);

        float fieldX = label.xMax + gap;
        float fieldW = rect.width - fieldX - pad - templateW - browseW - revealW - gap * 3f;
        Rect field = new Rect(fieldX, rect.y + 3f, Mathf.Max(110f, fieldW), 18f);
        EditorGUI.BeginChangeCheck();
        string typed = EditorGUI.TextField(field, _csvPath);
        if (EditorGUI.EndChangeCheck())
        {
            _csvPath = typed;
            EditorPrefs.SetString(CsvPathPrefKey, _csvPath);
        }

        Rect template = new Rect(field.xMax + gap, rect.y + 3f, templateW, 18f);
        if (GUI.Button(template, new GUIContent("Template", "Tạo file CSV mẫu (header + hướng dẫn + ví dụ) để bắt đầu"), EditorStyles.miniButton))
        {
            string dir = string.IsNullOrEmpty(_csvPath) ? Application.dataPath : System.IO.Path.GetDirectoryName(_csvPath);
            string name = string.IsNullOrEmpty(_csvPath) ? "WaveSheet.csv" : System.IO.Path.GetFileName(_csvPath);
            string picked = EditorUtility.SaveFilePanel("Tạo file CSV template", dir, name, "csv");
            if (!string.IsNullOrEmpty(picked))
            {
                WaveSheetImporter.CreateTemplate(picked);
                _csvPath = picked;
                EditorPrefs.SetString(CsvPathPrefKey, _csvPath);
                EditorUtility.RevealInFinder(picked);
                GUI.FocusControl(null);
            }
        }

        Rect browse = new Rect(template.xMax + gap, rect.y + 3f, browseW, 18f);
        if (GUI.Button(browse, new GUIContent("Browse…", "Chọn hoặc đặt vị trí file CSV"), EditorStyles.miniButton))
        {
            string startDir = string.IsNullOrEmpty(_csvPath) ? Application.dataPath : System.IO.Path.GetDirectoryName(_csvPath);
            string startName = string.IsNullOrEmpty(_csvPath) ? "WaveSheet.csv" : System.IO.Path.GetFileName(_csvPath);
            string picked = EditorUtility.SaveFilePanel("Chọn / đặt file CSV wave", startDir, startName, "csv");
            if (!string.IsNullOrEmpty(picked))
            {
                _csvPath = picked;
                EditorPrefs.SetString(CsvPathPrefKey, _csvPath);
                GUI.FocusControl(null);
            }
        }

        Rect reveal = new Rect(browse.xMax + gap, rect.y + 3f, revealW, 18f);
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_csvPath) || !System.IO.File.Exists(_csvPath)))
        {
            if (GUI.Button(reveal, new GUIContent("Mở thư mục", "Hiện file CSV trong Explorer"), EditorStyles.miniButton))
            {
                EditorUtility.RevealInFinder(_csvPath);
            }
        }
    }

    private void DrawStatusBar(Rect rect)
    {
        EditorGUI.DrawRect(rect, SidebarBg);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0f, 0f, 0f, 0.35f));

        Rect left = new Rect(rect.x + 8f, rect.y + 3f, rect.width * 0.6f, 16f);
        if (_issues.Count > 0)
        {
            GUI.Label(left, EditorGUIUtility.TrTextContentWithIcon($" {_issues.Count} lỗi — sửa hết mới Save được", "console.erroricon.sml"), EditorStyles.miniLabel);
        }
        else if (_spawnPointLabels == null)
        {
            GUI.Label(left, EditorGUIUtility.TrTextContentWithIcon(" Không thấy WaveManager — mở SampleScene để có popup cổng/đường + validate đủ", "console.warnicon.sml"), EditorStyles.miniLabel);
            Rect btn = new Rect(rect.xMax - 120f, rect.y + 2f, 112f, 18f);
            if (GUI.Button(btn, "Mở SampleScene", EditorStyles.miniButton))
            {
                if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SampleScenePath);
                    RefreshSceneInfo();
                    Repaint();
                }
            }
            return;
        }
        else if (_csvExportPending)
        {
            var warnStyle = new GUIStyle(EditorStyles.miniLabel);
            warnStyle.normal.textColor = new Color32(255, 213, 79, 255);
            GUI.Label(left, "● Asset đã Save — bấm Export CSV để cập nhật sheet", warnStyle);
        }
        else
        {
            var okStyle = new GUIStyle(EditorStyles.miniLabel);
            okStyle.normal.textColor = new Color32(108, 203, 95, 255);
            GUI.Label(left, "✔ Hợp lệ — Save ghi asset, Export CSV ghi sheet", okStyle);
        }

        Rect right = new Rect(rect.xMax - 200f, rect.y + 3f, 192f, 16f);
        var rightStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = TextMuted } };
        GUI.Label(right, _dirty ? "chưa save" : "đã đồng bộ", rightStyle);
    }

    // ---------------- Sidebar ----------------

    private void DrawSidebar(Rect rect)
    {
        GUILayout.BeginArea(rect);
        _sidebarScroll = EditorGUILayout.BeginScrollView(_sidebarScroll);

        int maxEnemies = 1;
        foreach (WaveDraft w in _waves) maxEnemies = Mathf.Max(maxEnemies, w.TotalEnemies);

        for (int i = 0; i < _waves.Count; i++)
        {
            DrawSidebarItem(i, maxEnemies);
        }

        GUILayout.Space(6f);

        // Thêm wave: copy từ wave cuối (thường muốn tăng dần từ nền cũ), không có thì tạo mặc định.
        Rect addRect = GUILayoutUtility.GetRect(0f, 30f, GUILayout.ExpandWidth(true));
        addRect.x += 10f; addRect.width -= 20f;
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = Accent;
        if (GUI.Button(addRect, "+  Thêm Wave"))
        {
            WaveDraft template = _waves.Count > 0 ? _waves[_waves.Count - 1].Clone() : NewDefaultWave();
            template.IsBoss = false;
            _waves.Add(template);
            _selected = _waves.Count - 1;
            MarkDirty();
        }
        GUI.backgroundColor = oldBg;

        GUILayout.Space(8f);
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawSidebarItem(int index, int maxEnemies)
    {
        WaveDraft wave = _waves[index];
        Rect rect = GUILayoutUtility.GetRect(0f, SidebarItemHeight, GUILayout.ExpandWidth(true));
        bool selected = index == _selected;
        bool hover = rect.Contains(Event.current.mousePosition);

        // Click chọn wave.
        if (Event.current.type == EventType.MouseDown && hover)
        {
            _selected = index;
            GUI.FocusControl(null);
            Event.current.Use();
            Repaint();
        }

        if (selected)
        {
            EditorGUI.DrawRect(rect, new Color(Accent.r, Accent.g, Accent.b, 0.16f));
        }
        else if (hover)
        {
            EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.04f));
        }

        // Vạch accent bên trái: tím = thường, đỏ = boss.
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), wave.IsBoss ? BossColor : (selected ? Accent : new Color(1f, 1f, 1f, 0.12f)));

        // Tiêu đề + badge.
        Rect title = new Rect(rect.x + 12f, rect.y + 6f, rect.width - 70f, 16f);
        GUI.Label(title, $"Wave {index + 1}", _sidebarTitleStyle);

        float badgeX = rect.xMax - 8f;
        if (WaveHasIssue(index))
        {
            Rect err = new Rect(badgeX - 16f, rect.y + 6f, 16f, 16f);
            GUI.Label(err, EditorGUIUtility.TrIconContent("console.erroricon.sml"));
            badgeX -= 20f;
        }
        if (wave.IsBoss)
        {
            Rect badge = new Rect(badgeX - 38f, rect.y + 6f, 38f, 15f);
            EditorGUI.DrawRect(badge, BossColor);
            GUI.Label(badge, "BOSS", _badgeStyle);
        }

        // Dòng phụ: tổng quái · nhóm · thời gian.
        Rect sub = new Rect(rect.x + 12f, rect.y + 23f, rect.width - 20f, 14f);
        GUI.Label(sub, $"{wave.TotalEnemies} quái · {wave.Groups.Count} nhóm · {wave.BuildTime:0}s + {wave.CombatTime:0}s", _sidebarSubStyle);

        // Thanh cường độ (tổng quái so với wave đông nhất) — nhìn lướt là thấy nhịp khó dần.
        Rect barBg = new Rect(rect.x + 12f, rect.yMax - 9f, rect.width - 24f, 3f);
        EditorGUI.DrawRect(barBg, new Color(1f, 1f, 1f, 0.07f));
        float frac = Mathf.Clamp01((float)wave.TotalEnemies / maxEnemies);
        EditorGUI.DrawRect(new Rect(barBg.x, barBg.y, barBg.width * frac, barBg.height), wave.IsBoss ? BossColor : Accent);

        // Kẻ phân cách dưới.
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.25f));
    }

    private static WaveDraft NewDefaultWave()
    {
        var wave = new WaveDraft();
        wave.Groups.Add(new GroupDraft());
        return wave;
    }

    // ---------------- Detail pane ----------------

    private void DrawDetail(Rect rect)
    {
        GUILayout.BeginArea(new Rect(rect.x + 14f, rect.y + 10f, rect.width - 28f, rect.height - 14f));

        if (_waves.Count == 0)
        {
            GUILayout.FlexibleSpace();
            var empty = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 13 };
            GUILayout.Label("Chưa có wave nào — bấm  + Thêm Wave  ở cột trái để bắt đầu.", empty);
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
            return;
        }

        _selected = Mathf.Clamp(_selected, 0, _waves.Count - 1);
        WaveDraft wave = _waves[_selected];

        _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
        EditorGUI.BeginChangeCheck();

        DrawDetailHeader(wave);
        GUILayout.Space(10f);
        DrawTimingSection(wave);
        GUILayout.Space(14f);
        DrawGroupTable(wave);
        DrawWaveIssues();

        if (EditorGUI.EndChangeCheck())
        {
            MarkDirty();
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawDetailHeader(WaveDraft wave)
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label($"Wave {_selected + 1}", _titleStyle, GUILayout.Height(24f));
        if (wave.IsBoss)
        {
            Rect badge = GUILayoutUtility.GetRect(44f, 16f, GUILayout.Width(44f));
            badge.y += 5f;
            EditorGUI.DrawRect(badge, BossColor);
            GUI.Label(badge, "BOSS", _badgeStyle);
        }

        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(_selected == 0))
        {
            if (GUILayout.Button(new GUIContent("▲", "Chuyển lên (đổi chỗ với wave trước)"), GUILayout.Width(26f), GUILayout.Height(22f)))
            {
                (_waves[_selected - 1], _waves[_selected]) = (_waves[_selected], _waves[_selected - 1]);
                _selected--;
                MarkDirty();
            }
        }
        using (new EditorGUI.DisabledScope(_selected == _waves.Count - 1))
        {
            if (GUILayout.Button(new GUIContent("▼", "Chuyển xuống"), GUILayout.Width(26f), GUILayout.Height(22f)))
            {
                (_waves[_selected + 1], _waves[_selected]) = (_waves[_selected], _waves[_selected + 1]);
                _selected++;
                MarkDirty();
            }
        }
        if (GUILayout.Button(new GUIContent("Nhân bản", "Chèn bản copy ngay sau wave này"), GUILayout.Width(72f), GUILayout.Height(22f)))
        {
            _waves.Insert(_selected + 1, wave.Clone());
            _selected++;
            MarkDirty();
        }

        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(BossColor.r, BossColor.g, BossColor.b, 0.85f);
        if (GUILayout.Button("Xóa", GUILayout.Width(48f), GUILayout.Height(22f))
            && EditorUtility.DisplayDialog("Xóa wave?", $"Xóa Wave {_selected + 1}? Các wave sau sẽ dồn số lên.", "Xóa", "Hủy"))
        {
            _waves.RemoveAt(_selected);
            _selected = Mathf.Clamp(_selected, 0, _waves.Count - 1);
            MarkDirty();
        }
        GUI.backgroundColor = oldBg;

        EditorGUILayout.EndHorizontal();

        int spawnSeconds = 0;
        foreach (GroupDraft g in wave.Groups) spawnSeconds += Mathf.CeilToInt(g.Count * g.Interval);
        GUILayout.Label($"{wave.TotalEnemies} quái · {wave.Groups.Count} nhóm · spawn hết trong ~{spawnSeconds}s", _subtitleStyle);
    }

    private void DrawTimingSection(WaveDraft wave)
    {
        GUILayout.Label("THỜI GIAN & LOẠI WAVE", _columnHeaderStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        EditorGUIUtility.labelWidth = 62f;
        wave.BuildTime = EditorGUILayout.FloatField(new GUIContent("Build (s)", "Thời gian chuẩn bị trước wave"), wave.BuildTime, GUILayout.MaxWidth(160f));
        GUILayout.Space(12f);
        EditorGUIUtility.labelWidth = 74f;
        wave.CombatTime = EditorGUILayout.FloatField(new GUIContent("Combat (s)", "Thời gian tối đa của trận đánh — hết quái sớm thì kết thúc sớm"), wave.CombatTime, GUILayout.MaxWidth(172f));
        EditorGUIUtility.labelWidth = 0f;

        GUILayout.FlexibleSpace();
        wave.IsBoss = EditorGUILayout.ToggleLeft(new GUIContent("Boss wave", "Giết boss = thắng ngay (trừ endless mode)"), wave.IsBoss, GUILayout.Width(90f));

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawGroupTable(WaveDraft wave)
    {
        GUILayout.Label("NHÓM QUÁI", _columnHeaderStyle);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Header cột — canh theo đúng công thức chia cột của row bên dưới.
        Rect header = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
        GroupColumns cols = ComputeColumns(header);
        GUI.Label(cols.Enemy, "Loại quái", _columnHeaderStyle);
        GUI.Label(cols.Count, "Số lượng", _columnHeaderStyle);
        GUI.Label(cols.Interval, "Giãn cách (s)", _columnHeaderStyle);
        GUI.Label(cols.Spawn, "Cổng spawn", _columnHeaderStyle);
        GUI.Label(cols.Path, "Đường đi", _columnHeaderStyle);

        int removeGroup = -1;
        for (int gi = 0; gi < wave.Groups.Count; gi++)
        {
            GroupDraft g = wave.Groups[gi];
            Rect row = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            if (gi % 2 == 1)
            {
                EditorGUI.DrawRect(row, RowAlt);
            }

            cols = ComputeColumns(row);

            // Chấm màu nhận diện loại quái.
            Color dot = EnemyColors.TryGetValue(g.EnemyType, out Color c) ? c : Color.gray;
            EditorGUI.DrawRect(new Rect(cols.Dot.x, cols.Dot.y + 7f, 9f, 9f), dot);

            g.EnemyType = (EnemyType)EditorGUI.EnumPopup(Pad(cols.Enemy), g.EnemyType);
            bool unmapped = _mappedEnemyTypes != null && !_mappedEnemyTypes.Contains(g.EnemyType);
            if (unmapped)
            {
                GUI.Label(new Rect(cols.Enemy.xMax - 18f, cols.Enemy.y, 18f, 18f),
                    EditorGUIUtility.TrIconContent("console.warnicon.sml", "Loại quái này chưa gắn prefab trên WaveManager — không spawn được"));
            }

            g.Count = EditorGUI.IntField(Pad(cols.Count), g.Count);
            g.Interval = EditorGUI.FloatField(Pad(cols.Interval), g.Interval);

            if (_spawnPointLabels != null)
            {
                g.SpawnPoint = EditorGUI.Popup(Pad(cols.Spawn), g.SpawnPoint, _spawnPointLabels);
                g.Path = EditorGUI.Popup(Pad(cols.Path), g.Path, _pathLabels);
            }
            else
            {
                g.SpawnPoint = EditorGUI.IntField(Pad(cols.Spawn), g.SpawnPoint);
                g.Path = EditorGUI.IntField(Pad(cols.Path), g.Path);
            }

            if (GUI.Button(new Rect(cols.Delete.x, cols.Delete.y + 3f, 20f, 18f), new GUIContent("✕", "Xóa nhóm này"), EditorStyles.miniButton))
            {
                removeGroup = gi;
            }
        }

        if (removeGroup >= 0)
        {
            wave.Groups.RemoveAt(removeGroup);
            MarkDirty();
        }

        GUILayout.Space(4f);
        if (GUILayout.Button("+  Thêm nhóm quái", GUILayout.Height(24f), GUILayout.Width(150f)))
        {
            GroupDraft template = wave.Groups.Count > 0 ? wave.Groups[wave.Groups.Count - 1].Clone() : new GroupDraft();
            wave.Groups.Add(template);
            MarkDirty();
        }
        GUILayout.Space(2f);

        EditorGUILayout.EndVertical();
    }

    private void DrawWaveIssues()
    {
        bool any = false;
        var sb = new System.Text.StringBuilder();
        foreach ((int wi, string msg) in _issues)
        {
            if (wi == _selected || wi == -1)
            {
                sb.AppendLine(msg);
                any = true;
            }
        }

        if (any)
        {
            GUILayout.Space(8f);
            EditorGUILayout.HelpBox(sb.ToString().TrimEnd(), MessageType.Error);
        }
    }

    // ---------------- Table column math ----------------

    private struct GroupColumns
    {
        public Rect Dot, Enemy, Count, Interval, Spawn, Path, Delete;
    }

    /// <summary>Chia cột cho bảng nhóm quái — header và row dùng chung để luôn thẳng hàng.</summary>
    private static GroupColumns ComputeColumns(Rect row)
    {
        const float dotW = 14f, countW = 64f, intervalW = 84f, deleteW = 24f, gap = 6f;
        float flexible = row.width - dotW - countW - intervalW - deleteW - gap * 6f;
        float enemyW = Mathf.Max(90f, flexible * 0.38f);
        float spawnW = Mathf.Max(80f, flexible * 0.31f);
        float pathW = Mathf.Max(80f, flexible * 0.31f);

        float x = row.x;
        GroupColumns c;
        c.Dot = new Rect(x, row.y, dotW, row.height); x += dotW + gap;
        c.Enemy = new Rect(x, row.y, enemyW, row.height); x += enemyW + gap;
        c.Count = new Rect(x, row.y, countW, row.height); x += countW + gap;
        c.Interval = new Rect(x, row.y, intervalW, row.height); x += intervalW + gap;
        c.Spawn = new Rect(x, row.y, spawnW, row.height); x += spawnW + gap;
        c.Path = new Rect(x, row.y, pathW, row.height); x += pathW + gap;
        c.Delete = new Rect(x, row.y, deleteW, row.height);
        return c;
    }

    /// <summary>Thu control lại giữa row 24px cho thoáng.</summary>
    private static Rect Pad(Rect r) => new Rect(r.x, r.y + 3f, r.width, 18f);
}
#endif
