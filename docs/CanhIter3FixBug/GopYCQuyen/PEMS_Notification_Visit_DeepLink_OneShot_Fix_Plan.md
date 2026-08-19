# PEMS — Fix Notification Deep-Link mở đúng đoàn và không tái mở sau khi đóng

## 1. Mục tiêu

Sửa triệt để lỗi người dùng phản ánh:

> Khi nhấp vào thông báo ở chuông, hệ thống không đưa người dùng đến đúng đoàn được nhắc đến.  
> Ví dụ: Staff Leader nhận thông báo có đoàn cần duyệt, nhưng khi bấm vào notification thì giao diện gần như không thay đổi hoặc không mở đúng đoàn cần xử lý.

Đồng thời **không được tái tạo bug cũ**:

> Notification mở modal/detail thông qua query parameter, nhưng parameter không được consume/xóa khỏi URL. Sau khi đóng modal, chỉ cần đổi tab/filter/search/page thì effect chạy lại và mở lại notification cũ.

Kết quả mong muốn:

```text
CLICK NOTIFICATION
        ↓
Xác định đúng business target
        ↓
Điều hướng tới /dashboard/visit
        ↓
Consume one-shot navigation command
        ↓
Xóa command khỏi URL bằng replace
        ↓
Load đúng request / campus instance
        ↓
Mở đúng detail / CAMPUS_REVIEW / entry context
        ↓
User đóng
        ↓
Không tự mở lại dù đổi tab/filter/search/page/refresh
```

---

# 2. Code baseline đã rà soát

Thực hiện trên code mới nhất của PEMS nhánh:

```text
Dev
```

HEAD đã rà soát khi lập plan:

```text
a7f164d77066319b8c7e82e261ed3b3e384cd41e
```

Trước khi implement:

1. Pull/checkout `Dev` mới nhất.
2. Ghi lại HEAD thực tế.
3. Nếu HEAD thay đổi, audit lại các file notification/deep-link liên quan trước khi sửa.
4. Không implement dựa trên branch cũ hoặc file cũ.

---

# 3. Scope

Task này tập trung vào:

```text
NOTIFICATION → VISIT DEEP-LINK → EXACT TARGET → ONE-SHOT CONSUMPTION
```

Bao gồm:

- Bell notification.
- Notifications page.
- Visit Request Management.
- Notification DTO/API data.
- Visit notification producers liên quan request/campus.
- Legacy notification fallback.
- Query param synchronization.
- One-shot deep-link consumption.
- Regression tests chống reopen.
- Multi-campus exact-target routing.

Không mở rộng sang:

- business rule duyệt/từ chối;
- permission model mới;
- workflow mới;
- thay đổi database schema nếu chưa thật sự cần;
- redesign toàn bộ notification UI;
- sửa nội dung email;
- sửa business logic không liên quan.

Nếu phát hiện bug khác trong quá trình sửa:

```text
Ghi nhận riêng.
Không trộn vào PR này.
```

---

# 4. Bằng chứng từ code hiện tại

## 4.1 Backend đã biết đúng `VisitRequestId`

File:

```text
backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/V2CreateNotifier.cs
```

Thông báo cho Staff Leader đang được tạo theo từng campus đang chờ duyệt:

```csharp
var pendingInstances = created.CampusInstances
    .Where(c => c.Status == VisitInstanceStatus.WaitingRequestApproval
                && c.CoordinatorUserId.HasValue)
    .ToList();
```

Notification hiện có:

```csharp
VisitRequestId: created.VisitRequestId,
ActionType: NotificationActionTypes.OpenVisitDetail,
ActionUrl: $"/dashboard/visit?visitRequestId={created.VisitRequestId}"
```

Kết luận:

- Backend **không bị mất request id**.
- Notification đã biết request nào cần mở.
- Tuy nhiên notification được tạo trong vòng lặp `pendingInstances`, nghĩa là backend cũng biết chính xác campus instance nhưng hiện chưa tận dụng đầy đủ context này trong deep-link.

---

## 4.2 API notification đã trả `TargetUrl`

File:

```text
backend/PEMS.Application/Notifications/Queries/GetMyNotifications/GetMyNotificationsQueryHandler.cs
```

Hiện có mapping:

```csharp
VisitRequestId = n.VisitRequestId,
VisitInstanceId = n.VisitInstanceId,
CampusId = n.CampusId,
ActionType = n.ActionType,
TargetUrl = n.ActionUrl,
CanOpen = !string.IsNullOrEmpty(n.ActionUrl)
```

Kết luận:

- FE có đủ cơ chế để nhận business identifiers.
- Không cần suy đoán notification dựa vào title/message.

---

## 4.3 Bell hiện đã gọi `navigate()`

File:

```text
frontend/pems-react/src/features/notifications/components/NotificationBellButton.tsx
```

Luồng click:

```tsx
if (!item.isRead) await markAsRead(item.notificationId);

const link = getNotificationLink(item, user);
setIsOpen(false);

if (!link) {
  setDetailModalItem(item);
  return;
}

navigate(link);
onNavigate?.();
```

Kết luận:

- Bell có thực hiện navigation.
- Lỗi không phải đơn giản là “button không có onClick”.

---

# 5. Root Cause

## RC-01 — `visitRequestId` bị copy vào React state chỉ khi mount

File:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
```

Hiện có pattern:

```tsx
const [notificationVisitRequestId, setNotificationVisitRequestId] =
  useState(searchParams.get('visitRequestId') || '');
```

`useState(initialValue)` chỉ dùng `initialValue` lúc component mount.

Nếu người dùng đã đang đứng tại:

```text
/dashboard/visit
```

sau đó click notification dẫn đến:

```text
/dashboard/visit?visitRequestId=123
```

React Router có thể giữ nguyên component vì pathname vẫn là `/dashboard/visit`.

Khi đó:

```text
URL đã đổi
nhưng notificationVisitRequestId state không tự đổi
```

Đây là nguyên nhân trực tiếp khiến:

```text
click notification
→ URL có thể đổi
→ page không phản ứng
→ người dùng thấy như không có gì xảy ra
```

---

## RC-02 — Initial load chỉ chạy lúc mount

Hiện có pattern:

```tsx
useEffect(() => {
  loadDelegations(activeTab, currentPage, pageSize, appliedFilters, sortOrder);
}, []);
```

Effect không theo dõi:

```text
searchParams
location.search
visitRequestId
tab
```

Do đó khi query string đổi trong cùng route:

```text
/dashboard/visit
→ /dashboard/visit?visitRequestId=123
```

page không bắt buộc reload dữ liệu.

---

## RC-03 — Một query parameter đang bị dùng cho hai mục đích

Hiện `visitRequestId` đang được dùng như:

```text
“filter danh sách xuống request này”
```

nhưng người dùng lại kỳ vọng notification là:

```text
“mở request này ngay”
```

Hai ý nghĩa khác nhau.

Không nên tiếp tục dùng cùng một param vừa làm persistent page state vừa làm one-shot command.

---

## RC-04 — Hiện deep-link chỉ lọc danh sách, chưa mở thẳng đúng business entry

Code hiện tại có comment:

```text
Đến từ 1 thông báo cụ thể (?visitRequestId=...):
chỉ hiển thị đúng đơn đó thay vì cả danh sách
```

Sau đó list bị filter:

```tsx
const filtered = notifFilter
  ? mapped.filter((r) => String(r.visitRequestId) === notifFilter)
  : mapped;
```

Tức behavior hiện tại là:

```text
notification
→ mở Visit Management
→ list còn 1 row
```

Trong khi kỳ vọng UX tốt hơn là:

```text
notification
→ mở đúng đoàn
→ mở đúng campus review / detail
→ user xử lý luôn
```

---

## RC-05 — Multi-campus chưa carry đủ target context

`V2CreateNotifier` đang loop theo:

```text
pendingInstances
```

nên tại thời điểm tạo notification đã biết:

```text
VisitRequestId
VisitInstanceId
CampusId
CoordinatorUserId
```

Nhưng notification hiện chủ yếu chỉ deep-link bằng:

```text
VisitRequestId
```

Với multi-campus, request-level id chưa đủ để nói:

```text
campus nào đang cần chính user này duyệt
```

---

# 6. Nguyên tắc fix bắt buộc

Không sửa theo kiểu:

```text
chỉ thêm [searchParams] vào useEffect
```

vì cách đó có thể làm page phản ứng với URL nhưng dễ tái tạo bug:

```text
đóng modal
→ param vẫn còn
→ đổi filter
→ effect chạy lại
→ modal cũ mở lại
```

Phải áp dụng pattern:

```text
ONE-SHOT COMMAND
```

Cụ thể:

```text
READ
→ EXECUTE
→ CONSUME
→ DELETE FROM URL
→ STATE OWNS UI
```

---

# 7. Thiết kế URL đề xuất

## 7.1 Tách persistent filter và one-shot command

### Persistent page state

Ví dụ:

```text
/dashboard/visit?tab=all&status=PENDING_APPROVAL
```

Các param này có thể tồn tại lâu dài:

```text
tab
status
keyword
page
pageSize
sortOrder
campusId
fromDate
toDate
visitRequestId   // chỉ giữ nếu thật sự dùng như persistent filter
```

### One-shot command

Dùng param riêng:

```text
openVisitRequestId
openVisitInstanceId
```

Ví dụ:

```text
/dashboard/visit?openVisitRequestId=123&openVisitInstanceId=456
```

Ý nghĩa:

```text
“Hãy mở business target này đúng một lần.”
```

Không dùng nó làm state lâu dài.

---

# 8. Luồng chuẩn sau khi sửa

## Case: Staff Leader nhận notification có đoàn cần duyệt

Notification:

```text
Đoàn ABC đang chờ duyệt tại cơ sở Hà Nội
```

Deep-link:

```text
/dashboard/visit?openVisitRequestId=123&openVisitInstanceId=456
```

Frontend:

```text
1. Nhận searchParams mới.
2. Đọc:
   openVisitRequestId = 123
   openVisitInstanceId = 456

3. Validate numeric/positive.

4. Consume command:
   - copy target vào React state/local intent object
   - xóa openVisitRequestId
   - xóa openVisitInstanceId
   - setSearchParams(next, { replace: true })

5. Load đúng row/request/instance.

6. Resolve business entry context.

7. Nếu target là campus review:
   openEntryContext(row)
   → CAMPUS_REVIEW
   → openRequestForm(row)

8. User đóng detail.

9. Clear React state.

10. Vì URL đã sạch nên:
    - search không mở lại
    - tab không mở lại
    - filter không mở lại
    - pagination không mở lại
    - refresh không mở lại
```

---

# 9. Consume URL trước khi mở UI

Pattern phải tương tự bug feedback đã từng được fix.

Pseudo-code:

```tsx
useEffect(() => {
  const rawRequestId = searchParams.get('openVisitRequestId');
  const rawInstanceId = searchParams.get('openVisitInstanceId');

  if (!rawRequestId && !rawInstanceId) return;

  const requestId = Number(rawRequestId);
  const instanceId = rawInstanceId ? Number(rawInstanceId) : null;

  const next = new URLSearchParams(searchParams);
  next.delete('openVisitRequestId');
  next.delete('openVisitInstanceId');

  setSearchParams(next, { replace: true });

  if (!Number.isFinite(requestId) || requestId <= 0) return;
  if (instanceId != null && (!Number.isFinite(instanceId) || instanceId <= 0)) return;

  setNotificationOpenIntent({
    visitRequestId: requestId,
    visitInstanceId: instanceId,
  });
}, [searchParams, setSearchParams]);
```

Điểm quan trọng:

```text
XÓA command khỏi URL bằng replace.
```

Không để param sống cùng modal/detail.

---

# 10. React state cho notification intent

Đề xuất state rõ nghĩa:

```tsx
type NotificationOpenIntent = {
  visitRequestId: number;
  visitInstanceId?: number | null;
};

const [notificationOpenIntent, setNotificationOpenIntent] =
  useState<NotificationOpenIntent | null>(null);
```

Sau khi UI target được mở:

```text
notificationOpenIntent
```

chỉ còn là transient React state.

Khi modal/detail đóng:

```tsx
setNotificationOpenIntent(null);
```

Không cần ghi lại URL.

---

# 11. Không được tái mở khi query string thay đổi

Sau khi consume:

```text
/dashboard/visit?openVisitRequestId=123&openVisitInstanceId=456
```

phải trở về dạng sạch, ví dụ:

```text
/dashboard/visit?tab=all&status=PENDING_APPROVAL
```

Sau đó user:

```text
search
filter
switch tab
pagination
sort
reset filters
```

không có command nào trong URL để replay.

---

# 12. Reuse `openEntryContext()` thay vì tạo navigation logic mới

`VisitRequestManagement.tsx` đã có:

```tsx
const openEntryContext = (row: Row): boolean => {
  ...
  switch (entry) {
    case 'HOST_PROCESS':
      ...
    case 'PROCESS_SUMMARY':
      ...
    case 'RECEPTION_DETAIL':
      ...
    case 'CONTRIBUTION':
      ...
    case 'CAMPUS_REVIEW':
      openRequestForm(row);
      return true;
  }
}
```

Do đó notification deep-link sau khi resolve row nên ưu tiên:

```text
openEntryContext(row)
```

Không tạo thêm một hệ thống mapping route thứ hai nếu existing entry context đã đủ.

Mục tiêu:

```text
notification target
        ↓
same business routing rules as normal row click
```

Tránh drift giữa:

```text
click row
```

và:

```text
click notification
```

---

# 13. Backend notification context cần chuẩn hóa

## 13.1 Notification mới của Staff Leader cần carry instance context

Trong:

```text
V2CreateNotifier.cs
```

đang loop theo:

```csharp
pendingInstances.Select(c => ...)
```

Nên tận dụng:

```text
c.VisitInstanceId
c.CampusId
```

Khi tạo notification.

Kỳ vọng data:

```text
VisitRequestId = created.VisitRequestId
VisitInstanceId = c.VisitInstanceId
CampusId = c.CampusId
ActionType = OPEN_CAMPUS_REVIEW hoặc semantic equivalent
```

Nếu project không muốn thêm ActionType mới, vẫn phải đảm bảo FE có đủ identifier để resolve đúng campus.

---

## 13.2 ActionUrl mới

Có thể chọn một trong hai hướng.

### Hướng A — URL mang one-shot command

```csharp
ActionUrl:
$"/dashboard/visit?openVisitRequestId={created.VisitRequestId}&openVisitInstanceId={c.VisitInstanceId}"
```

### Hướng B — FE build link từ semantic DTO

Backend trả:

```text
VisitRequestId
VisitInstanceId
CampusId
ActionType
```

Frontend `getNotificationLink()` dựng:

```text
/dashboard/visit?openVisitRequestId=...&openVisitInstanceId=...
```

Khuyến nghị:

```text
ActionType + structured IDs là source of truth.
ActionUrl là transport/fallback.
```

---

# 14. `getNotificationLink()` cần normalize cả notification mới và notification cũ

File:

```text
frontend/pems-react/src/features/notifications/components/NotificationBellButton.tsx
```

Hiện đã có nhiều rewrite cho legacy notification.

Cần mở rộng theo nguyên tắc:

## Priority

```text
1. Structured identifiers + actionType
2. Existing targetUrl
3. Legacy rewrite
4. Detail modal fallback nếu thật sự không có target
```

Ví dụ:

```tsx
if (
  item.actionType === 'OPEN_VISIT_DETAIL' &&
  item.visitRequestId
) {
  const params = new URLSearchParams();
  params.set('openVisitRequestId', String(item.visitRequestId));

  if (item.visitInstanceId) {
    params.set('openVisitInstanceId', String(item.visitInstanceId));
  }

  return `/dashboard/visit?${params.toString()}`;
}
```

Không tin tuyệt đối `targetUrl` cũ nếu structured IDs hiện có đã nói rõ hơn.

---

# 15. Legacy notification compatibility

Không được fix notification mới rồi làm notification cũ chết.

## Case L1 — Notification mới

Có:

```text
VisitRequestId
VisitInstanceId
ActionType
```

Expected:

```text
mở đúng campus
```

---

## Case L2 — Notification cũ có `VisitRequestId`, thiếu `VisitInstanceId`

Expected:

```text
fallback mở request-level detail
```

Không đoán campus.

---

## Case L3 — Notification rất cũ có:

```text
ActionUrl = /dashboard/visit
VisitRequestId != null
```

Hiện code có legacy rewrite.

Cần đổi sang one-shot format:

```text
/dashboard/visit?openVisitRequestId=123
```

---

## Case L4 — Không có ActionUrl nhưng RelatedType/RelatedId còn đủ

Nếu có thể reconstruct an toàn từ structured data:

```text
reconstruct
```

Nếu không:

```text
mở NotificationDetailModal
```

Không đoán ID từ title/message free text.

---

# 16. Notifications Page phải dùng cùng resolver

File:

```text
frontend/pems-react/src/pages/notifications/NotificationsPage.tsx
```

Hiện page đã gọi:

```tsx
const link = getNotificationLink(item, user);
...
navigate(link);
```

Giữ nguyên nguyên tắc:

```text
Bell và NotificationsPage phải dùng chung getNotificationLink()
```

Không duplicate route resolution.

Mục tiêu:

```text
click cùng 1 notification ở bell
=
click cùng notification ở Notifications page
```

---

# 17. Xử lý khi đang đứng sẵn ở `/dashboard/visit`

Đây là case bắt buộc phải test.

Current bug:

```text
current route:
/dashboard/visit

click notification:
/dashboard/visit?openVisitRequestId=123
```

Vì pathname không đổi, component có thể không remount.

Fix phải **chủ động phản ứng với one-shot param** bằng effect.

Không dựa vào remount.

---

# 18. Xử lý click notification A rồi B

Scenario:

```text
đang mở đoàn A
→ click bell
→ chọn notification đoàn B
```

Expected:

```text
B phải được mở.
```

Không được:

```text
A vẫn giữ
hoặc
A đóng rồi không có gì xảy ra
hoặc
A tự mở lại
```

Implementation phải reset/replace transient intent đúng cách.

Có thể dùng:

```text
notificationOpenIntent
```

theo latest command wins.

---

# 19. Xử lý click đúng notification khi modal/detail đang mở

Phải xác định behavior rõ:

```text
Notification B được click trong khi detail A đang mở
```

Khuyến nghị:

```text
close/switch A
→ resolve B
→ open B
```

Không stack nhiều modal Visit Detail.

---

# 20. Không dùng `window.location.reload()`

Không fix bằng:

```js
window.location.reload()
```

hoặc full-page hard navigation.

Lý do:

- che giấu state bug;
- UX xấu;
- mất local filters/state không cần thiết;
- khó test;
- không giải quyết semantic one-shot command;
- dễ phát sinh flash/race.

Phải sửa bằng React Router + React state đúng cách.

---

# 21. Không để command bị carry qua helper update URL

Mọi helper kiểu:

```tsx
const params = new URLSearchParams(searchParams);
```

phải defense-in-depth:

```tsx
params.delete('openVisitRequestId');
params.delete('openVisitInstanceId');
```

trước khi set params mới.

Tương tự pattern đã dùng với:

```text
feedbackVisitInstanceId
```

Mục tiêu:

Ngay cả nếu one-shot effect chưa consume kịp trong một race hiếm:

```text
filter/page/tab update
```

cũng không được clone command sang URL mới.

---

# 22. Audit tất cả helper có clone `searchParams`

Trong:

```text
VisitRequestManagement.tsx
```

tìm toàn bộ:

```text
new URLSearchParams(searchParams)
setSearchParams(...)
navigate(location.pathname + location.search)
```

Kiểm tra:

- có carry one-shot params không;
- có replace đúng không;
- có làm mất các persistent params khác không.

Rule:

```text
One-shot params:
DELETE

Persistent filters:
PRESERVE
```

---

# 23. Không xóa nhầm persistent params

Khi consume:

```text
openVisitRequestId
openVisitInstanceId
```

chỉ xóa đúng 2 param này.

Ví dụ URL:

```text
/dashboard/visit
?tab=all
&keyword=abc
&status=PENDING_APPROVAL
&openVisitRequestId=123
&openVisitInstanceId=456
```

Sau consume phải thành:

```text
/dashboard/visit
?tab=all
&keyword=abc
&status=PENDING_APPROVAL
```

Không làm mất:

```text
tab
keyword
status
page
pageSize
sort
campus filters
date filters
```

---

# 24. Request/instance resolution

Sau khi nhận intent:

```text
visitRequestId
visitInstanceId
```

không nên filter mù list hiện tại rồi kỳ vọng row tồn tại.

Cần xác định cách resolve phù hợp.

Ưu tiên:

```text
1. Nếu row target đã có trong response hiện tại → dùng luôn.
2. Nếu chưa có → fetch target bằng API phù hợp hoặc load list scoped theo target.
3. Không phụ thuộc target phải nằm ở page hiện tại.
4. Không phụ thuộc filter hiện tại phải chứa target.
```

Ví dụ:

User đang filter:

```text
status=CLOSED
```

nhưng notification target là:

```text
PENDING_APPROVAL
```

Click notification vẫn phải mở target.

Không được:

```text
filter hiện tại không chứa row
→ rows=[]
→ không mở được
```

Notification intent phải có quyền override việc tìm target.

---

# 25. Multi-campus routing

Ví dụ:

```text
Request 100
├─ Hà Nội      instance 201
├─ HCM         instance 202
└─ Đà Nẵng     instance 203
```

Staff Leader Hà Nội nhận notification:

```text
requestId = 100
instanceId = 201
```

Expected:

```text
mở đúng campus Hà Nội / CAMPUS_REVIEW
```

Không chỉ:

```text
mở request 100 rồi bắt user tự tìm campus
```

Nếu legacy notification thiếu instance:

```text
fallback request detail
```

Không đoán instance.

---

# 26. Permission và authorization

Deep-link không được bypass permission.

Frontend chỉ điều hướng.

Backend vẫn là source of truth cho:

```text
canViewRequestDetail
allowedActions
primaryEntryContext
role/scope
campus ownership
```

Nếu notification cũ trỏ target user hiện tại không còn quyền:

```text
backend trả 403 / business refusal
```

Frontend phải:

- không crash;
- hiển thị error/toast phù hợp;
- không cố fallback sang route nhạy cảm khác bằng guess.

---

# 27. Mark-as-read không được phụ thuộc việc mở thành công

Hiện Bell:

```tsx
if (!item.isRead) await markAsRead(item.notificationId);
```

Cần confirm behavior mong muốn:

- click notification = user đã tương tác/read;
- nếu target bị 403/not found, notification có thể vẫn read.

Không thay đổi behavior này trong task nếu không có requirement khác.

---

# 28. Regression tests bắt buộc

Tạo test riêng cho Visit notification deep-link.

Gợi ý file:

```text
frontend/pems-react/src/pages/dashboard/visit/__tests__/
VisitRequestManagementNotificationDeepLink.test.tsx
```

---

## TEST-01 — Consume command đúng một lần

Initial:

```text
?openVisitRequestId=123&openVisitInstanceId=456
```

Expected:

```text
target mở
openVisitRequestId bị xóa
openVisitInstanceId bị xóa
```

---

## TEST-02 — Preserve persistent params

Initial:

```text
?tab=all
&keyword=ha
&page=2
&openVisitRequestId=123
&openVisitInstanceId=456
```

Sau consume:

```text
tab=all
keyword=ha
page=2
```

vẫn còn.

---

## TEST-03 — Close rồi search không reopen

```text
open notification target
→ close
→ search
```

Expected:

```text
target vẫn đóng
```

---

## TEST-04 — Close rồi đổi tab không reopen

```text
open
→ close
→ tab=registered
```

Expected:

```text
không reopen
```

---

## TEST-05 — Close rồi filter không reopen

```text
open
→ close
→ change status filter
```

Expected:

```text
không reopen
```

---

## TEST-06 — Close rồi pagination không reopen

```text
open
→ close
→ page 2
```

Expected:

```text
không reopen
```

---

## TEST-07 — Refresh sau khi consume

Vì URL đã sạch:

```text
refresh
```

Expected:

```text
không tự mở notification cũ
```

---

## TEST-08 — Đang ở `/dashboard/visit` rồi click notification

Initial route:

```text
/dashboard/visit
```

Simulate navigate:

```text
/dashboard/visit?openVisitRequestId=123
```

Expected:

```text
component không cần remount nhưng vẫn mở 123
```

---

## TEST-09 — A rồi B

```text
open A
click notification B
```

Expected:

```text
B mở
A không reopen
```

---

## TEST-10 — Multi-campus exact instance

Input:

```text
requestId=100
instanceId=201
```

Expected:

```text
open campus instance 201
```

Không được mở 202/203.

---

## TEST-11 — Legacy request-only notification

Input:

```text
visitRequestId=100
visitInstanceId=null
```

Expected:

```text
fallback request-level detail
```

---

## TEST-12 — Invalid IDs

Input:

```text
openVisitRequestId=abc
openVisitInstanceId=-1
```

Expected:

```text
không mở
xóa command khỏi URL
không loop
```

---

## TEST-13 — Target không nằm trong filter hiện tại

Current page filter:

```text
status=CLOSED
```

Notification target:

```text
PENDING_APPROVAL request 123
```

Expected:

```text
vẫn resolve và mở request 123
```

---

## TEST-14 — Target không nằm trong page hiện tại

Current list page:

```text
page=5
```

Target row thuộc page 1 hoặc ngoài current page.

Expected:

```text
vẫn mở đúng target
```

---

## TEST-15 — Bell và NotificationsPage giống nhau

Cùng notification:

```text
click từ Bell
```

và:

```text
click từ NotificationsPage
```

Expected:

```text
same final business target
```

---

# 29. Backend tests

Nếu thay đổi `V2CreateNotifier`:

Bổ sung unit/integration test xác nhận:

```text
WAITING_REQUEST_APPROVAL campus
→ coordinator nhận notification
→ VisitRequestId đúng
→ VisitInstanceId đúng
→ CampusId đúng
→ ActionType đúng
→ ActionUrl đúng one-shot format nếu ActionUrl vẫn được dùng
```

Multi-campus:

```text
Campus A coordinator
→ notification instance A

Campus B coordinator
→ notification instance B
```

Không được trộn instance.

---

# 30. Kiểm tra notification producers khác

Task này xuất phát từ “đoàn cần duyệt”, nhưng sau khi tạo generic one-shot mechanism phải audit các Visit notification khác dùng:

```text
/dashboard/visit
/dashboard/visit?visitRequestId=
```

Tìm toàn repo:

```text
ActionUrl
OpenVisitDetail
OPEN_VISIT_DETAIL
/dashboard/visit?visitRequestId
/dashboard/visit
VisitRequestId:
VisitInstanceId:
CampusId:
```

Mục tiêu:

- không bỏ sót producer dùng route cũ;
- không mass-convert nếu semantics khác;
- mỗi notification phải mở nơi hợp lý với role/business event.

---

# 31. Những notification không được ép vào request detail

Không được vì task này mà chuyển mọi notification Visit thành:

```text
OPEN_REQUEST_DETAIL
```

Ví dụ các loại khác có thể cần:

```text
HOST_PROCESS
PROCESS_SUMMARY
RECEPTION_DETAIL
CONTRIBUTION
INVITATION
FEEDBACK MODAL
DEPARTMENT TASK
POST-VISIT TASK
```

Phải giữ semantic routing hiện có.

---

# 32. Verification thủ công

Sau automated test, test thật trên browser.

## Staff Leader

1. Đang ở homepage → click “Có yêu cầu tiếp khách mới”.
2. Đang ở Visit Management → click notification.
3. Đang filter một status khác → click.
4. Đang page khác → click.
5. Multi-campus → đúng campus.
6. Đóng detail → search.
7. Đóng detail → đổi tab.
8. Đóng detail → đổi filter.
9. Đóng detail → pagination.
10. Refresh → không reopen.

---

## Other roles

Spot-check notification Visit cho:

```text
Visitor
Staff
Staff Leader
Department Leader
Department Staff
Student
HO
```

Không cần biến task thành full notification redesign, nhưng phải đảm bảo one-shot resolver không phá route cũ.

---

# 33. Verification commands

Frontend:

```bash
npm run lint
npm run test:unit
npm run build
```

Nếu project có targeted test:

```bash
npm run test:unit -- VisitRequestManagementNotificationDeepLink
```

Nếu E2E có liên quan:

```bash
npm run test:e2e
```

Backend nếu thay notifier/service:

```bash
dotnet test
```

hoặc targeted project tương ứng.

---

# 34. Definition of Done

Task chỉ được coi là DONE khi tất cả điều sau pass.

- [ ] Notification “đoàn cần duyệt” có đúng `VisitRequestId`.
- [ ] Notification mới carry đúng `VisitInstanceId` nếu target là campus-specific.
- [ ] Notification mới carry đúng `CampusId` nếu cần.
- [ ] Bell click mở đúng target.
- [ ] NotificationsPage click mở cùng target.
- [ ] Đang ở `/dashboard/visit` vẫn nhận deep-link mới mà không cần remount.
- [ ] One-shot command được consume đúng một lần.
- [ ] `openVisitRequestId` bị xóa khỏi URL bằng `replace`.
- [ ] `openVisitInstanceId` bị xóa khỏi URL bằng `replace`.
- [ ] Persistent filters không bị xóa nhầm.
- [ ] Đóng target không tự reopen.
- [ ] Search sau đóng không reopen.
- [ ] Filter sau đóng không reopen.
- [ ] Tab change sau đóng không reopen.
- [ ] Pagination sau đóng không reopen.
- [ ] Refresh sau consume không reopen.
- [ ] Click A rồi B mở đúng B.
- [ ] Multi-campus mở đúng instance.
- [ ] Legacy notification thiếu instance fallback an toàn.
- [ ] Legacy notification không parse ID từ free-text.
- [ ] Notification target ngoài current page vẫn mở được.
- [ ] Notification target ngoài current filter vẫn mở được.
- [ ] `openEntryContext()` được reuse nếu phù hợp.
- [ ] Không tạo duplicate routing logic không cần thiết.
- [ ] Không dùng `window.location.reload()`.
- [ ] Không bypass backend authorization.
- [ ] FE unit tests pass.
- [ ] BE tests liên quan pass nếu backend thay đổi.
- [ ] lint pass.
- [ ] build pass.
- [ ] Manual browser spot-check pass.
- [ ] Không regression luồng feedback one-shot cũ.

---

# 35. Repo-wide search cuối cùng

Trước khi báo hoàn thành, search:

```text
/dashboard/visit?visitRequestId
/dashboard/visit
openVisitRequestId
openVisitInstanceId
feedbackVisitInstanceId
new URLSearchParams(searchParams)
setSearchParams(
navigate(
ActionUrl:
OpenVisitDetail
VisitRequestId:
VisitInstanceId:
CampusId:
```

Đối chiếu từng result liên quan notification/deep-link.

Không được báo:

```text
“đã fix notification”
```

nếu chỉ sửa Bell hoặc chỉ sửa một `useEffect`.

---

# 36. Báo cáo sau triển khai

Dev phải báo rõ:

| ID | File | Vấn đề trước | Thay đổi | Test |
|---|---|---|---|---|
| NDL-01 | NotificationBellButton.tsx | URL cũ dùng persistent param | build one-shot link | PASS |
| NDL-02 | VisitRequestManagement.tsx | state không sync khi query đổi | consume one-shot intent | PASS |
| NDL-03 | VisitRequestManagement.tsx | command có thể bị carry qua URL updates | delete command params defensively | PASS |
| NDL-04 | V2CreateNotifier.cs | thiếu exact campus instance context | attach instance/campus identifiers | PASS |
| NDL-05 | tests | chưa có regression reopen coverage | add deep-link tests | PASS |

Tổng kết:

```text
Dev HEAD:
Notification producers audited:
Frontend resolvers audited:
Legacy routes handled:
Deep-link tests added:
Backend tests added:
Manual scenarios passed:
Remaining exceptions:
```

Nếu có exception:

```text
File:
Notification type:
Reason:
Fallback behavior:
Why safe:
```

---

# 37. Nguyên tắc quan trọng nhất

Không sửa theo tư duy:

```text
“Bấm notification không thấy gì → ép reload trang.”
```

Phải sửa theo:

```text
STRUCTURED BUSINESS TARGET
        ↓
ONE-SHOT NAVIGATION INTENT
        ↓
CONSUME ON ARRIVAL
        ↓
DELETE FROM URL
        ↓
RESOLVE EXACT ROW / INSTANCE
        ↓
REUSE EXISTING ENTRY CONTEXT
        ↓
OPEN UI
        ↓
CLEAR TRANSIENT STATE
        ↓
NO REPLAY
```

Kết quả cuối cùng phải đạt:

> Người dùng bấm vào một notification về đoàn khách thì PEMS đưa họ đến đúng request/campus/business screen được nhắc đến, kể cả khi họ đang đứng sẵn ở trang Visit Management. Deep-link chỉ được thực thi một lần; sau khi mở, command bị xóa khỏi URL nên đóng modal/detail rồi search, filter, đổi tab, phân trang hoặc refresh đều không làm notification cũ tự mở lại.
