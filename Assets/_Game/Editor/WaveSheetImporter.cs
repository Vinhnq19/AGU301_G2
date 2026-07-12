#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Wave;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sheet → SO pipeline: Docs/WaveSheet.csv là source of truth cho wave data.
/// Import parse + validate CSV rồi tạo/cập nhật in-place DB_Wave_N.asset (giữ GUID,
/// không vỡ reference) và dựng lại list DB_WaveCatalog theo thứ tự. Export sinh lại
/// CSV từ asset hiện có. Chi tiết: Docs/WAVE_DATA_PIPELINE_PLAN.md.
///
/// Menu: Tools > Waves > Export Current Waves to CSV / Import Wave Sheet (CSV)
/// </summary>
public static class WaveSheetImporter
{
    private const string WaveDataFolder = "Assets/_Game/Generated/Data/WaveData";
    private const string CatalogPath = WaveDataFolder + "/DB_WaveCatalog.asset";
    private const string CsvHeader = "wave,buildTime,combatTime,isBoss,enemyType,count,interval,spawnPoint,path";
    // Cột growth (tùy chọn, cột 10): hệ số nhân count mỗi wave khi cột wave là range "A-B".
    // Ví dụ: wave=1-9, count=10, growth=1.2 → wave 1: 10, wave 2: 12, wave 3: 14 (round), ...
    private const string CsvHeaderWithGrowth = CsvHeader + ",growth";

    /// <summary>Đường dẫn CSV mặc định (Docs/WaveSheet.csv). Wave Designer có thể trỏ path khác qua ô "CSV".</summary>
    public static string DefaultCsvPath =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "WaveSheet.csv"));

    // ---------------- Export ----------------

    [MenuItem("Tools/Waves/Export Current Waves to CSV")]
    public static void Export() => Export(DefaultCsvPath);

    /// <summary>Export catalog hiện tại ra CSV tại <paramref name="csvPath"/> (đường dẫn tuyệt đối).</summary>
    public static void Export(string csvPath)
    {
        if (string.IsNullOrEmpty(csvPath))
        {
            Debug.LogError("[WaveSheetImporter] Export: đường dẫn CSV rỗng.");
            return;
        }

        WaveCatalogSO catalog = AssetDatabase.LoadAssetAtPath<WaveCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[WaveSheetImporter] Catalog not found: {CatalogPath}");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(CsvHeaderWithGrowth);

        for (int i = 0; i < catalog.waves.Count; i++)
        {
            int waveNumber = i + 1;
            WaveSO wave = catalog.waves[i];
            if (wave == null)
            {
                Debug.LogWarning($"[WaveSheetImporter] Catalog slot {waveNumber} is null — skipped, sheet will fail continuity check on import.");
                continue;
            }

            if (wave.spawnGroups == null || wave.spawnGroups.Count == 0)
            {
                Debug.LogWarning($"[WaveSheetImporter] Wave {waveNumber} has no spawn groups — skipped, sheet will fail continuity check on import.");
                continue;
            }

            foreach (SpawnGroup g in wave.spawnGroups)
            {
                sb.AppendLine(string.Join(",",
                    waveNumber.ToString(CultureInfo.InvariantCulture),
                    wave.buildPhaseDuration.ToString(CultureInfo.InvariantCulture),
                    wave.combatPhaseDuration.ToString(CultureInfo.InvariantCulture),
                    wave.isBossWave ? "TRUE" : "FALSE",
                    g.enemyType.ToString(),
                    g.count.ToString(CultureInfo.InvariantCulture),
                    g.spawnInterval.ToString(CultureInfo.InvariantCulture),
                    g.spawnPointIndex.ToString(CultureInfo.InvariantCulture),
                    g.pathIndex.ToString(CultureInfo.InvariantCulture),
                    "")); // growth: export luôn ra dữ liệu phẳng, để trống = 1
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath));
        // BOM để Excel nhận UTF-8 (sheet có thể chứa tiếng Việt sau này).
        File.WriteAllText(csvPath, sb.ToString(), new UTF8Encoding(true));
        Debug.Log($"[WaveSheetImporter] Exported {catalog.waves.Count} waves to {csvPath}");
    }

    // ---------------- Template ----------------

    [MenuItem("Tools/Waves/Create CSV Template")]
    public static void CreateTemplate() => CreateTemplate(DefaultCsvPath);

    /// <summary>
    /// Ghi file CSV mẫu tại <paramref name="csvPath"/>: header + comment hướng dẫn (dòng '#' bị Import bỏ qua)
    /// + 3 dòng ví dụ (wave đơn, dải wave có growth, boss) — import được luôn để bắt đầu nhanh.
    /// </summary>
    public static void CreateTemplate(string csvPath)
    {
        if (string.IsNullOrEmpty(csvPath))
        {
            Debug.LogError("[WaveSheetImporter] CreateTemplate: đường dẫn CSV rỗng.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(CsvHeaderWithGrowth);
        sb.AppendLine("# Mỗi dòng = 1 nhóm quái; nhiều dòng cùng 'wave' gộp thành 1 wave.");
        sb.AppendLine("# wave: 'N' hoặc dải 'A-B' (vd 2-9). buildTime/combatTime tính bằng giây. isBoss: TRUE/FALSE.");
        sb.AppendLine($"# enemyType (theo tên): {string.Join(" | ", Enum.GetNames(typeof(EnemyType)))}");
        sb.AppendLine("# count: số con. interval: giây giữa 2 con. spawnPoint/path: chỉ số cổng/đường trong scene (0=North,1=East,2=West).");
        sb.AppendLine("# growth (cột cuối, tùy chọn): với dải 'A-B', count nhân growth mỗi wave (vd 1.2). Để trống = giữ nguyên.");
        sb.AppendLine("# --- 3 dòng ví dụ dưới import được ngay; sửa/xóa tùy ý ---");
        sb.AppendLine("1,30,90,FALSE,Runner,8,1.5,0,0,");
        sb.AppendLine("2-9,25,100,FALSE,Runner,10,1.2,0,0,1.15");
        sb.AppendLine("10,30,120,TRUE,RatKing,1,0,1,1,");

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath));
        File.WriteAllText(csvPath, sb.ToString(), new UTF8Encoding(true));
        Debug.Log($"[WaveSheetImporter] Created CSV template at {csvPath}");
    }

    // ---------------- Import ----------------

    private sealed class Row
    {
        public int Line;
        public int Wave;
        public float BuildTime;
        public float CombatTime;
        public bool IsBoss;
        public EnemyType EnemyType;
        public int Count;
        public float Interval;
        public int SpawnPoint;
        public int Path;
    }

    [MenuItem("Tools/Waves/Import Wave Sheet (CSV)")]
    public static void Import() => Import(DefaultCsvPath);

    /// <summary>Import CSV tại <paramref name="csvPath"/> (tuyệt đối) → validate → ghi asset + rebuild catalog.</summary>
    public static void Import(string csvPath)
    {
        if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath))
        {
            Debug.LogError($"[WaveSheetImporter] Sheet not found: {csvPath}. Run 'Tools > Waves > Export Current Waves to CSV' first to generate a template.");
            return;
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        List<Row> rows = ParseCsv(File.ReadAllLines(csvPath), errors);
        SortedDictionary<int, List<Row>> byWave = GroupAndValidate(rows, errors, warnings);

        foreach (string w in warnings)
        {
            Debug.LogWarning($"[WaveSheetImporter] {w}");
        }

        if (errors.Count > 0)
        {
            var report = new StringBuilder();
            report.AppendLine($"[WaveSheetImporter] Import FAILED — {errors.Count} error(s), no assets were written:");
            foreach (string e in errors)
            {
                report.AppendLine($"  - {e}");
            }
            Debug.LogError(report.ToString());
            return;
        }

        WriteAssets(byWave);
    }

    private static List<Row> ParseCsv(string[] lines, List<string> errors)
    {
        var rows = new List<Row>();
        bool headerSeen = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            int lineNumber = i + 1;

            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }

            if (!headerSeen)
            {
                string normalized = line.Replace(" ", "").ToLowerInvariant();
                if (normalized != CsvHeader.ToLowerInvariant()
                    && normalized != CsvHeaderWithGrowth.ToLowerInvariant())
                {
                    errors.Add($"line {lineNumber}: header mismatch. Expected '{CsvHeader}' (cột growth tùy chọn), got '{line}'.");
                    return rows;
                }
                headerSeen = true;
                continue;
            }

            string[] cols = line.Split(',');
            if (cols.Length != 9 && cols.Length != 10)
            {
                errors.Add($"line {lineNumber}: expected 9-10 columns, got {cols.Length}. (Nếu dùng dấu phẩy thập phân kiểu vi-VN thì đổi sang dấu chấm '.')");
                continue;
            }

            for (int c = 0; c < cols.Length; c++)
            {
                cols[c] = cols[c].Trim();
            }

            var row = new Row { Line = lineNumber };
            bool ok = true;
            ok &= ParseWaveRange(cols[0], lineNumber, errors, out int waveStart, out int waveEnd);
            ok &= ParseFloat(cols[1], "buildTime", lineNumber, errors, out row.BuildTime);
            ok &= ParseFloat(cols[2], "combatTime", lineNumber, errors, out row.CombatTime);
            ok &= ParseBool(cols[3], "isBoss", lineNumber, errors, out row.IsBoss);
            ok &= ParseEnemyType(cols[4], lineNumber, errors, out row.EnemyType);
            ok &= ParseInt(cols[5], "count", lineNumber, errors, out row.Count);
            ok &= ParseFloat(cols[6], "interval", lineNumber, errors, out row.Interval);
            ok &= ParseInt(cols[7], "spawnPoint", lineNumber, errors, out row.SpawnPoint);
            ok &= ParseInt(cols[8], "path", lineNumber, errors, out row.Path);

            float growth = 1f;
            if (cols.Length == 10 && cols[9].Length > 0)
            {
                ok &= ParseFloat(cols[9], "growth", lineNumber, errors, out growth);
                if (growth <= 0f)
                {
                    errors.Add($"line {lineNumber}: growth must be > 0 (got {growth.ToString(CultureInfo.InvariantCulture)}).");
                    ok = false;
                }
            }

            if (!ok)
            {
                continue;
            }

            if (row.Count <= 0)
            {
                // Check trước khi expand — expand clamp min 1 nên phải bắt count gốc <= 0 tại đây.
                errors.Add($"line {lineNumber}: count must be > 0 (got {row.Count}).");
                continue;
            }

            // Expand range: mỗi wave trong [start, end] một Row, count nhân growth^(w - start).
            for (int w = waveStart; w <= waveEnd; w++)
            {
                var expanded = new Row
                {
                    Line = lineNumber,
                    Wave = w,
                    BuildTime = row.BuildTime,
                    CombatTime = row.CombatTime,
                    IsBoss = row.IsBoss,
                    EnemyType = row.EnemyType,
                    Count = Mathf.Max(1, Mathf.RoundToInt(row.Count * Mathf.Pow(growth, w - waveStart))),
                    Interval = row.Interval,
                    SpawnPoint = row.SpawnPoint,
                    Path = row.Path
                };
                rows.Add(expanded);
            }
        }

        if (!headerSeen)
        {
            errors.Add("sheet is empty (no header line found).");
        }

        return rows;
    }

    private static SortedDictionary<int, List<Row>> GroupAndValidate(
        List<Row> rows, List<string> errors, List<string> warnings)
    {
        var byWave = new SortedDictionary<int, List<Row>>();

        bool hasSceneLimits = TryGetSceneLimits(
            out int spawnPointCount, out int pathCount, out HashSet<EnemyType> mappedTypes);
        if (!hasSceneLimits)
        {
            warnings.Add("WaveManager not found in the open scene — skipping spawnPoint/path range and prefab-mapping checks. Open SampleScene for full validation.");
        }

        foreach (Row row in rows)
        {
            if (row.Wave < 1)
            {
                errors.Add($"line {row.Line}: wave must be >= 1 (got {row.Wave}).");
                continue;
            }
            if (row.BuildTime <= 0f)
            {
                errors.Add($"line {row.Line}: buildTime must be > 0 (got {row.BuildTime.ToString(CultureInfo.InvariantCulture)}).");
            }
            if (row.CombatTime <= 0f)
            {
                errors.Add($"line {row.Line}: combatTime must be > 0 (got {row.CombatTime.ToString(CultureInfo.InvariantCulture)}).");
            }
            if (row.Count <= 0)
            {
                errors.Add($"line {row.Line}: count must be > 0 (got {row.Count}).");
            }
            if (row.Interval < 0f)
            {
                errors.Add($"line {row.Line}: interval must be >= 0 (got {row.Interval.ToString(CultureInfo.InvariantCulture)}).");
            }
            if (row.SpawnPoint < 0)
            {
                errors.Add($"line {row.Line}: spawnPoint must be >= 0 (got {row.SpawnPoint}).");
            }
            if (row.Path < 0)
            {
                errors.Add($"line {row.Line}: path must be >= 0 (got {row.Path}).");
            }

            if (hasSceneLimits)
            {
                if (row.SpawnPoint >= spawnPointCount)
                {
                    errors.Add($"line {row.Line}: spawnPoint {row.SpawnPoint} out of range — scene has {spawnPointCount} spawn points (valid: 0..{spawnPointCount - 1}).");
                }
                if (row.Path >= pathCount)
                {
                    errors.Add($"line {row.Line}: path {row.Path} out of range — scene has {pathCount} enemy paths (valid: 0..{pathCount - 1}).");
                }
                if (!mappedTypes.Contains(row.EnemyType))
                {
                    errors.Add($"line {row.Line}: enemyType '{row.EnemyType}' has no prefab mapping on WaveManager (_enemyPrefabMappings).");
                }
            }

            if (!byWave.TryGetValue(row.Wave, out List<Row> group))
            {
                group = new List<Row>();
                byWave[row.Wave] = group;
            }
            group.Add(row);
        }

        // Các dòng cùng wave phải khớp buildTime/combatTime/isBoss với dòng đầu của wave đó.
        foreach (KeyValuePair<int, List<Row>> kv in byWave)
        {
            Row first = kv.Value[0];
            for (int i = 1; i < kv.Value.Count; i++)
            {
                Row r = kv.Value[i];
                if (!Mathf.Approximately(r.BuildTime, first.BuildTime)
                    || !Mathf.Approximately(r.CombatTime, first.CombatTime)
                    || r.IsBoss != first.IsBoss)
                {
                    errors.Add($"line {r.Line}: wave {kv.Key} has inconsistent buildTime/combatTime/isBoss (must match line {first.Line}).");
                }
            }
        }

        // Wave đánh số liên tục 1..N, không lủng.
        int expected = 1;
        foreach (int waveNumber in byWave.Keys)
        {
            if (waveNumber != expected)
            {
                errors.Add($"wave numbering must be contiguous starting at 1 — expected wave {expected}, found wave {waveNumber}.");
                break;
            }
            expected++;
        }

        // Boss nên là wave cuối (giết boss = thắng ngay).
        int lastWave = 0;
        foreach (int waveNumber in byWave.Keys)
        {
            lastWave = waveNumber;
        }
        foreach (KeyValuePair<int, List<Row>> kv in byWave)
        {
            if (kv.Value[0].IsBoss && kv.Key != lastWave)
            {
                warnings.Add($"wave {kv.Key} is a boss wave but not the last wave ({lastWave}) — killing the boss ends the game immediately.");
            }
        }

        if (byWave.Count == 0 && errors.Count == 0)
        {
            errors.Add("sheet contains no data rows.");
        }

        return byWave;
    }

    private static void WriteAssets(SortedDictionary<int, List<Row>> byWave)
    {
        WaveCatalogSO catalog = AssetDatabase.LoadAssetAtPath<WaveCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[WaveSheetImporter] Catalog not found: {CatalogPath}");
            return;
        }

        int created = 0;
        int updated = 0;
        int groupCount = 0;
        var newList = new List<WaveSO>();

        foreach (KeyValuePair<int, List<Row>> kv in byWave)
        {
            string assetPath = $"{WaveDataFolder}/DB_Wave_{kv.Key}.asset";
            WaveSO wave = AssetDatabase.LoadAssetAtPath<WaveSO>(assetPath);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveSO>();
                AssetDatabase.CreateAsset(wave, assetPath);
                created++;
            }
            else
            {
                updated++;
            }

            Row first = kv.Value[0];
            wave.buildPhaseDuration = first.BuildTime;
            wave.combatPhaseDuration = first.CombatTime;
            wave.isBossWave = first.IsBoss;
            wave.spawnGroups = new List<SpawnGroup>();
            foreach (Row r in kv.Value)
            {
                wave.spawnGroups.Add(new SpawnGroup
                {
                    enemyType = r.EnemyType,
                    count = r.Count,
                    spawnInterval = r.Interval,
                    spawnPointIndex = r.SpawnPoint,
                    pathIndex = r.Path
                });
                groupCount++;
            }

            EditorUtility.SetDirty(wave);
            newList.Add(wave);
        }

        catalog.waves = newList;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        // Asset thừa (wave bị xóa khỏi sheet): chỉ warning, không tự xóa file —
        // tránh dialog chặn automation; user tự xóa nếu đúng ý đồ.
        foreach (string guid in AssetDatabase.FindAssets("t:WaveSO", new[] { WaveDataFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("DB_Wave_")
                && int.TryParse(name.Substring("DB_Wave_".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                && !byWave.ContainsKey(n))
            {
                Debug.LogWarning($"[WaveSheetImporter] {path} is no longer in the sheet — removed from catalog. Delete the asset manually if intended.");
            }
        }

        Debug.Log($"[WaveSheetImporter] Import OK — {byWave.Count} waves ({groupCount} spawn groups): {updated} updated, {created} created. Catalog rebuilt.");
    }

    // ---------------- Scene limits ----------------

    private static bool TryGetSceneLimits(
        out int spawnPointCount, out int pathCount, out HashSet<EnemyType> mappedTypes)
    {
        spawnPointCount = 0;
        pathCount = 0;
        mappedTypes = null;

        WaveManager manager = UnityEngine.Object.FindFirstObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            return false;
        }

        var so = new SerializedObject(manager);
        SerializedProperty spawnPoints = so.FindProperty("_spawnPoints");
        SerializedProperty paths = so.FindProperty("_enemyPaths");
        SerializedProperty mappings = so.FindProperty("_enemyPrefabMappings");
        if (spawnPoints == null || paths == null || mappings == null)
        {
            return false;
        }

        spawnPointCount = spawnPoints.arraySize;
        pathCount = paths.arraySize;

        mappedTypes = new HashSet<EnemyType>();
        for (int i = 0; i < mappings.arraySize; i++)
        {
            SerializedProperty element = mappings.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("prefab").objectReferenceValue != null)
            {
                mappedTypes.Add((EnemyType)element.FindPropertyRelative("enemyType").intValue);
            }
        }

        return true;
    }

    // ---------------- Parse helpers ----------------

    /// <summary>Cột wave nhận "N" hoặc range "A-B" (vd "5-9" = sinh spawn group cho wave 5..9).</summary>
    private static bool ParseWaveRange(string s, int line, List<string> errors, out int start, out int end)
    {
        start = 0;
        end = 0;

        int dash = s.IndexOf('-');
        if (dash < 0)
        {
            if (!ParseInt(s, "wave", line, errors, out start))
            {
                return false;
            }
            end = start;
            return true;
        }

        string left = s.Substring(0, dash).Trim();
        string right = s.Substring(dash + 1).Trim();
        if (!int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)
            || !int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out end))
        {
            errors.Add($"line {line}: wave range '{s}' is not valid. Use 'N' or 'A-B' (e.g. 5-9).");
            return false;
        }

        if (start < 1 || end < start)
        {
            errors.Add($"line {line}: wave range '{s}' is not valid — need 1 <= A <= B.");
            return false;
        }

        return true;
    }

    private static bool ParseInt(string s, string column, int line, List<string> errors, out int value)
    {
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        errors.Add($"line {line}: {column} is not a valid integer (got '{s}').");
        return false;
    }

    private static bool ParseFloat(string s, string column, int line, List<string> errors, out float value)
    {
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        errors.Add($"line {line}: {column} is not a valid number (got '{s}'). Dùng dấu chấm '.' cho số thập phân.");
        return false;
    }

    private static bool ParseBool(string s, string column, int line, List<string> errors, out bool value)
    {
        if (bool.TryParse(s, out value))
        {
            return true;
        }
        errors.Add($"line {line}: {column} must be TRUE or FALSE (got '{s}').");
        return false;
    }

    private static bool ParseEnemyType(string s, int line, List<string> errors, out EnemyType value)
    {
        // Chỉ chấp nhận TÊN enum (không nhận số) để sheet dễ đọc và không lệ thuộc thứ tự enum.
        if (!int.TryParse(s, out _) && Enum.TryParse(s, ignoreCase: true, out value) && Enum.IsDefined(typeof(EnemyType), value))
        {
            return true;
        }
        value = default;
        errors.Add($"line {line}: enemyType '{s}' is not a valid EnemyType. Valid: {string.Join(", ", Enum.GetNames(typeof(EnemyType)))}.");
        return false;
    }
}
#endif
