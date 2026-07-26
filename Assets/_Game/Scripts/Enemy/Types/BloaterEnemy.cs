using System.Collections.Generic;
using Assets._Game.Scripts.Enemy;
using DG.Tweening;
using DungeonBuilder.Core.Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Enemy.Types
{
    /// <summary>
    /// Bloater: Quái vật trâu bò, cận chiến.
    /// Có kĩ năng bị động xả khí độc xung quanh gây sát thương cho người chơi trong phạm vi.
    /// </summary>
    public sealed class BloaterEnemy : BaseEnemy
    {
        [Header("Bloater Config")]
        [SerializeField, Min(0.1f)] private float _gasRadius = 2.5f;
        [SerializeField, Min(0f)] private float _gasDamage = 5f;
        [SerializeField, Min(0.1f)] private float _gasDuration = 3f;
        [SerializeField, Min(0.1f)] private float _gasCooldown = 5f;
        [SerializeField, Min(0.1f)] private float _gasTickInterval = 1f;
        [Tooltip("Thời gian báo trước (giây) giữa lúc hiện vòng gas và lúc bắt đầu gây damage — " +
                 "cho người chơi kịp phản ứng/né. Để 0 = nổ ngay như cũ (không công bằng).")]
        [SerializeField, Min(0f)] private float _gasWindup = 0.7f;
        [SerializeField] private SpriteRenderer _gasFillVisual;

        private float _gasActiveTimer;
        private float _gasCooldownTimer;
        private float _gasWindupTimer;
        private bool _isWindingUp;
        private bool _isGasActive;
        private float _damageTickTimer;
        private readonly Collider2D[] _gasScanResults = new Collider2D[32];

        private LineRenderer _gasIndicator;
        private Tween _pulseTween;
        private Tween _fadeTween;
        private Tween _gasFillTween;

        protected override void Update()
        {
            base.Update();
            if (!IsServer || _isDying) return;

            if (_isGasActive)
            {
                _gasActiveTimer -= Time.deltaTime;
                _damageTickTimer -= Time.deltaTime;

                if (_damageTickTimer <= 0f)
                {
                    _damageTickTimer = _gasTickInterval;
                    ApplyGasDamage();
                }

                if (_gasActiveTimer <= 0f)
                {
                    _isGasActive = false;
                    _gasCooldownTimer = _gasCooldown;
                    StopGasVisualClientRpc();
                }
            }
            else if (_isWindingUp)
            {
                // Giai đoạn báo trước: vòng gas đã hiện nhưng CHƯA gây damage.
                _gasWindupTimer -= Time.deltaTime;
                if (_gasWindupTimer <= 0f)
                {
                    _isWindingUp = false;
                    _isGasActive = true;
                    _gasActiveTimer = _gasDuration;
                    _damageTickTimer = 0f; // hết windup mới bắt đầu tick damage
                }
            }
            else
            {
                _gasCooldownTimer -= Time.deltaTime;
                if (_gasCooldownTimer <= 0f)
                {
                    // Hiện telegraph trước, damage đến sau _gasWindup giây.
                    _isWindingUp = true;
                    _gasWindupTimer = _gasWindup;
                    StartGasVisualClientRpc();
                }
            }
        }

        private void ApplyGasDamage()
        {
            int layerMask = ~LayerMask.GetMask("Enemy");
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(layerMask);
            filter.useLayerMask = true;
            filter.useTriggers = true;

            int count = Physics2D.OverlapCircle(transform.position, _gasRadius, filter, _gasScanResults);
            HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

            for (int i = 0; i < count; i++)
            {
                if (_gasScanResults[i] == null) continue;

                IDamageable damageable = _gasScanResults[i].GetComponentInParent<IDamageable>();
                if (IsValidTarget(damageable))
                {
                    if (damagedTargets.Add(damageable))
                    {
                        damageable.TakeDamage(_gasDamage, 0);
                    }
                }
            }
        }

        public override void OnReturnToPool()
        {
            base.OnReturnToPool();
            // Reset cả trạng thái windup, tránh con tái dùng từ pool nổ gas ngay khi spawn.
            _isWindingUp = false;
            _isGasActive = false;
            _gasWindupTimer = 0f;
            _gasActiveTimer = 0f;
            _pulseTween?.Kill();
            _fadeTween?.Kill();
            _gasFillTween?.Kill();
            if (_gasIndicator != null)
            {
                _gasIndicator.enabled = false;
            }
            if (_gasFillVisual != null)
            {
                _gasFillVisual.enabled = false;
            }
        }

        private void EnsureGasIndicator()
        {
            if (_gasIndicator != null) return;

            GameObject obj = new GameObject("GasIndicator");
            obj.transform.SetParent(transform, false);
            _gasIndicator = obj.AddComponent<LineRenderer>();
            _gasIndicator.useWorldSpace = false;
            _gasIndicator.loop = true;
            _gasIndicator.startWidth = 0.08f;
            _gasIndicator.endWidth = 0.08f;

            Color c = new Color(0.2f, 0.8f, 0.2f, 0f);
            _gasIndicator.startColor = c;
            _gasIndicator.endColor = c;

            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                _gasIndicator.material = new Material(spriteShader);
            }

            int segments = 32;
            _gasIndicator.positionCount = segments;
            float angle = 0f;
            for (int i = 0; i < segments; i++)
            {
                float x = Mathf.Sin(angle) * _gasRadius;
                float y = Mathf.Cos(angle) * _gasRadius;
                _gasIndicator.SetPosition(i, new Vector3(x, y, 0f));
                angle += (2f * Mathf.PI) / segments;
            }

            _gasIndicator.enabled = false;
        }

        [ClientRpc]
        private void StartGasVisualClientRpc()
        {
            EnsureGasIndicator();
            if (_gasIndicator == null) return;

            _gasIndicator.enabled = true;
            
            _fadeTween?.Kill();
            _fadeTween = DOTween.To(() => _gasIndicator.startColor, color => {
                _gasIndicator.startColor = color;
                _gasIndicator.endColor = color;
            }, new Color(0.2f, 0.8f, 0.2f, 0.35f), 0.5f);

            _pulseTween?.Kill();
            _pulseTween = DOTween.To(() => _gasIndicator.startWidth, w => {
                _gasIndicator.startWidth = w;
                _gasIndicator.endWidth = w;
            }, 0.14f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

            if (_gasFillVisual != null)
            {
                _gasFillVisual.transform.localPosition = Vector3.zero;
                _gasFillVisual.transform.localScale = new Vector3(_gasRadius * 2f, _gasRadius * 2f, 1f);
                _gasFillVisual.enabled = true;
                _gasFillVisual.color = new Color(0.2f, 0.8f, 0.2f, 0.05f);
                _gasFillTween?.Kill();
                _gasFillTween = _gasFillVisual.DOFade(0.15f, 0.5f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
        }

        [ClientRpc]
        private void StopGasVisualClientRpc()
        {
            _pulseTween?.Kill();
            _fadeTween?.Kill();
            
            if (_gasIndicator != null)
            {
                _fadeTween = DOTween.To(() => _gasIndicator.startColor, color => {
                    _gasIndicator.startColor = color;
                    _gasIndicator.endColor = color;
                }, new Color(0.2f, 0.8f, 0.2f, 0f), 0.4f)
                .OnComplete(() => {
                    if (_gasIndicator != null) _gasIndicator.enabled = false;
                });
            }

            _gasFillTween?.Kill();
            if (_gasFillVisual != null)
            {
                _gasFillVisual.DOFade(0f, 0.4f).OnComplete(() => {
                    if (_gasFillVisual != null) _gasFillVisual.enabled = false;
                });
            }
        }
    }
}
