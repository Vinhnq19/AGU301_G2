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
        [SerializeField] private EnemyPrefabMapping[] _enemyPrefabMappings;
        [SerializeField] private EnemyPath[] _enemyPaths;
        [SerializeField] private Transform _coreTarget;
        [SerializeField] private Transform[] _spawnPoints;

        private readonly NetworkVariable<int> _currentWave = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _phaseCountdown = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<GamePhase> _gamePhase = new(GamePhase.Build, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private EventBus _eventBus;
        private INetworkPool _pool;
        private readonly HashSet<ulong> _activeEnemyIds = new();
        private readonly Dictionary<EnemyType, NetworkObject> _prefabLookup = new();

        [Inject]
        public void Construct(EventBus eventBus, INetworkPool pool)
        {
            _eventBus = eventBus;
            _pool = pool;
        }

        public override void OnNetworkSpawn()
        {
            _phaseCountdown.OnValueChanged += HandlePhaseCountdownChanged;

            if (IsServer)
            {
                InitializePrefabLookup();
                RunWaveLoopAsync().Forget();
            }
        }

        public override void OnNetworkDespawn()
        {
            _phaseCountdown.OnValueChanged -= HandlePhaseCountdownChanged;
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
                while (IsServer && IsSpawned && IsNetworkReady())
                {
                    float buildDuration = 30f; // Default fallback
                    if (_waveCatalog != null && _waveCatalog.waves != null && _currentWave.Value < _waveCatalog.waves.Count)
                    {
                        var waveConfig = _waveCatalog.waves[_currentWave.Value];
                        if (waveConfig != null)
                        {
                            buildDuration = waveConfig.buildPhaseDuration;
                        }
                    }
                    else if (_waveCatalog != null && _waveCatalog.waves != null && _waveCatalog.waves.Count > 0)
                    {
                        var lastWave = _waveCatalog.waves[_waveCatalog.waves.Count - 1];
                        if (lastWave != null)
                        {
                            buildDuration = lastWave.buildPhaseDuration;
                        }
                    }

                    _gamePhase.Value = GamePhase.Build;
                    await CountdownAsync(buildDuration);
                    if (!IsNetworkReady())
                    {
                        return;
                    }

                    _currentWave.Value++;
                    _gamePhase.Value = GamePhase.Combat;
                    _eventBus?.RaiseWaveStarted(_currentWave.Value);

                    await SpawnWaveAsync(_currentWave.Value);
                    if (!IsNetworkReady())
                    {
                        return;
                    }

                    await UniTask.WaitUntil(AllEnemiesDead, cancellationToken: destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask CountdownAsync(float duration)
        {
            float remaining = duration;
            while (remaining > 0f)
            {
                if (!IsNetworkReady())
                {
                    return;
                }

                _phaseCountdown.Value = remaining;
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: destroyCancellationToken);
                remaining -= 1f;
            }

            if (IsNetworkReady())
            {
                _phaseCountdown.Value = 0f;
            }
        }

        private async UniTask SpawnWaveAsync(int waveNumber)
        {
            _activeEnemyIds.Clear();

            if (!IsNetworkReady() || _pool == null || _waveCatalog == null || _waveCatalog.waves == null || _waveCatalog.waves.Count == 0)
            {
                return;
            }

            WaveSO waveConfig = null;
            int waveIndex = waveNumber - 1;
            bool isFallback = false;

            if (waveIndex < _waveCatalog.waves.Count)
            {
                waveConfig = _waveCatalog.waves[waveIndex];
            }
            else
            {
                waveConfig = _waveCatalog.waves[_waveCatalog.waves.Count - 1];
                isFallback = true;
            }

            if (waveConfig == null || waveConfig.spawnGroups == null)
            {
                return;
            }

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
                    spawnCount += (waveNumber - _waveCatalog.waves.Count);
                }

                for (int i = 0; i < spawnCount; i++)
                {
                    if (!IsNetworkReady())
                    {
                        return;
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

        private bool IsNetworkReady()
        {
            return IsServer
                && IsSpawned
                && NetworkManager != null
                && NetworkManager.IsListening;
        }
    }
}
