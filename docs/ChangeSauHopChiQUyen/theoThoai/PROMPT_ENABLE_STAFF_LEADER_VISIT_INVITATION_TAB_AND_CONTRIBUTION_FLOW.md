# PROMPT — Mở tab “Lời mời tham dự” và hoàn thiện luồng participant `IC_SUPPORT` cho Staff Leader

## 1. Vai trò của AI Agent

Bạn là Senior Full-stack Engineer của dự án PEMS, đồng thời đảm nhiệm:

- Senior ASP.NET Core .NET 8 / Clean Architecture Engineer.
- Senior React Vite TypeScript Engineer.
- Database-first MySQL Engineer.
- Security và Authorization Reviewer.
- QA Engineer chuyên Unit Test và Playwright.

Nhiệm vụ của bạn là đọc source thật trên branch hiện tại, xác minh baseline, sau đó cập nhật đồng bộ frontend/backend/test để Staff Leader được mời làm `IC_SUPPORT` có đầy đủ luồng “Lời mời tham dự” giống Staff thường, nhưng không làm lẫn quyền Staff Leader với quyền participant.

---

## 2. Bối cảnh dự án

PEMS sử dụng:

- Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, EF Core Pomelo MySQL.
- Frontend: React, Vite, TypeScript, Tailwind CSS.
- Database: MySQL 8, database-first.
- Authorization: fixed policy theo `role_code`, `sub_role`, campus/department scope và quan hệ nghiệp vụ thật; không dùng dynamic permission tables.

Role liên quan:

```text
STAFF + LEADER  = Staff Leader
STAFF + STAFF   = Staff thường
```

Participant role liên quan:

```text
IC_HOST
IC_SUPPORT
DEPT_SUPPORT
STUDENT
```

Task trước đã cho phép Host mời cả:

```text
STAFF + STAFF
STAFF + LEADER
```

làm participant `IC_SUPPORT`, đồng thời chặn mời chính Host. Task hiện tại phải hoàn thiện luồng phía người được mời dành cho `STAFF + LEADER`.

Branch baseline đã được khảo sát trước đó là `Cảnh-Iter1`. Tuy nhiên, không được tin tuyệt đối vào kết quả khảo sát cũ: phải kiểm tra lại branch/current HEAD và source thật trước khi sửa.

---

## 3. Tài liệu và source bắt buộc đọc trước

Đọc và đối chiếu tối thiểu:

```text
PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_UI_DESIGN_SYSTEM_PROMPT.md
CLEAN_ARCHITECTURE.md
PEMS_PROMPT_GENERATION_RULES.md
USE_CASE_LIST.md
PERMISSION_MATRIX.md
PERMISSION_RULES.md
VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
PROJECT_STRUCTURE_FULL.md
PROJECT_KNOWLEDGE.md
SQL fresh-create mới nhất của dự án
```

Đọc source hiện tại, không sửa theo suy đoán. Search và kiểm tra tối thiểu các file/module sau; nếu đường dẫn đã đổi thì search theo tên class/component/API trước:

### Frontend

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitParticipantInvitationDetail.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitContributionPage.tsx
frontend/pems-react/src/features/delegations/config/visitRequestFilterConfig.ts
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/App.tsx
```

### Backend

```text
GetVisitInvitationsQueryHandler
GetVisitInvitationDetailQueryHandler
ViewMyVisitInvitationsQueryHandler
VisitInvitationProjection
RespondVisitParticipantInvitationCommandHandler
ViewGuestDelegationListQueryHandler
GetVisitInstanceContributionQueryHandler
GetVisitProcessPermissionsQueryHandler
VisitInvitationsController
DelegationsController
```

### Test

Search toàn bộ existing Unit Test, Architecture Test và Playwright liên quan đến:

```text
GetVisitInvitations
GetVisitInvitationDetail
RespondVisitParticipantInvitation
ViewGuestDelegationList
GetVisitInstanceContribution
VisitRequestManagement
VisitParticipantInvitationDetail
IC_SUPPORT
Staff Leader
attending
OPEN_CONTRIBUTION
```

---

## 4. Thứ tự ưu tiên khi nguồn mâu thuẫn

Khi tài liệu, SQL, source và comment cũ mâu thuẫn nhau, ưu tiên:

1. SQL fresh-create mới nhất.
2. SQL Table & Field Dictionary mới nhất.
3. PEMS Canonical Business Rules.
4. PEMS UC Implementation Rulebook.
5. Permission Rules/Matrix hiện hành.
6. Project Overview và Visitor Management System.
7. Source code hiện tại.
8. Tài liệu/comment legacy chỉ dùng để phát hiện drift, không dùng làm chuẩn nếu mâu thuẫn.

Không được:

- Dùng `permissions`, `role_permissions` hoặc dynamic permission.
- Dùng role legacy như `STAFF_L`, `STAFF_P`.
- Tạo field/table/status không tồn tại trong SQL.
- Tin comment cũ nếu hành vi source hiện tại đã khác.
- Báo hoàn thành khi chưa build/test hoặc chưa ghi rõ lệnh nào không thể chạy.

---

## 5. Mục tiêu task

Sau khi hoàn thành:

1. Staff Leader luôn nhìn thấy tab **“Lời mời tham dự”** trong màn Quản lý tiếp khách.
2. Staff Leader có bốn tab độc lập:

```text
Yêu cầu tại cơ sở
Tôi là host
Lời mời tham dự
Đơn tôi đăng ký
```

3. Tab “Lời mời tham dự” tải đúng các participant row của chính Staff Leader khi họ được mời làm `IC_SUPPORT`.
4. Staff Leader xem được danh sách, tìm kiếm/lọc, xem form đăng ký read-only, xem chi tiết lời mời, chấp nhận hoặc từ chối.
5. Sau khi chấp nhận, Staff Leader vào được Contribution theo đúng permission/lifecycle hiện hành.
6. Quyền của tab attending phải dựa trên participant relation, không được lẫn với quyền Staff Leader duyệt campus hoặc quyền Host.
7. Banner lời mời chờ phản hồi, danh sách và summary phải đồng bộ ngay sau accept/decline.
8. Các API/list handler liên quan không còn mâu thuẫn về việc Staff Leader có được làm invitee hay không.
9. Không thay đổi database.

---

## 6. Baseline dự kiến phải xác minh trước khi code

Kết quả khảo sát trước trên `Cảnh-Iter1` cho thấy:

### 6.1. Frontend đang ẩn tab Staff Leader

Trong `VisitRequestManagement.tsx`, dự kiến đang có logic tương đương:

```ts
const isStaffLeader = isStaff && subRole === 'LEADER';
const isRegularStaff = isStaff && subRole === 'STAFF';
const canUseAttendingTab = isRegularStaff || isDept || isStudent;
```

Vì thiếu `isStaffLeader` nên:

- Tab không render.
- `?tab=attending` không được `isTabAllowed` chấp nhận và bị reset về tab mặc định.
- Các luồng list/action đã tồn tại phía sau không có entry point bình thường.

### 6.2. API invitation mới đã hỗ trợ toàn bộ role STAFF

`GetVisitInvitationsQueryHandler` dự kiến đang lọc:

```text
roleCode == STAFF
participant_role == IC_SUPPORT
status != ASSIGNED
```

và chưa hard-code `subRole == STAFF`, vì vậy Staff Leader có thể được trả dữ liệu nếu frontend mở tab.

### 6.3. Accept/decline đã dựa trên ownership

`RespondVisitParticipantInvitationCommandHandler` dự kiến đã kiểm tra:

```text
participant.user_id == currentUserId
participant.is_host == false
participant_role IN (IC_SUPPORT, DEPT_SUPPORT, STUDENT)
status còn phản hồi được
```

Do đó Staff Leader với participant role `IC_SUPPORT` đã có thể accept/decline mà không cần tạo command mới.

### 6.4. Contribution authorization đã nhận participant ACCEPTED/ASSIGNED

`GetVisitInstanceContributionQueryHandler` dự kiến đã cấp access khi tồn tại participant row:

```text
visit_instance_id == route visitInstanceId
user_id == currentUserId
is_host == false
status IN (ACCEPTED, ASSIGNED)
```

Relation phải được xác định là `IC_SUPPORT` khi Staff Leader đang truy cập với tư cách participant.

### 6.5. Có lỗi lifecycle `OPEN_CONTRIBUTION`

`GetVisitInvitationsQueryHandler` và `GetVisitInvitationDetailQueryHandler` dự kiến đang tái dùng điều kiện kiểu `isActiveForInvitation` cho cả:

```text
ACCEPT_INVITATION / DECLINE_INVITATION
OPEN_CONTRIBUTION
```

Trong khi cửa sổ phản hồi chỉ là `ASSIGNED/BEFORE_VISIT`, còn Contribution có lifecycle khác. Hậu quả có thể là nút Contribution biến mất ở `DURING_VISIT`, `AFTER_VISIT` hoặc `CLOSED`, dù direct contribution API vẫn cho participant hợp lệ truy cập.

### 6.6. Banner pending đang phụ thuộc gián tiếp vào `showTabs`

Banner dự kiến đang gọi `getMyInvitations(false)` khi `showTabs == true`. Staff Leader tình cờ thỏa điều kiện vì có nhiều tab, nhưng đây không phải capability đúng. Sau accept/decline trực tiếp tại bảng, list được reload nhưng pending banner có thể chưa được refresh.

### 6.7. Handler legacy còn chặn Staff Leader

`ViewGuestDelegationListQueryHandler` dự kiến còn comment/rule cũ tương đương:

```text
Staff Leader/IC Head are never invitees
tab == attending && isStaffLeader => empty result
```

Trong khi frontend attending hiện có thể đang dùng `/visit-invitations/my`. Đây là contract drift cần xử lý có chủ đích.

Nếu bất kỳ baseline nào không còn đúng tại current HEAD, phải báo rõ khác biệt và điều chỉnh implementation theo source thật. Không được cố sửa theo baseline cũ.

---

## 7. Phạm vi được sửa

Được sửa:

- Capability/tab visibility và invitation state ở màn quản lý tiếp khách.
- Invitation list/detail API và allowed actions.
- Pending invitation banner/refetch.
- Contribution navigation/action eligibility.
- Handler legacy đang mâu thuẫn nếu vẫn còn endpoint/consumer.
- Comment/documentation inline đã lỗi thời trong các file bị tác động.
- Unit Test và Playwright regression test liên quan trực tiếp.

Được refactor helper nhỏ nếu giúp dùng chung business policy, ví dụ:

```text
CanRespondToInvitation
CanViewContribution
CanMutateContributionSection
```

nhưng không được refactor lan rộng ngoài task.

---

## 8. Phạm vi không được sửa

Không được:

- Thay đổi schema, tạo migration hoặc patch SQL.
- Thay đổi logic mời candidate đã hoàn thành ở task trước, trừ khi phát hiện regression trực tiếp.
- Cho phép mời chính Host.
- Cho Staff Leader trong tab attending thực hiện approve/reject campus, assign host, cancel bằng quyền Host hoặc thao tác quy trình Host.
- Thay đổi logistics.
- Thay đổi email template trừ khi source hiện tại không thể gửi/link lời mời cho Staff Leader.
- Thêm dynamic permission.
- Dùng frontend để thay thế authorization backend.
- Commit, push, tạo PR hoặc làm sạch working tree nếu người dùng chưa yêu cầu.
- Ghi đè/xóa thay đổi sẵn có không thuộc task.

Không bắt buộc tạo Integration Test mới cho task này. Nếu existing Integration Test bị ảnh hưởng bởi contract được sửa thì cập nhật tối thiểu để build/test không hỏng; ưu tiên Unit Test và Playwright theo yêu cầu bên dưới.

---

## 9. Ma trận nghiệp vụ phải triển khai

### 9.1. Capability theo actor

| Actor | Responsible/campus | Hosted | Attending | Registered |
|---|---:|---:|---:|---:|
| `STAFF + STAFF` | Có | Theo rule hiện tại | Có | Có |
| `STAFF + LEADER` | Có | Có | **Có** | Có |
| Department/Student | Giữ nguyên | Giữ nguyên | Giữ nguyên | Giữ nguyên |

Staff Leader phải luôn thấy tab attending, kể cả khi danh sách rỗng. Khi rỗng, hiển thị empty state chuyên nghiệp; không ẩn tab theo số lượng lời mời.

### 9.2. Ma trận trạng thái lời mời

| Participant status | Hiển thị | Allowed actions cơ bản |
|---|---|---|
| `INVITED` | Có | `VIEW_INVITATION_DETAIL`, `VIEW_REQUEST_FORM`, `ACCEPT_INVITATION`, `DECLINE_INVITATION` nếu còn trong response window |
| `ACCEPTED` | Có | `VIEW_INVITATION_DETAIL`, `VIEW_REQUEST_FORM`, `OPEN_CONTRIBUTION` theo contribution authorization |
| `ASSIGNED` | Không hợp lệ cho Staff/Staff Leader `IC_SUPPORT`; giữ rule hiện tại để loại/cảnh báo dữ liệu sai | Không tự mở rộng |
| `DECLINED` | Có | Chỉ xem chi tiết, form read-only và lý do; không contribution |
| `REMOVED` | Không hiển thị | Direct detail phải 404 hoặc policy không lộ resource hiện hành |

### 9.3. Tách response window và contribution lifecycle

Không được tiếp tục tái dùng một boolean cho hai capability khác nhau.

```text
canRespondToInvitation
  = participant status INVITED
  + request/campus còn hiệu lực
  + campus nằm trong cửa sổ được phản hồi theo rule hiện hành

canOpenContribution
  = participant status ACCEPTED/ASSIGNED
  + direct contribution endpoint thực sự authorize current user
```

Nguồn sự thật cho `OPEN_CONTRIBUTION` phải thống nhất với Contribution authorization. List/detail không được tự tạo một lifecycle hẹp hơn direct API.

Yêu cầu tối thiểu:

- `INVITED` chỉ accept/decline trong đúng cửa sổ hiện hành.
- `ACCEPTED` vẫn có đường vào Contribution ở các giai đoạn mà Contribution page cho xem.
- Quyền sửa từng section trong Contribution tiếp tục theo flag backend, không suy ra từ việc được mở trang.
- `Được mở Contribution` không đồng nghĩa với `được sửa mọi section`.
- Với `CLOSED`, `CANCELLED`, `REJECTED`, phải đối chiếu rule hiện hành và dùng chung policy. Không tạo chênh lệch giữa list, detail và direct URL.

### 9.4. Nhiều quan hệ đồng thời

Một Staff Leader có thể đồng thời là:

```text
Staff Leader của campus
Host của một visit instance
IC_SUPPORT được mời ở một visit instance khác
Người đăng ký một visit request
```

Mỗi tab phải dùng đúng actor relation:

```text
Yêu cầu tại cơ sở -> STAFF_LEADER campus scope
Tôi là host        -> current_host_user_id / IC_HOST
Lời mời tham dự    -> own visit_participants row, role IC_SUPPORT
Đơn tôi đăng ký    -> registrant/creator relation, read-only theo rule hiện hành
```

Không dùng global Staff Leader role để thêm action mutate vào tab attending.

---

## 10. Backend requirements

### 10.1. `GetVisitInvitationsQueryHandler`

Xác minh và giữ ownership query:

```text
visit_participants.user_id = currentUserId
visit_participants.is_host = false
visit_participants.status != REMOVED
```

Với `roleCode == STAFF`, cho phép cả `subRole == STAFF` và `subRole == LEADER` khi participant role là `IC_SUPPORT`.

Không được lọc list theo:

```text
created_by = currentUserId
invited_by = currentUserId
current_host_user_id = currentUserId
```

Giữ search/date/status filter hiện hành, nhưng đảm bảo `invitationStatus` lọc theo participant status.

Tách allowed actions:

- Response actions dựa trên response window.
- Contribution action dựa trên contribution access policy.
- Declined không có action mutate.
- Removed không trả về.

### 10.2. `GetVisitInvitationDetailQueryHandler`

Chỉ chính participant được xem:

```text
participant_id = route participantId
user_id = currentUserId
is_host = false
status != REMOVED
```

Allowed actions phải dùng cùng policy với list để không có tình trạng:

```text
list có action nhưng detail không có
hoặc detail có action nhưng direct endpoint từ chối
```

### 10.3. Respond command

Giữ backend authorization hiện hành:

- Chỉ phản hồi lời mời của chính mình.
- Không phản hồi host slot.
- Chỉ participant role hợp lệ.
- Chỉ trạng thái còn phản hồi được.
- Decline reason validation giữ nguyên.
- Revalidate request/campus lifecycle ở backend.

Không thêm hard-code `subRole == STAFF` làm Staff Leader bị chặn.

### 10.4. Contribution authorization

Xác minh accepted Staff Leader được nhận relation `IC_SUPPORT` khi truy cập với participant row.

Giữ permission theo section:

```text
CanViewContributionPage
CanViewRequestSummary
CanViewAgendaSummary
CanViewParticipantSummary
CanViewLogisticsSummary
CanEditMinutes
CanUploadMedia
CanCreateNews
CanEditNews
IsReadOnly
```

Không hard-code Staff Leader luôn read-only nếu họ đồng thời là accepted `IC_SUPPORT`. Quyền participant phải được tính riêng, kể cả khi display relation hoặc global role precedence là Staff Leader.

Nếu hiện có duplicated eligibility giữa list/detail/contribution, ưu tiên tạo shared policy/helper nhỏ ở Application layer thay vì tiếp tục copy điều kiện khác nhau.

### 10.5. Handler management legacy

Search tất cả consumer của `ViewGuestDelegationListQueryHandler` với `tab=attending`.

- Nếu endpoint vẫn public/reachable: bỏ rule loại Staff Leader, cập nhật comment và thêm regression test.
- Nếu nhánh attending thực sự legacy/dead: không xóa mù quáng. Báo bằng chứng consumer, sau đó deprecate/remove chỉ khi không phá compatibility.
- Không được để comment “Staff Leader never invitees” tồn tại sau khi business rule mới đã cho phép Staff Leader làm `IC_SUPPORT`.

---

## 11. Frontend requirements

### 11.1. Capability rõ nghĩa

Trong `VisitRequestManagement.tsx`, tạo capability rõ nghĩa, ví dụ:

```ts
const canReceiveParticipantInvitations =
  isRegularStaff || isStaffLeader || isDept || isStudent;

const canUseAttendingTab = canReceiveParticipantInvitations;
```

Không dùng `showTabs` thay cho capability nhận lời mời.

### 11.2. Thứ tự tab Staff Leader

Hiển thị theo thứ tự:

```text
Yêu cầu tại cơ sở
Tôi là host
Lời mời tham dự
Đơn tôi đăng ký
```

`?tab=attending` phải được giữ nguyên sau refresh/back navigation.

### 11.3. Tái sử dụng invitation list hiện có

Không tạo màn hình/list API mới nếu source hiện tại đã có `visitInvitations.getMyInvitations`.

Mapping row phải giữ:

```text
participantId
visitRequestId
visitInstanceId
invitationStatus
participantRole
allowedActions
```

Filter attending hiện hành phải hoạt động cho Staff Leader trước khi rơi vào filter config riêng của Staff Leader campus review.

### 11.4. Action trong bảng

Render action từ `allowedActions`, không hard-code theo display label.

Trong tab attending:

- `VIEW_REQUEST_FORM`: mở form đăng ký read-only.
- `VIEW_INVITATION_DETAIL`: mở `/dashboard/visit/invitations/{participantId}`.
- `ACCEPT_INVITATION`: chấp nhận.
- `DECLINE_INVITATION`: mở modal lý do rồi từ chối.
- `OPEN_CONTRIBUTION`: ưu tiên điều hướng `/dashboard/visit/contribution/{visitInstanceId}`.

Không render:

```text
APPROVE_AND_ASSIGN_HOST
CAMPUS_REJECT
CANCEL_BY_HOST
OPEN_HOST_PROCESS
```

chỉ vì current user có global role Staff Leader.

### 11.5. Đồng bộ banner/list sau phản hồi

Tách helper tải pending invitations, ví dụ `loadPendingInvitations()`.

Sau accept/decline thành công tại bảng:

```text
reload invitation list
reload pending banner
reload summary/count
```

Không để lời mời đã phản hồi tiếp tục nằm ở banner cho đến khi refresh toàn trang.

Trang detail sau accept/decline cũng phải trả về trạng thái mới đúng; khi quay lại management page, list/banner phải đúng.

### 11.6. Nhãn participant

Khi current user là `STAFF + LEADER` và participant role là `IC_SUPPORT`, hiển thị nhãn:

```text
Staff Leader hỗ trợ IC
```

Thay vì nhãn gây hiểu nhầm “Staff hỗ trợ IC”. Dùng helper chung nếu list và detail cùng cần.

### 11.7. UI/UX

Giữ nguyên PEMS design system:

- Không redesign toàn trang.
- Không đổi màu thương hiệu ngoài scope.
- Giữ responsive desktop/mobile.
- Có loading, error, empty state rõ ràng.
- Icon/nút có `title`/`aria-label` phù hợp.
- Không dùng `window.alert`/`window.prompt` mới.
- Không hiển thị action frontend nếu backend không trả `allowedActions` tương ứng.

---

## 12. Authorization và security bắt buộc

Backend phải chứng minh:

1. Staff Leader chỉ xem invitation có `participant.user_id == currentUserId`.
2. Không thể đoán `participantId` của người khác để xem/accept/decline.
3. `REMOVED` không lộ qua list hoặc direct detail.
4. `INVITED/DECLINED` không được vào Contribution.
5. Chỉ `ACCEPTED/ASSIGNED` participant hợp lệ mới được vào Contribution.
6. Tab attending không kế thừa action duyệt campus từ Staff Leader.
7. Tab attending không kế thừa action Host nếu user không phải Host thật của instance.
8. Frontend visibility không được xem là authorization.
9. Không mở rộng quyền cho toàn bộ Staff Leader ở mọi visit instance; quyền phải dựa trên participant row hoặc relation thật.

---

## 13. Database/SQL rules

Không thay đổi database.

Chỉ xác minh schema hiện tại đã đủ, tối thiểu các semantic field:

```text
visit_participants.participant_id
visit_participants.visit_instance_id
visit_participants.user_id
visit_participants.participant_role
visit_participants.status
visit_participants.is_host
visit_participants.invited_by
visit_participants.invited_at
visit_participants.responded_at
visit_participants.note
visit_request_campuses.visit_instance_id
visit_request_campuses.visit_request_id
visit_request_campuses.current_host_user_id
visit_request_campuses.status
visit_requests.status
```

Nếu tên cột/source mapping khác, dùng tên thật từ SQL/entity. Không tạo migration/patch SQL cho task này.

---

## 14. Test requirements

### 14.1. Backend Unit Test bắt buộc

Bổ sung regression test theo pattern/harness hiện có, không tạo fake test chỉ để pass.

Tối thiểu phải có:

#### Invitation list

1. `STAFF + LEADER` có own `IC_SUPPORT/INVITED` được trả về.
2. Item `INVITED` có view form/detail + accept/decline khi còn trong response window.
3. `ACCEPTED` có `OPEN_CONTRIBUTION` khi direct contribution policy cho phép.
4. Kiểm tra các lifecycle quan trọng ít nhất `BEFORE_VISIT`, `DURING_VISIT`, `AFTER_VISIT` và `CLOSED` theo canonical policy.
5. `DECLINED` chỉ có action read-only.
6. `REMOVED` không xuất hiện.
7. Lời mời của user khác không xuất hiện.
8. `ASSIGNED` không được coi là invitation hợp lệ cho Staff Leader `IC_SUPPORT` nếu rule hiện hành dành status này cho Department Staff.

#### Invitation detail/respond

9. Staff Leader xem được detail của own invitation.
10. Direct detail của người khác bị từ chối/không tìm thấy theo policy hiện hành.
11. Staff Leader accept own invitation thành công.
12. Staff Leader decline own invitation với lý do hợp lệ thành công.
13. Không thể phản hồi lại item đã `ACCEPTED/DECLINED`.
14. Request/campus hết response window không cho accept/decline.

#### Contribution và permission isolation

15. Accepted Staff Leader được nhận participant relation `IC_SUPPORT` trong Contribution.
16. Accepted Staff Leader nhận đúng section flags theo lifecycle.
17. Invited/declined/removed Staff Leader không được mở Contribution.
18. Staff Leader participant không tự động nhận action campus approval/Host trong attending context.

#### Legacy handler

19. Nếu `ViewGuestDelegationList(tab=attending)` còn được hỗ trợ, thêm test chứng minh Staff Leader không còn bị hard-block sai.

Mỗi test phải gọi handler/policy thật, assert dữ liệu/action thật. Không mock chính logic đang cần kiểm tra.

### 14.2. Frontend Playwright bắt buộc

Tạo spec regression nhỏ, dùng real component/UI với network mock theo pattern hiện có:

1. Staff Leader thấy đúng bốn tab theo thứ tự yêu cầu.
2. `?tab=attending` được giữ và gọi đúng invitation API.
3. Empty state hiển thị đúng khi không có lời mời.
4. `INVITED` hiển thị nút xem form, accept, decline.
5. Accept gọi đúng endpoint, reload list/banner và item không còn ở pending banner.
6. Decline bắt buộc lý do, gọi đúng endpoint, reload list/banner.
7. `ACCEPTED` hiển thị action vào Contribution và điều hướng đúng route.
8. Trạng thái lifecycle sau khi chuyến thăm bắt đầu/kết thúc vẫn hiển thị Contribution nếu backend permission cho phép.
9. Attending tab không có action approve/reject campus hoặc Host action.
10. Hiển thị nhãn “Staff Leader hỗ trợ IC”.

Không chỉ assert mock payload; phải assert DOM/action/navigation thật của UI.

### 14.3. Không yêu cầu Integration Test mới

Không tạo Integration Test mới trừ khi current test architecture bắt buộc để giữ contract. Nếu không tạo, báo rõ lý do trong completion report.

---

## 15. Quy trình thực hiện bắt buộc

### Bước 1 — Git/source audit

- Báo branch/current HEAD.
- Chạy `git status --short` và bảo toàn thay đổi sẵn có.
- Search consumer, route, API và test hiện tại.
- Xác minh từng baseline ở mục 6.
- Chỉ ra endpoint invitation nào là canonical cho attending tab.

### Bước 2 — Chốt policy trước khi sửa

Viết ngắn gọn ma trận:

```text
role/subRole
participant status
campus/request lifecycle
allowedActions
destination route
```

Nếu list/detail/contribution đang mâu thuẫn, chọn policy theo nguồn chuẩn và ghi rõ.

### Bước 3 — Implement tối thiểu

- Mở capability/tab Staff Leader.
- Đồng bộ pending banner.
- Tách response và contribution capability.
- Đồng bộ list/detail/direct contribution.
- Dọn guard/comment legacy có liên quan.
- Không refactor ngoài scope.

### Bước 4 — Test/build

Chạy tối thiểu:

```bash
dotnet build backend/PEMS.Api
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.ArchitectureTests

cd frontend/pems-react
npm run lint
npm run build
npx playwright test <target-spec>
```

Nếu dev server khóa file build, dùng output folder tạm hoặc dừng đúng process do task tạo; không được coi file-lock là compile failure nếu compile riêng thành công.

Không sửa test để che lỗi. Không bỏ test/skip test nhằm đạt pass.

### Bước 5 — Self-review

Kiểm tra lại:

- Staff Leader có đủ bốn tab.
- Ownership được revalidate ở backend.
- Accept/decline và contribution không dùng chung sai lifecycle.
- Banner không stale.
- Không rò quyền Staff Leader/Host vào attending.
- Không thay đổi database.
- Không làm hỏng Staff thường, Department hoặc Student.

---

## 16. Definition of Done

Task chỉ hoàn thành khi:

- [ ] Baseline được xác minh bằng source current HEAD.
- [ ] Staff Leader thấy tab “Lời mời tham dự”.
- [ ] URL `?tab=attending` hoạt động.
- [ ] Own invitations được load; invitation của người khác/removed không lộ.
- [ ] Accept/decline hoạt động ở list/detail và được backend revalidate.
- [ ] Pending banner/list/summary đồng bộ sau phản hồi.
- [ ] Accepted Staff Leader có đường vào Contribution đúng lifecycle.
- [ ] List/detail/direct Contribution dùng cùng authorization policy.
- [ ] Attending tab không có campus approval hoặc Host actions sai ngữ cảnh.
- [ ] Comment/rule legacy “Staff Leader never invitees” đã được xử lý.
- [ ] Không có schema/migration/SQL patch.
- [ ] Unit Test mới pass.
- [ ] Architecture Test pass.
- [ ] Frontend TypeScript/lint/build pass.
- [ ] Targeted Playwright pass.
- [ ] Working tree của người dùng được bảo toàn.
- [ ] Completion report ghi kết quả thật, không phỏng đoán.

---

## 17. Completion report bắt buộc

Sau khi hoàn thành, trả báo cáo theo đúng cấu trúc:

```text
1. Git context và baseline
   - Branch/current HEAD
   - Working tree trước/sau
   - Baseline nào đúng/sai so với prompt

2. Root cause
   - Vì sao Staff Leader thiếu tab
   - Vì sao contribution action bị mất theo lifecycle nếu có
   - Endpoint/handler nào mâu thuẫn

3. Files changed
   - Backend
   - Frontend
   - Tests
   - Database: xác nhận không đổi

4. Logic implemented
   - Capability/tab
   - Invitation ownership/status/actions
   - Contribution lifecycle
   - Pending banner synchronization
   - Role/relation isolation

5. Authorization/security
   - Direct URL
   - Ownership
   - Removed/declined/invited contribution denial
   - Không rò campus/Host action

6. Tests/build đã chạy
   - Ghi chính xác command
   - Discovered/passed/failed/skipped
   - Build/lint/Playwright result
   - Không được chỉ ghi “all pass” nếu không có số liệu thật

7. Database impact
   - No schema change
   - No migration
   - No SQL patch

8. Remaining risks
   - Chỉ ghi rủi ro thật còn lại
```

Nếu có bước không thể chạy do môi trường, phải ghi rõ blocker và bằng chứng. Không được báo “đã verify đầy đủ” khi chưa thực sự chạy.
