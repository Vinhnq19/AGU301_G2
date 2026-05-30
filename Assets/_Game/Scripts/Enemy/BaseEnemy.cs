using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Data;
using DungeonBuilder.Enemy.States;
using DungeonBuilder.Networking.Pool;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Enemy
{
    [RequireComponent(typeof(NetworkObject))]
    public class BaseEnemy : NetworkBehaviour, IDamageable, IPoolable
    {
        [SerializeField] private EnemyDataSO _data;
        [SerializeField] private Transform _coreTarget;
        [SerializeField] private Transform _visual;
        [SerializeField, Min(0.1f)] private float _attackRange = 1.25f;
        [SerializeField, Min(0f)] private float _attackDamage = 10f;
        [SerializeField, Min(0.1f)] private float _attackInterval = 1f;

        private readonly NetworkVariable<float> _currentHP = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private EventBus _eventBus;
        private INetworkPool _pool;
        private CoreManager _coreManager;
        private EnemyStateMachine _stateMachine;
        private Collider2D[] _colliders;
        private Rigidbody2D[] _rigidbodies;
        private bool _isDying;
        private float _lastAttackTime;

        public EnemyType EnemyType => _data != null ? _data.enemyType : DungeonBuilder.Core.Enums.EnemyType.Drone;
        public float MoveSpeed => _data != null ? _data.moveSpeed : 2f;
        public Transform Visual => _visual;

        [Inject]
        public void Construct(EventBus eventBus, INetworkPool pool, CoreManager coreManager)
        {
            _eventBus = eventBus;
            _pool = pool;
            _coreManager = coreManager;
        }

        private void Awake()
        {
            _stateMachine = new EnemyStateMachine(this);
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _rigidbodies = GetComponentsInChildren<Rigidbody2D>(true);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ResetEnemy();
            }
        }

        private void Update()
        {
            if (!IsServer || _isDying)
            {
                return;
            }

            _stateMachine.Update();
        }

        public void TakeDamage(float amount, ulong attackerClientId = 0)
        {
            if (!IsServer || amount <= 0f || _isDying)
            {
                return;
            }

            _currentHP.Value = Mathf.Max(0f, _currentHP.Value - amount);
            ApplyKnockbackClientRpc(Vector3.up * 0.1f, 0.1f);

            if (_currentHP.Value <= 0f)
            {
                DieAsync().Forget();
            }
        }

        public void OnGetFromPool()
        {
            _isDying = false;
            SetPhysicsActive(true);

            if (_visual != null)
            {
                _visual.DOKill();
                _visual.localPosition = Vector3.zero;
                _visual.localScale = Vector3.one;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                ResetEnemy();
            }
        }

        public void OnReturnToPool()
        {
            _isDying = false;
            _stateMachine.ChangeState(null);
            SetPhysicsActive(false);

            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one;
        }

        public void ChangeState(IEnemyState nextState)
        {
            _stateMachine.ChangeState(nextState);
        }

        public void SetCoreTarget(Transform coreTarget)
        {
            _coreTarget = coreTarget;
        }

        public virtual bool IsBlockedByWall()
        {
            return false;
        }

        public bool IsCoreInAttackRange()
        {
            return _coreTarget != null && Vector3.Distance(transform.position, _coreTarget.position) <= _attackRange;
        }

        public virtual void MoveTowardsCore()
        {
            if (_coreTarget == null)
            {
                return;
            }

            Vector3 nextPosition = Vector3.MoveTowards(transform.position, _coreTarget.position, MoveSpeed * Time.deltaTime);
            transform.position = nextPosition;
        }

        public virtual void AttackCurrentBlocker()
        {
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

            _visual.DOKill();
            _visual.DOLocalMove(_visual.localPosition + localOffset, duration)
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

            _visual.DOKill();
            _visual.DOPunchPosition(Vector3.right * 0.08f, 0.25f, 8, 0.5f);
        }

        private void ResetEnemy()
        {
            float maxHealth = _data != null ? _data.maxHealth : 100f;
            _currentHP.Value = maxHealth;
            _isDying = false;
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
            _eventBus?.RaiseEnemyKilled(EnemyType);
            PlayDeathEffectClientRpc();

            try
            {
                await UniTask.Delay(TimeSpan.FromMilliseconds(500), cancellationToken: destroyCancellationToken);
                _pool?.Return(NetworkObject);
            }
            catch (OperationCanceledException)
            {
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
    }
}
