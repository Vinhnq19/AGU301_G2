using System;
using System.Collections.Generic;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Data;
using DungeonBuilder.Networking.Pool;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Harvesting
{
    /// <summary>
    /// Sinh các node tài nguyên (cây/quặng) lên map mỗi khi một wave bắt đầu.
    /// Vị trí spawn lấy từ một danh sách "slot" cấu hình sẵn trong scene; mỗi slot
    /// chỉ chứa tối đa một node tại một thời điểm. Loại tài nguyên được chọn theo
    /// trọng số phụ thuộc số wave (loại hiếm mở khóa muộn và tăng tỉ lệ dần).
    /// Chạy server-authoritative: chỉ server quyết định spawn, node tự replicate.
    /// </summary>
    public sealed class ResourceSpawner : NetworkBehaviour
    {
        [SerializeField] private ResourceSpawnConfigSO _config;

        [Tooltip("Danh sách vị trí spawn cấu hình sẵn trong scene. Mỗi Transform là một slot.")]
        [SerializeField] private Transform[] _spawnSlots;

        private EventBus _eventBus;
        private INetworkPool _pool;

        // slotIndex -> node đang chiếm slot đó. Slot không có key = đang trống.
        private readonly Dictionary<int, NetworkObject> _occupiedSlots = new();
        private System.Random _random;

        [Inject]
        public void Construct(EventBus eventBus, INetworkPool pool)
        {
            _eventBus = eventBus;
            _pool = pool;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            // Seed cố định theo NetworkObjectId để các lần chạy có thể tái lập khi debug.
            _random = new System.Random(unchecked((int)NetworkObjectId * 31 + 17));

            if (_eventBus != null)
            {
                _eventBus.OnWaveStarted += HandleWaveStarted;
            }
            else
            {
                Debug.LogError($"[{nameof(ResourceSpawner)}] EventBus chưa được inject. Kiểm tra đăng ký trong GameLifetimeScope.", this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _eventBus != null)
            {
                _eventBus.OnWaveStarted -= HandleWaveStarted;
            }

            _occupiedSlots.Clear();
        }

        private void HandleWaveStarted(int wave, bool isBossWave)
        {
            if (!IsServer || _config == null || _pool == null)
            {
                DBLog.Warning("resource.spawn.blocked", $"Resource spawn blocked. server={IsServer}, configNull={_config == null}, poolNull={_pool == null}.", 1f, this);
                return;
            }

            if (_spawnSlots == null || _spawnSlots.Length == 0)
            {
                DBLog.Warning("resource.spawn.no-slots", "Không có spawn slot nào được cấu hình.", 1f, this);
                return;
            }

            PruneFreedSlots();

            int requested = _config.GetNodeCount(wave);
            List<int> emptySlots = CollectEmptySlots();
            int spawnCount = Mathf.Min(requested, emptySlots.Count);

            if (spawnCount < requested)
            {
                DBLog.Warning("resource.spawn.slot-limited", $"Yêu cầu spawn {requested} node nhưng chỉ còn {emptySlots.Count} slot trống ở wave {wave}.", 1f, this);
            }

            int spawned = 0;
            for (int i = 0; i < spawnCount; i++)
            {
                int pickIndex = _random.Next(emptySlots.Count);
                int slotIndex = emptySlots[pickIndex];
                emptySlots.RemoveAt(pickIndex);

                if (TrySpawnNodeInSlot(slotIndex, wave))
                {
                    spawned++;
                }
            }

            DBLog.Info($"resource.spawn.wave.{wave}", $"Spawned {spawned}/{requested} resource node(s) ở wave {wave}. emptyBefore={emptySlots.Count + spawned}.", 0.5f, this);
        }

        private bool TrySpawnNodeInSlot(int slotIndex, int wave)
        {
            if (!TryPickEntry(wave, out ResourceSpawnEntry entry) || entry.nodePrefab == null)
            {
                DBLog.Warning("resource.spawn.no-entry", $"Không chọn được loại tài nguyên hợp lệ ở wave {wave}.", 1f, this);
                return false;
            }

            Transform slot = _spawnSlots[slotIndex];
            Vector3 position = slot != null ? slot.position : transform.position;
            Quaternion rotation = slot != null ? slot.rotation : Quaternion.identity;

            NetworkObject nodeObject = _pool.Get(entry.nodePrefab, position, rotation);
            if (nodeObject == null)
            {
                DBLog.Warning("resource.spawn.pool-null", $"Pool trả về null cho {entry.resourceType} ở slot {slotIndex}.", 1f, this);
                return false;
            }

            if (!nodeObject.IsSpawned)
            {
                nodeObject.Spawn();
            }

            HarvestableNode node = nodeObject.GetComponent<HarvestableNode>();
            if (node == null)
            {
                DBLog.Warning("resource.spawn.no-node", $"Prefab {entry.nodePrefab.name} thiếu HarvestableNode.", 1f, nodeObject);
                _pool.Return(nodeObject);
                return false;
            }

            node.Configure(entry.nodeData, slotIndex, this);
            _occupiedSlots[slotIndex] = nodeObject;
            return true;
        }

        /// <summary>
        /// Node gọi lại khi bị cạn kiệt/bị phá để giải phóng slot cho wave sau.
        /// </summary>
        public void NotifyNodeDepleted(int slotIndex)
        {
            if (!IsServer)
            {
                return;
            }

            _occupiedSlots.Remove(slotIndex);
        }

        private void PruneFreedSlots()
        {
            // Dọn các node đã despawn (về pool) mà chưa kịp notify.
            _tmpRemove.Clear();
            foreach (KeyValuePair<int, NetworkObject> pair in _occupiedSlots)
            {
                if (pair.Value == null || !pair.Value.IsSpawned)
                {
                    _tmpRemove.Add(pair.Key);
                }
            }

            foreach (int slotIndex in _tmpRemove)
            {
                _occupiedSlots.Remove(slotIndex);
            }
        }

        private readonly List<int> _tmpRemove = new();

        private List<int> CollectEmptySlots()
        {
            var empty = new List<int>(_spawnSlots.Length);
            for (int i = 0; i < _spawnSlots.Length; i++)
            {
                if (!_occupiedSlots.ContainsKey(i))
                {
                    empty.Add(i);
                }
            }

            return empty;
        }

        private bool TryPickEntry(int wave, out ResourceSpawnEntry chosen)
        {
            chosen = default;

            if (_config.entries == null || _config.entries.Count == 0)
            {
                return false;
            }

            float totalWeight = 0f;
            foreach (ResourceSpawnEntry entry in _config.entries)
            {
                totalWeight += _config.GetWeight(entry, wave);
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            double roll = _random.NextDouble() * totalWeight;
            float cumulative = 0f;
            foreach (ResourceSpawnEntry entry in _config.entries)
            {
                cumulative += _config.GetWeight(entry, wave);
                if (roll <= cumulative)
                {
                    chosen = entry;
                    return true;
                }
            }

            // Fallback do sai số floating point: chọn entry hợp lệ cuối cùng.
            for (int i = _config.entries.Count - 1; i >= 0; i--)
            {
                if (_config.GetWeight(_config.entries[i], wave) > 0f)
                {
                    chosen = _config.entries[i];
                    return true;
                }
            }

            return false;
        }
    }
}
