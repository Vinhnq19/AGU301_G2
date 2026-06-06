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

        /// <summary>
        /// Client gui yeu cau dat tower. Placement mien phi — tai nguyen can duoc contribute sau khi dat.
        /// </summary>
        public void RequestPlaceTower(Vector2Int gridPosition, TowerType towerType)
        {
            DBLog.Info($"build.send.{OwnerClientId}", $"[BuildingController] Place tower intent sent. grid={gridPosition}, type={towerType}.", 0.25f, this);
            PlaceTowerServerRpc(gridPosition, towerType);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PlaceTowerServerRpc(Vector2Int gridPosition, TowerType towerType, RpcParams rpcParams = default)
        {
            DBLog.Info($"build.recv.{rpcParams.Receive.SenderClientId}", $"[BuildingController] Place intent received. sender={rpcParams.Receive.SenderClientId}, grid={gridPosition}, type={towerType}.", 0.25f, this);

            TowerDataSO data = GetTowerData(towerType);
            NetworkObject prefab = GetTowerPrefab(towerType);

            if (data == null || prefab == null || _grid == null || _pool == null)
            {
                DBLog.Warning($"build.reject.refs.{towerType}", $"[BuildingController] Place rejected: missing refs.", 1f, this);
                return;
            }

            if (!_grid.IsValidPlacement(gridPosition))
            {
                DBLog.Warning($"build.reject.grid.{gridPosition}", $"[BuildingController] Place rejected: invalid grid {gridPosition}.", 0.5f, this);
                return;
            }

            if (!_grid.PlaceTower(gridPosition, data))
            {
                DBLog.Warning($"build.reject.occupied.{gridPosition}", $"[BuildingController] Place rejected: cell occupied {gridPosition}.", 0.5f, this);
                return;
            }

            NetworkObject tower = _pool.Get(prefab, _grid.GridToWorld(gridPosition), Quaternion.identity);
            if (tower == null)
            {
                _grid.ClearTower(gridPosition);
                DBLog.Warning($"build.reject.pool.{gridPosition}", $"[BuildingController] Place failed: pool returned null.", 1f, this);
                return;
            }

            if (!tower.IsSpawned)
            {
                tower.Spawn();
            }

            _grid.SetTowerNetworkObjectId(gridPosition, tower.NetworkObjectId);

            BaseTower baseTower = tower.GetComponent<BaseTower>();
            baseTower?.OnPlaced(gridPosition);

            DBLog.Info($"build.accept.{gridPosition}", $"[BuildingController] Tower placed (under construction). type={towerType}, grid={gridPosition}, networkId={tower.NetworkObjectId}.", 0.25f, tower);
        }

        // ─── Contribute ─────────────────────────────────────────────────

        /// <summary>
        /// Client gui yeu cau contribute tai nguyen vao tower dang xay tai gridPosition.
        /// Server se tru min(available, remaining) tu SharedResourceManager.
        /// </summary>
        public void RequestContributeTower(Vector2Int gridPosition)
        {
            DBLog.Info($"contribute.send.{OwnerClientId}", $"[BuildingController] Contribute intent sent. grid={gridPosition}.", 0.25f, this);
            ContributeTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ContributeTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (_grid == null || _sharedResources == null)
            {
                DBLog.Warning($"contribute.reject.refs.{gridPosition}", "[BuildingController] Contribute rejected: missing refs.", 1f, this);
                return;
            }

            if (!_grid.TryGetCell(gridPosition, out GridCell cell))
            {
                DBLog.Warning($"contribute.reject.grid.{gridPosition}", $"[BuildingController] Contribute rejected: no tower at {gridPosition}.", 0.5f, this);
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cell.TowerNetworkObjectId, out NetworkObject netObj))
            {
                DBLog.Warning($"contribute.reject.notfound.{cell.TowerNetworkObjectId}", "[BuildingController] Contribute rejected: tower not found.", 1f, this);
                return;
            }

            BaseTower tower = netObj.GetComponent<BaseTower>();
            if (tower == null)
            {
                DBLog.Warning($"contribute.reject.component.{gridPosition}", "[BuildingController] Contribute rejected: BaseTower not found.", 1f, this);
                return;
            }

            if (tower.IsConstructed)
            {
                DBLog.Warning($"contribute.reject.done.{gridPosition}", "[BuildingController] Contribute rejected: tower already constructed.", 0.5f, this);
                return;
            }

            TowerDataSO data = GetTowerData(cell.OccupiedBy);
            if (data == null)
            {
                DBLog.Warning($"contribute.reject.data.{gridPosition}", "[BuildingController] Contribute rejected: TowerDataSO not found.", 1f, this);
                return;
            }

            int woodRemaining = Mathf.Max(0, data.woodCost - tower.WoodPaid);
            int oreRemaining  = Mathf.Max(0, data.oreCost  - tower.OrePaid);

            int woodToSpend = Mathf.Min(_sharedResources.GetAmount(ResourceType.Wood), woodRemaining);
            int oreToSpend  = Mathf.Min(_sharedResources.GetAmount(ResourceType.Ore),  oreRemaining);

            if (woodToSpend > 0) _sharedResources.TrySpend(ResourceType.Wood, woodToSpend);
            if (oreToSpend  > 0) _sharedResources.TrySpend(ResourceType.Ore,  oreToSpend);

            int newWoodPaid = tower.WoodPaid + woodToSpend;
            int newOrePaid  = tower.OrePaid  + oreToSpend;
            tower.UpdateConstruction(newWoodPaid, newOrePaid);

            DBLog.Info($"tower.contribute.{gridPosition}", $"[BuildingController] Contributed +{woodToSpend}W +{oreToSpend}O. Progress: {newWoodPaid}/{data.woodCost}W {newOrePaid}/{data.oreCost}O.", 0.25f, tower);

            if (tower.IsConstructed)
            {
                DBLog.Info($"tower.activated.{gridPosition}", $"[BuildingController] Tower fully constructed and activated at {gridPosition}.", 0.25f, tower);
            }
        }

        // ─── Upgrade ────────────────────────────────────────────────────

        /// <summary>
        /// Client gui yeu cau nang cap tower tai gridPosition len server.
        /// </summary>
        public void RequestUpgradeTower(Vector2Int gridPosition)
        {
            DBLog.Info($"upgrade.send.{gridPosition}", $"[BuildingController] Upgrade request sent. grid={gridPosition}.", 0.25f, this);
            UpgradeTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void UpgradeTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (_grid == null || _sharedResources == null)
            {
                DBLog.Warning("upgrade.reject.refs", "[BuildingController] Upgrade rejected: missing refs.", 1f, this);
                return;
            }

            if (!_grid.TryGetCell(gridPosition, out GridCell cell))
            {
                DBLog.Warning($"upgrade.reject.grid.{gridPosition}", $"[BuildingController] Upgrade rejected: no tower at {gridPosition}.", 0.5f, this);
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cell.TowerNetworkObjectId, out NetworkObject netObj))
            {
                DBLog.Warning($"upgrade.reject.notfound.{cell.TowerNetworkObjectId}", "[BuildingController] Upgrade rejected: tower not found.", 1f, this);
                return;
            }

            BaseTower tower = netObj.GetComponent<BaseTower>();
            if (tower == null || !tower.CanUpgrade)
            {
                DBLog.Warning($"upgrade.reject.maxlevel.{gridPosition}", "[BuildingController] Upgrade rejected: max level or null.", 0.5f, this);
                return;
            }

            if (!tower.IsConstructed)
            {
                DBLog.Warning($"upgrade.reject.construction.{gridPosition}", "[BuildingController] Upgrade rejected: tower not yet constructed.", 0.5f, this);
                return;
            }

            TowerDataSO data = GetTowerData(cell.OccupiedBy);
            if (data == null)
            {
                DBLog.Warning("upgrade.reject.data", "[BuildingController] Upgrade rejected: TowerDataSO not found.", 1f, this);
                return;
            }

            if (!_sharedResources.CanAfford(ResourceType.Wood, data.upgradeCostWood)
                || !_sharedResources.CanAfford(ResourceType.Ore, data.upgradeCostOre))
            {
                DBLog.Warning($"upgrade.reject.cost.{cell.OccupiedBy}", "[BuildingController] Upgrade rejected: not enough resources.", 0.5f, this);
                return;
            }

            if (!_sharedResources.TrySpend(ResourceType.Wood, data.upgradeCostWood)) return;
            if (!_sharedResources.TrySpend(ResourceType.Ore, data.upgradeCostOre))
            {
                _sharedResources.AddResource(ResourceType.Wood, data.upgradeCostWood);
                return;
            }

            tower.UpgradeLevel();
            DBLog.Info($"upgrade.accept.{gridPosition}", $"[BuildingController] Tower upgraded. grid={gridPosition}, level={tower.CurrentLevel}.", 0.25f, this);
        }

        // ─── Remove ─────────────────────────────────────────────────────

        /// <summary>
        /// Client gui yeu cau thao do tower. Hoan 50% tai nguyen da contribute thuc te.
        /// </summary>
        public void RequestRemoveTower(Vector2Int gridPosition)
        {
            DBLog.Info($"remove.send.{gridPosition}", $"[BuildingController] Remove request sent. grid={gridPosition}.", 0.25f, this);
            RemoveTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RemoveTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (_grid == null || _sharedResources == null || _pool == null)
            {
                DBLog.Warning("remove.reject.refs", "[BuildingController] Remove rejected: missing refs.", 1f, this);
                return;
            }

            if (!_grid.TryGetCell(gridPosition, out GridCell cell))
            {
                DBLog.Warning($"remove.reject.grid.{gridPosition}", $"[BuildingController] Remove rejected: no tower at {gridPosition}.", 0.5f, this);
                return;
            }

            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cell.TowerNetworkObjectId, out NetworkObject netObj);

            if (netObj != null)
            {
                BaseTower tower = netObj.GetComponent<BaseTower>();
                if (tower != null)
                {
                    int refundWood = Mathf.RoundToInt(tower.WoodPaid * 0.5f);
                    int refundOre  = Mathf.RoundToInt(tower.OrePaid  * 0.5f);
                    if (refundWood > 0) _sharedResources.AddResource(ResourceType.Wood, refundWood);
                    if (refundOre  > 0) _sharedResources.AddResource(ResourceType.Ore,  refundOre);
                    DBLog.Info($"remove.refund.{gridPosition}", $"[BuildingController] Refunded: wood={refundWood}, ore={refundOre} (50% of contributed).", 0.25f, this);
                }
            }

            _grid.ClearTower(gridPosition);

            if (netObj != null)
            {
                _pool.Return(netObj);
            }

            DBLog.Info($"remove.accept.{gridPosition}", $"[BuildingController] Tower removed. grid={gridPosition}.", 0.25f, this);
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
