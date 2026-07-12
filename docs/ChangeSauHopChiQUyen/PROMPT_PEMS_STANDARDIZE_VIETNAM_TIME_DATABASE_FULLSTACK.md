# PROMPT — AUDIT VÀ CHUẨN HÓA TOÀN BỘ PEMS LƯU THỜI GIAN NGHIỆP VỤ THEO GIỜ VIỆT NAM

## 1. Vai trò của AI Agent

Bạn là Senior Full-stack Engineer phụ trách sửa PEMS, đồng thời đảm nhiệm:

- Senior ASP.NET Core .NET 8 / Clean Architecture Engineer.
- Senior React Vite TypeScript Engineer.
- Database-first MySQL 8 Engineer.
- Date/time and timezone correctness reviewer.
- Security reviewer cho JWT, OTP, session, OAuth và token expiry.
- QA Engineer chịu trách nhiệm unit, integration và frontend/E2E test.

Không sửa theo suy đoán. Trước khi thay đổi code, phải search và đọc source hiện tại, SQL mới nhất và test liên quan.

---

## 2. Bối cảnh dự án

PEMS là Partner & Event Management System của FPT University.

Tech stack:

- Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, FluentValidation.
- Persistence: EF Core/Pomelo MySQL, database-first/manual fresh-create SQL.
- Frontend: React, Vite, TypeScript, Tailwind CSS.
- Database: MySQL 8, phần lớn trường thời gian dùng `DATETIME`.
- Hệ thống nghiệp vụ vận hành theo múi giờ Việt Nam `Asia/Ho_Chi_Minh`, UTC+07:00 và không có DST.

Quyết định nghiệp vụ đã được xác nhận:

> Toàn bộ cột `DATETIME` do PEMS quản lý và lưu trong MySQL phải là giờ Việt Nam, bao gồm cả thời gian nghiệp vụ, audit, OTP, session và bản ghi/snapshot thời gian hết hạn token. Nếu người dùng thực hiện thao tác lúc 20:00 Việt Nam thì MySQL phải lưu 20:00, API phải biểu diễn rõ UTC+07:00 và giao diện phải hiển thị 20:00, kể cả khi API/DB server hoặc trình duyệt chạy ở timezone khác.

Ranh giới bắt buộc giữa MySQL và giao thức ngoài:

- **Bên trong MySQL:** mọi cột `DATETIME` do PEMS quản lý, kể cả `expires_at` của OTP/session/refresh-token/token snapshot, phải lưu Vietnam wall-clock.
- **Bên trong JWT:** NumericDate `exp`/`iat`/`nbf` vẫn phải là Unix timestamp theo chuẩn JWT. Đây không phải cột `DATETIME` trong MySQL.
- **Payload gốc từ Google OAuth/OIDC hoặc hệ thống ngoài:** phải được đọc đúng contract UTC/Unix/duration của provider. Khi PEMS chuyển thành một cột `DATETIME` để lưu MySQL, phải convert sang giờ Việt Nam trước khi lưu.
- **Khi PEMS phát JWT hoặc gọi provider:** phải convert từ giá trị MySQL Vietnam wall-clock sang instant UTC/Unix đúng chuẩn tại integration boundary. Không đưa trực tiếp wall-clock local vào protocol claim.
- Không được hiểu “JWT/OAuth giữ chuẩn UTC/Unix” là cho phép lưu UTC trong cột MySQL. Chuẩn bên ngoài và chuẩn persistence bên trong là hai boundary khác nhau nhưng phải biểu diễn cùng một thời điểm thực tế.

---

## 3. Tài liệu và source bắt buộc đọc trước

Đọc và đối chiếu tối thiểu:

1. `PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md`.
2. `CLEAN_ARCHITECTURE.md`.
3. `PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md`.
4. `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`.
5. `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`.
6. `VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md`.
7. `PROJECT_STRUCTURE_FULL.md`.
8. `PEMS_v10_NEW_FINAL_SQL_TABLE_FIELD_DICTIONARY_MATCHED.docx`.
9. SQL fresh-create mới nhất, bao gồm file hiện được cung cấp `pems_full_v10_TTS_Gallery_FULL_UPDATED_NOTIFICATIONS_FIXED(3).sql` nếu đây vẫn là baseline mới nhất.
10. Backend/frontend source trên branch hiện tại.
11. Toàn bộ unit/integration/frontend tests liên quan tới date/time, visit request, reminder, OTP/session, email, notification, news, feedback, reports và minutes.

Khi tài liệu, comment, code hoặc SQL mâu thuẫn, phải báo cáo mâu thuẫn và ưu tiên:

1. Quyết định nghiệp vụ trong prompt này về timezone.
2. SQL fresh-create mới nhất.
3. SQL Table & Field Dictionary mới nhất.
4. Canonical Business Rules.
5. UC Implementation Rulebook.
6. Source code hiện tại.
7. Tài liệu legacy chỉ dùng để đối chiếu.

Không dùng dynamic permissions, `permissions`, `role_permissions` hoặc role legacy để thực hiện task này.

---

## 4. Hiện trạng đã biết cần xác minh bằng source

Không mặc định các nhận định dưới đây vẫn đúng; hãy xác minh lại trên branch hiện tại trước khi sửa:

1. `DateTimeService` đã có cả `UtcNow` và `VietnamNow`.
2. `VietnamTime.Now()` chuyển từ UTC sang `Asia/Ho_Chi_Minh`/UTC+7.
3. Flow tạo Visit Request đang lấy `_clock.UtcNow`, truyền vào `VisitRequestService.CreateAsync(...)`, rồi lưu `SubmittedAt` và `CreatedAt` bằng UTC.
4. MySQL `DATETIME` không lưu timezone/offset, EF có thể đọc lại với `DateTimeKind.Unspecified`.
5. Một số API trả chuỗi như `2026-07-02T13:00:00` không có `Z` hoặc `+07:00`.
6. Frontend có nhiều formatter tự viết bằng `new Date(value).toLocaleString(...)`, dẫn đến kết quả phụ thuộc browser timezone và contract chuỗi đầu vào.
7. `SubmittedVisitRequestDetailModal` đang hiển thị “Ngày gửi” bằng formatter tự viết; người dùng gửi 20:00 nhưng UI hiện 13:00.
8. `GetVisitInstanceMinutesQueryHandler` từng có helper `AsUtc(...)`; helper này phản ánh policy UTC cũ và phải được đánh giá lại, không được giữ/mở rộng máy móc nếu dữ liệu minutes chuyển sang Vietnam wall-clock.
9. `VisitRequestFormValidationRules` và một số validator/handler còn dùng `DateTime.Now`.
10. Các Email Draft handlers có thể còn dùng `DateTime.Now`.
11. SQL có nhiều `DEFAULT CURRENT_TIMESTAMP` và `ON UPDATE CURRENT_TIMESTAMP`, nhưng app chưa chắc đã ép MySQL session timezone thành `+07:00` trên mọi connection.
12. Một số frontend code tự nối `Z`, dùng `Date.UTC`, dùng `toISOString()` hoặc coi audit timestamp là wall-clock.

Phải đưa ra bảng xác minh trước khi code:

| Finding | File/table | Hiện trạng | Loại thời gian | Cần sửa |
|---|---|---|---|---|
| Ví dụ: VisitRequest.SubmittedAt | ... | ghi UTC vào DATETIME | Vietnam audit wall-clock | Có |

---

## 5. Mục tiêu task

### 5.1. Mục tiêu chính

Chuẩn hóa end-to-end để:

```text
Thao tác thực tế:          20:00 Asia/Ho_Chi_Minh
MySQL DATETIME:            20:00
API JSON:                  20:00:00+07:00
Frontend tại Việt Nam:     20:00
Frontend tại Mỹ/Châu Âu:   vẫn hiển thị 20:00 giờ Việt Nam
```

### 5.2. Mục tiêu kỹ thuật

- Một policy thời gian duy nhất, được mô tả rõ trong code và test.
- Không còn business logic phụ thuộc timezone của OS/container.
- Không còn `DateTime.Now` trong production business logic.
- Không còn formatter ngày giờ rải rác và hiểu timezone theo suy đoán.
- MySQL `CURRENT_TIMESTAMP` phải sinh giờ Việt Nam ổn định cả local và Railway.
- API phải phân biệt được timestamp Vietnam wall-clock với dữ liệu UTC của giao thức ngoài.
- Không cộng trừ 7 giờ bằng tay tại nhiều layer.
- Không làm lệch planned visit, agenda, logistics, reminder hoặc date boundary 24/72 giờ.

---

## 6. Phạm vi được sửa

Được phép sửa khi source audit chứng minh cần thiết:

### Backend

- `VietnamTime` và `IDateTimeService`/`DateTimeService`.
- Command/Query Handler và service ghi/đọc MySQL `DATETIME`.
- FluentValidation validators có so sánh thời gian.
- DTO/request/response liên quan timestamp.
- EF Core configuration, interceptor và DI cần thiết để ép MySQL session timezone.
- JSON mapping/serialization ở DTO boundary nếu cần.
- Background jobs: reminder, notification, TTS, scheduled processing.
- Email, feedback, news, minutes, documents, accounts, partner, gallery, FAQ, audit và toàn bộ security persistence trong MySQL, bao gồm OTP/session/token-expiry snapshot.

### Frontend

- Shared date/time utility.
- Page/component đang dùng `new Date`, `Date.UTC`, `toISOString`, `toLocaleString`, `toLocaleDateString` hoặc tự nối `Z`.
- Type/interface và API adapter nếu response thay đổi thành ISO 8601 có `+07:00`.
- `datetime-local` mapping, form edit/resubmit và schedule display.

### Database

- Fresh-create SQL mới nhất.
- Cấu hình timezone cho connection/session.
- Comment/documentation cho semantics của các cột thời gian.
- Một file patch riêng cho database hiện có nếu và chỉ nếu từng cột được chứng minh đang lưu UTC hoặc sai timezone.

### Tests

- Unit tests.
- Integration tests với MySQL test database thật.
- Frontend/component/Playwright tests phù hợp.
- Architecture test nếu thêm shared service/interceptor.

---

## 7. Phạm vi không được sửa

- Không thay đổi role, sub-role, authorization hoặc business workflow không liên quan timezone.
- Không thay đổi status Visit Request/campus/logistics/news/feedback.
- Không thay đổi UI layout/design ngoài phần cần thiết để hiển thị timezone rõ ràng.
- Không thay `DATETIME` thành `TIMESTAMP` hoặc thêm bảng/cột mới nếu chưa có phân tích và lý do bắt buộc.
- Không thay NumericDate nằm bên trong JWT hoặc payload gốc của Google/OAuth thành chuỗi Vietnam wall-clock; chỉ convert tại boundary khi đọc/ghi cột MySQL.
- Không cộng `INTERVAL 7 HOUR` cho toàn bộ database.
- Không sửa seed planned schedule/agenda/logistics chỉ vì thấy giá trị không có offset.
- Không chạy patch dữ liệu lên database production.
- Không dùng `DateTime.Now` hoặc timezone của server làm nguồn nghiệp vụ.
- Không dùng JavaScript `setHours(getHours() + 7)` hoặc cộng `7 * 60 * 60 * 1000`.
- Không thêm `Z` vào mọi chuỗi một cách máy móc; `Z` nghĩa là UTC và sẽ làm sai dữ liệu được lưu dưới Vietnam wall-clock.
- Không báo hoàn thành nếu chưa build/test hoặc chưa ghi rõ phần không chạy được.

---

## 8. Policy thời gian bắt buộc sau khi sửa

### 8.1. Vietnam wall-clock trong MySQL

Các thời điểm nghiệp vụ/audit do PEMS quản lý phải lưu theo giờ Việt Nam, ví dụ:

- `created_at`, `updated_at`, `submitted_at`.
- `approved_at`, `decided_at`, `rejected_at`.
- `cancelled_at`, `last_resubmitted_at`.
- `sent_at`, `read_at`, `verified_at`.
- `planned_start_at`, `planned_end_at`.
- Agenda start/end.
- Logistics proposed/required/handover times.
- Reminder scheduled time.
- Feedback/news/minutes timestamps.

Không mặc định danh sách trên đầy đủ. Phải audit schema và writer của từng field.

Giá trị .NET dùng để ghi MySQL wall-clock nên có semantics `DateTimeKind.Unspecified`, tránh để provider tự convert lần nữa. Tạo helper rõ nghĩa nếu cần, ví dụ:

```csharp
public static DateTime VietnamNow()
{
    var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
    return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
}
```

Không copy nguyên ví dụ nếu project đã có helper tốt hơn; tái sử dụng abstraction hiện có.

### 8.2. Security persistence trong MySQL cũng lưu Vietnam wall-clock

Mọi cột MySQL `DATETIME` do PEMS quản lý phải dùng giờ Việt Nam, không tạo ngoại lệ UTC trong database chỉ vì field thuộc authentication/security. Bao gồm nhưng không giới hạn:

- OTP `created_at`, `expires_at`, `used_at`, `locked_at`.
- Session `created_at`, `expires_at`, `last_activity_at`, `revoked_at`.
- Refresh token/session rotation timestamp.
- Password reset token expiry.
- Email action token expiry.
- OAuth token-expiry snapshot nếu PEMS lưu thành cột `DATETIME`.
- Login/security audit timestamps.

Writer và reader/comparison của những field này đều phải dùng Vietnam semantics:

```csharp
var vietnamNow = _clock.VietnamNow;
bool expired = entity.ExpiresAt <= vietnamNow;
```

Không được lưu `expires_at` theo giờ Việt Nam rồi so sánh với `_clock.UtcNow`, và cũng không được ghi UTC vào MySQL rồi đọc như Vietnam local.

### 8.3. JWT/OAuth giữ chuẩn ở integration boundary, không phải trong MySQL

Giữ UTC/Unix/duration đúng contract chỉ cho dữ liệu đang nằm trong hoặc đi qua protocol:

- JWT `iat`, `nbf`, `exp` là NumericDate/Unix timestamp.
- OAuth `expires_in` là duration tính bằng giây.
- Google/OIDC `exp` hoặc timestamp provider trả về theo UTC/Unix.
- Timestamp/signature của provider ngoài nếu contract yêu cầu UTC.

Quy tắc chuyển đổi hai chiều:

```text
Provider/JWT UTC hoặc Unix
    → convert thành Asia/Ho_Chi_Minh
    → lưu MySQL DATETIME Vietnam wall-clock

MySQL DATETIME Vietnam wall-clock
    → gắn timezone Asia/Ho_Chi_Minh
    → convert thành UTC/Unix
    → phát JWT hoặc gọi/so sánh theo protocol ngoài
```

Ví dụ cùng một thời điểm:

```text
JWT/provider:          13:00 UTC hoặc một Unix timestamp
MySQL PEMS:            20:00 Vietnam wall-clock
API/UI PEMS:           20:00:00+07:00
```

Không được cộng thêm 7 giờ vào Unix timestamp. Phải convert timezone đúng chuẩn để giữ nguyên instant và lifetime.

### 8.4. API contract

Đối với thời gian Việt Nam trả cho client, API phải trả ISO 8601 có offset:

```json
"2026-07-02T20:00:00+07:00"
```

Không trả chuỗi mơ hồ:

```json
"2026-07-02T20:00:00"
```

Ưu tiên `DateTimeOffset` tại DTO boundary hoặc helper chuyển `DateTime` wall-clock thành `DateTimeOffset` với `+07:00`. Không cấu hình global JSON converter mặc định coi mọi `DateTime` là cùng một loại nếu trong hệ thống vẫn có UTC/external timestamps.

### 8.5. Frontend display contract

Frontend luôn hiển thị thời gian nghiệp vụ theo:

```text
timeZone: 'Asia/Ho_Chi_Minh'
```

Browser timezone không được làm thay đổi kết quả.

Tạo utility tập trung, ví dụ:

```text
formatVietnamDateTime
formatVietnamDate
formatVietnamTime
toVietnamDateTimeLocalInput
fromDateTimeLocalInput
```

Tên và signature phải theo convention hiện tại sau khi audit.

Không dùng một formatter cho cả date-only, Vietnam wall-clock và UTC external timestamp nếu semantics khác nhau.

---

## 9. Quy trình audit bắt buộc trước khi code

### 9.1. Audit backend writers/readers

Search toàn repository tối thiểu theo:

```text
DateTime.Now
DateTime.UtcNow
_clock.UtcNow
_clock.VietnamNow
VietnamTime.Now
DateTime.SpecifyKind
DateTimeOffset
ToUniversalTime
ConvertTimeFromUtc
CURRENT_TIMESTAMP
UTC_TIMESTAMP
NOW()
```

Với mỗi occurrence production, phân loại:

| File | Field/table | Writer/reader | Current clock | Required clock | Action |
|---|---|---|---|---|---|

### 9.2. Audit database columns

Tạo inventory tất cả cột:

- `DATETIME`.
- `TIMESTAMP` nếu có.
- Default `CURRENT_TIMESTAMP`.
- `ON UPDATE CURRENT_TIMESTAMP`.
- Trigger dùng `NOW()`/`CURRENT_TIMESTAMP`/`UTC_TIMESTAMP()`.

Phân nhóm:

1. Vietnam audit wall-clock.
2. Vietnam planned/business wall-clock.
3. Vietnam security/session/OTP/token-expiry persistence.
4. Date-only/time-only.
5. Giá trị protocol/external UTC/Unix chỉ tồn tại tại integration boundary, không phải cột MySQL `DATETIME` được phép lưu UTC.
6. Không đủ bằng chứng về dữ liệu cũ — chưa được migrate nhưng target semantics vẫn phải được xác định.

### 9.3. Audit frontend

Search tối thiểu:

```text
new Date(
Date.UTC
toISOString
toLocaleString
toLocaleDateString
toLocaleTimeString
setHours
getTimezoneOffset
endsWith('Z')
+ 'Z'
timeZone:
```

Mỗi occurrence phải xác định field đầu vào là:

- Vietnam ISO `+07:00`.
- Vietnam raw wall-clock legacy.
- UTC ISO `Z`.
- Date-only.
- `datetime-local` value.

Không thay đổi khi chưa biết semantics.

### 9.4. Audit deployment/database runtime

Kiểm tra:

- Timezone của máy API local.
- Timezone container/Railway.
- `@@session.time_zone` và `@@global.time_zone` của MySQL.
- Connection pooling có tạo session mới không.
- Fresh-create SQL có `SET time_zone` không.
- Test database có cùng cấu hình production không.

---

## 10. Backend implementation requirements

### 10.1. Clock abstraction

- Dùng `IDateTimeService`; không đọc clock trực tiếp trong business handler/validator.
- `VietnamNow` phải luôn trả giờ Asia/Ho_Chi_Minh không phụ thuộc OS timezone.
- Không hardcode `DateTime.UtcNow.AddHours(7)` rải rác.
- Nếu cần fixed clock cho tests, mở rộng test double thay vì sleep hoặc dùng thời gian thật.

### 10.2. Persistence writers

Mọi handler/service ghi MySQL field thuộc Vietnam policy phải dùng cùng một `vietnamNow` trong một transaction, ví dụ:

```csharp
var vietnamNow = _clock.VietnamNow;
```

Không gọi clock nhiều lần trong cùng một operation nếu các timestamp phải giống nhau.

Ưu tiên audit các flow:

1. UC-17 public/authenticated create Visit Request.
2. Edit pending và resubmit rejected.
3. Campus approve/reject và assign Host.
4. Request/campus cancellation.
5. Invitation/participant/logistics/agenda/minutes.
6. Feedback/news/email/notification.
7. Account/department/campus/partner/gallery/document/FAQ.
8. Audit/security logs, OTP, sessions, refresh/password-reset/email-action tokens và background jobs.

### 10.3. Validators và business boundary

- Validation lịch thăm 24/72 giờ phải so với `_clock.VietnamNow`.
- Không dùng `DateTime.Now` trong static rule.
- Nếu validator cần clock, inject `IDateTimeService` đúng DI pattern.
- Planned time từ request phải được normalize thành Vietnam wall-clock trước khi so sánh.
- Kiểm tra boundary chính xác tại `now + 24h`, trước boundary một giây và sau boundary một giây.
- Không dùng `DateTime.ToUniversalTime()` trên value `Unspecified` nếu chưa gắn đúng timezone.

### 10.4. Authentication/security persistence

- Audit toàn bộ OTP/session/refresh-token/password-reset/email-action-token writer và reader.
- Mọi MySQL `DATETIME` của các module này phải ghi VietnamNow và so sánh với VietnamNow.
- Khi tạo JWT, chuyển Vietnam wall-clock expiry thành UTC/Unix NumericDate tại boundary; không ghi trực tiếp local DateTime vào JWT claim.
- Khi nhận Google/OAuth `expires_in` hoặc `exp`, tính instant theo contract provider, convert sang Asia/Ho_Chi_Minh rồi mới lưu MySQL.
- Khi cần kiểm tra provider token theo protocol, convert MySQL Vietnam wall-clock trở lại instant UTC/Unix hoặc dùng duration/instant gốc theo contract; không thay đổi lifetime.
- Đặt tên helper rõ nghĩa, ví dụ `ToVietnamWallClock`, `VietnamWallClockToUtc`, `ToUnixTimeSeconds`, nhưng phải theo convention hiện có và có unit test.

### 10.5. Background jobs

- Reminder/scheduled jobs phải so sánh cùng semantics với cột DB.
- Worker chạy trên Railway UTC vẫn phải dispatch đúng giờ Việt Nam.
- Không double-convert scheduled time.
- Kiểm tra polling window khi qua 00:00 Việt Nam.

### 10.6. MySQL session timezone

Đảm bảo mọi connection do application mở đều chạy:

```sql
SET time_zone = '+07:00';
```

Không chỉ đặt trong một lần import SQL. Dùng cơ chế phù hợp với EF Core/Pomelo hiện tại, ví dụ connection interceptor/connection-open hook được đăng ký trong Infrastructure DI.

Yêu cầu:

- Hoạt động với connection pooling.
- Không mở connection riêng ngoài DbContext mà bỏ qua timezone setup.
- Không cần quyền `SET GLOBAL time_zone` trên managed MySQL.
- `CURRENT_TIMESTAMP` và `ON UPDATE CURRENT_TIMESTAMP` phải sinh Vietnam wall-clock.
- Có integration test hoặc diagnostic assertion cho `SELECT NOW(), UTC_TIMESTAMP(), @@session.time_zone`.

### 10.7. Controller/Clean Architecture

- Controller chỉ gọi MediatR/service abstraction phù hợp.
- Không đưa timezone conversion/business logic vào controller.
- Shared timezone helper nằm đúng layer và không tạo dependency ngược.
- Infrastructure chịu trách nhiệm connection/session behavior.

---

## 11. Frontend implementation requirements

### 11.1. Shared utility

Tạo hoặc chuẩn hóa một utility dùng chung trong shared layer. Utility phải:

- Nhận ISO có `+07:00`.
- Hiển thị cố định Asia/Ho_Chi_Minh.
- Hỗ trợ locale Việt/Anh nhưng timezone vẫn là Việt Nam.
- Trả fallback an toàn cho null/invalid input.
- Không âm thầm coi chuỗi không offset là UTC.
- Có compatibility path rõ ràng cho legacy raw wall-clock nếu API chưa thể đổi đồng loạt trong một commit.

### 11.2. Các màn ưu tiên sửa

Audit và sửa ít nhất:

- `SubmittedVisitRequestDetailModal` — “Ngày gửi”.
- Duplicate Visit Request summary — `existingSubmittedAt`.
- Decision reason và cancellation reason panels.
- Visit Request list/detail/edit/resubmit.
- Staff dashboard/calendar/detail.
- Visitor visit detail.
- Notifications bell/page.
- News list/detail/visit news.
- Feedback list/detail/modal.
- Email list/detail/draft/sent detail.
- Minutes, documents, partner, gallery, FAQ, account và reports nếu có hiển thị timestamp.

### 11.3. `datetime-local`

- `datetime-local` không mang timezone.
- Khi hydrate form từ API `+07:00`, phải giữ nguyên giờ Việt Nam, không dùng `toISOString()` làm trôi -7 giờ.
- Khi submit, gửi contract mà backend hiểu rõ là Vietnam wall-clock hoặc `+07:00` theo API hiện tại.
- Edit → save không thay đổi thời gian nếu người dùng không sửa.
- Lặp edit/save nhiều lần không làm drift thời gian.

### 11.4. UI/UX

Nếu cần, hiển thị nhãn nhỏ:

```text
Giờ Việt Nam (GMT+7)
Vietnam Time (GMT+7)
```

Không redesign layout. Chỉ bổ sung timezone label ở nơi người dùng có thể hiểu nhầm, đặc biệt schedule, export filter và form nhập thời gian.

---

## 12. Database/SQL requirements

### 12.1. Fresh-create SQL

- Đọc toàn bộ DDL, default, trigger và seed liên quan time.
- Bổ sung `SET time_zone = '+07:00'` cho session chạy fresh-create nếu phù hợp.
- Cập nhật comment/documentation để nêu rõ MySQL business/audit `DATETIME` là Vietnam wall-clock.
- Không dựa riêng vào câu `SET` trong fresh-create để cấu hình runtime application connection.
- Không tạo migration EF vì dự án database-first/manual SQL, trừ khi repository hiện tại đã thay đổi policy này.

### 12.2. Existing database patch

Nếu cần chuyển dữ liệu đang có, tạo file patch riêng, không nhét conversion nguy hiểm vào fresh-create SQL.

Patch phải có:

1. Precheck query.
2. Danh sách table/column được chuyển.
3. Bằng chứng writer cũ lưu UTC.
4. Exclusion list các field đã là Vietnam wall-clock.
5. Transaction nếu MySQL/table operation cho phép.
6. Postcheck query.
7. Comment cảnh báo backup và chỉ chạy một lần.
8. Không tự động execute patch.

Tuyệt đối không được:

```sql
UPDATE every_table
SET every_datetime = DATE_ADD(every_datetime, INTERVAL 7 HOUR);
```

Phải đặc biệt không cộng lại 7 giờ cho:

- `planned_start_at`, `planned_end_at` nếu source/seed chứng minh đã là giờ Việt Nam.
- Agenda start/end đã nhập theo giờ lịch Việt Nam.
- Logistics scheduled/proposed time đã là wall-clock.
- Reminder scheduled time đã là wall-clock.
- Seed được viết trực tiếp theo lịch Việt Nam.

Nếu một cột có dữ liệu trộn UTC và Vietnam local, không được update hàng loạt. Hãy báo blocker và đề xuất cách nhận diện record an toàn.

### 12.3. Không đổi schema không cần thiết

Giữ `DATETIME` nếu đã phù hợp với quyết định lưu wall-clock. Không đổi sang `TIMESTAMP` chỉ để “tự có timezone”, vì điều đó có thể tạo conversion ngầm và phá planned schedule.

---

## 13. Validation và security rules

- Backend vẫn là security/business validation boundary.
- Frontend timezone conversion không được quyết định quyền sửa/hủy/approve.
- Boundary 24/72 giờ phải được backend tính theo VietnamNow.
- OTP/session/refresh-token/password-reset/email-action-token expiry được lưu trong MySQL phải là Vietnam wall-clock và được so sánh với VietnamNow.
- JWT token phải tiếp tục interoperable với chuẩn NumericDate/Unix time; convert từ MySQL Vietnam wall-clock sang UTC/Unix đúng instant tại boundary.
- OAuth/Google raw payload vẫn được parse theo contract provider; nếu lưu snapshot vào MySQL `DATETIME`, phải convert sang Vietnam wall-clock trước khi persistence.
- Việc convert không được kéo dài hoặc rút ngắn token lifetime 7 giờ.
- Không log raw token/secret trong báo cáo timezone.
- Không dùng client-supplied timezone để thay đổi audit timestamp của server.
- Server tự lấy VietnamNow cho created/submitted/decided/cancelled timestamps.
- Không cho payload spoof các audit fields.

---

## 14. Test requirements

Test phải đủ để chứng minh policy hoạt động nhưng không tạo test trùng lặp không cần thiết.

### 14.1. Unit tests

1. `VietnamTime` trả đúng UTC+7 khi host OS chạy UTC.
2. `VietnamNow` có `DateTimeKind` đúng theo persistence policy.
3. UTC 13:00 → Vietnam wall-clock 20:00.
4. Chuyển qua ngày/tháng/năm, ví dụ UTC 18:30 ngày 31/12 → Vietnam 01:30 ngày 01/01.
5. Validation 24/72 giờ tại boundary.
6. API DTO conversion thành `+07:00`.
7. Frontend formatter với ISO `+07:00`.
8. Invalid/null timestamp fallback.
9. JWT NumericDate → Vietnam wall-clock DB → JWT NumericDate round-trip giữ nguyên Unix timestamp.
10. OAuth `expires_in = 3600` → MySQL expiry Vietnam time đúng một giờ, không thành tám giờ hoặc âm sáu giờ.

### 14.2. Integration tests với MySQL test database thật

Test ít nhất:

1. Tạo Visit Request tại fake Vietnam time 20:00.
2. Assert DB `submitted_at` và `created_at` lưu 20:00, không phải 13:00.
3. Assert API trả `20:00:00+07:00`.
4. Approve/reject/cancel/resubmit lưu đúng Vietnam time.
5. `CURRENT_TIMESTAMP`/`ON UPDATE CURRENT_TIMESTAMP` trên application connection dùng `+07:00`.
6. Planned start/end round-trip không drift.
7. Reminder được claim/dispatch đúng giờ Việt Nam khi process OS timezone là UTC.
8. Duplicate window vẫn đúng số phút sau khi đổi clock.
9. OTP/session/token-expiry MySQL fields lưu Vietnam wall-clock và comparison dùng VietnamNow.
10. JWT/OAuth tests hiện có vẫn pass, Unix timestamp/lifetime không lệch khi round-trip qua MySQL Vietnam time.

Không dùng production database. Dùng `pems_test` hoặc infrastructure test hiện có.

### 14.3. Frontend/component/Playwright tests

Chạy test dưới ít nhất hai browser timezone nếu infrastructure hỗ trợ:

- `Asia/Ho_Chi_Minh`.
- `America/New_York` hoặc UTC.

Cả hai phải hiển thị “Ngày gửi: 20:00” cho payload `2026-07-02T20:00:00+07:00`.

Cover:

- Submitted Visit Request modal.
- Decision/cancellation timestamp.
- Duplicate timestamp.
- Edit/resubmit `datetime-local` không drift.
- Planned visit schedule không cộng thêm 7 giờ.
- Locale VI/EN không thay đổi timezone.

### 14.4. Regression/build

Chạy tối thiểu:

```bash
dotnet build
dotnet test
npm run build
npm run lint
```

Chạy Playwright/frontend tests hiện có nếu repository đã cấu hình.

Không báo “all tests pass” nếu chỉ chạy subset. Ghi rõ số lượng pass/fail/skip cho từng test project.

---

## 15. Trình tự triển khai bắt buộc

### Phase 1 — Audit, chưa sửa

- Lập inventory backend/frontend/database.
- Phân loại từng field/time occurrence.
- Xác nhận baseline failing case 20:00 → 13:00.
- Chỉ ra những chỗ không đủ bằng chứng để migrate.

### Phase 2 — Time foundation

- Chuẩn hóa `VietnamTime`/clock abstraction.
- Thêm MySQL session timezone handling.
- Thêm shared frontend date/time utility.
- Viết foundation tests.

### Phase 3 — Visit Request vertical slice

- Sửa create/edit/resubmit/approve/reject/cancel timestamps.
- Sửa submitted detail DTO/API/modal.
- Sửa duplicate, decision và cancellation display.
- Chứng minh DB/API/UI đều là 20:00 Vietnam time.

### Phase 4 — Cross-module rollout

- News, feedback, email, notification.
- Minutes, logistics, agenda, reminder.
- Account, department, campus, partner, document, gallery, FAQ, report.
- Security/session/OTP được chuyển toàn bộ MySQL `DATETIME` sang Vietnam wall-clock; JWT/OAuth chỉ giữ UTC/Unix trong raw protocol boundary.

### Phase 5 — SQL alignment và dữ liệu cũ

- Update fresh-create SQL.
- Sinh patch existing DB riêng nếu đủ bằng chứng.
- Không chạy patch production.

### Phase 6 — Verification và report

- Build/test full.
- Báo remaining risks và manual deployment steps.

---

## 16. Acceptance criteria

### AC-01 — Create Visit Request

```gherkin
Given thời điểm hiện tại tại Việt Nam là 20:00
And API server chạy ở UTC
When Visitor gửi đơn thành công
Then visit_requests.submitted_at lưu 20:00
And created_at lưu 20:00
And API trả submittedAt có offset +07:00
And UI hiển thị 20:00.
```

### AC-02 — Browser timezone độc lập

```gherkin
Given API trả 2026-07-02T20:00:00+07:00
When trang được mở trên browser timezone America/New_York
Then UI vẫn hiển thị 20:00 giờ Việt Nam.
```

### AC-03 — Planned schedule không drift

```gherkin
Given planned_start_at là 09:00 giờ Việt Nam
When Visitor mở Edit và lưu mà không thay đổi thời gian
Then database vẫn là 09:00
And UI vẫn là 09:00
And không thành 02:00 hoặc 16:00.
```

### AC-04 — 24-hour boundary

```gherkin
Given VietnamNow cố định
When planned start đúng bằng VietnamNow + 24 giờ
Then rule xử lý đúng theo đặc tả
And kết quả không phụ thuộc timezone của OS server.
```

### AC-05 — MySQL default

```gherkin
Given application mở một pooled MySQL connection
When insert/update sử dụng CURRENT_TIMESTAMP
Then session timezone là +07:00
And giá trị sinh ra là Vietnam wall-clock.
```

### AC-06 — Security database time đồng bộ nhưng protocol không bị phá

```gherkin
Given một JWT/OAuth token có expiry chuẩn UTC/Unix time
When PEMS lưu expiry snapshot vào MySQL
Then cột MySQL lưu thời gian tương đương theo giờ Việt Nam
And mọi comparison nội bộ trên cột đó dùng VietnamNow
When PEMS phát lại JWT hoặc xử lý protocol provider
Then giá trị được convert đúng về UTC/Unix
And Unix instant/lifetime không tăng hoặc giảm 7 giờ.
```

### AC-07 — OTP/session MySQL time

```gherkin
Given VietnamNow là 20:00
And một session có lifetime 30 phút
When session được tạo
Then sessions.created_at lưu 20:00 giờ Việt Nam
And sessions.expires_at lưu 20:30 giờ Việt Nam
And session còn hiệu lực lúc 20:29:59
And session hết hiệu lực đúng 20:30:00
And kết quả không phụ thuộc timezone của API server.
```

---

## 17. Output/report format sau khi hoàn thành

Báo cáo bắt buộc gồm:

### 17.1. Root cause

- Vì sao 20:00 bị hiển thị/lưu thành 13:00.
- Writer, DB type, API serialization và frontend formatter liên quan.

### 17.2. Inventory và quyết định

| Module/table/field | Trước | Sau | Vietnam/UTC | Lý do |
|---|---|---|---|---|

### 17.3. Files changed

| Layer | File | Change |
|---|---|---|
| Backend Application | ... | ... |
| Infrastructure | ... | ... |
| Database | ... | ... |
| Frontend | ... | ... |
| Tests | ... | ... |

### 17.4. SQL artifacts

- Fresh-create SQL đã cập nhật gì.
- Patch existing DB có được tạo hay không.
- Nếu có patch: precheck, columns converted, exclusions và cách chạy an toàn.
- Xác nhận patch chưa tự động chạy vào production.

### 17.5. Verification

- Kết quả DB cho case 20:00.
- JSON API thực tế.
- UI thực tế trên ít nhất timezone Việt Nam và một timezone khác.
- Kết quả build/test với số lượng cụ thể.

### 17.6. Remaining risks

- Field chưa đủ bằng chứng.
- Dữ liệu cũ bị trộn timezone.
- Deployment step cần Railway/MySQL configuration.
- Test chưa chạy được và lý do.

---

## 18. Definition of Done

Task chỉ hoàn thành khi:

- [ ] Có inventory date/time backend, frontend và SQL.
- [ ] Có policy rõ ràng: toàn bộ MySQL `DATETIME` là Vietnam wall-clock; UTC/Unix chỉ tồn tại tại raw protocol/integration boundary.
- [ ] Không còn `DateTime.Now` trong production business logic thuộc scope.
- [ ] MySQL application session dùng `+07:00` ổn định với connection pool.
- [ ] Visit Request mới gửi lúc 20:00 lưu DB 20:00.
- [ ] API trả timestamp Việt Nam với `+07:00`.
- [ ] UI luôn hiển thị Asia/Ho_Chi_Minh, không phụ thuộc browser timezone.
- [ ] Planned/agenda/logistics/reminder không bị double conversion.
- [ ] Boundary 24/72 giờ dùng VietnamNow.
- [ ] OTP/session/token-expiry fields trong MySQL lưu giờ Việt Nam và được so sánh với VietnamNow.
- [ ] JWT/OAuth/external protocol vẫn dùng UTC/Unix đúng chuẩn tại boundary và giữ nguyên lifetime.
- [ ] Fresh-create SQL được đồng bộ.
- [ ] Không migrate dữ liệu cũ không đủ bằng chứng.
- [ ] Unit, integration, frontend build/lint và test phù hợp đã chạy.
- [ ] Báo cáo đúng files changed, test results và remaining risks.

---

## 19. Lệnh bắt đầu dành cho AI Agent

Hãy bắt đầu bằng Phase 1 — audit, chưa sửa code ngay.

Trước tiên trả về:

1. Root cause đã xác minh của case 20:00 → 13:00.
2. Inventory các cách lấy/ghi/format time hiện có.
3. Bảng phân loại MySQL datetime fields theo Vietnam audit/Vietnam business/Vietnam security/date-only/unknown; không chấp nhận cột MySQL `DATETIME` UTC làm target state.
4. Danh sách file dự kiến sửa theo layer.
5. Rủi ro migration dữ liệu cũ.
6. Kế hoạch test tối ưu.

Sau khi đã có evidence từ source, tiếp tục implement toàn bộ thay đổi an toàn trong cùng task. Nếu gặp dữ liệu cũ bị trộn timezone hoặc không thể chứng minh cột nào đang lưu UTC, không tự đoán và không chạy conversion hàng loạt; ghi rõ blocker trong báo cáo.
