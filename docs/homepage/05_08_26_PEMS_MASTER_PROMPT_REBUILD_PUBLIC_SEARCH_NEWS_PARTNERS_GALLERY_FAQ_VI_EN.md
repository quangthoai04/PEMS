# PEMS — MASTER IMPLEMENTATION PROMPT  
## Rebuild Public Homepage Search: News, Partners, Gallery, FAQ, Deep Link, VI/EN

> **Mục đích:** Dùng nguyên file này làm prompt cho AI Agent đọc repository PEMS, đối chiếu nhánh `Dev`, sau đó triển khai trên **nhánh hiện tại đang checkout**.  
> **Phạm vi:** Chỉ làm lại chức năng Search trên public homepage và các deep-link cần thiết để mở đúng nội dung tìm được.

---

# 1. Vai trò của AI Agent

Bạn là **Senior Full-stack Engineer** cho dự án PEMS:

- Backend: .NET / MediatR / EF Core / MySQL.
- Frontend: React / TypeScript / Vite / Tailwind.
- Kiến trúc: Clean Architecture hiện tại của repository.
- Yêu cầu ưu tiên:
  - đúng logic hiện có;
  - ít thay đổi nhất;
  - không over-engineer;
  - không tự đổi schema, API hoặc architecture ngoài phạm vi;
  - production-ready;
  - có test và bằng chứng build.

---

# 2. Quy tắc branch bắt buộc

## 2.1. Đọc `Dev`, code trên nhánh hiện tại

Trước khi sửa:

```bash
git status --short
git branch --show-current
git rev-parse HEAD
git rev-parse Dev
git log --oneline -10
```

Bắt buộc:

1. Dùng nhánh `Dev` làm nguồn tham khảo để đọc logic mới nhất.
2. Code trên **nhánh hiện tại đang checkout**.
3. Không checkout sang `Dev`.
4. Không sửa, commit hoặc push trực tiếp lên `Dev`.
5. Không tự tạo branch mới.
6. Không reset, clean, stash, discard hoặc ghi đè WIP hiện có.
7. Không tự merge/rebase `Dev` vào nhánh hiện tại.
8. Có thể dùng các lệnh đọc an toàn:

```bash
git show Dev:<path>
git diff Dev...HEAD -- <path>
git log Dev -- <path>
git grep <keyword> Dev
```

9. Trước khi sửa file, phải so sánh bản trên `Dev` với bản trên nhánh hiện tại để không làm mất thay đổi đã có.
10. Nếu phát hiện conflict logic giữa `Dev` và nhánh hiện tại:
    - ưu tiên giữ hành vi mới/hợp lệ trên nhánh hiện tại;
    - chỉ lấy phần cần thiết từ `Dev`;
    - không ghi đè nguyên file một cách mù quáng.

Nếu nhánh hiện tại chính là `Dev`, không tự ý tạo/switch branch; báo rõ tình trạng trước khi mutation.

---

# 3. Mục tiêu cuối cùng

Làm lại toàn bộ Public Search để:

```text
Search đúng 4 nhóm:
- Tin tức
- Đối tác
- Gallery
- FAQ
```

Yêu cầu:

1. Chỉ trả dữ liệu public.
2. Tìm theo đúng ngôn ngữ public hiện tại:
   - giao diện VI → tìm nội dung VI;
   - giao diện EN → tìm nội dung EN.
3. Không fallback nội dung tiếng Việt trong kết quả English.
4. Click kết quả phải mở đúng nội dung cụ thể.
5. Thiết kế lại Search Popup hiện đại, gọn, responsive.
6. Bỏ “Gợi ý tìm kiếm phổ biến”.
7. Bỏ Campus khỏi phạm vi search.
8. Bỏ phần địa chỉ/hotline/email/social khỏi Search Popup.
9. Sửa lỗi “Xem thêm đối tác liên quan” luôn xuất hiện.
10. Không thêm bảng, không migration và không sửa canonical SQL cho task này.

---

# 4. Hiện trạng phải audit trước khi code

Tìm và đọc kỹ các file thật trên `Dev` và nhánh hiện tại. Đường dẫn dự kiến:

## Frontend

```text
frontend/pems-react/src/components/modals/SearchPopup.tsx
frontend/pems-react/src/components/layout/Header.tsx
frontend/pems-react/src/features/public-search/api/publicSearchApi.ts
frontend/pems-react/src/features/public-search/types/publicSearch.types.ts
frontend/pems-react/src/pages/FAQPage.tsx
frontend/pems-react/src/features/public-faq/api/publicFaqApi.ts
frontend/pems-react/src/features/public-faq/types/publicFaq.types.ts
frontend/pems-react/src/pages/CampusDetailVisitPage.tsx
frontend/pems-react/src/features/visit-fptu/publicVisitFptuApi.ts
frontend/pems-react/src/features/visit-fptu/publicVisitFptu.types.ts
frontend/pems-react/src/pages/NewsDetailPage.tsx
frontend/pems-react/src/pages/PartnerDetailPage.tsx
frontend/pems-react/src/App.tsx
frontend/pems-react/src/shared/api/endpoints.ts
```

Tìm file translation thực tế cho namespace `search`, `faq`, `visitFptu`, `news`, `partners`.

## Backend

```text
backend/PEMS.Api/Controllers/PublicContentController.cs
backend/PEMS.Api/Controllers/FaqsController.cs
backend/PEMS.Api/Controllers/PublicVisitFptuController.cs

backend/PEMS.Application/PublicContent/Queries/SearchInformation/
backend/PEMS.Application/Galleries/Public/
backend/PEMS.Application/Faqs/
backend/PEMS.Application/News/
backend/PEMS.Application/Partners/
```

Đặc biệt kiểm tra:

```text
SearchInformationQuery.cs
SearchInformationQueryValidator.cs
SearchInformationQueryHandler.cs
SearchInformationDto.cs
```

## Tests

Tìm:

```text
tests/**/SearchInformationQueryTests.cs
tests/**/*PublicSearch*
tests/**/*Faq*
tests/**/*Gallery*
frontend/pems-react/src/**/*.test.*
frontend/pems-react/src/**/*.spec.*
```

Hiện tại có khả năng `SearchInformationQueryTests` đang bị `Skip` và chỉ là TODO. Phải xác nhận trên code thật và thay bằng test thật nếu đúng.

---

# 5. Luồng chức năng mới

```text
Người dùng mở Search Popup
→ input tự focus
→ người dùng nhập keyword
→ debounce khoảng 350 ms
→ FE gửi keyword + languageCode + limit
→ BE tìm trong News, Partner, Gallery, FAQ
→ BE lọc public visibility
→ BE xếp hạng theo độ liên quan
→ BE trả kết quả từng nhóm + hasMore
→ FE hiển thị theo từng section
→ click result
→ đóng popup
→ mở đúng nội dung chi tiết
```

Giữ endpoint search hiện tại nếu phù hợp:

```http
GET /api/public/search
```

Request:

```text
keyword
languageCode
limit
```

Không tạo trang Search riêng trong task này.

---

# 6. Phạm vi search mới

## Giữ

```text
News
Partners
Gallery
FAQ
```

## Loại khỏi Search Popup

```text
Campuses
Popular search suggestions
Campus contact footer
Hotline
Email
Social links
```

Search Popup không phải trang Contact.

---

# 7. Contract API mục tiêu

## 7.1. Response

```ts
export interface SearchInformationResult {
  news: SearchNewsResult[];
  partners: SearchPartnerResult[];
  galleries: SearchGalleryResult[];
  faqs: SearchFaqResult[];

  hasMore: {
    news: boolean;
    partners: boolean;
    galleries: boolean;
    faqs: boolean;
  };

  totalCount: number;
}
```

Xóa khỏi contract:

```text
campuses
SearchCampusResult
SearchCampusResultDto
```

Thêm:

```text
galleries
SearchGalleryResult
SearchGalleryResultDto
hasMore
```

## 7.2. News result

```ts
export interface SearchNewsResult {
  newsId: number;
  title: string;
  summary?: string | null;
  publishedAt?: string | null;
}
```

## 7.3. Partner result

```ts
export interface SearchPartnerResult {
  partnerId: number;
  name: string;
  descriptionPreview?: string | null;
  country?: string | null;
  publicSlug?: string | null;
}
```

## 7.4. FAQ result

```ts
export interface SearchFaqResult {
  faqId: number;
  question: string;
  answerPreview?: string | null;
  faqType: string;
  faqTypeLabel: string;
}
```

## 7.5. Gallery result

```ts
export interface SearchGalleryResult {
  galleryItemId: number;

  title: string;
  descriptionPreview?: string | null;

  campusCode: string;
  campusName: string;

  areaId: number;
  areaName: string;

  locationId: number;
  locationName: string;

  mediaKind: string;
  thumbnailUrl?: string | null;
}
```

Các field sau bắt buộc để deep-link đúng:

```text
campusCode
locationId
galleryItemId
```

---

# 8. Language rule bắt buộc

## 8.1. Chuẩn hóa ngôn ngữ frontend

Không gửi trực tiếp:

```text
en-US
en-GB
vi-VN
```

Chuẩn hóa:

```ts
export type PublicSearchLanguage = 'vi' | 'en';

export function normalizePublicSearchLanguage(
  language?: string,
): PublicSearchLanguage {
  return language?.toLowerCase().startsWith('en') ? 'en' : 'vi';
}
```

Nguồn:

```ts
i18n.resolvedLanguage ?? i18n.language
```

Request:

```ts
publicSearchApi.search({
  keyword,
  limit: 5,
  languageCode,
});
```

## 8.2. Backend validation

Chỉ chấp nhận:

```text
vi
en
```

Kiểm tra convention hiện tại của project:

- nếu validator đang dùng strict input → trả validation error;
- nếu hệ thống đang normalize default → fallback `vi`.

Không tự tạo behavior khác convention hiện tại.

---

# 9. Search tiếng Việt

Khi `languageCode = vi`, tìm và hiển thị nội dung VI.

## News

```text
title VI
summary VI
```

## Partner

```text
name VI
shortName VI
description VI
country VI
```

Có thể fallback về cột legacy/gốc tiếng Việt nếu translation VI thiếu, nếu đó đúng convention public Partner hiện tại.

## FAQ

```text
question VI
answer VI
FAQ type label VI
```

Có thể fallback về dữ liệu gốc VI nếu đúng convention hiện tại.

## Gallery

```text
title VI
description VI
areaName VI
locationName VI
campus name/code/city
```

---

# 10. Search tiếng Anh

Khi `languageCode = en`, chỉ tìm và hiển thị nội dung EN.

## News

```text
NewsTranslation.LanguageCode = "en"
title EN
summary EN
```

## Partner

```text
PartnerTranslation.LanguageCode = "en"
name EN
shortName EN
description EN
country EN
```

## FAQ

```text
FaqTranslation.LanguageCode = "en"
question EN
answer EN
FAQ type label EN
```

## Gallery

```text
titleEn
descriptionEn
areaNameEn
locationNameEn
campus code và dữ liệu trung lập phù hợp
```

## Quy tắc nghiêm ngặt

```text
Không có translation EN hợp lệ
→ không xuất hiện trong English search
```

Không được:

```text
Match nội dung VI rồi hiển thị EN
Fallback EN sang VI
Hiển thị title VI trong popup English
```

Không gọi translation API trong lúc search. Chỉ đọc bản dịch đã lưu trong database.

---

# 11. Sửa News search

Audit logic hiện tại. Nếu đang dùng:

```csharp
_db.NewsTranslations.Any(t =>
    t.NewsId == n.NewsId &&
    ContainsKeyword(t))
```

mà không lọc `LanguageCode`, phải sửa.

Logic mục tiêu:

```csharp
.Where(n => n.Status == Published)
.Where(n => _db.NewsTranslations.Any(t =>
    t.NewsId == n.NewsId &&
    t.LanguageCode == requestedLang &&
    (
        EF.Functions.Like(t.Title, pattern) ||
        (t.Summary != null && EF.Functions.Like(t.Summary, pattern))
    )))
```

Projection cũng chỉ lấy translation đúng `requestedLang`.

Không search trên mọi translation rồi chọn language để hiển thị sau.

---

# 12. Sửa Partner search

## Visibility

Chỉ trả:

```text
ProfileStatus = APPROVED
Visibility = PUBLIC
```

## Fields

```text
name
shortName
description
country
```

## Language

```text
VI:
- ưu tiên translation VI;
- fallback cột gốc VI nếu đúng behavior hiện tại.

EN:
- bắt buộc translation EN;
- không fallback VI.
```

## Performance

Không tải toàn bộ Partner public về memory rồi mới `.Contains()`.

Đẩy tối đa các bước sau xuống database:

```text
visibility filter
language filter
keyword match
ranking
Take(limit + 1)
```

Không đổi architecture nếu EF query hiện tại có helper/repository phù hợp; tái sử dụng code hiện có.

---

# 13. Sửa FAQ search

## Visibility

Chỉ trả:

```text
Status = PUBLISHED
```

## Fields

```text
question
answer
faqType label
```

## Language

```text
VI:
- translation VI hoặc dữ liệu gốc VI theo convention hiện tại.

EN:
- bắt buộc translation EN;
- không fallback VI.
```

## Performance

Không tải toàn bộ FAQ rồi lọc trong memory.

Đẩy lọc `status + language + keyword + Take()` xuống database.

---

# 14. Thêm Gallery search

## 14.1. Public visibility chain

Chỉ trả item khi toàn bộ chain hợp lệ:

```text
GalleryItem.Status = PUBLISHED
GalleryItem.DeletedAt = NULL

Location.Status = ACTIVE
Area.Status = ACTIVE
Campus.Status = ACTIVE

Có ít nhất một GalleryItemMedia:
- Status = ACTIVE
- DeletedAt = NULL
```

Phải đồng nhất với Public Gallery Detail API để tránh:

```text
Search thấy item
→ click
→ detail trả 404
```

Tái sử dụng visibility rule/helper hiện có nếu có.

## 14.2. Fields VI

```text
item.Title
content.DescriptionVi
location.LocationName
area.AreaName
campus.Name
campus.CampusCode
campus.City
```

## 14.3. Fields EN

```text
item.TitleEn
content.DescriptionEn
location.LocationNameEn
area.AreaNameEn
campus.CampusCode
các campus metadata trung lập phù hợp
```

Khi search field EN, kiểm tra trạng thái translation tương ứng nếu model đang có:

```text
item.TranslationStatus = READY
location.TranslationStatus = READY
area.TranslationStatus = READY
```

Không dùng bản dịch chưa READY.

## 14.4. Thumbnail

Trả thumbnail public của primary media:

```text
thumbnail URL
hoặc primary image public URL
```

Không trả URL file nội bộ yêu cầu đăng nhập.

Tái sử dụng factory/helper URL từ Public Gallery hiện tại.

---

# 15. Relevance ranking

Không chỉ sort theo ngày/tên/displayOrder.

Đề xuất score:

```text
100 — title/name/question khớp hoàn toàn
80  — title/name/question bắt đầu bằng keyword
60  — title/name/question chứa keyword
40  — summary/description/answer chứa keyword
20  — country/type/area/location/campus chứa keyword
```

Tie-break:

```text
News:
- PublishedAt mới nhất

Partner:
- Name tăng dần

FAQ:
- DisplayOrder tăng dần

Gallery:
- DisplayOrder hoặc GalleryItemId theo convention hiện tại
```

Không thêm Elasticsearch, search server, cache layer hoặc bảng index mới.

Nếu EF/MySQL không hỗ trợ một biểu thức ranking trực tiếp, dùng giải pháp đơn giản, có giới hạn nhỏ, không kéo toàn bộ bảng về memory.

---

# 16. Limit và hasMore

Mỗi nhóm query độc lập với:

```text
Take(limit + 1)
```

Ví dụ `limit = 5`:

```text
0–5 rows:
hasMore = false

6 rows:
hasMore = true
response chỉ trả 5 rows
```

Không cần `COUNT(*)` riêng cho từng nhóm nếu không có yêu cầu khác.

Phải chốt rõ ý nghĩa `totalCount`:

- ưu tiên giữ semantics tương thích hiện tại nếu frontend đang dùng;
- nếu đổi thành tổng số result được trả trong popup, document bằng test;
- không giả vờ đây là tổng toàn DB nếu không chạy count thật.

---

# 17. Fix nút “Xem thêm đối tác liên quan”

Hiện lỗi có thể do CTA nằm ở footer chung của toàn result container.

Phải:

1. Di chuyển CTA vào section Partner.
2. Chỉ hiển thị khi:

```ts
result.partners.length > 0 &&
result.hasMore.partners
```

3. Route:

```text
/partners?search={encodedKeyword}
```

Case bắt buộc:

```text
0 Partner
→ không hiện

3 Partner, limit 5
→ không hiện

5 Partner, tổng đúng 5
→ không hiện

5 Partner, còn kết quả
→ hiện

Chỉ có News/Gallery/FAQ
→ không hiện
```

Kiểm tra `PartnersPage` có thực sự đọc query param `search`. Nếu chưa có:

- bổ sung tối thiểu để hydrate ô search/filter hiện có;
- không làm lại toàn trang Partner;
- không đưa người dùng sang URL có param nhưng page bỏ qua.

---

# 18. Deep link News

Route mục tiêu:

```text
/news/{newsId}
```

Kiểm tra:

1. Route hiện có.
2. News Detail fetch đúng ID.
3. Dùng global public language.
4. Không cần tạo route mới nếu route hiện tại đã đúng.

---

# 19. Deep link Partner

Route mục tiêu:

```text
/partners/{publicSlug}
```

Fallback:

```text
/partners/{partnerId}
```

Kiểm tra Partner Detail hiện chấp nhận slug hay chỉ ID.

Không tự dùng slug nếu API/page không hỗ trợ. Giữ mapping tương thích:

```ts
`/partners/${publicSlug || partnerId}`
```

Trang đích phải hiển thị đúng ngôn ngữ global hiện tại.

---

# 20. Deep link FAQ

## 20.1. URL

```text
/faq?faqId={faqId}
```

## 20.2. Backend

Ưu tiên thêm hoặc tái sử dụng endpoint public lấy một FAQ:

```http
GET /api/public/faqs/{faqId}?languageCode=vi|en
```

Chỉ trả khi:

```text
FAQ status = PUBLISHED
Có content đúng language
```

English không fallback VI.

Hidden/not found/không có EN:

```text
404
```

Controller chỉ gọi MediatR; business logic nằm trong query handler.

## 20.3. Frontend FAQPage

Khi có `faqId`:

1. Đọc bằng `useSearchParams`.
2. Fetch đúng FAQ public theo current language.
3. Set đúng FAQ type/filter.
4. Bảo đảm FAQ xuất hiện dù đang nằm ở page pagination khác.
5. Set `openFaqId`.
6. Gắn DOM ID:

```tsx
id={`faq-${faq.faqId}`}
```

7. Sau render:

```ts
document
  .getElementById(`faq-${faqId}`)
  ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
```

8. Khi user tự:
   - đổi category;
   - nhập search;
   - chuyển page;
   - clear filter;

   thì xóa `faqId` khỏi URL để không tự mở lại.

## 20.4. Error

Nếu FAQ không còn public:

```text
Không crash page
Hiện thông báo phù hợp
Trang FAQ vẫn sử dụng bình thường
```

---

# 21. Deep link Gallery

## 21.1. URL

```text
/visit-fptu/{campusCode}?locationId={locationId}&itemId={galleryItemId}
```

Ví dụ:

```text
/visit-fptu/hn?locationId=12&itemId=88
```

## 21.2. Khi load/reload

`CampusDetailVisitPage` phải:

1. Đọc `campusCode` từ route.
2. Tải navigation campus.
3. Xác nhận `locationId` thuộc campus.
4. Mở đúng Location Showcase hoặc location view hiện hành.
5. Đọc `itemId`.
6. Fetch Gallery Item Detail.
7. Xác nhận item thuộc đúng campus/location.
8. Mở ngay item detail modal.
9. Hiển thị title/content theo global language.

## 21.3. URL synchronization

Khi mở item từ UI:

```text
set itemId
giữ locationId
```

Khi đóng item:

```text
xóa itemId
giữ locationId
```

Khi đóng location/showcase:

```text
xóa locationId
xóa itemId
```

Khi chuyển item prev/next trong modal:

```text
cập nhật itemId tương ứng
```

## 21.4. Invalid item

Nếu item bị hidden/deleted sau khi search:

```text
Không crash
Không mở modal rỗng
Giữ location nếu vẫn hợp lệ
Hiện thông báo content không còn public
```

Không cho item thuộc campus/location khác được mở bằng cách sửa query string.

---

# 22. Redesign SearchPopup

File chính dự kiến:

```text
frontend/pems-react/src/components/modals/SearchPopup.tsx
```

## 22.1. Xóa hoàn toàn suggestion

Xóa:

```text
VISIT_RELATED_FAQ_TYPES
suggestions state
handleChipClick
effect tải campuses
effect tải partner types
effect tải FAQ type counts
imports chỉ dùng cho suggestions
khối “Gợi ý tìm kiếm phổ biến”
```

Mở popup không được gọi API suggestion.

## 22.2. Xóa contact footer

Xóa:

```text
CAMPUS_CONTACTS
translatedContacts
địa chỉ 5 campus
hotline
email
social links
contact footer area
```

## 22.3. Xóa Campus result

Xóa:

```text
Campus DTO/type
Campuses result section
MapPin import nếu không còn dùng
```

## 22.4. Thêm Gallery section

Mỗi result hiển thị:

```text
thumbnail
title
campus
area
location
media kind
arrow/icon mở detail
```

Click:

```ts
goTo(
  `/visit-fptu/${campusCode.toLowerCase()}?locationId=${locationId}&itemId=${galleryItemId}`,
);
```

---

# 23. UI mục tiêu

## Desktop

```text
┌────────────────────────────────────────────┐
│ Tìm kiếm nội dung                      [X] │
│ [🔍 Nhập từ khóa tìm kiếm...        ][×] │
├────────────────────────────────────────────┤
│ 12 kết quả cho “FPT”                      │
│                                            │
│ TIN TỨC                                    │
│ Tiêu đề                          Ngày đăng │
│ Summary...                                 │
│                                            │
│ ĐỐI TÁC                                    │
│ Tên đối tác                     Quốc gia   │
│ Description...                  Xem thêm → │
│                                            │
│ GALLERY                                    │
│ [thumb] Title                              │
│         Campus · Area · Location           │
│                                            │
│ FAQ                                        │
│ Câu hỏi                         Nhóm FAQ    │
└────────────────────────────────────────────┘
```

## Style

Dùng design system hiện tại của PEMS:

```text
Primary navy: #004c91
Accent orange: #f37021
Text: slate-800 / slate-900
Secondary text: slate-500 / slate-600
Border: slate-200
Surface: white / slate-50
```

Popup:

```text
max-w-4xl hoặc max-w-5xl
max-height 80–85vh
header cố định
input cố định
chỉ result area scroll
rounded-2xl
border slate-200
shadow vừa phải
```

Không:

```text
gradient mạnh
shadow quá đậm
card lồng card nhiều tầng
animation thừa
modal 1200px kèm footer dài
```

## Responsive

Mobile:

```text
gần full-screen
padding hợp lý
metadata wrap
thumbnail nhỏ gọn
không horizontal scroll
close button luôn truy cập được
input không bị zoom/cắt
```

---

# 24. UI states

## Initial

VI:

```text
Nhập từ khóa để tìm trong Tin tức, Đối tác, Gallery và FAQ.
```

EN:

```text
Enter a keyword to search News, Partners, Gallery and FAQs.
```

Không suggestions.

## Loading

Dùng skeleton 3–4 rows.

Không chỉ spinner giữa vùng trắng lớn.

## Empty

VI:

```text
Không tìm thấy nội dung phù hợp với “{keyword}”.
Hãy thử từ khóa ngắn hơn hoặc cách viết khác.
```

EN:

```text
No results found for “{keyword}”.
Try a shorter or different keyword.
```

## Error

VI:

```text
Không thể tìm kiếm lúc này.
[Thử lại]
```

EN:

```text
Search is temporarily unavailable.
[Try again]
```

## Empty keyword

```text
Không gọi API
Clear result/error
Disable search action nếu cần
```

Thêm clear button trong input.

---

# 25. Debounce, retry và stale response

## Debounce

Khoảng:

```text
350 ms
```

Giữ theo code hiện tại nếu đang là 350 ms.

Search button/Enter có thể trigger ngay.

## Stale-response guard

Case:

```text
Request VI đang chạy
→ user đổi EN
→ request EN chạy
→ response VI về muộn
```

Không cho response VI ghi đè EN.

Dùng một trong:

```text
AbortController
request sequence ID
effect cancellation token
```

Request identity phải gồm:

```text
keyword
languageCode
```

Khi đổi language:

```text
clear result VI ngay
clear error
set loading
refetch cùng keyword với language mới
```

Không giữ result cũ trong lúc chờ.

---

# 26. Date formatting

Không dùng formatter Việt Nam cố định cho English.

Dùng:

```ts
new Intl.DateTimeFormat(locale, options)
```

Ví dụ:

```text
VI: 05/08/2026
EN: Aug 5, 2026
```

Locale lấy từ normalized language/current i18n.

---

# 27. Keyword highlight

Có thể highlight trong:

```text
title
name
question
summary/description preview
```

Yêu cầu:

```text
render bằng React nodes
escape regex special characters
không dùng dangerouslySetInnerHTML
```

Không bắt buộc nếu làm tăng scope/rủi ro; ưu tiên core flow trước.

---

# 28. Translation keys

Tìm đúng thư mục locale hiện tại và cập nhật VI/EN.

Cần các key tương đương:

```text
title
placeholder
initialHint
resultCount
news
partners
gallery
faq
viewMore
noResult
noResultHint
error
retry
clear
searchAria
closeAria
```

Xóa key không còn dùng nếu không được reference:

```text
popularSuggestions
campuses
contact labels trong Search Popup
```

Không xóa key đang dùng ở page khác.

---

# 29. Database rule

Task này:

```text
Không thêm bảng
Không thêm cột
Không migration
Không sửa fresh-create SQL
Không sửa canonical SQL hash
Không seed schema mới
```

Chỉ dùng bảng/translation hiện có.

Index chỉ được thêm nếu:

1. đo query thật;
2. chứng minh performance chưa đạt;
3. user cho phép database change.

Mặc định không thêm index trong task này.

---

# 30. Input, validation, output

## Input

```text
keyword
languageCode
limit
```

## Validation

```text
keyword trim
keyword max length theo convention hiện tại
languageCode: vi | en
limit: 1..20 hoặc giới hạn hiện tại
```

## Business logic

```text
public visibility
language strictness
relevance ranking
limit + 1
hasMore
```

## Output

```text
News
Partners
Gallery
FAQ
hasMore
totalCount
```

## Error cases

```text
empty keyword
invalid language
invalid limit
database/API failure
content hidden between search and click
invalid deep-link IDs
```

---

# 31. Files dự kiến thay đổi

Chỉ sửa file thực sự cần thiết sau khi audit.

## Backend

```text
backend/PEMS.Application/PublicContent/Queries/SearchInformation/SearchInformationDto.cs
backend/PEMS.Application/PublicContent/Queries/SearchInformation/SearchInformationQuery.cs
backend/PEMS.Application/PublicContent/Queries/SearchInformation/SearchInformationQueryValidator.cs
backend/PEMS.Application/PublicContent/Queries/SearchInformation/SearchInformationQueryHandler.cs

backend/PEMS.Application/Faqs/.../GetPublicFaqDetail/*
backend/PEMS.Api/Controllers/PublicContentController.cs
hoặc controller public FAQ thực tế

tests/.../SearchInformationQueryTests.cs
tests liên quan FAQ/Gallery deep link API
```

Có thể cần helper dùng lại Public Gallery media URL.

## Frontend

```text
frontend/pems-react/src/components/modals/SearchPopup.tsx
frontend/pems-react/src/features/public-search/types/publicSearch.types.ts
frontend/pems-react/src/features/public-search/api/publicSearchApi.ts
frontend/pems-react/src/features/public-faq/api/publicFaqApi.ts
frontend/pems-react/src/pages/FAQPage.tsx
frontend/pems-react/src/pages/CampusDetailVisitPage.tsx
frontend/pems-react/src/pages/PartnersPage.tsx
frontend/pems-react/src/shared/api/endpoints.ts
translation files VI/EN
frontend tests liên quan
```

Không sửa nguyên file nếu chỉ cần patch nhỏ.

---

# 32. Backend tests bắt buộc

## Visibility

- News Draft không xuất hiện.
- Partner chưa Approved không xuất hiện.
- Partner Private không xuất hiện.
- FAQ Hidden không xuất hiện.
- Gallery Draft không xuất hiện.
- Gallery deleted không xuất hiện.
- Location inactive không xuất hiện.
- Area inactive không xuất hiện.
- Campus inactive không xuất hiện.
- Gallery không có active media không xuất hiện.

## Language

- VI keyword tìm được VI result.
- VI keyword không match khi language EN.
- EN keyword tìm được EN result.
- News thiếu EN không xuất hiện trong EN search.
- Partner thiếu EN không xuất hiện trong EN search.
- FAQ thiếu EN không xuất hiện trong EN search.
- Gallery translation chưa READY không xuất hiện trong EN search.
- Response EN không chứa text VI fallback.

## Ranking

- Exact title/name/question đứng trên description match.
- Starts-with đứng trên contains.
- Primary field match đứng trên metadata match.

## Limit

- Limit độc lập theo section.
- `hasMore` chính xác.
- `totalCount` đúng semantics đã chốt.

## Deep link/API

- FAQ detail chỉ trả Published.
- FAQ detail EN thiếu translation trả 404.
- Gallery detail hidden trả 404.
- Public endpoints không leak internal/admin fields.

Bỏ `[Fact(Skip = ...)]` của Search test nếu hiện đang skip.

---

# 33. Frontend tests bắt buộc

- Mở popup không gọi API suggestions.
- Không còn “Gợi ý tìm kiếm phổ biến”.
- Không còn Campus section.
- Không còn contact footer.
- Empty keyword không gọi API.
- Debounce hoạt động.
- Search button/Enter hoạt động.
- Clear input xóa result.
- Loading skeleton hiển thị.
- Empty state đúng VI/EN.
- Error + retry hoạt động.
- Click News đúng URL.
- Click Partner đúng URL.
- Click FAQ mở đúng accordion.
- Reload FAQ URL vẫn mở đúng item.
- Click Gallery đúng campus/location/item.
- Reload Gallery URL vẫn mở đúng item.
- Đóng Gallery item xóa `itemId`.
- Partner CTA chỉ hiện khi `hasMore.partners`.
- Chuyển VI → EN không bị stale response.
- Popup EN không lẫn nhãn VI.
- Mobile không overflow.
- Escape đóng popup.
- Focus input khi mở.
- Close button có `aria-label`.
- TypeScript không lỗi.

---

# 34. Build và gates

Phải chạy theo command thật của repo.

Tối thiểu:

```bash
dotnet build
dotnet test <unit-test-project>
dotnet test <relevant-integration-test-project>

cd frontend/pems-react
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

Nếu script khác, đọc `package.json` và solution/project files để dùng đúng lệnh.

Không báo green nếu chưa chạy.

Nếu test baseline có lỗi pre-existing:

1. chứng minh lỗi tồn tại trước thay đổi;
2. chạy test target mới;
3. báo rõ số lượng và file lỗi;
4. không tự sửa lỗi ngoài scope trừ khi nó chặn trực tiếp task này.

---

# 35. Thứ tự triển khai

## Giai đoạn 0 — Preflight

```text
branch
HEAD
Dev HEAD
working tree
WIP
baseline build/test liên quan
diff Dev...HEAD
```

## Giai đoạn 1 — Audit

```text
current SearchPopup
current API contract
current query handler
translation model
Gallery visibility
FAQ/Partner/News routes
deep-link behavior
existing tests
```

## Giai đoạn 2 — Backend contract

```text
remove Campus
add Gallery
add hasMore
validate language
```

## Giai đoạn 3 — Backend search logic

```text
News language fix
Partner DB query
FAQ DB query
Gallery search
ranking
limit + 1
tests
```

## Giai đoạn 4 — Deep links

```text
FAQ exact item
Gallery exact item
Partners search query param
verify News/Partner details
```

## Giai đoạn 5 — Popup redesign

```text
remove suggestions
remove contacts
remove Campus
add Gallery section
fix Partner CTA
states
responsive
accessibility
```

## Giai đoạn 6 — VI/EN and race safety

```text
normalize language
strict EN search
refetch on language switch
stale guard
locale date
destination language
```

## Giai đoạn 7 — Gates

```text
backend build
backend tests
frontend typecheck
frontend lint
frontend tests
frontend build
manual VI
manual EN
manual mobile
```

---

# 36. Manual test data matrix

Dùng dữ liệu thật/seed hiện có hoặc tạo fixture test cô lập.

| Case | VI content | EN content | Expected VI | Expected EN |
|---|---:|---:|---:|---:|
| News A | Có | Có | Hiện | Hiện |
| News B | Có | Không | Hiện | Không |
| Partner A | Có | Có | Hiện | Hiện |
| Partner B | Có | Không | Hiện | Không |
| FAQ A | Có | Có | Hiện | Hiện |
| FAQ B | Có | Không | Hiện | Không |
| Gallery A | Có | READY | Hiện | Hiện |
| Gallery B | Có | Missing/Not READY | Hiện | Không |
| Hidden content | Có | Có | Không | Không |

Ngoài ra:

```text
exact match
starts-with
contains
description-only match
campus/location metadata match
no result
API error
language switch mid-request
content hidden after search
```

---

# 37. Không được làm

```text
Không checkout/switch sang Dev để code.
Không commit/push lên Dev.
Không reset/clean/stash WIP.
Không merge/rebase ngoài yêu cầu.
Không thêm bảng hoặc migration.
Không thêm search engine mới.
Không gọi translation API trong search.
Không fallback VI trong English search.
Không trả draft/hidden/private content.
Không refactor toàn bộ public module.
Không đổi route public không liên quan.
Không tạo abstraction dư thừa.
Không dùng dangerouslySetInnerHTML để highlight.
Không báo hoàn thành khi chưa build/test.
```

---

# 38. Báo cáo cuối cùng bắt buộc

Báo cáo theo format:

## Preflight

```text
Current branch
Current HEAD
Dev HEAD used for reference
Working tree/WIP status
Baseline status
```

## Thay đổi chính

```text
Backend files changed
Frontend files changed
Tests changed
Docs changed
Database files changed: expected none
```

## Logic hoàn thành

```text
4 content groups
public visibility
VI strict search
EN strict search
deep links
popup redesign
Partner hasMore CTA
race-condition handling
```

## Gates

```text
Backend build
Backend unit tests
Integration tests
Frontend typecheck
Frontend lint
Frontend tests
Frontend build
Manual VI
Manual EN
Manual mobile
```

## Nợ còn lại

Chỉ ghi nợ thật, không che giấu test chưa chạy hoặc lỗi pre-existing.

## Commit

Không commit/push trừ khi được người dùng yêu cầu rõ. Nếu được yêu cầu commit:

```text
commit scope nhỏ
message rõ
không include WIP ngoài task
không push nếu chưa được yêu cầu
```

---

# 39. Definition of Done

Chỉ được báo hoàn thành khi tất cả điều kiện sau đạt:

```text
[ ] Search chỉ còn News, Partners, Gallery, FAQ.
[ ] Campus bị loại khỏi search contract và UI.
[ ] Popular suggestions bị xóa.
[ ] Contact footer bị xóa khỏi Search Popup.
[ ] Search VI chỉ tìm/hiển thị VI.
[ ] Search EN chỉ tìm/hiển thị EN.
[ ] Không fallback VI trong English.
[ ] Chỉ public content được trả.
[ ] Gallery search dùng cùng visibility chain với public detail.
[ ] News click mở đúng bài.
[ ] Partner click mở đúng partner.
[ ] FAQ click mở đúng accordion.
[ ] Gallery click mở đúng campus/location/item.
[ ] Reload deep-link FAQ hoạt động.
[ ] Reload deep-link Gallery hoạt động.
[ ] Partner “Xem thêm” chỉ hiện khi hasMore.partners = true.
[ ] Popup có initial/loading/empty/error/retry.
[ ] Language switch không bị stale response.
[ ] Date format đúng locale.
[ ] Mobile không overflow.
[ ] Không có database migration/schema change.
[ ] Backend build xanh.
[ ] Backend search tests xanh.
[ ] Frontend typecheck/build xanh.
[ ] Test/manual evidence được báo cáo trung thực.
```

---

# 40. Kết luận triển khai

Thực hiện theo nguyên tắc:

```text
Đọc code `Dev`
→ đối chiếu nhánh hiện tại
→ giữ nguyên current branch
→ sửa ít file nhất
→ không làm mất WIP
→ không đổi database
→ hoàn thành backend + frontend + deep links + tests
→ báo cáo có bằng chứng
```
