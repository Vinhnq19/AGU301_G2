using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Building;
using Assets._Game.Scripts.Data;
using Assets._Game.Scripts.Enemy;
using Cysharp.Threading.Tasks;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Networking;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.Projectile;
using DungeonBuilder.Core.Interfaces;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Building
{
    /// <summary>
    /// Script chinh cua moi tower. Quan ly vong doi: Place → Contribute → Active → Upgrade → Remove.
    /// NetworkList theo doi tien do xay dung tu build cost cua tower.
    /// Attack loop chi bat dau sau khi IsConstructed == true.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class BaseTower : NetworkBehaviour, IDamageable
    {
        [Header("Tower Config")]
        [SerializeField] protected TowerDataSO _data;
        [SerializeField] private NetworkObject _bulletPrefab;
        [SerializeField] protected Transform _firePoint;


        private readonly NetworkVariable<int> _currentLevel = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _health = new(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly Collider2D[] _overlapResults = new Collider2D[16];
        private ContactFilter2D _enemyFilter;

        protected TowerModel _model;
        private TowerPresenter _presenter;
        private TowerView _view;

        [Inject] protected INetworkPool _pool;

        public int CurrentLevel => _currentLevel.Value;
        public bool CanUpgrade  => _data != null && _currentLevel.Value < _data.maxLevel;

        protected virtual void Awake()
        {
            _enemyFilter = new ContactFilter2D();
            _enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
            _enemyFilter.useTriggers = false;

            _presenter = GetComponent<TowerPresenter>();
            _view      = GetComponentInChildren<TowerView>();
        }

        public override void OnNetworkSpawn()
        {
            _model = new TowerModel(_data);

            _currentLevel.OnValueChanged += HandleLevelChanged;
            _health.OnValueChanged += HandleHealthChanged;

            if (_presenter != null)
                _presenter.Initialize(_model, _view);

            _model.SetLevel(_currentLevel.Value);
            
            if (IsServer && _health.Value == 100f && _currentLevel.Value == 1) // Just initialized
            {
                _health.Value = _model.MaxHealth;
            }
            _model.SetHealth(_health.Value);

            DBLog.Info(
                $"tower.spawn.{NetworkObjectId}",
                $"[BaseTower] Spawned. data={(_data != null ? _data.name : "NULL")}, " +
                $"presenter={(_presenter != null ? "OK" : "NULL")}, view={(_view != null ? "OK" : "NULL")}",
                0f, this);

            if (IsServer)
                StartAttackLoopAsync().Forget();
        }

        public override void OnNetworkDespawn()
        {
            _currentLevel.OnValueChanged -= HandleLevelChanged;
            _health.OnValueChanged -= HandleHealthChanged;
        }

        /// <summary>Goi boi BuildingController sau tower.Spawn().</summary>
        public void OnPlaced(Vector2Int gridPosition)
        {
            DBLog.Info($"tower.placed.{NetworkObjectId}", $"[BaseTower] Tower placed at grid={gridPosition}.", 0f, this);
        }


        public void UpgradeLevel()
        {
            if (!IsServer || !CanUpgrade) return;
            _currentLevel.Value++;
            _model.SetLevel(_currentLevel.Value); // Force model update before getting MaxHealth
            _health.Value = _model.MaxHealth; // Heal to full on upgrade
            DBLog.Info($"tower.upgrade.{NetworkObjectId}", $"[BaseTower] Upgraded to level {_currentLevel.Value}.", 0f, this);
        }

        public void TakeDamage(float amount, ulong attackerClientId = 0)
        {
            if (!IsServer) return;
            
            _health.Value -= amount;
            DBLog.Info($"tower.damage.{NetworkObjectId}", $"[BaseTower] Took {amount} damage. HP: {_health.Value}/{_model.MaxHealth}.", 0.2f, this);

            if (_health.Value <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (!IsServer) return;
            DBLog.Info($"tower.die.{NetworkObjectId}", $"[BaseTower] Destroyed.", 0f, this);
            if (_pool != null)
            {
                _pool.Return(NetworkObject);
            }
            else
            {
                NetworkObject.Despawn();
            }
        }

        private async UniTaskVoid StartAttackLoopAsync()
        {
            try
            {
                while (IsServer && IsSpawned)
                {
                    float interval = _data != null && _data.attackRate > 0f ? 1f / _data.attackRate : 1f;
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: destroyCancellationToken);
                    if (!IsServer || !IsSpawned) return;

                    BaseEnemy target = FindClosestEnemy();
                    if (target != null) FireAt(target);
                }
            }
            catch (OperationCanceledException) { }
        }

        private BaseEnemy FindClosestEnemy()
        {
            if (_data == null || _model == null) return null;
            float range = _model.Range;
            int count = Physics2D.OverlapCircle(transform.position, range, _enemyFilter, _overlapResults);
            BaseEnemy closest = null;
            float closestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (_overlapResults[i] == null) continue;
                BaseEnemy enemy = _overlapResults[i].GetComponentInParent<BaseEnemy>();
                if (enemy == null || !enemy.IsSpawned) continue;
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < closestDist) { closestDist = dist; closest = enemy; }
            }
            return closest;
        }

        protected virtual void FireAt(BaseEnemy target)
        {
            if (_bulletPrefab == null || _pool == null || target == null) return;
            Vector3 firePos   = _firePoint != null ? _firePoint.position : transform.position;
            Vector3 direction = (target.transform.position - firePos).normalized;
            Quaternion rot    = direction != Vector3.zero ? Quaternion.FromToRotation(Vector3.up, direction) : Quaternion.identity;

            NetworkObject bulletObj = _pool.Get(_bulletPrefab, firePos, rot);
            if (bulletObj == null) return;

            BaseBullet bullet = bulletObj.GetComponent<BaseBullet>();
            if (bullet == null) { _pool.Return(bulletObj); return; }

            float damage = _model != null ? _model.Damage : (_data != null ? _data.damage : 10f);
            bullet.Initialize(damage, _data?.bulletSpeed ?? 8f, _data?.bulletLifetime ?? 3f, target.NetworkObjectId, firePos);

            if (!bulletObj.IsSpawned) bulletObj.Spawn();

            DBLog.Info($"tower.fire.{NetworkObjectId}", $"[BaseTower] Fired. target={target.NetworkObjectId}, dmg={damage:0.0}.", 0.1f, this);
        }


        private void HandleLevelChanged(int _, int newVal) => _model?.SetLevel(newVal);
        private void HandleHealthChanged(float _, float newVal) => _model?.SetHealth(newVal);

        private void OnDrawGizmosSelected()
        {
            float range = _model != null ? _model.Range : (_data != null ? _data.range : 4f);
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.1f);
            Gizmos.DrawSphere(transform.position, range);
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, range);
            Vector3 fp = _firePoint != null ? _firePoint.position : transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(fp, 0.1f);
        }
    }
}
