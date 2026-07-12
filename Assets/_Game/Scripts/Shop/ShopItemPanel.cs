using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class ShopItemPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private GameObject soldOutLabel;

    // Label của nút Buy. Không gán trong Inspector cũng được — Setup tự tìm child TMP.
    [SerializeField] private TextMeshProUGUI buyButtonLabel;

    [Header("Layouts")]
    [SerializeField] private GameObject buyButtonLayout;
    [SerializeField] private GameObject sellButtonLayout;

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
        if (costText != null)
        {
            costText.text = $"{item.Price} {item.CurrencyType}";
        }

        // Item nâng cấp kỹ năng: chỉ có 1 nút "Update", KHÔNG bán được.
        bool canSell = item.isSellable && !item.IsUpgrade;

        // Buy button logic — với item Update thì nút này chính là nút "Update".
        bool canBuy = !item.IsSoldOut && ownedCurrency >= item.Price;
        buyButton.interactable = canBuy;

        // Đổi label nút Buy: "Update" cho item nâng cấp, "Buy" cho item thường.
        // Panel được pool/tái sử dụng nên PHẢI set cả 2 nhánh để không dính label cũ.
        if (buyButtonLabel == null && buyButton != null)
        {
            buyButtonLabel = buyButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        if (buyButtonLabel != null)
        {
            buyButtonLabel.text = item.IsUpgrade ? "Update" : "Buy";
        }

        if (buyButtonLayout != null)
        {
            buyButtonLayout.SetActive(!canBuy); // Hiện overlay (layout) khi không mua được
        }

        if (soldOutLabel != null)
        {
            soldOutLabel.SetActive(item.IsSoldOut);
        }

        // Sell button logic
        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(canSell);
            sellButton.interactable = canSell;

            if (sellButtonLayout != null)
            {
                // Item Update thì không có khái niệm bán → ẩn luôn overlay "không bán được".
                sellButtonLayout.SetActive(!canSell && !item.IsUpgrade);
            }

            sellButton.onClick.RemoveAllListeners();
            if (canSell)
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