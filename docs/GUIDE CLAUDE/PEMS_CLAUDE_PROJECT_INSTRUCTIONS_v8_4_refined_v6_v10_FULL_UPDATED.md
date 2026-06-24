# PEMS — CLAUDE PROJECT INSTRUCTIONS v8.4 refined v6 / v10 FULL UPDATED

> File này dùng để dán vào **Claude Project Instructions** hoặc đặt trong project dưới dạng:
>
> ```text
> .claude/CLAUDE.md
> ```
>
> Phiên bản này là bản **FULL UPDATED theo SQL v10**. Nội dung cũ v8.4 refined v6 được giữ nguyên đầy đủ ở **PHẦN B** để đối chiếu lịch sử. Khi có mâu thuẫn giữa phần v10 và nội dung cũ, **luôn ưu tiên PHẦN A — V10 CURRENT INSTRUCTIONS**.

---

# PHẦN A — V10 CURRENT INSTRUCTIONS / NỘI DUNG CHUẨN HIỆN TẠI

## A0. Kết luận: file này có cần cập nhật không?

Có. File cũ đang là `PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_FULL_UPDATED.md`, trong khi SQL hiện tại đã chuyển sang bản v10:

```text
pems_full_create_manual_wide_coverage_seed_v8_4_refined_v6_v10_clean_logistics_handover_fields.sql
```

Các thay đổi SQL v10 ảnh hưởng trực tiếp tới Claude/AI Agent khi code:

```text
1. Schema tăng từ 47 bảng lên 49 bảng.
2. Tổng field tăng từ 694 lên 719 field.
3. FAQ bỏ language_code và đổi faq_type.
4. Partner thêm owner_campus_id để Staff Leader duyệt đúng campus.
5. Logistics handover/signature tách sang bảng mới visit_logistics_item_handovers.
6. visit_logistics_items bỏ toàn bộ field ký cũ.
7. Email action qua nút bấm dùng bảng mới email_action_tokens.
8. Không làm inbox/mail nhận thật trong v10.
9. Không hỗ trợ chuyển nhiệm vụ logistics từ người này sang người khác.
```

Vì vậy, Claude Project Instructions phải được cập nhật để AI Agent không code theo schema cũ, không dùng field đã bị xóa và không tự thêm inbox/assignment-transfer ngoài scope.

---

## A1. Quy tắc ưu tiên tuyệt đối sau SQL v10

Khi làm việc với PEMS, nếu có mâu thuẫn giữa file này, tài liệu cũ, comment cũ, seed cũ hoặc code cũ, Claude phải ưu tiên theo thứ tự:

```text
1. DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
2. pems_full_create_manual_wide_coverage_seed_v8_4_refined_v6_v10_clean_logistics_handover_fields.sql
3. PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
4. PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
5. PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
6. VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
7. PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_v10_FULL_UPDATED.md
8. PEMS_v8_4_refined_v6_v10_FULL_SQL_TABLE_FIELD_DICTIONARY.docx
9. Code backend/frontend hiện tại
10. Tài liệu legacy chỉ dùng để đối chiếu, không dùng làm chuẩn code nếu mâu thuẫn
```

Rule quan trọng:

```text
- SQL/schema v10 là nguồn chuẩn cho bảng, cột, enum, constraint, foreign key.
- File canonical v10 là nguồn chuẩn cho business flow.
- Không tự bịa field, enum, status, permission code, route, bảng hoặc role.
- Không sửa code theo flow cũ nếu canonical v10 đã override.
- Không báo hoàn thành nếu chưa build/test hoặc chưa nói rõ lý do không chạy được.
- Không tái tạo dynamic permissions table.
- Không thêm inbox email thật nếu người dùng chưa chốt phase mới.
- Không thêm bảng chuyển nhiệm vụ logistics.
```

---

## A2. Trạng thái schema v10

```text
Database: MySQL 8
Style: database-first / fresh create-only SQL
Schema version: v8.4 refined v6 v10 clean logistics handover fields
Base tables: 49
Total fields: 719
Dynamic permission tables: removed
permissions table: không có
role_permissions table: không có
```

Bảng mới trong v10:

```text
1. visit_logistics_item_handovers
2. email_action_tokens
```

Bảng thay đổi trong v10:

```text
1. faqs
2. partners
3. visit_logistics_items
```

Bảng không được tự thêm trong v10:

```text
email_threads
email_messages
email_message_recipients
visit_logistics_assignment_logs
partner_review_logs
partner_approval_requests
permissions
role_permissions
```

---

## A3. Vai trò của Claude khi code PEMS

Claude phải làm việc như:

```text
Senior Full-stack Architect
Senior .NET 8 Clean Architecture Developer
Senior React TypeScript Engineer
Database-first MySQL Engineer
Security / Fixed Policy / Scope Reviewer
Enterprise UI/UX Dashboard Reviewer
QA / Seed Data Consistency Reviewer
Schema v10 Alignment Reviewer
```

Mọi task cần được kiểm tra theo chuỗi đồng bộ:

```text
SQL v10
→ Entity
→ Enum / constants
→ EF Configuration
→ DbContext
→ DTO / Request / Response
→ Validator
→ Handler / Service
→ Controller / Route
→ Scope / authorization policy
→ Notification / Email
→ Frontend type
→ Frontend API service
→ Frontend page/modal/action
→ Seed/test data
→ Build backend
→ Build frontend
→ Manual verification
→ Docs/changelog
```

Không được báo hoàn thành nếu chỉ sửa một layer.

---

## A4. Database-first / manual SQL rule sau v10

PEMS vẫn theo hướng database-first.

Không được:

```text
- Không tự chạy auto migration bừa.
- Không đổi schema bằng code nếu chưa có SQL patch hoặc chưa được yêu cầu.
- Không tự tạo enum/status/field/table nếu SQL v10 chưa có.
- Không xóa destructive dữ liệu production nếu chưa có yêu cầu rõ.
- Không seed runtime trong Program.cs nếu project đã chốt manual seed.
- Không dùng mock data khi UC cần DB thật.
- Không dùng INSERT IGNORE để che lỗi seed.
- Không tái tạo permissions / role_permissions.
```

Nếu cần thay đổi database sau v10:

```text
- Tạo patch SQL riêng trong database/scripts/.
- Ghi rõ patch này chạy sau full-create v10.
- Không dùng patch thay cho việc cập nhật entity/DTO/frontend.
- Nếu user yêu cầu fresh-create, phải tạo file full CREATE TABLE mới, không dùng ALTER TABLE.
```

---

## A5. Role/SubRole canonical giữ nguyên sau v10

SQL v10 không đổi role/subRole.

Role runtime hợp lệ:

```text
ADMIN
HO
STAFF
DEPARTMENT
STUDENT
VISITOR
```

SubRole runtime hợp lệ:

```text
LEADER
STAFF
NULL
```

Effective role:

| Effective role | role_code | sub_role | Ghi chú |
|---|---|---|---|
| Admin | ADMIN | NULL | Quản trị kỹ thuật/config/audit/account theo policy |
| HO | HO | NULL | Xử lý multi-campus |
| Staff Leader | STAFF | LEADER | Trưởng IC campus |
| IC Staff | STAFF | STAFF | Host/support |
| Department Leader | DEPARTMENT | LEADER | Trưởng phòng ban GENERAL |
| Department Staff | DEPARTMENT | STAFF | Nhân sự phòng ban GENERAL |
| Student | STUDENT | NULL | Sinh viên hỗ trợ |
| Visitor | VISITOR | NULL | Khách ngoài |

Không dùng runtime:

```text
DEPT
STAFF_LEADER as role_code
DEPARTMENT_LEADER as role_code
DEPT_LEADER
STAFF_L
STAFF_P
DEPT_L
DEPT_P
LEADER as role_code
```

Các giá trị như `STAFF_LEADER`, `AUTO_STAFF_LEADER`, `DEPT_SUPPORT` nếu còn xuất hiện trong schema/logic là nhãn nghiệp vụ/audit/participant role, không phải `role_code`.

---

## A6. Dynamic permissions vẫn bị loại bỏ

Không được query hoặc tạo lại:

```text
permissions
role_permissions
permission_code
permission_level
runtime DB permission matrix
```

Authorization phải dùng fixed policy:

```text
role_code
sub_role / effectiveRole
primary_campus_id
department_id
owner_campus_id
ownership
visitor_user_id
coordinator_user_id
current_host_user_id
participant relationship
logistics assignment
record status
```

Frontend chỉ ẩn/hiện menu, route, button và tránh gọi API sai. Backend vẫn là lớp quyết định cuối cùng.

---

## A7. Thay đổi FAQ trong v10

### A7.1 Schema mới

`faqs` đã bỏ:

```text
language_code
```

`faqs.faq_type` chỉ còn các giá trị:

```text
ACCOUNT_ACCESS
VISIT_REQUEST
DELEGATION_MANAGEMENT
LOGISTICS_RESOURCE
DOCUMENT_MEDIA
NOTIFICATION_EMAIL
OTHER
```

### A7.2 Backend phải cập nhật

Xóa khỏi entity/DTO/request/response/query:

```text
LanguageCode
languageCode
language_code
```

Cập nhật constants/enum:

```csharp
public static class FaqTypes
{
    public const string AccountAccess = "ACCOUNT_ACCESS";
    public const string VisitRequest = "VISIT_REQUEST";
    public const string DelegationManagement = "DELEGATION_MANAGEMENT";
    public const string LogisticsResource = "LOGISTICS_RESOURCE";
    public const string DocumentMedia = "DOCUMENT_MEDIA";
    public const string NotificationEmail = "NOTIFICATION_EMAIL";
    public const string Other = "OTHER";
}
```

Validator chỉ cho phép các type trên.

### A7.3 Frontend phải cập nhật

Không còn dropdown ngôn ngữ FAQ.

Không gửi/nhận:

```text
languageCode
```

FAQ type options:

```ts
export const FAQ_TYPES = {
  ACCOUNT_ACCESS: 'ACCOUNT_ACCESS',
  VISIT_REQUEST: 'VISIT_REQUEST',
  DELEGATION_MANAGEMENT: 'DELEGATION_MANAGEMENT',
  LOGISTICS_RESOURCE: 'LOGISTICS_RESOURCE',
  DOCUMENT_MEDIA: 'DOCUMENT_MEDIA',
  NOTIFICATION_EMAIL: 'NOTIFICATION_EMAIL',
  OTHER: 'OTHER',
} as const;
```

Không dùng type cũ:

```text
PROGRAM
TUITION_FEE
VISA
DORMITORY
SECURITY
LOGISTICS
```

Nếu gặp data/label cũ trong code, phải migrate mapping sang type mới hoặc xóa khỏi runtime.

---

## A8. Thay đổi Partner trong v10

### A8.1 Schema mới

`partners` thêm:

```text
owner_campus_id BIGINT UNSIGNED NOT NULL
FK -> campuses(campus_id)
```

Mục đích:

```text
Staff Leader duyệt partner đúng campus, không duyệt partner campus khác.
```

### A8.2 Rule tạo partner

Khi Staff/IC Staff tạo partner:

```text
owner_campus_id = currentUser.primary_campus_id
```

Frontend không được tự truyền `owner_campus_id` từ form public/internal nếu không có rule rõ ràng, để tránh giả mạo campus.

### A8.3 Rule duyệt partner

Staff Leader chỉ được xem/duyệt/từ chối partner khi:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
partners.owner_campus_id = currentUser.primary_campus_id
partners.profile_status = PENDING_APPROVAL
```

Không suy luận campus từ `created_by` nếu đã có `owner_campus_id`.

### A8.4 Không thêm lịch sử duyệt nhiều lần trong v10

Không tạo thêm:

```text
partner_review_logs
partner_approval_requests
```

Nếu cần lịch sử sau này, phải hỏi lại user và patch schema riêng.

---

## A9. Thay đổi Logistics trong v10

### A9.1 `visit_logistics_items` chỉ còn lưu workflow

`visit_logistics_items` dùng cho:

```text
request
receive
assign
assignee response
negotiation / proposed change
proposal response
due/completion
status
audit
```

### A9.2 Các field đã bị xóa khỏi `visit_logistics_items`

Không được dùng trong entity, DTO, query, response, UI:

```text
handover_confirmed_by
handover_confirmed_at
handover_note
service_report_signed_by
service_report_signed_at
service_report_file_id
```

Nếu code còn các property tương ứng, phải xóa:

```csharp
HandoverConfirmedBy
HandoverConfirmedAt
HandoverNote
ServiceReportSignedBy
ServiceReportSignedAt
ServiceReportFileId
```

### A9.3 Bảng mới `visit_logistics_item_handovers`

Bảng mới lưu ký mượn/ký trả:

```text
visit_logistics_item_handovers
```

Các field chính:

```text
handover_id
logistics_item_id
handover_type
borrower_signed_by
borrower_signed_at
provider_signed_by
provider_signed_at
item_condition
condition_note
attachment_file_id
created_at
created_by
```

Enum:

```text
handover_type: BORROW, RETURN
item_condition: GOOD, DAMAGED, MISSING, OTHER
```

Ý nghĩa chữ ký:

| handover_type | borrower_signed_by/at | provider_signed_by/at |
|---|---|---|
| BORROW | Bên mượn ký nhận | Bên cho mượn ký bàn giao |
| RETURN | Bên mượn ký trả | Bên cho mượn ký nhận lại |

Constraint nghiệp vụ:

```text
Mỗi logistics_item_id tối đa có 1 row BORROW và 1 row RETURN.
unique(logistics_item_id, handover_type)
```

### A9.4 Backend entity/config cần thêm

Thêm entity:

```csharp
public class VisitLogisticsItemHandover
{
    public ulong HandoverId { get; set; }
    public ulong LogisticsItemId { get; set; }
    public string HandoverType { get; set; } = default!;
    public ulong? BorrowerSignedBy { get; set; }
    public DateTime? BorrowerSignedAt { get; set; }
    public ulong? ProviderSignedBy { get; set; }
    public DateTime? ProviderSignedAt { get; set; }
    public string? ItemCondition { get; set; }
    public string? ConditionNote { get; set; }
    public ulong? AttachmentFileId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ulong? CreatedBy { get; set; }
}
```

Thêm `DbSet`:

```csharp
public DbSet<VisitLogisticsItemHandover> VisitLogisticsItemHandovers => Set<VisitLogisticsItemHandover>();
```

Thêm constants:

```csharp
public static class LogisticsHandoverTypes
{
    public const string Borrow = "BORROW";
    public const string Return = "RETURN";
}

public static class LogisticsItemConditions
{
    public const string Good = "GOOD";
    public const string Damaged = "DAMAGED";
    public const string Missing = "MISSING";
    public const string Other = "OTHER";
}
```

### A9.5 Không chuyển nhiệm vụ logistics

Không triển khai transfer/reassign.

Backend phải chặn:

```text
Nếu visit_logistics_items.assigned_to_user_id đã có giá trị
và request muốn đổi sang user khác
→ trả 409 Conflict hoặc BusinessRuleException
→ message: "Nhiệm vụ đã được phân công, không thể chuyển sang người khác."
```

Không thêm bảng:

```text
visit_logistics_assignment_logs
```

Nếu gán nhầm, xử lý bằng từ chối/hủy item hiện tại hoặc tạo logistics item mới theo nghiệp vụ, không gọi là chuyển nhiệm vụ.

---

## A10. Email scope trong v10

### A10.1 Không làm inbox/mail nhận thật

Trong v10 không có:

```text
email_threads
email_messages
email_message_recipients
```

Không đọc Gmail inbox/mailbox ở phase này.

Không code các tab sau nếu chưa có yêu cầu mới:

```text
Inbox thật
Mail nhận thật
Thread gửi/nhận đầy đủ
Reply email tự do đọc từ mailbox
Gmail API read-only sync
IMAP inbox sync
Webhook nhận Gmail message
```

### A10.2 Email module trong v10 gồm

```text
email_templates
sent_emails
sent_email_recipients
email_action_tokens
```

Ý nghĩa:

| Bảng | Vai trò |
|---|---|
| `email_templates` | Template email |
| `sent_emails` | Lịch sử email gửi đi / outbox snapshot |
| `sent_email_recipients` | Tracking người nhận và delivery status |
| `email_action_tokens` | Token một lần cho nút bấm trong email |

Màn Email Management nên hiển thị:

```text
Email đã gửi
Trạng thái gửi
Trạng thái từng người nhận
Trạng thái phản hồi qua nút email
```

Không gọi là inbox nếu chưa có sync mail thật.

---

## A11. Bảng mới `email_action_tokens`

### A11.1 Mục đích

Dùng để xử lý các nút trong email mà không cần đăng nhập:

```text
Xác nhận
Từ chối
Thương lượng
Chấp nhận đề xuất
Từ chối đề xuất
Ký nhận
Ký trả
```

Người nhận bấm link trong email, backend validate token rồi update bảng nghiệp vụ tương ứng.

### A11.2 Field chính

```text
email_action_token_id
token_hash
action_group_key
action_context
target_type
target_id
intended_action
recipient_user_id
recipient_email
sent_email_id
sent_email_recipient_id
expires_at
used_at
used_action
result_status
result_message
used_ip
used_user_agent
created_at
```

### A11.3 Enum hợp lệ

`action_context`:

```text
PARTICIPATION_RESPONSE
LOGISTICS_ASSIGNEE_RESPONSE
LOGISTICS_NEGOTIATION
LOGISTICS_PROPOSAL_RESPONSE
LOGISTICS_HANDOVER_SIGNATURE
```

`target_type`:

```text
VISIT_PARTICIPANT
LOGISTICS_ITEM
LOGISTICS_HANDOVER
```

`intended_action`:

```text
ACCEPT
DECLINE
NEGOTIATE
APPROVE_PROPOSAL
REJECT_PROPOSAL
CONFIRM_BORROW
CONFIRM_RETURN
```

`result_status`:

```text
PENDING
SUCCESS
ALREADY_RESPONDED
EXPIRED
INVALID
FAILED
```

### A11.4 Backend constants cần có

```csharp
public static class EmailActionContexts
{
    public const string ParticipationResponse = "PARTICIPATION_RESPONSE";
    public const string LogisticsAssigneeResponse = "LOGISTICS_ASSIGNEE_RESPONSE";
    public const string LogisticsNegotiation = "LOGISTICS_NEGOTIATION";
    public const string LogisticsProposalResponse = "LOGISTICS_PROPOSAL_RESPONSE";
    public const string LogisticsHandoverSignature = "LOGISTICS_HANDOVER_SIGNATURE";
}

public static class EmailActionTargetTypes
{
    public const string VisitParticipant = "VISIT_PARTICIPANT";
    public const string LogisticsItem = "LOGISTICS_ITEM";
    public const string LogisticsHandover = "LOGISTICS_HANDOVER";
}

public static class EmailActionIntendedActions
{
    public const string Accept = "ACCEPT";
    public const string Decline = "DECLINE";
    public const string Negotiate = "NEGOTIATE";
    public const string ApproveProposal = "APPROVE_PROPOSAL";
    public const string RejectProposal = "REJECT_PROPOSAL";
    public const string ConfirmBorrow = "CONFIRM_BORROW";
    public const string ConfirmReturn = "CONFIRM_RETURN";
}

public static class EmailActionResultStatuses
{
    public const string Pending = "PENDING";
    public const string Success = "SUCCESS";
    public const string AlreadyResponded = "ALREADY_RESPONDED";
    public const string Expired = "EXPIRED";
    public const string Invalid = "INVALID";
    public const string Failed = "FAILED";
}
```

### A11.5 Security rule

Bắt buộc:

```text
1. Không lưu token raw trong DB.
2. Chỉ lưu token_hash.
3. Link email chứa raw token ngẫu nhiên đủ mạnh.
4. Backend hash raw token trước khi lookup.
5. Token phải có expires_at.
6. Token chỉ dùng một lần.
7. Ghi used_at, used_action, used_ip, used_user_agent.
8. Nếu token hết hạn: result_status = EXPIRED.
9. Nếu token không hợp lệ: trả INVALID, không lộ target.
10. Nếu nghiệp vụ đã được phản hồi: trả ALREADY_RESPONDED, không update lần hai.
```

Khuyến nghị an toàn:

```text
GET /public/email-actions/{token}
→ chỉ preview action/status và hiển thị nút xác nhận cuối.

POST /public/email-actions/{token}
→ mới update nghiệp vụ.
```

Lý do: tránh email security scanner tự mở link GET và làm thay user.

Nếu người dùng yêu cầu "bấm một phát là lưu", vẫn phải đảm bảo one-time token, check status hiện tại và audit IP/User-Agent.

---

## A12. Public email action endpoint rule

Endpoint gợi ý:

```text
GET  /public/email-actions/{token}
POST /public/email-actions/{token}

GET  /public/email-actions/{token}/negotiate
POST /public/email-actions/{token}/negotiate
```

Controller:

```text
- Có thể AllowAnonymous.
- Không xử lý business logic trong Controller.
- Controller chỉ lấy token/body/IP/User-Agent rồi gọi MediatR.
```

Handler phải:

```text
[ ] Hash raw token rồi tìm email_action_tokens.token_hash.
[ ] Check token tồn tại.
[ ] Check expires_at.
[ ] Check used_at/result_status.
[ ] Load target theo target_type + target_id.
[ ] Validate target hiện tại còn cho phép action không.
[ ] Validate recipient_user_id/recipient_email nếu có.
[ ] Update bảng nghiệp vụ trong transaction.
[ ] Set used_at, used_action, result_status, result_message.
[ ] Set used_ip, used_user_agent.
[ ] Nếu đã trả lời rồi, set/return ALREADY_RESPONDED và không update nghiệp vụ lần hai.
```

Target handling:

| target_type | Bảng nghiệp vụ | Action |
|---|---|---|
| VISIT_PARTICIPANT | `visit_participants` | ACCEPT/DECLINE participation |
| LOGISTICS_ITEM | `visit_logistics_items` | ACCEPT/DECLINE/NEGOTIATE/APPROVE_PROPOSAL/REJECT_PROPOSAL |
| LOGISTICS_HANDOVER | `visit_logistics_item_handovers` | CONFIRM_BORROW/CONFIRM_RETURN |

---

## A13. Participation via email action

Khi gửi email mời tham gia đoàn:

```text
visit_participants.status = INVITED
```

Tạo token:

```text
action_context = PARTICIPATION_RESPONSE
target_type = VISIT_PARTICIPANT
target_id = visit_participant_id
intended_action = ACCEPT hoặc DECLINE
action_group_key = PARTICIPATION:{visit_participant_id}:{recipient_email}
```

Khi ACCEPT:

```text
visit_participants.status = ACCEPTED
visit_participants.responded_at = NOW()
email_action_tokens.used_at = NOW()
email_action_tokens.used_action = ACCEPT
email_action_tokens.result_status = SUCCESS
```

Khi DECLINE:

```text
visit_participants.status = DECLINED
visit_participants.responded_at = NOW()
email_action_tokens.used_at = NOW()
email_action_tokens.used_action = DECLINE
email_action_tokens.result_status = SUCCESS
```

Nếu bấm lại hoặc bấm link ngược sau khi đã trả lời:

```text
Không update visit_participants lần hai.
Return result_status = ALREADY_RESPONDED.
Message: "Bạn đã trả lời lời mời này rồi."
```

---

## A14. Logistics assignee response via email action

Khi Department Leader giao logistics item cho Department Staff:

```text
visit_logistics_items.assigned_to_user_id = department staff
visit_logistics_items.status = ASSIGNED
```

Tạo token:

```text
action_context = LOGISTICS_ASSIGNEE_RESPONSE
target_type = LOGISTICS_ITEM
target_id = logistics_item_id
intended_action = ACCEPT hoặc DECLINE hoặc NEGOTIATE
action_group_key = LOGISTICS_ASSIGNEE:{logistics_item_id}:{recipient_email}
```

ACCEPT:

```text
visit_logistics_items.status = ACCEPTED
visit_logistics_items.assignee_accepted_at = NOW()
```

DECLINE:

```text
visit_logistics_items.status = REJECTED
visit_logistics_items.assignee_response_note = note nếu có
decision_note = reason nếu nghiệp vụ cần
```

NEGOTIATE:

```text
Mở public negotiate form.
Không update trực tiếp bằng GET.
Submit form mới update proposal fields.
```

---

## A15. Logistics negotiation / proposal flow

Khi người nhận chọn thương lượng:

```text
GET /public/email-actions/{token}/negotiate
→ hiển thị form nhập:
   - proposed_quantity
   - proposed_usage_start_at
   - proposed_usage_end_at
   - proposed_description
   - proposal_note
```

Khi submit:

```text
visit_logistics_items.status = CHANGE_PROPOSED
visit_logistics_items.proposed_by = recipient_user_id nếu có
visit_logistics_items.proposed_at = NOW()
visit_logistics_items.proposed_quantity = form.proposed_quantity
visit_logistics_items.proposed_usage_start_at = form.proposed_usage_start_at
visit_logistics_items.proposed_usage_end_at = form.proposed_usage_end_at
visit_logistics_items.proposed_description = form.proposed_description
visit_logistics_items.proposal_note = form.proposal_note
email_action_tokens.result_status = SUCCESS
```

Bên yêu cầu phản hồi đề xuất:

```text
action_context = LOGISTICS_PROPOSAL_RESPONSE
intended_action = APPROVE_PROPOSAL hoặc REJECT_PROPOSAL
```

APPROVE_PROPOSAL:

```text
proposal_response = ACCEPTED
proposal_responded_by = current/action user
proposal_responded_at = NOW()
proposal_response_note = note nếu có
```

REJECT_PROPOSAL:

```text
proposal_response = REJECTED
proposal_responded_by = current/action user
proposal_responded_at = NOW()
proposal_response_note = note bắt buộc nếu nghiệp vụ yêu cầu
```

---

## A16. Logistics handover signing via email action

Ký mượn/ký trả phải dùng `visit_logistics_item_handovers`.

### A16.1 Borrow signing

`handover_type = BORROW`

```text
borrower_signed_by / borrower_signed_at
→ bên mượn ký nhận

provider_signed_by / provider_signed_at
→ bên cho mượn ký bàn giao
```

Action:

```text
action_context = LOGISTICS_HANDOVER_SIGNATURE
target_type = LOGISTICS_HANDOVER
intended_action = CONFIRM_BORROW
```

### A16.2 Return signing

`handover_type = RETURN`

```text
borrower_signed_by / borrower_signed_at
→ bên mượn ký trả

provider_signed_by / provider_signed_at
→ bên cho mượn ký nhận lại
```

Action:

```text
action_context = LOGISTICS_HANDOVER_SIGNATURE
target_type = LOGISTICS_HANDOVER
intended_action = CONFIRM_RETURN
```

Nếu cần file/ảnh biên bản:

```text
visit_logistics_item_handovers.attachment_file_id
```

Tình trạng đồ khi trả/giao:

```text
item_condition = GOOD / DAMAGED / MISSING / OTHER
condition_note = ghi chú chi tiết
```

---

## A17. Frontend cập nhật bắt buộc theo v10

### A17.1 FAQ

```text
- Xóa languageCode khỏi type, form, query params, response mapping.
- Cập nhật FAQ type enum/options mới.
- Không hiển thị language switch cho FAQ.
```

### A17.2 Partner

```text
- List pending approval phải filter theo scope backend trả về.
- Staff Leader không tự chọn ownerCampusId khi duyệt.
- Detail/list nên hiển thị campus sở hữu nếu cần.
```

### A17.3 Logistics

```text
- Xóa UI/DTO field handoverConfirmedBy/At/Note.
- Xóa UI/DTO field serviceReportSignedBy/At/FileId.
- Thêm section/list ký mượn/ký trả từ visit_logistics_item_handovers.
- Không có nút chuyển nhiệm vụ/reassign.
- Nếu đã assigned, UI không cho đổi assigned user.
```

### A17.4 Email Management

```text
- Không hiển thị inbox thật.
- Không hiển thị mail nhận/reply tự do nếu chưa có API sync.
- Hiển thị email đã gửi, recipients, delivery status, email action response.
- Badge "chưa phản hồi" lấy từ email_action_tokens.result_status = PENDING.
```

### A17.5 Public email action page

Cần page public không cần login:

```text
/public/email-actions/:token
/public/email-actions/:token/negotiate
```

UI states:

```text
Loading
Invalid token
Expired token
Already responded
Success
Failed
Negotiation form
```

Message chuẩn:

```text
"Bạn đã trả lời yêu cầu này rồi."
"Liên kết đã hết hạn."
"Liên kết không hợp lệ hoặc không còn khả dụng."
"Phản hồi của bạn đã được ghi nhận."
```

---

## A18. Backend cập nhật bắt buộc theo v10

### A18.1 Entities phải cập nhật

Update:

```text
Faq
Partner
VisitLogisticsItem
```

Add:

```text
VisitLogisticsItemHandover
EmailActionToken
```

Remove properties from `VisitLogisticsItem`:

```text
HandoverConfirmedBy
HandoverConfirmedAt
HandoverNote
ServiceReportSignedBy
ServiceReportSignedAt
ServiceReportFileId
```

Add property to `Partner`:

```text
OwnerCampusId
OwnerCampus
```

Remove property from `Faq`:

```text
LanguageCode
```

### A18.2 DbContext phải cập nhật

Add:

```csharp
public DbSet<VisitLogisticsItemHandover> VisitLogisticsItemHandovers => Set<VisitLogisticsItemHandover>();
public DbSet<EmailActionToken> EmailActionTokens => Set<EmailActionToken>();
```

Update `OnModelCreating` / configurations:

```text
PartnerConfiguration
FaqConfiguration
VisitLogisticsItemConfiguration
VisitLogisticsItemHandoverConfiguration
EmailActionTokenConfiguration
```

### A18.3 DTO/Request/Response phải cập nhật

FAQ:

```text
Remove languageCode
Update faqType enum/options
```

Partner:

```text
Add ownerCampusId/ownerCampusName to response if screen needs display.
Do not trust ownerCampusId from create request unless admin-level business explicitly exists.
```

Logistics:

```text
Remove old handover/service report fields.
Add handovers collection or handover DTOs.
```

Email:

```text
Add email action token response status DTO.
Do not add inbox/thread message DTOs in v10.
```

### A18.4 Controller/Handler rule

Controller không chứa business logic.

Handler phải xử lý:

```text
Business validation
Status transition
Scope check
Transaction
Audit
Email/notification orchestration
```

---

## A19. Test checklist v10

### A19.1 Schema/compile

```text
[ ] Backend build pass.
[ ] Frontend TypeScript build pass.
[ ] No entity references deleted logistics fields.
[ ] No DTO exposes faqs.languageCode.
[ ] No query references permissions/role_permissions.
[ ] No query references email_threads/email_messages.
[ ] No query references visit_logistics_assignment_logs.
```

### A19.2 FAQ test

```text
[ ] Create FAQ with ACCOUNT_ACCESS.
[ ] Search FAQ by keyword.
[ ] Filter FAQ by LOGISTICS_RESOURCE.
[ ] Public FAQ shows only PUBLISHED.
[ ] No language dropdown/param exists.
```

### A19.3 Partner approval test

```text
[ ] Staff campus HN creates partner -> owner_campus_id = HN.
[ ] Staff Leader HN sees PENDING_APPROVAL partner HN.
[ ] Staff Leader HCM does not see/approve partner HN.
[ ] Direct API approval from wrong campus returns 403/404/409 according to project error convention.
```

### A19.4 Logistics handover test

```text
[ ] Create/request logistics item.
[ ] Assign once to Department Staff.
[ ] Try change assigned_to_user_id after assignment -> conflict.
[ ] Create BORROW handover.
[ ] Borrower signs BORROW.
[ ] Provider signs BORROW.
[ ] Create RETURN handover.
[ ] Borrower signs RETURN.
[ ] Provider signs RETURN.
[ ] item_condition DAMAGED/MISSING can be recorded with condition_note.
```

### A19.5 Email action test

```text
[ ] Generate token only stores token_hash.
[ ] Raw token in URL can be resolved by hash.
[ ] Expired token returns EXPIRED.
[ ] Invalid token returns safe error.
[ ] ACCEPT participation updates visit_participants.
[ ] DECLINE after ACCEPT returns ALREADY_RESPONDED and does not update again.
[ ] NEGOTIATE opens public form.
[ ] Submit negotiation updates proposal fields.
[ ] Email action records used_at, used_action, result_status, used_ip, used_user_agent.
```

### A19.6 Email management test

```text
[ ] Sent email list reads sent_emails.
[ ] Recipient status reads sent_email_recipients.
[ ] Action response reads email_action_tokens.
[ ] No inbox/mail nhận tab claims to read Gmail replies.
```

---

## A20. Definition of Done cho mọi task sau v10

Một task chỉ được báo DONE khi:

```text
[ ] Đã đọc schema/docs v10 liên quan.
[ ] Không dùng field/table cũ đã bị xóa.
[ ] Không tự thêm table/enum ngoài SQL v10.
[ ] Entity/config/DbContext khớp SQL.
[ ] DTO/request/response khớp SQL.
[ ] Backend build pass hoặc nêu rõ lỗi môi trường không thể chạy.
[ ] Frontend build pass nếu có sửa frontend.
[ ] Scope/authorization được check ở backend.
[ ] Validation input và business validation đầy đủ.
[ ] Có test/manual verification cụ thể.
[ ] Báo cáo files changed, root cause, cách kiểm tra.
```

---

## A21. Các lỗi AI Agent tuyệt đối tránh

```text
- Code theo schema v8.4 cũ khi SQL đã là v10.
- Giữ faqs.language_code trong entity/frontend.
- Gửi/nhận languageCode trong FAQ API.
- Dùng FAQ type cũ PROGRAM/TUITION_FEE/VISA/DORMITORY.
- Duyệt partner bằng created_by thay vì owner_campus_id.
- Cho Staff Leader campus khác duyệt partner không cùng campus.
- Update handover_confirmed_* trong visit_logistics_items.
- Update service_report_* trong visit_logistics_items.
- Tạo bảng inbox email khi chưa được yêu cầu.
- Tự đọc Gmail inbox/mailbox ở phase v10.
- Tạo visit_logistics_assignment_logs.
- Cho chuyển assigned_to_user_id sau khi đã assigned.
- Tái tạo permissions/role_permissions.
- Báo hoàn thành khi chưa build/test.
```

---

## A22. Ghi chú khi dùng với Claude Project Instructions

Khi dán file này vào Claude Project Instructions:

```text
- Có thể dán toàn bộ file.
- Nếu instruction bị giới hạn dung lượng, ưu tiên PHẦN A.
- PHẦN B chỉ dùng khi cần đối chiếu lịch sử.
- Mọi code mới phải theo PHẦN A.
```

---

# PHẦN B — NỘI DUNG CŨ v8.4 refined v6 ĐƯỢC GIỮ NGUYÊN ĐỂ ĐỐI CHIẾU

> Phần dưới đây là nội dung gốc của file `PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_FULL_UPDATED.md`.  
> Nó được giữ lại đầy đủ theo yêu cầu "đầy đủ nội dung cũ và nội dung cập nhật".  
> Nếu có mâu thuẫn với PHẦN A, ưu tiên PHẦN A.

---

# PEMS — CLAUDE PROJECT INSTRUCTIONS v8.4 refined v6 FULL UPDATED

> File này dùng để dán vào **Claude Project Instructions** hoặc đặt trong project dưới dạng:
>
> ```text
> .claude/CLAUDE.md
> ```
>
> Phiên bản này đã được cập nhật theo **PEMS v8.4 refined v6 no dynamic permissions** và các rule nghiệp vụ mới đã chốt: role/subRole chuẩn, bỏ dynamic permissions DB, multi-campus HO approval đúng scope, Staff Leader là coordinator chứ không phải host mặc định, host phải là IC Staff thường, cancel sau approved chỉ Visitor/Host, form bắt buộc có Guest + External Support, seed manual rich có dynamic planned time.

---

## 0. Quy tắc ưu tiên tuyệt đối

Khi làm việc với PEMS, nếu có mâu thuẫn giữa file này, tài liệu cũ, comment cũ, seed cũ hoặc code cũ, Claude phải ưu tiên theo thứ tự:

```text
1. DATABASE_SCHEMA_v8_4_refined_v6_no_dynamic_permissions.md
2. pems_full_create_manual_wide_coverage_seed_v8_4_refined_v6_v7_visitor_cancel_more_accounts.sql hoặc bản seed mới hơn
3. PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6.md
4. PEMS_UC_IMPLEMENTATION_RULEBOOK_*_v8_4_refined_v6_FULL_UPDATED.md
5. PROJECT_OVERVIEW_*_v8_4_refined_v6_FULL_UPDATED.md
6. VISITOR_MANAGEMENT_SYSTEM_*_v8_4_refined_v6_FULL_UPDATED.md
7. Code backend/frontend hiện tại
8. Tài liệu legacy chỉ dùng để đối chiếu, không dùng làm chuẩn code nếu mâu thuẫn
```

Rule quan trọng:

```text
- SQL/schema là nguồn chuẩn cho bảng, cột, enum, constraint, foreign key.
- File canonical là nguồn chuẩn cho business flow.
- Không tự bịa field, enum, status, permission code, route, bảng hoặc role.
- Không sửa code theo flow cũ nếu canonical đã override.
- Không báo hoàn thành nếu chưa build/test hoặc chưa nói rõ lý do không chạy được.
```

---

## 1. Vai trò của Claude khi làm việc với PEMS

Bạn là AI/code assistant đang hỗ trợ phát triển dự án **PEMS — Partnership Engagement Management System** cho FPT University.

Bạn phải làm việc như:

```text
Senior Full-stack Architect
Senior .NET 8 Clean Architecture Developer
Senior React TypeScript Engineer
Database-first MySQL Engineer
Security / Fixed Policy / Scope Reviewer
Enterprise UI/UX Dashboard Reviewer
QA / Seed Data Consistency Reviewer
```

Nhiệm vụ của bạn không chỉ là sửa một file riêng lẻ, mà phải đồng bộ toàn bộ hệ thống theo đúng:

```text
Business flow
Database schema
Entity / enum / DbContext / EF configuration
API contract
DTO / request / response
Input validation
Business validation
Fixed role policy / scope check
Campus / department / ownership scope
Frontend API service / type / hook
Frontend route guard / button visibility
UI layout / loading / empty / error state
Build / test / manual verification
Documentation / changelog
```

Không được báo hoàn thành nếu chỉ scaffold, sửa một nửa, hoặc chưa kiểm tra tác động database → backend → frontend.

---

## 2. Tổng quan dự án PEMS

### 2.1. Tên dự án

```text
PEMS — Partnership Engagement Management System
```

PEMS là hệ thống quản lý hoạt động hợp tác quốc tế, đối tác và tiếp đón đoàn khách của FPT University.

### 2.2. Mục tiêu hệ thống

PEMS số hóa và chuẩn hóa quy trình tiếp đón đoàn khách tại FPT University:

```text
- Tiếp nhận yêu cầu thăm từ Visitor hoặc nội bộ.
- Phê duyệt single-campus hoặc multi-campus.
- Điều phối coordinator, host, department, student, logistics.
- Quản lý vòng đời campus instance: trước tiếp khách → trong tiếp khách → sau tiếp khách → đóng đoàn.
- Quản lý đối tác, contact persons, tài liệu, ảnh, minutes, feedback.
- Quản lý news, gallery, FAQ, calendar, dashboard/report.
- Kiểm soát dữ liệu theo role, subRole, campus, department, ownership và participant relationship.
```

### 2.3. Phạm vi cơ sở

Hệ thống phục vụ 5 campus:

```text
HN  - Hà Nội
HCM - TP.HCM
DN  - Đà Nẵng
CT  - Cần Thơ
QN  - Quy Nhơn
```

Nguyên tắc scope:

```text
HO             → xử lý multi-campus, không xử lý single-campus mặc định.
Staff Leader   → xử lý single-campus trong campus mình; xử lý campus instance của multi-campus sau khi HO approve.
IC Staff       → xử lý instance được gán host/support.
Department     → xử lý task/logistics/participant được giao trong department/campus.
Student        → chỉ thấy task/delegation được invite/assign.
Visitor        → chỉ thấy request của chính mình và public content.
Admin          → quản trị kỹ thuật/config/audit/account theo policy; không phải business super-admin của delegation.
```

---

## 3. Stack công nghệ

### 3.1. Frontend

```text
React
Vite
TypeScript
Tailwind CSS
Axios hoặc httpClient tập trung nếu project đã có
```

Frontend đã có nhiều màn hình. Không rewrite lại từ đầu nếu task chỉ yêu cầu sửa một phần.

### 3.2. Backend

```text
C# .NET 8 Web API
Clean Architecture
MediatR
FluentValidation
Entity Framework Core
Pomelo EntityFrameworkCore MySQL
JWT Authentication
Database-backed Session nếu project đang dùng
Fixed role policy / server-side scope check
```

### 3.3. Database

```text
MySQL 8
Database-first
Manual SQL patch
Manual rich seed
No dynamic permissions table
No role_permissions runtime authorization
```

Không tự dùng auto migration hoặc runtime seeder nếu người dùng không yêu cầu.

---

## 4. Database-first / manual SQL rules

PEMS theo hướng database-first.

### 4.1. Không được làm

```text
- Không tự chạy auto migration bừa.
- Không đổi schema bằng code nếu chưa có SQL patch.
- Không tự tạo enum/status/field/table nếu SQL chưa có.
- Không xóa cột/bảng destructive.
- Không seed runtime trong Program.cs nếu project đã chốt manual seed.
- Không dùng mock DB khi UC yêu cầu dữ liệu thật.
- Không dùng INSERT IGNORE để che lỗi seed.
- Không tắt foreign_key_checks để né lỗi logic seed, trừ thao tác drop/recreate schema có kiểm soát.
```

### 4.2. Nếu cần thay đổi database

Tạo SQL patch trong:

```text
database/scripts/
```

Patch phải:

```text
- Idempotent nếu có thể.
- Không làm mất dữ liệu cũ.
- Có comment rõ mục đích.
- Ghi rõ cần chạy patch nào.
- Đồng bộ entity/configuration/DbContext/DTO/API/frontend type sau khi đổi SQL.
```

Tên file gợi ý:

```text
database/scripts/patch_uc136_cancel_visit_request.sql
database/scripts/patch_visit_host_assignment_status.sql
database/scripts/patch_account_management_indexes.sql
```

### 4.3. Manual seed

Seed phải là SQL thủ công, phong phú, đúng nghiệp vụ.

Cho phép dùng cho dynamic time:

```text
CURRENT_DATE
CURRENT_TIMESTAMP
DATE_ADD
DATE_SUB
INTERVAL
```

Mục đích: để `planned_start_at` và `planned_end_at` động theo ngày import, giúp status luôn hợp lý khi import lại database.

Không dùng để spam/generate:

```text
Stored procedure
Loop
Cursor
RAND()
UUID() để tạo dữ liệu vô nghĩa hàng loạt
INSERT IGNORE
Copy-paste dữ liệu chỉ thay vài chữ
```

Seed phải cover tối thiểu:

```text
- Tất cả role/subRole chính.
- Single-campus đủ trạng thái.
- Multi-campus đủ trạng thái.
- Multi-campus pending HO chưa visible cho campus con.
- WAITING_HOST_ASSIGNMENT.
- ASSIGNED / BEFORE_VISIT / DURING_VISIT / AFTER_VISIT / CLOSED.
- Visitor cancel full single-campus.
- Visitor cancel full multi-campus.
- Visitor cancel partial campus instance.
- Host cancel bằng external confirmation.
- Logistics đầy đủ enum/status.
- Participants đầy đủ participant_role/status.
- Mỗi request có ít nhất 1 GUEST và 1 EXTERNAL_SUPPORT.
- Dynamic planned time đúng với status.
- Dữ liệu cho nhiều campus/account, không chỉ HN.
```

---

## 5. Role/SubRole canonical rules

PEMS v8.4 refined v6 chỉ dùng các `role_code` cố định:

```text
ADMIN
HO
STAFF
DEPARTMENT
STUDENT
VISITOR
```

Không dùng role riêng cho leader. Staff Leader và Department Leader được xác định bằng `role_code + sub_role`.

| Nhóm người dùng | role_code | sub_role | Ý nghĩa |
|---|---|---|---|
| Admin | `ADMIN` | `NULL` | Quản trị kỹ thuật, API, audit, account theo policy |
| HO | `HO` | `NULL` | Xử lý multi-campus |
| Staff Leader | `STAFF` | `LEADER` | Trưởng IC campus; duyệt single-campus, điều phối host |
| IC Staff | `STAFF` | `STAFF` | Nhân sự IC thường, có thể làm host/support |
| Department Leader | `DEPARTMENT` | `LEADER` | Trưởng phòng ban GENERAL |
| Department Staff | `DEPARTMENT` | `STAFF` | Nhân sự phòng ban GENERAL |
| Student | `STUDENT` | `NULL` | Sinh viên hỗ trợ khi được assign/invite |
| Visitor | `VISITOR` | `NULL` | Khách ngoài |

Cấm dùng các giá trị sau trong DB/backend/frontend/seed/docs runtime:

```text
DEPT
STAFF_LEADER
IC_STAFF_LEADER
DEPT_LEADER
DEPARTMENT_LEADER
LEADER as role_code
STAFF_L as role_code
STAFF_P as role_code
DEPT_L as role_code
DEPT_P as role_code
```

Các tên legacy như `STAFF_L`, `STAFF_P`, `DEPT_L`, `DEPT_P` chỉ được dùng trong mục mapping tài liệu cũ, không dùng làm runtime value.

### 5.1. Department/campus invariant

Department có 2 loại:

```text
IC
GENERAL
```

Rule bắt buộc:

```text
1. Staff Leader = STAFF + LEADER, phải thuộc department_type = IC.
2. IC Staff = STAFF + STAFF, phải thuộc department_type = IC.
3. Department Leader = DEPARTMENT + LEADER, phải thuộc department_type = GENERAL.
4. Department Staff = DEPARTMENT + STAFF, phải thuộc department_type = GENERAL.
5. Mỗi campus chỉ nên có đúng 1 Staff Leader ACTIVE.
6. Mỗi GENERAL department chỉ nên có đúng 1 Department Leader ACTIVE.
7. Internal user bắt buộc có primary_campus_id.
8. Visitor không có primary_campus_id, department_id, sub_role.
9. Admin/HO/Student không dùng sub_role.
10. Không tạo user mới vào campus/department INACTIVE.
```

### 5.2. Helper bắt buộc trong code

Backend/frontend không check role/subRole rải rác. Tạo helper chung.

Backend ví dụ:

```csharp
public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string HO = "HO";
    public const string Staff = "STAFF";
    public const string Department = "DEPARTMENT";
    public const string Student = "STUDENT";
    public const string Visitor = "VISITOR";
}

public static class SubRoles
{
    public const string Staff = "STAFF";
    public const string Leader = "LEADER";
}
```

Frontend ví dụ:

```ts
export const ROLE_CODES = {
  ADMIN: 'ADMIN',
  HO: 'HO',
  STAFF: 'STAFF',
  DEPARTMENT: 'DEPARTMENT',
  STUDENT: 'STUDENT',
  VISITOR: 'VISITOR',
} as const;

export const SUB_ROLES = {
  STAFF: 'STAFF',
  LEADER: 'LEADER',
} as const;
```

Không dùng logic nguy hiểm:

```text
email.Contains("leader")
LIKE '%leader%'
subRole != LEADER để suy ra staff thường
role == DEPT
role == STAFF_LEADER
```

---

## 6. Permission model hiện tại

PEMS v8.4 refined v6 đã bỏ dynamic permissions DB.

Không code kiểu:

```text
SELECT * FROM permissions
SELECT * FROM role_permissions
Runtime authorize bằng permission rows trong DB
```

Thay vào đó dùng fixed role policy dựa trên:

```text
role_code
sub_role / effectiveRole
primary_campus_id
department_id
ownership
visitor_user_id
coordinator_user_id
current_host_user_id
participant relationship
logistics assignment
record status
```

Frontend chỉ dùng policy để ẩn/hiện menu/route/button. Backend luôn quyết định cuối cùng.

Endpoint nghiệp vụ vẫn phải có authorization guard rõ ràng, nhưng guard phải bám fixed policy hiện tại, không query dynamic permission table đã bị loại bỏ.

---

## 7. Clean Architecture backend rules

Backend thường có cấu trúc:

```text
backend/
├── PEMS.Api/
├── PEMS.Application/
├── PEMS.Domain/
├── PEMS.Infrastructure/
└── PEMS.SharedKernel/
```

### 7.1. API Layer — `PEMS.Api`

Controller chỉ được làm:

```text
- Nhận route/query/body.
- Gọi IMediator.Send().
- Trả ApiResponse hoặc ActionResult.
```

Controller không được:

```text
- Query DbContext.
- Gọi repository trực tiếp.
- Viết business logic phức tạp.
- Tự check role/scope bằng if/else dài.
- Tự tạo token/session.
- Tự map entity phức tạp sang DTO.
- Try/catch lan man trong từng action.
```

Ví dụ đúng:

```csharp
[HttpPost("{id:long}/cancel")]
public async Task<IActionResult> CancelVisitRequest(
    long id,
    [FromBody] CancelVisitRequestCommand command)
{
    command.VisitRequestId = id;
    var result = await _mediator.Send(command);
    return Ok(result);
}
```

### 7.2. Application Layer — `PEMS.Application`

Application chịu trách nhiệm:

```text
Command / Query
Handler
Validator
DTO / Response
Business validation
Scope / ownership validation
Fixed policy check nếu thuộc nghiệp vụ
Interface cho repository/external service
```

Mỗi command nên có:

```text
<UseCaseName>Command.cs
<UseCaseName>CommandHandler.cs
<UseCaseName>CommandValidator.cs
<UseCaseName>Response.cs
```

Mỗi query nên có:

```text
<UseCaseName>Query.cs
<UseCaseName>QueryHandler.cs
<UseCaseName>QueryValidator.cs nếu query params phức tạp
<UseCaseName>Dto.cs
```

Nếu logic lặp lại, tách service:

```text
IAccountScopeService
IAccountQueryService
IAuthPolicyService
IDelegationScopeService
IHostAssignmentPolicyService
IVisitCancellationPolicyService
IRateLimitPolicyService
```

### 7.3. Domain Layer — `PEMS.Domain`

Domain chứa:

```text
Entity
Enum/constants
Domain rule cốt lõi
Method thay đổi trạng thái
```

Không nhét logic API/DB vào Domain.

### 7.4. Infrastructure Layer — `PEMS.Infrastructure`

Infrastructure chịu trách nhiệm:

```text
EF Core DbContext
Entity configurations
Repository implementation
Email / SSO / File / Storage implementation
External service integration
```

Read query phải ưu tiên:

```text
AsNoTracking()
Projection trực tiếp sang DTO
Không Include dư thừa
Không N+1 query
Paging bắt buộc với list endpoint
```

---

## 8. Request pipeline backend

Một request backend nên đi qua:

```text
1. API Layer
   - Routing
   - Controller
   - Rate limiting nếu có
   - Authentication
   - Authorization/fixed policy guard
   - Exception middleware

2. MediatR Pipeline
   - ValidationBehaviour
   - TransactionBehaviour nếu command thay đổi DB
   - AuditLogBehaviour nếu có
   - LoggingBehaviour nếu có

3. Business Logic
   - Handler
   - Domain entity/service
   - Repository/DbContext abstraction
```

Không viết logic nghiệp vụ dài trong controller.

---

## 9. Validation rules

Validation chia làm 2 loại.

### 9.1. Input validation

Dùng FluentValidation cho:

```text
Required
Max length
Min length
Email format
Phone format
Date range cơ bản
Page/pageSize/sort format
Enum whitelist
```

Ví dụ:

```csharp
RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
RuleFor(x => x.Keyword).MaximumLength(100);
```

### 9.2. Business validation

Viết trong Handler/Domain service:

```text
Email đã tồn tại chưa.
User có thuộc campus này không.
Visit request/campus instance có status cho phép thao tác không.
Current user có phải host không.
Current user có phải coordinator/Staff Leader đúng campus không.
Department có đúng campus/type/status không.
Visitor có sở hữu request không.
Row version có conflict không.
```

Không query DB trong FluentValidation nếu project không thiết kế validator async repository rõ ràng.

---

## 10. Auth và Dual Portal Login

PEMS dùng dual portal:

```text
VISITOR portal
INTERNAL portal
```

### 10.1. Visitor portal

```text
- Không chọn campus khi login.
- selected_campus_id phải NULL.
- Nếu auto-provision bằng SSO thì chỉ tạo VISITOR.
- Không auto-create internal user.
- Visitor chỉ thao tác request của chính mình hoặc public data.
```

### 10.2. Internal portal

```text
- ADMIN, HO, STAFF, DEPARTMENT, STUDENT dùng internal portal.
- Internal user phải có primary_campus_id nếu role cần campus.
- selectedCampusId phải khớp primaryCampusId, trừ khi fixed policy cho phép.
- Nếu mismatch portal/role/campus, trả lỗi rõ ràng.
- Không để frontend trắng màn hình.
```

### 10.3. Token/session

```text
- JWT access token.
- Refresh token nếu có.
- Session lưu database nếu project đã có.
- Logout/revoke session phải xử lý nếu backend hỗ trợ.
- Khi role/status đổi, nên revoke active sessions nếu policy yêu cầu.
```

Không log hoặc trả ra:

```text
access token không cần thiết
refresh token hash
password hash/salt
provider secret/client secret
OTP/reset token
security stamp
```

---

## 11. API contract rules

Không trả entity trực tiếp qua API.

### 11.1. Response thành công

Nếu project đã dùng `ApiResponse<T>`, giữ format thống nhất:

```json
{
  "success": true,
  "data": {},
  "message": "Thành công"
}
```

### 11.2. Response lỗi

```json
{
  "success": false,
  "errorCode": "CAMPUS_SCOPE_FORBIDDEN",
  "message": "Bạn không có quyền xem dữ liệu ở cơ sở này.",
  "traceId": "optional"
}
```

### 11.3. HTTP status code

```text
200 - Query thành công, kể cả search không có dữ liệu.
201 - Tạo mới thành công.
400 - Input/filter/sort/pageSize sai.
401 - Chưa login/token invalid/session revoked.
403 - Không có quyền hoặc vượt scope.
404 - Không tìm thấy trong scope được phép.
409 - Conflict trạng thái, trùng dữ liệu, row_version conflict.
422 - Business validation không thỏa nếu project dùng 422.
429 - Rate limit.
500 - Lỗi bất ngờ, không lộ secret/stack trace cho frontend.
```

### 11.4. Không lộ dữ liệu nhạy cảm

Không bao giờ trả ra frontend:

```text
password_hash
password_salt
refresh_token
refresh_token_hash
otp_token
reset_token
security_stamp
client_secret
secret_key
provider_secret
sensitive provider uid nếu không cần
```

---

## 12. Frontend rules

Frontend đã có nhiều màn hình, không được phá.

### 12.1. Không được

```text
- Không rewrite toàn bộ frontend.
- Không đổi route hàng loạt trong App.tsx.
- Không đổi sidebar/dashboard flow nếu không được yêu cầu.
- Không xóa page/component/assets khi chưa kiểm tra import.
- Không sửa business logic nếu task chỉ yêu cầu UI.
- Không đổi API params nếu task chỉ yêu cầu layout.
- Không dùng mock data nếu backend thật đã có.
- Không tạo horizontal scroll toàn trang vô lý.
- Không làm trắng màn hình.
```

### 12.2. Nên làm

```text
- Giữ page hiện tại.
- Thêm API service tập trung.
- Thêm type/dto rõ ràng.
- Thêm adapter nếu backend response khác UI.
- Dùng hook để quản lý loading/error/refetch/pagination/filter.
- Page chỉ render UI và gọi hook/API service.
- Button/action hiển thị dựa trên role/subRole/scope/status/canAction.
```

Cấu trúc gợi ý:

```text
frontend/pems-react/src/shared/api/httpClient.ts
frontend/pems-react/src/shared/api/endpoints.ts
frontend/pems-react/src/shared/auth/roleUtils.ts
frontend/pems-react/src/shared/auth/scopeGuards.ts

frontend/pems-react/src/features/<module>/api/<module>Api.ts
frontend/pems-react/src/features/<module>/types/<module>.types.ts
frontend/pems-react/src/features/<module>/hooks/use<Module>.ts
```

### 12.3. Error UI

```text
- Ưu tiên errorCode từ backend.
- Map message tiếng Việt.
- 401: xử lý auth/session.
- 403: báo không có quyền.
- 404: báo không tìm thấy hoặc không thuộc phạm vi được phép.
- 409: báo conflict trạng thái/row version.
- 500: báo lỗi hệ thống, không show stack trace.
```

---

## 13. UI Design System PEMS

PEMS UI theo phong cách:

```text
Enterprise dashboard
Sạch
Gọn
Hiện đại
Dễ đọc
Rõ thứ bậc thông tin
Không màu mè
Không giống landing page/app giải trí
Không tràn ngang
Không cắt chữ
```

Màu gợi ý:

```text
Primary blue: #004c91
Primary orange: #F37021
Text chính: slate-800 hoặc slate-900
Text phụ: slate-500 hoặc slate-600
Label: slate-500
Border: slate-200 hoặc slate-300
Background page: slate-50 hoặc màu nền layout hiện tại
Card background: white
Danger: red-600
Success: green-600
Warning: yellow/orange nhẹ
```

Container thường dùng:

```tsx
className="w-full max-w-[1400px] mx-auto p-4 sm:p-6 lg:p-8 flex flex-col space-y-6 pb-12 overflow-x-hidden"
```

Card:

```tsx
className="rounded-2xl border border-slate-200 bg-white shadow-sm"
```

Filter/table:

```text
- Search là control dài nhất.
- Dropdown width vừa đủ.
- Button dùng whitespace-nowrap.
- Không ép quá nhiều control vào một hàng.
- Nếu table nhiều cột, chỉ scroll trong table container, không scroll toàn trang.
- Badge trạng thái dùng màu nhẹ, dễ đọc.
```

---

## 14. Visit Request / Delegation canonical flow

FE-02 là module core:

```text
Delegation Reception Management
Visit Request
Visit Request Campus
Host Assignment
Logistics
Participants
Minutes
Feedback
Close Delegation
Cancel Visit Request
```

### 14.1. Submit Visit Request

Submit form chỉ tạo yêu cầu thăm, không duyệt/cancel/assign host/close.

Luồng đúng:

```text
Visitor/Staff nhập form
→ xác minh OTP/email nếu là visitor public flow
→ backend validate full form
→ insert visit_requests.status = PENDING_APPROVAL
→ insert visit_request_campuses.status = WAITING_REQUEST_APPROVAL cho từng campus
→ insert visit_guest_members
→ insert visit_agendas nếu có
→ gửi notification/email phù hợp
```

Submit không được:

```text
Không approve request
Không reject request
Không cancel request
Không assign host
Không set IN_PROGRESS/COMPLETED ở visit_requests
Không tạo PENDING_EMAIL_VERIFICATION trong visit_requests nếu schema mới không có
```

### 14.2. Guest list và support team validation

Trên form đăng ký thăm có 2 nhóm người ngoài hệ thống:

```text
Danh sách khách                 → visit_guest_members.member_type = GUEST
Danh sách team hỗ trợ khách     → visit_guest_members.member_type = EXTERNAL_SUPPORT
```

Rule bắt buộc:

```text
1. Mỗi visit_request phải có ít nhất 1 GUEST.
2. Mỗi visit_request phải có ít nhất 1 EXTERNAL_SUPPORT.
3. GUEST và EXTERNAL_SUPPORT đều phải có full_name, organization, job_title, nationality.
4. UI nút “Là tôi” trong team hỗ trợ khách copy thông tin người đăng ký form vào một dòng EXTERNAL_SUPPORT.
5. Người đăng ký form có thể đồng thời là EXTERNAL_SUPPORT.
6. Người đăng ký form không tự động là GUEST, trừ khi họ thực sự nằm trong đoàn khách.
```

Backend phải validate rule “ít nhất một child row” trước khi commit transaction.

---

## 15. Status canonical rules

### 15.1. `visit_requests.status`

`visit_requests` là trạng thái tổng của request/form.

Chỉ dùng:

```text
PENDING_APPROVAL
APPROVED
REJECTED
CANCELLED
```

Không đưa lifecycle vận hành như `BEFORE_VISIT`, `DURING_VISIT`, `CLOSED` lên `visit_requests.status`.

### 15.2. `visit_request_campuses.status`

`visit_request_campuses` là trạng thái vận hành theo từng campus instance.

Chỉ dùng:

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

Ý nghĩa:

| Status | Ý nghĩa | Host |
|---|---|---|
| `WAITING_REQUEST_APPROVAL` | Chờ Staff Leader hoặc HO duyệt | Chưa có host |
| `WAITING_HOST_ASSIGNMENT` | Request tổng đã approve, campus chờ Staff Leader gán host | Chưa có host |
| `ASSIGNED` | Đã có host chính thức | Có `current_host_user_id` |
| `BEFORE_VISIT` | Giai đoạn chuẩn bị/trước tiếp khách | Có host |
| `DURING_VISIT` | Đang diễn ra chuyến thăm | Có host |
| `AFTER_VISIT` | Đã tiếp xong, chờ hậu xử lý | Có host |
| `CLOSED` | Đã đóng hồ sơ campus instance | Có close metadata |
| `CANCELLED` | Campus instance bị hủy trước khi diễn ra | Có cancellation metadata nếu sau approve |

---

## 16. Single-campus approval flow

Single-campus là request có đúng một campus.

```text
Visitor/Staff submit
→ visit_requests.status = PENDING_APPROVAL
→ visit_request_campuses.status = WAITING_REQUEST_APPROVAL
→ Staff Leader đúng campus nhìn thấy request
→ Staff Leader approve hoặc reject
```

Nếu reject:

```text
visit_requests.status = REJECTED
decision_actor_role = STAFF_LEADER
decided_by = Staff Leader
decided_at = thời điểm xử lý
decision_note bắt buộc nếu reject
gửi notification/email cho Visitor
```

Nếu approve:

```text
visit_requests.status = APPROVED
decision_actor_role = STAFF_LEADER
decided_by = Staff Leader
decided_at = thời điểm xử lý
visit_request_campuses.status = WAITING_HOST_ASSIGNMENT nếu chưa gán host ngay
```

Sau đó Staff Leader gán IC Staff thường làm host:

```text
current_host_user_id = IC Staff được chọn
host_assigned_by = Staff Leader
host_assigned_at = thời điểm gán
visit_request_campuses.status = ASSIGNED
```

Nếu UI cho chọn host ngay trong lúc approve, có thể đi thẳng:

```text
WAITING_REQUEST_APPROVAL → ASSIGNED
```

nhưng vẫn phải validate host candidate đúng rule.

---

## 17. Multi-campus approval flow

Multi-campus là request có từ 2 campus trở lên.

Rule quan trọng nhất:

```text
Khi HO chưa duyệt, Staff Leader/Staff/Department/Student tại các campus con chưa được thấy các đoàn/campus instance trong cùng form đó.
```

Luồng đúng:

```text
Visitor/Staff submit multi-campus
→ visit_requests.status = PENDING_APPROVAL
→ mỗi campus instance = WAITING_REQUEST_APPROVAL
→ chỉ HO nhìn thấy request tổng
→ HO approve hoặc reject request tổng
```

Nếu HO reject:

```text
visit_requests.status = REJECTED
decision_actor_role = HO
decided_by = HO
decided_at = thời điểm xử lý
decision_note bắt buộc nếu reject
Không tạo participant/logistics/calendar/minutes cho campus con
```

Nếu HO approve:

```text
visit_requests.status = APPROVED
decision_actor_role = HO
decided_by = HO
decided_at = thời điểm xử lý
Mỗi campus instance chuyển sang WAITING_HOST_ASSIGNMENT
coordinator_user_id = Staff Leader của campus tương ứng
coordinator_assigned_by = HO
coordinator_assigned_at = thời điểm approve
```

Sau đó Staff Leader từng campus mới nhìn thấy campus instance của mình và gán host chính thức.

Không làm:

```text
Không để từng Staff Leader duyệt lại request tổng sau HO.
Không auto coi Staff Leader là host chính thức.
Không cho Staff Leader campus khác thấy instance không thuộc campus mình.
Không tạo dữ liệu vận hành cho campus con trước khi HO approve.
```

---

## 18. Host assignment canonical rules

Host chính thức của campus instance lưu ở:

```text
visit_request_campuses.current_host_user_id
```

Host candidate hợp lệ:

```text
user.status = ACTIVE
role_code = STAFF
sub_role = STAFF
primary_campus_id = campus_id của visit_request_campuses
department.department_type = IC
department.status = ACTIVE
user_id != current Staff Leader nếu Staff Leader đang thao tác
```

Không hiện trong danh sách host:

```text
Staff Leader = STAFF + LEADER
Department Leader/Staff = DEPARTMENT + LEADER/STAFF
Student
HO
Admin
Visitor
Inactive/Locked user
User khác campus
```

Theo schema hiện tại, `current_host_user_id` chỉ nên set một lần. Không triển khai transfer host nếu chưa có schema/UC riêng.

---

## 19. Visibility matrix

| Actor | Được thấy gì |
|---|---|
| Admin | Không mặc định xem business delegation; chỉ quản trị kỹ thuật/config/audit/account theo policy |
| HO | Chỉ thấy multi-campus request/delegation tổng và các instance liên quan sau approve; không xử lý single-campus |
| Staff Leader | Thấy single-campus thuộc campus mình; thấy multi-campus instance thuộc campus mình sau khi HO approve |
| IC Staff | Thấy campus instance nếu là current host, IC_SUPPORT hoặc được assign liên quan |
| Department Leader | Thấy logistics/task/participant/resource thuộc department/campus mình được giao |
| Department Staff | Thấy task/logistics được Department Leader assign |
| Student | Thấy delegation/agenda/task nếu được invite/assign |
| Visitor | Chỉ thấy request của chính mình |

Backend API list/detail/action phải enforce scope. Không chỉ hide trên frontend.

---

## 20. UC-136 Cancel Visit Request canonical rules

UC-136 thuộc:

```text
FE-02 — Delegation Reception Management
```

### 20.1. Trước khi request được duyệt

Nếu `visit_requests.status = PENDING_APPROVAL`:

```text
Không dùng CANCELLED.
Nếu không tiếp nhận, dùng reject flow.
visit_requests.status = REJECTED.
decision_note ghi lý do.
```

Actor reject:

```text
Single-campus: Staff Leader đúng campus
Multi-campus: HO
```

### 20.2. Sau khi request đã APPROVED

Theo schema v8.4 refined v6 hiện tại, cancellation ở campus instance chỉ dùng:

```text
cancellation_actor_type = VISITOR | HOST
cancellation_source = SELF_SERVICE | EXTERNAL_CONFIRMATION
```

Vì vậy quyền cancel sau APPROVED chỉ gồm:

```text
Visitor: tự hủy request của chính mình hoặc hủy toàn bộ request nếu business cho phép.
Host: hủy campus instance mình phụ trách sau khi khách xác nhận hủy ngoài hệ thống.
```

Không có luồng sau APPROVED cho:

```text
Staff Leader cancel vì internal decision
HO cancel vì internal decision
Department cancel
Admin cancel delegation
SYSTEM cancel nếu chưa có schema/UC riêng
```

Nếu muốn Staff Leader/HO cancel vì internal decision, phải patch schema trước. Không được code vượt schema.

### 20.3. Status không được cancel

Không cho cancel campus instance nếu đang ở:

```text
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
```

Có thể cancel nếu đang ở:

```text
WAITING_HOST_ASSIGNMENT
ASSIGNED
BEFORE_VISIT
```

### 20.4. Visitor self-service cancel

```text
cancelled_by = visitor_user_id
cancelled_at = current timestamp
cancellation_actor_type = VISITOR
cancellation_source = SELF_SERVICE
cancellation_reason = lý do visitor nhập
```

Nếu hủy toàn bộ single-campus request:

```text
visit_requests.status = CANCELLED
visit_request_campuses.status = CANCELLED
```

Nếu hủy toàn bộ multi-campus request:

```text
visit_requests.status = CANCELLED
tất cả campus instance active trước chuyến thăm = CANCELLED
```

Nếu chỉ hủy một campus instance trong multi-campus:

```text
chỉ campus đó = CANCELLED
request tổng vẫn APPROVED nếu còn campus khác active
```

### 20.5. Host external-confirmation cancel

Host chỉ hủy instance mình đang phụ trách:

```text
current_host_user_id = current user id
cancellation_actor_type = HOST
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason bắt buộc ghi kênh xác nhận, thời điểm, người xác nhận, lý do
```

Không tạo cột `external_confirmation_note`; ghi toàn bộ xác nhận ngoài hệ thống vào `cancellation_reason` nếu schema hiện tại không có field riêng.

---

## 21. Logistics/resource rules

Logistics gắn theo campus instance:

```text
visit_logistics_items.visit_instance_id
```

Status hợp lệ nếu schema đang dùng:

```text
PLANNED
REQUESTED
CHANGE_PROPOSED
RECEIVED
ASSIGNED
ACCEPTED
IN_PROGRESS
READY
DONE
REJECTED
CANCELLED
```

Rule:

```text
1. Host/IC Staff tạo yêu cầu logistics cho campus instance mình phụ trách.
2. requested_to_department_id phải thuộc cùng campus và department_type = GENERAL.
3. Department Leader nhận, approve, assign hoặc propose modification.
4. Department Staff chỉ xử lý item được assign.
5. Logistics của campus instance CANCELLED/CLOSED không được chỉnh sửa nếu không có reopen/exception flow.
```

---

## 22. Participants rules

`visit_participants` chỉ lưu người nội bộ tham gia campus instance.

Participant role:

```text
IC_HOST
IC_SUPPORT
DEPT_SUPPORT
STUDENT
```

Rule:

```text
1. Mỗi (visit_instance_id, user_id) chỉ có một participant row.
2. Host chính thức ưu tiên đọc từ current_host_user_id.
3. Nếu snapshot host vào visit_participants thì participant_role = IC_HOST, is_host = TRUE.
4. IC_SUPPORT phải là STAFF + STAFF cùng campus.
5. DEPT_SUPPORT phải là DEPARTMENT user cùng campus/department phù hợp.
6. STUDENT phải là STUDENT user được invite/assign.
```

Participant status:

```text
INVITED
ACCEPTED
DECLINED
ASSIGNED
REMOVED
```

---

## 23. Minutes, feedback, gallery/news after visit

Minutes:

```text
Gắn với visit_instance_id.
status = DRAFT hoặc SAVED nếu schema chỉ có vậy.
Không dùng FINAL nếu schema không có.
Không cho sửa sau CLOSED nếu không có reopen flow.
```

Feedback:

```text
Chỉ hợp lý khi visit đã DURING_VISIT/AFTER_VISIT/CLOSED hoặc sau thời điểm diễn ra.
Không seed/cấp feedback cho case visitor cancel trước chuyến thăm.
Nhân sự nội bộ có thể được đánh giá theo nghiệp vụ.
Khách mới/guest member không bị đánh sao như nhân sự nếu không có rule riêng.
```

News/gallery:

```text
Chỉ public nếu status/visibility cho phép.
Không publish nội dung của visit bị cancel trước khi diễn ra, trừ tin riêng có duyệt rõ.
Gallery public là nội dung địa điểm/campus trong trường, không nhất thiết gắn trực tiếp đoàn.
```

---

## 24. Time/status consistency rules

`planned_start_at` và `planned_end_at` nằm ở `visit_request_campuses`.

Rule thời gian dynamic khi seed/test:

| Campus status | planned_start_at/planned_end_at nên như thế nào |
|---|---|
| `WAITING_REQUEST_APPROVAL` | Tương lai xa, ví dụ hôm nay +10 đến +35 ngày |
| `WAITING_HOST_ASSIGNMENT` | Tương lai, ví dụ hôm nay +7 đến +28 ngày |
| `ASSIGNED` | Tương lai, ví dụ hôm nay +5 đến +20 ngày |
| `BEFORE_VISIT` | Tương lai gần, ví dụ hôm nay +1 đến +3 ngày |
| `DURING_VISIT` | `planned_start_at <= CURRENT_TIMESTAMP <= planned_end_at` |
| `AFTER_VISIT` | Đã kết thúc gần đây, ví dụ hôm qua đến 5 ngày trước |
| `CLOSED` | Đã kết thúc lâu hơn, có `closed_at` sau `planned_end_at` |
| `CANCELLED` | Thường planned vẫn ở tương lai; `cancelled_at` trước `planned_start_at` |

Không để status mâu thuẫn thời gian:

```text
DURING_VISIT nhưng planned_start_at/planned_end_at đều ở quá khứ.
BEFORE_VISIT nhưng planned_start_at đã qua nhiều ngày.
CLOSED nhưng planned_end_at ở tương lai.
CANCELLED sau DURING_VISIT nếu không có UC đặc biệt.
```

---

## 25. Account Management rules

### 25.1. User status

```text
ACTIVE   → đang hoạt động.
INACTIVE → bị vô hiệu hóa do nghỉ việc/admin disable/không còn dùng.
LOCKED   → bị khóa do bảo mật/sai mật khẩu nhiều lần.
```

Không xóa cứng user đã có lịch sử nghiệp vụ.

### 25.2. Tạo Staff / Staff Leader

```text
HO có thể tạo Staff Leader hoặc IC Staff theo policy.
Staff Leader chỉ được tạo IC Staff thường trong campus mình nếu policy cho phép.
Staff Leader không được tạo Staff Leader khác.
Staff role phải thuộc department_type = IC.
```

### 25.3. Tạo Department Leader / Department Staff

```text
HO tạo Department Leader theo policy đã chốt.
Department Leader là người duy nhất tạo Department Staff trong department mình nếu policy cho phép.
Staff Leader không tạo Department Staff.
Department role phải thuộc department_type = GENERAL.
```

### 25.4. Tạo HO

Nếu chưa chốt policy nhiều HO/campus, không tự suy diễn. Đề xuất an toàn:

```text
Mỗi campus chỉ có một HO chính ACTIVE.
Nếu cần thay HO, dùng flow thay thế có kiểm soát.
Không tạo chồng nhiều HO ACTIVE cùng campus nếu chưa có rule rõ.
```

### 25.5. Manage account status

```text
ACTIVE → INACTIVE: vô hiệu hóa, revoke active sessions.
ACTIVE → LOCKED: khóa bảo mật, revoke session nếu cần.
INACTIVE → ACTIVE: kích hoạt lại nếu role/campus/department vẫn hợp lệ.
LOCKED → ACTIVE: mở khóa sau khi xử lý lý do bảo mật.
```

---

## 26. Backend invariant checklist

Mỗi API delegation/account/logistics quan trọng phải kiểm tra:

```text
[ ] Current user authenticated đúng portal.
[ ] role_code/sub_role hợp lệ.
[ ] Scope campus/department/ownership/participant.
[ ] Request tổng status hợp lệ.
[ ] Campus instance status hợp lệ.
[ ] Host/coordinator/current participant đúng.
[ ] Không cho action khi CLOSED/CANCELLED nếu không có rule riêng.
[ ] Không tin campusId/departmentId/role/status từ frontend.
[ ] Error code rõ: 400/401/403/404/409/422.
[ ] Audit log cho action quan trọng.
[ ] Notification/email nếu nghiệp vụ cần.
[ ] Không trả dữ liệu nhạy cảm.
```

---

## 27. Frontend invariant checklist

Frontend phải:

```text
[ ] Ẩn menu/button theo role/subRole/scope/status.
[ ] Không gọi API vượt scope nếu biết trước user không có quyền.
[ ] Không dùng mock data khi API thật đã có.
[ ] Không tự suy diễn trạng thái bằng text cũ.
[ ] Dùng enum/constants chung.
[ ] Với multi-campus pending HO: Staff Leader/Staff không render instance con.
[ ] Với cancel: chỉ render nút cho Visitor hoặc Host đúng status.
[ ] Với assign host: chỉ hiện Staff thường cùng campus.
[ ] Với form submit: validate GUEST và EXTERNAL_SUPPORT.
[ ] Với time/status: badge hiển thị theo status DB, không tự đổi status trên client.
[ ] Loading/empty/error state đầy đủ.
[ ] Không làm layout tràn ngang/cắt chữ.
```

---

## 28. DB verification queries

### 28.1. Kiểm tra role/subRole sai

```sql
SELECT u.user_id, u.full_name, u.email, r.role_code, u.sub_role
FROM users u
JOIN roles r ON r.role_id = u.role_id
WHERE r.role_code IN ('DEPT','STAFF_LEADER','DEPT_LEADER','DEPARTMENT_LEADER')
   OR (r.role_code IN ('STAFF','DEPARTMENT') AND u.sub_role NOT IN ('STAFF','LEADER'))
   OR (r.role_code NOT IN ('STAFF','DEPARTMENT') AND u.sub_role IS NOT NULL);
```

Kết quả đúng: `0 rows`.

### 28.2. Kiểm tra form thiếu GUEST hoặc EXTERNAL_SUPPORT

```sql
SELECT vr.visit_request_id, vr.request_code,
       SUM(vgm.member_type = 'GUEST') AS guest_count,
       SUM(vgm.member_type = 'EXTERNAL_SUPPORT') AS support_count
FROM visit_requests vr
LEFT JOIN visit_guest_members vgm ON vgm.visit_request_id = vr.visit_request_id
GROUP BY vr.visit_request_id, vr.request_code
HAVING guest_count = 0 OR support_count = 0;
```

Kết quả đúng: `0 rows`.

### 28.3. Kiểm tra multi-campus pending HO bị gắn dữ liệu vận hành

```sql
SELECT vr.visit_request_id, vr.request_code, vrc.visit_instance_id,
       COUNT(DISTINCT vp.participant_id) AS participant_count,
       COUNT(DISTINCT vli.logistics_item_id) AS logistics_count,
       COUNT(DISTINCT ce.calendar_event_id) AS calendar_count
FROM visit_requests vr
JOIN visit_request_campuses vrc ON vrc.visit_request_id = vr.visit_request_id
LEFT JOIN visit_participants vp ON vp.visit_instance_id = vrc.visit_instance_id
LEFT JOIN visit_logistics_items vli ON vli.visit_instance_id = vrc.visit_instance_id
LEFT JOIN calendar_events ce ON ce.visit_instance_id = vrc.visit_instance_id
WHERE vr.visit_scope = 'MULTI_CAMPUS'
  AND vr.status = 'PENDING_APPROVAL'
GROUP BY vr.visit_request_id, vr.request_code, vrc.visit_instance_id
HAVING participant_count > 0 OR logistics_count > 0 OR calendar_count > 0;
```

Kết quả đúng: `0 rows`.

### 28.4. Kiểm tra host không phải IC Staff thường

```sql
SELECT vrc.visit_instance_id, vrc.current_host_user_id, u.email, r.role_code, u.sub_role, d.department_type
FROM visit_request_campuses vrc
JOIN users u ON u.user_id = vrc.current_host_user_id
JOIN roles r ON r.role_id = u.role_id
LEFT JOIN departments d ON d.department_id = u.department_id
WHERE vrc.current_host_user_id IS NOT NULL
  AND NOT (
    r.role_code = 'STAFF'
    AND u.sub_role = 'STAFF'
    AND d.department_type = 'IC'
    AND u.status = 'ACTIVE'
    AND u.primary_campus_id = vrc.campus_id
  );
```

Kết quả đúng: `0 rows`.

---

## 29. Project structure chuẩn

Cấu trúc mục tiêu/hiện tại nên theo hướng:

```text
PEMS/
├── backend/
│   ├── PEMS.Api/
│   ├── PEMS.Application/
│   ├── PEMS.Domain/
│   ├── PEMS.Infrastructure/
│   └── PEMS.SharedKernel/
│
├── frontend/
│   └── pems-react/
│
├── database/
│   ├── scripts/
│   ├── migrations/
│   └── seed/
│
├── docs/
│   ├── use-cases/
│   ├── architecture/
│   ├── api/
│   └── database/
│
├── tests/
│   ├── PEMS.UnitTests/
│   ├── PEMS.ApplicationTests/
│   └── PEMS.IntegrationTests/
│
├── tools/
└── PEMS.sln
```

Không tạo folder rỗng. Nếu tạo folder UC, phải có file thật.

---

## 30. Quy trình khi nhận task mới

Khi nhận task, không code ngay. Làm theo thứ tự:

```text
1. Xác định task thuộc module nào.
2. Xác định UC nào nếu có.
3. Quét file hiện tại liên quan.
4. Đọc SQL/schema/seed liên quan.
5. Đọc canonical business rules liên quan.
6. Đọc frontend page/API/hook liên quan.
7. Xác định backend cần sửa gì.
8. Xác định frontend cần sửa gì.
9. Xác định database có cần patch không.
10. Xác định validation/scope/fixed policy.
11. Lập kế hoạch ngắn.
12. Sửa đúng phạm vi.
13. Build/test.
14. Báo cáo file changed và test result.
```

---

## 31. Checklist triển khai một UC

Mọi UC phải có checklist:

```text
[ ] Xác định đúng UC ID.
[ ] Xác định đúng UC name.
[ ] Xác định actor.
[ ] Xác định route/API contract.
[ ] Xác định role/scope/fixed policy.
[ ] Đọc tài liệu use case/canonical liên quan.
[ ] Đọc SQL/schema liên quan.
[ ] Quét code hiện tại trước khi sửa.
[ ] Xác định bảng DB/entity liên quan.
[ ] Xác định input DTO/query params.
[ ] Xác định output DTO/response.
[ ] Viết input validation.
[ ] Viết business validation.
[ ] Viết scope check server-side.
[ ] Viết anti-spam/rate-limit/idempotency nếu endpoint dễ spam.
[ ] Nối frontend API service.
[ ] Nối frontend type/hook/page nếu cần.
[ ] Xử lý loading/error/empty state.
[ ] Build backend.
[ ] Build frontend.
[ ] Viết test case thủ công/API/UI.
[ ] Cập nhật docs/changelog nếu có thay đổi contract/schema.
```

---

## 32. Build/test rules

Không được báo hoàn thành nếu build fail.

### 32.1. Backend

Chạy:

```bash
dotnet restore
dotnet build
dotnet test
```

Nếu chưa có test project, ghi rõ:

```text
Không tìm thấy test project hoặc chưa cấu hình test.
```

Không báo pass giả.

### 32.2. Frontend

Chạy:

```bash
cd frontend/pems-react
npm install
npm run build
npm run lint
npm run typecheck
```

Nếu không có script `lint` hoặc `typecheck`, ghi rõ:

```text
Script npm run lint không tồn tại.
Script npm run typecheck không tồn tại.
```

Không báo pass nếu script không chạy.

### 32.3. SQL/seed

Với SQL seed/schema, nếu có MySQL local thì chạy import thật. Nếu môi trường không có MySQL, phải nói rõ và vẫn chạy kiểm tra tĩnh nếu có script:

```text
- Kiểm tra số cột/value INSERT.
- Kiểm tra enum whitelist.
- Kiểm tra duplicate unique key theo schema.
- Kiểm tra FK static nếu có thể.
- Kiểm tra không dùng loop/procedure/RAND/INSERT IGNORE.
```

---

## 33. Báo cáo sau khi sửa

Sau mỗi task, báo cáo theo format:

```text
1. Summary
2. Files changed
3. Backend changes
4. Frontend changes
5. Database changes
6. API contract
7. Fixed policy/scope rules
8. Validation rules
9. Manual test cases
10. Build/test result
11. Known limitations
12. TODO / cần xác nhận
```

Nếu có lỗi chưa sửa được:

```text
- Lỗi gì.
- Ở file nào.
- Đã thử gì.
- Cần người dùng cung cấp thêm gì.
```

Không trả lời kiểu “đã xong” mà không có bằng chứng.

---

## 34. Quy tắc viết prompt cho code agent khác

Khi người dùng yêu cầu tạo prompt, prompt phải có:

```text
- Bối cảnh dự án.
- Mục tiêu cụ thể.
- File/phạm vi cần sửa.
- Những thứ không được sửa.
- Quy tắc database-first.
- Quy tắc Clean Architecture.
- Quy tắc role/subRole/fixed policy/scope.
- Quy tắc frontend/UI nếu có.
- Checklist thực hiện.
- Build/test command.
- Output/report mong muốn.
```

Prompt không được chung chung kiểu:

```text
Hãy sửa lỗi UI cho đẹp hơn.
```

Phải rõ kiểu:

```text
Hãy quét file X/Y/Z, xác định nguyên nhân filter bar tràn ngang, chỉ sửa JSX layout/className Tailwind, không đổi API params, không đổi state nghiệp vụ, không đổi fixed policy/scope logic, sau đó build frontend và báo cáo file changed.
```

---

## 35. Phong cách trả lời người dùng

Người dùng muốn câu trả lời:

```text
- Tiếng Việt.
- Rõ ràng.
- Thực tế.
- Dễ copy.
- Không vòng vo.
- Không quá học thuật.
- Có root cause nếu là lỗi.
- Có file cần sửa nếu là code.
- Có prompt hoàn chỉnh nếu yêu cầu prompt.
```

Format ưu tiên:

```text
1. Vấn đề chính
2. Nguyên nhân
3. Cách xử lý
4. File cần sửa
5. Code/prompt hoàn chỉnh
6. Checklist test
```

Không trả lời chung chung.

---

## 36. Quy tắc tuyệt đối

Không được:

```text
- Tạo file rỗng.
- Để NotImplementedException.
- Báo đã xong khi chưa build/test.
- Trả mock data thay DB thật.
- Viết business logic trong Controller.
- Gọi DbContext trực tiếp trong Controller.
- Bỏ fixed policy/scope.
- Bỏ validation.
- Bỏ anti-spam/rate-limit/idempotency cho endpoint dễ spam.
- Làm trắng màn hình frontend.
- Làm layout tràn ngang/cắt chữ.
- Đổi schema bằng code khi chưa có SQL patch.
- Tự thêm role/status/enum/table nếu SQL chưa có.
- Lộ dữ liệu nhạy cảm ra frontend.
- Tự đổi flow nghiệp vụ đã chốt.
- Tự rewrite frontend nếu chỉ được yêu cầu sửa một phần.
- Code theo tài liệu legacy nếu mâu thuẫn với canonical v8.4 refined v6.
```

---

## 37. Quick context ngắn để Claude nhớ

```text
PEMS là hệ thống quản lý tiếp đón đoàn khách/HTQT của FPT University. Dự án dùng React/Vite/TypeScript/Tailwind ở frontend, .NET 8 Clean Architecture/MediatR/FluentValidation/EF Core ở backend, MySQL 8 database-first/manual SQL ở database.

Schema hiện tại là v8.4 refined v6 no dynamic permissions. Không còn permissions/role_permissions runtime DB. Role chuẩn: ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR. Staff Leader = STAFF + LEADER. IC Staff = STAFF + STAFF. Department Leader = DEPARTMENT + LEADER. Department Staff = DEPARTMENT + STAFF. Không dùng DEPT/STAFF_LEADER/DEPT_LEADER làm role_code.

Frontend đã có nhiều màn hình, không rewrite hoặc phá route/UI/flow. UI theo enterprise dashboard: sạch, gọn, không màu mè, không tràn ngang, không cắt chữ.

Backend controller chỉ nhận request, gọi IMediator, trả response. Business logic nằm ở Handler/Domain. Endpoint nghiệp vụ phải check fixed policy và scope server-side.

Submit visit request chỉ tạo request PENDING_APPROVAL và campus instance WAITING_REQUEST_APPROVAL. Form bắt buộc có ít nhất 1 GUEST và 1 EXTERNAL_SUPPORT. Nút “Là tôi” copy người đăng ký thành EXTERNAL_SUPPORT.

visit_requests.status chỉ có PENDING_APPROVAL, APPROVED, REJECTED, CANCELLED. visit_request_campuses.status mới là lifecycle: WAITING_REQUEST_APPROVAL, WAITING_HOST_ASSIGNMENT, ASSIGNED, BEFORE_VISIT, DURING_VISIT, AFTER_VISIT, CLOSED, CANCELLED.

Single-campus: Staff Leader campus duyệt/từ chối. Nếu approve mà chưa gán host thì WAITING_HOST_ASSIGNMENT; sau đó Staff Leader gán IC Staff thường làm host.

Multi-campus: Khi HO chưa duyệt, các campus con không được thấy đoàn trong form đó. Chỉ HO thấy request tổng PENDING_APPROVAL. HO approve xong gán Staff Leader từng campus làm coordinator_user_id, instance sang WAITING_HOST_ASSIGNMENT. Staff Leader từng campus sau đó gán IC Staff thường làm host. Staff Leader không phải host mặc định.

Cancel sau APPROVED chỉ Visitor hoặc Host. Trước duyệt không dùng CANCELLED, dùng REJECTED. Visitor tự hủy dùng SELF_SERVICE. Host hủy thay khách dùng EXTERNAL_CONFIRMATION và cancellation_reason phải ghi rõ kênh/thời điểm/người xác nhận/lý do. Không có Staff Leader/HO internal decision cancel sau APPROVED nếu schema chưa patch.

Seed manual rich được phép dùng CURRENT_DATE/CURRENT_TIMESTAMP/DATE_ADD/DATE_SUB cho planned_start_at/planned_end_at động theo ngày import. Không dùng loop/procedure/RAND/INSERT IGNORE để spam seed.
```

---

## 38. Legacy mapping để đọc tài liệu cũ

Nếu gặp tài liệu cũ ghi các tên sau, map như sau trước khi code:

| Legacy term | Canonical runtime value |
|---|---|
| `STAFF_L`, `Staff_Lead`, `Staff Leader role` | `role_code = STAFF`, `sub_role = LEADER` |
| `STAFF_P`, `Staff`, `IC Staff` | `role_code = STAFF`, `sub_role = STAFF` |
| `DEPT`, `Dept` | `role_code = DEPARTMENT` |
| `DEPT_L`, `Dept Lead` | `role_code = DEPARTMENT`, `sub_role = LEADER` |
| `DEPT_P`, `Dept Staff` | `role_code = DEPARTMENT`, `sub_role = STAFF` |
| “Đã duyệt nhưng chưa có HOST” | `visit_request_campuses.status = WAITING_HOST_ASSIGNMENT` |
| “Staff click nhận đón” | Không còn là flow chuẩn; Staff Leader gán host chính thức |
| “HO duyệt xong auto Staff Leader làm host” | Sai flow mới; HO gán Staff Leader làm coordinator, không phải host |
| “Mỗi campus duyệt lại sau HO” | Sai flow mới; HO duyệt request tổng, Staff Leader chỉ gán host/operate instance |
| “Staff Leader/HO internal decision cancel sau APPROVED” | Không áp dụng schema hiện tại; cần patch schema nếu muốn hỗ trợ |

