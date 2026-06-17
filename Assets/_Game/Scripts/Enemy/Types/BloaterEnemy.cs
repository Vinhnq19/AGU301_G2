using Assets._Game.Scripts.Enemy;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Enemy.Types
{
    /// <summary>
    /// Bloater: Quái vật trâu bò, cận chiến gây 20 DMG mỗi 2s.
    /// Có kĩ năng bị động xả khí độc xung quanh gây 5 DMG/s cho người chơi trong phạm vi, kéo dài 3s, cooldown 5s.
    /// </summary>
    public sealed class BloaterEnemy : BaseEnemy
    {
        private float _gasActiveTimer;
        private float _gasCooldownTimer;
        private bool _isGasActive;
        private float _damageTickTimer;

        private void Update()
        {
            if (!IsServer || _isDying) return;

            if (_isGasActive)
            {
                _gasActiveTimer -= Time.deltaTime;
                _damageTickTimer -= Time.deltaTime;

                if (_damageTickTimer <= 0f)
                {
                    _damageTickTimer = 1f;
                    ApplyGasDamage();
                }

                if (_gasActiveTimer <= 0f)
                {
                    _isGasActive = false;
                    _gasCooldownTimer = 5f;
                    StopGasVisualClientRpc();
                }
            }
            else
            {
                _gasCooldownTimer -= Time.deltaTime;
                if (_gasCooldownTimer <= 0f)
                {
                    _isGasActive = true;
                    _gasActiveTimer = 3f;
                    _damageTickTimer = 0f; // Gây sát thương ngay lập tức khi kích hoạt
                    StartGasVisualClientRpc();
                }
            }
        }

        private void ApplyGasDamage()
        {
            // Tìm tất cả Player trong phạm vi 2.5f và gây 5 sát thương
            Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(transform.position, 2.5f, LayerMask.GetMask("Player"));
            foreach (var col in hitPlayers)
            {
                if (col == null) continue;
                var playerStats = col.GetComponentInParent<DungeonBuilder.Player.PlayerStats>();
                if (playerStats != null)
                {
                    playerStats.TakeDamage(5f, 0);
                }
            }
        }

        [ClientRpc]
        private void StartGasVisualClientRpc()
        {
            // Hiển thị vòng độc bằng cách nhuộm xanh nhẹ visual của Bloater để biểu hiện khí độc tỏa ra
            if (_visual != null)
            {
                var sr = _visual.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(0.4f, 0.9f, 0.4f, 1f); // Nhuộm xanh lá
                }
            }
        }

        [ClientRpc]
        private void StopGasVisualClientRpc()
        {
            // Trả về màu gốc (vàng đất của Bloater)
            if (_visual != null)
            {
                var sr = _visual.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(0.95f, 0.65f, 0.2f, 1f);
                }
            }
        }
    }
}
