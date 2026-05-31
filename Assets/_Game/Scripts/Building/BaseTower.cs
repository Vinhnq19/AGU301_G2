using System;
using Assets._Game.Scripts.Building;
using Assets._Game.Scripts.Data;
using Assets._Game.Scripts.Enemy;
using Cysharp.Threading.Tasks;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.Projectile;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Building
{
    /// <summary>
    /// Script chinh cua moi tower. Quan ly vong doi: Place, Attack, Upgrade, Remove.
    /// Attack loop chi chay tren Server. NetworkVariable replicate state sang tat ca client.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class BaseTower : NetworkBehaviour
    {
        [Header("Tower Config")]
        [SerializeField] protected TowerDataSO _data;
        [SerializeField] private NetworkObject _bulletPrefab;
        [SerializeField] private Transform _firePoint;

        private readonly NetworkVariable<int> _currentLevel = new(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Non-alloc enemy detection buffer — class level
        private readonly Collider2D[] _overlapResults = new Collider2D[16];
        private ContactFilter2D _enemyFilter;

        private TowerModel _model;
        private TowerPresenter _presenter;
        private TowerView _view;

        [Inject] private INetworkPool _pool;

        public int CurrentLevel => _currentLevel.Value;
        public bool CanUpgrade => _data != null && _currentLevel.Value < _data.maxLevel;

        private void Awake()
        {
            _enemyFilter = new ContactFilter2D();
            _enemyFilter.SetLayerMask(LayerMask.GetMask("Enemy"));
            _enemyFilter.useTriggers = false;

            _presenter = GetComponent<TowerPresenter>();
            _view = GetComponentInChildren<TowerView>();
        }

        public override void OnNetworkSpawn()
        {
            _model = new TowerModel(_data);
            _currentLevel.OnValueChanged += HandleLevelChanged;

            if (_presenter != null)
            {
                _presenter.Initialize(_model, _view);
            }

            _model.SetLevel(_currentLevel.Value);

            if (IsServer)
            {
                StartAttackLoopAsync().Forget();
            }
        }

        public override void OnNetworkDespawn()
        {
            _currentLevel.OnValueChanged -= HandleLevelChanged;
        }

        // ─── Lifecycle Methods ────────────────────────────────────────

        /// <summary>
        /// Goi boi BuildingController ngay sau tower.Spawn() de ghi nhan vi tri grid.
        /// </summary>
        public void OnPlaced(Vector2Int gridPosition)
        {
            DBLog.Info($"tower.placed.{NetworkObjectId}", $"Tower placed at grid={gridPosition}.", 0f, this);
        }

        /// <summary>
        /// Tang level tower. Chi goi tu server (BuildingController.UpgradeTowerServerRpc).
        /// </summary>
        public void UpgradeLevel()
        {
            if (!IsServer || !CanUpgrade) return;
            _currentLevel.Value++;
            DBLog.Info($"tower.upgrade.{NetworkObjectId}", $"Tower upgraded to level {_currentLevel.Value}.", 0f, this);
        }

        // ─── Internal ─────────────────────────────────────────────────

        /// <summary>
        /// Attack loop server-only: tim enemy gan nhat trong range roi ban.
        /// </summary>
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
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy;
                }
            }

            return closest;
        }

        protected virtual void FireAt(BaseEnemy target)
        {
            if (_bulletPrefab == null || _pool == null || target == null) return;

            Vector3 firePos = _firePoint != null ? _firePoint.position : transform.position;
            Vector3 direction = (target.transform.position - firePos).normalized;
            Quaternion rotation = direction != Vector3.zero
                ? Quaternion.FromToRotation(Vector3.right, direction)
                : Quaternion.identity;

            NetworkObject bulletObj = _pool.Get(_bulletPrefab, firePos, rotation);
            if (bulletObj == null) return;

            BaseBullet bullet = bulletObj.GetComponent<BaseBullet>();
            if (bullet == null)
            {
                _pool.Return(bulletObj);
                return;
            }

            float damage = _model != null ? _model.Damage : (_data != null ? _data.damage : 10f);
            float speed  = _data != null ? _data.bulletSpeed   : 8f;
            float life   = _data != null ? _data.bulletLifetime : 3f;

            bullet.Initialize(damage, speed, life, target.NetworkObjectId, firePos);

            if (!bulletObj.IsSpawned)
            {
                bulletObj.Spawn();
            }

            DBLog.Info($"tower.fire.{NetworkObjectId}", $"Bullet fired. target={target.NetworkObjectId}, dmg={damage:0.0}.", 0.1f, this);
        }

        private void HandleLevelChanged(int previousValue, int newValue)
        {
            _model?.SetLevel(newValue);
        }

        /// <summary>
        /// Ve truc quan khi chon tower trong Editor:
        /// - Vong tron vang: tam tan cong (range)
        /// - Diem xanh la: vi tri ban dan (firePoint hoac root)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            float range = _model != null
                ? _model.Range
                : (_data != null ? _data.range : 4f);

            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.1f);
            Gizmos.DrawSphere(transform.position, range);
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, range);

            Vector3 firePos = _firePoint != null ? _firePoint.position : transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(firePos, 0.1f);
        }
    }
}
