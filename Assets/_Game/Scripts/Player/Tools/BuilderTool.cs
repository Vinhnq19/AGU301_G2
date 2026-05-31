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

        // True khi panel vua duoc mo boi UseAction.
        // CancelAction duoc goi ca khi tha chuot va khi doi tool.
        // Flag nay giup bo qua lan cancel dau tien (mouse-up) — chi dong panel khi doi tool.
        private bool _panelJustOpened;

        public ToolType ToolType => ToolType.Builder;

        // True neu pointer dang tren UI element trong frame hien tai.
        // Duoc cap nhat trong Update() — IsPointerOverGameObject() chi dung
        // khi goi tu Update, KHONG dung khi goi tu InputAction callback.
        private bool _isPointerOverUI;

        private void Awake()
        {
            EnsureReferences();
        }

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

        public void UseAction(Vector3 targetPosition)
        {
            if (!IsOwner)
            {
                DBLog.Warning($"build.tool.not-owner.{NetworkObjectId}", $"Builder use ignored. owner={OwnerClientId}.", 1f, this);
                return;
            }
            // Neu chuot tren UI (panel, button...) → bo qua.
            // Doc cache tu Update() thay vi goi truc tiep — tranh warning New Input System.
            if (_isPointerOverUI)
            {
                return;
            }

            EnsureReferences();

            Vector2Int gridPos = _gridManager != null
                ? _gridManager.WorldToGrid(targetPosition)
                : Vector2Int.RoundToInt(new Vector2(targetPosition.x, targetPosition.y));

            // Khong mo panel neu o da bi chiem hoac out of bounds
            if (_gridManager != null && !_gridManager.IsValidPlacement(gridPos))
            {
                DBLog.Info($"build.tool.invalid.{NetworkObjectId}", $"Cell {gridPos} is occupied or out of bounds.", 0.5f, this);
                return;
            }

            if (_towerSelectionView == null)
            {
                DBLog.Warning($"build.tool.no-panel.{NetworkObjectId}", "TowerSelectionView not found. Panel will not open.", 1f, this);
                return;
            }

            DBLog.Info($"build.tool.open-panel.{NetworkObjectId}", $"Opening tower selection. grid={gridPos}.", 0.2f, this);
            _panelJustOpened = true;
            _towerSelectionView.RequestShowAt(gridPos);
        }

        public void CancelAction()
        {
            EnsureReferences();

            if (_isPointerOverUI)
            {
                return;
            }
            if (_panelJustOpened)
            {
                _panelJustOpened = false;
                return;
            }

            _towerSelectionView?.RequestHide();
        }

        private void EnsureReferences()
        {
            if (_buildingController == null)
            {
                _buildingController = FindFirstObjectByType<BuildingController>();
            }

            if (_gridManager == null)
            {
                _gridManager = FindFirstObjectByType<GridManager>();
            }

            if (_towerSelectionView == null)
            {
                _towerSelectionView = FindFirstObjectByType<TowerSelectionView>();
            }
        }
    }
}
