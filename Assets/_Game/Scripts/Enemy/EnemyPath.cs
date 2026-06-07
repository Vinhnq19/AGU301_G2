using UnityEngine;

namespace DungeonBuilder.Enemy
{
    public sealed class EnemyPath : MonoBehaviour
    {
        [SerializeField] private Transform[] _waypoints;

        public Transform[] Waypoints => _waypoints;
    }
}
