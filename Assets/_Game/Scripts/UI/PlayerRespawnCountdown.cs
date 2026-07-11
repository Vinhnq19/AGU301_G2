using DungeonBuilder.Player;
using TMPro;
using UnityEngine;

namespace DungeonBuilder.UI
{
    /// <summary>
    /// Hiển thị số giây đếm ngược auto-respawn (20, 19, ... 0) phía trước mặt player đang chết.
    /// Billboard về camera giống <see cref="PlayerNameplate"/>. Ẩn khi player sống / không đếm.
    ///
    /// Subscribe <see cref="PlayerStats.OnAutoRespawnCountdownChanged"/> để cập nhật mỗi khi server
    /// (NetworkVariable<float>) thay đổi giá trị countdown.
    /// </summary>
    public sealed class PlayerRespawnCountdown : MonoBehaviour
    {
        [SerializeField] private PlayerStats _stats;
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private GameObject _visualRoot;

        private void Awake()
        {
            if (_stats == null)
            {
                _stats = GetComponentInParent<PlayerStats>();
            }
        }

        private void OnEnable()
        {
            if (_stats == null)
            {
                _stats = GetComponentInParent<PlayerStats>();
            }

            if (_stats != null)
            {
                _stats.OnAutoRespawnCountdownChanged += HandleCountdownChanged;
                // Đồng bộ trạng thái hiện tại ngay khi enable (phòng khi enable sau khi event đã fire).
                HandleCountdownChanged(_stats.AutoRespawnCountdown);
            }
        }

        private void OnDisable()
        {
            if (_stats != null)
            {
                _stats.OnAutoRespawnCountdownChanged -= HandleCountdownChanged;
                _stats = null;
            }
        }

        private void LateUpdate()
        {
            // Billboard effect: luôn xoay về main camera để text không bị nghiêng khi camera xoay.
            if (Camera.main != null)
            {
                transform.rotation = Camera.main.transform.rotation;
            }
        }

        private void HandleCountdownChanged(float secondsRemaining)
        {
            bool show = secondsRemaining > 0f;

            // _visualRoot có thể trỏ vào chính GameObject chứa script này. SetActive(false) lên
            // chính nó sẽ gọi OnDisable → unsubscribe → không bao giờ nhận event để hiện lại.
            // Vì vậy chỉ SetActive khi _visualRoot là object KHÁC; còn lại toggle text renderer.
            if (_visualRoot != null && _visualRoot != gameObject)
            {
                _visualRoot.SetActive(show);
            }

            if (_countdownText != null)
            {
                _countdownText.enabled = show;
                // Làm tròn lên để hiển thị "20" đầy đủ lúc mới chết, "1" ngay trước khi respawn.
                int display = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
                _countdownText.text = display.ToString();
            }
        }
    }
}