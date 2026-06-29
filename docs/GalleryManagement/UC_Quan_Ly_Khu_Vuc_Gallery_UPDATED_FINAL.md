# ĐẶC TẢ USE CASE — QUẢN LÝ KHU VỰC GALLERY

## 1. Phạm vi đặc tả

Tài liệu này mô tả chức năng **Quản lý khu vực** nằm trong module **Quản lý VisitFPTU Gallery**.

Chức năng bao gồm:

- View list khu vực/vị trí.
- Search khu vực/vị trí.
- Filter theo khu vực.
- Filter theo trạng thái.
- Thêm vị trí vào khu vực có sẵn.
- Thêm khu vực mới kèm vị trí đầu tiên.
- Chỉnh sửa vị trí, có thể đổi sang khu vực có sẵn.
- Chỉnh sửa vị trí, có thể tạo khu vực mới rồi chuyển vị trí sang khu vực đó.
- Enable location.
- Disable location.
- Đồng bộ nghiệp vụ giữa `gallery_locations.status` và `gallery_items.status`.
- Cập nhật ảnh hưởng của location status lên trang Quản lý Gallery và Public Gallery.

Chức năng chưa nằm trong scope này:

- Upload media Gallery.
- Edit nội dung bài đăng Gallery.
- View detail gallery item.
- Enable/disable gallery item trực tiếp.
- Xóa cứng khu vực/vị trí.
- Inactive toàn bộ area cấp cha bằng UI riêng.
- Quản lý public Gallery page.
- Photo face tag.

---

## 2. Mục tiêu nghiệp vụ

Trang **Quản lý khu vực** dùng để quản trị danh mục khu vực/tòa và vị trí cụ thể phục vụ cho Gallery.

Gallery có 2 cấp địa điểm:

```text
Khu vực / Tòa / Khu lớn
  └── Vị trí cụ thể
```

Ví dụ:

```text
TÒA ALPHA
  └── Hồ sen
  └── Trước tòa nhà Alpha

TÒA DELTA
  └── Trước tòa nhà Delta
  └── Thư viện
  └── Sảnh chính
```

Nghiệp vụ đã chốt:

```text
1 location = tối đa 1 gallery item.
1 gallery item = 1 bài đăng Gallery đầy đủ gồm:
- title
- description
- ảnh/video
- media_kind
- status
```

Ví dụ:

```text
Location: TÒA ALPHA / Hồ sen

Gallery item duy nhất của location này:
- Title: Khung cảnh hồ sen mùa hè
- Description: Không gian xanh trong khuôn viên campus.
- Media: ảnh/video
- Status: PUBLISHED hoặc HIDDEN
```

Trang **Quản lý khu vực** không upload file và không edit nội dung bài đăng Gallery. Tuy nhiên, trạng thái location có ảnh hưởng trực tiếp đến bài đăng Gallery tương ứng vì mỗi location chỉ có tối đa một bài đăng.

---

## 3. Actor và phân quyền

### 3.1 Primary Actor

**Staff Leader**

Điều kiện runtime:

```text
role_code = STAFF
sub_role = LEADER
users.status = ACTIVE
users.primary_campus_id IS NOT NULL
```

Staff Leader chỉ quản lý khu vực/vị trí trong campus của mình.

Ví dụ:

```text
Staff Leader Hà Nội chỉ thấy và thao tác gallery_areas/gallery_locations thuộc campus Hà Nội.
Staff Leader HCM không được xem/sửa location của Hà Nội.
```

### 3.2 Forbidden Actors

Các actor sau không được thao tác màn này trong scope hiện tại:

```text
ADMIN
HO
STAFF + STAFF
DEPARTMENT + LEADER
DEPARTMENT + STAFF
STUDENT
VISITOR
```

Backend phải trả `403 Forbidden` nếu các actor này gọi API trực tiếp.

---

## 4. Bảng database liên quan

UC này dùng trực tiếp:

```text
campuses
gallery_areas
gallery_locations
gallery_items
users
audit_logs / audit_log_changes nếu hệ thống đang ghi audit
```

Trong đó:

- `gallery_areas`: khu vực/tòa/khu lớn.
- `gallery_locations`: vị trí cụ thể thuộc khu vực.
- `gallery_items`: bài đăng Gallery thuộc location, được update một chiều khi disable location.
- `users`: kiểm tra Staff Leader và campus scope.
- `audit_logs`, `audit_log_changes`: ghi lịch sử nếu hệ thống đang bật audit.

Không dùng trực tiếp trong UC này:

```text
gallery_item_media
files
photo_face_tags
```

Ghi chú:

- Trang Quản lý khu vực không upload file.
- Trang Quản lý khu vực không edit title/description/media của gallery item.
- Disable location không xóa gallery item và không xóa file Google Drive.
- Disable location có thể update `gallery_items.status` từ `PUBLISHED` sang `HIDDEN`.

---

## 5. Mô hình dữ liệu

### 5.1 `gallery_areas`

Đại diện cho khu vực/tòa/khu lớn.

Các field quan trọng:

```text
area_id
campus_id
area_name
area_key
status
display_order
created_at
created_by
updated_at
updated_by
```

Ý nghĩa:

| Field | Ý nghĩa |
|---|---|
| `area_id` | Khóa chính khu vực |
| `campus_id` | Campus sở hữu khu vực |
| `area_name` | Tên hiển thị, ví dụ `TÒA DELTA` |
| `area_key` | Tên đã normalize để chống trùng |
| `status` | Trạng thái area, hiện UI chưa có thao tác trực tiếp |
| `display_order` | Thứ tự hiển thị |
| `created_at/created_by` | Audit tạo |
| `updated_at/updated_by` | Audit cập nhật |

Constraint quan trọng:

```sql
UNIQUE KEY uq_gallery_areas_campus_key (campus_id, area_key)
```

Trong cùng một campus không được tạo trùng khu vực sau khi normalize.

---

### 5.2 `gallery_locations`

Đại diện cho vị trí cụ thể thuộc một khu vực.

Các field quan trọng:

```text
location_id
area_id
location_name
location_key
status
display_order
created_at
created_by
updated_at
updated_by
```

Ý nghĩa:

| Field | Ý nghĩa |
|---|---|
| `location_id` | Khóa chính vị trí |
| `area_id` | Khu vực cha |
| `location_name` | Tên vị trí, ví dụ `Thư viện` |
| `location_key` | Tên vị trí đã normalize |
| `status` | Trạng thái vị trí, chính là toggle trên UI Quản lý khu vực |
| `display_order` | Thứ tự hiển thị |
| `created_at/created_by` | Audit tạo |
| `updated_at/updated_by` | Audit cập nhật |

Constraint quan trọng:

```sql
UNIQUE KEY uq_gallery_locations_area_key (area_id, location_key)
```

Trong cùng một khu vực không được tạo trùng vị trí sau khi normalize.

---

### 5.3 `gallery_items`

Đại diện cho bài đăng Gallery thuộc một location.

Các field quan trọng trong scope liên quan:

```text
gallery_item_id
location_id
title
description
media_kind
status
created_at
created_by
updated_at
updated_by
deleted_at
deleted_by
```

Nghiệp vụ đã chốt:

```text
Mỗi location tối đa chỉ có 1 gallery item.
```

Vì vậy DB nên có constraint:

```sql
UNIQUE KEY uq_gallery_items_location (location_id)
```

Nếu location đã có gallery item, người dùng không được tạo thêm bài đăng mới cho location đó. Muốn thay đổi nội dung thì phải edit gallery item hiện có ở trang Quản lý Gallery.

---

## 6. Quan hệ bảng

```text
campuses
  └── gallery_areas
        └── gallery_locations
              └── gallery_items
                    └── gallery_item_media
                          └── files
```

Quan hệ nghiệp vụ:

```text
Một campus có nhiều gallery_areas.
Một gallery_area có nhiều gallery_locations.
Một gallery_location có tối đa một gallery_item.
Một gallery_item có một hoặc nhiều media.
```

Trong UC Quản lý khu vực, thao tác trực tiếp đến:

```text
campuses
  └── gallery_areas
        └── gallery_locations
```

Tuy nhiên, khi disable location, backend có xử lý bổ sung tới `gallery_items.status`.

---

## 7. Quy ước tên và normalize key

Backend phải normalize tên để sinh key chống trùng.

Ví dụ:

```text
"TÒA DELTA"       → "toa-delta"
"Toà Delta"       → "toa-delta"
"  TÒA   DELTA "  → "toa-delta"
"Thư viện"        → "thu-vien"
"Thư  Viện"       → "thu-vien"
```

Rule normalize đề xuất:

```text
1. Trim đầu/cuối.
2. Gộp nhiều khoảng trắng thành một khoảng trắng.
3. Chuyển lowercase.
4. Bỏ dấu tiếng Việt.
5. Thay khoảng trắng/ký tự đặc biệt bằng dấu `-`.
6. Gộp nhiều dấu `-` liên tiếp.
7. Trim dấu `-` ở đầu/cuối.
```

Không được chỉ check trùng bằng text gốc vì sẽ lọt các bản ghi gần giống nhau.

---

## 8. State machine

### 8.1 `gallery_locations.status`

```text
ACTIVE → INACTIVE
INACTIVE → ACTIVE
```

Ý nghĩa UI:

| DB status | UI label | Ý nghĩa |
|---|---|---|
| `ACTIVE` | Hoạt động | Có thể chọn khi upload Gallery, có thể hiện public |
| `INACTIVE` | Ngừng hoạt động | Không cho upload mới, không hiện public |

Toggle trên table Quản lý khu vực update:

```text
gallery_locations.status
```

Khi chuyển `ACTIVE → INACTIVE`, backend có thể update thêm `gallery_items.status` theo rule ở phần nghiệp vụ.

---

### 8.2 `gallery_areas.status`

`gallery_areas.status` vẫn tồn tại trong DB nhưng hiện UI chưa có thao tác riêng để inactive toàn bộ area.

Rule hiện tại:

```text
Khi tạo area mới từ modal, gallery_areas.status mặc định ACTIVE.
Khi toggle một row trên màn Quản lý khu vực, chỉ update gallery_locations.status.
Không tự động update gallery_areas.status.
```

Sau này nếu có chức năng “Ngừng hoạt động toàn bộ TÒA DELTA”, đó sẽ là UC khác hoặc action riêng cho area cấp cha.

---

### 8.3 `gallery_items.status`

```text
PUBLISHED → HIDDEN
HIDDEN → PUBLISHED
```

Ý nghĩa:

| DB status | UI Gallery item | Public Gallery |
|---|---|---|
| `PUBLISHED` | Hiển thị | Có thể hiển thị nếu area/location cũng ACTIVE |
| `HIDDEN` | Đã ẩn | Không hiển thị public |

Toggle này nằm ở trang **Quản lý Gallery**, không phải trang **Quản lý khu vực**.

Tuy nhiên, khi disable location, backend sẽ tự động chuyển gallery item từ `PUBLISHED` sang `HIDDEN`.

---

## 9. Nghiệp vụ location status và gallery item status

### 9.1 Nguyên tắc tổng quát

```text
gallery_locations.status = trạng thái khả dụng của vị trí.
gallery_items.status = trạng thái hiển thị của bài đăng Gallery.
```

Public Gallery chỉ hiển thị bài đăng khi đủ:

```text
gallery_areas.status = ACTIVE
AND gallery_locations.status = ACTIVE
AND gallery_items.status = PUBLISHED
AND gallery_items.deleted_at IS NULL
```

---

### 9.2 Logic khi disable location

Khi Staff Leader disable một location ở trang **Quản lý khu vực**, backend phải xử lý trong cùng transaction:

```text
1. Set gallery_locations.status = INACTIVE.
2. Tìm gallery item tương ứng của location đó.
3. Nếu gallery item đang PUBLISHED:
   - Set gallery_items.status = HIDDEN.
   - Set gallery_items.updated_by = currentUserId.
   - Set gallery_items.updated_at = NOW().
4. Nếu gallery item đã HIDDEN:
   - Giữ nguyên HIDDEN.
5. Không update gallery_item_media.
6. Không xóa file Google Drive.
7. Không update gallery_areas.status.
```

SQL logic minh họa:

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

Đây là cascade một chiều từ location sang gallery item khi disable location.

---

### 9.3 Logic khi enable location

Khi Staff Leader enable lại location ở trang **Quản lý khu vực**, backend chỉ xử lý:

```text
1. Set gallery_locations.status = ACTIVE.
2. Không tự động set gallery_items.status = PUBLISHED.
3. Gallery item tương ứng vẫn giữ nguyên status hiện tại.
```

Nếu gallery item đã bị auto set từ `PUBLISHED` sang `HIDDEN` khi disable location, thì sau khi enable location lại, gallery item vẫn là `HIDDEN`.

Người dùng muốn bài đăng public trở lại thì phải sang trang **Quản lý Gallery** và bật toggle của gallery item.

SQL logic minh họa:

```sql
UPDATE gallery_locations
SET status = 'ACTIVE',
    updated_by = @currentUserId,
    updated_at = NOW()
WHERE location_id = @locationId;
```

Tuyệt đối không chạy:

```sql
UPDATE gallery_items
SET status = 'PUBLISHED'
WHERE location_id = @locationId;
```

---

## 10. Hai toggle khác nhau như thế nào?

| Toggle | Trang | Update DB | Ghi chú |
|---|---|---|---|
| Toggle location | Quản lý khu vực | `gallery_locations.status` | Khi tắt location thì tự ẩn gallery item nếu item đang PUBLISHED |
| Toggle gallery item | Quản lý Gallery | `gallery_items.status` | Chỉ cho bật/tắt item khi location đang ACTIVE |

### 10.1 Toggle location

Nằm ở trang:

```text
Quản lý khu vực
```

Update:

```text
gallery_locations.status
```

Tác động:

- Khi location `ACTIVE`: có thể chọn để upload Gallery.
- Khi location `INACTIVE`: không cho upload mới vào vị trí đó.
- Khi location `INACTIVE`: bài đăng Gallery thuộc location đó không hiển thị public.
- Khi disable location: nếu gallery item đang `PUBLISHED`, tự set về `HIDDEN`.
- Khi enable location: không tự set gallery item về `PUBLISHED`.

### 10.2 Toggle gallery item

Nằm ở trang:

```text
Quản lý Gallery
```

Update:

```text
gallery_items.status
```

Tác động:

- Khi gallery item `PUBLISHED`: bài đăng có thể hiển thị public nếu area/location cũng ACTIVE.
- Khi gallery item `HIDDEN`: bài đăng không hiển thị public.
- Không làm thay đổi trạng thái location `gallery_locations.status`.
- Nếu location đang `INACTIVE`, toggle gallery item bị disable và không được bật lên `PUBLISHED`.

---

## 11. Trạng thái hiển thị trong trang Quản lý Gallery

Trang **Quản lý Gallery** vẫn hiển thị gallery item thuộc location inactive, nhưng phải hiển thị cảnh báo rõ.

Nếu location inactive:

```text
- Dưới tên vị trí hiển thị badge: "Vị trí ngừng hoạt động".
- Toggle gallery item bị disabled.
- Không cho bật item sang PUBLISHED.
```

Ví dụ row trong bảng Quản lý Gallery:

```text
Khu vực: TÒA ALPHA

Vị trí cụ thể:
  Hồ sen
  [Vị trí ngừng hoạt động]

Tiêu đề: Khung cảnh hồ sen mùa hè
Trạng thái bài đăng: Đã ẩn
Toggle: disabled
```

Badge đề xuất:

```text
Vị trí ngừng hoạt động
```

Màu badge đề xuất:

```text
bg-orange-50
text-orange-700
border-orange-200
```

---

## 12. Rule cho toggle gallery item khi location inactive

Khi `gallery_locations.status = INACTIVE`, toggle của gallery item trên trang **Quản lý Gallery** phải bị disable.

Frontend:

```text
Nếu locationStatus = INACTIVE:
- Disable toggle item.
- Không gọi API change gallery item status.
- Hiển thị badge "Vị trí ngừng hoạt động".
- Có thể hiển thị tooltip:
  "Vị trí đang ngừng hoạt động. Hãy bật lại vị trí trước khi hiển thị bài đăng."
```

Backend vẫn phải chặn nếu user gọi API trực tiếp.

Rule backend:

```text
Nếu request muốn set gallery_items.status = PUBLISHED
AND location hoặc area không ACTIVE
→ reject HTTP 409.
```

Message đề xuất:

```text
Không thể hiển thị bài đăng vì vị trí đang ngừng hoạt động.
```

Nếu request set `gallery_items.status = HIDDEN` khi location inactive, backend có thể cho phép hoặc coi là no-op. Nhưng UI hiện tại nên disable toàn bộ toggle để tránh hiểu nhầm.

---

## 13. Bảng trạng thái sau cập nhật

| Area status | Location status | Gallery item status | Toggle item ở Quản lý Gallery | Public hiển thị? |
|---|---|---|---|---|
| ACTIVE | ACTIVE | PUBLISHED | Enabled / ON | Có |
| ACTIVE | ACTIVE | HIDDEN | Enabled / OFF | Không |
| ACTIVE | INACTIVE | HIDDEN | Disabled / OFF | Không |
| ACTIVE | INACTIVE | PUBLISHED | Không nên tồn tại sau disable location | Không |
| INACTIVE | ACTIVE | HIDDEN | Disabled hoặc blocked theo effective status | Không |
| INACTIVE | ACTIVE | PUBLISHED | Không nên cho bật public | Không |

Ghi chú:

Trường hợp `ACTIVE / INACTIVE / PUBLISHED` có thể chỉ xuất hiện nếu dữ liệu cũ hoặc update thủ công sai DB. Backend list vẫn có thể trả về, nhưng frontend phải disable toggle và public query vẫn không hiển thị.

---

## 14. Công thức public visibility

Một bài đăng Gallery chỉ hiển thị trên public Gallery khi đủ tất cả điều kiện:

```text
gallery_areas.status = ACTIVE
AND gallery_locations.status = ACTIVE
AND gallery_items.status = PUBLISHED
AND gallery_items.deleted_at IS NULL
```

Nếu thiếu một trong các điều kiện trên, bài đăng không hiển thị public.

SQL minh họa:

```sql
SELECT
    gi.gallery_item_id,
    ga.area_name,
    gl.location_name,
    gi.title,
    gi.description,
    gi.media_kind
FROM gallery_items gi
JOIN gallery_locations gl
    ON gl.location_id = gi.location_id
JOIN gallery_areas ga
    ON ga.area_id = gl.area_id
WHERE ga.status = 'ACTIVE'
  AND gl.status = 'ACTIVE'
  AND gi.status = 'PUBLISHED'
  AND gi.deleted_at IS NULL;
```

---

## 15. UC-LOC-01 — View List Khu vực/Vị trí

### 15.1 Mục tiêu

Staff Leader xem danh sách các vị trí Gallery thuộc campus của mình.

### 15.2 Endpoint đề xuất

```http
GET /api/gallery-management/locations
```

Query params:

```text
keyword
areaId
status
page
pageSize
sortBy
sortDirection
```

Không nhận `campusId` từ frontend. Backend lấy campus từ current user.

### 15.3 Main Flow

1. Staff Leader mở trang `/dashboard/gallery/locations`.
2. Frontend gọi API list locations.
3. Backend xác thực user.
4. Backend kiểm tra user là Staff Leader active.
5. Backend lấy `currentUser.primary_campus_id`.
6. Backend query `gallery_locations` join `gallery_areas`.
7. Backend chỉ trả locations thuộc campus của Staff Leader.
8. Frontend hiển thị table:

```text
STT
Khu vực (Tòa/Khu)
Vị trí cụ thể
Trạng thái
Ngày tạo
Hành động
```

### 15.4 Response đề xuất

```json
{
  "items": [
    {
      "locationId": 1,
      "areaId": 1,
      "areaName": "TÒA ALPHA",
      "locationName": "Hồ sen",
      "status": "INACTIVE",
      "createdAt": "2026-05-05T00:00:00",
      "updatedAt": null,
      "hasGalleryItem": true,
      "galleryItemId": 15,
      "galleryItemStatus": "HIDDEN",
      "canEdit": true,
      "canToggle": true
    }
  ],
  "page": 1,
  "pageSize": 5,
  "totalItems": 25,
  "totalPages": 5
}
```

### 15.5 Query logic đề xuất

```sql
SELECT
    gl.location_id,
    ga.area_id,
    ga.area_name,
    gl.location_name,
    gl.status,
    gl.created_at,
    gl.updated_at,
    gi.gallery_item_id,
    gi.status AS gallery_item_status
FROM gallery_locations gl
JOIN gallery_areas ga
    ON ga.area_id = gl.area_id
LEFT JOIN gallery_items gi
    ON gi.location_id = gl.location_id
   AND gi.deleted_at IS NULL
WHERE ga.campus_id = @currentUserCampusId
ORDER BY gl.created_at DESC, gl.location_id DESC;
```

`LEFT JOIN gallery_items` dùng để biết location đã có bài đăng hay chưa, nhưng UC này không edit nội dung gallery item.

---

## 16. UC-LOC-02 — Search Khu vực/Vị trí

### 16.1 Mục tiêu

Staff Leader tìm kiếm theo tên khu vực hoặc tên vị trí cụ thể.

### 16.2 Search scope

Keyword tìm theo:

```text
gallery_areas.area_name
gallery_locations.location_name
```

Có thể tìm theo key để hỗ trợ không dấu:

```text
gallery_areas.area_key
gallery_locations.location_key
```

### 16.3 Main Flow

1. Staff Leader nhập keyword vào ô `Tìm kiếm khu vực, vị trí...`.
2. Frontend debounce 300–500 ms.
3. Frontend gọi lại API list.
4. Backend trim keyword.
5. Backend search trong phạm vi campus của Staff Leader.
6. UI hiển thị danh sách khớp.

### 16.4 Business Rules

```text
BR-LOC-SEARCH-01: Search không được bỏ qua campus scope.
BR-LOC-SEARCH-02: Keyword rỗng sau trim thì không áp dụng search.
BR-LOC-SEARCH-03: Search không phân biệt hoa/thường.
BR-LOC-SEARCH-04: Nên search theo area_name/location_name và area_key/location_key để hỗ trợ tiếng Việt không dấu.
```

---

## 17. UC-LOC-03 — Filter Khu vực/Vị trí

### 17.1 Mục tiêu

Staff Leader lọc danh sách location theo khu vực và trạng thái.

### 17.2 Filter fields

| UI filter | DB |
|---|---|
| Tất cả khu vực | `gallery_areas.area_id` |
| Trạng thái | `gallery_locations.status` |

Giá trị status:

```text
ACTIVE
INACTIVE
```

UI label:

| DB value | UI |
|---|---|
| `ACTIVE` | Hoạt động |
| `INACTIVE` | Ngừng hoạt động |

### 17.3 Main Flow

1. Staff Leader chọn filter `Tất cả khu vực` hoặc một khu vực cụ thể.
2. Staff Leader chọn filter `Trạng thái`.
3. Frontend gọi API list với query params.
4. Backend áp dụng filter cùng campus scope.
5. UI cập nhật table.

### 17.4 Business Rules

```text
BR-LOC-FILTER-01: areaId filter chỉ hợp lệ nếu area thuộc campus của current user.
BR-LOC-FILTER-02: status chỉ nhận ACTIVE hoặc INACTIVE.
BR-LOC-FILTER-03: Nếu areaId không thuộc campus current user, backend trả 403 hoặc 404.
BR-LOC-FILTER-04: Search và filter kết hợp theo AND logic.
```

---

## 18. UC-LOC-04 — Thêm vị trí vào khu vực có sẵn

### 18.1 Mục tiêu

Staff Leader thêm một vị trí cụ thể mới vào một khu vực/tòa đã tồn tại.

UI hiện tại:

```text
Modal title: Thêm khu vực mới
Radio: Khu vực có sẵn
Dropdown: TÒA DELTA
Input: Vị trí cụ thể
Button: Tạo mới
```

Về nghiệp vụ, đây là thao tác:

```text
Create gallery_locations under existing gallery_areas
```

Không phải tạo area mới.

### 18.2 Endpoint đề xuất

```http
POST /api/gallery-management/locations
```

Body:

```json
{
  "mode": "EXISTING_AREA",
  "areaId": 3,
  "newAreaName": null,
  "locationName": "Sảnh chính"
}
```

### 18.3 Main Flow

1. Staff Leader bấm `Thêm khu vực mới`.
2. Modal mở ở mode `Khu vực có sẵn`.
3. Staff Leader chọn area từ dropdown.
4. Staff Leader nhập `Vị trí cụ thể`.
5. Staff Leader bấm `Tạo mới`.
6. Frontend validate field bắt buộc.
7. Backend kiểm tra Staff Leader và campus scope.
8. Backend kiểm tra area tồn tại, thuộc campus current user, status ACTIVE.
9. Backend normalize `locationName` thành `location_key`.
10. Backend kiểm tra trùng `(area_id, location_key)`.
11. Backend insert `gallery_locations`.
12. UI đóng modal, reload list, hiển thị thông báo thành công.

### 18.4 Business Rules

```text
BR-LOC-CREATE-EXISTING-01: areaId bắt buộc khi mode = EXISTING_AREA.
BR-LOC-CREATE-EXISTING-02: area phải thuộc campus của Staff Leader.
BR-LOC-CREATE-EXISTING-03: Không cho thêm location vào area INACTIVE.
BR-LOC-CREATE-EXISTING-04: locationName bắt buộc, không rỗng sau trim.
BR-LOC-CREATE-EXISTING-05: locationName tối đa 150 ký tự.
BR-LOC-CREATE-EXISTING-06: Không cho trùng location_key trong cùng area.
BR-LOC-CREATE-EXISTING-07: Location mới mặc định status = ACTIVE.
BR-LOC-CREATE-EXISTING-08: created_by = current user.
BR-LOC-CREATE-EXISTING-09: Location mới ban đầu có thể chưa có gallery item.
```

---

## 19. UC-LOC-05 — Thêm khu vực mới kèm vị trí đầu tiên

### 19.1 Mục tiêu

Staff Leader tạo một khu vực/tòa mới và đồng thời tạo vị trí cụ thể đầu tiên thuộc khu vực đó.

UI hiện tại:

```text
Modal title: Thêm khu vực mới
Radio: Khu vực mới
Input: Nhập tên khu vực mới
Input: Vị trí cụ thể
Button: Tạo mới
```

### 19.2 Endpoint đề xuất

Dùng chung endpoint:

```http
POST /api/gallery-management/locations
```

Body:

```json
{
  "mode": "NEW_AREA",
  "areaId": null,
  "newAreaName": "TÒA GAMMA",
  "locationName": "Sảnh chính"
}
```

### 19.3 Main Flow

1. Staff Leader bấm `Thêm khu vực mới`.
2. Staff Leader chọn radio `Khu vực mới`.
3. Frontend hiển thị input nhập tên khu vực mới.
4. Staff Leader nhập `Tên khu vực / Tòa`.
5. Staff Leader nhập `Vị trí cụ thể`.
6. Staff Leader bấm `Tạo mới`.
7. Backend validate role/scope.
8. Backend normalize `newAreaName` thành `area_key`.
9. Backend kiểm tra trùng `(campus_id, area_key)`.
10. Backend normalize `locationName` thành `location_key`.
11. Backend tạo `gallery_areas` với `status = ACTIVE`.
12. Backend tạo `gallery_locations` với `status = ACTIVE`.
13. UI reload list và hiển thị row mới.

### 19.4 Transaction rule

Tạo area mới và location mới phải nằm trong cùng transaction.

```text
Nếu insert area thành công nhưng insert location lỗi → rollback cả hai.
Không để area rỗng không có location do lỗi giữa chừng.
```

### 19.5 Business Rules

```text
BR-LOC-CREATE-NEW-01: newAreaName bắt buộc khi mode = NEW_AREA.
BR-LOC-CREATE-NEW-02: locationName bắt buộc.
BR-LOC-CREATE-NEW-03: area_name tối đa 150 ký tự.
BR-LOC-CREATE-NEW-04: location_name tối đa 150 ký tự.
BR-LOC-CREATE-NEW-05: Không cho trùng area_key trong cùng campus.
BR-LOC-CREATE-NEW-06: Area mới mặc định ACTIVE.
BR-LOC-CREATE-NEW-07: Location mới mặc định ACTIVE.
BR-LOC-CREATE-NEW-08: created_by của area và location đều là current user.
BR-LOC-CREATE-NEW-09: Location mới chưa tự động tạo gallery item.
```

---

## 20. UC-LOC-06 — Edit location với khu vực có sẵn

### 20.1 Mục tiêu

Staff Leader chỉnh sửa một vị trí hiện có, bao gồm:

- Đổi tên vị trí cụ thể.
- Chuyển vị trí sang khu vực có sẵn khác trong cùng campus.

UI hiện tại:

```text
Modal title: Chỉnh sửa khu vực
Radio: Khu vực có sẵn
Dropdown: TÒA ALPHA
Input: Hồ sen
Button: Cập nhật
```

Về nghiệp vụ, đây là edit `gallery_locations`.

### 20.2 Endpoint đề xuất

```http
PUT /api/gallery-management/locations/{locationId}
```

Body:

```json
{
  "mode": "EXISTING_AREA",
  "areaId": 1,
  "newAreaName": null,
  "locationName": "Hồ sen"
}
```

### 20.3 Main Flow

1. Staff Leader bấm edit icon ở một row.
2. Frontend mở modal edit.
3. Frontend load dữ liệu location hiện tại.
4. Staff Leader chọn `Khu vực có sẵn`.
5. Staff Leader chọn area từ dropdown.
6. Staff Leader sửa `Vị trí cụ thể`.
7. Staff Leader bấm `Cập nhật`.
8. Backend kiểm tra location tồn tại và thuộc campus của Staff Leader.
9. Backend kiểm tra area mới tồn tại và thuộc campus Staff Leader.
10. Backend normalize `locationName`.
11. Backend kiểm tra trùng `(area_id, location_key)` với location khác.
12. Backend update `gallery_locations.area_id`, `location_name`, `location_key`, `updated_by`, `updated_at`.
13. UI reload list.

### 20.4 Business Rules

```text
BR-LOC-EDIT-EXISTING-01: Chỉ Staff Leader cùng campus được edit.
BR-LOC-EDIT-EXISTING-02: areaId mới phải thuộc cùng campus.
BR-LOC-EDIT-EXISTING-03: Cho phép chuyển location sang area khác trong cùng campus.
BR-LOC-EDIT-EXISTING-04: Không cho chuyển sang area INACTIVE nếu rule upload/public chỉ dùng active area.
BR-LOC-EDIT-EXISTING-05: locationName không được rỗng.
BR-LOC-EDIT-EXISTING-06: Không cho trùng location_key trong area đích.
BR-LOC-EDIT-EXISTING-07: Edit location không làm thay đổi gallery_items.status.
BR-LOC-EDIT-EXISTING-08: Gallery item thuộc location này sẽ tự hiển thị khu vực mới qua join.
BR-LOC-EDIT-EXISTING-09: Vì mỗi location tối đa 1 gallery item, chuyển location sang area khác không tạo thêm gallery item.
```

---

## 21. UC-LOC-07 — Edit location với khu vực mới

### 21.1 Mục tiêu

Staff Leader tạo một khu vực mới trong lúc chỉnh sửa location, sau đó chuyển location hiện tại sang khu vực mới đó.

UI hiện tại:

```text
Modal title: Chỉnh sửa khu vực
Radio: Khu vực mới
Input: Nhập tên khu vực mới
Input: Hồ sen
Button: Cập nhật
```

### 21.2 Endpoint đề xuất

Dùng chung endpoint edit:

```http
PUT /api/gallery-management/locations/{locationId}
```

Body:

```json
{
  "mode": "NEW_AREA",
  "areaId": null,
  "newAreaName": "TÒA GAMMA",
  "locationName": "Hồ sen"
}
```

### 21.3 Main Flow

1. Staff Leader bấm edit location.
2. Staff Leader chọn `Khu vực mới`.
3. Staff Leader nhập tên khu vực mới.
4. Staff Leader giữ hoặc sửa `Vị trí cụ thể`.
5. Staff Leader bấm `Cập nhật`.
6. Backend kiểm tra location hiện tại thuộc campus của Staff Leader.
7. Backend normalize `newAreaName`.
8. Backend kiểm tra trùng area trong campus.
9. Backend tạo `gallery_areas` mới với status ACTIVE.
10. Backend update location hiện tại sang area mới.
11. Backend update `location_name`, `location_key`, `updated_by`, `updated_at`.
12. UI reload list.

### 21.4 Transaction rule

Tạo area mới và chuyển location phải cùng transaction.

```text
Nếu tạo area mới xong nhưng update location lỗi → rollback area mới.
```

### 21.5 Business Rules

```text
BR-LOC-EDIT-NEW-01: newAreaName bắt buộc.
BR-LOC-EDIT-NEW-02: Không cho tạo area trùng sau normalize.
BR-LOC-EDIT-NEW-03: Area mới mặc định ACTIVE.
BR-LOC-EDIT-NEW-04: Location sau edit giữ nguyên status hiện tại.
BR-LOC-EDIT-NEW-05: Nếu location đang INACTIVE, sau khi chuyển sang area mới vẫn INACTIVE.
BR-LOC-EDIT-NEW-06: Không tự đổi location status khi edit tên/khu vực.
BR-LOC-EDIT-NEW-07: Gallery item thuộc location vẫn giữ nguyên, chỉ đổi area hiển thị qua location mới.
```

---

## 22. UC-LOC-08 — Enable location

### 22.1 Mục tiêu

Staff Leader bật lại một location đang `INACTIVE`.

### 22.2 Endpoint đề xuất

```http
PATCH /api/gallery-management/locations/{locationId}/status
```

Body:

```json
{
  "status": "ACTIVE"
}
```

### 22.3 Main Flow

1. Staff Leader bật toggle ở row location.
2. Frontend gọi API update status.
3. Backend kiểm tra Staff Leader và campus scope.
4. Backend kiểm tra location tồn tại.
5. Backend set `gallery_locations.status = ACTIVE`.
6. Backend set `updated_by`, `updated_at`.
7. Backend không update `gallery_items.status`.
8. Backend ghi audit log nếu có.
9. UI cập nhật badge `Hoạt động`.
10. Trang Quản lý Gallery bỏ badge `Vị trí ngừng hoạt động`.
11. Toggle gallery item active trở lại.
12. Gallery item vẫn giữ trạng thái hiện tại, thường là `HIDDEN`.

### 22.4 Business Rules

```text
BR-LOC-ENABLE-01: Enable location chỉ update gallery_locations.status = ACTIVE.
BR-LOC-ENABLE-02: Enable location không tự set gallery_items.status = PUBLISHED.
BR-LOC-ENABLE-03: Gallery item đã bị set HIDDEN khi disable location vẫn giữ HIDDEN.
BR-LOC-ENABLE-04: Khi location ACTIVE lại, toggle gallery item ở trang Quản lý Gallery được enable trở lại.
BR-LOC-ENABLE-05: Badge “Vị trí ngừng hoạt động” biến mất khi location ACTIVE lại.
BR-LOC-ENABLE-06: Chỉ khi người dùng bật toggle gallery item thì gallery_items.status mới đổi từ HIDDEN sang PUBLISHED.
BR-LOC-ENABLE-07: Nếu gallery item đang HIDDEN, public Gallery vẫn chưa hiển thị bài đăng sau khi enable location.
```

### 22.5 SQL logic minh họa

```sql
UPDATE gallery_locations
SET status = 'ACTIVE',
    updated_by = @currentUserId,
    updated_at = NOW()
WHERE location_id = @locationId;
```

Không được chạy:

```sql
UPDATE gallery_items
SET status = 'PUBLISHED'
WHERE location_id = @locationId;
```

---

## 23. UC-LOC-09 — Disable location

### 23.1 Mục tiêu

Staff Leader ngừng hoạt động một location để:

- Không cho upload media mới vào location đó.
- Không hiển thị bài đăng Gallery thuộc location đó ở public Gallery.
- Vẫn giữ bài đăng Gallery và media cũ trong hệ thống quản lý.
- Tự động ẩn bài đăng Gallery nếu bài đăng đang `PUBLISHED`.
- Không tự động publish lại bài đăng khi location được enable trở lại.

### 23.2 Endpoint đề xuất

```http
PATCH /api/gallery-management/locations/{locationId}/status
```

Body:

```json
{
  "status": "INACTIVE"
}
```

### 23.3 Main Flow

1. Staff Leader tắt toggle ở row location.
2. Frontend gọi API update status.
3. Backend kiểm tra Staff Leader và campus scope.
4. Backend kiểm tra location tồn tại.
5. Backend set `gallery_locations.status = INACTIVE`.
6. Backend tìm gallery item thuộc location đó.
7. Nếu gallery item đang `PUBLISHED`, backend set `gallery_items.status = HIDDEN`.
8. Nếu gallery item đã `HIDDEN`, backend giữ nguyên.
9. Backend cập nhật `updated_by`, `updated_at` cho location và gallery item nếu có thay đổi.
10. Backend ghi audit log.
11. Frontend reload trang Quản lý khu vực.
12. Trang Quản lý Gallery vẫn hiển thị item đó nhưng:
    - Có badge `Vị trí ngừng hoạt động`.
    - Toggle gallery item bị disabled.
    - Trạng thái gallery item là `HIDDEN`.

### 23.4 Business Rules

```text
BR-LOC-DISABLE-01: Disable location update gallery_locations.status = INACTIVE.
BR-LOC-DISABLE-02: Nếu location có gallery item đang PUBLISHED, backend phải set gallery_items.status = HIDDEN.
BR-LOC-DISABLE-03: Nếu gallery item đã HIDDEN, giữ nguyên HIDDEN.
BR-LOC-DISABLE-04: Disable location không update gallery_areas.status.
BR-LOC-DISABLE-05: Disable location không update gallery_item_media.status.
BR-LOC-DISABLE-06: Disable location không xóa gallery item.
BR-LOC-DISABLE-07: Disable location không xóa file Google Drive.
BR-LOC-DISABLE-08: Trang Quản lý Gallery vẫn hiển thị gallery item thuộc location inactive.
BR-LOC-DISABLE-09: Trang Quản lý Gallery phải hiển thị badge “Vị trí ngừng hoạt động” dưới tên vị trí.
BR-LOC-DISABLE-10: Toggle gallery item bị disabled khi location inactive.
BR-LOC-DISABLE-11: Public Gallery không hiển thị bài đăng thuộc location inactive.
BR-LOC-DISABLE-12: Mọi cập nhật location và gallery item khi disable location phải nằm trong cùng transaction.
BR-LOC-DISABLE-13: Enable location sau đó không tự set gallery item về PUBLISHED.
```

### 23.5 SQL logic minh họa

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

### 23.6 Ví dụ nghiệp vụ

Trước khi disable:

```text
Area: TÒA ALPHA = ACTIVE
Location: Hồ sen = ACTIVE
Gallery item: Khung cảnh hồ sen mùa hè = PUBLISHED
Public Gallery: Có hiển thị
```

Sau khi disable location:

```text
Area: TÒA ALPHA = ACTIVE
Location: Hồ sen = INACTIVE
Gallery item: Khung cảnh hồ sen mùa hè = HIDDEN
Public Gallery: Không hiển thị
Quản lý Gallery: Vẫn hiển thị bài đăng, toggle item disabled, có badge vị trí inactive
```

Sau khi enable location lại:

```text
Area: TÒA ALPHA = ACTIVE
Location: Hồ sen = ACTIVE
Gallery item: Khung cảnh hồ sen mùa hè = HIDDEN
Public Gallery: Vẫn không hiển thị
Quản lý Gallery: Toggle item active trở lại, badge vị trí inactive biến mất
```

Sau khi user bật toggle gallery item:

```text
Area: TÒA ALPHA = ACTIVE
Location: Hồ sen = ACTIVE
Gallery item: Khung cảnh hồ sen mùa hè = PUBLISHED
Public Gallery: Có hiển thị lại
```

---

## 24. Cập nhật rule cho trang Quản lý Gallery

### 24.1 Toggle gallery item

Khi Staff Leader bật/tắt toggle gallery item:

```text
Nếu location ACTIVE và area ACTIVE:
- Cho phép đổi gallery_items.status.

Nếu location INACTIVE hoặc area INACTIVE:
- Không cho đổi gallery_items.status sang PUBLISHED.
- Toggle bị disabled trên frontend.
- Backend reject nếu gọi API trực tiếp.
```

### 24.2 Business Rules bổ sung

```text
BR-GAL-STATUS-01: Toggle gallery item chỉ active khi area ACTIVE và location ACTIVE.
BR-GAL-STATUS-02: Nếu location INACTIVE, frontend disable toggle gallery item.
BR-GAL-STATUS-03: Nếu location INACTIVE, frontend hiển thị badge “Vị trí ngừng hoạt động” dưới tên vị trí.
BR-GAL-STATUS-04: Backend không cho set gallery_items.status = PUBLISHED nếu location hoặc area không ACTIVE.
BR-GAL-STATUS-05: Khi location được enable lại, toggle gallery item active trở lại nhưng status item vẫn giữ nguyên HIDDEN.
BR-GAL-STATUS-06: Chỉ khi người dùng click toggle item thì gallery_items.status mới đổi HIDDEN → PUBLISHED.
```

---

## 25. API DTO đề xuất

### 25.1 `GalleryLocationListQuery`

```csharp
public sealed record GalleryLocationListQuery(
    string? Keyword,
    long? AreaId,
    string? Status,
    int Page = 1,
    int PageSize = 10,
    string? SortBy = "createdAt",
    string? SortDirection = "desc"
) : IRequest<PagedResult<GalleryLocationListItemDto>>;
```

### 25.2 `GalleryLocationListItemDto`

```csharp
public sealed class GalleryLocationListItemDto
{
    public long LocationId { get; init; }
    public long AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    public bool HasGalleryItem { get; init; }
    public long? GalleryItemId { get; init; }
    public string? GalleryItemStatus { get; init; }
}
```

### 25.3 `CreateGalleryLocationCommand`

```csharp
public sealed record CreateGalleryLocationCommand(
    string Mode,
    long? AreaId,
    string? NewAreaName,
    string LocationName
) : IRequest<GalleryLocationDetailDto>;
```

### 25.4 `UpdateGalleryLocationCommand`

```csharp
public sealed record UpdateGalleryLocationCommand(
    long LocationId,
    string Mode,
    long? AreaId,
    string? NewAreaName,
    string LocationName
) : IRequest<GalleryLocationDetailDto>;
```

### 25.5 `ChangeGalleryLocationStatusCommand`

```csharp
public sealed record ChangeGalleryLocationStatusCommand(
    long LocationId,
    string Status
) : IRequest<GalleryLocationDetailDto>;
```

### 25.6 `GalleryAreaOptionDto`

```csharp
public sealed class GalleryAreaOptionDto
{
    public long AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
```

### 25.7 `GalleryLocationOptionDto`

```csharp
public sealed class GalleryLocationOptionDto
{
    public long LocationId { get; init; }
    public long AreaId { get; init; }
    public string AreaName { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public bool HasGalleryItem { get; init; }
    public long? GalleryItemId { get; init; }
}
```

---

## 26. Endpoint tổng hợp đề xuất

```http
GET    /api/gallery-management/locations
GET    /api/gallery-management/areas/options
GET    /api/gallery-management/locations/options
POST   /api/gallery-management/locations
PUT    /api/gallery-management/locations/{locationId}
PATCH  /api/gallery-management/locations/{locationId}/status
```

### 26.1 `GET /areas/options`

Dùng cho:

- Filter `Tất cả khu vực`.
- Dropdown trong modal thêm/sửa khu vực.
- Dropdown trong modal upload/edit Gallery.

Rule:

```text
Chỉ trả area thuộc campus current user.
Mặc định chỉ trả ACTIVE area cho form tạo/upload.
Có thể cho quản lý trả cả ACTIVE và INACTIVE nếu cần filter nội bộ.
```

### 26.2 `GET /locations/options`

Dùng cho:

- Dropdown `Tất cả vị trí cụ thể` trong Gallery.
- Dropdown vị trí khi upload/edit Gallery.

Rule cho upload Gallery:

```text
Chỉ trả location có:
gallery_locations.status = ACTIVE
gallery_areas.status = ACTIVE
gallery_areas.campus_id = currentUser.primary_campus_id
```

Vì mỗi location tối đa 1 gallery item, khi tạo mới gallery item:

```text
Không trả location đã có gallery item active/non-deleted.
Hoặc trả nhưng đánh dấu disabled với message:
"Vị trí này đã có bài đăng Gallery."
```

---

## 27. Backend implementation notes

### 27.1 Controller

Controller chỉ:

```text
- Nhận HTTP request.
- Map request sang Query/Command.
- Gọi IMediator.
- Trả ApiResponse.
```

Không đặt logic business trong controller.

### 27.2 Handler

Handler phải xử lý:

```text
- Check current user authenticated.
- Check Staff Leader role.
- Check primary_campus_id.
- Check area/location thuộc campus.
- Normalize areaName/locationName.
- Check duplicate key.
- Insert/update trong transaction.
- Update audit fields.
- Khi disable location, auto-hide gallery item nếu item đang PUBLISHED.
```

### 27.3 Scope check bắt buộc

Mọi query/update/toggle phải join về `gallery_areas.campus_id`.

Không chỉ check `location_id` đơn lẻ.

Ví dụ check đúng:

```sql
SELECT gl.*
FROM gallery_locations gl
JOIN gallery_areas ga ON ga.area_id = gl.area_id
WHERE gl.location_id = @locationId
  AND ga.campus_id = @currentUserCampusId;
```

### 27.4 Không xóa cứng

Không implement hard delete trong UC này.

Nếu sau này cần xóa, phải kiểm tra:

```text
location đã có gallery_items chưa?
area đã có locations chưa?
có cần soft delete không?
```

Hiện tại toggle `INACTIVE` là đủ.

### 27.5 Transaction bắt buộc khi disable location

Khi disable location, update location và update gallery item phải cùng transaction.

```text
Nếu update location thành công nhưng update item lỗi → rollback cả hai.
Không để trạng thái lệch giữa location và gallery item.
```

---

## 28. Frontend implementation notes

### 28.1 Route

Route đề xuất:

```text
/dashboard/gallery/locations
```

### 28.2 Page title

UI hiện tại:

```text
Quản lý khu vực
Danh sách các tòa và khu vực cụ thể
```

Gợi ý wording nghiệp vụ rõ hơn:

```text
Quản lý khu vực
Danh sách khu vực/tòa và vị trí cụ thể dùng cho Gallery
```

### 28.3 Button

UI hiện tại:

```text
+ Thêm khu vực mới
```

Về nghiệp vụ, button này có thể tạo:

```text
- Vị trí mới trong khu vực có sẵn.
- Khu vực mới kèm vị trí đầu tiên.
```

Có thể giữ wording `Thêm khu vực mới` theo mockup, nhưng trong code nên đặt tên command là `CreateGalleryLocation`, không phải chỉ `CreateArea`.

### 28.4 Modal create

Fields:

```text
Radio:
- Khu vực có sẵn
- Khu vực mới

Nếu chọn Khu vực có sẵn:
- Dropdown Khu vực/Tòa
- Input Vị trí cụ thể

Nếu chọn Khu vực mới:
- Input Tên khu vực mới
- Input Vị trí cụ thể
```

Button:

```text
Hủy
Tạo mới
```

### 28.5 Modal edit

Fields giống modal create, nhưng button submit là:

```text
Cập nhật
```

Khi edit location hiện tại:

```text
- Mặc định chọn radio Khu vực có sẵn.
- Dropdown chọn area hiện tại.
- Input location_name hiện tại.
```

Nếu user chuyển sang `Khu vực mới`:

```text
- Area dropdown ẩn.
- New area name input hiện.
- Location input giữ giá trị hiện tại.
```

### 28.6 Validation frontend

```text
- Nếu mode = EXISTING_AREA: areaId bắt buộc.
- Nếu mode = NEW_AREA: newAreaName bắt buộc.
- locationName luôn bắt buộc.
- Trim trước khi gửi.
- Không cho submit khi input chỉ toàn khoảng trắng.
```

Backend vẫn validate lại.

---

## 29. Mapping UI ↔ Database

### 29.1 List table

| UI | DB |
|---|---|
| STT | row number theo pagination |
| Khu vực (Tòa/Khu) | `gallery_areas.area_name` |
| Vị trí cụ thể | `gallery_locations.location_name` |
| Trạng thái | `gallery_locations.status` |
| Ngày tạo | `gallery_locations.created_at` |
| Edit icon | mở edit modal cho `location_id` |
| Toggle | update `gallery_locations.status` |

### 29.2 Modal create existing area

| UI | DB |
|---|---|
| Khu vực có sẵn | mode = `EXISTING_AREA` |
| Dropdown area | `gallery_areas.area_id` |
| Vị trí cụ thể | `gallery_locations.location_name` |

### 29.3 Modal create new area

| UI | DB |
|---|---|
| Khu vực mới | mode = `NEW_AREA` |
| Tên khu vực mới | `gallery_areas.area_name` |
| Vị trí cụ thể | `gallery_locations.location_name` |

### 29.4 Modal edit

| UI | DB |
|---|---|
| Chọn khu vực có sẵn | update `gallery_locations.area_id` |
| Chọn khu vực mới | insert `gallery_areas`, update `gallery_locations.area_id` |
| Vị trí cụ thể | update `gallery_locations.location_name` |
| Cập nhật | set `updated_by`, `updated_at` |

---

## 30. Public Gallery impact

Public Gallery phải loại inactive location:

```sql
WHERE ga.status = 'ACTIVE'
  AND gl.status = 'ACTIVE'
  AND gi.status = 'PUBLISHED'
  AND gi.deleted_at IS NULL
```

Khi Staff Leader inactive `Hồ sen`:

```text
Bài đăng Gallery thuộc TÒA ALPHA / Hồ sen không hiện public.
Bài đăng bị set về HIDDEN nếu trước đó đang PUBLISHED.
Bài đăng vẫn còn trong trang Quản lý Gallery nội bộ.
Toggle item bị disabled do location inactive.
Nếu bật lại Hồ sen, bài đăng vẫn HIDDEN.
Người dùng phải bật lại toggle item thì bài đăng mới PUBLISHED và hiện public.
```

---

## 31. Acceptance Criteria

### AC-LOC-01 — View list đúng campus

Given Staff Leader Hà Nội đăng nhập  
When mở trang Quản lý khu vực  
Then hệ thống chỉ hiển thị locations thuộc campus Hà Nội  
And không hiển thị locations của campus khác.

### AC-LOC-02 — Search theo khu vực

Given có area `TÒA DELTA`  
When nhập keyword `delta`  
Then các location thuộc `TÒA DELTA` xuất hiện.

### AC-LOC-03 — Search theo vị trí

Given có location `Thư viện`  
When nhập keyword `thư viện`  
Then row chứa `Thư viện` xuất hiện.

### AC-LOC-04 — Filter theo khu vực

Given có location thuộc `TÒA ALPHA` và `TÒA DELTA`  
When chọn filter `TÒA DELTA`  
Then chỉ locations thuộc `TÒA DELTA` được hiển thị.

### AC-LOC-05 — Filter theo trạng thái

Given có location ACTIVE và INACTIVE  
When chọn trạng thái `Ngừng hoạt động`  
Then chỉ location có `status = INACTIVE` hiển thị.

### AC-LOC-06 — Tạo location trong area có sẵn

Given Staff Leader chọn `Khu vực có sẵn = TÒA DELTA`  
And nhập `Vị trí cụ thể = Sảnh chính`  
When bấm `Tạo mới`  
Then DB tạo row `gallery_locations` thuộc area `TÒA DELTA`  
And status mặc định là ACTIVE.

### AC-LOC-07 — Tạo area mới kèm location

Given Staff Leader chọn `Khu vực mới`  
And nhập `Tên khu vực mới = TÒA GAMMA`  
And nhập `Vị trí cụ thể = Sảnh chính`  
When bấm `Tạo mới`  
Then DB tạo row `gallery_areas`  
And DB tạo row `gallery_locations` thuộc area mới  
And cả hai được tạo trong cùng transaction.

### AC-LOC-08 — Không cho trùng area

Given campus đã có `TÒA DELTA`  
When Staff Leader tạo khu vực mới `toa delta`  
Then backend reject với HTTP 409  
And không tạo area mới.

### AC-LOC-09 — Không cho trùng location trong cùng area

Given `TÒA DELTA` đã có `Thư viện`  
When Staff Leader thêm `Thư viện` lần nữa vào `TÒA DELTA`  
Then backend reject với HTTP 409.

### AC-LOC-10 — Cho phép cùng tên location ở area khác

Given `TÒA ALPHA` đã có `Sảnh chính`  
When Staff Leader thêm `Sảnh chính` vào `TÒA DELTA`  
Then backend cho phép nếu trong `TÒA DELTA` chưa có `Sảnh chính`.

### AC-LOC-11 — Edit location

Given location `Hồ sen` thuộc `TÒA ALPHA`  
When Staff Leader đổi tên thành `Hồ sen Alpha`  
Then `gallery_locations.location_name` được cập nhật  
And `updated_by/updated_at` được cập nhật.

### AC-LOC-12 — Chuyển location sang area có sẵn

Given location `Hồ sen` thuộc `TÒA ALPHA`  
When Staff Leader chọn area `TÒA DELTA` và bấm cập nhật  
Then `gallery_locations.area_id` đổi sang area `TÒA DELTA`.

### AC-LOC-13 — Edit tạo area mới

Given location `Hồ sen` đang tồn tại  
When Staff Leader chọn `Khu vực mới = TÒA GAMMA`  
Then backend tạo `gallery_areas = TÒA GAMMA`  
And chuyển location `Hồ sen` sang area mới.

### AC-LOC-14 — Disable location tự ẩn gallery item đang published

Given location `TÒA ALPHA / Hồ sen` đang ACTIVE  
And location này có gallery item đang `PUBLISHED`  
When Staff Leader disable location  
Then `gallery_locations.status = INACTIVE`  
And `gallery_items.status = HIDDEN`  
And public Gallery không hiển thị bài đăng đó.

### AC-LOC-15 — Disable location không đổi item đã hidden

Given location đang ACTIVE  
And location này có gallery item đang `HIDDEN`  
When Staff Leader disable location  
Then `gallery_locations.status = INACTIVE`  
And `gallery_items.status` vẫn là `HIDDEN`.

### AC-LOC-16 — Quản lý Gallery hiển thị badge vị trí inactive

Given location đang INACTIVE  
And location này có gallery item  
When Staff Leader mở trang Quản lý Gallery  
Then row gallery item vẫn hiển thị  
And dưới tên vị trí có badge `Vị trí ngừng hoạt động`  
And toggle gallery item bị disabled.

### AC-LOC-17 — Enable location không tự publish lại gallery item

Given location đang INACTIVE  
And gallery item của location đó đang `HIDDEN`  
When Staff Leader enable location  
Then `gallery_locations.status = ACTIVE`  
And `gallery_items.status` vẫn là `HIDDEN`  
And public Gallery vẫn chưa hiển thị bài đăng.

### AC-LOC-18 — Enable location mở lại toggle gallery item

Given location vừa được enable lại  
When Staff Leader mở trang Quản lý Gallery  
Then badge `Vị trí ngừng hoạt động` không còn hiển thị  
And toggle gallery item active trở lại  
And toggle đang ở trạng thái OFF vì item vẫn HIDDEN.

### AC-LOC-19 — User bật lại gallery item sau khi enable location

Given location đang ACTIVE  
And gallery item đang HIDDEN  
When Staff Leader bật toggle gallery item  
Then `gallery_items.status = PUBLISHED`  
And public Gallery hiển thị bài đăng nếu area cũng ACTIVE.

### AC-LOC-20 — Backend chặn bật item khi location inactive

Given location đang INACTIVE  
And gallery item đang HIDDEN  
When user gọi API trực tiếp để set gallery item PUBLISHED  
Then backend trả HTTP 409  
And `gallery_items.status` vẫn là HIDDEN.

### AC-LOC-21 — Mỗi location tối đa một gallery item

Given location `TÒA DELTA / Thư viện` đã có gallery item  
When Staff Leader tạo thêm gallery item mới vào cùng location  
Then backend reject với HTTP 409  
And thông báo `Vị trí này đã có bài đăng Gallery.`

### AC-LOC-22 — Forbidden actor

Given user không phải Staff Leader  
When gọi API create/edit/toggle location  
Then backend trả HTTP 403  
And không thay đổi DB.

---

## 32. Error messages đề xuất

| Case | Message |
|---|---|
| Thiếu area existing | `Vui lòng chọn khu vực/tòa.` |
| Thiếu tên area mới | `Vui lòng nhập tên khu vực/tòa mới.` |
| Thiếu location | `Vui lòng nhập vị trí cụ thể.` |
| Area trùng | `Khu vực này đã tồn tại. Vui lòng chọn từ danh sách khu vực có sẵn.` |
| Location trùng | `Vị trí này đã tồn tại trong khu vực đã chọn.` |
| Area inactive | `Khu vực này đang ngừng hoạt động.` |
| Location không tồn tại | `Không tìm thấy vị trí Gallery.` |
| Khác campus | `Bạn không có quyền thao tác vị trí này.` |
| Forbidden role | `Bạn không có quyền quản lý khu vực Gallery.` |
| Location đã có gallery item | `Vị trí này đã có bài đăng Gallery.` |
| Bật item khi location inactive | `Không thể hiển thị bài đăng vì vị trí đang ngừng hoạt động.` |
| Bật item khi area inactive | `Không thể hiển thị bài đăng vì khu vực đang ngừng hoạt động.` |
| Tooltip toggle disabled | `Vị trí đang ngừng hoạt động. Hãy bật lại vị trí trước khi hiển thị bài đăng.` |

---

## 33. Checklist cho AI Agent khi code

```text
[ ] Không code upload media trong UC này.
[ ] Không code edit nội dung gallery item trong UC này.
[ ] Dùng bảng gallery_areas và gallery_locations.
[ ] Có xét ảnh hưởng tới gallery_items khi disable location.
[ ] Toggle trên row Quản lý khu vực update gallery_locations.status.
[ ] Khi disable location, nếu gallery item đang PUBLISHED thì set gallery_items.status = HIDDEN.
[ ] Nếu gallery item đang HIDDEN thì giữ nguyên.
[ ] Enable location chỉ update gallery_locations.status = ACTIVE.
[ ] Enable location không tự set gallery_items.status = PUBLISHED.
[ ] Trang Quản lý Gallery hiển thị badge "Vị trí ngừng hoạt động" khi location inactive.
[ ] Trang Quản lý Gallery disable toggle item khi location inactive.
[ ] Backend chặn set gallery_items.status = PUBLISHED nếu location/area inactive.
[ ] Sau khi enable location, toggle item active trở lại nhưng item vẫn HIDDEN.
[ ] Chỉ khi user click toggle item thì gallery_items.status mới đổi HIDDEN → PUBLISHED.
[ ] Không update gallery_areas.status từ toggle location hiện tại.
[ ] Không update gallery_item_media.status khi disable/enable location.
[ ] Không xóa file Google Drive.
[ ] Không xóa cứng area/location.
[ ] Staff Leader = role_code STAFF + sub_role LEADER.
[ ] Không dùng role STAFF_LEADER.
[ ] Không nhận campusId từ frontend.
[ ] Backend lấy campus từ currentUser.primary_campus_id.
[ ] Mọi query location phải join gallery_areas để check campus_id.
[ ] Normalize area_name thành area_key.
[ ] Normalize location_name thành location_key.
[ ] Check unique (campus_id, area_key).
[ ] Check unique (area_id, location_key).
[ ] Đảm bảo mỗi location tối đa một gallery item bằng DB unique hoặc backend validation.
[ ] Tạo area mới + location mới trong cùng transaction.
[ ] Edit tạo area mới + chuyển location trong cùng transaction.
[ ] Disable location + auto-hide item phải nằm trong cùng transaction.
[ ] Ghi audit cho cả location status change và item auto-hide nếu hệ thống đang dùng audit.
[ ] Frontend validate mode EXISTING_AREA / NEW_AREA.
[ ] Frontend reload list sau create/edit/toggle.
[ ] Public Gallery phải lọc area ACTIVE + location ACTIVE + gallery item PUBLISHED.
[ ] Build backend.
[ ] Build frontend.
[ ] Test Postman cho list/search/filter/create/edit/enable/disable.
```
