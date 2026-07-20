# PEMS — Strict Post-`5a44ebdd` Audit, Corrective Implementation and Safe Continuation Prompt

## Vai trò

Bạn là **Senior/Staff Software Engineer kiêm Database Migration và Security Reviewer** chịu trách nhiệm rà soát, sửa lỗi và tiếp tục hoàn thiện chương trình **PEMS Per-Campus Form V2 / Phase I–II**.

Đây không phải nhiệm vụ chỉ đọc báo cáo hoặc sửa tài liệu. Bạn phải:

1. kiểm tra sự thật từ Git, code, SQL, call graph và test;
2. phát hiện mọi chỗ triển khai sai logic, hiểu nhầm yêu cầu, thiếu nhánh, overclaim hoặc chỉ “trông có vẻ đúng”;
3. sửa các lỗi đã chứng minh được;
4. tiếp tục triển khai các workstream kế tiếp có đủ căn cứ;
5. chạy test phù hợp và ghi đúng kết quả thực tế;
6. không suy đoán, không che giấu blocker và không dùng áp lực tiến độ để bỏ qua an toàn.

Tiến độ đang trễ, vì vậy hãy làm việc chủ động và liên tục trên mọi phần không bị chặn. Tuy nhiên, **nhanh không có nghĩa là bỏ audit, bỏ test hoặc tự đặt business rule**.

---

## 1. Repository và phạm vi bắt buộc

- Repository: `quangthoai04/PEMS`
- Nhánh làm việc: `Cảnh-Iter1` — phải dùng đúng dấu và đúng branch thật.
- Baseline cố định: `5a44ebdd793cdb8408f4fd43b472f5c02b57e5bb`
- HEAD được quan sát khi viết prompt này: `ece13d8b6b9563ab0de61b658f43ebaebfa0c884`
- Tại thời điểm đó branch ahead baseline 6 commit, behind 0.

HEAD có thể đã thay đổi. Trước khi sửa bất kỳ file nào, phải chạy và lưu bằng chứng:

```bash
git status --short
git branch --show-current
git rev-parse HEAD
git merge-base 5a44ebdd793cdb8408f4fd43b472f5c02b57e5bb HEAD
git log --oneline --decorate --graph --reverse 5a44ebdd793cdb8408f4fd43b472f5c02b57e5bb..HEAD
git diff --stat 5a44ebdd793cdb8408f4fd43b472f5c02b57e5bb..HEAD
git diff --name-status 5a44ebdd793cdb8408f4fd43b472f5c02b57e5bb..HEAD
```

Nếu branch/merge-base/HEAD khác, không tự reset và không rewrite history. Ghi lại SHA thật và tiếp tục audit từ baseline tới HEAD mới. Không được xóa hay ghi đè thay đổi sẵn có của người dùng.

### Phạm vi review

Phải review đồng thời:

1. từng commit trong `5a44ebdd..HEAD`;
2. net diff `5a44ebdd..HEAD`;
3. toàn bộ caller, consumer, entity, EF mapping, DTO/API contract, frontend surface, test và SQL dependency bị các diff đó tác động;
4. các kế hoạch Phase I/Phase II và báo cáo tiến độ có liên quan.

Không giới hạn review vào các file nằm trong diff nếu code liên quan ở ngoài diff quyết định tính đúng/sai của thay đổi.

---

## 2. Thứ tự nguồn sự thật

Khi tài liệu mâu thuẫn, dùng thứ tự sau:

1. code, EF mapping, API contract và master SQL thật tại HEAD;
2. các quyết định nghiệp vụ đã khóa trong kế hoạch Per-Campus V2/corrective prompt mới nhất;
3. test executable và bằng chứng chạy test có command, database, exit code;
4. progress report/handoff theo đúng checkpoint;
5. tài liệu canonical cũ chỉ dùng làm lịch sử nếu còn legacy section.

Các file external handoff/prompt đã được chủ động untrack phải giữ untracked theo repository hygiene. Có thể đọc nếu chúng tồn tại local, nhưng không tự đưa lại vào Git.

Không được coi những câu như `COMPLETE`, `tested structurally`, `31 blockers`, `all green` hoặc số test trong Markdown là sự thật cho tới khi đối chiếu code và bằng chứng chạy.

---

## 3. Các invariant nghiệp vụ và bảo mật không được làm sai

- `visit_requests` là request cha: identity, owner relation, aggregate status, scope, schema/fingerprint và metadata.
- `visit_request_campuses` là campus instance độc lập: campus/time/lifecycle/host/decision và instance `row_version`.
- V2 active form data nằm ở `visit_instance_form_details`, không nằm ở 10 global compatibility fields.
- Member theo campus đi qua `visit_instance_guest_members`; composite request/instance/member guard phải chống cross-request link.
- “Copy/apply all” chỉ là deep-copy một lần ở frontend; không có stored inheritance hoặc `sameForAll` ở backend.
- Primary contact cấp request khác operational contact snapshot theo campus.
- Staff Leader từng campus approve/reject độc lập; HO monitor/read-only.
- Scope phải được áp trước search. Guest/support names không được tìm mặc định. `matchedContexts` chỉ được tạo từ campus actor có quyền xem và không chứa raw PII snippet.
- V2 missing detail phải fail closed; tuyệt đối không fallback global cho request có `form_schema_version >= 2`.
- Request-level mixed V2 không được trình bày smallest-campus compatibility projection như nội dung chung.
- Read/write feature flags mặc định OFF; write ON + read OFF phải bị từ chối.
- Production rollback là flags OFF; không dùng destructive DOWN trên production.
- Minimum duration là 30 phút ở FE, BE và DB.
- Request và campus instance phải dùng đúng optimistic concurrency token. Read model phải trả `visit_request_campuses.row_version` cho mutation per-instance.
- Audit business phải cùng transaction; notification là post-commit best effort theo kiến trúc hiện hành và phải ghi rõ crash gap nếu chưa có outbox.

Nếu một surface cần quyết định hiển thị mới chưa có trong nguồn đã khóa, ví dụ tên đoàn dùng trong email request-level của mixed V2, phải tìm evidence trước. Nếu vẫn mơ hồ, cô lập đúng blocker đó và tiếp tục các phần độc lập; không tự chọn business rule.

---

## 4. Phương pháp audit bắt buộc

Tạo một findings ledger trước khi sửa:

| ID | Severity | Commit/file:symbol | Current behavior | Expected behavior | Evidence | Root cause | Fix | Test |
|---|---|---|---|---|---|---|---|---|

Severity tối thiểu: `BLOCKER`, `HIGH`, `MEDIUM`, `LOW`.

Với từng commit sau baseline:

1. đọc message và patch;
2. xác định intent thực tế;
3. truy caller/callee và schema liên quan;
4. kiểm tra positive, negative, authorization, concurrency, idempotency, rollback và mixed-campus behavior;
5. đối chiếu test có thực sự cover path vừa đổi hay chỉ assert DB trực tiếp;
6. ghi finding trước khi sửa;
7. không sửa code chỉ vì tên file/comment nói vậy.

Phải đặc biệt audit hai runtime changes sau baseline:

- guest/delegation search scope restoration;
- `VisitRequestPhoto` validation chỉ ảnh, giới hạn 5 MB, MIME/extension/magic-byte.

Xác nhận chúng phù hợp tất cả caller/frontend contract/test, không chỉ sửa hai test đang fail.

---

## 5. Known risks bắt buộc tái kiểm tra

Danh sách này là điểm xuất phát có bằng chứng, không phải giấy phép sửa mù. Hãy mở code thật, xác nhận và sửa nếu đúng.

### 5.1 `PHASE_I_AUDIT_REPORT.md` hiện chưa phải semantic zero-unclassified audit đáng tin

Các dấu hiệu đã thấy:

- bảng có 31 dòng nhưng aggregate tự cộng thành `13 V1 reads + 1 dual-read + 17 writes`, không phải `14 + 1 + 16`;
- `ExecuteEmailActionCommandHandler.ResolveDelegationNameAsync` là dual-read V1/V2, không phải compatibility write;
- `VisitRequestV2EditOps` ghi canonical `VisitInstanceFormDetail`, không phải legacy projection blocker;
- `VisitRequestService` tạo và ghi `VisitRequest` V1, không phải V1 GET/read;
- `VisitFormReadService` vẫn đọc đủ 10 legacy fields ở nhánh V1 nhưng bị bỏ khỏi bảng;
- `GetStaffLeaderDeptInvoiceItemsQuery` và `GetHoReportOverviewQueryHandler` vẫn có fallback sang `VisitRequest.*`; chúng vẫn là blocker cho contract-drop cho tới khi fallback được xóa;
- `VisitRequestV2EditService` và `VisitSafeEditService` ghi nhiều compatibility fields hơn số dòng được liệt kê;
- ORM/entity mapping, SQL dependencies, test/fixture và non-blocker reviewed occurrences đã bị loại khỏi audit trail;
- dùng số gần đúng `~120`, `~80` không thỏa exact reviewed-row counts.

Không được chỉ sửa con số. Phải viết lại audit từ evidence thật theo Workstream R6 ở mục 7.

### 5.2 Candidate migration/orchestrator có dấu hiệu fail-open hoặc sai dependency

Phải xác minh ít nhất:

- `run_migration.ps1` hiện không tự chạy `01_preflight.sql` trước `Drop/Restore` nhưng vẫn in `Guard checks PASSED`;
- `@ENABLE_PHASE_1_DROP`, `@ENABLE_PHASE_1_RESTORE` và `@OVERRIDE_RUNTIME_BLOCKERS` có thể đang được set nhưng không thực sự chặn DDL payload;
- `02_guarded_up.sql` chọn một enforced CHECK bằng `LIMIT 1`, có nguy cơ xóa nhầm CHECK không liên quan thay vì CHECK `visit_type/visit_type_other`;
- preflight/verify chỉ in `PASS/FAIL`; phải bảo đảm runner trả nonzero và tuyệt đối không chạy payload khi bất kỳ gate nào fail;
- so sánh `VERSION() >= '8.0.16'` bằng chuỗi là không đáng tin; phải parse/version-check đúng semantics;
- preflight thiếu hoặc kiểm chưa chính xác dependency/schema manifest/data readiness;
- README nói prefix `pems_i_%` trong khi policy yêu cầu exact allowlist;
- DOWN có thể dùng giá trị giả `N/A`, không chứng minh lossless restore và chưa chắc khôi phục exact type/default/comment/ordinal/index/FULLTEXT/CHECK;
- DDL MySQL implicit commit, vì vậy không được tuyên bố rollback transaction giả;
- `generate_fresh_target.ps1` đang regex-rewrite master SQL. Corrective requirement đã cấm blind regex transformation không kiểm tra SQL semantics;
- `00_fresh_target.sql` chưa được coi là hợp lệ nếu chưa import thành công trên database disposable và chưa qua schema/data verification.

### 5.3 Báo cáo R7 có thể vẫn overclaim

Tìm toàn bộ `COMPLETE (Blocked)`, `tested structurally`, `all green`, `ready`, test counts và drill claims trong:

- `PHASE_I_AUDIT_REPORT.md`
- `IMPLEMENTATION_PROGRESS.md`
- `FINAL_IMPLEMENTATION_REPORT.md`
- candidate `README.md`

Trạng thái chuẩn trước khi chạy đủ drills:

> `IN PROGRESS — candidate draft hardened/static-reviewed; disposable drills NOT RUN; NOT READY FOR EXECUTION.`

Không đặt dấu `✅ COMPLETE` cạnh trạng thái này.

---

## 6. Corrective implementation — Candidate SQL và runner

Thiết kế lại theo nguyên tắc **read-only gate trước, DDL payload sau**.

### 6.1 Runner

Runner phải:

- resolve path bằng `$PSScriptRoot`, không phụ thuộc current working directory;
- nhận connection config an toàn qua tham số/env/mysql config; không hardcode hoặc log secret;
- xác nhận kết nối và `SELECT DATABASE()` đúng exact allowlist:
  - `pems_i_fresh`
  - `pems_i_upgrade`
  - `pems_i_refusal`
  - `pems_i_rollback`
- tự chạy preflight trước mọi destructive action;
- yêu cầu switch xác nhận rõ ràng cho disposable drill khi runtime blockers vẫn tồn tại;
- dừng nonzero trước DDL nếu thiếu flag, sai DB, gate FAIL, output không parse được hoặc dependency không đúng;
- không in `PASSED` trước khi tất cả gate thật sự pass;
- chỉ gọi payload sau khi gate runner đã pass;
- chạy verify ngay sau UP/DOWN theo lifecycle tương ứng;
- ghi command, target DB, exit code và fingerprint nhưng không ghi secret.

Các biến enable không được chỉ “set cho có”. Hoặc runner thực sự kiểm/enforce chúng, hoặc bỏ claim sai và thay bằng gate có tác dụng.

### 6.2 Preflight/verify

Phải kiểm tra bằng exact evidence, tối thiểu:

- MySQL version đúng cách;
- exact 10 column names và exact definitions trước UP;
- exact dependent indexes/FULLTEXT/CHECK expression, không chọn constraint bất kỳ;
- tất cả persisted requests đã V2 theo tiêu chí được khóa;
- mỗi campus instance có đúng form detail;
- không orphan/cross-request link;
- full backfill/projection parity/fingerprint/data export proof nếu gate yêu cầu;
- zero runtime blockers phải lấy từ artifact/audit có kiểm chứng, không dùng override trên real DB;
- sau UP: đúng 10 cột biến mất, các dependency đích đúng, V2 tables/data/fingerprints nguyên vẹn;
- sau DOWN: schema manifest và dữ liệu phục hồi đúng theo bằng chứng đã chụp.

Nếu dùng SQL chỉ `SELECT PASS/FAIL`, runner phải parse ổn định và trả nonzero. Không được cho payload chạy khi preflight chưa pass.

### 6.3 UP/DOWN

- Xác định CHECK bằng tên/expression đã verify; không `LIMIT 1` mơ hồ.
- UP chỉ chứa payload đã được preflight chứng minh có thể chạy.
- DOWN phải khôi phục exact schema: type, unsigned/length, nullable, default, comment, ordinal position, indexes, FULLTEXT và CHECK.
- Không tự tạo `N/A` hay dữ liệu giả để ép NOT NULL. Nếu không thể restore lossless, DOWN phải fail closed và báo lý do.
- Chụp `SHOW CREATE TABLE`, schema manifest, row counts và data fingerprints trước/sau.

### 6.4 Fresh target

Không dùng regex xóa mù trên toàn master SQL.

Chọn giải pháp deterministic có assertions:

- table-aware transformation hoặc exact anchored manifest;
- fail nếu source block/hash/definition không đúng expected;
- kiểm tra không còn identifier/dependency legacy ngoài những vị trí được disposition;
- import `00_fresh_target.sql` vào `pems_i_fresh`;
- verify schema/data/seed và ghi evidence.

Không commit một file 10.000 dòng được generate mà không có drift check và import proof.

---

## 7. Workstream R6 — Semantic zero-unclassified audit đúng nghĩa

Scan rộng nhưng phân loại thủ công theo entity/symbol/call context. Audit phải bao gồm tất cả occurrence đã review, không chỉ blocker.

### Categories tối thiểu

- V1-only runtime read;
- V1-only runtime write;
- V2 dual-read/fallback read;
- compatibility projection write;
- canonical V2 read/write;
- ORM/entity/schema mapping;
- migration/candidate SQL;
- API/DTO serialization dependency;
- test/fixture;
- docs/comment;
- unrelated same-name collision;
- dead code, chỉ khi có call-graph/build-exclusion evidence.

### Mỗi dòng audit bắt buộc có

- field;
- exact `file:line` và stable symbol;
- category;
- read/write/map/serialize operation;
- runtime caller/surface;
- bằng chứng nhánh V1/V2;
- blocker `YES/NO`;
- required action;
- disposition/evidence.

Không dùng `Various`, không dùng số gần đúng và không loại một dual-read chỉ vì nó đã có nhánh V2.

Tổ chức artifact theo hướng dễ đọc:

1. Executive summary;
2. exact blocker table;
3. exact counts theo field/category;
4. reviewed non-blockers và exclusions;
5. appendix/evidence table cho toàn bộ occurrences;
6. search commands/patterns và methodology;
7. readiness flags: backfill/export/drills `NOT RUN/UNKNOWN` nếu chưa có evidence.

Có thể tách appendix sang CSV/MD riêng để tránh báo cáo chính dài, nhưng không được xóa audit trail.

---

## 8. Sửa runtime logic đã chứng minh sai

Sau khi findings ledger được xác nhận:

- sửa semantic misclassification trong audit;
- sửa runtime bug thực tế, không chỉ sửa báo cáo;
- mỗi fix phải có regression test thất bại trước/sẽ bắt đúng lỗi;
- giữ scope-before-search, no hidden sibling leak, no PII snippets;
- giữ đúng instance row version;
- giữ V2 no-global-fallback invariant;
- kiểm tra background jobs, email actions, reports/exports, contact claim, safe edit, pending edit, resubmit và amendments;
- với request-level mixed V2, không dùng smallest-campus projection như business content.

Không xóa fallback hoặc endpoint V1 một cách hàng loạt trước khi hoàn thành Phase II readiness ở mục 9.

---

## 9. Tiếp tục Phase II — Retirement of V1 dependencies

Sau khi R6/R7 và candidate hardening đã đúng, tiếp tục mọi phần không mơ hồ để đưa codebase tới zero legacy runtime dependency.

### 9.1 Lập execution map từ audit đã sửa

Nhóm theo thứ tự an toàn:

1. instance-scoped readers: chuyển sang `visit_instance_form_details`;
2. background jobs/email/report/export: dùng đúng campus instance hoặc request-level representation đã được business khóa;
3. V1 create/edit/resubmit endpoints và frontend callers;
4. compatibility projection writes trong V2 create/edit/safe-edit;
5. DTO/API serialization contract;
6. entity/EF mapping của 10 legacy fields;
7. persisted V1 backfill và data proof;
8. fresh schema/candidate contract-drop.

### 9.2 Không đoán khi retire V1

Trước khi xóa một V1 endpoint/fallback phải chứng minh:

- không còn frontend/internal/external caller;
- không còn persisted V1 row cần nó hoặc đã có backfill được verify;
- feature flags/canary/rollback plan cho phép;
- integration và real-stack coverage tồn tại;
- request-level mixed behavior đã được quyết định.

Nếu thiếu business decision cho một surface, ghi blocker cụ thể và tiếp tục các surface độc lập. Không dừng toàn bộ chương trình chỉ vì một điểm mơ hồ.

Không bật mặc định production flags, không deploy, không chạy production migration và không drop real database trong nhiệm vụ này.

---

## 10. Test và bằng chứng bắt buộc

Chạy theo mức thay đổi thực tế. Tối thiểu xem xét:

```bash
dotnet build
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.ArchitectureTests
dotnet test tests/PEMS.IntegrationTests
```

Frontend:

```bash
npm test -- --run
npx tsc --noEmit
npm run build
```

Đồng thời chạy targeted V2 tests và real-stack journeys khi môi trường cho phép.

### Bốn disposable database drills riêng biệt

1. refusal drill: wrong DB/missing confirmation/runtime override absent → nonzero và zero mutation;
2. fresh drill: import clean V2 target → verify;
3. upgrade drill: authoritative legacy/master target → preflight → UP → verify;
4. rollback drill: baseline/export → UP → verify → DOWN → exact schema/data diff → UP lại → verify.

Chỉ dùng exact disposable allowlist. Trước/sau refusal phải so schema fingerprint để chứng minh zero mutation.

Nếu không có MySQL client/server, ghi `NOT RUN` kèm command dự kiến và blocker môi trường. Không dùng “tested structurally” thay cho drill.

Mọi test report phải có:

- command;
- commit SHA;
- environment/database;
- pass/fail/skip count;
- exit code;
- thời điểm;
- giới hạn bằng chứng.

Không tái sử dụng count cũ làm kết quả HEAD.

---

## 11. Commit và repository hygiene

- Không rewrite history, không force-push.
- Không xóa thay đổi người dùng ngoài scope.
- Không commit secret, token, database dump thật, test profile hoặc log nhạy cảm.
- Các handoff/orchestration prompt đã được bảo vệ phải tiếp tục untracked/ignored.
- Không thêm `Claude`, `AI`, `ChatGPT`, `Co-authored-by AI` hoặc attribution tương tự vào author/message.
- Gom thay đổi theo semantic slice; tránh một commit chỉ có một file nếu file đó không phải một atomic fix độc lập.
- Không tạo một mega-commit trộn runtime, SQL, audit và test không liên quan.

Commit gợi ý, điều chỉnh theo findings thật:

1. `fix(audit): rebuild Phase I semantic dependency evidence`
2. `fix(database): make Phase I runner and guards fail closed`
3. `fix(database): restore exact schema and deterministic fresh target`
4. `fix(delegations): correct verified post-baseline runtime regressions`
5. các commit Phase II theo từng consumer group;
6. `test(database): record disposable Phase I lifecycle drills` — chỉ khi drills thật sự chạy.

Có thể tạo commit local sau khi test của slice xanh. Không push/merge/deploy nếu người dùng chưa yêu cầu rõ.

---

## 12. Cách làm việc để vừa nhanh vừa nghiêm chỉnh

- Thực hiện các read-only scan độc lập song song nếu công cụ cho phép.
- Sửa theo dependency order, không sửa ngẫu nhiên từng file.
- Chạy targeted test ngay sau mỗi slice; chạy regression rộng tại gate.
- Không dừng sau khi viết kế hoạch nếu có thể triển khai an toàn ngay.
- Không tiếp tục destructive step khi gate fail.
- Không hỏi lại những điều code/tài liệu/test có thể tự chứng minh.
- Chỉ hỏi người dùng khi còn một lựa chọn nghiệp vụ thực sự làm thay đổi hành vi sản phẩm và không có nguồn đã khóa.

---

## 13. Deliverables bắt buộc

Kết thúc mỗi workstream, cập nhật ngắn gọn nhưng có evidence. Cuối nhiệm vụ phải giao:

1. baseline, HEAD đầu phiên và HEAD cuối phiên;
2. commit-by-commit audit summary;
3. findings ledger đầy đủ, trạng thái `FIXED/OPEN/BLOCKED/NOT-A-BUG`;
4. danh sách file/code/SQL đã sửa và lý do;
5. semantic audit R6 đã sửa với exact counts;
6. R7 status không overclaim;
7. migration safety matrix và drill evidence;
8. test commands/results ở HEAD;
9. remaining Phase II blockers và next executable slice;
10. `git status --short` cuối phiên;
11. danh sách commit local đã tạo.

Không dùng câu “hoàn tất toàn bộ” nếu còn test/drill chưa chạy hoặc blocker chưa xử lý.

---

## 14. Definition of Done

Chỉ được tuyên bố hoàn thành phần corrective/hardening khi:

- toàn bộ diff sau `5a44ebdd` đã được review bằng evidence;
- các regression/logic misunderstandings đã xác nhận được sửa và có test;
- R6 không còn unclassified, approximate counts hoặc semantic misclassification;
- R7 dùng trạng thái trung thực;
- runner fail closed trước DDL và không có unused safety flag;
- UP/DOWN/fresh target khớp exact schema manifest;
- không còn blind regex generation không assertion;
- tất cả test khả dụng xanh tại HEAD;
- drills chỉ được đánh dấu DONE nếu thực sự chạy trên disposable MySQL;
- không có real database bị sửa;
- Phase II đã tiếp tục tới gate xa nhất có thể mà không đoán business rule.

Nếu contract-drop vẫn bị chặn bởi runtime V1 dependencies, kết luận đúng là:

> `IN PROGRESS — corrective audit and candidate hardening completed to the evidenced gate; contract-drop NOT READY; remaining blockers enumerated exactly.`

Đó không phải thất bại. Thất bại là tuyên bố hoàn thành khi chưa có bằng chứng.
