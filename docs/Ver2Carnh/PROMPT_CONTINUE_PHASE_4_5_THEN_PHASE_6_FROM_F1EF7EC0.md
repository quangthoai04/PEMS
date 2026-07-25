# PEMS — CONTINUE FROM PHASE 5 VERIFIED
# RUN PHASE 4.5 STABILIZATION, THEN PHASE 6

Bạn tiếp tục dự án PEMS Pure V2 từ baseline hiện tại.

Đây là phiên **IMPLEMENTATION + RUNTIME VERIFICATION**. Không chỉ audit hoặc viết báo cáo. Phải sửa defect thật, bổ sung regression test, chạy gate trên đúng một HEAD và commit theo functional slice.

---

## 1. Trạng thái hiện tại

```text
Phase 1 VERIFIED
Phase 2 VERIFIED
Phase 3 VERIFIED
Phase 4 backend/frontend code gates GREEN
Phase 4 critical real-stack E2E DEFERRED
Phase 5 VERIFIED
Phase 4.5 STACK AND ERROR-STATE STABILIZATION — NEXT
Phase 6 NOT STARTED
Project NOT YET FINAL
```

Git baseline hiện tại:

- Local branch: `Canh-Iter1`
- Tracking branch: `origin/Cảnh-Iter1`
- Local HEAD: `f1ef7ec0`
- Remote HEAD: `f1ef7ec0`
- Ahead/behind: `0/0`
- Stash: `8/8`
- `stash@{0}` = `f27b0853` — owner-WIP backup của feedback notification, đã được audit và commit nhưng chưa drop theo chỉ đạo
- Ba file prompt hiện untracked và không được add nếu chưa có lệnh riêng:
  - `docs/Ver2Carnh/PROMPT_AUDIT_PEMS_CODE_ALIGN_WITH_LATEST_PURE_V2_SQL(1).md`
  - `docs/Ver2Carnh/PROMPT_IMPLEMENT_PEMS_PURE_V2_ONLY_AFTER_AUDIT_APPROVED.md`
  - `docs/Ver2Carnh/PROMPT_PHASE_4_5_STACK_ERROR_STABILIZATION.md`

Gate gần nhất:

- Backend build: 0 error
- ArchitectureTests: `14/14`
- UnitTests: `958/958`
- IntegrationTests: `569/569`
- Frontend lint/unit/build: gate Phase 4 đã xanh, frontend không bị thay đổi trong Phase 5
- `pems_db`: 81 bảng
- Persistent disposable DB: 0
- Không có tên AI trong các commit của agent

Phase 5 commits đã hoàn tất và push:

- `00e6a2d6`
- `2a9a52af`
- `1c06b131`
- `58b9c100`
- `1ebf88e5`
- `35070d99`
- `d473577e`

Không amend, reset, squash, rebase hoặc rewrite các commit này.

---

# 2. Vì sao phải làm Phase 4.5 trước Phase 6

Phase 5 đã VERIFIED, nhưng các finding P0 sau vẫn có thể làm real-stack E2E sai hoặc đánh lừa người dùng:

1. Frontend, backend và database có thể chạy lệch baseline.
2. Backend chỉ kiểm tra kết nối MySQL, chưa kiểm tra Pure V2 schema readiness.
3. API lỗi có thể bị UI hiển thị thành danh sách rỗng.
4. 403/404/409/500/network bị phân loại sai.
5. Reminder, logistics, agenda candidates có thể dùng `false`, `[]`, `0` khi API fail.
6. Dashboard HO có thể loading vô hạn.
7. Desktop/mobile có thể xử lý lỗi khác nhau.
8. Upload contract chưa thống nhất.
9. During/After Visit có thể còn mock/localStorage/hardcode.

Do đó thứ tự bắt buộc:

```text
Phase 4.5 stabilization
→ Phase 4.5 full gate
→ Phase 6 SQL canonical + real-stack E2E
→ Phase 7 cleanup
```

Không làm lại Phase 5 trừ khi Phase 4.5 tạo regression có bằng chứng.

---

# 3. Preflight

Chạy:

```bash
git status --short --branch
git branch --show-current
git rev-parse HEAD
git fetch origin Cảnh-Iter1
git rev-parse origin/Cảnh-Iter1
git rev-list --left-right --count origin/Cảnh-Iter1...HEAD
git log -15 --oneline --decorate
git stash list --format="%gd %H %s"
git diff --check
```

Xác nhận:

- local/remote vẫn `f1ef7ec0`;
- ahead/behind `0/0`;
- stash 8/8 còn nguyên;
- ba prompt vẫn untracked;
- working tree không có thay đổi không rõ nguồn.

Nếu remote hoặc working tree thay đổi:

- không tự merge;
- không tự rebase;
- không push;
- không commit/revert thay đổi lạ;
- báo phạm vi và dừng để chủ dự án quyết định.

---

# 4. PHASE 4.5A — Đồng bộ frontend, backend và database

## Mục tiêu

Chứng minh một stack duy nhất:

```text
Frontend đúng HEAD
→ Backend đúng HEAD
→ MySQL đúng database/schema tương ứng
```

## Kiểm tra

### Frontend

- API base URL;
- Vite proxy;
- environment;
- backend port;
- build artifact và commit;
- không trỏ backend cũ.

### Backend

- environment thực tế;
- URL/port bind;
- appsettings được load;
- connection string cuối cùng;
- database name;
- Pure V2 kill-switch;
- route map đang active.

### Database

- `SELECT DATABASE()`;
- table count;
- bảng/cột Pure V2 bắt buộc;
- `visit_instance_form_details`;
- trigger/FK quan trọng;
- canonical/hash marker hiện có;
- dữ liệu request/instance/detail tối thiểu.

## Diagnostics

Có thể bổ sung development/testing diagnostics, nhưng không lộ secret:

```json
{
  "environment": "Development",
  "apiCommit": "<HEAD>",
  "databaseName": "<db>",
  "pureV2": true,
  "schemaReady": true
}
```

## Exit

- FE–BE–DB cùng baseline;
- không port/database ambiguity;
- route mới không 404 vì backend cũ;
- có runtime evidence.

Commit gợi ý:

```text
fix(stack): align frontend backend and database runtime
```

---

# 5. PHASE 4.5B — Pure V2 database readiness

## Readiness phải kiểm tra

- kết nối đúng database;
- bảng Pure V2 bắt buộc tồn tại;
- cột runtime bắt buộc tồn tại;
- trigger/FK cốt lõi tồn tại;
- generated column quan trọng đúng;
- không có canonical mismatch rõ ràng;
- runtime không yêu cầu cột V1 đã loại bỏ.

Không dùng `FormSchemaVersion` để khôi phục dual-version.

## Hành vi khi fail

- readiness health fail;
- log nêu rõ thiếu gì;
- business API không tiếp tục như bình thường;
- không fallback V1;
- không lộ secret.

Phân biệt:

### Schema readiness

Lỗi hệ thống/canonical → readiness fail.

### Per-request data readiness

Một request thiếu `visit_instance_form_details`:

- endpoint liên quan trả `409 VISIT_FORM_DETAIL_MISSING`;
- không fallback V1;
- readiness toàn hệ thống không nhất thiết fail nếu chỉ là một record;
- UI phải hiển thị lỗi dữ liệu, không hiển thị empty.

## Tests

1. Canonical DB → pass.
2. Thiếu table → fail.
3. Thiếu column → fail.
4. Thiếu trigger/FK theo policy → fail.
5. Request thiếu detail → endpoint 409.
6. Error/log không lộ secret.

Commit gợi ý:

```text
feat(health): enforce Pure V2 database readiness
```

---

# 6. PHASE 4.5C — Chuẩn hóa API error contract

Chuẩn hóa:

```text
loading
success-data
success-empty
403 forbidden
404 not found
409 domain/schema conflict
422 validation
500 server
network/offline
timeout
```

Rà các surface:

- Visit Process;
- Visit Photos;
- Minutes;
- Feedback;
- Dashboard HO;
- Reminder;
- Logistics;
- Agenda candidates;
- Documents;
- News eligibility.

Backend cần trả:

- HTTP status đúng;
- stable error code;
- user-safe message;
- correlation/request id nếu có;
- không stack trace production.

Frontend cần một error normalization layer dùng chung, không đoán bằng chuỗi message.

Mapping bắt buộc:

- 403 → không có quyền.
- 404 → không tồn tại.
- 409 `VISIT_FORM_DETAIL_MISSING` → dữ liệu Pure V2 chưa đầy đủ.
- 5xx → lỗi hệ thống.
- network → lỗi kết nối.
- timeout → timeout/retry.

Commit gợi ý:

```text
refactor(api): standardize visit error classification
```

---

# 7. PHASE 4.5D — Loading, empty và error trên frontend

Ưu tiên:

1. Tiếp khách / Visit Process.
2. Ảnh đoàn khách.
3. Biên bản.
4. Feedback.
5. Dashboard HO.
6. Reminder.
7. Logistics.
8. Agenda candidates.

Mỗi màn phải có state tách biệt:

```text
idle
loading
success-data
success-empty
error
```

Không dùng `[]`, `false`, `0` để biểu diễn cả empty và error.

## Tests frontend bắt buộc

- loading;
- success-data;
- success-empty;
- 403;
- 404;
- 409 `VISIT_FORM_DETAIL_MISSING`;
- 500;
- network;
- retry success;
- spinner kết thúc trong `finally`;
- desktop/mobile parity.

Commit gợi ý:

```text
fix(ui): distinguish visit loading empty and error states
```

---

# 8. PHASE 4.5E — Chặn thao tác khi dependency API fail

Rà:

- reminder state/capability;
- logistics candidates/items;
- agenda candidates;
- host/staff candidates;
- dashboard counters;
- calendar/tasks;
- pending lists.

API fail phải:

1. đánh dấu dependency failed;
2. chặn action phụ thuộc;
3. hiện lỗi;
4. cho retry;
5. không gửi mutation với dữ liệu mặc định.

Ví dụ:

```text
agenda candidates fail
→ disable assign action
→ error + retry
```

Không được:

```text
agenda candidates fail
→ []
→ UI kết luận không có ứng viên
```

Commit gợi ý:

```text
fix(ui): block visit actions when dependencies fail
```

---

# 9. PHASE 4.5F — Dashboard, calendar và task failures

## Dashboard HO

Mọi async branch phải kết thúc:

- `finally`;
- cancellation;
- timeout;
- unmount;
- partial failure.

Nếu nhiều API:

- quyết định all-or-nothing hay partial rendering;
- card lỗi hiển thị riêng;
- không để một API fail giữ spinner toàn trang.

## Calendar/task

Không được:

- giữ dữ liệu cũ mà không cảnh báo;
- biến lỗi thành lịch rỗng;
- chỉ `console.error`.

Tests:

- spinner dừng;
- partial failure theo contract;
- stale banner;
- retry;
- timeout;
- unmount an toàn.

Commit gợi ý:

```text
fix(dashboard): terminate loading and surface partial failures
```

---

# 10. PHASE 4.5G — Upload contract

Lập matrix:

| Surface | Types | Max size | Max count | Backend | Frontend | Error |
|---|---|---:|---:|---|---|---|

Phân biệt:

- Visit Photos;
- News media;
- Document attachment;
- Minutes attachment.

Yêu cầu:

- UI text khớp backend;
- backend enforce type/size/count;
- không cho video ở endpoint chỉ hỗ trợ ảnh;
- MIME/extension giả bị chặn;
- mobile/desktop cùng rule.

Commit gợi ý:

```text
fix(upload): align visit media limits across frontend and backend
```

---

# 11. PHASE 4.5H — Loại bỏ mock/localStorage/hardcode

## During Visit

Rà:

- rating mẫu;
- notes mẫu;
- business card mẫu;
- contact/document chỉ localStorage;
- dữ liệu không persist.

Mỗi phần phải:

- nối API thật và test persistence; hoặc
- ẩn/disable rõ “chưa hỗ trợ”.

Không để prototype xuất hiện như tính năng production.

## After Visit / News

Xóa hardcode:

```text
University of Tokyo
```

Dùng `delegation_name` của target instance Pure V2.

Tests:

- reload vẫn có dữ liệu;
- request/campus isolation;
- mixed request không lấy campus đầu tiên;
- localStorage không là persistence chính.

Commit gợi ý:

```text
fix(visit): remove prototype data from active visit flows
```

---

# 12. OAuth secret classification

Chính sách hiện tại:

```text
Shared development credentials intentionally tracked
Production security hardening deferred
```

Không tự xóa development credentials hoặc rewrite history.

Phải phân loại:

### Shared development credential có chủ đích

- ghi security debt;
- không blocker development;
- không log secret;
- production dùng secret riêng.

### Production/personal active credential

- P0 security;
- báo chủ dự án rotate/revoke;
- chuyển env/User Secrets;
- không tự thực hiện destructive history rewrite.

---

# 13. Phase 4.5 tests và evidence

Backend:

```bash
dotnet build PEMS.slnx

dotnet test backend/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj \
  --logger "trx;LogFileName=phase45-architecture.trx"

dotnet test backend/PEMS.UnitTests/PEMS.UnitTests.csproj \
  --logger "trx;LogFileName=phase45-unit.trx"

dotnet test backend/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --logger "trx;LogFileName=phase45-integration.trx"
```

Expected baseline tối thiểu:

- Architecture: 14
- Unit: 958
- Integration: 569

Frontend:

```bash
npm run lint
npm run test:unit
npm run build
```

Targeted browser/component tests:

- Visit Process;
- Visit Photos;
- Minutes;
- Feedback.

Test cả data/empty/403/404/409/500/network/retry.

Tạo một evidence package duy nhất trên cùng HEAD, ngoài repo hoặc trong ignored path:

```text
phase45-evidence/
```

Gồm:

- HEAD/status;
- backend/frontend build logs;
- TRX;
- frontend unit result;
- database name;
- readiness result;
- diagnostics result;
- browser trace/screenshots;
- canonical/hash đang dùng;
- timestamps.

Không kết luận bằng evidence từ HEAD khác.

---

# 14. Phase 4.5 gate

Chỉ kết luận:

```text
Phase 4.5 VERIFIED
Phase 6 AUTHORIZED
```

khi:

1. FE–BE–DB cùng baseline.
2. Readiness phát hiện schema sai.
3. Canonical schema làm readiness pass.
4. `VISIT_FORM_DETAIL_MISSING` vẫn fail-closed.
5. UI không biến error thành empty.
6. 403/404/409/500/network đúng.
7. Dashboard không loading vô hạn.
8. Dependency API fail không dùng default.
9. Desktop/mobile parity.
10. Upload contract thống nhất.
11. Mock/localStorage/hardcode luồng chính được xử lý.
12. Backend build xanh.
13. Architecture/Unit/Integration xanh.
14. Frontend lint/unit/build xanh.
15. Targeted browser tests xanh.
16. Evidence cùng một HEAD.
17. `git diff --check` sạch.
18. `pems_db` không bị mutation ngoài dự kiến.
19. Disposable DB = 0 sau process exit.
20. Không có tên AI trong commit metadata.

Nếu còn P0:

```text
Phase 4.5 IN PROGRESS
Phase 6 PAUSED
Project NOT YET FINAL
```

---

# 15. Push và chuyển Phase 6

Sau Phase 4.5 VERIFIED:

1. Full gate.
2. Fetch remote.
3. Nếu remote không đổi, push thường lên `origin/Cảnh-Iter1`.
4. Không force-push.
5. Xác minh local/remote `0/0`.
6. Stash vẫn đủ 8 trừ khi chủ dự án cho phép drop owner-WIP backup.
7. Ba prompt vẫn untracked.
8. Chuyển Phase 6.

Nếu remote thay đổi:

- không merge/rebase;
- không push;
- báo và dừng.

---

# 16. PHASE 6 — SQL canonical + real-stack E2E

Chỉ bắt đầu sau Phase 4.5 VERIFIED.

## SQL

- import canonical vào DB disposable;
- verification issue count = 0;
- negative guards;
- `GET DIAGNOSTICS`;
- placeholder seed;
- missing agenda;
- generated columns;
- trigger/FK consistency;
- self-check cột V1 đã loại;
- tính SHA-256 sau khi SQL final;
- cập nhật hash pin cùng commit;
- không chạy trên `pems_db`.

## Real-stack

```text
React frontend
→ .NET backend
→ MySQL disposable
```

Critical journeys:

- login;
- CTA → V2;
- deep link;
- refresh;
- authenticated create;
- OTP initiate/verify/replay;
- mixed HN/HCM/ĐN;
- pending edit;
- reject → resubmit;
- claim;
- transfer;
- amendment;
- approve/reject;
- assign host;
- invitation;
- agenda;
- logistics;
- photos/media consent;
- expense;
- minutes/documents;
- news contribution;
- export;
- cancel;
- dirty prompt;
- error/retry states.

Kiểm cả API response và database state.

Stability:

- full IntegrationTests ít nhất 3 lần;
- critical E2E ít nhất 2 lần;
- log/TRX riêng;
- không ghi đè failure đầu;
- DB cleanup 0.

---

# 17. Không được xử lý trong phiên này

Để Phase 7:

- orphan `tests/PEMS.ApplicationTests/`;
- khoảng 98 `FormSchemaVersions` test references;
- dead compatibility cleanup cuối;
- quyết định drop owner-WIP stash backup.

Không audit FAQ/Partner/Translation/Gallery public nếu không liên quan trực tiếp Visit V2.

---

# 18. Báo cáo

Báo:

```text
Current phase/slice
Local/remote HEAD
Ahead/behind
Stash/untracked state

Defects confirmed
False positives
Files changed
Runtime contract
Security classification

Tests added
Counts before/after
First-failure evidence
Backend/frontend/database gate

Commits
Push status
Remaining P0/P1
Exact resume point
```

Không dừng chỉ để hỏi có tiếp tục không.

Chỉ dừng khi:

- remote/working tree thay đổi ngoài dự kiến;
- cần business decision;
- cần destructive DB operation;
- cần production credential/deployment;
- platform hard limit.

Mọi lỗi code/test/fixture thông thường phải tự root-cause và tiếp tục.
