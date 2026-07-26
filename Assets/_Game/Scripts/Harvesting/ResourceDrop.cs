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

        [Header("Drop Feel")]
        [Tooltip("Số cung nảy khi rơi. 1 = bay thẳng rồi đáp, 2-3 = nảy thêm cho cảm giác có trọng lượng.")]
        [SerializeField] private int _bounceCount = 2;
        [Tooltip("Độ xoay (độ) của item khi đang bay. 0 = tắt.")]
        [SerializeField] private float _spinDegrees = 360f;
        [Tooltip("Scale lúc mới xuất hiện (nhân với scale gốc) rồi pop lên 100%.")]
        [SerializeField, Range(0.1f, 1f)] private float _popInScale = 0.55f;
        [Tooltip("Độ bẹt (squash) khi đáp đất. 0 = tắt.")]
        [SerializeField, Range(0f, 0.8f)] private float _landSquash = 0.35f;
        [Tooltip("Biên độ nhấp nhô sau khi đáp, để item trông 'mời gọi nhặt'. 0 = tắt.")]
        [SerializeField] private float _idleBobHeight = 0.09f;
        [Tooltip("Thời gian 1 nhịp nhấp nhô (giây).")]
        [SerializeField] private float _idleBobDuration = 0.7f;

        private readonly NetworkVariable<ResourceType> _resourceType = new(ResourceType.Wood, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _amount = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<Vector3> _jumpOffset = new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Header("Magnet")]
        [Tooltip("Tốc độ hút tối đa (unit/giây).")]
        [SerializeField] private float _attractSpeed = 6f;
        [Tooltip("Tốc độ hút lúc bắt đầu — thấp hơn max để item 'từ từ bị kéo' rồi tăng tốc.")]
        [SerializeField] private float _attractStartSpeed = 1.5f;
        [Tooltip("Gia tốc hút (unit/giây²). Càng cao càng giật mạnh về phía player.")]
        [SerializeField] private float _attractAcceleration = 14f;
        [Tooltip("Delay sau khi jump xong trước khi magnet có thể hút (giây).")]
        [SerializeField] private float _magnetDelay = 0.1f;

        private IResourceService _sharedResources;
        private INetworkPool _pool;
        private bool _canPickup;
        private bool _isMagnetted;
        private bool _magnetAllowed;
        private Transform _magnetTarget;
        private Vector3 _initialVisualScale = Vector3.one;
        private float _currentAttractSpeed;

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

            PlayDropAnimation();

            if (IsServer)
            {
                DOVirtual.DelayedCall(_jumpDuration + _magnetDelay, () => _magnetAllowed = true);
            }
        }

        /// <summary>
        /// Hiệu ứng rơi (client-side visual): pop-in + bay theo cung có nảy + xoay,
        /// đáp đất thì squash&amp;stretch, rồi nhấp nhô nhẹ để báo "nhặt được".
        /// </summary>
        private void PlayDropAnimation()
        {
            _visual.DOKill();
            // Visual nhảy từ vị trí offset (local) về gốc; root đã được server dịch + sync qua NetworkTransform.
            _visual.localPosition = -_jumpOffset.Value;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = _initialVisualScale * _popInScale;

            // Bay theo cung, nảy thêm cho có cảm giác trọng lượng.
            _visual.DOLocalJump(Vector3.zero, _jumpPower, Mathf.Max(1, _bounceCount), _jumpDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(HandleLanded);

            // Pop-in: phóng to về scale gốc ngay đầu cú nhảy.
            _visual.DOScale(_initialVisualScale, _jumpDuration * 0.35f).SetEase(Ease.OutBack);

            // Xoay khi bay, chiều ngẫu nhiên để nhiều token không xoay đồng loạt.
            if (!Mathf.Approximately(_spinDegrees, 0f))
            {
                float dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                _visual.DOLocalRotate(new Vector3(0f, 0f, _spinDegrees * dir), _jumpDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.OutQuad);
            }
        }

        /// <summary>Đáp đất: bẹt xuống rồi bật lại, sau đó nhấp nhô nhẹ.</summary>
        private void HandleLanded()
        {
            if (_visual == null) return;

            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;

            if (_landSquash <= 0f)
            {
                StartIdleBob();
                return;
            }

            Vector3 squashed = new Vector3(
                _initialVisualScale.x * (1f + _landSquash),
                _initialVisualScale.y * (1f - _landSquash),
                _initialVisualScale.z);

            DOTween.Sequence().SetTarget(_visual)
                .Append(_visual.DOScale(squashed, 0.07f).SetEase(Ease.OutQuad))
                .Append(_visual.DOScale(_initialVisualScale, 0.18f).SetEase(Ease.OutBack))
                .OnComplete(StartIdleBob);
        }

        private void StartIdleBob()
        {
            if (_visual == null || _idleBobHeight <= 0f) return;

            _visual.DOLocalMoveY(_idleBobHeight, _idleBobDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        public void BeginMagnetAttract(Transform target)
        {
            if (!IsServer || _isMagnetted || !_canPickup || !_magnetAllowed) return;
            _isMagnetted = true;
            _magnetTarget = target;
            _currentAttractSpeed = _attractStartSpeed;

            // Cho mọi client biết để dừng nhấp nhô + nhấn scale, tránh item vừa bay vừa lắc.
            OnMagnetStartedClientRpc();
        }

        [ClientRpc]
        private void OnMagnetStartedClientRpc()
        {
            if (_visual == null) return;

            _visual.DOKill();
            _visual.localRotation = Quaternion.identity;
            _visual.DOLocalMove(Vector3.zero, 0.1f).SetEase(Ease.OutQuad);
            // Nhấn nhẹ scale để thấy rõ item "bị hút".
            _visual.DOScale(_initialVisualScale * 1.15f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo);
        }

        private void Update()
        {
            if (!IsServer || !_isMagnetted || _magnetTarget == null) return;

            // Tăng tốc dần: item bị kéo nhẹ rồi lao nhanh về player (cảm giác lực hút).
            _currentAttractSpeed = Mathf.MoveTowards(
                _currentAttractSpeed, _attractSpeed, _attractAcceleration * Time.deltaTime);

            transform.position = Vector3.MoveTowards(
                transform.position, _magnetTarget.position, _currentAttractSpeed * Time.deltaTime);
        }

        public void OnGetFromPool()
        {
            _canPickup = true;
            _magnetAllowed = false;
            _currentAttractSpeed = 0f;
            SetCollisionActive(true);
            ResetVisual();
        }

        public void OnReturnToPool()
        {
            _canPickup = false;
            _isMagnetted = false;
            _magnetAllowed = false;
            _magnetTarget = null;
            _currentAttractSpeed = 0f;
            SetCollisionActive(false);
            ResetVisual();
        }

        /// <summary>
        /// Trả visual về trạng thái gốc. PHẢI reset cả rotation vì drop được pool tái sử dụng —
        /// bỏ sót sẽ khiến item lần sau spawn ra với góc xoay/scale còn dư từ lần trước.
        /// </summary>
        private void ResetVisual()
        {
            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
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
