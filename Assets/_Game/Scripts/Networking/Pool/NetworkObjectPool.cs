using System.Collections.Generic;
using DungeonBuilder.Core.Debugging;
using DungeonBuilder.Core.Interfaces;
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DungeonBuilder.Networking.Pool
{
    public sealed class NetworkObjectPool : MonoBehaviour, INetworkPool
    {
        [SerializeField] private List<PoolEntry> _entries = new();

        private readonly Dictionary<uint, PoolEntry> _entriesByHash = new();
        private readonly Dictionary<uint, Queue<NetworkObject>> _poolByHash = new();
        private readonly Dictionary<uint, PooledPrefabInstanceHandler> _handlersByHash = new();
        private readonly HashSet<NetworkObject> _pooledObjects = new();
        private bool _handlersRegistered;

        private IObjectResolver _resolver;

        [Inject]
        public void Construct(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        private void Awake()
        {
            CleanupPooledSceneObjects();
            BuildPools();
            TryRegisterHandlers();
        }

        private void Start()
        {
            TryRegisterHandlers();
        }

        private void OnDestroy()
        {
            UnregisterHandlers();
        }

        public NetworkObject Get(NetworkObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogError($"{nameof(NetworkObjectPool)} cannot get a null prefab.");
                return null;
            }

            return GetByHash(GetPrefabHash(prefab), position, rotation);
        }

        public void Return(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                return;
            }

            uint hash = GetPrefabHash(networkObject);
            if (networkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                networkObject.Despawn(false);
            }

            ReturnByHash(hash, networkObject);
        }

        private void BuildPools()
        {
            _entriesByHash.Clear();
            _poolByHash.Clear();

            foreach (PoolEntry entry in _entries)
            {
                if (entry?.Prefab == null)
                {
                    continue;
                }

                uint hash = GetPrefabHash(entry.Prefab);
                _entriesByHash[hash] = entry;
                _poolByHash.TryAdd(hash, new Queue<NetworkObject>());
            }
        }

        private void TryRegisterHandlers()
        {
            if (_handlersRegistered || NetworkManager.Singleton == null)
            {
                return;
            }

            foreach (KeyValuePair<uint, PoolEntry> pair in _entriesByHash)
            {
                uint hash = pair.Key;
                PoolEntry entry = pair.Value;
                if (entry.Prefab == null)
                {
                    continue;
                }

                var handler = new PooledPrefabInstanceHandler(hash, this);
                NetworkManager.Singleton.PrefabHandler.AddHandler(hash, handler);
                _handlersByHash[hash] = handler;
            }

            _handlersRegistered = true;
        }

        private void UnregisterHandlers()
        {
            if (!_handlersRegistered || NetworkManager.Singleton == null)
            {
                return;
            }

            foreach (uint hash in _handlersByHash.Keys)
            {
                NetworkManager.Singleton.PrefabHandler.RemoveHandler(hash);
            }

            _handlersByHash.Clear();
            _handlersRegistered = false;
        }

        private NetworkObject GetByHash(uint hash, Vector3 position, Quaternion rotation)
        {
            if (!CanKeepPooledObjects())
            {
                DBLog.Warning($"pool.get.not-listening.{hash}", $"Pool get blocked because NetworkManager is not listening. hash={hash}.", 1f, this);
                return null;
            }

            if (!_entriesByHash.TryGetValue(hash, out PoolEntry entry))
            {
                Debug.LogError($"{nameof(NetworkObjectPool)} has no entry for hash {hash}.");
                return null;
            }

            Queue<NetworkObject> queue = _poolByHash[hash];
            NetworkObject networkObject = queue.Count > 0 ? queue.Dequeue() : CreateInstance(entry);
            _pooledObjects.Remove(networkObject);

            Transform instanceTransform = networkObject.transform;
            instanceTransform.SetPositionAndRotation(position, rotation);
            if (!networkObject.gameObject.activeSelf)
            {
                networkObject.gameObject.SetActive(true);
            }

            Inject(networkObject);

            foreach (IPoolable poolable in networkObject.GetComponentsInChildren<IPoolable>(true))
            {
                poolable.OnGetFromPool();
            }

            DBLog.Info($"pool.get.{hash}", $"Pool get. prefab={entry.Prefab.name}, hash={hash}, remaining={queue.Count}, pos={position}.", 0.5f, networkObject);
            return networkObject;
        }

        private void ReturnByHash(uint hash, NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                return;
            }

            if (!_poolByHash.TryGetValue(hash, out Queue<NetworkObject> queue))
            {
                Destroy(networkObject.gameObject);
                return;
            }

            if (!CanKeepPooledObjects())
            {
                _pooledObjects.Remove(networkObject);
                Destroy(networkObject.gameObject);
                DBLog.Info($"pool.destroy-not-listening.{hash}", $"Destroyed pooled object because NetworkManager is not listening. hash={hash}, object={networkObject.name}.", 0.5f, networkObject);
                return;
            }

            if (!_pooledObjects.Add(networkObject))
            {
                DBLog.Warning($"pool.duplicate-return.{hash}", $"Ignored duplicate pool return. hash={hash}, object={networkObject.name}.", 1f, networkObject);
                return;
            }

            networkObject.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
            networkObject.gameObject.SetActive(false);

            foreach (IPoolable poolable in networkObject.GetComponentsInChildren<IPoolable>(true))
            {
                poolable.OnReturnToPool();
            }

            queue.Enqueue(networkObject);
            DBLog.Info($"pool.return.{hash}", $"Pool return. hash={hash}, count={queue.Count}, object={networkObject.name}.", 0.5f, networkObject);
        }

        private NetworkObject CreateInstance(PoolEntry entry)
        {
            Transform parent = entry.Parent != null ? entry.Parent : transform;
            NetworkObject instance = Instantiate(entry.Prefab, parent);
            Inject(instance);
            DBLog.Info($"pool.create.{GetPrefabHash(entry.Prefab)}", $"Pool created instance. prefab={entry.Prefab.name}.", 0.5f, instance);
            return instance;
        }

        private static uint GetPrefabHash(NetworkObject networkObject)
        {
            return networkObject != null ? networkObject.PrefabIdHash : 0;
        }

        private static bool CanKeepPooledObjects()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        }

        private void CleanupPooledSceneObjects()
        {
            var visitedParents = new HashSet<Transform>();
            foreach (PoolEntry entry in _entries)
            {
                Transform parent = entry?.Parent != null ? entry.Parent : transform;
                if (parent == null || !visitedParents.Add(parent))
                {
                    continue;
                }

                foreach (NetworkObject pooledObject in parent.GetComponentsInChildren<NetworkObject>(true))
                {
                    if (pooledObject == null || pooledObject.transform == parent)
                    {
                        continue;
                    }

                    DBLog.Warning($"pool.cleanup.{pooledObject.PrefabIdHash}", $"Removed pooled NetworkObject left in scene before network start. object={pooledObject.name}, hash={pooledObject.PrefabIdHash}.", 0f, pooledObject);
                    Destroy(pooledObject.gameObject);
                }
            }
        }

        private void Inject(NetworkObject networkObject)
        {
            if (_resolver == null || networkObject == null)
            {
                return;
            }

            _resolver.InjectGameObject(networkObject.gameObject);
        }

        private sealed class PooledPrefabInstanceHandler : INetworkPrefabInstanceHandler
        {
            private readonly uint _hash;
            private readonly NetworkObjectPool _pool;

            public PooledPrefabInstanceHandler(uint hash, NetworkObjectPool pool)
            {
                _hash = hash;
                _pool = pool;
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            {
                return _pool.GetByHash(_hash, position, rotation);
            }

            public void Destroy(NetworkObject networkObject)
            {
                _pool.ReturnByHash(_hash, networkObject);
            }
        }
    }
}
