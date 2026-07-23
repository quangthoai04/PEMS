# PEMS PURE V2-ONLY — IMPLEMENTATION REPORT

**Phiên:** IMPLEMENTATION (sau audit)
**Ngày:** 2026-07-23
**Branch:** `Canh-Iter1` (tracking `origin/Cảnh-Iter1`) — không chạm `Dev`

---

## 1. KẾT LUẬN

> ## ⛔ NOT READY
>
> **Phase 0 hoàn tất và đã verify.** Phase 1–7 **chưa hoàn tất**.
> Ngoài ra credential rotation chưa được xác nhận → `SECURITY ROTATION PENDING`.

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
| DECISION-01 — operational contact riêng từng campus | ⏳ chưa tới (Phase 2) |
| DECISION-02 — Pure V2-only, bỏ dual-read/fallback | 🔶 một phần (V1 dead code đã xóa; discriminator chưa gỡ hết) |
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
| **GAP-001** | 12 phantom EF mapping | 🔶 **IN PROGRESS** — đã xóa, còn 183 consumer lỗi (stash) |
| **GAP-004** | Write path ghi cột đã xóa | 🔶 **IN PROGRESS** |
| **GAP-006** | Dual-read theo discriminator | 🔶 **IN PROGRESS** — 64/~100 site đã collapse |
| **GAP-011** | V1 service còn DI | 🔶 **IN PROGRESS** — đã xóa, nằm trong stash |
| **GAP-005** | Feature flag deadlock | ⬜ **OPEN** (Phase 4) |
| **GAP-008** | Frontend route theo `formSchemaVersion` | ⬜ **OPEN** (Phase 4) |
| **GAP-009** | 14 false-fail negative guard (`GET DIAGNOSTICS`) | ⬜ **OPEN** (Phase 6) |
| **GAP-014** | FK delete behavior drift | ⬜ **OPEN** (Phase 1B) |
| **GAP-015** | 151 seed placeholder | ⬜ **OPEN** (Phase 6) |
| **GAP-016** | 3 instance thiếu agenda | ⬜ **OPEN** (Phase 6) |
| **GAP-017** | Nullability lệch (email_templates, news_content_sections) | ⬜ **OPEN** (Phase 1B) |
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

## 5. PHASE 1–2 — DANG DỞ, ĐÃ STASH 🔶

### Đã làm (nằm trong `stash@{0}`)

1. **Xóa đúng 12 phantom mapping** — `VisitRequest` (11) + `VisitRequestPendingForm` (1).
2. **Collapse 64 ternary discriminator** — `cond ? <V2> : <V1>` → `<V2>`, bằng scanner cân bằng dấu (không phải regex).
3. **Xóa V1 dead code** đã chứng minh unreachable (0 dispatch từ `PEMS.Api`):
   `CreateAuthenticatedVisitRequest`, `VerifyAndCreateVisitRequest`, `UpdatePendingVisitRequest`, `ResubmitRejectedVisitRequest`, `InitiateVisitRequest`, `VisitRequestService`, `IVisitRequestService`, + 3 file test V1.
4. **Chuyển `InitiateVisitRequestResponse`** sang namespace Pure V2 `Commands.VisitRequestOtp` — DTO này dùng chung cho initiate-v2/resend/recover đang sống nhưng lại nằm trong folder V1.

### Còn lại: **183 lỗi compile / 47 file**

| Member | Số lỗi |
|---|---|
| `FormSchemaVersion` | 38 |
| `DelegationName` | 37 |
| `MediaConsentStatus` | 14 |
| `Purpose` / `WorkingContent` | 11 mỗi loại |
| `VisitType` | 10 |
| `NoteToFptu` / `TransportationNote` / `MediaConsentNote` / `VisitTypeOther` / `WorkingLanguage` | 8 mỗi loại |

Điểm nặng nhất: `VisitFormReadService.cs` (22) — lõi dual-read, cần **viết lại** chứ không collapse máy móc; và 6 handler có khối copy 10 field (`GetEditableVisitRequestDetail`, `GetSubmittedVisitRequestFormDetail`, `GetVisitInstanceSummary`, `GetVisitProcessDetail`, `GetVisitInstanceContribution`, `GetStaffCalendarDetail`).

### Vì sao stash thay vì commit

Mỗi commit phải build được (§17). Cây mã dở dang không build → **tệ hơn HEAD**. Stash giữ nguyên 100% công việc và khôi phục được:

```bash
git stash list      # stash@{0} = WIP Pure V2 phase1/2
git stash pop       # tiếp tục từ đúng chỗ đang dở
```

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
| `dotnet build PEMS.slnx` | ✅ **Build succeeded, 0 Error(s)** |
| `dotnet test tests/PEMS.ArchitectureTests` | ✅ **14/14 passed** |
| `dotnet test tests/PEMS.UnitTests` | ✅ **937/937 passed** (trước phiên: **không build được**) |
| `dotnet test tests/PEMS.IntegrationTests` | ⏳ **chưa chạy** — xem §8 |
| Frontend `lint` / `test:unit` / `build` | ⏳ chưa chạy lại (Phase 4 chưa bắt đầu; phiên audit trước: lint 0 error, 389/389, build OK) |
| SQL import disposable | ⏳ chưa chạy lại trong phiên này (phiên audit: import + rerun OK, 81 bảng, 0 `pems_seed_*`) |
| E2E real-stack | ⏳ chưa chạy |

Unit test: **932 → 937** (thêm test contract đọc photo folder + unauthenticated).

---

## 8. VÌ SAO CHƯA CHẠY INTEGRATION TEST

Bootstrap mới **fail-closed đúng thiết kế**, nhưng bộ integration test hiện có được viết cho schema cũ. Chạy lúc này sẽ fail vì lý do không liên quan (entity vẫn map 12 cột phantom — GAP-001 nằm trong stash), nên kết quả **không chứng minh được gì**.

Integration test chỉ nên chạy sau khi Phase 1 hoàn tất (EF khớp schema). Đây là lựa chọn có chủ đích, **không phải bỏ qua**.

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

**Phase 1 (tiếp tục từ stash):** sửa 183 lỗi / 47 file; rà FK `DeleteBehavior` (GAP-014); nullability (GAP-017); thêm schema contract test chạy trên MySQL thật.

**Phase 2:** create/OTP/verify/pending-edit/resubmit/safe-edit ghi detail **riêng từng campus** (DECISION-01); viết lại `VisitFormReadService` Pure V2; claim/transfer/amendment.

**Phase 3:** ~40 downstream consumer đọc đúng instance detail; scope-before-keyword; lập QUERY CONSUMER MATRIX runtime.

**Phase 4:** bỏ 2 feature flag (GAP-005); capability endpoint luôn `enabled=true`; frontend bỏ `formSchemaVersion` (GAP-008); bỏ route `unsupported-version`.

**Phase 5:** contract test Translation/Gallery/FAQ/Partner/Vision/Expense (audit cho thấy tên cột đã khớp 100% → nhiều khả năng `VERIFIED — NO CODE CHANGE`).

**Phase 6:** sửa `GET DIAGNOSTICS` ordering (GAP-009); 151 placeholder → 0; 3 agenda; bổ sung self-check discriminator; re-import + rerun; **cập nhật hash**.

**Phase 7:** cleanup dead code/config/docs.

---

## 13. VIỆC CHỦ DỰ ÁN PHẢI LÀM

1. ⛔ **ROTATE credential** (chặn deploy — ưu tiên cao nhất):
   - Gmail app password (SMTP)
   - JWT signing key
   - Google OAuth client secret + refresh token (Drive)
   Sau đó cấp qua environment variable / secret manager. **Không** ghi giá trị vào repo.
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
[x] Unit tests xanh (937/937), không bị skip do build.
[x] Integration bootstrap fail-closed.
[x] Bootstrap import đúng một SQL canonical và verify đúng hash.
[ ] SQL fresh import + rerun trong phiên này.                         (phiên audit: đạt)
[ ] Mọi SQL issue_count = 0.                                          ← Phase 6
[ ] Negative guard 14/14 PASS thật.                                   ← Phase 6
[ ] 151 placeholder về 0.                                             ← Phase 6
[ ] 3 operational instance thiếu agenda về 0.                         ← Phase 6
[ ] Self-check xác minh không còn discriminator ở cả hai bảng.        ← Phase 6
[~] Không còn 12 phantom EF mapping.                                  ← đã xóa, trong stash
[ ] EF contract test materialize toàn bộ mapping trên schema thật.    ← Phase 1C
[~] Không còn runtime read/write form_schema_version.                 ← 64 site xong, 183 lỗi còn lại
[ ] Không còn runtime read/write 10 global-form column.               ← Phase 2
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
[~] Không còn V1 service/handler reachable.                            ← đã xóa, trong stash
[ ] Không regression permission/audit/notification/idempotency.        ← cần integration test
[x] Commit gom theo chức năng, không có tên AI.
```

Legend: `[x]` đạt · `[~]` một phần / trong stash · `[ ]` chưa · `←` phase phụ trách
