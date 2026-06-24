using System;
using System.Linq;
using Assets._Game.Scripts.Building;
using Assets._Game.Scripts.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.UI.TowerAction
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TowerActionPopupView : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_Text _towerNameText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _upgradeCostText;
        
        [Header("Buttons")]
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _removeButton;
        [SerializeField] private Button _closeButton;

        private CanvasGroup _canvasGroup;

        public event Action OnUpgradeClicked;
        public event Action OnRemoveClicked;
        public event Action OnCloseClicked;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            
            if (_upgradeButton != null)
                _upgradeButton.onClick.AddListener(() => OnUpgradeClicked?.Invoke());
                
            if (_removeButton != null)
                _removeButton.onClick.AddListener(() => OnRemoveClicked?.Invoke());
                
            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());

            // Ẩn mặc định
            gameObject.SetActive(false);
            _canvasGroup.alpha = 0f;
        }

        public void Render(TowerModel model)
        {
            if (model == null) return;

            if (_levelText != null)
                _levelText.text = $"Level {model.Level}";

            if (_towerNameText != null && model.Data != null)
            {
                _towerNameText.text = model.Data.towerType switch
                {
                    DungeonBuilder.Core.Enums.TowerType.Arrow => "Arrow Tower",
                    DungeonBuilder.Core.Enums.TowerType.Cannon => "Cannon Tower",
                    DungeonBuilder.Core.Enums.TowerType.Frost => "Frost Tower",
                    DungeonBuilder.Core.Enums.TowerType.SpikeTrap => "Spike Trap",
                    DungeonBuilder.Core.Enums.TowerType.Laser => "Laser Tower",
                    _ => model.Data.towerType.ToString()
                };
            }

            if (_upgradeButton != null)
                _upgradeButton.interactable = model.CanUpgrade;

            if (_upgradeCostText != null)
            {
                if (model.CanUpgrade)
                {
                    string costStr = model.UpgradeCost.Count > 0
                        ? string.Join("  ", model.UpgradeCost.Select(c => $"{c.amount}{ResourceCost.Abbr(c.type)}"))
                        : "Free";
                    _upgradeCostText.text = costStr;
                }
                else
                {
                    _upgradeCostText.text = "MAX LEVEL";
                }
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad);
        }

        public void Hide()
        {
            _canvasGroup.DOKill();
            _canvasGroup.DOFade(0f, 0.2f).SetEase(Ease.InQuad).OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }

        public void PlayInsufficientFundsAnimation()
        {
            if (_upgradeButton != null)
            {
                _upgradeButton.transform.DOKill(complete: true);
                _upgradeButton.transform.DOShakePosition(0.5f, new Vector3(10f, 0, 0), 20, 90f, false, true);
            }

            if (_upgradeCostText != null)
            {
                _upgradeCostText.DOKill(complete: true);
                Color originalColor = _upgradeCostText.color;
                _upgradeCostText.DOColor(Color.red, 0.1f).SetLoops(4, LoopType.Yoyo).OnComplete(() =>
                {
                    _upgradeCostText.color = originalColor;
                });
            }
        }
    }
}
