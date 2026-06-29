# PROMPT / ĐẶC TẢ CẬP NHẬT — CHO PHÉP 1 LOCATION CÓ NHIỀU GALLERY ITEM

## 1. Bối cảnh

Dự án PEMS đã hoàn thành các chức năng:

- Quản lý Gallery cho Staff Leader.
- Quản lý khu vực Gallery.
- Upload ảnh/video lên Google Drive thông qua upload service dùng chung.
- Public VisitFPTU Gallery hiển thị theo campus/khu vực/vị trí/gallery item.

Trước đó nghiệp vụ từng chốt:

```text
1 location = tối đa 1 gallery item.
```

Hiện tại cần sửa lại nghiệp vụ thành:

```text
1 location = có thể có nhiều gallery item.
1 gallery item = 1 bài đăng Gallery đầy đủ gồm title, description, ảnh/video, media_kind, status.
```

Nghĩa là một vị trí cụ thể như `TÒA DELTA / Thư viện` có thể có nhiều bài đăng Gallery khác nhau, ví dụ:

```text
TÒA DELTA / Thư viện
  - Gallery item 1: Không gian tự học hiện đại
  - Gallery item 2: Góc đọc sách sinh viên
  - Gallery item 3: Khu vực học nhóm
```

Cách tạo, cách hiển thị bảng quản lý Gallery, edit, view detail, enable/disable gallery item vẫn giữ nguyên như hiện tại. Chỉ thay đổi phần ràng buộc và các rule đang suy ra từ ràng buộc cũ.

---

## 2. Mục tiêu cập nhật

AI Agent cần cập nhật code và database để đạt các mục tiêu sau:

```text
1. Một location có thể có 0, 1 hoặc nhiều gallery item.
2. Không còn ràng buộc unique trên gallery_items.location_id.
3. Không còn backend/frontend validation chặn tạo thêm gallery item khi location đã có item.
4. Trang Quản lý Gallery vẫn hiển thị 1 row = 1 gallery item.
5. Nếu nhiều item cùng location thì bảng Quản lý Gallery hiển thị nhiều row cùng khu vực/vị trí nhưng khác title/status/media/date.
6. Trang Quản lý khu vực vẫn hiển thị 1 row = 1 location, không bị duplicate row khi location có nhiều item.
7. Khi disable location, tất cả gallery item PUBLISHED thuộc location đó phải bị set về HIDDEN.
8. Khi enable location, không tự publish lại bất kỳ gallery item nào.
9. Public Gallery phải hỗ trợ nhiều gallery item trong cùng một location.
```

---

## 3. File/tài liệu cần đọc trước khi code

AI Agent phải đọc kỹ các file/tài liệu sau trước khi sửa:

```text
1. Database mới nhất:
   pems_full_v10_new_final_visit_lifecycle_news_not_required.sql

2. UC Quản lý Gallery:
   UC_Quan_Ly_VisitFPTU_Gallery.md

3. UC Quản lý khu vực Gallery:
   UC_Quan_Ly_Khu_Vuc_Gallery_UPDATED_FINAL.md

4. UC Public VisitFPTU Gallery nếu có:
   UC_Public_VisitFPTU_Gallery.md

5. Google Drive upload foundation / upload service dùng chung nếu có liên quan tới create/edit gallery item.
```

Không được code theo trí nhớ hoặc mock data. Phải đối chiếu code hiện tại với database thật.

---

## 4. Thay đổi database bắt buộc

### 4.1 Xóa unique constraint trên `gallery_items.location_id`

Hiện tại database đang có ràng buộc kiểu:

```sql
UNIQUE KEY uq_gallery_items_location (location_id)
```

Ràng buộc này làm cho một location chỉ tạo được một gallery item. Cần xóa.

Nếu sửa trực tiếp trong file full SQL, trong `CREATE TABLE gallery_items`, xóa dòng:

```sql
UNIQUE KEY uq_gallery_items_location (location_id),
```

Vẫn giữ index thường:

```sql
KEY idx_gallery_items_location_status (location_id, status, deleted_at)
```

Index này vẫn cần để list/filter/query theo location chạy nhanh.

### 4.2 SQL patch nếu database đã chạy

Nếu DB đã tạo rồi, tạo patch:

```sql
ALTER TABLE gallery_items
  DROP INDEX uq_gallery_items_location;
```

Nếu muốn patch an toàn, kiểm tra index tồn tại trước:

```sql
SELECT COUNT(*) AS index_exists
FROM information_schema.statistics
WHERE table_schema = DATABASE()
  AND table_name = 'gallery_items'
  AND index_name = 'uq_gallery_items_location';
```

### 4.3 Kết quả mong muốn

Sau khi sửa:

```text
gallery_items.location_id không còn unique.
Nhiều row trong gallery_items có thể dùng cùng một location_id.
```

Ví dụ hợp lệ:

```sql
INSERT INTO gallery_items (location_id, title, description, media_kind, status)
VALUES
  (10, 'Không gian tự học hiện đại', '...', 'IMAGE', 'PUBLISHED'),
  (10, 'Góc đọc sách sinh viên', '...', 'IMAGE', 'PUBLISHED'),
  (10, 'Khu vực học nhóm', '...', 'VIDEO', 'HIDDEN');
```

---

## 5. Thay đổi trong UC Quản lý khu vực

### 5.1 Sửa mục tiêu nghiệp vụ

Thay rule cũ:

```text
1 location = tối đa 1 gallery item.
```

Bằng rule mới:

```text
1 location = có thể có nhiều gallery item.
1 gallery item = 1 bài đăng Gallery đầy đủ gồm title, description, ảnh/video, media_kind, status.
```

Nói chính xác:

```text
Một location có thể có 0, 1 hoặc nhiều gallery item.
```

### 5.2 Sửa mô tả ảnh hưởng của location status

Thay đoạn cũ:

```text
Trang Quản lý khu vực không upload file và không edit nội dung bài đăng Gallery.
Tuy nhiên, trạng thái location có ảnh hưởng trực tiếp đến bài đăng Gallery tương ứng vì mỗi location chỉ có tối đa một bài đăng.
```

Bằng:

```text
Trang Quản lý khu vực không upload file và không edit nội dung bài đăng Gallery.
Tuy nhiên, trạng thái location có ảnh hưởng trực tiếp đến toàn bộ các bài đăng Gallery thuộc location đó.
```

### 5.3 Sửa quan hệ nghiệp vụ

Thay:

```text
Một gallery_location có tối đa một gallery_item.
```

Bằng:

```text
Một gallery_location có nhiều gallery_items.
```

Sơ đồ quan hệ vẫn giữ:

```text
campuses
  └── gallery_areas
        └── gallery_locations
              └── gallery_items
                    └── gallery_item_media
                          └── files
```

Chỉ thay cardinality:

```text
gallery_locations 1 - n gallery_items
```

---

## 6. Xóa toàn bộ rule cũ “location đã có gallery item”

Phải xóa các rule/message/validation sau khỏi UC, backend và frontend:

```text
Mỗi location tối đa một gallery item.
Location đã có gallery item thì không được tạo thêm.
Không trả location đã có gallery item trong dropdown upload.
Disable location chỉ xử lý một gallery item duy nhất.
Vị trí này đã có bài đăng Gallery.
UNIQUE KEY uq_gallery_items_location (location_id).
```

Trong UC Quản lý khu vực, xóa acceptance criteria cũ:

```text
AC-LOC-21 — Mỗi location tối đa một gallery item

Given location `TÒA DELTA / Thư viện` đã có gallery item
When Staff Leader tạo thêm gallery item mới vào cùng location
Then backend reject với HTTP 409
And thông báo `Vị trí này đã có bài đăng Gallery.`
```

Thay bằng AC mới ở phần Acceptance Criteria của tài liệu này.

---

## 7. Cập nhật backend Quản lý Gallery

### 7.1 Create Gallery Item

Giữ nguyên form tạo hiện tại:

```text
title
description
locationId
status
files[]
```

Nhưng bỏ rule:

```text
Không cho tạo nếu location đã có gallery item.
```

Thay bằng:

```text
Cho phép tạo nhiều gallery item trong cùng một location.
```

Vẫn giữ các validation khác:

```text
- Chỉ Staff Leader active được tạo gallery item.
- locationId bắt buộc.
- Location phải tồn tại.
- Location phải thuộc campus của Staff Leader.
- Location phải ACTIVE.
- Area cha phải ACTIVE.
- title bắt buộc.
- description bắt buộc.
- files[] bắt buộc khi tạo mới.
- Mỗi gallery item phải có ít nhất một media.
- Mỗi gallery item chỉ có một primary media.
- Backend tự tính media_kind từ media active.
- Upload file phải dùng IFileUploadService.
```

### 7.2 Những đoạn code cần tìm và bỏ

Tìm trong backend các đoạn tương tự:

```csharp
var exists = await _db.GalleryItems
    .AnyAsync(x => x.LocationId == request.LocationId && x.DeletedAt == null, ct);

if (exists)
{
    throw new ConflictException("Vị trí này đã có bài đăng Gallery.");
}
```

Hoặc:

```sql
SELECT COUNT(*)
FROM gallery_items
WHERE location_id = @LocationId
  AND deleted_at IS NULL;
```

Nếu count > 0 rồi reject thì phải xóa validation này.

### 7.3 Edit Gallery Item

Edit vẫn giữ nguyên:

```text
Edit theo gallery_item_id.
Không edit theo location_id.
```

Nếu user chuyển một gallery item sang location khác, vẫn được phép nếu:

```text
- location mới thuộc campus của Staff Leader.
- location mới ACTIVE.
- area cha ACTIVE.
```

Không cần kiểm tra location mới đã có item hay chưa.

### 7.4 Enable/Disable Gallery Item

Không đổi logic chính:

```text
Disable gallery item → chỉ update đúng gallery_items.status của item đó về HIDDEN.
Enable gallery item → chỉ update đúng gallery_items.status của item đó về PUBLISHED nếu area/location ACTIVE và item còn media active.
```

Không được update các item khác cùng location khi toggle một gallery item.

---

## 8. Cập nhật frontend Quản lý Gallery

### 8.1 Trang list

Trang list vẫn giữ nguyên logic:

```text
1 row = 1 gallery item.
```

Nếu cùng một location có nhiều gallery item thì bảng sẽ có nhiều dòng cùng `Khu vực` và `Vị trí cụ thể`, nhưng khác `Tiêu đề`, `Định dạng`, `Trạng thái`, `Ngày tạo`.

Ví dụ:

| Khu vực | Vị trí cụ thể | Tiêu đề | Trạng thái |
|---|---|---|---|
| TÒA DELTA | Thư viện | Không gian tự học hiện đại | Hiển thị |
| TÒA DELTA | Thư viện | Góc đọc sách sinh viên | Đã ẩn |
| TÒA DELTA | Thư viện | Khu vực học nhóm | Hiển thị |

### 8.2 Upload modal

Dropdown `Vị trí thực tế` không được disable location chỉ vì location đó đã có gallery item.

Phải bỏ các logic frontend kiểu:

```text
- Ẩn location đã có gallery item.
- Disable location đã có gallery item.
- Hiển thị message: Vị trí này đã có bài đăng Gallery.
```

Dropdown location chỉ cần lọc:

```text
- location ACTIVE.
- area ACTIVE.
- thuộc campus của Staff Leader.
```

### 8.3 Error message

Xóa message:

```text
Vị trí này đã có bài đăng Gallery.
```

Không còn dùng message này ở create Gallery.

---

## 9. Cập nhật Quản lý khu vực — list location

### 9.1 Vấn đề cần tránh

Nếu một location có nhiều gallery item, query list location không được join trực tiếp `gallery_items` rồi trả từng row, vì sẽ làm duplicate location trên trang Quản lý khu vực.

Sai:

```sql
SELECT gl.*, gi.gallery_item_id, gi.status
FROM gallery_locations gl
LEFT JOIN gallery_items gi ON gi.location_id = gl.location_id
```

Nếu location có 5 item, query này trả 5 row location.

### 9.2 Cách đúng

Trang Quản lý khu vực vẫn phải hiển thị:

```text
1 row = 1 location.
```

Nếu cần thông tin gallery item, dùng aggregate count.

DTO cũ dạng số ít:

```csharp
public bool HasGalleryItem { get; init; }
public long? GalleryItemId { get; init; }
public string? GalleryItemStatus { get; init; }
```

Phải đổi thành:

```csharp
public bool HasGalleryItems { get; init; }
public int GalleryItemCount { get; init; }
public int PublishedGalleryItemCount { get; init; }
public int HiddenGalleryItemCount { get; init; }
```

Response mẫu:

```json
{
  "locationId": 1,
  "areaId": 1,
  "areaName": "TÒA ALPHA",
  "locationName": "Hồ sen",
  "status": "INACTIVE",
  "createdAt": "2026-05-05T00:00:00",
  "updatedAt": null,
  "hasGalleryItems": true,
  "galleryItemCount": 5,
  "publishedGalleryItemCount": 0,
  "hiddenGalleryItemCount": 5,
  "canEdit": true,
  "canToggle": true
}
```

Query đúng:

```sql
SELECT
    gl.location_id,
    ga.area_id,
    ga.area_name,
    gl.location_name,
    gl.status,
    gl.created_at,
    gl.updated_at,
    COUNT(gi.gallery_item_id) AS gallery_item_count,
    SUM(CASE WHEN gi.status = 'PUBLISHED' THEN 1 ELSE 0 END) AS published_gallery_item_count,
    SUM(CASE WHEN gi.status = 'HIDDEN' THEN 1 ELSE 0 END) AS hidden_gallery_item_count
FROM gallery_locations gl
JOIN gallery_areas ga
    ON ga.area_id = gl.area_id
LEFT JOIN gallery_items gi
    ON gi.location_id = gl.location_id
   AND gi.deleted_at IS NULL
WHERE ga.campus_id = @currentUserCampusId
GROUP BY
    gl.location_id,
    ga.area_id,
    ga.area_name,
    gl.location_name,
    gl.status,
    gl.created_at,
    gl.updated_at
ORDER BY gl.created_at DESC, gl.location_id DESC;
```

---

## 10. Cập nhật logic Disable location

### 10.1 Logic mới

Khi Staff Leader disable location:

```text
1. Set gallery_locations.status = INACTIVE.
2. Tìm tất cả gallery item thuộc location đó.
3. Với mọi gallery item đang PUBLISHED:
   - Set gallery_items.status = HIDDEN.
   - Set gallery_items.updated_by = currentUserId.
   - Set gallery_items.updated_at = NOW().
4. Gallery item đã HIDDEN thì giữ nguyên.
5. Không update gallery_item_media.
6. Không xóa file Google Drive.
7. Không update gallery_areas.status.
```

SQL đúng:

```sql
UPDATE gallery_locations
SET status = 'INACTIVE',
    updated_by = @currentUserId,
    updated_at = NOW()
WHERE location_id = @locationId;

UPDATE gallery_items
SET status = 'HIDDEN',
    updated_by = @currentUserId,
    updated_at = NOW()
WHERE location_id = @locationId
  AND status = 'PUBLISHED'
  AND deleted_at IS NULL;
```

Câu SQL thứ hai update tất cả item PUBLISHED thuộc location đó.

### 10.2 Code cần tránh

Không được dùng logic chỉ xử lý một item:

```csharp
var item = await _db.GalleryItems
    .FirstOrDefaultAsync(x => x.LocationId == locationId, ct);

if (item != null && item.Status == Published)
{
    item.Status = Hidden;
}
```

Phải xử lý batch/all items:

```csharp
var publishedItems = await _db.GalleryItems
    .Where(x => x.LocationId == locationId
             && x.Status == GalleryItemStatus.Published
             && x.DeletedAt == null)
    .ToListAsync(ct);

foreach (var item in publishedItems)
{
    item.Status = GalleryItemStatus.Hidden;
    item.UpdatedBy = currentUserId;
    item.UpdatedAt = now;
}
```

Hoặc dùng bulk update nếu project đang hỗ trợ.

### 10.3 Transaction

Disable location + hide all published gallery items phải nằm trong cùng transaction.

```text
Nếu update location thành công nhưng update item lỗi → rollback cả hai.
```

---

## 11. Cập nhật logic Enable location

Khi Staff Leader enable location:

```text
1. Set gallery_locations.status = ACTIVE.
2. Không tự động set bất kỳ gallery item nào về PUBLISHED.
3. Tất cả gallery item thuộc location vẫn giữ nguyên status hiện tại.
4. Toggle của các gallery item thuộc location đó active trở lại.
5. Staff Leader muốn public item nào thì tự bật toggle item đó ở trang Quản lý Gallery.
```

SQL đúng:

```sql
UPDATE gallery_locations
SET status = 'ACTIVE',
    updated_by = @currentUserId,
    updated_at = NOW()
WHERE location_id = @LocationId;
```

Không được chạy:

```sql
UPDATE gallery_items
SET status = 'PUBLISHED'
WHERE location_id = @LocationId;
```

---

## 12. Cập nhật badge và toggle ở Quản lý Gallery khi location inactive

Rule cũ vẫn giữ, nhưng áp dụng cho tất cả gallery item thuộc location đó.

Nếu location INACTIVE:

```text
- Tất cả gallery item thuộc location đó vẫn hiển thị trong trang Quản lý Gallery.
- Dưới tên vị trí của từng row hiển thị badge “Vị trí ngừng hoạt động”.
- Toggle của từng gallery item thuộc location đó bị disabled.
- Không cho bật bất kỳ item nào sang PUBLISHED khi location inactive.
```

Khi enable location lại:

```text
- Badge “Vị trí ngừng hoạt động” biến mất khỏi tất cả item thuộc location đó.
- Toggle của tất cả item thuộc location đó active trở lại.
- Status của các item vẫn giữ HIDDEN cho đến khi Staff Leader bật từng item.
```

Backend vẫn phải chặn trực tiếp nếu user gọi API set item `PUBLISHED` trong khi location/area inactive:

```text
Nếu request muốn set gallery_items.status = PUBLISHED
AND location hoặc area không ACTIVE
→ reject HTTP 409.
```

Message:

```text
Không thể hiển thị bài đăng vì vị trí đang ngừng hoạt động.
```

---

## 13. Cập nhật `/locations/options`

Endpoint:

```http
GET /api/gallery-management/locations/options
```

Rule mới:

```text
Trả location nếu:
- gallery_locations.status = ACTIVE
- gallery_areas.status = ACTIVE
- gallery_areas.campus_id = currentUser.primary_campus_id
```

Không kiểm tra location đã có bao nhiêu gallery item.

Xóa rule cũ:

```text
Không trả location đã có gallery item active/non-deleted.
Hoặc trả nhưng đánh dấu disabled với message: "Vị trí này đã có bài đăng Gallery."
```

---

## 14. Cập nhật Public Gallery

Public visibility vẫn giữ công thức:

```text
area ACTIVE
location ACTIVE
gallery item PUBLISHED
media ACTIVE
```

Nhưng khi click một location, public page phải hỗ trợ nhiều gallery item trong location đó.

Có thể chọn một trong hai cách hiển thị:

```text
Cách 1: Hiển thị danh sách/slider các gallery item thuộc location đó.
Cách 2: Hiển thị item đầu tiên, có nút next/previous để chuyển qua các gallery item khác cùng location.
```

Query theo location phải trả nhiều item:

```sql
SELECT
    gi.gallery_item_id,
    ga.area_name,
    gl.location_name,
    gi.title,
    gi.description,
    gi.media_kind,
    gi.status,
    gi.display_order,
    gi.created_at
FROM gallery_items gi
JOIN gallery_locations gl ON gl.location_id = gi.location_id
JOIN gallery_areas ga ON ga.area_id = gl.area_id
WHERE gl.location_id = @LocationId
  AND ga.status = 'ACTIVE'
  AND gl.status = 'ACTIVE'
  AND gi.status = 'PUBLISHED'
  AND gi.deleted_at IS NULL
ORDER BY gi.display_order ASC, gi.created_at DESC, gi.gallery_item_id DESC;
```

Nếu public API hiện tại trả một `galleryItem` duy nhất theo location, phải đổi thành danh sách `galleryItems`.

---

## 15. Acceptance Criteria mới/cập nhật

### AC-GAL-MULTI-01 — Tạo nhiều gallery item trong cùng một location

Given location `TÒA DELTA / Thư viện` đang ACTIVE  
And location này đã có một gallery item  
When Staff Leader tạo thêm gallery item mới vào cùng location ở trang Quản lý Gallery  
Then backend cho phép tạo nếu dữ liệu hợp lệ  
And không trả lỗi `Vị trí này đã có bài đăng Gallery.`  
And trang Quản lý Gallery hiển thị thêm một dòng mới cùng khu vực/vị trí đó.

### AC-GAL-MULTI-02 — List Gallery hiển thị nhiều row cùng location

Given location `TÒA DELTA / Thư viện` có 3 gallery item  
When Staff Leader mở trang Quản lý Gallery  
Then bảng hiển thị 3 row gallery item  
And cả 3 row có cùng khu vực/vị trí  
And mỗi row có title/status/media riêng.

### AC-LOC-MULTI-01 — Quản lý khu vực không duplicate row location

Given location `TÒA DELTA / Thư viện` có 5 gallery item  
When Staff Leader mở trang Quản lý khu vực  
Then bảng Quản lý khu vực chỉ hiển thị 1 row cho location `Thư viện`  
And không duplicate thành 5 row.

### AC-LOC-MULTI-02 — Disable location tự ẩn tất cả gallery item published

Given location `TÒA DELTA / Thư viện` đang ACTIVE  
And location này có 3 gallery item PUBLISHED  
And 2 gallery item HIDDEN  
When Staff Leader disable location  
Then `gallery_locations.status = INACTIVE`  
And 3 gallery item PUBLISHED được set về HIDDEN  
And 2 gallery item HIDDEN vẫn giữ nguyên HIDDEN  
And tất cả 5 item vẫn hiển thị trong trang Quản lý Gallery nội bộ  
And tất cả 5 item có badge `Vị trí ngừng hoạt động`  
And toggle của tất cả 5 item bị disabled.

### AC-LOC-MULTI-03 — Enable location không publish lại bất kỳ gallery item nào

Given location `TÒA DELTA / Thư viện` đang INACTIVE  
And location này có nhiều gallery item HIDDEN  
When Staff Leader enable location  
Then `gallery_locations.status = ACTIVE`  
And tất cả gallery item vẫn giữ trạng thái HIDDEN  
And toggle của tất cả item active trở lại  
And Staff Leader phải bật từng item nếu muốn public lại.

### AC-PGAL-MULTI-01 — Public Gallery hiển thị nhiều item trong cùng location

Given location `TÒA DELTA / Thư viện` đang ACTIVE  
And location này có 3 gallery item PUBLISHED  
When public user click location `Thư viện`  
Then public page hiển thị được danh sách/slider 3 gallery item thuộc location đó.

---

## 16. Error message cần xóa

Xóa message:

```text
Vị trí này đã có bài đăng Gallery.
```

Không dùng message này ở:

```text
- Backend create gallery item.
- Frontend upload modal.
- Location options dropdown.
- Acceptance Criteria.
```

Vẫn giữ các message khác:

```text
- Vui lòng chọn khu vực/tòa.
- Vui lòng nhập tên khu vực/tòa mới.
- Vui lòng nhập vị trí cụ thể.
- Khu vực này đã tồn tại.
- Vị trí này đã tồn tại trong khu vực đã chọn.
- Khu vực này đang ngừng hoạt động.
- Không tìm thấy vị trí Gallery.
- Bạn không có quyền thao tác vị trí này.
- Không thể hiển thị bài đăng vì vị trí đang ngừng hoạt động.
```

---

## 17. Checklist cho AI Agent

```text
[ ] Đọc database mới nhất trước khi sửa.
[ ] Xóa UNIQUE KEY uq_gallery_items_location (location_id) khỏi full SQL.
[ ] Nếu DB đã chạy, tạo patch DROP INDEX uq_gallery_items_location.
[ ] Giữ index thường idx_gallery_items_location_status.
[ ] Xóa backend validation chặn location đã có item.
[ ] Xóa message “Vị trí này đã có bài đăng Gallery.”.
[ ] Cho phép nhiều gallery_items có cùng location_id.
[ ] Dropdown location khi upload Gallery vẫn trả location ACTIVE dù location đã có item.
[ ] Trang Quản lý Gallery hiển thị 1 row cho mỗi gallery item.
[ ] Nếu nhiều item cùng location thì list hiển thị nhiều row cùng khu vực/vị trí.
[ ] Edit gallery item vẫn sửa theo gallery_item_id, không theo location_id.
[ ] Toggle gallery item vẫn update đúng một gallery_item_id.
[ ] Disable gallery item chỉ ảnh hưởng đúng item đó.
[ ] Trang Quản lý khu vực vẫn hiển thị 1 row cho mỗi location.
[ ] Location list dùng aggregate count, không join làm lặp row location.
[ ] DTO location list đổi từ galleryItemId/galleryItemStatus sang galleryItemCount/publishedGalleryItemCount/hiddenGalleryItemCount nếu code đang dùng số ít.
[ ] Disable location phải set tất cả gallery item PUBLISHED thuộc location đó về HIDDEN.
[ ] Enable location không tự publish lại bất kỳ gallery item nào.
[ ] Location inactive thì tất cả item thuộc location đó có badge “Vị trí ngừng hoạt động”.
[ ] Location inactive thì toggle của tất cả item thuộc location đó bị disabled.
[ ] Backend chặn set item PUBLISHED nếu location/area inactive.
[ ] Public Gallery hỗ trợ nhiều gallery item trong cùng một location.
[ ] Build backend.
[ ] Build frontend.
[ ] Test create nhiều item cùng location.
[ ] Test disable location có nhiều item.
[ ] Test enable location không publish lại item.
[ ] Test Quản lý khu vực không duplicate row location.
[ ] Test Public Gallery khi location có nhiều item PUBLISHED.
```

---

## 18. Kết luận

Chỉ thay đổi phần ràng buộc và các rule đang suy ra từ ràng buộc cũ.

Bỏ nghiệp vụ:

```text
1 location = tối đa 1 gallery item.
```

Chốt mới:

```text
1 location = có thể có nhiều gallery item.
```

Giữ nguyên:

```text
- Quản lý khu vực vẫn quản lý area/location.
- Tạo khu vực/vị trí vẫn như hiện tại.
- Quản lý Gallery vẫn tạo item theo form hiện tại.
- Bảng Quản lý Gallery vẫn hiển thị theo từng gallery item.
- Toggle gallery item vẫn theo từng gallery item.
- Toggle location vẫn theo location.
- Public visibility vẫn là area ACTIVE + location ACTIVE + gallery item PUBLISHED.
```

Bắt buộc sửa:

```text
- Xóa UNIQUE KEY uq_gallery_items_location.
- Xóa backend validation chặn location đã có item.
- Xóa frontend disable location vì đã có item.
- Disable location phải xử lý tất cả item thuộc location, không chỉ một item.
- DTO/list Quản lý khu vực không dùng galleryItemId/galleryItemStatus dạng số ít nữa.
```
