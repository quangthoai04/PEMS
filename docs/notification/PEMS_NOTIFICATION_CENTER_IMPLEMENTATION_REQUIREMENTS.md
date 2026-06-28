# PEMS — Yêu cầu triển khai code chi tiết cho phần Thông báo (UC-09 View Notifications)

> **Mục đích file:** Dùng làm prompt/yêu cầu triển khai cho AI Coding Agent hoặc Developer khi code phần **Notification Center / Dashboard Alerts** trong hệ thống PEMS.
>
> **Phạm vi:** In-app notification cá nhân theo từng user, hiển thị bằng icon chuông + badge số chưa đọc + popover danh sách thông báo như ảnh mẫu.
>
> **Nguyên tắc nguồn chuẩn:** Ưu tiên SQL/schema v10 hiện tại, `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`, `VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md`, `USE_CASE_NOTES.md`, sau đó mới đến code hiện tại.

---

## 1. Bối cảnh nghiệp vụ

Trong PEMS, thông báo là kênh nhắc việc/nội bộ giúp user biết những sự kiện cần xử lý hoặc cần theo dõi, ví dụ:

- Được phân công Host.
- Có đơn chờ duyệt.
- Có yêu cầu logistics mới.
- Department Leader gán nhiệm vụ cho Department Staff.
- Người được mời xác nhận/từ chối tham gia.
- Logistics sắp đến hạn/quá hạn.
- Có bài news hoặc partner chờ duyệt.
- Trạng thái đoàn/đơn thay đổi.
- Có hành động qua email action token đã được phản hồi.

Thông báo phải là **thông báo cá nhân theo từng `recipient_user_id`**, không phải thông báo global theo role. Mỗi dòng trong bảng `notifications` thuộc về đúng một user nhận.

---

## 2. Phạm vi triển khai

### 2.1. Phải triển khai

- Backend API đọc danh sách thông báo của user hiện tại.
- Backend API đếm số thông báo chưa đọc.
- Backend API đánh dấu một thông báo là đã đọc.
- Backend API đánh dấu tất cả thông báo của user hiện tại là đã đọc.
- Backend service dùng chung để tạo thông báo sau các business event.
- Frontend notification bell ở header/dashboard layout.
- Badge đỏ hiển thị số lượng unread.
- Popover danh sách thông báo mới nhất.
- Click vào thông báo thì mark read và điều hướng tới màn liên quan nếu có thể.
- Polling nhẹ để cập nhật unread count.
- Chống lộ dữ liệu: user chỉ xem/mark read thông báo của chính mình.

### 2.2. Chưa triển khai trong scope này

- Không làm realtime SignalR/WebSocket ở phase đầu.
- Không thêm inbox email thật.
- Không thêm bảng `email_threads`, `email_messages`, `email_message_recipients`.
- Không để frontend tự tạo notification giả bằng local state.
- Không tạo notification bằng trigger SQL phức tạp.
- Không sửa schema nếu chưa thật sự cần.
- Không xóa notification khi user đọc, vì bảng hiện tại không có `deleted_at` hoặc `archived_at`.

---

## 3. Cơ sở dữ liệu hiện tại

Dùng bảng hiện có: `notifications`.

### 3.1. Field mapping

| Column | Cách dùng trong code |
|---|---|
| `notification_id` | Khóa chính thông báo. |
| `recipient_user_id` | User nhận thông báo. Mọi query bắt buộc filter theo current user. |
| `title` | Tiêu đề ngắn hiển thị trong popover, ví dụ `Bạn được phân công Host`. |
| `message` | Nội dung mô tả chi tiết hơn, có thể null. |
| `notification_type` | Loại thông báo nghiệp vụ. Do DB dùng `VARCHAR(80)`, backend/frontend phải dùng constants chung để tránh sai chính tả. |
| `related_type` | Loại entity liên quan, ví dụ `VISIT_REQUEST`, `VISIT_INSTANCE`, `LOGISTICS_ITEM`. Có thể null với thông báo hệ thống. |
| `related_id` | ID bản ghi liên quan. Vì không có FK cứng, backend phải tự validate khi cần điều hướng/kiểm tra scope. |
| `is_read` | `false` = chưa đọc, `true` = đã đọc. |
| `read_at` | Thời điểm đánh dấu đã đọc. Null nếu chưa đọc. |
| `created_at` | Thời điểm tạo thông báo. Dùng sort desc và hiển thị time ago. |

### 3.2. Không yêu cầu patch SQL ở phase đầu

Không cần thêm cột `target_url`, `priority`, `metadata_json`, `dedup_key` trong phase này.

Frontend/backend sẽ tự compute `targetUrl` từ `related_type + related_id`. Nếu sau này cần notification phức tạp hơn thì mới cân nhắc patch SQL.

### 3.3. Query rule bắt buộc

```sql
-- Danh sách thông báo của tôi
SELECT *
FROM notifications
WHERE recipient_user_id = @CurrentUserId
ORDER BY created_at DESC
LIMIT @PageSize OFFSET @Offset;

-- Số chưa đọc
SELECT COUNT(*)
FROM notifications
WHERE recipient_user_id = @CurrentUserId
  AND is_read = FALSE;

-- Mark one read
UPDATE notifications
SET is_read = TRUE,
    read_at = COALESCE(read_at, NOW())
WHERE notification_id = @NotificationId
  AND recipient_user_id = @CurrentUserId;

-- Mark all read
UPDATE notifications
SET is_read = TRUE,
    read_at = COALESCE(read_at, NOW())
WHERE recipient_user_id = @CurrentUserId
  AND is_read = FALSE;
```

Tuyệt đối không query notification chỉ theo `role_code`, `campus_id`, `related_id` nếu không filter `recipient_user_id`.

---

## 4. Constants chuẩn hóa

Vì `notification_type` và `related_type` là `VARCHAR`, phải tạo constants ở backend và frontend.

### 4.1. Backend constants gợi ý

Tạo file gợi ý:

```text
backend/PEMS.Domain/Constants/NotificationTypes.cs
backend/PEMS.Domain/Constants/NotificationRelatedTypes.cs
```

```csharp
public static class NotificationTypes
{
    public const string VisitRequestSubmitted = "VISIT_REQUEST_SUBMITTED";
    public const string CrossCampusRequestSubmitted = "CROSS_CAMPUS_REQUEST_SUBMITTED";
    public const string VisitRequestApproved = "VISIT_REQUEST_APPROVED";
    public const string VisitRequestRejected = "VISIT_REQUEST_REJECTED";
    public const string WaitingHostAssignment = "WAITING_HOST_ASSIGNMENT";
    public const string HostAssigned = "HOST_ASSIGNED";
    public const string VisitStatusChanged = "VISIT_STATUS_CHANGED";
    public const string VisitCancelled = "VISIT_CANCELLED";
    public const string VisitReadyToClose = "VISIT_READY_TO_CLOSE";
    public const string VisitClosed = "VISIT_CLOSED";

    public const string ParticipationInvited = "PARTICIPATION_INVITED";
    public const string ParticipationResponded = "PARTICIPATION_RESPONDED";
    public const string ParticipationRemoved = "PARTICIPATION_REMOVED";

    public const string LogisticsRequestCreated = "LOGISTICS_REQUEST_CREATED";
    public const string LogisticsAssigned = "LOGISTICS_ASSIGNED";
    public const string LogisticsAssigneeResponded = "LOGISTICS_ASSIGNEE_RESPONDED";
    public const string LogisticsProposalCreated = "LOGISTICS_PROPOSAL_CREATED";
    public const string LogisticsProposalResponded = "LOGISTICS_PROPOSAL_RESPONDED";
    public const string LogisticsReady = "LOGISTICS_READY";
    public const string LogisticsDone = "LOGISTICS_DONE";
    public const string LogisticsHandoverRequired = "LOGISTICS_HANDOVER_REQUIRED";
    public const string LogisticsHandoverSigned = "LOGISTICS_HANDOVER_SIGNED";
    public const string LogisticsDueSoon = "LOGISTICS_DUE_SOON";
    public const string LogisticsOverdue = "LOGISTICS_OVERDUE";

    public const string AgendaRequired = "AGENDA_REQUIRED";
    public const string AgendaUpdated = "AGENDA_UPDATED";
    public const string VisitReminder = "VISIT_REMINDER";

    public const string MinutesCreated = "MINUTES_CREATED";
    public const string MinutesUpdated = "MINUTES_UPDATED";
    public const string ActionItemAssigned = "ACTION_ITEM_ASSIGNED";

    public const string NewsPendingApproval = "NEWS_PENDING_APPROVAL";
    public const string NewsReviewed = "NEWS_REVIEWED";
    public const string PartnerPendingApproval = "PARTNER_PENDING_APPROVAL";
    public const string PartnerReviewed = "PARTNER_REVIEWED";

    public const string AccountCreated = "ACCOUNT_CREATED";
    public const string AccountStatusChanged = "ACCOUNT_STATUS_CHANGED";
    public const string SystemAlert = "SYSTEM_ALERT";
}
```

```csharp
public static class NotificationRelatedTypes
{
    public const string VisitRequest = "VISIT_REQUEST";
    public const string VisitInstance = "VISIT_INSTANCE";
    public const string VisitParticipant = "VISIT_PARTICIPANT";
    public const string LogisticsItem = "LOGISTICS_ITEM";
    public const string LogisticsHandover = "LOGISTICS_HANDOVER";
    public const string Agenda = "AGENDA";
    public const string Minutes = "MINUTES";
    public const string MinuteActionItem = "MINUTE_ACTION_ITEM";
    public const string News = "NEWS";
    public const string Partner = "PARTNER";
    public const string CalendarEvent = "CALENDAR_EVENT";
    public const string Account = "ACCOUNT";
    public const string System = "SYSTEM";
}
```

### 4.2. Frontend constants gợi ý

```text
frontend/pems-react/src/features/notifications/constants/notificationTypes.ts
frontend/pems-react/src/features/notifications/constants/notificationRelatedTypes.ts
```

Frontend không được hard-code chuỗi rải rác trong component.

---

## 5. Backend architecture yêu cầu

Dự án đang theo Clean Architecture + MediatR. Controller chỉ nhận request, gọi `IMediator`, trả response. Business logic nằm trong Application Handler/Service.

### 5.1. Entity/DbContext

Kiểm tra hiện tại đã có entity cho `notifications` chưa.

Nếu chưa có, tạo:

```text
backend/PEMS.Domain/Entities/Notification.cs
backend/PEMS.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs
```

Entity tối thiểu:

```csharp
public class Notification
{
    public ulong NotificationId { get; set; }
    public ulong RecipientUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? RecipientUser { get; set; }
}
```

Configuration phải map đúng:

```text
table: notifications
PK: notification_id
FK: recipient_user_id -> users.user_id
index: recipient_user_id, is_read, created_at
index: related_type, related_id
index: notification_type, created_at
```

Không đổi tên cột SQL.

### 5.2. Application folder

Tạo feature theo cấu trúc:

```text
backend/PEMS.Application/Notifications/
├── Common/
│   ├── NotificationDto.cs
│   ├── NotificationQueryFilters.cs
│   ├── NotificationRouteResolver.cs
│   ├── NotificationConstants.cs nếu chưa đặt ở Domain
│   ├── INotificationService.cs
│   └── NotificationService.cs
├── Queries/
│   ├── GetMyNotifications/
│   │   ├── GetMyNotificationsQuery.cs
│   │   ├── GetMyNotificationsQueryHandler.cs
│   │   └── GetMyNotificationsQueryValidator.cs
│   └── GetMyUnreadNotificationCount/
│       ├── GetMyUnreadNotificationCountQuery.cs
│       └── GetMyUnreadNotificationCountQueryHandler.cs
└── Commands/
    ├── MarkNotificationAsRead/
    │   ├── MarkNotificationAsReadCommand.cs
    │   └── MarkNotificationAsReadCommandHandler.cs
    └── MarkAllNotificationsAsRead/
        ├── MarkAllNotificationsAsReadCommand.cs
        └── MarkAllNotificationsAsReadCommandHandler.cs
```

### 5.3. API Controller

Nếu chưa có controller, tạo:

```text
backend/PEMS.Api/Controllers/NotificationsController.cs
```

Route đề xuất:

```http
GET   /api/notifications?page=1&pageSize=10&isRead=
GET   /api/notifications/unread-count
PATCH /api/notifications/{notificationId}/read
PATCH /api/notifications/mark-all-read
```

Controller không được query DbContext trực tiếp.

### 5.4. DTO response

```csharp
public sealed class NotificationDto
{
    public ulong NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string? RelatedType { get; set; }
    public ulong? RelatedId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TimeAgoText { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
}
```

`TargetUrl` là computed field, không lưu DB.

### 5.5. Pagination response

Dùng format phân trang hiện có trong project. Nếu chưa có format chung, response nên có:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 10,
  "totalItems": 31,
  "totalPages": 4
}
```

Validation:

```text
page >= 1
pageSize từ 1 đến 50, default 10
isRead optional: true/false/null
```

---

## 6. NotificationService tạo thông báo

### 6.1. Interface

```csharp
public interface INotificationService
{
    Task CreateAsync(
        ulong recipientUserId,
        string title,
        string? message,
        string notificationType,
        string? relatedType,
        ulong? relatedId,
        CancellationToken cancellationToken);

    Task CreateManyAsync(
        IEnumerable<CreateNotificationItem> items,
        CancellationToken cancellationToken);
}

public sealed record CreateNotificationItem(
    ulong RecipientUserId,
    string Title,
    string? Message,
    string NotificationType,
    string? RelatedType,
    ulong? RelatedId);
```

### 6.2. Rule tạo thông báo

Khi tạo thông báo:

```text
[ ] recipient_user_id phải tồn tại.
[ ] User nhận phải ACTIVE nếu notification dành cho user đang hoạt động.
[ ] Title required, trim, max 255.
[ ] notification_type required, max 80.
[ ] related_type max 80 nếu có.
[ ] related_id chỉ set khi có related_type.
[ ] is_read = false.
[ ] read_at = null.
[ ] created_at = now.
[ ] Loại bỏ duplicate recipient trong CreateManyAsync.
[ ] Không throw làm hỏng toàn bộ nghiệp vụ nếu notification không quan trọng? Tùy policy hiện tại. Khuyến nghị: trong cùng transaction với nghiệp vụ chính cho event quan trọng; nếu lỗi dữ liệu notification do code, rollback để phát hiện bug.
```

### 6.3. Chống trùng thông báo

Do schema hiện tại chưa có `dedup_key`, chống trùng bằng query mềm trước khi insert với các reminder/job:

```text
Không tạo lại nếu đã tồn tại notification cùng:
recipient_user_id + notification_type + related_type + related_id
trong khoảng thời gian phù hợp.
```

Ví dụ:

```text
VISIT_REMINDER 24h trước: không tạo lại trong cùng ngày.
LOGISTICS_DUE_SOON: không tạo lại trong 12-24h.
LOGISTICS_OVERDUE: có thể tạo mỗi ngày một lần, không tạo mỗi lần polling.
```

Với business event chỉ xảy ra một lần như `HOST_ASSIGNED`, có thể không cần dedupe nếu code đảm bảo assignment chỉ chạy một lần.

---

## 7. Logic tạo thông báo theo business event

### 7.1. Visit Request / Delegation

| Event | Khi nào tạo | Người nhận | Type | Related |
|---|---|---|---|---|
| Submit single-campus | Sau khi tạo request thành công | Staff Leader của campus | `VISIT_REQUEST_SUBMITTED` | `VISIT_REQUEST`, request_id |
| Submit multi-campus | Sau khi tạo request thành công | HO users active | `CROSS_CAMPUS_REQUEST_SUBMITTED` | `VISIT_REQUEST`, request_id |
| HO approve multi-campus | Sau khi request APPROVED và campus instances release | Staff Leader từng campus trong request | `WAITING_HOST_ASSIGNMENT` | `VISIT_INSTANCE`, visit_instance_id |
| HO reject multi-campus | Sau khi reject thành công | Visitor/người tạo đơn | `VISIT_REQUEST_REJECTED` | `VISIT_REQUEST`, request_id |
| Staff Leader approve single-campus | Sau approve thành công | Visitor/người tạo đơn | `VISIT_REQUEST_APPROVED` | `VISIT_REQUEST`, request_id |
| Staff Leader reject single-campus | Sau reject thành công | Visitor/người tạo đơn | `VISIT_REQUEST_REJECTED` | `VISIT_REQUEST`, request_id |
| Staff Leader assign Host | Sau khi set `current_host_user_id` | Host được gán | `HOST_ASSIGNED` | `VISIT_INSTANCE`, visit_instance_id |
| Host chuyển lifecycle | Sau khi status đổi | Staff Leader/participants liên quan nếu cần | `VISIT_STATUS_CHANGED` | `VISIT_INSTANCE`, visit_instance_id |
| Visitor/Host cancel | Sau khi cancel thành công | Staff Leader, Host, Visitor liên quan | `VISIT_CANCELLED` | `VISIT_INSTANCE` hoặc `VISIT_REQUEST` |
| Đủ điều kiện đóng đoàn | Khi backend phát hiện đủ điều kiện close | Host hoặc Staff Leader | `VISIT_READY_TO_CLOSE` | `VISIT_INSTANCE`, visit_instance_id |
| Closed | Sau khi close thành công | Host, Staff Leader, Visitor nếu cần | `VISIT_CLOSED` | `VISIT_INSTANCE`, visit_instance_id |

Rule đặc biệt multi-campus:

```text
Không tạo notification cho Staff Leader/Host/Department/Student của campus con khi multi-campus request vẫn PENDING_APPROVAL và chưa được HO approve/release.
Chỉ sau HO approve, Staff Leader của từng campus mới nhận notification WAITING_HOST_ASSIGNMENT.
```

### 7.2. Participant / Invitation

| Event | Người nhận | Type | Related |
|---|---|---|---|
| Host mời IC support/Department/Student | User được mời | `PARTICIPATION_INVITED` | `VISIT_PARTICIPANT`, participant_id |
| Người được mời accept/decline | Host hoặc người mời | `PARTICIPATION_RESPONDED` | `VISIT_PARTICIPANT`, participant_id |
| User bị remove khỏi đoàn | User bị remove | `PARTICIPATION_REMOVED` | `VISIT_PARTICIPANT`, participant_id |

Nếu phản hồi qua email action token, sau khi token update `visit_participants.status` thành công thì cũng tạo notification cho Host/người liên quan.

### 7.3. Logistics / Resource

| Event | Người nhận | Type | Related |
|---|---|---|---|
| Host gửi request logistics tới phòng ban | Department Leader của phòng đó | `LOGISTICS_REQUEST_CREATED` | `LOGISTICS_ITEM`, logistics_item_id |
| Department Leader gán staff xử lý | Department Staff được gán | `LOGISTICS_ASSIGNED` | `LOGISTICS_ITEM`, logistics_item_id |
| Department Staff accept/decline | Department Leader và Host | `LOGISTICS_ASSIGNEE_RESPONDED` | `LOGISTICS_ITEM`, logistics_item_id |
| Staff đề xuất thay đổi | Host | `LOGISTICS_PROPOSAL_CREATED` | `LOGISTICS_ITEM`, logistics_item_id |
| Host phản hồi đề xuất | Department Leader/Staff liên quan | `LOGISTICS_PROPOSAL_RESPONDED` | `LOGISTICS_ITEM`, logistics_item_id |
| Logistics READY | Host | `LOGISTICS_READY` | `LOGISTICS_ITEM`, logistics_item_id |
| Logistics DONE | Host | `LOGISTICS_DONE` | `LOGISTICS_ITEM`, logistics_item_id |
| Cần ký mượn/ký trả | Bên cần ký | `LOGISTICS_HANDOVER_REQUIRED` | `LOGISTICS_HANDOVER`, handover_id |
| Một bên đã ký | Bên còn lại/Host | `LOGISTICS_HANDOVER_SIGNED` | `LOGISTICS_HANDOVER`, handover_id |
| Gần đến hạn | Assignee hoặc Department Leader | `LOGISTICS_DUE_SOON` | `LOGISTICS_ITEM`, logistics_item_id |
| Quá hạn | Assignee, Department Leader, Host | `LOGISTICS_OVERDUE` | `LOGISTICS_ITEM`, logistics_item_id |

Rule v10:

```text
Ký mượn/ký trả dùng bảng visit_logistics_item_handovers, không dùng field ký cũ trong visit_logistics_items.
Không hỗ trợ chuyển nhiệm vụ logistics từ người A sang người B. Nếu đã assigned_to_user_id thì không đổi sang user khác.
```

### 7.4. Agenda / Calendar / Reminder

| Event | Người nhận | Type | Related |
|---|---|---|---|
| Đơn đã có Host nhưng chưa có agenda | Host | `AGENDA_REQUIRED` | `VISIT_INSTANCE`, visit_instance_id |
| Agenda được cập nhật | Participants liên quan | `AGENDA_UPDATED` | `AGENDA`, agenda_id hoặc `VISIT_INSTANCE` |
| Sắp đến giờ tiếp khách | Host, participants | `VISIT_REMINDER` | `VISIT_INSTANCE`, visit_instance_id |
| Calendar event cá nhân gần tới giờ | Owner user | `VISIT_REMINDER` hoặc `SYSTEM_ALERT` | `CALENDAR_EVENT`, calendar_event_id |

Reminder nên chạy bằng background job nếu project đã có scheduler. Nếu chưa có scheduler, chỉ code service + query sẵn, chưa cần bật job.

### 7.5. News / Partner / Account

| Event | Người nhận | Type | Related |
|---|---|---|---|
| Có news chờ duyệt | Staff Leader đúng scope | `NEWS_PENDING_APPROVAL` | `NEWS`, news_id |
| News được duyệt/từ chối | Người tạo bài | `NEWS_REVIEWED` | `NEWS`, news_id |
| Partner gửi duyệt | Staff Leader theo `owner_campus_id` | `PARTNER_PENDING_APPROVAL` | `PARTNER`, partner_id |
| Partner được duyệt/từ chối | Người tạo partner | `PARTNER_REVIEWED` | `PARTNER`, partner_id |
| Tài khoản được tạo | User mới | `ACCOUNT_CREATED` | `ACCOUNT`, user_id |
| Tài khoản đổi trạng thái | User liên quan nếu cần | `ACCOUNT_STATUS_CHANGED` | `ACCOUNT`, user_id |

---

## 8. Target URL / điều hướng

Vì DB chưa có `target_url`, backend hoặc frontend phải compute từ `related_type + related_id`.

### 8.1. Mapping gợi ý

| Related type | Target URL gợi ý |
|---|---|
| `VISIT_REQUEST` | `/dashboard/visit-requests/{relatedId}` hoặc route hiện có cho request detail |
| `VISIT_INSTANCE` | `/dashboard/visit-process/{relatedId}` |
| `VISIT_PARTICIPANT` | `/dashboard/visit-process/{visitInstanceId}?tab=participants` |
| `LOGISTICS_ITEM` | `/dashboard/visit-process/{visitInstanceId}?tab=logistics` |
| `LOGISTICS_HANDOVER` | `/dashboard/visit-process/{visitInstanceId}?tab=logistics&handoverId={relatedId}` |
| `AGENDA` | `/dashboard/visit-process/{visitInstanceId}?tab=agenda` |
| `MINUTES` | `/dashboard/minutes/{relatedId}` hoặc route hiện có |
| `MINUTE_ACTION_ITEM` | `/dashboard/minutes/{minutesId}?actionItemId={relatedId}` |
| `NEWS` | `/dashboard/news/{relatedId}` |
| `PARTNER` | `/dashboard/partners/{relatedId}` |
| `CALENDAR_EVENT` | `/dashboard/calendar?eventId={relatedId}` |
| `ACCOUNT` | `/dashboard/accounts/{relatedId}` nếu current user có quyền, nếu không thì `/dashboard/profile` |
| `SYSTEM` hoặc null | Không điều hướng hoặc về `/dashboard` |

### 8.2. Scope khi điều hướng

Không được vì notification có `related_id` mà bypass scope.

Khi user click notification:

```text
1. Mark read notification nếu thuộc current user.
2. Điều hướng sang URL liên quan.
3. Màn detail đích vẫn phải tự gọi API detail và backend vẫn check role/scope như bình thường.
4. Nếu user không còn quyền xem object đích, detail API trả 403/404, frontend hiển thị thông báo phù hợp.
```

---

## 9. API chi tiết

### 9.1. GET /api/notifications

Request:

```http
GET /api/notifications?page=1&pageSize=10&isRead=false
Authorization: Bearer <token>
```

Query params:

| Param | Type | Rule |
|---|---|---|
| `page` | number | Optional, default 1, min 1. |
| `pageSize` | number | Optional, default 10, min 1, max 50. |
| `isRead` | boolean/null | Optional. Null/empty = all. |

Handler logic:

```text
[ ] Lấy currentUserId từ CurrentUserService.
[ ] Query notifications WHERE recipient_user_id = currentUserId.
[ ] Nếu isRead có giá trị, filter theo is_read.
[ ] Sort created_at DESC, notification_id DESC.
[ ] Map sang NotificationDto.
[ ] Compute timeAgoText.
[ ] Compute targetUrl nếu có thể.
[ ] Trả paginated response.
```

Response item mẫu:

```json
{
  "notificationId": 19003,
  "title": "Bạn được phân công Host",
  "message": "Bạn là host của VR-SC-HN-0003 và các chặng liên quan.",
  "notificationType": "HOST_ASSIGNED",
  "relatedType": "VISIT_INSTANCE",
  "relatedId": 3003,
  "isRead": false,
  "readAt": null,
  "createdAt": "2026-06-28T09:00:00",
  "timeAgoText": "Vừa xong",
  "targetUrl": "/dashboard/visit-process/3003"
}
```

### 9.2. GET /api/notifications/unread-count

Response:

```json
{
  "unreadCount": 3
}
```

Handler logic:

```text
SELECT COUNT(*) WHERE recipient_user_id = currentUserId AND is_read = false.
```

### 9.3. PATCH /api/notifications/{notificationId}/read

Handler logic:

```text
[ ] Tìm notification theo notification_id + recipient_user_id = currentUserId.
[ ] Nếu không tồn tại, trả 404 hoặc 403 theo convention project.
[ ] Nếu đã đọc rồi, trả success idempotent, không đổi read_at.
[ ] Nếu chưa đọc, set is_read = true, read_at = now.
[ ] Trả notification đã update hoặc `{ success: true }`.
```

### 9.4. PATCH /api/notifications/mark-all-read

Handler logic:

```text
[ ] Update tất cả notification của current user đang is_read = false.
[ ] Set is_read = true, read_at = now.
[ ] Trả số lượng updated.
```

Response:

```json
{
  "updatedCount": 5
}
```

---

## 10. Frontend implementation

### 10.1. File/folder gợi ý

```text
frontend/pems-react/src/features/notifications/
├── api/notificationsApi.ts
├── components/NotificationBell.tsx
├── components/NotificationPopover.tsx
├── constants/notificationTypes.ts
├── constants/notificationRelatedTypes.ts
├── hooks/useNotifications.ts
├── types/notification.types.ts
└── utils/notificationNavigation.ts
```

Nếu project đã có cấu trúc shared khác, bám theo cấu trúc hiện tại, không tạo trùng service HTTP.

### 10.2. TypeScript types

```ts
export type NotificationItem = {
  notificationId: number;
  title: string;
  message: string | null;
  notificationType: string;
  relatedType: string | null;
  relatedId: number | null;
  isRead: boolean;
  readAt: string | null;
  createdAt: string;
  timeAgoText: string;
  targetUrl: string | null;
};

export type UnreadNotificationCountResponse = {
  unreadCount: number;
};
```

### 10.3. NotificationBell behavior

```text
[ ] Component đặt ở header/dashboard layout, gần avatar/user menu.
[ ] Khi layout mount: gọi unread-count.
[ ] Nếu unreadCount > 0, hiển thị badge đỏ trên icon chuông.
[ ] Badge hiển thị tối đa "99+" nếu count > 99.
[ ] Khi click chuông: mở popover và fetch 5-10 notification mới nhất.
[ ] Khi click ngoài popover hoặc ESC: đóng popover.
[ ] Poll unread-count mỗi 30-60 giây khi user đang ở dashboard.
[ ] Khi tab/browser hidden thì tạm dừng polling nếu có thể.
```

### 10.4. NotificationPopover UI

Theo ảnh mẫu:

```text
Header:
- Title: "Thông báo"
- Action: "Đánh dấu đã đọc"

List item:
- Dot xanh hoặc nền nhẹ nếu unread.
- Title bold.
- Message text phụ, line-clamp 2.
- Time ago ở bên phải hoặc dưới title trên mobile.
- Hover nhẹ.
- Click được nếu có targetUrl.

Footer optional:
- "Xem tất cả" nếu sau này có trang full notification.
```

State UI:

```text
Loading: skeleton hoặc text "Đang tải thông báo..."
Empty: "Bạn chưa có thông báo nào."
Error: "Không tải được thông báo. Vui lòng thử lại."
```

### 10.5. Click notification

```text
[ ] Gọi PATCH /api/notifications/{id}/read.
[ ] Optimistic update isRead=false -> true trong local state.
[ ] Giảm unreadCount nếu item trước đó unread.
[ ] Nếu item có targetUrl thì navigate.
[ ] Nếu mark read API fail nhưng navigate vẫn được thì không crash UI; lần sau count sẽ tự đồng bộ lại.
```

### 10.6. Mark all read

```text
[ ] Gọi PATCH /api/notifications/mark-all-read.
[ ] Set tất cả item đang hiển thị isRead = true.
[ ] Set unreadCount = 0.
[ ] Nếu fail, hiển thị toast/error nhẹ.
```

### 10.7. Styling bắt buộc

- Dùng phong cách enterprise dashboard.
- Primary blue `#004c91` cho text/link chính nếu cần.
- Badge đỏ nhỏ, không quá lớn.
- Popover nền trắng, border slate-200, shadow vừa phải.
- Không dùng gradient mạnh.
- Không dùng animation phức tạp.
- Icon-only button phải có `aria-label` và `title`.
- Popover không được bị cắt bởi parent `overflow-hidden`; kiểm tra layout header.

---

## 11. Job/reminder logic

Nếu project đã có background job/scheduler, thêm job notification reminder. Nếu chưa có, chỉ chuẩn bị service để sau này bật.

### 11.1. Reminder job đề xuất

```text
NotificationReminderJob
Chạy mỗi 30 phút hoặc mỗi giờ.
```

### 11.2. Các reminder nên tạo

| Reminder | Điều kiện | Người nhận | Type | Dedupe |
|---|---|---|---|---|
| Visit sắp diễn ra | planned_start_at trong 24h hoặc 2h tới | Host + participants | `VISIT_REMINDER` | 1 lần/mốc/user/instance |
| Logistics gần đến hạn | due_at trong 24h, status chưa DONE/CANCELLED/REJECTED | assigned user hoặc Department Leader | `LOGISTICS_DUE_SOON` | 1 lần/ngày/user/item |
| Logistics quá hạn | due_at < now, status chưa DONE/CANCELLED/REJECTED | assigned user + Department Leader + Host | `LOGISTICS_OVERDUE` | 1 lần/ngày/user/item |
| Agenda thiếu | instance đã có Host, còn BEFORE_VISIT/ASSIGNED, chưa có agenda | Host | `AGENDA_REQUIRED` | 1 lần/ngày/user/instance |

### 11.3. Dedupe reminder

Vì chưa có `dedup_key`, kiểm tra bằng query:

```sql
SELECT 1
FROM notifications
WHERE recipient_user_id = @UserId
  AND notification_type = @NotificationType
  AND related_type = @RelatedType
  AND related_id = @RelatedId
  AND created_at >= @DedupeFrom;
```

---

## 12. Security / RBAC / scope

### 12.1. Rule bắt buộc

```text
[ ] User chỉ list notification của chính mình.
[ ] User chỉ mark read notification của chính mình.
[ ] Không có API lấy notification theo userId từ client.
[ ] Không tin query/body recipientUserId từ frontend.
[ ] related_id không được dùng để bypass detail scope.
[ ] Notification service chỉ tạo cho user ACTIVE và đúng scope nghiệp vụ.
[ ] Với multi-campus pending HO, không tạo notification cho campus con trước khi HO approve.
[ ] Admin không mặc định nhận notification delegation nghiệp vụ nếu không phải actor hợp lệ.
```

### 12.2. Email action token integration

Khi người nhận bấm nút email:

```text
1. Public email action handler validate token.
2. Nếu token valid và update nghiệp vụ thành công:
   - update email_action_tokens.used_at, used_action, result_status.
   - tạo notification cho người cần biết trong hệ thống.
3. Nếu token đã dùng rồi:
   - trả ALREADY_RESPONDED.
   - không update nghiệp vụ lần hai.
   - không tạo notification phản hồi trùng.
```

---

## 13. Test cases bắt buộc

### 13.1. Backend tests

```text
[ ] User A không xem được notification của User B.
[ ] User A không mark read được notification của User B.
[ ] GET /api/notifications trả sort created_at desc.
[ ] isRead filter hoạt động đúng.
[ ] unread-count chỉ đếm của current user.
[ ] Mark read idempotent: mark lần 2 vẫn success, read_at không đổi.
[ ] Mark all read chỉ update current user.
[ ] NotificationService tạo đúng title/type/related/is_read=false.
[ ] Dedupe reminder không tạo lặp.
[ ] Multi-campus pending HO không tạo notification cho Staff Leader campus con.
[ ] Email action token SUCCESS tạo notification cho Host/Leader liên quan.
[ ] Email action token ALREADY_RESPONDED không tạo duplicate notification.
```

### 13.2. Frontend tests/manual checklist

```text
[ ] Login user có unread notification -> badge đỏ hiện đúng số.
[ ] Không có unread -> không hiện badge hoặc badge = 0 bị ẩn.
[ ] Click chuông mở popover.
[ ] Loading/empty/error state rõ ràng.
[ ] Unread item có dot xanh/nền nhẹ.
[ ] Click item mark read và điều hướng đúng.
[ ] Click Đánh dấu đã đọc -> tất cả item chuyển read, badge về 0.
[ ] Polling cập nhật unread-count sau 30-60 giây.
[ ] Popover không bị tràn/cắt ở 1366px, 1024px và mobile.
[ ] Keyboard: ESC đóng popover; icon button có aria-label.
```

### 13.3. SQL/manual verification

```sql
-- Kiểm tra notification theo user
SELECT notification_id, recipient_user_id, title, notification_type, related_type, related_id, is_read, read_at, created_at
FROM notifications
WHERE recipient_user_id = 4
ORDER BY created_at DESC;

-- Kiểm tra unread count
SELECT COUNT(*) AS unread_count
FROM notifications
WHERE recipient_user_id = 4 AND is_read = FALSE;

-- Kiểm tra không có notification lộ sai user khi mark read
SELECT notification_id, recipient_user_id, is_read, read_at
FROM notifications
WHERE notification_id = @NotificationId;
```

---

## 14. Prompt triển khai cho AI Coding Agent

Dán nguyên khối sau cho AI Agent khi bắt đầu code:

```text
Bạn là Senior Full-stack Developer cho hệ thống PEMS. Hãy triển khai hoàn chỉnh UC-09 View Notifications / Notification Center theo SQL v10 hiện tại.

Bắt buộc đọc trước:
- DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
- PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
- VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
- USE_CASE_NOTES.md
- CLEAN_ARCHITECTURE.md
- PROJECT_STRUCTURE_FULL.md

Mục tiêu:
1. Backend API:
   - GET /api/notifications?page=&pageSize=&isRead=
   - GET /api/notifications/unread-count
   - PATCH /api/notifications/{notificationId}/read
   - PATCH /api/notifications/mark-all-read
2. Backend NotificationService dùng chung để tạo notification sau business event.
3. Frontend NotificationBell + NotificationPopover hiển thị giống ảnh mẫu: icon chuông, badge số chưa đọc, danh sách notification, dot unread, nút Đánh dấu đã đọc.
4. Tích hợp tạo notification vào các event quan trọng: submit/approve/reject visit request, assign host, participant invite/response, logistics request/assign/response/proposal/ready/done/handover, news/partner review, account created/status changed nếu code hiện tại có các handler tương ứng.
5. Không sửa SQL nếu không cần. Dùng bảng notifications hiện có.
6. Không dùng mock data. Không để frontend tự tạo notification giả.
7. Không triển khai inbox email thật hoặc bảng email_threads/email_messages.
8. Không dùng dynamic permissions table. Phân quyền bằng fixed role policy + current user scope.
9. User chỉ xem và mark read notification của chính mình.
10. Sau khi code phải build backend và frontend, báo rõ file changed, logic đã gắn event nào, event nào chưa gắn vì chưa tìm thấy handler.

Quy tắc DB:
- notifications.recipient_user_id là user nhận cụ thể.
- is_read mặc định false, read_at null.
- targetUrl không lưu DB, compute từ related_type + related_id.

Quy tắc frontend:
- Khi layout mount gọi unread-count.
- Click chuông fetch list mới nhất.
- Poll unread-count 30-60 giây khi user ở dashboard.
- Click item mark read rồi navigate nếu có targetUrl.
- Mark all read set unreadCount = 0.

Không được:
- Query notification theo role chung rồi show cho nhiều user.
- Cho user thao tác notification của user khác.
- Tạo notification cho campus con của multi-campus khi HO chưa approve.
- Bỏ scope ở detail page đích.
- Tạo file rác, scaffold trống, NotImplementedException.
```

---

## 15. Definition of Done

Chỉ được báo hoàn thành khi đạt đủ:

```text
[ ] Entity/config/DbContext khớp bảng notifications nếu trước đó thiếu.
[ ] Có API list/unread-count/mark-read/mark-all-read chạy thật bằng DB.
[ ] API luôn dùng currentUserId, không nhận recipientUserId từ client.
[ ] Có NotificationService dùng chung.
[ ] Ít nhất gắn notification vào các handler chính có sẵn trong code hiện tại.
[ ] Frontend có NotificationBell/Popover trong dashboard header.
[ ] Badge unread hoạt động.
[ ] Click item mark read + navigate.
[ ] Mark all read hoạt động.
[ ] Loading/empty/error states đầy đủ.
[ ] Không mock data.
[ ] Không sửa schema ngoài yêu cầu.
[ ] Backend build pass.
[ ] Frontend build pass.
[ ] Có báo cáo file changed + test checklist.
```
