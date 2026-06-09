using System.Linq;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Building;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Data;
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
        private readonly BuildingController _buildingController;
        private readonly IResourceService _resources;
        private readonly TowerCatalogSO _catalog;
        private bool _disposed;

        public TowerSelectionPresenter(
            TowerSelectionView view,
            TowerSelectionModel model,
            BuildingController buildingController,
            IResourceService resources,
            TowerCatalogSO catalog)
            : base(view, model)
        {
            _buildingController = buildingController;
            _resources = resources;
            _catalog = catalog;

            _resources.ResourceChanged += HandleResourceChanged;
        }

        public override void Initialize()
        {
            View.SetPresenter(this);
            base.Initialize();
        }


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


        /// <summary>
        /// Player chon mot tower trong panel.
        /// </summary>
        public void OnTowerSelected(TowerType towerType)
        {
            if (!Model.IsOpen) return;
            _buildingController?.RequestPlaceTower(Model.SelectedGridPosition, towerType);
            Hide();
        }

        protected override void OnModelChanged()
        {
            View.Render(Model);

            if (Model.IsOpen) View.Show();
            else              View.Hide();
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _resources.ResourceChanged -= HandleResourceChanged;
            _disposed = true;
            base.Dispose();
        }


        private void RefreshAffordability()
        {
            if (_catalog == null) return;

            foreach (TowerDataSO data in _catalog.Towers)
            {
                Model.UpdateAffordability(data, _resources.CanAfford(data.buildCost));
            }
        }

        private void HandleResourceChanged(ResourceChanged change)
        {
            if (!Model.IsOpen) return;
            RefreshAffordability();
            Model.NotifyAffordabilityChanged();
        }
    }
}
