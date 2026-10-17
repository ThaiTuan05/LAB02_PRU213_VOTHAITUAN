# Snow Boarder

Game trượt tuyết 2D phong cách pixel. Chọn một trong ba ngọn núi, trượt xuống về đích, thu thập
bông tuyết, làm trick trên không và né vật cản trên đường.

## Cách chạy

Unity **6000.6.4f1** → mở `Assets/Scenes/SnowBoarder.unity` → bấm **Play**.

## Điều khiển

| Phím | Hành động |
|---|---|
| `W` / `↑` | Đẩy ván, tăng tốc |
| `S` / `↓` | Phanh |
| `A` `D` / `←` `→` | Nghiêng khi trượt, xoay khi bay |
| `Shift` + `A`/`D` | Xoay nhanh để làm trick |
| `Space` | Nhảy |
| `Esc` | Tạm dừng |
| `R` | Chơi lại |

## Ba ngọn núi

| Núi | Độ dài | Hạn | Đặc điểm |
|---|---|---|---|
| **Ridge Run** | 285 m | 40 s | Dốc thoải, ít chướng ngại — dành cho luyện điều khiển |
| **Glacier Ridge** | 470 m | 60 s | Dốc hơn, nhiều ramp, có những cú nảy lớn |
| **Summit Descent** | 700 m | 90 s | Dốc nhất, chướng ngại dày, trượt trong đêm |

**Free Ride** chơi theo nhịp của bạn, không giới hạn thời gian.
**Time Attack** phải về đích trong đúng số giây ghi ở bảng trên.

Điểm cao được lưu riêng cho từng ngọn núi và từng chế độ.

## Nhiệm vụ mỗi lượt

| Nhiệm vụ | Thưởng |
|---|---|
| Thu thập 10 bông tuyết | +300 |
| Đáp một vòng 360° đầy đủ | +400 |
| Về đích còn ít nhất 2 mạng | +500 |

Bạn có **3 mạng**. Hết mạng là kết thúc lượt.

## Combo trên không

Mỗi vòng 360° bạn xoay trên không sẽ tăng hệ số combo ngay trong lúc đang bay, không cần
đợi tiếp đất:

- Combo tối đa **×8**
- Chuỗi được giữ trong **7 giây**, hết thời hạn thì về ×1
- Điểm flip = `250 × số vòng × combo`, cộng thêm `175 × (số vòng − 1) × combo` nếu xoay nhiều
  vòng trong cùng một cú nhảy
- Bông tuyết được **100 × combo**
- Ngã là mất combo

Mỗi lần combo tăng sẽ có sóng xung kích trắng chuyển dần sang vàng quanh nhân vật, tiếng
chuông leo thang theo bậc combo, camera rung nhẹ, và thẻ COMBO trên HUD nở rộng rồi từ từ
co lại.

## Đáp xuống

| Độ lệch | Kết quả |
|---|---|
| Dưới 12° | **Perfect Landing** — cộng thêm 150 điểm |
| Dưới 45° | **Clean Air** — thưởng thêm theo thời gian bay |
| Trên 65° | **Bad landing** — mất một mạng |

## Vật cản

- **Đá**: lướt qua đỉnh đá thì an toàn và còn được bonus, nhưng đâm ngang vào sườn đá thì
  **luôn mất một mạng**, không có ngoại lệ. Thân cây thì đụng vào là ngã.
- Vạch cam ở thân cây và dưới chân vật cản để bạn nhận biết trước khi tới.
- Cây lá trên dốc phía sau là cảnh nền, không gây ngã.

## Shield

- Mỗi lớp shield đỡ **đúng một đòn** rồi biến mất, không mất mạng.
- Có thể chồng tối đa **3 lớp**.
- Khi đỡ đòn, bạn được đặt xuống ngay phía sau vật cản kèm chút quán tính để không bị kẹt.
- Rơi khỏi dốc luôn được đưa về an toàn, kể cả khi đang có shield.

## Vật phẩm trên đường

| | Tác dụng |
|---|---|
| ❄ **Bông tuyết** | Cộng điểm, giá trị tăng theo combo |
| ⚡ **Speed Boost** | Tăng tốc trong 5 giây |
| 🛡 **Shield** | Thêm một lớp đỡ đòn |

Tiếng khi nhặt bông tuyết sẽ **cao dần theo hệ số combo**, nên nhặt liên hoàn nghe ra được
điểm đang tăng. Mỗi ngọn núi có một chủ đề nhạc nền riêng; trong Options có thanh âm lượng
và nút bật/tắt nhạc.

---

**Sinh viên:** ThaiTuan05 · **Môn:** PRU213 · **Lab:** 02