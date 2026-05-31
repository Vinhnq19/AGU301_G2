using System.Linq;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Building;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Networking;
using DungeonBuilder.UI.Base;

namespace DungeonBuilder.UI.TowerSelection
{
    /// <summary>
    /// Presenter singleton (VContainer IInitializable).
    /// Quan ly viec mo/dong Tower Selection Panel va xu ly tower selection.
    /// </summary>
    public sealed class TowerSelectionPresenter
        : BasePresenter<TowerSelectionView, TowerSelectionModel>
    {
        private readonly EventBus _eventBus;
        private readonly BuildingController _buildingController;
        private readonly SharedResourceManager _resources;
        private readonly TowerCatalogSO _catalog;

        public TowerSelectionPresenter(
            TowerSelectionView view,
            TowerSelectionModel model,
            EventBus eventBus,
            BuildingController buildingController,
            SharedResourceManager resources,
            TowerCatalogSO catalog)
            : base(view, model)
        {
            _eventBus = eventBus;
            _buildingController = buildingController;
            _resources = resources;
            _catalog = catalog;

            _eventBus.OnResourceUpdated += HandleResourceUpdated;
        }

        public void Initialize()
        {
            View.SetPresenter(this);
            base.Initialize();
        }

        // ─── Public API (gọi từ BuilderTool) ─────────────────────────

        /// <summary>
        /// Mo panel chon tower tai gridPos. Goi tu BuilderTool.UseAction().
        /// </summary>
        public void ShowAt(UnityEngine.Vector2Int gridPos)
        {
            RefreshAffordability();
            Model.Open(gridPos, _catalog != null ? _catalog.Towers.ToArray() : System.Array.Empty<TowerDataSO>());
        }

        /// <summary>
        /// Dong panel. Goi tu BuilderTool.CancelAction() hoac backdrop click.
        /// </summary>
        public void Hide()
        {
            Model.Close();
        }

        // ─── Gọi từ TowerSelectionView ────────────────────────────────

        /// <summary>
        /// Player chon mot tower trong panel.
        /// </summary>
        public void OnTowerSelected(TowerType towerType)
        {
            if (!Model.IsOpen) return;
            _buildingController?.RequestPlaceTower(Model.SelectedGridPosition, towerType);
            Hide();
        }

        // ─── BasePresenter ────────────────────────────────────────────

        protected override void OnModelChanged()
        {
            View.Render(Model);

            if (Model.IsOpen) View.Show();
            else              View.Hide();
        }

        public override void Dispose()
        {
            _eventBus.OnResourceUpdated -= HandleResourceUpdated;
            base.Dispose();
        }

        // ─── Private ─────────────────────────────────────────────────

        private void RefreshAffordability()
        {
            if (_catalog == null || _resources == null) return;

            foreach (TowerDataSO data in _catalog.Towers)
            {
                bool canAfford = _resources.CanAfford(ResourceType.Wood, data.woodCost)
                              && _resources.CanAfford(ResourceType.Ore,  data.oreCost);
                Model.UpdateAffordability(data, canAfford);
            }
        }

        private void HandleResourceUpdated(ResourceType type, int value)
        {
            if (!Model.IsOpen) return;
            RefreshAffordability();
            Model.NotifyAffordabilityChanged();
        }
    }
}
