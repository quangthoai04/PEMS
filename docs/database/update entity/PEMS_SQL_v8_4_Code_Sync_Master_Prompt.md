# PEMS SQL v8.4 Code Sync Master Prompt

> Use this prompt for an AI coding assistant working inside the PEMS repository.  
> Goal: synchronize the existing Backend, Frontend, Entity, DTO, validation, query, and UI logic with the updated SQL schema.

---

## 0. Role and mission

You are working on **PEMS — Partnership Engagement Management System**.

Act as all of the following at the same time:

- Senior .NET Clean Architecture Developer
- Senior React TypeScript Engineer
- Database-first MySQL Engineer
- EF Core mapping reviewer
- RBAC / security reviewer
- Frontend API-contract reviewer
- Production bug fixer

The project already has working features such as login, submit visit request form, view guest delegation list, visit request detail, request processing, and multiple dashboard modules. After the SQL schema update, those existing code paths no longer fully match the database. Your task is to **update the existing code to match the new SQL**, not rewrite the whole project.

---

## 1. Absolute source of truth

### 1.1 Primary SQL source

Use this SQL as the schema source of truth:

```text
pems_full_seed_logic_v8_4_frontend_normalized_full.sql
```

Expected repository location:

```text
database/scripts/pems_full_seed_logic_v8_4_frontend_normalized_full.sql
```

If the SQL is currently stored elsewhere, search the repository for that filename and copy it into `database/scripts/` if needed.

### 1.2 Supporting documents to read before coding

Read these files before modifying code:

```text
docs/architecture/PROJECT_STRUCTURE_FULL.md
PROJECT_STRUCTURE_FULL.md

docs/architecture/CLEAN_ARCHITECTURE.md
CLEAN_ARCHITECTURE.md

docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY.md
PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY.md

docs/PROJECT_OVERVIEW.md
PROJECT_OVERVIEW.md

docs/VISITOR_MANAGEMENT_SYSTEM.md
VISITOR_MANAGEMENT_SYSTEM.md

docs/database/PEMS_v8_4_2_Current_SQL_Change_Report.docx
PEMS_v8_4_2_Current_SQL_Change_Report.docx
```

If any document conflicts with the SQL, **the SQL wins**. Do not invent a column, enum, table, status, route, permission code, or DTO field that is not in SQL or already required by existing implemented UI.

### 1.3 If multiple SQL versions exist

If the repo contains more than one of these files:

```text
pems_full_seed_logic_v8_4_frontend_normalized_full.sql
pems_full_seed_logic_v8_4_1_revised_frontend_normalized.sql
pems_full_seed_logic_v8_4_2_safe_update_fixed.sql
```

then do this:

1. Open the file explicitly named by the user/task.
2. Treat that file as primary.
3. Do not silently switch to another SQL version.
4. If the code comments or docs mention a newer override, report the conflict and ask whether to switch source of truth.

---

## 2. Non-negotiable rules

### 2.1 Database-first rule

PEMS is database-first/manual SQL.

Do not:

```text
- Do not create EF Core migrations.
- Do not run auto migration.
- Do not change schema from C# code.
- Do not add runtime seed in Program.cs.
- Do not create columns/tables in code that are not in the SQL source of truth.
- Do not keep mapping to dropped JSON columns if SQL has normalized them.
```

If the SQL is truly missing a field required by implemented frontend, do not hack the code. Create a small SQL patch proposal and report it clearly.

### 2.2 Clean Architecture rule

Controllers in `PEMS.Api` must only:

```text
- Receive route/query/body.
- Call IMediator.Send().
- Return ApiResponse/ActionResult.
```

Controllers must not:

```text
- Query DbContext directly.
- Contain business if/else chains.
- Map Entity to DTO manually in controller.
- Write try/catch blocks for normal business errors.
```

Business logic belongs in Application handlers/services and Domain methods.

### 2.3 Existing functionality rule

Do not rewrite or delete working features. Update them in place.

Do not remove these flows:

```text
- Dual portal login / SSO login / current auth flow.
- Submit visit request form and OTP verification flow.
- View guest delegation list.
- View guest delegation detail.
- Process visit request.
- Dashboard layout, role guard, permission guard.
```

If code currently uses mock data for a module that now has real SQL/API support, replace it with real API integration gradually and safely.

### 2.4 Frontend rule

Do not change UI layout unnecessarily. Update:

```text
- TypeScript types.
- API payloads.
- Adapters/mappers.
- Hook state.
- Enum/status mapping.
- Fields shown in existing screens.
- Validation schema.
```

Keep existing component structure unless a component cannot compile or has a clear API mismatch.

### 2.5 Security rule

Never return or expose:

```text
password_hash
password_salt
refresh_token
refresh_token_hash
provider_subject
provider_uid
security_stamp
otp_token
reset_token
api_key_encrypted
bearer_token_encrypted
client_secret_encrypted
basic_password_encrypted
any raw secret
```

Files do not carry business visibility by themselves unless SQL explicitly says so. File access should be derived from the parent business entity: partner, campus, gallery, news, document, visit request, minute, logistics, or related module.

---

## 3. First required action: build a schema impact map

Before changing code, generate a schema impact map from the SQL.

### 3.1 Required SQL inspection commands

After importing the SQL into MySQL 8, run:

```sql
SELECT TABLE_NAME
FROM information_schema.tables
WHERE table_schema = DATABASE()
ORDER BY TABLE_NAME;

SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_KEY, EXTRA
FROM information_schema.columns
WHERE table_schema = DATABASE()
ORDER BY TABLE_NAME, ORDINAL_POSITION;

SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND DATA_TYPE = 'enum'
ORDER BY TABLE_NAME, COLUMN_NAME;

SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND DATA_TYPE = 'json'
ORDER BY TABLE_NAME, COLUMN_NAME;
```

Save the result into a temporary working note:

```text
docs/database/SCHEMA_IMPACT_MAP_v8_4.md
```

### 3.2 Required impact map sections

The impact map must list:

```text
1. Tables added by SQL v8.4.
2. Tables removed or not used anymore.
3. Existing tables with new columns.
4. Existing tables with changed enum values.
5. JSON columns removed or replaced by normalized columns/tables.
6. Backend Domain entities affected.
7. EF Core DbContext/Configuration affected.
8. Application commands/queries affected.
9. Frontend types/adapters/pages affected.
10. Build/test risks.
```

Do not start code changes until this impact map is written.

---

## 4. Backend update plan

Update backend from database layer outward.

### 4.1 Domain entities to audit

Audit and update these folders:

```text
backend/PEMS.Domain/Entities/Users
backend/PEMS.Domain/Entities/Delegations
backend/PEMS.Domain/Entities/Emails
backend/PEMS.Domain/Entities/Calendar
backend/PEMS.Domain/Entities/AgendaTemplates
backend/PEMS.Domain/Entities/ApiIntegrations
backend/PEMS.Domain/Entities/Minutes
backend/PEMS.Domain/Entities/Feedbacks
backend/PEMS.Domain/Entities/Galleries
backend/PEMS.Domain/Entities/Partners
backend/PEMS.Domain/Entities/Documents
backend/PEMS.Domain/Entities/Faqs
backend/PEMS.Domain/Entities/News
backend/PEMS.Domain/Entities/Campuses
backend/PEMS.Domain/Entities/Departments
```

For every SQL table, there must be a matching Entity if the table is used by EF Core. For query-only views, use DTO projection instead of creating unnecessary aggregate entities.

### 4.2 New/normalized child entities to add if present in SQL

If these tables exist in the selected SQL file, add Domain entities and DbSet mappings:

```text
minute_participants
feedback_rating_items
sent_email_recipients
calendar_event_attendees
calendar_event_reminders
api_configuration_headers
agenda_template_items
audit_log_changes
```

If these tables do not exist in the selected SQL, do not add them.

If `gallery_locations` exists in the selected SQL, map it correctly. If it does not exist, store gallery area/location fields directly on `galleries` and remove any dependency on a Location Management table.

If `public_content_blocks` exists in the selected SQL, map it only if the system actually uses public content management. If the current agreed requirement says there is no public content block management, do not wire UI/API to it unless explicitly requested.

### 4.3 Enums to audit

Extract every SQL `ENUM(...)` and sync with C# enums in:

```text
backend/PEMS.Domain/Enums
backend/PEMS.Domain/Constants
backend/PEMS.Application/Common/Security
frontend/pems-react/src/shared/constants
frontend/pems-react/src/features/**/types
```

Rules:

```text
- C# enum/string constants must match SQL values exactly.
- Frontend enum values sent to API must match SQL values exactly.
- Vietnamese labels must be display-only.
- Do not store Vietnamese display text as enum value.
- If SQL has role_code = DEPARTMENT, frontend may display DEPT but API value must be DEPARTMENT.
```

Pay special attention to:

```text
roles.role_code
users.status
users.created_via
users.sub_role
user_auth_providers.provider_type
user_sessions.status
security_events.event_type
security_events.result
security_events.failure_reason_code
visit_requests.status
visit_requests.visit_scope
visit_requests.created_source
visit_requests.visit_type
visit_requests.working_language
visit_requests.transportation_type
visit_requests.media_consent_status
visit_requests.cancellation_actor_type
visit_requests.cancellation_source
visit_request_campuses.status
visit_guest_members.member_type
visit_participants.participant_role
visit_participants.invitation_status
visit_logistics_items.status
minutes.status
feedbacks.status
news.status
faqs.status
faqs.faq_type
galleries.status
gallery_images.media_type
email_templates.status
sent_emails.status
sent_email_recipients.recipient_type
sent_email_recipients.delivery_status
calendar_events.status
calendar_event_attendees.response_status
calendar_event_reminders.status
api_configurations.auth_type
api_configurations.status
api_configurations.last_test_status
agenda_templates.status
audit_logs.action_type
```

Only include enum names that exist in the actual SQL.

### 4.4 EF Core mapping

Update:

```text
backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
backend/PEMS.Infrastructure/Persistence/Configurations/*.cs
```

Required:

```text
- DbSet<T> for every EF-backed table.
- Table names match SQL exactly.
- Column names match SQL exactly.
- BIGINT UNSIGNED maps to ulong/long consistently with existing project convention.
- DATETIME maps to DateTime/DateTime?.
- TEXT/LONGTEXT maps to string?.
- DECIMAL maps to decimal.
- BOOLEAN/TINYINT(1) maps to bool.
- ENUM maps to string or configured enum conversion, but must serialize exact SQL value.
- Required/optional nullability matches SQL.
- Indexes/unique constraints configured where needed for EF model consistency.
```

Do not use Data Annotations in Domain entities. Use Fluent API configuration.

### 4.5 Remove old JSON mapping

Search the full backend for:

```text
support_team_json
contact_person_json
participants_json
translations_json
variables_json
recipients_json
metadata_json
attendees_json
reminders_json
credentials_json
headers_json
body_template_json
settings_json
items_json
old_values_json
new_values_json
metadata JSON
```

If the selected SQL removed these columns, remove code that reads/writes them and replace with normalized fields/tables.

Examples:

```text
- visit_requests.contact_person_json -> contact_person_full_name, contact_person_organization, contact_person_phone, contact_person_email.
- visit_requests.support_team_json -> visit_guest_members with member_type = EXTERNAL_SUPPORT if SQL supports it.
- minutes.participants_json -> minute_participants table.
- sent_emails.recipients_json -> sent_email_recipients table.
- calendar_events.attendees_json -> calendar_event_attendees table.
- agenda_templates.items_json -> agenda_template_items table.
- audit_logs.old_values_json/new_values_json -> audit_log_changes table if SQL supports it.
```

If the selected SQL still contains a JSON column, keep compatibility but avoid adding new business logic that depends on JSON unless no normalized structure exists.

---

## 5. Backend module-specific checklist

### 5.1 Authentication and session

Files to inspect:

```text
backend/PEMS.Application/Authentication/**
backend/PEMS.Infrastructure/Identity/**
backend/PEMS.Infrastructure/Logging/SecurityAuditService.cs
backend/PEMS.Domain/Entities/Users/SecurityEvent.cs
backend/PEMS.Domain/Entities/Users/UserSession.cs
backend/PEMS.Domain/Entities/Users/LoginLog.cs
frontend/pems-react/src/features/authentication/**
frontend/pems-react/src/shared/auth/**
```

Required sync:

```text
- Login response fields must match users, roles, campuses, departments, user_sessions.
- Dual portal login must validate login_portal / selected_campus_id according to SQL fields and business rule.
- Visitor auto-provision must match users.created_via and users role/campus nullability.
- Security events must match SQL event_type/result/failure_reason_code/login_portal/selected_campus_id/email_snapshot/provider_type if present.
- Do not add local-password brute-force fields to security_events if SQL does not have them.
```

### 5.2 Visit request submit form

Files to inspect:

```text
backend/PEMS.Application/Delegations/Commands/SubmitVisitRequest
backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest
backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest
backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs
backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs
backend/PEMS.Domain/Entities/Delegations/VisitRequestCampus.cs
backend/PEMS.Domain/Entities/Delegations/VisitGuestMember.cs
frontend/pems-react/src/features/visit-request/**
frontend/pems-react/src/components/modals/VisitingFormPopup.tsx
```

Required sync:

```text
- DTO request must include only fields backed by SQL.
- registrant fields must map to SQL columns.
- contact person fields must map to explicit SQL columns, not JSON.
- selected campuses must create visit_request_campuses rows.
- guest list must create visit_guest_members rows.
- support/external team must use member_type if available.
- working_language must only send enum values that SQL accepts.
- Do not send working_language_other unless the selected SQL has it.
- transportation fields must match selected SQL.
- media_consent_status/media_consent_note must match selected SQL if present.
- OTP flow must not create final visit_requests before verification unless SQL/business flow requires it.
```

### 5.3 View guest delegation list/search/detail

Files to inspect:

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList
backend/PEMS.Application/Delegations/Queries/SearchDelegations
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationDetails
backend/PEMS.Application/Delegations/Mappings/DelegationsMappingProfile.cs
frontend/pems-react/src/features/delegations/**
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitRequestDetail.tsx
frontend/pems-react/src/pages/dashboard/visit/HoVisitProcessDetail.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
```

Required sync:

```text
- List projections must include new SQL columns required by frontend, e.g. delegation name, visit type, visit scope, campuses, host, status, display status, contact person, transportation, media consent, cancellation metadata.
- Strict visibility must remain: HO sees multi-campus scope; Staff Leader sees same-campus scope; Admin does not see business visit/delegation data unless SQL/views explicitly allow it.
- sortBy plannedStartAt/plannedEndAt/requestCode/status/currentHost must use whitelisted SQL columns.
- No direct ID access without scope check.
- Status labels shown in frontend must be derived from SQL enum values, not hard-coded Vietnamese values.
```

### 5.4 Process visit request and cancellation

Files to inspect:

```text
backend/PEMS.Application/Delegations/Commands/ApproveCrossCampusRequest
backend/PEMS.Application/Delegations/Commands/ProcessVisitRequest
backend/PEMS.Application/Delegations/Commands/CancelVisitRequest if present
backend/PEMS.Domain/Entities/Delegations/VisitStatusLog.cs
frontend/pems-react/src/pages/dashboard/visit/**
frontend/pems-react/src/features/delegations/**
```

Required sync:

```text
- visit_requests.status transitions must match SQL enums.
- visit_request_campuses.status transitions must match SQL enums.
- Single-campus approval must assign host according to current SQL/business rule.
- Multi-campus approval by HO must create/release campus instances according to SQL/business rule.
- cancellation_actor_type, cancellation_source, cancellation_reason, cancelled_by, cancelled_at must be written only if these columns exist.
- Do not use external_confirmation_note.
- Do not use CANCELLED as a substitute for REJECTED before approval unless SQL/business rule explicitly says so.
```

### 5.5 Email module

Files to inspect:

```text
backend/PEMS.Domain/Entities/Emails/**
backend/PEMS.Application/Emails/**
backend/PEMS.Infrastructure/Email/**
frontend/pems-react/src/features/emails/**
frontend/pems-react/src/pages/dashboard/emails/**
```

Required sync:

```text
- email_templates fields must match SQL.
- If template content is normalized, use explicit columns or child table from SQL.
- sent_emails must store one send event.
- sent_email_recipients must store per-recipient delivery status if the table exists.
- SendEmailCommand must support TO/CC/BCC only if SQL supports recipient_type.
- ReplytoEmail must not create fake thread records unless SQL has thread/message structure.
- Frontend Send Email tab must submit payload matching SQL-backed DTO.
- Do not keep recipients_json if SQL removed it.
```

### 5.6 Gallery module

Files to inspect:

```text
backend/PEMS.Domain/Entities/Galleries/**
backend/PEMS.Application/Galleries/**
frontend/pems-react/src/features/gallery-management/**
frontend/pems-react/src/pages/dashboard/gallery/GalleryManagement.tsx
frontend/pems-react/src/pages/dashboard/gallery/LocationManagement.tsx
frontend/pems-react/src/pages/VisitFPTUPage.tsx
```

Required sync:

```text
- If gallery_locations exists, wire it only if the current SQL and requirements really use location management.
- If gallery_locations does not exist, remove dependencies on LocationManagement and store area_name, specific_location_name, location_description directly on galleries if those columns exist.
- gallery_images.media_type must support IMAGE/VIDEO only if SQL has it.
- thumbnail_file_id/hero_file_id/virtual_tour_url must match SQL.
- Public gallery / Visit FPTU must only show active/published content according to SQL fields.
```

### 5.7 Files/documents module

Files to inspect:

```text
backend/PEMS.Domain/Entities/Documents/**
backend/PEMS.Infrastructure/FileStorage/**
backend/PEMS.Application/Documents/**
frontend/pems-react/src/features/documents/**
```

Required sync:

```text
- files table stores technical file metadata.
- Do not rely on files.visibility unless it exists in selected SQL.
- File permission must be derived from parent entity/module scope.
- external_file_id/web_view_url/download_url/thumbnail_url/file_purpose must be mapped if present.
- Document list/search must project file metadata safely and not leak storage secrets.
```

### 5.8 Meeting minutes module

Files to inspect:

```text
backend/PEMS.Domain/Entities/Minutes/**
backend/PEMS.Application/Delegations/Commands/CreateMeetingMinutes
backend/PEMS.Application/Delegations/Commands/EditMeetingMinutes
backend/PEMS.Application/MeetingMinutes/**
frontend/pems-react/src/features/meeting-minutes/**
frontend/pems-react/src/pages/dashboard/minutes/MinuteManagement.tsx
```

Required sync:

```text
- minutes table stores main minute record only.
- minute_action_items handles action items.
- minute_participants handles participant snapshots if table exists.
- Do not write participants_json if SQL removed it.
- Detail DTO must return participants/action items in a stable structure.
```

### 5.9 Feedback module

Files to inspect:

```text
backend/PEMS.Domain/Entities/Feedbacks/**
backend/PEMS.Application/Delegations/Commands/SubmitDelegationFeedback
backend/PEMS.Application/Feedbacks/**
frontend/pems-react/src/features/feedbacks/**
frontend/pems-react/src/pages/dashboard/feedback/**
```

Required sync:

```text
- feedbacks.rating remains overall rating if SQL has it.
- feedback_rating_items stores category ratings if table exists.
- Frontend feedback detail/summary must aggregate from SQL-backed data.
- Remove mockData.ts usage from production path when backend is ready.
```

### 5.10 Partner module

Files to inspect:

```text
backend/PEMS.Domain/Entities/Partners/**
backend/PEMS.Application/Partners/**
backend/PEMS.Application/Delegations/Commands/CreatePartnerProfile
frontend/pems-react/src/features/partners/**
frontend/pems-react/src/pages/dashboard/partners/**
frontend/pems-react/src/pages/PartnersPage.tsx
frontend/pems-react/src/pages/PartnerDetailPage.tsx
```

Required sync:

```text
- logo_file_id/cover_file_id/address/public_slug/profile_status/review fields must be mapped if present.
- cooperation_status must not be confused with approval/profile_status.
- partner_contacts remain separate contact-person table.
- Public partner list must filter by SQL visibility/status fields if present.
```

### 5.11 FAQ module

Files to inspect:

```text
backend/PEMS.Domain/Entities/Faqs/Faq.cs
backend/PEMS.Application/Faqs/**
backend/PEMS.Application/PublicContent/Queries/ViewFaq
frontend/pems-react/src/features/faq-management/**
frontend/pems-react/src/pages/dashboard/faq/**
frontend/pems-react/src/pages/FAQPage.tsx
```

Required sync:

```text
- faq_type/category must match selected SQL.
- status must match SQL values. Display labels Visible/Hidden can be frontend labels only.
- language_code must be mapped only if SQL has it.
- Public FAQ must show only visible/published FAQs.
- Management FAQ must show both visible and hidden according to role.
```

### 5.12 Calendar module

Files to inspect:

```text
backend/PEMS.Domain/Entities/Calendar/**
backend/PEMS.Application/Calendars/**
frontend/pems-react/src/features/calendars/**
```

Required sync:

```text
- calendar_events stores main event.
- attendees/reminders must use child tables if present.
- Do not write attendees_json/reminders_json if SQL removed them.
- response_status/reminder status enums must match SQL.
```

### 5.13 API management module

Files to inspect:

```text
backend/PEMS.Domain/Entities/ApiIntegrations/**
backend/PEMS.Application/ApiIntegrations/**
backend/PEMS.Infrastructure/ExternalServices/ApiClient/**
frontend/pems-react/src/features/api-management/**
frontend/pems-react/src/pages/dashboard/apis/ApiManagement.tsx
```

Required sync:

```text
- api_configurations fields must match SQL.
- api_configuration_headers must be used if table exists.
- Credentials must be masked/encrypted and never returned raw to frontend.
- TestAPIConnection must update last_test_status/last_tested_at/last_test_message if those columns exist.
- View API config DTO must mask secrets.
```

### 5.14 Agenda templates module

Files to inspect:

```text
backend/PEMS.Domain/Entities/AgendaTemplates/**
backend/PEMS.Application/AgendaTemplates/**
frontend/pems-react/src/features/agenda-templates/**
frontend/pems-react/src/pages/dashboard/visit/AgendaTemplateManagement.tsx
```

Required sync:

```text
- If agenda_template_items table exists, do not use items_json.
- Create/update template must handle item list transactionally.
- View detail must return ordered items.
```

### 5.15 Account, role, campus, department, profile

Files to inspect:

```text
backend/PEMS.Application/Accounts/**
backend/PEMS.Application/Roles/**
backend/PEMS.Application/Campuses/**
backend/PEMS.Application/Departments/**
backend/PEMS.Application/Profiles/**
frontend/pems-react/src/features/account-management/**
frontend/pems-react/src/features/role-permission-management/**
frontend/pems-react/src/features/campus-management/**
frontend/pems-react/src/features/department-management/**
frontend/pems-react/src/features/profile/**
```

Required sync:

```text
- users table must match SQL exactly. Do not add display_name/preferred_language/bio in code if SQL does not have them.
- campuses table must match SQL exactly. Do not add public hero fields if SQL does not have them.
- role_code/sub_role conventions must match SQL seed.
- Account list/detail must never expose password/provider secrets.
- Department scope rules must remain enforced.
```

---

## 6. Frontend update plan

### 6.1 Frontend files to update first

Start with shared contract files:

```text
frontend/pems-react/src/shared/types/api.types.ts
frontend/pems-react/src/shared/types/auth.types.ts
frontend/pems-react/src/shared/types/common.types.ts
frontend/pems-react/src/shared/constants/roles.ts
frontend/pems-react/src/shared/constants/statusCodes.ts
frontend/pems-react/src/shared/constants/permissions.ts
frontend/pems-react/src/shared/constants/ucCodes.ts
frontend/pems-react/src/shared/api/endpoints.ts
```

Then update feature-specific types:

```text
frontend/pems-react/src/features/visit-request/types/visitRequest.types.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/features/emails/types/emails.types.ts
frontend/pems-react/src/features/gallery-management/types/galleryManagement.types.ts
frontend/pems-react/src/features/partners/types/partners.types.ts
frontend/pems-react/src/features/documents/types/documents.types.ts
frontend/pems-react/src/features/meeting-minutes/types/meetingMinutes.types.ts
frontend/pems-react/src/features/feedbacks/types/feedbacks.types.ts
frontend/pems-react/src/features/faq-management/types/faqManagement.types.ts
frontend/pems-react/src/features/calendars/types/calendars.types.ts
frontend/pems-react/src/features/api-management/types/apiManagement.types.ts
frontend/pems-react/src/features/agenda-templates/types/agendaTemplates.types.ts
frontend/pems-react/src/features/account-management/types/accountManagement.types.ts
```

### 6.2 Adapter rules

Every adapter must convert between API shape and UI shape explicitly.

Do not let UI components depend on raw backend response names if the screen uses different labels.

Rules:

```text
- API enum value remains exact SQL value.
- UI label is mapped separately.
- Date strings are parsed/format only at display boundary.
- Missing optional fields must be handled safely.
- Do not assume arrays are non-null.
- Do not infer fields that backend does not return.
```

### 6.3 Validation schema update

Update frontend validation schemas:

```text
frontend/pems-react/src/features/visit-request/schema/visitRequest.schema.ts
frontend/pems-react/src/features/**/schema/*.ts if present
```

Validation must match SQL and backend validators:

```text
- Required fields match NOT NULL business inputs.
- Enum fields limited to SQL enum values.
- Email/phone length/format valid.
- Date ranges valid.
- Working language values match SQL exactly.
- No working_language_other unless SQL has it.
- Contact person fields map to explicit columns if present.
```

---

## 7. Required search commands before coding

Run these searches in repo root and fix every result that is incompatible with SQL:

```bash
rg "support_team_json|contact_person_json|participants_json|translations_json|variables_json|recipients_json|metadata_json|attendees_json|reminders_json|credentials_json|headers_json|body_template_json|settings_json|items_json|old_values_json|new_values_json"

rg "working_language_other|APPROVED_BUT_NO_HOST|PENDING_EMAIL_VERIFICATION|external_confirmation_note"

rg "files\.visibility|visibility.*files|FileVisibility"

rg "gallery_locations|LocationManagement|gallery_location_id|area_name|specific_location_name|location_description"

rg "public_content_blocks|PublicContentBlock"

rg "DEPT\b|DEPARTMENT\b|STAFF_L|STAFF_LEADER|AUTO_STAFF_LEADER"

rg "metadata" backend/PEMS.Domain/Entities/Users backend/PEMS.Application/Authentication backend/PEMS.Infrastructure/Logging
```

Do not blindly delete search results. For every match, decide:

```text
- Keep because SQL still has it.
- Replace because SQL normalized it.
- Remove because feature no longer exists.
- Report conflict because docs/code and SQL disagree.
```

---

## 8. Execution order

Follow this order strictly:

```text
1. Read SQL and docs.
2. Generate SCHEMA_IMPACT_MAP_v8_4.md.
3. Import SQL locally and verify schema.
4. Update Domain entities and enums.
5. Update EF Core DbContext and configurations.
6. Build backend.
7. Update Application commands/queries/DTOs/validators/handlers.
8. Build backend again.
9. Update Infrastructure repositories/services/file/email/security services.
10. Run backend tests or at least targeted manual API tests.
11. Update frontend shared types/constants/endpoints.
12. Update feature types/adapters/hooks/pages.
13. Run frontend build.
14. Do end-to-end manual checks for login, submit visit form, view list request, detail, process request.
15. Write final changelog/report.
```

Do not update frontend first unless backend contract is already confirmed.

---

## 9. Required backend build and test commands

Run from repo root:

```bash
dotnet restore
dotnet build
```

If tests exist and are runnable:

```bash
dotnet test
```

If build fails, fix compile errors before continuing. Do not report completion with failing backend build.

---

## 10. Required frontend build commands

Run:

```bash
cd frontend/pems-react
npm install
npm run build
```

If available:

```bash
npm run lint
npm run typecheck
```

Do not report completion with failing TypeScript build.

---

## 11. Required manual verification flows

At minimum, verify these flows against the updated SQL-backed backend:

```text
1. Internal SSO login.
2. Visitor portal login or visitor auto-provision if supported.
3. Current user / permissions endpoint.
4. Public submit visit request form opens and validates.
5. OTP initiation / verify / create visit request flow.
6. Visit request creates visit_requests, visit_request_campuses, visit_guest_members correctly.
7. Staff Leader / HO view guest delegation list with correct scope.
8. View guest delegation detail loads without missing fields.
9. Process visit request approve/reject uses SQL statuses.
10. Email template list/detail/send works with current email schema.
11. Gallery management list/create/update works with current gallery schema.
12. Partner list/detail uses file/logo/cover fields safely.
13. FAQ public and management status mapping works.
14. Documents/file URLs resolve correctly without file visibility leakage.
15. Backend and frontend do not reference dropped JSON columns.
```

---

## 12. Expected final deliverables from the coding assistant

When finished, produce a report:

```markdown
# PEMS SQL v8.4 Code Sync Completion Report

## 1. SQL source of truth used
- File name:
- Commit/path:
- Import status:

## 2. Schema impact map summary
- Tables added:
- Tables changed:
- Tables removed/not used:
- JSON columns replaced:
- Enum changes:

## 3. Backend files changed
- Domain:
- Application:
- Infrastructure:
- API:
- Tests:

## 4. Frontend files changed
- Shared types/constants:
- Feature types/adapters/hooks:
- Pages/components:

## 5. API contract changes
- Endpoint:
- Request:
- Response:
- Error codes:

## 6. Validation and business rules updated

## 7. Permission/scope rules verified

## 8. Build/test results
- dotnet restore:
- dotnet build:
- dotnet test:
- npm install:
- npm run build:
- npm run lint/typecheck if available:

## 9. Manual verification results

## 10. Known risks / remaining TODO
```

Completion is not accepted without build results.

---

## 13. Short command prompt to start work

Use this as the actual instruction to the coding agent:

```text
Hãy cập nhật toàn bộ code PEMS để khớp với SQL source of truth: database/scripts/pems_full_seed_logic_v8_4_frontend_normalized_full.sql.

Bối cảnh: Login, submit visit form, view list request, view detail, process request và nhiều module dashboard đã có code, nhưng sau thay đổi SQL thì Domain Entity, EF mapping, DTO, handler, repository, frontend types, adapters, hooks và pages đang chưa khớp schema.

Yêu cầu bắt buộc:
1. Đọc PROJECT_STRUCTURE_FULL.md, CLEAN_ARCHITECTURE.md, PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY.md, PROJECT_OVERVIEW.md, VISITOR_MANAGEMENT_SYSTEM.md và SQL mới trước khi sửa code.
2. SQL là nguồn chuẩn cuối cùng. Không tự bịa table/column/enum/permission/route.
3. Tạo docs/database/SCHEMA_IMPACT_MAP_v8_4.md trước khi code.
4. Update theo thứ tự: Domain + Enums -> EF DbContext/Configurations -> Application DTO/Command/Query/Validator/Handler -> Infrastructure Services/Repositories -> Frontend types/API/adapters/hooks/pages.
5. Không rewrite UI, không phá frontend hiện có, không xóa chức năng đang chạy.
6. Loại bỏ hoặc thay thế các mapping tới JSON columns nếu SQL đã chuẩn hóa thành cột/bảng riêng.
7. Cập nhật các module bị ảnh hưởng: Authentication, Visit Request Submit, Delegations List/Detail/Process, Email, Gallery, Files/Documents, Meeting Minutes, Feedback, Partner, FAQ, Calendar, API Management, Agenda Templates, Account/Role/Campus/Department/Profile.
8. Giữ RBAC/scope backend là lớp bảo vệ cuối cùng.
9. Build backend và frontend trước khi báo hoàn thành.
10. Báo cáo file đã sửa, API contract, validation, enum mapping, build/test result và rủi ro còn lại.
```

