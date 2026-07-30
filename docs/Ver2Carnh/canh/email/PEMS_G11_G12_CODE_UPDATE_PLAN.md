# KẾ HOẠCH CẬP NHẬT LOGIC CODE PEMS
## G11-H/I/J — TO/CC/BCC, catalog email cố định, variable contract  
## G12 — Database contact guard closure

> Ngày lập: 2026-07-29  
> Trạng thái: Kế hoạch triển khai; chưa phải bằng chứng đã sửa xong  
> Phạm vi: Bổ sung vào kế hoạch G11 hiện có và tách G12 thành workstream độc lập  
> Nguyên tắc vận hành: Không commit, push, tạo PR, merge hoặc deploy nếu chưa có lệnh riêng của chủ dự án

---

## 1. Mục tiêu

Kế hoạch này chuyển toàn bộ yêu cầu trong file đính kèm thành các hạng mục code có thể triển khai, kiểm thử và nghiệm thu:

1. Sửa dứt điểm năm database trigger bảo vệ quan hệ đầu mối chính, đưa:
   - `contact_guard_negative_failures` từ `14` về `0`;
   - `contact_guard_positive_failures` giữ ở `0`.
2. Hoàn thiện TO/CC/BCC trong màn hình soạn, xem trước, lưu nháp, gửi và trả lời email.
3. Chuyển quản lý email template thành catalog hệ thống cố định:
   - chỉ xem và cập nhật nội dung;
   - không cho người dùng tạo, xóa, clone hoặc đổi `templateCode`.
4. Hợp nhất contract biến template để:
   - sidebar hiển thị đúng biến của đúng template;
   - template chuẩn mở ra không còn cảnh báo sai;
   - preview VI/EN và gửi thật dùng cùng quy tắc;
   - cấu hình sai thật sự vẫn bị phát hiện và chặn.
5. Kết nối các hạng mục mới với G11 hiện có:
   - `R-103`: persistent idempotency cho sáu report/invoice send action;
   - `R-106`: preview an toàn cho `{{actionBlock}}`;
   - `R-104`, `R-105`: vẫn chỉ chuẩn bị decision evidence, không tự ý implement.
6. Chỉ kết luận hệ thống sẵn sàng sau khi G11 và G12 đều đạt, full regression xanh và canonical SQL được kiểm chứng.

---

## 2. Các quyết định đã chốt

### 2.1. Email template là system catalog cố định

- Backend registry quyết định danh sách `templateCode`.
- Database lưu nội dung có thể hot-edit, không phải nơi cho người dùng tự phát minh mã template mới.
- Giao diện chỉ hỗ trợ xem, sửa nội dung và khôi phục mặc định.
- Không cho tạo, xóa, clone, đổi mã, đổi module, đổi phân loại bảo mật hoặc tự sửa contract biến.
- Số lượng hiện được nhắc đến là 30 nhưng phải xác nhận lại từ HEAD; không được hard-code con số 30 trước audit.

### 2.2. “Cho nhiều biến nhất có thể” phải an toàn

Một biến chỉ được đưa vào template khi caller thật có thể cung cấp dữ liệu đó trong send mode.

- Không lấy hợp của tất cả biến mà các caller “có thể có” nếu một caller khác không bảo đảm dữ liệu.
- `requiredVariables` phải được mọi caller reachable của template cung cấp.
- `optionalVariables` được phép bỏ khỏi nội dung; nếu được chèn vào nội dung thì caller vẫn phải cung cấp giá trị hoặc registry phải có fallback xác định, không nhạy cảm.
- Nếu các caller dùng chung một template nhưng có khả năng dữ liệu khác nhau, ưu tiên:
  1. bổ sung dữ liệu chung tại dispatcher/caller;
  2. chỉ khi nghiệp vụ thật sự khác mới đề xuất thêm một **system template code trong code release**, không mở chức năng tạo template tùy ý cho người dùng.

### 2.3. CC/BCC không áp dụng cho email nhạy cảm

- Email có OTP, token, link xác nhận, reset password hoặc action nhạy cảm không được có CC/BCC.
- Frontend chỉ hỗ trợ trải nghiệm; backend và `EmailRecipientPolicyEnforcer` là lớp quyết định cuối cùng.
- BCC phải có trong SMTP envelope nhưng không xuất hiện trong header mà TO/CC nhận được.

### 2.4. G11 và G12 không chạy đồng thời trên cùng working tree

Hai workstream có thể cùng ảnh hưởng canonical SQL/hash. Thứ tự khuyến nghị:

1. G12 — sửa contact guard và chốt hash/baseline mới.
2. G11-J — hợp nhất variable contract.
3. G11-I — khóa catalog ở chế độ update-only.
4. G11-H — hoàn thiện TO/CC/BCC.
5. Hợp nhất với `R-103` và `R-106`.
6. Full regression và final gate.

Không cho hai Agent cùng sửa canonical SQL hoặc cùng cập nhật `ExpectedSha256`.

---

## 3. Phát hiện quan trọng từ tài liệu hiện có

### 3.1. Xung đột UC-45

Tài liệu hiện tại còn ghi:

- UC-42: View Email Template List.
- UC-43: View Email Template Detail.
- UC-44: Update Email Template.
- UC-45: Create Email Template.

`PERMISSION_MATRIX.md` còn cấp UC-45 mức `F` cho HO. Quyết định update-only mới phải thay thế hành vi này.

Cách xử lý:

- Giữ số UC-45 trong lịch sử để không renumber hàng loạt.
- Đánh dấu `UC-45: DEPRECATED / NOT AVAILABLE — system template catalog is fixed`.
- Xóa route/menu/capability tạo template khỏi active product.
- Giữ UC-44 cho HO theo quyền `E`.
- Không tự mở quyền quản lý template cho Admin, Staff Leader hoặc role khác nếu permission source hiện hành không cho phép.

### 3.2. Schema đã có nền TO/CC/BCC

File SQL được cung cấp đã có:

- `sent_email_recipients.recipient_type ENUM('TO','CC','BCC')`;
- `email_draft_recipients.recipient_type ENUM('TO','CC','BCC')`.

Vì vậy phải audit và tái sử dụng schema hiện có trước khi nghĩ tới cột/bảng mới. Vấn đề trọng tâm có khả năng nằm ở API, policy, mapping, UI, reply behavior và MIME verification.

### 3.3. File SQL đính kèm không được dùng làm authority cho số template

File SQL đính kèm chỉ seed 16 template trong phần được kiểm tra, trong khi checkpoint dự án hiện nói catalog active là 30. Do đó:

- Không lấy file đính kèm làm nguồn canonical để quyết định số template.
- Phải audit registry, seed/sync script và database trên HEAD thật.
- Chỉ cập nhật hash của canonical SQL trong repository thật, không dùng hash của bản copy đính kèm.

### 3.4. Năm trigger contact guard đã xác định

1. `trg_visit_requests_primary_contact_guard_bi`
2. `trg_visit_requests_primary_contact_guard_bu`
3. `trg_users_protect_active_primary_contact_bu`
4. `trg_visit_request_identity_changes_user_guard_bi`
5. `trg_visit_request_identity_changes_user_guard_bu`

Self-test hiện mô tả 14 negative case và 7 positive case.

---

## 4. Traceability yêu cầu → workstream

| Requirement | Workstream | Thành phần chính | Gate |
|---|---|---|---|
| Sửa 5 contact guard trigger | G12 | Canonical SQL, migration, DB tests | 14 negative pass, 7 positive pass |
| Sai dữ liệu phải trả `SQLSTATE 45000` | G12 | Trigger + self-test | Actual SQLSTATE đúng từng case |
| NULL, user không tồn tại, role/status sai, UPDATE | G12 | Trigger logic + integration | Không đường bypass |
| TO/CC/BCC trong compose/preview | G11-H | DTO, handler, draft, FE chips | E2E + MIME |
| BCC không bị lộ | G11-H | SMTP/provider + response redaction | Raw MIME không có Bcc header |
| Sensitive email cấm CC/BCC | G11-H | Policy enforcer + FE capability | API và FE đều chặn |
| Chỉ update template cố định | G11-I | Controller/handler/FE/permission docs | Không tạo bản ghi/code mới |
| Khóa `templateCode` và metadata hệ thống | G11-I | Update whitelist | Direct API không đổi được |
| Khôi phục mẫu gốc | G11-I | Registry + restore command | Khôi phục đúng canonical |
| Biến sidebar đúng template | G11-J | Registry metadata API + FE | Không lẫn module |
| Mẫu chuẩn mở không báo sai | G11-J | Parser/validator/load state | 0 false warning |
| Preview VI/EN 30/30 | G11-J + R-106 | Renderer/sanitizer/sample data | 100% active catalog |
| `actionBlock` preview an toàn | R-106 | Action registry + renderer | Không token/link click thật |
| Send mode fail-closed | G11-J + R-106 | Runtime validation | Thiếu biến/action bị chặn |
| Canonical SQL/hash | G12 + G11 | SQL package + hash test | Fresh import và hash đúng |
| Không production-ready khi contact guard còn lỗi | Final gate | G10 readiness | G11 và G12 cùng đạt |

---

## 5. Kiến trúc đích

### 5.1. Nguồn sự thật

| Dữ liệu | Nguồn có thẩm quyền | Không được là nguồn có thẩm quyền |
|---|---|---|
| Danh sách `templateCode` | Backend system template registry | Dòng DB do người dùng tự tạo |
| Nội dung VI/EN hiện dùng | `email_templates` sau sync/hot-edit hợp lệ | Hard-code rải rác trong handler |
| Variable contract | Backend registry/contract dùng chung | `variables_text` do UI tự sửa |
| Preview sample | Contract backend | Dictionary hard-code theo màn hình FE |
| Security/sensitive/action metadata | Registry + policy code | Payload từ frontend |
| TO/CC/BCC cuối cùng | Backend authorization/policy/normalization | Danh sách frontend gửi lên mà không kiểm tra |
| Contact ownership invariant | Database trigger + application validation | Chỉ validation frontend |

### 5.2. Phân lớp

- API:
  - nhận request;
  - xác thực/authorize;
  - bind DTO;
  - trả stable error/validation issues;
  - không tự query và sửa logic catalog.
- Application:
  - điều phối template update/restore/preview;
  - điều phối recipient validation/policy;
  - thực hiện contract validation;
  - gọi dispatcher.
- Infrastructure:
  - persistence;
  - renderer/sanitizer;
  - SMTP/provider/file-sink;
  - migration/canonical SQL support.
- Frontend:
  - render capability do backend trả về;
  - chip editor TO/CC/BCC;
  - hiển thị field-level issues;
  - không tự quyết định security policy.

---

## 6. Giai đoạn 0 — Preflight và baseline

### 6.1. Repository integrity

Ghi lại trước khi sửa:

- branch hiện tại;
- `HEAD`;
- remote tracking status;
- `git status --short`;
- toàn bộ modified/untracked/deleted;
- `git diff --check`;
- `git stash list`;
- conflict-marker scan;
- hash canonical SQL hiện tại;
- giá trị `CanonicalSqlScript.ExpectedSha256`;
- số template code trong registry;
- số active template trong DB canonical;
- baseline test mới nhất.

Không:

- checkout/reset/rebase;
- apply/drop stash;
- sửa WIP ngoài phạm vi;
- commit/push/PR/merge/deploy;
- chạy migration trên `pems_db`;
- gửi email thật.

### 6.2. Database an toàn

- Tạo database disposable từ fresh canonical import.
- Guard rõ tên DB; từ chối chạy nếu tên là `pems_db`.
- Dùng user/test configuration riêng.
- File-sink/pickup directory thay SMTP thật.
- Ghi lại và drop database disposable sau kiểm thử.

### 6.3. Audit code bắt buộc

Lập inventory theo source thật:

1. `SystemEmailTemplates`, `EmailTemplateVariables`, `SensitiveEmailVariables`.
2. `EmailTemplateRenderer`, sanitizer, preview handler.
3. `EmailRecipientValidator`, `EmailRecipientPolicyEnforcer`.
4. `ISystemEmailDispatcher` và Prepare/Deliver boundary.
5. Các controller/command:
   - create/update/toggle/restore template;
   - compose/edit/send draft;
   - reply/reply-all;
   - preview.
6. Frontend:
   - `TemplateManagement`;
   - compose modal/page;
   - preview modal;
   - email detail/reply;
   - API clients, hooks, types và local draft/autosave.
7. Sáu report/invoice send action của R-103.
8. Tất cả caller của từng template active.

Deliverable audit:

| Template code | Caller | Reachable | Variables supplied | Sensitive | Action | CC/BCC allowed | Preview status |
|---|---|---:|---|---:|---|---:|---|

Không triển khai trước khi bảng này đủ cho toàn active catalog.

---

## 7. G12 — Database contact guard closure

### 7.1. Invariant bắt buộc

#### `visit_requests`

- `primary_contact_access_status = ACTIVE`:
  - `visitor_user_id` bắt buộc khác NULL;
  - user tồn tại;
  - role chính xác là `VISITOR`;
  - `users.status = ACTIVE`.
- `primary_contact_access_status = PENDING_CONFIRMATION`:
  - `visitor_user_id` bắt buộc NULL.
- Không áp quy tắc account role lên operational contact snapshot trong `visit_instance_form_details`.

#### `visit_request_identity_changes`

- `new_user_id` có thể NULL khi claim/transfer còn PENDING.
- Khi `new_user_id` khác NULL, user phải tồn tại và là ACTIVE VISITOR.
- INSERT và UPDATE phải cùng một contract.

#### `users`

User đang là primary contact ACTIVE của request chưa CANCELLED:

- không được chuyển role khỏi VISITOR;
- không được chuyển status khỏi ACTIVE.

### 7.2. Reproduce trước khi sửa

Fresh import canonical SQL trên DB disposable và lưu cho từng case:

| Test | Operation | Expected state | Expected message | Actual state | Actual message | Trigger fired |
|---|---|---|---|---|---|---|
| NEG-01..14 | Theo self-test | `45000` | Stable code | ... | ... | ... |
| POS-01..07 | Theo self-test | Không lỗi | Accepted | ... | ... | ... |

Phân loại nguyên nhân:

- trigger không được tạo;
- trigger order sai;
- điều kiện SQL có NULL semantics sai;
- trigger đúng nhưng fixture/self-test sai;
- một trigger khác chặn trước với message khác;
- case UPDATE dùng row seed đã không còn phù hợp;
- importer/test runner parse sai result;
- schema/canonical drift.

Không “sửa” bằng cách đổi expected message để khớp lỗi ngẫu nhiên.

### 7.3. Hardening logic

Kiểm tra và sửa:

- `SELECT COUNT(*)` phải phân biệt rõ 0, 1 và dữ liệu bất thường.
- So sánh role/status phải NULL-safe; không để `NULL <> 'VISITOR'` trở thành UNKNOWN rồi lọt qua `IF`.
- Role không tồn tại/inactive phải bị từ chối rõ.
- User không tồn tại phải trả `PRIMARY_CONTACT_USER_NOT_FOUND`.
- Wrong role phải trả `PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR`.
- Inactive visitor phải trả `PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE`.
- State mismatch trả đúng:
  - `ACTIVE_PRIMARY_CONTACT_REQUIRES_VISITOR_USER`;
  - `PENDING_PRIMARY_CONTACT_MUST_NOT_HAVE_VISITOR_USER`.
- User protection phải kiểm tra role lookup bị NULL/not found.
- `FOLLOWS` phải trỏ tới trigger có thật và thứ tự lỗi phải có chủ đích.
- INSERT và UPDATE không được có logic lệch nhau.

Không tạo stored helper chỉ để giảm dòng code nếu MySQL trigger restrictions, determinism hoặc migration convention không phù hợp.

### 7.4. Bộ test bắt buộc

Negative:

1. ADMIN làm `visitor_user_id`.
2. HO làm `visitor_user_id`.
3. STAFF + LEADER làm `visitor_user_id`.
4. STAFF + STAFF làm `visitor_user_id`.
5. DEPARTMENT + LEADER làm `visitor_user_id`.
6. DEPARTMENT + STAFF làm `visitor_user_id`.
7. STUDENT làm `visitor_user_id`.
8. INACTIVE VISITOR làm `visitor_user_id`.
9. ACTIVE nhưng `visitor_user_id = NULL`.
10. PENDING nhưng `visitor_user_id` đã có.
11. Đổi linked VISITOR sang STAFF.
12. Deactivate linked VISITOR.
13. INSERT APPLIED identity change với internal user.
14. UPDATE pending identity change sang internal user.
15. Bổ sung user id không tồn tại.
16. Bổ sung role id/status bất hợp lệ nếu FK/fixture cho phép kiểm tra đúng tầng.
17. Kiểm tra đường UPDATE riêng cho `visitor_user_id`.

Positive:

1. INSERT request với ACTIVE VISITOR.
2. PENDING + NULL visitor.
3. TRANSFER PENDING giữ owner cũ.
4. TRANSFER APPLIED đổi sang ACTIVE VISITOR mới.
5. Staff registrant dùng primary contact VISITOR khác.
6. Staff Leader registrant dùng primary contact VISITOR khác.
7. Operational contact được phép dùng email Staff như snapshot.
8. Cancelled request không khóa đổi role/status nếu đúng owner decision hiện hành.

### 7.5. SQL package

Nếu repository chưa có package phù hợp, tạo theo convention:

```text
docs/database/scripts/contact_guard_closure/
├── 01_preflight.sql
├── 02_up_replace_triggers.sql
├── 03_verify.sql
└── 04_rollback_guidance.md
```

Yêu cầu:

- guard DB name;
- drop/create đúng năm trigger, không đụng trigger khác;
- idempotent ở mức hợp lý;
- chạy lần hai không drift;
- verify trigger body/order;
- rollback guidance không làm mất audit/data;
- không sửa seed ngoài phạm vi.

### 7.6. G12 gate

Chỉ đạt khi:

```text
contact_guard_negative_failures = 0
contact_guard_positive_failures = 0
```

Đồng thời:

- mọi negative case sai trả SQLSTATE `45000`;
- positive case không bị chặn oan;
- fresh import xanh;
- migration lần một và lần hai xanh;
- application integration test cho `visitor_user_id` xanh;
- canonical hash được cập nhật có chủ đích;
- không có thay đổi Gallery/template seed ngoài phạm vi G12.

---

## 8. G11-J — Một variable contract duy nhất

### 8.1. Nguyên nhân phải điều tra

Chuỗi hiện có khả năng đang lệch:

```text
Template DB
→ backend registry
→ API allowed variables
→ frontend sidebar
→ preview sample dictionary
→ runtime caller variables
→ renderer
```

Ngoài sai mapping theo template, phải kiểm tra thêm lỗi frontend state:

- validate khi metadata chưa load xong;
- giữ biến của template trước khi chuyển template;
- cache không keyed theo `templateCode`/version;
- fallback logistics bị dùng cho account template;
- sai casing như `FullName` và `fullName`;
- parser subject/body dùng quy tắc khác nhau.

### 8.2. Contract đề xuất

Mỗi system template definition có:

```text
templateCode
allowedVariables
requiredVariables
optionalVariables
sensitiveVariables
forbiddenInSubject
previewSampleValuesVi
previewSampleValuesEn
actionBlockRequirement
securityClassification
allowCc
allowBcc
reachableCallers
defaultSubjectVi
defaultSubjectEn
defaultBodyVi
defaultBodyEn
```

`variables_text` trong DB, nếu còn giữ, chỉ là projection/cache được sync từ registry; người dùng không được tự sửa và runtime không dùng nó làm authority.

### 8.3. Variable rules

- Variable name phải dùng canonical casing từ registry.
- Không tự động coi hai biến khác casing là cùng biến nếu việc đó che lỗi cấu hình.
- Trước khi siết case-sensitive, phải audit và migrate toàn canonical content/caller dictionary cùng một lượt.
- Placeholder hợp lệ phải theo parser duy nhất cho subject, body, preview và send.
- Unknown variable: chặn lưu.
- Malformed placeholder: chặn lưu.
- Required variable bị xóa: chặn lưu.
- Optional variable bị xóa khỏi nội dung: cho phép.
- Placeholder được dùng nhưng runtime không có value: send fail-closed.
- Sensitive variable trong subject: chặn bằng policy hiện có.
- `actionBlock` do renderer/action registry tạo; frontend không truyền HTML/action URL tùy ý.

### 8.4. API metadata

Template detail API phải trả:

- nội dung VI/EN;
- immutable metadata;
- editable fields;
- variable contract;
- localized labels/descriptions;
- preview capability;
- CC/BCC capability;
- row version/concurrency token nếu hệ thống có.

Frontend không giữ danh sách biến hard-code theo module.

### 8.5. Validation issue model

Trả issue có cấu trúc:

```text
field: subjectVi | subjectEn | bodyVi | bodyEn
code
variableName
messageVi
messageEn
severity
```

Stable codes tối thiểu, ưu tiên tái sử dụng code có sẵn:

- `EMAIL_TEMPLATE_VARIABLE_UNKNOWN`
- `EMAIL_TEMPLATE_VARIABLE_MALFORMED`
- `EMAIL_TEMPLATE_REQUIRED_VARIABLE_MISSING`
- `EMAIL_TEMPLATE_RUNTIME_VARIABLE_MISSING`
- `EMAIL_TEMPLATE_SUBJECT_FORBIDDEN_SENSITIVE_VARIABLE`
- `EMAIL_TEMPLATE_ACTION_BLOCK_REQUIRED`

Không chỉ trả một câu chung “Một số biến chưa được định nghĩa hoặc sai định dạng”.

### 8.6. Preview

- Preview dùng chính renderer và sanitizer của send mode.
- Sample data lấy từ backend contract.
- Không sinh OTP/token/action URL thật.
- `actionBlock` hiển thị dạng disabled, không click.
- Không có `javascript:`, event handler hoặc fake production link.
- Preview fail rõ khi template DB thật sự sai.
- Không bỏ qua unresolved placeholder để làm UI “xanh”.

### 8.7. ACCOUNT_EMAIL_CONFIRMATION

Audit riêng:

- `fullName`
- `roleName`
- `campusName`
- `actionBlock`
- `expiresInHours`

Phải chứng minh:

- tên/casing khớp registry;
- sidebar hiển thị đúng;
- preview có sample an toàn;
- caller thật cung cấp đủ;
- subject không chứa biến nhạy cảm/action block;
- body dùng action block đúng policy.

### 8.8. Frontend load state

- `loading`: chưa validate, hiển thị skeleton/loading.
- `ready`: chỉ validate bằng contract của đúng template.
- `error`: không dùng contract stale/fallback của template khác.
- Khi đổi template:
  - hủy/ignore response cũ;
  - reset issues cũ;
  - cache keyed theo code và version.
- Canonical template hợp lệ mở ra không có warning.
- Template DB đã bị chỉnh sai từ trước vẫn phải hiển thị warning thật.

### 8.9. G11-J gate

- Toàn active catalog mở lần đầu: 0 false warning.
- Sidebar: 0 biến sai module.
- Preview VI: 100%.
- Preview EN: 100%.
- Canonical content: 0 unknown/malformed/missing-required issue.
- Cố ý thêm biến sai: chặn lưu.
- Cố ý xóa required/action block: chặn lưu.
- Send mode thiếu runtime value: fail-closed.
- Hot-edit DB có hiệu lực không restart.

---

## 9. G11-I — Catalog cố định, chỉ update

### 9.1. Backend registry/DB reconciliation

Tạo verify:

```text
registryCodes == databaseSystemTemplateCodes
count(registryCodes) == count(distinct database template_code)
no unknown database code
no missing database code
```

Không tự xóa row lịch sử. Nếu DB có code thừa:

- phân loại referenced/unreferenced;
- không xóa nếu đã được `sent_emails`/draft/history tham chiếu;
- tách inactive historical record khỏi active system catalog theo migration plan an toàn.

### 9.2. Update whitelist

Cho phép HO có UC-44 sửa đúng các trường nội dung:

- `name` nếu đây là display name;
- `description` nếu đây là mô tả quản trị;
- `subjectVi`;
- `subjectEn`;
- `bodyVi`;
- `bodyEn`.

Khóa:

- `templateCode`;
- `purpose/module`;
- `campusId` nếu system catalog là global;
- `bodyFormat` nếu caller/renderer không hỗ trợ đổi;
- `status`;
- sensitive/security classification;
- action type;
- variable contract;
- created metadata.

Trường nào chưa chắc chắn phải được audit trước; không cho mass assignment từ request DTO sang entity.

### 9.3. Create/delete/clone/status

- Gỡ nút “Thêm mẫu mới”.
- Không có Delete/Clone.
- Đóng create/delete API ở backend, không chỉ ẩn frontend.
- Khuyến nghị giữ route legacy trong một giai đoạn và trả stable business error:
  - `EMAIL_TEMPLATE_CATALOG_FIXED`.
- Direct API không tạo được dòng thứ N+1.
- `ToggleEmailTemplateStatus` phải bị khóa nếu caller không có fallback an toàn.
- Sync script chỉ đồng bộ system registry, không phải user-facing create API.

### 9.4. Restore default

- Default lấy từ registry/versioned canonical definition.
- Restore chạy lại cùng validation pipeline như update.
- Restore không đổi `templateCode` hoặc metadata hệ thống.
- Có audit log: actor, template code, timestamp, before/after hash; không lưu secret.

### 9.5. Optimistic concurrency

Audit `updated_at`, row version hoặc ETag:

- hai HO mở cùng template không được silently overwrite;
- update stale trả stable concurrency error;
- UI cho reload/merge thủ công;
- không dùng last-write-wins nếu architecture hiện có đã hỗ trợ concurrency.

### 9.6. Documentation alignment

Cập nhật:

- `USE_CASE_LIST.md`: UC-45 deprecated/not available.
- `PERMISSION_MATRIX.md`: bỏ quyền active UC-45; giữ UC-44 cho HO.
- `PERMISSION_RULES.md`: catalog fixed/update-only.
- UI/help text: “Quản lý mẫu hệ thống”, không gọi là “tạo mẫu”.

Không renumber UC khác.

### 9.7. G11-I gate

- UI không còn Add/Delete/Clone/Toggle trái quyết định.
- API create/delete không thể mutate.
- Update không đổi code/count/code set.
- Không tạo dòng thứ N+1.
- Hot-edit nội dung hoạt động.
- Restore default đúng.
- Concurrent update không overwrite im lặng.
- Audit log đầy đủ.

---

## 10. G11-H — TO/CC/BCC trong compose, preview và reply

### 10.1. Inventory entry point

Quét toàn frontend và backend cho:

- compose mới;
- compose từ visit/logistics/report;
- preview trước gửi;
- draft/autosave;
- send draft;
- reply;
- reply all;
- resend/retry;
- report/invoice send UI;
- các route API-only.

Mỗi entry point phải được đánh dấu:

| Entry point | TO editable | CC | BCC | Sensitive | Backend-resolved recipients | Reply support |
|---|---:|---:|---:|---:|---:|---:|

### 10.2. Recipient contract

DTO dùng danh sách typed recipient:

```text
email
displayName?
recipientType: TO | CC | BCC
```

Backend:

- trim;
- validate từng email;
- so sánh case-insensitive;
- deduplicate trong cùng loại;
- từ chối email xuất hiện ở nhiều loại để tránh tự đổi semantics TO/CC/BCC;
- yêu cầu ít nhất một TO;
- áp giới hạn recipient theo convention hiện có, không tự bịa giới hạn;
- không tin display name/recipient list khi route phải resolve theo resource scope.

Stable errors, ưu tiên reuse:

- `EMAIL_RECIPIENT_REQUIRED`
- `EMAIL_RECIPIENT_INVALID`
- `EMAIL_RECIPIENT_DUPLICATE`
- `EMAIL_CC_BCC_NOT_ALLOWED`
- `EMAIL_RECIPIENT_FORBIDDEN`

### 10.3. Capability

Backend trả:

```text
toMode: EDITABLE | BACKEND_RESOLVED_LOCKED
allowCc
allowBcc
reasonCode
```

Frontend không tự suy sensitive chỉ từ tên template.

### 10.4. Draft/autosave

- Lưu TO/CC/BCC trong `email_draft_recipients`.
- Update draft phải replace/diff recipients trong transaction phù hợp.
- Draft owner scope giữ nguyên.
- Autosave không tạo recipient duplicate.
- Preview đọc đúng snapshot draft hiện tại.
- Send draft phải re-authorize và revalidate, không tin vì draft từng hợp lệ.

### 10.5. Preview summary

Người đang soạn hợp lệ thấy:

- TO;
- CC;
- BCC;
- subject;
- body;
- attachment.

Redaction:

- sender/owner có thể thấy BCC của email mình;
- TO/CC không được thấy BCC;
- BCC participant không được thấy các BCC khác;
- viewer trái scope không được thấy email.

### 10.6. Reply và Reply All

Reply:

- đích mặc định là sender hợp lệ;
- không carry BCC cũ.

Reply All:

- chỉ thực hiện khi người dùng chọn;
- lấy sender + TO + CC hợp lệ;
- loại current user;
- không lấy BCC cũ;
- deduplicate;
- re-authorize resource/email scope;
- không tin danh sách frontend tự mở rộng.

### 10.7. MIME/provider

- TO/CC có header đúng.
- BCC chỉ tham gia envelope.
- Raw `.eml`/file-sink không chứa Bcc header hoặc danh sách BCC trong body.
- Không ghi `DELIVERED` nếu provider contract chỉ xác nhận accepted/sent.
- History rows lưu typed recipients để audit nhưng API phải redact theo viewer.

### 10.8. Sensitive policy

Email có:

- OTP;
- account confirmation;
- password reset;
- contact claim/transfer token;
- invitation/action token;
- bất kỳ template `CarriesSecret` hoặc `HasSensitiveAction`

thì:

- `allowCc = false`;
- `allowBcc = false`;
- API nhận CC/BCC phải trả stable error;
- frontend ẩn/disable có giải thích;
- không có đường legacy bypass `EmailRecipientPolicyEnforcer`.

### 10.9. Frontend UX

- Ba vùng TO/CC/BCC rõ ràng.
- Multi-recipient chip/list.
- Paste nhiều địa chỉ theo format đang hỗ trợ.
- Field-level error chỉ đúng chip lỗi.
- Disable send khi invalid/pending.
- Loading/submit state chống double-click.
- Keyboard/accessibility:
  - focus order;
  - label;
  - remove chip bằng phím;
  - error announcement;
  - contrast đúng design system.

### 10.10. G11-H gate

- Manual/non-sensitive email gửi TO/CC/BCC đúng.
- Sensitive email bị chặn ở FE và API.
- Duplicate cross-list bị chặn.
- Draft/autosave/reopen giữ đúng recipient.
- Reply/Reply All không tái dùng BCC.
- BCC không lộ trong MIME hoặc response trái quyền.
- Authorization file attachment và email scope không regression.

---

## 11. Kết nối với R-103 và R-106

### 11.1. R-103 idempotency

Không mở rộng idempotency mù quáng ra toàn email module.

- Sáu report/invoice send action vẫn theo persistent idempotency contract đã lập.
- Fingerprint phải bao gồm recipient backend đã resolve và attachment source identity.
- Nếu TO/CC/BCC thay đổi thì fingerprint thay đổi.
- Same key + different recipients phải trả:
  - `IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST`.
- Replay success không tạo lại:
  - PDF;
  - attachment row;
  - sent-email row;
  - recipient rows;
  - MIME.
- `OUTCOME_UNKNOWN` không tự resend.

Manual compose/reply chỉ được đưa vào persistent idempotency nếu audit chứng minh scope hiện tại đã yêu cầu; không tự mở rộng G11 core.

### 11.2. R-106 action-block preview

Variable contract mới phải là nền cho R-106:

- Registry xác định template nào cần `actionBlock`.
- Preview tạo disabled action block.
- Send mode yêu cầu action data thật và fail-closed.
- Preview không sinh token/URL thật.
- Preview và send dùng cùng renderer/sanitizer.
- Sensitive history policy giữ nguyên.

---

## 12. Kế hoạch test

### 12.1. Unit test backend

#### Variable/template

- Registry code uniqueness.
- DB code set reconciliation.
- Unknown/malformed/missing-required.
- Optional variable removal allowed.
- Runtime value missing fails.
- Sensitive variable forbidden in subject.
- Action block preview/send separation.
- Update whitelist.
- Code/security/action/status immutability.
- Restore default.
- Concurrency conflict.

#### Recipient

- Normalize/trim.
- Invalid email.
- Duplicate within list.
- Duplicate across TO/CC/BCC.
- Missing TO.
- Sensitive blocks CC/BCC.
- Backend-resolved TO cannot be overridden.
- Reply and Reply All semantics.
- BCC redaction.

### 12.2. Integration test backend + MySQL

- Năm trigger, INSERT/UPDATE.
- 14 negative + 7 positive + supplemental cases.
- Create template direct API blocked.
- Delete/clone/status direct API blocked.
- Update cannot mutate immutable fields.
- Update keeps count/code set.
- Hot-edit send uses new DB content without restart.
- Restore default.
- Draft recipients persisted.
- Send creates correct typed recipient rows.
- BCC history/redaction.
- Sensitive API bypass rejected.
- `FileDownloadAuthorizationTests` vẫn xanh.

### 12.3. Frontend tests

- Template page không có “Thêm mẫu mới”.
- Không có delete/clone/toggle trái rule.
- Sidebar changes with selected template.
- No stale logistics variables on account template.
- No validation before contract ready.
- Canonical template opens without warning.
- Genuine invalid edit shows structured issue.
- TO/CC/BCC chips.
- Duplicate and invalid handling.
- Sensitive capabilities hide/disable CC/BCC.
- Draft reload retains recipients.
- Reply does not carry BCC.
- Reply All excludes old BCC/current user.
- Preview summary and accessibility.

### 12.4. Catalog matrix

Với mỗi active template:

| Code | Open clean | VI preview | EN preview | Caller variables | Sensitive policy | Action preview | Hot-edit |
|---|---:|---:|---:|---:|---:|---:|---:|

Không dùng một test loop chỉ assert HTTP 200; phải assert:

- không unresolved placeholder;
- không token thật;
- không link giả clickable;
- không unsafe HTML/script/event;
- output đúng template/language;
- send mode thiếu action vẫn bị chặn.

### 12.5. Real-stack

Môi trường:

- backend thật;
- frontend thật;
- disposable MySQL;
- file-sink/pickup;
- không SMTP thật.

Journeys:

1. HO mở toàn bộ template, không false warning.
2. Preview toàn catalog VI/EN.
3. Sửa template hợp lệ, save, preview/send thấy nội dung mới không restart.
4. Thêm biến sai, save bị chặn với field/code rõ.
5. Xóa required/action block, save bị chặn.
6. Direct API create/delete template bị chặn.
7. Manual compose có TO + CC + BCC; DB và MIME đúng.
8. Sensitive template cố gửi CC/BCC; UI và API cùng chặn.
9. Reply và Reply All không carry BCC.
10. Viewer TO/CC không thấy BCC.
11. Contact guard 14 negative/7 positive xanh.
12. R-103 retry/concurrency không tạo MIME/row trùng.

---

## 13. Full regression và baseline

Chạy không filter:

1. Backend solution build non-incremental.
2. Toàn bộ backend unit.
3. Architecture tests.
4. Toàn bộ integration.
5. Frontend tests theo config mặc định.
6. `tsc --noEmit`.
7. Vite production build.
8. Fresh canonical SQL import.
9. G12 migration lần một và lần hai.
10. G11 schema/migration lần một và lần hai nếu có.
11. Email-template sync lần một và lần hai.
12. File-sink/fake-SMTP.
13. Real-stack journeys.
14. Preview toàn active catalog VI/EN.
15. Attachment authorization regression.

Baseline từ prompt G11 hiện có:

```text
Unit >= 1730
Architecture >= 14
Integration >= 1020
Frontend >= 891
Frontend files >= 68
Backend warnings <= 208
```

Quy tắc:

- Preflight phải xác nhận lại baseline HEAD.
- Nếu HEAD đã cao hơn, baseline mới là số cao hơn.
- Không giảm test, xóa test, skip, nới assertion hoặc tăng timeout để ép xanh.
- Báo cả failed/skipped/duration/warnings/delta.
- Liệt kê test mới, test sửa, assertion thay đổi và lần chạy đỏ.

---

## 14. Static scan

Quét tracked và untracked:

```text
CreateEmailTemplate
DeleteEmailTemplate
CloneEmailTemplate
ToggleEmailTemplateStatus
templateCode
variables_text
allowedVariables
requiredVariables
optionalVariables
previewSample
actionBlock
otpCode
EmailRecipientPolicyEnforcer
recipient_type
TO
CC
BCC
ReplyAll
ISystemEmailDispatcher
Idempotency-Key
OUTCOME_UNKNOWN
SendAsync
TrySendAsync
body_snapshot
DELIVERED
visitor_user_id
PRIMARY_CONTACT
SQLSTATE
contact_guard_negative_failures
contact_guard_positive_failures
dangerouslySetInnerHTML
innerHTML
javascript:
onerror
FileId
attachment
```

Phân loại mỗi hit:

- production-safe;
- test fixture;
- historical snapshot;
- accepted debt;
- violation đã sửa;
- blocking violation.

---

## 15. Database và canonical hash

### 15.1. G12

- Hash trước G12.
- Chỉ sửa năm trigger/self-test/migration cần thiết.
- Fresh import.
- Migration idempotency.
- Hash sau G12.
- Update `ExpectedSha256`.
- Chứng minh template/Gallery/seed ngoài phạm vi không đổi.

### 15.2. G11-H/I/J

- Không thêm schema nếu recipient tables và template columns hiện có đủ.
- Nếu cần schema thật sự:
  - additive;
  - migration package;
  - fresh import;
  - run twice;
  - update hash riêng có giải thích.
- `variables_text` không được trở thành user-editable authority.
- Template count/code set không thay đổi trong G11-I/J.

### 15.3. Hash sequencing

Mỗi workstream cập nhật hash sau khi hoàn tất, không để hai thay đổi cùng ghi đè một expected hash cũ.

---

## 16. Tài liệu cần cập nhật

Email standardization:

```text
docs/email-standardization/04-requirement-test-traceability.md
docs/email-standardization/05-final-verification-report.md
docs/email-standardization/07-g11-residual-technical-closure.md
docs/email-standardization/08-open-product-decisions.md
```

Database:

```text
docs/database/scripts/contact_guard_closure/*
```

Business/permission:

- `USE_CASE_LIST.md`
- `PERMISSION_MATRIX.md`
- `PERMISSION_RULES.md`

Nội dung bắt buộc:

- UC-45 deprecated/not available;
- UC-44 update-only cho HO;
- catalog fixed;
- variable contract authority;
- recipient privacy;
- G12 test matrix;
- canonical hash trước/sau;
- residual risk;
- R-103/R-106 status;
- G10 readiness.

---

## 17. Repository integrity cuối lượt

Chạy và báo:

```text
git status
git diff --check
git rev-parse HEAD
git stash list
conflict-marker scan
canonical SQL SHA-256
artifact scan
credential/token scan
```

Xác nhận:

- HEAD không đổi nếu không được phép commit.
- Stash không đổi.
- Không deletion ngoài dự kiến.
- Không `.eml`, `.pdf`, `.trx`, coverage, `TestResults`, `dist`, log hoặc credential test lọt vào WIP.
- Disposable DB đã drop.
- `pems_db` chưa bị ghi.
- Không email thật.
- Không commit/push/PR/merge/deploy.

---

## 18. Gate cuối

### 18.1. G12

```text
R-DB-CONTACT-GUARD: CLOSED / NOT CLOSED
G12: ĐẠT / CHƯA ĐẠT
```

ĐẠT khi:

- 5 trigger đúng;
- negative failures = 0;
- positive failures = 0;
- integration/fresh import/migration/hash xanh.

### 18.2. G11 bổ sung

```text
G11-H TO/CC/BCC: ĐẠT / CHƯA ĐẠT
G11-I Fixed catalog: ĐẠT / CHƯA ĐẠT
G11-J Variable contract: ĐẠT / CHƯA ĐẠT
```

Không được ĐẠT nếu:

- compose nghiệp vụ cho phép nhưng thiếu TO/CC/BCC;
- sensitive email có thể thêm CC/BCC;
- BCC lộ trong MIME/UI/API trái quyền;
- UI hoặc API còn tạo/xóa/clone system template;
- `templateCode` hoặc count/code set bị đổi;
- canonical template mở còn false warning;
- sidebar lẫn biến module khác;
- preview và send không dùng cùng contract/renderer;
- active catalog chưa preview đủ VI/EN;
- R-103 còn duplicate outbound attempt.

### 18.3. Tổng kết

```text
R-103: CLOSED / NOT CLOSED
R-104: BLOCKED — awaiting owner role/UC/metric decision
R-105: BLOCKED — awaiting owner UX decision
R-106: CLOSED / NOT CLOSED
G11: ĐẠT / CHƯA ĐẠT
G12: ĐẠT / CHƯA ĐẠT
G10 readiness: READY / NOT READY
G10 execution: NOT STARTED
```

G10 readiness chỉ `READY` khi G11 và G12 đều đạt. Không được dùng việc G11 xanh để che `contact_guard_negative_failures = 14`.

---

## 19. Cấu trúc báo cáo thực thi

1. Preflight và repository integrity.
2. Audit active template/caller/variable matrix.
3. Audit compose/preview/reply entry point.
4. G12 root cause từng negative case.
5. Năm trigger đã sửa.
6. SQL migration/fresh import/idempotency/hash.
7. Variable contract implementation.
8. Catalog update-only implementation.
9. TO/CC/BCC implementation.
10. MIME/BCC privacy evidence.
11. R-103 integration evidence.
12. R-106 100% VI/EN preview matrix.
13. Focused tests.
14. Real-stack evidence.
15. Full regression.
16. Static scan.
17. Test accounting.
18. File thay đổi.
19. Documentation changes.
20. Residual risks.
21. R-103/R-104/R-105/R-106 status.
22. G11 status.
23. G12 status.
24. G10 readiness.
25. `G10 execution: NOT STARTED`.

---

## 20. Định nghĩa hoàn thành

Công việc chỉ hoàn thành khi người kiểm tra độc lập có thể chứng minh:

1. Sai primary-contact relation bị database chặn bằng SQLSTATE 45000.
2. Quan hệ hợp lệ không bị chặn oan.
3. UI và API chỉ cho cập nhật system template đã đăng ký.
4. Template count/code set không đổi do thao tác quản trị.
5. Sidebar, save validation, preview và send dùng cùng variable contract.
6. Template canonical mở sạch; cấu hình sai thật mới báo lỗi.
7. Toàn active catalog preview được ở VI và EN.
8. `actionBlock` preview an toàn nhưng send vẫn fail-closed.
9. TO/CC/BCC hoạt động end-to-end cho email được phép.
10. Sensitive email không thể có CC/BCC.
11. BCC không lộ.
12. Idempotent report/invoice send không tạo outbound attempt trùng.
13. Full regression và database verification xanh.
14. Không ghi vào production DB, không gửi email thật và không thực hiện Git/deployment ngoài quyền.

