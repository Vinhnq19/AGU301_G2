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

    public void OpenShop() => shopPanel.SetActive(true);
    public void CloseShop() => shopPanel.SetActive(false);

    // CẬP NHẬT: Xử lý hiển thị bằng cách tái sử dụng (Pooling)
    public void CreateItemPanels(List<ShopItem> items, System.Action<string> onBuyCallback = null)
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
                panelPool[i].Setup(items[i], onBuyCallback);
            }
            else
            {
                // Nếu Pool chưa đủ Panel -> Tạo mới, gán data và thêm vào Pool
                var panelObj = GameObject.Instantiate(itemPanelPrefab.gameObject, itemPanelContainer);
                var shopItemPanel = panelObj.GetComponent<ShopItemPanel>();

                shopItemPanel.Setup(items[i], onBuyCallback);
                panelPool.Add(shopItemPanel);
            }
        }
    }
}