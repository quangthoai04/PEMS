# 00 — PEMS Partner + Business Card OCR Master Plan

> Mục tiêu: triển khai **full module Đối tác** và **quét danh thiếp bằng Cloud API có màn cấu hình API riêng** cho PEMS.  
> Áp dụng cho backend ASP.NET Core .NET 8 Clean Architecture + MediatR + EF Core/Pomelo MySQL, frontend React Vite TypeScript/Tailwind, MySQL database-first.

---

## 1. Phạm vi tổng thể

Module này gồm 2 mảng lớn, cần code theo thứ tự để tránh lẫn nghiệp vụ:

| Mảng | File prompt chi tiết | Mục tiêu |
|---|---|---|
| Partner Management | `01_PEMS_PARTNER_MODULE_FULL_PROMPT.md` | Quản lý đối tác, tạo/sửa/xem chi tiết, duyệt/từ chối, contact, document, alias, link khách/biên bản với partner |
| Business Card OCR + API Configuration | `02_PEMS_BUSINESS_CARD_OCR_API_CONFIG_PROMPT.md` | Admin cấu hình Google Document AI, test connection, quota/log; Staff scan danh thiếp thật bằng cloud OCR, review rồi lưu contact |

Không gộp toàn bộ vào một task code duy nhất. Nên chạy theo phase để mỗi phần build/test được riêng.

---

## 2. Source of truth cần giữ đúng

Khi code, phải giữ các quy tắc sau:

```text
- SQL/schema v10 là nguồn chuẩn cho bảng/cột/enum.
- Database-first: nếu đổi DB thì tạo patch SQL rõ ràng trước.
- Không tự tạo dynamic permissions / role_permissions.
- Authorization dùng fixed policy: role_code, sub_role, primary_campus_id, owner_campus_id, visit scope, status.
- partners.owner_campus_id là campus sở hữu hồ sơ partner.
- Staff tạo partner: backend tự set owner_campus_id = currentUser.primary_campus_id.
- Staff Leader chỉ duyệt partner cùng owner_campus_id và profile_status = PENDING_APPROVAL.
- OCR danh thiếp không được tự commit thẳng vào partner_contacts; phải có bước user review/confirm.
- Frontend không bao giờ gọi Google Document AI trực tiếp; backend gọi cloud API.
- Không log raw service account, token, ảnh base64, raw OCR text chưa mã hóa.
```

---

## 3. Các bảng SQL liên quan

### 3.1. Bảng hiện có cần tận dụng

```text
partners
partner_contacts
documents
files
notifications
api_configurations
api_configuration_headers
api_usage_quotas
api_request_logs
visit_requests
visit_request_campuses
visit_guest_members
minute_participants
users
campuses
```

### 3.2. Bảng mới cần thêm

```text
partner_aliases
visit_guest_partner_links
business_card_ocr_jobs
```

### 3.3. Bảng hiện có cần ALTER

```text
api_configurations
```

Cần thêm các cột phục vụ cấu hình provider OCR cloud:

```text
settings_json
credentials_json_encrypted
secret_ref
data_sensitivity
allows_provider_training
retention_days
```

---

## 4. Thứ tự triển khai khuyến nghị

### Phase 1 — SQL patch nền

```text
1. Tạo patch SQL riêng:
   docs/database/scripts/patch_partner_ocr_api_full_module.sql

2. Patch gồm:
   - CREATE TABLE partner_aliases
   - CREATE TABLE visit_guest_partner_links
   - ALTER TABLE api_configurations
   - CREATE TABLE business_card_ocr_jobs
   - Seed api_configurations cho BUSINESS_CARD_OCR_GOOGLE_DOCUMENT_AI ở trạng thái INACTIVE
   - Seed api_usage_quotas cho config OCR

3. Chạy patch trên DB local.
4. Kiểm tra FK/index/enum.
5. Cập nhật entity/config/DbContext.
```

### Phase 2 — Backend API Configuration

```text
1. Implement Admin API Integration endpoints:
   - List configs
   - Detail config
   - Create/update Google Document AI config
   - Test connection
   - Enable/disable
   - View logs
   - View/update quota

2. Add security:
   - ADMIN-only for edit/test/enable/disable
   - Mask secret in responses
   - Encrypt credentials_json_encrypted if DB storage is used
   - Prefer secret_ref/env variable for service account in real deployment

3. Build backend.
4. Test config validation and authorization.
```

### Phase 3 — Backend Partner core

```text
1. Implement Partner list/detail/create/update.
2. Implement approve/reject.
3. Implement contact CRUD and set primary.
4. Implement document section via documents.owner_type = PARTNER.
5. Implement partner_aliases CRUD.
6. Implement partner match API.
7. Implement visit_guest_partner_links API.
8. Add validation, audit, notification.
9. Build backend.
```

### Phase 4 — Frontend Partner Management

```text
1. Replace mock PartnerManagement data with API.
2. Build PartnerManagementPage.
3. Build PartnerDetailPage.
4. Build PartnerCreate/Edit form.
5. Build pending approval actions.
6. Build contact/document/alias sections.
7. Build ParticipantPartnerCell for meeting/minutes table.
8. Build frontend.
```

### Phase 5 — Backend Business Card OCR

```text
1. Implement GoogleDocumentAiBusinessCardOcrProvider.
2. Implement parser raw OCR text -> business card fields.
3. Implement scan endpoint.
4. Implement confirm-contact endpoint.
5. Implement discard endpoint.
6. Implement quota/rate-limit/idempotency/file-hash/job-lock.
7. Write api_request_logs sanitized.
8. Build backend.
```

### Phase 6 — Frontend Business Card OCR

```text
1. Build BusinessCardScanModal.
2. Build upload/preview state.
3. Build processing state.
4. Build OCR review form.
5. Build PartnerSelector.
6. Build Confirm Contact action.
7. Integrate into PartnerDetail contact section.
8. Integrate into meeting/minutes participant table.
9. Build frontend.
```

### Phase 7 — End-to-end test

```text
1. Test Admin config Google Document AI.
2. Test Staff scan card.
3. Test OCR result review.
4. Test confirm contact creates partner_contacts.
5. Test scan from meeting row creates visit_guest_partner_links.
6. Test partner badge updates after confirm.
7. Test role/scope/access control.
8. Test spam/quota/idempotency.
9. Test backend/frontend build.
```

---

## 5. Definition of Done

Chỉ coi là xong khi đạt đủ checklist:

```text
[ ] SQL patch chạy sạch trên DB local.
[ ] Entity/EF config/DbContext khớp SQL.
[ ] Không còn PartnerManagement mock baseData.
[ ] Partner list/detail/create/update chạy DB thật.
[ ] Staff Leader approve/reject partner đúng campus.
[ ] Partner contact CRUD chạy DB thật.
[ ] Partner document dùng documents.owner_type = PARTNER.
[ ] Partner alias dùng để match tên khác.
[ ] Meeting/minutes table có cột Đối tác.
[ ] Tạo partner từ khách prefill đúng.
[ ] API Configuration có màn Admin cấu hình Google Document AI.
[ ] Test connection Google Document AI hoạt động.
[ ] OCR scan gọi cloud API thật qua backend.
[ ] OCR result lưu business_card_ocr_jobs.
[ ] User confirm mới tạo partner_contacts.
[ ] Confirm từ meeting row tạo visit_guest_partner_links.
[ ] Quota/rate-limit/idempotency hoạt động.
[ ] Logs không lộ secret/raw image/raw OCR text.
[ ] Backend build pass.
[ ] Frontend build pass.
[ ] Test role ADMIN/HO/Staff Leader/Staff/Public.
```

---

## 6. Các lỗi tuyệt đối tránh

```text
- Không để frontend gọi Google Document AI trực tiếp.
- Không hardcode processorId/credential trong React.
- Không commit service account JSON vào git.
- Không trả serviceAccountJson raw qua API response.
- Không log ảnh base64 hoặc raw OCR text ra console.
- Không tự động insert partner_contacts ngay sau OCR.
- Không cho Staff Leader duyệt partner khác owner_campus_id.
- Không để frontend truyền owner_campus_id khi tạo partner.
- Không tạo partner public nếu chưa APPROVED.
- Không tạo contact trùng email trong cùng partner.
- Không confirm OCR job 2 lần.
- Không dùng dynamic permissions table.
```

