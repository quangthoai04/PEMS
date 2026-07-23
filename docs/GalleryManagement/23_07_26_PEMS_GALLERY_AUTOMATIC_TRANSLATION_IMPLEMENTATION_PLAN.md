# PEMS – Kế hoạch triển khai hoàn chỉnh chức năng dịch tự động cho Gallery

## 1. Mục tiêu tài liệu

Tài liệu này là yêu cầu triển khai đầy đủ để AI Agent cập nhật chức năng đa ngôn ngữ cho module **Public VisitFPTU Gallery** và các luồng quản lý Gallery của **Staff Leader**.

Mục tiêu cuối cùng:

- Staff Leader chỉ nhập tên khu vực, tên vị trí và tiêu đề Gallery Item bằng tiếng Việt.
- Backend dùng Google Cloud Translation để dịch sang tiếng Anh tại thời điểm tạo hoặc sửa dữ liệu.
- Bản dịch tiếng Anh được lưu bền vững trong database.
- Public API trả đồng thời dữ liệu tiếng Việt và tiếng Anh.
- Khi người dùng bấm biểu tượng VI/EN trên header, frontend chỉ đổi chuỗi đang hiển thị.
- Tuyệt đối không gọi Translation API khi người dùng public chuyển ngôn ngữ.
- Việc đổi ngôn ngữ không reload dữ liệu, không làm mất vị trí carousel, modal hoặc trạng thái đang xem.
- Khi Translation API lỗi, dữ liệu tiếng Việt vẫn được lưu và public fallback về tiếng Việt.

Đây là thay đổi xuyên suốt:

```text
Database
→ Domain Entity
→ Translation Foundation
→ Gallery Write Handlers
→ Public Gallery DTO
→ Public Gallery Query Handlers
→ Frontend Type
→ Frontend Rendering
→ Retry / Backfill
→ Test
```

---

# 2. Baseline hiện tại phải giữ nguyên

## 2.1. Chức năng Gallery đang có

Không được làm hỏng các chức năng hiện tại:

- Staff Leader chỉ quản lý Gallery trong campus của mình.
- Quản lý area và location.
- Area mới dùng video MP4 làm cover.
- Location dùng ảnh cover.
- Một location có thể có nhiều Gallery Item.
- Gallery Item có loại:
  - `MEDIA`
  - `VISIT_DELEGATION`
- Media hỗ trợ:
  - Ảnh upload từ máy.
  - Video YouTube.
- Tối đa 20 media cho mỗi item.
- Có đúng một primary media.
- Có mô tả tiếng Việt và tiếng Anh.
- Có audio tiếng Việt và tiếng Anh do Staff Leader upload.
- Có publish/hide.
- Khi location bị inactive, các item đang public bị ẩn.
- Public Gallery có:
  - Campus navigation.
  - Area Showcase.
  - Location Showcase.
  - Gallery grid.
  - Gallery detail modal.
  - Audio VI/EN.
  - Public media proxy.
  - HTTP Range cho audio/video.

## 2.2. Các trường đã có trong database

Không tạo bảng dịch mới cho Gallery.

### `gallery_areas`

Đã có:

```text
area_name
area_name_en
translation_source
translation_status
translation_source_hash
translated_at
```

### `gallery_locations`

Đã có:

```text
location_name
location_name_en
translation_source
translation_status
translation_source_hash
translated_at
```

### `gallery_items`

Đã có:

```text
title
title_en
translation_source
translation_status
translation_source_hash
translated_at
```

### `gallery_item_contents`

Đã có:

```text
description_vi
audio_vi_file_id
description_en
audio_en_file_id
```

Phạm vi thay đổi lần này chỉ tự động dịch:

```text
area_name
location_name
title
```

Không tự động ghi đè:

```text
description_vi
description_en
audio_vi_file_id
audio_en_file_id
caption
alt_text
caption_en
alt_text_en
```

---

# 3. Quy tắc kiến trúc bắt buộc

## 3.1. Không dịch khi public chuyển ngôn ngữ

Không được gọi Google Translation trong các trường hợp sau:

- Người dùng bấm EN trên header.
- Người dùng bấm lại VI.
- Người dùng mở Public Gallery.
- Người dùng chuyển campus.
- Người dùng chuyển area.
- Người dùng chuyển location.
- Người dùng mở Gallery Item.
- Người dùng đổi ảnh/video.
- Người dùng mở hoặc đóng modal.
- Public API được gọi.
- Trang public reload.
- Frontend render lại.

Luồng đúng:

```text
Staff Leader tạo hoặc sửa dữ liệu tiếng Việt
→ Backend dịch VI sang EN
→ Backend lưu EN vào database
→ Public API trả cả VI và EN
→ Frontend chọn chuỗi theo i18n.resolvedLanguage
```

## 3.2. Không đưa credential xuống frontend

Tuyệt đối không:

- Gọi Google Translation từ React.
- Gửi service account tới frontend.
- Gửi access token tới frontend.
- Tạo public endpoint cho phép dịch văn bản tùy ý.
- Để anonymous user kích hoạt dịch.

Google Translation chỉ được gọi từ backend.

## 3.3. Translation failure không được chặn lưu tiếng Việt

Nếu Google Translation lỗi:

```text
Dữ liệu tiếng Việt vẫn được tạo/sửa thành công.
Trường tiếng Anh không được giữ bản cũ sai ngữ nghĩa.
translation_status = FAILED.
Public fallback về tiếng Việt.
Staff Leader có thể retry sau.
```

Không được rollback toàn bộ nghiệp vụ Gallery chỉ vì provider dịch lỗi.

---

# 4. Database và migration

## 4.1. Kiểm tra độ dài cột

Kiểm tra định nghĩa thực tế trong full SQL baseline mới nhất và EF configuration.

Khuyến nghị độ dài:

```sql
gallery_areas.area_name_en         VARCHAR(255) NULL
gallery_locations.location_name_en VARCHAR(255) NULL
gallery_items.title_en             VARCHAR(500) NULL
```

Lý do:

- Bản dịch tiếng Anh có thể dài hơn tiếng Việt.
- Không được cắt chuỗi dịch bằng `Substring`.
- Không được lưu dữ liệu bị mất nghĩa.

Nếu schema hiện tại ngắn hơn, phải cập nhật:

1. Full SQL baseline mới nhất.
2. Additive migration cho database đang tồn tại.
3. EF Fluent Configuration hoặc Data Annotation nếu đang giới hạn độ dài.

Không thay đổi cột tiếng Việt nếu không cần.

## 4.2. Translation metadata

Giữ thống nhất:

```text
translation_source:
- AUTO
- MANUAL

translation_status:
- PENDING
- READY
- FAILED
- OUTDATED
```

Quy ước trong scope này:

### Dịch tự động thành công

```text
*_en                    = translated text
translation_source      = AUTO
translation_status      = READY
translation_source_hash = SHA-256 của nguồn tiếng Việt đã normalize
translated_at           = VietnamNow
```

### Dịch tự động thất bại

```text
*_en                    = NULL
translation_source      = AUTO
translation_status      = FAILED
translation_source_hash = SHA-256 của nguồn tiếng Việt mới
translated_at           = NULL
```

### Nguồn tiếng Việt thay đổi nhưng chưa dịch

```text
*_en                    = NULL
translation_source      = AUTO hoặc giữ logic phù hợp
translation_status      = OUTDATED
translation_source_hash = SHA-256 của nguồn mới
translated_at           = NULL
```

Không để public dùng bản dịch cũ khi nguồn VI đã thay đổi.

---

# 5. Translation foundation dùng chung

## 5.1. Không để Gallery phụ thuộc trực tiếp vào News

Hiện provider dịch có abstraction theo module News, ví dụ:

```text
INewsTranslationService
GoogleNewsTranslationService
```

Cần tách abstraction dùng chung, ví dụ:

```text
IContentTranslationService
GoogleCloudTranslationService
```

Interface tối thiểu:

```csharp
Task<IReadOnlyList<string>> TranslateTextAsync(
    IReadOnlyList<string> contents,
    string sourceLanguage,
    string targetLanguage,
    CancellationToken cancellationToken);

Task<IReadOnlyList<string>> TranslateHtmlAsync(
    IReadOnlyList<string> contents,
    string sourceLanguage,
    string targetLanguage,
    CancellationToken cancellationToken);

Task<TranslationConnectionTestResult> TestConnectionAsync(...);
```

Có thể triển khai tương thích tạm thời:

```text
GoogleCloudTranslationService
├── IContentTranslationService
└── INewsTranslationService
```

Mục tiêu:

- News không bị phá.
- FAQ không bị phá.
- Partner không bị phá.
- Gallery dùng abstraction chung.
- Không tạo thêm provider Google thứ hai.
- Không nhân đôi logic lấy credential hoặc quota.

## 5.2. Giữ config Google Translation hiện có

Ưu tiên tái sử dụng config hiện tại:

```text
api_code = NEWS_TRANSLATION_GOOGLE_CLOUD
provider = GOOGLE_CLOUD_TRANSLATION
purpose = NEWS_TRANSLATION
```

Không bắt buộc đổi `api_code` ngay vì có thể phá config đang chạy.

Có thể đổi display label ở UI quản trị thành tên chung hơn, nhưng không được làm gián đoạn runtime.

## 5.3. Batch translation

Provider hiện phải hỗ trợ truyền nhiều chuỗi trong một request:

```json
{
  "contents": [
    "Tòa Alpha",
    "Trước tòa"
  ],
  "mimeType": "text/plain",
  "sourceLanguageCode": "vi",
  "targetLanguageCode": "en"
}
```

Không gọi từng chuỗi thành từng HTTP request riêng nếu chúng thuộc cùng một nghiệp vụ.

Ví dụ:

```text
Tạo area mới + location đầu tiên
→ 1 HTTP request Google chứa 2 chuỗi
```

Batch giảm số lần request, mặc dù quota ký tự vẫn tính theo tổng ký tự.

---

# 6. Chuẩn hóa và hash nguồn

## 6.1. Tạo helper dùng chung

Tạo helper/service riêng, ví dụ:

```text
TranslationSourceNormalizer
TranslationSourceHasher
GalleryTranslationCoordinator
```

Không để từng handler tự viết logic riêng.

## 6.2. Normalize nguồn

Trước khi tính hash:

```text
Trim đầu và cuối.
Gộp nhiều khoảng trắng liên tiếp thành một khoảng trắng.
Giữ nguyên dấu tiếng Việt.
Giữ nguyên chữ hoa/chữ thường.
Dùng đúng giá trị sẽ lưu vào database.
```

Ví dụ:

```text
"  Tòa    Alpha  "
→ "Tòa Alpha"
```

## 6.3. Hash

Dùng:

```text
SHA-256
Encoding UTF-8
Hex 64 ký tự
```

Hash được lưu tại:

```text
translation_source_hash
```

## 6.4. Quy tắc quyết định dịch

Không gọi dịch nếu đồng thời:

```text
Nguồn VI không thay đổi.
Hash khớp.
translation_status = READY.
Trường EN có dữ liệu.
```

Cần gọi dịch khi:

```text
Entity được tạo mới.
Nguồn VI thay đổi.
Người dùng chủ động bấm Dịch lại.
Backfill phát hiện EN thiếu hoặc metadata không hợp lệ.
```

Không tự retry `FAILED` trong mọi lần save không liên quan.

Ví dụ:

| Thao tác | Gọi Translation API |
|---|---:|
| Đổi tên area | Có |
| Đổi tên location | Có |
| Đổi title item | Có |
| Thay area cover video | Không |
| Thay location cover image | Không |
| Chuyển location sang area khác | Không |
| Thêm ảnh Gallery Item | Không |
| Xóa ảnh | Không |
| Đổi primary | Không |
| Thay audio VI/EN | Không |
| Sửa description VI/EN | Không |
| Đổi trạng thái | Không |
| Bấm Dịch lại | Có |

---

# 7. Translation result model

Nên tạo model nội bộ để tránh handler tự xử lý lộn xộn.

Ví dụ:

```csharp
public sealed class TranslationPreparationResult
{
    public string SourceText { get; init; }
    public string SourceHash { get; init; }
    public string? TranslatedText { get; init; }
    public bool Success { get; init; }
    public string Status { get; init; }
    public string? ErrorCode { get; init; }
}
```

Hoặc coordinator nhận danh sách key:

```text
AREA_NAME
LOCATION_NAME
ITEM_TITLE
```

và trả dictionary:

```text
AREA_NAME     → translated text
LOCATION_NAME → translated text
```

Phải bảo đảm map đúng thứ tự kết quả.

---

# 8. Create Gallery Area / Location

## 8.1. File chính

Cập nhật handler tạo area/location, ví dụ:

```text
CreateGalleryLocationCommandHandler
```

## 8.2. Trường hợp thêm location vào area có sẵn

Luồng:

1. Xác thực Staff Leader.
2. Lấy campus từ current user.
3. Normalize `locationName`.
4. Validate location name.
5. Kiểm tra area tồn tại, active, đúng campus.
6. Kiểm tra location key không trùng.
7. Upload cover location theo logic hiện tại.
8. Dịch `locationName`.
9. Tạo location với VI, EN và metadata.
10. Save.
11. Audit.

Không dịch lại area hiện có.

### Thành công

```text
LocationName              = nguồn VI
LocationNameEn            = translated EN
TranslationSource         = AUTO
TranslationStatus         = READY
TranslationSourceHash     = hash
TranslatedAt              = now
```

### Provider lỗi

```text
LocationName              = nguồn VI
LocationNameEn            = NULL
TranslationSource         = AUTO
TranslationStatus         = FAILED
TranslationSourceHash     = hash
TranslatedAt              = NULL
```

Request tạo location vẫn thành công.

## 8.3. Trường hợp tạo area mới + location đầu tiên

Luồng:

1. Normalize `newAreaName`.
2. Normalize `locationName`.
3. Validate.
4. Kiểm tra area key không trùng.
5. Validate/upload area cover video.
6. Validate/upload location cover image.
7. Batch dịch:

```text
[
  newAreaName,
  locationName
]
```

8. Tạo `GalleryArea`.
9. Tạo `GalleryLocation`.
10. Ghi VI/EN/metadata cho cả hai.
11. Commit area + location trong cùng transaction.
12. Audit.

Nếu dịch lỗi:

- Vẫn tạo area và location.
- Cả hai entity tương ứng được đánh dấu `FAILED`.
- Không rollback vì translation failure.
- Nếu chỉ một kết quả lỗi, xử lý theo entity tương ứng nếu provider/model cho phép; nếu provider trả lỗi toàn request thì cả batch là `FAILED`.

## 8.4. Không nhân đôi chuỗi trong batch

Nếu hai source giống nhau:

```text
newAreaName = "Alpha"
locationName = "Alpha"
```

Có thể deduplicate trước khi gửi:

```text
["Alpha"]
```

Sau đó map cùng kết quả về hai entity.

---

# 9. Update Gallery Area / Location

## 9.1. File chính

Cập nhật:

```text
UpdateGalleryLocationCommandHandler
```

## 9.2. Dữ liệu phải đọc trước

Trước khi thay đổi:

```text
oldLocationName
oldLocationNameEn
oldLocationTranslationStatus
oldLocationTranslationSourceHash

oldAreaName nếu tạo/sửa area liên quan
```

## 9.3. Chỉ thay cover location

Không gọi dịch.

Giữ nguyên:

```text
LocationNameEn
TranslationSource
TranslationStatus
TranslationSourceHash
TranslatedAt
```

## 9.4. Chỉ chuyển location sang area khác

Không gọi dịch nếu `locationName` không đổi.

Không dịch lại area đích.

## 9.5. Đổi tên location

1. Normalize title mới.
2. So sánh với tên đã normalize cũ.
3. Nếu thay đổi:
   - Tính hash.
   - Gọi dịch.
   - Cập nhật EN và metadata.
4. Nếu không thay đổi:
   - Không gọi dịch.
   - Giữ metadata cũ.

## 9.6. Tạo area mới trong lúc sửa location

Luôn cần dịch tên area mới.

Chỉ dịch location nếu tên location thay đổi.

Ví dụ:

```text
newAreaName thay đổi
locationName giữ nguyên
→ batch chỉ có newAreaName
```

Nếu cả hai thay đổi:

```text
[
  newAreaName,
  newLocationName
]
```

## 9.7. Thay area cover video của area hiện có

Không gọi dịch nếu area name không đổi.

---

# 10. Create Gallery Item

## 10.1. File chính

Cập nhật:

```text
AddGalleryItemCommandHandler
```

## 10.2. Phạm vi dịch

Chỉ dịch:

```text
title
```

Không dịch:

```text
descriptionVi
descriptionEn
audioVi
audioEn
caption
altText
```

## 10.3. Luồng

1. Giữ toàn bộ validation hiện tại.
2. Normalize title theo logic hiện có.
3. Tính hash title.
4. Gọi Google Translation VI → EN.
5. Upload audio/media theo logic hiện tại.
6. Tạo `GalleryItem`.
7. Gán:

```text
Title
TitleEn
TranslationSource
TranslationStatus
TranslationSourceHash
TranslatedAt
```

8. Tạo content và media.
9. Save.
10. Audit.
11. Nếu provider lỗi:
    - Vẫn save item.
    - `TitleEn = NULL`.
    - `TranslationStatus = FAILED`.
    - Response có warning.

## 10.4. Thời điểm gọi dịch và cleanup

Cần cân nhắc luồng upload file:

- Không được làm phát sinh file mồ côi.
- Nếu gọi dịch trước upload và dịch lỗi thì vẫn tiếp tục tạo item.
- Nếu gọi dịch sau upload và DB fail thì cơ chế compensation hiện tại vẫn phải cleanup file.
- Translation failure không được đi vào catch chung làm rollback toàn bộ.

Nên bắt riêng lỗi translation và chuyển thành kết quả `FAILED`, không throw ra khỏi handler.

---

# 11. Update Gallery Item

## 11.1. File chính

Cập nhật:

```text
UpdateGalleryItemCommandHandler
```

## 11.2. Title không đổi

Nếu normalized title không đổi:

```text
Không gọi Translation API.
Không sửa TitleEn.
Không sửa TranslationStatus.
Không sửa TranslationSourceHash.
Không sửa TranslatedAt.
```

Áp dụng kể cả khi người dùng:

- Thêm ảnh.
- Xóa ảnh.
- Thêm YouTube.
- Xóa YouTube.
- Đổi primary.
- Đổi item type.
- Đổi location.
- Sửa description.
- Thay audio VI.
- Thay audio EN.

## 11.3. Title thay đổi

1. Normalize title mới.
2. Tính hash.
3. Gọi dịch.
4. Nếu thành công:
   - Cập nhật `TitleEn`.
   - `READY`.
5. Nếu lỗi:
   - `TitleEn = NULL`.
   - `FAILED`.
6. Tiếp tục lưu các thay đổi khác.

Không để bản dịch title cũ còn hiển thị khi title VI đã đổi.

---

# 12. Response và warning

## 12.1. Response model

Các response create/update nên có khả năng trả warning không phá contract cũ, ví dụ:

```json
{
  "message": "Đã tạo Gallery Item.",
  "warnings": [
    {
      "code": "GALLERY_TRANSLATION_FAILED",
      "message": "Đã lưu dữ liệu tiếng Việt. Bản dịch tiếng Anh chưa được tạo."
    }
  ]
}
```

Hoặc bổ sung:

```text
translationWarning
```

Giữ backward compatibility nếu frontend cũ chưa đọc warning.

## 12.2. Toast frontend Staff Leader

### Dịch thành công

```text
Đã tạo khu vực và vị trí.
```

### Lưu VI thành công, dịch lỗi

Hiển thị warning toast:

```text
Đã lưu dữ liệu tiếng Việt. Bản dịch tiếng Anh chưa được tạo; trang public sẽ tạm hiển thị tiếng Việt.
```

Không hiển thị như lỗi làm người dùng nghĩ create/update thất bại.

---

# 13. Logging và audit

Log tối thiểu:

```text
EntityType
EntityId
SourceHash
TranslationStatus
ProviderErrorCode
Timestamp
```

Không log:

```text
Service account JSON
Private key
Access token
Authorization header
Toàn bộ credential
```

Audit Gallery hiện có phải tiếp tục hoạt động.

Có thể bổ sung vào audit payload:

```text
translationStatus
hasEnglishName
hasEnglishTitle
```

Không cần lưu toàn bộ translated text vào audit nếu không cần.

---

# 14. Public DTO

Không đổi tên hoặc xóa field hiện tại.

Chỉ bổ sung field EN, nullable.

## 14.1. Area DTO

```text
areaName
areaNameEn
```

## 14.2. Location DTO

```text
locationName
locationNameEn
```

## 14.3. Gallery Item DTO

```text
title
titleEn
```

## 14.4. Grid preview

Nên bổ sung:

```text
descriptionPreview
descriptionPreviewEn
```

vì `description_en` đã tồn tại.

Nếu không bổ sung, card ở chế độ EN vẫn có thể hiển thị preview tiếng Việt.

## 14.5. DTO cần rà soát

Cập nhật tối thiểu:

```text
PublicGalleryAreaDto
PublicGalleryLocationDto
PublicGalleryAreaSummaryDto
PublicGalleryLocationSummaryDto
PublicGalleryGridItemDto
PublicGalleryShowcaseItemDto
PublicGalleryItemSummaryDto
```

## 14.6. Không expose metadata nội bộ

Public DTO không trả:

```text
translation_source
translation_status
translation_source_hash
translated_at
```

Backend chỉ quyết định:

```text
READY + EN có giá trị
→ trả EN

PENDING / FAILED / OUTDATED / EN rỗng
→ trả null
```

---

# 15. Public Query Handlers

## 15.1. Campus Navigation

Cập nhật:

```text
GetPublicCampusNavigationQueryHandler
```

Query phải đọc:

```text
AreaName
AreaNameEn
AreaTranslationStatus

LocationName
LocationNameEn
LocationTranslationStatus

Title
TitleEn
ItemTranslationStatus
```

Map:

```text
AreaNameEn =
    AreaTranslationStatus == READY && !blank
        ? AreaNameEn
        : null
```

Tương tự location và title.

## 15.2. Location Gallery Grid

Cập nhật:

```text
GetPublicLocationGalleryItemsQueryHandler
```

Bổ sung:

```text
AreaNameEn
LocationNameEn
TitleEn
DescriptionEn
```

Response:

```text
Area.AreaNameEn
Location.LocationNameEn
Items[].TitleEn
Items[].DescriptionPreviewEn
```

`DescriptionPreviewEn` dùng cùng logic cắt preview hiện tại nhưng lấy từ `description_en`.

## 15.3. Location Showcase

Cập nhật:

```text
GetPublicLocationShowcaseQueryHandler
```

Bổ sung:

```text
area.areaNameEn
location.locationNameEn
mediaItems[].titleEn
visitDelegationItems[].titleEn
```

## 15.4. Gallery Item Detail

Cập nhật:

```text
GetPublicGalleryItemDetailQueryHandler
```

Hiện description/audio đã có VI/EN.

Bổ sung:

```text
AreaNameEn
LocationNameEn
TitleEn
```

## 15.5. Không gọi translation trong query handler

Tất cả public query chỉ đọc DB.

Tuyệt đối không inject Translation Service vào public query.

---

# 16. Frontend public types

Cập nhật:

```text
frontend/pems-react/src/features/visit-fptu/publicVisitFptu.types.ts
```

Ví dụ:

```ts
export interface PublicGalleryArea {
  areaId: number;
  areaName: string;
  areaNameEn?: string | null;
}

export interface PublicGalleryLocation {
  locationId: number;
  locationName: string;
  locationNameEn?: string | null;
  title: string;
  titleEn?: string | null;
}

export interface PublicGalleryGridItem {
  galleryItemId: number;
  title: string;
  titleEn?: string | null;
  descriptionPreview: string;
  descriptionPreviewEn?: string | null;
}

export interface PublicGalleryShowcaseItem {
  galleryItemId: number;
  title: string;
  titleEn?: string | null;
}

export interface PublicGalleryItemSummary {
  galleryItemId: number;
  title: string;
  titleEn?: string | null;
}
```

Các field EN phải optional/null để giữ backward compatibility.

---

# 17. Frontend localized helper

## 17.1. Tạo helper chung

Tạo file, ví dụ:

```text
frontend/pems-react/src/shared/i18n/localizedDbText.ts
```

Nội dung:

```ts
export function localizedDbText(
  vi: string | null | undefined,
  en: string | null | undefined,
  language: string | undefined,
): string {
  const isEnglish = language?.toLowerCase().startsWith('en');

  if (isEnglish && en?.trim()) {
    return en.trim();
  }

  return vi?.trim() ?? '';
}
```

Có thể thêm helper object:

```ts
export function isEnglishLanguage(language?: string): boolean {
  return language?.toLowerCase().startsWith('en') ?? false;
}
```

## 17.2. Dùng `i18n.resolvedLanguage`

Trong component:

```ts
const { t, i18n } = useTranslation(...);
const language = i18n.resolvedLanguage ?? i18n.language;
```

Không chỉ so sánh:

```text
i18n.language === "en"
```

vì runtime có thể là:

```text
en
en-US
vi
vi-VN
```

---

# 18. Không lưu localized text vào state

Giữ state nguyên payload song ngữ:

```text
nav
grid
showcaseData
detailData
```

Không tạo state như:

```text
displayedAreaName
displayedLocationName
displayedTitle
```

Chỉ derive tại render:

```ts
const displayAreaName = localizedDbText(
  area.areaName,
  area.areaNameEn,
  language,
);
```

Khi global language thay đổi, React tự render lại.

Không thêm `i18n.language` vào dependency của các effect fetch API.

Mục tiêu:

```text
Bấm EN
→ không gọi lại navigation
→ không gọi lại grid
→ không gọi lại showcase
→ không gọi lại detail
```

---

# 19. Vị trí frontend phải thay

Cập nhật toàn bộ `CampusDetailVisitPage.tsx` và component liên quan.

Rà soát tất cả chỗ dùng trực tiếp:

```text
area.areaName
location.locationName
item.title
detail.galleryItem.title
```

Thay bằng helper localize tại các vị trí:

1. Sidebar area.
2. Sidebar location.
3. Hover/flyout location.
4. Ô area ở màn đầu.
5. Ô location ở màn đầu.
6. Area Showcase title.
7. Location thumbnail label.
8. Location Showcase heading.
9. MEDIA thumbnail tooltip.
10. VISIT_DELEGATION thumbnail tooltip.
11. Gallery grid card title.
12. Gallery grid description preview.
13. Breadcrumb trong detail modal.
14. Gallery item title trong modal.
15. `alt` của ảnh.
16. `title` của button/card.
17. Text share metadata nếu lấy title.
18. Empty/error message có chèn tên area/location.
19. Deep-link view có render tên.
20. Any breadcrumb/chip khác sử dụng tên DB.

Không chỉ sửa một màn hình.

---

# 20. Đồng bộ global language với detail modal

## 20.1. Trạng thái hiện tại

Detail modal có language state riêng:

```text
vi
en
```

Mặc định đang có thể luôn là VI.

## 20.2. Hành vi cần đạt

### Khi mở modal

```text
Header VI
→ modal mặc định VI

Header EN
→ modal mặc định EN
```

### Khi global language đổi lúc modal đang mở

1. Dừng audio đang phát.
2. Chuyển selected language theo global language.
3. Đổi description.
4. Đổi audio URL.
5. Đổi title/area/location.
6. Không autoplay audio mới.

### Manual tab trong modal

Người dùng vẫn được phép bấm tab VI/EN thủ công.

Cần quyết định rõ:

- Global language là giá trị mặc định.
- Sau khi người dùng tự chọn tab trong modal, có thể giữ lựa chọn thủ công cho đến khi global language đổi hoặc item đổi.
- Khi item đổi, reset theo global language hiện tại.

Không để hai audio chạy đồng thời.

---

# 21. Retry Gallery Translation

## 21.1. Command

Tạo command nội bộ, ví dụ:

```text
RetryGalleryTranslationCommand
```

Input:

```text
entityType:
- AREA
- LOCATION
- ITEM

entityId
```

## 21.2. Quyền

Chỉ:

```text
role = STAFF
sub_role = LEADER
đúng campus scope
active user
```

## 21.3. Hành vi

Retry chỉ dịch:

```text
AREA     → area_name
LOCATION → location_name
ITEM     → title
```

Không thay đổi:

- Cover.
- Media.
- Audio.
- Description.
- Status.
- Display order.
- Location relation.

## 21.4. Khi hiển thị nút

Admin/Staff Leader UI có thể hiển thị “Dịch lại” khi:

```text
PENDING
FAILED
OUTDATED
EN missing
```

Không bắt buộc public biết trạng thái này.

## 21.5. Kết quả

Thành công:

```text
READY
```

Thất bại:

```text
FAILED
```

Ghi audit phù hợp.

---

# 22. Backfill dữ liệu cũ

## 22.1. Command

Tạo:

```text
BackfillGalleryTranslationsCommand
```

hoặc script/app command tương đương.

## 22.2. Phạm vi

```text
gallery_areas
gallery_locations
gallery_items
```

## 22.3. Điều kiện chọn row

```text
EN IS NULL
hoặc translation_status IN (PENDING, FAILED, OUTDATED)
hoặc source hash thiếu
hoặc source hash không khớp nguồn hiện tại
```

## 22.4. Batch

- Đọc theo batch DB.
- Gom chuỗi.
- Deduplicate.
- Chia request Google theo giới hạn hợp lý, ví dụ 50 chuỗi/request.
- Map kết quả đúng entity.
- Save theo batch.
- Log số thành công/thất bại.

## 22.5. Idempotent

Chạy lại phải an toàn:

```text
READY + hash đúng + EN có dữ liệu
→ skip
```

## 22.6. Không tự chạy khi startup

Tuyệt đối không tự backfill mỗi lần backend khởi động.

Chỉ chạy có chủ đích:

- Admin command.
- Maintenance script.
- One-time operation.
- Secured internal endpoint nếu dự án có chuẩn quản trị tương ứng.

Không gọi từ public request.

---

# 23. Validation translated text

Trước khi ghi DB:

```text
Kết quả không null.
Kết quả không rỗng sau Trim.
Số kết quả bằng số input.
Độ dài không vượt cột.
Map đúng thứ tự.
```

Nếu không hợp lệ:

```text
Đánh dấu FAILED.
Không lưu EN rỗng.
Không rollback dữ liệu VI.
```

Không tự cắt chuỗi để ép vừa cột.

Nếu cần hỗ trợ translated string dài hơn, sửa schema.

---

# 24. Error handling

## 24.1. Provider không cấu hình

Nếu thiếu config hoặc credential:

```text
Translation result = FAILED
Gallery create/update vẫn tiếp tục
Log warning
Response có warning
```

Không trả 500 cho toàn bộ nghiệp vụ Gallery.

## 24.2. Timeout

- Bắt timeout riêng.
- Không retry vô hạn.
- Dùng retry policy hiện tại nếu có.
- Không làm request Staff Leader treo quá lâu.

Có thể cân nhắc best-effort timeout ngắn phù hợp.

## 24.3. Provider rate limit/quota

- Đánh dấu FAILED.
- Không tự loop retry trong request.
- Cho phép retry bằng command sau.

---

# 25. API contract và compatibility

## 25.1. Giữ field cũ

Không đổi:

```text
areaName
locationName
title
```

Chúng tiếp tục là tiếng Việt.

Chỉ bổ sung:

```text
areaNameEn
locationNameEn
titleEn
```

## 25.2. Frontend cũ

Frontend cũ vẫn hoạt động vì field cũ giữ nguyên.

## 25.3. Backend cũ trong giai đoạn rollout

Frontend field EN phải optional để không crash khi backend chưa được deploy đồng thời.

---

# 26. Test backend

## 26.1. Unit test Translation foundation

- Normalize khoảng trắng đúng.
- Hash ổn định.
- Cùng source cho cùng hash.
- Source khác cho hash khác.
- Batch giữ đúng thứ tự.
- Deduplicate map đúng.

## 26.2. Create location trong area cũ

- Chỉ gọi dịch một chuỗi.
- Không dịch area.
- Lưu `LocationNameEn`.
- Status `READY`.

## 26.3. Create area mới + location

- Chỉ gọi provider một lần.
- Request chứa hai source.
- Map đúng:
  - result 0 → area.
  - result 1 → location.
- Hai status `READY`.

## 26.4. Translation create lỗi

- Area/location VI vẫn tồn tại.
- EN null.
- Status `FAILED`.
- Response có warning.
- Không làm hỏng transaction area/location.

## 26.5. Update cover

- Provider không được gọi.
- Metadata dịch giữ nguyên.

## 26.6. Update location name

- Provider được gọi đúng một lần.
- Hash mới.
- EN mới.
- Status `READY`.

## 26.7. Move location không đổi tên

- Provider không được gọi.

## 26.8. Create Gallery Item

- Chỉ dịch title.
- Không dịch description.
- Không dịch media caption.
- `TitleEn` được lưu.

## 26.9. Update item không đổi title

Các case:

- Thay audio.
- Thêm ảnh.
- Xóa ảnh.
- Đổi primary.
- Thêm YouTube.
- Đổi location.
- Đổi item type.
- Sửa mô tả.

Tất cả phải xác nhận provider không được gọi.

## 26.10. Update title

- Provider được gọi.
- EN cũ không được giữ khi source đổi và dịch lỗi.
- Hash cập nhật.

## 26.11. Public fallback

| Status | EN field public |
|---|---|
| READY + EN | Giá trị EN |
| READY + EN rỗng | null |
| PENDING | null |
| FAILED | null |
| OUTDATED | null |

---

# 27. Integration test

Tạo test end-to-end backend:

1. Login Staff Leader.
2. Tạo area mới + location.
3. Kiểm tra DB:
   - VI.
   - EN.
   - hash.
   - status.
4. Tạo Gallery Item.
5. Kiểm tra `title_en`.
6. Gọi navigation API.
7. Gọi location grid API.
8. Gọi location showcase API.
9. Gọi detail API.
10. Xác nhận tất cả trả cả VI và EN.
11. Simulate provider failure.
12. Xác nhận fallback.

---

# 28. Frontend test

## 28.1. Chuyển ngôn ngữ

1. Mở Gallery ở VI.
2. Ghi nhận request network.
3. Bấm EN.
4. Xác nhận đổi:
   - Area name.
   - Location name.
   - Item title.
   - Breadcrumb.
   - Detail modal.
5. Không có request Translation API.
6. Không gọi lại public navigation/showcase/detail.
7. Bấm VI/EN nhiều lần vẫn không phát sinh request mới.

## 28.2. State preservation

Khi đổi ngôn ngữ:

- Không reset area đang chọn.
- Không reset location.
- Không reset active thumbnail.
- Không reset carousel.
- Không đóng modal.
- Không thay item đang xem.
- Không mất deep-link state.

## 28.3. Fallback

Nếu EN null:

- Hiển thị VI.
- Không để chuỗi rỗng.
- Không hiển thị `"undefined"` hoặc `"null"`.

## 28.4. Modal/audio

- Header EN → mở modal mặc định EN.
- Audio VI đang phát, đổi global EN → audio dừng.
- Không autoplay audio EN.
- User vẫn có thể chọn VI thủ công.

---

# 29. Real-stack validation

Chạy thực tế:

```text
React
→ .NET API
→ MySQL
→ Google Cloud Translation
```

Test tối thiểu:

1. Tạo area `Tòa Alpha`.
2. Tạo location `Trước tòa`.
3. Kiểm tra DB có EN.
4. Tạo item `Tượng rồng Việt Nam`.
5. Kiểm tra DB có `title_en`.
6. Mở public.
7. Bấm EN.
8. Xác nhận UI đổi.
9. Ghi nhận Network:
   - Không gọi translate.
   - Không reload API không cần thiết.
10. Tắt translation config.
11. Tạo dữ liệu mới.
12. Xác nhận VI vẫn được lưu và public fallback VI.
13. Bật lại config.
14. Retry.
15. Xác nhận chuyển `READY`.

---

# 30. File dự kiến cần thay đổi

Tên file có thể khác nhẹ theo codebase, nhưng tối thiểu rà soát:

## Backend Application

```text
News/Services/INewsTranslationService.cs
Translation/IContentTranslationService.cs
Galleries/Common/TranslationSourceNormalizer.cs
Galleries/Common/TranslationSourceHasher.cs
Galleries/Common/GalleryTranslationCoordinator.cs

Galleries/Commands/CreateGalleryLocation/CreateGalleryLocationCommandHandler.cs
Galleries/Commands/UpdateGalleryLocation/UpdateGalleryLocationCommandHandler.cs
Galleries/Commands/AddGalleryItem/AddGalleryItemCommandHandler.cs
Galleries/Commands/UpdateGalleryItem/UpdateGalleryItemCommandHandler.cs

Galleries/Commands/RetryGalleryTranslation/*
Galleries/Commands/BackfillGalleryTranslations/*

Galleries/Public/Common/PublicGalleryDtos.cs
Galleries/Public/Queries/GetPublicCampusNavigation/GetPublicCampusNavigationQueryHandler.cs
Galleries/Public/Queries/GetPublicLocationGalleryItem/GetPublicLocationGalleryItemQueryHandler.cs
Galleries/Public/Queries/GetPublicLocationShowcase/GetPublicLocationShowcaseQueryHandler.cs
Galleries/Public/Queries/GetPublicGalleryItemDetail/GetPublicGalleryItemDetailQueryHandler.cs
```

## Backend Infrastructure

```text
Translation/GoogleCloudTranslationService.cs
DependencyInjection.cs
```

## Domain

```text
Entities/Galleries/GalleryArea.cs
Entities/Galleries/GalleryLocation.cs
Entities/Galleries/GalleryItem.cs
```

Chỉ sửa entity nếu mapping/length chưa đúng.

## API

Nếu cần endpoint retry/backfill:

```text
Controllers/GalleriesController.cs
```

Endpoint phải bảo mật và campus-scoped.

## Frontend

```text
features/visit-fptu/publicVisitFptu.types.ts
pages/CampusDetailVisitPage.tsx
shared/i18n/localizedDbText.ts

features/gallery-management/types/galleryManagement.types.ts
pages/dashboard/gallery/LocationManagementStaffLeader.tsx
pages/dashboard/gallery/GalleryManagementStaffLeader.tsx
```

Frontend Staff Leader chỉ cần sửa thêm nếu hiển thị warning/retry/status dịch.

## Database

```text
PEMS_FULL_V2_I18N_GOOGLE_VISION_FACE_SCAN_COMPLETE_FIXED.sql
docs/database/... additive migration tương ứng
```

## Tests

```text
tests/PEMS.UnitTests/Galleries/*
tests/PEMS.IntegrationTests/Galleries/*
frontend tests tương ứng
```

---

# 31. Thứ tự triển khai

## Phase 1 – Database

- Kiểm tra length.
- Cập nhật full baseline.
- Tạo additive migration.
- Không thêm bảng dịch mới.

## Phase 2 – Translation foundation

- Abstraction dùng chung.
- Provider dùng chung.
- DI.
- Normalizer.
- Hasher.
- Coordinator.
- Unit test.

## Phase 3 – Write path

- Create area/location.
- Update area/location.
- Create item.
- Update item.
- Warning.
- Logging/audit.

## Phase 4 – Public backend

- DTO EN.
- Navigation.
- Grid.
- Showcase.
- Detail.
- Fallback theo status.

## Phase 5 – Public frontend

- Type EN.
- Localized helper.
- Rà soát toàn bộ UI.
- Không refetch theo language.
- Đồng bộ modal/audio.

## Phase 6 – Retry/backfill

- Retry command.
- Backfill command.
- Scope.
- Audit.
- Idempotency.

## Phase 7 – Validation

- Unit.
- Integration.
- Frontend.
- Real-stack.

---

# 32. Những điều tuyệt đối không làm

Không:

- Dịch tại public read time.
- Dịch mỗi lần bấm EN.
- Gọi Google từ React.
- Gọi lại API public chỉ vì đổi language.
- Tạo cache runtime thay cho lưu DB.
- Giữ bản dịch cũ khi source VI đã đổi.
- Cắt chuỗi translated text.
- Rollback Gallery create/update chỉ vì dịch lỗi.
- Ghi credential vào log.
- Expose translation metadata ra public.
- Tự chạy backfill mỗi startup.
- Làm hỏng News/FAQ/Partner translation.
- Thay đổi scope Staff Leader.
- Thay đổi logic media/audio hiện hữu.
- Đưa tên AI/Claude/Agent vào commit message.

---

# 33. Acceptance criteria

Chỉ coi là hoàn thành khi đạt đủ:

## Write side

- Tạo area mới dịch area + location bằng một batch.
- Tạo location trong area cũ chỉ dịch location.
- Tạo Gallery Item dịch title.
- Sửa tên mới dịch lại.
- Sửa cover/media/audio không gọi dịch.
- Translation lỗi vẫn lưu VI.
- Hash và status đúng.

## Public backend

- Navigation trả VI + EN.
- Grid trả VI + EN.
- Showcase trả VI + EN.
- Detail trả VI + EN.
- EN chỉ trả khi `READY`.

## Public frontend

- Header VI/EN đổi area/location/title.
- Không gọi translation.
- Không refetch public API.
- Không reset state.
- Fallback VI khi EN thiếu.
- Modal/audio đồng bộ.

## Operations

- Có retry.
- Có backfill idempotent.
- Không chạy backfill tự động.
- Có log an toàn.
- Có test.

---

# 34. Kết quả mẫu

## Database sau create

```text
gallery_areas
area_name              = "Tòa Alpha"
area_name_en           = "Alpha Building"
translation_source     = "AUTO"
translation_status     = "READY"
translation_source_hash= "<sha256>"
translated_at          = "<Vietnam time>"
```

```text
gallery_locations
location_name              = "Trước tòa"
location_name_en           = "In Front of the Building"
translation_source         = "AUTO"
translation_status         = "READY"
translation_source_hash    = "<sha256>"
translated_at              = "<Vietnam time>"
```

```text
gallery_items
title                   = "Tượng rồng Việt Nam"
title_en                = "Vietnamese Dragon Statue"
translation_source      = "AUTO"
translation_status      = "READY"
translation_source_hash = "<sha256>"
translated_at           = "<Vietnam time>"
```

## Public response mẫu

```json
{
  "areaId": 1,
  "areaName": "Tòa Alpha",
  "areaNameEn": "Alpha Building",
  "locations": [
    {
      "locationId": 1,
      "locationName": "Trước tòa",
      "locationNameEn": "In Front of the Building",
      "title": "Tượng rồng Việt Nam",
      "titleEn": "Vietnamese Dragon Statue"
    }
  ]
}
```

## Frontend

```text
Header VI
→ Tòa Alpha
→ Trước tòa
→ Tượng rồng Việt Nam

Header EN
→ Alpha Building
→ In Front of the Building
→ Vietnamese Dragon Statue
```

Số lần gọi Google Translation khi người dùng đổi VI/EN:

```text
0
```

---

# 35. Yêu cầu làm việc của AI Agent

1. Đọc codebase trước khi sửa.
2. Xác nhận branch đang làm việc.
3. Không sửa nhánh `Dev` nếu người dùng không yêu cầu.
4. Không tự thay đổi database đang chạy.
5. Không chạy destructive SQL.
6. Không làm thay đổi ngoài scope.
7. Chạy test sau từng phase.
8. Báo cáo file đã sửa.
9. Báo cáo test đã chạy.
10. Báo cáo phần chưa thể xác minh.
11. Gom thay đổi thành các commit logic, không tạo quá nhiều commit nhỏ một file.
12. Commit message không chứa tên AI hoặc công cụ.

Commit gợi ý:

```text
feat(gallery): persist translated gallery names and titles
feat(gallery): expose bilingual public gallery data
feat(gallery): localize public gallery database content
test(gallery): cover gallery translation workflows
```

Không commit khi chưa được phép nếu workflow hiện tại yêu cầu review trước.
