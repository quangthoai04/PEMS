# Department Task / Logistics / Email Token Flow

## 1. Scope
Tài liệu này ghi lại chi tiết toàn bộ logic nghiệp vụ, cơ chế matrix trạng thái (status matrix), chính sách hiển thị nút trong email (email button policy), token guard và SQL kiểm tra token lỗi thời (stale-token checks) dành cho luồng phối hợp giữa IC/Host và Department (Leader/Staff).

## 2. Roles involved
- **IC / Host**: Người gửi yêu cầu tham gia hoặc hậu cần.
- **Department Leader**: Trưởng phòng ban tiếp nhận yêu cầu, có thể xử lý trực tiếp hoặc phân công xuống cấp dưới.
- **Department Staff**: Nhân viên thuộc phòng ban, được Department Leader phân công xử lý hậu cần/tham gia.

## 3. Core business rules
IC/Host có thể gửi 2 loại việc cho phòng ban:
1. Lời mời tham gia hỗ trợ đoàn.
2. Yêu cầu logistics / hậu cần.

Department Leader khi nhận việc có thể:
- Chấp nhận trực tiếp.
- Từ chối trực tiếp (bắt buộc nhập lý do).
- Gán cho cấp dưới xử lý (chỉ được thực hiện qua hệ thống).
- Đề xuất thay đổi lại cho IC/Host (chỉ được thực hiện qua hệ thống).

Department Staff nếu được gán việc có thể:
- Chấp nhận nhiệm vụ.
- Từ chối nhiệm vụ (bắt buộc nhập lý do).
- Đề xuất thay đổi lại cho IC/Host (chỉ được thực hiện qua hệ thống).

IC/Host khi nhận đề xuất thay đổi có thể chấp nhận hoặc từ chối đề xuất qua hệ thống. Khi IC/Host xử lý, luồng đề xuất kết thúc.

## 4. Email button policy
### 4.1. Email gửi cho Department Leader
Chỉ chứa các nút public email token sau:
- Đồng ý (hoặc Chấp nhận)
- Từ chối
- Xem chi tiết / Hành động khác (link vào hệ thống, yêu cầu đăng nhập)

Không chứa public email token cho thao tác Gán nhân sự hay Đề xuất thay đổi để đảm bảo kiểm tra quyền và xác thực người dùng.

### 4.2. Email gửi cho Department Staff
Chỉ chứa các nút:
- Chấp nhận nhiệm vụ
- Từ chối nhiệm vụ
- Xem chi tiết trong hệ thống (link vào hệ thống, yêu cầu đăng nhập)

### 4.3. Email gửi cho IC/Host khi có proposal
Chỉ hiển thị link "Xem chi tiết trong hệ thống". IC/Host bắt buộc phải đăng nhập để chấp nhận / từ chối đề xuất.

## 5. Department Leader flow
- Nhận email từ IC/Host (Trạng thái ban đầu: `REQUESTED`).
- Nếu click Chấp nhận/Đồng ý trên email: Trạng thái đổi thành `ACCEPTED`. (Lưu ý: Sau khi đã ACCEPTED thì không được gán cấp dưới nữa. Nếu muốn gán, Leader phải vào hệ thống gán từ khi item còn REQUESTED).
- Nếu click Từ chối trên email: Bắt buộc render form lấy lý do, cập nhật `REJECTED`. (Lưu ý: DECLINED là dành cho Department Staff).
- Truy cập vào hệ thống để phân công (assign_to) cho nhân sự thuộc phòng ban (khi item đang `REQUESTED`). Khi gán thành công, status thành `ASSIGNED`, token cũ liên kết với Leader sẽ bị invalid, và email mới được gửi cho Staff.

## 6. Department Staff flow
- Staff nhận email báo đã được phân công (Trạng thái: `ASSIGNED`).
- Staff bấm Chấp nhận -> Trạng thái đổi thành `ACCEPTED`.
- Staff bấm Từ chối -> Render form lấy lý do -> Đổi thành `DECLINED`. (Lưu ý: REJECTED là dành cho Department Leader từ chối IC/Host).
- `DECLINED` là trạng thái **kết thúc (terminal)**: hệ thống không tự động hay cho phép gán lại nhiệm vụ đã bị Staff từ chối cho một Staff khác — Leader phải tạo lại yêu cầu logistics mới nếu vẫn cần xử lý. Không có nhánh "phân công người khác" từ `DECLINED`.
- Gửi đề xuất thay đổi yêu cầu thông qua giao diện hệ thống. Không được gửi đề xuất hay gán người khác qua email.

## 7. IC/Host proposal response flow
Khi có đề xuất thay đổi từ Department Leader/Staff:
- Hệ thống gửi thông báo cho IC/Host (không sinh email public token).
- IC/Host vào hệ thống xem chi tiết thay đổi (proposed_quantity, proposed_usage_start, proposed_usage_end, proposal_note).
- IC/Host Chấp nhận (proposal_response = ACCEPTED) hoặc Từ chối (proposal_response = REJECTED, bắt buộc proposal_response_note).

## 8. Status matrix

### 8.1. Participant invitation matrix
Có 2 loại "lời mời tham gia" khác hẳn nhau, mint token dưới 2 action context riêng — token của loại này
KHÔNG hợp lệ với dòng đang ở trạng thái của loại kia:

- **Lời mời trực tiếp** (Host/Leader mời thẳng một người) — context `PARTICIPATION_RESPONSE`, chỉ hợp lệ
  khi participant đang `INVITED`.
- **Phân công qua Department** (Leader gán một Staff xử lý thay) — context
  `PARTICIPATION_ASSIGNMENT_RESPONSE`, chỉ hợp lệ khi participant đang `ASSIGNED`.

| Current DB Status | Token context                       | Token Action   | Result Code       | Ghi chú                                                  |
|--------------------|-------------------------------------|----------------|-------------------|-----------------------------------------------------------|
| INVITED            | PARTICIPATION_RESPONSE              | ACCEPT/DECLINE | SUCCESS           | Thao tác hợp lệ (lời mời trực tiếp)                       |
| ASSIGNED           | PARTICIPATION_ASSIGNMENT_RESPONSE   | ACCEPT/DECLINE | SUCCESS           | Thao tác hợp lệ (Staff xác nhận/từ chối nhiệm vụ được phân công) |
| INVITED            | PARTICIPATION_ASSIGNMENT_RESPONSE   | ACCEPT/DECLINE | INVALID           | Token phân công dùng cho dòng còn ở lời mời trực tiếp — không đúng ngữ cảnh |
| ASSIGNED           | PARTICIPATION_RESPONSE              | ACCEPT/DECLINE | INVALID           | Token lời mời trực tiếp dùng cho dòng đã được phân công — không đúng ngữ cảnh |
| ACCEPTED/DECLINED  | (cả 2 context)                      | ACCEPT/DECLINE | ALREADY_RESPONDED | Đã phản hồi trước đó                                       |
| REMOVED            | (cả 2 context)                      | ACCEPT/DECLINE | INVALID           | Lời mời/nhiệm vụ đã bị thu hồi                             |

Quy tắc gán lại (`AssignDepartmentStaffCommandHandler`, xem BUG-02/BUG-03): một dòng đang `ASSIGNED` chỉ
được ghi đè bởi đúng Leader đã gán (idempotent, không mint token mới); Leader khác, hoặc dòng đã
`ACCEPTED`/`DECLINED`/`REMOVED`, đều bị từ chối (`ConflictException`) — không có khái niệm "gán lại" ngầm
định cho các trạng thái này.

### 8.2. Logistics request response matrix (Dành cho Leader)
| Current DB Status                   | Token Action   | Result Code       | Ghi chú                                 |
|-------------------------------------|----------------|-------------------|-----------------------------------------|
| REQUESTED                           | ACCEPT/DECLINE | SUCCESS           | Thao tác hợp lệ                         |
| ACCEPTED / DONE                     | ACCEPT/DECLINE | ALREADY_RESPONDED | Leader đã tiếp nhận, duyệt hoặc hoàn tất|
| ASSIGNED                            | ACCEPT/DECLINE | INVALID           | Leader đã gán cấp dưới -> không thể nhận|
| REJECTED / DECLINED / CANCELLED     | ACCEPT/DECLINE | INVALID           | Đã hủy bỏ hoặc bị từ chối               |
| CHANGE_PROPOSED / IN_PROGRESS       | ACCEPT/DECLINE | INVALID           | Đang có quy trình khác chạy             |

### 8.3. Logistics assignee response matrix (Dành cho Staff)
| Current DB Status                   | Token Action   | Result Code       | Ghi chú                                         |
|-------------------------------------|----------------|-------------------|-------------------------------------------------|
| ASSIGNED (Đúng User)                | ACCEPT/DECLINE | SUCCESS           | Thao tác hợp lệ                                 |
| ASSIGNED (Sai User)                 | ACCEPT/DECLINE | INVALID           | Đã được gán cho người khác (hoặc re-assigned)   |
| ACCEPTED                            | ACCEPT/DECLINE | ALREADY_RESPONDED | Staff đã phản hồi                               |
| DECLINED                            | ACCEPT/DECLINE | ALREADY_RESPONDED | Staff đã từ chối                                |
| REJECTED / CANCELLED / DONE         | ACCEPT/DECLINE | INVALID           | Đã hoàn thành hoặc bị hủy bỏ                    |
| CHANGE_PROPOSED                     | ACCEPT/DECLINE | INVALID           | Token assignee response không dùng được ở đây   |

### 8.4. Parent Guard (áp dụng chung)
Nếu `VisitRequestCampus.Status` ở trạng thái CANCELLED hoặc CLOSED:
- Toàn bộ action trả về `INVALID`.

## 9. Email action contexts
Các Action Context đang được hệ thống PEMS mint token và sử dụng thực tế:
- `PARTICIPATION_RESPONSE`: Gửi lời mời tham gia trực tiếp vào visit (Host/Leader mời thẳng một người).
- `PARTICIPATION_ASSIGNMENT_RESPONSE`: Gửi phân công tham gia qua Department (Leader gán một Staff xử lý thay) — token riêng, không lẫn với lời mời trực tiếp (xem Mục 8.1).
- `LOGISTICS_REQUEST_RESPONSE`: Gửi yêu cầu hậu cần cho Department Leader.
- `LOGISTICS_ASSIGNEE_RESPONSE`: Gửi phân công công việc hậu cần cho Department Staff.

Các Action Context **bị vô hiệu hóa / không còn mint public token** (Portal-only):
- `LOGISTICS_PROPOSAL_RESPONSE`: đã ngừng mint token public (trước đây có mint) — quyết định Chấp nhận/Từ chối đề xuất bắt buộc thực hiện trong hệ thống sau khi đăng nhập, không còn nút Approve/Reject qua email cho IC/Host (xem Mục 4.3/7). Token cũ (mint trước khi đổi luồng) khi được click chỉ trả `INVALID` kèm hướng dẫn đăng nhập, không bao giờ mutate dữ liệu.
- `LOGISTICS_NEGOTIATION`
- `LOGISTICS_HANDOVER_SIGNATURE`: Chưa triển khai mint token chữ ký bàn giao qua email.

## 10. Token guard rules
Trong `ExecuteEmailActionCommandHandler` và `GetEmailActionInfoQueryHandler`:
1. Kiểm tra tồn tại của Token.
2. Kiểm tra `ResultStatus == INVALID` -> Báo "Lời mời đã bị thu hồi / không còn hiệu lực".
3. Kiểm tra Token `ExpiresAt` -> Báo hết hạn.
4. Kiểm tra `UsedAt != null` hoặc `ResultStatus == ALREADY_RESPONDED` -> Báo đã phản hồi.
5. Kiểm tra Parent Status -> Trả `INVALID` nếu parent = CANCELLED / CLOSED.
6. Lấy DB Realtime Status và validate (xem Status Matrix).
7. Execute Action (với transaction).

## 11. Token invalidation rules
Sử dụng `EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync()` hoặc `InvalidateTokensForVisitInstanceAsync()` để vô hiệu hóa token trong các trường hợp:
- Participant bị Host REMOVED.
- Logistics item bị CANCELLED.
- Leader Re-assign cho Staff khác -> Assignee token cũ bị vô hiệu hóa.
- Leader gán Staff (từ INVITED thành ASSIGNED) -> Token Participant cũ của Leader bị vô hiệu hóa.
- Visit Instance bị CANCELLED / CLOSED -> Hủy sạch Token liên quan.
- Department Leader bấm Đồng ý/Từ chối request: invalidate token còn lại cùng group.
- Department Staff chấp nhận/từ chối nhiệm vụ: invalidate token còn lại cùng group.
- Item CANCELLED / REJECTED / DECLINED / DONE: invalidate toàn bộ pending token của item.

## 12. Frontend visibility rules
- **Department Leader View**: Nếu trạng thái còn cho phép xử lý, hiển thị các nút "Chấp nhận", "Từ chối", "Gán nhân sự" và "Đề xuất thay đổi". Nút "Gán nhân sự" chỉ dành cho role Leader. Từ chối và Đề xuất thay đổi yêu cầu mở modal/form nhập lý do (Proposal Note). Không hiển thị nếu item đã DONE / CANCELLED / REJECTED / CLOSED. Sau khi ACCEPTED không cần gán cấp dưới.
- **Department Staff View**: Chỉ `assigned_to_user_id` hiện tại mới thấy các nút "Chấp nhận", "Từ chối", "Đề xuất thay đổi". Không hiển thị nếu DONE / CANCELLED / REJECTED / DECLINED.
- **IC/Host View**: Khi có proposal đang mở, hiển thị so sánh trường thông tin đề xuất và gốc kèm theo nút "Chấp nhận đề xuất", "Từ chối đề xuất". Sau khi phản hồi, nút biến mất và hiển thị kết quả.

## 13. Toast/notification rules
- Frontend sử dụng toast góc phải màn hình sau mỗi hành động POST/PUT thành công (vd: Gán nhân sự thành công, Đề xuất thành công, Chấp nhận thành công).
- Frontend thông báo lỗi (ví dụ: Thiếu lý do đề xuất) dưới dạng error message validation / toast.
- Không spam thông báo liên tục khi bấm nhiều nút, chỉ dùng 1 toast.
- Nếu token link email đã bị `INVALID`, render trực tiếp View Lỗi thay vì redirect vào hệ thống.

## 14. SQL stale-token checks
Có thể chạy các Query sau trực tiếp trên MySQL Database để lọc token lỗi thời:

### Lọc trạng thái không tồn tại
```sql
SELECT COUNT(*) AS received_status_count
FROM visit_logistics_items
WHERE status = 'RECEIVED';
-- Phải luôn = 0.
```

### Participant stale tokens
```sql
SELECT t.email_action_token_id, t.action_context, t.target_type, t.target_id, t.intended_action, t.result_status, p.status AS participant_status
FROM email_action_tokens t
JOIN visit_participants p ON p.participant_id = t.target_id
WHERE t.target_type = 'VISIT_PARTICIPANT' AND t.result_status = 'PENDING'
  AND p.status IN ('ACCEPTED', 'DECLINED', 'REMOVED', 'ASSIGNED');
```

### Logistics request token sai status
```sql
SELECT t.email_action_token_id, t.action_context, t.target_id, t.intended_action, t.result_status, l.status AS logistics_status
FROM email_action_tokens t
JOIN visit_logistics_items l ON l.logistics_item_id = t.target_id
WHERE t.target_type = 'LOGISTICS_ITEM' AND t.action_context = 'LOGISTICS_REQUEST_RESPONSE' AND t.result_status = 'PENDING'
  AND l.status <> 'REQUESTED';
```

### Logistics assignee token sai status hoặc sai người
```sql
SELECT t.email_action_token_id, t.action_context, t.target_id, t.recipient_user_id, l.assigned_to_user_id, t.result_status, l.status AS logistics_status
FROM email_action_tokens t
JOIN visit_logistics_items l ON l.logistics_item_id = t.target_id
WHERE t.target_type = 'LOGISTICS_ITEM' AND t.action_context = 'LOGISTICS_ASSIGNEE_RESPONSE' AND t.result_status = 'PENDING'
  AND (l.status <> 'ASSIGNED' OR l.assigned_to_user_id <> t.recipient_user_id);
```

### Parent cancelled/closed stale tokens
```sql
SELECT t.email_action_token_id, t.action_context, t.target_type, t.target_id, t.result_status, vrc.status AS campus_visit_status
FROM email_action_tokens t
LEFT JOIN visit_participants p ON t.target_type = 'VISIT_PARTICIPANT' AND p.participant_id = t.target_id
LEFT JOIN visit_logistics_items l ON t.target_type = 'LOGISTICS_ITEM' AND l.logistics_item_id = t.target_id
LEFT JOIN visit_request_campuses vrc ON vrc.visit_instance_id = COALESCE(p.visit_instance_id, l.visit_instance_id)
WHERE t.result_status = 'PENDING' AND vrc.status IN ('CANCELLED', 'CLOSED');
```

## 15. Test checklist
- [x] Không còn RECEIVED trong logic backend/frontend.
- [x] IC gửi logistics request -> status REQUESTED.
- [x] Email Department Leader có Đồng ý/Từ chối/Xem chi tiết.
- [x] Email Request cho Leader không có nút Gán nhân sự dạng public token.
- [x] Email Request cho Leader không có nút Đề xuất thay đổi dạng public token.
- [x] Department Leader bấm Đồng ý -> REQUESTED -> ACCEPTED.
- [x] Sau ACCEPTED không còn cho gán cấp dưới.
- [x] Department Leader bấm Từ chối -> REQUESTED -> REJECTED.
- [x] Department Leader ở status REQUESTED bấm Gán nhân sự trong hệ thống -> REQUESTED -> ASSIGNED.
- [x] Staff nhận email assignee.
- [x] Staff bấm Chấp nhận -> ASSIGNED -> ACCEPTED.
- [x] Staff bấm Từ chối -> ASSIGNED -> DECLINED.
- [x] Đổi assignee -> Staff cũ bấm email cũ -> INVALID.
- [x] Cancel item -> mọi token cũ -> INVALID.
- [x] Parent CANCELLED/CLOSED -> mọi token logistics cũ -> INVALID.
- [x] Department-Staff phân công (ASSIGNED) trả lời được qua Email (`PARTICIPATION_ASSIGNMENT_RESPONSE`) — trước đây bị từ chối nhầm (BUG-02).
- [x] Portal và Email race trên cùng 1 dòng participant/logistics — chỉ đúng 1 bên thắng, không có mutation từ phía thua (concurrency test thật trên MySQL 2 kết nối).
- [x] Response race với Campus Cancel — response thua lock không commit trên snapshot cũ (parent-lifecycle concurrency test).
- [x] Đề xuất thay đổi (proposal) không còn mint public token — email chỉ có link "Xem chi tiết trong hệ thống" (BUG-07).
- [x] CSP Production: route `/api/public/email-actions/*` cho phép form/style inline; mọi route khác giữ `form-action 'none'`.

## 16. Known limitations / pending confirmations
- `LOGISTICS_PROPOSAL_RESPONSE`: **Quyết định thiết kế cố định**, không phải tính năng còn thiếu — proposal Chấp nhận/Từ chối chỉ được thực hiện trong hệ thống sau khi đăng nhập; không mint public token nữa (xem Mục 9). Không có kế hoạch "bật lại" mint token cho context này.
- `LOGISTICS_HANDOVER_SIGNATURE`: Chưa triển khai mint token chữ ký bàn giao qua email.
