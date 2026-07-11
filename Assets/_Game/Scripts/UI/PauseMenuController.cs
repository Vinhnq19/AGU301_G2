using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEngine.UI;

namespace DungeonBuilder.UI
{
    public class PauseMenuController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Kéo object chứa toàn bộ giao diện Settings Panel vào đây")]
        [SerializeField] private GameObject settingsPanel;

        private bool _isPaused = false;

        private void Awake()
        {
            BindSettingsPanelButtons();

            // Đảm bảo lúc mới vào game thì panel này tắt
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        private void Update()
        {
            // Lắng nghe phím ESC để bật/tắt
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_isPaused)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }
            }
        }

        public void PauseGame()
        {
            _isPaused = true;
            if (settingsPanel != null)
                settingsPanel.SetActive(true);

            // Chỉ đóng băng thời gian khi KHÔNG trong phiên mạng: server dùng Time.deltaTime
            // để đếm ngược wave/auto-respawn cho MỌI người chơi, nên host bấm ESC không được
            // phép làm Time.timeScale = 0 (sẽ treo cả trận cho tất cả client, không chỉ local).
            if (!IsNetworked())
            {
                Time.timeScale = 0f;
            }
            AudioListener.pause = true;
        }

        public void ResumeGame()
        {
            _isPaused = false;
            if (settingsPanel != null)
                settingsPanel.SetActive(false);

            // Trả lại thời gian và âm thanh bình thường
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private static bool IsNetworked()
        {
            var net = NetworkManager.Singleton;
            return net != null && net.IsListening;
        }

        public void ReturnToLobby()
        {
            // Trả lại thời gian và âm thanh trước khi out để không bị đơ/tắt tiếng game ở ván mới
            Time.timeScale = 1f;
            AudioListener.pause = false;
            
            // Tắt kết nối mạng (nếu có)
            var net = NetworkManager.Singleton;
            if (net != null && net.IsListening)
            {
                net.Shutdown();
            }

            // Chuyển về sảnh chờ
            SceneManager.LoadScene("LobbyScene");
        }

        private void BindSettingsPanelButtons()
        {
            if (settingsPanel == null)
                return;

            Button[] buttons = settingsPanel.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button.name == "ResumeBtn")
                {
                    button.onClick.RemoveListener(ResumeGame);
                    button.onClick.AddListener(ResumeGame);
                }
                else if (button.name == "ReturnToLobbyBtn")
                {
                    button.onClick.RemoveListener(ReturnToLobby);
                    button.onClick.AddListener(ReturnToLobby);
                }
            }
        }
    }
}
