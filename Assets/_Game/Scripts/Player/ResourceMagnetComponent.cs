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

        // KHÔNG dùng [Inject] cho IResourceService: player được spawn runtime nên
        // PlayerLifetimeScope không có parent là GameLifetimeScope → VContainer không resolve được
        // (ném "No such registration of type: IResourceService"). Lấy trực tiếp service ở
        // OnNetworkSpawn thay vì phụ thuộc DI xuyên scope.
        private static IResourceService FindResourceService()
        {
            var shared = FindFirstObjectByType<DungeonBuilder.Networking.SharedResourceManager>(
                FindObjectsInactive.Include);
            return shared as IResourceService;
        }

        private void Awake()
        {
            _playerStats = GetComponent<PlayerStats>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer) return;

            // Bán kính cơ bản có ngay để hút được từ giây đầu; nếu service chưa sẵn sàng
            // (player spawn trước khi GameRoot của scene mới init) thì FixedUpdate sẽ thử lại.
            UpdateRadius(0);
            TryBindResourceService();
        }

        /// <summary>
        /// Gắn vào IResourceService khi nó đã tồn tại. Gọi lại được nhiều lần — cần vậy vì
        /// player được spawn ngay lúc chuyển scene, thời điểm đó SharedResourceManager có thể chưa có.
        /// </summary>
        private void TryBindResourceService()
        {
            if (_resourceService != null) return;

            _resourceService = FindResourceService();
            if (_resourceService == null) return;

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
            if (!IsServer) return;

            // Retry gắn service (rẻ: chỉ chạy tới khi gắn được, sau đó return ngay).
            TryBindResourceService();

            if (_currentRadius <= 0f) return;
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
