using System;
using Assets._Game.Scripts.Enemy;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Assets._Game.Scripts.UI
{
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _fillImage;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeDuration = 0.2f;
        [SerializeField] private float _hideDelay = 3.0f;
        [SerializeField] private float _fillTransitionDuration = 0.2f;

        private BaseEnemy _enemy;
        private Tween _fadeTween;
        private Tween _fillTween;

        private void OnEnable()
        {
            _enemy = GetComponentInParent<BaseEnemy>();
            ResetVisuals();
            if (_enemy != null)
            {
                _enemy.OnHealthChanged += HandleHealthChanged;
                InitializeHealthBar(_enemy.CurrentHP, _enemy.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (_enemy != null)
            {
                _enemy.OnHealthChanged -= HandleHealthChanged;
                _enemy = null;
            }
            KillTweens();
        }

        private void ResetVisuals()
        {
            KillTweens();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
            if (_fillImage != null)
            {
                _fillImage.fillAmount = 1f;
            }
        }

        private void InitializeHealthBar(float currentHP, float maxHP)
        {
            if (_fillImage != null && maxHP > 0f)
            {
                _fillImage.fillAmount = Mathf.Clamp01(currentHP / maxHP);
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        private void HandleHealthChanged(float currentHP, float maxHP)
        {
            if (_canvasGroup == null || _fillImage == null) return;

            if (currentHP <= 0f)
            {
                _canvasGroup.alpha = 0f;
                _fillImage.fillAmount = 0f;
                KillTweens();
                return;
            }

            float fillAmount = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;

            _fillTween?.Kill();
            _fillTween = _fillImage.DOFillAmount(fillAmount, _fillTransitionDuration);

            _fadeTween?.Kill();
            
            Sequence seq = DOTween.Sequence();
            seq.Append(_canvasGroup.DOFade(1f, _fadeDuration));
            seq.AppendInterval(_hideDelay);
            seq.Append(_canvasGroup.DOFade(0f, _fadeDuration));
            _fadeTween = seq;
        }

        private void KillTweens()
        {
            _fadeTween?.Kill();
            _fadeTween = null;
            _fillTween?.Kill();
            _fillTween = null;
        }
    }
}