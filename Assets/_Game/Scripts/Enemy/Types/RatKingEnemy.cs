using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Enemy;
using DG.Tweening;
using DungeonBuilder.Core.Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Enemy.Types
{
    /// <summary>
    /// Rat King (Boss): Trùm chương 1, HP 3000, DMG 15.
    /// Kỹ năng:
    /// - Melee: Vả cận chiến gây 15 DMG mỗi 1.5s (dựa trên BaseEnemy).
    /// - Magic Lasers: Bắn 6 tia ma pháp tức thời (LineRenderer) gây 30 DMG (200% phép), cooldown 5s.
    /// - Self-Healing: Gục 5s hồi 2 HP/s khi HP < 75% (cooldown 10s).
    /// - Summon: Triệu hồi 10 Runner ngay xung quanh bản thân mỗi 30s.
    /// </summary>
    public sealed class RatKingEnemy : BaseEnemy
    {
        [Header("Boss Config")]
        [SerializeField] private NetworkObject _runnerPrefab;

        private const float _bossScale = 2.2f;

        private float _magicCooldownTimer = 5f;
        private float _summonCooldownTimer = 30f;
        private float _healCooldownTimer = 0f;

        private bool _isHealing = false;
        private float _healDurationTimer = 0f;
        private float _healTickTimer = 0f;

        private Transform[] _gates;
        private LineRenderer[] _laserRenderers;
        private readonly Collider2D[] _magicScanResults = new Collider2D[16];

        protected override void Awake()
        {
            base.Awake();

            // Thiết lập kích thước khổng lồ cho Boss
            if (_visual != null)
            {
                _visual.localScale = Vector3.one * _bossScale;
            }

            // Khởi tạo trước 6 LineRenderer để vẽ tia ma thuật (Zero-GC)
            _laserRenderers = new LineRenderer[6];
            for (int i = 0; i < 6; i++)
            {
                GameObject laserObj = new GameObject($"MagicLaser_{i}");
                laserObj.transform.SetParent(transform);
                laserObj.transform.localPosition = Vector3.zero;

                LineRenderer lr = laserObj.AddComponent<LineRenderer>();
                lr.enabled = false;
                lr.startWidth = 0.2f;
                lr.endWidth = 0.05f;
                lr.sortingLayerName = "Default";
                lr.sortingOrder = 50;

                // Màu tím ma pháp
                Color purple = new Color(0.7f, 0.2f, 0.9f, 1f);
                lr.startColor = purple;
                lr.endColor = new Color(0.7f, 0.2f, 0.9f, 0f);

                Shader spriteShader = Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    lr.material = new Material(spriteShader);
                }

                _laserRenderers[i] = lr;
            }
        }

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

            // Bắn 6 tia ma pháp (Magic Lasers) mỗi 5s
            if (_magicCooldownTimer <= 0f)
            {
                Transform target = FindNearestMagicTarget();
                if (target != null)
                {
                    _magicCooldownTimer = 5f;
                    CastMagicLasers(target);
                }
            }
        }

        private void CastMagicLasers(Transform target)
        {
            if (target == null) return;

            Vector3 startPos = transform.position;
            Vector3 targetPos = target.position;
            Vector2 baseDir = (targetPos - startPos).normalized;

            Vector3[] startPositions = new Vector3[6];
            Vector3[] endPositions = new Vector3[6];

            float[] angles = { -25f, -15f, -5f, 5f, 15f, 25f };
            HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

            for (int i = 0; i < 6; i++)
            {
                Vector2 dir = RotateVector(baseDir, angles[i]);
                RaycastHit2D hit = Physics2D.Raycast(startPos, dir, 12f, ~LayerMask.GetMask("Enemy"));

                startPositions[i] = startPos;
                if (hit.collider != null)
                {
                    endPositions[i] = hit.point;

                    IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (damageable != null && hit.collider.GetComponentInParent<BaseEnemy>() == null)
                    {
                        if (damagedTargets.Add(damageable))
                        {
                            damageable.TakeDamage(30f, 0); // 200% DMG = 30
                        }
                    }
                }
                else
                {
                    endPositions[i] = startPos + (Vector3)(dir * 12f);
                }
            }

            ShowMagicLasersClientRpc(startPositions, endPositions);
        }

        [ClientRpc]
        private void ShowMagicLasersClientRpc(Vector3[] startPositions, Vector3[] endPositions)
        {
            if (_laserRenderers == null) return;

            for (int i = 0; i < Mathf.Min(6, startPositions.Length, endPositions.Length, _laserRenderers.Length); i++)
            {
                LineRenderer lr = _laserRenderers[i];
                if (lr == null) continue;

                lr.DOKill();
                lr.enabled = true;

                Vector3 s = startPositions[i];
                Vector3 e = endPositions[i];
                s.z = -1f; // Đưa tia nổi lên trên
                e.z = -1f;

                lr.SetPosition(0, s);
                lr.SetPosition(1, e);

                Color sc = lr.startColor;
                Color ec = lr.endColor;
                sc.a = 1f;
                ec.a = 0f;
                lr.startColor = sc;
                lr.endColor = ec;

                lr.startWidth = 0.2f;
                lr.endWidth = 0.05f;

                // Tạo hiệu ứng mờ và nhỏ dần rồi biến mất
                DOTween.To(() => lr.startWidth, w => lr.startWidth = w, 0f, 0.4f).SetEase(Ease.OutQuad);
                DOTween.To(() => lr.endWidth, w => lr.endWidth = w, 0f, 0.4f).SetEase(Ease.OutQuad)
                       .OnComplete(() =>
                       {
                           if (lr != null)
                           {
                               lr.enabled = false;
                           }
                       });
            }
        }

        private Transform FindNearestMagicTarget()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(~LayerMask.GetMask("Enemy"));
            filter.useLayerMask = true;

            int count = Physics2D.OverlapCircle(transform.position, 12f, filter, _magicScanResults);

            Transform bestTarget = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (_magicScanResults[i] == null) continue;

                IDamageable damageable = _magicScanResults[i].GetComponentInParent<IDamageable>();
                if (damageable == null) continue;
                if (_magicScanResults[i].GetComponentInParent<BaseEnemy>() != null) continue;

                float dist = Vector3.Distance(transform.position, _magicScanResults[i].transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestTarget = _magicScanResults[i].transform;
                }
            }

            // Fallback về CoreTarget
            if (bestTarget == null && _coreTarget != null)
            {
                float coreDist = Vector3.Distance(transform.position, _coreTarget.position);
                if (coreDist <= 12f)
                {
                    bestTarget = _coreTarget;
                }
            }

            return bestTarget;
        }

        private Vector2 RotateVector(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
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

        public override void OnGetFromPool()
        {
            base.OnGetFromPool();
            if (_visual != null)
            {
                _visual.localScale = Vector3.one * _bossScale;
            }
        }

        public override void OnReturnToPool()
        {
            base.OnReturnToPool();
            if (_visual != null)
            {
                _visual.localScale = Vector3.one * _bossScale;
            }

            if (_laserRenderers != null)
            {
                foreach (LineRenderer lr in _laserRenderers)
                {
                    if (lr != null)
                    {
                        lr.DOKill();
                        lr.enabled = false;
                    }
                }
            }
        }

        [ClientRpc]
        private void StartHealVisualClientRpc()
        {
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
            if (_visual != null)
            {
                var sr = _visual.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(0.8f, 0.2f, 0.15f, 1f);
                }
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected(); // Vẽ tầm vả cận chiến 1.8f màu đỏ của Base

            // Vẽ thêm tầm bắn ma pháp 12f màu tím
            Gizmos.color = new Color(0.6f, 0.1f, 0.8f, 0.1f);
            Gizmos.DrawSphere(transform.position, 12f);
            Gizmos.color = new Color(0.6f, 0.1f, 0.8f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 12f);
        }
    }
}
