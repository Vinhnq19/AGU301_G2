using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Building
{
    public struct GridCell
    {
        public Vector2Int GridPosition { get; }
        public bool IsOccupied { get; private set; }
        public TowerType OccupiedBy { get; private set; }
        public ulong TowerNetworkObjectId { get; private set; }

        public GridCell(Vector2Int gridPosition)
        {
            GridPosition = gridPosition;
            IsOccupied = false;
            OccupiedBy = default;
            TowerNetworkObjectId = 0;
        }

        public void Occupy(TowerType towerType, ulong networkObjectId = 0)
        {
            IsOccupied = true;
            OccupiedBy = towerType;
            TowerNetworkObjectId = networkObjectId;
        }

        public void SetNetworkObjectId(ulong networkObjectId)
        {
            TowerNetworkObjectId = networkObjectId;
        }

        public void Release()
        {
            IsOccupied = false;
            OccupiedBy = default;
            TowerNetworkObjectId = 0;
        }
    }
}
