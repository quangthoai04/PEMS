# PEMS — FIX PROMPT: EDITABLE DETAIL DRIFT FOR PRE-DECISION VISIT REQUESTS

## 0. Mục tiêu

Fix bug trên **Dev mới nhất** khiến màn danh sách hiển thị nút **Sửa đơn**, nhưng khi người đăng ký bấm vào thì trang edit trả:

```text
Đơn không còn ở trạng thái có thể sửa. Vui lòng quay lại.
```

Case đã tái hiện:

```text
Request:
PENDING_CONTACT_CONFIRMATION

Campuses:
- WAITING_CONTACT_CONFIRMATION
- WAITING_REQUEST_APPROVAL
```

Đây vẫn là trạng thái **pre-decision**: chưa campus nào được Staff Leader approve/reject.

Theo policy hiện tại:
- người đăng ký vẫn được sửa request;
- list đã trả `EDIT_PENDING_REQUEST`;
- command update thực tế cũng đã cho phép;
- chỉ `GetEditableVisitRequestDetailQueryHandler` còn giữ rule cũ.

---

# 1. Preflight

Trước khi sửa:

```bash
git branch --show-current
git rev-parse HEAD
git status --short
git diff --stat
git diff --check
```

Baseline Dev đã được kiểm tra trước đó:

```text
ceaebfdb552f96ac730f120d40b7a2842054fbd2
Merge branch 'Duy-Iter1' into Dev
```

Nhưng **không hard reset về SHA này** nếu Dev đã tiến lên.

Không chạy:

```text
git reset --hard
git restore .
git clean
git stash
```

Giữ nguyên working tree hiện tại.

---

# 2. Root cause cần fix

File:

```text
backend/PEMS.Application/Delegations/Queries/
GetEditableVisitRequestDetail/
GetEditableVisitRequestDetailQueryHandler.cs
```

Logic cũ hiện tương đương:

```csharp
var isEditablePending =
    visit.Status == VisitRequestStatuses.PendingApproval
    && instances.Count > 0
    && instances.All(i => i.Status == VisitInstanceStatus.WaitingRequestApproval)
    && instances.Min(i => i.PlannedStartAt) >= vnNow.AddHours(24);
```

Logic này sai ở 2 điểm:

```text
A. Lifecycle drift
Chỉ chấp nhận:
PENDING_APPROVAL + toàn WAITING_REQUEST_APPROVAL

Trong khi canonical policy hiện cho cả:
PENDING_CONTACT_CONFIRMATION
PENDING_APPROVAL

và campus:
WAITING_CONTACT_CONFIRMATION
WAITING_REQUEST_APPROVAL
```

```text
B. Time-policy drift
Handler hardcode 24 giờ.

Canonical mutation cutoff hiện là:
VisitMutationPolicy.RequiredLeadHours = 6

72 giờ là:
VisitMutationPolicy.MinScheduleLeadHours
= minimum lead time của LỊCH MỚI được submit,
KHÔNG phải thời hạn để mở action sửa.
```

---

# 3. Source of truth phải dùng

Không tạo thêm một predicate thứ tư.

Phải reuse canonical:

```text
VisitMutationPolicy
VisitMutationGuard
VisitMutationAction.EditPendingRequest
```

Command hiện tại:

```text
UpdatePendingVisitRequestV2CommandHandler
```

đã dùng đúng pattern:

```csharp
VisitMutationGuard.EnsureRequestLevelAllowed(
    VisitMutationAction.EditPendingRequest,
    visit,
    now,
    c => c.Status is VisitInstanceStatuses.WaitingContactConfirmation
                  or VisitInstanceStatuses.WaitingRequestApproval,
    VisitRequestErrorCodes.VisitRequestNotEditable);
```

Editable-detail query phải align với cùng semantics.

---

# 4. Implementation yêu cầu

## 4.1 Không dùng predicate cũ

Loại bỏ logic kiểu:

```text
visit.Status == PendingApproval
all campus == WaitingRequestApproval
AddHours(24)
```

Không thay `24` bằng `6` rồi giữ nguyên lifecycle cũ.

Phải sửa cả lifecycle lẫn cutoff.

---

## 4.2 Pending edit eligibility đúng

Whole-request pending edit chỉ tồn tại khi:

```text
request status:
PENDING_CONTACT_CONFIRMATION
hoặc
PENDING_APPROVAL
```

và:

```text
mọi campus đều:
WAITING_CONTACT_CONFIRMATION
hoặc
WAITING_REQUEST_APPROVAL
```

và:

```text
mutation cutoff chưa đạt
theo VisitMutationPolicy.RequiredLeadHours
```

---

## 4.3 Preferred implementation

Ưu tiên gọi canonical guard thay vì duplicate logic.

Thiết kế mong muốn:

```text
1. Xác định mode candidate:
   - pending edit
   - resubmit
   - none

2. Nếu pending-edit candidate:
   gọi canonical guard trực tiếp.
   Nếu guard refuse:
   để exception canonical đi ra ngoài,
   không biến thành generic "không editable".

3. Nếu resubmit:
   giữ canonical resubmit behavior hiện có.

4. Nếu không thuộc mode nào:
   trả VisitRequestNotEditable.
```

Mục tiêu là:
- list;
- editable-detail GET;
- update command

phải trả cùng một verdict cho cùng state/time.

---

# 5. Preserve resubmit behavior

Không làm hỏng flow:

```text
request = REJECTED
all campuses = REJECTED
```

`Mode = RESUBMIT`

Không mở resubmit cho:
- cancelled;
- partially decided;
- closed;
- active visit;
- campus chưa rejected đồng nhất nếu current business rule không cho.

Nếu resubmit hiện có canonical guard/service riêng, reuse nó.

Không mở rộng scope ngoài bug này.

---

# 6. Không được phá topology rule

Whole-request pending edit chỉ được dùng **trước khi có bất kỳ campus decision nào**.

Không cho phép request-level edit nếu một campus đã vào:

```text
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
REJECTED
CANCELLED
```

trừ khi action thuộc flow khác đã được thiết kế riêng.

Đặc biệt:

```text
mixed decision:
1 campus approved
1 campus waiting
```

không được dùng whole-request edit để add/remove/replace topology.

Pending campus sau sibling decision phải dùng flow `EditPendingCampus` nếu policy hiện hỗ trợ.

---

# 7. Phân biệt 6 giờ và 72 giờ

Đây là invariant bắt buộc.

## 7.1 `RequiredLeadHours = 6`

Dùng để trả lời:

```text
"Còn được mở action sửa hay không?"
```

Ví dụ:

```text
now = 10:00
current planned start = 16:00
=> exact T-6
=> action edit vẫn open
```

Nếu:

```text
start = 15:59
=> < 6h
=> edit action closed
```

---

## 7.2 `MinScheduleLeadHours = 72`

Dùng để validate:

```text
"Lịch MỚI user đang nhập có được phép submit không?"
```

Không dùng 72 giờ để quyết định có được mở editor hay không.

Không dùng 6 giờ để cho phép user submit lịch mới cách hiện tại 10 giờ.

Ví dụ đúng:

```text
Current visit starts in 10h
→ editor có thể vẫn mở vì > 6h

User đổi start time thành +20h
→ submit phải fail vì < 72h

User đổi start time thành +80h
→ submit có thể pass nếu các rule khác hợp lệ
```

---

# 8. Error behavior

Nếu action bị block vì cutoff:

Không trả generic:

```text
Đơn không còn ở trạng thái có thể sửa.
```

nếu canonical `VisitMutationGuard` đã cung cấp:

```text
VISIT_MUTATION_CUTOFF_REACHED
CutoffAt
PlannedStartAt
RequiredLeadHours
```

Giữ structured error để frontend có thể hiển thị đúng nguyên nhân.

Nếu action bị block vì lifecycle:
- giữ stable domain error hiện có;
- không parse string ở frontend.

---

# 9. Tests bắt buộc

## 9.1 Editable detail — mixed pre-decision

Case:

```text
request = PENDING_CONTACT_CONFIRMATION

campus A = WAITING_CONTACT_CONFIRMATION
campus B = WAITING_REQUEST_APPROVAL

earliest start > now + 6h
registrant = current user
```

Expected:

```text
GET editable detail = success
Mode = EDIT
IsEditablePending = true
```

Đây là regression test quan trọng nhất.

---

## 9.2 Fully waiting approval

```text
request = PENDING_APPROVAL
all campuses = WAITING_REQUEST_APPROVAL
start > now + 6h
```

Expected:

```text
success
Mode = EDIT
```

---

## 9.3 Exact boundary T-6

```text
earliest start = VietnamNow + 6h
```

Expected:

```text
editable
```

---

## 9.4 Inside cutoff

```text
earliest start < VietnamNow + 6h
```

Expected:

```text
refused
VISIT_MUTATION_CUTOFF_REACHED
```

Nếu current API maps exception khác, giữ canonical mapping đang dùng bởi `VisitMutationGuard`.

---

## 9.5 Campus already decided

Ví dụ:

```text
request = PARTIALLY_APPROVED
campus A = ASSIGNED
campus B = WAITING_REQUEST_APPROVAL
```

Expected:

```text
whole-request edit refused
```

Không được regress thành editable.

---

## 9.6 Rejected resubmit

```text
request = REJECTED
all campuses = REJECTED
```

Expected:

```text
Mode = RESUBMIT
IsResubmittable = true
```

---

## 9.7 72-hour proposed schedule validation vẫn giữ

Thêm/giữ test ở submit path:

```text
editor mở được vì current schedule > 6h
nhưng proposed new start < 72h
→ submit rejected
```

Mục tiêu: chứng minh Agent không trộn 6h và 72h.

---

# 10. Parity test giữa list / detail / command

Bổ sung test hoặc ít nhất regression audit chứng minh cùng một state:

```text
PENDING_CONTACT_CONFIRMATION
[WAITING_CONTACT_CONFIRMATION, WAITING_REQUEST_APPROVAL]
> T-6
```

cho kết quả:

```text
List:
EDIT_PENDING_REQUEST present

Editable Detail:
200 / Mode EDIT

Update Command:
accepted nếu payload hợp lệ
```

Không để 3 layer tự duy trì 3 luật khác nhau.

---

# 11. Frontend

Không sửa frontend để ẩn nút cây bút cho case hợp lệ này.

Frontend hiện render action từ backend `allowedActions`, đây là hướng đúng.

Chỉ sửa frontend nếu:
- cần map structured cutoff error tốt hơn;
- có test chứng minh message hiện tại che mất canonical reason.

Không hardcode frontend lifecycle để "đồng bộ" bằng tay.

Backend vẫn là source of truth.

---

# 12. Files dự kiến

Primary:

```text
backend/PEMS.Application/Delegations/Queries/
GetEditableVisitRequestDetail/
GetEditableVisitRequestDetailQueryHandler.cs
```

Tests liên quan query:

```text
tests/PEMS.UnitTests/...
```

hoặc exact existing test location hiện có.

Có thể cần update:
- shared mutation-policy tests;
- editable-detail tests;
- list/detail parity tests.

Không mở rộng sang SQL.

Bug này **không cần migration DB**.

---

# 13. Không sửa SQL

Không cần:

```text
ALTER TABLE
UPDATE seed
reseed
canonical SQL patch
```

Lỗi nằm ở application read-model policy drift.

Không tạo SQL patch cho fix này.

---

# 14. Regression audit

Search:

```bash
rg -n "AddHours\(24\)|PendingApproval|WaitingRequestApproval|EditPendingRequest|RequiredLeadHours|MinScheduleLeadHours"   backend/PEMS.Application/Delegations   backend/PEMS.Domain   tests
```

Mục tiêu audit:

```text
- không còn hardcoded 24h trong editable-detail path;
- pending-edit lifecycle dùng cả 2 pre-decision statuses;
- không có read model khác vẫn promise/refuse khác command;
- 6h và 72h không bị trộn.
```

Không xóa `24` ở unrelated business rule nếu nó thực sự có semantics khác.

---

# 15. Gates

Chạy:

```bash
dotnet build backend/PEMS.Domain/PEMS.Domain.csproj
dotnet build backend/PEMS.Application/PEMS.Application.csproj
dotnet build backend/PEMS.Infrastructure/PEMS.Infrastructure.csproj
dotnet build backend/PEMS.Api/PEMS.Api.csproj
```

Expected:

```text
PASS
0 errors
```

Chạy full backend UnitTests.

Expected:

```text
0 failed
```

Nếu frontend không sửa thì vẫn chạy tối thiểu:

```bash
cd frontend/pems-react
npm run lint
npm run build
```

Nếu frontend có sửa:
- chạy frontend UnitTests liên quan;
- ưu tiên full unit suite nếu khả thi.

Cuối cùng:

```bash
git diff --check
git status --short
git diff --stat
git diff --name-only
```

---

# 16. Gates được waive

Report đúng:

```text
Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner
```

Không ghi PASS.

Nếu real-stack không có:

```text
Real-stack:
NOT RUN — environment unavailable
```

---

# 17. Report format bắt buộc

```text
## Preflight

Branch:
HEAD before:
Working tree before:

## Root cause

Old editable-detail rule:
Canonical rule:
Why list showed edit but editor refused:

## Implementation

Files changed:
Lifecycle fix:
Cutoff fix:
Guard/policy reused:
Any frontend change:

## Behavior matrix

1. PENDING_CONTACT_CONFIRMATION +
   WAITING_CONTACT_CONFIRMATION/WAITING_REQUEST_APPROVAL
   =>

2. PENDING_APPROVAL +
   all WAITING_REQUEST_APPROVAL
   =>

3. exact T-6
   =>

4. inside T-6
   =>

5. one campus already decided
   =>

6. fully rejected
   =>

## 6h vs 72h verification

Action-open cutoff:
Proposed schedule floor:
Regression evidence:

## Tests

New/updated tests:
UnitTests:
Frontend tests if any:

## Gates

Domain build:
Application build:
Infrastructure build:
Api build:
Backend UnitTests:
Frontend typecheck:
Frontend build:
git diff --check:

Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner

Real-stack:

## SQL

SQL changes required:
NO

## Remaining issues

List any remaining in-scope issue.
If none:
NONE

## Git

Branch:
HEAD after:
Commit created:
Working tree after:
```

---

# 18. Definition of Done

Chỉ báo hoàn thành khi:

- [ ] Case ảnh thực tế mở editor thành công.
- [ ] `PENDING_CONTACT_CONFIRMATION` được coi là editable pre-decision.
- [ ] `WAITING_CONTACT_CONFIRMATION` được coi là pre-decision campus state.
- [ ] `WAITING_REQUEST_APPROVAL` vẫn hoạt động.
- [ ] Không còn hardcoded `AddHours(24)` trong editable-detail pending-edit gate.
- [ ] Whole-request edit dùng canonical `VisitMutationPolicy/Guard`.
- [ ] Exact T-6 được phép.
- [ ] < T-6 bị chặn.
- [ ] 72h proposed schedule validation vẫn giữ.
- [ ] Request có campus đã quyết định không được whole-request edit.
- [ ] Resubmit rejected không regress.
- [ ] List / Detail / Command cùng verdict.
- [ ] Backend builds PASS.
- [ ] Backend UnitTests PASS.
- [ ] `git diff --check` PASS.
- [ ] Không SQL change.
- [ ] Không disable test.
- [ ] Không commit secret/debug artifact.

Khi đạt toàn bộ mới báo:

```text
EDITABLE DETAIL POLICY DRIFT FIX COMPLETE
```

Nếu còn lệch giữa list/detail/command thì không báo complete.
