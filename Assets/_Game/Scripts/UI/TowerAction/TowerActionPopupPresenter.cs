using System;
using Assets._Game.Scripts.Building;
using DungeonBuilder.Core;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using DungeonBuilder.Core.Interfaces;

namespace DungeonBuilder.UI.TowerAction
{
    public sealed class TowerActionPopupPresenter : IInitializable, IDisposable
    {
        private readonly EventBus _eventBus;
        private readonly TowerActionPopupView _view;
        private readonly IResourceService _resources;
        
        private TowerPresenter _currentTower;

        [Inject]
        public TowerActionPopupPresenter(EventBus eventBus, TowerActionPopupView view, IResourceService resources)
        {
            _eventBus = eventBus;
            _view = view;
            _resources = resources;
        }

        public void Initialize()
        {
            if (_eventBus != null)
            {
                _eventBus.OnTowerClicked += HandleTowerClicked;
            }

            if (_view != null)
            {
                _view.OnUpgradeClicked += HandleUpgradeClicked;
                _view.OnRemoveClicked += HandleRemoveClicked;
                _view.OnCloseClicked += HandleCloseClicked;
            }
        }

        public void Dispose()
        {
            if (_eventBus != null)
            {
                _eventBus.OnTowerClicked -= HandleTowerClicked;
            }

            if (_view != null)
            {
                _view.OnUpgradeClicked -= HandleUpgradeClicked;
                _view.OnRemoveClicked -= HandleRemoveClicked;
                _view.OnCloseClicked -= HandleCloseClicked;
            }
        }

        private void HandleTowerClicked(TowerPresenter tower)
        {
            if (tower == null || tower.Model == null) return;

            // Xóa theo dõi tháp cũ nếu có
            if (_currentTower != null && _currentTower.Model != null)
            {
                _currentTower.Model.OnChanged -= RefreshView;
            }

            _currentTower = tower;
            _currentTower.Model.OnChanged += RefreshView;

            RefreshView();
            _view.Show();
        }

        private void RefreshView()
        {
            if (_currentTower != null && _view != null)
            {
                _view.Render(_currentTower.Model);
            }
        }

        private void HandleUpgradeClicked()
        {
            if (_currentTower != null && _currentTower.Model != null)
            {
                if (_resources != null && !_resources.CanAfford(_currentTower.Model.UpgradeCost))
                {
                    _view.PlayInsufficientFundsAnimation();
                    return;
                }

                _currentTower.RequestUpgrade();
                // Không tự tắt Popup để người chơi có thể nâng cấp tiếp nếu đủ tiền
                // Nếu nâng cấp thất bại hoặc max level thì view sẽ tự render lại nút mờ đi.
            }
        }

        private void HandleRemoveClicked()
        {
            if (_currentTower != null)
            {
                _currentTower.RequestRemove();
                HandleCloseClicked(); // Bán xong thì tắt luôn
            }
        }

        private void HandleCloseClicked()
        {
            if (_currentTower != null && _currentTower.Model != null)
            {
                _currentTower.Model.OnChanged -= RefreshView;
            }
            _currentTower = null;
            
            _view?.Hide();
        }
    }
}
