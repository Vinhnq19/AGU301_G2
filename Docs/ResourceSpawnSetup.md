# Hệ thống sinh Resource theo Wave — Hướng dẫn setup

Tài liệu này mô tả hệ thống tài nguyên (cây/quặng) theo wave.

## Hai chế độ — chế độ ĐANG DÙNG: node cố định + gate wave

- **Node cố định + respawn tại chỗ (đang dùng)**: Các node đặt sẵn trong `DB_ResourceNodes` có **loại + vị trí cố định**. Bị khai thác cạn → **tự sinh lại tại chỗ** sau `respawnTime`. Node **loại hiếm bị khóa (ẩn)** cho tới wave `minWaveToAppear` (gate theo wave). KHÔNG cần `ResourceSpawner`.
- *(Tùy chọn) Spawn động vào slot*: `ResourceSpawner` + slot chọn loại ngẫu nhiên theo wave. Để dùng chế độ này thì wire `DB_ResourceSpawner`; nếu dùng chế độ node cố định thì **để DB_ResourceSpawner tắt / `_spawnSlots` rỗng** để tránh spawn chồng.

> ## ⚡ Tool cho chế độ node cố định
>
> 1. **`Dungeon Builder → Apply Resource Wave Gate`** ([ResourceNodeWaveGateTool.cs](../Assets/_Game/Editor/ResourceNodeWaveGateTool.cs)) — gán `minWaveToAppear` cho mọi `ResourceNodeDataSO` theo bảng (Wood/Stone=1, Ore/Copper=2, Iron/Crystal=3, BlueGems=5, PurpleGems=6). Chỉnh bảng trong file rồi chạy lại nếu muốn.
> 2. **`Dungeon Builder → Setup Per-Type Resource Drops`** — sinh drop prefab riêng từng loại (mục 5).
> 3. Đảm bảo mỗi node prefab có material flash + `_visualRenderer` (tool `Setup Resource Spawn System` đã làm, hoặc gán tay theo mục 2).
>
> **Cách gate hoạt động**: node có `minWaveToAppear > 1` sẽ bị `_isLocked = true` (ẩn + không đập được) khi `currentWave < minWaveToAppear`. Khi wave đạt mốc, node tự mở khóa + làm tươi. Node `minWaveToAppear = 1` luôn hiện. Logic trong `HarvestableNode.ApplyWaveGate()` (nghe `EventBus.OnWaveStarted`).

---

## Code đã hoàn tất

- `Assets/_Game/Scripts/Data/ResourceNodeDataSO.cs` — thêm `minWaveToAppear` (gate wave cho node cố định).
- `Assets/_Game/Scripts/Data/ResourceSpawnConfigSO.cs` — SO cấu hình spawn động theo wave (chế độ tùy chọn).
- `Assets/_Game/Scripts/Harvesting/ResourceSpawner.cs` — spawn node động vào slot (chế độ tùy chọn).
- `Assets/_Game/Scripts/Harvesting/HarvestableNode.cs` — Configure, IPoolable, damage-flash (shader), death VFX, **gate theo wave + respawn tại chỗ**.
- `Assets/_Game/Scripts/Harvesting/ResourceDrop.cs` — prefab riêng từng loại; jump tween **nhảy ra bên phải** (server dịch root + NetworkTransform sync nên vùng nhặt đi theo). Chỉnh `_jumpRightDistance`/`_jumpPower`/`_jumpDuration`/`_jumpVerticalJitter` trong Inspector.
- `Assets/_Game/Scripts/Networking/Scopes/GameLifetimeScope.cs` — đã đăng ký `ResourceSpawner`.
- `Assets/_Game/Editor/ResourceSpawnSetupTool.cs` — Editor tool tự động setup.

---

## 1. Shader damage-flash (`SpriteFlash`) — tool tự sinh

Tool sinh sẵn `Assets/_Game/Material/SpriteFlash.shader` (URP 2D Sprite Unlit, property `_FlashColor`/`_FlashAmount`) + material `M_SpriteFlash.mat`. `_FlashAmount` được điều khiển runtime qua `MaterialPropertyBlock` trong `HarvestableNode.SetFlashAmount()`, nên một material dùng chung cho mọi node là đủ.

> Nếu muốn bản **ShaderGraph** thay vì .shader: tạo *Sprite Unlit Shader Graph* tên `SpriteFlash`, thêm 2 property dưới đây rồi gán vào `M_SpriteFlash`:
>
> 1. **Properties** (Blackboard, dấu `+`):
>    - `_FlashColor` — kiểu **Color**, default trắng `(1,1,1,1)`, bật *Exposed*.
>    - `_FlashAmount` — kiểu **Float**, mode *Slider* `[0..1]`, default `0`, bật *Exposed*. Tên phải đúng `_FlashAmount` vì code dùng `Shader.PropertyToID("_FlashAmount")`.
> 2. **Node graph** (Fragment): nhân sprite texture với *Vertex Color* → `spriteColor`; **Lerp** `A=spriteColor.rgb, B=_FlashColor.rgb, T=_FlashAmount` → **Base Color**; nối `spriteColor.a` → **Alpha**.
> 3. **Save Asset** rồi gán shader vào `M_SpriteFlash`.

---

## 2. Prefab node (cây / quặng)

Với mỗi loại tài nguyên cần một prefab `HarvestableNode`. Có thể nhân bản từ prefab harvesting hiện có trong `Assets/_Game/Generated/Prefabs/Harvesting/`.

Cấu trúc bắt buộc (giữ nguyên quy ước art của project — xem [UnitySetupWalkthrough.md](UnitySetupWalkthrough.md)):

```
[Root]  NetworkObject + NetworkTransform + HarvestableNode + Collider2D
└── Visual  (SpriteRenderer, dùng material M_SpriteFlash)
```

Trên component `HarvestableNode`, gán Inspector:
- **Data** — `ResourceNodeDataSO` mặc định (có thể để spawner override).
- **Resource Drop Prefab** — prefab `ResourceDrop` (đã có sẵn trong project).
- **Visual** — Transform của child `Visual`.
- **Visual Renderer** — SpriteRenderer của child `Visual` (dùng cho flash). *Bắt buộc gán nếu muốn thấy flash.*
- **Colliders** — các Collider2D của node.
- **VFX** — `Flash Duration` (~0.15s), `Death Duration` (~0.3s).

Material của `Visual` SpriteRenderer = **`M_SpriteFlash`**.

> Lưu ý: node spawn từ wave (qua spawner) sẽ **không tự respawn** — wave sau điều khiển. Node **đặt sẵn trong scene** (không qua spawner) vẫn tự respawn sau `respawnTime` như cũ.

---

## 3. Đăng ký prefab vào pool & NetworkPrefabs

1. **NetworkObjectPool** (component trong scene): thêm mỗi prefab node mới vào list `_entries` (Prefab + Parent tùy chọn). Pattern giống các prefab enemy/drop hiện có.
2. **NetworkPrefabs list** (`Assets/_Game/Generated/DB_NetworkPrefabs.asset`): thêm prefab node để Netcode nhận diện.

---

## 4. Tạo & cấu hình `ResourceSpawnConfigSO`

1. Chuột phải trong `Assets/_Game/Generated/Data/ResourceData/` → *Create → Dungeon Builder → Data → Resource Spawn Config*.
2. Điền `entries`, mỗi dòng = một loại tài nguyên:

| Field | Ý nghĩa | Ví dụ Wood | Ví dụ BlueGems |
|---|---|---|---|
| `resourceType` | Loại | Wood | BlueGems |
| `nodePrefab` | Prefab node | TreeNode | BlueGemNode |
| `nodeData` | Override data (tùy chọn) | — | — |
| `minWaveToAppear` | Wave mở khóa | 1 | **5** (4 wave đầu không có) |
| `baseWeight` | Trọng số khi mở khóa | 10 | 1 |
| `weightGainPerWave` | Tăng mỗi wave | 0 | 0.5 |
| `maxWeight` | Trần (0 = vô hạn) | 10 | 6 |

3. Tham số toàn cục:
   - `baseNodesPerWave` — số node ở wave 1 (vd 2).
   - `nodesPerWaveGain` — số node cộng thêm mỗi wave (vd 1).
   - `maxNodesPerWave` — trần (vd 12).

**Cách rarity tăng dần theo wave** (logic trong `ResourceSpawnConfigSO.GetWeight`): trước `minWaveToAppear`, weight = 0 (không spawn). Từ wave đó trở đi: `weight = baseWeight + weightGainPerWave * (wave - minWaveToAppear)`, clamp bởi `maxWeight`. Đặt `weightGainPerWave` cao hơn cho loại hiếm để tỉ lệ của chúng tăng nhanh dần so với loại phổ thông.

---

## 5. Drop riêng từng loại — tool tự sinh

Trước đây mọi node dùng chung một `DB_ResourceDrop`. Giờ **mỗi loại có prefab drop riêng** (visual/màu khác nhau).

Menu **`Dungeon Builder → Setup Per-Type Resource Drops`** ([ResourceDropSetupTool.cs](../Assets/_Game/Editor/ResourceDropSetupTool.cs)) tự động:
- Duyệt mọi node prefab trong `Generated/Prefabs/Harvesting/`, đọc `resourceType` từ `ResourceNodeDataSO` của node (không hardcode).
- Với mỗi loại: clone `DB_ResourceDrop` → `DB_{Type}Drop` (vd `DB_WoodDrop`, `DB_BlueGemsDrop`), tô màu `Visual` SpriteRenderer theo loại.
- Gán drop prefab tương ứng vào `_resourceDropPrefab` của **mọi** node cùng loại.
- Đăng ký drop prefab vào `NetworkObjectPool` + NetworkPrefabs list.

Idempotent (chạy lại không tạo trùng). **Lưu ý quan trọng**: logic cộng tài nguyên vẫn do node gọi `drop.Configure(type, amount)` lúc spawn — prefab riêng chỉ khác ở **visual**. Sau khi chạy tool, bạn chỉ cần thay sprite đẹp cho từng `DB_{Type}Drop` nếu muốn (tool chỉ tô màu).

> `ResourceIconSO` đã bị bỏ — không còn icon lookup runtime.

---

## 6. Đặt ResourceSpawner & slot trong scene — tool tự tạo

Tool `Setup Resource Spawn System` đã tạo sẵn trong `SampleScene`:
- `DB_ResourceSpawner` (có `NetworkObject` + `ResourceSpawner`), wire `_config` + `_spawnSlots` + vào `GameLifetimeScope._resourceSpawner`.
- Con `Slots` chứa 10 slot `Slot_00..Slot_09` rải theo lưới: `localPosition = ((i % 5) * 2 - 4, (i / 5) * 2 + 2, 0)`.
  - Slot_00 = `(-4, 2)`, Slot_04 = `(4, 2)`, Slot_05 = `(-4, 4)`, Slot_09 = `(4, 4)`.

**Việc cần làm tay**: kéo `DB_ResourceSpawner` tới vị trí phù hợp trên map, hoặc chỉnh từng `Slot_xx` cho khớp địa hình (mỗi slot 1 ô, không nên đè lên đường đi enemy / chỗ đặt tower). Có thể thêm/bớt slot — `_spawnSlots` chỉ là mảng Transform.

---

## 7. Kiểm thử (theo plan)

1. **Compile sạch** — không lỗi; chạy `ResourceStoreTests` để chắc không vỡ.
2. Host bằng ParrelSync (xem [UnitySetupWalkthrough.md](UnitySetupWalkthrough.md)). Mỗi wave bắt đầu → node mới xuất hiện ở slot trống, số lượng tăng dần, không chồng nhau.
3. Đặt BlueGems `minWaveToAppear = 5` → 4 wave đầu không có BlueGems; từ wave 5 tỉ lệ tăng dần (theo dõi log `resource.spawn.*` qua `DBLog`).
4. Đập node → flash (shader). Phá hết → death VFX (scale). Drop **nhảy ra bên phải** (cung jump) + pop, **visual đúng loại** (prefab riêng); đi tới chỗ item đáp (bên phải) để nhặt → cộng tài nguyên (kiểm HUD).
5. Client thứ 2 thấy mọi thứ khớp host (server-authoritative).
6. Sau nhiều wave: node + drop được `Return` về pool và tái dùng đúng — log `pool.return`/`pool.get`.
