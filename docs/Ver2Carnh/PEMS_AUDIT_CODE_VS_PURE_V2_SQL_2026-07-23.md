# PEMS — AUDIT ĐỒNG BỘ CODE ↔ SQL PURE V2 CANONICAL

**Phiên:** AUDIT + LẬP KẾ HOẠCH (không sửa code)
**Ngày:** 2026-07-23
**Người thực hiện:** Senior Architect audit pass, đọc repo thật + import SQL thật trên MySQL 8 disposable

---

## 1. EXECUTIVE SUMMARY

| Mục | Giá trị |
|---|---|
| Branch audited | local `Canh-Iter1` → tracking `origin/Cảnh-Iter1` |
| HEAD thực tế | `19bed5101b8b3bd564d438e6add90c67a2f83fa6` ("sql v2") |
| HEAD trong prompt | `19bed510…` — **KHỚP**, không có commit mới |
| Merge-base với `Dev` | `584f3ddace324eb0b4f6916ca586e6f1b2e05090` |
| Trạng thái vs `Dev` | ahead 11, behind 0 |
| Working tree | sạch; chỉ có `docs/Ver2Carnh/` untracked (chính prompt) |
| SQL canonical blob | `825b95672491d653d5537c95b4e81f3c000b229f` ✅ khớp |
| SQL canonical SHA-256 | `7ec63e9044ecd1910e9a7137c99773bb13b36902f3042fd7bc6cfce402892415` ✅ khớp |
| Số dòng SQL | 14,832 ✅ khớp |
| Import MySQL 8 disposable | ✅ THÀNH CÔNG (exit 0, stderr rỗng), rerun ✅ idempotent |

### Kết quả build/test **thực chạy**

| Gate | Kết quả |
|---|---|
| `dotnet build PEMS.slnx` | ❌ **FAIL — 1 error** (`GalleryTestDbContext`) |
| Backend production (`PEMS.Api` + deps) | ✅ PASS, 0 error, 181 warning |
| `PEMS.ArchitectureTests` | ✅ **14/14 PASS** |
| `PEMS.UnitTests` | ❌ **KHÔNG BUILD ĐƯỢC** → không chạy được test nào |
| `PEMS.IntegrationTests` (build) | ✅ build OK |
| `PEMS.IntegrationTests` (run) | ⛔ **CỐ Ý KHÔNG CHẠY** — bootstrap trỏ file SQL không tồn tại và fail-open; chạy sẽ không chứng minh được gì (xem GAP-002) |
| Frontend `npm run build` | ✅ PASS (built in 53.42s) |
| Frontend `npm run lint` (`tsc --noEmit`) | ✅ PASS, 0 error |
| Frontend `npm run test:unit` | ✅ **389/389 PASS** (28 files) |
| E2E / realstack | ⛔ không chạy — phụ thuộc bootstrap DB đang hỏng + outbound sink chưa xác nhận |

### Số lượng gap

| Severity | Số lượng |
|---|---|
| **P0** | **7** |
| P1 | 5 |
| P2 | 6 |
| P3 | 3 |

### KẾT LUẬN

> ## ⛔ HỆ THỐNG **KHÔNG THỂ CHẠY** VỚI SQL MỚI. **KHÔNG READY.**

Bằng chứng quyết định (không phải suy đoán):

1. **EF model map 12 cột không tồn tại** trên đúng 2 entity. Đã chứng minh bằng lệnh chạy thật trên schema vừa import:
   ```
   ERROR 1054 (42S22): Unknown column 'v.form_schema_version' in 'field list'
   ERROR 1054 (42S22): Unknown column 'p.form_schema_version' in 'field list'
   ```
   → **Mọi** query EF materialize `VisitRequest` hoặc `VisitRequestPendingForm` fail ngay lập tức. Đây là toàn bộ nghiệp vụ visit request.

2. **Solution không compile** — `dotnet build PEMS.slnx` = 1 error → không thể chạy unit test.

3. **Feature flag deadlock**: cả 2 flag mặc định `false`, và **không** appsettings nào được deploy bật chúng (chỉ `appsettings.Testing.json` — file này **bị gitignore**). V1 đã trả `410 GONE`. → Ở Dev/Prod, **không ai tạo được đơn tham quan**.

**Điểm sáng cần ghi nhận (đã kiểm chứng, không phải giả định):**

- SQL canonical import sạch, rerun idempotent, **81 persistent tables**, **0 view**, **0 object `pems_seed_*`**, direct-seed 19/90/8 đúng như tuyên bố.
- Ánh xạ entity↔table là **81/81, không có bảng nào thiếu entity**. Sai lệch cột **chỉ tập trung ở 2 file** — phạm vi sửa hẹp và rõ ràng.
- Toàn bộ module Translation/Gallery/FAQ/Partner/Vision/Expense **khớp schema ở mức tên cột** (0 mismatch).
- Frontend build + typecheck + 389 unit test đều xanh.

---

## 2. BASELINE VÀ EVIDENCE

### 2.1 Preflight repository

```
git rev-parse HEAD      → 19bed5101b8b3bd564d438e6add90c67a2f83fa6
git branch --show-current → Canh-Iter1
git merge-base Dev HEAD  → 584f3ddace324eb0b4f6916ca586e6f1b2e05090
git rev-list --left-right --count Dev...HEAD → 0   11
git status --short --branch → ## Canh-Iter1...origin/Cảnh-Iter1
                              ?? docs/Ver2Carnh/
```

**Ghi chú tên branch:** prompt yêu cầu branch `Cảnh-Iter1` (có dấu). Bản local checkout tên `Canh-Iter1` (không dấu) nhưng **tracking đúng** `origin/Cảnh-Iter1` và HEAD trùng khớp tuyệt đối → đúng branch cần audit. Không thực hiện checkout/reset/rebase.

`git diff --stat Dev...HEAD` → 60 file, +3,931 / −1,020. Phân bố khớp mô tả prompt (backend chủ yếu là query handler; Domain chỉ thêm `VisitRequestFingerprintGuard.cs`; Infrastructure chỉ sửa `ApplicationDbContext.cs`).

### 2.2 Xác minh SQL canonical

```
path   : docs/database/scripts/PEMS_FULL_V2_ONLY_CANONICAL_..._DIRECT_SEED_NO_STAGING_LATEST (1).sql
blob   : 825b95672491d653d5537c95b4e81f3c000b229f          ✅
sha256 : 7ec63e9044ecd1910e9a7137c99773bb13b36902f3042fd7bc6cfce402892415  ✅
wc -l  : 14832                                             ✅
bytes  : 1,479,602
```

> ⚠️ **Cảnh báo phương pháp đo:** PowerShell `Get-Content | Measure-Object -Line` trả **13,825** vì nó bỏ qua dòng trống. `wc -l` trả đúng **14,832**. Khi verify line count phải dùng `wc -l`, không dùng `Measure-Object -Line`.

Thư mục `docs/database/scripts/` hiện chỉ còn **duy nhất 1 file .sql** ở root → không có ambiguity về canonical.

### 2.3 Static scan no-staging

```
CREATE TABLE      : 81
CREATE VIEW       : 0
CREATE TRIGGER    : 32
CREATE PROCEDURE  : 2   (đều DROP sau khi CALL → routines cuối cùng = 0, ĐÚNG THIẾT KẾ)
CREATE TABLE/VIEW `pems_seed_*` : 0   ✅
```

Toàn file chỉ có **2 hit** `pems_seed_` — cả hai ở dòng 14783 và 14829, đều là guard check cuối file (`LEFT(table_name,10)='pems_seed_'`). ✅ Đúng đặc tả delta.

### 2.4 Giới hạn môi trường (khai báo trung thực)

- **Không có Docker** trên máy audit. Đã dùng phương án 2 của prompt: bản copy tạm **ngoài repository** (`scratchpad/canonical_retargeted.sql`), retarget sang `pems_schema_audit_20260723140353`.
- MySQL: instance local **8.0.46** — instance này **có chứa `pems_db` thật**. Vì vậy safety proof là bắt buộc và đã thực hiện (§2.5). File canonical trong repo **không bị sửa**.
- **Integration test không được chạy** (lý do ở GAP-002). Đây là giới hạn có chủ đích, không phải "chưa có thời gian".
- **E2E/realstack không chạy**: cần DB bootstrap đúng + xác nhận sink email/translation/Vision, cả hai chưa thỏa.

### 2.5 Safety proof trước khi import

Sau khi retarget, quét lại toàn bộ file tạm:

```
grep '`pems_db`'  → chỉ 3 hit, tất cả ở dòng 79/80/81, đều là COMMENT (`-- …`)
USE / CREATE DATABASE / DROP DATABASE → đúng 2 câu, cả hai đã trỏ pems_schema_audit_20260723140353
SOURCE / \.  (client command) → 0
```

Kết quả `SELECT @@hostname, @@port, @@version` được in trước import; target xác nhận là DB audit dùng-một-lần.

### 2.6 Kết quả import

| Kiểm tra | Kết quả |
|---|---|
| Import lần 1 | exit **0**, stderr **rỗng**, 8.44s |
| Import lần 2 (rerun trên chính DB đó) | exit **0**, stderr rỗng → ✅ rerunnable fresh-create |
| `DATABASE()` | `pems_schema_audit_20260723140353` ✅ |
| base tables | **81** ✅ |
| views | **0** ✅ |
| `pems_seed_*` objects | **0** ✅ |
| triggers | **32** ✅ |
| routines (sau import) | **0** ✅ (2 procedure bị DROP sau CALL — đúng thiết kế) |
| foreign keys | 251 |
| check constraints | 50 |
| cột `form_schema_version` (toàn DB) | **0** ✅ |
| 10 cột global-form trên `visit_requests` | **0** ✅ |

Kiểm tra **độc lập** qua `information_schema` (không dựa vào procedure PASS), đúng như prompt §5.3 yêu cầu — xác nhận cả `visit_requests.form_schema_version` **và** `visit_request_pending_forms.form_schema_version` đều vắng mặt.

### 2.7 Invariant Pure V2 trên dữ liệu seed

| Invariant | Kết quả |
|---|---|
| requests / campuses / details | 117 / 204 / **204** |
| instance thiếu detail | **0** ✅ |
| orphan detail | **0** ✅ |
| duplicate detail (>1 / instance) | **0** ✅ |
| request không có campus | **0** ✅ |
| `SINGLE_CAMPUS` ≠ 1 instance | **0** ✅ |
| `MULTI_CAMPUS` < 2 instance | **0** ✅ |
| visit < 30 phút | **0** ✅ |
| member link gaps | **0** ✅ |
| primary contact state mismatch | **0** ✅ |
| baseline revision (form + request) | **0** issue ✅ |

---

## 3. SQL DELTA SUMMARY (`5165e088…` → `7ec63e90…`)

| Chỉ số | Cũ | Mới | Ghi chú |
|---|---|---|---|
| CREATE TABLE | 82 | **81** | −1: bỏ staging table |
| CREATE VIEW | 1 | **0** | −1: bỏ helper view |
| Persistent runtime tables | 81 | **81** | **KHÔNG đổi** |
| Triggers | 32 | **32** | không đổi |
| Procedures | 2 | **2** | không đổi |

**Persistent runtime DDL: KHÔNG thay đổi.** Toàn bộ delta nằm ở cơ chế seed.

Hai object seed-only đã bị loại bỏ hoàn toàn:
```
pems_seed_visit_request_form_v2      (staging table)
pems_seed_visit_requests_v2_compat   (helper view)
```

Assertion cuối file đã đổi từ kiểm tra một staging table cụ thể → **từ chối mọi object prefix `pems_seed_`**:
```
PURE_V2_REFUSED_SEED_HELPER_OBJECT_PRESENT
pure_v2_seed_helper_objects
```

### 3.1 SEED PHASE MATRIX

| Trường | Batch 1 | Batch 2 | Batch 3 |
|---|---|---|---|
| `visit_requests` INSERT | dòng 4821 | dòng 5782 | dòng 9300 |
| `visit_request_campuses` INSERT | dòng 4843 | dòng 5875 | dòng 9315 |
| `visit_instance_form_details` INSERT | dòng **4879** | dòng **6041** | dòng **9335** |
| Request templates | **19** | **90** | **8** |
| Đo bằng | 18 `UNION ALL SELECT` + 1 SELECT đầu | 89 + 1 | 7 + 1 |
| Insert order | campus **trước**, detail **ngay sau** | idem | idem |
| Cơ chế | `INSERT … SELECT FROM visit_request_campuses vrc JOIN visit_requests vr JOIN (inline derived table) sf` | idem | idem |
| Staging | **KHÔNG** — derived table inline | không | không |
| Detail rows | = số campus rows của batch | idem | idem |
| Authorship | `created_at = vrc.created_at`, `created_by = COALESCE(vr.created_by, vr.visitor_user_id)` | idem | idem |
| `form_revision` / `approval_revision` / `row_version` | 1 / 1 / 0 | idem | idem |

**Tổng: 19 + 90 + 8 = 117 = đúng số `visit_requests` trong DB sau import.** ✅
**campus rows = detail rows ở mọi batch.** ✅

**Enrichment order:** các câu UPDATE ghi **trực tiếp** vào `visit_instance_form_details`; không có staging→backfill cuối file. Downstream consumer (news, feedback, agenda, logistics, minutes, notification, calendar, sent email) đọc trực tiếp `visit_requests + visit_request_campuses + visit_instance_form_details`, và đều nằm **sau** dòng 9335 → không đọc detail trước khi detail tồn tại. Baseline revision snapshot check trả 0 issue → phản ánh dữ liệu sau enrichment.

### 3.2 ⚠️ RANH GIỚI SEED CONTRACT vs RUNTIME CONTRACT

Khối direct-seed khởi tạo `operational_contact_*` của **mọi** campus từ **một** giá trị request-level:

```sql
vr.contact_person_full_name,
NULLIF(TRIM(vr.contact_person_organization), ''),
vr.contact_person_phone,
NULLIF(TRIM(vr.contact_person_email), ''),
```

> **Đây là seed convenience, TUYỆT ĐỐI KHÔNG phải quy tắc runtime.**
> Backend **không được** copy contact request-level sang mọi campus khi user nhập khác nhau. Tương tự, comment "Preserve V1 shared-member semantics" trong seed **không** trở thành ràng buộc runtime bắt mọi member xuất hiện ở mọi campus.

### 3.3 Tên SQL cũ đã xóa + consumer còn trỏ sai

`docs/database/scripts/PEMS_FULL_V2_TRANSLATION_GALLERY_FULL.sql` đã bị xóa ở commit `19bed510`. Ngoài ra nhiều tên SQL lịch sử khác cũng không còn tồn tại. Consumer còn trỏ sai — xem GAP-002/GAP-003/GAP-012/GAP-013.

---

## 4. FULL SCHEMA–CODE MATRIX

Phương pháp: script tự động parse **mọi** `[Table("…")]` + `[Column("…")]` trong `backend/PEMS.Domain/Entities/**`, đối chiếu với `information_schema.columns` của schema vừa import.

### 4.1 Kết quả tổng hợp

```
Entity có [Table]        : 81
DB base tables           : 81
Bảng DB KHÔNG có entity  : 0        ✅
Entity trỏ bảng không tồn tại : 0   ✅
Cột entity KHÔNG tồn tại trong DB : 12  ❌  (tập trung ở đúng 2 file)
```

**Đây là tin tốt quan trọng:** độ phủ bảng là 81/81 hoàn hảo. Toàn bộ sai lệch schema-level nằm gọn trong 2 file.

### 4.2 12 cột phantom (P0)

| Entity | File:line | Cột map | Tồn tại trong DB? |
|---|---|---|---|
| `VisitRequestPendingForm` | `VisitRequestPendingForm.cs:30` | `form_schema_version` | ❌ |
| `VisitRequest` | `VisitRequest.cs:53` | `form_schema_version` | ❌ |
| `VisitRequest` | `VisitRequest.cs:83` | `delegation_name` | ❌ |
| `VisitRequest` | `VisitRequest.cs:89` | `visit_type` | ❌ |
| `VisitRequest` | `VisitRequest.cs:92` | `visit_type_other` | ❌ |
| `VisitRequest` | `VisitRequest.cs:94` | `purpose` | ❌ |
| `VisitRequest` | `VisitRequest.cs:97` | `working_content` | ❌ |
| `VisitRequest` | `VisitRequest.cs:116` | `working_language` | ❌ |
| `VisitRequest` | `VisitRequest.cs:122` | `transportation_note` | ❌ |
| `VisitRequest` | `VisitRequest.cs:125` | `media_consent_status` | ❌ |
| `VisitRequest` | `VisitRequest.cs:128` | `media_consent_note` | ❌ |
| `VisitRequest` | `VisitRequest.cs:131` | `note_to_fptu` | ❌ |

`visit_requests` thực tế có **36 cột**; `visit_request_pending_forms` có **8 cột** (không có discriminator).

### 4.3 Trạng thái theo nhóm module

| Nhóm bảng | Entity | Cột khớp | Status | Ghi chú |
|---|---|---|---|---|
| `visit_requests`, `visit_request_pending_forms` | ✅ | ❌ 12 phantom | **FAIL (P0)** | GAP-001 |
| `visit_request_campuses`, `visit_instance_form_details`, `visit_guest_members`, `visit_instance_guest_members` | ✅ | ✅ | PASS | lõi Pure V2 khớp |
| `visit_request_identity_changes` (+`_events`), `visit_instance_amendments` (+`_changes`), revision history ×2 | ✅ | ✅ | PASS | guard column VIRTUAL cố ý không map ✅ |
| `visit_request_fingerprint_guards` | ✅ | ✅ | PASS | entity mới ở HEAD |
| `visit_photo_folders`, `visit_photos`, `visit_photo_face_scans`, `visit_photo_face_detections`, `photo_face_tags` | ✅ | ✅ | PASS | |
| `visit_expense_reports` / `_items` / `_report_events` | ✅ | ✅ | PASS | `total_amount` STORED GENERATED, đã có `[DatabaseGenerated(Computed)]` ✅ |
| `partner_translations`, `faq_translations`, `gallery_item_contents` | ✅ | ✅ | PASS | |
| `gallery_areas/locations/items/item_media` | ✅ | ✅ | PASS | `gallery_items.description` **không** còn map ✅ |
| `gallery_item_tts_audios` | — | — | PASS | **0 reference** toàn repo ✅ |
| `documents.owner_type` | ✅ | ✅ | PASS | enum DB đã có `VISIT_INSTANCE_MEDIA` ✅ |
| `partner_contacts.avatar_file_id` | ✅ | ✅ | PASS | |
| `audit_logs` / `audit_log_changes` (context/metadata mới) | ✅ | ✅ | PASS | |

> **Lưu ý đúng theo yêu cầu prompt:** matrix trên xác nhận **tên bảng + tên cột + nullability + generated column**. Nó **chưa** xác nhận được ở mức chạy thật: type/length/precision, composite FK behavior, và write round-trip cho từng bảng — vì `PEMS.UnitTests` không build và integration bootstrap hỏng. Do đó các dòng PASS ở trên là **PASS ở mức contract tĩnh**, chưa phải PASS ở mức runtime. Không được coi là bằng chứng đầy đủ.

### 4.4 Nullability mismatch (đã quét tự động)

| Table | Column | CLR | DB | Mức |
|---|---|---|---|---|
| `email_templates` | `purpose` | `string?` | NOT NULL, no default | P2 |
| `news_content_sections` | `section_title` | `string?` | NOT NULL, no default | P2 |
| `news_content_sections` | `section_body_html` | `string?` | NOT NULL, no default | P2 |
| `visit_expense_items` | `total_amount` | `decimal` | NULLABLE (STORED GENERATED) | P3 |

### 4.5 DbSet coverage

`IApplicationDbContext` expose **75** DbSet; `ApplicationDbContext` có **80**. 5 entity chỉ truy cập được qua concrete context (không qua interface):

```
ApiConfigurationHeader, AuditLogChange, CalendarEventAttendee,
CalendarEventReminder, FeedbackRatingItem
```
→ P3 (chủ ý hoặc thiếu sót, cần xác nhận), không phải lỗi runtime.

---

## 5. V1 / LEGACY REMNANT INVENTORY

### 5.1 Quy mô

```
'FormSchemaVersion|form_schema_version|formSchemaVersion|PER_CAMPUS_V2_MIN'
  → 112 file, 309 hit  (backend / frontend / tests)
```

### 5.2 Phân loại (đúng yêu cầu: KHÔNG xóa mù quáng)

#### A. RUNTIME DEPENDENCY — phải xóa (P0)

Đọc `VisitRequest.FormSchemaVersion` (property map cột đã xóa → query fail):

| File:line | Vai trò |
|---|---|
| `VisitFormReadService.cs:70` | `var isV2 = request.FormSchemaVersion >= FormSchemaVersions.PerCampus` — **trái tim dual-read** |
| `VisitFormReadService.cs:324` | gán vào `ResolvedVisitFormDto` |
| `VisitFormReadService.cs:368` | nhánh isV2 thứ 2 |
| `VisitInstanceEffectiveName.cs:31` | `c.VisitRequest!.FormSchemaVersion >= …` trong **projection EF** |
| `VisitInstanceEffectiveName.cs:40` | idem |

#### B. WRITE PATH ghi cột đã xóa (P0)

| File:line | Ghi gì | Reachable? |
|---|---|---|
| `VisitRequestV2CreateService.cs:135` | `FormSchemaVersion = FormSchemaVersions.PerCampus` | ✅ qua `/api/v2/visit-requests` |
| `VisitRequestV2CreateService.cs:147-156` | `DelegationName, VisitType, VisitTypeOther, Purpose, WorkingContent, WorkingLanguage` | ✅ |
| `VisitRequestV2EditService.cs:321-330` | 10 cột global | ✅ pending-edit |
| `VisitRequestV2EditService.cs:554-563` | 10 cột global | ✅ resubmit |
| `VisitSafeEditService.cs:288-297` | 10 cột global | ✅ **`PATCH /api/v2/visit-requests/{id}/safe-details`** (Controller:451) |
| `InitiateVisitRequestV2CommandHandler` | `VisitRequestPendingForm.FormSchemaVersion` | ✅ initiate OTP |
| `VisitRequestService.cs:149-163` | 10 cột global (V1 create) | ⚠️ DI-registered nhưng **không có route** |
| `UpdatePendingVisitRequestCommandHandler.cs:169-179` | 10 cột global (V1) | ⚠️ V1 route trả 410 |

> **Điểm nghiêm trọng nhất:** `VisitSafeEditService` **không phải dead code V1** — nó phục vụ một route **`/api/v2/`** đang sống.

#### C. HỢP LỆ — KHÔNG được xóa

| File:line | Lý do |
|---|---|
| `VisitRequestV2EditOps.cs:100-114` | ghi `detail.*` → `VisitInstanceFormDetail`, **đúng Pure V2** |
| `GetVisitInvitationByIdQueryHandler.cs:89-91` | `flat.X = d.X` — DTO nhận từ detail ✅ |
| `EmailActions/*.cs` (`result.DelegationName`) | field DTO, resolve qua `ResolveDelegationNameAsync(instance…)` ✅ |
| `UpdateAgendaTemplateCommandHandler.cs:73` | `template.VisitType` → bảng `agenda_templates`, **bảng khác** ✅ |
| `UpdateEmailTemplateCommandHandler.cs:32` | `template.Purpose` → `email_templates` ✅ |
| `VisitInstanceFormDetail.cs` | định nghĩa hợp lệ các field cùng tên ✅ |

#### D. FRONTEND route theo discriminator (P1)

| File:line | Hành vi |
|---|---|
| `visitVersionRouting.ts:11-15` | `PER_CAMPUS_V2_MIN = 2`, `isPerCampusV2(v) = (v ?? 0) >= 2` |
| `visitVersionRouting.ts:40-47` | **nếu không phải v2 → toàn bộ edit/resubmit/detail = `/dashboard/visit/unsupported-version`** |
| `VisitRequestManagement.tsx:359,1175,1178,1222` | dùng `row.formSchemaVersion` |
| `SubmittedVisitRequestDetailModal.tsx:92,103` | `isPerCampusV2(...)` quyết định render |
| `delegations.types.ts:766,948` / `visitRequestV2Api.ts:180` | contract field |

> **Failure mode cụ thể:** API không còn nguồn cấp `formSchemaVersion` → giá trị `undefined` → `isPerCampusV2` = false → **mọi dòng trong danh sách quản lý** rơi vào `unsupported-version`. Detail/Edit/Resubmit chết toàn bộ.

#### E. TEST V1 / comment lịch sử (P2/P3)

~11 file test integration + unit còn set/assert `FormSchemaVersion`; nhiều comment mô tả dual-read V1/V2 trong `IVisitFormReadService.cs:12-14`, `VisitCampusFormContent.cs:8`, `VisitRequestConstants.cs:126`, `PerCampusFormV2Options.cs:5-7`.

### 5.3 Feature flag (P0)

```
PerCampusFormV2Options.Enabled       = false   (mặc định, PerCampusFormV2Options.cs:14)
PerCampusFormV2WriteOptions.Enabled  = false   (mặc định)

appsettings.json             → 0 occurrence
appsettings.Development.json → 0 occurrence
appsettings.Production.json  → 0 occurrence
appsettings.Testing.json     → Enabled: true  ⚠️ NHƯNG file này bị .gitignore (dòng 53)
```

`PublicFeaturesController` trả `enabled = read && write` = **false** ở mọi môi trường được deploy.

**Đính chính một giả định trong prompt:** prompt lo ngại frontend sẽ "fail SAFE về V1". Code thực tế **không** làm vậy — `useVisitEntryCta.tsx:37-41,102-104` xử lý `disabled` bằng `notifyCapabilityDisabled()` → toast *"Cấu hình hệ thống không hợp lệ (V2 bị tắt)"*. Đây là **hard error rõ ràng, không phải silent downgrade** — thiết kế tốt hơn prompt giả định. Nhưng hậu quả cuối vẫn là **P0: không tạo được đơn**.

---

## 6. ENUM & STATE MACHINE MATRIX

Enum trích trực tiếp từ schema đã import:

| Bảng.cột | Giá trị trong SQL | Nhận xét |
|---|---|---|
| `visit_requests.status` | `PENDING_APPROVAL, PARTIALLY_APPROVED, APPROVED, REJECTED, CANCELLED` | có `PARTIALLY_APPROVED` (campus-independent approval) |
| `visit_requests.visit_scope` | `SINGLE_CAMPUS, MULTI_CAMPUS` | |
| `visit_requests.created_source` | `VISITOR_SUBMITTED, STAFF_CREATED` | |
| `visit_requests.primary_contact_access_status` | `PENDING_CONFIRMATION, ACTIVE` | |
| `visit_request_campuses.status` | `WAITING_REQUEST_APPROVAL, ASSIGNED, BEFORE_VISIT, DURING_VISIT, AFTER_VISIT, CLOSED, CANCELLED, REJECTED` | |
| `visit_request_campuses.decision_actor_role` | `STAFF_LEADER, STAFF` | |
| `visit_request_campuses.decision_source` | `STANDARD_CAMPUS_REVIEW, INTERNAL_SELF_HOST, INTERNAL_LEADER_ASSIGN` | |
| `visit_request_campuses.cancellation_actor_type` | `VISITOR, HOST` | |
| `visit_request_campuses.cancellation_source` | `SELF_SERVICE, EXTERNAL_CONFIRMATION` | |
| **`visit_instance_form_details.visit_type`** | `CAMPUS_TOUR, MEETING, WORKSHOP, SIGNING_CEREMONY, EXCHANGE, OTHER` | ⚠️ **chỉ còn trên detail** |
| **`visit_instance_form_details.working_language`** | `VI, EN` | ⚠️ chỉ còn trên detail |
| **`visit_instance_form_details.media_consent_status`** | `AGREED, DECLINED` | ⚠️ chỉ còn trên detail |
| `visit_request_identity_changes.change_kind` | `INITIAL_CLAIM, TRANSFER` | |
| `visit_request_identity_changes.status` | `PENDING, APPLIED, DECLINED, EXPIRED, CANCELLED, SUPERSEDED` | |
| `visit_request_identity_changes.confirmation_method` | `GOOGLE_SSO, OTP_FALLBACK` | |
| `visit_request_identity_changes.target_relation` | `PRIMARY_CONTACT` | |
| `visit_instance_amendments.status` | `DRAFT, PENDING_APPROVAL, APPROVED, REJECTED, WITHDRAWN, EXPIRED, CANCELLED` | |
| `documents.owner_type` | `GENERAL, VISIT, PARTNER, MINUTES, NEWS, LOGISTICS, REPORT, VISIT_INSTANCE_MEDIA` | ✅ đã có `VISIT_INSTANCE_MEDIA` |
| `documents.status` | `DRAFT, PUBLISHED, ARCHIVED` | |

**Phát hiện then chốt:** 3 enum `visit_type` / `working_language` / `media_consent_status` giờ **chỉ tồn tại trên `visit_instance_form_details`**. Mọi validator/DTO/frontend union nào còn coi chúng là thuộc tính request-level là sai contract.

`DocumentsOwnerTypeEnumConsistencyTests` — test bảo vệ enum này hiện **trỏ 1 file SQL không tồn tại** (GAP-003) → nó sẽ FAIL, không còn bảo vệ được gì.

---

## 7. QUERY & AUTHORIZATION MATRIX

> ⚠️ **Giới hạn phải nêu rõ:** phần này dựa trên đọc code tĩnh. **Không** xác minh được bằng chạy thật vì UnitTests không build và integration bootstrap hỏng. Trạng thái ghi là *static-PASS*, không phải runtime-PASS.

| Surface / API | Nguồn form | Mixed behavior | Status |
|---|---|---|---|
| `GetHoReportOverview` (:292) | `CampusInstances.FirstOrDefault()!.FormDetail!.DelegationName` | `HasMixedCampusDetails ? "Khác nhau theo cơ sở" : <detail>` | static-PASS (label an toàn, không rò campus ẩn) |
| `GetHODashboardOverview` (:83) | idem | idem | static-PASS |
| `ViewFeedbackSummary` (:112) | idem | idem | static-PASS |
| `ViewDocumentDetail` (:90) | idem | idem | static-PASS |
| `UpdatePendingVisitRequestV2` (:114) | idem | idem | static-PASS |
| `GetStaffLeaderDeptInvoiceItems` (:102) | detail **của chính instance** — comment nêu rõ không gate theo `HasMixedCampusDetails` | per-instance | static-PASS ✅ |
| `VisitFormReadService` | **rẽ nhánh theo `FormSchemaVersion`** | — | **FAIL (P0)** |
| `VisitInstanceEffectiveName` | **rẽ nhánh theo `FormSchemaVersion`** trong projection | — | **FAIL (P0)** |

**Ghi nhận tích cực:** pattern `HasMixedCampusDetails ? "Khác nhau theo cơ sở" : <detail đơn nhất>` là **rule deterministic, an toàn**, không lộ nội dung campus ẩn — đúng yêu cầu prompt §7 Phase G mục 4-5.

`HasMixedCampusDetails` được **tính server-side**: gán tại `VisitRequestV2CreateService.cs:136`, `VisitRequestV2EditService.cs:319,552`, `VisitSafeEditService.cs:286`. Field cùng tên trong `CreateVisitRequestV2Command.cs:22` nằm ở **`CreateVisitRequestV2Response`** (record kết quả), **không phải** input — Command chỉ nhận `VisitRequestFormDataV2 Form`. ✅ **Không tin client.**

---

## 8. GAP REGISTER

### 🔴 P0

---
**GAP-001 — EF map 12 cột không tồn tại → mọi nghiệp vụ visit chết**

- **Severity:** P0 · **Module:** Domain/EF
- **Evidence code:** `VisitRequest.cs:53,83,89,92,94,97,116,122,125,128,131`; `VisitRequestPendingForm.cs:30`
- **Evidence SQL:** `visit_requests` = 36 cột, `visit_request_pending_forms` = 8 cột; `information_schema` xác nhận 0 cột `form_schema_version` toàn DB
- **Chứng minh runtime:**
  ```
  ERROR 1054 (42S22): Unknown column 'v.form_schema_version' in 'field list'
  ERROR 1054 (42S22): Unknown column 'p.form_schema_version' in 'field list'
  ```
- **Failure mode:** EF sinh SELECT gồm mọi scalar property đã map. Bất kỳ query nào materialize 2 entity này → MySQL 1054 → 500. Không cần handler đọc property đó.
- **Impact:** Toàn bộ module Visit Request không hoạt động. Dashboard/report/calendar/email/photo phụ thuộc đều sập.
- **Fix:** Xóa 11 property khỏi `VisitRequest`, 1 khỏi `VisitRequestPendingForm`. Chuyển mọi read sang `VisitInstanceFormDetail`.
- **Dependencies:** chặn GAP-004, GAP-005, GAP-006
- **Tests:** contract test build EF model + query tối thiểu mỗi DbSet trên schema thật

---
**GAP-002 — Integration bootstrap trỏ SQL không tồn tại + FAIL-OPEN**

- **Severity:** P0 · **Module:** Test infrastructure
- **Evidence:** `tests/PEMS.IntegrationTests/TestInfrastructure/DisposableDatabaseManager.cs:48-55`
  ```csharp
  var sqlPath = Path.Combine(..., "PEMS_FULL_V2_SEED_COMPLETE_CONTACT_GUARD_AND_DASHBOARD_COVERAGE.sql");
  if (File.Exists(sqlPath)) { … }        // KHÔNG có else → im lặng bỏ qua
  ```
- **Evidence SQL:** file này **KHÔNG tồn tại** tại HEAD (đã verify). Thư mục chỉ còn 1 file .sql duy nhất.
- **Failure mode:** tạo DB disposable **rỗng**, rồi chạy test trên schema trống → fail nhiễu, hoặc tệ hơn là ngộ nhận "đã test với SQL canonical".
- **Phụ đề nghiêm trọng:** dòng 52 chỉ `Replace("USE \`pems_db\`;")`. SQL canonical **vẫn có `CREATE DATABASE IF NOT EXISTS \`pems_db\`` (dòng 321)** → nếu sau này ai đó sửa đúng path mà quên rewrite dòng 321, script sẽ **tạo/chạm `pems_db`** trên MySQL dùng chung.
- **Bằng chứng phụ:** trên MySQL local hiện có **22 database `pems_realstack_run_*` bị rò rỉ** → cleanup-on-failure không hoạt động.
- **Fix:** trỏ đúng canonical path; **fail-closed** khi thiếu file / nhiều candidate / sai hash; rewrite **mọi** database-selection statement + rescan file tạm; assert sau import (DATABASE(), 81 tables, 0 `pems_seed_*`, 0 legacy col, mọi issue_count=0); cleanup kể cả khi fail.
- **Tests:** unit test cho chính bootstrap (thiếu file → throw; hash sai → throw)

---
**GAP-003 — Solution KHÔNG BUILD → toàn bộ unit test không chạy được**

- **Severity:** P0 · **Module:** Tests
- **Evidence:** `tests/PEMS.UnitTests/TestInfrastructure/GalleryTestDbContext.cs:30`
  ```
  error CS0535: 'GalleryTestDbContext' does not implement interface member
                'IApplicationDbContext.VisitRequestFingerprintGuards'
  ```
- **Nguyên nhân:** commit thêm `VisitRequestFingerprintGuards` vào `IApplicationDbContext` đã cập nhật `PartnersTestDbContext`, `CampusUcTestHarness`, `DelegationsTestHarness`, `Uc106TestHarness` — **bỏ sót** `GalleryTestDbContext`.
- **Impact:** `dotnet build PEMS.slnx` fail → không chạy được unit test nào → mất toàn bộ lưới an toàn.
- **Fix:** thêm `DbSet<VisitRequestFingerprintGuard> VisitRequestFingerprintGuards` vào `GalleryTestDbContext`.

---
**GAP-004 — Write path ghi cột đã xóa (gồm 1 route `/api/v2/` đang sống)**

- **Severity:** P0 · **Module:** Write path
- **Evidence:**
  - `VisitSafeEditService.cs:288-297` ← `PATCH /api/v2/visit-requests/{id}/safe-details` (`VisitRequestsController.cs:451`) **← ĐANG SỐNG**
  - `VisitRequestV2CreateService.cs:135,147-156`
  - `VisitRequestV2EditService.cs:321-330, 554-563`
  - `InitiateVisitRequestV2CommandHandler` (PendingForm.FormSchemaVersion)
  - `VisitRequestService.cs:149-163` (V1, DI-registered, không route)
  - `UpdatePendingVisitRequestCommandHandler.cs:169-179` (V1, route 410)
- **Failure mode:** `SaveChanges` sinh INSERT/UPDATE chứa cột không tồn tại → 1054 → mọi create/edit/resubmit/safe-edit fail.
- **Fix:** loại bỏ ghi request-level; ghi vào `VisitInstanceFormDetail` trong cùng transaction.

---
**GAP-005 — Feature flag deadlock: không ai tạo được đơn ở Dev/Prod**

- **Severity:** P0 · **Module:** Config
- **Evidence:** `PerCampusFormV2Options.cs:14` (default false); `PerCampusFormV2WriteOptions` (default false); 0 occurrence trong `appsettings{,.Development,.Production}.json`; `appsettings.Testing.json` bật nhưng **bị .gitignore:53**
- **Evidence đối chiếu:** `VisitRequestsController.cs:39,54,105,137,229,259` → V1 trả `410 VISIT_FORM_V1_RETIRED`
- **Failure mode:** capability = false → `useVisitEntryCta.tsx:103` `notifyCapabilityDisabled()` → user thấy *"Cấu hình hệ thống không hợp lệ (V2 bị tắt)"*; V1 thì 410. **Không có đường nào tạo đơn.**
- **Fix (khuyến nghị):** **xóa hẳn 2 feature gate** — schema không thể rollback về V1 nên flag không còn ý nghĩa. Nếu buộc giữ endpoint capability cho client cũ, backend phải **luôn** báo enabled=true.
- **Cấm:** giữ trạng thái "V2 OFF → dùng V1".

---
**GAP-006 — Runtime dual-read còn rẽ nhánh theo discriminator**

- **Severity:** P0 · **Module:** Read service
- **Evidence:** `VisitFormReadService.cs:70,324,368`; `VisitInstanceEffectiveName.cs:31,40`
- **Failure mode:** đọc property map cột không tồn tại; riêng `VisitInstanceEffectiveName.cs:31` nằm trong **projection EF** → dịch thẳng vào SQL → 1054.
- **Fix:** bỏ nhánh V1, đọc thẳng `VisitInstanceFormDetail`; xóa `FormSchemaVersion` khỏi `ResolvedVisitFormDto`.

---
**GAP-007 — SMTP credential plaintext trong repo VÀ trong git history đã push**

- **Severity:** P0 (Security) · **Module:** Config
- **Evidence:** `backend/PEMS.Api/appsettings.json` — mục `Smtp.Password` (Gmail app password) và `JwtSettings.SecretKey`. **Giá trị không được tái hiện trong báo cáo này.**
- **Evidence history:** **16 revision** của file chứa password khác rỗng; commit mới nhất chạm file có mặt trên **5 remote branch** (`origin/Dev`, `origin/Cảnh-Iter1`, `origin/Duy-Iter1`, `origin/Phanh_Iter2`, `origin/HEAD`).
- **Impact:** credential coi như đã lộ với mọi người có quyền đọc repo.
- **Fix (thứ tự bắt buộc):**
  1. **ROTATE ngay** Gmail app password + JWT secret (ưu tiên tuyệt đối — history rewrite KHÔNG cứu được credential đã lộ);
  2. chuyển sang environment variable / secret manager;
  3. bỏ file khỏi tracking, thêm `appsettings.json` vào `.gitignore`, cung cấp `appsettings.example.json`;
  4. cân nhắc history rewrite **theo quy trình bảo mật + có phê duyệt** (ảnh hưởng 5 branch, cần phối hợp cả team).
- **Không** trộn task này vào slice schema.

### 🟠 P1

**GAP-008** — Frontend route theo `formSchemaVersion` → mọi row rơi vào `unsupported-version`. `visitVersionRouting.ts:11-47`, `VisitRequestManagement.tsx:359,1175,1178,1222`, `SubmittedVisitRequestDetailModal.tsx:92,103`, `delegations.types.ts:766,948`, `visitRequestV2Api.ts:180`.

**GAP-009** — Self-test của SQL canonical báo **14/14 NEGATIVE FAIL** sai sự thật.
Nguyên nhân **đã chứng minh bằng thực nghiệm**: trong handler, `SET v_raised = TRUE;` chạy **trước** `GET DIAGNOSTICS` → câu `SET` **xóa diagnostics area** → `v_sqlstate`/`v_message` = NULL → điều kiện PASS không bao giờ thỏa.
Probe chứng minh:
```
raised=1 | sqlstate_after_SET=NULL | msg_after_SET=NULL | sqlstate_get_first=45000 | msg_get_first=PROBE_MSG
```
**Trigger thật sự HOẠT ĐỘNG** — test trực tiếp: `UPDATE visit_requests SET primary_contact_access_status='ACTIVE', visitor_user_id=NULL` → `ERROR 1644 (45000): ACTIVE_PRIMARY_CONTACT_REQUIRES_VISITOR_USER`.
**Fix:** đưa `GET DIAGNOSTICS` lên làm câu **đầu tiên** trong handler. ⚠️ Đây là sửa **lỗi thật của harness**, không phải "sửa SQL để test xanh" — guard vẫn giữ nguyên.

**GAP-010** — `DocumentsOwnerTypeEnumConsistencyTests.cs:31` trỏ SQL không tồn tại → test enum guard FAIL, mất tác dụng bảo vệ.

**GAP-011** — V1 service vẫn DI-registered: `DependencyInjection.cs:74` (`IVisitRequestService`), `:85` (`IVisitSafeEditService`). `VisitSafeEditService` còn phục vụ route v2 đang sống (xem GAP-004).

**GAP-012** — `frontend/pems-react/scripts/run-realstack-e2e.mjs:31` trỏ `PEMS_FULL_V11_REMOVED_TTS_19_07_26.sql` (**không tồn tại**) → E2E realstack không thể chạy đúng.

### 🟡 P2

**GAP-013** — Stale SQL reference: `TestTc/Program.cs:10` (hard-code path tuyệt đối, file không tồn tại); `docs/database/scripts/review_env/Build-ReviewDatabase.ps1:54`; `phase_1_candidate/generate_fresh_target.ps1:1`; `phase_1_candidate/tests/Test-SqlSafetyGuard.ps1:381`.

**GAP-014** — FK delete behavior drift EF vs DB. DB: SET NULL 146 / RESTRICT 58 / CASCADE 47 (251 FK). EF: SetNull 105 / Restrict 53 / Cascade 45 (203 `OnDelete`) → ~48 quan hệ không cấu hình. Ví dụ xác nhận: `ApplicationDbContext.cs:305` `VisitorUserId` = `Restrict`, SQL `fk_visit_requests_visitor` = **SET NULL**. *Không có thư mục EF Migrations → SQL là nguồn sự thật, nên đây là lệch hành vi in-memory, không sinh schema sai.*

**GAP-015** — Seed placeholder chưa được enrichment ghi đè: `seed_placeholder_terms_remaining = 151`, tất cả ở `visit_instance_form_details` (`purpose`/`note_to_fptu` còn chứa `"Seed coverage:"`).

**GAP-016** — `operational_visit_instances_missing_agenda_final = 3`: instance `5075` (DURING_VISIT), `5085` (AFTER_VISIT), `5146` (CLOSED) không có agenda dù đã ở trạng thái vận hành.

**GAP-017** — Nullability mismatch (§4.4): `email_templates.purpose`, `news_content_sections.section_title`, `.section_body_html` — CLR `string?` nhưng DB NOT NULL, không default → insert null → MySQL 1364.

**GAP-018** — 22 database `pems_realstack_run_*` rò rỉ trên MySQL local → cleanup harness không đảm bảo.

### 🔵 P3

**GAP-019** — 5 entity không expose qua `IApplicationDbContext` (§4.5).
**GAP-020** — `visit_expense_items.total_amount`: CLR `decimal` non-nullable vs DB nullable (STORED GENERATED). Đã có `[DatabaseGenerated(Computed)]` nên write an toàn; chỉ rủi ro đọc nếu `quantity`/`unit_price` NULL.
**GAP-021** — Comment/doc mô tả dual-read V1/V2 đã lỗi thời: `IVisitFormReadService.cs:12-14`, `VisitCampusFormContent.cs:8`, `VisitRequestConstants.cs:126`, `PerCampusFormV2Options.cs:5-7`, `VisitRequest.cs:51-52,77-82`.

---

## 9. CONFLICT REGISTER

**CONFLICT-01 — Seed khởi tạo operational contact từ request-level contact**
- SQL seed (block 4879/6041/9335) copy `vr.contact_person_*` sang mọi campus.
- Business rule Pure V2: mỗi campus có operational contact riêng.
- **Không tự chọn.** Phương án A: coi đây thuần seed convenience, runtime **bắt buộc** giữ contact riêng từng campus *(khuyến nghị — đúng tinh thần prompt §5.3)*. Phương án B: coi là default khi client không gửi contact riêng.
- **Cần chủ dự án chốt trước khi code Phase 2.**

**CONFLICT-02 — Giữ hay xóa feature flag**
- Phương án A *(khuyến nghị)*: xóa hẳn 2 flag + `PublicFeaturesController` + `perCampusV2Capability`. Sạch, hết nguy cơ deadlock. Nhược: client cũ gọi endpoint sẽ 404.
- Phương án B: giữ endpoint, hard-code `enabled=true`. Tương thích ngược, nhưng để lại dead config.
- **Cần chốt trước Phase 4.**

**CONFLICT-03 — DoD "mọi issue_count = 0" vs harness lỗi**
- 3 check khác 0: `contact_guard_negative_failures=14` (**false alarm**, GAP-009), `seed_placeholder_terms_remaining=151` (thật), `operational_visit_instances_missing_agenda_final=3` (thật).
- Đề xuất: sửa harness (GAP-009) + làm sạch seed (GAP-015/016) rồi mới coi DoD đạt. **Không** nới lỏng DoD.

**CONFLICT-04 — `sp_pems_assert_pure_v2_only` không tự kiểm discriminator**
- Procedure chỉ liệt kê 10 global column, **không** kiểm `form_schema_version` ở cả 2 bảng.
- Audit này đã kiểm độc lập qua `information_schema` → kết quả 0 ✅. Đề xuất bổ sung 2 check vào procedure để không phụ thuộc kiểm tra thủ công.

---

## 10. KẾ HOẠCH CẬP NHẬT ĐỀ XUẤT

> Sắp theo **phụ thuộc**, không theo thư mục. Không commit chỉ chứa report.

### Phase 0 — Safety & unblock (chặn mọi thứ khác)
- **Mục tiêu:** rotate secret; khôi phục khả năng build/test; khóa đúng canonical SQL.
- **File:** `appsettings*.json`, `.gitignore`, `GalleryTestDbContext.cs`, `DisposableDatabaseManager.cs`, `DocumentsOwnerTypeEnumConsistencyTests.cs`, `run-realstack-e2e.mjs`, `TestTc/Program.cs`
- **Gap:** GAP-007, GAP-003, GAP-002, GAP-010, GAP-012, GAP-013
- **Thay đổi:** rotate + chuyển secret ra env; thêm DbSet thiếu; bootstrap fail-closed + verify SHA-256 `7ec63e90…` + rewrite mọi DB-selection + assert sau import + cleanup chắc chắn.
- **Test:** `dotnet build PEMS.slnx` = 0 error; unit test chạy được; bootstrap test (thiếu file → throw).
- **Exit:** solution build sạch; bootstrap chứng minh import đúng hash; không còn secret plaintext.
- **Rollback:** revert config commit (secret đã rotate nên an toàn).

### Phase 1 — Domain/EF Pure V2 contract
- **Phụ thuộc:** Phase 0
- **File:** `VisitRequest.cs`, `VisitRequestPendingForm.cs`, `ApplicationDbContext.cs`
- **Gap:** GAP-001, GAP-014, GAP-017
- **Thay đổi:** xóa 12 property phantom; rà FK delete behavior khớp SQL; sửa nullability.
- **Không** tạo entity/DbSet/migration cho `pems_seed_*`.
- **Test:** **contract test mới** — build EF model + query tối thiểu **mỗi** DbSet trên schema import từ hash `7ec63e90…`, bắt `Unknown column`/sai relationship; write round-trip cho bảng trọng yếu.
- **Exit:** 0 `Unknown column`; contract test xanh.

### Phase 2 — V2 write/read core
- **Phụ thuộc:** Phase 1 · **Cần CONFLICT-01 đã chốt**
- **File:** `VisitRequestV2CreateService.cs`, `VisitRequestV2EditService.cs`, `VisitSafeEditService.cs`, `InitiateVisitRequestV2CommandHandler.cs`, `VisitFormReadService.cs`, `VisitInstanceEffectiveName.cs`, `VisitRequestService.cs`, `UpdatePendingVisitRequestCommandHandler.cs`, `DependencyInjection.cs`
- **Gap:** GAP-004, GAP-006, GAP-011
- **Thay đổi:** bỏ discriminator + V1 fallback; ghi thẳng `VisitInstanceFormDetail` trong transaction; **giữ contact/member riêng từng campus** (không copy request-level); gỡ DI của service V1 thực sự chết.
- **Test:** create/OTP/replay/15-min guard/pending edit/resubmit/safe-edit/amendment/claim/transfer — assert không partial data, không double notification.
- **Exit:** toàn bộ luồng write xanh trên schema thật.

### Phase 3 — Downstream consumers
- **Phụ thuộc:** Phase 2
- **Phạm vi:** list/search/dashboard/calendar/invitation/process/contribution/feedback/minutes/documents/report/export/email/photo (~40 handler đã liệt kê §5.2).
- **Thay đổi:** mọi projection lấy `FormDetail` đúng instance; scope **trước** keyword; giữ rule label mixed deterministic; tránh N+1.
- **Test:** `QUERY CONSUMER MATRIX` chuyển từ *static-PASS* → *runtime-PASS*.

### Phase 4 — API + Frontend
- **Phụ thuộc:** Phase 3 · **Cần CONFLICT-02 đã chốt**
- **File:** `visitVersionRouting.ts`, `VisitRequestManagement.tsx`, `SubmittedVisitRequestDetailModal.tsx`, `delegations.types.ts`, `visitRequestV2Api.ts`, `perCampusV2Capability.tsx`, `useVisitEntryCta.tsx`, `PublicFeaturesController.cs`, `PerCampusFormV2*Options.cs`
- **Gap:** GAP-005, GAP-008
- **Thay đổi:** bỏ routing theo discriminator (route thẳng V2); gỡ/hard-code capability; xóa route `unsupported-version`; kiểm min-30-phút cả FE lẫn BE.
- **Test:** cập nhật 389 unit test FE; E2E deep link/refresh/modal/edit/resubmit.
- **Exit:** CTA luôn mở V2 hợp lệ; không còn `formSchemaVersion` runtime.

### Phase 5 — Translation/Gallery/FAQ/Partner/Vision/Expense closure
- **Phụ thuộc:** Phase 1
- **Ghi chú:** tên cột đã khớp 100% (§4.3) → phần lớn là **contract test**, không phải sửa mapping.
- **Kiểm:** FAQ/Partner ghi VI+EN cùng transaction, manual EN không bị auto-translate ghi đè, public read từ translation table (không gọi API mỗi lần đọc); Gallery mô tả+audio từ `gallery_item_contents`; Vision chỉ detection + manual tagging, guest tag đúng instance, confirm idempotent.

### Phase 6 — Tests / real-stack / security regression
- **Phụ thuộc:** Phase 2–5
- **Gap:** GAP-009, GAP-015, GAP-016, GAP-018
- **Thay đổi:** sửa `GET DIAGNOSTICS` ordering; làm sạch 151 placeholder + 3 agenda thiếu; cleanup DB rò rỉ; schema/seed assertion chạy **trước** test.
- **Exit:** mọi `issue_count = 0` **thật**; Unit/Architecture/Integration/FE build+test xanh; E2E critical journey xanh.

### Phase 7 — Dead-code / config / docs cleanup
- **Gap:** GAP-019, GAP-020, GAP-021
- Xóa stale SQL path; chỉ giữ **một** canonical entrypoint; cập nhật comment dual-read lỗi thời; bổ sung 2 check discriminator vào `sp_pems_assert_pure_v2_only` (CONFLICT-04).

### Gom commit theo functional slice
```
1. fix(build): GalleryTestDbContext + bootstrap fail-closed + stale SQL refs   [P0]
2. chore(security): rotate + externalize secrets                              [P0, tách riêng]
3. refactor(domain): drop V1 columns from VisitRequest/PendingForm + contract test
4. refactor(visit): Pure V2 write path (create/edit/resubmit/safe-edit/initiate)
5. refactor(visit): Pure V2 read service + effective name
6. refactor(consumers): per-instance FormDetail across downstream surfaces
7. feat(fe): V2-only routing + remove capability gate
8. test(sql): fix GET DIAGNOSTICS ordering + seed cleanup
9. chore: dead code & docs
```
*Không thêm tên AI vào commit metadata.*

---

## 11. DEFINITION OF DONE ĐỀ XUẤT

```
[x] Canonical SQL đúng path, blob 825b9567…, SHA-256 7ec63e90…, 14,832 dòng.
[x] SQL import MySQL 8 disposable thành công, rerun thành công.
[x] 81 persistent tables; 0 view; 0 object pems_seed_*.
[x] Ba direct-seed block 19/90/8 phủ đủ campus instance; detail = campus ở mọi batch.
[x] Không có discriminator ở visit_requests VÀ visit_request_pending_forms (kiểm độc lập).
[x] Không có 10 global-form column trên visit_requests.
[ ] MỌI issue_count = 0  → còn 151 placeholder, 3 agenda thiếu, 14 false-FAIL harness.
[ ] Integration bootstrap fail-closed và chứng minh đã import đúng canonical hash.
[ ] Không còn runtime/test reference tới tên SQL đã xóa.
[ ] Import test không tạo/chạm pems_db ngoài môi trường disposable được phép.
[ ] Không còn entity map cột không tồn tại.            ← GAP-001
[ ] Không còn runtime dependency form_schema_version.  ← GAP-006
[ ] Không còn read/write 10 global-form column.        ← GAP-004
[ ] dotnet build PEMS.slnx = 0 error.                  ← GAP-003
[ ] V2 create/OTP/replay/edit/resubmit/claim/transfer/amendment xanh.
[ ] Feature/config không thể làm cả V1 và V2 cùng bất khả dụng. ← GAP-005
[ ] Query instance/request/mixed đúng và scope-before-keyword (runtime-verified).
[x] Enum/constants khớp SQL ở mức tên cột/bảng (81/81, 0 phantom ngoài 2 file).
[ ] Translation/Gallery/FAQ/Partner/Vision/Expense contract xanh ở mức RUNTIME.
[ ] Unit/Architecture/Integration/frontend build/test xanh.  (FE xanh; BE unit chưa build được)
[ ] E2E real-stack critical journeys xanh.
[ ] Không có credential plaintext trong trạng thái cuối + đã ROTATE.  ← GAP-007
[ ] Không có dead V1 handler/service được DI hoặc reachable.
[ ] Không regression permission, audit, notification, idempotency.
```

---

## 12. TIÊU CHUẨN KẾT LUẬN

Theo §9 của prompt, **chưa** đạt READY:

| Điều kiện READY | Trạng thái |
|---|---|
| 1. SQL đúng hash import thật trên MySQL 8 disposable | ✅ ĐẠT |
| 2. Direct-seed/no-staging assertions đạt **và** bootstrap dùng đúng SQL | ⚠️ nửa đầu ĐẠT, **bootstrap KHÔNG ĐẠT** |
| 3. EF model chạy thật trên schema đó | ❌ **KHÔNG ĐẠT** (1054) |
| 4. Zero-remnant Pure V2 + stale SQL reference | ❌ 112 file / 309 hit + 6 stale ref |
| 5. Write/read/downstream/frontend contract có evidence | ❌ write path ghi cột đã xóa |
| 6. Test matrix xanh | ❌ solution không build |
| 7. P0/P1 = 0 hoặc có chấp nhận rủi ro | ❌ 7 P0 + 5 P1 |

**→ Kết luận: NOT READY.** Ưu tiên tuyệt đối: **GAP-007 (rotate secret)** → **GAP-003 (build)** → **GAP-001 (EF)** → **GAP-002 (bootstrap)** → **GAP-005 (flag)**.

---

## PHỤ LỤC A — Tái lập môi trường audit

```bash
# 1. Tạo bản copy retarget NGOÀI repo (không sửa file canonical)
sed -e 's/CREATE DATABASE IF NOT EXISTS `pems_db`/CREATE DATABASE IF NOT EXISTS `pems_schema_audit_<TS>`/g' \
    -e 's/USE `pems_db`;/USE `pems_schema_audit_<TS>`;/g' \
    "docs/database/scripts/PEMS_FULL_V2_ONLY_CANONICAL_..._LATEST (1).sql" > /tmp/retargeted.sql

# 2. Safety rescan — BẮT BUỘC trước khi import
grep -n '`pems_db`' /tmp/retargeted.sql        # chỉ được còn comment
grep -nE '^\s*(USE|CREATE DATABASE|DROP DATABASE)' /tmp/retargeted.sql
grep -nE '^\s*(SOURCE|\\\.)' /tmp/retargeted.sql   # phải rỗng

# 3. Import
mysql -u <user> -h 127.0.0.1 -P 3306 --default-character-set=utf8mb4 -e "source /tmp/retargeted.sql"
```

Script audit dùng trong phiên này (ngoài repo, không commit):
- `entity_schema_diff.ps1` — đối chiếu `[Table]`/`[Column]` ↔ `information_schema`
- `nullability_diff.ps1` — đối chiếu nullability CLR ↔ DB

**Dọn dẹp:** database `pems_schema_audit_20260723140353` cần `DROP DATABASE` sau khi review xong bằng chứng.

## PHỤ LỤC B — Lệnh đã chạy để lấy evidence

| Mục đích | Lệnh |
|---|---|
| Hash SQL | `git hash-object`, `Get-FileHash -Algorithm SHA256`, `wc -l` |
| No-staging | `grep -nE '^\s*CREATE (TABLE\|VIEW)' \| grep pems_seed_` |
| Schema counts | `information_schema.tables/columns/triggers/routines/referential_constraints` |
| Phantom column | `entity_schema_diff.ps1` |
| Chứng minh 1054 | `SELECT v.form_schema_version FROM visit_requests v LIMIT 1` |
| Guard trigger sống | `UPDATE visit_requests SET primary_contact_access_status='ACTIVE', visitor_user_id=NULL` → 1644 |
| Lỗi GET DIAGNOSTICS | procedure probe `sp_diag_probe` |
| Secret history | đếm revision có `"Password"` khác rỗng (**không in giá trị**) |
| Build/test | `dotnet build`, `dotnet test`, `npm run build/lint/test:unit` |
