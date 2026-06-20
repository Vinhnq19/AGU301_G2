using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using VContainer;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Networking.Pool;

public class Shop : NetworkBehaviour
{
    [SerializeField] private ShopPresenter presenter;
    [SerializeField] private ShopView view;
    [SerializeField] private ShopModel model;

    private IResourceService _sharedResources;
    private INetworkPool _pool;

    // Network synchronized shop item data
    private NetworkList<ShopItemData> networkItemData;

    // Mapping ResourceType -> ShopItemData Index
    private Dictionary<ResourceType, int> itemDataIndexMap = new Dictionary<ResourceType, int>();

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
            // Sync Sell price vào NetworkList để server-authoritative runtime change (sales/events)
            var itemData = new ShopItemData(item.ResourceType, item.RemainingQuantity, item.Sell);
            itemDataIndexMap[item.ResourceType] = networkItemData.Count;
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
    /// Gọi từ Presenter để mua item theo ResourceType và đồng bộ trên network.
    /// Hỗ trợ số lượng: mua `quantity` unit cùng lúc.
    /// </summary>
    public void BuyItem(ResourceType resourceType, int quantity)
    {
        if (quantity <= 0)
            return;

        if (!IsServer)
        {
            // Client gọi RPC tới Server
            BuyItemServerRpc(resourceType, quantity);
        }
        else
        {
            // Server xử lý trực tiếp
            ProcessBuyItem(resourceType, quantity);
        }
    }

    [Rpc(SendTo.Server)]
    private void BuyItemServerRpc(ResourceType resourceType, int quantity)
    {
        ProcessBuyItem(resourceType, quantity);
    }

    private void ProcessBuyItem(ResourceType resourceType, int quantity)
    {
        if (!itemDataIndexMap.TryGetValue(resourceType, out int index))
            return;

        if (index < 0 || index >= networkItemData.Count)
            return;

        var itemData = networkItemData[index];

        // Tìm ShopItem tương ứng
        var shopItem = model.items.Find(x => x.ResourceType == resourceType);
        if (shopItem == null)
            return;

        // Kiểm tra điều kiện mua
        if (shopItem.IsSoldOut)
            return;

        // Server-side clamp theo stock còn lại (authoritative)
        int qty = quantity;
        if (!shopItem.isUnlimited)
        {
            if (qty > itemData.RemainingQuantity)
                qty = itemData.RemainingQuantity;

            if (qty <= 0)
                return;

            itemData.RemainingQuantity -= qty;
            networkItemData[index] = itemData;
        }

        // Broadcast event tới tất cả clients (cấp `qty` resource)
        OnItemPurchasedClientRpc(resourceType, qty);
    }

    [Inject]
    public void Construct(IResourceService sharedResources, INetworkPool pool)
    {
        _sharedResources = sharedResources;
        _pool = pool;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnItemPurchasedClientRpc(ResourceType resourceType, int quantity)
    {
        Debug.Log($"[Network] Item purchased: {quantity}x {resourceType}");
        // Cập nhật tài nguyên người chơi (cấp `quantity` resource)
        if (_sharedResources != null)
        {
            _sharedResources.TryAdd(resourceType, quantity);
        }
    }

    /// <summary>
    /// Gọi từ Presenter để bán item theo ResourceType và đồng bộ trên network.
    /// Client sẽ gửi RPC tới Server; Server xử lý trực tiếp.
    /// Hỗ trợ số lượng: bán `quantity` unit cùng lúc.
    /// </summary>
    public void SellItem(ResourceType resourceType, int quantity)
    {
        if (quantity <= 0)
            return;

        if (!IsServer)
        {
            SellItemServerRpc(resourceType, quantity);
        }
        else
        {
            ProcessSellItem(resourceType, quantity);
        }
    }

    [Rpc(SendTo.Server)]
    private void SellItemServerRpc(ResourceType resourceType, int quantity)
    {
        ProcessSellItem(resourceType, quantity);
    }

    private void ProcessSellItem(ResourceType resourceType, int quantity)
    {
        if (_sharedResources == null)
            return;

        // Tìm ShopItem tương ứng (chỉ để check isSellable — flag tĩnh từ ScriptableObject)
        var shopItem = model.items.Find(x => x.ResourceType == resourceType);
        if (shopItem == null)
            return;

        // Guard 1: chỉ xử lý nếu item được đánh dấu sellable
        if (!shopItem.isSellable)
            return;

        // Lookup network data để lấy Sell price server-authoritative
        if (!itemDataIndexMap.TryGetValue(resourceType, out int index))
            return;
        if (index < 0 || index >= networkItemData.Count)
            return;

        var itemData = networkItemData[index];
        int qty = quantity;

        // Guard 2: server validate atomic — trừ `qty` resource khỏi player.
        // TrySpend tự check đủ resource và trừ trong cùng transaction; fail nguyên lô nếu thiếu.
        var costs = new ResourceCost[] { new ResourceCost(resourceType, qty) };
        if (!_sharedResources.TrySpend(costs))
        {
            Debug.LogWarning($"[Shop] Sell failed — player không đủ {qty} {resourceType}");
            return;
        }

        // Cộng coin cho player dùng Sell price từ itemData (network-replicated, server-authoritative)
        int coinReceived = itemData.Sell * qty;
        if (coinReceived > 0)
        {
            _sharedResources.TryAdd(ResourceType.Coin, coinReceived);
        }

        // Broadcast cho clients (chỉ để log/UI feedback; data đã sync qua NetworkList resource)
        OnItemSoldClientRpc(resourceType, coinReceived);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnItemSoldClientRpc(ResourceType resourceType, int coinReceived)
    {
        Debug.Log($"[Network] Item sold: {resourceType} for {coinReceived} coin(s)");
        // Resources đã được SharedResourceManager tự đồng bộ qua NetworkList
        // → HUD sẽ tự update nhờ ResourceChanged event
    }

    public NetworkList<ShopItemData> GetNetworkItemData()
    {
        return networkItemData;
    }

    /// <summary>Số resource hiện có của player — dùng ở client để clamp số lượng khi Sell.</summary>
    public int GetResourceAmount(ResourceType type)
    {
        return _sharedResources != null ? _sharedResources.GetAmount(type) : 0;
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