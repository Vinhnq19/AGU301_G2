using DungeonBuilder.Building;
using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace DungeonBuilder.Player.Tools
{
    /// <summary>
    /// Routes the attack input to the right action by click context — no manual
    /// tool swapping. Click on a buildable grid cell -> BuilderTool (open the tower
    /// panel); anything else -> HarvestToolBase (harvest a node / free foraging).
    /// </summary>
    public sealed class ToolController : NetworkBehaviour
    {
        [SerializeField] private Camera _camera;

        private InputReader _inputReader;
        private BuilderTool _builderTool;
        private HarvestToolBase _harvestTool;
        private GridManager _grid;

        [Inject]
        public void Construct(InputReader inputReader)
        {
            _inputReader = inputReader;
        }

        private void Awake()
        {
            _builderTool = GetComponent<BuilderTool>();
            _harvestTool = GetComponent<HarvestToolBase>();
        }

        public override void OnNetworkSpawn()
        {
            DBLog.Info($"tool.spawn.{NetworkObjectId}", $"ToolController spawned. owner={OwnerClientId}, isOwner={IsOwner}.", 0f, this);

            if (!IsOwner || _inputReader == null)
            {
                DBLog.Warning($"tool.no-subscribe.{NetworkObjectId}", $"ToolController did not subscribe input. isOwner={IsOwner}, inputReaderNull={_inputReader == null}.", 2f, this);
                return;
            }

            _inputReader.OnAttackPressed += UseAt;
            _inputReader.OnAttackCanceled += CancelUse;
        }

        public override void OnNetworkDespawn()
        {
            if (_inputReader == null)
            {
                return;
            }

            _inputReader.OnAttackPressed -= UseAt;
            _inputReader.OnAttackCanceled -= CancelUse;
        }

        private void UseAt()
        {
            Vector3 targetWorldPosition = GetTargetWorldPosition();

            if (_grid == null)
            {
                _grid = FindFirstObjectByType<GridManager>();
            }

            // Build wins when the click lands on a valid, empty buildable cell;
            // otherwise harvest/forage (cell occupied by a tower is not valid placement,
            // and tower clicks are handled separately by TowerPresenter).
            bool build = _grid != null
                && _builderTool != null
                && _grid.IsValidPlacement(_grid.WorldToGrid(targetWorldPosition));

            DBLog.Info($"tool.use.{NetworkObjectId}", $"Attack routed to {(build ? "build" : "harvest")}. target={targetWorldPosition}.", 0.2f, this);

            if (build)
            {
                _builderTool.UseAction(targetWorldPosition);
            }
            else
            {
                _harvestTool?.UseAction(targetWorldPosition);
            }
        }

        private void CancelUse()
        {
            // BuilderTool.CancelAction opens/closes the tower panel on mouse-up;
            // HarvestToolBase.CancelAction is a no-op.
            _builderTool?.CancelAction();
        }

        private Vector3 GetTargetWorldPosition()
        {
            Camera activeCamera = _camera != null ? _camera : Camera.main;
            if (activeCamera == null || Mouse.current == null)
            {
                return transform.position;
            }

            Vector3 screenPosition = Mouse.current.position.ReadValue();
            screenPosition.z = Mathf.Abs(activeCamera.transform.position.z - transform.position.z);
            return activeCamera.ScreenToWorldPoint(screenPosition);
        }
    }
}
