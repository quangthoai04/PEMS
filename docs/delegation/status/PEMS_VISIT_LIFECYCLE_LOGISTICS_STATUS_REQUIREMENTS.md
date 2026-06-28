# PEMS — Đặc tả yêu cầu cập nhật logic trạng thái tiếp khách, hủy, đóng đoàn và logistics

> **Mục đích tài liệu:** Tổng hợp chi tiết toàn bộ các yêu cầu đã chốt để AI Agent/Developer dùng khi audit và cập nhật code backend/frontend cho vòng đời tiếp khách trong PEMS.
>
> **Phạm vi chính:** `visit_requests.status`, `visit_request_campuses.status`, `VisitProcess`, `VisitRequestManagement`, logistics workflow, hủy request/campus instance, đóng đoàn, allowedActions và UI button/readonly state.
>
> **Nguyên tắc:** Backend là source of truth. Frontend chỉ render theo status, allowedActions và scope backend trả về. Không đổi trạng thái chỉ vì user mở trang hoặc gọi API GET.

---

## 1. Quyết định nghiệp vụ đã chốt

### 1.1. Visitor được hủy đơn khi `PENDING_APPROVAL`

Visitor được phép hủy đơn khi request còn ở trạng thái:

```text
PENDING_APPROVAL
```

Khi Visitor hủy đơn trước khi duyệt/chưa có `visitInstanceId`:

```text
visit_requests.status = CANCELLED
```

Yêu cầu xử lý:

- Actor phải là Visitor sở hữu request.
- Lý do hủy bắt buộc.
- Không tạo hoặc không mở `VisitProcess`.
- UI chỉ cho xem form yêu cầu và lý do hủy.
- Không hiện nút “Xem quy trình”.
- Không sinh/cập nhật campus lifecycle nếu chưa có campus instance hợp lệ.

### 1.2. Visitor hủy request đã duyệt

Visitor được hủy request đã duyệt nếu toàn bộ campus instance liên quan **chưa bước vào tiếp khách**.

Cho phép hủy nếu các campus instance chỉ nằm trong các trạng thái:

```text
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
CANCELLED
```

Không cho Visitor hủy nếu có bất kỳ campus instance nào ở:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
```

Thông báo lỗi đề xuất:

```text
Không thể hủy vì chuyến thăm đã bắt đầu hoặc đã kết thúc tại ít nhất một cơ sở.
```

### 1.3. Không tự động chuyển `BEFORE_VISIT -> DURING_VISIT` theo thời gian

Không dùng background job/time-based auto transition cho trạng thái này trong giai đoạn hiện tại.

Cơ chế đã chốt:

```text
Host bấm nút xác nhận ở cuối tab Trước tiếp khách
→ backend chuyển BEFORE_VISIT -> DURING_VISIT
→ frontend chuyển sang tab Trong tiếp khách
→ frontend scroll lên đầu tab Trong tiếp khách.
```

Không chuyển trạng thái khi:

- Host mở trang.
- User xem detail.
- API GET list/detail chạy.
- `planned_start_at` đã tới giờ.

### 1.4. Không tự động chuyển `DURING_VISIT -> AFTER_VISIT` theo thời gian

Không dùng background job/time-based auto transition cho trạng thái này trong giai đoạn hiện tại.

Cơ chế đã chốt:

```text
Host bấm nút xác nhận kết thúc tiếp khách ở cuối tab Trong tiếp khách
→ backend chuyển DURING_VISIT -> AFTER_VISIT
→ frontend chuyển sang tab Sau tiếp khách
→ frontend scroll lên đầu tab Sau tiếp khách.
```

Không chuyển trạng thái khi:

- User mở trang.
- API GET list/detail chạy.
- Đã qua `planned_end_at`.

### 1.5. Chỉ tab “Trước tiếp khách” bị khóa sau khi chuyển giai đoạn

Khi Host xác nhận hoàn thành tab Trước tiếp khách và status chuyển sang `DURING_VISIT`:

- Tab **Trước tiếp khách** chuyển sang read-only.
- Không cho sửa/xóa/thêm các nội dung thuộc tab Trước tiếp khách nữa.
- Tab **Trong tiếp khách** vẫn hoạt động bình thường.
- Tab **Sau tiếp khách** vẫn hoạt động bình thường khi được mở.

Khi chuyển từ `DURING_VISIT` sang `AFTER_VISIT`:

- Không tự khóa toàn bộ tab Trong tiếp khách nếu nghiệp vụ hiện tại vẫn cho phép bổ sung/sửa/xóa thông tin trong giai đoạn sau.
- Chỉ tab Trước tiếp khách là tab bị khóa chắc chắn sau khi đã qua giai đoạn chuẩn bị.
- Không dùng một biến read-only chung để khóa hết cả 3 tab khi chưa `CLOSED` hoặc `CANCELLED`.

### 1.6. Fix lỗi có 2 nút trùng tác dụng trong `VisitProcess`

Hiện tại UI có tình trạng cùng một giai đoạn có 2 nút có tác dụng giống nhau, ví dụ:

- Một nút trong nội dung tab.
- Một nút ở thanh cuối trang/stage bar.

Yêu cầu sửa:

```text
Mỗi tab chỉ có đúng 1 CTA chuyển giai đoạn chính.
```

Quy định CTA theo tab:

| Tab | Nút chính duy nhất | Transition |
|---|---|---|
| Trước tiếp khách | Xác nhận hoàn thành chuẩn bị | `BEFORE_VISIT -> DURING_VISIT` |
| Trong tiếp khách | Xác nhận kết thúc tiếp khách | `DURING_VISIT -> AFTER_VISIT` |
| Sau tiếp khách | Hoàn tất & đóng đoàn | `AFTER_VISIT -> CLOSED` |

Nếu hiện có cả:

```text
Đồng ý chốt đoàn và lưu trữ
Hoàn tất & đóng đoàn
```

mà đều cùng tác dụng đóng đoàn, chỉ giữ lại một nút chính:

```text
Hoàn tất & đóng đoàn
```

---

## 2. State machine chuẩn

### 2.1. Request-level status — `visit_requests.status`

Chỉ dùng các trạng thái:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

Không đưa các trạng thái vận hành campus instance lên `visit_requests.status`, ví dụ không dùng:

```text
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
IN_PROGRESS
COMPLETED
```

### 2.2. Campus instance status — `visit_request_campuses.status`

Các trạng thái hợp lệ:

```text
WAITING_REQUEST_APPROVAL
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Flow chuẩn:

```text
WAITING_REQUEST_APPROVAL
→ WAITING_HOST_ASSIGNMENT
→ ASSIGNED
→ BEFORE_VISIT
→ DURING_VISIT
→ AFTER_VISIT
→ CLOSED
```

Luồng hủy:

```text
PENDING_APPROVAL request
→ CANCELLED request nếu Visitor hủy trước duyệt.

WAITING_HOST_ASSIGNMENT / ASSIGNED / BEFORE_VISIT
→ CANCELLED campus instance nếu actor hợp lệ hủy trước tiếp khách.
```

Không cho hủy từ:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

---

## 3. Transition backend cần audit/cập nhật

### 3.1. `WAITING_HOST_ASSIGNMENT -> ASSIGNED`

Xảy ra khi Staff Leader chọn Host chính thức cho campus instance.

Backend validate:

- Actor là Staff Leader đúng campus.
- Host được chọn là IC Staff thường.
- Host thuộc campus/scope hợp lệ.
- Instance chưa `CANCELLED` hoặc `CLOSED`.
- Không cho đổi host nếu rule đã chốt host assignment là one-time.

Kết quả:

```text
visit_request_campuses.status = ASSIGNED
current_host_user_id = selectedHostId
assigned_by = currentUserId
assigned_at = now
```

Label UI:

| Role xem | Label |
|---|---|
| Staff/Host | Đã được phân công |
| Staff Leader | Đã phân công Host |

### 3.2. `ASSIGNED -> BEFORE_VISIT`

Xảy ra khi Host bấm:

```text
Bắt đầu chuẩn bị
```

Backend validate:

- Actor là `current_host_user_id`.
- Status hiện tại là `ASSIGNED`.
- Không read-only.
- Không `CANCELLED` hoặc `CLOSED`.

Kết quả:

```text
visit_request_campuses.status = BEFORE_VISIT
updated_by = currentUserId
updated_at = now
```

Không tự chuyển chỉ vì Host mở trang.

### 3.3. `BEFORE_VISIT -> DURING_VISIT`

Xảy ra khi Host bấm:

```text
Xác nhận hoàn thành chuẩn bị
```

Backend validate:

- Actor là `current_host_user_id`.
- Status hiện tại là `BEFORE_VISIT`.
- Không `CANCELLED` hoặc `CLOSED`.
- Có thể validate tối thiểu các thông tin bắt buộc ở tab Trước tiếp khách đã được lưu hợp lệ nếu project đã có rule.

Kết quả:

```text
visit_request_campuses.status = DURING_VISIT
updated_by = currentUserId
updated_at = now
```

Frontend sau success:

- Reload detail/permissions/allowedActions.
- Khóa tab Trước tiếp khách read-only.
- `setActiveTab('during')`.
- Scroll lên đầu tab Trong tiếp khách.
- Toast: `Đã xác nhận hoàn thành chuẩn bị.`

### 3.4. `DURING_VISIT -> AFTER_VISIT`

Xảy ra khi Host bấm:

```text
Xác nhận kết thúc tiếp khách
```

Backend validate:

- Actor là `current_host_user_id`.
- Status hiện tại là `DURING_VISIT`.
- Không `CANCELLED` hoặc `CLOSED`.

Kết quả:

```text
visit_request_campuses.status = AFTER_VISIT
updated_by = currentUserId
updated_at = now
```

Frontend sau success:

- Reload detail/permissions/allowedActions.
- `setActiveTab('after')`.
- Scroll lên đầu tab Sau tiếp khách.
- Toast: `Đã xác nhận kết thúc tiếp khách.`

Không khóa tab Trong/Sau nếu nghiệp vụ hiện tại vẫn cần cho phép bổ sung/sửa/xóa.

### 3.5. `AFTER_VISIT -> CLOSED`

Xảy ra khi Host bấm:

```text
Hoàn tất & đóng đoàn
```

Backend validate:

- Actor là `current_host_user_id`.
- Status hiện tại là `AFTER_VISIT`.
- Không `CANCELLED` hoặc `CLOSED`.

Điều kiện đóng đoàn đã chốt:

1. Đã qua `planned_end_at`.
2. Không còn logistics item bắt buộc đang active/chưa hoàn tất.
3. Nếu có handover thì đã ký đủ theo rule BORROW/RETURN.
4. Nếu có minutes thì phải hoàn thành đầy đủ action item bắt buộc.
5. Phải có ít nhất một bài tin tức đã được duyệt liên quan đến chuyến thăm **hoặc** Host xác nhận chuyến này không cần tạo/duyệt bài tin tức.

Nếu chưa đủ điều kiện, backend trả lỗi rõ và không update status.

Kết quả khi đủ điều kiện:

```text
visit_request_campuses.status = CLOSED
closed_at = now nếu có field
closed_by = currentUserId nếu có field
updated_by = currentUserId
updated_at = now
```

Frontend sau success:

- Reload detail/permissions/allowedActions.
- Badge chuyển sang `Đã đóng đoàn`.
- Chỉ còn read-only.
- Không hiện nút mutate.
- Toast: `Đã đóng đoàn thành công.`

---

## 4. Rule hủy đã chốt

### 4.1. Visitor hủy `PENDING_APPROVAL`

Cho phép:

```text
visit_requests.status = PENDING_APPROVAL
→ CANCELLED
```

Điều kiện:

- Actor là Visitor owner của request.
- Lý do hủy bắt buộc.
- Chưa có `visitInstanceId`/process setup.

Sau hủy:

- Không có nút xem quy trình.
- Chỉ xem form yêu cầu + lý do hủy.

### 4.2. Visitor hủy `APPROVED` request

Cho phép nếu:

- Visitor là owner của request.
- Không có campus nào `DURING_VISIT`, `AFTER_VISIT`, `CLOSED`.

Nếu có bất kỳ campus nào đã:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
```

thì chặn.

Thông báo lỗi:

```text
Không thể hủy vì chuyến thăm đã bắt đầu hoặc đã kết thúc tại ít nhất một cơ sở.
```

### 4.3. Host hủy campus instance

Host chỉ được hủy khi:

```text
current_user_id = current_host_user_id
AND campusStatus IN ('ASSIGNED', 'BEFORE_VISIT')
```

Không cho Host hủy ở:

```text
WAITING_HOST_ASSIGNMENT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Thông báo lỗi:

```text
Chỉ có thể hủy lịch thăm trước khi đoàn bắt đầu tiếp khách.
```

### 4.4. Khi campus instance bị `CANCELLED`

Khi campus instance bị `CANCELLED`, toàn bộ logistics item chưa terminal của instance đó phải chuyển sang:

```text
CANCELLED
```

và lưu:

```text
decision_note = “Hủy logistics do campus instance đã hủy.”
```

Nếu có lý do hủy cụ thể từ Host/Visitor, nối rõ:

```text
“Hủy logistics do campus instance đã hủy. Lý do: {cancellationReason}”
```

Không chuyển các logistics item đã terminal:

```text
DONE
REJECTED
DECLINED
CANCELLED
```

---

## 5. Read-only sau khi hủy

### 5.1. `CANCELLED` nhưng đã từng setup

Nếu campus instance đã từng có:

- `visitInstanceId`
- `currentHostUserId` hoặc host
- setup/logistics/agenda/participant/minutes data

thì vẫn cho Host/Staff Leader/HO nội bộ vào xem quy trình ở chế độ read-only.

Yêu cầu:

- Không hiện nút sửa.
- Không hiện nút hủy nữa.
- Không cho gọi API update logistics/agenda/minutes nếu status `CANCELLED`.
- Cho xem lại thông tin đã setup.
- Cho xem lý do hủy.

Route frontend:

```text
/dashboard/visit/process/{visitInstanceId}
```

State đề xuất:

```text
isReadOnly = true
cancelled = true
status = Đã hủy
```

### 5.2. `CANCELLED` trước khi duyệt/chưa setup

Nếu đơn bị hủy trước khi có process:

- Không có `visitInstanceId`.
- Không có `currentHostUserId`.
- Không có setup.

Thì không cho vào `VisitProcess`.

Chỉ cho:

- Xem form yêu cầu.
- Xem lý do hủy.

---

## 6. Logistics status flow đã chốt

### 6.1. Status logistics hợp lệ

Chỉ dùng:

```text
REQUESTED
ASSIGNED
ACCEPTED
IN_PROGRESS
DONE
REJECTED
DECLINED
CANCELLED
CHANGE_PROPOSED
```

Không dùng status cũ:

```text
PLANNED
RECEIVED
READY
COMPLETED
```

### 6.2. Flow chính

Flow logistics mới chốt:

```text
REQUESTED
→ ASSIGNED
→ ACCEPTED
→ IN_PROGRESS
→ DONE
```

Nhánh từ chối/hủy:

```text
REQUESTED -> REJECTED
ASSIGNED  -> DECLINED
REQUESTED/ASSIGNED/ACCEPTED/IN_PROGRESS -> CANCELLED
```

### 6.3. Ý nghĩa từng status

| Status | Ý nghĩa |
|---|---|
| `REQUESTED` | Host gửi yêu cầu logistics qua hệ thống. |
| `ASSIGNED` | Department Leader nhận yêu cầu và gán người xử lý. Nếu Leader tự làm thì `assigned_to_user_id = Leader`. Nếu Leader giao Staff thì `assigned_to_user_id = Staff`. |
| `ACCEPTED` | Người được giao nhiệm vụ đã xác nhận nhận việc. Người này có thể là Department Leader hoặc Department Staff. |
| `IN_PROGRESS` | Người xử lý đã bắt đầu thực hiện nhiệm vụ. |
| `DONE` | Nhiệm vụ logistics hoàn tất. |
| `REJECTED` | Department Leader từ chối yêu cầu logistics từ Host. Lý do lưu vào `decision_note`. |
| `DECLINED` | Người được giao từ chối nhận nhiệm vụ. Lý do lưu vào `assignee_response_note`. |
| `CANCELLED` | Host/campus hủy logistics trước khi tiếp khách hoặc campus bị hủy. Lý do lưu vào `decision_note`. |
| `CHANGE_PROPOSED` | Có đề xuất thay đổi logistics cần phản hồi. |

### 6.4. Không dùng `ACCEPTED` để chỉ Leader accepted request

Không dùng `ACCEPTED` với nghĩa “Department Leader đã chấp nhận request nhưng chưa có người làm”.

Vì flow đã chốt:

```text
Leader nhận/giao luôn -> ASSIGNED
Assignee nhận việc -> ACCEPTED
```

Nếu Leader tự làm:

```text
REQUESTED -> ASSIGNED với assigned_to_user_id = Leader
Leader bấm nhận việc -> ACCEPTED
Leader bấm bắt đầu làm -> IN_PROGRESS
Leader hoàn tất -> DONE
```

Nếu Leader giao Staff:

```text
REQUESTED -> ASSIGNED với assigned_to_user_id = Staff
Staff bấm nhận việc -> ACCEPTED
Staff bấm bắt đầu làm -> IN_PROGRESS
Staff hoàn tất -> DONE
```

---

## 7. allowedActions cần audit/cập nhật

Backend là source of truth cho action.

Nếu hiện tại chưa có, được phép bổ sung allowedActions mới:

```text
START_PREPARATION
COMPLETE_BEFORE_VISIT
COMPLETE_DURING_VISIT
CLOSE_VISIT
VIEW_PROCESS
PROCESS_VISIT
VIEW_CANCEL_REASON
VIEW_REQUEST_FORM
```

Không dùng dynamic permission DB. `allowedActions` phải được tính từ:

- `role_code`
- `sub_role`
- `current_host_user_id`
- `visitor_user_id`
- campus scope
- department scope
- participant/logistics assignment
- record status

### 7.1. `ASSIGNED`

Host hiện tại:

```text
START_PREPARATION
PROCESS_VISIT
CANCEL_BY_HOST
```

Staff Leader:

```text
VIEW_PROCESS
```

### 7.2. `BEFORE_VISIT`

Host hiện tại:

```text
PROCESS_VISIT
COMPLETE_BEFORE_VISIT
CANCEL_BY_HOST
```

Staff Leader:

```text
VIEW_PROCESS
```

### 7.3. `DURING_VISIT`

Host hiện tại:

```text
PROCESS_VISIT
COMPLETE_DURING_VISIT
```

Không có:

```text
CANCEL_BY_HOST
```

### 7.4. `AFTER_VISIT`

Host hiện tại:

```text
PROCESS_VISIT
CLOSE_VISIT
```

Không có:

```text
CANCEL_BY_HOST
```

### 7.5. `CLOSED`

Chỉ xem:

```text
VIEW_PROCESS
```

Không có action mutate.

### 7.6. `CANCELLED`

Nếu đã từng setup/có `visitInstanceId`/host:

```text
VIEW_PROCESS
VIEW_CANCEL_REASON
```

Nếu chưa setup/chưa có `visitInstanceId`:

```text
VIEW_REQUEST_FORM
VIEW_CANCEL_REASON
```

Không có action mutate.

---

## 8. Frontend cần audit/cập nhật

### 8.1. Files chính cần kiểm tra

```text
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitDuringTab.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitAfterTab.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
```

### 8.2. Tận dụng logic cũ nếu đã có

Nếu file đã có:

```text
advanceStage(stage: 'before' | 'during' | 'after')
completeBeforeVisit
completeDuringVisit
completeAfterVisit
renderStageBar
```

thì không viết mới trùng. Hãy audit logic hiện tại và sửa lại theo rule mới.

### 8.3. Fix duplicate CTA trong `VisitProcess`

Trong mỗi tab chỉ giữ một CTA chính.

Không để cùng lúc xuất hiện:

- Nút trong nội dung tab.
- Nút stage bar ngoài nội dung tab.

nếu cả hai cùng chuyển giai đoạn.

Giữ rule:

```text
Tab before: 1 nút COMPLETE_BEFORE_VISIT
Tab during: 1 nút COMPLETE_DURING_VISIT
Tab after: 1 nút CLOSE_VISIT
```

### 8.4. Read-only theo từng tab

Không dùng một biến chung khóa toàn bộ màn khi chưa cần.

Gợi ý:

```ts
const beforeReadOnly =
  isReadOnlyRoute ||
  isCancelledView ||
  instanceStatus === 'DURING_VISIT' ||
  instanceStatus === 'AFTER_VISIT' ||
  instanceStatus === 'CLOSED';

const duringReadOnly =
  isReadOnlyRoute ||
  isCancelledView ||
  instanceStatus === 'CLOSED';

const afterReadOnly =
  isReadOnlyRoute ||
  isCancelledView ||
  instanceStatus === 'CLOSED';
```

Nếu nghiệp vụ vẫn cho chỉnh trong During/After trước khi `CLOSED`, không khóa 2 tab này chỉ vì status đã tiến tới `AFTER_VISIT`.

### 8.5. Scroll lên đầu tab sau khi chuyển

Sau khi chuyển:

```text
BEFORE_VISIT -> DURING_VISIT
DURING_VISIT -> AFTER_VISIT
```

Frontend phải:

- `setActiveTab` đúng tab tiếp theo.
- `scrollIntoView` đầu nội dung tab hoặc `window.scrollTo({ top: 0, behavior: 'smooth' })`.
- Không để user vẫn nằm ở cuối trang sau khi bấm xác nhận.

### 8.6. Message rõ nghĩa trên UI

Tab before:

```text
Sau khi xác nhận, thông tin ở tab Trước tiếp khách sẽ được khóa và quy trình chuyển sang giai đoạn Trong tiếp khách.
```

Tab during:

```text
Sau khi xác nhận, quy trình sẽ chuyển sang giai đoạn Sau tiếp khách.
```

Tab after:

```text
Sau khi đóng đoàn, toàn bộ quy trình sẽ chuyển sang chế độ chỉ xem.
```

---

## 9. Backend endpoints/commands cần audit

Kiểm tra endpoint/command hiện có trước khi tạo mới.

Có thể đã có:

```text
completeBeforeVisit
completeDuringVisit
completeAfterVisit
```

Nếu có, cập nhật validate/status theo rule mới.

Nếu chưa có, thêm theo convention hiện tại:

```http
POST /api/delegations/visit-requests/{visitRequestId}/instances/{visitInstanceId}/start-preparation
POST /api/delegations/visit-requests/{visitRequestId}/instances/{visitInstanceId}/complete-before-visit
POST /api/delegations/visit-requests/{visitRequestId}/instances/{visitInstanceId}/complete-during-visit
POST /api/delegations/visit-requests/{visitRequestId}/instances/{visitInstanceId}/close
```

Controller chỉ gọi MediatR, không nhét business logic trong controller.

Commands gợi ý:

```text
StartPreparationCommand
CompleteBeforeVisitCommand
CompleteDuringVisitCommand
CloseVisitCommand
CancelVisitRequestCommand
CancelVisitRequestCampusCommand
```

---

## 10. Điều kiện đóng đoàn chi tiết

Khi gọi `CloseVisitCommand`, backend phải validate:

1. Instance tồn tại.
2. Current user là `current_host_user_id`.
3. Status hiện tại là `AFTER_VISIT`.
4. Không `CANCELLED` hoặc `CLOSED`.
5. `planned_end_at` đã qua.
6. Không còn logistics item bắt buộc active.
7. Nếu có logistics handover thì đã ký đủ theo BORROW/RETURN rule.
8. Nếu có minutes thì các `minute_action_items` bắt buộc đã hoàn thành.
9. Có ít nhất một news bài tin tức đã được duyệt liên quan đến visit instance **hoặc** có cờ/xác nhận “Không cần bài tin tức cho chuyến thăm này”.

Nếu hiện chưa có field xác nhận “Không cần bài tin tức”, cần xử lý theo thứ tự:

- Ưu tiên dùng field hiện có nếu đã có.
- Nếu chưa có, báo lại cần SQL patch trước khi code.
- Không tự bịa field trong backend/frontend nếu DB chưa có.

Không cho đóng đoàn nếu thiếu điều kiện mà vẫn update status.

---

## 11. Logistics khi campus bị hủy

Khi `CancelVisitRequestCampusCommand` thành công:

```text
visit_request_campuses.status = CANCELLED
```

Trong cùng transaction:

- Tìm tất cả `visit_logistics_items` của `visitInstanceId`.
- Với item chưa terminal thì set `status = CANCELLED`.
- Lưu `decision_note` phù hợp.
- Không đụng item terminal:

```text
DONE
REJECTED
DECLINED
CANCELLED
```

Nếu có email/token logistics chưa dùng:

- Token bấm sau khi item đã `CANCELLED` phải trả `ALREADY_RESPONDED` hoặc `INVALID_STATE`.
- Không cho token update lại item đã cancelled.

---

## 12. Email action token liên quan logistics

Nếu logistics phản hồi qua email:

- Token chỉ dùng một lần.
- Token hết hạn không xử lý.
- Token đã dùng không xử lý lại.
- Nếu action cần lý do mà chưa nhập lý do thì không consume token.

Mapping logistics email:

```text
Leader reject request:
REQUESTED -> REJECTED
reason -> decision_note

Assignee decline:
ASSIGNED -> DECLINED
reason -> assignee_response_note

Assignee accept:
ASSIGNED -> ACCEPTED

Start work:
ACCEPTED -> IN_PROGRESS

Complete:
IN_PROGRESS -> DONE
```

---

## 13. UI danh sách `VisitRequestManagement`

File:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
```

Label Staff:

| Status/Case | Label |
|---|---|
| `ASSIGNED` | Đã được phân công |
| `BEFORE_VISIT` | Đang chuẩn bị |
| `DURING_VISIT` | Đang tiếp khách |
| `AFTER_VISIT` | Chờ đóng đoàn |
| `CLOSED` | Đã đóng đoàn |
| `CANCELLED` by Visitor | Đã hủy bởi khách |
| `CANCELLED` by Host | Đã hủy bởi Host |

Action:

- `CANCEL` chỉ hiện ở `ASSIGNED`/`BEFORE_VISIT` cho Host.
- `DURING_VISIT`/`AFTER_VISIT`/`CLOSED` không hiện cancel.
- `CANCELLED` đã setup thì hiện “Xem quy trình đã hủy”.
- `CANCELLED` chưa setup thì chỉ xem form + lý do hủy.
- `REJECTED` chỉ xem form + lý do từ chối.

---

## 14. Không được sửa sai phạm vi

Không được:

- Không dùng dynamic permissions table.
- Không auto-migrate nếu chưa có SQL patch được yêu cầu.
- Không tự thêm field DB nếu chưa có patch.
- Không dùng mock data.
- Không dùng status cũ `PLANNED`, `RECEIVED`, `READY`, `COMPLETED` nếu schema/code mới đã bỏ.
- Không để GET/list/detail tự đổi trạng thái.
- Không khóa cả 3 tab khi chỉ cần khóa tab Trước tiếp khách.
- Không để 2 nút cùng tác dụng xuất hiện trên cùng tab.
- Không để frontend tự đổi status giả khi API fail.
- Không dùng `any`, `ts-ignore`, `ts-expect-error`.
- Không refactor sâu làm đổi flow ngoài phạm vi.

---

## 15. Test bắt buộc

### 15.1. Backend

```bash
dotnet build
```

### 15.2. Frontend

```bash
cd frontend/pems-react
npm run lint
npm run build
```

### 15.3. Manual/API test

#### Request/Cancel

1. Visitor hủy `PENDING_APPROVAL` → request `CANCELLED`, không có `VisitProcess`.
2. Visitor hủy `APPROVED` khi chưa campus nào `DURING/AFTER/CLOSED` → thành công.
3. Visitor hủy `APPROVED` khi có campus `DURING_VISIT` → bị chặn.
4. Host hủy `ASSIGNED` → `CANCELLED`.
5. Host hủy `BEFORE_VISIT` → `CANCELLED`.
6. Host hủy `DURING_VISIT` → bị chặn.
7. Host hủy `AFTER_VISIT` → bị chặn.
8. Host hủy `CLOSED` → bị chặn.

#### Campus lifecycle

9. `ASSIGNED` không tự chuyển khi Host mở trang.
10. Host bấm “Bắt đầu chuẩn bị” → `ASSIGNED -> BEFORE_VISIT`.
11. Host bấm “Xác nhận hoàn thành chuẩn bị” → `BEFORE_VISIT -> DURING_VISIT`.
12. Sau khi chuyển `DURING_VISIT`, tab Trước tiếp khách read-only.
13. Sau khi chuyển `DURING_VISIT`, tự chuyển sang tab Trong tiếp khách và scroll lên đầu.
14. Tab Trong tiếp khách vẫn có thể thao tác theo quyền.
15. Host bấm “Xác nhận kết thúc tiếp khách” → `DURING_VISIT -> AFTER_VISIT`.
16. Sau khi chuyển `AFTER_VISIT`, tự chuyển sang tab Sau tiếp khách và scroll lên đầu.
17. Tab Sau tiếp khách vẫn có thể thao tác theo quyền.
18. Tab Sau tiếp khách chỉ có một nút “Hoàn tất & đóng đoàn”, không có nút trùng.
19. `CloseVisit` bị chặn nếu thiếu điều kiện.
20. `CloseVisit` thành công khi đủ điều kiện → `CLOSED`, read-only.

#### Logistics

21. Khi campus `CANCELLED`, logistics chưa terminal → `CANCELLED + decision_note`.
22. Logistics terminal `DONE/REJECTED/DECLINED/CANCELLED` không bị đổi lại.
23. Logistics flow `REQUESTED -> ASSIGNED -> ACCEPTED -> IN_PROGRESS -> DONE` hoạt động đúng.
24. Leader tự làm logistics: `assigned_to_user_id = Leader`, sau đó Leader nhận việc → `ACCEPTED`.
25. Leader giao Staff: `assigned_to_user_id = Staff`, Staff nhận việc → `ACCEPTED`.
26. Assignee decline → `DECLINED + assignee_response_note`.
27. Leader reject request → `REJECTED + decision_note`.

---

## 16. Báo cáo Agent cần trả về sau khi audit/sửa

Agent phải báo cáo theo format:

```text
1. Root cause/gap:
2. Files đã kiểm tra:
3. Files đã sửa:
4. Request status logic đã đúng chưa:
5. Campus instance lifecycle đã đúng chưa:
6. Visitor cancel PENDING_APPROVAL đã xử lý thế nào:
7. BEFORE_VISIT -> DURING_VISIT thủ công đã xử lý thế nào:
8. DURING_VISIT -> AFTER_VISIT thủ công đã xử lý thế nào:
9. Fix duplicate button trong VisitProcess:
10. Rule khóa tab Trước tiếp khách sau khi chuyển:
11. Rule During/After vẫn editable:
12. CloseVisit validation:
13. Logistics flow đã cập nhật:
14. Logistics cancel theo campus cancel:
15. allowedActions đã cập nhật:
16. Có đổi SQL/schema không:
17. Build/lint đã chạy:
18. Manual test checklist:
19. Rủi ro/cần quyết định thêm:
```

---

## 17. Tóm tắt ngắn cho Developer

Các điểm quan trọng nhất cần nhớ:

1. Visitor được hủy `PENDING_APPROVAL`; nếu chưa có process thì chỉ xem form + lý do hủy.
2. Host chỉ hủy được trước khi tiếp khách: `ASSIGNED` hoặc `BEFORE_VISIT`.
3. Không tự động chuyển trạng thái theo thời gian.
4. `BEFORE_VISIT -> DURING_VISIT` là Host bấm xác nhận hoàn thành chuẩn bị.
5. `DURING_VISIT -> AFTER_VISIT` là Host bấm xác nhận kết thúc tiếp khách.
6. Chỉ tab Trước tiếp khách bị khóa sau khi đã chuyển sang Trong tiếp khách.
7. During/After vẫn thao tác được cho đến khi `CLOSED`, trừ các phần nghiệp vụ riêng có rule khác.
8. Mỗi tab chỉ có một CTA chuyển giai đoạn, không để nút trùng.
9. Đóng đoàn phải validate điều kiện bắt buộc.
10. Logistics flow chốt là `REQUESTED -> ASSIGNED -> ACCEPTED -> IN_PROGRESS -> DONE`.
11. `ACCEPTED` logistics nghĩa là người được giao nhận việc, không phải Leader accepted request.
12. Khi campus bị hủy, logistics chưa terminal phải tự chuyển `CANCELLED`.
