using DungeonBuilder.Building;
using DungeonBuilder.Core.Debugging;
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
    /// Mo ActionPanel khi click tower da IsConstructed (build luon available, khong can swap tool).
    /// </summary>
    public sealed class TowerPresenter : MonoBehaviour, IPointerClickHandler
    {
        private TowerModel _model;
        private TowerView _view;
        private GridManager _gridManager;
        private BuildingController _buildingController;
        private DungeonBuilder.Core.EventBus _eventBus;

        public TowerModel Model => _model;

        [Inject]
        public void Construct(BuildingController buildingController, GridManager gridManager, DungeonBuilder.Core.EventBus eventBus)
        {
            _buildingController = buildingController;
            _gridManager = gridManager;
            _eventBus = eventBus;
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
            _buildingController?.RequestUpgradeTower(gridPos);
        }

        /// <summary>
        /// Goi tu TowerView.RemoveButton.onClick.
        /// </summary>
        public void RequestRemove()
        {
            Vector2Int gridPos = GetGridPosition();
            DBLog.Info($"tower.remove.request.{gridPos}", $"[TowerPresenter] Remove request. grid={gridPos}.", 0.25f, this);
            _buildingController?.RequestRemoveTower(gridPos);
        }

        /// <summary>
        /// Physics2D Raycaster hit BoxCollider2D tren root. Tool swap was removed,
        /// so clicking a constructed tower always toggles its action panel.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            _eventBus?.RaiseTowerClicked(this);
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

        private void Update()
        {
            if (_view == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient) return;

            var localClient = NetworkManager.Singleton.LocalClient;
            if (localClient?.PlayerObject != null)
            {
                float dist = Vector3.Distance(transform.position, localClient.PlayerObject.transform.position);
                if (dist < 4f)
                {
                    _view.ShowProximityUI();
                }
                else
                {
                    _view.HideProximityUI();
                }
            }
        }
    }
}
