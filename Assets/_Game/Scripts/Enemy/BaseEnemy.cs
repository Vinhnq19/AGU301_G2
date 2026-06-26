using System;
using Assets._Game.Scripts.Data;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Enemy;
using DungeonBuilder.Enemy.States;
using DungeonBuilder.Networking.Pool;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace Assets._Game.Scripts.Enemy
{
    [RequireComponent(typeof(NetworkObject))]
    public class BaseEnemy : NetworkBehaviour, IDamageable, IPoolable
    {
        [SerializeField] private EnemyDataSO _data;
        [SerializeField] protected Transform _coreTarget;
        [SerializeField] protected Transform _visual;
        [SerializeField, Min(0.1f)] protected float _attackRange = 1.25f;
        [SerializeField, Min(0f)] protected float _attackDamage = 10f;
        [SerializeField, Min(0.1f)] protected float _attackInterval = 1f;
        [SerializeField] protected NetworkObject _projectilePrefab;
        [SerializeField] private NetworkObject _tokenDropPrefab;

        private readonly NetworkVariable<float> _currentHP = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private EventBus _eventBus;
        protected INetworkPool _pool;
        private CoreManager _coreManager;
        private EnemyStateMachine _stateMachine;
        private Collider2D[] _colliders;
        private Rigidbody2D[] _rigidbodies;
        protected bool _isDying;
        protected float _lastAttackTime;
        protected float _slowMultiplier = 1f;
        private Transform[] _currentPathWaypoints;
        private int _currentWaypointIndex;
        protected DungeonBuilder.Building.BaseTower _currentBlocker;

        private Tween _knockbackTween;
        private Tween _stunTween;
        private Tween _slowTween;
        private Vector3 _initialScale = Vector3.one;
        private bool _hasCachedScale;

        protected Animator _animator;
        private Vector3 _lastPosition;

        private void CacheInitialScaleIfNeeded()
        {
            if (!_hasCachedScale && _visual != null)
            {
                _initialScale = _visual.localScale;
                _hasCachedScale = true;
            }
        }

        public EnemyType EnemyType => _data != null ? _data.enemyType : EnemyType.Drone;
        public float MoveSpeed => (_data != null ? _data.moveSpeed : 2f) * _slowMultiplier;
        public Transform Visual => _visual;

        [Inject]
        public void Construct(EventBus eventBus, INetworkPool pool, CoreManager coreManager)
        {
            _eventBus = eventBus;
            _pool = pool;
            _coreManager = coreManager;
        }

        protected virtual void Awake()
        {
            _stateMachine = new EnemyStateMachine(this);
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _rigidbodies = GetComponentsInChildren<Rigidbody2D>(true);

            foreach (Rigidbody2D body in _rigidbodies)
            {
                if (body != null)
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.useFullKinematicContacts = true;
                }
            }

            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
            }

            _lastPosition = transform.position;
            if (_visual != null)
            {
                _animator = _visual.GetComponent<Animator>();
            }
        }

        public event Action<float, float> OnHealthChanged;

        public NetworkVariable<float> CurrentHPNetVar => _currentHP;

        public override void OnNetworkSpawn()
        {
            _currentHP.OnValueChanged += HandleHealthChanged;
            if (IsServer)
            {
                ResetEnemy();
            }
        }

        public override void OnNetworkDespawn()
        {
            _currentHP.OnValueChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float previousValue, float newValue)
        {
            OnHealthChanged?.Invoke(newValue, MaxHealth);
        }

        protected virtual void Update()
        {
            UpdateAnimator();

            if (!IsServer || _isDying)
            {
                return;
            }

            _stateMachine.Update();
        }

        private void UpdateAnimator()
        {
            if (_animator == null) return;

            float speed = 0f;
            if (Time.deltaTime > 0f)
            {
                float distanceMoved = Vector3.Distance(transform.position, _lastPosition);
                speed = distanceMoved / Time.deltaTime;
            }

            if (_isDying)
            {
                speed = 0f;
            }

            _animator.SetFloat("Speed", speed);

            if (speed > 0.05f && _visual != null)
            {
                CacheInitialScaleIfNeeded();
                float dx = transform.position.x - _lastPosition.x;
                if (dx < -0.001f)
                {
                    _visual.localScale = new Vector3(-Mathf.Abs(_initialScale.x), _initialScale.y, _initialScale.z);
                }
                else if (dx > 0.001f)
                {
                    _visual.localScale = new Vector3(Mathf.Abs(_initialScale.x), _initialScale.y, _initialScale.z);
                }
            }

            _lastPosition = transform.position;
        }

        public void TakeDamage(float amount, ulong attackerClientId = 0)
        {
            if (!IsServer || amount <= 0f || _isDying)
            {
                return;
            }

            _currentHP.Value = Mathf.Max(0f, _currentHP.Value - amount);
            ApplyKnockbackClientRpc(Vector3.up * 0.1f, 0.1f);
            PlayDamageFlashClientRpc();

            if (_currentHP.Value <= 0f)
            {
                DieAsync().Forget();
            }
        }

        public void Heal(float amount)
        {
            if (!IsServer || _isDying || amount <= 0f) return;
            float maxHealth = _data != null ? _data.maxHealth : 100f;
            _currentHP.Value = Mathf.Min(maxHealth, _currentHP.Value + amount);
        }

        public float CurrentHP => _currentHP.Value;
        public float MaxHealth => _data != null ? _data.maxHealth : 100f;

        public virtual void OnGetFromPool()
        {
            _isDying = false;
            _currentPathWaypoints = null;
            _currentWaypointIndex = 0;
            SetPhysicsActive(true);

            CacheInitialScaleIfNeeded();

            if (_visual != null)
            {
                _visual.DOKill();
                _visual.localPosition = Vector3.zero;
                _visual.localScale = _initialScale;
            }
            _lastPosition = transform.position;
        }

        public virtual void OnReturnToPool()
        {
            _isDying = false;
            _slowMultiplier = 1f;
            _stateMachine.ChangeState(null);
            SetPhysicsActive(false);
            _currentPathWaypoints = null;
            _currentWaypointIndex = 0;

            CacheInitialScaleIfNeeded();

            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = _initialScale;
            _lastPosition = transform.position;
        }

        public void ChangeState(IEnemyState nextState)
        {
            _stateMachine.ChangeState(nextState);
        }

        public void SetCoreTarget(Transform coreTarget)
        {
            _coreTarget = coreTarget;
        }

        public void SetPath(Transform[] waypoints)
        {
            _currentPathWaypoints = waypoints;
            _currentWaypointIndex = 0;
        }

        public virtual bool IsBlockedByWall()
        {
            Vector3 targetPos = GetCurrentTargetPosition();
            Vector3 dir = (targetPos - transform.position).normalized;

            RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, 0.6f, ~LayerMask.GetMask("Enemy", "Player"));
            if (hit.collider != null)
            {
                DungeonBuilder.Building.BaseTower tower = hit.collider.GetComponentInParent<DungeonBuilder.Building.BaseTower>();
                if (tower != null && tower.IsTargetable)
                {
                    _currentBlocker = tower;
                    return true;
                }
            }
            _currentBlocker = null;
            return false;
        }

        protected Vector3 GetCurrentTargetPosition()
        {
            if (_currentPathWaypoints != null && _currentWaypointIndex < _currentPathWaypoints.Length)
            {
                Transform waypoint = _currentPathWaypoints[_currentWaypointIndex];
                if (waypoint != null)
                {
                    return waypoint.position;
                }
            }
            return _coreTarget != null ? _coreTarget.position : transform.position;
        }

        public virtual bool IsCoreInAttackRange()
        {
            return _coreTarget != null && Vector3.Distance(transform.position, _coreTarget.position) <= _attackRange;
        }

        public virtual void MoveTowardsCore()
        {
            if (_currentPathWaypoints != null && _currentWaypointIndex < _currentPathWaypoints.Length)
            {
                Transform waypoint = _currentPathWaypoints[_currentWaypointIndex];
                if (waypoint != null)
                {
                    Vector3 targetPos = waypoint.position;
                    Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetPos, MoveSpeed * Time.deltaTime);
                    transform.position = nextPosition;

                    if (Vector3.Distance(transform.position, targetPos) < 0.15f)
                    {
                        _currentWaypointIndex++;
                    }
                    return;
                }
            }

            if (_coreTarget == null)
            {
                return;
            }

            Vector3 nextPositionDirect = Vector3.MoveTowards(transform.position, _coreTarget.position, MoveSpeed * Time.deltaTime);
            transform.position = nextPositionDirect;
        }

        public virtual void AttackCurrentBlocker()
        {
            if (_currentBlocker == null) return;

            if (Time.time - _lastAttackTime >= _attackInterval)
            {
                _lastAttackTime = Time.time;
                _currentBlocker.TakeDamage(_attackDamage, 0);
            }
        }

        public virtual void AttackCore()
        {
            if (Time.time - _lastAttackTime < _attackInterval)
            {
                return;
            }

            _lastAttackTime = Time.time;
            _coreManager?.TakeDamage(_attackDamage);
        }

        [ClientRpc]
        public void ApplyKnockbackClientRpc(Vector3 localOffset, float duration)
        {
            if (_visual == null)
            {
                return;
            }

            _knockbackTween?.Kill();
            _visual.localPosition = Vector3.zero;
            _knockbackTween = _visual.DOLocalMove(localOffset, duration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => _visual.localPosition = Vector3.zero);
        }

        [ClientRpc]
        public void PlayDeathEffectClientRpc()
        {
            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
        }

        [ClientRpc]
        public void PlayStunFeedbackClientRpc()
        {
            if (_visual == null)
            {
                return;
            }

            _stunTween?.Kill();
            _visual.localPosition = Vector3.zero;
            _stunTween = _visual.DOPunchPosition(Vector3.right * 0.08f, 0.25f, 8, 0.5f);
        }

        /// <summary>
        /// Áp dụng hiệu ứng chậm lên enemy trong khoảng thời gian nhất định. Server-only.
        /// </summary>
        public void ApplySlow(float slowMultiplier, float duration)
        {
            if (!IsServer || _isDying) return;
            _slowMultiplier = Mathf.Clamp(slowMultiplier, 0.1f, 1f);
            PlaySlowFeedbackClientRpc();
            SlowResetAsync(duration).Forget();
        }

        private async UniTaskVoid SlowResetAsync(float duration)
        {
            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(duration),
                    cancellationToken: destroyCancellationToken);
                _slowMultiplier = 1f;
            }
            catch (OperationCanceledException) { }
        }

        [ClientRpc]
        private void PlaySlowFeedbackClientRpc()
        {
            if (_visual == null) return;
            CacheInitialScaleIfNeeded();
            _slowTween?.Kill();
            _visual.localScale = _initialScale;
            _slowTween = _visual.DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f);
        }

        private void ResetEnemy()
        {
            float maxHealth = _data != null ? _data.maxHealth : 100f;
            _currentHP.Value = maxHealth;
            _isDying = false;
            _slowMultiplier = 1f;
            _lastAttackTime = -999f;
            _stateMachine.ChangeState(new MoveToCoreState());
        }

        private async UniTaskVoid DieAsync()
        {
            if (_isDying)
            {
                return;
            }

            _isDying = true;
            bool isBoss = _data != null && _data.isBoss;
            _eventBus?.RaiseEnemyKilled(EnemyType, isBoss);
            PlayDeathEffectClientRpc();

            if (IsServer && _tokenDropPrefab != null && _data != null && _data.rewardToken > 0)
            {
                SpawnTokenDrops(_data.rewardToken);
            }

            try
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(500), cancellationToken: destroyCancellationToken);
                _pool?.Return(NetworkObject);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void SpawnTokenDrops(int totalTokens)
        {
            if (_pool == null || _tokenDropPrefab == null)
            {
                return;
            }

            int maxDrops = 5;
            int dropCount = Mathf.Min(totalTokens, maxDrops);
            if (dropCount <= 0)
            {
                return;
            }

            int baseAmount = totalTokens / dropCount;
            int remainder = totalTokens % dropCount;

            for (int i = 0; i < dropCount; i++)
            {
                int amount = baseAmount + (i == dropCount - 1 ? remainder : 0);
                if (amount <= 0)
                {
                    continue;
                }

                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = UnityEngine.Random.Range(0.5f, 1.2f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;

                NetworkObject dropObj = _pool.Get(_tokenDropPrefab, transform.position, Quaternion.identity);
                if (dropObj != null)
                {
                    var drop = dropObj.GetComponent<DungeonBuilder.Harvesting.ResourceDrop>();
                    if (drop != null)
                    {
                        drop.ConfigureWithOffset(ResourceType.Token, amount, offset);
                    }

                    if (!dropObj.IsSpawned)
                    {
                        dropObj.Spawn();
                    }
                }
            }
        }

        [ClientRpc]
        public void PlayDamageFlashClientRpc()
        {
            if (_visual != null)
            {
                var sr = _visual.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.DOKill();
                    Color original = sr.color;
                    sr.color = new Color(1f, 0.4f, 0.4f, 1f);
                    sr.DOColor(original, 0.15f);
                }
            }
        }

        protected bool IsValidTarget(IDamageable damageable)
        {
            if (damageable == null) return false;

            // Quái không tự bắn đồng bọn
            if (damageable is BaseEnemy) return false;

            // Quái không bắn mỏ tài nguyên
            if (damageable is DungeonBuilder.Harvesting.HarvestableNode) return false;

            // Quái không bắn người chơi
            if (damageable is DungeonBuilder.Player.PlayerStats) return false;

            return true;
        }

        protected void ShootProjectileAt(Transform targetTransform, Color color, float sizeMultiplier = 1f)
        {
            if (_projectilePrefab == null || _pool == null || targetTransform == null) return;

            NetworkObject targetNetObj = targetTransform.GetComponentInParent<NetworkObject>();
            if (targetNetObj == null)
            {
                var tower = targetTransform.GetComponentInParent<DungeonBuilder.Building.BaseTower>();
                if (tower != null)
                {
                    targetNetObj = tower.NetworkObject;
                }
            }

            if (targetNetObj == null && targetTransform == _coreTarget && _coreManager != null)
            {
                targetNetObj = _coreManager.NetworkObject;
            }

            if (targetNetObj == null) return;

            NetworkObject bulletObj = _pool.Get(_projectilePrefab, transform.position, Quaternion.identity);
            if (bulletObj != null)
            {
                DungeonBuilder.Projectile.EnemyProjectile bullet = bulletObj.GetComponent<DungeonBuilder.Projectile.EnemyProjectile>();
                if (bullet != null)
                {
                    bullet.Initialize(_attackDamage, 8f, 5f, targetNetObj.NetworkObjectId, targetTransform.position, color, Vector3.one * sizeMultiplier);
                }

                if (!bulletObj.IsSpawned)
                {
                    bulletObj.Spawn();
                }
            }
        }

        private void SetPhysicsActive(bool active)
        {
            foreach (Collider2D col in _colliders)
            {
                if (col != null)
                {
                    col.enabled = active;
                }
            }

            foreach (Rigidbody2D body in _rigidbodies)
            {
                if (body == null)
                {
                    continue;
                }

                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = active;
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.15f, 0.15f, 0.08f); // Màu đỏ nhạt cho quái
            Gizmos.DrawSphere(transform.position, _attackRange);
            Gizmos.color = new Color(0.9f, 0.15f, 0.15f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, _attackRange);
        }
    }
}
