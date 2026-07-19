# PROMPT AI — Student: biên bản, ảnh đoàn khách và tạo tin tức từ trang Đóng góp kết quả

Bạn là Senior Full-stack Engineer. Hãy đọc codebase hiện tại trước khi sửa, đặc biệt trang **Đóng góp kết quả**, module **Biên bản trong Quản lý tiếp khách**, module **Quản lý tin tức**, nền tảng upload Google Drive dùng chung và database full mới có `visit_photo_folders`, `visit_photos`.

## Nguyên tắc

- Code clean, đúng Clean Architecture/convention hiện tại; sửa tối thiểu, không phá API/UI/chức năng cũ.
- Không liên quan Gallery; tuyệt đối không dùng `gallery_items`/`gallery_item_media`.
- Tái sử dụng component, command/query, validation và API hiện có; không copy-paste logic thành hai implementation riêng.
- Giữ phân quyền backend, scope theo campus/visit instance và trạng thái; frontend ẩn nút không thay thế authorization backend.

## 1. Biên bản ngay tại trang Đóng góp kết quả

- Trong phần **Biên bản**, nút `Tạo biên bản` hoặc `Sửa biên bản` mở modal ngay trên trang.
- Modal phải có đầy đủ chức năng và giao diện tương đương phần **Biên bản cuộc họp** trong luồng Quản lý tiếp khách: tên/trạng thái biên bản, danh sách người tham gia và điểm danh, ghi chú/nội dung rich text, đầu mục công việc và thao tác lưu/sửa hiện có.
- Tái sử dụng cùng component/form schema/API của màn hình cũ để dữ liệu hai nơi luôn đồng bộ; không tạo bảng biên bản mới.
- Student chỉ được tạo/sửa nếu là participant hợp lệ của đúng `visit_instance_id` và business rule hiện tại cho phép; không được truy cập instance khác bằng cách đổi URL/body ID.

## 2. Upload và quản lý ảnh đoàn khách

### Trang Đóng góp kết quả

- Tại phần **Ảnh / Media**, thêm nút `Upload ảnh` và danh sách ảnh đã tải lên.
- Chỉ nhận ảnh theo validation policy server-side (JPG/JPEG/PNG/WEBP, MIME + magic bytes, giới hạn dung lượng phù hợp); hỗ trợ upload nhiều ảnh nếu kiến trúc hiện tại cho phép.
- Dùng `IFileUploadService`, thêm `FilePurpose.VisitRequestPhoto` → DB value `VISIT_REQUEST_PHOTO` → object-key prefix phù hợp → map tới `GoogleDriveOptions.VisitRequestPhotoFolderId`. Không gọi Drive service trực tiếp, không hard-code folder ID/token.
- Cấu trúc Drive bắt buộc:

```text
Ảnh của đoàn khách/                 (VisitRequestPhotoFolderId)
└── VR-{visit_request_id}/          (mỗi đoàn/request đúng một folder)
    ├── HN/                         (folder theo campus_code)
    └── HCM/
```

- Dựa vào `visit_instance_id`, backend tự resolve `visit_request_id` và `campus_code`; không tin các ID quan hệ do frontend tự khai báo.
- Dùng `visit_photo_folders` để lưu folder Drive của đoàn; dùng `visit_photos` liên kết `file_id` với đúng request, instance, folder và Student upload. Không tạo schema trùng.
- Upload hợp lệ khi user `ACTIVE`, role `STUDENT`, có `visit_participants.participant_role = STUDENT`, `status = ACCEPTED` trong đúng instance. Upload Drive/DB phải có cleanup/compensation nếu bước sau thất bại.

### Tab Quản lý ảnh đoàn khách

- Thêm tab/menu `Quản lý ảnh đoàn khách` cho Student trong đúng khu vực chức năng.
- Bảng gồm: `STT | Tên đoàn khách | Tên thư mục | Hành động`.
- Tên đoàn phải lấy theo read-path v2 per-campus (`visit_instance_form_details`/service dual-read hiện có), không dùng sai compatibility projection của `visit_requests`.
- Chỉ hiển thị các đoàn/campus instance Student đang được phép tham gia; phân trang/tìm kiếm theo convention hiện tại.
- `Xem chi tiết`: mở trang/modal liệt kê ảnh bằng URL proxy `/api/files/{fileId}/content`; nếu có nút mở Drive thì dùng `web_view_url` do backend trả về, không tự ghép URL.
- `Chỉnh sửa`: cho thêm ảnh hoặc xóa mềm ảnh (`REMOVED`, đủ `removed_at`, `removed_by`, `removal_reason`). Khi xóa, áp dụng chính sách xóa Drive/metadata hiện có và không làm ảnh hưởng file của module khác.
- Chống IDOR cho list/detail/upload/delete; backend luôn kiểm tra ownership/participant scope.

## 3. Tạo tin tức từ đúng đoàn đang xem

- Trong phần **Tin tức** của trang Đóng góp kết quả, thêm nút `Tạo tin tức`.
- Nút điều hướng trực tiếp sang màn hình tạo tin tức hiện có, truyền định danh đúng `visit_instance_id` (và request ID nếu route hiện tại cần).
- Form tạo tin tự động preselect/lock đoàn đang chọn; không bắt Student chọn đoàn lại và không cho thay sang đoàn ngoài scope.
- Tái sử dụng màn hình/API tạo news hiện tại; giữ nguyên workflow `chờ Staff Leader duyệt trước khi đăng`, không tạo luồng publish mới.
- Nếu đã có bài tin phù hợp, xử lý theo rule hiện tại (ẩn nút, mở bài hiện có hoặc báo rõ), không tạo duplicate ngoài ý muốn.

## Backend/API tối thiểu

- Bổ sung query/command/controller cần thiết cho: list folder theo Student scope, detail ảnh, upload ảnh, soft-delete ảnh.
- DTO không trả trực tiếp token/secret/Drive file ID nếu frontend không cần; ưu tiên file proxy URL.
- Transaction/audit/error code theo convention dự án; xử lý concurrent first-upload để chỉ tạo đúng một folder cho mỗi `visit_request_id`.

## Kiểm thử bắt buộc

- Unit + integration tests cho happy path và: Student không tham gia, INVITED/DECLINED/REMOVED, inactive user, sai instance/request/campus, file giả MIME, file quá lớn, duplicate/concurrent folder, upload Drive thành công nhưng DB lỗi, xóa ảnh không thuộc scope, IDOR list/detail, v2 mixed-campus lấy đúng tên đoàn.
- Regression biên bản và news cũ; build FE/BE; chạy toàn bộ test suite liên quan.

## Hoàn tất

- Báo cáo file đã sửa, API/route mới, migration/schema mapping, test đã chạy và kết quả.
- Không commit nếu chưa được yêu cầu. Nếu commit: gom thay đổi theo feature hợp lý, author/committer `Tcanh12 <canhnvthe186121@fpt.edu.vn>`, không có Claude/AI attribution.
