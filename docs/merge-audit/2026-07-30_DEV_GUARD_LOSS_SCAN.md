---
type: merge-audit
feature: dev-into-canh-iter1
status: final
updated: 2026-07-30
links:
  - docs/merge-audit/2026-07-28_P0_HANDLER_REVIEW.md
  - docs/merge-audit/2026-07-29_LATEST_DEV_DELTA_REVIEW.md
---

# Dev guard-loss scan — Cảnh-Iter1 integration

> Rà soát toàn bộ file production Dev đã sửa kể từ merge base, tìm construct **có trên Dev nhưng mất tại
> integration HEAD**. Chạy sau khi human review phát hiện C-1.

## 0. Vì sao cần bản rà này

C-1 không phải lỗi biên dịch, không phải test đỏ, không phải xung đột merge. Nó là **một guard biến mất
lặng lẽ**: `AssignDepartmentStaffCommandHandler` mất transaction + user lock mà Dev vừa thêm, trong khi
XML doc của chính class đó vẫn tiếp tục mô tả cả hai.

Nó lọt qua: 1693 unit test, 945 integration test, 56 real-stack journey, CI 5/5 xanh, và một bản audit P0
do chính agent viết đã đánh **PASS** cho handler này. Người phát hiện là **human reviewer đọc diff**.

Bài học: không bộ test nào hỏi "đoạn này có nguyên tử không" thì không bộ test nào trả lời. Vì vậy bản rà
này quét **theo họ construct**, không dựa vào build/test xanh.

## 1. Phạm vi

```
merge base : 06c73b9491b7fb5afb88d20fc64de5ed9a56500c
so sánh    : merge-base  ↔  origin/Dev  ↔  integration HEAD
phạm vi    : backend/**/*.cs, frontend/**/*.ts, frontend/**/*.tsx
file quét  : 173 file production Dev đã sửa kể từ merge base
```

Script: `scripts` tạm trong scratchpad; logic = đếm từng họ construct trên 3 revision, mọi **giảm** đều
phải mở file đọc tay và phân loại.

## 2. Họ construct đã quét

| Họ | Mẫu |
|---|---|
| Transaction | `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`, `TransactionScope`, `ExecuteInTransaction` |
| Concurrency/locking | `IUserMutationLockService`, `LockUsersAsync`, `LockDepartmentsAsync`, `FOR UPDATE`, `RowVersion`, `ConcurrencyToken` |
| Authorization/scope | `ForbiddenException`, `AuthBusinessException`, `_currentUser.RoleCode/SubRole/DepartmentId/PrimaryCampusId`, `ScopeService`, `AllowedActions` |
| Dependency/invariant | `DependencyChecker`, `DependencyRule`, `Blockers`, `ConflictException`, `NotFoundException`, `ValidationException` |
| Security/email/file | `EmailRecipientPolicyEnforcer`, `SensitiveEmailVariables`, `IFileAccessAuthorizationService`, `.Hash(`, `TokenHash`, `AssertSafeToImport` |

## 3. Kết quả tổng

**5 lượt giảm trên 3 file.** Sau khi đọc tay: **1 regression thật** (C-1), **4 lượt là cùng MỘT thay đổi
có chủ đích** (validation email-override chuyển vào type dùng chung).

| # | File | Họ | Base | Dev | HEAD | Phân loại |
|---|---|---|---|---|---|---|
| 1 | `AssignDepartmentStaffCommandHandler.cs` | transaction | 0 | 2 | 0 | **REGRESSION (C-1)** |
| 2 | `AssignDepartmentStaffCommandHandler.cs` | locking | 0 | 4 | 1 | **REGRESSION (C-1)** |
| 3 | `AssignDepartmentStaffCommandHandler.cs` | dependency | 9 | 9 | 5 | Intentionally superseded |
| 4 | `InviteVisitParticipantCommandHandler.cs` | dependency | 19 | 20 | 15 | Intentionally superseded |
| 5 | `PrepareVisitLogisticsCommandHandler.cs` | dependency | 14 | 14 | 9 | Intentionally superseded |

Authorization: **0 giảm.** Security/email/file: **0 giảm.** Không file nào Dev sửa bị mất khỏi HEAD.

## 4. Chi tiết từng hit

### Hit 1 + 2 — REGRESSION (C-1)

- **File:** `backend/PEMS.Application/Delegations/Commands/AssignDepartmentStaff/AssignDepartmentStaffCommandHandler.cs`
- **Construct:** `BeginTransactionAsync` + `CommitAsync`; `IUserMutationLockService` + `LockUsersAsync`
- **Merge base:** 0 — chưa có
- **Dev:** có đủ (`_lockService` field, ctor injection, transaction :65, lock :66, commit :243)
- **HEAD:** không có gì ngoài `<see cref="IUserMutationLockService"/>` **trong comment**
- **Reason for difference:** Dev THÊM guard sau điểm rẽ nhánh; bản hợp nhất lấy nhánh Cảnh cho file này và
  guard mới của Dev không được mang theo. Không phải Cảnh chủ động bỏ — merge base = 0 chứng minh guard
  chưa từng tồn tại ở nhánh Cảnh để mà bỏ.
- **Decision:** **Khôi phục.** Lấy vị trí transaction/lock/commit từ Dev, giữ toàn bộ email dispatcher,
  template, attachment, status guard của bản integration.
- **Evidence:** `git show origin/Dev:<path> | grep -n` vs `grep -n` tại HEAD (đếm 3 vs 0).
- **Test:** `AssignDepartmentStaffAtomicityTests` (3 test, MySQL thật) + 2 unit test lock.
  **Đã xác minh ĐỎ** khi gỡ lại transaction/lock: rollback FAIL, concurrency FAIL.
- **Owner confirmation required:** Không — khôi phục nguyên trạng ý định của Dev.

### Hit 3, 4, 5 — Intentionally superseded (cùng một thay đổi)

- **Files:** `AssignDepartmentStaff`, `InviteVisitParticipant`, `PrepareVisitLogistics` command handlers
- **Construct:** 5 `ValidationException` cho email-override, giống hệt nhau ở cả 3 file:
  tiêu đề rỗng · tiêu đề quá dài · nội dung rỗng · nội dung quá dài · nội dung rỗng sau khi lọc
- **Reason for difference:** chuẩn hoá email của Cảnh chuyển đúng 5 phép kiểm tra này vào
  `SystemEmailContent.AuthoredByUser.Create` — factory DUY NHẤT dựng được kiểu đó, nên handler không thể
  bỏ qua. Xác minh bằng cách tìm nguyên văn chuỗi thông báo:
  `backend/PEMS.Application/Emails/Common/SystemEmailContent.cs:76,79,88,91,105`.
- **Decision:** Giữ nguyên. **Không khôi phục** — khôi phục sẽ nhân bản luật ở 3 nơi, đúng thứ mà việc
  gom vào một type nhằm loại bỏ.
- **Bằng chứng bảo toàn:** luật không chỉ còn nguyên mà còn **mạnh hơn** — mỗi lỗi nay kèm stable error
  code (`EmailErrorCodes.AuthoredSubjectRequired`, `AuthoredBodyRequired`…), bản Dev không có.
- **Đối chiếu guard nghiệp vụ:** mọi guard nghiệp vụ ánh xạ 1:1 Dev→HEAD. Ví dụ `InviteVisitParticipant`:
  D68→H81, D76→H89, D80→H93, D82→H95, D97→H110, D104→H117, D154→H162, D374→H400, D393→H419, D403→H429,
  D413→H439, D420→H446, D424→H450, D427→H453, D429→H455, D448→H474, D453→H479. Không mất cái nào.
- **Riêng `AssignDepartmentStaff`:** HEAD còn **thêm** một guard Dev không có — kiểm tra
  `targetStaff.Status != UserStatuses.Active`. Guard nghiệp vụ: Dev 7 → HEAD 8.
- **Test:** validation đã có test tại tầng `SystemEmailContent`; 3 handler có unit test riêng cho nhánh
  authored-content.
- **Owner confirmation required:** Không.

## 5. Hạn chế của bản rà này

Nói rõ để người sau không tin quá mức:

1. **Quét theo mẫu văn bản.** Một guard bị đổi tên hoặc viết lại bằng cấu trúc khác sẽ không hiện thành
   "giảm". Bản rà bắt được C-1 vì guard *biến mất*, không phải vì hiểu ngữ nghĩa.
2. **Chỉ so số lượng.** Guard bị **đảo thứ tự** (vd lock đặt SAU eligibility check) giữ nguyên số đếm và
   sẽ lọt. Đây chính là lý do phải có `AssignDepartmentStaffAtomicityTests` — thứ tự chỉ chứng minh được
   bằng test chạy thật, không bằng đếm.
3. **Chỉ quét file Dev đã sửa.** Guard mất ở file chỉ nhánh Cảnh động tới không nằm trong phạm vi (xem
   M-1 dưới đây, phát hiện bằng lượt quét rộng hơn).
4. **Không chứng minh guard còn ĐÚNG**, chỉ chứng minh còn TỒN TẠI.

## 6. M-1 — `SendEmailDraftCommandHandler` (ngoài phạm vi C-1)

Lượt quét rộng phát hiện file này cũng giảm transaction. Truy vết lịch sử:

| Revision | `BeginTransaction`/`Commit` |
|---|---|
| merge base `06c73b94` | 2 |
| `origin/Dev` `1a0f9c53` | 2 (Dev không đụng file này) |
| Cảnh `52d666bb` | 0 |
| HEAD | 0 |

**Không phải merge regression** — Dev không sửa file, Cảnh chủ động viết lại trong `54441654`
("standardize core email infrastructure").

**Cơ chế thay thế có bảo đảm không?** Có, và mạnh hơn cho đúng bài toán này:

- Bản Dev tự dựng row `sent_emails` trong transaction rồi cập nhật draft → commit.
- Bản hiện tại `TryClaimAsync` **giành draft trước khi gửi** (compare-and-set sang SENT); chỉ một request
  đi tiếp được. Sau đó `ManualEmailSender.SendAsync` ghi lịch sử; cuối cùng một `SaveChangesAsync` chỉ để
  **liên kết ngược** `sent_email_id`.

Transaction không thể làm việc gửi SMTP trở nên rollback-được — email đã bay là đã bay. Claim-trước-gửi
mới là primitive đúng cho ngữ nghĩa "gửi đúng một lần". Các kịch bản hỏng:

- Chết sau claim, trước khi gửi → draft SENT, không có email. **Fail-closed**, không gửi trùng.
- Chết sau khi gửi, trước liên kết ngược → email đã gửi và đã ghi nhận, draft SENT nhưng `sent_email_id`
  còn NULL. Thiệt hại: **truy vết**, không phải gửi trùng hay mất email.

**Verdict: INTENTIONAL + SAFE.** Nợ nhỏ còn lại: khoảng trống `sent_email_id` NULL sau sự cố. Ghi nhận,
không chặn merge. Đề nghị owner xác nhận đây là đánh đổi có chủ ý.

## 7. Kết luận

| Hạng mục | Kết quả |
|---|---|
| File production Dev đã sửa, đã quét | 173 |
| Transaction giảm | 1 (C-1 — đã sửa) |
| Lock giảm | 1 (C-1 — đã sửa) |
| Authorization giảm | 0 |
| Dependency-check giảm | 3 (đều là cùng một thay đổi có chủ ý) |
| Security/email/file giảm | 0 |
| File Dev sửa bị mất khỏi HEAD | 0 |
| **Regression thật** | **1, đã khôi phục kèm test** |
| Chờ owner xác nhận | 1 (M-1, không chặn) |

Bản rà này **không** kết luận "sạch vì build/test xanh" — C-1 đã chứng minh điều đó vô nghĩa. Kết luận
dựa trên đối chiếu 3 revision theo từng họ construct, và mọi lượt giảm đều đã mở file đọc tay.
