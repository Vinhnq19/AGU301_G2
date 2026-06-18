using DungeonBuilder.Building;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.UI.TowerSelection;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Player.Tools
{
    public sealed class BuilderTool : NetworkBehaviour, ITool
    {
        [SerializeField] private BuildingController _buildingController;
        [SerializeField] private GridManager _gridManager;
        [SerializeField] private TowerSelectionView _towerSelectionView;

        // Luu vi tri grid du dinh mo panel (validate khi mouse-down, mo khi mouse-up)
        private bool _hasPendingOpen;
        private Vector2Int _pendingGridPos;

        public ToolType ToolType => ToolType.Builder;

        private bool _isPointerOverUI;

        private void Update()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                _isPointerOverUI = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            }
        }

        public override void OnNetworkSpawn()
        {
            EnsureReferences();
        }

        /// <summary>
        /// Goi khi mouse DOWN. Chi validate va luu grid position —
        /// chua mo panel de tranh click-through vao TowerOptionButton.
        /// </summary>
        public void UseAction(Vector3 targetPosition)
        {
            _hasPendingOpen = false;

            if (!IsOwner)
            {
                DBLog.Warning($"build.tool.not-owner.{NetworkObjectId}", $"Builder use ignored. owner={OwnerClientId}.", 1f, this);
                return;
            }

            if (_isPointerOverUI) return;

            EnsureReferences();

            Vector2Int gridPos = _gridManager != null
                ? _gridManager.WorldToGrid(targetPosition)
                : Vector2Int.RoundToInt(new Vector2(targetPosition.x, targetPosition.y));

            if (_gridManager != null && !_gridManager.IsValidPlacement(gridPos))
            {
                DBLog.Info($"build.tool.invalid.{NetworkObjectId}", $"Cell {gridPos} is occupied or out of bounds.", 0.5f, this);
                return;
            }

            if (_towerSelectionView == null)
            {
                DBLog.Warning($"build.tool.no-panel.{NetworkObjectId}", "TowerSelectionView not found.", 1f, this);
                return;
            }

            // Luu lai de mo panel khi mouse-up (CancelAction)
            _pendingGridPos = gridPos;
            _hasPendingOpen = true;
            DBLog.Info($"build.tool.pending.{NetworkObjectId}", $"Panel open pending. grid={gridPos}.", 0.2f, this);
        }

        /// <summary>
        /// Goi khi mouse UP. Neu co pending open → mo panel. Neu khong → dong panel.
        /// </summary>
        public void CancelAction()
        {
            EnsureReferences();

            if (_hasPendingOpen)
            {
                _hasPendingOpen = false;

                if (!_isPointerOverUI)
                {
                    DBLog.Info($"build.tool.open-panel.{NetworkObjectId}", $"Opening tower selection. grid={_pendingGridPos}.", 0.2f, this);
                    _towerSelectionView?.RequestShowAt(_pendingGridPos);
                }
                return;
            }

            // Khong co pending → dong panel (click ngoai panel)
            if (!_isPointerOverUI)
            {
                _towerSelectionView?.RequestHide();
            }
        }

        private void EnsureReferences()
        {
            if (_buildingController == null)
                _buildingController = FindFirstObjectByType<BuildingController>();
            if (_gridManager == null)
                _gridManager = FindFirstObjectByType<GridManager>();
            if (_towerSelectionView == null)
                _towerSelectionView = FindFirstObjectByType<TowerSelectionView>();
        }
    }
}
