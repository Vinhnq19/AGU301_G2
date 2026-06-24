using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

namespace DungeonBuilder.Player
{
    public sealed class PlayerNameplate : NetworkBehaviour
    {
        [SerializeField] private TMP_Text _nameText;

        public NetworkVariable<FixedString32Bytes> PlayerName = new(
            "",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public override void OnNetworkSpawn()
        {
            PlayerName.OnValueChanged += HandleNameChanged;
            UpdateNameText(PlayerName.Value.ToString());
        }

        public override void OnNetworkDespawn()
        {
            PlayerName.OnValueChanged -= HandleNameChanged;
        }

        private void HandleNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
        {
            UpdateNameText(newValue.ToString());
        }

        private void UpdateNameText(string newName)
        {
            if (_nameText != null)
            {
                _nameText.text = newName;
            }
        }

        private void LateUpdate()
        {
            // Billboard effect: Always face the main camera
            if (Camera.main != null)
            {
                transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
