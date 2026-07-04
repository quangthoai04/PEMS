# PROMPT AI CODE — Sửa nhanh trang Report Staff Leader

> **Mục tiêu:** Sửa lại trang report của **Staff Leader** tại `/dashboard/reports` vì UI hiện tại còn xấu, nhiều khung/ô, ít chart, nhiều vùng trống và chức năng xuất báo cáo CSV quá sơ sài.

---

## 1. Bối cảnh hiện tại

Trang hiện tại đã đổi thành **Báo cáo vận hành campus**, nhưng vẫn còn các vấn đề:

```text
1. UI vẫn nhiều khung/card lớn, chiếm nhiều diện tích.
2. Một số section trống như “Khối lượng công việc Host”, “Logistics” nhưng vẫn chiếm chỗ lớn.
3. Không có chart chuyên nghiệp rõ ràng.
4. Pipeline hiện tại đơn giản, chưa đủ trực quan.
5. Export CSV hiện tại quá sơ sài, chỉ có vài dòng tiêu đề:
   - Partnership Engagement Management System
   - Staff Leader Campus Operation Report
   - Export Format: CSV
6. File export thiếu KPI, thiếu lifecycle, thiếu host workload, thiếu pending actions, thiếu logistics, thiếu close readiness, thiếu feedback.
```

---

## 2. Yêu cầu bắt buộc

```text
1. Không dùng mock data.
2. Không hard-code số liệu.
3. Không dùng random/faker/sample array.
4. Dữ liệu phải lấy từ DB/API thật hiện có.
5. Staff Leader chỉ xem dữ liệu campus của mình: currentUser.primary_campus_id.
6. Không xem dữ liệu campus khác.
7. Không dùng permissions/role_permissions/dynamic permission.
8. Không tự tạo field/table mới.
9. UI phải gọn hơn, ít khung hơn, ít khoảng trắng hơn.
10. Không để section trống lớn chiếm diện tích.
11. Export CSV phải chuyên nghiệp và có đầy đủ dữ liệu report.
12. Backend và frontend build phải pass.
```

---

## 3. Mục tiêu UI sau khi sửa

Trang Staff Leader Report phải trở thành **Campus Operation Dashboard** thật sự, giúp Staff Leader nhìn nhanh:

```text
Có bao nhiêu đơn cần duyệt?
Có bao nhiêu chuyến chưa gán host?
Chuyến nào đang chuẩn bị / đang diễn ra / sau tiếp khách?
Host nào đang phụ trách nhiều việc?
Logistics phòng ban nào đang chậm?
Hồ sơ nào chưa đóng được?
Feedback nào thấp cần xem?
Export báo cáo có đủ dữ liệu không?
```

---

## 4. Sửa Header

Giữ header gọn:

```text
Title: Báo cáo vận hành campus
Subtitle: Tổng quan phê duyệt, phân công host, logistics và chất lượng tiếp đón
Badge: Staff Leader · [Tên campus]
```

Bên phải:

```text
[ Năm nay / Tháng này / Quý này / Tùy chỉnh ]
[ Xuất báo cáo ]
```

Không tăng chiều cao header.

---

## 5. Sửa KPI — bỏ card lớn

Hiện KPI đang ổn hơn trước nhưng vẫn cần gọn hơn.

Yêu cầu:

```text
Bỏ kiểu nhiều card rời lớn.
Dùng 1 KPI strip mỏng duy nhất.
Các KPI chia bằng divider nhẹ.
Không dùng shadow/card quá dày.
Không padding quá lớn.
```

KPI hiển thị:

```text
Chờ duyệt
Chờ gán host
Đang chuẩn bị
Đang diễn ra
Sau tiếp khách
Chưa đóng/quá hạn
Feedback TB
Tổng khách
```

KPI nào quan trọng thì highlight nhẹ:

```text
Chờ duyệt: orange/warning
Chờ gán host: orange
Chưa đóng/quá hạn: red
Feedback TB: neutral/success
```

Không tự bịa delta.

---

## 6. Sửa khối “Cần xử lý”

Hiện alert bar có ít thông tin. Hãy đổi thành **compact action bar**.

Nội dung:

```text
Đơn cần duyệt
Chuyến chưa gán host
Logistics chậm
Hồ sơ sau tiếp khách chưa hoàn tất
Feedback thấp
```

Mỗi item gồm:

```text
Icon nhỏ
Số lượng
Label ngắn
Nút “Xem”
```

Click “Xem”:

```text
Filter bảng tương ứng hoặc scroll tới section tương ứng.
```

Không dùng alert box quá cao.

---

## 7. Chart bắt buộc phải có

Hiện trang thiếu chart chuyên nghiệp. Cần thêm chart thật, lấy data thật.

### 7.1. Chart 1 — Xu hướng chuyến thăm theo tháng

Tên:

```text
Xu hướng chuyến thăm theo tháng
```

Loại:

```text
Line chart hoặc bar chart.
```

Series:

```text
Tổng chuyến
Đã đóng
Bị hủy
Đang xử lý
```

Nếu data backend đang có series khác thì map tương ứng, nhưng không mock.

Yêu cầu UI:

```text
Chiều cao 260–300px.
Tooltip rõ.
Legend gọn.
Không gradient mạnh.
Không chiếm quá nhiều diện tích.
Có empty state nếu không có data.
```

### 7.2. Chart 2 — Phân bổ trạng thái chuyến

Tên:

```text
Phân bổ trạng thái chuyến
```

Loại:

```text
Donut chart hoặc horizontal bar chart.
```

Status:

```text
Chờ gán host
Đã gán host
Trước chuyến
Đang diễn ra
Sau chuyến
Đã đóng
Đã hủy
```

### 7.3. Chart 3 — Khối lượng host

Tên:

```text
Khối lượng host
```

Loại:

```text
Bar chart hoặc compact table.
```

Data:

```text
Host
Số chuyến phụ trách
Sắp tới 7 ngày
Đang chuẩn bị
Đang diễn ra
```

Nếu không có data:

```text
Không để box lớn trống.
Hiển thị compact empty state nhỏ:
“Không có host đang phụ trách trong bộ lọc này.”
```

---

## 8. Không để box trống lớn

Hiện các box như:

```text
Khối lượng công việc Host
Tiến độ logistics / hỗ trợ phòng ban
```

đang trống nhưng vẫn chiếm nhiều diện tích.

Yêu cầu:

```text
Nếu không có data, giảm chiều cao section.
Không để min-height quá lớn.
Không để box 200px chỉ có một dòng text.
Có thể ẩn section phụ nếu không có data và không quan trọng.
Empty state chỉ cao khoảng 72–96px.
```

Empty state mẫu:

```text
Không có dữ liệu trong bộ lọc này.
```

---

## 9. Bảng chính — Đơn cần Staff Leader xử lý

Thêm hoặc đưa lên cao bảng:

```text
Đơn cần Staff Leader xử lý
```

Columns:

```text
Ưu tiên
Tên đoàn
Loại đơn
Ngày thăm
Số khách
Trạng thái
Thời gian chờ
Hành động
```

Action:

```text
Xem chi tiết
Duyệt / Từ chối nếu flow hiện có hỗ trợ
Gán host nếu flow hiện có hỗ trợ
```

Không tự bịa action nếu route/modal chưa có. Nếu có flow sẵn thì điều hướng đúng flow.

---

## 10. Bảng Close Readiness

Thêm section:

```text
Hồ sơ cần hoàn tất sau tiếp khách
```

Columns:

```text
Đoàn
Host
Ngày kết thúc
Logistics
Minutes
News
Feedback
Có thể đóng
```

Badge:

```text
Đủ
Thiếu
Còn mở
Không cần
Chưa thể đóng
```

Mục tiêu:

```text
Staff Leader nhìn được chuyến nào kẹt vì logistics, minutes, news hay feedback.
```

---

## 11. Logistics section

Nếu có data, hiển thị compact table:

```text
Tiến độ logistics / hỗ trợ phòng ban
```

Columns:

```text
Phòng ban
Tổng yêu cầu
Chờ phản hồi
Đang xử lý
Hoàn thành
Từ chối
Quá hạn
```

Nếu không có data:

```text
Không để section cao.
Dùng empty state compact.
```

---

## 12. Feedback section

Bỏ cách chỉ hiển thị “đánh giá cao nhất” nếu nó không giúp vận hành.

Đổi thành section có tab:

```text
Feedback thấp cần chú ý
Feedback tốt gần đây
```

Columns:

```text
Đoàn
Host
Rating
Nội dung ngắn
Ngày thăm
Hành động
```

Tab mặc định nên là:

```text
Feedback thấp cần chú ý
```

Vì Staff Leader cần xử lý vấn đề trước.

---

## 13. Sửa Export CSV — yêu cầu bắt buộc

Export hiện tại quá sơ sài. Phải sửa để file CSV có đủ dữ liệu thật.

### 13.1. Tên file

```text
PEMS_StaffLeader_Campus_Report_YYYYMMDD_HHmm.csv
```

### 13.2. Header CSV

CSV phải có phần header:

```text
Partnership Engagement Management System
Staff Leader Campus Operation Report
Campus: [Tên campus]
Period: [fromDate - toDate]
Generated by: [Tên user]
Generated at: [datetime]
Applied filters: [status, host, logistics, rating...]
Export Format: CSV
```

### 13.3. Section 1 — Executive Summary

Gồm:

```text
Metric,Value
Chờ duyệt,...
Chờ gán host,...
Đang chuẩn bị,...
Đang diễn ra,...
Sau tiếp khách,...
Chưa đóng/quá hạn,...
Feedback TB,...
Tổng khách,...
```

### 13.4. Section 2 — Lifecycle Summary

Columns:

```text
Status,Count,Percentage
```

Rows:

```text
Chờ gán host
Đã gán host
Trước chuyến
Đang diễn ra
Sau chuyến
Đã đóng
Đã hủy
```

### 13.5. Section 3 — Host Workload

Columns:

```text
Host,Assigned,Upcoming 7 Days,Before Visit,During Visit,After Visit,Average Feedback
```

Nếu không có data:

```text
No data available for this section
```

### 13.6. Section 4 — Pending Actions

Columns:

```text
Request Code,Delegation Name,Type,Planned Date,Guest Count,Status,Waiting Hours
```

### 13.7. Section 5 — Logistics Summary

Columns:

```text
Department,Total,Requested,In Progress,Done,Rejected,Overdue
```

### 13.8. Section 6 — Close Readiness

Columns:

```text
Delegation,Host,Planned End,Logistics,Minutes,News,Feedback,Can Close,Blockers
```

### 13.9. Section 7 — Feedback Summary

Columns:

```text
Delegation,Host,Rating,Comment,Date
```

### 13.10. Export rules

```text
Không được chỉ export vài dòng tiêu đề.
Không export mock data.
Export phải dùng đúng filter hiện tại.
Nếu section không có data, ghi “No data available for this section”.
CSV cần escape dấu phẩy, xuống dòng, dấu ngoặc kép đúng chuẩn.
```

---

## 14. Backend cần sửa

Nếu API report/export hiện trả dữ liệu giả/rỗng thì phải sửa backend.

Yêu cầu:

```text
Endpoint Staff Leader report check:
role_code = STAFF
sub_role = LEADER

Scope:
currentUser.primary_campus_id

Không cho xem campus khác.
Không để ReportsController AllowAnonymous.
Không dùng dynamic permissions.
Dùng AsNoTracking.
Aggregate tại DB.
Không load toàn bộ DB lên memory.
Tránh N+1 query.
```

Các bảng có thể dùng:

```text
visit_requests
visit_request_campuses
visit_guest_members
visit_participants
users
departments
visit_logistics_items
visit_logistics_item_handovers
minutes
minute_action_items
feedbacks
feedback_rating_items
news
```

---

## 15. Frontend cần sửa

Sửa các phần liên quan:

```text
reports.types.ts
reportsApi.ts
useReports.ts hoặc useStaffLeaderReport.ts
reportsAdapter.ts
Page/component của /dashboard/reports
Export button/menu
```

Yêu cầu:

```text
Loading skeleton gọn.
Empty state compact.
Chart responsive.
Không tràn ngang page.
Table nhiều cột scroll trong container.
Không để section rỗng quá cao.
Không thêm thư viện chart mới nếu project đã có chart hiện hữu.
Nếu chưa có chart lib, dùng component/chart hiện có hoặc SVG đơn giản.
```

---

## 16. Build/test

Chạy:

```bash
dotnet build
```

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

Nếu không chạy được:

```text
Báo rõ lý do.
Không được nói đã hoàn thành 100%.
```

---

## 17. Acceptance criteria

Sau khi sửa:

```text
1. UI Staff Leader report gọn hơn rõ rệt.
2. Không còn nhiều box trống lớn.
3. Có chart xu hướng chuyến thăm.
4. Có chart phân bổ trạng thái chuyến.
5. Có chart/bảng khối lượng host.
6. Có bảng đơn cần xử lý.
7. Có bảng close readiness.
8. Logistics section không còn trống lớn.
9. Feedback section ưu tiên feedback thấp cần chú ý.
10. Dữ liệu lấy từ API/DB thật, không mock.
11. Scope đúng campus của Staff Leader.
12. Export CSV có đầy đủ header và các section dữ liệu.
13. CSV không chỉ có 3 dòng tiêu đề.
14. Export dùng đúng filter hiện tại.
15. Backend build pass.
16. Frontend build pass.
```

---

## 18. Báo cáo kết quả sau khi code

Sau khi hoàn thành, báo cáo theo format:

```text
Đã sửa UI:
- ...

Đã sửa API/data:
- ...

Đã sửa export CSV:
- ...

File đã sửa:
- ...

Build result:
- Backend:
- Frontend:

Cách test:
- ...
```

---

## 19. Lưu ý cuối

```text
Ưu tiên sửa đúng 2 vấn đề chính:
1. UI report hiện còn rỗng, nhiều khung, ít chart.
2. Export CSV hiện quá sơ sài, thiếu dữ liệu.

Không làm lan man sang role HO hoặc Department Leader.
Không thêm business flow mới nếu chưa có.
Không sửa schema.
Không dùng mock data.
```
