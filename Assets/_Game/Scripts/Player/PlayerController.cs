using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Data;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : NetworkBehaviour
    {
        [SerializeField] private PlayerDataSO _data;
        [SerializeField, Min(0.01f)] private float _dashDuration = 0.2f;
        [SerializeField] private PlayerStats _playerStats;

        private InputReader _inputReader;
        private Rigidbody2D _rigidbody;
        private PlayerAnimation _animation;
        private Vector2 _moveInput;
        private float _lastDashTime = -999f;
        private bool _isDashing;
        private float _dashEndTime;

        /// <summary>
        /// True khi player đang chết HOẶC đang trong thao tác revive (đứng đợi).
        /// Set bởi <see cref="SetMovementLocked"/> hoặc khi nhận event OnDeadStateChanged.
        /// </summary>
        private bool _movementLocked;

        private float Speed => _data != null ? _data.speed : 5f;
        private float DashCooldown => _data != null ? _data.dashCooldown : 1f;
        private float DashForce => _data != null ? _data.dashForce : 8f;

        [Inject]
        public void Construct(InputReader inputReader)
        {
            _inputReader = inputReader;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _animation = GetComponent<PlayerAnimation>();
            if (_playerStats == null)
            {
                _playerStats = GetComponent<PlayerStats>();
            }
        }

        public override void OnNetworkSpawn()
        {
            DBLog.Info($"player.spawn.{NetworkObjectId}", $"Player spawned. id={NetworkObjectId}, owner={OwnerClientId}, isOwner={IsOwner}, isServer={IsServer}, inputReaderNull={_inputReader == null}.", 0f, this);

            if (!IsOwner || _inputReader == null)
            {
                return;
            }

            _inputReader.OnMove += HandleMove;
            _inputReader.OnDashPressed += HandleDashPressed;

            if (_playerStats != null)
            {
                _playerStats.OnDeadStateChanged += HandleDeadStateChanged;
                // Đồng bộ trạng thái hiện tại (phòng khi script enable sau khi event đã fire lần đầu).
                HandleDeadStateChanged(_playerStats.IsDead);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_inputReader == null)
            {
                return;
            }

            _inputReader.OnMove -= HandleMove;
            _inputReader.OnDashPressed -= HandleDashPressed;

            if (_playerStats != null)
            {
                _playerStats.OnDeadStateChanged -= HandleDeadStateChanged;
            }
        }

        private void FixedUpdate()
        {
            if (!IsOwner || _rigidbody == null)
            {
                return;
            }

            if (IsInputBlocked())
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            if (_animation != null && _animation.IsForaging)
            {
                _rigidbody.linearVelocity = Vector2.zero;
                return;
            }

            // While dashing, keep the dash velocity; don't let normal movement overwrite it.
            if (_isDashing)
            {
                if (Time.time >= _dashEndTime)
                {
                    _isDashing = false;
                }
                else
                {
                    return;
                }
            }

            _rigidbody.linearVelocity = _moveInput * Speed;
        }

        /// <summary>
        /// True khi player chết hoặc đang bị khóa bởi SetMovementLocked (revive).
        /// Check trực tiếp IsDead mỗi frame — không phụ thuộc event chain — để chắc chắn
        /// player hết máu là bất động kể cả khi event chain bị race / skip.
        /// </summary>
        private bool IsInputBlocked()
        {
            if (_movementLocked) return true;
            if (_playerStats != null && _playerStats.IsDead) return true;
            return false;
        }

        /// <summary>
        /// Khóa / mở khóa di chuyển + toàn bộ input (dùng khi player chết hoặc đang revive).
        /// Khi locked: input map tắt + Rigidbody chuyển sang Static để ClientNetworkTransform không drift được.
        /// </summary>
        public void SetMovementLocked(bool locked)
        {
            _movementLocked = locked;
            if (_inputReader != null)
            {
                _inputReader.SetEnabled(!locked);
            }
            if (_rigidbody != null)
            {
                if (locked)
                {
                    _moveInput = Vector2.zero;
                    _rigidbody.linearVelocity = Vector2.zero;
                    _rigidbody.angularVelocity = 0f;
                    // Đóng băng body để physics + collision drift không đẩy player đi được.
                    _rigidbody.bodyType = RigidbodyType2D.Static;
                }
                else
                {
                    _rigidbody.bodyType = RigidbodyType2D.Dynamic;
                }
            }
        }

        private void HandleDeadStateChanged(bool isDead)
        {
            SetMovementLocked(isDead);
        }

        private void HandleMove(Vector2 moveInput)
        {
            if (IsInputBlocked()) return;
            _moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        private void HandleDashPressed()
        {
            if (IsInputBlocked()) return;
            if (_animation != null && _animation.IsForaging)
            {
                return;
            }

            if (_rigidbody == null || _isDashing || Time.time - _lastDashTime < DashCooldown)
            {
                return;
            }

            Vector2 dashDirection;
            if (_moveInput.sqrMagnitude > 0.01f)
            {
                dashDirection = _moveInput.normalized;
            }
            else if (_animation != null)
            {
                dashDirection = _animation.GetFacingVector();
            }
            else
            {
                dashDirection = Vector2.up;
            }

            _isDashing = true;
            _dashEndTime = Time.time + _dashDuration;
            _lastDashTime = Time.time;
            _rigidbody.linearVelocity = dashDirection * DashForce;
            DBLog.Info($"player.dash.{NetworkObjectId}", $"Dash applied. direction={dashDirection}, speed={DashForce}, duration={_dashDuration}.", 0.25f, this);
        }
    }
}