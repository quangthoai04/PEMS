> [!WARNING]
> **LEGACY ARCHITECTURE NOTE (Campus-independent Approval Update)**
> This document has been updated to reflect the new Campus-independent Approval architecture.
> - **HO is now monitor/read-only.** There is no centralized multi-campus approval by HO.
> - **Staff Leader approval is per-campus.** Each Staff Leader directly receives and approves/rejects their own campus instance right after submission.
> - **Self-hosting is supported.** Staff Leaders can assign themselves as the host during approval.
> - **ASSIGNED is removed.** Approving a request now requires assigning a host immediately.
> - **New statuses:** `PARTIALLY_APPROVED` (request level) and `REJECTED` (campus level) are added. 
> - **Cancel logic:** Visitors can cancel requests in `PENDING_APPROVAL` or `PARTIALLY_APPROVED` states.
> - **Transportation:** `transportation_note` and `transportation_note` are replaced by `transportation_note`.
> Please refer to the latest codebase and SQL schema for the current implementation.

<!-- =====================================================================
PEMS DOC UPDATE v8.2-clean-sync-use-case-notes
Generated: 2026-06-20
Mode: FULL DOCUMENT CLEAN SYNC.
UC-136 has been added to the main UC Notes table.
Parser-risk related-UC table has been converted into bullet clarification notes.
===================================================================== -->

# UC Notes

> File này chỉ giữ các trường đã cần dùng hiện tại: **UC ID**, **UC Name**, **Note**. Các trường khác như Actor, Preconditions, Postconditions, FT, Priority, Status... tạm thời không đưa vào vì chưa kiểm định.
> **Updated for:** SQL v8.2 `SSO_AUTO_PROVISION` + strict visit visibility + UC-136 cancellation flow. ADMIN không xem/hủy visit/delegation; HO xem được cả đơn liên cơ sở và đơn một cơ sở ở mức theo dõi, nhưng chỉ được duyệt/từ chối/hủy/xử lý nghiệp vụ đối với đơn liên cơ sở; đơn một cơ sở là read-only đối với HO. Staff Leader chỉ xem/xử lý/hủy theo campus scope.

---

> ## ⚠️ Cập nhật tình trạng triển khai — 2026-07-02
>
> - **Rule "HO xem SINGLE_CAMPUS read-only" đã xác nhận đúng với code hiện tại** (evidence: `ViewGuestDelegationListQueryHandler.cs:455-456`, `ReadOnlyOnly`/`ActionableOnly` query flags dành riêng cho HO). Tên file này phản ánh đúng nghiệp vụ thật.
> - Note của **UC-21 Search Delegations** (dòng UC-21 bên dưới) mô tả nghiệp vụ đúng nhưng handler đứng sau route `searchdelegations` hiện là stub (`NotImplementedException`) — danh sách/tìm kiếm thật đang chạy qua UC-20 `View Guest Delegation List`.
> - Note của **UC-34 Submit Delegation Feedback**, **UC-36/UC-50-54 Partner**, **UC-55 View Document List**, **UC-61 Delete Gallery Item**, **UC-62/63 Minutes List/Search**, **UC-72-78 Calendar**, **UC-87 Assign Campus Lead**, **UC-89 Publish News**, **UC-92 Add Multilingual News** mô tả nghiệp vụ dự kiến đúng nhưng **code hiện tại chưa triển khai** (stub hoặc scaffold chết). Danh sách đầy đủ + evidence: `docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_..._v10_FULL_UPDATED.md` mục "V11 Implementation Status Addendum" và `docs/use-cases/USE_CASE_LIST.md` (đã cập nhật cùng ngày).
> - Note của **UC-116 Reassign Department Lead** ngụ ý "Department Lead" là actor hợp lệ duy nhất, nhưng code hiện tại không kiểm tra quyền actor nào cả.
> - **UC-136** (cuối file) cần bổ sung: Visitor giờ được hủy request ngay cả khi còn `PENDING_APPROVAL`, không chỉ sau khi đơn `APPROVED` như phần "Rule hủy theo role" mô tả — xem `docs/PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md` mục "V11.3".

---


| UC ID | UC Name | Note |
|---|---|---|
| UC-01 | View Homepage | Người dùng (đã đăng nhập hoặc chưa) xem trang chủ với tin tức, thư viện ảnh, FAQ và thông tin liên hệ. Dữ liệu chỉ lấy từ nội dung đã được công khai/Published; không hiển thị bản nháp, nội dung bị ẩn hoặc dữ liệu nội bộ. |
| UC-02 | Search Information | Tìm kiếm từ khóa trên toàn bộ nội dung công khai: tin tức, FAQ, đối tác, thư viện ảnh. Dữ liệu chỉ lấy từ nội dung đã được công khai/Published; không hiển thị bản nháp, nội dung bị ẩn hoặc dữ liệu nội bộ. |
| UC-03 | View Contact Info | Xem thông tin liên hệ của văn phòng hợp tác quốc tế FPTU trên trang chủ công khai. Dữ liệu chỉ lấy từ nội dung đã được công khai/Published; không hiển thị bản nháp, nội dung bị ẩn hoặc dữ liệu nội bộ. |
| UC-04 | View Policy & Terms | Xem chính sách sử dụng hệ thống và điều khoản dịch vụ; không cần đăng nhập. Dữ liệu chỉ lấy từ nội dung đã được công khai/Published; không hiển thị bản nháp, nội dung bị ẩn hoặc dữ liệu nội bộ. |
| UC-05 | View FAQ | Xem các mục FAQ đang hiển thị công khai trên trang chủ; không cần đăng nhập. Dữ liệu chỉ lấy từ nội dung đã được công khai/Published; không hiển thị bản nháp, nội dung bị ẩn hoặc dữ liệu nội bộ. |
| UC-06 | View News | Xem các bài tin tức đã xuất bản về các hoạt động hợp tác quốc tế của FPTU. Dữ liệu chỉ lấy từ nội dung đã được công khai/Published; không hiển thị bản nháp, nội dung bị ẩn hoặc dữ liệu nội bộ. |
| UC-07 | View Partners | Xem danh sách các tổ chức đối tác quốc tế của FPTU trên trang chủ công khai. Dữ liệu chỉ lấy từ nội dung đã được công khai/Published; không hiển thị bản nháp, nội dung bị ẩn hoặc dữ liệu nội bộ. |
| UC-08 | View Gallery | Xem thư viện ảo tham quan cơ sở mà không cần đăng nhập; hỗ trợ nội dung FT-05. Dữ liệu chỉ lấy từ nội dung đã được công khai/Published; không hiển thị bản nháp, nội dung bị ẩn hoặc dữ liệu nội bộ. |
| UC-09 | View Notifications | Xem thông báo cá nhân: phân công nhiệm vụ, nhắc nhở deadline, nhắc nhở sự kiện và cập nhật trạng thái. Thông báo phải được lọc theo đúng người nhận/role, hỗ trợ trạng thái đã đọc/chưa đọc và không lộ thông báo của người khác. |
| UC-10 | Login via SSO | Đăng nhập qua SSO theo đúng cổng đăng nhập. Cổng Visitor cho phép VISITOR đăng nhập bằng Google SSO/FEID; nếu email chưa tồn tại và chính sách cho phép, backend có thể tự tạo tài khoản VISITOR với `users.created_via = SSO_AUTO_PROVISION`, không gắn campus, không department và không sub_role. Cổng Internal không auto-provision tài khoản mới; tài khoản nội bộ phải tồn tại sẵn, đúng role/portal/campus và đúng trạng thái. Nếu user dùng sai cổng, backend phải từ chối bằng thông báo rõ ràng như: tài khoản của bạn không phù hợp với cổng đăng nhập này. |
| UC-11 | Login via Credentials | Đăng nhập bằng email và mật khẩu chỉ phục vụ giai đoạn triển khai/dev, test hoặc tài khoản được cấu hình LOCAL_PASSWORD. Ở production, cơ chế chính là SSO/FEID theo cổng đăng nhập. Login credentials vẫn phải kiểm tra trạng thái tài khoản, role, portal, campus nếu có, rate limit, lockout và audit log; không dùng làm luồng chính để cấp tài khoản thật. |
| UC-12 | Logout | Kết thúc phiên làm việc hiện tại và xóa token xác thực; cần thu hồi refresh token/session hiện tại nếu hệ thống có lưu phiên phía server. Chỉ người dùng đã đăng nhập được xem/sửa dữ liệu của chính mình; các trường nhạy cảm và role/campus không được tự ý thay đổi nếu không có quyền. |
| UC-13 | Forgot Password | Khởi tạo đặt lại mật khẩu chỉ áp dụng cho tài khoản LOCAL_PASSWORD trong giai đoạn dev/test hoặc trường hợp được cấu hình đặc biệt. Với tài khoản production SSO/FEID, việc khôi phục đăng nhập phụ thuộc nhà cung cấp định danh tương ứng, không tự reset mật khẩu local trong PEMS. OTP/link reset nếu dùng phải có thời hạn, giới hạn số lần gửi/nhập sai và không tiết lộ email có tồn tại hay không. |
| UC-14 | View Profile | Xem hồ sơ cá nhân: tên hiển thị, ảnh đại diện, vai trò, cơ sở phụ trách và thông tin liên hệ. Chỉ người dùng đã đăng nhập được xem/sửa dữ liệu của chính mình; các trường nhạy cảm và role/campus không được tự ý thay đổi nếu không có quyền. |
| UC-15 | Update Profile | Chỉnh sửa thông tin cá nhân: ảnh đại diện, tên hiển thị, số điện thoại và ngôn ngữ hiển thị ưa thích (VI/EN). Chỉ người dùng đã đăng nhập được xem/sửa dữ liệu của chính mình; các trường nhạy cảm và role/campus không được tự ý thay đổi nếu không có quyền. |
| UC-16 | Change Password | Đổi mật khẩu đăng nhập; yêu cầu xác minh mật khẩu hiện tại; mật khẩu mới phải đáp ứng quy tắc độ phức tạp. Cần kiểm tra mật khẩu hiện tại, độ mạnh mật khẩu mới, chống reuse nếu có rule và ghi nhận thời điểm thay đổi. |
| UC-17 | Submit Visit Request | Khách mời gửi yêu cầu thăm quan chính thức đến FPTU sau khi xác minh email/OTP thành công. Form chưa xác minh chỉ lưu tạm phía frontend/session, không tạo dòng trong `visit_requests` và không cần trạng thái `PENDING_EMAIL_VERIFICATION`. Khi OTP đúng và backend validate form hợp lệ, hệ thống tạo `visit_requests` với trạng thái đầu tiên `PENDING_APPROVAL`, lưu `registrant_nationality` nếu có và tạo các campus instance tương ứng. |
| UC-18 | Approve Cross-Campus Request | HO chỉ có quyền phê duyệt hoặc từ chối yêu cầu `MULTI_CAMPUS`. HO được phép thấy đơn `SINGLE_CAMPUS` trong danh sách/chi tiết ở chế độ theo dõi read-only để nắm tình hình toàn hệ thống, nhưng không được approve, reject, cancel, assign host hoặc thực hiện bất kỳ thao tác xử lý nào trên đơn `SINGLE_CAMPUS`. Khi HO duyệt liên cơ sở, backend ghi `decision_actor_role = HO`, `decided_by`, `decided_at`, `decision_note`; từ thời điểm này Staff Leader của các campus nằm trong đơn mới được thấy phần campus instance của mình. |
| UC-19 | View Guest Delegation Details | Xem chi tiết đoàn phái phải lọc theo strict visibility. ADMIN không xem chi tiết visit/delegation. HO được xem detail của `MULTI_CAMPUS` theo quyền xử lý liên cơ sở và được xem detail `SINGLE_CAMPUS` ở chế độ read-only/monitoring; với `SINGLE_CAMPUS`, backend phải trả `allowedActions` chỉ gồm xem hoặc rỗng hành động xử lý, không cho approve/reject/cancel/assign host. Staff Leader xem `SINGLE_CAMPUS` thuộc campus mình và xem `MULTI_CAMPUS` chỉ sau khi HO duyệt/release, chỉ nếu campus mình nằm trong đơn. Staff/Department/Student/VISITOR chỉ xem bản ghi mình được phân công, là participant, là owner hoặc có quan hệ hợp lệ với visit/delegation. Detail API không được chỉ dựa vào ID trên URL; phải kiểm tra role, scope, `visit_scope` và action permission ở backend. |
| UC-20 | View Guest Delegation List | Danh sách đoàn phái phải dùng source query theo role. HO được thấy cả `MULTI_CAMPUS` và `SINGLE_CAMPUS` để theo dõi tổng quan; tuy nhiên `MULTI_CAMPUS` mới có action duyệt/từ chối/hủy/xử lý theo UC tương ứng, còn `SINGLE_CAMPUS` phải hiển thị read-only và không được trả action xử lý trong `allowedActions`. Nếu dùng `vw_visit_requests_for_ho`, view/query này phải được cập nhật để gồm cả `SINGLE_CAMPUS` với cờ read-only/action scope rõ ràng. Staff Leader lấy từ `vw_visit_requests_for_staff_leader` và bắt buộc filter `visible_campus_id = CurrentUser.PrimaryCampusId`. ADMIN không có list nghiệp vụ visit/delegation. Các role còn lại chỉ thấy record liên quan đến phân công, tham gia, owner hoặc phạm vi được giao. |
| UC-21 | Search Delegations | Tìm kiếm đoàn phái phải áp dụng cùng scope với list/detail trước khi search hoặc trong cùng query. HO được search ra cả `MULTI_CAMPUS` và `SINGLE_CAMPUS`, nhưng kết quả `SINGLE_CAMPUS` của HO chỉ được mở xem read-only và không có action xử lý. Staff Leader không được search ra `MULTI_CAMPUS` đang chờ HO duyệt và không được thấy campus khác. ADMIN không được search visit/delegation nghiệp vụ. Search result không được trả dữ liệu ngoài quyền dù người dùng đoán đúng mã đơn/từ khóa. |
| UC-22 | Process Visit Request | Staff Leader chỉ xử lý request `SINGLE_CAMPUS` thuộc chính campus của mình khi trạng thái còn `PENDING_APPROVAL`. Staff Leader không xử lý `MULTI_CAMPUS`; đơn liên cơ sở đang chờ duyệt chỉ HO có quyền duyệt/từ chối qua UC-18. HO có thể nhìn thấy `SINGLE_CAMPUS` để theo dõi nhưng không được xử lý UC-22 trên đơn một cơ sở. Khi Staff Leader duyệt đơn một cơ sở, backend ghi `decision_actor_role = STAFF_LEADER`, `decided_by`, `decided_at`, `decision_note`; nếu từ chối/hủy phải ghi lý do và audit log. |
| UC-23 | Create Guest Delegation | Staff tạo thủ công hồ sơ đoàn phái mới: thông tin tổ chức khách, ngày thăm, mục đích và loại đoàn phái. Chỉ cho phép thao tác theo trạng thái hợp lệ của đoàn phái/yêu cầu; cần lưu audit log để truy vết thay đổi nghiệp vụ. |
| UC-24 | Update Guest Delegation | Chỉnh sửa thông tin đoàn phái (thông tin khách, ngày thăm, cơ sở, mục đích) khi trạng thái cho phép thay đổi. Chỉ cho phép thao tác theo trạng thái hợp lệ của đoàn phái/yêu cầu; cần lưu audit log để truy vết thay đổi nghiệp vụ. |
| UC-25 | Prepare Visit Logistics | Cấu hình logistics đón tiếp: thông điệp LED chào mừng, lộ trình tham quan, đặt phòng họp và phân công nhân sự. Thay đổi nguồn lực phải kiểm tra xung đột lịch/phòng/thiết bị và kích hoạt xác nhận lại nếu ảnh hưởng bộ phận liên quan. |
| UC-26 | Update Visit Logistics | Sửa đổi logistics đã cấu hình; kích hoạt yêu cầu xác nhận lại từ các bộ phận bị ảnh hưởng nếu có thay đổi nguồn lực. Thay đổi nguồn lực phải kiểm tra xung đột lịch/phòng/thiết bị và kích hoạt xác nhận lại nếu ảnh hưởng bộ phận liên quan. |
| UC-27 | Confirm Participation | Nhân sự được mời xác nhận hoặc từ chối vai trò tham gia trong sự kiện đón tiếp đoàn phái sắp tới. Cần ghi nhận người xử lý, thời điểm, trạng thái trước/sau và lý do khi từ chối; các bên liên quan nhận thông báo. |
| UC-28 | Approve Resource Request | Department Lead xem xét và chấp thuận hoặc từ chối yêu cầu phòng họp/thiết bị của Staff cho đoàn phái. Cần ghi nhận người xử lý, thời điểm, trạng thái trước/sau và lý do khi từ chối; các bên liên quan nhận thông báo. |
| UC-29 | Propose Resource Modification | Department Lead hoặc Department đề xuất thay đổi phân bổ nguồn lực đã được duyệt; Staff phải xác nhận trước khi áp dụng. Thay đổi nguồn lực phải kiểm tra xung đột lịch/phòng/thiết bị và kích hoạt xác nhận lại nếu ảnh hưởng bộ phận liên quan. |
| UC-30 | Confirm The Change Proposal | Staff chấp thuận hoặc từ chối đề xuất thay đổi nguồn lực từ nhân sự bộ phận; logistics được cập nhật khi chấp thuận. Cần ghi nhận người xử lý, thời điểm, trạng thái trước/sau và lý do khi từ chối; các bên liên quan nhận thông báo. |
| UC-31 | Create Meeting Minutes | Tạo mới biên bản họp trong đoàn phái đang diễn ra với danh sách tham dự, nội dung thảo luận và hạng mục hành động. Biên bản phải gắn với đúng đoàn phái, người tạo/tham gia và lưu phiên bản chỉnh sửa; khi đoàn phái đóng thì nội dung cần bị khóa hoặc chỉ xem. |
| UC-32 | Edit Meeting Minutes | Chỉnh sửa nội dung biên bản họp: điểm thảo luận, quyết định, hạng mục hành động và chữ ký điện tử tham dự viên. Biên bản phải gắn với đúng đoàn phái, người tạo/tham gia và lưu phiên bản chỉnh sửa; khi đoàn phái đóng thì nội dung cần bị khóa hoặc chỉ xem. |
| UC-33 | View Meeting Minutes Details | Xem toàn bộ nội dung của một hồ sơ biên bản họp cụ thể liên quan đến đoàn phái. Biên bản phải gắn với đúng đoàn phái, người tạo/tham gia và lưu phiên bản chỉnh sửa; khi đoàn phái đóng thì nội dung cần bị khóa hoặc chỉ xem. |
| UC-34 | Submit Delegation Feedback | Gửi đánh giá sao và nhận xét bằng văn bản cho sự kiện đoàn phái; quy tắc theo vai trò ngăn chủ nhà tự đánh giá. Mỗi người tham gia chỉ gửi phản hồi theo vai trò được phân công; không cho host tự đánh giá nếu business rule đã quy định. |
| UC-35 | Scan Business Card | Quét danh thiếp vật lý qua camera thiết bị; OCR trích xuất tên, chức danh, tổ chức, email và số điện thoại. Kết quả OCR/manual entry cần được người dùng kiểm tra trước khi lưu; email/tổ chức nên được kiểm tra trùng để tránh tạo partner lặp. |
| UC-36 | Create Partner Profile | Tạo hồ sơ tổ chức đối tác mới kèm thông tin người liên hệ; có thể được kích hoạt từ kết quả quét danh thiếp OCR. Kết quả OCR/manual entry cần được người dùng kiểm tra trước khi lưu; email/tổ chức nên được kiểm tra trùng để tránh tạo partner lặp. |
| UC-37 | Upload Attached Documents | Tải lên các tệp liên quan đến đoàn phái (đề xuất, tài liệu giới thiệu, thỏa thuận) vào thư viện tài liệu đoàn phái. Cần kiểm tra định dạng/kích thước file, quyền upload, liên kết đúng delegation và quét/kiểm soát file trước khi lưu. |
| UC-38 | Upload Visit Photos | Tải lên ảnh từ chuyến thăm đoàn phái để xây dựng lưu trữ ảnh có tài liệu cho hồ sơ đoàn phái. Cần kiểm tra định dạng/kích thước file, quyền upload, liên kết đúng delegation và quét/kiểm soát file trước khi lưu. |
| UC-39 | Tag Faces on Photos | Gắn thẻ khuôn mặt người tham dự trong ảnh đoàn phái đã tải lên để nhận dạng và lưu trữ ảnh có thể tìm kiếm. Cần kiểm tra định dạng/kích thước file, quyền upload, liên kết đúng delegation và quét/kiểm soát file trước khi lưu. |
| UC-40 | Create News Article | Tạo bài tin tức về chuyến thăm đoàn phái vừa hoàn thành; cần Staff Leader phê duyệt biên tập trước khi xuất bản. Bài viết tạo từ đoàn phái cần lưu nháp và đi qua luồng phê duyệt trước khi hiển thị công khai. |
| UC-41 | Close Delegation | Chính thức đóng hồ sơ đoàn phái, khóa toàn bộ nội dung và lưu trữ để tham khảo lịch sử. Chỉ đóng khi các nhiệm vụ bắt buộc hoàn tất; sau khi đóng hồ sơ bị khóa, chỉ vai trò có quyền mới được xem lịch sử. |
| UC-42 | View Email Template List | Cho phép HO xem danh sách mẫu email tự động đang được cấu hình trong hệ thống, gồm tên mẫu, sự kiện kích hoạt, ngôn ngữ, trạng thái và lần cập nhật cuối. Chỉ hiển thị mẫu theo quyền quản trị; không chỉnh sửa nội dung tại màn hình danh sách. |
| UC-43 | View Email Template Detail | Cho phép HO xem chi tiết một mẫu email tự động, gồm subject, body, biến động/placeholder, ngôn ngữ, sự kiện kích hoạt và trạng thái sử dụng. Cần kiểm tra placeholder hợp lệ và tránh hiển thị token hoặc thông tin cấu hình nhạy cảm. |
| UC-44 | Update Email Template | Cho phép HO cập nhật nội dung mẫu email tự động đã tồn tại như tiêu đề, thân email, ngôn ngữ, placeholder và trạng thái. Hệ thống cần validate biến bắt buộc, lưu lịch sử chỉnh sửa và không làm gián đoạn các email đã gửi trước đó. |
| UC-45 | Create Email Template | Tạo và tùy chỉnh mẫu email tự động cho các sự kiện hệ thống: xác nhận thăm quan, cấp tài khoản, nhắc nhở deadline. Mẫu email thuộc cấu hình hệ thống, cần validate placeholder, ngôn ngữ và sự kiện kích hoạt; chỉ vai trò có quyền mới được tạo/sửa. |
| UC-46 | Edit Email Content | Chỉnh sửa nội dung email trước khi gửi; người dùng có thể bắt đầu từ mẫu có sẵn hoặc soạn từ đầu. Email phải có người nhận hợp lệ, nội dung được lưu lịch sử/outbox và phân quyền xem theo người gửi/người nhận hoặc đoàn phái liên quan. |
| UC-47 | Send Email | Gửi email đã soạn đến đối tác hoặc người dùng hệ thống; email đã gửi được lưu trong lịch sử. Email phải có người nhận hợp lệ, nội dung được lưu lịch sử/outbox và phân quyền xem theo người gửi/người nhận hoặc đoàn phái liên quan. |
| UC-48 | View Email | Xem hộp thư đến và lịch sử email đã gửi trong phạm vi cá nhân hoặc phạm vi người dùng là participant hợp lệ. Quyền xem email nên được hiểu là Own-scope: chỉ xem email do chính user gửi/nhận, email trong conversation user tham gia, hoặc email liên kết với visit request/delegation mà user có quyền truy cập. Không được xem toàn bộ email hệ thống chỉ vì có quyền xem email. |
| UC-49 | Reply to Email | Soạn và gửi trả lời cho email đã nhận; trả lời được nối thành chủ đề với tin nhắn gốc. Email phải có người nhận hợp lệ, nội dung được lưu lịch sử/outbox và phân quyền xem theo người gửi/người nhận hoặc đoàn phái liên quan. |
| UC-50 | Process Partner Creation Request | Staff Leader xem xét và chấp thuận hoặc từ chối yêu cầu tạo tổ chức đối tác mới do Staff gửi. Cần kiểm tra trùng tổ chức/người liên hệ trước khi duyệt; từ chối phải có lý do và thông báo cho Staff gửi yêu cầu. |
| UC-51 | Edit Partner Information | Cập nhật thông tin tổ chức đối tác: tên, quốc gia, loại quan hệ, phân loại, mô tả và thông tin liên hệ. Partner cần quản lý theo trạng thái quan hệ, thông tin liên hệ và lịch sử đoàn phái; thao tác sửa phải lưu audit log. |
| UC-52 | View Partner Lists | Duyệt danh sách phân trang các tổ chức đối tác; có thể lọc theo quốc gia, loại hoặc trạng thái quan hệ. Partner cần quản lý theo trạng thái quan hệ, thông tin liên hệ và lịch sử đoàn phái; thao tác sửa phải lưu audit log. |
| UC-53 | Search Partners | Tìm kiếm hồ sơ đối tác theo tên tổ chức, quốc gia gốc hoặc từ khóa phân loại. Partner cần quản lý theo trạng thái quan hệ, thông tin liên hệ và lịch sử đoàn phái; thao tác sửa phải lưu audit log. |
| UC-54 | View Partner Details | Xem đầy đủ hồ sơ đối tác: thông tin tổ chức, người liên hệ, các đoàn phái đã qua và tài liệu liên quan. Partner cần quản lý theo trạng thái quan hệ, thông tin liên hệ và lịch sử đoàn phái; thao tác sửa phải lưu audit log. |
| UC-55 | View Document List | Xem tất cả tài liệu đã tải lên qua các hồ sơ đoàn phái; lọc theo ID đoàn phái, loại tệp hoặc ngày tải lên. Chỉ hiển thị/tìm kiếm tài liệu người dùng có quyền truy cập theo đoàn phái/cơ sở; cần hỗ trợ lọc và không cho tải file bị hạn chế quyền. |
| UC-56 | Search Documents | Tìm kiếm tài liệu đoàn phái theo tên tệp, loại tệp, tên người tải lên hoặc ID đoàn phái liên quan. Chỉ hiển thị/tìm kiếm tài liệu người dùng có quyền truy cập theo đoàn phái/cơ sở; cần hỗ trợ lọc và không cho tải file bị hạn chế quyền. |
| UC-57 | View Gallery Item List | Xem danh sách tất cả mục thư viện ảo tham quan cơ sở (hình ảnh, tiêu đề, mô tả) trong giao diện quản lý. Nội dung gallery phải có ảnh, tiêu đề/mô tả phù hợp và trạng thái hiển thị; file upload cần kiểm tra định dạng/kích thước. |
| UC-58 | Search Gallery Items | Tìm kiếm mục thư viện ảo theo tiêu đề, vị trí cơ sở hoặc từ khóa mô tả. Nội dung gallery phải có ảnh, tiêu đề/mô tả phù hợp và trạng thái hiển thị; file upload cần kiểm tra định dạng/kích thước. |
| UC-59 | Add Gallery Item | Tải lên hình ảnh tham quan cơ sở mới kèm tiêu đề và văn bản mô tả vào thư viện ảo. Nội dung gallery phải có ảnh, tiêu đề/mô tả phù hợp và trạng thái hiển thị; file upload cần kiểm tra định dạng/kích thước. |
| UC-60 | Update Gallery Item | Chỉnh sửa chi tiết mục thư viện: cập nhật tiêu đề, mô tả hoặc thay thế tệp hình ảnh hiện có. Nội dung gallery phải có ảnh, tiêu đề/mô tả phù hợp và trạng thái hiển thị; file upload cần kiểm tra định dạng/kích thước. |
| UC-61 | Delete Gallery Item | Xóa vĩnh viễn một mục khỏi thư viện tham quan ảo; việc xóa diễn ra ngay lập tức và không thể hoàn tác. Cần xác nhận trước khi xóa; nên cân nhắc soft delete thay vì xóa vĩnh viễn để tránh mất dữ liệu public/ảnh đã liên kết. |
| UC-62 | View Minutes List | Xem biên bản họp đã lưu trữ từ tất cả đoàn phái đã đóng; lọc theo ngày, cơ sở hoặc ID đoàn phái. Kho biên bản chỉ hiển thị hồ sơ người dùng có quyền xem; hỗ trợ lọc theo đoàn phái, thời gian, tác giả và trạng thái lưu trữ. |
| UC-63 | Search/Filter Minutes | Tìm kiếm kho lưu trữ biên bản họp theo từ khóa, ID đoàn phái, tác giả biên bản hoặc khoảng thời gian. Kho biên bản chỉ hiển thị hồ sơ người dùng có quyền xem; hỗ trợ lọc theo đoàn phái, thời gian, tác giả và trạng thái lưu trữ. |
| UC-64 | View List FAQ | Cho phép HO xem danh sách FAQ trong màn hình quản trị, gồm câu hỏi, ngôn ngữ, trạng thái hiển thị, ngày cập nhật và người cập nhật. Màn hình dùng để chọn FAQ cần chỉnh sửa/ẩn/hiện, không hiển thị FAQ ở trạng thái bị xóa mềm nếu có. |
| UC-65 | Create FAQ | Cho phép HO tạo FAQ mới với câu hỏi/câu trả lời  và trạng thái ban đầu Draft hoặc Hidden. Cần validate nội dung bắt buộc, tránh trùng câu hỏi quá rõ và chỉ FAQ Visible/Published mới hiển thị ra trang công khai. |
| UC-66 | Update FAQ | Chỉnh sửa mục FAQ hiện có: sửa đổi văn bản câu hỏi hoặc nội dung trả lời bằng một trong hai ngôn ngữ. FAQ cần quản lý song ngữ, trạng thái hiển thị và nội dung bắt buộc; chỉ FAQ Visible/Published mới xuất hiện ở trang công khai. |
| UC-67 | Change FAQ Visibility | Chuyển đổi trạng thái hiển thị của mục FAQ (Hiển thị hoặc Ẩn) mà không xóa vĩnh viễn. FAQ cần quản lý song ngữ, trạng thái hiển thị và nội dung bắt buộc; chỉ FAQ Visible/Published mới xuất hiện ở trang công khai. |
| UC-68 | Search FAQ | Tìm kiếm các mục FAQ theo từ khóa trong giao diện quản lý của HO để chỉnh sửa hoặc quản lý hiển thị. FAQ cần quản lý song ngữ, trạng thái hiển thị và nội dung bắt buộc; chỉ FAQ Visible/Published mới xuất hiện ở trang công khai. |
| UC-69 | View Dashboard Statistics | Xem bảng thống kê đoàn phái: tổng số tiếp đón theo năm, phân tích theo quốc gia khách, số lượt tham quan trực tuyến. Số liệu phải tính theo bộ lọc quyền/cơ sở/thời gian, nêu rõ kỳ báo cáo và định dạng xuất file; tránh xuất dữ liệu ngoài phạm vi quyền. |
| UC-70 | Export Statistics Report | Xuất bảng thống kê hiện tại dưới dạng tệp báo cáo PDF hoặc Excel có thể tải xuống. Số liệu phải tính theo bộ lọc quyền/cơ sở/thời gian, nêu rõ kỳ báo cáo và định dạng xuất file; tránh xuất dữ liệu ngoài phạm vi quyền. |
| UC-71 | Filter Dashboard By Time | Áp dụng bộ lọc khoảng thời gian tùy chỉnh cho bảng thống kê để xem dữ liệu theo giai đoạn báo cáo cụ thể. Số liệu phải tính theo bộ lọc quyền/cơ sở/thời gian, nêu rõ kỳ báo cáo và định dạng xuất file; tránh xuất dữ liệu ngoài phạm vi quyền. |
| UC-72 | View My Events | Xem lịch cá nhân hiển thị các sự kiện đoàn phái được phân công, deadline hạng mục hành động và sự kiện cá nhân. Lịch phải hợp nhất sự kiện cá nhân và sự kiện đoàn phái theo quyền xem; cập nhật/xóa chỉ áp dụng cho sự kiện cá nhân do người dùng sở hữu. |
| UC-73 | View Department Calendar | Xem lịch chia sẻ của bộ phận hiển thị tất cả sự kiện của bộ phận và cơ sở người dùng. Lịch phải hợp nhất sự kiện cá nhân và sự kiện đoàn phái theo quyền xem; cập nhật/xóa chỉ áp dụng cho sự kiện cá nhân do người dùng sở hữu. |
| UC-74 | Switch View Mode | Chuyển đổi chế độ hiển thị lịch giữa Ngày, Tuần, Tháng và danh sách Lịch biểu. Lịch phải hợp nhất sự kiện cá nhân và sự kiện đoàn phái theo quyền xem; cập nhật/xóa chỉ áp dụng cho sự kiện cá nhân do người dùng sở hữu. |
| UC-75 | Add Personal Event | Tạo sự kiện cá nhân tùy chỉnh với tiêu đề, ngày, giờ, địa điểm và mô tả tùy chọn. Lịch phải hợp nhất sự kiện cá nhân và sự kiện đoàn phái theo quyền xem; cập nhật/xóa chỉ áp dụng cho sự kiện cá nhân do người dùng sở hữu. |
| UC-76 | Delete Personal Event | Xóa sự kiện cá nhân đã tạo trước đó; chỉ chủ sở hữu sự kiện mới có thể xóa sự kiện của mình. Lịch phải hợp nhất sự kiện cá nhân và sự kiện đoàn phái theo quyền xem; cập nhật/xóa chỉ áp dụng cho sự kiện cá nhân do người dùng sở hữu. |
| UC-77 | Update Personal Event | Chỉnh sửa sự kiện lịch cá nhân: tiêu đề, ngày, giờ, địa điểm hoặc mô tả. Lịch phải hợp nhất sự kiện cá nhân và sự kiện đoàn phái theo quyền xem; cập nhật/xóa chỉ áp dụng cho sự kiện cá nhân do người dùng sở hữu. |
| UC-78 | View Event Details | Xem đầy đủ chi tiết sự kiện lịch: tên đoàn phái, ngày/giờ, địa điểm và người tham dự được phân công. Lịch phải hợp nhất sự kiện cá nhân và sự kiện đoàn phái theo quyền xem; cập nhật/xóa chỉ áp dụng cho sự kiện cá nhân do người dùng sở hữu. |
| UC-79 | Search/Filter Feedback | Tìm kiếm và lọc mục phản hồi đoàn phái theo ID đoàn phái, vai trò người tham dự, đánh giá sao hoặc ngày gửi. Kết quả phản hồi cần tổng hợp theo đoàn phái/vai trò và ẩn thông tin nhạy cảm nếu quy định; chỉ người có quyền báo cáo được xem. |
| UC-80 | View Feedback Summary | Xem tóm tắt phản hồi tổng hợp theo đoàn phái: điểm đánh giá sao trung bình và nhận xét nhóm theo vai trò tham dự. Kết quả phản hồi cần tổng hợp theo đoàn phái/vai trò và ẩn thông tin nhạy cảm nếu quy định; chỉ người có quyền báo cáo được xem. |
| UC-81 | Add New Campus | Thêm cơ sở FPTU mới với tên, vị trí, địa chỉ, thông tin liên hệ và trạng thái hoạt động ban đầu là Active. Campus là dữ liệu master; cần kiểm tra trùng tên/mã, trạng thái Active/Inactive và ảnh hưởng đến phân công đoàn phái/tài khoản khi thay đổi. |
| UC-82 | View Campus List | Xem danh sách tất cả cơ sở FPTU kèm trạng thái hoạt động và trưởng cơ sở hiện được phân công. Campus là dữ liệu master; cần kiểm tra trùng tên/mã, trạng thái Active/Inactive và ảnh hưởng đến phân công đoàn phái/tài khoản khi thay đổi. |
| UC-83 | Search and Filter Campus | Tìm kiếm cơ sở theo tên hoặc lọc theo trạng thái hoạt động và khu vực địa lý. Campus là dữ liệu master; cần kiểm tra trùng tên/mã, trạng thái Active/Inactive và ảnh hưởng đến phân công đoàn phái/tài khoản khi thay đổi. |
| UC-84 | View Campus Details | Xem đầy đủ thông tin cơ sở: tên, vị trí, danh sách bộ phận, trưởng phụ trách và trạng thái hiện tại. Campus là dữ liệu master; cần kiểm tra trùng tên/mã, trạng thái Active/Inactive và ảnh hưởng đến phân công đoàn phái/tài khoản khi thay đổi. |
| UC-85 | Update Campus | Chỉnh sửa chi tiết cơ sở: tên, địa chỉ vật lý, thông tin liên hệ và trưởng cơ sở được phân công. Campus là dữ liệu master; cần kiểm tra trùng tên/mã, trạng thái Active/Inactive và ảnh hưởng đến phân công đoàn phái/tài khoản khi thay đổi. |
| UC-86 | Manage Campus Status | Kích hoạt hoặc vô hiệu hóa cơ sở; cơ sở bị vô hiệu hóa bị loại trừ khỏi phân công đoàn phái mới. Campus là dữ liệu master; cần kiểm tra trùng tên/mã, trạng thái Active/Inactive và ảnh hưởng đến phân công đoàn phái/tài khoản khi thay đổi. |
| UC-87 | Assign Campus Lead | Chỉ định người dùng Staff Leader làm điều phối viên chính cho cơ sở cụ thể; trưởng cũ được thông báo về thay đổi. Campus là dữ liệu master; cần kiểm tra trùng tên/mã, trạng thái Active/Inactive và ảnh hưởng đến phân công đoàn phái/tài khoản khi thay đổi. |
| UC-88 | Approve News | Staff Leader xem xét bài tin tức đã gửi và chấp thuận hoặc từ chối kèm ghi chú phản hồi biên tập. Quyết định duyệt/từ chối phải ghi nhận người duyệt, thời điểm và ghi chú; tác giả nhận thông báo để chỉnh sửa nếu bị từ chối. |
| UC-89 | Publish News | Xuất bản bài tin tức đã được phê duyệt để hiển thị công khai trên trang chủ hệ thống. Chỉ bài đã được duyệt mới được publish; trạng thái ẩn/hiện không xóa nội dung và phải đồng bộ với trang công khai. |
| UC-90 | View News List | Duyệt bài tin tức trong giao diện quản lý; lọc theo trạng thái thực tế của SQL mới: `PENDING_REVIEW`, `REJECTED`, `PUBLISHED`, `HIDDEN`. Không dùng trạng thái `DRAFT`/`ARCHIVED` cho news. Tin tức hỗ trợ dữ liệu song ngữ, ảnh bìa, section rich text và file/ảnh theo từng section. |
| UC-91 | View News Details | Xem đầy đủ nội dung bài tin tức trong giao diện quản lý hoặc giao diện công khai. Public chỉ xem `PUBLISHED`; quản lý nội bộ xem theo quyền và trạng thái `PENDING_REVIEW`, `REJECTED`, `PUBLISHED`, `HIDDEN`. Nội dung chi tiết lấy từ `news_translations`, `news_content_sections` và `news_section_files`. |
| UC-92 | Add Multilingual News | Tạo bài tin tức đa ngôn ngữ bằng `news_translations`; nội dung rich text chia thành `news_content_sections`; ảnh/file inline lưu ở `news_section_files`. Bài mới mặc định `PENDING_REVIEW`, không dùng trạng thái `DRAFT`. |
| UC-93 | Manage News Visibility | Chuyển đổi bài tin tức giữa `PUBLISHED` và `HIDDEN` mà không xóa hồ sơ. Chỉ bài đã đủ điều kiện xuất bản mới được hiển thị công khai; `HIDDEN` không xuất hiện ở public homepage/news list. |
| UC-94 | Edit News | Chỉnh sửa nội dung, tiêu đề, bản dịch, section rich text, ảnh bìa hoặc file/ảnh trong section theo quyền. Bài bị từ chối có thể chỉnh và nộp lại về `PENDING_REVIEW`; bài đã published nếu sửa nội dung quan trọng cần tuân theo workflow review của module news. |
| UC-95 | View Account List | Xem danh sách phân trang tất cả tài khoản người dùng hệ thống; lọc theo loại vai trò, cơ sở và trạng thái. Danh sách/chi tiết tài khoản phải lọc theo quyền quản trị và không hiển thị dữ liệu nhạy cảm như mật khẩu/token. |
| UC-96 | Create Account | Tạo tài khoản người dùng mới với role, sub_role nếu cần, campus và department hợp lệ. Trong giai đoạn triển khai/dev có thể tạo tài khoản dùng LOCAL_PASSWORD để test. Khi build hệ thống thật theo SSO-first, hệ thống không gửi mật khẩu tạm; Staff Leader hoặc role được cấp quyền tạo tài khoản tạo tài khoản bằng email và thông tin role/campus để người dùng đăng nhập qua SSO/FEID đúng cổng. Tài khoản nội bộ không được auto-provision khi login nếu chưa tồn tại trong hệ thống. |
| UC-97 | Manage Account Status | Kích hoạt, vô hiệu hóa hoặc tạm khóa tài khoản; vô hiệu hóa sẽ chấm dứt ngay tất cả phiên đăng nhập đang hoạt động. Khi khóa/vô hiệu hóa cần thu hồi phiên/token hiện tại và ghi log; không nên xóa tài khoản đã có lịch sử nghiệp vụ. |
| UC-98 | View Account Details | Xem đầy đủ thông tin tài khoản: tên, email, vai trò, cơ sở, trạng thái và thời gian đăng nhập cuối cùng. Danh sách/chi tiết tài khoản phải lọc theo quyền quản trị và không hiển thị dữ liệu nhạy cảm như mật khẩu/token. |
| UC-99 | Search and Filter Accounts | Tìm kiếm tài khoản theo tên hoặc email; lọc theo loại vai trò, cơ sở phụ trách hoặc trạng thái tài khoản. Danh sách/chi tiết tài khoản phải lọc theo quyền quản trị và không hiển thị dữ liệu nhạy cảm như mật khẩu/token. |
| UC-100 | Update Account Role | Thay đổi vai trò được phân công của tài khoản người dùng. Trường hợp người dùng đang là VISITOR muốn chuyển sang vai trò nội bộ, Staff Leader của campus có thể cập nhật role/sub_role, campus và department phù hợp; campus sau khi chuyển role được tính theo campus mà Staff Leader đang quản lý hoặc campus được chọn trong thao tác cập nhật. Sau khi chuyển sang role nội bộ, user phải đăng nhập qua cổng nội bộ và chọn đúng campus. Thay đổi role phải ghi audit log và có thể yêu cầu đăng nhập lại để nhận quyền mới. |
| UC-101 | Add New Department | Tạo bộ phận mới với tên, loại, cơ sở phụ trách và trạng thái ban đầu là Active. Department là dữ liệu master theo campus; cần kiểm tra trạng thái Active/Inactive và ảnh hưởng đến phân công nhiệm vụ đoàn phái. |
| UC-102 | Update Department | Chỉnh sửa thông tin bộ phận: tên, loại, mô tả và cơ sở phụ trách. Department là dữ liệu master theo campus; cần kiểm tra trạng thái Active/Inactive và ảnh hưởng đến phân công nhiệm vụ đoàn phái. |
| UC-103 | Search and Filter Departments | Tìm kiếm bộ phận theo tên hoặc lọc theo cơ sở phụ trách và trạng thái hoạt động. Department là dữ liệu master theo campus; cần kiểm tra trạng thái Active/Inactive và ảnh hưởng đến phân công nhiệm vụ đoàn phái. |
| UC-104 | View Department List | Duyệt danh sách tất cả bộ phận trên các cơ sở kèm trạng thái và thông tin trưởng bộ phận. Department là dữ liệu master theo campus; cần kiểm tra trạng thái Active/Inactive và ảnh hưởng đến phân công nhiệm vụ đoàn phái. |
| UC-105 | View Department Details | Xem đầy đủ thông tin bộ phận: tên, loại, cơ sở, trưởng hiện tại, danh sách nhân sự và trạng thái hoạt động. Department là dữ liệu master theo campus; cần kiểm tra trạng thái Active/Inactive và ảnh hưởng đến phân công nhiệm vụ đoàn phái. |
| UC-106 | Manage Department Status | Kích hoạt hoặc vô hiệu hóa bộ phận; bộ phận bị vô hiệu hóa bị loại khỏi phân công nhiệm vụ điều phối đoàn phái. Department là dữ liệu master theo campus; cần kiểm tra trạng thái Active/Inactive và ảnh hưởng đến phân công nhiệm vụ đoàn phái. |
| UC-107 | Add Department Personnel | Thêm người dùng hệ thống hiện có vào bộ phận với tư cách nhân viên; hệ thống gửi thông báo đến người dùng được thêm. Thao tác chỉ thay đổi liên kết nhân sự-bộ phận, không xóa tài khoản; cần kiểm tra nhiệm vụ đang mở trước khi remove/reassign. |
| UC-108 | View Personnel Details | Xem đầy đủ hồ sơ thành viên bộ phận: vai trò, nhiệm vụ được phân công, thông tin liên hệ và lịch sử tham gia sự kiện. Thao tác chỉ thay đổi liên kết nhân sự-bộ phận, không xóa tài khoản; cần kiểm tra nhiệm vụ đang mở trước khi remove/reassign. |
| UC-109 | Search Personnel | Tìm kiếm nhân sự bộ phận theo tên, loại vai trò hoặc trạng thái phân công nhiệm vụ hiện tại. Thao tác chỉ thay đổi liên kết nhân sự-bộ phận, không xóa tài khoản; cần kiểm tra nhiệm vụ đang mở trước khi remove/reassign. |
| UC-110 | Review Assigned Tasks | Department xem tất cả nhiệm vụ được giao, kiểm tra chi tiết nhiệm vụ và cập nhật trạng thái hoàn thành. Nhiệm vụ phải có người phụ trách, deadline, trạng thái và liên kết delegation; cập nhật trạng thái cần thông báo/ghi log. |
| UC-111 | Assign Tasks | Department Lead phân công nhiệm vụ điều phối cụ thể (bố trí phòng, catering, vận chuyển, AV) cho nhân sự bộ phận. Nhiệm vụ phải có người phụ trách, deadline, trạng thái và liên kết delegation; cập nhật trạng thái cần thông báo/ghi log. |
| UC-112 | Sign The Service Delivery Report | Các bên có thẩm quyền ký điện tử báo cáo bàn giao dịch vụ để xác nhận tất cả nhiệm vụ điều phối đã hoàn thành. Chữ ký số/điện tử phải lưu người ký, thời điểm và trạng thái; sau khi đủ chữ ký báo cáo được khóa để làm bằng chứng bàn giao. |
| UC-113 | Remove Personnel | Xóa người dùng khỏi danh sách nhân sự bộ phận; tài khoản người dùng không bị xóa, chỉ xóa liên kết bộ phận. Thao tác chỉ thay đổi liên kết nhân sự-bộ phận, không xóa tài khoản; cần kiểm tra nhiệm vụ đang mở trước khi remove/reassign. |
| UC-114 | View Coordination Tasks | Xem tất cả nhiệm vụ điều phối cho các đoàn phái đang hoạt động được phân công cho bộ phận này. Nhiệm vụ phải có người phụ trách, deadline, trạng thái và liên kết delegation; cập nhật trạng thái cần thông báo/ghi log. |
| UC-115 | Search Coordination Tasks | Tìm kiếm nhiệm vụ điều phối theo ID đoàn phái, người được giao, trạng thái hoàn thành hoặc deadline sắp đến. Nhiệm vụ phải có người phụ trách, deadline, trạng thái và liên kết delegation; cập nhật trạng thái cần thông báo/ghi log. |
| UC-116 | Reassign Department Lead | Department Lead chuyển giao vai trò trưởng bộ phận cho thành viên khác; trưởng cũ tự động chuyển thành Department. Cần đảm bảo người được gán thuộc đúng bộ phận/cơ sở, thông báo cho bên liên quan và ghi log chuyển giao trách nhiệm. |
| UC-117 | View Role List | Xem tất cả vai trò hệ thống đã cấu hình kèm tên và tóm tắt quyền hạn ở cấp độ cao. Chỉ Admin được quản lý role; dữ liệu role phải thể hiện tên, mô tả, trạng thái và tóm tắt quyền. |
| UC-118 | Create New Role | Tạo vai trò hệ thống tùy chỉnh với tên duy nhất; quyền hạn được cấu hình riêng tại UC-118 Configure Role Permissions. Tên vai trò phải duy nhất; role mới nên chưa có quyền mặc định hoặc dùng bộ quyền an toàn, sau đó cấu hình tại UC-118. |
| UC-119 | Configure Role Permissions | Thiết lập quyền hạn chi tiết ở cấp UC (Toàn quyền/Chỉnh sửa/Xem/Cá nhân/Không có) cho từng tính năng theo vai trò. Cần kiểm soát ma trận quyền theo UC/tính năng, ghi log thay đổi và tránh tự khóa quyền Admin khỏi hệ thống. |
| UC-120 | Update Role Details | Chỉnh sửa tên hiển thị hoặc văn bản mô tả của vai trò hệ thống hiện có. Chỉ Admin được quản lý role; dữ liệu role phải thể hiện tên, mô tả, trạng thái và tóm tắt quyền. |
| UC-121 | Disable/Delete Role | Vô hiệu hóa hoặc xóa vai trò; hệ thống xác nhận không có người dùng nào đang được gán vai trò trước khi tiến hành. Không cho xóa/vô hiệu hóa role đang gán cho user hoặc role hệ thống bắt buộc; nên ưu tiên disable để giữ lịch sử. |
| UC-122 | View API Configuration | Xem danh sách tất cả tích hợp API bên ngoài đã cấu hình kèm URL endpoint, trạng thái và thời gian sử dụng gần nhất. Thông tin xác thực phải được mã hóa/che giấu khi hiển thị; cần validate endpoint, method, timeout và dependency trước khi lưu/xóa. |
| UC-123 | Create API Configuration | Đăng ký tích hợp API bên ngoài mới: tên nhà cung cấp, URL endpoint, thông tin xác thực và cài đặt yêu cầu. Thông tin xác thực phải được mã hóa/che giấu khi hiển thị; cần validate endpoint, method, timeout và dependency trước khi lưu/xóa. |
| UC-124 | Update API Configuration | Chỉnh sửa cài đặt tích hợp API hiện có: URL endpoint, thông tin xác thực, giá trị timeout hoặc request header. Thông tin xác thực phải được mã hóa/che giấu khi hiển thị; cần validate endpoint, method, timeout và dependency trước khi lưu/xóa. |
| UC-125 | Delete API Configuration | Xóa cấu hình tích hợp API; hệ thống kiểm tra không có tính năng nào phụ thuộc vào nó trước khi xóa. Thông tin xác thực phải được mã hóa/che giấu khi hiển thị; cần validate endpoint, method, timeout và dependency trước khi lưu/xóa. |
| UC-126 | Test API Connection | Gửi yêu cầu kiểm tra đến endpoint API để xác minh kết nối, tính hợp lệ của token xác thực và định dạng phản hồi. Kết quả test cần hiển thị success/failure, mã lỗi và thời gian phản hồi; không ghi lộ secret vào log. |
| UC-127 | Manage API Status | Bật hoặc tắt tích hợp API mà không xóa cấu hình; API bị tắt bị loại khỏi tất cả lệnh gọi hệ thống. Giới hạn/trạng thái API phải được áp dụng thống nhất cho các job gọi API và có cảnh báo khi gần vượt quota. |
| UC-128 | Configure Request Limit | Đặt giới hạn lượt yêu cầu tối đa cho hệ thống sử dụng API bên ngoài để ngăn vượt quá hạn ngạch chi phí dùng theo tháng. Giới hạn/trạng thái API phải được áp dụng thống nhất cho các job gọi API và có cảnh báo khi gần vượt quota. |
| UC-129 | View API Logs | Xem nhật ký lịch sử tất cả yêu cầu API gửi đi: endpoint, phương thức HTTP, mã trạng thái, thời gian phản hồi và timestamp. Log phải hỗ trợ lọc, không lưu lộ token/secret trong plain text và có thời gian phản hồi/mã lỗi để phục vụ giám sát. |
| UC-130 | Search API Logs | Tìm kiếm và lọc nhật ký API theo URL endpoint, mã trạng thái HTTP, khoảng thời gian hoặc ngưỡng thời gian phản hồi. Log phải hỗ trợ lọc, không lưu lộ token/secret trong plain text và có thời gian phản hồi/mã lỗi để phục vụ giám sát. |
| UC-131 | Create Agenda Template | Tạo mẫu agenda theo `visit_type` (`CAMPUS_TOUR/MEETING/WORKSHOP/SIGNING_CEREMONY/EXCHANGE/OTHER`) và phạm vi (GLOBAL do HO, hoặc theo campus do Staff Leader). Mỗi mục dùng `start_offset_minutes` + `duration_minutes` (KHÔNG dùng giờ tuyệt đối), kèm tiêu đề, mô tả, địa điểm, vai trò phụ trách mặc định. Module dùng 4 bảng: `agenda_templates`, `agenda_template_items`, `agenda_template_defaults`, `visit_agendas`. |
| UC-132 | Update Agenda Template | Chỉnh sửa mẫu agenda (header + danh sách mục theo offset/duration). Full-replace danh sách mục; không tự động sửa `visit_agendas` đã sinh ra cho đoàn đang xử lý. Phân quyền theo scope (HO=GLOBAL, Staff Leader=campus mình). |
| UC-133 | Delete Agenda Template | Soft delete mẫu agenda (set `deleted_at/deleted_by`). Chặn xóa nếu mẫu đang là default của `(scope, visit_type)` — phải đổi default trước. Mẫu đã xóa bị loại khỏi list/default. |
| UC-134 | View Agenda Template List | Danh sách mẫu theo scope của người dùng (HO thấy tất cả; Staff Leader thấy GLOBAL + campus mình), kèm `visit_type`, phạm vi, trạng thái, số mục và cờ "mặc định". Hỗ trợ lọc theo `visit_type`/campus/status. |
| UC-135 | View Agenda Template Detail | Chi tiết một mẫu agenda: toàn bộ mục theo offset/duration, địa điểm, vai trò phụ trách, cờ đã-xóa/mặc-định. Màn hình xem/kiểm tra. |
| — | Manage Agenda Template Default | Đặt/sửa mẫu mặc định theo `(campus_scope_key, visit_type)` trong `agenda_template_defaults`. Default phải cùng `visit_type` và cùng scope với template, và template phải `ACTIVE`/chưa xóa. HO quản lý GLOBAL; Staff Leader quản lý campus mình. |
| — | Apply Agenda Template (setup agenda) | Host mở setup cho một campus instance: hệ thống tự chọn default theo **campus + visit_type**, fallback **GLOBAL + visit_type**; host có thể chọn mẫu khác (kể cả khác `visit_type`). Apply tính `start_time = visit_request_campuses.planned_start_at + start_offset_minutes`, `end_time = start_time + duration_minutes` và ghi `visit_agendas` (DATETIME đầy đủ ngày + giờ), `source_template_item_id` trỏ về template item. `replaceExisting=false` + đã có agenda → 409; chỉ Host trong giai đoạn `ASSIGNED/BEFORE_VISIT`. `visit_request_campuses` KHÔNG bị ghi và KHÔNG lưu template đã apply. |
| UC-136 | Cancel Visit Request | Hủy yêu cầu thăm/đoàn khách thuộc FE-02 Delegation Reception Management. Visitor được hủy đơn của chính mình khi chưa bước vào giai đoạn đang diễn ra/hậu xử lý/đã đóng. Host được hủy campus instance mình phụ trách nếu khách đã xác nhận hủy qua kênh ngoài hệ thống; khi đó `cancellation_source = EXTERNAL_CONFIRMATION` và `cancellation_reason` phải ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. Staff Leader được hủy trong phạm vi campus của mình theo rule nghiệp vụ. HO chỉ xử lý hủy `MULTI_CAMPUS`; nếu HO nhìn thấy `SINGLE_CAMPUS` thì chỉ được xem read-only, không có nút hủy/thao tác. Admin không có quyền hủy visit/delegation nghiệp vụ. Không dùng `external_confirmation_note`. |

---

## Ghi chú

- Tài liệu này chỉ dùng để lưu phần ghi chú nghiệp vụ/triển khai cho từng Use Case.
- Chưa xác nhận các cột FT, Actor, Priority, Status, Precondition, Postcondition nên không đưa vào file này.
- Khi các trường còn lại được kiểm định, có thể tạo file UC specification đầy đủ riêng.


---

## SQL v8.2 Scope Notes

- `visit_requests` bắt đầu từ `PENDING_APPROVAL` vì OTP/email verification hoàn tất trước khi insert DB.
- Không dùng bảng `pending_visit_requests`; frontend có thể giữ form tạm bằng state/sessionStorage có TTL ngắn.
- `users.created_via` gồm `MANUAL_CREATED`, `VISITOR_FORM`, `SSO_AUTO_PROVISION`.
- ADMIN là technical admin, không xem visit/delegation nghiệp vụ.
- HO xem được cả `MULTI_CAMPUS` và `SINGLE_CAMPUS`; chỉ `MULTI_CAMPUS` được duyệt/từ chối/hủy/xử lý, còn `SINGLE_CAMPUS` là read-only/monitoring và không có action xử lý.
- Staff Leader chỉ xem/xử lý `SINGLE_CAMPUS` thuộc campus mình; `MULTI_CAMPUS` chỉ thấy sau khi HO duyệt/release và chỉ trong campus của mình.
- Backend list/detail/search phải dùng đúng visibility query/view; frontend ẩn menu/nút không đủ để bảo mật.
- Với HO, backend phải tách rõ `viewScope` và `actionScope`: HO có `viewScope` với cả `SINGLE_CAMPUS`/`MULTI_CAMPUS`, nhưng `actionScope` chỉ áp dụng cho `MULTI_CAMPUS`. Mọi response cho `SINGLE_CAMPUS` của HO phải là read-only, không trả `allowedActions` xử lý.
- UC-136 dùng `cancellation_reason` cho cả xác nhận ngoài hệ thống và lý do nội bộ; không tạo/không dùng `external_confirmation_note`.

---

# Reference Notes — UC-136 Cancellation Flow


## V8.2 Reference — UC-136 Cancel Visit Request thuộc Delegation Reception Management

> Phần này là ghi chú triển khai bổ sung cho UC-136. UC-136 đã được đưa vào bảng chính ở đầu file. Nếu tài liệu cũ còn flow “đã duyệt nhưng chưa có host” hoặc “mỗi cơ sở duyệt lại sau HO”, ưu tiên rule V8.2 ở phần này.

### 1. Feature ownership

UC hủy đơn thăm thuộc **FE-02 — Quản lý Tiếp đón Đoàn khách / Delegation Reception Management** vì đây là thao tác trên vòng đời đoàn/visit request, không phải bước submit form.

```text
Feature: FE-02 Delegation Reception Management
UC: UC-136 Cancel Visit Request
Permission code: UC-136.CANCEL_VISIT_REQUEST
```

### 2. Không dùng `external_confirmation_note`

Không tạo cột `external_confirmation_note`. Khi Host hủy thay khách dựa trên xác nhận ngoài hệ thống, toàn bộ thông tin xác nhận được ghi vào `cancellation_reason`.

```text
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason = "Khách xác nhận hủy qua email/điện thoại/Zalo..., thời gian..., người xác nhận..., lý do..."
```

### 3. Cancellation metadata chuẩn

Áp dụng cho `visit_requests` và `visit_request_campuses`:

```sql
cancelled_by BIGINT UNSIGNED NULL,
cancelled_at DATETIME NULL,
cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL,
cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION') NULL,
cancellation_reason TEXT NULL
```

### 4. Meaning của `cancellation_source`

| Value | Meaning | Khi dùng |
|---|---|---|
| `SELF_SERVICE` | Người dùng tự thao tác trên hệ thống | Visitor tự hủy đơn của chính họ |
| `EXTERNAL_CONFIRMATION` | Hủy dựa trên xác nhận ngoài hệ thống | Host hủy thay khách sau khi khách xác nhận qua email/điện thoại/Zalo/gặp trực tiếp |
| `INTERNAL_DECISION` | Nội bộ hủy vì lý do vận hành | HO/Staff Leader hủy vì campus không thể tiếp, trùng lịch, lý do tổ chức |

### 5. Rule hủy theo role

| Actor | Scope | Nguồn hủy hợp lệ | Ghi chú |
|---|---|---|---|
| Visitor | Đơn của chính họ | `SELF_SERVICE` | Chỉ hủy khi chưa vào giai đoạn `DURING_VISIT`, `AFTER_VISIT`, `CLOSED` |
| Host | Campus instance mình đang phụ trách | `EXTERNAL_CONFIRMATION` | Bắt buộc nhập `cancellation_reason` rõ kênh/thời điểm/người xác nhận |
| Staff Leader | Đơn/campus thuộc campus mình | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Không xử lý campus khác |
| HO | `MULTI_CAMPUS` | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Có thể hủy request tổng liên cơ sở nếu nghiệp vụ cho phép |
| Admin | Không có quyền nghiệp vụ visit/delegation | Không áp dụng | ADMIN không được hủy delegation |

### 6. Rule trạng thái

- `visit_requests.status = CANCELLED` dùng khi hủy request/delegation tổng.
- `visit_request_campuses.status = CANCELLED` dùng khi hủy một campus instance.
- Không cho hủy campus instance nếu đã vào `DURING_VISIT`, `AFTER_VISIT`, hoặc `CLOSED`.
- Không dùng `CANCELLED` thay cho `REJECTED`. Nếu đơn đang `PENDING_APPROVAL` và người duyệt không chấp nhận, dùng reject flow.

### 7. Vị trí code Clean Architecture

```text
PEMS.Application/Delegations/Commands/CancelVisitRequest/
├── CancelVisitRequestCommand.cs
├── CancelVisitRequestCommandHandler.cs
├── CancelVisitRequestCommandValidator.cs
└── CancelVisitRequestResponse.cs
```

Controller chỉ nhận request và gọi `IMediator`. Logic kiểm tra scope, current host, request/campus status, và cancellation metadata nằm trong Handler/Domain Entity.


## UC-136 Detail Note

UC-136 — Cancel Visit Request: Hủy yêu cầu thăm/đoàn khách thuộc FE-02 Delegation Reception Management. Visitor được hủy đơn của chính mình khi chưa bước vào giai đoạn đang diễn ra/hậu xử lý/đã đóng. Host được hủy campus instance mình phụ trách nếu khách đã xác nhận hủy qua kênh ngoài hệ thống; khi đó `cancellation_source = EXTERNAL_CONFIRMATION` và `cancellation_reason` phải ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do. Staff Leader được hủy trong phạm vi campus của mình theo rule nghiệp vụ. HO chỉ xử lý hủy `MULTI_CAMPUS`; nếu thấy `SINGLE_CAMPUS` thì chỉ xem read-only và không có action hủy/thao tác. Admin không có quyền hủy visit/delegation nghiệp vụ. Không dùng `external_confirmation_note`.

## Related UC Clarification Notes

- **UC-17 Submit Visit Request:** Submit form xong mới tạo request. Hủy sau submit không thuộc UC-17, mà thuộc UC-136.
- **UC-18 (LEGACY — HO không còn duyệt):** duyệt là việc của Staff Leader từng campus. Giữ mục này để đối chiếu lịch sử. Nội dung cũ: HO duyệt/từ chối multi-campus; nếu cần hủy sau khi đã duyệt thì dùng UC-136.
- **UC-22 Process Visit Request:** Staff Leader xử lý approve/reject single-campus; nếu hủy sau khi đã duyệt thì dùng UC-136.
- **UC-41 Close Delegation:** Close Delegation là đóng hồ sơ sau khi hoàn tất, không phải hủy.
