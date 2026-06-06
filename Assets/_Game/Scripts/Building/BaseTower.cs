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
    /// Script chinh cua moi tower. Quan ly vong doi: Place, Contribute, Attack, Upgrade, Remove.
    /// Tower bat dau o trang thai UnderConstruction (woodPaid=0, orePaid=0).
    /// Attack loop chi chay sau khi IsConstructed == true.
    /// NetworkVariable replicate state sang tat ca client.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class BaseTower : NetworkBehaviour
    {
        [Header("Tower Config")]
        [SerializeField] protected TowerDataSO _data;
        [SerializeField] private NetworkObject _bulletPrefab;
        [SerializeField] private Transform _firePoint;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _visual;

        private readonly NetworkVariable<int> _currentLevel = new(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _woodPaid = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _orePaid = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly Collider2D[] _overlapResults = new Collider2D[16];
        private ContactFilter2D _enemyFilter;

        private TowerModel _model;
        private TowerPresenter _presenter;
        private TowerView _view;

        [Inject] private INetworkPool _pool;

        public int CurrentLevel => _currentLevel.Value;
        public bool CanUpgrade  => _data != null && _currentLevel.Value < _data.maxLevel;
        public int WoodPaid     => _woodPaid.Value;
        public int OrePaid      => _orePaid.Value;
        public bool IsConstructed => _data == null
            || (_woodPaid.Value >= _data.woodCost && _orePaid.Value >= _data.oreCost);

        private void Awake()
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
            _woodPaid.OnValueChanged     += (_, _) => HandleConstructionChanged();
            _orePaid.OnValueChanged      += (_, _) => HandleConstructionChanged();

            if (_presenter != null)
            {
                _presenter.Initialize(_model, _view);
            }

            if (IsServer)
            {
                _woodPaid.Value = 0;
                _orePaid.Value  = 0;
            }

            _model.SetLevel(_currentLevel.Value);
            _model.SetConstructionProgress(_woodPaid.Value, _orePaid.Value);
            UpdateVisualAlpha();

            if (IsServer)
            {
                StartAttackLoopAsync().Forget();
            }
        }

        public override void OnNetworkDespawn()
        {
            _currentLevel.OnValueChanged -= HandleLevelChanged;
        }

        /// <summary>
        /// Goi boi BuildingController ngay sau tower.Spawn() de ghi nhan vi tri grid.
        /// </summary>
        public void OnPlaced(Vector2Int gridPosition)
        {
            DBLog.Info($"tower.placed.{NetworkObjectId}", $"[BaseTower] Tower placed at grid={gridPosition}.", 0f, this);
        }

        /// <summary>
        /// Cap nhat tien do xay dung. Chi goi tu server (BuildingController.ContributeTowerServerRpc).
        /// </summary>
        public void UpdateConstruction(int woodPaid, int orePaid)
        {
            if (!IsServer) return;
            _woodPaid.Value = woodPaid;
            _orePaid.Value  = orePaid;
        }

        /// <summary>
        /// Tang level tower. Chi goi tu server (BuildingController.UpgradeTowerServerRpc).
        /// </summary>
        public void UpgradeLevel()
        {
            if (!IsServer || !CanUpgrade) return;
            _currentLevel.Value++;
            DBLog.Info($"tower.upgrade.{NetworkObjectId}", $"[BaseTower] Tower upgraded to level {_currentLevel.Value}.", 0f, this);
        }

        private async UniTaskVoid StartAttackLoopAsync()
        {
            try
            {
                await UniTask.WaitUntil(() => IsConstructed, cancellationToken: destroyCancellationToken);

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

            Vector3 firePos  = _firePoint != null ? _firePoint.position : transform.position;
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
            float speed  = _data != null ? _data.bulletSpeed    : 8f;
            float life   = _data != null ? _data.bulletLifetime : 3f;

            bullet.Initialize(damage, speed, life, target.NetworkObjectId, firePos);

            if (!bulletObj.IsSpawned)
            {
                bulletObj.Spawn();
            }

            DBLog.Info($"tower.fire.{NetworkObjectId}", $"[BaseTower] Bullet fired. target={target.NetworkObjectId}, dmg={damage:0.0}.", 0.1f, this);
        }

        private void HandleLevelChanged(int previousValue, int newValue)
        {
            _model?.SetLevel(newValue);
        }

        private void HandleConstructionChanged()
        {
            UpdateVisualAlpha();
            _model?.SetConstructionProgress(_woodPaid.Value, _orePaid.Value);
        }

        private void UpdateVisualAlpha()
        {
            if (_visual == null) return;
            Color c = _visual.color;
            c.a = IsConstructed ? 1f : 0.4f;
            _visual.color = c;
        }

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
