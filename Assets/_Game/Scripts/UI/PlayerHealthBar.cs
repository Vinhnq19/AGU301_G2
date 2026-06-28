using DG.Tweening;
using DungeonBuilder.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.UI
{
    /// <summary>
    /// Thanh máu dưới chân player. Lắng nghe <see cref="PlayerStats.OnHPChanged"/>
    /// (replicated qua NetworkVariable) và cập nhật fill local. Tự billboard về camera.
    ///
    /// Khi player chết: chuyển sang hiển thị revive progress (màu vàng) thay cho HP (đỏ).
    /// Server tick progress qua <see cref="PlayerStats.OnReviveStateChanged"/>.
    /// </summary>
    public sealed class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _backgroundImage;
        [SerializeField, Min(0.01f)] private float _fillTransitionDuration = 0.15f;
        [SerializeField] private bool _hideWhenFull = true;
        [SerializeField] private Color _hpFillColor = new Color(0.85f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color _reviveFillColor = new Color(1f, 0.82f, 0.2f, 1f);

        private PlayerStats _stats;
        private Tween _fillTween;
        private float _currentFill = 1f;
        private bool _hasReceivedFirstUpdate;
        private bool _isShowingRevive;

        private void Awake()
        {
            _stats = GetComponentInParent<PlayerStats>();
        }

        private void OnEnable()
        {
            if (_stats == null)
            {
                _stats = GetComponentInParent<PlayerStats>();
            }

            if (_stats != null)
            {
                _stats.OnHPChanged += HandleHPChanged;
                _stats.OnDeadStateChanged += HandleDeadStateChanged;
                _stats.OnReviveStateChanged += HandleReviveStateChanged;
                InitializeFromCurrent();
            }
        }

        private void OnDisable()
        {
            if (_stats != null)
            {
                _stats.OnHPChanged -= HandleHPChanged;
                _stats.OnDeadStateChanged -= HandleDeadStateChanged;
                _stats.OnReviveStateChanged -= HandleReviveStateChanged;
                _stats = null;
            }

            _hasReceivedFirstUpdate = false;
            _isShowingRevive = false;
            KillFillTween();
        }

        private void LateUpdate()
        {
            if (Camera.main != null)
            {
                transform.rotation = Camera.main.transform.rotation;
            }
        }

        private void InitializeFromCurrent()
        {
            if (_stats == null) return;

            bool isDead = _stats.IsDead;
            _isShowingRevive = isDead;

            float fill = ComputeDesiredFill(isDead);
            _currentFill = fill;
            ApplyFillImmediate(fill);
            ApplyFillColor(isDead);
            UpdateVisibility(fill, isDead);
        }

        private float ComputeDesiredFill(bool isDead)
        {
            if (isDead)
            {
                return _stats.ReviveProgress;
            }

            float max = _stats.MaxHP;
            return max > 0f ? Mathf.Clamp01(_stats.CurrentHP / max) : 0f;
        }

        private void HandleHPChanged(float currentHP, float maxHP)
        {
            if (_stats == null || _stats.IsDead || _isShowingRevive) return;

            float targetFill = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;

            if (!_hasReceivedFirstUpdate)
            {
                _hasReceivedFirstUpdate = true;
                _currentFill = targetFill;
                KillFillTween();
                ApplyFillImmediate(targetFill);
            }
            else
            {
                AnimateFillTo(targetFill);
            }
            UpdateVisibility(targetFill, false);
        }

        private void HandleDeadStateChanged(bool isDead)
        {
            _isShowingRevive = isDead;
            ApplyFillColor(isDead);

            if (isDead)
            {
                // Vừa chết: chuyển sang hiển thị revive (hiện tại = 0).
                _hasReceivedFirstUpdate = true;
                AnimateFillTo(0f);
                UpdateVisibility(0f, true);
            }
            else
            {
                // Vừa được hồi sinh: fill = HP hiện tại (thường là _reviveHealFraction).
                _hasReceivedFirstUpdate = true;
                float max = _stats.MaxHP;
                float fill = max > 0f ? Mathf.Clamp01(_stats.CurrentHP / max) : 0f;
                _currentFill = fill;
                KillFillTween();
                ApplyFillImmediate(fill);
                UpdateVisibility(fill, false);
            }
        }

        private void HandleReviveStateChanged(float progress, ulong reviverClientId)
        {
            if (_stats == null || !_stats.IsDead || !_isShowingRevive) return;

            AnimateFillTo(progress);
            UpdateVisibility(progress, true);
        }

        private void ApplyFillColor(bool isDead)
        {
            if (_fillImage != null)
            {
                _fillImage.color = isDead ? _reviveFillColor : _hpFillColor;
            }
        }

        private void AnimateFillTo(float targetFill)
        {
            if (_fillImage == null) return;

            _currentFill = targetFill;
            _fillTween?.Kill();
            _fillTween = _fillImage.DOFillAmount(targetFill, _fillTransitionDuration).SetEase(Ease.OutQuad);
        }

        private void ApplyFillImmediate(float fill)
        {
            if (_fillImage != null)
            {
                _fillImage.fillAmount = fill;
            }
        }

        private void UpdateVisibility(float fill, bool isDead)
        {
            // Khi đang chết: luôn hiện (kể cả progress = 0, để thấy nền đợi cứu).
            // Khi sống: ẩn khi HP đầy (nếu _hideWhenFull).
            bool shouldShow = isDead ? true : (!_hideWhenFull || fill < 0.999f);
            if (_backgroundImage != null) _backgroundImage.enabled = shouldShow;
            if (_fillImage != null) _fillImage.enabled = shouldShow;
        }

        private void KillFillTween()
        {
            _fillTween?.Kill();
            _fillTween = null;
        }
    }
}