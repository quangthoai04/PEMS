# TASK — PEMS SQL CLEANUP RE-AUDIT & SAFE IMPLEMENTATION

## 0. BỐI CẢNH

Đây là **kế hoạch cleanup đề xuất**, KHÔNG phải kết luận cuối cùng.

Các đánh giá trước đó về field / ENUM / index / default / constraint có thể:

- đúng;
- đúng một phần;
- đã lỗi thời vì code thay đổi;
- chỉ nhìn thấy write/read nhưng bỏ sót business logic;
- bỏ sót frontend/API/test/seed/trigger;
- bỏ sót dữ liệu thật trong DB;
- hoặc nếu clean theo đề xuất có thể làm mất behavior đã triển khai.

**Không được coi bất kỳ mục "REMOVE / DROP / THU ENUM" nào bên dưới là lệnh bắt buộc.**

Mục tiêu của task này là:

```text
ĐỌC CODE + SQL + DATA THỰC TẾ LẠI TỪ ĐẦU
        ↓
XÁC MINH TỪNG ĐỀ XUẤT
        ↓
SAFE + THỰC SỰ DƯ THỪA
        → CLEAN ĐỒNG BỘ

CÓ ẢNH HƯỞNG LOGIC / CONTRACT / DATA / SECURITY
        → KHÔNG TỰ CLEAN
        → REPORT EVIDENCE
        → ĐÁNH DẤU CONFIRM REQUIRED
        → CHỜ OWNER XÁC NHẬN
```

Nguyên tắc quan trọng nhất:

> **Không được hy sinh logic đã triển khai chỉ để SQL trông gọn hơn.**

---

# 1. MỤC TIÊU

Rà soát lại toàn bộ các đề xuất cleanup ở các nhóm sau:

1. `files`
2. `partners`
3. `partner_translations`
4. `partner_aliases`
5. `documents`

và mọi:

- Entity;
- EF mapping;
- DTO;
- handler/service;
- controller/API;
- frontend type/API/UI;
- enum/constants;
- seed;
- trigger;
- FK;
- index;
- migration;
- report/export;
- authorization;
- background job;
- integration;
- test;

có liên quan trực tiếp.

Sau khi audit:

### Tự triển khai

Chỉ được tự triển khai những thay đổi được chứng minh là:

```text
SAFE_REMOVE
```

hoặc:

```text
SAFE_SIMPLIFY
```

tức là:

- không còn producer cần thiết;
- không còn reader có ý nghĩa;
- không ảnh hưởng business decision;
- không ảnh hưởng auth/security;
- không ảnh hưởng API/UI contract cần giữ;
- không làm mất dữ liệu có ý nghĩa;
- không đổi workflow;
- không đổi URL/public contract;
- không đổi lifecycle;
- không đổi retention/audit semantics;
- migration có thể thực hiện an toàn.

### Không tự triển khai

Nếu thay đổi làm thay đổi logic hiện tại, phải:

```text
CONFIRM_REQUIRED
```

và **không thực hiện thay đổi đó** trước khi owner xác nhận.

---

# 2. GIT SAFETY — BẮT BUỘC

Trước khi làm:

```bash
git status
git branch --show-current
git rev-parse HEAD
git log -1 --oneline
```

Ghi rõ:

```text
Current branch:
Current HEAD:
Modified files:
Untracked files:
```

Không:

- reset working tree;
- checkout đè file;
- stash/drop thay đổi của người khác;
- overwrite unrelated work;
- commit/push nếu chưa được yêu cầu.

Nếu có thay đổi pre-existing:

```text
PRESERVE
```

và phân biệt rõ:

```text
PRE-EXISTING
vs
THIS TASK
```

---

# 3. NGUYÊN TẮC AUDIT — KHÔNG SEARCH TÊN RỒI KẾT LUẬN

Với **MỖI field / ENUM value / index / default / FK / trigger candidate**, phải trace đủ:

```text
SQL DDL
↓
Seed / historical data
↓
Entity
↓
EF configuration
↓
Write producers
↓
Read consumers
↓
Business branches
↓
Authorization/security
↓
API DTO / response
↓
Frontend type/API/UI
↓
Report/export/email
↓
Background jobs
↓
Tests
↓
Migration
↓
Real DB data
```

Không được kết luận:

```text
"search ít hit → dư"
```

hoặc:

```text
"chỉ write → xóa"
```

một cách tự động.

---

# 4. PHÂN LOẠI USAGE BẮT BUỘC

Với mỗi field/value, phân loại chính xác:

## A. DECLARATION_ONLY

Chỉ tồn tại ở:

- DDL;
- Entity;
- constant;
- type;

không producer / consumer.

Có thể là candidate remove mạnh.

---

## B. WRITE_ONLY_METADATA

Có producer ghi nhưng không reader.

Không mặc định là dư.

Phải hỏi:

- provenance có cần không?
- audit có cần không?
- migration/history có cần không?
- external integration có đọc trực tiếp DB không?
- có mục tiêu integrity không?

---

## C. DTO_ONLY / DISPLAY_ONLY

Được trả API / hiển thị UI nhưng không ra quyết định.

Có thể cleanup nhưng sẽ ảnh hưởng contract/UI.

Nếu xóa làm thay đổi response/UI:

```text
CONFIRM_REQUIRED
```

trừ khi UI/contract đó đã được owner xác nhận là obsolete.

---

## D. QUERY_USED

Có:

```text
WHERE
JOIN
ORDER BY
GROUP BY
FILTER
SEARCH
```

Phải giữ hoặc refactor consumer trước.

---

## E. BUSINESS_LOGIC

Có branch:

```text
if / switch / policy / validator / state transition
```

Không được xóa tự động.

```text
CONFIRM_REQUIRED
```

nếu đề xuất thay đổi behavior.

---

## F. SECURITY / AUTHORIZATION

Có liên quan:

- ownership;
- permission;
- access;
- download authorization;
- session;
- file permission;
- campus scope;
- visibility;

=> mặc định:

```text
KEEP
```

trừ khi có replacement tương đương được chứng minh.

---

## G. AUDIT / HISTORY

Dùng để trace:

- ai;
- lúc nào;
- nguồn;
- trạng thái lịch sử;

Không xóa chỉ vì UI không đọc.

Nếu muốn bỏ semantics audit:

```text
CONFIRM_REQUIRED
```

---

# 5. MỖI CANDIDATE PHẢI CÓ DECISION CODE

Chỉ dùng các decision sau:

```text
KEEP_REQUIRED
KEEP_INTENTIONAL_METADATA
SAFE_REMOVE
SAFE_SIMPLIFY
CONDITIONAL_AFTER_MIGRATION
CONFIRM_REQUIRED_LOGIC_CHANGE
CONFIRM_REQUIRED_API_UI_CHANGE
CONFIRM_REQUIRED_DATA_SEMANTICS
BLOCKED_INSUFFICIENT_EVIDENCE
```

Không dùng từ mơ hồ như:

```text
probably
seems unused
likely safe
```

mà không có evidence.

---

# 6. CHECKLIST ĐỀ XUẤT — `files`

Các mục sau chỉ là **hypothesis cần re-audit**.

## 6.1 `bucket_name`

Đề xuất cũ:

```text
REMOVE
```

Phải kiểm tra lại:

- `UploadedFile.BucketName`;
- `StoredFileInfo`;
- upload handlers;
- report email/archive;
- DTO/projection;
- any storage abstraction;
- tests;
- migration;
- real data.

Chỉ SAFE_REMOVE nếu:

```text
0 runtime decision
0 storage locator dependency
0 meaningful persisted data
0 external contract dependency
```

Nếu storage abstraction tương lai vẫn dựa vào bucket semantics:

```text
KEEP_INTENTIONAL_METADATA
```

hoặc report.

---

## 6.2 `storage_provider`

Current proposal muốn hướng tới:

```text
GOOGLE_DRIVE
OTHER
```

và loại:

```text
S3
AZURE
GCS
```

`LOCAL` chỉ loại sau migration.

Audit lại toàn bộ producers/consumers:

```text
LOCAL
S3
AZURE
GCS
GOOGLE_DRIVE
OTHER
```

### S3 / AZURE / GCS

Chỉ remove enum value nếu:

```text
0 production producer
0 production consumer
0 real DB row
0 migration/compat requirement
```

### LOCAL

KHÔNG remove nếu còn bất kỳ:

- local file producer;
- local storage reader;
- `IFileStorageService` binding;
- report attachment/local archival;
- existing `files.storage_provider='LOCAL'`;
- local bytes chưa migrate.

Nếu muốn remove `LOCAL` nhưng cần chuyển architecture:

```text
CONFIRM_REQUIRED_LOGIC_CHANGE
```

hoặc:

```text
CONDITIONAL_AFTER_MIGRATION
```

Không tự chuyển toàn bộ storage architecture chỉ để clean ENUM.

---

## 6.3 `checksum_sha256`

Đề xuất hiện tại:

```text
KEEP COLUMN
candidate DROP idx_files_checksum
```

Audit:

- checksum compute;
- integrity;
- dedupe;
- validation;
- query;
- incident/debug;
- API contract.

Không xóa column chỉ vì chưa có `WHERE checksum`.

Index chỉ drop sau index audit.

---

## 6.4 `external_file_id`

Column proposal:

```text
KEEP
```

Index proposal:

```text
idx_files_external_file_id → candidate DROP
```

Phải search query từ:

```text
external provider id → files row
```

Nếu không có consumer và không FK/unique requirement:

index có thể SAFE_REMOVE.

Column phải giữ nếu Google Drive/YouTube locator còn dùng.

---

## 6.5 Các index của `files`

Audit tối thiểu:

```text
PRIMARY(file_id)
uq_files_object_key
idx_files_uploaded_by(uploaded_by, uploaded_at)
idx_files_mime_time(mime_type, uploaded_at)
idx_files_checksum(checksum_sha256)
idx_files_external_file_id(external_file_id)
idx_files_purpose_time(file_purpose, uploaded_at)
fk_files_uploaded_by
```

Không drop index chỉ vì:

```text
table hiện ít row
```

Phải map:

```text
runtime query shape
+
left-most prefix
+
FK/UNIQUE dependency
+
expected scale
+
EXPLAIN
```

---

# 7. CHECKLIST ĐỀ XUẤT — `partners`

## 7.1 `public_slug`

Đề xuất:

```text
candidate REMOVE
```

Nhưng việc bỏ nó có thể đổi URL:

```text
/partners/{slug}
→
/partners/{id}
```

Audit:

- public route;
- link generation;
- public detail;
- SEO/share links;
- frontend fallback;
- bookmarks;
- integration tests;
- API lookup;
- existing slug rows.

Nếu removal đổi public URL contract:

```text
CONFIRM_REQUIRED_API_UI_CHANGE
```

KHÔNG tự drop.

Nếu owner chưa confirm bỏ SEO slug, giữ.

---

## 7.2 `profile_status.DRAFT`

Đề xuất:

```text
REMOVE DRAFT
```

Audit:

- Create Partner;
- import;
- OCR flow;
- edit-before-submit;
- seed;
- tests;
- direct API;
- migration;
- real DB counts.

Query:

```sql
SELECT profile_status, COUNT(*)
FROM partners
GROUP BY profile_status;
```

Nếu DRAFT có data hoặc hidden flow:

```text
CONFIRM_REQUIRED_DATA_SEMANTICS
```

Nếu 0 producer + 0 rows + 0 consumer:

có thể SAFE_SIMPLIFY.

---

## 7.3 Default `profile_status`

Đề xuất:

```text
APPROVED
→
PENDING_APPROVAL
```

Đây có thể ảnh hưởng các insert không set explicit status.

Phải kiểm tra:

- mọi producer;
- seed;
- integration;
- direct SQL;
- import;
- tests.

Nếu runtime business rule thực sự yêu cầu approval nhưng DB default đang lệch:

có thể sửa, nhưng report evidence.

Nếu còn flow intentional auto-approved:

```text
CONFIRM_REQUIRED_LOGIC_CHANGE
```

---

## 7.4 Default `visibility`

Đề xuất:

```text
PUBLIC
→
INTERNAL
```

Đây là behavior/public exposure.

Phải audit mọi create path.

Nếu thay đổi có khả năng làm resource mới không còn public như trước:

```text
CONFIRM_REQUIRED_LOGIC_CHANGE
```

Không auto-change chỉ vì code handler thường set explicit value.

---

## 7.5 `ft_partners_search`

Đề xuất:

```text
DROP
```

Audit:

- `MATCH ... AGAINST`;
- raw SQL;
- stored query;
- EF Functions;
- site-wide search;
- external scripts.

Nếu tất cả runtime search đều:

```text
LIKE / Contains
```

và không constraint dependency:

SAFE_REMOVE.

---

# 8. CHECKLIST ĐỀ XUẤT — `partner_translations`

Có **mâu thuẫn trong các proposal cũ**, nên phần này phải audit đặc biệt cẩn thận.

---

## 8.1 `translation_status`

Đề xuất:

```text
REMOVE COLUMN
```

Current enum:

```text
PENDING
READY
FAILED
OUTDATED
```

Phải verify:

- producers từng value;
- readers;
- retry/background translation;
- failure handling;
- stale/outdated translation;
- UI badges;
- API filters;
- real data counts.

Query:

```sql
SELECT translation_status, COUNT(*)
FROM partner_translations
GROUP BY translation_status;
```

Nếu row existence thực sự đồng nghĩa "READY" và không có lifecycle:

SAFE_REMOVE.

Nếu có lifecycle dù ít dùng:

KEEP.

---

## 8.2 `source_hash`

Đề xuất:

```text
REMOVE
```

Audit chính xác:

- compute;
- compare;
- translation cache;
- retranslation avoidance;
- duplicate detection;
- background job;
- future stale check;
- migration.

Nếu:

```text
compute → write → never read
```

và không data contract:

SAFE_REMOVE.

---

## 8.3 `translated_at`

Đề xuất:

```text
REMOVE
```

Phải chứng minh nó luôn redundant với:

```text
created_at / updated_at
```

và không có reader/report.

Nếu có semantic "thời điểm dịch" độc lập:

KEEP.

---

## 8.4 `translation_source`

**Proposal cũ đang không thống nhất.**

Một bản đề xuất:

```text
KEEP AUTO / MANUAL / LEGACY
```

Bản khác đề xuất:

```text
AUTO / MANUAL only
LEGACY → MANUAL
```

KHÔNG được chọn một bản theo cảm tính.

Audit:

- Create Partner;
- Update Partner;
- OCR/import;
- seed;
- migration;
- existing DB;
- UI;
- analytics/report;
- provenance requirement.

Query:

```sql
SELECT translation_source, COUNT(*)
FROM partner_translations
GROUP BY translation_source;
```

Nếu `LEGACY` vẫn thể hiện provenance có ý nghĩa khác `MANUAL`:

```text
KEEP_INTENTIONAL_METADATA
```

Nếu `LEGACY` chỉ là cách cũ để nói "human/original" và owner muốn semantics chỉ là human vs machine:

```text
CONFIRM_REQUIRED_DATA_SEMANTICS
```

trước khi map:

```text
LEGACY → MANUAL
```

Không rewrite provenance history tự động.

---

## 8.5 `country`, `city`

Đề xuất:

```text
candidate REMOVE after reader migration
```

Phải chạy precheck:

```sql
SELECT COUNT(*)
FROM partner_translations t
JOIN partners p ON p.partner_id = t.partner_id
WHERE NOT (t.country <=> p.country);

SELECT COUNT(*)
FROM partner_translations t
JOIN partners p ON p.partner_id = t.partner_id
WHERE NOT (t.city <=> p.city);
```

Nếu khác `0`:

```text
CONFIRM_REQUIRED_DATA_SEMANTICS
```

Nếu bằng `0`, vẫn phải kiểm tra public query và API contract.

Việc đổi:

```text
translation.country/city
→
partners.country/city
```

có thể là SAFE_SIMPLIFY nếu output giữ byte/semantic equivalent.

Phải có regression test trước khi drop.

---

## 8.6 Index

Audit:

```text
PRIMARY(partner_translation_id)
UNIQUE(partner_id, language_code)
idx_partner_translations_lang_status
ft_partner_translations_search
```

Unique `(partner_id, language_code)` mặc định KEEP trừ khi invariant thay đổi.

`idx_partner_translations_lang_status` chỉ drop nếu status bị drop hoặc query không cần.

FULLTEXT chỉ drop nếu không có runtime `MATCH ... AGAINST`.

---

# 9. CHECKLIST ĐỀ XUẤT — `partner_aliases`

Đây là phần **rủi ro cao**, vì proposal không chỉ clean schema mà còn đổi workflow.

Proposal cũ:

```text
10 columns → 6 columns

REMOVE:
source
status
updated_at
updated_by

SOFT DELETE → HARD DELETE
```

## 9.1 `status`

Nếu hiện:

```text
ACTIVE
INACTIVE
```

và delete UI đang thực hiện:

```text
ACTIVE → INACTIVE
```

thì việc drop `status` + hard delete là **thay đổi behavior**.

Dù UI không có restore, đây vẫn là:

```text
SOFT DELETE
→
HARD DELETE
```

=> BẮT BUỘC:

```text
CONFIRM_REQUIRED_LOGIC_CHANGE
```

Không được auto implement.

Phải report:

- current deletion behavior;
- revive behavior;
- unique conflict behavior;
- audit/history;
- API semantics;
- matching semantics;
- tests;
- DB rows ACTIVE/INACTIVE.

---

## 9.2 `source`

Proposal:

```text
REMOVE
```

Current values có thể gồm:

```text
MANUAL
OCR
AUTO_MATCH
IMPORT
```

Phải audit producer/consumer/data.

Nếu UI hiển thị source label, removal thay đổi UI contract:

```text
CONFIRM_REQUIRED_API_UI_CHANGE
```

Nếu OCR source có provenance có ý nghĩa:

```text
KEEP_INTENTIONAL_METADATA
```

Không xóa chỉ vì matching không dùng field này.

---

## 9.3 `updated_at`, `updated_by`

Nếu chúng chỉ phục vụ soft-delete/revive:

có thể dư **sau khi** owner xác nhận hard delete.

Nhưng trước confirmation:

```text
KEEP
```

Không drop trước workflow decision.

---

## 9.4 Index

Audit:

```text
uq_partner_alias_key(partner_id, alias_name_key)
idx_partner_alias_lookup(alias_name_key, status)
idx_partner_alias_partner(partner_id)
```

Nếu status được giữ:

không tự đổi lookup index.

Nếu `(partner_id)` đã được unique composite cover cho FK/query:

`idx_partner_alias_partner` có thể redundant, nhưng verify FK backing + EXPLAIN trước.

---

# 10. CHECKLIST ĐỀ XUẤT — `documents`

## 10.1 `status`

Proposal:

```text
REMOVE COLUMN
```

Current UI/backend có thể vẫn expose:

```text
DRAFT
PUBLISHED
ARCHIVED
```

Dù producer hiện chỉ hard-code `PUBLISHED`, việc xóa status có thể làm mất:

- filter;
- badge;
- summary;
- API field;
- future/archive semantics;
- existing historical rows.

Phải audit:

```text
producer
reader
SearchDocuments
sort/filter
frontend filter
badge
summary
seed
real DB
tests
```

Query:

```sql
SELECT status, COUNT(*)
FROM documents
GROUP BY status;
```

Nếu removal cần bỏ UI filter/badge/summary:

```text
CONFIRM_REQUIRED_API_UI_CHANGE
```

Không auto implement.

---

## 10.2 `owner_type`

Proposal muốn thu ENUM về:

```text
VISIT
PARTNER
LOGISTICS
REPORT
VISIT_INSTANCE_MEDIA
```

và bỏ:

```text
GENERAL
MINUTES
NEWS
```

Phải audit mỗi value độc lập:

```text
producer
reader
legacy handler
existing DB rows
seed
migration
frontend/API
```

Không kết luận:

```text
"không producer mới → drop"
```

nếu historical/current rows vẫn cần đọc.

Nếu chỉ còn legacy rows nhưng read compatibility vẫn cần:

```text
CONFIRM_REQUIRED_DATA_SEMANTICS
```

hoặc migrate có kế hoạch.

---

## 10.3 `ft_documents_search`

Proposal:

```text
DROP
```

Chỉ drop nếu toàn runtime không dùng MySQL FULLTEXT.

---

## 10.4 `idx_documents_category`

Proposal:

```text
KEEP only if justified
```

Phải map query + EXPLAIN.

---

# 11. ENUM AUDIT — QUY TẮC CHUNG

Với **MỖI ENUM value** dự định remove:

Phải tạo bảng:

| Table.Column | Value | Producer | Consumer | DB Count | Seed Count | Test | Decision |
|---|---|---|---|---:|---:|---|---|

Không shrink ENUM nếu:

- DB còn row;
- seed còn row;
- migration/history cần;
- frontend gửi value;
- API accepts value;
- validator accepts value;
- switch/branch handles value;
- hidden workflow sinh value.

Nếu value chỉ có declaration nhưng 0 producer/consumer/data:

SAFE_REMOVE.

---

# 12. DEFAULT AUDIT — KHÔNG COI DEFAULT LÀ TRANG TRÍ

Với default đề xuất thay đổi, phải search mọi insert path xem:

```text
explicitly sets column?
or
relies on DB default?
```

Một default có thể không được code đọc nhưng vẫn là runtime behavior cho:

- seed;
- raw SQL;
- import;
- old endpoint;
- tests;
- maintenance scripts.

Nếu changing default changes resulting data:

```text
CONFIRM_REQUIRED_LOGIC_CHANGE
```

trừ khi mismatch với documented/current business invariant được chứng minh rõ.

---

# 13. INDEX AUDIT

Với mỗi index candidate:

1. `SHOW INDEX`
2. FK/UNIQUE dependency
3. runtime query shape
4. generated SQL nếu EF
5. left-most prefix
6. overlap
7. ORDER BY
8. LIKE/Contains behavior
9. expected table growth
10. EXPLAIN / EXPLAIN ANALYZE nếu an toàn

Decision:

```text
KEEP
DROP
ADD
```

Phải có reason.

Không:

```text
small table => drop
```

Không:

```text
future scale => keep
```

nếu không có query shape thật.

---

# 14. REAL DATABASE PRECHECK

Trước destructive change:

- clone DB hoặc backup;
- record row counts;
- record ENUM distribution;
- record NULL/non-NULL distribution cho field drop nếu liên quan;
- record orphan/FK state;
- record historical values.

Không chạy destructive SQL trên DB thật trước khi:

```text
audit decision complete
+
safe migration rehearsed on clone
```

---

# 15. CONFIRMATION GATE — QUY TẮC BẮT BUỘC

Nếu audit phát hiện proposed cleanup ảnh hưởng bất kỳ logic nào, KHÔNG tự quyết định.

Ví dụ bắt buộc confirm:

```text
public_slug removal
→ đổi URL contract

LOCAL storage removal
→ đổi storage architecture

partner_alias status removal
→ soft delete → hard delete

documents.status removal
→ bỏ filter/badge/lifecycle contract

translation_source LEGACY → MANUAL
→ rewrite provenance semantics

visibility default change
→ thay public exposure

profile_status default/value change
→ thay workflow

owner_type legacy value removal
→ có current/historical data reader
```

Output ngay:

```text
CONFIRM REQUIRED

Candidate:
Current behavior:
Proposed behavior:
Code paths affected:
API/UI affected:
Data affected:
Risk:
Safe alternatives:
Recommended option:
```

Tiếp tục audit các mục khác, nhưng **không implement candidate đó**.

---

# 16. SAFE CHANGE IMPLEMENTATION ORDER

Chỉ áp dụng với candidate đã xác minh SAFE.

## Phase A — Tests first

Thêm/điều chỉnh test chứng minh:

- behavior trước và sau không đổi;
- dropped metadata không còn contract cần thiết;
- enum removed không còn producer;
- query still works;
- auth/security unaffected.

---

## Phase B — Code compatibility

Remove đồng bộ:

```text
Entity
EF config
constants
handler writes
handler reads
DTO
API
frontend type
frontend UI
tests
```

Sau phase này code phải compile/typecheck với target model.

---

## Phase C — Migration

Script phải có:

```text
PRECHECK
DATA GUARD
DROP dependent FK/index
ALTER/DROP
VERIFY
```

Phải idempotent hoặc có explicit safe re-run behavior.

Không `DELETE` row chỉ để ALTER ENUM chạy.

---

## Phase D — Canonical SQL

Cập nhật authoritative master SQL:

- table columns;
- ENUM;
- default;
- index;
- FK;
- trigger;
- seed;
- comments.

Nếu canonical hash pin tồn tại:

```text
recompute SHA-256
update pin
```

---

## Phase E — Clone validation

Fresh import:

```text
PASS
```

Migration from current clone:

```text
PASS
```

Second migration run:

```text
PASS / clean no-op
```

---

# 17. KHÔNG ĐƯỢC LÀM

- Không coi proposal cũ là source of truth.
- Không xóa field vì “không thấy UI”.
- Không xóa audit/provenance chỉ vì write-only.
- Không đổi soft-delete → hard-delete không confirm.
- Không đổi public URL contract không confirm.
- Không đổi storage architecture không confirm.
- Không shrink ENUM khi còn data mà không report.
- Không rewrite history để schema đẹp.
- Không bỏ security field/index/FK mà không trace.
- Không comment/hard-code logic để compile.
- Không xóa DTO field mà không check frontend/API consumer.
- Không drop DB trước rồi mới sửa EF.
- Không global replace enum strings.
- Không sửa unrelated architecture.
- Không commit/push nếu chưa được yêu cầu.

---

# 18. VERIFICATION GATES

Sau mọi SAFE change đã implement:

## Backend

```text
dotnet build
unit tests
integration tests
architecture tests
```

Expected:

```text
0 failed
```

---

## Frontend

```text
typecheck/lint
build
unit tests
```

Expected:

```text
0 failed
```

---

## Database

```text
fresh canonical import
migration-on-clone
migration idempotency
FK/index verification
ENUM verification
seed verification
canonical hash verification
```

---

## Runtime smoke

Chạy affected flows thật.

Tối thiểu theo module đã clean:

### Files

- upload;
- download;
- authorization;
- email attachment;
- Gallery/YouTube;
- report archive.

### Partners

- create;
- approval/reject;
- update;
- public list/detail;
- search;
- translation;
- alias matching.

### Documents

- create/producers;
- list/search;
- detail;
- filters;
- download;
- campus authorization;
- logistics/report flows.

Không ghi PASS nếu chưa chạy thật.

---

# 19. OUTPUT BẮT BUỘC — PHẦN 1: RE-AUDIT MATRIX

Trước khi implement destructive change, xuất bảng:

| Table | Candidate | Proposal cũ | Producer | Reader | Logic impact | API/UI impact | DB rows | Decision |
|---|---|---|---|---|---|---|---:|---|

Decision phải dùng code ở §5.

---

# 20. OUTPUT BẮT BUỘC — PHẦN 2: CONFIRMATION QUEUE

Nếu có bất kỳ mục ảnh hưởng logic:

## CONFIRMATION QUEUE

### CONFIRM-01

```text
Table:
Field/Enum/Index:
Current behavior:
Proposed cleanup:
Why it is not purely redundant:
Affected files:
Affected endpoint/UI:
Existing data:
Risk:
Option A:
Option B:
Recommendation:
```

Không implement mục này.

---

# 21. OUTPUT BẮT BUỘC — PHẦN 3: SAFE CHANGES APPLIED

Chỉ liệt kê mục thực sự SAFE đã sửa:

```text
TABLE:
- removed columns:
- altered ENUM:
- defaults:
- indexes:
- FKs:
- triggers:
- code files:
- frontend files:
- tests:
```

---

# 22. OUTPUT BẮT BUỘC — PHẦN 4: DB DATA SAFETY

Report:

```text
DB used:
Clone/backup:
Precheck counts:
Legacy enum counts:
Dropped-column data characteristics:
Migration result:
Idempotency:
Fresh import:
Canonical SHA:
```

---

# 23. OUTPUT BẮT BUỘC — PHẦN 5: TEST GATES

Table:

| Gate | Result | Count / Evidence |
|---|---|---|
| Backend build | PASS/FAIL | |
| Backend unit | PASS/FAIL | |
| Backend integration | PASS/FAIL | |
| Architecture | PASS/FAIL | |
| Frontend typecheck | PASS/FAIL | |
| Frontend build | PASS/FAIL | |
| Frontend unit | PASS/FAIL | |
| Fresh DB import | PASS/FAIL | |
| Migration clone | PASS/FAIL | |
| Idempotency | PASS/FAIL | |
| Runtime smoke | PASS/FAIL/BLOCKED | |

---

# 24. DEFINITION OF DONE

Task chỉ được ghi:

```text
SQL CLEANUP RE-AUDIT COMPLETE
```

khi:

1. Mọi candidate trong 5 bảng đã được re-audit bằng code hiện tại.
2. Không quyết định dựa vào proposal cũ.
3. Mọi ENUM value đã có producer/consumer/data count.
4. Mọi index đã có dependency/query evidence.
5. SAFE candidates đã clean đồng bộ code + DB + FE + tests.
6. Không có `Unknown column` / enum mismatch / FK/index error.
7. Fresh import PASS.
8. Migration clone PASS.
9. Regression gates PASS.
10. Không mất behavior đã triển khai.

Nếu còn mục cần owner quyết định:

```text
SQL CLEANUP RE-AUDIT COMPLETE — CONFIRMATION REQUIRED
```

và list rõ các `CONFIRM-xx`.

Không được tự biến chúng thành DONE.

---

# 25. KẾT LUẬN PHƯƠNG PHÁP

Đây là **verification-first cleanup**, không phải schema-minimization exercise.

Ưu tiên theo thứ tự:

```text
1. Correctness
2. Existing business logic
3. Security / authorization
4. Data integrity / audit
5. API/UI compatibility
6. Performance/index correctness
7. Schema cleanliness
```

Nếu một column/ENUM nhìn "dư" nhưng đang giữ một behavior có chủ đích:

```text
KEEP
```

hoặc:

```text
CONFIRM REQUIRED
```

Không được phá behavior để đạt số lượng cột ít hơn.

---

# 26. FINAL INSTRUCTION

Bắt đầu bằng:

```text
AUDIT ONLY
```

Không chạy destructive migration ngay.

Hãy đọc code mới nhất và DB hiện tại, lập **Re-audit Matrix** trước.

Sau đó:

- mục nào `SAFE_REMOVE` / `SAFE_SIMPLIFY` → triển khai;
- mục nào ảnh hưởng logic/contract/data semantics → dừng mục đó và đưa vào **Confirmation Queue**;
- tiếp tục các mục safe còn lại;
- cuối cùng report đầy đủ evidence.

**Không commit/push nếu chưa được yêu cầu.**
