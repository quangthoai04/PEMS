# PROMPT FIX DOCUMENT MANAGEMENT PAGE — PEMS

Bạn là Senior Full-stack Engineer + Senior Frontend UI/UX Engineer cho PEMS.

Nhiệm vụ: sửa trang Quản lý tài liệu và kiểm tra logic lưu tài liệu sinh ra từ Logistics/Report. Không đổi schema nếu không cần. Không xóa chức năng đang chạy. Không dùng mock nếu đã có API thật. Code sạch, UI compact, responsive mobile.

## 1. Sửa UI trang Quản lý tài liệu

Hiện tại trang dùng nhiều card/ô lớn làm chiếm diện tích.

Yêu cầu UI:

- Hạn chế tối đa ô/card/khung lớn.
- Phần thống kê phía trên không dùng 4 card lớn nữa.
- Đổi thành 1 dòng tổng quan compact, ví dụ:
  `Tổng quan: 4 tài liệu • Draft 1 • Published 2 • Archived 1`
- Desktop: hiển thị 1 dòng gọn.
- Mobile: cho wrap nhẹ thành 1–2 dòng, không thành nhiều card lớn.
- Bộ lọc cũng làm compact, không chiếm nhiều chiều cao.
- Nếu còn “Bộ lọc nâng cao”, chỉ giữ nếu thật sự cần; nếu không thì thay bằng filter trực tiếp: Loại tài liệu, Trạng thái, Thời gian, Reset.
- Table/list tài liệu giữ đủ thông tin nhưng giảm padding, giảm row height, không làm chữ quá to.
- Không lược bỏ chức năng xem, tải, mở Google Drive nếu đang có.

## 2. Sửa modal xem chi tiết tài liệu

Hiện tại modal chi tiết có nhiều ô/card ở cột trái, chiếm diện tích.

Yêu cầu:

- Bỏ kiểu mỗi nhóm thông tin nằm trong card/ô lớn.
- Chuyển sang layout text key-value compact:
  - Tên tài liệu: ...
  - Loại: ...
  - Trạng thái: ...
  - File: ...
  - Dung lượng: ...
  - Google Drive: ...
  - Thuộc đoàn: ...
  - Cập nhật: ...
- Dùng section heading nhỏ + divider mỏng.
- Không lược bỏ thông tin.
- Giữ nguyên nút Zalo, Gmail, Tải xuống máy, Mở trong Google Drive nếu đang có.
- Nếu file không preview được, phần preview phải hiển thị lý do rõ ràng và gọn:
  `Không thể xem trước. File chưa có liên kết Google Drive hợp lệ hoặc định dạng không hỗ trợ preview.`
- Không để modal phải scroll quá nhiều ở cột trái.
- Desktop tận dụng chiều ngang: bên trái thông tin compact, bên phải preview.
- Mobile: hiển thị thông tin trước, preview sau, không tràn ngang.

## 3. Kiểm tra lý do không preview được file

Kiểm tra backend/API/data:

- File có tồn tại trong bảng `files` không?
- Có `storage_provider = GOOGLE_DRIVE` không?
- Có `external_file_id` thật không?
- Có `web_view_url` hoặc `download_url` thật không?
- Google Drive file có quyền xem phù hợp không?
- MIME type có hỗ trợ preview không?
- Seed data hiện tại có phải chỉ là metadata giả không?

Nếu seed data chỉ insert metadata giả, không có file thật trên Google Drive, thì preview không thể hoạt động. Khi đó:

- Không coi đây là bug UI.
- UI phải hiển thị fallback rõ ràng.
- Backend không được trả link giả khiến iframe/preview lỗi khó hiểu.

## 4. Logic lưu document khi sinh file nghiệp vụ

Hiện tại document có nhiều loại. Cần kiểm tra và triển khai logic hợp lý.

### A. Logistics handover PDF

Ở biên bản ký mượn/ký trả logistics, nếu có nút tải PDF:

- Khi user bấm tải PDF, backend sinh file PDF.
- Đồng thời upload file PDF lên Google Drive.
- Tạo record trong `files`.
- Tạo record trong `documents` với loại tài liệu logistics, ví dụ `document_type = LOGISTICS` hoặc category tương ứng theo schema hiện tại.
- Gắn document với `visit_request_id` / `visit_instance_id` / `logistics_item_id` / `handover_id` nếu schema/API có hỗ trợ.
- Sau đó mới trả file cho user download.
- Không chỉ download về máy mà không lưu metadata nghiệp vụ.

### B. Report PDF/Excel

Tương tự với report:

- Khi export PDF hoặc Excel từ module Report, backend sinh file.
- Upload lên Google Drive.
- Tạo `files`.
- Tạo `documents` với loại `REPORT`.
- Gắn với scope phù hợp: campus, report type, date range, generated_by.
- Trả file cho user download.
- Nếu người dùng ghi “exe” thì kiểm tra lại: nếu là Excel thì dùng `.xlsx`; không lưu hoặc phát sinh file `.exe`.

## 5. Quy tắc tránh tạo trùng document

Khi user bấm tải nhiều lần:

- Nếu cùng một nghiệp vụ, cùng version/nội dung, cùng generated parameters, không nên tạo vô hạn document trùng.
- Có thể dùng một trong hai hướng:
  - Reuse document đã sinh gần nhất nếu nội dung chưa đổi.
  - Hoặc tạo version mới có `version`, `generated_at`, `generated_by`.
- Chọn hướng ít phá code hiện tại nhất.
- Không làm mất khả năng audit.

## 6. Backend implementation gợi ý

Tạo service dùng chung, ví dụ:

- `DocumentGenerationService`
- `DocumentArchiveService`
- `GoogleDriveFileStorageService` nếu đã có thì reuse

Flow chuẩn:

```text
Generate business file
→ Upload to Google Drive
→ Insert files metadata
→ Insert documents metadata
→ Return download response
```

Không viết logic upload/archive lặp lại ở nhiều controller.

Controller chỉ gọi MediatR/Service theo pattern hiện có. Business rule nằm trong Handler/Service. Không nhét logic dài trong Controller.

## 7. Frontend implementation gợi ý

Tách component:

- `DocumentSummaryCompact`
- `DocumentFilterBar`
- `DocumentTable`
- `DocumentDetailModal`
- `DocumentPreviewPanel`
- `DocumentActionButtons`

Không nhét toàn bộ UI vào một file lớn. Không đổi route lớn nếu không cần.

## 8. Test sau khi sửa

Kiểm tra:

- Trang Quản lý tài liệu không còn 4 card lớn.
- Modal chi tiết tài liệu gọn hơn, không còn nhiều ô/card lớn.
- Preview hiển thị được nếu file có Drive link hợp lệ.
- Nếu file seed giả không có Drive link thật, UI hiển thị fallback rõ ràng.
- Bấm tải PDF logistics:
  - File tải về máy.
  - Đồng thời có record trong `files`/`documents`.
  - Document hiện ở trang Quản lý tài liệu với loại Logistics.
- Bấm export Report PDF/Excel:
  - File tải về máy.
  - Đồng thời có record trong `files`/`documents`.
  - Document hiện ở trang Quản lý tài liệu với loại Report.
- Build frontend/backend không lỗi.

## 9. Báo cáo lại

Báo cáo lại theo format:

- Files changed
- UI đã compact những phần nào
- Kết quả kiểm tra preview Google Drive
- Logic lưu document từ Logistics/Report đã làm thế nào
- Build/test result
