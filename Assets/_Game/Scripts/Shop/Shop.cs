using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class Shop : NetworkBehaviour
{
    [SerializeField] private ShopPresenter presenter;
    [SerializeField] private ShopView view;
    [SerializeField] private ShopModel model;

    // Network synchronized shop item data
    private NetworkList<ShopItemData> networkItemData;

    // Mapping ItemId -> ShopItemData Index
    private Dictionary<string, int> itemDataIndexMap = new Dictionary<string, int>();

    private void Awake()
    {
        // Initialize NetworkList
        networkItemData = new NetworkList<ShopItemData>();
        
        view.Initialize();
        presenter = new ShopPresenter(view, model, this);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Server: Initialize network data từ model
        if (IsServer)
        {
            InitializeNetworkData();
        }

        // Subscribe to network data changes
        networkItemData.OnListChanged += OnShopDataChanged;

        // Initial refresh
        RefreshShopData();
    }

    public override void OnNetworkDespawn()
    {
        if (networkItemData != null)
        {
            networkItemData.OnListChanged -= OnShopDataChanged;
        }
        base.OnNetworkDespawn();
    }

    private void InitializeNetworkData()
    {
        if (model == null || model.items == null)
            return;

        networkItemData.Clear();
        itemDataIndexMap.Clear();

        foreach (var item in model.items)
        {
            var itemData = new ShopItemData(item.Id, item.RemainingQuantity);
            itemDataIndexMap[item.Id] = networkItemData.Count;
            networkItemData.Add(itemData);
        }
    }

    private void OnShopDataChanged(NetworkListEvent<ShopItemData> changeEvent)
    {
        // Refresh presenter với dữ liệu mới từ network
        RefreshShopData();
    }

    private void RefreshShopData()
    {
        // Update model từ network data
        if (model != null && model.items != null)
        {
            for (int i = 0; i < model.items.Count && i < networkItemData.Count; i++)
            {
                model.items[i].RemainingQuantity = networkItemData[i].RemainingQuantity;
            }
        }

        // Notify presenter to refresh view
        presenter?.RefreshShop();
    }

    /// <summary>
    /// Gọi từ Presenter để mua item và đồng bộ trên network
    /// </summary>
    public void BuyItem(string itemId)
    {
        if (!IsServer)
        {
            // Client gọi RPC tới Server
            BuyItemServerRpc(itemId);
        }
        else
        {
            // Server xử lý trực tiếp
            ProcessBuyItem(itemId);
        }
    }

    [Rpc(SendTo.Server)]
    private void BuyItemServerRpc(string itemId)
    {
        ProcessBuyItem(itemId);
    }

    private void ProcessBuyItem(string itemId)
    {
        if (!itemDataIndexMap.TryGetValue(itemId, out int index))
            return;

        if (index < 0 || index >= networkItemData.Count)
            return;

        var itemData = networkItemData[index];
        
        // Tìm ShopItem tương ứng
        var shopItem = model.items.Find(x => x.Id == itemId);
        if (shopItem == null)
            return;

        // Kiểm tra điều kiện mua
        if (shopItem.IsSoldOut)
            return;

        // Giảm số lượng
        if (!shopItem.isUnlimited)
        {
            itemData.RemainingQuantity--;
            networkItemData[index] = itemData;
        }

        // Broadcast event tới tất cả clients
        OnItemPurchasedClientRpc(itemId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnItemPurchasedClientRpc(string itemId)
    {
        Debug.Log($"[Network] Item purchased: {itemId}");
    }

    public NetworkList<ShopItemData> GetNetworkItemData()
    {
        return networkItemData;
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            view.OpenShop();
        }
    }

    public void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            view.CloseShop();
        }
    }
}