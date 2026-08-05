# Phase 0 — Preflight & audit map (evidence từ HEAD)

> Nguồn: đọc code/schema tại `Canh-Iter1 @ 33aba830`, không dựa vào tài liệu legacy.
> Đối chiếu với `PEMS_HARD_CUTOVER_OPERATIONAL_CONTACT_CONFIRMATION_AND_CAMPUS_APPROVAL_MASTER_PLAN.md`.

## 1. Baseline

| Mục | Giá trị |
|---|---|
| Branch / HEAD | `Canh-Iter1` @ `33aba830` |
| Working tree | sạch (chỉ có `docs/Ver2Carnh/removeDauMoi/` untracked) |
| Stashes | 9 (không đụng tới) |
| `dotnet build PEMS.slnx` | **xanh** (exit 0) |
| Canonical SQL | `docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql` — 17.198 dòng |
| Hash pin | `tests/PEMS.IntegrationTests/TestInfrastructure/CanonicalSqlScript.cs` (`ExpectedSha256`, `ExpectedBaseTableCount`, `ExpectedTriggerCount`) |

## 2. Điểm plan mô tả SAI so với HEAD (đã có sẵn, KHÔNG phải việc phải làm)

Plan §4.3 yêu cầu xóa một số thứ **vốn không tồn tại**, và §3.4/§2.2 mô tả một số thứ **đã có**:

| Plan nói | Thực tế HEAD |
|---|---|
| Xóa `WAITING_HO_APPROVAL` / `HO_APPROVED` | **Không tồn tại** — 0 hit trong toàn repo |
| Xóa HO request approve/reject handlers/routes | **Không tồn tại** — không có endpoint HO mutation trên visit request |
| Thêm `PARTIALLY_APPROVED` vào aggregate status | **Đã có** trong enum `visit_requests.status` + `VisitRequestStatuses` |
| Duyệt độc lập theo campus | **Đã có** — `ApproveCampusInstance` / `RejectCampusInstance` per `visit_instance_id` |
| Approve phải gán Host cùng transaction | **Đã có** — `HOST_REQUIRED_ON_APPROVAL` |
| Operational contact 4 trường per campus | **Đã có** trong `visit_instance_form_details.operational_contact_*` |
| Rate limit / dispatcher / idempotency email | **Đã có** — `IRateLimitService`, `email_send_idempotency`, `EmailActionTokenService` |

**Hệ quả:** phạm vi thật hẹp hơn plan, tập trung vào việc **chuyển đầu mối từ cấp request sang cấp campus** + **cổng xác nhận toàn cục**.

## 3. Mô hình hiện tại (cái phải xóa)

### 3.1 Request-level primary contact — `visit_requests`

```
visitor_user_id                 BIGINT NULL   -- chủ sở hữu đầu mối chính, luôn role VISITOR
contact_person_full_name        NOT NULL
contact_person_organization     NOT NULL
contact_person_phone            NOT NULL
contact_person_email            NOT NULL
primary_contact_access_status   ENUM('PENDING_CONFIRMATION','ACTIVE')
primary_contact_verified_at     DATETIME NULL
```
+ index `idx_visit_requests_visitor`, `idx_visit_requests_contact_access`, `idx_visit_requests_contact_email`
+ 3 CHECK trên `contact_person_*`
+ FULLTEXT `ft_visit_requests_frontend_search` chứa 3 cột `contact_person_*`
+ FK `fk_visit_requests_visitor`

### 3.2 Identity change — cấp REQUEST, không phải campus

`visit_request_identity_changes`: khóa theo `visit_request_id`, `target_relation ENUM('PRIMARY_CONTACT')`,
`change_kind ENUM('INITIAL_CLAIM','TRANSFER')`, generated `pending_guard = visit_request_id:target_relation`.
Không có `visit_instance_id`. Không có `token_hash` (token sống ở `email_action_tokens`).

### 3.3 Campus status còn `ASSIGNED`

`visit_request_campuses.status ENUM('WAITING_REQUEST_APPROVAL','ASSIGNED','BEFORE_VISIT',…)`.
`ASSIGNED` được đếm là "approved" trong 2 trigger aggregate và trong `VisitRequestAggregateStatusService`.

### 3.4 Trigger phải viết lại/xóa

| Trigger | Việc |
|---|---|
| `trg_visit_requests_primary_contact_guard_bi` / `_bu` | **Xóa** (guard `visitor_user_id` + `primary_contact_access_status`) |
| `trg_users_protect_active_primary_contact_bu` | **Viết lại** theo `visit_request_campuses.operational_contact_user_id` |
| `trg_visit_request_identity_changes_user_guard_bi` / `_bu` | **Viết lại** — bỏ ép role VISITOR (nội bộ được xác nhận) |
| `trg_identity_changes_transfer_bi` / `_bu` | Giữ, đổi `INITIAL_CLAIM` → `INITIAL_CONFIRMATION` |
| `trg_visit_campuses_aggregate_ai` / `_au` | **Viết lại** — bỏ `ASSIGNED`, thêm nhánh `PENDING_CONTACT_CONFIRMATION` |
| `trg_visit_campuses_assignment_validate_bi` / `_bu` | **Viết lại** — `ASSIGNED` → `BEFORE_VISIT`, guard gate |
| `trg_visit_requests_cancel_validate_bu` | **Viết lại** — bỏ nhánh `visitor_user_id`/`primary_contact_access_status` |
| `trg_visit_campuses_cancel_validate_bu` | Kiểm tra lại tham chiếu `visitor_user_id` |
| **Mới** | Guard fail-closed: request không vào approval state khi còn campus thiếu `operational_contact_user_id` |
| **Mới** | Guard: campus không approve/reject khi request còn `PENDING_CONTACT_CONFIRMATION` |

### 3.5 Procedure kiểm chứng

`sp_pems_contact_guard_tests()` (dòng 13345) và `sp_pems_assert_pure_v2_only()` (dòng 14937) — cả hai
đều assert mô hình primary-contact cũ, phải viết lại.

## 4. Bản đồ code bị ảnh hưởng

### 4.1 Backend — Domain

| File | Việc |
|---|---|
| `Entities/Delegations/VisitRequest.cs` | xóa `VisitorUserId`, `ContactPerson*`, `PrimaryContactAccessStatus`, `PrimaryContactVerifiedAt` |
| `Entities/Delegations/VisitRequestCampus.cs` | thêm `OperationalContactUserId` + navigation |
| `Entities/Delegations/VisitInstanceFormDetail.cs` | `OperationalContactEmail` → non-nullable; xóa `NoteToFptu` |
| `Entities/Delegations/VisitRequestIdentityChange.cs` | thêm `VisitInstanceId`, `TokenVersion`; đổi `ChangeKind`/bỏ `TargetRelation` |
| `Constants/VisitRequestConstants.cs` | +`PendingContactConfirmation`, +`WaitingContactConfirmation`, −`Assigned`, + error codes mới |

### 4.2 Backend — Application (67 file chạm `PrimaryContact`)

Cụm chính:
- `Common/DTOs/VisitFormV2Dtos.cs`, `VisitFormV2EditDtos.cs` — bỏ block primary contact
- `Commands/CreateVisitRequestV2/*` (7 file) + `InitiateVisitRequestV2`, `VerifyAndCreateVisitRequestV2`
- `Commands/VisitContactClaim/*` (6 file) → đổi tên/scope sang **operational contact per instance**
- `Commands/VisitContactTransfer/*` (6 file) → per instance
- `Commands/ApproveCampusInstance` (342 dòng), `RejectCampusInstance` (230 dòng) — thêm gate guard
- `Commands/CancelVisitRequest`, `UpdatePendingVisitRequestV2`, `ResubmitRejectedVisitRequestV2`, `VisitAmendments/*`
- `Services/VisitFormRead/VisitFormReadService.cs` (748 dòng) — scope theo instance
- `Services/VisitRequestAggregateStatusService.cs` — thêm `PENDING_CONTACT_CONFIRMATION`, bỏ `ASSIGNED`
- `Common/VisitInstanceAccess.cs` — **trả 1 relation duy nhất** → phải chuyển sang **capability union** (plan §2.1)

### 4.3 Backend — Infrastructure

`Services/VisitRequestV2CreateService.cs` (365), `VisitRequestV2EditService.cs` (762),
`VisitContactClaimService.cs`, `VisitContactClaimMaintenanceService.cs`, `VisitRequestV2Canonical.cs`.

### 4.4 Frontend (33 file `primaryContact` + 24 file `contactPoint` + 26 file `noteToFptu`)

`schema/visitRequestV2.schema.ts`, `utils/visitRequestV2Form.ts`, `hooks/useVisitRequestFormV2.ts`,
`components/v2/{VisitRequestFormV2,CampusVisitCard,VisitRequestV2DetailView,VisitRequestV2SubmittedSummary}.tsx`,
`components/ContactIdentityActions.tsx`, `api/visitRequestV2Api.ts`, i18n VI/EN, + toàn bộ test kèm theo.

### 4.5 Test

- `tests/PEMS.IntegrationTests/Database/ContactGuardTests.cs` — assert guard cũ
- `TestInfrastructure/{CanonicalSqlScript,CanonicalSqlHashTests,SchemaContractTests,DisposableDatabaseManager}.cs`
- `VisitRequests/*` (≈10 file), `Emails/*` (5 file dùng `contact_person`)
- `tests/PEMS.UnitTests/VisitRequests/*`, `Delegations/*`
- `frontend/pems-react/tests-realstack/*`, `tests/visit-request-single-form.spec.ts`

## 5. Quyết định thiết kế chốt trước khi code

1. **Token store**: KHÔNG thêm `token_hash` vào `visit_request_identity_changes` (plan §4.1). Token đã sống ở
   `email_action_tokens` (hashed, single-use, expiry, có sẵn dispatcher + invalidation helper). Thêm 2 token
   store là drift. Thay vào đó identity change nhận `token_version` để supersede + dedupe key.
   → Ghi nhận là **sai lệch có chủ ý** so với plan §4.1.
2. **`confirmation_method`**: giữ nguyên cột (GOOGLE_SSO/OTP_FALLBACK) — plan không yêu cầu xóa.
3. **`gate_revision`**: thêm cột `contact_gate_revision` trên `visit_requests` để làm dedupe key
   `APPROVAL_READY:{requestId}:{instanceId}:{gateRevision}` (plan §6.3).
4. **Capability union**: `VisitInstanceAccess.ResolveRelationAsync` trả 1 string → thay bằng record
   `VisitInstanceCapabilities` (cờ độc lập), giữ hàm cũ làm shim cho consumer chưa chuyển để không vỡ 20+ call site cùng lúc.

---

# Phase 2 — Kết quả (2026-08-05)

Chạy trên MySQL 8.0.46 thật, database dùng-một-lần `pems_cutover_probe`. `pems_db` KHÔNG bị
đụng: mọi câu lệnh chọn database được retarget trước, và văn bản sinh ra được quét lại — còn
một dòng non-comment nào nhắc `pems_db` là script tự từ chối chạy.

## Gate Phase 2

| # | Điều kiện | Kết quả |
|---|---|---|
| 1 | Fresh import canonical SQL | **PASS** (exit 0, không ERROR) |
| 2 | `sp_pems_contact_guard_tests` | **PASS** — 0 negative failure / 0 positive failure (11 NEG + 4 POS) |
| 3 | `sp_pems_assert_pure_v2_only` | **PASS** — không SIGNAL `PURE_V2_REFUSED*` |
| 4 | Đủ 11 seed scenario §4.4 | **PASS** — 11 request, 5 trạng thái aggregate, 1 đầu mối × 3 campus, cổng chặn leader-registrant, 3 trạng thái lời mời |
| 5 | Không còn legacy Primary contact | **PASS** |

Đối chiếu baseline (import chính file này từ `Canh-Iter1`):

| | Baseline | Sau cutover |
|---|---|---|
| base tables | 81 | 81 |
| triggers | 32 | **33** (−3 guard cũ, +4 guard mới) |
| `contact_guard_negative_failures` | 0 | 0 |
| check còn non-zero | `operational_visit_instances_missing_agenda_final`=3, `invalid_04_host_driven_email_wrong_sender`=1, `seed_placeholder_terms_remaining`≠0 | **2 cái đầu y hệt baseline; `seed_placeholder_terms_remaining` về 0** |

→ Không sinh check hỏng mới. Hai check còn đỏ là **nợ seed có sẵn trên `Canh-Iter1`**, không
phải hồi quy của đợt cutover này.

## Cách gate 5 phân biệt "còn legacy" với "assertion hợp lệ"

Quét không dựa vào đoán pattern. Ba loại nhắc tên còn lại được loại trừ **theo lý do**, không
theo hình dạng chuỗi:

- `DROP TRIGGER IF EXISTS` — cần giữ để import đè lên DB cũ xoá được trigger cũ;
- câu lệnh `information_schema.columns` — tồn tại chính là để **chứng minh cột đã biến mất**;
- comment.

Mọi lần xuất hiện khác (trong SELECT/INSERT/UPDATE/JOIN) đều làm gate đỏ.

## Việc đã làm ở Phase 2c–2e

**2c — seed:** 6 câu `INSERT INTO visit_requests` bỏ 7 cột đầu mối cấp request; 3 câu được bổ
sung `registrant_user_id` mang đúng user id mà `visitor_user_id` từng giữ (nếu không, chính các
lệnh huỷ trong seed sẽ vi phạm trigger huỷ mới); 32 dòng campus `ASSIGNED` → `BEFORE_VISIT`;
3 khối `INSERT … SELECT` form-detail chuyển nguồn đầu mối từ `vr.contact_person_*` sang
`vr.registrant_*` (trong seed hai bộ này **giống hệt nhau từng byte**, nên mỗi campus seed trở
thành REGISTRANT_SELF_MATCH — đúng ca auto-link §1.5, không lời mời, không email); 117 dòng
derived-table bỏ `note_to_fptu`.

Mọi phép cắt cột đều **theo vị trí sau khi parse tuple**, không regex trong lòng tuple, nên giá
trị chứa dấu phẩy hay ngoặc không thể bị hỏng. Mỗi phép thay có assert số lần khớp.

**Một khối bị cắt bỏ hẳn:** kịch bản `INITIAL_CLAIM` cấp request (mục "5) Seed the completed
INITIAL_CLAIM history"). Nó mã hoá máy trạng thái đầu mối-cấp-request; ánh xạ sang per-campus
mà không bịa dữ liệu là không làm được. Thay bằng ma trận §4.4.

**2d — verify + self-test:** viết lại `sp_pems_contact_guard_tests` thành 11 negative + 4
positive theo guard mới; thay khối "FINAL VERIFICATION QUERIES" bằng 11 check per-campus + 2
check schema-level (cột đầu mối cấp request và `note_to_fptu` phải vắng mặt); thêm assertion
cổng-xác-nhận hai chiều vào `sp_pems_assert_pure_v2_only`.

**2e — ma trận §4.4:** request 47001–47011, instance 47101–47121, identity change 67001–67009.

## Lưu ý mang sang Phase 3

- `CanonicalSqlScript.ExpectedBaseTableCount` đang pin **82** nhưng thực tế **81 ở cả baseline
  lẫn sau cutover** → sai lệch **có sẵn**, phải sửa ở Phase 3 cùng lúc với `ExpectedTriggerCount`
  32 → **33** và `ExpectedSha256`.
- Sai lệch có chủ ý so với plan §4.1: token vẫn ở `email_action_tokens`; identity change chỉ
  thêm `token_version`. Đã được chốt với chủ dự án.
