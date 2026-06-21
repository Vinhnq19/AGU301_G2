using Assets._Game.Scripts.Enemy;
using DungeonBuilder.Core.Interfaces;
using UnityEngine;

namespace DungeonBuilder.Enemy.Types
{
    /// <summary>
    /// Spitter: Quái vật tấn công tầm xa bằng cách bắn đạn acid.
    /// Không bị cản lại bởi tường (bắn qua tường).
    /// </summary>
    public sealed class SpitterEnemy : BaseEnemy
    {
        private Transform _currentTarget;
        private readonly Collider2D[] _targetScanResults = new Collider2D[16];

        public override bool IsBlockedByWall()
        {
            return false;
        }

        public override bool IsCoreInAttackRange()
        {
            _currentTarget = FindNearestTarget();
            return _currentTarget != null;
        }

        public override void AttackCore()
        {
            if (!IsServer) return;

            if (Time.time - _lastAttackTime < _attackInterval)
            {
                return;
            }

            if (_currentTarget == null)
            {
                _currentTarget = FindNearestTarget();
            }
            if (_currentTarget == null) return;

            _lastAttackTime = Time.time;

            // Bắn đạn acid màu xanh lá cây, kích thước nhỏ gọn (size = 0.7)
            ShootProjectileAt(_currentTarget, new Color(0.2f, 0.9f, 0.2f, 1f), 0.7f);
        }

        private Transform FindNearestTarget()
        {
            int layerMask = ~LayerMask.GetMask("Enemy");
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(layerMask);
            filter.useLayerMask = true;
            filter.useTriggers = true;
            int count = Physics2D.OverlapCircle(transform.position, _attackRange, filter, _targetScanResults);

            Transform bestTower = null;
            float closestTowerDist = float.MaxValue;

            Transform bestHeroOrCore = null;
            float closestHeroOrCoreDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (_targetScanResults[i] == null) continue;

                // Kiểm tra xem có thể gây sát thương không
                IDamageable damageable = _targetScanResults[i].GetComponentInParent<IDamageable>();
                if (!IsValidTarget(damageable)) continue;

                float dist = Vector3.Distance(transform.position, _targetScanResults[i].transform.position);

                // Phân loại: Tháp canh (Tower) vs Anh hùng/Lõi (Hero/Core)
                var tower = damageable as DungeonBuilder.Building.BaseTower;
                if (tower != null)
                {
                    if (!tower.IsTargetable) continue; // Bỏ qua bẫy gai

                    if (dist < closestTowerDist)
                    {
                        closestTowerDist = dist;
                        bestTower = _targetScanResults[i].transform;
                    }
                }
                else
                {
                    // Anh hùng hoặc lõi hoặc các đối tượng damageable khác
                    if (dist < closestHeroOrCoreDist)
                    {
                        closestHeroOrCoreDist = dist;
                        bestHeroOrCore = _targetScanResults[i].transform;
                    }
                }
            }

            // Kiểm tra Core Target
            if (_coreTarget != null)
            {
                float coreDist = Vector3.Distance(transform.position, _coreTarget.position);
                if (coreDist <= _attackRange)
                {
                    if (coreDist < closestHeroOrCoreDist)
                    {
                        closestHeroOrCoreDist = coreDist;
                        bestHeroOrCore = _coreTarget;
                    }
                }
            }

            // Thứ tự ưu tiên: Tower > Core = Hero
            if (bestTower != null)
            {
                return bestTower;
            }
            return bestHeroOrCore;
        }
    }
}
