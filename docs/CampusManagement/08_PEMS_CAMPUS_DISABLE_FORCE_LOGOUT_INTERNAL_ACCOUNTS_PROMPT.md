# PEMS — Tự đăng xuất tài khoản nội bộ khi Campus bị Disable

> **Mục đích:** Dùng trực tiếp file này làm prompt cho AI Agent đọc source và triển khai chức năng tự đăng xuất các tài khoản nội bộ thuộc campus ngay sau khi HO disable campus thành công.
>
> **Phạm vi:** Chỉ tập trung vào session revocation, Campus Access Gate, frontend forced logout, audit/security event và Unit Test liên quan. Không triển khai Integration Test trong task này.

---

# 1. Vai trò của AI Agent

Bạn là Senior .NET 8 Clean Architecture Developer, Authentication/Authorization Engineer, React TypeScript Engineer, MySQL Database-First Engineer, Security Reviewer và Unit Test Engineer.

Trước khi sửa, phải search và đọc source hiện tại. Không sửa theo suy đoán. Không tự bịa tên file, class, middleware, table, field, route, enum, session field hoặc error code. Nếu source đã có helper/service/middleware tương đương thì phải reuse.

---

# 2. Mục tiêu nghiệp vụ

Khi HO chuyển campus:

```text
ACTIVE → INACTIVE
```

hệ thống phải:

```text
1. Revoke toàn bộ session đang hoạt động của tài khoản nội bộ thuộc campus đó.
2. Chặn các tài khoản này tiếp tục login.
3. Chặn refresh token.
4. Chặn authenticated request tiếp theo dù access token JWT còn hạn.
5. Frontend tự xóa auth state và đưa user về trang đăng nhập.
6. Không thay đổi users.status.
7. Không thay đổi departments.status.
8. Không xóa user, department hoặc dữ liệu lịch sử.
9. Khi campus được enable lại, user phải đăng nhập lại bằng session mới.
```

---

# 3. Source và tài liệu bắt buộc phải đọc

## Backend

Search và đọc tối thiểu:

```text
CampusesController
ManageCampusStatus command/handler/validator/response
AuthenticationController
Login command/handler
Google SSO login handler
Refresh token handler
Session validation middleware
Current user middleware
JWT service
Session service/repository
UserSession entity
User entity
Role entity
Campus entity
EffectiveRole resolver
Exception/error handling middleware
Security event service
Audit log service/behaviour
```

## Frontend

Search và đọc:

```text
Auth context/store
Login page
API client/interceptor
Global error handler
Protected route
Logout flow
Token/session storage helper
Toast/notification system
i18n error mapping
```

## Database

Đọc SQL fresh-create mới nhất và xác nhận schema thật của:

```text
campuses
users
roles
user_sessions
security_events
audit_logs
audit_log_changes
```

Không tự bịa các field như `revoked_at`, `revoked_by`, `revoke_reason`, `is_revoked`, `status`. Phải dùng đúng field trong SQL/source.

## Tài liệu chuẩn

```text
PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
CLEAN_ARCHITECTURE.md
PROJECT_KNOWLEDGE.md
PROJECT_STRUCTURE_FULL.md
```

---

# 4. Tài khoản bị tự đăng xuất

Chỉ áp dụng với tài khoản có:

```text
users.primary_campus_id = campus vừa bị disable
```

và thuộc các nhóm:

| Effective Role | Runtime role/sub-role | Revoke |
|---|---|---:|
| Staff Leader | `STAFF + LEADER` | Có |
| IC Staff | `STAFF + STAFF` | Có |
| Department Leader | `DEPARTMENT + LEADER` | Có |
| Department Staff | `DEPARTMENT + STAFF` | Có |
| Student | `STUDENT + NULL` | Có |

Không dùng role code legacy như:

```text
STAFF_LEADER
DEPARTMENT_LEADER
DEPT
STAFF_L
STAFF_P
LEADER
```

PEMS xác định quyền bằng `role_code`, `sub_role` và `primary_campus_id`.

# 5. Tài khoản không bị ảnh hưởng

Không revoke hoặc chặn:

| Nhóm | Lý do |
|---|---|
| `HO` | Phải tiếp tục quản lý và enable lại campus |
| `ADMIN` | Quản trị toàn hệ thống |
| `VISITOR` | Không phải tài khoản nội bộ trực thuộc campus |
| User thuộc campus khác | Ngoài phạm vi campus bị disable |

Không được revoke mơ hồ tất cả user có `primary_campus_id`. Phải kiểm tra đồng thời campus scope và effective role.

---

# 6. Thời điểm revoke session

Chỉ revoke sau khi campus thực sự đủ điều kiện disable.

```text
1. HO gửi yêu cầu disable.
2. Backend xác thực HO hợp lệ.
3. Kiểm tra campus tồn tại và đang ACTIVE.
4. Kiểm tra toàn bộ business blocker của UC-86.
5. Nếu disable thất bại:
   - Không đổi campus status.
   - Không revoke session.
   - Không ghi audit success.
6. Nếu disable hợp lệ:
   - Chuyển campus sang INACTIVE.
   - Revoke session đúng phạm vi.
   - Ghi audit/security event.
   - Commit transaction.
```

**Disable thất bại thì không session nào bị revoke.**

---

# 7. Cơ chế revoke session

Backend phải đọc schema/source để dùng đúng cơ chế revoke hiện có.

Logic:

```text
Tìm tất cả session đang hoạt động
của STAFF, DEPARTMENT và STUDENT
có primary_campus_id = campus bị disable
→ đánh dấu revoked theo field thật của schema
→ lưu reason tương đương CAMPUS_DISABLED nếu source hỗ trợ
```

Không xóa cứng session nếu hệ thống dùng session history để audit/truy vết.

Không thay đổi:

```text
users.status
users.role_id
users.sub_role
users.primary_campus_id
users.department_id
departments.status
```

Tài khoản vẫn tồn tại; user chỉ bị chặn vì campus đang `INACTIVE`.

---

# 8. Revoke session không đủ — phải có Campus Access Gate

Access token JWT có thể còn hạn sau khi session DB bị revoke.

Bắt buộc có hai lớp:

```text
Lớp 1: Revoke toàn bộ session hiện tại.
Lớp 2: Campus Access Gate kiểm tra campus status ở request tiếp theo.
```

Không chấp nhận trường hợp chỉ revoke refresh token nhưng access token cũ vẫn dùng được đến khi hết hạn.

# 9. Campus Access Gate

Tạo hoặc mở rộng rule dùng chung theo pattern source hiện tại, về mặt ý nghĩa:

```text
CampusAccessRule
CampusAccessGate
CampusScopedUserAccessPolicy
```

Rule:

```text
Nếu user là STAFF, DEPARTMENT hoặc STUDENT
AND users.primary_campus_id thuộc campus INACTIVE
→ từ chối truy cập.
```

Gate phải đọc dữ liệu backend hiện tại. Không tin campus status trong JWT vì campus có thể bị disable sau lúc phát token.

# 10. Điểm bắt buộc áp dụng Gate

## Login credentials

```text
Nếu user thuộc campus INACTIVE
→ không tạo session
→ không phát access token
→ không phát refresh token
→ trả lỗi campus inactive
```

## Google SSO nội bộ

Sau khi map Google identity sang internal user:

```text
Nếu user thuộc campus INACTIVE
→ không tạo session
→ không phát token
→ trả lỗi campus inactive
```

Không áp dụng rule campus-scoped cho Visitor SSO.

## Refresh token

Trước khi cấp token mới:

```text
1. Session tồn tại.
2. Session chưa bị revoke.
3. User còn hợp lệ.
4. Campus của user vẫn ACTIVE.
```

Nếu campus `INACTIVE`:

```text
- Không cấp token mới.
- Revoke session nếu chưa revoke.
- Trả lỗi campus inactive.
```

## Mọi authenticated request

Middleware/authorization layer phải kiểm tra:

```text
Session hợp lệ
AND user hợp lệ
AND campus access hợp lệ
```

JWT còn hạn nhưng campus `INACTIVE` vẫn phải bị chặn ở request tiếp theo.

---

# 11. Error contract

Reuse error infrastructure hiện tại. Nếu chưa có mã tương đương, bổ sung theo convention dự án, ví dụ về mặt ý nghĩa:

```text
CAMPUS_INACTIVE_ACCESS_DENIED
```

VI:

```text
Cơ sở của tài khoản hiện đã ngừng hoạt động.
Vui lòng liên hệ Head Office để được hỗ trợ.
```

EN:

```text
Your campus is currently inactive.
Please contact Head Office for assistance.
```

Không trả chung chung `Unauthorized`, `Session expired` hoặc `Invalid account`.

## Phân biệt HTTP status

| Tình huống | HTTP đề xuất |
|---|---:|
| Token không hợp lệ/hết hạn | `401 Unauthorized` |
| Session không tồn tại hoặc revoked | Theo convention session hiện tại |
| Token hợp lệ nhưng campus INACTIVE | `403 Forbidden` |
| Login/refresh bị chặn do campus INACTIVE | `403 Forbidden` |

---

# 12. Frontend Forced Logout

Global error handler phải nhận diện:

```text
CAMPUS_INACTIVE_ACCESS_DENIED
```

và thực hiện:

```text
1. Xóa access token.
2. Xóa refresh token/session local nếu có.
3. Xóa authenticated user state.
4. Clear cache dữ liệu nhạy cảm.
5. Redirect về trang đăng nhập.
6. Hiển thị thông báo campus đã ngừng hoạt động.
```

Không chỉ hiện toast rồi giữ user ở dashboard.

Không cần polling campus status. Request tiếp theo bị backend chặn là đủ để trigger logout.

# 13. User đang mở hệ thống lúc HO disable campus

Ví dụ:

```text
10:00 User đang ở dashboard.
10:01 HO disable campus.
10:01 Backend revoke session và commit.
10:02 User gọi API tiếp theo.
```

Kết quả:

```text
Backend phát hiện session revoked hoặc campus INACTIVE
→ trả lỗi
→ frontend clear auth state
→ redirect login
```

Request bắt đầu trước khi transaction commit có thể hoàn thành. Mọi request mới sau commit phải bị chặn.

Không cần WebSocket/realtime push trong phase này, trừ khi source đã có cơ chế sẵn.

---

# 14. Khi Campus được Enable lại

Khi HO chuyển:

```text
INACTIVE → ACTIVE
```

hệ thống:

```text
- Cho phép tài khoản nội bộ đăng nhập lại.
- Không tự khôi phục session cũ.
- Không un-revoke refresh token/session cũ.
- Không tự đăng nhập user trở lại.
```

User phải đăng nhập lại để tạo session, access token và refresh token mới.

# 15. Quan hệ với Operational Availability

Campus được enable nhưng chưa có Staff Leader:

```text
STAFF/DEPARTMENT/STUDENT vẫn được đăng nhập
vì campus đã ACTIVE.
```

Campus vẫn chưa xuất hiện trên form đăng ký nếu chưa đủ:

```text
Campus ACTIVE
AND IC Department ACTIVE
AND Staff Leader ACTIVE hợp lệ
```

Hai rule độc lập:

```text
Campus status:
quyết định tài khoản nội bộ có được truy cập hệ thống.

Operational availability:
quyết định campus có nhận đăng ký tham quan mới.
```

---

# 16. Transaction và consistency

Nếu `campuses` và `user_sessions` cùng database, xử lý trong cùng transaction:

```text
Check disable conditions
→ Update campus INACTIVE
→ Revoke sessions
→ Audit/security event
→ Commit
```

Nếu revoke session lỗi thì rollback campus status.

Không để xảy ra:

```text
Campus INACTIVE nhưng session chưa revoke.
Session đã revoke nhưng campus vẫn ACTIVE.
```

Campus Access Gate vẫn phải tồn tại như lớp bảo vệ dự phòng.

---

# 17. Audit và Security Event

## Audit campus

```text
action = DISABLE_CAMPUS
entity = CAMPUS
entity_id = campusId
old_status = ACTIVE
new_status = INACTIVE
actor_user_id = current HO
occurred_at = current time
```

## Security event tổng hợp

Theo schema/convention hiện tại, về mặt ý nghĩa:

```text
event_type = CAMPUS_DISABLED_SESSIONS_REVOKED
campus_id
affected_user_count
revoked_session_count
actor_user_id
occurred_at
```

Không cần một event cho từng session nếu gây log quá lớn, trừ khi source bắt buộc.

Không log token, session secret, password, credential hoặc PII không cần thiết.

---

# 18. Unit Test backend bắt buộc

Task này chỉ yêu cầu Unit Test. Không triển khai Integration Test.

## Disable thành công

Test revoke active session của:

```text
Staff Leader
IC Staff
Department Leader
Department Staff
Student
```

Assert:

```text
Không đổi users.status.
Không đổi departments.status.
Không xóa user.
Không thay đổi role/sub-role.
Không xóa session nếu source dùng soft revoke.
```

## Không revoke sai đối tượng

```text
Không revoke HO.
Không revoke ADMIN.
Không revoke VISITOR.
Không revoke user campus khác.
Không xử lý lại session đã revoked.
```

## Disable thất bại

```text
Campus vẫn ACTIVE.
Không session nào bị revoke.
Không ghi audit success.
Không ghi security event success.
```

## Campus Access Rule

```text
STAFF thuộc campus INACTIVE → denied
DEPARTMENT thuộc campus INACTIVE → denied
STUDENT thuộc campus INACTIVE → denied
HO → allowed
ADMIN → allowed
VISITOR → allowed
User thuộc campus ACTIVE → allowed
```

## Login

```text
STAFF/DEPARTMENT/STUDENT thuộc campus INACTIVE:
- bị chặn
- không tạo session
- không phát token

HO/ADMIN/VISITOR:
- không bị Campus Access Gate chặn
```

## Refresh

```text
Campus INACTIVE:
- không cấp access token mới
- không cấp refresh token mới
- session giữ revoked
```

## Authenticated request

```text
JWT còn hạn nhưng campus INACTIVE → request bị chặn
Session đã revoked → request bị chặn
Campus ACTIVE → request tiếp tục pipeline
```

## Enable lại

```text
User có thể đăng nhập mới.
Session cũ vẫn revoked.
Không tự khôi phục token.
```

---

# 19. Frontend Test bắt buộc

Dùng framework hiện có.

```text
Global API nhận CAMPUS_INACTIVE_ACCESS_DENIED:
- clear access token
- clear refresh token/session local
- clear auth store/context
- clear sensitive cache
- redirect login
- hiển thị đúng message

Không logout sai khi gặp error code khác.
Không lặp redirect vô hạn.
Enable lại không tự phục hồi session cũ.
```

Không tạo hoặc chạy Integration Test.

---

# 20. Không được làm

```text
Không đổi users.status.
Không đổi departments.status.
Không xóa user.
Không xóa cứng session nếu source dùng soft revoke.
Không revoke HO/ADMIN/VISITOR.
Không revoke user campus khác.
Không chỉ revoke refresh token.
Không chỉ xử lý frontend.
Không tin campus status trong JWT.
Không polling liên tục nếu không cần.
Không WebSocket nếu chưa có yêu cầu.
Không un-revoke session khi enable.
Không tạo dynamic permission.
Không thêm bảng mới nếu schema đã đủ.
Không tạo Integration Test.
Không báo test pass nếu chưa chạy.
```

---

# 21. Phạm vi được sửa

```text
Manage Campus Status handler
Session repository/service
User session entity/config nếu source chưa map đủ field thật
Login handlers
SSO handlers
Refresh token handler
Session validation middleware
Campus access policy/helper/service
Error codes/messages
Audit/security event handling
Frontend API interceptor
Auth store/context
Protected route/logout flow
i18n error messages
Unit Tests
Frontend tests
```

# 22. Phạm vi không được sửa

```text
Visit approval architecture
Campus operational availability
Create Campus flow
Department status flow
Account status flow
OTP flow
Notification flow
Database schema ngoài nhu cầu thực tế
Deployment config
```

Issue ngoài scope chỉ báo cáo, không sửa âm thầm.

---

# 23. Quy trình triển khai bắt buộc

## Phase A — Audit hiện trạng

Báo cáo trước khi sửa:

```text
1. user_sessions schema thật.
2. JWT/session linking hiện tại.
3. SessionValidationMiddleware đang kiểm tra gì.
4. Login/SSO/refresh flow hiện tại.
5. ManageCampusStatus handler hiện tại.
6. Error handling hiện tại.
7. Audit/security event hiện tại.
8. Frontend auth state/interceptor hiện tại.
9. Gap so với prompt này.
10. Có cần SQL change không.
```

## Phase B — Kế hoạch thay đổi

```text
File sẽ sửa.
File sẽ thêm.
Rule dùng chung.
Error code.
Transaction boundary.
Unit Test.
Frontend test.
Database change: có/không.
```

## Phase C — Implement

Tuân thủ Clean Architecture và pattern source hiện tại.

## Phase D — Verification

Chạy:

```text
dotnet build
Unit Tests liên quan
Architecture Tests nếu thay middleware/layer/controller
Frontend typecheck/build
Frontend lint
Frontend tests
```

Không chạy Integration Test.

## Phase E — Final Report

Báo cáo evidence thật.

---

# 24. Output report bắt buộc

```text
A. Current-state findings
- Session model
- JWT/session validation
- Login/refresh/middleware behavior
- Frontend auth behavior

B. Implementation summary
- Session revocation
- Campus Access Gate
- Login/SSO/refresh blocking
- Authenticated request blocking
- Frontend forced logout
- Audit/security event

C. Files changed
- Mỗi file + lý do

D. Tests
- Unit Test backend
- Frontend test
- Passed/failed/skipped
- Commands đã chạy
- Xác nhận không triển khai Integration Test

E. Build/lint
- Backend
- Frontend
- Architecture test nếu có

F. Database
- Có/không schema change

G. Remaining risks
- Chỉ ghi issue thật còn lại
```

---

# 25. Business Rules chốt riêng

```text
BR-AUTH-CAMPUS-01:
Khi campus được disable thành công, hệ thống phải revoke toàn bộ session
đang hoạt động của STAFF, DEPARTMENT và STUDENT thuộc campus đó.

BR-AUTH-CAMPUS-02:
HO, ADMIN, VISITOR và user thuộc campus khác không bị revoke.

BR-AUTH-CAMPUS-03:
Disable campus không thay đổi users.status hoặc departments.status.

BR-AUTH-CAMPUS-04:
Chỉ revoke session sau khi toàn bộ điều kiện disable campus đã thành công.

BR-AUTH-CAMPUS-05:
Nếu disable thất bại, không session nào được revoke.

BR-AUTH-CAMPUS-06:
Campus Access Gate phải chặn login, SSO login, refresh token
và authenticated request của user thuộc campus INACTIVE.

BR-AUTH-CAMPUS-07:
Revoke session không thay thế Campus Access Gate.
Access token còn hạn vẫn phải bị chặn ở request tiếp theo.

BR-AUTH-CAMPUS-08:
Frontend phải clear auth state và redirect login khi nhận
CAMPUS_INACTIVE_ACCESS_DENIED.

BR-AUTH-CAMPUS-09:
Enable campus không khôi phục session đã revoke.
User phải đăng nhập lại để tạo session mới.

BR-AUTH-CAMPUS-10:
Campus ACTIVE nhưng chưa có Staff Leader vẫn cho phép
tài khoản nội bộ đăng nhập; Staff Leader chỉ quyết định
khả năng campus nhận đăng ký tham quan.
```

---

# 26. Definition of Done

```text
[ ] Disable thành công revoke đúng session STAFF/DEPARTMENT/STUDENT.
[ ] Không revoke HO/ADMIN/VISITOR/campus khác.
[ ] Disable thất bại không revoke session.
[ ] Không đổi users.status.
[ ] Không đổi departments.status.
[ ] Login credentials bị chặn khi campus INACTIVE.
[ ] SSO internal bị chặn khi campus INACTIVE.
[ ] Refresh bị chặn khi campus INACTIVE.
[ ] JWT còn hạn vẫn bị chặn ở request tiếp theo.
[ ] Frontend clear auth và redirect login.
[ ] Enable không un-revoke session cũ.
[ ] User phải đăng nhập lại.
[ ] Audit/security event đúng.
[ ] Unit Tests pass.
[ ] Frontend tests pass.
[ ] Backend build pass.
[ ] Frontend build/typecheck/lint pass.
[ ] Không có test skipped mới.
[ ] Không triển khai Integration Test.
[ ] Không thay schema ngoài scope.
```

---

# 27. Kết luận không được hiểu sai

```text
Disable campus thành công
→ campus INACTIVE
→ revoke session của STAFF, DEPARTMENT, STUDENT thuộc campus
→ chặn login, refresh và request tiếp theo
→ frontend clear auth và redirect login.

Không đổi users.status.
Không đổi departments.status.
Không ảnh hưởng HO, ADMIN, VISITOR hoặc campus khác.

Enable lại
→ cho phép đăng nhập mới
→ không khôi phục session cũ.
```
