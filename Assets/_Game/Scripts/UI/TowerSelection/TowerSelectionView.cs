using DungeonBuilder.Core.Enums;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.UI.TowerSelection
{
    /// <summary>
    /// MonoBehaviour tren Canvas Screen Space.
    /// Hien thi danh sach tower co the xay khi player click o trong.
    /// Yeu cau: EventSystem trong scene, GraphicRaycaster tren Canvas nay.
    /// </summary>
    public sealed class TowerSelectionView : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _backdropButton; // Image trong suot toan man hinh — click de dong panel

        [Header("Tower Buttons")]
        [SerializeField] private TowerOptionButton[] _towerButtons;

        private TowerSelectionPresenter _presenter;

        private void Awake()
        {
            // Dam bao panel va backdrop bat dau o trang thai tat
            _panelRoot?.SetActive(false);
            if (_backdropButton != null)
            {
                _backdropButton.gameObject.SetActive(false);
            }
        }

        public void SetPresenter(TowerSelectionPresenter presenter)
        {
            _presenter = presenter;

            if (_backdropButton != null)
            {
                _backdropButton.onClick.RemoveAllListeners();
                _backdropButton.onClick.AddListener(() => _presenter?.Hide());
            }
        }

        public void Show()
        {
            _panelRoot?.SetActive(true);
            // Bat backdrop khi panel mo: click ngoai panel se dong panel
            if (_backdropButton != null)
            {
                _backdropButton.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            _panelRoot?.SetActive(false);
            // Tat backdrop khi panel dong: tranh block IsPointerOverGameObject()
            // tren toan man hinh khien BuilderTool.UseAction() khong chay duoc
            if (_backdropButton != null)
            {
                _backdropButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Goi tu BuilderTool: mo panel tai gridPos chi dinh.
        /// Tranh inject Presenter truc tiep vao BuilderTool (khac VContainer scope).
        /// </summary>
        public void RequestShowAt(Vector2Int gridPos)
        {
            _presenter?.ShowAt(gridPos);
        }

        /// <summary>
        /// Goi tu BuilderTool: dong panel.
        /// </summary>
        public void RequestHide()
        {
            _presenter?.Hide();
        }

        /// <summary>
        /// Cap nhat trang thai hien thi cua tung TowerOptionButton theo model.
        /// </summary>
        public void Render(TowerSelectionModel model)
        {
            if (model == null || _towerButtons == null) return;

            for (int i = 0; i < _towerButtons.Length; i++)
            {
                if (_towerButtons[i] == null) continue;

                if (i >= model.AvailableTowers.Length)
                {
                    _towerButtons[i].gameObject.SetActive(false);
                    continue;
                }

                var data = model.AvailableTowers[i];
                bool canAfford = model.CanAfford(data.towerType);
                _towerButtons[i].gameObject.SetActive(true);
                _towerButtons[i].Setup(data, canAfford, OnTowerSelected);
            }
        }

        private void OnTowerSelected(TowerType towerType)
        {
            _presenter?.OnTowerSelected(towerType);
        }
    }
}
