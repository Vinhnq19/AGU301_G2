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

        private InputReader _inputReader;
        private Rigidbody2D _rigidbody;
        private PlayerAnimation _animation;
        private Vector2 _moveInput;
        private float _lastDashTime = -999f;
        private bool _isDashing;
        private float _dashEndTime;

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
        }

        public override void OnNetworkDespawn()
        {
            if (_inputReader == null)
            {
                return;
            }

            _inputReader.OnMove -= HandleMove;
            _inputReader.OnDashPressed -= HandleDashPressed;
        }

        private void FixedUpdate()
        {
            if (!IsOwner || _rigidbody == null)
            {
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

        private void HandleMove(Vector2 moveInput)
        {
            _moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        private void HandleDashPressed()
        {
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
            
            if (DungeonBuilder.Audio.AudioManager.Instance != null)
            {
                DungeonBuilder.Audio.AudioManager.Instance.PlaySFX(DungeonBuilder.Core.Enums.SoundType.SFX_Hero_Dash, transform.position);
            }
            
            DBLog.Info($"player.dash.{NetworkObjectId}", $"Dash applied. direction={dashDirection}, speed={DashForce}, duration={_dashDuration}.", 0.25f, this);
        }
    }
}
