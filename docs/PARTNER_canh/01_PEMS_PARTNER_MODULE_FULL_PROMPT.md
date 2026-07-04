# 01 — PEMS Partner Module Full Implementation Prompt

> Mục tiêu: code full module **Partner Management** cho PEMS: quản lý đối tác, tạo/sửa/xem chi tiết, duyệt/từ chối, người liên hệ, tài liệu, alias, match partner, và link khách/người tham gia biên bản với partner.

---

## 1. Vai trò của AI/code agent

Bạn là Senior Full-stack Developer cho PEMS. Khi triển khai module này, phải làm đồng bộ:

```text
SQL patch
→ Domain Entity
→ EF Configuration
→ DbContext
→ DTO/Request/Response
→ Validator
→ MediatR Command/Query Handler
→ Controller route
→ Authorization/scope check
→ Notification/audit log
→ Frontend type/API service
→ Frontend page/component/modal
→ Test backend/frontend
```

Không báo hoàn thành nếu chỉ sửa một layer.

---

## 2. Quy tắc nghiệp vụ Partner

### 2.1. Role/scope chính

```text
ADMIN
- Quản trị kỹ thuật. Có thể xem toàn bộ dữ liệu nếu hệ thống cho phép, nhưng không phải actor nghiệp vụ chính của Partner approval.

HO
- Có thể xem danh sách partner nội bộ/toàn hệ thống theo nhu cầu monitor.
- Không phải actor duyệt partner nếu giữ rule owner_campus_id theo Staff Leader.

STAFF + LEADER
- Staff Leader campus.
- Xem partner cùng owner_campus_id.
- Duyệt/từ chối partner PENDING_APPROVAL cùng owner_campus_id.
- Tạo/sửa contact trong campus scope nếu UI cho phép.

STAFF + STAFF
- IC Staff.
- Tạo partner mới ở trạng thái PENDING_APPROVAL.
- Backend tự set owner_campus_id = currentUser.primary_campus_id.
- Thêm/sửa contact trong partner thuộc scope.
- Link khách trong visit/biên bản với partner nếu user có quyền với visit đó.

DEPARTMENT/STUDENT
- Không quản lý partner toàn hệ thống.
- Chỉ xem partner approved/internal nếu có use case cần hoặc là participant hợp lệ.

VISITOR/PUBLIC
- Chỉ thấy partner APPROVED + PUBLIC qua public endpoint.
```

### 2.2. Partner status

Runtime enum dùng DB value, label tiếng Việt chỉ dùng UI:

```text
DRAFT              → Bản nháp
PENDING_APPROVAL   → Chờ duyệt
APPROVED           → Đã duyệt
REJECTED           → Từ chối
```

### 2.3. Visibility

```text
PRIVATE  → chỉ nội bộ có quyền/scope
INTERNAL → nội bộ PEMS
PUBLIC   → public page, chỉ cho phép nếu profile_status = APPROVED
```

### 2.4. Quy tắc bắt buộc

```text
- Frontend không được tự truyền owner_campus_id.
- Backend tự set owner_campus_id = currentUser.primary_campus_id khi Staff tạo partner.
- Staff Leader chỉ duyệt/từ chối partner cùng owner_campus_id.
- Từ chối partner bắt buộc có review_note/reason.
- Không cho PUBLIC partner nếu chưa APPROVED.
- Không tạo contact trùng email trong cùng partner.
- Không hard delete contact đã có liên kết nghiệp vụ; dùng status INACTIVE.
- Một partner chỉ có một primary contact ACTIVE.
- Public endpoint không trả PENDING_APPROVAL/REJECTED/PRIVATE.
```

---

## 3. SQL cần cập nhật cho Partner

Tạo patch SQL riêng:

```text
docs/database/scripts/patch_partner_module.sql
```

### 3.1. Không tạo lại bảng đã có

Tận dụng các bảng hiện có:

```text
partners
partner_contacts
documents
files
visit_requests
visit_request_campuses
visit_guest_members
minute_participants
notifications
audit_logs
users
campuses
```

### 3.2. Tạo bảng `partner_aliases`

Mục đích: lưu tên gọi khác của partner để match chính xác hơn.

```sql
CREATE TABLE partner_aliases (
  partner_alias_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  partner_id BIGINT UNSIGNED NOT NULL,

  alias_name VARCHAR(255) NOT NULL,
  alias_name_key VARCHAR(300) NOT NULL COMMENT 'Normalized key: lower-case, remove accents, punctuation, repeated spaces',

  source ENUM('MANUAL','OCR','AUTO_MATCH','IMPORT') NOT NULL DEFAULT 'MANUAL',
  status ENUM('ACTIVE','INACTIVE') NOT NULL DEFAULT 'ACTIVE',

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (partner_alias_id),
  UNIQUE KEY uq_partner_alias_key (partner_id, alias_name_key),
  KEY idx_partner_alias_lookup (alias_name_key, status),
  KEY idx_partner_alias_partner (partner_id),

  CONSTRAINT fk_partner_alias_partner
    FOREIGN KEY (partner_id) REFERENCES partners(partner_id)
    ON UPDATE CASCADE ON DELETE CASCADE,

  CONSTRAINT fk_partner_alias_created_by
    FOREIGN KEY (created_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_partner_alias_updated_by
    FOREIGN KEY (updated_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Alternative names of partners for matching organization names from guests/OCR.';
```

### 3.3. Tạo bảng `visit_guest_partner_links`

Mục đích: lưu khách/người tham gia biên bản đang được gắn với partner nào.

```sql
CREATE TABLE visit_guest_partner_links (
  link_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

  visit_request_id BIGINT UNSIGNED NOT NULL,
  visit_instance_id BIGINT UNSIGNED NULL,

  guest_member_id BIGINT UNSIGNED NULL,
  minute_participant_id BIGINT UNSIGNED NULL,

  partner_id BIGINT UNSIGNED NOT NULL,
  partner_contact_id BIGINT UNSIGNED NULL,

  match_source ENUM(
    'AUTO_NAME',
    'AUTO_EMAIL_DOMAIN',
    'MANUAL',
    'CREATED_FROM_GUEST',
    'BUSINESS_CARD_OCR'
  ) NOT NULL DEFAULT 'MANUAL',

  match_status ENUM('SUGGESTED','CONFIRMED','REJECTED') NOT NULL DEFAULT 'CONFIRMED',

  confidence_score DECIMAL(5,2) NULL,
  note TEXT NULL,

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  updated_at DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  updated_by BIGINT UNSIGNED NULL,

  PRIMARY KEY (link_id),

  KEY idx_vgpl_visit_request (visit_request_id),
  KEY idx_vgpl_visit_instance (visit_instance_id),
  KEY idx_vgpl_guest_member (guest_member_id),
  KEY idx_vgpl_minute_participant (minute_participant_id),
  KEY idx_vgpl_partner (partner_id),
  KEY idx_vgpl_contact (partner_contact_id),
  KEY idx_vgpl_status (match_status),
  KEY idx_vgpl_source (match_source),

  CONSTRAINT fk_vgpl_visit_request
    FOREIGN KEY (visit_request_id) REFERENCES visit_requests(visit_request_id)
    ON UPDATE CASCADE ON DELETE CASCADE,

  CONSTRAINT fk_vgpl_visit_instance
    FOREIGN KEY (visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE CASCADE,

  CONSTRAINT fk_vgpl_guest_member
    FOREIGN KEY (guest_member_id) REFERENCES visit_guest_members(guest_member_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_vgpl_minute_participant
    FOREIGN KEY (minute_participant_id) REFERENCES minute_participants(minute_participant_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_vgpl_partner
    FOREIGN KEY (partner_id) REFERENCES partners(partner_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,

  CONSTRAINT fk_vgpl_partner_contact
    FOREIGN KEY (partner_contact_id) REFERENCES partner_contacts(contact_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_vgpl_created_by
    FOREIGN KEY (created_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_vgpl_updated_by
    FOREIGN KEY (updated_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CHECK (guest_member_id IS NOT NULL OR minute_participant_id IS NOT NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Links visit guests/minute participants to partner profiles for partner labels and partner history.';
```

---

## 4. Backend structure

### 4.1. Domain entities

Tạo/cập nhật trong:

```text
backend/PEMS.Domain/Entities/
```

Cần có:

```text
Partner.cs
PartnerContact.cs
PartnerAlias.cs
VisitGuestPartnerLink.cs
Document.cs
File.cs
```

### 4.2. EF configurations

Tạo/cập nhật trong:

```text
backend/PEMS.Infrastructure/Persistence/Configurations/
```

Cần có:

```text
PartnerConfiguration.cs
PartnerContactConfiguration.cs
PartnerAliasConfiguration.cs
VisitGuestPartnerLinkConfiguration.cs
```

### 4.3. Application folders

Đề xuất:

```text
backend/PEMS.Application/Partners/
  Commands/
    CreatePartner/
    UpdatePartner/
    ApprovePartner/
    RejectPartner/
    CreatePartnerFromGuest/
  Queries/
    GetPartners/
    GetPartnerDetail/
    GetPendingPartnerApprovals/
    MatchPartner/
  Contacts/
    Commands/
      CreatePartnerContact/
      UpdatePartnerContact/
      DeactivatePartnerContact/
      SetPrimaryPartnerContact/
    Queries/
      GetPartnerContacts/
  Aliases/
    Commands/
      CreatePartnerAlias/
      DeactivatePartnerAlias/
    Queries/
      GetPartnerAliases/
  Documents/
    Commands/
      UploadPartnerDocument/
    Queries/
      GetPartnerDocuments/
  VisitLinks/
    Commands/
      CreateOrUpdateVisitGuestPartnerLink/
      RejectVisitGuestPartnerSuggestion/
    Queries/
      GetVisitGuestPartnerLinks/
```

---

## 5. Backend API endpoints

### 5.1. Partner main

```text
GET    /api/partners
GET    /api/partners/{partnerId}
POST   /api/partners
PUT    /api/partners/{partnerId}
POST   /api/partners/{partnerId}/approve
POST   /api/partners/{partnerId}/reject
GET    /api/partners/pending-approvals
GET    /api/partners/match
```

### 5.2. Partner contacts

```text
GET    /api/partners/{partnerId}/contacts
POST   /api/partners/{partnerId}/contacts
PUT    /api/partners/{partnerId}/contacts/{contactId}
DELETE /api/partners/{partnerId}/contacts/{contactId}
POST   /api/partners/{partnerId}/contacts/{contactId}/set-primary
```

### 5.3. Partner aliases

```text
GET    /api/partners/{partnerId}/aliases
POST   /api/partners/{partnerId}/aliases
DELETE /api/partners/{partnerId}/aliases/{aliasId}
```

### 5.4. Partner documents

```text
GET  /api/partners/{partnerId}/documents
POST /api/partners/{partnerId}/documents
```

Use existing `documents` table:

```text
documents.owner_type = PARTNER
documents.owner_id = partnerId
```

### 5.5. Visit/meeting partner links

```text
GET  /api/visit-instances/{visitInstanceId}/partner-links
POST /api/visit-instances/{visitInstanceId}/partner-links
PUT  /api/visit-instances/{visitInstanceId}/partner-links/{linkId}
POST /api/visit-instances/{visitInstanceId}/partner-links/{linkId}/reject-suggestion
```

### 5.6. Public partner

```text
GET /api/public/partners
GET /api/public/partners/{partnerIdOrSlug}
```

Only return:

```text
profile_status = APPROVED
visibility = PUBLIC
```

---

## 6. DTO examples

### 6.1. Partner list item

```ts
export interface PartnerListItemDto {
  partnerId: number;
  partnerCode?: string | null;
  name: string;
  shortName?: string | null;
  country?: string | null;
  ownerCampusId: number;
  ownerCampusName: string;
  creatorName?: string | null;
  profileStatus: 'DRAFT' | 'PENDING_APPROVAL' | 'APPROVED' | 'REJECTED';
  visibility: 'PRIVATE' | 'INTERNAL' | 'PUBLIC';
  createdAt: string;
  reviewedAt?: string | null;
}
```

### 6.2. Create partner request

```ts
export interface CreatePartnerRequest {
  partnerCode?: string | null;
  name: string;
  shortName?: string | null;
  country?: string | null;
  city?: string | null;
  websiteUrl?: string | null;
  address?: string | null;
  description?: string | null;
  partnerType?: string | null;
  visibility?: 'PRIVATE' | 'INTERNAL' | 'PUBLIC';
  logoFileId?: number | null;
  coverFileId?: number | null;
  source?: 'MANUAL' | 'FROM_GUEST' | 'BUSINESS_CARD_OCR';
  initialContact?: CreatePartnerContactRequest | null;
}
```

### 6.3. Partner contact

```ts
export interface PartnerContactDto {
  contactId: number;
  partnerId: number;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  jobTitle?: string | null;
  departmentName?: string | null;
  sourceType: 'MANUAL' | 'BUSINESS_CARD_OCR' | 'IMPORT';
  scannedCardFileId?: number | null;
  ocrConfidence?: number | null;
  isPrimary: boolean;
  status: 'ACTIVE' | 'INACTIVE';
}
```

### 6.4. Partner match response

```ts
export interface PartnerMatchDto {
  matchStatus: 'NONE' | 'SUGGESTED' | 'PENDING_APPROVAL' | 'APPROVED';
  partnerId?: number;
  partnerName?: string;
  profileStatus?: 'DRAFT' | 'PENDING_APPROVAL' | 'APPROVED' | 'REJECTED';
  confidence?: number;
  reason?: string;
}
```

---

## 7. Partner matching logic

Không match đơn giản bằng `string.Contains`. Làm theo thứ tự:

```text
1. Normalize organization/name:
   - lower-case
   - trim
   - remove accents
   - remove punctuation
   - collapse spaces
   - remove common suffix/prefix if safe: university, college, institute, co ltd, jsc, corporation...

2. Exact match alias:
   partner_aliases.alias_name_key == normalized organization

3. Exact/near match partners.name, partners.short_name.

4. Email domain match:
   email domain from contact → compare with partner website domain or known alias/domain.

5. Confidence:
   - >= 90: strong suggestion
   - 70-89: suggested, user must confirm
   - < 70: NONE
```

Response reason examples:

```text
Matched by alias
Matched by email domain
Matched by normalized name
Possible fuzzy match
No matching partner found
```

---

## 8. Validation rules

### 8.1. Partner validation

```text
[ ] name required.
[ ] partnerCode unique if provided.
[ ] websiteUrl valid if provided.
[ ] visibility PUBLIC only when profile_status APPROVED.
[ ] reject reason required.
[ ] owner_campus_id cannot be set by frontend.
[ ] duplicate normalized name should warn or block depending rule.
[ ] Staff Leader cannot approve/reject partner outside owner_campus_id.
```

### 8.2. Contact validation

```text
[ ] fullName required.
[ ] email valid if provided.
[ ] phone valid if provided.
[ ] duplicate email in same partner blocked.
[ ] set primary must unset other active primary contacts.
[ ] deactivate primary contact should either block or automatically choose another primary only if business agrees.
```

### 8.3. Visit link validation

```text
[ ] guest_member_id or minute_participant_id must exist.
[ ] partner_id required.
[ ] user must have access to visitInstanceId.
[ ] cannot confirm link to partner outside user's allowed scope unless partner is APPROVED/INTERNAL and visit scope allows it.
[ ] rejected suggestion should not appear as active badge.
```

---

## 9. Frontend structure

Create feature folder:

```text
frontend/pems-react/src/features/partners/
  api/
    partnersApi.ts
    partnerContactsApi.ts
    partnerAliasesApi.ts
    partnerDocumentsApi.ts
    partnerLinksApi.ts
  types/
    partner.types.ts
  components/
    PartnerStatusBadge.tsx
    PartnerVisibilityBadge.tsx
    PartnerFilterBar.tsx
    PartnerTable.tsx
    PartnerForm.tsx
    PartnerApprovalActions.tsx
    PartnerRejectModal.tsx
    PartnerContactTable.tsx
    PartnerContactFormModal.tsx
    PartnerDocumentSection.tsx
    PartnerAliasSection.tsx
    PartnerSelector.tsx
    ParticipantPartnerCell.tsx
```

Pages:

```text
frontend/pems-react/src/pages/dashboard/partners/
  PartnerManagementPage.tsx
  PartnerDetailPage.tsx
  PartnerCreatePage.tsx
  PartnerEditPage.tsx
  PartnerPendingApprovalPage.tsx
```

---

## 10. UI requirements

### 10.1. Partner Management list

Columns:

```text
STT
Mã đối tác
Tên đối tác
Quốc gia
Campus sở hữu
Người tạo
Trạng thái
Hành động
```

Filters:

```text
Search by partner code/name
Country
Campus
Profile status
Visibility
```

Actions:

```text
View detail
Approve/Reject if Staff Leader and pending/same campus
Create partner
```

### 10.2. Partner Detail

Sections/tabs:

```text
Overview
Contacts
Documents
Cooperation history
Aliases
Review/approval panel
```

Overview:

```text
Logo
Cover
Partner code
Name
Short name
Country/city
Website
Address
Partner type
Cooperation status
Profile status
Visibility
Owner campus
Created by/created at
Reviewed by/reviewed at/review note
```

### 10.3. ParticipantPartnerCell in meeting/minutes table

For internal participant:

```text
Badge: Nội bộ
No partner action
```

For guest participant:

```text
APPROVED linked partner:
- Badge xanh: Đối tác
- Action: Xem chi tiết

PENDING_APPROVAL linked partner:
- Badge vàng: Chờ duyệt
- Action: Xem hồ sơ

SUGGESTED:
- Badge xanh dương: Gợi ý
- Actions: Liên kết / Bỏ qua

NONE:
- Badge xám: Chưa có
- Actions: Tạo đối tác / Quét danh thiếp nếu OCR module available
```

---

## 11. Notification and audit

Create notifications for:

```text
Partner created PENDING_APPROVAL → notify Staff Leader of owner campus.
Partner approved/rejected → notify creator.
Partner contact created from OCR → optional notify owner/creator.
Partner linked to visit guest → audit only or notify if needed.
```

Audit event names:

```text
CREATE_PARTNER
UPDATE_PARTNER
APPROVE_PARTNER
REJECT_PARTNER
CREATE_PARTNER_CONTACT
UPDATE_PARTNER_CONTACT
DEACTIVATE_PARTNER_CONTACT
SET_PRIMARY_PARTNER_CONTACT
CREATE_PARTNER_ALIAS
DEACTIVATE_PARTNER_ALIAS
LINK_VISIT_GUEST_PARTNER
REJECT_VISIT_GUEST_PARTNER_SUGGESTION
```

---

## 12. Backend tests

```text
[ ] Create partner success -> PENDING_APPROVAL.
[ ] Create partner ignores frontend owner_campus_id.
[ ] Duplicate partnerCode -> 409.
[ ] Public visibility while pending -> blocked.
[ ] Staff Leader approve same campus -> success.
[ ] Staff Leader approve other campus -> 403.
[ ] Reject without reason -> 422.
[ ] Staff sees partner created by self/campus scope.
[ ] Public partners exclude pending/rejected/private.
[ ] Create contact success.
[ ] Duplicate contact email same partner -> 409.
[ ] Set primary unsets previous primary.
[ ] Deactivate contact works as soft delete.
[ ] Create alias normalized unique.
[ ] Match partner by alias.
[ ] Match partner by email domain.
[ ] Link guest to partner success.
[ ] Reject suggested link removes badge.
```

---

## 13. Frontend tests/manual checks

```text
[ ] Partner list loading/error/empty states.
[ ] Search/filter/pagination call backend.
[ ] Create partner form validates required fields.
[ ] Detail page renders overview/contact/document/alias.
[ ] Approve/reject only visible for Staff Leader pending/same campus.
[ ] Reject modal requires reason.
[ ] Contact CRUD works.
[ ] Alias section works.
[ ] ParticipantPartnerCell shows correct badges.
[ ] Create partner from guest pre-fills organization/name/contact info.
[ ] Public partner page hides pending/rejected/private.
```

---

## 14. Build commands

Run after implementation:

```bash
# Backend
cd backend
dotnet build

# Frontend
cd frontend/pems-react
npm install
npm run build
```

Do not claim done if build fails.

