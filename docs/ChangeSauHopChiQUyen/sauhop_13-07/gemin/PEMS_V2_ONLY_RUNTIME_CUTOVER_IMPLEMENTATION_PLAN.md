# PEMS — Kế hoạch triển khai V2-Only Runtime

> **Mục tiêu:** Chuyển toàn bộ module Visit Request/Delegation của PEMS sang sử dụng Per-Campus Form V2 làm runtime duy nhất, đồng thời bảo toàn toàn bộ hành vi tốt từng có ở V1 như validation, toast, OTP, dirty guard, loading, chống double-submit, error handling, concurrency, audit, notification và authorization.
>
> **Phạm vi:** Frontend + Backend + Tests + Tài liệu triển khai.
>
> **Không thuộc phạm vi:** Drop 10 cột legacy trong database, chạy Phase I contract-drop, deploy production, thay đổi business rule đã khóa.
>
> **Trạng thái database đầu vào đã xác nhận:**
>
> - `non_v2_requests = 0`
> - `missing_form_details = 0`
> - `orphan_form_details = 0`
> - toàn bộ truy vấn `issue_count` ở cuối SQL full đều bằng `0`

---

# 1. Kết luận điều hành

Database hiện đã đủ điều kiện để bắt đầu V2-only runtime cutover.

Việc cần làm tiếp theo không phải là xóa V1 ngay lập tức, mà là:

1. Audit toàn bộ behavior V1/V2.
2. Đưa validation/toast/error handling tốt của V1 thành shared infrastructure.
3. Chuyển toàn bộ route, CTA, modal và API caller sang V2.
4. Retire backend mutation V1.
5. Chuyển toàn bộ reader/consumer sang canonical per-campus data.
6. Chạy full regression và real-stack.
7. Chỉ sau khi parity đầy đủ mới xóa runtime code V1.
8. Giữ lại 10 cột compatibility để xử lý trong một Phase I riêng.

Trạng thái cuối mong muốn:

```text
Runtime: V2-ONLY
Frontend V1: RETIRED
Backend V1 mutation: RETIRED
Legacy DB columns: RETAINED TEMPORARILY
Contract-drop: NOT PART OF THIS IMPLEMENTATION
```

---

# 2. Nguyên tắc bắt buộc

## 2.1. Không làm trên nhánh Dev

Repository:

```text
GitHub: quangthoai04/PEMS
Branch local: Canh-Iter1
Remote: origin/Cảnh-Iter1
```

Không được:

- code trực tiếp trên `Dev`;
- reset/rebase/amend commit đã push;
- push, merge hoặc tạo PR khi chưa được yêu cầu;
- thêm AI attribution vào commit;
- sửa global Git config.

Commit author/committer:

```text
Tcanh12 <canhnvthe186121@fpt.edu.vn>
```

## 2.2. Không drop schema V1 trong lượt này

Không xóa:

```text
visit_requests.delegation_name
visit_requests.visit_type
visit_requests.visit_type_other
visit_requests.purpose
visit_requests.working_content
visit_requests.working_language
visit_requests.transportation_note
visit_requests.media_consent_status
visit_requests.media_consent_note
visit_requests.note_to_fptu
```

Các field này tạm thời chỉ còn là compatibility projection.

Canonical source V2:

```text
visit_request_campuses
visit_instance_form_details
visit_guest_members
visit_instance_guest_members
```

## 2.3. Không xóa V1 trước khi chứng minh parity

Không được xóa component/handler V1 chỉ vì database đã là V2.

Phải chứng minh V2 đã giữ đầy đủ:

- validation;
- toast;
- field error;
- form-state preservation;
- loading;
- double-submit guard;
- dirty guard;
- OTP;
- draft;
- concurrency;
- audit;
- notification;
- authorization;
- error mapping;
- lifecycle handling.

---

# 3. Git preflight

Trước khi sửa code, chạy:

```bash
git status --short --branch
git branch --show-current
git remote -v
git fetch --all --prune
git rev-parse HEAD
git rev-parse origin/Cảnh-Iter1
git rev-list --left-right --count origin/Cảnh-Iter1...HEAD
git log --oneline --decorate --graph -30
git diff --check
```

Xác minh:

- đang ở đúng branch;
- local/remote không có divergence bất thường;
- không có staged changes ngoài scope;
- không ghi đè file người dùng đang sửa;
- không có prompt/handoff untracked bị add nhầm.

Nếu Git identity chưa đúng:

```bash
git config user.name "Tcanh12"
git config user.email "canhnvthe186121@fpt.edu.vn"
```

Chỉ sửa repository-local config.

---

# 4. Baseline trước thay đổi

## 4.1. Backend

Chạy theo solution thực tế:

```bash
dotnet build
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.ArchitectureTests
dotnet test tests/PEMS.IntegrationTests
```

Ghi lại:

| Gate | Command | Passed | Failed | Skipped | Exit code |
|---|---|---:|---:|---:|---:|
| Build | | | | | |
| Unit | | | | | |
| Architecture | | | | | |
| Integration | | | | | |

## 4.2. Frontend

Đọc `package.json`, sau đó dùng đúng script thật:

```bash
npm run typecheck
npm run lint
npm run test
npm run build
```

Không tự đoán tên script nếu project dùng tên khác.

## 4.3. Database

Xác nhận lại:

```sql
SELECT COUNT(*) AS non_v2_requests
FROM visit_requests
WHERE form_schema_version IS NULL
   OR form_schema_version < 2;

SELECT COUNT(*) AS missing_form_details
FROM visit_request_campuses vrc
LEFT JOIN visit_instance_form_details vifd
  ON vifd.visit_instance_id = vrc.visit_instance_id
WHERE vifd.visit_instance_id IS NULL;

SELECT COUNT(*) AS orphan_form_details
FROM visit_instance_form_details vifd
LEFT JOIN visit_request_campuses vrc
  ON vrc.visit_instance_id = vifd.visit_instance_id
WHERE vrc.visit_instance_id IS NULL;
```

Tất cả phải bằng `0`.

---

# 5. Phase A — Inventory frontend V1/V2

## 5.1. Tìm toàn bộ component/hook/router liên quan

Tìm tối thiểu:

```text
VisitRequestV2Modal
VisitRequestV2Page
EditVisitRequestV2Page
VisitRequestV2DetailView
CampusVisitCard
CampusVisitDetailCard
CreateVisitRequestEntry
resolveVisitRowRoutes
isPerCampusV2
perCampusV2Entry
usePerCampusV2Capability
PerCampusV2CapabilityProvider
EditVisitRequest.tsx
V1 modal/popup
V1 API clients
V1 validation schemas
V1 draft keys
V1 toast/error handlers
```

## 5.2. Audit tất cả entry point

Bắt buộc kiểm tra:

- Homepage Hero CTA;
- Homepage Final CTA;
- FAQ CTA;
- Partner CTA;
- Dashboard tạo đơn;
- Visit Request Management;
- Public registration;
- Authenticated registration;
- Đóng góp đoàn;
- Search result;
- Notification deep link;
- Calendar;
- Invitation;
- Host detail;
- Department detail;
- HO detail;
- Report detail.

Tạo bảng:

| Call site | File/symbol | Hiện dùng | Cách xác định version | Fallback | Kết quả đích |
|---|---|---|---|---|---|
| Hero CTA | | V1/V2 | capability/version | | V2 |
| FAQ CTA | | V1/V2 | | | V2 |
| Partner CTA | | V1/V2 | | | V2 |
| Dashboard | | V1/V2 | | | V2 |

Không để call site chưa phân loại.

---

# 6. Phase B — Inventory backend V1/V2

Tìm và phân loại:

## 6.1. Mutation V1

- create authenticated;
- public initiate;
- public verify;
- pending edit;
- rejected resubmit;
- draft mutation;
- flat-form mutation.

## 6.2. Mutation V2

- authenticated create V2;
- public initiate V2;
- public verify V2;
- pending edit V2;
- resubmit V2;
- safe edit;
- amendment;
- identity claim;
- identity transfer;
- cancel 3A.

## 6.3. Reader/consumer

- request detail;
- instance detail;
- list;
- search;
- dashboard;
- report;
- export;
- invoice;
- email;
- notification;
- background jobs;
- calendar;
- invitation;
- contribution;
- logistics;
- meeting minutes;
- feedback.

Tạo bảng:

| Endpoint/consumer | V1/V2 | Source hiện tại | Caller | Quyết định |
|---|---|---|---|---|
| Public create | | | | RETIRE_V1 / KEEP_V2 |
| Pending edit | | | | |
| Invoice | | | | MIGRATE_TO_V2 |
| Search | | | | KEEP_V2 |
| Email builder | | | | MIGRATE_TO_V2 |

Giá trị quyết định:

```text
KEEP_V2
MIGRATE_TO_V2
RETIRE_V1
KEEP_TOMBSTONE_410
COMPATIBILITY_PROJECTION_ONLY
DELETE_AFTER_PARITY
BLOCKED
```

---

# 7. Phase C — V1/V2 behavior parity audit

Tạo tài liệu:

```text
docs/ChangeSauHopChiQUyen/sauhop_13-07/
V1_V2_RUNTIME_BEHAVIOR_PARITY.md
```

## 7.1. Validation parity

Kiểm tra V1 và V2 có đầy đủ:

### Request-level

- registrant name;
- organization;
- nationality;
- job title;
- phone;
- email;
- primary contact;
- partner;
- source;
- ownership relation.

### Per-campus

- campus bắt buộc;
- campus ACTIVE;
- không duplicate campus;
- SINGLE đúng 1 campus;
- MULTI từ 2 campus trở lên;
- thời gian bắt đầu;
- thời gian kết thúc;
- duration >= 30 phút;
- advance window 72h khi create;
- advance window 24h khi edit/resubmit;
- delegation name;
- visit type;
- OTHER bắt buộc `visitTypeOther`;
- purpose;
- working content;
- visitors;
- support members;
- operational contact;
- working language;
- transportation note;
- media consent;
- note to FPTU;
- maximum lengths;
- không HTML/script;
- rowVersion;
- immutable fields;
- lifecycle.

Phải kiểm tra ở cả:

```text
Frontend schema
Backend FluentValidation
Backend service/business rules
Database constraints
```

## 7.2. UX parity

Kiểm tra:

- toast thành công;
- toast lỗi;
- inline field error;
- focus field lỗi đầu tiên;
- scroll tới campus card lỗi;
- mở accordion chứa lỗi;
- không đóng modal khi API lỗi;
- không reset form;
- giữ campus/member data;
- loading khi submit;
- disable nút;
- chống double-click;
- dirty form guard;
- confirm remove campus;
- confirm copy/apply all;
- empty state;
- retry state;
- responsive;
- không hiện raw exception.

## 7.3. OTP parity

Kiểm tra:

- initiate;
- OTP modal;
- countdown;
- resend;
- cooldown;
- expired;
- invalid OTP;
- rate limit;
- idempotent replay;
- pending snapshot binding;
- submission mismatch;
- giữ nguyên form state;
- không provision primary contact trước claim.

## 7.4. Error parity

Frontend phải map bằng `errorCode`, không match `message`.

Tối thiểu:

```text
VISIT_FORM_VALIDATION_FAILED
VISIT_DURATION_TOO_SHORT
DUPLICATE_CAMPUS
VISIT_REQUEST_VERSION_CONFLICT
VISIT_INSTANCE_VERSION_CONFLICT
VISIT_FORM_CONCURRENCY_CONFLICT
VISIT_FORM_DETAIL_MISSING
VISIT_REQUEST_NOT_PER_CAMPUS_V2
PER_CAMPUS_V2_PENDING_NOT_FOUND
PER_CAMPUS_V2_SUBMISSION_FORM_MISMATCH
PER_CAMPUS_V2_READ_REQUIRED
OTP_INVALID_OR_EXPIRED
OTP_RATE_LIMITED
AMENDMENT_ALREADY_PENDING
AMENDMENT_BASE_REVISION_CONFLICT
AMENDMENT_WINDOW_EXPIRED
IDENTITY_CHANGE_ALREADY_PENDING
IDENTITY_CHANGE_EXPIRED
IDENTITY_CHANGE_CONFLICT
CONTACT_ACCOUNT_NOT_ACTIVE
CONTACT_EMAIL_INTERNAL_ACCOUNT_CONFLICT
```

HTTP behavior:

| HTTP | Hành vi UI |
|---:|---|
| 400 | map field errors, giữ form |
| 401 | auth flow, không mất draft |
| 403 | toast không có quyền |
| 404 | không tìm thấy/không có scope |
| 409 | conflict, không silent overwrite |
| 410 | V1 retired |
| 422 | lifecycle không hợp lệ |
| 429 | rate-limit + retry |
| 500 | lỗi chung, không lộ stack trace |

## 7.5. Backend parity

Kiểm tra V2 giữ:

- idempotency;
- transaction;
- rollback;
- optimistic concurrency;
- row lock;
- audit;
- notification sau commit;
- dedupe;
- authorization;
- scope-before-query;
- hidden-campus non-leak;
- first-create-only notification;
- replay không re-notify;
- copy-on-write;
- sibling no-op;
- history/revision;
- stable error contract.

Mỗi dòng phải có trạng thái:

```text
PASS
MISSING
PARTIAL
REGRESSION
NOT_APPLICABLE
```

Không xóa V1 trước khi mọi behavior cần giữ đều `PASS`.

---

# 8. Phase D — Shared frontend infrastructure

Không copy nguyên component V1 sang V2.

Tách hoặc tái sử dụng:

```text
features/visit-request/shared/
├── validation/
│   ├── visitFormRules.ts
│   ├── normalizeVisitForm.ts
│   ├── visitFieldPaths.ts
│   └── mapServerValidationErrors.ts
├── errors/
│   ├── visitErrorCodes.ts
│   ├── getVisitErrorMessage.ts
│   └── handleVisitApiError.ts
├── hooks/
│   ├── useSubmitGuard.ts
│   ├── useDirtyFormGuard.ts
│   ├── useFirstInvalidField.ts
│   └── useVisitFormToast.ts
└── constants/
```

Yêu cầu:

1. V2 create/edit/resubmit dùng chung normalization.
2. Public/authenticated dùng chung campus validation.
3. Toast/error map theo `errorCode`.
4. API lỗi không reset form.
5. API lỗi không đóng modal.
6. Double-submit bị chặn.
7. Dirty guard hoạt động ở route và modal.
8. Field error xác định đúng campus/member.
9. Reuse behavior tốt từ V1.
10. Không refactor ngoài module nếu không cần.

---

# 9. Phase E — Frontend V2-only cutover

## 9.1. Loại bỏ silent V1 fallback

Logic đích:

```text
Capability loading
→ disable CTA + loading

Capability error
→ toast + retry

Capability enabled
→ mở V2

Capability disabled trong V2-only runtime
→ deployment misconfigured
→ không mở V1
```

Tuyệt đối không:

```text
loading → V1
error → V1
outside provider → V1
missing version → V1
```

## 9.2. Capability chỉ còn diagnostic

Sau cutover:

- không dùng capability để chọn V1/V2;
- frontend luôn dùng V2;
- capability chỉ cho biết:
  - backend sẵn sàng;
  - deployment config lỗi;
  - trạng thái loading/error.

## 9.3. Chuyển toàn bộ CTA

Tất cả điểm tạo đơn phải mở:

```text
VisitRequestV2Modal
hoặc
VisitRequestV2Page
```

## 9.4. Sửa `resolveVisitRowRoutes()`

Logic đích:

```text
formSchemaVersion >= 2
→ V2 routes

formSchemaVersion NULL/missing
→ UNSUPPORTED_VISIT_FORM_VERSION

formSchemaVersion < 2
→ retired/unsupported
→ không mở V1
```

Không suy đoán version dựa trên:

- số campus;
- mixed flag;
- route;
- status;
- cache;
- old payload.

## 9.5. Chuyển shared-modal call sites

Không để V2 row mở flat V1 modal rồi chờ backend trả 409.

Audit:

- HO detail;
- invitation;
- host;
- department;
- calendar;
- notification;
- report;
- search result.

## 9.6. V2 form state rules

Giữ:

- stable client keys;
- `useFieldArray`;
- đóng accordion không unregister;
- deep-copy;
- campus A không mutate B;
- member arrays độc lập;
- copy/apply-all có confirm;
- draft key V2 riêng;
- sticky submit;
- dirty prompt;
- textarea auto-expand;
- responsive card/table.

---

# 10. Phase F — Backend V2-only cutover

## 10.1. Retire mutation V1

Tìm exact V1 endpoint rồi trả:

```http
410 Gone
```

Response:

```json
{
  "errorCode": "VISIT_FORM_V1_RETIRED",
  "message": "Phiên bản biểu mẫu cũ không còn được hỗ trợ."
}
```

Tombstone phải:

- không write database;
- không gửi email;
- không gửi notification;
- log endpoint + correlation ID;
- không log PII;
- có integration test.

## 10.2. Canonical source V2

Không được dùng 10 global fields làm business source.

Chỉ cho phép chúng ở:

- EF/schema mapping;
- compatibility projection writer;
- migration;
- verify;
- test legacy;
- docs Phase I.

Không được dùng trong:

- detail;
- list;
- search;
- email;
- notification;
- report;
- export;
- PDF;
- invoice;
- calendar;
- invitation;
- background jobs;
- logistics;
- minutes;
- feedback;
- dashboard.

## 10.3. Instance-scoped consumer

Mọi consumer theo `visitInstanceId` phải:

1. authorize đúng instance;
2. đọc đúng target detail;
3. không đọc sibling;
4. không dùng smallest campus;
5. missing detail → `VISIT_FORM_DETAIL_MISSING`;
6. không leak hidden sibling.

Kiểm tra:

- approve/reject;
- host assignment;
- participant invitation;
- department assignment;
- email action;
- calendar;
- visit process;
- invoice/export;
- amendment;
- safe edit;
- contribution.

## 10.4. Request-level/aggregate consumer

Không dùng một campus đại diện cho mixed request.

Đích:

```text
Request common data
+ per-campus sections
+ mixed label
+ matchedContexts
+ scope-before-keyword/count/order/page
```

Audit:

- VisitRequestManagement;
- HO overview;
- Staff Leader list;
- report;
- export;
- dashboard;
- notification;
- search;
- email.

Guest/support names vẫn không được search mặc định nếu chưa có quyết định mới.

## 10.5. Compatibility projection writer

Có thể giữ projection writer tạm thời nhưng phải:

- gom về một service/helper;
- đánh dấu temporary;
- test uniform/mixed;
- không rải write logic;
- không cho reader dùng projection.

## 10.6. Security/validation không được giảm

Giữ:

- FluentValidation;
- business validation;
- role/sub-role;
- campus scope;
- relation;
- lifecycle;
- idempotency;
- transaction;
- rowVersion;
- row locking;
- audit;
- post-commit notification;
- token hash;
- masked PII;
- stable errors;
- scope-before-projection.

Không tin frontend gửi:

```text
role
userId
visitorUserId
status
formSchemaVersion
hasMixed
allowedActions
revision
sameForAll
```

---

# 11. Phase G — Workflow coverage bắt buộc

## 11.1. Create

- public;
- authenticated;
- single-campus;
- multi-campus uniform;
- multi-campus mixed;
- A = contact;
- A ≠ contact;
- replay;
- notification first-create-only.

## 11.2. Pending edit

- target-only edit;
- sibling untouched;
- add campus;
- remove removable campus;
- block remove có downstream;
- member copy-on-write;
- stale request rowVersion;
- stale instance rowVersion;
- immutable email.

## 11.3. Resubmit

- all rejected;
- fixed campus set;
- same instance IDs;
- old decision history;
- reroute current leader;
- concurrency.

## 11.4. Identity

- initial claim;
- accept;
- decline;
- resend;
- replace typo;
- expiry;
- redaction;
- cancel 3A;
- transfer initiate;
- transfer accept;
- transfer decline;
- transfer resend;
- transfer cancel;
- old owner relation removed;
- old account vẫn ACTIVE.

## 11.5. Safe edit/amendment

- safe edit;
- urgent media withdrawal;
- amendment submit;
- correct-campus approve/reject;
- wrong-campus forbidden;
- withdraw;
- expire;
- history/revision;
- sibling approval unchanged.

## 11.6. Operational

- Staff Leader approve/reject;
- Staff Leader self-host;
- Staff Leader invitation;
- Staff invitation;
- participant accept/decline;
- department/student assignment;
- wrong-campus denial;
- student contribution/photo;
- report/export/email đúng campus.

---

# 12. Phase H — Remove unreachable V1 runtime code

Chỉ bắt đầu khi:

- parity không còn `MISSING/REGRESSION`;
- frontend không còn V1 caller;
- backend V1 route chỉ còn tombstone;
- targeted V2 xanh;
- full test xanh.

Xóa frontend:

- V1 create form/modal;
- `EditVisitRequest.tsx` nếu chỉ dành V1;
- V1 resubmit;
- V1 flat detail;
- V1 API client;
- V1 validation riêng;
- V1 draft logic;
- V1 routing;
- capability fallback;
- old cache fallback.

Xóa backend:

- V1 create handlers;
- V1 initiate/verify;
- V1 edit/resubmit;
- V1 DTO/validator;
- dual-read V1 fallback;
- V1 DI registration;
- V1 email/notification builder;
- unused compatibility readers.

Không xóa shared behavior vừa được V2 sử dụng.

Sau cleanup:

```bash
rg -n "EditVisitRequest|CreateAuthenticatedVisitRequest|VerifyAndCreateVisitRequest|InitiateVisitRequest|fallback.*v1|formSchemaVersion.*1|FORM_VERSION_UPGRADE_REQUIRED" .
```

Mọi hit còn lại phải được phân loại:

```text
tombstone
test
schema
migration
documentation
false positive
```

---

# 13. Test plan

## 13.1. Backend

```bash
dotnet build
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.ArchitectureTests
dotnet test tests/PEMS.IntegrationTests
```

Bổ sung test cho:

- V1 endpoint → 410;
- zero writes;
- V2 canonical source;
- missing detail;
- mixed request;
- target-campus isolation;
- hidden-campus denial;
- allowedActions;
- rowVersion;
- validation;
- idempotency;
- audit;
- notification;
- identity;
- amendment;
- search/report/export/email.

Nên thêm architecture/source guard để ngăn V2 consumer mới đọc legacy fields ngoài allowlist.

## 13.2. Frontend

```bash
npm run typecheck
npm run lint
npm run test
npm run build
```

Test:

- capability loading/error không mở V1;
- missing version không mở V1;
- all CTA → V2;
- detail/edit/resubmit → V2;
- shared modal → V2;
- toast;
- field validation;
- focus/scroll;
- form state preserved;
- double-submit;
- dirty guard;
- OTP;
- 409;
- 410;
- 403/404/429/500;
- multi-campus isolation.

## 13.3. Real-stack

Chạy:

```text
React/Vite
→ .NET API
→ MySQL pems_db
```

Không mock network.

Journey matrix tối thiểu:

1. Public create + OTP.
2. Authenticated create.
3. Single campus.
4. Multi uniform.
5. Multi mixed.
6. Detail.
7. Pending edit.
8. Resubmit.
9. Staff Leader approve/reject.
10. Staff Leader self-host.
11. Staff Leader invitation.
12. Staff invitation.
13. Participant accept/decline.
14. Identity claim.
15. Identity transfer.
16. Safe edit.
17. Amendment.
18. Search.
19. Report/export.
20. Email action.
21. Wrong-campus denial.
22. Hidden-campus non-leak.
23. Student contribution/photo.
24. V1 tombstone 410.

Mỗi journey ghi:

| ID | Persona | Request | Route/API | Expected | Actual | HTTP | DB evidence | Result |
|---|---|---|---|---|---|---:|---|---|

---

# 14. Error handling và toast contract

Tạo central frontend mapper.

## 14.1. Validation

```text
400
→ field errors
→ mở card lỗi
→ focus field
→ giữ form
```

## 14.2. Authorization

```text
401
→ auth flow
→ giữ draft

403
→ toast không có quyền
→ không leak data

404
→ không tìm thấy hoặc không có scope
```

## 14.3. Concurrency

```text
409
→ toast dữ liệu đã thay đổi
→ nút reload
→ không overwrite
```

## 14.4. Retired V1

```text
410
→ toast phiên bản cũ không còn hỗ trợ
→ không mở V1
```

## 14.5. Lifecycle/rate-limit/system

```text
422
→ trạng thái không cho phép

429
→ thử lại sau
→ giữ dữ liệu

500
→ thông báo chung
→ không hiện raw SQL/stack trace
```

Không hiện duplicate toast cho một response.

---

# 15. Tài liệu đầu ra

Tạo/cập nhật:

```text
docs/ChangeSauHopChiQUyen/sauhop_13-07/
V1_V2_RUNTIME_BEHAVIOR_PARITY.md

docs/ChangeSauHopChiQUyen/sauhop_13-07/
V2_ONLY_RUNTIME_CUTOVER_REPORT.md

docs/ChangeSauHopChiQUyen/sauhop_13-07/
V2_ONLY_REALSTACK_JOURNEY_MATRIX.md
```

## 15.1. Behavior parity report

| V1 behavior | V2 implementation | File/symbol | Test | Status | Action |
|---|---|---|---|---|---|

## 15.2. Cutover report

Ghi:

- route đã chuyển;
- V1 caller đã loại bỏ;
- backend endpoint retired;
- canonical source audit;
- compatibility allowlist;
- feature flag behavior;
- test results;
- remaining limitation;
- Phase I vẫn riêng.

## 15.3. Journey matrix

Không commit:

- password;
- OTP;
- access token;
- refresh token;
- PII nhạy cảm;
- stack trace có secret.

---

# 16. Commit strategy

Gợi ý 3–5 semantic commits:

## Commit 1

```text
refactor(frontend): preserve visit form behavior for v2 cutover
```

Scope:

- validation;
- toast/error mapper;
- dirty guard;
- submit guard;
- tests.

## Commit 2

```text
feat(frontend): route all visit workflows to per-campus v2
```

Scope:

- CTA;
- routing;
- shared modal;
- capability no-fallback;
- tests.

## Commit 3

```text
feat(backend): retire v1 visit mutations and enforce v2 reads
```

Scope:

- 410 tombstones;
- consumer migration;
- projection writer;
- tests.

## Commit 4

```text
refactor(delegations): remove unreachable v1 runtime paths
```

Scope:

- delete V1 runtime;
- DI cleanup;
- frontend cleanup;
- tests.

## Commit 5

```text
docs(delegations): record v2-only cutover evidence
```

Không bắt buộc đủ 5 commit.

Trước commit:

```bash
git diff --check
git status --short
git diff
git diff --cached --name-status
```

Không push.

---

# 17. Definition of Done

## Database

- [ ] non-V2 = 0.
- [ ] missing detail = 0.
- [ ] orphan detail = 0.
- [ ] issue_count = 0.
- [ ] không reset DB ngoài yêu cầu.

## Frontend

- [ ] mọi CTA dùng V2.
- [ ] mọi detail/edit/resubmit dùng V2.
- [ ] capability loading/error không mở V1.
- [ ] missing version không mở V1.
- [ ] shared modal không mở V1.
- [ ] validation parity PASS.
- [ ] toast parity PASS.
- [ ] dirty/double-submit/OTP/error parity PASS.
- [ ] V1 component không reachable.

## Backend

- [ ] V1 mutation trả 410 hoặc đã xóa.
- [ ] không còn V1 internal caller.
- [ ] mọi V2 consumer đọc per-campus canonical data.
- [ ] mixed request không dùng projection.
- [ ] missing detail fail closed.
- [ ] scope trước projection/search.
- [ ] audit/notification/concurrency/idempotency giữ nguyên.
- [ ] compatibility projection chỉ trong allowlist.

## Tests

- [ ] build PASS.
- [ ] Unit PASS.
- [ ] Architecture PASS.
- [ ] Integration PASS.
- [ ] targeted V2 PASS.
- [ ] TypeScript PASS.
- [ ] lint PASS.
- [ ] Vitest PASS.
- [ ] Vite build PASS.
- [ ] real-stack PASS.

## Git

- [ ] không làm trên Dev.
- [ ] không push/merge/PR.
- [ ] không rewrite pushed commits.
- [ ] không AI attribution.
- [ ] đúng author/committer.
- [ ] không còn temp/generated files.

## Final status

```text
Runtime: V2-ONLY
V1 runtime: RETIRED
Legacy DB columns: RETAINED
Contract-drop: SEPARATE PHASE
```

---

# 18. Báo cáo cuối phiên

Báo cáo theo thứ tự:

1. Branch/HEAD/upstream.
2. Baseline.
3. Frontend inventory.
4. Backend inventory.
5. Parity findings.
6. Shared behavior changes.
7. Route/capability changes.
8. Backend retirement changes.
9. Legacy-field audit.
10. Projection allowlist.
11. V1 files removed.
12. Backend tests.
13. Frontend tests.
14. Real-stack journeys.
15. Database verification.
16. Documentation.
17. Commits.
18. Author/no-AI verification.
19. Không push/merge.
20. Remaining limitations.
21. Runtime status.
22. Contract-drop status.

Mỗi test phải ghi:

```text
PASS
FAIL
NOT RUN
BLOCKED
```

Không dùng kết quả cũ thay cho test hiện tại.

---

# 19. Stop conditions

Chỉ dừng khi:

- business rule authoritative mâu thuẫn;
- working tree overlap không thể tách;
- cần deploy production;
- cần destructive DB action ngoài phạm vi;
- thiếu credential/external service không có local alternative.

Không dừng vì:

- build lỗi;
- test fail;
- TypeScript lỗi;
- lint lỗi;
- route lỗi;
- validation mismatch;
- thiếu fixture.

Các lỗi thông thường phải tự điều tra, sửa và chạy lại.
