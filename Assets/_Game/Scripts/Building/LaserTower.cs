using Assets._Game.Scripts.Enemy;
using DG.Tweening;
using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Building
{
    /// <summary>
    /// Tháp laser: Bắn tia raycast xuyên thấu (Piercing) qua nhiều kẻ địch
    /// xếp thành hàng. Tốc độ sạc chậm nhưng sát thương cực lớn.
    /// Zero-GC: Dùng buffer RaycastHit2D[] tĩnh cấp phát 1 lần duy nhất.
    /// Visuals: ClientRpc(startPos, endPos) vẽ LineRenderer lóe sáng, mờ dần.
    /// </summary>
    public sealed class LaserTower : BaseTower
    {
        [Header("Laser Tower")]
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private float _laserDisplayDuration = 0.3f;
        [SerializeField, Min(0.01f)] private float _laserStartWidth = 0.3f;  // Đầu to
        [SerializeField, Min(0.01f)] private float _laserEndWidth   = 0.08f; // Đuôi nhỏ dần

        // Buffer tĩnh tái sử dụng - Zero GC allocation
        private readonly RaycastHit2D[] _raycastBuffer = new RaycastHit2D[16];
        private ContactFilter2D _raycastFilter;
        private Tweener _fadeTween;

        protected override void Awake()
        {
            base.Awake(); // Đảm bảo BaseTower khởi tạo _presenter, _view, _enemyFilter

            // Cache filter 1 lần - Zero GC
            _raycastFilter = new ContactFilter2D();
            _raycastFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
            _raycastFilter.useTriggers = false;

            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = false;
                // Áp dụng kích thước từ Inspector
                _lineRenderer.startWidth = _laserStartWidth;
                _lineRenderer.endWidth   = _laserEndWidth;
                
                // Đảm bảo LineRenderer không bị render dưới background
                _lineRenderer.sortingLayerName = "Default";
                _lineRenderer.sortingOrder = 50; // Set order cao để đè lên background
            }
        }

        /// <summary>
        /// Override: Bắn tia xuyên thấu theo hướng từ tháp đến mục tiêu.
        /// Server xử lý damage, sau đó broadcast vị trí tia để Client vẽ.
        /// </summary>
        protected override void FireAt(BaseEnemy target)
        {
            if (!IsServer || _model == null || target == null) return;

            Vector3 firePos = _firePoint != null ? _firePoint.position : transform.position;
            Vector2 dir = (target.transform.position - firePos).normalized;
            float range = _model.Range;
            float damage = _model.Damage;

            var filter = _raycastFilter;
            int count = Physics2D.Raycast(firePos, dir, filter, _raycastBuffer, range);
            int hits = 0;

            for (int i = 0; i < count; i++)
            {
                if (_raycastBuffer[i].collider == null) continue;
                BaseEnemy enemy = _raycastBuffer[i].collider.GetComponentInParent<BaseEnemy>();
                if (enemy == null || !enemy.IsSpawned) continue;

                enemy.TakeDamage(damage);
                hits++;
            }

            // Tia luôn kéo dài bằng đúng bán kính range, bất kể có trúng địch hay không
            Vector3 endPos = firePos + (Vector3)(dir * range);

            DBLog.Info($"laser.fire.{NetworkObjectId}",
                $"[LaserTower] Fired. pierced={hits}, dmg={damage:0.0}, range={range:0.0}.",
                0.1f, this);

            // Broadcast visual tia laser xuống tất cả Client
            ShowLaserClientRpc(firePos, endPos);
            PlayFireEffectClientRpc();
        }

        /// <summary>
        /// Vẽ tia laser lóe sáng và mờ dần bằng DOTween trên tất cả Client.
        /// </summary>
        [ClientRpc]
        private void ShowLaserClientRpc(Vector3 startPos, Vector3 endPos)
        {
            if (_lineRenderer == null) return;

            _fadeTween?.Kill();

            _lineRenderer.enabled = true;
            
            // Ép Z = -1 để đảm bảo LineRenderer nổi lên trên trong môi trường 2D
            startPos.z = -1f;
            endPos.z = -1f;
            
            _lineRenderer.SetPosition(0, startPos);
            _lineRenderer.SetPosition(1, endPos);

            // Đặt alpha về 1 để flash sáng
            Color startColor = _lineRenderer.startColor;
            Color endColor = _lineRenderer.endColor;
            startColor.a = 1f;
            endColor.a = 1f;
            _lineRenderer.startColor = startColor;
            _lineRenderer.endColor = endColor;

            // Mờ dần rồi tắt
            float alpha = 1f;
            _fadeTween = DOTween.To(() => alpha, a =>
            {
                alpha = a;
                Color sc = _lineRenderer.startColor; sc.a = a; _lineRenderer.startColor = sc;
                Color ec = _lineRenderer.endColor;   ec.a = a; _lineRenderer.endColor = ec;
            }, 0f, _laserDisplayDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    if (_lineRenderer != null) _lineRenderer.enabled = false;
                });
        }
    }
}
