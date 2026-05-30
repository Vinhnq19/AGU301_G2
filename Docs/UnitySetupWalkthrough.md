# Hướng Dẫn Setup Và Test Dungeon Builder

Phần setup trong Unity đã được tự động hóa bằng editor tool:

```text
Assets/_Game/Editor/DungeonBuilderBootstrapper.cs
```

Tool này tạo sẵn placeholder sprite, ScriptableObject data, prefab, GameObject trong scene, HUD text, NetworkManager prefab list, pool entries và các reference serialized cần thiết.

## 1. Chạy Bootstrap

Codex đã đặt flag chạy một lần tại:

```text
ProjectSettings/DungeonBuilderBootstrapRequested.flag
```

Nếu Unity đang mở, quay lại Unity và đợi compile/import xong. Sau khi compile thành công, bootstrap sẽ tự chạy. Nếu không tự chạy hoặc file flag vẫn còn, chạy thủ công bằng menu:

```text
Dungeon Builder > Bootstrap Generated Setup
```

Nếu muốn chạy bằng PowerShell, đóng Unity trước rồi chạy:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "d:\NHH\PersonalProject\AGU301_G2" `
  -executeMethod DungeonBuilder.Editor.DungeonBuilderBootstrapper.Bootstrap `
  -logFile "d:\NHH\PersonalProject\AGU301_G2\Logs\DungeonBuilderBootstrap.log"
```

## 2. Asset Và Object Được Tạo

Sau khi bootstrap chạy thành công, kiểm tra có các thư mục:

```text
Assets/_Game/Generated/Data/
Assets/_Game/Generated/Prefabs/
Assets/_Game/Generated/Sprites/
Assets/_Game/Generated/DB_NetworkPrefabs.asset
```

Trong `Assets/Scenes/SampleScene.unity`, tool sẽ tạo:

```text
GameRoot
DB_Core
DB_SpawnPoints
DB_ResourceNodes
DB_HUDCanvas
EventSystem
```

`GameRoot` sẽ có và đã wire reference cho:

- `NetworkObjectPool`
- `SharedResourceManager`
- `GridManager`
- `BuildingController`
- `WaveManager`
- `GameLifetimeScope`

Prefab được tạo gồm:

- `DB_Player`
- `DB_ResourceDrop`
- `DB_WoodNode`, `DB_StoneNode`, `DB_OreNode`, `DB_CrystalNode`
- `DB_DroneEnemy`, `DB_BruteEnemy`, `DB_MinerBugEnemy`
- `DB_ArrowTower`, `DB_CannonTower`, `DB_FrostTower`

Visual chỉ là placeholder circle, square, capsule để test flow trước. Sau khi flow ổn mới thay art thật.

## 3. Checklist Sau Bootstrap

1. Console không có compile error.
2. `ProjectSettings/DungeonBuilderBootstrapRequested.flag` đã biến mất. Nếu còn, chạy menu bootstrap thủ công.
3. `NetworkManager > Player Prefab` trỏ đến `DB_Player`.
4. `NetworkManager > Network Prefabs` dùng `DB_NetworkPrefabs`.
5. `GameRoot > GameLifetimeScope` đã có đủ reference.
6. `GameRoot > NetworkObjectPool` có entries cho drop, enemy, tower.
7. `GameRoot > BuildingController` có tower data và tower prefab mapping.
8. `GameRoot > WaveManager` có core target, spawn points, enemy prefabs.
9. `DB_HUDCanvas > HUDView` đã gán đủ TMP text refs.

## 4. Luồng Test 1: Host Một Instance

Mục tiêu: xác nhận scene, spawn player, pool, harvest, HUD hoạt động trước khi test client.

1. Mở `Assets/Scenes/SampleScene.unity`.
2. Bấm Play.
3. Chọn object `NetworkManager` trong Hierarchy.
4. Trong Inspector của `NetworkManager`, bấm `Start Host`.
5. Xác nhận player `DB_Player` spawn ra scene.
6. Di chuyển bằng `WASD` hoặc arrow keys.
7. Dùng chuột trái vào node tài nguyên gần player.
8. Đi vào `DB_ResourceDrop` vừa spawn.
9. Kiểm tra HUD:
   - Wood/Stone/Ore/Crystal tăng.
   - Không có `NullReferenceException`.
   - Resource chỉ tăng khi server xử lý pickup.
10. Đợi hết countdown build phase.
11. Xác nhận `WaveManager` spawn enemy ở `DB_SpawnPoints`.
12. Dùng weapon/tool đánh enemy và kiểm tra enemy death effect rồi return pool.

Nếu cần đổi tool trong setup placeholder hiện tại:

- `2`: next tool.
- `1`: previous tool.
- Tool order mặc định: Axe, Pickaxe, Weapon, Builder.

## 5. Luồng Test 2: Build Tower

Mục tiêu: xác nhận `BuilderTool -> BuildingController -> SharedResourceManager -> NetworkObjectPool`.

1. Chạy Host như Luồng Test 1.
2. Harvest vài node để có Wood/Ore.
3. Bấm `2` vài lần để chuyển đến `Builder`.
4. Click vào vị trí trống trên grid.
5. Xác nhận:
   - Resource bị trừ theo `TowerDataSO`.
   - Tower prefab spawn ở vị trí grid.
   - `GridManager` không cho đặt đè lên cell đã occupied.

Nếu Builder không đặt được:

- Kiểm tra `GameRoot > BuildingController` có đủ tower data và prefab mapping.
- Kiểm tra `GameRoot > NetworkObjectPool` có tower prefabs.
- Kiểm tra Console có lỗi VContainer resolve `BuildingController` hoặc `GridManager` không.

## 6. Luồng Test 3: Host Và Client Bằng ParrelSync

Mục tiêu: xác nhận server-authoritative resource và network spawn hiển thị ở nhiều client.

1. Mở project chính trong Unity.
2. Dùng ParrelSync tạo/mở clone nếu chưa có.
3. Ở project chính:
   - Mở `SampleScene`.
   - Bấm Play.
   - Chọn `NetworkManager`.
   - Bấm `Start Host`.
4. Ở project clone:
   - Mở cùng scene.
   - Bấm Play.
   - Chọn `NetworkManager`.
   - Bấm `Start Client`.
5. Kiểm tra client connect và player thứ hai spawn.
6. Từ client, harvest node tài nguyên.
7. Kiểm tra cả Host và Client:
   - Cùng thấy resource drop.
   - HUD resource cập nhật giống nhau.
   - Client không tự cộng resource local.
8. Đợi wave spawn enemy.
9. Kiểm tra enemy xuất hiện trên cả Host và Client.
10. Kill enemy từ Host hoặc Client, xác nhận death effect chạy và enemy biến mất trên cả hai.

## 7. Luồng Test 4: Anti-Cheat Và Edge Case

Mục tiêu: kiểm tra server từ chối input không hợp lệ thay vì crash.

1. Đứng xa resource node rồi click vào node.
2. Expected: server từ chối vì quá range, không spawn drop.
3. Đứng xa enemy rồi attack.
4. Expected: server từ chối vì quá range, enemy không mất máu.
5. Thử đặt tower khi thiếu resource.
6. Expected: không spawn tower, resource không âm.
7. Destroy/disable `WaveManager` khi countdown đang chạy.
8. Expected: không có exception do UniTask đã dùng `destroyCancellationToken`.

## 8. Input Cleanup Sau Khi Test Flow Ổn

`InputReader` hiện hỗ trợ cả tên action cũ và tên action theo plan. Khi flow placeholder đã chạy ổn, nên chỉnh `Assets/InputSystem_Actions.inputactions`:

- Rename `Jump` thành `Dash`.
- Rename `Next` thành `NextTool`.
- Rename `Previous` thành `PrevTool`.
- Thêm `Hotbar1` đến `Hotbar6`.
- Giữ nguyên `UI` action map.

Sau khi chỉnh input, test lại Luồng 1 và Luồng 2.

## 9. Khi Nào Bắt Đầu Thay Art Thật

Chỉ thay art/prefab thật sau khi các flow này pass:

- Host spawn player.
- Harvest và HUD resource hoạt động.
- Resource sync giữa Host/Client.
- Wave spawn enemy.
- Enemy death return pool.
- Builder đặt được tower.

Khi thay art thật, giữ nguyên rule prefab:

```text
[Root] NetworkObject + NetworkTransform + gameplay scripts
  [Child] Visual chứa SpriteRenderer/model
```

DOTween chỉ được tween child `Visual`, không tween root có `NetworkTransform`.
