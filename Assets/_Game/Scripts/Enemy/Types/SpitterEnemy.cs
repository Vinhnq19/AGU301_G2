using Assets._Game.Scripts.Enemy;
using UnityEngine;

namespace DungeonBuilder.Enemy.Types
{
    /// <summary>
    /// Spitter: quái tầm xa bắn đạn acid, không bị tường cản (bắn qua tường).
    ///
    /// Thứ tự ưu tiên mục tiêu KHÔNG còn hardcode trong đây nữa — cấu hình bằng
    /// Targeting Profile trên prefab (mặc định nên là Tower → Player → Core).
    /// Thêm hành vi giữ khoảng cách: đã trong tầm bắn thì lùi ra, không đi vào melee.
    /// </summary>
    public sealed class SpitterEnemy : BaseEnemy
    {
        [Header("Spitter")]
        [Tooltip("Tỉ lệ tầm bắn muốn giữ. 0.7 = cố giữ khoảng cách ~70% tầm bắn; " +
                 "gần hơn thì lùi ra. Để 0 = không lùi (như cũ).")]
        [SerializeField, Range(0f, 1f)] private float _preferredRangeRatio = 0.7f;

        [Tooltip("Tốc độ lùi ra so với tốc độ đi thường.")]
        [SerializeField, Range(0.1f, 1f)] private float _backpedalSpeedRatio = 0.6f;

        public override bool IsBlockedByWall()
        {
            return false;
        }

        public override void MoveTowardsTarget()
        {
            // Giữ khoảng cách: nếu mục tiêu lại quá gần thì lùi ra thay vì áp sát.
            if (_preferredRangeRatio > 0f && CurrentTarget != null)
            {
                float preferred = _attackRange * _preferredRangeRatio;
                float dist = Vector3.Distance(transform.position, CurrentTarget.position);

                if (dist < preferred)
                {
                    Vector3 away = (transform.position - CurrentTarget.position);
                    if (away.sqrMagnitude > 0.0001f)
                    {
                        away.Normalize();
                        transform.position += away * (MoveSpeed * _backpedalSpeedRatio * Time.deltaTime);
                    }
                    return;
                }
            }

            base.MoveTowardsTarget();
        }

        public override void AttackCurrentTarget()
        {
            if (!IsServer) return;

            if (Time.time - _lastAttackTime < _attackInterval)
            {
                return;
            }

            if (CurrentTarget == null) return;

            _lastAttackTime = Time.time;

            // Bắn đạn acid màu xanh lá cây, kích thước nhỏ gọn (size = 0.7)
            ShootProjectileAt(CurrentTarget, new Color(0.2f, 0.9f, 0.2f, 1f), 0.7f);
        }
    }
}
