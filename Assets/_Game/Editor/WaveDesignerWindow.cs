#if UNITY_EDITOR
using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Wave;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cửa sổ thiết kế wave trực quan — thay cho việc sửa tay asset hoặc CSV.
/// Sửa từng wave (build/combat time, boss), thêm/xóa/nhân bản wave, thêm/xóa nhóm quái.
/// SAVE = validate → ghi DB_Wave_N.asset + DB_WaveCatalog (giữ GUID) → TỰ ĐỘNG export
/// Docs/WaveSheet.csv để sheet và asset luôn đồng bộ. Import CSV = nạp sheet vào tool.
///
/// Menu: Tools > Waves > Wave Designer. Chi tiết pipeline: Docs/WAVE_DESIGN_GUIDE.md.
/// </summary>
public sealed class WaveDesignerWindow : EditorWindow
{
    private const string WaveDataFolder = "Assets/_Game/Generated/Data/WaveData";
    private const string CatalogPath = WaveDataFolder + "/DB_WaveCatalog.asset";

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
        public bool Foldout = true;

        public WaveDraft Clone()
        {
            var clone = new WaveDraft { BuildTime = BuildTime, CombatTime = CombatTime, IsBoss = IsBoss, Foldout = true };
            foreach (GroupDraft g in Groups) clone.Groups.Add(g.Clone());
            return clone;
        }
    }

    private readonly List<WaveDraft> _waves = new();
    private readonly List<string> _errors = new();
    private Vector2 _scroll;
    private bool _dirty;

    // Thông tin scene phục vụ popup + validate (null nếu không mở được SampleScene).
    private string[] _spawnPointLabels;
    private string[] _pathLabels;
    private HashSet<EnemyType> _mappedEnemyTypes;

    [MenuItem("Tools/Waves/Wave Designer")]
    public static void Open()
    {
        var window = GetWindow<WaveDesignerWindow>("Wave Designer");
        window.minSize = new Vector2(520f, 400f);
        window.LoadFromAssets();
    }

    private void OnEnable()
    {
        RefreshSceneInfo();
        if (_waves.Count == 0)
        {
            LoadFromAssets();
        }
    }

    // ---------------- Load / Save ----------------

    private void LoadFromAssets()
    {
        _waves.Clear();
        _errors.Clear();
        _dirty = false;

        WaveCatalogSO catalog = AssetDatabase.LoadAssetAtPath<WaveCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            _errors.Add($"Không tìm thấy catalog: {CatalogPath}");
            return;
        }

        foreach (WaveSO wave in catalog.waves)
        {
            if (wave == null) continue;
            var draft = new WaveDraft
            {
                BuildTime = wave.buildPhaseDuration,
                CombatTime = wave.combatPhaseDuration,
                IsBoss = wave.isBossWave,
                Foldout = false
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
        if (_errors.Count > 0)
        {
            return; // Lỗi hiển thị trong window, không ghi gì cả.
        }

        WaveCatalogSO catalog = AssetDatabase.LoadAssetAtPath<WaveCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            _errors.Add($"Không tìm thấy catalog: {CatalogPath}");
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

        // Đồng bộ CSV mỗi lần Save — sheet luôn khớp với asset.
        WaveSheetImporter.Export();

        _dirty = false;
        Debug.Log($"[WaveDesigner] Saved {_waves.Count} waves ({created} created) + exported CSV.");
    }

    // ---------------- Validation ----------------

    private void Validate()
    {
        _errors.Clear();

        if (_waves.Count == 0)
        {
            _errors.Add("Chưa có wave nào.");
        }

        for (int i = 0; i < _waves.Count; i++)
        {
            WaveDraft w = _waves[i];
            string prefix = $"Wave {i + 1}";

            if (w.BuildTime <= 0f) _errors.Add($"{prefix}: buildTime phải > 0.");
            if (w.CombatTime <= 0f) _errors.Add($"{prefix}: combatTime phải > 0.");
            if (w.Groups.Count == 0) _errors.Add($"{prefix}: cần ít nhất 1 nhóm quái.");
            if (w.IsBoss && i != _waves.Count - 1)
            {
                // Cảnh báo mềm — vẫn cho save nhưng nhắc (giết boss = thắng ngay).
                Debug.LogWarning($"[WaveDesigner] {prefix} là boss nhưng không phải wave cuối — giết boss sẽ kết thúc trận ngay.");
            }

            for (int gi = 0; gi < w.Groups.Count; gi++)
            {
                GroupDraft g = w.Groups[gi];
                string gp = $"{prefix} nhóm {gi + 1}";
                if (g.Count <= 0) _errors.Add($"{gp}: count phải > 0.");
                if (g.Interval < 0f) _errors.Add($"{gp}: interval phải >= 0.");

                if (_spawnPointLabels != null && (g.SpawnPoint < 0 || g.SpawnPoint >= _spawnPointLabels.Length))
                {
                    _errors.Add($"{gp}: spawnPoint {g.SpawnPoint} ngoài phạm vi (0..{_spawnPointLabels.Length - 1}).");
                }
                if (_pathLabels != null && (g.Path < 0 || g.Path >= _pathLabels.Length))
                {
                    _errors.Add($"{gp}: path {g.Path} ngoài phạm vi (0..{_pathLabels.Length - 1}).");
                }
                if (_mappedEnemyTypes != null && !_mappedEnemyTypes.Contains(g.EnemyType))
                {
                    _errors.Add($"{gp}: '{g.EnemyType}' chưa gắn prefab trên WaveManager — không spawn được.");
                }
            }
        }
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
            _spawnPointLabels[i] = $"{i} - {(t != null ? t.name.Replace("Spawn_", "") : "?")}";
        }

        _pathLabels = new string[paths.arraySize];
        for (int i = 0; i < paths.arraySize; i++)
        {
            Object p = paths.GetArrayElementAtIndex(i).objectReferenceValue;
            _pathLabels[i] = $"{i} - {(p != null ? p.name.Replace("Path_", "") : "?")}";
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

    private void OnGUI()
    {
        DrawToolbar();

        if (_spawnPointLabels == null)
        {
            EditorGUILayout.HelpBox("Không tìm thấy WaveManager trong scene đang mở — hãy mở SampleScene để có popup chọn cổng spawn/đường đi và validate đầy đủ.", MessageType.Warning);
        }

        if (_errors.Count > 0)
        {
            EditorGUILayout.HelpBox(string.Join("\n", _errors), MessageType.Error);
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        int removeWave = -1;
        int duplicateWave = -1;
        int moveUp = -1;
        int moveDown = -1;

        for (int i = 0; i < _waves.Count; i++)
        {
            DrawWave(i, ref removeWave, ref duplicateWave, ref moveUp, ref moveDown);
        }

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("+  Thêm Wave", GUILayout.Height(28f)))
        {
            // Wave mới copy từ wave cuối (thường muốn tăng dần từ nền cũ), không có thì tạo mặc định.
            WaveDraft template = _waves.Count > 0 ? _waves[_waves.Count - 1].Clone() : NewDefaultWave();
            template.IsBoss = false;
            _waves.Add(template);
            _dirty = true;
        }

        EditorGUILayout.EndScrollView();

        // Áp thao tác cấu trúc sau vòng draw để không phá layout.
        if (removeWave >= 0) { _waves.RemoveAt(removeWave); _dirty = true; }
        if (duplicateWave >= 0) { _waves.Insert(duplicateWave + 1, _waves[duplicateWave].Clone()); _dirty = true; }
        if (moveUp > 0) { (_waves[moveUp - 1], _waves[moveUp]) = (_waves[moveUp], _waves[moveUp - 1]); _dirty = true; }
        if (moveDown >= 0 && moveDown < _waves.Count - 1) { (_waves[moveDown + 1], _waves[moveDown]) = (_waves[moveDown], _waves[moveDown + 1]); _dirty = true; }
    }

    private static WaveDraft NewDefaultWave()
    {
        var wave = new WaveDraft();
        wave.Groups.Add(new GroupDraft());
        return wave;
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUI.enabled = _dirty || _errors.Count > 0;
        if (GUILayout.Button(_dirty ? "Save*  (asset + CSV)" : "Save  (asset + CSV)", EditorStyles.toolbarButton, GUILayout.Width(140f)))
        {
            SaveAll();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Reload từ asset", EditorStyles.toolbarButton, GUILayout.Width(110f)))
        {
            if (!_dirty || EditorUtility.DisplayDialog("Bỏ thay đổi?", "Đang có chỉnh sửa chưa Save. Nạp lại từ asset sẽ mất các thay đổi này.", "Nạp lại", "Hủy"))
            {
                RefreshSceneInfo();
                LoadFromAssets();
            }
        }

        if (GUILayout.Button("Import CSV → Tool", EditorStyles.toolbarButton, GUILayout.Width(120f)))
        {
            if (!_dirty || EditorUtility.DisplayDialog("Bỏ thay đổi?", "Đang có chỉnh sửa chưa Save. Import CSV sẽ ghi đè bằng nội dung sheet.", "Import", "Hủy"))
            {
                WaveSheetImporter.Import();
                RefreshSceneInfo();
                LoadFromAssets();
            }
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label($"{_waves.Count} waves{(_dirty ? "  •  CHƯA SAVE" : string.Empty)}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawWave(int index, ref int removeWave, ref int duplicateWave, ref int moveUp, ref int moveDown)
    {
        WaveDraft wave = _waves[index];

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Header: foldout + tóm tắt + nút thao tác.
        EditorGUILayout.BeginHorizontal();
        int totalEnemies = 0;
        foreach (GroupDraft g in wave.Groups) totalEnemies += g.Count;
        string title = $"Wave {index + 1}{(wave.IsBoss ? "  ⚔ BOSS" : string.Empty)}   —   {totalEnemies} quái / {wave.Groups.Count} nhóm";
        wave.Foldout = EditorGUILayout.Foldout(wave.Foldout, title, true, EditorStyles.foldoutHeader);

        if (GUILayout.Button("▲", GUILayout.Width(24f))) moveUp = index;
        if (GUILayout.Button("▼", GUILayout.Width(24f))) moveDown = index;
        if (GUILayout.Button("Nhân bản", GUILayout.Width(70f))) duplicateWave = index;
        if (GUILayout.Button("Xóa", GUILayout.Width(44f)) && EditorUtility.DisplayDialog("Xóa wave?", $"Xóa Wave {index + 1}? Các wave sau sẽ dồn số lên.", "Xóa", "Hủy")) removeWave = index;
        EditorGUILayout.EndHorizontal();

        if (!wave.Foldout)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUI.BeginChangeCheck();

        // Phase times + boss.
        EditorGUILayout.BeginHorizontal();
        wave.BuildTime = EditorGUILayout.FloatField(new GUIContent("Build (s)", "Thời gian chuẩn bị trước wave"), wave.BuildTime);
        wave.CombatTime = EditorGUILayout.FloatField(new GUIContent("Combat (s)", "Thời gian tối đa của trận đánh — hết quái sớm thì kết thúc sớm"), wave.CombatTime);
        wave.IsBoss = EditorGUILayout.ToggleLeft(new GUIContent("Boss", "Giết boss = thắng ngay (trừ endless mode)"), wave.IsBoss, GUILayout.Width(60f));
        EditorGUILayout.EndHorizontal();

        // Groups.
        int removeGroup = -1;
        for (int gi = 0; gi < wave.Groups.Count; gi++)
        {
            GroupDraft g = wave.Groups[gi];
            EditorGUILayout.BeginHorizontal();

            g.EnemyType = (EnemyType)EditorGUILayout.EnumPopup(g.EnemyType, GUILayout.Width(90f));
            bool unmapped = _mappedEnemyTypes != null && !_mappedEnemyTypes.Contains(g.EnemyType);
            if (unmapped)
            {
                GUILayout.Label(new GUIContent("⚠", "Loại quái này chưa gắn prefab — không spawn được"), GUILayout.Width(18f));
            }

            GUILayout.Label("x", GUILayout.Width(12f));
            g.Count = EditorGUILayout.IntField(g.Count, GUILayout.Width(44f));
            GUILayout.Label(new GUIContent("mỗi", "Giây giữa 2 con"), GUILayout.Width(28f));
            g.Interval = EditorGUILayout.FloatField(g.Interval, GUILayout.Width(40f));
            GUILayout.Label("s", GUILayout.Width(12f));

            if (_spawnPointLabels != null)
            {
                g.SpawnPoint = EditorGUILayout.Popup(g.SpawnPoint, _spawnPointLabels, GUILayout.Width(85f));
                g.Path = EditorGUILayout.Popup(g.Path, _pathLabels, GUILayout.Width(85f));
            }
            else
            {
                g.SpawnPoint = EditorGUILayout.IntField(g.SpawnPoint, GUILayout.Width(40f));
                g.Path = EditorGUILayout.IntField(g.Path, GUILayout.Width(40f));
            }

            if (GUILayout.Button("−", GUILayout.Width(22f))) removeGroup = gi;
            EditorGUILayout.EndHorizontal();
        }

        if (removeGroup >= 0)
        {
            wave.Groups.RemoveAt(removeGroup);
            _dirty = true;
        }

        if (GUILayout.Button("+ Thêm nhóm quái", GUILayout.Width(140f)))
        {
            GroupDraft template = wave.Groups.Count > 0 ? wave.Groups[wave.Groups.Count - 1].Clone() : new GroupDraft();
            wave.Groups.Add(template);
            _dirty = true;
        }

        if (EditorGUI.EndChangeCheck())
        {
            _dirty = true;
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
