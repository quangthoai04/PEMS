# PEMS – Kế hoạch fix các bug Invitation / Contribution / Transfer Host

> Phạm vi: các bug đã phát hiện trong quá trình audit code hiện tại trên branch `Dev`, tập trung vào:
> - Invitation / participant state
> - Department Leader / Department Staff contribution
> - Transfer Host
> - Visibility ở các list
> - Authorization / state consistency
>
> Mục tiêu: sửa từ **nguồn state + authorization**, không vá UI để che lỗi.

---

# 1. Tổng hợp bug cần fix

## BUG-01 — Transfer Host làm Host cũ thành `IC_SUPPORT + ASSIGNED`

### Hiện trạng

Khi Host A được Staff Leader chuyển sang Host B:

```text
Trước transfer

A:
IsHost = true
ParticipantRole = IC_HOST
Status = ASSIGNED
```

Code hiện tại chỉ đổi:

```text
IsHost = false
ParticipantRole = IC_SUPPORT
```

nhưng giữ nguyên:

```text
Status = ASSIGNED
```

Kết quả:

```text
A:
IC_SUPPORT + ASSIGNED   ❌
```

Đây là state không phù hợp với logic hiện tại của hệ thống.

### Hậu quả

- Host cũ có thể biến mất khỏi danh sách lời mời/tham dự.
- Host cũ có thể bị từ chối ở Visit Process.
- Host cũ không xuất hiện trong Agenda Responsible Candidates.
- Contribution/list/process có thể cho kết quả quyền khác nhau.

### Phương án sửa

Trong:

```text
backend/PEMS.Application/Delegations/Commands/TransferVisitHost/TransferVisitHostCommandHandler.cs
```

khi hạ Host cũ thành support:

```text
IsHost = false
ParticipantRole = IC_SUPPORT
Status = ACCEPTED
```

State mong muốn:

```text
A:
IC_HOST + ASSIGNED
        ↓ Transfer
IC_SUPPORT + ACCEPTED

B:
IC_HOST + ASSIGNED
```

### Không nên sửa

Không nên giữ:

```text
IC_SUPPORT + ASSIGNED
```

rồi đi mở filter ở tất cả query.

Cũng không nên chuyển Host cũ về:

```text
IC_SUPPORT + INVITED
```

trừ khi nghiệp vụ thực sự muốn gửi lại một lời mời mới và bắt Host cũ Accept/Decline lại.

---

# 2. BUG-02 — Host cũ biến mất khỏi danh sách sau Transfer Host

### Nguyên nhân

Các query dành cho Staff đang cố ý loại `ASSIGNED` đối với `IC_SUPPORT`.

Ví dụ logic hiện tại:

```text
ParticipantRole = IC_SUPPORT
AND Status != ASSIGNED
```

Sau BUG-01, Host cũ có:

```text
IC_SUPPORT + ASSIGNED
```

nên bị loại.

### Hậu quả

Host cũ:

```text
không còn CurrentHostUserId
+
không lọt attending/invitation list
=
"biến mất" khỏi UI
```

### Phương án sửa

Không sửa trực tiếp filter trước.

Fix nguồn state tại BUG-01:

```text
IC_SUPPORT + ACCEPTED
```

Sau đó các query hiện tại sẽ tự nhận Host cũ như một participant đã tham gia.

### File liên quan cần regression test

```text
backend/PEMS.Application/Delegations/Queries/GetVisitInvitations/GetVisitInvitationsQueryHandler.cs

backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs
```

---

# 3. BUG-03 — Department Leader mất nút “Đóng góp” sau khi giao Staff

### Hiện trạng

Trong assignments progress, participant của một visit được group lại.

Code ưu tiên row hiển thị:

```text
activeStaff
→ declinedStaff
→ leaderRow
→ first row
```

Sau đó lại dùng chính row `selected` này để tính:

```text
CanOpenContribution
```

### Tình huống lỗi

```text
Department Leader đã ACCEPT lời mời
↓
Leader giao nhiệm vụ cho Department Staff
↓
activeStaff được chọn làm row hiển thị
↓
currentUser = Leader
selected participant = Staff
↓
LeaderId != StaffId
↓
CanOpenContribution = false
```

Trong khi Leader vẫn có participant relation hợp lệ.

### Hậu quả

- Nút Contribution biến mất trên list.
- Gõ trực tiếp URL Contribution có thể vẫn vào được.
- List permission và endpoint permission bị drift.

### Phương án sửa

Trong:

```text
backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs
```

phải tách:

```text
participant dùng để hiển thị
```

và:

```text
participant dùng để authorization
```

Không dùng `selected` để tính quyền của current user.

Pseudo logic:

```csharp
var currentUserParticipant = group
    .Where(x => x.p.UserId == currentUserId)
    .OrderByDescending(...)
    .FirstOrDefault();

var canOpenContribution =
    currentUserParticipant != null
    && ContributionAccess.IsDepartmentContributorForInvitation(
        currentUserId,
        currentUserParticipant.p.UserId,
        currentUserParticipant.p.Status);
```

`activeStaff` vẫn có thể dùng để hiển thị:

```text
Người phụ trách = Staff A
```

nhưng quyền của Leader phải lấy từ relation của Leader.

---

# 4. BUG-04 — Invitation chưa Accept nhưng chuyến đã qua lại hiển thị “Hoàn thành”

### Hiện trạng

Logic normalize hiện tại ưu tiên:

```text
nếu visit AFTER_VISIT / CLOSED
hoặc now > endAt
→ DONE
```

trước khi xử lý:

```text
INVITED
```

### Tình huống lỗi

```text
Raw status = INVITED
User chưa Accept
Visit đã kết thúc
↓
UI status = DONE
↓
Hiển thị "Hoàn thành"
```

Điều này sai nghĩa nghiệp vụ.

### Hậu quả

Người dùng nhìn UI tưởng:

```text
đã tham gia và hoàn thành
```

nhưng thực tế:

```text
chưa hề phản hồi lời mời
```

và Contribution vẫn có thể bị từ chối.

### Phương án sửa

Trong:

```text
GetAssignmentsProgressListQuery.cs
```

sửa `NormalizeInvitationStatus`.

State mong muốn:

```text
INVITED + visit chưa kết thúc
→ REQUESTED

INVITED + visit đã kết thúc
→ EXPIRED

ACCEPTED + visit đang diễn ra
→ IN_PROGRESS

ACCEPTED + visit kết thúc
→ DONE

DECLINED
→ REJECTED

CANCELLED
→ CANCELLED
```

Bổ sung label:

```text
EXPIRED → Hết hạn / Không phản hồi
```

### Không sửa DB raw status

Không cần đổi:

```text
INVITED → EXPIRED
```

trong database.

`EXPIRED` chỉ nên là derived UI status.

---

# 5. BUG-05 — Có 2 luồng Accept Invitation không đồng nhất

### Hiện trạng

Hệ thống đang có ít nhất hai đường xử lý Accept.

## Luồng chuẩn hơn

```text
RespondVisitParticipantInvitation
```

Có các guard:

```text
authenticated
ownership
participant role
current participant status
visit request status
campus status
transaction/lock
audit
```

## Luồng legacy

```text
DepartmentReceptionTasks.AcceptInvitation
```

Handler cũ có logic riêng và không đầy đủ guard như luồng chuẩn.

### Rủi ro

Cùng một thao tác:

```text
INVITED → ACCEPTED
```

nhưng tùy người dùng bấm ở màn hình nào mà rule khác nhau.

Có thể dẫn tới:

- Accept participant không thuộc current user.
- Accept lại participant đã xử lý.
- Accept khi visit không còn hợp lệ.
- State transition không đồng nhất giữa Dashboard và Invitation Detail.

### Phương án sửa

Chỉ giữ **một domain flow Accept/Decline**.

Tất cả các entry point:

```text
Invitation Detail
Shared Dashboard
Email Action
Notification Action
```

phải cuối cùng đi qua cùng một service/handler nghiệp vụ.

Ưu tiên dùng:

```text
RespondVisitParticipantInvitation
```

hoặc tách core logic của nó thành domain service dùng chung.

### Không nên

Không để hai handler khác nhau cùng tự làm:

```text
participant.Status = ACCEPTED
```

---

# 6. BUG-06 — Transfer Host có thể sửa nhầm historical participant rows

## 6.1 Outgoing Host

### Hiện trạng

Transfer đang có logic tương đương:

```text
tất cả participant của previousHostId
→ đổi IsHost / Role
```

Nếu một user có nhiều participant rows lịch sử trong cùng instance, có nguy cơ sửa cả row cũ.

Ví dụ:

```text
User A

Row 10 = DECLINED       ← lịch sử
Row 20 = ACCEPTED       ← participant khác
Row 30 = IC_HOST        ← Host active
```

Transfer chỉ được sửa:

```text
Row 30
```

không được sửa Row 10/20.

### Phương án sửa

Resolve đúng outgoing active Host:

```text
UserId = previousHostId
AND IsHost = true
AND ParticipantRole = IC_HOST
AND Status != REMOVED
```

Chỉ mutate đúng một row.

Sau đó:

```text
IC_HOST + ASSIGNED
→ IC_SUPPORT + ACCEPTED
```

---

## 6.2 Incoming Host

### Hiện trạng

Incoming participant đang được tìm theo kiểu:

```text
FirstOrDefault(UserId == newHost)
```

Nếu user từng:

```text
DECLINED
```

rồi được invite lại:

```text
ACCEPTED
```

thì có thể tồn tại nhiều row.

`FirstOrDefault()` không có nghĩa là:

```text
active participant
```

### Hậu quả

Có thể pick nhầm historical `DECLINED` row rồi biến nó thành Host.

Sau đó một user có thể đồng thời có:

```text
Row A = IC_HOST + ASSIGNED
Row B = IC_SUPPORT + ACCEPTED
```

gây mâu thuẫn ở list/relation/authorization.

### Phương án sửa

Tạo helper/domain service để resolve participant active theo priority rõ ràng.

Ví dụ:

```text
Host active
ACCEPTED
ASSIGNED
INVITED
DECLINED
```

nhưng phải phân biệt rõ historical row.

Sau transfer phải đảm bảo invariant:

```text
Trong cùng VisitInstance:
mỗi user chỉ có tối đa 1 active participant relation.
```

Các row:

```text
DECLINED
REMOVED
```

không được tự ý mutate chỉ vì cùng UserId.

---

# 7. BUG-07 — Test hiện tại chưa khóa các state quan trọng

### Hiện trạng

Test Transfer Host đã kiểm tra Host cũ:

```text
IsHost = false
ParticipantRole = IC_SUPPORT
```

nhưng chưa assert:

```text
Status = ACCEPTED
```

nên `IC_SUPPORT + ASSIGNED` vẫn pass test.

### Phương án sửa

Bổ sung regression/integration test bắt buộc.

---

# 8. Test cases bắt buộc sau khi fix

## Transfer Host

```text
TC01
Host A transfer sang B
→ A.IsHost = false
→ A.Role = IC_SUPPORT
→ A.Status = ACCEPTED
→ B.IsHost = true
→ B.Role = IC_HOST
→ B.Status = ASSIGNED
```

```text
TC02
Sau transfer Host A vẫn xuất hiện ở attending/invitation list.
```

```text
TC03
Host A cũ mở Visit Process được với relation IC_SUPPORT.
```

```text
TC04
Host A cũ mở Contribution được.
```

```text
TC05
Host A cũ xuất hiện trong Agenda Responsible Candidates nếu còn ACTIVE.
```

```text
TC06
Transfer không mutate historical DECLINED/REMOVED rows.
```

```text
TC07
Incoming Host có nhiều participant rows
→ chỉ đúng active row được promote thành Host
→ không tạo 2 active relation cho cùng user.
```

---

## Department Contribution

```text
TC08
Dept Leader INVITED
→ CanOpenContribution = false
```

```text
TC09
Dept Leader ACCEPTED
→ CanOpenContribution = true
```

```text
TC10
Dept Leader ACCEPTED rồi giao Staff
→ Leader vẫn CanOpenContribution = true
```

```text
TC11
Staff ASSIGNED nhưng chưa Accept
→ kiểm tra đúng rule Contribution theo nghiệp vụ đã thống nhất
```

```text
TC12
Staff ACCEPTED
→ Staff CanOpenContribution = true
```

```text
TC13
List CanOpenContribution và Contribution endpoint phải trả cùng verdict.
```

---

## Invitation lifecycle

```text
TC14
INVITED + BEFORE_VISIT
→ REQUESTED
```

```text
TC15
INVITED + visit đã hết
→ EXPIRED
→ không phải DONE
```

```text
TC16
ACCEPTED + visit đã kết thúc
→ DONE
```

```text
TC17
DECLINED
→ REJECTED
```

---

## Accept authorization

```text
TC18
User A Accept invitation của chính A
→ success
```

```text
TC19
User B gửi participantId của A
→ Forbidden
```

```text
TC20
Invitation đã ACCEPTED/DECLINED
→ không được Accept lại
```

```text
TC21
Visit CANCELLED/REJECTED/CLOSED
→ không được Accept
```

---

# 9. Thứ tự triển khai đề xuất

## Phase 1 — Fix Transfer Host state

Sửa trước:

```text
TransferVisitHostCommandHandler.cs
```

- outgoing Host → `IC_SUPPORT + ACCEPTED`
- chỉ mutate active Host row
- resolve incoming active participant đúng
- đảm bảo không tạo nhiều active relation

---

## Phase 2 — Fix Department Contribution

Sửa:

```text
GetAssignmentsProgressListQuery.cs
```

- không dùng `selected` để authorize current user
- lấy đúng participant của current user
- list permission phải đồng nhất endpoint permission

---

## Phase 3 — Fix Invitation UI lifecycle

Sửa:

```text
NormalizeInvitationStatus
```

Thêm:

```text
EXPIRED
```

và label tương ứng.

---

## Phase 4 — Unify Accept Invitation

Dọn luồng legacy.

Mục tiêu:

```text
1 business action
= 1 state transition implementation
```

Tất cả UI/API entry point dùng chung rule.

---

## Phase 5 — Regression tests

Bổ sung test trước khi kết thúc fix.

Không merge nếu chưa cover:

```text
Transfer Host
Contribution Leader/Staff
Expired Invitation
Ownership Accept
Historical Participant
```

---

# 10. State machine thống nhất sau khi fix

## IC Host

```text
Staff Leader assign Host
        ↓
IC_HOST + ASSIGNED
        ↓ Transfer Host
IC_SUPPORT + ACCEPTED
```

---

## IC Support / Student Support

```text
INVITED
   ↓ Accept
ACCEPTED

INVITED
   ↓ Decline
DECLINED
```

---

## Department Leader

```text
DEPT_SUPPORT + INVITED
        ↓ Accept
DEPT_SUPPORT + ACCEPTED
```

Nếu Leader giao xuống Staff:

```text
Leader:
DEPT_SUPPORT + ACCEPTED
hoặc trạng thái relation hợp lệ tương đương theo domain

Staff:
DEPT_SUPPORT + ASSIGNED
        ↓ Staff Accept
DEPT_SUPPORT + ACCEPTED
```

Việc giao Staff **không được làm mất relation Contribution hợp lệ của Leader nếu nghiệp vụ xác định Leader vẫn là participant của chuyến**.

---

# 11. Invariant cần giữ toàn hệ thống

## Invariant 1

```text
Current Host:
VisitRequestCampus.CurrentHostUserId = UserId
```

phải tương ứng với một participant active:

```text
IsHost = true
ParticipantRole = IC_HOST
Status = ASSIGNED
```

---

## Invariant 2

Host cũ sau transfer:

```text
IsHost = false
ParticipantRole = IC_SUPPORT
Status = ACCEPTED
```

---

## Invariant 3

Không dùng row phục vụ UI display để quyết định quyền của current user.

```text
Display relation ≠ Authorization relation
```

---

## Invariant 4

`DONE` chỉ dành cho participant đã thực sự tham gia.

```text
INVITED + hết hạn
≠ DONE
```

---

## Invariant 5

Mọi Accept/Decline invitation phải dùng cùng một domain rule.

---

## Invariant 6

Historical participant rows không được mutate chỉ vì cùng `UserId`.

---

## Invariant 7

List permission và endpoint authorization phải đồng nhất.

Ví dụ:

```text
CanOpenContribution = true
↔
Contribution endpoint cho phép truy cập
```

không được có tình trạng:

```text
button ẩn nhưng URL vào được
```

hoặc:

```text
button hiện nhưng API 403
```

---

# 12. Các file chính cần kiểm tra/sửa

```text
backend/PEMS.Application/Delegations/Commands/TransferVisitHost/TransferVisitHostCommandHandler.cs
```

```text
backend/PEMS.Application/DepartmentReceptionTasks/Queries/GetAssignmentsProgressList/GetAssignmentsProgressListQuery.cs
```

```text
backend/PEMS.Application/Delegations/Commands/RespondVisitParticipantInvitation/*
```

```text
backend/PEMS.Application/DepartmentReceptionTasks/Commands/AcceptInvitation/*
```

```text
backend/PEMS.Application/Delegations/Queries/GetVisitInvitations/GetVisitInvitationsQueryHandler.cs
```

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs
```

```text
backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs
```

```text
backend/PEMS.Application/Delegations/Queries/GetVisitInstanceContribution/GetVisitInstanceContributionQueryHandler.cs
```

```text
backend/PEMS.Application/Delegations/Common/ResponsibleCandidateEligibility.cs
```

```text
frontend/pems-react/src/pages/dashboard/departments/SharedDashboardView.tsx
```

Các file test chính cần bổ sung/điều chỉnh:

```text
tests/PEMS.IntegrationTests/VisitRequests/VisitHostTransferV2Tests.cs
```

```text
tests/PEMS.IntegrationTests/DepartmentReceptionTasks/GetAssignmentsProgressListQueryTests.cs
```

```text
tests/PEMS.IntegrationTests/DepartmentReceptionTasks/ContributionAuthorizationScopeTests.cs
```

và test cho invitation response/ownership hiện có.

---

# 13. Definition of Done

Chỉ coi là fix xong khi các flow sau hoạt động đúng end-to-end:

```text
Flow A
Staff Leader assign Host A
→ transfer Host A sang B
→ A vẫn thấy visit dưới vai trò support
→ A vào Process được
→ A vào Contribution được
→ A không còn quyền Host
→ B là Host duy nhất
```

```text
Flow B
Host mời Department Leader
→ Leader Accept
→ Leader thấy Contribution
→ Leader giao Staff
→ Leader vẫn giữ đúng quyền của mình
→ Staff Accept
→ Staff có đúng quyền của Staff
```

```text
Flow C
User được mời nhưng không Accept
→ visit kết thúc
→ UI hiển thị EXPIRED/Hết hạn
→ không hiển thị Hoàn thành
→ không có Contribution
```

```text
Flow D
User khác cố Accept participantId không thuộc mình
→ backend từ chối
```

```text
Flow E
Một user có participant lịch sử DECLINED/REMOVED
→ Transfer Host không mutate các row lịch sử đó
```

---

# 14. Kết luận

Các bug hiện tại không nên sửa rời rạc bằng cách thêm điều kiện frontend.

Nguồn lỗi chính là:

```text
State transition không thống nhất
+
Read model đang giả định state khác với state command handler ghi xuống
+
Authorization có chỗ lấy sai participant relation
```

Hướng sửa đúng là:

```text
1. Chuẩn hóa participant state
2. Chuẩn hóa Transfer Host
3. Chuẩn hóa Contribution authorization
4. Chuẩn hóa Invitation lifecycle
5. Chỉ giữ một Accept/Decline domain flow
6. Khóa toàn bộ bằng integration/regression tests
```
