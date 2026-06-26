using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ShopView
{
    [SerializeField] private GameObject shopPanel;

    [SerializeField] private Button coinTabButton;
    [SerializeField] private Button tokenTabButton;

    [SerializeField] private Image coinTabImage;
    [SerializeField] private Image tokenTabImage;

    [SerializeField] private ShopItemPanel itemPanelPrefab;
    [SerializeField] private Transform itemPanelContainer;

    [SerializeField] private ShopQuantityPopup quantityPopup;

    [SerializeField] private TransactionToast toast;

    public event Action<CurrencyType> OnTabChanged;

    // THÊM: List dùng để lưu trữ và tái sử dụng các Item Panel (Object Pooling)
    private List<ShopItemPanel> panelPool = new List<ShopItemPanel>();

    public ShopView() { }

    public void Initialize()
    {
        coinTabButton.onClick.RemoveAllListeners();
        tokenTabButton.onClick.RemoveAllListeners();

        coinTabButton.onClick.AddListener(() =>
            OnTabChanged?.Invoke(CurrencyType.Coin));

        tokenTabButton.onClick.AddListener(() =>
            OnTabChanged?.Invoke(CurrencyType.Token));

        // Popup nhập số lượng: wire 1 lần + ẩn mặc định
        if (quantityPopup != null)
        {
            quantityPopup.Initialize();
            quantityPopup.Hide();
        }
    }

    public void SetTab(CurrencyType active)
    {
        coinTabImage.color =
            active == CurrencyType.Coin
            ? Color.white
            : new Color(0f, 1f, 0f);

        tokenTabImage.color =
            active == CurrencyType.Token
            ? Color.white
            : new Color(0f, 1f, 0f);
    }

    public bool IsOpen => shopPanel != null && shopPanel.activeSelf;

    public void OpenShop() => shopPanel.SetActive(true);

    public void CloseShop()
    {
        // Đóng shop cũng phải ẩn popup nhập số lượng (nếu đang mở)
        HideQuantityPopup();
        shopPanel.SetActive(false);
    }

    /// <summary>Mở popup nhập số lượng cho 1 item + thao tác (Buy/Sell).</summary>
    /// <param name="unitPrice">Đơn giá để hiện tổng trên nút (Price cho Buy, Sell cho Sell).</param>
    public void ShowQuantityPopup(string itemName, Sprite itemIcon, ShopAction action, int maxQty, int unitPrice, Action<int> onConfirm)
    {
        if (quantityPopup == null)
        {
            Debug.LogWarning(
                "[ShopView] quantityPopup chưa được gán trong Inspector — không thể mở popup nhập số lượng. " +
                "Kéo GameObject popup (có script ShopQuantityPopup) vào field quantityPopup của ShopView để fix.");
            return;
        }

        quantityPopup.Show(itemName, itemIcon, action, maxQty, unitPrice, onConfirm);
    }

    public void HideQuantityPopup() => quantityPopup?.Hide();

    public void ShowToast(string message, bool success)
    {
        if (toast == null)
        {
            Debug.LogWarning(
                "[ShopView] toast chưa được gán trong Inspector — không thể hiển thị feedback. " +
                "Kéo GameObject toast (có script TransactionToast) vào field toast của ShopView để fix.");
            return;
        }
        toast.Show(message, success);
    }

    // CẬP NHẬT: Xử lý hiển thị bằng cách tái sử dụng (Pooling)
    public void CreateItemPanels(List<ShopItem> items, int ownedCurrency, System.Action<string> onBuyCallback = null, System.Action<string> onSellCallback = null)
    {
        // 1. Tạm ẩn tất cả các panel đang có trong Pool
        foreach (var panel in panelPool)
        {
            panel.gameObject.SetActive(false);
        }

        // 2. Duyệt qua danh sách data mới
        for (int i = 0; i < items.Count; i++)
        {
            if (i < panelPool.Count)
            {
                // Nếu trong Pool đã có sẵn Panel -> Bật nó lên và gán data mới
                panelPool[i].gameObject.SetActive(true);
                panelPool[i].Setup(items[i], ownedCurrency, onBuyCallback, onSellCallback);
            }
            else
            {
                // Nếu Pool chưa đủ Panel -> Tạo mới, gán data và thêm vào Pool
                var panelObj = GameObject.Instantiate(itemPanelPrefab.gameObject, itemPanelContainer);
                var shopItemPanel = panelObj.GetComponent<ShopItemPanel>();

                shopItemPanel.Setup(items[i], ownedCurrency, onBuyCallback, onSellCallback);
                panelPool.Add(shopItemPanel);
            }
        }
    }
}