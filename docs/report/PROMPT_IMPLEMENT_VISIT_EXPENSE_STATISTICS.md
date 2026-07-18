# PROMPT TRIỂN KHAI THỐNG KÊ CHI PHÍ TIẾP ĐÓN ĐOÀN

## Vai trò

Bạn là Senior Full-stack Engineer phụ trách dự án PEMS (.NET, MySQL, frontend hiện có).

Hãy đọc toàn bộ codebase liên quan trước khi sửa, đặc biệt:

- Luồng `visit_request_campuses` và form v2 theo từng campus instance.
- Trạng thái `AFTER_VISIT`/`CLOSED`.
- Trang “Sau tiếp khách” của Host.
- `visit_logistics_items` và `visit_logistics_item_handovers` của Department.
- Hai trang report hiện tại của Staff Leader và Department Leader.
- Logic xuất PDF, gửi email, phân quyền, audit, validation, API response và test hiện có.

Không đoán tên folder/class/API. Phải tìm implementation thật rồi mở rộng đúng kiến trúc, convention và DI hiện tại. Không phá v1/v2, không làm lộ dữ liệu campus khác và không sửa chức năng ngoài phạm vi.

## Database đầu vào

Database full mới đã bổ sung ba bảng:

- `visit_expense_reports`
- `visit_expense_items`
- `visit_expense_report_events`

Không tạo một hệ thống bảng khác trùng mục đích. Không lưu tiền trực tiếp vào `visit_logistics_items`, `visit_logistics_item_handovers`, `minutes` hoặc `visit_request_campuses`.

Ý nghĩa:

- `report_scope = GENERAL`: chi phí chung do Host nhập tại “Sau tiếp khách”.
- `report_scope = LOGISTICS`: chi phí của Department theo một `logistics_item_id`.
- `item_origin = REQUEST_ITEM`: dòng khởi tạo từ đơn hậu cần.
- `MANUAL`: Host tự nhập.
- `ADDITIONAL`: khoản phát sinh ngoài nội dung ban đầu.
- `DAMAGE_LOSS`: hỏng/mất, ví dụ chén vỡ.
- `total_amount` là generated column `quantity × unit_price`; client không được gửi hoặc tin tưởng thành tiền tự tính.

## Mục tiêu nghiệp vụ

Thay khái niệm “hóa đơn” hiện tại bằng **Thống kê chi phí tiếp đón**. Đây là bảng kê/biên lai nội bộ, không phải hóa đơn VAT.

Một campus instance có:

- Tối đa một bảng chi phí `GENERAL`.
- Tối đa một bảng `LOGISTICS` cho mỗi đơn hậu cần.
- Biên lai tổng hợp của đoàn bằng tổng mọi dòng `GENERAL` và `LOGISTICS` thuộc cùng `visit_instance_id`.

Không tính tiền chỉ dựa trên số đơn yêu cầu. Một đoàn không có đơn hậu cần vẫn được Host nhập chi phí chung; đơn hậu cần có thể có thêm khoản phát sinh.

## 1. Nhập chi phí tại Department

### Vị trí

Trong màn hình xử lý/ký biên bản bàn giao của từng `visit_logistics_item`, bổ sung khu vực **Ghi chú chi phí** ở giai đoạn sau tiếp khách.

### Điều kiện

- Chỉ Department Leader hoặc Department Staff thuộc đúng `requested_to_department_id` được xem/sửa bảng hậu cần đó.
- Chỉ cho sửa khi đơn hậu cần đã hoàn thành theo workflow thật và campus instance ở `AFTER_VISIT`.
- Nếu loại tài sản cần trả, chỉ mở nhập chính thức sau khi ký trả hợp lệ. Với vật tư tiêu hao/dịch vụ không có bước trả, dùng trạng thái `DONE` làm gate.
- Khi instance `CLOSED`, chỉ được xem và xuất; không sửa trực tiếp.
- Backend phải kiểm tra `logistics_item_id`, `visit_instance_id` và `department_id` thật sự thuộc cùng một đơn. Không tin ID do frontend gửi.

### Bảng nhập

Hiển thị bảng nhỏ:

| STT | Tên khoản chi | Số lượng | Đơn vị | Đơn giá | Thành tiền | Ghi chú | Hành động |
|---|---|---:|---|---:|---:|---|---|

Khi mở lần đầu:

- Tạo/lấy `LOGISTICS` report idempotently.
- Khởi tạo đúng một dòng `REQUEST_ITEM` từ `visit_logistics_items.title` và số lượng cuối cùng đã được chấp nhận; nếu có `proposed_quantity` đã được ACCEPTED thì dùng số lượng đó, nếu không dùng `quantity`.
- Không tự đặt đơn giá; người dùng nhập giá thực tế.
- Không tạo trùng dòng khi refresh hoặc gọi đồng thời.

Department có thể bấm dấu `+` để thêm:

- `ADDITIONAL`, ví dụ phát sinh thêm tea break.
- `DAMAGE_LOSS`, ví dụ “Chén vỡ”, số lượng 2, đơn giá bồi thường.
- `OTHER` khi không thuộc hai loại trên.

Cho phép thêm/sửa/xóa dòng khi report chưa chốt. Có ô `report_note` ghi chú chung. Nút **Lưu chi phí** phải lưu header và danh sách item trong một transaction, tăng `row_version`, ghi event `SAVED`/`UPDATED` và chống lost update.

Không cho số lượng `<= 0`, đơn giá `< 0`, tên rỗng hoặc giá vượt giới hạn `DECIMAL(18,2)`. Format tiền VND ở frontend nhưng gửi số thuần cho API.

## 2. Nhập chi phí chung tại Host

### Vị trí

Tại mục **Sau tiếp khách** của campus instance, bổ sung card **Ghi chú chi phí tiếp đón**.

### Quyền

- Chỉ `current_host_user_id` của instance được sửa, bất kể tài khoản đó là Staff hay Staff Leader đang làm Host.
- Staff/Staff Leader không phải Host không được sửa chỉ vì có cùng role/campus.
- Staff Leader có quyền report chỉ được xem thống kê theo campus, không được sửa thay Host nếu không có policy hiện hữu cho phép rõ ràng.
- Chỉ sửa ở `AFTER_VISIT`; `CLOSED` chỉ xem/xuất.

### Bảng nhập

Dùng cùng component bảng chi phí của Department nhưng mặc định các dòng là `MANUAL`. Host tự nhập tên, số lượng, đơn vị, đơn giá và ghi chú; có thể thêm/xóa dòng.

Đây là `GENERAL` report, không gắn `logistics_item_id` và `department_id`. Không sao chép các dòng hậu cần vào GENERAL vì khi tổng hợp sẽ bị tính trùng.

Trong card có bộ lọc hiển thị:

- **Chung**: chỉ item của GENERAL report.
- **Hậu cần**: item LOGISTICS do các Department đã lưu, chỉ đọc với Host.
- **Tất cả**: hợp nhất hai nhóm, có subtotal từng nhóm và tổng cộng.

Nút **Lưu chi phí** chỉ lưu phần GENERAL do Host sở hữu. Nút **Xuất thống kê** xuất biên lai tổng hợp hiện tại của chính đoàn.

## 3. Đổi hai trang report thành “Thống kê chi phí”

Đổi toàn bộ label/title/breadcrumb/nút liên quan từ “Hóa đơn”, “Báo cáo hóa đơn” hoặc tên report cũ sang **Thống kê chi phí** hoặc **Xuất thống kê chi phí**. Không dùng từ “hóa đơn” trong UI/PDF mới.

### 3.1. Trang report của Staff Leader

Giữ nguyên phần 1 và phần 2 nếu không thuộc phạm vi.

#### Phần 3 — Báo cáo phòng ban khác

Ở cột **Hành động**:

- Bỏ nút **Xuất hóa đơn**.
- Thay bằng chức năng/nút **Ghi chú** và **Gửi mail**, có giao diện và hành vi giống chính xác phần 2 “Nhân sự” hiện có.
- Tái sử dụng component/service/modal/email flow của phần 2 thay vì copy logic mới.
- Recipient và quyền phải lấy theo phòng ban của đúng dòng; không gửi nhầm campus/phòng ban.

#### Phần 4 — Thống kê chi phí

Thêm section/card mới số 4 tên **Thống kê chi phí**.

Giữ kiểu thao tác hiện có:

- Từ ngày, đến ngày.
- Nút **Tải danh sách**.
- Danh sách các đoàn thuộc campus mà Staff Leader được phép xem.
- Ngày lọc theo ngày tiếp đón của `visit_request_campuses`, không theo ngày tạo expense.
- Scope-before-filter/search: kiểm tra campus trước rồi mới lọc ngày/từ khóa.

Danh sách nên có tối thiểu:

- STT.
- Mã đoàn/đơn.
- Tên đoàn lấy v2-safe theo campus instance.
- Ngày tiếp đón.
- Chi phí chung.
- Chi phí hậu cần.
- Tổng chi phí.
- Trạng thái ghi nhận.
- Hành động **Xem chi tiết**.

Không hiển thị mỗi đơn hậu cần thành một đoàn riêng.

Khi bấm **Xem chi tiết**:

- Không mở biên bản bàn giao.
- Mở **Biên lai chi phí tiếp đón** của đoàn.
- Hợp nhất GENERAL của Host và toàn bộ LOGISTICS đã lưu của các Department thuộc đúng instance.

### 3.2. Trang report của Department Leader

- Đổi tên trang thành **Thống kê chi phí**.
- Chỉ hiển thị dữ liệu `LOGISTICS` của đúng department và campus mà Department Leader quản lý.
- Giữ lọc thời gian và tải danh sách.
- Danh sách nhóm theo đoàn/campus instance, không lặp đoàn thành nhiều dòng chỉ vì có nhiều logistics item.
- Xem chi tiết hiển thị các đơn hậu cần và khoản phát sinh của department đó.
- Department Leader không được thấy GENERAL hoặc chi phí của department khác, trừ khi policy hiện tại quy định rõ quyền rộng hơn.
- Có nút xuất thống kê chi phí trong phạm vi department.

## 4. Thiết kế biên lai chi phí

Thiết kế modal/drawer/PDF chuyên nghiệp, gọn và dễ đọc; không tạo một bảng ngang quá rộng hoặc trang kéo quá dài.

Header:

- Logo/tên hệ thống theo template hiện có.
- Tiêu đề **BIÊN LAI CHI PHÍ TIẾP ĐÓN**.
- Mã đoàn, tên đoàn, campus, thời gian tiếp đón, Host.

Nội dung:

1. **Chi phí chung**: các dòng Host nhập và subtotal.
2. **Chi phí hậu cần**: nhóm theo phòng ban hoặc đơn hậu cần, có subtotal từng nhóm.
3. **Tổng cộng**: tổng GENERAL + LOGISTICS, định dạng VND.
4. Ghi chú chung cần thiết.

Các nhóm không có dữ liệu có thể ẩn hoặc hiển thị “Chưa ghi nhận chi phí”. Không hiển thị `CANCELLED` report trong tổng mặc định.

Modal nên có vùng header cố định, nội dung cuộn bên trong và footer tổng tiền/nút hành động cố định. Trên mobile chuyển bảng thành card hoặc cho cuộn ngang có kiểm soát.

## 5. Xuất PDF thống kê

### Xuất một đoàn

PDF phải chứa đúng biên lai của campus instance đang xem và số liệu snapshot nhất quán tại thời điểm xuất.

### Xuất tổng theo khoảng thời gian

Nút xuất tổng của Staff Leader phải tạo một PDF gồm:

- Trang tổng hợp các đoàn trong khoảng ngày và tổng chi phí từng đoàn.
- Tổng cộng của tất cả đoàn.
- Sau phần tổng hợp là biên lai chi tiết của từng đoàn.
- Mỗi đoàn bắt đầu ở trang/khối rõ ràng; không lặp header quá lớn và không để bảng bị cắt khó đọc.

Department Leader xuất cùng cấu trúc nhưng chỉ trong phạm vi department.

Ghi event `EXPORTED` cho report liên quan. Không đánh dấu report đã gửi email chỉ vì export.

## 6. Loại bỏ gửi hóa đơn qua email

- Bỏ nút **Gửi hóa đơn qua email** khỏi phần thống kê chi phí.
- Không tạo API gửi hóa đơn/biên lai tự động mới.
- Chức năng **Gửi mail** tại phần 3 Staff Leader là flow email/ghi chú sẵn có giống phần 2, không phải gửi hóa đơn.
- Không làm hỏng email flow khác trong hệ thống.

## 7. API và transaction

Thiết kế endpoint theo convention thật của project. Tối thiểu cần use case tương đương:

- Lấy/tạo idempotent GENERAL report của instance.
- Lấy/tạo idempotent LOGISTICS report của logistics item.
- Lưu danh sách item và ghi chú trong một transaction.
- Lấy biên lai tổng hợp của một instance.
- Lấy danh sách thống kê theo ngày cho Staff Leader.
- Lấy danh sách thống kê theo ngày cho Department Leader.
- Xuất PDF một đoàn và xuất PDF tổng hợp.

Mọi command phải authorize lại ở backend. Không nhận `department_id`, campus scope, Host ownership hoặc tổng tiền từ frontend làm nguồn sự thật. Tổng tiền đọc bằng `SUM(visit_expense_items.total_amount)`.

Khi tạo LOGISTICS report, backend tự lấy `visit_instance_id` và `requested_to_department_id` từ `visit_logistics_items`. Khi tạo GENERAL report, backend tự xác định Host từ `visit_request_campuses.current_host_user_id`.

Xử lý unique conflict do hai request đồng thời theo hướng idempotent: đọc lại bản ghi thắng cuộc, không trả 500 và không tạo report/dòng khởi tạo trùng.

## 8. Quy tắc đọc dữ liệu v2

- Tên đoàn và nội dung form phải dùng read service/projection v2-safe hiện có.
- Với multi-campus, mỗi `visit_instance_id` có biên lai và chi phí riêng.
- Không fallback về global request fields đối với v2 non-mixed nếu convention hiện tại cấm fallback.
- Không để Staff Leader campus A hoặc Department campus A đọc chi phí campus B.

## 9. Validation và trạng thái

- `DRAFT`: mới tạo, chưa lưu chính thức.
- `SAVED`: đã lưu và được tính trong report mặc định.
- `FINALIZED`: đã chốt, chỉ đọc; mở lại phải có quyền và ghi event `REOPENED`.
- `CANCELLED`: không tính trong tổng mặc định.
- Không cho sửa item của report FINALIZED/CANCELLED.
- Dùng `row_version`; conflict trả HTTP 409 theo error format hiện có.
- Lưu item và event trong cùng transaction; rollback toàn bộ nếu một dòng sai.

## 10. Test bắt buộc

### Backend/integration

- Host tạo và cập nhật GENERAL report thành công ở AFTER_VISIT.
- Staff/Staff Leader không phải Host không sửa được GENERAL.
- Department đúng đơn tạo LOGISTICS report; department khác bị 403/404 theo security convention.
- LOGISTICS report tự lấy đúng instance/department từ logistics item.
- Dòng REQUEST_ITEM chỉ tạo một lần khi replay/concurrent request.
- Số lượng × đơn giá cho kết quả đúng, bao gồm số thập phân và số tiền lớn hợp lệ.
- Không nhận total giả từ client.
- Tổng đoàn bằng GENERAL + tất cả LOGISTICS SAVED/FINALIZED, loại CANCELLED.
- Multi-campus không trộn chi phí giữa các instance.
- Scope được kiểm tra trước filter/search.
- CLOSED chỉ đọc/xuất; FINALIZED không sửa.
- Row-version conflict trả 409 và không ghi đè.
- Transaction rollback không để item/event mồ côi.
- PDF một đoàn và PDF tổng có đúng scope, tổng tiền và thứ tự đoàn.

### Frontend

- Add/edit/delete dòng và tính preview thành tiền.
- Filter Chung/Hậu cần/Tất cả không tính trùng.
- Department thêm khoản phát sinh/DAMAGE_LOSS.
- Staff Leader section 3 không còn Xuất hóa đơn; có Ghi chú + Gửi mail như phần 2.
- Section 4 tải danh sách theo ngày và mở đúng biên lai, không mở handover.
- Department Leader chỉ thấy chi phí của phòng mình.
- Không còn nút Gửi hóa đơn qua email.
- Responsive và trạng thái loading/empty/error/409 rõ ràng.

Chạy lại toàn bộ unit, integration, architecture test, frontend typecheck và production build. Không chỉ chạy test mới.

## 11. Yêu cầu bàn giao

- Báo cáo danh sách file đã sửa theo Backend/Frontend/SQL/Test.
- Nêu API và rule phân quyền đã triển khai.
- Nêu rõ report lấy tên đoàn theo v2-safe path nào.
- Cung cấp kết quả test/build thực tế, không khẳng định khi chưa chạy.
- Không sửa dữ liệu/feature ngoài phạm vi.
- Không thêm AI/Claude/Co-authored-by attribution vào commit.
- Gom commit theo lát cắt chức năng hợp lý; tránh một commit vụn chỉ chứa một thay đổi nhỏ nếu có thể gộp an toàn.

## Acceptance Criteria cuối

1. Department ghi được chi phí gốc và phát sinh sau xử lý hậu cần.
2. Host ghi được chi phí chung tại Sau tiếp khách.
3. Dữ liệu chung và hậu cần không trùng, không lẫn campus/department.
4. Hai trang report được đổi thành Thống kê chi phí đúng quyền.
5. Staff Leader phần 3 có Ghi chú + Gửi mail, không còn Xuất hóa đơn.
6. Staff Leader có phần 4 hiển thị đoàn, tổng tiền và biên lai chi tiết.
7. Xem chi tiết không còn mở biên bản bàn giao.
8. PDF tổng có bảng tổng hợp và biên lai từng đoàn, bố cục gọn.
9. Không còn nút Gửi hóa đơn qua email.
10. Toàn bộ regression test và build xanh.
