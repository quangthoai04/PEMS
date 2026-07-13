# Prompt sửa lỗi đồng bộ quốc gia đối tác

Bạn là **Senior Full-stack Engineer** của dự án **PEMS**. Hãy kiểm tra và sửa lỗi dữ liệu quốc gia đối tác không đồng bộ giữa:

- Trang quản lý đối tác.
- Quả cầu đối tác ở Homepage.
- Trang public `/partners`.

## Hiện trạng

Hệ thống đã có đối tác thuộc khoảng **10 quốc gia**, nhưng:

- Homepage chỉ ghim **7 quốc gia**, bao gồm Việt Nam.
- Trang `/partners` chỉ thống kê **6 quốc gia**.

## Yêu cầu thực hiện

1. Trước khi sửa, hãy search và đọc source thật của:
   - Partner Management.
   - Public Partner API.
   - Homepage globe/map.
   - Trang public `/partners`.
   - Frontend API service, type/interface và dữ liệu mapping liên quan.

   Không sửa theo suy đoán.

2. Kiểm tra kỹ xem frontend hoặc backend hiện đang:
   - Dùng danh sách quốc gia hardcode.
   - Dùng mock data.
   - Giới hạn bằng `slice`, `take`, `pageSize` hoặc pagination.
   - Dùng cache cũ.
   - Dùng các API khác nhau giữa Homepage và `/partners`.
   - Lọc sai theo trạng thái duyệt hoặc trạng thái hiển thị.

3. Homepage và `/partners` phải dùng cùng một nguồn dữ liệu public từ backend.

4. Danh sách quốc gia phải được tạo động bằng dữ liệu đối tác thực tế, chỉ lấy các đối tác:
   - `profile_status = APPROVED`.
   - Được phép hiển thị công khai theo field/trạng thái hiện có trong source và database.

5. Chuẩn hóa tên hoặc mã quốc gia trước khi:
   - Đếm số quốc gia.
   - Nhóm đối tác theo quốc gia.
   - Hiển thị cờ.
   - Ghim vị trí trên quả cầu.

   Tránh trường hợp cùng một quốc gia bị tách thành nhiều nhóm do:
   - Khác chữ hoa/chữ thường.
   - Khác dấu.
   - Dùng tên viết tắt.
   - Dùng tên tiếng Việt và tiếng Anh khác nhau.

6. Không giới hạn cố định chỉ 6 hoặc 7 quốc gia.

7. Không hardcode danh sách cờ, quốc gia hoặc tọa độ theo dữ liệu demo. Nếu cần tọa độ để ghim trên quả cầu, phải có mapping đầy đủ và có fallback rõ ràng cho quốc gia mới.

8. Việt Nam chỉ là điểm trung tâm đại diện cho FPT University:
   - Không cộng Việt Nam vào số “quốc gia đối tác” nếu không có partner công khai thuộc Việt Nam.
   - Có thể vẫn hiển thị Việt Nam như điểm trung tâm trên quả cầu, nhưng phải tách biệt với số liệu thống kê quốc gia đối tác.

9. Sau khi sửa, xác minh:
   - Số quốc gia trên `/partners` khớp dữ liệu public thực tế.
   - Mọi quốc gia có đối tác public đều xuất hiện trên quả cầu.
   - Tổng số đối tác và tổng số quốc gia đều được tính từ dữ liệu thật.
   - Không làm thay đổi luồng duyệt đối tác.
   - Không làm thay đổi quyền xem nội bộ/công khai.
   - Không làm lộ đối tác chưa duyệt, bị từ chối, bản nháp hoặc chỉ hiển thị nội bộ.

10. Bổ sung test phù hợp cho các trường hợp:
    - Có nhiều partner cùng một quốc gia.
    - Có quốc gia mới chưa từng xuất hiện trong dữ liệu demo.
    - Quốc gia có tên khác chữ hoa/chữ thường.
    - Partner đã duyệt nhưng không công khai.
    - Partner công khai nhưng chưa được duyệt.
    - Việt Nam chỉ là điểm trung tâm, không bị cộng sai vào thống kê.

11. Chạy:
    - Backend build.
    - Frontend build.
    - Frontend lint.
    - Các test liên quan.

## Phạm vi không được làm

- Không thay đổi workflow duyệt/từ chối đối tác.
- Không thay đổi authorization hoặc campus scope.
- Không thêm field, bảng, enum hoặc API mới nếu source và SQL chưa yêu cầu.
- Không refactor sâu ngoài phạm vi lỗi đồng bộ dữ liệu.
- Không dùng mock data để che lỗi.
- Không báo hoàn thành nếu chưa build/test hoặc chưa nêu rõ lý do không chạy được.

## Báo cáo cuối

Báo cáo theo cấu trúc:

1. Nguyên nhân gốc.
2. Dữ liệu/API cũ đang được dùng ở từng màn hình.
3. File đã sửa.
4. Logic mới.
5. Cách xử lý Việt Nam trên quả cầu và trong thống kê.
6. Test đã bổ sung.
7. Kết quả build/lint/test.
8. Kết quả trước và sau khi sửa.
