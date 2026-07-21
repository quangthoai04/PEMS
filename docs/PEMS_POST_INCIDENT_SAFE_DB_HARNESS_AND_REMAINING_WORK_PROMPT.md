# PEMS — Prompt tiếp tục code sau sự cố import nhầm `pems_db`

> Đây là prompt thực thi tự chứa cho AI coding agent/developer tiếp theo. Phải trực tiếp kiểm tra local repository, bảo toàn history và dữ liệu người dùng, sửa import/drill harness trước mọi thao tác MySQL tiếp theo, viết tests, chạy các gate khả dụng và tiếp tục các workstream còn lại. Không chỉ lập kế hoạch. Không được tự ý phục hồi binlog, không được che giấu incident và không được dùng một guard chưa test để tiếp tục chạy destructive drills.

## 1. Vai trò và mục tiêu

Bạn là **Senior Software Architect + Senior Full-stack Engineer + Database Safety/Recovery Engineer + Test Engineer** tiếp tục chương trình **PEMS Per-Campus Form V2** trên repository `quangthoai04/PEMS`, branch local `Canh-Iter1` / remote `origin/Cảnh-Iter1`.

Mục tiêu theo thứ tự bắt buộc:

1. đóng incident ở mức evidence, không thực hiện recovery;
2. sửa drill/import harness để raw SQL không thể chuyển target sang protected database;
3. chứng minh harness từ chối payload nguy hiểm **trước khi mysql process được gọi**;
4. chỉ sau safety gate mới tiếp tục Workstream B negative parity/refusal drills;
5. hoàn thiện F5 exact manifest, R6 zero-unclassified appendix và deterministic fresh target;
6. giữ những phần code/tests đã hoàn thành trước incident;
7. không mutate protected databases thêm lần nữa;
8. báo cáo trung thực, không overclaim.

Không push, merge, deploy hoặc bật production flags nếu người dùng chưa yêu cầu rõ.

---

## 2. Sự cố đã xác nhận và quyết định của chủ dự án

### 2.1 Incident

Trong lúc dựng Workstream B parity fixture, master SQL:

`PEMS_FULL_V11_REMOVED_TTS_19_07_26.sql`

được truyền vào mysql với intended target `pems_i_refusal`. Tuy nhiên dump chứa top-level statements:

```sql
CREATE DATABASE IF NOT EXISTS pems_db;
USE pems_db;
```

`USE pems_db` đã thay đổi default database bên trong session. Các DROP/CREATE/seed sau đó chạy vào `pems_db`, còn `pems_i_refusal` rỗng.

### 2.2 Scope đã xác nhận

- `pems_db` hiện là exact master-seed state theo báo cáo incident.
- `pems_test` không tồn tại trên máy.
- `pems_pr3_test` không bị chạm.
- Chủ dự án xác nhận `pems_db` trước incident **không có dữ liệu nhập thủ công hoặc dữ liệu không thể tái tạo**.
- Vì vậy **không thực hiện binlog PITR**.
- Không replay binlog, không restore đè, không `RESET MASTER`, không `PURGE BINARY LOGS`.
- Giữ binlogs hiện có cho tới khi incident report và harness fix hoàn tất.
- Trạng thái master seed hiện tại của `pems_db` được chủ dự án chấp nhận, nhưng `pems_db` vẫn là protected database cho mọi phiên tiếp theo.

Không được hỏi lại recovery trừ khi phát hiện evidence mới cho thấy có dữ liệu không tái tạo.

### 2.3 Root cause

Root cause không chỉ là “quên đọc header”. Safety design đã sai vì:

- runner tin database truyền trên command line là target cuối cùng;
- exact connection allowlist không ngăn `USE` trong payload;
- raw dump được pipe/import mà không có statement-aware safety scan;
- mysql credential có quyền trên protected database;
- disposable drills chạy cùng MySQL server chứa protected databases;
- không có regression test chứng minh unsafe payload bị chặn trước process spawn.

Phải sửa theo defense-in-depth, không chỉ xóa hai dòng trong một file.

---

## 3. Repository checkpoint và phần phải bảo toàn

Remote checkpoint đã review trước đó:

- `d64bde66973caaf9765070e529cb1599743ec03d`

Local sequence trước incident từng có:

- `494bbdf5`
- `4b6735b1`
- `5296ad4a`

Work hoàn thành sau đó theo bàn giao:

- `fd48fcc2` — C1/C2 behavioral regressions, reported 20/20 trên real MySQL/Pomelo; Unit 530/530; Architecture 14/14.
- `5f422fdb` — image-only upload toast đổi thành `ảnh`; reported tsc clean, Vitest 99/99, build pass.

Ngoài ra:

- `bcf5b500` được báo cáo là commit của người khác đã track thư mục `docs/ChangeSauHopChiQUyen/sauhop_13-07/gemin/`.
- Không revert/xóa/sửa commit hoặc thư mục này nếu không thuộc scope.
- Có file untracked mới:
  `PEMS_FULL_V11_ACTOR_RELATION_SEED_FIXED_20_07_26.sql`.
- File này không phải do agent incident tạo. Phải giữ nguyên, không commit, không import. Chỉ được scan read-only và báo hazard.

### 3.1 Checkpoint commands

Trước khi sửa code:

```bash
git status --short --branch
git remote -v
git rev-parse HEAD
git rev-parse origin/Cảnh-Iter1
git merge-base d64bde66973caaf9765070e529cb1599743ec03d HEAD
git log --oneline --decorate --graph -30
git diff --stat d64bde66973caaf9765070e529cb1599743ec03d..HEAD
git diff --check d64bde66973caaf9765070e529cb1599743ec03d..HEAD
```

Kiểm từng expected commit nếu tồn tại:

```bash
git cat-file -t fd48fcc2
git cat-file -t 5f422fdb
git cat-file -t bcf5b500
git show --stat --oneline fd48fcc2
git show --stat --oneline 5f422fdb
git show --stat --oneline bcf5b500
```

Quy tắc:

- Không reset về remote.
- Không amend/squash/rewrite commit cũ.
- Nếu HEAD đã đi tiếp, audit mọi commit mới.
- Không xóa untracked SQL hoặc thay đổi người dùng.
- Nếu expected hash không tồn tại, không giả định nội dung; dùng history/diff thật.
- Không chạy bất kỳ command MySQL mutation nào trong checkpoint phase.

---

## 4. Protected resources — khóa cứng

Protected databases:

- `pems_db`
- `pems_test`
- `pems_pr3_test`

Exact Phase I disposable allowlist:

- `pems_i_fresh`
- `pems_i_upgrade`
- `pems_i_refusal`
- `pems_i_rollback`

Quy tắc:

- Không create/drop/alter/insert/update/delete/truncate trên protected databases.
- Không dùng root/admin credential cho drills sau khi harness mới được triển khai.
- Không chạy raw `mysql < master.sql` hoặc pipe raw dump vào mysql.
- Không import file untracked mới.
- Không chạy migration script trên `pems_it_regression`; DB đó chỉ có thể dùng cho integration harness nếu config cho phép và phải cleanup.
- Mọi disposable mutation phải đi qua wrapper/harness đã safety-validated.
- Nếu không có restricted MySQL credential hoặc isolated server, dừng **chỉ DB execution**, ghi blocker và tiếp tục code/static tests/R6. Không quay lại root import.

---

## 5. Workstream S0 — incident closure và evidence

Tạo hoặc cập nhật một incident artifact ngắn, không chứa secret/raw sensitive log, ví dụ:

`docs/ChangeSauHopChiQUyen/sauhop_13-07/PHASE_I_DB_IMPORT_INCIDENT_2026_07_20.md`

Nội dung tối thiểu:

- thời điểm phát hiện;
- intended target và actual target;
- exact hazardous statements;
- protected DB affected;
- scope/data-loss assessment;
- chủ dự án xác nhận không có irreproducible data;
- recovery decision: `NO PITR — current master seed accepted`;
- DBs không bị chạm;
- work stopped point;
- root cause theo nhiều lớp;
- corrective/preventive actions;
- binlogs retained, not replayed/purged;
- link tới harness tests/evidence sau khi hoàn thành.

Không dùng incident report để đổ lỗi cá nhân. Ghi technical cause và control gaps.

Cập nhật database touch ledger trung thực:

- `pems_db`: overwritten by master import, accepted by owner because reproducible;
- `pems_i_refusal`: intended target nhưng empty;
- `pems_pr3_test`: untouched;
- `pems_test`: absent;
- recovery: not attempted/not required.

---

## 6. Workstream S1 — thiết kế safe SQL import pipeline

Phải có hai component tách biệt:

1. **Raw Import Guard** — luôn hard-fail với dangerous input; không mutate/rewrite file.
2. **Explicit Disposable Fixture Transformer** — chỉ dùng cho authoritative known source, statement-aware, có exact assertions và tạo artifact mới; raw source không bao giờ được import trực tiếp.

### 6.1 Raw Import Guard

Trước khi spawn `mysql`, parse/tokenize SQL đủ để phân biệt:

- executable statement;
- quoted string/identifier;
- line/block/versioned comments;
- delimiter/routine body;
- mysql client meta-command.

Không dùng một regex đơn để “bảo vệ”. Regex có thể là lớp phụ, không phải parser/semantic gate duy nhất.

Hard-fail nếu executable input chứa, case/whitespace/comment-obfuscated variants:

#### Database-control statements

- `CREATE DATABASE` / `CREATE SCHEMA`;
- `ALTER DATABASE` / `ALTER SCHEMA`;
- `DROP DATABASE` / `DROP SCHEMA`;
- `USE <db>`;
- `CREATE/DROP/ALTER TABLE` fully qualified tới protected schema;
- bất kỳ fully-qualified read/write/object reference tới:
  - `pems_db`;
  - `pems_test`;
  - `pems_pr3_test`.

#### Server/global/admin statements

- `CREATE USER`, `ALTER USER`, `DROP USER`;
- `GRANT`, `REVOKE`;
- `SET GLOBAL`, `SET PERSIST`;
- `RESET MASTER`, `RESET REPLICA`;
- `PURGE BINARY LOGS`;
- `SHUTDOWN`;
- plugin/component install/uninstall;
- replication/source configuration;
- mysql client `SOURCE` / `\.` includes;
- dynamic SQL that constructs protected database names or database-control statements when deterministically detectable.

Nếu parser không thể phân loại statement an toàn, fail closed với exact reason. Không cho unknown statement tiếp tục chỉ vì không match denylist.

### 6.2 Do not silently strip

Raw importer không được:

- silently remove `USE pems_db`;
- silently remove `CREATE DATABASE`;
- search/replace `pems_db` thành target;
- tiếp tục khi match count khác expected;
- ghi đè source file;
- tạo half-sanitized artifact sau failure.

Unsafe raw source phải trả:

- exit nonzero;
- file hash;
- exact statement type/location;
- mysql process invoked = `NO`;
- zero database mutation.

### 6.3 Explicit fixture transformer

Để dùng authoritative master làm disposable fixture, tạo explicit transformer với contract:

- chỉ nhận exact known master source path/kind;
- compute SHA-256 và structural fingerprint;
- parse top-level SQL statements;
- assert exactly expected database header statements;
- transform/remove chúng có chủ đích vào **new temp/output artifact**;
- reject bất kỳ database-control statement bổ sung;
- reject remaining protected fully-qualified references;
- preserve strings/comments/data values byte-semantically trừ expected transformed statements;
- emit transformation manifest:
  - source path/hash;
  - removed/transformed statement IDs;
  - target DB;
  - output hash;
  - statement counts;
- generate atomically: validate temp output rồi mới publish;
- same input + target → same output hash;
- source drift → nonzero/no output replacement.

Không gọi đây là “strip header”. Đây là asserted statement transformation.

### 6.4 Runtime target assertions

Wrapper phải:

- exact target `ValidateSet`/allowlist;
- resolve paths bằng script directory, không CWD;
- không log password;
- connect bằng explicit target DB;
- chạy `SELECT DATABASE()` trước import và yêu cầu exact target;
- chạy safe transformed artifact trong controlled session;
- artifact không được có `USE` hoặc database-control statements;
- chạy `SELECT DATABASE()` sau import và yêu cầu exact target;
- verify expected target tables/data;
- confirm protected DB fingerprints unchanged;
- capture native mysql exit code;
- cleanup temp artifact trong success/failure.

Chỉ `SELECT DATABASE()` không đủ; nó là một lớp sau static/semantic guard.

### 6.5 Least-privilege credential

Drill runner phải từ chối credential có global/admin hoặc protected DB privileges.

Trước mutation:

- inspect `CURRENT_USER()`;
- inspect `SHOW GRANTS FOR CURRENT_USER()`;
- reject root/admin/global `ALL PRIVILEGES`;
- require grants chỉ trên exact disposable target cần dùng;
- reject privilege trên protected schemas.

Không tự `CREATE USER`/`GRANT` trên shared server. Nếu restricted credential chưa tồn tại:

- tạo setup SQL/documentation để chủ dự án chạy khi họ cho phép; hoặc
- dùng isolated disposable MySQL instance nếu môi trường đã có;
- nếu cả hai không có, mark DB drills `BLOCKED — restricted execution environment unavailable`.

Không fallback về root.

### 6.6 Isolated server preference

Ưu tiên chạy drills trên MySQL server/process/container/instance tách biệt, không chứa protected databases. Nếu không có, restricted credential + semantic import guard + protected fingerprint checks là tối thiểu bắt buộc.

Không tự cài Docker hoặc thay đổi hạ tầng máy nếu chưa được cho phép.

---

## 7. Workstream S2 — regression tests cho incident class

Tests phải chứng minh unsafe payload bị chặn **trước process spawn**. Dùng fake mysql executable/process spy hoặc abstraction có thể assert invocation count.

### 7.1 Required unsafe fixtures

Tạo test fixtures nhỏ, không dùng protected DB thật:

1. exact incident:

```sql
CREATE DATABASE IF NOT EXISTS pems_db;
USE pems_db;
DROP TABLE IF EXISTS visit_requests;
```

2. mixed case/whitespace/comments:

```sql
UsE /* incident */ `pems_db`;
```

3. protected qualified identifiers:

```sql
DROP TABLE `pems_db`.`visit_requests`;
INSERT INTO pems_test.users VALUES (...);
```

4. alternate protected DB `pems_pr3_test`;
5. `DROP DATABASE` / `ALTER DATABASE`;
6. mysql client `SOURCE` / `\.`;
7. `SET GLOBAL`, `GRANT`, user-management and binlog purge/reset;
8. dangerous statement hidden after many valid statements;
9. dangerous tokens in plain comments/string literals that should not become false executable positives, while dynamic SQL constructing protected statements must be dispositioned fail-closed;
10. delimiter/routine fixture;
11. UTF-8/BOM and Windows line endings;
12. source file changed after scan but before invocation — protect against TOCTOU by importing validated bytes/hash, not rereading untrusted path.

Expected for every unsafe executable fixture:

- validation FAIL;
- exit nonzero;
- exact reason/location;
- mysql invocation count = 0;
- no output artifact replacement;
- no DB connection needed.

### 7.2 Safe fixtures

Tests phải cho phép intended target-scoped SQL, ví dụ unqualified DDL/DML after wrapper selects exact disposable DB, nhưng vẫn validate:

- safe source hash;
- no database control;
- no protected qualification;
- expected statement count;
- output reproducibility.

### 7.3 Transformer tests

- exact known header transforms once;
- zero/multiple header matches fail;
- additional `USE` later fails;
- remaining `pems_db.table` fails;
- same-name literal/comment not accidentally mutated;
- source drift fails;
- repeated run identical output hash;
- failure leaves previous artifact unchanged;
- raw importer still rejects original master.

### 7.4 Scan untracked actor-relation SQL

Read-only scan:

`PEMS_FULL_V11_ACTOR_RELATION_SEED_FIXED_20_07_26.sql`

Requirements:

- compute hash;
- detect/report database-control/protected references;
- do not import;
- do not modify;
- do not commit;
- classify `SAFE FOR DIRECT IMPORT = NO` nếu có `CREATE DATABASE`, `USE` hoặc protected qualifications.

Không kết luận ownership hoặc xóa file.

---

## 8. Safety gate trước khi resume MySQL drills

Không được chạy Workstream B DB mutations cho tới khi tất cả điều sau đạt:

- incident fixture rejected;
- mysql invocation count zero on unsafe input;
- transformer tests pass;
- raw master rejected by raw importer;
- transformed artifact has manifest/hash;
- restricted credential/isolated server available;
- runner proves current user lacks protected/global privileges;
- protected fingerprint snapshot captured read-only;
- exact target `DATABASE()` pre/post checks implemented;
- cleanup path tested;
- incident report updated.

Nếu gate chưa đạt, tiếp tục F5 static code, R6 appendix và unit tests nhưng không chạy destructive MySQL.

---

## 9. Work đã hoàn thành trước incident — audit, không làm mất

### 9.1 Commit `fd48fcc2`

Audit exact diff và tests. Reported evidence:

- C1/C2 behavioral regressions 20/20 trên real MySQL/Pomelo;
- reverting C1 fix làm uniform-v2 trả `STALE_GLOBAL_DELEGATION`;
- reverting C2 làm filter over-match và under-match;
- Unit 530/530;
- Architecture 14/14.

Không tin report mù. Xác nhận:

- V1 parity;
- uniform V2 stale projection;
- mixed V2;
- missing detail;
- auth/scope;
- provider translation;
- no N+1/client-side full-table load.

Nếu evidence/tests đúng, giữ commit và mark F2/F6 `VERIFIED`. Không viết lại test chỉ để đổi style.

### 9.2 Commit `5f422fdb`

Audit:

- upload contract image-only 5 MB;
- success toast chỉ nói `ảnh`;
- historical video preview có thể giữ;
- không mở lại video upload;
- frontend gates thật sự pass.

Nếu đúng, giữ commit và mark F9 `VERIFIED`.

### 9.3 Commit `bcf5b500`

Chỉ audit ownership/scope. Không revert `gemin/`, không sửa nội dung nếu không cần cho task này. Ghi đây là tracked pre-existing/other change theo evidence Git.

---

## 10. Resume Workstream B — negative parity/refusal matrix

Chỉ chạy sau Safety Gate §8.

### 10.1 Parity mismatch đủ 10 fields

Trên disposable fixture được tạo qua safe transformer/harness, parameterize:

- delegation_name;
- visit_type;
- visit_type_other;
- purpose;
- working_content;
- working_language;
- transportation_note;
- media_consent_status;
- media_consent_note;
- note_to_fptu.

Với từng field:

1. fixture ban đầu PASS;
2. mutate đúng một global projection value trên disposable DB;
3. preflight phải FAIL;
4. exact mismatch count/field evidence;
5. payload not invoked;
6. exit nonzero;
7. target schema/data fingerprint unchanged bởi attempted UP;
8. protected DB fingerprints unchanged.

Thêm NULL-vs-empty, NULL-vs-whitespace, selected smallest-campus determinism, missing detail, no projectable request và NULL schema version.

### 10.2 UP/DOWN refusal matrix

UP:

- missing flag/override;
- wrong lifecycle;
- non-v2/NULL version;
- missing/duplicate/orphan detail;
- column/comment/default/ordinal drift;
- index/FULLTEXT/CHECK drift;
- external dependency drift;
- flags tự set nhưng prerequisites sai.

DOWN:

- called on pre-UP state;
- missing flag;
- post-UP manifest drift;
- canonical mandatory NULL;
- missing/duplicate/orphan detail;
- restore object conflicts;
- invalid verify/preflight mode;
- flags tự set nhưng prerequisites sai.

Mọi refusal:

- first DDL not executed;
- runner/mysql nonzero;
- disposable fingerprint unchanged;
- protected fingerprints unchanged;
- cleanup pass.

Không dùng `pems_db` làm baseline/refusal target.

---

## 11. Workstream F5 — exact schema/dependency manifest

Tiếp tục phần còn thiếu từ previous prompt:

- exact column name/order/type/length/enum/null/default/comment/charset/collation/extra;
- exact secondary index name, uniqueness, type, visibility, ordered members, `SEQ_IN_INDEX`, prefix/collation;
- exact FULLTEXT ordered member list trước/sau;
- deterministic normalized CHECK expression fingerprint, không chỉ `LIKE`;
- exact unrelated CHECK expression set, không chỉ count;
- generated columns/FKs/views/triggers/routines/events dependency sweep;
- invalid mode fail;
- payload re-check critical state trước DDL;
- direct flags không bypass guards;
- drift regression cases.

Expected manifest phải lấy từ authoritative master/code, không đoán.

F5 chỉ `VERIFIED` khi implemented + drift-tested + normal lifecycle vẫn pass qua safe harness.

---

## 12. Workstream R6 — zero-unclassified appendix

Không trì hoãn vì DB gate. R6 phần lớn là read-only/code artifact và có thể tiếp tục ngay.

Recompute census tại HEAD cuối cho 10 PascalCase fields và snake_case identifiers trên:

- backend production;
- Domain/EF mapping;
- API DTO/serialization;
- SQL/master/candidate/scripts;
- tests/fixtures/seeds;
- frontend contracts;
- docs/comments/collisions.

Tạo reproducible appendix CSV/MD với:

- occurrence/group ID;
- field;
- exact file:line(s);
- raw-hit count;
- stable symbol/expression;
- entity/table;
- category;
- operation;
- caller/surface;
- V1/V2 behavior;
- blocker;
- required action;
- disposition;
- evidence/test.

Categories tối thiểu:

- V1-only runtime read/write;
- V2 dual-read/live fallback;
- compatibility projection write;
- canonical V2 read/write;
- ORM/entity/schema mapping;
- API/DTO serialization;
- SQL/migration/master;
- tests/fixtures/seeds;
- frontend;
- docs/comment;
- unrelated collision;
- dead/excluded với evidence.

Reconcile:

- raw total = appendix raw-hit sum;
- distinct files khớp;
- field/category/blocker totals khớp;
- zero blank/Various/Unclassified;
- blocker summary derived từ appendix;
- known sites có disposition.

Không gọi `zero-unclassified` trước khi reconciliation pass.

---

## 13. Workstream deterministic fresh target

Safe import incident làm yêu cầu fresh-target nghiêm ngặt hơn.

Không dùng blind regex. Fresh generator phải reuse statement-aware parser/transformer từ S1:

- exact source hash/shape assertions;
- transform only intended `visit_requests` schema/dependencies;
- remove 10 legacy columns đúng manifest;
- adjust indexes/FULLTEXT/CHECK statement-aware;
- seed transformation có canonical V2 proof hoặc fail;
- no database-control/protected refs in output;
- output reproducible hash;
- atomic publish;
- source drift and zero/multiple match tests;
- raw output passes Raw Import Guard;
- import only through restricted safe harness vào `pems_i_fresh`;
- exact fresh verify;
- protected fingerprints unchanged;
- cleanup disposable DB.

Nếu seed V1 không thể chuyển thành canonical V2 mà không đoán/fabricate, ghi exact blocker và vẫn hoàn thiện schema-only deterministic artifact/tests nếu contract cho phép. Không đổi `form_schema_version = 2` máy móc rồi gọi valid backfill.

---

## 14. F8 evidence và reporting

F8 phải có trong ledger, không bị bỏ sót.

Mọi test/drill ghi:

- command;
- HEAD SHA;
- timestamp/timezone;
- tool/MySQL version;
- target DB;
- current MySQL user privilege classification;
- source/transformed artifact hashes;
- pass/fail/skip;
- exit code;
- mysql invoked YES/NO;
- pre/post target fingerprint;
- pre/post protected fingerprints;
- cleanup result;
- limitations.

Local tests không phải GitHub CI. Vercel permission failure không phải code result.

Cập nhật:

- incident report;
- `PHASE_I_AUDIT_REPORT.md`;
- R6 appendix;
- candidate `README.md`;
- `IMPLEMENTATION_PROGRESS.md`;
- `FINAL_IMPLEMENTATION_REPORT.md`;
- drill evidence matrix.

Không dùng:

- “protected databases never touched” — incident đã xảy ra;
- “zero mutation” nếu không có fingerprints;
- “all prerequisites enforced” khi F5 partial;
- “enumerated exactly” khi R6 incomplete;
- “lossless” chỉ dựa happy path;
- “all green” nếu full IT/drills unavailable.

Status incident đúng:

> `INCIDENT CLOSED WITHOUT RECOVERY — pems_db was overwritten by reproducible master seed; owner confirmed no irreproducible data; no PITR performed; safe-import controls implemented/tested as listed.`

Status chương trình đúng nếu vẫn còn blocker:

> `IN PROGRESS — safe-import controls and available corrective work completed to the evidenced gate; contract-drop NOT READY; remaining audit/fresh/runtime/data/cutover blockers listed exactly where evidence permits.`

---

## 15. Test gates

Sau từng slice chạy targeted tests; cuối phiên chạy available regressions:

- safe SQL parser/guard unit tests;
- process-not-invoked tests;
- transformer golden/drift/TOCTOU tests;
- C1/C2 behavioral integration tests;
- backend Unit;
- Architecture;
- targeted MySQL/Pomelo integration;
- negative migration/refusal matrix sau safety gate;
- frontend Vitest/tsc/build nếu frontend changed;
- fresh drill nếu restricted environment available.

Full IT:

- ưu tiên config override sang disposable `pems_it_regression` nếu harness hỗ trợ;
- không create/mutate `pems_test`;
- nếu không override an toàn, `NOT RUN` với blocker.

Không reuse counts cũ như result HEAD mới nếu chưa rerun.

---

## 16. Commit strategy

Không sửa history cũ. Gợi ý commits mới:

1. `fix(database): reject unsafe SQL imports before mysql execution`
2. `test(database): cover protected-database import escape vectors`
3. `docs(database): record the accepted pems_db import incident`
4. `test(database): prove Phase I refusal paths are zero-mutation`
5. `fix(database): enforce exact Phase I schema manifests`
6. `fix(audit): complete legacy-field occurrence dispositions`
7. `fix(database): generate deterministic safe fresh target`
8. `docs(database): record exact guarded drill evidence`

Gom parser + tests hợp lý nếu cùng atomic safety slice; tránh commit một file lẻ khi cùng semantic fix cần nhiều file.

Không commit:

- untracked actor-relation SQL;
- protected DB dump;
- raw binlogs;
- secret/credential;
- huge raw logs;
- unrelated `gemin/` changes;
- AI attribution.

Không push/merge/deploy nếu chưa được yêu cầu.

---

## 17. Deliverables bắt buộc

Cuối phiên giao:

1. remote/local HEAD đầu-cuối, ahead/behind/divergence;
2. audit commits `fd48fcc2`, `5f422fdb`, `bcf5b500` nếu tồn tại;
3. findings ledger đầy đủ, gồm incident finding và F1–F10;
4. incident report path + recovery decision;
5. raw-import guard threat matrix;
6. unsafe-fixture tests với mysql invocation count;
7. transformer source/output hashes + manifest;
8. restricted credential/isolated environment proof hoặc blocker;
9. scan result của untracked actor-relation SQL, không import/modify;
10. C1/C2/F9 verification status;
11. 10-field negative parity matrix;
12. UP/DOWN refusal matrix;
13. F5 exact manifest coverage;
14. R6 census/appendix/reconciliation;
15. deterministic fresh result/drill hoặc exact blocker;
16. exact commands/test results at final HEAD;
17. DB touch ledger, gồm incident lịch sử và mọi disposable DB trong phiên;
18. protected DB fingerprint evidence sau harness tests;
19. cleanup proof;
20. remaining technical/environment/business blockers;
21. next executable slice;
22. `git status --short --branch`;
23. commits mới và push/merge/deploy status.

Không kết thúc bằng “Done” nếu safety gate, R6 hoặc fresh còn mở. Nêu rõ phần nào VERIFIED, phần nào BLOCKED/NOT RUN.

---

## 18. Definition of Done của prompt này

Prompt chỉ hoàn tất khi:

- không PITR/recovery/destructive action trên `pems_db`;
- incident được document trung thực và owner decision ghi rõ;
- raw importer hard-fail database escape/admin statements;
- exact incident fixture bị reject trước mysql spawn;
- parser handles comments/strings/delimiters/TOCTOU fixtures;
- master raw dump không thể import trực tiếp;
- explicit transformer có assertions, hashes và atomic output;
- restricted credential hoặc isolated server enforced trước DB drills;
- protected DB fingerprints không đổi trong mọi post-fix test/drill;
- C1/C2 tests và F9 commit được audit/verified;
- negative parity/refusal matrix chạy sau safety gate hoặc ghi blocker đúng;
- F5 exact manifest được implement + drift-tested;
- R6 appendix reconcile zero unclassified;
- deterministic fresh artifact pass guard, import và verify, hoặc exact non-guessable seed blocker được cô lập;
- F8 evidence đầy đủ;
- no protected DB/untracked user file/unrelated tracked content bị sửa;
- all available tests pass tại final HEAD;
- reports không xóa lịch sử incident hoặc overclaim;
- không push/merge/deploy ngoài thẩm quyền.

Ngay cả khi prompt này đạt DoD, contract-drop chỉ READY khi runtime V1 dependencies, persisted-data backfill, caller cutover và rollout gates riêng đã hoàn thành. Không suy rộng kết luận.
