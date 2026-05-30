using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Player.Tools
{
    public sealed class WeaponTool : NetworkBehaviour, ITool
    {
        [SerializeField] private LayerMask _targetMask = ~0;
        [SerializeField, Min(0.01f)] private float _targetRadius = 0.35f;
        [SerializeField, Min(0.1f)] private float _serverAttackRange = 2f;
        [SerializeField, Min(0f)] private float _serverDamage = 15f;

        public ToolType ToolType => DungeonBuilder.Core.Enums.ToolType.Weapon;

        public void UseAction(Vector3 targetPosition)
        {
            if (!IsOwner)
            {
                return;
            }

            Collider2D collider = Physics2D.OverlapCircle(targetPosition, _targetRadius, _targetMask);
            NetworkObject target = collider != null ? collider.GetComponentInParent<NetworkObject>() : null;
            if (target != null)
            {
                AttackEnemyServerRpc(target.NetworkObjectId);
            }
        }

        public void CancelAction()
        {
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void AttackEnemyServerRpc(ulong enemyNetworkObjectId, RpcParams rpcParams = default)
        {
            if (!TryGetSenderPlayer(rpcParams.Receive.SenderClientId, out NetworkObject playerObject))
            {
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyNetworkObjectId, out NetworkObject targetObject))
            {
                return;
            }

            IDamageable damageable = targetObject.GetComponent<IDamageable>();
            if (damageable == null)
            {
                return;
            }

            float distance = Vector3.Distance(playerObject.transform.position, targetObject.transform.position);
            if (distance > _serverAttackRange)
            {
                return;
            }

            damageable.TakeDamage(_serverDamage, rpcParams.Receive.SenderClientId);
        }

        private bool TryGetSenderPlayer(ulong senderClientId, out NetworkObject playerObject)
        {
            playerObject = null;
            if (NetworkManager.Singleton == null
                || !NetworkManager.Singleton.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client)
                || client.PlayerObject == null)
            {
                return false;
            }

            playerObject = client.PlayerObject;
            return true;
        }
    }
}
