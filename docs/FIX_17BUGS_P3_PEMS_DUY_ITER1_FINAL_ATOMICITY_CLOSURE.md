# PEMS — FINAL OPERATIONAL CONTACT ATOMICITY CLOSURE PLAN

## 0. Mục tiêu

Tiếp tục trực tiếp trên branch:

```text
Duy-Iter1
```

Baseline hiện tại theo báo cáo gần nhất:

```text
Branch:              Duy-Iter1
HEAD:                930e79294c62ca1b1c082a04aa95bb4c327728cd
Working tree before: 63 modified, 10 untracked
Working tree after:  71 modified, 15 untracked
Commits created:     NONE
```

Các hạng mục V01–V18 đã gần hoàn tất.

Phần còn lại trước khi Project Owner commit là **đóng nốt consistency hole của operational-contact invitation token atomicity** ở 3 flow còn lại:

```text
Replace
Reinvite
Resend
```

`Transfer` đã được sửa theo hướng đúng:

```text
business mutation
+ token persistence
= cùng transaction

email dispatch
= sau commit, best-effort
```

Mục tiêu cuối cùng là áp cùng invariant này cho **tất cả operational-contact invitation flows**, sau đó chạy lại các gate bắt buộc và báo cáo.

---

# 1. Quy tắc quan trọng

Không:

```text
git reset
git reset --hard
git restore .
git checkout -- .
git clean
git stash
```

Không discard working tree hiện tại.

Không refactor lại toàn bộ V01–V18.

Không sửa module ngoài scope.

Không tạo schema/table mới.

Không đưa SMTP/email dispatch vào DB transaction.

Không nuốt lỗi token persistence.

Không fake transaction test bằng provider không hỗ trợ transaction rồi báo PASS.

Không commit trước khi hoàn thành audit và báo cáo.

---

# 2. Vấn đề còn lại cần sửa

AI Agent đã phát hiện:

```text
Replace
Reinvite
Resend
```

vẫn có khả năng mint operational-contact token **sau business commit**.

Điều này tạo cùng một class bug với V18.

---

# 3. Invariant bắt buộc cho mọi operational-contact invitation flow

Mọi flow tạo hoặc thay token phải đảm bảo:

```text
BEGIN TRANSACTION

business state mutation
identity change state
token version / resend count
history / audit
usable email action token(s)

COMMIT

email dispatch best-effort
```

Nếu token persistence fail:

```text
ROLLBACK
```

Không được để lại:

```text
PENDING identity change
+
không có usable token
```

Nếu SMTP/provider fail:

```text
business state vẫn giữ
token vẫn usable
resend vẫn recover được
```

---

# 4. Flow A — ReplaceOperationalContact

## 4.1 Vấn đề cần loại bỏ

Không cho phép:

```text
create INITIAL_CONFIRMATION PENDING
→ commit
→ mint token
→ mint fail
→ invitation pending nhưng không có usable token
```

---

## 4.2 Flow đích

```text
BEGIN TRANSACTION

validate actor
validate campus/request lifecycle
validate contact email
validate no conflicting pending identity change

update operational-contact snapshot nếu business flow yêu cầu

create VisitRequestIdentityChange
  ChangeKind = INITIAL_CONFIRMATION
  Status = PENDING

append identity event
append audit

mint ACCEPT/DECLINE token(s)
persist token(s)

COMMIT

dispatch invitation email best-effort
```

Nếu token persistence fail:

```text
rollback toàn bộ Replace mutation
```

Không được để form/contact snapshot đã thay nhưng invitation không dùng được.

---

# 5. Flow B — ReinviteOperationalContactConfirmation

## 5.1 Vấn đề cần loại bỏ

Không cho phép:

```text
create/reopen invitation PENDING
→ commit
→ mint token
→ mint fail
```

---

## 5.2 Flow đích

```text
BEGIN TRANSACTION

validate actor
validate campus still needs confirmation
validate no confirmed operational contact
validate no conflicting active invitation
validate lifecycle

create/recreate pending identity change theo current design
or reactivate the intended reinvite state only if current code already does so

append event/audit

mint usable token(s)
persist token(s)

COMMIT

dispatch email best-effort
```

Không tạo business rule mới nếu current handler đã có semantics đúng.

Chỉ sửa transaction/token boundary.

---

# 6. Flow C — ResendOperationalContactConfirmation

## 6.1 Đây là flow quan trọng nhất

Hiện pattern có thể là:

```text
invalidate old tokens
→ token_version++
→ resend_count++
→ commit
→ mint token mới
→ mint fail
```

Kết quả:

```text
identity change vẫn PENDING
old links đã chết
new link không tồn tại
```

Đây là trạng thái không được phép tồn tại.

---

## 6.2 Flow đích

```text
BEGIN TRANSACTION

lock pending identity change FOR UPDATE

validate:
- Status = PENDING
- invitation not expired
- actor allowed
- resend cooldown
- resend count
- campus/request still valid

invalidate/supersede token version cũ
increment token_version
increment resend_count

mint token(s) cho token_version mới
persist token(s)

append:
OPERATIONAL_CONTACT_INVITATION_RESENT

append audit

COMMIT

dispatch email best-effort
```

---

## 6.3 Failure semantics của Resend

### Token mint/persistence fail

Expected:

```text
ROLLBACK
```

Sau rollback:

```text
old token/version vẫn ở trạng thái như trước request resend
```

Không được tạo trạng thái:

```text
old token dead
new token missing
```

---

### Email dispatch fail

Expected:

```text
new token/version đã commit
invitation vẫn PENDING
email dispatch log error
business API không giả rằng DB rollback
```

Resend có thể được thực hiện lại theo cooldown/policy hiện tại.

---

# 7. Reuse code hiện tại

Phải reuse:

```text
MintInvitationTokensAsync
DispatchInvitationEmailAsync
```

hoặc exact helper names hiện tại sau khi verify code.

Không tạo implementation token thứ hai.

Nếu `SendInvitationAsync` cũ vừa mint vừa dispatch và không phù hợp transaction mới:

- tách caller sang mint + dispatch helpers hiện có;
- không duplicate token creation;
- không giữ helper cũ như một path nguy hiểm nếu không còn caller hợp lệ.

---

# 8. Audit toàn bộ callers

Search toàn repo:

```text
SendInvitationAsync
MintInvitationTokensAsync
DispatchInvitationEmailAsync
VisitContactClaim
VisitContactTransfer
VisitRequestIdentityChange
```

Lập bảng:

| Caller | Business mutation commit | Token persistence | Email dispatch | Atomic? |
|---|---|---|---|---|
| Create initial invitation | | | | |
| Replace | | | | |
| Reinvite | | | | |
| Resend | | | | |
| Transfer | | | | |
| Create V2 notifier nếu liên quan | | | | |

Sau fix phải xác nhận:

```text
không còn operational-contact invitation flow nào:
business COMMIT
→ token mint
```

Nếu còn intentional case:

```text
giải thích rõ lý do
```

Không bỏ qua.

---

# 9. Transfer — chỉ audit lại, không rewrite

`InitiateOperationalContactTransferCommandHandler` đã được báo cáo đúng:

```text
BeginTransactionAsync
→ guards
→ create TRANSFER PENDING
→ event/audit
→ mint tokens
→ SaveChanges
→ Commit
→ email dispatch outside transaction
```

Chỉ verify không bị regression trong lần sửa này.

Không refactor lại nếu không cần.

---

# 10. SMTP / email provider semantics

Email dispatch luôn nằm:

```text
sau COMMIT
```

Không kéo SMTP vào transaction.

Reason:

```text
DB transaction không được giữ mở trong lúc chờ network/email provider.
```

Nếu email fail:

```text
log failure
business state + usable token vẫn tồn tại
```

Nếu product hiện có resend:

```text
resend là recovery path
```

---

# 11. Error handling

Token persistence failure:

```text
không được catch rồi tiếp tục commit
```

Phải propagate để transaction rollback.

Email dispatch failure:

```text
được catch/log sau commit
```

Không biến thành rollback giả.

Không trả generic success/failure mâu thuẫn với DB state.

---

# 12. Tests — chỉ test những gì environment chứng minh được

Project Owner đã chốt:

```text
KHÔNG cần DB-backed IntegrationTests
KHÔNG cần ArchitectureTests .NET 9
```

Do đó không cố dựng MySQL chỉ cho closure này.

---

## 12.1 Unit/static tests nên bổ sung nếu architecture cho phép

Test:

```text
correct token_version passed to mint
resend_count increments đúng
dispatch được gọi sau business success
dispatcher failure không làm handler biến thành DB rollback logic
no duplicate dispatch
one invitation decision group per version
```

---

## 12.2 Không fake rollback test

Nếu in-memory provider:

```text
ignore transaction
```

thì không dùng nó để khẳng định:

```text
token persistence fail → DB rollback thật
```

Trong report ghi:

```text
Transaction rollback semantics:
NOT VERIFIED WITHOUT DB
```

Đây là kết quả hợp lệ theo scope Project Owner.

---

# 13. Audit 71 modified + 15 untracked

Sau fix chạy:

```bash
git status --short
git diff --stat
git diff --check
git diff --name-only
```

---

## 13.1 Unrelated files

Expected:

```text
NONE
```

Nếu có:

- báo rõ;
- không tự xóa nếu không chắc là file của task.

---

## 13.2 Generated files

Không commit:

```text
bin/
obj/
dist/
node_modules/
coverage/
TestResults/
.tmp-build/
logs/
```

---

## 13.3 Secrets

Rà:

```text
password
DB credential
SMTP password
Google secret
API key
raw token
private key
JWT production secret
```

Không commit secret thật.

---

## 13.4 Debug

Rà:

```text
console.log
Console.WriteLine
debugger
TODO TEMP
HACK
temporary bypass
hardcoded user id
hardcoded campus id
hardcoded request id
```

---

## 13.5 Authorization

Không được làm yếu:

```text
VisitFormReadService
Staff Leader contact gate
pending invitee visibility
news eligibility
contact management actor checks
```

Không broad permission để test pass.

---

# 14. Hai plan docs untracked

Báo cáo hiện có 2 plan `.md` untracked:

```text
FIX_17BUGS_P2_...
FIX_17BUGS_PEMS_MASTER_...
```

Mặc định:

```text
KHÔNG commit
```

trừ khi Project Owner yêu cầu lưu chúng vào repository.

Không tự add planning docs tạm chỉ vì đang untracked.

---

# 15. Required gates sau final fix

## Backend build

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
0 compile errors
```

---

## Backend UnitTests

Chạy full unit tests.

Baseline gần nhất:

```text
2456 passed
0 failed
0 skipped
```

Sau fix có thể tăng.

Expected:

```text
0 failed
```

---

## Frontend typecheck

Chạy exact script trong package:

```text
npm run lint
```

nếu repo hiện dùng `lint = tsc --noEmit`.

Báo đúng semantics:

```text
Frontend typecheck: PASS
Frontend ESLint: NOT AVAILABLE
```

nếu không có ESLint script.

---

## Frontend build

```bash
npm run build
```

Expected PASS.

---

## Frontend unit tests

Baseline gần nhất:

```text
2217/2217
131 files
```

Expected:

```text
0 failed
```

---

## Git diff

```bash
git diff --check
```

Expected:

```text
PASS
```

---

# 16. Gates được WAIVE

Phải report chính xác:

```text
Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner
```

Không ghi PASS.

---

# 17. Real-stack

Nếu environment không chạy:

```text
Real-stack:
NOT RUN — environment unavailable
```

Không block closure.

Không giả PASS.

---

# 18. Không commit

Sau khi sửa xong:

```text
KHÔNG COMMIT
```

AI Agent chỉ báo cáo.

Project Owner sẽ tự quyết định commit/push sau khi đọc report.

---

# 19. Report format bắt buộc

```text
## Preflight

Branch:
HEAD:
Working tree before:
Working tree after:

## Operational-contact atomicity closure

### Transfer
Transaction starts:
Business state write:
Token persistence:
Commit:
Email dispatch:
Failure semantics:
Changes in this pass:

### Replace
Transaction starts:
Business state write:
Token persistence:
Commit:
Email dispatch:
Failure semantics:
Files changed:

### Reinvite
Transaction starts:
Business state write:
Token persistence:
Commit:
Email dispatch:
Failure semantics:
Files changed:

### Resend
Transaction starts:
Old token invalidation:
Token version bump:
New token persistence:
Commit:
Email dispatch:
Rollback behavior:
Files changed:

## Audit all token callers

| Caller | Business commit before token? | Atomic after fix? | Notes |
|---|---|---|---|

Confirm explicitly:

No operational-contact invitation flow remains with:
business COMMIT → token mint

YES / NO

If NO, list every remaining caller.

## Static/unit verification

Tests added:
Tests changed:
What is proven:
What is NOT proven without DB:

## Working-tree audit

Modified:
Untracked:
Unrelated:
Generated:
Secrets:
Debug:
Hardcoded IDs:
Authorization concerns:
Tests skipped/disabled by this task:
git diff --check:

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

Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner

Real-stack:
PASS / NOT RUN + reason

## Remaining known issues

List all remaining issues.

If no remaining in-scope code issue:
NONE

Do not hide:
NOT VERIFIED WITHOUT DB

## Git

Branch:
HEAD:
Commits created:
NONE
Working tree:
```

---

# 20. Definition of Done

Chỉ báo:

```text
CODE CLOSURE COMPLETE FOR AGREED SCOPE
```

khi:

- [ ] Transfer atomic.
- [ ] Replace atomic.
- [ ] Reinvite atomic.
- [ ] Resend atomic.
- [ ] Không còn operational-contact flow commit-before-token.
- [ ] Token persistence failure không bị nuốt.
- [ ] SMTP dispatch nằm sau commit.
- [ ] Resend rollback không làm chết old link nếu new token persistence thất bại.
- [ ] Backend builds PASS.
- [ ] Backend UnitTests PASS.
- [ ] Frontend typecheck PASS.
- [ ] Frontend build PASS.
- [ ] Frontend UnitTests PASS.
- [ ] `git diff --check` PASS.
- [ ] Không secret.
- [ ] Không generated file.
- [ ] Không debug bypass.
- [ ] Không test bị disable.
- [ ] Không commit.
- [ ] Integration DB-backed ghi `NOT RUN — waived by project owner`.
- [ ] ArchitectureTests ghi `NOT RUN — waived by project owner`.

---

# 21. Sau khi hoàn thành

Project Owner sẽ tự:

```text
git add
git commit
git push origin Duy-Iter1
```

Sau đó gửi SHA mới để review trực tiếp toàn bộ diff:

```text
930e79294c62ca1b1c082a04aa95bb4c327728cd
→
NEW_SHA
```

Trước khi merge vào `Dev`, reviewer sẽ kiểm tra lại:

```text
V01 → V18
+
Replace/Reinvite/Resend token atomicity closure
+
security
+
tests
+
working-tree hygiene
```

Không merge chỉ dựa vào báo cáo Agent nếu chưa review diff sau push.
