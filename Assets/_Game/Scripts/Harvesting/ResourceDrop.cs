using DG.Tweening;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Networking;
using DungeonBuilder.Networking.Pool;
using DungeonBuilder.Player;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace DungeonBuilder.Harvesting
{
    public sealed class ResourceDrop : NetworkBehaviour, IPoolable
    {
        [SerializeField] private Transform _visual;

        private readonly NetworkVariable<ResourceType> _resourceType = new(ResourceType.Wood, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _amount = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private SharedResourceManager _sharedResources;
        private INetworkPool _pool;
        private bool _canPickup;

        [Inject]
        public void Construct(SharedResourceManager sharedResources, INetworkPool pool)
        {
            _sharedResources = sharedResources;
            _pool = pool;
        }

        public void Configure(ResourceType type, int amount)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            _resourceType.Value = type;
            _amount.Value = amount;
            DBLog.Info($"drop.configure.{NetworkObjectId}", $"ResourceDrop configured. id={NetworkObjectId}, type={type}, amount={amount}.", 0.2f, this);
        }

        public void OnGetFromPool()
        {
            _canPickup = true;
            SetCollisionActive(true);

            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one;
            _visual.DOLocalJump(Vector3.up * 0.5f, 0.3f, 1, 0.4f)
                .SetEase(Ease.OutBounce)
                .OnComplete(() => _visual.localPosition = Vector3.zero);
        }

        public void OnReturnToPool()
        {
            _canPickup = false;
            SetCollisionActive(false);

            if (_visual == null)
            {
                return;
            }

            _visual.DOKill();
            _visual.localPosition = Vector3.zero;
            _visual.localScale = Vector3.one;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_canPickup)
            {
                return;
            }

            if (!IsServer || _sharedResources == null || _pool == null)
            {
                DBLog.Warning($"drop.pickup.blocked.{NetworkObjectId}", $"Pickup ignored. server={IsServer}, sharedNull={_sharedResources == null}, poolNull={_pool == null}.", 1f, this);
                return;
            }

            if (other.GetComponentInParent<PlayerController>() == null)
            {
                DBLog.Info($"drop.pickup.non-player.{NetworkObjectId}", $"Pickup trigger ignored by non-player: {other.name}.", 1f, this);
                return;
            }

            _canPickup = false;
            SetCollisionActive(false);
            DBLog.Info($"drop.pickup.{NetworkObjectId}", $"ResourceDrop picked up. type={_resourceType.Value}, amount={_amount.Value}, by={other.name}.", 0.2f, this);
            _sharedResources.AddResource(_resourceType.Value, _amount.Value);
            _pool.Return(NetworkObject);
        }

        private void SetCollisionActive(bool active)
        {
            foreach (Collider2D dropCollider in GetComponentsInChildren<Collider2D>(true))
            {
                dropCollider.enabled = active;
            }
        }
    }
}
