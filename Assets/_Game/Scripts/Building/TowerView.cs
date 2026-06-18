using System.Linq;
using Assets._Game.Scripts.Building;
using DungeonBuilder.Building;
using Assets._Game.Scripts.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private bool _isProximityUiVisible = false;
        private CanvasGroup _actionPanelGroup;
        private Vector3 _baseRangeScale = Vector3.one;

        private void Awake()
        {
            if (_rangeCircle != null)
            {
                Color c = _rangeCircle.color;
                c.a = 0f;
                _rangeCircle.color = c;
            }

            if (_levelText != null)
            {
                Color c = _levelText.color;
                c.a = 0f;
                _levelText.color = c;
            }

            if (_actionPanel != null)
            {
                _actionPanelGroup = _actionPanel.GetComponent<CanvasGroup>();
                if (_actionPanelGroup == null) _actionPanelGroup = _actionPanel.AddComponent<CanvasGroup>();

                EventTrigger trigger = _actionPanel.GetComponent<EventTrigger>() ?? _actionPanel.AddComponent<EventTrigger>();
                EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((_) => HidePanel());
                trigger.triggers.Add(exitEntry);
                
                _actionPanel.SetActive(false);
            }
        }

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

            if (_rangeCircle != null && _rangeCircle.sprite != null)
            {
                float diameter = model.Range * 2f;
                float spriteSize = _rangeCircle.sprite.bounds.size.x;
                float targetScale = spriteSize > 0f ? diameter / spriteSize : diameter;

                // Bù trừ scale của parent để vòng tròn không bị méo (hình bầu dục)
                Vector3 parentScale = _rangeCircle.transform.parent != null ? _rangeCircle.transform.parent.lossyScale : Vector3.one;
                _baseRangeScale = new Vector3(
                    targetScale / (parentScale.x != 0 ? Mathf.Abs(parentScale.x) : 1f),
                    targetScale / (parentScale.y != 0 ? Mathf.Abs(parentScale.y) : 1f),
                    1f
                );

                _rangeCircle.transform.DOKill();
                _rangeCircle.transform.localScale = _baseRangeScale;

                if (_isProximityUiVisible)
                {
                    _rangeCircle.transform.DOScale(_baseRangeScale * 1.05f, 1f)
                        .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                }
            }

            if (_upgradeButton != null)
            {
                _upgradeButton.interactable = model.CanUpgrade && model.IsConstructed;
            }

            if (_upgradeCostText != null)
            {
                if (model.CanUpgrade)
                {
                    string costStr = model.UpgradeCost.Count > 0
                        ? string.Join("  ", model.UpgradeCost.Select(c => $"{c.amount}{ResourceCost.Abbr(c.type)}"))
                        : "Free";
                    _upgradeCostText.text = $"Upgrade: {costStr}";
                }
                else
                {
                    _upgradeCostText.text = "MAX";
                }
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
                string progress = model.BuildCost.Count > 0
                    ? string.Join("  ", model.BuildCost.Select(c =>
                        $"{model.GetPaid(c.type)}/{c.amount}{ResourceCost.Abbr(c.type)}"))
                    : "";
                _constructionProgressText.text = progress;
            }
        }

        /// <summary>
        /// Toggle panel Upgrade/Remove. Chi goi khi tower da IsConstructed.
        /// Goi tu TowerPresenter.OnPointerClick() tren root.
        /// </summary>
        public void TogglePanel()
        {
            if (_actionPanel == null) return;
            if (_actionPanel.activeSelf)
            {
                HidePanel();
            }
            else
            {
                _actionPanel.SetActive(true);
                if (_actionPanelGroup != null)
                {
                    _actionPanelGroup.DOKill();
                    _actionPanelGroup.alpha = 0f;
                    _actionPanelGroup.DOFade(1f, 0.2f);
                }
            }
        }

        public void HidePanel()
        {
            if (_actionPanel != null && _actionPanel.activeSelf)
            {
                if (_actionPanelGroup != null)
                {
                    _actionPanelGroup.DOKill();
                    _actionPanelGroup.DOFade(0f, 0.2f).OnComplete(() => _actionPanel.SetActive(false));
                }
                else
                {
                    _actionPanel.SetActive(false);
                }
            }
        }

        public void ShowProximityUI()
        {
            if (_isProximityUiVisible) return;
            _isProximityUiVisible = true;

            if (_rangeCircle != null)
            {
                _rangeCircle.DOKill();
                _rangeCircle.DOFade(0.15f, 0.3f);
                _rangeCircle.transform.DOKill();
                _rangeCircle.transform.localScale = _baseRangeScale; // Reset về scale chuẩn trước khi anim
                _rangeCircle.transform.DOScale(_baseRangeScale * 1.05f, 1f)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }

            if (_levelText != null)
            {
                _levelText.DOKill();
                _levelText.DOFade(1f, 0.3f);
            }
        }

        public void HideProximityUI()
        {
            if (!_isProximityUiVisible) return;
            _isProximityUiVisible = false;

            if (_rangeCircle != null)
            {
                _rangeCircle.DOKill();
                _rangeCircle.DOFade(0f, 0.3f);
                _rangeCircle.transform.DOKill();
            }

            if (_levelText != null)
            {
                _levelText.DOKill();
                _levelText.DOFade(0f, 0.3f);
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null) text.text = value;
        }
    }
}
