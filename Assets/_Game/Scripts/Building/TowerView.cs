using DungeonBuilder.Building;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets._Game.Scripts.Building
{
    /// <summary>
    /// View của tower: hiển thị level, range indicator, panel Upgrade/Remove.
    /// Yêu cầu Physics2D Raycaster trên camera và EventSystem trong scene.
    /// </summary>
    public sealed class TowerView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Info")]
        [SerializeField] private TMP_Text _levelText;

        [Header("Range")]
        [SerializeField] private SpriteRenderer _rangeCircle;

        [Header("Action Panel")]
        [SerializeField] private GameObject _actionPanel;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _removeButton;
        [SerializeField] private TMP_Text _upgradeCostText;

        private TowerPresenter _presenter;

        public void SetPresenter(TowerPresenter presenter)
        {
            _presenter = presenter;

            if (_upgradeButton != null)
            {
                _upgradeButton.onClick.RemoveAllListeners();
                _upgradeButton.onClick.AddListener(() => _presenter?.RequestUpgrade());
            }

            if (_removeButton != null)
            {
                _removeButton.onClick.RemoveAllListeners();
                _removeButton.onClick.AddListener(() => _presenter?.RequestRemove());
            }
        }

        /// <summary>
        /// Cập nhật toàn bộ UI theo TowerModel hiện tại.
        /// </summary>
        public void Render(TowerModel model)
        {
            if (model == null) return;

            SetText(_levelText, $"Lv{model.Level}");

            if (_rangeCircle != null)
            {
                float diameter = model.Range * 2f;
                _rangeCircle.transform.localScale = new Vector3(diameter, diameter, 1f);
            }

            if (_upgradeButton != null)
            {
                _upgradeButton.interactable = model.CanUpgrade;
            }

            if (_upgradeCostText != null)
            {
                _upgradeCostText.text = model.CanUpgrade
                    ? $"Upgrade: {model.UpgradeWoodCost}W / {model.UpgradeOreCost}O"
                    : "MAX";
            }
        }

        /// <summary>
        /// Toggle panel Upgrade/Remove khi player click vào tower.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_actionPanel == null) return;
            _actionPanel.SetActive(!_actionPanel.activeSelf);
        }

        public void HidePanel()
        {
            if (_actionPanel != null)
            {
                _actionPanel.SetActive(false);
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value;
        }
    }
}
