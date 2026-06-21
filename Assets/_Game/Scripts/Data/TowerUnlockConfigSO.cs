using System.Collections.Generic;
using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace DungeonBuilder.Data
{
    /// <summary>
    /// Cau hinh tower nao duoc unlock mac dinh khi bat dau match.
    /// Cac tower con lai phai mua bang Token trong shop de unlock.
    /// Reset moi match (khong persist).
    /// </summary>
    [CreateAssetMenu(fileName = "TowerUnlockConfig", menuName = "Dungeon Builder/Data/Tower Unlock Config")]
    public sealed class TowerUnlockConfigSO : ScriptableObject
    {
        [SerializeField] private TowerType[] _defaultUnlocked = { TowerType.Arrow };

        public IReadOnlyList<TowerType> DefaultUnlocked => _defaultUnlocked;
    }
}
