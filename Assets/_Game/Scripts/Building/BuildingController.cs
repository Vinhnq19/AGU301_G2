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
        private IBuildCommandValidator _validator;

        [Inject]
        public void Construct(
            IResourceService sharedResources,
            GridManager grid,
            INetworkPool pool,
            IBuildCommandValidator validator)
        {
            _sharedResources = sharedResources;
            _grid = grid;
            _pool = pool;
            _validator = validator;
        }

        public void RequestPlaceTower(Vector2Int gridPosition, TowerType towerType)
        {
            DBLog.Info($"build.send.{OwnerClientId}", $"[BuildingController] Place intent sent. grid={gridPosition}, type={towerType}.", 0.25f, this);
            PlaceTowerServerRpc(gridPosition, towerType);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PlaceTowerServerRpc(Vector2Int gridPosition, TowerType towerType, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            DBLog.Info($"build.recv.{senderClientId}", $"[BuildingController] Place received. grid={gridPosition}, type={towerType}.", 0.25f, this);

            TowerDataSO data = GetTowerData(towerType);
            NetworkObject prefab = GetTowerPrefab(towerType);
            if (data == null || prefab == null || _grid == null || _pool == null || _sharedResources == null || _validator == null)
            {
                DBLog.Warning($"build.reject.refs.{towerType}", "[BuildingController] Place rejected: missing refs or invalid tower type.", 1f, this);
                return;
            }

            BuildValidationResult validation = _validator.ValidatePlacement(senderClientId, gridPosition);
            if (!validation.IsAllowed)
            {
                LogValidationRejected("build", gridPosition, validation);
                return;
            }

            NetworkObject towerObject = _pool.Get(prefab, _grid.GridToWorld(gridPosition), Quaternion.identity);
            if (towerObject == null)
            {
                DBLog.Warning($"build.reject.pool.{gridPosition}", "[BuildingController] Place failed: pool returned null.", 1f, this);
                return;
            }

            BaseTower tower = towerObject.GetComponent<BaseTower>();
            if (tower == null)
            {
                _pool.Return(towerObject);
                DBLog.Warning($"build.reject.component.{gridPosition}", "[BuildingController] Place failed: BaseTower not found.", 1f, this);
                return;
            }

            if (!_sharedResources.TrySpend(data.buildCost))
            {
                _pool.Return(towerObject);
                DBLog.Warning($"build.reject.cost.{gridPosition}", "[BuildingController] Place rejected: not enough resources.", 0.5f, this);
                return;
            }

            if (!_grid.PlaceTower(gridPosition, data))
            {
                RefundCosts(data.buildCost);
                _pool.Return(towerObject);
                DBLog.Warning($"build.reject.occupied.{gridPosition}", $"[BuildingController] Place rejected: cell occupied {gridPosition}.", 0.5f, this);
                return;
            }

            try
            {
                if (!towerObject.IsSpawned)
                {
                    towerObject.Spawn();
                }

                _grid.SetTowerNetworkObjectId(gridPosition, towerObject.NetworkObjectId);
                tower.OnPlaced(gridPosition);
                tower.CompleteConstruction();
            }
            catch (Exception exception)
            {
                _grid.ClearTower(gridPosition);
                RefundCosts(data.buildCost);
                _pool.Return(towerObject);
                Debug.LogError($"[BuildingController] Place failed and was rolled back at {gridPosition}: {exception}", this);
                return;
            }

            DBLog.Info($"build.accept.{gridPosition}", $"[BuildingController] Tower placed and paid in full. type={towerType}, networkId={towerObject.NetworkObjectId}.", 0.25f, towerObject);
        }

        public void RequestContributeTower(Vector2Int gridPosition)
        {
            DBLog.Info($"contribute.send.{OwnerClientId}", $"[BuildingController] Contribute intent sent. grid={gridPosition}.", 0.25f, this);
            ContributeTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ContributeTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (!TryGetTower(rpcParams.Receive.SenderClientId, gridPosition, "contribute", out BaseTower tower, out _))
            {
                return;
            }

            DBLog.Warning(
                $"contribute.reject.done.{gridPosition}",
                "[BuildingController] Contribution rejected: towers are paid in full when placed.",
                0.5f,
                tower);
        }

        public void RequestUpgradeTower(Vector2Int gridPosition)
        {
            DBLog.Info($"upgrade.send.{gridPosition}", $"[BuildingController] Upgrade request sent. grid={gridPosition}.", 0.25f, this);
            UpgradeTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void UpgradeTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (!TryGetTower(rpcParams.Receive.SenderClientId, gridPosition, "upgrade", out BaseTower tower, out TowerDataSO data))
            {
                return;
            }

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
            if (upgradeCost.Length == 0)
            {
                DBLog.Warning($"upgrade.reject.config.{gridPosition}", "[BuildingController] Upgrade rejected: upgrade cost is not configured.", 0.5f, this);
                return;
            }

            if (!_sharedResources.TrySpend(upgradeCost))
            {
                DBLog.Warning($"upgrade.reject.cost.{gridPosition}", "[BuildingController] Upgrade rejected: not enough resources.", 0.5f, this);
                return;
            }

            tower.UpgradeLevel();
            DBLog.Info($"upgrade.accept.{gridPosition}", $"[BuildingController] Tower upgraded to level {tower.CurrentLevel}.", 0.25f, this);
        }

        public void RequestRemoveTower(Vector2Int gridPosition)
        {
            DBLog.Info($"remove.send.{gridPosition}", $"[BuildingController] Remove request sent. grid={gridPosition}.", 0.25f, this);
            RemoveTowerServerRpc(gridPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RemoveTowerServerRpc(Vector2Int gridPosition, RpcParams rpcParams = default)
        {
            if (_grid == null || _sharedResources == null || _pool == null || _validator == null)
            {
                DBLog.Warning("remove.reject.refs", "[BuildingController] Remove rejected: missing refs.", 1f, this);
                return;
            }

            if (!TryGetTower(rpcParams.Receive.SenderClientId, gridPosition, "remove", out BaseTower tower, out TowerDataSO data))
            {
                return;
            }

            var refundedTypes = new HashSet<ResourceType>();
            var refundSummary = new StringBuilder();
            foreach (ResourceCost cost in data.buildCost)
            {
                if (!refundedTypes.Add(cost.type))
                {
                    continue;
                }

                int refund = Mathf.RoundToInt(tower.GetPaid(cost.type) * 0.5f);
                if (refund > 0 && _sharedResources.TryAdd(cost.type, refund))
                {
                    refundSummary.Append($"{refund}{ResourceCost.Abbr(cost.type)} ");
                }
            }

            if (refundSummary.Length > 0)
            {
                DBLog.Info($"remove.refund.{gridPosition}", $"[BuildingController] Refunded: {refundSummary.ToString().Trim()} (50% of build cost).", 0.25f, this);
            }

            _grid.ClearTower(gridPosition);
            _pool.Return(tower.NetworkObject);
            DBLog.Info($"remove.accept.{gridPosition}", $"[BuildingController] Tower removed at {gridPosition}.", 0.25f, this);
        }

        private bool TryGetTower(
            ulong senderClientId,
            Vector2Int gridPosition,
            string action,
            out BaseTower tower,
            out TowerDataSO data)
        {
            tower = null;
            data = null;

            if (_grid == null || _sharedResources == null || _validator == null)
            {
                DBLog.Warning($"{action}.reject.refs.{gridPosition}", $"[BuildingController] {action} rejected: missing refs.", 1f, this);
                return false;
            }

            BuildValidationResult validation = _validator.ValidateTowerAction(senderClientId, gridPosition, out NetworkObject towerObject);
            if (!validation.IsAllowed)
            {
                LogValidationRejected(action, gridPosition, validation);
                return false;
            }

            if (!_grid.TryGetCell(gridPosition, out GridCell cell))
            {
                DBLog.Warning($"{action}.reject.grid.{gridPosition}", $"[BuildingController] {action} rejected: no tower at {gridPosition}.", 0.5f, this);
                return false;
            }

            tower = towerObject.GetComponent<BaseTower>();
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

        private void RefundCosts(IReadOnlyList<ResourceCost> costs)
        {
            if (costs == null || _sharedResources == null)
            {
                return;
            }

            foreach (ResourceCost cost in costs)
            {
                if (cost.amount > 0)
                {
                    _sharedResources.TryAdd(cost.type, cost.amount);
                }
            }
        }

        private void LogValidationRejected(string action, Vector2Int gridPosition, BuildValidationResult validation)
        {
            DBLog.Warning(
                $"{action}.reject.authority.{validation.Code}.{gridPosition}",
                $"[BuildingController] {action} rejected: authority={validation.Code}, grid={gridPosition}.",
                0.5f,
                this);
        }

        private TowerDataSO GetTowerData(TowerType towerType)
        {
            foreach (TowerDataSO data in _towerData)
            {
                if (data != null && data.towerType == towerType)
                {
                    return data;
                }
            }

            return null;
        }

        private NetworkObject GetTowerPrefab(TowerType towerType)
        {
            foreach (TowerPrefabEntry entry in _towerPrefabs)
            {
                if (entry.TowerType == towerType)
                {
                    return entry.Prefab;
                }
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
