using System.Collections.Generic;
using Assets._Game.Scripts.Data;
using UnityEngine;

namespace DungeonBuilder.Data
{
    /// <summary>
    /// Danh sach cac tower co the xay. Assign vao GameLifetimeScope.
    /// </summary>
    [CreateAssetMenu(fileName = "TowerCatalog", menuName = "Dungeon Builder/Data/Tower Catalog")]
    public sealed class TowerCatalogSO : ScriptableObject
    {
        [SerializeField] private TowerDataSO[] _towers;

        public IReadOnlyList<TowerDataSO> Towers => _towers;
    }
}
