# PEMS — Tổng hợp lỗi Authorization/Security hiện tại và phương án vá

> **Phạm vi:** Toàn bộ lỗi đã được xác nhận trong đợt audit Authentication / Authorization / Access Control gần nhất của PEMS, cộng với các business rule đã được chốt trực tiếp.
>
> **Repository:** `quangthoai04/PEMS`  
> **Branch:** `Dev`  
> **Commit audit:** `ceeb4e3b022a5afc350cce9444e9386104488c6e`  
> **Ngày tổng hợp:** 17/08/2026  
> **Trạng thái:** **CHƯA NÊN RELEASE PRODUCTION trước khi xử lý nhóm P0.**

---

## 1. Nguyên tắc vá chung

Backend phải kiểm tra đầy đủ:

```text
Authentication
    + Role / Effective Role
    + Resource/Object ownership
    + Campus / Department scope
    + Relationship với đúng Visit/Minute/Partner
    + Workflow state
    = ALLOW
```

Nguyên tắc quan trọng:

```text
Scope != Permission
```

Ví dụ Student cùng campus với một tài khoản khác không đồng nghĩa Student có quyền xem Account Management của campus đó.

Frontend route guard chỉ phục vụ UX. **Backend phải là nơi quyết định quyền cuối cùng**, vì API có thể bị gọi trực tiếp bằng Postman/cURL/DevTools.

---

# 2. Business rule đã chốt

## 2.1. Upload ảnh đoàn

Được phép khi:

```text
IsHost(user, visitInstance)
OR IsAcceptedParticipant(user, visitInstance)
```

Tức là:
- Host của đúng Visit Instance → ALLOW.
- Người tham gia tiếp đón đúng Visit Instance và đã `ACCEPTED` → ALLOW.
- Staff/Staff Leader chỉ cùng campus → DENY.
- Participant chưa ACCEPTED → DENY.
- Participant của Visit khác → DENY.
- Admin không có relationship → DENY.

Student **có thể upload ảnh** nếu là Accepted Participant của đúng Visit.

---

## 2.2. Face Scan / Face Tag

Chỉ được phép khi:

```text
(EffectiveRole == STAFF OR EffectiveRole == STAFF_LEADER)
AND IsHost(user, visitInstance)
```

Do đó:
- Staff + Host đúng Visit → ALLOW.
- Staff Leader + Host đúng Visit → ALLOW.
- Staff Accepted Participant nhưng không Host → DENY.
- Staff Leader chỉ cùng campus → DENY.
- Student Accepted Participant → DENY.
- Admin → DENY.

**Student không được Face Scan.**

---

## 2.3. Upload Visit Document

Chỉ:

```text
IsHost(user, visitInstance)
```

mới được upload tài liệu của chuyến.

Accepted Participant nhưng không phải Host vẫn bị DENY.

---

## 2.4. ADMIN và Partner

ADMIN **không có quyền nghiệp vụ nội bộ liên quan tới Partner**.

ADMIN không được:
- xem Partner Management nội bộ;
- xem Partner pending/private;
- create/edit/approve/reject Partner;
- link/unlink Visit–Partner;
- xem/quản lý Visit–Partner relationship nội bộ.

Tuy nhiên ADMIN **vẫn được xem Partner công khai trên Homepage giống người dùng bình thường**.

Public Partner phải dựa trên dữ liệu được phép công khai, ví dụ:

```text
APPROVED + PUBLIC
```

và không phụ thuộc quyền Partner Management.

---

# 3. Tổng quan bug hiện tại

| ID | Bug | Severity | Priority |
|---|---|---|---|
| SEC-01 | UpdateAccountRole privilege escalation | CRITICAL | P0 |
| SEC-02 | Account Detail lộ PII do thiếu permission gate | CRITICAL | P0 |
| SEC-03 | Account List/Search lộ PII do chỉ scope campus | CRITICAL | P0 |
| SEC-04 | Account Statistics thiếu Account Management gate | MEDIUM | P2 |
| SEC-05 | Legacy Department Search Personnel IDOR/PII leak | CRITICAL | P0 |
| SEC-06 | Legacy Department Personnel Detail IDOR/PII leak | CRITICAL | P0 |
| SEC-07 | Legacy Update Department Personnel unauthorized write | CRITICAL | P0 |
| SEC-08 | Legacy Remove Personnel unauthorized status change | CRITICAL | P0 |
| SEC-09 | Legacy Reassign Department Lead privilege escalation | CRITICAL | P0 |
| SEC-10 | Minutes PDF/Excel Export IDOR | CRITICAL | P0 |
| SEC-11 | Minutes Search/Read scope quá rộng | CRITICAL | P0 |
| SEC-12 | Submitted Visit multi-campus sibling data leak | HIGH | P1 |
| SEC-13 | Assign Department Staff ownership gap | HIGH | P1 |
| SEC-14 | Visit Photo upload vào Visit không liên quan | CRITICAL | P0 |
| SEC-15 | Visit Photo metadata cross-campus qua broad scope | MEDIUM | P2 |
| SEC-16 | Face Scan dùng media scope quá rộng | HIGH | P1 |
| SEC-17 | Visit Document upload kế thừa media scope quá rộng | HIGH | P1 |
| SEC-18 | Admin lọt vào Visit–Partner scope không đúng policy | MEDIUM | P1 |
| SEC-19 | Notification unauthorized/not-found biến thành HTTP 500 | MEDIUM | P2 |
| SEC-20 | Raw exception message bị trả ra client | MEDIUM | P2 |
| SEC-21 | Legacy AssignTasks route tồn tại nhưng handler NotImplemented | MEDIUM | P2 |

---

# 4. SEC-01 — UpdateAccountRole privilege escalation

**Severity:** CRITICAL / P0  
**Component:** `AccountsController`, `UpdateAccountRoleCommandHandler`, `AccountProvisioningRules`

## Hiện trạng

Action cập nhật role chưa fail-closed theo actor trước khi đi vào nhánh xử lý requested role. Handler có logic riêng cho Staff Leader nhưng generic branch vẫn có thể xử lý role shape và ghi:

- `RoleId`
- `SubRole`
- `DepartmentId`
- `PrimaryCampusId`

Nếu actor không thuộc nhóm quản lý tài khoản mà vẫn gọi trực tiếp endpoint, backend không được dựa vào frontend để chặn.

## Rủi ro

Privilege escalation: user thường có khả năng thử thay đổi quyền của bản thân hoặc target khác.

## Phương án vá

Chuẩn hóa policy:

```text
AccountManagementAccess
```

Trước mọi mutation:

```text
if (!CanManageAccounts(currentUser))
    DENY;
```

Sau đó mới kiểm tra:

```text
CanActorAssignTargetRole(actor, requestedRole, requestedSubRole, target)
```

Bắt buộc:
1. actor thuộc role được quản lý account;
2. actor có scope với target;
3. actor chỉ assign được role/subrole policy cho phép;
4. cấm self-escalation;
5. không lấy authority của actor từ request body;
6. re-read actor/target từ DB trước mutation nếu cần;
7. revoke session target sau role change.

## Test bắt buộc

```text
Student -> update role bản thân lên STAFF              => DENY
Visitor -> update target khác                           => DENY
Department Staff -> update role                         => DENY
Allowed manager + wrong campus target                   => DENY
Allowed manager + invalid target role                   => DENY
Allowed manager + valid target trong scope              => ALLOW
Sau role change, session cũ của target                  => REVOKED/DENY
```

---

# 5. SEC-02 — Account Detail lộ PII

**Severity:** CRITICAL / P0

`ViewAccountDetailsQueryHandler` có các nhánh đặc quyền nhưng role khác có thể rơi xuống logic same-campus thay vì trước tiên yêu cầu Account Management permission.

Response có thể chứa:
- email;
- phone;
- gender;
- nationality;
- student code;
- role/status;
- last login;
- authentication provider.

## Vá

Thứ tự phải là:

```text
1. Require CanAccessAccountManagement(actor)
2. Xác định global/campus scope
3. Check target nằm trong scope
4. Sau đó mới project dữ liệu
```

Không query/project PII rồi mới check authorization.

---

# 6. SEC-03 — Account List/Search lộ PII

**Severity:** CRITICAL / P0  
**Component:** `AccountListQueryExecutor`, `SearchandFilterAccountsQueryHandler`

Hiện executor có thể scope role không đặc quyền theo campus nhưng chưa yêu cầu actor có quyền Account Management.

## Vá

Không tồn tại nhánh mặc định kiểu “other campus-scoped roles” cho Account Management.

```text
if (!CanAccessAccountManagement(actor))
    DENY;

query = ApplyAccountManagementScope(query, actor);
```

Frontend `/dashboard/accounts` tiếp tục guard nhưng chỉ là lớp UX.

---

# 7. SEC-04 — Account Statistics thiếu permission gate

**Severity:** MEDIUM / P2

Statistics có pattern tương tự List/Search.

## Vá

Dùng cùng `AccountManagementAccess` như List/Detail/Search; không tạo policy riêng.

---

# 8. Legacy `/api/Departments`

PEMS hiện có song song:

```text
/api/department-leader
```

và legacy:

```text
/api/Departments
```

API mới có scope tốt hơn, trong khi nhiều endpoint personnel legacy chỉ yêu cầu authenticated.

## Chiến lược

**Ưu tiên retire/disable legacy personnel endpoints** và chuyển consumer sang API mới.

Nếu bắt buộc giữ compatibility, legacy phải reuse canonical authorization service của API mới; không duy trì hai implementation quyền khác nhau.

---

# 9. SEC-05 — Search Personnel IDOR / PII leak

**Severity:** CRITICAL / P0

`SearchPersonnelQueryHandler` dùng `DepartmentId` từ client để query nhưng không có current-user authorization tương ứng.

Có thể trả Name, Email, Phone, Gender, Campus, SystemRole, Status, Avatar.

## Vá

Ưu tiên:

```text
Retire legacy endpoint
```

Nếu chưa thể:

```text
Require actor role
+ derive/validate actor department/campus từ DB
+ validate target department scope
+ sau đó mới query
```

`DepartmentId` do client truyền chỉ là target selector, không phải bằng chứng quyền.

---

# 10. SEC-06 — View Personnel Detail IDOR

**Severity:** CRITICAL / P0

Không được chỉ kiểm tra `UserId + DepartmentId` tồn tại.

## Vá

Phải check:

```text
CanViewDepartmentPersonnel(actor, target)
```

bằng canonical `DepartmentPersonnelAccess`.

---

# 11. SEC-07 — Update Department Personnel unauthorized write

**Severity:** CRITICAL / P0

Target có thể được tìm bằng `UserId + DepartmentId` rồi sửa FullName/Phone/Gender mà thiếu actor authorization đầy đủ.

## Vá

```text
Require CanManageDepartmentPersonnel(actor, targetDepartment)
AND CanModifyTarget(actor, target)
```

Bổ sung audit actor/target/before/after.

---

# 12. SEC-08 — Remove Personnel unauthorized status change

**Severity:** CRITICAL / P0

Có một số guard như self/current head nhưng thiếu gate caller thực sự có quyền remove personnel.

## Vá

```text
Require canonical department-management access
Require exact department/campus scope
Prevent self/current leader theo business rule
Then mutate status
```

Không tin `DepartmentId` request như authority.

---

# 13. SEC-09 — Reassign Department Lead privilege escalation

**Severity:** CRITICAL / P0

Handler nhận `DepartmentId` và `NewLeaderUserId`, có thể:
- demote old leader;
- set target `SubRole = LEADER`;
- set `HeadUserId`;

nhưng thiếu actor authorization đầy đủ.

## Vá

```text
Require role được phép reassign leader
AND actor scope đúng campus/department
AND new leader active
AND new leader thuộc đúng department
AND target role shape hợp lệ
```

Không để Department Staff tự đưa mình thành Leader bằng cách thay `NewLeaderUserId`.

---

# 14. SEC-10 — Minutes PDF/Excel Export IDOR

**Severity:** CRITICAL / P0

Export load Minute theo ID nhưng authorization không reuse đầy đủ relationship policy như các luồng Minute mạnh hơn.

## Rủi ro

Đổi `minutesId` có thể thử truy cập:
- nội dung biên bản;
- participants;
- action items;
- dữ liệu chuyến.

## Vá

Tạo một canonical authorization/predicate duy nhất:

```text
MinuteAccess
```

và bắt buộc dùng cho:

```text
Detail
Search
PDF Export
Excel Export
Edit
Save/Lock
```

Khuyến nghị:

```text
AuthorizedMinutesFor(actor)
    .Where(x => x.Id == requestedId)
```

Authorization nên được đưa vào query SQL thay vì load object toàn cục rồi check thiếu sau đó.

---

# 15. SEC-11 — Minutes Search/Read scope quá rộng

**Severity:** CRITICAL / P0

Search thiên về campus scope thay vì exact relationship.

## Vá

Search phải reuse:

```text
AuthorizedMinutesFor(actor)
```

Không viết một authorization rule riêng cho Search.

---

# 16. SEC-12 — Submitted Visit multi-campus sibling data leak

**Severity:** HIGH / P1

Participant có relationship với một campus instance có thể rơi vào logic trả sibling campus instances của cùng Visit Request.

Có thể lộ:
- host;
- operational contact;
- phone/email;
- status/decision/reason;
- timing/cancellation.

## Vá

Với user không có global request-level permission:

```text
visibleInstances = only instances where actor has exact relationship
```

Không dùng:

```text
actor liên quan 1 instance => trả toàn bộ CampusInstances
```

---

# 17. SEC-13 — Assign Department Staff ownership gap

**Severity:** HIGH / P1

Handler kiểm tra Department Leader/same department nhưng row participant dùng làm authority chưa bị buộc chắc chắn thuộc current user.

## Vá

Tốt nhất derive authority server-side:

```text
currentUser.UserId
+ target VisitInstance
+ participant relationship
+ Department Leader role
```

Nếu vẫn nhận `participantId`, bắt buộc:

```text
participant.UserId == currentUser.UserId
AND participant belongs target VisitInstance
AND participant department == currentUser department
AND lifecycle/state hợp lệ
```

---

# 18. Thiết kế lại Visit Media Authorization

Không tiếp tục dùng một broad helper kiểu:

```text
VisitInstanceMediaAccessScope
```

cho mọi mutation.

Nên tách:

```text
VisitPhotoAccess
  - CanView
  - CanUpload
  - CanDelete

VisitFaceScanAccess
  - CanScan
  - CanTag

VisitDocumentAccess
  - CanView
  - CanUpload
  - CanDelete
```

Ba capability có business rule khác nhau.

---

# 19. SEC-14 — Visit Photo upload unrelated Visit

**Severity:** CRITICAL / P0

`VisitPhotoStudentScope.ResolveAcceptedStudentAsync` có broad allow theo role trước khi xác minh exact relationship.

## Rule đúng

```text
IsHost(currentUserId, visitInstanceId)
OR Exists AcceptedParticipant(currentUserId, visitInstanceId)
```

Không allow chỉ vì:

```text
role == STAFF
role == ADMIN
same campus
```

## DB trigger

Nếu DB trigger hiện mirror broad rule cũ, phải cập nhật trigger/migration đồng bộ. Không để app và DB hiểu quyền khác nhau.

---

# 20. SEC-15 — Visit Photo metadata cross-campus

**Severity:** MEDIUM / P2

Broad media view scope có thể cho Staff Leader vào target không đúng exact scope/relationship.

## Vá

Tạo `CanViewVisitPhoto` riêng. Không suy ra quyền View từ Upload hoặc Face Scan.

**Không phá generic file-download authorization hiện có.**

---

# 21. SEC-16 — Face Scan authorization quá rộng

**Severity:** HIGH / P1

## Rule đúng

```text
(Staff OR StaffLeader)
AND IsHost(currentUser, targetVisitInstance)
```

## Vá

`StartFaceScanCommandHandler` và toàn bộ Face Tag/Face processing mutation phải dùng `VisitFaceScanAccess`.

Không reuse:
- `CanUploadPhoto`;
- `CanViewMedia`.

Student Accepted có thể upload Photo nhưng **không được Scan**.

## Test

```text
Staff Host đúng Visit                  => ALLOW
StaffLeader Host đúng Visit            => ALLOW
Staff Accepted nhưng không Host        => DENY
Staff cùng campus không Host           => DENY
Student Accepted                       => DENY
Admin                                  => DENY
Host của Visit khác                    => DENY
```

---

# 22. SEC-17 — Visit Document upload scope quá rộng

**Severity:** HIGH / P1

## Rule đúng

```text
Only IsHost(currentUser, targetVisitInstance)
```

## Vá

`UploadVisitDocumentCommandHandler` phải gọi `VisitDocumentAccess.CanUpload()` riêng.

Accepted Participant, Student, Staff chỉ cùng campus, Staff Leader chỉ cùng campus và Admin đều không được mặc định upload.

---

# 23. SEC-18 — Admin lọt vào Visit–Partner access

**Severity:** MEDIUM / P1

Canonical Partner policy và `VisitLinkSupport` đang không nhất quán.

## Rule đúng

```text
ADMIN -> Homepage public Partner             => ALLOW
ADMIN -> Public approved Partner detail      => ALLOW
ADMIN -> Internal Partner Management         => DENY
ADMIN -> Pending/Private Partner             => DENY
ADMIN -> Visit–Partner relationship          => DENY
ADMIN -> Link/Unlink                         => DENY
ADMIN -> Approve/Reject                      => DENY
```

## Vá

1. `VisitLinkSupport` align/reuse `PartnerAccess`.
2. Bỏ mọi hard-code `Admin => Allow` trong internal Partner flow.
3. Public Partner endpoint tách khỏi internal Partner authorization.
4. Public query chỉ trả dữ liệu thực sự public/approved.

---

# 24. SEC-19 — Notification unauthorized/not-found trả 500

**Severity:** MEDIUM / P2

Notification ownership nhìn chung có recipient scope, nhưng expected not-found/unauthorized branch dùng generic `Exception`.

## Vá

Dùng:
- `NotFoundException`;
- `ForbiddenException`;
- `ConflictException`;
- `BusinessException`.

Expected authorization/business failure không được thành HTTP 500.

---

# 25. SEC-20 — Raw exception message leakage

**Severity:** MEDIUM / P2

Legacy Reassign Department Lead có pattern kiểu:

```text
"Lỗi khi đổi trưởng phòng: " + ex.Message
```

## Vá

Client chỉ nhận safe error code/message.

Server log giữ:
- exception;
- stack trace;
- correlation ID;
- actor ID;
- resource ID.

Không trả internal SQL/provider/stack detail ra client.

---

# 26. SEC-21 — AssignTasks NotImplemented

**Severity:** MEDIUM / P2

Route tồn tại nhưng handler `throw new NotImplementedException(...)`.

## Vá

Chọn một:
1. implement đầy đủ authorization + nghiệp vụ; hoặc
2. disable/remove endpoint khỏi production.

Không để endpoint callable rồi trả 500.

---

# 27. Những phần đang làm đúng — không nên phá khi refactor

## Authentication

- JWT validation issuer/audience/lifetime/signing key.
- Fallback policy yêu cầu authenticated mặc định.
- Public action opt-in `[AllowAnonymous]`.
- Session validation re-check user/role/department/campus state từ DB.
- Refresh token rotation và re-check DB state.
- Logout revoke session/refresh session.
- BCrypt work factor hiện dùng mức 12.
- Credentials login có anti-enumeration/lockout/state checks.
- Google login xác minh provider và DB role/account; account Google mới không tự nhận privileged role.

## Profile

Update Profile đi theo field allow-list và chặn field nhạy cảm như role/status/department/campus/password. Giữ nguyên anti-mass-assignment này.

## Generic file download

`/api/files/{id}/content` có access authorization trước storage read. Khi sửa Visit Photo/Document không được bypass lớp này.

## Invitation Accept/Decline

Luồng này có exact `participant.UserId == actorUserId` và state/lifecycle check tương đối tốt; có thể dùng làm mẫu cho relationship authorization.

---

# 28. Kiến trúc authorization đề xuất

## Canonical services

```text
AccountManagementAccess
DepartmentPersonnelAccess
MinuteAccess
VisitRequestAccess
VisitParticipantAccess
VisitPhotoAccess
VisitFaceScanAccess
VisitDocumentAccess
PartnerAccess
```

## Flow

```text
[Authorize]
    ↓
Command/Query Handler
    ↓
Authorization Service
    ↓
Load only authorized target from DB
    ↓
Business State Validation
    ↓
Mutation / Projection
```

## Fail closed

Sai:

```text
if Admin -> allow
else if HO -> ...
else -> same campus
```

Đúng:

```text
if actor belongs explicit allowed capability:
    evaluate scope
else:
    DENY
```

## Không tin authority từ request

Các field:

```text
DepartmentId
CampusId
UserId
ParticipantId
RoleCode
SubRole
```

chỉ là target selector.

Actor identity/scope phải lấy từ:

```text
ICurrentUserService
+ current DB state
```

---

# 29. Kế hoạch triển khai vá

## Phase 0 — Tạo regression tests trước

Tạo exploit tests để fail trên code cũ:

```text
Student -> UpdateAccountRole
Department Staff -> Reassign self to Leader
Random authenticated -> Remove other department personnel
Visitor/null-campus -> Export guessed Minute ID
Staff -> Upload photo to unrelated VisitInstanceId
StaffLeader -> Face Scan Visit mà mình không Host
Student Accepted -> Face Scan
Accepted participant non-host -> Upload Visit Document
Admin -> internal Visit–Partner APIs
```

---

## Phase 1 — P0 blockers

Thứ tự:
1. Fix `UpdateAccountRole`.
2. Disable/migrate unsafe legacy Department personnel APIs.
3. Fix Account Detail/List/Search.
4. Fix Minutes PDF/Excel Export.
5. Fix Minutes Search/Read.
6. Fix Visit Photo Upload exact relationship.

**Không release production trước khi Phase 1 pass integration tests.**

---

## Phase 2 — Visit relationship

1. Fix multi-campus sibling leak.
2. Fix assign-department-staff ownership.
3. Tách Photo / Face Scan / Visit Document policy.
4. Update DB trigger/migration liên quan nếu có.

---

## Phase 3 — Partner

1. Remove Admin internal business access khỏi `VisitLinkSupport`.
2. Reuse canonical `PartnerAccess`.
3. Verify ADMIN vẫn xem Public Partner Homepage.
4. Verify Pending/Private data không leak qua public endpoint.

---

## Phase 4 — Error/reliability

1. Replace generic exceptions.
2. Remove raw `ex.Message`.
3. Disable/implement NotImplemented route.
4. Chuẩn hóa status code.

---

# 30. HTTP response policy

| Tình huống | Response |
|---|---:|
| Chưa đăng nhập | 401 |
| Đăng nhập nhưng capability không được phép | 403 |
| IDOR-sensitive resource không muốn lộ tồn tại | 404 |
| Resource/state conflict | 409 |
| Business validation fail | 422 |
| Request validation fail | 400 |
| Unhandled server error | 500 generic |

---

# 31. Authorization test matrix bắt buộc

Mỗi sensitive endpoint phải test:

```text
1. Unauthenticated
2. Wrong role — same campus
3. Wrong role — same department
4. Allowed role — wrong campus
5. Allowed role — wrong department
6. Allowed role — unrelated resource
7. Exact owner/host/participant
8. Target ID tampering
9. Self vs other target
10. Invalid workflow state
11. Stale/revoked session
12. Concurrency/race nếu mutation quan trọng
```

Bao phủ 8 effective roles:

```text
ADMIN
HO
STAFF_LEADER
STAFF
DEPARTMENT_LEAD
DEPARTMENT
STUDENT
VISITOR
```

---

# 32. Test Media theo rule mới

## PHOTO

```text
Host đúng Visit                         -> 200
Student ACCEPTED đúng Visit             -> 200
Department participant ACCEPTED         -> 200
Staff ACCEPTED đúng Visit               -> 200
Participant PENDING                     -> DENY
Accepted participant của Visit khác     -> DENY
Staff cùng campus không relationship    -> DENY
StaffLeader cùng campus không relation  -> DENY
Admin                                   -> DENY
```

## FACE SCAN

```text
Staff + Host đúng Visit                 -> 200
StaffLeader + Host đúng Visit           -> 200
Staff + Accepted nhưng không Host       -> DENY
Staff cùng campus nhưng không Host      -> DENY
Student + Accepted                      -> DENY
Department Staff + Accepted             -> DENY
Admin                                   -> DENY
Host của Visit khác                     -> DENY
```

## VISIT DOCUMENT

```text
Host đúng Visit                         -> 200
Accepted Participant không Host         -> DENY
Student Accepted                        -> DENY
Staff cùng campus                       -> DENY
StaffLeader cùng campus                 -> DENY
Admin                                   -> DENY
Host của Visit khác                     -> DENY
```

---

# 33. Test Partner / ADMIN

```text
ADMIN -> Homepage public partner                  => ALLOW
ADMIN -> Public approved partner detail           => ALLOW
ADMIN -> Internal Partner list/management         => DENY
ADMIN -> Pending Partner                          => DENY
ADMIN -> Private Partner                          => DENY
ADMIN -> Visit–Partner relationship               => DENY
ADMIN -> Link/Unlink                              => DENY
ADMIN -> Approve/Reject                           => DENY
```

Phải test thêm Anonymous/Visitor để bảo đảm fix Admin không làm hỏng Homepage.

---

# 34. Release gate

## Backend

```bash
dotnet test
```

Yêu cầu:
- Unit tests pass.
- Integration tests pass.
- Negative authorization tests pass.
- Không còn sensitive endpoint chỉ có `[Authorize]` nhưng thiếu object/scope/relationship check.

## Frontend

```bash
npm run lint
npm run test:unit
npm run build
```

Nếu có E2E:

```bash
npm run e2e
```

Frontend hide/show không thay thế backend authorization.

---

# 35. Runtime/Postman verification

Nên lưu evidence:

```text
BEFORE FIX
Token role thấp + thay ID/request body
-> API trả 200/mutation hoặc data ngoài quyền

AFTER FIX
Cùng request
-> 403/404
-> DB không thay đổi
-> Audit log có actor/action/result
```

Ưu tiên demo:
1. `UpdateAccountRole`.
2. `reassigndepartmentlead`.
3. `removepersonnel`.
4. Account Search/Detail.
5. Minutes PDF/Excel guessed ID.
6. Visit Photo cross-visit upload.
7. Face Scan non-host.
8. Visit Document non-host.
9. Admin gọi internal Partner endpoint.

---

# 36. Các mục deployment cần verify riêng

Các mục này **chưa được coi là bug code chỉ dựa vào source audit**:

- JWT signing secret production có đủ mạnh không;
- secret rotation;
- production DB đã chạy đủ migration/trigger chưa;
- Google Drive/storage ACL thực tế;
- CORS production;
- HTTPS/reverse proxy headers;
- production logs có ghi token/PII không;
- DB account có excessive privileges không;
- Google provider/client production config;
- rate limiting khi chạy multi-instance;
- Production có vô tình bật Development exception details không.

Cần checklist staging/production riêng.

---

# 37. Definition of Done

- [ ] SEC-01 đến SEC-11 P0 đã đóng.
- [ ] Không còn unsafe legacy Department personnel route hoặc tất cả đã reuse canonical policy.
- [ ] Account List/Detail/Search require Account Management permission trước scope.
- [ ] Minutes Search/Detail/PDF/Excel dùng cùng canonical authorization.
- [ ] Photo Upload chỉ Host hoặc Accepted Participant đúng Visit.
- [ ] Face Scan chỉ Staff/Staff Leader **và là Host đúng Visit**.
- [ ] Visit Document Upload chỉ Host đúng Visit.
- [ ] Student Accepted upload được Photo nhưng không Scan/Document.
- [ ] ADMIN không có internal Partner permission.
- [ ] ADMIN vẫn xem được Partner public trên Homepage.
- [ ] Không còn broad `Admin/Staff => allow` trong media mutation.
- [ ] Không còn generic exception cho expected permission/not-found flow.
- [ ] Không trả raw internal exception ra client.
- [ ] Negative authorization tests pass cho 8 effective roles.
- [ ] Object ID tampering tests pass.
- [ ] Wrong campus/department/visit tests pass.
- [ ] `dotnet test` pass.
- [ ] Frontend lint/unit/build pass.
- [ ] Runtime/Postman smoke test chứng minh trước/sau fix.

---

# 38. Thứ tự fix cuối cùng

```text
P0-1  UpdateAccountRole
P0-2  Legacy /api/Departments personnel APIs
P0-3  Account List / Detail / Search
P0-4  Minutes Export PDF / Excel
P0-5  Minutes Search / Read authorization
P0-6  Visit Photo Upload exact relationship

P1-1  Face Scan: Staff/StaffLeader + Host only
P1-2  Visit Document: Host only
P1-3  Assign Department Staff ownership
P1-4  Multi-campus sibling Visit data
P1-5  Remove Admin from internal Partner relationship

P2-1  Account Statistics
P2-2  Photo metadata/view scope
P2-3  Notification exception contract
P2-4  Raw exception leakage
P2-5  NotImplemented legacy route
```

**Không bắt đầu bằng việc chỉ hide/show button ở frontend. P0 phải được vá ở backend trước.**

---

# 39. Kết luận

Snapshot `Dev` hiện có nhiều lớp Authentication tốt nhưng Authorization chưa đồng nhất giữa các module.

Các nguyên nhân chính:
1. chỉ kiểm tra authenticated nhưng thiếu capability;
2. nhầm scope với permission;
3. tin ID từ client mà không xác minh relationship;
4. helper access quá rộng được reuse cho nhiều nghiệp vụ khác rule;
5. legacy API và API mới có mức bảo vệ khác nhau;
6. role policy không nhất quán giữa module.

Hướng xử lý chuẩn là:

```text
Centralize authorization theo từng resource/capability
+ Fail closed
+ Check exact relationship tại backend
+ Apply authorization vào query nếu có thể
+ Negative integration tests
+ Runtime evidence trước/sau fix
```

Đây là điều kiện cần trước khi coi PEMS đủ an toàn về Authorization/Access Control cho production.
