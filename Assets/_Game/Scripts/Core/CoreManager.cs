using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Interfaces;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Core
{
    public sealed class CoreManager : NetworkBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int _maxHealth = 100;

        private readonly NetworkVariable<int> _currentHealth = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private EventBus _eventBus;

        public int MaxHealth => _maxHealth;
        public int CurrentHealth => _currentHealth.Value;

        [Inject]
        public void Construct(EventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public override void OnNetworkSpawn()
        {
            _currentHealth.OnValueChanged += HandleHealthChanged;

            if (IsServer)
            {
                _currentHealth.Value = _maxHealth;
                DBLog.Info("core.spawn", $"CoreManager spawned on server. maxHealth={_maxHealth}.", 0f, this);
            }
        }

        public override void OnNetworkDespawn()
        {
            _currentHealth.OnValueChanged -= HandleHealthChanged;
        }

        public void TakeDamage(float amount, ulong attackerClientId = 0)
        {
            if (!IsServer || amount <= 0f)
            {
                return;
            }

            int previous = _currentHealth.Value;
            _currentHealth.Value = Mathf.Max(0, _currentHealth.Value - Mathf.RoundToInt(amount));
            DBLog.Info("core.damage", $"Core took damage. amount={amount}, hp={previous}→{_currentHealth.Value}.", 0.2f, this);
        }

        private void HandleHealthChanged(int previousValue, int newValue)
        {
            _eventBus?.RaiseCoreHealthChanged(newValue);
            
            if (newValue < previousValue)
            {
                if (DungeonBuilder.Audio.AudioManager.Instance != null)
                {
                    DungeonBuilder.Audio.AudioManager.Instance.PlaySFX(DungeonBuilder.Core.Enums.SoundType.SFX_Core, transform.position);
                }
            }
            
            if (newValue <= 0)
                _eventBus?.RaiseGameEnded(false);
        }
    }
}
