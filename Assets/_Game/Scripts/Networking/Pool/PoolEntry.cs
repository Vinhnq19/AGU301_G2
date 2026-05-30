using System;
using Unity.Netcode;
using UnityEngine;

namespace DungeonBuilder.Networking.Pool
{
    [Serializable]
    public sealed class PoolEntry
    {
        [SerializeField] private NetworkObject _prefab;
        [SerializeField, Min(0)] private int _prewarmCount = 4;
        [SerializeField] private Transform _parent;

        public NetworkObject Prefab => _prefab;
        public int PrewarmCount => _prewarmCount;
        public Transform Parent => _parent;
    }
}
