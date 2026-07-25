# PROMPT AUDIT TOÀN DIỆN PEMS VÀ LẬP KẾ HOẠCH ĐỒNG BỘ CODE VỚI SQL PURE V2 MỚI NHẤT

Bạn là **Senior Software Architect + Senior .NET/React/MySQL Engineer**. Nhiệm vụ của bạn là đọc repository thật, kiểm tra toàn bộ tác động của SQL mới nhất lên backend, frontend, entity, EF Core mapping, enum/constants, query/projection, API contract, validation, authorization, test và cấu hình runtime; sau đó lập kế hoạch cập nhật chi tiết.

## 1. Phạm vi của phiên làm việc này

Đây là phiên **AUDIT + LẬP KẾ HOẠCH**, chưa phải phiên sửa code.

Bạn phải:

1. Đọc code thật trên đúng branch và đúng commit.
2. Import/kiểm tra SQL chỉ trên database disposable, tuyệt đối không dùng database thật.
3. Tạo ma trận đối chiếu SQL ↔ backend ↔ frontend ↔ test.
4. Chỉ ra phần đã khớp, khớp một phần, còn thiếu, thừa hoặc đã trở thành dead code.
5. Đề xuất kế hoạch cập nhật theo phase, thứ tự phụ thuộc, file dự kiến sửa và test bắt buộc.
6. Dẫn chứng mọi kết luận bằng `file:line`, câu lệnh đã chạy và kết quả thực tế.

Không được:

- sửa code, format hàng loạt, commit, push, merge hoặc mở PR;
- sửa SQL canonical để làm test xanh;
- suy đoán “có vẻ đã xong” từ tên file, tên handler hoặc tài liệu tiến độ;
- báo PASS nếu chưa chạy kiểm tra;
- chạy SQL mới trên `pems_db`, `pems_test`, database Railway/production hoặc database có dữ liệu cần giữ;
- in credential, token, password, connection string hoặc secret ra terminal/report.
- phục hồi SQL staging/helper cũ để tránh sửa test bootstrap hoặc code runtime.

## 2. Repository, branch và baseline đã được xác minh

- Repository: `quangthoai04/PEMS`
- Branch phải audit: `Cảnh-Iter1` — giữ đúng dấu và chữ hoa/thường.
- HEAD tại thời điểm cập nhật prompt: `19bed5101b8b3bd564d438e6add90c67a2f83fa6`
- HEAD của prompt trước: `3ce95977a417c3bdbdb1787d37186713cbb4bd5f`
- Commit chung gần nhất với `Dev`: `584f3ddace324eb0b4f6916ca586e6f1b2e05090`
- Trạng thái so với `Dev`: ahead 11 commit, behind 0.
- Diff `Dev...Cảnh-Iter1`: 60 file:
  - backend: 43;
  - frontend: 3;
  - tests: 11;
  - docs: 2;
  - root: 1.
- Trong diff này, Domain chỉ thêm `VisitRequestFingerprintGuard.cs`; Infrastructure chỉ sửa `ApplicationDbContext.cs`. Vì vậy không được coi số lượng query handler đã sửa là bằng chứng toàn bộ EF model đã khớp schema.

Delta từ HEAD của prompt trước tới HEAD hiện tại chỉ có commit:

```text
19bed5101b8b3bd564d438e6add90c67a2f83fa6 — sql v2
```

Commit này:

- đổi tên và viết lại cơ chế seed của SQL canonical: `+1,715/-283`, tổng `1,998` dòng thay đổi;
- xóa `docs/database/scripts/PEMS_FULL_V2_TRANSLATION_GALLERY_FULL.sql`;
- không sửa backend, frontend hoặc test.

Vì vậy, mọi lệch code đã ghi nhận ở HEAD `3ce95977...` vẫn phải được coi là **chưa được khắc phục**, trừ khi audit tại HEAD thực tế chứng minh ngược lại.

Nếu HEAD đã thay đổi, ghi rõ:

- HEAD cũ trong prompt;
- HEAD thực tế;
- commit mới xuất hiện;
- diff làm thay đổi kết luận nào.

Không tự checkout/reset/rebase nếu working tree đang có thay đổi của người dùng.

## 3. SQL canonical mới nhất

File canonical trong repository:

```text
docs/database/scripts/PEMS_FULL_V2_ONLY_CANONICAL_TRANSLATION_GALLERY_FAQ_VISION_GUARD_DIRECT_SEED_NO_STAGING_LATEST (1).sql
```

Thông tin kiểm chứng:

```text
Git blob SHA-1: 825b95672491d653d5537c95b4e81f3c000b229f
SHA-256:        7ec63e9044ecd1910e9a7137c99773bb13b36902f3042fd7bc6cfce402892415
Số dòng:       14,832
```

Baseline SQL trước đó để lập delta:

```text
Tên cũ:        PEMS_FULL_V2_ONLY_CANONICAL_TRANSLATION_GALLERY_FAQ_VISION_GUARD_LATEST.sql
Git blob:      6406cf4c06a07b3fcfb3f22887fa187f634e8246
SHA-256:       5165e088f9fa244afaa535a90d18bf2260132a4420ee1ad1028017f86aef8875
Số dòng:       13,400
```

Đây là fresh-create script có tính phá hủy dữ liệu vì chọn thẳng `pems_db` rồi drop/recreate toàn bộ object trong database được chọn. Chỉ được chạy theo một trong hai cách:

1. MySQL disposable container hoàn toàn tách biệt, trong đó `pems_db` chỉ là database test có thể xóa; hoặc
2. tạo bản copy tạm ngoài repository, đổi target sang tên allowlist như `pems_schema_audit_<timestamp>`, quét lại toàn file để bảo đảm không còn lệnh trỏ sang database thật.

Không sửa file canonical trong repo khi chỉ cần tạo bản import tạm.

Tên file hiện tại có khoảng trắng và hậu tố `(1)`. Mọi command phải quote path đầy đủ. Không tự fallback sang tên SQL cũ, không chọn file chỉ bằng wildcard và không dùng file SQL đã bị xóa.

## 4. Thứ tự nguồn sự thật

Khi có mâu thuẫn, dùng thứ tự sau:

1. Yêu cầu hiện tại của chủ dự án trong prompt này.
2. SQL canonical mới nhất đối với **persistence contract**: bảng, cột, type, nullability, default, enum, key, index, FK, trigger, check constraint, stored verification.
3. `PEMS_CANONICAL_BUSINESS_RULES...`, `PERMISSION_RULES.md`, `PERMISSION_MATRIX.md` đối với nghiệp vụ, actor, scope và bảo mật — nhưng phải loại phần đã tự ghi là legacy.
4. Code và test tại HEAD để xác định hành vi hiện tại, không dùng code cũ để phủ nhận schema mới.
5. Tài liệu handoff/plan/report cũ chỉ dùng làm lịch sử.

Nếu SQL và business rule hiện hành thật sự mâu thuẫn:

- không tự chọn một phía;
- ghi vào `CONFLICT REGISTER`;
- nêu ảnh hưởng của từng phương án;
- đề xuất phương án khuyến nghị;
- đánh dấu cần chủ dự án chốt trước khi code.

Không tái sinh logic V1 chỉ vì tài liệu hoặc test cũ còn nhắc đến V1.

## 5. Các thay đổi lớn của SQL bắt buộc phải audit

SQL mới tuyên bố **81 persistent runtime tables** và runtime **Pure V2-only**.

### 5.1 Visit Request Pure V2-only

`visit_requests` không còn:

```text
form_schema_version
delegation_name
visit_type
visit_type_other
purpose
working_content
working_language
transportation_note
media_consent_status
media_consent_note
note_to_fptu
```

`visit_request_pending_forms` cũng không còn `form_schema_version`.

Toàn bộ nội dung form hoạt động phải đọc/ghi qua:

```text
visit_request_campuses
visit_instance_form_details
visit_guest_members
visit_instance_guest_members
```

Không còn dual-read V1/V2 và không còn compatibility projection từ cột global.

### 5.2 Các bảng/module mới hoặc thay đổi quan trọng

Phải audit tối thiểu:

```text
visit_instance_form_details
visit_instance_guest_members
visit_request_identity_changes
visit_request_identity_change_events
visit_instance_amendments
visit_instance_amendment_changes
visit_instance_form_revision_history
visit_request_revision_history
visit_request_pending_forms
visit_request_fingerprint_guards

visit_photo_folders
visit_photos
visit_photo_face_scans
visit_photo_face_detections

visit_expense_reports
visit_expense_items
visit_expense_report_events

partner_translations
faq_translations
gallery_item_contents
```

Các thay đổi trên bảng cũ phải audit:

```text
visit_requests: bỏ discriminator và 10 cột form global; giữ identity/scope/lifecycle.
documents.owner_type: thêm VISIT_INSTANCE_MEDIA.
partner_contacts: thêm avatar_file_id.
audit_logs: thêm correlation/request/instance/source/reason context.
audit_log_changes: thêm category/format/sensitive/order metadata.
gallery_areas, gallery_locations, gallery_items, gallery_item_media:
  thêm trường VI–EN và translation metadata.
gallery_items: bỏ description.
gallery_item_tts_audios: bị loại bỏ.
```

Các invariant Pure V2 phải giữ:

- request luôn có ít nhất một campus instance;
- mỗi instance luôn có đúng một `visit_instance_form_details`;
- `SINGLE_CAMPUS` có đúng một instance;
- `MULTI_CAMPUS` có ít nhất hai instance;
- member không được link chéo request;
- mọi member của request phải có link campus hợp lệ theo contract seed;
- `ACTIVE` primary contact phải có `visitor_user_id`;
- `PENDING_CONFIRMATION` phải chưa có `visitor_user_id`;
- visit tối thiểu 30 phút;
- `has_mixed_campus_details` do backend tính, không tin client;
- fingerprint guard khóa duplicate submit trong transaction;
- FAQ/Partner public read từ translation rows đã lưu, không gọi Translation API ở mỗi lần đọc;
- face scan chỉ là face detection + manual tagging, không biến thành face recognition.

### 5.3 Delta mới: direct seed, tuyệt đối không staging

So với SQL hash `5165e088...`, SQL hash `7ec63e90...` giữ nguyên **81 persistent runtime tables** nhưng thay đổi lớn cách tạo dữ liệu mẫu:

```text
CREATE TABLE count: 82 → 81
CREATE VIEW count:   1 → 0
Persistent tables:  81 → 81
Triggers:            32 → 32
Procedures defined:   2 → 2
```

Phần giảm một table và một view chính là hai helper seed-only đã bị loại bỏ:

```text
pems_seed_visit_request_form_v2
pems_seed_visit_requests_v2_compat
```

SQL mới:

1. Không tạo staging table, helper view hoặc object `pems_seed_*` ở bất kỳ thời điểm nào.
2. Có ba khối `INSERT INTO visit_instance_form_details` đặt ngay sau ba batch `visit_request_campuses`.
3. Ba khối direct seed chứa lần lượt 19, 90 và 8 request template; join với campus rows để tạo đúng một detail cho mỗi instance.
4. Các seed consumer như news, feedback, agenda, logistics, minutes, notification, calendar và sent email đọc trực tiếp:

```text
visit_requests
+ visit_request_campuses
+ visit_instance_form_details
```

5. Phần enrichment cập nhật trực tiếp `visit_instance_form_details`; không cập nhật staging rồi backfill cuối file.
6. Phần finalization không còn khối materialize detail và không còn `DROP` helper table/view.
7. Pure V2 assertion cuối file đã đổi từ kiểm tra riêng một staging table thành từ chối mọi object có prefix `pems_seed_`:

```text
PURE_V2_REFUSED_SEED_HELPER_OBJECT_PRESENT
pure_v2_seed_helper_objects
```

Phải phân biệt rõ **seed contract** và **runtime business contract**:

- không tạo entity, DbSet, repository, migration hoặc service cho `pems_seed_*`;
- không sao chép cách direct seed thành logic runtime nếu payload thật cho phép từng campus có form/contact/member khác nhau;
- comment seed “Preserve V1 shared-member semantics” không tự động trở thành quy tắc runtime bắt mọi member xuất hiện ở mọi campus;
- việc seed lấy request contact làm giá trị khởi tạo detail không cho phép backend bỏ qua operational contact riêng từng campus;
- business logic runtime vẫn phải ghi trực tiếp vào các bảng canonical V2 trong transaction.

Lưu ý giới hạn self-check hiện tại: `sp_pems_assert_pure_v2_only` và query `pure_v2_legacy_columns` chỉ liệt kê 10 global-form columns của `visit_requests`; chúng không tự kiểm tra:

```text
visit_requests.form_schema_version
visit_request_pending_forms.form_schema_version
```

Fresh-create schema hiện không tạo hai cột này, nhưng audit vẫn phải query `information_schema.columns` độc lập. Không được dùng việc procedure PASS để suy ra discriminator chắc chắn vắng mặt.

Audit phải lập thêm `SEED PHASE MATRIX`:

```text
Seed batch
Request IDs
Campus rows
Detail rows
Insert order
Downstream consumers
Enrichment order
Revision snapshot order
Authorship fields
Final verification
Status/evidence
```

## 6. Các lệch đã thấy tại HEAD — phải xác minh và mở rộng

Đây là lead ban đầu, không phải danh sách cuối cùng.

### P0 — EF model đang map cột không tồn tại

Tại HEAD đã thấy:

```text
backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs
  vẫn map form_schema_version và 10 cột form global đã bị SQL xóa.

backend/PEMS.Domain/Entities/Delegations/VisitRequestPendingForm.cs
  vẫn map form_schema_version.
```

Chỉ cần entity còn map các cột này, một query EF bình thường trên `VisitRequests` hoặc `VisitRequestPendingForms` có thể sinh SQL chứa cột không tồn tại, dù handler không đọc property đó.

### P0 — runtime còn phụ thuộc discriminator V1/V2

Quét chính branch hiện tại đã tìm thấy `FormSchemaVersion`, `form_schema_version` hoặc `formSchemaVersion` trong khoảng 92 file code/test. Phải phân loại từng hit:

- runtime dependency cần xóa;
- API/DTO contract cần đổi;
- frontend routing/rendering cần đổi;
- test V1 cần xóa hoặc viết lại;
- comment/doc lịch sử có thể giữ ở khu vực archive.

Các vùng có dependency thật đã thấy gồm:

```text
VisitFormReadService
VisitInstanceEffectiveName
ScheduleConflictResolver
create/initiate/verify/edit/resubmit/claim/transfer/amendment
dashboard/calendar/invitation/process/contribution
feedback/document/report/export/email-action/photo
ViewGuestDelegationList và nhiều projection khác
frontend visitVersionRouting, modal, list management, API/types
```

### P0 — write path vẫn gán cột đã bị xóa

Đã thấy:

```text
VisitRequestV2CreateService
  gán VisitRequest.FormSchemaVersion.

InitiateVisitRequestV2CommandHandler
  gán VisitRequestPendingForm.FormSchemaVersion.

CreateVisitRequestV2CommandHandler và VerifyAndCreateVisitRequestV2CommandHandler
  filter replay theo VisitRequest.FormSchemaVersion.
```

### P0 — Integration test bootstrap trỏ tới SQL không tồn tại và fail-open

Tại HEAD hiện tại đã xác minh:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/DisposableDatabaseManager.cs
  vẫn hard-code:
  PEMS_FULL_V2_SEED_COMPLETE_CONTACT_GUARD_AND_DASHBOARD_COVERAGE.sql
```

File này không tồn tại tại commit `19bed510...`. Code hiện chỉ chạy import bên trong:

```text
if (File.Exists(sqlPath)) { ... }
```

và không có nhánh `else` để fail. Hậu quả:

- database disposable có thể được tạo nhưng để trống;
- integration test có thể fail bằng lỗi nhiễu hoặc tệ hơn là dùng bootstrap khác mà người chạy không nhận ra;
- kết quả test không chứng minh code khớp SQL canonical mới;
- tên SQL vừa đổi tiếp tục không được test infrastructure sử dụng.

Ngoài ra, bootstrap hiện chỉ thay literal `USE \`pems_db\`;`. SQL canonical vẫn có `CREATE DATABASE IF NOT EXISTS \`pems_db\``. Phải audit toàn bộ target-rewrite, không được coi một lệnh `Replace` là safety proof.

Kế hoạch phải yêu cầu:

1. tìm toàn bộ hard-coded SQL filename/path;
2. chỉ định đúng một canonical SQL path;
3. fail-closed nếu file thiếu, có nhiều candidate, hash sai hoặc target rewrite không thành công;
4. không tạo/chạm `pems_db` khi chạy integration test trên MySQL dùng chung;
5. sau import phải assert database đích, 81 tables, 0 helper object và mọi `issue_count = 0`;
6. cleanup disposable DB cả khi import/build test thất bại;
7. không báo integration PASS nếu chưa chứng minh schema được import từ hash `7ec63e90...`.

### P0/P1 — feature flag không còn phù hợp Pure V2

Hiện vẫn có:

```text
PerCampusFormV2Options
PerCampusFormV2WriteOptions
PublicFeaturesController
frontend perCampusV2Capability
```

Hai options mặc định `Enabled = false`, trong khi endpoint V1 create/edit/resubmit đã trả `410 VISIT_FORM_V1_RETIRED`.

Phải kiểm tra cấu hình runtime thật. Nếu không có override, hệ thống có thể rơi vào trạng thái:

- V1 bị retire;
- V2 bị 404/disable;
- người dùng không thể tạo đơn.

Kế hoạch phải quyết định rõ:

- xóa feature gates vì schema không thể rollback về V1; hoặc
- giữ endpoint capability chỉ vì tương thích client cũ nhưng backend luôn báo Pure V2 enabled.

Không được giữ trạng thái “V2 OFF → dùng V1” vì database không còn V1 contract.

### P1 — frontend còn route theo formSchemaVersion

Đã thấy:

```text
visitVersionRouting.ts
SubmittedVisitRequestDetailModal.tsx
VisitRequestManagement.tsx
visitRequestV2Api.ts
delegations.types.ts
```

Pure V2 phải route/render theo API V2 mà không cần discriminator từ DB. Phải audit cả cached state, deep link, modal, edit, resubmit, detail, list row và error route `unsupported-version`.

### P1 — delete behavior có dấu hiệu lệch

Ví dụ đã thấy:

```text
ApplicationDbContext:
  VisitRequest.VisitorUserId dùng DeleteBehavior.Restrict.

SQL:
  fk_visit_requests_visitor dùng ON DELETE SET NULL.
```

Không chỉ sửa ví dụ này. Phải đối chiếu toàn bộ FK action giữa EF model và SQL.

### Khu vực có vẻ đã được triển khai nhưng vẫn phải contract-test

Code hiện đã có entity/DbSet cho:

```text
VisitRequestFingerprintGuard
FaqTranslation
PartnerTranslation
Gallery translation fields
GalleryItemContent
VisitPhotoFaceScan
VisitPhotoFaceDetection
partner contact avatar
visit expense tables
```

Không đánh dấu PASS chỉ vì class tồn tại. Phải đối chiếu type, length, precision, nullability, default, enum, unique key, composite FK, delete behavior, query và write transaction.

### P0 Security — không đưa secret vào report

Audit tĩnh đã thấy dấu hiệu credential SMTP dạng plaintext nằm trong tracked appsettings. Không được lặp lại giá trị. Hãy:

- đánh dấu secret exposure là P0 riêng;
- đề xuất rotate credential;
- chuyển sang environment/secret manager;
- kiểm tra git history theo quy trình bảo mật phù hợp;
- không trộn thao tác rotate/xóa history vào task schema nếu chưa được cấp quyền.

### Trạng thái kiểm thử từ xa

HEAD hiện không có GitHub Actions workflow run được ghi nhận; hai Vercel status đang failure. Không kết luận nguyên nhân nếu chưa có log. Vì vậy phải chạy build/test local hoặc trong môi trường disposable và ghi rõ giới hạn nếu không chạy được.

## 7. Quy trình audit bắt buộc

### Phase A — Preflight repository

Chạy và lưu kết quả:

```bash
git status --short --branch
git branch --show-current
git rev-parse HEAD
git log -15 --oneline --decorate
git merge-base Dev HEAD
git rev-list --left-right --count Dev...HEAD
git diff --stat Dev...HEAD
git diff --name-status Dev...HEAD
```

Kiểm tra `AGENTS.md`, `.agents/`, instruction trong repo nếu có. Không sửa working tree.

Xác minh SQL:

```bash
CANONICAL_SQL='docs/database/scripts/PEMS_FULL_V2_ONLY_CANONICAL_TRANSLATION_GALLERY_FAQ_VISION_GUARD_DIRECT_SEED_NO_STAGING_LATEST (1).sql'

test -f "$CANONICAL_SQL"
wc -l "$CANONICAL_SQL"
git hash-object "$CANONICAL_SQL"
sha256sum "$CANONICAL_SQL"

git diff --find-renames --stat \
  3ce95977a417c3bdbdb1787d37186713cbb4bd5f..HEAD \
  -- docs/database/scripts

git diff --find-renames --name-status \
  3ce95977a417c3bdbdb1787d37186713cbb4bd5f..HEAD \
  -- docs/database/scripts

rg --files docs/database/scripts | sort
```

Nếu hash khác baseline, dùng file tại HEAD thực tế và lập delta trước khi tiếp tục.

Xác minh no-staging bằng static scan:

```bash
if rg -n '^[[:space:]]*CREATE[[:space:]]+(TABLE|VIEW)[[:space:]]+`?pems_seed_' "$CANONICAL_SQL"; then
  echo 'FAIL: seed helper DDL is present'
  exit 1
fi

rg -n 'pems_seed_' "$CANONICAL_SQL"
```

Ở hash đã khóa, lệnh thứ hai chỉ được định vị guard/check prefix cuối file; tuyệt đối không được có `CREATE TABLE`, `CREATE VIEW`, insert/update/drop dependency vào helper cụ thể.

Quét consumer còn trỏ tới tên SQL cũ:

```bash
rg -n --hidden \
  -g '!**/.git/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/dist/**' -g '!**/node_modules/**' \
  'PEMS_FULL_.*\.sql|PEMS_FULL_V2_SEED_COMPLETE_CONTACT_GUARD_AND_DASHBOARD_COVERAGE|GUARD_LATEST|PEMS_FULL_V2_TRANSLATION_GALLERY_FULL' \
  .
```

Mỗi hit phải được phân loại: canonical hiện hành, tài liệu lịch sử, fixture/test bootstrap đang chạy hoặc stale reference.

### Phase B — Lập manifest SQL máy đọc được

Từ SQL hoặc tốt hơn từ `information_schema` sau khi import disposable, lập manifest đủ:

- table/view/trigger/procedure;
- column name, ordinal, data type/store type;
- unsigned, length, precision/scale;
- nullable, default, generated/computed;
- PK, alternate/composite key;
- unique/non-unique/fulltext index;
- FK columns, principal columns, update/delete action;
- check constraints;
- enum/set values;
- trigger event/timing và invariant;
- seed API config/status;
- final self-check query.

Tách manifest thành:

- persistent runtime objects;
- transient/helper objects — SQL mới phải là 0;
- direct-seed blocks;
- seed-only enrichment/verification statements.

Với ba direct-seed blocks, ghi rõ:

- request template count `19/90/8`;
- request IDs và campus instances được join;
- số detail dự kiến/thực tế;
- `created_at/created_by/updated_at/updated_by`;
- thời điểm downstream seed bắt đầu đọc detail;
- thời điểm tạo baseline form/request revisions;
- mọi `NOT EXISTS`/idempotency guard;
- nguy cơ duplicate, missing detail hoặc stale snapshot nếu statement bị đổi thứ tự.

Không dùng regex đơn giản làm bằng chứng duy nhất cho SQL nhiều dòng. Regex chỉ dùng để định vị; kết luận cuối dựa trên MySQL 8 `information_schema` hoặc parser đáng tin cậy.

### Phase C — Import SQL an toàn trên MySQL 8 disposable

Trước import:

- chứng minh instance/container là disposable;
- in `SELECT DATABASE(), @@hostname, @@port;`;
- dùng allowlist tên DB;
- abort nếu target là DB thật;
- không dùng credential production.

Sau import:

1. Xác nhận script chạy tới cuối không có lỗi bị bỏ qua.
2. Xác nhận 81 persistent base tables.
3. Thu toàn bộ `check_name/issue_count`; mọi `issue_count` phải bằng 0.
4. Gọi/kiểm tra Pure V2 assertions.
5. Static scan phải chứng minh helper table/view không được tạo; post-import phải có 0 table/view prefix `pems_seed_`. Không được mô tả sai thành “đã tạo rồi drop”.
6. Xác nhận không có discriminator ở cả:
   - `visit_requests.form_schema_version`;
   - `visit_request_pending_forms.form_schema_version`.
7. Xác nhận không có 10 global-form columns trên `visit_requests`.
8. Xác nhận:
   - số `visit_request_campuses` bằng số `visit_instance_form_details`;
   - không thiếu detail;
   - không có orphan detail;
   - mỗi request template direct-seed phủ đúng các campus rows của nó;
   - downstream seed consumer không đọc detail trước khi detail tồn tại;
   - baseline revision snapshot phản ánh dữ liệu sau enrichment, không phải dữ liệu trung gian.
9. Xác nhận trigger count, FK count, procedures và indexes quan trọng.
10. Xác nhận `merged_runtime_table_count`, `pure_v2_seed_helper_objects` và mọi final check đều 0.
11. Chạy import lần hai trên chính disposable DB để kiểm tra rerunnable fresh-create.
12. Xác nhận quá trình transform/import không tạo hoặc thay đổi `pems_db` ngoài database/container disposable đã được cho phép.

Không dùng `--force` để nuốt lỗi MySQL.

### Phase D — Audit EF Core model và entity

Đối chiếu toàn bộ `ApplicationDbContext`, `IApplicationDbContext`, entity attributes/configurations với 81 bảng.

Với từng entity:

- table name;
- property ↔ column;
- CLR type ↔ MySQL type/unsigned;
- nullable;
- max length;
- precision/scale;
- default/store-generated;
- enum/string conversion;
- key/index;
- relationship;
- FK delete/update behavior;
- computed/generated column có bị EF cố ghi hay không;
- row version/concurrency semantics.

Tạo một integration contract test trên disposable DB để:

- build EF model;
- thực thi query tối thiểu cho mỗi mapped DbSet;
- phát hiện `Unknown column`, sai table, sai composite key, sai relationship;
- kiểm tra write round-trip cho các bảng trọng yếu;
- rollback/cleanup sau test.

Trong phiên audit này không commit test; chỉ mô tả test cần tạo và có thể dùng script tạm ngoài repo để lấy bằng chứng.

### Phase E — Zero-remnant scan V1

Chạy tối thiểu:

```bash
rg -n --hidden \
  -g '!**/bin/**' -g '!**/obj/**' -g '!**/dist/**' -g '!**/node_modules/**' \
  'FormSchemaVersion|form_schema_version|formSchemaVersion|FormSchemaVersions|PER_CAMPUS_V2_MIN' \
  backend frontend tests

rg -n --hidden \
  -g '!**/bin/**' -g '!**/obj/**' -g '!**/dist/**' -g '!**/node_modules/**' \
  'PerCampusFormV2|PerCampusFormV2Write|VISIT_FORM_V1_RETIRED|unsupported-version|dual-read|legacy global|compatibility projection' \
  backend frontend tests

rg -n --hidden \
  -g '!**/bin/**' -g '!**/obj/**' \
  'DelegationName|VisitTypeOther|WorkingContent|WorkingLanguage|TransportationNote|MediaConsentStatus|MediaConsentNote|NoteToFptu' \
  backend tests

rg -n --hidden \
  -g '!**/bin/**' -g '!**/obj/**' -g '!**/dist/**' -g '!**/node_modules/**' \
  'pems_seed_visit_request_form_v2|pems_seed_visit_requests_v2_compat|PEMS_FULL_V2_SEED_COMPLETE_CONTACT_GUARD_AND_DASHBOARD_COVERAGE|PEMS_FULL_V2_TRANSLATION_GALLERY_FULL|VISION_GUARD_LATEST\.sql' \
  .
```

Với nhóm property form, không được xóa mù quáng vì tên giống nhau vẫn hợp lệ trong DTO hoặc `VisitInstanceFormDetail`. Phải phân loại:

- truy cập `VisitRequest.<global field>`: lỗi Pure V2;
- truy cập `VisitInstanceFormDetail.<field>`: hợp lệ;
- DTO/API field: hợp lệ nếu dữ liệu được map từ instance;
- comment/test legacy: cleanup hoặc archive.

Kết quả phải có `ZERO-REMNANT INVENTORY` với mỗi hit runtime và quyết định xử lý.

Đối với seed/helper hit:

- runtime code/test bootstrap phụ thuộc helper hoặc filename đã xóa: FAIL;
- tài liệu lịch sử có nhãn archive rõ ràng: có thể giữ;
- final SQL guard dùng prefix `pems_seed_`: hợp lệ;
- không được “sửa” bằng cách phục hồi staging/helper object đã bị SQL mới loại bỏ.

### Phase F — Audit toàn bộ write path

Theo trace từ Controller → Command → Validator → Handler/Service → EF/SQL → audit/notification:

```text
authenticated create
public initiate OTP
public verify/create
idempotent replay
15-minute duplicate guard
pending edit
rejected resubmit
safe edit
amendment submit/approve/reject/withdraw/expire
initial primary-contact claim
contact transfer/resend/cancel/accept/decline/expire/redact
request/campus cancellation
approve/reject campus + host assignment
```

Với mỗi luồng, xác nhận:

- không ghi cột đã xóa;
- tạo đủ request/instance/detail/member-link/revision/audit trong cùng transaction;
- không để partial data;
- tính `visit_scope` và `has_mixed_campus_details` server-side;
- concurrency token đúng request và từng instance;
- failure/duplicate/retry không gửi notification hai lần;
- quyền registrant và ACTIVE primary contact đúng lifecycle;
- `visitor_user_id` không được gán trước claim khi email khác;
- không có V1 handler/service còn được DI hoặc gọi gián tiếp.
- không tạo staging/helper object hoặc mô phỏng quy trình seed trong runtime;
- request/campus/detail/member-link được tạo trực tiếp từ payload canonical, không copy mù quáng một request-level snapshot sang mọi campus;
- operational contact và guest/support member của từng campus giữ đúng dữ liệu riêng khi input khác nhau.

### Phase G — Audit query/projection và scope trước keyword

Audit toàn bộ:

```text
list/detail/search/filter/sort/paging
dashboard
calendar
host candidate/conflict
invitation
visit process/contribution
department reception task
feedback
meeting minutes
documents
reports/expense/invoice/export
email templates/actions/history
notifications
photos/face scan
news/partner/gallery links
```

Quy tắc:

1. Scope/authorization phải được áp dụng trước keyword/projection.
2. Instance-scoped surface lấy đúng `FormDetail` của instance đó.
3. Request-scoped surface không được lấy “campus đầu tiên” tùy tiện.
4. Nếu cần representative label, phải có rule deterministic đã được ghi rõ và không làm rò rỉ campus ẩn.
5. Request mixed phải trả cấu trúc per-campus hoặc label an toàn; không giả định global text.
6. Không N+1.
7. Search các field form phải đi qua `visit_instance_form_details`, không qua cột đã xóa.
8. Full-text/index usage phải khớp SQL mới.

Tạo `QUERY CONSUMER MATRIX` gồm:

```text
Surface/API
Actor/relation
Scope filter
Source table
Field projection
Mixed behavior
Authorization evidence
Index/performance note
Status: PASS/PARTIAL/FAIL
```

### Phase H — Audit enum, constants và state machine

Trích toàn bộ enum trong SQL rồi đối chiếu với:

- Domain enums/constants;
- validator whitelist;
- handler transition;
- DTO;
- frontend union/option/badge/filter;
- seed;
- unit/integration/E2E tests.

Ít nhất phải có matrix cho:

```text
role_code + sub_role
visit_requests.status
visit_request_campuses.status
visit_scope
visit_type
participant_role/status
identity change kind/status/method
amendment status/change class/history source type
working_language
media_consent_status
document owner_type
translation source/status/language
gallery item/media/status/type
face scan/review status
expense status/event
logistics status/type/priority/coordination
news/FAQ/partner statuses
```

Không thêm enum value ngoài SQL. Không giữ frontend option đã bị SQL bỏ.

### Phase I — Audit API contract và frontend

Lập API matrix:

```text
Route/method
Auth
Request DTO
Response DTO
Nullable/required
Enum
Error code
Frontend caller
UI state
Status
```

Pure V2 frontend phải kiểm tra:

- entry CTA luôn mở V2 hợp lệ;
- không còn phụ thuộc capability OFF để quay về V1;
- không còn route theo `formSchemaVersion`;
- detail/edit/resubmit/modal/deep link cùng dùng contract V2;
- list row không cần discriminator;
- form payload không gửi derived fields;
- schedule tối thiểu 30 phút ở FE và BE;
- field length/nullable/required khớp contract đã chốt;
- stale response/dirty form/concurrency errors được xử lý;
- per-campus member/contact/content không bị copy ngầm khi save;
- i18n labels và error codes còn đủ;
- không có mock/stub che API thật.

### Phase J — Audit Translation, Gallery, FAQ, Partner và Vision

Xác nhận end-to-end:

#### FAQ/Partner

- create/update lưu VI và EN đúng transaction;
- manual EN không bị auto-translation ghi đè;
- public read/search dùng translation table + fallback đã chốt;
- translation provider failure policy rõ ràng;
- source hash/status/outdated/retry đúng;
- cache/service runtime-translation cũ nếu không còn dùng phải được đánh dấu dead code.

#### Gallery

- Area/Location/Item/Media mapping đủ translation metadata;
- `gallery_items.description` không còn bị đọc/ghi;
- mô tả + audio lấy từ `gallery_item_contents`;
- không còn runtime dependency `gallery_item_tts_audios`;
- title/description/caption/alt VI–EN và retry/outdated đúng;
- video thumbnail và file ownership không regression.

#### Google Vision

- config code, quota, secret reference đúng;
- không lưu credential thô;
- chỉ ACCEPTED STUDENT đúng instance được upload;
- scan permission đúng host/staff workflow hiện hành;
- detection boxes/precision/count invariant đúng;
- guest tag bắt buộc thuộc exact instance;
- confirm idempotent, không tạo trùng face tag;
- không có auto identity recognition/embedding/emotion.

### Phase K — Security và authorization

Không coi frontend hide button là authorization.

Với từng endpoint bị tác động, kiểm tra:

```text
authentication
role_code + sub_role
campus scope
department scope
record relation
participant relation
lifecycle status
ownership
anti-enumeration
PII masking
audit
rate limit/idempotency
```

Đặc biệt:

- Admin không tự có quyền xem nghiệp vụ visit;
- HO monitor/read-only, không approve/reject campus;
- Staff Leader xử lý đúng campus;
- Host/participant chỉ thấy instance liên quan;
- registrant và primary contact có quyền theo lifecycle đã chốt;
- public token/OTP không cấp relation trước xác minh;
- scope phải áp dụng trước search.

### Phase L — Build và test

Trước khi chạy integration tests, kiểm tra test bootstrap:

1. Canonical SQL path phải resolve đúng file hash `7ec63e90...`.
2. Thiếu file hoặc hash/path không đúng phải dừng ngay, không được bỏ qua.
3. Target database phải khớp allowlist `pems_test_run_<32 hex>` hoặc chạy trong container hoàn toàn disposable.
4. Không được chỉ `Replace("USE pems_db")` rồi giả định an toàn; phải loại/đổi mọi database-selection statement và re-scan toàn script tạm.
5. Sau import và trước khi khởi động app factory, assert:

```text
DATABASE() = disposable target
base table count = 81
pems_seed_* object count = 0
missing instance detail = 0
legacy/discriminator columns = 0
all issue_count = 0
```

6. Nếu import/assert fail, test suite phải fail rõ nguyên nhân và cleanup database disposable.
7. Ghi hash/path SQL thực tế vào test evidence; không log connection string/credential.

Xác nhận command thật từ project:

```bash
dotnet restore PEMS.slnx
dotnet build PEMS.slnx --no-restore
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj --no-build
dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj --no-build
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj --no-build

cd frontend/pems-react
npm ci
npm run lint
npm run test:unit
npm run build
```

Chỉ chạy E2E/real-stack sau khi disposable DB và outbound integration sink đã được xác nhận an toàn:

```bash
npm run test:e2e
npm run test:e2e:realstack
```

Không gửi email thật, gọi translation/OCR/Vision tính phí hoặc ghi Google Drive thật trong test nếu chưa có explicit test sink/mocking policy.

## 8. Báo cáo đầu ra bắt buộc

Trả về một file Markdown duy nhất, cấu trúc:

### 1. Executive Summary

- HEAD/branch audited;
- SQL hash;
- build/test thực chạy;
- số P0/P1/P2/P3;
- kết luận hiện tại có thể chạy với SQL mới hay chưa.

### 2. Baseline và Evidence

- commands;
- kết quả;
- giới hạn môi trường;
- không che lỗi bằng câu “chưa có thời gian”.

### 3. SQL Delta Summary

- 81 persistent tables;
- object mới/xóa/thay đổi;
- invariant/trigger/check quan trọng.
- delta chính xác từ hash `5165e088...` sang `7ec63e90...`;
- persistent runtime DDL thay đổi hay không;
- removal của staging table/helper view;
- ba direct-seed blocks `19/90/8`;
- downstream join/enrichment/revision order;
- final guard/check `pems_seed_*`;
- tên SQL cũ đã xóa và mọi consumer còn trỏ sai.

Kèm `SEED PHASE MATRIX`; không trộn seed-only tuple logic với runtime business rule.

### 4. Full Schema–Code Matrix

Một dòng cho từng bảng, tối thiểu:

```text
DB object
Entity
DbSet/config
Read consumers
Write consumers
Frontend contract
Test coverage
Status
Evidence
Gap ID
```

Không chỉ lập matrix cho bảng mới.

### 5. V1/Legacy Remnant Inventory

Phân loại toàn bộ discriminator, global field, feature flag, route, handler, service, DTO, frontend và test V1 còn sót.

### 6. Enum & State Machine Matrix

SQL ↔ backend ↔ frontend ↔ test, nêu value thừa/thiếu/casing sai/transition sai.

### 7. Query & Authorization Matrix

Đặc biệt cho list/search/dashboard/report/export/email và mixed multi-campus.

### 8. Gap Register

Mỗi gap:

```text
ID
Severity: P0/P1/P2/P3
Module
Evidence file:line
SQL evidence
Failure mode
Data/security impact
Recommended fix
Dependencies
Tests required
```

Severity:

- P0: app không chạy, ghi sai dữ liệu, auth/secret nghiêm trọng;
- P1: luồng chính sai hoặc thiếu;
- P2: edge case, cleanup cần thiết, performance đáng kể;
- P3: comment/doc/naming/dead code không ảnh hưởng runtime.

### 9. Conflict Register

Mọi mâu thuẫn SQL ↔ business docs ↔ code, với phương án và điểm cần chủ dự án chốt.

### 10. Kế hoạch cập nhật đề xuất

Kế hoạch phải theo dependency, không theo thư mục:

1. **Phase 0 — Safety/P0 blockers**
   - secret handling;
   - disposable DB;
   - baseline;
   - khóa đúng canonical SQL path/hash;
   - sửa test bootstrap fail-open/stale filename trong kế hoạch;
   - EF unknown-column blockers.
2. **Phase 1 — Domain/EF Pure V2 contract**
   - entity, DbContext, relationship, enum, pending form, guard.
   - không tạo entity/DbSet/migration cho seed helper đã bị loại bỏ.
3. **Phase 2 — V2 write/read core**
   - create/OTP/replay/edit/resubmit/identity/amendment;
   - bỏ discriminator và V1 fallback.
4. **Phase 3 — All downstream consumers**
   - list/search/dashboard/calendar/report/export/email/photo/etc.
5. **Phase 4 — API + Frontend contract**
   - routing/modal/types/capability/error handling.
6. **Phase 5 — Translation/Gallery/FAQ/Partner/Vision/Expense contract closure**
7. **Phase 6 — Tests, real-stack, performance, security regression**
   - integration bootstrap import đúng SQL direct-seed và fail-closed;
   - schema/seed assertions chạy trước test.
8. **Phase 7 — Dead-code/config/docs cleanup**
   - xóa stale SQL path/reference;
   - chỉ giữ một canonical entrypoint rõ ràng.

Với mỗi phase ghi:

- mục tiêu;
- file/module dự kiến;
- thay đổi logic;
- migration/config impact;
- test;
- exit criteria;
- rollback;
- phụ thuộc phase trước;
- đề xuất gom commit theo functional slice.

Không tạo commit chỉ chứa một report nếu có thể gom cùng functional slice. Không thêm tên AI vào commit metadata.

### 11. Definition of Done đề xuất

Tối thiểu:

```text
[ ] Canonical SQL đúng path, Git blob 825b9567..., SHA-256 7ec63e90..., 14,832 dòng.
[ ] SQL import MySQL 8 disposable thành công, rerun thành công.
[ ] 81 persistent tables và mọi issue_count = 0.
[ ] Không có CREATE/runtime dependency vào pems_seed_*; post-import helper object count = 0.
[ ] Ba direct-seed blocks phủ đủ campus instance; detail/revision snapshot đúng thứ tự.
[ ] Integration bootstrap fail-closed và chứng minh đã import đúng canonical hash.
[ ] Không còn runtime/test reference tới các tên SQL đã xóa.
[ ] Import test không tạo/chạm pems_db ngoài môi trường disposable được phép.
[ ] Không còn entity map cột không tồn tại.
[ ] Không còn runtime dependency form_schema_version/FormSchemaVersion.
[ ] Không còn read/write 10 global-form columns trên visit_requests.
[ ] Mọi active request có instance detail đầy đủ.
[ ] V2 create/OTP/replay/edit/resubmit/claim/transfer/amendment xanh.
[ ] Feature/config không thể làm cả V1 và V2 cùng bất khả dụng.
[ ] Query instance/request/mixed đúng và scope-before-keyword.
[ ] Enum/constants/DTO/frontend unions khớp SQL.
[ ] Translation/Gallery/FAQ/Partner/Vision/Expense contract xanh.
[ ] Unit/Architecture/Integration/frontend build/test xanh.
[ ] E2E real-stack critical journeys xanh.
[ ] Không có credential plaintext trong trạng thái cuối.
[ ] Không có dead V1 handler/service được DI hoặc reachable.
[ ] Không regression permission, audit, notification, idempotency.
```

## 9. Tiêu chuẩn kết luận

Không được kết luận “code đã khớp SQL” chỉ vì:

- build compile;
- entity class tồn tại;
- unit test dùng in-memory DB xanh;
- SQL có seed;
- endpoint trả 200 ở happy path;
- tài liệu cũ ghi “FINAL”.

Chỉ kết luận READY khi:

1. SQL đúng hash được import thật trên MySQL 8 disposable;
2. direct seed/no-staging assertions đạt và test bootstrap chứng minh đã dùng đúng SQL;
3. EF model chạy thật trên schema đó;
4. zero-remnant scan Pure V2 + stale SQL/helper reference đạt;
5. write/read/downstream/frontend contracts đều có evidence;
6. test matrix xanh;
7. P0/P1 bằng 0 hoặc có quyết định chấp nhận rủi ro rõ ràng từ chủ dự án.

## 10. Câu lệnh bắt đầu

Bắt đầu ngay bằng:

1. preflight repo/branch/HEAD/working tree;
2. verify path/blob/SHA-256/line count SQL;
3. lập delta `5165e088... → 7ec63e90...` và xác nhận direct-seed/no-staging;
4. audit mọi SQL filename consumer, đặc biệt integration bootstrap fail-open;
5. tạo disposable DB plan và safety proof;
6. lập SQL + seed-phase manifest;
7. chạy zero-remnant/stale-reference scan;
8. lập gap register trước;
9. sau đó mới lập implementation plan.

Trong phiên này không sửa code. Nếu phát hiện P0, báo rõ nhưng vẫn hoàn thành audit các vùng còn lại trong khả năng, trừ khi tiếp tục sẽ gây rủi ro dữ liệu hoặc lộ secret.
