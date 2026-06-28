using Assets._Game.Scripts.Enemy;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using System;

namespace DungeonBuilder.Projectile
{
    /// <summary>
    /// Đạn đại bác: gây AoE damage cho mọi enemy trong bán kính tại điểm nổ.
    /// Có kèm animation phát nổ.
    /// </summary>
    public sealed class CannonBullet : BaseBullet
    {
        [Header("Explosion Settings")]
        [SerializeField, Min(0.1f)] private float _aoeRadius = 1.5f;
        [Tooltip("Gán Animator chứa Animation nổ vào đây")]
        [SerializeField] private Animator _explosionAnimator;
        [Tooltip("Thời gian tồn tại của vụ nổ trước khi đạn biến mất")]
        [SerializeField] private float _explosionDuration = 0.5f;

        // Buffer khai báo ở class level — tái sử dụng, Zero-GC
        private readonly Collider2D[] _aoeResults = new Collider2D[8];
        private ContactFilter2D _enemyFilter;

        private void Awake()
        {
            _enemyFilter = new ContactFilter2D();
            _enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
            _enemyFilter.useTriggers = false;

            // Đạn đại bác hình tròn nên không cần xoay bám theo mục tiêu
            _autoRotate = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Reset Animator về trạng thái gốc trên mọi Client khi đạn được lôi ra từ Pool
            if (_explosionAnimator != null)
            {
                _explosionAnimator.Rebind();
                _explosionAnimator.Update(0f);
            }
        }

        protected override async void OnHit(BaseEnemy target)
        {
            if (!IsServer) return;

            // 1. Dừng đạn lập tức, không cho phép update di chuyển và va chạm tiếp
            _isActive = false;

            // 2. Xử lý sát thương AoE
            int count = Physics2D.OverlapCircle(transform.position, _aoeRadius, _enemyFilter, _aoeResults);
            for (int i = 0; i < count; i++)
            {
                if (_aoeResults[i] == null) continue;
                BaseEnemy enemy = _aoeResults[i].GetComponentInParent<BaseEnemy>();
                enemy?.TakeDamage(Damage, 0);
            }

            // 3. Kích hoạt hiệu ứng cháy nổ trên toàn bộ Client
            PlayExplosionAnimClientRpc();

            // 4. Nếu có Animator, Server đợi animation diễn ra xong rồi mới trả về Pool
            if (_explosionAnimator != null)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_explosionDuration), cancellationToken: this.GetCancellationTokenOnDestroy());
                }
                catch (OperationCanceledException)
                {
                    // Object bị destroy giữa chừng -> an toàn thoát ra
                    return; 
                }
            }

            // Trả về Pool sau khi xong hiệu ứng
            ReturnToPool();
        }

        [ClientRpc]
        private void PlayExplosionAnimClientRpc()
        {
            if (DungeonBuilder.Audio.AudioManager.Instance != null)
            {
                DungeonBuilder.Audio.AudioManager.Instance.PlaySFX(DungeonBuilder.Core.Enums.SoundType.SFX_Canon_Boom, transform.position);
            }

            if (_explosionAnimator != null)
            {
                // Gọi trigger "Explode" để play animation vụ nổ
                _explosionAnimator.SetTrigger("Explode");
            }
            else
            {
                // Fallback nếu chưa set up Animator
                base.PlayHitEffectClientRpc();
            }
        }

        /// <summary>
        /// Vẽ AoE radius trực quan khi chọn object trong Editor.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // Filled: bán kính vùng sát thương AoE
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, _aoeRadius);
            // Wire: viền rõ ràng
            Gizmos.color = new Color(1f, 0.3f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, _aoeRadius);
        }
    }
}
