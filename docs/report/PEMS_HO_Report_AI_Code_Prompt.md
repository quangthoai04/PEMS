# PROMPT AI CODE — Thiết kế lại trang Report cho role HO

> **Mục tiêu:** Thiết kế lại và triển khai thật trang **Head Office Report** cho PEMS tại route `/dashboard/reports`, theo hướng **gọn hơn, chuyên nghiệp hơn, ít khung/ô hơn, hiển thị được nhiều thông tin tổng quan hơn**, đồng thời **lấy dữ liệu thật từ database hiện có, không dùng mock data**.

---

## 0. Vai trò của AI code

Bạn là **Senior Full-stack Engineer** cho dự án **PEMS — Partnership Engagement Management System**.

Bạn cần làm việc như:

```text
Senior .NET 8 Clean Architecture Developer
Senior React TypeScript Engineer
Database-first MySQL Engineer
Security / Authorization Reviewer
Enterprise Dashboard UI/UX Engineer
Report / Data Aggregation Engineer
```

Khi làm task này, phải kiểm tra đồng bộ cả backend và frontend:

```text
Database/schema hiện có
→ Entity/DbContext/EF config nếu cần
→ DTO/Query/Handler
→ Controller/API route
→ Authorization/scope
→ Frontend type
→ API service
→ Hook
→ Adapter
→ Page/component UI
→ Export report
→ Build/test
```

Không được chỉ sửa UI nếu dữ liệu vẫn là mock.

---

## 1. Bối cảnh hiện tại

Trang hiện tại tại:

```text
/dashboard/reports
```

đang có UI dạng dashboard với các nội dung như:

```text
Tổng chuyến tham quan
Tổng lượt khách
Chuyến sắp tới
Điểm đánh giá TB
Biểu đồ lượt khách tham quan
Phân bổ loại đoàn khách
Các đoàn khách đánh giá cao nhất
```

Vấn đề:

```text
1. UI có quá nhiều khung/card lớn.
2. Nhiều khoảng trắng, làm màn dài nhưng chứa ít thông tin.
3. Nội dung còn chung chung, giống báo cáo tham quan thông thường.
4. Chưa phản ánh đúng nhu cầu của role HO.
5. Chưa tập trung vào multi-campus request, approval, campus performance, close readiness, feedback/news/email quality.
6. Có nguy cơ đang dùng mock/hard-code data.
7. Export báo cáo cần chuyên nghiệp hơn vì đây là chức năng quan trọng.
```

---

## 2. Mục tiêu cuối cùng

Thiết kế lại trang **HO Report** theo hướng:

```text
Gọn hơn
Ít khung/ô hơn
Tổng quan hơn
Dễ đọc hơn
Có tính điều hành hơn
Chart chuyên nghiệp hơn
Bảng compact hơn
Export báo cáo chuyên nghiệp hơn
Dùng data thật từ DB hiện có
Không dùng mock data
Có authorization đúng cho role HO
```

Trang này phải giúp HO trả lời nhanh:

```text
Có bao nhiêu đơn multi-campus đang chờ duyệt?
Có đơn nào chờ HO duyệt quá lâu không?
Tỷ lệ duyệt/từ chối/hủy là bao nhiêu?
Campus nào đang xử lý nhiều chuyến nhất?
Campus nào còn nhiều hồ sơ chưa đóng?
Chuyến nào đã xong nhưng chưa đủ điều kiện đóng?
Feedback toàn hệ thống đang tốt hay xấu?
News/email/action token có vấn đề gì không?
Có thể xuất báo cáo theo đúng filter hiện tại không?
```

---

## 3. Yêu cầu bắt buộc về dữ liệu

### 3.1. Tuyệt đối không dùng mock data

Không được:

```text
Không dùng mock array.
Không hard-code số liệu như 124, 4.500, 4.8, 12.
Không dùng random/faker data.
Không dùng dữ liệu mẫu trong component.
Không để API trả object rỗng giả.
Không để NotImplementedException ở endpoint report được màn này gọi.
Không tự tạo field/table mới nếu schema chưa có.
```

Phải:

```text
Lấy dữ liệu thật từ database hiện có.
Nếu API report hiện đang mock thì phải sửa backend để aggregate thật.
Nếu dữ liệu thiếu thì hiển thị empty state chuyên nghiệp, không tự bịa số.
Nếu một section chưa có dữ liệu tương ứng thì trả về count = 0 hoặc empty list từ backend.
```

### 3.2. Các bảng nên dùng để tổng hợp report

Ưu tiên kiểm tra và dùng các bảng hiện có:

```text
visit_requests
visit_request_campuses
visit_guest_members
visit_participants
campuses
users
feedbacks
feedback_rating_items
news
sent_emails
sent_email_recipients
email_action_tokens
visit_logistics_items
visit_logistics_item_handovers
minutes
minute_action_items
files
notifications
audit_logs nếu cần
```

Lưu ý:

```text
visit_requests = trạng thái đơn tổng / approval request.
visit_request_campuses = tiến độ chuyến thăm theo từng campus.
Không được tính lifecycle campus từ visit_requests.status.
Không được dùng dynamic permissions.
Không được query bảng permissions / role_permissions vì schema hiện tại đã bỏ.
```

---

## 4. Scope role HO

Trang này chỉ dành cho **Head Office — HO**.

### 4.1. Quyền xem dữ liệu

HO được xem:

```text
Tổng quan toàn hệ thống.
Tất cả multi-campus request.
Campus performance toàn bộ cơ sở.
Feedback/news/email tổng hợp toàn hệ thống.
SINGLE_CAMPUS nếu hệ thống hiện hỗ trợ HO monitoring read-only.
```

HO không được:

```text
Không xử lý thay Staff Leader các hành động vận hành campus chi tiết nếu flow hiện tại không cho phép.
Không xử lý logistics/task của phòng ban.
Không sửa dữ liệu ngoài scope.
Không có business quyền như Admin nếu không được quy định.
```

### 4.2. Authorization bắt buộc

Backend phải enforce:

```text
Chỉ role_code = HO được gọi endpoint HO report.
Nếu user không đăng nhập → 401.
Nếu user đăng nhập nhưng không phải HO → 403.
Frontend chỉ ẩn UI không đủ, backend vẫn phải kiểm tra.
```

Nếu `ReportsController` hiện chưa có authorization, phải vá ngay:

```text
Thêm [Authorize] hoặc attribute/policy hiện có của project.
Thêm role/scope check trong handler/service nếu controller chỉ xác thực chung.
Không để /api/reports/* gọi ẩn danh.
```

---

## 5. Các file/khu vực cần kiểm tra trước khi code

Trước khi sửa, hãy tìm đúng file hiện đang render route `/dashboard/reports`.

Kiểm tra tối thiểu:

```text
backend/PEMS.Api/Controllers/ReportsController.cs

backend/PEMS.Application/Reports/
backend/PEMS.Application/Reports/Queries/
backend/PEMS.Application/Reports/Commands/

frontend/pems-react/src/features/reports/api/reportsApi.ts
frontend/pems-react/src/features/reports/hooks/useReports.ts
frontend/pems-react/src/features/reports/adapters/reportsAdapter.ts
frontend/pems-react/src/features/reports/types/reports.types.ts

frontend/pems-react/src/pages/**/reports**
frontend/pems-react/src/routes/**
frontend/pems-react/src/layouts/**
frontend/pems-react/src/components/**
```

Nếu tên file khác, hãy search theo:

```text
/dashboard/reports
Thống kê & Báo cáo
Quản lý báo cáo
Xuất báo cáo
reportsApi
useReports
```

---

## 6. Backend API cần có

### 6.1. Endpoint lấy overview

Tạo hoặc sửa endpoint:

```http
GET /api/reports/ho-overview
```

Query params:

```text
fromDate?: string
toDate?: string
preset?: THIS_MONTH | THIS_QUARTER | THIS_YEAR | CUSTOM
campusId?: number | ALL
visitScope?: ALL | SINGLE_CAMPUS | MULTI_CAMPUS
requestStatus?: ALL | PENDING_APPROVAL | APPROVED | REJECTED | CANCELLED
campusInstanceStatus?: ALL | WAITING_REQUEST_APPROVAL | WAITING_HOST_ASSIGNMENT | ASSIGNED | BEFORE_VISIT | DURING_VISIT | AFTER_VISIT | CLOSED | CANCELLED
visitType?: string | ALL
```

Nếu project đã có route convention khác, dùng route hiện có nhưng phải giữ contract rõ ràng.

### 6.2. Endpoint export

Tạo hoặc sửa endpoint:

```http
POST /api/reports/ho-overview/export
```

Body:

```json
{
  "fromDate": "2026-01-01",
  "toDate": "2026-12-31",
  "preset": "THIS_YEAR",
  "campusId": "ALL",
  "visitScope": "ALL",
  "requestStatus": "ALL",
  "campusInstanceStatus": "ALL",
  "visitType": "ALL",
  "exportFormat": "EXCEL",
  "reportSections": [
    "EXECUTIVE_SUMMARY",
    "APPROVAL_OVERVIEW",
    "CAMPUS_PERFORMANCE",
    "LIFECYCLE_CLOSE_READINESS",
    "FEEDBACK_QUALITY",
    "CONTENT_EMAIL_EFFECTIVENESS"
  ]
}
```

Supported formats:

```text
PDF
EXCEL
CSV
```

Lưu ý:

```text
Nếu project chưa có thư viện PDF/Excel, không tự thêm thư viện mới nếu chưa được phép.
Trước tiên kiểm tra project đã có thư viện export nào chưa.
Nếu chưa có, implement CSV trước và để TODO rõ cho PDF/Excel.
Nếu đã có export service hiện hữu, tái sử dụng.
```

---

## 7. DTO response đề xuất

Tạo DTO response rõ ràng.

```csharp
public sealed class HoReportOverviewDto
{
    public DateTime GeneratedAt { get; set; }
    public HoReportFilterSummaryDto FilterSummary { get; set; }
    public HoReportKpisDto Kpis { get; set; }
    public List<HoAttentionItemDto> AttentionItems { get; set; }
    public List<HoMonthlyTrendDto> MonthlyTrend { get; set; }
    public HoApprovalBreakdownDto ApprovalBreakdown { get; set; }
    public List<HoCampusPerformanceDto> CampusPerformance { get; set; }
    public List<HoLifecyclePipelineItemDto> LifecyclePipeline { get; set; }
    public List<HoPendingMultiCampusRequestDto> MultiCampusPendingRequests { get; set; }
    public List<HoCloseReadinessDto> CloseReadiness { get; set; }
    public HoFeedbackSummaryDto FeedbackSummary { get; set; }
    public HoContentEmailSummaryDto ContentAndEmailSummary { get; set; }
}
```

### 7.1. Filter summary

```text
generatedAt
preset
fromDate
toDate
campusId
campusName
visitScope
requestStatus
campusInstanceStatus
visitType
generatedByUserId
generatedByName
```

### 7.2. KPI

```text
totalRequests
multiCampusPending
approvedRequests
rejectedRequests
cancelledRequests
activeCampusInstances
closedCampusInstances
overdueCloseInstances
averageDecisionHours
averageFeedbackRating
totalGuests
```

### 7.3. Attention items

```text
key
label
count
severity: INFO | WARNING | DANGER | SUCCESS
description
targetSection
```

Các item cần có:

```text
pendingHoApprovalOver48h
afterVisitNotClosed
closedWithoutFeedback
missingNewsOrNewsNotConfirmed
emailActionExpiredOrNoResponse
lowFeedbackCount
```

### 7.4. Monthly trend

```text
month
monthLabel
totalRequests
singleCampusRequests
multiCampusRequests
approved
rejected
cancelled
totalGuests
```

### 7.5. Approval breakdown

```text
approved
rejected
pending
cancelled
approvalRate
rejectionRate
averageDecisionHours
```

### 7.6. Campus performance

```text
campusId
campusCode
campusName
totalInstances
waitingHostAssignment
assigned
beforeVisit
duringVisit
afterVisit
closed
cancelled
averageFeedbackRating
overdueCloseCount
guestCount
```

### 7.7. Lifecycle pipeline

```text
status
labelVi
count
percentage
```

Status:

```text
WAITING_REQUEST_APPROVAL
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

### 7.8. Multi-campus pending requests

```text
requestId
requestCode
delegationName
organizationName
submittedAt
plannedStartAt
plannedEndAt
requestedCampusCount
guestCount
waitingHours
status
detailUrl nếu frontend cần
```

### 7.9. Close readiness

```text
visitInstanceId
requestId
requestCode
delegationName
campusName
plannedEndAt
hostName
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

Blockers ví dụ:

```text
LOGISTICS_OPEN
HANDOVER_SIGNATURE_MISSING
ACTION_ITEMS_OPEN
MINUTES_MISSING
NEWS_MISSING
FEEDBACK_MISSING
```

### 7.10. Feedback summary

```text
averageRating
totalFeedbacks
lowFeedbackCount
topRatedVisits[]
lowRatedVisits[]
ratingByCampus[]
```

### 7.11. Content/email summary

```text
publishedNewsCount
pendingNewsCount
instancesMissingNewsCount
emailSentCount
emailFailedCount
emailDeliveredRate
actionTokenRespondedCount
actionTokenExpiredCount
actionTokenPendingCount
```

---

## 8. Query logic gợi ý

### 8.1. Total requests

```text
COUNT(visit_requests)
```

Filter theo:

```text
submitted_at hoặc planned_start_at tùy schema hiện tại và logic report hiện có.
Nếu report là operational report, ưu tiên planned_start_at/planned_end_at.
Nếu report là request intake report, ưu tiên submitted_at.
Có thể ghi rõ trong tooltip UI: “Tính theo ngày gửi yêu cầu” hoặc “Tính theo ngày thăm”.
```

### 8.2. Multi-campus pending

```text
visit_requests.visit_scope = 'MULTI_CAMPUS'
AND visit_requests.status = 'PENDING_APPROVAL'
```

### 8.3. Approved / rejected / cancelled

```text
GROUP BY visit_requests.status
```

Status:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

### 8.4. Campus instance lifecycle

Lấy từ:

```text
visit_request_campuses.status
```

Không lấy lifecycle từ `visit_requests.status`.

### 8.5. Active campus instances

```text
visit_request_campuses.status NOT IN ('CLOSED', 'CANCELLED')
```

### 8.6. Closed campus instances

```text
visit_request_campuses.status = 'CLOSED'
```

### 8.7. Overdue close instances

Gợi ý:

```text
planned_end_at < NOW()
AND visit_request_campuses.status IN ('AFTER_VISIT', 'DURING_VISIT', 'BEFORE_VISIT', 'ASSIGNED')
```

Nếu business rule hiện có định nghĩa khác, ưu tiên code/rule hiện tại.

### 8.8. Total guests

Ưu tiên:

```text
COUNT(visit_guest_members)
```

join theo `visit_request_id` hoặc `visit_instance_id` tùy schema thực tế.

Nếu DB có `guest_count` snapshot thì dùng field hiện có, nhưng phải kiểm tra schema.

### 8.9. Average decision hours

```text
TIMESTAMPDIFF(HOUR, submitted_at, decided_at)
```

Chỉ tính request đã có `decided_at`.

### 8.10. Feedback rating

```text
AVG(feedbacks.rating)
```

Nếu rating nằm trong `feedback_rating_items`, tính average từ rating item hoặc field overall rating tùy schema thực tế.

Không tự bịa field.

### 8.11. Close readiness

Tổng hợp:

```text
logisticsOpenCount:
visit_logistics_items.status NOT IN ('DONE', 'REJECTED', 'DECLINED', 'CANCELLED')

missingHandoverSignatureCount:
visit_logistics_item_handovers
WHERE borrower_signed_at IS NULL OR provider_signed_at IS NULL

openActionItemCount:
minute_action_items.status NOT IN ('DONE', 'CANCELLED')

hasMinutes:
EXISTS minutes WHERE visit_instance_id = ...

hasPublishedNews:
EXISTS news WHERE visit_instance_id = ... AND status = 'PUBLISHED'

newsNotRequired:
visit_request_campuses.news_not_required = true

feedbackCount:
COUNT(feedbacks)
```

`canClose = true` khi không còn blocker theo rule hiện tại.

### 8.12. Email/action token summary

Tổng hợp từ:

```text
sent_emails
sent_email_recipients
email_action_tokens
```

Gợi ý:

```text
emailSentCount
emailFailedCount
emailDeliveredRate
actionTokenRespondedCount: used_at IS NOT NULL hoặc result_status thành công
actionTokenExpiredCount: expires_at < NOW() AND used_at IS NULL
actionTokenPendingCount: used_at IS NULL AND expires_at >= NOW()
```

Kiểm tra enum/field thực tế trước khi code.

---

## 9. Backend implementation rules

### 9.1. Clean Architecture

Không viết logic aggregate trong Controller.

Controller chỉ:

```text
Validate route basics nếu cần
Gọi IMediator
Trả ApiResponse/FileResult
```

Logic ở:

```text
PEMS.Application/Reports/Queries/...
PEMS.Application/Reports/Commands/...
```

Nếu cần repository/service, đặt đúng layer hiện có.

### 9.2. EF Core performance

Phải:

```text
Dùng AsNoTracking cho read-only query.
Aggregate ở DB càng nhiều càng tốt.
Tránh load toàn bộ bảng rồi tính trong memory.
Tránh N+1 query.
Dùng cancellationToken.
Top N cho preview list.
Pagination hoặc limit cho bảng dài.
```

Preview limit đề xuất:

```text
multiCampusPendingRequests: top 10
closeReadiness: top 10
topRatedVisits: top 5
lowRatedVisits: top 5
campusPerformance: all campuses
```

### 9.3. Error handling

Không trả stack trace.

Trả lỗi rõ:

```text
401: Phiên đăng nhập không hợp lệ hoặc đã hết hạn.
403: Bạn không có quyền xem báo cáo Head Office.
500: Không thể tải dữ liệu báo cáo. Vui lòng thử lại sau.
```

### 9.4. Không tự sửa schema

Không tạo bảng/field mới.

Nếu field không tồn tại:

```text
Tìm field tương ứng trong schema/entity hiện tại.
Nếu không có dữ liệu, bỏ section đó hoặc trả 0/empty.
Ghi TODO rõ, không tự ALTER TABLE.
```

---

## 10. Frontend UI/UX cần thiết kế lại

### 10.1. Nguyên tắc thiết kế

Thiết kế theo kiểu **enterprise dashboard**:

```text
Sạch
Gọn
Chuyên nghiệp
Tập trung dữ liệu
Dễ scan
Ít khung/ô
Ít khoảng trắng thừa
Không màu mè
Không gradient mạnh
Không shadow quá đậm
Không card lồng card
Không làm giống landing page
```

Màu:

```text
Primary blue: #004c91
Primary orange: #F37021
Text chính: slate-800 / slate-900
Text phụ: slate-500 / slate-600
Border: slate-200
Background page: slate-50
Card/section: white
Warning: amber/orange nhẹ
Danger: red nhẹ
Success: green nhẹ
```

### 10.2. Mục tiêu giảm khung/ô

Thay vì nhiều card lớn riêng lẻ, dùng:

```text
1 container chính max-width 1400.
Header không nằm trong card.
Filter bar mỏng.
KPI strip chung trong một section.
KPI ngăn bằng divider nhẹ thay vì mỗi KPI là một card lớn.
Chart section 2 cột, không lồng card con.
Bảng compact, row thấp.
Tabs/segmented controls để gom nội dung liên quan.
Không tạo nhiều section dọc nếu có thể gom bằng tab.
```

Mục tiêu:

```text
Ở màn đầu tiên sau header phải nhìn thấy:
- Filter chính
- KPI strip
- Attention items
- Một phần chart chính
```

Không để người dùng phải cuộn quá nhiều mới thấy dữ liệu quan trọng.

---

## 11. Layout UI mới đề xuất

### 11.1. Header compact

Nội dung:

```text
Breadcrumb: Dashboard / Quản lý báo cáo
Title: Báo cáo Head Office
Subtitle: Tổng quan yêu cầu tiếp đón, hiệu suất cơ sở và chất lượng sau chuyến thăm
Badge: HO · Toàn hệ thống
```

Bên phải:

```text
Khoảng thời gian nhanh
Nút Xuất báo cáo
```

Không để title quá cao.

### 11.2. Filter bar gọn

Filter chính phải nhìn thấy, không giấu hết sau icon phễu.

Controls:

```text
Khoảng thời gian: Năm nay / Tháng này / Quý này / Tùy chỉnh
Campus: Tất cả / HN / HCM / DN / CT / QN
Scope: Tất cả / Single-campus / Multi-campus
Request status
Campus instance status
Nút Áp dụng
Nút Reset
```

Gợi ý UI:

```text
[ Năm nay v ] [ Campus: Tất cả v ] [ Scope: Multi-campus v ] [ Status: Tất cả v ] [ Áp dụng ] [ Reset ]
```

Nếu nhiều filter quá:

```text
Hiển thị 4 filter chính.
Các filter còn lại đặt trong “Bộ lọc nâng cao”.
Nhưng filter đang áp dụng phải hiển thị thành chips nhỏ.
```

### 11.3. KPI strip compact

Không dùng 4 card lớn như hiện tại.

Dùng 8 KPI nhỏ:

```text
Tổng yêu cầu
Chờ HO duyệt
Đã duyệt
Bị từ chối
Campus đang xử lý
Đã đóng
Quá hạn đóng hồ sơ
Feedback TB
```

Thiết kế:

```text
Một section trắng duy nhất.
Grid 4 cột x 2 hàng hoặc 8 cột trên wide screen.
Mỗi KPI nhỏ: label + value + optional delta.
Icon nhỏ, không chiếm nhiều diện tích.
Dùng divider hoặc border nhẹ.
```

Highlight:

```text
Chờ HO duyệt: warning nhẹ.
Quá hạn đóng hồ sơ: danger nhẹ.
Feedback TB: success/info nhẹ.
```

Không tự bịa delta nếu backend không trả.

### 11.4. Khối “Cần HO chú ý”

Đặt ngay dưới KPI.

Nội dung:

```text
Đơn multi-campus chờ duyệt quá 48h
Campus instance AFTER_VISIT chưa CLOSED
Chuyến thiếu feedback/news
Email/action token chưa phản hồi hoặc hết hạn
Feedback thấp cần xem
```

Thiết kế:

```text
Một hàng compact dạng alert summary.
Mỗi item là chip/card nhỏ ngang.
Có count lớn nhỏ vừa đủ.
Có nút “Xem”.
Click “Xem” sẽ filter bảng tương ứng hoặc scroll tới section.
```

Không dùng card quá cao.

### 11.5. Chart chính

Hàng chart chính gồm 2 cột:

#### Cột trái 65%

```text
Xu hướng yêu cầu theo tháng
```

Series:

```text
Tổng yêu cầu
Single-campus
Multi-campus
Approved
Rejected nếu phù hợp
```

#### Cột phải 35%

```text
Tỷ lệ quyết định
```

Dạng:

```text
Donut hoặc compact bar
Approved / Rejected / Pending / Cancelled
```

Yêu cầu chart:

```text
Chiều cao 260–300px.
Tooltip rõ ràng.
Legend gọn.
Không gradient mạnh.
Không chiếm quá nhiều chiều cao.
Có empty state khi không có data.
```

### 11.6. Lifecycle pipeline

Section:

```text
Tiến độ campus instances
```

Các trạng thái:

```text
Chờ duyệt
Chờ gán host
Đã gán host
Trước tiếp khách
Đang tiếp
Sau tiếp khách
Đã đóng
Đã hủy
```

Tương ứng DB status:

```text
WAITING_REQUEST_APPROVAL
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Thiết kế:

```text
Dạng step/segment ngang.
Mỗi step có count + percentage.
Màu nhẹ.
Click step để lọc bảng.
Không dùng quá nhiều icon.
```

### 11.7. Campus performance table

Section:

```text
Hiệu suất theo cơ sở
```

Columns:

```text
Campus
Tổng chuyến
Chờ host
Chuẩn bị
Đang tiếp
Sau tiếp
Đã đóng
Quá hạn
Feedback TB
```

Thiết kế:

```text
Table compact.
Header xanh #004c91 hoặc header nhẹ slate tùy UI hiện tại.
Row height khoảng 52–56px.
Có mini progress bar cho tỷ lệ đã đóng hoặc quá hạn.
Campus có quá hạn/feedback thấp highlight nhẹ.
Nếu nhiều cột, scroll ngang trong table container.
```

### 11.8. Multi-campus pending requests

Section:

```text
Đơn liên cơ sở cần HO xử lý
```

Columns:

```text
Mã đơn
Tên đoàn
Tổ chức
Số campus
Số khách
Ngày thăm
Thời gian chờ
Trạng thái
Hành động
```

Action:

```text
Xem chi tiết
```

Không tự thêm approve/reject trực tiếp nếu flow approve/reject đã có màn/modal riêng. Nếu route/modal có sẵn, link đúng tới đó.

Empty state:

```text
Không có đơn liên cơ sở đang chờ xử lý.
```

### 11.9. Close readiness

Section:

```text
Hồ sơ sau tiếp khách cần hoàn tất
```

Columns:

```text
Đoàn
Campus
Host
Kết thúc
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
Có thể đóng
Chưa thể đóng
```

Dùng badge nhỏ, không dùng icon quá nhiều màu.

Click row:

```text
Navigate tới visit instance detail nếu route có sẵn.
Hoặc mở drawer read-only nếu đã có component.
Không tự tạo flow sửa dữ liệu phức tạp.
```

### 11.10. Feedback & Content Quality

Gom vào một section dùng tab/segmented control:

```text
[ Feedback ] [ News & Email ]
```

Tab Feedback:

```text
Average rating
Total feedback
Low feedback count
Top high-rated visits
Top low-rated visits
Rating by campus
```

Tab News & Email:

```text
Published news
Pending news
Missing news
Email delivered rate
Failed emails
Action tokens pending/expired/responded
```

Không tách thành quá nhiều card dọc.

---

## 12. Export report chuyên nghiệp

### 12.1. Export dropdown

Nút:

```text
Xuất báo cáo
```

Mở dropdown:

```text
Xuất báo cáo tổng quan HO
Xuất campus performance
Xuất đơn liên cơ sở chờ xử lý
Xuất close readiness
Xuất feedback & content
```

Định dạng:

```text
PDF
Excel
CSV
```

Nếu chỉ hỗ trợ CSV hiện tại, UI phải disable PDF/Excel hoặc ghi “Sắp hỗ trợ”, không cho click lỗi.

### 12.2. Confirm trước khi export

Trước khi export, hiển thị popover/modal nhỏ:

```text
Báo cáo: Head Office Report
Khoảng thời gian: ...
Campus: ...
Scope: ...
Trạng thái: ...
Section: ...
Định dạng: ...
```

Nút:

```text
Hủy
Xuất báo cáo
```

### 12.3. File export

Tên file:

```text
PEMS_HO_Report_YYYYMMDD_HHmm.pdf
PEMS_HO_Report_YYYYMMDD_HHmm.xlsx
PEMS_HO_Report_YYYYMMDD_HHmm.csv
```

Header báo cáo:

```text
Partnership Engagement Management System
Head Office Report
Period: ...
Campus filter: ...
Visit scope: ...
Generated by: ...
Generated at: ...
```

Sections trong báo cáo:

```text
1. Executive Summary
2. Approval & Request Overview
3. Campus Performance
4. Lifecycle & Close Readiness
5. Feedback Quality
6. Content & Email Effectiveness
```

### 12.4. Export phải dùng đúng filter

Bắt buộc:

```text
Filter trên UI đang áp dụng thế nào thì export ra đúng như vậy.
Không export toàn bộ nếu user đang filter.
Không export mock data.
Không export dữ liệu ngoài scope HO.
```

### 12.5. Loading/error export

Khi export:

```text
Nút export có loading.
Chặn double click.
Nếu 403: “Bạn không có quyền xuất báo cáo Head Office.”
Nếu 500: “Không thể xuất báo cáo. Vui lòng thử lại sau.”
Nếu không có dữ liệu: vẫn cho export file với header + empty state, hoặc hỏi xác nhận.
```

---

## 13. Frontend implementation

Sửa theo module reports hiện có.

### 13.1. Types

Trong:

```text
frontend/pems-react/src/features/reports/types/reports.types.ts
```

Thêm type:

```text
HoReportOverview
HoReportKpis
HoAttentionItem
HoMonthlyTrend
HoApprovalBreakdown
HoCampusPerformance
HoLifecyclePipelineItem
HoPendingMultiCampusRequest
HoCloseReadiness
HoFeedbackSummary
HoContentEmailSummary
HoReportFilters
HoReportExportRequest
```

Không dùng `any` tràn lan.

### 13.2. API service

Trong:

```text
frontend/pems-react/src/features/reports/api/reportsApi.ts
```

Thêm:

```text
getHoReportOverview(filters)
exportHoReport(filters, format, sections)
```

Phải gọi backend thật.

Không trả mock.

### 13.3. Hook

Trong:

```text
frontend/pems-react/src/features/reports/hooks/useReports.ts
```

Hoặc tạo hook riêng:

```text
useHoReport()
```

Hook cần quản lý:

```text
filters
appliedFilters
data
loading
error
refetch
exportLoading
exportError
applyFilters
resetFilters
exportReport
```

### 13.4. Adapter

Trong:

```text
frontend/pems-react/src/features/reports/adapters/reportsAdapter.ts
```

Map:

```text
Status DB → label tiếng Việt
Status DB → badge color
Number → compact number
Rating → 1 decimal
Date → dd/MM/yyyy
Waiting hours → “2 ngày 4 giờ”
Percentage → “78%”
```

Không map sai status.

### 13.5. Page/component

Tìm file hiện render `/dashboard/reports`.

Sửa UI theo layout mới.

Có thể tách component nhỏ nếu cần nhưng không refactor quá sâu:

```text
HoReportHeader
HoReportFilterBar
HoKpiStrip
HoAttentionBar
HoTrendChart
HoApprovalChart
HoLifecyclePipeline
HoCampusPerformanceTable
HoPendingRequestsTable
HoCloseReadinessTable
HoFeedbackContentSection
HoExportMenu
```

Không tạo quá nhiều component nếu project hiện đang đơn giản, nhưng cần code dễ đọc.

---

## 14. Loading / Empty / Error states

### 14.1. Loading

Hiển thị skeleton:

```text
KPI skeleton strip
Chart skeleton
Table skeleton rows
```

Không để trắng màn hình.

### 14.2. Empty

Từng section có empty riêng:

```text
Không có dữ liệu trong khoảng thời gian đã chọn.
Không có đơn liên cơ sở đang chờ xử lý.
Không có hồ sơ sau tiếp khách cần hoàn tất.
Chưa có feedback trong bộ lọc hiện tại.
```

### 14.3. Error

Thông báo tiếng Việt:

```text
Không thể tải báo cáo. Vui lòng thử lại.
```

Có nút:

```text
Thử lại
```

### 14.4. Forbidden

Nếu API trả 403:

```text
Bạn không có quyền xem báo cáo Head Office.
```

Không render dashboard rỗng.

---

## 15. Chart design notes

Yêu cầu chart:

```text
Không quá cao.
Không dùng màu quá nhiều.
Không gradient mạnh.
Không legend dài.
Tooltip rõ ràng.
Trục gọn.
Có empty state.
Không vỡ layout khi sidebar thu gọn.
```

Nếu dùng line chart:

```text
Tối đa 3–4 series.
Đường rõ.
Không fill area quá đậm.
```

Nếu dùng donut:

```text
Có center label tổng.
Legend compact 2 cột.
Không dùng quá nhiều slice.
```

Nếu dùng pipeline:

```text
Dùng step segments thay vì chart phức tạp.
Count rõ.
Percentage nhỏ.
```

---

## 16. Thiết kế để giảm chiều dài màn

Áp dụng các kỹ thuật:

```text
1. KPI strip thay card lớn.
2. Attention bar dạng ngang.
3. Chart row 2 cột.
4. Feedback/News/Email gom bằng tab.
5. Table preview top 10 + “Xem thêm”.
6. Không đặt mỗi dữ liệu một card riêng.
7. Không dùng padding p-8 cho mọi section.
8. Section padding p-4 hoặc p-5.
9. Table row compact.
10. Dùng grid responsive thay vì stack dọc quá sớm.
```

Mục tiêu:

```text
Dashboard nhìn được tổng quan ngay trong 1–1.5 màn đầu.
Các bảng dài nằm phía dưới, có preview và xem thêm.
```

---

## 17. Business notes cần nhớ

### 17.1. HO khác Staff Leader

HO:

```text
Tập trung vào toàn hệ thống và multi-campus.
Xem campus performance.
Xem chất lượng feedback/news/email.
Không đi sâu xử lý task phòng ban.
```

Staff Leader:

```text
Tập trung campus của mình, host assignment, visit operation.
```

Dept Leader:

```text
Tập trung task/logistics phòng ban.
```

Không copy nguyên dashboard Staff Leader/Dept Leader sang HO.

### 17.2. Không nhầm request status và campus instance status

```text
visit_requests.status:
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED

visit_request_campuses.status:
WAITING_REQUEST_APPROVAL
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Report HO phải dùng đúng tầng dữ liệu.

### 17.3. Không dùng dynamic permissions

Không dùng:

```text
permissions
role_permissions
permission_code
permission_level
```

Authorization dựa trên:

```text
role_code
sub_role
primary_campus_id
department_id
campus scope
owner scope
participant relationship
record status
```

Với HO report, tối thiểu check `role_code = HO`.

### 17.4. Không tự thêm business flow mới

Không thêm:

```text
Approve/reject inline nếu flow hiện tại không có.
Host assignment trong HO report.
Logistics handling trong HO report.
Department task processing trong HO report.
```

Report là tổng hợp và điều hướng, không biến thành màn xử lý tất cả.

---

## 18. Security checklist

Bắt buộc kiểm tra:

```text
ReportsController có [Authorize] hoặc policy tương đương.
HO endpoint check role HO.
Export endpoint check role HO.
Không trả dữ liệu nếu token thiếu/sai.
Không dựa vào frontend role.
Không log dữ liệu nhạy cảm.
Không lộ stack trace.
Không AllowAnonymous cho report.
Không dùng debug-user hoặc endpoint dev để test report trong code production.
```

Nếu phát hiện security issue khác, ghi rõ trong kết quả.

---

## 19. Performance checklist

Bắt buộc:

```text
AsNoTracking cho query read-only.
Aggregate tại DB.
Limit top N preview.
Không N+1 query.
Không load toàn bộ dataset lớn vào memory.
Có cancellationToken.
Có index-awareness nếu query chậm.
Không gọi nhiều API nhỏ không cần thiết nếu có thể dùng một overview endpoint.
```

Frontend:

```text
Không render bảng hàng trăm row trong dashboard preview.
Có pagination hoặc xem thêm.
Không re-fetch liên tục mỗi lần gõ filter nếu chưa bấm Apply.
Không export nhiều lần khi double click.
```

---

## 20. Accessibility / UX checklist

```text
Button có label rõ.
Icon không đứng một mình nếu gây khó hiểu.
Badge status có text, không chỉ màu.
Chart có tooltip/legend.
Empty state có text rõ.
Error state có nút retry.
Focus state không bị mất.
Contrast đủ đọc.
Responsive không vỡ trên tablet.
```

---

## 21. Acceptance criteria

Sau khi hoàn thành, phải đạt:

```text
1. Vào /dashboard/reports bằng role HO thấy UI HO Report mới.
2. UI gọn hơn bản cũ, ít card lớn/khung/ô hơn.
3. Màn đầu tiên hiển thị được filter, KPI, attention items và chart chính.
4. Không còn số liệu mock/hard-code.
5. KPI lấy từ API thật.
6. Chart lấy từ API thật.
7. Bảng campus performance lấy từ API thật.
8. Bảng multi-campus pending lấy từ API thật.
9. Close readiness lấy từ API thật.
10. Feedback/news/email summary lấy từ API thật nếu có dữ liệu.
11. Thay đổi filter làm toàn bộ report cập nhật.
12. Export dùng đúng filter hiện tại.
13. Không phải HO gọi API bị 403.
14. Loading/empty/error state hoạt động.
15. Không tràn ngang page.
16. Table nhiều cột chỉ scroll trong container.
17. Backend build pass.
18. Frontend build pass.
19. Không còn NotImplementedException ở endpoint report được gọi.
20. Báo cáo cuối task liệt kê file đã sửa, API contract, cách test, build result.
```

---

## 22. Lệnh build/test cần chạy

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

Nếu project có:

```bash
npm run lint
npm run typecheck
```

Phải chạy nếu có script tương ứng.

Nếu không chạy được vì thiếu môi trường/database/secret:

```text
Nói rõ lệnh nào không chạy được.
Nói rõ lỗi.
Không báo “hoàn thành 100%”.
Đưa hướng dẫn để người dùng tự chạy.
```

---

## 23. Cách báo cáo kết quả sau khi code

Sau khi làm xong, trả lời theo format:

```text
Đã hoàn thành:
- Backend:
  - ...
- Frontend:
  - ...
- Export:
  - ...
- Security:
  - ...

API đã thêm/sửa:
- GET ...
- POST ...

File đã sửa:
- ...

Cách test:
- ...

Build result:
- Backend: pass/fail
- Frontend: pass/fail

Lưu ý còn lại:
- ...
```

Không được nói chung chung “đã tối ưu UI” nếu không mô tả cụ thể đã sửa gì.

---

## 24. Lưu ý quan trọng cuối cùng

```text
1. Đây là report quan trọng của HO, ưu tiên độ tin cậy dữ liệu hơn hiệu ứng UI.
2. Không dùng mock data trong bất kỳ trường hợp nào.
3. Không làm UI đẹp nhưng số liệu sai.
4. Không làm dashboard quá dài bằng cách thêm quá nhiều card.
5. Không biến report thành màn xử lý nghiệp vụ hỗn hợp.
6. Không phá route/sidebar hiện có.
7. Không đổi business rule nếu chưa được yêu cầu.
8. Không tự thêm thư viện nặng nếu project chưa có.
9. Không tự tạo DB migration/ALTER TABLE.
10. Không bỏ qua authorization cho ReportsController.
```
