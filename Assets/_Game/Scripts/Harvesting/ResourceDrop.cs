using Cysharp.Threading.Tasks;
using DG.Tweening;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.Player;
using Unity.Netcode;
using UnityEngine;
using VContainer;
using System;

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
        private readonly NetworkVariable<Vector3> _jumpOffset = new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Magnet")]
        [SerializeField] private float _attractSpeed = 6f;
        [Tooltip("Delay sau khi jump xong trước khi magnet có thể hút (giây).")]
        [SerializeField] private float _magnetDelay = 0.1f;

        private IResourceService _sharedResources;
        private INetworkPool _pool;
        private bool _canPickup;
        private bool _isMagnetted;
        private bool _magnetAllowed;
        private Transform _magnetTarget;
        private Vector3 _initialVisualScale = Vector3.one;

        private void Awake()
        {
            if (_visual != null)
                _initialVisualScale = _visual.localScale;
        }

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
            Vector3 offset = new Vector3(_jumpRightDistance, jitter, 0f);
            _jumpOffset.Value = offset;
            transform.position += offset;

            DBLog.Info($"drop.configure.{NetworkObjectId}", $"ResourceDrop configured. id={NetworkObjectId}, type={type}, amount={amount}, rightOffset={_jumpRightDistance}.", 0.2f, this);
        }

        public void ConfigureWithOffset(ResourceType type, int amount, Vector3 offset)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            _resourceType.Value = type;
            _amount.Value = amount;
            _jumpOffset.Value = offset;
            transform.position += offset;

            DBLog.Info($"drop.configure.offset.{NetworkObjectId}", $"ResourceDrop configured with custom offset. id={NetworkObjectId}, type={type}, amount={amount}, offset={offset}.", 0.2f, this);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.localScale = _initialVisualScale;
            // Visual nhảy từ vị trí offset (local) về gốc; root đã được server dịch + sync qua NetworkTransform.
            _visual.localPosition = -_jumpOffset.Value;
            float jumpDone = _jumpDuration + _magnetDelay;
            _visual.DOLocalJump(Vector3.zero, _jumpPower, 1, _jumpDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => _visual.localPosition = Vector3.zero);
            _visual.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.6f);

            if (IsServer)
            {
                DOVirtual.DelayedCall(jumpDone, () => _magnetAllowed = true);
            }
        }

        public void BeginMagnetAttract(Transform target)
        {
            if (!IsServer || _isMagnetted || !_canPickup || !_magnetAllowed) return;
            _isMagnetted = true;
            _magnetTarget = target;
        }

        private void Update()
        {
            if (!IsServer || !_isMagnetted || _magnetTarget == null) return;
            transform.position = Vector3.MoveTowards(
                transform.position, _magnetTarget.position, _attractSpeed * Time.deltaTime);
        }

        public void OnGetFromPool()
        {
            _canPickup = true;
            _magnetAllowed = false;
            SetCollisionActive(true);

            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = _initialVisualScale;
        }

        public void OnReturnToPool()
        {
            _canPickup = false;
            _isMagnetted = false;
            _magnetAllowed = false;
            _magnetTarget = null;
            SetCollisionActive(false);

            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = _initialVisualScale;
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

            PlayPickupSoundClientRpc(transform.position);
            
            ReturnToPoolAsync().Forget();
        }

        [ClientRpc]
        private void PlayPickupSoundClientRpc(Vector3 pos)
        {
            if (DungeonBuilder.Audio.AudioManager.Instance != null)
            {
                DungeonBuilder.Audio.AudioManager.Instance.PlaySFX(SoundType.SFX_Item_Pickup, pos);
            }
        }

        private async UniTaskVoid ReturnToPoolAsync()
        {
            try
            {
                if (_visual != null)
                {
                    _visual.DOScale(Vector3.zero, 0.1f);
                }
                await UniTask.Delay(TimeSpan.FromSeconds(0.15f), cancellationToken: this.GetCancellationTokenOnDestroy());
                if (IsServer)
                {
                    _pool?.Return(NetworkObject);
                }
            }
            catch (OperationCanceledException)
            {
            }
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
