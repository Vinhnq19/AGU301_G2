using UnityEngine;

namespace DungeonBuilder.Data
{
    [CreateAssetMenu(fileName = "BuildAuthoritySettings", menuName = "Dungeon Builder/Data/Build Authority Settings")]
    public sealed class BuildAuthoritySettingsSO : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float _maxBuildDistance = 6f;

        public float MaxBuildDistance => _maxBuildDistance;
    }
}
