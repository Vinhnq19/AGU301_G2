using DungeonBuilder.Core;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Harvesting;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Player
{
    public sealed class ResourceMagnetComponent : NetworkBehaviour
    {
        [Tooltip("Bán kính hút theo MiningSkill. Index 0/1 là mức cơ bản — phải > 0 để Hero đi gần " +
                 "là hút được ngay từ đầu game (skill mặc định = 1); nâng skill thì hút xa hơn.")]
        [SerializeField] private float[] _radiusBySkillLevel = { 1.8f, 1.8f, 2.4f, 3.2f, 4f, 5f };
        [Tooltip("Layer của các ResourceDrop (mặc định drop nằm ở layer Default).")]
        [SerializeField] private LayerMask _dropLayer = 1; // 1 << 0 = Default

        private IResourceService _resourceService;
        private float _currentRadius;
        private PlayerStats _playerStats;

        private readonly Collider2D[] _overlapBuffer = new Collider2D[32];

        [Inject]
        public void Construct(IResourceService resourceService)
        {
            _resourceService = resourceService;
        }

        private void Awake()
        {
            _playerStats = GetComponent<PlayerStats>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer) return;

            // Inject có thể chưa chạy (thiếu wire ở LifetimeScope) — vẫn phải hút được ở mức cơ bản
            // thay vì NullReference rồi tắt hẳn tính năng.
            if (_resourceService == null)
            {
                Debug.LogWarning("[ResourceMagnet] IResourceService chưa được inject — dùng bán kính cơ bản, " +
                                 "magnet sẽ không nâng theo MiningSkill.", this);
                UpdateRadius(0);
                return;
            }

            _resourceService.ResourceChanged += HandleResourceChanged;
            UpdateRadius(_resourceService.GetAmount(ResourceType.MiningSkill));
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (_resourceService != null)
                _resourceService.ResourceChanged -= HandleResourceChanged;
        }

        private void FixedUpdate()
        {
            if (!IsServer || _currentRadius <= 0f) return;
            // Player chết thì không hút đồ.
            if (_playerStats != null && _playerStats.IsDead) return;

            int count = Physics2D.OverlapCircleNonAlloc(transform.position, _currentRadius, _overlapBuffer, _dropLayer);
            for (int i = 0; i < count; i++)
            {
                if (_overlapBuffer[i] == null) continue;
                var drop = _overlapBuffer[i].GetComponentInParent<ResourceDrop>();
                drop?.BeginMagnetAttract(transform);
            }
        }

        private void HandleResourceChanged(ResourceChanged change)
        {
            if (change.Type == ResourceType.MiningSkill)
                UpdateRadius(change.CurrentAmount);
        }

        private void UpdateRadius(int skillLevel)
        {
            if (_radiusBySkillLevel == null || _radiusBySkillLevel.Length == 0)
            {
                _currentRadius = 0f;
                return;
            }

            int idx = Mathf.Clamp(skillLevel, 0, _radiusBySkillLevel.Length - 1);
            _currentRadius = _radiusBySkillLevel[idx];
        }

        private void OnDrawGizmosSelected()
        {
            if (_radiusBySkillLevel == null || _radiusBySkillLevel.Length == 0) return;

            // Runtime: vẽ bán kính đang dùng. Edit mode: vẽ bán kính cơ bản (index 0).
            float radius = _currentRadius > 0f ? _currentRadius : _radiusBySkillLevel[0];
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
