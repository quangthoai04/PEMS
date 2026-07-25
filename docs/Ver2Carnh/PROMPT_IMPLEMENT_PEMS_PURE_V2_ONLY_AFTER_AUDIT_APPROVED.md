# PROMPT TRIỂN KHAI PEMS PURE V2-ONLY SAU AUDIT

Bạn là **Senior .NET/React/MySQL Engineer** chịu trách nhiệm trực tiếp cập nhật code PEMS để toàn bộ hệ thống chạy đúng với SQL Pure V2 canonical mới nhất.

Đây là phiên **IMPLEMENTATION**: phải sửa code, bổ sung/sửa test, chạy xác minh thật và gom commit theo từng nhóm chức năng. Không dừng lại ở việc phân tích hoặc viết thêm một kế hoạch mới.

---

## 1. Mục tiêu cuối cùng

Đưa dự án từ trạng thái:

```text
⛔ NOT READY — 7 P0, 5 P1, 6 P2, 3 P3
```

thành trạng thái có thể chứng minh:

```text
✅ READY — code, Entity Framework, backend, frontend, test và SQL cùng tuân thủ Pure V2-only
```

Kết quả không được dựa vào suy đoán. Mỗi phần chỉ được đánh dấu hoàn thành khi có:

- thay đổi code cụ thể;
- test tương ứng;
- lệnh build/test/import đã chạy thật;
- kết quả thực tế;
- bằng chứng không còn lỗi hoặc dependency V1 ở runtime.

---

## 2. Bốn quyết định đã được chủ dự án chốt

Các quyết định dưới đây là yêu cầu hiện hành, **không hỏi lại và không mở lại conflict**.

### DECISION-01 — Đầu mối vận hành lưu riêng theo từng campus

- Mỗi `visit_request_campus` phải có đúng dữ liệu form của campus đó trong `visit_instance_form_details`.
- `operational_contact_*` phải lấy từ payload của chính campus tương ứng.
- Không dùng `contact_person_*` cấp đơn làm fallback khi create, OTP verify, pending edit, resubmit, safe edit hoặc amendment.
- Không sao chép một đầu mối cấp đơn sang tất cả campus ở runtime.
- Việc SQL seed hiện copy dữ liệu request-level chỉ là cách dựng dữ liệu mẫu, không phải business rule runtime.
- Danh sách khách và nhân sự hỗ trợ cũng phải giữ đúng liên kết từng campus; không tự biến dữ liệu riêng thành danh sách dùng chung.

### DECISION-02 — Hệ thống chuyển hẳn sang Pure V2-only

- Không còn dual-read, dual-write hoặc fallback về V1.
- Không được giữ trạng thái “V2 tắt thì dùng V1”; V1 đã retired và trả `410`.
- Trong giai đoạn chuyển tiếp:
  - backend tạm giữ API capability cho client cũ nhưng luôn trả `enabled=true`;
  - kết quả không được phụ thuộc hai feature flag cũ;
  - frontend bỏ gọi/chờ capability và mở thẳng luồng V2.
- Chỉ xóa hẳn endpoint capability sau khi có xác nhận frontend và backend mới đều đã được triển khai. Nếu chưa có xác nhận deploy, giữ endpoint tương thích nhưng đánh dấu deprecated; không giữ dead configuration.

### DECISION-03 — Mọi SQL verification phải thực sự bằng 0

- Giữ tiêu chuẩn mọi `issue_count = 0`.
- Không bỏ qua 14 false-fail của negative guard harness.
- Phải sửa đúng thứ tự `GET DIAGNOSTICS`, không làm yếu trigger.
- Phải xử lý 151 placeholder và 3 operational instance thiếu agenda.
- Phải bổ sung kiểm tra `form_schema_version` cho cả `visit_requests` và `visit_request_pending_forms` vào self-check Pure V2.

### DECISION-04 — Cấu hình không nhạy cảm vẫn có thể được track

- Không bắt buộc xóa toàn bộ `appsettings.json` khỏi Git.
- Giữ file cấu hình chung nếu file chỉ chứa giá trị không nhạy cảm.
- Xóa mọi SMTP password, JWT secret và credential thật khỏi file được track.
- Lấy secret từ environment variable hoặc secret manager của môi trường chạy.
- File cấu hình local có secret phải nằm trong `.gitignore`.
- Không tự rewrite Git history. Đây là tác vụ riêng, ảnh hưởng nhiều branch và chỉ được làm khi chủ dự án phê duyệt rõ ràng.

---

## 3. Repository và baseline bắt buộc xác minh

```text
Repository: quangthoai04/PEMS
Remote branch: Cảnh-Iter1
Local branch audit trước đó: Canh-Iter1, tracking origin/Cảnh-Iter1
HEAD đã audit: 19bed5101b8b3bd564d438e6add90c67a2f83fa6
Merge-base với Dev tại thời điểm audit:
584f3ddace324eb0b4f6916ca586e6f1b2e05090
```

SQL canonical đã audit:

```text
docs/database/scripts/PEMS_FULL_V2_ONLY_CANONICAL_TRANSLATION_GALLERY_FAQ_VISION_GUARD_DIRECT_SEED_NO_STAGING_LATEST (1).sql

Git blob SHA-1:
825b95672491d653d5537c95b4e81f3c000b229f

SHA-256:
7ec63e9044ecd1910e9a7137c99773bb13b36902f3042fd7bc6cfce402892415

Số dòng theo wc -l: 14,832
Persistent tables: 81
Views: 0
Triggers: 32
Persistent object pems_seed_*: 0
Direct-seed request batches: 19 / 90 / 8
```

### Preflight bắt buộc

Trước khi sửa:

```bash
git status --short --branch
git branch --show-current
git rev-parse HEAD
git log -10 --oneline --decorate
git merge-base Dev HEAD
git rev-list --left-right --count Dev...HEAD
```

Quy tắc:

1. Chỉ làm trên branch đang tracking `origin/Cảnh-Iter1`.
2. Tuyệt đối không sửa, commit, push hoặc merge vào `Dev`.
3. Không checkout/reset/rebase khi working tree có thay đổi chưa rõ chủ sở hữu.
4. Không dùng `git reset --hard`, `git checkout -- <file>` hoặc lệnh phá hủy thay đổi.
5. Nếu HEAD đã khác `19bed510...`:
   - ghi lại HEAD mới;
   - đọc toàn bộ commit mới;
   - xác định gap nào đã được sửa hoặc phát sinh;
   - điều chỉnh implementation theo code thật;
   - không reset về HEAD cũ.
6. Nếu SQL canonical đã đổi hash:
   - dừng phần import/contract test;
   - báo chính xác path, hash cũ, hash mới và commit làm thay đổi;
   - không tự dùng hash cũ để công nhận PASS.
7. Bảo toàn mọi thay đổi không thuộc nhiệm vụ của người dùng.

---

## 4. Nguồn sự thật và tài liệu phải đọc

Thứ tự ưu tiên khi có khác biệt:

1. Bốn quyết định đã chốt trong prompt này.
2. SQL canonical mới nhất đối với schema, enum, constraint, FK, trigger và seed verification.
3. Business rule và permission hiện hành trong:
   - `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`
   - `PERMISSION_RULES.md`
   - `PERMISSION_MATRIX.md`
   - `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`
4. Code và test tại HEAD thực tế.
5. `CLEAN_ARCHITECTURE.md`, `PROJECT_STRUCTURE_FULL.md` và UI design system để giữ đúng cấu trúc dự án.
6. Handoff, plan và report cũ chỉ dùng làm lịch sử; không dùng nội dung V1 cũ để phủ nhận Pure V2.

Đọc báo cáo audit:

```text
PEMS_AUDIT_CODE_VS_PURE_V2_SQL_2026-07-23.md
```

Không lặp lại toàn bộ audit. Dùng report để triển khai, nhưng luôn đối chiếu lại code thật trước khi sửa.

---

## 5. Phạm vi được phép và điều bị cấm

### Được phép

- sửa backend, frontend, test, SQL canonical và tài liệu liên quan trực tiếp;
- thêm test contract/integration cần thiết;
- chạy formatter có phạm vi đúng file đã sửa;
- chạy build, unit, architecture, integration và real-stack E2E an toàn;
- tạo commit cục bộ theo functional slice sau khi slice đã qua gate.

### Không được phép

- sửa trực tiếp `Dev`;
- merge/rebase `Dev` vào branch nếu chưa được yêu cầu;
- push, mở PR hoặc merge PR nếu chủ dự án chưa yêu cầu rõ;
- force-push hoặc rewrite Git history;
- tự rotate credential bằng cách đoán hoặc tạo secret mới trong repo;
- in SMTP password, JWT secret, token, connection string hoặc credential ra terminal/report;
- chạy SQL trên `pems_db`, Railway, production hoặc database có dữ liệu cần giữ;
- khôi phục staging table/view `pems_seed_*`;
- tạo EF migration để tái sinh 12 cột đã bị xóa;
- sửa/xóa trigger chỉ để negative test xanh;
- xóa mọi hit có tên `DelegationName`, `Purpose`, `WorkingLanguage`... một cách mù quáng; các field trên DTO và `VisitInstanceFormDetail` vẫn hợp lệ;
- format hàng loạt file không liên quan;
- tạo commit chỉ có một report nếu có thể gom report vào functional slice;
- đưa `Claude`, `ChatGPT`, `Codex`, `AI-generated`, `Co-authored-by AI` hoặc tên AI khác vào commit subject/body/metadata.

---

## 6. Baseline lỗi đã được chứng minh

### P0

| Gap | Lỗi đã xác minh | Vùng chính |
|---|---|---|
| GAP-001 | EF map 12 cột không tồn tại; MySQL 1054 | `VisitRequest.cs`, `VisitRequestPendingForm.cs` |
| GAP-002 | Integration bootstrap trỏ SQL đã xóa và fail-open | `DisposableDatabaseManager.cs` |
| GAP-003 | Solution không build vì thiếu `VisitRequestFingerprintGuards` | `GalleryTestDbContext.cs` |
| GAP-004 | Write path còn ghi cột request-level đã xóa | create/edit/resubmit/safe-edit/OTP |
| GAP-005 | V1 trả 410 nhưng V2 flags mặc định false | capability/config/frontend entry |
| GAP-006 | Runtime còn dual-read theo discriminator | `VisitFormReadService`, `VisitInstanceEffectiveName` |
| GAP-007 | SMTP password và JWT secret đã tồn tại trong Git history | cấu hình + credential rotation |

### P1

- Frontend route theo `formSchemaVersion` và có thể đưa mọi row sang `unsupported-version`.
- SQL negative guard harness báo sai 14 failure vì gọi `SET` trước `GET DIAGNOSTICS`.
- Enum consistency test trỏ SQL không tồn tại.
- V1 service còn được DI đăng ký; `VisitSafeEditService` vẫn phục vụ route V2 thật.
- Real-stack E2E script trỏ SQL cũ đã xóa.

### P2/P3 cần đóng trong cùng chương trình

- Stale SQL path ở `TestTc`, review script và phase candidate scripts.
- EF delete behavior chưa khớp hoàn toàn SQL; ví dụ `VisitorUserId` đang `Restrict` trong EF nhưng SQL là `SET NULL`.
- 151 seed placeholder còn sót.
- 3 operational instance thiếu agenda.
- Nullability lệch ở `email_templates`, `news_content_sections`.
- Disposable real-stack DB không luôn được cleanup.
- 5 entity chưa expose qua `IApplicationDbContext`.
- Comment/doc dual-read đã lỗi thời.

Không được coi số liệu trên là cố định nếu HEAD đã đổi. Phải xác minh và ghi nhận delta.

---

## 7. Cách triển khai chung

Thực hiện tuần tự theo phase dưới đây vì có phụ thuộc. Trong mỗi phase:

1. đọc code và test liên quan;
2. nêu ngắn gọn root cause vừa xác minh;
3. sửa nhỏ nhất nhưng hoàn chỉnh;
4. thêm/sửa test chứng minh đúng contract;
5. chạy gate của phase;
6. xem `git diff --check` và diff thật;
7. chỉ commit khi gate của slice xanh;
8. cập nhật implementation report;
9. mới chuyển phase tiếp theo.

Không dừng sau một bản kế hoạch khác. Chỉ dừng khi:

- có thay đổi người dùng chồng trực tiếp lên file phải sửa;
- SQL/baseline đã đổi theo cách làm thay đổi contract;
- cần thao tác ngoài repository mà agent không có quyền, ví dụ rotate credential;
- không thể tạo database disposable an toàn;
- phát hiện nguy cơ chạm database thật.

Credential rotation là manual security gate. Nếu chưa được chủ dự án xác nhận đã rotate, vẫn có thể hoàn thành refactor local an toàn, nhưng:

- luôn giữ trạng thái security/deploy là **BLOCKED**;
- không tuyên bố READY;
- không deploy;
- không ghi secret thay thế vào repo.

---

## 8. PHASE 0 — Security, build và test infrastructure

### 0A. Externalize secret và khóa deploy

Mục tiêu:

- repository không còn giá trị SMTP password/JWT secret thật ở HEAD;
- runtime đọc secret từ environment;
- thiếu secret ở môi trường cần thiết phải fail rõ ràng, không âm thầm dùng giá trị yếu;
- credential cũ phải được chủ dự án rotate bên Gmail/provider và môi trường deploy.

Thực hiện:

1. Kiểm tra cấu hình bằng tên key, không in value.
2. Xóa giá trị nhạy cảm khỏi file tracked.
3. Giữ `appsettings.json` với cấu hình không nhạy cảm.
4. Thêm hoặc cập nhật file mẫu an toàn nếu thực sự cần.
5. Bảo đảm local secret override bị ignore.
6. Kiểm tra Railway/production binding dùng environment variable tương ứng ở code/config; không tự thay đổi môi trường ngoài repo nếu không có quyền.
7. Thêm startup/config validation phù hợp:
   - production không chấp nhận JWT secret rỗng hoặc placeholder;
   - SMTP bật thì credential bắt buộc hợp lệ;
   - test có thể inject fake configuration.
8. Không đưa secret vào test fixture, snapshot, log hoặc exception.
9. Không rewrite history trong task này.

Xác minh:

```bash
git grep -n -I -E '"(Password|SecretKey)"[[:space:]]*:[[:space:]]*"[^"]+"' -- \
  ':!*.example.json' ':!*Test*'
```

Không in match nếu command có nguy cơ lộ value; có thể dùng script chỉ trả tên file/key và số lượng.

Kết luận security chỉ PASS khi:

- HEAD sạch secret;
- runtime binding đúng;
- test cấu hình xanh;
- chủ dự án xác nhận credential cũ đã rotate.

### 0B. Khôi phục solution build

Sửa `GalleryTestDbContext` để implement:

```text
IApplicationDbContext.VisitRequestFingerprintGuards
```

Không chỉ thêm code cho compile; bảo đảm test harness khởi tạo DbSet đúng pattern của bốn harness đã cập nhật.

Gate:

```bash
dotnet build PEMS.slnx
dotnet test tests/PEMS.ArchitectureTests --no-restore
dotnet test tests/PEMS.UnitTests --no-restore
```

### 0C. Sửa integration bootstrap theo hướng fail-closed

`DisposableDatabaseManager` và mọi consumer test phải:

1. trỏ đúng duy nhất SQL canonical;
2. fail ngay nếu file thiếu;
3. fail nếu có nhiều candidate;
4. xác minh SHA-256 mong đợi;
5. không fallback sang tên SQL cũ;
6. tạo database disposable bằng tên allowlist duy nhất;
7. retarget mọi statement chọn/tạo/xóa database, không chỉ thay một câu `USE`;
8. quét lại bản SQL tạm trước import;
9. từ chối nếu còn statement trỏ `pems_db` ngoài comment;
10. từ chối `SOURCE`, `\.` hoặc client include ngoài dự kiến;
11. import bằng MySQL 8 disposable;
12. sau import xác minh:
    - `DATABASE()` đúng target;
    - 81 base tables;
    - 0 view;
    - 0 object `pems_seed_*`;
    - 32 trigger;
    - không có `form_schema_version` ở hai bảng;
    - không có 10 cột form-global trên `visit_requests`;
    - mọi assertion cuối SQL bằng 0;
13. cleanup database trong `finally`, kể cả test/import fail.

Thêm test cho chính bootstrap:

- thiếu file → throw;
- sai hash → throw;
- nhiều candidate → throw;
- còn database statement ngoài allowlist → throw;
- import fail → cleanup;
- import thành công → đủ invariant.

Sửa các SQL reference đang stale trong:

- `DocumentsOwnerTypeEnumConsistencyTests.cs`;
- `frontend/pems-react/scripts/run-realstack-e2e.mjs`;
- `TestTc/Program.cs`;
- `docs/database/scripts/review_env/Build-ReviewDatabase.ps1`;
- `phase_1_candidate/generate_fresh_target.ps1`;
- `phase_1_candidate/tests/Test-SqlSafetyGuard.ps1`;
- mọi path runtime/test khác được `rg` phát hiện.

Không thay bằng wildcard thiếu kiểm soát.

### Exit Phase 0

- `dotnet build PEMS.slnx` = 0 error.
- Architecture test xanh.
- Unit test chạy được.
- Bootstrap fail-closed được test.
- Không consumer đang chạy trỏ tên SQL đã xóa.
- Security code/config ở HEAD không chứa credential thật.
- Nếu credential chưa rotate: báo rõ `SECURITY ROTATION PENDING`, không giả PASS.

---

## 9. PHASE 1 — Domain và EF Core khớp Pure V2

### 1A. Xóa đúng 12 phantom mapping

Xóa khỏi `VisitRequestPendingForm`:

```text
FormSchemaVersion
```

Xóa khỏi `VisitRequest`:

```text
FormSchemaVersion
DelegationName
VisitType
VisitTypeOther
Purpose
WorkingContent
WorkingLanguage
TransportationNote
MediaConsentStatus
MediaConsentNote
NoteToFptu
```

Sau khi xóa, dùng compiler và `rg` để xử lý mọi consumer; không tạo `[NotMapped]` để che lỗi nếu runtime vẫn phụ thuộc dữ liệu request-level.

Không xóa các field cùng tên khi chúng thuộc:

- `VisitInstanceFormDetail`;
- DTO/API response hợp lệ;
- agenda template;
- email template;
- module khác có column riêng trong SQL.

### 1B. Rà EF mapping theo SQL source of truth

Đối chiếu tối thiểu:

- type, max length, precision;
- nullability và default;
- alternate/composite key;
- index/unique constraint;
- generated column;
- FK và `DeleteBehavior`.

Xử lý có bằng chứng:

- `visit_requests.visitor_user_id`: EF phải khớp SQL `SET NULL`;
- `email_templates.purpose`: CLR phải phản ánh DB NOT NULL;
- `news_content_sections.section_title`: CLR phải phản ánh DB NOT NULL;
- `news_content_sections.section_body_html`: CLR phải phản ánh DB NOT NULL;
- `visit_expense_items.total_amount`: giữ semantics computed/generated an toàn;
- rà 5 entity chưa expose qua `IApplicationDbContext`; thêm chỉ khi application layer cần contract đó hoặc interface được thiết kế để phủ toàn context, không thêm máy móc.

Không tạo migration để sửa ngược SQL canonical nếu schema hiện tại là chủ đích.

### 1C. Thêm schema contract test chạy trên MySQL thật

Test phải:

1. dùng bootstrap canonical đã sửa;
2. build EF model;
3. materialize query tối thiểu cho mọi mapped entity/DbSet;
4. bắt lỗi unknown table/column/type;
5. kiểm tra relationship trọng yếu;
6. write/read round-trip cho:
   - `visit_requests`;
   - `visit_request_pending_forms`;
   - `visit_request_campuses`;
   - `visit_instance_form_details`;
   - guest/member link;
   - identity change;
   - amendment;
   - revision history;
   - fingerprint guard.

### Exit Phase 1

- 81/81 persistent table có mapping chủ đích.
- 0 mapped column không tồn tại.
- Không còn 12 phantom property.
- Contract test chạy thật trên canonical SQL xanh.
- Không có `Unknown column` hoặc `Unknown table`.

---

## 10. PHASE 2 — Pure V2 write/read core

### 2A. Create và public OTP

Rà và sửa:

- `VisitRequestV2CreateService`;
- `InitiateVisitRequestV2CommandHandler`;
- verify-v2 handler/service;
- pending snapshot/fingerprint guard;
- notification first-create-only;
- idempotency/replay logic.

Contract bắt buộc:

1. `visit_requests` chỉ lưu metadata cấp đơn, danh tính và trạng thái thực sự tồn tại trong schema.
2. Tạo đúng N `visit_request_campuses`.
3. Mỗi campus có đúng một `visit_instance_form_details`.
4. Mỗi detail lấy field của chính campus trong payload.
5. Không ghi bất kỳ cột global-form đã xóa nào.
6. Không ghi discriminator.
7. Guest/support member liên kết đúng campus.
8. `HasMixedCampusDetails` được tính server-side, không tin client.
9. Request, campus, detail, member, revision, identity claim và fingerprint nằm trong transaction phù hợp.
10. Failure ở giữa không để lại request/campus/detail dở dang.
11. Idempotency không tạo duplicate request hoặc gửi notification lần hai.
12. OTP snapshot không cần `FormSchemaVersion`; payload snapshot phải phục hồi đủ Pure V2.

Test tối thiểu:

- single campus;
- multi-campus có nội dung giống nhau;
- multi-campus có nội dung khác nhau;
- từng campus có operational contact khác nhau;
- member chỉ thuộc một campus;
- người đăng ký khác đầu mối chính;
- OTP replay;
- fingerprint collision/replay;
- transaction rollback;
- min duration và validation error.

### 2B. Pending edit, resubmit và safe edit

Rà và sửa:

- `VisitRequestV2EditService`;
- pending-edit;
- resubmit after reject;
- `VisitSafeEditService`;
- route `PATCH /api/v2/visit-requests/{id}/safe-details`;
- row version/revision;
- change classifier.

Yêu cầu:

- chỉ update đúng `VisitInstanceFormDetail` của campus target;
- không copy detail campus A sang campus B;
- không dùng request-level fallback;
- preserve field không thuộc phạm vi edit;
- optimistic concurrency hoạt động ở request và instance theo contract hiện có;
- recompute mixed indicator server-side;
- revision snapshot phản ánh dữ liệu sau update;
- reject stale row version;
- rollback toàn transaction khi một campus fail.

### 2C. Amendment, claim và transfer

Chứng minh các luồng sau vẫn chạy sau khi bỏ property V1:

- initial primary-contact claim;
- transfer initiate/accept/decline/resend/cancel/expire;
- amendment draft/submit/approve/reject/withdraw/expire;
- media consent withdrawal urgent path;
- approve target campus only.

Không mở rộng quyền chỉ vì một người là registrant hoặc primary contact.

### 2D. Read service V2-only

Rà và sửa:

- `VisitFormReadService.cs`;
- `IVisitFormReadService`;
- `VisitInstanceEffectiveName.cs`;
- `ResolvedVisitFormDto`;
- mọi helper dual-read.

Yêu cầu:

- bỏ hoàn toàn nhánh dựa vào `FormSchemaVersion`;
- request-level summary dùng rule deterministic hiện hành;
- instance-level detail luôn lấy detail của đúng instance;
- thiếu detail phải fail rõ ràng, không fallback sang request-global;
- mixed request không tự lấy campus đầu tiên làm dữ liệu chung;
- giữ DTO field hợp lệ nếu frontend/API còn cần, nhưng nguồn phải là Pure V2.

### 2E. Gỡ V1 runtime thật sự chết

- Gỡ DI và route/service V1 không còn reachable.
- Giữ `410 VISIT_FORM_V1_RETIRED` chỉ khi còn cần compatibility contract; không gọi lại V1 service.
- Không xóa `VisitSafeEditService` chỉ vì tên xuất hiện trong inventory: route V2 của nó đang sống, phải refactor.
- Phân loại test V1:
  - chuyển thành Pure V2 nếu test còn bảo vệ nghiệp vụ;
  - xóa nếu chỉ bảo vệ hành vi đã retired;
  - giữ test 410 nếu endpoint retired vẫn còn public.

### Exit Phase 2

Các luồng sau xanh trên MySQL canonical:

```text
create-v2
public initiate OTP
verify-v2
idempotency/replay
pending edit
resubmit
safe edit
claim
transfer
amendment
cancel
campus approval/rejection
host assignment
```

Và:

- 0 partial data;
- 0 double notification;
- 0 runtime read/write `form_schema_version`;
- 0 runtime read/write 10 global-form column.

---

## 11. PHASE 3 — Downstream query, scope và authorization

Rà toàn bộ consumer của `VisitRequest` và form data, gồm:

- request/instance lists;
- detail và editable detail;
- search/filter/pagination;
- Dashboard, HO dashboard, report overview;
- calendar;
- invitation;
- process/contribution;
- agenda;
- feedback;
- meeting minutes;
- documents/media/photo;
- expense/invoice/report/export;
- email action và notification;
- host/staff/staff leader/department/campus/HO views.

Quy tắc:

1. Instance surface đọc `VisitInstanceFormDetail` của đúng instance.
2. Request surface:
   - nếu uniform, có thể dùng summary deterministic;
   - nếu mixed, dùng nhãn an toàn như `Khác nhau theo cơ sở` hoặc contract đã quy định;
   - không rò dữ liệu campus ngoài scope.
3. Không dùng `FirstOrDefault()` như business fallback nếu thứ tự campus không phải contract.
4. Authorization/scope phải được áp dụng trước keyword search và trước projection dữ liệu nhạy cảm.
5. Registrant, primary contact, operational contact và host là các quan hệ khác nhau; không suy quyền từ email trùng nhau.
6. Đầu mối vận hành từng campus không tự có quyền duyệt hoặc gán host.
7. Không tạo N+1 query; dùng projection/include có chủ đích.
8. Không làm mất pagination, sort, filter hoặc error code hiện hành.

Tạo `QUERY CONSUMER MATRIX` trong implementation report:

| Surface | Actor/scope | Nguồn detail | Mixed behavior | Test | Kết quả |
|---|---|---|---|---|---|

Không được ghi `static-PASS`; phase này yêu cầu runtime test.

### Exit Phase 3

- Tất cả surface trọng yếu có integration test.
- Không có cross-campus leakage.
- Scope-before-keyword được test bằng dữ liệu có keyword trùng ngoài scope.
- Query mixed/uniform trả đúng contract.
- Export/email/report dùng đúng instance detail.

---

## 12. PHASE 4 — API và frontend V2-only

### 4A. Backend capability chuyển tiếp

- Loại dependency vào `PerCampusFormV2Options.Enabled` và write flag.
- Capability endpoint tạm thời luôn trả V2 enabled cho client cũ.
- Không để cấu hình có thể làm V2 bị tắt.
- Thêm test chứng minh mọi environment config không tạo deadlock.
- Đánh dấu endpoint deprecated nếu kiến trúc dự án có convention tương ứng.
- Không xóa endpoint trong phase này nếu chưa có xác nhận cả frontend/backend mới đã deploy.

### 4B. API contract

- Xóa `formSchemaVersion` khỏi request/response DTO nếu không còn ý nghĩa.
- Nếu cần giữ tạm field response vì compatibility, giá trị phải là constant V2 và có kế hoạch xóa rõ; không đọc database discriminator.
- Đồng bộ nullable, required, max length, enum và error code với SQL/business rule.
- Không nhận `hasMixedCampusDetails` như nguồn sự thật từ client.

### 4C. Frontend route thẳng V2

Rà và sửa tối thiểu:

- `visitVersionRouting.ts`;
- `VisitRequestManagement.tsx`;
- `SubmittedVisitRequestDetailModal.tsx`;
- `delegations.types.ts`;
- `visitRequestV2Api.ts`;
- capability helper/hook;
- `useVisitEntryCta.tsx`;
- route `unsupported-version`;
- mọi CTA mở form.

Yêu cầu:

1. Không route/render theo `formSchemaVersion`.
2. Không gọi capability trước khi mở V2.
3. Hero, final CTA, FAQ, Partner và Dashboard đều mở luồng V2 thống nhất.
4. Deep link và refresh vẫn hoạt động.
5. Edit/resubmit/detail không rơi vào `unsupported-version`.
6. Multi-campus form giữ riêng:
   - mục đích;
   - nội dung làm việc;
   - operational contact;
   - ngôn ngữ;
   - transportation note;
   - media consent;
   - guest/support members.
7. Khi lưu/sửa một campus không ghi đè campus khác.
8. Validation frontend chỉ hỗ trợ UX; backend vẫn enforce.
9. Giữ modal, dirty prompt, sticky submit và hành vi hiện hành đã được xác nhận.

Test frontend:

- cập nhật unit test bị ảnh hưởng;
- thêm test direct V2 CTA;
- detail/edit/resubmit khi response không có discriminator;
- single/multi-campus preservation;
- stale response protection nếu luồng translation bị tác động;
- deep link/refresh.

### Exit Phase 4

- Frontend không còn runtime dependency `formSchemaVersion`.
- Không còn capability deadlock.
- CTA luôn vào V2.
- Backend tương thích client cũ ở endpoint capability.
- `npm run lint`, unit test và build xanh.

---

## 13. PHASE 5 — Module liên quan không được regression

Audit đã xác nhận mapping tên cột của Translation/Gallery/FAQ/Partner/Vision/Expense khớp ở mức tĩnh. Không sửa lan rộng nếu runtime test không phát hiện lỗi.

Phải chạy/bổ sung contract test cho:

### Translation, News, FAQ, Partner

- VI/EN được lưu đúng bảng translation/content hiện hành;
- update song ngữ nằm trong transaction phù hợp;
- auto-translation không ghi đè bản EN đã sửa tay;
- FAQ English dùng cache/runtime rule hiện hành;
- public read không gọi translation API không cần thiết;
- không phá scope của student/staff khi tạo News.

### Gallery

- description/content đọc từ cấu trúc canonical;
- media tối đa theo rule hiện hành;
- video upload và thumbnail không bị ảnh hưởng;
- không tái sinh bảng `gallery_item_tts_audios` đã bỏ.

### Vision và ảnh đoàn

- detection và manual tagging đúng contract;
- guest tag thuộc đúng visit instance;
- confirmation idempotent;
- không truy cập ảnh ngoài scope.

### Expense/report

- generated `total_amount` được đọc an toàn;
- report/invoice lấy đúng đoàn và campus;
- không dùng global-form column đã xóa.

Nếu test xanh và không có mismatch, ghi `VERIFIED — NO CODE CHANGE`, không tạo thay đổi giả.

---

## 14. PHASE 6 — Sửa SQL verification và seed data

Chỉ sửa đúng SQL canonical hiện hành.

### 6A. Negative guard harness

Trong exception handler:

- `GET DIAGNOSTICS` phải là câu đầu tiên lấy diagnostics;
- chỉ sau đó mới set biến đánh dấu;
- giữ nguyên trigger/guard business rule;
- test trực tiếp vẫn phải nhận đúng SQLSTATE/message mong đợi;
- 14/14 negative cases phải PASS thật.

Không xóa negative case hoặc đổi expected thành giá trị sai để làm xanh.

### 6B. Seed placeholder

- Xác định đủ 151 value còn chứa `Seed coverage:`.
- Thay bằng nội dung dữ liệu mẫu có nghĩa và phù hợp từng case.
- Không chỉ đổi chuỗi sang placeholder khác.
- Sau import, `seed_placeholder_terms_remaining = 0`.

### 6C. Operational instance thiếu agenda

Xử lý đúng ba instance đã phát hiện:

```text
5075 — DURING_VISIT
5085 — AFTER_VISIT
5146 — CLOSED
```

Seed agenda hợp lệ theo trạng thái và campus tương ứng; không hạ trạng thái để né invariant.

Sau import:

```text
operational_visit_instances_missing_agenda_final = 0
```

### 6D. Pure V2 self-check

Bổ sung assertion từ `information_schema`:

- `visit_requests.form_schema_version` không tồn tại;
- `visit_request_pending_forms.form_schema_version` không tồn tại;
- 10 global-form column không tồn tại trên `visit_requests`;
- 0 object prefix `pems_seed_`;
- mỗi campus instance có đúng một detail;
- mọi invariant hiện hành vẫn giữ.

### 6E. Re-import

Trên MySQL 8 disposable:

1. import fresh;
2. kiểm tra exit code/stderr;
3. chạy lại trên cùng disposable target để chứng minh rerunnable;
4. chạy independent `information_schema` verification;
5. chạy mọi stored/self-check;
6. xác nhận tất cả `issue_count = 0`;
7. cleanup trong mọi trường hợp.

SQL sau khi sửa sẽ có hash mới. Cập nhật có chủ đích:

- expected hash trong bootstrap;
- report;
- test guard;
- không giữ hash cũ như thể vẫn canonical.

---

## 15. PHASE 7 — Cleanup có kiểm soát

Chỉ làm sau khi Phase 0–6 xanh:

- xóa V1 dead handler/service/helper không còn reachable;
- xóa config class/registration của hai flag cũ;
- frontend đã không còn gọi capability;
- giữ capability endpoint hard-coded `enabled=true` nếu chưa có xác nhận deploy hai phía;
- tạo follow-up rõ ràng để xóa endpoint sau deploy, không giả vờ đã hoàn tất rollout;
- xóa route `unsupported-version` nếu không còn consumer;
- xóa stale SQL path;
- cập nhật comment/docs dual-read lỗi thời;
- rà `FormSchemaVersion|form_schema_version|formSchemaVersion`.

Mỗi hit còn lại phải thuộc một trong các nhóm:

1. SQL negative assertion xác minh column không tồn tại;
2. test compatibility 410 có chủ đích;
3. tài liệu lịch sử được ghi rõ là legacy;
4. không còn hit runtime.

Không xóa tài liệu lịch sử nếu việc xóa không cần thiết; chỉ không để nó bị hiểu nhầm là contract hiện hành.

---

## 16. Test matrix cuối cùng

### Backend

```bash
dotnet restore PEMS.slnx
dotnet build PEMS.slnx --no-restore
dotnet test tests/PEMS.ArchitectureTests --no-build
dotnet test tests/PEMS.UnitTests --no-build
dotnet test tests/PEMS.IntegrationTests
```

Dùng đúng path thực tế nếu tên project khác. Không báo PASS nếu project bị skip hoặc không discover test.

### Frontend

Tại đúng thư mục frontend:

```bash
npm run lint
npm run test:unit
npm run build
```

Nếu lockfile yêu cầu clean install, dùng package-manager tương ứng, không tự đổi lockfile ngoài ý muốn.

### SQL

- fresh import MySQL 8 disposable;
- rerun idempotent;
- 81 tables;
- 0 view;
- 32 trigger;
- 0 `pems_seed_*`;
- mọi `issue_count = 0`;
- contract test EF xanh.

### E2E/real-stack

Chỉ chạy khi:

- bootstrap đã fail-closed và import đúng canonical;
- email, Translation, Vision/Drive và outbound integration được cô lập bằng sink/fake an toàn;
- không có nguy cơ gửi email thật.

Critical journeys:

- public create + OTP verify;
- internal create;
- pending edit;
- reject + resubmit;
- claim/transfer;
- amendment;
- campus approval + host assignment;
- detail/edit/resubmit frontend;
- mixed-campus read;
- report/export/email preview không gửi thật.

### Security regression

- không có secret thật ở HEAD;
- log không lộ secret;
- production validation từ chối missing/placeholder secret;
- credential rotation được chủ dự án xác nhận;
- không rewrite history ngoài scope.

---

## 17. Chiến lược commit bắt buộc

Commit theo functional slice, chỉ commit khi test liên quan xanh. Có thể điều chỉnh số commit theo diff thật, nhưng giữ ranh giới nghiệp vụ:

```text
1. chore(security): externalize runtime secrets and validate configuration
2. fix(test-infra): restore solution build and fail-close canonical SQL bootstrap
3. refactor(domain): align visit entities and EF mappings with Pure V2 schema
4. refactor(visit): migrate create, OTP and edit writes to per-campus details
5. refactor(visit): make form reads Pure V2-only
6. refactor(consumers): read per-instance form details across downstream queries
7. refactor(frontend): route all visit flows directly to V2
8. fix(sql): repair guard verification and complete canonical seed invariants
9. chore(visit): remove verified V1 dead code and stale documentation
```

Quy tắc commit:

- Không nhất thiết một file là một commit.
- Không gom toàn bộ dự án thành một commit khổng lồ.
- Security/config tách khỏi schema refactor.
- Mỗi commit phải build được hoặc ghi rõ vì sao một chuỗi commit tạm thời không thể tách; ưu tiên lịch sử luôn build được.
- Trước commit:

```bash
git status --short
git diff --check
git diff --stat
git diff
```

- Không stage file ngoài phạm vi.
- Commit subject ngắn, mô tả chức năng.
- Không có tên AI trong subject, body hoặc trailer.
- Không push, merge hoặc mở PR nếu chưa được yêu cầu.

Nếu working tree ban đầu đã có thay đổi của người dùng, không đưa chúng vào commit của nhiệm vụ này.

---

## 18. Implementation report phải duy trì

Tạo/cập nhật:

```text
docs/Ver2Carnh/PEMS_PURE_V2_IMPLEMENTATION_REPORT.md
```

Không tạo commit chỉ chứa report; gom report vào functional slice phù hợp.

Report gồm:

1. baseline ban đầu và HEAD thực tế;
2. quyết định đã chốt;
3. gap register P0/P1/P2/P3 với trạng thái:
   - OPEN;
   - IN PROGRESS;
   - FIXED;
   - VERIFIED;
   - BLOCKED;
4. file đã sửa theo phase;
5. test đã thêm/sửa;
6. command đã chạy và kết quả thật;
7. SQL path/hash mới nếu SQL thay đổi;
8. query consumer matrix runtime;
9. commit hash theo slice;
10. blocker còn lại;
11. credential rotation confirmation chỉ ghi trạng thái, không ghi value;
12. final Definition of Done.

Không dán credential hoặc connection string vào report.

---

## 19. Definition of Done

Chỉ kết luận `READY` khi tất cả mục sau đạt:

```text
[ ] Làm việc đúng branch tracking origin/Cảnh-Iter1; không sửa Dev.
[ ] Credential thật đã bị xóa khỏi HEAD.
[ ] Chủ dự án xác nhận SMTP password và JWT secret cũ đã rotate.
[ ] Production lấy secret từ environment/secret manager và fail rõ khi thiếu.
[ ] dotnet build PEMS.slnx = 0 error.
[ ] Architecture tests xanh.
[ ] Unit tests xanh, không bị skip do build.
[ ] Integration bootstrap fail-closed.
[ ] Bootstrap import đúng một SQL canonical và verify đúng hash mới nhất.
[ ] SQL fresh import + rerun thành công trên MySQL 8 disposable.
[ ] 81 tables, 0 view, 32 trigger, 0 pems_seed_*.
[ ] Mọi SQL issue_count = 0.
[ ] Negative guard 14/14 PASS thật; trigger vẫn chặn dữ liệu sai.
[ ] 151 placeholder đã về 0.
[ ] 3 operational instance thiếu agenda đã về 0.
[ ] Self-check xác minh không còn discriminator ở cả hai bảng.
[ ] Không còn 12 phantom EF mapping.
[ ] EF contract test materialize được toàn bộ mapping trên schema thật.
[ ] Không còn runtime read/write form_schema_version.
[ ] Không còn runtime read/write 10 global-form column.
[ ] Create/OTP/edit/resubmit/safe-edit ghi detail riêng từng campus.
[ ] Không fallback operational contact request-level.
[ ] Guest/support members giữ đúng campus.
[ ] Claim/transfer/amendment/revision/idempotency xanh.
[ ] Downstream query đọc đúng instance detail.
[ ] Scope-before-keyword và cross-campus isolation được test.
[ ] Frontend không route/render theo formSchemaVersion.
[ ] Frontend không phụ thuộc capability trước khi mở V2.
[ ] Capability endpoint chuyển tiếp luôn enabled cho client cũ.
[ ] Frontend lint, unit test và build xanh.
[ ] Translation/Gallery/FAQ/Partner/Vision/Expense runtime contract xanh.
[ ] Real-stack critical E2E xanh trong môi trường outbound an toàn.
[ ] Không còn stale SQL runtime/test reference.
[ ] Không còn V1 service/handler reachable ngoài compatibility 410 có chủ đích.
[ ] Không có regression permission, audit, notification hoặc idempotency.
[ ] Mọi commit được gom theo chức năng và không chứa tên AI.
```

Nếu còn bất kỳ P0/P1 hoặc credential rotation chưa được xác nhận:

```text
⛔ NOT READY
```

Không được đổi thành `READY WITH CAVEATS`.

---

## 20. Cách báo cáo cuối phiên

Trả về theo thứ tự:

1. **Kết luận:** READY hoặc NOT READY.
2. **HEAD và branch cuối.**
3. **Các phase đã hoàn thành.**
4. **Gap đã đóng/chưa đóng.**
5. **Build/test/SQL/E2E thực chạy.**
6. **SQL canonical path/hash cuối.**
7. **Danh sách commit local đã tạo.**
8. **Blocker cần chủ dự án làm**, đặc biệt credential rotation/deploy sequencing.
9. **File report.**

Không chỉ nói “đã sửa xong”. Mọi PASS phải kèm số test hoặc kết quả xác minh cụ thể.

---

## 21. Bắt đầu ngay

Thực hiện theo thứ tự:

1. Preflight Git và baseline.
2. Kiểm tra trạng thái credential rotation mà không đọc/in secret.
3. Phase 0B để solution build và unit test chạy được.
4. Phase 0C để khóa integration bootstrap vào SQL canonical.
5. Tiếp tục Phase 1 → Phase 7.

Không hỏi lại DECISION-01 đến DECISION-04. Chỉ hỏi khi gặp đúng blocker ngoài quyền hoặc thay đổi mới làm contract không còn khớp baseline.
