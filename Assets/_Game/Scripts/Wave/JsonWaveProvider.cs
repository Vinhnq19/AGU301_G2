using System;
using System.Collections.Generic;
using System.IO;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using UnityEngine;

namespace DungeonBuilder.Wave
{
    /// <summary>
    /// Lớp override cho dev/host: đọc StreamingAssets/waves.json nếu có, ngược lại
    /// delegate về provider fallback (SoWaveProvider). File lỗi/parse fail → bỏ toàn bộ
    /// JSON và dùng fallback (fail-safe, không dùng nửa vời). Reload() đọc lại file —
    /// dùng cho nút cheat "Reload Waves (JSON)", áp dụng từ wave kế tiếp.
    /// </summary>
    public sealed class JsonWaveProvider : IWaveProvider
    {
        [Serializable]
        private class SheetJson
        {
            public WaveJson[] waves;
        }

        [Serializable]
        private class WaveJson
        {
            public float buildTime;
            public float combatTime;
            public bool isBoss;
            public GroupJson[] spawnGroups;
        }

        [Serializable]
        private class GroupJson
        {
            public string enemyType;
            public int count;
            public float interval;
            public int spawnPoint;
            // path bỏ khỏi schema — auto = spawnPoint. Field 'path' cũ trong JSON (nếu có) bị JsonUtility bỏ qua.
        }

        private readonly IWaveProvider _fallback;
        private readonly string _path;
        private List<WaveData> _waves; // null = JSON không hoạt động → fallback

        public bool IsJsonActive => _waves != null;

        public JsonWaveProvider(IWaveProvider fallback)
        {
            _fallback = fallback;
            _path = Path.Combine(Application.streamingAssetsPath, "waves.json");
            Reload();
        }

        public int WaveCount => _waves != null ? _waves.Count : _fallback.WaveCount;

        public WaveData GetWave(int index)
        {
            return _waves != null ? _waves[index] : _fallback.GetWave(index);
        }

        public void Reload()
        {
            if (!File.Exists(_path))
            {
                _waves = null;
                Debug.Log("[WaveProvider] Using WaveCatalog SO (no waves.json override).");
                return;
            }

            try
            {
                SheetJson sheet = JsonUtility.FromJson<SheetJson>(File.ReadAllText(_path));
                if (sheet?.waves == null || sheet.waves.Length == 0)
                {
                    throw new Exception("'waves' array is missing or empty.");
                }

                var parsed = new List<WaveData>(sheet.waves.Length);
                for (int i = 0; i < sheet.waves.Length; i++)
                {
                    WaveJson w = sheet.waves[i];
                    if (w.buildTime <= 0f || w.combatTime <= 0f)
                    {
                        throw new Exception($"wave {i + 1}: buildTime/combatTime must be > 0.");
                    }
                    if (w.spawnGroups == null || w.spawnGroups.Length == 0)
                    {
                        throw new Exception($"wave {i + 1}: spawnGroups is missing or empty.");
                    }

                    var groups = new List<SpawnGroup>(w.spawnGroups.Length);
                    foreach (GroupJson g in w.spawnGroups)
                    {
                        if (!Enum.TryParse(g.enemyType, ignoreCase: true, out EnemyType type)
                            || !Enum.IsDefined(typeof(EnemyType), type))
                        {
                            throw new Exception($"wave {i + 1}: enemyType '{g.enemyType}' is not a valid EnemyType.");
                        }
                        if (g.count <= 0 || g.interval < 0f || g.spawnPoint < 0)
                        {
                            throw new Exception($"wave {i + 1}: invalid count/interval/spawnPoint values.");
                        }

                        groups.Add(new SpawnGroup
                        {
                            enemyType = type,
                            count = g.count,
                            spawnInterval = g.interval,
                            spawnPointIndex = g.spawnPoint,
                            pathIndex = g.spawnPoint // path auto = cổng spawn
                        });
                    }

                    parsed.Add(new WaveData
                    {
                        buildPhaseDuration = w.buildTime,
                        combatPhaseDuration = w.combatTime,
                        isBossWave = w.isBoss,
                        spawnGroups = groups
                    });
                }

                _waves = parsed;
                Debug.Log($"[WaveProvider] Using JSON override ({parsed.Count} waves) from {_path}");
            }
            catch (Exception e)
            {
                _waves = null;
                Debug.LogError($"[WaveProvider] Failed to load waves.json — falling back to WaveCatalog SO. {e.Message}");
            }
        }
    }
}
