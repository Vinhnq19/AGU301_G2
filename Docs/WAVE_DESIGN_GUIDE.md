# Hướng dẫn thiết kế màn chơi (Wave Design)

> Dành cho người chỉnh balance/nội dung wave. Không cần biết code.
> Nguồn dữ liệu duy nhất: **`Docs/WaveSheet.csv`** — sửa file này rồi Import, KHÔNG sửa tay asset trong Unity nữa.

---

## 1. Hai cách chỉnh wave — chọn 1

### Cách A: Cửa sổ Wave Designer (khuyên dùng — trực quan, không cần mở Excel)

Menu **Tools > Waves > Wave Designer**:

- Mỗi wave 1 khung: chỉnh **Build (s)** / **Combat (s)** / tick **Boss**; header hiện tổng số quái.
- Từng nhóm quái 1 dòng: chọn loại quái, số con, giây giữa 2 con, cổng spawn + đường đi
  (popup theo tên North/East/West — mở SampleScene để có popup và validate đầy đủ).
- Nút: **+ Thêm nhóm quái**, **+ Thêm Wave** (copy wave cuối làm nền), **Nhân bản**, **Xóa**, **▲▼** đổi thứ tự.
- **Save (asset + CSV)**: kiểm tra lỗi (sai thì hiện đỏ trong cửa sổ, KHÔNG ghi gì) →
  ghi vào game + **tự động xuất `Docs/WaveSheet.csv`** — sheet và game luôn khớp nhau.
- **Import CSV → Tool**: có người sửa CSV ngoài Excel thì bấm nút này để nạp vào cửa sổ.
- Loại quái chưa có prefab hiện dấu ⚠ ngay trên dòng.

### Cách B: Sửa thẳng file CSV (quen Excel / sửa hàng loạt / dùng dải wave + growth)

```
Mở WaveSheet.csv → Sửa số liệu → Unity: Tools > Waves > Import Wave Sheet (CSV)
→ Đọc Console (lỗi thì sửa tiếp / OK thì Play) → Play thử bằng cheat
```

1. Mở `Docs/WaveSheet.csv` bằng **Excel** (hoặc VS Code). File là UTF-8, số thập phân dùng **dấu chấm** (`1.5`, không phải `1,5`).
2. Sửa/thêm dòng theo bảng cột ở mục 2.
3. Vào Unity, chạy menu **Tools > Waves > Import Wave Sheet (CSV)**.
4. Nhìn Console:
   - `Import OK — N waves...` → dữ liệu đã vào game.
   - `Import FAILED — ...` → đọc danh sách lỗi (có số dòng cụ thể), sửa CSV rồi Import lại. **Import fail thì không có gì bị ghi đè** — an toàn.
5. Bấm Play để thử (mẹo test nhanh ở mục 5).

> Nếu lỡ sửa nát file CSV: chạy **Tools > Waves > Export Current Waves to CSV** để sinh lại
> sheet từ dữ liệu đang có trong game.

---

## 2. Ý nghĩa từng cột

```csv
wave,buildTime,combatTime,isBoss,enemyType,count,interval,spawnPoint,path,growth
```

| Cột | Ý nghĩa | Ghi chú |
|---|---|---|
| `wave` | Wave số mấy | `3` = wave 3; `1-9` = áp cho cả wave 1→9 (xem mục 3) |
| `buildTime` | Giây chuẩn bị trước wave | Các dòng cùng 1 wave phải ghi giống nhau |
| `combatTime` | Giây tối đa của trận đánh | Hết quái sớm thì wave kết thúc sớm |
| `isBoss` | `TRUE`/`FALSE` | Wave boss: **giết boss = thắng luôn** → chỉ nên đặt ở wave cuối |
| `enemyType` | Tên loại quái | `Runner`, `Spitter`, `Bloater`, `RatKing` (đã có prefab). `Drone`, `Brute`, `MinerBug` chưa gắn prefab — dùng sẽ báo lỗi |
| `count` | Số con của nhóm này | > 0 |
| `interval` | Giây giữa 2 con spawn | 0 = ra cùng lúc |
| `spawnPoint` | Cổng xuất quái | `0` = Bắc, `1` = Đông, `2` = Tây |
| `path` | Đường quái đi | `0` = đường Bắc, `1` = Đông, `2` = Tây — thường đặt **trùng số với spawnPoint** |
| `growth` | Hệ số tăng quái mỗi wave | Chỉ có tác dụng khi `wave` là dải `A-B`. Bỏ trống = không tăng |

- **1 dòng = 1 nhóm quái.** Một wave muốn nhiều loại quái / nhiều cổng thì viết nhiều dòng cùng số wave.
- Wave phải đánh số liền mạch `1, 2, 3...` không được nhảy cóc.

---

## 3. Viết nhanh bằng dải wave + growth

Thay vì viết 9 dòng gần giống nhau, viết 1 dòng:

```csv
1-9,60,100,FALSE,Runner,10,1,0,0,1.2
```

nghĩa là: wave 1 có 10 Runner, mỗi wave sau **nhân 1.2** (làm tròn):
wave 2 = 12, wave 3 = 14, wave 4 = 17, ... wave 9 = 43 con.

Ví dụ nguyên 1 màn 10 wave chỉ cần 3 dòng:

```csv
wave,buildTime,combatTime,isBoss,enemyType,count,interval,spawnPoint,path,growth
1-9,60,100,FALSE,Runner,10,1,0,0,1.2
1-9,60,100,FALSE,Spitter,3,2,2,2,1.3
10,60,180,TRUE,RatKing,1,0,1,1,
```

Có thể trộn dải + dòng lẻ (vd thêm `5,60,100,FALSE,Bloater,2,3,1,1,` để riêng wave 5 có Bloater) — miễn là `buildTime/combatTime/isBoss` của cùng 1 wave khớp nhau giữa các dòng.

---

## 4. Chế độ vô tận (endless)

- Chọn GameObject **GameRoot** trong SampleScene → component **WaveManager** → tick **Endless Mode**.
- Sau wave cuối game không kết thúc: wave cuối lặp lại, mỗi wave vượt thêm **+1 quái mỗi nhóm**.
- Ở endless: giết boss KHÔNG thắng (boss thành quái mạnh lặp lại), HUD chỉ hiện `Wave: 12` không kèm tổng.
- Thua duy nhất khi mất Core.

---

## 5. Mẹo test nhanh khi Play

Mở bảng cheat: gõ **`/huydeptrai`** vào khung chat.

| Việc cần | Cách làm |
|---|---|
| Vào thẳng wave 7 để test | Nhập `7` vào ô trong mục WAVE → bấm **Jump to wave** (chỉ dùng lúc đang chuẩn bị) → bấm **SKIP** trên HUD |
| Bỏ qua thời gian chuẩn bị | Nút **SKIP** trên HUD |
| Sửa số liệu KHÔNG cần thoát Play | Tạo file `Assets/StreamingAssets/waves.json` (cấu trúc bên dưới) → sửa file → bấm **Reload Waves (JSON)** trong cheat → áp dụng từ wave kế tiếp |
| Test chết/hồi sinh | Nút **Tu sat (test respawn)** trong cheat |

Cấu trúc `waves.json` (chỉ dành cho test nhanh trên máy dev — **đừng commit**; bản chính thức vẫn là CSV):

```json
{
  "waves": [
    {
      "buildTime": 30, "combatTime": 60, "isBoss": false,
      "spawnGroups": [
        { "enemyType": "Runner", "count": 10, "interval": 1.0, "spawnPoint": 0, "path": 0 }
      ]
    }
  ]
}
```

Console sẽ ghi rõ đang chạy nguồn nào: `Using JSON override (...)` hay `Using WaveCatalog SO`.

---

## 6. Checklist trước khi commit

- [ ] Import báo `Import OK`, không có warning lạ trong Console.
- [ ] Play thử ít nhất wave đầu + wave boss (dùng Jump to wave).
- [ ] Commit **cả** `Docs/WaveSheet.csv` **và** thư mục `Assets/_Game/Generated/Data/WaveData/` (CSV và asset phải đi cùng nhau).
- [ ] Không commit `Assets/StreamingAssets/waves.json` (file test cá nhân).
