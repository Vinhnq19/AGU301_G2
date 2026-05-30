using System;
using Cysharp.Threading.Tasks;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Data;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.Player;
using DungeonBuilder.Player.Tools;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Harvesting
{
    public sealed class HarvestableNode : NetworkBehaviour, IHarvestable, IDamageable
    {
        [SerializeField] private ResourceNodeDataSO _data;
        [SerializeField] private NetworkObject _resourceDropPrefab;
        [SerializeField, Min(0.1f)] private float _serverInteractionRange = 2f;
        [SerializeField] private Transform _visual;
        [SerializeField] private Collider2D[] _colliders;

        private readonly NetworkVariable<int> _hitsRemaining = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _isDepleted = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private INetworkPool _pool;

        public bool IsDepletable => true;

        [Inject]
        public void Construct(INetworkPool pool)
        {
            _pool = pool;
        }

        public override void OnNetworkSpawn()
        {
            _isDepleted.OnValueChanged += HandleDepletedChanged;

            if (IsServer)
            {
                ResetNode();
            }

            SetNodeActive(!_isDepleted.Value);
            DBLog.Info($"node.spawn.{NetworkObjectId}", $"HarvestableNode spawned. type={_data?.resourceType}, hits={_hitsRemaining.Value}, server={IsServer}.", 0f, this);
        }

        public override void OnNetworkDespawn()
        {
            _isDepleted.OnValueChanged -= HandleDepletedChanged;
        }

        public void OnInteract(PlayerController interactor)
        {
            if (IsServer && interactor != null && IsPlayerInRange(interactor.NetworkObject))
            {
                HarvestOnce();
            }
        }

        public void TakeDamageFrom(ITool tool)
        {
            if (!IsServer)
            {
                return;
            }

            HarvestOnce();
        }

        public void TakeDamage(float amount, ulong attackerClientId = 0)
        {
            if (!IsServer)
            {
                return;
            }

            HarvestOnce();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void InteractWithNodeServerRpc(RpcParams rpcParams = default)
        {
            if (!TryGetSenderPlayer(rpcParams.Receive.SenderClientId, out NetworkObject playerObject))
            {
                return;
            }

            if (!IsPlayerInRange(playerObject))
            {
                return;
            }

            HarvestOnce();
        }

        private void HarvestOnce()
        {
            ResolvePool();

            if (_data == null || _resourceDropPrefab == null || _pool == null || _isDepleted.Value)
            {
                DBLog.Warning($"node.harvest.blocked.{NetworkObjectId}", $"Harvest blocked. dataNull={_data == null}, dropPrefabNull={_resourceDropPrefab == null}, poolNull={_pool == null}, depleted={_isDepleted.Value}.", 0.5f, this);
                return;
            }

            if (_hitsRemaining.Value <= 0)
            {
                _hitsRemaining.Value = Mathf.Max(1, _data.hitsToBreak);
            }

            _hitsRemaining.Value = Mathf.Max(0, _hitsRemaining.Value - 1);
            DBLog.Info($"node.harvest.{NetworkObjectId}", $"Harvested node. type={_data.resourceType}, hitsRemaining={_hitsRemaining.Value}, amount={_data.amountPerHit}.", 0.2f, this);
            SpawnResourceDrop();

            if (_hitsRemaining.Value <= 0)
            {
                _isDepleted.Value = true;
                DBLog.Info($"node.depleted.{NetworkObjectId}", $"Node depleted. type={_data.resourceType}, respawn={_data.respawnTime:0.00}s.", 0f, this);
                StartRespawnAsync().Forget();
            }
        }

        private void SpawnResourceDrop()
        {
            NetworkObject dropObject = _pool.Get(_resourceDropPrefab, transform.position, Quaternion.identity);
            if (dropObject == null)
            {
                DBLog.Warning($"node.drop.null.{NetworkObjectId}", $"Resource drop spawn failed. type={_data.resourceType}.", 0.5f, this);
                return;
            }

            ResourceDrop drop = dropObject.GetComponent<ResourceDrop>();
            drop?.Configure(_data.resourceType, _data.amountPerHit);
            dropObject.Spawn();
            DBLog.Info($"node.drop.spawn.{NetworkObjectId}", $"Spawned resource drop. dropId={dropObject.NetworkObjectId}, type={_data.resourceType}, amount={_data.amountPerHit}.", 0.2f, dropObject);
        }

        private async UniTaskVoid StartRespawnAsync()
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_data.respawnTime), cancellationToken: destroyCancellationToken);
                if (IsServer)
                {
                    ResetNode();
                    DBLog.Info($"node.respawn.{NetworkObjectId}", $"Node respawned. type={_data.resourceType}, hits={_hitsRemaining.Value}.", 0f, this);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ResetNode()
        {
            if (_data == null)
            {
                return;
            }

            _hitsRemaining.Value = Mathf.Max(1, _data.hitsToBreak);
            _isDepleted.Value = false;
        }

        private void HandleDepletedChanged(bool previousValue, bool newValue)
        {
            SetNodeActive(!newValue);
        }

        private void SetNodeActive(bool active)
        {
            if (_visual != null)
            {
                _visual.gameObject.SetActive(active);
            }

            if (_colliders == null)
            {
                return;
            }

            foreach (Collider2D nodeCollider in _colliders)
            {
                if (nodeCollider != null)
                {
                    nodeCollider.enabled = active;
                }
            }
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

        private bool IsPlayerInRange(NetworkObject playerObject)
        {
            if (playerObject == null)
            {
                return false;
            }

            float distance = Vector3.Distance(playerObject.transform.position, transform.position);
            bool inRange = distance <= _serverInteractionRange;
            if (!inRange)
            {
                DBLog.Warning($"node.range.reject.{NetworkObjectId}", $"Node rejected interaction: out of range. distance={distance:0.00}, max={_serverInteractionRange:0.00}, playerPos={playerObject.transform.position}, nodePos={transform.position}.", 0.5f, this);
            }

            return inRange;
        }

        private void ResolvePool()
        {
            if (_pool != null)
            {
                return;
            }

            Debug.LogError($"[{nameof(HarvestableNode)}] INetworkPool was not injected on '{gameObject.name}'. Verify GameLifetimeScope registration and that this object is spawned via the pool.", this);
        }
    }
}
