using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class ShopItemPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private GameObject soldOutLabel;

    [SerializeField] private Image iconImage;

    private string itemId;
    private Action<string> onBuy;
    private Action<string> onSell;

    [SerializeField] private float maxSize = 64f;

    public void Setup(ShopItem item, int ownedCurrency, Action<string> onBuyCallback, Action<string> onSellCallback = null)
    {
        itemId = item.Id;
        onBuy = onBuyCallback;
        onSell = onSellCallback;

        if (item.Icon && iconImage)
        {
            iconImage.sprite = item.Icon;

            RectTransform rt = iconImage.rectTransform;
            Sprite sprite = item.Icon;

            float width = sprite.rect.width;
            float height = sprite.rect.height;

            if (width > height)
            {
                rt.sizeDelta = new Vector2(maxSize, maxSize * height / width);
            }
            else
            {
                rt.sizeDelta = new Vector2(maxSize * width / height, maxSize);
            }
        }


        nameText.text = item.Name;

        // Buy enabled only if in stock AND the player can afford at least one.
        buyButton.interactable = !item.IsSoldOut && ownedCurrency >= item.Price;

        // Sell button: disable + auto-dim (Unity ColorBlock.disabledColor) khi item không sellable
        if (sellButton != null)
        {
            sellButton.interactable = item.isSellable;

            sellButton.onClick.RemoveAllListeners();
            if (item.isSellable)
            {
                sellButton.onClick.AddListener(() => onSell?.Invoke(itemId));
            }
        }
        else if (onSellCallback != null)
        {
            // Defensive: nếu dev quên wire sellButton trong Inspector
            // → log loud để biết ngay thay vì click im lặng không có gì xảy ra
            Debug.LogWarning(
                $"[ShopItemPanel] '{name}': sellButton field is NOT assigned in Inspector — Sell action sẽ không fire. " +
                $"Kéo Button 'Sell' vào field _sellButton để fix.", this);
        }

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy?.Invoke(itemId));
    }
}