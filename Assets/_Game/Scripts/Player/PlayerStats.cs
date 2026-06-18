using System;
using DungeonBuilder.Data;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Player
{
    public sealed class PlayerStats : NetworkBehaviour
    {
        [SerializeField] private PlayerDataSO _data;
        [SerializeField, Min(0f)] private float _defaultManaUseCost = 10f;

        private readonly NetworkVariable<float> _hp = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _mana = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _shield = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _stamina = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action<float, float> OnHPChanged;
        public event Action<float, float> OnManaChanged;

        public float MaxHP => _data != null ? _data.maxHP : 100f;
        public float MaxMana => _data != null ? _data.maxMana : 100f;

        public override void OnNetworkSpawn()
        {
            _hp.OnValueChanged += HandleHPChanged;
            _mana.OnValueChanged += HandleManaChanged;

            if (IsServer)
            {
                _hp.Value = MaxHP;
                _mana.Value = MaxMana;
                _shield.Value = 0f;
                _stamina.Value = 100f;
            }

            OnHPChanged?.Invoke(_hp.Value, MaxHP);
            OnManaChanged?.Invoke(_mana.Value, MaxMana);
        }

        public override void OnNetworkDespawn()
        {
            _hp.OnValueChanged -= HandleHPChanged;
            _mana.OnValueChanged -= HandleManaChanged;
        }

        public void ApplyDamage(float amount)
        {
            if (!IsServer || amount <= 0f)
            {
                return;
            }

            float shieldAbsorb = Mathf.Min(_shield.Value, amount);
            _shield.Value -= shieldAbsorb;
            _hp.Value = Mathf.Max(0f, _hp.Value - (amount - shieldAbsorb));
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
        }

        private void HandleManaChanged(float previousValue, float newValue)
        {
            OnManaChanged?.Invoke(newValue, MaxMana);
        }
    }
}
