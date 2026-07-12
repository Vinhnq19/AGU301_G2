using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonBuilder.Player
{
    /// <summary>
    /// Component trên owner client: phát hiện click chuột vào player đang chết trong vùng → yêu cầu server bắt đầu revive.
    /// Mỗi frame, nếu đang revive mà ra khỏi vùng → tự yêu cầu cancel (server validate lại lần cuối + reset progress về 0).
    /// Server (PlayerStats) mới là nơi thật sự tick progress + check range mỗi frame, đảm bảo authoritative.
    /// </summary>
    public sealed class ReviveInteractor : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float _reviveRange = 2.5f;
        [SerializeField] private InputReader _inputReader;
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _playerMask = ~0;

        private PlayerStats _myStats;
        private PlayerStats _localReviveTarget; // owner client only
        private ulong _localReviveTargetNetId; // backup khi target bị despawn trước khi RPC về

        private void Awake()
        {
            _myStats = _playerStats != null ? _playerStats : GetComponent<PlayerStats>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            if (_inputReader != null)
            {
                _inputReader.OnInteractPressed += HandleInteractPressed;
            }
            if (_myStats != null)
            {
                _myStats.OnReviveStateChanged += HandleReviveStateChanged;
                _myStats.OnDeadStateChanged += HandleDeadStateChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_inputReader != null)
            {
                _inputReader.OnInteractPressed -= HandleInteractPressed;
            }
            if (_myStats != null)
            {
                _myStats.OnReviveStateChanged -= HandleReviveStateChanged;
                _myStats.OnDeadStateChanged -= HandleDeadStateChanged;
            }
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Đang revive ai đó → check range mỗi frame.
            if (_localReviveTarget == null && _localReviveTargetNetId != 0)
            {
                // Target bị despawn giữa chừng → thử clear.
                if (TryResolveLocalTarget())
                {
                    CheckLocalTargetRange();
                }
                return;
            }

            if (_localReviveTarget != null)
            {
                CheckLocalTargetRange();
            }
        }

        private void CheckLocalTargetRange()
        {
            if (_localReviveTarget == null) return;

            // Target đã được revive (không còn chết) → clear local.
            if (!_localReviveTarget.IsDead)
            {
                ClearLocalTarget();
                return;
            }

            // Server ghi nhận NGƯỜI KHÁC là reviver (mình click sau, request bị từ chối)
            // → clear để không bị khóa di chuyển vô hạn chờ một revive không tồn tại.
            if (_localReviveTarget.ReviverClientId != ulong.MaxValue
                && _localReviveTarget.ReviverClientId != OwnerClientId)
            {
                ClearLocalTarget();
                return;
            }

            float dist = Vector2.Distance(transform.position, _localReviveTarget.transform.position);
            if (dist > _reviveRange)
            {
                ulong netId = _localReviveTarget.NetworkObjectId;
                ClearLocalTarget();
                RequestCancelReviveRpc(netId);
            }
        }

        private bool TryResolveLocalTarget()
        {
            if (NetworkManager.Singleton == null) return false;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_localReviveTargetNetId, out var netObj))
            {
                _localReviveTargetNetId = 0;
                return false;
            }
            _localReviveTarget = netObj.GetComponent<PlayerStats>();
            return _localReviveTarget != null;
        }

        private void HandleInteractPressed()
        {
            if (!IsOwner || _myStats == null || _myStats.IsDead) return;

            // Đang revive → click lần nữa để cancel thủ công.
            if (_localReviveTarget != null)
            {
                ulong netId = _localReviveTarget.NetworkObjectId;
                ClearLocalTarget();
                RequestCancelReviveRpc(netId);
                return;
            }

            // Tìm player đang chết dưới con trỏ chuột.
            PlayerStats target = FindDownedPlayerUnderCursor();
            if (target == null || target == _myStats) return;
            if (!target.IsDead) return;
            if (target.ReviverClientId != ulong.MaxValue) return; // đã có đồng minh khác đang cứu

            float dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist > _reviveRange) return;

            // Khóa di chuyển ngay tại owner để phản hồi tức thì (server confirm sau qua NetworkVariable).
            _localReviveTarget = target;
            _localReviveTargetNetId = target.NetworkObjectId;
            if (_playerController != null) _playerController.SetMovementLocked(true);

            RequestStartReviveRpc(target.NetworkObjectId);
        }

        private void HandleDeadStateChanged(bool isDead)
        {
            if (!IsOwner) return;
            if (isDead)
            {
                // Mình vừa chết → clear mọi tham chiếu revive đang dở.
                ClearLocalTarget();
            }
        }

        private void HandleReviveStateChanged(float progress, ulong reviverClientId)
        {
            if (!IsOwner || _myStats == null) return;

            bool iAmReviver = reviverClientId == OwnerClientId;

            if (iAmReviver)
            {
                // Server đã ghi nhận mình là reviver → đảm bảo khóa di chuyển.
                if (_playerController != null) _playerController.SetMovementLocked(true);
            }
            else
            {
                // Server đã clear mình khỏi reviver (do out-of-range / cancel / complete).
                if (_localReviveTarget != null || _localReviveTargetNetId != 0)
                {
                    ClearLocalTarget();
                }
            }
        }

        private void ClearLocalTarget()
        {
            _localReviveTarget = null;
            _localReviveTargetNetId = 0;
            if (_playerController != null) _playerController.SetMovementLocked(false);
        }

        private PlayerStats FindDownedPlayerUnderCursor()
        {
            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return null;

            Vector2? screen = GetPointerPosition();
            if (!screen.HasValue) return null;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.Value.x, screen.Value.y, Mathf.Abs(cam.transform.position.z)));
            Vector2 world2D = new Vector2(world.x, world.y);

            Collider2D hit = Physics2D.OverlapPoint(world2D, _playerMask);
            if (hit == null) return null;
            return hit.GetComponentInParent<PlayerStats>();
        }

        private static Vector2? GetPointerPosition()
        {
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                return Touchscreen.current.primaryTouch.position.ReadValue();
            }
            return null;
        }

        [Rpc(SendTo.Server)]
        private void RequestStartReviveRpc(ulong targetNetId)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out var netObj)) return;
            var targetStats = netObj.GetComponent<PlayerStats>();
            if (targetStats == null || !targetStats.IsDead) return;
            if (_myStats == null || _myStats.IsDead) return;
            if (targetStats.OwnerClientId == OwnerClientId) return; // không tự cứu

            float dist = Vector2.Distance(transform.position, netObj.transform.position);
            if (dist > _reviveRange * 1.25f) return; // cho phép sai số nhỏ từ client

            targetStats.ServerStartRevive(OwnerClientId);
        }

        [Rpc(SendTo.Server)]
        private void RequestCancelReviveRpc(ulong targetNetId)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out var netObj)) return;
            var targetStats = netObj.GetComponent<PlayerStats>();
            if (targetStats == null) return;
            targetStats.ServerCancelRevive(OwnerClientId);
        }
    }
}