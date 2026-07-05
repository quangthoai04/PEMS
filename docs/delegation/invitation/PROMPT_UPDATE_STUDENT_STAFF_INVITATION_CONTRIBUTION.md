# PROMPT — Cập nhật rule “Lời mời tham dự” cho Student/Staff và mở đường vào Contribution

## 0. Mục tiêu của task

Cập nhật lại logic **tab “Lời mời tham dự”** trong module Quản lý tiếp khách để xử lý đúng các trường hợp:

1. **Student được Staff/Host mời hỗ trợ đoàn** phải xem được **form đăng ký tham quan read-only** ngay khi lời mời còn `INVITED`.
2. **Student sau khi accept lời mời** phải có đường vào trang **Contribution** để đóng góp media/news/minutes theo flag backend.
3. **Staff thường được Staff khác mời hỗ trợ đoàn** phải nhìn thấy lời mời trong tab “Lời mời tham dự”.
4. Không mở rộng quyền sai: user chỉ được xem/làm việc với các đoàn mà họ là participant hợp lệ.

Task này cần sửa đồng bộ **backend authorization/query/DTO** và **frontend routing/action rendering**. Không chỉ sửa UI.

---

## 1. Bối cảnh hiện tại

### 1.1. File frontend liên quan

Cần đọc và kiểm tra tối thiểu các file sau:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitParticipantInvitationDetail.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitContributionPage.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitProcessSummaryPage.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitDuringTab.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitFeedbackPage.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitorVisitDetailPage.tsx
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
```

Nếu đường dẫn thực tế khác, hãy search source theo tên component/API trước khi sửa.

### 1.2. File backend liên quan

Cần search source backend theo các keyword sau:

```text
VisitInvitationsController
GetMyInvitations
GetMyInvitation
AcceptInvitation
DeclineInvitation
VisitParticipantInvitation
SubmittedVisitRequestDetail
GetSubmittedVisitRequestDetail
VisitContribution
GetVisitInstanceContribution
GetVisitInstanceContributionQueryHandler
visit_participants
participant_role
allowedActions
```

Các tầng cần kiểm tra:

```text
Controller
Command / Query
Handler
DTO / Response
Validator
Entity
EF Configuration
DbContext
Authorization/scope helper
Existing tests
```

---

## 2. Vấn đề hiện tại cần sửa

### 2.1. Student không xem được form đăng ký tham quan khi được mời

Hiện tại khi Student bấm icon xem form, UI mở modal xem form đăng ký tham quan, nhưng backend có thể trả `403` vì endpoint detail chỉ cho các role như HO / Staff Leader / Host / Visitor, chưa tính trường hợp **Student là participant được mời**.

Sai ở đây không phải là Student được xem full mọi đơn, mà là backend đang thiếu scope:

```text
Current user là participant hợp lệ của visit instance/request
→ được xem form đăng ký read-only
```

### 2.2. Student đã accept nhưng chưa có nút vào Contribution

Trong `VisitRequestManagement.tsx`, tab `attending` đang có nhánh:

```tsx
if (activeTab === 'attending') {
  const partId = (row as any).participantId;
  if (isDept && subRole === 'STAFF') {
    navTo(`/dashboard/visit/department-tasks/${partId}`);
  } else {
    navTo(`/dashboard/visit/invitations/${partId}`);
  }
  return;
}
```

Logic này khiến Student/Staff hỗ trợ dù đã `ACCEPTED` vẫn chỉ đi vào trang lời mời, không đi vào:

```text
/dashboard/visit/contribution/{visitInstanceId}
```

Trong cùng file đã có logic nhận `OPEN_CONTRIBUTION`, nhưng chưa ưu tiên đúng cho tab attending.

### 2.3. Staff thường không thấy lời mời do Staff khác mời mình hỗ trợ

Tab “Lời mời tham dự” phải load theo người **được mời**:

```text
visit_participants.user_id = currentUserId
```

Không được load theo người tạo đoàn, người mời, hoặc current host.

Nếu backend đang lọc sai theo các điều kiện dưới đây thì Staff B sẽ không thấy lời mời do Staff A gửi:

```text
created_by = currentUserId
invited_by = currentUserId
current_host_user_id = currentUserId
```

---

## 3. Rule nghiệp vụ mới cần áp dụng

### 3.1. Actor áp dụng

Rule này áp dụng cho các user có thể được mời hỗ trợ đoàn:

```text
STAFF + STAFF
DEPARTMENT + LEADER
DEPARTMENT + STAFF
STUDENT
```

Trong đó trọng tâm của task này là:

```text
STUDENT
STAFF + STAFF
```

### 3.2. Scope hợp lệ để xem lời mời

Một user được xem lời mời nếu tồn tại row participant:

```text
visit_participants.user_id = currentUserId
AND visit_participants.status IN ('INVITED', 'ACCEPTED', 'ASSIGNED', 'DECLINED')
AND visit_participants.participant_role IN ('IC_SUPPORT', 'DEPT_SUPPORT', 'STUDENT')
```

Không cho xem nếu:

```text
visit_participants.status = 'REMOVED'
hoặc user không có participant row
hoặc user chỉ đoán URL participantId/visitInstanceId
```

### 3.3. Scope hợp lệ để xem form đăng ký tham quan read-only

User được mở form đăng ký tham quan read-only nếu thuộc một trong các nhóm cũ hoặc nhóm participant hợp lệ:

```text
1. Visitor owner của đơn
2. HO đúng scope multi-campus/single-campus monitoring theo rule hiện hành
3. Staff Leader đúng campus scope
4. Host/current_host_user_id của visit instance
5. Participant được mời/giao trong visit_participants:
   - user_id = currentUserId
   - status IN ('INVITED', 'ACCEPTED', 'ASSIGNED', 'DECLINED')
```

Với nhóm participant, chỉ cho:

```text
VIEW_REQUEST_FORM_READONLY
```

Không cho approve/reject/cancel/assign host nếu role không có quyền.

---

## 4. Rule theo trạng thái lời mời

| invitationStatus | Ý nghĩa UI | Hành động cho Student/Staff hỗ trợ |
|---|---|---|
| `INVITED` | Chờ phản hồi | Xem form đăng ký read-only, xem chi tiết lời mời, chấp nhận, từ chối |
| `ACCEPTED` | Đã nhận lời | Xem form đăng ký read-only, xem chi tiết lời mời, vào Contribution |
| `ASSIGNED` | Đã được giao / mới được giao | Xem form đăng ký read-only, xem chi tiết lời mời, vào Contribution |
| `DECLINED` | Đã từ chối | Xem form đăng ký read-only, xem lại chi tiết lời mời/lý do; không vào Contribution |
| `REMOVED` | Đã bị gỡ khỏi đoàn | Không hiển thị trong tab; gọi trực tiếp phải 403 hoặc 404 |

---

## 5. Backend cần cập nhật

## 5.1. Sửa query “Get My Invitations”

Tìm handler/API đang dùng cho:

```ts
delegationsApi.visitInvitations.getMyInvitations(...)
```

Yêu cầu query:

```sql
SELECT ...
FROM visit_participants vp
JOIN visit_request_campuses vrc ON vrc.visit_instance_id = vp.visit_instance_id
JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
LEFT JOIN users invitedBy ON invitedBy.user_id = vp.invited_by
WHERE vp.user_id = @currentUserId
  AND vp.status IN ('INVITED', 'ACCEPTED', 'ASSIGNED', 'DECLINED')
  AND vp.participant_role IN ('IC_SUPPORT', 'DEPT_SUPPORT', 'STUDENT')
```

Không được lọc bằng:

```sql
vrc.current_host_user_id = @currentUserId
vr.created_by = @currentUserId
vp.invited_by = @currentUserId
```

Các điều kiện đó chỉ phù hợp cho tab “Đơn phụ trách” hoặc lịch sử người mời, không phù hợp với tab “Lời mời tham dự”.

### 5.1.1. Filter search/date/status

Nếu API có filter, áp dụng theo đúng ngữ cảnh lời mời:

```text
keyword:
- delegationName
- partnerName nếu có
- campusName
- invitedByName

invitationStatus:
- vp.status

fromDate / toDate:
- vrc.planned_start_at / vrc.planned_end_at
```

Không lọc status theo request/campus thay cho invitationStatus nếu frontend truyền `invitationStatus`.

---

## 5.2. DTO response bắt buộc

Item trả về cho tab attending cần đủ dữ liệu:

```ts
export interface VisitInvitationListItem {
  participantId: number;
  visitRequestId: number;
  visitInstanceId: number;
  delegationName: string;
  campusName: string | null;
  plannedStartAt: string | null;
  plannedEndAt: string | null;

  invitationStatus: 'INVITED' | 'ACCEPTED' | 'ASSIGNED' | 'DECLINED';
  participantRole: 'IC_SUPPORT' | 'DEPT_SUPPORT' | 'STUDENT';

  invitedByName: string | null;
  invitedByUserId?: number | null;
  invitedAt?: string | null;
  respondedAt?: string | null;
  responseNote?: string | null;

  visitRequestStatus?: string | null;
  campusVisitStatus?: string | null;

  allowedActions: string[];
}
```

Không bắt buộc đúng tên interface nếu project đã có type khác, nhưng response phải có đủ semantic fields.

---

## 5.3. allowedActions theo trạng thái

Backend là nơi quyết định action. Frontend chỉ render theo `allowedActions`.

### INVITED

```text
VIEW_REQUEST_FORM
VIEW_INVITATION_DETAIL
ACCEPT_INVITATION
DECLINE_INVITATION
```

### ACCEPTED / ASSIGNED

```text
VIEW_REQUEST_FORM
VIEW_INVITATION_DETAIL
OPEN_CONTRIBUTION
```

### DECLINED

```text
VIEW_REQUEST_FORM
VIEW_INVITATION_DETAIL
```

### REMOVED

Không trả trong list. Nếu gọi detail trực tiếp:

```text
403 Forbidden
hoặc 404 Not Found nếu project đang dùng cách không lộ resource tồn tại
```

---

## 5.4. Sửa authorization của API xem form đăng ký tham quan

Tìm endpoint đang được `SubmittedVisitRequestDetailModal` gọi.

Yêu cầu:

```text
Nếu current user không thuộc các scope cũ nhưng có participant hợp lệ:
- cho xem form đăng ký tham quan read-only
- không trả action mutate
- không trả dữ liệu nội bộ không cần thiết nếu endpoint này đang dùng chung cho nhiều màn
```

Pseudo-code:

```csharp
var isParticipant = await _db.VisitParticipants.AnyAsync(p =>
    p.VisitInstance.VisitRequestId == request.VisitRequestId
    && p.UserId == currentUser.UserId
    && new[] { "INVITED", "ACCEPTED", "ASSIGNED", "DECLINED" }.Contains(p.Status)
);

if (!oldScopeAllowed && !isParticipant)
{
    throw new ForbiddenException("Bạn không có quyền xem đơn này.");
}
```

Nếu form detail đang nhận `visitRequestId`, check participant qua join:

```text
visit_participants.visit_instance_id
→ visit_request_campuses.visit_instance_id
→ visit_request_campuses.visit_request_id
```

Nếu form detail nhận `visitInstanceId`, check trực tiếp theo instance.

---

## 5.5. Sửa API invitation detail

Trang:

```text
/dashboard/visit/invitations/{participantId}
```

phải cho chính participant xem chi tiết lời mời.

Scope:

```text
visit_participants.participant_id = route participantId
AND visit_participants.user_id = currentUserId
AND visit_participants.status IN ('INVITED', 'ACCEPTED', 'ASSIGNED', 'DECLINED')
```

Nếu status là `ACCEPTED` hoặc `ASSIGNED`, response cần có:

```text
canOpenContribution = true
visitInstanceId
```

hoặc dùng `allowedActions` chứa `OPEN_CONTRIBUTION`.

---

## 5.6. Sửa authorization của Contribution page

Trang:

```text
/dashboard/visit/contribution/{visitInstanceId}
```

không nên bị chặn toàn trang nếu user là Student/Staff hỗ trợ đã accepted/assigned.

Scope hợp lệ:

```text
visit_participants.visit_instance_id = route visitInstanceId
AND visit_participants.user_id = currentUserId
AND visit_participants.status IN ('ACCEPTED', 'ASSIGNED')
```

Khi hợp lệ, backend trả contribution DTO với các flag cụ thể:

```ts
canViewContribution: boolean;
canUploadMedia: boolean;
canCreateNews: boolean;
canEditMinutes: boolean;
canEditNews: boolean;
canEditFeedback?: boolean;
```

Không hardcode tất cả Student đều được sửa mọi phần. Nếu một phần chưa cho thao tác thì section đó read-only/disabled theo flag.

Rule quan trọng:

```text
Được vào trang contribution ≠ được sửa mọi section.
```

---

## 6. Frontend cần cập nhật

## 6.1. Cập nhật type AllowedAction

Trong type frontend, bổ sung nếu chưa có:

```ts
type AllowedAction =
  | 'VIEW_REQUEST_FORM'
  | 'VIEW_INVITATION_DETAIL'
  | 'ACCEPT_INVITATION'
  | 'DECLINE_INVITATION'
  | 'OPEN_CONTRIBUTION'
  // giữ các action cũ...
```

Nếu `AllowedAction` đang là union type chặt, phải bổ sung để TypeScript không lỗi.

---

## 6.2. Cập nhật mapping row trong `VisitRequestManagement.tsx`

Trong nhánh:

```tsx
if (effectiveTab === 'attending') {
  const response = await delegationsApi.visitInvitations.getMyInvitations(invParams);
  const items: any[] = response.items || [];
  const mapped: Row[] = items.map(...)
}
```

Đảm bảo mapped row giữ lại:

```ts
participantId
visitRequestId
visitInstanceId
invitationStatus
participantRole
allowedActions
```

Không làm mất `allowedActions` khi spread/map.

Gợi ý mapping:

```tsx
const mapped: Row[] = items.map((item) => {
  let statusText = item.invitationStatus;
  if (statusText === 'INVITED') statusText = 'Chờ phản hồi';
  else if (statusText === 'ACCEPTED') statusText = 'Đã nhận lời';
  else if (statusText === 'ASSIGNED') statusText = 'Mới được giao';
  else if (statusText === 'DECLINED') statusText = 'Đã từ chối';

  return {
    ...item,
    id: item.visitInstanceId || item.visitRequestId || item.participantId,
    name: item.delegationName || 'Không có tên',
    org: item.invitedByName ? `Người mời: ${item.invitedByName}` : '-',
    campus: item.campusName || '-',
    host: '-',
    sender: '-',
    time: formatDateTimeShort(item.plannedStartAt),
    statusText,
    allowedActions: item.allowedActions || [],
  };
});
```

---

## 6.3. Cập nhật helper check action

Thêm helper trong `VisitRequestManagement.tsx`:

```tsx
const getInvitationStatusCode = (row: Row) =>
  ((row as any).invitationStatus || '').toUpperCase();

const isAcceptedInvitation = (row: Row) =>
  ['ACCEPTED', 'ASSIGNED'].includes(getInvitationStatusCode(row));

const isInvitedInvitation = (row: Row) =>
  getInvitationStatusCode(row) === 'INVITED';

const isDeclinedInvitation = (row: Row) =>
  getInvitationStatusCode(row) === 'DECLINED';

const hasAllowedAction = (row: Row, action: string) =>
  (row.allowedActions || []).includes(action as AllowedAction);
```

Không gate theo label tiếng Việt như `statusText === 'Đã nhận lời'`.

---

## 6.4. Sửa `canOpenProcess(row)`

Hiện tại `canOpenProcess` có đoạn:

```tsx
if (activeTab === 'attending') return true;
```

Cần sửa để rõ ràng hơn:

```tsx
if (activeTab === 'attending') {
  const actions = row.allowedActions || [];

  if (actions.includes('OPEN_CONTRIBUTION')) return true;
  if (actions.includes('VIEW_INVITATION_DETAIL')) return true;

  return !!(row as any).participantId;
}
```

---

## 6.5. Sửa `getProcessActionTitle(row)`

Trong tab attending:

```tsx
if (activeTab === 'attending') {
  if ((row.allowedActions || []).includes('OPEN_CONTRIBUTION')) {
    return 'Vào trang đóng góp';
  }

  if (isInvitedInvitation(row)) return 'Xem và phản hồi lời mời';
  if (isDeclinedInvitation(row)) return 'Xem lời mời đã từ chối';

  return isDept && subRole === 'STAFF' ? 'Xem nhiệm vụ' : 'Xem lời mời';
}
```

---

## 6.6. Sửa `handleProcess(row)`

Trong `handleProcess`, nhánh `activeTab === 'attending'` phải ưu tiên `OPEN_CONTRIBUTION`.

Gợi ý:

```tsx
if (activeTab === 'attending') {
  const partId = (row as any).participantId;
  const actions = row.allowedActions || [];

  if (actions.includes('OPEN_CONTRIBUTION') && row.visitInstanceId) {
    navTo(`/dashboard/visit/contribution/${row.visitInstanceId}`);
    return;
  }

  if (isDept && subRole === 'STAFF') {
    navTo(`/dashboard/visit/department-tasks/${partId}`);
    return;
  }

  navTo(`/dashboard/visit/invitations/${partId}`);
  return;
}
```

Nếu Department Staff sau khi accepted cũng cần contribution thì không hardcode chặn; nhưng nếu hiện nghiệp vụ Department Staff dùng task page riêng, giữ như trên.

---

## 6.7. Sửa render icon/action trong tab attending

Hiện slot 2 dùng `canOpenProcess(row)` và icon `ArrowRightCircle`, hoặc icon `FileText` nếu `OPEN_CONTRIBUTION`.

Nên giữ pattern hiện tại nhưng đảm bảo:

```tsx
tone={can('OPEN_CONTRIBUTION') ? 'orange' : 'blue'}
icon={can('OPEN_CONTRIBUTION') ? <FileText /> : <ArrowRightCircle />}
title={getProcessActionTitle(row)}
```

Nếu UI cần rõ hơn, có thể dùng label trên mobile:

```tsx
label={can('OPEN_CONTRIBUTION') ? 'Đóng góp' : undefined}
```

Không bắt buộc label desktop nếu layout chật.

---

## 6.8. Nút xem form đăng ký

Hiện code đang render nút xem form nếu có `visitRequestId`:

```tsx
{row.visitRequestId ? (
  <ActionIconButton title="Xem form yêu cầu" ... onClick={() => openRequestForm(row)} />
) : ...}
```

Có thể giữ, nhưng tốt hơn là gate bằng backend action:

```tsx
const canViewRequestForm =
  row.visitRequestId &&
  (
    activeTab !== 'attending' ||
    (row.allowedActions || []).includes('VIEW_REQUEST_FORM')
  );
```

Sau đó render:

```tsx
{canViewRequestForm ? (
  <ActionIconButton
    title="Xem form đăng ký tham quan"
    tone="blue"
    icon={<FileText className="h-5 w-5" />}
    onClick={(e) => { e.stopPropagation(); openRequestForm(row); }}
  />
) : (
  <span className="h-9 w-9" aria-hidden="true" />
)}
```

Điểm quan trọng: nếu frontend hiện nút nhưng backend chưa sửa authorization thì vẫn 403. Phải sửa backend trước hoặc cùng lúc.

---

## 6.9. Sửa `VisitParticipantInvitationDetail.tsx`

Trang chi tiết lời mời cần có 3 vùng rõ:

```text
1. Thông tin lời mời
2. Tóm tắt đoàn / nút xem form đăng ký read-only
3. Hành động theo status
```

### Khi INVITED

Hiển thị:

```text
- Xem form đăng ký tham quan
- Chấp nhận lời mời
- Từ chối lời mời
```

### Khi ACCEPTED / ASSIGNED

Hiển thị:

```text
- Badge: Đã nhận lời
- Nút: Xem form đăng ký tham quan
- Nút chính: Vào trang đóng góp
```

Route:

```tsx
navigate(`/dashboard/visit/contribution/${visitInstanceId}`);
```

### Khi DECLINED

Hiển thị:

```text
- Badge: Đã từ chối
- Lý do từ chối nếu có
- Nút: Xem form đăng ký tham quan
```

Không hiển thị nút contribution.

---

## 6.10. Sửa `VisitContributionPage.tsx`

Kiểm tra page đang gọi API nào để lấy permission/flags.

Yêu cầu UI:

```text
- Nếu canViewContribution = true: load page.
- Nếu section nào false flag: section đó read-only/disabled với thông báo ngắn.
- Không chặn toàn trang chỉ vì Student không phải host.
```

Ví dụ:

```tsx
if (!data.canViewContribution) {
  return <ForbiddenState message="Bạn không có quyền truy cập trang đóng góp của đoàn này." />;
}
```

Với từng section:

```tsx
<MediaContributionSection disabled={!data.canUploadMedia} />
<MinutesContributionSection readOnly={!data.canEditMinutes} />
<NewsContributionSection disabled={!data.canCreateNews && !data.canEditNews} />
```

Không hardcode theo role ở frontend nếu backend đã trả flag.

---

## 7. Security / Authorization bắt buộc

Không được chỉ sửa frontend.

Backend phải đảm bảo:

```text
- User không có participant row không xem được lời mời.
- User không có participant row không xem được form bằng URL trực tiếp.
- User INVITED/DECLINED không vào contribution.
- User ACCEPTED/ASSIGNED mới vào contribution.
- REMOVED không hiển thị và không truy cập được.
- Student/Staff không được approve/reject/cancel/assign host nếu không có role đó.
```

Không dùng dynamic permissions:

```text
permissions
role_permissions
permission_code
permission_level
```

Authorization phải dùng fixed policy:

```text
role_code
sub_role
primary_campus_id
department_id
visitor_user_id
current_host_user_id
visit_participants.user_id
visit_participants.status
record status
```

---

## 8. Test bắt buộc

## 8.1. Backend integration/API tests

Tạo/cập nhật test cho các case:

### Case 1 — Student INVITED xem được form read-only

```text
Given Student S có visit_participants row status INVITED
When S gọi API xem form đăng ký visitRequestId tương ứng
Then trả 200
And response không có action mutate
```

### Case 2 — Student không thuộc participant bị chặn

```text
Given Student S không có participant row trong visitRequestId
When S gọi API xem form bằng URL trực tiếp
Then trả 403 hoặc 404 theo convention project
```

### Case 3 — Student INVITED chưa vào contribution

```text
Given Student S có participant status INVITED
When S gọi API contribution của visitInstanceId
Then trả 403
```

### Case 4 — Student ACCEPTED vào contribution

```text
Given Student S có participant status ACCEPTED
When S gọi API contribution của visitInstanceId
Then trả 200
And canViewContribution = true
```

### Case 5 — Staff B được Staff A mời hỗ trợ thấy lời mời

```text
Given Staff A là host/creator
And Staff A mời Staff B vào visit_participants.user_id = StaffB
When Staff B gọi getMyInvitations
Then response có lời mời đó
```

### Case 6 — Staff B ACCEPTED thấy OPEN_CONTRIBUTION

```text
Given Staff B participant status ACCEPTED
When Staff B gọi getMyInvitations
Then item.allowedActions contains OPEN_CONTRIBUTION
```

### Case 7 — DECLINED không vào contribution

```text
Given participant status DECLINED
When gọi getMyInvitations
Then allowedActions không chứa OPEN_CONTRIBUTION
And contribution API trả 403
```

### Case 8 — REMOVED không hiển thị

```text
Given participant status REMOVED
When gọi getMyInvitations
Then item không xuất hiện
And invitation detail direct call bị 403/404
```

---

## 8.2. Frontend manual test

Test bằng browser với ít nhất các account:

```text
Student
Staff thường A
Staff thường B
Host/IC Staff
Staff Leader
```

Checklist:

```text
[ ] Student có lời mời INVITED thấy tab “Lời mời tham dự”.
[ ] Student bấm icon form xem được form đăng ký read-only, không còn 403.
[ ] Student INVITED bấm action process đi vào invitation detail, chưa vào contribution.
[ ] Student accept xong row đổi trạng thái “Đã nhận lời”.
[ ] Student sau accept thấy action “Vào trang đóng góp”.
[ ] Student bấm vào contribution route đúng `/dashboard/visit/contribution/{visitInstanceId}`.
[ ] Staff B được Staff A mời thấy lời mời trong tab attending.
[ ] Staff B accept xong thấy contribution.
[ ] User không thuộc participant nhập URL form/contribution trực tiếp bị chặn.
```

---

## 9. Không được sửa ngoài scope

Không sửa các phần sau nếu không liên quan trực tiếp:

```text
- Flow HO approve/reject
- Flow Staff Leader approve/assign host
- Flow Visitor submit/cancel
- Host process lifecycle
- Gallery/news/minutes business rule sâu nếu không liên quan permission contribution
- Database schema nếu không bắt buộc
- UI layout toàn trang nếu không cần
```

Không tạo mock data để che lỗi backend. Nếu cần seed test, seed rõ trong test database hoặc test setup.

---

## 10. Definition of Done

Task chỉ được báo hoàn thành khi đạt đủ:

```text
[ ] Backend query getMyInvitations load theo invitee `visit_participants.user_id = currentUserId`.
[ ] Staff được Staff khác mời hỗ trợ thấy lời mời.
[ ] Student được mời thấy lời mời.
[ ] Student/Staff được mời xem được form đăng ký read-only.
[ ] Student/Staff INVITED chưa vào contribution.
[ ] Student/Staff ACCEPTED hoặc ASSIGNED vào được contribution.
[ ] allowedActions trả đúng theo invitationStatus.
[ ] Frontend không gate logic bằng label tiếng Việt.
[ ] Backend chặn direct URL nếu user không có scope.
[ ] Backend build thành công.
[ ] Frontend build TypeScript thành công.
[ ] Test backend/API liên quan pass hoặc báo rõ test nào chưa chạy và lý do.
```

---

## 11. Báo cáo sau khi sửa

Sau khi sửa code, báo cáo theo format:

```md
# Báo cáo cập nhật lời mời tham dự Student/Staff

## 1. File đã sửa

| Layer | File | Nội dung sửa |
|---|---|---|
| Backend | ... | ... |
| Frontend | ... | ... |
| Test | ... | ... |

## 2. Logic đã cập nhật

- ...
- ...

## 3. API/DTO thay đổi

- ...

## 4. Test đã chạy

```bash
dotnet build
dotnet test
npm run build
```

## 5. Kết quả kiểm tra manual

- Student INVITED: ...
- Student ACCEPTED: ...
- Staff B được Staff A mời: ...

## 6. Lưu ý còn lại

- ...
```
