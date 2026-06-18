using System;
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
    /// Đạn của quái vật. Tấn công người chơi, tháp canh hoặc lõi năng lượng (IDamageable).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class EnemyProjectile : NetworkBehaviour, IPoolable
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private float _speed;
        private float _lifetime;
        private ulong _targetNetworkObjectId;
        private Vector3 _lastKnownTargetPos;
        private bool _isActive;
        private float _lifetimeTimer;
        private float _damage;

        [Inject] private INetworkPool _pool;

        public void Initialize(float damage, float speed, float lifetime, ulong targetNetworkObjectId, Vector3 spawnPosition, Color color, Vector3 localScale)
        {
            _damage = damage;
            _speed = speed;
            _lifetime = lifetime;
            _targetNetworkObjectId = targetNetworkObjectId;
            _lastKnownTargetPos = spawnPosition;
            _isActive = true;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = color;
            }
            if (_visual != null)
            {
                _visual.localScale = localScale;
            }
        }

        public override void OnNetworkSpawn()
        {
            _isActive = IsServer;
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

            // Tìm và cập nhật vị trí target
            if (NetworkManager.Singleton?.SpawnManager?.SpawnedObjects != null
                && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_targetNetworkObjectId, out NetworkObject targetObj)
                && targetObj != null)
            {
                if (targetObj.TryGetComponent<DungeonBuilder.Core.CoreManager>(out _))
                {
                    GameObject visualCore = GameObject.Find("DB_Core");
                    _lastKnownTargetPos = visualCore != null ? visualCore.transform.position : targetObj.transform.position;
                }
                else
                {
                    _lastKnownTargetPos = targetObj.transform.position;
                }
            }

            float distanceToTarget = Vector3.Distance(transform.position, _lastKnownTargetPos);
            float step = _speed * Time.deltaTime;

            if (distanceToTarget <= step)
            {
                transform.position = _lastKnownTargetPos;

                if (IsServer)
                {
                    IDamageable damageable = null;
                    if (NetworkManager.Singleton?.SpawnManager?.SpawnedObjects != null
                        && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_targetNetworkObjectId, out NetworkObject obj)
                        && obj != null)
                    {
                        damageable = obj.GetComponent<IDamageable>();
                    }

                    OnHit(damageable);
                }
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, _lastKnownTargetPos, step);
        }

        protected virtual void OnHit(IDamageable target)
        {
            if (!IsServer) return;
            target?.TakeDamage(_damage, 0);
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
