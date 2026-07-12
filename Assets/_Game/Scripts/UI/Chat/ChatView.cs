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
        [SerializeField] private Button _closeButton;

        [Header("Settings")]
        [SerializeField] private float _fadeDuration = 0.2f;

        [Header("Cheat")]
        [Tooltip("Gõ đúng mật mã này trong chat sẽ mở CheatPanel thay vì gửi tin nhắn.")]
        [SerializeField] private string _cheatCode = "/huydeptrai";

        private bool _isPanelVisible;
        private bool _isInputActive;

        private static bool IsNetworkConnected =>
            NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost ||
             NetworkManager.Singleton.IsServer ||
             NetworkManager.Singleton.IsConnectedClient);

        private void Start()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(HidePanel);
            }

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

            // Phím ` (BackQuote): bật/tắt chat panel. Dùng ` vì ký tự này không gõ trong
            // tin nhắn nên toggle được cả khi input field đang focus. Mở là focus input luôn.
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                if (_isPanelVisible)
                {
                    HidePanel();
                }
                else
                {
                    ShowPanelWithInput();
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
        /// Hien panel (khong focus input). Panel giu nguyen cho den khi nguoi choi tu dong
        /// (nut X, phim M/Esc hoac click ra ngoai).
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
                StartCoroutine(FocusInputNextFrame());
            }
        }

        private IEnumerator FocusInputNextFrame()
        {
            // Đợi 1 frame rồi mới focus + xóa text để ký tự của phím toggle (`)
            // không bị TMP_InputField nhận vào làm ký tự đầu tin nhắn.
            yield return null;
            _inputField.text = string.Empty;
            _inputField.ActivateInputField();
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

            // Mật mã cheat: không gửi lên network (giữ bí mật), mở CheatPanel và đóng chat.
            if (!string.IsNullOrEmpty(_cheatCode) &&
                string.Equals(text.Trim(), _cheatCode, System.StringComparison.OrdinalIgnoreCase))
            {
                var cheatPanel = FindFirstObjectByType<DungeonBuilder.UI.Cheat.CheatPanelView>(FindObjectsInactive.Include);
                if (cheatPanel != null)
                {
                    cheatPanel.Show();
                }
                else
                {
                    Debug.LogWarning("[ChatView] Không tìm thấy CheatPanelView trong scene.");
                }

                HidePanel();
                return;
            }

            Presenter?.SubmitMessage(text);
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
    }
}
