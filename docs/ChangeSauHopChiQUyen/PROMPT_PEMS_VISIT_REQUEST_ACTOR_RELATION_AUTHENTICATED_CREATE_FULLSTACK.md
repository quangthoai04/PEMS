# PROMPT — CẬP NHẬT TOÀN DIỆN LUỒNG TẠO ĐOÀN KHÁCH, HAI QUAN HỆ TÀI KHOẢN, DUYỆT THEO CAMPUS VÀ DANH SÁCH THEO ROLE

## 1. Vai trò của AI Agent

Bạn là Senior Full-stack Engineer chịu trách nhiệm triển khai hoàn chỉnh thay đổi này trong dự án PEMS, đồng thời đóng vai trò:

- Senior ASP.NET Core .NET 8 / Clean Architecture / MediatR Engineer.
- Senior React Vite TypeScript / React Hook Form / Zod Engineer.
- MySQL 8 database-first Engineer.
- Security, Authorization và Privacy Reviewer.
- QA Engineer phụ trách Unit, Integration, Architecture và Playwright/E2E test.
- Enterprise Dashboard UI/UX Reviewer.

Đây là yêu cầu **thực thi code full-stack**, không chỉ phân tích hoặc viết kế hoạch. Trước khi sửa phải audit source thật; sau đó triển khai, build và test đến khi hoàn tất. Không dừng ở báo cáo hiện trạng nếu không gặp blocker thực sự.

---

## 2. Bối cảnh dự án

PEMS là hệ thống quản lý đối tác và tiếp đón đoàn khách của FPT University.

Tech stack:

- Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, FluentValidation, EF Core/Pomelo MySQL.
- Frontend: React 19, Vite, TypeScript, Tailwind CSS, React Hook Form, Zod, i18next.
- Database: MySQL 8, database-first/manual SQL; **không tự ý dùng EF Migration nếu repository hiện quản lý schema bằng SQL thủ công**.
- Authorization: fixed policy theo `role_code`, `sub_role/effectiveRole`, campus scope, ownership, host/participant relation và status. Không có dynamic permission runtime.

Các role liên quan:

- `VISITOR`.
- `STAFF + STAFF` — IC Staff thường.
- `STAFF + LEADER` — Staff Leader.

Các role không được tạo đoàn khách trong scope này, trừ khi source hiện có business rule mới hơn và có bằng chứng rõ ràng:

- ADMIN, HO, DEPARTMENT, DEPARTMENT LEADER, STUDENT.

---

## 3. Tài liệu và source bắt buộc đọc trước

Trước khi sửa, hãy search và đọc source hiện tại. Không sửa theo suy đoán và không bịa file/API/table/field.

Đối chiếu tối thiểu:

1. `PROJECT_KNOWLEDGE.md` mới nhất, nhưng phải nhớ tài liệu này có base commit; source hiện tại mới hơn luôn phải được audit lại.
2. `PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md`.
3. `CLEAN_ARCHITECTURE.md`.
4. `PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md`.
5. `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`.
6. `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`.
7. `VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md`.
8. `PERMISSION_RULES.md`, `PERMISSION_MATRIX.md`, `USE_CASE_LIST.md` và `USE_CASE_NOTES_HO_VIEW_SINGLE_READONLY.md`.
9. `PEMS_UI_DESIGN_SYSTEM_PROMPT.md`.
10. `PEMS_v10_NEW_FINAL_SQL_TABLE_FIELD_DICTIONARY_MATCHED.docx`.
11. SQL fresh-create mới nhất thực sự đang dùng trong repository/database.
12. Source backend/frontend/test hiện tại.

Phải search và đọc toàn bộ chuỗi liên quan, gồm nhưng không giới hạn:

- `VisitRequest`, `VisitRequestCampus`, `User`, participant/host entity.
- `DelegationsController`, `VisitRequestsController` và route thật đang được UI gọi.
- `InitiateVisitRequest`, `ResendVisitRequestOtp`, `VerifyAndCreateVisitRequest`.
- `CreateGuestDelegation` và các handler authenticated-create/legacy tương đương.
- `ApproveCampusInstance`, `RejectCampusInstance`, host candidate, aggregate status service.
- `ViewGuestDelegationList`, `ViewGuestDelegationDetails`, `GetSubmittedVisitRequestFormDetail`.
- `GetEditableVisitRequestDetail`, `UpdatePendingVisitRequest`, `ResubmitRejectedVisitRequest`, cancel, feedback eligibility và pending feedback query.
- Visitor account provision/SSO auto-provision service.
- Notification/email/audit/idempotency/duplicate services.
- `VisitingFormPopup.tsx`, `CreateVisitRequest.tsx`, `VisitRequestManagement.tsx`, `VisitorVisitDetailPage.tsx`.
- Form schema, `useVisitRequestForm`, draft storage, API services, adapters, types, routes và auth context.
- `DashboardLayout`, `NotificationBell`, page header/action layout.
- Existing unit/integration/architecture/Playwright tests.

Kiểm tra cả mock, stub, `NotImplementedException`, endpoint legacy không còn UI gọi, dead code và route/component trùng chức năng.

### Thứ tự ưu tiên khi nguồn mâu thuẫn

1. SQL fresh-create mới nhất và patch SQL đã thực thi.
2. Source code/runtime behavior mới nhất.
3. SQL dictionary mới nhất.
4. Business rules/canonical docs mới nhất.
5. Các tài liệu cũ chỉ dùng để tìm lịch sử, không được dùng để phục hồi flow HO approval hoặc status legacy.

Riêng yêu cầu trong prompt này là business rule mới và **cố ý thay đổi** rule cũ “submit không bao giờ approve/gán host” theo ngoại lệ rất hẹp: authenticated IC Staff/Staff Leader có thể xử lý trực tiếp **campus của chính mình** trong lúc tạo đơn. Visitor/public và campus khác vẫn phải chờ Staff Leader campus duyệt.

---

## 4. Hiện trạng UI đã quan sát từ ảnh tham chiếu

Phải xác minh lại bằng source và browser, nhưng lấy các vấn đề sau làm acceptance baseline:

1. Màn Staff thường:
   - Đã có nút cam `+ Tạo đoàn khách` ở góc phải phần tiêu đề.
   - Chuông thông báo đang đè/chồng lên vùng nút ở góc trên bên phải.
   - Hiện có tab `Đơn phụ trách` và `Lời mời tham dự`.
   - Cần thêm tab `Đơn tôi đăng ký`.

2. Màn Staff Leader:
   - Chưa thấy nút `Tạo đoàn khách`.
   - Hiện chỉ có một nhóm danh sách `Đơn phụ trách`, chưa tách rõ campus-review, host, invitation và registered views.

3. Màn Visitor:
   - Tiêu đề đang là `Đơn của tôi`.
   - Chưa thấy nút `Tạo đoàn khách` khi đã đăng nhập.
   - Chưa có hai tab `Tôi là đầu mối` và `Tôi là người đăng ký`.

Không được fix overlap bằng cách tăng `z-index` tùy tiện hoặc chèn margin magic chỉ hợp một màn hình. Hãy đặt page title, create action và notification area trong layout/flex/grid có vùng dành riêng, responsive ổn định.

---

## 5. Mục tiêu tổng thể

Triển khai thống nhất các nội dung sau:

1. Một request có hai quan hệ tài khoản rõ ràng:
   - Người đăng ký/submitter: chỉ theo dõi sau khi gửi.
   - Đầu mối liên hệ/contact owner: chủ sở hữu thao tác request.
2. Public chưa đăng nhập và người đã đăng nhập đều có thể tạo đoàn khách theo đúng quyền.
3. Form authenticated dùng chung form core với public form, không copy nguyên component; thông tin người đăng ký được prefill từ account.
4. Visitor tạo đơn luôn chờ Staff Leader từng campus duyệt.
5. Staff thường có thể tại campus của mình:
   - Tự nhận làm host và xử lý trực tiếp; hoặc
   - Gửi Staff Leader xử lý.
6. Staff Leader có thể tại campus của mình:
   - Tự làm host;
   - Gán host khác cùng campus ngay;
   - Hoặc để xử lý sau.
7. Campus khác luôn chờ Staff Leader của campus đó duyệt/gán host.
8. Single/multi-campus aggregate status phải đúng.
9. Tách server-side list/tab theo quan hệ và role; không fetch toàn bộ rồi lọc bảo mật ở frontend.
10. Fix vị trí nút tạo không bị notification bell che và bổ sung nút còn thiếu cho Staff Leader/Visitor.
11. Cập nhật validation, authorization, email/notification/audit, SQL/entity/EF mapping và test đầy đủ.

---

## 6. Mô hình nghiệp vụ bắt buộc

### 6.1. Hai quan hệ trên `visit_requests`

Giữ semantic tối thiểu thay đổi:

```text
visitor_user_id = tài khoản đầu mối liên hệ/contact owner; có quyền request-level mutation theo status
registrant_user_id = tài khoản người đăng ký/submitter; chỉ xem và theo dõi
created_by = registrant_user_id/current authenticated actor đã gửi form
```

`visitor_user_id` phải trỏ tới role kỹ thuật `VISITOR`.

`registrant_user_id` có thể trỏ tới:

- VISITOR.
- STAFF + STAFF.
- STAFF + LEADER.

Không tạo role mới như `REGISTRANT_VIEWER`; relation trên request mới là nguồn quyền.

Không tự động thêm người đăng ký hoặc đầu mối vào `visit_guest_members`. Nếu UI hiện có checkbox/nghiệp vụ “người này tham gia đoàn”, chỉ copy vào guest list khi người dùng chọn rõ ràng.

### 6.2. Quy tắc cùng một người hay khác người

Không áp dụng `registrant_user_id <> visitor_user_id` cho mọi trường hợp vì sẽ mâu thuẫn với visitor tự đăng ký đoàn mình dẫn đầu.

Quy tắc đúng:

| Registrant | Contact owner có thể cùng tài khoản? |
|---|---:|
| Public/Visitor | Có |
| Authenticated VISITOR | Có |
| Staff thường | Không |
| Staff Leader | Không |

UI Visitor/public có thể có lựa chọn `Tôi cũng là đầu mối liên hệ`. Staff/Staff Leader không được thấy lựa chọn này.

Với Staff/Staff Leader, contact email bắt buộc là một người khác và phải là/được tạo thành tài khoản VISITOR.

### 6.3. Quyền theo relation

Contact owner có thể, nhưng luôn phụ thuộc status/time/business rule hiện hành:

- Xem form đã gửi và public-safe progress.
- Sửa request đang chờ khi đủ điều kiện.
- Gửi lại request bị từ chối khi đủ điều kiện.
- Hủy request/campus theo rule 24 giờ và lifecycle.
- Gửi feedback khi đủ eligibility.
- Nhận notification yêu cầu thao tác và pending-feedback notification.

Registrant viewer chỉ được:

- Xem form snapshot đã gửi.
- Xem request code, aggregate status.
- Xem public-safe progress từng campus.
- Xem public-safe rejection/cancellation reason.

Registrant viewer không được:

- Edit, resubmit, cancel, submit feedback.
- Nhận pending-feedback/action-required notification dành cho contact owner.
- Xem participant nội bộ, logistics, minutes, preparation note, host workflow hoặc contribution nội bộ chỉ nhờ relation registrant.

Nếu một internal registrant đồng thời có relation khác như HOST, PARTICIPANT hoặc CAMPUS_REVIEWER, họ chỉ có action phát sinh từ relation đó. Relation registrant vẫn read-only.

Không tạo một hàm chung `CanViewEverything`. Xây access policy tập trung có relation/action rõ ràng, ví dụ:

```text
CONTACT_OWNER
REGISTRANT_VIEWER
CAMPUS_REVIEWER
HOST
PARTICIPANT
NONE
```

Các mutation request-level như edit/resubmit/contact-owner cancel/visitor feedback phải tiếp tục kiểm tra `visitor_user_id`, không mở cho `registrant_user_id`.

---

## 7. Account provisioning và email conflict

### 7.1. Public chưa đăng nhập

Flow public vẫn dùng OTP V2 hiện tại:

1. Validate full form và email conflict ở initiate.
2. OTP gửi tới email người đăng ký.
3. Khi verify thành công, trong cùng transaction:
   - Normalize cả registrant email và contact email.
   - Ensure/link registrant VISITOR account.
   - Ensure/link contact-owner VISITOR account.
   - Nếu hai normalized email giống nhau, reuse một account.
   - Tạo request/campus/guest/agenda/participant cần thiết.
   - Ghi notification/audit/idempotency state.
4. Commit rồi mới gửi external email theo convention hiện tại.

Không tạo contact account ngay lúc initiate để tránh sinh account rác trước khi OTP registrant được xác minh.

### 7.2. Authenticated VISITOR

- `registrant_user_id` lấy từ current authenticated user.
- Không yêu cầu OTP lại nếu session/SSO hiện tại đã xác thực đúng theo auth policy.
- Cho phép contact là chính current Visitor hoặc một Visitor khác.
- Nếu contact khác, provision/link contact account trong transaction.
- Visitor không được gửi mode tự duyệt/tự host/gán host.

### 7.3. Authenticated Staff/Staff Leader

- `registrant_user_id = currentUserId`.
- Không yêu cầu OTP.
- Contact bắt buộc khác current user.
- Contact email phải là Visitor account hoặc email chưa tồn tại để backend tạo Visitor account.
- Không cho internal user trở thành contact owner.

### 7.4. Conflict rules bắt buộc

Backend normalize email bằng một helper duy nhất, ít nhất trim + case-insensitive comparison.

| Trạng thái email contact | Xử lý |
|---|---|
| Chưa có user | Tạo VISITOR account |
| ACTIVE VISITOR | Link user hiện có |
| INACTIVE VISITOR | Field error |
| LOCKED VISITOR | Field error |
| Email đang thuộc bất kỳ internal role nào | Field error, không đổi role |

Tương tự, public registrant email không được chiếm/internal-account rồi bị repurpose thành VISITOR; hãy trả lỗi phù hợp hoặc hướng dẫn đăng nhập internal portal theo behavior đã chốt trong source.

Không overwrite `users.full_name`, phone hoặc profile của một VISITOR đã tồn tại chỉ vì người khác nhập contact snapshot trên form. Request giữ snapshot riêng. Chỉ dùng snapshot để khởi tạo user mới; account hiện có chỉ được link.

Contact account khác registrant chưa được OTP xác minh bởi chính contact, vì vậy không được set `users.email_verified_at` như thể contact đã verify. Google SSO/portal login đúng email sẽ xác thực account theo auth flow hiện tại.

Nếu cùng một tài khoản:

- Chỉ tạo/link một user.
- Không gửi email/notification trùng.
- Relation ưu tiên là `CONTACT_OWNER`.

Mọi account provision + request creation phải nằm trong transaction. Unique-email race phải được bắt và map thành kết quả xác định, không để 500 mơ hồ hoặc partial account.

---

## 8. Form tạo đoàn khách dùng chung cho public và authenticated

### 8.1. Không nhân đôi form

Audit `VisitingFormPopup`, `CreateVisitRequest` và các form hiện có. Tách/reuse một form core chung nếu cần, ví dụ theo hướng:

```text
VisitRequestFormCore
  + mode: PUBLIC | AUTHENTICATED_VISITOR | STAFF | STAFF_LEADER
  + registrant source
  + campus processing options
  + submit adapter
```

Tên thật phải theo convention repository; không bắt buộc dùng đúng placeholder trên.

Public và authenticated phải dùng cùng:

- Form fields và section order.
- Zod/business validation chung.
- Guest/support/agenda/campus slot logic.
- i18n labels và error mapping.
- Responsive design.
- Draft sanitation/close behavior nếu feature draft đã tồn tại.

Khác biệt chỉ nằm ở identity, OTP và campus processing mode.

### 8.2. Registrant section khi đã đăng nhập

- Prefill từ auth/current-user profile.
- Email và account identity không được lấy từ payload tùy ý.
- Full name/email nên read-only để chống impersonation.
- Phone, organization, job title, nationality: prefill nếu có; cho bổ sung snapshot nếu business form yêu cầu nhưng không âm thầm cập nhật user profile.
- Nếu profile thiếu field bắt buộc, UI hiển thị rõ field cần bổ sung.

### 8.3. Contact section

- Luôn required.
- Visitor/public có lựa chọn “Tôi cũng là đầu mối liên hệ”.
- Staff/Staff Leader bắt buộc nhập người khác; không hiển thị lựa chọn dùng chính mình.
- Không lookup rồi auto-reveal name/phone của account có sẵn chỉ dựa trên email.
- Map server error đúng vào field contact email.

### 8.4. OTP và authenticated submit

- Public: giữ initiate → OTP/recovery/human verification → verify-and-create.
- Authenticated: submit trực tiếp bằng JWT/session qua authenticated command/endpoint phù hợp; không fake OTP và không dùng public session token.
- Nếu endpoint authenticated chưa tồn tại hoặc `CreateGuestDelegation` là stub/legacy, hãy audit rồi chọn một hướng duy nhất: hoàn thiện/reuse an toàn hoặc tạo endpoint resource-style mới. Báo cáo rõ quyết định và tránh để hai flow cùng sống nhưng lệch validation.

### 8.5. Draft

Nếu draft auto-save đã được triển khai:

- Giữ nguyên rule không lưu OTP, session token, File/binary hoặc auth token.
- Draft authenticated phải được namespace theo current user/flow để tài khoản khác trên cùng thiết bị không nhìn thấy draft của nhau.
- Restore không được tự gia hạn TTL nếu người dùng chưa chỉnh sửa.
- Submit thành công phải cancel pending auto-save rồi clear draft.

Không được làm regression OTP V2, human verification, resend quota, `submission_id`, business fingerprint/idempotency và duplicate detection đã có trong code mới nhất. Authenticated create phải có idempotency tương đương; hãy reuse service/canonicalization hiện có thay vì tạo thuật toán duplicate thứ hai.

---

## 9. Campus submission mode và state transition

Frontend/API contract cần biểu diễn rõ lựa chọn theo từng campus, theo semantic tương đương:

```text
SEND_FOR_REVIEW
SELF_HOST
ASSIGN_HOST
```

Tên enum/code thật có thể thay đổi để phù hợp source, nhưng không dùng boolean mơ hồ.

### 9.1. Visitor/public

Mọi campus:

```text
mode = SEND_FOR_REVIEW
visit_request_campuses.status = WAITING_REQUEST_APPROVAL
coordinator = ACTIVE Staff Leader của campus
```

Backend phải reject payload Visitor cố gửi `SELF_HOST`, `ASSIGN_HOST`, `hostUserId`, `decidedBy` hoặc actor metadata.

### 9.2. Staff thường

Tại `primary_campus_id` của chính Staff và chỉ khi campus đó được chọn:

- `SELF_HOST`: xử lý trực tiếp campus, current user là host.
- `SEND_FOR_REVIEW`: để Staff Leader campus duyệt/gán host.

Staff thường:

- Không có `ASSIGN_HOST` cho người khác.
- Không được direct-process campus khác.
- Không được dùng self-host mode để approve một request đã tồn tại của Visitor; ngoại lệ chỉ áp dụng trong transaction tạo request do chính Staff đó đăng ký.
- Phải là ACTIVE `STAFF + STAFF`, thuộc IC department đúng campus.

### 9.3. Staff Leader

Tại campus của chính Leader:

- `SELF_HOST`: Leader tự làm host và xử lý ngay.
- `ASSIGN_HOST`: Leader chọn ACTIVE IC Staff/Staff Leader hợp lệ cùng campus và xử lý ngay.
- `SEND_FOR_REVIEW`: để trạng thái chờ, Leader xử lý sau bằng flow approve chuẩn.

Tại campus khác: luôn `SEND_FOR_REVIEW` tới Staff Leader của campus đó.

### 9.4. Ghi dữ liệu direct processing

Khi direct-process own campus, trong cùng transaction phải mirror semantics an toàn của `ApproveCampusInstanceCommandHandler` hiện tại:

- Campus instance chuyển trạng thái đã duyệt/có host theo enum hiện hành, dự kiến `ASSIGNED`.
- `current_host_user_id` set đúng host.
- `host_assigned_by`, `host_assigned_at` set đúng actor/time.
- `decided_by`, `decided_at`, `decision_actor_role`, `decision_source` set đầy đủ.
- Coordinator vẫn là Staff Leader phù hợp để campus có người theo dõi.
- Tạo/update official host participant đúng semantics hiện tại; không tự bịa participant status.
- Ghi audit.
- Notification host/Leader/contact/registrant đúng loại và chống trùng.

Host conflict policy giữ như flow hiện tại: kiểm tra và cảnh báo non-blocking nếu canonical rule vẫn cho phép, nhưng phải yêu cầu người dùng xác nhận rõ trước direct assignment.

Do hệ thống hiện không hỗ trợ transfer host, UI phải cảnh báo trước khi xác nhận self-host/assign-host rằng host chính thức không thể đổi theo flow hiện tại. Không tự triển khai transfer host ngoài scope.

### 9.5. Aggregate status ban đầu

Không thêm request status mới.

| Kết quả campus ban đầu | `visit_requests.status` |
|---|---|
| Tất cả `WAITING_REQUEST_APPROVAL` | `PENDING_APPROVAL` |
| Single own-campus direct processed | `APPROVED` |
| Có campus direct processed và còn campus chờ | `PARTIALLY_APPROVED` |
| Tất cả campus đã processed hợp lệ | `APPROVED` |

Phải gọi `VisitRequestAggregateStatusService` hoặc single source hiện có và mirror chính xác SQL aggregate trigger. Không tự viết công thức thứ ba trong controller/frontend.

---

## 10. Danh sách và tab theo role

Phân trang, search, filter, count và authorization phải xử lý server-side. Khi đổi tab phải reset page về 1 và không giữ stale actions/count của tab trước.

DTO row nên trả relation/view context, read-only state và `AllowedActions` do backend tính; frontend không tự suy quyền chỉ từ role/status.

### 10.1. Visitor

Thêm hai tab:

1. `Tôi là đầu mối`

```sql
vr.visitor_user_id = @CurrentUserId
```

- Hiển thị owner actions theo status.
- Detail dùng submitted form/public-safe process, không mở internal logistics/minutes/participant chỉ vì owner.

2. `Tôi là người đăng ký`

```sql
vr.registrant_user_id = @CurrentUserId
AND (vr.visitor_user_id IS NULL OR vr.visitor_user_id <> @CurrentUserId)
```

- Read-only tuyệt đối theo relation registrant.
- Không render edit/resubmit/cancel/feedback action.
- Direct mutation API vẫn phải trả 403.

Nếu Visitor vừa là registrant vừa là contact owner, request chỉ xuất hiện ở `Tôi là đầu mối`, không lặp ở tab registrant.

### 10.2. Staff thường

Giữ và hoàn thiện:

- `Đơn phụ trách`: current official host/host-scope records.
- `Lời mời tham dự`: participant invitation hiện tại.
- Thêm `Đơn tôi đăng ký`: `vr.registrant_user_id = currentUserId`, hiển thị submitted-form/progress read-only theo relation registrant.

Nếu Staff vừa đăng ký vừa self-host, request có thể xuất hiện ở `Đơn phụ trách` cho host actions và `Đơn tôi đăng ký` cho tracking context. Trong registered tab không hiển thị owner actions; có thể có badge `Đồng thời là host` và link tới host context nếu kiến trúc route hỗ trợ.

Không cấp full campus list cho Staff thường.

### 10.3. Staff Leader

Tổ chức tab rõ ràng:

- `Yêu cầu tại cơ sở`: toàn bộ campus instance thuộc `primary_campus_id`, có subfilter `Cần xử lý / Đã xử lý / Đang diễn ra / Hoàn tất / Từ chối-Hủy` hoặc filter hiện hành tương đương.
- `Tôi là host`.
- `Lời mời tham dự` nếu existing participant flow áp dụng cho Leader.
- `Đơn tôi đăng ký`: read-only theo registrant relation.

Không làm mất flow approve/reject/assign-host hiện có của `Yêu cầu tại cơ sở`.

### 10.4. Detail và public-safe progress

Registrant viewer chỉ thấy tối thiểu:

- Form snapshot đã gửi.
- Request code và aggregate status.
- Campus name/time.
- Public-safe campus progress mapping.
- Public-safe reject/cancel reason.

Không trả internal preparation note, private participant identity, logistics, minutes, action token hoặc internal workflow metadata trong registrant-view endpoint/DTO.

---

## 11. Nút “Tạo đoàn khách” và layout

### 11.1. Visibility

Nút `+ Tạo đoàn khách` phải xuất hiện nhất quán cho:

- Authenticated VISITOR.
- Staff thường.
- Staff Leader.

Không xuất hiện cho role khác ngoài scope.

Nếu button/route đã có nhưng bị ẩn sai policy, sửa policy/route. Nếu chỉ Staff có handler còn Leader/Visitor thiếu, tái sử dụng cùng action/component chứ không copy page riêng.

### 11.2. Fix overlap với NotificationBell

Ảnh Staff cho thấy notification bell chồng lên nút tạo. Sửa bằng layout bền vững:

- Page header là flex/grid trong content flow.
- Nhóm title/breadcrumb và nhóm action có khoảng dành riêng.
- Notification bell thuộc layout/header shell phải có vùng riêng; không che page action.
- Không dùng z-index hack, pixel absolute hoặc margin chỉ đúng tại 1920px.
- Kiểm tra tối thiểu desktop 1280/1440/1920, tablet và mobile.
- Mobile: title và action có thể wrap thành hai hàng; nút vẫn nhìn thấy, không tràn ngang, không che bell.
- Giữ design system: primary blue `#004c91`, orange `#F37021` tiết chế, border/shadow nhẹ, focus ring và keyboard accessibility.

### 11.3. UI states

Mỗi list/tab phải có:

- Loading/skeleton phù hợp.
- Empty state theo context.
- Error + retry.
- Disabled/loading state cho submit/action chống double click.
- Tooltip/aria-label cho icon action.
- Responsive table hoặc mobile card đúng pattern hiện có.
- i18n Việt/Anh, không hardcode text mới chỉ ở một ngôn ngữ.

---

## 12. Backend implementation chain

Đồng bộ theo chuỗi:

```text
SQL
→ Domain Entity/Constants
→ EF Configuration/DbContext
→ DTO/Request/Response
→ FluentValidation Validator
→ Handler/Domain service/access policy
→ Controller/Route
→ Notification/Email/Audit/Idempotency
→ Frontend types/API/adapters/hooks/components
→ Tests
```

Yêu cầu kiến trúc:

- Controller mỏng, chỉ nhận HTTP input/current context và gọi MediatR.
- FluentValidation xử lý shape/input validation.
- Handler/domain/access service xử lý role, account conflict, campus scope, state transition và transaction.
- Không đặt business logic vào React component hoặc Controller.
- Không tin `registrantUserId`, `createdBy`, `decidedBy`, `hostAssignedBy`, role/subrole/campus từ client.
- Current actor lấy từ authenticated claims rồi revalidate user/status/scope từ DB khi cần.
- Reuse form validation rule/service hiện có để public và authenticated không lệch nhau.
- Reuse aggregate, account provision, duplicate/idempotency, notification và audit service hiện có.
- Tránh N+1 khi build list/count/relations.

Authenticated payload có thể mang semantic per-campus mode, nhưng backend phải validate mode theo role/scope. Không bắt buộc giữ đúng tên DTO ví dụ dưới đây:

```ts
type CampusSubmissionMode =
  | 'SEND_FOR_REVIEW'
  | 'SELF_HOST'
  | 'ASSIGN_HOST';
```

Regular Staff không được gửi host ID khác mình. Leader chỉ được gửi host candidate cho own campus.

---

## 13. Database/SQL bắt buộc

Audit SQL mới nhất trước khi sửa. Dự kiến tối thiểu cần:

### 13.1. `visit_requests`

```sql
registrant_user_id BIGINT UNSIGNED NULL
```

- FK tới `users(user_id)`.
- Index phục vụ registered list, ưu tiên `(registrant_user_id, submitted_at)` hoặc index phù hợp query plan thực tế.
- Sửa comment `visitor_user_id` thành contact owner/action owner.
- Giữ nullable để backfill/legacy an toàn; code mới phải populate với request mới.

Không đổi tên vật lý `visitor_user_id` chỉ để đẹp nếu việc rename phá quá nhiều handler/query. Có thể cải thiện semantic ở domain/DTO/comment nhưng phải giữ alignment.

### 13.2. `visit_request_campuses`

Mở audit actor cho direct Staff flow, dự kiến:

```sql
decision_actor_role ENUM('STAFF_LEADER', 'STAFF') NULL
```

Thêm nguồn quyết định rõ ràng, semantic tương đương:

```text
STANDARD_CAMPUS_REVIEW
INTERNAL_SELF_HOST
INTERNAL_LEADER_ASSIGN
```

Tên cột/value phải nhất quán với convention hiện tại, ví dụ `decision_source`; không dùng free-text để suy flow.

### 13.3. Trigger/invariant

Sửa/recreate trigger cần thiết để DB safety không mâu thuẫn code:

- Main request cancellation: `cancelled_by` phải đúng contact owner hiện hữu, không chỉ “một user bất kỳ có role VISITOR”.
- Visitor campus cancel: actor phải đúng contact owner của parent request.
- Regular Staff direct self-host chỉ hợp lệ nếu request đang được tạo bởi chính họ, own campus, host=self và source đúng.
- Staff thường không được assign người khác.
- Staff Leader direct assign chỉ own campus và candidate hợp lệ cùng campus.
- Không cho direct process other campus.
- Aggregate trigger/service vẫn mirror chính xác.

### 13.4. Fresh-create và upgrade patch

Vì SQL hiện là database-first:

- Cập nhật fresh-create SQL mới nhất.
- Tạo safe upgrade/backfill patch riêng cho DB đã có dữ liệu nếu repository có convention patch.
- Không chạy lại file có `DROP DATABASE` trên môi trường hiện tại.
- Không dùng EF Migration nếu Program/source xác nhận migrations không quản lý schema.

Backfill phải audit trước:

- Visitor-submitted same normalized email: có thể set `registrant_user_id = visitor_user_id`.
- Different email: chỉ link ACTIVE VISITOR đã tồn tại; không auto-create hàng loạt account lịch sử.
- Legacy `STAFF_CREATED`: không mù quáng gán. Audit `created_by`, role và registrant snapshot; unresolved để NULL/report thủ công nếu không chắc.
- Không rewrite lịch sử snapshot chỉ để khớp mô hình mới.

Thêm verification query phát hiện:

- Contact owner không phải VISITOR.
- Missing registrant ở request mới.
- Direct Staff decision sai campus/host.
- Duplicate relation/list anomalies.

### 13.5. Seed

Nếu project duy trì rich seed, thêm tối thiểu scenario:

- Public/Visitor registrant = contact owner.
- Public/Visitor registrant khác contact owner.
- Staff tạo đơn và gửi Leader.
- Staff own-campus self-host.
- Staff multi-campus self-host own campus + other campus pending.
- Staff Leader self-host.
- Staff Leader assign another same-campus host.
- Contact email conflict internal/inactive/locked cho test fixture phù hợp.

Không thêm bảng relation mới nếu requirement vẫn chỉ có một registrant và một contact owner.

---

## 14. Validation và error contract

### 14.1. Form validation chung

Giữ toàn bộ validation hiện tại về:

- Required registrant/contact/delegation fields.
- Campus/time range và minimum lead time hiện hành.
- Visit type/other.
- Purpose/working content.
- Guest/support minimum rows.
- Agenda rule.
- Language, transportation, media consent.
- Duplicate/idempotency.

Không làm public và authenticated có hai bộ rule lệch nhau.

### 14.2. Validation mới

Tạo error code ổn định, map đúng field/UI; tên cụ thể phải theo convention source. Tối thiểu phân biệt:

- Registrant email không thể dùng trong public Visitor flow vì thuộc internal account.
- Contact email thuộc internal role.
- Contact Visitor INACTIVE.
- Contact Visitor LOCKED.
- Internal registrant cố dùng chính mình làm contact.
- Visitor gửi campus processing mode không hợp lệ.
- Staff self-host other campus.
- Staff cố assign người khác.
- Leader assign host khác campus/inactive/sai IC role.
- Own campus không nằm trong selected campuses nhưng payload gửi direct mode.
- Campus pending không có ACTIVE Staff Leader.
- Direct host conflict warning/confirmation contract.
- Concurrency/idempotency duplicate.

Không trả chi tiết nhạy cảm kiểu “email này là tài khoản Staff X tại phòng ban Y” cho người ngoài. UI message chỉ cần nói email không thể dùng làm tài khoản đầu mối Visitor.

Validator kiểm tra shape; handler recheck DB-dependent rule trong transaction để chống TOCTOU/race.

---

## 15. Authorization và security

### 15.1. Backend là security boundary

Ẩn button/tab ở frontend chỉ là UX. Mọi endpoint phải tự authorize.

Test trực tiếp API phải chứng minh:

- Registrant viewer gọi edit/resubmit/cancel/feedback → 403.
- Visitor gửi self-host/assign-host fields → 400/403 theo error convention.
- Staff direct-process other campus → 403.
- Staff assign another host → 403.
- Leader assign candidate other campus → 403.
- Internal email không được repurpose thành Visitor.
- Payload spoof `registrantUserId`, `createdBy`, role/campus/decision metadata không có tác dụng.

### 15.2. Quan hệ chồng nhau

Một user có thể có nhiều relation. Authorization thực tế là union của relation hợp lệ, nhưng UI từng tab phải chỉ hiển thị action theo context:

- Registered tab luôn read-only.
- Hosted tab có host operations.
- Campus-review tab có Staff Leader actions.
- Visitor owner tab có owner actions.

Không vì user là registrant mà hạ quyền host/reviewer ở endpoint khác; cũng không vì họ là host mà cấp owner edit/resubmit/feedback.

### 15.3. Privacy

- Không trả dữ liệu profile contact account chỉ vì email lookup.
- Registrant DTO chỉ public-safe.
- Không log OTP, raw token, JWT, password, Google credential hoặc full sensitive payload.
- Audit các mutation/direct assignment/account linkage bằng ID và business metadata cần thiết.

### 15.4. Transaction và external side effects

Trong transaction:

- Account link/provision.
- Request/campus/guest/agenda/host participant.
- Aggregate status.
- Audit/in-app notification/idempotency state theo convention hiện tại.

Email/SMTP gửi sau commit hoặc theo outbox/status pattern hiện tại để email failure không rollback dữ liệu business đã commit; phải ghi failure đúng convention.

---

## 16. Notification, email và audit

### Public/Visitor request

- Registrant nhận xác nhận submission/read-only relation.
- Contact owner nhận thông báo mình là đầu mối và cách đăng nhập Visitor Portal.
- Nếu cùng user, chỉ một thông báo/email với owner semantics.
- Staff Leader của từng pending campus nhận action-required notification.

### Internal request

- Registrant internal nhận submission confirmation.
- External contact nhận ownership/login notice.
- Pending campus Leader nhận action-required notification.
- Own campus direct processed: Leader nhận informational notification nếu cần monitoring, không tạo pending action giả.
- Assigned host khác actor nhận host-assignment notification.

### Status/feedback

- Status updates có thể gửi cả registrant và contact owner.
- Action-required/cancel/edit/resubmit/feedback reminder chỉ gửi đúng actor có quyền.
- Pending feedback query chỉ dùng contact owner relation.
- Dùng `DedupeKey`/unique mechanism hiện có; không tạo notification trùng khi cùng recipient mang nhiều relation.

Audit tối thiểu:

- Public/authenticated submission source.
- Registrant/contact user IDs.
- Direct self-host hoặc Leader direct assign source.
- Campus decision/host actor/time.
- Account linkage/provision outcome ở mức không lộ dữ liệu nhạy cảm.

---

## 17. Frontend API, types và state

- Cập nhật type/interface cho `registrantUserId` chỉ ở response; không cho client tự gán identity.
- Thêm relation/view enum và `AllowedActions` theo response thật.
- API list gửi view/tab filter server-side.
- Abort/ignore stale request khi đổi tab/search nhanh nếu hook hiện có nguy cơ race.
- Search/filter/pagination/count phải theo active tab.
- Map error code vào đúng field và dịch VI/EN.
- Không filter permission-sensitive rows từ một full dataset ở browser.
- Không dùng mock data sau khi endpoint thật đã có.
- Không để hidden action vẫn gọi API qua keyboard/menu.

---

## 18. Test strategy bắt buộc — đủ nhưng không thừa

Trước khi viết test, đọc test infrastructure hiện tại và reuse fixture/helper. Unit test không dùng DB thật. Rule phụ thuộc query/transaction/FK/trigger phải nằm ở Integration Test MySQL test DB riêng.

Ưu tiên `[Theory]`/parameterized test cho matrix role/status/mode thay vì tách nhiều test trùng setup. Mỗi business rule quan trọng phải có ít nhất một test chứng minh.

### 18.1. Unit tests

#### Validator

- Required fields và normalized emails.
- Contact same/different matrix theo actor role.
- Campus mode payload shape.
- `ASSIGN_HOST` cần host ID; mode khác không nhận host ID.
- Parameterized invalid role/mode combinations không cần DB.

#### Access policy/allowed actions

- CONTACT_OWNER vs REGISTRANT_VIEWER.
- Registrant không có edit/resubmit/cancel/feedback.
- HOST/CAMPUS_REVIEWER relations không bị mất quyền riêng nhưng không biến thành owner.
- Visitor same registrant/contact ưu tiên owner relation.

#### Aggregate/state helper

- All pending → PENDING_APPROVAL.
- Single direct assigned → APPROVED.
- Mixed assigned/pending → PARTIALLY_APPROVED.
- Giữ các case rejected/cancelled hiện có không regression.

#### Account/normalization helper

- Same normalized email reuse user.
- Existing internal contact conflict.
- Existing Visitor không bị overwrite profile.

### 18.2. Integration tests — API + MySQL thật

#### Public unauthenticated

- Registrant = contact: một account, hai FK cùng ID, một email/notification owner semantics.
- Registrant khác contact: hai Visitor accounts/links đúng.
- Contact existing ACTIVE Visitor: reuse.
- Contact internal/inactive/locked: field-specific 400; không tạo request.
- Transaction failure: không để lại một trong hai account/request children.
- OTP V2, human verification, resend quota, session token và attempt persistence không regression.
- Submission idempotency và business duplicate logic không regression.

#### Authenticated Visitor

- Create button/endpoint tạo request không cần OTP lại.
- Registrant lấy từ token, payload spoof bị bỏ/chặn.
- Contact self và contact khác đều đúng.
- Tất cả campus bắt buộc WAITING_REQUEST_APPROVAL.
- Visitor cố gửi SELF_HOST/ASSIGN_HOST → rejected.

#### Authenticated Staff thường

- Own-campus `SEND_FOR_REVIEW` → pending, Leader coordinator/action notification.
- Own-campus `SELF_HOST` → campus assigned/approved semantics, host=self, audit/participant/decision source đúng.
- Single own-campus self-host → aggregate APPROVED.
- Multi own-campus self-host + other campus review → aggregate PARTIALLY_APPROVED; other campus pending đúng Leader.
- Staff direct-process other campus → 403.
- Staff assign another host → 403.
- Staff current email làm contact → rejected vì internal account.
- Staff registrant sau submit không có owner edit/cancel/feedback.
- Nếu Staff đồng thời host, host endpoint vẫn hoạt động đúng scope.

#### Authenticated Staff Leader

- Own-campus self-host.
- Own-campus assign ACTIVE same-campus IC Staff.
- Own-campus leave for review/later.
- Assign inactive/wrong-role/other-campus candidate → rejected.
- Other campus luôn pending cho Leader campus đó.
- Multi-campus aggregate đúng.

#### List/detail/permission

- Visitor contact tab chỉ owner rows.
- Visitor registrant tab loại row mà họ đồng thời là owner.
- Registrant detail chỉ public-safe fields.
- Direct mutation bằng registrant → 403 cho edit/resubmit/cancel/feedback.
- Staff registered list, hosted list và invitations không lẫn scope.
- Leader campus list chỉ own campus; registered/host views đúng.
- Pagination/search/filter/count chạy trên active server-side view.
- Same relation không tạo duplicate row/notification.

#### SQL/trigger/concurrency

- FK/mapping `registrant_user_id` thật.
- Trigger chặn cancelled_by là Visitor khác nhưng không phải contact owner.
- Trigger chặn forged Staff direct decision.
- Concurrent double submit/idempotency tạo đúng một request.
- Unique-email race không tạo hai users.

### 18.3. Frontend component/integration tests

- Nút tạo hiển thị cho VISITOR/STAFF/STAFF_LEADER; ẩn với role không hợp lệ.
- Button mở đúng shared form.
- Authenticated form prefill registrant; identity field read-only.
- Public form vẫn editable + OTP.
- Visitor chỉ có mode review.
- Staff own-campus thấy SELF_HOST/review; other campus locked review.
- Staff Leader own-campus thấy self/assign/later và host selector đúng.
- Server errors map đúng contact/registrant/campus field.
- Tab change reset page và không hiển thị stale actions.
- Registered tab không render owner actions.
- Loading/empty/error/retry.
- VI/EN không có raw key/hardcoded language leak.

### 18.4. UI visual/responsive test

Kiểm tra Staff, Staff Leader, Visitor ở tối thiểu:

- 1920px.
- 1440px.
- 1280px.
- Tablet.
- Mobile.

Assert/visual inspect:

- Notification bell không overlap create button.
- Button không bị cắt/che.
- Tabs không tràn ngang; nếu scroll thì có accessible tablist.
- Form/modal không tràn viewport; footer/action dùng được.
- Focus/keyboard/Escape/aria-label đúng pattern hiện tại.

### 18.5. Playwright/E2E tối thiểu

Chỉ tạo E2E cho các flow xuyên layer quan trọng, tránh trùng toàn bộ integration tests:

1. Public registrant khác contact → OTP → submit → registrant read-only list/contact owner list.
2. Logged-in Visitor tạo đơn → tất cả campus pending.
3. Staff own-campus self-host single request.
4. Staff multi-campus self-host own + other pending.
5. Staff Leader assign another host on own campus.
6. Registrant viewer không thấy/cannot execute mutation actions.

Nếu test environment không thể gửi email/OTP thật, dùng fixture/test seam hiện có; không bỏ qua backend authorization verification.

### 18.6. Regression/build

Chạy theo repository thực tế, tối thiểu:

```text
dotnet build PEMS.slnx
dotnet test các project Unit/Integration/Architecture liên quan
dotnet test full solution nếu thời gian/môi trường cho phép
npm run lint
npm run build
npx playwright test các suite liên quan
full Playwright suite nếu khả thi
```

Không báo pass nếu chưa chạy. Nếu không chạy được, ghi chính xác command, blocker và phần chưa verify.

---

## 19. Thứ tự triển khai an toàn

Thực hiện theo expand-and-backfill để code cũ vẫn chạy trong lúc chuyển đổi:

1. Audit current state và lập impact map.
2. SQL expand: nullable `registrant_user_id`, enum/source/index/FK/trigger patch.
3. Entity/EF mapping/constants.
4. Account provision service + access policy.
5. Public verify-and-create ghi cả registrant/contact, giữ OTP V2/idempotency.
6. Authenticated create command/endpoint và shared validation.
7. Campus direct mode + aggregate/audit/notification.
8. List/detail queries và authorization.
9. Shared frontend form + role-specific mode.
10. Buttons/tabs/layout/notification overlap/i18n.
11. Backfill/seed/verification query.
12. Unit → Integration → frontend → E2E → full regression.

Không bật registrant list trước khi backend detail/action authorization đã chặn đầy đủ, tránh mở nhầm quyền cancel/edit.

---

## 20. Phạm vi không được làm

- Không khôi phục HO approval multi-campus cũ.
- Không thêm dynamic permissions, `permissions`, `role_permissions` hoặc permission code runtime.
- Không cho Staff thường approve request Visitor đã tồn tại chỉ vì cùng campus.
- Không cho Staff/Leader xử lý trực tiếp campus khác.
- Không tự triển khai host transfer.
- Không tự động biến registrant/contact thành guest.
- Không đổi role internal account thành VISITOR.
- Không đưa OTP/session/raw token vào draft/log/response.
- Không dùng frontend-only filtering làm authorization.
- Không hardcode mock data để làm list có vẻ hoạt động.
- Không chạy fresh-create `DROP DATABASE` trên DB hiện có.
- Không sửa module không liên quan hoặc xóa behavior cũ chưa chứng minh là dead code.

---

## 21. Báo cáo current-state bắt buộc trước khi implement

Trước khi sửa, báo cáo ngắn gọn trong working notes:

1. Nút tạo hiện tồn tại ở role/page nào và mở component/route nào.
2. Public và authenticated hiện dùng command/endpoint nào.
3. `visitor_user_id` hiện được gán từ registrant hay contact trong code thật.
4. Có hay chưa `registrant_user_id` trong SQL/entity.
5. List handler hiện filter từng role như thế nào.
6. Mutation handlers hiện check ownership ra sao.
7. Trigger hiện chấp nhận actor nào.
8. OTP V2/idempotency/duplicate mới nhất đang nằm ở đâu.
9. Existing tests nào có thể reuse/extend.
10. Những tài liệu/comment legacy nào phải cập nhật để không gây hiểu nhầm.

Sau current-state audit, tiếp tục implement; không chờ xác nhận lại trừ khi phát hiện mâu thuẫn làm thay đổi bản chất business rule hoặc có nguy cơ mất dữ liệu.

---

## 22. Format báo cáo hoàn thành

Sau khi làm xong, trả báo cáo bằng tiếng Việt theo cấu trúc:

### 22.1. Kết quả

- Hoàn thành/chưa hoàn thành phần nào.
- Flow public, authenticated Visitor, Staff, Staff Leader.

### 22.2. Current-state findings và root cause

- Vì sao thiếu create button/tab.
- Vì sao notification bell overlap.
- Ownership/account mapping cũ hoạt động thế nào.

### 22.3. Files changed

| Layer | File | Thay đổi |
|---|---|---|
| SQL | ... | ... |
| Domain/EF | ... | ... |
| Backend | ... | ... |
| Frontend | ... | ... |
| Tests | ... | ... |

### 22.4. Business logic đã triển khai

- Hai account relations.
- Role/mode/campus matrix.
- Aggregate transition.
- List/tab/allowed actions.
- Notification/email/audit.

### 22.5. Validation và security

- Field/business validation.
- 401/403/scope/payload spoofing.
- Privacy/account conflict.
- Transaction/idempotency/concurrency.

### 22.6. SQL và backfill

- DDL/trigger/index/FK.
- Backfill result và unresolved rows.
- Verification queries.

### 22.7. Tests/build thực tế

Liệt kê từng command và kết quả thật, ví dụ:

```text
dotnet build: ...
Unit: x/x
Integration: x/x
Architecture: x/x
npm run lint: ...
npm run build: ...
Playwright: x/x
```

### 22.8. Remaining risks

- Chỉ ghi rủi ro thật còn lại, không che giấu test chưa chạy.

---

## 23. Definition of Done

- [ ] Đã audit source/SQL/test thật trước khi sửa.
- [ ] Public chưa đăng nhập vẫn submit qua OTP V2 và tạo/link đúng registrant/contact accounts.
- [ ] Authenticated Visitor/Staff/Staff Leader có nút tạo và submit flow thật.
- [ ] Form core được reuse; authenticated registrant được prefill, không cho spoof identity.
- [ ] Contact owner luôn là VISITOR; internal email conflict bị chặn.
- [ ] Visitor/public có thể cùng registrant/contact; internal actor thì không.
- [ ] `registrant_user_id` được ghi cho request mới và chỉ có read-only relation.
- [ ] Contact owner mutations vẫn status/time-gated.
- [ ] Staff self-host chỉ own campus trong create flow.
- [ ] Staff Leader self/assign/later chỉ own campus; other campus pending.
- [ ] Single/multi aggregate status đúng và mirror trigger/service.
- [ ] Visitor/Staff/Leader lists và tabs đúng scope, server-side pagination/filter/count.
- [ ] Visitor same owner/registrant không bị duplicate hai tab.
- [ ] Registrant direct mutation APIs trả 403.
- [ ] Nút tạo không còn bị notification bell che ở các breakpoint.
- [ ] UI loading/empty/error/responsive/i18n/accessibility đầy đủ.
- [ ] SQL fresh-create + safe patch/backfill + trigger/entity/EF alignment hoàn tất.
- [ ] Notification/email/audit không trùng và đúng actor.
- [ ] OTP V2/idempotency/duplicate/draft hiện có không regression.
- [ ] Unit, Integration, Architecture, frontend build/lint và E2E cần thiết đã pass hoặc có blocker được báo chính xác.
- [ ] Báo cáo cuối nêu đủ file sửa, test thật và rủi ro còn lại.

