using UnityEngine;
using UnityEngine.SceneManagement;

namespace DungeonBuilder.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene Settings")]
        [Tooltip("Tên chính xác của Scene Lobby trong Build Settings")]
        [SerializeField] private string lobbySceneName = "LobbyScene";

        /// <summary>
        /// Gọi hàm này khi bấm nút START
        /// </summary>
        public void OnStartClicked()
        {
            Debug.Log("Chuyển sang Lobby Scene...");
            SceneManager.LoadScene(lobbySceneName);
        }

        /// <summary>
        /// Gọi hàm này khi bấm nút QUIT
        /// </summary>
        public void OnQuitClicked()
        {
            Debug.Log("Thoát Game!");
            
            // Lệnh thoát game khi đã build ra file .exe
            Application.Quit();

            // Dành cho lúc đang test trong Unity Editor
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
