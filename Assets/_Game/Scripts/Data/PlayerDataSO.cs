using UnityEngine;

namespace DungeonBuilder.Data
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "Dungeon Builder/Data/Player")]
    public sealed class PlayerDataSO : ScriptableObject
    {
        public float maxHP = 100f;
        public float speed = 5f;
        public float maxMana = 100f;
        public float dashCooldown = 1f;
    }
}
