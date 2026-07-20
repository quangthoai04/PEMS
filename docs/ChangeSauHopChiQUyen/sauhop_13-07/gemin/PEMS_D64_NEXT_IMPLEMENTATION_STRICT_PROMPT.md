# PEMS — Prompt tiếp tục code sau `d64bde66`

> Prompt này là chỉ thị thực thi tự chứa cho AI coding agent/developer tiếp theo. Không chỉ lập kế hoạch hoặc viết báo cáo. Phải trực tiếp kiểm tra repository thật, sửa code/SQL/tests, chạy các gate có thể chạy, tạo commit theo từng semantic slice và báo cáo bằng chứng trung thực.

## 1. Vai trò và mục tiêu

Bạn là **Senior Software Architect + Senior Full-stack Engineer + Database Migration Engineer + Security/Test Engineer** tiếp tục chương trình **PEMS Per-Campus Form V2** trên repository:

- GitHub: `quangthoai04/PEMS`
- remote branch: `Cảnh-Iter1`
- local branch có thể là `Canh-Iter1`
- checkpoint đã review gần nhất: `d64bde66973caaf9765070e529cb1599743ec03d`
- baseline lịch sử của đợt audit trước: `5a44ebdd793cdb8408f4fd43b472f5c02b57e5bb`

Mục tiêu của phiên này:

1. sửa nốt các lỗi safety còn tồn tại trong Phase I migration candidate;
2. hoàn thành R6 thành semantic zero-unclassified audit đúng nghĩa;
3. sửa các V2 reader vẫn dùng legacy projection dù đã có canonical detail;
4. tiếp tục các phần Phase II độc lập, không dùng một business decision mơ hồ để dừng toàn bộ chương trình;
5. thay fresh-target blind regex bằng cơ chế deterministic có assertions và chạy drill nếu môi trường cho phép;
6. giữ trạng thái/báo cáo/test evidence chính xác, tuyệt đối không đoán hoặc overclaim.

Không được drop 10 cột trên database thật. Không bật production flags. Không deploy, merge hoặc push nếu người dùng chưa yêu cầu rõ.

---

## 2. Quy tắc bắt đầu phiên

Trước khi sửa bất kỳ file nào, chạy và ghi lại bằng chứng:

```bash
git status --short --branch
git remote -v
git fetch --all --prune
git rev-parse HEAD
git rev-parse origin/Cảnh-Iter1
git merge-base d64bde66973caaf9765070e529cb1599743ec03d HEAD
git log --oneline --decorate --graph -30
git diff --stat d64bde66973caaf9765070e529cb1599743ec03d..HEAD
```

Quy tắc:

- Nếu HEAD vẫn là `d64bde66`, tiếp tục từ đó.
- Nếu HEAD đã đi tiếp, không reset/rewrite history. Audit toàn bộ commit mới từ `d64bde66..HEAD` trước khi thay đổi.
- Nếu local/remote lệch, ghi rõ ahead/behind/divergence; không tự merge/rebase khi chưa hiểu nguyên nhân.
- Giữ nguyên mọi thay đổi chưa commit của người dùng. Không xóa hoặc commit các prompt/handoff đang được để untracked/ignored.
- Nếu file đang có thay đổi ngoài scope, tránh ghi đè. Chỉ dừng hỏi khi thực sự không thể tách thay đổi an toàn.
- Có thể tạo commit local sau khi gate của slice xanh. Không force-push, không amend/rewrite commit cũ.
- Nếu cần cấu hình author, chỉ dùng repository-local config. Không thêm `AI`, `ChatGPT`, `Claude` hoặc attribution AI vào author/message/trailer.

Tạo findings ledger ngay từ đầu với các trạng thái:

`OPEN | CONFIRMED | FIXED | BLOCKED | NOT-A-BUG | NEEDS-BUSINESS-DECISION`

Mọi finding phải có exact `file:line`, symbol, caller/surface, impact, reproduction/evidence, fix và test/drill bắt lỗi.

---

## 3. Nguồn sự thật và thứ tự ưu tiên

Đọc code, schema và tests thật trước khi tin báo cáo. Thứ tự ưu tiên:

1. source code + tests tại HEAD;
2. authoritative master SQL hiện có trong repository, đặc biệt `docs/database/scripts/PEMS_FULL_V11_REMOVED_TTS_19_07_26.sql` hoặc successor được Git history chứng minh;
3. `PEMS_PER_CAMPUS_V2_MASTER_HANDOFF_PROMPT(1).md`;
4. `PEMS_CANONICAL_BUSINESS_RULES...md`;
5. rulebook/use cases/permission rules/clean architecture/project overview;
6. migration candidate và báo cáo tiến độ hiện tại.

Các file sau là **artifact cần kiểm chứng/sửa**, không phải nguồn sự thật tự thân:

- `docs/ChangeSauHopChiQUyen/sauhop_13-07/PHASE_I_AUDIT_REPORT.md`
- `docs/ChangeSauHopChiQUyen/sauhop_13-07/IMPLEMENTATION_PROGRESS.md`
- `docs/ChangeSauHopChiQUyen/sauhop_13-07/FINAL_IMPLEMENTATION_REPORT.md`
- `docs/database/scripts/phase_1_candidate/*`

Nếu tài liệu và code mâu thuẫn, tìm Git history, tests và canonical business rules. Không tự chọn hành vi sản phẩm mới.

---

## 4. Các invariant đã khóa — không được hiểu lại

### 4.1 Per-campus V2

- Với `form_schema_version >= 2`, source of truth của 10 form fields là `visit_instance_form_details`.
- Global fields trên `visit_requests` chỉ là compatibility projection tạm thời.
- V2 read/business/search/report không được dùng global projection làm source.
- Instance-scoped consumer phải lấy form/delegation từ **target instance**.
- Missing V2 detail không được fallback về global legacy fields; dùng error/integrity behavior ổn định đã có trong project.
- Request-level flat handler gặp mixed V2 trả stable `409` theo contract hiện hành.
- Scope/authorization phải áp dụng trước keyword, projection, pagination enrichment hoặc matched contexts.
- Không được leak hidden sibling campus hoặc PII snippets.
- V1 phải giữ hành vi hiện hữu cho tới khi V1 caller/backfill/cutover được chứng minh.

### 4.2 Compatibility projection

- V2 uniform: projection là common snapshot.
- V2 mixed: projection dùng campus có `campus_id` nhỏ nhất **chỉ để compatibility/NOT NULL**.
- Không được dùng smallest-campus projection làm business display/search/report/email content của V2.
- `contact_person_*` là authoritative request-level primary-contact snapshot, không thuộc 10 legacy form fields.

### 4.3 Database safety

- MySQL DDL auto-commit; transaction không thể cứu một chuỗi ALTER đã chạy một phần.
- Mọi điều kiện có thể chứng minh phải được kiểm **trước ALTER đầu tiên**.
- Chỉ bốn database disposable được phép làm target destructive, exact match:
  - `pems_i_fresh`
  - `pems_i_upgrade`
  - `pems_i_refusal`
  - `pems_i_rollback`
- Tuyệt đối không mutate `pems_db`, `pems_test`, `pems_pr3_test` hoặc database thật khác.
- Production rollback là flags OFF/canary rollback, không phải destructive DOWN.

---

## 5. Known findings bắt buộc tái kiểm tra

Không được tin mù danh sách này; hãy mở code thật và xác nhận. Nhưng không được bỏ qua finding nào.

### F1 — R6 vẫn partial

Current audit tự ghi:

> `semantic occurrence audit is PARTIALLY COMPLETE`

Nó có raw census `1172 hits / 137 files` ở checkpoint nhưng chưa có per-occurrence disposition. Con số này phải được tính lại tại HEAD, không hardcode nếu code đã đổi.

### F2 — blocker table hiện không exhaustive

Ít nhất hai site đã được biết nhưng không xuất hiện trong bảng blocker §3:

- `backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderDeptInvoiceItemsQuery.cs`
- `backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs`

`GetStaffLeaderDeptInvoiceItemsQuery` hiện chỉ dùng `FormDetail` khi V2 **và mixed**; V2 uniform vẫn dùng `VisitRequest.DelegationName`. Đây vẫn là legacy dependency và vi phạm V2 canonical-read invariant.

`GetHoReportOverviewQueryHandler` vẫn dùng `VisitRequest.VisitType` cho V2 uniform tại nhiều request/instance/report filters. Đây cũng là blocker.

Do đó không được giữ câu “exactly 10 blocker sites” cho tới khi R6 hoàn chỉnh.

### F3 — `check_projection_parity` không kiểm parity

Trong `01_preflight.sql`, gate được đặt tên projection parity nhưng hiện chỉ kiểm request có ít nhất một projectable form detail. Nó không so sánh 10 legacy values với deterministic compatibility projection.

Hệ quả: một DB có legacy projection lệch canonical detail vẫn có thể pass UP; sau DOWN, dữ liệu legacy có thể bị ghi đè bằng giá trị khác. Fingerprint trùng trên một fixture không chứng minh lossless cho mọi state được preflight chấp nhận.

### F4 — DOWN không có read-only preflight

`run_migration.ps1 -Action Down` hiện gọi `04_down_restore.sql` trực tiếp. `04_down_restore.sql` ADD COLUMN và UPDATE trước khi kiểm `@unbackfilled`. Nếu gate này fail, schema đã bị mutate và auto-commit một phần.

Đây không phải fail-closed theo contract “all guards before first ALTER”.

### F5 — exact schema checks chưa exact

Các điểm phải xác minh/sửa:

- indexes chủ yếu chỉ được kiểm theo tên, chưa kiểm exact ordered columns, `SEQ_IN_INDEX`, index type và uniqueness;
- FULLTEXT chỉ kiểm có/không `delegation_name`, chưa kiểm exact ordered member set;
- CHECK được nhận diện bằng `LIKE '%visit_type%'`, chưa đối chiếu exact normalized expression;
- column preflight/verify chưa kiểm đầy đủ comment, ordinal, default, charset/collation/generation attributes cần thiết;
- verify mode ngoài `UP|DOWN` có thể làm các mode-specific checks bị skip;
- “exactly one detail per instance” không được chứng minh nếu chỉ đếm missing và so tổng row count;
- cần kiểm `form_schema_version IS NULL` cùng với `<> 2`;
- cần kiểm views/triggers/generated columns/FK/routines hoặc schema dependencies khác có thể làm DROP fail giữa chừng.

### F6 — Phase II đã bị dừng quá rộng

Mixed request-level email representation có thể cần business decision, nhưng không chặn các instance-scoped readers hoặc filters có canonical semantics đã khóa. Phải tiếp tục phần độc lập thay vì dừng toàn bộ.

### F7 — fresh target vẫn untrusted

`generate_fresh_target.ps1` đang dùng blind regex rewrite; `00_fresh_target.sql` chưa được import/verify. Không được đánh dấu fresh target DONE trước khi thay bằng deterministic transformation có assertions và drill thật.

### F8 — test/drill evidence chưa đầy đủ

Current report có counts và fingerprints nhưng thiếu raw command/timestamp/log artifact đầy đủ. GitHub không có Actions run cho checkpoint; Vercel statuses fail do permission/team invitation không phải proof code test.

### F9 — frontend text nhỏ

`VisitPhotoPanel.tsx` đã giới hạn upload image-only 5 MB nhưng success toast vẫn ghi `ảnh/video`. Có thể giữ video rendering để xem historical files; chỉ sửa upload-facing text nếu evidence xác nhận.

### F10 — phantom search hit còn mở

Các common-data clauses `RegistrantFullName/Nationality/JobTitle` có thể surface row nhưng không có matched-context field code tương ứng. Phải bảo đảm searchable fields và matched-context contract đồng bộ, nhưng không tự quyết định mở rộng searchable PII nếu canonical rule không khóa.

---

## 6. Workstream A — làm migration candidate thật sự fail-closed

### A1. Tách rõ UP preflight và DOWN preflight

Có thể dùng hai file hoặc một file có mode, nhưng contract phải rõ và machine-parseable:

- `UP` preflight chỉ pass trên exact pre-UP state.
- `DOWN` preflight chỉ pass trên exact post-UP state và chứng minh toàn bộ source data cần restore trước ALTER đầu tiên.
- mode thiếu/sai phải `FAIL`, mysql/runner exit nonzero.
- runner phải chạy đúng preflight trước **cả UP và DOWN**.
- output phải có đúng một verdict token; runner từ chối missing, duplicated hoặc contradictory verdict.
- preflight và payload chạy ở hai mysql process khác nhau thì payload phải re-check critical immutable prerequisites để giảm TOCTOU. Cờ session tự set không được coi là bằng chứng đủ.

### A2. UP preflight — exact proof

Tối thiểu kiểm:

1. exact disposable DB allowlist;
2. đúng MySQL product/version; không để MariaDB hoặc version parse sai pass ngoài ý muốn;
3. exact `visit_requests` table/engine/charset/collation cần thiết;
4. exact 10 legacy columns:
   - name;
   - ordinal position;
   - data/column type;
   - length/enum members;
   - nullable;
   - default;
   - comment;
   - charset/collation/generation/extra attributes khi áp dụng;
5. exact dependent indexes:
   - exact name;
   - exact ordered member columns;
   - `SEQ_IN_INDEX`;
   - `INDEX_TYPE`;
   - uniqueness/visibility nếu áp dụng;
6. exact FULLTEXT member list và order;
7. exact normalized `visit_type/visit_type_other` CHECK expression và exactly one match;
8. các CHECK khác không tham chiếu 10 fields và vẫn giữ nguyên;
9. không có FK/view/trigger/generated column/routine/schema dependency ngoài manifest có thể làm DROP fail sau một số DDL đã commit;
10. mọi persisted request có `form_schema_version = 2`, bao gồm fail khi NULL;
11. mỗi `visit_request_campuses` instance có **exactly one** `visit_instance_form_details` row;
12. không duplicate, orphan hoặc cross-request/cross-instance inconsistency;
13. canonical mandatory fields đủ để restore NOT NULL;
14. zero runtime blockers lấy từ audit artifact đã hoàn chỉnh; override chỉ được phép khi runner đang chạy explicit disposable drill.

### A3. Sửa projection parity đúng nghĩa

Tạo deterministic source projection cho mỗi request:

- chọn campus theo `campus_id ASC`, tie-break bằng stable instance id nếu cần;
- exactly one selected source row/request;
- so sánh **đủ 10 fields** giữa `visit_requests` và selected `visit_instance_form_details`;
- dùng MySQL null-safe equality (`<=>`) cho nullable fields;
- so sánh stored value chính xác, không `COALESCE`, trim hoặc normalize làm mất bằng chứng lossless;
- xuất exact mismatch count theo field và request count;
- bất kỳ mismatch nào phải làm UP preflight FAIL;
- thêm fixture/test trong đó chỉ một trong 10 fields lệch để chứng minh gate bắt đúng;
- thêm fixture NULL-vs-empty-string để chứng minh null semantics đúng.

Không dùng tên `projection parity` cho một gate chỉ kiểm existence.

### A4. DOWN preflight — tất cả trước DDL

DOWN preflight phải read-only và chứng minh trước `ADD COLUMN` đầu tiên:

- database đúng allowlist;
- exact post-UP schema: 10 fields absent; targeted secondary indexes absent; visit-type CHECK absent; FULLTEXT exact post-UP shape; unrelated CHECKs còn nguyên;
- canonical V2 tables/links tồn tại đúng manifest;
- exactly one detail per instance, no orphan/duplicate;
- mỗi request có deterministic source detail;
- mandatory source fields non-null;
- enum/string values nằm trong domain mà legacy schema có thể chứa;
- không tồn tại schema object sẽ conflict với column/index/check được restore;
- exact master definitions dùng cho restore đã được assertion với authoritative master SQL.

Nếu bất kỳ gate fail:

- runner exit nonzero;
- `04_down_restore.sql` không được gọi;
- schema/data fingerprint phải không đổi.

Không chấp nhận thiết kế “ADD/UPDATE trước rồi fail và để cột NULLable” như một fail-closed result.

### A5. Guard trong payload

`02_guarded_up.sql` và `04_down_restore.sql` phải tự bảo vệ khi bị source trực tiếp:

- exact allowlist;
- explicit enable flag;
- expected lifecycle state;
- re-check critical schema/data prerequisites trước first ALTER;
- không coi `@PHASE1_PREFLIGHT_OK = 1` do caller tự set là proof duy nhất;
- mọi failure xảy ra trước first DDL;
- không chọn arbitrary constraint/index bằng `LIMIT 1` nếu chưa chứng minh unique exact match;
- không bịa placeholder data;
- không tuyên bố transaction rollback cho MySQL DDL.

### A6. Verify phải exact và fail-closed

`03_verify.sql`:

- chỉ chấp nhận exact `UP` hoặc `DOWN`; mode khác FAIL;
- kiểm exact manifest, không chỉ object existence;
- UP: 10 fields/dependencies đích absent, FULLTEXT exact new shape, unrelated schema intact, V2 data/fingerprint intact;
- DOWN: columns exact type/null/default/comment/ordinal/collation, indexes exact, FULLTEXT exact, CHECK exact expression, unrelated constraints intact;
- verify exactly-one detail/no orphan thay vì chỉ so tổng row count;
- kiểm data restoration bằng deterministic fingerprints/snapshots phù hợp, không chỉ tìm literal `N/A`;
- machine token đúng một lần và runner trả nonzero khi FAIL/unparseable.

### A7. Runner correctness/security

Giữ các phần tốt hiện có và bổ sung:

- `$PSScriptRoot` path resolution;
- exact `ValidateSet` + SQL allowlist defense-in-depth;
- password không nằm trong command line/log;
- quote/escape an toàn cho executable/user/host/port;
- capture đúng native exit code;
- chạy preflight trước UP và DOWN;
- không in PASS sớm;
- không chạy verify nếu payload chưa DONE;
- log command shape, DB, commit SHA, timestamp, exit code và fingerprints nhưng không log secret;
- cleanup temp file cả success/failure;
- nếu `mysql` không tồn tại, exit nonzero và báo command dự kiến; không giả lập drill bằng static review.

---

## 7. Workstream B — hoàn thành semantic R6 zero-unclassified audit

Làm hai lượt:

1. census/classification tại HEAD trước runtime fixes để lập execution map;
2. regenerate/finalize sau khi fixes hoàn tất để phản ánh HEAD cuối.

### B1. Scope

Scan toàn repository nơi có dependency, không chỉ `backend/**/*.cs` runtime:

- Application/Infrastructure/API/Domain;
- entity + EF mapping;
- API DTO/serialization;
- SQL/master/candidate/migrations;
- tests/fixtures/seeds;
- scripts;
- docs/comments nếu xuất hiện trong raw census.

10 fields/symbols:

`DelegationName`, `VisitType`, `VisitTypeOther`, `Purpose`, `WorkingContent`, `WorkingLanguage`, `TransportationNote`, `MediaConsentStatus`, `MediaConsentNote`, `NoteToFptu`, cùng snake_case DB identifiers tương ứng khi scan SQL.

### B2. Categories tối thiểu

- V1-only runtime read;
- V1-only runtime write;
- V2 dual-read/live V1 fallback;
- compatibility projection write;
- canonical V2 read/write;
- ORM/entity/schema mapping;
- migration/candidate SQL dependency;
- API/DTO serialization dependency;
- test/fixture/seed;
- docs/comment;
- unrelated same-name collision;
- dead code, chỉ khi có call-graph/build-exclusion evidence.

### B3. Evidence appendix bắt buộc

Có thể tách file chính và appendix CSV/MD để dễ đọc, nhưng **không xóa audit trail**.

Mỗi occurrence hoặc exact grouped symbol-site phải có:

- occurrence ID;
- field;
- exact `file:line`;
- stable symbol/member/expression;
- category;
- read/write/map/serialize operation;
- entity/table thực sự bị chạm;
- runtime caller/surface hoặc test/script owner;
- V1/V2 branch evidence;
- blocker `YES/NO`;
- required action;
- disposition/evidence/test.

Nếu gộp contiguous hits trong một stable symbol, appendix phải giữ danh sách exact line/hit count để tổng raw hits reconcile được. Không dùng `Various`, `~`, “khoảng”, hoặc chỉ nêu vài ví dụ.

Phải đặc biệt kiểm lại:

- `GetStaffLeaderDeptInvoiceItemsQuery`;
- `GetHoReportOverviewQueryHandler`;
- `VisitFormReadService`;
- `VisitRequestService` cả reads và writes;
- email action/info resolver;
- two background jobs;
- `VisitContactClaimService`;
- V2 create/edit/safe-edit projection writers;
- `VisitRequest` Domain entity và EF mappings;
- DTO/API contracts;
- all report/export/search/email paths;
- master/candidate SQL;
- tests/fixtures.

Chỉ được gọi `zero-unclassified` khi:

- raw total = tổng dispositions;
- distinct files reconcile;
- blocker table là projection của appendix, không phải danh sách viết tay riêng;
- exact counts theo field/category/read-write/blocker đều tự cộng khớp;
- known sites ở trên có disposition rõ ràng.

---

## 8. Workstream C — Phase II executable slice: bỏ V2 legacy fallback ở instance readers

Sau khi execution map đủ để không sửa mù, triển khai ngay các phần không cần business decision.

### C1. `GetStaffLeaderDeptInvoiceItemsQuery`

Hành vi bắt buộc:

- V1: giữ nguyên `VisitRequest.DelegationName` và output hiện hữu.
- mọi V2 (`form_schema_version >= 2`), bất kể uniform hay mixed: lấy `ci.FormDetail.DelegationName` của **chính target instance**.
- không condition source bằng `HasMixedCampusDetails`.
- missing detail không fallback global hoặc trả chuỗi rỗng như thể hợp lệ; dùng integrity/error contract đã được khóa trong project.
- giữ Staff Leader campus/department authorization và không tạo N+1.

Regression tests tối thiểu:

1. V2 uniform, global `DelegationName` cố tình stale/sai, detail đúng → output phải dùng detail;
2. V2 mixed → output target campus only;
3. V2 missing detail → stable failure, không legacy fallback;
4. V1 → output byte/semantic-identical;
5. wrong-campus/department access vẫn bị chặn.

### C2. `GetHoReportOverviewQueryHandler`

Audit tất cả `VisitRequest.VisitType` dependencies trong:

- request base query;
- instance base query;
- operational query;
- pending multi-campus query;
- preview/list/projection phần sau của file;
- bất kỳ export/report companion handler nào.

Hành vi:

- V1 filters tiếp tục dùng legacy `VisitRequest.VisitType`.
- mọi V2 instance filter dùng target `ci.FormDetail.VisitType`.
- V2 request-level filter dùng canonical per-campus details theo report semantics đã khóa, ví dụ request match khi một authorized/in-scope campus detail match; không dùng smallest projection.
- uniform V2 cũng dùng canonical detail, không rơi vào legacy branch.
- missing detail phải theo project integrity contract; không silently hide corrupt V2 row nếu canonical behavior yêu cầu 409/failure.
- giữ HO-only defense-in-depth, time filters, count semantics, campus scope và không N+1.

Tests tối thiểu:

1. uniform V2 có stale global visit type nhưng canonical detail match filter;
2. uniform V2 có global match nhưng canonical detail không match → không được tính;
3. mixed V2 match một campus đúng report/campus filter;
4. V1 behavior không đổi;
5. missing detail behavior;
6. count/trend/pipeline không double-count request ngoài semantics hiện hữu.

### C3. Các reader độc lập tiếp theo

Từ R6 execution map, tiếp tục theo thứ tự:

1. instance-scoped report/export readers;
2. background job xử lý từng campus instance;
3. instance-bound reminders/notifications;
4. helpers có target `visit_instance_id` rõ ràng.

Pattern:

```text
if V1:
    use legacy request fields unchanged
else:
    resolve authorized target instance detail
    missing detail => stable integrity failure
    never fallback global
```

Nếu một job/email thực sự request-level và mixed representation chưa khóa, đánh dấu đúng một blocker `NEEDS-BUSINESS-DECISION`, viết test/spec skeleton nếu hữu ích, rồi tiếp tục các site độc lập khác.

Không xóa hàng loạt V1 endpoint/fallback, projection writers, entity fields hoặc columns trước khi chứng minh:

- frontend/internal/external callers đã cut over;
- persisted V1 rows đã backfill;
- feature flag/canary/rollback;
- integration/real-stack coverage;
- request-level mixed contract.

---

## 9. Workstream D — sửa phantom search hit theo evidence

Audit `f4549b23` và code HEAD:

- SQL searchable common fields;
- `matchedContexts` field-code builder;
- FE label mapping;
- scope-before-keyword/pagination;
- PII/no-leak rules.

Invariant: một row được surface do field nào thì authorized consumer phải nhận stable, non-PII matched-context code cho field đó; không được có “phantom hit” với context rỗng.

Chỉ chọn một trong hai khi canonical product source chứng minh:

1. giữ field searchable và thêm complete field codes/FE labels/tests; hoặc
2. bỏ field khỏi keyword predicate để khớp contract hiện tại.

Nếu nguồn không khóa việc registrant nationality/job title có được search hay không, không tự chọn. Ghi blocker nhỏ này nhưng tiếp tục workstream khác.

Guest/support member search vẫn phải giữ theo quyết định đã restore: không thêm lại nếu không có positive locked requirement và security tests.

---

## 10. Workstream E — deterministic fresh target

Không tiếp tục blind regex rewrite.

Chọn một giải pháp deterministic có assertion, ví dụ:

- table-aware/statement-aware transformation; hoặc
- exact anchored blocks kèm source hash/count/shape assertions; hoặc
- maintained fresh schema artifact được diff bằng exact manifest với authoritative master.

Yêu cầu:

- fail nếu source master path/hash/expected table block/schema shape thay đổi;
- assert chỉ đúng 10 fields và exact dependencies đích bị loại;
- không xóa same-name token ở table/entity khác;
- assert không còn legacy identifiers/dependencies ngoài explicit dispositions;
- tạo `00_fresh_target.sql` reproducibly;
- import vào `pems_i_fresh` từ scratch;
- verify tables/indexes/checks/FKs/seeds/canonical V2 structures;
- record generator command, source hash, output hash, DB, time, exit code;
- test generator refusal khi source anchor bị drift.

Không đánh dấu fresh DONE nếu chỉ generate file mà chưa import và verify.

---

## 11. Test và disposable drills

### 11.1 Code tests

Chạy targeted tests sau mỗi slice, sau đó regression phù hợp:

- backend unit tests;
- architecture tests;
- targeted integration tests cho report/invoice/read-service;
- full integration khi disposable test DB có thể dựng an toàn;
- frontend Vitest;
- `npx tsc --noEmit`;
- `npm run build`;
- real-stack journeys liên quan nếu môi trường sẵn sàng.

Không tái sử dụng count cũ làm result của HEAD mới.

### 11.2 Refusal matrix — zero mutation

Trên disposable clones, ít nhất chứng minh:

- UP thiếu enable/override hợp lệ → nonzero, unchanged fingerprint;
- UP projection mismatch từng nhóm field → refused trước DDL;
- UP FULLTEXT/index/CHECK drift → refused trước DDL;
- UP non-v2/NULL schema version → refused;
- UP missing/duplicate/orphan detail → refused;
- DOWN gọi trên pre-UP/wrong lifecycle state → refused trước DDL;
- DOWN canonical mandatory field NULL/missing detail → refused trước DDL;
- DOWN index/FT/check conflict → refused trước DDL;
- invalid verify mode → FAIL/nonzero;
- direct-source payload chỉ set cờ nhưng prerequisite sai → refused trước DDL.

Chụp schema/data fingerprint trước và sau từng refusal; phải giống nhau.

### 11.3 Bốn lifecycle drills

Chỉ trên exact disposable allowlist:

1. refusal drill: failure paths + zero mutation;
2. upgrade drill: authoritative legacy/master target → UP preflight → UP → exact verify;
3. rollback drill: baseline/export/fingerprints → UP → verify → DOWN preflight → DOWN → exact schema/data diff → UP lại → verify;
4. fresh drill: deterministic fresh artifact → import `pems_i_fresh` → exact verify.

Không sửa `form_schema_version = 2` bằng một UPDATE đơn lẻ rồi dùng nó làm evidence backfill production-ready. Có thể dùng fixture cơ học riêng để drill DDL, nhưng phải ghi rõ giới hạn và không gọi đó là data-readiness proof.

Mỗi test/drill result phải ghi:

- exact command;
- HEAD SHA;
- timestamp/timezone;
- environment/tool versions;
- database name;
- pass/fail/skip counts;
- exit code;
- schema/data fingerprints trước/sau;
- giới hạn của bằng chứng;
- đường dẫn log artifact nếu có.

Không ghi password/token/secret vào report hoặc commit.

Nếu MySQL client/server không có, ghi `NOT RUN` với command dự kiến và blocker môi trường. Không thay bằng câu “tested structurally”.

---

## 12. R7 — trạng thái và tài liệu

Cập nhật các file theo evidence thật:

- `PHASE_I_AUDIT_REPORT.md`
- `IMPLEMENTATION_PROGRESS.md`
- `FINAL_IMPLEMENTATION_REPORT.md`
- candidate `README.md`
- audit appendix mới nếu có;
- drill/test report mới nếu có.

Không giữ historical paragraph mâu thuẫn với current status mà không gắn rõ “historical”. Kiểm toàn file, không chỉ thay một dòng đầu.

Không dùng:

- `✅ COMPLETE` khi R6/fresh/drill/blockers còn mở;
- `exact`, `lossless`, `zero-unclassified`, `all green`, `ready` nếu gate tương ứng chưa có evidence;
- approximate counts;
- test counts từ commit cũ như thể vừa chạy ở HEAD.

Status hợp lệ tùy kết quả thực:

```text
IN PROGRESS — migration safety gaps corrected to the evidenced gate; R6/Phase II/fresh status as listed; contract-drop NOT READY.
```

Chỉ đổi `NOT READY` khi toàn bộ runtime dependencies, persisted V1 data, caller cutover, exact schema/fresh proof và rollout gates thật sự hoàn tất.

---

## 13. Commit strategy

Không tạo mega-commit trộn SQL, audit, runtime và frontend không liên quan. Gợi ý semantic slices, điều chỉnh theo findings thật:

1. `fix(database): make Phase I down path fail closed`
2. `fix(database): verify exact projection and schema manifests`
3. `fix(reports): remove v2 legacy fallbacks from canonical readers`
4. `fix(audit): complete legacy-field occurrence dispositions`
5. `fix(database): generate and verify deterministic fresh target`
6. `docs(database): record exact Phase I drill evidence`

Mỗi commit chỉ tạo sau targeted gates của slice. Không commit raw DB dump, secret, temp auth profile, huge logs hoặc user prompt/handoff được yêu cầu untracked.

---

## 14. Business-decision boundary

Không hỏi lại điều code/tests/canonical docs đã khóa.

Đã khóa, được triển khai ngay:

- V2 instance-scoped → target instance detail;
- V2 business/search/report không dùng compatibility projection;
- request-level flat mixed → stable 409;
- V1 giữ hành vi trong giai đoạn compatibility;
- scope trước search/projection.

Có thể vẫn cần người dùng chốt:

- request-level email/claim/background notification phải hiển thị tên/nội dung gì khi mixed và không có target instance duy nhất;
- registrant nationality/job title có thuộc public/product search contract hay không.

Khi gặp đúng blocker này:

1. ghi surface/caller cụ thể;
2. đưa 2–3 lựa chọn với security/data implications;
3. không tự chọn;
4. tiếp tục các workstream độc lập.

Không được dùng một blocker nghiệp vụ nhỏ để kết thúc phiên sớm.

---

## 15. Deliverables bắt buộc

Cuối phiên phải giao:

1. baseline/checkpoint, HEAD đầu phiên, HEAD cuối phiên, merge-base và divergence;
2. commit-by-commit audit cho mọi commit sau `d64bde66` có trước lúc bắt đầu;
3. findings ledger đầy đủ, gồm F1–F10 và finding mới;
4. danh sách files/symbols/SQL đã sửa và lý do;
5. UP/DOWN safety matrix: prerequisite, nơi enforce, refusal evidence, mutation status;
6. exact projection-parity result theo 10 fields;
7. R6 raw census + exact reconciliation + blocker table + appendix path;
8. Phase II readers đã migrate và behavioral tests V1/uniform-V2/mixed-V2/missing-detail/auth;
9. fresh generator/import/verify evidence hoặc honest blocker;
10. exact commands/test results/drills ở HEAD cuối;
11. database touch ledger xác nhận database nào được tạo/mutate/drop và database thật nào không chạm;
12. remaining blockers chia `technical`, `environment`, `business decision`;
13. next executable slice;
14. `git status --short --branch` cuối phiên;
15. danh sách commits local đã tạo; nói rõ có push/merge/deploy hay không.

Không dùng câu “hoàn tất toàn bộ” nếu còn appendix incomplete, fresh NOT RUN, tests unavailable, runtime blockers, V1 persisted data hoặc business decision chưa chốt.

---

## 16. Definition of Done cho phiên này

Chỉ được gọi corrective/hardening slice của prompt này hoàn tất khi:

- DOWN có read-only preflight và mọi refusal được chứng minh zero mutation;
- UP projection parity so sánh exact đủ 10 fields;
- UP/DOWN preflight + verify dùng exact schema manifests, không chỉ object names/LIKE;
- invalid verify mode fail;
- direct payload không thể vượt prerequisite chỉ bằng cách set flag;
- R6 không còn unclassified và totals reconcile;
- known omitted reader sites có disposition và các instance-scoped V2 fallbacks đã được sửa/test;
- fresh generator không còn blind regex và fresh import/verify đã chạy, hoặc status vẫn ghi rõ chưa hoàn tất;
- tất cả available gates ở HEAD xanh, unavailable gates ghi NOT RUN đúng lý do;
- không database thật nào bị mutate;
- báo cáo không overclaim;
- Phase II đã tiến tới gate xa nhất có thể mà không tự đoán business rule.

Nếu vẫn còn runtime V1 create/projection writers hoặc production backfill chưa thực hiện, kết luận đúng phải là:

> `IN PROGRESS — corrective migration safety and independent V2-reader retirement completed to the evidenced gate; contract-drop NOT READY; remaining runtime/data/cutover blockers enumerated exactly.`

Đó là trạng thái hợp lệ. Điều không hợp lệ là tuyên bố READY/COMPLETE khi bằng chứng chưa đủ.
