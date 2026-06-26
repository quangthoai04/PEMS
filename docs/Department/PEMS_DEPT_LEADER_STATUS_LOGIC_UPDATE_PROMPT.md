# PROMPT CODE — Sửa Logic Trạng Thái Đơn Yêu Cầu + Thư Mời Department Leader Theo DB Mới

## 0. Bối cảnh

Tôi đang code module **Department Leader — Nhiệm vụ tiếp khách** trong PEMS.

Tôi đã dùng DB mới. Bảng `visit_logistics_items.status` hiện chỉ còn các status:

```text
REQUESTED
CHANGE_PROPOSED
ASSIGNED
ACCEPTED
IN_PROGRESS
DONE
REJECTED
DECLINED
CANCELLED
```

Không còn dùng:

```text
PLANNED
RECEIVED
READY
```

Yêu cầu: sửa lại toàn bộ logic trạng thái của **đơn yêu cầu + thư mời** từ backend đến frontend, nhưng **không sửa UI layout**.

---

## 1. Nguyên tắc bắt buộc

```text
Không rewrite UI.
Không đổi layout, màu sắc, className nếu không cần.
Không tạo mock data.
Không hard-code data.
Không tạo file rác.
Không tạo file thừa.
Không tạo bảng mới.
Không tự ý sửa schema DB.
Không query permissions / role_permissions.
Lấy dữ liệu thật từ DB.
Code clean, dễ tìm, đúng feature folder hiện tại.
Không ảnh hưởng chức năng khác.
Backend theo Clean Architecture: Controller chỉ gọi MediatR.
Build backend pass.
Frontend build pass.
```

Nếu phải sửa enum backend/frontend thì sửa đúng theo DB mới:

```text
REQUESTED
CHANGE_PROPOSED
ASSIGNED
ACCEPTED
IN_PROGRESS
DONE
REJECTED
DECLINED
CANCELLED
```

Không còn dùng trong code:

```text
PLANNED
RECEIVED
READY
```

Nếu còn enum/string mapping cũ trong backend hoặc frontend thì phải xóa/sửa hết.

---

## 2. Ý nghĩa status mới

### Với đơn yêu cầu logistics `visit_logistics_items`

```text
REQUESTED:
Đơn đã gửi trực tiếp tới phòng ban của Department Leader.
Đây là trạng thái “Chờ phân công / Chưa phân công”.

ASSIGNED:
Department Leader đã giao cho nhân sự.
Đang chờ nhân sự phản hồi.

ACCEPTED:
Department Leader tự nhận hoặc nhân sự đã chấp nhận nhiệm vụ.

DECLINED:
Nhân sự từ chối lần phân công.
Không phải từ chối toàn bộ đơn.
Sau DECLINED, Department Leader được phân công lại.

CHANGE_PROPOSED:
Phòng ban đề xuất thay đổi.
Đang chờ bên gửi yêu cầu phản hồi.

REJECTED:
Từ chối toàn bộ đơn yêu cầu.

IN_PROGRESS:
Đơn đang xử lý.
Với đơn yêu cầu có bàn giao: sau khi ký đủ bàn giao thì chuyển IN_PROGRESS.

DONE:
Hoàn thành.
Với đơn yêu cầu có nghiệm thu: sau khi ký đủ nghiệm thu thì chuyển DONE.

CANCELLED:
Đơn bị hủy do đoàn/request bị hủy hoặc không còn cần xử lý.
```

### Với thư mời `visit_participants`

Không sửa enum DB nếu bảng `visit_participants` vẫn đang dùng enum riêng như:

```text
INVITED
ACCEPTED
DECLINED
ASSIGNED
REMOVED
```

Backend phải trả về **unified status** cho UI:

```text
INVITED của Department Leader -> REQUESTED
INVITED của nhân sự được giao -> ASSIGNED
ACCEPTED trước giờ diễn ra -> ACCEPTED
ACCEPTED trong thời gian đoàn diễn ra -> IN_PROGRESS
ACCEPTED sau khi đoàn kết thúc / CLOSED -> DONE
DECLINED do leader từ chối toàn bộ -> REJECTED
DECLINED do nhân sự từ chối phân công -> DECLINED
visit instance CANCELLED -> CANCELLED
```

Không ép sửa enum `visit_participants` nếu DB chưa đổi. Chỉ map sang unified status trong DTO/API.

---

## 3. Dashboard Department Leader

Sửa các KPI/card trên dashboard.

Chỉ lấy dữ liệu từ **thời điểm hiện tại tới tương lai**.

Dữ liệu gồm cả:

```text
Đơn yêu cầu logistics
Thư mời tham gia
```

### Chờ phân công

```text
Lấy các item có unifiedStatus = REQUESTED
```

Ý nghĩa:

```text
Đơn yêu cầu / thư mời đang chờ Department Leader xử lý hoặc phân công.
```

### Đoàn sắp tới

```text
Lấy các item có unifiedStatus = ACCEPTED
```

Chỉ lấy từ hiện tại tới tương lai.

### Đang xử lý

```text
Lấy các item có unifiedStatus = IN_PROGRESS
```

Chỉ lấy từ hiện tại tới tương lai.

Không dùng PLANNED / RECEIVED / READY trong dashboard.

---

## 4. Tab Bảng lịch

Sửa logic màu sắc và action trong calendar.

### Màu xanh dương “Đã xử lý”

Các item **không phải REQUESTED** thì hiển thị màu xanh dương hoặc màu “đã xử lý” theo UI hiện tại.

Áp dụng cho:

```text
ASSIGNED
ACCEPTED
CHANGE_PROPOSED
DECLINED
REJECTED
IN_PROGRESS
DONE
```

### Màu xám

Item màu xám nếu:

```text
- item nằm trong quá khứ
- hoặc unifiedStatus = CANCELLED
```

Thêm chú thích màu xám ở legend:

```text
Bị hủy / Đã hết hạn
```

### Item CANCELLED

Với item CANCELLED:

```text
Ẩn hết nút:
- Từ chối
- Chấp nhận
- Ủy quyền / Đổi người phụ trách
- Đề xuất
- Ký

Chỉ hiển thị text cuối modal:
“Đơn yêu cầu / thư mời đã bị hủy do đoàn khách hủy. Lý do: {cancelReason}”
```

Lý do hủy lấy từ `visit_request_campuses.cancellation_reason` hoặc field cancellation reason hiện có của request/instance. Không hard-code.

---

## 5. Tab “Phân công và tiến độ”

Tab này dùng để thay thế / gộp logic của phân công và theo dõi tiến độ.

Dữ liệu gồm cả:

```text
Đơn yêu cầu
Thư mời
```

### Filter trạng thái phải đúng

Sửa lại filter:

```text
Chưa phân công -> REQUESTED
Đã giao -> ASSIGNED
Chấp nhận -> ACCEPTED
Từ chối -> REJECTED
Đang đề xuất -> CHANGE_PROPOSED
Trong tiến trình -> IN_PROGRESS
Hoàn thành -> DONE
Hủy -> CANCELLED
Từ chối phân công -> DECLINED
```

Không còn filter PLANNED / RECEIVED / READY.

### Search

Search được cả:

```text
Tên đoàn khách
Mã request
Tên đối tác / tổ chức
Tên nhiệm vụ
Nội dung nhiệm vụ
```

### Filter loại

```text
Tất cả
Thư mời
Đơn yêu cầu
```

### Filter phạm vi

```text
Tôi
Văn phòng
```

Ý nghĩa:

```text
Tôi:
- đơn/thư do current user đang phụ trách
- hoặc Department Leader tự ACCEPTED
- hoặc assigned_to_user_id = currentUser.userId
- hoặc visit_participants.user_id = currentUser.userId

Văn phòng:
- tất cả đơn/thư thuộc currentUser.department_id
```

### Filter ngày và sort

Dùng thời gian:

```text
Đơn yêu cầu:
COALESCE(usage_start_at, planned_start_at)
COALESCE(usage_end_at, planned_end_at)

Thư mời:
visit_request_campuses.planned_start_at
visit_request_campuses.planned_end_at
```

Sort theo ngày.

---

## 6. Rule chuyển trạng thái đơn yêu cầu

### Đơn mới gửi tới phòng ban

```text
status = REQUESTED
assigned_to_user_id = NULL
```

Không dùng RECEIVED nữa.

### Leader từ chối toàn bộ đơn

```text
REQUESTED -> REJECTED
```

Rule:

```text
- Bắt buộc nhập lý do.
- Lưu vào decision_note hoặc field hiện có phù hợp.
- REJECTED = từ chối toàn bộ đơn yêu cầu.
```

### Leader tự nhận làm

```text
REQUESTED -> ACCEPTED
```

Update:

```text
assigned_to_user_id = currentUser.userId
assigned_by = currentUser.userId
assigned_at = NOW()
assignee_accepted_at = NOW()
status = ACCEPTED
```

Nếu có bảng `visit_logistics_assignment_attempts`, insert attempt:

```text
status = ACCEPTED
assignee_user_id = currentUser.userId
assigned_by = currentUser.userId
responded_at = NOW()
```

### Leader giao cho nhân sự

```text
REQUESTED -> ASSIGNED
```

Update:

```text
assigned_to_user_id = selectedStaffUserId
assigned_by = currentUser.userId
assigned_at = NOW()
status = ASSIGNED
```

Insert assignment attempt:

```text
status = PENDING
```

### Nhân sự chấp nhận

```text
ASSIGNED -> ACCEPTED
```

Update latest assignment attempt:

```text
status = ACCEPTED
responded_at = NOW()
```

Update item:

```text
status = ACCEPTED
assignee_accepted_at = NOW()
```

### Nhân sự từ chối

```text
ASSIGNED -> DECLINED
```

Rule:

```text
- Chỉ được từ chối trước 24 giờ so với thời điểm đoàn diễn ra.
- Bắt buộc nhập lý do.
- Đây là từ chối lần phân công, không phải từ chối toàn bộ đơn.
```

Check:

```text
NOW() <= planned_start_at - 24 giờ
```

Update latest assignment attempt:

```text
status = DECLINED
responded_at = NOW()
response_note = reason
```

Update item:

```text
status = DECLINED
assignee_response_note = reason
```

Sau đó leader được phân công lại.

### Phân công lại sau DECLINED

Cho phép hiện nút “Đổi người phụ trách / Phân công” khi:

```text
status = REQUESTED
hoặc status = DECLINED
```

Khi phân công lại:

```text
DECLINED -> ASSIGNED
```

Không sửa attempt cũ. Insert attempt mới.

---

## 7. Rule CHANGE_PROPOSED

Nút đề xuất phải hoạt động thật.

Cho phép đề xuất khi item chưa hoàn thành/hủy và người dùng có quyền xử lý.

Khi đề xuất:

```text
status = CHANGE_PROPOSED
proposed_by = currentUser.userId
proposed_at = NOW()
proposed_description = nội dung đề xuất
proposed_usage_start_at / proposed_usage_end_at nếu có
proposed_quantity nếu có
proposal_response = NULL
```

Nếu bên đoàn từ chối đề xuất:

```text
CHANGE_PROPOSED -> REJECTED
proposal_response = REJECTED
proposal_responded_at = NOW()
proposal_response_note = note
decision_note = note
```

Nếu bên đoàn đồng ý đề xuất:

```text
CHANGE_PROPOSED -> ACCEPTED
proposal_response = ACCEPTED
proposal_responded_at = NOW()
```

Sau khi proposal accepted, tạo notification cho người phụ trách và Department Leader.

---

## 8. Rule IN_PROGRESS / DONE

### Với thư mời

Thư mời không có status IN_PROGRESS/DONE trong DB nếu dùng `visit_participants`.

Backend phải derive unified status:

```text
Nếu đã ACCEPTED và thời gian hiện tại nằm trong planned_start_at -> planned_end_at:
unifiedStatus = IN_PROGRESS

Nếu đã ACCEPTED và thời gian hiện tại > planned_end_at hoặc visit instance CLOSED:
unifiedStatus = DONE

Nếu visit instance CANCELLED:
unifiedStatus = CANCELLED
```

### Với đơn yêu cầu

Đơn yêu cầu chỉ chuyển IN_PROGRESS sau khi ký đủ bàn giao.

```text
ACCEPTED -> IN_PROGRESS
```

Điều kiện:

```text
handover_type = BORROW
provider_signed_at IS NOT NULL
borrower_signed_at IS NOT NULL
```

Đơn yêu cầu chỉ chuyển DONE sau khi ký đủ nghiệm thu.

```text
IN_PROGRESS -> DONE
```

Điều kiện:

```text
handover_type = RETURN
borrower_signed_at IS NOT NULL
provider_signed_at IS NOT NULL
```

Khi DONE:

```text
completed_at = NOW()
```

---

## 9. Rule ký bàn giao / nghiệm thu

Dùng bảng:

```text
visit_logistics_item_handovers
```

Không lưu chữ ký trong `visit_logistics_items`.

### Thứ tự ký bàn giao

Phòng ban là bên cho mượn.

Thứ tự bắt buộc:

```text
1. Phòng ban cho mượn ký bàn giao trước.
2. Bên đoàn khách / bên mượn ký nhận sau.
```

Mapping DB:

```text
handover_type = BORROW

provider_signed_by / provider_signed_at:
Phòng ban cho mượn ký trước.

borrower_signed_by / borrower_signed_at:
Bên đoàn khách / bên mượn ký sau.
```

Sau khi đủ 2 chữ ký BORROW:

```text
visit_logistics_items.status = IN_PROGRESS
```

### Thứ tự ký nghiệm thu

Khi đoàn trả lại:

```text
1. Bên đoàn khách / bên mượn ký trả trước.
2. Phòng ban cho mượn xác nhận ký cuối cùng.
```

Mapping DB:

```text
handover_type = RETURN

borrower_signed_by / borrower_signed_at:
Bên đoàn khách / bên mượn ký trả trước.

provider_signed_by / provider_signed_at:
Phòng ban cho mượn xác nhận ký cuối cùng.
```

Sau khi đủ 2 chữ ký RETURN:

```text
visit_logistics_items.status = DONE
completed_at = NOW()
```

### Note từng lần ký

Mỗi lần ký phải có note/ghi chú.

Yêu cầu:

```text
- Ký bàn giao bên phòng ban: lưu note bàn giao của provider.
- Ký nhận bên đoàn khách: lưu note ký nhận của borrower.
- Ký trả/nghiệm thu bên đoàn khách: lưu note/feedback trả.
- Ký xác nhận nghiệm thu bên phòng ban: lưu note/feedback cuối.
```

Nếu DB hiện có field note riêng cho từng bên thì dùng đúng field đó.

Nếu DB hiện chỉ có `condition_note` hoặc một field note chung trong `visit_logistics_item_handovers`, không tự tạo cột mới. Hãy kiểm tra schema trước:

```text
- Nếu đủ field note riêng: lưu đúng từng note.
- Nếu chỉ có note chung: dùng note chung theo từng handover row BORROW/RETURN và báo rõ giới hạn.
- Không tự bịa field không có trong DB.
```

### Chặn ký sai thứ tự

Backend phải chặn:

```text
Không cho bên mượn ký BORROW nếu provider chưa ký bàn giao.
Không cho tạo/ký RETURN nếu BORROW chưa đủ 2 chữ ký.
Không cho provider ký RETURN nếu borrower chưa ký trả.
Không cho ký lại nếu chữ ký đã tồn tại.
Không cho ký nếu item CANCELLED / REJECTED / DONE.
```

---

## 10. Quyền hành động của Department Leader

Tôi đang là Department Leader.

Rule:

```text
Tôi chỉ thực hiện được action xử lý với item tôi đã ACCEPTED hoặc tôi đang là người phụ trách.
```

Nếu đơn/thư do nhân sự khác phụ trách:

```text
Leader chỉ xem được.
Không được xử lý thay.
Không hiện nút action xử lý.
```

Nếu item CANCELLED:

```text
Chỉ xem được.
Ẩn toàn bộ action.
Hiển thị lý do hủy.
```

Nếu item REJECTED / DONE:

```text
Chỉ xem hoặc xem lịch sử.
Không hiện action xử lý tiếp.
```

---

## 11. Điều kiện hiện nút “Đổi người phụ trách / Phân công”

Chỉ hiện khi:

```text
unifiedStatus = REQUESTED
hoặc unifiedStatus = DECLINED
```

Không hiện khi:

```text
ASSIGNED
ACCEPTED
CHANGE_PROPOSED
IN_PROGRESS
DONE
REJECTED
CANCELLED
```

Nếu đã giao cho nhân sự và nhân sự chưa phản hồi:

```text
status = ASSIGNED
Không hiện “Đổi người phụ trách”
```

Nếu nhân sự từ chối:

```text
status = DECLINED
Hiện lại “Đổi người phụ trách / Phân công”
```

---

## 12. Logic CANCELLED

Nếu đơn yêu cầu / thư mời bị hủy do đoàn khách hủy:

```text
unifiedStatus = CANCELLED
```

UI:

```text
Ẩn hết nút:
- Từ chối
- Chấp nhận
- Ủy quyền / Đổi người phụ trách
- Đề xuất
- Ký bàn giao
- Ký nghiệm thu
```

Chỉ hiển thị text:

```text
“Đơn yêu cầu / thư mời đã bị hủy do đoàn khách hủy. Lý do: {cancelReason}”
```

Lý do hủy lấy từ DB thật:

```text
visit_request_campuses.cancellation_reason
hoặc visit_requests.cancellation_reason
hoặc field cancel reason hiện có trong schema
```

Không hard-code lý do.

---

## 13. Backend cần kiểm tra/sửa

Tìm và sửa toàn bộ nơi còn dùng status cũ:

```text
PLANNED
RECEIVED
READY
```

Các nơi cần sửa:

```text
Domain constants/enums
DTO response status enum
Query filter status
Command validation
Status transition validation
Frontend TypeScript enum/type
Badge/status label mapping
Dashboard query
Calendar query
Assignment/progress query
Detail modal permission flags
Notification target/status logic
Email action token handler nếu có
```

Không để code build pass nhưng logic còn mapping cũ.

---

## 14. API / Query cần rà soát

Rà soát các API đang phục vụ UI:

```text
Dashboard Department Leader
Bảng lịch
Phân công và tiến độ
Detail thư mời
Detail đơn yêu cầu
Đề xuất thay đổi
Chấp nhận
Từ chối
Phân công
Ký bàn giao
Ký nghiệm thu
Notification bell
```

Các API list nên trả thêm permission flags:

```ts
{
  canViewDetail: boolean;
  canViewDelegationDetail: boolean;
  canAssign: boolean;
  canAccept: boolean;
  canDecline: boolean;
  canReject: boolean;
  canProposeChange: boolean;
  canSignBorrowProvider: boolean;
  canSignBorrowBorrower: boolean;
  canSignReturnBorrower: boolean;
  canSignReturnProvider: boolean;
  isReadOnly: boolean;
  cancelReason?: string;
}
```

Frontend chỉ dựa vào flags này để ẩn/hiện button.

Backend vẫn phải kiểm tra lại quyền khi gọi action.

---

## 15. Frontend cần kiểm tra/sửa

Không sửa UI layout.

Chỉ sửa:

```text
API service
hooks
DTO types
status mapping
filter options
action handlers
button visibility
toast message
refetch sau action
empty/loading/error state nếu đang sai
```

Sau mỗi action thành công:

```text
refetch dashboard cards nếu liên quan
refetch calendar
refetch assignment/progress list
refetch detail modal nếu đang mở
refetch notification count nếu liên quan
show toast
```

Không reload full page.

---

## 16. Notification bell

Rà soát notification để map đúng logic mới.

Cần có notification cho:

```text
- Có thư mời mới gửi tới phòng ban.
- Có đơn yêu cầu mới gửi tới phòng ban.
- Bên đoàn đồng ý đề xuất.
- Bên đoàn từ chối đề xuất.
- Nhân sự từ chối nhiệm vụ được giao.
- Nhắc trước 15 phút khi đoàn sắp diễn ra.
- Đơn/thư bị hủy do đoàn khách hủy.
```

Nếu chưa có scheduler/background job:

```text
Không tự thêm công nghệ mới.
Dùng cơ chế hiện có.
Nếu chưa có, implement tối thiểu bằng query idempotent khi load dashboard/calendar/notification.
```

---

## 17. Checklist nghiệm thu

```text
[ ] Không còn dùng PLANNED / RECEIVED / READY trong backend enum.
[ ] Không còn dùng PLANNED / RECEIVED / READY trong frontend type/mapping.
[ ] Dashboard “Chờ phân công” lấy REQUESTED từ hiện tại tới tương lai.
[ ] Dashboard “Đoàn sắp tới” lấy ACCEPTED từ hiện tại tới tương lai.
[ ] Dashboard “Đang xử lý” lấy IN_PROGRESS từ hiện tại tới tương lai.
[ ] Calendar item đã xử lý màu xanh dương nếu không phải REQUESTED.
[ ] Calendar item quá khứ hoặc CANCELLED màu xám.
[ ] Calendar có chú thích màu xám: Bị hủy / Đã hết hạn.
[ ] Item CANCELLED ẩn hết action, chỉ hiện text lý do hủy.
[ ] Tab Phân công và tiến độ filter đúng các trạng thái mới.
[ ] Search được cả đoàn khách và nhiệm vụ.
[ ] Nút đề xuất hoạt động thật, không mock.
[ ] CHANGE_PROPOSED bị bên đoàn từ chối thì chuyển REJECTED.
[ ] Đề xuất được đồng ý thì chuyển ACCEPTED và có notification.
[ ] Nút phân công chỉ hiện ở REQUESTED / DECLINED.
[ ] ASSIGNED không hiện đổi người phụ trách.
[ ] Nhân sự từ chối chỉ được trước 24h.
[ ] Nhân sự từ chối chuyển status DECLINED.
[ ] Leader phân công lại được sau DECLINED.
[ ] Leader chỉ xử lý item mình ACCEPTED / mình phụ trách.
[ ] Item nhân sự khác phụ trách thì leader chỉ xem.
[ ] Ký bàn giao đúng thứ tự: phòng ban ký trước, bên mượn ký sau.
[ ] Đủ 2 chữ ký bàn giao thì status IN_PROGRESS.
[ ] Chỉ được ký nghiệm thu sau khi bàn giao đủ.
[ ] Ký nghiệm thu đúng thứ tự: bên mượn ký trả trước, phòng ban xác nhận cuối.
[ ] Đủ 2 chữ ký nghiệm thu thì status DONE.
[ ] Note từng lần ký được lưu đúng theo schema hiện có.
[ ] Không tạo mock data.
[ ] Không tạo file rác.
[ ] Backend build pass.
[ ] Frontend build pass.
```

---

## 18. Báo cáo sau khi sửa

Báo cáo ngắn gọn:

```text
Đã sửa:
- Backend enum/status mapping...
- Dashboard logic...
- Calendar logic...
- Phân công và tiến độ...
- Đề xuất thay đổi...
- Ký bàn giao/nghiệm thu...
- Notification...

Files changed:
- ...

DB:
- Dùng DB mới.
- Không sửa schema.
- Không thêm bảng.

Build:
- Backend: pass/fail
- Frontend: pass/fail

Lưu ý còn lại:
- ...
```
