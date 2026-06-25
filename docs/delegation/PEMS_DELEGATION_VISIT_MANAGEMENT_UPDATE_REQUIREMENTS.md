# PEMS — Yêu cầu cập nhật Delegation/Visit Management: Notification, Status, Filter, Approve & Assign Host, Staff Leader Detail

> File này dùng để đưa cho AI Agent đọc và code cập nhật module **Quản lý tiếp khách / Delegation / Visit Management** theo các vấn đề đã phát hiện trên UI hiện tại.  
> Mục tiêu là sửa đúng nghiệp vụ, dùng dữ liệu thật từ database, không tạo mock data, không tạo file rác, không đổi schema nếu không thật sự cần.

---

## 1. Bối cảnh lỗi hiện tại

Trên màn **Quản lý tiếp khách** và modal **Duyệt đơn & chọn host** hiện có các vấn đề:

1. Các thao tác như **hủy**, **duyệt**, **từ chối**, **gán host**, **duyệt & gán host** chưa có thông báo thành công/thất bại rõ ràng.
2. Phần **xem lý do hủy** chưa được nhấn mạnh bằng viền đỏ nhẹ giống phần **xem lý do từ chối**.
3. UI đang hiển thị trạng thái kiểu ghép như **“Đã duyệt · Đã phân công Host”**, gây rối vì trộn `visit_requests.status` với `visit_request_campuses.status`.
4. Visitor hiện chưa có filter theo loại đơn:
   - Đơn một cơ sở.
   - Đơn liên cơ sở.
5. Modal **Duyệt đơn & chọn host** đang báo lỗi chung:  
   **“Không thể gán host. Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.”**
6. Staff Leader sau khi duyệt hiện chưa xem được chi tiết host đang chuẩn bị tới đâu. Cần xác định và triển khai đúng rule: Staff Leader được theo dõi instance thuộc campus mình, không phải bị cấm xem.

---

## 2. Tài liệu/source bắt buộc phải đọc trước khi code

Trước khi sửa code, AI Agent phải đọc và đối chiếu:

```text
docs hoặc root project:
- PROJECT_STRUCTURE_FULL.md
- DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
- PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
- PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
- VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
- PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_v10_FULL_UPDATED.md
- PEMS_UI_DESIGN_SYSTEM_PROMPT.md
```

Nếu code hiện tại mâu thuẫn với các tài liệu trên, ưu tiên theo thứ tự:

```text
1. SQL/database schema v10 mới nhất.
2. Canonical business rules v10.
3. UC implementation rulebook v10.
4. Visitor/delegation management rules.
5. Code backend/frontend hiện tại.
```

---

## 3. Nguyên tắc bắt buộc khi sửa

Không được:

```text
- Không dùng mock data.
- Không tạo file rác.
- Không tự bịa field, bảng, enum, status, route hoặc role.
- Không dùng dynamic permissions hoặc permissions/role_permissions.
- Không đổi role/subRole canonical.
- Không rewrite toàn bộ màn nếu chỉ cần sửa đúng phần lỗi.
- Không thêm thư viện toast mới nếu project đã có toast/alert component sẵn.
- Không báo hoàn thành nếu backend/frontend build lỗi.
```

Bắt buộc:

```text
- Dùng data thật từ database.
- Backend vẫn là lớp check quyền/scope cuối cùng.
- Frontend chỉ ẩn/hiện action và tránh gọi API sai quyền, không thay thế backend authorization.
- Mọi lỗi nghiệp vụ phải trả mã lỗi rõ nghĩa như 400/403/404/409/422 thay vì 500 chung.
- Frontend phải hiển thị message/errors thật từ backend nếu backend có trả.
- Sau mutation thành công phải refetch list/detail liên quan.
```

---

## 4. Yêu cầu cập nhật chi tiết

---

### 4.1. Bổ sung thông báo thành công/thất bại cho các action

Áp dụng cho các action hiện có trên màn quản lý tiếp khách/list/detail/modal:

```text
- Approve request.
- Reject request.
- Cancel request/delegation.
- Assign host.
- Approve & assign host.
- Close delegation nếu màn hiện tại có.
- Update logistics/status nếu màn hiện tại có.
```

#### Yêu cầu frontend

Khi thành công:

```text
- Hiển thị thông báo thành công rõ ràng.
- Đóng modal nếu action hoàn tất.
- Reset form nếu phù hợp.
- Refetch lại list/detail/statistics nếu có.
```

Ví dụ message:

```text
- Duyệt đơn thành công.
- Từ chối đơn thành công.
- Hủy đơn thành công.
- Gán host thành công.
- Duyệt đơn và gán host thành công.
```

Khi thất bại:

```text
- Không đóng modal.
- Không xóa dữ liệu user đã nhập.
- Ưu tiên hiển thị message/errors thật từ API.
- Nếu API không trả message rõ ràng thì mới fallback: “Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.”
```

#### Yêu cầu backend

```text
- Lỗi validation input trả 400/422.
- Không có quyền trả 403.
- Không tìm thấy bản ghi trả 404.
- Sai trạng thái nghiệp vụ hoặc conflict trả 409.
- Chỉ trả 500 cho lỗi hệ thống thật.
- Không nuốt lỗi nghiệp vụ thành 500 chung.
```

---

### 4.2. Sửa UI phần xem lý do hủy

Hiện tại phần lý do hủy chưa nổi bật. Cần style giống phần xem lý do từ chối.

#### Yêu cầu UI

Box lý do hủy dùng style nhẹ, enterprise, không quá gắt:

```text
- Background: red-50 hoặc màu đỏ rất nhạt.
- Border: red-200.
- Text chính: red-700 hoặc slate-700 kết hợp icon đỏ.
- Có icon cảnh báo nhẹ nếu project đã dùng Lucide AlertCircle.
- Có title rõ: “Lý do hủy” hoặc “Thông tin hủy”.
```

#### Nội dung nên hiển thị nếu có dữ liệu

```text
- Lý do hủy.
- Người hủy nếu backend có trả.
- Thời điểm hủy nếu backend có trả.
- Nguồn hủy nếu backend có trả: SELF_SERVICE / EXTERNAL_CONFIRMATION / INTERNAL_OPERATION...
```

Không bịa dữ liệu nếu API chưa trả field. Nếu thiếu field cần thiết, cập nhật DTO/API đúng theo schema hiện có.

---

### 4.3. Chuẩn hóa trạng thái hiển thị và filter

Hiện tại không được hiển thị trạng thái kiểu:

```text
Đã duyệt · Đã phân công Host
```

Vì đây là kiểu ghép gây nhầm giữa:

```text
visit_requests.status              -> trạng thái quyết định tổng của request
visit_request_campuses.status      -> trạng thái vận hành của từng campus instance
```

#### Quy tắc hiển thị mới

Trong màn vận hành theo campus/role, ưu tiên hiển thị `visit_request_campuses.status`.

`visit_requests.status` chỉ dùng để biểu diễn quyết định tổng:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

`visit_request_campuses.status` dùng cho tiến độ vận hành:

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

#### Label đề xuất cho Staff Leader

```text
WAITING_REQUEST_APPROVAL  -> Chờ duyệt
WAITING_HOST_ASSIGNMENT   -> Chờ gán host
ASSIGNED                  -> Đã phân công host
BEFORE_VISIT              -> Trước tiếp khách / Đang chuẩn bị
DURING_VISIT              -> Trong tiếp khách
AFTER_VISIT               -> Sau tiếp khách / Chờ hoàn tất
CLOSED                    -> Đã đóng đoàn
CANCELLED                 -> Đã hủy
```

#### Label đề xuất cho Host/IC Staff

```text
ASSIGNED                  -> Mới được giao
BEFORE_VISIT              -> Đang chuẩn bị
DURING_VISIT              -> Trong tiếp khách
AFTER_VISIT               -> Sau tiếp khách / Chờ hoàn tất
CLOSED                    -> Đã đóng đoàn
CANCELLED                 -> Đã hủy
```

#### Label đề xuất cho Visitor

Visitor nên thấy ngôn ngữ dễ hiểu hơn, không quá nội bộ:

```text
PENDING_APPROVAL          -> Chờ duyệt
APPROVED                  -> Đã được duyệt
REJECTED                  -> Đã bị từ chối
CANCELLED                 -> Đã hủy

WAITING_HOST_ASSIGNMENT   -> Đang sắp xếp người phụ trách
ASSIGNED                  -> Đã phân công người phụ trách
BEFORE_VISIT              -> Sắp diễn ra
DURING_VISIT              -> Đang diễn ra
AFTER_VISIT               -> Đã diễn ra
CLOSED                    -> Đã hoàn tất
```

#### Yêu cầu filter

```text
- Filter status phải dùng enum thống nhất backend/frontend.
- Không hard-code text rời rạc ở nhiều nơi.
- Nếu đã có constants/status map thì cập nhật map đó.
- Nếu chưa có, tạo helper/map tập trung trong module hiện có, không tạo file rác.
```

---

### 4.4. Thêm filter đơn một cơ sở / liên cơ sở cho Visitor

Visitor list cần có filter theo `visitScope`:

```text
ALL
SINGLE_CAMPUS
MULTI_CAMPUS
```

UI label:

```text
Tất cả
Một cơ sở
Liên cơ sở
```

#### Backend rule bắt buộc

Visitor chỉ được xem request của chính mình:

```text
currentUser.role_code = VISITOR
visit_requests.visitor_user_id = currentUser.user_id
```

Thêm filter scope không được làm lộ dữ liệu của Visitor khác.

#### Frontend rule

```text
- Thêm dropdown hoặc segmented filter phù hợp UI hiện tại.
- Khi đổi filter, gọi API với param đúng tên hiện có hoặc tên mới thống nhất với backend.
- Không phá các filter hiện có như keyword/status/date.
- Reset filter phải đưa visitScope về ALL.
```

#### Backend query gợi ý

Nếu API list hiện đã có criteria object, thêm:

```text
visitScope?: ALL | SINGLE_CAMPUS | MULTI_CAMPUS
```

Áp dụng filter:

```sql
AND (@VisitScope IS NULL OR @VisitScope = 'ALL' OR vr.visit_scope = @VisitScope)
```

Nhưng vẫn phải có ownership predicate trước/sau:

```sql
AND vr.visitor_user_id = @CurrentUserId
```

---

### 4.5. Fix lỗi “Duyệt đơn & chọn host”

Hiện UI đã load được host candidates và hiển thị “Không trùng lịch”, nhưng khi submit lại báo:

```text
Không thể gán host. Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.
```

Cần debug tận gốc, không chỉ sửa message ngoài frontend.

#### Các bước bắt buộc

```text
1. Reproduce bằng tài khoản Staff Leader.
2. Mở Network tab kiểm tra request payload.
3. Kiểm tra response body/status code.
4. Kiểm tra backend log/exception stack trace.
5. Xác định lỗi nằm ở:
   - payload sai field,
   - route sai,
   - status transition sai,
   - handler chưa support approve + assign cùng lúc,
   - current_host_user_id conflict,
   - insert visit_participants conflict,
   - calendar conflict check sai,
   - scope check sai,
   - transaction/save lỗi,
   - hoặc frontend parse response sai.
```

#### Rule chọn host bắt buộc

Host candidate hợp lệ phải thỏa:

```text
- users.status = ACTIVE
- roles.role_code = STAFF
- users.sub_role = STAFF
- users.primary_campus_id = visit_request_campuses.campus_id
- departments.department_type = IC
- departments.status = ACTIVE
- Không phải Staff Leader hiện tại
- Không trùng lịch theo rule overlap đã chốt
```

Không được chọn:

```text
- Staff Leader làm host mặc định.
- Department user làm host chính.
- User INACTIVE/LOCKED.
- User khác campus.
- User thuộc department inactive.
```

#### Transition cần support

Nếu Staff Leader duyệt và chọn host cùng lúc:

```text
WAITING_REQUEST_APPROVAL -> ASSIGNED
```

Nếu Staff Leader duyệt trước, gán host sau:

```text
WAITING_REQUEST_APPROVAL -> WAITING_HOST_ASSIGNMENT -> ASSIGNED
```

Nếu request bị từ chối/hủy/đóng:

```text
Không cho gán host.
Trả 409 Conflict với message rõ ràng.
```

#### Backend response yêu cầu

Không trả 500 cho lỗi nghiệp vụ. Ví dụ:

```text
409: Đơn này không còn ở trạng thái có thể gán host.
409: Host đã được phân công cho đơn này.
409: Nhân sự được chọn không còn khả dụng trong khung giờ này.
403: Bạn không có quyền gán host cho cơ sở này.
404: Không tìm thấy campus instance.
422: HostId không hợp lệ.
```

#### Frontend yêu cầu

```text
- Submit button có loading state.
- Disable double click khi đang submit.
- Hiển thị lỗi cụ thể từ backend.
- Không đóng modal khi lỗi.
- Giữ host đã chọn khi lỗi để user thử lại.
```

---

### 4.6. Staff Leader xem chi tiết tiến độ host chuẩn bị

Staff Leader không bị cấm xem chi tiết sau khi duyệt. Cần triển khai hoặc sửa guard nếu đang chặn sai.

#### Rule visibility

Staff Leader được xem:

```text
- SINGLE_CAMPUS thuộc campus của mình.
- MULTI_CAMPUS instance thuộc campus của mình sau khi HO approve/release.
```

Staff Leader không được xem:

```text
- Instance thuộc campus khác.
- Multi-campus pending HO approval nếu chưa được release về campus.
- Dữ liệu không liên quan tới campus mình.
```

#### UI yêu cầu

Trên list, Staff Leader phải có nút xem chi tiết cho các instance hợp lệ.

Detail nên hiển thị read-only các phần:

```text
- Thông tin request/delegation tổng quan.
- Campus instance hiện tại.
- Host hiện tại.
- Trạng thái vận hành hiện tại.
- Agenda/lịch trình.
- Logistics/resource items.
- Department tasks/support nếu có.
- Participants/students/support staff nếu có.
- Timeline/audit/status history nếu API có trả.
- Lý do từ chối/hủy nếu có.
```

#### Action rule

Staff Leader chỉ được thao tác những action đúng scope và đúng status:

```text
- Duyệt/từ chối khi đang chờ duyệt và thuộc campus mình.
- Gán host khi đúng trạng thái.
- Theo dõi tiến độ host chuẩn bị.
```

Staff Leader không được tùy tiện làm thay Host nếu action đó thuộc Host/IC Staff.

---

## 5. Backend implementation checklist

Kiểm tra/cập nhật các phần tương ứng trong backend:

```text
- DelegationsController / VisitRequestsController route liên quan.
- Query list delegation/visit request theo role.
- Query detail delegation/visit request theo role.
- Approve command.
- Reject command.
- Cancel command.
- AssignHost command.
- ApproveAndAssignHost command nếu có.
- GetHostCandidates query.
- DTO response cho list/detail.
- Validator cho request body/query param.
- Fixed role policy/scope helper.
- Exception mapping middleware.
```

Backend phải đảm bảo:

```text
- Controller chỉ gọi MediatR.
- Handler chứa business validation.
- FluentValidation xử lý input validation.
- Không query DbContext trực tiếp trong Controller.
- Có transaction cho mutation.
- Có audit log cho approve/reject/cancel/assign host.
```

---

## 6. Frontend implementation checklist

Kiểm tra/cập nhật các phần tương ứng trong frontend:

```text
- VisitRequestManagement.tsx
- VisitProcess.tsx
- Delegation/Visit API service file.
- Delegation/Visit TypeScript types.
- Status badge component/helper nếu có.
- Filter bar component nếu có.
- Modal approve/reject/cancel/assign host.
- Detail route/view guard.
```

Frontend phải đảm bảo:

```text
- Có success/failure notification.
- Không hard-code status label phân tán.
- Filter visitor có visitScope.
- Button action đúng role/status.
- Detail Staff Leader không bị guard sai.
- Modal giữ dữ liệu khi submit lỗi.
- Loading/empty/error state rõ ràng.
- Không làm vỡ responsive layout.
```

---

## 7. Acceptance Criteria

### AC-01 — Action success notification

```text
Given Staff Leader thực hiện approve/reject/cancel/assign host hợp lệ
When API trả success
Then UI hiển thị thông báo thành công
And modal đóng nếu action hoàn tất
And list/detail được refetch
```

### AC-02 — Action failure notification

```text
Given Staff Leader submit action nhưng backend trả lỗi nghiệp vụ
When API trả 400/403/404/409/422
Then UI hiển thị đúng message từ backend
And modal không đóng
And dữ liệu đã nhập/host đã chọn vẫn được giữ
```

### AC-03 — Cancellation reason UI

```text
Given một đơn đã hủy có cancellation_reason
When user mở detail hoặc vùng xem lý do hủy
Then lý do hủy hiển thị trong box nền đỏ nhạt, viền đỏ nhẹ
And dễ phân biệt với thông tin thường
```

### AC-04 — Không còn status ghép gây rối

```text
Given một campus instance có request status APPROVED và instance status ASSIGNED
When user xem list
Then UI không hiển thị “Đã duyệt · Đã phân công Host”
And chỉ hiển thị status vận hành phù hợp như “Đã phân công host”
```

### AC-05 — Visitor filter theo scope

```text
Given Visitor có cả đơn SINGLE_CAMPUS và MULTI_CAMPUS của chính mình
When chọn filter “Một cơ sở”
Then chỉ hiển thị request SINGLE_CAMPUS của chính Visitor đó

When chọn filter “Liên cơ sở”
Then chỉ hiển thị request MULTI_CAMPUS của chính Visitor đó

When chọn “Tất cả”
Then hiển thị cả hai loại của chính Visitor đó
```

### AC-06 — Không lộ dữ liệu Visitor khác

```text
Given có request của Visitor A và Visitor B
When Visitor A gọi list với visitScope bất kỳ
Then API chỉ trả request của Visitor A
And không trả request của Visitor B
```

### AC-07 — Approve & assign host thành công

```text
Given Staff Leader thuộc campus HN
And có campus instance HN ở trạng thái WAITING_REQUEST_APPROVAL
And chọn một IC Staff hợp lệ, không trùng lịch
When Staff Leader bấm “Duyệt & gán host”
Then backend cập nhật instance sang ASSIGNED
And current_host_user_id được set đúng host
And UI hiển thị “Duyệt đơn và gán host thành công”
And list/detail cập nhật trạng thái mới
```

### AC-08 — Approve & assign host lỗi nghiệp vụ

```text
Given instance đã CANCELLED hoặc CLOSED
When Staff Leader cố gán host
Then backend trả 409
And frontend hiển thị lỗi rõ ràng
And không cập nhật host/status
```

### AC-09 — Staff Leader xem detail sau duyệt

```text
Given Staff Leader thuộc campus HN
And có SINGLE_CAMPUS instance HN đã APPROVED/ASSIGNED/BEFORE_VISIT
When Staff Leader bấm xem chi tiết
Then detail mở được
And hiển thị host, agenda, logistics, participants, status/timeline nếu có
And các phần vận hành hiển thị read-only nếu Staff Leader không phải actor thao tác trực tiếp
```

### AC-10 — Staff Leader không xem sai scope

```text
Given Staff Leader HN cố truy cập detail instance thuộc campus HCM
When gọi API trực tiếp bằng ID
Then backend trả 403 hoặc 404 theo policy hiện tại
And frontend không hiển thị dữ liệu
```

---

## 8. Manual test checklist

Chạy test tối thiểu bằng các tài khoản seed thật nếu có:

```text
- staff.leader.hn@fpt.edu.vn
- staff.hn@fpt.edu.vn
- visitor@example.com
```

Checklist:

```text
[ ] Staff Leader login và mở Quản lý tiếp khách.
[ ] Duyệt đơn single-campus hợp lệ.
[ ] Từ chối đơn và kiểm tra toast + reason.
[ ] Hủy đơn và kiểm tra toast + cancellation reason box.
[ ] Mở modal Duyệt đơn & chọn host.
[ ] Search host candidate.
[ ] Chọn host hợp lệ.
[ ] Submit duyệt & gán host.
[ ] Kiểm tra Network response không còn 500 nếu là lỗi nghiệp vụ.
[ ] Kiểm tra list cập nhật status đúng, không còn text ghép.
[ ] Staff Leader mở detail instance sau khi duyệt/gán host.
[ ] Kiểm tra detail hiển thị tiến độ host chuẩn bị.
[ ] Visitor login.
[ ] Visitor lọc Tất cả / Một cơ sở / Liên cơ sở.
[ ] Kiểm tra Visitor không thấy request của người khác.
[ ] Build backend pass.
[ ] Build frontend pass.
```

---

## 9. Output report yêu cầu sau khi code xong

AI Agent phải báo cáo theo format:

```text
1. Summary
2. Files changed
3. Backend changes
4. Frontend changes
5. Status/label/filter changes
6. Approve & assign host root cause
7. Staff Leader detail rule/result
8. Test/build result
9. Remaining risks nếu có
```

Không báo “hoàn thành” nếu:

```text
- Chưa build.
- Chưa test luồng lỗi approve & assign host.
- Chưa xác định root cause lỗi 500/generic error.
- Chỉ sửa frontend message nhưng backend vẫn trả lỗi nghiệp vụ thành 500.
```

---

## 10. Prompt ngắn để chạy AI Agent

```text
Đọc source PEMS hiện tại và cập nhật module Delegation/Visit Management theo file yêu cầu này. Dùng SQL v10/canonical rules làm chuẩn. Không dùng mock data, không tạo file rác, không đổi schema nếu không cần, không thêm thư viện mới nếu project đã có toast/alert.

Cần fix:
1. Thêm success/failure notification cho approve/reject/cancel/assign host/approve & assign host.
2. Sửa box xem lý do hủy có viền đỏ nhẹ giống lý do từ chối.
3. Chuẩn hóa status label/filter, bỏ kiểu “Đã duyệt · Đã phân công Host”.
4. Thêm Visitor filter theo ALL/SINGLE_CAMPUS/MULTI_CAMPUS và vẫn enforce ownership.
5. Debug/fix lỗi modal “Duyệt đơn & chọn host” đang báo lỗi hệ thống.
6. Cho Staff Leader xem detail tiến độ host chuẩn bị đúng campus scope, read-only nếu không có quyền thao tác.

Backend phải trả lỗi nghiệp vụ bằng 400/403/404/409/422 rõ nghĩa, không trả 500 chung. Frontend phải hiển thị message thật từ API và giữ modal/form khi lỗi. Build backend/frontend phải pass và báo cáo root cause + files changed.
```
