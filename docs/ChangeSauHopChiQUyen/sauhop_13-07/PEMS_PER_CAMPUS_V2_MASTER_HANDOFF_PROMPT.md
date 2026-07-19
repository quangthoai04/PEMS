# PEMS Per-Campus Form v2 — Master Handoff & Continuation Prompt

> Tài liệu này là prompt bàn giao tự chứa. Một AI/developer mới phải có thể đọc riêng tài liệu này, kiểm tra repository thật và tiếp tục triển khai mà không cần đọc lại toàn bộ hội thoại trước.
>
> Trạng thái chương trình: **IN PROGRESS**. Không được tuyên bố hoàn thành cho tới khi đạt toàn bộ Definition of Done ở cuối tài liệu.

---

# PHẦN A — PROMPT ĐIỀU HÀNH DÀNH CHO AI TIẾP NHẬN

Bạn là Senior Software Architect kiêm Senior Full-stack Engineer chịu trách nhiệm tiếp tục chương trình **PEMS Per-Campus Form v2** trên repository PEMS.

Mục tiêu cuối cùng là chuyển form đăng ký chuyến thăm từ mô hình “một nội dung dùng chung cho mọi campus” sang mô hình:

- một request cha;
- nhiều campus instance độc lập;
- mỗi campus có lịch, nội dung, thành viên, đầu mối vận hành và trạng thái duyệt riêng;
- người dùng có thể nhập các campus giống nhau bằng thao tác sao chép, nhưng dữ liệu được lưu thành snapshot độc lập;
- registrant và primary contact có cơ chế quyền rõ ràng;
- thay primary contact phải qua xác nhận;
- sửa sau duyệt phải dùng safe edit hoặc amendment;
- mọi mutation quan trọng có revision, audit, notification và test;
- search/list/report/export/email không được lấy campus đầu tiên làm đại diện cho request mixed;
- thời lượng chuyến thăm tối thiểu là 30 phút ở frontend, backend và database.

Đây là terminal program task:

1. Không dừng sau mỗi file, handler hoặc commit để hỏi “có tiếp tục không”.
2. Tự chuyển sang phần kế tiếp khi functional slice hiện tại đã xanh.
3. Chỉ dừng khi:
   - thiếu business decision thật sự chưa được tài liệu này khóa;
   - cần mutation/destructive action trên database thật;
   - gặp conflict không thể giải quyết an toàn;
   - platform hard-limit buộc kết thúc.
4. Lỗi build/test thông thường không phải blocker: tự điều tra, sửa và chạy lại.
5. Nếu platform hard-limit, phải tạo checkpoint sạch, cập nhật report/progress và nêu chính xác điểm resume.
6. Không được giảm chất lượng bằng cách viết hàng loạt production code chưa test.

---

# PHẦN B — NGUỒN SỰ THẬT VÀ TRẠNG THÁI REPOSITORY

## B1. Repository và branch

- Repository gốc: quangthoai04/PEMS.
- Branch triển khai hiện tại: Canh-Iter1.
- HEAD mới nhất được báo cáo sau create-v2: 3d6c0ce8.
- Có một Dev merge giữa quá trình: ae060dcf.
- Một số commit Phase A cũ đã được Dev merge gom thành 770caa33 và fb9a11c6.
- Trước khi code phải chạy git status, git log và kiểm tra code thật; không tin tuyệt đối hash/báo cáo cũ.

## B2. Tài liệu phải đọc trong repository

Tìm bằng rg --files nếu đường dẫn thay đổi:

- PEMS_MULTI_CAMPUS_PER_CAMPUS_FORM_AND_IDENTITY_EDIT_PLAN.md
- FINAL_IMPLEMENTATION_REPORT.md
- IMPLEMENTATION_PROGRESS.md
- PR3_PRE_PR4_AUDIT_MAP.md
- PR3_TEST_REPORT.md
- thư mục docs/database/scripts/percampus_v2_migration/
- canonical business rules, permission rules/matrix và use-case docs hiện hành.

Plan PEMS_MULTI_CAMPUS_PER_CAMPUS_FORM_AND_IDENTITY_EDIT_PLAN.md đang được chủ dự án cố ý để untracked. Được phép đọc nhưng **không git add/commit** nếu chưa có yêu cầu mới.

## B3. Thứ tự ưu tiên nguồn sự thật

Khi tài liệu mâu thuẫn:

1. Code/schema/test thực tế tại HEAD hiện tại.
2. Các quyết định nghiệp vụ đã khóa trong prompt này.
3. Báo cáo tiến độ mới nhất.
4. Master plan.
5. Tài liệu legacy.

Không triển khai lại rule legacy nói HO duyệt request liên cơ sở. Rule hiện tại:

- Staff Leader xử lý từng campus độc lập.
- HO monitor/read-only toàn bộ.
- Admin không có business action cho visit.
- Project dùng fixed policy theo role/sub-role/relation/campus/status; không tự tạo hệ permission động mới.

## B4. Trạng thái runtime an toàn được báo cáo gần nhất

- Read flag PerCampusFormV2 tồn tại và mặc định OFF.
- Write flag PerCampusFormV2Write đã được tạo trong Phase B-2 và mặc định OFF.
- Write ON nhưng read OFF phải bị reject bằng PER_CAMPUS_V2_READ_REQUIRED.
- Chỉ khi cả read và write ON mới chạy create-v2.
- Không tự bật flag mặc định hoặc production.
- v2_requests = 0 trong các persistent DB được kiểm tra: pems_db và pems_pr3_test.
- pems_db và pems_test không được mutation bởi chương trình test/triển khai này.
- Integration test chỉ chạy trên disposable DB được tạo mới từ PR-2 master.
- appsettings.Testing.json đã được restore về database pems_test sau test.
- Working tree gần nhất được báo cáo sạch ngoài plan document untracked.

## B5. Test baseline mới nhất

Baseline mới nhất sau create-v2:

- UnitTests: 474/474.
- ArchitectureTests: 14/14.
- IntegrationTests: 320/320 trên fresh disposable MySQL.
- MySQL đã dùng để verify SQL/migration: 8.0.46.

Con số Unit 435 trước đó là stale incremental build. Baseline hiện hành là 474, trừ khi clean discovery tại HEAD chứng minh số khác.

---

# PHẦN C — BÀI TOÁN GỐC VÀ NĂM QUYẾT ĐỊNH ĐÃ KHÓA

## C1. Bài toán gốc

Form cũ lưu gần như toàn bộ nội dung chuyến thăm trong visit_requests nên một request liên cơ sở buộc mọi campus dùng chung:

- tên đoàn;
- loại hình;
- mục tiêu;
- nội dung làm việc;
- danh sách khách;
- đội hỗ trợ;
- đầu mối;
- ngôn ngữ;
- phương tiện;
- truyền thông;
- ghi chú.

visit_request_campuses cũ chủ yếu chỉ có campus, lịch, lifecycle, quyết định và host. Vì vậy frontend không thể giải quyết yêu cầu mới nếu database/backend vẫn dùng mô hình cũ.

## C2. Quyết định 1 — request tạo sau OTP A, B claim sau

Người đăng ký A submit form và xác minh OTP. Ngay khi OTP A hợp lệ:

- request được tạo;
- các campus được route cho Staff Leader;
- approval không chờ B.

Nếu primary-contact email B sau normalize bằng registrant email A:

- dùng cùng account;
- registrant_user_id và visitor_user_id cùng trỏ account đó;
- primary_contact_access_status = ACTIVE;
- primary_contact_verified_at được set phù hợp.

Nếu B khác A:

- vẫn tạo request ngay;
- lưu snapshot primary contact;
- visitor_user_id = NULL;
- primary_contact_access_status = PENDING_CONFIRMATION;
- tạo identity change INITIAL_CLAIM PENDING, expires 72 giờ;
- gửi lời mời cho B;
- B đăng nhập Google bằng đúng normalized email và bấm accept;
- chỉ transaction accept mới link visitor_user_id và chuyển ACTIVE.

Account B có sẵn vẫn phải explicit accept. Google SSO có thể provision identity nhưng không tự cấp request relation. Không dùng trạng thái user INACTIVE/LOCKED để biểu diễn invitation pending.

## C3. Quyết định 2 — sửa sau duyệt bằng safe edit/amendment

Không cho người dùng âm thầm thay nội dung mà Staff Leader đã duyệt.

Phân loại do backend quyết định:

### Safe/correction

- registrant full name, organization, job title, phone;
- primary-contact name, organization, phone;
- transportation note;
- note to FPTU;
- media note/consent.

Xử lý:

- apply ngay;
- tăng request/form revision phù hợp;
- field-level audit;
- notify Staff Leader/Host bị ảnh hưởng.

### Privacy urgent

media_consent_status chuyển sang DECLINED:

- apply ngay kể cả dưới 24 giờ;
- HIGH/URGENT notification;
- không chờ amendment approval.

### Approval-sensitive

- delegation name;
- visit type/other;
- purpose;
- working content;
- guest/support list;
- working language;
- operational contact khi ảnh hưởng điều phối;
- logistics-impacting requirement.

Xử lý:

- tạo amendment theo visit_instance_id;
- approved snapshot cũ vẫn active;
- current Staff Leader đúng campus approve/reject;
- campus khác không bị reset.

### Structural

- thêm/bỏ/đổi campus;
- thay lịch.

Pending request có thể edit theo lifecycle. Sau duyệt:

- thêm campus tạo instance mới chờ duyệt;
- bỏ campus đã duyệt dùng cancel flow;
- đổi campus = cancel cũ + add mới;
- đổi lịch campus đã duyệt = amendment.

## C4. Quyết định 3 — registrant cancel khi initial contact pending

Registrant được cancel request nếu:

- primary_contact_access_status = PENDING_CONFIRMATION của INITIAL_CLAIM;
- trạng thái request hiện tại vốn cho phép cancel;
- còn qua guard 24 giờ;
- chưa có campus started;
- có cancellation reason.

Khi contact ACTIVE:

- registrant không còn ngoại lệ cancel chỉ vì là co-editor;
- owner rule trở về primary contact, trừ khi cùng user id.

Transfer pending không được coi là initial pending; owner cũ vẫn ACTIVE.

Handler, allowedActions, permission docs và SQL trigger phải đồng nhất.

## C5. Quyết định 4 — expiry và retention

- OTP fallback: theo cấu hình OTP hiện tại; resend làm OTP cũ invalid ngay.
- Initial invitation: 72 giờ.
- Transfer invitation: 24 giờ.
- Initial invitation expired không hủy request.
- Transfer expired không đổi owner cũ.
- EXPIRED, DECLINED, CANCELLED, SUPERSEDED giữ dữ liệu đầy đủ 90 ngày.
- Sau 90 ngày redact token refs, pending snapshot, full email; giữ masked email, request, actor, kind, status, timestamps.
- APPLIED giữ theo audit policy chung.
- Expiry/redaction jobs phải idempotent, batchable, có metric và IDENTITY_CHANGE_REDACTED event.

## C6. Quyết định 5 — search theo scope

- Registrant/active primary contact: parent + tất cả campus của own request.
- HO: tất cả request/campus nhưng read-only.
- Staff Leader: chỉ detail primary campus.
- Host/participant/department/student: chỉ instance có relation.
- Scope phải được xác định trước keyword search.
- Hidden campus không được ảnh hưởng hit/count/order/score/badge.
- Request trả một lần với matchedContexts đã lọc quyền.
- Không bật search guest/support names mặc định vì PII/chi phí.

---

# PHẦN D — KIẾN TRÚC VÀ CÁCH LƯU DỮ LIỆU V2

## D1. Aggregate đích

Một lần submit multi-campus tạo:

- 1 visit_requests;
- N visit_request_campuses;
- N visit_instance_form_details;
- guest/support rows độc lập cho từng campus;
- N nhóm visit_instance_guest_members links;
- baseline revision/history;
- audit;
- identity INITIAL_CLAIM nếu A khác B.

“Một dòng visit_requests” nghĩa là một record cha, không có nghĩa bảng chỉ có ít cột.

## D2. Dữ liệu request-level

visit_requests là request cha và giữ:

- request_code;
- submission_id;
- fingerprint;
- registrant_user_id và registrant snapshot;
- visitor_user_id và primary-contact snapshot;
- partner;
- created source;
- visit_scope;
- form_schema_version;
- has_mixed_campus_details;
- primary-contact access state;
- aggregate status;
- submit/resubmit/cancel metadata;
- row version;
- created/updated audit metadata.

Primary contact là owner/co-editor cấp request.

## D3. Dữ liệu per-campus

visit_request_campuses giữ:

- visit_instance_id;
- visit_request_id;
- campus_id;
- planned_start_at/planned_end_at;
- campus lifecycle;
- coordinator;
- decision;
- host;
- cancel/close/preparation metadata;
- row_version.

visit_instance_form_details giữ snapshot đầy đủ của campus:

- delegation_name;
- visit_type/visit_type_other;
- purpose;
- working_content;
- operational_contact_full_name/organization/phone/email;
- working_language;
- transportation_note;
- media_consent_status/note;
- note_to_fptu;
- form_revision;
- approval_revision;
- row_version.

## D4. Hai loại contact tuyệt đối không được trộn

### Primary contact

- request-level;
- liên quan account VISITOR;
- quản lý/theo dõi/chỉnh sửa request theo lifecycle;
- email đổi qua identity workflow.

### Operational contact

- per-campus snapshot;
- phục vụ vận hành tại cơ sở;
- có thể khác giữa campus;
- không tự tạo account hoặc cấp quyền.

Không tạo owner account riêng cho mỗi operational contact trong scope hiện tại. Nếu sau này cần, đó là feature visit_request_collaborators riêng.

## D5. Thành viên

visit_guest_members giữ dữ liệu người với member_type:

- GUEST;
- EXTERNAL_SUPPORT.

visit_instance_guest_members xác định người thuộc instance nào và mang cả visit_request_id để composite FK chống cross-request link.

Đối với request v2 mới:

- mỗi campus tạo member rows độc lập;
- bấm copy từ campus khác vẫn tạo guest_member_id khác;
- sửa campus A không đổi campus B.

Đối với dữ liệu legacy đã backfill:

- một member có thể đang link nhiều instance;
- lần sửa đầu phải copy-on-write;
- không sửa row shared;
- không cascade mù vào minutes, feedback, OCR, face/gallery, partner links hoặc history.

## D6. Compatibility projection

Các cột global legacy vẫn tồn tại tạm trong visit_requests:

- delegation_name;
- visit_type;
- visit_type_other;
- purpose;
- working_content;
- working_language;
- transportation_note;
- media_consent_status;
- media_consent_note;
- note_to_fptu.

Với form_schema_version = 2:

- source of truth là visit_instance_form_details;
- global columns chỉ là compatibility projection;
- nếu campus giống nhau, projection dùng common snapshot;
- nếu mixed, projection dùng campus có campus_id nhỏ nhất chỉ để tương thích/NOT NULL;
- has_mixed_campus_details = 1;
- read/business/search/report v2 không được dùng projection làm source.

contact_person_* là authoritative request-level primary-contact snapshot, không phải legacy projection. Nếu SQL comment cuối bảng nói contact_person_* là projection thì comment đó cần sửa.

Không drop global columns trong rollout đầu. Contract cleanup chỉ làm sau zero legacy runtime references và backfill/cutover được chứng minh.

## D7. has_mixed_campus_details

Backend tính, client không được gửi.

So sánh normalized copyable content:

- form detail;
- operational contact;
- guest/support member sets;
- additional requirements.

Không tính campus_id hoặc schedule. Nếu chỉ campus/time khác nhưng form/member giống thì mixed = 0.

## D8. Duration và timezone

- DB check: end > start.
- DB named check: TIMESTAMPDIFF(MINUTE, start, end) >= 30.
- 29m59s fail; 30m00s pass.
- FE và BE cũng enforce.
- Giữ convention hiện tại: DATETIME local wall-clock Asia/Ho_Chi_Minh.
- API truyền timezone rõ và normalize trước khi duration.
- Không tự cộng cứng 7 giờ.

## D9. SQL PR-2 đã hoàn thành

Migration package:

- 00_README_IMPORT_ORDER.md;
- 01_preflight_readiness.sql;
- 02_up_additive.sql;
- 03_backfill.sql;
- 04_verify.sql;
- 05_rollback_down.sql;
- PR2_TEST_REPORT.md.

Các bảng mới:

1. visit_instance_form_details
2. visit_instance_guest_members
3. visit_request_identity_changes
4. visit_request_identity_change_events
5. visit_instance_amendments
6. visit_instance_amendment_changes
7. visit_instance_form_revision_history
8. visit_request_revision_history

Altered:

- visit_requests thêm schema/mixed/contact access fields;
- visit_request_campuses thêm 30-minute check và composite unique;
- visit_guest_members thêm composite unique;
- audit tables thêm request/instance/source/correlation/masking context;
- cancel trigger có ngoại lệ 3A.

Kết quả MySQL 8.0.46 được báo cáo:

- fresh import pass, V01–V15 = 0;
- upgrade v1 -> UP -> backfill -> verify pass;
- 204 instance = 204 detail = 204 baseline revision;
- 762 member links;
- rerun idempotent, counts không đổi;
- constraint/trigger 29/29 pass;
- rollback guard từ chối khi có v2-only amendment data và clean DOWN khi an toàn.

Không chạy destructive DOWN trên database thật sau khi có v2 writes.

---

# PHẦN E — API, READ CONTRACT VÀ ERROR CONTRACT

## E1. Payload v2

Contract khái niệm:

~~~text
VisitRequestFormV2
  submissionId
  registrant
  primaryContact
  partnerId
  campusVisits[]

CampusVisit
  visitInstanceId? / clientKey
  campusId hoặc campusCode theo convention hiện hành
  startDatetime
  endDatetime
  delegationName
  visitType
  visitTypeOther
  purpose
  workingContent
  visitors[]
  supportMembers[]
  operationalContact
  workingLanguage
  transportationNote
  mediaConsentStatus
  mediaConsentNote
  noteToFptu
  processing
  rowVersion?
~~~

Backend không tin:

- visitScope;
- formSchemaVersion;
- hasMixedCampusDetails;
- status;
- revision;
- coordinator/decision;
- visitorUserId;
- sameForAll.

Frontend luôn gửi snapshot đã resolve đầy đủ cho từng campus.

## E2. Route strategy

Không đổi shape âm thầm trên route v1 trong rolling deployment.

Giữ route v1 hiện hành cho v1 compatibility.

Route v2 đã có:

- POST /api/v2/visit-requests.
- GET /api/v2/visit-requests/{id} reference/canonical read.

Route v2 dự kiến:

- pending edit;
- resubmit;
- safe-details;
- identity changes;
- amendment submit/approve/reject/withdraw;
- history;
- scoped search.

Public initiate phải lưu canonical v2 payload trong pending session; verify chỉ xác nhận session/submission/OTP, không được nhận một form khác rồi tạo.

## E3. Read classification

### Request-level flat DTO

- v1 byte-identical;
- v2 non-mixed đọc detail per-campus, không global;
- v2 mixed trả 409 FORM_VERSION_UPGRADE_REQUIRED vì DTO phẳng không biểu diễn được dữ liệu khác nhau.

### Instance-level DTO

- v2 mixed vẫn trả 200;
- chỉ đọc target visit_instance_id;
- sibling campus khác không liên quan;
- missing detail trả 409 VISIT_FORM_DETAIL_MISSING;
- không global fallback.

### Aggregate/list/report

- không chọn campus đầu tiên;
- parent common data + per-campus collection/sections/match contexts;
- scope trước projection/search;
- không leak hidden campus.

## E4. Error codes ổn định

Ít nhất giữ/triển khai:

- VISIT_FORM_VALIDATION_FAILED
- VISIT_DURATION_TOO_SHORT
- DUPLICATE_CAMPUS
- VISIT_NOT_EDITABLE
- VISIT_NOT_RESUBMITTABLE
- VISIT_FORM_CONCURRENCY_CONFLICT
- VISIT_INSTANCE_SCOPE_FORBIDDEN
- VISIT_FORM_DETAIL_MISSING
- FORM_VERSION_UPGRADE_REQUIRED
- PER_CAMPUS_V2_READ_REQUIRED
- IDENTITY_CHANGE_ALREADY_PENDING
- IDENTITY_CHANGE_EMAIL_UNCHANGED
- IDENTITY_CHANGE_TARGET_NOT_ALLOWED
- IDENTITY_CHANGE_EXPIRED
- IDENTITY_CHANGE_CONFLICT
- IDENTITY_GOOGLE_EMAIL_MISMATCH
- IDENTITY_CONFIRMATION_REQUIRED
- IDENTITY_CHANGE_SUPERSEDED
- CONTACT_EMAIL_INTERNAL_ACCOUNT_CONFLICT
- CONTACT_ACCOUNT_NOT_ACTIVE
- OTP_INVALID_OR_EXPIRED
- OTP_RATE_LIMITED
- AMENDMENT_ALREADY_PENDING
- AMENDMENT_NOT_EDITABLE
- AMENDMENT_BASE_REVISION_CONFLICT
- AMENDMENT_APPROVER_SCOPE_FORBIDDEN
- AMENDMENT_WINDOW_EXPIRED
- SEARCH_SCOPE_FORBIDDEN

Public errors không được tiết lộ email/account có tồn tại. Per-campus validation trả stable field path để frontend focus đúng campus/field.

---

# PHẦN F — PERMISSION VÀ LIFECYCLE

## F1. Actor permissions

### Registrant đúng request

- xem toàn request;
- pending edit/resubmit nếu lifecycle hợp lệ;
- safe edit/amendment trước cutoff;
- initial identity invitation action;
- đề xuất transfer;
- cancel chỉ theo ngoại lệ 3A khi initial contact pending.

### Primary contact ACTIVE

- xem toàn request;
- co-edit/resubmit/safe edit/amendment;
- đề xuất transfer;
- owner cancel theo policy hiện hành.

### Staff Leader

- chỉ campus của mình;
- approve/reject campus;
- assign host;
- approve/reject amendment đúng campus;
- không sửa form do role, trừ khi đồng thời là exact registrant.

### Host/participant/department/student

- chỉ instance có relation;
- không nhận all-campus payload rồi filter ở client.

### HO

- monitor/read-only toàn bộ;
- không approve/reject request tổng hoặc amendment.

### Admin/người ngoài

- không có visit business action;
- raw security audit chỉ cho role chuyên biệt nếu hệ thống có.

## F2. Lifecycle edit

- Tất cả instance WAITING_REQUEST_APPROVAL, earliest start >=24h: pending edit, có thể add/remove campus.
- Tất cả instance REJECTED, earliest start >=24h: resubmit; campus set giữ nguyên.
- PARTIALLY_APPROVED/APPROVED/ASSIGNED/BEFORE_VISIT, >=24h: safe edit + amendment.
- Dưới 24h: khóa new amendment/transfer self-service; media consent withdrawal vẫn được apply urgent.
- DURING_VISIT/AFTER_VISIT/CLOSED/CANCELLED: khóa self-service; support correction workflow ngoài scope.

Approval state và amendment state độc lập. Không reset campus approval vì amendment pending.

---

# PHẦN G — AUDIT, REVISION, SECURITY VÀ NOTIFICATION

## G1. Audit invariant

Audit schema tồn tại không có nghĩa behavior đã đầy đủ.

Mọi mutation mới phải:

- ghi audit header;
- ghi field/event rows;
- cùng transaction business;
- audit failure làm mutation failure;
- stable field path;
- masked email/sensitive value;
- correlation_id;
- visit_request_id;
- visit_instance_id khi áp dụng;
- source_type/source_id;
- reason.

Không dựa vào middleware tự suy luận diff. Dùng IVisitAuditWriter/domain service và architecture/integration test audit completeness.

## G2. Revision

- form_revision tăng với mọi applied instance change.
- approval_revision tăng khi approval-sensitive amendment được approve/apply.
- safe change không làm mất pending amendment.
- revision history immutable.
- request revision lưu request-level display snapshots.
- identity event là source lịch sử account/email relation.

## G3. Audit actions tối thiểu

- VISIT_REQUEST_CREATED_V2
- PRIMARY_CONTACT_INVITATION_CREATED
- PRIMARY_CONTACT_INVITATION_RESENT
- PRIMARY_CONTACT_INVITATION_DECLINED/EXPIRED/CANCELLED
- PRIMARY_CONTACT_CLAIM_APPLIED
- PRIMARY_CONTACT_TRANSFER_REQUESTED/APPLIED
- VISIT_SAFE_FIELDS_UPDATED
- VISIT_AMENDMENT_SUBMITTED
- VISIT_AMENDMENT_APPROVED/REJECTED/WITHDRAWN/EXPIRED
- VISIT_INSTANCE_FORM_REVISION_APPLIED
- VISIT_REQUEST_CANCELLED_BY_REGISTRANT_PENDING_CONTACT
- IDENTITY_CHANGE_REDACTED

## G4. Security

- Không log raw OTP, Google token, acceptance token, session token, full PII payload.
- Token lưu hash, single-use, expiry.
- Resend supersede token cũ.
- Rate-limit theo normalized email + IP/device/session.
- Public response chống account enumeration.
- Authorization re-query DB mỗi request; không cache ownership lâu trong JWT.
- GET invitation chỉ masked và không mutation.
- Accept/decline yêu cầu authenticated session + CSRF + exact normalized email.
- Escape/sanitize HTML/email/print.
- File import chống malformed, oversize, formula injection.

## G5. Notification

- Chỉ dispatch sau transaction commit.
- Idempotent/dedupe.
- Rollback không gửi.
- Retry submission không gửi trùng.
- Staff Leader nhận đúng campus content.
- B nhận initial claim email nếu B khác A.
- Không nhét full diff/PII vào notification metadata; người nhận mở endpoint authorized.

---

# PHẦN H — TIẾN ĐỘ ĐÃ HOÀN THÀNH

## H1. SQL PR-2 — hoàn thành

Additive migration, backfill, verify, rollback guard và fresh-create đã chạy trên MySQL thật như mô tả ở D9.

## H2. Persistence/dual-read PR-3 — hoàn thành nền tảng

Đã thêm/mapping 8 entities:

- VisitInstanceFormDetail
- VisitInstanceGuestMember
- VisitRequestIdentityChange
- VisitRequestIdentityChangeEvent
- VisitInstanceAmendment
- VisitInstanceAmendmentChange
- VisitInstanceFormRevisionHistory
- VisitRequestRevisionHistory

DbContext có:

- shared-PK one-to-one;
- alternate/composite keys;
- composite FKs;
- delete behavior/index;
- generated guard columns không EF-write.

IVisitFormReadService:

- v1 đọc global projection;
- v2 chỉ đọc detail+links;
- missing detail không fallback;
- scope trước projection;
- batched/no N+1.

## H3. Read-detail handlers — reported migrated

Các handler đã được báo cáo migrate trong các session/merge trước; AI tiếp nhận phải verify audit map/code vì report count cũ có mâu thuẫn:

1. GetSubmittedVisitRequestFormDetail
2. GetEditableVisitRequestDetail
3. GetVisitProcessDetail
4. GetVisitInstanceSummary
5. GetVisitInstanceContribution
6. GetVisitInvitationDetail
7. GetStaffCalendarDetail
8. GetRequestDetail (Department)
9. GetInvitationDetail (Department)
10. GetVisitInvitationById
11. GetAgendaSetupForInstance

Request-level flat handlers trả 409 khi mixed. Instance-level handlers trả 200 target-only khi mixed.

Known consolidated commits sau Dev merge:

- 770caa33
- fb9a11c6

Một số pre-merge commits có thể đã bị consolidate; không dựa vào hash cũ nếu git log không còn.

## H4. Command/export read consumers — hoàn thành 7/7

Đã được báo cáo v2-safe:

1. ApproveCampusInstance
2. RejectCampusInstance
3. InviteVisitParticipant
4. AssignDepartmentStaff
5. ExecuteEmailAction
6. GetEmailActionInfo
7. ExportDeptLeaderInvoice

Commits:

- 38f22143
- 836041f0

Các consumer instance-level phải lấy delegation/form từ target instance.

## H5. Create-v2 core — hoàn thành và test

Commits:

- 0f67eff8: aggregate service, write flag, fingerprint v2, DTOs.
- 4dd1c1d4: command, POST /api/v2/visit-requests, two-flag gate, idempotency.
- 3d6c0ce8: report checkpoint.

Đã triển khai:

- một transaction tạo parent + N instances + N details;
- Staff Leader routing;
- members per-campus độc lập;
- links;
- baseline instance/request revisions;
- VISIT_REQUEST_CREATED_V2 audit;
- INITIAL_CLAIM 72h;
- form_schema_version=2;
- backend-derived scope/mixed;
- version-tagged v2 fingerprint;
- smallest-campus compatibility projection;
- submissionId idempotency;
- concurrent unique-race rollback/return winner;
- single, multi-same, multi-mixed;
- member copy independent IDs;
- A=B ACTIVE;
- A khác B PENDING;
- 29m fail/30m pass;
- duplicate campus;
- mixed ignores campus/time.

Latest gate:

- Unit 474/474.
- Architecture 14/14.
- Integration 320/320.

## H6. Deferred từ create-v2 — chưa được quên

Create-v2 core đã chạy nhưng luồng end-user chưa hoàn chỉnh cho tới khi đóng:

1. FluentValidation structural validator tại API boundary.
2. Public OTP create-v2 bridge.
3. Post-commit Staff Leader notification và B invitation delivery.

Business validation trong service không thay thế API structural validation. Authenticated endpoint không thay thế public OTP form. Identity row không thay thế việc gửi invitation.

## H7. Mâu thuẫn/stale report phải sửa

AI tiếp nhận phải kiểm tra và cập nhật report:

- Một đoạn report cũ nói write flag chưa tạo; latest truth là write flag đã tạo và OFF.
- Report có nơi ghi 6 read handlers, trong khi hội thoại/audit map nêu nhiều handler hơn; verify code và lập danh sách canonical.
- Unit baseline cũ 435, latest clean baseline 474.
- FINAL_IMPLEMENTATION_REPORT phải giữ STATUS: IN PROGRESS.
- Không tuyên bố Phase B end-user complete nếu public OTP/notification chưa đóng.

---

# PHẦN I — PHẦN CÒN LẠI VÀ THỨ TỰ THỰC HIỆN

## I0. Việc đầu tiên khi resume

1. Verify branch/HEAD/status/log sau Dev merge.
2. Verify flags/defaults.
3. Verify persistent DB v2 count read-only.
4. Verify all reported handlers/consumers.
5. Correct stale progress report.
6. Không redo phần đã xanh nếu code/test chứng minh còn nguyên.

## I1. B-2.5 — đóng create-v2 end-to-end

### Structural validator

- campusVisits required/non-empty;
- max campus configurable, mặc định 10;
- no duplicate campus;
- ACTIVE campus;
- required/trimmed strings;
- OTHER needs value;
- email/phone convention;
- member limits, mặc định 200 mỗi loại/campus;
- time and 30-minute boundary;
- reject system-derived client fields;
- service vẫn revalidate.

### Public OTP create-v2

- write OFF: v1 flow byte-identical;
- write ON/read OFF: reject;
- both ON: OTP hợp lệ mới CreateV2Async;
- OTP bind registrant email + submission/pending payload;
- verify không nhận form khác;
- consumed/replay/rate-limit;
- same submission idempotent;
- validation fail không partial;
- không log OTP.

### Post-commit notifications

- Staff Leader per campus;
- B initial invitation;
- no notification on rollback;
- no duplicates on retry;
- correct instance content;
- use existing outbox/after-commit pattern honestly;
- document limitation nếu chưa exactly-once.

## I2. Phase C — pending edit v2

Tạo v2 endpoint/command và write-flag gate.

### Authorization

- registrant_user_id;
- ACTIVE visitor_user_id;
- pending B không có quyền;
- unrelated actor forbidden;
- role Staff/Host không tự có form edit.

### Concurrency

- expected request rowVersion;
- expected rowVersion từng instance;
- stable visitInstanceId;
- conflict 409 VISIT_FORM_CONCURRENCY_CONFLICT;
- không last-write-wins.

### Mutation

- common registrant display corrections;
- per-campus detail;
- schedule;
- full replace guest/support target instance;
- add campus pending;
- remove campus pending nếu không có downstream blocker;
- không full-replace approved/started/rejected sai flow;
- account-binding email không đổi như text thường;
- operational contact email vẫn chỉ snapshot.

### Member handling

- v2 independent;
- legacy shared copy-on-write;
- remove links target-only;
- orphan delete chỉ khi FK/history cho phép.

### Recompute/side effects

- scope;
- mixed;
- fingerprint;
- compatibility projection;
- revisions;
- row versions;
- audit;
- after-commit notifications;
- add campus routing/revoke removed instance artifacts.

## I3. Phase C — rejected resubmit v2

Điều kiện:

- actor authorized;
- tất cả instances REJECTED;
- earliest start/cutoff rule;
- campus set giữ nguyên;
- visitInstanceIds giữ nguyên.

Transaction:

- lock request/instances;
- expected versions;
- snapshot old rejection decisions;
- replace detail/member per instance;
- preserve history;
- clear/reinitialize correct decision fields;
- increment resubmission_count/revisions;
- recompute;
- route leaders;
- audit;
- after-commit notification;
- concurrent one winner.

## I4. Phase D — identity

Triển khai endpoints/state machine:

- create/change initial invitation;
- resend;
- cancel;
- masked GET landing;
- accept;
- decline;
- transfer from ACTIVE owner;
- Google exact email;
- OTP fallback only if enabled;
- supersede/replay/expiry;
- old owner keeps access until apply;
- transaction swaps visitor_user_id;
- old account remains ACTIVE but loses request relation;
- no approval reset;
- self-service cutoff before DURING_VISIT and 24h.

Background jobs:

- expire 72h/24h;
- redact failures after 90 days;
- batch/idempotent;
- events/metrics.

Cancel 3A:

- handler;
- allowedActions;
- SQL trigger already prepared;
- tests initial pending vs ACTIVE vs transfer pending.

## I5. Phase E — safe edit/amendment

- backend field classifier;
- safe immediate apply;
- privacy urgent media decline;
- one pending amendment/instance;
- proposed patch old/new;
- approved snapshot unchanged before approval;
- current Staff Leader approve/reject;
- base form/approval revision conflicts;
- safe concurrent edits do not erase amendment;
- approve patch atomically;
- calendar/reminder/logistics sync;
- audit/history/notification;
- reject/withdraw/expire preserve active snapshot;
- sibling campuses unaffected;
- lock from DURING_VISIT.

## I6. Phase F — list/search/dashboard/report/export/email

Use PR3_PRE_PR4_AUDIT_MAP.

Migrate every Class-C surface:

- delegation lists;
- department/staff calendars;
- dashboards;
- assignments progress;
- invitation lists;
- eligible news instances;
- host candidates;
- related visitor details;
- HO/Staff Leader/Department reports;
- invoice;
- export/print;
- email previews/templates;
- conflict labels.

Rules:

- list request once;
- instance content target-only;
- aggregate per-campus sections;
- no smallest-campus representation;
- search scope-before-keyword;
- no hidden side channel;
- export/email sanitized and authorized.

Kết thúc bằng repository-wide audit 10 global fields và zero-unclassified-reference report.

## I7. Phase G — frontend

### Form

- request-level registrant;
- request-level primary contact;
- campus cards/tabs;
- mỗi campus đầy đủ lịch/form/members/operational contact/additional requirements;
- copy from campus là deep copy một lần;
- add/remove;
- no hidden shared state;
- nested errors;
- dirty state;
- draft v1->v2;
- max limits/config;
- 30-minute validation.

### Display

- post-submit summary theo campus;
- status/Staff Leader/Host/revision per campus;
- request common data một lần;
- mixed content rõ ràng;
- request-level legacy 409 route sang v2 UI, không show raw technical error.

### Edit/workflow

- pending edit/resubmit;
- initial claim/transfer;
- safe edit/amendment;
- active vs proposed diff;
- approve/reject/withdraw;
- history/audit masked;
- allowedActions từ backend nhưng command vẫn re-authorize.

### Frontend gates

- TypeScript;
- lint;
- Vitest/RTL hoặc framework tương đương;
- build;
- Playwright E2E;
- accessibility/keyboard;
- responsive 390px;
- VI/EN.

## I8. Phase H — final verification/rollout

- SQL fresh/upgrade/backfill/idempotency/rollback on disposable MySQL;
- backend build;
- full Unit/Architecture/Integration;
- frontend install/lint/unit/build;
- E2E single/multi-same/multi-mixed;
- role matrix;
- missing detail;
- 29m59s/30m;
- identity claim/transfer/expiry;
- cancel 3A;
- safe/amendment;
- search no leak;
- export/email sections;
- concurrency/idempotency;
- downstream minutes/feedback/OCR/gallery/partner links;
- no N+1/query bounds;
- no raw secret/PII logs.

Feature enable:

- additive SQL first;
- backend/read compatibility;
- backfill verify;
- frontend flag OFF;
- canary;
- metrics;
- gradual rollout;
- rollback via flags, not dropping tables.

## I9. Phase I — contract cleanup prep

Chỉ sau:

- zero legacy runtime reads;
- all v1 data backfilled/cutover;
- no old client/draft;
- rollback/export proven.

Chuẩn bị guarded migration:

- drop 10 legacy global fields/index/check;
- update fresh-create clean v2;
- test disposable;
- never apply destructive contract migration to real DB automatically.

---

# PHẦN J — TEST MATRIX TỐI THIỂU

## J1. Database

- fresh import;
- upgrade;
- backfill rerun;
- one detail/instance;
- no orphan/cross-request;
- duration boundaries;
- OTHER blank;
- unique pending identity/amendment;
- expiry/redaction;
- audit indexes/survival;
- EXPLAIN critical queries.

## J2. Backend create

- flag combinations;
- public OTP;
- authenticated create;
- single/multi same/mixed;
- same form different campus/time mixed=0;
- member independent;
- A=B;
- A khác B;
- invalid campus;
- duplicates;
- transaction rollback;
- sequential/concurrent idempotency;
- route leader;
- revision/audit;
- no duplicate notification.

## J3. Backend edit/resubmit

- registrant/ACTIVE contact;
- pending/unrelated forbidden;
- update A no B;
- add/remove;
- approved/started blocked;
- 30 min;
- copy-on-write;
- request/instance conflicts;
- rollback;
- revision/audit/recompute;
- all-rejected only;
- IDs preserved;
- history preserved.

## J4. Identity

- exact Google email;
- wrong/missing account;
- GET no mutation;
- accept replay;
- expired/superseded;
- transfer old/new rights;
- account old not deleted;
- approval unchanged;
- OTP fallback rate-limit;
- concurrent apply;
- jobs and retention.

## J5. Amendment

- classifier;
- safe;
- urgent media decline;
- one pending;
- approved snapshot invariant;
- approver scope;
- base revision conflict;
- approve/reject/withdraw/expire;
- sibling isolation;
- downstream sync;
- audit completeness.

## J6. Authorization/search

- registrant/contact all own campuses;
- Staff Leader own campus;
- host/participant relation;
- HO read-only all;
- Admin/unrelated forbidden;
- IDOR object IDs;
- hidden keyword no hit/count/score/context leak.

## J7. Frontend/E2E

- form shape/deep copy/errors/draft;
- review cards;
- identity states;
- amendment diff;
- responsive/accessibility;
- public OTP multi-campus;
- authenticated create;
- edit/resubmit;
- reports/export/email;
- rolling compatibility.

---

# PHẦN K — GIT/COMMIT POLICY BẮT BUỘC

## K1. Không có tên AI

Không để trong author, committer, message hoặc trailers:

- Claude;
- Claude Code;
- Claude Opus;
- AI Agent;
- Generated by AI;
- Co-Authored-By của AI;
- Assisted-by/Generated-With tương tự.

Không tự đổi global git config.

Trước commit kiểm tra local user.name/user.email. Không tự đoán email.

Sau commit verify:

~~~text
git show -s --format=fuller HEAD
git log -1 --format="%an <%ae>%n%cn <%ce>%n%B"
~~~

Nếu AI attribution xuất hiện và commit chưa push, amend ngay. Không rewrite shared/pushed history nếu người dùng chưa yêu cầu.

## K2. Gom theo functional slice

Không commit từng file/handler nhỏ.

Không tạo commit riêng chỉ chứa:

- report/progress;
- checkpoint;
- hash fixup;
- test count;
- comments/XML docs;
- mock adjustment liên quan feature.

Gom production code + DTO/validator + tests + report của cùng behavior.

Mỗi phase lớn ưu tiên 1–3 commits có thể review/test/rollback độc lập.

Không sửa file không liên quan chỉ để tăng số file. Commit một file chỉ khi bản chất thay đổi thật sự độc lập và chỉ cần một file; ghi lý do trong report.

Nếu quên report/tests và commit chưa push, amend thay vì tạo docs-only commit.

Ví dụ:

- feat(delegations): support per-campus pending edits
- feat(delegations): support rejected-request resubmission
- feat(delegations): implement primary-contact claim and transfer
- feat(delegations): add post-approval amendments

Không đưa tên model AI hoặc nội dung hội thoại vào commit.

## K3. Không push/merge tự động

- Được commit local theo functional slice.
- Không push, merge, rebase shared history hoặc mở PR nếu người dùng chưa yêu cầu.
- Preserve unrelated user changes.

---

# PHẦN L — QUY TẮC LÀM VIỆC VÀ CHECKPOINT

1. Dùng rg/rg --files để rà code.
2. Không mutation database thật.
3. Disposable DB phải tạo fresh từ PR-2 master.
4. Restore appsettings sau test bằng cơ chế an toàn/trap.
5. Không claim pass nếu test chưa chạy.
6. Nếu test bị infrastructure/encoding lỗi, chứng minh bằng baseline/delta và sửa fixture reproducibly; không chỉnh tay DB một lần để “làm xanh”.
7. Không sửa production seed để chiều test.
8. Scope trước projection.
9. Không fallback v2.
10. Không N+1.
11. Không tạo code half-tested.
12. Cập nhật IMPLEMENTATION_PROGRESS/report trong cùng functional commit.
13. Kết thúc session chỉ khi:
    - phase hiện tại xanh và committed; hoặc
    - hard blocker/hard-limit với checkpoint sạch.

Checkpoint phải ghi:

- HEAD/commits;
- files/modules;
- test discovered/pass/fail/skipped;
- flags;
- v2 persistent count;
- DB nào được dùng;
- appsettings restored;
- git status;
- phần done/deferred;
- exact next action.

---

# PHẦN M — DEFINITION OF DONE CUỐI

Chỉ đánh dấu FINAL khi tất cả điều sau đúng:

1. SQL preflight/UP/backfill/verify/fresh/rollback được test MySQL thật.
2. Domain/DbContext/API/backend/frontend đồng bộ v2.
3. Public OTP và authenticated create v2 hoạt động.
4. Pending edit/resubmit per-campus hoàn chỉnh.
5. 30 phút enforce FE/BE/DB.
6. Primary contact initial claim/transfer không cấp quyền trước verify.
7. Old account không bị xóa/khóa sau transfer.
8. Cancel 3A đồng nhất API/allowedActions/trigger.
9. Safe/amendment giữ approved snapshot trước approval và không reset sibling.
10. Revision/audit đầy đủ trong cùng transaction.
11. Expiry 72h/24h và retention 90 ngày có jobs/tests.
12. Search/list/dashboard/report/export/email v2-safe, no hidden leak.
13. Frontend create/edit/read/identity/amendment hoàn chỉnh.
14. Unit/Architecture/Integration/frontend/component/E2E đều xanh.
15. Downstream minutes/feedback/OCR/gallery/partner links không regression.
16. Feature flags, metrics, canary và rollback được tài liệu hóa/test.
17. Zero-unclassified global-field references.
18. Contract cleanup được chuẩn bị/test disposable nhưng không chạy destructive trên DB thật.
19. Git status sạch ngoài plan untracked đã được chủ dự án xác nhận.
20. Commit metadata không chứa tên AI và commit được gom theo functional slice.
21. FINAL_IMPLEMENTATION_REPORT chuyển từ IN PROGRESS sang FINAL, liệt kê toàn bộ commits/tests/limitations/rollout.

---

# PHẦN N — LỆNH BẮT ĐẦU CHO AI TIẾP NHẬN

Bắt đầu bằng:

1. Verify repository/branch/HEAD/status.
2. Đọc các report/plan/audit map.
3. Rà và sửa các mâu thuẫn tiến độ ở H7.
4. Không redo phần đã được code/test chứng minh.
5. Đóng B-2.5: structural validator + public OTP create-v2 + post-commit notification.
6. Tiếp tục Phase C pending edit và resubmit.
7. Nếu còn budget, tự chuyển Phase D, không hỏi người dùng.
8. Tuân thủ Git policy K: không AI attribution, không commit report một file, gom functional slices.

Chỉ trả một báo cáo cuối session khi phase đang làm đã xanh/commit hoặc có hard blocker/hard-limit thật sự.

