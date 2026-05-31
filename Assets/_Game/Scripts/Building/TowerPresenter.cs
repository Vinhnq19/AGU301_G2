using DungeonBuilder.Building;
using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace Assets._Game.Scripts.Building
{
    /// <summary>
    /// Presenter của tower (MonoBehaviour trên root prefab). Bridges TowerModel ↔ TowerView.
    /// Xử lý request Upgrade / Remove gửi lên server qua BuildingController.
    /// </summary>
    public sealed class TowerPresenter : MonoBehaviour
    {
        private TowerModel _model;
        private TowerView _view;
        private GridManager _gridManager;
        private BuildingController _buildingController;

        [Inject]
        public void Construct(BuildingController buildingController, GridManager gridManager)
        {
            _buildingController = buildingController;
            _gridManager = gridManager;
        }

        /// <summary>
        /// Gọi bởi BaseTower.OnNetworkSpawn() sau khi tạo TowerModel.
        /// </summary>
        public void Initialize(TowerModel model, TowerView view)
        {
            _model = model;
            _view = view;

            if (_view != null)
            {
                _view.SetPresenter(this);
            }

            if (_model != null)
            {
                _model.OnChanged += OnModelChanged;
            }

            OnModelChanged();
        }

        public void RequestUpgrade()
        {
            Vector2Int gridPos = GetGridPosition();
            DBLog.Info($"tower.upgrade.request.{gridPos}", $"Upgrade request from presenter. grid={gridPos}.", 0.25f, this);
            _buildingController?.RequestUpgradeTower(gridPos);
        }

        public void RequestRemove()
        {
            Vector2Int gridPos = GetGridPosition();
            DBLog.Info($"tower.remove.request.{gridPos}", $"Remove request from presenter. grid={gridPos}.", 0.25f, this);
            _view?.HidePanel();
            _buildingController?.RequestRemoveTower(gridPos);
        }

        private void OnModelChanged()
        {
            _view?.Render(_model);
        }

        private Vector2Int GetGridPosition()
        {
            return _gridManager != null
                ? _gridManager.WorldToGrid(transform.position)
                : Vector2Int.RoundToInt(new Vector2(transform.position.x, transform.position.y));
        }

        private void OnDestroy()
        {
            if (_model != null)
            {
                _model.OnChanged -= OnModelChanged;
            }
        }
    }
}
