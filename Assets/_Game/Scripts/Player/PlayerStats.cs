using System;
using DungeonBuilder.Data;
using Unity.Netcode;
using UnityEngine;

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

        private readonly NetworkVariable<float> _hp = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _mana = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _shield = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _stamina = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isDead = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _reviveProgress = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _reviverClientId = new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        public override void OnNetworkSpawn()
        {
            _hp.OnValueChanged += HandleHPChanged;
            _mana.OnValueChanged += HandleManaChanged;
            _isDead.OnValueChanged += HandleDeadStateChangedNetworked;
            _reviveProgress.OnValueChanged += HandleAnyReviveChange;
            _reviverClientId.OnValueChanged += HandleAnyReviveChange;

            if (IsServer)
            {
                _hp.Value = MaxHP;
                _mana.Value = MaxMana;
                _shield.Value = 0f;
                _stamina.Value = 100f;
            }

            OnHPChanged?.Invoke(_hp.Value, MaxHP);
            OnManaChanged?.Invoke(_mana.Value, MaxMana);
            OnDeadStateChanged?.Invoke(_isDead.Value);
            OnReviveStateChanged?.Invoke(_reviveProgress.Value, _reviverClientId.Value);
        }

        public override void OnNetworkDespawn()
        {
            _hp.OnValueChanged -= HandleHPChanged;
            _mana.OnValueChanged -= HandleManaChanged;
            _isDead.OnValueChanged -= HandleDeadStateChangedNetworked;
            _reviveProgress.OnValueChanged -= HandleAnyReviveChange;
            _reviverClientId.OnValueChanged -= HandleAnyReviveChange;
        }

        private void Update()
        {
            // Server tick revive: chỉ chạy khi player này đang chết và có người cứu.
            if (!IsServer) return;
            if (!_isDead.Value) return;
            if (_reviverClientId.Value == ulong.MaxValue) return;

            // Reviver ra khỏi vùng → cancel (reset về 0, player vẫn chết).
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

            if (newValue < previousValue)
            {
                float damageAmount = previousValue - newValue;
                OnPlayerHit?.Invoke(damageAmount, newValue, MaxHP);
            }
        }

        private void HandleManaChanged(float previousValue, float newValue)
        {
            OnManaChanged?.Invoke(newValue, MaxMana);
        }

        private void HandleDeadStateChangedNetworked(bool previousValue, bool newValue)
        {
            OnDeadStateChanged?.Invoke(newValue);
        }

        private void HandleAnyReviveChange<T>(T previousValue, T newValue)
        {
            OnReviveStateChanged?.Invoke(_reviveProgress.Value, _reviverClientId.Value);
        }

        public void TakeDamage(float amount, ulong attackerClientId = 0)
        {
            ApplyDamage(amount);
        }
    }
}