using System.Collections.Generic;
using UnityEngine;

namespace DungeonBuilder.Data
{
    [CreateAssetMenu(fileName = "WaveCatalog", menuName = "Dungeon Builder/Data/Wave Catalog")]
    public sealed class WaveCatalogSO : ScriptableObject
    {
        public List<WaveSO> waves = new();
    }
}
