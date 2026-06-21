using System.Linq;
using Assets._Game.Scripts.Building;
using Assets._Game.Scripts.Data;
using DG.Tweening;
using DungeonBuilder.Building;
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
        [SerializeField] private LineRenderer _rangeLine;
        [SerializeField] private int _circleSegments = 40;

        [Header("Action Panel")]
        [SerializeField] private GameObject _actionPanel;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _removeButton;
        [SerializeField] private TMP_Text _upgradeCostText;

        [Header("Health Bar")]
        [SerializeField] private Image _healthFillImage;
        [SerializeField] private CanvasGroup _healthBarGroup;

        private TowerPresenter _presenter;
        private bool _isProximityUiVisible = false;
        private CanvasGroup _actionPanelGroup;
        private Vector3 _baseRangeScale = Vector3.one;
        private Tween _autoCloseTween;

        private void Awake()
        {
            if (_rangeLine != null)
            {
                Color c = _rangeLine.startColor;
                c.a = 0f;
                _rangeLine.startColor = c;
                _rangeLine.endColor = c;
                _rangeLine.useWorldSpace = false;
                _rangeLine.loop = true;
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
        }

        /// <summary>
        /// Cap nhat UI theo TowerModel hien tai (level, range, upgrade cost).
        /// </summary>
        public void Render(TowerModel model)
        {
            if (model == null) return;

            SetText(_levelText, $"Lv{model.Level}");

            if (_rangeLine != null && model.Range > 0)
            {
                DrawCircle(_rangeLine, model.Range, _circleSegments);

                _baseRangeScale = Vector3.one;
                _rangeLine.transform.DOKill();
                _rangeLine.transform.localScale = _baseRangeScale;

                if (_isProximityUiVisible)
                {
                    _rangeLine.transform.DOScale(_baseRangeScale * 1.05f, 1f)
                        .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                }
            }

            if (_upgradeButton != null)
            {
                _upgradeButton.interactable = model.CanUpgrade;
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

            if (_healthFillImage != null && model.MaxHealth > 0)
            {
                float targetFill = Mathf.Clamp01(model.CurrentHealth / model.MaxHealth);
                _healthFillImage.DOKill();
                _healthFillImage.DOFillAmount(targetFill, 0.25f).SetEase(Ease.OutCubic);

                if (_healthBarGroup != null && _healthBarGroup.alpha < 1f)
                {
                    _healthBarGroup.DOKill();
                    _healthBarGroup.DOFade(1f, 0.3f);
                }
            }
        }

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

                // Tự động tắt panel sau 3 giây
                _autoCloseTween?.Kill();
                _autoCloseTween = DOVirtual.DelayedCall(3f, HidePanel);
            }
        }

        public void HidePanel()
        {
            _autoCloseTween?.Kill();

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

            if (_rangeLine != null)
            {
                DOTween.Kill(_rangeLine);
                float startA = _rangeLine.startColor.a;
                DOVirtual.Float(startA, 0.5f, 0.3f, (a) => {
                    if (_rangeLine == null) return;
                    Color c = _rangeLine.startColor; c.a = a;
                    _rangeLine.startColor = c;
                    _rangeLine.endColor = c;
                }).SetId(_rangeLine);

                _rangeLine.transform.DOKill();
                _rangeLine.transform.localScale = _baseRangeScale; // Reset về scale chuẩn trước khi anim
                _rangeLine.transform.DOScale(_baseRangeScale * 1.05f, 1f)
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

            if (_rangeLine != null)
            {
                DOTween.Kill(_rangeLine);
                float startA = _rangeLine.startColor.a;
                DOVirtual.Float(startA, 0f, 0.3f, (a) => {
                    if (_rangeLine == null) return;
                    Color c = _rangeLine.startColor; c.a = a;
                    _rangeLine.startColor = c;
                    _rangeLine.endColor = c;
                }).SetId(_rangeLine);

                _rangeLine.transform.DOKill();
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

        private void DrawCircle(LineRenderer line, float radius, int segments)
        {
            line.positionCount = segments;
            float angle = 0f;
            for (int i = 0; i < segments; i++)
            {
                float x = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
                float y = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
                line.SetPosition(i, new Vector3(x, y, 0f));
                angle += (360f / segments);
            }
        }
    }
}
