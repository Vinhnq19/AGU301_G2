using DungeonBuilder.Building;
using DungeonBuilder.Core.Enums;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Player.Tools
{
    public sealed class BuilderTool : NetworkBehaviour, ITool
    {
        [SerializeField] private TowerType _selectedTowerType = TowerType.Arrow;
        [SerializeField] private BuildingController _buildingController;
        [SerializeField] private GridManager _gridManager;

        public ToolType ToolType => DungeonBuilder.Core.Enums.ToolType.Builder;

        [Inject]
        public void Construct(BuildingController buildingController, GridManager gridManager)
        {
            _buildingController = buildingController;
            _gridManager = gridManager;
        }

        public void UseAction(Vector3 targetPosition)
        {
            if (!IsOwner || _buildingController == null)
            {
                return;
            }

            Vector2Int gridPosition = _gridManager != null
                ? _gridManager.WorldToGrid(targetPosition)
                : Vector2Int.RoundToInt(new Vector2(targetPosition.x, targetPosition.y));

            _buildingController.RequestPlaceTower(gridPosition, _selectedTowerType);
        }

        public void CancelAction()
        {
        }

        public void SetTowerType(TowerType towerType)
        {
            _selectedTowerType = towerType;
        }
    }
}
