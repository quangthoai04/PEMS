# PEMS — PUBLIC GALLERY ITEM SHARING + FACEBOOK OPEN GRAPH PREVIEW
## FULL IMPLEMENTATION GUIDE FOR AI AGENT

> Scope: Hoàn thiện **toàn bộ tính năng chia sẻ Gallery Item public**, gồm:
>
> 1. Sao chép canonical deep-link.
> 2. Facebook Share.
> 3. Facebook rich preview/Open Graph động cho từng Gallery Item: đúng tiêu đề, mô tả, ảnh đại diện và URL.
>
> Không đổi architecture tổng thể, không thêm DB schema, không tạo share token, không chuyển React/Vite sang Next.js/SSR.

---

# 0. Snapshot / kiến trúc hiện tại cần hiểu trước khi sửa

Repository:

```text
quangthoai04/PEMS
```

Target branch:

```text
Dev
```

Tại thời điểm lập plan, `Dev` đã có public Gallery thực tế và deep-link bằng:

```text
/visit-fptu/{campusCode}?locationId={locationId}&itemId={galleryItemId}
```

Frontend deploy trên Vercel, backend deploy Railway.

`frontend/pems-react/vercel.json` hiện có:

```json
{
  "rewrites": [
    {
      "source": "/api/:path*",
      "destination": "https://pems-production-2245.up.railway.app/api/:path*"
    },
    {
      "source": "/(.*)",
      "destination": "/index.html"
    }
  ]
}
```

Điều này có nghĩa:

```text
Browser request
/visit-fptu/hn?locationId=...&itemId=...
        ↓
Vercel
        ↓
/index.html
        ↓
React chạy
        ↓
CampusDetailVisitPage đọc query params
        ↓
Mở đúng Location + Gallery Item
```

Đây là đúng cho browser.

Nhưng crawler Facebook không nên phụ thuộc vào React runtime để lấy metadata động. Vì vậy cần bổ sung server-generated Open Graph HTML cho crawler, nhưng vẫn giữ nguyên SPA cho người dùng thật.

---

# 1. Mục tiêu cuối cùng

Khi user mở Gallery Item:

```text
Campus HN
→ Tòa Demo 01
→ Sảnh chính
→ Tượng 01
```

và bấm:

```text
Share
├── Sao chép liên kết
└── Facebook
```

canonical URL phải là:

```text
https://www.pems-fpt.site/visit-fptu/hn?locationId=21&itemId=105
```

## Copy Link

Người nhận mở link:

```text
PEMS
→ đúng campus
→ đúng location
→ đúng Gallery Item
```

## Facebook Share

Facebook crawler đọc cùng URL và phải nhận được server-generated HTML có:

```html
<meta property="og:title" ... />
<meta property="og:description" ... />
<meta property="og:image" ... />
<meta property="og:url" ... />
<meta property="og:type" content="website" />
<meta property="og:site_name" ... />
```

Kết quả mong muốn trên Facebook:

```text
┌────────────────────────────────────┐
│                                    │
│       [ẢNH ĐẠI DIỆN GALLERY]       │
│                                    │
├────────────────────────────────────┤
│ Tượng 01                           │
│ FPT là một trong những tập đoàn... │
│ pems-fpt.site                      │
└────────────────────────────────────┘
```

Khi người dùng click card Facebook:

```text
Facebook
→ canonical PEMS URL
→ browser nhận SPA
→ React mở đúng Gallery Item
```

---

# 2. Kiến trúc được chọn

## Không dùng

Không:

- chuyển project sang Next.js;
- SSR toàn bộ frontend;
- prerender toàn site;
- tạo share token;
- tạo `gallery_shares`;
- lưu lượt chia sẻ;
- thêm Facebook SDK;
- thêm Facebook OAuth;
- thêm Facebook App ID chỉ để share URL;
- tạo một URL share khác rồi redirect user nếu không cần thiết.

## Dùng

Giữ **một canonical URL duy nhất**:

```text
/visit-fptu/{campusCode}?locationId=X&itemId=Y
```

Sau đó phân luồng ở Vercel theo request:

```text
                     ┌─ Browser thường
Canonical URL ───────┤
                     │   → index.html
                     │   → React SPA
                     │   → existing deep-link
                     │
                     └─ Facebook crawler
                         → Vercel conditional rewrite
                         → Railway anonymous OG preview endpoint
                         → HTML chứa Open Graph metadata động
```

Điểm quan trọng:

**URL bên ngoài KHÔNG đổi.**

Vercel dùng rewrite, không redirect.

Facebook vẫn coi URL được scrape là:

```text
https://www.pems-fpt.site/visit-fptu/hn?locationId=21&itemId=105
```

---

# 3. Tại sao chọn cách này

Project hiện là:

```text
React + Vite static SPA
+
Vercel
+
.NET backend trên Railway
```

Nếu cố SSR toàn React chỉ để có Facebook metadata thì thay đổi quá lớn.

Backend hiện đã có source of truth public Gallery:

```text
GET /api/public/visit-fptu/gallery-items/{galleryItemId}
```

Endpoint này đã enforce:

```text
GalleryItem = PUBLISHED
AND GalleryItem not deleted
AND Location = ACTIVE
AND Area = ACTIVE
AND Campus = ACTIVE
AND item có active media
```

Do đó OG preview phải **reuse chính public read path này**.

Không được query DB theo một rule public mới độc lập nếu có thể tránh.

---

# 4. Existing public visibility phải là source of truth

Backend file:

```text
backend/PEMS.Application/Galleries/Public/Queries/
GetPublicGalleryItemDetail/
GetPublicGalleryItemDetailQueryHandler.cs
```

Handler hiện đã enforce effective visibility.

OG preview không được tạo metadata cho:

- item HIDDEN;
- item deleted;
- location INACTIVE;
- area INACTIVE;
- campus INACTIVE;
- item không có media active.

Nếu public detail trả 404 thì social preview cũng phải 404.

---

# 5. Existing public media proxy phải được reuse

Backend hiện đã có:

```text
/api/public/visit-fptu/media/{fileId}/content
```

Đây là anonymous route nhưng server-side scoped theo public Gallery visibility.

Không đưa raw Google Drive URL ra Open Graph.

Đối với uploaded image:

```text
og:image =
https://www.pems-fpt.site/api/public/visit-fptu/media/{fileId}/content
```

Vercel `/api/*` sẽ proxy đến Railway.

Ưu điểm:

- cùng public domain;
- HTTPS;
- không expose Drive;
- visibility vẫn do backend kiểm tra;
- Facebook crawler có thể GET ảnh mà không login.

---

# 6. Phase A — Canonical share URL

Tạo:

```text
frontend/pems-react/src/shared/utils/galleryShare.ts
```

## Interface

```ts
export interface GalleryShareTarget {
  campusCode: string;
  locationId: number;
  galleryItemId: number;
}
```

## Builder

```ts
export function buildGalleryShareUrl({
  campusCode,
  locationId,
  galleryItemId,
}: GalleryShareTarget): string {
  const url = new URL(
    `/visit-fptu/${campusCode.toLowerCase()}`,
    window.location.origin,
  );

  url.searchParams.set('locationId', String(locationId));
  url.searchParams.set('itemId', String(galleryItemId));

  return url.toString();
}
```

Không dùng:

```ts
window.location.href
```

làm source of truth cho share URL.

---

# 7. Phase B — Copy Link

Trong:

```text
frontend/pems-react/src/pages/CampusDetailVisitPage.tsx
```

dùng:

```ts
detail.location.locationId
detail.galleryItem.galleryItemId
campusCode
```

để build URL.

## Clipboard

Có:

```ts
copyTextToClipboard()
```

với Clipboard API + fallback.

Success:

```text
Đã sao chép liên kết.
```

Error:

```text
Không thể sao chép liên kết. Vui lòng thử lại.
```

Dùng `react-hot-toast`/toast mechanism hiện có.

Không tự tạo toast system mới.

---

# 8. Phase C — Facebook Share action

Utility:

```ts
export function buildFacebookShareUrl(url: string): string {
  return `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(url)}`;
}
```

Open:

```ts
window.open(
  buildFacebookShareUrl(shareUrl),
  '_blank',
  'noopener,noreferrer,width=640,height=720',
);
```

Không dùng Facebook SDK.

---

# 9. Phase D — Backend social preview endpoint

Bổ sung endpoint anonymous trong:

```text
backend/PEMS.Api/Controllers/PublicVisitFptuController.cs
```

Đề xuất route:

```text
GET /api/public/visit-fptu/share-preview/{campusCode}
    ?locationId={locationId}
    &itemId={galleryItemId}
```

Ví dụ internal Railway request:

```text
https://pems-production-2245.up.railway.app/
api/public/visit-fptu/share-preview/hn
?locationId=21
&itemId=105
```

Endpoint này không phải URL được user share.

Nó chỉ là internal origin mà Vercel rewrite Facebook crawler tới.

---

# 10. Endpoint phải reuse GetPublicGalleryItemDetailQuery

Không tạo query DB khác nếu không cần.

Pseudo:

```csharp
[HttpGet("share-preview/{campusCode}")]
public async Task<IActionResult> GetSharePreview(
    string campusCode,
    long locationId,
    long itemId,
    CancellationToken cancellationToken)
{
    var detail = await _mediator.Send(
        new GetPublicGalleryItemDetailQuery(itemId),
        cancellationToken);

    // Validate URL ownership.
    if (!string.Equals(
            detail.Campus.CampusCode,
            campusCode,
            StringComparison.OrdinalIgnoreCase) ||
        detail.Location.LocationId != (ulong)locationId)
    {
        return NotFound();
    }

    // Build metadata + HTML.
}
```

Nếu `GetPublicGalleryItemDetailQuery` 404:

```text
share preview = 404
```

Không catch rồi tạo fake preview.

---

# 11. Cross-campus / cross-location validation

Metadata endpoint nhận:

```text
campusCode
locationId
itemId
```

Phải validate cả 3.

Ví dụ request:

```text
/visit-fptu/hn?locationId=21&itemId=999
```

nhưng item `999` thuộc:

```text
DN / location 51
```

thì:

```text
404
```

Không generate OG card cho URL giả.

Điều này phải đồng nhất với deep-link validation hiện có ở frontend.

---

# 12. Tạo Share Metadata DTO/presentation model

Không nhất thiết đưa DTO vào Application nếu endpoint có thể map từ existing public detail DTO.

Có thể tạo helper trong API layer:

```text
backend/PEMS.Api/PublicGallery/
PublicGallerySharePreviewBuilder.cs
```

hoặc vị trí tương đương phù hợp structure hiện tại.

Model nội bộ:

```csharp
internal sealed record PublicGalleryShareMetadata(
    string Title,
    string Description,
    string ImageUrl,
    string CanonicalUrl,
    string SiteName,
    string Locale);
```

Không expose endpoint JSON mới nếu không cần.

---

# 13. Metadata title

Default dùng nội dung VI để canonical preview ổn định.

Ví dụ:

```text
Tượng 01 | VisitFPTU
```

Hoặc nếu muốn ngắn hơn:

```text
Tượng 01
```

Khuyến nghị:

```text
og:title = GalleryItem.Title
og:site_name = "PEMS - VisitFPTU"
```

Không nhồi:

```text
campus + area + location + title + PEMS...
```

vào title nếu quá dài.

Area/location có thể đưa vào description nếu cần.

---

# 14. Metadata description

Nguồn:

```text
detail.GalleryItem.Content.Vi.Description
```

Normalize:

1. trim;
2. collapse whitespace;
3. bỏ line break dư;
4. truncate khoảng 180–220 ký tự;
5. HTML encode trước khi chèn vào HTML.

Ví dụ:

```text
FPT là một trong những tập đoàn công nghệ hàng đầu Việt Nam...
```

Nếu description rỗng:

```text
Khám phá {locationName} tại {campusName} trên VisitFPTU.
```

Không để meta description rỗng.

---

# 15. Metadata image selection

## Priority

Chọn media theo thứ tự:

### 1. Primary media nếu là IMAGE

Dùng public proxy URL:

```text
https://www.pems-fpt.site
/api/public/visit-fptu/media/{fileId}/content
```

### 2. Primary media nếu là YouTube

Dùng:

```text
ThumbnailUrl
```

chỉ khi:

- source type = YouTube;
- URL là HTTPS hợp lệ.

### 3. Nếu primary không tạo được image preview

Tìm media IMAGE active tiếp theo.

### 4. Nếu không có image nào

Nếu có YouTube media:

```text
dùng YouTube thumbnail đầu tiên
```

### 5. Fallback cuối

Dùng static site image:

```text
https://www.pems-fpt.site/og/gallery-default.jpg
```

---

# 16. Static OG fallback image

Tạo:

```text
frontend/pems-react/public/og/gallery-default.jpg
```

Khuyến nghị kích thước:

```text
1200 x 630
```

Nội dung:

- FPT University / VisitFPTU;
- hình campus phù hợp;
- không chứa thông tin item cụ thể.

Vite sẽ copy file `public` thành static asset giữ nguyên URL:

```text
/og/gallery-default.jpg
```

Không reference file qua:

```text
/src/assets/...
```

trong OG metadata production.

---

# 17. Absolute URL bắt buộc cho OG

Không emit:

```html
<meta property="og:image" content="/api/public/..." />
```

Phải emit absolute HTTPS:

```html
<meta
  property="og:image"
  content="https://www.pems-fpt.site/api/public/visit-fptu/media/123/content"
/>
```

Tương tự:

```text
og:url
canonical
```

đều phải absolute.

---

# 18. Không dùng Request.Host làm canonical domain

Không build canonical URL từ:

```csharp
Request.Host
```

vì endpoint thực tế chạy Railway và request nội bộ tới origin có thể mang host không mong muốn.

Dùng config hiện có:

```text
App:FrontendBaseUrl
```

Production phải cấu hình:

```text
https://www.pems-fpt.site
```

hoặc canonical frontend domain thực tế.

Nếu production hiện sử dụng:

```text
https://pems-fpt.site
```

không `www`, dùng chính domain canonical thật của project.

**AI Agent phải kiểm tra cấu hình production hiện tại trước khi hard-code bất kỳ hostname nào.**

Ưu tiên:

```text
IConfiguration["App:FrontendBaseUrl"]
```

hoặc options hiện có của codebase.

---

# 19. Build canonical URL backend-side

Ví dụ:

```csharp
var frontendBase = _configuration["App:FrontendBaseUrl"]
    ?? throw new InvalidOperationException("App:FrontendBaseUrl is required.");

var canonicalUrl =
    $"{frontendBase.TrimEnd('/')}/visit-fptu/{campusCode.ToLowerInvariant()}" +
    $"?locationId={locationId}&itemId={itemId}";
```

Nếu codebase có utility URI builder thì reuse.

Không concatenate user-supplied free text ngoài validated ids/code.

---

# 20. Resolve uploaded image URL

Public detail hiện trả relative media URL:

```text
/api/public/visit-fptu/media/{fileId}/content
```

Helper:

```csharp
private static string ToAbsoluteFrontendUrl(
    string frontendBase,
    string relativeOrAbsolute)
{
    if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var absolute))
        return absolute.ToString();

    return new Uri(
        new Uri(frontendBase.TrimEnd('/') + "/"),
        relativeOrAbsolute.TrimStart('/'))
        .ToString();
}
```

Đối với uploaded file:

**ưu tiên rebuild từ trusted `FileId`** nếu DTO có sẵn `FileId`.

Ví dụ:

```text
{frontendBase}/api/public/visit-fptu/media/{fileId}/content
```

Không cần tin raw URL từ client.

---

# 21. HTML escaping — bắt buộc

Dynamic fields:

```text
title
description
areaName
locationName
image alt
```

phải HTML encode.

Dùng:

```csharp
System.Text.Encodings.Web.HtmlEncoder.Default
```

Ví dụ:

```csharp
var title = HtmlEncoder.Default.Encode(metadata.Title);
```

Không chèn thẳng DB text vào HTML.

Mục tiêu:

- tránh malformed HTML;
- tránh XSS/reflected markup trong preview response.

---

# 22. Open Graph HTML tối thiểu

Response crawler nên tương tự:

```html
<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8" />

  <title>Tượng 01 | VisitFPTU</title>

  <meta
    name="description"
    content="FPT là một trong những tập đoàn công nghệ..."
  />

  <link
    rel="canonical"
    href="https://www.pems-fpt.site/visit-fptu/hn?locationId=21&amp;itemId=105"
  />

  <meta property="og:type" content="website" />
  <meta property="og:site_name" content="PEMS - VisitFPTU" />

  <meta property="og:title" content="Tượng 01" />

  <meta
    property="og:description"
    content="FPT là một trong những tập đoàn công nghệ..."
  />

  <meta
    property="og:url"
    content="https://www.pems-fpt.site/visit-fptu/hn?locationId=21&amp;itemId=105"
  />

  <meta
    property="og:image"
    content="https://www.pems-fpt.site/api/public/visit-fptu/media/123/content"
  />

  <meta
    property="og:image:secure_url"
    content="https://www.pems-fpt.site/api/public/visit-fptu/media/123/content"
  />

  <meta
    property="og:image:alt"
    content="Tượng 01"
  />

  <meta property="og:locale" content="vi_VN" />
  <meta property="og:locale:alternate" content="en_US" />
</head>
<body>
  <p>
    <a href="CANONICAL_URL">Mở nội dung trên VisitFPTU</a>
  </p>
</body>
</html>
```

`og:image:type`, width, height chỉ emit khi thật sự biết chính xác.

Không hard-code dimension giả cho user-uploaded image.

---

# 23. Open Graph properties bắt buộc

Tối thiểu:

```text
og:title
og:type
og:image
og:url
```

Nên thêm:

```text
og:description
og:site_name
og:image:secure_url
og:image:alt
og:locale
og:locale:alternate
```

---

# 24. Response headers cho preview HTML

Endpoint:

```text
Content-Type: text/html; charset=utf-8
```

Nên:

```text
Cache-Control: no-store
X-Content-Type-Options: nosniff
```

Lý do dùng `no-store` cho endpoint crawler:

- item/title/image có thể bị Staff Leader sửa;
- item có thể bị hide;
- tránh Vercel/origin giữ stale HTML preview ngoài ý muốn.

Facebook vẫn có cache riêng của Facebook; phần đó không do PEMS kiểm soát.

---

# 25. Vercel conditional rewrite cho Facebook crawler

Sửa:

```text
frontend/pems-react/vercel.json
```

Rule social crawler phải nằm **trước**:

```text
/api/:path*
```

và trước catch-all:

```text
/(.*) → /index.html
```

Đề xuất:

```json
{
  "rewrites": [
    {
      "source": "/visit-fptu/:campusCode",
      "has": [
        {
          "type": "header",
          "key": "user-agent",
          "value": ".*(facebookexternalhit|Facebot).*"
        },
        {
          "type": "query",
          "key": "locationId",
          "value": "(?<locationId>[0-9]+)"
        },
        {
          "type": "query",
          "key": "itemId",
          "value": "(?<itemId>[0-9]+)"
        }
      ],
      "destination": "https://pems-production-2245.up.railway.app/api/public/visit-fptu/share-preview/:campusCode?locationId=:locationId&itemId=:itemId"
    },
    {
      "source": "/api/:path*",
      "destination": "https://pems-production-2245.up.railway.app/api/:path*"
    },
    {
      "source": "/(.*)",
      "destination": "/index.html"
    }
  ]
}
```

AI Agent phải validate syntax với Vercel version/config hiện tại.

Không thay catch-all SPA logic ngoài việc chèn rule social trước nó.

---

# 26. Vì sao rewrite chỉ cho crawler

Nếu rewrite mọi request:

```text
/visit-fptu/hn?... → preview HTML
```

thì browser user sẽ không chạy React SPA.

Do đó rule phải có:

```text
User-Agent contains
facebookexternalhit
OR
Facebot
```

Browser Chrome/Edge/Safari không match:

```text
→ index.html
→ React
```

Meta crawler match:

```text
→ backend OG HTML
```

---

# 27. Query params phải bắt buộc trong crawler rewrite

Chỉ rewrite nếu có cả:

```text
locationId
itemId
```

Ví dụ page campus thường:

```text
/visit-fptu/hn
```

không có Gallery Item cụ thể.

Nó phải tiếp tục:

```text
→ index.html
```

Không đưa page chung vào item preview endpoint.

---

# 28. Facebook Preview chọn VI làm canonical metadata

Không tạo URL riêng:

```text
?lang=vi
?lang=en
```

trong task này.

Canonical preview dùng VI.

Có thể emit:

```html
<meta property="og:locale" content="vi_VN" />
<meta property="og:locale:alternate" content="en_US" />
```

Frontend user vẫn có thể đổi VI/EN sau khi mở Gallery Item.

---

# 29. Facebook cache behavior

Phải hiểu:

```text
PEMS backend cache
≠
Facebook scraper cache
```

Dù PEMS trả `no-store`, Facebook có thể cache metadata của URL sau khi scrape.

Sau khi Staff Leader thay:

- title;
- description;
- primary image;

Facebook card cũ có thể chưa đổi ngay.

Operational verification phải dùng Facebook Sharing Debugger và yêu cầu scrape lại URL.

Không xây cơ chế invalidate Facebook cache bằng hack trong PEMS.

---

# 30. Khi Gallery Item bị HIDDEN

Existing public query phải trả 404.

Social preview endpoint:

```text
→ 404
```

Không trả stale metadata từ DB riêng.

Canonical link user click:

```text
React
→ public API
→ content hidden/not found
```

Không được bypass.

---

# 31. Khi Location/Area/Campus INACTIVE

Giống item HIDDEN:

```text
GetPublicGalleryItemDetailQuery
→ 404
```

OG endpoint:

```text
→ 404
```

---

# 32. Khi primary media thay đổi

Staff Leader edit item:

```text
old primary
→ new primary
```

Preview builder đọc detail hiện tại ở thời điểm scrape.

Facebook lần scrape mới phải nhận:

```text
og:image = new primary/fallback selection
```

Nếu uploaded file mới có `fileId` mới thì URL ảnh cũng tự đổi.

Đây là tốt cho cache busting.

---

# 33. YouTube media

Current Gallery cho phép video YouTube.

Nếu primary media:

```text
SourceType = YOUTUBE
```

không dùng iframe/embed URL làm `og:image`.

Dùng:

```text
thumbnailUrl
```

Nếu thumbnail URL không hợp lệ:

```text
→ tìm IMAGE media khác
→ fallback static OG image
```

Không scrape/download YouTube image về PEMS trong task này.

---

# 34. Uploaded VIDEO legacy

Nếu data legacy có uploaded video nhưng không có thumbnail:

Không:

```text
og:image = video/mp4
```

Thay:

```text
→ image media khác
→ YouTube thumbnail khác
→ fallback /og/gallery-default.jpg
```

---

# 35. HTML body không phải app

Preview endpoint không cần render React application.

Nó chỉ phục vụ crawler.

Body tối thiểu:

```html
<body>
  <a href="canonical-url">Open VisitFPTU</a>
</body>
```

Không copy toàn bộ Vite `index.html`.

Không inject JavaScript redirect.

Browser user bình thường không tới endpoint vì Vercel UA routing.

---

# 36. Không cloaking nội dung sai lệch

Metadata crawler phải phản ánh đúng Gallery Item mà browser mở.

Không trả một title/image cho crawler nhưng link mở nội dung khác.

Do đó:

```text
campusCode + locationId + itemId
```

đều phải validate against same public detail response.

---

# 37. Backend file dự kiến thay đổi

## Bắt buộc

```text
backend/PEMS.Api/Controllers/PublicVisitFptuController.cs
```

Thêm public social preview action.

## Nên tạo helper

```text
backend/PEMS.Api/PublicGallery/PublicGallerySharePreviewBuilder.cs
```

hoặc folder naming tương ứng architecture hiện tại.

Helper chịu trách nhiệm:

- metadata mapping;
- description normalize/truncate;
- image selection;
- canonical URL;
- HTML escaping;
- HTML rendering.

Không nhét 100+ dòng string building vào controller nếu có thể tránh.

---

# 38. Frontend file dự kiến thay đổi

```text
frontend/pems-react/src/pages/CampusDetailVisitPage.tsx
frontend/pems-react/src/shared/utils/galleryShare.ts
frontend/pems-react/vercel.json
```

i18n resource tương ứng:

```text
visitFptu
```

Thêm fallback asset:

```text
frontend/pems-react/public/og/gallery-default.jpg
```

---

# 39. Backend config

Production Railway phải có:

```text
App__FrontendBaseUrl=https://www.pems-fpt.site
```

hoặc exact canonical production domain.

AI Agent phải check naming conventions hiện tại trước khi thay.

Không commit production secret.

`FrontendBaseUrl` không phải secret.

---

# 40. Unit tests — frontend utility

Tạo/reuse convention hiện có.

Test:

### URL builder

Input:

```text
HN, 21, 105
```

Expect:

```text
/visit-fptu/hn
locationId=21
itemId=105
```

### Facebook builder

URL phải encoded đúng.

### Clipboard

- success;
- API missing fallback;
- failure.

---

# 41. Component tests — share UI

Mở Gallery Item detail.

## Copy

Assert:

```text
clipboard.writeText
```

nhận canonical URL.

## Facebook

Mock:

```ts
window.open
```

Assert first argument decode ra canonical URL.

## Menu

- action xong menu đóng;
- click outside đóng nếu implemented;
- Escape đóng nếu integrated.

---

# 42. Backend tests — preview endpoint

Cần test ít nhất:

## Public item

Input:

```text
campus=HN
location=21
item=105
```

Expected:

```text
200
Content-Type text/html
```

HTML chứa:

```text
og:title
og:description
og:image
og:url
og:type
```

---

# 43. Backend test — hidden item

Item:

```text
HIDDEN
```

Expected:

```text
404
```

---

# 44. Backend test — location inactive

Expected:

```text
404
```

---

# 45. Backend test — area inactive

Expected:

```text
404
```

---

# 46. Backend test — campus inactive

Expected:

```text
404
```

---

# 47. Backend test — cross-location tamper

Public item tồn tại nhưng:

```text
requested locationId != detail.Location.LocationId
```

Expected:

```text
404
```

---

# 48. Backend test — cross-campus tamper

Expected:

```text
404
```

---

# 49. Backend test — HTML escaping

Title:

```text
A "test" <script>alert(1)</script>
```

Description có:

```text
< > & "
```

Expected:

HTML response không chứa executable raw markup.

Phải encoded.

---

# 50. Backend test — image selection

Case:

```text
primary IMAGE
```

Expect public proxy absolute URL.

Case:

```text
primary YOUTUBE
```

Expect thumbnail.

Case:

```text
primary unusable + second IMAGE
```

Expect second image.

Case:

```text
no usable image
```

Expect:

```text
/og/gallery-default.jpg
```

---

# 51. Vercel routing test

Lưu ý:

Conditional `has` routing có thể không hoạt động giống production khi chỉ dùng local dev server.

Phải test deployment preview/production routing.

## Facebook crawler simulation

```bash
curl -A "facebookexternalhit/1.1" \
  "https://www.pems-fpt.site/visit-fptu/hn?locationId=21&itemId=105"
```

Expected response body chứa:

```html
<meta property="og:title"
<meta property="og:image"
<meta property="og:url"
```

Không phải generic Vite `index.html`.

---

# 52. Browser UA routing test

```bash
curl -A "Mozilla/5.0" \
  "https://www.pems-fpt.site/visit-fptu/hn?locationId=21&itemId=105"
```

Expected:

```text
Vite/React index.html
```

Browser thật:

```text
→ React render
→ detail modal tự mở
```

---

# 53. Facebook Sharing Debugger verification

Sau deploy production:

1. lấy một canonical Gallery Item URL;
2. đưa vào Facebook Sharing Debugger;
3. yêu cầu scrape;
4. kiểm tra:
   - title;
   - description;
   - image;
   - canonical URL;
5. nếu metadata vừa sửa, dùng scrape again.

Không đánh giá feature chỉ bằng việc nhìn HTML trong browser DevTools sau khi React đã mount.

Crawler cần server response ban đầu.

---

# 54. Kiểm tra og:image trực tiếp

Mở URL lấy từ:

```html
<meta property="og:image">
```

bằng Incognito.

Expected:

```text
HTTP 200
Content-Type image/*
không login
```

Nếu URL ảnh trả:

```text
401
403
HTML
```

Facebook preview sẽ lỗi.

---

# 55. Kích thước ảnh

Fallback static:

```text
1200x630
```

là lựa chọn tốt cho social card.

Đối với uploaded Gallery image:

- dùng ảnh thật;
- không fake `og:image:width/height` nếu không biết;
- Facebook tự fetch.

Nếu về sau muốn crop mọi Gallery preview thành đúng 1200x630:

```text
đó là task image transformation riêng
```

Không cần để hoàn thành feature này.

---

# 56. Public media response

Existing endpoint hiện anonymous và stream media.

Không đổi sang authenticated `/api/files/{id}/content`.

OG image phải dùng public gallery media route.

---

# 57. Error behavior của preview endpoint

## Invalid numeric query

ASP.NET binding trả 400 nếu input invalid.

Có thể dùng constraints/nullable validation phù hợp convention.

## Valid ids nhưng không match

```text
404
```

## Item không public

```text
404
```

## Unexpected internal error

Dùng global error handling hiện tại.

Không trả stack trace trong HTML.

---

# 58. Cache

Preview HTML:

```text
Cache-Control: no-store
```

Media endpoint giữ policy hiện có nếu không có yêu cầu khác.

Không mở scope sang rework toàn bộ CDN caching.

---

# 59. SEO canonical

Thêm:

```html
<link rel="canonical" href="CANONICAL_GALLERY_URL" />
```

và:

```html
<meta property="og:url" content="CANONICAL_GALLERY_URL" />
```

Hai URL phải giống nhau về logical identity.

---

# 60. Không tạo duplicate canonical giữa www/non-www

Chọn domain production canonical chính xác.

Ví dụ nếu app chạy chuẩn trên:

```text
https://www.pems-fpt.site
```

thì luôn generate domain đó.

Nếu domain chuẩn thực tế là:

```text
https://pems-fpt.site
```

thì dùng domain đó.

Không để config mỗi môi trường tự sinh host theo incoming request.

---

# 61. Definition of Done — toàn feature

## Share cơ bản

- [ ] Share menu giữ UI hiện tại.
- [ ] Copy Link hoạt động.
- [ ] Có success toast.
- [ ] Có error handling.
- [ ] Facebook Share hoạt động.
- [ ] Không còn raw `window.location.href` làm share source.
- [ ] Canonical deep-link đúng campus/location/item.
- [ ] Reload canonical URL mở đúng item.

## Facebook rich preview

- [ ] Có server-rendered OG preview endpoint.
- [ ] Preview endpoint anonymous nhưng reuse public visibility.
- [ ] HIDDEN item trả 404.
- [ ] INACTIVE location/area/campus trả 404.
- [ ] Cross-campus/location mismatch trả 404.
- [ ] `og:title` động.
- [ ] `og:description` động.
- [ ] `og:image` động.
- [ ] `og:url` canonical.
- [ ] `og:type=website`.
- [ ] `og:site_name`.
- [ ] HTML encode dynamic data.
- [ ] og:image là absolute HTTPS.
- [ ] Uploaded image dùng public media proxy.
- [ ] YouTube dùng thumbnail.
- [ ] Có fallback image.
- [ ] Preview response `text/html; charset=utf-8`.
- [ ] Preview response không bị Vercel cache stale ngoài ý muốn.
- [ ] Facebook crawler được Vercel rewrite sang OG endpoint.
- [ ] Browser thường vẫn nhận React SPA.
- [ ] Existing `/api/*` Railway proxy không hỏng.
- [ ] Existing SPA catch-all không hỏng.

## Tests

- [ ] Frontend utility tests.
- [ ] Share component tests.
- [ ] Existing deep-link tests green.
- [ ] Backend preview tests.
- [ ] Visibility/tamper tests.
- [ ] HTML escaping test.
- [ ] Image-selection tests.
- [ ] Backend build green.
- [ ] Frontend typecheck green.
- [ ] Frontend build green.
- [ ] Relevant test suites green.
- [ ] Deployed curl crawler test pass.
- [ ] Facebook Sharing Debugger nhận đúng metadata.

---

# 62. Gate trước khi kết thúc task

Tối thiểu:

```text
Backend build
Relevant backend tests

Frontend typecheck
Frontend build
Relevant Gallery/deep-link/share tests
```

Sau deploy:

```text
curl Facebook crawler UA
curl normal browser UA
Facebook Sharing Debugger
Incognito canonical URL
Incognito og:image URL
```

---

# 63. Không làm ngoài scope

Không tự ý:

- đổi Gallery DB schema;
- đổi public Gallery data model;
- đổi Staff Leader Gallery behavior;
- đổi Google Drive architecture;
- tạo share analytics;
- đổi toàn site SEO;
- SSR toàn frontend;
- migrate framework;
- thêm các mạng xã hội khác;
- làm dynamic 1200x630 image generator;
- thay public media authorization.

Nếu phát hiện lỗi critical liên quan thì báo riêng, không tự mở rộng task.

---

# 64. Báo cáo AI Agent sau implementation

Trả ngắn gọn:

```text
## Preflight
- Branch
- HEAD
- Working tree

## Files changed
- ...

## Share
- Canonical URL
- Copy
- Facebook

## Open Graph
- Preview endpoint
- Vercel crawler routing
- Title/description/image/url
- Visibility enforcement
- HTML escaping
- Fallback image

## Tests
- Backend
- Frontend
- Deep-link
- Curl crawler
- Browser
- Facebook Debugger

## Result
- PASS/FAIL
```

Nếu Facebook Debugger chưa thể chạy vì production chưa deploy:

```text
không được báo FULL COMPLETE
```

Hãy báo rõ:

```text
Code complete
Deployment verification pending
```

---

# 65. Final architecture

```text
                         SHARE BUTTON
                              │
                              ▼
                  Canonical Gallery Item URL
                              │
           ┌──────────────────┴──────────────────┐
           │                                     │
           ▼                                     ▼
      Normal browser                      Facebook crawler
           │                                     │
           ▼                                     ▼
 Vercel SPA catch-all                 Vercel conditional rewrite
           │                                     │
           ▼                                     ▼
      /index.html                     Railway share-preview endpoint
           │                                     │
           ▼                                     ▼
        React SPA                       Existing public Gallery query
           │                                     │
           ▼                                     ▼
 locationId + itemId                     Visibility enforcement
           │                                     │
           ▼                                     ▼
 Open Gallery Item                   Dynamic Open Graph HTML
                                                 │
                                                 ▼
                                  title + description + image + URL
                                                 │
                                                 ▼
                                      Facebook rich preview
```

---

# 66. Quyết định triển khai quan trọng

**Một URL, hai response path theo User-Agent.**

Đây là điểm cốt lõi.

Không dùng:

```text
/share/gallery/105
```

làm public share URL riêng nếu không cần.

User và Facebook đều dùng:

```text
https://www.pems-fpt.site/visit-fptu/hn?locationId=21&itemId=105
```

Nhưng:

```text
Browser → SPA
Facebook crawler → OG HTML
```

Nhờ Vercel rewrite, URL bên ngoài không đổi.

Đây là giải pháp ít thay đổi nhất nhưng vẫn cho Facebook preview động đúng từng Gallery Item trong architecture React/Vite + Vercel + .NET/Railway hiện tại.
