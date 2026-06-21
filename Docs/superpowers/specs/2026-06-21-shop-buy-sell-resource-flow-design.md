# Shop Buy/Sell Resource Flow + UI Feedback — Design

**Date:** 2026-06-21
**Status:** Approved (pending spec review)
**Scope:** Make the shop actually charge/award the correct currency on Buy/Sell, and show transaction feedback UI to the acting player.

---

## 1. Problem

The shop UI, networking, and quantity popup already exist, but the transaction logic is incomplete:

- **Buy charges nothing.** `Shop.ProcessBuyItem` deducts shop stock and grants the resource, but never checks or spends any currency. `ShopItem.Price` and `ShopItem.CurrencyType` are unused on the spend side.
- **Sell always awards Coin.** `Shop.ProcessSellItem` hardcodes `_sharedResources.TryAdd(ResourceType.Coin, …)` regardless of the item's `CurrencyType`.
- **No transaction feedback UI.** Only `Debug.Log` exists. Success/failure is invisible to the player.
- **No affordability guard.** The buy button is never disabled when the player can't afford an item; the quantity popup is clamped only by stock.

The HUD itself already updates correctly: `SharedResourceManager` fires `ResourceChanged` on every mutation, which `HUDPresenter` consumes to re-render Coin/Token/resource counts. So once resource values change correctly, "update UI" for the counts is automatic.

## 2. Goals / Non-Goals

**Goals**
- Buy: atomically spend `Price × qty` of `item.CurrencyType`, grant `qty` of `item.ResourceType`, deduct stock. Server-authoritative.
- Sell: spend `qty` of `item.ResourceType`, grant `Sell × qty` of `item.CurrencyType`. Server-authoritative.
- Show a transient toast on the **acting player's** client for both success and failure.
- Client clamps the buy quantity to what the player can afford and disables the buy button when the player can't afford even one.

**Non-Goals (YAGNI)**
- Per-player inventories (resources stay match-wide shared).
- Runtime/sales-driven price changes (read `Price`/`CurrencyType` from the `ShopItem` ScriptableObject directly; do **not** add them to the networked `ShopItemData`).
- DOTween or third-party tweening (use a simple coroutine).
- Broadcast "player X bought Y" to other players (toast is requester-only).

## 3. Decisions (confirmed with user)

| Decision | Choice |
|---|---|
| Sell currency | The item's own `CurrencyType` (Token-tab → Token, Coin-tab → Coin). |
| Feedback form | Transient toast notification; success **and** failure. |
| Affordability UX | Client clamps max qty to affordable + disables buy button when broke; server still validates and shows an error on rejection. |
| Feedback transport | **Approach B** — one `[Rpc(SendTo.ClientsAndHost)]` carrying `requesterClientId`; each client shows the toast only if `LocalClientId == requesterClientId`. Matches existing shop RPC style; works on any NGO version. |

## 4. Architecture

### 4.1 Core principle
All resource mutation happens **server-side** in `ProcessBuyItem` / `ProcessSellItem`. The existing `NetworkList` replication + `ResourceChanged` event then auto-update every client's HUD. The new `ClientRpc` carries **only toast feedback**, gated to the requester (Approach B).

Today, `OnItemPurchasedClientRpc` performs the resource grant (which only takes effect on the host, since `TryAdd` is server-only, and relies on `NetworkList` replication for other clients). This design **removes** that grant from the ClientRpc and performs it server-side in `ProcessBuyItem`, making Buy consistent with Sell and leaving ClientRpc as a pure feedback channel.

### 4.2 Data flow — Buy (client initiates)

1. Player clicks Buy on an item panel → `ShopItemPanel` → `onBuy(itemId)` → `ShopPresenter.HandleBuyItem`.
2. Presenter computes `maxQty = min(stockMax, affordableQty)` where `affordableQty = Price > 0 ? owned / Price : UnlimitedBuyMax`. Opens the quantity popup clamped to `[1, maxQty]`.
3. Player confirms `qty` → `DoTransaction(item, Buy, qty)` → `shopNetwork.BuyItem(item.ResourceType, qty)`.
4. Client `Shop` (not server) → `BuyItemServerRpc(resourceType, qty)`. The server reads the requester's identity from the RPC receive params (see §5.2).
5. Server `ProcessBuyItem(resourceType, qty, requesterClientId)`:
   - Look up `ShopItem` (`Price`, `CurrencyType`, stock).
   - Sold out → feedback `FailedStock`.
   - Clamp `qty` by stock.
   - `currencyRT = item.CurrencyType.ToResourceType()`; `totalCost = Price × qty` (guarded against overflow).
   - `CanAfford({currencyRT, totalCost})`? No → feedback `FailedAfford`.
   - `TrySpend({currencyRT, totalCost})` + deduct stock + `TryAdd(resourceType, qty)`.
   - Feedback `Success` with deltas: `gained=(resourceType, qty)`, `spent=(currencyType, totalCost)`.
   - Fire `OnTransactionFeedbackClientRpc(result, gainedType, gainedAmt, spentType, spentAmt, requesterClientId)`.
6. Each client: if `LocalClientId == requesterClientId` → `presenter.ShowFeedback(...)` → `view.ShowToast(...)` → `TransactionToast.Show(...)`.

### 4.3 Data flow — Buy (host initiates)
Same as above, except `BuyItem` calls `ProcessBuyItem(..., NetworkManager.ServerClientId)` directly (no ServerRpc). The feedback ClientRpc (`ClientsAndHost`) fires on the host; `LocalClientId == ServerClientId` → toast shows.

### 4.4 Data flow — Sell
Mirror of Buy. `ProcessSellItem(resourceType, qty, requesterClientId)`:
- Look up `ShopItem` (`Sell`, `CurrencyType`).
- `isSellable`? No → `FailedNotSellable` (defensive; button is client-disabled).
- `TrySpend({resourceType, qty})`? No → `FailedNoResource`.
- `received = Sell × qty`; `TryAdd(item.CurrencyType.ToResourceType(), received)`.
- Feedback `Success`: `gained=(currencyType, received)`, `spent=(resourceType, qty)`.

## 5. Component changes

### 5.1 New files

**`Shop/ShopTxResult.cs`**
```csharp
public enum ShopTxResult { Success, FailedAfford, FailedStock, FailedNotSellable, FailedNoResource }
```

**`Shop/TransactionToast.cs`** — `MonoBehaviour`.
- Serialized: `TMP_Text label`, optional `CanvasGroup canvasGroup`, `float duration = 2f`, `Color successColor`, `Color failureColor`.
- `Show(string message, bool success)`: set text + color, activate, restart fade coroutine (show → wait `duration` → deactivate; optional alpha fade if `canvasGroup` assigned).
- Pure static `Format(ShopTxResult result, ResourceType gainedType, int gainedAmt, ResourceType spentType, int spentAmt) → (string message, bool success)`. Mirrors the `ShopQuantityPopup.FormatConfirmLabel` testable-pure pattern.

**`Shop/ShopMath.cs`** — pure helper for testability.
- `static int MaxBuyQty(int stockMax, int owned, int price)` → `min(stockMax, price > 0 ? owned / price : UnlimitedBuyMax)`, floored at 0. (`UnlimitedBuyMax` stays in `ShopPresenter` as today; pass it in as `stockMax` for unlimited items.)

### 5.2 Modified files

**`Shop/CurrencyType.cs`** — add extension:
```csharp
public static ResourceType ToResourceType(this CurrencyType c) =>
    c == CurrencyType.Token ? ResourceType.Token : ResourceType.Coin;
```

**`Shop/Shop.cs`**
- `BuyItem`/`SellItem`: thread `requesterClientId`. Non-server → ServerRpc; host → `ProcessX(..., NetworkManager.ServerClientId)` directly.
- `BuyItemServerRpc` / `SellItemServerRpc`: obtain the requester's client ID **authoritatively** via `RpcParams.Receive.SenderClientClientId` (preferred — the server must not trust a client-asserted ID). If the installed NGO build does not expose that API, fall back to passing `NetworkManager.LocalClientId` as an explicit RPC parameter; this affects **toast routing only** (resources remain server-authoritative), so the trust risk is benign. Forward the resolved ID into `ProcessX`.
- `ProcessBuyItem`: add affordability check + atomic currency spend + server-side resource grant + stock deduct (stock deduct unchanged) + feedback.
- `ProcessSellItem`: award `item.CurrencyType.ToResourceType()` instead of hardcoded `ResourceType.Coin` + feedback; add `FailedNoResource` guard.
- **Replace** `OnItemPurchasedClientRpc` and `OnItemSoldClientRpc` with a single `OnTransactionFeedbackClientRpc(int result, int gainedType, int gainedAmt, int spentType, int spentAmt, ulong requesterClientId)` (`ClientsAndHost`) → requester-only `presenter.ShowFeedback(...)`.
- `Construct(IResourceService, INetworkPool)`: after assigning `_sharedResources`, subscribe `_sharedResources.ResourceChanged`; handler refreshes affordability only when `Type == Coin || Type == Token` via `presenter.RefreshShop()`. Unsubscribe in `OnNetworkDespawn`.

**`Shop/ShopPresenter.cs`**
- `HandleBuyItem`: compute `maxQty` via `ShopMath.MaxBuyQty(stockMax, owned, item.Price)` where `owned = shopNetwork.GetResourceAmount(item.CurrencyType.ToResourceType())`; guard `maxQty <= 0`.
- `RefreshShop`: compute `ownedCurrency = shopNetwork.GetResourceAmount(currentCurrency.ToResourceType())` and pass to `view.CreateItemPanels`.
- New `ShowFeedback(ShopTxResult, ResourceType gainedType, int gainedAmt, ResourceType spentType, int spentAmt)`: format via `TransactionToast.Format`, call `view.ShowToast(message, success)`.

**`Shop/ShopView.cs`**
- `CreateItemPanels(List<ShopItem> items, int ownedCurrency, Action<string> onBuy, Action<string> onSell)`: forward `ownedCurrency` to `panel.Setup`.
- Add `[SerializeField] private TransactionToast toast;` + `ShowToast(string message, bool success)` (log a loud warning if `toast` is unassigned, mirroring the existing `quantityPopup` guard).

**`Shop/ShopItemPanel.cs`**
- `Setup(ShopItem item, int ownedCurrency, Action<string> onBuy, Action<string> onSell)`: `buyButton.interactable = !item.IsSoldOut && ownedCurrency >= item.Price`.

## 6. Toast messages

| Result | Message |
|---|---|
| `Success` (Buy) | `"+10 Wood   −50 Coin"` (`+{gained} {gainedName}   −{spent} {spentName}`) |
| `Success` (Sell) | `"+50 Coin   −10 Wood"` |
| `FailedAfford` | `"Not enough {currencyName}"` / `"Not enough {resourceName}"` (sell) |
| `FailedStock` | `"Sold out"` |
| `FailedNoResource` | `"Not enough {resourceName}"` |
| `FailedNotSellable` | `"Cannot sell this item"` |

Resource/currency names use the full enum name (`Wood`, `Coin`, `Token`, …), not the HUD abbreviation.

## 7. Scene / prefab work (non-code)

Create a toast UI element under the shop canvas (a `TMP_Text`, optionally on a panel with a `CanvasGroup`), attach `TransactionToast`, and drag it into the `toast` field of `ShopView` in the Inspector. This is called out for manual setup; it is not generated by code.

## 8. Testing (EditMode, pure functions)

Add an EditMode test assembly if none exists. Cover:
- `TransactionToast.Format` for every `ShopTxResult` (success wording, each failure wording).
- `CurrencyType.ToResourceType()` (Coin → `ResourceType.Coin`, Token → `ResourceType.Token`).
- `ShopMath.MaxBuyQty` (stock clamp, affordability clamp, `price == 0` unlimited path, `owned < price` → 0).
- Existing `ShopQuantityPopup.ClampQuantity` / `FormatConfirmLabel` continue to follow the same testable-pure pattern (no change required).

Networking/MonoBehaviour glue (RPC round-trip, toast coroutine) is verified manually in the scene.

## 9. Risks / edge cases

- `Price == 0`: `affordableQty` → `UnlimitedBuyMax`; `totalCost = 0`; `CanAfford` true; `TrySpend(0)` safe.
- Overflow on `Price × qty`: bounded by `owned / Price` when `Price > 0`; guard the multiply defensively.
- Host path: `LocalClientId == ServerClientId` → toast shows for host buyer.
- Refresh cost: only on Coin/Token change (not on every harvest), and `CreateItemPanels` reuses the existing object pool — cheap.
- `_sharedResources` null guards retained (consistent with existing code).
