# PEMS — Email Action / Department Staff / Logistics System-Wide Fix Specification

> **Mục đích:** Tài liệu giao cho coding agent để sửa toàn bộ cụm lỗi liên quan đến email action, Department Staff assignment, Logistics response, deep-link và các regression liên quan.
>
> **Repository:** `quangthoai04/PEMS`  
> **Branch baseline đã rà soát:** `Dev`  
> **Baseline commit:** `12a62f8ce7b36abc763e4de395ab0aaef51eec95`  
> **Ngày chốt yêu cầu:** 2026-08-21
>
> **Quan trọng:** Agent phải đọc source hiện tại trước khi sửa. Nếu branch đã tiến thêm commit so với baseline trên, phải rebase kết luận này lên source mới nhất, không được ghi đè các fix mới hơn.

---

## 1. Yêu cầu nghiệp vụ đã được chốt — đây là nguồn sự thật cho đợt fix này

Các rule dưới đây là **business requirement đã được chốt trong cuộc rà soát**. Nếu code/comment/doc cũ mâu thuẫn với các rule này thì **rule dưới đây được ưu tiên**.

### 1.1. Lời mời phòng ban tham gia hỗ trợ đoàn

Luồng đúng:

```text
IC / Host
   ↓
mời một Department tham gia hỗ trợ đoàn
   ↓
Department Leader nhận lời mời
   ↓
Leader có thể giao lời mời/nhiệm vụ đó xuống một Department Staff
   ↓
Staff participant = ASSIGNED
   ↓
Staff phải được quyền tự phản hồi:
   ├─ Accept  → ACCEPTED
   └─ Decline → DECLINED
```

Yêu cầu bắt buộc:

- `ASSIGNED` ở **Department Staff assignment** là trạng thái hợp lệ đang chờ Staff phản hồi.
- Staff phải phản hồi được **cả trong Portal và từ email**.
- Portal và Email phải dùng cùng business transition.
- Không được có trường hợp Portal cho `ASSIGNED` phản hồi nhưng Email lại cấm.
- Leader giao Staff không có nghĩa Staff tự động đồng ý.
- Không được reset một Staff đã `ACCEPTED` về lại `ASSIGNED` chỉ vì request assign bị retry/double-click.

### 1.2. Logistics / hậu cần

Luồng đúng:

```text
Host tạo logistics request
   ↓
REQUESTED
   ↓
Department Leader
   ├─ tự tiếp nhận                → ACCEPTED
   ├─ từ chối request của Host    → REJECTED
   └─ giao cho Department Staff   → ASSIGNED
                                      ↓
                                 Staff phản hồi
                                      ├─ Accept  → ACCEPTED
                                      └─ Decline → DECLINED (TERMINAL)
```

Rule quan trọng:

- `REJECTED` = Department Leader từ chối yêu cầu logistics của Host.
- `DECLINED` = Department Staff từ chối nhiệm vụ đã được Leader giao.
- Khi Staff từ chối logistics (`DECLINED`) thì **luồng kết thúc**.
- **Không thêm logic reassign Staff khác sau khi Staff đã từ chối.**
- Không được đổi `DECLINED` thành `REQUESTED`.
- Portal và Email phải cho ra cùng final state.

### 1.3. Logistics proposal

Rule chốt cho đợt fix:

```text
Department Leader / Staff tạo proposal
   ↓
Host nhận thông báo/email
   ↓
Host phải đăng nhập hệ thống
   ↓
Host xem chi tiết proposal
   ↓
Accept / Reject proposal trong Portal
```

Yêu cầu:

- Email proposal **không được cấp public Approve/Reject token**.
- Email proposal chỉ có link **“Xem chi tiết trong hệ thống”**.
- Host phải đăng nhập mới được Accept/Reject proposal.

### 1.4. Dev credentials

Theo yêu cầu hiện tại:

- **Không xử lý/xóa/rotate credential dev trong `appsettings.json` trong task này.**
- Không biến vấn đề dev-secret thành blocker cho fix email.
- Đây là chủ đích để tiện phát triển hiện tại.

---

# 2. Những việc KHÔNG được làm

Agent **không được**:

1. Không relax CSP cho toàn bộ API chỉ để sửa một HTML page.
2. Không đổi public email action từ `GET confirm -> POST mutate` thành mutate trực tiếp bằng GET.
   - Mail scanner/prefetch có thể tự mở GET.
3. Không coi mọi participant `ASSIGNED` đều hợp lệ một cách mù quáng.
   - Phải phân biệt assignment-response thật với token cũ đã bị invalidate.
4. Không thêm reassign logistics sau `DECLINED`.
5. Không đổi Staff logistics decline thành `REQUESTED`.
6. Không giữ `REJECTED` cho Staff logistics decline.
7. Không tiếp tục mint public `APPROVE_PROPOSAL/REJECT_PROPOSAL` token cho proposal mới.
8. Không hardcode thêm frontend URL rải rác ở handler mới.
9. Không sửa bằng cách chỉ “làm đẹp UI”; phải sửa cả state contract và token contract.
10. Không xóa các security headers khác như `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`.
11. Không thay đổi credential dev theo task này.

---

# 3. BUG-01 — P0 — CSP Production làm hỏng UI email action và chặn POST

## 3.1. Hiện trạng

File:

`backend/PEMS.Api/Middleware/SecurityHeadersMiddleware.cs`

Production/non-Development hiện áp:

```text
default-src 'none';
frame-ancestors 'none';
base-uri 'none';
form-action 'none'
```

Trong khi:

`backend/PEMS.Api/Email/EmailActionHtmlPages.cs`

render HTML bằng:

- inline `style="..."`
- `<form method="post">`
- button submit POST về chính URL hiện tại.

Controller:

`backend/PEMS.Api/Controllers/PublicEmailActionsController.cs`

đúng thiết kế:

```text
GET  /api/public/email-actions/{token}  → chỉ render confirmation
POST /api/public/email-actions/{token}  → mới mutate/consume token
```

## 3.2. Hậu quả

Ở Production:

- inline CSS bị CSP chặn;
- trang hiện HTML thô như screenshot;
- `form-action 'none'` có thể chặn submit Accept/Decline ngay ở browser;
- request POST có thể không tới backend.

## 3.3. Fix bắt buộc

Tách CSP theo loại route/response.

### Với `/api/public/email-actions/*`

Policy tối thiểu nên tương đương:

```text
default-src 'none';
script-src 'none';
style-src 'unsafe-inline';
form-action 'self';
frame-ancestors 'none';
base-uri 'none';
```

Giữ nguyên các header khác.

### Với API JSON thông thường

Giữ policy strict hiện tại:

```text
default-src 'none';
frame-ancestors 'none';
base-uri 'none';
form-action 'none'
```

### Không làm

Không set `style-src 'unsafe-inline'` và `form-action 'self'` cho toàn bộ backend.

## 3.4. Test bắt buộc

Tạo integration test chạy environment non-Development/Production-like:

- GET email-action phải trả `text/html`.
- CSP của route email-action phải chứa:
  - `style-src 'unsafe-inline'`
  - `form-action 'self'`
  - `script-src 'none'`
  - `frame-ancestors 'none'`
- Một API JSON bình thường vẫn có `form-action 'none'`.
- Browser/E2E phải submit được form POST.
- GET không được consume token.

---

# 4. BUG-02 — P0 — Department Staff `ASSIGNED` phản hồi được trong Portal nhưng Email lại cấm

## 4.1. Evidence hiện tại

### Producer

File:

`backend/PEMS.Application/Delegations/Commands/AssignDepartmentStaff/AssignDepartmentStaffCommandHandler.cs`

Handler:

- tạo/update participant của Staff;
- set:

```text
Status = ASSIGNED
ParticipantRole = DEPT_SUPPORT
AssignedBy = Leader
AssignedAt = now
```

- mint 2 token:
  - `ACCEPT`
  - `DECLINE`
- gửi email Staff với Accept/Decline.

Comment trong source cũng nói rõ Staff được giao vẫn phải tự trả lời.

### Portal

Shared business transition:

`backend/PEMS.Application/Delegations/Common/VisitInvitationResponse.cs`

cho phép participant:

```text
INVITED
hoặc
ASSIGNED
```

và chuyển:

```text
Accept  → ACCEPTED
Decline → DECLINED
```

### Email GET

`backend/PEMS.Application/EmailActions/GetEmailActionInfoQueryHandler.cs`

hiện coi:

```text
participant.Status == ASSIGNED
→ INVALID
```

### Email POST

`backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs`

hiện chủ động reject:

```text
ASSIGNED
→ "Thành phần tham gia đã được phân công trực tiếp, không thể phản hồi qua email."
```

Đây là producer/consumer contradiction.

## 4.2. Expected behavior

```text
Department Leader assign Staff
      ↓
Staff participant = ASSIGNED
      ↓
Email token dành cho chính Staff
      ├─ ACCEPT  → ACCEPTED
      └─ DECLINE → DECLINED
```

Kết quả phải giống Portal.

## 4.3. Hướng fix khuyến nghị

### Preferred: tách context riêng cho Staff assignment

Thêm context semantic rõ ràng, ví dụ:

```text
PARTICIPATION_RESPONSE
    = direct invitation
    = INVITED → ACCEPTED / DECLINED

PARTICIPATION_ASSIGNMENT_RESPONSE
    = Department Leader giao Staff
    = ASSIGNED → ACCEPTED / DECLINED
```

`AssignDepartmentStaffCommandHandler.NewToken(...)` phải mint context assignment mới.

GET/POST email action xử lý context mới theo đúng state `ASSIGNED`.

### Đồng thời hợp nhất business transition

Không để email tự viết lại participant state bằng một implementation khác.

Ưu tiên tái sử dụng/refactor logic từ:

`VisitInvitationResponse.ApplyAsync(...)`

thành shared response service, ví dụ:

```text
VisitParticipantResponseService
```

để Portal và Email dùng chung:

- ownership/target-user validation;
- allowed role;
- allowed status;
- visit lifecycle guard;
- decline reason;
- final state;
- RespondedAt/UpdatedAt;
- audit;
- token invalidation.

Email có thể truyền actor là `token.RecipientUserId` sau khi đã verify token-target-recipient match.

### Compatibility option

Nếu agent không tạo context mới thì chỉ được cho `ASSIGNED` hợp lệ khi **tất cả** điều kiện assignment thật được chứng minh, ví dụ:

- participant role = `DEPT_SUPPORT`;
- `AssignedBy != null`;
- token recipient = participant user;
- token vẫn PENDING;
- token không phải token cũ của Leader;
- visit còn trong response window.

Nhưng context riêng vẫn là phương án sạch hơn.

## 4.4. Renderer wording

Staff được Leader giao không nên bị gọi sai là “lời mời thông thường”.

Nên hiển thị kiểu:

```text
Xác nhận nhận nhiệm vụ
Bạn được phòng ban phân công tham gia hỗ trợ đoàn ...
```

Decline:

```text
Từ chối nhiệm vụ được phân công
```

## 4.5. Test bắt buộc

E2E:

```text
Leader assignment
→ participant ASSIGNED
→ email generated
→ GET Accept token = VALID
→ POST Accept
→ participant ACCEPTED
→ sibling Decline token = ALREADY_RESPONDED/INVALID
```

Và:

```text
Leader assignment
→ participant ASSIGNED
→ POST Decline với reason
→ participant DECLINED
→ reason persisted
```

Thêm test:

- token cũ của Leader sau khi delegate Staff không được sống lại.
- direct `INVITED` invitation vẫn hoạt động như cũ.
- Staff assignment token không hoạt động nếu recipient không phải participant owner.
- cancelled/closed/started visit vẫn chặn.

---

# 5. BUG-03 — P1 — `AssignDepartmentStaff` có thể reset state đã xử lý về `ASSIGNED`

## 5.1. Hiện trạng

Trong:

`AssignDepartmentStaffCommandHandler.cs`

khi tìm thấy `existingParticipant`, code hiện làm gần như vô điều kiện:

```text
existingParticipant.ParticipantRole = DEPT_SUPPORT
existingParticipant.Status = ASSIGNED
existingParticipant.AssignedBy = currentLeader
existingParticipant.AssignedAt = now
```

Không guard đầy đủ theo status hiện tại.

## 5.2. Case lỗi

```text
Leader assign Staff A
→ ASSIGNED

Staff A Accept
→ ACCEPTED

request assign bị retry/double-click/chạy lại
→ existingParticipant != null
→ ACCEPTED bị ghi ngược thành ASSIGNED
```

Đây là reverse transition sai.

## 5.3. Fix bắt buộc

Thêm explicit state guard cho target participant.

Minimum rule:

```text
Không tồn tại participant
→ tạo ASSIGNED

INVITED
→ có thể chuyển ASSIGNED nếu đúng nghiệp vụ delegation

ASSIGNED
→ idempotent; không reset, không tạo duplicate state transition

ACCEPTED
→ không được chuyển ngược ASSIGNED

DECLINED
→ không tự ý chuyển ngược ASSIGNED

REMOVED
→ không tự phục hồi; phải theo flow re-invite riêng nếu hệ thống có

Unknown/unsupported
→ Conflict
```

### Idempotency

Nếu cùng exact assignment request bị gửi lặp:

- không tạo participant duplicate;
- không reset `RespondedAt`;
- không tạo thêm action group token vô hạn;
- không gửi duplicate email nếu operation đã hoàn tất.

Nếu hệ thống cần “resend email”, phải dùng resend flow riêng, không giả lập bằng assign lại.

## 5.4. Eligibility guard bổ sung

Current handler kiểm tra:

- role code Department;
- cùng department;
- account Active.

Nhưng nghiệp vụ yêu cầu giao cho **Department Staff**.

Agent phải audit và đảm bảo target không phải một Department Leader khác. Nếu domain có `SubRole`, cần enforce:

```text
targetStaff.SubRole == STAFF
```

trừ khi source có business rule khác được chứng minh rõ.

## 5.5. Test bắt buộc

- `ACCEPTED` không thể bị reset về `ASSIGNED`.
- `DECLINED` không tự bị reset.
- retry cùng assignment là idempotent.
- không duplicate token group/email.
- không assign nhầm Department Leader như Staff.
- inactive/out-of-department user vẫn bị chặn.

---

# 6. BUG-04 — P1 — Logistics Staff decline: Portal dùng `REJECTED`, Email dùng `DECLINED`

## 6.1. Business rule đúng

```text
Leader từ chối logistics request của Host → REJECTED
Staff từ chối assignment của Leader      → DECLINED
```

Staff decline là terminal theo yêu cầu hiện tại.

## 6.2. Hiện trạng Portal

File:

`backend/PEMS.Application/DepartmentReceptionTasks/Commands/DeclineAssignedLogisticsTask/DeclineAssignedLogisticsTaskCommand.cs`

Attempt được set đúng:

```text
attempt.Status = DECLINED
ResponseSource = PORTAL
ResponseNote = reason
```

Nhưng item lại set:

```text
l.Status = REJECTED
l.AssignedToUserId = null
l.AssignedBy = null
l.AssignedAt = null
```

Comment/message còn nói:

```text
Leader có thể phân công lại
"Vui lòng phân công người khác."
"Phòng ban sẽ phân công người khác."
```

Tất cả các câu này mâu thuẫn với requirement đã chốt.

## 6.3. Hiện trạng Email

Email assignee handler đang dùng:

```text
ASSIGNED → DECLINED
```

Đây mới là state đúng theo requirement.

## 6.4. Fix bắt buộc

Portal decline phải trở thành:

```text
item.Status = DECLINED
item.AssigneeResponseNote = reason
item.UpdatedBy = staff
item.UpdatedAt = now

attempt.Status = DECLINED
attempt.ResponseNote = reason
attempt.ResponseSource = PORTAL
attempt.RespondedAt = now
```

### Không reassign

Không set lại `REQUESTED`.

Không cho `AssignRequestAssignee` assign tiếp sau `DECLINED`.

### Assignment history

Khuyến nghị giữ:

```text
AssignedToUserId
AssignedBy
AssignedAt
```

để final record còn trả lời được:

> ai giao, giao cho ai, lúc nào, người đó đã từ chối.

Assignment attempt vẫn là audit history chi tiết.

### Notification wording

Bỏ toàn bộ wording:

```text
"Vui lòng phân công người khác"
"Phòng ban sẽ phân công người khác"
```

Đổi thành thông báo terminal/informational, ví dụ:

```text
"Nhân viên X đã từ chối nhiệm vụ Y."
```

`IsActionRequired` cho thông báo decline này nên là `false`, trừ khi có business action terminal khác thật sự.

## 6.5. Test bắt buộc

Portal:

```text
ASSIGNED
→ Decline
→ item DECLINED
→ attempt DECLINED
→ reason persisted
→ assignment relation/history còn truy được
```

Email:

```text
ASSIGNED
→ email Decline
→ item DECLINED
```

Assert Portal và Email cho cùng semantic final state.

Assert `DECLINED` không thể assign người khác.

---

# 7. BUG-05 — P1 — Logistics xử lý trong Portal nhưng pending email token cũ không được đóng ngay

## 7.1. Tình huống

Staff nhận email:

```text
[Chấp nhận]
[Từ chối]
```

Hai token:

```text
ACCEPT  = PENDING
DECLINE = PENDING
```

Staff không dùng email mà vào Portal Accept/Decline.

Business state đã đổi, nhưng current Portal handlers:

- `AcceptAssignedLogisticsTask`
- `DeclineAssignedLogisticsTask`

không chủ động invalidate toàn bộ pending email-action token tương ứng giống participant flow.

## 7.2. Hậu quả

DB có thể tồn tại:

```text
item = ACCEPTED hoặc DECLINED

nhưng:
email token ACCEPT  = PENDING
email token DECLINE = PENDING
```

Email handler còn target-state guard nên thường vẫn chặn mutation lần hai, nhưng token state bị stale và gây:

- dữ liệu không nhất quán;
- stale-token query báo sai;
- UI/email cũ gây hiểu nhầm;
- tăng độ phức tạp cho concurrency.

## 7.3. Fix bắt buộc

Khi Staff xử lý logistics trong Portal:

### Accept

```text
item        → ACCEPTED
attempt     → ACCEPTED
email group → ALREADY_RESPONDED / INVALID
```

### Decline

```text
item        → DECLINED
attempt     → DECLINED
email group → ALREADY_RESPONDED / INVALID
```

Dùng:

`EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(...)`

hoặc một shared method semantic tốt hơn.

Thực hiện trong cùng transaction với business response.

## 7.4. Kiến trúc khuyến nghị

Tạo:

```text
LogisticsAssigneeResponseService
```

Cả 4 entry point gọi chung:

```text
Portal Accept
Portal Decline
Email Accept
Email Decline
```

Service chịu trách nhiệm:

- ownership;
- target status;
- reason;
- attempt state;
- item state;
- final terminal rule;
- invalidation;
- audit;
- notification event payload.

## 7.5. Test bắt buộc

- Portal Accept xong → email Accept/Decline cũ không còn PENDING.
- Portal Decline xong → email Accept/Decline cũ không còn PENDING.
- Click email cũ sau Portal response → “đã phản hồi trước đó”.
- Không mutate state lần hai.

---

# 8. BUG-06 — P1 — Backend đang sinh frontend deep-link không tồn tại

## 8.1. Current builders

File:

`backend/PEMS.Infrastructure/Email/EmailActionTokenService.cs`

Hiện có:

```text
BuildLogisticsDetailUrl(id)
→ /dashboard/departments/tasks/{id}
```

Frontend `App.tsx` không có route `departments/tasks/:id`.

Hiện có:

```text
BuildVisitInstanceDetailUrl(visitRequestId, visitInstanceId)
→ /dashboard/visit/process/{visitRequestId}/{visitInstanceId}
```

Frontend thực tế khai báo:

```text
/dashboard/visit/process/:id
```

không có route 2 segment id như builder đang tạo.

`BuildDepartmentAssignmentUrl(...)` hiện trỏ:

```text
/dashboard/visit/department-tasks/{participantId}
```

route này hiện tồn tại, nên không được sửa bừa nếu không có lý do.

## 8.2. Hậu quả

Các nút email như:

- “Xem chi tiết trong hệ thống”
- “Xem chi tiết chuyến tiếp khách”

có thể dẫn tới Not Found/route sai.

Notification có frontend resolver riêng nên một số URL “sai shape” vẫn được rewrite khi click notification. **Email click không đi qua notification resolver**, nên phải là URL thật ngay từ lúc gửi.

## 8.3. Fix bắt buộc

Không tiếp tục dùng một generic `BuildLogisticsDetailUrl(id)` cho mọi role/context.

Tạo canonical deep-link builder có context, ví dụ:

```text
IFrontendDeepLinkService

BuildVisitParticipantAssignmentUrl(participantId)
BuildDepartmentStaffLogisticsTaskUrl(logisticsItemId)
BuildDepartmentLeaderLogisticsTaskUrl(logisticsItemId)
BuildHostVisitProcessUrl(visitInstanceId)
BuildVisitProcessUrl(visitInstanceId)
```

Expected canonical URLs phải dựa trên route thật hiện tại.

Ví dụ Department Staff task hiện frontend resolver đang dùng shape:

```text
/dashboard?taskId={id}&itemType=REQUEST
```

Department Leader task dùng:

```text
/dashboard/visit?taskId={id}&itemType=REQUEST
```

Host logistics proposal/detail nên đi vào existing Host/Visit process screen bằng `visitInstanceId`, không dùng một route Department không tồn tại.

## 8.4. Audit bắt buộc

Repo-wide search tất cả nơi tạo:

```text
/dashboard/...
FrontendBaseUrl
BuildLogisticsDetailUrl
BuildVisitInstanceDetailUrl
ActionUrl
detailUrl
```

Phân loại:

- email direct link;
- notification link;
- reminder link;
- account/confirmation link.

Không thay notification semantic resolver bằng hardcode mới nếu resolver đang đúng.

## 8.5. Test bắt buộc

Thêm route contract tests:

- mọi URL backend generate cho email phải match một route frontend canonical;
- Visit reminder detail không được có `/process/{requestId}/{instanceId}`;
- Logistics Staff detail không được có `/departments/tasks/{id}` nếu route đó không tồn tại.

---

# 9. BUG-07 — P1 — Logistics proposal đang mint public token trái business rule

## 9.1. Hiện trạng code

File:

`backend/PEMS.Application/DepartmentReceptionTasks/Commands/ProposeRequestChange/ProposeRequestChangeCommand.cs`

Hiện đang:

- tạo `approveRaw`;
- tạo `rejectRaw`;
- tạo public action URL;
- render `LogisticsProposalActionBlock(approveUrl, rejectUrl, detailUrl)`;
- mint:
  - `APPROVE_PROPOSAL`
  - `REJECT_PROPOSAL`
- context:
  - `LOGISTICS_PROPOSAL_RESPONSE`.

POST email handler còn có `HandleLogisticsProposalAsync`.

Nhưng GET:

`GetEmailActionInfoQueryHandler`

không có branch cho `LOGISTICS_PROPOSAL_RESPONSE`.

Đồng thời business rule đã chốt yêu cầu proposal phải xử lý trong Portal.

## 9.2. Fix bắt buộc

Khi tạo proposal:

```text
Không tạo approveRaw
Không tạo rejectRaw
Không mint EmailActionToken proposal
Không render public Approve/Reject button
```

Email Host chỉ render:

```text
[Xem chi tiết trong hệ thống]
```

link login-required tới đúng Host logistics/detail screen.

In-app notification vẫn giữ và phải dẫn đúng màn proposal.

## 9.3. Registry/preview phải đổi theo

File:

`backend/PEMS.Application/Emails/Common/EmailActionTemplates.cs`

`LogisticsChangeProposalToHost` hiện mô tả:

```text
Chấp nhận đề xuất / Từ chối đề xuất
+ one-time links
+ detail
```

Phải đổi thành **detail-only login-required action**.

Preview phải giống email thật:

```text
[Xem chi tiết trong hệ thống]
```

không có disabled public approve/reject button nữa.

## 9.4. Legacy tokens

Không cần đảm bảo public proposal tokens cũ tiếp tục mutate.

Cách an toàn:

- không mint token mới;
- legacy token nếu còn pending phải bị invalid/unsupported;
- người dùng được hướng vào Portal nếu cần xử lý proposal hiện tại.

Không được vô tình cho legacy public token bypass login requirement.

## 9.5. Test bắt buộc

- tạo proposal mới → `EmailActionTokens` không có `LOGISTICS_PROPOSAL_RESPONSE` mới;
- email proposal chỉ có detail link;
- Host phải đăng nhập để accept/reject;
- proposal decision trong Portal vẫn hoạt động;
- notification Host mở đúng proposal;
- preview = real send action layout.

---

# 10. BUG-08 — P1 — Public email-action renderer đang dùng wording sai context

## 10.1. Hiện trạng

File:

`backend/PEMS.Api/Email/EmailActionHtmlPages.cs`

Renderer hiện chủ yếu phân biệt:

```text
isLogisticsRequest = Context == LOGISTICS_REQUEST_RESPONSE
```

Mọi context khác rơi về wording participant invitation.

Vì vậy `LOGISTICS_ASSIGNEE_RESPONSE` có thể hiển thị:

```text
"lời mời tham gia hỗ trợ tiếp khách"
```

trong khi người dùng thực tế đang phản hồi **nhiệm vụ hậu cần**.

`RenderResult()` cũng suy luận:

```text
Action == DECLINE
→ từ chối

còn lại
→ chấp nhận
```

Đây là model binary quá thô.

Terminal message cũng dùng wording “lời mời” cho nhiều context.

## 10.2. Fix bắt buộc

Tạo context/action presentation mapping thay vì nhiều `if` rải rác.

Ví dụ:

```text
EmailActionPresentation
- Context
- Action
- Title
- Intro
- DetailLabel
- DeclineReasonLabel
- DeclinePlaceholder
- SubmitLabel
- SuccessTitle
- SuccessMessage
- Accent
```

Các public context phải explicit:

```text
PARTICIPATION_RESPONSE
PARTICIPATION_ASSIGNMENT_RESPONSE
LOGISTICS_REQUEST_RESPONSE
LOGISTICS_ASSIGNEE_RESPONSE
```

Proposal không còn public action theo BUG-07.

Không được dùng:

```text
unknown action != ACCEPT
→ coi như DECLINE
```

Unknown context/action phải invalid.

## 10.3. Expected wording

### Direct participant invitation

```text
Xác nhận chấp nhận lời mời
Từ chối lời mời tham gia
```

### Department Staff assignment

```text
Xác nhận nhận nhiệm vụ
Từ chối nhiệm vụ được phân công
```

### Logistics request to Leader

```text
Xác nhận tiếp nhận yêu cầu logistics
Từ chối yêu cầu logistics
```

### Logistics Staff assignment

```text
Xác nhận nhận nhiệm vụ hậu cần
Từ chối nhiệm vụ hậu cần
```

## 10.4. Test bắt buộc

Snapshot/string semantic tests theo `(Context, Action)`:

- không có context logistics nào chứa wording participant invitation;
- không có assignment nào bị gọi nhầm là request của Leader;
- invalid action không render button mutation;
- terminal body cũng context-aware.

---

# 11. BUG-09 — P1 — Email Preview của Logistics Assignee không giống email thật

## 11.1. Hiện trạng

`EmailActionTemplates.cs` khai báo:

```text
LogisticsAssigneeAssignment
→ Accept/Decline
```

Nhưng real send tại:

`AssignRequestAssigneeCommand.cs`

render:

```text
LogisticsAssigneeActionBlock(
    acceptUrl,
    declineUrl,
    detailUrl
)
```

Tức email thật có 3 action:

```text
[Chấp nhận nhiệm vụ]
[Từ chối nhiệm vụ]
[Xem chi tiết]
```

Preview registry hiện mô tả coarse hơn và có thể chỉ preview Accept/Decline.

## 11.2. Fix bắt buộc

Preview action block phải giống action layout real send.

### Tốt nhất

Thay boolean matrix:

```text
HasAcceptDecline
HasAssignLink
HasDetailLink
HasLogisticsAction
...
```

bằng action kind rõ nghĩa:

```text
EmailActionPresentationKind

ParticipantInvitation
DepartmentLeaderInvitation
DepartmentStaffAssignment
LogisticsRequest
LogisticsAssignee
ProposalDetailOnly
VisitReminderDetail
ConfirmEmail
...
```

Preview và real send dùng cùng metadata/kind.

### Minimum acceptable

Nếu chưa refactor registry:

- `LogisticsAssigneeAssignment` phải khai báo có detail;
- disabled preview block phải hiển thị đúng 3 buttons/links với đúng label.

## 11.3. Test bắt buộc

Cho mỗi action template:

```text
preview action labels == real-send action labels
preview action count  == real-send action count
```

Đặc biệt:

- Department Staff assignment;
- Logistics Assignee assignment;
- Logistics Request;
- Proposal detail-only sau fix.

---

# 12. BUG-10 — P1 — One-time token có race window khi Accept/Decline tới đồng thời

## 12.1. Hiện trạng

Email handler hiện:

```text
load token
check PENDING
load target
check target state
...
begin transaction
mutate
ConsumeToken()
BurnSiblings()
Save
```

Token và target được validate trước khi mutation transaction khóa action group.

Có 2 token khác nhau trong cùng `ActionGroupKey`:

```text
Token A = ACCEPT
Token B = DECLINE
```

Hai request đồng thời có thể cùng đọc trạng thái cũ.

## 12.2. Fix bắt buộc

Action group phải được claim/lock atomically.

Một pattern phù hợp MySQL/InnoDB:

```text
1. Hash raw token.
2. Read token đủ để biết ActionGroupKey.
3. BEGIN TRANSACTION.
4. SELECT toàn bộ token của ActionGroupKey
   ORDER BY email_action_token_id
   FOR UPDATE.
5. Tìm selected token trong locked rows.
6. Re-check:
   - selected còn PENDING?
   - UsedAt null?
   - group chưa có SUCCESS?
7. Lock/re-check business target theo cùng lock protocol.
8. Apply business transition.
9. selected token → SUCCESS.
10. siblings pending → ALREADY_RESPONDED/INVALID.
11. Save audit/notification.
12. COMMIT.
```

Không lock token A rồi mới lock token B theo thứ tự khác nhau ở hai request vì dễ deadlock. Lock cả group theo deterministic order.

## 12.3. Cross-channel race

Phải test cả:

```text
Email Accept vs Email Decline
Portal response vs Email response
```

Nếu Portal và Email đi qua shared response service thì dùng cùng lock/transaction protocol.

## 12.4. Test bắt buộc

Integration test với 2 DB connections/request song song:

```text
Accept + Decline cùng lúc
```

Assert:

```text
exactly 1 action success
exactly 1 second response already-responded/invalid
exactly 1 final business state
no contradictory audit
no duplicate business notification
```

---

# 13. BUG-11 — P1/P2 — Email action đang tạo Notification trực tiếp và thiếu semantic routing fields

## 13.1. Hiện trạng

Trong:

`ExecuteEmailActionCommandHandler.cs`

một số flow dùng trực tiếp:

```text
_db.Notifications.Add(new Notification { ... })
```

với dữ liệu tối thiểu.

Trong khi hệ thống đã có:

`INotificationService`

và semantic fields:

- Category
- VisitRequestId
- VisitInstanceId
- ActionType
- ActionUrl
- MetadataJson / eventKey
- DedupeKey khi cần.

## 13.2. Hậu quả

Notification tạo từ email response có thể:

- thiếu đúng destination;
- thiếu event semantic;
- xử lý khác notification tạo từ Portal;
- không được frontend resolver phân loại đúng;
- tái sinh lỗi click notification sai màn.

## 13.3. Fix bắt buộc

Portal response và Email response của cùng business action phải phát cùng semantic notification event.

Ưu tiên:

```text
INotificationService.CreateAsync/CreateManyAsync
```

Không tạo `Notification` bare trực tiếp trong email handler.

Ví dụ participant response phải có:

```text
Category = INVITATION
VisitRequestId / VisitInstanceId
ActionType = OPEN_VISIT_DETAIL hoặc đúng semantic
ActionUrl = canonical builder
MetadataJson = eventKey PARTICIPATION_ACCEPTED / PARTICIPATION_DECLINED
```

Logistics response tương tự dùng logistics event keys hiện có.

## 13.4. Test bắt buộc

Cùng một action thực hiện qua:

```text
Portal
Email
```

phải tạo notification semantic tương đương:

- same event intent;
- same related target;
- same visit instance;
- destination hợp lệ.

---

# 14. BUG-12 — P2 — Production URL config có localhost fallback nguy hiểm cho email direct link

## 14.1. Hiện trạng

`EmailActionTokenService` có fallback:

```text
App:PublicApiBaseUrl ?? http://localhost:5265
App:FrontendBaseUrl  ?? http://localhost:5173
```

Dev dùng fallback là tiện.

Nhưng Production nếu quên environment variable thì application vẫn có thể boot và gửi email chứa localhost URL.

## 14.2. Fix

**Không ảnh hưởng dev convenience.**

Chỉ trong Production startup:

- require `App:PublicApiBaseUrl`;
- require `App:FrontendBaseUrl`;
- URL phải parse được;
- không cho localhost/127.0.0.1;
- ưu tiên HTTPS.

Dev/Testing vẫn giữ fallback hiện tại nếu team muốn.

Đây không phải task rotate credential.

## 14.3. Test

Production config thiếu base URL → startup validation fail với message chỉ nêu key, không in secret.

Development thiếu URL → vẫn chạy fallback.

---

# 15. Refactor mục tiêu — tránh sửa kiểu vá từng handler

Đợt fix này không nên kết thúc bằng nhiều `if` mới ở nhiều file.

## 15.1. Một business action = một transition implementation

Mục tiêu:

```text
VisitParticipantResponseService
    ← Portal
    ← Email

LogisticsAssigneeResponseService
    ← Portal
    ← Email
```

Shared service chịu trách nhiệm business state; entry point chỉ chịu trách nhiệm authentication/token transport.

## 15.2. Email transport và business state phải tách

Email handler:

```text
token lookup
token ownership
token expiry
action-group lock
↓
shared business service
↓
consume/burn token
```

Portal handler:

```text
authenticated current user
authorization
↓
shared business service
↓
invalidate related email tokens
```

## 15.3. Deep-link phải có một canonical builder

Không viết:

```csharp
$"/dashboard/..."
```

lặp khắp handler nếu đó là route nghiệp vụ.

## 15.4. Renderer phải context-driven

Không:

```text
if logistics request else participant
```

Mà map explicit `(Context, Action)`.

---

# 16. Thứ tự triển khai khuyến nghị

## Phase 1 — P0 runtime fix

1. Sửa CSP route email-action.
2. Sửa Staff assignment email `ASSIGNED`.
3. Thêm Staff assignment context/presentation hoặc strict equivalent.
4. Production-like E2E chứng minh Staff Accept/Decline được từ email.

**Gate:** Chưa pass Phase 1 thì không coi screenshot bug đã xong.

## Phase 2 — State consistency

5. `AssignDepartmentStaff` state guard + idempotency.
6. Enforce target Department Staff eligibility.
7. Logistics Portal decline `REJECTED → DECLINED`.
8. Xóa wording/reassign behavior sau Staff logistics decline.
9. Portal logistics response invalidate email token.
10. Shared response service/transaction.

## Phase 3 — Link / proposal / presentation consistency

11. Canonical deep-link service.
12. Fix all broken email detail URLs.
13. Proposal chuyển thành portal-only decision.
14. Update proposal email registry/preview.
15. Context-aware email action renderer.
16. Logistics Assignee preview = real send.

## Phase 4 — Integrity / system-wide hardening

17. ActionGroup concurrency lock.
18. Email response notification semantic parity.
19. Production base URL validation.
20. Regression suite toàn hệ thống.

---

# 17. Test matrix bắt buộc trước khi báo DONE

Agent không được chỉ chạy unit test đang có. Phải thêm/điều chỉnh test để cover matrix dưới đây.

## 17.1. Participation — direct invitation

| Initial | Action | Channel | Expected |
|---|---|---|---|
| INVITED | Accept | Portal | ACCEPTED |
| INVITED | Decline + reason | Portal | DECLINED |
| INVITED | Accept | Email | ACCEPTED |
| INVITED | Decline + reason | Email | DECLINED |
| ACCEPTED | any | Email old token | Already responded |
| DECLINED | any | Email old token | Already responded |
| REMOVED | any | Email | Invalid |
| visit closed/cancelled/started | any | Email | Invalid |

## 17.2. Department Staff assignment

| Initial | Action | Expected |
|---|---|---|
| new Staff participant | Leader assign | ASSIGNED |
| ASSIGNED | Portal Accept | ACCEPTED |
| ASSIGNED | Portal Decline | DECLINED |
| ASSIGNED | Email Accept | ACCEPTED |
| ASSIGNED | Email Decline | DECLINED |
| ACCEPTED | Assign retry | vẫn ACCEPTED / conflict-idempotent, tuyệt đối không ASSIGNED |
| DECLINED | Assign retry | không tự reset ASSIGNED |
| ASSIGNED same assignment | duplicate request | không duplicate uncontrolled token/email |

## 17.3. Logistics Leader request

| Initial | Actor | Action | Expected |
|---|---|---|---|
| REQUESTED | Leader | Accept | ACCEPTED |
| REQUESTED | Leader | Reject + reason | REJECTED |
| REQUESTED | Leader | Assign Staff | ASSIGNED |

## 17.4. Logistics Staff assignment

| Initial | Channel | Action | Expected |
|---|---|---|---|
| ASSIGNED correct Staff | Portal | Accept | ACCEPTED |
| ASSIGNED correct Staff | Portal | Decline + reason | DECLINED terminal |
| ASSIGNED correct Staff | Email | Accept | ACCEPTED |
| ASSIGNED correct Staff | Email | Decline + reason | DECLINED terminal |
| DECLINED | Leader | assign another | blocked |
| Portal responded | old email token | click | Already responded/Invalid, no mutation |
| wrong recipient | Email | any | Invalid |

## 17.5. Proposal

| Event | Expected |
|---|---|
| Create proposal | CHANGE_PROPOSED |
| Proposal email | detail link only |
| New proposal token rows | none |
| Host not logged in | cannot mutate proposal |
| Host logged in | can accept/reject via Portal |
| Preview | detail-only like real email |

## 17.6. CSP/browser

| Case | Expected |
|---|---|
| GET email action Production-like | styled HTML |
| Accept form | POST reaches backend |
| Decline form | POST reason reaches backend |
| normal JSON API | strict CSP unchanged |
| mail scanner GET | no mutation |

## 17.7. Concurrency

- Accept vs Decline email simultaneous.
- Portal Accept vs Email Decline simultaneous.
- Double-click POST same token.
- Retry AssignDepartmentStaff.
- Exactly one terminal state.

---

# 18. Acceptance criteria — agent chỉ được báo hoàn thành khi tất cả đúng

## Functional

- [ ] Screenshot lỗi CSS/form không còn.
- [ ] Department Staff `ASSIGNED` Accept/Decline được từ email.
- [ ] Department Staff `ASSIGNED` Accept/Decline được từ Portal.
- [ ] Portal/Email cùng final participant state.
- [ ] `ACCEPTED` không bị reset về `ASSIGNED`.
- [ ] Logistics Staff decline Portal = `DECLINED`.
- [ ] Logistics Staff decline Email = `DECLINED`.
- [ ] `DECLINED` logistics là terminal; không reassign.
- [ ] Portal logistics response đóng email token cũ.
- [ ] Proposal email không mint public Approve/Reject token.
- [ ] Proposal Host xử lý trong Portal.
- [ ] Email direct detail links đều trỏ route frontend tồn tại.
- [ ] Visit reminder direct link không dùng route 2-id sai.
- [ ] Logistics assignee preview giống email thật.
- [ ] Public email-action wording đúng context.

## Integrity

- [ ] One action group chỉ được consume một verdict.
- [ ] Sibling token bị burn/invalidate đúng.
- [ ] Cross-channel response không mutate lần hai.
- [ ] Audit không mâu thuẫn.
- [ ] Notification semantic có destination đúng.
- [ ] Không duplicate uncontrolled email/token khi retry assignment.

## Security

- [ ] GET không mutate.
- [ ] CSP chỉ nới đúng public email-action HTML route.
- [ ] Không mở script.
- [ ] Không relax CSP toàn API.
- [ ] Proposal mutation vẫn yêu cầu login.
- [ ] Không thay đổi dev credentials trong task này.

## Quality gates

- [ ] Backend build pass.
- [ ] Frontend build pass.
- [ ] Unit tests pass.
- [ ] Integration tests pass.
- [ ] Production-like email-action test pass.
- [ ] Real-stack/browser E2E cho các flow chính pass.

---

# 19. Các file agent phải rà tối thiểu

## Backend — Public email action

- `backend/PEMS.Api/Middleware/SecurityHeadersMiddleware.cs`
- `backend/PEMS.Api/Controllers/PublicEmailActionsController.cs`
- `backend/PEMS.Api/Email/EmailActionHtmlPages.cs`
- `backend/PEMS.Application/EmailActions/GetEmailActionInfoQueryHandler.cs`
- `backend/PEMS.Application/EmailActions/ExecuteEmailActionCommandHandler.cs`
- `backend/PEMS.Application/EmailActions/EmailTokenInvalidationHelper.cs`
- email action DTO/constants/result files liên quan.

## Backend — Participation/Department assignment

- `backend/PEMS.Application/Delegations/Commands/AssignDepartmentStaff/AssignDepartmentStaffCommandHandler.cs`
- `backend/PEMS.Application/Delegations/Common/VisitInvitationResponse.cs`
- Department Accept/Decline invitation handlers.
- Participant status/constants.
- Assignment/invitation integration tests.

## Backend — Logistics

- `backend/PEMS.Application/DepartmentReceptionTasks/Commands/AssignRequestAssignee/AssignRequestAssigneeCommand.cs`
- `backend/PEMS.Application/DepartmentReceptionTasks/Commands/AcceptAssignedLogisticsTask/AcceptAssignedLogisticsTaskCommand.cs`
- `backend/PEMS.Application/DepartmentReceptionTasks/Commands/DeclineAssignedLogisticsTask/DeclineAssignedLogisticsTaskCommand.cs`
- `backend/PEMS.Application/DepartmentReceptionTasks/Commands/ProposeRequestChange/ProposeRequestChangeCommand.cs`
- Host proposal confirmation/rejection handlers.
- `GetAssignmentsProgressList` read model.
- Logistics status constants/entities/attempts.

## Backend — Email composition / URL

- `backend/PEMS.Infrastructure/Email/EmailActionTokenService.cs`
- `backend/PEMS.Application/Emails/Common/EmailActionTemplates.cs`
- `backend/PEMS.Application/Emails/Common/EmailComposition.cs`
- preview/approved-content handlers.
- reminder email sender.

## Frontend

- `frontend/pems-react/src/App.tsx`
- Department shared dashboard/task views.
- logistics task/detail components.
- notification destination resolver.
- email preview UI.
- any one-shot task query-param handling.

## Tests

Repo-wide search:

```text
DepartmentStaffAssignment
LogisticsEmailEndToEnd
EmailAction
SecurityHeaders
VisitReminder
EmailPreview
resolveNotificationDestination
AssignRequestAssignee
AcceptAssignedLogisticsTask
DeclineAssignedLogisticsTask
ProposeRequestChange
```

---

# 20. Source inconsistencies agent phải xử lý, không được copy doc cũ mù quáng

Hiện tài liệu:

`docs/business-rules/department-task-logistics-email-token-flow.md`

có các phần đúng với requirement logistics:

- Staff assignment logistics: `ASSIGNED → ACCEPTED/DECLINED`.
- `REJECTED` dành cho Leader.
- Proposal Host xử lý trong hệ thống, không public token.

Nhưng cùng tài liệu đó còn matrix participant nói:

```text
ASSIGNED + ACCEPT/DECLINE → INVALID
```

Trong khi business requirement đã chốt cho **Department Staff được Leader giao lời mời tham gia** là:

```text
ASSIGNED → ACCEPTED / DECLINED
```

Vì vậy agent phải:

1. phân biệt direct invitation và delegated Staff assignment;
2. không áp matrix `ASSIGNED = INVALID` lên Staff assignment;
3. cập nhật lại tài liệu business rule sau khi code được sửa để không tiếp tục mâu thuẫn.

---

# 21. Deliverables cuối cùng agent phải trả lại

Agent khi hoàn thành phải báo rõ:

1. **Root causes** đã sửa.
2. **Files changed**.
3. **State transitions before/after**.
4. **Email action contexts before/after**.
5. **Deep-link mappings before/after**.
6. **Tests added/updated**.
7. **Commands đã chạy + kết quả**.
8. **Có migration DB hay không**.
9. **Legacy token behavior**.
10. **Các phần cố tình không sửa**:
    - không reassign logistics sau Staff decline;
    - không rotate/remove dev credentials.
11. Commit message đề xuất.

Không báo “done” chỉ vì build pass; phải chứng minh các acceptance criteria ở mục 18.

---

# 22. Commit message gợi ý

Nếu gom thành một commit lớn:

```text
fix(email-actions): align staff and logistics response flows end to end
```

Nếu chia patch:

```text
fix(email-actions): allow assigned staff responses and repair production CSP
fix(logistics): align staff decline state and invalidate stale email tokens
fix(email-links): use canonical frontend routes for outbound email actions
fix(logistics): make proposal decisions portal-only
test(email-actions): add production and cross-channel regression coverage
```
