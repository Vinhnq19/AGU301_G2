using Assets._Game.Scripts.Enemy;
using DG.Tweening;
using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Building
{
    /// <summary>
    /// Bẫy gai: Không bắn đạn. Khi đến lượt tấn công, đâm gai đồng thời lên
    /// tất cả kẻ địch trong phạm vi. Xuyên thấu nhiều mục tiêu (AoE).
    /// Zero-GC: Buffer _aoeBuffer và _aoeFilter cấp phát 1 lần, không tạo rác.
    /// Visuals: ClientRpc gọi animation bung gai trên tất cả máy.
    /// </summary>
    public sealed class SpikeTrapTower : BaseTower
    {
        [Header("Spike Trap")]
        [SerializeField] private Transform _spikeVisual;
        [SerializeField] private float _spikeAnimScale = 1.3f;
        [SerializeField] private float _spikeAnimDuration = 0.15f;

        // Buffer và filter được cache 1 lần - Zero GC, tuân thủ Agents.md
        private readonly Collider2D[] _aoeBuffer = new Collider2D[16];
        private ContactFilter2D _aoeFilter;

        public override void OnNetworkSpawn()
        {
            // Cache filter 1 lần duy nhất, không tạo mới trong vòng lặp
            _aoeFilter = new ContactFilter2D();
            _aoeFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
            _aoeFilter.useTriggers = false;

            base.OnNetworkSpawn();
        }

        /// <summary>
        /// Override: Không cần target đơn lẻ. Đâm gai vào toàn vùng AoE.
        /// </summary>
        protected override void FireAt(BaseEnemy target)
        {
            if (!IsServer || _model == null) return;

            float range = _model.Range;
            int count = Physics2D.OverlapCircle(transform.position, range, _aoeFilter, _aoeBuffer);
            float damage = _model.Damage;
            int hits = 0;

            for (int i = 0; i < count; i++)
            {
                if (_aoeBuffer[i] == null) continue;
                BaseEnemy enemy = _aoeBuffer[i].GetComponentInParent<BaseEnemy>();
                if (enemy == null || !enemy.IsSpawned) continue;

                enemy.TakeDamage(damage);
                hits++;
            }

            if (hits > 0)
            {
                DBLog.Info($"spike.hit.{NetworkObjectId}",
                    $"[SpikeTrapTower] AoE hit {hits} enemies. dmg={damage:0.0}, range={range:0.0}.",
                    0.1f, this);
                PlaySpikeAnimClientRpc();
            }
        }

        /// <summary>
        /// Phát animation đâm gai cho tất cả Client (Server + Client).
        /// Kill TRƯỚC rồi reset scale, sau đó mới chạy tween mới.
        /// </summary>
        [ClientRpc]
        private void PlaySpikeAnimClientRpc()
        {
            if (_spikeVisual == null) return;

            // BUG FIX: Kill trước để tween cũ không ghi đè scale reset
            DOTween.Kill(_spikeVisual);
            _spikeVisual.localScale = Vector3.one;

            _spikeVisual.DOScale(_spikeAnimScale, _spikeAnimDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                    _spikeVisual.DOScale(1f, _spikeAnimDuration)
                        .SetEase(Ease.InBack));
        }
    }
}
