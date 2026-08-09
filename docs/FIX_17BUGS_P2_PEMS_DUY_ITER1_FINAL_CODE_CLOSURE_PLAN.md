# PEMS — FINAL CODE CLOSURE PLAN CHO Duy-Iter1

## 0. Mục tiêu

Tiếp tục trực tiếp trên branch:

```text
Duy-Iter1
```

để hoàn thiện nốt các phần còn thiếu sau khi AI Agent đã triển khai master plan V01 → V18.

Baseline được báo cáo gần nhất:

```text
Branch:        Duy-Iter1
HEAD start:    930e79294c62ca1b1c082a04aa95bb4c327728cd
Working tree:  63 modified, 8 new files
```

Không làm lại toàn bộ V01–V18. Chỉ:

1. Hoàn thiện các phần còn thiếu.
2. Audit toàn bộ working tree hiện tại.
3. Chạy các gate bắt buộc còn lại.
4. Không chạy các gate Project Owner đã waive.
5. Không commit cho tới khi code/audit/gate bắt buộc hoàn tất.
6. Sau khi Project Owner commit + push lên `Duy-Iter1`, code sẽ được review lại trực tiếp trên GitHub trước khi merge vào `Dev`.

---

# 1. Trạng thái implementation hiện tại

Theo báo cáo gần nhất của AI Agent:

| ID | Trạng thái |
|---|---|
| V01 | Đã triển khai |
| V02 | Đã triển khai |
| V03 | Đã triển khai |
| V04 | Đã triển khai |
| V05 | Đã triển khai |
| V06 | Đã triển khai |
| V07 | Đã triển khai |
| V08 | Đã triển khai |
| **V09** | Backend xong, **frontend chưa làm** |
| V10 | Đã triển khai |
| V11 | Đã triển khai |
| V12 | Đã triển khai |
| V13 | Đã triển khai |
| V14 | Minimal solution đã làm; phần email-update riêng là optional |
| V15 | Đã triển khai |
| **V16** | Backend canonical eligibility đã làm; **Create News FE reason mapping chưa hoàn tất** |
| V17 | Đã triển khai |
| V18 | Đã triển khai code; cần static/unit audit lại transaction boundary |

Không được tuyên bố hoàn thành nếu V09 hoặc V16 vẫn chưa xong.

---

# 2. Các gate Project Owner chủ động WAIVE

Trong closure lần này:

## KHÔNG bắt buộc chạy

```text
DB-backed IntegrationTests trên MySQL
ArchitectureTests bằng .NET 9
```

Trong báo cáo cuối phải ghi đúng:

```text
Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner
```

Không được ghi `PASS`.

Nếu real-stack journey không thể chạy trong environment hiện tại:

```text
Real-stack:
NOT RUN — environment unavailable
```

Không được giả định pass.

---

# 3. Quy tắc làm việc

## 3.1 Không phá working tree hiện tại

Không:

```text
git reset
git reset --hard
git checkout -- .
git restore .
git clean
git stash
```

trừ khi Project Owner yêu cầu rõ.

Không discard các thay đổi V01–V18 đang tồn tại.

## 3.2 Không refactor ngoài scope

Chỉ sửa:

- V09 frontend;
- V16 frontend reason mapping;
- regression thật phát hiện trong audit;
- static/unit issue của V18 nếu cần;
- tests liên quan;
- i18n/types/api wiring cần thiết.

Không:

- rewrite toàn bộ Visit V2;
- đổi architecture không cần thiết;
- tạo dynamic permissions;
- broadening authorization chỉ để UI hoạt động;
- tạo schema/table mới nếu không bắt buộc;
- sửa các module không liên quan.

---

# 4. PRE-FLIGHT bắt buộc

Trước khi code:

```bash
git branch --show-current
git rev-parse HEAD
git status --short
git diff --stat
git diff --check
git diff --name-only
```

Expected branch:

```text
Duy-Iter1
```

Ghi lại:

```text
Branch:
HEAD:
Modified:
Added:
Untracked:
```

Nếu HEAD khác baseline:

```text
930e79294c62ca1b1c082a04aa95bb4c327728cd
```

không tự reset. Chỉ báo lại HEAD thực tế và tiếp tục từ đó nếu working tree vẫn là task này.

---

# 5. V09 — HOÀN THIỆN FRONTEND “LỜI MỜI ĐẦU MỐI CỦA TÔI”

## 5.1 Backend đã có

Backend được báo cáo đã triển khai:

```text
GET /api/v2/me/operational-contact-invitations
POST accept-by-id
POST decline-by-id
```

hoặc route tương đương theo code thực tế.

Agent phải verify route/type hiện tại trước khi viết FE. Không invent route.

## 5.2 Mục tiêu UX

Người dùng đã đăng nhập, nếu email tài khoản trùng invitation target, phải có surface:

```text
Lời mời đầu mối của tôi
```

để:

```text
Xem lời mời
→ Xác nhận
hoặc
→ Từ chối
```

Không bắt người dùng tìm lại email.

## 5.3 Quyền truy cập

Pending invitee:

```text
CHƯA phải confirmed operational contact
```

nên không được mở full request detail chỉ vì email match.

Không mở rộng `VisitFormReadService` thành broad request visibility. FE chỉ dùng invitation-specific API.

## 5.4 Dữ liệu hiển thị

Mỗi invitation hiển thị tối thiểu nếu backend trả:

```text
Request code
Campus
Delegation name
Planned start/end
Invitation kind
Masked/allowed email
Status
ExpiresAt
```

Có thể thêm current contact name với TRANSFER nếu backend đã expose an toàn.

Không expose:

```text
token raw
token hash
internal correlation id
internal snapshot JSON
sensitive full fields không cần thiết
```

## 5.5 States

### Loading

Hiển thị skeleton/spinner phù hợp.

### Empty

```text
Bạn hiện không có lời mời đầu mối nào cần xử lý.
```

### Pending

Có:

```text
[Xác nhận]
[Từ chối]
```

### Accepted

Không còn CTA quyết định. Hiển thị `Đã xác nhận`.

Sau Accept:

1. refresh invitation list;
2. refresh relevant relation/state;
3. nếu backend đã link `OperationalContactUserId`, user từ lúc này mới có normal contact relation;
4. navigation nếu có phải dựa trên backend relation mới.

### Declined

Không còn CTA quyết định. Hiển thị `Đã từ chối`.

### Expired / Cancelled / Superseded

Read-only. Không hiển thị Accept/Decline.

## 5.6 Double submit

Khi action đang chạy:

```text
disable Accept
disable Decline
```

Không cho double click tạo hai request.

## 5.7 Error handling

Map stable backend codes nếu đã có. Ví dụ:

```text
INVITATION_EXPIRED
INVITATION_ALREADY_RESPONDED
INVITATION_NOT_FOR_CURRENT_USER
CONTACT_CHANGE_NOT_PENDING
```

Dùng code thực tế trong repo. Không invent error contract nếu backend đã có.

Fallback chỉ dùng cho lỗi không xác định.

## 5.8 Navigation / discoverability

Agent phải tìm vị trí phù hợp trong UI hiện tại.

Ưu tiên một trong:

```text
Notification → invitation page
Profile/user menu → Lời mời đầu mối của tôi
Visit management → invitation surface
```

Không tạo menu top-level mới nếu không cần. Nếu notification hiện tại đã có link invitation, reuse.

## 5.9 i18n

Phải có VI + EN. Không hardcode nguyên một flow bằng tiếng Việt trong component.

## 5.10 Tests V09

Tối thiểu:

- API [] → empty state.
- PENDING → Accept + Decline visible.
- Accept → API once → loading disabled → refresh → CTA gone.
- Decline → tương tự.
- EXPIRED → no CTA.
- Backend stable code → đúng message.
- Không có route/link full request detail trước confirmed relation nếu backend không cấp.

---

# 6. V16 — HOÀN THIỆN CREATE NEWS REASON-CODE MAPPING

## 6.1 Vấn đề còn lại

Backend đã canonicalize News eligibility.

Nhưng Create News frontend vẫn còn câu kiểu:

```text
Chuyến tiếp khách này chưa đủ điều kiện để viết tin tức
(chưa vào giai đoạn Sau tiếp khách, không yêu cầu tin tức,
hoặc bạn không phải Host/người tham gia).
```

Đây là lỗi UX vì frontend đang đoán nhiều nguyên nhân.

## 6.2 Mục tiêu

Cùng một:

```text
current user
+
visitInstanceId
```

phải nhận cùng eligibility verdict ở:

```text
Visit Process
Create News preset
POST create news
```

Frontend không viết lại business rule. Backend là source of truth.

## 6.3 Reason mapping

Đọc exact reason codes backend hiện tại và map sang VI/EN.

Nhóm cần bao phủ:

```text
Writing window chưa mở
Actor không có relation hợp lệ
Participant role không được viết
NewsNotRequired
Media consent không cho phép
Author đã có news cho instance
Visit instance không còn hợp lệ
```

Ví dụ message:

```text
Chuyến thăm chưa đến giai đoạn có thể viết tin.
Bạn không phải người được phép viết tin cho chuyến thăm này.
Vai trò tham gia của bạn không được phép tạo bài tin cho chuyến này.
Chuyến thăm này đã được xác nhận không cần bài tin tức.
Không thể tạo bài tin do khách không đồng ý sử dụng nội dung truyền thông.
Bạn đã có bài tin tức cho chuyến thăm này.
```

Dùng wording phù hợp với code/i18n hiện tại.

## 6.4 Không dùng message gom nhiều nguyên nhân

Xóa/fix logic kiểu:

```text
reason A hoặc B hoặc C
```

nếu backend đã trả reason code cụ thể.

## 6.5 Preset visitInstanceId

Scenario:

```text
/dashboard/news/create?visitInstanceId=3006
```

Nếu backend nói eligible:

```text
form phải load được preset 3006
```

Nếu backend nói không eligible:

```text
hiển thị đúng reason code
```

Không tự kết luận chỉ vì item không xuất hiện trong một local array nếu backend có endpoint verdict cụ thể.

Nếu eligible-query hiện chỉ trả list, reuse canonical backend response hiện có; không tự code business rule ở FE.

## 6.6 Existing article

Nếu user đã có bài:

```text
không tạo duplicate
```

Nếu product hiện có route edit/detail thì CTA có thể dẫn tới bài hiện tại. Không tạo flow mới không cần thiết.

## 6.7 Tests V16

Tối thiểu:

- Process `canCreate = true` → Create News preset cùng instance usable.
- Writing window denied → đúng message.
- News not required → đúng message.
- Media denied → đúng message.
- Wrong relation → đúng message.
- Existing article → không cho duplicate.
- Không duplicated eligibility logic ở frontend.

---

# 7. V14 — CHỐT SCOPE

Không triển khai thêm:

```text
INVITATION_DETAILS_UPDATED
```

email riêng.

Giữ solution hiện tại:

```text
Invitation email có thể chứa snapshot tại thời điểm gửi
Landing page đọc live/current DB
Email nói rõ người nhận cần xem dữ liệu mới nhất ở confirmation page
```

Không rotate token chỉ vì registrant sửa nội dung không liên quan identity.

---

# 8. V18 — FINAL STATIC / UNIT AUDIT CHO TRANSFER ATOMICITY

## 8.1 Expected architecture

Flow đúng:

```text
BEGIN TRANSACTION

validate actor
validate transfer window
validate current contact
validate target
validate no pending change

create VisitRequestIdentityChange TRANSFER PENDING
create identity event
create audit
mint token(s)
persist email_action_token(s)

COMMIT

dispatch email best-effort
```

## 8.2 Flow bị cấm

Không được còn:

```text
COMMIT transfer
↓
create token
↓
token creation fail
↓
TRANSFER vẫn PENDING nhưng không có usable link
```

## 8.3 SMTP semantics

SMTP/email provider failure xảy ra sau commit:

```text
không rollback transfer
không rollback action token
```

Invitation phải recover được qua resend.

## 8.4 Static audit

Check:

```text
BeginTransaction
SaveChanges
Commit
SendInvitation / dispatch
```

Đảm bảo token persistence ở trong transaction.

Nếu token service tự SaveChanges ngoài transaction bằng DbContext khác, phải kiểm tra lại thật kỹ. Không giả định “cùng method = cùng transaction”.

## 8.5 Unit/fake tests nếu có thể

Không cần MySQL thật.

Nếu architecture cho phép mock/fake:

### Token persistence failure

Expected:

```text
business transaction không được commit thành dangling pending transfer
```

### Email delivery failure

Expected:

```text
business state + token vẫn valid
```

### Duplicate initiate

Expected:

```text
one pending change
```

Nếu không thể unit-test transaction semantics vì EF/MySQL specifics:

```text
ghi rõ NOT VERIFIED WITHOUT DB
```

Không fake PASS.

---

# 9. AUDIT TOÀN BỘ WORKING TREE

Working tree được báo cáo:

```text
63 modified
8 new
```

Đây là thay đổi lớn. Phải audit trước commit.

## 9.1 Commands

```bash
git status --short
git diff --stat
git diff --check
git diff --name-only
```

## 9.2 Unrelated files

Tìm và báo:

```text
file ngoài V01–V18
editor settings
IDE files
temporary docs
generated outputs
local config
```

Không tự xóa file không rõ nguồn nếu có nguy cơ là thay đổi của người dùng. Báo riêng.

## 9.3 Generated files

Không commit:

```text
bin/
obj/
dist/
coverage/
TestResults/
.tmp-build/
```

hoặc generated artifact tương đương.

## 9.4 Secrets

Tìm:

```text
password
secret
api key
token raw
SMTP credential
Google client secret
DB password
private key
```

Không commit credential thật.

Example config được phép nếu chỉ placeholder/test-only safe value theo repo policy.

## 9.5 Debug code

Tìm:

```text
console.log
Console.WriteLine
debugger
TODO TEMP
HACK
temporary bypass
AllowAnonymous không có lý do
hardcoded user id
hardcoded campus id
hardcoded request id
```

Không xóa log production hợp lệ chỉ vì có từ `Console`; phải review context.

## 9.6 Authorization regression

Audit đặc biệt:

```text
VisitFormReadService
AllowedActions
list queries
history detail
contact invitation APIs
news APIs
visit stage APIs
```

Không có:

```text
STAFF → broad request access
Leader → bypass contact gate
pending invitee → full request detail
frontend role-only authorization thay backend
```

## 9.7 Test integrity

Không được:

```text
.Skip
skip:
xit
xdescribe
disable suite
comment assertion
weaken expected result
```

chỉ để tests xanh.

Nếu pre-existing skipped tests tồn tại trước task, không tự nhận là do task.

---

# 10. REQUIRED GATES

## 10.1 Backend builds

Chạy per-project genuine build. Không dùng grep che lỗi.

Tối thiểu:

```bash
dotnet build backend/PEMS.Domain/PEMS.Domain.csproj
dotnet build backend/PEMS.Application/PEMS.Application.csproj
dotnet build backend/PEMS.Infrastructure/PEMS.Infrastructure.csproj
dotnet build backend/PEMS.Api/PEMS.Api.csproj
```

Expected:

```text
0 compile errors
```

Warnings phải báo nếu mới phát sinh đáng kể.

## 10.2 Backend UnitTests

Chạy full unit suite hiện tại.

Baseline gần nhất được báo:

```text
2456/2456 PASS
```

Không assume số test phải y hệt nếu Agent bổ sung test.

Báo:

```text
passed
failed
skipped
total
```

## 10.3 Frontend typecheck

Chạy command package thực tế, ví dụ:

```bash
npm run typecheck
```

Expected PASS.

## 10.4 Frontend build

```bash
npm run build
```

Expected PASS.

## 10.5 Frontend unit

Baseline gần nhất:

```text
2193/2193
128 files
```

Sau bổ sung V09/V16 số lượng có thể tăng.

Expected:

```text
0 failed
```

## 10.6 Frontend lint

Nếu package.json có script:

```text
lint
```

thì chạy.

Nếu không có:

```text
NOT AVAILABLE — no lint script
```

Không invent command.

## 10.7 Git diff check

Bắt buộc:

```bash
git diff --check
```

Expected:

```text
PASS
```

Không whitespace errors.

---

# 11. GATES KHÔNG BẮT BUỘC

## 11.1 DB-backed IntegrationTests

Không chạy trong closure này.

Report:

```text
NOT RUN — waived by project owner
```

Không ghi PASS.

## 11.2 ArchitectureTests .NET 9

Không chạy trong closure này.

Report:

```text
NOT RUN — waived by project owner
```

Không tự đổi target framework chỉ để chạy test.

---

# 12. REAL-STACK / MANUAL SMOKE

Nếu local frontend/backend hiện đang chạy và có usable DB, nên smoke tối thiểu:

### V09

```text
login đúng invitee
→ mở My Operational Contact Invitations
→ Accept/Decline
```

### V16

```text
Process cho Create News
→ Create page load đúng preset/reason
```

### V13

```text
mở 2 campus cùng lúc
```

### V17

```text
Before Visit trước T-6
→ disabled / message đúng
```

### V12

```text
stale approve/reject
→ 409 + reload
```

Nếu không có environment:

```text
Real-stack: NOT RUN — environment unavailable
```

Không block closure lần này.

---

# 13. KHÔNG COMMIT NGAY

Sau khi code xong:

```bash
git status --short
git diff --stat
git diff --check
```

AI Agent phải báo cáo lại trước.

Không tự commit nếu Project Owner chưa yêu cầu.

---

# 14. BÁO CÁO BẮT BUỘC TRƯỚC COMMIT

Format:

```text
## Preflight

Branch:
HEAD:
Working tree before:
Working tree after:

## Remaining work completed

V09:
- root cause:
- files:
- behavior:
- tests:

V16:
- root cause:
- files:
- behavior:
- tests:

V14:
- final scope:

V18:
- transaction audit:
- token persistence location:
- email dispatch location:
- tests/static verification:

## Working-tree audit

Modified:
Added:
Untracked:
Unrelated files:
Generated files:
Secrets:
Debug code:
Hardcoded IDs:
Authorization concerns:
Tests disabled/skipped by this task:
git diff --check:

## Gates

Backend Domain build:
Backend Application build:
Backend Infrastructure build:
Backend Api build:
Backend UnitTests:

Frontend typecheck:
Frontend build:
Frontend UnitTests:
Frontend lint:

Integration DB-backed:
NOT RUN — waived by project owner

ArchitectureTests:
NOT RUN — waived by project owner

Real-stack:
PASS / NOT RUN + reason

## Remaining known issues

List every remaining issue.
If none in the agreed code scope:
NONE

## Git

HEAD:
Commits created:
NONE

Working tree count:
```

---

# 15. Điều kiện để Project Owner commit

Chỉ nên commit khi:

- [ ] V09 frontend hoàn chỉnh.
- [ ] V16 frontend reason mapping hoàn chỉnh.
- [ ] V14 giữ scope minimal đã chốt.
- [ ] V18 static transaction audit không còn partial-commit rõ ràng.
- [ ] Backend 4 project build PASS.
- [ ] Backend UnitTests PASS.
- [ ] Frontend typecheck PASS.
- [ ] Frontend build PASS.
- [ ] Frontend UnitTests PASS.
- [ ] Lint PASS nếu script tồn tại.
- [ ] `git diff --check` PASS.
- [ ] Không có secret.
- [ ] Không có generated file.
- [ ] Không có debug bypass.
- [ ] Không có unrelated code bị đưa vào vô lý.
- [ ] Không có test bị disable để làm xanh.
- [ ] Integration DB-backed được ghi đúng là waived.
- [ ] ArchitectureTests được ghi đúng là waived.

---

# 16. Commit strategy sau khi được Project Owner yêu cầu

Không bắt buộc một commit duy nhất.

Nếu working tree dễ tách an toàn, ưu tiên nhóm:

```text
1. fix(visit): visibility, edit rights and concurrency
2. fix(contact): invitation lifecycle and transfer atomicity
3. fix(history): revision baselines and identity history
4. fix(news): canonical visit-news eligibility and reason UX
5. fix(visit-process): T-6 lifecycle gate
6. fix(frontend): operational contact invitation UX and campus accordion
7. test: visit-v2 regression coverage
```

Nhưng:

- không split nếu việc split làm mất atomic code/test relation;
- không reorder bằng reset/rebase nguy hiểm;
- không amend lịch sử người khác.

Nếu Project Owner muốn 1 commit tổng:

```text
fix: close Visit V2 logic and workflow regressions
```

cũng chấp nhận được nếu diff đã được audit.

---

# 17. Sau khi commit/push

Project Owner sẽ:

```text
commit
push Duy-Iter1
```

Sau đó cung cấp:

```text
Commit SHA mới
```

Code review tiếp theo sẽ so sánh:

```text
930e79294c62ca1b1c082a04aa95bb4c327728cd
→
new SHA
```

---

# 18. Checklist review sau push

Reviewer sẽ kiểm tra:

- V01: STAFF list → đúng relation route, không generic-detail 403.
- V02: Registrant snapshot editable đúng; email/account identity vẫn immutable.
- V03: Pending-contact edit hoạt động.
- V04: Global contact gate đúng.
- V05: Không notify leader trước gate.
- V06: Cancel invitation semantics/state đúng.
- V07: Reinvite same-email sau cancel.
- V08: History identity mapping/detail.
- V09: Frontend invitation page + Accept/Decline.
- V10: Public Accept/Decline contact email action an toàn.
- V11: Pending wording.
- V12: Stale approve/reject guard.
- V13: Multi-expand campus accordion.
- V14: Landing page live-data semantics.
- V15: Revision baseline + comparison status.
- V16: Canonical News eligibility + reason mapping.
- V17: T-6 Before→During gate.
- V18: Transfer/token atomicity.

---

# 19. Review security sau push

Đặc biệt kiểm tra:

```text
No broad STAFF visibility
No Leader pre-gate review
No pending invitee full-request visibility
No public GET mutation
No raw token persistence
No token leak to history
No duplicate transfer
No partial transfer/token commit
No automatic stale approve retry
No frontend-only business authorization
```

---

# 20. Review DB/schema sau push

Không yêu cầu live MySQL trong closure hiện tại.

Nhưng code/canonical SQL phải nhất quán với:

```text
VISIT_CONTACT_CLAIM
VISIT_CONTACT_TRANSFER
VISIT_REQUEST_IDENTITY_CHANGE
ACCEPT
DECLINE
revision source MIGRATION nếu đang reuse
```

Không tạo migration/schema mới nếu không thật sự cần.

Runtime DB sẽ được xác minh riêng khi có environment.

---

# 21. Definition of Done của closure này

Closure code được coi là đủ để Project Owner commit khi:

```text
V09 = DONE
V16 = DONE
V14 = intentionally minimal / DONE
V18 = statically consistent
Required builds/tests = PASS
Working-tree audit = clean
git diff --check = PASS
No known in-scope code defect
```

Hai gate sau **không thuộc DoD của closure này** theo quyết định Project Owner:

```text
DB-backed IntegrationTests
ArchitectureTests .NET 9
```

---

# 22. Không được báo sai trạng thái

Không dùng:

```text
FINAL CLOSURE COMPLETE
ALL TESTS GREEN
ALL GATES PASS
```

nếu hai waived gate chưa chạy.

Có thể báo:

```text
CODE CLOSURE COMPLETE FOR AGREED SCOPE

Required gates: PASS
Integration DB-backed: NOT RUN — waived by project owner
ArchitectureTests: NOT RUN — waived by project owner
```

chỉ khi V09/V16 và required gates đã hoàn thành.

---

# 23. Kết quả cuối cùng mong đợi

AI Agent trả về một working tree trên:

```text
Duy-Iter1
```

với:

- V01–V18 hoàn chỉnh trong scope đã chốt;
- V09 frontend đã có;
- V16 frontend reason mapping đã có;
- V14 không mở rộng ngoài yêu cầu;
- V18 không còn partial-commit rõ ràng;
- backend build + unit xanh;
- frontend typecheck/build/unit xanh;
- diff audit sạch;
- chưa commit nếu Project Owner chưa yêu cầu.

Sau đó Project Owner sẽ commit/push và gửi SHA để review trước khi merge vào `Dev`.
