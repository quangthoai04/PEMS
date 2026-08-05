# PEMS — Master plan hard cutover Operational Contact và duyệt độc lập theo campus

## 1. Mục tiêu và quy tắc khóa

Triển khai một lần sang mô hình cuối trong môi trường dev, không giữ tương thích với mô hình cũ.

Các quy tắc bắt buộc:

1. Không có HO duyệt đơn một cơ sở hoặc liên cơ sở. HO chỉ xem/monitor read-only.
2. Mỗi `STAFF_LEADER` chỉ duyệt hoặc từ chối `visit_instance_id` thuộc campus của mình.
3. Đơn liên cơ sở không có quyết định tổng của HO; các campus được quyết định độc lập.
4. Mọi campus phải có Operational contact đã xác nhận trước khi bất kỳ campus nào xuất hiện trong hàng chờ duyệt của Staff Leader.
5. Nếu email Operational contact sau normalize trùng email đã xác thực của Registrant, backend tự liên kết cùng `user_id`, ghi audit và không gửi email xác nhận.
6. Không coi tên hoặc số điện thoại trùng là bằng chứng cùng người. Danh tính chỉ khớp bằng `registrant_user_id` và normalized verified email.
7. Operational contact khác Registrant luôn phải xác nhận đúng campus qua email, kể cả tài khoản đó đã tồn tại hoặc là tài khoản nội bộ.
8. Registrant có thể là `VISITOR`, `STAFF` hoặc `STAFF_LEADER`; role người tạo không được bypass cổng xác nhận.
9. Sau khi cổng xác nhận mở, Staff Leader đúng campus được duyệt và phải chọn Host trong cùng thao tác; cho phép tự chọn mình làm Host nếu hợp lệ và không trùng lịch.
10. Backend là nguồn quyết định quyền và `allowedActions`; frontend chỉ render theo contract backend.
11. Hard cutover: không backup, backfill, dual-read, dual-write, feature flag tương thích hoặc giữ workflow Primary contact.

## 2. Mô hình nghiệp vụ cuối

### 2.1 Vai trò theo phạm vi

| Actor/relation | Quyền cuối |
|---|---|
| Registrant | Xem toàn request và tất cả campus; quản lý phần request-level; theo dõi xác nhận; sửa đầu mối trước quyết định; hủy toàn request khi trạng thái cho phép |
| Operational contact đã xác nhận | Xem và thao tác nghiệp vụ được phép trên đúng `visit_instance_id` có `operational_contact_user_id = currentUserId`; không thấy sibling campus |
| Operational contact chưa xác nhận | Chỉ xem landing xác nhận đã mask; chưa có quyền vào request/campus |
| Staff Leader đúng campus | Chỉ thấy/duyệt/từ chối campus mình sau khi toàn bộ đầu mối của request đã xác nhận |
| Staff Leader khác campus | Không thấy và không xử lý instance |
| Host | Chỉ xử lý instance được gán qua `current_host_user_id` |
| HO | Xem/monitor toàn bộ ở chế độ read-only; không approve, reject, assign host hoặc cancel theo vai trò HO |
| ADMIN | Không có business action trên visit nếu rule hiện tại không cấp |

Một user có thể đồng thời là Registrant, Operational contact, Staff Leader hoặc Host. Backend phải cộng capability độc lập; không dùng một `relation` duy nhất làm mất quyền khác.

### 2.2 Trạng thái request

```text
PENDING_CONTACT_CONFIRMATION
    -> PENDING_APPROVAL
    -> PARTIALLY_APPROVED
    -> APPROVED

PENDING_APPROVAL / PARTIALLY_APPROVED
    -> REJECTED khi tất cả campus bị từ chối

Các trạng thái hợp lệ theo rule riêng
    -> CANCELLED
```

Quy tắc aggregate, bỏ qua campus đã `CANCELLED`:

| Điều kiện | `visit_requests.status` |
|---|---|
| Còn ít nhất một campus chưa có `operational_contact_user_id` | `PENDING_CONTACT_CONFIRMATION` |
| Đã xác nhận đủ; chưa campus nào approved; còn campus chờ quyết định | `PENDING_APPROVAL` |
| Có ít nhất một campus approved và còn campus chờ quyết định | `PARTIALLY_APPROVED` |
| Không còn campus chờ và có ít nhất một campus approved | `APPROVED` |
| Tất cả campus bị reject | `REJECTED` |
| Hủy toàn request | `CANCELLED` |

Trường hợp một campus approved, một campus rejected và không còn campus chờ thì request tổng là `APPROVED`; chi tiết vẫn thể hiện đúng từng campus.

### 2.3 Trạng thái campus

```text
WAITING_CONTACT_CONFIRMATION
    -> WAITING_REQUEST_APPROVAL
    -> BEFORE_VISIT
    -> DURING_VISIT
    -> AFTER_VISIT
    -> CLOSED

WAITING_REQUEST_APPROVAL -> REJECTED
Các trạng thái hợp lệ theo rule riêng -> CANCELLED
```

- Campus tự khớp Registrant được chuyển ngay sang `WAITING_REQUEST_APPROVAL`.
- Campus khác giữ `WAITING_CONTACT_CONFIRMATION` đến khi accept.
- Dù một campus đã là `WAITING_REQUEST_APPROVAL`, Staff Leader vẫn chưa thấy nếu request cha còn `PENDING_CONTACT_CONFIRMATION`.
- `ASSIGNED` bị loại bỏ. Approve phải gán Host trong cùng transaction và chuyển campus sang `BEFORE_VISIT`.

## 3. Luồng xử lý chuẩn

### 3.1 Submit request

Trong một transaction:

1. Validate OTP/SSO của Registrant và toàn bộ payload.
2. Validate mỗi campus có ít nhất một Staff Leader `ACTIVE`; thiếu ở bất kỳ campus nào thì fail toàn bộ submit.
3. Tạo một request cha và N campus instance/detail/member độc lập.
4. Với từng campus:
   - Normalize Operational contact email.
   - Nếu trùng verified email của Registrant: set `operational_contact_user_id = registrant_user_id`, status campus `WAITING_REQUEST_APPROVAL`, ghi event `OPERATIONAL_CONTACT_AUTO_CONFIRMED_REGISTRANT_MATCH`; không tạo invitation và không gửi mail.
   - Nếu khác: để `operational_contact_user_id = NULL`, status campus `WAITING_CONTACT_CONFIRMATION`, tạo identity change `INITIAL_CONFIRMATION/PENDING` có token hash và hạn 72 giờ.
5. Nếu còn campus chưa xác nhận: request `PENDING_CONTACT_CONFIRMATION`; nếu không còn: request `PENDING_APPROVAL`.
6. Ghi history/audit và commit.
7. Sau commit mới gửi email confirm cho các contact khác Registrant. Chỉ khi request đã sẵn sàng mới gửi notification duyệt tới Staff Leader của từng campus.

Retry cùng submission/idempotency key phải trả lại kết quả cũ, không tạo request, identity row hoặc email lần hai.

### 3.2 Xác nhận Operational contact

Landing GET chỉ trả dữ liệu mask: request code, campus, lịch, organization và trạng thái token; không mutate.

Accept yêu cầu:

- token hash hợp lệ, single-use, chưa hết hạn và status `PENDING`;
- session đã authenticated;
- normalized account email đúng `target_email`;
- account `ACTIVE`; tài khoản nội bộ được phép xác nhận, không ép mọi contact thành `VISITOR`;
- request/campus chưa cancel và instance thuộc đúng request;
- không có identity change khác đã thắng race.

Transaction lock theo thứ tự cố định: request -> campus -> identity change. Sau đó:

1. Set `operational_contact_user_id`.
2. Chuyển campus `WAITING_CONTACT_CONFIRMATION -> WAITING_REQUEST_APPROVAL`.
3. Chuyển identity change thành `APPLIED`, ghi event và timestamp.
4. Recompute request aggregate.
5. Nếu đây là xác nhận cuối: request thành `PENDING_APPROVAL`, tăng gate revision và tạo notification Staff Leader theo từng campus.
6. Commit rồi mới dispatch notification/email.

Accept lặp lại phải idempotent. Hai xác nhận cuối chạy đồng thời chỉ được mở gate và gửi một notification cho mỗi campus.

### 3.3 Decline, expiry, resend và đổi contact

- `DECLINED`/`EXPIRED`: không cấp quyền; campus và request tiếp tục chờ xác nhận.
- Registrant được thay contact, resend hoặc hủy request theo state/cutoff hiện hành.
- Resend tạo token version mới và `SUPERSEDED` token cũ ngay trong transaction.
- Trước khi có bất kỳ campus decision nào, đổi một contact đã auto/đã confirm sang email khác sẽ clear relation campus đó, đóng lại global gate và chặn toàn bộ Staff Leader processing.
- Sau khi đã có quyết định campus, đổi contact phải dùng transfer: người cũ giữ quyền đến khi người mới accept; không reset quyết định và không ảnh hưởng sibling campus.
- Transfer hết hạn/decline không đổi owner hiện tại.

### 3.4 Staff Leader duyệt độc lập

Staff Leader queue chỉ trả row khi:

```text
request.status != PENDING_CONTACT_CONFIRMATION
campus.status == WAITING_REQUEST_APPROVAL
campus.campus_id == CurrentUser.PrimaryCampusId
CurrentUser == ACTIVE STAFF + LEADER
```

Approve body bắt buộc `hostUserId`, `decisionNote` tùy rule và `rowVersion`.

Backend kiểm tra lại:

- cổng xác nhận vẫn mở;
- actor đúng campus và active;
- instance chưa có decision;
- host active, hợp lệ cùng campus;
- cho phép `hostUserId == currentUserId`;
- không có `HOST_SCHEDULE_CONFLICT`;
- row version còn đúng.

Approve + assign Host + audit + aggregate recompute trong cùng transaction. Reject bắt buộc lý do, chỉ đổi đúng instance và recompute aggregate. First valid decision wins; retry cùng idempotency key trả kết quả cũ.

HO không có endpoint mutation tương đương. Gọi trực tiếp API approve/reject/assign-host bằng role HO phải trả `403`.

## 4. Database hard cutover

### 4.1 Schema cuối

`visit_requests`:

- Giữ `registrant_user_id` và snapshot Registrant.
- Thêm `PENDING_CONTACT_CONFIRMATION` và `PARTIALLY_APPROVED` vào aggregate status.
- Xóa toàn bộ cột account/contact/decision cấp request của mô hình Primary contact và HO approval.
- Request không còn `decided_by/decision_actor_role` đại diện cho một quyết định tổng; quyết định nằm ở từng campus.

`visit_request_campuses`:

```sql
operational_contact_user_id BIGINT UNSIGNED NULL,
FOREIGN KEY (operational_contact_user_id)
    REFERENCES users(user_id)
    ON UPDATE RESTRICT
    ON DELETE RESTRICT
```

- Không `UNIQUE` trên `operational_contact_user_id` vì một người có thể phụ trách nhiều campus.
- Giữ decision, `decided_by`, `decided_at`, `decision_note`, `current_host_user_id` và `row_version` ở cấp instance.
- Bổ sung `WAITING_CONTACT_CONFIRMATION`, `PARTIALLY_APPROVED` chỉ ở request level và `REJECTED` ở campus level.
- Xóa `ASSIGNED` khỏi enum/status transition.

`visit_instance_form_details`:

- Giữ `operational_contact_full_name`, `operational_contact_organization`, `operational_contact_phone`, `operational_contact_email`.
- Email `NOT NULL`, trim không rỗng và normalize tại application boundary.
- Xóa `note_to_fptu` nếu đã nằm trong quyết định hard cutover trước.

`visit_request_identity_changes` được định nghĩa lại chỉ cho Operational contact:

- `visit_instance_id BIGINT UNSIGNED NOT NULL`.
- Composite FK `(visit_request_id, visit_instance_id)` tới đúng campus của request.
- `kind = INITIAL_CONFIRMATION | TRANSFER`.
- `status = PENDING | APPLIED | DECLINED | EXPIRED | CANCELLED | SUPERSEDED`.
- Lưu `target_email` normalized, `token_hash`, `token_version`, `expires_at`, actor và timestamps.
- Không có `target_relation` và không có giá trị Primary contact.
- Generated guard + unique index bảo đảm mỗi instance chỉ có một identity change `PENDING`.

`visit_request_identity_change_events` giữ audit append-only theo `visit_instance_id`.

### 4.2 Guard và index

- Index `(operational_contact_user_id, visit_instance_id)` cho own-scope.
- Index Staff Leader queue theo `(campus_id, status, visit_request_id)`.
- Index identity theo `(visit_instance_id, status)`, `token_hash` unique và `expires_at` cho maintenance job.
- DB guard fail-closed không cho request vào approval state nếu còn active campus thiếu `operational_contact_user_id`.
- DB guard không cho campus approve/reject khi request còn `PENDING_CONTACT_CONFIRMATION`.
- DB guard không cho campus vào `BEFORE_VISIT` nếu thiếu Host/decision hợp lệ.
- Backend vẫn phải validate đầy đủ; trigger/constraint không thay thế authorization.

### 4.3 Xóa logic cũ

Xóa khỏi canonical SQL, entity, mapping, DTO, API, trigger, view, seed và test:

```text
visitor_user_id
contact_person_*
primary_contact_*
PrimaryContact / primaryContact / contactPoint
request-wide contact claim/transfer
WAITING_HO_APPROVAL / HO_APPROVED
HO request approve/reject handlers/routes
ASSIGNED lifecycle
```

Không tạo migration compatibility, backfill hoặc rollback dữ liệu cũ. Sửa canonical full SQL trực tiếp, cập nhật hash/parity test và recreate fresh database dev sau khi code/schema đã đồng bộ.

### 4.4 Seed và verify SQL

Seed tối thiểu phải có:

- single-campus self-contact, không invitation;
- multi-campus tất cả self-contact;
- mixed self-contact + external pending;
- tất cả contact đã confirm, chờ từng Staff Leader;
- một approved + một pending = `PARTIALLY_APPROVED`;
- approved + rejected, không còn pending = `APPROVED`;
- tất cả rejected = `REJECTED`;
- Staff Leader là Registrant nhưng vẫn bị gate theo sibling contact;
- contact cùng user phụ trách nhiều campus;
- expired/superseded/replayed invitation;
- HO monitor-only.

Verify SQL phải trả zero row cho trạng thái bất khả thi và xác nhận số bảng/hash canonical mong đợi.

## 5. Backend

### 5.1 Domain/Application

Ưu tiên tận dụng code hiện có, đổi phạm vi thay vì tạo tầng mới dư thừa:

- `VisitRequestV2CreateService`: match self-contact, tạo invitation per instance, aggregate gate và after-commit dispatch.
- Claim/transfer hiện tại: đổi tên và scope thành Operational contact, bắt buộc `visit_instance_id`.
- `VisitContactClaimService`/maintenance: xử lý accept, decline, resend, expiry, transfer và event per-campus.
- `VisitInstanceAccess`: trả capability độc lập cho Registrant, Operational contact, Staff Leader và Host.
- `VisitFormReadService`: Registrant thấy toàn request; Operational contact chỉ instance ACTIVE; pending contact không có quyền.
- `ApproveCampusInstance`/`RejectCampusInstance`: bỏ mọi HO branch, thêm global confirmation guard, campus scope, concurrency và aggregate recompute.
- Host resolver/validator: cho self-hosting, kiểm tra active/scope/schedule.
- Aggregate status service dùng một hàm duy nhất cho create, confirm, approve, reject, cancel, resubmit và edit.

Không phân quyền bằng email lưu trong form. Email chỉ dùng để bind invitation; quyền runtime đọc từ `operational_contact_user_id` và relation hiện tại trong DB.

### 5.2 API contract

Contract cần có hoặc chuyển sang instance scope:

```http
POST /api/v2/visit-requests
GET  /api/public/operational-contact-confirmations/{token}
POST /api/operational-contact-confirmations/{token}/accept
POST /api/operational-contact-confirmations/{token}/decline
POST /api/v2/visit-requests/{requestId}/instances/{instanceId}/operational-contact-confirmation/resend
PUT  /api/v2/visit-requests/{requestId}/instances/{instanceId}/operational-contact
POST /api/v2/visit-requests/{requestId}/instances/{instanceId}/operational-contact/transfer
POST /api/delegations/{requestId}/campuses/{instanceId}/approve
POST /api/delegations/{requestId}/campuses/{instanceId}/reject
```

Tên route cuối có thể giữ style hiện tại, nhưng mọi mutation contact/approval phải chứa và validate `visit_instance_id`; payload sibling campus bị từ chối.

Response detail/list trả:

- `confirmationSummary.total/confirmed/pending/declined/expired`;
- trạng thái xác nhận từng campus, không trả token/raw email ngoài quyền;
- `allowedActions` do backend tính;
- aggregate status và instance status riêng;
- `isReadOnlyMonitoring` cho HO.

Error code ổn định:

```text
CONTACT_CONFIRMATION_REQUIRED
OPERATIONAL_CONTACT_CONFIRMATION_NOT_FOUND
OPERATIONAL_CONTACT_CONFIRMATION_EXPIRED
OPERATIONAL_CONTACT_CONFIRMATION_SUPERSEDED
OPERATIONAL_CONTACT_EMAIL_MISMATCH
OPERATIONAL_CONTACT_ALREADY_CONFIRMED
OPERATIONAL_CONTACT_CONFIRMATION_RATE_LIMITED
OPERATIONAL_CONTACT_CHANGE_CONFLICT
STAFF_LEADER_NOT_AVAILABLE
CAMPUS_SCOPE_FORBIDDEN
VISIT_INSTANCE_NOT_IN_REQUEST
APPROVAL_ALREADY_DECIDED
HOST_SCHEDULE_CONFLICT
CONCURRENCY_CONFLICT
```

Public errors không tiết lộ email/account có tồn tại.

### 5.3 Consumer audit bắt buộc

Audit toàn bộ nơi đang đọc request-level owner/contact hoặc HO approval:

- list/search/detail/process views;
- pending edit, resubmit, safe edit, amendment, cancel;
- feedback, minutes, news, documents/files và history authorization;
- reports, PDF/export, dashboard/calendar;
- reminder và setup-progress recipient resolver;
- email action info/execute, recipient resolver và sent-email visibility;
- notification routing;
- audit/history projection;
- frontend route resolver và `allowedActions`.

Không dùng campus đầu tiên/campus nhỏ nhất làm đại diện cho request mixed. Scope phải áp dụng trước search, paging, count, sort và projection.

## 6. Authorization, filter và security

### 6.1 Enforcement order

Mọi endpoint protected thực hiện theo thứ tự:

```text
Authentication
-> active account / portal validity
-> effective role
-> relation (Registrant/Operational contact/Staff Leader/Host)
-> campus scope
-> request + instance association
-> confirmation gate
-> lifecycle/status
-> row version/idempotency
-> business action
```

- Cross-campus direct URL trả `403 CAMPUS_SCOPE_FORBIDDEN`.
- Instance không thuộc request trả `404` hoặc stable not-found theo convention, không leak object khác.
- Trước đủ xác nhận, approve/reject/get-host-candidates/assign-host/setup mutation trả `409 CONTACT_CONFIRMATION_REQUIRED`.
- Staff Leader là Registrant vẫn chỉ có creator actions; processing actions không xuất hiện trước gate.
- HO `allowedActions` chỉ read/monitor; API mutation luôn 403.

### 6.2 Token và dữ liệu nhạy cảm

- Token dùng CSPRNG, lưu hash, single-use, có expiry; không log raw token.
- Không log raw OTP, auth token, full confirmation URL hoặc full contact payload.
- Accept/decline yêu cầu authenticated session, CSRF protection và exact normalized email.
- GET landing không mutation và chỉ trả dữ liệu mask.
- HTML/email được sanitize/encode; redirect URL dùng allow-list.
- Authorization re-query DB, không lưu ownership dài hạn trong JWT.
- Audit actor, request, instance, old/new status và timestamp; không ghi raw secret.

### 6.3 Anti-spam và idempotency email

Tận dụng `IRateLimitService`, middleware hiện có, dispatcher và `email_send_idempotency`; không tạo bảng draft mới.

Giới hạn cấu hình tập trung, mặc định đề xuất:

- cooldown resend: 60 giây;
- tối đa 5 resend cho một invitation trong 24 giờ;
- thêm bucket theo normalized email + IP/device/session để chặn đổi request/instance nhằm né limit;
- trả `429`, `Retry-After` và error code ổn định;
- CAPTCHA chỉ kích hoạt theo ngưỡng/risk ở endpoint public nếu hạ tầng hiện có hỗ trợ.

Dedupe key phải gắn đúng event/version, ví dụ:

```text
OP_CONTACT_CONFIRM:{identityChangeId}:{tokenVersion}
APPROVAL_READY:{requestId}:{visitInstanceId}:{gateRevision}
OP_CONTACT_TRANSFER:{identityChangeId}:{tokenVersion}
```

- Dispatch chỉ sau commit.
- Retry transaction/submission không gửi trùng.
- Resend supersede token cũ trước khi gửi token mới.
- Một địa chỉ phụ trách nhiều campus vẫn có identity row per-campus; dispatcher có thể gộp các mail phát sinh cùng submit cho cùng normalized email nếu renderer hiện có hỗ trợ mà không làm thay đổi semantics.
- Mail confirm chỉ chứa dữ liệu tối thiểu của đúng campus, không lộ sibling campus hoặc dữ liệu nhạy cảm.
- Khi gate mở, gửi riêng notification duyệt tới Staff Leader của từng campus; không gửi mail yêu cầu HO duyệt.

## 7. Frontend

### 7.1 Form create/edit

- Xóa section Primary contact và `contactPoint`.
- `CampusVisitCard` giữ Operational contact với email bắt buộc.
- Chỉ giữ nút “Dùng thông tin người đăng ký”.
- Khi chọn nút này, hiển thị “Trùng người đăng ký — không cần xác nhận email”; backend vẫn xác minh lại, không tin cờ từ frontend.
- Xóa “Dùng đầu mối chính” và `noteToFptu`.
- Schema, form defaults, mapper, validation và i18n VI/EN phải đồng bộ contract mới.

### 7.2 Sau submit và trang người tạo

- Hiển thị tiến độ `Đã xác nhận X/Y đầu mối`.
- Từng campus có badge: `Tự xác nhận`, `Đã gửi email`, `Đã xác nhận`, `Từ chối`, `Hết hạn`.
- Cho resend/thay contact/cancel đúng `allowedActions`.
- Có cooldown countdown, loading, success, empty và error state.
- Nếu tất cả campus tự khớp Registrant, chuyển thẳng sang “Chờ Staff Leader từng cơ sở duyệt”.
- Staff/Staff Leader là người tạo vẫn thấy trang own-request nhưng không thấy nút xử lý theo vai trò Staff Leader trước gate.

### 7.3 Staff Leader và HO

- Staff Leader queue chỉ hiển thị instance campus mình sau global gate.
- Multi-campus hiển thị một row/context cho campus hiện tại; không lộ sibling detail.
- Approve modal bắt buộc chọn Host, cho chọn chính mình và hiển thị conflict lỗi từ backend.
- Reject bắt buộc lý do.
- Không suy luận quyền từ role/status ở frontend; render từ `allowedActions`.
- HO page chỉ monitor: aggregate + per-campus status + confirmation progress; xóa approve/reject/assign/cancel controls.
- Direct route trái quyền hiển thị 403/404/409 đúng contract, không render dữ liệu cũ trước khi fail.

## 8. Test plan

### 8.1 Unit test

- Normalize email và self-match chỉ theo verified identity.
- Self-contact gán cùng user, không tạo identity change/email.
- Name/phone trùng nhưng email khác vẫn phải confirm.
- Aggregate confirmation/approval cho mọi tổ hợp pending/approved/rejected/cancelled.
- Capability union khi một user có nhiều relation.
- Staff Leader creator không bypass gate.
- HO không có mutation action.
- Resend cooldown/max count/supersede.
- Token expiry, replay, decline và idempotent accept.
- Host eligibility/self-host/schedule conflict.
- Recipient resolver không gửi sibling data và không gửi HO approval mail.

### 8.2 Integration/API test

- Fresh canonical import MySQL thành công.
- Submit fail atomic khi một campus không có Staff Leader ACTIVE.
- Single-campus self-contact: zero confirmation email, Staff Leader thấy sau submit.
- Multi-campus all-self: zero confirmation email, mọi campus mở queue cùng lúc.
- Multi-campus mixed: chưa xác nhận đủ thì không Staff Leader nào thấy queue.
- Xác nhận cuối: tất cả đúng Staff Leader thấy đúng campus; notification đúng một lần.
- Contact cùng một account ở nhiều campus được link đúng từng instance.
- Existing internal account khác Registrant vẫn phải accept.
- Pending contact gọi detail/mutation bị chặn.
- Staff Leader campus A không đọc/duyệt campus B qua URL trực tiếp.
- HO list/detail read-only; approve/reject/assign trả 403.
- Approve HN + HCM pending -> `PARTIALLY_APPROVED`.
- Approved + rejected, hết pending -> `APPROVED`.
- Tất cả rejected -> `REJECTED`.
- Hai Staff Leader quyết định đồng thời không làm sai aggregate.
- Hai confirmation cuối đồng thời chỉ mở gate/dispatch một lần.
- Resend cũ bị supersede; old link fail; 429 có `Retry-After`.
- Retry submit/confirm/approve cùng idempotency key không tạo duplicate.
- Đổi contact trước decision đóng lại gate; transfer sau decision giữ owner cũ đến accept.
- Search/list/count/filter không leak hidden campus.
- Report/export/email/reminder chỉ dùng instance/contact đúng quyền.

### 8.3 Frontend/component/E2E

- Form không còn Primary contact, `contactPoint`, “Dùng đầu mối chính”, `noteToFptu`.
- Nút dùng Registrant điền đúng và hiển thị skip-confirm state.
- Progress X/Y và mọi badge/error VI/EN đúng.
- Cooldown resend hoạt động và reload không reset giả.
- Staff Leader queue bị ẩn trước gate, mở sau confirmation cuối.
- Staff Leader creator không thấy action sớm.
- HO không có action ở list/detail/modal và deep link.
- Approve + self-host/reject/aggregate multi-campus chạy E2E.
- Mobile/responsive, loading/empty/error và accessibility cơ bản không regression.

### 8.4 Security test

- Token chỉ lưu hash; raw token/OTP không xuất hiện trong log.
- Account/email enumeration bị chặn.
- CSRF, token tampering, replay, expired, superseded, wrong-email đều fail.
- IDOR request/instance/campus bị chặn.
- Rate-limit theo invitation/email/IP/session không né được bằng đổi instance liên tục.
- Email HTML/script injection và malicious redirect bị chặn.
- Unauthorized export/file/email-history không lộ data.

## 9. Thứ tự triển khai

### Phase 0 — Preflight và audit map

- Chốt branch/HEAD/WIP, build/test baseline và canonical SQL hash.
- Lập danh sách mọi reference mô hình cũ, HO approval, `ASSIGNED`, owner/contact request-level và consumer downstream.
- Đọc code/schema/test tại HEAD làm bằng chứng trước khi sửa; không dựa riêng tài liệu legacy đang mâu thuẫn.

### Phase 1 — Khóa rule và contract

- Cập nhật canonical business rules, permission matrix/rules, UC list/notes, project overview, system doc, handoff và API contract.
- UC-18 không còn “Approve Cross-Campus”; chuyển thành monitor/read-only hoặc đánh dấu superseded theo convention tài liệu.
- UC-22 áp dụng Staff Leader per-campus cho cả single và multi-campus.

### Phase 2 — Canonical database cuối

- Sửa full SQL trực tiếp; xóa schema cũ; thêm relation, statuses, constraint, trigger, index, view và seed mới.
- Cập nhật EF entity/configuration/DbContext và canonical hash/parity test.
- Chưa recreate DB dùng chung khi code chưa compile với schema mới.

### Phase 3 — Create + confirmation gate

- Sửa DTO/validator/create service.
- Triển khai self-match, per-campus identity state, accept/decline/resend/expiry/transfer, aggregate recompute, concurrency và audit.
- Nối email dispatcher, idempotency và rate limit.

### Phase 4 — Authorization + approval independent

- Sửa access service, filters/views, list/detail/search và `allowedActions`.
- Xóa HO mutation.
- Sửa approve/reject per-campus, approve + host atomic, self-host và schedule conflict.

### Phase 5 — Consumer audit

- Chuyển edit/resubmit/amendment/cancel/history/files/report/export/reminder/email/notification sang instance scope và relation mới.
- Chứng minh không còn first-campus projection hoặc hidden-campus side channel.

### Phase 6 — Frontend cutover

- Sửa form/schema/API/hooks/detail/list/queue/modals/i18n.
- Xóa component và state cũ; không giữ route/contract compatibility.

### Phase 7 — Test và fresh recreate dev DB

- Chạy unit, architecture, integration trên disposable fresh DB và frontend tests trước.
- Khi toàn bộ code/schema đồng bộ mới drop/recreate database dev từ canonical SQL mới.
- Chạy real-stack E2E và verify SQL trên DB mới.

### Phase 8 — Cleanup/closure

- Xóa code/dead route/trigger/view/test/doc cũ.
- Re-run repository audit, canonical hash, build và full gates.
- Chỉ báo hoàn thành khi không còn debt/compatibility path được giấu bằng comment hoặc feature flag.

## 10. Nhóm file dự kiến bị ảnh hưởng

| Khu vực | Thay đổi |
|---|---|
| Canonical full SQL + verify/hash | Schema/status/constraint/trigger/view/seed hard cutover |
| `VisitRequest`, `VisitRequestCampus`, identity entities | Xóa owner cũ; thêm Operational contact user relation và instance-scoped identity |
| `VisitFormV2Dtos` + validators | Xóa Primary contact; Operational contact email required; remove `noteToFptu` |
| `VisitRequestV2CreateService` | Self-match, invitation per campus, global gate, idempotent dispatch |
| Claim/transfer services/handlers | Rename và chuyển hoàn toàn sang Operational contact + `visit_instance_id` |
| `VisitFormReadService`, `VisitInstanceAccess` | Capability + scope mới |
| Approve/Reject Campus handlers | Staff Leader-only; confirmation guard; approve + host atomic |
| List/search/detail views/handlers | Staff Leader own-campus after gate; HO monitor-only |
| Email dispatcher/resolvers/jobs | Confirm/resend/expiry/dedupe/rate-limit/recipient per-campus |
| Report/export/reminder/file/history consumers | Instance scope, không dùng request-wide owner |
| `VisitRequestFormV2`, `CampusVisitCard` | Xóa Primary contact; dùng Registrant; confirmation UX |
| Staff Leader/HO pages | Queue per-campus; HO read-only; host required on approve |
| Backend/frontend/integration/E2E tests | Bao phủ state, permission, security, concurrency và anti-spam |
| Business/permission/UC/handoff docs | Xóa mô tả HO approval và workflow contact cũ |

Tên file cụ thể phải lấy từ repository HEAD khi triển khai; không tạo abstraction mới nếu service/handler hiện tại có thể sửa trực tiếp.

## 11. Gate hoàn thành

Chỉ coi hoàn tất khi:

1. Fresh canonical SQL import và verify xanh trên MySQL đúng version dự án.
2. Không còn runtime/schema/API/frontend sử dụng mô hình Primary contact hoặc request-wide contact claim/transfer.
3. Không còn HO approve/reject/assign-host route, button, `allowedAction`, policy hoặc test kỳ vọng thành công.
4. Staff Leader chỉ xử lý own-campus và chỉ sau global confirmation gate.
5. Self-contact không tạo identity invitation và không gửi email.
6. Non-self contact không có quyền trước accept.
7. Aggregate status đúng với mọi tổ hợp campus và concurrency.
8. Email confirm/approval notification after-commit, idempotent và rate-limited.
9. Không leak sibling campus trong list/search/detail/report/export/email/file.
10. Backend build, Unit, Architecture, Integration, frontend typecheck/lint/build/unit/component và real-stack E2E đều xanh.
11. Canonical hash/parity, DB trigger/verify và repository audit đều xanh.
12. Tài liệu active không còn mô tả trái ngược về HO approval, `ASSIGNED` hoặc contact workflow.

## 12. Ngoài phạm vi

- Không backup/backfill dữ liệu dev cũ.
- Không compatibility V1/V2 hoặc feature flag cho mô hình contact cũ.
- Không permission engine động mới; giữ fixed policy theo role/sub-role/relation/campus/status.
- Không đổi architecture, API unrelated hoặc database module ngoài các consumer thực sự phụ thuộc workflow này.
