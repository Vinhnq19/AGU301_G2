using Assets._Game.Scripts.Enemy;
using UnityEngine;

namespace DungeonBuilder.Enemy.Types
{
    /// <summary>
    /// Runner: Zombie nhiễm phóng xạ yếu nhất, cận chiến gây 5 DMG, delay 1s.
    ///
    /// Hành vi riêng: NƯỚC RÚT — khi đã tới gần core thì tăng tốc, tạo cảm giác cấp bách
    /// và buộc người chơi phải xử lý dứt điểm ở vòng phòng thủ trong.
    /// </summary>
    public sealed class RunnerEnemy : BaseEnemy
    {
        [Header("Runner — nước rút")]
        [Tooltip("Khoảng cách tới core bắt đầu nước rút (unit). Để 0 = tắt.")]
        [SerializeField, Min(0f)] private float _sprintDistance = 3.5f;

        [Tooltip("Hệ số tăng tốc khi nước rút.")]
        [SerializeField, Range(1f, 3f)] private float _sprintMultiplier = 1.6f;

        public override float MoveSpeed
        {
            get
            {
                float speed = base.MoveSpeed;
                if (_sprintDistance <= 0f || _coreTarget == null)
                {
                    return speed;
                }

                return Vector3.Distance(transform.position, _coreTarget.position) <= _sprintDistance
                    ? speed * _sprintMultiplier
                    : speed;
            }
        }
    }
}
