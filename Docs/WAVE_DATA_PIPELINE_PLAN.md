# Kế hoạch: Wave Data Pipeline (Sheet → SO + JSON override)

> Tài liệu tự chứa cho phiên làm việc sau. Đọc xong file này là đủ context để bắt tay implement,
> không cần lịch sử hội thoại cũ.
>
> **Trạng thái: Phase 1 + 2 + 3 ĐÃ XONG (2026-07-12).**
> Phase 3 gồm: cột `growth` + range wave "A-B" trong CSV (3 dòng sinh được 10 wave),
> `_endlessMode` trên WaveManager (Inspector; giết boss không thắng, HUD hiện "Wave: x"),
> cheat "Jump to wave" trong Cheat Panel (chỉ Build phase).
> **Đã chốt:** nguồn sheet = file CSV trong repo tại `Docs/WaveSheet.csv`.
>
> Đã có: `Assets/_Game/Editor/WaveSheetImporter.cs` (menu `Tools > Waves > Export/Import`),
> `IWaveProvider`/`SoWaveProvider`/`JsonWaveProvider` trong `Assets/_Game/Scripts/Wave/`,
> đăng ký DI trong `GameLifetimeScope`, nút "Reload Waves (JSON)" trong Cheat Panel,
> HUD hiển thị tổng wave dynamic (bỏ hardcode "/10").
> JSON override: tạo file `Assets/StreamingAssets/waves.json` (không commit) — schema xem class
> `JsonWaveProvider.SheetJson`: `{"waves":[{"buildTime","combatTime","isBoss","spawnGroups":[{"enemyType","count","interval","spawnPoint","path"}]}]}`.

---

## 1. Bối cảnh — hệ thống wave hiện tại

Dự án: game tower-defense co-op multiplayer, Unity 6000.3.15f1 + URP, Netcode for GameObjects
(server-authoritative, host là server), VContainer DI, UniTask.

### Luồng dữ liệu hiện tại
- `Assets/_Game/Scripts/Data/WaveCatalogSO.cs` — `WaveCatalogSO` = `List<WaveSO> waves`.
- `Assets/_Game/Scripts/Data/WaveSO.cs` — mỗi wave:
  - `buildPhaseDuration`, `combatPhaseDuration` (float, giây)
  - `isBossWave` (bool — giết boss = thắng ngay)
  - `spawnGroups: List<SpawnGroup>` với struct `SpawnGroup { EnemyType enemyType; int count; float spawnInterval; int spawnPointIndex; int pathIndex; }`
- Asset thực tế: `Assets/_Game/Generated/Data/WaveData/DB_WaveCatalog.asset` + `DB_Wave_1..10.asset`.
  Hiện đang **sửa tay trong Inspector** — đau: không nhìn tổng thể để cân bằng, dễ sai index, lỗi chỉ lộ lúc runtime.
- `EnemyType` enum (`Assets/_Game/Scripts/Core/Enums/EnemyType.cs`): `Runner, Spitter, Bloater, RatKing, Drone, Brute, MinerBug`.

### Luồng runtime hiện tại (`Assets/_Game/Scripts/Wave/WaveManager.cs`, trên GameObject `GameRoot` trong SampleScene)
- Server-only. `OnNetworkSpawn` → dựng `_prefabLookup` từ `_enemyPrefabMappings` (Inspector, EnemyType → NetworkObject prefab), set `_totalWaves`, chạy `RunWaveLoopAsync()` (UniTask).
- Vòng lặp: `Build phase` (đếm ngược, skip được qua `RequestSkipBuildPhaseServerRpc` — nút SKIP trên HUD)
  → `_currentWave++` → `SpawnWaveAsync` chạy song song → `Combat phase` đếm ngược,
  **kết thúc sớm** khi spawn xong và hết quái sống (`AllEnemiesDead()` prune `_activeEnemyIds`
  theo `NetworkManager.SpawnManager.SpawnedObjects`) → hết wave cuối set `_allWavesCompleted` → win.
- Spawn từng con: lấy vị trí `_spawnPoints[spawnPointIndex % length]` (các `Spawn_North/East/West` dưới
  `[World]/DB_SpawnPoints`), lấy enemy từ `INetworkPool` (`_pool.Get`), gọi `SetCoreTarget(_coreTarget)`
  + `SetPath(_enemyPaths[pathIndex].Waypoints)` **trước khi** `enemyObj.Spawn()`, delay `spawnInterval` giữa mỗi con.
- Sync client qua NetworkVariable (`_currentWave/_totalWaves/_phaseCountdown/_gamePhase`) → `EventBus` → HUD.
- **Dead code cần biết:** `SpawnWaveAsync` có nhánh fallback "wave vượt catalog → dùng wave cuối + cộng thêm quái"
  nhưng không bao giờ chạy vì `RunWaveLoopAsync` `break` ngay sau wave cuối. Phase 3 sẽ tận dụng nhánh này cho endless mode.

---

## 2. Quyết định — Phương án C (Hybrid), đã chốt với user

**Sheet là source of truth → importer sinh ScriptableObject cho build (ổn định) + JSON override cho dev (iterate nhanh).**

Giữ/bỏ:

| Thành phần | Số phận |
|---|---|
| Sửa tay `WaveSO` trong Inspector | BỎ — thay bằng sửa sheet rồi Import |
| `DB_Wave_N.asset` + `DB_WaveCatalog.asset` | GIỮ — thành sản phẩm sinh tự động, không sửa tay |
| `WaveManager` (loop, spawn, pool, NetworkVariable) | GIỮ ~99% — chỉ đổi ~5 dòng đọc qua `IWaveProvider` |
| Networking, `EnemyPath`, spawn points, `_enemyPrefabMappings` | GIỮ nguyên |
| JSON (`StreamingAssets/waves.json`) | THÊM — chỉ là lớp override cho dev/host, build vẫn chạy SO |

Lý do: runtime multiplayer server-authoritative cần ổn định — SO được Unity serialize sẵn, không rủi ro parse
lúc runtime. Sheet/JSON chỉ tấn công chỗ đau (soạn/cân bằng data chậm).

---

## 3. Phase 1 — Sheet importer + validation (~nửa ngày, ưu tiên cao nhất)

### 3.1 Schema CSV (1 dòng = 1 spawn group)
```csv
wave,buildTime,combatTime,isBoss,enemyType,count,interval,spawnPoint,path
1,90,100,FALSE,Runner,10,1.5,0,0
1,90,100,FALSE,Runner,10,1.5,2,2
2,45,100,FALSE,Runner,14,1.2,0,0
10,60,180,TRUE,RatKing,1,0,1,1
```
- Các dòng cùng `wave` gộp thành 1 `WaveSO`; `buildTime/combatTime/isBoss` lấy từ dòng đầu của wave đó
  (nếu các dòng cùng wave lệch nhau → lỗi validation).
- `enemyType` theo TÊN enum (không dùng số) để sheet dễ đọc.

### 3.2 Editor tool `WaveSheetImporter`
- File mới: `Assets/_Game/Editor/WaveSheetImporter.cs` (theo pattern các tool sẵn có như
  `Assets/_Game/Editor/PlayerAnimationSpriteSetup.cs` — static class + `[MenuItem]`).
- Menu: `Tools > Waves > Import Wave Sheet (CSV)` (+ nếu chọn Google Sheets: `Tools > Waves > Fetch & Import from Google Sheets`, dùng `UnityWebRequest`/`HttpClient` tải CSV từ URL publish-to-web, URL lưu trong EditorPrefs hoặc một config SO).
- Hành vi: parse CSV → **tạo/cập nhật in-place** `Assets/_Game/Generated/Data/WaveData/DB_Wave_N.asset`
  (LoadAssetAtPath trước, chỉ CreateAsset khi chưa có → GIỮ GUID, không vỡ reference từ `DB_WaveCatalog`)
  → cập nhật list `DB_WaveCatalog.waves` đúng thứ tự → `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`.
- Wave bị xóa khỏi sheet → hỏi confirm rồi xóa asset thừa (hoặc chỉ warning, tùy chọn lúc implement).

### 3.3 Validation lúc import (fail = không ghi asset, in báo cáo lỗi rõ ràng)
- `enemyType` parse được thành enum `EnemyType` VÀ có trong `_enemyPrefabMappings` của `GameRoot`
  trong `Assets/Scenes/SampleScene.unity` (đọc qua scene hoặc mở SampleScene kiểm tra — tối thiểu: kiểm tra enum hợp lệ, warning nếu không mở được scene).
- `spawnPoint` trong `[0, số spawn points)`, `path` trong `[0, số EnemyPath)` — đọc từ `GameRoot`/`DB_SpawnPoints`.
- `count > 0`, `interval >= 0`, `buildTime/combatTime > 0`, wave đánh số liên tục `1..N` không trùng/lủng.
- Boss: wave có `isBoss=TRUE` nên là wave cuối (warning nếu không phải).

### 3.4 Reverse-export
- Menu `Tools > Waves > Export Current Waves to CSV`: sinh `Docs/WaveSheet.csv` từ 10 wave hiện có
  → user có ngay sheet đúng schema để bắt đầu sửa. **Làm bước này ĐẦU TIÊN** (có sheet mẫu + test round-trip import/export).

---

## 4. Phase 2 — JSON override + hot reload (~nửa ngày)

### 4.1 `IWaveProvider`
- File mới: `Assets/_Game/Scripts/Wave/IWaveProvider.cs`:
  `int WaveCount { get; }` + `WaveData GetWave(int index)` — trong đó `WaveData` là struct/DTO thuần
  (mirror các field của `WaveSO`) để provider JSON không phụ thuộc SO.
- `SoWaveProvider` (mặc định): wrap `WaveCatalogSO` hiện tại.
- Sửa `WaveManager`: thay các chỗ đọc `_waveCatalog.waves[...]` (~4 chỗ: `OnNetworkSpawn` đếm totalWaves,
  `RunWaveLoopAsync` lấy duration, `SpawnWaveAsync` lấy config, `HandleCurrentWaveChanged` lấy isBoss)
  bằng `_waveProvider.GetWave(...)`. Đăng ký provider qua VContainer trong
  `Assets/_Game/Scripts/Networking/Scopes/GameLifetimeScope.cs` (đã có sẵn `builder.Register...` pattern).

### 4.2 `JsonWaveProvider`
- Đọc `Application.streamingAssetsPath + "/waves.json"` — cùng cấu trúc dữ liệu với CSV (mảng wave, mỗi wave có spawnGroups).
- Chỉ bật khi: `Application.isEditor` hoặc build Development + file tồn tại; ngược lại fallback `SoWaveProvider`.
- Parse bằng `JsonUtility` (đủ dùng, không cần package mới) — cần wrapper class serializable.
- Log rõ nguồn đang dùng: `[WaveProvider] Using JSON override (N waves)` / `Using WaveCatalog SO`.

### 4.3 Hot reload trong Cheat Panel
- `Assets/_Game/Scripts/UI/Cheat/CheatPanelView.cs` + generator `Assets/_Game/Scripts/Editor/CheatPanelSetup.cs`
  (regenerate prefab qua menu `Tools > Cheat > Create Cheat Panel Prefab`, xong phải thay instance trong SampleScene —
  xem mục 6 lưu ý quy trình regenerate).
- Nút "Reload Waves (JSON)": chỉ host; gọi reload trên provider; áp dụng từ wave KẾ TIẾP (không đụng wave đang chạy).
- Cheat panel mở bằng cách gõ `/huydeptrai` trong chat (xem `ChatView._cheatCode`).

---

## 5. Phase 3 — Tiện ích cân bằng (tùy chọn, làm sau)
- Cột scaling trong sheet (vd `countFormula = base*1.2^wave`) → importer sinh N wave từ ít dòng.
- Endless mode: bỏ `break` trong `RunWaveLoopAsync`, thêm `[SerializeField] bool _endlessMode` —
  nhánh fallback scaling trong `SpawnWaveAsync` đã có sẵn, đang là dead code.
- Cheat "Jump to wave X".

---

## 6. Lưu ý quan trọng cho phiên implement

1. **Commit:** user là `Ahy18 <huycv18.work@gmail.com>`, muốn NHIỀU commit nhỏ theo nhóm logic,
   **TUYỆT ĐỐI KHÔNG thêm trailer "Co-Authored-By: Claude"**. Commit thẳng không cần hỏi lại (user đã ủy quyền "cứ commit nhé").
2. **Unity MCP sẵn có** — dùng `execute_code`/`execute_menu_item`/`manage_scene` để chạy tool, verify, play-test.
   Console MCP (`read_console`) thường trả 0 entries — đọc `%LOCALAPPDATA%\Unity\Editor\Editor.log` thay thế.
   `refresh_unity` hay báo "Connection closed" khi domain reload — cứ đợi rồi ping bằng `execute_code`
   trả `EditorApplication.isCompiling / EditorUtility.scriptCompilationFailed`.
3. **Test flow chuẩn** (KHÔNG gọi `NetworkManager.StartHost()` trực tiếp — player sẽ spawn nhầm ở lobby):
   Play → `SceneManager.LoadScene("LobbyScene")` → `LobbyConnectionService.StartHost("TestHost")`
   (nếu port 7777 bị chiếm do socket leak từ phiên play trước: set field private `_port` qua reflection sang port khác)
   → `LobbyController.RequestStartGame()` → SampleScene, player spawn.
4. **Hierarchy đã tổ chức lại**: các scene có nhóm `[Core]/[World]/[UI]/[Managers]`; `NetworkManager`, `AudioManager`
   (DontDestroyOnLoad) và mọi GameObject có `NetworkObject` (`GameRoot`, `Shop`, ...) phải Ở ROOT — đừng kéo vào nhóm.
5. **Quy trình sửa Cheat Panel**: sửa `CheatPanelSetup.cs` → chạy menu regenerate → XÓA instance cũ trong SampleScene
   → instantiate prefab mới vào `[UI]` (cạnh `ChatCanvas`) → save scene. (SaveAsPrefabAsset đổi fileID nên
   không thể để instance cũ tự cập nhật.)
6. **CSV encoding**: sheet có thể chứa tên tiếng Việt trong tương lai — đọc bằng UTF-8; số thực dùng
   `CultureInfo.InvariantCulture` khi parse (máy user locale vi-VN, dấu phẩy thập phân dễ gây bug).
7. Sau mỗi phase: verify bằng play-test flow ở mục (3) — wave 1 spawn đúng số quái, HUD hiện PREPARING/Wave x/10,
   console sạch — rồi mới commit.

## 7. Thứ tự thực thi đề xuất cho phiên sau
1. Hỏi user: nguồn sheet = CSV trong repo hay Google Sheets URL? (mặc định hợp lý: CSV trong repo tại `Docs/WaveSheet.csv`).
2. Phase 1: Export CSV từ data hiện tại → importer + validation → round-trip test → play-test → commit.
3. Phase 2: `IWaveProvider` + `SoWaveProvider` → refactor `WaveManager` → play-test → commit;
   `JsonWaveProvider` + nút Reload cheat → play-test hot-reload → commit.
4. Phase 3: chỉ khi user yêu cầu.
