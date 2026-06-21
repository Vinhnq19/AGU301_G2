using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Harvesting;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Player.Tools
{
    public abstract class HarvestToolBase : NetworkBehaviour, ITool
    {
        [SerializeField] private LayerMask _targetMask = ~0;
        [SerializeField, Min(0.01f)] private float _targetRadius = 0.35f;
        [SerializeField, Min(0.1f)] private float _fallbackSearchRadius = 2f;
        [SerializeField, Min(0.1f)] private float _serverInteractionRange = 2f;
        [SerializeField, Min(0.05f)] private float _swingDuration = 0.5f;

        private readonly Collider2D[] _fallbackResults = new Collider2D[16];
        private bool _isSwinging;
        private float _swingEnd;
        private ulong _pendingTargetId;
        private PlayerAnimation _animation;

        public abstract ToolType ToolType { get; }

        private void Awake()
        {
            _animation = GetComponent<PlayerAnimation>();
        }

        public void UseAction(Vector3 targetPosition)
        {
            if (!IsOwner || _isSwinging)
            {
                return;
            }

            NetworkObject target = FindTarget(targetPosition);
            if (target == null)
            {
                DBLog.Warning($"{ToolType}.send.no-target.{NetworkObjectId}", $"{ToolType} found no harvest target. click={targetPosition}, player={transform.position}.", 0.5f, this);
                return;
            }

            _isSwinging = true;
            _swingEnd = Time.time + _swingDuration;
            _pendingTargetId = target.NetworkObjectId;
            if (_animation != null)
            {
                _animation.BeginForaging(target.transform.position);
            }
        }

        private void Update()
        {
            if (!IsOwner || !_isSwinging)
            {
                return;
            }

            if (Time.time < _swingEnd)
            {
                return;
            }

            _isSwinging = false;
            if (_animation != null)
            {
                _animation.EndForaging();
            }
            InteractWithNodeServerRpc(_pendingTargetId);
        }

        public virtual void CancelAction()
        {
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void InteractWithNodeServerRpc(ulong targetNetworkObjectId, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            DBLog.Info($"{ToolType}.server.recv.{senderClientId}", $"{ToolType} harvest intent received. sender={senderClientId}, targetId={targetNetworkObjectId}.", 0.2f, this);

            if (!TryGetSenderPlayer(rpcParams.Receive.SenderClientId, out NetworkObject playerObject))
            {
                DBLog.Warning($"{ToolType}.server.no-player.{senderClientId}", $"{ToolType} rejected harvest: sender player not found. sender={senderClientId}.", 1f, this);
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject targetObject))
            {
                DBLog.Warning($"{ToolType}.server.no-object.{senderClientId}", $"{ToolType} rejected harvest: target object not spawned. targetId={targetNetworkObjectId}.", 1f, this);
                return;
            }

            IHarvestable harvestable = targetObject.GetComponent<IHarvestable>();
            if (harvestable == null)
            {
                DBLog.Warning($"{ToolType}.server.not-harvestable.{targetNetworkObjectId}", $"{ToolType} rejected harvest: target is not IHarvestable. target={targetObject.name}.", 1f, targetObject);
                return;
            }

            float distance = Vector3.Distance(playerObject.transform.position, targetObject.transform.position);
            if (distance > _serverInteractionRange)
            {
                DBLog.Warning($"{ToolType}.server.out-of-range.{senderClientId}", $"{ToolType} rejected harvest: out of range. distance={distance:0.00}, max={_serverInteractionRange:0.00}, playerPos={playerObject.transform.position}, targetPos={targetObject.transform.position}, target={targetObject.name}.", 0.5f, targetObject);
                return;
            }

            DBLog.Info($"{ToolType}.server.accept.{targetNetworkObjectId}", $"{ToolType} harvest accepted. sender={senderClientId}, target={targetObject.name}, distance={distance:0.00}.", 0.2f, targetObject);
            harvestable.TakeDamageFrom(this);
        }

        private NetworkObject FindTarget(Vector3 targetPosition)
        {
            Collider2D collider = Physics2D.OverlapCircle(targetPosition, _targetRadius, _targetMask);
            NetworkObject clickedTarget = collider != null ? collider.GetComponentInParent<NetworkObject>() : null;
            if (IsHarvestable(clickedTarget))
            {
                float clickedDistance = Vector3.Distance(transform.position, clickedTarget.transform.position);
                if (clickedDistance <= _fallbackSearchRadius)
                {
                    return clickedTarget;
                }

                DBLog.Warning($"{ToolType}.click.too-far.{NetworkObjectId}", $"{ToolType} clicked harvest target too far from player. target={clickedTarget.name}, distance={clickedDistance:0.00}, max={_fallbackSearchRadius:0.00}. Searching nearest valid target instead.", 0.5f, clickedTarget);
            }

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_targetMask);
            filter.useLayerMask = true;
            int fallbackCount = Physics2D.OverlapCircle(transform.position, _fallbackSearchRadius, filter, _fallbackResults);
            NetworkObject nearestTarget = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < fallbackCount; i++)
            {
                NetworkObject candidate = _fallbackResults[i].GetComponentInParent<NetworkObject>();
                if (!IsHarvestable(candidate)) continue;

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestTarget = candidate;
                }
            }

            if (nearestTarget != null)
            {
                DBLog.Info($"{ToolType}.fallback.{NetworkObjectId}", $"{ToolType} click missed but found nearby harvest target. target={nearestTarget.name}, distance={nearestDistance:0.00}.", 0.5f, nearestTarget);
            }

            return nearestTarget;
        }

        private static bool IsHarvestable(NetworkObject target)
        {
            return target != null && target.GetComponent<HarvestableNode>() != null;
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
