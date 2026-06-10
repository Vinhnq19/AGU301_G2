using System;
using System.Collections.Generic;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.UI.Base;
using UnityEngine;

namespace DungeonBuilder.UI.TowerSelection
{
    public sealed class TowerSelectionModel : IModel
    {
        public event Action OnChanged;

        public Vector2Int SelectedGridPosition { get; private set; }
        public TowerDataSO[] AvailableTowers   { get; private set; } = Array.Empty<TowerDataSO>();
        public bool IsOpen                     { get; private set; }

        private readonly Dictionary<TowerType, bool> _affordability = new();

        public void Open(Vector2Int gridPos, TowerDataSO[] towers)
        {
            SelectedGridPosition = gridPos;
            AvailableTowers = towers ?? Array.Empty<TowerDataSO>();
            IsOpen = true;
            OnChanged?.Invoke();
        }

        public void Close()
        {
            IsOpen = false;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// Cap nhat trang thai du tien cua mot loai tower.
        /// </summary>
        public void UpdateAffordability(TowerDataSO data, bool canAfford)
        {
            if (data == null) return;
            _affordability[data.towerType] = canAfford;
        }

        /// <summary>
        /// Phat su kien OnChanged sau khi da cap nhat affordability cua tat ca tower.
        /// </summary>
        public void NotifyAffordabilityChanged()
        {
            OnChanged?.Invoke();
        }

        public bool CanAfford(TowerType type)
        {
            if (!_affordability.TryGetValue(type, out bool can)) return true;
            return can;
        }
    }
}
