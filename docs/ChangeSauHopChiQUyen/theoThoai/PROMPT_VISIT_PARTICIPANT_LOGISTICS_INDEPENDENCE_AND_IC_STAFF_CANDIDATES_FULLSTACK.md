# PROMPT — TÁCH LOGIC MỜI THAM GIA VÀ YÊU CẦU HẬU CẦN, TỰ ĐIỀN THỜI GIAN LOGISTIC, MỞ RỘNG ỨNG VIÊN IC SUPPORT

## 1. Vai trò của AI Agent

Bạn là Senior Full-stack Engineer chịu trách nhiệm cập nhật chức năng chuẩn bị đoàn khách trong dự án PEMS. Bạn phải làm việc đồng thời với các vai trò:

- Senior ASP.NET Core .NET 8 / Clean Architecture Developer.
- Senior React Vite TypeScript Engineer.
- Database-first MySQL Reviewer.
- Security và Authorization Reviewer.
- QA Engineer, có trách nhiệm viết test thật và chạy verification thật.

Không chỉ mô tả giải pháp. Hãy đọc source hiện tại, triển khai code, bổ sung test phù hợp, chạy build/test và báo cáo bằng chứng.

---

## 2. Bối cảnh dự án

PEMS sử dụng:

- Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, EF Core/Pomelo MySQL.
- Frontend: React Vite TypeScript, Tailwind CSS.
- Database: MySQL 8, database-first, fresh-create SQL.
- Authorization: fixed policy theo `role_code`, `sub_role`, campus, department, host và participant relationship; không dùng dynamic permissions.
- Thời gian nghiệp vụ: Vietnam wall-clock, timezone `Asia/Ho_Chi_Minh`.

Role runtime hợp lệ:

- `ADMIN`
- `HO`
- `STAFF`
- `DEPARTMENT`
- `STUDENT`
- `VISITOR`

SubRole hợp lệ:

- `LEADER`
- `STAFF`
- `NULL`

Các effective role liên quan trực tiếp đến task:

- `STAFF + LEADER` = Staff Leader.
- `STAFF + STAFF` = IC Staff.
- `DEPARTMENT + LEADER` = Department Leader.
- `DEPARTMENT + STAFF` = Department Staff.

Cấm dùng role legacy hoặc tự tạo role mới như `STAFF_LEADER` làm `role_code`, `STAFF_L`, `STAFF_P`, `DEPT_L`, `DEPT_P`.

---

## 3. Tài liệu và source bắt buộc đọc trước khi sửa

Trước khi code, hãy search và đọc source hiện tại. Không sửa theo suy đoán và không chỉ tin vào tên/comment cũ.

### 3.1. Tài liệu chuẩn

Đọc và đối chiếu ở mức cần thiết:

1. `PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md`
2. `CLEAN_ARCHITECTURE.md`
3. `PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md`
4. `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`
5. `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`
6. `VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md`
7. `PERMISSION_MATRIX.md`
8. `PERMISSION_RULES.md`
9. `PROJECT_STRUCTURE_FULL.md`
10. `PEMS_UI_DESIGN_SYSTEM_PROMPT.md`
11. `PEMS_v10_NEW_FINAL_SQL_TABLE_FIELD_DICTIONARY_MATCHED.docx`
12. SQL fresh-create mới nhất trong repository.
13. Existing Unit Test, frontend test và test infrastructure thật đang được solution sử dụng.

Khi tài liệu/code/comment mâu thuẫn, ưu tiên:

1. SQL fresh-create mới nhất.
2. SQL Table & Field Dictionary mới nhất.
3. Canonical Business Rules.
4. UC Implementation Rulebook.
5. Project Overview/Visitor Management System.
6. Source runtime hiện tại.
7. Tài liệu legacy chỉ dùng tham khảo.

### 3.2. Baseline source đã xác định, nhưng vẫn phải search lại trên branch hiện tại

Kiểm tra tối thiểu các file/module sau và mọi caller/usages liên quan:

#### Backend

- `backend/PEMS.Api/Controllers/DelegationsController.cs`
- `backend/PEMS.Application/Delegations/Queries/GetSupportDepartments/GetSupportDepartmentsQuery.cs`
- `backend/PEMS.Application/Delegations/Queries/GetSupportDepartments/GetSupportDepartmentsQueryHandler.cs`
- `backend/PEMS.Application/Delegations/Queries/GetSupportDepartments/SupportDepartmentDto.cs`
- `backend/PEMS.Application/Delegations/Queries/GetParticipantCandidates/GetParticipantCandidatesQueryHandler.cs`
- `backend/PEMS.Application/Delegations/Commands/InviteVisitParticipant/InviteVisitParticipantCommand.cs`
- `backend/PEMS.Application/Delegations/Commands/InviteVisitParticipant/InviteVisitParticipantCommandHandler.cs`
- `backend/PEMS.Application/Delegations/Commands/PrepareVisitLogistics/PrepareVisitLogisticsCommandHandler.cs`
- `backend/PEMS.Application/Delegations/Commands/PrepareVisitLogistics/PrepareVisitLogisticsCommandValidator.cs`
- `backend/PEMS.Application/Delegations/Common/VisitInstanceAccess.cs`
- `backend/PEMS.Application/Delegations/Queries/GetVisitProcessPermissions/GetVisitProcessPermissionsQueryHandler.cs`
- `backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs`
- Constants/entities/EF configuration liên quan đến `visit_participants`, `visit_request_campuses`, `visit_logistics_items`, roles, subroles và statuses.

#### Frontend

- `frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx`
- `frontend/pems-react/src/features/delegations/components/ParticipantInvitationSection.tsx`
- `frontend/pems-react/src/features/delegations/components/LogisticsRequestSection.tsx`
- `frontend/pems-react/src/features/delegations/api/delegationsApi.ts`
- `frontend/pems-react/src/features/delegations/types/delegations.types.ts`
- `frontend/pems-react/src/shared/api/endpoints.ts`
- `frontend/pems-react/src/shared/utils/vietnamTime.ts`
- Locale/i18n files nếu component liên quan đã được đưa vào i18n.

#### Test

- Search toàn bộ `tests/PEMS.UnitTests/**`.
- Search `frontend/pems-react/tests/**` và test runner hiện có.
- Không đặt test vào scaffold/project không nằm trong solution hoặc không được test discovery chạy.

### 3.3. Search bắt buộc trước khi đổi contract

Search toàn repository các từ/call site:

```text
GetSupportDepartments
SupportDepartmentDto
SupportDepartment
getSupportDepartments
canInvite
disabledReason
IC_SUPPORT
GetParticipantCandidates
InviteVisitParticipant
PrepareVisitLogistics
usageStartAt
usageEndAt
plannedStartAt
plannedEndAt
```

Phải xác định đầy đủ consumer trước khi rename/remove field. Không được sửa một call site rồi để call site khác lỗi âm thầm.

---

## 4. Current-state findings cần xác minh lại

Baseline đã quan sát trên source hiện tại:

1. Backend `PrepareVisitLogisticsCommandHandler` không chặn yêu cầu logistic chỉ vì Department Leader đã nằm trong `visit_participants`.
2. `GetSupportDepartmentsQueryHandler` trả cờ `CanInvite` dành cho nghiệp vụ mời phòng ban tham gia.
3. Cờ này trở thành `false` khi leader đã có participant active ở một trong các trạng thái `INVITED`, `ACCEPTED`, `ASSIGNED`.
4. Frontend `LogisticsRequestSection` đang tái sử dụng chính `canInvite` để khóa lựa chọn phòng ban xử lý logistic.
5. Đây là root cause làm hai nghiệp vụ độc lập bị nối nhầm ở UI/API contract.
6. `VisitProcess` đã có `detail.plannedStartAt` và `detail.plannedEndAt`, nhưng chưa truyền xuống `LogisticsRequestSection`.
7. `ResourceForm` hiện khởi tạo `usageStartAt` và `usageEndAt` rỗng.
8. Candidate `IC_SUPPORT` hiện chỉ chấp nhận `STAFF + STAFF`, chưa gồm `STAFF + LEADER`.
9. Backend `ResolveInviteeAsync` cũng đang validate cứng `SubRole == STAFF`, nên chỉ sửa frontend/query là chưa đủ.

Trước khi implement, hãy xác minh từng finding bằng source của branch hiện tại và báo cáo nếu baseline đã thay đổi.

---

## 5. Mục tiêu task

Triển khai đồng bộ ba thay đổi:

1. Tách hoàn toàn capability “mời phòng ban tham gia” khỏi capability “gửi yêu cầu hậu cần cho phòng ban”.
2. Tự điền thời gian bắt đầu/kết thúc sử dụng logistic bằng thời gian đoàn tới thăm campus instance hiện tại, nhưng vẫn cho Host chỉnh sửa.
3. Cho phép Host mời cả Staff Leader và IC Staff cùng campus vào nhóm `IC_SUPPORT`, trừ chính Host và các participant đang active.

Kết quả phải đúng cả frontend, backend validation, authorization, response type và test.

---

## 6. Phạm vi được sửa

Được sửa trong phạm vi cần thiết:

### Backend

- DTO/query/handler trả danh sách phòng ban hỗ trợ.
- Candidate query cho `IC_SUPPORT`.
- Command handler mời participant.
- Comment/error message liên quan để phản ánh đúng nghiệp vụ.
- Helper authorization/capability nếu thật sự cần sau khi audit.
- Unit Test thật cho logic thay đổi.

### Frontend

- `VisitProcess` và `LogisticsRequestSection` để truyền/khởi tạo thời gian.
- `ParticipantInvitationSection` để hiển thị Staff/Staff Leader phù hợp.
- API types/interfaces và tất cả consumer của capability phòng ban.
- Frontend test hiện có phù hợp với stack của repository.

### Database

- Chỉ đọc và xác minh schema hiện có.
- Task này dự kiến không cần schema change, migration, patch SQL hoặc seed mới.

---

## 7. Phạm vi không được sửa

Không được:

1. Thay đổi schema database nếu source hiện tại đã đủ field.
2. Thêm table, column, enum, participant role hoặc logistics status mới.
3. Tạo participant role mới như `STAFF_LEADER_SUPPORT`; Staff Leader được mời vẫn dùng `IC_SUPPORT`.
4. Thay đổi luồng Accept/Decline token, email transaction, notification hoặc audit ngoài phần cần thiết.
5. Thay đổi quy tắc duplicate participant active.
6. Cho phép mời chính `current_host_user_id`.
7. Cho phép mời user khác campus, inactive hoặc không thuộc IC department active.
8. Khóa cứng thời gian logistic bằng thời gian đoàn; đây chỉ là default có thể chỉnh.
9. Dùng `toISOString()`, tự append `Z` hoặc chuyển datetime-local qua UTC.
10. Đưa business logic vào Controller.
11. Chỉ ẩn/hiện ở frontend mà thiếu backend validation.
12. Dùng mock data cho flow thật.
13. Dùng dynamic permissions, `permissions`, `role_permissions`, `permission_code`.
14. Tạo test giả, assert hời hợt, test implementation detail vô nghĩa hoặc test không được discovery chạy.
15. Tự commit, push, merge hoặc mở PR nếu chưa được yêu cầu.

---

## 8. Quy trình phân tích trước khi code

Trước khi chỉnh sửa:

1. Xác định branch, working tree và các thay đổi đang tồn tại; không ghi đè thay đổi không liên quan.
2. Đọc chuỗi đầy đủ:

```text
Frontend page/component
→ frontend API service/type
→ backend controller
→ query/command
→ handler/validator
→ entity/EF mapping
→ SQL schema
→ existing tests
```

3. Vẽ ngắn gọn current flow của:
   - Mời IC Support.
   - Mời phòng ban hỗ trợ.
   - Chọn phòng ban cho system logistic request.
   - Khởi tạo thời gian logistic form.
4. Search mock/stub/dead code/legacy duplicate endpoint có thể khiến sửa nhầm flow.
5. Xác định chính xác các caller dùng `SupportDepartment.canInvite` trước khi đổi contract.
6. Chỉ sau khi có kết luận root cause mới bắt đầu sửa.

---

## 9. Yêu cầu nghiệp vụ chi tiết

## 9.1. Mời tham gia và yêu cầu logistic là hai nghiệp vụ độc lập

Một Department Leader có thể đồng thời:

- Là participant của đoàn với `participant_role = DEPT_SUPPORT`.
- Là người đại diện nhận email/notification logistic cho phòng ban.

Việc leader đã được mời hoặc đã tham gia đoàn không được làm phòng ban mất khả năng nhận logistic.

Ma trận bắt buộc:

| Điều kiện | Mời phòng ban tham gia | Gửi logistic qua hệ thống |
|---|---:|---:|
| Có active leader, leader chưa là active participant | Cho phép | Cho phép |
| Leader đang `INVITED` | Không mời trùng | Cho phép |
| Leader đang `ACCEPTED` | Không mời trùng | Cho phép |
| Leader đang `ASSIGNED` | Không mời trùng | Cho phép |
| Leader `DECLINED` hoặc `REMOVED` | Cho phép mời lại | Cho phép |
| Không có active leader | Không cho mời | Không cho system request |
| Department khác campus/inactive/không phải GENERAL | Không hợp lệ | Không hợp lệ |

Luồng `OFFLINE_COORDINATED` giữ nguyên: có thể lưu dấu vết theo rule hiện tại; nếu chọn department thì department vẫn phải đúng campus, `GENERAL`, `ACTIVE`, nhưng không được buộc phải có leader chỉ để lưu dấu vết offline nếu backend hiện hành không yêu cầu.

## 9.2. Tách capability ở contract phòng ban

Không tiếp tục dùng một cờ `canInvite` chung cho cả hai màn hình.

Thiết kế contract rõ nghĩa, ưu tiên:

```csharp
CanInviteParticipant
ParticipantDisabledReason
CanReceiveLogistics
LogisticsDisabledReason
```

Frontend tương ứng:

```ts
canInviteParticipant: boolean;
participantDisabledReason?: string | null;
canReceiveLogistics: boolean;
logisticsDisabledReason?: string | null;
```

Quy tắc:

```text
canInviteParticipant =
    active Department Leader tồn tại
    AND leader không có participant active trong instance

canReceiveLogistics =
    active Department Leader tồn tại
```

Participant active dùng đúng tập trạng thái đang có:

```text
INVITED
ACCEPTED
ASSIGNED
```

`DECLINED` và `REMOVED` không block lời mời lại.

Yêu cầu triển khai:

- `ParticipantInvitationSection` dùng `canInviteParticipant` và `participantDisabledReason`.
- `LogisticsRequestSection` dùng `canReceiveLogistics` và `logisticsDisabledReason`.
- Không dùng `canInviteParticipant` trong logistic.
- Backend create logistic vẫn revalidate department/leader độc lập; không tin capability từ frontend.
- Search mọi consumer trước khi xóa field legacy. Nếu cần giữ `canInvite/disabledReason` tạm thời để tương thích caller khác, phải ghi rõ lý do và bảo đảm toàn bộ known caller được migrate; không để hai nguồn logic drift lâu dài.

Ví dụ response mong muốn khi leader đã tham gia:

```json
{
  "departmentId": 10,
  "departmentName": "Phòng Công nghệ Thông tin HN",
  "leaderUserId": 123,
  "leaderName": "IT Leader HN",
  "leaderEmail": "it.leader.hn@fpt.edu.vn",
  "canInviteParticipant": false,
  "participantDisabledReason": "Trưởng phòng này đã có trong danh sách tham gia của đoàn.",
  "canReceiveLogistics": true,
  "logisticsDisabledReason": null
}
```

## 9.3. Tự điền thời gian logistic theo campus instance

Nguồn thời gian duy nhất cho default là:

```text
detail.plannedStartAt của visit_request_campuses hiện tại
detail.plannedEndAt của visit_request_campuses hiện tại
```

Không lấy earliest time của toàn visit request và không lấy thời gian campus khác trong multi-campus request.

Yêu cầu:

1. Truyền `plannedStartAt` và `plannedEndAt` từ `VisitProcess` xuống `LogisticsRequestSection`.
2. Truyền tiếp default time xuống mọi `ResourceCard` qua props/shared props phù hợp.
3. Khi tạo system logistic form mới:

```text
usageStartAt = plannedStartAt
usageEndAt = plannedEndAt
```

4. Áp dụng cho:
   - Welcome LED.
   - Xe điện.
   - Người lái.
   - Phòng họp.
   - Teabreak.
   - Yêu cầu khác ở mode `SYSTEM_REQUEST`.
5. Luồng offline hiện không có time inputs thì giữ nguyên, không tự mở rộng UI ngoài scope.
6. Host được chỉnh lại cả hai giá trị.
7. Không tự ghi đè giá trị Host đã chỉnh khi component re-render/refetch.
8. Nếu đã có logistics item, dùng `usageStartAt/usageEndAt` của item đã lưu, không thay bằng planned time.
9. Khi reset một form chưa lưu, reset về planned time mặc định, không reset thành chuỗi rỗng.
10. Nếu planned time null/invalid, giữ empty fallback và để validation hiện tại xử lý; không bịa thời gian.

Timezone rules bắt buộc:

- Dùng `toVietnamDateTimeLocalInput()` trong shared `vietnamTime.ts` để hydrate `<input type="datetime-local">`.
- Gửi datetime-local wall-clock string về API bằng contract hiện có.
- Không dùng `new Date(value).toISOString()`.
- Không append `Z`.
- Không phụ thuộc timezone của browser.
- Nên dùng helper chung cả khi hydrate existing logistics item, thay vì `.slice(0, 16)` nếu API có thể trả offset `+07:00`.

Đây chỉ là default. Không thêm rule bắt buộc logistic time phải luôn bằng planned visit time, vì setup/cleanup có thể cần thời gian khác.

Giữ các validation hiện có:

- `usageStartAt` bắt buộc cho system request.
- `usageEndAt` bắt buộc cho system request.
- Start không nằm trong quá khứ theo Vietnam time policy hiện tại.
- End phải sau start.
- `dueAt` bắt buộc cho `HIGH/URGENT`.
- `dueAt` không nằm trong quá khứ.
- `dueAt <= usageStartAt`.

## 9.4. Mời cả Staff Leader và IC Staff làm IC Support

Candidate `type = IC_SUPPORT` phải bao gồm:

```text
u.status = ACTIVE
AND role_code = STAFF
AND sub_role IN (LEADER, STAFF)
AND primary_campus_id = instance.campus_id
AND user thuộc IC department ACTIVE
AND user_id != instance.current_host_user_id
AND user chưa có participant active trong instance
```

Không được chỉ sửa candidate query. `InviteVisitParticipantCommandHandler.ResolveInviteeAsync` phải revalidate cùng rule để chống gọi API trực tiếp/payload spoofing.

Ma trận:

| Candidate | Xuất hiện/được mời? |
|---|---:|
| `STAFF + STAFF`, ACTIVE, IC ACTIVE, cùng campus | Có |
| `STAFF + LEADER`, ACTIVE, IC ACTIVE, cùng campus | Có |
| Chính current Host, dù là Staff hay Staff Leader | Không |
| Đã `INVITED/ACCEPTED/ASSIGNED` | Không |
| Đã `DECLINED/REMOVED` | Có thể mời lại |
| Khác campus | Không |
| User INACTIVE | Không |
| IC department INACTIVE | Không |
| Không thuộc IC department | Không |

Khi mời Staff Leader:

- Vẫn tạo/reuse `visit_participants` với `participant_role = IC_SUPPORT`.
- Không đổi `role_code` hoặc `sub_role` của tài khoản.
- Không tạo participant enum mới.
- Giữ flow email, one-time Accept/Decline token, notification, audit và transaction hiện tại.
- UI nên hiển thị nhãn phân biệt dựa trên `candidate.subRole`, ví dụ `Staff Leader hỗ trợ IC` hoặc `Staff hỗ trợ IC`, nhưng participant role vẫn là `IC_SUPPORT`.
- Error message backend phải phản ánh cả Staff và Staff Leader, không còn nói như thể chỉ Staff thường là hợp lệ.

## 9.5. Quyền của Staff Leader sau khi chấp nhận lời mời

Audit kỹ precedence hiện tại giữa system relation `STAFF_LEADER` và participant relation `IC_SUPPORT`.

Yêu cầu kết quả:

- Staff Leader vẫn giữ quyền hệ thống Staff Leader đúng campus.
- Khi có participant row `IC_SUPPORT` đã `ACCEPTED`, họ đồng thời nhận các quyền contribution gắn với accepted participant của chính instance đó theo rule hiện tại.
- Không thay global relation precedence chỉ để một test pass nếu việc đó làm mất quyền Staff Leader.
- `GetVisitProcessPermissions`/contribution handlers phải dựa vào accepted participant row cho quyền participant-specific, không chỉ dựa vào label relation.
- Nếu phát hiện endpoint participant-only đang dùng duy nhất `VisitInstanceAccess.ResolveRelationAsync` và vì precedence trả `STAFF_LEADER` nên Staff Leader được mời bị từ chối sai, hãy sửa nhỏ nhất tại authorization/capability check liên quan. Không đổi toàn bộ permission model nếu chưa chứng minh cần thiết.

---

## 10. Backend implementation rules

Tuân thủ chuỗi:

```text
SQL
→ Entity/EF Configuration
→ DTO
→ Query/Command
→ Handler/Validator
→ Controller
→ Frontend type/API/component
→ Test
```

Rules:

1. Controller chỉ binding request và gọi MediatR.
2. Input validation thuộc FluentValidation khi phù hợp.
3. Business validation/campus/department/participant/status thuộc Handler hoặc shared scoped helper.
4. Không tin `userId`, `departmentId`, capability hoặc role label do frontend gửi.
5. `GetSupportDepartments` phải host-only như hiện tại, nhưng comment/error wording cần phản ánh đây là danh sách dùng cho cả participant invitation và logistics selection.
6. `PrepareVisitLogisticsCommandHandler` phải tiếp tục xác thực:
   - Authenticated.
   - Actor là current Host.
   - Instance ở `ASSIGNED` hoặc `BEFORE_VISIT`.
   - Department cùng campus.
   - Department `GENERAL + ACTIVE`.
   - System request có active Department Leader.
7. Không thêm check participant vào create logistic.
8. Duplicate logistics rule theo `(visit_instance_id, item_type, title)` và active status giữ nguyên.
9. Email thất bại không được rollback item đã commit theo behavior hiện có.
10. Không tạo N+1 query không cần thiết; ưu tiên projection/grouping hiện có.

---

## 11. Frontend/UI rules

1. Giữ layout/design hiện tại, chỉ sửa hành vi và nhãn cần thiết.
2. Dropdown phòng ban trong participant invitation:
   - Disable dựa trên `canInviteParticipant`.
   - Hiển thị `participantDisabledReason`.
3. Dropdown phòng ban trong system logistic:
   - Cho chọn dựa trên `canReceiveLogistics`.
   - Hiển thị `logisticsDisabledReason` nếu không thể chọn.
   - Trường hợp leader đã tham gia nhưng active vẫn phải có nút `Chọn`.
4. Offline department dropdown giữ behavior hiện tại; không áp cờ invitation lên nó.
5. Candidate IC Support hiển thị cả Staff Leader và Staff.
6. Không hiển thị current Host trong candidate list.
7. Có loading/empty/error behavior như hiện tại; không nuốt lỗi mới.
8. Không hard-code fake department/leader/time.
9. Nếu module đã dùng i18n, thêm key VI/EN với parity; nếu phần này vẫn là legacy hard-coded Vietnamese, không mở rộng thành refactor i18n toàn màn ngoài scope, nhưng không tạo chuỗi mojibake.
10. Responsive hiện tại không được regress.

---

## 12. Database/SQL alignment

Xác minh các dữ liệu hiện có đủ phục vụ task:

- `visit_request_campuses.planned_start_at`
- `visit_request_campuses.planned_end_at`
- `visit_request_campuses.current_host_user_id`
- `visit_participants.visit_instance_id`
- `visit_participants.user_id`
- `visit_participants.participant_role`
- `visit_participants.status`
- `visit_logistics_items.requested_to_department_id`
- `visit_logistics_items.usage_start_at`
- `visit_logistics_items.usage_end_at`
- `departments.department_type`
- `departments.status`
- `departments.head_user_id`
- `users.role_id`, `users.sub_role`, `users.status`, `users.primary_campus_id`, `users.department_id`

Expected result: không cần schema change.

Không được:

- Tạo EF migration.
- Sửa fresh-create SQL.
- Tạo patch SQL.
- Thêm enum/status/column để giải quyết vấn đề vốn chỉ là API/UI capability.

Nếu source thật chứng minh bắt buộc phải sửa schema, dừng và báo cáo lý do; không tự mở rộng scope.

---

## 13. Validation và authorization

### 13.1. Participant invitation

Backend phải chặn:

- Actor không authenticated.
- Actor không phải current Host.
- Instance ngoài preparation window.
- User target không tồn tại/không ACTIVE.
- User khác campus.
- Target không phải `STAFF + LEADER/STAFF` thuộc IC ACTIVE đối với `IC_SUPPORT`.
- Target chính là current Host.
- Target đã có participant active trong instance.

### 13.2. Logistic request

Backend phải chặn:

- Actor không phải current Host.
- Sai stage.
- Department sai campus, không phải GENERAL hoặc inactive.
- System request thiếu active leader.
- Invalid/missing required usage time.
- Invalid time ordering/due date theo validator hiện tại.

Backend tuyệt đối không được chặn chỉ vì Department Leader đã có participant active.

### 13.3. Fixed policy

Authorization dựa trên:

- `role_code`
- `sub_role`
- `primary_campus_id`
- `department_id`
- `current_host_user_id`
- participant relationship
- instance/participant/logistics status

Không dùng dynamic permissions.

---

## 14. Test requirements

Đọc test infrastructure thật trước khi tạo test. Test phải nằm trong project/test runner thực sự được solution hoặc frontend config chạy.

Không tạo Integration Test mới nếu task hiện tại chỉ được triển khai theo phạm vi Unit Test + frontend component/E2E test nhỏ. Nếu repository đã có Integration Test trực tiếp cho các endpoint này và thay đổi contract làm chúng fail, phải cập nhật test hiện có, nhưng không mở rộng thành một bộ integration suite mới ngoài phạm vi.

## 14.1. Backend Unit Test bắt buộc

### GetSupportDepartments

Cover tối thiểu:

1. Active leader chưa tham gia:
   - `CanInviteParticipant = true`.
   - `CanReceiveLogistics = true`.
2. Leader `INVITED`:
   - invitation false.
   - logistics true.
3. Leader `ACCEPTED`:
   - invitation false.
   - logistics true.
4. Leader `ASSIGNED`:
   - invitation false.
   - logistics true.
5. Leader `DECLINED/REMOVED`:
   - invitation true.
   - logistics true.
6. Không có active leader:
   - cả hai false với reason đúng ngữ cảnh.
7. Department khác campus/inactive/non-GENERAL không được trả như candidate hợp lệ.

Dùng `[Theory]`/parameterized test cho nhóm status khi phù hợp, không copy-paste test thừa.

### GetParticipantCandidates — IC_SUPPORT

Cover:

1. `STAFF + STAFF` hợp lệ được trả về.
2. `STAFF + LEADER` hợp lệ được trả về.
3. Current Host bị loại dù subrole hợp lệ.
4. Active participant `INVITED/ACCEPTED/ASSIGNED` bị loại.
5. `DECLINED/REMOVED` có thể xuất hiện lại.
6. Khác campus/inactive/non-IC/IC inactive bị loại.

### InviteVisitParticipant

Cover:

1. Mời Staff Leader hợp lệ tạo/reuse participant `IC_SUPPORT` với `INVITED`.
2. Mời IC Staff hợp lệ vẫn hoạt động như cũ.
3. Không cho mời chính Host.
4. Duplicate active participant trả conflict.
5. Staff Leader khác campus/inactive/không thuộc IC ACTIVE bị từ chối.
6. Nếu Staff Leader có row `DECLINED/REMOVED`, flow re-invite giữ behavior hiện tại.

Không fake email success nếu handler contract yêu cầu mock service; assert state/call quan trọng và transaction outcome, không chỉ assert không throw.

### PrepareVisitLogistics regression

Có test chứng minh system logistic request vẫn được tạo khi active Department Leader đồng thời có participant status:

- `INVITED`
- `ACCEPTED`
- `ASSIGNED`

Và vẫn fail khi không có active leader hoặc department sai scope.

Nếu handler test phụ thuộc DB quá sâu và Unit Test infrastructure hiện tại không hỗ trợ đáng tin cậy, không dựng fake DbSet mong manh chỉ để pass. Hãy:

- Tận dụng test infrastructure/application context hiện có; hoặc
- Tách pure capability computation nhỏ để Unit Test, đồng thời giữ business validation trong handler; và
- Ghi rõ phần nào chưa thể chứng minh ở Unit level, không tuyên bố pass giả.

## 14.2. Frontend test bắt buộc

Dùng test framework đang thực sự tồn tại trong repository. Cover:

1. Department có:

```text
canInviteParticipant = false
canReceiveLogistics = true
```

thì:

- Bị disable ở invitation UI.
- Vẫn chọn được ở logistic UI.

2. Form system logistic mới tự điền đúng `plannedStartAt/plannedEndAt` của campus instance.
3. Không lệch giờ khi browser timezone không phải Việt Nam nếu test framework hỗ trợ timezone context.
4. Host có thể chỉnh giá trị default.
5. Re-render/refetch không ghi đè giá trị chưa submit mà Host đã chỉnh.
6. Existing logistics item dùng time đã lưu của item.
7. Reset form chưa lưu quay về planned time mặc định.
8. Staff Leader candidate hiển thị đúng nhãn.
9. Current Host không xuất hiện.

Nếu project chưa có component-test framework, dùng Playwright scenario nhỏ, ổn định và đúng flow; không thêm một test stack lớn chỉ cho task này nếu không cần.

## 14.3. Test quality rules

- Không sửa production code chỉ để test dễ pass nếu làm sai thiết kế.
- Không assert constant do test tự dựng rồi coi là nghiệp vụ đã được cover.
- Không mock chính method cần kiểm tra.
- Mỗi business rule quan trọng phải có assertion quan sát state/result thật.
- Không bỏ/skip test mới.
- Không đổi expected result của test cũ nếu test cũ đang phản ánh rule đúng; chỉ cập nhật khi contract nghiệp vụ đã thay đổi rõ ràng.
- Báo cáo đúng số test discovered/passed/failed/skipped.

---

## 15. Verification commands

Tự xác định solution/package scripts thật trước khi chạy. Tối thiểu:

```bash
dotnet build
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj
```

Frontend, từ `frontend/pems-react`:

```bash
npm run lint
npm run build
```

Và chạy targeted frontend test/Playwright test đã thêm hoặc bị ảnh hưởng.

Không bắt buộc chạy toàn bộ Integration Test nếu task không thêm Integration Test. Nếu có cập nhật existing Integration Test do contract thay đổi, chạy targeted suite đó và báo cáo riêng.

Không báo `pass` nếu command chưa chạy. Nếu môi trường thiếu dependency/DB/config, ghi chính xác command nào bị block và lỗi gì.

---

## 16. Acceptance Criteria

### AC-01 — Logistic độc lập với invitation

Given Department Leader đã là participant `INVITED`, `ACCEPTED` hoặc `ASSIGNED` của instance  
When Host mở dropdown chọn phòng ban xử lý logistic  
Then phòng ban vẫn có thể được chọn nếu department/leader vẫn active và đúng campus.

### AC-02 — Không mời participant trùng

Given Department Leader đã là active participant  
When Host mở phần “Phòng ban hỗ trợ”  
Then phòng ban bị disable cho lời mời và hiển thị reason phù hợp.

### AC-03 — Gửi logistic thành công

Given phòng ban cùng campus, `GENERAL + ACTIVE`, có active Department Leader đã tham gia đoàn  
When Host gửi system logistic request hợp lệ  
Then `visit_logistics_items` được tạo theo flow hiện tại và email/notification hướng đến leader theo behavior hiện có.

### AC-04 — Default thời gian đúng campus

Given campus instance có planned window  
When Host mở system logistic form mới  
Then start/end được điền bằng planned start/end của chính campus instance, theo Vietnam wall-clock.

### AC-05 — Default không phải khóa cứng

Given form đã được điền planned time  
When Host chỉnh start/end hợp lệ  
Then frontend giữ giá trị đã chỉnh và gửi đúng payload đó.

### AC-06 — Existing item không bị ghi đè

Given logistics item đã lưu có usage time khác planned window  
When Host xem lại item  
Then UI hiển thị usage time của item.

### AC-07 — Mời Staff Leader

Given một `STAFF + LEADER` active thuộc IC department active cùng campus và không phải Host  
When Host tìm IC Support  
Then Staff Leader xuất hiện và có thể được mời với participant role `IC_SUPPORT`.

### AC-08 — Không mời chính Host

Given current Host là Staff hoặc Staff Leader  
When tải candidate IC Support  
Then Host không xuất hiện; direct API call mời chính Host cũng bị backend từ chối.

### AC-09 — Không phá quyền Staff Leader

Given Staff Leader đã chấp nhận lời mời IC Support  
When truy cập các chức năng được phép của instance  
Then họ vẫn giữ scope Staff Leader và nhận participant-specific contribution permission theo accepted participant row, không bị mất quyền do relation precedence.

### AC-10 — Không đổi database

Given schema hiện tại đã có đủ field  
When hoàn thành task  
Then không có migration, patch SQL hoặc thay đổi fresh-create SQL.

---

## 17. Output/report format bắt buộc

Sau khi hoàn tất, trả báo cáo đúng cấu trúc:

# Completion Report

## 1. Git context và baseline

- Branch/ref đã làm.
- Working tree trước/sau.
- Baseline findings đã xác minh.

## 2. Root cause

- Vì sao invitation đã chặn nhầm logistic.
- Vì sao Staff Leader chưa xuất hiện.
- Vì sao logistic time đang rỗng.

## 3. Files changed

| Layer | File | Change |
|---|---|---|
| Backend API/Application | ... | ... |
| Frontend | ... | ... |
| Test | ... | ... |
| Database | Không đổi | Xác minh schema đủ |

## 4. Logic implemented

- Capability split.
- Time default.
- Staff Leader candidate/invite validation.
- Permission precedence audit.

## 5. Validation và authorization

- Host check.
- Campus/department scope.
- Role/subrole.
- Participant duplicate.
- Status window.
- Time validation.

## 6. Tests/build đã chạy

Ghi từng command và kết quả thật:

- Backend build.
- Unit Test: discovered/passed/failed/skipped.
- Architecture Test.
- Frontend lint/build.
- Targeted frontend test.
- Existing Integration Test nếu có cập nhật.

## 7. Database impact

- Xác nhận có/không schema change.

## 8. Remaining risks

- Chỉ ghi rủi ro thực tế còn lại.
- Không ghi “không có” nếu chưa chạy đủ verification.

---

## 18. Definition of Done

- [ ] Đã search/read toàn bộ caller của `GetSupportDepartments` và capability fields.
- [ ] Đã xác minh root cause trên branch hiện tại.
- [ ] Invitation và logistic dùng hai capability riêng.
- [ ] Leader active đã tham gia vẫn nhận được system logistic request.
- [ ] Invitation duplicate vẫn bị chặn.
- [ ] Không có participant check mới trong logistic handler.
- [ ] Default logistic time lấy đúng campus instance.
- [ ] Dùng Vietnam datetime-local helper, không qua UTC/toISOString.
- [ ] Default time vẫn chỉnh được và không ghi đè manual input.
- [ ] Existing item dùng time đã lưu.
- [ ] Candidate `IC_SUPPORT` gồm cả `STAFF + LEADER` và `STAFF + STAFF`.
- [ ] Current Host bị loại ở query và bị chặn ở command handler.
- [ ] Participant role vẫn là `IC_SUPPORT`.
- [ ] Đã audit Staff Leader relation/participant permission precedence.
- [ ] Frontend type khớp backend DTO.
- [ ] Không thêm role/status/table/column mới.
- [ ] Không sửa SQL/migration.
- [ ] Unit Test thật cover capability, candidate, invite và logistic regression.
- [ ] Frontend test cover dropdown split và time default.
- [ ] Không có test fake/skip.
- [ ] Backend build và Unit Test pass, hoặc báo cáo chính xác blocker.
- [ ] Frontend lint/build và targeted test pass, hoặc báo cáo chính xác blocker.
- [ ] Completion Report liệt kê đầy đủ file sửa và bằng chứng verification.
