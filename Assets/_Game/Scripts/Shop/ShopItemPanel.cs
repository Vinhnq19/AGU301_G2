using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class ShopItemPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private GameObject soldOutLabel;

    private string itemId;
    private Action<string> onBuy;

    public void Setup(ShopItem item, Action<string> onBuyCallback)
    {
        itemId = item.Id;
        onBuy = onBuyCallback;

        nameText.text = item.Name;
        priceText.text = item.Price.ToString();

        // soldOutLabel.SetActive(item.IsSoldOut);
        buyButton.interactable = !item.IsSoldOut;

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy?.Invoke(itemId));
    }
}