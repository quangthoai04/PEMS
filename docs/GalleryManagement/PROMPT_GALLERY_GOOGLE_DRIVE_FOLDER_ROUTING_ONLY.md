# PROMPT / ĐẶC TẢ CODE — Chỉ đổi nơi lưu trữ ảnh/video Gallery trên Google Drive theo từng folder đã config

## 0. Mục tiêu chính

Hiện tại chức năng upload và frontend Gallery đã ổn. **Không sửa UI, không đổi flow upload, không đổi API contract nếu không bắt buộc.**

Yêu cầu lần này chỉ là: khi upload ảnh/video Gallery, backend phải đưa file vào đúng folder con trên Google Drive đã được tạo sẵn và đã config trong `GoogleDrive` options.

Cần tách nơi lưu trữ như sau:

| Loại upload Gallery | Bảng nghiệp vụ lưu file | Folder Google Drive cần dùng |
|---|---|---|
| Ảnh đại diện khu vực | `gallery_areas.cover_file_id` | `GalleryAreaFolderId` |
| Ảnh đại diện vị trí | `gallery_locations.cover_file_id` | `GalleryLocationFolderId` |
| Ảnh/video Gallery item loại `MEDIA` | `gallery_item_media.file_id` | `GalleryItemFolderId` |
| Ảnh/video Gallery item loại `VISIT_DELEGATION` / khách tham quan | `gallery_item_media.file_id` | `GalleryDelegationFolderId` |

Folder cha `GalleryFolderId` vẫn có thể giữ làm folder tổng/fallback, nhưng upload thực tế của 4 nhóm trên phải đi vào đúng folder con.

---

## 1. Phạm vi sửa

### 1.1. Trong scope

Chỉ sửa backend storage routing:

```text
1. Kiểm tra GoogleDriveOptions đã có đủ folder id mới chưa.
2. Nếu thiếu property thì bổ sung property tương ứng.
3. Mở rộng FilePurpose nếu cần để phân biệt rõ:
   - GalleryAreaCover
   - GalleryLocationCover
   - GalleryItemImage
   - GalleryItemVideo
   - GalleryDelegationImage
   - GalleryDelegationVideo
4. Sửa GoogleDriveFolderResolver để map từng FilePurpose vào đúng folder id.
5. Sửa handler upload khu vực/vị trí/item để truyền đúng FilePurpose.
6. Sửa FileValidationPolicy / FilePurposeDbValues / ObjectKeyPrefix nếu thêm FilePurpose mới.
7. Chạy build/test để đảm bảo upload vẫn chạy, chỉ đổi folder đích.
```

### 1.2. Ngoài scope

Không làm các phần sau:

```text
1. Không sửa lại frontend nếu hiện tại đã upload ổn.
2. Không đổi giao diện quản lý khu vực/gallery.
3. Không đổi public gallery UI.
4. Không đổi logic tạo/sửa area/location/item ngoài việc truyền đúng FilePurpose khi upload.
5. Không đổi RBAC/phân quyền.
6. Không đổi RefreshToken/OAuth flow nếu token hiện tại đang dùng được.
7. Không gọi Google Drive trực tiếp trong handler nghiệp vụ.
8. Không hard-code folderId trong handler.
9. Không lưu binary/base64 vào MySQL.
10. Không trả direct Google Drive URL cho frontend.
11. Không xóa file cũ khỏi Google Drive khi thay ảnh, trừ khi hệ thống đã có rule riêng từ trước.
12. Không tạo mock data hoặc file rác.
```

---

## 2. Tài liệu và file cần đọc trước khi code

AI Agent phải đọc source thật trước khi sửa, không code theo trí nhớ.

Ưu tiên đọc các file sau trong project:

```text
backend/PEMS.Application/Common/Files/FilePurpose.cs
backend/PEMS.Application/Common/Files/FileValidationPolicy.cs
backend/PEMS.Application/Common/Files/FileObjectKeyBuilder.cs
backend/PEMS.Application/Common/Files/FileUploadService.cs
backend/PEMS.Application/Common/Interfaces/IFileUploadService.cs
backend/PEMS.Application/Common/Interfaces/IFileStorageFolderResolver.cs
backend/PEMS.Application/Common/Storage/GoogleDriveOptions.cs
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveFolderResolver.cs
backend/PEMS.Infrastructure/FileStorage/GoogleDrive/GoogleDriveStorageService.cs
backend/PEMS.Api/appsettings.json
backend/PEMS.Api/appsettings.Development.json
```

Tìm các handler upload Gallery hiện tại:

```text
Create/Update Gallery Area hoặc Location
Create/Update Gallery Item
Các command/handler có dùng IFileUploadService
Các command/handler có FilePurpose.GalleryImage hoặc FilePurpose.GalleryVideo
```

Gợi ý lệnh tìm:

```bash
grep -R "FilePurpose.Gallery" -n backend/PEMS.Application backend/PEMS.Infrastructure backend/PEMS.Api
grep -R "UploadBusinessFileAsync" -n backend/PEMS.Application
grep -R "GalleryFolderId\|GalleryAreaFolderId\|GalleryLocationFolderId\|GalleryItemFolderId\|GalleryDelegationFolderId" -n backend
```

---

## 3. Kiến trúc bắt buộc phải giữ

Luồng upload chuẩn hiện tại phải giữ nguyên:

```text
Business Handler
→ IFileUploadService.UploadBusinessFileAsync(..., FilePurpose.X, ...)
→ FileUploadService validate/checksum/object_key
→ IFileStorageFolderResolver.ResolveFolderId(FilePurpose.X)
→ GoogleDriveStorageService upload lên Google Drive
→ FileUploadService insert metadata vào bảng files
→ Handler lưu uploaded.FileId vào bảng nghiệp vụ tương ứng
```

Không được sửa theo hướng:

```text
Handler nghiệp vụ → đọc appsettings → lấy folderId → gọi Google Drive trực tiếp
```

Handler nghiệp vụ chỉ được chọn **FilePurpose**, không chọn folderId.

---

## 4. Config GoogleDrive cần có

Trong `GoogleDriveOptions`, cần có tối thiểu các property sau.

Nếu đã có rồi thì dùng đúng tên đang có trong code, không tạo trùng property.

```csharp
public string GalleryFolderId { get; set; } = string.Empty;

public string GalleryAreaFolderId { get; set; } = string.Empty;
public string GalleryLocationFolderId { get; set; } = string.Empty;
public string GalleryItemFolderId { get; set; } = string.Empty;
public string GalleryDelegationFolderId { get; set; } = string.Empty;
```

Trong `appsettings.Development.json`, các key đã được người dùng config sẵn. Chỉ kiểm tra key có bind đúng với `GoogleDriveOptions` hay không.

Ví dụ cấu trúc mong muốn:

```json
{
  "GoogleDrive": {
    "RootFolderId": "ID_ROOT_FOLDER",

    "GalleryFolderId": "ID_FOLDER_gallery-media",
    "GalleryAreaFolderId": "ID_FOLDER_gallery-area",
    "GalleryLocationFolderId": "ID_FOLDER_gallery-location",
    "GalleryItemFolderId": "ID_FOLDER_gallery-item",
    "GalleryDelegationFolderId": "ID_FOLDER_gallery-delegation"
  }
}
```

Nếu project có `appsettings.Development.example.json`, cập nhật placeholder tương ứng nhưng không commit secret thật:

```json
{
  "GoogleDrive": {
    "GalleryAreaFolderId": "YOUR_GALLERY_AREA_FOLDER_ID",
    "GalleryLocationFolderId": "YOUR_GALLERY_LOCATION_FOLDER_ID",
    "GalleryItemFolderId": "YOUR_GALLERY_ITEM_FOLDER_ID",
    "GalleryDelegationFolderId": "YOUR_GALLERY_DELEGATION_FOLDER_ID"
  }
}
```

---

## 5. FilePurpose cần dùng

### 5.1. Cách làm khuyến nghị

Nếu hiện tại `FilePurpose` mới chỉ có:

```csharp
GalleryImage,
GalleryVideo
```

thì cần mở rộng để routing folder chính xác hơn:

```csharp
public enum FilePurpose
{
    UserAvatar,

    GalleryAreaCover,
    GalleryLocationCover,

    GalleryItemImage,
    GalleryItemVideo,

    GalleryDelegationImage,
    GalleryDelegationVideo,

    GalleryImage, // legacy nếu còn code cũ
    GalleryVideo, // legacy nếu còn code cũ

    NewsImage,
    NewsAttachment,
    Document,
    MinutesAttachment,
    VisitRequestAttachment,
    PartnerDocument,
    LogisticsAttachment,
    Other
}
```

Nếu không muốn giữ legacy `GalleryImage/GalleryVideo`, phải sửa toàn bộ chỗ dùng cũ sang purpose mới và đảm bảo build xanh.

### 5.2. DB value cho `files.file_purpose`

Thêm mapping DB value:

```csharp
public const string GalleryAreaCover = "GALLERY_AREA_COVER";
public const string GalleryLocationCover = "GALLERY_LOCATION_COVER";

public const string GalleryItemImage = "GALLERY_ITEM_IMAGE";
public const string GalleryItemVideo = "GALLERY_ITEM_VIDEO";

public const string GalleryDelegationImage = "GALLERY_DELEGATION_IMAGE";
public const string GalleryDelegationVideo = "GALLERY_DELEGATION_VIDEO";
```

Cập nhật extension `ToDbValue()`:

```csharp
FilePurpose.GalleryAreaCover => FilePurposeDbValues.GalleryAreaCover,
FilePurpose.GalleryLocationCover => FilePurposeDbValues.GalleryLocationCover,
FilePurpose.GalleryItemImage => FilePurposeDbValues.GalleryItemImage,
FilePurpose.GalleryItemVideo => FilePurposeDbValues.GalleryItemVideo,
FilePurpose.GalleryDelegationImage => FilePurposeDbValues.GalleryDelegationImage,
FilePurpose.GalleryDelegationVideo => FilePurposeDbValues.GalleryDelegationVideo,
```

Lưu ý:

```text
Nếu files.file_purpose là VARCHAR thì không cần SQL patch cho file_purpose.
Nếu files.file_purpose là ENUM/CHECK constraint trong source thật thì phải tạo patch thêm các giá trị mới.
Không tự bịa DB value nếu DB không cho phép.
```

### 5.3. Object key prefix

Cập nhật `ToObjectKeyPrefix()` để dễ trace file:

```csharp
FilePurpose.GalleryAreaCover => "gallery/areas",
FilePurpose.GalleryLocationCover => "gallery/locations",
FilePurpose.GalleryItemImage => "gallery/items",
FilePurpose.GalleryItemVideo => "gallery/items",
FilePurpose.GalleryDelegationImage => "gallery/delegations",
FilePurpose.GalleryDelegationVideo => "gallery/delegations",
```

Nếu muốn đơn giản hơn, có thể dùng prefix:

```text
gallery-area
gallery-location
gallery-item
gallery-delegation
```

Quan trọng: object key chỉ để trace trong DB, không quyết định folder Google Drive. Folder Google Drive phải do `GoogleDriveFolderResolver` quyết định.

---

## 6. Sửa GoogleDriveFolderResolver

Mục tiêu: `ResolveFolderId(FilePurpose purpose)` phải map đúng folder id theo purpose.

Pseudo code:

```csharp
public string ResolveFolderId(FilePurpose purpose)
{
    var folderId = purpose switch
    {
        FilePurpose.GalleryAreaCover => _options.GalleryAreaFolderId,
        FilePurpose.GalleryLocationCover => _options.GalleryLocationFolderId,

        FilePurpose.GalleryItemImage or FilePurpose.GalleryItemVideo =>
            _options.GalleryItemFolderId,

        FilePurpose.GalleryDelegationImage or FilePurpose.GalleryDelegationVideo =>
            _options.GalleryDelegationFolderId,

        // Legacy fallback nếu source còn gọi GalleryImage/GalleryVideo cũ.
        // Nên map về GalleryItemFolderId để không còn rơi vào folder gallery chung.
        FilePurpose.GalleryImage or FilePurpose.GalleryVideo =>
            !string.IsNullOrWhiteSpace(_options.GalleryItemFolderId)
                ? _options.GalleryItemFolderId
                : _options.GalleryFolderId,

        FilePurpose.UserAvatar => _options.AvatarFolderId,

        // Các purpose khác giữ nguyên logic hiện tại.
        _ => ResolveExistingNonGalleryFolder(purpose)
    };

    if (string.IsNullOrWhiteSpace(folderId))
    {
        throw new BusinessRuleException(
            $"Google Drive folder is not configured for purpose {purpose}.",
            "GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED");
    }

    return folderId;
}
```

Lưu ý constructor `BusinessRuleException` trong source thật có thể là `(message, errorCode)`. Phải kiểm tra code thật trước khi dùng.

### 6.1. Không fallback âm thầm cho folder mới

Với 4 folder Gallery mới, nếu thiếu config thì nên báo lỗi rõ:

```text
GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED
```

Không nên âm thầm đẩy ảnh khu vực/vị trí/khách tham quan vào `GalleryFolderId` chung, vì như vậy sẽ không đạt yêu cầu phân folder.

Có thể chỉ fallback legacy `GalleryImage/GalleryVideo` cũ về `GalleryItemFolderId` hoặc `GalleryFolderId` để tránh crash nếu còn code cũ chưa sửa hết.

---

## 7. Sửa FileValidationPolicy

Nếu thêm purpose mới, phải thêm rule validate tương ứng.

Ảnh đại diện khu vực/vị trí:

```text
GalleryAreaCover:
- JPG/JPEG/PNG/WEBP
- max 5MB hoặc theo rule GalleryImage hiện tại
- RequireImageMagicBytes = true
- Chặn SVG

GalleryLocationCover:
- JPG/JPEG/PNG/WEBP
- max 5MB hoặc theo rule GalleryImage hiện tại
- RequireImageMagicBytes = true
- Chặn SVG
```

Ảnh/video item:

```text
GalleryItemImage:
- Dùng rule tương đương GalleryImage hiện tại.

GalleryItemVideo:
- Dùng rule tương đương GalleryVideo hiện tại.

GalleryDelegationImage:
- Dùng rule tương đương GalleryImage hiện tại.

GalleryDelegationVideo:
- Dùng rule tương đương GalleryVideo hiện tại.
```

Không làm yếu validation hiện tại.

---

## 8. Sửa handler upload khu vực

Tìm handler tạo/sửa khu vực/vị trí hiện tại, các nơi upload `areaCoverImage`.

Khi upload ảnh đại diện khu vực, phải truyền:

```csharp
FilePurpose.GalleryAreaCover
```

Ví dụ:

```csharp
var uploadedAreaCover = await _fileUploadService.UploadBusinessFileAsync(
    areaCoverStream,
    areaCoverFileName,
    areaCoverContentType,
    areaCoverSize,
    FilePurpose.GalleryAreaCover,
    currentUserId,
    cancellationToken);

area.CoverFileId = uploadedAreaCover.FileId;
```

Kết quả mong muốn:

```text
File thật nằm trong Google Drive folder: GalleryAreaFolderId
files.file_purpose = GALLERY_AREA_COVER
gallery_areas.cover_file_id = uploadedAreaCover.FileId
Frontend vẫn dùng /api/files/{fileId}/content
```

Không đổi form field frontend nếu hiện tại đã upload ổn.

---

## 9. Sửa handler upload vị trí

Tìm handler tạo/sửa vị trí hiện tại, các nơi upload `locationCoverImage`.

Khi upload ảnh đại diện vị trí, phải truyền:

```csharp
FilePurpose.GalleryLocationCover
```

Ví dụ:

```csharp
var uploadedLocationCover = await _fileUploadService.UploadBusinessFileAsync(
    locationCoverStream,
    locationCoverFileName,
    locationCoverContentType,
    locationCoverSize,
    FilePurpose.GalleryLocationCover,
    currentUserId,
    cancellationToken);

location.CoverFileId = uploadedLocationCover.FileId;
```

Kết quả mong muốn:

```text
File thật nằm trong Google Drive folder: GalleryLocationFolderId
files.file_purpose = GALLERY_LOCATION_COVER
gallery_locations.cover_file_id = uploadedLocationCover.FileId
Frontend vẫn dùng /api/files/{fileId}/content
```

Khi edit mà không upload ảnh mới thì giữ nguyên `cover_file_id` cũ.

---

## 10. Sửa handler upload Gallery item loại MEDIA

Tìm handler tạo/sửa gallery item hiện tại.

Nếu `itemType == MEDIA`, file phải đi vào `GalleryItemFolderId`.

Cách chọn purpose:

```csharp
private static FilePurpose ResolveMediaItemPurpose(string contentType)
{
    var isVideo = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    return isVideo
        ? FilePurpose.GalleryItemVideo
        : FilePurpose.GalleryItemImage;
}
```

Khi upload:

```csharp
var purpose = ResolveMediaItemPurpose(file.ContentType ?? string.Empty);

var uploaded = await _fileUploadService.UploadBusinessFileAsync(
    stream,
    file.FileName,
    file.ContentType ?? string.Empty,
    file.Length,
    purpose,
    currentUserId,
    cancellationToken);
```

Kết quả mong muốn:

```text
File thật nằm trong Google Drive folder: GalleryItemFolderId
files.file_purpose = GALLERY_ITEM_IMAGE hoặc GALLERY_ITEM_VIDEO
gallery_items.item_type = MEDIA
gallery_item_media.file_id = uploaded.FileId
```

---

## 11. Sửa handler upload Gallery item loại VISIT_DELEGATION / khách tham quan

Nếu `itemType == VISIT_DELEGATION`, file phải đi vào `GalleryDelegationFolderId`.

Cách chọn purpose:

```csharp
private static FilePurpose ResolveDelegationItemPurpose(string contentType)
{
    var isVideo = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    return isVideo
        ? FilePurpose.GalleryDelegationVideo
        : FilePurpose.GalleryDelegationImage;
}
```

Hoặc dùng chung một hàm:

```csharp
private static FilePurpose ResolveGalleryUploadPurpose(
    string itemType,
    string contentType)
{
    var isVideo = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    return itemType switch
    {
        "MEDIA" => isVideo
            ? FilePurpose.GalleryItemVideo
            : FilePurpose.GalleryItemImage,

        "VISIT_DELEGATION" => isVideo
            ? FilePurpose.GalleryDelegationVideo
            : FilePurpose.GalleryDelegationImage,

        _ => throw new BusinessRuleException(
            "Loại nội dung Gallery không hợp lệ.",
            "GALLERY_ITEM_TYPE_INVALID")
    };
}
```

Kết quả mong muốn:

```text
File thật nằm trong Google Drive folder: GalleryDelegationFolderId
files.file_purpose = GALLERY_DELEGATION_IMAGE hoặc GALLERY_DELEGATION_VIDEO
gallery_items.item_type = VISIT_DELEGATION
gallery_item_media.file_id = uploaded.FileId
```

Nếu hệ thống hiện tại chỉ cho ảnh khách tham quan, không cho video, thì vẫn có thể chỉ dùng `GalleryDelegationImage`. Nếu UI/backend đã cho video Gallery item thì nên hỗ trợ cả `GalleryDelegationVideo`.

---

## 12. Sửa legacy call còn lại

Sau khi sửa các handler chính, chạy tìm kiếm:

```bash
grep -R "FilePurpose.GalleryImage" -n backend/PEMS.Application
grep -R "FilePurpose.GalleryVideo" -n backend/PEMS.Application
```

Đánh giá từng kết quả:

```text
1. Nếu là upload ảnh/video Gallery item MEDIA cũ → đổi sang GalleryItemImage/GalleryItemVideo.
2. Nếu là upload khách tham quan/đoàn khách → đổi sang GalleryDelegationImage/GalleryDelegationVideo.
3. Nếu là upload ảnh khu vực → đổi sang GalleryAreaCover.
4. Nếu là upload ảnh vị trí → đổi sang GalleryLocationCover.
5. Nếu không chắc → đọc handler, DTO, bảng nghiệp vụ đang lưu file_id để xác định.
```

Không để ảnh khu vực/vị trí/khách tham quan tiếp tục dùng `GalleryImage` chung.

---

## 13. Có cần sửa database không?

### 13.1. Không cần sửa bảng nghiệp vụ nếu upload/frontend đã ổn

Nếu hiện tại các field sau đã tồn tại và đang dùng được, không sửa database nghiệp vụ:

```text
gallery_areas.cover_file_id
gallery_locations.cover_file_id
gallery_items.item_type
gallery_item_media.file_id
```

### 13.2. Chỉ kiểm tra `files.file_purpose`

Cần kiểm tra cột `files.file_purpose` trong source thật.

Nếu là:

```sql
file_purpose VARCHAR(100) NULL
```

thì không cần SQL patch khi thêm DB value mới.

Nếu là ENUM/CHECK constraint thì tạo patch thêm các value:

```text
GALLERY_AREA_COVER
GALLERY_LOCATION_COVER
GALLERY_ITEM_IMAGE
GALLERY_ITEM_VIDEO
GALLERY_DELEGATION_IMAGE
GALLERY_DELEGATION_VIDEO
```

---

## 14. Kiểm thử bắt buộc

Sau khi sửa, test đúng 4 nhóm upload.

### 14.1. Test ảnh khu vực

```text
Action:
- Tạo khu vực mới hoặc thay ảnh khu vực.

Expected:
- File xuất hiện trong Google Drive folder GalleryAreaFolderId.
- Không xuất hiện trong GalleryFolderId chung nếu folder con đã config đúng.
- files.file_purpose = GALLERY_AREA_COVER.
- gallery_areas.cover_file_id trỏ đúng file_id.
- UI vẫn hiển thị ảnh qua /api/files/{fileId}/content.
```

### 14.2. Test ảnh vị trí

```text
Action:
- Tạo vị trí mới hoặc thay ảnh vị trí.

Expected:
- File xuất hiện trong Google Drive folder GalleryLocationFolderId.
- files.file_purpose = GALLERY_LOCATION_COVER.
- gallery_locations.cover_file_id trỏ đúng file_id.
- UI vẫn hiển thị ảnh qua /api/files/{fileId}/content.
```

### 14.3. Test Gallery item MEDIA

```text
Action:
- Tạo gallery item với itemType = MEDIA.
- Upload ảnh/video.

Expected:
- File xuất hiện trong Google Drive folder GalleryItemFolderId.
- files.file_purpose = GALLERY_ITEM_IMAGE hoặc GALLERY_ITEM_VIDEO.
- gallery_items.item_type = MEDIA.
- gallery_item_media.file_id trỏ đúng file_id.
- is_primary logic giữ nguyên.
- media_kind logic giữ nguyên.
```

### 14.4. Test Gallery item VISIT_DELEGATION / khách tham quan

```text
Action:
- Tạo gallery item với itemType = VISIT_DELEGATION.
- Upload ảnh/video khách tham quan.

Expected:
- File xuất hiện trong Google Drive folder GalleryDelegationFolderId.
- files.file_purpose = GALLERY_DELEGATION_IMAGE hoặc GALLERY_DELEGATION_VIDEO.
- gallery_items.item_type = VISIT_DELEGATION.
- gallery_item_media.file_id trỏ đúng file_id.
- is_primary logic giữ nguyên.
- media_kind logic giữ nguyên.
```

### 14.5. Test lỗi thiếu config

Tạm thời để trống một folder id mới, ví dụ `GalleryAreaFolderId`, rồi upload ảnh khu vực.

Expected:

```text
- Backend trả lỗi rõ GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED.
- Không insert dữ liệu nghiệp vụ sai.
- Không làm frontend crash.
```

Sau test phải restore config thật.

---

## 15. Checklist build và rà soát cuối

Trước khi báo hoàn thành:

```text
1. Build backend xanh.
2. Không còn compile error do enum FilePurpose mới.
3. Không còn handler Gallery upload dùng sai purpose chung nếu đã xác định được ngữ cảnh.
4. GoogleDriveOptions bind được các folder id mới.
5. GoogleDriveFolderResolver map đúng 4 nhóm folder.
6. FileValidationPolicy có rule cho purpose mới.
7. ToDbValue và ToObjectKeyPrefix có đủ nhánh mới.
8. Không sửa frontend ngoài config/example nếu không bắt buộc.
9. Không thay đổi API response/request nếu upload hiện tại đã ổn.
10. Không commit secret trong appsettings.Development.json.
```

---

## 16. Báo cáo sau khi code xong

Khi hoàn thành, báo cáo ngắn gọn theo form:

```text
Đã sửa:
1. GoogleDriveOptions: ...
2. FilePurpose/FilePurposeDbValues/ObjectKeyPrefix: ...
3. FileValidationPolicy: ...
4. GoogleDriveFolderResolver: ...
5. Handler upload area cover: ...
6. Handler upload location cover: ...
7. Handler upload gallery item MEDIA: ...
8. Handler upload gallery item VISIT_DELEGATION: ...

Kết quả test:
- Area cover → folder ... OK
- Location cover → folder ... OK
- MEDIA item → folder ... OK
- VISIT_DELEGATION item → folder ... OK
- Build backend: OK
```

Nếu có phần chưa làm được, phải nói rõ lý do và file liên quan.
