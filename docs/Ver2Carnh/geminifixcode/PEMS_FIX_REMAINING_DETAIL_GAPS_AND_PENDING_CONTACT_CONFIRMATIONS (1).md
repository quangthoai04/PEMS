# PEMS — FIX NỐT CÁC GAP CÒN LẠI SAU FULL-DETAIL NOTES AUDIT

## Mục tiêu

Làm việc trên code mới nhất của nhánh `Dev`, đồng thời **bảo toàn toàn bộ local work chưa commit hiện tại** của task:

> Chuẩn hóa hiển thị đầy đủ `Ghi chú gửi FPTU` ở tất cả màn hình xem đơn.

Task trước đã hoàn thành phần Notes và hiện working tree đang chứa các thay đổi chưa commit.

Nhiệm vụ lần này là xử lý nốt **các gap còn lại đã được audit xác nhận**, gồm:

1. `SharedDashboardView` chưa render đầy đủ 5 trường **Đầu mối đoàn khách phối hợp tại cơ sở** dù backend DTO đã trả dữ liệu.
2. `Dev` đang có 10 TypeScript errors + 3 frontend test failures do rename:
   `pendingConfirmations` → `pendingContactConfirmations`.
3. Audit lần cuối để chắc chắn các full-detail surface không còn thiếu trường form quan trọng nào sau khi fix 2 mục trên.

Không mở rộng scope sang refactor lớn, database schema, email flow hoặc các feature khác.

---

# 1. PREFLIGHT — BẮT BUỘC

Trước khi sửa:

```bash
git status --short
git branch --show-current
git rev-parse HEAD
git fetch origin Dev
git rev-parse origin/Dev
git log --oneline HEAD..origin/Dev
git stash list
```

Ghi lại:

```text
Branch:
HEAD:
origin/Dev:
Working tree:
Existing stashes:
```

## Yêu cầu an toàn

Hiện working tree có local work chưa commit từ task Notes.

Không được:

```bash
git reset --hard
git clean -fd
git checkout .
git restore .
```

Không được làm mất bất kỳ file local nào.

Nếu `origin/Dev` đã tiến lên:

1. Lập danh sách incoming commits/files.
2. Lập overlap matrix với local changes.
3. Tạo safety backup rõ tên trước khi sync.
4. Merge/reapply theo semantics.
5. Không dùng ours/theirs cơ học.
6. Không đụng các stash cũ.

Nếu `HEAD == origin/Dev`, không cần tạo stash chỉ để cho có.

---

# 2. BẢO TOÀN TASK NOTES ĐÃ XONG

Không được rollback các thay đổi vừa hoàn thành.

Business rule phải tiếp tục đúng:

```text
General Notes = visit_instance_form_details.notes
```

Label:

```text
Ghi chú gửi FPTU
```

Các full-detail surface phải:

```text
notes có giá trị → hiện giá trị
notes rỗng/null → hiện —
```

Không được trộn với:

```text
transportation_note
decision_note
cancellation_reason
VisitParticipant.Note
ProposalNote
BorrowNote
ReturnNote
```

Các trạng thái:

```text
Normal
Rejected
Cancelled
Closed
Multi-campus
```

phải tiếp tục pass.

---

# 3. GAP 1 — SHAREDDASHBOARDVIEW THIẾU ĐẦU MỐI PHỐI HỢP

## Hiện trạng

Backend DTO đã có đủ 5 trường:

```text
OperationalContactFullName
OperationalContactOrganization
OperationalContactJobTitle
OperationalContactPhone
OperationalContactEmail
```

Nhưng `SharedDashboardView` chưa render chúng ở các detail surface của Department.

Các flow cần audit ít nhất:

```text
Department invitation detail
Department logistics request detail
```

Mục tiêu:

> Khi Department mở chi tiết đơn/nhiệm vụ, phải thấy đầy đủ đầu mối đoàn khách phối hợp tại đúng campus của nhiệm vụ đó.

---

# 4. SOURCE OF TRUTH CHO OPERATIONAL CONTACT

Pure V2 source:

```text
visit_instance_form_details
```

Các cột:

```text
operational_contact_full_name
operational_contact_organization
operational_contact_job_title
operational_contact_phone
operational_contact_email
```

Không dùng:

```text
visit_requests registrant
request-level primary contact
sibling campus contact
host
coordinator
```

Target instance phải được xác định từ chính invitation/logistics item.

Ví dụ:

```text
logistics item
→ visit_instance_id
→ target VisitInstanceFormDetail
→ OperationalContact*
```

---

# 5. UI CHUẨN CHO ĐẦU MỐI

Ở `SharedDashboardView`, thêm một block riêng rõ nghĩa:

```text
ĐẦU MỐI ĐOÀN KHÁCH PHỐI HỢP TẠI CƠ SỞ
```

Hiển thị:

```text
Họ và tên
Đơn vị công tác
Chức vụ
Số điện thoại
Email
```

Không gộp vào:

```text
Người tạo
Người giao
Người nhận nhiệm vụ
Host
Điều phối viên
```

Đây là một person relation riêng.

---

# 6. EMPTY VALUE BEHAVIOR

Nếu một trường optional không có dữ liệu:

```text
—
```

Không xóa cả block.

Ví dụ:

```text
Họ và tên: Kim Min Jae
Đơn vị công tác: SeoulTech Global Engagement Center
Chức vụ: International Partnerships Manager
Số điện thoại: —
Email: kim.minjae@...
```

Nếu toàn bộ dữ liệu operational contact bất thường đều rỗng do inconsistency, vẫn render block với placeholder thay vì silently hide.

Không tạo fallback sang người khác.

---

# 7. DEPARTMENT INVITATION DETAIL

Audit backend:

```text
DepartmentReceptionTasks/GetInvitationDetail
InvitationDetailDto
```

Nếu DTO hiện đã có đủ 5 field thì **không đổi backend contract**.

Frontend:

```text
SharedDashboardView.tsx
```

Phải render block Operational Contact khi mở invitation detail.

Đảm bảo không nhầm:

```text
Invitation.Note
```

với:

```text
NotesToFptu
```

và không nhầm operational contact với sender/inviter.

---

# 8. DEPARTMENT LOGISTICS REQUEST DETAIL

Audit backend:

```text
DepartmentReceptionTasks/GetRequestDetail
RequestDetailDto
```

Nếu DTO đã có đủ:

```text
OperationalContactFullName
OperationalContactOrganization
OperationalContactJobTitle
OperationalContactPhone
OperationalContactEmail
```

thì chỉ sửa frontend.

Frontend:

```text
SharedDashboardView.tsx
```

Phải render đầu mối ở phần thông tin đoàn/yêu cầu, không đặt lẫn trong block:

```text
Hậu cần
Đề xuất thay đổi
Biên bản bàn giao
Người được giao
```

---

# 9. TEST CHO GAP OPERATIONAL CONTACT

Tạo fixture:

```text
OperationalContactFullName = "Kim Min Jae"
OperationalContactOrganization = "SeoulTech Global Engagement Center"
OperationalContactJobTitle = "International Partnerships Manager"
OperationalContactPhone = "+821012340001"
OperationalContactEmail = "kim.minjae@seoultech.example"
```

## Case A — Department invitation detail

Expected UI có đủ:

```text
ĐẦU MỐI ĐOÀN KHÁCH PHỐI HỢP TẠI CƠ SỞ
Kim Min Jae
SeoulTech Global Engagement Center
International Partnerships Manager
+821012340001
kim.minjae@seoultech.example
```

## Case B — Department logistics request detail

Cùng assertion như trên.

## Case C — empty optional fields

Expected:

```text
Số điện thoại: —
```

hoặc placeholder tương đương hiện tại.

## Case D — multi-campus

HN:

```text
Contact HN
```

HCM:

```text
Contact HCM
```

Mở task HN chỉ được thấy HN.

Không cross-campus leakage.

---

# 10. GAP 2 — FIX RENAME pendingConfirmations → pendingContactConfirmations

Remote `Dev` hiện có regression do field được đổi tên:

```text
pendingConfirmations
→
pendingContactConfirmations
```

Nhưng các caller/test chưa được cập nhật hết.

Task này cần sửa dứt điểm.

---

# 11. AUDIT RENAME TRƯỚC KHI SỬA

Search toàn repo:

```bash
rg -n "pendingConfirmations|pendingContactConfirmations" frontend backend tests
```

Phân loại tất cả hits:

```text
A. Canonical/current field = pendingContactConfirmations
B. Stale production caller = pendingConfirmations
C. Stale tests/fixtures = pendingConfirmations
D. Historical docs only
```

Không blindly replace trong docs/migration nếu đó là historical record không ảnh hưởng runtime.

---

# 12. SOURCE OF TRUTH CHO RENAME

Xác nhận DTO/API mới nhất thực sự dùng:

```text
pendingContactConfirmations
```

Nếu đúng, frontend phải align theo field mới.

Không tạo compatibility alias kiểu:

```ts
pendingConfirmations ?? pendingContactConfirmations
```

trừ khi API thật sự đang phải support hai contract version.

Với Dev hiện tại, mục tiêu là **một contract canonical duy nhất**.

---

# 13. PRODUCTION CALLERS CẦN FIX

Audit ít nhất file đã được báo lỗi:

```text
useVisitRequestFormV2.ts
```

và mọi caller khác từ `rg`.

Ví dụ stale code:

```ts
response.pendingConfirmations
```

đổi thành:

```ts
response.pendingContactConfirmations
```

Chỉ đổi field access, không thay business logic.

---

# 14. TESTS / FIXTURES CẦN FIX

Audit ít nhất các suite đã được báo:

```text
VisitRequestV2SubmittedSummary.test.tsx
visitRequestV2SuccessScreen.test.tsx
visitRequestV2SubmissionStage.test.tsx
VisitRequestV2DraftUx.test.tsx
```

và mọi fixture/helper liên quan.

Nếu fixture đang có:

```ts
pendingConfirmations: [...]
```

đổi thành:

```ts
pendingContactConfirmations: [...]
```

Không sửa expected behavior ngoài việc align contract.

---

# 15. KHÔNG ĐƯỢC LÀM SAI BUSINESS RULE CONFIRMATION

Tên field mới phải tiếp tục mang nghĩa:

> Danh sách các campus đang chờ **Operational Contact confirmation**.

Không được biến nó thành:

```text
request-level confirmation
host confirmation
email confirmation
registrant confirmation
```

Per-campus semantics phải giữ nguyên.

---

# 16. TEST CASES CHO RENAME

## Case A — response có pendingContactConfirmations

Frontend đọc đúng số lượng và render đúng UI.

## Case B — empty list

```json
{
  "pendingContactConfirmations": []
}
```

Không crash.

## Case C — multi-campus

Ví dụ:

```text
HN pending
HCM confirmed
```

UI chỉ hiện pending đúng HN.

## Case D — no stale property access

Search sau sửa:

```bash
rg -n "pendingConfirmations" frontend/pems-react/src frontend/pems-react/tests
```

Expected:

```text
0 runtime/test hits
```

Ngoại trừ historical comment/doc nếu có lý do hợp lệ.

---

# 17. FIX 10 TYPESCRIPT ERRORS

Sau rename fix, chạy:

```bash
cd frontend/pems-react
npx tsc --noEmit
```

Mục tiêu:

```text
0 errors
```

Nếu còn errors:

1. So sánh với pristine `origin/Dev`.
2. Phân loại.
3. Nếu cùng root cause rename này → fix nốt.
4. Nếu unrelated → báo riêng, không mở rộng scope.

Nhưng theo audit hiện tại, 10 errors đã được chứng minh cùng root cause rename này nên task lần này phải đưa chúng về 0.

---

# 18. FIX 3 FRONTEND TEST FAILURES

Chạy targeted trước:

```text
VisitRequestV2SubmittedSummary.test.tsx
visitRequestV2SuccessScreen.test.tsx
visitRequestV2SubmissionStage.test.tsx
VisitRequestV2DraftUx.test.tsx
```

Xác định chính xác 3 failure nào do stale field.

Fix fixtures/assertions để dùng contract mới.

Không tăng timeout chỉ để che failure.

Không đổi business expectation.

---

# 19. FULL DETAIL FINAL AUDIT

Sau khi fix 2 gap chính, audit nhanh các detail surface một lần cuối.

Mục tiêu:

> Không còn trường form quan trọng nào backend đã trả nhưng frontend full-detail lại không render.

Audit ít nhất:

```text
VisitProcess
VisitRequestDetail
VisitorVisitDetailPage
StaffVisitDetailModal
SubmittedVisitRequestDetailModal
VisitRequestV2DetailView
VisitContributionPage
VisitParticipantInvitationDetail
SharedDashboardView department invitation
SharedDashboardView logistics request
```

Lập matrix:

| Surface | Purpose | WorkingContent | Transportation | NotesToFptu | OperationalContact | Status metadata |
|---|---:|---:|---:|---:|---:|---:|

Không cần đưa full fields vào LIST/summary rows nếu đó không phải detail surface.

---

# 20. UNUSED GetVisitInvitationDetail SURFACE

Hiện đã biết:

```text
GetVisitInvitationDetail (/visit-invitations/{id})
```

có thể chưa có frontend caller.

Không tạo frontend mới chỉ để dùng endpoint này.

Chỉ verify:

```text
NotesToFptu mapping không sai
contract compile
tests pass
```

Không thêm `Purpose/WorkingContent` hoặc mở rộng API nếu không có consumer thực tế.

---

# 21. GIỮ NGUYÊN EMAIL / SETUP-PROGRESS CHANGES

Task này không được ảnh hưởng các thay đổi email trước đó:

```text
Schedule Report = default optional attachment
Drive failure non-blocking
EmailRichTextEditor source === 'user'
```

Nếu merge/sync chạm các file email, phải giữ semantics hiện tại.

Không restore:

```text
mandatory Schedule Report
email_drafts
locked report requirement
```

---

# 22. TESTS BẮT BUỘC — FRONTEND

Targeted:

```text
SharedDashboardView related tests
Department invitation detail tests
Department logistics request detail tests
VisitRequestV2SubmittedSummary.test.tsx
visitRequestV2SuccessScreen.test.tsx
visitRequestV2SubmissionStage.test.tsx
VisitRequestV2DraftUx.test.tsx
```

Sau đó:

```bash
npx tsc --noEmit
npm run lint
npm run test
npm run build
```

Dùng command thực tế của repo nếu scripts khác.

---

# 23. TESTS BẮT BUỘC — BACKEND

Backend chỉ cần thay nếu audit phát hiện DTO/project mapping chưa đủ.

Chạy targeted:

```text
DeptInvitationDetailV2Tests
RequestDetailV2Tests
MyVisitInvitationByIdV2Tests
VisitProcessDetailV2Tests
```

Sau đó:

```text
dotnet build
PEMS.UnitTests
PEMS.IntegrationTests
```

Giữ full green baseline đã đạt:

```text
Unit: 2384
Integration: 1622
```

Nếu test count tăng do test mới thì báo count mới.

---

# 24. ACCEPTANCE CRITERIA — OPERATIONAL CONTACT

Department invitation:

```text
open detail
→ full operational contact block visible
```

Department logistics:

```text
open detail
→ full operational contact block visible
```

Multi-campus:

```text
task HN → HN contact only
task HCM → HCM contact only
```

Empty optional:

```text
field remains visible with —
```

---

# 25. ACCEPTANCE CRITERIA — PENDING CONTACT CONFIRMATIONS

Sau sửa:

```text
tsc --noEmit → 0 errors
```

Search:

```text
pendingConfirmations
```

không còn stale runtime/test caller.

UI flow:

```text
create/submit V2
→ response.pendingContactConfirmations
→ success/submission/draft UX đọc đúng
→ tests pass
```

Không có compatibility hack nếu không cần.

---

# 26. ACCEPTANCE CRITERIA — FULL DETAIL

Sau final audit:

```text
Purpose               ✓
Working Content       ✓
Transportation Note   ✓
Notes to FPTU         ✓
Operational Contact   ✓
Cancellation metadata ✓ when cancelled
Decision metadata     ✓ when rejected
```

Không có surface full-detail nào:

```text
backend có field
frontend im lặng bỏ field
```

trừ field bị ẩn vì authorization/scope có chủ đích và có bằng chứng.

---

# 27. KHÔNG ĐƯỢC LÀM

Không:

- tạo bảng mới;
- migration schema;
- request-level Notes;
- clone form data;
- đổi authorization;
- đổi lifecycle/status;
- sửa email flow;
- refactor `SharedDashboardView` toàn bộ;
- replace field names trong historical SQL/docs một cách mù quáng;
- compatibility alias không cần thiết;
- tăng timeout để che test failure;
- bỏ test;
- skip typecheck;
- reset local work;
- xóa stash cũ;
- push.

---

# 28. ƯU TIÊN IMPLEMENTATION

Thứ tự:

```text
1. Preflight + fetch
2. Protect local work
3. Audit SharedDashboardView operational contact
4. Implement operational-contact UI
5. Add targeted tests
6. Audit pendingConfirmations rename
7. Fix all stale callers/fixtures
8. Run targeted TS/tests
9. Run final full-detail matrix audit
10. Run full frontend gates
11. Run backend gates
12. Report
```

Không làm rộng hơn.

---

# 29. BÁO CÁO CUỐI

Báo cáo đúng format:

## 1. Preflight

```text
Branch:
HEAD before:
origin/Dev:
Working tree:
Safety backup:
Existing stashes:
```

## 2. Gap 1 — Operational Contact

```text
Backend DTO status:
Frontend before:
Frontend after:
Files changed:
```

## 3. Gap 2 — pendingContactConfirmations

```text
Canonical field:
Stale callers found:
Production files fixed:
Tests/fixtures fixed:
Remaining pendingConfirmations hits:
```

## 4. Final full-detail coverage matrix

| Surface | Notes | Transportation | Operational Contact | Cancellation/Decision | Result |
|---|---|---|---|---|---|

## 5. Tests

```text
Operational-contact targeted:
V2 rename targeted:
Frontend full:
Backend targeted:
Backend full:
```

## 6. Gates

```text
backend build:
unit:
integration:
frontend typecheck:
frontend lint:
frontend vitest:
frontend build:
```

## 7. Regression checks

Confirm:

```text
General Notes semantics preserved:
Multi-campus isolation:
Cancelled/rejected detail:
Email setup-progress unaffected:
```

## 8. Remaining gaps

Chỉ ghi gap thật sự còn tồn tại.

## 9. Git

```text
HEAD after:
Files changed:
Working tree:
Commit:
Push:
```

Không commit/push nếu chưa được yêu cầu.
