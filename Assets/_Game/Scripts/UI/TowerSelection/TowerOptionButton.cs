using System;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Core.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonBuilder.UI.TowerSelection
{
    /// <summary>
    /// MonoBehaviour tren moi button tower trong Tower Selection Panel.
    /// Hien thi ten, chi phi, va trang thai du tien cua tower.
    /// </summary>
    public sealed class TowerOptionButton : MonoBehaviour
    {
        [Header("UI Refs")]
        [SerializeField] private Button _button;
        // [SerializeField] private Image _icon;             // TODO: bat comment khi them Sprite icon vao TowerDataSO
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private GameObject _disabledOverlay; // Image mau do ban trong suot khi khong du tien

        private TowerType _towerType;
        private Action<TowerType> _onSelect;

        private void Awake()
        {
            _button?.onClick.AddListener(OnButtonClicked);
        }

        private void OnDestroy()
        {
            _button?.onClick.RemoveListener(OnButtonClicked);
        }

        /// <summary>
        /// Cap nhat du lieu hien thi cho button nay.
        /// </summary>
        public void Setup(TowerDataSO data, bool canAfford, Action<TowerType> onSelect)
        {
            if (data == null) return;

            _towerType = data.towerType;
            _onSelect  = onSelect;

            // Ten tower
            if (_nameText != null)
            {
                _nameText.text = data.towerType.ToString();
            }

            // Chi phi
            if (_costText != null)
            {
                _costText.text = data.oreCost > 0
                    ? $"{data.woodCost}W  {data.oreCost}O"
                    : $"{data.woodCost}W";
            }

            // Icon (TODO: bat comment khi co Sprite)
            // if (_icon != null && data.icon != null) _icon.sprite = data.icon;

            // Affordability
            if (_button != null)
            {
                _button.interactable = canAfford;
            }

            if (_disabledOverlay != null)
            {
                _disabledOverlay.SetActive(!canAfford);
            }
        }

        private void OnButtonClicked()
        {
            _onSelect?.Invoke(_towerType);
        }
    }
}
