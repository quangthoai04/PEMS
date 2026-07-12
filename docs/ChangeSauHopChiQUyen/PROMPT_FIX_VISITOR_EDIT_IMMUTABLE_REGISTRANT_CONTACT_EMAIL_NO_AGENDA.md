# PROMPT — FIX VISITOR EDIT/RESUBMIT: KHÓA NGƯỜI ĐĂNG KÝ, KHÓA DANH TÍNH ĐẦU MỐI VÀ KHÔNG CHO VISITOR SỬA AGENDA

## 1. Vai trò của AI Agent

Bạn là Senior Full-stack Engineer của dự án PEMS, đồng thời đảm nhiệm:

- Senior ASP.NET Core .NET 8 Clean Architecture Developer.
- Senior React Vite TypeScript Engineer.
- Database-first MySQL Engineer.
- Security/Authorization Reviewer.
- QA Engineer phụ trách Unit Test, Integration Test và Frontend Test phù hợp.

Không sửa code theo suy đoán. Trước khi sửa phải search, đọc source thật và báo cáo current state.

## 2. Bối cảnh dự án

PEMS là hệ thống quản lý yêu cầu tham quan/tiếp đoàn của FPT University.

- Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, EF Core/Pomelo MySQL.
- Frontend: React, Vite, TypeScript, Tailwind CSS.
- Database: MySQL 8, database-first, fresh-create/manual SQL.
- Role chuẩn: `ADMIN`, `HO`, `STAFF + LEADER/STAFF`, `DEPARTMENT + LEADER/STAFF`, `STUDENT`, `VISITOR`.
- Không sử dụng dynamic permissions hoặc các bảng `permissions`, `role_permissions` để phân quyền runtime.

Hệ thống hiện có luồng Visitor đăng nhập để mở lại đơn đang chờ xử lý hoặc đơn bị từ chối nhằm **Sửa đơn/Gửi lại**. Form hiện đang hiển thị các khối:

1. Thông tin người đăng ký.
2. Danh sách đoàn/yêu cầu chuyến thăm.
3. Đầu mối liên hệ.
4. Yêu cầu bổ sung và các nội dung khác.

Ngoài ra, hệ thống đã/đang có quan hệ actor của đơn để phân biệt người đăng ký form và đầu mối liên hệ. Phải đọc source và SQL mới nhất để xác định đúng table, field, enum và cách liên kết hiện có; không tự bịa tên.

## 3. Tài liệu và source bắt buộc đọc

Trước khi sửa, hãy đọc và đối chiếu tối thiểu:

1. `PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md`.
2. `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`.
3. `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`.
4. `VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md`.
5. `PERMISSION_MATRIX.md` và `PERMISSION_RULES.md`.
6. `CLEAN_ARCHITECTURE.md`.
7. `PEMS_UI_DESIGN_SYSTEM_PROMPT.md`.
8. `PROJECT_STRUCTURE_FULL.md`.
9. SQL fresh-create mới nhất và mọi SQL patch liên quan visit-request actor/account ownership.
10. `PEMS_v10_NEW_FINAL_SQL_TABLE_FIELD_DICTIONARY_MATCHED.docx`.
11. Source frontend/backend hiện tại và test hiện có của Create/Edit/Resubmit Visit Request.

Nếu tài liệu cũ mâu thuẫn với SQL/source mới đã được triển khai, hãy chỉ ra mâu thuẫn và ưu tiên nguồn chuẩn mới nhất. Không khôi phục các rule HO approval hoặc status legacy đã bị thay thế bởi campus-independent approval.

## 4. Mục tiêu

Sửa luồng Visitor **Sửa đơn** và **Gửi lại đơn** để đảm bảo:

1. Không thể thay đổi danh tính người đã đăng ký/nộp đơn.
2. Không thể đổi email hoặc tài khoản đầu mối liên hệ qua form chỉnh sửa thông thường.
3. Không thể thay đổi quan hệ “Tôi cũng là đầu mối liên hệ” sau khi đơn đã được gửi.
4. Đầu mối liên hệ hợp lệ vẫn có thể sửa các thông tin liên lạc không làm chuyển quyền sở hữu, nếu thỏa điều kiện sửa/gửi lại.
5. Visitor không được tạo, gửi, chỉnh sửa hoặc ghi đè Agenda; Agenda do Staff/Host chuẩn bị.
6. Bảo vệ phải nằm ở backend và authorization thật, không chỉ khóa input frontend.
7. Giữ nguyên các điều kiện Edit/Resubmit hiện hành và không phá vỡ OTP, actor relation, approval, campus status, audit hoặc notification.

## 5. Quyết định nghiệp vụ bắt buộc

### 5.1. Phân biệt hai nhóm thông tin

#### A. Thông tin người đăng ký

Sau khi đơn được gửi lần đầu, toàn bộ nhóm **Thông tin người đăng ký** là snapshot chỉ đọc, bao gồm các trường thực tế đang tồn tại trong source/SQL, ví dụ:

- Họ và tên.
- Quốc tịch.
- Đơn vị/tổ chức và liên kết partner nếu có.
- Chức danh/phòng ban.
- Mã quốc gia và số điện thoại.
- Email.
- User/actor ID hoặc quan hệ sở hữu liên quan.

Visitor không được sửa các dữ liệu này trong cả:

- Sửa đơn đang `PENDING_APPROVAL`.
- Gửi lại đơn đã `REJECTED`.

Lý do: đây là dấu vết người đã thực hiện việc đăng ký/nộp đơn. Không được biến Edit/Resubmit thành chuyển người đăng ký.

Nếu hệ thống có trang cập nhật hồ sơ tài khoản, việc thay đổi hồ sơ phải là luồng riêng và không tự động ghi đè snapshot của đơn đã gửi.

#### B. Thông tin đầu mối liên hệ

Sau khi đơn được gửi:

**Bắt buộc khóa, không cho thay đổi:**

- Email đầu mối liên hệ.
- User ID/account ID liên kết với đầu mối.
- Actor relation/actor type thể hiện ai là đầu mối.
- Checkbox hoặc lựa chọn “Tôi cũng là đầu mối liên hệ”.
- Bất kỳ thao tác nào làm chuyển quyền sửa/hủy/feedback sang tài khoản khác.

**Có thể cho đầu mối liên hệ thực sự chỉnh sửa:**

- Họ và tên đầu mối.
- Đơn vị công tác.
- Mã quốc gia và số điện thoại.
- Chức danh nếu form/source hiện có trường này.

Các thay đổi trên chỉ cập nhật dữ liệu/snapshot liên hệ của chính đơn. Không được âm thầm sửa bảng `users` hoặc hồ sơ toàn cục, trừ khi source hiện tại đã có một UC cập nhật profile riêng và task này được mở rộng rõ ràng. Không mở rộng trong task này.

Chỉ account đang được liên kết là đầu mối của đơn mới được thực hiện Edit/Resubmit. Nếu account chỉ có quan hệ **người đăng ký/người điền form** nhưng không phải đầu mối, họ chỉ được xem/ theo dõi trạng thái và không được thấy hoặc gọi action Edit/Resubmit/Cancel/Feedback.

Nếu cùng một account vừa là người đăng ký vừa là đầu mối:

- Thông tin người đăng ký vẫn là snapshot chỉ đọc.
- Email và quan hệ đầu mối vẫn bị khóa.
- Các trường liên lạc không định danh của phần đầu mối có thể sửa theo rule trên.

Không đồng bộ ngược các thay đổi phần đầu mối vào snapshot người đăng ký.

### 5.2. Agenda

Agenda do Staff/Host chuẩn bị, không phải nội dung Visitor khai báo.

Trong luồng Visitor Edit/Resubmit:

- Không hiển thị Agenda như trường editable.
- Nếu cần hiển thị để tham khảo, chỉ hiển thị read-only.
- Frontend không đưa Agenda vào update payload của Visitor.
- Backend command/DTO/handler dành cho Visitor không được nhận hoặc map Agenda.
- Payload giả mạo bằng DevTools/Postman không được tạo, sửa, xóa hoặc ghi đè Agenda hiện có.
- API quản lý Agenda phải tiếp tục được bảo vệ bằng role/scope dành cho Staff/Host đúng nghiệp vụ hiện tại.

### 5.3. Điều kiện Edit/Resubmit phải giữ nguyên

Đọc source thật để xác nhận chi tiết, nhưng phải giữ các rule đã triển khai:

- Edit pending chỉ khi request đang `PENDING_APPROVAL`.
- Tất cả campus instance vẫn chưa có quyết định và đang ở trạng thái chờ duyệt phù hợp với SQL/source mới.
- Thời điểm bắt đầu sớm nhất còn cách hiện tại tối thiểu 24 giờ.
- Resubmit chỉ áp dụng khi request bị `REJECTED` và các campus instance thỏa điều kiện rejected hiện hành.
- Resubmit giữ đúng logic xóa/reset quyết định, cập nhật `resubmission_count`, `last_resubmitted_at/by`, audit, notification và aggregate status hiện có.

Không tự thay đổi các rule trên nếu task không phát hiện bug trực tiếp liên quan.

## 6. Phạm vi được sửa

Sau khi search source, được sửa đúng các phần liên quan:

- Visitor Edit/Resubmit page/component/form schema.
- Form state, default values và payload mapper.
- Frontend types/API service liên quan.
- API endpoint/controller hiện có của Visitor Edit/Resubmit.
- Command/DTO, Validator, Handler và authorization/scope check liên quan.
- Mapping, query/detail DTO cần thiết để trả dữ liệu read-only.
- Audit log hiện có nếu cần ghi nhận các trường đầu mối được phép sửa.
- Unit Test, Integration Test và frontend test liên quan.
- Locale `vi.json`/`en.json` nếu thêm helper text hoặc lỗi mới.

## 7. Phạm vi không được sửa

- Không đổi schema/database nếu source và SQL hiện tại đã đủ khả năng lưu actor relation và snapshot. Nếu thật sự thiếu, phải dừng ở báo cáo phân tích và đề xuất SQL riêng; không tự thêm migration/column/table.
- Không sửa Create Visit Request public flow ngoài những chỗ dùng chung bắt buộc phải tách để Edit/Resubmit an toàn.
- Không thay đổi campus-independent approval.
- Không đưa HO trở lại luồng duyệt request mới.
- Không sửa host assignment, logistics, minutes, feedback hoặc news nếu không liên quan trực tiếp.
- Không triển khai chức năng “Đổi đầu mối liên hệ” trong task này.
- Không tự động sửa profile `users` từ payload chỉnh sửa request.
- Không thêm dynamic permission.
- Không dùng role/status/table/field legacy hoặc không tồn tại.

## 8. Quy trình phân tích trước khi code

Trước khi sửa, phải:

1. Search route/page/component của Visitor Edit và Resubmit.
2. Xác định Create và Edit có dùng chung component/schema/DTO hay không.
3. Đọc payload thực tế frontend đang gửi.
4. Đọc controller, command, validator và handler để xác định hiện tại trường nào có thể bị ghi đè.
5. Xác định nguồn authorization hiện tại đang dựa vào `created_by`, email, visitor user ID hay actor relation.
6. Đọc SQL/entity/configuration để xác định chính xác trường snapshot và actor relation.
7. Kiểm tra Agenda hiện được load/save ở đâu và có bị update gián tiếp trong handler hay không.
8. Đọc test hiện có và chỉ ra coverage đang thiếu.
9. Tìm mock/stub/dead code/legacy mapping có thể bỏ qua backend protection.
10. Viết báo cáo current state ngắn gọn trước khi bắt đầu sửa.

Không đoán tên file, API, bảng hoặc column. Hãy search source và dùng tên thật.

## 9. Yêu cầu frontend/UI

### 9.1. Thông tin người đăng ký

- Hiển thị toàn bộ khối ở chế độ chỉ đọc để Visitor vẫn kiểm tra được dữ liệu đã nộp.
- Không chỉ dùng styling làm input trông bị khóa trong khi vẫn có thể thay đổi state.
- Có thể dùng read-only information card hoặc control `readOnly/disabled` phù hợp, nhưng payload mapper tuyệt đối không gửi các trường bị khóa.
- Bỏ các dấu hiệu gây hiểu nhầm rằng trường còn chỉnh sửa được, ví dụ nút xóa organization, dropdown nationality hoặc con trỏ editable.
- Hiển thị helper text song ngữ, ví dụ:
  - VI: “Thông tin người đăng ký được ghi nhận tại thời điểm gửi đơn và không thể thay đổi trong lần chỉnh sửa này.”
  - EN: “Registrant information was recorded when the request was submitted and cannot be changed during this edit.”

### 9.2. Đầu mối liên hệ

- Email hiển thị read-only, không có nút clear và không cho sửa state.
- Checkbox “Tôi cũng là đầu mối liên hệ” hiển thị trạng thái đã lưu nhưng bị khóa trong Edit/Resubmit.
- Họ tên, đơn vị, điện thoại và chức danh nếu có vẫn editable cho account đầu mối hợp lệ.
- Không cho chọn account/email khác.
- Thêm helper text giải thích email được dùng để liên kết tài khoản và quyền của đầu mối nên không thể đổi trong form này.

### 9.3. Agenda

- Không render editor/input Agenda trong Visitor Edit/Resubmit.
- Nếu trang hiện cần hiển thị Agenda, dùng read-only section rõ ràng “Agenda do FPT University chuẩn bị”.
- Không để Agenda nằm trong schema validation hoặc dirty-state của Visitor form.

### 9.4. Authorization hiển thị action

- Account chỉ là người đăng ký nhưng không phải đầu mối: không hiển thị nút Sửa đơn/Gửi lại/Hủy/Feedback.
- Account là đầu mối hợp lệ: hiển thị action theo status/time rules hiện hành.
- Backend vẫn là nguồn quyết định cuối cùng; frontend hide action chỉ phục vụ UX.
- Bảo đảm responsive và song ngữ VI/EN theo design system hiện tại.

## 10. Yêu cầu backend

1. Ưu tiên tách request DTO/command của Create khỏi DTO/command của Visitor Edit/Resubmit nếu hiện đang dùng chung và gây mass assignment.
2. Update command chỉ chứa các trường Visitor thực sự được phép sửa.
3. Không lấy email/user ID/actor relation từ payload để xác định ownership. Lấy current user từ authenticated claims rồi kiểm tra quan hệ đầu mối trong database.
4. Handler không được map lại toàn bộ entity bằng payload theo cách ghi đè trường bảo vệ.
5. Giữ nguyên toàn bộ trường người đăng ký từ database.
6. Giữ nguyên contact email, linked user ID và actor relation từ database.
7. Chỉ map các trường đầu mối được phép chỉnh sửa.
8. Không map hoặc gọi logic cập nhật Agenda trong Visitor handler.
9. Không update bảng `users` từ command này.
10. Bảo đảm transaction atomic: nếu validation/authorization thất bại thì không có partial update.
11. Ghi audit cho các thay đổi hợp lệ theo cơ chế audit hiện có; không tự tạo audit framework mới.

### 10.1. Chống payload giả mạo/mass assignment

Nếu API hiện nhận full-form payload và chưa thể đổi contract ngay mà không phá flow:

- So sánh các field bảo vệ với giá trị hiện có trong database.
- Nếu client cố đổi registrant data, contact email, checkbox/relationship hoặc actor ID, trả lỗi nghiệp vụ `400 Bad Request` hoặc `409 Conflict` nhất quán với convention hiện tại.
- Không được silently chấp nhận payload rồi thay đổi dữ liệu.
- Không được có partial update trước khi trả lỗi.

Nếu tách được DTO an toàn và serializer bỏ qua field lạ, phải có test chứng minh payload chứa field Agenda hoặc field định danh không thể làm thay đổi database. Dùng error code/message theo convention có sẵn; không bịa convention mới nếu dự án đã có chuẩn.

### 10.2. Authorization

- Anonymous: `401 Unauthorized`.
- Visitor không liên quan đến request: `403 Forbidden` hoặc `404 Not Found` theo anti-enumeration convention hiện tại.
- Visitor chỉ là registrant/submitter nhưng không phải contact: không được Edit/Resubmit/Cancel/Feedback.
- Visitor là contact hợp lệ: được action nếu status/time rules cho phép.
- Không tin email trong request body.
- Không authorize chỉ bằng việc email text trùng nhau; ưu tiên user ID và actor relation đã xác minh.

## 11. Validation

- Các trường đầu mối được phép sửa vẫn phải giữ validation hiện hành: required, trim, max length, phone/country code, organization và quy tắc dữ liệu liên quan.
- Không validate các field người đăng ký như field editable trong Visitor Edit/Resubmit.
- Không validate Agenda trong Visitor Edit/Resubmit.
- Validation frontend và backend phải đồng bộ.
- Không cho whitespace-only hoặc dữ liệu vượt max length.
- Thông báo lỗi/helper mới phải có đủ VI/EN, không hard-code text ngoài locale.

## 12. Database/SQL alignment

- Đọc SQL fresh-create và patch actor relation mới nhất trước khi code.
- Không thêm migration EF tự động trong dự án database-first.
- Không đổi FK, unique constraint, status enum hoặc actor type nếu không có yêu cầu/schema chính thức.
- Không thay đổi identity/actor relation khi Edit/Resubmit.
- Không xóa lịch sử người đăng ký hoặc lịch sử quyết định.
- Nếu trường registrant/contact hiện nằm trực tiếp trên `visit_requests` hoặc bảng con khác, phải dùng đúng cấu trúc thật và bảo vệ tương đương.

## 13. Test bắt buộc

Không tạo test trùng lặp không cần thiết. Ưu tiên các test có giá trị chứng minh rule và security boundary.

### 13.1. Unit Test

Tối thiểu phải chứng minh:

1. Handler cập nhật được các trường đầu mối được phép sửa.
2. Handler không thay đổi bất kỳ trường người đăng ký nào.
3. Handler không thay đổi contact email, linked user ID hoặc actor relation.
4. Handler không tạo/sửa/xóa Agenda.
5. Validator từ chối trường đầu mối hợp lệ bị rỗng/whitespace/sai định dạng theo rule hiện hành.
6. Authorization/service xác định đúng contact và registrant-only nếu logic này có unit-testable component.

### 13.2. Integration Test API trên MySQL test thật

Tối thiểu phải có các case:

1. Anonymous Edit/Resubmit → `401`.
2. Visitor không liên quan → forbidden/not found theo convention.
3. Registrant-only nhưng không phải contact → không được Edit/Resubmit.
4. Contact hợp lệ + điều kiện pending hợp lệ → sửa được các trường contact không định danh và nội dung request được phép.
5. Cố đổi toàn bộ registrant data → request thất bại hoặc database vẫn giữ nguyên toàn bộ protected fields theo contract đã chọn.
6. Cố đổi contact email/account/relationship → không đổi database và không chuyển quyền.
7. Cố gửi Agenda trong payload → Agenda database trước và sau giống hệt nhau.
8. Payload có cả field hợp lệ và protected field giả mạo → không được partial update nếu API chọn reject payload.
9. Resubmit hợp lệ giữ nguyên registrant/contact identity nhưng vẫn thực hiện đúng reset decision/counter/audit hiện hành.
10. Các điều kiện status/campus decision/24 giờ vẫn được enforce.

Phải truy vấn lại database để assert dữ liệu thật sau API call, không chỉ assert status code.

### 13.3. Frontend Test

Tối thiểu chứng minh:

1. Khối người đăng ký hiển thị read-only.
2. Contact email và checkbox quan hệ bị khóa.
3. Contact name/organization/phone còn editable với contact hợp lệ.
4. Payload không chứa registrant protected fields, contact email/actor identity hoặc Agenda.
5. Registrant-only không thấy action Edit/Resubmit.
6. Agenda editor không xuất hiện trong Visitor flow.
7. Locale VI/EN không hiển thị raw translation key.

## 14. Build và verification

Sau khi sửa, chạy các lệnh đúng theo repository hiện tại, tối thiểu:

- Backend build.
- Unit Test liên quan.
- Integration Test liên quan trên test database, tuyệt đối không reset `pems_db` development.
- Frontend build.
- Frontend lint.
- Frontend test/Playwright liên quan nếu repository đã cấu hình.

Không báo “đã hoàn thành” nếu chưa chạy. Nếu môi trường không cho chạy bước nào, phải ghi rõ lệnh, blocker và phần chưa được verify.

## 15. Định dạng báo cáo đầu ra

Báo cáo cuối cùng phải gồm:

1. **Current-state findings:** file/API/handler hiện tại và nguyên nhân khiến field còn sửa được.
2. **Business rules implemented:** liệt kê chính xác field nào khóa, field nào còn sửa.
3. **Authorization findings:** registrant-only và contact được xác định bằng gì.
4. **Files changed:** file và mục đích thay đổi.
5. **Database impact:** xác nhận có/không thay SQL.
6. **Tests added/updated:** tên test và hành vi được chứng minh.
7. **Verification results:** số test pass/fail, build/lint.
8. **Remaining risks/blockers:** nếu có.

## 16. Definition of Done

Task chỉ hoàn thành khi:

- Người đăng ký không thể sửa bất kỳ dữ liệu snapshot nào trong Edit/Resubmit.
- Contact không thể đổi email, account hoặc quan hệ đầu mối.
- Contact hợp lệ vẫn sửa được name/organization/phone và field không định danh được cho phép.
- Registrant-only không thể Edit/Resubmit/Cancel/Feedback.
- Visitor không thể tạo hoặc thay đổi Agenda qua UI hay API.
- Backend chống được payload giả mạo và mass assignment.
- Không có partial update khi request bị từ chối.
- Không phá điều kiện status/campus/24 giờ/resubmission/audit hiện hành.
- UI rõ ràng, responsive và đủ VI/EN.
- Build và test liên quan đã pass hoặc blocker được báo cáo trung thực.

