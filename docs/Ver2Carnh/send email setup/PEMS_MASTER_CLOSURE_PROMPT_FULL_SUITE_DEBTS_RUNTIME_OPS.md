# PEMS — MASTER PROMPT ĐÓNG FULL-SUITE, NỢ NGHIỆP VỤ, NỢ TEST VÀ XÁC MINH VẬN HÀNH

## Vai trò

Bạn là Senior Full-stack Engineer phụ trách đóng các khoản nợ còn lại của PEMS sau đợt sửa email.

Phải làm việc trên source code và trạng thái Git thực tế đang checkout. Không giả định số lượng test, commit, tên branch local hoặc working tree vẫn giống báo cáo cũ.

Không dừng ở việc lập kế hoạch. Hãy điều tra root cause, triển khai fix trong phạm vi được phép, chạy gate và báo cáo evidence.

---

# 1. Mục tiêu tổng

Hoàn thành các nhóm sau:

1. Đóng **4 lỗi full-suite còn lại**:
   - 1 backend unit test ở visit photo folders.
   - 2 frontend test ở `logisticsDescription`.
   - 1 frontend test ở `operationalContactQuickFill`.

2. Chốt và triển khai nghiệp vụ reminder có **0 người nhận hợp lệ**.

3. Điều tra dứt điểm các dòng `documents` bị sót:
   - dữ liệu hợp lệ;
   - orphan do test;
   - hay production có thể tạo orphan.

4. Làm sạch test file/download:
   - bỏ cleanup viết tay;
   - dùng helper chung;
   - tách vùng ID fixture đang đụng nhau.

5. Chạy runtime smoke các luồng email đã sửa với outbound tắt.

6. Xác minh Google Drive trên môi trường dev/test an toàn nếu có credential phù hợp.

7. Điều tra các mục vận hành:
   - hai Vercel deployment bị blocked;
   - local `Canh-Iter1` khác remote `Cảnh-Iter1`;
   - nhánh merge tạm chưa xóa;
   - giữ nguyên WIP và stash.

---

# 2. Nguồn sự thật và các quyết định đã chốt

## 2.1 Reminder không còn người nhận

Prompt này coi quyết định sau là **đã được duyệt**:

```text
Khi reminder đến giờ nhưng không còn Host/participant/email hợp lệ:

status      = CANCELLED
reasonCode  = NO_ELIGIBLE_RECIPIENTS
send        = không
retry       = không
sent email history = không tạo sent_emails vì không có send attempt
```

Hệ thống phải giữ evidence tại reminder/schedule/execution record hiện có:

```text
status
reasonCode hoặc trường lỗi tương đương
processedAt/cancelledAt
message dễ hiểu cho UI/log
```

Không được tạo một email history giả với trạng thái SENT/FAILED khi provider chưa từng được gọi.

Nếu schema hiện tại không có chỗ lưu lý do:

- Không tự ý đổi schema.
- Dừng trước migration.
- Báo chính xác entity/table hiện tại thiếu gì và đề xuất thay đổi tối thiểu.
- Chỉ tiếp tục đổi schema khi được duyệt.

## 2.2 Bốn test đỏ

Không được làm test xanh giả bằng:

```text
skip
todo
return sớm
bỏ assertion
tăng timeout vô lý
mock mất luồng cần kiểm tra
làm yếu production behavior
```

## 2.3 Git safety

Phải giữ nguyên mọi commit đã tồn tại trước task, đặc biệt các SHA được nhắc trong tài liệu:

```text
13221badd1bc82fbba0529b4071d191979b1a512
37f97b0406e71f5a5f1c51316bd389c8a2d403e4
```

Ngoài ra, nếu HEAD hiện đã có thêm các commit email mới hơn, tất cả cũng phải được giữ nguyên.

Không:

```text
amend
reset
rebase
squash
force push
đụng stash
xóa WIP
```

Không yêu cầu working tree sạch nếu đang có WIP hợp lệ. Thay vào đó:

- ghi hash/diff của WIP trước khi sửa;
- chỉ stage file thuộc task;
- chứng minh WIP ngoài scope không đổi.

---

# 3. G0 — Preflight

Ghi nhận:

```text
branch local
HEAD
origin/Cảnh-Iter1
origin/Canh-Iter1 nếu tồn tại
ahead/behind
git status --short
git diff --stat
stash count
danh sách nhánh merge tạm liên quan
backend/frontend process đang chạy
database hiện tại
Smtp.Enabled
Drive config presence, không in secret
```

Lưu checksum hoặc `git diff --numstat` cho các file WIP cần giữ.

Không checkout branch khác, rename branch, xóa branch hoặc clean working tree tại bước này.

Chạy baseline riêng từng lỗi đang đỏ và lưu:

```text
test name đầy đủ
expected
actual
stack trace
file:line
DOM/state nếu là frontend
```

Không dựa vào số lượng test lịch sử như `2180/2180` hoặc `1444/1444`.

Mục tiêu là:

```text
discovered total / discovered total
0 failed
```

---

# 4. G1 — Backend visit photo folders

## 4.1 Lỗi cần đóng

```text
GetMyVisitPhotoFoldersQueryHandlerTests.SearchFiltersOnResolvedDelegationName
```

Phải chạy riêng toàn bộ class:

```bash
dotnet test tests/PEMS.UnitTests \
  --filter "FullyQualifiedName~GetMyVisitPhotoFoldersQueryHandlerTests"
```

## 4.2 Root cause cần kiểm chứng

Production handler tìm kiếm theo:

```text
resolved delegation name
folder name
PhotoFaceTag.DisplayName
PhotoFaceTag.PersonNameKey
guest full name
guest organization
```

Test DbContext hiện có khả năng ignore hoặc thiếu model:

```text
PhotoFaceTag
VisitGuestMember
VisitInstanceGuestMember
```

Đây phải được xác minh từ code hiện tại, không mặc định tin báo cáo cũ.

## 4.3 Hướng sửa ưu tiên

Nếu đúng là test harness thiếu model:

- map đầy đủ tối thiểu các entity mà production query dùng;
- thêm `DbSet` cần thiết;
- gỡ đúng `Ignore<T>()`;
- cấu hình key/relationship theo production DbContext;
- dùng SQLite in-memory nếu EF InMemory không biểu diễn đúng `LIKE`, relationship hoặc translation.

Không sửa handler bằng cách né query:

```text
catch InvalidOperationException
bỏ face tag search
bỏ guest search
return sớm khi delegation name match
chỉ chạy nhánh phụ trong một số dataset
```

## 4.4 Regression tests bắt buộc

Giữ hoặc thêm test:

```text
Search resolved delegation name
Search folder name
Search PhotoFaceTag.DisplayName
Search PhotoFaceTag.PersonNameKey
Search guest full name
Search guest organization
No match → empty
Không lộ instance ngoài scope
```

Phải chứng minh targeted class xanh do model test đúng, không phải behavior production bị thu hẹp.

---

# 5. G2 — `logisticsDescription`

Chạy:

```bash
npm run test -- \
  src/features/delegations/__tests__/logisticsDescription.test.tsx \
  --reporter=verbose
```

Báo chính xác hai test đỏ.

## Contract phải giữ

```text
newline được giữ trong cùng text node
CSS dùng whitespace-pre-wrap
chuỗi dài dùng break-words
không dangerouslySetInnerHTML
HTML nguy hiểm hiển thị như text
không fallback title
không dùng coordination note thay description
empty/loading state giữ nguyên
```

Hướng sửa ưu tiên:

```tsx
<p className="whitespace-pre-wrap break-words ...">
  {resolved.text}
</p>
```

Không:

```text
split theo \n thành nhiều <p>
sinh <br> thủ công
parse markdown ngầm
render HTML nghiệp vụ
```

Acceptance:

- newline còn nguyên;
- chuỗi 400 ký tự không làm rộng modal;
- `<script>` và `<img onerror>` không trở thành node thực thi;
- component comment, test và UX cùng một contract.

---

# 6. G3 — `operationalContactQuickFill`

Chạy:

```bash
npm run test -- \
  src/features/visit-request/__tests__/operationalContactQuickFill.test.tsx \
  --reporter=verbose
```

Báo:

```text
test name
expected
actual
DOM/state lúc fail
timer/promise pending nếu có
```

Không kết luận timeout nếu chưa chứng minh.

## Contract nghiệp vụ

Quick-fill là **copy một lần**, không liên kết hai chiều.

Copy đúng bốn trường:

```text
fullName
organization
phone
email
```

Yêu cầu:

- chỉ campus được bấm thay đổi;
- không ghi đè dữ liệu đã nhập khi chưa xác nhận;
- source đổi sau copy không cập nhật đích;
- đích đổi không cập nhật source;
- autosave/restore giữ dữ liệu copy;
- organization vẫn searchable/free-solo;
- button chỉ enabled khi source đủ điều kiện;
- confirmation state không dùng chung giữa campus.

## Điều tra production

Kiểm tra:

```text
watch() có state mới không
setValue có shouldDirty/shouldTouch
combobox organization có nhận form value
campus index/key có ổn định sau add/remove
autosave serialize đủ bốn field
restore hydrate organization
hidden/mobile duplicate control
debounce 700ms và pending promise
localStorage cleanup giữa tests
timer reset
```

Chỉ sửa test nếu production behavior đã đúng và test không deterministic.

Được phép:

```text
waitFor state async thật
query control visible chính xác
fake timer có kiểm soát
flush debounce/autosave promise
reset storage/timers
```

Không được:

```text
tăng timeout toàn suite
bỏ assertion
mock bỏ draft flow
chuyển test UI thành test function nội bộ
```

Chạy full frontend ít nhất hai lần nếu root cause liên quan timing/order.

---

# 7. G4 — Reminder không có người nhận

## 7.1 Audit flow hiện tại

Tìm:

```text
reminder scheduler/hosted service
recipient resolver
reminder status transition
retry logic
sent email history writer
UI/query hiển thị reminder status
```

Lập flow hiện tại:

```text
PENDING
→ đến hạn
→ resolve Host/participants/email
→ dispatch
→ status cuối
```

## 7.2 Behavior cần implement

Khi danh sách người nhận sau validation bằng 0:

```text
Không gọi email provider
Không gọi dispatcher send
Không tạo sent_emails
Không retry
Reminder → CANCELLED
Reason → NO_ELIGIBLE_RECIPIENTS
Ghi processed/cancelled timestamp
Log structured ở mức Information/Warning phù hợp
```

Message VI:

```text
Đã hủy nhắc lịch vì không còn người nhận đủ điều kiện.
```

Message EN nếu UI có localization:

```text
The reminder was cancelled because no eligible recipients remained.
```

## 7.3 Concurrency/idempotency

Hai worker không được xử lý cùng reminder hai lần.

Nếu reminder đã `CANCELLED`, chạy lại không:

```text
gửi mail
tạo history
đổi reason
```

## 7.4 Tests

Thêm test:

```text
No Host
Host không có email hợp lệ
Participant không còn ACCEPTED
Tất cả recipient bị loại do invalid/duplicate/policy
0 recipient → CANCELLED + reason
0 recipient → không provider call
0 recipient → không sent_emails
0 recipient → không retry
worker chạy lại → idempotent
có ít nhất 1 recipient → flow gửi bình thường không đổi
```

Nếu không có trường lưu reason mà cần schema change, dừng theo §2.1.

---

# 8. G5 — Điều tra `documents` bị sót

Không xóa dữ liệu trước khi có root cause.

## 8.1 Phân loại

Lập query/evidence để chia các row bị sót thành:

```text
A. Document hợp lệ, owner còn tồn tại
B. Orphan do test cleanup thiếu
C. Orphan có thể do production transaction/flow
D. Row legacy có business meaning cần giữ
```

Đối chiếu:

```text
owner_type
owner_id
file_id
document_category
created_at
created_by
source flow/test
FK hiện có
soft-delete semantics
```

## 8.2 Reproduce

- Chạy suite nghi ngờ riêng.
- Snapshot row `documents` trước/sau.
- Chạy suite hai lần.
- Chạy song song nếu CI cho phép.
- Xác định test hoặc production command tạo row.

## 8.3 Quyết định sửa

Nếu chỉ là test leakage:

```text
Sửa fixture/cleanup helper
Không sửa production
Không xóa dữ liệu thật
```

Nếu production có thể tạo orphan:

```text
Chứng minh flow cụ thể
Sửa transaction/order/compensation tối thiểu
Thêm regression test
```

Nếu cần cleanup dữ liệu hiện có:

- tạo script idempotent, scoped rõ;
- có preflight count;
- có backup/rollback;
- không chạy vào `pems_db` nếu chưa được duyệt;
- không thêm vào canonical SQL nếu đây không phải dữ liệu canonical.

Báo riêng:

```text
root cause
affected rows
production risk
cleanup decision
```

---

# 9. G6 — Làm sạch test file/download

## 9.1 Cleanup helper chung

Hai suite:

```text
FileDownloadAuthorizationTests
FileDownloadRouteTests
```

không được tiếp tục cleanup viết tay nếu helper chung đã tồn tại.

Chuyển sang helper chuẩn hiện tại, bảo đảm cleanup:

```text
email/file links nếu có
documents
file ownership/reference rows
files
các dependent rows theo FK
```

Không tạo helper thứ hai nếu `FixtureCleanup` hoặc helper tương đương đã có.

## 9.2 Tách vùng ID

Audit registry/base ID của:

```text
FileDownloadRouteTests
FilePreviewDownloadTests
```

Chọn hai vùng:

```text
không overlap
không đụng suite khác
được ghi comment/constant rõ
```

Không hard-code vùng mới trước khi search toàn test project.

## 9.3 Tests

- Chạy từng suite riêng.
- Chạy cả hai cùng filter.
- Chạy song song nếu runner hỗ trợ.
- Chạy hai lần liên tiếp.
- Snapshot `documents/files` trước/sau phải không tăng ngoài expected persistent fixtures.

---

# 10. G7 — Runtime smoke email

Chạy app với:

```text
Smtp__Enabled=false
file sink hoặc provider fake
database dev/test, không phải production
```

Chạy tối thiểu 13 ca:

```text
1. Account confirmation
2. Password reset OTP
3. Visit request OTP
4. Participant invitation
5. Logistics email
6. Reminder email có recipient
7. Reminder email 0 recipient
8. Manual compose không attachment
9. Manual compose có attachment
10. Reply
11. Setup-progress VI
12. Setup-progress EN + đồng bộ mới nhất
13. Draft reopen/autosave/discard/404/409 + double send
```

Với mỗi ca ghi:

```text
HTTP status
DB row/status
recipient groups
attachment count
file sink artifact
no unresolved placeholder
no real outbound
```

Case bắt buộc:

```text
Draft không tồn tại → composer fail-closed
Attachment mất → draft còn DRAFT, không provider call
OTP 429 → message VI + countdown theo server
```

Không tự kết luận PASS nếu thiếu credential/account để chạy.

---

# 11. G8 — Google Drive dev/test verification

Chỉ chạy khi có:

```text
credential dev/test
folder test riêng
quyền xóa file test
```

Không in secret.

Flow:

```text
Upload file nhỏ
GET metadata
Download
So sánh hash/bytes
Delete
Xác nhận file không còn
```

Xác minh mapping:

```text
CONFIG_MISSING
TOKEN_EXPIRED
AUTH_FAILED
UNAVAILABLE
FOLDER_NOT_FOUND_OR_NO_PERMISSION
UPLOAD_FAILED
```

Không cố tình làm hỏng credential dùng chung của team.

Nếu không có credential phù hợp:

```text
Status = BLOCKED
Lý do cụ thể
Automated stub tests đã chạy
Không ghi PASS
```

---

# 12. G9 — Vận hành Git/Vercel

## 12.1 Vercel deployment blocked

Điều tra hai deployment:

```text
commit SHA
environment
blocked reason
GitHub/Vercel check log
do config, permission, ignored build, deployment protection hay code/build
```

Nếu code/build:

- reproduce local;
- sửa trong scope riêng;
- chạy FE build.

Nếu config/permission:

- không sửa code giả;
- ghi exact setting cần thay đổi;
- không redeploy production khi chưa được duyệt.

## 12.2 Branch local/remote

Kiểm tra:

```text
refs/heads/Canh-Iter1
refs/remotes/origin/Cảnh-Iter1
upstream config
SHA của hai ref
```

Không rename/delete tự động.

Nếu cùng SHA và chỉ khác tên:

- đề xuất canonical branch;
- đưa lệnh an toàn;
- chờ duyệt trước khi đổi ref.

Nếu khác SHA:

- báo ahead/behind và unique commits;
- không merge/rebase trong task này nếu chưa được yêu cầu.

## 12.3 Nhánh merge tạm

Chỉ đề xuất xóa khi:

```text
PR đã merged/closed
branch không có unique commit cần giữ
commit đã reachable từ canonical branch
```

Không xóa local/remote nếu chưa được duyệt.

## 12.4 WIP

Hai file WIP hoặc mọi file WIP hiện có phải giữ byte-identical nếu ngoài scope.

---

# 13. Gate cuối

## 13.1 Targeted

```bash
dotnet test tests/PEMS.UnitTests \
  --filter "FullyQualifiedName~GetMyVisitPhotoFoldersQueryHandlerTests"

npm run test -- \
  src/features/delegations/__tests__/logisticsDescription.test.tsx

npm run test -- \
  src/features/visit-request/__tests__/operationalContactQuickFill.test.tsx
```

Chạy targeted reminder, file/download và documents tests tương ứng.

## 13.2 Full

```bash
dotnet build PEMS.slnx
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.ArchitectureTests
dotnet test tests/PEMS.IntegrationTests

npm run typecheck
npm run build
npm run test

git diff --check
```

Full frontend chạy ít nhất hai lần nếu từng có timing issue.

Mục tiêu:

```text
100% discovered tests passed
0 failed
```

Không dùng số lượng test cũ làm điều kiện cứng.

## 13.3 Regression email/database

Chạy lại tối thiểu:

```text
backend unit ~Emails
backend integration ~Emails
backend integration ~VisitRequests
backend integration ~SetupProgress
EmailTemplateSyncScriptTests
frontend features/emails
canonical SQL verification/hash gate
```

Không để task closure phá các commit email trước.

---

# 14. Commit strategy

Không amend commit cũ.

Ưu tiên tách:

```text
fix(test): close visit photo and frontend regression failures
fix(reminders): cancel schedules with no eligible recipients
test(files): unify cleanup and isolate fixture ranges
fix(documents): prevent orphan document rows
```

Chỉ tạo `fix(documents)` nếu production bug được chứng minh. Nếu chỉ test leakage, gộp vào `test(files)` hoặc commit test riêng.

Các thay đổi docs/ops có thể dùng:

```text
docs(ops): record deployment and branch verification
```

Không push.

Chỉ stage file thuộc từng commit. Sau mỗi commit kiểm tra WIP ngoài scope vẫn nguyên.

---

# 15. Definition of Done

Chỉ báo `DONE` khi:

```text
[ ] Backend unit full suite 100% xanh.
[ ] Frontend unit full suite 100% xanh.
[ ] Bốn test đỏ được sửa bằng root cause thật.
[ ] Reminder 0 recipient có behavior CANCELLED + NO_ELIGIBLE_RECIPIENTS.
[ ] Reminder 0 recipient không send, không retry, không sent_emails.
[ ] Documents leakage được phân loại bằng evidence.
[ ] Không còn cleanup viết tay ở hai file test đã nêu.
[ ] Fixture ID ranges không overlap.
[ ] Backend build/architecture/integration xanh.
[ ] Frontend typecheck/build xanh.
[ ] Email/database regression xanh.
[ ] Runtime smoke đã chạy hoặc ghi BLOCKED chính xác.
[ ] Drive thật đã verify hoặc ghi BLOCKED chính xác.
[ ] Vercel blocked được phân loại code/config.
[ ] Branch mismatch và merge branch được báo rõ, không tự ý đổi/xóa.
[ ] WIP và stash giữ nguyên.
[ ] Không đổi schema nếu chưa được duyệt.
[ ] Không gửi email thật.
[ ] Không push.
```

Không báo DONE nếu:

```text
targeted xanh nhưng full suite đỏ
runtime/Drive chưa chạy mà ghi PASS
documents chưa có root cause nhưng đã xóa
reminder 0 recipient vẫn retry hoặc tạo sent history
```

---

# 16. Báo cáo cuối

```text
ROOT CAUSE
- Backend photo search
- Logistics failure 1
- Logistics failure 2
- Operational quick-fill
- Reminder 0 recipient
- Documents leakage
- File cleanup/ID collision
- Vercel/Git operations

FILES CHANGED
- file
- production/test/docs
- reason

BEFORE / AFTER
- test counts
- reminder state
- documents rows
- file fixture isolation
- runtime UI behavior

GATES
- backend build
- backend unit
- architecture
- integration
- frontend targeted
- frontend full run 1
- frontend full run 2
- typecheck
- frontend build
- email/database regression
- git diff --check

RUNTIME
- 13 email cases
- Google Drive test
- outbound safety

GIT SAFETY
- HEAD before
- HEAD after
- commits created
- status
- stash count
- WIP hashes
- ahead/behind
- not pushed

BLOCKED ITEMS
- exact missing credential/access/approval
- không được ghi PASS

REMAINING DEBT
- chỉ liệt kê việc có evidence chưa thể đóng
```
