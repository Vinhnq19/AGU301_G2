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
        [SerializeField] private float[] _radiusBySkillLevel = { 0f, 0f, 1.5f, 2.5f, 3.5f, 5f };
        [SerializeField] private LayerMask _dropLayer;

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
            int idx = Mathf.Clamp(skillLevel, 0, _radiusBySkillLevel.Length - 1);
            _currentRadius = _radiusBySkillLevel[idx];
        }
    }
}
