using System.Collections;
using DG.Tweening;
using DungeonBuilder.Chat;
using DungeonBuilder.UI.Base;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonBuilder.UI.Chat
{
    public sealed class ChatView : BaseView<ChatPresenter>
    {
        [Header("References")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private Transform _messageContainer;
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private CanvasGroup _panelCanvasGroup;
        [SerializeField] private RectTransform _panelRectTransform;
        [SerializeField] private ChatMessageItem _messageItemPrefab;

        [Header("Settings")]
        [SerializeField] private float _autoCloseDuration = 5f;
        [SerializeField] private float _fadeDuration = 0.2f;

        private bool _isPanelVisible;
        private bool _isInputActive;
        private Coroutine _autoCloseCoroutine;

        private static bool IsNetworkConnected =>
            NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost ||
             NetworkManager.Singleton.IsServer ||
             NetworkManager.Singleton.IsConnectedClient);

        private void Start()
        {
            HidePanelImmediate();
        }

        private void OnDestroy()
        {
            Presenter?.Dispose();
        }

        private void Update()
        {
            if (!IsNetworkConnected)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!_isInputActive)
                {
                    ShowPanelWithInput();
                }
                else if (_inputField != null && !string.IsNullOrWhiteSpace(_inputField.text))
                {
                    SendCurrentInput();
                }
            }

            if (_isPanelVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                HidePanel();
            }

            if (_isPanelVisible && Input.GetMouseButtonDown(0) && _panelRectTransform != null)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(_panelRectTransform, Input.mousePosition))
                {
                    HidePanel();
                }
            }
        }

        public override void Render()
        {
            if (Presenter == null || _messageContainer == null || _messageItemPrefab == null)
            {
                return;
            }

            foreach (Transform child in _messageContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (ChatMessage msg in Presenter.Messages)
            {
                ChatMessageItem item = Instantiate(_messageItemPrefab, _messageContainer);
                item.Setup(msg.SenderName, msg.Text);
            }

            ScrollToBottom();
        }

        /// <summary>
        /// Goi boi ChatPresenter khi co tin nhan moi den.
        /// Hien panel (khong focus input) va reset timer.
        /// </summary>
        public void OnNewMessageArrived()
        {
            if (!IsNetworkConnected)
            {
                return;
            }

            ShowPanel();
        }

        private void ShowPanelWithInput()
        {
            ShowPanel();

            _isInputActive = true;
            if (_inputField != null)
            {
                _inputField.ActivateInputField();
            }
        }

        private void ShowPanel()
        {
            _isPanelVisible = true;

            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.DOKill();
                _panelCanvasGroup.DOFade(1f, _fadeDuration).SetEase(Ease.OutQuad);
                _panelCanvasGroup.interactable = true;
                _panelCanvasGroup.blocksRaycasts = true;
            }

            ResetAutoCloseTimer();
        }

        private void HidePanel()
        {
            _isPanelVisible = false;
            _isInputActive = false;

            if (_inputField != null)
            {
                _inputField.DeactivateInputField();
                _inputField.text = string.Empty;
            }

            EventSystem.current?.SetSelectedGameObject(null);

            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.DOKill();
                _panelCanvasGroup.DOFade(0f, _fadeDuration).SetEase(Ease.InQuad).OnComplete(() =>
                {
                    if (_panelCanvasGroup != null)
                    {
                        _panelCanvasGroup.interactable = false;
                        _panelCanvasGroup.blocksRaycasts = false;
                    }
                });
            }

            StopAutoCloseTimer();
        }

        private void HidePanelImmediate()
        {
            _isPanelVisible = false;
            _isInputActive = false;

            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = 0f;
                _panelCanvasGroup.interactable = false;
                _panelCanvasGroup.blocksRaycasts = false;
            }
        }

        private void SendCurrentInput()
        {
            if (_inputField == null)
            {
                return;
            }

            string text = _inputField.text;
            _inputField.text = string.Empty;
            Presenter?.SubmitMessage(text);
            ResetAutoCloseTimer();
            _inputField.ActivateInputField();
        }

        private void ScrollToBottom()
        {
            if (_scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.normalizedPosition = new Vector2(0f, 0f);
            }
        }

        private void ResetAutoCloseTimer()
        {
            StopAutoCloseTimer();
            _autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
        }

        private void StopAutoCloseTimer()
        {
            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = null;
            }
        }

        private IEnumerator AutoCloseRoutine()
        {
            yield return new WaitForSeconds(_autoCloseDuration);
            HidePanel();
        }
    }
}
