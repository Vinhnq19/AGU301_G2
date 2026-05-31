using Assets._Game.Scripts.Data;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Networking;
using DungeonBuilder.Networking.Pool;
using System;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Building
{
    public sealed class BuildingController : NetworkBehaviour
    {
        [SerializeField] private TowerDataSO[] _towerData;
        [SerializeField] private TowerPrefabEntry[] _towerPrefabs;

        private SharedResourceManager _sharedResources;
        private GridManager _grid;
        private INetworkPool _pool;

        [Inject]
        public void Construct(SharedResourceManager sharedResources, GridManager grid, INetworkPool pool)
        {
            _sharedResources = sharedResources;
            _grid = grid;
            _pool = pool;
        }

        // ─── Place ──────────────────────────────────────────────────────

        public void RequestPlaceTower(Vector2Int gridPosition, TowerType towerType)
        {
            DBLog.Info($"build.send.{OwnerClientId}", $"Place tower intent sent. grid={gridPosition}, type={towerType}.", 0.25f, this);
            PlaceTowerServerRpc(gridPosition, towerType);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PlaceTowerServerRpc(Vector2Int gridPosition, TowerType towerType, RpcParams rpcParams = default)
        {
            DBLog.Info($"build.recv.{rpcParams.Receive.SenderClientId}", $"Place tower intent received. sender={rpcParams.Receive.SenderClientId}, grid={gridPosition}, type={towerType}.", 0.25f, this);

            TowerDataSO data = GetTowerData(towerType);
            NetworkObject prefab = GetTowerPrefab(towerType);

            if (data == null || prefab == null || _sharedResources == null || _grid == null || _pool == null)
            {
                DBLog.Warning($"build.reject.refs.{towerType}", $"Place tower rejected: missing refs.", 1f, this);
                return;
            }

            if (!_grid.IsValidPlacement(gridPosition))
            {
                DBLog.Warning($"build.reject.grid.{gridPosition}", $"Place tower rejected: invalid grid {gridPosition}.", 0.5f, this);
                return;
            }

            if (!_sharedResources.CanAfford(ResourceType.Wood, data.woodCost)
                || !_sharedResources.CanAfford(ResourceType.Ore, data.oreCost))
            {
                DBLog.Warning($"build.reject.cost.{towerType}", $"Place tower rejected: not enough resources.", 0.5f, this);
                return;
            }

            if (!_sharedResources.TrySpend(ResourceType.Wood, data.woodCost)) return;
            if (!_sharedResources.TrySpend(ResourceType.Ore, data.oreCost))
            {
                _sharedResources.AddResource(ResourceType.Wood, data.woodCost);
                return;
            }

            if (!_grid.PlaceTower(gridPosition, data))
            {
                _sharedResources.AddResource(ResourceType.Wood, data.woodCost);
                _sharedResources.AddResource(ResourceType.Ore, data.oreCost);
                return;
            }

            NetworkObject tower = _pool.Get(prefab, _grid.GridToWorld(gridPosition), Quaternion.identity);
            if (tower == null)
            {
                _grid.ClearTower(gridPosition);
                _sharedResources.AddResource(ResourceType.Wood, data.woodCost);
                _sharedResources.AddResource(ResourceType.Ore, data.oreCost);
                DBLog.Warning($"build.reject.pool.{gridPosition}", $"Tower placement failed: pool returned null.", 1f, this);
                return;
            }

            // Guard giong BaseTower.FireAt(): neu pool da spawn roi thi khong Spawn() lai.
            // Neu goi Spawn() tren object da spawn → exception → SetTowerNetworkObjectId khong chay
            // → TowerNetworkObjectId = 0 → Remove khong tim duoc tower de despawn.
            if (!tower.IsSpawned)
            {
                tower.Spawn();
            }

            // Ghi NetworkObjectId vào GridCell sau Spawn (ID được gán khi Spawn)
            _grid.SetTowerNetworkObjectId(gridPosition, tower.NetworkObjectId);

            // Thông báo tower về vị trí của nó
            BaseTower baseTower = tower.GetComponent<BaseTower>();
            baseTower?.OnPlaced(gridPosition);

            DBLog.Info($"build.accept.{gridPosition}", $"Tower placed. type={towerType}, grid={gridPosition}, networkId={tower.NetworkObjectId}.", 0.25f, tower);
        }

        // ─── Upgrade ────────────────────────────────────────────────────

        /// <summary>
        /// Client gọi để gửi yêu cầu nâng cấp tower tại gridPosition lên server.
        /// </summary>
        public void RequestUpgradeTower(Vector2Int gridPosition)
        {
            DBLog.Info($"upgrade.send.{gridPosition}", $"Upgrade request sent. grid={gridPosition}.", 0.25f, this);
            UpgradeTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void UpgradeTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (_grid == null || _sharedResources == null)
            {
                DBLog.Warning("upgrade.reject.refs", "Upgrade rejected: missing refs.", 1f, this);
                return;
            }

            if (!_grid.TryGetCell(gridPosition, out GridCell cell))
            {
                DBLog.Warning($"upgrade.reject.grid.{gridPosition}", $"Upgrade rejected: no tower at {gridPosition}.", 0.5f, this);
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cell.TowerNetworkObjectId, out NetworkObject netObj))
            {
                DBLog.Warning($"upgrade.reject.notfound.{cell.TowerNetworkObjectId}", "Upgrade rejected: tower object not found.", 1f, this);
                return;
            }

            BaseTower tower = netObj.GetComponent<BaseTower>();
            if (tower == null || !tower.CanUpgrade)
            {
                DBLog.Warning($"upgrade.reject.maxlevel.{gridPosition}", "Upgrade rejected: tower is at max level or null.", 0.5f, this);
                return;
            }

            TowerDataSO data = GetTowerData(cell.OccupiedBy);
            if (data == null)
            {
                DBLog.Warning("upgrade.reject.data", "Upgrade rejected: TowerDataSO not found.", 1f, this);
                return;
            }

            if (!_sharedResources.CanAfford(ResourceType.Wood, data.upgradeCostWood)
                || !_sharedResources.CanAfford(ResourceType.Ore, data.upgradeCostOre))
            {
                DBLog.Warning($"upgrade.reject.cost.{cell.OccupiedBy}", "Upgrade rejected: not enough resources.", 0.5f, this);
                return;
            }

            if (!_sharedResources.TrySpend(ResourceType.Wood, data.upgradeCostWood)) return;
            if (!_sharedResources.TrySpend(ResourceType.Ore, data.upgradeCostOre))
            {
                _sharedResources.AddResource(ResourceType.Wood, data.upgradeCostWood);
                return;
            }

            tower.UpgradeLevel();
            DBLog.Info($"upgrade.accept.{gridPosition}", $"Tower upgraded. grid={gridPosition}, level={tower.CurrentLevel}.", 0.25f, this);
        }

        // ─── Remove ─────────────────────────────────────────────────────

        /// <summary>
        /// Client gọi để gửi yêu cầu tháo dỡ tower tại gridPosition, hoàn trả 50% tài nguyên.
        /// </summary>
        public void RequestRemoveTower(Vector2Int gridPosition)
        {
            DBLog.Info($"remove.send.{gridPosition}", $"Remove request sent. grid={gridPosition}.", 0.25f, this);
            RemoveTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RemoveTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (_grid == null || _sharedResources == null || _pool == null)
            {
                DBLog.Warning("remove.reject.refs", "Remove rejected: missing refs.", 1f, this);
                return;
            }

            if (!_grid.TryGetCell(gridPosition, out GridCell cell))
            {
                DBLog.Warning($"remove.reject.grid.{gridPosition}", $"Remove rejected: no tower at {gridPosition}.", 0.5f, this);
                return;
            }

            // Hoàn trả 50% tài nguyên gốc (không tính upgrade cost)
            TowerDataSO data = GetTowerData(cell.OccupiedBy);
            if (data != null)
            {
                int refundWood = Mathf.RoundToInt(data.woodCost * 0.5f);
                int refundOre  = Mathf.RoundToInt(data.oreCost  * 0.5f);
                if (refundWood > 0) _sharedResources.AddResource(ResourceType.Wood, refundWood);
                if (refundOre  > 0) _sharedResources.AddResource(ResourceType.Ore,  refundOre);
                DBLog.Info($"remove.refund.{gridPosition}", $"Refunded: wood={refundWood}, ore={refundOre}.", 0.25f, this);
            }

            _grid.ClearTower(gridPosition);

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cell.TowerNetworkObjectId, out NetworkObject netObj))
            {
                _pool.Return(netObj);
            }

            DBLog.Info($"remove.accept.{gridPosition}", $"Tower removed. grid={gridPosition}.", 0.25f, this);
        }

        // ─── Helpers ────────────────────────────────────────────────────

        private TowerDataSO GetTowerData(TowerType towerType)
        {
            foreach (TowerDataSO data in _towerData)
            {
                if (data != null && data.towerType == towerType) return data;
            }
            return null;
        }

        private NetworkObject GetTowerPrefab(TowerType towerType)
        {
            foreach (TowerPrefabEntry entry in _towerPrefabs)
            {
                if (entry.TowerType == towerType) return entry.Prefab;
            }
            return null;
        }

        [Serializable]
        private sealed class TowerPrefabEntry
        {
            [SerializeField] private TowerType _towerType;
            [SerializeField] private NetworkObject _prefab;

            public TowerType TowerType => _towerType;
            public NetworkObject Prefab => _prefab;
        }
    }
}
