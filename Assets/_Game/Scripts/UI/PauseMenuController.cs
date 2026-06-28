using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

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
                
            // Đóng băng thời gian và âm thanh
            Time.timeScale = 0f;
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
    }
}
