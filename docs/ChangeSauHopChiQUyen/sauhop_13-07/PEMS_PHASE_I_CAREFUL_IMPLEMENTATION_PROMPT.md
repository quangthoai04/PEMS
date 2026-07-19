# PROMPT THỰC THI TIẾP — RECONCILE DEV MERGE + PHASE I GUARDED CONTRACT-DROP PREPARATION

Bạn đang tiếp tục chương trình **PEMS Per-Campus Visit Form V2** trên repository hiện có. Hãy trực tiếp kiểm tra source, Git history, database scripts và tests rồi thực hiện công việc. Không chỉ đưa kế hoạch hoặc scaffold. Tuy nhiên, **không được đoán business rule, không được sửa qua loa để test xanh, không được thực hiện destructive migration trên database thật**.

Mục tiêu của phiên:

1. Reconcile có bằng chứng hai vấn đề do Dev auto-merge gây ra.
2. Khôi phục một baseline test rõ ràng trong phạm vi có thể thực hiện an toàn.
3. Chuẩn bị và drill **Phase I guarded contract-drop candidate** chỉ trên các disposable MySQL databases.
4. Kết luận trung thực về readiness; nhiều khả năng là **prepared/tested, but NOT READY FOR EXECUTION**.

Không tuyên bố chương trình FINAL, không bật production flags và không xóa V1 để tạo cảm giác readiness giả.

---

## 1. Bối cảnh và checkpoint bắt buộc phải xác minh

Checkpoint được báo cáo gần nhất:

- branch: `Canh-Iter1`;
- local HEAD: `5a44ebdd`;
- upstream: `origin/Cảnh-Iter1`;
- trạng thái lúc kết thúc phiên trước: `0 behind / 3 ahead`;
- Dev đã được môi trường auto-merge qua pushed merge commit `64c83a59`;
- `64c83a59` có parent 1 là checkpoint trước đó `5b943b1a`;
- Slice 6 đã hoàn thành;
- real-stack API-level A–H: `8/8`;
- full Browser DOM workflows: `9/9`;
- tổng real-stack: `17/17`;
- Vitest: `99`;
- Architecture: `14/14`;
- E2E auth guards: `4/4`;
- TypeScript: `0` lỗi;
- Vite build: pass;
- Unit hiện `528/530` vì hai photo-upload failures sau Dev merge;
- targeted V2 IT hiện `44/45` vì guest-name search conflict;
- last known clean full Integration trước merge: `400/400`; full suite chưa rerun sau merge;
- `PerCampusFormV2.Enabled` và `PerCampusFormV2Write.Enabled` vẫn mặc định OFF;
- Phase I chưa bắt đầu;
- bốn plan/handoff docs đang untracked và không được stage/commit.

Các con số trên chỉ là resume context. Phải tự xác minh trạng thái hiện tại vì môi trường có thể auto-push/auto-merge bất đồng bộ giữa các phiên.

---

## 2. Source of truth và thứ tự ưu tiên

Trước khi sửa code, đọc các tài liệu/source thực tế sau nếu tồn tại trong repo hoặc thư mục tài liệu “sau hop 13-07”:

1. `PEMS_PER_CAMPUS_V2_FULL_CONVERSATION_HANDOFF.md`.
2. `PEMS_PER_CAMPUS_V2_MASTER_HANDOFF_PROMPT*.md`.
3. `FINAL_IMPLEMENTATION_REPORT.md`.
4. `IMPLEMENTATION_PROGRESS.md`.
5. `PR3_PRE_PR4_AUDIT_MAP.md` và các audit map/report liên quan.
6. H2 E2E matrix, H3 rollout/observability và H4 real-stack docs.
7. `PEMS_CANONICAL_BUSINESS_RULES...md`.
8. `PEMS_UC_IMPLEMENTATION_RULEBOOK...md`.
9. `PERMISSION_MATRIX.md`, `PERMISSION_RULES.md` và HO read-only notes.
10. SQL master hiện hành, migration README và toàn bộ additive patches V2.
11. Source code và tests ở current HEAD.

Thứ tự xử lý khi nguồn mâu thuẫn:

- business/security rule explicit, mới hơn và có thẩm quyền cao hơn thắng;
- source hiện tại không tự động thắng canonical rule chỉ vì được merge sau;
- test hiện tại cũng không tự động là truth nếu mâu thuẫn với rule explicit;
- commit message không đủ để thay đổi business rule;
- nếu tìm thấy hai quyết định Product/Security ngang thẩm quyền nhưng trái nhau, **dừng riêng hạng mục đó**, đưa bằng chứng file/commit/line và xin quyết định; không đoán.

Đặc biệt, master handoff canonical hiện ghi rõ:

> Không bật search guest/support names mặc định vì PII/chi phí.

Vì vậy, nếu không tìm thấy một quyết định Product/Security mới hơn và explicit cho phép guest-name search, phải giữ/khôi phục hành vi **guest/support names không được tìm mặc định**. Không được âm thầm cập nhật test để hợp thức hóa clause mới từ Dev.

---

## 3. Nguyên tắc làm việc không được vi phạm

### 3.1. Audit-first, evidence-first

- Không sửa file trước khi tái hiện lỗi và xác định root cause.
- Không dựa vào tên test hoặc báo cáo cũ; đọc implementation, caller, validator, test fixture và Git diff.
- Khi nói một reference là runtime read/write, phải chỉ ra đường gọi hoặc consumer cụ thể.
- Khi nói database guard tồn tại, phải kiểm tra `information_schema` hoặc DDL thực tế.
- Khi nói test pass/fail, phải đưa command và count thực chạy.
- Không báo “full regression green” nếu còn skipped, compile failure hoặc suite chưa chạy.

### 3.2. Không làm test xanh giả

Không được:

- xóa/skip/disable test;
- nới assertion chỉ để nhận output hiện tại;
- đổi expected error code sang text matching;
- catch-all exception rồi trả success;
- mock network trong suite được gọi real-stack;
- sửa seed để né rule;
- bỏ security assertion hidden-campus/PII;
- mass-edit connection strings hoặc 25 test files chỉ để ép suite chạy;
- đổi protected DB schema để hợp test.

Chỉ sửa test khi đã chứng minh bằng rule/contract explicit rằng test cũ sai. Báo rõ bằng chứng và lý do.

### 3.3. Git

- Kiểm tra branch, HEAD, upstream, ahead/behind, merge-base, parents và remote ref.
- Kiểm tra `git status --short`, staged diff và untracked files trước mutation.
- Mọi pushed/auto-pushed commit là immutable.
- Không reset/rebase/amend/force-push pushed history.
- Không chạy `git push`, merge hoặc mở PR.
- Không đổi `git config --global`.
- Không stage bốn plan/handoff docs.
- Stage bằng explicit pathspec; trước commit chạy `git diff --cached --name-status`.
- Author và committer cho commit mới:
  `Tcanh12 <canhnvthe186121@fpt.edu.vn>`.
- Không thêm AI/Claude/ChatGPT/Generated/Assisted/Co-Authored attribution.

### 3.4. Database safety

Protected databases, tuyệt đối không DDL/DML/recreate/drop:

- `pems_db`;
- `pems_test`;
- `pems_pr3_test`.

Phase I chỉ dùng đúng disposable databases:

- `pems_i_fresh`;
- `pems_i_upgrade`;
- `pems_i_refusal`;
- `pems_i_rollback`.

Nếu cần database khác, phải là tên explicit bắt đầu `pems_i_`, được tạo trong phiên và drop trong cleanup. Không dùng biến rỗng, glob hoặc unresolved shell expansion khi drop database.

Không chạy candidate UP/DOWN trên staging, production hoặc protected DB dưới bất kỳ lý do nào.

### 3.5. Feature flags và production

- Không thêm section bật flags trong appsettings.
- Không đổi default OFF.
- Không deploy, canary hoặc đổi production configuration.
- Không wire destructive SQL vào startup/migration auto-run.
- Production rollback vẫn là flags OFF, không phải DOWN migration.

---

## 4. Phase R — preflight và audit Dev merge

Thực hiện read-only preflight trước mọi edit:

1. Xác minh `5a44ebdd`, `64c83a59`, `5b943b1a` còn reachable.
2. Xem `git show --stat --oneline --decorate 64c83a59` và hai-parent diff.
3. Xác định chính xác file overlap với Per-Campus V2, test harness, search và upload validation.
4. Xác minh bốn untracked plan/handoff docs; không chạm vào chúng.
5. Xác minh flags OFF từ source/config.
6. Xác minh appsettings testing đang trỏ lại `pems_test`; nếu cần đổi tạm cho disposable, backup byte-exact và cài trap restore bằng absolute path.
7. Ghi preflight evidence vào working notes/report.

Điều kiện dừng:

- branch sai;
- checkpoint mất khỏi ancestry;
- unexpected divergence không phải benign forward progression;
- staged/user changes overlap với scope;
- không xác định được protected DB boundary.

Nếu gặp điều kiện dừng, không tự reset hoặc xóa thay đổi; báo rõ và xin hướng dẫn.

---

## 5. Phase R1 — reconcile guest-name search conflict

Mục tiêu: xác định và thực thi đúng business/security contract, không chọn theo cảm tính.

### 5.1. Audit bắt buộc

1. Tìm tất cả keyword-search clauses liên quan:
   - guest name;
   - support member name;
   - `visit_guest_members`;
   - `visit_instance_guest_members`;
   - `matchedContexts`;
   - both request-level and instance-level query paths.
2. Đọc `ViewGuestDelegationListQueryHandler` và builder/DTO liên quan.
3. Kiểm tra thứ tự thật:
   `authorization scope → keyword → count → order → page → matchedContexts enrichment`.
4. Kiểm tra hidden sibling campus có thể ảnh hưởng hit/count/order hay không.
5. Kiểm tra raw guest/support name có bị trả trong `matchedContexts`, log hoặc audit không.
6. Xem commit/merge diff nào thêm guest-name clause và commit message/rationale.
7. Tìm explicit rule mới hơn canonical C6. Không suy đoán từ code.

### 5.2. Decision rule

- Nếu không có explicit Product/Security decision mới hơn: canonical rule thắng; loại bỏ/revert riêng guest/support default-search clause và giữ test `Guest_member_names_are_not_searched_and_produce_no_row`.
- Nếu có explicit rule mới hơn cho phép search: cập nhật implementation và tests theo rule đó, nhưng vẫn bắt buộc scope-before-keyword, zero hidden-campus leak và PII-free matchedContexts/logs.
- Nếu nguồn ngang thẩm quyền mâu thuẫn: dừng riêng R1 và báo user bằng bảng bằng chứng; không tự quyết.

### 5.3. Tests tối thiểu

- guest-only keyword không tạo row khi canonical exclusion áp dụng;
- support-only keyword không tạo row;
- hidden sibling guest/support name không thay đổi hit/count/order;
- authorized campus scalar/delegation keyword vẫn tạo đúng CAMPUS context;
- request-level keyword vẫn tạo REQUEST context;
- owner full scope và campus-scoped leader cho kết quả đúng;
- matchedContexts không chứa raw PII;
- existing Slice 5B security tests xanh.

Không mở rộng search surface ngoài quyết định đã chứng minh.

---

## 6. Phase R2 — sửa hai photo-upload validation failures

Mục tiêu: sửa root cause của regression do `FileValidationPolicy.cs` change, không nới tests tùy tiện.

### 6.1. Reproduce và isolate

1. Chạy đúng hai failing Unit tests riêng, lưu full failure names/messages/stack traces.
2. Chạy toàn bộ test class/module photo upload.
3. Dùng `git blame`, `git log -p` và two-parent merge diff để xác định thay đổi gây regression.
4. Đọc:
   - `FileValidationPolicy.cs`;
   - interface/rules liên quan;
   - validator/service gọi policy;
   - endpoint/use case upload ảnh;
   - DTO/configuration;
   - positive, boundary và negative tests;
   - docs về extension, MIME, size, purpose và security.
5. Xác định contract cho từng file purpose; không assume mọi upload dùng cùng rule.

### 6.2. Security invariants

Giữ nguyên hoặc làm mạnh hơn:

- allowlist extension/MIME/purpose;
- không tin filename hoặc Content-Type đơn lẻ;
- giới hạn size đúng boundary;
- reject empty/truncated/invalid content nếu policy hiện yêu cầu;
- không path traversal;
- không raw file/PII/secret logs;
- error code ổn định;
- không làm Gallery/other upload purposes regression.

### 6.3. Fix rule

- Nếu implementation mới trái contract explicit: sửa implementation.
- Nếu test cũ trái contract explicit mới hơn: cập nhật test với citation/evidence trong report.
- Nếu thiếu contract: không đoán; lập bảng options/impact và hỏi user.
- Sửa nhỏ nhất đủ đúng; không refactor rộng ngoài scope.

### 6.4. Gates cho R2

- hai targeted tests pass;
- toàn bộ photo/file validation unit tests pass;
- full Unit target `530/530` hoặc count mới cao hơn nếu Dev thêm tests;
- Architecture pass;
- không làm thay đổi Per-Campus V2 behavior ngoài guest-search decision độc lập.

---

## 7. Checkpoint sau reconcile

Chỉ bắt đầu Phase I khi:

- R1 đã có quyết định bằng chứng hoặc được user xác nhận;
- R2 root cause đã giải quyết;
- Unit full green;
- targeted V2 IT full green;
- working tree chỉ chứa intended changes + bốn untracked plan/handoff docs;
- protected DBs không bị mutation.

Nếu một decision đang blocked, được phép dừng ở clean checkpoint sau R1/R2; không bắt đầu Phase I dở dang.

Commit reconcile theo logical slice, ví dụ:

- `fix(delegations): restore canonical guest-search scope`
- `fix(files): align photo upload validation with purpose policy`

Chỉ dùng message thực sự đúng với thay đổi. Không gộp unrelated fixes.

---

## 8. Phase I-A — zero-unclassified audit của 10 legacy fields

Audit đúng mười global fields trên `visit_requests`:

1. `delegation_name`
2. `visit_type`
3. `visit_type_other`
4. `purpose`
5. `working_content`
6. `working_language`
7. `transportation_note`
8. `media_consent_status`
9. `media_consent_note`
10. `note_to_fptu`

Không nhầm với operational-contact fields; các field đó không thuộc contract drop này.

### 8.1. Phạm vi audit

Tìm bằng `rg`/source navigation trong:

- entities và EF mappings;
- DbContext/configurations;
- commands, queries, handlers và services;
- create/edit/resubmit/safe-edit/amendment;
- list/detail/search;
- report/export/email/notification;
- agenda/minutes/feedback/invoice/partner/gallery/OCR/downstream;
- DTOs/mappers/serializers;
- validators;
- raw SQL/stored routines/views/triggers;
- migrations/master/seed/backfill/verify/rollback;
- frontend API types/clients;
- Unit/Integration/E2E tests;
- docs/examples/fixtures.

### 8.2. Phân loại bắt buộc cho từng occurrence

Mỗi occurrence phải thuộc đúng một nhóm:

- runtime read V1;
- runtime read compatibility/dual-read;
- runtime write V1;
- runtime compatibility projection write;
- migration/backfill/verify only;
- EF/schema mapping;
- test/fixture only;
- documentation/comment only;
- false positive/unrelated symbol.

Tạo bảng zero-unclassified có tối thiểu:

| Field | File/symbol | Read/write | Runtime path/consumer | V1/V2 behavior | Required after drop? | Blocker | Planned action |
|---|---|---|---|---|---|---|---|

Không đánh dấu “unused” chỉ dựa vào grep. Với runtime code, phải trace caller/DI/endpoint/job.

### 8.3. Readiness verdict

Đánh giá riêng:

- zero legacy runtime reads;
- zero legacy runtime writes;
- all persisted requests `form_schema_version=2`;
- complete per-instance detail/member backfill;
- no old client/draft;
- V1 fallback retired;
- feature flags/cutover state;
- export/restore proven;
- full regression state.

Expected hiện tại:

- runtime dual-read còn tồn tại;
- compatibility projection writers còn tồn tại;
- flags vẫn OFF;
- V1 fallback còn chủ đích.

Do đó verdict dự kiến là **NOT READY FOR EXECUTION**. Audit phải chứng minh bằng source/data, không copy kết luận này mà không kiểm tra.

---

## 9. Phase I-B — candidate SQL package

Chuẩn bị candidate package nhưng không wire/runs trên DB thật. Tên file phải theo convention hiện có sau khi inspect repo; không tự bịa số migration nếu đã có file mới.

Package logic cần có:

1. `preflight/readiness` — read-only.
2. `guarded_up` — destructive candidate, default-deny.
3. `verify` — read-only postcondition.
4. `down_restore_compatibility` — candidate rollback/reconstruction.
5. README/runbook với exact commands, allowlist DB và expected outcomes.

### 9.1. Read-only preflight

Phải kiểm tra tối thiểu:

- current database nằm trong explicit disposable allowlist/prefix;
- schema version và exact definitions của 10 columns;
- actual dependent indexes/checks/constraints từ `information_schema`;
- mọi request đủ điều kiện V2 theo rule đã xác định;
- mỗi active campus instance có đúng một form detail;
- không missing/orphan/cross-request detail/member links;
- backfill counts và deterministic compatibility projection hợp lệ;
- không pending migration/schema drift chưa xử lý;
- readiness evidence bên source cho zero reads/writes được cung cấp riêng;
- explicit operator confirmation vẫn chưa được bật mặc định.

Preflight không được DDL/DML.

### 9.2. Guarded UP

Yêu cầu:

- default-deny;
- require explicit opt-in/confirmation theo SQL convention repo;
- refuse nếu database không phải disposable Phase I;
- chạy toàn bộ guards trước DDL đầu tiên vì MySQL DDL implicit commit;
- nếu một guard fail, cả 10 columns/index/check vẫn còn nguyên;
- chỉ drop đúng 10 fields và dependencies thực tế đã audit;
- không drop operational-contact fields;
- không drop V2 tables/history/audit;
- không chứa `USE pems_db`, `USE pems_test` hoặc `USE pems_pr3_test`;
- không được app startup tự gọi;
- rerun behavior phải explicit: controlled no-op hoặc refusal có code/message ổn định;
- không dùng dynamic target từ env/glob không resolve.

Không đoán tên index/check. Lấy từ master hiện hành và `information_schema`, rồi verify fresh-vs-upgrade.

### 9.3. Verify

Phải chứng minh:

- 10 columns không còn sau UP;
- dependent legacy indexes/checks đã xử lý đúng;
- V2 tables/columns/FKs/unique/checks vẫn nguyên;
- detail/member/request counts không đổi ngoài expected DDL metadata;
- no orphan/cross-request;
- representative V2 reads/queries vẫn hoạt động trên candidate-compatible code/schema context;
- schema fingerprint đúng expected.

Không tuyên bố runtime app compatible sau drop nếu audit vẫn còn legacy readers/writers. Khi runtime hiện tại chưa compatible, drill SQL là schema-candidate drill chứ không phải proof production can run.

### 9.4. DOWN restore compatibility

DOWN phải:

- chỉ chạy trên disposable allowlist;
- re-add exact types/nullability/defaults/collation/comments theo pre-drop schema;
- restore exact dependent indexes/checks cần thiết;
- reconstruct compatibility projection từ V2 source deterministically;
- với multi-campus, dùng rule canonical hiện hành, ví dụ smallest `campus_id`, sau khi xác minh source/migration conventions;
- không biến projection thành V2 source of truth;
- preserve V2 tables/data/history;
- có guards chống partial/incorrect restore;
- verify schema/data sau restore.

Nếu dữ liệu không thể khôi phục losslessly, phải ghi rõ DOWN là restore-compatibility chứ không phải time-travel restore, và chứng minh deterministic result.

### 9.5. Fresh-create candidate

Chuẩn bị clean-V2 fresh-create candidate theo yêu cầu Phase I nhưng:

- không thay thế master đang dùng cho production/runtime khi readiness còn FAIL;
- có thể tạo candidate artifact hoặc disposable materialization riêng;
- phải schema-diff với upgrade+UP result;
- ghi rõ đây là candidate, chưa được promote thành canonical production master.

---

## 10. Phase I-C — disposable drills

Mọi database phải được tạo mới trong phiên và drop khi xong.

### 10.1. `pems_i_fresh`

- Dựng clean V2 candidate từ đúng master/migrations đã audit.
- Chạy verify.
- Chứng minh 10 legacy fields absent trong candidate clean schema.
- Chứng minh V2 schema/guards đầy đủ.

### 10.2. `pems_i_upgrade`

- Dựng baseline/version đúng theo migration history thực tế; không assume patch list cũ vẫn đầy đủ.
- Apply additive V2 migrations theo README hiện hành.
- Chạy backfill/verify/idempotency nếu applicable.
- Seed/ensure all preconditions.
- Chạy preflight rồi guarded UP với explicit opt-in.
- Chạy verify.

### 10.3. `pems_i_refusal`

Tạo từng refusal fixture độc lập hoặc reset rõ ràng:

- database name không allowlisted;
- V1 row còn tồn tại;
- missing form detail;
- orphan/cross-request member/detail;
- schema drift/index/check mismatch;
- explicit confirmation thiếu;
- bất kỳ blocker canonical nào audit phát hiện.

Với mỗi case:

- assert stable refusal/error;
- assert không DDL nào xảy ra;
- assert cả 10 columns và dependencies còn nguyên;
- assert V2 data không bị đổi.

### 10.4. `pems_i_rollback`

- Dựng eligible upgrade schema.
- Chạy preflight + UP.
- Verify post-UP.
- Chạy DOWN restore compatibility.
- Verify exact definitions/index/checks.
- Verify deterministic projections và V2 data preservation.
- So sánh schema trước UP và sau DOWN; giải thích mọi difference hợp lệ.

### 10.5. Schema diff và evidence

So sánh ít nhất:

- fresh candidate vs upgrade+UP;
- pre-UP vs post-DOWN;
- columns;
- types/nullability/defaults/comments;
- indexes/uniques;
- FKs/checks;
- table counts;
- critical row counts/fingerprints.

Không chỉ so `SHOW TABLES`; dùng canonicalized `information_schema` queries hoặc tool/script deterministic.

---

## 11. Test gates sau thay đổi

Chạy theo phạm vi thay đổi và báo đúng thực tế:

### Backend

- targeted guest-search security IT;
- targeted photo/file validation Unit tests;
- full Unit;
- Architecture;
- targeted Per-Campus V2 suites;
- E2E auth guards;
- full Integration nếu có thể cấu hình hoàn toàn trên disposable mà không sửa protected DB hoặc mass-edit source.

Nếu full Integration không thể chạy do 25 files hardcode `pems_pr3_test`:

- không recreate/mutate `pems_pr3_test`;
- không mass-edit connection strings;
- ghi exact blocker/count;
- chạy tối đa targeted/self-rollback suites an toàn;
- không gọi full suite pass.

### Frontend/E2E

- `npm run test:unit`;
- `npm run lint`/`tsc --noEmit`;
- `npm run build`;
- browser-contract suite nếu dependency ổn;
- `npm run test:e2e:realstack` sau guest-search reconciliation vì search DOM journey có thể bị ảnh hưởng;
- expected real-stack total: API-level `8` + DOM `9` = `17`, trừ khi suite hợp lệ được mở rộng.

Không đếm mocked-network test là real-stack.

### SQL

- preflight pass trên eligible DB;
- every refusal case pass;
- guarded UP pass;
- verify pass;
- DOWN pass;
- fresh-vs-upgrade schema diff pass;
- pre-UP-vs-post-DOWN diff được giải thích;
- no partial DDL.

---

## 12. Cleanup và hygiene

Trước khi kết thúc:

- drop explicit disposable `pems_i_*` databases đã tạo;
- xác minh protected DBs không mutation;
- restore appsettings byte-exact;
- kill chỉ test API/Vite processes do phiên tạo;
- không đụng dev server của user;
- unlink junction an toàn, không recurse;
- xóa temp SQL copies, publish dirs, profiles, OTP inbox, logs, screenshots/traces chứa data;
- secret chỉ tồn tại trong process env;
- flags vẫn default OFF;
- kiểm tra không test rows leak;
- `git status` chỉ còn intended tracked state và bốn untracked plan/handoff docs;
- không stage prompt/handoff docs.

---

## 13. Commit strategy

Chỉ commit khi một logical slice hoàn chỉnh và gates tương ứng đã chạy.

Khuyến nghị tách:

1. Guest-search reconciliation, nếu có code change.
2. Photo validation fix, nếu có code change.
3. Phase I audit + guarded SQL candidate + drill automation/tests.
4. Report/progress update.

Không tạo empty/no-op commit. Không commit disposable evidence chứa secrets/PII. Evidence text phải sanitized.

Trước mỗi commit:

- `git diff --check`;
- review diff;
- explicit stage paths;
- `git diff --cached --name-status`;
- verify author/committer;
- scan commit message for AI markers.

Không push/merge/PR.

---

## 14. Stop conditions

Dừng và báo user thay vì đoán nếu:

- canonical guest-search rule có một newer explicit Product/Security rule mâu thuẫn;
- photo upload contract không thể xác định từ docs/source/tests/history;
- protected DB là cách duy nhất để chạy một test;
- UP guard không thể đảm bảo mọi precondition trước first DDL;
- exact pre-drop definitions/dependencies của 10 columns không xác định được;
- DOWN reconstruction rule không xác định;
- user changes overlap scope;
- auto-merge mới thay đổi files đang sửa;
- destructive command target không resolve explicit;
- Phase I candidate vô tình được wire vào runtime/startup;
- cleanup không thể xác nhận.

Khi dừng, hoàn tất mọi read-only evidence và cleanup có thể thực hiện trước khi báo blocker.

---

## 15. Báo cáo cuối phiên bắt buộc

Báo cáo bằng tiếng Việt, có số liệu thực, theo đúng thứ tự:

1. Branch/HEAD/upstream/ahead-behind.
2. Auto-push/auto-merge phát hiện trong phiên.
3. Preflight/divergence và fate của checkpoint commits.
4. Guest-search sources đã đọc, decision và bằng chứng.
5. Photo-upload failures: tên test, root cause, contract và fix.
6. Baseline Unit/targeted V2 trước và sau.
7. Phase I zero-unclassified audit: số occurrence theo từng category.
8. Readiness gate PASS/FAIL từng mục.
9. Danh sách chính xác candidate SQL/docs/scripts đã tạo.
10. Preflight/UP/verify/DOWN design và guards.
11. Kết quả `pems_i_fresh`.
12. Kết quả `pems_i_upgrade`.
13. Kết quả `pems_i_refusal`, từng refusal case và no-partial-DDL evidence.
14. Kết quả `pems_i_rollback`.
15. Schema diff fresh-vs-upgrade và pre-UP-vs-post-DOWN.
16. Unit/Architecture/Integration counts.
17. Vitest/browser-contract/real-stack counts.
18. Lint/TypeScript/build.
19. Disposable DBs đã dùng và cleanup.
20. Protected DB hygiene.
21. appsettings restore.
22. Feature flags final.
23. Git status và bốn untracked docs.
24. Commits SHA/message/files.
25. Author/committer/no-AI verification.
26. Xác nhận không push/merge/PR.
27. Done/Deferred/Limitations.
28. Program status và exact resume point.

Không được bỏ qua failing/skipped/not-run suites. Phân biệt rõ:

- PASS;
- FAIL;
- NOT RUN;
- BLOCKED;
- last known clean result.

---

## 16. Kết luận hợp lệ dự kiến

Nếu audit/drills đúng như trạng thái hiện tại, kết luận phải gần nghĩa:

> Phase I guarded contract-drop candidate đã được chuẩn bị và kiểm thử trên disposable databases. Candidate preflight/UP/verify/DOWN và refusal guards đã được chứng minh, không database thật nào bị sửa. Tuy nhiên execution vẫn NOT READY vì V1 fallback, flags mặc định OFF và/hoặc legacy runtime readers/writers còn tồn tại. Không destructive migration nào được áp dụng vào production, staging hoặc protected databases.

Chỉ được đổi sang READY nếu có bằng chứng mới cho **tất cả** readiness gates. Không xóa V1 code hoặc bật flags chỉ để làm kết luận đẹp hơn.

Chương trình vẫn là `IN PROGRESS` cho đến khi Phase I preparation, final audit và các yêu cầu rollout được hoàn tất đúng thẩm quyền.
