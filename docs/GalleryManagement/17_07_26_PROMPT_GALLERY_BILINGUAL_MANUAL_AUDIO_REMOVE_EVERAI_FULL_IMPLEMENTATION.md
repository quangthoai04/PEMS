# PEMS Gallery — Thay EverAI TTS bằng Audio Song Ngữ Upload Thủ Công

## 0. Mục đích tài liệu

Tài liệu này là đặc tả triển khai đầy đủ dành cho AI Agent/code agent.

Nhiệm vụ là thay toàn bộ cơ chế EverAI TTS hiện tại của Gallery bằng cơ chế STAFF LEADER tự upload hai bản ghi âm tương ứng với hai nội dung tiếng Việt và tiếng Anh.

Phạm vi bao gồm:

- Database.
- Domain entities.
- EF Core configuration.
- Application commands/queries.
- API contracts.
- Google Drive upload.
- STAFF LEADER Gallery Management UI.
- Public VisitFPTU Gallery UI.
- Seed data.
- Audit.
- Error handling.
- Tests.
- Xóa hoàn toàn EverAI TTS khỏi production code.

Không triển khai kiểu vá tạm thời chỉ thêm vài field vào form. Phải hoàn thành end-to-end và loại bỏ toàn bộ cơ chế TTS cũ.

---

# 1. Baseline code hiện tại

Repository:

```text
quangthoai04/PEMS
```

Baseline đã được rà soát:

```text
Branch: Dev
Commit tham chiếu khi đặc tả được tạo: d736bd12ded928d4fd2ab86467288753cfba8f9a
```

Các phần Gallery Management hiện có:

- STAFF LEADER chỉ quản lý Gallery trong `primary_campus_id` lấy từ JWT.
- Tạo/sửa Gallery Item.
- Gallery Item có nhiều media.
- Ảnh upload từ máy.
- Video YouTube lưu dạng external metadata.
- `MEDIA` và `VISIT_DELEGATION`.
- Primary media.
- Quản lý Area/Location.
- Area cover MP4.
- Location cover image.
- Google Drive folder routing.
- Public Gallery.
- EverAI TTS tự động tạo audio từ `gallery_items.description`.

Các file quan trọng hiện tại có thể bao gồm:

```text
backend/PEMS.Domain/Entities/Galleries/GalleryItem.cs
backend/PEMS.Domain/Entities/Galleries/GalleryItemTtsAudio.cs

backend/PEMS.Application/Galleries/
backend/PEMS.Application/Galleries/Tts/

backend/PEMS.Api/Controllers/GalleriesController.cs
backend/PEMS.Api/Controllers/GalleryManagementTtsController.cs

frontend/pems-react/src/pages/dashboard/gallery/
  GalleryManagementStaffLeader.tsx
  GalleryUpsertModal.tsx
  GalleryDetailModal.tsx

frontend/pems-react/src/features/gallery-management/
  api/galleryManagementApi.ts
  types/galleryManagement.types.ts

frontend/pems-react/src/pages/CampusDetailVisitPage.tsx
frontend/pems-react/src/features/visit-fptu/
  publicVisitFptuApi.ts
  publicVisitFptu.types.ts
```

AI Agent phải search toàn repository theo các từ khóa sau trước khi code:

```text
EverAi
EverAI
Tts
TTS
Narration
EnsureAudio
Regenerate
SourceTextHash
GalleryItemTts
gallery_item_tts_audios
description
GalleryAudio
```

Không được giả định chỉ những file liệt kê ở trên bị ảnh hưởng.

---

# 2. Yêu cầu nghiệp vụ cuối cùng — không được thay đổi

## 2.1. Điều kiện hợp lệ của một Gallery Item

Mỗi Gallery Item bắt buộc phải có đầy đủ:

```text
1. Mô tả tiếng Việt
2. Audio tiếng Việt
3. Mô tả tiếng Anh
4. Audio tiếng Anh
5. Ít nhất một Gallery media
```

Gallery media vẫn theo rule hiện tại:

- Ảnh upload từ máy; hoặc
- Video YouTube; hoặc
- Kết hợp nhiều media.

## 2.2. Bốn trường song ngữ đều bắt buộc khi tạo

Create Gallery Item chỉ thành công khi có đủ:

```text
descriptionVi
audioVi
descriptionEn
audioEn
```

Thiếu bất kỳ một trường nào phải từ chối request.

Ma trận:

| Description VI | Audio VI | Description EN | Audio EN | Kết quả |
|---|---|---|---|---|
| Có | Có | Có | Có | Thành công |
| Thiếu | Có | Có | Có | Từ chối |
| Có | Thiếu | Có | Có | Từ chối |
| Có | Có | Thiếu | Có | Từ chối |
| Có | Có | Có | Thiếu | Từ chối |

Description chỉ chứa khoảng trắng được xem là rỗng.

## 2.3. Rule khi chỉnh sửa

Sau mọi thao tác edit, Gallery Item vẫn phải có đủ:

```text
descriptionVi
audioVi hiện tại hoặc audioVi mới
descriptionEn
audioEn hiện tại hoặc audioEn mới
```

Cho phép:

- Sửa description VI.
- Sửa description EN.
- Giữ audio VI hiện tại.
- Thay audio VI bằng file mới.
- Giữ audio EN hiện tại.
- Thay audio EN bằng file mới.

Không cho phép:

- Xóa audio VI.
- Xóa audio EN.
- Xóa bản tiếng Anh.
- Để description VI rỗng.
- Để description EN rỗng.

## 2.4. Public Gallery

Public Gallery mặc định hiển thị bản tiếng Việt.

Người dùng có thể chuyển giữa:

```text
VI
EN
```

Khi đổi ngôn ngữ:

- Description phải đổi theo ngôn ngữ.
- Audio phải đổi theo ngôn ngữ.
- Audio đang phát phải dừng ngay.
- Không tự động phát audio ngôn ngữ mới.
- Người dùng phải bấm icon loa để phát.

Do hai bản đều bắt buộc:

- Nút chuyển ngôn ngữ luôn khả dụng.
- Icon loa luôn có audio ở cả VI và EN.
- Không fallback audio Việt khi đang chọn English.

---

# 3. Xóa hoàn toàn EverAI TTS

## 3.1. Không giữ dữ liệu cũ

Dữ liệu EverAI hiện tại chỉ là seed data.

Không cần:

- Migrate audio EverAI cũ.
- Giữ lịch sử job.
- Giữ các row `READY`.
- Giữ request ID.
- Giữ callback data.
- Giữ source hash.

## 3.2. Các chức năng phải bị xóa

Xóa toàn bộ:

```text
EverAI API client
EverAI API key/config
EverAI callback
TTS background worker
TTS queue
TTS hash service
TTS status service
Auto-generate audio
Lazy-generate audio
Manual regenerate
Public ensure endpoint
Public status polling
Management TTS status
Management regenerate button
```

Không được để production code còn phụ thuộc EverAI.

## 3.3. Trạng thái TTS phải biến mất

Không còn các trạng thái:

```text
READY
PROCESSING
FAILED
STALE
NOT_CREATED
DISABLED
INVALID_DESCRIPTION
TEMPORARILY_UNAVAILABLE
UP_TO_DATE
```

Không còn:

```text
audioStatus
canRegenerate
voiceCode
sourceTextHash
triggerSource
progress
errorMessage của TTS
```

---

# 4. Thiết kế database được chọn

## 4.1. Không dùng một bảng localization nhiều dòng

Vì nghiệp vụ cố định đúng hai ngôn ngữ và cả hai đều bắt buộc, thiết kế được chọn là:

```text
gallery_items 1 ─── 1 gallery_item_contents
```

Mỗi Gallery Item có đúng một row nội dung song ngữ.

## 4.2. Tạo bảng `gallery_item_contents`

Schema đề xuất:

```sql
CREATE TABLE gallery_item_contents (
    gallery_item_id BIGINT UNSIGNED NOT NULL,

    description_vi TEXT NOT NULL
        COMMENT 'Mô tả tiếng Việt, bắt buộc',

    audio_vi_file_id BIGINT UNSIGNED NOT NULL
        COMMENT 'files.file_id của bản ghi âm tiếng Việt, bắt buộc',

    description_en TEXT NOT NULL
        COMMENT 'Mô tả tiếng Anh, bắt buộc',

    audio_en_file_id BIGINT UNSIGNED NOT NULL
        COMMENT 'files.file_id của bản ghi âm tiếng Anh, bắt buộc',

    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by BIGINT UNSIGNED NULL,

    updated_at DATETIME NULL,
    updated_by BIGINT UNSIGNED NULL,

    PRIMARY KEY (gallery_item_id),

    KEY idx_gallery_item_contents_audio_vi (
        audio_vi_file_id
    ),

    KEY idx_gallery_item_contents_audio_en (
        audio_en_file_id
    ),

    FULLTEXT KEY ft_gallery_item_contents_descriptions (
        description_vi,
        description_en
    ),

    CONSTRAINT chk_gallery_item_description_vi_not_blank
        CHECK (CHAR_LENGTH(TRIM(description_vi)) > 0),

    CONSTRAINT chk_gallery_item_description_en_not_blank
        CHECK (CHAR_LENGTH(TRIM(description_en)) > 0),

    CONSTRAINT fk_gallery_item_contents_item
        FOREIGN KEY (gallery_item_id)
        REFERENCES gallery_items(gallery_item_id)
        ON UPDATE RESTRICT
        ON DELETE CASCADE,

    CONSTRAINT fk_gallery_item_contents_audio_vi
        FOREIGN KEY (audio_vi_file_id)
        REFERENCES files(file_id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_gallery_item_contents_audio_en
        FOREIGN KEY (audio_en_file_id)
        REFERENCES files(file_id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_gallery_item_contents_created_by
        FOREIGN KEY (created_by)
        REFERENCES users(user_id)
        ON UPDATE CASCADE
        ON DELETE SET NULL,

    CONSTRAINT fk_gallery_item_contents_updated_by
        FOREIGN KEY (updated_by)
        REFERENCES users(user_id)
        ON UPDATE CASCADE
        ON DELETE SET NULL
) ENGINE=InnoDB
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_unicode_ci
COMMENT='Nội dung mô tả và bản ghi âm song ngữ của Gallery Item';
```

## 4.3. Lý do dùng PK là `gallery_item_id`

```text
PRIMARY KEY (gallery_item_id)
```

Bảo đảm:

- Quan hệ 1:1 thật sự.
- Một item chỉ có một bộ nội dung song ngữ.
- Không cần `content_id`.
- Query đơn giản.
- Không thể tạo hai content row cho một item.

## 4.4. Xóa `gallery_items.description`

Trạng thái cuối cùng:

```text
gallery_items.description
→ bị xóa
```

`gallery_items` chỉ giữ metadata:

```text
gallery_item_id
location_id
title
item_type
media_kind
status
display_order
created_at/by
updated_at/by
deleted_at/by
```

Nội dung chuyển sang:

```text
gallery_item_contents.description_vi
gallery_item_contents.description_en
```

## 4.5. Migration theo hai giai đoạn

### Migration A — Additive

- Tạo `gallery_item_contents`.
- Thêm FK/index/check.
- Chưa drop `gallery_items.description`.
- Chưa drop bảng TTS.
- Cập nhật application đọc/ghi schema mới.

### Migration B — Cleanup

Sau khi code mới đã hoạt động:

```sql
DROP TABLE IF EXISTS gallery_item_tts_audios;
```

Sau khi không còn consumer dùng cột cũ:

```sql
ALTER TABLE gallery_items
DROP COLUMN description;
```

Nếu FULLTEXT index hiện tại chứa `description`, phải drop/recreate phù hợp.

## 4.6. Migration phải idempotent theo convention dự án

Nếu package SQL của dự án đang dùng information_schema guard, migration mới phải tuân thủ cùng convention:

- Check table tồn tại.
- Check column tồn tại.
- Check index tồn tại.
- Check FK tồn tại.
- Có verify script.
- Có import order rõ ràng.
- Không drop dữ liệu ngoài phạm vi Gallery seed.

---

# 5. Xử lý dữ liệu seed hiện tại

## 5.1. Không migrate EverAI seed audio

Không copy dữ liệu từ:

```text
gallery_item_tts_audios
```

sang bảng mới.

## 5.2. Gallery Item seed cũ

Gallery Item cũ chưa có đủ bốn trường mới sẽ không còn hợp lệ.

Phương án sạch:

- Giữ Campus.
- Giữ Gallery Area.
- Giữ Gallery Location.
- Giữ Area cover.
- Giữ Location cover.
- Xóa/reseed Gallery Item và media seed liên quan nếu cần.

## 5.3. Thứ tự xóa theo FK

Cần kiểm tra schema thực tế trước khi chạy, nhưng logic tổng quát:

```text
1. gallery_item_media của seed Gallery
2. gallery_item_contents nếu đã tồn tại
3. gallery_item_tts_audios
4. gallery_items seed
5. files seed không còn được tham chiếu
```

Không xóa file dùng chung hoặc file thật ngoài seed.

## 5.4. Seed audio không thể chỉ làm bằng SQL giả

`audio_vi_file_id` và `audio_en_file_id` phải trỏ tới file thật.

Không được:

- Tạo row `files` giả không có object trên Drive.
- Tạo URL giả.
- Dùng cùng một file ID cho dữ liệu không hợp lệ nếu nghiệp vụ yêu cầu file riêng.

Hai cách hợp lệ:

### Cách A — Pre-upload

1. Upload audio mẫu lên Google Drive.
2. Tạo row `files`.
3. Seed content bằng file ID thật.

### Cách B — Seed utility qua backend

Dùng:

```text
IFileUploadService
FilePurpose.GalleryAudio
```

để upload file seed, sau đó insert dữ liệu.

---

# 6. Domain entity và EF Core

## 6.1. Thêm entity `GalleryItemContent`

Đề xuất:

```csharp
[Table("gallery_item_contents")]
public sealed class GalleryItemContent
{
    [Key]
    [Column("gallery_item_id")]
    public ulong GalleryItemId { get; set; }

    [Column("description_vi")]
    public string DescriptionVi { get; set; } = null!;

    [Column("audio_vi_file_id")]
    public ulong AudioViFileId { get; set; }

    [Column("description_en")]
    public string DescriptionEn { get; set; } = null!;

    [Column("audio_en_file_id")]
    public ulong AudioEnFileId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public ulong? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public ulong? UpdatedBy { get; set; }

    public GalleryItem GalleryItem { get; set; } = null!;
    public UploadedFile AudioViFile { get; set; } = null!;
    public UploadedFile AudioEnFile { get; set; } = null!;
}
```

## 6.2. Cập nhật `GalleryItem`

Cuối cùng xóa:

```csharp
public string Description { get; set; }
```

Thêm:

```csharp
public GalleryItemContent Content { get; set; } = null!;
```

## 6.3. DbContext interface và implementation

Thêm:

```csharp
DbSet<GalleryItemContent> GalleryItemContents { get; }
```

Cập nhật:

- `IApplicationDbContext`.
- `ApplicationDbContext`.
- Test DbContext/mock nếu có.
- Entity scanning/configuration registration.

## 6.4. EF configuration

```csharp
builder.HasKey(x => x.GalleryItemId);

builder.HasOne(x => x.GalleryItem)
    .WithOne(x => x.Content)
    .HasForeignKey<GalleryItemContent>(x => x.GalleryItemId)
    .OnDelete(DeleteBehavior.Cascade);

builder.HasOne(x => x.AudioViFile)
    .WithMany()
    .HasForeignKey(x => x.AudioViFileId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasOne(x => x.AudioEnFile)
    .WithMany()
    .HasForeignKey(x => x.AudioEnFileId)
    .OnDelete(DeleteBehavior.Restrict);
```

Nếu project dùng explicit max length/collation/index config trong Fluent API, phải đồng bộ với SQL.

## 6.5. Xóa entity TTS

Xóa:

```text
GalleryItemTtsAudio.cs
```

Xóa:

- DbSet tương ứng.
- EF mapping.
- Navigation.
- Query references.
- Test fixtures.
- Seed references.

---

# 7. Google Drive và file upload

## 7.1. Giữ folder hiện tại

Cả audio VI và EN tiếp tục dùng:

```text
FilePurpose.GalleryAudio
files.file_purpose = GALLERY_AUDIO
GoogleDriveOptions.GalleryAudioFolderId
```

Không cần tạo folder mới.

Không cần tạo:

```text
GALLERY_AUDIO_VI
GALLERY_AUDIO_EN
```

Ngôn ngữ được xác định bởi FK trong `gallery_item_contents`.

## 7.2. Upload qua foundation chung

Bắt buộc dùng:

```csharp
IFileUploadService.UploadBusinessFileAsync(...)
```

với:

```csharp
FilePurpose.GalleryAudio
```

Không gọi trực tiếp Google Drive SDK từ Gallery handler.

## 7.3. Validation audio

Giữ policy hiện tại nếu đang hỗ trợ:

```text
.mp3
.wav
audio/mpeg
audio/mp3
audio/wav
audio/x-wav
Tối đa 20 MB
```

Hai file được validate độc lập.

Backend phải kiểm tra:

- File không null.
- File size > 0.
- File size không vượt giới hạn.
- Extension hợp lệ.
- MIME hợp lệ.
- Magic-byte/signature nếu upload foundation có hỗ trợ.
- File purpose đúng.

## 7.4. Tên field quyết định ngôn ngữ

```text
audioVi → audio_vi_file_id
audioEn → audio_en_file_id
```

Không suy luận ngôn ngữ từ filename.

## 7.5. Object key

Có thể giữ object key builder hiện tại.

Nếu mở rộng được mà không phá convention, filename/object key có thể mang hậu tố:

```text
-vi
-en
```

Nhưng đây không phải nguồn sự thật; DB FK mới là nguồn sự thật.

---

# 8. API create Gallery Item

## 8.1. Multipart contract mới

Request:

```text
title
locationId
itemType
status

descriptionVi
audioVi

descriptionEn
audioEn

files[]
youtubeUrls[]
primaryMediaKey
```

Không dùng `description` cũ trong contract cuối cùng.

## 8.2. DTO/command

Command phải có:

```csharp
string Title
string DescriptionVi
GalleryUploadFileCommandDto AudioVi
string DescriptionEn
GalleryUploadFileCommandDto AudioEn
ulong LocationId
string ItemType
string Status
IReadOnlyList<GalleryUploadFileCommandDto> Files
IReadOnlyList<string> YoutubeUrls
string? PrimaryMediaKey
```

Tên class phải theo convention hiện tại của project.

## 8.3. Controller binding

Controller phải đọc:

```text
IFormFile audioVi
IFormFile audioEn
```

và convert sang DTO file giống foundation hiện tại.

Không buffer lặp không cần thiết.

Nếu controller hiện đang copy mọi file vào RAM, không mở rộng limit vô lý; giữ giới hạn audio nhỏ và cân nhắc streaming theo convention dự án.

## 8.4. Validation create

Bắt buộc:

```text
descriptionVi.Trim() != ""
descriptionEn.Trim() != ""
audioVi != null
audioEn != null
ít nhất 1 media sau khi tính files + youtubeUrls
```

Error code ổn định đề xuất:

```text
GALLERY_DESCRIPTION_VI_REQUIRED
GALLERY_AUDIO_VI_REQUIRED
GALLERY_DESCRIPTION_EN_REQUIRED
GALLERY_AUDIO_EN_REQUIRED

GALLERY_AUDIO_VI_INVALID
GALLERY_AUDIO_EN_INVALID
GALLERY_AUDIO_TOO_LARGE
GALLERY_DESCRIPTION_TOO_LONG
```

Message tiếng Việt có thể là:

```text
Vui lòng nhập mô tả tiếng Việt.
Vui lòng chọn bản ghi âm tiếng Việt.
Vui lòng nhập mô tả tiếng Anh.
Vui lòng chọn bản ghi âm tiếng Anh.
```

## 8.5. Giới hạn description

Giới hạn 1000 ký tự hiện có nguồn gốc từ EverAI.

Phải quyết định rõ:

- Nếu giữ 1000: ghi nhận đây là business rule của Gallery, không phải TTS cap.
- Nếu tăng: cập nhật cả VI và EN, backend và frontend đồng nhất.

Không để comment/config còn ghi “EverAI narration cap”.

---

# 9. Transaction create

## 9.1. Scope

Giữ guard hiện tại:

```text
STAFF + LEADER
primary_campus_id từ JWT
```

Không nhận campus từ client.

Location phải:

- Tồn tại.
- Thuộc đúng campus.
- `ACTIVE`.
- Area cũng hợp lệ theo rule hiện tại.

## 9.2. Trình tự

1. Validate actor/scope.
2. Validate description VI.
3. Validate description EN.
4. Validate audio VI.
5. Validate audio EN.
6. Validate Gallery media.
7. Validate YouTube URLs.
8. Upload audio VI.
9. Upload audio EN.
10. Upload Gallery images.
11. Register YouTube metadata.
12. Begin/continue DB transaction theo convention.
13. Insert `gallery_items`.
14. Insert `gallery_item_contents`.
15. Insert `gallery_item_media`.
16. Resolve primary media.
17. Recompute `media_kind`.
18. Ghi audit.
19. Commit.
20. Trả detail DTO mới.

## 9.3. Điều kiện success

Chỉ thành công khi tồn tại:

```text
1 gallery_items row
1 gallery_item_contents row
audio_vi_file_id hợp lệ
audio_en_file_id hợp lệ
>= 1 gallery_item_media
đúng 1 primary media
```

## 9.4. Không gọi TTS sau create

Xóa mọi đoạn:

```text
EnsureAudioAsync
AUTO_GENERATE
queue TTS
post-commit TTS
```

Toast/frontend cũng không nói “audio sẽ được tạo”.

---

# 10. Compensation và orphan file

Google Drive không nằm trong transaction MySQL.

## 10.1. Rủi ro

Ví dụ:

1. Upload audio VI thành công.
2. Upload audio EN thành công.
3. DB insert thất bại.

Hai file có thể bị orphan.

## 10.2. Cách xử lý bắt buộc

Triển khai ít nhất một cơ chế compensation.

Khuyến nghị:

```text
A. Cố gắng cleanup ngay trong catch
B. Có orphan cleanup fallback
```

## 10.3. Tracking file đã upload trong request

Handler/service nên giữ danh sách:

```text
uploadedFileIds
uploadedExternalFileIds hoặc object keys
```

Nếu transaction fail:

- Gọi cleanup service.
- Log lỗi cleanup nhưng không che lỗi gốc.
- Không xóa file trước khi chắc chắn không có DB reference.

## 10.4. Không xóa file audio cũ trước commit khi edit

Flow edit:

```text
Upload file mới
→ Update FK
→ Commit
→ Cleanup file cũ sau commit
```

---

# 11. API update Gallery Item

## 11.1. Request

```text
galleryItemId
title
locationId
itemType

descriptionVi
descriptionEn

newAudioVi optional
newAudioEn optional

keepMediaIds[]
newFiles[]
youtubeUrls[]
primaryMediaKey
```

Không có:

```text
removeAudioVi
removeAudioEn
removeEnglishContent
```

## 11.2. Rule edit

```text
newAudioVi không gửi
→ giữ audio_vi_file_id hiện tại

newAudioVi có
→ upload và thay audio_vi_file_id

newAudioEn không gửi
→ giữ audio_en_file_id hiện tại

newAudioEn có
→ upload và thay audio_en_file_id
```

Description VI và EN luôn bắt buộc.

## 11.3. Dữ liệu cũ thiếu audio

Nếu item cũ còn tồn tại trong thời gian chuyển đổi và thiếu content/audio:

- Không cho save edit nếu chưa bổ sung đủ.
- Form edit phải yêu cầu chọn file thiếu.
- Sau cleanup seed, trạng thái này không được tồn tại.

## 11.4. Audit edit

Ghi:

```text
descriptionViChanged
descriptionEnChanged
oldAudioViFileId
newAudioViFileId
oldAudioEnFileId
newAudioEnFileId
```

Không lưu binary.

---

# 12. DTO quản lý Gallery

## 12.1. Detail DTO

Đề xuất:

```json
{
  "galleryItemId": 100,
  "title": "Thư viện Alpha",
  "itemType": "MEDIA",
  "mediaKind": "MIXED",
  "status": "PUBLISHED",

  "content": {
    "descriptionVi": "Nội dung tiếng Việt...",
    "audioVi": {
      "fileId": 501,
      "fileName": "alpha-library-vi.mp3",
      "mimeType": "audio/mpeg",
      "fileSize": 2048000,
      "url": "/api/files/501/content"
    },

    "descriptionEn": "English content...",
    "audioEn": {
      "fileId": 502,
      "fileName": "alpha-library-en.mp3",
      "mimeType": "audio/mpeg",
      "fileSize": 1980000,
      "url": "/api/files/502/content"
    }
  },

  "area": {},
  "location": {},
  "campus": {},
  "media": []
}
```

## 12.2. List DTO

Không còn `AudioStatus`.

List có thể giữ gọn:

```text
galleryItemId
area/location
title
itemType
mediaKind
status
createdAt
createdBy
primaryMedia
```

Không cần thêm badge “đủ audio” vì DB bắt buộc.

## 12.3. Xóa DTO TTS

Xóa mọi DTO/type:

```text
GalleryItemTtsManagementStatus
GalleryItemTtsStatus
GalleryItemTtsRegenerateResult
GalleryItemTtsAudioResponse
PublicTtsAudio
```

---

# 13. Search/filter backend

## 13.1. Search

Chuyển từ:

```text
gallery_items.description
```

sang:

```text
gallery_item_contents.description_vi
gallery_item_contents.description_en
```

Search bao gồm:

```text
title
description_vi
description_en
area_name
location_name
```

Ví dụ:

```csharp
query = query.Where(i =>
    i.Title.Contains(keyword) ||
    i.Content.DescriptionVi.Contains(keyword) ||
    i.Content.DescriptionEn.Contains(keyword) ||
    i.Location.LocationName.Contains(keyword) ||
    i.Location.Area.AreaName.Contains(keyword));
```

Tối ưu query để tránh N+1.

## 13.2. Xóa audio status filter

Xóa:

```text
audioStatus query parameter
audio status filter
batch TTS status query
audio status column
```

---

# 14. STAFF LEADER UI — danh sách

File tham chiếu:

```text
GalleryManagementStaffLeader.tsx
```

## 14.1. Xóa cột Audio

Xóa:

```text
AUDIO column
AudioStatusBadge
READY/PROCESSING/STALE/FAILED...
```

Cập nhật `colSpan` cho loading/error/empty rows.

## 14.2. Xóa filter TTS

Xóa dropdown:

```text
Tất cả giọng đọc
Sẵn sàng
Đang tạo
Cần tạo lại
Lỗi
Chưa tạo
```

Xóa state:

```text
audioStatus
```

Xóa query param tương ứng.

## 14.3. Toast

Thay:

```text
Gallery item đã lưu. Giọng đọc sẽ được tạo tự động.
```

bằng:

```text
Đã tạo Gallery Item với đầy đủ nội dung song ngữ.
```

hoặc:

```text
Đã cập nhật Gallery Item.
```

---

# 15. STAFF LEADER UI — Create/Edit modal

File tham chiếu:

```text
GalleryUpsertModal.tsx
```

## 15.1. State mới

```typescript
const [descriptionVi, setDescriptionVi] = useState('');
const [descriptionEn, setDescriptionEn] = useState('');

const [audioVi, setAudioVi] = useState<File | null>(null);
const [audioEn, setAudioEn] = useState<File | null>(null);
```

Edit mode cần thêm metadata audio hiện tại.

## 15.2. Giao diện đề xuất

Có thể dùng tabs:

```text
[Tiếng Việt] [English]
```

### Tab Tiếng Việt

```text
MÔ TẢ TIẾNG VIỆT *
[textarea]

BẢN GHI ÂM TIẾNG VIỆT *
[Chọn MP3/WAV]

[audio controls]
filename
size
Thay file
```

### Tab English

```text
ENGLISH DESCRIPTION *
[textarea]

ENGLISH AUDIO *
[Select MP3/WAV]

[audio controls]
filename
size
Replace file
```

## 15.3. Create rule frontend

Submit chỉ hợp lệ khi:

```typescript
descriptionVi.trim().length > 0 &&
audioVi !== null &&
descriptionEn.trim().length > 0 &&
audioEn !== null &&
totalMedia > 0
```

Vẫn phải validate lại trong submit handler, không chỉ disable button.

## 15.4. Edit rule frontend

Edit hợp lệ khi:

```text
descriptionVi có nội dung
descriptionEn có nội dung
audio VI hiện tại hoặc file mới
audio EN hiện tại hoặc file mới
>= 1 media sau reconcile
```

## 15.5. Audio preview

Dùng:

```typescript
URL.createObjectURL(file)
```

Render:

```html
<audio controls />
```

Bắt buộc revoke:

```typescript
URL.revokeObjectURL(url)
```

khi:

- Đổi file.
- Xóa file mới trước submit.
- Đóng modal.
- Component unmount.

## 15.6. Không có nút xóa audio hiện tại

Chỉ có:

```text
Nghe thử
Giữ nguyên
Thay file
```

## 15.7. FormData create

```typescript
form.append('descriptionVi', input.descriptionVi);
form.append('audioVi', input.audioVi);

form.append('descriptionEn', input.descriptionEn);
form.append('audioEn', input.audioEn);
```

## 15.8. FormData edit

```typescript
form.append('descriptionVi', input.descriptionVi);
form.append('descriptionEn', input.descriptionEn);

if (input.newAudioVi) form.append('newAudioVi', input.newAudioVi);
if (input.newAudioEn) form.append('newAudioEn', input.newAudioEn);
```

---

# 16. STAFF LEADER UI — Detail modal

File tham chiếu:

```text
GalleryDetailModal.tsx
```

## 16.1. Xóa TTS logic

Xóa:

```text
getTtsAudioStatus
regenerateTtsAudio
polling mỗi 3 giây
TTS badge
Tạo lại audio
FAILED error
canRegenerate
```

## 16.2. Giao diện mới

Dùng tab/ngôn ngữ:

```text
[Tiếng Việt] [English]
```

Hiển thị:

```text
description tương ứng
audio player tương ứng
```

Khi đổi tab:

- Pause audio cũ.
- Reset player state.
- Đổi description.
- Đổi audio URL.

Footer chỉ cần:

```text
Chỉnh sửa
Đóng
```

---

# 17. Public backend DTO/API

## 17.1. Public detail DTO

Thay một `Description` bằng content song ngữ.

Đề xuất:

```json
{
  "galleryItem": {
    "galleryItemId": 100,
    "title": "Thư viện Alpha",
    "mediaKind": "MIXED",
    "status": "PUBLISHED",
    "content": {
      "vi": {
        "description": "Mô tả tiếng Việt...",
        "audioUrl": "/api/public/visit-fptu/gallery-items/100/audio/vi?v=501"
      },
      "en": {
        "description": "English description...",
        "audioUrl": "/api/public/visit-fptu/gallery-items/100/audio/en?v=502"
      }
    }
  },
  "media": []
}
```

## 17.2. Audio endpoint public

Tạo:

```http
GET /api/public/visit-fptu/gallery-items/{galleryItemId}/audio/{languageCode}
```

`languageCode` chỉ nhận:

```text
vi
en
```

## 17.3. Public visibility guard

Endpoint phải kiểm tra:

```text
Gallery Item tồn tại
Gallery Item chưa soft-delete
Gallery Item = PUBLISHED
Location = ACTIVE
Area = ACTIVE
Campus = ACTIVE
Content tồn tại
Audio file tương ứng tồn tại
files.file_purpose = GALLERY_AUDIO
```

Không hợp lệ trả 404 theo convention public Gallery.

## 17.4. Không expose file tùy ý

Không cho client truyền arbitrary `fileId` để đọc file.

Endpoint phải resolve file thông qua:

```text
galleryItemId + languageCode
```

## 17.5. Cache busting

Audio URL có thể thêm:

```text
?v={audioFileId}
```

để thay file không bị cache cũ.

---

# 18. Public frontend

File tham chiếu:

```text
CampusDetailVisitPage.tsx
```

## 18.1. Xóa EverAI public flow

Xóa:

```text
ensureTtsAudio()
getTtsAudioStatus()
pollNarration()
NARRATION_POLL_MS
NARRATION_POLL_MAX
PROCESSING logic
```

## 18.2. State ngôn ngữ

```typescript
type GalleryLanguage = 'vi' | 'en';

const [selectedLanguage, setSelectedLanguage] =
  useState<GalleryLanguage>('vi');
```

Khi đổi item:

```typescript
stopAudio();
setSelectedLanguage('vi');
```

## 18.3. Active content

```typescript
const activeContent =
  selectedLanguage === 'vi'
    ? detail.galleryItem.content.vi
    : detail.galleryItem.content.en;
```

## 18.4. Icon language

Thêm icon `Languages` cạnh icon loa.

UI có thể là:

```text
[VI | EN] [speaker]
```

Do cả hai ngôn ngữ bắt buộc, nút luôn hiển thị.

## 18.5. Phát audio trực tiếp

```typescript
const audio = new Audio(activeContent.audioUrl);
await audio.play();
```

State chỉ cần:

```text
idle
playing
error
```

Có thể giữ loading ngắn cho network load nhưng không dùng “generating”.

## 18.6. Khi đổi ngôn ngữ

1. Pause audio.
2. Remove src.
3. Reset state.
4. Đổi `selectedLanguage`.
5. Render description mới.
6. Không autoplay.

## 18.7. Khi đóng modal/chuyển item

```typescript
audio.pause();
audio.removeAttribute('src');
audio.load();
```

Cleanup timeout/listener nếu có.

## 18.8. Xóa text EverAI

Xóa translation keys/message:

```text
Đang tạo giọng đọc
Generating narration
Vui lòng chờ
Tạo lại audio
TTS failed
```

Thêm translation keys cho:

```text
Tiếng Việt
English
Chuyển ngôn ngữ
Phát bản ghi âm
Dừng bản ghi âm
Không thể phát bản ghi âm
```

---

# 19. Frontend types và API

## 19.1. Management types

Create:

```typescript
export interface CreateGalleryItemInput {
  title: string;
  locationId: number;
  itemType: GalleryItemType;
  status: GalleryStatus;

  descriptionVi: string;
  audioVi: File;

  descriptionEn: string;
  audioEn: File;

  files: File[];
  youtubeUrls?: string[];
  primaryMediaKey?: string | null;
}
```

Update:

```typescript
export interface UpdateGalleryItemInput {
  galleryItemId: number;
  title: string;
  locationId: number;
  itemType: GalleryItemType;

  descriptionVi: string;
  descriptionEn: string;

  newAudioVi?: File | null;
  newAudioEn?: File | null;

  keepMediaIds: number[];
  newFiles: File[];
  youtubeUrls?: string[];
  primaryMediaKey?: string | null;
}
```

## 19.2. Xóa TTS types

Xóa:

```text
GalleryItemTtsManagementStatus
GalleryItemTtsStatus
GalleryItemTtsRegenerateResult
PublicTtsAudio
```

## 19.3. Management API

Xóa:

```typescript
getTtsAudioStatus()
regenerateTtsAudio()
```

## 19.4. Public API

Xóa:

```typescript
ensureTtsAudio()
getTtsAudioStatus()
```

Public detail trả audio URL trực tiếp.

## 19.5. API endpoints

Xóa endpoint constants TTS.

Thêm endpoint builder:

```typescript
galleryAudio(galleryItemId, languageCode)
```

nếu DTO không trả sẵn URL.

---

# 20. Xóa backend EverAI — checklist

Search và xóa toàn bộ production code liên quan:

## 20.1. Domain

```text
GalleryItemTtsAudio
TTS status constants
TTS trigger constants
```

## 20.2. Application

```text
IGalleryItemTtsService
GalleryItemTtsService
IGalleryTtsHashService
GalleryTtsHashService
IGalleryTtsJobQueue
GalleryTtsJobQueue

RegenerateGalleryItemTtsAudio
GetGalleryItemTtsStatus
EnsurePublicGalleryItemTtsAudio
GetPublicGalleryItemTtsAudioStatus
```

## 20.3. Infrastructure

```text
IEverAiTtsClient
EverAiTtsClient
EverAI request/response models
callback verification
background worker
hosted service
job processor
polling
temporary audio download
```

## 20.4. API

```text
GalleryManagementTtsController
EverAI callback controller
public ensure/status endpoints
```

## 20.5. DI

Xóa registration của toàn bộ service trên.

## 20.6. Config

Xóa khỏi:

```text
appsettings*.json
.env.example
Railway variable docs
deployment docs
```

Các biến kiểu:

```text
EVERAI_API_KEY
EVERAI_BASE_URL
EVERAI_CALLBACK_URL
EVERAI_CALLBACK_SECRET
EVERAI_VOICE_CODE
EVERAI_AUDIO_TYPE
EVERAI_BITRATE
EVERAI_SPEED_RATE
EVERAI_PITCH_RATE
EVERAI_VOLUME
EVERAI_POLL_INTERVAL
EVERAI_FAILED_COOLDOWN
```

Giữ:

```text
GoogleDrive__GalleryAudioFolderId
```

## 20.7. Source scan cuối

Production source không được còn reference chạy thật đến:

```text
EverAI
GalleryItemTts
EnsureAudioAsync
MANUAL_REGENERATE
AUTO_GENERATE
```

Tài liệu lịch sử có thể giữ nếu project muốn, nhưng phải đánh dấu obsolete; không để AI Agent sau này hiểu nhầm là implementation hiện hành.

---

# 21. Audit

## 21.1. Create

Ghi event create với thông tin:

```json
{
  "galleryItemId": 100,
  "hasVietnameseContent": true,
  "hasEnglishContent": true,
  "audioViFileId": 501,
  "audioEnFileId": 502,
  "mediaCount": 4
}
```

## 21.2. Update

Ghi:

```text
descriptionViChanged
descriptionEnChanged
oldAudioViFileId
newAudioViFileId
oldAudioEnFileId
newAudioEnFileId
```

Không ghi binary.

Không ghi secret Drive URL nếu convention audit không cho phép.

---

# 22. Error handling

Các error code nên ổn định và có mapping frontend:

```text
GALLERY_DESCRIPTION_VI_REQUIRED
GALLERY_AUDIO_VI_REQUIRED
GALLERY_DESCRIPTION_EN_REQUIRED
GALLERY_AUDIO_EN_REQUIRED

GALLERY_AUDIO_VI_INVALID
GALLERY_AUDIO_EN_INVALID
GALLERY_AUDIO_TOO_LARGE

GALLERY_CONTENT_MISSING
GALLERY_AUDIO_FILE_MISSING
GALLERY_AUDIO_LANGUAGE_INVALID
GALLERY_AUDIO_NOT_PUBLIC_VISIBLE
```

Frontend phải hiển thị message cụ thể từng field.

Backend không chỉ trả generic `INVALID_REQUEST`.

---

# 23. Security

## 23.1. Management

- Chỉ `STAFF + LEADER`.
- Campus từ JWT.
- Không nhận campus từ request.
- Item/location phải thuộc campus actor.

## 23.2. File upload

- Không tin MIME client.
- Validate extension/MIME/signature.
- Validate size.
- File purpose đúng.
- Không cho upload file rỗng.

## 23.3. Public audio

- Không expose arbitrary file.
- Resolve qua item + language.
- Chỉ item public-visible.
- Không leak item hidden hoặc campus khác.
- 404 thay vì tiết lộ trạng thái nội bộ.

---

# 24. Tests bắt buộc

## 24.1. Database

- Không insert được content thiếu `description_vi`.
- Không insert được content thiếu `audio_vi_file_id`.
- Không insert được content thiếu `description_en`.
- Không insert được content thiếu `audio_en_file_id`.
- Một item chỉ có một content row.
- FK audio VI hợp lệ.
- FK audio EN hợp lệ.
- Không xóa file đang được tham chiếu.
- Xóa item cascade content.

## 24.2. Create command

Test:

```text
Đủ 4 field + media → success
Thiếu descriptionVi → 422
Thiếu audioVi → 422
Thiếu descriptionEn → 422
Thiếu audioEn → 422
Description VI whitespace → 422
Description EN whitespace → 422
Audio VI sai định dạng → 422
Audio EN sai định dạng → 422
Audio quá lớn → 422
Không có media → 422
Location khác campus → 403
Location inactive → reject
```

Success phải assert:

```text
gallery_items có row
gallery_item_contents có row
audio VI file ID đúng
audio EN file ID đúng
media được tạo
primary media đúng
audit được ghi
không có TTS job
```

## 24.3. Update command

- Không gửi audio mới → giữ hai file cũ.
- Chỉ thay VI → EN không đổi.
- Chỉ thay EN → VI không đổi.
- Thay cả hai → cả hai đổi.
- Description VI rỗng → reject.
- Description EN rỗng → reject.
- Không có remove audio.
- Item khác campus → 403.
- DB fail sau upload → compensation được gọi.

## 24.4. Query/list/detail

- Detail trả đủ content VI/EN.
- Search tìm được theo VI.
- Search tìm được theo EN.
- Không còn AudioStatus.
- Không có N+1.

## 24.5. Management frontend

- Create không submit nếu thiếu một trong bốn field.
- Audio VI preview.
- Audio EN preview.
- URL preview được revoke.
- Edit giữ file cũ khi không chọn file mới.
- Edit thay từng file độc lập.
- Không còn TTS filter/status/regenerate.
- Toast mới đúng.

## 24.6. Public backend

- Detail trả đủ VI/EN.
- Audio VI stream đúng.
- Audio EN stream đúng.
- Item hidden → 404.
- Location inactive → 404.
- Area inactive → 404.
- Campus inactive → 404.
- File purpose sai → không phục vụ.
- Invalid language → reject/404 theo convention.

## 24.7. Public frontend

- Mặc định VI.
- Đổi EN đổi description.
- Đổi EN đổi audio URL.
- Audio VI đang phát thì đổi EN phải dừng.
- Đổi item reset VI.
- Đóng modal dừng audio.
- Không còn ensure/status calls.
- Không còn polling timer.
- Audio error hiển thị message phù hợp.

## 24.8. Regression

Chạy:

```text
dotnet build
dotnet test
frontend typecheck/build
lint nếu project có
```

Không bỏ qua test Gallery bằng `[Fact(Skip=...)]` nếu test thuộc phạm vi thay đổi; phải thay placeholder bằng test thực tế.

---

# 25. Thứ tự triển khai

## Phase A — Database additive

1. Tạo bảng `gallery_item_contents`.
2. Thêm entity/config.
3. Thêm DbSet.
4. Thêm migration verify.
5. Chưa drop cột/table cũ.

## Phase B — Backend write/read mới

1. DTO create/update mới.
2. Validation bốn field.
3. Upload audio VI/EN.
4. Create transaction.
5. Update transaction.
6. Detail/list/search mới.
7. Compensation/orphan cleanup.
8. Public detail mới.
9. Public audio endpoint.

## Phase C — STAFF LEADER frontend

1. Create/Edit form song ngữ.
2. Hai audio picker.
3. Preview.
4. Update FormData.
5. Detail modal song ngữ.
6. Xóa TTS badges/filter/regenerate.
7. Cập nhật errors/toast.

## Phase D — Public frontend

1. DTO song ngữ.
2. Language toggle.
3. Direct audio playback.
4. Cleanup khi đổi language/item/modal.
5. Xóa ensure/poll.

## Phase E — Remove EverAI

1. Xóa controllers.
2. Xóa application TTS.
3. Xóa infrastructure client/worker/queue.
4. Xóa DI.
5. Xóa config.
6. Xóa tests cũ hoặc thay bằng tests mới.
7. Scan repository.

## Phase F — Cleanup database/seed

1. Xóa/reseed Gallery Item seed.
2. Tạo audio seed thật.
3. Drop `gallery_item_tts_audios`.
4. Drop `gallery_items.description`.
5. Rebuild indexes.
6. Verify counts/FK.
7. Full regression.

---

# 26. Danh sách module dự kiến bị ảnh hưởng

## Backend Domain

```text
GalleryItem.cs
GalleryItemContent.cs             → thêm
GalleryItemTtsAudio.cs            → xóa
UploadedFile relations
```

## Backend Application

```text
AddGalleryItemCommand
AddGalleryItemCommandHandler
Update/EditGalleryItemCommand
Update/EditGalleryItemCommandHandler
Gallery detail builder
Gallery list/search
Public Gallery DTO/builders

Galleries/Tts/**                  → xóa
```

## Backend Infrastructure

```text
EverAI client/**                  → xóa
TTS queue/**                      → xóa
TTS background service/**         → xóa
GoogleDrive folder resolver       → giữ GalleryAudio
File validation policy            → giữ/cập nhật audio policy
Cleanup service/job               → thêm hoặc tích hợp
```

## Backend API

```text
GalleriesController
Public VisitFPTU controller
GalleryManagementTtsController    → xóa
EverAI callback controller        → xóa
```

## Frontend Management

```text
GalleryManagementStaffLeader.tsx
GalleryUpsertModal.tsx
GalleryDetailModal.tsx
galleryManagementApi.ts
galleryManagement.types.ts
galleryError.ts
API_ENDPOINTS
```

## Frontend Public

```text
CampusDetailVisitPage.tsx
publicVisitFptuApi.ts
publicVisitFptu.types.ts
API_ENDPOINTS
i18n files
```

## Database/docs/tests

```text
main SQL/schema
migration scripts
seed scripts
database schema documentation
Gallery use-case documentation
application tests
integration tests
frontend tests
```

---

# 27. Acceptance criteria

Chỉ coi là hoàn thành khi đạt đủ:

1. Create bắt buộc description VI.
2. Create bắt buộc audio VI.
3. Create bắt buộc description EN.
4. Create bắt buộc audio EN.
5. Thiếu một trường phải fail và không tạo Gallery Item.
6. Hai audio lưu đúng Google Drive Gallery Audio folder.
7. `gallery_item_contents` quan hệ 1:1 với item.
8. Edit giữ hoặc thay từng audio độc lập.
9. Không thể xóa audio VI.
10. Không thể xóa audio EN.
11. Public luôn có VI/EN.
12. Đổi language đổi cả description và audio.
13. Đổi language dừng audio cũ.
14. Public phát audio trực tiếp, không ensure/poll.
15. Management không còn TTS status/filter/regenerate.
16. Source production không còn EverAI integration.
17. Database không còn `gallery_item_tts_audios`.
18. Database không còn `gallery_items.description`.
19. Seed Gallery mới có đủ hai description và hai file audio thật.
20. Tests mới bao phủ create/update/public/security/cleanup.
21. Full build và regression suite xanh.

Baseline cuối cùng:

```text
Gallery Item hợp lệ
├── Description VI: REQUIRED
├── Audio VI: REQUIRED
├── Description EN: REQUIRED
├── Audio EN: REQUIRED
├── Gallery media: REQUIRED
└── EverAI: REMOVED COMPLETELY
```

---

# 28. Nguyên tắc thực thi cho AI Agent

- Đọc code thật trước khi sửa.
- Không chỉ sửa frontend.
- Không tạo schema song song rồi để code cũ vẫn chạy.
- Không để fallback EverAI.
- Không giữ endpoint TTS “để dự phòng”.
- Không giữ state/status TTS không còn ý nghĩa.
- Không dùng raw Google Drive SDK trong handler.
- Không nhận campus từ client.
- Không làm mất audio cũ trước khi DB commit.
- Không tạo seed file giả.
- Không bỏ qua test.
- Không commit tên/attribution của AI.
- Gom thay đổi theo functional slice; tránh nhiều commit vụn chỉ chứa một file không có ý nghĩa độc lập.
- Cập nhật tài liệu schema/use case sau khi code hoàn thành.
