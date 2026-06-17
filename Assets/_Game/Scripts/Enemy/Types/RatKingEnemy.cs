using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Enemy;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Enemy.Types
{
    /// <summary>
    /// Rat King (Boss): Trùm chương 1, HP 3000, DMG 15.
    /// Kỹ năng:
    /// - Melee: Vả cận chiến gây 15 DMG mỗi 1.5s.
    /// - Magic Projectiles: Bắn 6 tia ma pháp gây 30 DMG (200% phép), cooldown 5s.
    /// - Self-Healing: Gục 5s hồi 2 HP/s khi HP < 75% (cooldown 10s).
    /// - Summon: Triệu hồi 10 Runner tại các cổng ngẫu nhiên mỗi 30s.
    /// </summary>
    public sealed class RatKingEnemy : BaseEnemy
    {
        [Header("Boss Config")]
        [SerializeField] private NetworkObject _runnerPrefab;

        private float _magicCooldownTimer = 5f;
        private float _summonCooldownTimer = 30f;
        private float _healCooldownTimer = 0f;

        private bool _isHealing = false;
        private float _healDurationTimer = 0f;
        private float _healTickTimer = 0f;

        private Transform[] _gates;

        private void Start()
        {
            FindGates();
        }

        protected override void Update()
        {
            base.Update();
            if (!IsServer || _isDying) return;

            // Xử lý cơ chế hồi máu tự trị
            if (_isHealing)
            {
                _healDurationTimer -= Time.deltaTime;
                _healTickTimer -= Time.deltaTime;

                if (_healTickTimer <= 0f)
                {
                    _healTickTimer = 1f;
                    Heal(2f); // Hồi 2 HP theo đúng GDD
                }

                if (_healDurationTimer <= 0f)
                {
                    _isHealing = false;
                    _slowMultiplier = 1f; // Phục hồi di chuyển
                    _healCooldownTimer = 10f; // Reset cooldown hồi máu
                    StopHealVisualClientRpc();
                }
                return; // Đang hồi phục sẽ không di chuyển/tấn công
            }

            // Giảm cooldown
            if (_healCooldownTimer > 0f) _healCooldownTimer -= Time.deltaTime;
            if (_magicCooldownTimer > 0f) _magicCooldownTimer -= Time.deltaTime;
            _summonCooldownTimer -= Time.deltaTime;

            // Kiểm tra HP để kích hoạt hồi máu
            if (CurrentHP < MaxHealth * 0.75f && _healCooldownTimer <= 0f)
            {
                _isHealing = true;
                _healDurationTimer = 5f;
                _healTickTimer = 0f;
                _slowMultiplier = 0f; // Đứng yên
                StartHealVisualClientRpc();
                return;
            }

            // Triệu hồi Runner mỗi 30s
            if (_summonCooldownTimer <= 0f)
            {
                _summonCooldownTimer = 30f;
                SummonRunners();
            }

            // Bắn đạn ma thuật ma pháp (Magic Projectiles) mỗi 5s
            if (_magicCooldownTimer <= 0f && _coreTarget != null && Vector3.Distance(transform.position, _coreTarget.position) <= 12f)
            {
                _magicCooldownTimer = 5f;
                CastMagicProjectiles();
            }
        }

        private void CastMagicProjectiles()
        {
            if (_coreTarget == null) return;

            // Bắn 6 viên đạn phép thuật màu tím rực rỡ (size = 1.5)
            for (int i = 0; i < 6; i++)
            {
                // Thêm độ lệch vị trí spawn ngẫu nhiên xung quanh boss
                Vector3 spawnOffset = UnityEngine.Random.insideUnitCircle * 0.4f;
                Vector3 spawnPos = transform.position + spawnOffset;
                
                // Spawn đạn phép
                NetworkObject bulletObj = _pool.Get(_projectilePrefab, spawnPos, Quaternion.identity);
                if (bulletObj != null)
                {
                    var bullet = bulletObj.GetComponent<DungeonBuilder.Projectile.EnemyProjectile>();
                    if (bullet != null)
                    {
                        // Sát thương 30 (200% DMG), tốc độ 8f, bay tới core
                        bullet.Initialize(30f, 8f, 5f, _coreTarget.GetComponentInParent<NetworkObject>().NetworkObjectId, _coreTarget.position, new Color(0.6f, 0.1f, 0.8f, 1f), Vector3.one * 1.5f);
                    }

                    if (!bulletObj.IsSpawned)
                    {
                        bulletObj.Spawn();
                    }
                }
            }
        }

        private void SummonRunners()
        {
            if (_runnerPrefab == null || _pool == null) return;
            if (_gates == null || _gates.Length == 0)
            {
                FindGates();
                if (_gates == null || _gates.Length == 0) return;
            }

            // Chọn cổng ngẫu nhiên trong 3 cổng
            Transform randomGate = _gates[UnityEngine.Random.Range(0, _gates.Length)];
            if (randomGate == null) return;

            for (int i = 0; i < 10; i++)
            {
                Vector3 spawnPos = randomGate.position + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.6f);
                NetworkObject runnerObj = _pool.Get(_runnerPrefab, spawnPos, Quaternion.identity);
                if (runnerObj != null)
                {
                    BaseEnemy enemy = runnerObj.GetComponent<BaseEnemy>();
                    if (enemy != null)
                    {
                        enemy.SetCoreTarget(_coreTarget);
                    }

                    if (!runnerObj.IsSpawned)
                    {
                        runnerObj.Spawn();
                    }
                }
            }
        }

        private void FindGates()
        {
            var north = GameObject.Find("Spawn_North")?.transform;
            var east = GameObject.Find("Spawn_East")?.transform;
            var west = GameObject.Find("Spawn_West")?.transform;

            var list = new List<Transform>();
            if (north != null) list.Add(north);
            if (east != null) list.Add(east);
            if (west != null) list.Add(west);

            _gates = list.ToArray();
        }

        [ClientRpc]
        private void StartHealVisualClientRpc()
        {
            // Hiệu ứng hồi máu: Boss chuyển sang màu xanh ngọc chớp nháy nhẹ
            if (_visual != null)
            {
                var sr = _visual.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(0.3f, 0.8f, 0.9f, 1f);
                }
            }
        }

        [ClientRpc]
        private void StopHealVisualClientRpc()
        {
            // Trả về màu đỏ của Boss
            if (_visual != null)
            {
                var sr = _visual.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(0.8f, 0.2f, 0.15f, 1f);
                }
            }
        }
    }
}
