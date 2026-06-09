using Assets._Game.Scripts.Data;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Networking.Pool;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Building
{
    public sealed class BuildingController : NetworkBehaviour
    {
        [SerializeField] private TowerDataSO[] _towerData;
        [SerializeField] private TowerPrefabEntry[] _towerPrefabs;

        private IResourceService _sharedResources;
        private GridManager _grid;
        private INetworkPool _pool;

        [Inject]
        public void Construct(IResourceService sharedResources, GridManager grid, INetworkPool pool)
        {
            _sharedResources = sharedResources;
            _grid = grid;
            _pool = pool;
        }

        // ─── Place ──────────────────────────────────────────────────────

        public void RequestPlaceTower(Vector2Int gridPosition, TowerType towerType)
        {
            DBLog.Info($"build.send.{OwnerClientId}", $"[BuildingController] Place intent sent. grid={gridPosition}, type={towerType}.", 0.25f, this);
            PlaceTowerServerRpc(gridPosition, towerType);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PlaceTowerServerRpc(Vector2Int gridPosition, TowerType towerType, RpcParams rpcParams = default)
        {
            DBLog.Info($"build.recv.{rpcParams.Receive.SenderClientId}", $"[BuildingController] Place received. grid={gridPosition}, type={towerType}.", 0.25f, this);

            TowerDataSO data   = GetTowerData(towerType);
            NetworkObject prefab = GetTowerPrefab(towerType);

            if (data == null || prefab == null || _grid == null || _pool == null)
            {
                DBLog.Warning($"build.reject.refs.{towerType}", "[BuildingController] Place rejected: missing refs.", 1f, this);
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
                DBLog.Warning($"build.reject.pool.{gridPosition}", "[BuildingController] Place failed: pool null.", 1f, this);
                return;
            }

            if (!tower.IsSpawned) tower.Spawn();

            _grid.SetTowerNetworkObjectId(gridPosition, tower.NetworkObjectId);
            tower.GetComponent<BaseTower>()?.OnPlaced(gridPosition);

            DBLog.Info($"build.accept.{gridPosition}", $"[BuildingController] Tower placed (under construction). type={towerType}, networkId={tower.NetworkObjectId}.", 0.25f, tower);
        }

        // ─── Contribute ─────────────────────────────────────────────────

        public void RequestContributeTower(Vector2Int gridPosition)
        {
            DBLog.Info($"contribute.send.{OwnerClientId}", $"[BuildingController] Contribute intent sent. grid={gridPosition}.", 0.25f, this);
            ContributeTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ContributeTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (!TryGetTower(gridPosition, "contribute", out BaseTower tower, out TowerDataSO data)) return;

            if (tower.IsConstructed)
            {
                DBLog.Warning($"contribute.reject.done.{gridPosition}", "[BuildingController] Contribute rejected: already constructed.", 0.5f, this);
                return;
            }

            if (!_sharedResources.TrySpend(data.buildCost))
            {
                DBLog.Warning(
                    $"tower.contribute.reject.cost.{gridPosition}",
                    "[BuildingController] Contribution rejected: not enough resources.",
                    0.5f,
                    tower);
                return;
            }

            tower.CompleteConstruction();
            DBLog.Info($"tower.contribute.{gridPosition}", "[BuildingController] Construction paid in full.", 0.25f, tower);

            if (tower.IsConstructed)
                DBLog.Info($"tower.activated.{gridPosition}", $"[BuildingController] Tower activated at {gridPosition}.", 0.25f, tower);
        }

        // ─── Upgrade ────────────────────────────────────────────────────

        public void RequestUpgradeTower(Vector2Int gridPosition)
        {
            DBLog.Info($"upgrade.send.{gridPosition}", $"[BuildingController] Upgrade request sent. grid={gridPosition}.", 0.25f, this);
            UpgradeTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void UpgradeTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (!TryGetTower(gridPosition, "upgrade", out BaseTower tower, out TowerDataSO data)) return;

            if (!tower.IsConstructed)
            {
                DBLog.Warning($"upgrade.reject.construction.{gridPosition}", "[BuildingController] Upgrade rejected: not constructed.", 0.5f, this);
                return;
            }

            if (!tower.CanUpgrade)
            {
                DBLog.Warning($"upgrade.reject.maxlevel.{gridPosition}", "[BuildingController] Upgrade rejected: max level.", 0.5f, this);
                return;
            }

            ResourceCost[] upgradeCost = data.GetUpgradeCostForLevel(tower.CurrentLevel);

            if (!_sharedResources.TrySpend(upgradeCost))
            {
                DBLog.Warning($"upgrade.reject.cost.{gridPosition}", "[BuildingController] Upgrade rejected: not enough resources.", 0.5f, this);
                return;
            }

            tower.UpgradeLevel();
            DBLog.Info($"upgrade.accept.{gridPosition}", $"[BuildingController] Tower upgraded to level {tower.CurrentLevel}.", 0.25f, this);
        }

        // ─── Remove ─────────────────────────────────────────────────────

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
                TowerDataSO data = GetTowerData(cell.OccupiedBy);
                if (tower != null && data != null)
                {
                    var sb = new StringBuilder();
                    var refundedTypes = new HashSet<ResourceType>();
                    foreach (ResourceCost cost in data.buildCost)
                    {
                        if (!refundedTypes.Add(cost.type))
                            continue;

                        int refund = Mathf.RoundToInt(tower.GetPaid(cost.type) * 0.5f);
                        if (refund > 0)
                        {
                            _sharedResources.TryAdd(cost.type, refund);
                            sb.Append($"{refund}{ResourceCost.Abbr(cost.type)} ");
                        }
                    }
                    if (sb.Length > 0)
                        DBLog.Info($"remove.refund.{gridPosition}", $"[BuildingController] Refunded: {sb.ToString().Trim()} (50% of contributed).", 0.25f, this);
                }
            }

            _grid.ClearTower(gridPosition);
            if (netObj != null) _pool.Return(netObj);
            DBLog.Info($"remove.accept.{gridPosition}", $"[BuildingController] Tower removed at {gridPosition}.", 0.25f, this);
        }

        // ─── Helpers ────────────────────────────────────────────────────

        private bool TryGetTower(Vector2Int gridPosition, string action, out BaseTower tower, out TowerDataSO data)
        {
            tower = null;
            data  = null;

            if (_grid == null || _sharedResources == null)
            {
                DBLog.Warning($"{action}.reject.refs.{gridPosition}", $"[BuildingController] {action} rejected: missing refs.", 1f, this);
                return false;
            }

            if (!_grid.TryGetCell(gridPosition, out GridCell cell))
            {
                DBLog.Warning($"{action}.reject.grid.{gridPosition}", $"[BuildingController] {action} rejected: no tower at {gridPosition}.", 0.5f, this);
                return false;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cell.TowerNetworkObjectId, out NetworkObject netObj))
            {
                DBLog.Warning($"{action}.reject.notfound.{cell.TowerNetworkObjectId}", $"[BuildingController] {action} rejected: NetworkObject not found.", 1f, this);
                return false;
            }

            tower = netObj.GetComponent<BaseTower>();
            if (tower == null)
            {
                DBLog.Warning($"{action}.reject.component.{gridPosition}", $"[BuildingController] {action} rejected: BaseTower not found.", 1f, this);
                return false;
            }

            data = GetTowerData(cell.OccupiedBy);
            if (data == null)
            {
                DBLog.Warning($"{action}.reject.data.{gridPosition}", $"[BuildingController] {action} rejected: TowerDataSO not found.", 1f, this);
                return false;
            }

            return true;
        }

        private TowerDataSO GetTowerData(TowerType towerType)
        {
            foreach (TowerDataSO d in _towerData)
                if (d != null && d.towerType == towerType) return d;
            return null;
        }

        private NetworkObject GetTowerPrefab(TowerType towerType)
        {
            foreach (TowerPrefabEntry entry in _towerPrefabs)
                if (entry.TowerType == towerType) return entry.Prefab;
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
