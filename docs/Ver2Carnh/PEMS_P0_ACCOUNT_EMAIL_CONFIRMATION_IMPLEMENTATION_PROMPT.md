# PEMS — P0 ACCOUNT EMAIL CONFIRMATION & TRUTHFUL EMAIL DELIVERY
# SECURITY-CRITICAL IMPLEMENTATION PROMPT

Bạn tiếp tục triển khai PEMS trên nhánh `Canh-Iter1`.

Đây là phiên **IMPLEMENTATION + SECURITY VERIFICATION**. Không chỉ audit hoặc viết kế hoạch. Phải sửa code thật, bổ sung migration/test thật, chạy gate thật và commit theo functional slice.

Không làm chống đối, không tạo luồng xác nhận thứ hai, không giữ đường tạo tài khoản `ACTIVE` trực tiếp và không báo email `SENT` khi thực tế không gửi.

---

# 1. Trạng thái hiện tại

Checkpoint gần nhất:

```text
P0 EMAIL/ACCOUNT FLOW — IN PROGRESS
P1 PAUSED
P2 PAUSED
```

Baseline gần nhất được báo:

- Branch: `Canh-Iter1`
- Local HEAD tại checkpoint: `bc6f62cd`
- Chưa commit/push phần email-flow hiện tại
- `origin/Dev` có một commit frontend-toast phía trước nhưng không liên quan email-flow
- Có thay đổi của teammate trong `VisitRequestManagement.tsx` và một số module không liên quan
- Không được chạm, stage, revert hoặc commit các thay đổi không thuộc task
- Không gửi email thật
- Phần P0 #3a đã sửa trong working tree:
  - `EmailService` không còn log full email body, OTP hoặc token khi SMTP disabled
  - chỉ log metadata an toàn
  - `PEMS.Infrastructure` build 0 error

Không tin tuyệt đối HEAD trên. Trước khi làm phải chạy preflight và dùng source thật làm chuẩn.

---

# 2. Preflight bắt buộc

Chạy:

```bash
git status --short --branch
git branch --show-current
git rev-parse HEAD
git fetch origin Cảnh-Iter1
git rev-parse origin/Cảnh-Iter1
git rev-list --left-right --count origin/Cảnh-Iter1...HEAD
git log -15 --oneline --decorate
git stash list --format="%gd %H %s"
git diff --check
```

Xác định:

1. File nào là thay đổi của task email/account.
2. File nào là WIP của teammate/chủ dự án.
3. File nào untracked nhưng không thuộc task.
4. Remote có thay đổi ngoài dự kiến hay không.

Nếu remote hoặc working tree thay đổi ngoài dự kiến:

- không merge;
- không rebase;
- không reset;
- không revert;
- không push;
- báo chính xác file/commit và dừng.

Không stage bằng `git add .`.

---

# 3. Root causes đã xác nhận

## P0 #1 — Không xác nhận quyền sở hữu email

`CreateAccountCommandHandler` hiện tạo tài khoản ở trạng thái `ACTIVE` ngay.

Ảnh hưởng:

- nhập nhầm email hợp lệ vẫn có thể tạo tài khoản;
- người nhận nhầm có thể SSO-login;
- role và head authority được cấp ngay khi tạo;
- account chưa chứng minh quyền sở hữu email.

## P0 #2 — Đường song song AddDepartmentPersonnel không an toàn

`AddDepartmentPersonnelCommandHandler` có đường tạo account riêng:

- thiếu authorization đầy đủ;
- tạo thẳng `ACTIVE`;
- bypass shared account rules;
- hardcode login URL;
- không đi qua email confirmation.

## P0 #3 — Email status không trung thực

`EmailService` khi `Smtp:Enabled=false`:

- trước đây log full body chứa OTP/token;
- trả success;
- caller ghi `SENT` dù không gửi.

P0 #3a đã sửa logging metadata-only nhưng chưa commit.

---

# 4. Thiết kế đã được chủ dự án chốt

## Account status

Thêm trạng thái mới:

```text
PENDING_EMAIL_CONFIRMATION
```

Không tái sử dụng `INACTIVE`.

Ý nghĩa:

- `PENDING_EMAIL_CONFIRMATION`: tài khoản mới, chưa xác nhận đúng email;
- `INACTIVE`: tài khoản bị vô hiệu hóa/ngừng hoạt động.

Pending account:

- không đăng nhập password;
- không đăng nhập SSO;
- không refresh token;
- không được chọn làm Host;
- không xuất hiện trong danh sách active personnel;
- không có effective authority;
- chỉ được giữ reservation theo quy tắc Head.

## Authority timing

Chọn:

```text
Reserve slot at creation, activate authority at confirmation
```

Reservation:

- chặn tạo thêm account pending/active cho cùng Head slot;
- không cấp quyền nghiệp vụ.

Effective authority chỉ có khi:

```text
user.status = ACTIVE
AND email confirmation = CONFIRMED
```

## Confirmation endpoint

- `POST`, không dùng GET để thay đổi trạng thái;
- token chỉ lưu hash;
- frontend có trang confirm riêng;
- URL dùng config, không hardcode domain.

---

# 5. Thứ tự triển khai bắt buộc

```text
0. Commit P0 #3a riêng sau targeted tests
1. Containment P0 #2
2. P0 #3b truthful email delivery
3. P0 #1 pending account + email confirmation
4. Hoàn tất P0 #2 bằng shared provisioning
5. Full P0 regression
6. Chỉ sau đó mới sang P1
```

Không bắt đầu P1/P2 khi P0 chưa đóng hoàn toàn.

---

# 6. Slice 0 — Commit P0 #3a an toàn

Mục tiêu:

- không log email body;
- không log OTP;
- không log token;
- không log full action URL;
- chỉ log metadata an toàn như recipient domain, template key, environment, reason skipped.

Bổ sung targeted tests:

1. SMTP disabled không log body.
2. Không log OTP/token.
3. Không log full URL chứa token.
4. Log chỉ chứa metadata an toàn.
5. Build sạch.

Commit:

```text
fix(email): stop logging sensitive email content
```

Không trộn với contract `TrySendAsync`.

---

# 7. Slice 1 — Containment khẩn cấp cho AddDepartmentPersonnel

Trước khi state machine mới hoàn tất, khóa đường bypass hiện tại.

## Controller

- thêm policy/role authorization đúng permission matrix;
- không chỉ dựa vào frontend;
- actor ngoài scope trả 403.

## Application handler

Enforce lại:

- campus scope;
- department scope;
- actor permission;
- role/sub-role hợp lệ;
- không tạo account `ACTIVE` trực tiếp.

Tạm thời:

- nếu gán một user `ACTIVE` đã tồn tại và đúng scope: cho phép theo rule;
- nếu cần tạo account mới trước khi shared provisioning hoàn tất: trả business error rõ ràng hoặc route sang shared command nếu đã sẵn sàng;
- không triển khai confirmation flow riêng tại đây.

Tests:

1. Unauthorized actor → 403.
2. Đúng role nhưng sai campus → từ chối.
3. Đúng campus nhưng sai department → từ chối.
4. Active existing user hợp lệ → gán được.
5. New account không thể được tạo `ACTIVE` trực tiếp.
6. Không hardcode URL.

Commit:

```text
fix(accounts): contain unsafe department personnel provisioning
```

Sau P0 #1 sẽ hoàn thiện lại slice này bằng shared provisioning.

---

# 8. Slice 2 — P0 #3b truthful email delivery

## Contract mới

Thêm result rõ ràng:

```text
Sent
Failed
Skipped
```

Có thể kèm:

- `ProviderMessageId`
- `FailureCode`
- `SafeFailureMessage`

Không trả:

- raw exception;
- token;
- OTP;
- full body;
- secret.

Có thể đặt tên:

```text
EmailDeliveryResult
EmailDeliveryOutcome
TrySendAsync
```

Không bắt buộc đúng tên, nhưng semantics phải rõ.

## Environment rules

### Development/Testing + SMTP disabled

```text
Skipped
```

- không ghi `SENT`;
- không coi là provider success;
- metadata-only log;
- file sink dùng riêng cho E2E nếu configured.

### Production + SMTP disabled/misconfigured

```text
Failed
```

- fail-closed;
- health/config validation phải báo lỗi;
- không giả vờ đã gửi.

### Provider accepted

```text
Sent
```

Chỉ trường hợp này mới ghi `SENT`.

### Provider exception/rejection

```text
Failed
```

## Migrate callers

Tìm toàn bộ:

```text
SendAsync
SendEmailAsync
SendCoreAsync
SENT
sent_at
delivery_status
retry_count
```

Phân loại từng caller:

1. Cần delivery outcome để lưu status.
2. Fire-and-observe wrapper.
3. Test/mock.
4. Dead code.

Không thay máy móc.

Mọi caller ghi status phải:

- `Sent` → `SENT`;
- `Skipped` → `SKIPPED` hoặc trạng thái tương đương;
- `Failed` → `FAILED`;
- không đặt `sent_at` khi skipped/failed.

Tests:

1. Dev disabled → Skipped.
2. Test disabled → Skipped.
3. Production disabled → Failed.
4. Provider success → Sent.
5. Provider exception → Failed.
6. Caller không ghi SENT khi skipped.
7. Caller không set sent_at khi skipped/failed.
8. Không log secret/body/token.

Commit:

```text
refactor(email): report delivery outcomes truthfully
```

---

# 9. Slice 3 — P0 #1 pending account + email confirmation

## 9.1 Database migration

Thêm additive, idempotent migration/table:

```text
account_email_confirmations
```

Tối thiểu:

```text
confirmation_id
user_id
target_email
token_hash
status
expires_at
resend_count
created_at
updated_at
confirmed_at
cancelled_at
```

Nên có:

- unique/index `token_hash`;
- index `user_id`;
- index `(status, expires_at)`;
- FK đến users;
- status constraint/enum;
- audit timestamps.

Không lưu:

- plaintext token;
- OTP;
- email body;
- full confirmation URL.

Chỉ chạy trên disposable/allowlisted DB.

Không chạy trực tiếp trên `pems_db`, Railway hoặc Production.

## 9.2 Account creation

Shared provisioning phải:

1. validate account info;
2. validate normalized email uniqueness;
3. validate role/sub-role/campus/department;
4. reserve Head slot nếu cần;
5. tạo user:
   ```text
   PENDING_EMAIL_CONFIRMATION
   ```
6. không cấp effective authority;
7. tạo confirmation token hash;
8. lưu target email;
9. commit transaction;
10. gửi email sau commit;
11. ghi truthful delivery outcome.

Nếu gửi email failed/skipped:

- account vẫn pending theo contract;
- UI/admin phải thấy trạng thái gửi;
- cho resend;
- không đổi account thành ACTIVE.

## 9.3 Login enforcement

Xác minh và test:

- password login pending → 403;
- SSO login pending → 403;
- refresh pending → 403;
- pending không được resolve thành active authority;
- pending không vào active candidates.

Không nới login để “tiện confirm”.

## 9.4 Confirm transaction

`POST /.../confirm`

Trong transaction:

1. hash token input;
2. lock confirmation row;
3. kiểm tra tồn tại;
4. status = PENDING;
5. chưa hết hạn;
6. target email khớp email user hiện tại;
7. account vẫn pending;
8. reservation vẫn thuộc account;
9. chuyển user → ACTIVE;
10. chuyển confirmation → CONFIRMED;
11. set confirmed_at;
12. revoke/cancel các confirmation active khác;
13. ghi audit;
14. commit.

Replay:

- đã confirmed → idempotent response hoặc stable `ALREADY_CONFIRMED`;
- không activate lần hai;
- không tạo authority trùng.

## 9.5 Resend

- cooldown/rate limit;
- token cũ revoke/cancel;
- token mới hash-only;
- tăng resend_count;
- gửi qua truthful email contract;
- không log token;
- không tạo nhiều active token.

## 9.6 Edit email

Chỉ account pending mới sửa email theo flow này.

- validate email mới;
- revoke token cũ;
- update normalized email;
- tạo token mới;
- gửi lại;
- email cũ không confirm được;
- giữ reservation.

## 9.7 Cancel/delete/expire

Khi:

- confirmation hết hạn theo cleanup policy;
- account pending bị hủy/xóa;
- admin cancel pending account;

thì:

- confirmation chuyển EXPIRED/CANCELLED;
- giải phóng Head reservation;
- account không có authority;
- audit đầy đủ.

Không để reservation bị giữ vĩnh viễn.

## 9.8 Head reservation

Phải tách:

```text
reservation
effective authority
```

Pending Head:

- giữ slot;
- không được authorize như Head;
- không được hiện là active Head trong UI/business queries.

Không cho hai pending/active account giữ cùng slot.

Tests:

1. Create pending account.
2. Password login blocked.
3. SSO blocked.
4. Refresh blocked.
5. Pending không nằm trong active list.
6. Confirm hợp lệ → ACTIVE.
7. Invalid token.
8. Expired token.
9. Replay.
10. Resend invalidates old token.
11. Edit email invalidates old token.
12. Cancel releases reservation.
13. Expire releases reservation.
14. Delete pending releases reservation.
15. Hai account không reserve cùng Head slot.
16. Pending Head không có effective permission.
17. Confirm Head activates authority đúng một lần.
18. Email delivery status truthful.

Commit gợi ý:

```text
feat(accounts): require email confirmation before activation
```

Có thể tách migration và application flow thành hai commit nếu mỗi commit độc lập build/test được:

```text
feat(accounts): add email confirmation persistence
feat(accounts): activate pending accounts on email confirmation
```

Không commit migration không có contract test.

---

# 10. Slice 4 — Hoàn tất P0 #2 bằng shared provisioning

Xóa parallel create logic trong `AddDepartmentPersonnel`.

Luồng cuối:

```text
authorize actor
→ validate campus/department
→ active existing user: assign personnel
→ new user: shared account provisioning
→ PENDING_EMAIL_CONFIRMATION
→ reserve slot nếu cần
→ confirmation token
→ truthful email delivery
```

Bỏ:

- direct `ACTIVE`;
- hardcoded `https://pems.fpt.edu.vn`;
- role/authority insert riêng;
- email/token logic riêng;
- duplicate validation.

URL dùng:

```text
App:FrontendBaseUrl
App:PublicApiBaseUrl
```

Config phải validate.

Tests:

1. Unauthorized → 403.
2. Wrong campus → forbidden.
3. Wrong department → forbidden.
4. Existing active user assignment.
5. New user → pending.
6. New user không login được.
7. Confirmation → active.
8. Head reservation đúng.
9. Không direct ACTIVE path.
10. URL từ config.
11. Dev email skipped đúng.
12. Production email failure fail-closed đúng policy.

Commit:

```text
fix(accounts): use shared confirmation provisioning for department personnel
```

---

# 11. Frontend

## 11.1 Account list/status

Thêm mapping:

```text
PENDING_EMAIL_CONFIRMATION
→ Chờ xác nhận email
```

Phân biệt rõ:

- ACTIVE → Đang hoạt động
- INACTIVE → Ngừng hoạt động
- PENDING_EMAIL_CONFIRMATION → Chờ xác nhận email

Pending account actions:

- gửi lại email;
- sửa email;
- hủy pending account;
- xem trạng thái gửi;
- không hiển thị action nghiệp vụ như active account.

## 11.2 Confirm page

Route public nhận token.

Phải có:

- loading;
- success;
- invalid token;
- expired;
- already confirmed;
- email changed/token revoked;
- server error;
- network/timeout;
- retry phù hợp;
- nút đến đăng nhập.

Không tự login sau confirm nếu contract chưa cho phép.

Không confirm bằng GET.

## 11.3 Create account UI

Sau create:

```text
Tài khoản đã được tạo và đang chờ xác nhận email.
```

Không hiển thị “Đã kích hoạt”.

Nếu email `Skipped` ở Development:

```text
Tài khoản đang chờ xác nhận. Email chưa được gửi trong môi trường phát triển.
```

Nếu `Failed`:

- hiển thị lỗi gửi;
- account vẫn pending;
- cho resend;
- không giả success delivery.

## 11.4 Candidate lists

Pending account không xuất hiện trong:

- Host candidates;
- Staff assignment;
- active personnel selectors;
- Head effective-authority lists.

Tests frontend:

1. Pending badge.
2. Pending khác inactive.
3. Confirm success.
4. Invalid.
5. Expired.
6. Already confirmed.
7. Network/server error.
8. Resend.
9. Edit email.
10. Pending không trong candidate list.
11. Create account message đúng delivery outcome.
12. Modal không đóng khi mutation fail.
13. Toast success/failure theo shared toast helper.

---

# 12. P0 full verification

## Backend

```bash
dotnet build PEMS.slnx

dotnet test backend/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj

dotnet test backend/PEMS.UnitTests/PEMS.UnitTests.csproj

dotnet test backend/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

## Frontend

```bash
npm run lint
npm run test:unit
npm run build
```

## Real-stack/file-sink E2E

Dùng:

```text
disposable MySQL
→ .NET Testing/Development safe config
→ React
→ file-sink email
→ browser
```

Không gửi SMTP thật.

Journeys:

1. HO/Staff Leader tạo account.
2. Account pending.
3. Login blocked.
4. Confirmation email file-sink có link/token.
5. Confirm.
6. Login success.
7. Resend invalidates token cũ.
8. Edit email invalidates token cũ.
9. Wrong recipient email không chiếm account.
10. Head reservation.
11. Cancel/expire releases reservation.
12. AddDepartmentPersonnel new user đi qua shared flow.
13. Production-style disabled email → Failed.
14. Dev disabled email → Skipped.
15. Không log OTP/token/body.

Evidence cùng một HEAD:

- HEAD/status;
- migration hash;
- DB name;
- backend logs;
- frontend logs;
- TRX;
- browser trace;
- file-sink email metadata;
- no-secret log scan;
- DB rows trước/sau;
- timestamps.

---

# 13. Commit policy

Không giữ toàn bộ P0 uncommitted.

Commit theo functional slice:

```text
fix(email): stop logging sensitive email content
fix(accounts): contain unsafe department personnel provisioning
refactor(email): report delivery outcomes truthfully
feat(accounts): require email confirmation before activation
fix(accounts): use shared confirmation provisioning for department personnel
```

Có thể tách migration riêng nếu độc lập và được test.

Không tạo:

- report-only commit;
- test-count-only commit;
- `fix`;
- `fix again`;
- mỗi file một commit.

Không amend commit đã push.

Trước push:

```bash
git fetch origin Cảnh-Iter1
git rev-list --left-right --count origin/Cảnh-Iter1...HEAD
```

Nếu remote đổi:

- không merge;
- không rebase;
- không push;
- báo và dừng.

---

# 14. Không được làm

- Không gửi email thật.
- Không log OTP/token/body.
- Không lưu plaintext token.
- Không tạo account ACTIVE trước confirm.
- Không cấp effective Head authority trước confirm.
- Không giữ reservation sau cancel/expire/delete.
- Không hardcode frontend URL.
- Không dùng GET để confirm.
- Không tạo confirmation flow thứ hai.
- Không skip test.
- Không giảm assertion.
- Không disable FK/trigger.
- Không dùng protected DB.
- Không commit WIP của teammate.
- Không add prompt/untracked ngoài task.
- Không thêm tên AI vào commit.

---

# 15. Chỉ sang P1 khi P0 VERIFIED

Chỉ kết luận:

```text
P0 EMAIL/ACCOUNT FLOW VERIFIED
P1 AUTHORIZED
```

khi:

1. Không sensitive email log.
2. Delivery status truthful.
3. Pending status hoạt động.
4. Login/SSO/refresh blocked khi pending.
5. Confirm activates đúng một lần.
6. Resend/edit email revoke token cũ.
7. Reservation/effective authority tách đúng.
8. Cancel/expire/delete giải phóng slot.
9. AddDepartmentPersonnel không bypass.
10. Frontend confirm page hoạt động.
11. Pending badge/list/filter đúng.
12. Full backend/frontend gate xanh.
13. File-sink E2E xanh.
14. Evidence cùng một HEAD.
15. Không real email.
16. Local/remote sync.
17. Không AI names.

Nếu còn bất kỳ P0:

```text
P0 EMAIL/ACCOUNT FLOW IN PROGRESS
P1 PAUSED
P2 PAUSED
```

---

# 16. Báo cáo sau mỗi phiên

Báo:

```text
Current P0 slice
Local/remote HEAD
Ahead/behind
Working tree/stash/untracked

Root cause addressed
Files changed
Security invariant
Migration/schema impact
Email delivery outcome behavior

Tests added
Counts before/after
First-failure evidence
Backend/frontend/E2E gate

Commits
Push status
Remaining P0
Exact resume point
```

Không dừng chỉ để hỏi có tiếp tục không.

Chỉ dừng khi:

- remote/working tree thay đổi ngoài dự kiến;
- cần business decision chưa khóa;
- cần destructive operation trên DB thật;
- cần production credential/deployment;
- platform hard limit.

Mọi lỗi code/test/fixture thông thường phải tự root-cause và tiếp tục.
