# PEMS — FIX PLAN: EMAIL INVITATION PREVIEW SCOPE + MINUTES PARTICIPANT DEDUPE/SYNC

## 1. Mục tiêu

Fix 3 lỗi đã xác định trong code hiện tại trên nhánh `Dev`:

1. **Mời thành phần hỗ trợ phòng ban (`DEPT_SUPPORT`)**
   - Khi mở email → chỉnh sửa → xem trước kết quả → gửi, hệ thống báo:
     - `Bản xem trước thuộc về một email khác. Vui lòng mở lại email cần gửi.`
   - Lỗi xuất hiện rõ khi có chỉnh sửa/đính kèm vì flow đi qua `FINAL_PREVIEW`.

2. **Biên bản lấy trùng người**
   - Nếu cùng một người xuất hiện đồng thời trong:
     - `GUEST`
     - `EXTERNAL_SUPPORT`
   - và thông tin giống nhau hoàn toàn, biên bản chỉ được lấy **1 người**.

3. **Xóa người trong biên bản rồi bấm “Đồng bộ người mới”**
   - Người vừa xóa khỏi draft phải có thể được đồng bộ lại ngay trong cùng phiên chỉnh sửa.
   - Không được tạo duplicate.
   - Không được mất người sau khi bấm Save.

---

# 2. Nguyên tắc triển khai

- Không thay đổi database schema.
- Không thay đổi API public nếu không cần thiết.
- Không bỏ cơ chế verify preview token / scope.
- Không hạ security validation để né lỗi.
- Không auto-save khi người dùng xóa participant trong biên bản.
- Không fuzzy-match tên người.
- Chỉ dedupe khi identity được xem là giống nhau theo rule rõ ràng.
- Ưu tiên thay đổi nhỏ nhất, giữ nguyên architecture hiện tại.

---

# 3. Phạm vi file bị ảnh hưởng

## Frontend

### 3.1 Email invitation

`frontend/pems-react/src/features/delegations/components/ParticipantInvitationSection.tsx`

Mục tiêu:

- Sửa scope của email preview cho `DEPT_SUPPORT`.
- Scope phải dùng chính `leaderUserId` mà backend sẽ resolve khi gửi.

### 3.2 Meeting minutes

`frontend/pems-react/src/pages/dashboard/visit/MinutesCard.tsx`

Mục tiêu:

- Track participant nguồn chính thức bị xóa khỏi draft.
- Khi bấm “Đồng bộ người mới”, restore đúng row cũ trước khi merge candidate mới.
- Không tạo row mới với `minuteParticipantId = 0` cho participant vừa xóa nhưng chưa Save.

---

## Backend

### 3.3 Minutes auto-fill

`backend/PEMS.Application/Delegations/Minutes/MinuteAutoFill.cs`

Mục tiêu:

- Dedupe người giữa `GUEST` và `EXTERNAL_SUPPORT`.
- Không chỉ dedupe theo `guest_member_id`.
- Nếu cùng identity xuất hiện ở cả 2 nhóm:
  - ưu tiên `GUEST`
  - bỏ bản `EXTERNAL_SUPPORT` trùng.

---

# 4. Fix 1 — Email preview scope sai với DEPT_SUPPORT

## 4.1 Root cause

Backend gửi lời mời participant luôn verify preview bằng scope:

```text
visitInstance:{visitInstanceId}|participant:{targetUserId}
```

Trong `InviteVisitParticipantCommandHandler`, `targetUserId` được backend resolve từ DB.

Với:

```text
IC_SUPPORT
STUDENT
```

frontend đã có:

```ts
payload.userId
```

nên scope đúng.

Nhưng với:

```text
DEPT_SUPPORT
```

frontend chỉ truyền:

```ts
{
  participantType: 'DEPT_SUPPORT',
  departmentId: d.departmentId
}
```

Trong khi `scopeFor()` hiện chỉ đọc:

```ts
target?.payload.userId
```

Kết quả:

```text
DEPT_SUPPORT preview scope = null / empty
```

nhưng lúc gửi backend resolve:

```text
departmentId
→ department leader
→ targetUserId
→ visitInstance:{id}|participant:{leaderUserId}
```

=> scope mismatch => backend reject.

---

# 5. Implementation — Fix email scope

## 5.1 Mở rộng local PreviewTarget

Trong:

`ParticipantInvitationSection.tsx`

Từ:

```ts
type PreviewTarget = {
  key: string;
  payload: Parameters<typeof delegationsApi.inviteVisitParticipant>[1];
  displayName: string;
  recipient: EmailPreviewRecipient;
};
```

Sửa thành:

```ts
type PreviewTarget = {
  key: string;
  payload: Parameters<typeof delegationsApi.inviteVisitParticipant>[1];
  displayName: string;
  recipient: EmailPreviewRecipient;

  /**
   * UserId backend sẽ dùng làm participant scope khi SEND.
   * Với IC_SUPPORT/STUDENT lấy từ payload.userId.
   * Với DEPT_SUPPORT lấy từ department.leaderUserId.
   */
  scopeParticipantUserId?: number | null;
};
```

---

## 5.2 Sửa scopeFor()

Hiện tại:

```ts
const scopeFor = (target: PreviewTarget | null) =>
  target?.payload.userId
    ? participantScopeKey(visitInstanceId, target.payload.userId)
    : null;
```

Thay bằng:

```ts
const scopeFor = (target: PreviewTarget | null) => {
  if (!target) return null;

  const participantUserId =
    target.scopeParticipantUserId ??
    target.payload.userId;

  return participantUserId
    ? participantScopeKey(visitInstanceId, participantUserId)
    : null;
};
```

---

## 5.3 IC_SUPPORT

Không cần thay behavior.

Có thể giữ nguyên:

```ts
{
  key: `ic-${c.userId}`,
  payload: {
    participantType: 'IC_SUPPORT',
    userId: c.userId,
  },
  displayName: c.fullName,
  recipient: ...
}
```

Vì fallback vẫn đọc:

```ts
payload.userId
```

---

## 5.4 STUDENT

Không cần thay behavior.

Có thể giữ nguyên:

```ts
{
  key: `st-${c.userId}`,
  payload: {
    participantType: 'STUDENT',
    userId: c.userId,
  },
  displayName: c.fullName,
  recipient: ...
}
```

---

## 5.5 DEPT_SUPPORT

Khi tạo target preview phòng ban, bổ sung:

```ts
scopeParticipantUserId: d.leaderUserId,
```

Ví dụ:

```ts
onPreview={() =>
  openEmailPreviewFor(
    'VISIT_DEPARTMENT_LEADER_INVITATION',
    {
      key: `dept-${d.departmentId}`,
      payload: {
        participantType: 'DEPT_SUPPORT',
        departmentId: d.departmentId,
      },
      scopeParticipantUserId: d.leaderUserId,
      displayName: `trưởng phòng ${d.departmentName}`,
      recipient: {
        name: d.leaderName,
        email: d.leaderEmail,
        roleLabel: 'Trưởng phòng',
        departmentName: d.departmentName,
        campusName: d.campusName,
      },
    },
  )
}
```

---

## 5.6 Guard

Không cho mở runtime preview nếu:

```ts
d.leaderUserId == null
```

Dù hiện tại `canInviteParticipant` đã được backend dùng để chặn khi không có leader active, FE vẫn nên không tạo invalid scope.

Không fallback sang:

```text
departmentId
```

vì backend SEND không dùng departmentId trong participant scope.

---

# 6. Không sửa backend preview verifier

Không sửa:

`backend/PEMS.Application/Emails/Preview/ApprovedEmailContent.cs`

Không bỏ đoạn kiểm tra:

```csharp
if (payload.TemplateCode != templateCode || payload.ScopeKey != scopeKey)
{
    ...
}
```

Đây là security guard đúng.

Không sửa backend thành chấp nhận:

```text
scopeKey == empty
```

hoặc:

```text
department scope
```

vì sẽ làm preview token có thể bị replay sang recipient khác.

---

# 7. Fix 2 — Biên bản lấy trùng GUEST và EXTERNAL_SUPPORT

## 7.1 Root cause

`MinuteAutoFill.ComputeNewRowsAsync()` hiện chống trùng bằng:

```text
user_id
guest_member_id
```

Đối với khách:

```csharp
seenGuestIds.Contains(g.GuestMemberId)
```

Nhưng một người có thể có:

```text
guest_member_id = 101, member_type = GUEST
guest_member_id = 205, member_type = EXTERNAL_SUPPORT
```

với thông tin giống nhau hoàn toàn.

Vì:

```text
101 != 205
```

code hiện tại thêm cả 2 vào `minute_participants`.

---

# 8. Business rule dedupe

Chỉ dedupe giữa các `VisitGuestMember` khi các field sau giống nhau sau normalize:

```text
FullName
Organization
JobTitle
Nationality
```

Không dùng:

```text
GuestMemberId
MemberType
DisplayOrder
```

để xác định identity.

---

## 8.1 Normalize

Rule normalize tối thiểu:

```text
Trim
collapse whitespace
case-insensitive
```

Không:

- fuzzy name matching
- bỏ dấu tiếng Việt
- match gần giống
- đoán cùng người khi thiếu dữ liệu

---

## 8.2 Priority

Nếu cùng identity xuất hiện ở:

```text
GUEST
EXTERNAL_SUPPORT
```

thì giữ:

```text
GUEST
```

và bỏ duplicate:

```text
EXTERNAL_SUPPORT
```

Nếu duplicate cùng loại:

```text
GUEST + GUEST
```

hoặc:

```text
EXTERNAL_SUPPORT + EXTERNAL_SUPPORT
```

thì giữ row có:

```text
DisplayOrder nhỏ hơn
```

sau đó tie-break bằng:

```text
GuestMemberId nhỏ hơn
```

để kết quả deterministic.

---

# 9. Implementation — MinuteAutoFill

Trong:

`backend/PEMS.Application/Delegations/Minutes/MinuteAutoFill.cs`

Thêm helper private:

```csharp
private static string NormalizeIdentityPart(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

    return string.Join(
            " ",
            value.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .ToUpperInvariant();
}
```

Thêm identity key:

```csharp
private static string GuestIdentityKey(VisitGuestMember guest)
{
    return string.Join(
        "|",
        NormalizeIdentityPart(guest.FullName),
        NormalizeIdentityPart(guest.Organization),
        NormalizeIdentityPart(guest.JobTitle),
        NormalizeIdentityPart(guest.Nationality));
}
```

---

# 10. Dedupe source guest list

Sau khi lấy danh sách guest linked vào instance, không loop trực tiếp toàn bộ list.

Tạo danh sách canonical.

Pseudo:

```csharp
var canonicalGuests = guests
    .GroupBy(x => GuestIdentityKey(x.Member))
    .Select(g =>
        g.OrderBy(x =>
                x.Member.MemberType == "EXTERNAL_SUPPORT" ? 1 : 0)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Member.GuestMemberId)
            .First())
    .OrderBy(x => x.DisplayOrder)
    .ThenBy(x => x.Member.GuestMemberId)
    .ToList();
```

Khuyến nghị giữ query result ở shape có:

```text
DisplayOrder
Member
```

thay vì `.Select(x => x.Member)` quá sớm.

Sau đó loop:

```csharp
foreach (var row in canonicalGuests)
{
    var g = row.Member;

    if (seenGuestIds.Contains(g.GuestMemberId))
        continue;

    ...
}
```

---

# 11. Dedupe với existing minute rows

Chỉ dedupe source guest list trước khi tạo row mới là chưa đủ.

Trường hợp:

1. Biên bản đã từng chứa `EXTERNAL_SUPPORT`.
2. Sau đó source có thêm cùng người dưới `GUEST`.
3. Bấm đồng bộ.

Nếu chỉ kiểm tra `seenGuestIds`, vẫn có thể thêm thêm bản `GUEST`.

Do đó cần tạo thêm:

```csharp
var seenGuestIdentityKeys = ...
```

Từ existing minute rows có `GuestMemberId`, cần resolve các `VisitGuestMember` tương ứng để build key.

Flow:

```text
existing minute guest ids
→ load VisitGuestMembers
→ build identity key
→ seenGuestIdentityKeys
```

Khi thêm source candidate:

```csharp
var identityKey = GuestIdentityKey(g);

if (seenGuestIds.Contains(g.GuestMemberId))
    continue;

if (seenGuestIdentityKeys.Contains(identityKey))
    continue;
```

Sau khi add:

```csharp
seenGuestIds.Add(g.GuestMemberId);
seenGuestIdentityKeys.Add(identityKey);
```

---

# 12. Không dedupe INTERNAL với GUEST

Không dùng:

```text
internal user full name
```

để dedupe với guest.

Các loại:

```text
INTERNAL
GUEST
```

là nguồn dữ liệu khác nhau.

Nếu một internal user có cùng tên với guest:

```text
Nguyễn Văn A
```

vẫn là 2 participant khác nhau.

---

# 13. Fix 3 — Xóa participant rồi Sync không quay lại

## 13.1 Root cause

Trong `MinutesCard.tsx`, xóa hiện chỉ sửa draft:

```ts
setDraftParticipants(
  prev => prev.filter(...)
);
```

DB chưa thay đổi.

Backend endpoint:

```text
newParticipantCandidates
```

lại dùng DB `minute_participants` hiện tại làm `existing`.

Vì row vừa xóa trên FE vẫn còn trong DB nên backend cho rằng:

```text
participant này đã tồn tại
```

=> không trả candidate.

---

# 14. Behavior mong muốn

Trong cùng phiên edit:

```text
A đang có trong biên bản
→ user xóa A
→ user bấm "Đồng bộ người mới"
→ A xuất hiện lại đúng 1 lần
→ giữ nguyên minuteParticipantId cũ
```

Không tạo:

```text
minuteParticipantId = 0
```

cho row vừa xóa nhưng chưa Save.

---

# 15. Implementation — track removed source participants

Trong:

`frontend/pems-react/src/pages/dashboard/visit/MinutesCard.tsx`

Thêm:

```ts
const removedSourceParticipantsRef =
  useRef<Map<string, DraftParticipant>>(new Map());
```

Key:

```ts
const sourceParticipantKey = (p: Pick<DraftParticipant, 'userId' | 'guestMemberId'>) => {
  if (p.userId != null) return `u:${p.userId}`;
  if (p.guestMemberId != null) return `g:${p.guestMemberId}`;
  return null;
};
```

---

# 16. Reset removed map khi bắt đầu session edit

Trong `enterEditing()`:

```ts
removedSourceParticipantsRef.current = new Map();
```

Mục đích:

- Không carry state từ phiên edit trước.
- Không restore dữ liệu cũ sai context.

---

# 17. Sửa removeParticipant()

Hiện tại:

```ts
const removeParticipant = (key: string) =>
  setDraftParticipants((prev) =>
    prev.filter((p) => p._key !== key));
```

Sửa thành:

```ts
const removeParticipant = (key: string) => {
  setDraftParticipants((prev) => {
    const row = prev.find((p) => p._key === key);

    if (row && row.minuteParticipantId > 0) {
      const sourceKey = sourceParticipantKey(row);

      if (sourceKey) {
        removedSourceParticipantsRef.current.set(sourceKey, row);
      }
    }

    return prev.filter((p) => p._key !== key);
  });
};
```

Chỉ track:

```text
userId != null
guestMemberId != null
```

Không track manual row:

```text
userId == null
guestMemberId == null
```

---

# 18. Sửa handleSyncNew()

## 18.1 Step 1 — Restore source rows vừa xóa

Trước khi merge candidate từ backend:

```ts
const removed = Array.from(
  removedSourceParticipantsRef.current.values()
);
```

Chỉ restore row nếu source của nó vẫn hợp lệ.

Vì backend endpoint hiện không trả row đang còn trong DB, FE không thể tự biết source có bị xóa khỏi delegation hay không chỉ bằng response candidates.

Do đó cách an toàn:

### Phase A — restore persisted row trong cùng unsaved session

Nếu row có:

```text
minuteParticipantId > 0
```

và user vừa xóa trong chính session hiện tại:

```text
restore row cũ
```

Đây là undo-via-sync đúng với trạng thái DB hiện tại.

Sau khi restore:

```ts
removedSourceParticipantsRef.current.delete(sourceKey);
```

---

## 18.2 Step 2 — Fetch backend candidates

Giữ:

```ts
const candidates =
  await delegationsApi.minutes.newParticipantCandidates(id);
```

---

## 18.3 Step 3 — Merge với draft hiện tại + restored

Build set từ danh sách mới nhất:

```ts
const working = [...draftParticipants, ...restored];

const haveUser = new Set(
  working
    .filter((p) => p.userId != null)
    .map((p) => p.userId)
);

const haveGuest = new Set(
  working
    .filter((p) => p.guestMemberId != null)
    .map((p) => p.guestMemberId)
);
```

Sau đó chỉ append candidate thật sự mới.

---

# 19. Không tạo duplicate khi Sync nhiều lần

Frontend tiếp tục dedupe:

```text
userId
guestMemberId
```

Backend đã dedupe nguồn guest identity ở `MinuteAutoFill`.

Hai lớp có mục tiêu khác nhau:

```text
Frontend
→ chống duplicate draft trong UI

Backend
→ đảm bảo source business rule chính xác
```

Không thay backend bằng frontend-only dedupe.

---

# 20. Save behavior

Giữ nguyên backend:

`SaveMinutesCommandHandler.ReconcileParticipants()`

Behavior đúng hiện tại:

```text
row có minuteParticipantId
→ update / processed

row DB bị client bỏ khỏi payload
→ remove khỏi minute_participants
```

Không sửa logic remove này.

Sau khi restore row cũ, payload phải gửi lại:

```text
minuteParticipantId cũ
```

để backend đánh dấu:

```text
processed
```

và không remove row.

---

# 21. Reset state sau Save / Cancel

Sau Save thành công:

```ts
removedSourceParticipantsRef.current.clear();
```

Sau Cancel:

```ts
removedSourceParticipantsRef.current.clear();
```

Khi lock hết hạn / thoát edit:

```ts
removedSourceParticipantsRef.current.clear();
```

Mục đích:

- Không leak removed-row state sang session sau.
- Không restore sai participant khi reload.

---

# 22. Test plan

## 22.1 Email invitation — frontend

File hiện có thể extend:

```text
frontend/pems-react/src/features/emails/__tests__/EmailPreviewModal.stages.test.tsx
```

và/hoặc test riêng cho:

```text
ParticipantInvitationSection
```

Cases:

### TC-EMAIL-01 — IC_SUPPORT

```text
Preview
→ Edit
→ Final Preview
→ Send
```

Expected:

```text
scope = visitInstance:{id}|participant:{userId}
send success
```

### TC-EMAIL-02 — STUDENT

Tương tự.

### TC-EMAIL-03 — DEPT_SUPPORT

Input:

```text
departmentId = 20
leaderUserId = 500
```

Expected preview request:

```text
scopeKey =
visitInstance:{visitInstanceId}|participant:500
```

Không được:

```text
null
visitInstance:{id}|department:20
```

### TC-EMAIL-04 — DEPT_SUPPORT + attachment

```text
Preview
→ Edit
→ attach file
→ Final Preview
→ Send
```

Expected:

```text
200
không có PREVIEW_TOKEN_INVALID
không có "Bản xem trước thuộc về một email khác"
```

### TC-EMAIL-05 — security

Token preview của:

```text
participant A
```

đem gửi:

```text
participant B
```

Expected:

```text
REJECT
```

Không sửa test security hiện có để cho pass sai.

---

# 23. Backend email regression tests

Extend:

```text
tests/PEMS.IntegrationTests/Emails/FinalPreviewSendParityTests.cs
```

và/hoặc:

```text
tests/PEMS.UnitTests/Delegations/InviteVisitParticipant/InviteVisitParticipantCommandHandlerTests.cs
```

Cases:

```text
correct scope → accept
wrong participant scope → reject
attachment hash same → accept
attachment changed after final preview → reject
```

---

# 24. Minutes dedupe tests

Tạo/extend tests cho:

```text
MinuteAutoFill
```

## TC-MIN-01

Source:

```text
GUEST:
Nguyễn Văn A / ABC / Manager / Vietnam

EXTERNAL_SUPPORT:
Nguyễn Văn A / ABC / Manager / Vietnam
```

Expected:

```text
1 minute participant
source kept = GUEST
```

---

## TC-MIN-02

Same name, khác organization:

```text
Nguyễn Văn A / ABC
Nguyễn Văn A / XYZ
```

Expected:

```text
2 participants
```

---

## TC-MIN-03

Khác casing / khoảng trắng:

```text
" Nguyễn   Văn A "
"nguyễn văn a"
```

các field khác tương đương.

Expected:

```text
1 participant
```

---

## TC-MIN-04

Internal user và guest cùng tên:

Expected:

```text
2 participants
```

---

## TC-MIN-05

Existing minute chứa external support identity X.

Sau đó source có thêm GUEST identity X.

Sync expected:

```text
0 new duplicate participant
```

---

# 25. Frontend minutes sync tests

## TC-SYNC-01

```text
A persisted trong draft
→ remove A
→ Sync
```

Expected:

```text
A quay lại
minuteParticipantId giữ nguyên
```

---

## TC-SYNC-02

```text
A remove
→ Sync
→ Sync lần nữa
```

Expected:

```text
A chỉ xuất hiện 1 lần
```

---

## TC-SYNC-03

```text
A remove
→ Save
```

Expected:

```text
A bị xóa khỏi minute_participants
```

Không tự restore nếu user không bấm Sync.

---

## TC-SYNC-04

```text
A remove
→ Sync
→ Save
```

Expected:

```text
A vẫn tồn tại
không duplicate
```

---

## TC-SYNC-05

Manual participant:

```text
userId = null
guestMemberId = null
```

Remove → Sync.

Expected:

```text
không restore
```

vì đây không phải source participant.

---

# 26. Regression checklist

Sau khi fix phải kiểm tra:

```text
dotnet build
dotnet test
npm test
npm run build
```

Các flow bắt buộc test tay:

1. Host mời Staff IC bình thường.
2. Host mời Student bình thường.
3. Host mời Department Leader bình thường.
4. Department Leader email:
   - xem trước
   - chỉnh sửa
   - không attachment
   - gửi.
5. Department Leader email:
   - chỉnh sửa
   - attach PDF/image
   - final preview
   - gửi.
6. Guest + External Support trùng hoàn toàn.
7. Guest + External Support khác 1 field.
8. Biên bản:
   - xóa participant
   - sync
   - save.
9. Biên bản:
   - xóa participant
   - save
   - mở edit lại.
10. Sync 3 lần liên tiếp không sinh duplicate.

---

# 27. Acceptance criteria

## Email

Pass khi:

```text
DEPT_SUPPORT preview/edit/final-preview/send
```

không còn lỗi:

```text
Bản xem trước thuộc về một email khác.
```

và security scope verification vẫn hoạt động.

---

## Minutes duplicate

Pass khi:

```text
GUEST identity X
EXTERNAL_SUPPORT identity X
```

chỉ tạo:

```text
1 minute_participant
```

với ưu tiên:

```text
GUEST
```

---

## Minutes sync

Pass khi:

```text
remove persisted participant
→ Sync
```

restore đúng row cũ:

```text
same minuteParticipantId
```

và:

```text
remove
→ Sync
→ Save
```

không làm participant biến mất.

---

# 28. Non-goals

Không làm trong task này:

- thay đổi DB schema;
- merge/xóa duplicate record trong `visit_guest_members`;
- fuzzy matching khách;
- auto-save biên bản;
- sửa workflow email;
- bỏ final preview token;
- đổi scope contract backend;
- refactor toàn bộ email composer;
- thay đổi behavior invite IC_SUPPORT/STUDENT.

---

# 29. Thứ tự triển khai

1. Fix `ParticipantInvitationSection.tsx`.
2. Add email regression tests.
3. Fix `MinuteAutoFill.cs`.
4. Add backend dedupe tests.
5. Fix `MinutesCard.tsx`.
6. Add frontend sync tests.
7. Run full affected test suites.
8. Manual verification trên flow `/dashboard/visit/process/{visitInstanceId}`.

---

# 30. Kết quả mong đợi cuối

Sau patch:

```text
Email invitation
DEPT_SUPPORT
→ preview scope đúng leader
→ edit/attach/final preview/send thành công
```

```text
Minutes
GUEST + EXTERNAL_SUPPORT cùng người
→ chỉ 1 participant
```

```text
Minutes edit
remove source participant
→ Sync
→ participant quay lại đúng row cũ
→ Save không mất người
```

Không cần migration SQL.
Không thay đổi database schema.
Không giảm security validation hiện có.
