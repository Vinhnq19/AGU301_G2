# Dungeon Builder — Codebase Foundation Plan

## Context
Xây dựng codebase nền tảng cho game "Dungeon Builder" (2.5D Top-down Tower Defense + Sandbox Harvesting). Clean slate hoàn toàn — xóa 22 scripts hiện tại vì chúng dùng Singleton anti-pattern và coroutine cũ. Codebase mới tuân thủ SOLID, Server-Authoritative multiplayer, VContainer DI (không Singleton), UniTask (không Coroutine), DOTween.

**Environment:** Unity 6000.3.11f1 · NGO 2.11.2 ✓ · Input System 1.19.0 ✓ · URP 17.3.0 ✓  
**Missing packages:** UniTask · DOTween · VContainer (cần cài trước khi generate code)

---

## Bước 0 — Xóa code cũ & Cài packages

### Xóa scripts cũ
Xóa toàn bộ nội dung trong thư mục (giữ folder structure):
```
Assets/_Game/Scripts/Controller/       → xóa hết .cs
Assets/_Game/Scripts/Gameplay/         → xóa hết .cs
Assets/_Game/Scripts/Interfaces/       → xóa hết .cs
Assets/_Game/Scripts/_Pattern/         → xóa hết .cs
```

### Cài packages qua manifest.json
File: `Packages/manifest.json` — thêm scoped registry và dependencies:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.cysharp",
        "jp.hadashikick"
      ]
    }
  ],
  "dependencies": {
    "com.cysharp.unitask": "2.5.10",
    "jp.hadashikick.vcontainer": "1.16.8",
    ...existing dependencies...
  }
}
```

**DOTween:** Download từ Asset Store (miễn phí) hoặc import `.unitypackage` từ [demigiant.com](http://dotween.demigiant.com/download.php). Sau khi import, chạy `Tools → DOTween Utility Panel → Setup DOTween`.

---

## Bước 1 — Cấu trúc thư mục mới

```
Assets/_Game/Scripts/
├── Core/
│   ├── Enums/              ResourceType.cs, EnemyType.cs, TowerType.cs, GamePhase.cs
│   ├── Interfaces/         IInteractable.cs, IHarvestable.cs, IDamageable.cs, IPoolable.cs
│   └── EventBus.cs
│
├── Data/                   ScriptableObjects (thuần C# data, zero logic)
│   ├── EnemyDataSO.cs
│   ├── TowerDataSO.cs
│   ├── ResourceNodeDataSO.cs
│   └── PlayerDataSO.cs
│
├── Networking/
│   ├── Pool/
│   │   ├── INetworkPool.cs
│   │   ├── NetworkObjectPool.cs     ← NGO INetworkPrefabInstanceHandler + _resolver.Inject()
│   │   └── PoolEntry.cs
│   ├── SharedResourceManager.cs    ← NetworkVariable resources + server-only Add/Spend
│   └── Scopes/
│       ├── GameLifetimeScope.cs     ← VContainer root
│       └── PlayerLifetimeScope.cs
│
├── Player/
│   ├── InputReader.cs
│   ├── PlayerController.cs
│   ├── PlayerStats.cs
│   └── Tools/
│       ├── ITool.cs
│       ├── ToolController.cs
│       ├── AxeTool.cs
│       ├── PickaxeTool.cs
│       ├── WeaponTool.cs
│       └── BuilderTool.cs
│
├── Harvesting/
│   ├── HarvestableNode.cs
│   └── ResourceDrop.cs
│
├── Building/
│   ├── GridCell.cs                  ← struct, không phải MonoBehaviour
│   ├── GridManager.cs
│   └── BuildingController.cs
│
├── Enemy/
│   ├── BaseEnemy.cs
│   ├── EnemyStateMachine.cs
│   ├── States/
│   │   ├── IEnemyState.cs
│   │   ├── MoveToCoreState.cs
│   │   ├── AttackWallState.cs
│   │   ├── AttackCoreState.cs
│   │   └── StunnedState.cs
│   └── Types/
│       ├── DroneEnemy.cs
│       ├── BruteEnemy.cs
│       └── MinerBugEnemy.cs
│
├── Wave/
│   └── WaveManager.cs
│
└── UI/
    ├── Base/
    │   ├── IModel.cs
    │   ├── BaseView.cs
    │   └── BasePresenter.cs
    └── HUD/                         ← tài nguyên hiển thị trực tiếp trên HUD (shared)
        ├── HUDModel.cs
        ├── HUDView.cs
        └── HUDPresenter.cs
```

**Namespace convention:** `DungeonBuilder.Core`, `DungeonBuilder.Data`, `DungeonBuilder.Networking`, `DungeonBuilder.Player`, `DungeonBuilder.Harvesting`, `DungeonBuilder.Building`, `DungeonBuilder.Enemy`, `DungeonBuilder.Wave`, `DungeonBuilder.UI`

---

## Bước 2 — Core Layer

### 2.1 Enums (Core/Enums/)
Mỗi file một enum:
- `ResourceType`: `Wood, Stone, Ore, Crystal`
- `EnemyType`: `Drone, Brute, MinerBug`
- `TowerType`: `Arrow, Cannon, Frost`
- `GamePhase`: `Build, Combat`

### 2.2 Interfaces (Core/Interfaces/)
```csharp
IInteractable  → void OnInteract(PlayerController interactor)
IHarvestable   → void TakeDamageFrom(ITool tool); bool IsDepletable
IDamageable    → void TakeDamage(float amount, ulong attackerClientId = 0)
IPoolable      → void OnGetFromPool(); void OnReturnToPool()
```
`IHarvestable` extends `IInteractable`.

### 2.3 EventBus (Core/EventBus.cs)
**Thuần C#, không MonoBehaviour.** Đăng ký qua VContainer `Lifetime.Singleton`.
```csharp
// Events
event Action<ResourceType, int>  OnResourceCollected
event Action<ResourceType, int>  OnResourceUpdated      // fires on ALL clients khi NetworkVariable đổi
event Action<int>                OnCoreHealthChanged
event Action<int>                OnWaveStarted
event Action<EnemyType>          OnEnemyKilled

// Raise methods (null-safe invoke)
void RaiseResourceCollected(ResourceType, int)
void RaiseResourceUpdated(ResourceType, int)
void RaiseCoreHealthChanged(int)
void RaiseWaveStarted(int)
void RaiseEnemyKilled(EnemyType)
```

---

## Bước 3 — Data Layer (ScriptableObjects)

Tất cả dùng `[CreateAssetMenu]`. Chỉ có data fields, zero logic, không kế thừa MonoBehaviour.

| File | Key Fields |
|---|---|
| `EnemyDataSO` | `EnemyType enemyType, float maxHealth, float moveSpeed, int rewardGold` |
| `TowerDataSO` | `TowerType towerType, float damage, float range, float attackRate, int woodCost, int oreCost` |
| `ResourceNodeDataSO` | `ResourceType resourceType, int hitsToBreak, int amountPerHit(=20), int maxAmount(=100), float respawnTime` |
| `PlayerDataSO` | `float maxHP, float speed, float maxMana, float dashCooldown` |

---

## Bước 4 — Networking Layer

### 4.1 NetworkObjectPool
**Base:** `MonoBehaviour` + `INetworkPrefabInstanceHandler` (NGO 2.x interface)  
**Key:** Dùng `Dictionary<uint, Queue<NetworkObject>>` với key là `GlobalObjectIdHash` (không dùng string key).

```csharp
// Lifecycle hooks vào NGO
void Awake() → foreach entry: NetworkManager.Singleton.PrefabHandler.AddHandler(hash, this)

// INetworkPrefabInstanceHandler implementation
NetworkObject Instantiate(ulong ownerClientId, Vector3, Quaternion) → lấy từ pool
void Destroy(NetworkObject) → trả về pool

// Public API
NetworkObject Get(NetworkObject prefab, Vector3, Quaternion)
void Return(NetworkObject obj)
```

Pool entries cấu hình qua `[SerializeField] List<PoolEntry>` trong Inspector.

> **[DI Injection Fix — Bắt buộc]**  
> NGO quản lý vòng đời của NetworkObject độc lập, bỏ qua VContainer. Sau mỗi lần pool cấp phát (cả `Instantiate` lẫn `Get`), phải inject thủ công:
> ```csharp
> // NetworkObjectPool cần inject IObjectResolver từ VContainer
> [Inject] private IObjectResolver _resolver;
> 
> // Ngay sau khi lấy object từ pool hoặc Instantiate mới:
> _resolver.Inject(networkObject.gameObject);
> ```
> Điều này đảm bảo mọi `[Inject]` field trong `BaseEnemy`, `PlayerController`, `HarvestableNode`, `BaseTower` đều được resolve đúng trước khi object active.

### 4.2 VContainer Scopes

**GameLifetimeScope** — trên root GameObject trong Scene:
```csharp
builder.Register<EventBus>(Lifetime.Singleton)
builder.RegisterInstance(networkObjectPool).As<INetworkPool>()
builder.RegisterInstance(sharedResourceManager)
builder.RegisterInstance(gridManager)
builder.RegisterInstance(waveManager)
builder.Register<HUDModel>(Lifetime.Singleton)
builder.Register<HUDPresenter>(Lifetime.Singleton)
```

**PlayerLifetimeScope** — trên Player Prefab, child scope của Game:
```csharp
builder.RegisterInstance(inputReader)
builder.RegisterInstance(playerController)
builder.RegisterInstance(playerStats)
builder.RegisterInstance(toolController)
```

**Inject vào NetworkBehaviour:** Dùng pattern `[Inject] public void Construct(...)` (không dùng constructor injection vì NetworkBehaviour là MonoBehaviour).

---

## Bước 5 — Player Systems

### 5.1 InputReader
**Base:** `MonoBehaviour` (injectable). Wrap `InputSystem_Actions` generated class. **Không có input handling nào ở chỗ khác.**

Cần reconfigure `Assets/InputSystem_Actions.inputactions`:
- **Player Action Map:** Move (Vector2), Attack (Button), Interact (Button), Dash (Button — map từ "Jump"), NextTool (Button), PrevTool (Button), Hotbar1-6 (Button)
- **UI Action Map:** Navigate, Submit, Cancel (giữ nguyên)

```csharp
// Events published
event Action<Vector2> OnMove
event Action<Vector2> OnLook
event Action          OnAttackPressed / OnAttackCanceled
event Action          OnInteractPressed
event Action          OnDashPressed
event Action          OnNextToolPressed / OnPrevToolPressed
```

### 5.2 PlayerController
**Base:** `NetworkBehaviour`. Components cần có: `NetworkTransform`, `Rigidbody2D`.

- Owner di chuyển local → `NetworkTransform` sync lên server
- `RequestDashServerRpc(Vector3 dashVector)` — server validate, apply force
- `OnNetworkSpawn/Despawn` để subscribe/unsubscribe `InputReader` events (chỉ khi `IsOwner`)

### 5.3 PlayerStats
**Base:** `NetworkBehaviour`. Tất cả stats là `NetworkVariable<float>(writePerm: Server)`.

```csharp
NetworkVariable<float> _hp, _mana, _shield, _stamina
event Action<float, float> OnHPChanged    // (current, max)
event Action<float, float> OnManaChanged

// Anti-Cheat: KHÔNG nhận float amount từ client
// Server tự tính damage từ EnemyDataSO, không tin input của client
[ServerRpc(RequireOwnership=false)] RequestUseManaServerRpc()
  // Server lấy mana cost từ data SO tương ứng, tự trừ
```

> **[Anti-Cheat Rule]** Bất kỳ ServerRpc nào liên quan đến stat thay đổi đều **cấm nhận giá trị numeric từ client**. Server tự tìm nguồn dữ liệu (SO, config, khoảng cách thực đo) để tính kết quả.

### 5.4 ToolController + Tools (Strategy Pattern)
`ToolController` inject `InputReader`, lưu `ITool[]`, chuyển tool theo index.

```csharp
interface ITool {
    void UseAction(Vector3 targetPos);
    void CancelAction();
    ToolType ToolType { get; }
}
```

Mỗi Tool là `MonoBehaviour` trên Player Prefab — gửi **action intent**, không gửi giá trị:
- `AxeTool.UseAction()` → raycast để tìm target → `InteractWithNodeServerRpc(targetNetworkObjectId)`
- `PickaxeTool.UseAction()` → tương tự, gửi node ID
- `WeaponTool.UseAction()` → `AttackEnemyServerRpc(enemyNetworkObjectId)`
- `BuilderTool.UseAction()` → `BuildingController.PlaceTowerServerRpc(gridPos, towerType)`

**Server-side validation cho mọi ServerRpc nhận NetworkObjectId:**
```
1. Tìm object bằng NetworkManager.SpawnManager.SpawnedObjects[id]
2. Kiểm tra khoảng cách giữa player và target (chống teleport hack)
3. Lấy thông số damage/yield từ tool's DataSO (không từ client)
4. Thực thi logic
```

---

## Bước 6 — Harvesting System

### HarvestableNode
**Base:** `NetworkBehaviour`. **Implements:** `IHarvestable`, `IDamageable`.

```csharp
NetworkVariable<int>  _hitsRemaining   (writePerm: Server)
NetworkVariable<bool> _isDepleted      (writePerm: Server)

// Anti-Cheat: AxeTool/PickaxeTool chỉ gửi ID của node này
// Server tự lấy yield amount từ ResourceNodeDataSO
[ServerRpc(RequireOwnership=false)]
InteractWithNodeServerRpc(ulong senderClientId) — không nhận amount
  → Server validate khoảng cách player-node
  → Server tự đọc _data.amountPerHit từ SO
  → giảm _hitsRemaining
  → SpawnResourceDrop() qua INetworkPool
  → nếu hết: StartRespawnAsync() với UniTask.Delay(destroyCancellationToken)
```

### ResourceDrop
**Base:** `NetworkBehaviour`. **Implements:** `IPoolable`.

> **[DOTween + NetworkTransform Rule]**  
> Root GameObject chứa `NetworkTransform` → **KHÔNG BAO GIỜ** tween bằng DOTween.  
> Tween phải thực hiện trên **Child Visual GameObject** (chứa Sprite/Model):
> ```csharp
> [SerializeField] private Transform _visual;  // child GameObject chứa SpriteRenderer
> // OnGetFromPool → tween _visual, KHÔNG phải transform
> _visual.DOJump(_visual.localPosition + Vector3.up * 0.5f, 0.3f, 1, 0.4f)
>        .SetEase(Ease.OutBounce);
> // OnReturnToPool
> _visual.DOKill();
> _visual.localPosition = Vector3.zero;  // reset về gốc sau khi kill
> ```

`OnTriggerEnter()` — **chỉ Server xử lý (`if (!IsServer) return`):**
  → Gọi `SharedResourceManager.AddResource(type, amount)` — server tự cộng vào NetworkVariable
  → `_pool.Return()` — client không tự làm gì

> **[Zero Trust Client]** Client không bao giờ tự cộng resource. Mọi cập nhật kho tài nguyên đi qua Server → NetworkVariable → OnValueChanged.

---

## Bước 7 — Grid & Building System

### GridCell (struct)
```csharp
struct GridCell { Vector2Int GridPosition; bool IsOccupied; TowerType OccupiedBy; }
```

### GridManager (MonoBehaviour)
```csharp
bool IsValidPlacement(Vector2Int pos)
bool PlaceTower(Vector2Int pos, TowerDataSO data)
Vector3 GridToWorld(Vector2Int pos)
Vector2Int WorldToGrid(Vector3 worldPos)
```

### BuildingController (NetworkBehaviour)
```csharp
[ServerRpc(RequireOwnership=false)]
PlaceTowerServerRpc(Vector2Int gridPos, TowerType towerType, ServerRpcParams rpcParams)
  → validate resources từ SharedResourceManager (server-side)
  → _grid.IsValidPlacement(gridPos)
  → sharedResources.TrySpend(woodCost, oreCost)
  → _grid.PlaceTower(gridPos, data)
  → _pool.Get(towerPrefab, ...).Spawn()
```

---

## Bước 8 — Enemy AI (State Pattern)

### IEnemyState
```csharp
void Enter(BaseEnemy enemy)
void Exit(BaseEnemy enemy)
void Update(BaseEnemy enemy)
```

### EnemyStateMachine (thuần C#)
`ChangeState(IEnemyState)` → Exit cũ → Enter mới. `Update()` gọi từ `BaseEnemy.Update()` **server-only** (`if (!IsServer) return`).

### BaseEnemy (NetworkBehaviour implements IDamageable, IPoolable)
```csharp
NetworkVariable<float> _currentHP   (writePerm: Server)

// Anti-Cheat: Tower logic chạy server-side, gọi trực tiếp (không cần RPC)
public void TakeDamage(float amount, ulong attackerClientId = 0)  // server-only
  → nếu (!IsServer) return
  → giảm _currentHP.Value → nếu <= 0: DieAsync()

// WeaponTool dùng RPC gửi enemy ID, server tự tính damage từ WeaponDataSO
[ServerRpc(RequireOwnership=false)]
AttackEnemyServerRpc(ulong enemyNetworkObjectId, ServerRpcParams rpcParams)
  → validate khoảng cách player-enemy
  → tìm enemy qua SpawnedObjects[id]
  → lấy damage từ WeaponDataSO
  → enemy.TakeDamage(weaponDamage, senderClientId)

async UniTaskVoid DieAsync()
  → _eventBus.RaiseEnemyKilled()
  → PlayDeathEffectClientRpc()
  → UniTask.Delay(TimeSpan.FromMilliseconds(500), cancellationToken: destroyCancellationToken)
  → _pool.Return()

// Knockback và death effects tween _visual (child), KHÔNG tween root transform
[SerializeField] private Transform _visual;  // child SpriteRenderer GameObject

[ClientRpc] ApplyKnockbackClientRpc(Vector3 localOffset, float duration)
  → _visual.DOLocalMove(_visual.localPosition + localOffset, duration)
           .SetEase(Ease.OutQuad)
           .OnComplete(() => _visual.localPosition = Vector3.zero)
  // Root transform KHÔNG bị DOTween chạm vào → NetworkTransform không bị desync

[ClientRpc] PlayDeathEffectClientRpc()
  → _visual.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack)

// OnGetFromPool: reset HP, _visual.localScale = Vector3.one, ChangeState(MoveToCoreState)
// OnReturnToPool: _visual.DOKill(); _visual.localPosition = Vector3.zero; _visual.localScale = Vector3.one
```

**Prefab structure bắt buộc cho mọi NetworkObject:**
```
[Root] EnemyPrefab       ← NetworkObject + NetworkTransform + Collider + Scripts
  └── [Child] _visual    ← SpriteRenderer/Model — mọi DOTween effect chạy ở đây
```

### States
| State | Logic |
|---|---|
| `MoveToCoreState` | NavMesh/direct move về Core; transition → AttackWall nếu bị chặn |
| `AttackWallState` | Đánh tường; transition → MoveToCore nếu tường bị phá |
| `AttackCoreState` | Đánh Core; trigger `OnCoreHealthChanged` |
| `StunnedState(float duration)` | Đếm elapsed time, DOTween shake trên `_visual`; transition → MoveToCore khi hết |

### Concrete Types
`DroneEnemy`, `BruteEnemy`, `MinerBugEnemy` kế thừa `BaseEnemy`, gắn `EnemyDataSO` tương ứng qua Inspector. Override states nếu cần hành vi đặc biệt (drone bay qua tường → `DroneMoveState`).

---

## Bước 9 — Wave System (NetworkBehaviour, server-only)

```csharp
NetworkVariable<int>       _currentWave    (writePerm: Server)
NetworkVariable<float>     _phaseCountdown (writePerm: Server)
NetworkVariable<GamePhase> _gamePhase      (writePerm: Server)

async UniTaskVoid RunWaveLoopAsync()  // gọi trong OnNetworkSpawn khi IsServer
  while (true):
    _gamePhase = Build
    await CountdownAsync(buildPhaseDuration)
    _currentWave++
    _gamePhase = Combat
    _eventBus.RaiseWaveStarted(_currentWave)
    await SpawnWaveAsync(_currentWave)
    await UniTask.WaitUntil(() => AllEnemiesDead(),
                            cancellationToken: destroyCancellationToken)
```

> **[UniTask Memory Leak — Quy tắc bắt buộc toàn dự án]**  
> **Mọi** `UniTask.Delay`, `UniTask.WaitUntil`, `UniTask.WaitWhile` trong toàn bộ codebase phải có `cancellationToken: destroyCancellationToken`:
> ```csharp
> // ĐÚNG — task tự hủy khi GameObject destroyed
> await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: destroyCancellationToken);
> await UniTask.WaitUntil(() => condition, cancellationToken: destroyCancellationToken);
>
> // SAI — tạo zombie task chạy mãi sau khi GameObject destroyed
> await UniTask.Delay(TimeSpan.FromSeconds(duration));
> ```
> Unity 6: `destroyCancellationToken` là built-in property của mọi `MonoBehaviour`, không cần manual `CancellationTokenSource`.

---

## Bước 10 — Shared Inventory + UI MVP

### 10.1 SharedResourceManager (NetworkBehaviour)
**Vị trí:** `Scripts/Networking/SharedResourceManager.cs`  
**Đăng ký:** `GameLifetimeScope` — `builder.RegisterInstance(sharedResourceManager)`  
Tài nguyên là **dùng chung toàn đội** — không có túi đồ riêng mỗi người.

```csharp
// Mỗi loại tài nguyên = 1 NetworkVariable (writePerm: Server, readPerm: Everyone)
NetworkVariable<int> _wood, _stone, _ore, _crystal

// Chỉ gọi được từ server
public void AddResource(ResourceType type, int amount)   // server-only
public bool TrySpend(ResourceType type, int amount)      // server-only, trả false nếu không đủ
public int  GetAmount(ResourceType type)                 // read-only, mọi client đọc được
```

**Data Flow (Zero Trust):**
```
ResourceDrop.OnTriggerEnter [Server only]
  → SharedResourceManager.AddResource(type, amount)   [Server: tăng NetworkVariable]
  → NetworkVariable.OnValueChanged fires on ALL clients
  → EventBus.RaiseResourceUpdated(type, newValue)      [client-side, từ OnValueChanged]
  → HUDPresenter.OnResourceUpdated()
  → HUDModel.SetResource(type, newValue)
  → HUDView.Render()
```

> Client **không bao giờ** tự cộng/trừ resource. Mọi thay đổi xuất phát từ Server.

### 10.2 Base MVP Classes
**Thư mục:** `Scripts/UI/Base/`

```csharp
interface IModel { event Action OnChanged; }

abstract class BasePresenter<TView, TModel>
  where TView  : BaseView<...>
  where TModel : IModel
{
  protected TView View; protected TModel Model;
  // ctor: subscribe Model.OnChanged → OnModelChanged()
  abstract void OnModelChanged();
  virtual void Initialize() → OnModelChanged()   // initial render
  virtual void Dispose(): unsubscribe
}

abstract class BaseView<TPresenter> : MonoBehaviour
{
  [Inject] void SetPresenter(TPresenter p)
  abstract void Render()
}
```

### 10.3 HUD MVP
**Thư mục:** `Scripts/UI/HUD/`

**HUDModel** (thuần C#, implements IModel):
```csharp
Dictionary<ResourceType, int> Resources
void SetResource(ResourceType type, int value) → OnChanged?.Invoke()
int GetResource(ResourceType type)
```

**HUDView** (MonoBehaviour trên HUD prefab):
```csharp
[SerializeField] TMP_Text woodText, stoneText, oreText, crystalText
[SerializeField] TMP_Text waveText, countdownText
override void Render() → cập nhật các TMP_Text từ Model qua Presenter
```

**HUDPresenter**:
```csharp
// Inject: HUDModel, HUDView, EventBus
// Subscribe EventBus.OnResourceUpdated → Model.SetResource() → View.Render()
// Subscribe EventBus.OnWaveStarted → cập nhật wave number
// Subscribe EventBus.OnCoreHealthChanged → cập nhật core HP bar
// Không bao giờ tự sửa Model trừ khi nhận từ EventBus (server-originated)
```

---

## NGO Pattern Reference

| Scenario | Pattern |
|---|---|
| HP, Mana, Shield, Stamina | `NetworkVariable<float>` (Server write) |
| Wave số, countdown | `NetworkVariable<int/float>` (Server write) |
| Tài nguyên đội (Wood, Stone...) | `NetworkVariable<int>` trong SharedResourceManager (Server write) |
| Player request attack/harvest | `[ServerRpc]` — chỉ gửi target ID, không gửi giá trị |
| Enemy gây damage cho player | Server tự tính, gọi trực tiếp (enemy logic chạy server-side) |
| Visual effects (death, knockback) | `[ClientRpc]` từ server — tween trên `_visual` child |
| Build tower | `[ServerRpc(RequireOwnership=false)]` + validate resources server-side |
| Enemy/Projectile/ResourceDrop spawn | Server-only qua NGO PrefabHandler pool + `_resolver.Inject()` |
| Resource pickup | Collision `if (!IsServer) return` → SharedResourceManager.AddResource() |

---

## Unity 6 Specific Notes
- `destroyCancellationToken` — MonoBehaviour built-in, tự cancel UniTask khi GameObject bị destroy. Không cần manual `CancellationTokenSource`.
- `FindAnyObjectByType` — thay `FindObjectOfType` (deprecated Unity 6). Nhưng codebase này không dùng Find — tất cả qua VContainer.
- `NetworkObject.GlobalObjectIdHash` — dùng làm key cho pool dictionary thay string.
- VContainer inject vào NetworkBehaviour qua `[Inject] void Construct(...)` method, không phải constructor.

---

## Master Constraints (Toàn dự án)

| Constraint | Rule |
|---|---|
| **No Singleton** | Mọi service truy cập qua VContainer DI. Chỉ ngoại lệ: `NetworkManager.Singleton` (framework-controlled) |
| **No switch-case cho extensibility** | Tower/Enemy mới → tạo class kế thừa + override. Không thêm `case TowerType.New` vào bất kỳ Manager nào |
| **No client value trust** | ServerRpc chỉ nhận ID và action intent. Server tự đọc số liệu từ DataSO |
| **No naked UniTask** | Mọi async wait đều có `cancellationToken: destroyCancellationToken` |
| **No manual Inject skip** | Mọi NetworkObject được pool spawn phải qua `_resolver.Inject(go)` ngay sau khi lấy ra |
| **DOTween + NetworkTransform** | Tween chỉ trên **child `_visual` GameObject**. Root chứa `NetworkTransform` không bao giờ bị DOTween di chuyển/scale |
| **SRP** | Mỗi script một trách nhiệm. `PlayerController` chỉ xử lý input→movement. Damage logic thuộc về tool/weapon |
| **OCP** | Mở rộng bằng kế thừa/strategy, đóng kín với modification |

---

## Thứ tự Implementation

1. Core enums + EventBus (thuần C#, test ngay)
2. 4 ScriptableObjects (data layer, cần sớm)
3. `INetworkPool` + `NetworkObjectPool` (bao gồm `_resolver.Inject()`)
4. `SharedResourceManager` (NetworkVariable resources, server-only mutations)
5. `GameLifetimeScope` VContainer setup (đăng ký SharedResourceManager + EventBus)
6. `InputReader` + reconfigure `.inputactions`
7. `PlayerStats` + `PlayerController`
8. `ToolController` + `ITool` + `AxeTool` + `PickaxeTool` (action-based RPCs)
9. `HarvestableNode` + `ResourceDrop` (server-only pickup → SharedResourceManager)
10. `GridManager` + `BuildingController` + `BuilderTool`
11. `BaseEnemy` + `EnemyStateMachine` + States
12. `WaveManager`
13. MVP base classes + `HUDModel/View/Presenter` (subscribe EventBus.OnResourceUpdated)
14. `PlayerLifetimeScope`
15. Concrete enemy types (Drone, Brute, MinerBug)

---

## Verification

1. **Bước 0:** Unity Console không có compile error sau khi xóa scripts cũ
2. **Packages:** `Window → Package Manager` thấy UniTask + VContainer trong list; DOTween setup panel mở được
3. **EventBus test:** Tạo test MonoBehaviour đăng ký `OnEnemyKilled`, raise event thủ công, kiểm tra log
4. **NGO Pool + DI test:** Dùng ParrelSync mở 2 Unity instances; spawn enemy từ server, verify cả 2 client thấy và không có NullReferenceException
5. **VContainer test:** Verify `[Inject]` field trong `PlayerController` resolve đúng, không lỗi "Unable to resolve"
6. **Anti-Cheat test:** Gửi ServerRpc với networkObjectId không tồn tại, verify server từ chối gracefully
7. **SharedResource test:** Harvest node từ client A, verify HUD của cả client A lẫn client B cập nhật đúng
8. **Wave loop test:** Vào Play mode, verify phase countdown đếm ngược, wave số tăng đúng sau khi quái chết hết
9. **UniTask leak test:** Destroy WaveManager giữa chừng đếm ngược, verify không có "object destroyed" exception
