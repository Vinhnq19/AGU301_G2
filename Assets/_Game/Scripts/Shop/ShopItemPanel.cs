using UnityEngine;
using UnityEngine.UI;
using System;

public class ShopItemPanel : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private GameObject soldOutLabel;

    private string itemId;
    private Action<string> onBuy;

    public void Setup(string id, string name, int price, bool isSoldOut, Action<string> onBuyCallback)
    {
        itemId = id;
        onBuy = onBuyCallback;

        nameText.text = name;
        priceText.text = price.ToString();

        soldOutLabel.SetActive(isSoldOut);
        buyButton.interactable = !isSoldOut;

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy?.Invoke(itemId));
    }
}