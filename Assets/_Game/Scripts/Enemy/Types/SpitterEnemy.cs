using Assets._Game.Scripts.Enemy;
using UnityEngine;

namespace DungeonBuilder.Enemy.Types
{
    /// <summary>
    /// Spitter: Quái vật tấn công tầm xa bằng cách bắn đạn acid.
    /// Không bị cản lại bởi tường (bắn qua tường).
    /// </summary>
    public sealed class SpitterEnemy : BaseEnemy
    {
        public override bool IsBlockedByWall()
        {
            return false;
        }

        public override void AttackCore()
        {
            if (!IsServer) return;

            if (Time.time - _lastAttackTime < _attackInterval)
            {
                return;
            }

            if (_coreTarget == null) return;

            _lastAttackTime = Time.time;

            // Bắn đạn acid màu xanh lá cây, kích thước nhỏ gọn (size = 0.7)
            ShootProjectileAt(_coreTarget, new Color(0.2f, 0.9f, 0.2f, 1f), 0.7f);
        }
    }
}
