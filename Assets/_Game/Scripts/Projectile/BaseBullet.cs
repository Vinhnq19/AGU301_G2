using System;
using Assets._Game.Scripts.Enemy;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Networking.Pool;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Projectile
{
    /// <summary>
    /// Đạn cơ sở. Server di chuyển và phát hiện hit; client nhận visual effect qua ClientRpc.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class BaseBullet : NetworkBehaviour, IPoolable
    {
        [SerializeField] private Transform _visual;
        [SerializeField, Min(0.01f)] private float _hitRadius = 0.2f;

        private float _speed;
        private float _lifetime;
        private ulong _targetNetworkObjectId;
        private Vector3 _lastKnownTargetPos;
        private bool _isActive;
        private float _lifetimeTimer;

        protected float Damage { get; private set; }

        [Inject] private INetworkPool _pool;

        /// <summary>
        /// Gọi bởi BaseTower sau pool.Get() và TRƯỚC bullet.Spawn().
        /// </summary>
        public void Initialize(float damage, float speed, float lifetime, ulong targetNetworkObjectId, Vector3 spawnPosition)
        {
            Damage = damage;
            _speed = speed;
            _lifetime = lifetime;
            _targetNetworkObjectId = targetNetworkObjectId;
            _lastKnownTargetPos = spawnPosition;
            _isActive = false;
        }

        public override void OnNetworkSpawn()
        {
            _isActive = true;
            if (IsServer)
            {
                _lifetimeTimer = _lifetime;
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            if (IsServer)
            {
                _lifetimeTimer -= Time.deltaTime;
                if (_lifetimeTimer <= 0f)
                {
                    ReturnToPool();
                    return;
                }
            }

            // Cập nhật vị trí target nếu còn sống (cho cả Server và Client)
            if (NetworkManager.Singleton?.SpawnManager?.SpawnedObjects != null
                && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_targetNetworkObjectId, out NetworkObject targetObj)
                && targetObj != null)
            {
                _lastKnownTargetPos = targetObj.transform.position;
            }

            // Tính toán khoảng cách và bước di chuyển (step)
            float distanceToTarget = Vector3.Distance(transform.position, _lastKnownTargetPos);
            float step = _speed * Time.deltaTime;

            // Nếu khoảng cách đến mục tiêu nhỏ hơn hoặc bằng bước di chuyển trong frame này
            // -> Chắc chắn đạn đã chạm mục tiêu
            if (distanceToTarget <= step)
            {
                transform.position = _lastKnownTargetPos;

                // Chỉ Server chịu trách nhiệm phân xử sát thương
                if (IsServer)
                {
                    BaseEnemy enemy = NetworkManager.Singleton?.SpawnManager?.SpawnedObjects != null
                        && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_targetNetworkObjectId, out NetworkObject obj)
                        ? obj?.GetComponent<BaseEnemy>()
                        : null;
                    
                    OnHit(enemy);
                }
                return;
            }

            // Di chuyển về phía target (Client tự mô phỏng nội bộ)
            transform.position = Vector3.MoveTowards(transform.position, _lastKnownTargetPos, step);
        }

        /// <summary>
        /// Xử lý khi đạn chạm mục tiêu. Override trong FrostBullet / CannonBullet.
        /// </summary>
        protected virtual void OnHit(BaseEnemy target)
        {
            if (!IsServer) return;
            target?.TakeDamage(Damage, 0);
            PlayHitEffectClientRpc();
            ReturnToPool();
        }

        protected void ReturnToPool()
        {
            if (!_isActive) return;
            _isActive = false;
            _pool?.Return(NetworkObject);
        }

        [ClientRpc]
        public void PlayHitEffectClientRpc()
        {
            if (_visual == null) return;
            _visual.DOKill();
            _visual.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                   .OnComplete(() =>
                   {
                       if (_visual != null) _visual.localScale = Vector3.one;
                   });
        }

        public void OnGetFromPool()
        {
            _isActive = false;
            if (_visual == null) return;
            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one;
        }

        public void OnReturnToPool()
        {
            _isActive = false;
            if (_visual == null) return;
            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one;
        }
    }
}
