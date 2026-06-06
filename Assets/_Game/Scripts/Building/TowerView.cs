using DungeonBuilder.Building;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets._Game.Scripts.Building
{
    /// <summary>
    /// View cua tower: hien thi level, range indicator, panel Upgrade/Remove, panel Construction.
    /// Yeu cau Physics2D Raycaster tren camera va EventSystem trong scene.
    /// </summary>
    public sealed class TowerView : MonoBehaviour
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

        [Header("Construction Panel")]
        [SerializeField] private GameObject _constructionPanel;
        [SerializeField] private TMP_Text _constructionProgressText;
        [SerializeField] private Button _contributeButton;
        [SerializeField] private Button _constructionRemoveButton;

        private TowerPresenter _presenter;

        /// <summary>
        /// Wire buttons vao presenter. Goi boi TowerPresenter.Initialize().
        /// </summary>
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

            if (_contributeButton != null)
            {
                _contributeButton.onClick.RemoveAllListeners();
                _contributeButton.onClick.AddListener(() => _presenter?.RequestContribute());
            }

            if (_constructionRemoveButton != null)
            {
                _constructionRemoveButton.onClick.RemoveAllListeners();
                _constructionRemoveButton.onClick.AddListener(() => _presenter?.RequestRemove());
            }
        }

        /// <summary>
        /// Cap nhat UI theo TowerModel hien tai (level, range, upgrade cost).
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
                _upgradeButton.interactable = model.CanUpgrade && model.IsConstructed;
            }

            if (_upgradeCostText != null)
            {
                _upgradeCostText.text = model.CanUpgrade
                    ? $"Upgrade: {model.UpgradeWoodCost}W / {model.UpgradeOreCost}O"
                    : "MAX";
            }
        }

        /// <summary>
        /// Cap nhat Construction Panel theo tien do dong gop tai nguyen.
        /// Hien construction panel khi chua xay xong, an khi da active.
        /// </summary>
        public void RenderConstruction(TowerModel model)
        {
            if (model == null) return;

            bool done = model.IsConstructed;
            _constructionPanel?.SetActive(!done);

            if (!done && _constructionProgressText != null)
            {
                _constructionProgressText.text = model.OreRequired > 0
                    ? $"{model.WoodPaid}/{model.WoodRequired}W  {model.OrePaid}/{model.OreRequired}O"
                    : $"{model.WoodPaid}/{model.WoodRequired}W";
            }
        }

        /// <summary>
        /// Toggle panel Upgrade/Remove. Chi goi khi tower da IsConstructed.
        /// Goi tu TowerPresenter.OnPointerClick() tren root.
        /// </summary>
        public void TogglePanel()
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
