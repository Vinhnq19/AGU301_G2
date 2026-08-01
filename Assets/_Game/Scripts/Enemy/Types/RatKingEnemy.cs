using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Enemy;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Enemy;
using DungeonBuilder.Wave;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Enemy.Types
{
    /// <summary>
    /// Rat King (Boss): Trùm chương 1.
    /// Kỹ năng:
    /// - Melee: Vả cận chiến.
    /// - Magic Lasers: Bắn 6 tia ma pháp tức thời (LineRenderer).
    /// - Self-Healing: Gục hồi máu khi HP thấp.
    /// - Summon: Triệu hồi Runner tuần tự tại các cổng.
    /// </summary>
    public sealed class RatKingEnemy : BaseEnemy
    {
        [Header("Boss Prefabs")]
        [SerializeField] private NetworkObject _runnerPrefab;

        [Header("Boss Balancing Configs")]
        [SerializeField, Min(0.1f)] private float _magicCooldown = 5f;
        [SerializeField, Min(0.1f)] private float _magicRange = 12f;
        [SerializeField, Min(0f)] private float _magicDamage = 30f;
        
        [SerializeField, Min(0.1f)] private float _summonInterval = 30f;
        [SerializeField, Min(1)] private int _summonCount = 10;
        [SerializeField, Min(0.01f)] private float _summonDelayBetweenRunners = 0.35f;

        [SerializeField, Range(0.1f, 0.9f)] private float _selfHealThreshold = 0.45f;
        [SerializeField, Min(0.1f)] private float _selfHealDuration = 5f;
        [SerializeField, Min(0.1f)] private float _selfHealCooldown = 10f;
        [SerializeField, Min(0.1f)] private float _selfHealRate = 2f;
        [Tooltip("Tổng damage nhận trong lúc đang hồi máu để CẮT MẠCH hồi. Biến pha hồi máu thành " +
                 "bài kiểm tra sát thương thay vì chỉ đứng đợi hết timer. Để 0 = không cắt được.")]
        [SerializeField, Min(0f)] private float _healInterruptDamage = 60f;

        [Header("Erratic Movement — di chuyển lộn xộn")]
        [Tooltip("Bán kính đảo hướng tối đa quanh đường đi (unit). Càng lớn boss càng lượn rộng. 0 = tắt.")]
        [SerializeField, Min(0f)] private float _wanderRadius = 2.5f;
        [Tooltip("Khoảng thời gian NGẮN NHẤT giữa 2 lần đổi hướng ngẫu nhiên (giây).")]
        [SerializeField, Min(0.05f)] private float _wanderIntervalMin = 0.35f;
        [Tooltip("Khoảng thời gian DÀI NHẤT giữa 2 lần đổi hướng ngẫu nhiên (giây).")]
        [SerializeField, Min(0.05f)] private float _wanderIntervalMax = 1.1f;
        [Tooltip("Độ mượt khi bẻ lái (0.05 = lượn mềm, 1 = giật cục đổi hướng tức thì).")]
        [SerializeField, Range(0.02f, 1f)] private float _wanderSmoothing = 0.12f;
        [Tooltip("Xác suất lao vọt (lunge) mỗi lần đổi hướng.")]
        [SerializeField, Range(0f, 1f)] private float _lungeChance = 0.3f;
        [Tooltip("Hệ số tăng tốc khi lao vọt.")]
        [SerializeField, Range(1f, 4f)] private float _lungeSpeedMultiplier = 2.2f;
        [Tooltip("Thời gian mỗi cú lao vọt (giây).")]
        [SerializeField, Min(0.05f)] private float _lungeDuration = 0.45f;

        private const float _bossScale = 2.2f;

        private float _magicCooldownTimer = 5f;
        private float _summonCooldownTimer = 30f;
        private float _healCooldownTimer = 0f;

        private bool _isHealing = false;
        private float _healDurationTimer = 0f;
        private float _healTickTimer = 0f;
        private float _damageDuringHeal = 0f;

        // --- Erratic movement ---
        private Vector3 _wanderOffset;        // độ lệch đang áp dụng (đã làm mượt)
        private Vector3 _wanderTargetOffset;  // độ lệch mục tiêu, đổi ngẫu nhiên theo nhịp
        private float _nextWanderTime;
        private float _lungeEndTime;

        private Transform[] _gates;
        private LineRenderer[] _laserRenderers;
        private readonly Collider2D[] _magicScanResults = new Collider2D[16];
        private WaveManager _waveManager;

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
                lr.sortingLayerName = "Tower";
                lr.sortingOrder = 220;

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
            _waveManager = FindFirstObjectByType<WaveManager>();
        }

        /// <summary>
        /// Dồn damage trong lúc boss hồi máu; đủ ngưỡng thì CẮT MẠCH hồi ngay.
        /// Người chơi bơm đủ sát thương sẽ phá được pha hồi máu thay vì đứng đợi.
        /// </summary>
        protected override void OnDamaged(float amount)
        {
            if (!_isHealing || _healInterruptDamage <= 0f)
            {
                return;
            }

            _damageDuringHeal += amount;
            if (_damageDuringHeal >= _healInterruptDamage)
            {
                EndHealing();
            }
        }

        /// <summary>
        /// Tốc độ boss: cộng thêm cú "lao vọt" bất chợt để nhịp di chuyển không đều,
        /// người chơi khó đoán được boss sẽ tới đâu.
        /// </summary>
        public override float MoveSpeed =>
            base.MoveSpeed * (Time.time < _lungeEndTime ? _lungeSpeedMultiplier : 1f);

        /// <summary>
        /// Bẻ lái điểm đến bằng độ lệch ngẫu nhiên → boss đi lượn/zigzag thay vì đi thẳng.
        /// Độ lệch được thu nhỏ dần khi đã tới sát mục tiêu, nếu không boss cứ lượn vòng
        /// quanh mà không bao giờ vào tầm đánh.
        /// </summary>
        protected override Vector3 ModifyDestination(Vector3 destination)
        {
            if (_wanderRadius <= 0f)
            {
                return destination;
            }

            float distance = Vector3.Distance(transform.position, destination);
            float closeRange = Mathf.Max(0.01f, _attackRange * 2f);
            float scale = Mathf.Clamp01(distance / closeRange);

            return destination + _wanderOffset * scale;
        }

        /// <summary>Định kỳ chọn hướng lệch mới (và thỉnh thoảng lao vọt), rồi nội suy cho mượt.</summary>
        private void UpdateErraticMovement()
        {
            if (_wanderRadius <= 0f)
            {
                return;
            }

            if (Time.time >= _nextWanderTime)
            {
                float min = Mathf.Min(_wanderIntervalMin, _wanderIntervalMax);
                float max = Mathf.Max(_wanderIntervalMin, _wanderIntervalMax);
                _nextWanderTime = Time.time + UnityEngine.Random.Range(min, max);

                Vector2 random = UnityEngine.Random.insideUnitCircle * _wanderRadius;
                _wanderTargetOffset = new Vector3(random.x, random.y, 0f);

                if (UnityEngine.Random.value < _lungeChance)
                {
                    _lungeEndTime = Time.time + _lungeDuration;
                }
            }

            _wanderOffset = Vector3.Lerp(_wanderOffset, _wanderTargetOffset, _wanderSmoothing);
        }

        private void ResetErraticMovement()
        {
            _wanderOffset = Vector3.zero;
            _wanderTargetOffset = Vector3.zero;
            _nextWanderTime = 0f;
            _lungeEndTime = 0f;
        }

        /// <summary>Kết thúc pha hồi máu (hết thời gian hoặc bị cắt mạch).</summary>
        private void EndHealing()
        {
            _isHealing = false;
            _damageDuringHeal = 0f;
            _slowMultiplier = 1f;                    // Phục hồi di chuyển
            _healCooldownTimer = _selfHealCooldown;  // Reset cooldown hồi máu
            StopHealVisualClientRpc();
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
                    Heal(_selfHealRate);
                }

                if (_healDurationTimer <= 0f)
                {
                    EndHealing();
                }
                return; // Đang hồi phục sẽ không di chuyển/tấn công
            }

            // Đảo hướng lộn xộn (chỉ khi đang thực sự di chuyển — lúc hồi máu đã return ở trên).
            UpdateErraticMovement();

            // Giảm cooldown
            if (_healCooldownTimer > 0f) _healCooldownTimer -= Time.deltaTime;
            if (_magicCooldownTimer > 0f) _magicCooldownTimer -= Time.deltaTime;
            _summonCooldownTimer -= Time.deltaTime;

            // Kiểm tra HP để kích hoạt hồi máu
            if (CurrentHP < MaxHealth * _selfHealThreshold && _healCooldownTimer <= 0f)
            {
                _isHealing = true;
                _healDurationTimer = _selfHealDuration;
                _healTickTimer = 0f;
                _damageDuringHeal = 0f;
                _slowMultiplier = 0f; // Đứng yên
                StartHealVisualClientRpc();
                return;
            }

            // Triệu hồi Runner tuần tự
            if (_summonCooldownTimer <= 0f)
            {
                _summonCooldownTimer = _summonInterval;
                SummonRunnersAsync().Forget();
            }

            // Bắn 6 tia ma pháp (Magic Lasers)
            if (_magicCooldownTimer <= 0f)
            {
                Transform target = FindNearestMagicTarget();
                if (target != null)
                {
                    _magicCooldownTimer = _magicCooldown;
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
                RaycastHit2D hit = Physics2D.Raycast(startPos, dir, _magicRange, ~LayerMask.GetMask("Enemy"));

                startPositions[i] = startPos;
                if (hit.collider != null)
                {
                    endPositions[i] = hit.point;

                    IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                    if (IsValidTarget(damageable))
                    {
                        if (damagedTargets.Add(damageable))
                        {
                            damageable.TakeDamage(_magicDamage, 0);
                        }
                    }
                }
                else
                {
                    endPositions[i] = startPos + (Vector3)(dir * _magicRange);
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

            int count = Physics2D.OverlapCircle(transform.position, _magicRange, filter, _magicScanResults);

            Transform bestTower = null;
            float closestTowerDist = float.MaxValue;

            Transform bestHeroOrCore = null;
            float closestHeroOrCoreDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (_magicScanResults[i] == null) continue;

                IDamageable damageable = _magicScanResults[i].GetComponentInParent<IDamageable>();
                if (!IsValidTarget(damageable)) continue;

                float dist = Vector3.Distance(transform.position, _magicScanResults[i].transform.position);

                // Phân loại: Tháp canh (Tower) vs Anh hùng/Lõi (Hero/Core)
                var tower = damageable as DungeonBuilder.Building.BaseTower;
                if (tower != null)
                {
                    if (!tower.IsTargetable) continue; // Bỏ qua bẫy gai

                    if (dist < closestTowerDist)
                    {
                        closestTowerDist = dist;
                        bestTower = _magicScanResults[i].transform;
                    }
                }
                else
                {
                    if (dist < closestHeroOrCoreDist)
                    {
                        closestHeroOrCoreDist = dist;
                        bestHeroOrCore = _magicScanResults[i].transform;
                    }
                }
            }

            // Fallback về CoreTarget
            if (_coreTarget != null)
            {
                float coreDist = Vector3.Distance(transform.position, _coreTarget.position);
                if (coreDist <= _magicRange)
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

        private Vector2 RotateVector(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
        }

        private async UniTaskVoid SummonRunnersAsync()
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

            // Tìm path tương ứng với cổng
            EnemyPath matchedPath = null;
            if (_waveManager != null && _waveManager.EnemyPaths != null)
            {
                string suffix = randomGate.name.Replace("Spawn_", "");
                foreach (var path in _waveManager.EnemyPaths)
                {
                    if (path != null && path.name.Contains(suffix))
                    {
                        matchedPath = path;
                        break;
                    }
                }
            }

            for (int i = 0; i < _summonCount; i++)
            {
                if (_isDying || !IsServer) return;

                Vector3 spawnPos = randomGate.position;
                NetworkObject runnerObj = _pool.Get(_runnerPrefab, spawnPos, Quaternion.identity);
                if (runnerObj != null)
                {
                    BaseEnemy enemy = runnerObj.GetComponent<BaseEnemy>();
                    if (enemy != null)
                    {
                        enemy.SetCoreTarget(_coreTarget);
                        if (matchedPath != null && matchedPath.Waypoints != null)
                        {
                            enemy.SetPath(matchedPath.Waypoints);
                        }
                    }

                    if (!runnerObj.IsSpawned)
                    {
                        runnerObj.Spawn();
                    }
                }

                // Chờ khoảng thời gian giữa mỗi con chui ra
                await UniTask.Delay(
                    TimeSpan.FromSeconds(_summonDelayBetweenRunners),
                    cancellationToken: destroyCancellationToken);
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
            ResetErraticMovement();
            if (_visual != null)
            {
                _visual.localScale = Vector3.one * _bossScale;
            }
        }

        public override void OnReturnToPool()
        {
            base.OnReturnToPool();
            ResetErraticMovement();
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

            // Vẽ thêm tầm bắn ma pháp
            Gizmos.color = new Color(0.6f, 0.1f, 0.8f, 0.1f);
            Gizmos.DrawSphere(transform.position, _magicRange);
            Gizmos.color = new Color(0.6f, 0.1f, 0.8f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _magicRange);
        }
    }
}
