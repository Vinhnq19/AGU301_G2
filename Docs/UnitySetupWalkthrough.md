# Huong Dan Setup Va Test Dungeon Builder

Phan setup trong Unity da duoc tu dong hoa bang editor tool:

```text
Assets/_Game/Editor/DungeonBuilderBootstrapper.cs
```

Tool nay tao placeholder sprite, ScriptableObject data, prefab, object trong scene, HUD text, NetworkManager prefab list, pool entries va cac reference serialized can thiet.

## 1. Trang Thai Hien Tai

Da kiem tra trong repo:

- `ProjectSettings/DungeonBuilderBootstrapRequested.flag` khong con, tuc la khong dang doi autorun bootstrap.
- `Assets/Scenes/SampleScene.unity` co `GameRoot`, `NetworkManager`, `BuildingController`, `GridManager`, `SharedResourceManager`, `NetworkObjectPool`.
- `NetworkManager` dang dung `DB_Player` lam Player Prefab va `DB_NetworkPrefabs` lam network prefab list.
- `BuildingController` da co 3 tower data va 3 tower prefab mapping: Arrow, Cannon, Frost.
- `NetworkObjectPool` da co pool entries cho tower prefabs.
- `DB_Player.prefab` da co `BuilderTool`, va `ToolController` dang order tool: Axe, Pickaxe, Weapon, Builder.
- Input hien tai dung action cu `Next`/`Previous`; code van support alias nay. Phim mac dinh la `2` de next tool, `1` de previous tool, chuot trai de use tool.

Ket luan: ve mat asset va reference, builder da duoc wire dung. Minh chua chay duoc Unity batch compile/play tu terminal vi project dang duoc mo trong mot Unity instance khac. De xac nhan runtime 100%, can test trong Editor dang mo theo phan 5 ben duoi va xem Console co log `build.accept` hay `build.reject`.

## 2. Chay Bootstrap Khi Can Tao Lai Setup

Neu Unity dang mo, quay lai Unity va doi compile/import xong. Neu setup bi mat reference hoac muon tao lai placeholder, chay menu:

```text
Dungeon Builder > Bootstrap Generated Setup
```

Neu muon chay bang PowerShell, dong Unity truoc roi chay:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "d:\NHH\PersonalProject\AGU301_G2" `
  -executeMethod DungeonBuilder.Editor.DungeonBuilderBootstrapper.Bootstrap `
  -logFile "d:\NHH\PersonalProject\AGU301_G2\Logs\DungeonBuilderBootstrap.log"
```

Neu len loi `another Unity instance is running with this project open`, dong Unity dang mo truoc khi chay batchmode.

## 3. Asset Va Object Duoc Tao

Sau khi bootstrap chay thanh cong, kiem tra co cac thu muc:

```text
Assets/_Game/Generated/Data/
Assets/_Game/Generated/Prefabs/
Assets/_Game/Generated/Sprites/
Assets/_Game/Generated/DB_NetworkPrefabs.asset
```

Trong `Assets/Scenes/SampleScene.unity`, tool tao:

```text
GameRoot
DB_Core
DB_SpawnPoints
DB_ResourceNodes
DB_HUDCanvas
EventSystem
NetworkManager
```

`GameRoot` can co va da wire reference cho:

- `NetworkObjectPool`
- `SharedResourceManager`
- `GridManager`
- `BuildingController`
- `WaveManager`
- `GameLifetimeScope`

Prefab duoc tao gom:

- `DB_Player`
- `DB_ResourceDrop`
- `DB_WoodNode`, `DB_StoneNode`, `DB_OreNode`, `DB_CrystalNode`
- `DB_DroneEnemy`, `DB_BruteEnemy`, `DB_MinerBugEnemy`
- `DB_ArrowTower`, `DB_CannonTower`, `DB_FrostTower`

Visual hien tai chi la placeholder circle, square, capsule de test flow truoc. Sau khi flow on moi thay art that.

## 4. Checklist Sau Bootstrap

1. Console khong co compile error.
2. `ProjectSettings/DungeonBuilderBootstrapRequested.flag` da bien mat.
3. `NetworkManager > Player Prefab` tro den `DB_Player`.
4. `NetworkManager > Network Prefabs` dung `DB_NetworkPrefabs`.
5. `GameRoot > GameLifetimeScope` da co du reference.
6. `GameRoot > NetworkObjectPool` co entries cho drop, enemy, tower.
7. `GameRoot > BuildingController` co tower data va tower prefab mapping.
8. `GameRoot > WaveManager` co core target, spawn points, enemy prefabs.
9. `DB_HUDCanvas > HUDView` da gan du TMP text refs.

## 5. Cach Dung Builder

Builder khong dat tower truc tiep tren client. Khi bam chuot, `BuilderTool` lay vi tri chuot, doi sang grid, goi `BuildingController.RequestPlaceTower`, sau do server moi quyet dinh co spawn tower hay khong.

Dieu kien de dat duoc tower:

- Dang chay Host hoac Client da connect vao Host.
- Player local da spawn va la owner.
- Da chon dung tool `Builder`.
- Co du resource cho tower dang chon.
- Click vao cell nam trong bounds va chua co tower.
- `BuildingController`, `GridManager`, `SharedResourceManager`, `NetworkObjectPool` inject duoc qua VContainer.

Cost mac dinh:

| Tower | Wood | Ore |
| --- | ---: | ---: |
| Arrow | 25 | 0 |
| Cannon | 40 | 15 |
| Frost | 25 | 10 |

`BuilderTool` hien dang selected tower mac dinh la Arrow, nen chi can 25 Wood la dat duoc tower dau tien.

### Test Builder Tren 1 Instance

1. Mo `Assets/Scenes/SampleScene.unity`.
2. Bam Play.
3. Chon object `NetworkManager` trong Hierarchy.
4. Trong Inspector cua `NetworkManager`, bam `Start Host`.
5. Xac nhan player `DB_Player` spawn ra scene.
6. Di chuyen bang `WASD` hoac arrow keys.
7. Chon dung tool harvest:
   - Tool order: Axe, Pickaxe, Weapon, Builder.
   - Bam `2` de sang tool tiep theo.
   - Bam `1` de lui tool truoc do.
8. Dung Axe/Pickaxe click vao resource node de lay resource drop.
9. Di vao drop de nhat. HUD phai tang resource.
10. Bam `2` den khi Console hien log dang chon `Builder`, hoac dem tu order mac dinh: tu Axe bam `2` 3 lan se toi Builder.
11. Click chuot trai vao vi tri trong gan player, tranh click trung node/resource/tower.
12. Expected:
    - HUD tru Wood.
    - Tower square spawn dung grid tren ca server.
    - Console co log `build.accept`.

Neu khong dat duoc, doc Console theo cac log sau:

- `build.reject.cost`: thieu resource. Harvest them Wood/Ore.
- `build.reject.grid`: click ngoai bounds hoac cell da occupied. Click vi tri khac.
- `build.reject.refs`: thieu reference/injection. Kiem tra `GameRoot > GameLifetimeScope`, `BuildingController`, `NetworkObjectPool`.
- `build.reject.pool`: pool khong tra ve tower. Kiem tra pool entries va network prefab list.
- Khong thay `build.send`: chua chon Builder, chua spawn player owner, hoac input attack khong vao.
- Thay `build.send` nhung khong thay `build.recv`: network chua listening hoac client chua connect Host.

## 6. Demo 2 Clone Choi Cung Nhau Bang ParrelSync

Muc tieu: chay 2 Unity Editor instance tren cung may, mot Host va mot Client, de test resource sync, player sync va tower spawn.

### 6.1. Tao Clone

1. Mo project goc trong Unity.
2. Tren menu Unity, mo ParrelSync. Thuong nam o:

```text
ParrelSync > Clones Manager
```

3. Neu chua co clone, bam `Create new clone`.
4. Doi ParrelSync tao clone xong. Clone se nam ngoai project goc va dung symlink toi `Assets`, `Packages`, `ProjectSettings`.
5. Trong Clones Manager, bam `Open in New Editor` cho clone.
6. Doi clone import/compile xong. Ca project goc va clone deu phai mo duoc `Assets/Scenes/SampleScene.unity`.

Neu clone khong thay package ParrelSync, kiem tra `Packages/manifest.json` co:

```json
"com.veriorpies.parrelsync": "https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync"
```

### 6.2. Chuan Bi Truoc Khi Bam Play

Lam tren ca project goc va clone:

1. Mo cung scene:

```text
Assets/Scenes/SampleScene.unity
```

2. Chon `NetworkManager`.
3. Kiem tra `UnityTransport`:
   - Address: `127.0.0.1`
   - Port: `7777`
   - Server Listen Address: `127.0.0.1`
4. Dam bao chi co 1 instance bam `Start Host`. Instance con lai chi bam `Start Client`.
5. Neu vua test truoc do, Stop Play ca hai instance truoc, roi Start lai theo dung thu tu Host truoc, Client sau.

### 6.3. Chay Host

Lam trong project goc:

1. Bam Play.
2. Chon `NetworkManager`.
3. Bam `Start Host`.
4. Expected:
   - Console co log network listening/host started.
   - Scene spawn 1 player.
   - `NetworkManager` dang IsHost/IsServer/IsClient.

Neu Host khong start:

- Kiem tra co instance nao khac dang giu port `7777` khong.
- Stop Play tat ca Unity instance, doi vai giay, roi chay lai Host truoc.
- Neu van loi port, doi port cua ca Host va Client sang cung mot port moi, vi du `7778`.

### 6.4. Chay Client Tu Clone

Lam trong Unity clone:

1. Bam Play.
2. Chon `NetworkManager`.
3. Bam `Start Client`.
4. Expected:
   - Client connect vao Host.
   - Host thay player thu hai spawn.
   - Client thay it nhat 2 player trong scene.
   - Ca hai instance thay cung resource nodes, core, HUD.

Neu Client khong connect:

- Host phai dang Play va da `Start Host` truoc.
- Address cua Client phai la `127.0.0.1`.
- Port cua Client phai trung Host.
- Console Client khong duoc co prefab mismatch. Neu co, chay bootstrap lai o project goc, de clone sync, roi mo lai clone.

### 6.5. Test 2 Nguoi Harvest Va Build

1. Tren Host, di chuyen player bang `WASD`.
2. Tren Client clone, di chuyen player bang `WASD`.
3. Xac nhan moi Editor chi dieu khien player cua minh.
4. Tren mot instance, chon Axe/Pickaxe roi click resource node.
5. Nhat drop.
6. Expected:
   - HUD resource tang tren ca Host va Client.
   - Resource khong chi tang local mot ben.
7. Harvest toi it nhat 25 Wood.
8. Tren Client clone, bam `2` den Builder, roi click cell trong.
9. Expected:
   - Tower spawn tren ca Host va Client.
   - HUD resource bi tru tren ca hai.
   - Host Console co `build.recv` va `build.accept`.
10. Tren Host, thu click dung cell vua co tower.
11. Expected:
    - Khong spawn tower thu hai cung cell.
    - Console co `build.reject.grid`.

### 6.6. Test Wave Va Enemy

1. Doi countdown build phase ket thuc.
2. Expected:
   - Enemy spawn tu `DB_SpawnPoints`.
   - Enemy hien tren ca Host va Client.
3. Dung Weapon tool de danh enemy.
4. Expected:
   - Enemy mat mau/death duoc server xu ly.
   - Enemy bien mat tren ca hai instance.
   - Khong co `NullReferenceException`.

### 6.7. Dau Hieu Demo 2 Clone Pass

Demo pass khi:

- Host va Client connect cung session.
- Moi ben dieu khien dung player cua minh.
- Resource/HUD sync giong nhau.
- Client co the gui request harvest/build len Host.
- Tower dat tu Client xuat hien tren ca hai instance.
- Enemy wave spawn va despawn tren ca hai instance.
- Console khong co compile error, prefab mismatch, VContainer resolve error, NullReferenceException lap lai.

## 7. Luong Test Anti-Cheat Va Edge Case

Muc tieu: kiem tra server tu choi input khong hop le thay vi crash.

1. Dung xa resource node roi click vao node.
2. Expected: server tu choi vi qua range, khong spawn drop.
3. Dung xa enemy roi attack.
4. Expected: server tu choi vi qua range, enemy khong mat mau.
5. Thu dat tower khi thieu resource.
6. Expected: khong spawn tower, resource khong am, Console co `build.reject.cost`.
7. Thu dat tower vao cell da occupied.
8. Expected: khong spawn tower, Console co `build.reject.grid`.
9. Destroy/disable `WaveManager` khi countdown dang chay.
10. Expected: khong co exception do UniTask da dung `destroyCancellationToken`.

## 8. Input Cleanup Sau Khi Test Flow On

`InputReader` hien support ca ten action cu va ten action theo plan. Khi flow placeholder da chay on, nen chinh `Assets/InputSystem_Actions.inputactions`:

- Rename `Jump` thanh `Dash`.
- Rename `Next` thanh `NextTool`.
- Rename `Previous` thanh `PrevTool`.
- Them `Hotbar1` den `Hotbar6`.
- Giu nguyen `UI` action map.

Sau khi chinh input, test lai builder va demo 2 clone.

## 9. Khi Nao Bat Dau Thay Art That

Chi thay art/prefab that sau khi cac flow nay pass:

- Host spawn player.
- Harvest va HUD resource hoat dong.
- Resource sync giua Host/Client.
- Wave spawn enemy.
- Enemy death return pool.
- Builder dat duoc tower.

Khi thay art that, giu nguyen rule prefab:

```text
[Root] NetworkObject + NetworkTransform + gameplay scripts
  [Child] Visual chua SpriteRenderer/model
```

DOTween chi duoc tween child `Visual`, khong tween root co `NetworkTransform`.
