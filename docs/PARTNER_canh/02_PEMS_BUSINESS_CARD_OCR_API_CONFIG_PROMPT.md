# 02 — PEMS Business Card OCR + API Configuration Full Implementation Prompt

> Mục tiêu: code **quét danh thiếp thật bằng Cloud API** và **màn cấu hình API OCR** trong PEMS.  
> Provider chính: **Google Cloud Document AI OCR Processor**.  
> Frontend chỉ upload ảnh tới backend; backend lấy config từ `api_configurations`, check quota/rate-limit, gọi Google, lưu OCR job, user review rồi confirm thành `partner_contacts`.

---

## 1. Mục tiêu chức năng

Triển khai 2 phần chạy cùng nhau:

```text
1. API Configuration for Business Card OCR
   - Admin cấu hình Google Document AI.
   - Test connection.
   - Enable/disable.
   - Quota/rate limit.
   - View sanitized logs.

2. Business Card OCR Flow
   - Staff/Staff Leader upload business card.
   - Backend gọi Google Document AI.
   - Parse OCR text thành field.
   - Match partner.
   - User review/sửa/chọn partner.
   - Confirm mới tạo partner_contacts.
   - Nếu scan từ biên bản thì tạo visit_guest_partner_links.
```

---

## 2. Quy tắc bảo mật bắt buộc

```text
- Frontend không gọi Google Document AI trực tiếp.
- Service account JSON không commit git.
- Service account JSON không trả raw qua API response.
- Ưu tiên secret_ref/env/secret manager cho credential.
- Nếu lưu credential trong DB thì phải mã hóa vào credentials_json_encrypted.
- Không log raw image/base64.
- Không log token/credential/header secret.
- Không log raw OCR text nếu chưa mã hóa.
- Raw OCR text phải encrypted hoặc không lưu.
- Có retention_days để purge OCR raw/draft sau thời hạn.
- api_request_logs chỉ lưu sanitized request/response metadata.
```

---

## 3. SQL patch

Tạo patch:

```text
docs/database/scripts/patch_business_card_ocr_api_config.sql
```

### 3.1. ALTER `api_configurations`

> Nếu một số cột đã tồn tại thì bỏ qua hoặc điều chỉnh cho khớp schema thật.

```sql
ALTER TABLE api_configurations
  ADD COLUMN settings_json JSON NULL
    COMMENT 'Non-secret provider settings such as project_id, location, processor_id, endpoint'
    AFTER body_template_text,

  ADD COLUMN credentials_json_encrypted LONGTEXT NULL
    COMMENT 'Encrypted credential payload; never expose raw value'
    AFTER settings_json,

  ADD COLUMN secret_ref VARCHAR(255) NULL
    COMMENT 'Reference to server env/secret manager when credentials are not stored in DB'
    AFTER credentials_json_encrypted,

  ADD COLUMN data_sensitivity ENUM('PUBLIC','INTERNAL','CONFIDENTIAL','RESTRICTED')
    NOT NULL DEFAULT 'CONFIDENTIAL'
    AFTER secret_ref,

  ADD COLUMN allows_provider_training BOOLEAN NOT NULL DEFAULT FALSE
    AFTER data_sensitivity,

  ADD COLUMN retention_days INT UNSIGNED NULL
    COMMENT 'How long raw OCR text/draft should be retained before purge'
    AFTER allows_provider_training;
```

### 3.2. CREATE `business_card_ocr_jobs`

```sql
CREATE TABLE business_card_ocr_jobs (
  ocr_job_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

  scanned_card_file_id BIGINT UNSIGNED NOT NULL,
  api_config_id BIGINT UNSIGNED NOT NULL,

  status ENUM(
    'UPLOADED',
    'PROCESSING',
    'SUCCEEDED',
    'FAILED',
    'CONFIRMED',
    'DISCARDED'
  ) NOT NULL DEFAULT 'UPLOADED',

  provider_name VARCHAR(150) NOT NULL DEFAULT 'GOOGLE_DOCUMENT_AI',
  provider_request_id VARCHAR(255) NULL,
  provider_processor_id VARCHAR(255) NULL,
  provider_location VARCHAR(50) NULL,

  raw_text_encrypted LONGTEXT NULL,
  parsed_json_encrypted LONGTEXT NULL,

  parsed_full_name VARCHAR(150) NULL,
  parsed_email VARCHAR(150) NULL,
  parsed_phone VARCHAR(50) NULL,
  parsed_job_title VARCHAR(150) NULL,
  parsed_department_name VARCHAR(150) NULL,
  parsed_organization VARCHAR(255) NULL,
  parsed_website_url VARCHAR(500) NULL,
  parsed_address VARCHAR(500) NULL,

  confidence_score DECIMAL(5,2) NULL,

  matched_partner_id BIGINT UNSIGNED NULL,
  confirmed_partner_id BIGINT UNSIGNED NULL,
  confirmed_contact_id BIGINT UNSIGNED NULL,

  source_visit_instance_id BIGINT UNSIGNED NULL,
  source_guest_member_id BIGINT UNSIGNED NULL,
  source_minute_participant_id BIGINT UNSIGNED NULL,

  file_sha256 CHAR(64) NULL,

  error_code VARCHAR(100) NULL,
  error_message TEXT NULL,

  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  created_by BIGINT UNSIGNED NULL,
  processed_at DATETIME NULL,
  confirmed_at DATETIME NULL,
  confirmed_by BIGINT UNSIGNED NULL,
  expires_at DATETIME NULL,
  deleted_at DATETIME NULL,

  PRIMARY KEY (ocr_job_id),

  KEY idx_bc_ocr_file (scanned_card_file_id),
  KEY idx_bc_ocr_config_status (api_config_id, status),
  KEY idx_bc_ocr_status_time (status, created_at),
  KEY idx_bc_ocr_file_hash (file_sha256),
  KEY idx_bc_ocr_matched_partner (matched_partner_id),
  KEY idx_bc_ocr_confirmed_partner (confirmed_partner_id),
  KEY idx_bc_ocr_confirmed_contact (confirmed_contact_id),
  KEY idx_bc_ocr_visit_context (source_visit_instance_id, source_guest_member_id, source_minute_participant_id),
  KEY idx_bc_ocr_created_by_time (created_by, created_at),

  CONSTRAINT fk_bc_ocr_file
    FOREIGN KEY (scanned_card_file_id) REFERENCES files(file_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,

  CONSTRAINT fk_bc_ocr_api_config
    FOREIGN KEY (api_config_id) REFERENCES api_configurations(api_config_id)
    ON UPDATE CASCADE ON DELETE RESTRICT,

  CONSTRAINT fk_bc_ocr_matched_partner
    FOREIGN KEY (matched_partner_id) REFERENCES partners(partner_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_bc_ocr_confirmed_partner
    FOREIGN KEY (confirmed_partner_id) REFERENCES partners(partner_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_bc_ocr_confirmed_contact
    FOREIGN KEY (confirmed_contact_id) REFERENCES partner_contacts(contact_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_bc_ocr_visit_instance
    FOREIGN KEY (source_visit_instance_id) REFERENCES visit_request_campuses(visit_instance_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_bc_ocr_guest_member
    FOREIGN KEY (source_guest_member_id) REFERENCES visit_guest_members(guest_member_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_bc_ocr_minute_participant
    FOREIGN KEY (source_minute_participant_id) REFERENCES minute_participants(minute_participant_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_bc_ocr_created_by
    FOREIGN KEY (created_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL,

  CONSTRAINT fk_bc_ocr_confirmed_by
    FOREIGN KEY (confirmed_by) REFERENCES users(user_id)
    ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Cloud OCR job for business cards. OCR draft must be reviewed before creating partner_contacts.';
```

### 3.3. Seed Google Document AI config

```sql
INSERT INTO api_configurations (
  api_code,
  name,
  provider_name,
  purpose,
  base_url,
  default_method,
  auth_type,
  settings_json,
  secret_ref,
  rate_limit_per_minute,
  monthly_quota,
  retry_enabled,
  max_retries,
  timeout_seconds,
  status,
  data_sensitivity,
  allows_provider_training,
  retention_days,
  created_by
)
VALUES (
  'BUSINESS_CARD_OCR_GOOGLE_DOCUMENT_AI',
  'Google Document AI - Business Card OCR',
  'GOOGLE_DOCUMENT_AI',
  'BUSINESS_CARD_OCR',
  'https://us-documentai.googleapis.com',
  'POST',
  'CUSTOM',
  JSON_OBJECT(
    'project_id', '',
    'location', 'us',
    'processor_id', '',
    'endpoint', 'us-documentai.googleapis.com',
    'max_file_size_mb', 10,
    'allowed_mime_types', JSON_ARRAY('image/jpeg', 'image/png', 'image/webp', 'application/pdf')
  ),
  'GOOGLE_DOCUMENT_AI_SERVICE_ACCOUNT',
  20,
  1000,
  TRUE,
  2,
  60,
  'INACTIVE',
  'CONFIDENTIAL',
  FALSE,
  30,
  1
);
```

### 3.4. Seed quota

Global quota:

```sql
INSERT INTO api_usage_quotas (
  api_config_id,
  campus_id,
  campus_scope_key,
  period_yyyymm,
  monthly_limit,
  used_count,
  created_by
)
SELECT
  api_config_id,
  NULL,
  'GLOBAL',
  DATE_FORMAT(CURRENT_DATE(), '%Y%m'),
  1000,
  0,
  1
FROM api_configurations
WHERE api_code = 'BUSINESS_CARD_OCR_GOOGLE_DOCUMENT_AI';
```

---

## 4. Google Cloud setup

Admin/dev cần chuẩn bị:

```text
1. Tạo Google Cloud Project.
2. Enable Document AI API.
3. Tạo Document AI OCR Processor.
4. Ghi lại:
   - project_id
   - location: us/eu
   - processor_id
   - endpoint: us-documentai.googleapis.com hoặc eu-documentai.googleapis.com
5. Tạo Service Account riêng cho PEMS OCR.
6. Cấp quyền tối thiểu: Document AI API User.
7. Tạo key JSON hoặc dùng Secret Manager.
8. Không commit JSON key vào git.
```

Backend appsettings chỉ để non-secret hoặc default:

```json
{
  "BusinessCardOcr": {
    "DefaultApiCode": "BUSINESS_CARD_OCR_GOOGLE_DOCUMENT_AI",
    "StoreRawOcrText": true,
    "RawOcrRetentionDays": 30
  }
}
```

Environment variable nếu dùng local key file:

```text
GOOGLE_APPLICATION_CREDENTIALS=/secure/path/pems-document-ai-sa.json
```

---

## 5. Backend packages

Trong Infrastructure project:

```bash
dotnet add package Google.Cloud.DocumentAI.V1
dotnet add package Google.Apis.Auth
```

---

## 6. Backend API Configuration module

### 6.1. Endpoints

```text
GET    /api/api-integrations
GET    /api/api-integrations/{apiConfigId}
POST   /api/api-integrations/business-card-ocr/google-document-ai
PUT    /api/api-integrations/{apiConfigId}
POST   /api/api-integrations/{apiConfigId}/test
POST   /api/api-integrations/{apiConfigId}/enable
POST   /api/api-integrations/{apiConfigId}/disable
GET    /api/api-integrations/{apiConfigId}/logs
GET    /api/api-integrations/{apiConfigId}/quota
PUT    /api/api-integrations/{apiConfigId}/quota
```

### 6.2. Permission

```text
ADMIN
- Full management: create/update/test/enable/disable/quota/logs.

HO
- Optional read-only monitor logs/status.

Other roles
- No access to API configuration.
```

### 6.3. Create/update config request

```ts
export interface UpsertGoogleDocumentAiOcrConfigRequest {
  name: string;
  projectId: string;
  location: string;
  processorId: string;
  endpoint: string;
  serviceAccountJson?: string | null;
  secretRef?: string | null;
  rateLimitPerMinute: number;
  monthlyQuota: number;
  timeoutSeconds: number;
  retentionDays: number;
}
```

### 6.4. Validation

```text
[ ] api_code unique.
[ ] base_url must be HTTPS.
[ ] purpose must be BUSINESS_CARD_OCR.
[ ] provider_name must be GOOGLE_DOCUMENT_AI.
[ ] projectId required.
[ ] location required.
[ ] processorId required.
[ ] endpoint required.
[ ] serviceAccountJson or secretRef required.
[ ] timeoutSeconds between 5 and 120.
[ ] rateLimitPerMinute > 0.
[ ] monthlyQuota > 0.
[ ] retentionDays between 1 and 365.
[ ] Cannot enable config unless last test success.
[ ] Never return raw serviceAccountJson.
```

### 6.5. Test connection flow

```text
Load api_configurations
→ Validate purpose/provider
→ Load credentials from credentials_json_encrypted or secret_ref/env
→ Build Google Document AI client
→ Test processor access:
   Prefer GetProcessor if SDK supports it; otherwise process a tiny sample image/PDF.
→ Save last_test_status/last_tested_at/last_test_message if these columns exist.
→ Insert api_request_logs sanitized.
→ Return success/failure.
```

Response:

```ts
export interface ApiConnectionTestResultDto {
  success: boolean;
  message: string;
  errorCode?: string | null;
  responseTimeMs: number;
  testedAt: string;
}
```

---

## 7. Backend OCR module

### 7.1. Application folders

```text
backend/PEMS.Application/BusinessCardOcr/
  Commands/
    ScanBusinessCard/
    ConfirmBusinessCardContact/
    DiscardBusinessCardOcrJob/
  Queries/
    GetBusinessCardOcrJob/
  Services/
    IBusinessCardOcrProvider.cs
    IBusinessCardTextParser.cs
    IExternalApiQuotaService.cs
    IExternalApiLogService.cs
```

Infrastructure:

```text
backend/PEMS.Infrastructure/Ocr/
  GoogleDocumentAiBusinessCardOcrProvider.cs
  BusinessCardTextParser.cs
```

### 7.2. Provider interface

```csharp
public interface IBusinessCardOcrProvider
{
    Task<BusinessCardOcrProviderResult> ExtractAsync(
        BusinessCardOcrProviderInput input,
        OcrApiConfiguration config,
        CancellationToken cancellationToken);
}

public sealed class BusinessCardOcrProviderInput
{
    public byte[] FileBytes { get; init; } = Array.Empty<byte>();
    public string MimeType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
}

public sealed class BusinessCardOcrProviderResult
{
    public string ProviderName { get; init; } = "GOOGLE_DOCUMENT_AI";
    public string RawText { get; init; } = string.Empty;
    public decimal ConfidenceScore { get; init; }
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? JobTitle { get; init; }
    public string? DepartmentName { get; init; }
    public string? Organization { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? Address { get; init; }
}
```

### 7.3. Google provider skeleton

```csharp
public sealed class GoogleDocumentAiBusinessCardOcrProvider : IBusinessCardOcrProvider
{
    private readonly IBusinessCardTextParser _parser;
    private readonly ICredentialResolver _credentialResolver;

    public GoogleDocumentAiBusinessCardOcrProvider(
        IBusinessCardTextParser parser,
        ICredentialResolver credentialResolver)
    {
        _parser = parser;
        _credentialResolver = credentialResolver;
    }

    public async Task<BusinessCardOcrProviderResult> ExtractAsync(
        BusinessCardOcrProviderInput input,
        OcrApiConfiguration config,
        CancellationToken cancellationToken)
    {
        // 1. Resolve endpoint/project/location/processor from config.settings_json.
        // 2. Resolve credentials from encrypted DB field or secret_ref/env.
        // 3. Build DocumentProcessorServiceClient.
        // 4. Call ProcessDocumentAsync.
        // 5. Extract document.Text and average token confidence.
        // 6. Parse rawText into business card fields.
        // 7. Return provider result.
        throw new NotImplementedException();
    }
}
```

### 7.4. Parser rules

Raw OCR text parser should extract:

```text
Email       → regex email
Phone       → regex international/VN phone
Website     → URL/domain regex
Organization→ line containing University, College, Institute, Company, Corporation, Co., Ltd., JSC, FPT, etc.
Job title   → line containing Director, Manager, Professor, Coordinator, Head, Officer, Specialist, Analyst, etc.
Full name   → line near email/title, not organization/domain/phone.
Address     → line with city/country/street keywords.
```

Do not use generative AI in phase 1 unless explicitly approved.

---

## 8. OCR endpoints

### 8.1. Scan

```text
POST /api/business-card-ocr/scan
Content-Type: multipart/form-data
```

Fields:

```text
file
visitInstanceId optional
guestMemberId optional
minuteParticipantId optional
partnerId optional
```

Processing:

```text
1. Authenticate user.
2. Role check: STAFF + LEADER or STAFF + STAFF.
3. If visitInstanceId provided, verify user has access to that visit instance.
4. Load ACTIVE api_configurations by api_code BUSINESS_CARD_OCR_GOOGLE_DOCUMENT_AI.
5. Check rate_limit_per_minute.
6. Check api_usage_quotas.
7. Validate file type/size from settings_json.
8. Compute SHA-256 file hash.
9. If same user uploaded same hash recently and job SUCCEEDED, return existing job.
10. Save file metadata to files.
11. Insert business_card_ocr_jobs status PROCESSING.
12. Call Google provider.
13. Match partner by organization/email domain/alias.
14. Update job SUCCEEDED or FAILED.
15. Insert api_request_logs sanitized.
16. Increment api_usage_quotas.used_count.
17. Return OCR draft.
```

Response:

```ts
export interface BusinessCardOcrScanResponse {
  ocrJobId: number;
  status: 'SUCCEEDED' | 'FAILED';
  providerName: string;
  confidenceScore?: number | null;
  parsed?: {
    fullName?: string | null;
    email?: string | null;
    phone?: string | null;
    jobTitle?: string | null;
    departmentName?: string | null;
    organization?: string | null;
    websiteUrl?: string | null;
    address?: string | null;
  };
  matchedPartner?: {
    partnerId: number;
    partnerName: string;
    profileStatus: 'DRAFT' | 'PENDING_APPROVAL' | 'APPROVED' | 'REJECTED';
    confidence: number;
    reason: string;
  } | null;
  errorMessage?: string | null;
}
```

### 8.2. Get job

```text
GET /api/business-card-ocr/jobs/{ocrJobId}
```

Return job detail, parsed fields, status, matched partner, but do not return raw OCR text except admin/debug mode.

### 8.3. Confirm contact

```text
POST /api/business-card-ocr/jobs/{ocrJobId}/confirm-contact
```

Request:

```ts
export interface ConfirmBusinessCardContactRequest {
  partnerId: number;
  fullName: string;
  email?: string | null;
  phone?: string | null;
  jobTitle?: string | null;
  departmentName?: string | null;
  note?: string | null;
  isPrimary?: boolean;
  visitInstanceId?: number | null;
  guestMemberId?: number | null;
  minuteParticipantId?: number | null;
}
```

Processing:

```text
1. Check job exists and status = SUCCEEDED.
2. Block if CONFIRMED/DISCARDED/FAILED.
3. Check partner exists and user can manipulate partner/contact.
4. Validate fullName required.
5. Validate email/phone format.
6. Check duplicate email in same partner.
7. Insert partner_contacts:
   - source_type = BUSINESS_CARD_OCR
   - scanned_card_file_id = job.scanned_card_file_id
   - ocr_confidence = job.confidence_score
8. If isPrimary true, unset previous primary active contact.
9. Update business_card_ocr_jobs:
   - status = CONFIRMED
   - confirmed_partner_id
   - confirmed_contact_id
   - confirmed_by
   - confirmed_at
10. If visit context provided, insert/update visit_guest_partner_links.
11. Audit log.
```

### 8.4. Discard

```text
POST /api/business-card-ocr/jobs/{ocrJobId}/discard
```

Processing:

```text
- Only allowed for job owner or role with scope.
- If job already CONFIRMED, block.
- Set status = DISCARDED.
- Do not create partner_contacts.
```

---

## 9. Anti-spam / cost control

Implement all:

```text
Rate limit by user:
- Example: 10 scans / 10 minutes.

Rate limit by IP:
- Example: 30 scans / 10 minutes.

Quota:
- api_usage_quotas monthly_limit by GLOBAL or campus.
- If exceeded: return 429.

Idempotency-Key:
- Frontend sends Idempotency-Key header.
- Backend should not create duplicate job for same key.

File hash:
- Same user + same file_sha256 + recent SUCCEEDED job -> return old job.

Job lock:
- PROCESSING job cannot be confirmed.
- CONFIRMED job cannot be confirmed again.

Request logs:
- Insert api_request_logs for every cloud call, success/failure/timeout.
- Sanitize request/response.
```

---

## 10. Frontend Admin API Configuration UI

Create/extend:

```text
frontend/pems-react/src/pages/dashboard/api-integrations/ApiIntegrationManagementPage.tsx
frontend/pems-react/src/features/api-integrations/
  api/apiIntegrationsApi.ts
  types/apiIntegration.types.ts
  components/ApiConfigCard.tsx
  components/GoogleDocumentAiConfigForm.tsx
  components/ApiConnectionTestModal.tsx
  components/ApiQuotaPanel.tsx
  components/ApiRequestLogsTable.tsx
```

### 10.1. API config card

Show:

```text
Name
Provider: GOOGLE_DOCUMENT_AI
Purpose: BUSINESS_CARD_OCR
Status: ACTIVE/INACTIVE/DISABLED
Base URL
Project ID
Location
Processor ID masked/visible
Secret status: Configured / Missing
Rate limit per minute
Monthly quota
Last test status
Last tested at
```

Actions:

```text
Edit
Test connection
Enable/Disable
View logs
Edit quota
```

### 10.2. Config form fields

```text
Name
Project ID
Location
Processor ID
Endpoint
Service Account JSON upload/paste OR Secret Ref
Rate limit per minute
Monthly quota
Timeout seconds
Retention days
```

Secret handling UI:

```text
- If credential exists: show ******** and “Replace credential”.
- Never display raw JSON after save.
```

---

## 11. Frontend Business Card OCR UI

Create:

```text
frontend/pems-react/src/features/business-card-ocr/
  api/businessCardOcrApi.ts
  types/businessCardOcr.types.ts
  components/BusinessCardScanModal.tsx
  components/BusinessCardUploadStep.tsx
  components/BusinessCardProcessingStep.tsx
  components/BusinessCardReviewForm.tsx
  components/BusinessCardResultStep.tsx
```

### 11.1. Modal steps

```text
Step 1 — Upload
- Drag/drop or choose file.
- Preview image/PDF name.
- Show active provider: Google Document AI.
- Button: Quét danh thiếp.

Step 2 — Processing
- Loading spinner.
- Disable close or warn before close.
- Text: Đang xử lý bằng Google Document AI...

Step 3 — Review
- Left: card preview.
- Right: editable fields:
  Họ tên
  Email
  Số điện thoại
  Chức danh
  Phòng ban
  Tổ chức
  Website
  Địa chỉ
- Confidence badge.
- Matched partner suggestion.
- Partner selector.
- Button: Lưu người liên hệ.

Step 4 — Result
- Success message.
- Link to partner detail.
- Link/contact name.
```

### 11.2. Integration points

```text
PartnerDetailPage:
- Contacts section has button “Quét danh thiếp”.
- If opened from partner detail, partnerId preselected.

Meeting/minutes participant table:
- Guest row action “Quét danh thiếp”.
- Pass visitInstanceId + guestMemberId/minuteParticipantId.
- After confirm, refresh partner badge cell.
```

---

## 12. Permission rules

```text
ADMIN
- Manage API config.
- View logs/quota.

HO
- Optional read-only monitoring of API config/logs.
- No credential edit unless explicitly allowed.

STAFF + LEADER
- Scan business card.
- Confirm contact.
- Create partner from OCR.
- Approve pending partner same campus.

STAFF + STAFF
- Scan business card.
- Confirm contact in partner/visit scope.
- Create partner PENDING_APPROVAL from OCR.

DEPARTMENT/STUDENT/VISITOR
- Cannot manage API config.
- Cannot scan business card in Partner Management.
```

If scan has visitInstanceId, also check visit process permission/scope.

---

## 13. Tests

### 13.1. API Configuration tests

```text
[ ] Admin creates Google Document AI config.
[ ] Non-admin create/update/test config -> 403.
[ ] Missing projectId/location/processorId -> 422.
[ ] Non-HTTPS base_url -> 422.
[ ] Missing serviceAccountJson and secretRef -> 422.
[ ] Secret raw not returned in GET detail.
[ ] Test connection success saves success/log.
[ ] Test connection fail saves failure/log.
[ ] Enable without successful test -> blocked.
[ ] Disable active config -> OCR scan blocked.
[ ] Update quota success.
[ ] Logs table excludes secret/raw request.
```

### 13.2. OCR scan tests

```text
[ ] Scan valid jpg -> file + job + Google call + SUCCEEDED.
[ ] Scan valid png/webp/pdf.
[ ] File too large -> 400/422.
[ ] Wrong MIME type -> 400/422.
[ ] Config inactive -> blocked, no Google call.
[ ] Quota exceeded -> 429, no Google call.
[ ] Rate limit exceeded -> 429.
[ ] Google timeout -> job FAILED + api_request_logs.
[ ] OCR low confidence -> still reviewable with warning.
[ ] Confirm SUCCEEDED job -> creates partner_contacts.
[ ] Confirm FAILED job -> blocked.
[ ] Confirm DISCARDED job -> blocked.
[ ] Confirm same job twice -> 409.
[ ] Duplicate email in partner -> 409.
[ ] Discard job -> no contact created.
[ ] Scan from meeting row -> confirm creates visit_guest_partner_links.
```

### 13.3. Frontend manual tests

```text
[ ] Admin config screen shows Google Document AI card.
[ ] Test connection button shows loading/success/failure.
[ ] Enable/disable updates status.
[ ] Logs tab loads sanitized logs.
[ ] Partner detail scan modal works.
[ ] Meeting row scan modal preselects context.
[ ] Review form allows editing OCR fields.
[ ] Partner selector can choose APPROVED or PENDING_APPROVAL partner.
[ ] Confirm creates contact and closes modal.
[ ] Badge in participant table updates.
```

---

## 14. Build commands

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

---

## 15. Final checklist

```text
[ ] api_configurations supports Google Document AI settings.
[ ] Admin can configure/test/enable/disable OCR provider.
[ ] Quota and rate limit prevent cost spam.
[ ] api_request_logs records sanitized cloud calls.
[ ] business_card_ocr_jobs stores OCR draft and status.
[ ] Google Document AI provider is called from backend only.
[ ] Parser extracts name/email/phone/title/org/website/address.
[ ] Partner matching returns suggestion.
[ ] Review form appears before saving.
[ ] Confirm creates partner_contacts only once.
[ ] Scan from meeting links guest to partner/contact.
[ ] No secret/raw image/raw OCR leaks in logs/responses.
[ ] Backend build pass.
[ ] Frontend build pass.
```

