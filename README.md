# Snow Boarder — Lab 02 PRU213

Game trượt tuyết 2D phong cách pixel, xây trên **Unity 6000.6.4f1** với **URP 2D**.

Toàn bộ màn chơi được **sinh bằng code lúc chạy** (terrain, ramp, vật cản, vật phẩm, cảnh nền) nên
không phụ thuộc scene dựng sẵn — chỉ cần mở `Assets/Scenes/SnowBoarder.unity` là chơi được.

---

## Yêu cầu & cách chạy

| | |
|---|---|
| Unity | **6000.6.4f1** (mở bằng đúng version này để không lỗi import) |
| Render pipeline | Universal Render Pipeline 2D |
| Input | Tự chuyển giữa Input System cũ và mới (`ENABLE_INPUT_SYSTEM`) |

```bash
git clone https://github.com/ThaiTuan05/LAB02_PRU213_VOTHAITUAN.git
# mở bằng Unity Hub → chọn đúng version 6000.6.4f1 → Add project
```

Mở `Assets/Scenes/SnowBoarder.unity`, bấm **Play**. Game tự tạo nhân vật, camera, UI và
nhạc nền ngay khi vào scene — không cần kéo thả gì thêm.

> Lần chạy đầu tiên Unity sẽ import package và build shader, mất vài phút. Bộ kiểm tra tự
> động cần thêm thời gian nữa vì có chụp ảnh màn hình.

---

## Điều khiển

| Phím | Hành động |
|---|---|
| `W` / `↑` | Đẩy ván, tăng tốc theo hướng mặt dốc |
| `S` / `↓` | Phanh |
| `A` `D` / `←` `→` | Nghiêng nhẹ khi trên mặt đất, xoay có kiểm soát khi bay |
| `Shift` + `A`/`D` | Xoay nhanh để làm trick |
| `Space` | Nhảy (có coyote time 0.12s) |
| `Esc` | Tạm dừng |
| `R` | Chơi lại lượt |

Thả `A`/`D` sẽ hãm xoay rất nhanh. Có thanh **Rotation Sensitivity** trong Options
(0.65 – 1.2, mặc định 0.9): giá trị thấp thì đáp dễ hơn.

---

## Ba ngọn núi

| Núi | Độ dài | Chế độ | Đặc điểm |
|---|---|---|---|
| **Ridge Run** | 285 m | EASY | Dốc thoải, ít chướng ngại — để học điều khiển |
| **Glacier Ridge** | 470 m | MEDIUM | Dốc hơn, nhiều ramp, địa hình băng trơn hơn |
| **Summit Descent** | 700 m | HARD | Dốc nhất, chướng ngại dày đặc, chạy ban đêm |

Mỗi núi có **2 chế độ**:

- **Free Ride** — chơi theo nhịp của bạn, không giới hạn thời gian.
- **Time Attack** — phải về đích trong **40 / 60 / 90 giây** tùy núi.

Điểm cao lưu **riêng cho từng núi và từng chế độ**.

---

## Luật chơi

### Nhiệm vụ mỗi lượt

| Nhiệm vụ | Thưởng |
|---|---|
| Thu thập 10 bông tuyết | +300 |
| Đáp một vòng 360° đầy đủ | +400 |
| Về đích còn ít nhất 2 mạng | +500 |

Mỗi phần thưởng chỉ nhận một lần trong lượt. Hết mạng thì kết thúc.

### Combo trên không

Mỗi vòng 360° hoàn thành **ngay trong lúc đang bay**, không phải đợi tiếp đất:

```
Điểm = 250 × (số vòng mới) × combo
      + 175 × (số vòng mới − 1) × combo     ← thưởng chuỗi nhiều vòng trong một cú nhảy
```

- Combo tối đa **×8**
- Chuỗi được giữ trong **7 giây**, hết thời hạn thì về ×1
- Nhặt bông tuyết được **100 × combo**
- Ngã là mất combo

Mỗi lần combo tăng sẽ có: sóng xung kích trắng→vàng quanh nhân vật, tiếng chuông leo
thang theo bậc, rung camera, và thẻ COMBO trên HUD nở rộng rồi từ từ co lại.

### Đáp xuống

| Điều kiện | Kết quả |
|---|---|
| Lệch < 12° | **PERFECT LANDING** +150 |
| Lệch < 45° | **CLEAN AIR** + thưởng airtime (50 điểm/giây) |
| Lệch > 65° | **Bad landing** — mất một mạng |

### Vật cản — luật va chạm đá

Ván (capsule 1.5 × 0.4) chạm đá (hình tròn bán kính 0.43) thì được coi là **trượt đá an toàn**
khi và chỉ khi thỏa **cả hai** điều kiện:

1. Nhân vật nằm ngang phía trên collider của đá: `|x − tâm đá| ≤ bán kính`
2. Mặt ván nằm trên mặt cong của chính cục đá tại đúng x đó:
   `y − 0.2 ≥ tâm.y + √(r² − dx²) − 0.08`

Luật này **thuần hình học** — không phụ thuộc pháp tuyến va chạm, tốc độ hay sự kiện
collision nào chạy trước, nên cùng một cú đụng luôn cho cùng kết quả.

Khi chạm sườn, thân ván cách tâm đá khoảng **1.18** trong khi bán kính chỉ **0.43**, lệch quá
xa để điều kiện (1) bị bỏ qua — **đâm ngang vào đá luôn mất một mạng**. Ngược lại, lướt qua
đỉnh thì an toàn vì điều kiện (2) bám theo vòm của đỉnh.

Góc bề mặt khi đáp lên đá lấy theo độ dốc thật của sườn núi, không theo pháp tuyến cong của
collider tròn, nên nhảy lên đáp đỉnh không bị phạt nhầm là *bad landing*.

**Thân cây luôn gây ngã** khi chạm ván.

### Shield

- Tối đa **3 lớp**, chồng lên nhau được.
- Mỗi lớp **dùng một lần rồi biến mất**, không mất mạng.
- Khi đỡ đòn, nhân vật được đặt xuống **ngay phía sau vật cản** (kèm chút quán tính) để không
  bị kẹt trước nó.
- Rơi khỏi dốc **luôn hồi phục được**, kể cả khi đang có shield.

### Vật phẩm

| | Tác dụng |
|---|---|
| ❄ Bông tuyết | +100 × combo điểm |
| ⚡ Speed Boost | Tăng tốc 5 giây, trần tốc độ 16 → 20 |
| 🛡 Shield | +1 lớp đỡ đòn |

---

## Âm thanh

| Nguồn | Cách tạo |
|---|---|
| Nhạc nền | Tổng hợp lúc chạy — mỗi núi một chủ đề riêng |
| Tiếng nhặt | Tổng hợp lúc chạy |
| Crash / Finish | File `.ogg` trong `Assets/Resources/SnowBoarder/Audio/` |

- **Nhạc nền**: Ridge Run 96 BPM (C trưởng, sáng), Glacier Ridge 78 BPM (chuông thuỷ tinh),
  Summit Descent 68 BPM (drone trầm). Đường trễ quấn ngược theo đúng độ dài vòng lặp nên loop
  không bị cắt. Clip được cache theo từng màn.
- **Tiếng bông tuyết leo thang điệu** theo hệ số combo (combo ×1 nghe nốt E5, ×8 nghe nốt A6)
  → nhặt liên hoàn nghe ra được điểm tăng dần.
- **Boost**: tiếng quét tần 180 Hz → 1.15 kHz. **Shield**: quãng năm đồng âm nổi lên.
- **Combo**: chuông sáng leo thang G4 → D5 kèm nhịp trầm.
- Âm lượng bám theo thanh AUDIO; có nút bật/tắt nhạc trong Options.

> **Vì sao nhạc không nhúng file?** FSBTool của Unity 6000.6.4f1 trên máy này báo
> `Failed decoding audio clip` với **mọi** file WAV hợp lệ (đã thử bản mono/stereo, 22.1k/44.1k,
> bản có `fact` và `LIST` chunk, và nhiều kiểu import setting khác nhau). Nếu nhúng file `.wav`
> thì game sẽ mất sạch âm thanh, nên toàn bộ nhạc và tiếng pickup được tổng hợp bằng code.
> Âm thanh dùng bảng tra sin 8192 mẫu nên mỗi chủ đề dựng trong khoảng 0.2 giây.

---

## Cấu trúc

```
Assets/
├── Scripts/
│   ├── SnowBoarderGame.cs          ← logic game: vật lý, va chạm, chấm điểm, vao ra màn
│   ├── SnowBoarderPresentation.cs  ← toàn bộ UI: menu, HUD, options, pause, kết quả
│   ├── SnowSynth.cs                ← bộ đồng bộ dùng chung (dao động, envelope, trễ, chuẩn hóa)
│   ├── SnowMusic.cs                ← 3 chủ đề nhạc nền, mỗi núi một bài
│   └── SnowSfx.cs                  ← tiếng bông tuyết, boost, shield, combo
├── Editor/
│   └── Lab2Validation.cs           ← 99 kiểm tra tự động (Editor-only)
├── Scenes/
│   └── SnowBoarder.unity           ← scene chính
└── Resources/SnowBoarder/          ← sprite, vật liệu, 2 file .ogg
```

### Điểm đáng chú ý trong code

- **Toàn bộ màn chơi sinh bằng code.** `Course()` dựng terrain bằng hàm `Height()` /
  `IceHeight()` / `SummitHeight()`, dựng mesh cho lớp đất và lớp tuyết, đặt `EdgeCollider2D`
  làm mặt đất, rồi rải ramp, đá, cây, vật phẩm và cảnh nền hai lớp. Đổi `track` là đổi được
  cả ngọn núi.
- **Menu dùng chính scene đang chơi** làm nền — camera bo vào nhân vật đứng sẵn trên dốc thay
  vì một ảnh nền tĩnh.
- **Sprite dùng URP 2D Sprite-Unlit** và `Full Rect` để không phụ thuộc ánh sáng scene.
- Tài nguyên sinh ra (mesh, material, texture, camera) đều được giải phóng trong `OnDestroy`.

---

## Kiểm thử tự động

`Assets/Editor/Lab2Validation.cs` chứa **99 kiểm tra** chạy headless:

```bash
Unity.exe -batchmode -projectPath "<project>" \
          -executeMethod Lab2Validation.Run -logFile Logs/Lab2.log
```

Kết quả lần chạy gần nhất: **99/99 PASS** trên Unity 6000.6.4f1.

Bao gồm các nhóm kiểm tra:

- Vật lý: tăng tốc từ đứng yên, tốc độ xoay và hãm xoay khi thả phím.
- Va chạm đá: đỉnh an toàn, **đâm ngang từ trái luôn mất đúng một mạng**, lướt qua đỉnh không
  bị phạt bad landing, góc bề mặt theo sườn núi.
- Shield một lần và đặt nhân vật ra sau vật cản.
- Combo tính ngay khi đủ vòng trên không, nhiều vòng cộng dồn.
- Ba múi có đúng độ dài, phong cảnh và vật cản khác nhau.
- Nhạc nền đủ chủ đề theo màn, tiếng pickup có ở mọi bậc combo, âm thanh kết thúc ở im lặng.
- Nhiệm vụ, Time Attack, lưu điểm cao.

> Các kiểm tra này dựng lại trạng thái có kiểm soát, **không thay thế việc chơi thử thực tế**.

---

## Ghi chú

- Điểm cao, âm lượng, trạng thái nhạc/tuyết và độ nhạy xoay lưu bằng `PlayerPrefs`.
- Bộ kiểm tra chạy xong sẽ **ghi đè `PlayerPrefs` về giá trị cũ** để không làm hỏng điểm cao
  của người chơi.
- `Lab2Assets/` (bản sao asset gốc của đề bài) **không được commit** — Unity không import thư
  mục nằm ngoài `Assets/`, và 14/19 file trong đó trùng GUID với bản đã nằm trong
  `Assets/Resources/SnowBoarder/`.

---

**Sinh viên:** ThaiTuan05 · **Môn:** PRU213 · **Lab:** 02