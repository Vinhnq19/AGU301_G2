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
    public class BaseTower : NetworkBehaviour
    {
        [Header("Tower Config")]
        [SerializeField] protected TowerDataSO _data;
        [SerializeField] private NetworkObject _bulletPrefab;
        [SerializeField] private Transform _firePoint;

        [Header("Visual")]
        [SerializeField] private SpriteRenderer _visual;

        private readonly NetworkVariable<int> _currentLevel = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkList<ResourceAmount> _paidResources = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly Collider2D[] _overlapResults = new Collider2D[16];
        private ContactFilter2D _enemyFilter;

        private TowerModel _model;
        private TowerPresenter _presenter;
        private TowerView _view;

        [Inject] private INetworkPool _pool;

        public int CurrentLevel => _currentLevel.Value;
        public bool CanUpgrade  => _data != null && _currentLevel.Value < _data.maxLevel;

        public int GetPaid(ResourceType type)
        {
            foreach (ResourceAmount resource in _paidResources)
            {
                if (resource.Type == type)
                    return resource.Amount;
            }

            return 0;
        }

        public bool IsConstructed
        {
            get
            {
                if (_data == null || _data.buildCost == null || _data.buildCost.Length == 0)
                    return true;
                foreach (var pair in GetRequiredBuildAmounts())
                    if (GetPaid(pair.Key) < pair.Value) return false;
                return true;
            }
        }

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

            if (IsServer)
                ResetConstructionProgress();

            _currentLevel.OnValueChanged += HandleLevelChanged;
            _paidResources.OnListChanged += HandlePaidChanged;

            if (_presenter != null)
                _presenter.Initialize(_model, _view);

            _model.SetLevel(_currentLevel.Value);
            foreach (ResourceAmount resource in _paidResources)
                _model.SetPaid(resource.Type, resource.Amount);

            UpdateVisualAlpha();

            DBLog.Info(
                $"tower.spawn.{NetworkObjectId}",
                $"[BaseTower] Spawned. data={(_data != null ? _data.name : "NULL")}, " +
                $"presenter={(_presenter != null ? "OK" : "NULL")}, view={(_view != null ? "OK" : "NULL")}, " +
                $"isConstructed={IsConstructed}.",
                0f, this);

            if (IsServer)
                StartAttackLoopAsync().Forget();
        }

        public override void OnNetworkDespawn()
        {
            _currentLevel.OnValueChanged -= HandleLevelChanged;
            _paidResources.OnListChanged -= HandlePaidChanged;
        }

        /// <summary>Goi boi BuildingController sau tower.Spawn().</summary>
        public void OnPlaced(Vector2Int gridPosition)
        {
            DBLog.Info($"tower.placed.{NetworkObjectId}", $"[BaseTower] Tower placed at grid={gridPosition}.", 0f, this);
        }

        /// <summary>
        /// Danh dau toan bo build cost da duoc thanh toan.
        /// Chi goi tu server sau khi IResourceService.TrySpend thanh cong.
        /// </summary>
        public void CompleteConstruction()
        {
            if (!IsServer) return;

            foreach (var pair in GetRequiredBuildAmounts())
            {
                if (TryGetPaidIndex(pair.Key, out int index))
                    _paidResources[index] = new ResourceAmount(pair.Key, pair.Value);
            }
        }

        /// <summary>Tang level tower. Chi goi tu server.</summary>
        public void UpgradeLevel()
        {
            if (!IsServer || !CanUpgrade) return;
            _currentLevel.Value++;
            DBLog.Info($"tower.upgrade.{NetworkObjectId}", $"[BaseTower] Upgraded to level {_currentLevel.Value}.", 0f, this);
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
                if (dist < closestDist) { closestDist = dist; closest = enemy; }
            }
            return closest;
        }

        protected virtual void FireAt(BaseEnemy target)
        {
            if (_bulletPrefab == null || _pool == null || target == null) return;
            Vector3 firePos   = _firePoint != null ? _firePoint.position : transform.position;
            Vector3 direction = (target.transform.position - firePos).normalized;
            Quaternion rot    = direction != Vector3.zero ? Quaternion.FromToRotation(Vector3.right, direction) : Quaternion.identity;

            NetworkObject bulletObj = _pool.Get(_bulletPrefab, firePos, rot);
            if (bulletObj == null) return;

            BaseBullet bullet = bulletObj.GetComponent<BaseBullet>();
            if (bullet == null) { _pool.Return(bulletObj); return; }

            float damage = _model != null ? _model.Damage : (_data != null ? _data.damage : 10f);
            bullet.Initialize(damage, _data?.bulletSpeed ?? 8f, _data?.bulletLifetime ?? 3f, target.NetworkObjectId, firePos);

            if (!bulletObj.IsSpawned) bulletObj.Spawn();

            DBLog.Info($"tower.fire.{NetworkObjectId}", $"[BaseTower] Fired. target={target.NetworkObjectId}, dmg={damage:0.0}.", 0.1f, this);
        }

        private void ResetConstructionProgress()
        {
            _paidResources.Clear();
            foreach (var pair in GetRequiredBuildAmounts())
            {
                _paidResources.Add(new ResourceAmount(pair.Key, 0));
            }
        }

        private Dictionary<ResourceType, int> GetRequiredBuildAmounts()
        {
            var totals = new Dictionary<ResourceType, int>();
            if (_data?.buildCost == null)
                return totals;

            foreach (ResourceCost cost in _data.buildCost)
            {
                if (!ResourceTypeUtility.IsValid(cost.type) || cost.amount < 0)
                    continue;

                totals.TryGetValue(cost.type, out int current);
                totals[cost.type] = cost.amount > int.MaxValue - current
                    ? int.MaxValue
                    : current + cost.amount;
            }

            return totals;
        }

        private bool TryGetPaidIndex(ResourceType type, out int index)
        {
            for (int i = 0; i < _paidResources.Count; i++)
            {
                if (_paidResources[i].Type == type)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private void HandlePaidChanged(NetworkListEvent<ResourceAmount> changeEvent)
        {
            if (changeEvent.Type != NetworkListEvent<ResourceAmount>.EventType.Value)
                return;

            _model?.SetPaid(changeEvent.Value.Type, changeEvent.Value.Amount);
            UpdateVisualAlpha();

            DBLog.Info(
                $"tower.construction.{NetworkObjectId}",
                $"[BaseTower] Construction updated. {changeEvent.Value.Type}={changeEvent.Value.Amount}, isConstructed={IsConstructed}.",
                0.1f, this);
        }

        private void HandleLevelChanged(int _, int newVal) => _model?.SetLevel(newVal);

        private void UpdateVisualAlpha()
        {
            if (_visual == null) return;
            Color c = _visual.color;
            c.a = IsConstructed ? 1f : 0.4f;
            _visual.color = c;
        }

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
