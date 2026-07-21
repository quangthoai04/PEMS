# PEMS — Prompt triển khai phần còn lại sau local HEAD `5296ad4a`

> Đây là prompt thực thi tự chứa cho AI coding agent/developer tiếp theo. Phải trực tiếp kiểm tra local repository, bảo toàn ba commit chưa push, viết code/tests/scripts, chạy các gate khả dụng và tạo commit theo semantic slice. Không chỉ lập kế hoạch, không đoán business rule, không dùng build xanh thay cho behavioral proof và không tuyên bố hoàn tất khi Definition of Done chưa đạt.

## 1. Vai trò, repository và checkpoint

Bạn là **Senior Software Architect + Senior Full-stack Engineer + Database Migration Engineer + Test/Security Engineer** tiếp tục chương trình **PEMS Per-Campus Form V2**.

Repository/branch:

- GitHub repository: `quangthoai04/PEMS`
- remote branch: `origin/Cảnh-Iter1`
- local branch thường là `Canh-Iter1`
- remote checkpoint đã review: `d64bde66973caaf9765070e529cb1599743ec03d`
- expected local HEAD theo bàn giao mới nhất: `5296ad4a`
- expected local commits chưa push:
  - `494bbdf5`
  - `4b6735b1`
  - `5296ad4a`
- expected state: local ahead remote 3 commits; chỉ `docs/ChangeSauHopChiQUyen/sauhop_13-07/gemin/` untracked và phải giữ nguyên.

Ba commit local chưa có trên GitHub tại thời điểm bàn giao. Vì vậy local repository là nguồn cần kiểm tra đầu tiên. Không reset về remote, không xóa, squash, amend hoặc rewrite ba commit này.

Mục tiêu phiên:

1. kiểm chứng và hoàn thiện các thay đổi local thay vì tin báo cáo;
2. thêm toàn bộ behavioral regression tests cho hai V2 reader vừa sửa;
3. chạy negative migration/refusal matrix để chứng minh F3/F4 thật sự fail-closed;
4. hoàn thiện F5 exact schema/dependency manifest;
5. hoàn thành R6 occurrence appendix và exact reconciliation;
6. thay fresh-target blind regex bằng deterministic generation có assertions, import và drill;
7. xử lý F8 evidence completeness, F9 cosmetic contract và cô lập F10 business decision;
8. giữ database thật nguyên vẹn và báo cáo trung thực.

Không push, merge, deploy hoặc bật production flags nếu người dùng chưa yêu cầu rõ.

---

## 2. Checkpoint bắt buộc trước khi sửa

Chạy và lưu output:

```bash
git status --short --branch
git remote -v
git rev-parse HEAD
git rev-parse origin/Cảnh-Iter1
git merge-base d64bde66973caaf9765070e529cb1599743ec03d HEAD
git log --oneline --decorate --graph -20
git cat-file -t 494bbdf5
git cat-file -t 4b6735b1
git cat-file -t 5296ad4a
git diff --stat d64bde66973caaf9765070e529cb1599743ec03d..HEAD
git diff --check d64bde66973caaf9765070e529cb1599743ec03d..HEAD
```

Sau đó audit từng local commit:

```bash
git show --stat --oneline 494bbdf5
git show --stat --oneline 4b6735b1
git show --stat --oneline 5296ad4a
git show --format=fuller --find-renames 494bbdf5
git show --format=fuller --find-renames 4b6735b1
git show --format=fuller --find-renames 5296ad4a
```

Quy tắc:

- Nếu expected HEAD/commits tồn tại, tiếp tục trên đó.
- Nếu HEAD đã đi tiếp, audit mọi commit mới trước khi sửa; không reset.
- Nếu một expected commit không tồn tại, không giả định nội dung. Dùng code/diff thật tại HEAD.
- Không commit thư mục `gemin/` hoặc các prompt/handoff protected/untracked.
- Không chạm thay đổi người dùng ngoài scope.
- Nếu working tree có thay đổi overlap, phân tích trước; không checkout/reset để xóa.
- Lập findings ledger ngay, gồm `F1` đến `F10`, finding mới và trạng thái:
  `OPEN | IMPLEMENTED-UNVERIFIED | VERIFIED | PARTIAL | BLOCKED | NEEDS-BUSINESS-DECISION | NOT-A-BUG`.

Không được dùng `FIXED` trước khi regression test hoặc drill phù hợp bắt đúng lỗi và pass tại HEAD.

---

## 3. Nguồn sự thật và invariant

Ưu tiên:

1. source code + tests + authoritative master SQL tại HEAD;
2. `PEMS_PER_CAMPUS_V2_MASTER_HANDOFF_PROMPT(1).md`;
3. canonical business rules, permission rules, use cases và clean architecture;
4. migration scripts;
5. progress/audit reports — đây là artifact cần sửa, không phải ground truth.

Invariant không được hiểu lại:

- V1 giữ behavior hiện hành trong compatibility period.
- Với `form_schema_version >= 2`, canonical source của 10 fields là `visit_instance_form_details`.
- V2 instance-scoped consumer dùng target instance detail cho cả uniform và mixed.
- V2 missing detail không fallback global projection.
- V2 read/business/search/report không dùng smallest-campus compatibility projection.
- Request-level flat mixed V2 dùng stable contract hiện hành, không tự tạo display rule mới.
- Scope/authorization trước search/filter/projection/enrichment.
- Không leak hidden sibling campus hoặc PII snippets.
- MySQL DDL auto-commit; mọi refusal phải xảy ra trước first DDL.
- Không fabricate dữ liệu để ép NOT NULL.

Exact destructive allowlist cho Phase I scripts:

- `pems_i_fresh`
- `pems_i_upgrade`
- `pems_i_refusal`
- `pems_i_rollback`

Tuyệt đối không mutate/drop:

- `pems_db`
- `pems_test`
- `pems_pr3_test`

Integration tests chỉ được dùng database disposable riêng nếu harness cho phép, ví dụ `pems_it_regression`; không chạy Phase I migration scripts trên DB này và phải drop nó sau test. Nếu harness hardcode `pems_test` và không có override an toàn, ghi `NOT RUN` thay vì chạm `pems_test`.

---

## 4. Chỉnh lại cách hiểu findings trước khi code

Bàn giao trước phải được hiệu chỉnh như sau:

| Finding | Trạng thái đầu phiên đúng |
|---|---|
| F1 R6 occurrence appendix | `OPEN` |
| F2 two canonical readers | `IMPLEMENTED-UNVERIFIED`, vì chưa có behavioral regression tests |
| F3 exact 10-field parity | `IMPLEMENTED-PARTIALLY-VERIFIED`, mới có happy-path fixture; negative matrix chưa đủ |
| F4 DOWN preflight | `IMPLEMENTED-PARTIALLY-VERIFIED`, đã có wrong-state refusal + lifecycle drill nhưng chưa đủ failure matrix |
| F5 exact manifest/dependency depth | `PARTIAL` |
| F6 independent Phase II slice | `IMPLEMENTED-UNVERIFIED` |
| F7 deterministic fresh target | `OPEN` |
| F8 exact test/drill evidence | `OPEN`; finding này bị bỏ khỏi ledger trước |
| F9 image-only upload text | `OPEN` |
| F10 phantom search contract | `NEEDS-BUSINESS-DECISION`, trừ khi canonical source khóa được lựa chọn |

Không được viết “every prerequisite is enforced” khi F5 còn partial.

Không được viết “remaining blockers enumerated exactly” khi F1 chưa zero-unclassified.

---

## 5. Workstream A — behavioral tests cho C1/C2 trước khi gọi F2 verified

Mở diff local của:

- `backend/PEMS.Application/Reports/Queries/GetStaffLeaderReportV2/GetStaffLeaderDeptInvoiceItemsQuery.cs`
- `backend/PEMS.Application/Reports/Queries/GetHoReportOverview/GetHoReportOverviewQueryHandler.cs`

Xác nhận code local thật sự dùng canonical detail cho **mọi V2**, không chỉ mixed, và V1 branch không đổi.

### A1. Test `GetStaffLeaderDeptInvoiceItemsQuery`

Viết behavioral tests bắt buộc:

1. **Uniform V2, stale projection**
   - `FormSchemaVersion >= PerCampus`;
   - `HasMixedCampusDetails = false`;
   - `VisitRequest.DelegationName = "STALE_GLOBAL"`;
   - target `FormDetail.DelegationName = "CANONICAL_TARGET"`;
   - output phải là `CANONICAL_TARGET`.
2. **Mixed V2, target-only**
   - hai campus có delegation khác nhau;
   - Staff Leader campus A chỉ nhận name của instance A;
   - không sibling leak.
3. **Missing V2 detail**
   - không được fallback `VisitRequest.DelegationName`;
   - phải theo stable integrity/error behavior đã dùng trong project.
4. **V1 parity**
   - V1 tiếp tục dùng global name;
   - output/status/order/filter giữ nguyên.
5. **Authorization**
   - wrong campus/department bị từ chối như trước;
   - không mở rộng scope do canonical join/enrichment.
6. **Provider translation**
   - query phải chạy với provider thực mà production dùng, không chỉ EF InMemory.

### A2. Test `GetHoReportOverviewQueryHandler`

Audit và test tất cả nơi local commit đã sửa, tối thiểu bốn visit-type filters và mọi delegation projection liên quan.

Test matrix:

1. uniform V2: global visit type stale, canonical detail match filter → request/instance phải được tính;
2. uniform V2: global match filter nhưng canonical detail không match → không được tính;
3. mixed V2: một campus match; kết quả phải theo exact request/instance/campus semantics hiện hữu;
4. campus filter không được dùng hidden sibling để surface unauthorized data;
5. V1 behavior/counts không đổi;
6. missing V2 detail không được silently fallback global;
7. monthly trend, pipeline, pending multi-campus và preview không double-count;
8. HO authorization defense-in-depth vẫn 403 cho non-HO;
9. query chạy trên Pomelo/MySQL production provider.

### A3. Pomelo scalar-subquery hazard

Không chấp nhận câu “Pomelo hazard” như lý do để bỏ test hoặc dừng implementation.

Quy trình:

1. viết reproduction test trên provider thật;
2. chụp exact generated SQL/exception nếu thực sự fail;
3. tìm pattern đã dùng thành công trong repository;
4. sửa bằng DB-side query hoặc two-phase **batched** enrichment;
5. scope/filter/page/count phải hoàn tất đúng thứ tự trước enrichment;
6. không N+1, không client-side load toàn bảng;
7. request-level mixed content không được chọn smallest projection;
8. thêm query-count hoặc evidence chứng minh bounded queries.

Nếu behavior request-level display thật sự chưa khóa, chỉ cô lập đúng projection/display đó. Các filters và instance reads có canonical semantics vẫn phải hoàn tất.

### A4. Gate của Workstream A

Không đổi F2/F6 thành `VERIFIED` cho tới khi:

- tests mới fail trên code trước `494bbdf5` hoặc fixture chứng minh đúng regression;
- tests pass tại HEAD cuối;
- provider translation pass;
- V1, uniform V2, mixed V2, missing detail và auth đều có evidence.

---

## 6. Workstream B — negative migration/refusal matrix cho F3/F4

Mở và audit local diff của:

- `01_preflight.sql`
- `02_guarded_up.sql`
- `03_verify.sql`
- `04_down_restore.sql`
- `run_migration.ps1`

Không chỉ đọc happy path. Dựng reusable disposable drill harness có cleanup `finally`, redacted logs và explicit fingerprint checks.

### B1. Exact parity negative tests

Trên disposable fixture, parameterize đủ 10 fields:

- `delegation_name`
- `visit_type`
- `visit_type_other`
- `purpose`
- `working_content`
- `working_language`
- `transportation_note`
- `media_consent_status`
- `media_consent_note`
- `note_to_fptu`

Với từng field:

1. bắt đầu từ fixture parity PASS;
2. mutate đúng một global projection value để lệch selected canonical detail;
3. chạy UP preflight;
4. expected FAIL với exact mismatch evidence;
5. runner không gọi payload;
6. mysql/runner exit nonzero;
7. schema/data fingerprint trước/sau giống nhau;
8. restore fixture rồi sang field kế tiếp.

Thêm cases:

- NULL vs empty string;
- NULL vs whitespace;
- two mixed campuses, selected source xác định bằng `campus_id ASC` và stable tie-break;
- selected detail missing;
- request không có projectable campus;
- duplicate source candidate nếu schema drift cho phép;
- `form_schema_version IS NULL`.

Không dùng `COALESCE`, trim hoặc normalization để khiến values khác nhau thành equal. Lossless proof cần stored-value equality với `<=>`.

### B2. UP refusal matrix

Chứng minh zero mutation cho:

- missing/invalid enable flags;
- runtime blocker override thiếu trên disposable drill;
- wrong lifecycle state;
- non-v2/NULL schema version;
- missing/duplicate/orphan detail;
- legacy column definition drift;
- column comment/default/ordinal drift;
- secondary index member/order/type drift;
- FULLTEXT member/order drift;
- visit-type CHECK expression drift hoặc multiple matches;
- unexpected other CHECK/generated/view/trigger/FK dependency;
- direct-source payload với flags tự set nhưng prerequisite sai.

### B3. DOWN refusal matrix

Chứng minh mọi case sau fail **trước first ALTER** và fingerprint không đổi:

- DOWN trên pre-UP schema;
- DOWN thiếu flag;
- post-UP FULLTEXT/index/CHECK state drift;
- canonical detail missing/orphan/duplicate;
- mandatory source field NULL;
- enum value không thể restore vào legacy definition;
- object name conflict với column/index/check sắp restore;
- invalid/missing preflight mode;
- direct-source DOWN chỉ set enable flag nhưng prerequisite sai.

### B4. Verify refusal

- mode không phải exact `UP|DOWN` phải FAIL/nonzero;
- missing/duplicate verdict token phải làm runner fail;
- verify không được pass khi exact manifest drift;
- verify data integrity không được chỉ dựa vào `detail_rows == instance_rows`.

### B5. Evidence

Mỗi case ghi:

- command;
- DB;
- HEAD;
- timestamp/timezone;
- expected/actual verdict;
- mysql exit;
- runner exit;
- pre/post fingerprint;
- payload invoked `YES/NO`;
- cleanup result.

Không commit password, dump thật hoặc log quá lớn. Có thể commit một Markdown/CSV evidence matrix và giữ raw disposable logs ngoài Git nếu chúng lớn/nhạy cảm.

Chỉ đổi F3/F4 thành `VERIFIED` khi negative matrix tương ứng xanh.

---

## 7. Workstream C — hoàn thiện F5 exact schema/dependency manifest

Current F5 là partial. Bổ sung exact assertions cho cả preflight và verify.

### C1. Columns

Đối với 10 fields, kiểm exact:

- name;
- ordinal position;
- data type và full column type;
- length/enum members;
- nullability;
- default;
- comment;
- charset/collation;
- generated/expression/extra attributes khi áp dụng.

Expected manifest phải xuất phát từ authoritative master SQL/current known schema, không đoán từ memory.

### C2. Indexes/FULLTEXT

Kiểm từng index bằng exact ordered manifest:

- index name;
- `NON_UNIQUE`;
- `INDEX_TYPE`;
- visibility nếu MySQL version hỗ trợ;
- ordered column/expression list theo `SEQ_IN_INDEX`;
- collation/prefix length khi áp dụng.

FULLTEXT trước UP và sau UP/DOWN phải đối chiếu exact member list/order, không chỉ kiểm `delegation_name` present/absent.

### C3. CHECK expression

- không dùng chỉ `LIKE '%visit_type%'` làm exact proof;
- resolve đúng constraint bằng deterministic normalized expression fingerprint;
- không hardcode auto-generated CHECK name nếu master khai báo unnamed;
- assert exactly one semantic expression match;
- verify sáu unrelated CHECKs bằng exact expression set/fingerprints, không chỉ count.

Normalization phải deterministic và có test source variants; không normalize mạnh đến mức hai expression khác nghĩa cho cùng fingerprint.

### C4. External dependencies

Preflight phải kiểm các object có thể làm DROP fail hoặc bị invalid:

- generated columns;
- indexes/checks/FKs;
- views;
- triggers;
- stored routines/events nếu schema dùng;
- ORM/raw SQL dependencies được disposition trong R6.

Không cần tuyên bố MySQL metadata chứng minh điều nó không thể chứng minh. Nếu routine dependency phải scan definition text, ghi limitation rõ và reconcile với repository audit.

### C5. Payload defense-in-depth

Vì runner preflight và payload có thể chạy ở hai mysql processes:

- payload re-check critical lifecycle/schema prerequisites trước first DDL;
- caller-set `@PHASE1_PREFLIGHT_OK = 1` không phải proof duy nhất;
- any mismatch abort trước ALTER;
- add refusal drill chứng minh flags không bypass được drift.

### C6. Gate F5

F5 chỉ `VERIFIED` khi:

- exact manifest checks implemented;
- drift cases fail zero mutation;
- UP/DOWN verify catches exact drift;
- normal lifecycle vẫn pass;
- README/audit không còn claim rộng hơn checks thật.

---

## 8. Workstream D — hoàn thành R6 zero-unclassified appendix

Không được trì hoãn R6 thêm một phiên chỉ vì 1172 hits lớn. Dùng reproducible tooling để quản lý census, sau đó manual semantic classification theo stable symbol/site.

### D1. Recompute tại HEAD cuối

Scan cả PascalCase C# symbols và snake_case SQL identifiers:

- `DelegationName` / `delegation_name`
- `VisitType` / `visit_type`
- `VisitTypeOther` / `visit_type_other`
- `Purpose` / `purpose`
- `WorkingContent` / `working_content`
- `WorkingLanguage` / `working_language`
- `TransportationNote` / `transportation_note`
- `MediaConsentStatus` / `media_consent_status`
- `MediaConsentNote` / `media_consent_note`
- `NoteToFptu` / `note_to_fptu`

Scope:

- backend production;
- Domain/entity/EF mapping;
- API DTO/serialization;
- SQL/master/candidate/migrations;
- scripts;
- tests/fixtures/seeds;
- frontend API/types/usages nếu contract liên quan;
- docs/comments/unrelated collisions để raw total reconcile.

Không hardcode `1172/137`; code/tests mới có thể làm counts đổi.

### D2. Reproducible appendix

Tạo artifact, ví dụ:

- `PHASE_I_AUDIT_APPENDIX.csv`; hoặc
- split CSV/MD theo category nếu một file quá lớn;
- kèm command/script tái tạo raw census.

Mỗi raw hit hoặc exact grouped stable-symbol site phải có:

- occurrence/group ID;
- field;
- file path;
- exact line number(s);
- raw-hit count;
- stable symbol/expression;
- entity/table bị chạm;
- category;
- operation read/write/map/serialize/schema/test/doc;
- runtime caller/surface;
- V1/V2 behavior;
- blocker YES/NO;
- required action;
- disposition;
- test/evidence reference.

Nếu gộp contiguous hits, phải giữ exact line list và raw count. Tổng grouped raw counts phải bằng census.

### D3. Categories

Tối thiểu:

- V1-only runtime read;
- V1-only runtime write;
- V2 dual-read/live V1 fallback;
- compatibility projection write;
- canonical V2 read/write;
- ORM/entity/schema mapping;
- API/DTO serialization dependency;
- migration/master/candidate SQL;
- test/fixture/seed;
- frontend contract;
- docs/comment;
- unrelated same-name collision;
- dead/excluded code với build/call evidence.

### D4. Known sites phải disposition

- `VisitFormReadService`;
- `VisitRequestService` reads và writes;
- `GetStaffLeaderDeptInvoiceItemsQuery`;
- `GetHoReportOverviewQueryHandler`;
- `ExportDeptLeaderInvoice`;
- email action/info resolver;
- two background jobs;
- `VisitContactClaimService`;
- V2 create/edit/safe-edit projection writers;
- `VisitRequest` entity + EF mapping;
- DTO/API properties;
- report/search/export/email surfaces;
- all master/candidate SQL and tests.

### D5. Reconciliation

Report phải tự động hoặc có reproducible checks chứng minh:

- raw hits = sum appendix raw-hit counts;
- distinct files khớp;
- count theo field/category/blocker khớp;
- blocker summary được sinh/đối chiếu từ appendix;
- zero `Unclassified`, `Various`, blank disposition hoặc approximate count.

Chỉ sau đó đổi F1 thành `VERIFIED` và dùng từ `zero-unclassified`.

---

## 9. Workstream E — deterministic fresh target và fresh drill

### E1. Bỏ blind regex

Audit `generate_fresh_target.ps1` và `00_fresh_target.sql`. Không vá regex thêm một lớp rồi gọi deterministic.

Chọn giải pháp có structure + assertions:

- SQL statement/table-aware parser; hoặc
- exact anchored table/insert blocks kèm source SHA-256, expected match count và structural assertions; hoặc
- maintained fresh artifact được exact-manifest diff với authoritative source.

Yêu cầu generator:

- source master path được resolve ổn định;
- source hash/expected structural fingerprint được kiểm;
- exactly one intended `visit_requests` CREATE TABLE được transform;
- chỉ đúng 10 legacy columns và exact dependencies bị loại;
- same-name identifiers ở tables/enums/OTP/files không bị xóa nhầm;
- inserts/seeds được xử lý statement-aware;
- fail nếu seed không thể chuyển thành valid canonical V2 data mà không fabricate;
- output reproducible: cùng input → cùng output hash;
- source drift/anchor drift → nonzero, không ghi artifact half-generated;
- temp output được atomic replace sau khi validation pass.

### E2. Fresh data semantics

Authoritative master hiện có thể chứa V1 rows/seeds. Không được chỉ đổi `form_schema_version = 2` hoặc bỏ columns trong INSERT rồi gọi đó là valid fresh V2 seed.

Phải chứng minh một trong các contract bằng nguồn thật:

1. fresh target là schema-only rồi seed canonical V2 riêng; hoặc
2. master seeds được deterministic/lossless backfill thành request + per-campus details; hoặc
3. seed subset legacy bị loại có chủ đích và tests không cần nó.

Nếu source không khóa lựa chọn, dừng đúng data-seed subproblem, ghi blocker và vẫn hoàn thiện deterministic schema generation/tests có thể làm an toàn. Không fabricate.

### E3. Fresh generator tests

- golden input → expected output manifest;
- repeated generation identical hash;
- source hash/shape drift refused;
- zero/multiple target table refused;
- same-name tokens ngoài target không bị đổi;
- invalid seed transform refused;
- no partial output on failure.

### E4. Fresh drill

Chỉ trên `pems_i_fresh`:

1. confirm DB exact allowlist;
2. create empty target;
3. import generated artifact;
4. run exact fresh/UP-state verify;
5. validate canonical V2 tables, FKs, checks, indexes, seeds/data contract;
6. record source/output hashes, commands, versions, exit codes, fingerprints;
7. drop `pems_i_fresh` trong cleanup;
8. prove `pems_db`, `pems_test`, `pems_pr3_test` untouched.

Chỉ sau import + verify mới đổi F7 thành `VERIFIED`.

---

## 10. Workstream F — F8 evidence, F9 text và F10 decision boundary

### F1. Evidence completeness — finding F8

Thêm lại F8 vào ledger. Với mọi test/drill, ghi:

- exact command;
- HEAD SHA;
- timestamp/timezone;
- tool/runtime/MySQL version;
- DB name;
- pass/fail/skip counts;
- exit code;
- pre/post fingerprints;
- cleanup result;
- limitation.

Không gọi local test output là GitHub CI. Nếu không có workflow run, ghi đúng `local evidence only`.

Không dùng Vercel permission/team-invite failure như code-test failure hoặc success.

### F2. F9 image-only upload text

Trong `VisitPhotoPanel.tsx`:

- upload accept/validation/help text/toast phải thống nhất image-only 5 MB;
- sửa success toast `ảnh/video` thành `ảnh` nếu upload contract thật sự image-only;
- có thể giữ video preview/rendering để tương thích historical files;
- không mở lại video upload;
- thêm/sửa targeted frontend test nếu component/util đã có test harness;
- chạy Vitest/tsc/build.

### F3. F10 phantom search contract

Audit source đã khóa trước khi hỏi:

- keyword SQL predicates;
- matched-context builder;
- FE field-code labels;
- permission/no-PII rules;
- Slice 5B security tests.

Invariant: field làm row match phải có authorized stable matched-context code; không phantom hit.

Nếu canonical product source không nói rõ có search `RegistrantFullName/Nationality/JobTitle` hay không, không tự chọn. Ghi một quyết định ngắn cho người dùng:

- Option A: giữ searchable, thêm field codes/labels/security tests;
- Option B: bỏ khỏi keyword predicate để khớp context contract hiện tại.

Nêu privacy/no-leak impact của mỗi option. Tiếp tục mọi phần độc lập trong khi chờ.

Guest/support member search không được thêm lại nếu không có locked positive requirement.

---

## 11. Full regression gates

Sau targeted tests của từng workstream, chạy gate rộng phù hợp.

### Backend

- `PEMS.UnitTests`;
- `PEMS.ArchitectureTests`;
- targeted report/invoice integration tests;
- migration/drill harness tests;
- full integration trên disposable override nếu harness hỗ trợ an toàn.

Nếu full IT cần `pems_test` hardcoded:

1. tìm config/env override đã dùng trong real-stack/integration harness;
2. ưu tiên disposable `pems_it_regression`;
3. xác nhận connection string runtime trước test;
4. drop disposable sau test;
5. nếu không thể override, ghi `NOT RUN`; tuyệt đối không create/mutate `pems_test`.

### Frontend

Khi F9 hoặc search code thay đổi:

```bash
npm test -- --run
npx tsc --noEmit
npm run build
```

### Real-stack

Chạy journeys liên quan nếu harness và disposable DB sẵn sàng. Không dùng network mocks để gọi đó là real-stack. Không giữ secret/profile/temp DB sau test.

Mọi count phải là result tại HEAD cuối, không copy từ `530/530`, `14/14`, `99/99` cũ nếu chưa chạy lại.

---

## 12. Commit strategy

Không sửa commit cũ. Tạo commit mới theo semantic slice sau khi tests tương ứng xanh. Gợi ý:

1. `test(reports): cover canonical v2 invoice and HO report reads`
2. `test(database): prove Phase I refusal paths are zero-mutation`
3. `fix(database): enforce exact Phase I schema manifests`
4. `fix(audit): complete legacy-field occurrence dispositions`
5. `fix(database): generate deterministic Phase I fresh target`
6. `fix(delegations): align visit-photo upload text with image policy`
7. `docs(database): record exact lifecycle and fresh-drill evidence`

Điều chỉnh theo diff thật, nhưng không tạo mega-commit trộn runtime/tests/SQL/docs không liên quan.

Không commit:

- `gemin/` prompt folder;
- secret/password/token;
- real DB dump;
- test auth profiles;
- temp/log lớn;
- AI attribution.

Không push/merge/deploy nếu người dùng chưa yêu cầu.

---

## 13. Cập nhật báo cáo không overclaim

Kiểm và cập nhật toàn bộ, không chỉ dòng status đầu:

- `PHASE_I_AUDIT_REPORT.md`
- `PHASE_I_AUDIT_APPENDIX.*`
- `IMPLEMENTATION_PROGRESS.md`
- `FINAL_IMPLEMENTATION_REPORT.md`
- candidate `README.md`
- test/drill evidence artifact.

Quy tắc trạng thái:

- F2 chỉ `VERIFIED` sau behavioral matrix;
- F3/F4 chỉ `VERIFIED` sau negative/refusal matrix;
- F5 chỉ `VERIFIED` sau exact drift tests;
- F1 chỉ `VERIFIED` sau zero-unclassified reconciliation;
- F7 chỉ `VERIFIED` sau fresh import + verify;
- F8 phải luôn có trong ledger;
- F10 giữ `NEEDS-BUSINESS-DECISION` nếu chưa có locked rule.

Không dùng:

- “every prerequisite enforced” khi manifest/dependency còn gap;
- “enumerated exactly” khi appendix incomplete;
- “all green” khi test unavailable;
- “lossless” nếu chỉ happy-path fixture;
- “CI passed” cho local commands;
- `✅ COMPLETE` cạnh contract-drop khi V1 runtime/data/cutover blockers còn sống.

Status hợp lệ nếu chưa đạt toàn bộ:

> `IN PROGRESS — behavioral reader tests and migration refusal evidence completed to the stated gate; exact audit/fresh/runtime/data/cutover blockers remain; contract-drop NOT READY.`

---

## 14. Deliverables bắt buộc

Cuối phiên giao:

1. remote checkpoint, local HEAD đầu/cuối, ahead/behind/divergence;
2. audit ba local commits `494bbdf5`, `4b6735b1`, `5296ad4a` và verdict từng commit;
3. findings ledger đủ F1–F10, không thiếu F8;
4. files/symbols/tests/scripts đã sửa;
5. C1/C2 behavioral matrix: V1, uniform V2 stale projection, mixed, missing detail, auth, provider translation;
6. parity negative matrix đủ 10 fields + NULL/empty cases;
7. UP/DOWN refusal matrix với zero-mutation fingerprints;
8. F5 exact schema/dependency manifest coverage và known limitations;
9. R6 census/reconciliation, appendix path, zero-unclassified count;
10. deterministic generator design, source/output hashes và generator tests;
11. fresh import/verify result hoặc honest blocker;
12. exact commands/results at final HEAD;
13. DB touch ledger: created/mutated/dropped/remaining; xác nhận ba protected DB không chạm;
14. cleanup proof;
15. business decisions còn lại và options;
16. next executable slice;
17. `git status --short --branch` cuối phiên;
18. commits mới, author/committer và push/merge/deploy status.

Không dùng câu “Done” đơn độc. Mỗi completion claim phải liên kết với test/drill/evidence.

---

## 15. Definition of Done của prompt này

Prompt này chỉ hoàn tất khi:

- ba local commits được audit và giữ nguyên history;
- C1/C2 behavioral regressions tồn tại và xanh trên provider phù hợp;
- F2/F6 không còn unverified;
- parity negative tests đủ 10 fields và null semantics xanh;
- UP/DOWN refusal cases fail trước DDL và chứng minh zero mutation;
- F3/F4 verified bằng negative evidence;
- F5 exact columns/indexes/FULLTEXT/CHECK/dependencies được implement + drift-tested;
- invalid verify/preflight modes fail;
- R6 appendix hoàn chỉnh, totals reconcile và zero unclassified;
- fresh generator không còn blind regex, generator tests xanh;
- `pems_i_fresh` import + exact verify pass, hoặc status trung thực nếu một seed business decision thật sự chặn;
- F8 evidence ledger đầy đủ;
- F9 text contract được sửa/test;
- F10 không bị tự chọn khi chưa có canonical rule;
- tất cả available regression gates ở HEAD cuối xanh;
- `pems_db`, `pems_test`, `pems_pr3_test` không bị mutate;
- disposable DB/temp artifacts được cleanup;
- reports không overclaim;
- không push/merge/deploy ngoài thẩm quyền.

Ngay cả khi prompt này hoàn tất, contract-drop vẫn có thể chưa READY vì V1 create/runtime dependencies, persisted V1 backfill, caller cutover và production rollout chưa hoàn thành. Khi đó kết luận đúng là:

> `IN PROGRESS — remaining corrective tests, exact audit and deterministic fresh-target work completed to the evidenced gate; contract-drop NOT READY; runtime/data/cutover blockers listed from the completed appendix.`

Không được biến việc hoàn thành prompt này thành tuyên bố toàn bộ PEMS Phase I/Phase II đã hoàn tất.
