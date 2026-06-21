using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Player
{
    /// <summary>
    /// Drives the player's visual: 3 directional sprite sets (up/down/side) and
    /// 3 animation states (Idle/Run/Foraging). The owner computes facing + state
    /// from velocity (and the attack input for Foraging) and syncs them via
    /// NetworkVariables; every client advances frames locally and swaps sprites
    /// on the child "Visual" SpriteRenderer.
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
        [SerializeField, Min(0.05f)] private float _foragingDuration = 0.5f;

        private Rigidbody2D _rigidbody;
        private InputReader _inputReader;

        private readonly NetworkVariable<FacingDir> _netFacing =
            new(FacingDir.Down, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<AnimState> _netState =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private float _animElapsed;
        private FacingDir _lastDrivenFacing = FacingDir.Down;
        private AnimState _lastDrivenState = AnimState.Idle;
        private float _foragingUntil = -1f;

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
            if (IsOwner && _inputReader != null)
            {
                _inputReader.OnAttackPressed += HandleAttackPressed;
            }

            DriveVisual(immediate: true);
            DBLog.Info($"anim.spawn.{NetworkObjectId}", $"PlayerAnimation spawned. isOwner={IsOwner}.", 0f, this);
        }

        public override void OnNetworkDespawn()
        {
            if (_inputReader != null)
            {
                _inputReader.OnAttackPressed -= HandleAttackPressed;
            }
        }

        private void HandleAttackPressed()
        {
            // Owner-only subscription; open a foraging window.
            _foragingUntil = Time.time + _foragingDuration;
        }

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
            Vector2 velocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector2.zero;
            float thresholdSq = _moveThreshold * _moveThreshold;
            bool foraging = Time.time < _foragingUntil;

            FacingDir facing = PlayerAnimLogic.ComputeFacing(velocity, thresholdSq, _netFacing.Value);
            AnimState state = PlayerAnimLogic.ComputeState(velocity, thresholdSq, foraging);

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
