# PEMS PURE V2-ONLY — IMPLEMENTATION REPORT

**Phiên:** IMPLEMENTATION (sau audit)
**Ngày:** 2026-07-23
**Branch:** `Canh-Iter1` (tracking `origin/Cảnh-Iter1`) — không chạm `Dev`

---

## 1. KẾT LUẬN

> ## ⛔ NOT READY
>
> **Phase 0 và Phase 1 hoàn tất, đã verify bằng kết quả chạy thật.** Phase 2–7 **chưa hoàn tất**.
> Ngoài ra credential rotation chưa được xác nhận → `SECURITY ROTATION PENDING`.
>
> `Phase 1 VERIFIED` **không** đồng nghĩa toàn dự án READY.

Không dùng nhãn `READY WITH CAVEATS` (bị cấm bởi §19).

### Baseline

| Mục | Giá trị |
|---|---|
| HEAD lúc bắt đầu | `19bed5101b8b3bd564d438e6add90c67a2f83fa6` — khớp prompt |
| Merge-base với `Dev` | `584f3ddace324eb0b4f6916ca586e6f1b2e05090` — khớp |
| ahead/behind `Dev` | 0 / 11 — khớp |
| SQL canonical blob | `825b95672491d653d5537c95b4e81f3c000b229f` ✅ |
| SQL canonical dòng | 14,832 (`wc -l`) ✅ |
| SQL canonical SHA-256 | `7ec63e9044ecd1910e9a7137c99773bb13b36902f3042fd7bc6cfce402892415` — **KHÔNG đổi trong phiên này** |

Baseline không đổi → không cần dừng theo §3.6.

---

## 2. BỐN QUYẾT ĐỊNH ĐÃ CHỐT

Không mở lại. Trạng thái áp dụng:

| Quyết định | Trạng thái |
|---|---|
| DECISION-01 — operational contact riêng từng campus | 🔶 đọc đã xong + có test chặn fallback; **ghi** thuộc Phase 2 |
| DECISION-02 — Pure V2-only, bỏ dual-read/fallback | ✅ ĐÃ THỰC HIỆN — 0 discriminator trong `backend/`, thiếu detail = fail loud |
| DECISION-03 — mọi `issue_count = 0` | ⏳ chưa tới (Phase 6) |
| DECISION-04 — config không nhạy cảm vẫn track được | ✅ ĐÃ THỰC HIỆN (Phase 0A) |

---

## 3. GAP REGISTER

| Gap | Mô tả | Trạng thái |
|---|---|---|
| **GAP-003** | Solution không build (`GalleryTestDbContext`) | ✅ **VERIFIED** — build 0 error, unit test chạy được |
| **GAP-002** | Bootstrap trỏ SQL đã xóa + fail-open | ✅ **VERIFIED** — fail-closed + hash pin + 11 test |
| **GAP-007** | SMTP password / JWT secret trong repo | 🔶 **FIXED (code)** / ⛔ **BLOCKED (rotation)** |
| **GAP-010** | Enum consistency test trỏ SQL không tồn tại | ✅ **VERIFIED** |
| **GAP-012** | E2E realstack trỏ SQL cũ | ✅ **FIXED** |
| **GAP-013** | Stale SQL path (TestTc, review script, phase_1) | ✅ **FIXED** |
| **(mới) GAP-022** | Upload ảnh: app layer yếu hơn DB trigger | ✅ **VERIFIED** — xem §6 |
| **GAP-001** | 12 phantom EF mapping | ✅ **VERIFIED** — đã xóa; `SchemaContractTests.Every_mapped_table_and_column_exists_in_the_canonical_schema` chặn tái diễn |
| **GAP-004** | Write path ghi cột đã xóa | ✅ **VERIFIED** — compatibility projection đã gỡ khỏi 3 write service; 517/517 integration test PASS |
| **GAP-006** | Dual-read theo discriminator | ✅ **VERIFIED** — không còn `FormSchemaVersion` nào trong `backend/`; thiếu detail = fail loud |
| **GAP-011** | V1 service còn DI | ✅ **VERIFIED** — handler/command/validator V1 đã xóa; hằng số `NotPerCampusV2` chết cũng đã gỡ |
| **GAP-005** | Feature flag deadlock | ⬜ **OPEN** (Phase 4) |
| **GAP-008** | Frontend route theo `formSchemaVersion` | ⬜ **OPEN** (Phase 4) |
| **GAP-009** | 14 false-fail negative guard (`GET DIAGNOSTICS`) | ⬜ **OPEN** (Phase 6) |
| **GAP-014** | FK delete behavior drift | ✅ **VERIFIED** — đo thật: chỉ **1** drift (`visit_requests.visitor_user_id`), đã sửa + 2 test chặn |
| **GAP-015** | 151 seed placeholder | ⬜ **OPEN** (Phase 6) |
| **GAP-016** | 3 instance thiếu agenda | ⬜ **OPEN** (Phase 6) |
| **GAP-017** | Nullability lệch (email_templates, news_content_sections) | ✅ **VERIFIED** — 3 cột đã đồng bộ entity/DTO/validator + 11 test |
| **GAP-018** | Disposable DB rò rỉ | ✅ **FIXED** — cleanup trong `catch` |
| **GAP-019/020/021** | DbSet coverage, computed column, comment lỗi thời | ⬜ **OPEN** (Phase 7) |

---

## 4. PHASE 0 — HOÀN TẤT ✅

### 0A — Externalize secret + validation

**File sửa:**
- `backend/PEMS.Api/appsettings.json` — bỏ giá trị `JwtSettings:SecretKey`, `Smtp:Password`; `Smtp:Enabled` → `false`
- `backend/PEMS.Api/appsettings.Development.json` — bỏ `GoogleDrive:ClientSecret`, `GoogleDrive:RefreshToken`
- `backend/PEMS.Api/Extensions/SecretConfigurationValidator.cs` — **mới**
- `backend/PEMS.Api/Program.cs` — gọi validation trước `builder.Build()`
- `backend/PEMS.Api/appsettings.Local.example.json` — **mới**
- `.gitignore` — thêm `.tmp-build/`, `appsettings.Development.Local.json`

**Phát hiện ngoài dự kiến:**
1. `backend/PEMS.Api/.tmp-build/` **bị track** — build output chứa **bản sao thứ hai** của `appsettings.json` với một SMTP password **khác**. Đã untrack (5 file).
2. `appsettings.Development.json` chứa **`GoogleDrive:RefreshToken`** — OAuth refresh token sống, chưa từng được nêu trong audit. Đã externalize.
3. `TestTc/` — project mồ côi (không có trong `PEMS.slnx`, không code nào tham chiếu) hard-code root password và **tự seed `pems_db` thật**. Đã xóa.

**Runtime binding:** cả 4 secret đọc qua `IConfiguration` section → env var override (`JwtSettings__SecretKey`, `Smtp__Password`, `GoogleDrive__ClientSecret`, `GoogleDrive__RefreshToken`) hoạt động không cần sửa consumer.

#### Hậu quả phát sinh và bản sửa (`5371aaaf`)

Bỏ JWT key khỏi `appsettings.json` để lại giá trị là **chuỗi rỗng**, không phải `null` — nên hai guard `?? throw` sẵn có **không kích hoạt**. Hệ quả đo được bằng cách chạy API thật và gọi `/api/auth/login`:

```
STATUS 500 — IDX10703: Cannot create a 'SymmetricSecurityKey', key length is zero.
```

App vẫn khởi động, nhưng mọi request chạm authentication đều 500. Đúng kiểu "âm thầm dùng giá trị yếu" mà validator lẽ ra phải chặn.

**Đã sửa:**
- Ngoài Production: sinh key ngẫu nhiên 32 byte cho tiến trình khi chưa cấu hình + log warning (token không sống qua restart).
- Chèn **ngay sau `CreateBuilder`**, trước mọi đăng ký service — `AddJwtAuthentication` chụp giá trị lúc registration, đặt muộn hơn thì handler vẫn giữ chuỗi rỗng (đây chính là lỗi ở lần sửa đầu).
- Production: vẫn từ chối khởi động khi thiếu key, **và** khi key ngắn hơn 32 byte (HS256 ném IDX10653 dưới 128 bit).

**Xác minh lại bằng chạy thật:** `IDX10703` biến mất; sai mật khẩu → `401 INVALID_CREDENTIALS`; đúng mật khẩu → đi tiếp tới business rule. Test: 9 → **13**.

#### Quyết định của chủ dự án: giữ credential trong config được track

Sau khi cân nhắc đánh đổi, **chủ dự án quyết định khôi phục 4 giá trị vào file được track** để đồng đội pull về là chạy được ngay, không cần cấu hình thủ công. Đã thực hiện bằng `git checkout 19bed510 -- <2 file appsettings>`.

Trạng thái sau khôi phục:

| Thành phần | Trạng thái |
|---|---|
| `JwtSettings:SecretKey` | có giá trị → không sinh key ngẫu nhiên nữa (đã xác minh: warning biến mất) |
| `Smtp:Password` + `Smtp:Enabled=true` | gửi email hoạt động trở lại |
| `GoogleDrive:ClientSecret` + `RefreshToken` | upload Drive hoạt động trở lại |
| Đăng nhập | ✅ xác minh thật: `Admin@123` qua xác thực, tới business rule `CAMPUS_REQUIRED` |

**Vẫn giữ lại từ `fc647d89`** (không ảnh hưởng việc chạy, vẫn có giá trị):
- `SecretConfigurationValidator` — Production vẫn từ chối khởi động khi thiếu/ngắn secret; dev vẫn có fallback key ngẫu nhiên nếu ai đó tự xóa giá trị.
- `.tmp-build/` bỏ track — build output trùng lặp, không có lý do commit.
- `TestTc/` đã xóa — trỏ file SQL không tồn tại, không nằm trong solution.
- `appsettings.Local.example.json` — cho ai muốn override bằng file local.

> ⚠️ **Rủi ro còn lại (chủ dự án đã biết và chấp nhận):** 4 credential này nằm trong repo và trong **16 revision lịch sử trên 5 remote branch**. Chấp nhận được với repo private trong nhóm đồ án. Nếu repo chuyển sang public hoặc nộp/công khai, **phải rotate trước**.

**Test:** `SecretConfigurationValidatorTests` — **9 test**, gồm test chứng minh thông báo lỗi **không echo giá trị**.

> ⛔ **BLOCKER:** Rotation là thao tác ngoài repository. Chủ dự án phải tự rotate Gmail app password, JWT secret, Google OAuth client secret + refresh token. Audit đã xác định credential nằm trong **16 revision lịch sử** trên **5 remote branch** → **history rewrite KHÔNG cứu được**; rotate là bắt buộc. Không tự rotate, không tự sinh secret mới vào repo, không rewrite history (§5).

### 0B — Khôi phục build

`tests/PEMS.UnitTests/TestInfrastructure/GalleryTestDbContext.cs`: thêm `VisitRequestFingerprintGuards` theo đúng pattern explicit-interface + `Ignore<>` của 4 harness đã cập nhật.

### 0C — Bootstrap fail-closed

**File mới:** `tests/PEMS.IntegrationTests/TestInfrastructure/CanonicalSqlScript.cs`

Đáp ứng đủ 13 yêu cầu §0C:

| # | Yêu cầu | Thực hiện |
|---|---|---|
| 1 | đúng một canonical SQL | glob `PEMS_FULL_*.sql`, `SearchOption.TopDirectoryOnly` |
| 2 | thiếu file → fail | `FileNotFoundException` |
| 3 | nhiều candidate → fail | `InvalidOperationException` liệt kê tên |
| 4 | verify SHA-256 | `ExpectedSha256` pin `7ec63e90…` |
| 5 | không fallback tên cũ | tên phải khớp `FileName` chính xác |
| 6 | allowlist DB name | `^pems_test_run_[0-9a-fA-F]{32}$` |
| 7 | retarget **mọi** statement | regex CREATE/DROP/USE DATABASE, không chỉ một `USE` |
| 8 | quét lại bản tạm | `AssertSafeToImport` |
| 9 | từ chối `pems_db` ngoài comment | có — bỏ qua dòng `--`/`#` |
| 10 | từ chối `SOURCE` / `\.` | có |
| 11 | import MySQL 8 disposable | có |
| 12 | assert sau import | `DATABASE()`, 81 bảng, 0 view, 0 `pems_seed_*`, 32 trigger, 0 `form_schema_version`, 0 cột global, detail 1-1, 0 orphan, 0 request thiếu campus |
| 13 | cleanup khi fail | `catch` → `DropDisposableDatabase` |

**Test:** `CanonicalSqlScriptTests` — **11 test** (thiếu file, hash, retarget đủ, từ chối target không disposable, guard `pems_db`/`SOURCE`, cho phép comment).

**Stale SQL reference đã sửa:** `DocumentsOwnerTypeEnumConsistencyTests.cs`, `run-realstack-e2e.mjs`, `Build-ReviewDatabase.ps1`, `generate_fresh_target.ps1`, `Test-SqlSafetyGuard.ps1`. Hit còn lại **chỉ là comment ghi nguồn gốc** trong file `.sql` — hợp lệ theo §15.

---

## 4bis. PHASE 1 — TIẾN ĐỘ QUA 6 PHIÊN (cập nhật mới nhất)

**Kết luận: `Phase 1 VERIFIED`.** Toàn bộ gate Phase 1 đã xanh bằng kết quả chạy thật.
Toàn dự án vẫn **NOT READY** vì `SECURITY ROTATION PENDING` (§13) và Phase 2–7 chưa làm.

### Baseline cuối phiên

| Mục | Giá trị |
|---|---|
| HEAD lúc bắt đầu phiên | `5509f4a5` |
| Branch | `Canh-Iter1` → `origin/Cảnh-Iter1`; **7** commit local phía trên `19bed510` |
| `d3a40adf` | `HEAD~3`, đúng vị trí `origin/Cảnh-Iter1` |
| SQL canonical | **không đổi** — blob `825b9567`, SHA-256 `7ec63e90…`, 14,832 dòng (`wc -l`) |

**Đính chính hai điểm sai trong bản báo cáo trước:**

1. Số commit local là **7**, không phải 6.
2. "UnitTests 937/937" và "915 / 23 fail" **không mâu thuẫn** — chúng đo hai trạng thái cây mã khác nhau: 937 là HEAD sạch (WIP đã stash), 915 là khi đã apply WIP. Chênh 22 test do WIP xóa 3 file test V1. Bản trước thiếu chú thích trạng thái nên gây hiểu nhầm.

### Stash (ghi theo commit hash, không theo chỉ số)

| Hash | Nội dung |
|---|---|
| `29b74f56` | **session 6a** — IntegrationTests chạy thật, 512/512 PASS |
| `f4827af5` | session 5 |
| `b318b121` | session 4 |
| `93ebee5f` | session 3 |
| `324482af` | session 2 |
| `8c1f8dbc` | session 1 |
| `e3272c7b` | `On Dev` (không thuộc công việc này) |

Không drop stash nào. Mọi thao tác dùng `git stash apply <hash>`, không dùng `pop`, không dùng `stash@{n}`.

### Đường cong lỗi biên dịch

| Mốc | Lỗi |
|---|---|
| Sau khi xóa 12 phantom mapping | 183 (47 file, toàn bộ solution) |
| Cuối session 2 | 48 |
| Cuối session 3 | production code = 0; IntegrationTests 136 |
| Cuối session 4 | IntegrationTests 43 (7 file) |
| Cuối session 5 | IntegrationTests **0**; UnitTests 915/915 |
| **Cuối session 6** | **0 error toàn solution; cả 3 bộ test PASS** |

### Ba test V1 đã bị loại — xác nhận coverage thay thế

Ba file test dưới đây bị xóa cùng handler V1 của chúng. Handler không còn tồn tại, nên test không còn target runtime hợp lệ — không phục hồi.

| Test V1 đã xóa | Handler V1 tương ứng | Coverage Pure V2 thay thế |
|---|---|---|
| `CreateAuthenticatedVisitRequestCommandValidatorTests` | `CreateAuthenticatedVisitRequestCommandHandler` (đã xóa) | `CreateVisitRequestV2CommandTests`, `CreateVisitRequestV2ServiceTests`, `ActorRelationAuthenticatedCreateApiTests` |
| `ResubmitRejectedVisitRequestCommandHandlerTests` | `ResubmitRejectedVisitRequestCommandHandler` (đã xóa) | `ResubmitRejectedVisitRequestV2CommandTests`, `ResubmitRejectedVisitRequestV2ServiceTests`, `VisitorEditResubmitApiTests` |
| `UpdatePendingVisitRequestCommandHandlerTests` | `UpdatePendingVisitRequestCommandHandler` (đã xóa) | `UpdatePendingVisitRequestV2CommandTests`, `UpdatePendingVisitRequestV2ServiceTests`, `EditableVisitRequestDetailV2Tests` |

### Session 6 đã làm

**1. Chạy IntegrationTests lần đầu tiên trong lịch sử dự án** trên database disposable.

Hai lỗi hạ tầng phải sửa trước khi bộ test chạy được:

- **Repo root không giải được** khi build ra thư mục ngoài repo. Không phải lỗi code — do cách chạy; đã chuyển output vào `tests/PEMS.IntegrationTests/bin/itrun/` (đã gitignore) vì tiến trình `PEMS.Api` đang chạy khóa file DLL.
- **Safety scanner chặn nhầm chính SQL canonical.** `AssertSafeToImport` tìm `USE` / `SOURCE` không neo vị trí, nên bắt nhầm 7 dòng: từ tiếng Anh "use" trong string literal và `MESSAGE_TEXT` (`'…must use SELF_SERVICE source'`, `'…use Internal Portal…'`), và một cột tên `source ENUM(...)`. Đã neo hai regex đúng như `Retarget` vốn làm (đầu câu lệnh, hoặc ngay sau `;`) và yêu cầu đối số của `SOURCE` trông giống đường dẫn. **Không nới lỏng:** MySQL không chấp nhận `USE` giữa câu lệnh hay trong stored program, nên neo vị trí không mất khả năng phát hiện. Đã thêm 9 test khoá lại cả hai chiều.

**2. Chuyển 14 test V1 còn sót sang bất biến Pure V2.** Sau khi seed bỏ cột global, các test này khẳng định giá trị không thể tồn tại (`GLOBAL-DELEG`). Không xóa, không skip — mỗi test đổi thành một bất biến **chưa được phủ** trong chính file đó (bảng ở dưới).

**3. Bỏ 3 tham số chết trong `CanonicalV2Seed.SeedRequest`** (`formSchemaVersion`, `globalDelegationName`, `globalVisitType`). Chúng không còn được ghi vào đâu cả, khiến các assertion `NotEqual(StaleGlobalName, …)` **rỗng nghĩa** — đọc như đang bảo vệ nhưng thực chất không. Đã bỏ tham số và đổi tên hằng số cho đúng sự thật.

### Assertion phải đổi — giải trình đầy đủ (§3)

| File | Assertion cũ | Vì sao trái Pure V2 | Thay bằng |
|---|---|---|---|
| `RequestDetailV2Tests`, `DeptInvitationDetailV2Tests` | `ContactPersonFullName == "Primary Contact"` (V1) | Không còn đường đọc V1 | **DECISION-01**: contact phải là operational contact của campus và **NotEqual** primary contact cấp đơn — mạnh hơn, và là giá trị duy nhất còn có thể bị fallback nhầm |
| `StaffCalendarDetailV2Tests` | global projection verbatim | như trên | contact + `VisitType`/`WorkingLanguage`/`MediaConsentStatus` đều phải đến từ detail, kèm NotEqual primary contact |
| `AgendaSetupForInstanceV2Tests`, `VisitInstanceContributionV2Tests`, `VisitInstanceSummaryV2Tests`, `VisitProcessDetailV2Tests`, `VisitInvitationDetailV2Tests` | `"GLOBAL-*"` | Cột global đã bị xóa | **3 campus mixed, đọc campus C (cuối)** — cặp A/B sẵn có không phân biệt được "đọc đúng target" với "đọc 2 campus đầu"; campus thứ ba thì có. Trực tiếp chặn lỗi "campus đại diện" |
| `MyVisitInvitationByIdV2Tests` | global + `OrganizationName` | như trên | Tách bạch: form field từ detail của instance được mời, `OrganizationName` (registrant) vẫn cấp đơn — "không fallback" không được biến thành "bỏ qua request row" |
| `EditableVisitRequestDetailV2Tests` | `Mode == "EDIT"` + global + khách `G1` | như trên | EDIT mode + nội dung per-campus + guest list qua `visit_instance_guest_members` (không còn roster cấp đơn để fallback) |
| `SubmittedVisitRequestFormDetailV2Tests` | rollup qua projection global | như trên | Giữ nguyên rollup (2 campus, đếm mỗi campus đúng 1 lần) nhưng seed V2 — rollup là thứ **không** được per-campus hoá |
| `GetHoReportOverviewCanonicalV2Tests` | lọc theo global visit type | như trên | Lọc từ **campus thứ hai** — reader "campus đại diện" sẽ báo campus 1 ở đây |
| `GetStaffLeaderDeptInvoiceItemsCanonicalV2Tests` | đọc global name | như trên | Đọc bởi Staff Leader **campus 2 / phòng ban 20** — mirror của case A sẵn có |
| `UpdatePendingVisitRequestV2CommandTests` bước 8 | `UPDATE visit_requests SET form_schema_version = 1` rồi chờ `NOT_PER_CAMPUS_V2` | Cột không tồn tại → `Unknown column` (P0 thật, bắt được nhờ chạy thật) | Khẳng định thẳng trên schema sống: **0 cột** `form_schema_version`. Mạnh hơn guard runtime cũ vì đúng cho mọi code path, không chỉ một endpoint |
| `SecretConfigurationValidatorTests.Smtp_disabled_does_not_require_credentials` | Production, không cấp JWT key | Validator (đúng) bắt buộc JWT key ở Production → test fail vì lý do khác chủ đề của nó | Cấp JWT key để cô lập đúng quy tắc SMTP. Quy tắc JWT đã có test riêng `Production_without_jwt_secret_fails_fast` → **không mất coverage** |

Không xóa test, không skip test, không nới lỏng assertion, không gán dữ liệu per-campus lên `VisitRequest`.

### GAP-014 — FK delete behavior (đo thật, không ước lượng)

Audit ước lượng lệch lớn dựa trên đếm tổng (DB 146/58/47 vs EF 105/53/45). **Con số đó gây hiểu nhầm**: EF chỉ khai báo `OnDelete` cho quan hệ nó mô hình hoá, và đếm theo cột thì một cột nằm trong hai FK sẽ bị ghép chéo sai.

So khớp **theo từng constraint** (tên constraint → tập cột có thứ tự → bảng đích) cho kết quả thật:

| Bảng.cột | SQL canonical | EF trước | Kết luận |
|---|---|---|---|
| `visit_requests.visitor_user_id` | `ON DELETE SET NULL` → `users` | `Restrict` | ❌ **Drift thật — đã sửa thành `SetNull`** |
| `visit_photo_face_detections.visit_instance_id` | 2 constraint khác nhau (CASCADE + RESTRICT) | 2 quan hệ tương ứng | ✅ Không lệch — báo cáo sai do so khớp theo cột; đã sửa cách so khớp |
| Tất cả FK còn lại | — | — | ✅ Khớp |

Vì sao quan trọng: EF **không** tạo constraint ở đây (database-first, không migration). `DeleteBehavior` quyết định số phận entity **đang được track**. Khai `Restrict` trong khi DB `SET NULL` khiến EF từ chối một thao tác xoá mà database cho phép — cùng một lệnh xoá cho kết quả khác nhau tuỳ graph có được load hay không.

Chặn tái diễn: `SchemaContractTests.Mapped_delete_behaviour_matches_the_canonical_foreign_keys` + `Set_null_relationships_only_target_nullable_columns`.

### GAP-017 — Nullability (đo thật)

| Cột | SQL canonical | CLR trước | Đã làm |
|---|---|---|---|
| `email_templates.purpose` | `ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION') NOT NULL` | `string?`, **không validator nào** | Entity + Create/Update command + 2 DTO đọc → bắt buộc; validator mới chặn null/rỗng/giá trị lạ. Trước đây thiếu purpose là **500 từ MySQL**, giờ là 400 có thông điệp |
| `news_content_sections.section_title` | `VARCHAR(255) NOT NULL` | `string?` | Entity → bắt buộc; bỏ `?? string.Empty` che lỗi ở read path |
| `news_content_sections.section_body_html` | `LONGTEXT NOT NULL` | `string?` | như trên |

Mọi write path sẵn có đều đã gán giá trị non-null → không cần đổi handler, chỉ đúng hoá kiểu. `ViewEmailTemplateListQuery.Purpose` **giữ `string?`** vì đó là bộ lọc tuỳ chọn, không phải giá trị lưu trữ.

`visit_expense_items.total_amount` (audit có nêu) **không lệch**: đây là STORED GENERATED, contract test bỏ qua đúng cách vì requiredness của EF không nói gì về cột do database sinh.

Chặn tái diễn: `SchemaContractTests.Mapped_nullability_matches_the_canonical_schema` + 11 test `EmailTemplatePurposeValidationTests` (có cả null hợp lệ lẫn null bị cấm).

### Schema contract test (mới)

`tests/PEMS.IntegrationTests/TestInfrastructure/SchemaContractTests.cs` — 5 test, chạy trên database disposable dựng từ SQL canonical đã pin hash:

| Test | Kiểm tra |
|---|---|
| `Canonical_schema_imports_with_the_expected_shape` | 81 base table; 0 cột `form_schema_version`; 0 cột form global trên `visit_requests`; detail per-campus có `delegation_name`; 0 object `pems_seed_*` |
| `Every_mapped_table_and_column_exists_in_the_canonical_schema` | Mọi entity/property EF map đều có table/column thật — chặn đúng lớp lỗi phantom mapping (`Unknown column`) |
| `Mapped_nullability_matches_the_canonical_schema` | GAP-017, bỏ qua cột store-generated |
| `Mapped_delete_behaviour_matches_the_canonical_foreign_keys` | GAP-014, so khớp theo constraint |
| `Set_null_relationships_only_target_nullable_columns` | `SetNull` không bao giờ trỏ vào cột `NOT NULL` |

Đọc schema qua **chính connection của DbContext** (client Oracle `MySql.Data` mà bootstrap dùng từ chối option `GuidFormat` của MySqlConnector), nên vừa tránh xung đột driver vừa đảm bảo soi đúng database EF đang map.

### Gate Phase 1 — kết quả chạy thật

| Gate | Kết quả |
|---|---|
| `dotnet build` Domain / Application / Infrastructure / Api | ✅ **0 error** (4/4) |
| `dotnet build PEMS.slnx` | ✅ **0 error** |
| `dotnet test` ArchitectureTests | ✅ **14/14** |
| `dotnet test` UnitTests | ✅ **926 / 926 PASS**, 0 fail, 0 skip |
| `dotnet test` IntegrationTests | ✅ **517 / 517 PASS**, 0 fail, 0 skip, **45 giây** |
| Schema contract test | ✅ **5/5 PASS** trên DB disposable |
| GAP-014 | ✅ VERIFIED |
| GAP-017 | ✅ VERIFIED |
| `git diff --check` | ✅ sạch |
| `Unknown column` / `Unknown table` | ✅ **0** (bản chạy đầu có 1 — trong SQL thô của test — đã sửa) |
| SQL chạy trên `pems_db` / Railway / DB thật | ✅ **không** — 0 database `pems_test_run_*` còn sót; `pems_db` vẫn đúng 81 bảng |

---

## 5. PHẦN ĐÃ HOÀN TẤT TRONG PHASE 1 ✅

1. **Xóa đúng 12 phantom mapping** — `VisitRequest` (11) + `VisitRequestPendingForm` (1).
2. **Collapse toàn bộ ternary discriminator** — `cond ? <V2> : <V1>` → `<V2>`. Không còn `FormSchemaVersion` nào trong `backend/`.
3. **Xóa V1 dead code** đã chứng minh unreachable (0 dispatch từ `PEMS.Api`): `CreateAuthenticatedVisitRequest`, `VerifyAndCreateVisitRequest`, `UpdatePendingVisitRequest`, `ResubmitRejectedVisitRequest`, `InitiateVisitRequest`, `VisitRequestService`, `IVisitRequestService`, + 3 file test V1, + hằng số chết `NotPerCampusV2`.
4. **Chuyển `InitiateVisitRequestResponse`** sang namespace Pure V2 `Commands.VisitRequestOtp`.
5. **`VisitFormReadService` viết lại** — bỏ dual-read; thiếu detail thì `ConflictException` + log lỗi, không im lặng.
6. **Media consent per-campus** trong toàn bộ luồng News.
7. **`V2CreateNotifier`** báo cho mỗi Staff Leader tên campus **của chính họ**.
8. **~20 fixture test** chuyển sang per-campus detail.

### Bài học đã ghi lại

Một script tự viết để collapse ternary đã **làm hỏng file nguồn** (bắt nhầm câu lệnh `if`, xoá 77 dòng). Phát hiện nhờ build; khôi phục bằng `git restore --source=HEAD`. Từ đó mọi sửa đổi hàng loạt đều làm **thủ công**, và kiểm tra cân bằng dấu ngoặc **không đủ** — đã phải soát tay mới thấy 3 lớp hỏng khác (mất dòng khởi tạo DTO, mất member của anonymous type, `=>` bị biến thành `=`).

---

## 6. PHÁT HIỆN NGOÀI PHẠM VI BAN ĐẦU — ĐÃ SỬA

**GAP-022 — Upload ảnh: application layer yếu hơn database.**

`VisitPhotoStudentScope.ResolveAcceptedStudentAsync` đã bị rỗng ruột thành lời gọi thẳng tới `VisitInstanceMediaAccessScope`; doc của chính nó vẫn ghi "ACTIVE STUDENT with ACCEPTED participation" nhưng **không còn kiểm tra role / trạng thái tài khoản / trạng thái tham gia**. Handler upload gọi thẳng scope rộng.

Trong khi đó trigger `trg_visit_photos_validate_bi` **vẫn** bắt buộc uploader là user `ACTIVE`, role `STUDENT`, participation `ACCEPTED`.

→ Host/Staff/Leader, student `INACTIVE`, hoặc student mới `ASSIGNED` **qua được authorization**, chạm INSERT và nhận `SIGNAL 45000` → **500 thay vì 403**.

**Đã sửa:** khôi phục 3 kiểm tra trong student scope; upload đi qua scope đó. Đọc (xem folder) giữ nguyên phạm vi rộng vì workflow face-scan cần Host mở được thư mục — đã viết test riêng cho contract đọc.

Phát hiện được vì Phase 0B làm unit test **chạy lại lần đầu**: 3 test authorization này đã fail âm thầm suốt thời gian solution không build.

---

## 7. COMMAND ĐÃ CHẠY & KẾT QUẢ THẬT

| Gate | Kết quả |
|---|---|
| `dotnet build PEMS.slnx` | ✅ **0 Error(s)** |
| `dotnet test tests/PEMS.ArchitectureTests` | ✅ **14/14 passed** |
| `dotnet test tests/PEMS.UnitTests` | ✅ **951/951 passed** (926 + 17 test Dev + 8 test per-campus mới) |
| `dotnet test tests/PEMS.IntegrationTests` | ✅ **517/517 passed**, 45s — bao gồm 5 schema contract test |

**Một lần chạy IntegrationTests báo 22 fail, không tái hiện được.** Chạy lại 4 lần liên tiếp (kể cả lặp đúng chuỗi lệnh đã gây lỗi): 517/517. Sau đó: 0 database `pems_test_run_*` còn sót, `Max_used_connections` 13/151, `Aborted_connects` 0. **Nguyên nhân chưa xác định** — tôi không lưu log của lần chạy đó nên bằng chứng đã mất; đây là thiếu sót về quy trình của tôi, không phải kết luận rằng lỗi vô hại. Nếu tái xuất hiện, phải giữ log trước khi chạy lại.
| SQL import disposable | ✅ chạy thật mỗi lần chạy integration; 81 bảng, 32 trigger, 0 `pems_seed_*`, cleanup để lại **0** DB rác |
| Frontend `lint` / `test:unit` / `build` | ⏳ chưa chạy lại (Phase 4 chưa bắt đầu; phiên audit trước: lint 0 error, 389/389, build OK) |
| E2E real-stack | ⏳ chưa chạy (Phase 5) |

**Ghi chú về cách chạy:** tiến trình `PEMS.Api` đang chạy (dev server) khóa DLL đầu ra nên `dotnet build`/`test` mặc định fail với `MSB3021/MSB3027`. Đây là **khoá file, không phải lỗi biên dịch** — đã build/test qua `-p:BaseOutputPath=…`. Đường dẫn output cho IntegrationTests phải nằm **trong repo** vì bootstrap dò repo root bằng cách đi ngược thư mục.

Unit test: **915 → 926** (thêm 11 test `EmailTemplatePurposeValidationTests`).
Integration test: **512 → 517** (thêm 5 schema contract test).

---

## 8. INTEGRATION TEST — ĐÃ CHẠY (cập nhật)

Mục này trước đây giải thích vì sao **chưa** chạy. Nay đã chạy, và điều đó là đúng đắn: chỉ khi chạy thật mới lộ ra hai lỗi mà build sạch không bao giờ cho thấy — một câu `UPDATE visit_requests SET form_schema_version = 1` còn sót trong SQL thô của test (`Unknown column`, đúng lớp P0 mà Phase 1 phải diệt), và safety scanner chặn nhầm chính SQL canonical.

An toàn khi chạy đã được kiểm tra trước, không suy đoán:

- Mọi đường vào database đều đi qua `DisposableDatabaseManager` (kể cả `PemsWebApplicationFactory` và `CanonicalV2ReaderFixture`); các comment nhắc `pems_test` chỉ là chú thích cũ.
- Tên database disposable khớp `^pems_test_run_[0-9a-fA-F]{32}$`; `pems_db` / `pems_test` / `pems_pr3_test` bị chặn tường minh.
- `appsettings.Testing.json`: `Smtp:Enabled = false` → **không có email thật nào được gửi**; `Turnstile:Enabled = false`.
- Sau khi chạy: **0** database `pems_test_run_*` còn sót (kiểm tra cả khi chạy fail lẫn khi chạy pass); `pems_db` vẫn đúng 81 bảng.
- MySQL 8.0.46.

---


## 8bis. MERGE DEV → CANH-ITER1: QUY TRÌNH BẮT BUỘC

**Đã xảy ra thật (2026-07-23).** Merge `Dev` (`4667ada9`) không hề conflict — Git merge sạch vì hai nhánh sửa **file khác nhau** — nhưng solution phát sinh **3 lỗi biên dịch**. Commit Dev `d7e342e5` thêm feature `ExportScheduleReport` viết theo schema V1, đọc `VisitRequest.DelegationName`, `.Purpose`, `.FormSchemaVersion` — ba property Pure V2 đã xóa.

Đây là **semantic conflict**: code mới dùng API mà nhánh kia vừa gỡ bỏ. Git không phát hiện được loại này; chỉ compiler mới thấy. Nhánh đã bị push ở trạng thái không build được cho tới khi sửa.

### Sau MỌI lần merge Dev vào `Canh-Iter1`, chạy NGAY trước khi commit tiếp hoặc push

```bash
dotnet build PEMS.slnx
```

Nếu build fail:

1. **Không push.**
2. Kiểm tra code mới từ Dev có dùng schema V1 không (`.FormSchemaVersion`, `VisitRequest.DelegationName/.Purpose/.WorkingContent/.WorkingLanguage/.MediaConsent*`, compatibility projection, dual-read, fallback request-level).
3. Sửa theo Pure V2: đọc từ `VisitInstanceFormDetail` của **đúng** campus instance; guest lấy qua `VisitInstanceGuestMember` của chính campus đó.
4. Kiểm tra harness test: nếu test dựng **service thật**, harness không được `Ignore<VisitInstanceFormDetail>()` hay `Ignore<VisitInstanceGuestMember>()`, và phải seed 1 detail cho mỗi instance.
5. Chạy full UnitTests **và** IntegrationTests.
6. Chỉ tiếp tục khi toàn bộ gate xanh.

### Giới hạn của SchemaContractTests

`SchemaContractTests` bảo vệ **schema ↔ EF mapping** (cột không tồn tại, nullability, FK delete rule). Nó **không** thay thế compiler gate: truy cập một property C# đã bị xóa chỉ bị bắt khi build. Hai lớp bảo vệ này không thay thế nhau.

---

## 9. SQL CANONICAL

**Không thay đổi trong phiên này.** Hash vẫn `7ec63e90…`, blob `825b9567…`, 14,832 dòng.

`CanonicalSqlScript.ExpectedSha256` đang pin đúng hash này. Khi Phase 6 sửa SQL (GET DIAGNOSTICS, 151 placeholder, 3 agenda, self-check discriminator), **bắt buộc** cập nhật hằng số này trong cùng commit — `CanonicalSqlScriptTests.Canonical_script_hash_matches_the_pinned_value` sẽ fail cho tới khi cập nhật, đúng như thiết kế.

---

## 10. QUERY CONSUMER MATRIX (runtime)

**Chưa lập.** §11 yêu cầu matrix có bằng chứng **runtime**, phụ thuộc integration test chạy được → phụ thuộc Phase 1. Không ghi `static-PASS` (bị cấm).

---

## 11. COMMIT ĐÃ TẠO (local, chưa push)

| Hash | Slice |
|---|---|
| `fc647d89` | `chore(security): externalize runtime secrets and validate configuration` |
| `400cf598` | `fix(test-infra): restore solution build and fail-close canonical SQL bootstrap` |
| `a88853fe` | `fix(visit-photos): enforce the database uploader rule in the application layer` |

Không push, không mở PR, không merge, không rewrite history. Không có tên AI trong subject/body/trailer. Mọi commit đều build được.

---

## 12. VIỆC CÒN LẠI

**Phase 1:** ✅ **HOÀN TẤT** — build 0 error, 926/926 unit, 517/517 integration, 5/5 schema contract, GAP-001/004/006/011/014/017 VERIFIED.

**Nợ kỹ thuật nhỏ ghi nhận, chưa làm (không chặn gate):** một số assertion `Assert.NotEqual("GLOBAL-DELEG", …)` trong các file V2 nay **rỗng nghĩa** vì literal đó không còn tồn tại ở đâu; và tham số `schemaVersion` trong các `Seed(...)` helper của bộ test V2 đã thành vết tích (nhánh `!isV2` không còn dùng). Nên dọn ở Phase 7 để test không đọc như đang bảo vệ thứ nó không bảo vệ.

**Phase 2:** create/OTP/verify/pending-edit/resubmit/safe-edit ghi detail **riêng từng campus** (DECISION-01); viết lại `VisitFormReadService` Pure V2; claim/transfer/amendment.

**Phase 3:** ~40 downstream consumer đọc đúng instance detail; scope-before-keyword; lập QUERY CONSUMER MATRIX runtime.

**Phase 4:** bỏ 2 feature flag (GAP-005); capability endpoint luôn `enabled=true`; frontend bỏ `formSchemaVersion` (GAP-008); bỏ route `unsupported-version`.

**Phase 5:** contract test Translation/Gallery/FAQ/Partner/Vision/Expense (audit cho thấy tên cột đã khớp 100% → nhiều khả năng `VERIFIED — NO CODE CHANGE`).

**Phase 6:** sửa `GET DIAGNOSTICS` ordering (GAP-009); 151 placeholder → 0; 3 agenda; bổ sung self-check discriminator; re-import + rerun; **cập nhật hash**.

**Phase 7:** cleanup dead code/config/docs.

---

## 13. VIỆC CHỦ DỰ ÁN PHẢI LÀM

1. ⛔ **ROTATE đủ 4 credential** (chặn công nhận toàn dự án READY):
   1. Gmail app password (SMTP)
   2. JWT signing key
   3. Google OAuth client secret
   4. Google OAuth refresh token

   Toàn dự án chỉ được kết luận `READY` sau khi chủ dự án xác nhận **cả bốn** đã rotate.
   Giá trị credential không được in ra terminal, báo cáo, commit hay chat.
2. Xác nhận có muốn rewrite Git history hay không (ảnh hưởng 5 branch — cần phê duyệt rõ ràng, ngoài phạm vi phiên này).
3. Quyết định thời điểm chạy Phase 1 tiếp theo (`git stash pop`).

---

## 14. DEFINITION OF DONE

```
[x] Làm việc đúng branch tracking origin/Cảnh-Iter1; không sửa Dev.
[x] Credential thật đã bị xóa khỏi HEAD.
[ ] Chủ dự án xác nhận SMTP password và JWT secret cũ đã rotate.     ← BLOCKED
[x] Production lấy secret từ environment và fail rõ khi thiếu.
[x] dotnet build PEMS.slnx = 0 error.
[x] Architecture tests xanh (14/14).
[x] Unit tests xanh (926/926), không bị skip do build.
[x] Integration tests CHẠY THẬT và xanh (517/517) trên DB disposable.
[x] Integration bootstrap fail-closed.
[x] Bootstrap import đúng một SQL canonical và verify đúng hash.
[x] SQL fresh import chạy thật trong phiên này (mỗi lần chạy integration).
[ ] Mọi SQL issue_count = 0.                                          ← Phase 6
[ ] Negative guard 14/14 PASS thật.                                   ← Phase 6
[ ] 151 placeholder về 0.                                             ← Phase 6
[ ] 3 operational instance thiếu agenda về 0.                         ← Phase 6
[ ] Self-check xác minh không còn discriminator ở cả hai bảng.        ← Phase 6
[x] Không còn 12 phantom EF mapping.
[x] EF contract test kiểm mọi mapping trên schema thật (SchemaContractTests).
[x] EF DeleteBehavior khớp FK canonical (GAP-014) + test chặn drift.
[x] Nullability khớp SQL/CLR/EF/DTO/validator (GAP-017) + test hai chiều.
[x] Không còn runtime read/write form_schema_version.
[x] Không còn runtime read/write 10 global-form column.
[ ] Create/OTP/edit/resubmit/safe-edit ghi detail riêng từng campus.  ← Phase 2
[ ] Không fallback operational contact request-level.                 ← Phase 2
[ ] Guest/support members giữ đúng campus.                            ← Phase 2
[ ] Claim/transfer/amendment/revision/idempotency xanh.                ← Phase 2
[ ] Downstream query đọc đúng instance detail.                         ← Phase 3
[ ] Scope-before-keyword và cross-campus isolation được test.          ← Phase 3
[ ] Frontend không route/render theo formSchemaVersion.                ← Phase 4
[ ] Frontend không phụ thuộc capability trước khi mở V2.               ← Phase 4
[ ] Capability endpoint luôn enabled cho client cũ.                    ← Phase 4
[ ] Frontend lint, unit test và build xanh (chạy lại sau Phase 4).
[ ] Translation/Gallery/FAQ/Partner/Vision/Expense runtime contract.   ← Phase 5
[ ] Real-stack critical E2E xanh.                                      ← Phase 6
[x] Không còn stale SQL runtime/test reference.
[x] Không còn V1 service/handler reachable.
[x] Không regression permission/audit/notification/idempotency.        ← 517/517 integration PASS
[x] Commit gom theo chức năng, không có tên AI.
```

Legend: `[x]` đạt · `[~]` một phần / trong stash · `[ ]` chưa · `←` phase phụ trách
