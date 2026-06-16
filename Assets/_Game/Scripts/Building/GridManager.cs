using System.Collections.Generic;
using Assets._Game.Scripts.Data;
using UnityEngine;

namespace DungeonBuilder.Building
{
    /// <summary>
    /// Quản lý lưới đặt tower. Lưu trạng thái ô trên server.
    /// </summary>
    public sealed class GridManager : MonoBehaviour
    {
        [SerializeField] private Vector3 _origin;
        [SerializeField, Min(0.1f)] private float _cellSize = 1f;
        [SerializeField] private Vector2Int _minBounds = new(-50, -50);
        [SerializeField] private Vector2Int _maxBounds = new(50, 50);

        [Header("Placement Restriction")]
        [SerializeField] private bool _usePredefinedSpotsOnly = true;
        [Tooltip("Kéo thả các GameObject đóng vai trò là 'điểm đặt tháp' vào đây")]
        [SerializeField] private Transform[] _predefinedSpotTransforms;

        private readonly Dictionary<Vector2Int, GridCell> _cells = new();
        private readonly HashSet<Vector2Int> _allowedTowerSpots = new();

        private void Awake()
        {
            if (_predefinedSpotTransforms == null) return;

            foreach (Transform spot in _predefinedSpotTransforms)
            {
                if (spot == null) continue;

                // Snap chính xác vị trí Visual về giao điểm lưới gần nhất
                // -> Đảm bảo visual và điểm đặt tháp luôn trùng nhau
                Vector2Int gridPos  = WorldToGrid(spot.position);
                spot.position       = GridToWorld(gridPos);

                _allowedTowerSpots.Add(gridPos);
            }
        }

        public bool IsValidPlacement(Vector2Int position)
        {
            if (_usePredefinedSpotsOnly && !_allowedTowerSpots.Contains(position))
            {
                // Thêm log de trace li do
                DungeonBuilder.Core.Debugging.DBLog.Info($"grid.invalid.spot", $"Position {position} is not in predefined spots.", 0.5f, this);
                return false;
            }

            if (!IsInsideBounds(position)) return false;
            
            if (_cells.TryGetValue(position, out GridCell cell) && cell.IsOccupied)
            {
                DungeonBuilder.Core.Debugging.DBLog.Info($"grid.invalid.occupied", $"Position {position} is already occupied.", 0.5f, this);
                return false;
            }

            return true;
        }

        public bool PlaceTower(Vector2Int position, TowerDataSO data)
        {
            if (data == null || !IsValidPlacement(position))
            {
                return false;
            }

            GridCell cell = _cells.TryGetValue(position, out GridCell existing)
                ? existing
                : new GridCell(position);
            cell.Occupy(data.towerType);
            _cells[position] = cell;
            return true;
        }

        /// <summary>
        /// Ghi lại NetworkObjectId sau khi tower đã Spawn(). Gọi ngay sau tower.Spawn().
        /// </summary>
        public void SetTowerNetworkObjectId(Vector2Int position, ulong networkObjectId)
        {
            if (!_cells.TryGetValue(position, out GridCell cell) || !cell.IsOccupied)
            {
                return;
            }

            cell.SetNetworkObjectId(networkObjectId);
            _cells[position] = cell;
        }

        /// <summary>
        /// Trả về GridCell nếu ô đó đang có tower. Dùng cho Upgrade/Remove RPC.
        /// </summary>
        public bool TryGetCell(Vector2Int position, out GridCell cell)
        {
            return _cells.TryGetValue(position, out cell) && cell.IsOccupied;
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

        public bool IsInsideBounds(Vector2Int position)
        {
            return position.x >= _minBounds.x
                && position.y >= _minBounds.y
                && position.x <= _maxBounds.x
                && position.y <= _maxBounds.y;
        }
        private void OnDrawGizmosSelected()
        {
            if (_predefinedSpotTransforms == null) return;

            foreach (Transform spot in _predefinedSpotTransforms)
            {
                if (spot == null) continue;

                // Tính toán ví trí snap đúng (không dùng _allowedTowerSpots vì Awake chưa chạy trong Editor)
                Vector2Int gridPos     = WorldToGrid(spot.position);
                Vector3    snappedPos  = GridToWorld(gridPos);

                // Vẽ ô lưới đã snapped - màu xanh lá nếu khớp visual, đỏ nếu lệch
                bool isAligned = Vector3.Distance(spot.position, snappedPos) < 0.01f;
                Gizmos.color = isAligned ? new Color(0f, 1f, 0f, 0.4f) : new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawCube(snappedPos, new Vector3(_cellSize, _cellSize, 0.01f));

                // Vẽ đường nối visual → snapped nếu đang lệch
                if (!isAligned)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(spot.position, snappedPos);
                }
            }
        }
    }
}
