using System;
using DungeonBuilder.Core.Enums;
using UnityEngine;

namespace Assets._Game.Scripts.Data
{
    /// <summary>
    /// Mot slot chi phi tai nguyen: loai resource va so luong.
    /// Dung trong TowerDataSO.buildCost[] va upgradeCost[].
    /// </summary>
    [Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        [Min(0)] public int amount;

        public ResourceCost(ResourceType type, int amount)
        {
            this.type   = type;
            this.amount = amount;
        }

        /// <summary>Viet tat hien thi UI: Wood→W, Stone→St, Ore→O, ...</summary>
        public static string Abbr(ResourceType type) => type switch
        {
            ResourceType.Wood       => "W",
            ResourceType.Stone      => "St",
            ResourceType.Ore        => "O",
            ResourceType.Crystal    => "C",
            ResourceType.Copper     => "Cu",
            ResourceType.Iron       => "Fe",
            ResourceType.BlueGems   => "BG",
            ResourceType.PurpleGems => "PG",
            _                       => type.ToString()
        };

        public override string ToString() => $"{amount}{Abbr(type)}";
    }
}
