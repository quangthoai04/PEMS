# PEMS — HO Campus Management  
## Hoàn thiện UC-86 Manage Campus Status và đồng bộ Campus Operational Availability

> **Mục đích:** Dùng trực tiếp file này làm prompt cho AI Agent đọc source, phân tích hiện trạng và triển khai đầy đủ chức năng enable/disable campus mà không phá vỡ luồng Create Campus hiện có.

---

# 1. Vai trò của AI Agent

Bạn là:

- Senior .NET 8 Clean Architecture Developer
- Senior React TypeScript Engineer
- MySQL Database-First Engineer
- Authorization and Business Rule Reviewer
- QA Engineer cho Unit Test backend và frontend test phù hợp
- Source Alignment Reviewer cho dự án PEMS

Bạn phải đọc source thật trước khi sửa. Không được sửa theo suy đoán, không được tự bịa file, route, DTO, enum, bảng, field hoặc error code.

---

# 2. Bối cảnh hệ thống

PEMS là hệ thống quản lý đối tác và toàn bộ vòng đời đoàn khách/tham quan tại nhiều campus.

Module liên quan:

```text
Campus Management
Visit Request / Delegation Management
Department Management
Account Management
Visitor Registration Form
```

Actor duy nhất được quản lý trạng thái campus:

```text
HO
role_code = HO
account status = ACTIVE
```

Authorization của dự án dùng fixed policy dựa trên role/sub-role/scope. Không dùng dynamic permissions.

Không được tạo lại hoặc query các bảng:

```text
permissions
role_permissions
```

---

# 3. Tài liệu và source bắt buộc phải đọc trước khi code

## 3.1. Đặc tả Campus Management

Đọc toàn bộ:

```text
00_CAMPUS_MANAGEMENT_COMMON_RULES_HO.md
01_UC82_VIEW_CAMPUS_LIST_HO.md
02_UC83_SEARCH_FILTER_CAMPUS_HO.md
03_UC81_CREATE_CAMPUS_HO.md
04_UC84_VIEW_CAMPUS_DETAILS_HO.md
05_UC85_UPDATE_CAMPUS_HO.md
06_UC86_MANAGE_CAMPUS_STATUS_HO.md
```

## 3.2. Nguồn chuẩn dự án

Đọc và đối chiếu:

```text
PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
PROJECT_STRUCTURE_FULL.md
PROJECT_KNOWLEDGE.md
CLEAN_ARCHITECTURE.md
PEMS_UI_DESIGN_SYSTEM_PROMPT.md
```

## 3.3. Database

Đọc SQL fresh-create mới nhất trong source và đối chiếu trực tiếp các bảng:

```text
campuses
departments
users
roles
visit_requests
visit_request_campuses
visit_participants
visit_logistics_items
visit_logistics_item_handovers
visit_agendas
minutes
notifications
audit_logs
audit_log_changes
```

Không chỉ dựa vào file dictionary nếu dictionary và SQL mới nhất lệch nhau.

## 3.4. Source code phải search

Search và đọc tối thiểu:

```text
CampusesController
CreateCampus command/handler/validator/response
ManageCampusStatus command/handler/validator/response
ViewCampusList query/handler/DTO
ViewCampusDetails query/handler/DTO
Campus entity và EF configuration
Department entity và EF configuration
User/Role entity và EffectiveRole resolution
Visitor/Public campus options endpoint
Submit Visit Request command/handler/validator
Existing campus availability helper/service/query
Existing Staff Leader availability helper/service/query
Existing transaction/audit/error handling patterns
Frontend Campus Management page
Frontend Campus Detail page
Frontend Campus API service và types
Frontend Visit Registration form và campus option loading
Existing Unit Tests và frontend tests
```

Nếu tên file/path khác, tự search source. Không được tạo file trùng chức năng khi source đã có helper tương đương.

---

# 4. Quy tắc ưu tiên khi có mâu thuẫn

Ưu tiên theo thứ tự:

```text
1. SQL fresh-create mới nhất
2. Entity/EF configuration đang chạy khớp SQL
3. Business rule đã chốt trong file prompt này
4. PEMS_CANONICAL_BUSINESS_RULES
5. PEMS_UC_IMPLEMENTATION_RULEBOOK
6. Campus Management UC documents
7. PROJECT_KNOWLEDGE và source hiện tại
8. Tài liệu legacy chỉ dùng để đối chiếu
```

Riêng các quyết định nghiệp vụ trong file này là yêu cầu mới đã được chốt, phải được triển khai ngay cả khi UC-86 cũ chưa mô tả đủ.

---

# 5. Mục tiêu triển khai

Hoàn thiện UC-86 để:

1. HO không thể disable một campus khi campus đó còn campus visit instance chưa kết thúc.
2. Không phá vỡ hoặc thay đổi luồng Create Campus hiện tại.
3. Tách rõ:
   - `campuses.status`
   - khả năng campus xuất hiện trên form đăng ký tham quan.
4. Chỉ trả campus cho form đăng ký khi campus đủ điều kiện vận hành.
5. Backend submit phải kiểm tra lại, không tin dữ liệu dropdown frontend.
6. Enable/disable không cascade sửa hoặc xóa dữ liệu lịch sử.
7. UI phải giải thích rõ vì sao campus ACTIVE nhưng chưa sẵn sàng nhận đăng ký.
8. Có Unit Test backend và frontend test thật; không có test giả chỉ để pass.


## 5.1. Phạm vi kiểm thử đã chốt

Task này chỉ triển khai:

```text
Unit Test backend
Frontend test phù hợp với phần UI bị thay đổi
Build, typecheck, lint và Architecture Test nếu thay đổi layer/controller
```

Không triển khai, không sửa và không yêu cầu chạy Integration Test trong task này.

Các tình huống API/DB/race condition vẫn phải được xử lý đúng trong production code và được kiểm chứng ở mức Unit Test bằng cách cô lập business logic, mock dependency có kiểm soát và assert đầy đủ state/output/error. Không được bỏ logic chỉ vì không viết Integration Test.

---

# 6. Quyết định bắt buộc: Giữ nguyên Create Campus

Không thay đổi luồng UC-81.

Khi HO tạo campus thành công:

```text
campuses.status = ACTIVE
campuses.ic_head_user_id = NULL
```

Backend tự tạo IC department trong cùng transaction:

```text
departments.campus_id = campus vừa tạo
departments.department_type = IC
departments.status = ACTIVE
departments.head_user_id = NULL
```

Không thêm field chọn Staff Leader vào Create Campus.

Không đổi campus mới thành `INACTIVE`.

Không bắt buộc có Staff Leader tại thời điểm tạo campus.

Kết quả mong đợi:

```text
Campus vừa tạo:
- ACTIVE trong Campus Management
- Có IC department ACTIVE
- Chưa có Staff Leader
- Chưa xuất hiện trên form đăng ký tham quan
```

Khi sau đó có Staff Leader ACTIVE hợp lệ:

```text
Campus tự động đủ điều kiện xuất hiện trên form.
Không cần HO disable/enable lại.
```

---

# 7. Tách Campus Status và Operational Availability

## 7.1. Campus Status

`campuses.status` chỉ có:

```text
ACTIVE
INACTIVE
```

Ý nghĩa:

| Status | Ý nghĩa |
|---|---|
| `ACTIVE` | Campus đang được phép hoạt động về mặt quản trị. |
| `INACTIVE` | Campus đã bị HO ngừng hoạt động. |

Toggle Campus Management chỉ phản ánh field này.

## 7.2. Operational Availability

Không thêm column/enum mới vào database.

Backend tính động:

```text
isAvailableForVisitRegistration
```

Campus chỉ sẵn sàng nhận đăng ký khi đồng thời thỏa:

```text
campus.status = ACTIVE
AND có đúng IC department ACTIVE hợp lệ
AND có đúng Staff Leader ACTIVE hợp lệ
```

Có thể tồn tại:

```text
campus.status = ACTIVE
isAvailableForVisitRegistration = false
```

Đây là trạng thái hợp lệ, không phải lỗi dữ liệu.

---

# 8. Quy tắc xác định Campus sẵn sàng nhận đăng ký

## 8.1. Campus phải ACTIVE

```text
campuses.status = ACTIVE
```

## 8.2. Phải có IC Department ACTIVE

Department hợp lệ:

```text
departments.campus_id = campuses.campus_id
departments.department_type = IC
departments.status = ACTIVE
```

Nếu không có IC department ACTIVE:

```text
isAvailableForVisitRegistration = false
```

Nếu dữ liệu bất thường có nhiều IC department ACTIVE:

```text
Không chọn ngẫu nhiên.
Coi là lỗi cấu hình.
Campus không được xuất hiện trên form.
Ghi readiness issue phù hợp.
```

Không sửa dữ liệu tổ chức tự động trong query availability.

## 8.3. Phải có Staff Leader ACTIVE hợp lệ

User phải đồng thời thỏa:

```text
roles.role_code = STAFF
users.sub_role = LEADER
users.status = ACTIVE
users.primary_campus_id = campus hiện tại
users.department_id = IC department ACTIVE của campus
```

Nếu source hiện tại còn kiểm tra role status thì giữ rule đó theo pattern hiện có.

Không dùng role code giả:

```text
STAFF_LEADER
STAFF_L
LEADER
```

`STAFF_LEADER` chỉ là effective role, không phải `role_code`.

## 8.4. Số lượng Staff Leader hợp lệ

```text
0 leader hợp lệ:
- campus không xuất hiện trên form

1 leader hợp lệ:
- campus xuất hiện trên form
- leader đó là coordinator/recipient theo flow hiện tại

> 1 leader hợp lệ:
- không dùng FirstOrDefault tùy ý
- coi là lỗi cấu hình
- campus không xuất hiện trên form
- readinessIssues phải phản ánh cấu hình Staff Leader không hợp lệ
```

Nếu source đã có rule/constraint bảo đảm đúng một Staff Leader, vẫn phải bảo vệ ở application layer khi query dữ liệu.

## 8.5. Không chỉ kiểm tra `ic_head_user_id`

Không được coi điều kiện sau là đủ:

```text
campuses.ic_head_user_id IS NOT NULL
```

Phải xác minh user thật theo role, sub-role, status, campus và IC department.

`campuses.ic_head_user_id` và `departments.head_user_id` có thể dùng để:

```text
hiển thị
đối chiếu consistency
ghi readiness warning
```

Nhưng không được là nguồn xác minh duy nhất nếu source có thể lệch mapping.

---

# 9. Reuse một nguồn logic duy nhất cho Availability

Không copy cùng một đoạn query sang nhiều handler.

Tìm helper/service/query hiện có, đặc biệt các class kiểu:

```text
HoCampusAvailability
StaffLeaderAvailability
CampusAvailability
CampusOperationalReadiness
```

Nếu chưa có, tạo một abstraction phù hợp kiến trúc hiện tại để tái sử dụng cho:

```text
Campus list/detail readiness
Visitor campus option endpoint
Submit Visit Request validation
Enable status response
Create Campus response/toast data nếu cần
```

Logic availability phải thống nhất tuyệt đối giữa:

```text
Dropdown hiển thị
Submit validation
Campus Management readiness badge
```

---

# 10. API Campus Options cho form đăng ký

Tìm endpoint hiện đang cấp campus cho Visit Registration Form.

Không dùng:

```text
GET /api/campuses?status=ACTIVE
```

rồi để frontend tự suy luận Staff Leader/IC department.

Backend phải chỉ trả campus thỏa toàn bộ:

```text
ACTIVE campus
AND active IC department
AND exactly one active valid Staff Leader
```

Frontend chỉ render dữ liệu backend trả về.

Không đưa campus chưa ready vào dropdown rồi disable option, trừ khi UX hiện tại đã chốt khác. Mặc định phải loại khỏi selectable options.

---

# 11. Backend Submit Visit Request phải recheck

Ẩn dropdown không phải security boundary.

Khi submit, với từng `campusId` được gửi lên, backend phải kiểm tra lại trong transaction:

```text
1. Campus tồn tại.
2. Campus ACTIVE.
3. Có IC department ACTIVE hợp lệ.
4. Có đúng một Staff Leader ACTIVE hợp lệ.
5. Staff Leader đúng campus và đúng IC department.
```

Không tin dữ liệu frontend hoặc dữ liệu đã load trước đó.

## 11.1. Multi-campus request

Nếu một campus không hợp lệ:

```text
Không tạo partial request.
Không tạo request cho các campus còn lại.
Rollback toàn bộ transaction.
Trả rõ campus nào không còn khả dụng và lý do.
```

## 11.2. Race condition

Ví dụ:

```text
Người dùng load form lúc campus còn available.
HO disable campus trước khi người dùng submit.
Backend phải từ chối submit.
```

Ví dụ:

```text
Staff Leader bị disable sau khi form load.
Backend phải từ chối submit.
```

---

# 12. Quy tắc Disable Campus

## 12.1. Kiểm tra theo campus instance

Dependency chính:

```text
visit_request_campuses.campus_id = campusId
```

Không được chỉ kiểm tra:

```text
visit_requests.status
```

Vì request multi-campus có các campus instance độc lập.

## 12.2. Các status block disable

Hard blocker:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
```

Bảng quyết định:

| `visit_request_campuses.status` | Block disable |
|---|---:|
| `WAITING_REQUEST_APPROVAL` | Có |
| `ASSIGNED` | Có |
| `BEFORE_VISIT` | Có |
| `DURING_VISIT` | Có |
| `AFTER_VISIT` | Có |
| `CLOSED` | Không |
| `CANCELLED` | Không |
| `REJECTED` | Không |

Nếu SQL/source mới nhất còn thêm status khác:

```text
Không tự đoán.
Phân loại dựa trên lifecycle canonical.
Mọi non-terminal operational status phải block.
Mọi terminal status chỉ không block khi business flow đã kết thúc thật.
```

Nếu source có legacy `WAITING_HOST_ASSIGNMENT`, xác định nó còn chạy thật hay chỉ legacy. Nếu còn có thể tồn tại runtime, nó phải block disable.

## 12.3. Không dùng ngày làm nguồn quyết định

Không dùng logic:

```text
planned_end_at < now
```

thay cho status.

Ví dụ:

```text
AFTER_VISIT dù đã quá ngày:
vẫn block.

CANCELLED dù ngày ở tương lai:
không block.
```

Ngày giờ chỉ dùng để hiển thị và sắp xếp blocker.

## 12.4. Không check request aggregate thay cho campus status

Không dựa riêng vào:

```text
visit_requests.status = APPROVED/PENDING/...
```

Một request tổng có thể còn campus khác hoạt động nhưng campus đang disable đã terminal, hoặc ngược lại.

---

# 13. Dependency không phải hard blocker riêng

Không dùng các dependency sau làm hard blocker độc lập nếu campus visit instance đã terminal:

```text
user ACTIVE
department ACTIVE
participant/invitation lịch sử
agenda lịch sử
logistics lịch sử
minutes
feedback
news
notification
reminder
```

Lý do:

```text
UC-86 không được biến thành chức năng cleanup toàn hệ thống.
Tính đóng hoàn chỉnh của child data phải được enforce ở UC Close Visit Instance.
```

Nếu phát hiện parent terminal nhưng child data bất thường:

```text
Có thể trả readiness/data-integrity warning trong preview.
Không được tự sửa child data trong UC-86.
Không mặc định block campus vĩnh viễn chỉ vì record lịch sử.
```

---

# 14. Không cascade khi Disable

Disable Campus tuyệt đối không được:

```text
Tự reject request đang chờ.
Tự cancel visit đã duyệt.
Tự chuyển AFTER_VISIT thành CLOSED.
Tự xóa hoặc thay host.
Tự xóa participant/invitation.
Tự hủy logistics.
Tự sửa agenda/minutes/news/feedback.
Tự disable user.
Tự revoke session hàng loạt.
Tự disable department.
Tự xóa dữ liệu.
```

Mỗi dependency phải được xử lý qua UC riêng.

---

# 15. Trạng thái hệ thống sau Disable

Khi disable thành công:

```text
campuses.status = INACTIVE
campuses.updated_by = current HO user_id
campuses.updated_at = thời gian hiện tại theo time convention của dự án
```

Campus INACTIVE:

```text
Vẫn hiển thị trong Campus Management.
Vẫn xem được Campus Detail.
Vẫn xem được lịch sử visit.
Vẫn giữ users và departments.
Vẫn giữ audit/report/documents.
Không xuất hiện trên Visit Registration Form.
Không được backend chấp nhận trong submit mới.
Không được chọn trong các business flow mới chỉ dành cho active campus.
```

Không xóa cứng campus.

---

# 16. Quy tắc Enable Campus

## 16.1. Điều kiện để `INACTIVE → ACTIVE`

Phải kiểm tra:

```text
campus_code hợp lệ
name hợp lệ
city có giá trị
address có giá trị
phone hợp lệ
email hợp lệ
có IC department ACTIVE hợp lệ
```

Dùng validator/business validation theo convention hiện tại.

## 16.2. Staff Leader không bắt buộc để set ACTIVE

Đây là quyết định bắt buộc để nhất quán với Create Campus.

Cho phép:

```text
Campus INACTIVE
+ master data hợp lệ
+ IC department ACTIVE
+ chưa có Staff Leader
→ enable thành ACTIVE thành công
→ isAvailableForVisitRegistration vẫn false
→ campus chưa xuất hiện trên form
```

Khi Staff Leader ACTIVE hợp lệ được tạo/gán sau đó:

```text
Campus tự động xuất hiện trên form.
Không cần toggle lại.
```

## 16.3. Enable không khôi phục dữ liệu cũ

Không được:

```text
Mở lại request REJECTED.
Khôi phục campus instance CANCELLED.
Kích hoạt user INACTIVE.
Kích hoạt department INACTIVE khác.
Gán lại host.
Gửi lại invitation.
Khôi phục logistics.
```

Enable chỉ thay đổi campus status và audit fields.

---

# 17. Idempotency của status change

Nếu request status bằng current status:

```text
ACTIVE → ACTIVE
INACTIVE → INACTIVE
```

Xử lý theo convention hiện tại, ưu tiên idempotent:

```text
Trả success/no-op.
Không sửa updated_at không cần thiết.
Không tạo audit thay đổi giả.
```

Nếu source hiện có convention conflict cho same-state transition, giữ nhất quán nhưng phải có test rõ.

---

# 18. Status Impact Preview

Triển khai preflight/preview trước khi HO xác nhận disable.

Trước hết search source để reuse endpoint/query hiện có.

Nếu chưa có, thêm query endpoint theo routing convention hiện tại, ví dụ về mặt ý nghĩa:

```text
GET /api/campuses/{campusId}/status-impact?targetStatus=INACTIVE
```

Không bắt buộc giữ đúng path ví dụ nếu project có naming convention khác.

Preview phải trả tối thiểu:

```ts
type CampusStatusImpactDto = {
  campusId: number;
  currentStatus: 'ACTIVE' | 'INACTIVE';
  targetStatus: 'ACTIVE' | 'INACTIVE';
  canChange: boolean;
  blockerCount: number;
  blockersByStatus: Record<string, number>;
  blockerExamples: Array<{
    visitInstanceId: number;
    requestId?: number;
    requestCode?: string;
    delegationName?: string;
    status: string;
    plannedStartAt?: string | null;
    plannedEndAt?: string | null;
  }>;
};
```

Không trả toàn bộ dữ liệu cá nhân của đoàn nếu UI chỉ cần tên/mã/trạng thái.

Giới hạn số example để tránh payload lớn.

Preview chỉ cải thiện UX. PATCH status vẫn phải recheck trong transaction.

---

# 19. PATCH Manage Campus Status

Giữ contract hiện tại nếu source đã có:

```http
PATCH /api/campuses/{campusId}/status
```

Body:

```json
{
  "status": "ACTIVE"
}
```

hoặc:

```json
{
  "status": "INACTIVE"
}
```

## 19.1. Disable flow backend

```text
1. Authentication.
2. Authorization: HO ACTIVE.
3. Validate campusId/status input.
4. Load và lock campus theo transaction strategy hiện tại.
5. Kiểm tra campus tồn tại.
6. Kiểm tra current status.
7. Query blocker theo visit_request_campuses.campus_id.
8. Nếu còn blocker: không update, trả 409.
9. Nếu không còn blocker: set INACTIVE.
10. Set updated_by/updated_at.
11. Audit.
12. Commit.
```

## 19.2. Enable flow backend

```text
1. Authentication.
2. Authorization: HO ACTIVE.
3. Validate campusId/status input.
4. Load/lock campus.
5. Kiểm tra campus tồn tại.
6. Kiểm tra master data.
7. Kiểm tra IC department ACTIVE.
8. Không bắt buộc Staff Leader.
9. Set ACTIVE.
10. Recompute operational readiness cho response.
11. Set updated_by/updated_at.
12. Audit.
13. Commit.
```

---

# 20. Error contract

Reuse error infrastructure hiện tại.

Không thêm mã trùng nếu source đã có mã tương đương.

Các lỗi nghiệp vụ cần phân biệt:

```text
CAMPUS_NOT_FOUND
CAMPUS_INACTIVE
CAMPUS_HAS_ACTIVE_VISITS
CAMPUS_HAS_NO_ACTIVE_IC_DEPARTMENT
CAMPUS_HAS_NO_ACTIVE_STAFF_LEADER
CAMPUS_STAFF_LEADER_CONFIGURATION_INVALID
CAMPUS_NOT_OPERATIONALLY_READY
```

Tên cuối cùng phải theo convention hiện có.

## 20.1. Disable bị blocker

HTTP:

```text
409 Conflict
```

Payload phải có machine-readable error code và blocker summary.

Ví dụ:

```json
{
  "success": false,
  "errorCode": "CAMPUS_HAS_ACTIVE_VISITS",
  "message": "Không thể ngừng hoạt động campus vì còn chuyến thăm chưa hoàn tất.",
  "details": {
    "total": 4,
    "byStatus": {
      "WAITING_REQUEST_APPROVAL": 2,
      "ASSIGNED": 1,
      "AFTER_VISIT": 1
    }
  }
}
```

## 20.2. Enable thiếu IC department/master data

Dùng `409` hoặc convention hiện tại của project, nhưng phải nhất quán và có error code riêng.

## 20.3. Submit campus không còn available

Trả lỗi xác định campus cụ thể và reason cụ thể.

Không trả chung chung `INTERNAL_SERVER_ERROR`.

---

# 21. Operational Readiness DTO

Bổ sung computed readiness vào Campus List và Campus Detail nếu contract hiện tại cho phép mở rộng tương thích.

DTO tối thiểu:

```ts
type CampusOperationalReadiness = {
  isAvailableForVisitRegistration: boolean;
  activeIcDepartmentExists: boolean;
  activeStaffLeaderExists: boolean;
  readinessIssues: string[];
};
```

Các issue gợi ý:

```text
CAMPUS_INACTIVE
ACTIVE_IC_DEPARTMENT_MISSING
MULTIPLE_ACTIVE_IC_DEPARTMENTS
ACTIVE_STAFF_LEADER_MISSING
MULTIPLE_ACTIVE_STAFF_LEADERS
STAFF_LEADER_WRONG_CAMPUS
STAFF_LEADER_WRONG_DEPARTMENT
IC_HEAD_MAPPING_INCONSISTENT
```

Chỉ trả issue thật sự cần cho HO. Không làm lộ dữ liệu nhạy cảm.

---

# 22. Frontend Campus Management

## 22.1. Status và readiness phải hiển thị riêng

Ví dụ:

```text
Trạng thái quản trị: Hoạt động
Khả năng tiếp nhận: Sẵn sàng nhận đăng ký
```

Hoặc:

```text
Trạng thái quản trị: Hoạt động
Khả năng tiếp nhận: Chưa sẵn sàng
Lý do: Chưa có Staff Leader đang hoạt động.
```

Campus INACTIVE:

```text
Trạng thái quản trị: Ngừng hoạt động
Khả năng tiếp nhận: Không nhận đăng ký
```

Không đổi toggle chỉ vì campus chưa có Staff Leader.

## 22.2. Sau Create Campus

Sau khi tạo campus ACTIVE nhưng chưa có Staff Leader, hiển thị thông báo rõ:

```text
Campus đã được tạo thành công.

Campus hiện đang hoạt động nhưng chưa xuất hiện trên form đăng ký vì chưa có Staff Leader đang hoạt động.
```

Không coi đây là create failure.

## 22.3. Disable confirmation

Trước khi confirm, gọi status impact preview.

Nếu `canChange = true`, modal phải nêu:

```text
Campus sẽ không còn xuất hiện trong các lựa chọn đăng ký/phân công mới.
Dữ liệu lịch sử, tài khoản và phòng ban vẫn được giữ nguyên.
```

Nếu `canChange = false`, không cho submit PATCH; hiển thị:

```text
Không thể ngừng hoạt động campus.

Campus hiện còn:
- x đơn đang chờ xử lý
- y chuyến đã được phân công/chuẩn bị
- z chuyến đang hoặc đã tiếp khách nhưng chưa đóng
```

Có action xem danh sách đoàn đang chặn nếu route hiện tại hỗ trợ filter.

## 22.4. PATCH vẫn xử lý 409

Dù preview cho phép, PATCH có thể bị 409 do race condition.

Frontend phải:

```text
Giữ toggle/badge cũ.
Hiển thị message backend.
Refresh blocker preview hoặc campus row.
Không optimistic update sai.
```

## 22.5. Enable success nhưng chưa ready

Nếu enable thành công mà thiếu Staff Leader:

```text
Toggle ON.
Badge Hoạt động.
Readiness vẫn Chưa sẵn sàng.
Hiển thị warning, không hiển thị lỗi.
```

## 22.6. Responsive và accessibility

Tuân thủ `PEMS_UI_DESIGN_SYSTEM_PROMPT.md`.

Bắt buộc:

```text
Keyboard accessible toggle/modal.
Focus management.
aria-label rõ cho action status.
Không dùng màu là tín hiệu duy nhất.
Loading state.
Error state.
Không vỡ mobile.
Không thêm animation thừa.
```

---

# 23. Concurrency và transaction

Phải xử lý race condition giữa submit và disable.

## 23.1. Submit

Trong transaction:

```text
Lock/check các campus được chọn.
Kiểm tra operational availability.
Sau đó mới tạo visit_request_campuses.
```

Với multi-campus, lock theo thứ tự `campus_id` tăng dần để giảm deadlock.

## 23.2. Disable

Trong transaction:

```text
Lock campus.
Recheck blocker.
Sau đó mới set INACTIVE.
```

Kết quả đúng:

```text
Submit lock trước:
- submit tạo campus instance
- disable chờ
- disable thấy blocker
- disable trả 409

Disable lock trước:
- disable set INACTIVE
- submit chờ
- submit thấy campus INACTIVE
- submit rollback
```

Không dựa vào preview để đảm bảo consistency.

Dùng transaction/locking pattern phù hợp EF Core/MySQL và source hiện tại. Không tự thêm raw SQL lock nếu project đã có abstraction an toàn tương đương.

---

# 24. Audit và logging

Mọi status change thành công phải ghi:

```text
entity = CAMPUS
entity_id = campusId
old_status
new_status
actor_user_id
occurred_at
```

Action semantic:

```text
ENABLE_CAMPUS
DISABLE_CAMPUS
```

Không ghi audit “thay đổi thành công” khi bị block.

Có thể ghi business/security event cho failed attempt nếu project đã có convention.

Không log:

```text
token
connection string
credential
PII không cần thiết
full request body chứa dữ liệu cá nhân
```

---

# 25. Database scope

Mặc định task này không cần thay schema.

Không được:

```text
Thêm operational_readiness column.
Thêm campus_status mới.
Thêm permission table.
Thêm migration EF tự động.
Đổi enum SQL nếu không có yêu cầu thực sự.
```

Operational availability là computed business state.

Chỉ tạo SQL patch nếu đọc source/schema phát hiện thiếu constraint bắt buộc và phải báo rõ trước khi thực hiện. Không âm thầm đổi database.

---

# 26. Backend architecture rules

Tuân thủ Clean Architecture:

```text
Controller:
- chỉ nhận request
- gọi MediatR/service theo pattern hiện tại
- không chứa business query phức tạp

Validator:
- input validation
- campusId/status enum/required/format

Handler/domain service:
- DB/business validation
- dependency check
- availability calculation
- transition

Infrastructure:
- EF query/config/transaction implementation
```

Không tạo circular dependency.

Không truy cập DbContext trực tiếp từ frontend/API layer nếu architecture hiện tại cấm.

Không dùng `try/catch` để nuốt exception.

Không trả exception message nội bộ ra client.

---

# 27. Business Rules cuối cùng

```text
BR-81-01
Campus mới được tạo với status ACTIVE.

BR-81-02
Create Campus không nhận hoặc chọn Staff Leader.

BR-81-03
Campus mới có ic_head_user_id = NULL.

BR-81-04
Create Campus tự tạo IC Department ACTIVE với head_user_id = NULL.

BR-81-05
Campus và IC Department được tạo trong cùng transaction.

BR-86-01
Chỉ HO có account ACTIVE được enable/disable campus.

BR-86-02
Campus không bị xóa cứng; chỉ chuyển ACTIVE/INACTIVE.

BR-86-03
Campus status và khả năng nhận đăng ký là hai khái niệm khác nhau.

BR-86-04
Campus chỉ xuất hiện trên form khi:
campus ACTIVE
AND IC Department ACTIVE hợp lệ
AND đúng một Staff Leader ACTIVE hợp lệ.

BR-86-05
Campus ACTIVE nhưng thiếu Staff Leader vẫn hiển thị trong Campus Management,
nhưng không xuất hiện trên form đăng ký.

BR-86-06
Backend submit phải kiểm tra lại toàn bộ operational availability;
không tin campusId từ frontend.

BR-86-07
Không cho disable campus nếu có campus instance ở:
WAITING_REQUEST_APPROVAL,
ASSIGNED,
BEFORE_VISIT,
DURING_VISIT,
AFTER_VISIT.

BR-86-08
CLOSED, CANCELLED và REJECTED không block disable.

BR-86-09
Dependency được kiểm tra theo visit_request_campuses.campus_id,
không dựa riêng vào visit_requests.status.

BR-86-10
Ngày giờ không thay thế status trong việc xác định blocker.

BR-86-11
Disable không tự động reject, cancel, close hoặc sửa dữ liệu visit.

BR-86-12
Disable không tự động thay đổi trạng thái user hoặc department.

BR-86-13
Campus INACTIVE bị loại khỏi business flow mới,
nhưng dữ liệu lịch sử vẫn được giữ nguyên.

BR-86-14
Enable yêu cầu master data hợp lệ và IC Department ACTIVE.

BR-86-15
Enable không bắt buộc Staff Leader để set campus ACTIVE;
Staff Leader chỉ là điều kiện operational availability.

BR-86-16
Enable không khôi phục request, participant, invitation,
host hoặc dependency lịch sử.

BR-86-17
Preview chỉ hỗ trợ UX; PATCH và Submit phải recheck trong transaction.

BR-86-18
Mọi status change thành công phải cập nhật updated_by,
updated_at và audit log.

BR-86-19
Nhiều hơn một IC Department ACTIVE hoặc nhiều hơn một Staff Leader ACTIVE hợp lệ
là lỗi cấu hình; campus không được xuất hiện trên form.

BR-86-20
Không được chọn ngẫu nhiên IC Department hoặc Staff Leader bằng FirstOrDefault
khi dữ liệu có nhiều bản ghi hợp lệ.
```

---

# 28. Unit Test bắt buộc

Viết unit test có giá trị nghiệp vụ, không test giả.

## 28.1. Create Campus regression

```text
Create hợp lệ:
- campus ACTIVE
- ic_head_user_id NULL
- IC department ACTIVE
- head_user_id NULL

Không có Staff Leader:
- create vẫn thành công
- readiness false
```

## 28.2. Availability evaluator/query

```text
Campus ACTIVE + IC ACTIVE + đúng 1 Leader ACTIVE:
- available true

Campus INACTIVE:
- false

Không có IC ACTIVE:
- false

Nhiều IC ACTIVE:
- false + configuration issue

Không có Leader:
- false

Leader INACTIVE:
- false

STAFF + sub_role STAFF:
- false

Leader khác campus:
- false

Leader thuộc GENERAL department:
- false

Nhiều Leader hợp lệ:
- false + configuration issue
```

## 28.3. Disable handler

```text
WAITING_REQUEST_APPROVAL:
- conflict
- campus giữ ACTIVE

ASSIGNED:
- conflict

BEFORE_VISIT:
- conflict

DURING_VISIT:
- conflict

AFTER_VISIT:
- conflict

Chỉ CLOSED:
- disable success

Chỉ CANCELLED:
- success

Chỉ REJECTED:
- success

Không có instance:
- success

Multi-campus:
- blocker của campus A không block campus B
```

## 28.4. Enable handler

```text
Master data thiếu:
- reject

Không có IC department ACTIVE:
- reject

Có IC ACTIVE nhưng chưa có Staff Leader:
- enable success
- status ACTIVE
- readiness false

Có đủ Staff Leader:
- enable success
- readiness true
```

## 28.5. Authorization

```text
Non-HO:
- forbidden

HO inactive/locked:
- bị chặn theo current auth/session policy

HO ACTIVE:
- được xử lý khi business rules pass
```

---

# 29. Frontend Test

Dùng test framework hiện có.

Tối thiểu kiểm tra:

```text
ACTIVE nhưng readiness false:
- toggle ON
- badge Hoạt động
- badge Chưa sẵn sàng
- reason hiển thị

Disable preview có blocker:
- không gọi PATCH
- hiển thị blocker summary

Preview pass nhưng PATCH trả 409:
- rollback UI state
- giữ toggle ON
- hiển thị backend message

Enable success readiness false:
- toggle ON
- warning hiển thị
- không hiển thị success sai kiểu “đã sẵn sàng nhận đăng ký”

Create Campus thiếu Leader:
- create success
- readiness warning
```

Không test chỉ bằng snapshot mơ hồ.

---

# 30. Không được làm

```text
Không đổi Create Campus sang INACTIVE.
Không bắt Staff Leader trong Create Campus.
Không bắt Staff Leader để enable campus.
Không cho ACTIVE campus thiếu Leader xuất hiện trên form.
Không chỉ lọc frontend.
Không chỉ check visit_requests.status.
Không dùng ngày thay status.
Không auto-cancel/reject/close visit.
Không auto-disable user/department.
Không xóa dữ liệu lịch sử.
Không thêm operational readiness vào DB.
Không tạo dynamic permission.
Không dùng role legacy.
Không sửa ngoài scope nếu không cần.
Không báo pass test nếu chưa chạy.
Không tạo test skipped/pending.
Không dùng test chỉ verify mock được gọi mà không assert outcome.
Không tạo, sửa hoặc chạy Integration Test trong task này.
```

---

# 31. Phạm vi được sửa

Được sửa khi cần:

```text
Backend Campus queries/commands/handlers/validators/DTOs
Backend campus availability helper/service
Backend visitor campus options query
Backend Submit Visit Request availability validation
Controller route nếu cần preview endpoint
Frontend Campus Management list/detail
Frontend Campus API service/types
Frontend Visit Registration campus loading nếu đang lọc sai
i18n translation keys
Unit Tests
Frontend tests
Documentation UC-86 nếu source repo quản lý docs cùng code
```

---

# 32. Phạm vi không được sửa

Không được tự ý refactor hoặc thay đổi:

```text
Approval architecture của visit request
Campus-independent Staff Leader approval
Host assignment flow
Visit lifecycle ngoài dependency check cần thiết
OTP flow
Notification architecture
Logistics workflow
Department status workflow
Account status workflow
Database schema ngoài yêu cầu
Deployment configuration
```

Nếu phát hiện bug ngoài scope, ghi vào báo cáo, không sửa âm thầm.

---

# 33. Quy trình triển khai bắt buộc

## Phase A — Audit hiện trạng

Báo cáo trước khi sửa:

```text
1. File/path thật liên quan.
2. Endpoint hiện tại.
3. Current Create Campus behavior.
4. Current Manage Status behavior.
5. Current campus options query.
6. Current submit validation.
7. Current status enums.
8. Existing tests.
9. Gap so với prompt này.
10. Có cần SQL change không.
```

## Phase B — Kế hoạch thay đổi

Liệt kê:

```text
File sẽ sửa.
File sẽ thêm.
Contract thay đổi.
Business query dùng chung.
Error code.
Test bổ sung.
Migration/SQL: phải là không, trừ khi có bằng chứng ngược lại.
```

## Phase C — Implement

Thực hiện theo Clean Architecture và pattern source hiện tại.

## Phase D — Verification

Chạy:

```text
dotnet build
Unit Tests liên quan
Architecture Tests nếu thay controller/layer
Frontend typecheck/build
Frontend lint
Frontend tests
```

Nếu command khác, dùng command thật của repo.

## Phase E — Final Report

Báo cáo evidence thật, không chỉ nói “đã hoàn thành”.

---

# 34. Output report bắt buộc của AI Agent

Sau khi code, trả báo cáo theo cấu trúc:

```text
A. Current-state findings
- Source files đã đọc
- Current behavior
- Gaps

B. Implementation summary
- Backend
- Frontend
- Shared availability logic
- Submit validation
- Status preview
- Audit/error handling

C. Business rule verification
- Create Campus giữ nguyên
- Disable blocker statuses
- Terminal statuses
- Enable without Staff Leader
- Operational availability

D. Files changed
- Mỗi file + lý do

E. Tests
- Unit Test backend mới/cập nhật
- Frontend test mới/cập nhật
- Số passed/failed/skipped
- Command đã chạy
- Runtime nếu có
- Xác nhận không triển khai Integration Test

F. Build/lint
- Backend
- Frontend
- Architecture tests

G. Database
- Có/không schema change
- Nếu không: xác nhận không migration/patch

H. Remaining risks
- Chỉ ghi issue thật còn lại
```

Không che giấu test fail.

---

# 35. Definition of Done

Chỉ được báo hoàn thành khi toàn bộ điều sau đúng:

```text
[ ] Create Campus vẫn tạo ACTIVE + IC ACTIVE + chưa cần Staff Leader.
[ ] Campus ACTIVE thiếu Leader không xuất hiện trên form.
[ ] Campus availability dùng chung một nguồn logic.
[ ] Submit recheck availability ở backend.
[ ] Multi-campus submit rollback nếu một campus invalid.
[ ] Disable block đúng mọi non-terminal campus instance.
[ ] CLOSED/CANCELLED/REJECTED không block.
[ ] Disable không cascade.
[ ] Enable không bắt Staff Leader.
[ ] Enable thiếu Leader trả ACTIVE nhưng readiness false.
[ ] UI phân biệt status và readiness.
[ ] Preview hiển thị blocker.
[ ] PATCH recheck trong transaction.
[ ] Authorization chỉ HO ACTIVE.
[ ] Audit đúng.
[ ] Unit Tests pass.
[ ] Frontend tests pass.
[ ] Backend build pass.
[ ] Frontend build/typecheck/lint pass.
[ ] Không có test skipped mới.
[ ] Không tạo/sửa/chạy Integration Test trong task này.
[ ] Không có schema change ngoài scope.
[ ] Không dùng dynamic permissions.
```

---

# 36. Kết luận nghiệp vụ không được hiểu sai

```text
Create Campus:
ACTIVE + IC Department ACTIVE + chưa có Staff Leader.

Campus Management:
vẫn hiển thị ACTIVE ngay sau create.

Visit Registration Form:
chỉ hiển thị campus khi:
ACTIVE
AND IC Department ACTIVE hợp lệ
AND đúng một Staff Leader ACTIVE hợp lệ.

Disable:
chỉ cho phép khi campus không còn campus instance ở trạng thái đang xử lý.

Terminal không block:
CLOSED
CANCELLED
REJECTED

Enable:
yêu cầu master data hợp lệ + IC Department ACTIVE.
Không bắt buộc Staff Leader.

Staff Leader:
là điều kiện để campus nhận đăng ký,
không phải điều kiện để campus mang status ACTIVE.

Không cascade:
không tự động sửa visit, user, department hoặc dữ liệu lịch sử.
```
