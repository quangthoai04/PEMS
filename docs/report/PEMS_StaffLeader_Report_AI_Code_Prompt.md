> [!WARNING]
> **LEGACY ARCHITECTURE NOTE (Campus-independent Approval Update)**
> This document has been updated to reflect the new Campus-independent Approval architecture.
> - **HO is now monitor/read-only.** There is no centralized multi-campus approval by HO.
> - **Staff Leader approval is per-campus.** Each Staff Leader directly receives and approves/rejects their own campus instance right after submission.
> - **Self-hosting is supported.** Staff Leaders can assign themselves as the host during approval.
> - **ASSIGNED is removed.** Approving a request now requires assigning a host immediately.
> - **New statuses:** `PARTIALLY_APPROVED` (request level) and `REJECTED` (campus level) are added. 
> - **Cancel logic:** Visitors can cancel requests in `PENDING_APPROVAL` or `PARTIALLY_APPROVED` states.
> - **Transportation:** `transportation_note` and `transportation_note` are replaced by `transportation_note`.
> Please refer to the latest codebase and SQL schema for the current implementation.

# PROMPT AI CODE — Trang Report cho role Staff Leader

> **Mục tiêu:** Code lại trang report cho **Staff Leader** tại `/dashboard/reports` theo hướng **Campus Operation Report**: gọn hơn, ít khung/ô hơn, hiển thị được nhiều thông tin vận hành hơn, lấy **data thật từ DB/API hiện có**, không dùng mock data.

---

## 1. Vai trò của AI code

Bạn là **Senior Full-stack Engineer** cho dự án **PEMS — Partnership Engagement Management System**.

Nhiệm vụ: thiết kế lại và triển khai thật trang report cho **role Staff Leader**.

Staff Leader không phải HO. Trang này chỉ tập trung vào **campus của Staff Leader**, gồm:

```text
Đơn single-campus chờ duyệt
Đơn/chuyến chờ gán host
Tiến độ visit tại campus
Host workload
Logistics theo phòng ban
Hồ sơ chưa đóng sau tiếp khách
Feedback campus
News/media sau chuyến thăm
Export báo cáo vận hành campus
```

---

## 2. Yêu cầu bắt buộc

```text
1. Không dùng mock data.
2. Không hard-code số liệu.
3. Không dùng random/faker/sample array.
4. Lấy dữ liệu thật từ database thông qua backend API.
5. Backend phải enforce role/scope, không chỉ ẩn UI frontend.
6. Staff Leader chỉ xem dữ liệu thuộc primary_campus_id của mình.
7. Không xem/sửa dữ liệu campus khác.
8. Không dùng permissions/role_permissions/dynamic permission.
9. Không tự tạo field/table mới.
10. Không phá flow approve/reject/assign host hiện có.
11. UI phải gọn, ít card lớn, không quá nhiều khung/ô.
12. Export báo cáo dùng đúng filter hiện tại.
13. Backend build và frontend build phải pass.
```

---

## 3. File/khu vực cần kiểm tra

Tìm đúng page đang render route:

```text
/dashboard/reports
```

Kiểm tra/sửa các khu vực:

```text
backend/PEMS.Api/Controllers/ReportsController.cs
backend/PEMS.Application/Reports/**
frontend/pems-react/src/features/reports/api/reportsApi.ts
frontend/pems-react/src/features/reports/hooks/useReports.ts
frontend/pems-react/src/features/reports/adapters/reportsAdapter.ts
frontend/pems-react/src/features/reports/types/reports.types.ts
frontend/pems-react/src/pages/**/reports**
frontend/pems-react/src/routes/**
```

Search thêm:

```text
Thống kê & Báo cáo
Lượt khách tham quan
Các đoàn khách đánh giá cao nhất
reportsApi
useReports
/dashboard/reports
```

---

## 4. Backend API cần có

Tạo/sửa endpoint:

```http
GET /api/reports/staff-leader-overview
```

Query params:

```text
fromDate?: string
toDate?: string
preset?: THIS_MONTH | THIS_QUARTER | THIS_YEAR | CUSTOM
visitStatus?: ALL | ASSIGNED | ASSIGNED | BEFORE_VISIT | DURING_VISIT | AFTER_VISIT | CLOSED | CANCELLED
requestStatus?: ALL | PENDING_APPROVAL | APPROVED | REJECTED | CANCELLED
hostUserId?: number | ALL
departmentId?: number | ALL
logisticsStatus?: string | ALL
feedbackRating?: ALL | LOW | HIGH
```

Export endpoint:

```http
POST /api/reports/staff-leader-overview/export
```

Body dùng cùng filter, thêm:

```text
exportFormat: PDF | EXCEL | CSV
reportSections: string[]
```

Nếu project chưa có thư viện PDF/Excel thì implement CSV trước, không tự thêm thư viện mới nếu chưa được yêu cầu.

---

## 5. Authorization/scope

Backend bắt buộc:

```text
Chỉ role_code = STAFF và sub_role = LEADER được gọi Staff Leader report.
Scope dữ liệu = currentUser.primary_campus_id.
Nếu không đăng nhập → 401.
Nếu không phải Staff Leader → 403.
Nếu cố truyền campus khác → ignore hoặc 403.
```

Staff Leader được xem:

```text
Single-campus request thuộc campus mình.
Multi-campus campus instance thuộc campus mình sau khi HO approve.
Host/staff/logistics/feedback/news thuộc campus mình.
```

Không được xem toàn hệ thống như HO.

---

## 6. DTO response gợi ý

Tạo DTO:

```text
StaffLeaderReportOverviewDto
```

Các phần cần trả:

```text
generatedAt
filterSummary
kpis
attentionItems
campusLifecyclePipeline
monthlyTrend
hostWorkload
logisticsByDepartment
pendingActionRequests
closeReadiness
feedbackSummary
newsMediaSummary
```

### KPI cần có

```text
pendingSingleCampusApproval
waitingHostAssignment
assignedVisits
beforeVisit
duringVisit
afterVisit
closedVisits
overdueOrNotClosed
averageFeedbackRating
totalGuests
```

Nếu muốn gọn chỉ hiển thị 6 KPI chính:

```text
Chờ duyệt
Chờ gán host
Đang chuẩn bị
Đang diễn ra
Sau tiếp khách
Chưa đóng/quá hạn
```

### Attention items

```text
singleCampusPendingOver24h
waitingHostAssignmentCount
upcomingVisitsWithoutPreparation
logisticsDelayedCount
afterVisitMissingMinutesNewsFeedback
lowFeedbackCount
```

### Lifecycle pipeline

Dùng `visit_request_campuses.status`:

```text
ASSIGNED
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

### Host workload

```text
hostUserId
hostName
assignedCount
upcoming7Days
beforeVisitCount
duringVisitCount
afterVisitCount
averageFeedbackRating
conflictCount nếu có data
```

### Logistics by department

```text
departmentId
departmentName
totalItems
requested
assigned
accepted
inProgress
done
rejected
declined
cancelled
overdueCount
```

### Pending action requests

Danh sách việc Staff Leader cần xử lý:

```text
type: APPROVAL | ASSIGN_HOST | MONITOR_LOGISTICS | CLOSE_READY
requestId
visitInstanceId
requestCode
delegationName
organizationName
plannedStartAt
plannedEndAt
guestCount
status
waitingHours
actionLabel
detailUrl
```

### Close readiness

```text
visitInstanceId
requestCode
delegationName
hostName
plannedEndAt
logisticsOpenCount
missingHandoverSignatureCount
openActionItemCount
hasMinutes
hasPublishedNews
newsNotRequired
feedbackCount
canClose
blockers[]
```

### Feedback summary

```text
averageRating
totalFeedbacks
lowFeedbackCount
topRatedVisits[]
lowRatedVisits[]
ratingByHost[]
```

### News/media summary

```text
publishedNewsCount
pendingNewsCount
missingNewsCount
newsNotRequiredCount
mediaUploadedCount
```

---

## 7. Query logic gợi ý

Dùng data thật:

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
files
```

Logic chính:

```text
pendingSingleCampusApproval:
visit_requests.visit_scope = 'SINGLE_CAMPUS'
AND visit_requests.status = 'PENDING_APPROVAL'
AND campus = currentUser.primary_campus_id

waitingHostAssignment:
visit_request_campuses.campus_id = currentUser.primary_campus_id
AND status = 'ASSIGNED'

host workload:
group by visit_request_campuses.current_host_user_id

logistics:
join visit_logistics_items theo visit_instance_id thuộc campus currentUser.primary_campus_id

close readiness:
check logistics terminal, handover signature, minutes, action items, news, feedback

feedback:
AVG rating trong campus scope
```

Cẩn thận:

```text
Không tính lifecycle từ visit_requests.status.
Không lấy campus khác.
Không load toàn bộ DB lên memory.
Dùng AsNoTracking.
Tránh N+1 query.
```

---

## 8. UI/UX thiết kế lại

### Nguyên tắc

```text
Enterprise dashboard
Gọn
Ít khung/ô
Ít card lớn
Không card lồng card
Không quá nhiều khoảng trắng
Không chart quá cao
Không tràn ngang page
Table compact
Chart chuyên nghiệp, tooltip/legend rõ
Màu chính: #004c91
Màu nhấn: #F37021
```

### Header

```text
Breadcrumb: Dashboard / Báo cáo campus
Title: Báo cáo vận hành campus
Subtitle: Tổng quan xử lý yêu cầu, phân công host, logistics và chất lượng tiếp đón
Badge: Staff Leader · Campus [Tên campus]
```

Bên phải:

```text
Filter nhanh
Xuất báo cáo
```

### Filter bar

Hiển thị rõ, không giấu hết sau icon:

```text
[ Khoảng thời gian ] [ Trạng thái chuyến ] [ Host ] [ Department ] [ Logistics status ] [ Rating ] [ Áp dụng ] [ Reset ]
```

Không cho chọn campus khác. Nếu cần, chỉ hiển thị read-only:

```text
Campus: HCM
```

### KPI strip

Thay 4 card lớn bằng 6–8 KPI nhỏ trong một section:

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

Thiết kế:

```text
1 section trắng mỏng
KPI nhỏ
Divider nhẹ
Không shadow lớn
Không padding quá cao
```

### Khối “Việc cần xử lý”

Đặt ngay dưới KPI:

```text
4 đơn single-campus đang chờ duyệt
3 chuyến đã duyệt nhưng chưa gán host
2 chuyến sắp diễn ra nhưng chưa chuẩn bị xong
5 logistics request đang chậm
2 chuyến AFTER_VISIT thiếu minutes/news/feedback
```

Mỗi item có nút:

```text
Xem
```

Click sẽ filter bảng tương ứng hoặc scroll tới section.

### Lifecycle pipeline

Thêm section:

```text
Tiến độ chuyến thăm tại campus
```

Các bước:

```text
Chờ gán host → Đã gán host → Trước tiếp khách → Đang tiếp → Sau tiếp khách → Đã đóng / Đã hủy
```

Mỗi step có:

```text
count
percentage
```

Click step để filter.

### Chart chính

Dùng 2 cột:

Cột trái 65%:

```text
Xu hướng chuyến thăm campus theo tháng
Series: Request / Campus instance / Closed / Cancelled hoặc Approved / Rejected / Closed
```

Cột phải 35%:

```text
Phân bổ lifecycle hoặc phân bổ trạng thái chuyến
```

Không dùng chart quá cao. Chiều cao khoảng 260–300px.

### Bảng “Đơn cần Staff Leader xử lý”

Đặt trước bảng feedback.

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
Duyệt
Từ chối
Gán host
Xem chi tiết
```

Chỉ hiện action nếu flow hiện có đã hỗ trợ. Nếu không, chỉ link tới detail/flow sẵn có.

### Host workload

Section:

```text
Hiệu suất host
```

Columns:

```text
Host
Đang phụ trách
Sắp tới 7 ngày
Đang chuẩn bị
Đang diễn ra
Feedback TB
Xung đột lịch
```

Có highlight nhẹ nếu host quá tải hoặc có conflict.

### Logistics theo phòng ban

Section:

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

Dùng badge/progress nhỏ, không tạo card riêng cho từng phòng ban.

### Close readiness

Section:

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

### Feedback/news

Gom trong một section có tab:

```text
[ Feedback gần đây ] [ News & Media ]
```

Tab Feedback:

```text
Đánh giá thấp cần chú ý
Đánh giá cao nhất
Rating theo host
```

Tab News & Media:

```text
News published
News pending
Chuyến thiếu news
Media uploaded
```

---

## 9. Export report

Nút:

```text
Xuất báo cáo
```

Dropdown:

```text
Xuất tổng quan campus
Xuất danh sách đơn chờ xử lý
Xuất workload host
Xuất logistics report
Xuất close readiness
Xuất feedback/news report
```

Format:

```text
PDF / Excel / CSV tùy backend hỗ trợ
```

Tên file:

```text
PEMS_StaffLeader_Campus_Report_YYYYMMDD_HHmm
```

Header file export:

```text
Partnership Engagement Management System
Staff Leader Campus Operation Report
Campus: [Tên campus]
Period: ...
Generated by: ...
Generated at: ...
Filters: ...
```

Export phải dùng đúng filter đang áp dụng.

---

## 10. Frontend loading/empty/error

Loading:

```text
Skeleton KPI
Skeleton chart
Skeleton table
```

Empty:

```text
Không có dữ liệu trong khoảng thời gian đã chọn.
Không có đơn cần xử lý.
Không có chuyến cần hoàn tất hồ sơ.
```

Error:

```text
Không thể tải báo cáo. Vui lòng thử lại.
```

403:

```text
Bạn không có quyền xem báo cáo Staff Leader.
```

---

## 11. Performance

Backend:

```text
AsNoTracking
Aggregate ở DB
Limit top 10 cho bảng preview
Không N+1 query
Không load toàn bộ bảng vào memory
Có cancellationToken
```

Frontend:

```text
Không refetch liên tục khi chưa bấm Áp dụng
Không render bảng quá dài ở dashboard
Có “Xem thêm” hoặc pagination
Export có loading, chặn double click
```

---

## 12. Acceptance criteria

Sau khi hoàn thành:

```text
1. Role Staff Leader vào /dashboard/reports thấy dashboard mới.
2. UI gọn hơn, ít card/khung/ô hơn.
3. Không còn mock/hard-code data.
4. KPI/chart/table lấy từ API thật.
5. Dữ liệu chỉ thuộc campus của Staff Leader.
6. Không xem được campus khác.
7. Filter thay đổi thì toàn bộ report cập nhật.
8. Export dùng đúng filter hiện tại.
9. Không phải Staff Leader gọi API bị 403.
10. Có loading/empty/error state.
11. Không tràn ngang page.
12. Table nhiều cột chỉ scroll trong table container.
13. Backend build pass.
14. Frontend build pass.
15. Báo cáo kết quả liệt kê file sửa, API sửa, cách test, build result.
```

---

## 13. Lệnh build/test

Backend:

```bash
dotnet restore
dotnet build
dotnet test
```

Frontend:

```bash
cd frontend/pems-react
npm install
npm run build
```

Nếu có script:

```bash
npm run lint
npm run typecheck
```

Nếu không chạy được vì thiếu DB/secret/env, phải nói rõ lý do, không được báo hoàn thành 100%.

---

## 14. Lưu ý cuối

```text
1. Đây là report vận hành campus, không phải report toàn hệ thống như HO.
2. Không copy UI/nội dung của HO.
3. Không copy UI/nội dung của Dept Leader.
4. Không dùng mock data.
5. Không làm đẹp UI nhưng số liệu sai.
6. Không tự thêm business flow mới.
7. Không thêm host transfer nếu rule hiện tại không cho phép.
8. Không dùng dynamic permissions.
9. Không tự sửa schema.
10. Ưu tiên dữ liệu đúng, scope đúng, UI gọn và dễ dùng.
```
