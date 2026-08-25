# PEMS — Explicit MEMBER / EXTERNAL Operational Contact Implementation Plan

## 1. Mục tiêu

Triển khai lại UX phần **Đầu mối phối hợp** trong Visit Request V2 theo hướng:

> Người dùng phải xác định rõ đầu mối là:
>
> - **MEMBER** — một người thuộc Danh sách khách hoặc Nhân sự hỗ trợ của chính campus đó; hoặc
> - **EXTERNAL** — một người không đi cùng đoàn / không nằm trong hai danh sách trên.

Mục tiêu chính là giải quyết vấn đề hiện tại:

```text
operationalContactClientMemberKey = null
```

đang có thể mang nhiều nghĩa khác nhau:

```text
1. Người dùng chưa chọn gì.
2. Người dùng cố ý chọn một đầu mối ngoài đoàn.
3. Member từng được chọn nhưng đã bị xóa.
4. Excel Replace / Apply-to-all làm mất member relation.
5. Quick-fill vừa clear relation.
6. Draft cũ restore lại trạng thái không còn đủ nghĩa.
```

Sau khi triển khai, frontend phải phân biệt rõ **ý định của người dùng**, nhưng **không thay đổi API contract, backend lifecycle hoặc database schema hiện tại**.

---

# 2. Source baseline đã xác minh

Repository:

```text
quangthoai04/PEMS
```

Branch baseline gần nhất đã đọc:

```text
Dev
```

HEAD baseline:

```text
dce2e276349a5ae0d9228ba140a85acac287c23d
```

Trước khi triển khai phải kiểm tra lại:

```bash
git status --short --branch
git log -1 --oneline
```

Nếu HEAD đã thay đổi:

- đọc lại toàn bộ file affected tại HEAD mới;
- không dùng line number cũ;
- source hiện tại là nguồn xác minh chính.

---

# 3. Kết luận kiến trúc hiện tại cần giữ nguyên

Backend hiện tại đã hỗ trợ hai trường hợp:

## 3.1. Contact là member

Frontend gửi:

```json
{
  "operationalContactClientMemberKey": "member-key-A"
}
```

Backend:

```text
clientMemberKey
   ↓
OperationalContactLink
   ↓
resolve đúng member
   ↓
OperationalContactGuestMemberId != null
```

## 3.2. Contact ngoài đoàn

Frontend gửi:

```json
{
  "operationalContactClientMemberKey": null
}
```

Backend giữ:

```text
OperationalContactGuestMemberId = null
```

và vẫn sử dụng contact snapshot:

```text
fullName
organization
jobTitle
phone
email
```

## 3.3. Quyền runtime vẫn không đổi

```text
OperationalContactUserId
```

vẫn là account thực sự có quyền vận hành campus sau confirmation.

Không thay đổi:

```text
Create confirmation
Accept / Decline
Replace
Transfer
Reinvite
Resend
Approval gate
Notifications
Host logic
```

---

# 4. Nguyên tắc triển khai

Không thêm backend business state.

Không thêm DB enum.

Không thêm DB column.

Không gửi `operationalContactSource` xuống API.

`operationalContactSource` chỉ là **frontend form intent**.

Dùng type:

```ts
export type OperationalContactSource =
  | 'MEMBER'
  | 'EXTERNAL'
  | null;
```

Trong form schema của mỗi campus:

```ts
operationalContactSource:
  z.enum(['MEMBER', 'EXTERNAL'])
    .nullable()
```

Semantics:

```text
null
= user chưa xác định loại đầu mối

MEMBER
= contact phải là exactly one member của campus

EXTERNAL
= contact ngoài delegation
```

---

# 5. UI cuối cùng

## 5.1. Fresh / Create campus

Hiển thị:

```text
Đầu mối phối hợp *

○ Người trong đoàn
○ Người không đi cùng đoàn
```

Không hiển thị một contact form đầy đủ trước khi user chọn source.

---

# 6. MEMBER mode

Nếu user chọn:

```text
● Người trong đoàn
```

hiển thị:

```text
Chọn đầu mối *
[ Chọn đầu mối ▼ ]

Nguyễn Văn A — Khách
Trần Văn B — Khách
Lê Văn C — Nhân sự hỗ trợ
```

Eligible members:

```text
visitors
+
supportTeam
```

Không có:

```text
internal host
registrant-only pseudo-member
auto-generated row
```

Support Team vẫn hợp lệ.

---

# 7. MEMBER identity invariant

Khi:

```text
operationalContactSource = 'MEMBER'
```

thì:

```text
operationalContactClientMemberKey
```

phải resolve **EXACTLY ONE** row trong:

```ts
[
  ...visitors,
  ...supportTeam
]
```

Không dùng:

```ts
.some(...)
```

để kết luận identity hợp lệ.

Không dùng:

```ts
.find(...)
```

để silently chọn row đầu tiên.

Phải dùng:

```ts
const matches = eligibleMembers.filter(
  m =>
    !!m.clientMemberKey &&
    m.clientMemberKey === selectedKey
);

if (matches.length !== 1) {
  // invalid
}
```

Cases:

```text
0 match
→ invalid

1 match
→ valid

>1 matches
→ invalid / ambiguous
```

---

# 8. MEMBER selected snapshot

Khi member A được chọn:

```text
operationalContactClientMemberKey = A.key
```

ba field sau lấy từ member A:

```text
fullName
organization
jobTitle
```

UI hiển thị readonly.

Ví dụ:

```text
Họ tên:       Nguyễn Văn A       [readonly]
Đơn vị:       ABC Corporation    [readonly]
Chức vụ:      Director           [readonly]
```

Hai field sau vẫn editable:

```text
Số điện thoại
Email
```

Lý do:

`VisitGuestMember` hiện không chứa phone/email.

---

# 9. EXTERNAL mode

Nếu user chọn:

```text
● Người không đi cùng đoàn
```

thì:

```ts
operationalContactSource = 'EXTERNAL'
operationalContactClientMemberKey = null
```

Hiển thị free-text contact form:

```text
Họ tên *
Đơn vị *
Chức vụ *
Số điện thoại
Email *
```

Không:

```text
auto add member
auto select member
guess same person
```

Backend payload vẫn giữ contract hiện tại.

---

# 10. Chuyển source: MEMBER ↔ EXTERNAL

Đây là thao tác thay đổi identity và có thể làm mất dữ liệu.

Không mutate trước khi confirmation nếu dữ liệu sẽ bị xóa.

---

## 10.1. MEMBER → EXTERNAL

Ví dụ:

```text
MEMBER A
phone = 090111...
email = a@gmail.com
```

User chọn:

```text
EXTERNAL
```

Recommended behavior:

```text
Confirm:
"Chuyển sang đầu mối không đi cùng đoàn sẽ bỏ liên kết với thành viên hiện tại.
Bạn có muốn tiếp tục?"
```

### Confirm

```text
source = EXTERNAL
key = null
```

Name/org/job:

Có hai lựa chọn implementation hợp lệ:

### Recommended

Clear member-derived identity:

```text
fullName = ''
organization = ''
jobTitle = ''
```

Phone/email:

Có thể giữ nếu sản phẩm muốn giúp user nhập lại nhanh,
nhưng cần hiểu chúng không còn được xác định thuộc A.

Để tránh identity contamination, phương án an toàn hơn:

```text
clear fullName
clear organization
clear jobTitle
clear phone
clear email
```

### Cancel

ZERO MUTATION.

---

## 10.2. EXTERNAL → MEMBER A

Nếu external contact có data:

```text
Nguyễn Văn D
d@gmail.com
```

và user chọn:

```text
MEMBER A
```

phải confirmation trước.

### Confirm

```text
source = MEMBER
key = A.key

fullName = A.fullName
organization = A.organization
jobTitle = A.jobTitle

phone = ''
email = ''
```

### Cancel

ZERO MUTATION.

---

# 11. MEMBER A → MEMBER B

Nếu:

```text
A selected
```

và user chọn B.

## 11.1. Nếu phone/email trống

Có thể apply ngay:

```text
key = B.key
name/org/job = B
```

## 11.2. Nếu phone hoặc email có dữ liệu

Phải confirm.

Confirm:

```text
key = B.key

fullName = B.fullName
organization = B.organization
jobTitle = B.jobTitle

phone = ''
email = ''
```

Cancel:

```text
A vẫn selected
A phone/email giữ nguyên
```

Không để:

```text
B + A.phone + A.email
```

---

# 12. NO KEY + MEMBER source edge case

Không được giả định:

```text
key = null
=> phone/email chắc chắn rỗng
```

Có thể xảy ra:

```text
source = MEMBER
key = null
phone/email != empty
```

do:

```text
member bị xóa
Excel Replace
Apply-to-all
stale key repair
old draft
```

Nếu user chọn member B:

```text
source = MEMBER
key = null
phone/email có data
```

thì vẫn phải confirm trước.

Confirm:

```text
key = B
name/org/job = B
phone/email = ''
```

Cancel:

```text
source vẫn MEMBER
key vẫn null
old phone/email giữ nguyên
form vẫn invalid
```

---

# 13. Centralize member-change transaction

Không rải logic đổi member ở nhiều handler.

Tạo centralized functions tương đương:

```ts
requestMemberChange(...)
applyMemberChange(...)
requestSourceChange(...)
applySourceChange(...)
```

Example:

```ts
type MemberChangeSource =
  | 'DROPDOWN'
  | 'REGISTRANT_QUICK_FILL';

type PendingMemberChange = {
  targetKey: string;
  source: MemberChangeSource;
};
```

`requestMemberChange` phải:

```text
1. resolve target exactly once
2. same-key → no-op
3. check destructive contact data
4. nếu cần confirmation → store pending only
5. nếu không → apply
```

`applyMemberChange` phải:

```text
1. resolve target exactly once
2. set source = MEMBER
3. set key
4. sync name/org/job ngay lập tức
5. clear phone/email nếu identity changed
6. success feedback theo action source
```

Không chỉ set key rồi chờ `useEffect` sync sau.

Existing sync effect vẫn giữ để:

```text
selected member row được chỉnh
→ contact snapshot name/org/job follow member
```

---

# 14. Quick action — “Đầu mối là người đăng ký”

Giữ quick action nhưng semantics mới phải rõ ràng.

Matching identity:

```text
fullName
+
jobTitle
+
organization
```

Không dùng name-only.

---

## 14.1. Exactly 1 member match

```text
Registrant = member A
```

thì:

```text
source = MEMBER
requestMemberChange(A.key, 'REGISTRANT_QUICK_FILL')
```

Nếu cần confirm:

```text
không toast trước confirm
```

Confirm xong mới success toast.

---

## 14.2. Zero member match

Registrant không nằm trong delegation.

Thì:

```text
source = EXTERNAL
key = null
```

và copy:

```text
fullName
organization
jobTitle
phone
email
```

Không:

```text
auto-add visitor
auto-add support
```

---

## 14.3. More than one match

Không tự `.find()` lấy row đầu.

Hiển thị message:

```text
Thông tin người đăng ký trùng với nhiều thành viên.
Vui lòng chọn đầu mối trực tiếp từ danh sách.
```

Không mutate form.

---

# 15. Stale member guard

Current code đã có guard clear stale key.

Sau sửa:

Nếu:

```text
source = MEMBER
key = A
```

và A biến mất:

```text
source = MEMBER
key = null
```

Không tự:

```text
source = EXTERNAL
```

vì user chưa quyết định external.

Phone/email:

```text
KEEP temporarily
```

để tránh silent data loss.

Form phải invalid.

UI message:

```text
Đầu mối đã chọn không còn trong danh sách đoàn.
Vui lòng chọn lại đầu mối.
```

---

# 16. Excel replace

Critical regression:

```text
A selected
↓
Excel Replace xóa A
↓
source = MEMBER
key = null
phone/email giữ
↓
form invalid
↓
user chọn B
↓
confirmation nếu phone/email còn
```

Không:

```text
auto external
auto choose another member
auto clear phone/email
```

---

# 17. Copy campus

Current copy/remint semantics phải giữ.

## MEMBER

Source:

```text
source = MEMBER
key = A
```

Copy sang campus khác:

```text
source = MEMBER
member rows mint fresh key
A → A'
contact key → A'
```

Nếu remap fail:

```text
source = MEMBER
key = null
```

Destination invalid và user chọn lại.

Không fallback bằng name.

## EXTERNAL

Source:

```text
source = EXTERNAL
key = null
contact snapshot = D
```

Copy:

```text
source = EXTERNAL
key = null
snapshot copied
```

---

# 18. Apply-to-all

Reuse same copy/remint logic.

Không tạo implementation riêng.

Member relation:

```text
source MEMBER
→ remap selected member

source EXTERNAL
→ keep external snapshot
```

---

# 19. withMemberKeys()

Current logic phải tiếp tục:

```text
mint missing member keys
translate legacy visitor index
repair stale selected key
```

Nhưng identity validity phải exact-count.

Không:

```ts
some(...)
```

Mà:

```ts
const matches = members.filter(
  m => m.clientMemberKey === picked
);

valid = matches.length === 1;
```

Nếu:

```text
0
hoặc
>1
```

thì:

```text
key = null
```

Không tự đổi source.

---

# 20. Payload builder

`operationalContactSource` là UI-only.

Không gửi:

```json
{
  "operationalContactSource": "MEMBER"
}
```

Payload hiện tại vẫn dùng:

```text
operationalContactClientMemberKey
operationalContactGuestMemberId
operationalContact snapshot
```

---

## 20.1. MEMBER payload

Source MEMBER:

```text
selected key phải resolve exactly 1
```

emit:

```text
operationalContactClientMemberKey = key
```

Nếu invalid:

```text
schema phải block submit trước API
```

Builder vẫn phải fail-safe:

```text
ambiguous/stale
→ do not silently pick first
```

---

## 20.2. EXTERNAL payload

Source EXTERNAL:

```text
operationalContactClientMemberKey = null
operationalContactGuestMemberId = null
```

Snapshot gửi như hiện tại.

---

# 21. operationalContactGuestMemberId derivation

Không dùng `.find()` nếu key có thể duplicate.

Resolve exact one.

Pseudo:

```ts
const matches = members.filter(
  m => m.clientMemberKey === selectedKey
);

const guestMemberId =
  matches.length === 1
    ? matches[0].guestMemberId ?? null
    : null;
```

---

# 22. Validation

Fresh campus only.

Required:

```text
operationalContactSource != null
```

## MEMBER

```text
source == MEMBER
→ selected key required
→ exact one member required
```

Error path:

```text
operationalContactClientMemberKey
```

## EXTERNAL

```text
source == EXTERNAL
→ key must be null
→ existing operationalContact completeness validation applies
```

Existing campus:

```text
do not require new source selector
```

---

# 23. Existing campus / Edit flow

Không mở rộng business scope.

Existing campus:

```text
visitInstanceId != null
```

nên:

```text
không render MEMBER / EXTERNAL selector mới
```

Giữ current:

```text
read-only contact snapshot
relation-only repair where existing code supports it
```

Không thay:

```text
VisitRequestV2EditService
campus-set immutability
contact snapshot immutability
```

---

# 24. useContactLinkPrompt

Đây là affected logic bắt buộc.

Current hook hỏi:

```text
"Đầu mối vừa nhập có phải cùng người với member này không?"
```

Sau explicit source:

## MEMBER

User đã chọn member rõ ràng.

```text
do not prompt
```

## EXTERNAL

User đã trả lời:

```text
"Người không đi cùng đoàn"
```

nên:

```text
do not prompt
```

Không được hỏi lại:

```text
"Cùng một người hay hai người khác nhau?"
```

## Legacy / no-source

Hook chỉ nên giữ compatibility cho trạng thái legacy nếu thật sự còn path tạo ra nó.

Do not delete hook blindly because it is shared with edit/create code.

Audit all call sites before simplifying.

---

# 25. Draft compatibility

Current draft version baseline:

```text
V2_DRAFT_SCHEMA_VERSION = 3
```

Không bump version nếu có thể migrate an toàn.

Old draft không có:

```text
operationalContactSource
```

Phải infer khi restore.

---

## 25.1. Old draft valid member key

Raw draft:

```text
key = A
```

=> infer:

```text
source = MEMBER
```

Sau đó repair key.

Nếu key stale:

```text
source vẫn MEMBER
key = null
```

Không infer thành EXTERNAL sau repair.

---

## 25.2. Old draft no key + contact has data

```text
key = null
contact has name/email/...
```

=> infer:

```text
source = EXTERNAL
```

---

## 25.3. Old draft no key + no contact data

=> infer:

```text
source = null
```

---

# 26. Draft meaningful-data detection

Nếu user chỉ chọn:

```text
source = MEMBER
```

hoặc:

```text
source = EXTERNAL
```

thì đó là user intent đáng được lưu.

Audit:

```text
hasMeaningfulV2Data
campusVisitHasContent
```

để không xảy ra:

```text
user chọn source
→ đóng modal
→ draft báo "nothing to save"
```

Source selection phải được tính là meaningful form content nếu nó khác default `null`.

---

# 27. Draft schema version decision

Preferred:

```text
keep version 3
```

nếu restore layer có thể infer safely.

Chỉ bump nếu source shape change khiến old draft không thể được restore an toàn.

Nếu bump:

```text
không destructive discard old draft
```

phải có migration.

---

# 28. i18n

Không hard-code text.

Add/update VI/EN keys.

Suggested keys:

```text
contact.sourceLabel
contact.sourceMember
contact.sourceExternal
contact.memberPlaceholder
contact.memberRequired
contact.memberInvalid
contact.externalHint
contact.memberHint
contact.memberLost
contact.switchToMemberConfirm
contact.switchToExternalConfirm
contact.changeMemberConfirm
contact.registrantAmbiguous
```

Exact naming follow existing convention.

---

# 29. Accessibility

Source selector phải:

```text
keyboard accessible
aria-invalid
visible error
```

Member dropdown:

```text
label
aria-invalid
data-field-error="true"
inline error
```

`focusFirstInvalidField()` phải focus đúng selector/dropdown.

Không chỉ hiện:

```text
"Còn 1 trường cần kiểm tra"
```

mà không chỉ vị trí.

---

# 30. Do NOT change backend

Expected backend diff:

```text
NONE
```

Không sửa:

```text
CreateVisitRequestV2CommandValidator
VisitRequestV2CreateService
OperationalContactLink
ReplaceOperationalContactCommandHandler
InitiateOperationalContactTransferCommandHandler
AcceptOperationalContactConfirmationCommandHandler
OperationalContactContracts
VisitInstanceFormDetail
VisitGuestMember
```

Nếu implementation chứng minh backend change là bắt buộc:

STOP và báo:

```text
1. reason
2. source evidence
3. exact affected contract
4. why frontend-only is insufficient
```

Không tự mở scope.

---

# 31. Replace / Transfer

Giữ nguyên current business behavior.

Current detail screen vẫn có thể:

```text
Replace contact
Transfer contact
```

bằng free-text identity + email.

Điều này phù hợp vì business model cuối cùng vẫn cho phép:

```text
MEMBER
hoặc
EXTERNAL
```

Không bắt Replace/Transfer phải chọn member.

---

# 32. Confirmation lifecycle

Không đổi:

```text
email invitation
Accept
Decline
Cancel
Reinvite
Resend
```

Không đổi:

```text
OperationalContactUserId
OperationalContactConfirmedAt
OperationalContactConfirmationSource
```

Không đổi status transitions.

---

# 33. Database

Không migration.

Không thêm:

```text
operational_contact_source
```

Không đổi nullable:

```text
operational_contact_guest_member_id
```

Representation hiện tại vẫn đủ:

```text
MEMBER
→ guestMemberId != null

EXTERNAL
→ guestMemberId = null
```

---

# 34. Main files expected to change

Frontend only.

Likely:

```text
features/visit-request/components/v2/CampusVisitCard.tsx
features/visit-request/schema/visitRequestV2.schema.ts
features/visit-request/utils/visitRequestV2Form.ts
features/visit-request/hooks/useContactLinkPrompt.ts
features/visit-request/hooks/useVisitRequestFormV2.ts
features/visit-request/utils/visitRequestV2DraftStorage.ts
shared/i18n/locales/vi/visitRequestV2.json
shared/i18n/locales/en/visitRequestV2.json
Visit Request frontend tests
```

Exact paths must be verified before edit.

---

# 35. Required frontend tests

At minimum:

## Source selection

- [ ] New campus source defaults `null`.
- [ ] Source null blocks submit.
- [ ] MEMBER can be selected.
- [ ] EXTERNAL can be selected.
- [ ] Source selector error is visible.
- [ ] Focus-first-invalid reaches source/member control.

## MEMBER

- [ ] MEMBER requires member key.
- [ ] Zero-match key invalid.
- [ ] Duplicate/ambiguous key invalid.
- [ ] Guest member selectable.
- [ ] Support member selectable.
- [ ] Name/org/job readonly after select.
- [ ] Phone editable.
- [ ] Email editable.
- [ ] Same-member selection is no-op.

## EXTERNAL

- [ ] EXTERNAL key is null.
- [ ] Full manual contact fields editable.
- [ ] Existing contact validation still applies.
- [ ] Payload remains backend-compatible.

## Identity switch

- [ ] A → B empty phone/email applies.
- [ ] A → B with phone confirms.
- [ ] A → B with email confirms.
- [ ] Cancel is zero mutation.
- [ ] Confirm clears A phone/email.
- [ ] B gets B name/org/job immediately.
- [ ] A data never leaks into B.

## Source switch

- [ ] EXTERNAL → MEMBER with existing data confirms.
- [ ] MEMBER → EXTERNAL confirms if destructive.
- [ ] Cancel source change zero mutation.
- [ ] Confirm yields internally consistent state.

## Quick fill

- [ ] Registrant exactly matches 1 member → MEMBER.
- [ ] Registrant zero member match → EXTERNAL.
- [ ] Registrant ambiguous → no mutation.
- [ ] No auto-add guest.
- [ ] No auto-add support.
- [ ] No name-only match.
- [ ] Success toast after actual apply only.

## Stale member

- [ ] Deleting selected A clears key.
- [ ] Source remains MEMBER.
- [ ] Phone/email preserved.
- [ ] Form becomes invalid.
- [ ] Selecting B afterwards confirms if old contact data exists.
- [ ] No automatic EXTERNAL conversion.

## Excel

- [ ] Guest Excel replace removing selected member clears key.
- [ ] Support Excel replace same.
- [ ] Replace Both same.
- [ ] No auto-selection.
- [ ] Old contact data never leaks to replacement member.

## Copy / Apply-to-all

- [ ] MEMBER key remaps to copied member.
- [ ] Failed remap → MEMBER + null key.
- [ ] EXTERNAL snapshot copies with null key.
- [ ] Apply-to-all follows same semantics.

## withMemberKeys

- [ ] valid key retained.
- [ ] stale key cleared.
- [ ] duplicate key cleared.
- [ ] source not silently changed.
- [ ] legacy visitor-index translation still works.

## Draft

- [ ] v3 draft with member key restores MEMBER.
- [ ] stale member draft restores MEMBER + null key.
- [ ] null key + contact data restores EXTERNAL.
- [ ] empty contact restores source null.
- [ ] selecting only source counts as meaningful data.
- [ ] OTP/session safety unaffected.

## ContactLinkPrompt

- [ ] MEMBER does not trigger prompt.
- [ ] EXTERNAL does not trigger prompt.
- [ ] explicit user choice is not asked again.
- [ ] legacy behavior only remains where needed.
- [ ] edit flow regression remains green.

## API contract

- [ ] `operationalContactSource` never appears in request payload.
- [ ] MEMBER payload carries member key.
- [ ] EXTERNAL payload carries null key.
- [ ] `operationalContactGuestMemberId` derivation uses exact-one semantics.

## Existing flows

- [ ] Public create.
- [ ] OTP create.
- [ ] Authenticated create.
- [ ] Staff/Staff Leader short-notice rule unaffected.
- [ ] Existing edit.
- [ ] Replace contact.
- [ ] Transfer contact.
- [ ] Confirmation UI.
- [ ] Detail view.

---

# 36. Commands to run

Before implementation:

```bash
git status --short --branch
git log -1 --oneline
```

During implementation:

```bash
npm run lint
```

Run focused Vitest files.

At end:

```bash
npm run lint
npx vitest run src/features/visit-request/__tests__/
npm run test:unit
npm run audit:responsive
npm run build
```

Use actual repository commands if paths differ.

If a command fails because path/filter is wrong:

```text
locate actual config
rerun equivalent test
do not skip
```

---

# 37. Explicit regression protection

Before DONE confirm:

```text
Backend changed: NO
API contract changed: NO
Database changed: NO
Migration added: NO

Replace behavior changed: NO
Transfer behavior changed: NO
Confirmation lifecycle changed: NO
Approval logic changed: NO
Notification logic changed: NO
72h rule changed: NO
30-minute rule changed: NO
200-member cap changed: NO
```

---

# 38. Final self-review checklist

Answer all:

1. Can fresh user submit without choosing MEMBER/EXTERNAL?
   - Expected: NO.

2. Can MEMBER submit with null key?
   - Expected: NO.

3. Can MEMBER key point to zero members?
   - Expected: NO.

4. Can MEMBER key point to more than one row?
   - Expected: NO.

5. Can Guest be selected?
   - Expected: YES.

6. Can Support be selected?
   - Expected: YES.

7. Can EXTERNAL still be used?
   - Expected: YES.

8. Does EXTERNAL send key null?
   - Expected: YES.

9. Is `operationalContactSource` sent to backend?
   - Expected: NO.

10. Can stale MEMBER silently become EXTERNAL?
    - Expected: NO.

11. Can member A phone/email leak into B?
    - Expected: NO.

12. Does Cancel mutate?
    - Expected: NO.

13. Does quick-fill zero-match become EXTERNAL?
    - Expected: YES.

14. Does ambiguous quick-fill choose first?
    - Expected: NO.

15. Does ContactLinkPrompt ask again after explicit source?
    - Expected: NO.

16. Do old drafts restore without destructive loss?
    - Expected: YES.

17. Does Replace still work?
    - Expected: YES.

18. Does Transfer still work?
    - Expected: YES.

19. Is database schema unchanged?
    - Expected: YES.

20. Did all tests actually run?
    - Expected: YES.

If any answer is unknown:

```text
continue investigation
do not report DONE
```

---

# 39. Final report format

Agent report must contain:

## A. Pre-flight

```text
repo
branch
HEAD
working tree
```

## B. Source verification

Explain current existing semantics:

```text
member key
guest member id
external null relation
OperationalContactUserId
```

## C. Files changed

Exact list.

## D. UI state implementation

Explain:

```text
null
MEMBER
EXTERNAL
```

## E. Transition matrix

Report actual behavior for:

```text
null → MEMBER
null → EXTERNAL
MEMBER A → MEMBER B
MEMBER → EXTERNAL
EXTERNAL → MEMBER
stale MEMBER
```

## F. Quick-fill

Report:

```text
one match
zero match
ambiguous
```

## G. Draft

Explain migration/inference.

## H. ContactLinkPrompt

Explain why no duplicate question remains.

## I. Payload

Show examples:

```text
MEMBER payload
EXTERNAL payload
```

and prove:

```text
operationalContactSource not sent
```

## J. Regression scope

Explicit:

```text
Backend changed: NO
Database changed: NO
API contract changed: NO
Replace changed: NO
Transfer changed: NO
```

## K. Tests

Exact commands + PASS/FAIL counts.

## L. Deviations

Any implementation deviation from plan with source evidence.

## M. Residual risks

Do not claim zero risk.

If none found in tested scope:

```text
No known residual regression identified within the tested scope.
```

---

# 40. Commit rule

Do not commit.

Do not push.

Do not merge.

Implementation + tests + report only.

Wait for explicit commit instruction.

---

# 41. Recommended implementation order

```text
1. Pre-flight.
2. Refresh current source.
3. Add form-only OperationalContactSource type.
4. Add fresh-campus schema validation.
5. Update CampusVisitCard UI.
6. Centralize source/member transition logic.
7. Fix A→B phone/email protection.
8. Update quick-fill semantics.
9. Update stale member behavior.
10. Update exact-one identity helpers.
11. Update copy/remint/apply-to-all.
12. Update payload exact-one derivation.
13. Update ContactLinkPrompt behavior.
14. Implement draft source inference/migration.
15. Update VI/EN.
16. Focused tests.
17. Full Visit Request tests.
18. Full frontend tests.
19. Responsive audit.
20. Build.
21. Final diff audit.
22. Self-review.
23. Final report.
```

---

# 42. Kết luận

Phương án này cố ý giữ nguyên architecture hiện tại:

```text
MEMBER
→ linked delegation member

EXTERNAL
→ unlinked contact snapshot
```

Thay đổi chính là:

> frontend không còn dùng `key = null` để vừa biểu diễn "chưa chọn" vừa biểu diễn "external".

Thay vào đó:

```text
source = null
→ chưa chọn

source = MEMBER
→ phải chọn chính xác một member

source = EXTERNAL
→ người không đi cùng đoàn
```

Đây là phương án có phạm vi sửa nhỏ, giữ được Replace/Transfer/Confirmation hiện tại và giảm đáng kể ambiguity trong form mà không mở rộng backend hoặc database scope.
