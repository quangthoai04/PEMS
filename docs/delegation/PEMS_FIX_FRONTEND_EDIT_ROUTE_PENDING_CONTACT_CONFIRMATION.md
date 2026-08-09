# PEMS — FRONTEND CLOSURE PROMPT
## Fix `EditVisitRequestV2Page` rejecting `PENDING_CONTACT_CONFIRMATION`

## 0. Mục tiêu

Fix bug còn lại trên **Dev mới nhất**:

```text
/dashboard/visit/v2/{visitRequestId}/edit
```

Màn danh sách đã hiển thị đúng nút **Sửa đơn** và backend editable-detail/update policy đã được sửa, nhưng frontend vẫn hiện:

```text
Đơn không còn ở trạng thái có thể sửa. Vui lòng quay lại.
```

Case thực tế:

```text
requestStatus = PENDING_CONTACT_CONFIRMATION

campuses:
- WAITING_CONTACT_CONFIRMATION
- WAITING_REQUEST_APPROVAL

actor:
- registrant của request
```

Expected:

```text
route /edit mở form bình thường
không render statusMismatch
```

Bug hiện tại nằm ở **frontend lifecycle gate cũ**, không phải SQL.

---

# 1. Baseline / Preflight

Dev remote đã được kiểm tra tại:

```text
d2b9ad39889fcc7d634d076dd43c687307f6a153
fix: align editable visit detail
```

Nhưng tại thời điểm thực thi phải lấy HEAD thật:

```bash
git branch --show-current
git rev-parse HEAD
git status --short
git diff --stat
git diff --check
```

Expected branch:

```text
Dev
```

Không hard reset về SHA trên nếu branch đã tiến lên.

Không chạy:

```text
git reset --hard
git restore .
git clean
git stash
```

Không discard thay đổi ngoài scope.

---

# 2. Root cause đã xác định

Primary file:

```text
frontend/pems-react/src/pages/dashboard/visit/EditVisitRequestV2Page.tsx
```

Hiện page có local gate:

```ts
const EDITABLE_STATUSES = new Set([
  'PENDING_APPROVAL',
  'PENDING',
]);

const RESUBMITTABLE_STATUSES = new Set([
  'REJECTED',
]);
```

Sau khi fetch:

```ts
const data = await getVisitRequestFormV2(id);
```

page tự check:

```ts
const editableForMode =
  mode === 'edit'
    ? EDITABLE_STATUSES.has(data.requestStatus)
    : RESUBMITTABLE_STATUSES.has(data.requestStatus);
```

Với:

```text
data.requestStatus = PENDING_CONTACT_CONFIRMATION
```

thì:

```text
EDITABLE_STATUSES.has(...) = false
→ setStatusMismatch(true)
→ render edit.notEditable
```

Do đó UI tự chặn **sau khi backend đã trả form thành công**.

---

# 3. Vì sao fix backend trước chưa đủ

Hiện verdict đang là:

```text
List backend
→ EDIT_PENDING_REQUEST
→ ALLOW

Editable-detail/backend policy
→ PENDING_CONTACT_CONFIRMATION là pre-decision
→ ALLOW

UpdatePendingVisitRequestV2Command
→ PENDING_CONTACT_CONFIRMATION +
   WAITING_CONTACT_CONFIRMATION / WAITING_REQUEST_APPROVAL
→ ALLOW

EditVisitRequestV2Page
→ local EDITABLE_STATUSES cũ
→ DENY
```

Đây là frontend policy drift.

Không sửa backend lại chỉ để chiều frontend.

---

# 4. Fix bắt buộc

## 4.1 `PENDING_CONTACT_CONFIRMATION` phải là edit-compatible request status

Trong edit mode, request status hợp lệ phải bao gồm tối thiểu:

```text
PENDING_CONTACT_CONFIRMATION
PENDING_APPROVAL
```

Nếu `PENDING` là legacy compatibility status vẫn còn được API thực tế trả về thì giữ.

Không tự xóa `PENDING` chỉ vì không thấy trong canonical enum nếu chưa audit callers/fixtures/backward compatibility.

Minimal correction:

```ts
const EDITABLE_STATUSES = new Set([
  'PENDING_CONTACT_CONFIRMATION',
  'PENDING_APPROVAL',
  'PENDING',
]);
```

Nhưng không dừng ở việc đổi đúng 1 dòng mà không thêm regression test.

---

# 5. Không duplicate thêm campus lifecycle ở frontend

Không thêm logic kiểu:

```ts
data.campuses.every(c =>
  c.status === 'WAITING_CONTACT_CONFIRMATION' ||
  c.status === 'WAITING_REQUEST_APPROVAL'
)
```

vào page chỉ để "chắc chắn".

Backend đã là authority cho:
- relation;
- campus lifecycle;
- mutation cutoff;
- whole-request topology;
- concurrency;
- actual write acceptance.

Frontend route gate chỉ nên phân biệt **route mode compatibility**:

```text
/edit      → pending-edit request status
/resubmit  → rejected request status
```

Không được rebuild `VisitMutationPolicy` bằng TypeScript.

---

# 6. Preferred structure để giảm drift

Nếu repo đã có shared frontend helper cho request lifecycle/action compatibility thì reuse helper đó.

Nếu chưa có, có thể tạo một helper nhỏ, ví dụ:

```text
frontend/pems-react/src/features/visit-request/utils/
visitRequestEditRoutePolicy.ts
```

hoặc đặt cạnh page/test theo convention hiện tại.

Helper chỉ trả lời:

```text
requestStatus có tương thích với route mode không?
```

Ví dụ semantics:

```ts
export const isPendingEditRequestStatus = (status?: string | null) =>
  status === 'PENDING_CONTACT_CONFIRMATION'
  || status === 'PENDING_APPROVAL'
  || status === 'PENDING'; // chỉ giữ nếu legacy alias còn cần

export const isResubmitRequestStatus = (status?: string | null) =>
  status === 'REJECTED';
```

Không đưa:
- cutoff 6h;
- schedule floor 72h;
- campus status;
- relation;
- topology;
- rowVersion

vào helper này.

Nếu việc tạo helper là overkill theo style repo hiện tại, sửa local Set cũng được, nhưng regression tests bắt buộc.

---

# 7. Preserve backend as final authority

Frontend **không được** coi việc status nằm trong Set là chứng minh user có quyền sửa.

Flow đúng:

```text
GET form/read model
→ HTTP 403/404/422/etc. từ backend phải được tôn trọng

Nếu fetch thành công:
→ frontend chỉ check route mode compatibility

Submit:
→ backend revalidate toàn bộ policy
```

Không thêm bypass kiểu:

```text
if PENDING_CONTACT_CONFIRMATION → force render form dù GET lỗi
```

Không swallow backend 403/422.

---

# 8. Relation check — audit, không widen

Hiện page có logic tương đương:

```ts
const isManager =
  data.viewer.relation === 'REGISTRANT'
  || data.viewer.relation === 'VISITOR_OWNER';
```

Trong pass này phải **audit** với backend write policy hiện tại.

Backend whole-request pending edit hiện được mô tả/guarded là **registrant-owned request-level mutation**.

Yêu cầu:

```text
- Không mở rộng quyền cho contact owner chỉ vì frontend từng gọi họ là manager.
- Nếu backend chỉ cho REGISTRANT, frontend route nên không hứa quyền rộng hơn.
- Nếu `VISITOR_OWNER` là alias backend read model dùng cho chính registrant trong legacy response,
  giữ compatibility và ghi rõ bằng test/comment.
```

Không thay đổi authorization semantics khi chưa đọc exact DTO/handler contract.

Nếu audit xác nhận `VISITOR_OWNER` có thể là non-registrant contact owner:
- sửa frontend để không hydrate edit form cho họ;
- thêm regression test.

Nếu audit xác nhận nó chỉ là legacy alias của registrant:
- giữ;
- document.

---

# 9. Regression tests bắt buộc

Existing test area đã có:

```text
frontend/pems-react/src/features/visit-request/__tests__/
EditVisitRequestV2Page.test.tsx
```

Ưu tiên bổ sung vào test hiện có thay vì tạo suite trùng chức năng.

## T01 — bug thực tế

Mock:

```text
mode = edit
requestStatus = PENDING_CONTACT_CONFIRMATION
viewer relation = REGISTRANT
GET resolves successfully
```

Expected:

```text
form render
edit.notEditable NOT visible
statusMismatch false by observable behavior
```

Nên assert một field/form heading thực sự xuất hiện.

---

## T02 — `PENDING_APPROVAL` vẫn edit được

```text
mode = edit
requestStatus = PENDING_APPROVAL
```

Expected:

```text
form render
```

---

## T03 — legacy `PENDING` nếu còn support

Nếu audit xác nhận `PENDING` vẫn cần:

```text
mode = edit
requestStatus = PENDING
→ form render
```

Nếu không còn support:
- không tự xóa trong pass này nếu việc đó mở scope;
- ghi technical debt rõ trong report.

---

## T04 — `REJECTED` không mở bằng `/edit`

```text
mode = edit
requestStatus = REJECTED
```

Expected:

```text
edit.notEditable visible
form hidden
```

---

## T05 — `REJECTED` mở bằng resubmit

```text
mode = resubmit
requestStatus = REJECTED
```

Expected:

```text
resubmit form render
```

---

## T06 — approved/cancelled không mở pending edit

Ít nhất một hoặc cả hai:

```text
APPROVED
CANCELLED
```

Expected:

```text
status mismatch UI
no editable form
```

---

## T07 — backend refusal vẫn thắng

Mock GET reject:

```text
403
hoặc canonical backend refusal
```

Expected:

```text
không render form
không dùng local status để bypass
show mapped backend/forbidden error
```

---

## T08 — relation parity

Theo kết quả audit §8:
- REGISTRANT được render khi status hợp lệ;
- non-owner/non-registrant bị chặn;
- `VISITOR_OWNER` behavior phải khớp backend contract, không tự phỏng đoán.

---

# 10. Test end-to-end parity ở frontend boundary

Phải có ít nhất một test mô phỏng đúng case người dùng vừa gặp:

```text
GET resolved form:
{
  requestStatus: "PENDING_CONTACT_CONFIRMATION",
  viewer: { relation: "REGISTRANT" },
  campuses: [...]
}

render:
<EditVisitRequestV2Page mode="edit" />
```

Expected:

```text
NO:
"Đơn không còn ở trạng thái có thể sửa. Vui lòng quay lại."

YES:
edit form + "Lưu thay đổi"
```

Đây là test mà pass backend trước còn thiếu.

---

# 11. Không sửa các policy khác

Không thay đổi trong pass này:

```text
VisitMutationPolicy.RequiredLeadHours = 6
VisitMutationPolicy.MinScheduleLeadHours = 72
UpdatePendingVisitRequestV2CommandHandler
GetEditableVisitRequestDetailQueryHandler
operational-contact confirmation workflow
approval/reject concurrency
SQL
email templates
```

Trừ khi test chứng minh một regression trực tiếp cần minimal correction.

---

# 12. Không sửa SQL

Fix này là frontend policy drift.

Expected:

```text
SQL changes required: NO
Migration required: NO
Runtime DB patch required: NO
```

Không chạy canonical SQL.

Không reseed DB.

---

# 13. Audit stale frontend status gates

Search toàn frontend:

```bash
rg -n \
  "EDITABLE_STATUSES|PENDING_CONTACT_CONFIRMATION|PENDING_APPROVAL|statusMismatch|edit\\.notEditable|RESUBMITTABLE_STATUSES" \
  frontend/pems-react/src
```

Mục tiêu:
- tìm các local status gates liên quan edit/resubmit;
- xác nhận không còn page/helper khác cũng loại `PENDING_CONTACT_CONFIRMATION`;
- không sửa unrelated status display/filter nếu không có drift.

Nếu phát hiện thêm một gate cùng action:
- align trong cùng pass;
- thêm test;
- report exact file/reason.

Không biến task thành refactor toàn bộ status system.

---

# 14. UI wording

Không cần đổi câu:

```text
Đơn không còn ở trạng thái có thể sửa. Vui lòng quay lại.
```

chỉ vì bug này.

Thông báo đó vẫn đúng cho:
- APPROVED;
- CANCELLED;
- lifecycle incompatible với edit route.

Bug là **nó đang được dùng cho một status hợp lệ**.

---

# 15. Frontend gates

Chạy tại:

```text
frontend/pems-react
```

Bắt buộc:

```bash
npm run lint
npm run build
npm run test:unit
```

Nếu `npm run lint` thực chất là:

```text
tsc --noEmit
```

report:

```text
Frontend typecheck: PASS
Frontend ESLint: NOT AVAILABLE
```

Không gọi typecheck là ESLint.

Nếu full unit suite có timeout/flaky:
- report lần đầu;
- rerun clean;
- không che giấu;
- không sửa global timeout chỉ để xanh nếu không cần.

---

# 16. Backend regression gate

Vì task chủ yếu frontend và backend vừa được sửa trước đó, tối thiểu compile/check backend liên quan để chắc working tree không phá:

```bash
dotnet build backend/PEMS.Application/PEMS.Application.csproj
dotnet build backend/PEMS.Api/PEMS.Api.csproj
```

Nếu thời gian/environment cho phép, chạy full UnitTests.

Không cần DB-backed tests cho frontend fix này.

---

# 17. Waived gates

Report đúng:

```text
Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner
```

Không ghi PASS.

Nếu real stack không chạy:

```text
Real-stack:
NOT RUN — environment unavailable
```

---

# 18. Manual verification nếu localhost đang chạy

Nếu có frontend + backend + DB local thật đang chạy, verify đúng case:

```text
Visitor/Registrant
Request 2003
/dashboard/visit/v2/2003/edit
```

Expected:

```text
form mở
không còn banner notEditable
dữ liệu request/campuses hydrate
Lưu thay đổi visible
```

Nếu không có real stack:

```text
Manual localhost:
NOT RUN — environment unavailable
```

Không giả PASS từ unit test.

---

# 19. Working-tree hygiene

Sau fix:

```bash
git status --short
git diff --stat
git diff --check
git diff --name-only
```

Không commit:
- prompt `.md` chỉ dùng để hướng dẫn agent nếu Project Owner không yêu cầu;
- `node_modules`;
- `dist`;
- coverage;
- debug logs;
- screenshots;
- secrets.

Không disable test.

---

# 20. Expected files

Primary expected:

```text
frontend/pems-react/src/pages/dashboard/visit/EditVisitRequestV2Page.tsx

frontend/pems-react/src/features/visit-request/__tests__/
EditVisitRequestV2Page.test.tsx
```

Optional nếu cần shared helper:

```text
frontend/pems-react/src/features/visit-request/utils/
<small route-policy helper>.ts

frontend tests cho helper nếu convention repo yêu cầu
```

Không cần SQL file.

Không cần backend domain policy change.

---

# 21. Report format bắt buộc

```text
## Preflight

Branch:
HEAD before:
Working tree before:

## Root cause

Frontend gate:
Backend current verdict:
Why backend fix did not fix UI:

## Implementation

Files changed:
EDITABLE_STATUSES before:
EDITABLE_STATUSES after:
Shared helper created:
Backend policy duplicated in FE:
YES / NO

## Relation audit

REGISTRANT:
VISITOR_OWNER:
Backend write-policy parity:
Any authorization change:

## Behavior matrix

PENDING_CONTACT_CONFIRMATION + edit:
PENDING_APPROVAL + edit:
PENDING + edit:
REJECTED + edit:
REJECTED + resubmit:
APPROVED + edit:
CANCELLED + edit:
backend GET refusal:

## Tests

Existing test file updated:
New test count:
Bug reproduction test:
Frontend UnitTests:

## Gates

Frontend typecheck:
Frontend build:
Frontend UnitTests:
Frontend ESLint:

Application build:
Api build:

git diff --check:

Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner

Real-stack:

## SQL

SQL changes:
NO

Migration:
NO

Runtime patch:
NO

## Manual localhost

Request 2003:
PASS / NOT RUN + reason

## Remaining issues

If none:
NONE

## Git

Branch:
HEAD after:
Commit created:
Working tree after:
```

---

# 22. Definition of Done

Chỉ báo complete khi:

- [ ] `PENDING_CONTACT_CONFIRMATION` mở được `/edit`.
- [ ] `PENDING_APPROVAL` không regress.
- [ ] `REJECTED` vẫn chỉ đi resubmit.
- [ ] APPROVED/CANCELLED không được mở pending edit.
- [ ] Backend refusal không bị frontend bypass.
- [ ] Không duplicate campus lifecycle/cutoff policy sang frontend.
- [ ] Relation gate được audit với backend.
- [ ] Regression test đúng case 2003 được thêm.
- [ ] `edit.notEditable` không xuất hiện cho `PENDING_CONTACT_CONFIRMATION`.
- [ ] Frontend typecheck PASS.
- [ ] Frontend build PASS.
- [ ] Frontend UnitTests PASS.
- [ ] Application/API build PASS.
- [ ] `git diff --check` PASS.
- [ ] Không SQL change.
- [ ] Không migration.
- [ ] Không disable test.
- [ ] Không secret/debug artifact.

Chỉ khi đạt toàn bộ mới báo:

```text
FRONTEND EDIT ROUTE POLICY DRIFT FIX COMPLETE
```

Nếu UI vẫn tự từ chối `PENDING_CONTACT_CONFIRMATION`, không báo complete.
