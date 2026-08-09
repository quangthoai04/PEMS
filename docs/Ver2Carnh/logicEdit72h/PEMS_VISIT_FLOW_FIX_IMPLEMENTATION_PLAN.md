# PEMS — IMPLEMENTATION PLAN: VISIT DETAIL, CONTACT HISTORY, AMENDMENT, EXPENSE & MULTI-CAMPUS UX

## 1. Mục tiêu

Hoàn thiện các lỗi và khoảng trống logic đã xác định trong các màn:

- Chi tiết đơn V2 / lịch sử thay đổi
- Quản lý đầu mối theo campus
- Đề xuất thay đổi sau duyệt
- Tab `Sau tiếp khách`
- Bảng chi phí
- UI nhiều campus

Nguyên tắc:

- Ít thay đổi nhất, giữ architecture hiện tại.
- Không thay đổi database schema nếu không thực sự bắt buộc.
- Backend vẫn là source of truth cho quyền, lifecycle và business rule.
- Frontend phải chặn lỗi có thể xác định trước để tránh gọi API vô ích.
- Không dùng GET có side effect tạo dữ liệu.
- Không trộn workflow thay đổi nội dung chuyến thăm với workflow identity/xác nhận/chuyển giao đầu mối.

---

# 2. Phạm vi triển khai

Triển khai theo 5 phase:

1. Fix tab `Sau tiếp khách` và expense report lifecycle.
2. Fix quản lý đầu mối + history refresh/detail.
3. Fix snapshot lịch sử `Trước thay đổi → Sau thay đổi`.
4. Fix amendment validation + tách identity đầu mối.
5. Thêm accordion cho multi-campus.

---

# PHASE 1 — SAU TIẾP KHÁCH / EXPENSE REPORT

## 3. Vấn đề hiện tại

Khi bấm tab `Sau tiếp khách`, `VisitAfterTab` mount `GeneralExpensePanel`.

`GeneralExpensePanel` gọi ngay:

```text
GET /VisitExpenses/general/{visitInstanceId}
```

Nhưng endpoint GET hiện đang chạy logic `GetOrCreateGeneralExpenseReport`.

Nếu report chưa tồn tại:

```text
GET
→ backend cố tạo report
→ instance.status != AFTER_VISIT
→ 403
→ "Can only initialize expense report in AFTER_VISIT state."
→ frontend toast.error()
```

Hậu quả:

- Chỉ mở tab cũng tạo request lỗi.
- GET đang có side effect.
- Có thể xuất hiện nhiều toast giống nhau.
- `CLOSED` + chưa có report cũng lỗi dù người dùng chỉ xem lại.

---

## 4. Logic đích

### 4.1 Read và Initialize phải tách riêng

Đổi contract:

```text
GET /api/VisitExpenses/general/{visitInstanceId}
→ chỉ đọc report hiện có
→ không tạo dữ liệu

POST /api/VisitExpenses/general/{visitInstanceId}/initialize
→ chỉ Host được tạo
→ chỉ khi instance = AFTER_VISIT
→ idempotent
```

### 4.2 Lifecycle

```text
BEFORE_VISIT
→ không initialize
→ nếu mở tab do read-only/history thì chỉ hiện trạng thái chưa khả dụng

DURING_VISIT
→ không initialize

AFTER_VISIT
→ Host có thể initialize nếu chưa có report
→ nếu report có rồi thì đọc bình thường

CLOSED
→ chỉ đọc report đã tồn tại
→ nếu chưa có report thì hiện empty/read-only
→ tuyệt đối không tạo mới
```

### 4.3 Không dùng toast cho trạng thái hợp lệ

Các case sau không phải lỗi hệ thống:

```text
report chưa tồn tại
report chưa được phép khởi tạo
CLOSED nhưng không có report
```

UI phải render empty state / disabled state thay vì toast lỗi.

---

## 5. File cần sửa

### Backend

#### `backend/PEMS.Api/Controllers/VisitExpensesController.cs`

Tách endpoint read/init:

```text
GET  /general/{visitInstanceId}
POST /general/{visitInstanceId}/initialize
```

Không dùng command tạo dữ liệu cho GET.

#### `backend/PEMS.Application/Delegations/VisitExpenses/Commands/GetOrCreateGeneralExpenseReport/...`

Tách thành:

```text
GetGeneralExpenseReportQuery
InitializeGeneralExpenseReportCommand
```

hoặc giữ tên class cũ nội bộ nhưng tuyệt đối không expose qua GET theo semantics create.

### Frontend

#### `frontend/pems-react/src/services/visit-expense.service.ts`

Tách:

```ts
getGeneralExpenseReport()
initializeGeneralExpenseReport()
```

#### `frontend/pems-react/src/pages/dashboard/visit/GeneralExpensePanel.tsx`

Không gọi create trong `useEffect`.

Load theo thứ tự:

```text
getExpenseSummary
getGeneralExpenseReport
getInstanceLogistics
```

Nếu chưa có general report:

```text
AFTER_VISIT + Host + editable
→ hiện CTA "Khởi tạo bảng chi phí"

CLOSED / readonly
→ "Chưa có bảng chi phí"

lifecycle khác
→ không initialize
```

Dedupe toast:

```ts
toast.error(message, {
  id: `expense-load-${visitInstanceId}`,
});
```

Nhưng ưu tiên là không gọi API sai lifecycle.

#### `frontend/pems-react/src/pages/dashboard/visit/VisitAfterTab.tsx`

Nhận thêm:

```ts
instanceStatus?: string;
```

#### `frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx`

Truyền:

```tsx
<VisitAfterTab
  visitInstanceId={perm?.visitInstanceId}
  instanceStatus={perm?.instanceStatus}
  ...
/>
```

---

## 6. Error contract

Không trả prose tiếng Anh trực tiếp cho UI.

Thêm stable codes nếu cần:

```text
EXPENSE_REPORT_NOT_FOUND
EXPENSE_REPORT_NOT_INITIALIZABLE
EXPENSE_REPORT_HOST_REQUIRED
EXPENSE_REPORT_READ_FORBIDDEN
```

Frontend map VI/EN.

---

# PHASE 2 — QUẢN LÝ ĐẦU MỐI

## 7. Không sửa gì nhưng vẫn bấm Lưu

### Vấn đề

Flow hiện tại:

```text
Mở "Chỉnh sửa đầu mối"
→ không sửa gì
→ Lưu
→ frontend vẫn gọi API
→ backend trả PROFILE_NO_CHANGES
→ toast/error
```

### Logic đích

Frontend compare original/current:

```text
fullName
organization
jobTitle
phone
email
```

Normalize:

```text
trim
phone normalize nếu cần
email lower-case nếu business rule đang dùng case-insensitive
```

Nếu không thay đổi:

```text
disable nút Lưu
```

hoặc:

```text
Lưu
→ đóng modal
→ không gọi API
```

Backend vẫn giữ `NO_CHANGES` guard.

### File

```text
frontend/.../ContactIdentityActions.tsx
```

---

## 8. Phân biệt INITIAL_CONFIRMATION và TRANSFER

### Vấn đề

UI đang dùng chung:

```text
"Hủy lời mời chuyển giao"
```

trong cả trường hợp đầu mối chưa xác nhận ban đầu.

### Logic đích

```text
INITIAL_CONFIRMATION
→ "Hủy lời mời xác nhận"

TRANSFER
→ "Hủy lời mời chuyển giao"
```

Không thay đổi backend semantics.

---

## 9. Refresh History sau thay đổi đầu mối

### Vấn đề

Sau:

```text
Sửa đầu mối
Gửi lời mời
Gửi lại
Hủy
Accept
Decline
```

parent chỉ reload detail request.

`VisitHistoryTimeline` có state/load riêng nên không cập nhật.

### Logic đích

Trong `VisitRequestV2DetailView.tsx`:

```ts
const [historyRefreshKey, setHistoryRefreshKey] = useState(0);

const reloadAfterContactMutation = async () => {
  await load();
  setHistoryRefreshKey(v => v + 1);
};
```

Truyền:

```tsx
<VisitHistoryTimeline
  visitRequestId={data.visitRequestId}
  refreshKey={historyRefreshKey}
/>
```

History reload khi:

```text
visitRequestId thay đổi
hoặc refreshKey thay đổi
```

---

## 10. Identity History phải có nút mắt

### Vấn đề

Frontend chỉ hiện icon Eye khi `eventId` tồn tại.

Backend identity history hiện không trả đủ:

```text
eventId
visitInstanceId
campus
```

### Logic đích

Identity event trả:

```text
eventId
source = IDENTITY_CHANGE
visitRequestId
visitInstanceId
campusId/campusName
eventType
fromStatus
toStatus
maskedEmail
actor
occurredAt
reason
```

Frontend có thể mở drawer chi tiết.

### Security

Không trả:

```text
full token
raw invitation token
pending_snapshot_json
credential data
full email nếu policy yêu cầu mask
```

Scope identity history theo đúng `visibleInstanceIds`.

Operational contact chỉ xem history campus được cấp quyền.

---

## 11. Wording lịch sử đầu mối

Không gom tất cả về:

```text
"Vai trò đầu mối liên hệ có thay đổi"
```

Map riêng:

```text
INITIAL_CONFIRMATION_CREATED
→ Đã gửi lời mời xác nhận đầu mối tới ...

TRANSFER_REQUESTED
→ Đã gửi lời mời chuyển giao đầu mối tới ...

INVITATION_RESENT
→ Đã gửi lại lời mời ...

INVITATION_CANCELLED
→ Đã hủy lời mời ...

TRANSFER_ACCEPTED
→ Đã chuyển giao đầu mối sang ...

TRANSFER_DECLINED
→ Người được mời đã từ chối nhận vai trò đầu mối.

INVITATION_EXPIRED
→ Lời mời đầu mối đã hết hạn.
```

---

# PHASE 3 — HISTORY SNAPSHOT / BEFORE → AFTER

## 12. Vấn đề

Drawer đang hiển thị:

```text
(trống) → giá trị mới
```

trong khi dữ liệu cũ thực tế không trống.

Nguyên nhân:

- snapshot schema giữa các write path không đồng nhất;
- legacy snapshot có nested object;
- snapshot mới có flat field;
- một số baseline test/seed có `{}`.

---

## 13. Canonical snapshot

Tạo một builder dùng chung, ví dụ:

```text
VisitFormRevisionSnapshotBuilder
```

Tất cả write path phải dùng:

```text
CREATE
PENDING_EDIT
RESUBMIT
SAFE_EDIT
AMENDMENT_APPLIED
```

Không tự serialize anonymous object ở từng service.

Canonical structure nên ổn định, ví dụ:

```json
{
  "delegationName": "...",
  "visitType": "...",
  "visitTypeOther": null,
  "purpose": "...",
  "workingContent": "...",
  "workingLanguage": "EN",
  "mediaConsentStatus": "AGREED",
  "transportationNote": "...",
  "notes": "...",
  "operationalContact": {
    "fullName": "...",
    "organization": "...",
    "jobTitle": "...",
    "phone": "...",
    "email": "..."
  },
  "plannedStartAt": "...",
  "plannedEndAt": "...",
  "visitors": [],
  "supportMembers": []
}
```

---

## 14. Legacy normalizer

`GetVisitHistoryDetailQueryHandler` phải normalize snapshot cũ trước diff.

Map ví dụ:

```text
operationalContactEmail
→ operationalContact.email

operationalContactFullName
→ operationalContact.fullName

operationalContactOrganization
→ operationalContact.organization

operationalContactJobTitle
→ operationalContact.jobTitle

operationalContactPhone
→ operationalContact.phone
```

Nếu tồn tại các alias thực sự tương đương như `noteToFptu`/`notes`, map về canonical field.

---

## 15. Phân biệt empty với unknown

Không dùng cùng một `null` cho:

```text
giá trị cũ thực sự null
```

và:

```text
không có snapshot lịch sử
```

UI:

```text
actual empty
→ (trống)

historical value unavailable
→ Không có dữ liệu lịch sử
```

Không giả dữ liệu cũ là trống.

---

## 16. Test history

Bắt buộc cover:

```text
revision 1 visitType=CAMPUS_TOUR
revision 2 visitType=MEETING
→ CAMPUS_TOUR → MEETING

notes null → "Cần xe điện"
→ (trống) → Cần xe điện

legacy nested operationalContact
→ đọc đúng

snapshot {}
→ "Không có dữ liệu lịch sử"
không được → "(trống)"
```

---

# PHASE 4 — AMENDMENT / ĐỀ XUẤT THAY ĐỔI

## 17. Nguyên tắc lifecycle

Giữ nguyên:

```text
ACTIVE revision N
→ submit proposal
→ PENDING_APPROVAL

Reject
→ active vẫn revision N
→ không rollback vì proposal chưa từng apply

Approve
→ apply proposal
→ revision N+1
```

Không sửa active data lúc submit.

---

## 18. Validation frontend

### Vấn đề

Input không hợp lệ như:

```text
+82101234000asa1
```

vẫn có thể được gửi và chỉ nhận generic error.

### Cần validate trước submit

```text
reason required
phone format
visitor >= 1
required visitor fields
plannedEnd > plannedStart
minimum duration
lead-time nếu frontend có đủ dữ liệu
email format nếu field đó còn tồn tại
```

Hiển thị lỗi ngay dưới field.

Không dùng:

```text
"Không thể gửi đề xuất. Vui lòng thử lại."
```

cho mọi lỗi.

---

## 19. Error mapping amendment

Map đầy đủ stable code:

```text
AMENDMENT_ALREADY_PENDING
AMENDMENT_WINDOW_EXPIRED
AMENDMENT_NOT_EDITABLE
AMENDMENT_BASE_REVISION_CONFLICT
VISIT_FORM_CONCURRENCY_CONFLICT
AMENDMENT_NO_CHANGES
INVALID_VISIT_TIME
INVALID_PHONE
VALIDATION_ERROR
```

Frontend hiển thị message có thể hành động.

---

## 20. Tách amendment và operational contact identity

### Nguyên tắc

Amendment dùng cho:

```text
delegation
visit type
purpose
working content
working language
members
schedule
các profile field được business xác định approval-sensitive
```

Workflow đầu mối riêng dùng cho:

```text
email identity
account relation
confirmation
transfer
resend
cancel invitation
accept
decline
```

### Yêu cầu

Không cho amendment bypass workflow đầu mối.

Tối thiểu:

```text
operationalContact.email
```

phải loại khỏi amendment.

Nếu thiết kế cuối cùng xác định cả identity relationship phải tách hoàn toàn, chỉ giữ amendment cho profile fields không làm đổi identity:

```text
fullName
organization
jobTitle
phone
```

nhưng việc này phải nhất quán với workflow `Chỉnh sửa đầu mối` đang dùng.

Không tạo hai đường khác nhau để đổi cùng một identity.

---

## 21. File cần sửa amendment

```text
frontend/.../VisitAmendmentSubmitModal.tsx

backend/PEMS.Application/Delegations/Services/VisitFieldClassifier.cs

backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs

backend/PEMS.Application/Common/DTOs/VisitAmendmentDtos.cs
```

Nếu bỏ email khỏi amendment, cập nhật DTO/tests tương ứng.

---

# PHASE 5 — MULTI-CAMPUS ACCORDION

## 22. Mục tiêu

Ở section:

```text
② NỘI DUNG THAM QUAN THEO CƠ SỞ
```

nếu có từ 2 campus trở lên, không render full tất cả campus cùng lúc.

---

## 23. UX đích

### 1 campus

```text
luôn mở
không cần icon accordion
UI giữ gần như hiện tại
```

### 2+ campus

Mỗi campus là accordion:

```text
Campus 1 ▲
  full content

Campus 2 ▼

Campus 3 ▼
```

Header campus khi đóng vẫn hiển thị:

```text
campus name
instance status
amendment badge
planned time
chevron
```

Mặc định:

```text
campus đầu tiên mở
campus còn lại đóng
```

Bấm campus khác:

```text
mở campus mới
đóng campus cũ
```

Cho phép đóng hết nếu user bấm lại campus đang mở.

---

## 24. File cần sửa

### `VisitRequestV2DetailView.tsx`

Thêm:

```ts
const multipleCampuses = data.campusVisits.length > 1;
const [expandedCampusId, setExpandedCampusId] = useState<number | null>(null);
```

Khởi tạo theo request:

```text
first campus open
```

Không reset vô lý sau mỗi background reload.

### `CampusVisitDetailCard.tsx`

Props:

```ts
collapsible?: boolean;
expanded?: boolean;
onToggle?: () => void;
```

Header thành button/interactive area khi collapsible.

Body render khi:

```text
!collapsible || expanded
```

Thêm accessibility:

```text
aria-expanded
aria-controls
keyboard Enter/Space
```

---

## 25. Deep-link contact

Hiện có:

```text
#contact-{visitInstanceId}
```

Sau khi accordion:

```text
deep link
→ parse visitInstanceId
→ setExpandedCampusId(id)
→ đợi render
→ scrollIntoView()
```

Không scroll vào DOM đang bị collapse.

---

# 26. Thứ tự triển khai

## Bước 1

Fix expense lifecycle trước:

```text
backend read/init split
frontend no auto-create
VisitProcess truyền instanceStatus
```

## Bước 2

Fix contact workflow:

```text
no-change guard
cancel wording
history refresh
identity event detail
scope security
```

## Bước 3

Fix history snapshot:

```text
canonical writer
legacy normalizer
unknown vs empty
```

## Bước 4

Fix amendment:

```text
frontend validation
error mapping
identity boundary
```

## Bước 5

Multi-campus accordion:

```text
parent state
card collapse
deep-link
tests
```

---

# 27. Test matrix bắt buộc

## Expense

- DURING_VISIT mở tab Sau → không initialize report.
- AFTER_VISIT Host + chưa có report → hiện CTA initialize.
- AFTER_VISIT Host initialize → tạo đúng 1 report.
- Refresh lại → không tạo duplicate.
- CLOSED + report tồn tại → xem được.
- CLOSED + không có report → empty/read-only, không toast.
- Không còn nhiều toast giống nhau khi chỉ mở tab.

## Contact

- Mở edit → không sửa → nút Save disabled hoặc không request.
- Sửa name/phone → update đúng.
- INITIAL_CONFIRMATION → label `Hủy lời mời xác nhận`.
- TRANSFER → label `Hủy lời mời chuyển giao`.
- Resend/cancel/accept/decline → history refresh ngay.
- Identity history có Eye.
- Viewer chỉ thấy campus trong scope.

## History

- Revision 1 → 2 hiển thị before/after thật.
- Legacy nested snapshot đọc được.
- Empty thật → `(trống)`.
- Missing history → `Không có dữ liệu lịch sử`.
- Không leak sensitive identity data.

## Amendment

- Submit proposal không đổi active content.
- Reject → active content giữ nguyên.
- Approve → apply + tăng revision.
- Invalid phone bị chặn frontend.
- No changes báo đúng.
- Concurrent/base revision conflict báo đúng.
- Không đổi operational contact email qua amendment nếu workflow identity riêng đang được áp dụng.

## Multi-campus

- 1 campus → giữ UI hiện tại.
- 2 campus → campus đầu mở, campus sau đóng.
- Switch campus → accordion hoạt động đúng.
- Badge/status/time vẫn thấy khi collapsed.
- Deep-link `#contact-ID` tự mở đúng campus.
- Background reload không tự nhảy sang campus khác nếu không cần.

---

# 28. Không làm trong task này

Không mở rộng ngoài scope:

- Không đổi database schema nếu hiện tại không bắt buộc.
- Không refactor toàn module Visit Process.
- Không đổi route lớn.
- Không đổi business lifecycle ngoài các rule nêu trên.
- Không tạo abstraction mới nếu helper/service hiện tại dùng lại được.
- Không thay đổi email system ngoài error wording liên quan trực tiếp.
- Không đổi quyền role khác nếu không liên quan trực tiếp tới các flow trên.

---

# 29. Definition of Done

Task hoàn thành khi:

```text
1. Mở Sau tiếp khách không còn tự gọi API create sai lifecycle.
2. GET expense không còn side effect.
3. Contact no-change không sinh lỗi.
4. Contact invitation wording đúng INITIAL/TRANSFER.
5. Contact mutation làm history refresh ngay.
6. Identity history có thể mở chi tiết.
7. Before/After history dùng dữ liệu cũ thật, không giả "(trống)".
8. Reject amendment giữ nguyên active revision.
9. Amendment validation báo lỗi đúng field/code.
10. Operational contact identity không bị bypass qua amendment.
11. Multi-campus có accordion khi >= 2 campus.
12. Deep-link contact vẫn hoạt động.
13. Backend + frontend tests liên quan đều green.
14. Không phát sinh regression ngoài scope.
```
