using DungeonBuilder.Building;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Player.Tools;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace Assets._Game.Scripts.Building
{
    /// <summary>
    /// Presenter cua tower (MonoBehaviour tren root prefab). Bridges TowerModel voi TowerView.
    /// Xu ly request Upgrade / Remove / Contribute gui len server qua BuildingController.
    /// IPointerClickHandler tren root (cung BoxCollider2D): nhan Physics2D Raycaster click.
    /// Chi mo ActionPanel khi Builder tool active va tower da IsConstructed.
    /// </summary>
    public sealed class TowerPresenter : MonoBehaviour, IPointerClickHandler
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
        /// Goi boi BaseTower.OnNetworkSpawn() sau khi tao TowerModel.
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

        /// <summary>
        /// Goi tu TowerView.UpgradeButton.onClick.
        /// </summary>
        public void RequestUpgrade()
        {
            Vector2Int gridPos = GetGridPosition();
            DBLog.Info($"tower.upgrade.request.{gridPos}", $"[TowerPresenter] Upgrade request. grid={gridPos}.", 0.25f, this);
            _view?.HidePanel();
            _buildingController?.RequestUpgradeTower(gridPos);
        }

        /// <summary>
        /// Goi tu TowerView.RemoveButton.onClick.
        /// </summary>
        public void RequestRemove()
        {
            Vector2Int gridPos = GetGridPosition();
            DBLog.Info($"tower.remove.request.{gridPos}", $"[TowerPresenter] Remove request. grid={gridPos}.", 0.25f, this);
            _view?.HidePanel();
            _buildingController?.RequestRemoveTower(gridPos);
        }

        /// <summary>
        /// Goi tu TowerView.ContributeButton.onClick.
        /// </summary>
        public void RequestContribute()
        {
            Vector2Int gridPos = GetGridPosition();
            DBLog.Info($"tower.contribute.request.{gridPos}", $"[TowerPresenter] Contribute request. grid={gridPos}.", 0.25f, this);
            _buildingController?.RequestContributeTower(gridPos);
        }

        /// <summary>
        /// Physics2D Raycaster hit BoxCollider2D tren root.
        /// Chi mo ActionPanel khi Builder tool active va tower da IsConstructed.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (ToolController.LocalActiveTool != ToolType.Builder) return;
            if (_model != null && !_model.IsConstructed) return;
            _view?.TogglePanel();
        }

        private void OnModelChanged()
        {
            _view?.Render(_model);
            _view?.RenderConstruction(_model);
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
