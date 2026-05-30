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
        [SerializeField, Min(0f)] private float _dashForce = 8f;

        private InputReader _inputReader;
        private Rigidbody2D _rigidbody;
        private Vector2 _moveInput;
        private float _lastDashTime = -999f;

        private float Speed => _data != null ? _data.speed : 5f;
        private float DashCooldown => _data != null ? _data.dashCooldown : 1f;

        [Inject]
        public void Construct(InputReader inputReader)
        {
            _inputReader = inputReader;
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
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

            _rigidbody.linearVelocity = _moveInput * Speed;
        }

        private void HandleMove(Vector2 moveInput)
        {
            _moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        }

        private void HandleDashPressed()
        {
            if (_rigidbody == null || Time.time - _lastDashTime < DashCooldown)
            {
                return;
            }

            Vector2 dashDirection = _moveInput.sqrMagnitude > 0.01f ? _moveInput.normalized : Vector2.up;
            _lastDashTime = Time.time;
            _rigidbody.AddForce(dashDirection * _dashForce, ForceMode2D.Impulse);
            DBLog.Info($"player.dash.{NetworkObjectId}", $"Dash applied. direction={dashDirection}, force={_dashForce}.", 0.25f, this);
        }
    }
}
