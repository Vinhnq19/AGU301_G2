using System;
using System.Collections.Generic;
using DungeonBuilder.Data;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Serialization;

namespace DungeonBuilder.Player
{
    public sealed class PlayerStats : NetworkBehaviour, DungeonBuilder.Core.Interfaces.IDamageable
    {
        [SerializeField] private PlayerDataSO _data;
        [SerializeField, Min(0f)] private float _defaultManaUseCost = 10f;

        [Header("Revive")]
        [SerializeField, Min(0.1f)] private float _reviveDuration = 3f;
        [SerializeField, Range(0.05f, 1f)] private float _reviveHealFraction = 0.5f;
        [SerializeField, Min(0.1f)] private float _reviveMaxDistance = 2.5f;

        [Header("Auto Respawn")]
        [Tooltip("Thời gian hồi sinh cơ bản (giây). 0 = tắt auto-respawn hoàn toàn.")]
        [FormerlySerializedAs("_autoRespawnDuration")]
        [SerializeField, Min(0f)] private float _baseRespawnTime = 20f;
        [Tooltip("Cộng thêm theo wave: (wave hiện tại - 1) × giá trị này.")]
        [SerializeField, Min(0f)] private float _respawnTimePerWave = 0.5f;
        [Tooltip("Cộng thêm theo số lần đã chết trong trận (tính cả lần này): deathCount × giá trị này.")]
        [SerializeField, Min(0f)] private float _respawnTimePerDeath = 2f;
        [SerializeField, Min(0f)] private float _minRespawnTime = 3f;
        [SerializeField, Min(1f)] private float _maxRespawnTime = 60f;
        [Tooltip("Bật tăng thời gian hồi sinh theo wave hiện tại.")]
        [SerializeField] private bool _scaleRespawnByWave = true;
        [Tooltip("Bật tăng thời gian hồi sinh theo số lần chết trong trận.")]
        [SerializeField] private bool _scaleRespawnByDeaths = true;

        [Header("Post-Revive")]
        [Tooltip("Bất tử trong N giây ngay sau khi sống lại (revive hoặc auto-respawn) để không chết ngay lập tức.")]
        [SerializeField, Min(0f)] private float _postReviveInvulnerability = 2f;

        private readonly NetworkVariable<float> _hp = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _mana = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _shield = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _stamina = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _reviveProgress = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _reviverClientId = new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        /// <summary>Seconds còn lại trước khi auto-respawn tại điểm spawn ban đầu. 0 = không đếm (đang sống / vừa respawn / bị disable).</summary>
        private readonly NetworkVariable<float> _autoRespawnCountdown = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Vị trí spawn ban đầu của player (capture tại OnNetworkSpawn phía server). Dùng cho auto-respawn.</summary>
        private Vector3 _initialSpawnPosition;

        /// <summary>Server-only: số lần đã chết trong trận này. Dùng cộng thời gian hồi sinh.</summary>
        private int _deathCount;

        /// <summary>Server-only: bất tử tới thời điểm này (Time.time) sau khi vừa hồi sinh.</summary>
        private float _invulnerableUntil;

        /// <summary>Các collider non-trigger cache tại Awake — chuyển sang trigger khi chết để xác không chặn đường.</summary>
        private Collider2D[] _solidColliders;

        private DungeonBuilder.Wave.WaveManager _waveManager;

        public event Action<float, float> OnHPChanged;
        public event Action<float, float> OnManaChanged;

        /// <summary>
        /// Bắn local trên TỪNG client khi player này vừa bị trúng đòn (HP giảm).
        /// </summary>
        public event Action<float, float, float> OnPlayerHit;

        /// <summary>Bắn local khi trạng thái chết thay đổi (true = vừa chết, false = vừa hồi sinh).</summary>
        public event Action<bool> OnDeadStateChanged;

        /// <summary>
        /// Bắn local khi revive progress hoặc reviver thay đổi. Arg: (progress 0..1, reviverClientId hoặc ulong.MaxValue).
        /// Dùng cho UI progress bar + reviver biết khi nào mình bị server cancel.
        /// </summary>
        public event Action<float, ulong> OnReviveStateChanged;

        /// <summary>
        /// Bắn local khi auto-respawn countdown thay đổi. Arg: seconds còn lại (>0 = đang đếm, 0 = không đếm).
        /// UI countdown subscribe event này để hiện "20", "19", ... trước mặt player đang chết.
        /// </summary>
        public event Action<float> OnAutoRespawnCountdownChanged;

        public float MaxHP => _data != null ? _data.maxHP : 100f;
        public float MaxMana => _data != null ? _data.maxMana : 100f;

        public float CurrentHP => _hp.Value;
        public float CurrentMana => _mana.Value;
        public bool IsDead => _isDead.Value;
        public float ReviveProgress => _reviveProgress.Value;
        public ulong ReviverClientId => _reviverClientId.Value;

        public float ReviveDuration => _reviveDuration;
        public float ReviveHealFraction => _reviveHealFraction;
        public float ReviveMaxDistance => _reviveMaxDistance;

        public float AutoRespawnCountdown => _autoRespawnCountdown.Value;
        public Vector3 InitialSpawnPosition => _initialSpawnPosition;

        private void Awake()
        {
            // Cache các collider đặc (non-trigger) để chuyển đổi khi chết/hồi sinh.
            // Collider vốn là trigger (vd vùng hút tài nguyên) giữ nguyên, không đụng.
            var all = GetComponentsInChildren<Collider2D>(true);
            var solids = new List<Collider2D>(all.Length);
            foreach (Collider2D c in all)
            {
                if (c != null && !c.isTrigger)
                {
                    solids.Add(c);
                }
            }
            _solidColliders = solids.ToArray();
        }

        public override void OnNetworkSpawn()
        {
            _hp.OnValueChanged += HandleHPChanged;
            _mana.OnValueChanged += HandleManaChanged;
            _isDead.OnValueChanged += HandleDeadStateChangedNetworked;
            _reviveProgress.OnValueChanged += HandleAnyReviveChange;
            _reviverClientId.OnValueChanged += HandleAnyReviveChange;
            _autoRespawnCountdown.OnValueChanged += HandleAutoRespawnCountdownChanged;

            if (IsServer)
            {
                // Capture vị trí spawn ban đầu (sau khi InstantiateAndSpawn set transform.position) để auto-respawn về đây.
                _initialSpawnPosition = transform.position;

                _hp.Value = MaxHP;
                _mana.Value = MaxMana;
                _shield.Value = 0f;
                _stamina.Value = 100f;
            }

            // Đồng bộ trạng thái collider theo trạng thái chết hiện tại (late-join thấy đúng xác).
            ApplyCorpsePhysics(_isDead.Value);

            OnHPChanged?.Invoke(_hp.Value, MaxHP);
            OnManaChanged?.Invoke(_mana.Value, MaxMana);
            OnDeadStateChanged?.Invoke(_isDead.Value);
            OnReviveStateChanged?.Invoke(_reviveProgress.Value, _reviverClientId.Value);
            OnAutoRespawnCountdownChanged?.Invoke(_autoRespawnCountdown.Value);
        }

        public override void OnNetworkDespawn()
        {
            _hp.OnValueChanged -= HandleHPChanged;
            _mana.OnValueChanged -= HandleManaChanged;
            _isDead.OnValueChanged -= HandleDeadStateChangedNetworked;
            _reviveProgress.OnValueChanged -= HandleAnyReviveChange;
            _reviverClientId.OnValueChanged -= HandleAnyReviveChange;
            _autoRespawnCountdown.OnValueChanged -= HandleAutoRespawnCountdownChanged;
        }

        private void Update()
        {
            // Server tick revive + auto-respawn: chỉ chạy khi player này đang chết.
            if (!IsServer) return;
            if (!_isDead.Value) return;

            // Auto-respawn countdown: nếu đếm về 0 và không có ai cứu (hoặc duration=0 thì tắt) → respawn.
            // Lưu ý: countdown chạy SONG SONG với revive. Nếu có ai cứu xong trước khi countdown hết,
            // CompleteRevive() sẽ set countdown = 0 → UI ẩn, player hồi sinh ở chỗ chết với HP = _reviveHealFraction.
            if (_baseRespawnTime > 0f && _autoRespawnCountdown.Value > 0f)
            {
                float newCountdown = _autoRespawnCountdown.Value - Time.deltaTime;
                if (newCountdown <= 0f)
                {
                    _autoRespawnCountdown.Value = 0f;
                    ServerAutoRespawn();
                    return;
                }

                _autoRespawnCountdown.Value = newCountdown;
                // Không return: nếu vẫn đang có reviver thì vẫn tick progress bên dưới.
            }

            // Không có ai đang cứu → chỉ chạy auto-respawn, không tick revive.
            if (_reviverClientId.Value == ulong.MaxValue) return;

            // Reviver ra khỏi vùng → cancel (reset về 0, player vẫn chết, auto-respawn vẫn đếm tiếp).
            if (!IsReviverInRange(_reviverClientId.Value))
            {
                CancelReviveState();
                return;
            }

            // Tick progress.
            float newProgress = Mathf.Clamp01(_reviveProgress.Value + Time.deltaTime / _reviveDuration);
            _reviveProgress.Value = newProgress;

            if (newProgress >= 1f)
            {
                CompleteRevive();
            }
        }

        public void ApplyDamage(float amount)
        {
            if (!IsServer || amount <= 0f)
            {
                return;
            }

            if (_isDead.Value)
            {
                // Đã chết rồi thì không nhận thêm damage.
                return;
            }

            if (Time.time < _invulnerableUntil)
            {
                // Vừa hồi sinh — còn trong khoảng bất tử ngắn.
                return;
            }

            float shieldAbsorb = Mathf.Min(_shield.Value, amount);
            _shield.Value -= shieldAbsorb;
            float newHP = Mathf.Max(0f, _hp.Value - (amount - shieldAbsorb));
            _hp.Value = newHP;

            if (newHP <= 0f && !_isDead.Value)
            {
                EnterDeadState();
            }
        }

        public void Heal(float amount)
        {
            if (!IsServer || amount <= 0f) return;
            if (_isDead.Value) return;
            _hp.Value = Mathf.Min(MaxHP, _hp.Value + amount);
        }

        /// <summary>
        /// Server: bắt đầu revive bởi clientId. Trả về true nếu apply được.
        /// </summary>
        public bool ServerStartRevive(ulong reviverClientId)
        {
            if (!IsServer || !_isDead.Value) return false;
            if (reviverClientId == OwnerClientId) return false; // không tự cứu mình
            if (_reviverClientId.Value != ulong.MaxValue) return false; // đã có người đang cứu — không cho cướp slot / reset progress
            _reviverClientId.Value = reviverClientId;
            _reviveProgress.Value = 0f;
            return true;
        }

        /// <summary>
        /// Server: yêu cầu cancel revive từ clientId. Chỉ reviver hiện tại hoặc chính chủ nhân player mới được cancel.
        /// </summary>
        public bool ServerCancelRevive(ulong requesterClientId)
        {
            if (!IsServer) return false;
            if (_reviverClientId.Value == ulong.MaxValue) return false;
            if (requesterClientId != _reviverClientId.Value && requesterClientId != OwnerClientId) return false;
            CancelReviveState();
            return true;
        }

        private void EnterDeadState()
        {
            _hp.Value = 0f;
            _isDead.Value = true;
            _reviveProgress.Value = 0f;
            _reviverClientId.Value = ulong.MaxValue;
            _deathCount++;
            // Bắt đầu đếm auto-respawn (nếu enabled). UI sẽ hiện số giây còn lại.
            _autoRespawnCountdown.Value = ComputeRespawnTime();
        }

        /// <summary>
        /// RespawnTime = Base + WaveBonus + DeathCountBonus, clamp [min, max].
        /// WaveBonus = (wave hiện tại - 1) × perWave; DeathCountBonus = số lần chết (gồm lần này) × perDeath.
        /// Trả 0 khi auto-respawn bị tắt (base = 0).
        /// </summary>
        private float ComputeRespawnTime()
        {
            if (_baseRespawnTime <= 0f)
            {
                return 0f;
            }

            float time = _baseRespawnTime;

            if (_scaleRespawnByWave)
            {
                if (_waveManager == null)
                {
                    _waveManager = FindFirstObjectByType<DungeonBuilder.Wave.WaveManager>();
                }
                int wave = _waveManager != null ? _waveManager.CurrentWave : 0;
                time += Mathf.Max(0, wave - 1) * _respawnTimePerWave;
            }

            if (_scaleRespawnByDeaths)
            {
                time += _deathCount * _respawnTimePerDeath;
            }

            return Mathf.Clamp(time, _minRespawnTime, _maxRespawnTime);
        }

        private void CancelReviveState()
        {
            _reviverClientId.Value = ulong.MaxValue;
            _reviveProgress.Value = 0f;
        }

        private void CompleteRevive()
        {
            _hp.Value = Mathf.Min(MaxHP, MaxHP * _reviveHealFraction);
            _isDead.Value = false;
            CancelReviveState();
            // Revive xong (chưa hết countdown): hủy auto-respawn, ở lại chỗ chết với HP = _reviveHealFraction.
            _autoRespawnCountdown.Value = 0f;
            _invulnerableUntil = Time.time + _postReviveInvulnerability;
        }

        /// <summary>
        /// Server-only (cheat/debug — CheatPanel): hồi sinh NGAY LẬP TỨC nếu đang chết —
        /// full HP, teleport về spawn (dùng lại flow auto-respawn). False nếu không phải
        /// server hoặc player chưa chết.
        /// </summary>
        public bool ServerForceRevive()
        {
            if (!IsServer || !_isDead.Value) return false;
            ServerAutoRespawn();
            return true;
        }

        /// <summary>
        /// Server: auto-respawn tại điểm spawn ban đầu khi countdown về 0. Teleport qua owner client
        /// (vì ClientNetworkTransform là owner-authoritative, server không gọi Teleport trực tiếp được).
        /// </summary>
        private void ServerAutoRespawn()
        {
            // Set state TRƯỚC khi teleport để UI/health bar/movement lock cập nhật ngay frame này.
            _hp.Value = MaxHP;
            _isDead.Value = false;
            _autoRespawnCountdown.Value = 0f;
            CancelReviveState();
            _invulnerableUntil = Time.time + _postReviveInvulnerability;

            // Teleport về điểm spawn ban đầu. Gửi RPC cho owner để authority side gọi Teleport
            // (ClientNetworkTransform chỉ cho phép authority = owner thay đổi transform).
            RequestOwnerTeleportRpc(_initialSpawnPosition);
        }

        [Rpc(SendTo.Owner)]
        private void RequestOwnerTeleportRpc(Vector3 targetPosition)
        {
            // Owner client là authority của ClientNetworkTransform → gọi Teleport ở đây để bypass interpolation
            // và replicate vị trí mới cho mọi client khác.
            var netTransform = GetComponent<NetworkTransform>();
            if (netTransform != null)
            {
                netTransform.Teleport(targetPosition, transform.rotation, transform.localScale);
            }
            else
            {
                // Fallback nếu thiếu NetworkTransform (không nên xảy ra): set trực tiếp.
                transform.position = targetPosition;
            }
        }

        private bool IsReviverInRange(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return false;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                return false;
            }

            var reviverObj = client.PlayerObject;
            if (reviverObj == null) return false;

            var reviverStats = reviverObj.GetComponent<PlayerStats>();
            if (reviverStats != null && reviverStats.IsDead)
            {
                // Reviver chết giữa chừng → cancel.
                return false;
            }

            float dist = Vector2.Distance(transform.position, reviverObj.transform.position);
            return dist <= _reviveMaxDistance;
        }

        [Rpc(SendTo.Server)]
        public void RequestUseManaServerRpc()
        {
            if (_mana.Value < _defaultManaUseCost)
            {
                return;
            }

            _mana.Value -= _defaultManaUseCost;
        }

        private void HandleHPChanged(float previousValue, float newValue)
        {
            OnHPChanged?.Invoke(newValue, MaxHP);

            if (newValue < previousValue) // Took damage
            {
                float damageAmount = previousValue - newValue;
                OnPlayerHit?.Invoke(damageAmount, newValue, MaxHP);
                if (DungeonBuilder.Audio.AudioManager.Instance != null)
                {
                    if (newValue <= 0f)
                    {
                        DungeonBuilder.Audio.AudioManager.Instance.PlaySFX(DungeonBuilder.Core.Enums.SoundType.SFX_Hero_Death, transform.position);
                    }
                    else
                    {
                        DungeonBuilder.Audio.AudioManager.Instance.PlaySFX(DungeonBuilder.Core.Enums.SoundType.SFX_Hero_Hurt, transform.position);
                    }
                }
            }
        }

        private void HandleManaChanged(float previousValue, float newValue)
        {
            OnManaChanged?.Invoke(newValue, MaxMana);
        }

        private void HandleDeadStateChangedNetworked(bool previousValue, bool newValue)
        {
            ApplyCorpsePhysics(newValue);
            OnDeadStateChanged?.Invoke(newValue);
        }

        /// <summary>
        /// Chạy trên MỌI peer khi trạng thái chết đổi: xác chết chuyển collider đặc sang trigger
        /// để không chặn đường (enemy lẫn đồng minh); hồi sinh thì trả lại như cũ.
        /// Trigger vẫn nhận Physics2D.OverlapPoint → click chuột để revive vẫn hoạt động bình thường.
        /// </summary>
        private void ApplyCorpsePhysics(bool isDead)
        {
            if (_solidColliders == null)
            {
                return;
            }

            foreach (Collider2D collider in _solidColliders)
            {
                if (collider != null)
                {
                    collider.isTrigger = isDead;
                }
            }
        }

        private void HandleAnyReviveChange<T>(T previousValue, T newValue)
        {
            OnReviveStateChanged?.Invoke(_reviveProgress.Value, _reviverClientId.Value);
        }

        private void HandleAutoRespawnCountdownChanged(float previousValue, float newValue)
        {
            OnAutoRespawnCountdownChanged?.Invoke(newValue);
        }

        public void TakeDamage(float amount, ulong attackerClientId = 0)
        {
            ApplyDamage(amount);
        }
    }
}