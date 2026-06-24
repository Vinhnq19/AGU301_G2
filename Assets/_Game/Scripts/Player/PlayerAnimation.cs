using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Player
{
    /// <summary>
    /// Drives the player's visual: 3 directional sprite sets (up/down/side) and
    /// 3 animation states (Idle/Run/Foraging). The owner computes facing + state
    /// from velocity and syncs them via NetworkVariables; every client advances
    /// frames locally on the child "Visual" SpriteRenderer. Foraging is begun/ended
    /// by the harvest tool (BeginForaging turns the player to face the node).
    /// </summary>
    public sealed class PlayerAnimation : NetworkBehaviour
    {
        [System.Serializable]
        private sealed class DirectionalSprites
        {
            public Sprite[] up;
            public Sprite[] down;
            public Sprite[] side; // side-right; flipped horizontally when facing Left
        }

        [SerializeField] private DirectionalSprites _idle;
        [SerializeField] private DirectionalSprites _run;
        [SerializeField] private DirectionalSprites _foraging;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField, Min(0.01f)] private float _frameRate = 10f;
        [SerializeField, Min(0.001f)] private float _moveThreshold = 0.05f;

        private Rigidbody2D _rigidbody;

        private readonly NetworkVariable<FacingDir> _netFacing =
            new(FacingDir.Down, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<AnimState> _netState =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Color> _playerColor =
            new(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float _animElapsed;
        private FacingDir _lastDrivenFacing = FacingDir.Down;
        private AnimState _lastDrivenState = AnimState.Idle;

        /// <summary>True while a foraging swing is in progress. Read by PlayerController to lock movement.</summary>
        public bool IsForaging { get; private set; }

        public Sprite DefaultSprite => _idle?.down?.Length > 0 ? _idle.down[0] : null;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _playerColor.Value = Color.HSVToRGB((OwnerClientId * 0.3f) % 1f, 0.8f, 1f);
            }

            _playerColor.OnValueChanged += HandleColorChanged;
            ApplyColor(_playerColor.Value);

            DriveVisual(immediate: true);
            DBLog.Info($"anim.spawn.{NetworkObjectId}", $"PlayerAnimation spawned. isOwner={IsOwner}.", 0f, this);
        }

        public override void OnNetworkDespawn()
        {
            _playerColor.OnValueChanged -= HandleColorChanged;
        }

        private void HandleColorChanged(Color previousValue, Color newValue)
        {
            ApplyColor(newValue);
        }

        private void ApplyColor(Color color)
        {
            if (_renderer != null)
            {
                _renderer.color = color;
            }
        }

        /// <summary>Called by the harvest tool when a swing starts: face the target node and enter Foraging.</summary>
        public void BeginForaging(Vector3 worldTarget)
        {
            IsForaging = true;
            _netState.Value = AnimState.Foraging;
            _netFacing.Value = PlayerAnimLogic.ComputeFacing((Vector2)(worldTarget - transform.position), 0f, _netFacing.Value);
        }

        /// <summary>Called by the harvest tool when the swing ends; State reverts to Idle/Run on the next update.</summary>
        public void EndForaging()
        {
            IsForaging = false;
        }

        /// <summary>Current facing as a unit vector (for dash direction, etc.).</summary>
        public Vector2 GetFacingVector() => _netFacing.Value switch
        {
            FacingDir.Up => Vector2.up,
            FacingDir.Down => Vector2.down,
            FacingDir.Left => Vector2.left,
            _ => Vector2.right,
        };

        private void Update()
        {
            if (IsOwner)
            {
                SampleOwnerIntent();
            }

            DriveVisual(immediate: false);
        }

        private void SampleOwnerIntent()
        {
            if (IsForaging)
            {
                if (_netState.Value != AnimState.Foraging)
                {
                    _netState.Value = AnimState.Foraging;
                }
                return; // keep node-facing + Foraging state for the whole swing
            }

            Vector2 velocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;
            float thresholdSq = _moveThreshold * _moveThreshold;

            FacingDir facing = PlayerAnimLogic.ComputeFacing(velocity, thresholdSq, _netFacing.Value);
            AnimState state = PlayerAnimLogic.ComputeState(velocity, thresholdSq, foraging: false);

            if (facing != _netFacing.Value)
            {
                _netFacing.Value = facing;
            }

            if (state != _netState.Value)
            {
                _netState.Value = state;
            }
        }

        private void DriveVisual(bool immediate)
        {
            FacingDir facing = _netFacing.Value;
            AnimState state = _netState.Value;

            if (facing != _lastDrivenFacing || state != _lastDrivenState)
            {
                _lastDrivenFacing = facing;
                _lastDrivenState = state;
                _animElapsed = 0f;
            }
            else if (!immediate)
            {
                _animElapsed += Time.deltaTime;
            }

            if (_renderer == null)
            {
                return;
            }

            Sprite[] arr = SelectArray(state, facing);
            if (arr == null || arr.Length == 0)
            {
                return;
            }

            int frame = PlayerAnimLogic.FrameAtTime(_animElapsed, 1f / _frameRate, arr.Length);
            _renderer.sprite = arr[frame];
            _renderer.flipX = facing == FacingDir.Left;
        }

        private Sprite[] SelectArray(AnimState state, FacingDir facing)
        {
            DirectionalSprites set = state switch
            {
                AnimState.Run => _run,
                AnimState.Foraging => _foraging,
                _ => _idle,
            };
            if (set == null)
            {
                return null;
            }

            return facing switch
            {
                FacingDir.Up => set.up,
                FacingDir.Down => set.down,
                _ => set.side,
            };
        }
    }
}
