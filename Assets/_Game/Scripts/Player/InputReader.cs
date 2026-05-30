using System;
using DungeonBuilder.Core.Debugging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonBuilder.Player
{
    public sealed class InputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private InputActionMap _playerMap;
        private InputAction _move;
        private InputAction _look;
        private InputAction _attack;
        private InputAction _interact;
        private InputAction _dash;
        private InputAction _nextTool;
        private InputAction _prevTool;
        private readonly InputAction[] _hotbar = new InputAction[6];

        public event Action<Vector2> OnMove;
        public event Action<Vector2> OnLook;
        public event Action OnAttackPressed;
        public event Action OnAttackCanceled;
        public event Action OnInteractPressed;
        public event Action OnDashPressed;
        public event Action OnNextToolPressed;
        public event Action OnPrevToolPressed;
        public event Action<int> OnHotbarPressed;

        private void OnEnable()
        {
            BindActions();
            Subscribe();
            _playerMap?.Enable();
            DBLog.Info($"input.enable.{GetInstanceID()}", $"InputReader enabled. assetNull={_inputActions == null}, playerMapNull={_playerMap == null}, attackNull={_attack == null}, interactNull={_interact == null}, nextNull={_nextTool == null}, prevNull={_prevTool == null}.", 1f, this);
        }

        private void OnDisable()
        {
            _playerMap?.Disable();
            Unsubscribe();
        }

        private void BindActions()
        {
            if (_inputActions == null)
            {
                DBLog.Warning($"input.no-asset.{GetInstanceID()}", "InputReader has no InputActionAsset assigned.", 2f, this);
                return;
            }

            _playerMap = _inputActions.FindActionMap("Player", false);
            if (_playerMap == null)
            {
                DBLog.Warning($"input.no-player-map.{GetInstanceID()}", "InputReader could not find Player action map.", 2f, this);
                return;
            }

            _move = FindAction("Move");
            _look = FindAction("Look");
            _attack = FindAction("Attack");
            _interact = FindAction("Interact");
            _dash = FindAction("Dash", "Jump");
            _nextTool = FindAction("NextTool", "Next");
            _prevTool = FindAction("PrevTool", "Previous");

            for (int i = 0; i < _hotbar.Length; i++)
            {
                _hotbar[i] = FindAction($"Hotbar{i + 1}");
            }
        }

        private InputAction FindAction(params string[] names)
        {
            foreach (string actionName in names)
            {
                InputAction action = _playerMap.FindAction(actionName, false);
                if (action != null)
                {
                    return action;
                }
            }

            return null;
        }

        private void Subscribe()
        {
            if (_move != null)
            {
                _move.performed += HandleMove;
                _move.canceled += HandleMove;
            }

            if (_look != null)
            {
                _look.performed += HandleLook;
                _look.canceled += HandleLook;
            }

            if (_attack != null)
            {
                _attack.performed += HandleAttackPerformed;
                _attack.canceled += HandleAttackCanceled;
            }

            if (_interact != null)
            {
                _interact.performed += HandleInteractPerformed;
            }

            if (_dash != null)
            {
                _dash.performed += HandleDashPerformed;
            }

            if (_nextTool != null)
            {
                _nextTool.performed += HandleNextToolPerformed;
            }

            if (_prevTool != null)
            {
                _prevTool.performed += HandlePrevToolPerformed;
            }

            SubscribeHotbar();
        }

        private void Unsubscribe()
        {
            if (_move != null)
            {
                _move.performed -= HandleMove;
                _move.canceled -= HandleMove;
            }

            if (_look != null)
            {
                _look.performed -= HandleLook;
                _look.canceled -= HandleLook;
            }

            if (_attack != null)
            {
                _attack.performed -= HandleAttackPerformed;
                _attack.canceled -= HandleAttackCanceled;
            }

            if (_interact != null)
            {
                _interact.performed -= HandleInteractPerformed;
            }

            if (_dash != null)
            {
                _dash.performed -= HandleDashPerformed;
            }

            if (_nextTool != null)
            {
                _nextTool.performed -= HandleNextToolPerformed;
            }

            if (_prevTool != null)
            {
                _prevTool.performed -= HandlePrevToolPerformed;
            }

            UnsubscribeHotbar();
        }

        private void SubscribeHotbar()
        {
            if (_hotbar[0] != null) _hotbar[0].performed += HandleHotbar1;
            if (_hotbar[1] != null) _hotbar[1].performed += HandleHotbar2;
            if (_hotbar[2] != null) _hotbar[2].performed += HandleHotbar3;
            if (_hotbar[3] != null) _hotbar[3].performed += HandleHotbar4;
            if (_hotbar[4] != null) _hotbar[4].performed += HandleHotbar5;
            if (_hotbar[5] != null) _hotbar[5].performed += HandleHotbar6;
        }

        private void UnsubscribeHotbar()
        {
            if (_hotbar[0] != null) _hotbar[0].performed -= HandleHotbar1;
            if (_hotbar[1] != null) _hotbar[1].performed -= HandleHotbar2;
            if (_hotbar[2] != null) _hotbar[2].performed -= HandleHotbar3;
            if (_hotbar[3] != null) _hotbar[3].performed -= HandleHotbar4;
            if (_hotbar[4] != null) _hotbar[4].performed -= HandleHotbar5;
            if (_hotbar[5] != null) _hotbar[5].performed -= HandleHotbar6;
        }

        private void HandleMove(InputAction.CallbackContext context)
        {
            OnMove?.Invoke(context.ReadValue<Vector2>());
        }

        private void HandleLook(InputAction.CallbackContext context)
        {
            OnLook?.Invoke(context.ReadValue<Vector2>());
        }

        private void HandleAttackPerformed(InputAction.CallbackContext context)
        {
            DBLog.Info($"input.attack.{GetInstanceID()}", "Input Attack performed.", 0.2f, this);
            OnAttackPressed?.Invoke();
        }

        private void HandleAttackCanceled(InputAction.CallbackContext context)
        {
            OnAttackCanceled?.Invoke();
        }

        private void HandleInteractPerformed(InputAction.CallbackContext context)
        {
            DBLog.Info($"input.interact.{GetInstanceID()}", "Input Interact performed.", 0.2f, this);
            OnInteractPressed?.Invoke();
        }

        private void HandleDashPerformed(InputAction.CallbackContext context)
        {
            OnDashPressed?.Invoke();
        }

        private void HandleNextToolPerformed(InputAction.CallbackContext context)
        {
            DBLog.Info($"input.next-tool.{GetInstanceID()}", "Input NextTool performed.", 0.2f, this);
            OnNextToolPressed?.Invoke();
        }

        private void HandlePrevToolPerformed(InputAction.CallbackContext context)
        {
            DBLog.Info($"input.prev-tool.{GetInstanceID()}", "Input PrevTool performed.", 0.2f, this);
            OnPrevToolPressed?.Invoke();
        }

        private void HandleHotbar1(InputAction.CallbackContext context) => OnHotbarPressed?.Invoke(0);
        private void HandleHotbar2(InputAction.CallbackContext context) => OnHotbarPressed?.Invoke(1);
        private void HandleHotbar3(InputAction.CallbackContext context) => OnHotbarPressed?.Invoke(2);
        private void HandleHotbar4(InputAction.CallbackContext context) => OnHotbarPressed?.Invoke(3);
        private void HandleHotbar5(InputAction.CallbackContext context) => OnHotbarPressed?.Invoke(4);
        private void HandleHotbar6(InputAction.CallbackContext context) => OnHotbarPressed?.Invoke(5);
    }
}
