# PROMPT / ĐẶC TẢ CẬP NHẬT — Gallery Area/Location Cover Image và Gallery Item Type

## 0. Mục tiêu của tài liệu

Tài liệu này dùng cho AI Agent đọc và cập nhật code/database của dự án PEMS theo yêu cầu mới:

```text
1. Trang Quản lý khu vực:
   - Khi tạo khu vực mới, bắt buộc upload 1 ảnh đại diện khu vực.
   - Khi tạo vị trí cụ thể mới, bắt buộc upload 1 ảnh đại diện vị trí.
   - Ảnh khu vực/vị trí là ảnh master data, không phải gallery item.

2. Trang Quản lý Gallery:
   - Khi thêm/sửa gallery item, bắt buộc chọn loại nội dung.
   - Loại nội dung gồm:
     + Media
     + Đoàn khách
   - Dùng để phân biệt gallery item nào là media giới thiệu vị trí, gallery item nào là ảnh/video đoàn khách.

3. Không link gallery item Đoàn khách với visit_instance ở phase này.

4. Chưa sửa UI Public VisitFPTU Gallery ở phase này.
```

AI Agent phải đọc kỹ database mới nhất trước khi code, không code theo trí nhớ, không mock data, không sinh file rác.

---

## 1. Bối cảnh hiện tại

Dự án PEMS hiện đã có module Gallery theo model chuẩn hóa:

```text
campuses
  └── gallery_areas
        └── gallery_locations
              └── gallery_items
                    └── gallery_item_media
                          └── files
```

Ý nghĩa hiện tại:

```text
gallery_areas
→ Khu vực / Tòa / Khu lớn, ví dụ TÒA ALPHA, TÒA DELTA, KHU THỂ THAO.

gallery_locations
→ Vị trí cụ thể thuộc khu vực, ví dụ Trước tòa, Thư viện, Sảnh chính.

gallery_items
→ Một bài đăng Gallery thuộc một location.

gallery_item_media
→ Danh sách file ảnh/video thuộc gallery item.

files
→ Metadata file dùng chung, file thật lưu qua upload service/Google Drive.
```

Nghiệp vụ hiện tại đã chốt:

```text
1 campus có nhiều area.
1 area có nhiều location.
1 location có nhiều gallery item.
1 gallery item có nhiều media.
Mỗi gallery item có đúng 1 primary media.
```

Hiện DB chưa có:

```text
- Ảnh đại diện khu vực.
- Ảnh đại diện vị trí.
- Field phân biệt gallery item loại Media hay Đoàn khách.
```

Do đó cần bổ sung DB + backend + frontend.

---

## 2. Phạm vi cập nhật

### 2.1. Trong scope

Cập nhật:

```text
1. Database:
   - gallery_areas thêm cover_file_id.
   - gallery_locations thêm cover_file_id.
   - gallery_items thêm item_type.

2. Backend:
   - Cập nhật entity/configuration/migration/full SQL.
   - Cập nhật create/update/list/detail của Quản lý khu vực.
   - Cập nhật create/update/list/detail/filter của Quản lý Gallery.
   - Cập nhật upload ảnh khu vực/vị trí qua IFileUploadService.
   - Validate bắt buộc ảnh khu vực/vị trí theo từng mode.
   - Validate bắt buộc item_type khi tạo/sửa gallery item.

3. Frontend:
   - Cập nhật modal thêm/sửa khu vực/vị trí để upload ảnh.
   - Cập nhật modal thêm/sửa Gallery để chọn Loại nội dung.
   - Cập nhật list/detail/filter Gallery để hiển thị item_type.
   - Cập nhật list/detail location để hiển thị ảnh đại diện nếu phù hợp.

4. Test:
   - Test DB patch.
   - Test API Postman.
   - Test UI create/update/list/filter.
```

### 2.2. Không nằm trong scope

Không làm các phần sau ở phase này:

```text
1. Không link gallery item Đoàn khách với visit_instance.
2. Không thêm visit_instance_id vào gallery_items.
3. Không sửa UI Public VisitFPTU Gallery.
4. Không thêm tab/filter public theo item_type.
5. Không thay đổi public flow hiện tại.
6. Không tạo bảng mới gallery_area_media/gallery_location_media.
7. Không cho nhiều ảnh đại diện khu vực/vị trí.
8. Không xóa file cũ khỏi Google Drive khi thay ảnh đại diện.
9. Không dùng lại bảng cũ galleries/gallery_images.
```

---

## 3. Chốt nghiệp vụ mới

## 3.1. Ảnh đại diện khu vực

Khu vực cần có 1 ảnh đại diện.

Ví dụ:

```text
TÒA ALPHA
→ ảnh tổng quan tòa Alpha.

TÒA DELTA
→ ảnh tổng quan tòa Delta.

KHU THỂ THAO
→ ảnh khu thể thao.
```

Ảnh này thuộc master data `gallery_areas`, không phải gallery item.

Lưu tại:

```text
gallery_areas.cover_file_id
```

---

## 3.2. Ảnh đại diện vị trí

Vị trí cụ thể cần có 1 ảnh đại diện.

Ví dụ:

```text
TÒA ALPHA / Trước tòa
→ ảnh mặt trước tòa Alpha.

TÒA DELTA / Thư viện
→ ảnh thư viện.

TÒA DELTA / Sảnh chính
→ ảnh sảnh chính.
```

Ảnh này thuộc master data `gallery_locations`, không phải gallery item.

Lưu tại:

```text
gallery_locations.cover_file_id
```

---

## 3.3. Loại nội dung Gallery item

Gallery item cần có field phân biệt loại nội dung:

```text
MEDIA
VISIT_DELEGATION
```

Mapping UI:

| DB value | UI label | Ý nghĩa |
|---|---|---|
| MEDIA | Media | Ảnh/video giới thiệu không gian, cơ sở vật chất, vật thể, phòng học, thư viện, sảnh, khu vực tại vị trí |
| VISIT_DELEGATION | Đoàn khách | Ảnh/video đoàn khách đã tới thăm và chụp tại vị trí đó |

Lưu tại:

```text
gallery_items.item_type
```

---

## 3.4. Không dùng media_kind cho item_type

Không được dùng `media_kind` để phân biệt Media/Đoàn khách.

`media_kind` vẫn giữ nhiệm vụ cũ:

```text
IMAGE
VIDEO
MIXED
```

Ý nghĩa:

```text
item_type
→ Nội dung này là Media hay Đoàn khách.

media_kind
→ File trong item là ảnh, video hay hỗn hợp.
```

Ví dụ:

```text
item_type = VISIT_DELEGATION
media_kind = IMAGE
→ Một gallery item đoàn khách, chỉ chứa ảnh.

item_type = MEDIA
media_kind = VIDEO
→ Một gallery item giới thiệu vị trí, chỉ chứa video.

item_type = VISIT_DELEGATION
media_kind = MIXED
→ Một gallery item đoàn khách, có cả ảnh và video.
```

---

## 4. Database changes

## 4.1. Thêm `cover_file_id` vào `gallery_areas`

### SQL patch

```sql
ALTER TABLE gallery_areas
ADD COLUMN cover_file_id BIGINT UNSIGNED NULL
  COMMENT 'Ảnh đại diện khu vực/tòa/khu lớn'
  AFTER area_key,
ADD KEY idx_gallery_areas_cover_file (cover_file_id),
ADD CONSTRAINT fk_gallery_areas_cover_file
  FOREIGN KEY (cover_file_id) REFERENCES files(file_id)
  ON UPDATE CASCADE ON DELETE RESTRICT;
```

### Ghi chú

Để `NULL` ở DB để không làm hỏng dữ liệu seed cũ.

Backend/UI phải validate bắt buộc khi tạo khu vực mới qua màn Quản lý khu vực.

---

## 4.2. Thêm `cover_file_id` vào `gallery_locations`

### SQL patch

```sql
ALTER TABLE gallery_locations
ADD COLUMN cover_file_id BIGINT UNSIGNED NULL
  COMMENT 'Ảnh đại diện vị trí cụ thể'
  AFTER location_key,
ADD KEY idx_gallery_locations_cover_file (cover_file_id),
ADD CONSTRAINT fk_gallery_locations_cover_file
  FOREIGN KEY (cover_file_id) REFERENCES files(file_id)
  ON UPDATE CASCADE ON DELETE RESTRICT;
```

### Ghi chú

Để `NULL` ở DB để không làm hỏng dữ liệu seed cũ.

Backend/UI phải validate bắt buộc khi tạo location mới qua màn Quản lý khu vực.

---

## 4.3. Thêm `item_type` vào `gallery_items`

### SQL patch

```sql
ALTER TABLE gallery_items
ADD COLUMN item_type ENUM('MEDIA','VISIT_DELEGATION') NOT NULL DEFAULT 'MEDIA'
  COMMENT 'MEDIA=ảnh/video giới thiệu vị trí; VISIT_DELEGATION=ảnh/video đoàn khách'
  AFTER description,
ADD KEY idx_gallery_items_item_type (item_type, status, deleted_at);
```

### Ghi chú

Dùng `DEFAULT 'MEDIA'` để dữ liệu cũ không bị lỗi.

Các gallery item cũ mặc định được hiểu là `MEDIA`.

---

## 4.4. Không thêm `visit_instance_id`

Không thêm các field sau ở phase này:

```text
gallery_items.visit_instance_id
gallery_items.visit_request_id
gallery_items.delegation_id
```

Lý do:

```text
User chưa cần link gallery item Đoàn khách với visit instance.
Phase này chỉ cần phân biệt item_type = MEDIA / VISIT_DELEGATION.
```

---

## 4.5. Full SQL cần cập nhật

Nếu đang sửa file full SQL thay vì migration patch, cần cập nhật:

### `CREATE TABLE gallery_areas`

Thêm:

```sql
cover_file_id BIGINT UNSIGNED NULL COMMENT 'Ảnh đại diện khu vực/tòa/khu lớn',
KEY idx_gallery_areas_cover_file (cover_file_id),
CONSTRAINT fk_gallery_areas_cover_file
  FOREIGN KEY (cover_file_id) REFERENCES files(file_id)
  ON UPDATE CASCADE ON DELETE RESTRICT
```

### `CREATE TABLE gallery_locations`

Thêm:

```sql
cover_file_id BIGINT UNSIGNED NULL COMMENT 'Ảnh đại diện vị trí cụ thể',
KEY idx_gallery_locations_cover_file (cover_file_id),
CONSTRAINT fk_gallery_locations_cover_file
  FOREIGN KEY (cover_file_id) REFERENCES files(file_id)
  ON UPDATE CASCADE ON DELETE RESTRICT
```

### `CREATE TABLE gallery_items`

Thêm:

```sql
item_type ENUM('MEDIA','VISIT_DELEGATION') NOT NULL DEFAULT 'MEDIA'
  COMMENT 'MEDIA=ảnh/video giới thiệu vị trí; VISIT_DELEGATION=ảnh/video đoàn khách',
KEY idx_gallery_items_item_type (item_type, status, deleted_at)
```

---

## 5. File purpose / Upload purpose

Nếu hệ thống đang có enum `FilePurpose`, nên bổ sung purpose mới:

```text
GALLERY_AREA_COVER
GALLERY_LOCATION_COVER
GALLERY_IMAGE
GALLERY_VIDEO
```

Mapping:

```text
Ảnh đại diện khu vực
→ FilePurpose.GALLERY_AREA_COVER

Ảnh đại diện vị trí
→ FilePurpose.GALLERY_LOCATION_COVER

Gallery item image
→ FilePurpose.GALLERY_IMAGE

Gallery item video
→ FilePurpose.GALLERY_VIDEO
```

Nếu enum hiện tại đang dùng naming khác như `GalleryImage`, `GalleryVideo`, hãy theo convention hiện có của project, nhưng vẫn cần tách purpose cho area cover và location cover nếu có thể.

Không dùng `FilePurpose.Document` cho ảnh Gallery.

Không gọi trực tiếp `IGoogleDriveStorageService`.

Tất cả upload phải đi qua:

```text
IFileUploadService
```

---

## 6. Backend — Quản lý khu vực

## 6.1. Endpoint create location/khu vực phải chuyển sang multipart

Endpoint hiện tại nếu đang nhận JSON thì cần đổi sang `multipart/form-data` vì có upload ảnh.

```http
POST /api/gallery-management/locations
Content-Type: multipart/form-data
```

### Form fields

```text
mode
areaId
newAreaName
locationName
areaCoverImage
locationCoverImage
```

Trong đó:

```text
mode = EXISTING_AREA hoặc NEW_AREA
```

---

## 6.2. Mode `EXISTING_AREA` — Thêm vị trí vào khu vực có sẵn

### Request

```text
mode = EXISTING_AREA
areaId = 3
newAreaName = null
locationName = Sảnh chính
areaCoverImage = null
locationCoverImage = file ảnh
```

### Validation

```text
1. User phải là Staff Leader active.
2. currentUser.primary_campus_id không được null.
3. mode phải là EXISTING_AREA.
4. areaId bắt buộc.
5. area phải tồn tại.
6. area phải thuộc campus của Staff Leader.
7. area.status phải ACTIVE.
8. locationName bắt buộc, trim không được rỗng.
9. locationName tối đa theo DB hiện tại, ví dụ 150 ký tự.
10. Normalize locationName thành location_key.
11. Không được trùng location_key trong cùng area.
12. locationCoverImage bắt buộc.
13. locationCoverImage chỉ được đúng 1 file.
14. locationCoverImage phải là image.
15. Không nhận video/document/PDF.
```

### Backend flow

```text
1. Nhận multipart request.
2. Xác thực user.
3. Kiểm tra role:
   role_code = STAFF
   sub_role = LEADER
   status = ACTIVE

4. Lấy currentUser.primary_campus_id.
5. Validate mode = EXISTING_AREA.
6. Load area theo areaId.
7. Check area.campus_id = currentUser.primary_campus_id.
8. Check area.status = ACTIVE.
9. Validate locationName.
10. Normalize locationName thành location_key.
11. Check duplicate:
    gallery_locations.area_id = areaId
    gallery_locations.location_key = normalized key

12. Validate locationCoverImage.
13. Upload locationCoverImage qua IFileUploadService với purpose GALLERY_LOCATION_COVER.
14. Lấy uploaded.FileId.
15. Insert gallery_locations:
    - area_id = areaId
    - location_name = locationName đã trim
    - location_key = normalized key
    - cover_file_id = uploaded.FileId
    - status = ACTIVE
    - display_order nếu project đang dùng
    - created_at = now
    - created_by = currentUserId

16. Ghi audit log nếu project đang dùng audit.
17. Trả response location vừa tạo.
```

### Response đề xuất

```json
{
  "locationId": 25,
  "areaId": 3,
  "areaName": "TÒA DELTA",
  "areaCoverFileId": 1101,
  "areaCoverUrl": "/api/files/1101/content",
  "locationName": "Sảnh chính",
  "locationCoverFileId": 1201,
  "locationCoverUrl": "/api/files/1201/content",
  "status": "ACTIVE",
  "createdAt": "2026-07-02T23:00:00"
}
```

---

## 6.3. Mode `NEW_AREA` — Tạo khu vực mới kèm vị trí đầu tiên

### Request

```text
mode = NEW_AREA
areaId = null
newAreaName = TÒA GAMMA
areaCoverImage = file ảnh khu vực
locationName = Sảnh chính
locationCoverImage = file ảnh vị trí
```

### Validation

```text
1. User phải là Staff Leader active.
2. currentUser.primary_campus_id không được null.
3. mode phải là NEW_AREA.
4. newAreaName bắt buộc, trim không được rỗng.
5. newAreaName tối đa theo DB hiện tại, ví dụ 150 ký tự.
6. Normalize newAreaName thành area_key.
7. Không được trùng area_key trong cùng campus.
8. areaCoverImage bắt buộc.
9. areaCoverImage chỉ được đúng 1 file.
10. areaCoverImage phải là image.
11. locationName bắt buộc, trim không được rỗng.
12. Normalize locationName thành location_key.
13. locationCoverImage bắt buộc.
14. locationCoverImage chỉ được đúng 1 file.
15. locationCoverImage phải là image.
16. Không nhận video/document/PDF ở 2 field cover image.
```

### Backend flow

```text
1. Nhận multipart request.
2. Xác thực user.
3. Kiểm tra Staff Leader active.
4. Lấy currentUser.primary_campus_id.
5. Validate mode = NEW_AREA.
6. Validate newAreaName.
7. Normalize newAreaName thành area_key.
8. Check duplicate:
   gallery_areas.campus_id = currentUser.primary_campus_id
   gallery_areas.area_key = normalized area key

9. Validate locationName.
10. Normalize locationName thành location_key.
11. Validate areaCoverImage.
12. Validate locationCoverImage.
13. Bắt đầu transaction.
14. Upload areaCoverImage qua IFileUploadService với purpose GALLERY_AREA_COVER.
15. Upload locationCoverImage qua IFileUploadService với purpose GALLERY_LOCATION_COVER.
16. Insert gallery_areas:
    - campus_id = currentUser.primary_campus_id
    - area_name = newAreaName đã trim
    - area_key = normalized area key
    - cover_file_id = uploaded area file id
    - status = ACTIVE
    - display_order nếu project đang dùng
    - created_at = now
    - created_by = currentUserId

17. Insert gallery_locations:
    - area_id = area vừa tạo
    - location_name = locationName đã trim
    - location_key = normalized location key
    - cover_file_id = uploaded location file id
    - status = ACTIVE
    - display_order nếu project đang dùng
    - created_at = now
    - created_by = currentUserId

18. Commit transaction.
19. Ghi audit nếu project đang dùng audit.
20. Trả response gồm area + location vừa tạo.
```

### Response đề xuất

```json
{
  "area": {
    "areaId": 9,
    "areaName": "TÒA GAMMA",
    "status": "ACTIVE",
    "coverFileId": 1201,
    "coverUrl": "/api/files/1201/content"
  },
  "location": {
    "locationId": 30,
    "locationName": "Sảnh chính",
    "status": "ACTIVE",
    "coverFileId": 1202,
    "coverUrl": "/api/files/1202/content"
  }
}
```

---

## 6.4. Update location/khu vực

Endpoint:

```http
PUT /api/gallery-management/locations/{locationId}
Content-Type: multipart/form-data
```

### Form fields

```text
mode
areaId
newAreaName
locationName
areaCoverImage
locationCoverImage
```

---

## 6.5. Update mode `EXISTING_AREA`

Dùng khi:

```text
- Đổi tên vị trí.
- Chuyển vị trí sang area có sẵn.
- Có thể thay ảnh đại diện vị trí.
```

### Request

```text
mode = EXISTING_AREA
areaId = 3
newAreaName = null
locationName = Sảnh chính mới
areaCoverImage = null
locationCoverImage = optional
```

### Rule

```text
1. Nếu không upload locationCoverImage mới:
   - giữ nguyên gallery_locations.cover_file_id cũ.

2. Nếu upload locationCoverImage mới:
   - validate đúng 1 ảnh.
   - upload qua IFileUploadService.
   - update gallery_locations.cover_file_id = fileId mới.

3. Không update gallery_items.status khi edit location.
4. Không xóa file ảnh cũ khỏi Google Drive ở phase này.
```

### Backend flow

```text
1. Kiểm tra Staff Leader active.
2. Load location hiện tại theo locationId.
3. Join gallery_areas để check location thuộc campus của Staff Leader.
4. Validate areaId mới tồn tại và thuộc cùng campus.
5. Validate area mới ACTIVE nếu rule hiện tại yêu cầu.
6. Validate locationName.
7. Normalize locationName thành location_key.
8. Check duplicate location_key trong area đích, bỏ qua chính location hiện tại.
9. Nếu có locationCoverImage:
   - validate image.
   - upload.
   - lấy fileId mới.
10. Update gallery_locations:
    - area_id = areaId
    - location_name
    - location_key
    - cover_file_id nếu có ảnh mới
    - updated_by
    - updated_at
11. Ghi audit nếu có.
12. Trả detail mới.
```

---

## 6.6. Update mode `NEW_AREA`

Dùng khi:

```text
- Tạo area mới trong lúc edit location.
- Chuyển location hiện tại sang area mới.
```

### Request

```text
mode = NEW_AREA
areaId = null
newAreaName = TÒA OMEGA
areaCoverImage = bắt buộc
locationName = Sảnh chính
locationCoverImage = optional
```

### Rule

```text
1. newAreaName bắt buộc.
2. areaCoverImage bắt buộc vì đang tạo area mới.
3. locationCoverImage optional.
4. Nếu không upload locationCoverImage mới:
   - giữ nguyên ảnh vị trí cũ.
5. Nếu upload locationCoverImage mới:
   - thay gallery_locations.cover_file_id.
6. Tạo area mới + update location phải nằm trong cùng transaction.
```

### Backend flow

```text
1. Kiểm tra Staff Leader active.
2. Load location hiện tại và check campus scope.
3. Validate newAreaName.
4. Normalize newAreaName thành area_key.
5. Check duplicate area_key trong campus.
6. Validate areaCoverImage bắt buộc.
7. Validate locationName.
8. Normalize locationName.
9. Nếu có locationCoverImage thì validate.
10. Bắt đầu transaction.
11. Upload areaCoverImage.
12. Insert gallery_areas mới với cover_file_id.
13. Nếu có locationCoverImage:
    - upload ảnh vị trí mới
    - lấy fileId mới
14. Update gallery_locations:
    - area_id = area mới
    - location_name
    - location_key
    - cover_file_id nếu có ảnh vị trí mới, nếu không giữ cũ
    - updated_by
    - updated_at
15. Commit transaction.
16. Trả detail mới.
```

---

## 6.7. List locations

Endpoint:

```http
GET /api/gallery-management/locations
```

Cần trả thêm ảnh đại diện area/location.

### Response list item đề xuất

```json
{
  "locationId": 25,
  "areaId": 3,
  "areaName": "TÒA DELTA",
  "areaCoverFileId": 1101,
  "areaCoverUrl": "/api/files/1101/content",
  "locationName": "Sảnh chính",
  "locationCoverFileId": 1201,
  "locationCoverUrl": "/api/files/1201/content",
  "status": "ACTIVE",
  "galleryItemCount": 4,
  "publishedGalleryItemCount": 2,
  "hiddenGalleryItemCount": 2,
  "createdAt": "2026-07-02T23:00:00",
  "updatedAt": null
}
```

### Query lưu ý

Do hiện tại một location có nhiều gallery item, list location không được join trực tiếp làm duplicate row.

Sai:

```sql
SELECT *
FROM gallery_locations gl
LEFT JOIN gallery_items gi ON gi.location_id = gl.location_id
```

Đúng: dùng aggregate count.

```sql
SELECT
    gl.location_id,
    ga.area_id,
    ga.area_name,
    ga.cover_file_id AS area_cover_file_id,
    gl.location_name,
    gl.cover_file_id AS location_cover_file_id,
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
WHERE ga.campus_id = @CurrentUserCampusId
GROUP BY
    gl.location_id,
    ga.area_id,
    ga.area_name,
    ga.cover_file_id,
    gl.location_name,
    gl.cover_file_id,
    gl.status,
    gl.created_at,
    gl.updated_at
ORDER BY gl.created_at DESC, gl.location_id DESC;
```

Backend map `cover_file_id` thành URL qua file proxy:

```text
/api/files/{fileId}/content
```

Không trả Google Drive raw link.

---

## 6.8. Detail location

Nên có hoặc cập nhật endpoint detail:

```http
GET /api/gallery-management/locations/{locationId}
```

Response đề xuất:

```json
{
  "area": {
    "areaId": 3,
    "areaName": "TÒA DELTA",
    "coverFileId": 1101,
    "coverUrl": "/api/files/1101/content"
  },
  "location": {
    "locationId": 25,
    "locationName": "Sảnh chính",
    "status": "ACTIVE",
    "coverFileId": 1201,
    "coverUrl": "/api/files/1201/content"
  },
  "gallerySummary": {
    "galleryItemCount": 4,
    "mediaCount": 2,
    "visitDelegationCount": 2,
    "publishedGalleryItemCount": 2,
    "hiddenGalleryItemCount": 2
  }
}
```

---

## 6.9. Enable/disable location giữ nguyên

Không đổi nghiệp vụ enable/disable location.

### Disable location

```text
1. Set gallery_locations.status = INACTIVE.
2. Tất cả gallery item PUBLISHED thuộc location đó chuyển về HIDDEN.
3. Không xóa gallery item.
4. Không xóa gallery_item_media.
5. Không xóa file Google Drive.
6. Không update gallery_areas.status.
7. Update location + hide item phải trong cùng transaction.
```

### Enable location

```text
1. Set gallery_locations.status = ACTIVE.
2. Không tự publish lại gallery item.
3. Gallery item vẫn HIDDEN cho tới khi Staff Leader bật lại ở Quản lý Gallery.
```

---

## 7. Backend — Quản lý Gallery

## 7.1. Thêm enum/domain value item type

Tạo enum hoặc constants:

```csharp
public enum GalleryItemType
{
    Media,
    VisitDelegation
}
```

Hoặc nếu project dùng string constants:

```csharp
public static class GalleryItemTypes
{
    public const string Media = "MEDIA";
    public const string VisitDelegation = "VISIT_DELEGATION";
}
```

DB value bắt buộc:

```text
MEDIA
VISIT_DELEGATION
```

UI label:

```text
MEDIA → Media
VISIT_DELEGATION → Đoàn khách
```

---

## 7.2. Create Gallery item

Endpoint hiện tại:

```http
POST /api/gallery-management/items
Content-Type: multipart/form-data
```

### Form fields mới

Thêm:

```text
itemType
```

### Request đầy đủ

```text
title
description
locationId
itemType
status
files[]
caption optional
altText optional
```

### Validation

```text
1. User phải là Staff Leader active.
2. currentUser.primary_campus_id không được null.
3. title bắt buộc.
4. description bắt buộc.
5. locationId bắt buộc.
6. location phải tồn tại.
7. location phải thuộc campus của Staff Leader.
8. area.status phải ACTIVE.
9. location.status phải ACTIVE.
10. itemType bắt buộc.
11. itemType chỉ nhận MEDIA hoặc VISIT_DELEGATION.
12. status nếu truyền chỉ nhận PUBLISHED hoặc HIDDEN.
13. files[] bắt buộc khi tạo mới.
14. files[] tối thiểu 1 file.
15. files[] tối đa theo rule hiện tại, ví dụ 5 file.
16. file phải đúng định dạng image/video theo rule hiện tại.
```

### Backend flow

```text
1. Nhận multipart/form-data.
2. Xác thực user.
3. Check Staff Leader active.
4. Lấy currentUser.primary_campus_id.
5. Validate title/description/locationId/itemType/status/files.
6. Load location join area.
7. Check area.campus_id = currentUser.primary_campus_id.
8. Check area.status = ACTIVE.
9. Check location.status = ACTIVE.
10. Upload từng file qua IFileUploadService:
    - image → GALLERY_IMAGE
    - video → GALLERY_VIDEO

11. Backend tự tính media_kind:
    - toàn ảnh → IMAGE
    - toàn video → VIDEO
    - có cả ảnh và video → MIXED

12. Bắt đầu transaction nếu chưa có.
13. Insert gallery_items:
    - location_id
    - title
    - description
    - item_type = request.itemType
    - media_kind = calculated media kind
    - status = request.status hoặc default PUBLISHED
    - display_order nếu project đang dùng
    - created_by
    - created_at

14. Insert gallery_item_media cho từng file:
    - gallery_item_id
    - file_id
    - media_type
    - thumbnail_file_id nếu có
    - caption
    - alt_text
    - is_primary = true cho file đầu tiên
    - is_primary = false cho các file sau
    - display_order
    - status = ACTIVE

15. Commit transaction.
16. Ghi audit nếu có.
17. Trả detail gallery item.
```

### Response detail đề xuất

```json
{
  "galleryItemId": 88,
  "title": "Đoàn ABC tham quan thư viện Delta",
  "description": "...",
  "itemType": "VISIT_DELEGATION",
  "itemTypeLabel": "Đoàn khách",
  "mediaKind": "IMAGE",
  "status": "PUBLISHED",
  "area": {
    "areaId": 3,
    "areaName": "TÒA DELTA"
  },
  "location": {
    "locationId": 15,
    "locationName": "Thư viện"
  },
  "media": [
    {
      "mediaId": 301,
      "fileId": 9001,
      "mediaType": "IMAGE",
      "url": "/api/files/9001/content",
      "isPrimary": true,
      "displayOrder": 0
    }
  ]
}
```

---

## 7.3. Edit Gallery item

Endpoint hiện tại:

```http
PUT /api/gallery-management/items/{galleryItemId}
Content-Type: multipart/form-data
```

### Form fields cần có

Thêm:

```text
itemType
```

Request đầy đủ:

```text
title
description
locationId
itemType
keepMediaIds[]
newFiles[]
primaryMediaId
```

### Rule

```text
1. itemType bắt buộc khi edit.
2. Cho phép đổi MEDIA ↔ VISIT_DELEGATION.
3. Đổi itemType không làm đổi media_kind.
4. media_kind vẫn tính lại từ media active sau edit.
5. Không cần visit_instance.
6. Không ảnh hưởng Public Gallery phase này.
```

### Backend flow bổ sung

Trong handler edit hiện tại, thêm:

```text
1. Validate itemType.
2. Update gallery_items.item_type = request.itemType.
3. Các rule media cũ giữ nguyên:
   - giữ media theo keepMediaIds
   - thêm newFiles
   - đảm bảo còn ít nhất 1 media active
   - đảm bảo đúng 1 primary media
   - tính lại media_kind
```

---

## 7.4. Change Gallery item status

Endpoint hiện tại:

```http
PATCH /api/gallery-management/items/{galleryItemId}/status
```

Không cần đổi request nếu chỉ đổi status.

Nhưng khi trả response nên có thêm:

```json
{
  "itemType": "MEDIA",
  "itemTypeLabel": "Media"
}
```

Rule cũ vẫn giữ:

```text
Không cho set PUBLISHED nếu area/location inactive.
```

---

## 7.5. List Gallery items

Endpoint:

```http
GET /api/gallery-management/items
```

### Query params cần bổ sung

```text
itemType
```

Ví dụ:

```http
GET /api/gallery-management/items?itemType=VISIT_DELEGATION
```

### Filter rule

```text
itemType null hoặc empty
→ không filter theo item_type.

itemType = MEDIA
→ chỉ trả gallery_items.item_type = MEDIA.

itemType = VISIT_DELEGATION
→ chỉ trả gallery_items.item_type = VISIT_DELEGATION.

itemType khác
→ HTTP 422 hoặc ignore theo convention hiện có, khuyến nghị HTTP 422.
```

### Query logic

```sql
AND (@ItemType IS NULL OR gi.item_type = @ItemType)
```

### Response list item thêm field

```json
{
  "galleryItemId": 88,
  "areaId": 3,
  "areaName": "TÒA DELTA",
  "locationId": 15,
  "locationName": "Thư viện",
  "itemType": "VISIT_DELEGATION",
  "itemTypeLabel": "Đoàn khách",
  "title": "Đoàn ABC tham quan thư viện",
  "description": "...",
  "mediaKind": "IMAGE",
  "status": "PUBLISHED",
  "createdAt": "2026-07-02T23:00:00",
  "primaryMedia": {
    "mediaId": 301,
    "fileId": 9001,
    "mediaType": "IMAGE",
    "fileUrl": "/api/files/9001/content"
  }
}
```

---

## 7.6. Detail Gallery item

Endpoint:

```http
GET /api/gallery-management/items/{galleryItemId}
```

Response thêm:

```json
{
  "itemType": "VISIT_DELEGATION",
  "itemTypeLabel": "Đoàn khách"
}
```

Detail modal cần hiển thị:

```text
Loại nội dung: Media
```

hoặc:

```text
Loại nội dung: Đoàn khách
```

Không hiển thị visit instance.

---

## 8. Frontend — Quản lý khu vực

## 8.1. Modal create — Mode `Khu vực có sẵn`

UI form:

```text
Khu vực có sẵn *
[Dropdown chọn khu vực]

Vị trí cụ thể *
[Input tên vị trí]

Ảnh đại diện vị trí *
[Upload 1 ảnh]

Button:
Hủy
Tạo mới
```

### Frontend validation

```text
- areaId bắt buộc.
- locationName bắt buộc.
- locationCoverImage bắt buộc.
- locationCoverImage chỉ được đúng 1 file.
- Chỉ accept image/* hoặc whitelist JPG/JPEG/PNG/WEBP.
- Không cho submit nếu thiếu ảnh.
```

### UX gợi ý

Upload field nên hiển thị:

```text
- Preview ảnh.
- Nút thay ảnh.
- Text "Chỉ upload 1 ảnh".
```

---

## 8.2. Modal create — Mode `Khu vực mới`

UI form:

```text
Tên khu vực mới *
[Input]

Ảnh đại diện khu vực *
[Upload 1 ảnh]

Vị trí cụ thể *
[Input]

Ảnh đại diện vị trí *
[Upload 1 ảnh]

Button:
Hủy
Tạo mới
```

### Frontend validation

```text
- newAreaName bắt buộc.
- areaCoverImage bắt buộc.
- areaCoverImage đúng 1 ảnh.
- locationName bắt buộc.
- locationCoverImage bắt buộc.
- locationCoverImage đúng 1 ảnh.
```

---

## 8.3. Modal edit location

Khi edit location:

```text
- Hiển thị ảnh đại diện vị trí hiện tại.
- Cho phép upload ảnh mới để thay.
- Nếu không upload ảnh mới, giữ ảnh cũ.
```

Nếu chọn `Khu vực mới` trong edit:

```text
- Bắt buộc upload ảnh đại diện khu vực mới.
```

Nếu chọn `Khu vực có sẵn`:

```text
- Không cần upload ảnh khu vực.
```

---

## 8.4. List location

Table hiện tại có thể thêm thumbnail nhỏ nếu phù hợp:

```text
STT
Ảnh vị trí
Khu vực
Vị trí cụ thể
Trạng thái
Số gallery item
Ngày tạo
Hành động
```

Nếu không muốn đổi layout nhiều, ít nhất detail/edit modal phải hiển thị ảnh.

---

## 9. Frontend — Quản lý Gallery

## 9.1. Modal create Gallery item

Form sau khi sửa:

```text
Tiêu đề *
Danh mục tòa/khu *
Vị trí thực tế *
Loại nội dung *
  - Media
  - Đoàn khách
Định dạng *
  - Hình ảnh
  - Video
  - Hỗn hợp nếu UI hiện tại hỗ trợ
Mô tả *
Files *
```

### Field mới

```text
Loại nội dung *
```

Options:

```text
Media
Đoàn khách
```

Mapping gửi backend:

```text
Media → MEDIA
Đoàn khách → VISIT_DELEGATION
```

### Frontend validation

```text
- itemType bắt buộc.
- Chỉ gửi MEDIA hoặc VISIT_DELEGATION.
- Không gửi text tiếng Việt xuống DB/API.
```

---

## 9.2. Modal edit Gallery item

Form edit thêm field:

```text
Loại nội dung *
```

Rule:

```text
- Hiển thị đúng itemType hiện tại.
- Cho phép đổi Media ↔ Đoàn khách.
- Submit itemType cùng với các field edit khác.
```

---

## 9.3. List Gallery

Thêm cột:

```text
Loại nội dung
```

Table đề xuất:

```text
STT
Khu vực
Vị trí cụ thể
Loại nội dung
Tiêu đề
Định dạng
Trạng thái
Ngày tạo
Hành động
```

Badge:

```text
MEDIA
→ "Media"

VISIT_DELEGATION
→ "Đoàn khách"
```

---

## 9.4. Filter Gallery

Filter bar thêm:

```text
Loại nội dung:
- Tất cả
- Media
- Đoàn khách
```

Mapping query:

```text
Tất cả → không gửi itemType
Media → itemType=MEDIA
Đoàn khách → itemType=VISIT_DELEGATION
```

---

## 9.5. Detail Gallery

Detail modal hiển thị thêm:

```text
Loại nội dung: Media
```

hoặc:

```text
Loại nội dung: Đoàn khách
```

Không hiển thị visit instance.

---

## 10. Public Gallery phase này

Không sửa UI public Gallery trong phase này.

Do đó không làm:

```text
- Không thêm tab Tất cả / Media / Đoàn khách.
- Không thêm filter public theo item_type.
- Không bắt buộc public card hiển thị badge item_type.
- Không đổi route public.
- Không đổi flow click location → grid/detail hiện tại.
```

Tuy nhiên backend public API nếu đang select `gallery_items.*` thì không cần expose item_type ngay.

Nếu public DTO đang map strict fields, có thể bỏ qua `item_type` ở phase này.

Public visibility vẫn giữ:

```text
campus ACTIVE
area ACTIVE
location ACTIVE
gallery item PUBLISHED
gallery item chưa deleted
media ACTIVE
media chưa deleted
```

`item_type` không làm ảnh hưởng public visibility.

---

## 11. Error messages đề xuất

## 11.1. Quản lý khu vực

```text
Vui lòng upload ảnh đại diện khu vực.
Vui lòng upload ảnh đại diện vị trí.
Mỗi khu vực chỉ được upload 1 ảnh đại diện.
Mỗi vị trí chỉ được upload 1 ảnh đại diện.
Ảnh đại diện khu vực không đúng định dạng.
Ảnh đại diện vị trí không đúng định dạng.
Vui lòng chọn khu vực/tòa.
Vui lòng nhập tên khu vực/tòa mới.
Vui lòng nhập vị trí cụ thể.
Khu vực này đã tồn tại.
Vị trí này đã tồn tại trong khu vực đã chọn.
Khu vực này đang ngừng hoạt động.
Không tìm thấy vị trí Gallery.
Bạn không có quyền thao tác vị trí này.
Bạn không có quyền quản lý khu vực Gallery.
```

---

## 11.2. Quản lý Gallery

```text
Vui lòng chọn loại nội dung.
Loại nội dung không hợp lệ.
Vui lòng chọn Media hoặc Đoàn khách.
Vui lòng nhập tiêu đề.
Vui lòng nhập mô tả.
Vui lòng chọn vị trí.
Gallery item phải có ít nhất một file media.
Không thể hiển thị bài đăng vì vị trí đang ngừng hoạt động.
Không thể hiển thị bài đăng vì khu vực đang ngừng hoạt động.
```

---

## 12. Business Rules

```text
BR-AREA-COVER-01:
Khi tạo khu vực mới, Staff Leader bắt buộc upload đúng 1 ảnh đại diện khu vực.

BR-AREA-COVER-02:
Ảnh đại diện khu vực được lưu tại gallery_areas.cover_file_id.

BR-AREA-COVER-03:
Ảnh đại diện khu vực chỉ được là image, không được là video/document.

BR-LOCATION-COVER-01:
Khi tạo vị trí cụ thể mới, Staff Leader bắt buộc upload đúng 1 ảnh đại diện vị trí.

BR-LOCATION-COVER-02:
Ảnh đại diện vị trí được lưu tại gallery_locations.cover_file_id.

BR-LOCATION-COVER-03:
Ảnh đại diện vị trí chỉ được là image, không được là video/document.

BR-LOCATION-COVER-04:
Khi edit location, nếu không upload ảnh vị trí mới thì giữ ảnh cũ.

BR-LOCATION-COVER-05:
Khi edit location và tạo khu vực mới, bắt buộc upload ảnh đại diện khu vực mới.

BR-GALLERY-TYPE-01:
Mỗi gallery item phải có item_type.

BR-GALLERY-TYPE-02:
item_type chỉ nhận MEDIA hoặc VISIT_DELEGATION.

BR-GALLERY-TYPE-03:
item_type = MEDIA dùng cho ảnh/video giới thiệu không gian, cơ sở vật chất hoặc nội dung có tại vị trí.

BR-GALLERY-TYPE-04:
item_type = VISIT_DELEGATION dùng cho ảnh/video đoàn khách đã tới thăm và chụp tại vị trí đó.

BR-GALLERY-TYPE-05:
item_type không thay thế media_kind.

BR-GALLERY-TYPE-06:
media_kind vẫn do backend tự tính theo file upload: IMAGE, VIDEO hoặc MIXED.

BR-GALLERY-TYPE-07:
Quản lý Gallery phải hỗ trợ list/filter/detail theo item_type.

BR-GALLERY-TYPE-08:
Phase này không link gallery item đoàn khách với visit_instance.

BR-GALLERY-TYPE-09:
Phase này không sửa UI Public Gallery.

BR-GALLERY-TYPE-10:
Các gallery item cũ mặc định item_type = MEDIA.
```

---

## 13. Acceptance Criteria

## 13.1. AC-AREA-COVER-01 — Tạo khu vực mới thiếu ảnh khu vực

```text
Given Staff Leader chọn Khu vực mới
And nhập tên khu vực
And nhập vị trí cụ thể
And upload ảnh vị trí
But không upload ảnh khu vực
When bấm Tạo mới
Then hệ thống không cho submit
And báo "Vui lòng upload ảnh đại diện khu vực."
```

---

## 13.2. AC-LOCATION-COVER-01 — Tạo vị trí thiếu ảnh vị trí

```text
Given Staff Leader chọn Khu vực có sẵn
And chọn TÒA DELTA
And nhập vị trí cụ thể
But không upload ảnh vị trí
When bấm Tạo mới
Then hệ thống không cho submit
And báo "Vui lòng upload ảnh đại diện vị trí."
```

---

## 13.3. AC-AREA-LOCATION-COVER-01 — Tạo khu vực mới thành công

```text
Given Staff Leader chọn Khu vực mới
And nhập tên khu vực hợp lệ
And upload 1 ảnh khu vực hợp lệ
And nhập vị trí cụ thể hợp lệ
And upload 1 ảnh vị trí hợp lệ
When bấm Tạo mới
Then DB tạo gallery_areas với cover_file_id
And DB tạo gallery_locations với cover_file_id
And cả hai thuộc campus của Staff Leader.
```

---

## 13.4. AC-LOCATION-COVER-02 — Thêm vị trí vào khu vực có sẵn thành công

```text
Given Staff Leader chọn Khu vực có sẵn
And chọn area hợp lệ thuộc campus của mình
And nhập tên vị trí hợp lệ
And upload 1 ảnh vị trí hợp lệ
When bấm Tạo mới
Then DB tạo gallery_locations với cover_file_id
And location thuộc area đã chọn
And location có status ACTIVE.
```

---

## 13.5. AC-LOCATION-COVER-03 — Edit location giữ ảnh cũ

```text
Given location đã có cover_file_id
When Staff Leader edit tên vị trí
And không upload ảnh vị trí mới
Then hệ thống giữ nguyên gallery_locations.cover_file_id.
```

---

## 13.6. AC-LOCATION-COVER-04 — Edit location thay ảnh mới

```text
Given location đã có ảnh đại diện
When Staff Leader upload ảnh vị trí mới trong modal edit
And bấm Cập nhật
Then hệ thống upload file mới
And update gallery_locations.cover_file_id sang file mới.
```

---

## 13.7. AC-GALLERY-TYPE-01 — Tạo Gallery item loại Media

```text
Given Staff Leader mở modal upload Gallery
When chọn Loại nội dung = Media
And nhập đủ title, description, location, files hợp lệ
Then backend tạo gallery_items.item_type = MEDIA.
```

---

## 13.8. AC-GALLERY-TYPE-02 — Tạo Gallery item loại Đoàn khách

```text
Given Staff Leader mở modal upload Gallery
When chọn Loại nội dung = Đoàn khách
And nhập đủ title, description, location, files hợp lệ
Then backend tạo gallery_items.item_type = VISIT_DELEGATION.
```

---

## 13.9. AC-GALLERY-TYPE-03 — Filter theo Đoàn khách

```text
Given hệ thống có gallery item Media và gallery item Đoàn khách
When Staff Leader filter Loại nội dung = Đoàn khách
Then bảng chỉ hiển thị gallery_items có item_type = VISIT_DELEGATION.
```

---

## 13.10. AC-GALLERY-TYPE-04 — Không cần visit instance

```text
Given Staff Leader tạo gallery item loại Đoàn khách
When submit form hợp lệ
Then hệ thống không yêu cầu chọn visit_instance
And gallery_items không cần visit_instance_id.
```

---

## 13.11. AC-GALLERY-TYPE-05 — Detail hiển thị loại nội dung

```text
Given gallery item có item_type = VISIT_DELEGATION
When Staff Leader mở detail
Then modal detail hiển thị Loại nội dung = Đoàn khách.
```

---

## 13.12. AC-PUBLIC-NO-CHANGE-01 — Public Gallery không bị đổi

```text
Given Public Gallery hiện đang hoạt động
When cập nhật item_type vào DB và Quản lý Gallery
Then Public Gallery vẫn hiển thị theo logic cũ
And không bắt buộc có tab/filter theo Media/Đoàn khách ở phase này.
```

---

## 14. Checklist cho AI Agent

## 14.1. Database

```text
[ ] Đọc SQL mới nhất trước khi sửa.
[ ] Thêm gallery_areas.cover_file_id.
[ ] Thêm FK gallery_areas.cover_file_id → files.file_id.
[ ] Thêm gallery_locations.cover_file_id.
[ ] Thêm FK gallery_locations.cover_file_id → files.file_id.
[ ] Thêm gallery_items.item_type ENUM('MEDIA','VISIT_DELEGATION').
[ ] Set DEFAULT 'MEDIA' cho item_type để dữ liệu cũ không lỗi.
[ ] Thêm index idx_gallery_items_item_type.
[ ] Không thêm visit_instance_id.
[ ] Không tạo bảng gallery_area_media/gallery_location_media.
[ ] Cập nhật full SQL nếu project dùng full SQL seed.
[ ] Tạo patch/migration nếu project đang dùng migration.
```

---

## 14.2. Backend Quản lý khu vực

```text
[ ] Cập nhật entity GalleryArea có CoverFileId.
[ ] Cập nhật entity GalleryLocation có CoverFileId.
[ ] Cập nhật EF configuration/FK.
[ ] Cập nhật create location endpoint sang multipart/form-data nếu cần.
[ ] Mode EXISTING_AREA bắt buộc locationCoverImage.
[ ] Mode NEW_AREA bắt buộc areaCoverImage và locationCoverImage.
[ ] Validate mỗi cover image đúng 1 file.
[ ] Validate cover image chỉ là image.
[ ] Upload area cover qua IFileUploadService.
[ ] Upload location cover qua IFileUploadService.
[ ] Không gọi trực tiếp Google Drive service.
[ ] Insert cover_file_id vào gallery_areas/gallery_locations.
[ ] Edit location cho phép thay ảnh vị trí.
[ ] Edit location mode NEW_AREA bắt buộc ảnh khu vực mới.
[ ] List locations trả areaCoverUrl/locationCoverUrl.
[ ] Detail location trả cover image.
[ ] Không phá logic enable/disable location.
[ ] Không làm duplicate row location khi location có nhiều gallery item.
```

---

## 14.3. Backend Quản lý Gallery

```text
[ ] Thêm enum/constant GalleryItemType.
[ ] Cập nhật entity GalleryItem có ItemType.
[ ] Cập nhật create command/request có itemType.
[ ] Cập nhật validator: itemType bắt buộc.
[ ] itemType chỉ nhận MEDIA hoặc VISIT_DELEGATION.
[ ] Create gallery item insert item_type.
[ ] Edit gallery item update item_type.
[ ] List gallery item trả itemType/itemTypeLabel.
[ ] Detail gallery item trả itemType/itemTypeLabel.
[ ] Filter gallery item theo itemType.
[ ] Không link visit_instance.
[ ] Không thêm visitInstanceId vào API.
[ ] Không dùng media_kind để phân biệt Media/Đoàn khách.
```

---

## 14.4. Frontend Quản lý khu vực

```text
[ ] Modal create mode Khu vực có sẵn thêm upload ảnh vị trí.
[ ] Modal create mode Khu vực mới thêm upload ảnh khu vực và ảnh vị trí.
[ ] Validate ảnh khu vực bắt buộc khi mode NEW_AREA.
[ ] Validate ảnh vị trí bắt buộc khi tạo location.
[ ] Chỉ cho chọn 1 file cho mỗi field cover image.
[ ] Chỉ accept image.
[ ] Gửi multipart/form-data.
[ ] Modal edit hiển thị ảnh hiện tại.
[ ] Modal edit cho phép thay ảnh vị trí.
[ ] Modal edit mode NEW_AREA bắt buộc ảnh khu vực mới.
[ ] List/detail hiển thị ảnh nếu UI có chỗ phù hợp.
```

---

## 14.5. Frontend Quản lý Gallery

```text
[ ] Modal create Gallery thêm field Loại nội dung.
[ ] Options: Media, Đoàn khách.
[ ] Gửi itemType = MEDIA hoặc VISIT_DELEGATION.
[ ] Validate itemType bắt buộc.
[ ] Modal edit Gallery thêm field Loại nội dung.
[ ] List Gallery thêm cột Loại nội dung.
[ ] Detail Gallery hiển thị Loại nội dung.
[ ] Filter Gallery thêm Loại nội dung.
[ ] Không thêm field visit instance.
[ ] Không sửa Public Gallery UI.
```

---

## 14.6. Build/Test

```text
[ ] Build backend.
[ ] Build frontend.
[ ] Test DB patch chạy được trên DB hiện tại.
[ ] Test tạo khu vực mới thiếu ảnh khu vực.
[ ] Test tạo vị trí thiếu ảnh vị trí.
[ ] Test tạo khu vực mới đủ 2 ảnh.
[ ] Test thêm vị trí vào khu vực có sẵn đủ ảnh vị trí.
[ ] Test edit location giữ ảnh cũ.
[ ] Test edit location thay ảnh mới.
[ ] Test tạo gallery item loại Media.
[ ] Test tạo gallery item loại Đoàn khách.
[ ] Test edit gallery item đổi loại nội dung.
[ ] Test filter gallery item theo Media.
[ ] Test filter gallery item theo Đoàn khách.
[ ] Test Public Gallery không bị lỗi sau khi thêm item_type.
```

---

## 15. Chốt cuối cùng

Sau khi cập nhật, model Gallery sẽ là:

```text
campuses
  └── gallery_areas
        ├── cover_file_id
        └── gallery_locations
              ├── cover_file_id
              └── gallery_items
                    ├── item_type: MEDIA / VISIT_DELEGATION
                    ├── media_kind: IMAGE / VIDEO / MIXED
                    └── gallery_item_media
                          └── files
```

Ý nghĩa:

```text
gallery_areas.cover_file_id
→ ảnh đại diện khu vực.

gallery_locations.cover_file_id
→ ảnh đại diện vị trí.

gallery_items.item_type
→ phân biệt item là Media hay Đoàn khách.

gallery_items.media_kind
→ phân biệt item chứa ảnh, video hay hỗn hợp.

gallery_item_media
→ danh sách file thật của gallery item.
```

Phase này chỉ sửa quản lý nội bộ:

```text
- Quản lý khu vực.
- Quản lý Gallery.
- Database/backend/frontend liên quan.
```

Không sửa Public Gallery UI trong phase này.
