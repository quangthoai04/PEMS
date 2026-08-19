# PEMS — FIX TRIỆT ĐỂ NOTIFICATION PRESENTATION + SEMANTIC ROUTING

## 0. Mục tiêu

Hãy đọc **code mới nhất trên GitHub branch `Dev`** trước khi sửa và xử lý triệt để hệ thống notification của PEMS theo 2 nhóm lỗi:

### BUG A — Nội dung notification hiển thị sai hoặc quá chung chung

Ví dụ thực tế ở Staff Leader:

```text
You have a new notification.
You have a new notification.
You have a new notification.
...
```

trong khi backend thực tế đã có những nội dung cụ thể như:

```text
Có yêu cầu tiếp khách mới
Campus-level pending Staff Leader approval tour đang chờ xử lý...
```

Không được để người dùng phải click từng notification mới biết notification đó nói về việc gì.

### BUG B — Click notification chưa luôn chuyển đúng nơi theo ý nghĩa notification

Ví dụ:

```text
"Có đoàn cần bạn duyệt"
```

thì phải mở đúng:

```text
request
+
campus instance
+
current CAMPUS_REVIEW context
```

chứ không chỉ:

```text
/dashboard/visit
```

hoặc lọc ra một request rồi bắt user tự tìm tiếp.

Tương tự:

```text
Bạn được gán làm Host
→ Host Process

Bạn được mời tham gia
→ Participation Invitation / Attending

Có yêu cầu hậu cần
→ đúng Logistics item

Tin tức chờ duyệt
→ đúng News record

Đối tác chờ duyệt
→ đúng Partner record
```

Không được dùng một route chung cho mọi notification chỉ vì cùng thuộc Visit.

---

# 1. Source of Truth

Trước khi implement:

```bash
git checkout Dev
git pull
git rev-parse HEAD
```

Báo lại chính xác:

```text
Branch:
HEAD:
```

Không sử dụng HEAD cũ từ báo cáo trước nếu `Dev` đã thay đổi.

Không sửa dựa trên screenshot.

Phải audit:

```text
notification producer
→ DB notification
→ API DTO
→ frontend presentation
→ click resolver
→ destination
```

cho từng loại notification.

---

# 2. Root Cause A — Notification bị biến thành generic message

Code hiện tại có:

```text
resolveNotificationPresentation.ts
```

với một tập `KNOWN_EVENT_KEYS` giới hạn.

Nếu notification:

```text
MetadataJson = null
```

hoặc:

```text
eventKey không nằm trong KNOWN_EVENT_KEYS
```

thì khi UI đang là English, resolver fallback thành:

```text
You have a new notification.
```

và:

```text
message = null
```

Do đó những notification Staff / Staff Leader / Department / HO chưa được migrate sang semantic event metadata bị mất toàn bộ ý nghĩa trên giao diện EN.

Đây không phải vì DB không có Title/Message.

Nhiều backend producer hiện vẫn tạo:

```text
Title = nội dung cụ thể
Message = nội dung cụ thể
MetadataJson = null
```

nên frontend EN intentionally không dùng raw Vietnamese legacy message và chuyển sang generic.

---

# 3. Root Cause B — Notification routing đang quá phụ thuộc `ActionUrl`

Frontend hiện có:

```text
getNotificationLink()
```

và đã có một số rewrite tốt theo:

```text
relatedType
role
actionType
visitRequestId
visitInstanceId
targetUrl
```

Tuy nhiên routing hiện chưa đầy đủ.

Một số notification khác nhau đang cùng sử dụng:

```text
OPEN_VISIT_DETAIL
```

mặc dù semantic khác nhau hoàn toàn:

```text
cần duyệt
đã duyệt
được gán Host
bị từ chối
bị hủy
Host thay đổi
HO theo dõi
...
```

Do đó một `ActionType` quá chung không đủ quyết định màn hình đúng.

---

# 4. Nguyên tắc kiến trúc bắt buộc

Tách rõ 4 khái niệm:

```text
1. Notification Event
2. Presentation
3. Business Target
4. Navigation Intent
```

Mô hình chuẩn:

```text
Business event xảy ra
        ↓
eventKey
+
structured params
+
target IDs
+
navigation intent
        ↓
Notification DB
        ↓
Frontend
        ├─ Presentation Resolver
        │      ↓
        │   VI / EN title + message
        │
        └─ Navigation Resolver
               ↓
          current backend state
               ↓
          đúng màn hình hiện tại
```

Không để `Title / Message` quyết định navigation.

Không parse ID từ text.

Không để `ActionUrl` là source of truth duy nhất.

---

# 5. Notification phải có semantic eventKey

Mỗi notification nghiệp vụ cần có `eventKey` ổn định và language-neutral.

Ví dụ:

```text
VISIT_REQUEST_WAITING_APPROVAL
CAMPUS_APPROVED
CAMPUS_REJECTED
HOST_ASSIGNED
HOST_CHANGED
PARTICIPATION_INVITED
PARTICIPATION_RESPONDED
VISIT_CANCELLED
VISIT_CLOSED
LOGISTICS_REQUEST_CREATED
LOGISTICS_ASSIGNED
LOGISTICS_DUE_SOON
AGENDA_REQUIRED
AGENDA_UPDATED
MINUTES_CREATED
ACTION_ITEM_ASSIGNED
NEWS_PENDING_APPROVAL
NEWS_REVIEWED
PARTNER_PENDING_APPROVAL
PARTNER_REVIEWED
ACCOUNT_CREATED
ACCOUNT_LOCKED
ACCOUNT_UNLOCKED
...
```

Không bắt buộc dùng chính xác tên trên nếu project đã có vocabulary tương đương.

Ưu tiên reuse existing constants. Không tạo duplicate eventKey mang cùng semantic.

---

# 6. Notification metadata phải chứa structured params

Ví dụ:

```json
{
  "eventKey": "VISIT_REQUEST_WAITING_APPROVAL",
  "params": {
    "delegationName": "SeoulTech Global Engagement Center",
    "requestCode": "VR-2026-00123",
    "campusName": "FPT University Hà Nội"
  }
}
```

Không lưu sentence hoàn chỉnh vào params.

Sai:

```json
{
  "message": "Đoàn ABC đang chờ bạn duyệt..."
}
```

Đúng:

```json
{
  "delegationName": "ABC",
  "requestCode": "VR-123",
  "campusName": "FPT University Hà Nội"
}
```

Frontend chịu trách nhiệm render language.

---

# 7. Bổ sung locale VI + EN đầy đủ

### VI

```json
"VISIT_REQUEST_WAITING_APPROVAL": {
  "title": "Có yêu cầu tiếp khách cần duyệt",
  "message": "{{delegationName}} đang chờ bạn xử lý tại {{campusName}}."
}
```

### EN

```json
"VISIT_REQUEST_WAITING_APPROVAL": {
  "title": "Visit request requires your review",
  "message": "{{delegationName}} is waiting for your review at {{campusName}}."
}
```

Khi đổi language `VI → EN`, notification hiện tại phải re-render ngay.

Không cần tạo notification mới. Không cần gọi lại backend chỉ để đổi language.

---

# 8. Mở rộng `resolveNotificationPresentation`

Audit:

```text
frontend/pems-react/src/features/notifications/utils/resolveNotificationPresentation.ts
```

Không để `KNOWN_EVENT_KEYS` chỉ bao phủ Guest/Visitor nếu resolver đang được dùng cho:

```text
Staff
Staff Leader
Department
HO
Admin
Student
Visitor
```

Nếu resolver là global notification presentation resolver thì vocabulary phải đủ cho notification global.

Không được để:

```text
Staff Leader notification
→ unknown
→ You have a new notification.
```

---

# 9. Bell và NotificationsPage phải dùng cùng presentation resolver

Các surface:

```text
NotificationBellButton
NotificationsPage
NotificationDetailModal
VisitorNotificationsSection
các notification widget khác nếu có
```

phải dùng cùng semantic resolver.

Không để:

```text
Bell → generic
NotificationsPage → raw Vietnamese
Modal → message khác
```

Cùng một notification phải có cùng title/message trên mọi surface.

---

# 10. Repo-wide audit tất cả notification producers

Search toàn backend:

```text
CreateNotificationRequest(
CreateNotificationItem(
notificationService.CreateAsync(
notificationService.CreateManyAsync(
INotificationService
NotificationTypes.
ActionType:
ActionUrl:
MetadataJson:
```

Lập bảng đầy đủ:

| Event | Producer | Recipient | EventKey | Target IDs | Action intent | Destination |
|---|---|---|---|---|---|---|

Không được chỉ audit vài notification đang thấy trong screenshot.

---

# 11. Semantic Routing Resolver

Tạo hoặc chuẩn hóa một resolver duy nhất cho navigation.

Ví dụ conceptual:

```text
resolveNotificationDestination(
    notification,
    currentUser,
    currentBusinessState
)
```

Không đặt routing logic phân tán tại Bell, NotificationsPage, Dashboard, Header.

Bell và NotificationsPage phải reuse cùng resolver.

---

# 12. Notification “có đoàn cần duyệt”

Backend phải carry đủ context:

```text
VisitRequestId
VisitInstanceId
CampusId
```

Presentation:

```text
Có yêu cầu tiếp khách cần duyệt
{{delegationName}} đang chờ bạn xử lý tại {{campusName}}.
```

Click:

```text
notification
↓
openVisitRequestId
+
openVisitInstanceId
↓
consume one-shot URL
↓
fetch CURRENT state
↓
CAMPUS_REVIEW nếu hiện tại vẫn pending
```

Nếu request đã được duyệt thì không mở approval UI stale mà mở current detail/context.

---

# 13. Notification “Bạn được gán làm Host”

Expected:

```text
Host
→ đúng VisitInstance
→ Host Process / Setup đoàn khách
```

Không chỉ mở Visit Management list.

Nếu user không còn là Host khi click notification cũ:

```text
fetch current state
→ không ép Host Process
→ mở current allowed detail
```

---

# 14. Notification “Bạn không còn là Host”

Không được redirect người dùng vào Host Process nếu current relation đã không còn là Host.

Expected:

```text
open current visit detail
```

hoặc context mà user hiện còn quyền xem.

---

# 15. Participant Invitation

Notification:

```text
Bạn được mời tham gia đoàn
```

Expected:

```text
Attending / invitation context
```

và phải thấy `Accept / Decline` nếu invitation hiện tại còn pending.

Không đưa IC Staff vào Host Process chỉ vì role Staff.

---

# 16. Campus approved / rejected

Visitor/Guest side:

```text
Campus approved
→ đúng request/campus
→ read current detail

Campus rejected
→ đúng request/campus
→ hiển thị current rejected state/reason
```

HO: nếu message nói cụ thể Campus A thì click nên resolve `request + instance Campus A`, không chỉ mở request-level chung nếu UI có khả năng mở campus-specific context.

---

# 17. Cancelled Visit

Notification:

```text
Lịch thăm đã bị hủy
```

Expected:

```text
current read-only detail
```

Không được route tới process screen nếu visit/campus đã cancelled và screen đó chỉ dành cho active Host process.

---

# 18. News Notification

Các notification:

```text
Tin tức đang chờ duyệt
Tin tức đã được duyệt
Tin tức bị từ chối
```

phải mở đúng:

```text
/dashboard/news?newsId=...
```

hoặc semantic detail/edit/review screen phù hợp current user.

---

# 19. Partner Notification

Các notification:

```text
Đối tác chờ duyệt
Đối tác được duyệt
Đối tác bị từ chối
```

phải mở đúng Partner record, không chỉ `/dashboard/partners` nếu có ID.

---

# 20. Account notification phải audit lại

Hiện có trường hợp notification gửi cho chính account recipient nhưng:

```text
ActionUrl = /dashboard/accounts
```

Điều này cần audit nghiêm túc.

Ví dụ Student, Visitor, Department Staff, Staff có thể không có quyền Account Management.

Không được:

```text
ACCOUNT_CREATED
→ recipient click
→ /dashboard/accounts
→ 403
```

Phải xác định destination phù hợp quyền thực tế.

Không tự đoán; đọc permission + actual UX trước khi quyết định.

---

# 21. Logistics

Audit toàn bộ:

```text
LOGISTICS_REQUEST_CREATED
LOGISTICS_ASSIGNED
LOGISTICS_ASSIGNEE_RESPONDED
LOGISTICS_PROPOSAL_CREATED
LOGISTICS_PROPOSAL_RESPONDED
LOGISTICS_READY
LOGISTICS_DONE
LOGISTICS_HANDOVER_REQUIRED
LOGISTICS_HANDOVER_SIGNED
LOGISTICS_DUE_SOON
LOGISTICS_OVERDUE
```

Nếu notification nói tới một logistics item cụ thể thì phải carry:

```text
VisitInstanceId
LogisticsItemId
```

và click phải mở đúng item/context.

---

# 22. Agenda / Minutes / Action Item

Audit:

```text
AGENDA_REQUIRED
AGENDA_UPDATED
MINUTES_CREATED
MINUTES_UPDATED
ACTION_ITEM_ASSIGNED
ACTION_ITEM_DUE
```

phải xác định đúng business IDs nếu domain có identifier tương ứng và click phải đưa user đến nội dung được nhắc.

---

# 23. Stale Notification Rule

Giữ nguyên:

```text
Notification = WHERE TO GO

Current backend state = WHAT IS TRUE NOW

Current authorization = WHAT USER CAN DO NOW
```

Không dùng historical notification để ép stale action.

Ví dụ notification lúc tạo nói "Bạn cần duyệt" nhưng current state đã duyệt:

```text
mở đúng target
→ current detail
→ không APPROVE lần nữa
```

---

# 24. One-shot deep-link

Các notification cần mở modal/context cụ thể phải tiếp tục dùng one-shot navigation command.

Ví dụ:

```text
/dashboard/visit?openVisitRequestId=123&openVisitInstanceId=456
```

Sau khi consume:

```text
delete openVisitRequestId
delete openVisitInstanceId
setSearchParams(..., { replace: true })
```

Sau đó UI được quản lý bằng React state.

Đóng modal → state = null.

Không replay khi search/filter/tab/pagination/sort/refresh.

---

# 25. Không dùng ActionType quá chung nếu semantic khác nhau

Audit `OPEN_VISIT_DETAIL`.

Nếu action type này đang đại diện cho quá nhiều behavior khác nhau thì cân nhắc semantic action types như:

```text
OPEN_CAMPUS_REVIEW
OPEN_VISIT_DETAIL
OPEN_HOST_PROCESS
OPEN_VISIT_INVITATION
OPEN_CONTRIBUTION
OPEN_LOGISTICS_DETAIL
OPEN_HANDOVER_DETAIL
OPEN_NEWS_DETAIL
OPEN_PARTNER_DETAIL
```

Không bắt buộc dùng đúng tên trên, nhưng action intent phải đủ rõ để frontend không phải đoán từ URL string.

---

# 26. Structured IDs quan trọng hơn ActionUrl

Ưu tiên:

```text
eventKey
actionType
relatedType
relatedId
visitRequestId
visitInstanceId
campusId
structured target IDs
```

`ActionUrl` có thể giữ để backward compatibility/fallback nhưng không nên là nguồn semantic duy nhất.

---

# 27. Legacy Notification

Notification cũ trong DB có thể có:

```text
MetadataJson = null
```

Không được bỏ chúng.

### VI

Có thể tiếp tục hiển thị legacy Title/Message nếu đó là dữ liệu system Vietnamese cũ.

### EN

Không được hiển thị raw VI.

Ưu tiên backfill semantic metadata cho các notification có thể reconstruct chắc chắn.

---

# 28. Backfill notification cũ

Có thể reconstruct dựa trên:

```text
notification_type
related_type
related_id
visit_request_id
visit_instance_id
campus_id
action_type
```

Không reconstruct chỉ bằng text matching nếu không có bằng chứng chắc chắn.

Nếu không thể xác định event an toàn thì giữ UNKNOWN.

---

# 29. Không để `unknown` trở thành trạng thái bình thường

Sau fix, `unknown` chỉ dành cho:

- historical data không reconstruct được;
- data corrupt;
- unrecognized future event.

Không được để notification mới tạo hôm nay lại hiện:

```text
You have a new notification.
```

---

# 30. Automated Guard

Thêm regression guard/test:

```text
all known semantic notification producers
→ eventKey exists
→ VI locale exists
→ EN locale exists
```

Nếu thêm event mới nhưng quên locale thì test FAIL.

---

# 31. Presentation Tests

Tối thiểu:

### P-01
`VISIT_REQUEST_WAITING_APPROVAL` ở VI → title/message cụ thể, có delegation/campus.

### P-02
Same notification ở EN → không được `You have a new notification.`

### P-03
Switch VI → EN runtime → notification row đổi language ngay.

### P-04
Known event thiếu translation → test fail.

---

# 32. Routing Tests

| Notification | Recipient | Expected |
|---|---|---|
| Visit waiting approval | Staff Leader | exact campus review |
| Host assigned | Host | Host Process |
| Host changed outgoing | old Host | current detail |
| Participant invitation | Participant | attending/invitation |
| Campus approved | Visitor | current visit detail |
| Campus rejected | Visitor | current rejected detail |
| Campus decision | HO | correct request/campus |
| Visit cancelled | relevant users | current cancelled detail |
| News pending | reviewer | exact news |
| Partner pending | reviewer | exact partner |
| Account created | recipient | authorized destination |
| Logistics assigned | assignee | exact logistics item |

---

# 33. Cross-role Permission Test

Với từng notification, recipient role phải có quyền mở destination.

Không được tạo notification mà trong normal intended flow:

```text
recipient
→ click
→ 403
```

Nếu quyền đã thay đổi sau khi notification được tạo thì 403 có thể hợp lệ.

---

# 34. Bell và Full Notifications Page

Cùng một item, click từ Bell và từ `/notifications` phải có:

```text
same presentation
same destination
same current-state behavior
```

Không duplicate logic.

---

# 35. Manual Real-stack Verification

Dùng:

```text
MySQL thật
Backend .NET thật
Frontend Vite thật
Browser Playwright thật
```

Test notification thật cho ít nhất:

```text
Staff Leader
Staff/Host
Visitor
Department
HO
```

Nếu có thể thêm Admin, Student.

---

# 36. Scenario bắt buộc — Staff Leader screenshot hiện tại

Tạo request thật khiến Staff Leader nhận notification.

Bell VI phải hiển thị:

```text
Có yêu cầu tiếp khách cần duyệt

Campus-level pending Staff Leader approval tour
đang chờ bạn xử lý tại FPT University Hà Nội.
```

Không được:

```text
You have a new notification.
```

EN:

```text
Visit request requires your review

Campus-level pending Staff Leader approval tour
is waiting for your review at FPT University Hanoi.
```

Click:

```text
→ đúng request
→ đúng Hà Nội instance
→ CAMPUS_REVIEW nếu còn pending
```

---

# 37. Scenario stale notification

Sau đó approve request.

Click lại notification cũ.

Expected:

```text
mở đúng request/instance
→ fetch current state
→ hiện Đã duyệt/current state
→ không mở stale Approve action
```

---

# 38. Không được làm

Không:

```text
unknown → luôn item.title
```

vì EN sẽ leak VI.

Không:

```text
if notificationType === VISIT
→ /dashboard/visit
```

vì quá chung.

Không parse navigation từ title/message.

Không hardcode mỗi screenshot.

Không sửa chỉ Bell mà bỏ NotificationsPage.

Không đổi mọi notification thành `OPEN_VISIT_DETAIL`.

Không tạo route mà recipient không có quyền.

Không auto mutation khi click notification.

Không dùng page reload để xử lý navigation.

---

# 39. Definition of Done

Task chỉ được coi DONE khi:

- [ ] Không còn notification mới hợp lệ hiện `You have a new notification.`.
- [ ] Mọi notification mới cần bilingual đều có semantic `eventKey`.
- [ ] Mỗi `eventKey` có VI translation.
- [ ] Mỗi `eventKey` có EN translation.
- [ ] Structured params đầy đủ.
- [ ] Bell và NotificationsPage dùng cùng presentation resolver.
- [ ] Bell và NotificationsPage dùng cùng navigation resolver.
- [ ] Staff Leader pending approval mở đúng campus.
- [ ] Host assignment mở đúng Host Process.
- [ ] Participant invitation mở đúng invitation context.
- [ ] Visitor approved/rejected notification mở đúng current detail.
- [ ] HO campus event không đưa vào Host screen.
- [ ] News mở đúng News record.
- [ ] Partner mở đúng Partner record.
- [ ] Account notification không dẫn recipient thường vào Account Management sai quyền.
- [ ] Logistics notification mở đúng item/context.
- [ ] Agenda/Minutes/Action Item notification được audit.
- [ ] Current backend state thắng stale notification state.
- [ ] One-shot navigation không replay.
- [ ] Legacy notifications có fallback/backfill strategy.
- [ ] Notification mới không được tạo `MetadataJson=null` nếu thuộc bilingual semantic inventory.
- [ ] FE tests pass.
- [ ] BE tests liên quan pass.
- [ ] lint pass.
- [ ] build pass.
- [ ] Real-stack browser verification pass.

---

# 40. Báo cáo cuối bắt buộc

## A. Baseline

```text
Branch:
HEAD:
```

## B. Producer inventory

| Event | Producer file | Recipient | eventKey | Params |
|---|---|---|---|---|

## C. Routing matrix

| Event | Recipient | Before | After |
|---|---|---|---|

## D. I18n matrix

| eventKey | VI | EN |
|---|---|---|

## E. Legacy handling

```text
Backfilled:
Not backfilled:
Reason:
```

## F. Tests

```text
Frontend:
Backend:
Lint:
Build:
Playwright:
Manual:
```

## G. Remaining exceptions

Nếu còn notification generic:

```text
Notification type:
Producer:
Why cannot map:
Risk:
```

Không được chỉ báo `"Notification đã fix."`

Phải chứng minh cả:

```text
WHAT USER SEES
+
WHERE CLICK GOES
```

đều đúng.

---

# Nguyên tắc chốt

Hệ thống notification PEMS phải tuân theo:

```text
EVENT KEY
    ↓
cho biết chuyện gì đã xảy ra

STRUCTURED PARAMS
    ↓
cho biết dữ liệu nào cần đưa vào câu

VI / EN TEMPLATE
    ↓
cho biết người dùng nhìn thấy gì

BUSINESS TARGET IDS
    ↓
cho biết notification nói về đối tượng nào

NAVIGATION INTENT
    ↓
cho biết loại màn hình nào cần mở

CURRENT BACKEND STATE
    ↓
cho biết đối tượng hiện tại đang ở trạng thái gì

CURRENT AUTHORIZATION
    ↓
cho biết user hiện tại được phép làm gì
```

Kết quả cuối:

```text
Notification nói gì
=
Notification hiển thị gì
=
Đối tượng mà click mở tới
```

phải thống nhất về mặt nghiệp vụ.

Không còn notification mới có ý nghĩa cụ thể nhưng lại hiện:

```text
You have a new notification.
```

Và không còn:

```text
"Có đoàn cần duyệt"
→ click
→ mở một trang chung không biết đoàn nào
```

mà phải là:

```text
"Có đoàn ABC cần duyệt tại Hà Nội"
→ click
→ đúng ABC
→ đúng Hà Nội
→ đúng current review/detail context.
```

Phần này phải được thực hiện như một **notification-system audit**, không chỉ vá vài notification đang xuất hiện trên ảnh.
