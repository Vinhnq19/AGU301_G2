using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Networking.Pool;
using Unity.Netcode;
using UnityEngine;
using VContainer;
using Assets._Game.Scripts.Enemy;

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

        private Vector3 _direction;
        [SerializeField] private float _collisionRadius = 0.2f;

        [Inject] private INetworkPool _pool;

        public void Initialize(float damage, float speed, float lifetime, ulong targetNetworkObjectId, Vector3 targetInitialPosition, Color color, Vector3 localScale)
        {
            _damage = damage;
            _speed = speed;
            _lifetime = lifetime;
            _targetNetworkObjectId = targetNetworkObjectId;
            _lastKnownTargetPos = targetInitialPosition;
            _isActive = true;

            _direction = (targetInitialPosition - transform.position).normalized;
            if (_direction == Vector3.zero)
            {
                _direction = Vector3.right;
            }

            transform.rotation = Quaternion.FromToRotation(Vector3.up, _direction);

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

            float step = _speed * Time.deltaTime;
            transform.position += _direction * step;

            if (IsServer)
            {
                int layerMask = ~LayerMask.GetMask("Enemy");
                ContactFilter2D filter = new ContactFilter2D();
                filter.SetLayerMask(layerMask);
                filter.useLayerMask = true;
                filter.useTriggers = true;
                Collider2D[] results = new Collider2D[4];
                int count = Physics2D.OverlapCircle(transform.position, _collisionRadius, filter, results);
                for (int i = 0; i < count; i++)
                {
                    if (results[i] == null) continue;
                    IDamageable damageable = results[i].GetComponentInParent<IDamageable>();
                    if (damageable == null)
                    {
                        // DB_Core holds BoxCollider2D but CoreManager is on GameRoot, so we find it dynamically
                        if (results[i].gameObject.name == "DB_Core" || results[i].gameObject.name.Contains("Core"))
                        {
                            damageable = FindAnyObjectByType<DungeonBuilder.Core.CoreManager>();
                        }
                    }

                    if (damageable != null && results[i].GetComponentInParent<BaseEnemy>() == null)
                    {
                        // Bỏ qua các tháp không thể target (vd: bẫy gai)
                        var tower = damageable as DungeonBuilder.Building.BaseTower;
                        if (tower != null && !tower.IsTargetable) continue;

                        OnHit(damageable);
                        return;
                    }
                }
            }
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
            transform.rotation = Quaternion.identity;
            if (_visual == null) return;
            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one;
        }

        public void OnReturnToPool()
        {
            _isActive = false;
            transform.rotation = Quaternion.identity;
            if (_visual == null) return;
            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one;
        }
    }
}
