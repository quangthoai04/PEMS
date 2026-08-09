# PEMS — FINAL MERGE CLOSURE PROMPT
## 5 remaining fixes + operational-contact email template sync + safe runtime SQL update

## 0. Mục tiêu

Tiếp tục trên branch:

```text
Duy-Iter1
```

Không làm lại toàn bộ V01–V18.

Không reset working tree.

Không chạy lại full canonical SQL trên database hiện tại chỉ để áp thay đổi email template.

Scope của pass cuối này chỉ gồm:

```text
F01 — Public Decline dùng VietnamNow
F02 — Confirmation page respect intendedAction
F03 — Approve/Reject bắt buộc ExpectedInstanceRowVersion
F04 — Reinvite recompute aggregate request status
F05 — Đồng bộ template email Operational Contact với cơ chế no-login hiện tại
```

Sau khi hoàn tất:
- chạy các gate được yêu cầu;
- audit working tree;
- báo cáo;
- KHÔNG tự merge vào Dev;
- chỉ commit nếu Project Owner yêu cầu.

---

# 1. Quy tắc an toàn

Không chạy:

```text
git reset
git reset --hard
git restore .
git checkout -- .
git clean
git stash
```

Không discard code hiện tại.

Không refactor ngoài scope.

Không đổi schema nếu không thật sự cần.

Không tạo bảng mới.

Không làm yếu authorization.

Không đưa SMTP/email provider vào DB transaction.

Không commit secrets.

Không fake test transaction bằng InMemory rồi báo rollback DB là PASS.

Không chạy:

```text
docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql
```

trên database hiện tại chỉ để cập nhật 2 email template.

---

# 2. PRE-FLIGHT

Trước khi sửa, chạy:

```bash
git branch --show-current
git rev-parse HEAD
git status --short
git diff --stat
git diff --check
```

Expected branch:

```text
Duy-Iter1
```

Không hardcode giả định HEAD cũ.

Báo HEAD thực tế tại thời điểm chạy.

Nếu working tree đang có thay đổi:
- giữ nguyên;
- không reset;
- chỉ sửa đúng scope;
- report file nào có trước và file nào phát sinh trong pass này nếu xác định được.

---

# 3. F01 — Public Decline phải dùng `IDateTimeService.VietnamNow`

## 3.1 Vấn đề

Operational-contact public decline hiện có path dùng:

```csharp
DateTime.Now
```

trong khi workflow còn lại dùng:

```csharp
IDateTimeService.VietnamNow
```

Điều này có thể làm sai:

```text
expiry validation
DeclinedAt
RetentionUntil
audit timestamp
event timestamp
```

trên server UTC/Railway.

---

## 3.2 Cần sửa

Inspect:

```text
PublicDeclineOperationalContactConfirmationCommandHandler
PublicContactAnswer
DeclineWithoutAccountAsync
```

Không để static helper tự gọi wall clock.

Preferred flow:

```text
handler inject IDateTimeService
        ↓
now = _clock.VietnamNow
        ↓
truyền now xuống helper
```

Ví dụ:

```csharp
var now = _clock.VietnamNow;

await PublicContactAnswer.DeclineWithoutAccountAsync(
    ...,
    now,
    cancellationToken);
```

---

## 3.3 Invariant

Trong public decline operational-contact không còn business logic dùng:

```text
DateTime.Now
DateTime.UtcNow
```

Mọi timestamp trong cùng action phải dựa trên cùng `now`.

---

## 3.4 Tests

Bổ sung test với fake clock.

Ví dụ:

```text
VietnamNow = 2026-08-09 16:00
```

Expected:

```text
DeclinedAt = 16:00
audit/event time = 16:00
RetentionUntil base = 16:00
```

Expiry:

```text
ExpiresAt <= VietnamNow
→ từ chối action theo canonical error
```

Không dùng real clock trong test.

---

# 4. F02 — Confirmation page phải respect `intendedAction`

## 4.1 Vấn đề

Operational-contact invitation hiện dùng hai token:

```text
ACCEPT token
DECLINE token
```

Mỗi token bị bind với một `IntendedAction`.

Confirmation-info đã trả:

```text
intendedAction
actionable
```

Nhưng UI không được render cả Accept và Decline cho cùng một action-bound token.

---

## 4.2 Logic đúng

Nếu:

```text
info.actionable == false
```

thì:

```text
không render mutation CTA
```

---

Nếu:

```text
info.intendedAction == ACCEPT
```

thì chỉ hiện:

```text
[Xác nhận làm đầu mối]
```

Không hiện Decline.

---

Nếu:

```text
info.intendedAction == DECLINE
```

thì chỉ hiện:

```text
Lý do từ chối (nếu business hiện hỗ trợ)
[Xác nhận từ chối]
```

Không hiện Accept.

---

## 4.3 Missing `intendedAction`

Phải fail-safe.

Không được:

```text
undefined → show both
```

Preferred:

```text
null/undefined
→ không render mutation CTA
→ hiển thị message link không hợp lệ / vui lòng dùng link mới nhất trong email
```

Nếu backend contract thực tế guarantee non-null cho actionable token thì vẫn thêm defensive fallback.

---

## 4.4 Tests

Bắt buộc:

### ACCEPT

```text
intendedAction = ACCEPT
actionable = true

Accept visible
Decline hidden
```

POST accept endpoint đúng một lần.

### DECLINE

```text
intendedAction = DECLINE
actionable = true

Decline visible
Accept hidden
```

POST decline endpoint đúng một lần.

### Settled token

```text
actionable = false
→ no mutation CTA
```

### Missing action

```text
undefined/null
→ fail-safe
→ no mutation CTA
```

---

# 5. F03 — Approve/Reject phải bắt buộc `ExpectedInstanceRowVersion`

## 5.1 Vấn đề

V12 đã có stale-review protection:

```text
render rowVersion
→ client gửi expectedInstanceRowVersion
→ backend lock current row
→ compare
→ mismatch => VISIT_INSTANCE_VERSION_CONFLICT
```

Nhưng nếu command/API vẫn cho:

```csharp
int? ExpectedInstanceRowVersion = null
```

và `null` được bỏ qua thì caller có thể bypass.

---

## 5.2 Mục tiêu

Approve và Reject đều phải yêu cầu expected row version.

Preferred:

```csharp
int ExpectedInstanceRowVersion
```

không nullable.

Nếu transport bắt buộc nullable thì validator/handler phải fail closed:

```text
null
→ reject request
→ không ghi decision
```

Có thể dùng stable validation code phù hợp codebase, ví dụ:

```text
VISIT_INSTANCE_VERSION_REQUIRED
```

nếu chưa có canonical code khác.

Không dùng:

```text
null → current version
```

Không auto-retry decision sau conflict.

---

## 5.3 Frontend

Verify mọi Approve/Reject caller đều gửi:

```text
rowVersion đang render trên màn review
```

Không hardcode.

Khi stale:

```text
409
→ báo dữ liệu đã thay đổi
→ reload
→ người duyệt xem lại
→ tự quyết định lại
```

---

## 5.4 Tests

Bắt buộc:

```text
missing expected version
→ rejected
→ no decision mutation

matching version
→ allow

stale version
→ VISIT_INSTANCE_VERSION_CONFLICT

Reject
→ cùng semantics với Approve
```

---

# 6. F04 — Reinvite phải recompute request aggregate

## 6.1 Vấn đề

Reinvite có thể đưa campus về:

```text
WAITING_CONTACT_CONFIRMATION
```

nhưng nếu không gọi canonical aggregate service thì:

```text
visit_requests.status
contact gate
contact_gate_revision
response.RequestStatus
```

có thể stale trong application state.

---

## 6.2 Cần sửa

Inspect:

```text
ReinviteOperationalContactConfirmationCommandHandler
IVisitRequestAggregateStatusService
VisitRequestAggregateStatusService
```

Inject aggregate service nếu chưa có.

Sau khi mutation campus/identity state được áp dụng và trước commit:

```csharp
var gateResult = _aggregate.Apply(visit);
```

hoặc exact API hiện có trong code.

Không tự duplicate aggregate logic.

Không tự bump `ContactGateRevision` thủ công nếu service đã làm.

---

## 6.3 Expected behavior

Ví dụ:

```text
request = PENDING_APPROVAL
campus = WAITING_REQUEST_APPROVAL
OperationalContactUserId = null
no active invitation

Reinvite
```

Expected:

```text
campus = WAITING_CONTACT_CONFIRMATION
request = PENDING_CONTACT_CONFIRMATION
```

Nếu gate chuyển từ open → closed:

```text
ContactGateRevision
```

được canonical aggregate service xử lý đúng.

Response phải trả status mới, không trả tracked state cũ.

---

## 6.4 Notification

Reinvite là gate-closing action.

Không gửi:

```text
approval-ready
```

notification trong action này.

Không tạo notification business mới ngoài scope.

---

## 6.5 Tests

Bắt buộc test:

```text
reinvite closes request contact gate
request status recomputed
response status matches recomputed status
gate revision semantics đúng theo aggregate service
```

---

# 7. F05 — Operational-contact email template phải đồng bộ với no-login flow

## 7.1 Hiện trạng

Logic operational-contact hiện đã dùng:

```text
2 one-time action tokens:
ACCEPT
DECLINE
```

Mỗi link:

```text
mở confirmation page
GET không mutate
không bắt buộc Google login
user chủ động POST action sau khi xem thông tin
```

Backend action block hiện là source of truth cho live links.

Nhưng canonical email content vẫn có wording cũ kiểu:

```text
"Trang xác nhận yêu cầu đăng nhập bằng đúng tài khoản Google..."
```

hoặc tiếng Anh tương đương.

Đây là content drift.

---

# 8. F05-A — Sửa defaults JSON

File:

```text
backend/PEMS.Application/Emails/Common/Assets/email-template-defaults.json
```

Chỉ sửa:

```text
VISIT_CONTACT_CLAIM
  bodyVi
  bodyEn

VISIT_CONTACT_TRANSFER
  bodyVi
  bodyEn
```

Không đổi template code.

Không đổi declared variables nếu không có contract change.

Không thêm token URL variables.

---

## 8.1 `VISIT_CONTACT_CLAIM`

### VI — bỏ wording Google login

Wording mới phải truyền đạt đúng ý:

```text
Vui lòng dùng các nút bên dưới để chấp nhận hoặc từ chối vai trò đầu mối liên hệ.
Bạn không cần đăng nhập PEMS.
Mỗi nút mở trang xác nhận bằng liên kết dùng một lần để Quý vị xem thông tin
chuyến thăm mới nhất trước khi quyết định.
```

Có thể chỉnh câu chữ cho tự nhiên nhưng không thay đổi semantics.

Security note nên dùng số nhiều vì có hai link:

```text
Các liên kết có thời hạn và mỗi liên kết chỉ dùng được một lần.
Vui lòng không chuyển tiếp email hoặc các liên kết này cho người khác.
```

### EN

Phải thể hiện:

```text
You do not need to sign in to PEMS.
Each button opens a confirmation page using a one-time link.
Review the latest visit details before deciding.
```

Security note:

```text
The links expire and each can be used once.
Do not forward the email or its links.
```

---

## 8.2 `VISIT_CONTACT_TRANSFER`

VI phải bỏ:

```text
Trang xác nhận yêu cầu đăng nhập bằng đúng tài khoản Google...
```

Thay bằng semantics:

```text
Bạn không cần đăng nhập PEMS để phản hồi lời mời này.
Các liên kết có thời hạn và mỗi liên kết chỉ dùng được một lần.
Vui lòng không chuyển tiếp email hoặc các liên kết này cho người khác.
```

EN tương ứng:

```text
You do not need to sign in to PEMS to respond.
The links expire and each can be used once.
Please do not forward the email or its links.
```

---

# 9. F05-B — KHÔNG sửa contract variables

Giữ nguyên variables hiện tại.

`VISIT_CONTACT_CLAIM` tiếp tục dùng các variable business hiện có, ví dụ:

```text
contactFullName
requestCode
delegationName
campusName
plannedTime
senderName
senderRole
senderEmail
senderPhone
senderDepartment
senderCampus
```

`VISIT_CONTACT_TRANSFER` có thêm:

```text
currentContactName
```

Không thêm:

```text
acceptUrl
declineUrl
confirmationUrl
token
rawToken
```

vào:

```text
variables_text
EmailVariableCatalog
editable template variables
```

---

# 10. F05-C — Giữ nguyên `{{actionBlock}}`

Phải giữ:

```text
{{actionBlock}}
```

trong body VI và EN của:

```text
VISIT_CONTACT_CLAIM
VISIT_CONTACT_TRANSFER
```

Không đổi thành:

```html
<a href="{{acceptUrl}}">
<a href="{{declineUrl}}">
```

Reason:

```text
raw action URLs là credential
backend mint token lúc gửi
backend inject trusted block
template editor không được sở hữu raw token/link
```

Không sửa:

```text
OperationalContactInvitationService token model
ContactRoleInvitationBlock
public routes
action token schema
```

trừ khi test chứng minh regression trực tiếp từ F01–F05.

---

# 11. F05-D — Sửa canonical full SQL

File:

```text
docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql
```

Sửa canonical seed content:

```text
row 70015 — VISIT_CONTACT_CLAIM
row 70016 — VISIT_CONTACT_TRANSFER
```

Phải khớp defaults JSON về:

```text
body_vi
body_en
```

Không đổi schema.

Không đổi IDs.

Không đổi `variables_text` nếu contract không thay đổi.

Không xóa `{{actionBlock}}`.

---

# 12. IMPORTANT — Canonical SQL KHÔNG phải runtime patch

`PEMS_FULL_VS_31_07_NEW.sql` chỉ được cập nhật để:

```text
fresh-create database mới
→ sinh đúng canonical template content
```

**KHÔNG chạy lại file full này trên database hiện tại để áp F05.**

Không:

```text
drop database
drop tables
reseed all business data
fresh import
```

chỉ vì cần sửa 2 template.

---

# 13. F05-E — Regenerate template sync SQL

File:

```text
docs/database/scripts/email_template_cc_bcc_sync/02_sync_templates.sql
```

File này là generated source.

**KHÔNG hand-edit.**

Sau khi:

```text
email-template-defaults.json
        ↓
canonical PEMS_FULL_VS_31_07_NEW.sql
```

đã đúng, regenerate `02_sync_templates.sql` theo procedure hiện có trong repo.

Giữ source-of-truth chain:

```text
email-template-defaults.json
        ↓
PEMS_FULL_VS_31_07_NEW.sql
        ↓
02_sync_templates.sql
```

Không đảo chiều.

---

# 14. F05-F — Tạo runtime patch cho database đang chạy

Tạo:

```text
docs/database/scripts/patches/
2026-08-09_visit_contact_no_login_email_copy.sql
```

Đây là file dùng để cập nhật database hiện tại **mà không chạy lại từ đầu**.

---

## 14.1 Runtime deployment rule

### Database hiện tại

Chỉ chạy:

```text
docs/database/scripts/patches/
2026-08-09_visit_contact_no_login_email_copy.sql
```

Không chạy canonical full SQL.

---

## 14.2 Patch phải là data-only

Không:

```text
ALTER TABLE
CREATE TABLE
DROP TABLE
TRUNCATE
DELETE toàn catalog
reseed toàn bộ email_templates
```

Chỉ targeted update:

```text
VISIT_CONTACT_CLAIM
VISIT_CONTACT_TRANSFER
```

---

## 14.3 Patch phải idempotent

Yêu cầu:

```text
chạy lần 1
→ sửa stale wording

chạy lần 2
→ không làm thay đổi tiếp
→ revision không tăng vô hạn
```

Dùng guard theo stale content.

Preferred:

```sql
WHERE template_code = '...'
  AND (
      body_vi LIKE '%stale wording%'
      OR body_en LIKE '%stale wording%'
  )
```

---

## 14.4 Mỗi template chỉ bump revision một lần

Không viết:

```text
UPDATE VI  → revision + 1
UPDATE EN  → revision + 1
```

cho cùng template.

Phải dùng một targeted UPDATE cho mỗi template:

```text
body_vi = ...
body_en = ...
revision = revision + 1
```

một lần.

---

## 14.5 Patch phải giữ operator content ngoài stale sentence

Ưu tiên:

```sql
REPLACE(...)
```

thay đúng câu cũ.

Không overwrite toàn bộ body runtime nếu không cần, vì Admin có thể đã chỉnh prose khác.

---

## 14.6 Patch skeleton

Patch nên có:

```sql
SET NAMES utf8mb4;

START TRANSACTION;

-- targeted UPDATE VISIT_CONTACT_CLAIM
-- targeted UPDATE VISIT_CONTACT_TRANSFER

COMMIT;

-- verification SELECT
```

Không hardcode credentials/database password.

---

## 14.7 Verification SQL

Cuối patch thêm:

```sql
SELECT
    template_code,
    revision,
    body_vi LIKE '%đăng nhập bằng đúng tài khoản Google%' AS stale_vi,
    body_en LIKE '%sign in with the Google account%' AS stale_en,
    body_vi LIKE '%{{actionBlock}}%' AS action_block_vi,
    body_en LIKE '%{{actionBlock}}%' AS action_block_en
FROM email_templates
WHERE template_code IN (
    'VISIT_CONTACT_CLAIM',
    'VISIT_CONTACT_TRANSFER'
);
```

Expected:

```text
stale_vi        = 0
stale_en        = 0
action_block_vi = 1
action_block_en = 1
```

---

# 15. F05-G — Re-pin canonical SQL normalized SHA-256

Vì canonical SQL thay đổi có chủ ý:

```text
docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql
```

phải update hash pin trong:

```text
tests/PEMS.IntegrationTests/TestInfrastructure/CanonicalSqlScript.cs
```

Dùng exact normalized hash helper/rule hiện có trong repo.

Không đoán hash.

Không dùng raw Windows CRLF hash nếu current contract dùng normalized content.

Chỉ update pin sau khi canonical SQL content đã final.

---

# 16. F05-H — Regression tests cho email no-login wording

File trọng tâm:

```text
tests/PEMS.IntegrationTests/Emails/
ContactRoleInvitationEndToEndTests.cs
```

Bổ sung regression assertion cho CLAIM và TRANSFER.

Phải chứng minh email thực gửi không còn:

```text
đăng nhập bằng đúng tài khoản Google
sign in with the Google account
```

và vẫn có:

```text
ACCEPT/confirmation URL
DECLINE URL
deadline
action block rendered
no unresolved {{
```

Có thể assert wording mới:

```text
không cần đăng nhập
do not need to sign in
```

nếu language của test phù hợp.

Không lưu raw token trong history vẫn phải giữ nguyên behavior/test cũ.

---

# 17. F05-I — Defaults / canonical parity

Rà các test hiện có như:

```text
EmailTemplateDefaultsParityTests
EmailTemplateContractTests
CanonicalSqlHashTests
```

Nếu source thay đổi làm test fail:
- sửa canonical/default generated chain đúng;
- không bypass test;
- không disable parity.

Expected:

```text
defaults JSON
canonical SQL
sync SQL
```

không drift.

---

# 18. Regression audit toàn pass

Sau khi sửa, search toàn repo:

```text
DateTime.Now
DateTime.UtcNow
ExpectedInstanceRowVersion
intendedAction
ReinviteOperationalContactConfirmation
VISIT_CONTACT_CLAIM
VISIT_CONTACT_TRANSFER
Google account
đăng nhập bằng đúng tài khoản Google
sign in with the Google account
{{actionBlock}}
acceptUrl
declineUrl
```

Xác nhận:

```text
F01:
public decline không dùng wall clock trực tiếp

F02:
action-bound token không còn UI cho action sai

F03:
Approve/Reject không bypass expected rowVersion

F04:
Reinvite recompute aggregate

F05:
email no-login wording đồng nhất code/default/canonical/runtime patch
```

---

# 19. Không sửa lại các phần đã đóng

Không rewrite nếu không có regression mới:

```text
V01 STAFF relation routing
V02 registrant editable snapshots
V03 waiting-contact edit
V04 global contact gate
V05 leader notification gate
V06 NO_ACTIVE_INVITATION
V07 reinvite semantics
V08 history aliases
V09 signed-in invitation surface
V10 public passwordless action-token architecture
V11 wording cũ đã sửa
V12 concurrency architecture ngoài nullable bypass F03
V13 accordion/deep-link
V14 live DB landing
V15 revision history
V16 news evaluator
V17 T-6 gate
V18 token atomicity
Replace/Reinvite/Resend/CreateV2 token mint atomicity
```

---

# 20. Backend gates

Chạy:

```bash
dotnet build backend/PEMS.Domain/PEMS.Domain.csproj
dotnet build backend/PEMS.Application/PEMS.Application.csproj
dotnet build backend/PEMS.Infrastructure/PEMS.Infrastructure.csproj
dotnet build backend/PEMS.Api/PEMS.Api.csproj
```

Expected:

```text
PASS
0 errors
```

---

# 21. Backend UnitTests

Chạy full UnitTests.

Expected:

```text
0 failed
```

Không disable test.

Không đổi `[Fact]` thành skip.

---

# 22. Frontend gates

Chạy theo package hiện tại:

```bash
npm run lint
npm run build
npm run test:unit
```

Nếu:

```text
lint = tsc --noEmit
```

thì report:

```text
Frontend typecheck: PASS
Frontend ESLint: NOT AVAILABLE
```

Không gọi typecheck là ESLint.

Nếu unit test flaky timeout lần đầu:
- rerun;
- report cả lần fail và lần pass;
- không che giấu.

---

# 23. SQL/static gates

Chạy/verify những gate không cần DB thật:

```text
canonical SQL hash/static tests nếu thuộc UnitTests
defaults parity tests nếu chạy được không cần DB
git diff --check
```

Nếu một test thực tế yêu cầu MySQL:
- không giả PASS;
- report theo waived DB-backed gate.

---

# 24. Gates được waive

Project Owner không yêu cầu chạy DB-backed IntegrationTests.

Report:

```text
Integration DB-backed:
NOT RUN — waived by project owner
```

Không ghi PASS.

ArchitectureTests .NET 9:

```text
ArchitectureTests:
NOT RUN — waived by project owner
```

Không retarget framework chỉ để chạy.

---

# 25. Real-stack

Nếu môi trường backend + MySQL thực không chạy:

```text
Real-stack:
NOT RUN — environment unavailable
```

Không block closure theo scope hiện tại.

---

# 26. Runtime SQL deployment instructions phải được report rõ

Report cuối phải phân biệt:

## Canonical source update

```text
PEMS_FULL_VS_31_07_NEW.sql
```

đã sửa để fresh import về sau đúng.

## Runtime database update

Database hiện tại chỉ cần chạy:

```text
docs/database/scripts/patches/
2026-08-09_visit_contact_no_login_email_copy.sql
```

Không cần fresh-create DB.

Không chạy lại full canonical SQL.

---

# 27. Audit working tree

Chạy:

```bash
git status --short
git diff --stat
git diff --check
git diff --name-only
```

Rà:

```text
unrelated files
generated files
secrets
debug code
hardcoded production IDs
disabled tests
temporary docs
```

Không commit:

```text
bin/
obj/
dist/
coverage/
TestResults/
node_modules/
temporary output
real credentials
```

---

# 28. Files F05 expected

Expected modified/new files có thể gồm:

```text
backend/PEMS.Application/Emails/Common/Assets/email-template-defaults.json

docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql

docs/database/scripts/email_template_cc_bcc_sync/02_sync_templates.sql

docs/database/scripts/patches/
2026-08-09_visit_contact_no_login_email_copy.sql

tests/PEMS.IntegrationTests/TestInfrastructure/CanonicalSqlScript.cs

tests/PEMS.IntegrationTests/Emails/
ContactRoleInvitationEndToEndTests.cs
```

Nếu cần file khác để giữ parity/contract:
- giải thích lý do;
- không mở rộng vô cớ.

---

# 29. Report format bắt buộc

```text
## Preflight

Branch:
HEAD before:
Working tree before:
Working tree after:

## F01 — Public Decline clock

Files:
Old behavior:
New behavior:
Clock source:
Tests:

## F02 — intendedAction UI

Files:
ACCEPT behavior:
DECLINE behavior:
actionable=false:
missing intendedAction:
Tests:

## F03 — Required decision rowVersion

Files:
Approve contract:
Reject contract:
Missing version:
Matching version:
Stale version:
Frontend caller verification:
Tests:

## F04 — Reinvite aggregate

Files:
Aggregate call location:
Request status behavior:
Gate revision behavior:
Response status:
Tests:

## F05 — Operational-contact email template sync

Templates:
- VISIT_CONTACT_CLAIM
- VISIT_CONTACT_TRANSFER

Defaults JSON:
Canonical SQL:
02_sync_templates regeneration:
Runtime patch:
Canonical hash re-pin:
Regression tests:

Variables changed:
NO / explain if YES

{{actionBlock}} preserved:
YES / NO

Raw accept/decline URL added to template variables:
NO

Google-login wording remaining:
NONE / list exact remaining occurrences

## Runtime DB update

Fresh full SQL required:
NO

File to run on existing DB:
docs/database/scripts/patches/2026-08-09_visit_contact_no_login_email_copy.sql

Patch data-only:
YES / NO

Patch idempotent:
YES / NO

Patch drops/recreates/reseeds database:
NO

Verification result if DB not run:
NOT RUN — DB execution not required in this closure
or actual result if locally tested safely

## Regression audit

Public decline DateTime.Now:
Both actions shown for action-bound token:
Approve/Reject nullable bypass:
Reinvite aggregate stale state:
Stale Google-login template wording:
Action block missing:
Raw token variables in editable template:

## Gates

Backend Domain:
Backend Application:
Backend Infrastructure:
Backend Api:
Backend UnitTests:

Frontend typecheck:
Frontend build:
Frontend UnitTests:
Frontend ESLint:

git diff --check:

Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner

Real-stack:
PASS / NOT RUN + reason

## Remaining known issues

List all remaining in-scope issues.

If none:
NONE

Do not hide:
NOT VERIFIED WITHOUT DB
where relevant.

## Git

Branch:
HEAD:
Commits created:
Working tree:
```

---

# 30. Definition of Done

Chỉ báo closure complete khi tất cả đúng:

## F01

- [ ] Public decline dùng `IDateTimeService.VietnamNow`.
- [ ] Không còn wall clock trực tiếp trong public decline business logic.
- [ ] Tests dùng fake clock.

## F02

- [ ] ACCEPT token chỉ hiện Accept.
- [ ] DECLINE token chỉ hiện Decline.
- [ ] `actionable=false` không có mutation CTA.
- [ ] missing intendedAction fail-safe.
- [ ] Frontend tests đủ.

## F03

- [ ] Approve bắt buộc expected rowVersion.
- [ ] Reject bắt buộc expected rowVersion.
- [ ] Missing version bị reject.
- [ ] Matching version pass.
- [ ] Stale version conflict.
- [ ] Không auto-retry decision.

## F04

- [ ] Reinvite gọi canonical aggregate service.
- [ ] Request status recompute.
- [ ] Gate revision do aggregate service xử lý.
- [ ] Response status không stale.

## F05

- [ ] `VISIT_CONTACT_CLAIM` body VI/EN không còn Google-login wording.
- [ ] `VISIT_CONTACT_TRANSFER` body VI/EN không còn Google-login wording.
- [ ] Defaults JSON cập nhật.
- [ ] Canonical SQL row 70015 cập nhật.
- [ ] Canonical SQL row 70016 cập nhật.
- [ ] `02_sync_templates.sql` regenerated, không hand-edit.
- [ ] Runtime patch được tạo.
- [ ] Runtime patch data-only.
- [ ] Runtime patch idempotent.
- [ ] Runtime patch chỉ target 2 template.
- [ ] Mỗi template bump revision tối đa 1 lần trên một lần migration thực.
- [ ] `variables_text` không thêm acceptUrl/declineUrl/token.
- [ ] `{{actionBlock}}` vẫn có ở VI/EN.
- [ ] Canonical normalized SHA-256 re-pinned.
- [ ] Regression assertions cho no-Google-login wording được thêm.
- [ ] Không cần chạy lại full SQL trên DB hiện tại.

## Gates

- [ ] Backend builds PASS.
- [ ] Backend UnitTests PASS.
- [ ] Frontend typecheck PASS.
- [ ] Frontend build PASS.
- [ ] Frontend UnitTests PASS.
- [ ] `git diff --check` PASS.
- [ ] Không secret.
- [ ] Không generated artifacts.
- [ ] Không debug bypass.
- [ ] Không test bị disable.

## Waived gates

Report exactly:

```text
Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner
```

---

# 31. Kết luận Agent được phép báo

Chỉ khi đạt toàn bộ scope trên mới báo:

```text
FINAL MERGE CLOSURE COMPLETE FOR AGREED SCOPE
```

Nếu còn bất kỳ lỗi in-scope nào:

```text
KHÔNG báo closure complete
```

và liệt kê phần còn lại.

---

# 32. Quy tắc cuối cho database hiện tại

Đây là điểm không được hiểu sai:

```text
PEMS_FULL_VS_31_07_NEW.sql
= canonical fresh-create source
= cập nhật để tương lai import mới đúng
= KHÔNG dùng để deploy F05 lên DB hiện tại
```

Database hiện tại deploy bằng:

```text
docs/database/scripts/patches/
2026-08-09_visit_contact_no_login_email_copy.sql
```

Flow:

```text
Existing DB
   ↓
run targeted runtime patch
   ↓
update 2 email_templates rows
   ↓
preserve all other application/business data
```

Không drop/recreate/reseed database.
