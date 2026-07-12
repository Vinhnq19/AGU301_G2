using DungeonBuilder.Core.Enums;
using DungeonBuilder.Networking;
using DungeonBuilder.Player;
using DungeonBuilder.Wave;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.UI.Cheat
{
    /// <summary>
    /// Panel cheat cho dev/test, mo bang cach go mat ma trong chat (xem ChatView._cheatCode).
    /// Component nay nam tren GameObject LUON ACTIVE; chi bat/tat _visualRoot (child)
    /// de tranh loi "tu tat chinh minh" (SetActive(false) len GO chua script -> mat event/Update).
    ///
    /// Cac cheat goi API server-authoritative truc tiep nen CHI co tac dung khi la HOST.
    /// Panel tu nhan biet host/client: status pill tren header + disable nut khi la client.
    /// Ket qua moi thao tac hien o feedback label duoi day panel (khong can mo Console).
    ///
    /// UI labels dung tieng Viet KHONG DAU: font TMP mac dinh (LiberationSans SDF) khong co
    /// glyph Latin Extended Additional (a., e^`, ...) — co dau se ra o vuong.
    /// </summary>
    public sealed class CheatPanelView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _addBasicResourcesButton;
        [SerializeField] private Button _addRareResourcesButton;
        [SerializeField] private Button _addCurrencyButton;
        [SerializeField] private Button _miningSkillButton;
        [SerializeField] private Button _forgingSkillButton;
        [SerializeField] private Button _fullHealButton;
        [SerializeField] private Button _reviveButton;
        [SerializeField] private Button _killPlayerButton;
        [SerializeField] private Button _reloadWavesButton;
        [SerializeField] private TMP_InputField _jumpWaveInput;
        [SerializeField] private Button _jumpWaveButton;

        [Header("Status UI")]
        [SerializeField] private Image _statusPill;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private TextMeshProUGUI _waveInfoText;
        [SerializeField] private TextMeshProUGUI _feedbackText;

        [Header("Amount chips")]
        [SerializeField] private Button[] _amountButtons;
        [SerializeField] private int[] _amountValues = { 100, 500, 1000, 5000 };

        [Header("Settings")]
        [SerializeField, Min(1)] private int _resourceAmount = 500;
        [SerializeField] private float _feedbackDuration = 3f;

        private static readonly Color HostColor = new Color32(74, 222, 128, 255);    // xanh la
        private static readonly Color ClientColor = new Color32(251, 146, 60, 255);  // cam
        private static readonly Color FeedbackOk = new Color32(74, 222, 128, 255);
        private static readonly Color FeedbackErr = new Color32(255, 92, 92, 255);
        private static readonly Color ChipSelected = new Color32(124, 108, 255, 255); // accent
        private static readonly Color ChipNormal = new Color32(35, 40, 56, 255);      // button bg

        private static readonly ResourceType[] BasicResources =
        {
            ResourceType.Wood, ResourceType.Stone, ResourceType.Ore, ResourceType.Crystal,
        };

        private static readonly ResourceType[] RareResources =
        {
            ResourceType.Copper, ResourceType.Iron, ResourceType.BlueGems, ResourceType.PurpleGems,
        };

        private static readonly ResourceType[] CurrencyResources =
        {
            ResourceType.Coin, ResourceType.Token,
        };

        private float _feedbackUntil;
        private float _nextStatusRefresh;

        public bool IsVisible => _visualRoot != null && _visualRoot.activeSelf;

        private bool IsHost =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Start()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_addBasicResourcesButton != null) _addBasicResourcesButton.onClick.AddListener(() => AddResources(BasicResources, "co ban"));
            if (_addRareResourcesButton != null) _addRareResourcesButton.onClick.AddListener(() => AddResources(RareResources, "hiem"));
            if (_addCurrencyButton != null) _addCurrencyButton.onClick.AddListener(() => AddResources(CurrencyResources, "tien te"));
            if (_miningSkillButton != null) _miningSkillButton.onClick.AddListener(() => AddSkill(ResourceType.MiningSkill, "Mining"));
            if (_forgingSkillButton != null) _forgingSkillButton.onClick.AddListener(() => AddSkill(ResourceType.ForgingSkill, "Forging"));
            if (_fullHealButton != null) _fullHealButton.onClick.AddListener(FullHealLocalPlayer);
            if (_reviveButton != null) _reviveButton.onClick.AddListener(ReviveLocalPlayer);
            if (_killPlayerButton != null) _killPlayerButton.onClick.AddListener(KillLocalPlayer);
            if (_reloadWavesButton != null) _reloadWavesButton.onClick.AddListener(ReloadWaves);
            if (_jumpWaveButton != null) _jumpWaveButton.onClick.AddListener(JumpToWave);

            WireAmountChips();
            Hide();
        }

        private void Update()
        {
            if (!IsVisible) return;

            // ESC dong panel (tien khi dang test).
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
                return;
            }

            // Feedback tu xoa sau _feedbackDuration.
            if (_feedbackText != null && _feedbackText.text.Length > 0 && Time.unscaledTime >= _feedbackUntil)
            {
                _feedbackText.text = string.Empty;
            }

            // Refresh status/wave dinh ky (re, khong can event).
            if (Time.unscaledTime >= _nextStatusRefresh)
            {
                _nextStatusRefresh = Time.unscaledTime + 0.25f;
                RefreshStatus();
            }
        }

        public void Show()
        {
            if (_visualRoot == null) return;
            _visualRoot.SetActive(true);
            RefreshStatus();
            RefreshAmountChips();
            if (_feedbackText != null) _feedbackText.text = string.Empty;
        }

        public void Hide()
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        // ---------- Status ----------

        /// <summary>Cap nhat pill HOST/CLIENT, wave hien tai va interactable cua cac nut server-only.</summary>
        private void RefreshStatus()
        {
            bool isHost = IsHost;

            if (_statusPill != null) _statusPill.color = isHost ? HostColor : ClientColor;
            if (_statusText != null) _statusText.text = isHost ? "HOST" : "CLIENT";

            // Cheat nao cung server-authoritative -> client thi disable het cho khoi bam nham.
            SetInteractable(_addBasicResourcesButton, isHost);
            SetInteractable(_addRareResourcesButton, isHost);
            SetInteractable(_addCurrencyButton, isHost);
            SetInteractable(_miningSkillButton, isHost);
            SetInteractable(_forgingSkillButton, isHost);
            SetInteractable(_fullHealButton, isHost);
            SetInteractable(_reviveButton, isHost);
            SetInteractable(_killPlayerButton, isHost);
            SetInteractable(_reloadWavesButton, isHost);
            SetInteractable(_jumpWaveButton, isHost);

            if (_waveInfoText != null)
            {
                var waveManager = FindFirstObjectByType<WaveManager>();
                _waveInfoText.text = waveManager != null
                    ? $"Wave hien tai: {waveManager.CurrentWave}"
                    : "Wave hien tai: --";
            }
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null) button.interactable = value;
        }

        private void ShowFeedback(string message, bool ok)
        {
            if (_feedbackText != null)
            {
                _feedbackText.text = message;
                _feedbackText.color = ok ? FeedbackOk : FeedbackErr;
                _feedbackUntil = Time.unscaledTime + _feedbackDuration;
            }

            if (!ok) Debug.LogWarning("[CheatPanel] " + message);
        }

        // ---------- Amount chips ----------

        private void WireAmountChips()
        {
            if (_amountButtons == null) return;

            for (int i = 0; i < _amountButtons.Length; i++)
            {
                if (_amountButtons[i] == null || i >= _amountValues.Length) continue;
                int amount = _amountValues[i]; // capture theo gia tri
                _amountButtons[i].onClick.AddListener(() =>
                {
                    _resourceAmount = amount;
                    RefreshAmountChips();
                });
            }

            RefreshAmountChips();
        }

        /// <summary>Chip dang chon to mau accent, cac chip khac mau nen nut.</summary>
        private void RefreshAmountChips()
        {
            if (_amountButtons == null) return;

            for (int i = 0; i < _amountButtons.Length; i++)
            {
                if (_amountButtons[i] == null || i >= _amountValues.Length) continue;
                var image = _amountButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = _amountValues[i] == _resourceAmount ? ChipSelected : ChipNormal;
                }
            }
        }

        // ---------- Cheats ----------

        private void AddResources(ResourceType[] types, string groupLabel)
        {
            if (!IsHost)
            {
                ShowFeedback("Chi HOST dung duoc cheat nay.", ok: false);
                return;
            }

            var resources = FindFirstObjectByType<SharedResourceManager>();
            if (resources == null)
            {
                ShowFeedback("Khong tim thay SharedResourceManager.", ok: false);
                return;
            }

            foreach (ResourceType type in types)
            {
                resources.TryAdd(type, _resourceAmount);
            }

            ShowFeedback($"+{_resourceAmount} tai nguyen {groupLabel}.", ok: true);
        }

        private void AddSkill(ResourceType skill, string label)
        {
            if (!IsHost)
            {
                ShowFeedback("Chi HOST dung duoc cheat nay.", ok: false);
                return;
            }

            var resources = FindFirstObjectByType<SharedResourceManager>();
            if (resources == null)
            {
                ShowFeedback("Khong tim thay SharedResourceManager.", ok: false);
                return;
            }

            resources.TryAdd(skill, 1);
            ShowFeedback($"+1 {label} Skill (Lv {resources.GetAmount(skill)}).", ok: true);
        }

        private static PlayerStats GetLocalPlayerStats()
        {
            var playerObj = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClient?.PlayerObject
                : null;
            return playerObj != null ? playerObj.GetComponent<PlayerStats>() : null;
        }

        private void FullHealLocalPlayer()
        {
            var stats = GetLocalPlayerStats();
            if (stats == null)
            {
                ShowFeedback("Khong tim thay PlayerStats cua local player.", ok: false);
                return;
            }

            stats.Heal(stats.MaxHP); // server-only: chi co tac dung khi la host
            ShowFeedback("Da hoi day mau.", ok: true);
        }

        /// <summary>Hoi sinh ngay local player neu dang chet (full HP, teleport ve spawn).</summary>
        private void ReviveLocalPlayer()
        {
            var stats = GetLocalPlayerStats();
            if (stats == null)
            {
                ShowFeedback("Khong tim thay PlayerStats cua local player.", ok: false);
                return;
            }

            if (!stats.IsDead)
            {
                ShowFeedback("Player chua chet — khong can hoi sinh.", ok: false);
                return;
            }

            if (stats.ServerForceRevive())
            {
                ShowFeedback("Da hoi sinh (full HP, ve spawn).", ok: true);
            }
            else
            {
                ShowFeedback("Hoi sinh that bai (chi HOST dung duoc).", ok: false);
            }
        }

        private void KillLocalPlayer()
        {
            var stats = GetLocalPlayerStats();
            if (stats == null)
            {
                ShowFeedback("Khong tim thay PlayerStats cua local player.", ok: false);
                return;
            }

            stats.ApplyDamage(float.MaxValue); // server-only: chi co tac dung khi la host
            ShowFeedback("Da tu sat (test respawn).", ok: true);
        }

        /// <summary>
        /// Doc lai StreamingAssets/waves.json (hot reload) — chi host, ap dung tu wave ke tiep.
        /// Xem Docs/WAVE_DATA_PIPELINE_PLAN.md.
        /// </summary>
        private void ReloadWaves()
        {
            if (!IsHost)
            {
                ShowFeedback("Reload Waves chi dung duoc khi la HOST.", ok: false);
                return;
            }

            var waveManager = FindFirstObjectByType<WaveManager>();
            if (waveManager == null)
            {
                ShowFeedback("Khong tim thay WaveManager.", ok: false);
                return;
            }

            waveManager.ReloadWaveData();
            ShowFeedback("Da reload waves.json.", ok: true);
        }

        /// <summary>
        /// Cheat host-only: nhay toi wave X (nhap so vao input). Chi trong Build phase —
        /// wave X spawn khi build ket thuc (bam SKIP tren HUD de vao ngay).
        /// </summary>
        private void JumpToWave()
        {
            if (!IsHost)
            {
                ShowFeedback("Jump to wave chi dung duoc khi la HOST.", ok: false);
                return;
            }

            if (_jumpWaveInput == null || !int.TryParse(_jumpWaveInput.text, out int targetWave) || targetWave < 1)
            {
                ShowFeedback("Nhap so wave hop le (>= 1) truoc.", ok: false);
                return;
            }

            var waveManager = FindFirstObjectByType<WaveManager>();
            if (waveManager == null)
            {
                ShowFeedback("Khong tim thay WaveManager.", ok: false);
                return;
            }

            if (waveManager.ServerJumpToWave(targetWave))
            {
                ShowFeedback($"Se vao wave {targetWave} khi build phase ket thuc (SKIP de vao ngay).", ok: true);
            }
            else
            {
                ShowFeedback($"Khong jump duoc toi wave {targetWave} (chi trong Build phase).", ok: false);
            }
        }
    }
}
