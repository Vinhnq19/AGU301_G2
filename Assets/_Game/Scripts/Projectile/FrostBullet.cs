using Assets._Game.Scripts.Enemy;
using UnityEngine;

namespace DungeonBuilder.Projectile
{
    /// <summary>
    /// Đạn băng: gây damage + làm chậm enemy trong thời gian nhất định.
    /// </summary>
    public sealed class FrostBullet : BaseBullet
    {
        [SerializeField, Range(0.1f, 1f)] private float _slowMultiplier = 0.5f;
        [SerializeField, Min(0.1f)] private float _slowDuration = 2f;

        protected override void OnHit(BaseEnemy target)
        {
            base.OnHit(target);
            target?.ApplySlow(_slowMultiplier, _slowDuration);
        }
    }
}
