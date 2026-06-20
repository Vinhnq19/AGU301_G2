using DG.Tweening;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.Player;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Harvesting
{
    public sealed class ResourceDrop : NetworkBehaviour, IPoolable
    {
        // Mỗi loại tài nguyên có prefab drop riêng với visual (sprite/màu) gắn sẵn trên _visual.
        [SerializeField] private Transform _visual;

        [Header("Jump Tween")]
        [Tooltip("Khoảng cách drop nhảy ra phía bên phải (local X).")]
        [SerializeField] private float _jumpRightDistance = 0.6f;
        [Tooltip("Độ cao cung nhảy.")]
        [SerializeField] private float _jumpPower = 0.5f;
        [Tooltip("Thời gian nhảy (giây).")]
        [SerializeField] private float _jumpDuration = 0.4f;
        [Tooltip("Độ lệch dọc ngẫu nhiên nhẹ để nhiều drop không chồng lên nhau (0 = tắt).")]
        [SerializeField] private float _jumpVerticalJitter = 0.25f;

        private readonly NetworkVariable<ResourceType> _resourceType = new(ResourceType.Wood, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _amount = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private IResourceService _sharedResources;
        private INetworkPool _pool;
        private bool _canPickup;

        [Inject]
        public void Construct(IResourceService sharedResources, INetworkPool pool)
        {
            _sharedResources = sharedResources;
            _pool = pool;
        }

        public void Configure(ResourceType type, int amount)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            _resourceType.Value = type;
            _amount.Value = amount;

            // Server dịch cả drop (root) sang phải để vùng nhặt (collider root) đi theo item.
            // NetworkTransform sẽ sync vị trí này tới mọi client.
            float jitter = _jumpVerticalJitter > 0f
                ? UnityEngine.Random.Range(-_jumpVerticalJitter, _jumpVerticalJitter)
                : 0f;
            transform.position += new Vector3(_jumpRightDistance, jitter, 0f);

            DBLog.Info($"drop.configure.{NetworkObjectId}", $"ResourceDrop configured. id={NetworkObjectId}, type={type}, amount={amount}, rightOffset={_jumpRightDistance}.", 0.2f, this);
        }

        public void OnGetFromPool()
        {
            _canPickup = true;
            SetCollisionActive(true);

            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
         //   _visual.localScale = Vector3.one;

            // Vị trí ngang (ra bên phải) do server dịch trên root + NetworkTransform sync.
            // Visual chỉ làm cung nhảy lên rồi đáp về 0 cho cảm giác "bật ra".
            _visual.localPosition = new Vector3(-_jumpRightDistance, 0f, 0f);
            _visual.DOLocalJump(Vector3.zero, _jumpPower, 1, _jumpDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => _visual.localPosition = Vector3.zero);
            _visual.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.6f);
        }

        public void OnReturnToPool()
        {
            _canPickup = false;
            SetCollisionActive(false);

            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
           // _visual.localScale = Vector3.one;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_canPickup)
            {
                return;
            }

            if (!IsServer || _sharedResources == null || _pool == null)
            {
                DBLog.Warning($"drop.pickup.blocked.{NetworkObjectId}", $"Pickup ignored. server={IsServer}, sharedNull={_sharedResources == null}, poolNull={_pool == null}.", 1f, this);
                return;
            }

            if (other.GetComponentInParent<PlayerController>() == null)
            {
                DBLog.Info($"drop.pickup.non-player.{NetworkObjectId}", $"Pickup trigger ignored by non-player: {other.name}.", 1f, this);
                return;
            }

            _canPickup = false;
            SetCollisionActive(false);
            DBLog.Info($"drop.pickup.{NetworkObjectId}", $"ResourceDrop picked up. type={_resourceType.Value}, amount={_amount.Value}, by={other.name}.", 0.2f, this);
            _sharedResources.TryAdd(_resourceType.Value, _amount.Value);
            _pool.Return(NetworkObject);
        }

        private void SetCollisionActive(bool active)
        {
            foreach (Collider2D dropCollider in GetComponentsInChildren<Collider2D>(true))
            {
                dropCollider.enabled = active;
            }
        }
    }
}
