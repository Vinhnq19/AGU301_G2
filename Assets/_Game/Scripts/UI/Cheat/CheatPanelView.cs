using DungeonBuilder.Core.Enums;
using DungeonBuilder.Networking;
using DungeonBuilder.Player;
using DungeonBuilder.Wave;
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
    /// Luu y: cac cheat tai nguyen / mau / tu sat goi API server-authoritative truc tiep
    /// nen chi co tac dung khi minh la HOST. (Skip build phase da co nut SKIP rieng tren HUD.)
    /// </summary>
    public sealed class CheatPanelView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _addBasicResourcesButton;
        [SerializeField] private Button _addRareResourcesButton;
        [SerializeField] private Button _fullHealButton;
        [SerializeField] private Button _killPlayerButton;
        [SerializeField] private Button _reloadWavesButton;
        [SerializeField] private TMPro.TMP_InputField _jumpWaveInput;
        [SerializeField] private Button _jumpWaveButton;

        [Header("Settings")]
        [SerializeField, Min(1)] private int _resourceAmount = 500;

        private static readonly ResourceType[] BasicResources =
        {
            ResourceType.Wood, ResourceType.Stone, ResourceType.Ore, ResourceType.Crystal,
        };

        private static readonly ResourceType[] RareResources =
        {
            ResourceType.Copper, ResourceType.Iron, ResourceType.BlueGems,
            ResourceType.PurpleGems, ResourceType.Coin,
        };

        public bool IsVisible => _visualRoot != null && _visualRoot.activeSelf;

        private void Start()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
            if (_addBasicResourcesButton != null) _addBasicResourcesButton.onClick.AddListener(() => AddResources(BasicResources));
            if (_addRareResourcesButton != null) _addRareResourcesButton.onClick.AddListener(() => AddResources(RareResources));
            if (_fullHealButton != null) _fullHealButton.onClick.AddListener(FullHealLocalPlayer);
            if (_killPlayerButton != null) _killPlayerButton.onClick.AddListener(KillLocalPlayer);
            if (_reloadWavesButton != null) _reloadWavesButton.onClick.AddListener(ReloadWaves);
            if (_jumpWaveButton != null) _jumpWaveButton.onClick.AddListener(JumpToWave);

            Hide();
        }

        public void Show()
        {
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(true);
            }
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

        private void AddResources(ResourceType[] types)
        {
            var resources = FindFirstObjectByType<SharedResourceManager>();
            if (resources == null)
            {
                Debug.LogWarning("[CheatPanel] Khong tim thay SharedResourceManager.");
                return;
            }

            foreach (ResourceType type in types)
            {
                if (!resources.TryAdd(type, _resourceAmount))
                {
                    Debug.LogWarning($"[CheatPanel] TryAdd {type} that bai (chi host dung duoc cheat nay).");
                }
            }
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
                Debug.LogWarning("[CheatPanel] Khong tim thay PlayerStats cua local player.");
                return;
            }

            stats.Heal(stats.MaxHP); // server-only: chi co tac dung khi la host
        }

        /// <summary>
        /// Doc lai StreamingAssets/waves.json (hot reload) — chi host, ap dung tu wave ke tiep.
        /// Xem Docs/WAVE_DATA_PIPELINE_PLAN.md.
        /// </summary>
        private void ReloadWaves()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[CheatPanel] Reload Waves chi dung duoc khi la HOST.");
                return;
            }

            var waveManager = FindFirstObjectByType<WaveManager>();
            if (waveManager == null)
            {
                Debug.LogWarning("[CheatPanel] Khong tim thay WaveManager.");
                return;
            }

            waveManager.ReloadWaveData();
        }

        /// <summary>
        /// Cheat host-only: nhay toi wave X (nhap so vao input). Chi trong Build phase —
        /// wave X spawn khi build ket thuc (bam SKIP tren HUD de vao ngay).
        /// </summary>
        private void JumpToWave()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[CheatPanel] Jump to wave chi dung duoc khi la HOST.");
                return;
            }

            if (_jumpWaveInput == null || !int.TryParse(_jumpWaveInput.text, out int targetWave) || targetWave < 1)
            {
                Debug.LogWarning("[CheatPanel] Nhap so wave hop le (>= 1) truoc khi bam Jump.");
                return;
            }

            var waveManager = FindFirstObjectByType<WaveManager>();
            if (waveManager == null)
            {
                Debug.LogWarning("[CheatPanel] Khong tim thay WaveManager.");
                return;
            }

            waveManager.ServerJumpToWave(targetWave);
        }

        private void KillLocalPlayer()
        {
            var stats = GetLocalPlayerStats();
            if (stats == null)
            {
                Debug.LogWarning("[CheatPanel] Khong tim thay PlayerStats cua local player.");
                return;
            }

            stats.ApplyDamage(float.MaxValue); // server-only: chi co tac dung khi la host
        }
    }
}
