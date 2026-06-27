# Yêu cầu triển khai: Department Task / Logistics / Email Token Flow

## 1. Mục tiêu

Tài liệu này dùng để yêu cầu AI Agent kiểm tra, hoàn thiện và triển khai đầy đủ luồng Department Leader / Department Staff xử lý **lời mời tham gia** và **yêu cầu logistics/hậu cần** do IC/Host gửi sang phòng ban.

Yêu cầu triển khai trên **base code thật**, không mock data, không sinh file rác, không phá UI hiện tại nếu không cần.

Các mục tiêu chính:

- Chuẩn hóa luồng Department Leader nhận, từ chối, gán cấp dưới, đề xuất thay đổi.
- Chuẩn hóa luồng Department Staff nhận, từ chối, đề xuất thay đổi khi được gán.
- Chuẩn hóa luồng IC/Host xử lý đề xuất thay đổi.
- Kiểm tra email nào được có token button, email nào chỉ được có link vào hệ thống.
- Bổ sung guard theo status cho `email_action_tokens` giống cơ chế bảo vệ token đã triển khai trước đó.
- Invalidate token cũ khi status target thay đổi ngoài email.
- Tạo file Markdown business rule chi tiết trong source code.

---

## 2. File Markdown bắt buộc cần tạo trong source code

Sau khi triển khai, AI Agent phải tạo hoặc cập nhật file sau trong repository:

```text
docs/business-rules/department-task-logistics-email-token-flow.md
```

Nếu folder chưa có thì tạo đúng folder này.

File `.md` này phải ghi lại chi tiết toàn bộ logic nghiệp vụ, status matrix, email button policy, token guard, frontend visibility rule, SQL stale-token checks và test checklist.

---

## 3. Bối cảnh nghiệp vụ

IC/Host có thể gửi sang phòng ban 2 loại việc:

```text
1. Lời mời tham gia hỗ trợ đoàn.
2. Yêu cầu logistics / hậu cần.
```

Người nhận ban đầu là `Department Leader`.

Department Leader có thể:

```text
1. Chấp nhận trực tiếp.
2. Từ chối trực tiếp.
3. Gán cho cấp dưới xử lý.
4. Đề xuất thay đổi lại cho IC/Host.
```

Department Staff nếu được Department Leader gán thì có thể:

```text
1. Chấp nhận nhiệm vụ.
2. Từ chối nhiệm vụ.
3. Đề xuất thay đổi lại cho IC/Host nếu nghiệp vụ cho phép.
```

IC/Host nếu nhận được đề xuất thay đổi thì có thể:

```text
1. Chấp nhận đề xuất.
2. Từ chối đề xuất.
```

Sau khi IC/Host chấp nhận hoặc từ chối đề xuất thì **luồng đề xuất kết thúc**, không được xử lý lặp lại.

---

## 4. Chính sách nút email bắt buộc

### 4.1. Email gửi cho Department Leader

Khi IC/Host gửi lời mời hoặc yêu cầu logistics cho Department Leader, email chỉ được có các nút/token sau:

```text
- Chấp nhận
- Từ chối
- Xem chi tiết trong hệ thống
```

Không được có public email token cho:

```text
- Gán nhân sự
- Đề xuất thay đổi
```

Hai hành động này bắt buộc Department Leader phải vào hệ thống và đăng nhập để thao tác.

Lý do:

- Gán nhân sự cần kiểm tra quyền Department Leader, danh sách nhân sự cùng phòng ban, trạng thái target hiện tại.
- Đề xuất thay đổi cần form nhập số lượng, thời gian, nội dung và lý do đề xuất.
- Không xử lý các thao tác phức tạp này bằng email public token nếu chưa có guard đầy đủ.

### 4.2. Email gửi cho Department Staff sau khi được gán

Khi Department Leader gán nhiệm vụ cho Department Staff, email gửi cho Staff được có:

```text
- Chấp nhận nhiệm vụ
- Từ chối nhiệm vụ
- Xem chi tiết trong hệ thống
```

Không được có public email token cho:

```text
- Gán tiếp người khác
- Đề xuất thay đổi qua email
- Ký bàn giao qua email
```

Nếu Department Staff muốn đề xuất thay đổi thì phải vào hệ thống.

### 4.3. Email gửi cho IC/Host khi có đề xuất thay đổi

Nếu `LOGISTICS_PROPOSAL_RESPONSE` chưa được implement đầy đủ trong `ExecuteEmailActionCommandHandler`, email gửi IC/Host khi có đề xuất chỉ được có:

```text
- Xem chi tiết trong hệ thống
```

IC/Host phải vào hệ thống để bấm:

```text
- Chấp nhận đề xuất
- Từ chối đề xuất
```

Nếu sau này muốn cho IC/Host duyệt đề xuất trực tiếp qua email thì phải implement riêng action context:

```text
LOGISTICS_PROPOSAL_RESPONSE
```

và guard đầy đủ theo status.

---

## 5. Luồng lời mời tham gia phòng ban

### 5.1. IC/Host gửi lời mời đến Department Leader

Trạng thái ban đầu:

```text
visit_participants.status = INVITED
```

Email action context:

```text
PARTICIPATION_RESPONSE
```

Token button:

```text
ACCEPT
DECLINE
```

Không có token cho:

```text
ASSIGN_STAFF
PROPOSE_CHANGE
```

### 5.2. Department Leader chấp nhận trực tiếp

Chỉ hợp lệ khi:

```text
visit_participants.status = INVITED
```

Kết quả:

```text
INVITED -> ACCEPTED
responded_at = NOW
note = null hoặc note nếu có
```

Sau đó luồng kết thúc.

### 5.3. Department Leader từ chối trực tiếp

Chỉ hợp lệ khi:

```text
visit_participants.status = INVITED
```

Yêu cầu:

```text
- Bắt buộc nhập lý do từ chối.
- GET chỉ render form.
- POST mới update DB.
```

Kết quả:

```text
INVITED -> DECLINED
responded_at = NOW
note = declineReason.Trim()
```

Sau đó luồng kết thúc.

### 5.4. Department Leader gán lời mời cho cấp dưới

Department Leader phải vào hệ thống để gán.

Backend phải validate:

```text
- Current user là Department Leader.
- Department Leader cùng department/campus với lời mời.
- Staff được gán thuộc cùng department.
- Participant hiện còn ở status cho phép gán.
- Visit instance chưa CANCELLED/CLOSED.
```

Gợi ý status:

```text
INVITED -> ASSIGNED
assigned_to_user_id = staffUserId
assigned_by = currentUserId
assigned_at = NOW
```

Sau khi gán:

```text
- Gửi email cho Department Staff.
- Invalidate token cũ của Department Leader nếu token đó không còn phù hợp.
```

Email cho Department Staff có:

```text
- Chấp nhận nhiệm vụ
- Từ chối nhiệm vụ
- Xem chi tiết trong hệ thống
```

### 5.5. Department Staff xử lý lời mời được gán

Staff chấp nhận:

```text
Chỉ hợp lệ khi:
visit_participants.status = ASSIGNED
AND assigned_to_user_id = current/token recipient user
```

Kết quả gợi ý:

```text
ASSIGNED -> ACCEPTED
responded_at = NOW
```

Staff từ chối:

```text
Chỉ hợp lệ khi:
visit_participants.status = ASSIGNED
AND assigned_to_user_id = current/token recipient user
```

Yêu cầu:

```text
- Bắt buộc lý do từ chối.
```

Kết quả gợi ý:

```text
ASSIGNED -> DECLINED
note = declineReason.Trim()
responded_at = NOW
```

Sau đó luồng kết thúc, trừ khi code hiện tại đã có flow trả về Department Leader. Nếu chưa có flow trả về thì không tự thêm.

---

## 6. Luồng logistics / hậu cần

### 6.1. IC/Host gửi yêu cầu logistics cho Department Leader

Trạng thái ban đầu:

```text
visit_logistics_items.status = REQUESTED
```

Email action context bắt buộc:

```text
LOGISTICS_REQUEST_RESPONSE
```

Không dùng chung với:

```text
LOGISTICS_ASSIGNEE_RESPONSE
```

Email cho Department Leader có:

```text
- Chấp nhận
- Từ chối
- Xem chi tiết trong hệ thống
```

Không có public token cho:

```text
- Gán nhân sự
- Đề xuất thay đổi
```

### 6.2. Department Leader chấp nhận yêu cầu logistics trực tiếp

Chỉ hợp lệ khi:

```text
visit_logistics_items.status = REQUESTED
```

Kết quả tùy flow hiện tại, nhưng phải nhất quán.

Nếu hệ thống có bước “phòng ban đã nhận yêu cầu”:

```text
REQUESTED -> RECEIVED
received_by = currentUserId
received_at = NOW
```

Nếu hệ thống coi Department Leader nhận là xử lý luôn:

```text
REQUESTED -> ACCEPTED
assignee_accepted_at = NOW
```

Ưu tiên dùng `RECEIVED` nếu sau đó vẫn có thể gán nhân sự hoặc xử lý nội bộ.

Sau khi nhận trực tiếp, token cũ cùng group phải invalid/success đúng trạng thái, không cho bấm lại nút còn lại.

### 6.3. Department Leader từ chối yêu cầu logistics

Chỉ hợp lệ khi:

```text
visit_logistics_items.status = REQUESTED
```

Yêu cầu:

```text
- Bắt buộc nhập lý do từ chối.
- GET chỉ render form.
- POST mới update.
```

Kết quả:

```text
REQUESTED -> REJECTED
assignee_response_note hoặc response_note = declineReason.Trim()
responded_at / updated_at = NOW
```

Sau đó luồng kết thúc.

### 6.4. Department Leader gán logistics cho cấp dưới

Department Leader phải vào hệ thống để gán.

Backend validate:

```text
- Current user là Department Leader.
- Logistics item thuộc department của leader.
- Staff được gán thuộc cùng department.
- Item status đang cho phép gán.
- Item chưa CANCELLED / REJECTED / DONE.
- Visit instance chưa CANCELLED / CLOSED.
```

Status cho phép gán nên là:

```text
REQUESTED
RECEIVED
```

Không cho gán nếu:

```text
CHANGE_PROPOSED
ACCEPTED
IN_PROGRESS
READY
DONE
REJECTED
CANCELLED
```

Kết quả:

```text
status = ASSIGNED
assigned_to_user_id = staffUserId
assigned_by = currentUserId
assigned_at = NOW
```

Sau khi gán:

```text
- Gửi email cho Department Staff.
- Email action context = LOGISTICS_ASSIGNEE_RESPONSE.
- Token cũ của assignee cũ nếu có phải INVALID.
- Token request cũ không còn phù hợp phải được invalidate.
```

Email cho Department Staff có:

```text
- Chấp nhận nhiệm vụ
- Từ chối nhiệm vụ
- Xem chi tiết trong hệ thống
```

### 6.5. Department Staff nhận logistics được gán

Email action context:

```text
LOGISTICS_ASSIGNEE_RESPONSE
```

Staff chấp nhận chỉ hợp lệ khi:

```text
visit_logistics_items.status = ASSIGNED
AND visit_logistics_items.assigned_to_user_id = token.recipient_user_id
```

Kết quả gợi ý:

```text
ASSIGNED -> ACCEPTED
assignee_accepted_at = NOW
```

Staff từ chối chỉ hợp lệ khi:

```text
visit_logistics_items.status = ASSIGNED
AND visit_logistics_items.assigned_to_user_id = token.recipient_user_id
```

Yêu cầu:

```text
- Bắt buộc lý do từ chối.
```

Kết quả gợi ý:

```text
ASSIGNED -> REJECTED
assignee_response_note = declineReason.Trim()
```

Sau đó luồng kết thúc, trừ khi code hiện tại đã có flow trả lại Department Leader. Nếu chưa có flow trả lại thì không tự thêm.

---

## 7. Luồng đề xuất thay đổi

### 7.1. Department Leader đề xuất thay đổi

Department Leader phải vào hệ thống.

Không được xử lý proposal bằng email token nếu chưa implement đủ.

Điều kiện cho phép đề xuất:

```text
- Item thuộc department của Department Leader.
- Item status IN (REQUESTED, RECEIVED, ASSIGNED) tùy flow hiện tại.
- Item chưa CANCELLED / REJECTED / DONE.
- Visit instance chưa CANCELLED / CLOSED.
- Chưa có proposal pending.
```

Khi gửi đề xuất:

```text
- Không ghi đè field gốc.
- Ghi vào proposed_*.
- proposal_note bắt buộc.
- proposed_by = currentUserId.
- proposed_at = NOW.
- proposal_response = NULL.
- proposal_response_note = NULL.
- status = CHANGE_PROPOSED.
```

Các field cần dùng nếu có:

```text
proposed_quantity
proposed_usage_start_at
proposed_usage_end_at
proposed_description
proposal_note
proposed_by
proposed_at
proposal_response
proposal_response_note
proposal_responded_by
proposal_responded_at
```

Gửi email/thông báo cho IC/Host.

Nếu chưa implement proposal token:

```text
Email chỉ có nút Xem chi tiết trong hệ thống.
```

### 7.2. Department Staff đề xuất thay đổi

Department Staff phải vào hệ thống.

Điều kiện:

```text
- Staff là assigned_to_user_id hiện tại.
- Item status IN (ASSIGNED, ACCEPTED) tùy flow hiện tại.
- Item chưa CANCELLED / REJECTED / DONE.
- Visit instance chưa CANCELLED / CLOSED.
- Chưa có proposal pending.
```

Khi gửi đề xuất:

```text
- Không ghi đè field gốc.
- Ghi proposed_*.
- proposal_note bắt buộc.
- proposed_by = Department Staff user id.
- proposed_at = NOW.
- status = CHANGE_PROPOSED.
```

Gửi email/thông báo cho IC/Host.

### 7.3. IC/Host xử lý đề xuất

IC/Host vào hệ thống để xử lý.

Hiển thị bắt buộc:

```text
- Thông tin yêu cầu gốc.
- Thông tin đề xuất.
- Người đề xuất.
- Thời điểm đề xuất.
- Lý do đề xuất.
```

IC/Host chấp nhận:

```text
proposal_response = ACCEPTED
proposal_responded_by = currentUserId
proposal_responded_at = NOW
```

Luồng đề xuất kết thúc.

IC/Host từ chối:

```text
proposal_response = REJECTED
proposal_response_note bắt buộc
proposal_responded_by = currentUserId
proposal_responded_at = NOW
```

Luồng đề xuất kết thúc.

Sau khi proposal đã có response:

```text
- Không còn nút chấp nhận/từ chối đề xuất.
- Token/email/link cũ nếu có không được xử lý lại.
```

---

## 8. Email action token guard theo status

Phải audit và bổ sung trong:

```text
ExecuteEmailActionCommandHandler
GetEmailActionInfoQueryHandler
PublicEmailActionsController nếu có logic liên quan
Email action token creation helper/service
```

### 8.1. Common guard

Khi user bấm email:

```text
1. Hash raw token.
2. Load token.
3. Nếu không có token -> INVALID.
4. Nếu result_status = INVALID -> INVALID.
5. Nếu expired -> EXPIRED.
6. Nếu result_status = ALREADY_RESPONDED / SUCCESS hoặc used_at != NULL -> ALREADY_RESPONDED.
7. Nếu PENDING -> load target thật từ DB.
8. Load parent visit instance nếu có.
9. Check parent status.
10. Check target current status.
11. Nếu hợp lệ mới update nghiệp vụ.
12. Nếu không hợp lệ thì không update nghiệp vụ.
```

Không được check `used_at != NULL` trước `result_status = INVALID`, vì token bị thu hồi có thể đã có `used_at`, nhưng message đúng là “không còn hiệu lực”.

### 8.2. Parent guard

Nếu parent `visit_request_campuses.status` thuộc:

```text
CANCELLED
CLOSED
```

thì mọi email action liên quan participant/logistics phải:

```text
result_status = INVALID
message = "Chuyến tiếp khách này đã bị hủy hoặc đã đóng, liên kết không còn hiệu lực."
```

Không update nghiệp vụ.

### 8.3. Participant token guard

Với:

```text
target_type = VISIT_PARTICIPANT
action_context = PARTICIPATION_RESPONSE
```

Chỉ cho xử lý khi:

```text
visit_participants.status = INVITED
```

Nếu:

```text
ACCEPTED -> ALREADY_RESPONDED
DECLINED -> ALREADY_RESPONDED
REMOVED -> INVALID
ASSIGNED -> INVALID nếu token là của lời mời cũ không còn phù hợp
```

Không bao giờ được:

```text
REMOVED -> ACCEPTED
REMOVED -> DECLINED
ACCEPTED -> DECLINED
DECLINED -> ACCEPTED
```

### 8.4. Logistics request token guard

Với:

```text
target_type = LOGISTICS_ITEM
action_context = LOGISTICS_REQUEST_RESPONSE
```

Chỉ cho xử lý khi:

```text
visit_logistics_items.status = REQUESTED
```

Nếu khác `REQUESTED`:

```text
Không update DB.
Trả INVALID hoặc ALREADY_RESPONDED tùy case.
```

Mapping gợi ý:

```text
RECEIVED / ACCEPTED -> ALREADY_RESPONDED
ASSIGNED / CHANGE_PROPOSED / IN_PROGRESS / READY -> INVALID
REJECTED / CANCELLED / DONE -> INVALID
```

### 8.5. Logistics assignee token guard

Với:

```text
target_type = LOGISTICS_ITEM
action_context = LOGISTICS_ASSIGNEE_RESPONSE
```

Chỉ cho xử lý khi:

```text
visit_logistics_items.status = ASSIGNED
AND visit_logistics_items.assigned_to_user_id = token.recipient_user_id
```

Nếu assignee không khớp:

```text
result_status = INVALID
message = "Bạn không còn là người phụ trách yêu cầu này."
```

Nếu status khác `ASSIGNED`:

```text
Không update DB.
```

Mapping gợi ý:

```text
ACCEPTED -> ALREADY_RESPONDED
REJECTED / CANCELLED / DONE -> INVALID
CHANGE_PROPOSED -> INVALID
```

### 8.6. Proposal token guard

Nếu `LOGISTICS_PROPOSAL_RESPONSE` chưa implement đầy đủ:

```text
- Không mint token.
- Không render nút proposal trong email.
- Handler gặp context này thì trả INVALID.
```

Nếu implement sau này:

```text
Chỉ hợp lệ khi:
status = CHANGE_PROPOSED
proposal_response IS NULL
proposed_at IS NOT NULL
```

---

## 9. Invalidate token khi trạng thái đổi ngoài email

Phải dùng helper hiện có hoặc tạo mới:

```csharp
EmailTokenInvalidationHelper.InvalidatePendingEmailActionTokensAsync(...)
```

Gọi helper khi:

```text
- Department Leader nhận/từ chối lời mời trong hệ thống.
- Department Leader nhận/từ chối logistics request trong hệ thống.
- Department Leader gán cấp dưới.
- Department Leader đổi assignee.
- Department Staff nhận/từ chối nhiệm vụ trong hệ thống.
- Department Leader gửi proposal.
- Department Staff gửi proposal.
- IC/Host chấp nhận/từ chối proposal.
- Participant bị REMOVED.
- Logistics item bị CANCELLED.
- Logistics item bị REJECTED.
- Logistics item DONE.
- Visit instance bị CANCELLED/CLOSED.
```

Token cũ phải chuyển sang:

```text
result_status = INVALID
used_at = NOW
result_message = lý do phù hợp
```

Nếu action đã được xử lý thành công bằng token thì set:

```text
result_status = SUCCESS
used_at = NOW
```

Các token sibling cùng `action_group_key` phải chuyển:

```text
result_status = ALREADY_RESPONDED
used_at = NOW
```

---

## 10. Frontend cần kiểm tra

### 10.1. Department Leader page

Khi Department Leader mở chi tiết lời mời/yêu cầu đang chờ, hiển thị theo quyền và status:

```text
- Chấp nhận
- Từ chối
- Gán nhân sự
- Đề xuất thay đổi
```

Điều kiện:

```text
- Gán nhân sự chỉ hiện cho Department Leader.
- Đề xuất thay đổi chỉ hiện khi status còn cho phép.
- Từ chối bắt buộc lý do.
- Đề xuất thay đổi bắt buộc proposal_note.
- Không hiện nút nếu item đã DONE / CANCELLED / REJECTED / CLOSED.
```

### 10.2. Department Staff page

Khi Staff được gán:

```text
- Chấp nhận nhiệm vụ
- Từ chối nhiệm vụ
- Đề xuất thay đổi nếu nghiệp vụ cho phép
```

Điều kiện:

```text
- Chỉ assigned_to_user_id hiện tại mới thấy nút.
- Không hiện nếu item DONE / CANCELLED / REJECTED.
- Từ chối bắt buộc lý do.
- Đề xuất bắt buộc proposal_note.
```

### 10.3. IC/Host page

Khi có proposal:

```text
- Hiển thị thông tin gốc.
- Hiển thị thông tin đề xuất.
- Hiển thị người đề xuất.
- Hiển thị lý do đề xuất.
- Có nút Chấp nhận đề xuất.
- Có nút Từ chối đề xuất, bắt buộc nhập lý do.
```

Sau khi xử lý:

```text
- Không còn nút accept/reject proposal.
- Hiển thị kết quả ACCEPTED/REJECTED.
```

---

## 11. Toast / notification

Mọi thao tác quan trọng phải có toast góc phải:

```text
- Chấp nhận thành công/thất bại.
- Từ chối thành công/thất bại.
- Gán nhân sự thành công/thất bại.
- Gửi đề xuất thành công/thất bại.
- IC/Host chấp nhận/từ chối proposal thành công/thất bại.
- Thiếu lý do từ chối.
- Thiếu lý do đề xuất.
- Link email không còn hiệu lực.
```

Inline validation vẫn giữ tại field, nhưng mỗi click lỗi phải có tối đa 1 toast chính, không spam.

---

## 12. SQL kiểm tra stale token

Tạo thêm section trong file `.md` và nếu cần tạo file SQL riêng:

```text
docs/database/scripts/check_stale_email_action_tokens.sql
```

Các query tối thiểu:

### 12.1. Participant stale tokens

```sql
SELECT
    t.email_action_token_id,
    t.action_context,
    t.target_type,
    t.target_id,
    t.intended_action,
    t.result_status,
    p.status AS participant_status
FROM email_action_tokens t
JOIN visit_participants p
    ON p.participant_id = t.target_id
WHERE t.target_type = 'VISIT_PARTICIPANT'
  AND t.result_status = 'PENDING'
  AND p.status IN ('ACCEPTED', 'DECLINED', 'REMOVED', 'ASSIGNED');
```

### 12.2. Logistics request token sai status

```sql
SELECT
    t.email_action_token_id,
    t.action_context,
    t.target_id,
    t.intended_action,
    t.result_status,
    l.status AS logistics_status
FROM email_action_tokens t
JOIN visit_logistics_items l
    ON l.logistics_item_id = t.target_id
WHERE t.target_type = 'LOGISTICS_ITEM'
  AND t.action_context = 'LOGISTICS_REQUEST_RESPONSE'
  AND t.result_status = 'PENDING'
  AND l.status <> 'REQUESTED';
```

### 12.3. Logistics assignee token sai status hoặc sai người

```sql
SELECT
    t.email_action_token_id,
    t.action_context,
    t.target_id,
    t.recipient_user_id,
    l.assigned_to_user_id,
    t.result_status,
    l.status AS logistics_status
FROM email_action_tokens t
JOIN visit_logistics_items l
    ON l.logistics_item_id = t.target_id
WHERE t.target_type = 'LOGISTICS_ITEM'
  AND t.action_context = 'LOGISTICS_ASSIGNEE_RESPONSE'
  AND t.result_status = 'PENDING'
  AND (
        l.status <> 'ASSIGNED'
        OR l.assigned_to_user_id <> t.recipient_user_id
      );
```

### 12.4. Proposal token stale nếu sau này có dùng

```sql
SELECT
    t.email_action_token_id,
    t.action_context,
    t.target_id,
    t.intended_action,
    t.result_status,
    l.status,
    l.proposal_response
FROM email_action_tokens t
JOIN visit_logistics_items l
    ON l.logistics_item_id = t.target_id
WHERE t.action_context = 'LOGISTICS_PROPOSAL_RESPONSE'
  AND t.result_status = 'PENDING'
  AND (
        l.status <> 'CHANGE_PROPOSED'
        OR l.proposal_response IS NOT NULL
      );
```

### 12.5. Parent cancelled/closed stale tokens

```sql
SELECT
    t.email_action_token_id,
    t.action_context,
    t.target_type,
    t.target_id,
    t.result_status,
    vrc.status AS campus_visit_status
FROM email_action_tokens t
LEFT JOIN visit_participants p
    ON t.target_type = 'VISIT_PARTICIPANT'
   AND p.participant_id = t.target_id
LEFT JOIN visit_logistics_items l
    ON t.target_type = 'LOGISTICS_ITEM'
   AND l.logistics_item_id = t.target_id
LEFT JOIN visit_request_campuses vrc
    ON vrc.visit_instance_id = COALESCE(p.visit_instance_id, l.visit_instance_id)
WHERE t.result_status = 'PENDING'
  AND vrc.status IN ('CANCELLED', 'CLOSED');
```

---

## 13. Test bắt buộc

Phải test bằng DB thật và API thật, không chỉ build.

### 13.1. Department Leader

```text
[ ] IC gửi logistics request cho Department Leader -> email có Chấp nhận/Từ chối/Xem chi tiết.
[ ] Email không có nút Gán nhân sự dạng public token.
[ ] Email không có nút Đề xuất thay đổi dạng public token.
[ ] Department Leader bấm Chấp nhận email -> chỉ thành công khi status REQUESTED.
[ ] Department Leader bấm Từ chối email -> bắt nhập lý do.
[ ] Department Leader vào hệ thống gán cấp dưới -> status ASSIGNED, gửi email cho Staff.
[ ] Department Leader vào hệ thống gửi proposal -> status CHANGE_PROPOSED, gửi email/thông báo cho IC/Host.
```

### 13.2. Department Staff

```text
[ ] Staff được gán -> nhận email có Chấp nhận nhiệm vụ/Từ chối nhiệm vụ/Xem chi tiết.
[ ] Staff bấm Chấp nhận -> chỉ thành công nếu assigned_to_user_id đúng.
[ ] Staff bấm Từ chối -> bắt nhập lý do.
[ ] Department Leader đổi assignee -> Staff cũ bấm email cũ -> INVALID.
[ ] Staff gửi proposal -> IC/Host thấy đề xuất.
```

### 13.3. IC/Host xử lý proposal

```text
[ ] IC/Host thấy thông tin gốc và thông tin đề xuất.
[ ] IC/Host chấp nhận proposal -> proposal_response = ACCEPTED.
[ ] IC/Host từ chối proposal -> bắt nhập lý do, proposal_response = REJECTED.
[ ] Sau khi xử lý proposal, không còn nút xử lý lại.
[ ] Email/link cũ nếu có không xử lý lại.
```

### 13.4. Token security

```text
[ ] Participant REMOVED -> bấm email cũ -> INVALID, DB không đổi.
[ ] Participant ACCEPTED -> bấm DECLINE email cũ -> ALREADY_RESPONDED, DB không đổi.
[ ] Logistics CANCELLED -> bấm email cũ -> INVALID, DB không đổi.
[ ] Logistics DONE -> bấm email cũ -> INVALID, DB không đổi.
[ ] Visit instance CANCELLED/CLOSED -> bấm mọi email cũ -> INVALID.
```

---

## 14. Nội dung bắt buộc trong file business rule Markdown

File:

```text
docs/business-rules/department-task-logistics-email-token-flow.md
```

Phải có các section:

```text
# Department Task / Logistics / Email Token Flow

## 1. Scope
## 2. Roles involved
## 3. Core business rules
## 4. Email button policy
## 5. Department Leader flow
## 6. Department Staff flow
## 7. IC/Host proposal response flow
## 8. Status matrix
## 9. Email action contexts
## 10. Token guard rules
## 11. Token invalidation rules
## 12. Frontend visibility rules
## 13. Toast/notification rules
## 14. SQL stale-token checks
## 15. Test checklist
## 16. Known limitations / pending confirmations
```

Trong status matrix phải có tối thiểu:

```text
- Participant invitation matrix
- Logistics request response matrix
- Logistics assignee response matrix
- Proposal response matrix nếu có
```

Trong Known limitations phải ghi rõ:

```text
- LOGISTICS_PROPOSAL_RESPONSE có được implement email token chưa.
- LOGISTICS_HANDOVER_SIGNATURE có dùng email token không.
- Khi Staff từ chối nhiệm vụ thì luồng kết thúc hay trả về Department Leader.
- Khi Department Leader nhận logistics trực tiếp thì status dùng RECEIVED hay ACCEPTED.
```

---

## 15. Gates bắt buộc

Sau khi sửa phải chạy:

```bash
dotnet build
npm run lint
npm run build
```

Nếu frontend dùng tsc riêng:

```bash
npx tsc --noEmit
```

Không báo hoàn tất nếu còn lỗi build/type.

---

## 16. Format báo cáo sau khi hoàn tất

Báo cáo theo format:

```text
1. Root cause / khoảng trống logic cũ
2. Luồng Department Leader đã triển khai
3. Luồng Department Staff đã triển khai
4. Luồng IC/Host xử lý proposal
5. Email nào có nút token, email nào chỉ có link vào hệ thống
6. Các action_context đang dùng
7. Token guard theo status đã bổ sung
8. Invalidate token khi status đổi đã bổ sung
9. File backend changed
10. File frontend changed
11. File Markdown đã tạo/cập nhật
12. SQL/query stale token
13. Test đã chạy
14. Case còn cần xác nhận nghiệp vụ
```
Bạn là Senior Full-stack Developer + Business Logic Reviewer cho dự án PEMS.

Nhiệm vụ: rà soát và cập nhật logic logistics theo 2 rule nghiệp vụ đã chốt:

1. SQL hiện tại không có status `RECEIVED`, vì vậy không được dùng `RECEIVED` trong backend/frontend logic.
2. Phân biệt rõ `REJECTED` và `DECLINED`:

   * `REJECTED` = Department Leader từ chối yêu cầu logistics ban đầu do IC/Host gửi sang.
   * `DECLINED` = Department Staff từ chối nhiệm vụ sau khi đã được Department Leader gán.

Làm trên code thật, không mock data, không sinh file rác, không phá UI nếu không cần.

# 1. Rule final về status logistics

SQL hiện tại của `visit_logistics_items.status` không có `RECEIVED`.

Các status hợp lệ đang dùng:

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

Tuyệt đối không dùng `RECEIVED` trong:

```text
- Backend enum
- Entity mapping
- Command handler
- Query handler
- DTO
- Validation
- Email action token guard
- Frontend type/status label/filter
- UI button visibility
- Markdown business rule
- SQL stale-token check
```

Nếu còn `RECEIVED` trong comment cũ thì chỉ được giữ nếu ghi rõ “removed/not used”. Không được còn trong logic chạy thật.

# 2. Rule Department Leader bấm Đồng ý

Khi IC/Host gửi yêu cầu logistics cho Department Leader:

```text
Initial status = REQUESTED
Email action context = LOGISTICS_REQUEST_RESPONSE
```

Department Leader bấm “Đồng ý” trong email hoặc hệ thống:

```text
REQUESTED -> ACCEPTED
```

Ý nghĩa:

```text
Department Leader / phòng ban nhận xử lý trực tiếp.
Không cần gán cấp dưới nữa.
```

Sau khi item đã `ACCEPTED`:

```text
- Không hiển thị nút Gán nhân sự.
- Không cho API gán nhân sự xử lý item đó.
- Không cho email request token cũ xử lý lại.
- Token “Từ chối” cùng action_group_key phải bị ALREADY_RESPONDED hoặc INVALID theo cơ chế hiện tại.
```

Nếu Department Leader muốn gán cấp dưới thì **không bấm Đồng ý**. Leader phải vào hệ thống và dùng nút “Gán nhân sự” khi item còn `REQUESTED`.

# 3. Rule Department Leader gán cấp dưới

Department Leader gán cấp dưới chỉ hợp lệ khi:

```text
status = REQUESTED
```

Kết quả:

```text
REQUESTED -> ASSIGNED
assigned_to_user_id = departmentStaffId
assigned_by = currentDepartmentLeaderId
assigned_at = NOW
```

Sau khi gán:

```text
- Gửi email mới cho Department Staff.
- Email action context = LOGISTICS_ASSIGNEE_RESPONSE.
- Email Staff có nút Chấp nhận nhiệm vụ / Từ chối nhiệm vụ / Xem chi tiết.
- Token request cũ của Department Leader phải được invalidate nếu không còn phù hợp.
```

Không cho gán nếu item đang ở:

```text
ACCEPTED
CHANGE_PROPOSED
IN_PROGRESS
DONE
REJECTED
DECLINED
CANCELLED
```

# 4. Rule phân biệt REJECTED và DECLINED

Phải cập nhật cố định như sau:

## 4.1. REJECTED

`REJECTED` dùng khi **Department Leader từ chối yêu cầu logistics ban đầu** do IC/Host gửi sang.

Flow:

```text
LOGISTICS_REQUEST_RESPONSE + DECLINE
REQUESTED -> REJECTED
```

Điều kiện:

```text
- action_context = LOGISTICS_REQUEST_RESPONSE
- intended_action = DECLINE
- current status = REQUESTED
```

Yêu cầu:

```text
- Từ chối bắt buộc nhập lý do.
- GET public email chỉ render form.
- POST mới update DB.
- Lưu lý do từ chối vào field response note phù hợp.
```

UI label:

```text
REJECTED = Phòng ban từ chối yêu cầu
```

## 4.2. DECLINED

`DECLINED` dùng khi **Department Staff từ chối nhiệm vụ** sau khi đã được Department Leader gán.

Flow:

```text
LOGISTICS_ASSIGNEE_RESPONSE + DECLINE
ASSIGNED -> DECLINED
```

Điều kiện:

```text
- action_context = LOGISTICS_ASSIGNEE_RESPONSE
- intended_action = DECLINE
- current status = ASSIGNED
- assigned_to_user_id = token.recipient_user_id
```

Yêu cầu:

```text
- Từ chối bắt buộc nhập lý do.
- GET public email chỉ render form.
- POST mới update DB.
- Lưu lý do vào assignee_response_note.
```

UI label:

```text
DECLINED = Nhân sự được gán từ chối nhiệm vụ
```

Không được dùng lẫn:

```text
Department Leader từ chối -> không dùng DECLINED.
Department Staff từ chối -> không dùng REJECTED.
```

# 5. Email action token guard cần cập nhật

## 5.1. LOGISTICS_REQUEST_RESPONSE

Chỉ dùng cho Department Leader phản hồi yêu cầu ban đầu.

Chỉ hợp lệ khi:

```text
status = REQUESTED
```

Mapping:

```text
ACCEPT  -> REQUESTED -> ACCEPTED
DECLINE -> REQUESTED -> REJECTED
```

Nếu status hiện tại khác `REQUESTED`:

```text
Không update DB.
Trả INVALID hoặc ALREADY_RESPONDED.
```

Gợi ý mapping lỗi:

```text
ACCEPTED -> ALREADY_RESPONDED
ASSIGNED -> INVALID, message: Yêu cầu đã được gán cho nhân sự xử lý.
CHANGE_PROPOSED -> INVALID, message: Yêu cầu đang có đề xuất thay đổi.
IN_PROGRESS -> INVALID
DONE -> INVALID
REJECTED -> ALREADY_RESPONDED hoặc INVALID
DECLINED -> INVALID
CANCELLED -> INVALID
```

## 5.2. LOGISTICS_ASSIGNEE_RESPONSE

Chỉ dùng cho Department Staff phản hồi nhiệm vụ được gán.

Chỉ hợp lệ khi:

```text
status = ASSIGNED
AND assigned_to_user_id = token.recipient_user_id
```

Mapping:

```text
ACCEPT  -> ASSIGNED -> ACCEPTED
DECLINE -> ASSIGNED -> DECLINED
```

Nếu `assigned_to_user_id` không khớp:

```text
result_status = INVALID
message = "Bạn không còn là người phụ trách yêu cầu này."
```

Nếu status hiện tại khác `ASSIGNED`:

```text
Không update DB.
Trả INVALID hoặc ALREADY_RESPONDED.
```

Gợi ý mapping lỗi:

```text
ACCEPTED -> ALREADY_RESPONDED
REJECTED -> INVALID
DECLINED -> ALREADY_RESPONDED hoặc INVALID
CHANGE_PROPOSED -> INVALID
DONE -> INVALID
CANCELLED -> INVALID
```

## 5.3. Parent guard

Nếu parent visit instance:

```text
CANCELLED
CLOSED
```

thì mọi token participant/logistics phải:

```text
result_status = INVALID
Không update DB.
message = "Chuyến tiếp khách này đã bị hủy hoặc đã đóng, liên kết không còn hiệu lực."
```

# 6. Button visibility cần đúng

## Department Leader nhìn item REQUESTED

Hiển thị:

```text
- Đồng ý
- Từ chối
- Gán nhân sự
- Đề xuất thay đổi
```

Trong email chỉ có public token:

```text
- Đồng ý
- Từ chối
```

Các nút sau bắt buộc vào hệ thống:

```text
- Gán nhân sự
- Đề xuất thay đổi
```

## Department Leader nhìn item ACCEPTED

Không hiển thị:

```text
- Đồng ý
- Từ chối
- Gán nhân sự
```

Vì Department Leader đã nhận xử lý trực tiếp.

## Department Leader nhìn item ASSIGNED

Không hiển thị:

```text
- Đồng ý
- Từ chối request cũ
```

Chỉ hiển thị thông tin người được gán và các action hợp lệ theo flow hiện tại.

## Department Staff nhìn item ASSIGNED

Chỉ staff đang được gán mới thấy:

```text
- Chấp nhận nhiệm vụ
- Từ chối nhiệm vụ
- Đề xuất thay đổi nếu nghiệp vụ cho phép
```

Không hiện nếu:

```text
DONE
CANCELLED
REJECTED
DECLINED
```

# 7. Invalidate token khi status đổi

Phải kiểm tra hoặc bổ sung gọi helper invalidate khi:

```text
- Department Leader bấm Đồng ý request: invalidate token Từ chối cùng group.
- Department Leader bấm Từ chối request: invalidate token Đồng ý cùng group.
- Department Leader gán cấp dưới: invalidate token request cũ nếu không còn phù hợp.
- Department Staff chấp nhận nhiệm vụ: invalidate token Từ chối cùng group.
- Department Staff từ chối nhiệm vụ: invalidate token Chấp nhận cùng group.
- Department Leader đổi assignee: invalidate token của assignee cũ.
- Item CANCELLED / REJECTED / DECLINED / DONE: invalidate toàn bộ pending token của item.
- Parent visit instance CANCELLED / CLOSED: invalidate toàn bộ pending token participant/logistics thuộc instance.
```

# 8. Audit toàn bộ code

Tìm và cập nhật các file liên quan:

```text
Backend:
- LogisticsItemStatus enum
- ExecuteEmailActionCommandHandler
- GetEmailActionInfoQueryHandler
- PrepareVisitLogisticsCommandHandler
- AssignRequestAssigneeCommand
- CancelVisitLogisticsItemCommandHandler
- ProposeRequestChangeCommand
- ConfirmTheChangeProposalCommand
- EmailComposition / action block
- Email token creation service/helper
- DTO / Query Handler trả logistics item
- Validation rules

Frontend:
- TypeScript type/enum logistics status
- status label/badge
- filter theo status
- button visibility
- LogisticsRequestSection
- Department TaskDetail
- SharedDashboardView
- departmentReceptionTasksApi
- delegationsApi

Docs:
- docs/business-rules/department-task-logistics-email-token-flow.md
- stale token SQL section
```

# 9. SQL stale token check cần cập nhật

Cập nhật hoặc tạo file SQL check nếu chưa có:

```text
docs/database/scripts/check_stale_email_action_tokens.sql
```

## 9.1. Không được có RECEIVED trong logic DB

```sql
SELECT COUNT(*) AS received_status_count
FROM visit_logistics_items
WHERE status = 'RECEIVED';
```

Nếu query lỗi vì enum không có `RECEIVED`, đó là đúng với SQL hiện tại. Nếu query chạy và có dữ liệu, DB đang lệch version.

## 9.2. Request token sai status

```sql
SELECT
    t.email_action_token_id,
    t.action_context,
    t.target_id,
    t.intended_action,
    t.result_status,
    l.status AS logistics_status
FROM email_action_tokens t
JOIN visit_logistics_items l
    ON l.logistics_item_id = t.target_id
WHERE t.target_type = 'LOGISTICS_ITEM'
  AND t.action_context = 'LOGISTICS_REQUEST_RESPONSE'
  AND t.result_status = 'PENDING'
  AND l.status <> 'REQUESTED';
```

## 9.3. Assignee token sai status hoặc sai người

```sql
SELECT
    t.email_action_token_id,
    t.action_context,
    t.target_id,
    t.recipient_user_id,
    l.assigned_to_user_id,
    t.result_status,
    l.status AS logistics_status
FROM email_action_tokens t
JOIN visit_logistics_items l
    ON l.logistics_item_id = t.target_id
WHERE t.target_type = 'LOGISTICS_ITEM'
  AND t.action_context = 'LOGISTICS_ASSIGNEE_RESPONSE'
  AND t.result_status = 'PENDING'
  AND (
        l.status <> 'ASSIGNED'
        OR l.assigned_to_user_id <> t.recipient_user_id
      );
```

# 10. Markdown business rule cần cập nhật

Cập nhật file:

```text
docs/business-rules/department-task-logistics-email-token-flow.md
```

Phải ghi rõ:

```text
- SQL hiện tại không có RECEIVED.
- Department Leader bấm Đồng ý: REQUESTED -> ACCEPTED.
- Sau ACCEPTED không cần gán cấp dưới.
- Muốn gán cấp dưới thì Department Leader phải vào hệ thống khi item còn REQUESTED và chuyển REQUESTED -> ASSIGNED.
- REJECTED = Department Leader từ chối request ban đầu.
- DECLINED = Department Staff từ chối nhiệm vụ được gán.
- LOGISTICS_REQUEST_RESPONSE + DECLINE -> REJECTED.
- LOGISTICS_ASSIGNEE_RESPONSE + DECLINE -> DECLINED.
- Gán nhân sự và đề xuất thay đổi không dùng public email token.
```

# 11. Test bắt buộc

Chạy test runtime/API thật nếu có thể.

Checklist:

```text
[ ] Không còn RECEIVED trong logic backend/frontend.
[ ] IC gửi logistics request -> status REQUESTED.
[ ] Email Department Leader có Đồng ý/Từ chối/Xem chi tiết.
[ ] Email không có nút Gán nhân sự public.
[ ] Email không có nút Đề xuất public.
[ ] Department Leader bấm Đồng ý -> REQUESTED -> ACCEPTED.
[ ] Sau ACCEPTED không còn cho gán cấp dưới.
[ ] Department Leader bấm Từ chối -> REQUESTED -> REJECTED.
[ ] Department Leader ở status REQUESTED bấm Gán nhân sự trong hệ thống -> REQUESTED -> ASSIGNED.
[ ] Staff nhận email assignee.
[ ] Staff bấm Chấp nhận -> ASSIGNED -> ACCEPTED.
[ ] Staff bấm Từ chối -> ASSIGNED -> DECLINED.
[ ] Đổi assignee -> Staff cũ bấm email cũ -> INVALID.
[ ] Cancel item -> mọi token cũ -> INVALID.
[ ] Parent CANCELLED/CLOSED -> mọi token logistics cũ -> INVALID.
```

# 12. Gates bắt buộc

Sau khi sửa chạy:

```bash
dotnet build
npm run lint
npm run build
```

Nếu frontend có TypeScript check riêng:

```bash
npx tsc --noEmit
```

Không báo hoàn tất nếu còn lỗi.

# 13. Báo cáo sau khi làm

Báo cáo theo format:

```text
1. Đã xác nhận SQL không có RECEIVED như thế nào
2. Những chỗ đã xóa/sửa RECEIVED trong backend
3. Những chỗ đã xóa/sửa RECEIVED trong frontend
4. Rule final Department Leader Đồng ý = REQUESTED -> ACCEPTED
5. Rule final Department Leader Từ chối = REQUESTED -> REJECTED
6. Rule final Gán cấp dưới = REQUESTED -> ASSIGNED
7. Rule final Department Staff Chấp nhận = ASSIGNED -> ACCEPTED
8. Rule final Department Staff Từ chối = ASSIGNED -> DECLINED
9. Token guard LOGISTICS_REQUEST_RESPONSE
10. Token guard LOGISTICS_ASSIGNEE_RESPONSE
11. Invalidate token khi status đổi
12. Markdown đã cập nhật
13. SQL stale token đã cập nhật
14. Test đã chạy
15. Case còn cần xác nhận
```
