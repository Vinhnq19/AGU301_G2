# Lobby Scene — Hướng dẫn setup & test

Hàng chờ multiplayer LAN chạy **trước** SampleScene: Host hiện IP làm Room ID, client cùng wifi gõ đúng IP + nhập tên là join, slot hiện cho mọi người, Host bấm Start → cả phòng vào game.

---

## ⚡ Cách nhanh: 1-click bằng Editor tool

Đã có Editor tool dựng **toàn bộ** scene tự động. Trong Unity:

> **Menu: `Dungeon Builder > Setup Lobby Scene`**

Tool sẽ tự làm hết (idempotent — chạy lại không tạo trùng):
1. Tạo **NetworkManager + UnityTransport** trong LobbyScene, gán sẵn PlayerPrefab (DB_Player) + NetworkPrefabsList, bật Scene Management.
2. Tạo **LobbyController** (kèm NetworkObject), **LobbyConnectionService**, **LobbyLifetimeScope**.
3. Dựng **UI đầy đủ** trên Canvas: ô nhập Tên, ô nhập IP (Join), nút **Host / Join / Start Game / Disconnect**, text **Room ID** + **Status**, và danh sách slot bên phải.
4. Tạo prefab **LobbySlotItem** ở `Assets/_Game/Generated/Prefabs/UI/`.
5. Wire toàn bộ references.
6. **Xóa NetworkManager khỏi SampleScene** (vì NM persist từ LobbyScene qua `DontDestroyOnLoad`).
7. Thêm cả 2 scene vào **Build Settings** (LobbyScene index 0, SampleScene index 1).

Sau khi chạy: mở **LobbyScene** và bấm **Play**. Xong.

> Nếu Unity báo lỗi compile editor tool lúc đầu: đợi Unity import xong script rồi mới thấy menu `Dungeon Builder`.

---

## 🎮 Test (2 instance / 2 máy cùng wifi)

1. **Máy A (Host)**: nhập Tên → bấm **HOST**. `Room ID (IP)` hiện IP LAN (vd `192.168.x.x`). Slot host xuất hiện kèm "(Host)".
2. **Máy B (Client, cùng wifi)**: nhập Tên + gõ đúng IP của A vào ô Join → **JOIN**. Slot B hiện ở **cả 2** màn hình.
3. **Máy A** bấm **START GAME** → cả A và B cùng load SampleScene, player spawn, game chạy.

Test nhiều instance trên 1 máy: dùng **ParrelSync** hoặc build ra .exe chạy song song với Editor.

### Nếu join không được dù đúng IP
- **Windows Firewall** (nguyên nhân #1): lần đầu Host, chọn **Allow** cho mạng **Private**. Hoặc tắt firewall tạm để test.
- **Wifi client isolation**: vài router/wifi công cộng chặn máy-tới-máy. Thử hotspot điện thoại.
- Kiểm tra cùng subnet (IP cùng dải `192.168.x.*`).

---

## 🧱 Kiến trúc (để hiểu/sửa)

```
LobbyScene (build index 0)              SampleScene (index 1)
 ├─ NetworkManager (DontDestroyOnLoad) ─── persist qua scene ──► (KHÔNG còn NM riêng)
 ├─ LobbyController (NetworkObject)         + NetworkList<LobbySlot>
 ├─ LobbyConnectionService                  Host/Join + approval + local IP
 ├─ LobbyLifetimeScope                      VContainer
 └─ Canvas + LobbyView                      MVP UI
```

- **Host bấm Start** → `LobbyController.RequestStartGame()` → `NetworkManager.SceneManager.LoadScene("SampleScene")` đồng bộ mọi client.
- **Tên người chơi** truyền qua `NetworkConfig.ConnectionData` (payload) → server đọc trong `ConnectionApprovalCallback` → thêm vào `NetworkList<LobbySlot>` → mọi client render.
- **Room ID = IP host** lấy bằng `LobbyConnectionService.GetLocalIPv4()`.

### Các file code (`Assets/_Game/Scripts/Networking/Lobby/`)
| File | Vai trò |
|------|---------|
| `LobbySlot.cs` | Struct sync trong NetworkList `{ClientId, PlayerName}` |
| `LobbyController.cs` | NetworkBehaviour, NetworkList slot, load game scene |
| `LobbyConnectionService.cs` | Host/Join, ConnectionApproval, lấy IP LAN |
| `LobbyModel/Presenter/View.cs` | MVP UI |
| `LobbySlotItem.cs` | 1 dòng slot |
| `LobbyLifetimeScope.cs` | VContainer scope |

Editor tool: `Assets/_Game/Editor/LobbySceneSetupTool.cs`

---

## 🛠️ Nếu muốn làm tay (không dùng tool)

<details>
<summary>Bấm để xem các bước thủ công</summary>

1. **NetworkManager**: copy GameObject NetworkManager (kèm UnityTransport, PlayerPrefab, NetworkPrefabsList) từ SampleScene sang LobbyScene; **xóa** bản ở SampleScene. (Code tự `DontDestroyOnLoad`.)
2. **LobbyController**: Empty GameObject + Add Component `Network Object` + `LobbyController`. Field `Game Scene Name` = `SampleScene`.
3. **LobbyConnectionService**: Empty GameObject + script; kéo LobbyController vào field `Lobby Controller`; Port 7777.
4. **Canvas UI** (uGUI + TMP): 2 InputField (Tên, IP), 4 Button (Host/Join/Start/Disconnect), 2 Text (Room ID, Status), 1 container có Vertical Layout Group cho slot. Add `LobbyView` lên Canvas, kéo-thả từng reference.
5. **LobbySlotItem prefab**: 1 GameObject (Horizontal Layout) chứa 2 TMP Text (index + tên) + script `LobbySlotItem`; lưu thành prefab; gán vào `LobbyView.Slot Item Prefab`.
6. **LobbyLifetimeScope**: Empty GameObject + script; gán LobbyView / LobbyController / ConnectionService.
7. **Build Settings**: LobbyScene index 0, SampleScene index 1.

</details>

---

## Debug nhanh trong Editor
- Mở thẳng SampleScene sẽ **không có NetworkManager** (đã chuyển sang LobbyScene). Luôn Play từ **LobbyScene**.
- Cần debug Host/Client kiểu cũ: bật cờ `_enabled` trên component `NetworkDebugUI`.
