using System.Collections.Generic;
using DungeonBuilder.Core.Debugging;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace DungeonBuilder.Player.Tools
{
    public sealed class ToolController : NetworkBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private MonoBehaviour[] _toolBehaviours;

        private readonly List<ITool> _tools = new();
        private InputReader _inputReader;
        private int _currentIndex;

        [Inject]
        public void Construct(InputReader inputReader)
        {
            _inputReader = inputReader;
        }

        private void Awake()
        {
            BuildToolList();
        }

        public override void OnNetworkSpawn()
        {
            DBLog.Info($"tool.spawn.{NetworkObjectId}", $"ToolController spawned. owner={OwnerClientId}, isOwner={IsOwner}, tools={_tools.Count}.", 0f, this);

            if (!IsOwner || _inputReader == null)
            {
                DBLog.Warning($"tool.no-subscribe.{NetworkObjectId}", $"ToolController did not subscribe input. isOwner={IsOwner}, inputReaderNull={_inputReader == null}.", 2f, this);
                return;
            }

            _inputReader.OnAttackPressed += UseCurrentTool;
            _inputReader.OnAttackCanceled += CancelCurrentTool;
            _inputReader.OnNextToolPressed += SelectNextTool;
            _inputReader.OnPrevToolPressed += SelectPreviousTool;
            _inputReader.OnHotbarPressed += SelectTool;
        }

        public override void OnNetworkDespawn()
        {
            if (_inputReader == null)
            {
                return;
            }

            _inputReader.OnAttackPressed -= UseCurrentTool;
            _inputReader.OnAttackCanceled -= CancelCurrentTool;
            _inputReader.OnNextToolPressed -= SelectNextTool;
            _inputReader.OnPrevToolPressed -= SelectPreviousTool;
            _inputReader.OnHotbarPressed -= SelectTool;
        }

        public void SelectTool(int index)
        {
            if (_tools.Count == 0 || index < 0 || index >= _tools.Count)
            {
                DBLog.Warning($"tool.select.invalid.{NetworkObjectId}", $"SelectTool ignored. index={index}, tools={_tools.Count}.", 1f, this);
                return;
            }

            _tools[_currentIndex].CancelAction();
            _currentIndex = index;
            DBLog.Info($"tool.select.{NetworkObjectId}", $"Selected tool index={_currentIndex}, type={_tools[_currentIndex].ToolType}.", 0.25f, this);
        }

        private void BuildToolList()
        {
            _tools.Clear();

            if (_toolBehaviours != null)
            {
                foreach (MonoBehaviour behaviour in _toolBehaviours)
                {
                    if (behaviour is ITool tool)
                    {
                        _tools.Add(tool);
                    }
                }
            }

            if (_tools.Count > 0)
            {
                return;
            }

            foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is ITool tool)
                {
                    _tools.Add(tool);
                }
            }

            DBLog.Info($"tool.build-list.{GetInstanceID()}", $"Tool list built: count={_tools.Count}.", 1f, this);
        }

        private void UseCurrentTool()
        {
            if (_tools.Count == 0)
            {
                DBLog.Warning($"tool.use.none.{NetworkObjectId}", "Attack pressed but no tools are registered.", 1f, this);
                return;
            }

            Vector3 targetWorldPosition = GetTargetWorldPosition();
            DBLog.Info($"tool.use.{NetworkObjectId}", $"Attack pressed. tool={_tools[_currentIndex].ToolType}, target={targetWorldPosition}.", 0.2f, this);
            _tools[_currentIndex].UseAction(targetWorldPosition);
        }

        private void CancelCurrentTool()
        {
            if (_tools.Count == 0)
            {
                return;
            }

            _tools[_currentIndex].CancelAction();
        }

        private void SelectNextTool()
        {
            if (_tools.Count == 0)
            {
                return;
            }

            SelectTool((_currentIndex + 1) % _tools.Count);
        }

        private void SelectPreviousTool()
        {
            if (_tools.Count == 0)
            {
                return;
            }

            SelectTool((_currentIndex - 1 + _tools.Count) % _tools.Count);
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
