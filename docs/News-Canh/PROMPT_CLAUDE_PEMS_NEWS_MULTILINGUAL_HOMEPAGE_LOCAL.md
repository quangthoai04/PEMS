# PROMPT CHO CLAUDE CODE — SỬA NEWS VÀ DỊCH NGÔN NGỮ PEMS 

Hãy kiểm tra code thực tế của dự án PEMS, sau đó triển khai các yêu cầu dưới đây ngay trên nhánh hiện tại. Không tự checkout, chuyển nhánh hoặc tạo nhánh mới.

## Yêu cầu

1. **Staff tạo tin không bắt buộc chọn đoàn**
   - Với Staff thường (`role_code = STAFF`, `sub_role = STAFF`), cho phép tạo tin tức mà không cần chọn đoàn/chuyến tiếp khách.
   - Nếu Staff muốn gắn tin với một đoàn thì vẫn cho phép chọn như hiện tại.

2. **Student vẫn bắt buộc chọn đoàn**
   - Với `role_code = STUDENT`, khi tạo tin bắt buộc phải chọn đoàn.
   - Backend phải kiểm tra Student có thật sự được phân công/tham gia đoàn đó hay không; không chỉ kiểm tra ở frontend.
   - Student không được publish tin trực tiếp, giữ nguyên quy trình duyệt hiện tại.

3. **Tạo tin từ trang Đóng góp đoàn**
   - Khi Student bấm nút **Tạo tin tức** trong trang **Đóng góp đoàn**, chuyển thẳng sang form tạo tin.
   - Đoàn đang xem phải được chọn sẵn trong form.
   - Khi tải lại trang vẫn giữ đúng đoàn đã chọn; backend vẫn phải kiểm tra quyền truy cập đoàn.

4. **Trình soạn tin song ngữ Việt–Anh**
   - Thêm phần chọn ngôn ngữ khi tạo/sửa tin.
   - Khi chọn English, hiển thị hai cột song song:
     - Bên trái: nội dung tiếng Việt.
     - Bên phải: nội dung tiếng Anh.
   - Áp dụng cho tiêu đề, mô tả ngắn, tiêu đề từng mục và nội dung từng mục.
   - Trên màn hình nhỏ có thể xếp hai phần theo chiều dọc.

5. **Dịch tự động an toàn**
   - Tái sử dụng Google Cloud Translation service và cấu hình API hiện có của dự án; không gọi Google trực tiếp từ frontend và không hard-code credential.
   - Dịch sau khi người dùng ngừng gõ một khoảng ngắn bằng debounce.
   - Hủy hoặc bỏ qua response cũ để bản dịch cũ không ghi đè kết quả mới.
   - Nếu người dùng đã sửa nội dung tiếng Anh thủ công thì các thay đổi tiếp theo ở tiếng Việt không được tự động ghi đè phần tiếng Anh đó.
   - Có nút **Dịch lại** để người dùng chủ động tạo lại bản dịch.

6. **Lưu bản dịch đúng kiến trúc hiện tại**
   - Tái sử dụng kiến trúc News multilingual, DTO, command/handler và bảng dịch hiện có.
   - Không tạo một cơ chế lưu bản dịch mới nếu dự án đã có sẵn.
   - Tin cũ chỉ có tiếng Việt vẫn phải xem và chỉnh sửa bình thường.

7. **Tối đa 10 ảnh/video cho mỗi mục nội dung**
   - Thay giới hạn hiện tại tối đa 1 ảnh/mục thành tối đa tổng cộng 10 ảnh hoặc video cho mỗi mục nội dung.
   - Cho phép chọn nhiều file, xem trước và xóa file.
   - Frontend hiển thị giới hạn nhưng backend bắt buộc phải kiểm tra lại giới hạn 10 file.
   - File thứ 11 phải bị từ chối.
   - Giữ nguyên quy tắc dung lượng, định dạng file và storage hiện có; chỉ mở rộng khi thật sự cần.

8. **Đồng bộ ngôn ngữ toàn bộ public site**
   - Nút chọn Vietnamese/English trên public header phải hoạt động thống nhất cho:
     - Homepage.
     - News.
     - FAQ.
     - Partner/Đối tác.
     - Search và kết quả tìm kiếm.
   - Giữ ngôn ngữ đã chọn khi chuyển trang và reload.
   - Text cố định của giao diện dùng i18n/resource hiện có.
   - Nội dung động ưu tiên bản dịch đã lưu; nếu chưa có thì dùng backend translation fallback có cache.
   - Cache/API query phải tách riêng theo locale để không hiển thị nhầm tiếng Việt khi đang chọn English.

9. **FAQ tiếng Anh**
   - FAQ trong database vẫn chỉ lưu tiếng Việt theo business rule hiện tại.
   - Không thêm lại `faqs.language_code` và không sửa nội dung FAQ gốc.
   - Khi người dùng chọn English, dịch `question` và `answer` ở backend/runtime, sau đó cache kết quả để tránh gọi Google lại liên tục.


Sau khi hoàn thành, hãy trả lời ngắn gọn:

1. Các chức năng đã sửa.
2. Danh sách file đã thay đổi.
3. Database/API có thay đổi gì.
4. Test và build đã chạy cùng kết quả.
5. Các vấn đề còn lại nếu có.

Chỉ sửa những file thật sự liên quan đến các yêu cầu trên và không dừng ở bước phân tích nếu không có blocker thực sự.
