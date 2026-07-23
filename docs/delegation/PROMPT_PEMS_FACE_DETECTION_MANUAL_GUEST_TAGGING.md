# PROMPT — Hoàn thiện quét khuôn mặt và gắn khách trong ảnh đoàn

Hãy đọc codebase PEMS và database full mới nhất, sau đó trực tiếp sửa code. Không lập thêm kế hoạch và không thay đổi schema SQL.

## Mục tiêu

Hoàn thiện chức năng tại **Sau tiếp khách → Lưu trữ ảnh đoàn khách → Scan và gán tên khuôn mặt**:

```text
Ảnh đã upload vào visit_photos
→ Google Cloud Vision FACE_DETECTION
→ lưu lần quét và bounding box
→ hiển thị khung mặt trên ảnh
→ Staff chọn thủ công khách thuộc đúng visit instance
→ xác nhận
→ tạo photo_face_tags và lưu lịch sử
```

Không nhận diện danh tính tự động, không lưu face embedding và không dùng dữ liệu cảm xúc.

## 1. Đồng bộ Entity/EF với database hiện tại

Tạo entity, `DbSet`, Fluent Configuration và navigation cho:

- `visit_photo_face_scans`
- `visit_photo_face_detections`

Map chính xác tên bảng/cột, enum string, `DECIMAL(10,8)`, `DECIMAL(6,5)`, index, unique key và quan hệ đã có trong SQL. Liên kết với:

- `visit_photos`
- `api_configurations`
- `visit_instance_guest_members`
- `photo_face_tags`
- `users`

Không tạo EF migration và không sửa SQL.

## 2. Provider Google Vision

Tạo `IFaceDetectionProvider` và implementation Google Vision:

- Đọc config `FACE_DETECTION_GOOGLE_VISION`.
- Chỉ chạy khi config `ACTIVE`.
- Dùng credential resolver/cơ chế quota, rate limit, timeout và usage log hiện hữu của API Management.
- Đọc bytes ảnh từ file/Google Drive bằng storage abstraction hiện có; không yêu cầu ảnh public.
- Gọi:

```text
POST https://vision.googleapis.com/v1/images:annotate
feature = FACE_DETECTION
```

- Lấy `fdBoundingPoly` hoặc `boundingPoly`, `detectionConfidence`.
- Chuyển tọa độ pixel thành tỷ lệ `0..1` theo kích thước ảnh.
- Không lưu raw response, access token, landmark hoặc emotion.
- Khi lỗi phải cập nhật scan `FAILED` với mã lỗi an toàn; không ghi credential vào log.

## 3. Backend API và nghiệp vụ

Tái sử dụng ảnh đã upload trong `visit_photos`, không upload ảnh lần hai.

Thêm các endpoint dưới `VisitPhotosController` hoặc controller cùng module:

```text
POST /api/visit-photos/{visitPhotoId}/face-scans
GET  /api/visit-photos/{visitPhotoId}/face-scans
GET  /api/visit-photos/face-scans/{faceScanId}
GET  /api/visit-photos/instances/{visitInstanceId}/taggable-guests
POST /api/visit-photos/face-scans/{faceScanId}/confirm
```

### Bắt đầu quét

- Kiểm tra ảnh `ACTIVE`, thuộc đúng `visit_request_id` và `visit_instance_id`.
- Chống bấm lặp: không tạo thêm scan khi ảnh đang có scan `PENDING/PROCESSING`.
- Tạo trạng thái `PENDING → PROCESSING → SUCCEEDED/FAILED`.
- Lưu mỗi mặt vào `visit_photo_face_detections`, gồm index, normalized bounding box và confidence.
- Cập nhật số mặt và thời gian hoàn tất.

### Danh sách khách có thể gắn

Chỉ trả khách thuộc đúng:

```text
visit_instance_guest_members
(visit_instance_id, guest_member_id)
```

DTO tối thiểu:

```text
guestMemberId, fullName, memberType, organization, jobTitle, nationality
```

Không trả khách của campus instance hoặc đoàn khác.

### Xác nhận gắn người

Body dạng batch:

```json
{
  "rowVersion": 0,
  "faces": [
    { "faceDetectionId": 1, "guestMemberId": 100, "ignored": false },
    { "faceDetectionId": 2, "guestMemberId": null, "ignored": true }
  ]
}
```

Trong một transaction:

- Mọi face của scan phải được gắn một khách hoặc đánh dấu bỏ qua.
- Không cho một khách xuất hiện hai lần trong cùng scan.
- Re-check khách thuộc đúng visit instance để chống IDOR.
- Với face được gắn:
  - tạo `photo_face_tags`;
  - lưu `file_id`, `visit_request_id`, `guest_member_id`, tên hiển thị, `person_name_key`, bounding box và người tạo;
  - cập nhật detection thành `CONFIRMED`, gắn `face_tag_id`.
- Với face bỏ qua: cập nhật `IGNORED`.
- Cập nhật counts, `confirmed_by`, `confirmed_at`, `row_version` và scan thành `CONFIRMED`.
- Xử lý concurrency, không cho xác nhận lại hoặc tạo tag trùng.

## 4. Phân quyền và audit

Backend là nguồn quyền duy nhất:

- Tái sử dụng đúng scope quản lý **Sau tiếp khách** hiện có; không mở rộng quyền chỉ vì frontend hiện nút.
- Người thao tác phải có quyền trên đúng `visit_instance_id`.
- ADMIN chỉ quản lý cấu hình API, không mặc nhiên được tag ảnh nếu nghiệp vụ hiện tại không cho phép.
- Ghi audit bằng hạ tầng hiện hữu cho:
  - `VISIT_PHOTO_FACE_SCAN_STARTED`
  - `VISIT_PHOTO_FACE_SCAN_SUCCEEDED`
  - `VISIT_PHOTO_FACE_SCAN_FAILED`
  - `VISIT_PHOTO_FACE_TAGS_CONFIRMED`
- Audit chỉ lưu ID, counts, actor và lỗi đã làm sạch; không lưu ảnh/base64/raw Google response.

## 5. Frontend hiện có tại VisitAfterTab

Giữ bố cục hiện tại nhưng thay toàn bộ mock bằng dữ liệu thật:

- Xóa `DEFAULT_GUESTS`, `PRESET_PHOTOS`, tọa độ giả và `setTimeout` mô phỏng scan.
- Giữ upload hiện hữu qua `visitPhotosApi`.
- Mở rộng types, endpoints và `visitPhotosApi` cho face scan.
- Khi chọn ảnh:
  - tải scan mới nhất/lịch sử;
  - bấm **Quét khuôn mặt** để gọi backend;
  - hiển thị loading và lỗi thật.
- Vẽ bounding box bằng `left/top/width/height = normalizedValue * 100%`.
- Click khung mặt để mở dropdown khách thật; có lựa chọn **Không thuộc đoàn / Bỏ qua**.
- Một khách đã chọn ở face khác phải bị loại khỏi dropdown.
- Chỉ bật **Xác nhận kết quả** khi mọi face đã xử lý.
- Sau xác nhận hiển thị read-only tên người trên từng khung.
- Hiển thị lịch sử quét: thời gian, người quét, trạng thái, số mặt phát hiện/xác nhận/bỏ qua và lỗi.
- Dùng cơ chế tải ảnh có authentication hiện hữu; không dựa vào URL public.
- Text tĩnh dùng i18n VI/EN.

## 6. Ràng buộc

- Không sửa database/schema.
- Không tạo chức năng nhận diện người tự động.
- Không tạo crop ảnh hoặc lưu thêm dữ liệu sinh trắc học.
- Không phá luồng upload Google Drive, News, Gallery hoặc Photo Management hiện có.
- Không dùng mock/fallback giả khi API lỗi.
- Chạy build backend và frontend, sửa hết lỗi compile/type.
- Báo danh sách file thay đổi và luồng hoàn chỉnh.
- Commit gọn theo chức năng; không ghi Claude, AI hoặc tên công cụ trong commit message.
