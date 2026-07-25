# PEMS — PHASE 4.5 STACK & ERROR-STATE STABILIZATION PLAN

## Mục tiêu

Sửa có hệ thống các lỗi P0/P1 đã phát hiện trước khi tiếp tục mở rộng Phase 5.

Đây là phiên **IMPLEMENTATION + RUNTIME VERIFICATION**. Không chỉ audit hoặc viết báo cáo. Phải:

- đọc code và cấu hình thật tại HEAD hiện tại;
- xác minh đúng frontend, backend và database đang chạy cùng baseline;
- sửa defect thật;
- bổ sung regression test;
- chạy gate trên đúng một HEAD;
- commit theo functional slice;
- không đánh dấu hoàn thành khi chỉ đọc code tĩnh.

Không làm chống đối, không che lỗi bằng dữ liệu mặc định, không giảm assertion và không gọi lỗi môi trường là lỗi nghiệp vụ khi chưa chứng minh.

---

# 1. Trạng thái dự án và quy tắc Git

Trạng thái gần nhất đã biết:

```text
Phase 1 VERIFIED
Phase 2 VERIFIED
Phase 3 VERIFIED
Phase 4 backend/frontend code gates GREEN
Phase 4 critical E2E DEFERRED TO PHASE 6
Phase 4.5 STACK AND ERROR-STATE STABILIZATION — AUTHORIZED
Phase 5 PAUSED
Project NOT YET FINAL
```

Trước khi làm, phải đọc trạng thái Git thật vì working tree/remote từng bị thay đổi bởi một process khác.

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

Yêu cầu:

- không reset;
- không rebase;
- không force-push;
- không amend commit đã push;
- không tự merge khi remote thay đổi;
- không commit thay đổi không rõ nguồn;
- không add hai file prompt untracked;
- không pop/drop stash nếu chưa được chủ dự án cho phép.

Nếu working tree có thay đổi lạ:

1. Ghi lại file và diff.
2. Xác định thay đổi thuộc chủ dự án hay agent.
3. Không commit hoặc revert khi chưa có quyết định.
4. Chỉ tiếp tục khi working tree an toàn cho functional slice mới.

---

# 2. Danh sách lỗi cần xử lý

## P0 — phải xử lý trước khi tiếp tục Phase 5

1. Frontend, backend và database có thể không đồng bộ.
2. Backend chỉ kiểm tra kết nối MySQL, chưa kiểm tra schema Pure V2 readiness.
3. API lỗi nhưng UI hiển thị như dữ liệu rỗng.
4. Phân loại sai network/403/404/409/500.
5. Dùng `false`, `[]`, `0` khi dependency API bị lỗi.
6. Dashboard HO có thể loading vô hạn.
7. Secret OAuth cần được phân loại đúng là development shared hay production/personal.

## P1 — xử lý ngay sau P0 trong cùng stabilization phase

1. Desktop và mobile xử lý lỗi không nhất quán.
2. Dashboard, lịch, nhiệm vụ che lỗi hoặc giữ dữ liệu cũ.
3. Upload contract không thống nhất.
4. During Visit còn mock data/localStorage.
5. News còn hardcode “University of Tokyo”.
6. Thiếu `visit_instance_form_details` gây 409 cần được hiển thị và chẩn đoán đúng.
7. Bằng chứng test chưa được đóng trên cùng một HEAD.

## P2 — chỉ xử lý nếu thuộc trực tiếp luồng Visit

- Email/template;
- campus filter;
- metadata ảnh;
- badge;
- các màn chỉ `console.error` rồi ẩn lỗi.

Các module public không liên quan trực tiếp đến Visit Request để backlog riêng.

---

# 3. Slice 4.5A — Đồng bộ frontend, backend và database

## 3.1 Mục tiêu

Chứng minh một stack duy nhất:

```text
Frontend đúng HEAD
→ Backend đúng HEAD
→ MySQL đúng canonical/schema tương ứng
```

## 3.2 Việc phải làm

### Frontend

Xác định:

- API base URL thực tế;
- port backend;
- environment đang dùng;
- có proxy Vite hay không;
- có backend cũ đang chạy song song hay không;
- build artifact có khớp HEAD không.

### Backend

Xác định:

- environment thực tế: Development/Testing/Production;
- URL/port đang bind;
- appsettings nào được load;
- connection string cuối cùng sau override;
- database name thật;
- feature flag/kill-switch Pure V2;
- API version/route map đang active.

### Database

Xác định:

- `SELECT DATABASE()`;
- table count;
- các bảng Pure V2 bắt buộc;
- `visit_instance_form_details`;
- FK/trigger quan trọng;
- dữ liệu request/instance/detail tối thiểu;
- database có phải canonical baseline tương ứng không.

## 3.3 Công cụ chẩn đoán

Thêm hoặc sử dụng endpoint/runtime diagnostics chỉ dành cho development/testing, không lộ secret:

```json
{
  "environment": "Development",
  "apiCommit": "<HEAD>",
  "databaseName": "<db>",
  "pureV2": true,
  "schemaReady": true
}
```

Không trả connection string, password, client secret hoặc token.

## 3.4 Test bắt buộc

1. Frontend gọi đúng backend hiện tại.
2. Backend report đúng environment và database.
3. Backend cũ/port sai tạo lỗi rõ ràng, không giả empty state.
4. Database sai tên bị phát hiện.
5. Frontend và backend có version/commit mismatch thì log cảnh báo rõ.

## 3.5 Exit gate

Chỉ đóng slice khi:

- FE–BE–DB cùng baseline;
- không còn port/database ambiguity;
- có bằng chứng runtime;
- không dùng backend cũ;
- không có 404 do route mismatch.

Commit gợi ý:

```text
fix(stack): align frontend backend and database runtime
```

---

# 4. Slice 4.5B — Database readiness Pure V2

## 4.1 Mục tiêu

Backend không được coi database là “ready” chỉ vì mở được kết nối MySQL.

## 4.2 Readiness phải kiểm tra

Tối thiểu:

- đúng database allowlist trong Development/Testing;
- bảng Pure V2 bắt buộc tồn tại;
- `visit_request_campuses`;
- `visit_instance_form_details`;
- bảng guest/member link;
- các cột runtime bắt buộc;
- các cột V1 đã bỏ không bị runtime yêu cầu;
- trigger/FK cốt lõi tồn tại;
- generated column quan trọng đúng;
- schema/hash/version marker theo cơ chế hiện có;
- không có migration/canonical mismatch rõ ràng.

Không dùng một giá trị “schema version” giả để khôi phục dual-version runtime.

## 4.3 Hành vi khi readiness fail

- readiness health phải fail;
- log phải nêu thiếu gì;
- API business không được tiếp tục chạy như bình thường;
- không fallback V1;
- không trả lỗi mơ hồ;
- không lộ thông tin nhạy cảm.

Ví dụ lỗi chuẩn hóa:

```text
DATABASE_NOT_READY
PURE_V2_SCHEMA_MISSING
VISIT_FORM_DETAIL_MISSING
DATABASE_BASELINE_MISMATCH
```

Không đổi các error code hiện có nếu contract đã khóa; chỉ bổ sung mapping rõ ràng.

## 4.4 Phân biệt schema và data readiness

### Schema readiness

Kiểm tra cấu trúc database.

### Data readiness

Không chặn toàn backend chỉ vì một request cũ thiếu detail.

Với request thiếu `visit_instance_form_details`:

- endpoint liên quan vẫn fail-closed bằng `409 VISIT_FORM_DETAIL_MISSING`;
- readiness tổng thể chỉ fail nếu lỗi có tính hệ thống/canonical;
- UI phải hiển thị lỗi dữ liệu cần sửa, không hiển thị “không có dữ liệu”.

## 4.5 Tests

1. Database đúng canonical → readiness pass.
2. Thiếu bảng Pure V2 → fail.
3. Thiếu cột bắt buộc → fail.
4. Thiếu trigger/FK quan trọng → fail theo policy.
5. Một request cũ thiếu detail → endpoint 409, không fallback.
6. Error/log không chứa secret.
7. Backend không khởi động business-ready trên schema cũ.

Commit gợi ý:

```text
feat(health): enforce Pure V2 database readiness
```

---

# 5. Slice 4.5C — Chuẩn hóa API error model

## 5.1 Mục tiêu

Frontend phải phân biệt chính xác:

```text
loading
success with data
success empty
403 forbidden
404 not found
409 domain/schema conflict
422 validation
500 server error
network/offline
timeout
```

## 5.2 Backend

Rà soát error response cho:

- Visit Process;
- Minutes;
- Feedback;
- Visit Photos;
- Dashboard HO;
- reminder;
- logistics;
- agenda candidates;
- document;
- news eligibility.

Mỗi lỗi cần có:

- HTTP status đúng;
- stable error code;
- message có thể hiển thị;
- correlation/request id nếu hệ thống có;
- không trả stack trace production;
- không gộp mọi lỗi thành 404/403.

## 5.3 Frontend

Tạo một error normalization layer dùng chung, ví dụ:

```text
ApiErrorKind:
- Forbidden
- NotFound
- Conflict
- Validation
- Server
- Network
- Timeout
- Unknown
```

Không yêu cầu đúng tên trên, nhưng phải có một contract thống nhất.

Không được mỗi component tự đoán lỗi bằng chuỗi message.

## 5.4 Mapping bắt buộc

- `403` → không có quyền.
- `404` → không tồn tại.
- `409 VISIT_FORM_DETAIL_MISSING` → dữ liệu Pure V2 chưa đầy đủ; cho retry/support, không fallback V1.
- `409` business conflict khác → message đúng error code.
- `5xx` → lỗi hệ thống.
- network/offline → lỗi kết nối.
- timeout → timeout/retry.
- response parse lỗi → lỗi dữ liệu/API contract.

Commit gợi ý:

```text
refactor(api): standardize visit error classification
```

---

# 6. Slice 4.5D — Chuẩn hóa loading/error/empty trên frontend

## 6.1 Màn hình ưu tiên P0

Bắt buộc sửa trước:

1. Tiếp khách / Visit Process.
2. Ảnh đoàn khách.
3. Biên bản.
4. Feedback.
5. Dashboard HO.
6. Reminder.
7. Logistics.
8. Agenda candidates.

## 6.2 Quy tắc state

Mỗi màn cần state riêng:

```text
idle
loading
success-data
success-empty
error
```

Không dùng:

```text
data = []
loading = false
```

để biểu diễn cả success-empty và API failure.

## 6.3 Hành vi UI

### Loading

- spinner/skeleton;
- có timeout/finally;
- không loading vô hạn.

### Success empty

Hiển thị thông báo đúng nghiệp vụ:

```text
Chưa có biên bản
Chưa có phản hồi
Chưa có ảnh
```

### Error

Hiển thị:

- tiêu đề lỗi;
- mô tả phù hợp;
- retry;
- mã lỗi/correlation id khi hữu ích;
- không biến thành empty.

### Stale data

Nếu giữ dữ liệu cũ khi refresh fail:

- phải có banner “Không thể cập nhật dữ liệu mới”;
- không giả vờ dữ liệu đang hiện là mới nhất;
- action phụ thuộc dữ liệu mới phải bị chặn nếu cần.

## 6.4 Desktop/mobile parity

Cùng endpoint phải có cùng semantics ở:

- desktop table;
- tablet;
- mobile cards.

Không được desktop có error state còn mobile trả “không có dữ liệu”.

## 6.5 Tests

Frontend unit/component tests phải cover:

- loading;
- success with data;
- success empty;
- 403;
- 404;
- 409 `VISIT_FORM_DETAIL_MISSING`;
- 500;
- network error;
- retry success;
- finally dừng spinner;
- desktop/mobile parity.

Commit gợi ý:

```text
fix(ui): distinguish visit loading empty and error states
```

---

# 7. Slice 4.5E — Không dùng giá trị mặc định khi dependency API lỗi

## 7.1 Các vùng cần rà

- Reminder capability/state.
- Logistics candidates/items.
- Agenda candidates.
- Host/staff candidate lists.
- Campus metadata.
- Dashboard counters.
- Calendar tasks.
- Pending lists.

## 7.2 Quy tắc

API failure không được biến thành:

```text
false
[]
0
null
```

rồi tiếp tục business action.

Phải:

1. đánh dấu dependency failed;
2. chặn action phụ thuộc;
3. hiển thị lỗi;
4. cho retry;
5. không gửi mutation với dữ liệu suy diễn sai.

Ví dụ:

```text
agenda candidates API fail
→ disable “Gán người phụ trách”
→ hiển thị lỗi + retry
```

Không được:

```text
agenda candidates API fail
→ []
→ UI kết luận không có ứng viên
```

## 7.3 Tests

- dependency fail → action disabled;
- retry success → action enabled;
- không gửi request với default data;
- không ghi state sai vào backend;
- stale cache được đánh dấu.

Commit gợi ý:

```text
fix(ui): block visit actions when dependencies fail
```

---

# 8. Slice 4.5F — Loading vô hạn và dashboard/lịch/nhiệm vụ che lỗi

## 8.1 Dashboard HO

Phải đảm bảo mọi async branch đều kết thúc:

- `finally`;
- cancellation;
- timeout;
- component unmount;
- partial request failure.

Nếu dashboard có nhiều API:

- xác định all-or-nothing hay partial rendering;
- nếu partial, card lỗi phải hiện riêng;
- không để một API fail giữ spinner toàn trang.

## 8.2 Calendar/task

Không được:

- giữ dữ liệu cũ mà không cảnh báo;
- biến lỗi thành lịch rỗng;
- bỏ qua rejected promise;
- chỉ `console.error`.

## 8.3 Tests

- một API fail, các card khác vẫn render theo contract;
- spinner dừng;
- retry hoạt động;
- stale banner hiển thị;
- unmount không setState warning;
- network timeout không loading vô hạn.

Commit gợi ý:

```text
fix(dashboard): terminate loading and surface partial failures
```

---

# 9. Slice 4.5G — Upload contract thống nhất

## 9.1 Audit contract thật

Xác định backend hiện hỗ trợ gì:

- MIME type;
- extension;
- max size;
- image/video;
- endpoint nào;
- visit photo vs news media vs document attachment.

Không ép mọi upload toàn hệ thống dùng cùng một rule nếu business khác nhau.

Tạo matrix:

| Surface | File types | Max size | Max count | Backend | Frontend | Error |
|---|---:|---:|---:|---|---|---|

## 9.2 Yêu cầu

- frontend text phải khớp backend;
- frontend validation chỉ là UX, backend vẫn enforce;
- không UI ghi 100 MB khi backend chỉ nhận 5 MB;
- không cho video nếu endpoint chỉ nhận ảnh;
- error code rõ cho size/type/count;
- mobile/desktop cùng rule.

## 9.3 Tests

- đúng loại/kích thước → pass;
- vượt size → fail đúng error;
- MIME giả/extension giả → fail;
- video chỉ pass ở endpoint hỗ trợ;
- frontend và backend constants không drift nếu có thể dùng shared config/capability.

Commit gợi ý:

```text
fix(upload): align visit media limits across frontend and backend
```

---

# 10. Slice 4.5H — Loại bỏ prototype, mock và hardcode trong luồng chính

## 10.1 During Visit

Rà:

- rating mẫu;
- notes mẫu;
- business card mẫu;
- contact/document lưu localStorage;
- dữ liệu không persist backend.

Phải xác định từng chức năng:

```text
production feature
prototype only
not implemented
```

Không được hiển thị như tính năng thật nếu không persist.

### Phương án

- kết nối API thật và test persistence; hoặc
- ẩn/disable có nhãn rõ “chưa hỗ trợ” nếu ngoài scope;
- không giữ mock data giả trong production flow.

## 10.2 After Visit / News

Xóa hardcode:

```text
University of Tokyo
```

Tên phải lấy từ:

- target `visit_instance_form_detail.delegation_name`;
- hoặc DTO Pure V2 tương ứng;
- không từ campus sibling;
- không fallback request global.

## 10.3 Tests

- reload vẫn còn dữ liệu đã lưu;
- request A không nhận dữ liệu mẫu của request B;
- news title/content dùng đúng delegation target instance;
- mixed request không lấy campus đầu tiên;
- localStorage không là persistence chính cho nghiệp vụ.

Commit gợi ý:

```text
fix(visit): remove prototype data from active visit flows
```

---

# 11. Slice 4.5I — OAuth secret classification

## 11.1 Chính sách dự án hiện tại

Chủ dự án đã quyết định:

```text
Shared development credentials intentionally tracked
Production security hardening deferred
```

Không tự:

- rewrite history;
- xóa development credentials;
- thay placeholder làm đồng đội không chạy được;
- rotate credential dùng chung khi chưa có quyết định.

## 11.2 Nhưng phải phân loại chính xác

Kiểm tra OAuth values trong `appsettings.Development.json`:

### Trường hợp A — shared development credential có chủ đích

- ghi nhận trong security debt;
- không coi là blocker development;
- không in secret vào log/report;
- yêu cầu production dùng secret manager/env riêng.

### Trường hợp B — production credential hoặc personal refresh token đang hoạt động

- báo P0 security;
- không tiếp tục sử dụng;
- chủ dự án phải rotate/revoke;
- chuyển sang environment/User Secrets;
- không rewrite history nếu chưa được yêu cầu, nhưng coi giá trị cũ đã lộ.

Không tự suy đoán loại credential.

## 11.3 Test/config gate

- production config không chứa dev secret;
- diagnostics không lộ secret;
- logs không in token;
- startup fail rõ khi production thiếu secret bắt buộc.

Commit chỉ khi có thay đổi được chủ dự án cho phép.

---

# 12. Phase 4.5 regression tests

## Backend

Chạy:

```bash
dotnet build PEMS.slnx

dotnet test backend/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj \
  --logger "trx;LogFileName=phase45-architecture.trx"

dotnet test backend/PEMS.UnitTests/PEMS.UnitTests.csproj \
  --logger "trx;LogFileName=phase45-unit.trx"

dotnet test backend/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj \
  --logger "trx;LogFileName=phase45-integration.trx"
```

## Frontend

Chạy tại đúng thư mục frontend:

```bash
npm run lint
npm run test:unit
npm run build
```

## Targeted browser tests

Bổ sung browser/component/E2E targeted cho bốn màn P0:

- Visit Process;
- Visit Photos;
- Minutes;
- Feedback.

Test cả:

- data;
- empty;
- 403;
- 404;
- 409;
- 500;
- network;
- retry.

---

# 13. Evidence package trên cùng một HEAD

Tạo một evidence package duy nhất, ví dụ:

```text
scratchpad/phase45-evidence/
```

Bao gồm:

- `git-head.txt`;
- `git-status.txt`;
- frontend build log;
- backend build log;
- Architecture TRX;
- Unit TRX;
- Integration TRX;
- frontend unit result;
- database name;
- schema readiness result;
- API diagnostics result;
- screenshots/traces của bốn màn P0;
- SQL/canonical hash đang dùng;
- start/end timestamp.

Không dùng bằng chứng từ HEAD khác để kết luận phase hiện tại.

---

# 14. Quy tắc xử lý failure

Lần fail đầu tiên phải giữ:

- TRX;
- console log;
- browser trace;
- screenshot;
- database name;
- backend PID/port;
- frontend URL;
- environment;
- stack trace;
- cleanup state.

Không rerun đè bằng chứng.

Phân loại:

```text
production defect
frontend state defect
backend contract defect
database readiness defect
fixture defect
environment/port mismatch
file lock
test defect
```

Không gọi “flaky” trước khi root-cause.

---

# 15. Phạm vi không được mở rộng

Trong Phase 4.5 không audit toàn bộ:

- FAQ;
- Partner;
- Translation framework;
- Gallery public;
- Homepage;
- CMS chung.

Chỉ sửa nếu trực tiếp gây lỗi cho Visit Request V2.

Không xử lý P2-F1/P2-F2 của Phase 7 trong phase này:

- 139 file test orphan;
- 98 reference `FormSchemaVersions` trong test harness.

---

# 16. Cách chia commit

Khuyến nghị:

```text
fix(stack): align frontend backend and database runtime
feat(health): enforce Pure V2 database readiness
refactor(api): standardize visit error classification
fix(ui): distinguish visit loading empty and error states
fix(ui): block visit actions when dependencies fail
fix(upload): align visit media limits
fix(visit): remove prototype data from active flows
test(e2e): verify visit error states on one baseline
```

Không bắt buộc đúng tên trên.

Yêu cầu:

- commit theo functional slice;
- không commit theo từng file;
- không amend commit đã push;
- không tên AI;
- không `Co-Authored-By AI`;
- không add hai prompt untracked;
- không làm mất stash.

---

# 17. Gate hoàn thành Phase 4.5

Chỉ kết luận:

```text
Phase 4.5 VERIFIED
Phase 5 RESUMED
```

khi tất cả điều kiện sau đạt:

1. Frontend, backend và database cùng baseline.
2. Backend readiness phát hiện schema Pure V2 sai.
3. Database đúng canonical làm readiness pass.
4. `VISIT_FORM_DETAIL_MISSING` vẫn fail-closed.
5. UI không biến API error thành empty state.
6. 403/404/409/500/network được phân loại đúng.
7. Dashboard không loading vô hạn.
8. Reminder/logistics/agenda không dùng default khi API fail.
9. Desktop/mobile error semantics giống nhau.
10. Upload contract đã thống nhất.
11. Mock/localStorage/hardcode trong luồng chính đã được xử lý.
12. Backend build xanh.
13. ArchitectureTests xanh.
14. UnitTests xanh.
15. IntegrationTests xanh.
16. Frontend lint xanh.
17. Frontend unit xanh.
18. Frontend build xanh.
19. Targeted browser tests bốn màn P0 xanh.
20. Evidence package cùng một HEAD.
21. `git diff --check` sạch.
22. `pems_db` không bị mutation ngoài dự kiến.
23. Không còn disposable DB sau process exit.
24. Không có tên AI trong commit metadata.

Nếu còn P0:

```text
Phase 4.5 IN PROGRESS
Phase 5 PAUSED
Project NOT YET FINAL
```

Không dùng nhãn “mostly ready” hoặc “ready with caveats”.

---

# 18. Sau Phase 4.5

Sau khi Phase 4.5 VERIFIED:

1. Fetch remote.
2. Nếu remote không đổi, push thường lên `origin/Cảnh-Iter1`.
3. Không force-push.
4. Xác minh local/remote `0/0`.
5. Giữ stash và hai prompt untracked.
6. Tiếp tục Phase 5 visit-adjacent:
   - Minutes mutation;
   - Logistics/handover/cancel cascade;
   - News contribution/media consent;
   - Visit Photos/Vision còn lại;
   - Expense còn lại.
7. Phase 6 vẫn chịu trách nhiệm:
   - SQL canonical final;
   - hash pin;
   - real-stack E2E toàn hành trình;
   - regression nhiều lần.
8. Phase 7 chịu trách nhiệm:
   - orphan tests;
   - `FormSchemaVersions` test harness;
   - dead compatibility cleanup;
   - final release gate.

---

# 19. Báo cáo sau mỗi phiên

Báo theo cấu trúc:

```text
Current phase/slice
Local/remote HEAD
Ahead/behind
Working tree/stash/untracked state

Defects confirmed
False positives
Files changed
Runtime contract changed
Security classification

Tests added
Counts before/after
First-failure evidence
Build/frontend/backend/database gate

Commits
Push status
Remaining P0/P1
Exact resume point
```

Không dừng chỉ để hỏi có tiếp tục không.

Chỉ dừng khi:

- remote thay đổi;
- có unknown working-tree change;
- cần business decision chưa khóa;
- cần destructive operation trên DB thật;
- cần production credential/deployment;
- gặp platform hard limit.

Mọi lỗi code/test/fixture thông thường phải tự root-cause và tiếp tục xử lý.
