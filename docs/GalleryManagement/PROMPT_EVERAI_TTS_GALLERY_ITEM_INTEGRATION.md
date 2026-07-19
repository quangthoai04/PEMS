> ⚠️ **OBSOLETE / KHÔNG CÒN HIỆU LỰC (2026-07-17).** Cơ chế EverAI TTS đã bị gỡ bỏ hoàn toàn và
> thay bằng **audio song ngữ (VI/EN) do Staff Leader tự upload** — xem
> `17_07_26_PROMPT_GALLERY_BILINGUAL_MANUAL_AUDIO_REMOVE_EVERAI_FULL_IMPLEMENTATION.md`. Tài liệu này
> chỉ giữ lại làm lịch sử; KHÔNG dùng để triển khai nữa (không còn bảng `gallery_item_tts_audios`,
> không còn cột `gallery_items.description`, không còn endpoint TTS).

# PROMPT / ĐẶC TẢ CODE — Tích hợp EverAI TTS cho VisitFPTU Gallery Item

> Tài liệu này dùng cho AI Agent đọc và code tích hợp **EverAI Text To Speech** vào module **VisitFPTU Gallery** của PEMS.
>
> Trạng thái hiện tại do user xác nhận:
>
> - Chức năng Gallery Management và upload Google Drive đã hoàn thiện.
> - Public Gallery đã có icon loa.
> - User đã thêm cấu hình `EverAiTts` với **API key thật** vào `backend/PEMS.Api/appsettings.Development.json`.
> - User đã tạo sẵn Google Drive folder `gallery-audio` và config `GoogleDrive:GalleryAudioFolderId`.
>
> AI Agent phải đọc kỹ source thật trước khi code. Không code theo trí nhớ. Không mock data. Không sinh file rác.

---

## 0. Quy tắc an toàn bắt buộc

```text
KHÔNG in/log EverAI ApiKey.
KHÔNG commit appsettings.Development.json nếu file này chứa secret thật.
KHÔNG copy ApiKey thật vào file example / docs / log / response.
KHÔNG để frontend gọi EverAI trực tiếp.
KHÔNG dùng EverAI audio_link làm link phát chính.
KHÔNG hard-code folderId Google Drive trong handler/job.
KHÔNG gọi Google Drive trực tiếp trong business handler hoặc TTS job.
KHÔNG lưu binary/base64 audio vào MySQL.
KHÔNG sửa flow upload Gallery/Avatar cũ nếu không cần.
KHÔNG triển khai Backfill missing audio.
```

Tất cả file audio sau khi tạo phải được upload qua nền tảng upload dùng chung:

```text
IFileUploadService.UploadBusinessFileAsync(..., FilePurpose.GalleryAudio, ...)
```

Frontend/public chỉ phát qua:

```text
/api/files/{audio_file_id}/content
```

---

## 1. Mục tiêu nghiệp vụ

Bổ sung audio thuyết minh cho từng `gallery_items`, lấy text từ:

```text
gallery_items.description
```

Luồng tổng quát:

```text
Gallery item description
→ Backend PEMS gọi EverAI TTS
→ EverAI tạo audio
→ Backend tải audio từ EverAI audio_link
→ Backend upload audio lên Google Drive folder gallery-audio
→ Lưu audio_file_id vào DB
→ Public Gallery phát audio qua /api/files/{audio_file_id}/content
```

Voice mặc định phải dùng:

```text
vi_female_hoaian_mb
```

Cấu hình audio mặc định:

```text
audio_type = mp3
bitrate = 128
speed_rate = 1.0
pitch_rate = 1.0
volume = 100
MaxInputCharacters = 1000
FailedCooldownMinutes = 30
```

---

## 2. Phạm vi triển khai

### 2.1. Trong scope

Cần triển khai:

```text
1. Backend config binding cho EverAiTts.
2. Google Drive routing cho GalleryAudioFolderId.
3. FilePurpose.GalleryAudio + files.file_purpose = GALLERY_AUDIO.
4. Bảng gallery_item_tts_audios.
5. Hash logic cho description + voice/audio setting.
6. EverAI TTS client.
7. GalleryItemTtsService.
8. Background job/worker xử lý tạo audio.
9. Callback endpoint từ EverAI.
10. Public API cho icon loa:
    - POST ensure
    - GET poll status
11. Dashboard Staff Leader API manual regenerate.
12. Hook auto generate vào create/update Gallery item.
13. Sửa UI create/edit Gallery item: textarea Mô tả to hơn + counter 0/1000.
14. Frontend public: gắn icon loa với TTS API, loading/poll/play.
15. Test unit/integration/UI.
```

### 2.2. Ngoài scope

Không làm:

```text
1. Không làm Backfill missing audio.
2. Không tạo endpoint tạo audio hàng loạt.
3. Không tạo trigger_source BACKFILL.
4. Không sửa lại UI Public Gallery ngoài logic icon loa nếu không cần.
5. Không thay đổi nghiệp vụ Gallery item MEDIA / VISIT_DELEGATION.
6. Không link Gallery audio với visit_instance.
7. Không xóa file audio cũ khỏi Google Drive ở phase này.
8. Không đổi role/RBAC Gallery hiện tại.
9. Không dùng mock data.
```

---

## 3. Source/tài liệu cần đọc trước khi code

AI Agent phải mở và đối chiếu source thật trước khi sửa.

Ưu tiên đọc các file backend sau:

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

Tìm các handler Gallery hiện tại:

```bash
grep -R "UploadBusinessFileAsync" -n backend/PEMS.Application
grep -R "FilePurpose.Gallery" -n backend/PEMS.Application backend/PEMS.Infrastructure backend/PEMS.Api
grep -R "GalleryAudioFolderId\|EverAiTts\|GalleryFolderId" -n backend
grep -R "gallery_item" -n backend/PEMS.Application | head -100
```

Tìm frontend form create/edit Gallery item và public icon loa:

```bash
grep -R "MÔ TẢ\|Mô tả\|description" -n frontend/pems-react/src
grep -R "loa\|volume\|audio\|speaker" -n frontend/pems-react/src
```

---

## 4. EverAI API contract cần dùng

### 4.1. Create TTS request

Endpoint:

```http
POST https://www.everai.vn/api/v1/tts
Content-Type: application/json
Authorization: Bearer <API_KEY>
```

Body PEMS nên gửi:

```json
{
  "response_type": "indirect",
  "callback_url": "https://your-domain/api/integrations/everai/tts/callback",
  "input_text": "Nội dung từ gallery_items.description",
  "voice_code": "vi_female_hoaian_mb",
  "audio_type": "mp3",
  "bitrate": 128,
  "speed_rate": 1.0,
  "pitch_rate": 1.0,
  "volume": 100
}
```

Với local/dev chưa có public callback URL có thể dùng polling. Nếu `UseCallback = false`, vẫn có thể truyền callback_url rỗng/null nếu API cho phép; nếu EverAI yêu cầu field, dùng URL đã config nhưng worker vẫn polling.

Response thành công dạng:

```json
{
  "status": 1,
  "result": {
    "request_id": "0e7ee265-34ff-4807-b9ae-a4eb3ef24573",
    "characters": 24,
    "voice_code": "vi_female_hoaian_mb",
    "audio_type": "mp3",
    "speed_rate": 1.0,
    "pitch_rate": 1.0,
    "bitrate": 128,
    "create_at": "2024-09-13",
    "status": "new"
  }
}
```

Nếu `status = 0`, đọc `error_code` và `error_message`, set job `FAILED`.

### 4.2. Get request / polling

Endpoint:

```http
GET https://www.everai.vn/api/v1/tts/{request_id}
Authorization: Bearer <API_KEY>
```

Response có thể có:

```json
{
  "status": 1,
  "result": {
    "request_id": "...",
    "progress": 100.0,
    "status": "done",
    "audio_link": "https://...",
    "audio_expired": false
  }
}
```

Nếu `audio_expired = true`, không dùng link đó; set lỗi `TTS_EVERAI_AUDIO_EXPIRED` hoặc cho manual regenerate tạo lại.

### 4.3. Callback

Endpoint PEMS nhận callback:

```http
POST /api/integrations/everai/tts/callback
```

Payload EverAI dạng:

```json
{
  "request_id": "...",
  "characters": 24,
  "voice_code": "vi_female_hoaian_mb",
  "audio_type": "mp3",
  "speed_rate": 1.0,
  "pitch_rate": 1.0,
  "bitrate": 128,
  "created_at": "2024-09-13",
  "status": "SUCCESS",
  "audio_link": "https://..."
}
```

`status` chính:

```text
SUCCESS
FAILURE
```

Callback phải idempotent: callback đến 2 lần không upload lại nếu row đã READY.

---

## 5. Cấu hình backend

### 5.1. appsettings.Development.json

User đã thêm `EverAiTts` với API key thật. AI Agent chỉ kiểm tra bind đúng, không in secret.

Cấu trúc mong muốn:

```json
{
  "GoogleDrive": {
    "GalleryAudioFolderId": "ID_FOLDER_gallery-audio"
  },
  "EverAiTts": {
    "Enabled": true,
    "BaseUrl": "https://www.everai.vn/api/v1",
    "ApiKey": "<REAL_API_KEY_IN_DEVELOPMENT_JSON>",
    "DefaultVoiceCode": "vi_female_hoaian_mb",
    "DefaultAudioType": "mp3",
    "DefaultBitrate": 128,
    "DefaultSpeedRate": 1.0,
    "DefaultPitchRate": 1.0,
    "DefaultVolume": 100,
    "MaxInputCharacters": 1000,
    "FailedCooldownMinutes": 30,
    "UseCallback": false,
    "CallbackUrl": "http://localhost:5265/api/integrations/everai/tts/callback",
    "PollingIntervalSeconds": 3,
    "PollingMaxAttempts": 20
  }
}
```

### 5.2. appsettings.json / example

Trong `appsettings.json` hoặc `appsettings.Development.example.json`, chỉ thêm placeholder, không có secret thật:

```json
{
  "EverAiTts": {
    "Enabled": false,
    "BaseUrl": "https://www.everai.vn/api/v1",
    "ApiKey": "",
    "DefaultVoiceCode": "vi_female_hoaian_mb",
    "DefaultAudioType": "mp3",
    "DefaultBitrate": 128,
    "DefaultSpeedRate": 1.0,
    "DefaultPitchRate": 1.0,
    "DefaultVolume": 100,
    "MaxInputCharacters": 1000,
    "FailedCooldownMinutes": 30,
    "UseCallback": false,
    "CallbackUrl": "",
    "PollingIntervalSeconds": 3,
    "PollingMaxAttempts": 20
  }
}
```

### 5.3. Options class

Tạo class, vị trí gợi ý:

```text
backend/PEMS.Application/Common/Options/EverAiTtsOptions.cs
```

Hoặc theo convention options hiện có của project.

```csharp
public sealed class EverAiTtsOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://www.everai.vn/api/v1";
    public string ApiKey { get; set; } = string.Empty;

    public string DefaultVoiceCode { get; set; } = "vi_female_hoaian_mb";
    public string DefaultAudioType { get; set; } = "mp3";
    public int DefaultBitrate { get; set; } = 128;
    public decimal DefaultSpeedRate { get; set; } = 1.0m;
    public decimal DefaultPitchRate { get; set; } = 1.0m;
    public int DefaultVolume { get; set; } = 100;

    public int MaxInputCharacters { get; set; } = 1000;
    public int FailedCooldownMinutes { get; set; } = 30;

    public bool UseCallback { get; set; }
    public string CallbackUrl { get; set; } = string.Empty;
    public int PollingIntervalSeconds { get; set; } = 3;
    public int PollingMaxAttempts { get; set; } = 20;
}
```

DI:

```csharp
services.Configure<EverAiTtsOptions>(configuration.GetSection("EverAiTts"));
```

---

## 6. Google Drive upload foundation cần mở rộng

### 6.1. GoogleDriveOptions

Thêm property nếu chưa có:

```csharp
public string GalleryAudioFolderId { get; set; } = string.Empty;
```

### 6.2. FilePurpose

Thêm enum value:

```csharp
GalleryAudio
```

DB value:

```csharp
public const string GalleryAudio = "GALLERY_AUDIO";
```

`ToDbValue()`:

```csharp
FilePurpose.GalleryAudio => FilePurposeDbValues.GalleryAudio
```

`ToObjectKeyPrefix()`:

```csharp
FilePurpose.GalleryAudio => "gallery/audio"
```

### 6.3. GoogleDriveFolderResolver

Map:

```csharp
FilePurpose.GalleryAudio => _options.GalleryAudioFolderId
```

Nếu thiếu config:

```text
GOOGLE_DRIVE_FOLDER_NOT_CONFIGURED
```

Không fallback âm thầm sang `GalleryFolderId`, vì yêu cầu audio phải vào folder `gallery-audio`.

### 6.4. FileValidationPolicy

Thêm rule:

```text
FilePurpose.GalleryAudio:
- Extensions: .mp3, .wav
- Mime types: audio/mpeg, audio/mp3, audio/wav, audio/x-wav
- Max size: 10MB hoặc 20MB theo convention project
- RequireImageMagicBytes = false
```

Nếu `files.file_purpose` là ENUM/CHECK trong SQL thật, phải patch thêm `GALLERY_AUDIO`.

---

## 7. Database patch

### 7.1. Tạo bảng gallery_item_tts_audios

```sql
CREATE TABLE gallery_item_tts_audios (
    tts_audio_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    gallery_item_id BIGINT UNSIGNED NOT NULL,

    source_text_hash CHAR(64) NOT NULL,
    source_text TEXT NOT NULL,

    voice_code VARCHAR(100) NOT NULL,
    audio_type ENUM('mp3','wav') NOT NULL DEFAULT 'mp3',
    bitrate INT NULL,
    speed_rate DECIMAL(3,1) NOT NULL DEFAULT 1.0,
    pitch_rate DECIMAL(3,1) NOT NULL DEFAULT 1.0,
    volume INT NOT NULL DEFAULT 100,

    status ENUM(
        'PENDING',
        'SUBMITTED',
        'PROCESSING',
        'READY',
        'FAILED',
        'CANCELLED'
    ) NOT NULL DEFAULT 'PENDING',

    everai_request_id VARCHAR(100) NULL,
    everai_audio_link TEXT NULL,

    audio_file_id BIGINT UNSIGNED NULL,

    trigger_source ENUM(
        'AUTO_GENERATE',
        'LAZY_GENERATE',
        'MANUAL_REGENERATE'
    ) NOT NULL,

    characters INT NULL,
    progress DECIMAL(5,2) NULL,

    error_code VARCHAR(100) NULL,
    error_message TEXT NULL,

    requested_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    submitted_at DATETIME NULL,
    processing_at DATETIME NULL,
    ready_at DATETIME NULL,
    failed_at DATETIME NULL,

    created_by BIGINT UNSIGNED NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT UNSIGNED NULL,
    updated_at DATETIME NULL,

    PRIMARY KEY (tts_audio_id),

    KEY idx_gallery_tts_item_status (
        gallery_item_id,
        status,
        created_at
    ),

    KEY idx_gallery_tts_request (
        everai_request_id
    ),

    KEY idx_gallery_tts_hash_lookup (
        gallery_item_id,
        source_text_hash,
        voice_code,
        audio_type,
        bitrate,
        speed_rate,
        pitch_rate,
        volume,
        status
    ),

    KEY idx_gallery_tts_audio_file (audio_file_id),

    CONSTRAINT fk_gallery_tts_item
        FOREIGN KEY (gallery_item_id)
        REFERENCES gallery_items(gallery_item_id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_gallery_tts_audio_file
        FOREIGN KEY (audio_file_id)
        REFERENCES files(file_id)
        ON UPDATE CASCADE
        ON DELETE RESTRICT,

    CONSTRAINT fk_gallery_tts_created_by
        FOREIGN KEY (created_by)
        REFERENCES users(user_id)
        ON UPDATE CASCADE
        ON DELETE SET NULL,

    CONSTRAINT fk_gallery_tts_updated_by
        FOREIGN KEY (updated_by)
        REFERENCES users(user_id)
        ON UPDATE CASCADE
        ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### 7.2. Chống tạo trùng running job

Khuyến nghị dùng generated column:

```sql
ALTER TABLE gallery_item_tts_audios
ADD COLUMN running_key VARCHAR(500)
GENERATED ALWAYS AS (
    CASE
        WHEN status IN ('PENDING','SUBMITTED','PROCESSING') THEN
            CONCAT(
                gallery_item_id, ':',
                source_text_hash, ':',
                voice_code, ':',
                audio_type, ':',
                IFNULL(bitrate, 0), ':',
                speed_rate, ':',
                pitch_rate, ':',
                volume
            )
        ELSE NULL
    END
) STORED,
ADD UNIQUE KEY uq_gallery_tts_running_key (running_key);
```

MySQL cho phép nhiều NULL trong unique index, nên READY/FAILED/CANCELLED không bị chặn. Chỉ PENDING/SUBMITTED/PROCESSING bị unique theo hash/config.

Nếu project không muốn dùng generated column, phải xử lý bằng transaction + lock:

```text
SELECT ... FOR UPDATE theo gallery_item_id + hash/config trước khi insert job.
```

Nhưng unique key vẫn an toàn hơn trong case nhiều public user bấm loa cùng lúc.

---

## 8. Quan hệ dữ liệu

Về public UX:

```text
1 gallery item → chỉ phát 1 audio hiện hành
```

Về DB:

```text
gallery_items 1 - n gallery_item_tts_audios
gallery_item_tts_audios n - 1 files
```

Lý do không làm 1-1 cứng:

```text
- Tạo audio có thể FAILED.
- Staff Leader có thể tạo lại audio.
- Description có thể sửa.
- Voice/audio setting có thể đổi.
- Cần lưu lịch sử request/lỗi/retry.
```

Public API chỉ trả audio READY khớp hash hiện tại:

```text
gallery_item_id = current item
source_text_hash = hash(description hiện tại + voice/audio setting hiện tại)
status = READY
audio_file_id IS NOT NULL
```

Nếu có nhiều READY cùng hash, chọn bản mới nhất theo `ready_at DESC, tts_audio_id DESC`.

---

## 9. Hash logic

Tạo service, ví dụ:

```text
IGalleryTtsHashService
GalleryTtsHashService
```

Canonical string:

```text
text=<normalized description>
voice_code=<voice_code>
audio_type=<audio_type>
bitrate=<bitrate>
speed_rate=<speed_rate>
pitch_rate=<pitch_rate>
volume=<volume>
```

Normalize description:

```text
Trim đầu/cuối.
Gộp nhiều whitespace thành một khoảng trắng.
Giữ nguyên dấu tiếng Việt.
Không lowercase.
```

SHA-256 canonical string → lowercase hex 64 ký tự.

Ý nghĩa:

```text
Description đổi → hash đổi.
Voice/audio setting đổi → hash đổi.
Audio READY cũ không khớp hash mới thì không phát public nữa.
Muốn áp dụng setting mới: manual regenerate hoặc lazy generate khi public bấm loa.
```

---

## 10. Backend services/classes cần tạo

### 10.1. EverAI client

Interface:

```csharp
public interface IEverAiTtsClient
{
    Task<EverAiCreateTtsResponse> CreateAsync(
        EverAiCreateTtsRequest request,
        CancellationToken cancellationToken);

    Task<EverAiGetTtsResponse> GetRequestAsync(
        string requestId,
        CancellationToken cancellationToken);
}
```

Implementation dùng `HttpClientFactory`. Không log ApiKey.

Request DTO:

```csharp
public sealed class EverAiCreateTtsRequest
{
    [JsonPropertyName("response_type")]
    public string ResponseType { get; init; } = "indirect";

    [JsonPropertyName("callback_url")]
    public string? CallbackUrl { get; init; }

    [JsonPropertyName("input_text")]
    public string InputText { get; init; } = string.Empty;

    [JsonPropertyName("voice_code")]
    public string VoiceCode { get; init; } = "vi_female_hoaian_mb";

    [JsonPropertyName("audio_type")]
    public string AudioType { get; init; } = "mp3";

    [JsonPropertyName("bitrate")]
    public int Bitrate { get; init; } = 128;

    [JsonPropertyName("speed_rate")]
    public decimal SpeedRate { get; init; } = 1.0m;

    [JsonPropertyName("pitch_rate")]
    public decimal PitchRate { get; init; } = 1.0m;

    [JsonPropertyName("volume")]
    public int Volume { get; init; } = 100;
}
```

### 10.2. GalleryItemTtsService

Interface:

```csharp
public interface IGalleryItemTtsService
{
    Task<GalleryItemTtsEnsureResult> EnsureAudioAsync(
        long galleryItemId,
        TtsTriggerSource triggerSource,
        long? actorUserId,
        bool requirePublicVisible,
        bool bypassFailedCooldown,
        CancellationToken cancellationToken);

    Task ProcessJobAsync(
        long ttsAudioId,
        CancellationToken cancellationToken);

    Task HandleEverAiCallbackAsync(
        EverAiTtsCallbackDto callback,
        CancellationToken cancellationToken);
}
```

`EnsureAudioAsync` là core dùng cho:

```text
AUTO_GENERATE khi create/update Gallery item
LAZY_GENERATE khi public bấm loa
MANUAL_REGENERATE khi Staff Leader bấm tạo lại
```

### 10.3. Background queue/worker

Dùng hạ tầng background job hiện có nếu project đã có. Nếu chưa có, tạo tối thiểu:

```text
IGalleryTtsJobQueue
GalleryTtsJobQueue
GalleryTtsBackgroundService : BackgroundService
```

Queue nhận `tts_audio_id` và gọi `GalleryItemTtsService.ProcessJobAsync`.

---

## 11. Luồng AUTO_GENERATE khi Staff Leader tạo/sửa Gallery item

Hook vào handler create/update Gallery item sau khi lưu item thành công.

Flow:

```text
1. Staff Leader tạo/sửa Gallery item.
2. Backend validate và lưu item như hiện tại.
3. Nếu description hợp lệ:
   - trim không rỗng
   - <= 1000 ký tự
   - EverAiTts.Enabled = true
   - ApiKey có
   - GalleryAudioFolderId có
4. Tính source_text_hash.
5. Nếu READY đúng hash/config đã có → không tạo.
6. Nếu PENDING/SUBMITTED/PROCESSING đúng hash/config đã có → không tạo.
7. Nếu chưa có → insert gallery_item_tts_audios PENDING trigger_source=AUTO_GENERATE.
8. Enqueue job.
9. API create/update trả thành công ngay, không chờ audio.
```

Nếu TTS config lỗi hoặc description không hợp lệ:

```text
Không fail create/update Gallery item.
Không tạo audio.
Có thể ghi warning log sanitized.
```

Nhưng nếu backend validation đã chốt description <= 1000, thì create/update phải reject khi description > 1000.

---

## 12. Luồng LAZY_GENERATE khi public bấm loa

Public icon loa gọi:

```http
POST /api/public/gallery-items/{galleryItemId}/tts-audio/ensure
```

Flow backend:

```text
1. Check public visibility:
   - campus ACTIVE
   - area ACTIVE
   - location ACTIVE
   - gallery item PUBLISHED
   - gallery item deleted_at IS NULL
   - nếu public gallery logic hiện tại yêu cầu media ACTIVE thì check theo cùng query public hiện có

2. Check description:
   - trim không rỗng
   - <= 1000 ký tự

3. Check config:
   - EverAiTts.Enabled = true
   - ApiKey có
   - GalleryAudioFolderId có

4. Tính source_text_hash.
5. Nếu READY đúng hash/config → trả READY + audioUrl.
6. Nếu running job đúng hash/config → trả PROCESSING.
7. Nếu FAILED gần nhất cùng hash/config và failed_at chưa quá 30 phút → trả TEMPORARILY_UNAVAILABLE.
8. Nếu chưa có gì → tạo PENDING trigger_source=LAZY_GENERATE, enqueue job, trả PROCESSING.
```

Response READY:

```json
{
  "status": "READY",
  "audioUrl": "/api/files/123/content"
}
```

Response PROCESSING:

```json
{
  "status": "PROCESSING",
  "message": "Giọng đọc đang được tạo, vui lòng chờ trong giây lát."
}
```

Response TEMPORARILY_UNAVAILABLE:

```json
{
  "status": "TEMPORARILY_UNAVAILABLE",
  "message": "Chưa thể tạo giọng đọc cho nội dung này. Vui lòng thử lại sau."
}
```

Không được tạo audio cho item HIDDEN / area INACTIVE / location INACTIVE / campus INACTIVE.

---

## 13. Public poll endpoint

Endpoint chỉ đọc:

```http
GET /api/public/gallery-items/{galleryItemId}/tts-audio
```

Flow:

```text
1. Check public visibility.
2. Tính hash theo description + setting hiện tại.
3. Nếu READY đúng hash → trả audioUrl.
4. Nếu running job → trả PROCESSING.
5. Nếu FAILED gần đây → trả TEMPORARILY_UNAVAILABLE.
6. Nếu chưa có → trả NOT_CREATED.
```

Response NOT_CREATED:

```json
{
  "status": "NOT_CREATED"
}
```

---

## 14. Background job processing

`ProcessJobAsync(ttsAudioId)`:

```text
1. Load tts row.
2. Nếu status không phải PENDING → bỏ qua.
3. Validate config lần cuối.
4. Set status = SUBMITTED, submitted_at = now.
5. Gọi EverAI POST /tts.
6. Nếu EverAI status = 0:
   - set FAILED
   - lưu error_code/error_message
   - failed_at = now
   - stop.
7. Nếu thành công:
   - lưu everai_request_id
   - lưu characters
   - set status = PROCESSING, processing_at = now.
8. Nếu UseCallback = true:
   - kết thúc job, chờ callback xử lý audio_link.
9. Nếu UseCallback = false:
   - polling GET /tts/{request_id} theo PollingIntervalSeconds/PollingMaxAttempts.
   - cập nhật progress nếu có.
   - khi status done + audio_link có:
       download audio_link.
       upload lên Google Drive qua IFileUploadService + FilePurpose.GalleryAudio.
       set audio_file_id.
       set status READY, ready_at = now.
   - nếu hết attempts chưa xong:
       giữ PROCESSING hoặc set FAILED tùy convention; khuyến nghị giữ PROCESSING và có worker retry sau nếu có scheduler.
```

### Download audio_link

Yêu cầu:

```text
- Dùng HttpClient.
- Kiểm tra HTTP success.
- Kiểm tra content-type audio.
- Kiểm tra size > 0 và không vượt max audio size.
- Không lưu file tạm nếu không cần; dùng stream/memory stream theo convention IFileUploadService.
```

File name gợi ý:

```text
gallery-item-{galleryItemId}-tts-{ttsAudioId}.mp3
```

`uploadedBy`:

```text
- created_by nếu có.
- Nếu LAZY_GENERATE không có user: dùng null/system user theo convention project.
- Nếu IFileUploadService bắt buộc uploadedBy long: dùng created_by của gallery item hoặc một system user id nếu project đã có.
```

---

## 15. EverAI callback endpoint

Endpoint:

```http
POST /api/integrations/everai/tts/callback
```

Flow:

```text
1. Nhận payload.
2. Không log audio_link đầy đủ nếu không cần; log sanitized.
3. Tìm row theo everai_request_id = request_id.
4. Nếu không thấy row → trả 200, ghi warning.
5. Nếu row READY → trả 200, idempotent.
6. Nếu status = SUCCESS:
   - nếu audio_link rỗng → FAILED.
   - download audio.
   - upload Google Drive FilePurpose.GalleryAudio.
   - update audio_file_id.
   - status = READY.
7. Nếu status = FAILURE:
   - status = FAILED.
   - failed_at = now.
   - error_code/error_message nếu có.
8. Trả 200.
```

Nếu muốn bảo vệ callback, có thể thêm secret query/header riêng trong CallbackUrl, ví dụ:

```text
/api/integrations/everai/tts/callback?token=<server-generated-token>
```

Nhưng không bắt buộc nếu EverAI không hỗ trợ signature. Không log token.

---

## 16. Manual regenerate

Endpoint:

```http
POST /api/gallery-management/items/{galleryItemId}/tts-audio/regenerate
```

Quyền:

```text
Staff Leader active.
Gallery item thuộc primary_campus_id của Staff Leader.
```

Flow:

```text
1. Staff Leader bấm “Tạo lại audio”.
2. Backend check scope campus.
3. Validate description trim không rỗng và <= 1000.
4. Tính hash theo setting hiện tại.
5. Nếu có running job cùng hash/config → trả PROCESSING.
6. Nếu không có → insert PENDING trigger_source=MANUAL_REGENERATE.
7. Enqueue job.
8. Trả PROCESSING.
```

Manual regenerate được bypass failed cooldown.

Dùng khi:

```text
- EverAI hết credits rồi đã nạp lại.
- ApiKey sai rồi đã sửa.
- Google Drive token lỗi rồi đã sửa.
- Audio FAILED.
- Voice/audio setting đổi và Staff Leader muốn item áp dụng setting mới ngay.
```

---

## 17. Không triển khai Backfill missing audio

Không tạo:

```http
POST /api/gallery-management/tts-audio/backfill-missing
```

Không thêm UI:

```text
Generate missing Gallery audio
Tạo audio cho nội dung còn thiếu
```

Không thêm enum:

```text
BACKFILL
```

Seed/data cũ chưa audio sẽ được xử lý bằng Lazy generate khi public user bấm loa.

---

## 18. Sửa create/edit Gallery item description UI

Hiện ô `MÔ TẢ` đang hơi thấp/nhỏ. Cần sửa cả create và edit Gallery item.

### 18.1. Yêu cầu UI

```text
- Textarea to/rộng hơn.
- Label: MÔ TẢ *
- Có counter realtime: 0/1000 ký tự.
- Chặn nhập quá 1000 ký tự.
- Paste >1000 ký tự thì tự cắt về 1000 hoặc báo lỗi; khuyến nghị tự cắt.
- Khi đạt 1000/1000 thì hiển thị warning nhỏ.
```

Gợi ý UI:

```text
MÔ TẢ *
[ textarea lớn ]
0/1000 ký tự
```

Khi đạt limit:

```text
1000/1000 ký tự
Bạn đã đạt giới hạn tối đa 1000 ký tự.
```

### 18.2. Gợi ý Tailwind

```tsx
const MAX_DESCRIPTION_LENGTH = 1000;

<textarea
  value={description}
  maxLength={MAX_DESCRIPTION_LENGTH}
  rows={6}
  onChange={(e) => setDescription(e.target.value.slice(0, MAX_DESCRIPTION_LENGTH))}
  className="min-h-[160px] w-full resize-y rounded-xl border px-4 py-3 text-sm"
/>

<div className="mt-1 flex items-center justify-between text-xs">
  <span className={description.length >= MAX_DESCRIPTION_LENGTH ? "text-red-600" : "text-gray-500"}>
    {description.length}/{MAX_DESCRIPTION_LENGTH} ký tự
  </span>
</div>
```

Nếu modal hiện tại hẹp, cân nhắc tăng modal width để textarea dễ nhập.

### 18.3. Frontend validation

```text
description.trim() không được rỗng.
description.length <= 1000.
Submit disabled hoặc hiện lỗi nếu invalid.
```

### 18.4. Backend validation

Create/Edit Gallery item command validator phải chặn:

```text
Description required.
Description trim không rỗng.
Description <= 1000 ký tự.
```

Nếu API trực tiếp gửi quá dài:

```http
HTTP 422
```

Message:

```text
Mô tả không được vượt quá 1000 ký tự.
```

---

## 19. Public frontend icon loa

Public Gallery đã có icon loa, chỉ cần gắn API.

Flow:

```text
User click loa
→ POST /api/public/gallery-items/{id}/tts-audio/ensure
→ Nếu READY: play audioUrl.
→ Nếu PROCESSING: icon loading, poll GET mỗi 2-3 giây.
→ Nếu READY sau poll: play audio.
→ Nếu TEMPORARILY_UNAVAILABLE/FAILED: hiện toast/tooltip nhẹ.
→ Nếu chuyển item/location: stop audio + cancel polling.
```

State gợi ý:

```text
idle
loading
playing
paused
error
```

Không autoplay audio khi mở trang. Chỉ phát khi user bấm loa.

Nếu đang phát item A mà user bấm loa item B:

```text
pause/stop audio A
cancel polling A nếu có
start ensure/play item B
```

---

## 20. Dashboard Staff Leader UI

Tối thiểu:

```text
Trong detail Gallery item hoặc row action:
- Hiển thị trạng thái audio nếu backend trả:
  Chưa tạo / Đang tạo / Sẵn sàng / Lỗi / Cần tạo lại
- Có nút “Tạo lại audio” gọi manual regenerate.
```

Nếu chưa muốn làm UI status phức tạp, tối thiểu có nút trong detail:

```text
[ Tạo lại audio ]
```

Khi create/edit item thành công:

```text
Toast: “Gallery item đã lưu. Giọng đọc sẽ được tạo tự động.”
```

Không bắt Staff Leader chờ audio READY.

---

## 21. API contracts

### 21.1. Public ensure

```http
POST /api/public/gallery-items/{galleryItemId}/tts-audio/ensure
```

Response:

```json
{
  "status": "READY",
  "audioUrl": "/api/files/123/content",
  "voiceCode": "vi_female_hoaian_mb",
  "audioType": "mp3"
}
```

Hoặc:

```json
{
  "status": "PROCESSING",
  "message": "Giọng đọc đang được tạo, vui lòng chờ trong giây lát."
}
```

Hoặc:

```json
{
  "status": "TEMPORARILY_UNAVAILABLE",
  "message": "Chưa thể tạo giọng đọc cho nội dung này. Vui lòng thử lại sau."
}
```

### 21.2. Public poll

```http
GET /api/public/gallery-items/{galleryItemId}/tts-audio
```

Response status:

```text
READY
PROCESSING
NOT_CREATED
TEMPORARILY_UNAVAILABLE
DISABLED
INVALID_DESCRIPTION
```

### 21.3. Management regenerate

```http
POST /api/gallery-management/items/{galleryItemId}/tts-audio/regenerate
```

Response:

```json
{
  "status": "PROCESSING",
  "message": "Giọng đọc đang được tạo lại."
}
```

### 21.4. EverAI callback

```http
POST /api/integrations/everai/tts/callback
```

Return 200 for handled/idempotent/unknown request with warning log.

---

## 22. Error codes

Chuẩn hóa mã lỗi nội bộ:

```text
TTS_DISABLED
TTS_CONFIG_MISSING
TTS_INVALID_DESCRIPTION
TTS_JOB_ALREADY_RUNNING
TTS_RECENT_FAILURE_COOLDOWN
TTS_EVERAI_AUTH_FAILED
TTS_EVERAI_REQUEST_FAILED
TTS_EVERAI_AUDIO_NOT_READY
TTS_EVERAI_AUDIO_EXPIRED
TTS_AUDIO_DOWNLOAD_FAILED
TTS_AUDIO_UPLOAD_FAILED
TTS_AUDIO_NOT_FOUND
```

Mapping:

```text
EverAI status = 0 → FAILED, lưu error_code/error_message.
HTTP 401/403 từ EverAI → TTS_EVERAI_AUTH_FAILED.
audio_expired = true → TTS_EVERAI_AUDIO_EXPIRED.
Download audio_link lỗi → TTS_AUDIO_DOWNLOAD_FAILED.
Upload Google Drive lỗi → TTS_AUDIO_UPLOAD_FAILED.
```

Public không trả stack trace hoặc raw lỗi kỹ thuật.

---

## 23. Rule chống tốn credits

Bắt buộc:

```text
1. Không tạo audio nếu description rỗng hoặc > 1000 ký tự.
2. Không tạo audio nếu EverAiTts.Enabled = false.
3. Không tạo audio nếu thiếu ApiKey.
4. Không tạo audio nếu thiếu GalleryAudioFolderId.
5. Public lazy generate chỉ cho item public-visible.
6. Không tạo audio cho item HIDDEN.
7. Không tạo audio cho area/location/campus INACTIVE.
8. Không tạo trùng job cùng hash/config.
9. Nếu FAILED gần đây, public không được retry liên tục.
10. Staff Leader manual regenerate mới được bypass cooldown.
11. Frontend không gọi EverAI trực tiếp.
```

---

## 24. Test plan

### 24.1. Unit test

```text
Hash service:
- Cùng description/config → cùng hash.
- Đổi description → hash khác.
- Đổi voice_code → hash khác.
- Đổi speed/pitch/volume → hash khác.
- Whitespace normalization hoạt động đúng.

Validation:
- Description rỗng → invalid.
- Description toàn khoảng trắng → invalid.
- Description > 1000 → invalid.
- Default voice = vi_female_hoaian_mb.
- audio_type/bitrate/speed/pitch/volume validate đúng.

Ensure service:
- Có READY đúng hash → trả READY.
- Có running job đúng hash → trả PROCESSING, không tạo row mới.
- FAILED gần đây → TEMPORARILY_UNAVAILABLE.
- Chưa có gì → tạo PENDING.
```

### 24.2. Integration test

```text
- Staff Leader create Gallery item description hợp lệ → tạo TTS PENDING AUTO_GENERATE.
- Staff Leader edit description → tạo hash mới/job mới.
- Public ensure item PUBLISHED chưa audio → tạo LAZY_GENERATE PENDING.
- Public ensure item HIDDEN → không tạo audio.
- Public ensure location INACTIVE → không tạo audio.
- 5 request ensure đồng thời → chỉ tạo 1 running job.
- EverAI create success → lưu everai_request_id, status PROCESSING.
- EverAI polling/callback SUCCESS → download audio → upload Google Drive → status READY.
- EverAI FAILURE → status FAILED.
- Manual regenerate → tạo MANUAL_REGENERATE PENDING.
- Public READY → trả /api/files/{fileId}/content.
```

### 24.3. UI test

```text
Dashboard create/edit:
- Textarea mô tả cao/rộng hơn.
- Counter hiển thị 0/1000.
- Nhập đến 1000 ký tự thì không nhập thêm.
- Paste >1000 ký tự không vượt quá giới hạn.
- Submit description rỗng bị chặn.
- Create/edit thành công thì audio tạo nền.

Public:
- Click loa READY → phát audio.
- Click loa PROCESSING → icon loading + poll.
- Poll READY → phát audio.
- TEMPORARILY_UNAVAILABLE → tooltip/toast nhẹ.
- Chuyển item/location → dừng audio và hủy polling.
```

---

## 25. Thứ tự triển khai khuyến nghị

```text
1. Kiểm tra appsettings.Development.json đã có EverAiTts và GoogleDrive:GalleryAudioFolderId.
2. Thêm EverAiTtsOptions + DI.
3. Thêm GalleryAudioFolderId vào GoogleDriveOptions nếu chưa có.
4. Thêm FilePurpose.GalleryAudio + GALLERY_AUDIO + object key prefix.
5. Sửa GoogleDriveFolderResolver map GalleryAudio → GalleryAudioFolderId.
6. Sửa FileValidationPolicy cho audio mp3/wav.
7. Patch DB: tạo gallery_item_tts_audios + unique running_key.
8. Tạo entity + EF configuration.
9. Tạo hash service.
10. Tạo EverAiTtsClient.
11. Tạo GalleryItemTtsService.
12. Tạo background queue/worker.
13. Tạo callback endpoint.
14. Hook AUTO_GENERATE vào create/update Gallery item.
15. Tạo public POST ensure + GET poll.
16. Tạo management manual regenerate endpoint.
17. Sửa UI create/edit description textarea + counter 0/1000.
18. Gắn public icon loa vào API ensure/poll/play.
19. Thêm dashboard nút Tạo lại audio.
20. Viết unit/integration/UI tests.
21. Manual test full flow.
```

---

## 26. Acceptance criteria

```text
AC-01: Staff Leader tạo Gallery item description hợp lệ → item lưu thành công và có TTS row PENDING AUTO_GENERATE.
AC-02: Staff Leader sửa description → hash đổi và hệ thống tạo job mới nếu chưa có READY/running job đúng hash.
AC-03: Description trong create/edit có textarea lớn hơn và counter 0/1000.
AC-04: Frontend không nhập/paste quá 1000 ký tự.
AC-05: Backend reject description > 1000 bằng HTTP 422.
AC-06: Public click loa item đã có audio READY → phát /api/files/{id}/content.
AC-07: Public click loa item chưa có audio → tạo LAZY_GENERATE PENDING và trả PROCESSING.
AC-08: Public poll đến khi READY → nhận audioUrl và phát được.
AC-09: Public click loa item HIDDEN/location INACTIVE/area INACTIVE/campus INACTIVE → không tạo EverAI job.
AC-10: Nhiều user bấm loa cùng lúc cùng item/hash → chỉ tạo 1 running job.
AC-11: EverAI SUCCESS → backend download audio, upload Drive vào GalleryAudioFolderId, lưu files row, update TTS READY.
AC-12: EverAI FAILURE hoặc upload lỗi → TTS FAILED, lưu error.
AC-13: FAILED gần đây → public không retry liên tục, trả TEMPORARILY_UNAVAILABLE.
AC-14: Staff Leader bấm Tạo lại audio → tạo MANUAL_REGENERATE job và có thể bypass cooldown.
AC-15: Không có endpoint hoặc UI Backfill missing audio.
AC-16: Frontend không gọi EverAI trực tiếp.
AC-17: Không dùng EverAI audio_link làm link phát chính.
AC-18: Không log ApiKey thật.
```

---

## 27. Ghi chú về production

Local/dev có thể dùng `appsettings.Development.json` như user đã cấu hình.

Production nên dùng environment variables cho secret:

```text
EverAiTts__Enabled=true
EverAiTts__ApiKey=<production-everai-api-key>
EverAiTts__DefaultVoiceCode=vi_female_hoaian_mb
GoogleDrive__GalleryAudioFolderId=<production-gallery-audio-folder-id>
```

Nếu hiện tại production vẫn chạy bằng `ASPNETCORE_ENVIRONMENT=Development`, code vẫn đọc được `appsettings.Development.json`, nhưng đây không phải cách an toàn lâu dài. Không commit secret thật.

---

## 28. Chốt cuối

Triển khai theo các quyết định:

```text
1. Voice mặc định: vi_female_hoaian_mb.
2. Text tạo audio: gallery_items.description.
3. Description max 1000 ký tự.
4. UI description create/edit: textarea lớn + counter 0/1000.
5. Auto generate khi Staff Leader tạo/sửa Gallery item.
6. Lazy generate khi public bấm icon loa mà chưa có audio.
7. Manual regenerate cho Staff Leader tạo lại audio 1 item.
8. Bỏ hoàn toàn Backfill missing audio.
9. DB quan hệ: gallery_items 1 - n gallery_item_tts_audios.
10. Public chỉ phát 1 audio hiện hành: READY + đúng hash hiện tại.
11. Hash gồm description + voice/audio setting.
12. Đổi voice/audio setting → audio cũ không còn đúng hash; tạo lại bằng manual regenerate hoặc lazy generate.
13. Audio lưu Google Drive qua FilePurpose.GalleryAudio → GalleryAudioFolderId.
14. Public phát qua /api/files/{audio_file_id}/content.
15. EverAI audio_link chỉ dùng nội bộ để download một lần.
16. Không frontend direct EverAI.
17. Không log/commit ApiKey thật.
```
