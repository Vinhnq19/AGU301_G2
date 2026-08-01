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
        [Tooltip("Khoảng cách văng RA XA tối thiểu khi rơi (unit).")]
        [SerializeField] private float _scatterMinDistance = 0.5f;
        [Tooltip("Khoảng cách văng RA XA tối đa khi rơi (unit). Càng lớn item càng bắn tung tóe.")]
        [SerializeField] private float _scatterMaxDistance = 1.5f;
        [Tooltip("Độ cao cung nhảy.")]
        [SerializeField] private float _jumpPower = 0.5f;
        [Tooltip("Thời gian nhảy (giây).")]
        [SerializeField] private float _jumpDuration = 0.4f;

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
        [SerializeField] private float _attractSpeed = 9f;
        [Tooltip("Tốc độ hút lúc bắt đầu — thấp hơn max để item 'từ từ bị kéo' rồi tăng tốc.")]
        [SerializeField] private float _attractStartSpeed = 2.5f;
        [Tooltip("Gia tốc hút (unit/giây²). Càng cao càng giật mạnh về phía player.")]
        [SerializeField] private float _attractAcceleration = 22f;
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

            // Văng ra MỌI HƯỚNG với khoảng cách ngẫu nhiên (trước đây luôn nhảy sang phải nên
            // đống drop xếp thành hàng thẳng, trông rất máy móc).
            // Server dịch cả drop (root) để vùng nhặt (collider root) đi theo item;
            // NetworkTransform sync vị trí này tới mọi client.
            Vector3 offset = GetScatterOffset();
            _jumpOffset.Value = offset;
            transform.position += offset;

            DBLog.Info($"drop.configure.{NetworkObjectId}", $"ResourceDrop configured. id={NetworkObjectId}, type={type}, amount={amount}, scatter={offset}.", 0.2f, this);
        }

        /// <summary>Vector văng ngẫu nhiên theo mọi hướng, độ dài trong [min, max].</summary>
        private Vector3 GetScatterOffset()
        {
            float max = Mathf.Max(_scatterMinDistance, _scatterMaxDistance);
            float min = Mathf.Min(_scatterMinDistance, _scatterMaxDistance);
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = UnityEngine.Random.Range(min, max);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
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

        /// <summary>
        /// Chặn spam tiếng nhặt: magnet thường tóm nhiều item cùng lúc, phát mỗi item một tiếng
        /// sẽ chồng thành tiếng ồn. Dùng chung cho mọi drop nên là static.
        /// </summary>
        private static float _lastCollectSfxTime = -99f;
        private const float CollectSfxCooldown = 0.12f;

        /// <summary>
        /// Tiếng DUY NHẤT của vòng đời nhặt item. Trước đây có 2 tiếng chồng nhau
        /// (magnet lúc bị hút + pickup lúc chạm) nghe như bị lặp — nay gộp còn một.
        /// </summary>
        private void PlayCollectSfx()
        {
            if (Time.time - _lastCollectSfxTime < CollectSfxCooldown) return;
            if (DungeonBuilder.Audio.AudioManager.Instance == null) return;

            _lastCollectSfxTime = Time.time;
            DungeonBuilder.Audio.AudioManager.Instance.PlaySFX(SoundType.SFX_Item_Magnet, transform.position);
        }

        [ClientRpc]
        private void OnMagnetStartedClientRpc()
        {
            PlayCollectSfx();

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

            // Item bị hút thì tiếng đã phát lúc bắt đầu hút rồi — không phát lần hai.
            // Chỉ item nhặt trực tiếp (đi đè lên, chưa kịp bị hút) mới cần phát ở đây.
            if (!_isMagnetted)
            {
                PlayCollectSoundClientRpc();
            }

            ReturnToPoolAsync().Forget();
        }

        [ClientRpc]
        private void PlayCollectSoundClientRpc()
        {
            PlayCollectSfx();
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
