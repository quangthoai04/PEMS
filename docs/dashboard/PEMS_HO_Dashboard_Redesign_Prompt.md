# PROMPT AI CODE — Thiết kế lại Dashboard HO, tránh trùng với Report

> **Mục tiêu:** Sửa lại **Dashboard của role HO** để không bị trùng với trang Report. Dashboard phải là màn **tổng quan nhiệm vụ cần làm hôm nay**, còn Report mới là màn **thống kê/phân tích/xuất báo cáo**.

---

## 1. Bối cảnh hiện tại

Dashboard HO hiện đang bị trùng nội dung với trang Report:

```text
- Có các chỉ số kiểu Tổng đoàn khách, Tổng lượt khách, SLA.
- Có biểu đồ báo cáo lớn.
- Có nút Xuất báo cáo Excel.
- Nội dung giống màn thống kê/report.
- UI nhiều khung/card lớn, chiếm nhiều diện tích.
- Chưa thể hiện rõ hôm nay HO cần xử lý gì.
```

Cần thiết kế lại để Dashboard HO trở thành màn:

```text
Tổng quan công việc
Cảnh báo cần xử lý
Điều phối nhanh
Shortcut đến các module quan trọng
```

Không biến dashboard thành trang báo cáo.

---

## 2. Yêu cầu bắt buộc

```text
1. Không thay đổi business logic.
2. Không phá route/API hiện có.
3. Không sửa schema.
4. Không dùng mock data mới.
5. Không hard-code số liệu.
6. Không dùng dynamic permissions.
7. Không làm Dashboard giống Report.
8. Không đặt nút Export báo cáo trên Dashboard.
9. Không để nhiều card/khung to chiếm diện tích.
10. Nếu thiếu data thì dùng empty state gọn, không tạo box trống lớn.
11. Nếu cần thêm API, phải scope đúng role HO.
12. Build frontend phải pass.
13. Nếu sửa backend, backend build phải pass.
```

---

## 3. Phân biệt Dashboard và Report

### Dashboard HO

Dashboard dùng để trả lời:

```text
Hôm nay HO cần xử lý gì?
Có bao nhiêu đơn liên cơ sở đang chờ duyệt?
Có đơn nào chờ quá lâu không?
Chuyến nào sắp diễn ra?
Campus nào đang có cảnh báo?
Có feedback/email/news nào cần chú ý?
Đi nhanh tới màn xử lý ở đâu?
```

### Report HO

Report dùng để:

```text
Thống kê dài hạn.
Phân tích xu hướng.
So sánh campus.
Xem chart chuyên sâu.
Xuất báo cáo Excel/PDF/CSV.
```

Vì vậy, những phần như biểu đồ lớn, xuất Excel, thống kê toàn quốc chi tiết phải đưa sang `/dashboard/reports`, không đặt ở Dashboard chính.

---

## 4. Nội dung cần bỏ khỏi Dashboard HO

Bỏ hoặc chuyển sang Report các phần sau nếu đang có:

```text
BÁO CÁO THỐNG KÊ TOÀN QUỐC
Xuất Báo Cáo Excel
Biểu đồ Số lượng đoàn khách theo năm
Biểu đồ Cơ cấu loại hình đoàn khách
KPI Tổng đoàn khách
KPI Tổng lượt khách
KPI Tỷ lệ hoàn thành SLA
Các chart phân tích dài hạn
```

Nếu vẫn cần xem, thêm shortcut:

```text
Xem báo cáo
```

để điều hướng sang `/dashboard/reports`.

---

## 5. Bố cục Dashboard HO mới

Thiết kế theo layout sau:

```text
1. Header gọn
2. Quick action bar
3. KPI strip nhiệm vụ cần làm
4. Khối “Cần HO xử lý”
5. Lịch/chuyến sắp tới
6. Đơn liên cơ sở chờ duyệt
7. Tình trạng campus compact
8. Hoạt động/thông báo gần đây
```

Mục tiêu: trong màn đầu tiên phải nhìn thấy được:

```text
Header
Quick actions
KPI strip
Cần HO xử lý
Một phần lịch/chuyến sắp tới
```

---

## 6. Header gọn

Bỏ hero banner quá lớn hiện tại.

Yêu cầu:

```text
Chiều cao khoảng 96–120px.
Không dùng gradient mạnh.
Không tạo banner quá cao.
Không chiếm quá nhiều khoảng trắng.
```

Nội dung:

```text
Xin chào, Head Office Coordinator
Tổng quan công việc điều phối và phê duyệt liên cơ sở hôm nay
Badge: HO · Toàn hệ thống
Bên phải: thời gian hệ thống nhỏ gọn
```

Không đặt nút export trong header Dashboard.

---

## 7. Quick action bar

Đặt ngay dưới header.

Dạng 1 hàng compact, không dùng card to.

Các nút:

```text
Duyệt đơn liên cơ sở
Xem lịch tiếp khách
Quản lý campus
Quản lý tài khoản
Quản lý tin tức
Xem báo cáo
```

Thiết kế:

```text
Icon nhỏ + text
Button/shortcut nhỏ gọn
Không shadow lớn
Không card lồng card
Không chiếm chiều cao quá nhiều
```

Click điều hướng đúng route hiện có.

Không tự tạo route mới nếu chưa có.

---

## 8. KPI strip nhiệm vụ cần làm

Thay các card thống kê lớn bằng **1 KPI strip mỏng**.

KPI nên gồm:

```text
Đơn liên cơ sở chờ duyệt
Đơn chờ quá 48h
Chuyến sắp diễn ra 7 ngày tới
Hồ sơ sau tiếp khách chưa đóng
Feedback thấp cần xem
Email/action token chưa phản hồi
```

Thiết kế:

```text
Một section trắng mỏng.
Các KPI chia bằng divider nhẹ.
Không mỗi KPI một card lớn.
Không dùng shadow/card dày.
Padding thấp.
Số liệu rõ nhưng không quá to.
```

Màu highlight:

```text
Chờ duyệt: orange
Quá hạn: red
Sắp diễn ra: blue
Feedback thấp: amber/red
```

Không tự bịa số liệu.

---

## 9. Khối “Cần HO xử lý”

Đây là khối quan trọng nhất của Dashboard HO.

Thiết kế dạng **task list compact**, không phải bảng lớn.

Danh sách item:

```text
Đơn multi-campus đang chờ duyệt
Đơn multi-campus chờ duyệt quá 48h
Chuyến after-visit chưa closed
Feedback rating thấp
News/email/action token cần chú ý
```

Mỗi dòng gồm:

```text
Icon trạng thái nhỏ
Tiêu đề ngắn
Mô tả 1 dòng
Badge số lượng/trạng thái
Nút hành động: Xem
```

Ví dụ:

```text
5 đơn liên cơ sở đang chờ duyệt · Ưu tiên xử lý trong hôm nay · [Xem]
```

Không dùng alert box lớn, không tạo khung cao.

---

## 10. Lịch / chuyến sắp tới

Thêm section:

```text
Lịch tiếp khách sắp tới toàn hệ thống
```

Dạng:

```text
Timeline compact hoặc list compact
Không dùng calendar full lớn
```

Mỗi item gồm:

```text
Tên đoàn
Campus
Ngày giờ
Số khách
Trạng thái
Host/campus phụ trách nếu có
Link xem chi tiết
```

Hiển thị:

```text
Top 5–7 item
Nút Xem tất cả
```

Nếu không có data:

```text
Không có chuyến sắp tới trong 7 ngày.
```

Empty state chỉ cao khoảng 64–96px.

---

## 11. Đơn liên cơ sở chờ duyệt

Thêm compact table:

```text
Đơn liên cơ sở chờ duyệt
```

Columns:

```text
Mã đơn
Tên đoàn
Tổ chức
Campus đăng ký
Ngày thăm
Thời gian chờ
Trạng thái
Hành động
```

Action:

```text
Xem chi tiết
Duyệt/Từ chối nếu flow hiện tại đã có route/modal
```

Không tự tạo flow approve mới nếu chưa có.

Nếu không có data:

```text
Không có đơn liên cơ sở đang chờ duyệt.
```

Không để box trống lớn.

---

## 12. Tình trạng campus compact

Thêm section:

```text
Tình trạng vận hành campus
```

Không làm chart lớn như Report.

Dùng compact table hoặc summary list:

```text
Campus
Chuyến đang xử lý
Sắp diễn ra
Sau tiếp khách
Chưa đóng
Cảnh báo
```

Mục tiêu:

```text
HO nhìn nhanh campus nào đang có vấn đề.
```

Không cần biểu đồ phân tích sâu.

---

## 13. Hoạt động / thông báo gần đây

Thêm section:

```text
Hoạt động gần đây
```

Dữ liệu ưu tiên lấy từ:

```text
notifications
audit logs
activity API hiện có
```

Nếu chưa có API, dùng notification data hiện có, không mock.

Mỗi item:

```text
Nội dung hoạt động
Module liên quan
Thời gian
Trạng thái
```

Ví dụ:

```text
Staff Leader HCM đã duyệt campus instance của đoàn ABC
Feedback 2 sao được gửi cho chuyến DEF
Email invitation gửi thất bại cho 1 người nhận
```

Hiển thị top 5–8 item.

---

## 14. UI style

Thiết kế theo enterprise dashboard:

```text
Sạch
Gọn
Dễ đọc
Ít khung
Ít card lớn
Không màu mè
Không giống landing page
Không shadow đậm
Không border dày
Không gradient mạnh
Không card lồng card
Không animation thừa
```

Màu chuẩn:

```text
Primary blue: #004c91
Accent orange: #F37021
Page background: slate-50
Section background: white
Border: slate-200
Text chính: slate-800 / slate-900
Text phụ: slate-500 / slate-600
```

Spacing:

```text
Section padding p-4 hoặc p-5.
Không dùng p-8 cho nhiều section.
Row height compact.
Header không quá cao.
```

---

## 15. Empty state

Nếu không có dữ liệu:

```text
Không tạo mock data để lấp trống.
Không để box cao trống.
Không để section cao 200px chỉ có 1 dòng chữ.
```

Empty state gợi ý:

```text
Không có đơn cần xử lý.
Không có chuyến sắp tới trong bộ lọc hiện tại.
Chưa có cảnh báo mới.
```

Chiều cao khoảng:

```text
64–96px
```

---

## 16. Data/API

Ưu tiên dùng API dashboard hiện có.

Nếu cần thêm/sửa API, tạo endpoint riêng cho dashboard HO:

```http
GET /api/dashboard/ho-overview
```

Response gợi ý:

```text
kpis
actionItems
upcomingVisits
pendingMultiCampusRequests
campusStatusSummary
recentActivities
```

Không dùng endpoint report để render dashboard nếu nó làm dashboard bị giống Report.

Dashboard API chỉ trả dữ liệu vận hành ngắn gọn, không trả report phân tích dài.

---

## 17. Authorization

Nếu có thêm backend API:

```text
Chỉ role_code = HO được gọi.
Không dùng permissions/role_permissions.
Nếu không đăng nhập trả 401.
Nếu không phải HO trả 403.
HO được xem multi-campus toàn hệ thống.
HO được xem single-campus read-only monitoring nếu business rule hiện tại cho phép.
```

Không dựa vào frontend để bảo vệ dữ liệu.

---

## 18. Responsive

Desktop:

```text
Layout 2 cột cho “Cần HO xử lý” và “Lịch sắp tới”.
```

Tablet:

```text
Stack hợp lý.
```

Mobile:

```text
KPI thành 2 cột.
List full width.
Không tràn ngang page.
```

---

## 19. Acceptance criteria

Sau khi sửa:

```text
1. Dashboard HO không còn giống trang Report.
2. Không còn nút Export báo cáo trên Dashboard.
3. Không còn chart báo cáo lớn trên Dashboard.
4. UI gọn hơn, ít khung/card lớn hơn.
5. Header thấp và gọn hơn.
6. Có quick actions rõ ràng.
7. Có KPI strip nhiệm vụ cần làm.
8. Có khối “Cần HO xử lý”.
9. Có danh sách chuyến sắp tới.
10. Có bảng đơn multi-campus chờ duyệt.
11. Có tình trạng campus compact.
12. Có hoạt động/thông báo gần đây.
13. Không dùng mock data.
14. Không phá chức năng hiện có.
15. Không sửa schema.
16. Không dùng dynamic permissions.
17. Không tràn ngang page.
18. Frontend build pass.
19. Nếu sửa backend thì backend build pass.
```

---

## 20. Build/test

Frontend:

```bash
cd frontend/pems-react
npm run build
```

Nếu có:

```bash
npm run lint
npm run typecheck
```

Backend nếu có sửa API:

```bash
dotnet build
dotnet test
```

Nếu không chạy được:

```text
Báo rõ lý do.
Không được nói hoàn thành 100% nếu chưa build/test.
```

---

## 21. Báo cáo kết quả sau khi code

Sau khi hoàn thành, báo cáo theo format:

```text
Đã sửa Dashboard HO:
- ...

Đã bỏ/chuyển khỏi dashboard vì thuộc report:
- ...

Quick actions đã thêm:
- ...

Section mới:
- ...

API đã dùng/thêm:
- ...

File đã sửa:
- ...

Build result:
- Frontend:
- Backend nếu có:

Cách test:
- ...
```

---

## 22. Lưu ý cuối

```text
Dashboard = màn làm việc hôm nay.
Report = màn thống kê/phân tích/xuất báo cáo.

Không để hai màn trùng nhau.
Không đưa export vào Dashboard.
Không đưa chart phân tích dài hạn vào Dashboard.
Không tạo nhiều card lớn.
Không dùng mock data.
Không sửa schema.
Không phá logic hiện có.
```
