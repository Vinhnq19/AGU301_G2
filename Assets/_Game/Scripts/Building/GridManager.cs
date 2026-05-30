using System.Collections.Generic;
using DungeonBuilder.Data;
using UnityEngine;

namespace DungeonBuilder.Building
{
    public sealed class GridManager : MonoBehaviour
    {
        [SerializeField] private Vector3 _origin;
        [SerializeField, Min(0.1f)] private float _cellSize = 1f;
        [SerializeField] private Vector2Int _minBounds = new(-50, -50);
        [SerializeField] private Vector2Int _maxBounds = new(50, 50);

        private readonly Dictionary<Vector2Int, GridCell> _cells = new();

        public bool IsValidPlacement(Vector2Int position)
        {
            return IsInsideBounds(position) && (!_cells.TryGetValue(position, out GridCell cell) || !cell.IsOccupied);
        }

        public bool PlaceTower(Vector2Int position, TowerDataSO data)
        {
            if (data == null || !IsValidPlacement(position))
            {
                return false;
            }

            GridCell cell = _cells.TryGetValue(position, out GridCell existing) ? existing : new GridCell(position);
            cell.Occupy(data.towerType);
            _cells[position] = cell;
            return true;
        }

        public void ClearTower(Vector2Int position)
        {
            if (!_cells.TryGetValue(position, out GridCell cell) || !cell.IsOccupied)
            {
                return;
            }

            cell.Release();
            _cells[position] = cell;
        }

        public Vector3 GridToWorld(Vector2Int position)
        {
            return _origin + new Vector3(position.x * _cellSize, position.y * _cellSize, 0f);
        }

        public Vector2Int WorldToGrid(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - _origin;
            return new Vector2Int(
                Mathf.RoundToInt(local.x / _cellSize),
                Mathf.RoundToInt(local.y / _cellSize));
        }

        private bool IsInsideBounds(Vector2Int position)
        {
            return position.x >= _minBounds.x
                && position.y >= _minBounds.y
                && position.x <= _maxBounds.x
                && position.y <= _maxBounds.y;
        }
    }
}
