# Shop Buy/Sell Resource Flow + UI Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the shop charge/award the correct currency on Buy/Sell (server-authoritative), and show a requester-only toast for success and failure, while the HUD auto-updates via the existing `ResourceChanged` pipeline.

**Architecture:** All resource mutation moves server-side into `Shop.ProcessBuyItem`/`ProcessSellItem` (Buy currently grants inside a ClientRpc — aligned to match Sell). A requester-only toast rides a `[Rpc(SendTo.ClientsAndHost)]` carrying `requesterClientId`, gated by `NetworkManager.LocalClientId == requesterClientId`. Pure, dependency-free logic (affordability clamp + toast formatting) lives in a small auto-referenced `ShopPure` assembly so it can be unit-tested; all other shop code stays in `Assembly-CSharp`.

**Tech Stack:** Unity (URP), C#, Unity Netcode for GameObjects **2.11.2** (`[Rpc(SendTo.X)]` API), VContainer DI, TextMeshPro, NUnit (Unity Test Framework, EditMode).

## Global Constraints

- **Netcode 2.11.2.** ServerRpc is `[Rpc(SendTo.Server)]`. To learn the requester, add a trailing `RpcParams rpcParams` and read `rpcParams.Receive.SenderClientId`. The client invokes the ServerRpc **without** passing `RpcParams` (NGO auto-populates it on receive); if the compiler complains, pass `default` as the final argument.
- **Feedback transport = Approach B.** One `[Rpc(SendTo.ClientsAndHost)]` carries `requesterClientId`; each client shows the toast only if `NetworkManager.LocalClientId == requesterClientId`. Host path passes `NetworkManager.ServerClientId`.
- **Resources are match-wide shared and server-authoritative** (`SharedResourceManager`). Never mutate resources on a non-server client; rely on `NetworkList` replication + `ResourceChanged`.
- **Buy** = spend `Price × qty` of `item.CurrencyType`, grant `qty` of `item.ResourceType`, deduct stock. **Sell** = spend `qty` of `item.ResourceType`, grant `Sell × qty` of `item.CurrencyType`.
- **Assembly layout.** Existing shop scripts compile into `Assembly-CSharp` (no asmdef exists). New pure helpers go in a NEW auto-referenced assembly `ShopPure`, in its own subfolder `Assets/_Game/Scripts/Shop/Pure/` so it does NOT swallow the existing shop files. Pure types use the **global namespace** (no `using` needed by consumers). `CurrencyType`→`ResourceType` mapping stays in `Assembly-CSharp` because it depends on `ResourceType`.
- **Toast text uses ASCII** `-` (not Unicode minus) and full enum names via `.ToString()` (e.g. `Wood`, `Coin`, `Token`).
- **Commits** follow the repo's bracketed style: `[feat] …`, `[test] …`, `[refactor] …`.

---

## File Structure

**Created**
- `Assets/_Game/Scripts/Shop/Pure/ShopPure.asmdef` — auto-referenced assembly for testable pure logic.
- `Assets/_Game/Scripts/Shop/Pure/ShopTxResult.cs` — transaction outcome enum (global ns).
- `Assets/_Game/Scripts/Shop/Pure/ShopMath.cs` — pure affordability clamp (global ns).
- `Assets/_Game/Scripts/Shop/Pure/ShopFeedbackFormat.cs` — pure toast formatter + `ShopFeedback` struct (global ns).
- `Assets/_Game/Tests/ShopPure.Tests.asmdef` — EditMode test assembly referencing `ShopPure`.
- `Assets/_Game/Tests/ShopMathTests.cs` — NUnit tests for `ShopMath`.
- `Assets/_Game/Tests/ShopFeedbackFormatTests.cs` — NUnit tests for `ShopFeedbackFormat`.
- `Assets/_Game/Scripts/Shop/TransactionToast.cs` — MonoBehaviour toast (Assembly-CSharp).

**Modified**
- `Assets/_Game/Scripts/Shop/CurrencyType.cs` — add `ToResourceType()` extension.
- `Assets/_Game/Scripts/Shop/ShopItemPanel.cs` — `Setup` takes `ownedCurrency`; disables Buy when unaffordable.
- `Assets/_Game/Scripts/Shop/ShopView.cs` — `CreateItemPanels` takes `ownedCurrency`; add `toast` field + `ShowToast`.
- `Assets/_Game/Scripts/Shop/ShopPresenter.cs` — affordability clamp via `ShopMath`; `RefreshShop` passes owned currency; `ShowFeedback`.
- `Assets/_Game/Scripts/Shop/Shop.cs` — currency spend/award, requester feedback RPC, `ResourceChanged`→refresh; remove old grant/log RPCs.

---

## Task 1: Pure helpers assembly + unit tests

**Files:**
- Create: `Assets/_Game/Scripts/Shop/Pure/ShopPure.asmdef`
- Create: `Assets/_Game/Scripts/Shop/Pure/ShopTxResult.cs`
- Create: `Assets/_Game/Scripts/Shop/Pure/ShopMath.cs`
- Create: `Assets/_Game/Scripts/Shop/Pure/ShopFeedbackFormat.cs`
- Create: `Assets/_Game/Tests/ShopPure.Tests.asmdef`
- Create: `Assets/_Game/Tests/ShopMathTests.cs`
- Create: `Assets/_Game/Tests/ShopFeedbackFormatTests.cs`

**Interfaces:**
- Produces: `enum ShopTxResult { Success, FailedAfford, FailedStock, FailedNotSellable, FailedNoResource }`; `static int ShopMath.MaxBuyQty(int stockMax, int owned, int price)`; `readonly struct ShopFeedback { string Message; bool Success; }`; `static ShopFeedback ShopFeedbackFormat.Format(ShopTxResult, string gainedName, int gainedAmt, string spentName, int spentAmt)`. All global namespace.

- [ ] **Step 1: Create the ShopPure assembly definition**

Create `Assets/_Game/Scripts/Shop/Pure/ShopPure.asmdef` (the `Pure/` subfolder is critical — an asmdef covers its own folder recursively, so putting it directly in `Shop/` would pull all existing shop files into this assembly and break compilation):

```json
{
    "name": "ShopPure",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`autoReferenced: true` means `Assembly-CSharp` (where `Shop.cs` etc. live) automatically references `ShopPure`, so the shop code can use these types with no `using`.

- [ ] **Step 2: Create the test assembly definition**

Create `Assets/_Game/Tests/ShopPure.Tests.asmdef`:

```json
{
    "name": "ShopPure.Tests",
    "rootNamespace": "",
    "references": [
        "ShopPure",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Write the failing tests for ShopMath**

Create `Assets/_Game/Tests/ShopMathTests.cs`:

```csharp
using NUnit.Framework;

public class ShopMathTests
{
    [Test] public void AffordLimitedByStock() =>
        Assert.AreEqual(5, ShopMath.MaxBuyQty(stockMax: 5, owned: 100, price: 10));

    [Test] public void AffordLimitedByCurrency() =>
        Assert.AreEqual(9, ShopMath.MaxBuyQty(stockMax: 10, owned: 95, price: 10));

    [Test] public void ExactlyAffordable() =>
        Assert.AreEqual(10, ShopMath.MaxBuyQty(stockMax: 10, owned: 100, price: 10));

    [Test] public void CannotAffordOne() =>
        Assert.AreEqual(0, ShopMath.MaxBuyQty(stockMax: 10, owned: 5, price: 10));

    [Test] public void FreeItemLimitedByStock() =>
        Assert.AreEqual(10, ShopMath.MaxBuyQty(stockMax: 10, owned: 0, price: 0));

    [Test] public void NegativeStockClampedToZero() =>
        Assert.AreEqual(0, ShopMath.MaxBuyQty(stockMax: -5, owned: 100, price: 10));

    [Test] public void NegativeOwnedClampedToZero() =>
        Assert.AreEqual(0, ShopMath.MaxBuyQty(stockMax: 10, owned: -1, price: 10));
}
```

- [ ] **Step 4: Write the failing tests for ShopFeedbackFormat**

Create `Assets/_Game/Tests/ShopFeedbackFormatTests.cs`:

```csharp
using NUnit.Framework;

public class ShopFeedbackFormatTests
{
    [Test] public void SuccessBuy()
    {
        var f = ShopFeedbackFormat.Format(ShopTxResult.Success, "Wood", 10, "Coin", 50);
        Assert.IsTrue(f.Success);
        Assert.AreEqual("+10 Wood / -50 Coin", f.Message);
    }

    [Test] public void SuccessSell()
    {
        var f = ShopFeedbackFormat.Format(ShopTxResult.Success, "Coin", 50, "Wood", 10);
        Assert.AreEqual("+50 Coin / -10 Wood", f.Message);
    }

    [Test] public void FailedAffordUsesSpentName()
    {
        var f = ShopFeedbackFormat.Format(ShopTxResult.FailedAfford, "", 0, "Coin", 0);
        Assert.IsFalse(f.Success);
        Assert.AreEqual("Not enough Coin", f.Message);
    }

    [Test] public void FailedNoResourceUsesSpentName()
    {
        var f = ShopFeedbackFormat.Format(ShopTxResult.FailedNoResource, "", 0, "Wood", 0);
        Assert.AreEqual("Not enough Wood", f.Message);
    }

    [Test] public void FailedStock() =>
        Assert.AreEqual("Sold out",
            ShopFeedbackFormat.Format(ShopTxResult.FailedStock, "", 0, "", 0).Message);

    [Test] public void FailedNotSellable() =>
        Assert.AreEqual("Cannot sell this item",
            ShopFeedbackFormat.Format(ShopTxResult.FailedNotSellable, "", 0, "", 0).Message);
}
```

- [ ] **Step 5: Run tests to verify they fail**

In Unity: **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**.
Expected: FAIL / compile error — `ShopMath` and `ShopFeedbackFormat` are not defined yet (and `ShopTxResult` does not exist).

- [ ] **Step 6: Implement ShopTxResult**

Create `Assets/_Game/Scripts/Shop/Pure/ShopTxResult.cs`:

```csharp
/// <summary>Outcome of a shop Buy/Sell transaction; drives toast feedback.</summary>
public enum ShopTxResult
{
    Success,
    FailedAfford,
    FailedStock,
    FailedNotSellable,
    FailedNoResource,
}
```

- [ ] **Step 7: Implement ShopMath**

Create `Assets/_Game/Scripts/Shop/Pure/ShopMath.cs`:

```csharp
using System;

/// <summary>Pure shop math. No Unity dependency — unit-testable.</summary>
public static class ShopMath
{
    /// <summary>
    /// Max units a player can buy now, clamped by BOTH remaining stock and what the
    /// player can afford. Floored at 0.
    /// </summary>
    /// <param name="stockMax">Stock cap. For unlimited items, pass the caller's safety cap.</param>
    /// <param name="owned">Currency the player holds (in the item's CurrencyType).</param>
    /// <param name="price">Per-unit price. &lt;= 0 means free (affordability capped only by stockMax).</param>
    public static int MaxBuyQty(int stockMax, int owned, int price)
    {
        if (stockMax < 0) stockMax = 0;
        if (owned < 0) owned = 0;
        int affordable = price > 0 ? owned / price : int.MaxValue;
        return Math.Min(affordable, stockMax);
    }
}
```

- [ ] **Step 8: Implement ShopFeedbackFormat (+ ShopFeedback)**

Create `Assets/_Game/Scripts/Shop/Pure/ShopFeedbackFormat.cs`:

```csharp
/// <summary>Toast payload: text + whether it represents success.</summary>
public readonly struct ShopFeedback
{
    public readonly string Message;
    public readonly bool Success;

    public ShopFeedback(string message, bool success)
    {
        Message = message;
        Success = success;
    }
}

/// <summary>
/// Pure toast-message formatter. Callers pass resolved resource/currency NAMES as
/// strings (no dependency on Assembly-CSharp enums) so this stays unit-testable.
/// On FailedAfford / FailedNoResource, <paramref name="spentName"/> is the name of
/// the resource/currency the player lacked.
/// </summary>
public static class ShopFeedbackFormat
{
    public static ShopFeedback Format(
        ShopTxResult result,
        string gainedName,
        int gainedAmt,
        string spentName,
        int spentAmt)
    {
        switch (result)
        {
            case ShopTxResult.Success:
                return new ShopFeedback($"+{gainedAmt} {gainedName} / -{spentAmt} {spentName}", true);
            case ShopTxResult.FailedAfford:
            case ShopTxResult.FailedNoResource:
                return new ShopFeedback($"Not enough {spentName}", false);
            case ShopTxResult.FailedStock:
                return new ShopFeedback("Sold out", false);
            case ShopTxResult.FailedNotSellable:
                return new ShopFeedback("Cannot sell this item", false);
            default:
                return new ShopFeedback("Transaction failed", false);
        }
    }
}
```

- [ ] **Step 9: Run tests to verify they pass**

Unity Test Runner ▸ EditMode ▸ Run All.
Expected: PASS — 13 tests green (`ShopMathTests` ×7, `ShopFeedbackFormatTests` ×6). Whole project still compiles (no changes to existing files).

- [ ] **Step 10: Commit**

```bash
git add Assets/_Game/Scripts/Shop/Pure Assets/_Game/Tests
git commit -m "[test] add ShopPure pure helpers + editmode tests"
```

---

## Task 2: CurrencyType → ResourceType mapping

**Files:**
- Modify: `Assets/_Game/Scripts/Shop/CurrencyType.cs`

**Interfaces:**
- Produces: `public static ResourceType CurrencyTypeExtensions.ToResourceType(this CurrencyType)` — Coin → `ResourceType.Coin`, Token → `ResourceType.Token`.
- Consumed by: Task 4 (ShopPresenter), Task 5 (Shop).

- [ ] **Step 1: Replace the file contents**

`Assets/_Game/Scripts/Shop/CurrencyType.cs` currently holds only the enum. Replace the whole file with:

```csharp
using DungeonBuilder.Core.Enums;

public enum CurrencyType
{
    Coin,
    Token,
}

/// <summary>Maps a shop currency to its tracked ResourceType.</summary>
public static class CurrencyTypeExtensions
{
    public static ResourceType ToResourceType(this CurrencyType currency) =>
        currency == CurrencyType.Token ? ResourceType.Token : ResourceType.Coin;
}
```

- [ ] **Step 2: Verify it compiles**

In Unity, let it recompile; no errors expected.
Expected: console clean. (No unit test — this maps two enums where `ResourceType` lives in `Assembly-CSharp`; it is verified manually in Task 6.)

- [ ] **Step 3: Commit**

```bash
git add Assets/_Game/Scripts/Shop/CurrencyType.cs
git commit -m "[feat] map CurrencyType to ResourceType"
```

---

## Task 3: TransactionToast UI component

**Files:**
- Create: `Assets/_Game/Scripts/Shop/TransactionToast.cs`

**Interfaces:**
- Produces: `public void TransactionToast.Show(string message, bool success)` — shows text, colors it, auto-hides after `duration`. Consumed by `ShopView.ShowToast` (Task 4).

- [ ] **Step 1: Create the component**

Create `Assets/_Game/Scripts/Shop/TransactionToast.cs`:

```csharp
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Transient toast for shop transaction feedback (success/failure). Shows, waits,
/// then hides. A "dumb" view: ShopPresenter formats the message; this only displays it.
/// Optional CanvasGroup drives a simple alpha fade; if unassigned it just show/hides.
/// </summary>
public class TransactionToast : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float duration = 2f;
    [SerializeField] private Color successColor = Color.white;
    [SerializeField] private Color failureColor = new Color(1f, 0.35f, 0.35f);

    private Coroutine _routine;

    private void Awake()
    {
        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    public void Show(string message, bool success)
    {
        if (label != null)
        {
            label.text = message;
            label.color = success ? successColor : failureColor;
        }

        gameObject.SetActive(true);
        SetAlpha(1f);

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(duration);
        SetAlpha(0f);
        gameObject.SetActive(false);
    }

    private void SetAlpha(float a)
    {
        if (canvasGroup != null) canvasGroup.alpha = a;
    }
}
```

- [ ] **Step 2: Verify it compiles**

Unity recompiles; no errors expected.
Expected: console clean. (Wired into the scene in Task 6.)

- [ ] **Step 3: Commit**

```bash
git add Assets/_Game/Scripts/Shop/TransactionToast.cs
git commit -m "[feat] add TransactionToast feedback component"
```

---

## Task 4: Affordability wiring (ShopItemPanel + ShopView + ShopPresenter)

**Files:**
- Modify: `Assets/_Game/Scripts/Shop/ShopItemPanel.cs`
- Modify: `Assets/_Game/Scripts/Shop/ShopView.cs`
- Modify: `Assets/_Game/Scripts/Shop/ShopPresenter.cs`

**Interfaces:**
- Consumes: `ShopMath.MaxBuyQty` (Task 1), `CurrencyType.ToResourceType()` (Task 2), `ShopFeedbackFormat.Format` + `ShopFeedback` (Task 1), `TransactionToast.Show` (Task 3).
- Produces: `ShopItemPanel.Setup(ShopItem, int ownedCurrency, Action<string>, Action<string>)`; `ShopView.CreateItemPanels(items, int ownedCurrency, onBuy, onSell)` + `ShopView.ShowToast(string, bool)` + `[SerializeField] TransactionToast toast`; `ShopPresenter.ShowFeedback(ShopTxResult, ResourceType, int, ResourceType, int)`.

- [ ] **Step 1: Update ShopItemPanel.Setup to gate the Buy button on affordability**

In `Assets/_Game/Scripts/Shop/ShopItemPanel.cs`, change the `Setup` signature and the buy-button line. The current signature is `public void Setup(ShopItem item, Action<string> onBuyCallback, Action<string> onSellCallback = null)`. Replace it with:

```csharp
public void Setup(ShopItem item, int ownedCurrency, Action<string> onBuyCallback, Action<string> onSellCallback = null)
{
    itemId = item.Id;
    onBuy = onBuyCallback;
    onSell = onSellCallback;

    nameText.text = item.Name;

    // Buy enabled only if in stock AND the player can afford at least one.
    buyButton.interactable = !item.IsSoldOut && ownedCurrency >= item.Price;

    // Sell button: disable + auto-dim when item is not sellable
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
        Debug.LogWarning(
            $"[ShopItemPanel] '{name}': sellButton field is NOT assigned in Inspector — Sell action sẽ không fire. " +
            $"Kéo Button 'Sell' vào field _sellButton để fix.", this);
    }

    buyButton.onClick.RemoveAllListeners();
    buyButton.onClick.AddListener(() => onBuy?.Invoke(itemId));
}
```

- [ ] **Step 2: Add the toast field + ShowToast to ShopView**

In `Assets/_Game/Scripts/Shop/ShopView.cs`, add a serialized `toast` field next to the existing `quantityPopup` field (inside the class, near the other `[SerializeField]` declarations):

```csharp
[SerializeField] private TransactionToast toast;
```

Add a `ShowToast` method (place it near `HideQuantityPopup`):

```csharp
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
```

- [ ] **Step 3: Thread ownedCurrency through CreateItemPanels**

In the same `ShopView.cs`, change the `CreateItemPanels` signature to accept `ownedCurrency` and pass it to both `Setup` calls:

```csharp
public void CreateItemPanels(List<ShopItem> items, int ownedCurrency, System.Action<string> onBuyCallback = null, System.Action<string> onSellCallback = null)
{
    foreach (var panel in panelPool)
    {
        panel.gameObject.SetActive(false);
    }

    for (int i = 0; i < items.Count; i++)
    {
        if (i < panelPool.Count)
        {
            panelPool[i].gameObject.SetActive(true);
            panelPool[i].Setup(items[i], ownedCurrency, onBuyCallback, onSellCallback);
        }
        else
        {
            var panelObj = GameObject.Instantiate(itemPanelPrefab.gameObject, itemPanelContainer);
            var shopItemPanel = panelObj.GetComponent<ShopItemPanel>();

            shopItemPanel.Setup(items[i], ownedCurrency, onBuyCallback, onSellCallback);
            panelPool.Add(shopItemPanel);
        }
    }
}
```

- [ ] **Step 4: Update ShopPresenter constructor + RefreshShop to pass owned currency**

In `Assets/_Game/Scripts/Shop/ShopPresenter.cs`, the constructor currently calls `view.CreateItemPanels(items, HandleBuyItem, HandleSellItem)` and `RefreshShop()` does the same. Replace the constructor's panel-creation block and `RefreshShop` so they compute and pass owned currency. Replace the existing `RefreshShop` method with:

```csharp
public void RefreshShop()
{
    var items = model.GetItemsByType(currentCurrency);
    int ownedCurrency = shopNetwork != null
        ? shopNetwork.GetResourceAmount(currentCurrency.ToResourceType())
        : 0;
    view.CreateItemPanels(items, ownedCurrency, HandleBuyItem, HandleSellItem);
}
```

And in the constructor, replace the lines:

```csharp
var items = model.GetItemsByType(currentCurrency);
view.CreateItemPanels(items, HandleBuyItem, HandleSellItem);
```

with:

```csharp
RefreshShop();
```

(The constructor already wires `view.OnTabChanged += HandleTabChanged;` above this — keep that line as-is.)

- [ ] **Step 5: Clamp buy max quantity by affordability in HandleBuyItem**

In the same `ShopPresenter.cs`, replace the body of `HandleBuyItem` with:

```csharp
private void HandleBuyItem(string itemId)
{
    Debug.Log($"Attempting to buy item: {itemId}");

    var item = FindItem(itemId);
    if (item == null)
    {
        Debug.LogWarning($"Item not found: {itemId}");
        return;
    }

    if (item.IsSoldOut)
    {
        Debug.LogWarning($"Item is sold out: {itemId}");
        return;
    }

    int stockMax = item.isUnlimited ? UnlimitedBuyMax : item.RemainingQuantity;
    if (stockMax <= 0)
    {
        Debug.LogWarning($"Cannot buy — no stock: {itemId}");
        return;
    }

    int owned = shopNetwork != null
        ? shopNetwork.GetResourceAmount(item.CurrencyType.ToResourceType())
        : 0;
    int maxQty = ShopMath.MaxBuyQty(stockMax, owned, item.Price);
    if (maxQty <= 0)
    {
        // Can't afford even one. Buy button should already be disabled; this guards races.
        return;
    }

    view.ShowQuantityPopup(item.Name, ShopAction.Buy, maxQty, item.Price,
        qty => DoTransaction(item, ShopAction.Buy, qty));
}
```

- [ ] **Step 6: Add ShowFeedback to ShopPresenter**

In the same `ShopPresenter.cs`, add this public method (e.g. right after `DoTransaction`):

```csharp
/// <summary>Called (via Shop feedback RPC) on the requester's client to show a toast.</summary>
public void ShowFeedback(ShopTxResult result, ResourceType gainedType, int gainedAmt, ResourceType spentType, int spentAmt)
{
    var feedback = ShopFeedbackFormat.Format(
        result,
        gainedType.ToString(),
        gainedAmt,
        spentType.ToString(),
        spentAmt);
    view.ShowToast(feedback.Message, feedback.Success);
}
```

`ShopPresenter.cs` already has `using DungeonBuilder.Core.Enums;` (for `ResourceType`). `ShopMath`, `ShopFeedbackFormat`, `ShopTxResult` are in the global namespace via the `ShopPure` assembly, so no `using` is required.

- [ ] **Step 7: Verify it compiles**

Unity recompiles.
Expected: console clean. (`Shop` still calls the OLD `CreateItemPanels`/`BuyItem`? No — `Shop` calls `presenter.RefreshShop()`, which we updated. `Shop.DoTransaction`→`shopNetwork.BuyItem`/`SellItem` signatures are unchanged in this task.) The buy flow now clamps by affordability and disables the button when broke; feedback toast isn't triggered yet until Task 5 wires the RPC.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Game/Scripts/Shop/ShopItemPanel.cs Assets/_Game/Scripts/Shop/ShopView.cs Assets/_Game/Scripts/Shop/ShopPresenter.cs
git commit -m "[feat] clamp shop buy by affordability + toast hookup"
```

---

## Task 5: Shop networking rewrite (currency spend/award + requester feedback)

**Files:**
- Modify: `Assets/_Game/Scripts/Shop/Shop.cs`

**Interfaces:**
- Consumes: `CurrencyType.ToResourceType()` (Task 2); `presenter.ShowFeedback` (Task 4); existing `IResourceService` (`CanAfford`/`TrySpend`/`TryAdd`), `ResourceCost`, `ResourceType`.
- Produces: server-authoritative Buy (spend currency, grant resource) and Sell (spend resource, grant `item.CurrencyType`); requester-only toast via `OnTransactionFeedbackClientRpc`.

- [ ] **Step 1: Replace BuyItem + its ServerRpc + ProcessBuyItem**

In `Assets/_Game/Scripts/Shop/Shop.cs`, replace the three methods `BuyItem`, `BuyItemServerRpc`, and `ProcessBuyItem` (and delete the old `OnItemPurchasedClientRpc`) with the block below. The grant moves server-side; the old ClientRpc grant is removed.

```csharp
public void BuyItem(ResourceType resourceType, int quantity)
{
    if (quantity <= 0)
        return;

    if (!IsServer)
    {
        // Client omits RpcParams; NGO auto-populates SenderClientId on the server.
        // If the compiler requires the argument, pass `default`.
        BuyItemServerRpc(resourceType, quantity);
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
```

- [ ] **Step 2: Replace SellItem + its ServerRpc + ProcessSellItem (and delete old OnItemSoldClientRpc)**

In the same file, replace `SellItem`, `SellItemServerRpc`, `ProcessSellItem` with:

```csharp
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
```

- [ ] **Step 3: Add SendFeedback + OnTransactionFeedbackClientRpc + SafeMultiply**

In the same file, add these three members (e.g. where the old ClientRpc methods were):

```csharp
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
```

- [ ] **Step 4: Subscribe to ResourceChanged in Construct + unsubscribe in OnNetworkDespawn**

Replace the existing `Construct` method with:

```csharp
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
```

Replace the existing `OnNetworkDespawn` with (adds the unsubscribe):

```csharp
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
```

- [ ] **Step 5: Verify it compiles + tests still pass**

Unity recompiles; then Test Runner ▸ EditMode ▸ Run All.
Expected: console clean; the 13 ShopPure tests still pass. `Shop.cs` already imports `Unity.Netcode` (for `RpcParams`), `Assets._Game.Scripts.Data` (`ResourceCost`), and `DungeonBuilder.Core.Enums` (`ResourceType`) — confirm no missing `using`. `ShopTxResult` is global-namespace (ShopPure) — no `using` needed.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Game/Scripts/Shop/Shop.cs
git commit -m "[feat] server-authoritative shop currency flow + requester toast"
```

---

## Task 6: Scene wiring + manual verification

**Files:**
- Modify (scene/prefab, non-code): the Shop UI in `Assets/Scenes/SampleScene.unity`.

This task is manual UI setup + multiplayer verification. There is no automated test for networking/MonoBehaviour glue.

- [ ] **Step 1: Create the toast UI element**

In the Shop UI canvas: create a child GameObject (e.g. `TransactionToast`) with a `TMP_Text` showing a placeholder, optionally on a panel that has a `CanvasGroup`. Add the `TransactionToast` component to it and wire its `label` (and `canvasGroup` if used). Leave it inactive (the script hides itself in `Awake`).

- [ ] **Step 2: Wire the toast into ShopView**

Select the Shop GameObject. In the `Shop` component's `view` (ShopView) section, drag the new `TransactionToast` object into the `toast` field.

- [ ] **Step 3: Verify Buy deducts currency and grants resource (host)**

Enter Play mode (host). Note starting Coin/Token/Wood. Open the shop, buy a Coin-tab item (e.g. Wood, qty 2).
Expected:
- Coin decreases by `Price × 2`.
- Wood increases by 2.
- HUD Coin/Wood update immediately.
- Toast shows `+2 Wood / -<cost> Coin` on the host only.

- [ ] **Step 4: Verify Sell awards the item's currency**

Sell a sellable resource (qty 2).
Expected:
- Resource decreases by 2.
- The item's `CurrencyType` increases by `Sell × 2` (Token-tab items give **Token**, not Coin).
- Toast shows `+<received> Coin / -2 Wood` (or `…Token…` for a Token-tab item).

- [ ] **Step 5: Verify affordability UX**

With Coin below an item's Price: its Buy button is **disabled**. Raise Coin (e.g. via another sell): the button re-enables. Try to buy more than affordable via the popup — the max is clamped.

- [ ] **Step 6: Verify requester-only feedback (host + client)**

Run a host + one client (NetworkDebugUI / ParrelSync). With the **client**, open the shop and buy an item.
Expected:
- Toast appears **only on the client** that clicked.
- Both clients' HUD update (shared resources replicate).
- Host clicking shows the toast only on the host.

- [ ] **Step 7: Verify failure toast**

On the client, attempt to sell more of a resource than owned (force via popup if the clamp allows a value above holdings, or sell down to 0 then sell again).
Expected: toast `Not enough <resource>` on the requester only; no resource change.

- [ ] **Step 8: Commit the scene/prefab changes**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "[feat] wire shop transaction toast into scene"
```

---

## Self-Review

**Spec coverage** (spec § vs. task):
- §4.2/4.3 Buy data flow (spend currency, grant resource, stock, feedback, requester gating) → Task 5 Step 1 + Step 3.
- §4.4 Sell data flow (spend resource, grant `item.CurrencyType`, feedback) → Task 5 Step 2.
- §5.1 `ShopTxResult` / `ShopMath` / `ShopFeedbackFormat`(+`ShopFeedback`) → Task 1.
- §5.2 `CurrencyType.ToResourceType()` → Task 2.
- §5.2 `TransactionToast` → Task 3.
- §5.2 `ShopItemPanel` affordability + `ShopView.CreateItemPanels`/`toast`/`ShowToast` + `ShopPresenter` clamp/`RefreshShop`/`ShowFeedback` → Task 4.
- §5.2 `Shop` Construct subscription + `OnNetworkFeedbackClientRpc` replacing the two old RPCs → Task 5 Step 3 + Step 4.
- §6 toast messages → Task 1 (Format) + verified Task 6.
- §7 scene wiring → Task 6 Step 1–2.
- §8 tests (`MaxBuyQty`, `Format`, mapping) → Task 1 covers `MaxBuyQty` + `Format`; `CurrencyType.ToResourceType()` is verified manually (depends on `ResourceType` in `Assembly-CSharp`, not referenceable by a test assembly) — documented in Task 2 Step 2.
- §9 edge cases (`Price == 0` → `SafeMultiply` returns 0, `TrySpend(0)` is a no-op; overflow guarded; host path via `ServerClientId`) → Task 5.

**Placeholder scan:** none — every code step contains complete code; manual steps describe exact actions and expected results.

**Type consistency:** `ShopItemPanel.Setup(ShopItem, int, Action<string>, Action<string>)` matches the call in `ShopView.CreateItemPanels` (Task 4 Step 3). `ShopView.CreateItemPanels(items, int ownedCurrency, …)` matches `ShopPresenter.RefreshShop` (Task 4 Step 4). `ShopPresenter.ShowFeedback(ShopTxResult, ResourceType, int, ResourceType, int)` matches the call in `Shop.OnTransactionFeedbackClientRpc` (Task 5 Step 3). `SendFeedback` params match both `ProcessBuyItem`/`ProcessSellItem` call sites. `ShopMath.MaxBuyQty(int,int,int)` matches the test signatures and the `HandleBuyItem` call. `ShopFeedbackFormat.Format(ShopTxResult,string,int,string,int)` matches its tests and `ShowFeedback`.
