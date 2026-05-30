using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Enemy;
using DungeonBuilder.Networking.Pool;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Wave
{
    public sealed class WaveManager : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float _buildPhaseDuration = 30f;
        [SerializeField, Min(0.1f)] private float _spawnInterval = 0.5f;
        [SerializeField, Min(1)] private int _baseEnemiesPerWave = 4;
        [SerializeField] private Transform _coreTarget;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private NetworkObject[] _enemyPrefabs;

        private readonly NetworkVariable<int> _currentWave = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _phaseCountdown = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<GamePhase> _gamePhase = new(GamePhase.Build, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private EventBus _eventBus;
        private INetworkPool _pool;
        private readonly HashSet<ulong> _activeEnemyIds = new();

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
                RunWaveLoopAsync().Forget();
            }
        }

        public override void OnNetworkDespawn()
        {
            _phaseCountdown.OnValueChanged -= HandlePhaseCountdownChanged;
        }

        private async UniTaskVoid RunWaveLoopAsync()
        {
            try
            {
                while (IsServer && IsSpawned && IsNetworkReady())
                {
                    _gamePhase.Value = GamePhase.Build;
                    await CountdownAsync(_buildPhaseDuration);
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

            if (!IsNetworkReady() || _pool == null || _enemyPrefabs == null || _enemyPrefabs.Length == 0)
            {
                return;
            }

            int count = Mathf.Max(1, _baseEnemiesPerWave + waveNumber - 1);

            for (int i = 0; i < count; i++)
            {
                if (!IsNetworkReady())
                {
                    return;
                }

                NetworkObject prefab = _enemyPrefabs[i % _enemyPrefabs.Length];
                Transform spawnPoint = GetSpawnPoint(i);
                Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
                Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

                NetworkObject enemy = _pool.Get(prefab, position, rotation);
                if (enemy != null)
                {
                    enemy.GetComponent<BaseEnemy>()?.SetCoreTarget(_coreTarget);
                    if (!enemy.IsSpawned)
                    {
                        enemy.Spawn();
                    }

                    _activeEnemyIds.Add(enemy.NetworkObjectId);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(_spawnInterval), cancellationToken: destroyCancellationToken);
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
