# PROMPT — Chuyển i18n động sang Database và dịch một lần khi lưu

Hãy đọc kỹ codebase PEMS và file SQL full mới nhất trước khi sửa. Không chỉ lập kế hoạch; hãy trực tiếp cập nhật backend, frontend và entity để khớp schema mới.

## Mục tiêu

### 1. Đồng bộ Entity/EF với database mới
- Cập nhật entity, `DbSet`, Fluent Configuration, mapper và DTO cho các bảng/cột i18n mới, đặc biệt:
  - `faq_translations`
  - `partner_translations`
  - `news_translations` hiện hữu
  - các cột song ngữ Gallery đã thêm trong SQL
- Thiết lập đúng FK, unique key `(entity_id, language_code)`, kiểu dữ liệu, độ dài và cascade theo SQL.
- Không tạo migration/schema khác với file SQL full mới nhất.

### 2. Public pages không gọi API dịch
Sửa toàn bộ Homepage/Home, FAQ, News, Partner và Search:

- Text giao diện cố định dùng `react-i18next`.
- Nội dung động lấy trực tiếp từ database theo `languageCode=vi|en`.
- Backend join bảng translation tương ứng và fallback `en → vi` nếu thiếu bản EN.
- Search phải tìm trên nội dung đúng ngôn ngữ đang chọn.
- Tuyệt đối không gọi Google Translation API khi tải trang, đổi ngôn ngữ, tìm kiếm, phân trang hoặc mở chi tiết.

### 3. Cơ chế tạo/sửa FAQ, News, Partner
Áp dụng cùng một UX cho cả ba module:

- Mặc định form chỉ hiển thị tab/cột **Tiếng Việt**.
- Có icon/nút **EN**.
- Chỉ khi bấm EN mới gọi Translation API **một lần cho toàn bộ nội dung hiện tại**, sau đó mở giao diện VI–EN song song.
- Bản EN sinh ra phải cho phép sửa thủ công.
- Không debounce và không dịch theo từng ký tự/trường.
- Không ghi đè nội dung EN đã sửa tay, trừ khi người dùng bấm rõ **Dịch lại từ tiếng Việt**.
- Khi bấm **Lưu**:
  - Nếu chưa từng mở/generate EN: backend tự dịch toàn bộ đúng một lần rồi lưu cả VI và EN.
  - Nếu EN đã được generate hoặc sửa: lưu nguyên VI và EN, không gọi dịch lại.
- Khi sửa dữ liệu cũ, tải cả hai bản dịch từ database; nếu thiếu EN thì chỉ dịch khi người dùng bấm EN hoặc khi lưu.

### 4. Quy tắc lưu dữ liệu
- FAQ lưu vào `faqs` và `faq_translations`.
- Partner lưu dữ liệu chung vào `partners`, nội dung ngôn ngữ vào `partner_translations`.
- News tiếp tục dùng kiến trúc `news_translations` hiện hữu.
- Lưu VI và EN trong transaction; nếu dịch lỗi, trả lỗi rõ ràng và không tạo dữ liệu nửa chừng.
- Không lưu raw credential, access token hoặc nội dung request dịch vào log.

### 5. Phạm vi và kiểm tra
- Không làm thay đổi workflow nghiệp vụ, quyền, route công khai hoặc cấu hình Google API hiện có.
- Không xóa dữ liệu/schema cũ.
- Giữ tương thích với dữ liệu seed VI–EN trong SQL mới.
- Chạy build backend và frontend, sửa hết lỗi compile/type.
- Báo rõ file đã sửa và luồng mới của FAQ, News, Partner, Homepage và Search.
- Commit gọn theo chức năng; không ghi Claude, AI hoặc tên công cụ trong commit message.
