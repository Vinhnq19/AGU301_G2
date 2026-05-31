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
            if (IsServer)
            {
                _isActive = true;
                StartLifetimeAsync().Forget();
            }
        }

        private void Update()
        {
            if (!IsServer || !_isActive) return;

            // Cập nhật vị trí target nếu còn sống
            if (NetworkManager.Singleton?.SpawnManager?.SpawnedObjects != null
                && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_targetNetworkObjectId, out NetworkObject targetObj)
                && targetObj != null)
            {
                _lastKnownTargetPos = targetObj.transform.position;
            }

            // Di chuyển về phía target
            transform.position = Vector3.MoveTowards(transform.position, _lastKnownTargetPos, _speed * Time.deltaTime);

            // Kiểm tra hit
            if (Vector3.Distance(transform.position, _lastKnownTargetPos) < _hitRadius)
            {
                BaseEnemy enemy = NetworkManager.Singleton?.SpawnManager?.SpawnedObjects != null
                    && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_targetNetworkObjectId, out NetworkObject obj)
                    ? obj?.GetComponent<BaseEnemy>()
                    : null;
                OnHit(enemy);
            }
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

        private async UniTaskVoid StartLifetimeAsync()
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_lifetime), cancellationToken: destroyCancellationToken);
                if (_isActive) ReturnToPool();
            }
            catch (OperationCanceledException) { }
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
