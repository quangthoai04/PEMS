# PROMPT THỰC THI PHẦN CÒN LẠI — PHASE I CANDIDATE AUDIT, DISPOSABLE DRILLS VÀ FINAL AUDIT

Bạn đang tiếp tục chương trình **PEMS Per-Campus Visit Form V2** trên repository hiện có. Hãy trực tiếp audit source/Git/SQL/tests, sửa candidate nếu phát hiện lỗi, dựng disposable MySQL infrastructure, chạy đầy đủ drills và regression gates rồi cập nhật báo cáo. Không chỉ lập kế hoạch hoặc đọc file rồi tuyên bố hoàn tất.

Yêu cầu chất lượng tối cao:

- làm nghiêm túc, evidence-first;
- không đoán business rule, schema, index/check name hoặc test result;
- không tin candidate SQL chỉ vì đã commit;
- không nới/skip test để làm xanh;
- không dùng protected database để tiện chạy test;
- không chạy destructive migration trên database thật;
- không xóa V1 hoặc bật flags để làm readiness giả;
- báo chính xác PASS, FAIL, NOT RUN, BLOCKED và last-known result.

Mục tiêu hợp lệ của phiên là:

1. Hoàn tất zero-unclassified audit đúng 10 legacy fields.
2. Review và harden năm Phase I candidate files.
3. Chạy đủ `pems_i_fresh`, `pems_i_upgrade`, `pems_i_refusal`, `pems_i_rollback`.
4. Chứng minh fresh-vs-upgrade parity, rollback compatibility và no-partial-DDL refusal.
5. Rerun các backend/frontend/real-stack gates cần thiết sau R1/R2.
6. Cập nhật report/progress với kết luận trung thực.

Kết luận dự kiến nếu mọi drill xanh:

> Phase I guarded contract-drop candidate prepared and tested on disposable databases; execution remains NOT READY while V1 fallback, legacy runtime readers/writers, persisted V1 data and default-OFF flags remain. No real database was modified.

Không được gọi production migration READY nếu bất kỳ readiness gate nào còn FAIL.

---

## 1. Resume context — phải tự xác minh, không được tin mù

Checkpoint được báo cáo gần nhất:

- branch: `Canh-Iter1`;
- HEAD: `a5610e2f` — `docs(database): prepare phase I contract-drop candidate and drills`;
- upstream: `origin/Cảnh-Iter1`;
- lúc kết thúc phiên trước: local `3 ahead`;
- pushed auto-merge: `64c83a59 Merge branch 'Dev' into Canh-Iter1`;
- checkpoints `5a44ebdd`, `64c83a59`, `5b943b1a` reachable;
- bốn plan/handoff docs untracked, không được stage/commit;
- R1 commit: `f4549b23` — restore canonical guest-search scope;
- R2 commit: `c1ebe1fc` — restore `VisitRequestPhoto` policy;
- Phase I draft commit: `a5610e2f`;
- Unit: `530/530`;
- targeted V2 IT: `45/45`;
- Architecture last known: `14/14`;
- full Integration clean gần nhất trước Dev merge: khoảng `400/400`, chưa có current clean full run;
- Vitest last known: `99`;
- real-stack last known: API-level `8/8` + full DOM `9/9` = `17/17`, chưa rerun sau R1;
- `PerCampusFormV2.Enabled` và `PerCampusFormV2Write.Enabled` mặc định OFF;
- Phase I four drills/schema diffs: NOT RUN;
- program status: `IN PROGRESS`;
- execution readiness: `NOT READY`.

Candidate directory được báo cáo:

`docs/database/scripts/phase_1_candidate/`

Candidate files:

1. `01_preflight.sql`
2. `02_guarded_up.sql`
3. `03_verify.sql`
4. `04_down_restore.sql`
5. `README.md`

Đây chỉ là resume context. Ngay khi bắt đầu phải kiểm tra actual HEAD, upstream và file contents vì môi trường có auto-push/auto-merge bất đồng bộ.

---

## 2. Quy tắc nguồn và phạm vi nghiệp vụ

Đọc trước khi sửa:

1. `PEMS_PER_CAMPUS_V2_FULL_CONVERSATION_HANDOFF.md`.
2. `PEMS_PER_CAMPUS_V2_MASTER_HANDOFF_PROMPT*.md`.
3. `FINAL_IMPLEMENTATION_REPORT.md`.
4. `IMPLEMENTATION_PROGRESS.md`.
5. `PR3_PRE_PR4_AUDIT_MAP.md` và audit reports liên quan.
6. H1 SQL drill evidence, H2 E2E matrix, H3 rollout/observability, H4 real-stack docs.
7. `PEMS_CANONICAL_BUSINESS_RULES*.md`.
8. `PEMS_UC_IMPLEMENTATION_RULEBOOK*.md`.
9. SQL master hiện hành, migration README và toàn bộ migrations sau master.
10. Current source, tests và Git history.

Không sửa lại R1/R2 nếu tests vẫn xanh, trừ khi audit phát hiện regression thực:

- guest/support names không được search mặc định vì PII/chi phí;
- scope phải xác định trước keyword;
- hidden campus không ảnh hưởng hit/count/order/context;
- `VisitRequestPhoto` là image-only, tối đa 5 MB và yêu cầu image magic bytes;
- không nhập `mp4/webm` vào image policy này nếu không có explicit newer contract.

Phase I chỉ liên quan đúng 10 global fields trên `visit_requests`:

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

Không được drop operational-contact fields hoặc bất kỳ V2/history/audit field nào.

---

## 3. Non-negotiable safety rules

### 3.1. Git

- Preflight branch/HEAD/upstream/ahead-behind/merge-base/parents.
- Dùng `git ls-remote` hoặc equivalent read-only check để phát hiện auto-push.
- Pushed/auto-pushed history immutable.
- Không reset/rebase/amend/force-push pushed commits.
- Không chạy push, merge hoặc mở PR.
- Không đổi global Git config.
- Giữ bốn plan/handoff docs untracked; không stage, edit hoặc delete chúng.
- Không stage prompt này.
- Stage explicit pathspec, review cached name-status trước commit.
- Commit author + committer: `Tcanh12 <canhnvthe186121@fpt.edu.vn>`.
- Không AI/Claude/ChatGPT/Generated/Assisted/Co-Authored attribution.

### 3.2. Database

Protected, tuyệt đối không DDL/DML/recreate/drop:

- `pems_db`;
- `pems_test`;
- `pems_pr3_test`.

Allowed disposable Phase I databases:

- `pems_i_fresh`;
- `pems_i_upgrade`;
- `pems_i_refusal`;
- `pems_i_rollback`.

Nếu cần phụ trợ, tên phải explicit bắt đầu `pems_i_`, được tạo trong phiên và cleanup cuối phiên.

Không dùng unresolved variable, wildcard shell hoặc command substitution để drop database. Trước drop phải in/validate exact target name bằng read-only check.

### 3.3. Runtime/production

- Không bật flags trong appsettings.
- Không đổi defaults OFF.
- Không deploy/canary.
- Không wire candidate SQL vào startup/automatic migrations.
- Không chạy UP/DOWN trên staging/production/protected DB.
- Production rollback vẫn là flags OFF, không phải DOWN.
- Không xóa V1 runtime code trong Phase I prep.

### 3.4. Test integrity

Không được:

- skip/disable/delete test;
- đổi assertion cho hợp output nếu không có contract evidence;
- gọi last-known result là current pass;
- dùng network mock trong real-stack;
- sửa 25 hardcoded test connection strings hàng loạt;
- recreate protected `pems_pr3_test`;
- chạy test biết chắc sẽ write protected DB ngoài sanctioned rollback harness;
- che lỗi bằng broad catch/fallback success.

---

## 4. Preflight bắt buộc

Trước mọi edit hoặc database creation:

1. Xác minh current branch và HEAD.
2. Xác minh ancestry của `a5610e2f`, `f4549b23`, `c1ebe1fc`, `5a44ebdd`, `64c83a59`, `5b943b1a`.
3. Kiểm tra remote ref và ahead/behind.
4. Kiểm tra staged/unstaged/untracked state.
5. Xác minh bốn untracked docs đúng expected paths.
6. Review diff `64c83a59..HEAD` và riêng ba checkpoint commits mới.
7. Xác minh appsettings testing hiện tại và flags OFF.
8. Phát hiện MySQL client/server/container/docker-compose options có sẵn bằng read-only commands.
9. Liệt kê databases hiện có; không connect/mutate protected DB.
10. Xác minh SQL master/version/migration chain hiện hành; không dùng V10/V11 cũ chỉ vì báo cáo trước từng dùng.

Stop nếu:

- unexpected divergence;
- user changes overlap candidate files;
- auto-merge mới đụng same files;
- protected DB boundary không rõ;
- không xác định được current master/migration order.

Không tự reset hoặc xóa user changes.

---

## 5. Phase I-A — independent review của candidate files

Không chạy candidate trước khi review line-by-line.

### 5.1. `README.md`

README phải ghi rõ:

- candidate only;
- `NOT READY FOR EXECUTION`;
- exact prerequisites;
- exact allowed database names;
- exact client invocation;
- error handling/nonzero behavior;
- no `--force` mode;
- files chạy theo thứ tự nào;
- UP destructive, DOWN restore-compatibility;
- expected outcomes của fresh/upgrade/refusal/rollback;
- cleanup commands dùng exact names;
- không production/staging/protected DB;
- không automatic migration wiring.

Nếu README nói “safe” hoặc “tested” khi drills chưa chạy, sửa wording thành draft/unverified trước drill.

### 5.2. Database allowlist — kiểm tra đặc biệt

Báo cáo trước nói scripts dùng prefix `pems_i_%`. Trong MySQL `LIKE`, ký tự `_` là wildcard một ký tự. Vì vậy:

- kiểm tra actual implementation;
- không mặc định `LIKE 'pems_i_%'` là exact literal prefix;
- guard phải chỉ chấp nhận intended Phase I database names;
- ưu tiên explicit allowlist bốn names hoặc literal-prefix validation không có wildcard ambiguity;
- nếu dùng `LIKE`, escape `_` đúng MySQL mode và test negative names;
- test tối thiểu các tên phải REFUSE:
  - `pems_db`;
  - `pems_test`;
  - `pems_pr3_test`;
  - `pemsXi_bad`;
  - `pems-i-bad`;
  - `pems_i`;
  - empty/NULL database context;
  - database khác chỉ tình cờ match wildcard.

Không chỉ kiểm tra prefix trong README; guard phải nằm trong executable SQL trước DDL.

### 5.3. `01_preflight.sql`

Chứng minh script read-only:

- không INSERT/UPDATE/DELETE/DDL/temp persistent mutation;
- không gọi procedure có side effect;
- không đổi schema/database ngoài harmless session variables;
- trả readiness matrix/counts rõ ràng;
- trả nonzero/stable refusal khi dùng như gate, hoặc documented machine-readable output mà orchestrator kiểm tra bắt buộc.

Preflight phải kiểm tra ít nhất:

- exact allowed database;
- required tables/columns;
- exact definitions của 10 legacy fields;
- actual dependent indexes/checks/FKs/triggers/views/routines từ `information_schema`;
- `form_schema_version` distribution;
- V1 rows count;
- missing/duplicate instance form details;
- orphan/cross-request form/member links;
- backfill completeness;
- null/invalid data relevant to reconstruction;
- schema drift;
- expected migration/table presence;
- explicit opt-in vẫn OFF mặc định.

Source-code readiness (zero runtime reads/writes) không thể được SQL tự chứng minh; README/preflight phải tham chiếu audit artifact riêng, không hardcode PASS giả.

### 5.4. `02_guarded_up.sql`

Review MySQL semantics thật:

- DDL implicit commit;
- `SIGNAL` behavior;
- procedure create/call/drop behavior;
- client có dừng ngay khi error hay tiếp tục statements;
- prepared statements/dynamic SQL;
- session variable lifetime;
- transaction không được dùng để hứa rollback DDL giả.

UP bắt buộc:

- default deny khi `@ENABLE_PHASE_1_DROP` NULL/0/missing;
- exact allowed database;
- mọi precondition chạy xong trước DDL đầu tiên;
- không DDL xảy ra nếu bất kỳ guard fail;
- hardcode/validate đúng 10 column identifiers;
- dependency names lấy từ actual schema/master, không đoán;
- refuse missing/unexpected columns;
- refuse unexpected index/check/schema drift;
- refuse V1 data/missing detail/orphan/cross-request;
- không drop operational contact/V2/history/audit;
- rerun behavior explicit và tested;
- no `USE pems_db/pems_test/pems_pr3_test`;
- no startup wiring.

Nếu script gồm nhiều ALTER statements, chứng minh không có precondition giữa chúng có thể fail sau ALTER đầu tiên. Post-DDL failures thuộc verify, không được mô tả là atomic rollback.

### 5.5. `03_verify.sql`

Verify phải machine-readable và kiểm tra:

- cả 10 columns absent;
- exact legacy dependencies handled;
- V2 tables/columns/indexes/FKs/checks unchanged;
- row counts/fingerprints của request/campus/detail/member/history không đổi ngoài expected schema metadata;
- no orphan/cross-request;
- representative candidate queries hoạt động ở mức schema/data phù hợp;
- schema fingerprint đúng;
- không tuyên bố current runtime app compatible nếu source audit còn readers/writers.

### 5.6. `04_down_restore.sql`

DOWN là restore-compatibility candidate, không phải time-travel rollback. Review:

- exact allowed DB guard;
- explicit opt-in/default deny nếu destructive/reconstructive;
- exact pre-drop type, length, nullability, default, collation, comment và ordinal expectations;
- exact indexes/checks restored;
- deterministic projection rule từ V2 details;
- smallest `campus_id` chỉ dùng nếu canonical/current migration xác nhận;
- behavior cho uniform/mixed/missing detail;
- V1 rows đã bị UP refuse nên không có unreconstructable V1-only data;
- V2 tables/history/audit preserved;
- idempotency/rerun behavior explicit;
- post-DOWN verify đầy đủ.

Nếu DOWN tạo backup table hoặc dùng dữ liệu tạm:

- xác minh lifetime;
- naming collision;
- cleanup;
- no secret/PII export;
- không phụ thuộc artifact đã mất sau client disconnect;
- không gọi đó là rollback đầy đủ nếu chỉ reconstruct compatibility projection.

### 5.7. SQL static checks

Tìm và review:

- `USE` statements;
- `DROP DATABASE/TABLE/COLUMN`;
- dynamic SQL;
- wildcard/prefix checks;
- `@ENABLE_PHASE_1_DROP`;
- `SIGNAL`;
- stored procedure delimiters;
- dependencies của 10 columns;
- comments nói quá mức evidence;
- hardcoded schema/table counts cũ;
- implicit assumptions về V10/V11.

Mọi fix candidate phải có test/drill chứng minh, không chỉ code review.

---

## 6. Phase I-B — zero-unclassified legacy-field audit

Báo cáo trước chưa có occurrence counts nên audit chưa hoàn tất.

### 6.1. Search scope

Tìm từng field trong:

- Domain entities;
- EF configurations/DbContext/migrations;
- Application commands/queries/services;
- API DTO/controllers;
- create/public create/pending edit/resubmit;
- safe edit/amendment;
- list/detail/search;
- report/export/email/notification;
- agenda/minutes/feedback/invoice/partner/gallery/OCR/downstream;
- background jobs;
- raw SQL/views/triggers/routines;
- frontend types/client/components;
- tests/fixtures/seeds;
- docs/comments/generated outputs.

Loại generated/bin/obj/node_modules khỏi primary count nhưng ghi rõ exclusions.

### 6.2. Classification

Mỗi occurrence phải vào đúng một category:

1. Runtime V1 read.
2. Runtime dual-read/compatibility read.
3. Runtime V1 write.
4. Runtime compatibility projection write.
5. EF/schema mapping required before drop.
6. Migration/backfill/verify/down-only.
7. Test/fixture-only.
8. Documentation/comment-only.
9. False positive/unrelated symbol.

Với runtime occurrence phải trace caller/consumer/endpoint/job; grep đơn thuần không đủ.

### 6.3. Required artifact

Tạo/update tracked Phase I audit report theo convention repo, chứa:

| Field | File:line/symbol | Category | Read/write | Runtime caller/consumer | V1/V2 behavior | Blocker? | Action before execution |
|---|---|---|---|---|---|---|---|

Thêm summary counts:

| Category | Occurrences | Unique files | Blocking occurrences |
|---|---:|---:|---:|

Zero-unclassified nghĩa là:

- tổng classified bằng tổng in-scope matches sau exclusions;
- mỗi false positive có lý do;
- không dùng “etc.” thay cho occurrence;
- không chỉ đưa vài ví dụ như `SubmitVisitRequestCommandHandler.cs`.

### 6.4. Readiness verdict

Ghi PASS/FAIL riêng:

- zero runtime reads;
- zero runtime writes;
- all persisted requests V2;
- full backfill;
- no old client/draft;
- V1 fallback retired;
- flags/cutover state;
- export/restore proof;
- current regression baseline.

Expected hiện tại là nhiều FAIL. Không sửa/remove compatibility code trong Phase I prep để ép PASS.

---

## 7. Phase I-C — dựng disposable MySQL infrastructure

Không chấp nhận dừng ngay với câu “local MySQL limitation” trước khi audit các lựa chọn an toàn có sẵn.

Theo thứ tự:

1. Tìm repo docker-compose/test-infrastructure scripts.
2. Kiểm tra local MySQL client/server/version.
3. Kiểm tra existing disposable container/service do project cung cấp.
4. Kiểm tra có thể start isolated MySQL instance/container trên non-production port mà không đụng user service hay không.
5. Kiểm tra required MySQL version/sql_mode/timezone/charset/collation từ source.
6. Không download/install service hoặc thay system config nếu cần authority ngoài task; báo blocker nếu đến bước đó.

Yêu cầu infrastructure:

- version tương thích production/project;
- database names explicit;
- credentials không log/commit;
- strict mode/collation/timezone documented;
- disposable data directory/workdir;
- process/container cleanup trong `finally`/trap;
- không bind/chạm protected server/schema;
- verify connection target trước mọi CREATE/DROP.

Nếu không thể dựng an toàn sau khi exhaust repo/local options:

- dừng trước UP;
- báo exact missing binary/service/permission/port;
- cung cấp exact reproducible commands cho môi trường có MySQL;
- không fake drill bằng static parsing;
- vẫn hoàn tất candidate review và zero-unclassified audit.

---

## 8. Phase I-D — bốn drills bắt buộc

### 8.1. Shared drill discipline

- Dùng exact current master/migration chain sau audit.
- Mỗi database được create fresh.
- Capture commands, exit codes, elapsed time và sanitized outputs.
- MySQL client không dùng `--force` hoặc option tiếp tục sau lỗi.
- Script refusal phải tạo nonzero/fail state được orchestrator nhận biết.
- Trước/after snapshot dùng canonicalized `information_schema` queries.
- Mỗi drill cleanup riêng; final cleanup kiểm tra lại.

### 8.2. `pems_i_fresh`

Mục tiêu: clean V2 candidate schema.

1. Xác định fresh-create source đúng; không dùng attachment V10 cũ nếu repo đã V11/V12.
2. Dựng candidate fresh schema không có 10 legacy columns.
3. Apply only migrations cần cho current V2 candidate.
4. Chạy verify.
5. Assert V2 tables/constraints/indexes/nullable rules/duration guards.
6. Assert no legacy columns/dependencies.
7. Snapshot schema fingerprint.

Không replace production master bằng clean candidate khi runtime readiness còn FAIL. Candidate fresh artifact phải được đánh dấu riêng.

### 8.3. `pems_i_upgrade`

Mục tiêu: current supported baseline → additive V2 → eligible data → guarded UP.

1. Dựng đúng baseline theo migration history thực tế.
2. Apply all required additive migrations theo actual README/order.
3. Run backfill/verify/idempotency nếu applicable.
4. Seed hoặc transform chỉ disposable data để mọi UP precondition pass.
5. Run `01_preflight.sql` và assert readiness data gates.
6. Confirm `@ENABLE_PHASE_1_DROP` default/missing REFUSE trước.
7. Set explicit enable only trong disposable session.
8. Run `02_guarded_up.sql`.
9. Run `03_verify.sql`.
10. Snapshot schema/data fingerprints.

### 8.4. `pems_i_refusal`

Phải chứng minh default-deny và no partial DDL.

Refusal cases tối thiểu:

1. Enable variable missing.
2. Enable = 0.
3. Database name ngoài allowlist.
4. Near-match name khai thác `_` wildcard.
5. V1 row còn tồn tại.
6. Missing form detail.
7. Duplicate detail nếu schema fixture cho phép tạo trước constraints hoặc controlled drift fixture.
8. Orphan/cross-request detail/member link.
9. Missing/unexpected legacy column.
10. Unexpected index/check/schema drift.
11. Incomplete backfill/null invalid projection source.
12. Rerun UP after already dropped schema theo documented behavior.

Mỗi case phải:

- start từ known snapshot hoặc reset fixture;
- return stable refusal/nonzero;
- assert 10 columns vẫn nguyên;
- assert dependencies vẫn nguyên;
- assert data fingerprints không đổi;
- assert no backup/temp artifact leak;
- không chỉ kiểm tra message text nếu có stable code/result mechanism.

Quan trọng: kiểm tra tất cả guards chạy trước first DDL. Một case fail sau ALTER đầu tiên là lỗi thiết kế, không phải refusal pass.

### 8.5. `pems_i_rollback`

Mục tiêu: prove UP + restore-compatibility DOWN.

1. Dựng eligible pre-UP schema/data.
2. Snapshot exact definitions/indexes/checks/data fingerprints.
3. Preflight + explicit enable + UP.
4. Verify post-UP.
5. Run DOWN theo required opt-in/guards.
6. Verify restored columns exact definitions.
7. Verify dependencies exact.
8. Verify deterministic projection for:
   - single campus;
   - multi-campus uniform;
   - multi-campus mixed;
   - nullable notes;
   - visit_type OTHER;
   - media consent/note combinations.
9. Verify V2 detail/member/history/audit unchanged.
10. Compare pre-UP vs post-DOWN schema fingerprint.
11. Document any intentional difference; unexplained difference = FAIL.

DOWN không được mô tả là khôi phục historical global values nếu nó reconstruct từ V2 source.

---

## 9. Schema diff và data evidence

Tạo deterministic/canonicalized evidence cho:

### 9.1. Fresh vs upgrade+UP

So sánh:

- tables;
- columns/type/null/default/collation/comment/ordinal;
- indexes/uniques;
- FKs;
- checks;
- triggers/views/routines nếu relevant;
- V2 table count;
- legacy absence;
- migration metadata.

Expected: identical hoặc mọi difference phải được giải thích và fixed nếu là drift.

### 9.2. Pre-UP vs post-DOWN

So sánh exact compatibility schema. Nếu order/comment/default/index khác, không gọi identical.

### 9.3. Data fingerprints

Ít nhất:

- request counts/status/schema versions;
- campus instance counts/IDs;
- form detail counts/revisions;
- guest/member link counts;
- identity/amendment/history/audit counts;
- deterministic hashes/count tuples cho projection fields;
- orphan/cross-request counts = 0.

Không đưa raw PII vào committed evidence.

---

## 10. Regression gates sau latest commits/candidate fixes

### 10.1. Backend

Chạy và báo counts thực:

- targeted photo/file validation tests;
- full Unit, expected baseline tối thiểu `530/530` nếu không có tests mới;
- Architecture, last baseline `14/14`;
- guest-search security IT;
- targeted V2 suite, last baseline `45/45`;
- E2E auth guard tests;
- SQL/drill tests mới nếu thêm automation.

### 10.2. Full Integration

Audit cách full suite chọn database.

- Ưu tiên chạy trên fresh disposable `pems_i_*` hoặc `pems_it_regression` nếu có cách cấu hình chuẩn.
- Không recreate/mutate `pems_pr3_test`.
- Không mass-edit 25 hardcoded test files.
- Không đổi protected DB để match master.
- Nếu có thể dùng isolated MySQL instance/container mà hardcoded database name tồn tại chỉ bên trong isolated server, phải chứng minh không phải protected server trước khi chạy.
- Nếu vẫn blocked, báo exact failing tests/schema mismatch và last-known clean count; không tuyên bố pass.

### 10.3. Frontend

- install chỉ khi cần; nếu `npm ci` gặp known Windows native lock, dùng documented `npm install --legacy-peer-deps` và báo đúng.
- `npm run test:unit` — last baseline `99`;
- `npm run lint`/`tsc --noEmit`;
- `npm run build`;
- browser-contract suite, last explicitly reported `78`, nếu harness/dependencies khả dụng.

### 10.4. Real-stack

Rerun vì R1 thay đổi search behavior:

- API-level A–H: expected `8`;
- full DOM workflows: expected `9`;
- total expected `17`;
- no network mock;
- search hidden-campus journey phải chứng minh guest exclusion không làm regression scalar/delegation matches;
- trace OFF, no HAR, no secret logs;
- disposable DB only.

Nếu suite count thay đổi hợp lệ, giải thích added/removed tests. Không hardcode success.

---

## 11. Candidate code/doc updates

Nếu review/drill phát hiện lỗi:

- sửa candidate nhỏ nhất nhưng đầy đủ;
- thêm drill automation/tests để regression-proof;
- update README exact commands/outcomes;
- update zero-unclassified audit report;
- update `FINAL_IMPLEMENTATION_REPORT.md` và `IMPLEMENTATION_PROGRESS.md` theo conventions;
- không sửa bốn untracked handoff docs;
- không promote candidate vào runtime/master production while readiness FAIL.

Không commit raw logs, credentials, database dumps có PII hoặc temp evidence.

---

## 12. Commit strategy

Không commit trước khi logical slice và gates tương ứng hoàn tất.

Khuyến nghị:

1. Candidate SQL hardening + audit map.
2. Drill automation/evidence docs.
3. Final report/progress update.

Có thể gộp 1–2 nếu tightly coupled và diff vẫn reviewable. Không gộp unrelated production code.

Trước commit:

- `git diff --check`;
- review full diff;
- explicit stage paths;
- verify cached name-status;
- verify no untracked handoff docs staged;
- run relevant gates;
- set author/committer per commit bằng local command options, không global config;
- scan message for prohibited attribution.

Không push/merge/PR.

---

## 13. Cleanup bắt buộc

Cuối phiên, dù pass/fail:

- stop test API/Vite/MySQL/container processes do phiên tạo;
- không kill user dev server;
- drop đúng explicit disposable DBs;
- validate targets before drop;
- remove disposable data dirs, temp SQL, publish dirs, profiles, OTP inbox, logs, screenshots/traces;
- unlink junction safely, never recurse into junction target;
- restore appsettings byte-exact;
- flags default OFF;
- verify no secrets/PII artifacts;
- verify protected DBs untouched;
- verify no leaked test rows where sanctioned rollback tests ran;
- `git status`: intended tracked state + đúng bốn untracked handoff docs.

“Clean” phải diễn đạt là “tracked tree clean; four expected untracked docs remain” nếu docs vẫn tồn tại.

---

## 14. Stop conditions

Dừng và báo evidence, không đoán, nếu:

- auto-merge mới overlap candidate/source đang sửa;
- current master/migration chain không xác định;
- exact definitions/dependencies của 10 columns không xác định;
- allowlist không thể làm fail-closed;
- MySQL client tiếp tục sau `SIGNAL` và orchestration không kiểm soát được;
- guard có thể fail sau first DDL;
- safe disposable MySQL infrastructure không thể dựng;
- chỉ protected DB mới chạy được test;
- DOWN projection rule mâu thuẫn canonical source;
- schema diff có unexplained drift;
- user changes overlap;
- cleanup target không explicit;
- candidate vô tình được runtime/startup gọi.

Trước khi dừng, hoàn tất read-only audit, rollback local temporary config và cleanup những gì có thể.

---

## 15. Definition of Done cho phiên này

Phase I preparation chỉ DONE khi tất cả điều sau có evidence:

- zero-unclassified map hoàn chỉnh;
- candidate review hoàn chỉnh;
- exact allowlist guard tested, gồm underscore-wildcard negatives;
- preflight read-only proven;
- UP default-deny proven;
- all preconditions before first DDL proven;
- verify proven;
- DOWN restore-compatibility proven;
- `pems_i_fresh` PASS;
- `pems_i_upgrade` PASS;
- `pems_i_refusal` PASS với no partial DDL;
- `pems_i_rollback` PASS;
- fresh-vs-upgrade diff PASS;
- pre-UP-vs-post-DOWN diff PASS hoặc explained intentional deltas;
- Unit/Architecture/targeted V2 green;
- frontend gates green;
- real-stack rerun green;
- full Integration PASS hoặc honest environment blocker documented, không protected mutation;
- cleanup/hygiene verified;
- reports updated;
- commits đúng author/no AI/no push.

Nếu drills NOT RUN thì Phase I preparation không DONE, dù scripts đã tồn tại.

---

## 16. Báo cáo cuối phiên bắt buộc

Báo cáo tiếng Việt theo đúng thứ tự:

1. Branch/HEAD/upstream/ahead-behind.
2. Auto-push/auto-merge trong phiên.
3. Preflight/divergence/fate của checkpoints.
4. Candidate files reviewed và diff/fixes từng file.
5. Database allowlist implementation + underscore-wildcard tests.
6. Zero-unclassified audit: total matches, exclusions và counts từng category.
7. Runtime read/write blocker map.
8. Readiness matrix PASS/FAIL.
9. MySQL infrastructure/version/sql_mode/charset/timezone.
10. `pems_i_fresh`: commands, exit codes, result.
11. `pems_i_upgrade`: commands, exit codes, result.
12. `pems_i_refusal`: từng case + no-partial-DDL evidence.
13. `pems_i_rollback`: UP/DOWN/data/schema result.
14. Fresh-vs-upgrade schema diff.
15. Pre-UP-vs-post-DOWN schema diff.
16. Data fingerprints/no orphan evidence.
17. Unit/Architecture/targeted/full Integration counts.
18. Vitest/browser-contract/real-stack counts.
19. TypeScript/lint/build.
20. Candidate/readme/audit/report files changed.
21. Disposable DBs/processes/artifacts cleanup.
22. Protected DB hygiene.
23. appsettings restore.
24. Feature flags final.
25. Git status + four untracked docs.
26. Commits SHA/message/files.
27. Author/committer/no-AI verification.
28. No push/merge/PR confirmation.
29. Done/Deferred/Limitations.
30. Program status và exact resume point.

Mỗi suite/drill phải ghi một trong:

- PASS;
- FAIL;
- NOT RUN;
- BLOCKED;
- last-known clean only.

Không dùng “liên quan đều pass” thay cho tên suite/count.

---

## 17. Kết luận và trạng thái cuối hợp lệ

Nếu tất cả candidate drills xanh nhưng runtime readiness vẫn FAIL, dùng kết luận:

> Phase I contract-drop candidate is prepared and tested on disposable databases only. Execution is NOT READY because V1 fallback, default-OFF flags, legacy runtime readers/writers and/or persisted V1 data remain. No protected, staging or production database was modified. No destructive migration was wired or executed outside disposable drills.

Program status vẫn là `IN PROGRESS` hoặc `PHASE I PREPARATION DONE / EXECUTION NOT READY`, tùy convention report hiện có. Không gọi production cutover FINAL.

Nếu bất kỳ drill/schema diff quan trọng nào chưa chạy hoặc fail:

> Phase I candidate remains DRAFT/INCOMPLETE. Do not call it prepared/tested.

Exact resume point phải nêu case/file/gate cụ thể, không ghi chung chung “tiếp tục test”.
