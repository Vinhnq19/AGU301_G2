using DungeonBuilder.Player;
using TMPro;
using UnityEngine;

namespace DungeonBuilder.UI
{
    /// <summary>
    /// Panel màn hình (screen-space) hiển thị to, rõ khi LOCAL player đang chết và đợi auto-respawn.
    /// Khác với <see cref="PlayerRespawnCountdown"/> (world-space, mọi client đều thấy trên đầu xác chết),
    /// panel này chỉ được bind cho player thuộc quyền sở hữu của client hiện tại, xem
    /// <see cref="DungeonBuilder.Gameplay.Camera.LocalPlayerRespawnPanelBinder"/>.
    /// </summary>
    public sealed class RespawnPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _countdownText;

        private PlayerStats _stats;

        private void Awake()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        public void Bind(PlayerStats stats)
        {
            if (stats == null || _stats == stats)
            {
                return;
            }

            Unbind();

            _stats = stats;
            _stats.OnDeadStateChanged += HandleDeadStateChanged;
            _stats.OnAutoRespawnCountdownChanged += HandleCountdownChanged;

            HandleDeadStateChanged(_stats.IsDead);
            HandleCountdownChanged(_stats.AutoRespawnCountdown);
        }

        public void Unbind()
        {
            if (_stats == null)
            {
                return;
            }

            _stats.OnDeadStateChanged -= HandleDeadStateChanged;
            _stats.OnAutoRespawnCountdownChanged -= HandleCountdownChanged;
            _stats = null;

            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleDeadStateChanged(bool isDead)
        {
            if (_root != null)
            {
                _root.SetActive(isDead);
            }
        }

        private void HandleCountdownChanged(float secondsRemaining)
        {
            if (_countdownText == null)
            {
                return;
            }

            int display = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
            _countdownText.text = $"{display}s";
        }
    }
}
