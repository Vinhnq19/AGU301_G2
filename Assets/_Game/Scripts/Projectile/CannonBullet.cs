using Assets._Game.Scripts.Enemy;
using UnityEngine;

namespace DungeonBuilder.Projectile
{
    /// <summary>
    /// Đạn đại bác: gây AoE damage cho mọi enemy trong bán kính tại điểm nổ.
    /// </summary>
    public sealed class CannonBullet : BaseBullet
    {
        [SerializeField, Min(0.1f)] private float _aoeRadius = 1.5f;

        // Buffer khai báo ở class level — tái sử dụng, không new trong mỗi frame
        private readonly Collider2D[] _aoeResults = new Collider2D[8];
        private ContactFilter2D _enemyFilter;

        private void Awake()
        {
            _enemyFilter = new ContactFilter2D();
            _enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
            _enemyFilter.useTriggers = false;
        }

        protected override void OnHit(BaseEnemy target)
        {
            if (!IsServer) return;

            int count = Physics2D.OverlapCircle(transform.position, _aoeRadius, _enemyFilter, _aoeResults);
            for (int i = 0; i < count; i++)
            {
                if (_aoeResults[i] == null) continue;
                BaseEnemy enemy = _aoeResults[i].GetComponentInParent<BaseEnemy>();
                enemy?.TakeDamage(Damage, 0);
            }

            PlayHitEffectClientRpc();
            ReturnToPool();
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
