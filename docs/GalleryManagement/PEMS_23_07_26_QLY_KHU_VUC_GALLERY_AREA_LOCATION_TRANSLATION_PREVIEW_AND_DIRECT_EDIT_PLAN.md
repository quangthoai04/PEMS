# PEMS – Kế hoạch chỉnh sửa Gallery Area/Location: refresh dropdown, preview dịch và cập nhật trực tiếp

## 1. Mục tiêu

Hoàn thiện ba nhóm thay đổi cho trang **Quản lý khu vực** của Staff Leader:

1. Sau khi tạo area mới cùng location đầu tiên, area mới phải xuất hiện ngay trong dropdown “Khu vực có sẵn”, không cần F5 hoặc reload.
2. Modal tạo mới có nút Translation để dịch trước, xem trước, sửa bản tiếng Anh rồi mới lưu. Một source snapshot chỉ được gọi Google Translation một lần.
3. Modal chỉnh sửa bỏ lựa chọn “Khu vực có sẵn/Khu vực mới”; Staff Leader sửa trực tiếp area và location hiện tại. Backend phải UPDATE đúng bản ghi hiện có, không INSERT area mới.

Phạm vi không thay đổi:

- Campus scope của Staff Leader.
- Upload video cover area và ảnh cover location.
- Gallery Item, media, YouTube, audio, publish/hide.
- Public Gallery đã đọc VI/EN từ database.
- Các bảng và cột song ngữ hiện có.

---

# 2. Baseline database

## `gallery_areas`

Đã có:

```text
area_id
campus_id
area_name
area_name_en
area_key
cover_file_id
status
display_order
translation_source
translation_status
translation_source_hash
translated_at
created_at
created_by
updated_at
updated_by
```

## `gallery_locations`

Đã có:

```text
location_id
area_id
location_name
location_name_en
location_key
cover_file_id
status
display_order
translation_source
translation_status
translation_source_hash
translated_at
created_at
created_by
updated_at
updated_by
```

Không tạo bảng translation mới trong phạm vi này.

---

# 3. Kết quả cuối cùng

## 3.1. Refresh dropdown

```text
Tạo:
Tòa Alpha + Trước tòa

Lưu thành công
→ mở lại modal
→ chọn “Khu vực có sẵn”
→ thấy ngay “Tòa Alpha”
```

Không cần F5, reload route hoặc đăng nhập lại.

## 3.2. Preview và lưu bản dịch

```text
Nhập VI
→ bấm Dịch
→ backend gọi Google một lần
→ modal hiện EN
→ Staff Leader có thể sửa EN
→ bấm Tạo mới/Cập nhật
→ backend lưu VI + EN
→ không gọi Google lại nếu VI không đổi
```

## 3.3. Edit đúng bản ghi

```text
area_id = 5, area_name = "Tòa Alpha"

Sửa thành "Tòa nhà Alpha"

Kết quả:
area_id vẫn = 5
area_name = "Tòa nhà Alpha"
area count không tăng
location.area_id không đổi
```

---

# 4. Sửa lỗi area mới không xuất hiện trong dropdown

## 4.1. Nguyên nhân

Hook `useGalleryFilterOptions` hiện chỉ fetch một lần và không trả `refetch`. Sau create, page chỉ refresh location list, còn options dùng cho dropdown vẫn là cache cũ.

## 4.2. Sửa hook

Đổi contract thành:

```ts
interface UseGalleryFilterOptionsResult {
  options: GalleryFilterOptions | null;
  areas: GalleryFilterArea[];
  loading: boolean;
  error: string | null;
  refetch: () => Promise<void>;
  upsertArea: (area: GalleryFilterArea) => void;
}
```

Yêu cầu:

- `refetch` dùng `useCallback`.
- Có request ID hoặc stale-response guard.
- Không áp dụng response cũ.
- Không xóa options cũ trong lúc refresh.
- Nếu refresh lỗi, giữ dữ liệu hiện có.
- `upsertArea` thêm/cập nhật theo `areaId`, không tạo duplicate.

## 4.3. Sau create/update thành công

Thực hiện:

```ts
upsertArea(response.area);

await Promise.all([
  refetchLocationList(),
  refetchFilterOptions(),
]);
```

Cả hai nơi phải cập nhật:

- Dropdown trong modal create.
- Dropdown filter của trang danh sách.

## 4.4. Response create/update

Backend nên trả:

```json
{
  "message": "Đã lưu khu vực và vị trí.",
  "area": {
    "areaId": 5,
    "areaName": "Tòa Alpha",
    "areaNameEn": "Alpha Building",
    "status": "ACTIVE"
  },
  "location": {
    "locationId": 12,
    "locationName": "Trước tòa",
    "locationNameEn": "In Front of the Building",
    "status": "ACTIVE"
  },
  "warnings": []
}
```

Giữ backward compatibility với frontend cũ nếu response trước đây chỉ có message.

---

# 5. Modal tạo mới – giữ hai chế độ

Chỉ modal create giữ:

```text
Khu vực có sẵn
Khu vực mới
```

Modal edit không dùng hai lựa chọn này.

---

# 6. Create mode: Khu vực có sẵn

## 6.1. UI

```text
TÊN KHU VỰC / TÒA
[Dropdown area hiện có]

VỊ TRÍ CỤ THỂ (VI)
[Input]

[Dịch sang EN]

VỊ TRÍ CỤ THỂ (EN)
[Input editable]

ẢNH ĐẠI DIỆN VỊ TRÍ
[Upload]
```

## 6.2. Dịch

Chỉ gửi:

```text
locationNameVi
```

Không dịch lại area đã chọn.

## 6.3. Nút Translation

- Icon `Languages` hoặc icon tương đương.
- Tooltip/label: `Dịch sang tiếng Anh`.
- Disable khi location VI rỗng.
- Disable trong lúc request.
- Hiện spinner.
- Chặn double-click.

---

# 7. Create mode: Khu vực mới

## 7.1. UI

```text
TÊN KHU VỰC / TÒA (VI)
[Input]

TÊN KHU VỰC / TÒA (EN)
[Input editable]

VIDEO ĐẠI DIỆN KHU VỰC
[Upload MP4]

VỊ TRÍ CỤ THỂ (VI)
[Input]

VỊ TRÍ CỤ THỂ (EN)
[Input editable]

ẢNH ĐẠI DIỆN VỊ TRÍ
[Upload]

[Dịch sang EN]
```

## 7.2. Batch translation

Một lần bấm dịch gửi:

```json
{
  "contents": [
    "Tòa Alpha",
    "Trước tòa"
  ]
}
```

Một request Google, map:

```text
result[0] → areaNameEn
result[1] → locationNameEn
```

Không gọi hai request riêng.

---

# 8. Làm rõ số lượng request

Sẽ có hai request tới backend PEMS nhưng chỉ một request tới Google:

| Hành động | PEMS request | Google request |
|---|---:|---:|
| Bấm Dịch | 1 | 1 |
| Bấm Tạo/Cập nhật | 1 | 0 |
| Tổng | 2 | 1 |

Không thể chỉ có một request backend vì Staff Leader cần xem và sửa EN trước khi lưu.

Quy tắc bắt buộc:

```text
Preview đã thành công
+ VI không thay đổi
→ save không được gọi Google lại
```

---

# 9. Endpoint preview translation

## 9.1. Endpoint

Ví dụ:

```http
POST /api/galleries/preview-location-translation
```

## 9.2. Quyền

Chỉ:

```text
role = STAFF
sub_role = LEADER
status = ACTIVE
đúng campus scope
```

Không public.

## 9.3. Request

Existing area:

```json
{
  "mode": "EXISTING_AREA",
  "areaNameVi": null,
  "locationNameVi": "Trước tòa"
}
```

New area:

```json
{
  "mode": "NEW_AREA",
  "areaNameVi": "Tòa Alpha",
  "locationNameVi": "Trước tòa"
}
```

Edit có thể dùng:

```json
{
  "mode": "EDIT",
  "areaNameVi": "Tòa nhà Alpha",
  "locationNameVi": "Sảnh phía trước",
  "includeArea": true,
  "includeLocation": true
}
```

## 9.4. Response

```json
{
  "area": {
    "sourceText": "Tòa Alpha",
    "sourceHash": "<sha256>",
    "translatedText": "Alpha Building"
  },
  "location": {
    "sourceText": "Trước tòa",
    "sourceHash": "<sha256>",
    "translatedText": "In Front of the Building"
  }
}
```

Với existing area, `area = null`.

## 9.5. Endpoint không ghi dữ liệu

Không:

- Insert/update DB.
- Upload file.
- Tạo area/location.
- Ghi translation metadata vào entity.
- Cho anonymous gọi.

---

# 10. Validation preview

Trước khi gọi provider:

- Mode hợp lệ.
- Location VI bắt buộc.
- Area VI bắt buộc khi `NEW_AREA`.
- Normalize khoảng trắng.
- Validate max length.
- Không gửi chuỗi rỗng.
- Kiểm tra số kết quả bằng số input.
- Không chấp nhận translated text rỗng.

---

# 11. Chống stale response

Tình huống:

```text
Nhập "Trước tòa"
→ bấm Dịch
→ sửa thành "Sảnh trước tòa"
→ response cũ trả về
```

Frontend không được ghi bản dịch cũ vào input EN.

Dùng:

```text
requestIdRef
hoặc AbortController
```

Chỉ apply khi source hiện tại vẫn khớp `sourceText/sourceHash` của response.

---

# 12. State translation trong modal

Area:

```text
areaNameVi
areaNameEn
areaSourceHash
areaTranslationOrigin
areaEnglishManuallyEdited
areaTranslationState
```

Location:

```text
locationNameVi
locationNameEn
locationSourceHash
locationTranslationOrigin
locationEnglishManuallyEdited
locationTranslationState
```

Origin:

```text
NONE
AUTO_PREVIEW
MANUAL
AUTO_ON_SAVE
```

State:

```text
IDLE
TRANSLATING
READY
STALE
FAILED
```

---

# 13. Khi preview thành công

Ví dụ location:

```text
locationNameEn = translatedText
locationSourceHash = sourceHash
locationTranslationOrigin = AUTO_PREVIEW
locationEnglishManuallyEdited = false
locationTranslationState = READY
```

Helper text:

```text
Bản dịch tự động — bạn có thể chỉnh sửa trước khi lưu.
```

---

# 14. Khi Staff Leader sửa EN

```text
translationOrigin = MANUAL
englishManuallyEdited = true
translationState = READY
```

Khi save:

```text
translation_source = MANUAL
translation_status = READY
translation_source_hash = hash của VI hiện tại
translated_at = now
```

Không gọi Google.

---

# 15. Khi VI thay đổi sau preview

## 15.1. EN chưa sửa tay

```text
translationState = STALE
```

Hiển thị:

```text
Nội dung tiếng Việt đã thay đổi. Vui lòng dịch lại.
```

Không cho lưu bản EN cũ như `READY`.

## 15.2. EN đã sửa tay

Có thể giữ EN manual nhưng hiển thị cảnh báo:

```text
Tên tiếng Việt đã thay đổi. Hãy kiểm tra lại bản tiếng Anh trước khi lưu.
```

Nếu user vẫn lưu:

```text
translation_source = MANUAL
translation_status = READY
```

Backend tự tính hash theo VI mới.

Không auto-translate theo từng ký tự.

---

# 16. Payload create mới

Multipart request cần có:

```text
mode
areaId

newAreaNameVi
newAreaNameEn
areaTranslationOrigin
areaTranslationSourceHash

locationNameVi
locationNameEn
locationTranslationOrigin
locationTranslationSourceHash

areaCoverVideo
locationCoverImage
```

Existing area:

```text
mode = EXISTING_AREA
areaId = selected area
area VI/EN = null
location VI/EN = values
```

New area:

```text
mode = NEW_AREA
newAreaNameVi/newAreaNameEn = values
locationNameVi/locationNameEn = values
```

---

# 17. Quyết định save ở backend

## AUTO_PREVIEW

Điều kiện:

```text
EN có dữ liệu
source hash khớp VI hiện tại
```

Backend:

- Không gọi Google.
- `translation_source = AUTO`.
- `translation_status = READY`.

## MANUAL

Backend:

- Không gọi Google.
- Tự tính hash.
- Lưu EN.
- `translation_source = MANUAL`.
- `translation_status = READY`.

## AUTO_ON_SAVE

Nếu user không preview và EN trống:

- Có thể dùng logic auto-translate hiện tại.
- Google được gọi một lần trong save.
- Provider lỗi vẫn lưu VI và ghi `FAILED`.

## Preview stale

Nếu origin là `AUTO_PREVIEW` nhưng hash không khớp:

```text
GALLERY_TRANSLATION_PREVIEW_STALE
```

Không âm thầm dịch lại.

---

# 18. Thiết kế lại modal chỉnh sửa

## 18.1. Bỏ radio

Xóa hoàn toàn:

```text
Khu vực có sẵn
Khu vực mới
```

## 18.2. Tiêu đề

```text
Chỉnh sửa khu vực và vị trí
```

## 18.3. UI

```text
TÊN KHU VỰC / TÒA (VI)
[Input]

TÊN KHU VỰC / TÒA (EN)
[Input]

VIDEO ĐẠI DIỆN KHU VỰC
[Preview hiện tại] [Chọn video mới]

VỊ TRÍ CỤ THỂ (VI)
[Input]

VỊ TRÍ CỤ THỂ (EN)
[Input]

ẢNH ĐẠI DIỆN VỊ TRÍ
[Preview hiện tại] [Chọn ảnh mới]

[Dịch sang EN]

[Hủy] [Cập nhật]
```

Nếu không chọn file mới:

- Giữ cover area.
- Giữ cover location.

Hiển thị chú thích:

```text
Thay đổi tên hoặc video đại diện khu vực sẽ áp dụng cho tất cả vị trí thuộc khu vực này.
```

---

# 19. Load detail authoritative khi edit

Không chỉ dùng row list nếu thiếu EN hoặc cover metadata.

Tạo/dùng endpoint:

```http
GET /api/galleries/location-details?locationId={id}
```

Response tối thiểu:

```json
{
  "locationId": 12,
  "areaId": 5,
  "areaName": "Tòa Alpha",
  "areaNameEn": "Alpha Building",
  "areaTranslationStatus": "READY",
  "areaCoverFileId": 100,
  "areaCoverUrl": "...",
  "areaCoverMediaType": "VIDEO",
  "locationName": "Trước tòa",
  "locationNameEn": "In Front of the Building",
  "locationTranslationStatus": "READY",
  "locationCoverFileId": 101,
  "locationCoverUrl": "...",
  "updatedAt": "..."
}
```

Luồng:

```text
Bấm Edit
→ modal loading
→ fetch detail
→ prefill
→ render form
```

---

# 20. Preview translation trong edit

Chỉ gửi field cần dịch:

- Area đổi hoặc EN thiếu → include area.
- Location đổi hoặc EN thiếu → include location.

Chỉ đổi location:

```json
{
  "contents": ["Sảnh trước tòa"]
}
```

Đổi cả hai:

```json
{
  "contents": [
    "Tòa nhà Alpha",
    "Sảnh phía trước"
  ]
}
```

Một Google request.

---

# 21. Update contract mới

Multipart update:

```text
locationId

areaNameVi
areaNameEn
areaTranslationOrigin
areaTranslationSourceHash

locationNameVi
locationNameEn
locationTranslationOrigin
locationTranslationSourceHash

areaCoverVideo optional
locationCoverImage optional
```

Loại bỏ khỏi edit:

```text
mode
targetAreaId
newAreaName theo nghĩa tạo area mới
```

Agent phải tìm toàn bộ consumer trước khi đổi contract.

---

# 22. Backend update đúng entity

Handler phải:

1. Load location theo `locationId`.
2. Include area hiện tại.
3. Validate Staff Leader và campus scope.
4. Giữ nguyên:
   - `location.LocationId`
   - `location.AreaId`
   - `area.AreaId`
5. Update `location.Area`.
6. Update `location`.
7. Save trong transaction.
8. Audit.
9. Cleanup file mới nếu DB fail.

Tuyệt đối không có trong edit:

```csharp
new GalleryArea()
_db.GalleryAreas.Add(...)
location.AreaId = newArea.AreaId
```

---

# 23. Field area cần update

Khi area name đổi:

```text
AreaName
AreaNameEn
AreaKey
TranslationSource
TranslationStatus
TranslationSourceHash
TranslatedAt
UpdatedAt
UpdatedBy
```

Chỉ đổi cover:

- Không sửa translation metadata.
- Không gọi Translation API.

Area name không đổi:

- Giữ EN và metadata.

---

# 24. Field location cần update

Khi location name đổi:

```text
LocationName
LocationNameEn
LocationKey
TranslationSource
TranslationStatus
TranslationSourceHash
TranslatedAt
UpdatedAt
UpdatedBy
```

Chỉ đổi cover:

- Không sửa translation metadata.
- Không gọi Translation API.

---

# 25. Kiểm tra trùng tên

Area:

```text
same campus
same normalized area_key
area_id != currentAreaId
```

Lỗi:

```text
GALLERY_AREA_DUPLICATE
Khu vực/tòa này đã tồn tại trong cơ sở.
```

Location:

```text
same area
same normalized location_key
location_id != currentLocationId
```

Lỗi:

```text
GALLERY_LOCATION_DUPLICATE
Vị trí này đã tồn tại trong khu vực.
```

---

# 26. Area có nhiều location

Ví dụ:

```text
Tòa Alpha
├── Trước tòa
├── Sảnh chính
└── Phòng hội thảo
```

Đổi area name từ một row phải đổi cho tất cả location dùng cùng `areaId`.

Sau update:

- Refetch location list.
- Refetch filter options.
- Update tất cả row cùng areaId.
- Không đổi quan hệ location-area.
- Không tăng area count.

---

# 27. Xử lý cover

Không chọn file mới:

- Giữ file ID cũ.
- Không upload.
- Không delete.

Chọn file mới:

```text
upload file mới
→ save DB
→ commit
```

Nếu DB fail:

- Cleanup file mới.
- DB giữ cover cũ.

Không xóa file cũ trước commit.

---

# 28. Audit

Area thay đổi:

```text
Action = UPDATE_GALLERY_AREA
EntityType = GalleryArea
EntityId = areaId
```

Location thay đổi:

```text
Action = UPDATE_GALLERY_LOCATION
EntityType = GalleryLocation
EntityId = locationId
```

Không tạo audit area nếu area không đổi.

Không log credential hoặc access token.

---

# 29. Response update

```json
{
  "message": "Đã cập nhật khu vực và vị trí.",
  "area": {
    "areaId": 5,
    "areaName": "Tòa nhà Alpha",
    "areaNameEn": "Alpha Building",
    "status": "ACTIVE"
  },
  "location": {
    "locationId": 12,
    "locationName": "Sảnh phía trước",
    "locationNameEn": "Front Lobby",
    "status": "ACTIVE"
  },
  "warnings": []
}
```

Frontend optimistic update rồi background refetch.

---

# 30. Frontend types

```ts
export interface GalleryFilterArea {
  areaId: number;
  areaName: string;
  areaNameEn?: string | null;
  status: GalleryAreaStatus;
}
```

```ts
export interface GalleryLocationDetail {
  locationId: number;
  areaId: number;

  areaName: string;
  areaNameEn?: string | null;
  areaTranslationStatus: TranslationStatus;
  areaCoverFileId?: number | null;
  areaCoverUrl?: string | null;
  areaCoverMediaType?: 'IMAGE' | 'VIDEO' | null;

  locationName: string;
  locationNameEn?: string | null;
  locationTranslationStatus: TranslationStatus;
  locationCoverFileId?: number | null;
  locationCoverUrl?: string | null;

  updatedAt?: string | null;
}
```

```ts
export type TranslationOrigin =
  | 'NONE'
  | 'AUTO_PREVIEW'
  | 'MANUAL'
  | 'AUTO_ON_SAVE';
```

```ts
export interface TranslationPreviewField {
  sourceText: string;
  sourceHash: string;
  translatedText: string;
}
```

---

# 31. API client

Bổ sung/rà soát:

```text
getFilterOptions
previewLocationTranslation
createLocation
getLocationDetail
updateLocation
```

- Preview dùng JSON.
- Create/update dùng multipart.
- Không gọi Google trực tiếp từ frontend.

---

# 32. Error handling

Preview lỗi:

- Không đóng modal.
- Giữ VI.
- Giữ EN manual.
- Cho retry.

Stale preview:

```text
GALLERY_TRANSLATION_PREVIEW_STALE
```

Frontend giữ modal mở và yêu cầu dịch lại.

Config Google thiếu:

- Có thể nhập EN thủ công.
- Save với `MANUAL`.

Provider lỗi:

- Không log secret.
- Log entity/field/sourceHash/errorCode.

---

# 33. Không được làm

Không:

- Giữ existing/new radio trong edit.
- Tạo area mới khi edit.
- Move location sang area khác trong edit.
- Gọi Google lại lúc save nếu preview hợp lệ.
- Auto-translate từng ký tự.
- Gọi Google từ React.
- Lưu EN auto cũ khi VI đã đổi.
- Chỉ refresh list mà không refresh options.
- Yêu cầu F5.
- Bắt upload lại cover.
- Phá campus scope.
- Phá Public Gallery translation.
- Đưa tên AI/Claude/Agent vào commit message.

---

# 34. Test bắt buộc

## Dropdown refresh

1. Tạo area/location.
2. Mở lại modal ngay.
3. Area mới phải có trong dropdown.
4. Không F5.

## Create existing area

1. Chọn area.
2. Nhập location VI.
3. Bấm Dịch.
4. Một Google call.
5. Sửa EN.
6. Save.
7. Không có Google call thứ hai.
8. DB lưu `MANUAL`.

## Create new area

1. Nhập area/location VI.
2. Bấm Dịch.
3. Một batch Google chứa hai source.
4. Save.
5. Không gọi lại Google.

## Preview stale

1. Preview.
2. Sửa VI.
3. Save.
4. Không lưu EN cũ như READY.
5. Trả lỗi stale hoặc yêu cầu manual.

## Edit trực tiếp

Trước:

```text
area_count = N
area_id = 5
```

Sau:

```text
area_count = N
area_id = 5
name đã đổi
```

## Shared area

- Đổi area từ một location.
- Tất cả row cùng area ID hiện tên mới.
- Location không bị move.

## Cover only

- Không gọi Translation API.
- Metadata dịch giữ nguyên.

---

# 35. File dự kiến cần thay đổi

Frontend:

```text
frontend/pems-react/src/pages/dashboard/gallery/LocationManagementStaffLeader.tsx
frontend/pems-react/src/features/gallery-management/hooks/useGalleryManagement.ts
frontend/pems-react/src/features/gallery-management/api/galleryManagementApi.ts
frontend/pems-react/src/features/gallery-management/types/galleryManagement.types.ts
```

Có thể tách thêm:

```text
LocationCreateModal.tsx
LocationEditModal.tsx
TranslationPreviewButton.tsx
BilingualNameFields.tsx
```

Backend:

```text
Galleries/Commands/CreateGalleryLocation/*
Galleries/Commands/UpdateGalleryLocation/*
Galleries/Commands/PreviewGalleryLocationTranslation/*
Galleries/Queries/GetGalleryLocationDetail/*
Galleries/Common/GalleryTranslationCoordinator.cs
Galleries/Common/TranslationSourceHasher.cs
Controllers/GalleriesController.cs
```

Tests:

```text
tests/PEMS.UnitTests/Galleries/*
tests/PEMS.IntegrationTests/Galleries/*
frontend tests tương ứng
```

---

# 36. Thứ tự triển khai

## Phase 1

- Sửa filter-options hook.
- Thêm refetch/upsert.
- Refresh dropdown/list.

## Phase 2

- Preview translation endpoint.
- Scope, batch, validation, test.

## Phase 3

- EN fields và nút dịch trong create modal.
- Stale/manual state.
- Save reuse preview.

## Phase 4

- Đổi backend edit sang direct update.
- Bỏ create-area branch.
- Duplicate checks, audit, cleanup.

## Phase 5

- Thiết kế lại edit modal.
- Load detail.
- Preview translation.
- Direct update payload.

## Phase 6

- Unit, integration, frontend, real-stack tests.

---

# 37. Acceptance criteria

Chỉ coi là hoàn thành khi:

- Area mới xuất hiện ngay trong dropdown.
- Existing-area mode chỉ dịch location.
- New-area mode batch dịch area + location.
- EN preview chỉnh sửa được.
- Save không gọi Google lần hai.
- Edit không còn radio.
- Edit update đúng area/location hiện tại.
- Area ID và location ID giữ nguyên.
- Area count không tăng.
- Shared area cập nhật đúng mọi location.
- Cover-only không gọi dịch.
- Không regress Public Gallery và Gallery Item.
- Không cần F5.

---

# 38. Hướng dẫn AI Agent

1. Đọc codebase và xác nhận branch.
2. Không sửa `Dev` nếu không được yêu cầu.
3. Tìm toàn bộ consumer trước khi đổi update contract.
4. Không chạy destructive SQL.
5. Không sửa ngoài scope.
6. Chạy test sau từng phase.
7. Báo cáo file đã sửa, API contract đã đổi và test đã chạy.
8. Gom commit theo logic, tránh mỗi file một commit.
9. Commit message không chứa tên AI hoặc công cụ.

Commit gợi ý:

```text
fix(gallery): refresh area options after location creation
feat(gallery): preview and reuse area location translations
fix(gallery): update existing area records from location editor
test(gallery): cover area location translation preview flows
```
