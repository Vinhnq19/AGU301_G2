using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Building
{
    public struct GridCell
    {
        public Vector2Int GridPosition { get; }
        public bool IsOccupied { get; private set; }
        public TowerType OccupiedBy { get; private set; }

        public GridCell(Vector2Int gridPosition)
        {
            GridPosition = gridPosition;
            IsOccupied = false;
            OccupiedBy = default;
        }

        public void Occupy(TowerType towerType)
        {
            IsOccupied = true;
            OccupiedBy = towerType;
        }

        public void Release()
        {
            IsOccupied = false;
            OccupiedBy = default;
        }
    }
}
