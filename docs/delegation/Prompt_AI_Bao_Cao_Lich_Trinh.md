# Prompt AI - Xuất báo cáo lịch trình PDF

Hãy đọc kỹ codebase và triển khai chức năng xuất **Báo cáo Lịch trình** dạng PDF, đồng bộ với kiến trúc, phân quyền và dữ liệu hiện có.

## Yêu cầu

- Tại giai đoạn **Trước tiếp khách**, thêm nút **“Báo cáo Lịch trình”** cạnh nút **“Xác nhận hoàn thành chuẩn bị”**. Khi bấm, hệ thống tải xuống file PDF.
- Thiết kế PDF A4 chuyên nghiệp, bám sát bố cục file mẫu `Meeting Agenda - Asia University and FPT University - 06Nov24_1(1).pdf`.
- Phần đầu trang:
  - Đối tác đã có logo: logo FPT bên trái, logo đối tác bên phải.
  - Đối tác mới hoặc chưa có logo: chỉ hiển thị logo FPT ở chính giữa.
- Nội dung PDF:
  - **Thời gian:** lấy từ form đăng ký.
  - **Địa điểm:** hiển thị `FPT University`; địa điểm cụ thể lấy từ từng nội dung trong Agenda.
  - **Mục tiêu:** lấy từ form đăng ký.
  - **Thành phần phía khách:** gộp danh sách khách và danh sách nhân sự hỗ trợ trong form.
  - **Thành phần phía FPT:** gồm Host và những người được mời tham gia đã đồng ý lời mời; không đưa người chưa phản hồi hoặc từ chối vào báo cáo.
  - **Bảng lịch trình:** lấy đúng các nội dung Agenda mà Host đã thiết lập, gồm các cột `Time`, `Activity Description`, `Venue`, `Party in Charge`; cột `Party in Charge` luôn hiển thị `FPT University`.
- Hỗ trợ nội dung dài và lịch trình nhiều dòng/trang; không để chữ, bảng hoặc logo bị cắt hay chồng lấn. Có số trang và giữ tiêu đề bảng khi sang trang.
- Dữ liệu PDF phải lấy từ backend theo đúng phạm vi đoàn/campus, không ghép dữ liệu giả hoặc chỉ xử lý ở frontend.
- Tái sử dụng dịch vụ xuất PDF, DTO và API hiện có nếu phù hợp; không làm ảnh hưởng luồng xác nhận hoàn thành chuẩn bị.
- Bổ sung xử lý lỗi rõ ràng khi thiếu dữ liệu cần thiết và thêm test cho mapping dữ liệu, quy tắc logo, danh sách người tham gia, Agenda và quyền tải báo cáo.

Sau khi hoàn thành, hãy chạy build/test liên quan và báo cáo ngắn gọn: file đã sửa, API/luồng dữ liệu sử dụng, test đã chạy và kết quả.
