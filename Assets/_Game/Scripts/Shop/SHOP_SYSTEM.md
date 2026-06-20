# Shop System

Tài liệu mô tả kiến trúc, luồng dữ liệu và UI của hệ thống Shop, bao gồm tính năng
**Quantity Popup** (nhập số lượng khi Buy/Sell).

> Quy ước: comment & tài liệu viết bằng tiếng Việt, identifier bằng tiếng Anh (theo
> style hiện tại của project).

---

## 1. Tổng quan

Hệ thống Shop cho phép người chơi **mua (Buy)** và **bán (Sell)** tài nguyên. Sử dụng:

- **MVP pattern** (Model–View–Presenter) để tách logic khỏi UI.
- **Unity Netcode for GameObjects** — **server-authoritative**: client gửi RPC lên
  server, server xử lý & đồng bộ dữ liệu qua `NetworkList`.

```
┌────────────┐   callback(itemId)   ┌───────────────┐   BuyItem/SellItem(rt, qty)   ┌────────┐
│ ShopItemPanel│ ──────────────────▶ │ ShopPresenter │ ───────────────────────────▶ │  Shop  │
│  (View/UI)  │ ◀────────────────── │ (mediator)    │ ◀─────────────────────────── │(Netcode)│
└────────────┘    Setup(item,…)     └───────────────┘       RefreshShop / data       └────────┘
        ▲                                                        │
        │ render                                                  │ reads/writes
        │                                                         ▼
┌────────────┐                                            ┌───────────────┐
│  ShopView  │                                            │   ShopModel   │  ◀── ShopItem (ScriptableObject)
│ (panel,    │                                            │ (list items)  │
│  tabs,     │                                            └───────────────┘
│  pooling,  │
│  popup)    │
└────────────┘
```

---

## 2. Các class

| Class | Loại | Vai trò |
|---|---|---|
| `Shop` | `NetworkBehaviour` | Entry point + mạng. Giữ `NetworkList<ShopItemData>`, xử lý Buy/Sell trên server qua RPC, mở/đóng shop khi player va chạm. |
| `ShopModel` | `[Serializable]` POCO | Nguồn dữ liệu tĩnh: `List<ShopItem> items`, lọc theo `CurrencyType`. |
| `ShopView` | `[Serializable]` POCO | Quản lý UI shop: panel, tabs (Coin/Token), object-pool các item panel, popup nhập số lượng. |
| `ShopPresenter` | `[Serializable]` POCO | Mediator giữa View ↔ Model ↔ Shop(network). Xử lý click Buy/Sell, đổi tab. |
| `ShopItem` | `ScriptableObject` | Định nghĩa 1 món: giá, giá bán, có bán được không, loại tiền, số lượng tồn, loại tài nguyên. |
| `ShopItemPanel` | `MonoBehaviour` | UI của 1 item: tên, giá, nút Buy, nút Sell. |
| `ShopItemData` | `struct : IEquatable` | Dữ liệu đồng bộ qua network: `ResourceType`, `RemainingQuantity`, `Sell`. |

### 2.1. `ShopItem` (ScriptableObject)

```csharp
public string Id;
public string Name;
public int Price;            // giá mua (hiện CHƯA được trừ — xem mục 6)
public int Sell;             // giá bán ra (coin nhận về / 1 unit)
public bool isUnlimited;     // true → không giới hạn tồn kho
public bool isSellable;      // true → hiện & bật nút Sell
public CurrencyType CurrencyType;  // tab nào (Coin/Token)
public int RemainingQuantity;      // tồn kho (runtime, đồng bộ network)
public ResourceType ResourceType;  // loại tài nguyên cấp/trừ khi giao dịch

public bool IsSoldOut => RemainingQuantity <= 0 && !isUnlimited;
```

### 2.2. `ShopItemData` (network)

NetworkList cần `Equals` để biết phần tử có thay đổi. `Sell` được replicate để server
có thể đổi giá bán runtime (sales/sự kiện) một cách authoritative.

---

## 3. Luồng dữ liệu

### 3.1. Buy

```
Player click Buy (ShopItemPanel)
  → ShopPresenter.HandleBuyItem(itemId)
  → [MỚI] mở Quantity Popup → onConfirm(qty)
  → Shop.BuyItem(resourceType, qty)
     ├─ client: BuyItemServerRpc(rt, qty)
     └─ server: ProcessBuyItem(rt, qty)
                · kiểm tra tồn kho, clamp qty theo stock còn lại
                · trừ RemainingQuantity (nếu không unlimited)
                · OnItemPurchasedClientRpc(rt, qty)  →  TryAdd(resourceType, qty) cho mỗi client
```

### 3.2. Sell

```
Player click Sell (ShopItemPanel)
  → ShopPresenter.HandleSellItem(itemId)
  → [MỚI] mở Quantity Popup → onConfirm(qty)
  → Shop.SellItem(resourceType, qty)
     ├─ client: SellItemServerRpc(rt, qty)
     └─ server: ProcessSellItem(rt, qty)
                · guard isSellable
                · TrySpend([{resourceType, qty}])  ← atomic: đủ mới trừ, thiếu → fail cả lô
                · TryAdd(Coin, Sell * qty)
                · OnItemSoldClientRpc(rt, coin)   (log/UI feedback)
```

> Resource của player được `SharedResourceManager` tự đồng bộ qua NetworkList → HUD tự
> update nhờ event `ResourceChanged` của `IResourceService`.

### 3.3. Mở/đóng shop

`Shop.OnTriggerEnter2D("Player")` → `ShopView.OpenShop()` (SetActive true).
`OnTriggerExit2D` → `CloseShop()` + `HideQuantityPopup()` (đóng popup nếu đang mở).

---

## 4. UI & Object Pooling

`ShopView.CreateItemPanels` dùng **object pooling** (`panelPool`): tạm ẩn toàn bộ panel
cũ, rồi bật lại/tạo mới vừa đủ theo danh sách item của tab hiện tại → tránh tạo/hủy
GameObject liên tục khi đổi tab hoặc refresh.

Tabs Coin/Token đổi `currentCurrency` trong presenter → `RefreshShop()` vẽ lại panel.

---

## 5. Tính năng: Quantity Popup (Buy/Sell theo số lượng)

### 5.1. Yêu cầu

- Ấn **Buy** hoặc **Sell** trên item → hiện **một popup overlay chung** để nhập số lượng.
- Popup có: ô nhập số lượng + nút **xác nhận** + nút **Cancel**.
  Nút xác nhận hiện **dynamic** `Buy - <tổng>$` / `Sell - <tổng>$` với tổng = số lượng × đơn giá
  (`Price` cho Buy, `Sell` cho Sell), cập nhật theo ô nhập.
- **Cancel** → ẩn popup.
- **Xác nhận** → thực hiện giao dịch với số lượng đã nhập, **popup GIỮ NGUYÊN mở** (không ẩn).
- Nhập quá mức khả thi → **clamp về max** khả thi (không báo lỗi).

### 5.2. Quyết định thiết kế (đã chốt)

| Quyết định | Lựa chọn |
|---|---|
| Kiểu popup | **Overlay chung** (1 popup dùng cho mọi item, thay vì popup riêng từng item) |
| Vượt giới hạn | **Clamp về max** khả thi (thay vì chặn + báo lỗi) |
| Phí Buy | **Giữ nguyên (miễn phí)** — Buy không trừ coin, chỉ cấp resource + trừ stock |

### 5.3. Component mới/sửa

**File mới:**
- `Shop/ShopAction.cs` — `enum { Buy, Sell }`.
- `Shop/ShopQuantityPopup.cs` (`MonoBehaviour`) — view của popup:
  - Serialize fields: `TMP_InputField quantityInput`, `Button confirmButton`,
    `Button cancelButton`, `TMP_Text confirmLabel`, `TMP_Text titleLabel` (tùy chọn),
    `TMP_Text maxLabel` (tùy chọn).
  - `Initialize()` — đặt `quantityInput.contentType = IntegerNumber`, wire 2 nút.
  - `Show(string itemName, ShopAction action, int maxQty, Action<int> onConfirm)` —
    đặt `confirmLabel` = "Buy"/"Sell", reset input = "1", `SetActive(true)`.
  - `Hide()` — `SetActive(false)`.
  - Confirm: parse → `Clamp(raw, 1, maxQty)` → ghi ngược vào input → `onConfirm(qty)`,
    **không gọi `Hide()`**.
  - Cancel: `Hide()`.
  - `static int ClampQuantity(int raw, int max)` — pure helper để unit-test.

**File sửa:**
- `Shop.cs`:
  - `BuyItem` / `SellItem` + 2 RPC + 2 `Process...` thêm tham số `int quantity`.
  - `OnItemPurchasedClientRpc` nhận `quantity` → `TryAdd(resourceType, quantity)`.
  - Thêm `public int GetResourceAmount(ResourceType)` (proxy
    `_sharedResources.GetAmount`) để presenter tính max cho Sell.
  - Server clamp Buy theo `RemainingQuantity` (authoritative).
- `ShopPresenter.cs`:
  - `HandleBuyItem` / `HandleSellItem`: thay vì gọi network ngay → mở popup.
  - Max: Buy = `isUnlimited ? 9999 : RemainingQuantity`; Sell = `GetResourceAmount(...)`.
  - Guard `maxQty <= 0` → không mở popup.
  - Thêm `DoTransaction(ShopItem item, ShopAction action, int qty)`.
- `ShopView.cs`:
  - Thêm `[SerializeField] ShopQuantityPopup quantityPopup`.
  - Thêm `ShowQuantityPopup(...)` / `HideQuantityPopup()`.
  - Gọi `HideQuantityPopup()` trong `CloseShop()`.

### 5.4. Edge cases

- Ô nhập rỗng / ký tự lạ → parse fail → mặc định `1`.
- Vượt max → clamp (client hiện số đã clamp; server cũng clamp/guard lại).
- Click Sell khi đang sở hữu `0` → presenter guard, không mở popup.
- Player đi ra khỏi shop → popup tự ẩn.
- Mua lượt 2 khi popup còn mở → clamp lại theo stock mới (server authoritative).

### 5.5. Editor wiring (làm trong Unity — code không tự tạo được UI)

1. Tạo GameObject con dưới shop panel (hoặc thành prefab) với cấu trúc:
   ```
   ShopQuantityPopup (root, gắn script ShopQuantityPopup)
   ├─ TMP_InputField        (Character Validation = Integer)
   ├─ Button - Confirm      + child TMP_Text (confirmLabel)
   ├─ Button - Cancel
   └─ TMP_Text - Title      (tùy chọn)
   ```
2. Kéo các ref vào Inspector của `ShopQuantityPopup`.
3. Kéo popup vào field `quantityPopup` mới của `ShopView` (trên object Shop).
4. **Xóa field `InputField - Amount` đang dư** trên `ShopItemPanel.prefab` (vì đã dùng overlay).

---

## 6. Lưu ý / hạn chế hiện tại

- **Buy hiện CHƯA trừ coin** (`ProcessBuyItem` chỉ giảm stock + cấp resource). Đã chốt
  giữ nguyên trong scope tính năng này. Nếu sau này muốn thu phí: dùng
  `IResourceService.CanAfford` / `TrySpend` với `Price * quantity`.
- `ResourceType` dùng cho cả "tài nguyên gameplay" (Wood, ...) lẫn "tiền" (Coin). Cẩn thận
  khi thêm loại mới — ảnh hưởng cả shop lẫn HUD/inventory.

---

## 7. Testing

- Unit-test pure helper `ShopQuantityPopup.ClampQuantity(raw, max)` (Editor tests, cùng
  kiểu `Assets/_Game/Editor/Tests/`).
- Kiểm thử mạng: host buy/sell + client nhận `OnItemPurchasedClientRpc` /
  `OnItemSoldClientRpc`; resource & stock đồng bộ qua NetworkList.
