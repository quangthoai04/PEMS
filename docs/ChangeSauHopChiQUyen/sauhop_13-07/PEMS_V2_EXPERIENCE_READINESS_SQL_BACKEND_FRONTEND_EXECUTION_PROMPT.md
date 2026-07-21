# PEMS — V2 Experience Readiness: Prompt thực thi SQL + Backend + Frontend

> Đây là prompt thực thi tự chứa dành cho AI coding agent tiếp theo. Mục tiêu là đưa PEMS Per-Campus Form V2 vào một môi trường review cô lập để chủ dự án có thể trải nghiệm sớm và đưa ra đánh giá thực tế. Phải trực tiếp kiểm tra repository, dựng dữ liệu review an toàn, chạy full stack, đi qua các hành trình người dùng thật và sửa lỗi chặn trải nghiệm. Không được chỉ lập kế hoạch hoặc chỉ sửa tài liệu.

---

## 1. Vai trò và mục tiêu ưu tiên

Bạn là **Senior Full-stack Engineer + Database Safety Engineer + Test Engineer** tiếp tục dự án PEMS:

- Repository: `quangthoai04/PEMS`
- Local branch dự kiến: `Canh-Iter1`
- Remote branch dự kiến: `origin/Cảnh-Iter1`
- Stack: .NET + EF Core/Pomelo + MySQL, React/Vite, Vitest và các test projects hiện có.

Mục tiêu duy nhất của lượt này là:

> **V2 EXPERIENCE READY** — SQL additive, backend và frontend phải chạy xuyên suốt trên một database review cô lập, với feature flags V2 chỉ bật trong môi trường review, có bộ dữ liệu đại diện đầy đủ và có bằng chứng browser/API/database cho các hành trình trọng yếu.

Đây **không phải** lượt contract-drop. Không cần drop 10 legacy columns để hoàn thành lượt này. Không để R6/F5/F7 kéo chậm khả năng trải nghiệm sản phẩm.

Ưu tiên theo thứ tự:

1. bảo toàn repository và gom commit hợp lý;
2. dựng môi trường review database an toàn;
3. tạo dữ liệu review có đủ vai trò và trường hợp nghiệp vụ;
4. chạy React → .NET API → MySQL thật với V2 flags bật riêng cho review;
5. sửa các lỗi SQL/backend/frontend chặn hành trình;
6. chạy regression gates và bàn giao checklist trải nghiệm;
7. báo cáo trung thực những gì chưa hoàn tất.

Không push, merge, deploy hoặc thay đổi production flags nếu chưa có yêu cầu rõ ràng của chủ dự án.

---

## 2. Trạng thái bàn giao phải hiểu đúng

### 2.1 Git checkpoint được báo cáo

- HEAD đầu phiên trước: `5f422fdb`
- HEAD cuối phiên trước: `056f18e4`
- Remote được báo cáo: `origin/Cảnh-Iter1 = c9791e32`
- Local được báo cáo `ahead 7, behind 0`
- Báo cáo đồng thời ghi merge-base `d64bde66`.
- Có hai prompt files untracked; phải bảo toàn.

Hai mô tả `ahead 7, behind 0` và `merge-base = d64bde66` có thể không đồng thời đúng nếu remote thật sự là `c9791e32`. Không được đoán. Phải kiểm tra lại bằng Git trước khi rewrite history hoặc tiếp tục code.

### 2.2 Công việc đã hoàn thành theo evidence gần nhất

- `fd48fcc2`: C1/C2 behavioral regressions, 20/20 trên MySQL/Pomelo; Unit 530/530; Architecture 14/14.
- `5f422fdb`: upload contract image-only và toast dùng từ `ảnh`; tsc/Vitest/build đã pass.
- `bcf5b500`: commit của tác giả khác đã track `gemin/`; không sửa/revert chỉ vì hygiene.
- Safe import controls đã triển khai:
  - `lib/SqlSafetyGuard.ps1`;
  - `import_disposable_fixture.ps1`;
  - tokenizer xử lý comments, strings, backticks, `DELIMITER`, executable versioned comments;
  - 50/50 local tests pass;
  - incident payload bị từ chối trước khi mysql được gọi;
  - master SQL được xác định `SAFE FOR DIRECT IMPORT = NO`.
- Incident report đã được tạo tại:
  `docs/ChangeSauHopChiQUyen/sauhop_13-07/PHASE_I_DB_IMPORT_INCIDENT_2026_07_20.md`.
- Incident được đóng **không recovery** vì chủ dự án xác nhận dữ liệu cũ có thể tái tạo.

### 2.3 Trạng thái chưa hoàn tất nhưng không phải trọng tâm lượt này

- Restricted MySQL credential: chưa được tạo/chạy bởi chủ dự án.
- F1/R6: per-occurrence disposition chưa hoàn tất.
- F5: exact manifest depth còn partial.
- F7: deterministic fresh-target generator chưa hoàn tất.
- 10-field negative parity và UP/DOWN refusal matrices chưa chạy vì thiếu môi trường hạn quyền.
- F10 search và cách hiển thị request-level cho mixed-campus cần quyết định nghiệp vụ.
- Contract-drop vẫn `NOT READY`.

Không được đổi các trạng thái này thành COMPLETE chỉ vì môi trường review chạy được.

---

## 3. Thứ tự nguồn sự thật

Khi tài liệu và code mâu thuẫn, dùng thứ tự:

1. database schema thực tế của **review database** và current source code;
2. tests đang chạy được;
3. canonical business rules, permission rules và use-case documents;
4. incident report và audit reports mới nhất;
5. progress/handoff documents cũ.

Đọc trước khi sửa:

- `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`
- `VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md`
- `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`
- `PERMISSION_MATRIX.md`
- `PERMISSION_RULES.md`
- `PEMS_PER_CAMPUS_V2_MASTER_HANDOFF_PROMPT*.md`
- current `PHASE_I_AUDIT_REPORT.md`
- current incident report
- current SQL safety guard, importer và tests
- current feature-flag bindings trong backend và capability handling trong frontend.

Không sao chép mù từ tài liệu cũ. Xác minh tên file, route, option class, config key, schema và command bằng code thật tại HEAD.

---

## 4. Phase E0 — Git safety và semantic commit consolidation

### 4.1 Kiểm tra bắt buộc

Chạy và lưu kết quả:

```bash
git status --short --branch
git remote -v
git fetch origin
git rev-parse HEAD
git rev-parse origin/Cảnh-Iter1
git merge-base HEAD origin/Cảnh-Iter1
git rev-list --left-right --count origin/Cảnh-Iter1...HEAD
git log --oneline --decorate --graph -35
git diff --check
```

Không reset về remote. Không xóa untracked files của người dùng.

### 4.2 Gom commit hiện tại

Chủ dự án yêu cầu tránh commit quá nhỏ. Nếu và chỉ nếu tất cả điều kiện sau đúng:

- các commit `9bcc5b97`, `8d910d74`, `2191bdb1`, `a65f29a4`, `056f18e4` tồn tại, liên tiếp và chưa push;
- không có commit của người khác xen giữa;
- working tree sạch ngoài hai prompt files đã biết;
- đã tạo local backup ref;
- quan hệ local/remote đã được xác minh, không còn mâu thuẫn;

thì được rewrite **chỉ năm commit này** thành khoảng hai semantic commits:

1. `fix(database): make disposable SQL imports fail closed`
   - guard, importer, transformer, fixtures, tests, fake mysql spy, restricted credential helper và incident evidence trực tiếp liên quan.
2. `audit(database): rebuild Phase I evidence and legacy-field census`
   - census tool, audit corrections, ledger/progress/report updates.

Không rewrite:

- `fd48fcc2`;
- `5f422fdb`;
- `bcf5b500`;
- `c9791e32`;
- bất kỳ commit đã push hoặc commit của tác giả khác.

Không force-push. Sau regroup phải chứng minh tree trước/sau byte-equivalent bằng tree hash hoặc empty diff. Nếu điều kiện không đủ, bỏ qua regroup và chỉ báo lý do; không để việc này chặn code trải nghiệm.

### 4.3 Commit hygiene cho lượt mới

- Không commit sau mỗi file.
- Implementation, tests, fixture và tài liệu trực tiếp chứng minh cùng behavior phải ở cùng commit.
- Mỗi workstream ưu tiên 1–3 semantic commits.
- Một-file commit chỉ hợp lệ nếu thay đổi độc lập và có thể review/revert riêng.
- Không có `Claude`, `AI`, `Co-authored-by AI` hoặc AI attribution trong commit metadata/message.
- Dùng đúng author đã cấu hình: không tự thay danh tính Git.

---

## 5. Database safety contract — không được vi phạm

### 5.1 Protected databases

Tuyệt đối không mutate:

- `pems_db`
- `pems_test`
- `pems_pr3_test`

`pems_db` đã từng bị overwrite bởi incident và trạng thái master seed hiện tại được chủ dự án chấp nhận, nhưng vẫn là protected database. Không dùng nó làm database trải nghiệm mặc định.

Không PITR, replay/purge/reset binlog hoặc recovery work trong lượt này.

### 5.2 Review database riêng

Tên khuyến nghị:

`pems_review_v2`

Đây là allowlist riêng cho **review application**, không được thêm tên này vào Phase I destructive runner một cách cơ học. Phase I runner vẫn giữ exact four-name allowlist hiện có.

Agent không được dùng `root` hoặc admin credential để tự tạo database/user. Nếu review DB hoặc restricted user chưa tồn tại:

1. tạo một bootstrap SQL ngắn cho chủ dự án review và chạy thủ công;
2. script phải chỉ tạo đúng `pems_review_v2` và đúng restricted review user;
3. grants chỉ được áp dụng cho `pems_review_v2.*`;
4. không grant global, `WITH GRANT OPTION`, `FILE`, `SUPER`, `SYSTEM_USER`, replication hoặc quyền trên protected DB;
5. không hardcode/commit password;
6. không tự chạy bootstrap bằng root.

Nếu manual bootstrap chưa được thực hiện, tiếp tục mọi code/static/test không cần DB và bàn giao **một manual unblock action** rõ ràng. Không quay lại root để “đẩy nhanh”.

### 5.3 Pre-mutation proof

Trước lần mutation đầu tiên trên review DB, phải chứng minh bằng read-only checks:

- `SELECT DATABASE()` đúng `pems_review_v2`;
- `CURRENT_USER()` không phải root/admin;
- grants chỉ cho phép review database theo thiết kế;
- không có quyền trên ba protected DB;
- target database là empty hoặc fingerprint hiện tại đã được chủ động xác nhận có thể reset;
- raw master SQL đã bị guard đánh dấu unsafe và không được direct import;
- transformed fixture có expected source hash, output hash, transformation manifest và scan PASS;
- mysql invocation chỉ xảy ra sau tất cả gate.

Nếu bất kỳ proof nào fail: không spawn mysql mutation process.

### 5.4 Cấm tuyệt đối

- raw `mysql < master.sql`;
- pipe raw dump vào mysql;
- `USE pems_db` hoặc bất kỳ protected DB;
- `CREATE/DROP/ALTER DATABASE` trong application fixture payload;
- silently strip/rewrite dangerous SQL mà không có asserted transformer;
- dùng root vì restricted user bị thiếu;
- disable global safety settings;
- chạy Phase I `UP` hoặc drop 10 legacy columns để dựng review;
- dùng `pems_i_refusal`, `pems_i_upgrade`, `pems_i_rollback` làm môi trường review dài hạn;
- xóa `pems_i_refusal` bằng tay trong lượt này.

---

## 6. Phase E1 — xây safe review fixture

### 6.1 Audit và mở rộng harness đúng cách

Đọc code hiện có của `SqlSafetyGuard.ps1`, importer, transformer và 50 tests. Không viết lại nếu đã đúng. Chỉ bổ sung một **review mode** tách biệt nếu cần.

Review mode phải:

- exact-target `pems_review_v2`;
- không làm yếu denylist protected DB/admin statements;
- không sửa Phase I destructive allowlist;
- từ chối source thay đổi hash/shape ngoài expected;
- tạo output atomically;
- rescan output trước mysql spawn;
- ngăn TOCTOU: bytes được scan phải chính là bytes được truyền cho mysql;
- ghi evidence không chứa password;
- unsafe input ⇒ exit nonzero, mysql invocation count zero, zero target mutation.

Thêm tests cho review mode nếu code thay đổi. Không coi 50 tests cũ là bằng chứng cho code mới nếu chưa bổ sung test tương ứng.

### 6.2 Import baseline an toàn

Mục tiêu baseline là schema/seed hiện hành đủ để chạy application V2 ở **additive compatibility state**:

- giữ 10 legacy columns;
- có các bảng Per-Campus V2 additive;
- không chạy contract-drop;
- không bật flags mặc định trong committed production configuration.

Xác định authoritative master source bằng repository thực tế. Không đoán filename mới nhất chỉ dựa trên timestamp. Scan mọi candidate read-only và chọn theo evidence/schema requirements.

Nếu authoritative master chưa chứa additive V2 schema:

1. import safe transformed master vào review DB;
2. chạy đúng additive migration chain hiện có theo thứ tự, qua safe wrapper;
3. chạy verify tương ứng;
4. không tự phát minh migration mới trừ khi code thực tế cần một additive schema change bị thiếu.

Mỗi bước phải có:

- input hash;
- target database;
- restricted user identity;
- mysql exit code;
- before/after schema fingerprint;
- row-count summary cho bảng chính;
- verdict token machine-readable;
- database touch ledger.

---

## 7. Phase E2 — deterministic review seed đủ case để trải nghiệm

Tạo hoặc sửa một seed artifact riêng cho review, ví dụ trong thư mục test/review fixture phù hợp với convention hiện có. Không nhét dữ liệu review vào production master nếu chưa có yêu cầu.

### 7.1 Yêu cầu kỹ thuật của seed

- Idempotent hoặc fail-closed khi target không đúng expected state.
- Chỉ chạy trên exact `pems_review_v2` qua restricted wrapper.
- Không chứa `USE`, `CREATE DATABASE`, `DROP DATABASE` hoặc protected references.
- Không disable `SQL_SAFE_UPDATES` global/session để lách lỗi.
- Mọi UPDATE/DELETE phải dùng primary/unique key hoặc một tập khóa đã materialize và kiểm tra count.
- Không dùng broad UPDATE có thể gây Error 1175.
- DML liên quan phải nằm trong transaction khi MySQL cho phép.
- Có precondition/postcondition counts; mismatch ⇒ rollback/fail.
- Dùng deterministic identifiers, timestamps hợp lý và quan hệ FK thật.
- Không hardcode password plaintext trái với cơ chế seed/auth hiện có.
- Không giả mạo trạng thái không thể đạt được qua business flow nếu mục tiêu test là hành trình thật; nếu phải fixture trực tiếp, ghi rõ fixture-only setup.

### 7.2 Personas tối thiểu

Tận dụng account seed hiện có nếu đúng role/sub-role/campus. Chỉ bổ sung phần thiếu. Tối thiểu cần nhận diện được:

- Visitor/Registrant A;
- Contact B khác A;
- một trường hợp A đồng thời là B;
- Staff Leader campus HN;
- Staff Leader campus HCM;
- Host/Staff được phân công;
- Department Leader;
- Department Staff nếu flow sử dụng;
- Student participant;
- invited participant/guest-support actors cần thiết;
- HO/read-only actor nếu current permission matrix có flow tương ứng.

Không đổi role/sub-role mapping theo suy đoán. Đối chiếu `PERMISSION_MATRIX.md`, `PERMISSION_RULES.md`, entity/schema và current authorization policies.

### 7.3 Scenario matrix tối thiểu

Tạo dữ liệu hoặc tạo qua API/browser để có:

1. V1 request kiểm tra compatibility.
2. V2 single-campus.
3. V2 multi-campus với dữ liệu form giống nhau.
4. V2 multi-campus mixed:
   - delegation name khác;
   - purpose/working content khác;
   - visit type hoặc language/media/transport khác khi business validation cho phép.
5. Registrant A = Contact A.
6. Registrant A ≠ Contact B, identity claim PENDING.
7. Request có host được phân công.
8. Request có participant invitation ở các trạng thái hợp lệ: pending/accepted/declined hoặc trạng thái thật trong code.
9. Request có student participant phục vụ contribution/photo flow.
10. Pending-edit và resubmit candidate.
11. Contact transfer candidate.
12. Amendment candidate và revision/history data.
13. Email/report candidate theo từng campus.
14. Missing-detail/corrupt fixture chỉ dành cho negative test, không trộn vào happy-path review account.

Tạo một file hướng dẫn ngắn liệt kê account/persona, request code, campus, trạng thái và journey dùng cho review. Không ghi secret vào Git.

---

## 8. Phase E3 — cấu hình môi trường full-stack review

### 8.1 Feature flags

Xác minh exact option classes/config binding ở HEAD. Dự kiến có:

- `PerCampusFormV2.Enabled`
- `PerCampusFormV2Write.Enabled`

Nhưng phải dùng tên thật từ source, không đoán.

Yêu cầu:

- default committed values vẫn `false`/OFF;
- chỉ bật flags bằng review/testing environment hoặc process environment;
- không commit secrets/connection string thật;
- không sửa production/Railway/Vercel settings;
- capability endpoint/provider frontend phải phản ánh đúng backend flags;
- khi flags OFF, V1 behavior vẫn không đổi;
- khi write ON nhưng read OFF, backend vẫn fail closed theo contract hiện có.

### 8.2 Review launch flow

Tạo hoặc cập nhật cách chạy review có thể tái lập, ưu tiên script/config không chứa secret:

1. validate DB target và current user;
2. start backend với Review/Testing environment;
3. start Vite frontend trỏ đúng API;
4. health/capability checks;
5. in exact URLs và persona checklist cho người review;
6. cleanup process đáng tin cậy.

Không auto-create/drop protected hoặc review database trong application startup.

---

## 9. Phase E4 — backend experience audit và corrective fixes

Không audit backend bằng string search thuần. Đi theo các journey thật và trace request từ controller → command/query → service → EF/MySQL → DTO.

Kiểm tra tối thiểu:

- public initiate-v2 và verify-v2 bằng OTP;
- authenticated create-v2;
- idempotency/replay;
- request and instance detail reads;
- pending edit và resubmit;
- target-campus approve/reject;
- assign host/department/student nếu use case hỗ trợ;
- participant invitation;
- contact claim/transfer;
- safe edit/amendment/history;
- per-campus email/action info;
- report/invoice/overview consumers đã được migrate;
- photo upload image-only contract;
- authorization theo role, sub-role, department và campus scope;
- missing-detail, stale row-version, invalid target campus và mixed request-level handling.

Nguyên tắc sửa:

- V2 instance-scoped output phải đọc canonical `visit_instance_form_details` của đúng instance/campus.
- Không dùng smallest-campus compatibility projection làm business truth cho V2.
- Không lấy detail của sibling campus.
- Không thêm silent V1 fallback mới.
- V1 compatibility phải giữ nguyên trong lượt review này.
- Không retire legacy writers/columns trong lượt này trừ một lỗi cục bộ bắt buộc và có test rõ ràng.
- Không self-select mixed request-level email/report display rule. Nếu flow cần rule mới, dừng đúng subtask đó, đưa evidence và 2–3 lựa chọn cho chủ dự án; tiếp tục các journey độc lập.
- Không xử lý F10 search bằng suy đoán trong lượt này.

Mọi bug fix cần regression test ở layer thấp nhất hợp lý và ít nhất một proof xuyên tầng nếu bug chỉ xuất hiện trên MySQL/Pomelo hoặc serialization/browser.

---

## 10. Phase E5 — frontend experience audit và corrective fixes

Đi theo UI thật, không chỉ dựa vào TypeScript compile.

Kiểm tra tối thiểu:

- capability loading và routing khi flags ON/OFF;
- public V2 form có thể chọn một/nhiều campus;
- mỗi campus có form detail độc lập;
- add/remove campus không làm mất hoặc copy nhầm dữ liệu;
- guest/support lists không chia sẻ mutable state giữa campus;
- submit/OTP/verify dùng đúng V2 payload;
- pending edit/resubmit hydrate đúng từng campus;
- detail pages hiển thị đúng campus;
- Staff Leader actions chỉ tác động target instance;
- claim/transfer/amendment/history pages;
- report/email screens liên quan;
- upload chỉ quảng bá và chấp nhận JPG/JPEG/PNG/WEBP, tối đa 5 MB, thông báo dùng từ `ảnh`;
- loading/empty/error/409/403/404/concurrency states;
- Vietnamese labels rõ nghĩa và không lộ technical exception/raw sensitive data;
- responsive behavior ở viewport desktop chính và ít nhất một mobile viewport.

Không redesign diện rộng. Chỉ sửa những gì chặn hoặc làm sai trải nghiệm V2, vi phạm design system, hoặc gây hiểu nhầm rõ ràng.

Không hardcode mock response khi API thật đã tồn tại. Không bypass authorization để demo.

---

## 11. Phase E6 — real-stack browser journey matrix

Chạy bằng browser automation hiện có hoặc real Chromium/Playwright theo infrastructure của repo:

React/Vite → .NET API → MySQL `pems_review_v2`.

### 11.1 Happy-path journeys

Tối thiểu:

1. Visitor tạo V2 single-campus và hoàn thành OTP.
2. Visitor tạo V2 multi-campus uniform.
3. Visitor tạo V2 multi-campus mixed và xác minh từng campus giữ đúng dữ liệu.
4. Registrant A = Contact A.
5. Registrant A ≠ Contact B, claim flow hoạt động theo thiết kế.
6. Visitor mở detail và pending-edit/resubmit.
7. Staff Leader campus HN chỉ thấy/xử lý HN instance.
8. Staff Leader campus HCM chỉ thấy/xử lý HCM instance.
9. Host/department/student assignment hoặc invitation theo permission thật.
10. Contact transfer hoặc amendment journey có thể thực thi trong fixture.
11. Email/report/detail surfaces lấy đúng per-campus values.
12. Student contribution/photo upload validation end-to-end.

### 11.2 Negative/authorization journeys

Tối thiểu:

- user không thuộc campus không được đọc/ghi instance khác;
- wrong role/sub-role bị từ chối;
- duplicate submit/idempotent replay không tạo request thứ hai;
- missing form detail trả đúng domain error;
- stale row version bị từ chối;
- write ON/read OFF fail closed;
- flags OFF không làm vỡ V1;
- invalid file type/oversize bị chặn đồng bộ frontend/backend;
- raw technical error không hiện cho người dùng.

### 11.3 Evidence

Cho mỗi journey ghi:

- journey ID;
- persona;
- preconditions/request code;
- exact route/API;
- expected result;
- actual result;
- PASS/FAIL/BLOCKED;
- DB assertions chính;
- screenshot hoặc automation artifact nếu framework hỗ trợ;
- bug/fix commit nếu có.

Không gọi một journey PASS nếu chỉ build xanh mà chưa chạy flow.

---

## 12. Test gates bắt buộc

Chạy theo mức thay đổi thực tế và ghi exact command/exit code:

### Database/safety

- toàn bộ SqlSafetyGuard/importer tests;
- incident fixture vẫn có mysql invocation count = 0;
- review target gate tests;
- seed idempotency hoặc deterministic refusal proof;
- protected DB fingerprints trước/sau review session không đổi.

### Backend

- targeted tests cho mọi bug fix;
- C1/C2 20/20 regressions;
- Unit full suite;
- Architecture full suite;
- relevant IntegrationTests trên isolated review/test DB;
- full IntegrationTests nếu environment đáp ứng và không chạm protected DB.

### Frontend

- `tsc`/typecheck;
- Vitest full suite;
- Vite production build;
- targeted component tests cho bug fix;
- browser/real-stack matrix.

Nếu một full suite không chạy được, ghi `NOT RUN` hoặc `BLOCKED` cùng nguyên nhân. Không dùng targeted suite để tuyên bố full suite pass.

Không sửa production seed/code chỉ để làm test xanh. Không làm yếu assertion.

---

## 13. Phạm vi cố ý hoãn

Không dành thời gian chính của lượt này cho:

- drop 10 legacy columns;
- Phase I destructive UP trên review DB;
- production backfill/cutover;
- complete 3,945-hit R6 disposition;
- F5 full manifest depth;
- F7 contract-dropped fresh target;
- F10 search decision;
- mixed request-level representation mới chưa được owner quyết định;
- deploy Railway/Vercel hoặc bật production flags.

Được phép ghi finding và tạo next-step ledger, nhưng không để các việc này chặn `V2 EXPERIENCE READY` nếu các hành trình review độc lập đã chạy.

---

## 14. Documentation và review handoff

Tạo/cập nhật một review guide ở vị trí phù hợp, ví dụ:

`docs/ChangeSauHopChiQUyen/sauhop_13-07/PEMS_V2_EXPERIENCE_REVIEW_GUIDE.md`

Nội dung:

- cách chạy backend/frontend review không chứa secret;
- exact review database name;
- feature flags và cách bật chỉ cho review;
- personas/account aliases, không ghi password;
- scenario/request codes;
- thứ tự journey đề xuất cho chủ dự án;
- expected UI/business result;
- known limitations và business decisions đang mở;
- cách reset review data qua safe harness;
- cảnh báo không dùng protected DB.

Cập nhật status docs trung thực:

- `V2 EXPERIENCE READY` chỉ nếu Definition of Done bên dưới pass;
- contract-drop vẫn `NOT READY`;
- R6/F5/F7 giữ trạng thái thật;
- incident statement không được xóa hoặc biến thành “protected DB never touched”.

---

## 15. Semantic commit plan cho lượt này

Lập plan sau khi biết files thật. Mục tiêu khoảng 2–3 commit, ví dụ:

1. `chore(database): establish the isolated v2 review fixture`
   - review-mode harness, bootstrap template, safe transformed fixture, deterministic review seed, tests và hướng dẫn trực tiếp.
2. `fix(per-campus): complete the end-to-end v2 review journeys`
   - backend/frontend fixes cùng regression tests cho cùng behavior.
3. `docs(per-campus): record the verified review matrix and remaining decisions`
   - chỉ dùng khi evidence/report đủ độc lập; nếu chỉ cập nhật nhỏ gắn với commit 1/2 thì squash vào commit tương ứng.

Không tạo commit cho từng file/test nhỏ. Không commit generated logs, screenshots chứa secret, connection strings, passwords hoặc temporary transformed files trừ khi artifact đó được thiết kế để version-control và đã scrubbed.

Trước commit:

```bash
git diff --check
git status --short
git diff --stat
```

Sau commit:

```bash
git log --oneline --decorate -15
git show --stat --oneline <each-new-commit>
git status --short --branch
```

Không push/merge/deploy.

---

## 16. Stop conditions

Dừng mutation ngay và báo nếu:

- target/current database không đúng `pems_review_v2`;
- current MySQL user là root/admin;
- grants chạm protected DB;
- source/output scan có unknown/dangerous statement;
- source hash/shape không đúng expected;
- mysql sẽ nhận bytes khác bytes đã scan;
- protected DB fingerprint thay đổi;
- cần một business rule mới cho mixed request-level representation;
- Git history chứa commit người khác trong vùng định rewrite;
- cần force-push hoặc destructive Git command;
- cần production flag/deploy để tiếp tục.

Khi một subtask bị block, tiếp tục các phần độc lập an toàn. Không dừng toàn bộ phiên nếu frontend/static/backend tests khác vẫn có thể hoàn thành.

---

## 17. Definition of Done

Chỉ được kết luận **V2 EXPERIENCE READY** khi tất cả điều sau có evidence:

### Git

- local/remote relation được xác minh;
- không mất user changes;
- commit mới được gom semantic;
- không rewrite commit pushed/other-author;
- working tree chỉ còn known intentional untracked files.

### Database

- review DB tách biệt và restricted credential được chứng minh;
- protected DBs không đổi trong lượt;
- baseline/additive schema import qua safe harness;
- review seed đủ persona/scenario và deterministic;
- không chạy contract-drop;
- DB touch ledger đầy đủ.

### Backend/frontend

- V2 flags bật chỉ trong review process;
- frontend, API và MySQL chạy xuyên suốt;
- single/uniform/mixed per-campus data không bị trộn;
- target-campus authorization đúng;
- create/OTP/read/edit/resubmit/approval và các identity/amendment/report/photo flows khả dụng theo matrix;
- các lỗi chặn trải nghiệm đã sửa và có regression tests.

### Verification

- guard tests pass;
- relevant backend suites pass;
- frontend typecheck/Vitest/build pass;
- real-stack journey matrix chạy thật;
- mọi NOT RUN/BLOCKED được ghi rõ;
- review guide đủ để chủ dự án tự trải nghiệm.

Nếu restricted credential/database bootstrap chưa được chủ dự án chạy thì trạng thái tối đa là:

`IN PROGRESS — review code/fixtures prepared; real-stack review BLOCKED pending restricted database bootstrap.`

Nếu chỉ build/test xanh nhưng chưa chạy browser matrix thì không được gọi `EXPERIENCE READY`.

---

## 18. Deliverables cuối phiên

Báo cáo theo đúng thứ tự:

1. HEAD start/end, remote SHA, merge-base, ahead/behind thật.
2. Kết quả commit consolidation và tree-equivalence proof, hoặc lý do không rewrite.
3. Files changed, nhóm theo SQL/backend/frontend/tests/docs.
4. Review DB bootstrap/restricted credential status.
5. Safe-import evidence: source/output hash, guard verdict, mysql invocation behavior.
6. Database touch ledger, gồm protected DB fingerprint proof.
7. Review seed personas và scenario matrix.
8. Feature-flag configuration, xác nhận defaults vẫn OFF.
9. Backend findings/fixes.
10. Frontend findings/fixes.
11. Browser journey matrix PASS/FAIL/BLOCKED.
12. Exact test commands, counts, exit codes và môi trường.
13. Business decisions còn cần chủ dự án, tách khỏi technical blockers.
14. Deferred F1/F5/F7/F10/contract-drop status.
15. Review guide path và cách chủ dự án bắt đầu trải nghiệm.
16. `git status --short --branch` cuối phiên.
17. New commits với hash/message/author; xác nhận không AI attribution.
18. Xác nhận không push/merge/deploy nếu không được yêu cầu.

Kết luận phải dùng một trong hai dạng:

```text
V2 EXPERIENCE READY — isolated review database, review-only flags, representative data and verified browser journeys are available; Phase I contract-drop remains NOT READY.
```

hoặc:

```text
IN PROGRESS — V2 review environment is not yet fully executable; exact blockers and completed evidence are listed above. Phase I contract-drop remains NOT READY.
```

Không dùng “100% complete” nếu còn journey chưa chạy, database chưa được bootstrap, hoặc business decision chưa được chốt.

