using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
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
                DBLog.Warning($"build.reject.refs.{towerType}", $"Place tower rejected: missing refs. dataNull={data == null}, prefabNull={prefab == null}, resourcesNull={_sharedResources == null}, gridNull={_grid == null}, poolNull={_pool == null}.", 1f, this);
                return;
            }

            if (!_grid.IsValidPlacement(gridPosition))
            {
                DBLog.Warning($"build.reject.grid.{gridPosition}", $"Place tower rejected: invalid grid position {gridPosition}.", 0.5f, this);
                return;
            }

            if (!_sharedResources.CanAfford(ResourceType.Wood, data.woodCost)
                || !_sharedResources.CanAfford(ResourceType.Ore, data.oreCost))
            {
                DBLog.Warning($"build.reject.cost.{towerType}", $"Place tower rejected: not enough resources. type={towerType}, woodCost={data.woodCost}, oreCost={data.oreCost}.", 0.5f, this);
                return;
            }

            if (!_sharedResources.TrySpend(ResourceType.Wood, data.woodCost))
            {
                return;
            }

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
                DBLog.Warning($"build.reject.pool.{gridPosition}", $"Tower placement failed: pool returned null. Grid and resources rolled back. type={towerType}, grid={gridPosition}.", 1f, this);
                return;
            }

            tower.Spawn();
            DBLog.Info($"build.accept.{gridPosition}", $"Tower placed. type={towerType}, grid={gridPosition}, networkId={tower.NetworkObjectId}.", 0.25f, tower);
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
