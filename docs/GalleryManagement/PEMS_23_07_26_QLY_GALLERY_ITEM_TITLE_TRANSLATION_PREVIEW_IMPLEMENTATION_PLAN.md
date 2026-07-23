# PEMS – Kế hoạch triển khai Translation Preview cho tiêu đề Gallery Item

## 1. Mục tiêu

Hoàn thiện chức năng tiêu đề song ngữ cho Gallery Item của Staff Leader:

1. Modal tạo mới ban đầu chỉ hiển thị **Tiêu đề (VI)**.
2. Bên dưới có nút **Dịch sang EN**.
3. Sau khi dịch, hiển thị **Tiêu đề (EN)** và cho phép chỉnh sửa trước khi lưu.
4. Nếu Tiêu đề (VI) thay đổi sau khi dịch, hiển thị cảnh báo và không lưu bản EN tự động cũ như bản dịch hợp lệ.
5. Modal chi tiết hiển thị tiêu đề EN ngay dưới tiêu đề VI, nhỏ hơn và màu xám.
6. Modal chỉnh sửa hiển thị cả VI/EN, có nút Dịch/Dịch lại và cảnh báo stale.
7. Một phiên bản title VI chỉ gọi Google Translation tối đa một lần.
8. Media, YouTube, area, location, item type, description VI/EN, audio VI/EN và primary media giữ nguyên.

---

# 2. Nguyên tắc request và quota

Để Staff Leader xem rồi sửa EN trước khi lưu, cần hai request tới backend:

| Hành động | PEMS request | Google request |
|---|---:|---:|
| Bấm Dịch | 1 | 1 |
| Bấm Tạo mới/Lưu thay đổi | 1 | 0 |
| Tổng | 2 | 1 |

Điều bắt buộc:

```text
Preview đã thành công
+ title VI không thay đổi
→ submit không được gọi Google lại
```

Frontend gửi lại:

```text
title
titleEn
titleTranslationOrigin
titleTranslationSourceHash
```

Backend kiểm tra hash rồi lưu trực tiếp.

---

# 3. Baseline hiện tại

Database `gallery_items` đã có:

```text
title
title_en
translation_source
translation_status
translation_source_hash
translated_at
```

Hiện frontend create/edit chỉ dùng một field `title`, API multipart chỉ gửi `title`, management detail DTO chỉ có `Title`, và detail modal chỉ render tiêu đề VI.

Không tạo bảng translation mới.

---

# 4. Modal tạo mới

## 4.1. Trạng thái ban đầu

```text
TIÊU ĐỀ (VI) *
[Nhập tiêu đề...]

[Icon Translation] Dịch sang tiếng Anh
```

Chưa render ô EN.

Các phần còn lại giữ nguyên.

## 4.2. Nút Dịch

- Đặt ngay dưới ô VI.
- Icon `Languages` hoặc tương đương.
- Disable khi VI rỗng.
- Disable và hiện spinner khi đang dịch.
- Chặn double-click.
- Không auto-translate theo từng ký tự.
- Không gọi khi blur.
- Chỉ gọi khi Staff Leader bấm nút.

## 4.3. Sau khi dịch

```text
TIÊU ĐỀ (EN) *
[Vietnamese Dragon Statue]

Bản dịch tự động — bạn có thể chỉnh sửa trước khi lưu.
```

Input EN phải editable.

Khi user sửa EN:

```text
titleTranslationOrigin = MANUAL
titleEnglishManuallyEdited = true
titleTranslationState = READY
```

---

# 5. State frontend

Giữ `title` là VI để giảm thay đổi contract, bổ sung:

```text
titleEn
titleTranslationOrigin
titleTranslationState
titleTranslationSourceHash
titleEnglishManuallyEdited
titlePreviewSourceText
isTranslatingTitle
titleTranslationError
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

# 6. Cảnh báo khi VI thay đổi sau preview

Ví dụ đã dịch:

```text
VI = "Tượng rồng Việt Nam"
EN = "Vietnamese Dragon Statue"
```

Sau đó VI đổi thành:

```text
"Tượng cóc Việt Nam"
```

## 6.1. EN chưa sửa tay

Đặt:

```text
titleTranslationState = STALE
```

Hiển thị cảnh báo dưới ô VI:

```text
Tiêu đề tiếng Việt đã thay đổi sau khi dịch. Vui lòng dịch lại để cập nhật tiêu đề tiếng Anh.
```

Có thể giữ EN cũ để tham khảo nhưng phải:

- Có badge `Cần dịch lại`.
- Không submit EN cũ với trạng thái READY.
- Yêu cầu Dịch lại hoặc sửa EN thủ công.

## 6.2. EN đã sửa tay

Hiển thị:

```text
Tiêu đề tiếng Việt đã thay đổi. Hãy kiểm tra lại tiêu đề tiếng Anh trước khi lưu.
```

Sau khi user chỉnh lại EN:

```text
origin = MANUAL
state = READY
```

---

# 7. Translation preview endpoint

Tái sử dụng translation foundation hiện có; không tạo Google provider mới.

Endpoint đề xuất:

```http
POST /api/galleries/preview-translation
```

Create request:

```json
{
  "entityType": "GALLERY_ITEM",
  "field": "TITLE",
  "entityId": null,
  "sourceText": "Tượng rồng Việt Nam"
}
```

Edit request:

```json
{
  "entityType": "GALLERY_ITEM",
  "field": "TITLE",
  "entityId": 123,
  "sourceText": "Tượng rồng Việt Nam"
}
```

Response:

```json
{
  "sourceText": "Tượng rồng Việt Nam",
  "sourceHash": "<sha256>",
  "translatedText": "Vietnamese Dragon Statue",
  "servedFrom": "GOOGLE"
}
```

Quyền:

```text
STAFF
sub_role = LEADER
ACTIVE
đúng campus scope
```

Endpoint không:

- Save DB.
- Update item.
- Upload file.
- Cho anonymous gọi.
- Expose credential.

---

# 8. Tối ưu preview trong edit

Nếu:

```text
input title VI = stored title
stored title_en có dữ liệu
translation_status = READY
stored hash khớp
```

thì preview endpoint trả `title_en` từ DB:

```text
servedFrom = DATABASE
Google calls = 0
```

Có thể bổ sung cache theo:

```text
gallery-title:vi:en:<sourceHash>
```

Nhưng DB vẫn là nguồn chính.

---

# 9. Chống stale response

Dùng `requestIdRef` hoặc `AbortController`.

Tình huống:

```text
Bấm Dịch cho title A
→ đổi VI thành title B
→ response title A trả về
```

Chỉ apply khi `sourceText/sourceHash` vẫn khớp VI hiện tại.

Nếu không:

- Bỏ response.
- Không ghi đè EN.
- Giữ trạng thái STALE/IDLE.

---

# 10. Create payload

Mở rộng type:

```ts
export interface CreateGalleryItemInput {
  title: string;
  titleEn?: string | null;
  titleTranslationOrigin:
    | 'NONE'
    | 'AUTO_PREVIEW'
    | 'MANUAL'
    | 'AUTO_ON_SAVE';
  titleTranslationSourceHash?: string | null;

  descriptionVi: string;
  audioVi: File;
  descriptionEn: string;
  audioEn: File;
  locationId: number;
  itemType: GalleryItemType;
  status: GalleryStatus;
  files: File[];
  youtubeUrls?: string[];
  primaryMediaKey?: string | null;
}
```

Multipart append:

```text
title
titleEn
titleTranslationOrigin
titleTranslationSourceHash
```

Các field cũ giữ nguyên.

---

# 11. Backend create decision

## AUTO_PREVIEW

Nếu EN có dữ liệu và hash khớp:

```text
Không gọi Google
Title = VI
TitleEn = EN
TranslationSource = AUTO
TranslationStatus = READY
TranslationSourceHash = hash
TranslatedAt = now
```

## MANUAL

```text
Không gọi Google
Tự hash VI
TitleEn = EN do Staff Leader nhập
TranslationSource = MANUAL
TranslationStatus = READY
```

## AUTO_ON_SAVE

Nếu không preview và EN rỗng:

- Backend gọi Google một lần trong create.
- Provider lỗi vẫn lưu VI.
- `TitleEn = NULL`.
- `TranslationStatus = FAILED`.

## Preview stale

Nếu origin `AUTO_PREVIEW` nhưng hash không khớp:

```text
GALLERY_TITLE_TRANSLATION_PREVIEW_STALE
```

Không âm thầm gọi Google lần hai.

---

# 12. Modal chi tiết

## Backend DTO

Bổ sung:

```csharp
public string Title { get; init; } = string.Empty;
public string? TitleEn { get; init; }
```

## Detail builder

Select thêm:

```text
i.TitleEn
i.TranslationStatus
```

Chỉ map EN khi:

```text
TranslationStatus = READY
và TitleEn không rỗng
```

## Frontend type

```ts
titleEn?: string | null;
```

## UI

```text
Tượng rồng Việt Nam
Vietnamese Dragon Statue
```

Title VI:

- Giữ style lớn, đậm, màu đen.

Title EN:

- Ngay bên dưới.
- Nhỏ hơn một cấp.
- Màu xám.
- Font medium/semibold.

Ví dụ:

```tsx
<h3 className="text-2xl font-black text-gray-900 leading-tight">
  {detail.title}
</h3>

{detail.titleEn?.trim() && (
  <p className="mt-1 text-base font-semibold text-slate-400 leading-snug">
    {detail.titleEn}
  </p>
)}
```

EN null thì không render dòng rỗng.

---

# 13. Modal chỉnh sửa

Prefill:

```text
title = existing.title
titleEn = existing.titleEn ?? ""
```

Luôn hiển thị:

```text
TIÊU ĐỀ (VI) *
[...]

[Dịch lại sang tiếng Anh]

TIÊU ĐỀ (EN) *
[...]
```

Nếu item cũ thiếu EN, label nút là `Dịch sang tiếng Anh`.

Khi VI khác `existing.title`, hiển thị:

```text
Tiêu đề tiếng Việt đã thay đổi. Vui lòng dịch lại hoặc kiểm tra và cập nhật tiêu đề tiếng Anh.
```

Các phần còn lại giữ nguyên hoàn toàn.

---

# 14. Update payload

```ts
export interface UpdateGalleryItemInput {
  galleryItemId: number;
  title: string;
  titleEn?: string | null;
  titleTranslationOrigin:
    | 'NONE'
    | 'AUTO_PREVIEW'
    | 'MANUAL'
    | 'AUTO_ON_SAVE';
  titleTranslationSourceHash?: string | null;

  descriptionVi: string;
  descriptionEn: string;
  newAudioVi?: File | null;
  newAudioEn?: File | null;
  locationId: number;
  itemType: GalleryItemType;
  keepMediaIds: number[];
  newFiles: File[];
  primaryMediaId?: number | null;
  youtubeUrls?: string[];
  primaryMediaKey?: string | null;
}
```

Multipart bổ sung:

```text
titleEn
titleTranslationOrigin
titleTranslationSourceHash
```

---

# 15. Backend update decision

## VI và EN không đổi

- Không gọi Google.
- Không sửa translation metadata.

Áp dụng khi chỉ sửa media/audio/description/location/item type/primary.

## Chỉ EN đổi

```text
Không gọi Google
TranslationSource = MANUAL
TranslationStatus = READY
Hash = hash VI hiện tại
```

## VI đổi và preview hợp lệ

Lưu trực tiếp, không gọi Google.

## VI đổi, chưa preview

`AUTO_ON_SAVE` cho phép backend gọi Google một lần.

## VI đổi, preview stale

Trả:

```text
GALLERY_TITLE_TRANSLATION_PREVIEW_STALE
```

Không giữ EN cũ là READY.

---

# 16. Validation

Title VI:

- Bắt buộc.
- Trim.
- Không chỉ khoảng trắng.
- Giữ max length hiện hành.

Title EN:

- Nếu gửi phải không rỗng sau Trim.
- Không vượt độ dài cột.
- Không cắt bằng `Substring`.

Manual EN:

- Được phép khác kết quả Google.
- Là nội dung do Staff Leader quyết định.

AUTO_PREVIEW:

- Hash bắt buộc khớp VI hiện tại.

---

# 17. Error handling

Preview lỗi:

```text
Không thể dịch tiêu đề lúc này. Vui lòng thử lại hoặc nhập tiêu đề tiếng Anh thủ công.
```

Yêu cầu:

- Không đóng modal.
- Không mất media/audio đã chọn.
- Giữ VI.
- Giữ EN manual.

Config thiếu:

- Cho nhập EN thủ công.
- Save với `MANUAL`.

Không log:

```text
Credential JSON
Private key
Access token
Authorization header
```

---

# 18. Files dự kiến sửa

Frontend:

```text
GalleryUpsertModal.tsx
GalleryDetailModal.tsx
galleryManagement.types.ts
galleryManagementApi.ts
shared/api/endpoints.ts
```

Backend:

```text
Galleries/Common/GalleryItemDetailDto.cs
Galleries/Common/GalleryDetailBuilder.cs
Galleries/Commands/AddGalleryItem/*
Galleries/Commands/UpdateGalleryItem/*
Galleries/Commands/PreviewGalleryTranslation/*
Galleries/Common/GalleryTranslationCoordinator.cs
Galleries/Common/TranslationSourceHasher.cs
Galleries/Common/TranslationSourceNormalizer.cs
Controllers/GalleriesController.cs
```

Tests:

```text
PEMS.UnitTests/Galleries/*
PEMS.IntegrationTests/Galleries/*
frontend tests tương ứng
```

---

# 19. Test bắt buộc

## Create

- Ban đầu chỉ có VI.
- Dịch làm EN xuất hiện.
- EN editable.
- Preview + save chỉ một Google call.
- Manual EN lưu source MANUAL.
- Không preview thì AUTO_ON_SAVE gọi Google một lần.
- VI đổi sau preview tạo cảnh báo/stale.

## Detail

- VI lớn.
- EN nhỏ, xám, bên dưới.
- EN null không render.

## Edit

- Load VI + EN.
- Có Dịch/Dịch lại.
- Thay media/audio không gọi Google.
- Chỉ sửa EN không gọi Google, source MANUAL.
- VI đổi + preview: preview 1 call, save 0 call.
- Bấm Dịch lại khi source không đổi và hash hợp lệ: trả DB, 0 Google call.

---

# 20. Acceptance criteria

Chỉ hoàn thành khi:

- Create ban đầu chỉ hiện title VI.
- Nút dịch hoạt động.
- EN xuất hiện và sửa được.
- VI đổi sau preview có cảnh báo.
- Không lưu auto EN stale.
- Detail hiện VI + EN đúng style.
- Edit load title EN hiện có.
- Edit có nút Dịch/Dịch lại.
- Save không gọi Google lần hai sau preview.
- Không đổi các phần media, area, location, item type, description và audio.
- Không regress Public Gallery.
- Không tăng quota do update không liên quan.

---

# 21. Thứ tự triển khai

## Phase 1

- DTO/contract `TitleEn`.
- Origin/hash.
- Detail DTO.

## Phase 2

- Preview endpoint.
- Scope.
- Hash.
- DB/cache reuse.

## Phase 3

- Create/update handler.
- Stale validation.
- Logging/audit.

## Phase 4

- Frontend create/edit state và UI.
- Multipart payload.
- Cảnh báo stale/manual.

## Phase 5

- Detail modal hiển thị EN.

## Phase 6

- Unit, integration, frontend và real-stack validation.

---

# 22. Hướng dẫn AI Agent

1. Đọc codebase trước khi sửa.
2. Xác nhận branch đang làm việc.
3. Không sửa `Dev` nếu không được yêu cầu.
4. Tìm toàn bộ consumer của `GalleryItemDetail` và create/update contracts.
5. Không sửa ngoài scope.
6. Không chạy destructive SQL.
7. Chạy test sau từng phase.
8. Báo cáo file đã sửa, contract đã đổi và test đã chạy.
9. Gom commit theo logic.
10. Không dùng tên AI/Claude/Agent trong commit message.

Commit gợi ý:

```text
feat(gallery): preview and persist translated item titles
feat(gallery): show bilingual titles in item details
test(gallery): cover title translation preview workflows
```
