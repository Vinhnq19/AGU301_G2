using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using VContainer;
using Assets._Game.Scripts.Data;
using DungeonBuilder.Core;
using DungeonBuilder.Core.Interfaces;
using DungeonBuilder.Core.Enums;
using DungeonBuilder.Data;
using DungeonBuilder.Networking.Pool;

public class Shop : NetworkBehaviour
{
    [SerializeField] private ShopPresenter presenter;
    [SerializeField] private ShopView view;
    [SerializeField] private ShopModel model;
    [SerializeField] private TowerCatalogSO _towerCatalog;
    [SerializeField] private TowerUnlockConfigSO _unlockConfig;

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

    private void Start()
    {
        // FIX: VContainer inject có thể chạy SAU Awake trong một số setup.
        // Nếu presenter null ở Awake (do `this` chưa inject xong), khởi tạo lại ở Start.
        if (presenter == null && view != null && model != null)
        {
            Debug.LogWarning("[Shop] presenter was null in Awake — re-initializing in Start");
            view.Initialize();
            presenter = new ShopPresenter(view, model, this);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Server: Initialize network data từ model
        if (IsServer)
        {
            ApplyDefaultUnlocks();
            InitializeNetworkData();
        }

        // Subscribe to network data changes
        networkItemData.OnListChanged += OnShopDataChanged;

        // Initial refresh
        RefreshShopData();
    }

    public override void OnNetworkDespawn()
    {
        if (_sharedResources != null)
        {
            _sharedResources.ResourceChanged -= HandleSharedResourceChanged;
        }

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

    /// <summary>Unlock mac dinh cac tower trong config (vd: Arrow) luc bat dau match.</summary>
    private void ApplyDefaultUnlocks()
    {
        if (_sharedResources == null || _unlockConfig == null || _towerCatalog == null)
        {
            return;
        }

        foreach (TowerType type in _unlockConfig.DefaultUnlocked)
        {
            TowerDataSO data = FindTower(type);
            if (data != null)
            {
                _sharedResources.TrySet(data.unlockResourceType, 1);
            }
        }
    }

    /// <summary>Map TowerType -> TowerDataSO tu catalog (dung cho ApplyDefaultUnlocks).</summary>
    private TowerDataSO FindTower(TowerType type)
    {
        if (_towerCatalog == null)
        {
            return null;
        }

        foreach (TowerDataSO data in _towerCatalog.Towers)
        {
            if (data.towerType == type)
            {
                return data;
            }
        }

        return null;
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
            // Pass `default` for RpcParams on the send side; NGO populates
            // rpcParams.Receive.SenderClientId from the real sender on the server.
            BuyItemServerRpc(resourceType, quantity, default);
        }
        else
        {
            ProcessBuyItem(resourceType, quantity, NetworkManager.ServerClientId);
        }
    }

    [Rpc(SendTo.Server)]
    private void BuyItemServerRpc(ResourceType resourceType, int quantity, RpcParams rpcParams)
    {
        ProcessBuyItem(resourceType, quantity, rpcParams.Receive.SenderClientId);
    }

    private void ProcessBuyItem(ResourceType resourceType, int quantity, ulong requesterClientId)
    {
        if (_sharedResources == null)
            return;

        var shopItem = model.items.Find(x => x.ResourceType == resourceType);
        if (shopItem == null)
            return;

        if (!itemDataIndexMap.TryGetValue(resourceType, out int index) || index < 0 || index >= networkItemData.Count)
            return;

        var itemData = networkItemData[index];

        ResourceType currencyRT = shopItem.CurrencyType.ToResourceType();

        // Sold out?
        if (shopItem.IsSoldOut)
        {
            SendFeedback(ShopTxResult.FailedStock, resourceType, 0, currencyRT, 0, requesterClientId);
            return;
        }

        int qty = quantity;
        if (!shopItem.isUnlimited)
        {
            if (qty > itemData.RemainingQuantity)
                qty = itemData.RemainingQuantity;

            if (qty <= 0)
            {
                SendFeedback(ShopTxResult.FailedStock, resourceType, 0, currencyRT, 0, requesterClientId);
                return;
            }
        }

        // Affordability: atomic check + spend of Price*qty in the item's currency.
        int totalCost = SafeMultiply(shopItem.Price, qty);
        var cost = new ResourceCost[] { new ResourceCost(currencyRT, totalCost) };
        if (!_sharedResources.CanAfford(cost) || !_sharedResources.TrySpend(cost))
        {
            SendFeedback(ShopTxResult.FailedAfford, resourceType, 0, currencyRT, 0, requesterClientId);
            return;
        }

        // Deduct stock.
        if (!shopItem.isUnlimited)
        {
            itemData.RemainingQuantity -= qty;
            networkItemData[index] = itemData;
        }

        // Grant the resource server-side; NetworkList replicates → ResourceChanged → HUD updates.
        _sharedResources.TryAdd(resourceType, qty);

        SendFeedback(ShopTxResult.Success, resourceType, qty, currencyRT, totalCost, requesterClientId);
    }

    [Inject]
    public void Construct(IResourceService sharedResources, INetworkPool pool)
    {
        _sharedResources = sharedResources;
        _pool = pool;

        if (_sharedResources != null)
        {
            _sharedResources.ResourceChanged += HandleSharedResourceChanged;
        }
    }

    private void HandleSharedResourceChanged(ResourceChanged change)
    {
        // Re-clamp/disable buy buttons only when a gating currency changes (cheap; ignores harvest spam).
        if (change.Type == ResourceType.Coin || change.Type == ResourceType.Token)
        {
            presenter?.RefreshShop();
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
            SellItemServerRpc(resourceType, quantity, default);
        }
        else
        {
            ProcessSellItem(resourceType, quantity, NetworkManager.ServerClientId);
        }
    }

    [Rpc(SendTo.Server)]
    private void SellItemServerRpc(ResourceType resourceType, int quantity, RpcParams rpcParams)
    {
        ProcessSellItem(resourceType, quantity, rpcParams.Receive.SenderClientId);
    }

    private void ProcessSellItem(ResourceType resourceType, int quantity, ulong requesterClientId)
    {
        if (_sharedResources == null)
            return;

        var shopItem = model.items.Find(x => x.ResourceType == resourceType);
        if (shopItem == null)
            return;

        if (!shopItem.isSellable)
        {
            SendFeedback(ShopTxResult.FailedNotSellable, shopItem.CurrencyType.ToResourceType(), 0, resourceType, 0, requesterClientId);
            return;
        }

        if (!itemDataIndexMap.TryGetValue(resourceType, out int index) || index < 0 || index >= networkItemData.Count)
            return;

        var itemData = networkItemData[index];
        int qty = quantity;

        // Spend the resource atomically; fail the whole batch if the player lacks enough.
        var spend = new ResourceCost[] { new ResourceCost(resourceType, qty) };
        if (!_sharedResources.TrySpend(spend))
        {
            SendFeedback(ShopTxResult.FailedNoResource, shopItem.CurrencyType.ToResourceType(), 0, resourceType, 0, requesterClientId);
            return;
        }

        // Award the item's currency (not hardcoded Coin).
        ResourceType currencyRT = shopItem.CurrencyType.ToResourceType();
        int received = SafeMultiply(itemData.Sell, qty);
        if (received > 0)
        {
            _sharedResources.TryAdd(currencyRT, received);
        }

        SendFeedback(ShopTxResult.Success, currencyRT, received, resourceType, qty, requesterClientId);
    }

    private void SendFeedback(
        ShopTxResult result,
        ResourceType gainedType, int gainedAmt,
        ResourceType spentType, int spentAmt,
        ulong requesterClientId)
    {
        OnTransactionFeedbackClientRpc(
            (int)result,
            (int)gainedType, gainedAmt,
            (int)spentType, spentAmt,
            requesterClientId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnTransactionFeedbackClientRpc(
        int result,
        int gainedType, int gainedAmt,
        int spentType, int spentAmt,
        ulong requesterClientId)
    {
        // Approach B: broadcast to all, but only the requester shows the toast.
        if (NetworkManager == null || NetworkManager.LocalClientId != requesterClientId)
            return;

        presenter?.ShowFeedback(
            (ShopTxResult)result,
            (ResourceType)gainedType, gainedAmt,
            (ResourceType)spentType, spentAmt);
    }

    /// <summary>Overflow-safe multiply; non-positive inputs yield 0 (free / no-op).</summary>
    private static int SafeMultiply(int a, int b)
    {
        if (a <= 0 || b <= 0) return 0;
        if (a > int.MaxValue / b) return int.MaxValue;
        return a * b;
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