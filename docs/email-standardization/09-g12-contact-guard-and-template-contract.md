# 09 — G12 Contact Guard Closure + G11-H/I/J Template Contract

> Ngày: 2026-07-30 · Nhánh `Canh-Iter1` · HEAD `c39e6f0404978a5a05b0c52681e01c8837fc4b29` (chưa commit)
> Tiếp nối `07-g11-residual-technical-closure.md`. Không thay thế nó.

---

## 1. Kết luận ngắn

```text
R-DB-CONTACT-GUARD: CLOSED
G12:                ĐẠT   (contact_guard_negative_failures = 0, positive = 0)
G11-H TO/CC/BCC:    ĐẠT
G11-I Fixed catalog: ĐẠT
G11-J Variable contract: ĐẠT
R-103 / R-106:      không regression
```

Canonical SQL SHA-256: `b8213ee5…57c5a0` → **`edf88cbd0cab31bf24b3907dda184d7952fb18f2f149af7513eab4116d0bf29c`**

---

## 2. G12 — điều thực sự sai không phải cái trigger

Báo cáo trước ghi `contact_guard_negative_failures = 14`, tức **toàn bộ** case âm thất bại. Đọc như
vậy thì database đang mở toang: gán ADMIN làm đầu mối chính cũng qua, vô hiệu hóa VISITOR đang liên kết
cũng qua.

Thực tế ngược lại. Năm trigger **đã chặn đúng cả 14 case ngay từ đầu**. Cái sai nằm ở dụng cụ đo.

Mỗi handler trong self-test viết:

```sql
DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
BEGIN
  SET v_raised = TRUE;                                    -- ← câu này
  GET DIAGNOSTICS CONDITION 1 v_sqlstate = RETURNED_SQLSTATE, v_message = MESSAGE_TEXT;
END;
```

MySQL **xóa diagnostics area khi câu lệnh đầu tiên trong handler chạy thành công**. `SET` thành công →
vùng diagnostics bị xóa → `GET DIAGNOSTICS CONDITION 1` đọc về `NULL` cho cả SQLSTATE và MESSAGE_TEXT.
Phép so `v_sqlstate = '45000'` trở thành `NULL` (UNKNOWN), `IF` coi là false, và cột báo cáo in
`COALESCE(v_message,'Operation unexpectedly succeeded')` — **đúng ngược sự thật**.

Bằng chứng đối chứng, cùng database, gọi trực tiếp:

```text
mysql> UPDATE visit_requests SET visitor_user_id = 2, primary_contact_access_status='ACTIVE'
       WHERE visit_request_id = 1001;
ERROR 1644 (45000): PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR
```

Đảo hai câu lệnh trong cả 21 handler → `0` / `0`, **không sửa một dòng trigger nào**.

> Bài học đáng giữ: một self-test có thể sai theo hướng *báo lỗi ở chỗ lành*. Nó nguy hiểm không kém
> hướng ngược lại, vì nó dạy người đọc rằng cảnh báo không có ý nghĩa gì.

### 2.1. Ba lỗi thật trong trigger (tìm bằng probe, không bằng đọc)

| # | Lỗi | Hệ quả trước khi sửa | Trạng thái |
|---|---|---|---|
| 1 | `v_user_status VARCHAR(20)` | `users.status` là ENUM có `PENDING_EMAIL_CONFIRMATION` — **26 ký tự**. `SELECT … INTO` raise `22001 Data too long` **từ trong trigger**. Ghi vẫn bị từ chối, nhưng bằng lỗi lưu trữ thay cho `PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE`. Mọi tài khoản mới đều đi qua trạng thái này → **có thật, không phải giả định** | `VARCHAR(30)` |
| 2 | `JOIN roles` (inner) | User tồn tại nhưng không đọc được role → `COUNT(*)` sụp về 0 → báo `PRIMARY_CONTACT_USER_NOT_FOUND`, **sai sự thật** và chỉ người debug đi lạc | `LEFT JOIN` + `<> 1` |
| 3 | `v_new_role_code <> 'VISITOR'` sau `SELECT … INTO` không có hàng | Biến còn `NULL`; `NULL <> 'VISITOR'` là UNKNOWN, `IF` coi là false → **guard ngừng guard trên nhánh đó** | `COUNT` + `<=>` NULL-safe |

Probe H2a trước khi sửa:

```text
H2a PENDING_EMAIL_CONFIRMATION visitor | BLOCK | raised=TRUE | 22001 | Data too long for column 'v_user_status'
```

sau khi sửa:

```text
NEG-16 | 45000 | PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE | PASS
```

### 2.2. Bộ self-test: 21 → 26 case

Thêm 5 case cho các đường không case nào trong 21 cũ đi qua:

| Case | Nội dung | Kết quả mong đợi |
|---|---|---|
| NEG-15 | `visitor_user_id` không tồn tại | `PRIMARY_CONTACT_USER_NOT_FOUND` (trigger trả lời **trước** khóa ngoại) |
| NEG-16 | VISITOR đang `PENDING_EMAIL_CONFIRMATION` | `PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE` |
| NEG-17 | UPDATE **chỉ** `visitor_user_id`, không chạm `primary_contact_access_status` | `PRIMARY_CONTACT_USER_MUST_BE_ACTIVE_VISITOR` |
| NEG-18 | Identity change trỏ tới VISITOR chưa xác nhận email | `PRIMARY_CONTACT_VISITOR_ACCOUNT_INACTIVE` |
| POS-08 | Vô hiệu hóa VISITOR chỉ liên kết request **CANCELLED** | được phép (đúng quyết định đã ghi) |

POS-08 có post-condition khẳng định fixture **thật sự** đã liên kết một request cancelled, nên nó không
thể pass bằng cách âm thầm vô hiệu hóa một visitor chẳng liên kết gì.

### 2.3. Bằng chứng database

| Kiểm tra | Kết quả |
|---|---|
| Fresh import canonical (final) | `negative_failures = 0`, `positive_failures = 0` · 18 NEG + 8 POS |
| Shape sau G12 | 83 bảng · 32 trigger · 254 FK · 30 template · 22 `sent_emails` — **y hệt baseline G11** |
| Guard migration: không đặt biến xác nhận | từ chối `45000`, 0 DDL |
| Guard migration: đặt sai tên DB | từ chối `45000`, 0 DDL |
| Migration lần 1 | `03_verify.sql` → **34 PASS / 0 FAIL / 0 INFO**, exit 0 |
| Migration lần 2 | snapshot trigger **MD5 giống hệt** lần 1 → idempotent |
| Đối chứng âm (xóa 1 guard) | verify exit **1**, 1 FAIL — gate hoạt động |
| Migrated vs fresh canonical | **0/32** trigger body khác nhau (so ở mức HEX) |
| Nội dung template + trigger ngoài phạm vi | digest giống hệt → **không drift** |
| Sync template lần 1 & 2 | verify **16 PASS / 0 FAIL**, tiếng Việt nguyên vẹn (`58C3A163` = "Xác") |

Package: `docs/database/scripts/contact_guard_closure/` (`01_preflight` · `02_up_replace_triggers` ·
`03_verify` · `04_rollback_guidance.md`).

> Trong lúc viết `01_preflight.sql`, chính nó **báo sai một lần**: điều kiện "đã hardened" dùng
> `LIKE '%<=>%' AND LIKE '%VARCHAR(30)%'`, mà bản pre-G12 của `trg_users_protect_active_primary_contact_bu`
> vốn đã chứa cả hai (`NEW.role_id <=> OLD.role_id` và một biến role-code `VARCHAR(30)`). Nó tự tin
> tuyên bố "G12 already applied" trên một database chưa migrate. Marker giờ chọn theo từng trigger
> (`v_new_role_count` cho trigger users, `v_user_status VARCHAR(30)` + `LEFT JOIN roles` cho bốn trigger
> còn lại) và đã kiểm chứng **cả hai chiều**.

---

## 3. G11-J — một hợp đồng biến duy nhất

### 3.1. Nguyên nhân

Màn hình quản lý mẫu mang **danh sách 11 biến hard-code** (5 "dùng chung" + 6 "hậu cần") và đối chiếu
bất kể mẫu nào đang mở. `ACCOUNT_EMAIL_CONFIRMATION` khai báo `fullName`, `roleName`, `campusName`,
`expiresInHours` — **không biến nào có trong danh sách đó**. Hệ quả đo được:

- mẫu canonical chưa ai sửa mở ra là có cảnh báo *trên mọi biến nó dùng hợp lệ*;
- sidebar đồng thời mời chèn biến hậu cần mà mẫu này **không bao giờ** nhận được giá trị;
- preview thay giá trị bằng sample của frontend, tức preview và send dùng **hai bảng khác nhau**;
- `executeSubmit` gửi `subjectEn: formData.subject, bodyEn: formData.content` — **ghi nội dung tiếng
  Việt lên bản tiếng Anh mỗi lần lưu**, mất dữ liệu âm thầm;
- validate chạy từ lần render đầu, trước khi có bất kỳ request nào.

### 3.2. Thiết kế

```text
SystemEmailTemplates + EmailActionTemplates + SensitiveEmailVariables
        └─> EmailTemplateContracts.For(code)          ← một câu trả lời
                ├─ EmailVariableCatalog (label + sample VI/EN)
                ├─ GET /api/email-templates/contract/{code}   → sidebar, validate, capability
                ├─ EmailTemplateContentValidator              → editor + update handler
                └─ PreviewEmailTemplate (UseSampleData)       → preview
```

`requiredVariables` định nghĩa **hẹp có chủ đích**: chỉ biến mà **bỏ khỏi nội dung là hỏng thông điệp** —
mã OTP trong email đặt lại mật khẩu, `actionBlock` trong thư mời. Coi mọi biến khai báo là bắt buộc sẽ
cấm người vận hành viết lại một câu văn không còn nhắc tên cơ sở, mà đó là sửa nội dung bình thường,
không phải lỗi. Không gì là tùy chọn khi **resolve**: placeholder có mặt mà runtime không có giá trị thì
send vẫn fail-closed.

### 3.3. Hai chế độ preview — và tại sao phải có hai

Lần sửa đầu của tôi làm preview **luôn** lấp sample. Nó phá một assertion đúng của G8
(`The_preview_refuses_exactly_what_the_send_refuses`), và assertion đó đúng:

- **Màn hình quản lý mẫu** không cầm thông điệp thật nào — nó cho người vận hành xem *câu chữ*. Không
  có sample thì mẫu canonical preview ra lỗi, đúng như đang xảy ra.
- **Modal soạn email** đang preview một thông điệp **thật** sắp gửi cho người thật. Lấp chỗ trống bằng
  "Nguyễn Văn An" ở đó sẽ cho host xem một bản khác với bản người nhận nhận được — và họ bấm duyệt.

Nên `UseSampleData` mặc định **false** (nghiêm, giống send); caller phải chủ động xin sample, và chỉ
đúng khi không có dữ liệu thật nào để mà sai.

### 3.4. `{{actionBlock}}` — bẫy tôi tự đặt rồi tự gỡ

Bản đầu chỉ cho `actionBlock` hợp lệ với **5** template có action spec. Nhưng **14** template canonical
viết `{{actionBlock}}` trong body. Kết quả: 9 template còn lại **không lưu được** — đúng cùng một kiểu
từ chối oan mà cả hợp đồng này ra đời để dẹp, chỉ nhắm vào tập template khác. Đã sửa: `actionBlock`
**hợp lệ trên mọi template** (backend cấp nó, không phải người vận hành), **bắt buộc** chỉ nơi registry
khai báo action spec, và **cấm trong subject** ở mọi nơi.

### 3.5. Gate

| Yêu cầu | Bằng chứng |
|---|---|
| Catalog phủ đúng registry, không thiếu không thừa | `EmailTemplateContractTests` (2 test đối xứng) |
| Toàn catalog mở lần đầu 0 false warning | `Every_template_accepts_content_built_from_its_own_contract` + FE `opens a canonical template with no warnings at all` |
| Sidebar 0 biến sai module | `Account_email_confirmation_offers_no_logistics_variables` + FE `offers only this template's variables` |
| Preview VI + EN toàn catalog | `Every_template_previews_from_samples_alone` × 2 ngôn ngữ, 0 unresolved placeholder |
| Không token/link thật trong preview | `No_preview_contains_a_real_token_or_a_clickable_link` × 2 |
| Biến lạ / sai casing / malformed bị chặn lưu | `EmailTemplateContentValidatorTests` |
| Xóa required / action block bị chặn | 2 test unit + 2 test integration |
| Xóa optional được phép | `Removing_an_optional_variable_is_allowed` |
| Send thiếu runtime value vẫn fail-closed | `Without_sample_mode_a_missing_caller_variable_still_fails` |
| Hot-edit có hiệu lực không restart | `EmailG8JourneyTests` (đã có, vẫn xanh) |

---

## 4. G11-I — catalog cố định, chỉ update

| Yêu cầu | Cách làm | Bằng chứng |
|---|---|---|
| Bỏ "Thêm mẫu mới" | Xóa khỏi UI **và** handler `Create` từ chối vô điều kiện | `offers no way to create a template` + `Create_is_refused_with_a_stable_code` (kèm assert **số dòng không đổi**) |
| Không Delete/Clone | Không có command nào tồn tại | `No_delete_command_exists_for_email_templates` (reflection) |
| Không đổi trạng thái | Handler `Toggle` từ chối | `Toggle_status_is_refused_with_a_stable_code` |
| Không đổi `templateCode`/metadata | Các property **bị xóa khỏi command** | `Update_command_does_not_expose_a_registry_owned_field` (6 field) |
| Không mass assignment | Whitelist 6 field nội dung | `An_update_cannot_move_the_registry_owned_fields` |
| `variables_text` không thành authority của user | Ghi lại từ registry mỗi lần save | `An_update_rewrites_variables_text_from_the_registry` |
| Count/code set không đổi | — | `A_content_update_leaves_the_count_and_code_set_untouched` |
| Concurrent update không overwrite im lặng | Token `updated_at ?? created_at` | 3 test concurrency |
| Mẫu lịch sử giữ, không sửa | Refuse `EMAIL_TEMPLATE_CATALOG_FIXED` | `A_historical_template_is_kept_but_not_editable` |
| Quyền đúng PERMISSION_MATRIX | `[RoleAuthorize(Ho)]` theo từng action ghi | Controller |

### 4.1. Lỗi phân quyền có thật, tìm ra khi đối chiếu tài liệu với code

`EmailTemplatesController` mang **một** `[RoleAuthorize]` ở cấp class liệt kê StaffLeader, Staff,
DepartmentLead, Department, Ho — áp cho **mọi** action, kể cả `POST`, `PUT`, `PATCH /status`.
`PERMISSION_MATRIX.md` §5.5 cấp UC-42→45 **chỉ cho HO**, mọi role khác là `—`.

Nghĩa là Staff và Department **tạo, sửa và tắt được mẫu email hệ thống**. Phía đọc thì cần rộng (ai
soạn email cũng chọn và xem trước mẫu), nên chia theo action chứ không siết cả controller.

### 4.2. Chưa làm — nói rõ

**Restore default chưa triển khai.** Nội dung mặc định hiện chỉ tồn tại ở canonical seed và
`email_template_cc_bcc_sync/02_sync_templates.sql`; không cột nào trong `email_templates` giữ bản gốc,
nên sau lần hot-edit đầu tiên bản gốc không còn ở đâu trong DB để copy về. Làm đúng cần **một bảng
additive** (`email_template_defaults`) nạp từ chính seed đó — thay vì nhân bản 30 × 4 trường nội dung
vào C#, việc sẽ tạo ra đúng loại drift mà tài liệu này cấm.

Đường vòng đang dùng được: chạy lại `02_sync_templates.sql` (idempotent, đã kiểm chứng 2 lần trong lượt
này) để đưa **toàn bộ** catalog về canonical. Không có restore **từng mẫu** trong ứng dụng.

---

## 5. G11-H — TO/CC/BCC

Phần lớn đã có từ G6–G9 và vẫn xanh: `EmailRecipientValidator` (một chỗ duy nhất cho mọi đường gửi),
`RecipientChipInput`, `ReplyComposer`, `recipients.ts`, `EmailMimeEnvelopeTests`,
`SentEmailHistoryAuthorizationTests`. Lượt này đóng thêm:

### 5.1. `FileSinkEmailService` bỏ qua policy — và tại sao đó là lỗi nặng hơn nó trông

Sink test/evidence **ghi thẳng mọi thứ được đưa vào**: không validate envelope, không gọi
`EmailRecipientPolicyEnforcer`, không kiểm tra header-break.

Vấn đề không chỉ là thiếu một kiểm tra. Sink chính là nơi **real-stack evidence** được thu. Một lần
chạy có thể cho thấy mẫu mang token dùng-một-lần đi kèm BCC, ghi lại là **pass**, và không chứng minh
được gì về production — nơi đúng lệnh gửi ấy bị từ chối. Một test double enforce ít hơn thứ nó thay thế
thì sinh ra bằng chứng cho một hệ thống không tồn tại.

Đã sửa: sink áp **cùng** ba cửa như `EmailService`, và ghi lại **envelope đã normalize** thay vì
request thô. `FileSinkPolicyParityTests` — 10 test, gồm 4 case từ chối kèm assert **0 dòng được ghi**.

### 5.2. Capability do backend quyết định

`GET /email-templates/contract/{code}` trả `allowCc` / `allowBcc` / `carriesSecret` /
`securityClassification`. Frontend **không suy sensitive từ tên mẫu**. Editor hiển thị cảnh báo
"không được phép CC/BCC" từ đúng cờ đó (FE test `warns that a secret-bearing template cannot be copied`).

`No_secret_bearing_template_permits_copies` quét **toàn registry**, nên một mẫu nhạy cảm thêm sau này
mà quên đặt policy sẽ làm test đỏ.

### 5.3. Phạm vi có chủ đích, không phải bỏ sót

Manual compose (`SendEmailCommandHandler`) **không** mang `TemplateCode` vào lệnh gửi, nên policy
enforcer không áp — và đó là **đúng**: nội dung là do người dùng gõ, placeholder không được thay, không
token nào được mint, nên envelope là quyết định của người gửi. Ghi ở đây để lần sau không ai đọc thành
lỗ hổng rồi "sửa" bằng cách chặn oan.

---

## 6. Regression

| Gate | Kết quả | Baseline |
|---|---|---|
| Build `--no-incremental` | **0 error, 208 warning** | 208 |
| Unit | **1820 / 1820** | ≥ 1765 |
| Architecture | **14 / 14** | ≥ 14 |
| Integration (không filter) | **1242 / 1242** — xanh ở lần chạy 2 và 3 | ≥ 1176 |
| Frontend | **957 / 957**, **71** file | ≥ 914 / 69 |
| `tsc --noEmit` | exit 0 | 0 |
| `vite build` | exit 0 | 0 |

Delta test: **+78** (unit +55, integration +66 … xem `04-requirement-test-traceability.md`), frontend **+43**.

### 6.1. Ba lần đỏ, nguyên nhân và cách xử lý — không lần nào bị ép xanh

1. **`EmailTemplateCatalogTests` làm đổ 3 class khác.** Test của tôi sửa nội dung template canonical
   thật và suite dùng chung một database, nên `ACCOUNT_ACTIVATED` bị bỏ lại với marker của test; ba
   class không liên quan (renderer coverage, G4 closure, R-106 preview matrix) đỏ theo, mỗi cái **báo
   một lỗi sản phẩm không tồn tại**. Sửa: snapshot toàn bộ nội dung template ở constructor, restore ở
   `Dispose`. Một test phá fixture của test khác không chỉ đỏ ồn ào — nó làm code lành trông như hỏng.
2. **`EmailG8JourneyTests.The_preview_refuses_exactly_what_the_send_refuses`.** Đây là assertion
   **đúng** mà thay đổi của tôi làm yếu đi. Sửa bằng cách tách hai chế độ preview (Mục 3.3), giữ nguyên
   hành vi nghiêm làm mặc định — **không** nới assertion.
3. **`emailHtmlSanitization.test.tsx`** bấm nút "Thêm mẫu mới" mà G11-I đã bỏ. Viết lại để đi qua đường
   **edit**; assertion bảo mật (sanitize *sau* khi thay biến) giữ nguyên nguyên vẹn.

### 6.2. Một lần đỏ không tất định — nói rõ là chưa giải thích được

Lần chạy integration **đầu tiên**: `VisitReminderDispatchIdempotencyTests.Two_workers_racing_the_same_reminder_produce_one_set_of_messages`
đỏ (`Assert.Single` — collection rỗng, tức **0** thư thay vì 1). Lần 2 và lần 3 xanh hoàn toàn
(1242/1242), và nó xanh khi chạy có filter.

Đã điều tra: `SeedAsync` chọn **Staff Leader ACTIVE đầu tiên** rồi **ghi đè email của user đó** bằng
marker của suite, restore ở `finally`. Đó là mutation trên một hàng dùng chung, trong khi xUnit chạy
các test class song song. Không suite nào khác ghi vào cùng hàng đó, nên giả thuyết chưa đủ chắc để
tuyên bố là nguyên nhân.

**Phân loại: nondeterministic, chưa giải thích xong.** Không dán nhãn "flaky" cho qua, không sửa test
để nó khỏi đỏ. Việc tôi thêm hai class integration mới (một class import canonical riêng, mất ~15 s) có
đổi timing của lần chạy chung, nên khả năng cao là **làm lộ** một race sẵn có chứ không tạo ra lỗi sản
phẩm. Có tiền lệ cùng dạng đã ghi nhận: `FileDownloadAuthorizationTests` đỏ một lần trong G6.5 và chưa
bao giờ tái hiện.

---

## 7. Static scan

3063 file (tracked **và** untracked). Các mục đáng nói:

| Term | Đếm | Phân loại |
|---|---:|---|
| `DeleteEmailTemplate` | 1 | **chỉ trong file kế hoạch** — không có code |
| `CloneEmailTemplate` | 1 | **chỉ trong file kế hoạch** — không có code |
| `DELIVERED` | 98 | 1 hằng số enum; **0 chỗ code gán** trạng thái này |
| `dangerouslySetInnerHTML` | 26 | 0 chỗ chưa sanitize (2 chỗ bị flag: 1 sanitize cách 9 dòng, 1 là comment trong chính sanitizer) |
| `javascript:` / `onerror` | 44 / 40 | tài liệu + test sanitize (assertion âm) |
| `contact_guard_*_failures` | 10 / 5 | canonical SQL + tài liệu |

---

## 8. Rủi ro còn lại

1. **R-104 / R-105 vẫn BLOCKED** — không triển khai, không tự quyết. Xem `08-open-product-decisions.md`.
2. **Restore default từng mẫu chưa có** (Mục 4.2). Đường vòng: chạy lại sync script.
3. **Concurrency ở độ phân giải giây.** Hai lần lưu trong cùng một giây không phân biệt được. Cần cột
   row-version đơn điệu.
4. **Một lần đỏ không tất định** ở reminder race test (Mục 6.2) — chưa giải thích xong.
5. **`FileSinkEmailService` đọc `MaxRecipients` từ default** thay vì configuration (nó chỉ chạy trong
   Testing). Nếu môi trường test cần trần khác, phải truyền `IOptions` vào.
6. **Ba file stub rỗng tên `Idempotency*`** vẫn còn (di sản G11) — nên xóa trong một commit riêng.

---

## 9. G10

`G10 readiness: READY` — với **hai** prerequisite database, chạy theo đúng thứ tự này:

```text
Backup → §5A email_dispatch_idempotency (G11) → §5B contact_guard_closure (G12) → verify → backend → frontend
```

`G10 execution: NOT STARTED`.

`contact_guard_closure` **không** sửa dữ liệu: nó chỉ thay body của 5 trigger. Nhưng nếu database đang
có hàng vi phạm invariant, những hàng đó **vẫn nằm đó** và mọi lần ghi sau vào chúng sẽ bắt đầu lỗi —
đây là lý do `01_preflight.sql` có 5 truy vấn đếm vi phạm và phải đọc trước khi chạy `02`.
