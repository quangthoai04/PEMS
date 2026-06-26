# PROMPT CODE — Gộp Tab “Phân công” + “Theo dõi tiến độ” cho Department Leader

## 0. Bối cảnh

Tôi đang code module **Department Leader — Nhiệm vụ tiếp khách** trong PEMS.

Hiện UI đã có các tab:

```text
- Bảng lịch
- Phân công
- Theo dõi tiến độ đoàn khách
```

Tôi muốn **gộp tab “Phân công” và “Theo dõi tiến độ đoàn khách”** vì hai tab này đang trùng nhiều về dữ liệu và trạng thái.

Tên tab mới:

```text
Phân công và tiến độ
```

UI hiện tại đã làm khá đẹp. Nhiệm vụ của bạn là **sửa logic, API, data mapping, filter, status, action**, không thiết kế lại UI.

---

## 1. Yêu cầu bắt buộc

```text
KHÔNG rewrite UI.
KHÔNG phá layout hiện tại.
KHÔNG đổi màu sắc/className/style nếu không cần.
KHÔNG xóa modal/detail/action đang dùng.
KHÔNG tạo mock data.
KHÔNG hard-code data.
KHÔNG tạo file rác.
KHÔNG tạo bảng mới.
KHÔNG sửa schema nữa.
KHÔNG ảnh hưởng các chức năng khác.
KHÔNG query permissions / role_permissions.
Dữ liệu phải lấy thật từ database.
Code clean, dễ tìm, đúng feature folder hiện tại.
Backend theo Clean Architecture: Controller chỉ gọi MediatR.
Backend build pass.
Frontend build pass.
```

Base DB hiện tại đã có bảng mới:

```text
visit_logistics_assignment_attempts
```

Bảng này dùng cho lịch sử từng lần phân công đơn yêu cầu logistics cho nhân sự phòng ban.

---

## 2. Actor và scope

User hiện tại là:

```text
role_code = DEPARTMENT
sub_role = LEADER
```

Department Leader chỉ thấy và xử lý dữ liệu thuộc phòng ban của mình:

```text
currentUser.department_id
```

Scope:

```text
Đơn yêu cầu logistics:
visit_logistics_items.requested_to_department_id = currentUser.department_id

Thư mời:
visit_participants.user_id thuộc users.department_id = currentUser.department_id
participant_role = DEPT_SUPPORT
```

Nhân sự được phân công phải thuộc cùng phòng ban:

```text
users.department_id = currentUser.department_id
role_code = DEPARTMENT
users.status = ACTIVE
```

---

## 3. Gộp tab UI

Hiện có:

```text
Tab Phân công
Tab Theo dõi tiến độ đoàn khách
```

Hãy gộp thành một tab:

```text
Phân công và tiến độ
```

Cách làm:

```text
- Dùng lại UI/table/card/filter hiện tại.
- Không redesign toàn bộ.
- Nếu code đang tách component Phân công và Theo dõi, hãy tái sử dụng component tốt nhất.
- Không xóa logic nếu đang được route khác dùng; nếu cần thì deprecate nhẹ hoặc route tab cũ về component mới.
- Bảng mới hiển thị cả THƯ MỜI và ĐƠN YÊU CẦU.
```

Column gợi ý giữ gần UI hiện tại:

```text
- Đoàn khách
- Loại: Thư mời / Đơn yêu cầu
- Nhiệm vụ / Nội dung
- Người phụ trách
- Trạng thái
- Ngày/Thời gian
- Hành động
```

Trong nút hành động vẫn phải có:

```text
Xem chi tiết đoàn khách
```

---

## 4. Logic status mới: KHÔNG dùng PLANNED / REQUESTED ở màn này

Trong enum DB của `visit_logistics_items.status` vẫn có:

```text
PLANNED
REQUESTED
```

Nhưng trong flow Department Leader hiện tại **không dùng 2 trạng thái này**.

Vì đơn bên kia gửi sẽ đến trực tiếp phòng ban, nên khi item xuất hiện cho Department Leader, trạng thái khởi đầu là:

```text
RECEIVED
```

Không tạo mới item cho màn này với:

```text
PLANNED
REQUESTED
```

---

## 5. Flow đơn yêu cầu logistics

Bảng chính:

```text
visit_logistics_items
visit_logistics_assignment_attempts
visit_logistics_item_handovers
```

### 5.1. Trạng thái mặc định khi đơn gửi tới phòng ban

Khi bên kia gửi đơn yêu cầu tới phòng ban:

```text
visit_logistics_items.status = RECEIVED
assigned_to_user_id = NULL
requested_to_department_id = current department
requested_at = NOW()
```

UI label:

```text
Chưa phân công
```

Mặc định người đang nhìn và chịu trách nhiệm xử lý bước đầu là Department Leader, nhưng DB chưa set `assigned_to_user_id` cho leader nếu leader chưa bấm tự nhận.

### 5.2. Department Leader từ chối đơn

```text
RECEIVED -> REJECTED
```

Rule:

```text
- Chỉ Department Leader của phòng ban đó được từ chối.
- Bắt buộc nhập lý do.
- Đây là từ chối toàn bộ đơn yêu cầu.
```

Update:

```text
status = REJECTED
decision_note = reason
updated_by = currentUser.userId
updated_at = NOW()
```

Toast:

```text
Đã từ chối đơn yêu cầu.
```

### 5.3. Department Leader tự nhận làm

```text
RECEIVED -> ACCEPTED
```

Update `visit_logistics_items`:

```text
status = ACCEPTED
assigned_to_user_id = currentUser.userId
assigned_by = currentUser.userId
assigned_at = NOW()
assignee_accepted_at = NOW()
updated_by = currentUser.userId
updated_at = NOW()
```

Insert `visit_logistics_assignment_attempts` để lưu lịch sử tự nhận:

```text
logistics_item_id = item id
assignee_user_id = currentUser.userId
assigned_by = currentUser.userId
assigned_at = NOW()
status = ACCEPTED
responded_at = NOW()
response_source = PORTAL
```

Rule:

```text
Nếu leader tự nhận thì leader có quyền xử lý tiếp: đề xuất thay đổi, ký biên bản nếu đúng vai trò, cập nhật tiến độ.
```

### 5.4. Department Leader giao cho nhân sự

```text
RECEIVED -> ASSIGNED
```

Update `visit_logistics_items`:

```text
status = ASSIGNED
assigned_to_user_id = selectedStaffUserId
assigned_by = currentUser.userId
assigned_at = NOW()
updated_by = currentUser.userId
updated_at = NOW()
```

Insert `visit_logistics_assignment_attempts`:

```text
logistics_item_id = item id
assignee_user_id = selectedStaffUserId
assigned_by = currentUser.userId
assigned_at = NOW()
status = PENDING
```

Sau khi giao cho người khác:

```text
- Department Leader chỉ xem được đơn đó.
- Department Leader không được xử lý thay nếu người khác đang được giao.
- Không hiện dòng/nút "Đổi người phụ trách" nữa.
- Chỉ hiện lại nút phân công nếu nhân sự từ chối.
```

### 5.5. Nhân sự chấp nhận làm

```text
ASSIGNED -> ACCEPTED
```

Update latest assignment attempt:

```text
status = ACCEPTED
responded_at = NOW()
response_source = PORTAL hoặc EMAIL_TOKEN
```

Update item:

```text
status = ACCEPTED
assignee_accepted_at = NOW()
updated_by = currentUser.userId
updated_at = NOW()
```

Toast:

```text
Đã chấp nhận nhiệm vụ.
```

### 5.6. Nhân sự từ chối làm

```text
ASSIGNED -> RECEIVED
```

Rule quan trọng:

```text
- Nhân sự chỉ được từ chối trước 24 giờ so với thời điểm đoàn diễn ra.
- Từ chối bắt buộc nhập lý do.
- Nhân sự từ chối KHÔNG làm item thành REJECTED.
- REJECTED chỉ dùng khi từ chối toàn bộ đơn yêu cầu.
```

Check thời gian:

```text
NOW() <= visit_request_campuses.planned_start_at - 24 giờ
```

Nếu quá hạn:

```text
Trả 409 hoặc 400:
“Bạn chỉ có thể từ chối nhiệm vụ trước thời điểm đoàn diễn ra ít nhất 24 giờ.”
```

Update latest assignment attempt:

```text
status = DECLINED
responded_at = NOW()
response_note = reason
response_source = PORTAL hoặc EMAIL_TOKEN
```

Update item:

```text
status = RECEIVED
assigned_to_user_id = NULL
assigned_by = NULL
assigned_at = NULL
assignee_response_note = reason
updated_by = currentUser.userId
updated_at = NOW()
```

Sau đó:

```text
- Department Leader nhận notification.
- Nút phân công hiện lại.
- Leader được phân công cho người khác.
```

Notification:

```text
Nhân sự {fullName} đã từ chối nhiệm vụ "{title}". Vui lòng phân công người khác.
```

---

## 6. Flow đề xuất thay đổi của đơn yêu cầu

Đối với đơn yêu cầu trước khi Department Leader hoặc nhân sự chấp nhận, nếu phòng ban muốn đề xuất thay đổi:

```text
RECEIVED hoặc ASSIGNED -> CHANGE_PROPOSED
```

Hoặc nếu người đang phụ trách đã ACCEPTED nhưng cần thương lượng lại thì vẫn cho theo rule hiện có nếu business cho phép.

Update:

```text
status = CHANGE_PROPOSED
proposed_by = currentUser.userId
proposed_at = NOW()
proposed_description = nội dung đề xuất
proposed_usage_start_at / proposed_usage_end_at nếu có
proposed_quantity nếu có
proposal_response = NULL
```

Khi bên kia phản hồi:

### Bên kia từ chối đề xuất

```text
CHANGE_PROPOSED -> REJECTED
```

Update:

```text
proposal_response = REJECTED
proposal_responded_by = responderUserId
proposal_responded_at = NOW()
proposal_response_note = note
status = REJECTED
decision_note = note
```

### Bên kia đồng ý đề xuất

```text
CHANGE_PROPOSED -> ACCEPTED
```

Rule:

```text
- Nếu người đề xuất là leader và leader đang tự nhận làm, status = ACCEPTED.
- Nếu item đã có assignee trước đó và assignee vẫn hợp lệ, có thể quay lại ACCEPTED hoặc ASSIGNED tùy trạng thái trước đó.
- Ưu tiên giữ logic đơn giản: proposal accepted thì người đang phụ trách được quyền tiếp tục xử lý và status = ACCEPTED.
```

Notification:

```text
Bên yêu cầu đã đồng ý đề xuất thay đổi cho "{title}".
```

---

## 7. Flow bàn giao / nghiệm thu của đơn yêu cầu

Bảng ký:

```text
visit_logistics_item_handovers
```

Không lưu chữ ký trong `visit_logistics_items`.

### Bàn giao

Dùng row:

```text
handover_type = BORROW
```

Cần 2 chữ ký kèm note:

```text
borrower_signed_by / borrower_signed_at = bên mượn ký nhận
provider_signed_by / provider_signed_at = bên cho mượn ký bàn giao
condition_note hoặc note phù hợp = ghi chú bàn giao
```

Khi cả 2 bên ký bàn giao xong:

```text
status = IN_PROGRESS
```

### Nghiệm thu / trả nhận

Dùng row:

```text
handover_type = RETURN
```

Cần 2 chữ ký kèm note/feedback:

```text
borrower_signed_by / borrower_signed_at = bên mượn ký trả
provider_signed_by / provider_signed_at = bên cho mượn ký nhận lại
condition_note hoặc note phù hợp = feedback/ghi chú nghiệm thu
```

Khi cả 2 bên ký nghiệm thu xong:

```text
status = DONE
completed_at = NOW()
```

---

## 8. Flow thư mời

Thư mời hiện dùng:

```text
visit_participants
participant_role = DEPT_SUPPORT
```

Với màn Department Leader, cần map thành item chung trong tab “Phân công và tiến độ”.

### 8.1. Thư mời mới gửi tới Department Leader

Nếu thư mời gửi tới leader:

```text
visit_participants.user_id = department.head_user_id
visit_participants.status = INVITED
participant_role = DEPT_SUPPORT
```

UI mapping:

```text
INVITED của leader -> RECEIVED / Chưa phân công
```

Mặc định người phụ trách hiển thị:

```text
Department Leader
```

Nhưng nếu leader chưa bấm nhận, UI status vẫn là:

```text
Chưa phân công
```

### 8.2. Department Leader từ chối thư mời

```text
INVITED -> DECLINED
```

UI mapping:

```text
DECLINED -> REJECTED / Từ chối
```

Rule:

```text
- Bắt buộc nhập lý do.
- Lưu lý do vào visit_participants.note.
- Set responded_at = NOW().
```

### 8.3. Department Leader tự nhận thư mời

```text
INVITED -> ACCEPTED
```

Update:

```text
visit_participants.status = ACCEPTED
responded_at = NOW()
```

UI mapping:

```text
ACCEPTED -> Đã chấp nhận
```

Leader có quyền xem và xử lý phần thư mời liên quan.

### 8.4. Department Leader giao thư mời cho nhân sự

Vì DB hiện không có bảng assignment_attempt riêng cho thư mời, dùng `visit_participants` để lưu lịch sử.

Khi giao cho nhân sự:

```text
- Không xóa row của leader.
- Tạo row visit_participants mới cho nhân sự được giao.
```

Insert:

```text
visit_instance_id = same instance
user_id = selectedStaffUserId
participant_role = DEPT_SUPPORT
is_host = 0
status = INVITED
invited_by = currentUser.userId
invited_at = NOW()
assigned_by = currentUser.userId
assigned_at = NOW()
note = NULL
```

UI mapping:

```text
Nhân sự row status INVITED + assigned_by not null -> ASSIGNED / Đã giao
```

Sau khi giao cho nhân sự:

```text
- Leader chỉ xem.
- Không hiện “Đổi người phụ trách”.
- Chỉ hiện lại phân công nếu nhân sự từ chối.
```

### 8.5. Nhân sự chấp nhận thư mời

```text
INVITED -> ACCEPTED
```

Update:

```text
status = ACCEPTED
responded_at = NOW()
```

### 8.6. Nhân sự từ chối thư mời

```text
INVITED -> DECLINED
```

Rule:

```text
- Chỉ được từ chối trước 24 giờ so với planned_start_at.
- Bắt buộc nhập lý do.
```

Update:

```text
status = DECLINED
responded_at = NOW()
note = reason
```

Sau đó:

```text
- Leader nhận notification.
- UI item thư mời quay về “Chưa phân công”.
- Nút phân công hiện lại cho leader.
```

### 8.7. IN_PROGRESS và DONE cho thư mời

Không cần lưu `IN_PROGRESS` / `DONE` vào `visit_participants` vì enum không có.

Hãy derive trạng thái hiển thị từ thời gian hoặc `visit_request_campuses.status`.

Mapping hiển thị:

```text
Nếu thư mời đã ACCEPTED và NOW nằm trong planned_start_at -> planned_end_at:
UI status = IN_PROGRESS / Trong tiến trình

Nếu thư mời đã ACCEPTED và NOW > planned_end_at:
UI status = DONE / Hoàn thành
```

Hoặc dùng campus instance status nếu đã có:

```text
DURING_VISIT -> IN_PROGRESS
AFTER_VISIT / CLOSED -> DONE
CANCELLED -> CANCELLED
```

Không sửa enum `visit_participants`.

---

## 9. Quyền xử lý sau khi giao người khác

Rule chung cho cả thư mời và đơn yêu cầu:

```text
Nếu leader giao cho người khác:
- Leader chỉ được xem chi tiết.
- Leader không được bấm action xử lý thay.
- Không hiện “Đổi người phụ trách” khi người được giao đang chờ phản hồi hoặc đã accepted.
- Chỉ hiện lại nút phân công nếu người được giao từ chối.
```

Nếu leader tự nhận:

```text
- Leader là người phụ trách.
- Leader được quyền xử lý tiếp theo trạng thái.
```

Nếu nhân sự được giao:

```text
- Chỉ nhân sự đó được accept/decline và xử lý phần được giao.
```

---

## 10. Filter, search, sort

Trong tab “Phân công và tiến độ”, cần filter/search/sort thật từ API.

### Search

Search được cả:

```text
- Tên đoàn khách
- Mã request
- Tên đối tác / đơn vị khách
- Tên nhiệm vụ
- Nội dung nhiệm vụ
```

### Filter loại

```text
Tất cả
Thư mời
Đơn yêu cầu
```

### Filter trạng thái

```text
Tất cả trạng thái
Chưa phân công
Đã giao
Chấp nhận
Từ chối
Đang đề xuất
Trong tiến trình
Hoàn thành
Đã hủy
```

Mapping:

```text
Chưa phân công = RECEIVED hoặc invitation leader INVITED, chưa có assignee active
Đã giao = ASSIGNED hoặc invitation staff INVITED đang chờ phản hồi
Chấp nhận = ACCEPTED
Từ chối = REJECTED hoặc invitation DECLINED
Đang đề xuất = CHANGE_PROPOSED
Trong tiến trình = IN_PROGRESS hoặc invitation đang trong thời gian diễn ra
Hoàn thành = DONE hoặc invitation đã hết thời gian
Đã hủy = CANCELLED hoặc visit instance CANCELLED
```

### Filter phạm vi

```text
Tất cả
Tôi
Văn phòng
```

Ý nghĩa:

```text
Tôi:
- assigned_to_user_id = currentUser.userId
- hoặc visit_participants.user_id = currentUser.userId
- hoặc leader tự nhận

Văn phòng:
- tất cả item thuộc currentUser.department_id
```

### Filter ngày

Dựa trên:

```text
COALESCE(usage_start_at, planned_start_at)
COALESCE(usage_end_at, planned_end_at)
```

### Sort

Sort theo ngày:

```text
Mặc định: ngày gần nhất lên trước hoặc theo existing UI.
Nên hỗ trợ sort ASC/DESC nếu UI đã có.
```

---

## 11. Thanh nổi bật màu cam cho đơn yêu cầu đang làm

Cần thêm / tận dụng một khu vực ở trên bảng:

```text
Đơn yêu cầu đang làm / Cần chú ý
```

Thiết kế:

```text
- Không redesign toàn page.
- Dùng màu cam nhẹ theo style hiện tại.
- Đặt phía trên bảng trong tab “Phân công và tiến độ”.
- Chỉ hiện khi có item cần chú ý.
```

Item nên xuất hiện ở thanh cam khi:

```text
1. Đơn yêu cầu đang assigned/accepted/in_progress/ready thuộc currentUser.
2. Đề xuất của mình được bên kia đồng ý.
3. Đã ký bàn giao đủ 2 bên, status IN_PROGRESS, đang chờ nghiệm thu.
4. Đến ngày/giờ bàn giao.
5. Đến ngày/giờ nghiệm thu.
6. Sắp đến hạn xử lý.
```

Mục tiêu:

```text
Leader hoặc nhân sự không bị miss đơn đang làm.
Ví dụ: đã ký bàn giao xong nhưng lúc nghiệm thu lại không tìm thấy đơn.
```

CTA trong thanh cam:

```text
- Xem chi tiết
- Ký bàn giao nếu đến bước bàn giao
- Ký nghiệm thu nếu đến bước nghiệm thu
```

Chỉ hiện action nếu current user có quyền xử lý.

---

## 12. Chuông thông báo / notifications

Map các event sau vào notification bell:

### Khi có thư mời mới gửi tới phòng ban

Tạo notification cho Department Leader:

```text
type/code: DEPARTMENT_INVITATION_RECEIVED
message: Bạn có thư mời tham gia đoàn "{delegationName}".
target_type = VISIT_PARTICIPANT hoặc VISIT_INSTANCE
target_id = participant_id hoặc visit_instance_id
```

### Khi có đơn yêu cầu mới gửi tới phòng ban

Tạo notification cho Department Leader:

```text
type/code: LOGISTICS_REQUEST_RECEIVED
message: Phòng ban nhận được đơn yêu cầu "{title}" từ đoàn "{delegationName}".
target_type = LOGISTICS_ITEM
target_id = logistics_item_id
```

### Khi bên kia đồng ý đề xuất

Tạo notification cho người phụ trách hiện tại và Department Leader:

```text
type/code: LOGISTICS_PROPOSAL_ACCEPTED
message: Đề xuất thay đổi cho "{title}" đã được chấp nhận.
target_type = LOGISTICS_ITEM
target_id = logistics_item_id
```

### Khi bên kia từ chối đề xuất

```text
type/code: LOGISTICS_PROPOSAL_REJECTED
message: Đề xuất thay đổi cho "{title}" đã bị từ chối.
target_type = LOGISTICS_ITEM
target_id = logistics_item_id
```

### Trước giờ đón đoàn 15 phút

Cần thông báo:

```text
type/code: VISIT_STARTING_SOON
message: Đoàn "{delegationName}" sẽ bắt đầu sau 15 phút.
```

Người nhận:

```text
- Department Leader nếu còn item chưa phân công/chưa hoàn tất.
- Người phụ trách đã accepted.
```

Nếu project chưa có background job/scheduler:

```text
- Không tự thêm công nghệ mới.
- Dùng cơ chế hiện có nếu có.
- Nếu chưa có scheduler, implement theo cách tối thiểu:
  - Khi load notification hoặc calendar, backend kiểm tra các visit sắp diễn ra trong 15 phút.
  - Tạo notification idempotent nếu chưa có notification cùng type + target + recipient trong ngày.
```

### Khi nhân sự từ chối nhiệm vụ được giao

Tạo notification cho Department Leader:

```text
type/code: ASSIGNMENT_DECLINED
message: Nhân sự "{staffName}" đã từ chối nhiệm vụ "{title}". Vui lòng phân công người khác.
target_type = LOGISTICS_ITEM hoặc VISIT_PARTICIPANT
target_id = item id
```

---

## 13. API gợi ý

Không bắt buộc đặt y hệt nếu project đã có convention khác, nhưng phải clean và dễ tìm.

### Query unified list

```text
GET /api/department/reception-tasks/assignments-progress
```

Query params:

```ts
{
  search?: string;
  itemType?: 'ALL' | 'INVITATION' | 'REQUEST';
  status?: 'ALL' | 'RECEIVED' | 'ASSIGNED' | 'ACCEPTED' | 'REJECTED' | 'CHANGE_PROPOSED' | 'IN_PROGRESS' | 'DONE' | 'CANCELLED';
  ownerScope?: 'ALL' | 'ME' | 'DEPARTMENT';
  fromDate?: string;
  toDate?: string;
  sortBy?: 'date';
  sortDirection?: 'ASC' | 'DESC';
  page?: number;
  pageSize?: number;
}
```

Response item:

```ts
{
  itemType: 'INVITATION' | 'REQUEST';
  itemId: number;
  visitRequestId: number;
  visitInstanceId: number;
  logisticsItemId?: number;
  participantId?: number;

  delegationName: string;
  requestCode: string;
  organizationName?: string;

  title: string;
  description?: string;

  currentResponsibleUserId?: number;
  currentResponsibleName?: string;
  isCurrentUserResponsible: boolean;
  isLeaderSelfAccepted: boolean;

  rawStatus: string;
  uiStatus: string;
  statusLabel: string;

  startAt: string;
  endAt: string;

  canViewDetail: boolean;
  canViewDelegationDetail: boolean;
  canAssign: boolean;
  canAccept: boolean;
  canDecline: boolean;
  canRejectRequest: boolean;
  canProposeChange: boolean;
  canSignBorrow: boolean;
  canSignReturn: boolean;

  latestDeclineReason?: string;
  latestAssignmentAttemptStatus?: string;

  needsAttention: boolean;
  attentionReason?: string;
}
```

### Attention bar

```text
GET /api/department/reception-tasks/attention-items
```

Trả về danh sách đơn yêu cầu đang làm/cần chú ý cho thanh cam.

### Detail

```text
GET /api/department/reception-tasks/items/{itemType}/{itemId}
```

Hoặc dùng endpoint detail hiện có, miễn là trả đủ:

```text
- Thông tin đoàn
- Thông tin nhiệm vụ
- Người phụ trách
- Trạng thái
- Lịch sử phân công
- Lý do từ chối gần nhất
- Biên bản bàn giao/nghiệm thu
- Permission flags cho button
```

### Actions logistics request

```text
POST /api/department/reception-tasks/requests/{logisticsItemId}/accept-self
POST /api/department/reception-tasks/requests/{logisticsItemId}/reject
POST /api/department/reception-tasks/requests/{logisticsItemId}/assign
POST /api/department/reception-tasks/requests/{logisticsItemId}/accept-assignment
POST /api/department/reception-tasks/requests/{logisticsItemId}/decline-assignment
POST /api/department/reception-tasks/requests/{logisticsItemId}/propose-change
POST /api/department/reception-tasks/requests/{logisticsItemId}/sign-borrow
POST /api/department/reception-tasks/requests/{logisticsItemId}/sign-return
```

### Actions invitation

```text
POST /api/department/reception-tasks/invitations/{participantId}/accept-self
POST /api/department/reception-tasks/invitations/{participantId}/reject
POST /api/department/reception-tasks/invitations/{participantId}/assign
POST /api/department/reception-tasks/invitations/{participantId}/accept-assignment
POST /api/department/reception-tasks/invitations/{participantId}/decline-assignment
```

---

## 14. Backend structure gợi ý

Không tạo file lung tung. Đặt đúng feature hiện có.

Gợi ý nếu chưa có module rõ:

```text
PEMS.Application/DepartmentReceptionTasks/
```

Queries:

```text
GetAssignmentsProgressList
GetAttentionItems
GetUnifiedTaskDetail
GetDepartmentAssigneeCandidates
```

Commands:

```text
AcceptLogisticsRequestSelf
RejectLogisticsRequest
AssignLogisticsRequest
AcceptLogisticsAssignment
DeclineLogisticsAssignment
ProposeLogisticsChange
SignLogisticsBorrow
SignLogisticsReturn

AcceptInvitationSelf
RejectInvitation
AssignInvitation
AcceptInvitationAssignment
DeclineInvitationAssignment
```

Domain/entity nếu chưa có cho base mới:

```text
VisitLogisticsAssignmentAttempt
```

DbContext:

```text
DbSet<VisitLogisticsAssignmentAttempt>
```

EF config:

```text
VisitLogisticsAssignmentAttemptConfiguration
```

Không dùng migration tự động. DB base đã có bảng.

---

## 15. Frontend cần sửa

Chỉ sửa theo logic, không đổi UI đẹp hiện tại.

Cần sửa:

```text
- tab config: gộp Phân công + Theo dõi tiến độ thành Phân công và tiến độ
- API service
- hooks
- DTO types
- status mapping
- filter/search/sort params
- action handlers
- permission flags cho button
- attention bar màu cam
- notification mapping nếu frontend cần link target
```

Không reload full page sau action.

Sau action thành công:

```text
- refetch list
- refetch attention bar
- refetch detail modal nếu đang mở
- refetch notification count nếu liên quan
- hiển thị toast
```

---

## 16. Checklist nghiệm thu

```text
[ ] Tab mới tên “Phân công và tiến độ”.
[ ] Không còn data mock.
[ ] List hiển thị cả thư mời và đơn yêu cầu.
[ ] Search được đoàn khách và nhiệm vụ.
[ ] Filter được loại: thư mời / đơn yêu cầu.
[ ] Filter được trạng thái: từ chối, chấp nhận, chưa phân công, đã giao, đang đề xuất, trong tiến trình, hoàn thành, hủy.
[ ] Filter được phạm vi: tôi / văn phòng.
[ ] Filter ngày hoạt động đúng.
[ ] Sort theo ngày đúng.
[ ] Đơn gửi tới phòng ban mặc định status RECEIVED / Chưa phân công.
[ ] Department Leader có thể từ chối kèm lý do.
[ ] Department Leader có thể tự nhận làm.
[ ] Department Leader có thể giao cho nhân sự.
[ ] Sau khi giao cho nhân sự, leader chỉ xem, không xử lý thay.
[ ] Nút đổi/phân công không hiện khi người được giao đang chờ hoặc đã accepted.
[ ] Nhân sự từ chối trước 24h thì item quay về RECEIVED và leader phân công lại được.
[ ] Nhân sự không thể từ chối nếu còn dưới 24h trước giờ đoàn.
[ ] Đề xuất thay đổi sang CHANGE_PROPOSED.
[ ] Bên kia từ chối đề xuất thì item REJECTED.
[ ] Bên kia đồng ý đề xuất thì item ACCEPTED và notification hiện.
[ ] Ký đủ bàn giao thì item IN_PROGRESS.
[ ] Ký đủ nghiệm thu thì item DONE.
[ ] Thư mời đến ngày diễn ra hiển thị IN_PROGRESS.
[ ] Thư mời hết thời gian hiển thị DONE.
[ ] Có thanh cam “đơn yêu cầu đang làm/cần chú ý”.
[ ] Notification bell có thông báo khi có thư mời mới.
[ ] Notification bell có thông báo khi có đơn yêu cầu mới.
[ ] Notification bell có thông báo khi proposal accepted/rejected.
[ ] Notification bell có nhắc trước 15 phút.
[ ] Notification bell có thông báo khi nhân sự từ chối.
[ ] Vẫn có nút xem chi tiết đoàn khách.
[ ] Không thay đổi layout UI lớn.
[ ] Không sinh file rác.
[ ] Backend build pass.
[ ] Frontend build pass.
```

---

## 17. Báo cáo sau khi code xong

Báo cáo ngắn gọn:

```text
Đã làm:
- Gộp tab Phân công + Theo dõi tiến độ thành Phân công và tiến độ.
- API/Query/Command đã thêm hoặc sửa.
- Status mapping đã cập nhật.
- Notification mapping đã cập nhật.
- Attention bar đã thêm/sửa.

DB:
- Dùng base hiện tại.
- Không đổi schema.
- Không thêm bảng.

Files changed:
- ...

Build:
- Backend: pass/fail
- Frontend: pass/fail

Lưu ý:
- Những phần chưa làm được hoặc cần xác nhận thêm.
```
