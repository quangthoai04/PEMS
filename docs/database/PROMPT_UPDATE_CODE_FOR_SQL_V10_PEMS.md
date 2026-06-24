# PROMPT CẬP NHẬT CODE THEO SQL PEMS v10

> Dùng file này cho AI Agent / Backend Developer / Frontend Developer để cập nhật code sau khi database PEMS đã đổi sang **SQL v10 fresh-create**.
>
> Mục tiêu: đồng bộ **SQL → Entity → Enum/Constants → DbContext/EF Config → DTO → Handler/Service → API → Frontend type/service/UI → Test**.

---

## 0. Nguồn chuẩn phải đọc trước khi code

Trước khi sửa code, bắt buộc đọc các file mới nhất sau:

```text
1. database/scripts/pems_full_create_manual_wide_coverage_seed_v8_4_refined_v6_v10_clean_logistics_handover_fields.sql
2. docs/DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
3. docs/PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
4. docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
5. docs/VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
6. docs/PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
```

Nếu tên file trong repo khác, hãy tìm bản tương ứng trong thư mục tài liệu hoặc bundle v10.

Nguyên tắc ưu tiên:

```text
SQL v10 mới nhất là nguồn chuẩn cho bảng/cột/enum/FK/index.
Canonical Business Rules v10 là nguồn chuẩn cho nghiệp vụ.
Code cũ chỉ là tham khảo, không được giữ logic cũ nếu mâu thuẫn với SQL v10.
```

---

## 1. Tổng quan thay đổi SQL v10 cần đồng bộ code

SQL v10 đã chốt các thay đổi chính sau:

```text
1. faqs
   - Bỏ cột language_code.
   - FAQ chỉ dùng tiếng Việt.
   - Đổi faq_type sang nhóm chức năng hệ thống PEMS.

2. partners
   - Thêm owner_campus_id.
   - Staff Leader chỉ duyệt partner thuộc owner_campus_id = currentUser.primary_campus_id.

3. visit_logistics_items
   - Bỏ các trường ký cũ:
     handover_confirmed_by
     handover_confirmed_at
     handover_note
     service_report_signed_by
     service_report_signed_at
     service_report_file_id
   - Bảng này chỉ còn lưu workflow chính của logistics/resource: request, receive, assign, accept, propose, complete.

4. visit_logistics_item_handovers
   - Bảng mới.
   - Lưu ký mượn/ký trả chi tiết.
   - Mỗi logistics_item_id chỉ có tối đa 1 BORROW và 1 RETURN.
   - Lưu đủ 4 chữ ký và 4 thời điểm ký.

5. email_action_tokens
   - Bảng mới.
   - Xử lý nút bấm trong email: xác nhận, từ chối, thương lượng, phản hồi đề xuất, ký mượn, ký trả.
   - Không làm inbox/mail nhận thật ở giai đoạn này.

6. Không thêm / không code
   - Không thêm email_threads.
   - Không thêm email_messages.
   - Không thêm email_message_recipients.
   - Không thêm visit_logistics_assignment_logs.
   - Không cho chuyển nhiệm vụ logistics từ người A sang người B sau khi đã assigned.
```

---

## 2. Các bảng/cột thay đổi chi tiết

## 2.1. `faqs`

### Code cần cập nhật

Xóa khỏi Entity/DTO/Form/Filter/API:

```text
language_code
LanguageCode
languageCode
idx_faqs_language_status
```

Enum `faq_type` mới:

```text
ACCOUNT_ACCESS
VISIT_REQUEST
DELEGATION_MANAGEMENT
LOGISTICS_RESOURCE
DOCUMENT_MEDIA
NOTIFICATION_EMAIL
OTHER
```

Cột còn lại của `faqs`:

```text
faq_id
faq_type
question
answer
display_order
status
created_at
created_by
updated_at
updated_by
```

### Backend cần sửa

```text
- Entity Faq: bỏ LanguageCode.
- FaqType enum/constant: cập nhật đúng 7 value mới.
- Faq DTO: bỏ languageCode.
- Create/Update FAQ Request: bỏ languageCode.
- Filter/Search FAQ Query: bỏ languageCode filter.
- EF Configuration: bỏ mapping LanguageCode, bỏ index language/status nếu code đang khai báo.
- Validation: faq_type phải nằm trong enum mới.
```

### Frontend cần sửa

```text
- Bỏ dropdown/filter ngôn ngữ FAQ nếu có.
- Bỏ field languageCode trong form create/update FAQ.
- Bỏ column languageCode trong table nếu có.
- Cập nhật FAQ type labels:
  ACCOUNT_ACCESS → Tài khoản & đăng nhập
  VISIT_REQUEST → Đăng ký tham quan
  DELEGATION_MANAGEMENT → Quản lý đoàn
  LOGISTICS_RESOURCE → Hậu cần & mượn tài nguyên
  DOCUMENT_MEDIA → Tài liệu & hình ảnh
  NOTIFICATION_EMAIL → Thông báo & email
  OTHER → Khác
```

---

## 2.2. `partners`

### Cột mới

```text
owner_campus_id BIGINT UNSIGNED NOT NULL
FK: owner_campus_id → campuses(campus_id)
Index:
- idx_partners_owner_status(owner_campus_id, profile_status)
- idx_partners_owner_created(owner_campus_id, created_at)
```

### Ý nghĩa

```text
owner_campus_id = campus sở hữu / quản lý partner request.
Staff Leader chỉ được xem/duyệt partner thuộc campus của mình.
```

### Backend cần sửa

Entity `Partner` thêm:

```csharp
public ulong OwnerCampusId { get; set; }
public Campus OwnerCampus { get; set; } = null!;
```

EF Configuration cần có:

```csharp
builder.Property(x => x.OwnerCampusId)
    .HasColumnName("owner_campus_id")
    .IsRequired();

builder.HasOne(x => x.OwnerCampus)
    .WithMany()
    .HasForeignKey(x => x.OwnerCampusId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(x => new { x.OwnerCampusId, x.ProfileStatus })
    .HasDatabaseName("idx_partners_owner_status");

builder.HasIndex(x => new { x.OwnerCampusId, x.CreatedAt })
    .HasDatabaseName("idx_partners_owner_created");
```

DTO/API cần thêm:

```text
ownerCampusId
ownerCampusCode
ownerCampusName
```

Create Partner:

```text
- Nếu actor là Staff/Staff Leader: owner_campus_id = currentUser.primary_campus_id.
- Không tin ownerCampusId từ frontend nếu actor không được chọn campus.
- Nếu HO/Admin được tạo thay nhiều campus thì phải validate campus ACTIVE.
```

Approve/Reject Partner:

```text
Actor hợp lệ: Staff Leader đúng campus.
Điều kiện bắt buộc:
partner.owner_campus_id == currentUser.primary_campus_id
partner.profile_status == PENDING_APPROVAL
currentUser role = STAFF, sub_role = LEADER
```

Không cần thêm bảng lịch sử duyệt nhiều vòng ở v10.

### Frontend cần sửa

```text
- Partner list/detail hiển thị ownerCampus nếu cần.
- Partner approval list của Staff Leader phải gọi API lọc theo scope backend, không tự lấy toàn bộ.
- Nếu có filter campus cho HO/Admin thì dùng ownerCampusId.
- Staff Leader không được thấy nút Approve/Reject nếu partner.ownerCampusId khác campus mình.
```

---

## 2.3. `visit_logistics_items`

### Các trường đã bị xóa khỏi SQL v10

Phải xóa khỏi Entity/DTO/Form/API/Mapping/Query:

```text
handover_confirmed_by
handover_confirmed_at
handover_note
service_report_signed_by
service_report_signed_at
service_report_file_id
```

Tương ứng C# cần xóa:

```text
HandoverConfirmedBy
HandoverConfirmedAt
HandoverNote
ServiceReportSignedBy
ServiceReportSignedAt
ServiceReportFileId
HandoverConfirmedByUser navigation nếu có
ServiceReportSignedByUser navigation nếu có
ServiceReportFile navigation nếu có
```

### Vai trò còn lại của `visit_logistics_items`

Bảng này chỉ lưu workflow chính:

```text
- item_type, title, description, quantity
- usage_start_at, usage_end_at
- status, priority
- requested_by, requested_to_department_id, requested_at
- received_by, received_at
- assigned_to_user_id, assigned_by, assigned_at
- assignee_accepted_at, assignee_response_note
- due_at, completed_at
- proposed_by, proposed_at, proposed_quantity, proposed_usage_start_at, proposed_usage_end_at, proposed_description, proposal_note
- proposal_responded_by, proposal_responded_at, proposal_response, proposal_response_note
- decision_note
- row_version, created_at, created_by, updated_at, updated_by
```

### Rule không chuyển nhiệm vụ

Backend phải chặn update đổi `assigned_to_user_id` sau khi đã có người được gán:

```text
Nếu old.assigned_to_user_id IS NOT NULL
và request.assigned_to_user_id khác old.assigned_to_user_id
→ trả 409 hoặc 400:
"Nhiệm vụ đã được phân công, không thể chuyển sang người khác."
```

Không tạo UI “chuyển nhiệm vụ”. Nếu UI cũ có nút transfer/reassign thì phải ẩn hoặc xóa luồng.

---

## 2.4. Bảng mới `visit_logistics_item_handovers`

### Cột của bảng

```text
handover_id BIGINT UNSIGNED AUTO_INCREMENT PK
logistics_item_id BIGINT UNSIGNED NOT NULL
handover_type ENUM('BORROW','RETURN') NOT NULL
borrower_signed_by BIGINT UNSIGNED NULL
borrower_signed_at DATETIME NULL
provider_signed_by BIGINT UNSIGNED NULL
provider_signed_at DATETIME NULL
item_condition ENUM('GOOD','DAMAGED','MISSING','OTHER') NULL
condition_note TEXT NULL
attachment_file_id BIGINT UNSIGNED NULL
created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
created_by BIGINT UNSIGNED NULL
```

### Ý nghĩa nghiệp vụ

| handover_type | borrower_signed_by/at | provider_signed_by/at |
|---|---|---|
| `BORROW` | Bên mượn ký nhận | Bên cho mượn ký bàn giao |
| `RETURN` | Bên mượn ký trả | Bên cho mượn ký nhận lại |

Một logistics item có tối đa:

```text
1 row BORROW
1 row RETURN
```

Do unique key:

```text
UNIQUE(logistics_item_id, handover_type)
```

### Entity cần tạo

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

    public VisitLogisticsItem LogisticsItem { get; set; } = null!;
    public User? BorrowerSignedByUser { get; set; }
    public User? ProviderSignedByUser { get; set; }
    public FileEntity? AttachmentFile { get; set; }
    public User? CreatedByUser { get; set; }
}
```

Tên `FileEntity` có thể đổi theo entity file thật trong project.

### Constants/Enums cần tạo

```csharp
public static class LogisticsHandoverTypes
{
    public const string Borrow = "BORROW";
    public const string Return = "RETURN";
}

public static class HandoverItemConditions
{
    public const string Good = "GOOD";
    public const string Damaged = "DAMAGED";
    public const string Missing = "MISSING";
    public const string Other = "OTHER";
}
```

### DbContext cần thêm

```csharp
public DbSet<VisitLogisticsItemHandover> VisitLogisticsItemHandovers => Set<VisitLogisticsItemHandover>();
```

### EF Configuration cần thêm

```csharp
builder.ToTable("visit_logistics_item_handovers");
builder.HasKey(x => x.HandoverId);

builder.Property(x => x.HandoverId).HasColumnName("handover_id");
builder.Property(x => x.LogisticsItemId).HasColumnName("logistics_item_id").IsRequired();
builder.Property(x => x.HandoverType).HasColumnName("handover_type").HasColumnType("enum('BORROW','RETURN')").IsRequired();
builder.Property(x => x.BorrowerSignedBy).HasColumnName("borrower_signed_by");
builder.Property(x => x.BorrowerSignedAt).HasColumnName("borrower_signed_at");
builder.Property(x => x.ProviderSignedBy).HasColumnName("provider_signed_by");
builder.Property(x => x.ProviderSignedAt).HasColumnName("provider_signed_at");
builder.Property(x => x.ItemCondition).HasColumnName("item_condition").HasColumnType("enum('GOOD','DAMAGED','MISSING','OTHER')");
builder.Property(x => x.ConditionNote).HasColumnName("condition_note");
builder.Property(x => x.AttachmentFileId).HasColumnName("attachment_file_id");
builder.Property(x => x.CreatedAt).HasColumnName("created_at");
builder.Property(x => x.CreatedBy).HasColumnName("created_by");

builder.HasIndex(x => new { x.LogisticsItemId, x.HandoverType })
    .IsUnique()
    .HasDatabaseName("uq_logistics_handover_type");

builder.HasOne(x => x.LogisticsItem)
    .WithMany(x => x.Handovers)
    .HasForeignKey(x => x.LogisticsItemId)
    .OnDelete(DeleteBehavior.Cascade);
```

Thêm navigation ở `VisitLogisticsItem`:

```csharp
public ICollection<VisitLogisticsItemHandover> Handovers { get; set; } = new List<VisitLogisticsItemHandover>();
```

### API/Handler cần có

Tùy project đã có module Logistics chưa, tạo hoặc cập nhật các use case sau:

```text
GET    /api/logistics/items/{id}/handovers
POST   /api/logistics/items/{id}/handovers/borrow/borrower-sign
POST   /api/logistics/items/{id}/handovers/borrow/provider-sign
POST   /api/logistics/items/{id}/handovers/return/borrower-sign
POST   /api/logistics/items/{id}/handovers/return/provider-sign
```

Có thể gom thành một endpoint:

```text
POST /api/logistics/items/{id}/handovers/sign
body:
{
  "handoverType": "BORROW" | "RETURN",
  "signerSide": "BORROWER" | "PROVIDER",
  "itemCondition": "GOOD" | "DAMAGED" | "MISSING" | "OTHER" | null,
  "conditionNote": "...",
  "attachmentFileId": 123
}
```

Validation bắt buộc:

```text
- logistics item tồn tại.
- user thuộc scope hợp lệ.
- handoverType chỉ BORROW/RETURN.
- signerSide chỉ BORROWER/PROVIDER.
- Không ký lại nếu field signed_at tương ứng đã có.
- Không tạo RETURN trước khi BORROW đủ chữ ký nếu business yêu cầu.
- Khi RETURN provider ký nhận lại, nên yêu cầu item_condition.
- attachment_file_id nếu có phải tồn tại trong files.
```

---

## 2.5. Bảng mới `email_action_tokens`

### Cột của bảng

```text
email_action_token_id BIGINT UNSIGNED AUTO_INCREMENT PK
token_hash VARCHAR(255) NOT NULL UNIQUE
action_group_key VARCHAR(180) NOT NULL
action_context ENUM(
  'PARTICIPATION_RESPONSE',
  'LOGISTICS_ASSIGNEE_RESPONSE',
  'LOGISTICS_NEGOTIATION',
  'LOGISTICS_PROPOSAL_RESPONSE',
  'LOGISTICS_HANDOVER_SIGNATURE'
) NOT NULL
target_type ENUM(
  'VISIT_PARTICIPANT',
  'LOGISTICS_ITEM',
  'LOGISTICS_HANDOVER'
) NOT NULL
target_id BIGINT UNSIGNED NOT NULL
intended_action ENUM(
  'ACCEPT',
  'DECLINE',
  'NEGOTIATE',
  'APPROVE_PROPOSAL',
  'REJECT_PROPOSAL',
  'CONFIRM_BORROW',
  'CONFIRM_RETURN'
) NOT NULL
recipient_user_id BIGINT UNSIGNED NULL
recipient_email VARCHAR(150) NOT NULL
sent_email_id BIGINT UNSIGNED NULL
sent_email_recipient_id BIGINT UNSIGNED NULL
expires_at DATETIME NOT NULL
used_at DATETIME NULL
used_action VARCHAR(50) NULL
result_status ENUM('PENDING','SUCCESS','ALREADY_RESPONDED','EXPIRED','INVALID','FAILED') NOT NULL DEFAULT 'PENDING'
result_message VARCHAR(500) NULL
used_ip VARCHAR(45) NULL
used_user_agent VARCHAR(500) NULL
created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
```

### Ý nghĩa

Bảng này thay cho chức năng nhận mail ở giai đoạn này.

```text
PEMS gửi email
→ email có nút/link hành động
→ người nhận bấm link
→ backend validate token
→ update bảng nghiệp vụ
→ đánh dấu email_action_tokens.used_at/result_status
```

Không đọc inbox/mail reply tự do.

### Entity cần tạo

```csharp
public class EmailActionToken
{
    public ulong EmailActionTokenId { get; set; }
    public string TokenHash { get; set; } = default!;
    public string ActionGroupKey { get; set; } = default!;
    public string ActionContext { get; set; } = default!;
    public string TargetType { get; set; } = default!;
    public ulong TargetId { get; set; }
    public string IntendedAction { get; set; } = default!;

    public ulong? RecipientUserId { get; set; }
    public string RecipientEmail { get; set; } = default!;
    public ulong? SentEmailId { get; set; }
    public ulong? SentEmailRecipientId { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedAction { get; set; }
    public string ResultStatus { get; set; } = "PENDING";
    public string? ResultMessage { get; set; }
    public string? UsedIp { get; set; }
    public string? UsedUserAgent { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? RecipientUser { get; set; }
    public SentEmail? SentEmail { get; set; }
    public SentEmailRecipient? SentEmailRecipient { get; set; }
}
```

### Constants cần tạo

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

public static class EmailIntendedActions
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

### Token security rule

```text
- Không lưu raw token trong DB.
- Chỉ lưu token_hash.
- Link email chứa raw token một lần.
- Khi request tới public endpoint, hash token rồi so sánh.
- Token phải có expires_at.
- Token đã used_at thì không update nghiệp vụ lần nữa.
- Nếu target đã phản hồi rồi, trả thông báo “Bạn đã trả lời rồi.” và set result_status = ALREADY_RESPONDED nếu phù hợp.
```

### API public cần có

```text
GET  /public/email-actions/{token}
POST /public/email-actions/{token}/confirm
POST /public/email-actions/{token}/decline
GET  /public/email-actions/{token}/negotiate
POST /public/email-actions/{token}/negotiate
```

Hoặc route tương đương theo convention project.

Các endpoint này có thể `AllowAnonymous`, nhưng handler phải validate token cực chặt.

### Luồng xử lý token

```text
1. Hash raw token.
2. Tìm email_action_tokens.token_hash.
3. Nếu không có → INVALID.
4. Nếu expires_at < now → EXPIRED.
5. Nếu used_at != null → trả trang đã phản hồi.
6. Load target theo target_type/target_id.
7. Kiểm tra target còn ở trạng thái cho phép phản hồi.
8. Apply action vào bảng nghiệp vụ.
9. Set used_at, used_action, used_ip, used_user_agent, result_status, result_message.
10. Commit transaction.
11. Trả public result page/message.
```

### Mapping action sang bảng nghiệp vụ

#### A. `PARTICIPATION_RESPONSE`

Target:

```text
target_type = VISIT_PARTICIPANT
target_id = visit_participants.participant_id
```

Action:

```text
ACCEPT  → visit_participants.status = ACCEPTED, responded_at = now
DECLINE → visit_participants.status = DECLINED, responded_at = now
```

Nếu status không còn `INVITED`:

```text
Không update.
Trả “Bạn đã trả lời lời mời này rồi.”
```

#### B. `LOGISTICS_ASSIGNEE_RESPONSE`

Target:

```text
target_type = LOGISTICS_ITEM
target_id = visit_logistics_items.logistics_item_id
```

Action:

```text
ACCEPT  → status = ACCEPTED, assignee_accepted_at = now
DECLINE → status = REJECTED, decision_note hoặc assignee_response_note = lý do nếu có
```

#### C. `LOGISTICS_NEGOTIATION`

Target:

```text
target_type = LOGISTICS_ITEM
```

Action:

```text
NEGOTIATE → mở public form nhập đề xuất.
Submit form → status = CHANGE_PROPOSED, fill proposed_* và proposal_note.
```

#### D. `LOGISTICS_PROPOSAL_RESPONSE`

Target:

```text
target_type = LOGISTICS_ITEM
```

Action:

```text
APPROVE_PROPOSAL → proposal_response = ACCEPTED, proposal_responded_at = now
REJECT_PROPOSAL  → proposal_response = REJECTED, proposal_responded_at = now
```

#### E. `LOGISTICS_HANDOVER_SIGNATURE`

Target:

```text
target_type = LOGISTICS_HANDOVER
target_id = visit_logistics_item_handovers.handover_id
```

Action:

```text
CONFIRM_BORROW → ký cho handover_type = BORROW
CONFIRM_RETURN → ký cho handover_type = RETURN
```

Backend phải xác định người ký là borrower hay provider theo token/action_context/recipient_user_id/recipient_email hoặc theo link được tạo.
Nếu cần phân biệt rõ bên ký trong link, có thể dùng `intended_action` + `action_group_key` hoặc bổ sung trong route/form, nhưng không tự thêm cột DB nếu chưa chốt.

---

## 3. Backend update checklist

## 3.1. Entity/Domain

Cập nhật/tạo các entity sau:

```text
[ ] Faq: bỏ LanguageCode, cập nhật FaqType.
[ ] Partner: thêm OwnerCampusId + OwnerCampus navigation.
[ ] VisitLogisticsItem: xóa 6 field ký cũ + navigation cũ.
[ ] VisitLogisticsItem: thêm ICollection<VisitLogisticsItemHandover> Handovers.
[ ] VisitLogisticsItemHandover: entity mới.
[ ] EmailActionToken: entity mới.
[ ] SentEmail / SentEmailRecipient: thêm navigation nếu cần tới EmailActionToken.
```

## 3.2. Enum/Constants

Tạo/cập nhật constants:

```text
[ ] FaqTypes
[ ] LogisticsHandoverTypes
[ ] HandoverItemConditions
[ ] EmailActionContexts
[ ] EmailActionTargetTypes
[ ] EmailIntendedActions
[ ] EmailActionResultStatuses
```

Kiểm tra không còn enum cũ:

```text
PROGRAM
TUITION_FEE
VISA
DORMITORY
SECURITY nếu dùng làm faq_type
LOGISTICS nếu dùng làm faq_type
language_code trong FAQ
```

## 3.3. DbContext / EF Configuration

```text
[ ] Add DbSet<VisitLogisticsItemHandover>.
[ ] Add DbSet<EmailActionToken>.
[ ] Add configuration for visit_logistics_item_handovers.
[ ] Add configuration for email_action_tokens.
[ ] Update PartnerConfiguration: owner_campus_id + FK + indexes.
[ ] Update FaqConfiguration: remove language_code + old language index.
[ ] Update VisitLogisticsItemConfiguration: remove 6 old signing fields/FKs/indexes.
[ ] Check all column names are snake_case and match SQL.
[ ] Check enum column SQL types match SQL exactly if project maps as string.
```

## 3.4. DTO / Request / Response

```text
[ ] FAQ DTOs remove languageCode.
[ ] FAQ create/update requests remove languageCode.
[ ] FAQ list/search query remove language filter.
[ ] Partner DTOs add ownerCampusId/ownerCampusName/ownerCampusCode if UI needs.
[ ] Partner create/update requests handle ownerCampusId per actor rule.
[ ] Logistics item DTO remove old handover/service report fields.
[ ] Logistics item detail DTO add handovers list.
[ ] Add VisitLogisticsItemHandoverDto.
[ ] Add SignHandoverCommand/Request.
[ ] Add EmailActionToken DTO only for internal admin/debug if needed; public response must not leak token_hash.
[ ] Add public email action request/response DTO.
```

Không trả ra frontend:

```text
token_hash
raw token
provider credentials
password_hash
refresh_token_hash
```

## 3.5. Application Handlers / Services

Cập nhật các module:

```text
[ ] FAQ handlers: remove language logic.
[ ] Partner handlers: scope by owner_campus_id.
[ ] Partner approval handlers: Staff Leader only same owner campus.
[ ] Logistics handlers: remove old signing logic from visit_logistics_items.
[ ] Logistics assignment handlers: block reassignment after assigned_to_user_id exists.
[ ] Logistics handover handlers: create/update BORROW/RETURN rows and signatures.
[ ] Email sending service: generate email_action_tokens and inject action links into email body.
[ ] Public email action handlers: validate token and update target tables.
[ ] Audit logging: log email action and handover signing.
[ ] Notification/email: send follow-up notifications if action changes business state.
```

## 3.6. Controllers / Routes

Cập nhật hoặc tạo controller:

```text
[ ] FaqsController: remove language query/body.
[ ] PartnersController: add owner campus fields in response and enforce scope.
[ ] LogisticsController or DelegationsController: add handover endpoints.
[ ] PublicEmailActionsController: AllowAnonymous token endpoints.
[ ] EmailsController: keep sent email/outbox only; do not create inbox endpoints unless later phase.
```

Không tạo các endpoint sau ở v10:

```text
GET /api/emails/inbox
GET /api/emails/threads
GET /api/emails/messages
POST /api/logistics/items/{id}/transfer
POST /api/logistics/items/{id}/reassign
```

---

## 4. Frontend update checklist

## 4.1. Types/Constants

Cập nhật TypeScript constants:

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

export const LOGISTICS_HANDOVER_TYPES = {
  BORROW: 'BORROW',
  RETURN: 'RETURN',
} as const;

export const HANDOVER_ITEM_CONDITIONS = {
  GOOD: 'GOOD',
  DAMAGED: 'DAMAGED',
  MISSING: 'MISSING',
  OTHER: 'OTHER',
} as const;

export const EMAIL_ACTION_CONTEXTS = {
  PARTICIPATION_RESPONSE: 'PARTICIPATION_RESPONSE',
  LOGISTICS_ASSIGNEE_RESPONSE: 'LOGISTICS_ASSIGNEE_RESPONSE',
  LOGISTICS_NEGOTIATION: 'LOGISTICS_NEGOTIATION',
  LOGISTICS_PROPOSAL_RESPONSE: 'LOGISTICS_PROPOSAL_RESPONSE',
  LOGISTICS_HANDOVER_SIGNATURE: 'LOGISTICS_HANDOVER_SIGNATURE',
} as const;
```

## 4.2. FAQ UI

```text
[ ] Bỏ languageCode khỏi form state.
[ ] Bỏ language filter/dropdown/table column.
[ ] Cập nhật labels FAQ type mới.
[ ] Validate chỉ enum mới.
[ ] API service không gửi languageCode.
```

## 4.3. Partner UI

```text
[ ] Partner list/detail có thể hiển thị Campus sở hữu.
[ ] Approval queue của Staff Leader chỉ hiển thị partner trong campus mình.
[ ] Button Approve/Reject dựa trên canApprove/canReject từ backend hoặc ownerCampusId check phụ ở frontend.
[ ] Form create partner không cho Staff tự chọn ownerCampusId nếu policy không cho.
```

## 4.4. Logistics UI

```text
[ ] Remove old handover fields from logistics item display/form.
[ ] Add handover section in logistics item detail:
    - BORROW: borrower signed, provider signed, timestamps
    - RETURN: borrower signed, provider signed, timestamps, condition
[ ] Add sign buttons according to backend canAction flags.
[ ] Hide transfer/reassign task action.
[ ] Show conflict message if backend returns “Nhiệm vụ đã được phân công, không thể chuyển sang người khác.”
```

## 4.5. Email management UI

Giai đoạn v10 không làm inbox thật.

```text
[ ] Email module chỉ hiển thị sent/outbox/delivery tracking.
[ ] Có thể hiển thị trạng thái phản hồi qua email_action_tokens nếu API hỗ trợ.
[ ] Không hiển thị tab Mail nhận/Inbox nếu chưa có API thật.
[ ] Không gọi Gmail API/inbox từ frontend.
```

## 4.6. Public email action pages

Tạo các page không cần login:

```text
/public/email-actions/:token
/public/email-actions/:token/negotiate
/public/email-actions/result
```

UI cần có:

```text
- Loading validate token.
- Success state.
- Already responded state: “Bạn đã trả lời rồi.”
- Expired state: “Liên kết đã hết hạn.”
- Invalid state: “Liên kết không hợp lệ.”
- Negotiation form nếu action NEGOTIATE.
```

Không bắt user đăng nhập để bấm email action token.

---

## 5. Validation nghiệp vụ cần enforce

## 5.1. FAQ

```text
- question not empty, max 500.
- answer not empty.
- faq_type thuộc enum mới.
- status chỉ PUBLISHED/HIDDEN.
- Không nhận languageCode trong request.
```

## 5.2. Partner approval

```text
- Actor là Staff Leader.
- Actor primary_campus_id != null.
- partner.owner_campus_id == actor.primary_campus_id.
- profile_status phải PENDING_APPROVAL khi approve/reject.
- reviewed_by = actor.user_id.
- reviewed_at = now.
- review_note bắt buộc khi REJECTED.
```

## 5.3. Logistics assignment

```text
- requested_to_department_id thuộc cùng campus với visit_instance.
- Department Leader chỉ nhận/assign item thuộc department mình.
- Department Staff chỉ xử lý item được assigned_to_user_id = currentUserId.
- Không chuyển nhiệm vụ sau khi assigned_to_user_id đã có.
```

## 5.4. Handover signing

```text
- Một logistics item có tối đa một BORROW và một RETURN.
- Không ký đè nếu signed_at đã có.
- RETURN không nên hoàn tất nếu BORROW chưa đủ chữ ký.
- condition_note bắt buộc nếu item_condition = DAMAGED/MISSING/OTHER.
- Khi cả provider/borrower đã ký RETURN thì item có thể DONE nếu business flow cho phép.
```

## 5.5. Email action token

```text
- Token không tồn tại → INVALID.
- Token hết hạn → EXPIRED.
- Token đã dùng → ALREADY_RESPONDED hoặc message đã trả lời.
- Target không tồn tại → INVALID hoặc FAILED.
- Target không còn ở trạng thái chờ phản hồi → ALREADY_RESPONDED.
- Token action không khớp target state → FAILED.
- Mọi update phải nằm trong transaction.
```

---

## 6. Search toàn repo và sửa/xóa pattern cũ

Chạy search các keyword sau:

```text
language_code
LanguageCode
languageCode
PROGRAM
TUITION_FEE
VISA
DORMITORY
handover_confirmed_by
HandoverConfirmedBy
handover_confirmed_at
HandoverConfirmedAt
handover_note
HandoverNote
service_report_signed_by
ServiceReportSignedBy
service_report_signed_at
ServiceReportSignedAt
service_report_file_id
ServiceReportFileId
email_threads
email_messages
email_message_recipients
inbox
mail nhận
received_emails
transfer logistics
reassign logistics
visit_logistics_assignment_logs
```

Quy tắc xử lý:

```text
- Nếu nằm trong legacy docs: có thể giữ nhưng phải đánh dấu legacy nếu file đó dùng cho code.
- Nếu nằm trong runtime backend/frontend: phải sửa/xóa.
- Nếu nằm trong migration cũ không dùng nữa: đảm bảo không được import nhầm.
```

---

## 7. Test checklist bắt buộc

## 7.1. Backend build/test

```bash
dotnet restore
dotnet build
```

Nếu có test:

```bash
dotnet test
```

## 7.2. Frontend build/test

```bash
cd frontend/pems-react
npm install
npm run build
```

Nếu có lint/typecheck:

```bash
npm run lint
npm run typecheck
```

## 7.3. Manual test cases

### FAQ

```text
[ ] Create FAQ không gửi languageCode vẫn thành công.
[ ] Filter FAQ theo type mới hoạt động.
[ ] Không còn type PROGRAM/TUITION_FEE/VISA/DORMITORY trên UI.
```

### Partner

```text
[ ] Staff Leader HN chỉ thấy partner owner_campus_id = HN.
[ ] Staff Leader HCM không duyệt được partner HN bằng direct API.
[ ] Reject partner bắt buộc review_note.
[ ] Approved partner ghi reviewed_by/reviewed_at.
```

### Logistics item

```text
[ ] Detail logistics item không còn field ký cũ.
[ ] Assign lần đầu thành công.
[ ] Assign lần hai sang người khác bị chặn.
[ ] Department Staff chỉ thấy item được giao.
```

### Handover

```text
[ ] BORROW borrower ký nhận thành công.
[ ] BORROW provider ký bàn giao thành công.
[ ] RETURN borrower ký trả thành công.
[ ] RETURN provider ký nhận lại thành công.
[ ] Ký lại cùng bên bị chặn.
[ ] item_condition DAMAGED/MISSING/OTHER yêu cầu note.
```

### Email action token

```text
[ ] Token ACCEPT participation đổi visit_participants.status = ACCEPTED.
[ ] Sau ACCEPT, bấm DECLINE cùng lời mời trả “Bạn đã trả lời rồi.”
[ ] Token hết hạn trả expired page.
[ ] Token sai trả invalid page.
[ ] NEGOTIATE mở form public, submit xong update proposed_*.
[ ] CONFIRM_BORROW/CONFIRM_RETURN cập nhật handover signature.
```

### Email module

```text
[ ] Email list chỉ hiển thị sent/outbox/delivery/action response.
[ ] Không có tab inbox thật nếu chưa implement.
[ ] Không gọi Gmail inbox API.
```

---

## 8. Definition of Done

Chỉ báo hoàn thành khi đủ:

```text
[ ] Backend entity khớp SQL v10.
[ ] EF configuration khớp SQL v10.
[ ] DbContext có bảng mới.
[ ] Không còn field bị xóa trong runtime code.
[ ] Enum/constants khớp SQL v10.
[ ] DTO/API không nhận/trả field cũ.
[ ] Business validation đúng rule v10.
[ ] Frontend type/service/UI đã cập nhật.
[ ] Không còn chức năng chuyển nhiệm vụ logistics.
[ ] Không có inbox/mail nhận thật ở v10.
[ ] Public email action token hoạt động không cần login.
[ ] Build backend pass.
[ ] Build frontend pass nếu sửa frontend.
[ ] Manual test checklist có kết quả.
[ ] Báo cáo rõ file changed, test result, known limitations.
```

---

## 9. Báo cáo sau khi code xong

AI Agent phải trả report theo format:

```markdown
# Báo cáo cập nhật code theo SQL v10

## 1. Summary

## 2. Database alignment
- SQL version used:
- Tables added:
- Columns removed:
- Columns added:

## 3. Backend files changed
- Entity:
- Enum/Constants:
- DbContext/Configuration:
- DTO/Requests/Responses:
- Handlers/Services:
- Controllers:

## 4. Frontend files changed
- Types/constants:
- API services:
- Pages/components:
- Route/public pages:

## 5. Business rules implemented

## 6. Removed legacy code

## 7. Build/test results

## 8. Manual test results

## 9. Known limitations / next phase
```

---

## 10. Lưu ý quan trọng cho giai đoạn sau

```text
- Nếu sau này muốn nhận mail thật/inbox/thread thì cần thiết kế phase mới với email_threads/email_messages/email_message_recipients và Gmail API/IMAP sync.
- Không tự thêm các bảng đó trong v10.
- Nếu sau này muốn chuyển nhiệm vụ logistics thì cần UC/schema riêng, không tự dùng audit_logs để làm transfer workflow chính.
- Nếu sau này muốn nhiều vòng duyệt partner thì cần partner approval request/history table riêng.
```
