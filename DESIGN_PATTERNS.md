# Design Patterns trong AGU301_G2

Tài liệu này mô tả các design pattern được áp dụng trong project, vị trí cụ thể trong codebase, và lý do sử dụng.

---

## 1. Model-View-Presenter (MVP)

**Dùng ở đâu:** Toàn bộ hệ thống UI và Building  
**Các file chính:**
- [Assets/_Game/Scripts/UI/Base/BasePresenter.cs](Assets/_Game/Scripts/UI/Base/BasePresenter.cs)
- [Assets/_Game/Scripts/UI/Base/BaseView.cs](Assets/_Game/Scripts/UI/Base/BaseView.cs)
- [Assets/_Game/Scripts/UI/Base/IModel.cs](Assets/_Game/Scripts/UI/Base/IModel.cs)
- [Assets/_Game/Scripts/Building/TowerModel.cs](Assets/_Game/Scripts/Building/TowerModel.cs) / [TowerPresenter.cs](Assets/_Game/Scripts/Building/TowerPresenter.cs) / [TowerView.cs](Assets/_Game/Scripts/Building/TowerView.cs)
- [Assets/_Game/Scripts/Networking/Lobby/LobbyModel.cs](Assets/_Game/Scripts/Networking/Lobby/LobbyModel.cs) / [LobbyPresenter.cs](Assets/_Game/Scripts/Networking/Lobby/LobbyPresenter.cs) / [LobbyView.cs](Assets/_Game/Scripts/Networking/Lobby/LobbyView.cs)
- [Assets/_Game/Scripts/UI/HUD/HUDModel.cs](Assets/_Game/Scripts/UI/HUD/HUDModel.cs) / [HUDPresenter.cs](Assets/_Game/Scripts/UI/HUD/HUDPresenter.cs) / [HUDView.cs](Assets/_Game/Scripts/UI/HUD/HUDView.cs)

**Cách dùng:**
- `IModel` yêu cầu mọi Model expose event `OnChanged`
- `BasePresenter<TView, TModel>` là generic abstract class — subscribe vào `Model.OnChanged` và gọi abstract `OnModelChanged()` khi dữ liệu thay đổi
- `BaseView<TPresenter>` định nghĩa `SetPresenter()` và abstract `Render()`
- **Tower**: `TowerModel` lưu trạng thái (Level, Health, Damage, Range) → `TowerPresenter` điều phối logic (upgrade/remove) → `TowerView` render UI
- **Lobby / HUD**: cùng cấu trúc — Model giữ state, Presenter xử lý business logic, View chỉ biết hiển thị

**Lý do:** Tách biệt business logic khỏi rendering, dễ test và maintain.

---

## 2. State Machine

**Dùng ở đâu:** Hệ thống AI của Enemy  
**Các file chính:**
- [Assets/_Game/Scripts/Enemy/EnemyStateMachine.cs](Assets/_Game/Scripts/Enemy/EnemyStateMachine.cs)
- [Assets/_Game/Scripts/Enemy/States/IEnemyState.cs](Assets/_Game/Scripts/Enemy/States/IEnemyState.cs)
- [Assets/_Game/Scripts/Enemy/States/MoveToCoreState.cs](Assets/_Game/Scripts/Enemy/States/MoveToCoreState.cs)
- [Assets/_Game/Scripts/Enemy/States/AttackCoreState.cs](Assets/_Game/Scripts/Enemy/States/AttackCoreState.cs)
- [Assets/_Game/Scripts/Enemy/States/AttackWallState.cs](Assets/_Game/Scripts/Enemy/States/AttackWallState.cs)
- [Assets/_Game/Scripts/Enemy/States/StunnedState.cs](Assets/_Game/Scripts/Enemy/States/StunnedState.cs)

**Cách dùng:**
- `IEnemyState` định nghĩa 3 method: `Enter(enemy)`, `Update(enemy)`, `Exit(enemy)`
- `EnemyStateMachine` giữ `currentState`, method `ChangeState()` gọi `Exit` → gán state mới → gọi `Enter`
- `BaseEnemy` tạo state machine trong `Awake()`, gọi `Update()` mỗi frame
- Mỗi state tự kiểm tra điều kiện chuyển state bên trong `Update()`

**Ví dụ luồng:** `MoveToCoreState` → phát hiện tường → `AttackWallState` → tường bị phá → `MoveToCoreState` → tới core → `AttackCoreState`

**Lý do:** Thay thế chuỗi if-else lồng nhau, mỗi state đóng gói hoàn toàn logic của mình.

---

## 3. Object Pool

**Dùng ở đâu:** Spawn/despawn networked objects (tower, enemy, projectile, drop)  
**Các file chính:**
- [Assets/_Game/Scripts/Networking/Pool/NetworkObjectPool.cs](Assets/_Game/Scripts/Networking/Pool/NetworkObjectPool.cs)
- [Assets/_Game/Scripts/Networking/Pool/INetworkPool.cs](Assets/_Game/Scripts/Networking/Pool/INetworkPool.cs)
- [Assets/_Game/Scripts/Networking/Pool/PoolEntry.cs](Assets/_Game/Scripts/Networking/Pool/PoolEntry.cs)
- [Assets/_Game/Scripts/Core/Interfaces/IPoolable.cs](Assets/_Game/Scripts/Core/Interfaces/IPoolable.cs)

**Cách dùng:**
- `NetworkObjectPool` dùng `Dictionary<hash, Queue<NetworkObject>>` để lưu pool riêng cho từng prefab
- `INetworkPool` expose `Get(prefab, position, rotation)` và `Return(networkObject)`
- `PooledPrefabInstanceHandler` implement `INetworkPrefabInstanceHandler` của Netcode — chuyển hướng instantiation qua pool
- Objects implement `IPoolable` để nhận callback `OnGetFromPool()` (reset state) và `OnReturnToPool()` (cleanup)

**Lý do:** Giảm GC allocation và chi phí instantiation trong môi trường multiplayer, đặc biệt khi spawn/despawn liên tục.

---

## 4. Strategy + Template Method

**Dùng ở đâu:** Hệ thống Tool của Player  
**Các file chính:**
- [Assets/_Game/Scripts/Player/Tools/ITool.cs](Assets/_Game/Scripts/Player/Tools/ITool.cs)
- [Assets/_Game/Scripts/Player/Tools/HarvestToolBase.cs](Assets/_Game/Scripts/Player/Tools/HarvestToolBase.cs)
- [Assets/_Game/Scripts/Player/Tools/AxeTool.cs](Assets/_Game/Scripts/Player/Tools/AxeTool.cs)
- [Assets/_Game/Scripts/Player/Tools/PickaxeTool.cs](Assets/_Game/Scripts/Player/Tools/PickaxeTool.cs)
- [Assets/_Game/Scripts/Player/Tools/BuilderTool.cs](Assets/_Game/Scripts/Player/Tools/BuilderTool.cs)
- [Assets/_Game/Scripts/Player/Tools/WeaponTool.cs](Assets/_Game/Scripts/Player/Tools/WeaponTool.cs)

**Cách dùng:**
- **Strategy**: `ITool` định nghĩa `UseAction(targetPosition)` và `CancelAction()`. `ToolController` chọn tool phù hợp lúc runtime dựa vào context click (ô trống buildable → `BuilderTool`, có target → `HarvestTool`)
- **Template Method**: `HarvestToolBase` implement `ITool` với logic chung (tìm target, animation swing, RPC lên server). `AxeTool` và `PickaxeTool` chỉ override property `ToolType`

**Lý do:** Tách biệt các hành vi tool mà không coupling. Template Method tránh duplicate code giữa các harvest tool.

---

## 5. Dependency Injection (thay Singleton)

**Dùng ở đâu:** Toàn bộ core services  
**Các file chính:**
- [Assets/_Game/Scripts/Networking/Scopes/GameLifetimeScope.cs](Assets/_Game/Scripts/Networking/Scopes/GameLifetimeScope.cs)
- [Assets/_Game/Scripts/Networking/Scopes/PlayerLifetimeScope.cs](Assets/_Game/Scripts/Networking/Scopes/PlayerLifetimeScope.cs)
- [Assets/_Game/Scripts/Core/EventBus.cs](Assets/_Game/Scripts/Core/EventBus.cs)
- [Assets/_Game/Scripts/Core/CoreManager.cs](Assets/_Game/Scripts/Core/CoreManager.cs)
- [Assets/_Game/Scripts/Building/GridManager.cs](Assets/_Game/Scripts/Building/GridManager.cs)
- [Assets/_Game/Scripts/Networking/SharedResourceManager.cs](Assets/_Game/Scripts/Networking/SharedResourceManager.cs)

**Cách dùng:**
- Dùng **VContainer** — lightweight DI container cho Unity
- `GameLifetimeScope` và `PlayerLifetimeScope` đăng ký các dependency theo scope lifetime
- Các class nhận dependency qua `[Inject] public void Construct(...)` thay vì `GetComponent` hay static instance
- `EventBus`, `GridManager`, `SharedResourceManager`, `BuildingController` được inject vào các hệ thống cần dùng

**Lý do:** Thay thế Singleton static (`Instance`), tránh hidden dependencies, dễ test và swap implementation.

---

## 6. Observer / Event Bus

**Dùng ở đâu:** Giao tiếp giữa các hệ thống không liên quan trực tiếp  
**Các file chính:**
- [Assets/_Game/Scripts/Core/EventBus.cs](Assets/_Game/Scripts/Core/EventBus.cs)
- [Assets/_Game/Scripts/Core/ResourceChanged.cs](Assets/_Game/Scripts/Core/ResourceChanged.cs)
- [Assets/_Game/Scripts/UI/Base/IModel.cs](Assets/_Game/Scripts/UI/Base/IModel.cs)

**Cách dùng:**
- `EventBus` chứa các `Action`/`Action<T>` events: `OnCoreHealthChanged`, `OnWaveStarted`, `OnEnemyKilled`, `OnGameEnded`, `OnPhaseCountdownChanged`
- Hệ thống nào cần notify → gọi `EventBus.RaiseX(...)`, hệ thống nào cần lắng nghe → subscribe vào event tương ứng
- `IModel.OnChanged` dùng pattern tương tự ở tầng MVP — Presenter subscribe, Model raise
- `SharedResourceManager.ResourceChanged` gửi event với payload `ResourceChanged` (struct chứa giá trị cũ/mới)

**Lý do:** Decoupling hoàn toàn giữa publisher và subscriber, không cần biết nhau tồn tại.

---

## 7. Factory

**Dùng ở đâu:** Tạo Tower, quản lý prefab  
**Các file chính:**
- [Assets/_Game/Scripts/Building/BuildingController.cs](Assets/_Game/Scripts/Building/BuildingController.cs)
- [Assets/_Game/Scripts/Networking/Pool/NetworkObjectPool.cs](Assets/_Game/Scripts/Networking/Pool/NetworkObjectPool.cs)
- [Assets/_Game/Scripts/Building/LaserTower.cs](Assets/_Game/Scripts/Building/LaserTower.cs)
- [Assets/_Game/Scripts/Building/SpikeTrapTower.cs](Assets/_Game/Scripts/Building/SpikeTrapTower.cs)

**Cách dùng:**
- `BuildingController.PlaceTowerServerRpc()` đóng vai factory: nhận `TowerType` enum, lookup prefab array, delegate instantiation cho `NetworkObjectPool`
- `NetworkObjectPool` đóng vai factory thứ cấp: `GetByHash()` tạo instance mới chỉ khi queue trống, ngược lại dequeue từ pool
- `BaseTower` có abstract `FireAt()` → `LaserTower` và `SpikeTrapTower` override với attack pattern riêng

**Lý do:** Tập trung logic tạo tower, dễ thêm loại tower mới mà không sửa code hiện tại.

---

## 8. Command (qua Netcode RPC)

**Dùng ở đâu:** Các hành động tower trong multiplayer  
**Các file chính:**
- [Assets/_Game/Scripts/Building/BuildingController.cs](Assets/_Game/Scripts/Building/BuildingController.cs)
- [Assets/_Game/Scripts/Building/TowerPresenter.cs](Assets/_Game/Scripts/Building/TowerPresenter.cs)

**Cách dùng:**
- `PlaceTowerServerRpc()`, `UpgradeTowerServerRpc()`, `RemoveTowerServerRpc()` là các command object — đóng gói hành động với đầy đủ tham số, serialize và gửi lên server để thực thi
- `TowerPresenter.RequestUpgrade()` / `RequestRemove()` gọi các RPC này qua `BuildingController`
- Server là authority duy nhất thực thi command — client chỉ request

**Lý do:** Mô hình command phù hợp với server-authoritative multiplayer, tách rời "yêu cầu hành động" khỏi "thực thi hành động".

---

## 9. Facade

**Dùng ở đâu:** Đơn giản hóa interface cho subsystem phức tạp  
**Các file chính:**
- [Assets/_Game/Scripts/Building/BuildingController.cs](Assets/_Game/Scripts/Building/BuildingController.cs)
- [Assets/_Game/Scripts/Player/Tools/ToolController.cs](Assets/_Game/Scripts/Player/Tools/ToolController.cs)

**Cách dùng:**
- `BuildingController` cung cấp một interface duy nhất (`RequestPlaceTower`, `RequestUpgradeTower`, `RequestRemoveTower`) cho mọi tower operation — bên trong tự lo validation, cost check, pool management, network RPC
- `ToolController` route input đến đúng tool dựa trên context click, che giấu toàn bộ logic phân luồng khỏi caller

**Lý do:** Giảm độ phức tạp mà các hệ thống bên ngoài phải hiểu.

---

## 10. Repository

**Dùng ở đâu:** Truy cập và quản lý dữ liệu game state  
**Các file chính:**
- [Assets/_Game/Scripts/Core/ResourceStore.cs](Assets/_Game/Scripts/Core/ResourceStore.cs)
- [Assets/_Game/Scripts/Networking/SharedResourceManager.cs](Assets/_Game/Scripts/Networking/SharedResourceManager.cs)
- [Assets/_Game/Scripts/Building/GridManager.cs](Assets/_Game/Scripts/Building/GridManager.cs)

**Cách dùng:**
- `ResourceStore`: in-memory dictionary cho resource amounts — expose `CanAfford()`, `TrySpend()`, `TryAdd()` thay vì để caller tự manipulate data
- `SharedResourceManager`: wrap `ResourceStore` + `NetworkList`, đồng bộ giữa server và clients, implement `IResourceService` cho read-only access
- `GridManager`: lưu 2D grid state, expose `IsValidPlacement()`, `PlaceTower()`, `ClearTower()`

**Lý do:** Tầng abstraction cho data access, business logic không cần biết dữ liệu lưu kiểu gì hay ở đâu.

---

## 11. Adapter

**Dùng ở đâu:** Cầu nối giữa Unity event system và game logic  
**Các file chính:**
- [Assets/_Game/Scripts/Building/TowerPresenter.cs](Assets/_Game/Scripts/Building/TowerPresenter.cs)
- [Assets/_Game/Scripts/Player/PlayerController.cs](Assets/_Game/Scripts/Player/PlayerController.cs)

**Cách dùng:**
- `TowerPresenter` implement `IPointerClickHandler` từ Unity EventSystems — adapt click event của UI system thành logic tower-specific (hiện panel, xử lý selection)
- `InputReader` adapt Unity Input System events thành action-specific callbacks mà các class khác subscribe vào

**Lý do:** Tách biệt Unity framework API khỏi game logic, dễ swap input system nếu cần.

---

## 12. Value Object (Immutable Data)

**Dùng ở đâu:** Event payload và network transfer  
**Các file chính:**
- [Assets/_Game/Scripts/Core/ResourceChanged.cs](Assets/_Game/Scripts/Core/ResourceChanged.cs)
- [Assets/_Game/Scripts/Networking/ResourceAmount.cs](Assets/_Game/Scripts/Networking/ResourceAmount.cs)
- [Assets/_Game/Scripts/Data/ResourceCost.cs](Assets/_Game/Scripts/Data/ResourceCost.cs)

**Cách dùng:**
- Các struct này là immutable data containers — truyền qua event hoặc qua network mà không lo bị mutate
- `ResourceChanged`: payload của resource event, chứa giá trị cũ và mới
- `ResourceAmount`: NetworkSerializable struct để đồng bộ qua Netcode
- `ResourceCost`: định nghĩa chi phí của một hành động (đặt/nâng cấp tower)

**Lý do:** Tránh side effect khi share data qua event, an toàn cho network serialization.

---

## Tổng kết

| Pattern | Vị trí chính | Mục đích |
|---|---|---|
| MVP | UI, Tower, Lobby, HUD | Tách logic khỏi rendering |
| State Machine | Enemy AI | Quản lý hành vi enemy |
| Object Pool | NetworkObjectPool | Giảm GC trong multiplayer |
| Strategy + Template Method | Tool system | Linh hoạt hành vi tool |
| Dependency Injection | VContainer Scopes | Thay thế Singleton |
| Observer / Event Bus | EventBus, IModel | Decouple hệ thống |
| Factory | BuildingController, Pool | Tập trung logic tạo object |
| Command | Netcode RPC | Server-authoritative actions |
| Facade | BuildingController, ToolController | Đơn giản hóa interface |
| Repository | ResourceStore, GridManager | Abstraction data access |
| Adapter | TowerPresenter, InputReader | Cầu nối Unity API |
| Value Object | ResourceChanged, ResourceAmount | Immutable data transfer |
