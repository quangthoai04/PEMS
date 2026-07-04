# PROMPT AI CODE — Sửa trang Report cho role Department Leader

> **Mục tiêu:** Code lại trang report cho **Department Leader** tại `/dashboard/reports` theo hướng **Department Operation Report**: gọn hơn, ít khung/ô hơn, tập trung vào công việc phòng ban, nhân sự, bàn giao, phát sinh, feedback và **thêm chức năng xuất hóa đơn PDF** dựa trên số lượng đồ/nguồn lực host yêu cầu phòng ban chuẩn bị nhân với đơn giá Department Leader nhập.

---

## 1. Vai trò của AI code

Bạn là **Senior Full-stack Engineer** cho dự án **PEMS — Partnership Engagement Management System**.

Nhiệm vụ:

```text
Thiết kế lại UI report cho Department Leader.
Lấy data thật từ DB/API hiện có.
Không dùng mock data.
Thêm chức năng xuất hóa đơn PDF chuyên nghiệp.
Đảm bảo dữ liệu chỉ thuộc department của Department Leader hiện tại.
```

Department Leader report phải tập trung vào:

```text
Yêu cầu logistics/task gửi tới phòng ban
Công việc chờ phân công
Công việc chờ nhân sự phản hồi
Công việc đang xử lý/quá hạn
Hiệu suất nhân sự phòng ban
Đề xuất thay đổi
Ký mượn/ký trả/bàn giao
Phát sinh hư hỏng/thiếu/cần xử lý
Feedback về phòng ban/logistics
Xuất báo cáo
Xuất hóa đơn PDF
```

---

## 2. Yêu cầu bắt buộc

```text
1. Không dùng mock data.
2. Không hard-code số liệu.
3. Không dùng random/faker/sample array.
4. Lấy dữ liệu thật từ database thông qua backend API.
5. Department Leader chỉ xem dữ liệu thuộc department_id của mình.
6. Không xem dữ liệu department khác.
7. Không dùng permissions/role_permissions/dynamic permission.
8. Không tự tạo field/table mới nếu chưa được yêu cầu.
9. Không phá flow logistics/handover hiện có.
10. UI phải gọn, ít khung/ô, nhìn được tổng quan.
11. Export báo cáo dùng đúng filter hiện tại.
12. Thêm chức năng xuất hóa đơn PDF trước mắt.
13. Hóa đơn phải thiết kế chuyên nghiệp, có thông tin người xuất, phòng ban, đoàn/visit, danh sách item, số lượng, đơn giá, thành tiền, tổng tiền.
14. Backend build và frontend build phải pass.
```

---

## 3. File/khu vực cần kiểm tra

Tìm đúng page đang render:

```text
/dashboard/reports
```

Kiểm tra/sửa:

```text
backend/PEMS.Api/Controllers/ReportsController.cs
backend/PEMS.Application/Reports/**
backend/PEMS.Application/DepartmentReceptionTasks/**
backend/PEMS.Application/VisitLogistics/**
backend/PEMS.Domain/Entities/**
backend/PEMS.Infrastructure/**

frontend/pems-react/src/features/reports/api/reportsApi.ts
frontend/pems-react/src/features/reports/hooks/useReports.ts
frontend/pems-react/src/features/reports/adapters/reportsAdapter.ts
frontend/pems-react/src/features/reports/types/reports.types.ts
frontend/pems-react/src/pages/**/reports**
frontend/pems-react/src/routes/**
```

Search thêm:

```text
Thống kê hiệu suất phòng ban
Thanh toán khắc phục
Hiệu suất nhân sự phòng ban
Phân bổ mảng việc
reportsApi
useReports
/dashboard/reports
```

---

## 4. Backend API cần có

### 4.1. Department Leader report overview

Tạo/sửa endpoint:

```http
GET /api/reports/department-leader-overview
```

Query params:

```text
fromDate?: string
toDate?: string
preset?: THIS_MONTH | THIS_QUARTER | THIS_YEAR | CUSTOM
logisticsStatus?: ALL | REQUESTED | CHANGE_PROPOSED | ASSIGNED | ACCEPTED | IN_PROGRESS | DONE | REJECTED | DECLINED | CANCELLED
itemType?: ALL | ROOM | TRANSPORT | MEAL | EQUIPMENT | BANNER | LED | OTHER
priority?: ALL | LOW | MEDIUM | HIGH | URGENT
assignedUserId?: number | ALL
dueStatus?: ALL | DUE_SOON | OVERDUE
handoverStatus?: ALL | COMPLETE | MISSING_SIGNATURE | DAMAGED | MISSING
feedbackRating?: ALL | LOW | HIGH
```

### 4.2. Export report

```http
POST /api/reports/department-leader-overview/export
```

Body dùng cùng filter, thêm:

```text
exportFormat: PDF | EXCEL | CSV
reportSections: string[]
```

### 4.3. Export invoice PDF

Tạo endpoint:

```http
POST /api/reports/department-leader-invoice/export-pdf
```

Body gợi ý:

```json
{
  "visitInstanceId": 123,
  "invoiceTitle": "Hóa đơn chuẩn bị hậu cần",
  "invoiceNote": "Chi phí tổng hợp theo số lượng vật phẩm/yêu cầu host gửi phòng ban.",
  "items": [
    {
      "logisticsItemId": 1,
      "itemName": "Nước suối",
      "itemType": "MEAL",
      "quantity": 50,
      "unit": "chai",
      "unitPrice": 8000,
      "note": "Chuẩn bị cho phòng họp"
    }
  ]
}
```

Lưu ý:

```text
quantity phải lấy mặc định từ số lượng host/request yêu cầu trong DB.
unitPrice do Department Leader nhập trên UI.
Thành tiền = quantity * unitPrice.
Tổng tiền = sum thành tiền.
Không lưu hóa đơn vào DB nếu chưa có bảng/requirement lưu invoice.
Trước mắt chỉ generate PDF và download.
Nếu muốn lưu file metadata vào bảng files thì chỉ làm nếu project đã có service chuẩn và được phép.
```

---

## 5. Authorization/scope

Backend bắt buộc:

```text
Chỉ role_code = DEPARTMENT và sub_role = LEADER được gọi Department Leader report.
Scope dữ liệu = currentUser.department_id.
Nếu không đăng nhập → 401.
Nếu không phải Department Leader → 403.
Nếu truyền departmentId khác → ignore hoặc 403.
```

Department Leader được xem:

```text
visit_logistics_items.requested_to_department_id = currentUser.department_id
hoặc task/logistics liên quan department hiện tại theo schema/code hiện có.
assignment attempts thuộc department này.
handover thuộc logistics item của department này.
feedback liên quan department/logistics item của department này.
```

Không được xem:

```text
Toàn hệ thống như HO.
Campus khác nếu không thuộc department/campus của mình.
Task/logistics của department khác.
```

---

## 6. DTO response gợi ý

Tạo DTO:

```text
DepartmentLeaderReportOverviewDto
```

Các phần:

```text
generatedAt
filterSummary
kpis
attentionItems
taskStatusPipeline
workTypeDistribution
staffPerformance
pendingTasks
proposalChanges
handoverSummary
incidentSummary
feedbackSummary
```

### KPI chính

```text
newRequests
waitingAssignment
waitingStaffResponse
inProgress
completed
declined
overdue
missingHandoverSignature
averageResponseHours
averageFeedbackRating
```

Nếu cần gọn trên UI, hiển thị 6 KPI:

```text
Yêu cầu mới
Chờ phản hồi
Đang xử lý
Hoàn thành
Quá hạn
Thiếu ký
```

### Attention items

```text
unassignedRequests
pendingStaffResponseOver24h
overdueTasks
changeProposalsWaiting
missingBorrowReturnSignature
damagedOrMissingItems
lowFeedbackCount
```

### Task status pipeline

```text
REQUESTED
ASSIGNED
ACCEPTED
IN_PROGRESS
CHANGE_PROPOSED
DONE
REJECTED
DECLINED
CANCELLED
```

### Work type distribution

```text
itemType
labelVi
count
quantityTotal
percentage
```

### Staff performance

```text
userId
fullName
assignedCount
pendingResponseCount
acceptedCount
inProgressCount
completedCount
declinedCount
overdueCount
completionRate
averageResponseHours
```

### Pending tasks

```text
logisticsItemId
visitInstanceId
requestCode
delegationName
itemName
itemType
quantity
unit
priority
status
dueAt
assignedToName
waitingHours
actionLabel
detailUrl
```

### Proposal changes

```text
logisticsItemId
itemName
proposedByName
proposedQuantity
proposedUsageStartAt
proposedUsageEndAt
proposalNote
proposalStatus
createdAt
```

### Handover summary

```text
logisticsItemId
itemName
handoverType: BORROW | RETURN
borrowerSigned
providerSigned
itemCondition
conditionNote
attachmentFileId
statusLabel
```

### Incident summary

```text
itemType
itemName
totalQuantity
damagedCount
missingCount
needActionCount
latestNote
```

### Feedback summary

```text
averageRating
totalFeedbacks
lowFeedbackCount
feedbackByItemType[]
lowRatedItems[]
recentFeedbacks[]
```

---

## 7. Query logic gợi ý

Dùng data thật từ:

```text
visit_logistics_items
visit_logistics_assignment_attempts
visit_logistics_item_handovers
visit_request_campuses
visit_requests
users
departments
feedbacks
feedback_rating_items
files
```

Logic chính:

```text
Department scope:
visit_logistics_items.requested_to_department_id = currentUser.department_id

newRequests:
status = REQUESTED

waitingAssignment:
status = REQUESTED và chưa có assigned_to_user_id nếu schema có

waitingStaffResponse:
assignment_attempts.status = PENDING hoặc logistics status = ASSIGNED

inProgress:
status = IN_PROGRESS

completed:
status = DONE

overdue:
due_at < NOW() và status NOT IN ('DONE','REJECTED','DECLINED','CANCELLED')

missingHandoverSignature:
handover borrower_signed_at IS NULL OR provider_signed_at IS NULL

damaged/missing:
handover item_condition = DAMAGED hoặc MISSING nếu enum hiện có
```

Cẩn thận:

```text
Không load toàn bộ DB lên memory.
Dùng AsNoTracking.
Aggregate ở DB.
Tránh N+1 query.
Không tự bịa enum/field.
Nếu field name khác, đọc entity/schema hiện tại trước khi code.
```

---

## 8. UI/UX thiết kế lại

### 8.1. Nguyên tắc

```text
Enterprise dashboard
Gọn
Ít khung/ô
Không card lồng card
Không chart quá cao
Không khoảng trắng lớn
Table compact
Badge nhỏ, rõ nghĩa
Màu chính #004c91
Màu nhấn #F37021
Không tràn ngang page
```

### 8.2. Header

```text
Breadcrumb: Dashboard / Thống kê phòng ban
Title: Báo cáo hiệu suất phòng ban
Subtitle: Tổng quan công việc, nhân sự, bàn giao và phát sinh của phòng ban
Badge: Department Leader · [Tên phòng ban]
```

Bên phải:

```text
Filter nhanh
Xuất báo cáo
Xuất hóa đơn PDF
```

### 8.3. Filter bar

Hiển thị rõ:

```text
[ Khoảng thời gian ] [ Trạng thái ] [ Mảng việc ] [ Nhân sự ] [ Deadline ] [ Bàn giao ] [ Áp dụng ] [ Reset ]
```

Không cho chọn department khác. Chỉ hiển thị read-only:

```text
Phòng ban: Đào tạo HN
```

### 8.4. KPI strip

Thay 4 card lớn bằng 6 KPI nhỏ:

```text
Yêu cầu mới
Chờ phản hồi
Đang xử lý
Hoàn thành
Quá hạn
Thiếu ký
```

Thiết kế:

```text
Một section trắng mỏng.
KPI ngăn bằng divider nhẹ.
Không shadow lớn.
Không padding quá cao.
```

### 8.5. Khối “Cần xử lý ngay”

Đặt ngay dưới KPI:

```text
5 yêu cầu chưa phân công
3 nhiệm vụ chờ nhân sự phản hồi quá 24h
2 nhiệm vụ quá hạn
4 item thiếu chữ ký bàn giao
1 đề xuất thay đổi đang chờ xử lý
```

Mỗi item có nút:

```text
Xem
```

Click để filter bảng tương ứng.

### 8.6. Tabs để tránh trang quá dài

Dùng tabs:

```text
Tổng quan
Công việc
Nhân sự
Bàn giao
Phát sinh & Feedback
Hóa đơn
```

#### Tab Tổng quan

```text
Task status pipeline
Phân bổ mảng việc
Xu hướng hoàn thành theo tháng
```

#### Tab Công việc

```text
Bảng nhiệm vụ chờ phân công/chờ phản hồi/đang xử lý/quá hạn
Bảng đề xuất thay đổi
```

#### Tab Nhân sự

```text
Hiệu suất nhân sự
Tỷ lệ hoàn thành
Từ chối
Quá hạn
Thời gian phản hồi TB
```

#### Tab Bàn giao

```text
BORROW/RETURN
Thiếu chữ ký
Tình trạng đồ
File biên bản/ảnh nếu có
```

#### Tab Phát sinh & Feedback

```text
Hư hỏng/thiếu/cần xử lý
Feedback thấp
Feedback theo mảng việc
```

#### Tab Hóa đơn

```text
Chọn visit/chuyến
Danh sách item host yêu cầu phòng ban chuẩn bị
Nhập đơn giá
Tính thành tiền
Xuất hóa đơn PDF
```

---

## 9. Bảng cần có

### 9.1. Bảng công việc cần xử lý

Columns:

```text
Ưu tiên
Tên nhiệm vụ
Đoàn/Visit
Mảng việc
Số lượng
Deadline
Trạng thái
Người xử lý
Hành động
```

Action:

```text
Phân công
Xem chi tiết
Nhắc phản hồi
```

Không thêm “chuyển nhiệm vụ” nếu business rule hiện tại không cho transfer.

### 9.2. Bảng hiệu suất nhân sự

Columns:

```text
Nhân sự
Được giao
Chờ phản hồi
Đã nhận
Đang xử lý
Hoàn thành
Từ chối
Quá hạn
Tỷ lệ hoàn thành
Phản hồi TB
```

Dùng progress bar nhỏ cho completion rate.

### 9.3. Bảng bàn giao

Columns:

```text
Item
Visit
Loại bàn giao
Bên mượn/trả ký
Bên giao/nhận ký
Tình trạng đồ
Ghi chú
File
Trạng thái
```

Badge:

```text
Đủ chữ ký
Thiếu bên mượn ký
Thiếu bên giao ký
Có hư hỏng
Thiếu/mất
```

### 9.4. Bảng phát sinh

Đổi tên “Thanh toán khắc phục” thành:

```text
Phát sinh sau bàn giao
```

Columns:

```text
Mảng việc
Số item
Tổng số lượng
Hư hỏng
Thiếu/mất
Cần xử lý
Ghi chú mới nhất
```

---

## 10. Chức năng xuất hóa đơn PDF

### 10.1. Mục tiêu

Department Leader có thể xuất hóa đơn PDF cho các đồ/nguồn lực mà host yêu cầu phòng ban chuẩn bị.

Cách tính:

```text
Số lượng = quantity host/request yêu cầu trong visit_logistics_items
Đơn giá = Department Leader nhập trên UI
Thành tiền = Số lượng * Đơn giá
Tổng tiền = tổng thành tiền các dòng
```

Trước mắt:

```text
Chỉ xuất PDF.
Không cần lưu hóa đơn vào DB nếu chưa có schema.
Không tạo bảng invoice mới.
```

### 10.2. UI tab Hóa đơn

Flow:

```text
1. Department Leader mở tab Hóa đơn.
2. Chọn visit/chuyến trong scope department.
3. Hệ thống load danh sách logistics item host yêu cầu phòng ban chuẩn bị.
4. Mỗi dòng hiển thị item name, type, quantity, unit.
5. Department Leader nhập đơn giá cho từng dòng.
6. UI tự tính thành tiền từng dòng và tổng tiền.
7. Department Leader nhập ghi chú hóa đơn nếu cần.
8. Bấm “Xuất hóa đơn PDF”.
9. Backend generate PDF và download.
```

### 10.3. Invoice item table

Columns:

```text
STT
Tên hạng mục
Loại
Số lượng
Đơn vị
Đơn giá
Thành tiền
Ghi chú
```

Validation:

```text
Đơn giá bắt buộc nếu item được chọn xuất hóa đơn.
Đơn giá >= 0.
Số lượng lấy từ DB, không cho sửa nếu không có yêu cầu.
Cho phép bỏ chọn item khỏi hóa đơn.
Tổng tiền cập nhật realtime.
```

### 10.4. Thiết kế PDF hóa đơn

PDF phải chuyên nghiệp.

Header:

```text
FPT University / PEMS
HÓA ĐƠN CHUẨN BỊ HẬU CẦN
Mã hóa đơn tạm: PEMS-INV-YYYYMMDD-HHmm
Ngày xuất
Người xuất
Phòng ban
Campus
```

Thông tin chuyến:

```text
Tên đoàn
Mã visit/request
Ngày thăm
Host yêu cầu
Phòng ban chuẩn bị
```

Bảng chi tiết:

```text
STT
Hạng mục
Loại
Số lượng
Đơn vị
Đơn giá
Thành tiền
Ghi chú
```

Footer:

```text
Tổng cộng
Ghi chú
Chữ ký người lập
Chữ ký xác nhận phòng ban
Chữ ký xác nhận host / IC nếu cần
```

Style:

```text
Màu chủ đạo #004c91
Accent #F37021 dùng rất ít
Font dễ đọc
Header rõ
Bảng có border nhẹ
Tổng tiền nổi bật
Không dùng layout màu mè
Có ngày giờ xuất
Có dòng “Generated by PEMS”
```

Tên file:

```text
PEMS_Department_Invoice_[RequestCode]_[YYYYMMDD_HHmm].pdf
```

### 10.5. PDF implementation

```text
Kiểm tra project đã có thư viện PDF chưa.
Nếu đã có service export PDF, tái sử dụng.
Nếu chưa có thư viện, có thể dùng HTML-to-PDF nếu project đã có.
Không tự thêm thư viện mới quá nặng nếu chưa được phép.
Nếu bắt buộc cần thêm thư viện, báo rõ package và lý do.
```

---

## 11. Export report thường

Ngoài invoice, vẫn giữ export report:

```text
Xuất tổng quan phòng ban
Xuất danh sách công việc
Xuất hiệu suất nhân sự
Xuất bàn giao/ký nhận
Xuất phát sinh & feedback
```

Tên file:

```text
PEMS_DepartmentLeader_Report_YYYYMMDD_HHmm
```

Export dùng đúng filter hiện tại.

---

## 12. Loading/empty/error

Loading:

```text
Skeleton KPI
Skeleton chart
Skeleton table
```

Empty:

```text
Không có dữ liệu trong khoảng thời gian đã chọn.
Không có công việc cần xử lý.
Không có item để xuất hóa đơn.
```

Error:

```text
Không thể tải báo cáo. Vui lòng thử lại.
```

403:

```text
Bạn không có quyền xem báo cáo Department Leader.
```

Invoice error:

```text
Không thể xuất hóa đơn PDF. Vui lòng kiểm tra đơn giá và thử lại.
```

---

## 13. Performance

Backend:

```text
AsNoTracking
Aggregate tại DB
Limit top 10 cho dashboard preview
Không N+1 query
Không load toàn bộ DB vào memory
Có cancellationToken
```

Frontend:

```text
Không refetch liên tục khi chưa bấm Áp dụng
Không render bảng quá dài
Có pagination/xem thêm
Export PDF có loading
Chặn double click export
```

---

## 14. Acceptance criteria

Sau khi hoàn thành:

```text
1. Role Department Leader vào /dashboard/reports thấy dashboard mới.
2. UI gọn hơn, ít khung/ô hơn.
3. Có tabs: Tổng quan, Công việc, Nhân sự, Bàn giao, Phát sinh & Feedback, Hóa đơn.
4. Không còn mock/hard-code data.
5. Dữ liệu chỉ thuộc department của Department Leader.
6. KPI/chart/table lấy từ API thật.
7. Có khối “Cần xử lý ngay”.
8. Có bảng công việc cần xử lý.
9. Có bảng hiệu suất nhân sự đầy đủ hơn.
10. Có bảng bàn giao/ký mượn/ký trả.
11. Đổi “Thanh toán khắc phục” thành “Phát sinh sau bàn giao”.
12. Có tab Hóa đơn.
13. Tab Hóa đơn load item host yêu cầu phòng ban chuẩn bị từ DB thật.
14. Department Leader nhập đơn giá, UI tính thành tiền và tổng tiền.
15. Xuất hóa đơn PDF thành công.
16. PDF hóa đơn có thiết kế chuyên nghiệp.
17. Export report dùng đúng filter.
18. Không phải Department Leader gọi API bị 403.
19. Không tràn ngang page.
20. Backend build pass.
21. Frontend build pass.
22. Báo cáo kết quả liệt kê file sửa, API sửa, cách test, build result.
```

---

## 15. Lệnh build/test

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

Nếu có:

```bash
npm run lint
npm run typecheck
```

Nếu không chạy được vì thiếu DB/secret/env, phải nói rõ lý do, không báo hoàn thành 100%.

---

## 16. Lưu ý cuối

```text
1. Đây là report phòng ban, không phải report HO hoặc Staff Leader.
2. Tập trung vào task/logistics của department hiện tại.
3. Không xem dữ liệu department khác.
4. Không dùng mock data.
5. Không tự thêm bảng invoice.
6. Trước mắt invoice chỉ generate PDF/download.
7. Số lượng trong hóa đơn lấy từ yêu cầu host/phòng ban trong DB.
8. Đơn giá do Department Leader nhập.
9. Tổng tiền tính chính xác, format tiền Việt Nam rõ ràng.
10. Không thêm chức năng thanh toán thật nếu chưa được yêu cầu.
11. Không thêm transfer task nếu rule hiện tại không cho phép.
12. Không dùng dynamic permissions.
13. Không tự sửa schema.
14. Ưu tiên dữ liệu đúng, scope đúng, UI gọn, invoice chuyên nghiệp.
```
