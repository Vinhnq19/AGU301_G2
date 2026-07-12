using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Enemy;
using Cysharp.Threading.Tasks;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Enemy;
using DungeonBuilder.Networking.Pool;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Wave
{
    public sealed class WaveManager : NetworkBehaviour
    {
        [System.Serializable]
        public struct EnemyPrefabMapping
        {
            public EnemyType enemyType;
            public NetworkObject prefab;
        }

        [SerializeField] private WaveCatalogSO _waveCatalog;
        public WaveCatalogSO WaveCatalog => _waveCatalog;
        [SerializeField] private EnemyPrefabMapping[] _enemyPrefabMappings;
        [SerializeField] private EnemyPath[] _enemyPaths;
        public EnemyPath[] EnemyPaths => _enemyPaths;
        [SerializeField] private Transform _coreTarget;
        [SerializeField] private Transform[] _spawnPoints;

        private readonly NetworkVariable<int> _currentWave = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _totalWaves = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> TotalWavesNetVar => _totalWaves;

        /// <summary>Wave hiện tại (1-based, 0 = chưa bắt đầu wave nào). Đọc được trên mọi peer.</summary>
        public int CurrentWave => _currentWave.Value;
        private readonly NetworkVariable<float> _phaseCountdown = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<GamePhase> _gamePhase = new(GamePhase.Build, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _allWavesCompleted = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool _isGameEnded = false;

        private EventBus _eventBus;
        private INetworkPool _pool;
        private IWaveProvider _waveProvider;
        private readonly HashSet<ulong> _activeEnemyIds = new();
        private readonly Dictionary<EnemyType, NetworkObject> _prefabLookup = new();
        private bool _isSpawningWave = false;
        private bool _skipBuildPhaseRequested = false;

        [Inject]
        public void Construct(EventBus eventBus, INetworkPool pool, IWaveProvider waveProvider)
        {
            _eventBus = eventBus;
            _pool = pool;
            _waveProvider = waveProvider;
        }

        // Fallback khi không được inject (scene thiếu scope): đọc thẳng catalog như trước.
        private void EnsureProvider()
        {
            _waveProvider ??= new SoWaveProvider(_waveCatalog);
        }

        public override void OnNetworkSpawn()
        {
            EnsureProvider();
            _phaseCountdown.OnValueChanged += HandlePhaseCountdownChanged;
            _allWavesCompleted.OnValueChanged += HandleWavesCompleted;
            _gamePhase.OnValueChanged += HandleGamePhaseChanged;
            _currentWave.OnValueChanged += HandleCurrentWaveChanged;

            if (IsServer)
            {
                _eventBus.OnGameEnded += HandleGameEndedEvent;
                _eventBus.OnEnemyKilled += HandleEnemyKilled;
                InitializePrefabLookup();
                _totalWaves.Value = _waveProvider.WaveCount;
                RunWaveLoopAsync().Forget();
            }
        }

        public override void OnNetworkDespawn()
        {
            _phaseCountdown.OnValueChanged -= HandlePhaseCountdownChanged;
            _allWavesCompleted.OnValueChanged -= HandleWavesCompleted;
            _gamePhase.OnValueChanged -= HandleGamePhaseChanged;
            _currentWave.OnValueChanged -= HandleCurrentWaveChanged;
            if (IsServer && _eventBus != null)
            {
                _eventBus.OnGameEnded -= HandleGameEndedEvent;
                _eventBus.OnEnemyKilled -= HandleEnemyKilled;
            }
        }

        private void InitializePrefabLookup()
        {
            _prefabLookup.Clear();
            if (_enemyPrefabMappings != null)
            {
                foreach (var mapping in _enemyPrefabMappings)
                {
                    if (mapping.prefab != null)
                    {
                        _prefabLookup[mapping.enemyType] = mapping.prefab;
                    }
                }
            }
        }

        private async UniTaskVoid RunWaveLoopAsync()
        {
            try
            {
                while (IsServer && IsSpawned && IsNetworkReady() && !_isGameEnded)
                {
                    float buildDuration = 30f; // Default fallback
                    float combatDuration = 120f; // Default fallback
                    int waveCount = _waveProvider.WaveCount;
                    if (waveCount > 0)
                    {
                        // Wave vượt catalog dùng config của wave cuối (giữ semantics cũ).
                        WaveData waveConfig = _waveProvider.GetWave(Mathf.Min(_currentWave.Value, waveCount - 1));
                        buildDuration = waveConfig.buildPhaseDuration;
                        combatDuration = waveConfig.combatPhaseDuration;
                    }

                    _gamePhase.Value = GamePhase.Build;
                    await CountdownAsync(buildDuration);
                    if (!IsNetworkReady() || _isGameEnded)
                    {
                        return;
                    }

                    _currentWave.Value++;

                    // Khong block timer de timer chay song song voi luc spawn
                    SpawnWaveAsync(_currentWave.Value).Forget();
                    if (!IsNetworkReady() || _isGameEnded)
                    {
                        return;
                    }

                    await CountdownCombatAsync(combatDuration);

                    if (_currentWave.Value >= _waveProvider.WaveCount)
                    {
                        _allWavesCompleted.Value = true;
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask CountdownCombatAsync(float duration)
        {
            float remaining = duration;
            while (remaining > 0f && (_isSpawningWave || !AllEnemiesDead()) && !_isGameEnded)
            {
                if (!IsNetworkReady())
                {
                    return;
                }

                _phaseCountdown.Value = remaining;

                // Yield frame by frame for 1 second OR until all enemies are dead
                float elapsed = 0f;
                while (elapsed < 1f && (_isSpawningWave || !AllEnemiesDead()) && !_isGameEnded)
                {
                    await UniTask.Yield(cancellationToken: destroyCancellationToken);
                    elapsed += Time.deltaTime;
                }

                remaining -= 1f;
            }

            if (IsNetworkReady())
            {
                _phaseCountdown.Value = 0f;
            }
        }

        private async UniTask CountdownAsync(float duration)
        {
            float remaining = duration;
            while (remaining > 0f && !_isGameEnded && !_skipBuildPhaseRequested)
            {
                if (!IsNetworkReady())
                {
                    return;
                }

                _phaseCountdown.Value = remaining;
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: destroyCancellationToken);
                remaining -= 1f;
            }

            _skipBuildPhaseRequested = false;

            if (IsNetworkReady())
            {
                _phaseCountdown.Value = 0f;
            }
        }

        /// <summary>
        /// Cheat host-only: đọc lại nguồn wave data (JSON override nếu có).
        /// Áp dụng từ wave KẾ TIẾP — không đụng wave đang chạy.
        /// </summary>
        public void ReloadWaveData()
        {
            if (!IsServer)
            {
                return;
            }

            EnsureProvider();
            _waveProvider.Reload();
            _totalWaves.Value = _waveProvider.WaveCount;
            Debug.Log($"[WaveManager] Wave data reloaded — {_waveProvider.WaveCount} waves (applies from next wave).");
        }

        /// <summary>Bat ky client nao cung co the yeu cau bo qua thoi gian chuan bi (Build phase). Server xac thuc va xu ly.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestSkipBuildPhaseServerRpc()
        {
            if (_gamePhase.Value != GamePhase.Build)
            {
                return;
            }

            _skipBuildPhaseRequested = true;
        }

        private async UniTask SpawnWaveAsync(int waveNumber)
        {
            _isSpawningWave = true;
            _activeEnemyIds.Clear();

            try
            {
                if (!IsNetworkReady() || _pool == null || _waveProvider == null || _waveProvider.WaveCount == 0 || _isGameEnded)
                {
                    return;
                }

            int waveIndex = waveNumber - 1;
            int totalWaveCount = _waveProvider.WaveCount;
            bool isFallback = waveIndex >= totalWaveCount;
            WaveData waveConfig = _waveProvider.GetWave(Mathf.Min(waveIndex, totalWaveCount - 1));

            if (waveConfig.spawnGroups == null)
            {
                return;
            }

            bool isFirstEnemy = true;

            foreach (var group in waveConfig.spawnGroups)
            {
                if (!IsNetworkReady())
                {
                    return;
                }

                if (!_prefabLookup.TryGetValue(group.enemyType, out NetworkObject prefab) || prefab == null)
                {
                    Debug.LogWarning($"[WaveManager] Prefab not found for EnemyType: {group.enemyType}");
                    continue;
                }

                int spawnCount = group.count;
                if (isFallback)
                {
                    spawnCount += (waveNumber - totalWaveCount);
                }

                for (int i = 0; i < spawnCount; i++)
                {
                    if (!IsNetworkReady() || _isGameEnded)
                    {
                        return;
                    }

                    if (isFirstEnemy && IsServer)
                    {
                        _gamePhase.Value = GamePhase.Combat;
                        isFirstEnemy = false;
                    }

                    Transform spawnPoint = GetSpawnPoint(group.spawnPointIndex);
                    Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
                    Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

                    NetworkObject enemyObj = _pool.Get(prefab, position, rotation);
                    if (enemyObj != null)
                    {
                        BaseEnemy enemy = enemyObj.GetComponent<BaseEnemy>();
                        if (enemy != null)
                        {
                            enemy.SetCoreTarget(_coreTarget);

                            if (_enemyPaths != null && group.pathIndex >= 0 && group.pathIndex < _enemyPaths.Length)
                            {
                                EnemyPath path = _enemyPaths[group.pathIndex];
                                if (path != null && path.Waypoints != null)
                                {
                                    enemy.SetPath(path.Waypoints);
                                }
                            }
                        }

                        if (!enemyObj.IsSpawned)
                        {
                            enemyObj.Spawn();
                        }

                        _activeEnemyIds.Add(enemyObj.NetworkObjectId);
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(group.spawnInterval), cancellationToken: destroyCancellationToken);
                }
            }

            if (isFirstEnemy && IsServer)
            {
                // Fallback if no enemies were spawned
                _gamePhase.Value = GamePhase.Combat;
            }
            }
            finally
            {
                _isSpawningWave = false;
            }
        }

        private Transform GetSpawnPoint(int index)
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                return null;
            }

            return _spawnPoints[index % _spawnPoints.Length];
        }

        private bool AllEnemiesDead()
        {
            if (_activeEnemyIds.Count == 0)
            {
                return true;
            }

            // Remove any enemies that are no longer spawned (returned to pool, force-despawned, etc.)
            _activeEnemyIds.RemoveWhere(id =>
                NetworkManager?.SpawnManager == null ||
                !NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id));

            return _activeEnemyIds.Count == 0;
        }

        private void HandlePhaseCountdownChanged(float previousValue, float newValue)
        {
            _eventBus?.RaisePhaseCountdownChanged(newValue);
        }

        private void HandleGamePhaseChanged(GamePhase previousValue, GamePhase newValue)
        {
            _eventBus?.RaiseGamePhaseChanged(newValue);
        }

        private void HandleCurrentWaveChanged(int previousValue, int newValue)
        {
            if (newValue <= 0) return;

            EnsureProvider();
            bool isBoss = false;
            int waveIndex = newValue - 1;
            int waveCount = _waveProvider.WaveCount;
            if (waveCount > 0)
            {
                isBoss = _waveProvider.GetWave(Mathf.Min(waveIndex, waveCount - 1)).isBossWave;
            }

            _eventBus?.RaiseWaveStarted(newValue, isBoss);
        }

        private void HandleGameEndedEvent(bool isWin)
        {
            _isGameEnded = true;
        }

        private void HandleEnemyKilled(EnemyType type, bool isBoss)
        {
            if (isBoss && IsServer)
            {
                Debug.Log($"[WaveManager] Boss killed! Ending game with victory.");
                _allWavesCompleted.Value = true; // Trigger win
            }
        }

        private void HandleWavesCompleted(bool _, bool isCompleted)
        {
            if (isCompleted)
                _eventBus?.RaiseGameEnded(true);
        }

        private bool IsNetworkReady()
        {
            return IsServer
                && IsSpawned
                && NetworkManager != null
                && NetworkManager.IsListening;
        }
    }
}
