# PEMS — Kế hoạch fix 5 lỗi trên nhánh Dev

## 1. Mục tiêu

Fix đúng 5 lỗi đã xác định trên code hiện tại của nhánh `Dev`, giữ thay đổi tối thiểu, không đổi database schema, không đổi permission/lifecycle ngoài phạm vi yêu cầu.

Baseline đã rà soát:

- Repository: `quangthoai04/PEMS`
- Branch: `Dev`
- Commit: `a9d59b2d4e823aef58d76bc8290e4e6a0baf1bf9`

Các lỗi cần xử lý:

1. Login Modal bị giật nhẹ một lần khi mở.
2. Gửi lời mời phòng ban thất bại với lỗi preview token thuộc email khác.
3. Đồng bộ người tham gia biên bản chưa gộp người trùng giữa Guest và Support/Internal.
4. Xóa người trong biên bản rồi bấm đồng bộ lại thì người vừa xóa không quay lại.
5. Bỏ nút `Quét danh thiếp` bị lặp trong bảng người tham gia biên bản.

---

# 2. Nguyên tắc triển khai

- Không thay đổi SQL/schema.
- Không bỏ hoặc nới lỏng security check của email preview token.
- Không đổi permission hiện tại.
- Không refactor module ngoài scope.
- Backend vẫn là nguồn kiểm tra cuối cùng cho dữ liệu và security.
- Ưu tiên reuse helper/service hiện có.
- Bổ sung regression test cho từng bug.

---

# 3. Bug 1 — Login Modal bị giật nhẹ khi mở

## Nguyên nhân

File:

`frontend/pems-react/src/components/modals/LoginModal.tsx`

Modal hiện animate đồng thời:

```tsx
initial={{ opacity: 0, scale: 0.95, y: 20 }}
animate={{ opacity: 1, scale: 1, y: 0 }}
transition={{ type: 'spring', damping: 25, stiffness: 300 }}
```

`spring + scale + translateY` tạo một nhịp settle nhỏ sau khi modal xuất hiện.

Phần Google Sign-In trong:

`frontend/pems-react/src/features/authentication/components/LoginForm.tsx`

đã có logic chống re-render/jitter khi nhập email/password và đã reserve `min-h-[40px]`, không cần sửa lại.

## Kế hoạch sửa

### File thay đổi

`frontend/pems-react/src/components/modals/LoginModal.tsx`

### Thay đổi

Đổi animation modal sang tween ngắn, không translate Y.

Ví dụ:

```tsx
initial={{ opacity: 0, scale: 0.98 }}
animate={{ opacity: 1, scale: 1 }}
exit={{ opacity: 0, scale: 0.98 }}
transition={{ duration: 0.16, ease: 'easeOut' }}
```

### Không thay đổi

- Google SSO initialization.
- Login form state.
- Authentication flow.
- Redirect sau login.

## Kiểm tra

- Mở login lần đầu.
- Đóng và mở lại nhiều lần.
- Google script tải chậm.
- Gõ email/password liên tục.
- Chuyển VI/EN khi modal đang mở.

Expected:

- Modal không có cú giật sau khi vừa xuất hiện.
- Google button không nhấp nháy khi nhập credentials.

---

# 4. Bug 2 — Gửi lời mời phòng ban báo preview thuộc email khác

## Nguyên nhân

File FE:

`frontend/pems-react/src/features/delegations/components/ParticipantInvitationSection.tsx`

Scope preview hiện lấy từ:

```ts
target?.payload.userId
```

Nhưng khi mời phòng ban, payload là:

```ts
{
  participantType: 'DEPT_SUPPORT',
  departmentId: d.departmentId
}
```

nên không có `userId`.

Kết quả:

```text
Preview scope = empty
```

Trong khi backend:

`backend/PEMS.Application/Delegations/Commands/InviteVisitParticipant/InviteVisitParticipantCommandHandler.cs`

resolve `departmentId` thành trưởng phòng thực tế rồi kiểm tra scope theo:

```text
visitInstance:{visitInstanceId}|participant:{targetUserId}
```

Do preview scope và send scope khác nhau nên verifier trả lỗi:

```text
Bản xem trước thuộc về một email khác.
Vui lòng mở lại email cần gửi.
```

Backend query phòng ban đã trả sẵn:

```text
LeaderUserId
LeaderName
LeaderEmail
```

nên không cần đổi database/backend business rule.

## Kế hoạch sửa

### File chính

`frontend/pems-react/src/features/delegations/components/ParticipantInvitationSection.tsx`

### Thay đổi

Mở rộng `PreviewTarget`:

```ts
type PreviewTarget = {
  key: string;
  payload: Parameters<typeof delegationsApi.inviteVisitParticipant>[1];
  displayName: string;
  recipient: EmailPreviewRecipient;
  scopeUserId: number;
};
```

Với IC Support:

```ts
scopeUserId: c.userId
```

Với Student:

```ts
scopeUserId: c.userId
```

Với Department:

```ts
scopeUserId: d.leaderUserId
```

Đổi:

```ts
const scopeFor = (target: PreviewTarget | null) =>
  target
    ? participantScopeKey(visitInstanceId, target.scopeUserId)
    : null;
```

### Guard

Chỉ render nút preview/send Department khi:

```text
canInviteParticipant = true
AND leaderUserId != null
AND leaderEmail hợp lệ theo dữ liệu trả về
```

Không bypass token verifier.

### File liên quan chỉ cần kiểm tra

`frontend/pems-react/src/features/emails/utils/emailScopeKey.ts`

Không đổi contract hiện tại nếu không cần.

## Kiểm tra

### Case 1

Mời Staff hỗ trợ:

- Preview.
- Edit.
- Final preview.
- Send.

Expected: thành công.

### Case 2

Mời Student:

Expected: thành công như cũ.

### Case 3

Mời Department Leader:

Expected:

- Preview tạo đúng scope.
- Final preview thành công.
- Send không còn lỗi preview thuộc email khác.

### Security regression

Preview của leader A không được dùng gửi cho leader B.

---

# 5. Bug 3 — Đồng bộ biên bản chưa gộp Guest và Support/Internal trùng nhau

## Nguyên nhân

Backend:

`backend/PEMS.Application/Delegations/Minutes/MinuteAutoFill.cs`

hiện chỉ dedupe theo:

```text
user_id
guest_member_id
```

hai nhóm được kiểm tra độc lập:

```text
seenUserIds
seenGuestIds
```

Do đó một người xuất hiện đồng thời:

- trong `visit_participants` dưới dạng Internal/Support;
- trong `visit_instance_guest_members` dưới dạng Guest;

vẫn sinh thành hai row nếu ID khác loại dù snapshot thông tin giống nhau.

Frontend:

`frontend/pems-react/src/pages/dashboard/visit/MinutesCard.tsx`

cũng chỉ dedupe bằng:

```text
haveUser
haveGuest
```

nên vẫn có thể append duplicate cross-source.

## Quy tắc cần thống nhất

Không merge chỉ dựa vào họ tên.

Tạo identity fingerprint chuẩn hóa từ các field đủ mạnh:

```text
fullName
role/jobTitle
organization
```

Normalize:

- trim;
- lowercase;
- collapse whitespace;
- null -> empty;
- không dùng accent removal nếu chưa thực sự cần.

Khi Guest và Internal/Support có fingerprint giống nhau:

```text
Ưu tiên row Internal/Support có userId
```

vì `userId` là system identity mạnh hơn `guestMemberId`.

## Kế hoạch sửa backend

### File

`backend/PEMS.Application/Delegations/Minutes/MinuteAutoFill.cs`

### Thay đổi

Thêm helper private đơn giản:

```csharp
BuildIdentityKey(fullName, role, organization)
```

Theo flow:

1. Load Host.
2. Load accepted Internal participants.
3. Add Internal rows trước.
4. Ghi identity key của Internal vào `seenIdentityKeys`.
5. Khi loop Guest:
   - vẫn kiểm tra `guestMemberId`;
   - đồng thời kiểm tra fingerprint;
   - nếu fingerprint đã thuộc Internal thì skip Guest duplicate.

Không tạo abstraction/service mới nếu chỉ dùng trong module Minutes.

## Kế hoạch sửa frontend

### File

`frontend/pems-react/src/pages/dashboard/visit/MinutesCard.tsx`

### Thay đổi

Khi `handleSyncNew()` nhận candidates:

- dựng Set identity hiện có từ `draftParticipants`;
- candidate phải pass cả:
  - ID duplicate check;
  - cross-source identity duplicate check.

Nếu duplicate giữa Guest và Internal:

- giữ Internal;
- không append Guest.

## Backend guard khi Save

### File

`backend/PEMS.Application/Delegations/Minutes/SaveMinutesCommandHandler.cs`

Thêm kiểm tra cuối cùng để request gọi trực tiếp không tạo cross-source duplicate.

Không tin frontend là lớp duy nhất chống duplicate.

## Kiểm tra

### Case 1

Support và Guest:

```text
Name: Nguyễn Văn A
Role: Coordinator
Organization: ABC University
```

Expected: 1 row.

### Case 2

Cùng tên nhưng organization khác.

Expected: 2 row.

### Case 3

Cùng tên + organization nhưng role khác rõ ràng.

Expected: 2 row.

### Case 4

Internal có `userId`, Guest có `guestMemberId`, thông tin hoàn toàn giống nhau.

Expected: giữ Internal row.

---

# 6. Bug 4 — Xóa người rồi bấm Đồng bộ lại nhưng người không quay lại

## Nguyên nhân

Frontend:

`frontend/pems-react/src/pages/dashboard/visit/MinutesCard.tsx`

khi xóa chỉ làm:

```ts
setDraftParticipants(prev =>
  prev.filter(p => p._key !== key)
)
```

Row chỉ mất khỏi draft state, chưa mất trong DB.

Sau đó FE gọi:

```text
GET /MeetingMinutes/{minutesId}/new-participant-candidates
```

Backend:

`backend/PEMS.Application/Delegations/Minutes/GetNewMinuteParticipantsQueryHandler.cs`

load toàn bộ `MinuteParticipants` hiện có trong DB rồi truyền vào:

`MinuteAutoFill.ComputeNewRowsAsync(...)`

Vì row vừa xóa trên UI vẫn còn trong DB nên backend xem nó là:

```text
đã có trong minutes
```

và không trả lại candidate.

Chỉ khi bấm Save thì:

`SaveMinutesCommandHandler.cs`

mới hard-delete minute participant bị bỏ khỏi payload.

## Mục tiêu behavior

Trong cùng một edit session:

```text
Xóa participant
→ bấm Đồng bộ
→ nếu participant vẫn còn trong nguồn chính thức
→ participant phải xuất hiện lại
```

Nguồn chính thức:

- `visit_participants`
- `visit_instance_guest_members`

Nếu participant thực sự đã bị loại khỏi nguồn chính thức thì không được hồi sinh.

## Kế hoạch sửa

### Frontend

File:

`frontend/pems-react/src/pages/dashboard/visit/MinutesCard.tsx`

Thêm state:

```ts
const [removedParticipantIds, setRemovedParticipantIds] = useState<number[]>([]);
```

Khi remove row persisted:

```ts
if (participant.minuteParticipantId > 0) {
  add vào removedParticipantIds
}
```

Khi:

- enter editing mới;
- cancel;
- save thành công;

reset danh sách này.

Khi sync:

```ts
delegationsApi.minutes.newParticipantCandidates(
  minutesId,
  removedParticipantIds
)
```

### API client

File:

`frontend/pems-react/src/features/delegations/api/delegationsApi.ts`

Mở rộng function hiện tại bằng optional param:

```ts
ignoredExistingParticipantIds?: number[]
```

Không breaking caller khác.

### Controller

File:

`backend/PEMS.Api/Controllers/MeetingMinutesController.cs`

Mở rộng endpoint:

```text
GET {minutesId}/new-participant-candidates
```

nhận optional query list:

```text
ignoredExistingParticipantIds
```

### Query

Files:

- `GetNewMinuteParticipantsQuery.cs`
- `GetNewMinuteParticipantsQueryHandler.cs`

Trước khi gọi `ComputeNewRowsAsync`:

```text
existingFromDb
- row có MinuteParticipantId nằm trong ignoredExistingParticipantIds
= effectiveExisting
```

Sau đó:

```text
ComputeNewRowsAsync(effectiveExisting)
```

Security vẫn giữ:

- authentication;
- scope;
- canEdit;
- lock held by current user.

## Kiểm tra

### Case 1

Xóa Guest đang còn trong source → Sync.

Expected: Guest quay lại.

### Case 2

Xóa accepted Support đang còn trong source → Sync.

Expected: Support quay lại.

### Case 3

Xóa manual participant → Sync.

Expected: không tự quay lại.

### Case 4

Xóa participant khỏi draft, đồng thời participant không còn hợp lệ trong source.

Expected: không quay lại.

### Case 5

Cancel edit.

Expected: DB không đổi.

---

# 7. Bug 5 — Bỏ nút Quét danh thiếp thừa trong bảng biên bản

## Nguyên nhân

File:

`frontend/pems-react/src/features/partners/components/ParticipantPartnerCell.tsx`

ở row Guest chưa liên kết đang có cả:

```text
Tạo / liên kết
Quét danh thiếp
```

và mount riêng:

```tsx
<BusinessCardScanModal />
```

Trong khi phía dưới màn `VisitDuringTab` đã có nguyên section OCR:

- upload/chụp ảnh;
- OCR;
- edit extracted information;
- match partner;
- lưu contact.

File:

`frontend/pems-react/src/pages/dashboard/visit/VisitDuringTab.tsx`

Do đó button trong table là chức năng lặp.

## Kế hoạch sửa

### File

`frontend/pems-react/src/features/partners/components/ParticipantPartnerCell.tsx`

### Xóa

- import `ScanLine`;
- import `BusinessCardScanModal`;
- state `scanOpen`;
- button `Quét danh thiếp`;
- `<BusinessCardScanModal ... />`.

### Giữ

```text
Chưa liên kết
Tạo / liên kết
```

Không thay đổi phần OCR chính trong `VisitDuringTab.tsx`.

## Kiểm tra

- Guest chưa link partner:
  - chỉ thấy `Tạo / liên kết`.
- Không còn `Quét danh thiếp` trong từng row.
- Scan Card Visit phía dưới vẫn hoạt động.
- Create/link partner modal vẫn hoạt động.

---

# 8. Thứ tự triển khai

## Phase 1 — Email invitation blocker

1. Sửa `PreviewTarget`.
2. Bind `scopeUserId`.
3. Sửa Department preview target.
4. Test Staff / Student / Department.
5. Test cross-recipient token rejection.

## Phase 2 — Minutes synchronization

1. Thêm removed participant tracking ở FE.
2. Mở rộng API sync optional ignored IDs.
3. Update Query + Handler.
4. Test delete → sync.
5. Test lock/security không bị thay đổi.

## Phase 3 — Cross-source duplicate

1. Thêm identity normalizer trong `MinuteAutoFill`.
2. Dedupe Internal trước Guest.
3. Dedupe FE khi append sync candidates.
4. Thêm backend save guard.
5. Test same person / same name different org.

## Phase 4 — UI cleanup

1. Remove row-level business-card scan.
2. Verify OCR section phía dưới.

## Phase 5 — Login animation

1. Replace spring animation.
2. Verify no layout shift/jitter.

---

# 9. Danh sách file dự kiến thay đổi

## Frontend

```text
frontend/pems-react/src/components/modals/LoginModal.tsx

frontend/pems-react/src/features/delegations/components/ParticipantInvitationSection.tsx

frontend/pems-react/src/features/emails/utils/emailScopeKey.ts
  -> chỉ kiểm tra/reuse, không nhất thiết sửa

frontend/pems-react/src/pages/dashboard/visit/MinutesCard.tsx

frontend/pems-react/src/features/delegations/api/delegationsApi.ts

frontend/pems-react/src/features/partners/components/ParticipantPartnerCell.tsx
```

## Backend

```text
backend/PEMS.Api/Controllers/MeetingMinutesController.cs

backend/PEMS.Application/Delegations/Minutes/GetNewMinuteParticipantsQuery.cs

backend/PEMS.Application/Delegations/Minutes/GetNewMinuteParticipantsQueryHandler.cs

backend/PEMS.Application/Delegations/Minutes/MinuteAutoFill.cs

backend/PEMS.Application/Delegations/Minutes/SaveMinutesCommandHandler.cs
```

## Tests

Ưu tiên bổ sung vào các test suite hiện có thay vì tạo framework/test architecture mới.

Dự kiến:

```text
tests/PEMS.UnitTests/...
tests/PEMS.IntegrationTests/...
frontend/pems-react/src/**/__tests__/...
```

Tìm file test gần module hiện tại và thêm regression case vào đó trước khi tạo file mới.

---

# 10. Regression checklist cuối

## Authentication

- [ ] Login modal không giật khi mở.
- [ ] Google Sign-In không re-render khi nhập password.
- [ ] Password login hoạt động.
- [ ] Google login hoạt động.

## Invitation

- [ ] Invite IC Support.
- [ ] Invite Student.
- [ ] Invite Department Leader.
- [ ] Edit email rồi final preview.
- [ ] Send unchanged preview.
- [ ] Token không reuse sang recipient khác.

## Minutes

- [ ] Auto-fill Host.
- [ ] Auto-fill accepted participants.
- [ ] Auto-fill Guest đúng campus.
- [ ] Guest/Internal duplicate được gộp.
- [ ] Không merge người chỉ giống tên.
- [ ] Delete → Sync khôi phục người còn trong source.
- [ ] Manual row không tự hồi sinh.
- [ ] Save vẫn delete snapshot row bị bỏ.
- [ ] Lock/concurrency vẫn hoạt động.
- [ ] Permission không đổi.

## Partner / OCR

- [ ] Không còn nút Quét danh thiếp trong participant row.
- [ ] `Tạo / liên kết` vẫn hoạt động.
- [ ] Scan Card Visit phía dưới vẫn hoạt động.

---

# 11. Điều kiện hoàn thành

Task chỉ được coi là hoàn thành khi:

1. 5 bug reproduce trước fix.
2. 5 bug pass sau fix.
3. Không đổi database schema.
4. Không bypass email preview security.
5. Không đổi permission ngoài scope.
6. Backend tests liên quan pass.
7. Frontend build/typecheck pass.
8. Regression tests mới pass.
9. Không phát sinh duplicate participant mới trong minutes.
10. Department invitation send thành công cả path:
   - preview;
   - edit;
   - final preview;
   - send.
