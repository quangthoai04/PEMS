# PROMPT — HOÀN THIỆN, RÀ SOÁT VÀ NGHIỆM THU CUỐI CHUẨN HÓA GIỜ VIỆT NAM TRÊN NHÁNH HIỆN TẠI

## 1. Vai trò của AI Agent

Bạn là Senior Full-stack Engineer và QA/Security Reviewer của dự án PEMS, gồm:

- Senior ASP.NET Core .NET 8 / Clean Architecture Engineer.
- Senior React Vite TypeScript Engineer.
- Database-first MySQL 8 Engineer.
- Authentication/JWT/OAuth Security Reviewer.
- Test Engineer phụ trách Unit, Integration và Playwright.

Nhiệm vụ của bạn là **tiếp tục trên nhánh hiện tại**, đọc toàn bộ thay đổi chuẩn hóa múi giờ đã có, xác minh báo cáo hiện tại bằng source/test thật, sửa nốt các thiếu sót và đưa thay đổi đến trạng thái có thể nghiệm thu.

Đây là task hoàn thiện phần đã triển khai, **không được viết lại toàn bộ từ đầu** nếu code hiện tại đã đúng.

---

## 2. Bối cảnh dự án

PEMS là Partnership/Event/Reception/Engagement Management System của FPT University.

Tech stack:

- Backend: ASP.NET Core .NET 8, Clean Architecture, MediatR, EF Core, Pomelo MySQL.
- Frontend: React, Vite, TypeScript, Tailwind CSS.
- Database: MySQL 8, database-first, fresh-create/manual SQL.
- Authentication: JWT và Google OAuth/SSO.

Quyết định kiến trúc thời gian đã được chốt:

1. Tất cả cột MySQL `DATETIME` do PEMS quản lý lưu **Vietnam wall-clock**, tức giờ `Asia/Ho_Chi_Minh`/UTC+07:00.
2. Backend nghiệp vụ tạo và so sánh timestamp persistence bằng nguồn giờ Việt Nam tập trung.
3. API trả timestamp nghiệp vụ kèm offset `+07:00`, tránh chuỗi thời gian mơ hồ.
4. Frontend hiển thị cố định theo `Asia/Ho_Chi_Minh`, không phụ thuộc timezone của máy người dùng.
5. JWT/OAuth chỉ giữ UTC/Unix theo tiêu chuẩn **tại ranh giới giao thức**; khi lưu snapshot/expiry vào MySQL PEMS phải chuyển thành Vietnam wall-clock.
6. Không cộng/trừ 7 giờ thủ công trong application/frontend runtime.

Ví dụ chuẩn:

```text
Người dùng gửi lúc: 20:00 Việt Nam
MySQL submitted_at: 2026-07-02 20:00:00
API submittedAt:    2026-07-02T20:00:00+07:00
UI:                 20:00 02/07/2026

JWT exp: Unix timestamp chỉ đúng cùng instant 21:00 Việt Nam nếu token sống 1 giờ
MySQL token_expires_at: 2026-07-02 21:00:00
OAuth expires_in: 3600 giây, không phải một timezone
```

---

## 3. Báo cáo implementation hiện tại cần xác minh

Agent trước báo cáo đã thực hiện:

- Mở rộng `VietnamTime.cs` với `Now()`, `FromUtc`, `ToUtc`, `ToOffset`.
- Thêm `VietnamTimeZoneConnectionInterceptor.cs`, chạy `SET time_zone = '+07:00'` mỗi lần mở MySQL connection.
- Thêm `VietnamDateTimeJsonConverter.cs`, serialize `DateTime` thành chuỗi có `+07:00`.
- Đăng ký converter cho API và luồng JSON do exception middleware tự serialize.
- Sweep khoảng 145 file backend từ `_clock.UtcNow`, `DateTime.UtcNow`, `DateTime.Now` sang nguồn giờ Việt Nam tại các vị trí persistence/nghiệp vụ.
- Giữ UTC ở `JwtTokenService`, Google token validation/JWKS cache, OCR/in-memory cache.
- Bỏ `AddHours(7)`, `AddHours(-7)` trong report handlers và bỏ helper `AsUtc` ở minutes.
- Đổi contract `retryAtUtc` thành `retryAt` ở backend/frontend.
- Thêm `vietnamTime.ts` và chuyển các màn ưu tiên sang formatter/parser dùng `Asia/Ho_Chi_Minh`.
- Thêm/chỉnh fresh-create SQL và tạo `pems_patch_vietnam_time_standardization.sql` nhưng chưa chạy.
- Báo cáo test: build thành công; Unit 211/211; Integration 160/160; Architecture 14/14; Playwright 55/55.
- Còn một số màn lịch nội bộ dùng browser-local date getters.
- Dữ liệu cũ ở một số cột đang có khả năng trộn giữa UTC và Vietnam wall-clock.

**Không được tin báo cáo này chỉ bằng lời. Phải đối chiếu từng nhóm claim với source, diff và test thật trên đúng nhánh hiện tại.**

---

## 4. Tài liệu và source bắt buộc đọc trước

Đọc và đối chiếu theo mức liên quan:

1. `PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md`.
2. `CLEAN_ARCHITECTURE.md`.
3. `PEMS_v10_NEW_FINAL_SQL_TABLE_FIELD_DICTIONARY_MATCHED.docx`.
4. `PEMS_PROMPT_GENERATION_RULES.md`.
5. `VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md`.
6. `PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md`.
7. `PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md`.
8. `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`.
9. `PROJECT_STRUCTURE_FULL.md`.
10. SQL fresh-create mới nhất trong repository.
11. Patch `pems_patch_vietnam_time_standardization.sql` nếu đã tồn tại.
12. Toàn bộ source backend/frontend/test liên quan trực tiếp đến timestamp.

Khi tài liệu, code hoặc SQL mâu thuẫn, ưu tiên:

1. Quyết định thời gian trong prompt này.
2. SQL fresh-create mới nhất.
3. SQL Table & Field Dictionary mới nhất.
4. Canonical Business Rules.
5. UC Implementation Rulebook.
6. Source hiện tại sau khi xác minh luồng ghi/đọc thật.

Không được bịa file, table, column, API, enum hoặc test. Nếu tên trong báo cáo không tồn tại, search source để tìm implementation tương đương và ghi rõ sai khác.

---

## 5. Quy tắc Git và an toàn source

Trước khi sửa, chạy và ghi nhận:

```bash
git status --short --branch
git branch --show-current
git log --oneline --decorate -n 15
git diff --stat
git diff
```

Nếu branch có upstream/base phù hợp, xác định diff của nhánh hiện tại so với base/`Dev` bằng lệnh không phá hủy.

Yêu cầu:

- Làm trên đúng nhánh hiện tại.
- Không tự chuyển branch.
- Không reset, checkout bỏ thay đổi, clean hoặc ghi đè thay đổi chưa commit.
- Không sửa nội dung ngoài scope.
- Không tự merge/rebase với `Dev`.
- Không commit, push hoặc mở PR nếu người dùng chưa yêu cầu.
- Không tự chạy migration patch trên database dev/staging/production.

---

## 6. Mục tiêu cuối cùng

Hoàn thiện hệ thống để bảo đảm:

1. Timestamp do PEMS persistence ghi vào MySQL `DATETIME` là giờ Việt Nam.
2. Timestamp từ database được hiểu là Vietnam wall-clock, không bị EF/backend/frontend dịch thêm hoặc bớt 7 giờ.
3. API timestamp nghiệp vụ không còn chuỗi mơ hồ thiếu offset.
4. UI hiển thị đúng giờ Việt Nam trên cả browser Việt Nam và browser timezone nước ngoài.
5. Planned/agenda/business wall-clock không bị dịch ngày hoặc giờ.
6. JWT NumericDate và OAuth protocol vẫn chuẩn, không bị cộng 7 giờ sai instant.
7. OTP/session/token expiry snapshot trong database dùng giờ Việt Nam và được so sánh với `VietnamNow`.
8. Report/filter/calendar/export sử dụng cùng một quy ước.
9. Dữ liệu cũ chỉ được migrate khi biết chắc nguồn; không sửa mù các cột hỗn hợp.
10. Có bằng chứng test thật cho toàn bộ acceptance criteria.

---

## 7. Quy trình phân tích bắt buộc trước khi code

### 7.1. Lập inventory writer/reader

Search toàn repository tối thiểu các pattern:

```text
DateTime.Now
DateTime.UtcNow
DateTimeOffset.Now
DateTimeOffset.UtcNow
_clock.UtcNow
_clock.VietnamNow
VietnamTime
AddHours(7)
AddHours(-7)
ToUniversalTime
ToLocalTime
SpecifyKind
DateTimeKind.Utc
DateTimeKind.Local
DateTimeKind.Unspecified
UTC_TIMESTAMP
NOW()
CONVERT_TZ
SET time_zone
toLocaleString
toLocaleDateString
toLocaleTimeString
new Date(
+ 'Z'
+ "Z"
getDate/getHours/getDay/getMonth/getFullYear
setDate/setHours
datetime-local
retryAtUtc
```

Không thay thế cơ học. Với mỗi kết quả phải phân loại:

- PEMS database persistence.
- Business/planned wall-clock.
- API serialization/deserialization.
- JWT/OAuth/external protocol boundary.
- In-memory cache/non-persistent technical clock.
- Test/fixture/seed.
- Dead code/comment/documentation.

### 7.2. Lập ma trận thời gian

Tạo bảng audit trong báo cáo hoặc tài liệu phù hợp:

| Field/flow | Table/DTO | Writer hiện tại | Ý nghĩa | Quy ước đúng | Reader/comparison | Migration class |
|---|---|---|---|---|---|---|
| submitted_at | xác minh từ SQL | handler/service | audit nghiệp vụ | VN wall-clock | API/UI/report | UTC-only/VN-only/mixed/unknown |

Bao phủ tối thiểu:

- Visit request create/submit/edit/resubmit/cancel/approve/reject.
- Visit campus lifecycle và host assignment.
- Agenda/planned start/end.
- Feedback, minutes, logistics, invitations, participants.
- News, gallery/TTS, FAQ, notification.
- User/account audit fields.
- OTP issue/verify/expiry/attempt/rate-limit.
- Session create/refresh/revoke/expiry.
- Password reset và email-action tokens.
- Security audit/log.
- JWT creation/validation.
- Google OAuth token validation và expiry snapshot.
- Reports, filters, exports và calendar.

### 7.3. Báo cáo baseline trước khi sửa

Trước khi code, tóm tắt:

- Nhánh hiện tại và trạng thái working tree.
- Những claim nào trong báo cáo đã xác minh đúng.
- Những claim nào chưa đúng hoặc chưa đủ bằng chứng.
- File/màn/flow còn sót.
- Rủi ro dữ liệu cũ.
- Kế hoạch sửa tối thiểu.

Sau đó tiếp tục triển khai các thay đổi an toàn trong scope; chỉ dừng hỏi người dùng nếu gặp thao tác phá hủy hoặc cần chạy patch lên database thật.

---

## 8. Quy tắc Foundation và backend

### 8.1. Nguồn giờ chuẩn

- `VietnamTime.Now()`/`IClock.VietnamNow` phải trả Vietnam wall-clock với `DateTimeKind.Unspecified` để khớp MySQL `DATETIME`.
- Chuyển đổi phải dựa trên timezone/offset và giữ đúng instant.
- `FromUtc(ToUtc(vietnamWallClock))` phải round-trip đúng.
- Không dùng `AddHours(7)`/`AddHours(-7)` trong application runtime.
- Nếu nhận `DateTimeKind.Local`, không được ngầm coi đó là Việt Nam; xử lý rõ hoặc reject tùy contract.
- Ưu tiên `DateTimeOffset` tại ranh giới có instant/offset nếu việc đó phù hợp code hiện tại; không refactor tràn lan nếu không cần.

### 8.2. Persistence và comparison

- Mọi `CreatedAt`, `UpdatedAt`, `SubmittedAt`, `ApprovedAt`, `RejectedAt`, `CancelledAt`, expiry snapshot và audit field do PEMS ghi vào MySQL `DATETIME` phải là Vietnam wall-clock.
- So sánh DB expiry/lifecycle phải dùng cùng quy ước Vietnam wall-clock.
- Không được ghi UTC vào `DATETIME` rồi kỳ vọng frontend tự cộng 7 giờ.
- Không được chuyển planned/agenda time sang UTC nếu nghiệp vụ đang định nghĩa đó là giờ Việt Nam tại cơ sở.
- Kiểm tra EF mapping/value converter/interceptor để bảo đảm không có một lớp khác tự đổi timezone lần nữa.

### 8.3. MySQL connection timezone

Xác minh interceptor:

- Chạy `SET time_zone = '+07:00'` cho **mỗi physical/session connection được mở**.
- Hoạt động đúng với connection pooling.
- Áp dụng cho runtime API và test factory.
- Không cần `SET GLOBAL time_zone`.
- Không dựa vào timezone của OS/container/Railway.
- Không tạo SQL injection hoặc retry loop.

Lưu ý: session timezone ảnh hưởng `NOW()`, `CURRENT_TIMESTAMP` và `TIMESTAMP`; bản thân `DATETIME` không lưu timezone. Vì vậy application vẫn phải ghi đúng Vietnam wall-clock.

### 8.4. Không sweep mù

Không thay toàn bộ `UtcNow` thành `VietnamNow` nếu vị trí đó là:

- JWT NumericDate.
- OAuth/JWK/provider timestamp.
- TLS/HTTP/external protocol timestamp.
- In-memory technical cache cần instant tuyệt đối.

Mỗi ngoại lệ giữ UTC phải có lý do rõ trong code/report; không dùng lý do chung chung “security luôn UTC”.

---

## 9. JWT và OAuth — quy tắc bắt buộc

### 9.1. JWT

Các claim `iat`, `nbf`, `exp` phải là NumericDate/Unix timestamp theo chuẩn JWT.

Yêu cầu:

- Tạo và validate JWT dựa trên UTC/Unix hoặc `DateTimeOffset` đúng instant.
- Không đưa Vietnam wall-clock `Kind=Unspecified` trực tiếp cho JWT library như thể đó là UTC.
- Không cộng `7 * 3600` vào Unix timestamp.
- Lifetime phải giữ nguyên. Token 60 phút phải thực sự hết hạn sau đúng 60 phút.
- Nếu PEMS lưu `token_expires_at` trong MySQL, giá trị đó phải là Vietnam wall-clock của cùng instant.

Ví dụ:

```text
Issued: 20:00 VN = 13:00 UTC = Unix A
Expires: 21:00 VN = 14:00 UTC = Unix B
JWT exp = Unix B
DB token_expires_at = 21:00 VN
```

### 9.2. OAuth/Google

- Giữ nguyên raw provider contract.
- `expires_in` là thời lượng giây, không có timezone.
- Không cộng/trừ 7 giờ vào `expires_in`.
- Nếu nhận absolute provider timestamp có UTC/offset, parse thành instant rồi mới chuyển sang Vietnam wall-clock khi persist vào PEMS `DATETIME`.
- JWKS/OIDC/in-memory cache có thể dùng UTC instant nếu không persist vào PEMS database.

Ví dụ:

```text
VietnamNow = 20:00
expires_in = 3600
DB expires_at = 21:00 Vietnam wall-clock
```

### 9.3. Security persistence

OTP, session, refresh-session snapshot, password reset, email-action token và security audit **lưu trong MySQL PEMS** phải dùng giờ Việt Nam theo quyết định dự án.

Không được nhầm:

- “JWT bên trong protocol dùng Unix”

với:

- “mọi dữ liệu bảo mật trong database phải lưu UTC”.

Quyết định thứ hai không áp dụng cho PEMS.

---

## 10. API JSON contract

Xác minh global converter và mọi JSON serialization path:

- Timestamp nghiệp vụ trả ra dạng ISO 8601 có `+07:00`, ví dụ `2026-07-02T20:00:00+07:00`.
- Không trả chuỗi `2026-07-02T20:00:00` mơ hồ cho timestamp có thời gian.
- Input có offset phải được chuyển đúng instant sang Vietnam wall-clock trước khi persistence.
- Input không offset từ `datetime-local` được hiểu là Vietnam wall-clock theo contract PEMS.
- `Kind=Utc` phải convert đúng instant sang `+07:00`, không chỉ đổi nhãn `Kind`.
- Không double-convert giá trị đã là Vietnam wall-clock.
- `DateOnly` và field chỉ có ngày không bị dịch ngày.
- Nullable values và min/max/default date được xử lý an toàn.
- `ProblemDetails`, validation error và exception middleware dùng cùng JSON options nếu có timestamp.
- Swagger/OpenAPI/example/type frontend phải phản ánh contract mới nếu cần.

Sửa lại mô tả root cause trong tài liệu/report nếu đang nói rằng cả raw string và chuỗi nối `Z` đều luôn hiển thị 13:00. Mô tả đúng là frontend cũ có nhiều cách parse không nhất quán, dẫn tới kết quả khác nhau theo màn hình và browser timezone.

---

## 11. Frontend — hoàn tất sweep, không để residual không kiểm soát

### 11.1. Utility chuẩn

Xác minh `vietnamTime.ts` hoặc utility tương đương:

- `parseApiDate`.
- `formatVietnamDate`.
- `formatVietnamDateTime`.
- `formatVietnamTime`.
- `toVietnamDateTimeLocalInput`.
- `vietnamNowDateTimeLocal`.
- `formatVietnamRelative`.

Yêu cầu:

- Sử dụng `Intl.DateTimeFormat` với `timeZone: 'Asia/Ho_Chi_Minh'` hoặc thuật toán tương đương đã test.
- Không phụ thuộc timezone máy/browser.
- Không nối `Z` thủ công.
- Không cộng/trừ 7 giờ bằng milliseconds.
- Không parse `YYYY-MM-DD` thành instant nếu field chỉ là calendar date.
- Không dùng `toISOString()` trực tiếp để fill `datetime-local` vì có thể đổi ngày/giờ.

### 11.2. Hoàn tất các màn còn sót

Báo cáo trước nói một số màn như calendar nội bộ/task grid còn dùng browser-local getter. Trong task này phải:

1. Search lại toàn frontend.
2. Liệt kê chính xác từng residual.
3. Chuyển tất cả màn PEMS có hiển thị/xếp ngày giờ nghiệp vụ sang utility chuẩn nếu an toàn.
4. Nếu có residual buộc phải giữ, phải chứng minh đó là date-only hoặc browser-local theo nghiệp vụ, thêm comment/test và ghi rõ lý do; không được chỉ nói “chấp nhận được vì người dùng thường ở Việt Nam”.

Bao phủ:

- Visitor list/detail/edit/resubmit.
- Staff/Staff Leader/HO/Department dashboards.
- Calendar month/week/day và task grids.
- Agenda preview/editor.
- Reports/filter/export.
- Notifications/relative time.
- OTP/session retry countdown.
- News/gallery/feedback/minutes/logistics/invitations.
- `datetime-local` min/max/default/validation 24h/72h.

### 11.3. Compatibility

- Frontend mới phải đọc được API mới có `+07:00`.
- Nếu giữ compatibility cho chuỗi trần cũ, phải document rằng chuỗi đó được hiểu theo quy tắc nào.
- Compatibility parser không được che giấu dữ liệu legacy UTC đang bị lưu như `DATETIME`; đó là vấn đề migration, không phải trách nhiệm UI đoán từng row.

---

## 12. Database và dữ liệu cũ

### 12.1. Fresh-create SQL

Xác minh SQL fresh-create mới nhất:

- Có policy header giải thích `DATETIME = Vietnam wall-clock`.
- Thiết lập session `time_zone = '+07:00'` ở nơi phù hợp.
- `NOW()`/`CURRENT_TIMESTAMP` được hiểu theo session Việt Nam.
- Không còn dùng `UTC_TIMESTAMP()` cho cột PEMS `DATETIME` theo policy mới.
- Trigger/procedure/event/seed cũng tuân thủ policy.
- Không thay schema/table/column ngoài nhu cầu timezone.

### 12.2. Ma trận migration bắt buộc

Phân loại từng cột/tập row:

1. `UTC_ONLY_CONFIRMED`: writer lịch sử chắc chắn ghi UTC.
2. `VIETNAM_ONLY_CONFIRMED`: writer lịch sử chắc chắn ghi Vietnam wall-clock.
3. `MIXED`: có nhiều writer hoặc đã đổi logic giữa chừng.
4. `UNKNOWN`: chưa đủ bằng chứng.
5. `PROTOCOL_ONLY`: Unix/raw external value, không phải PEMS wall-clock column.

Chỉ `UTC_ONLY_CONFIRMED` mới được đưa vào câu lệnh convert tự động.

Không được blanket-update toàn bộ timestamp bằng `+7 giờ`.

Đối với `MIXED`/`UNKNOWN`:

- Tìm cutoff deploy bằng commit/deploy log nếu có bằng chứng.
- Nếu không có bằng chứng, giữ nguyên và tạo danh sách cần xử lý thủ công/reimport.
- Không tự chọn cutoff theo phỏng đoán.

### 12.3. Patch database hiện có

Patch phải:

- Có precheck và postcheck.
- Có guard rõ, ví dụ `@PEMS_CONFIRM`.
- Mặc định không chạy mutation nếu chưa confirm.
- Chạy trong transaction khi MySQL/DDL cho phép.
- Có row-count trước/sau.
- Idempotent hoặc có cơ chế chống chạy hai lần.
- Có backup/rollback guidance.
- Ghi rõ cột nào convert và bằng chứng writer cũ.
- Không tự chạy bởi app startup.
- Không được Agent tự thực thi lên database thật.

Nếu cần convert UTC chắc chắn sang VN và named timezone tables không bảo đảm tồn tại, có thể dùng offset rõ `CONVERT_TZ(value, '+00:00', '+07:00')` trong **one-time audited migration**, nhưng không đưa cách cộng/trừ này vào application runtime.

### 12.4. Chiến lược môi trường

- Development/test: ưu tiên import lại fresh-create mới nếu dữ liệu không cần giữ.
- Production/staging: backup trước; migrate chỉ nhóm chắc chắn; deploy backend/frontend đồng thời.
- OTP/session cũ: ưu tiên revoke/invalidate tất cả và yêu cầu đăng nhập/xin OTP lại thay vì cố dịch các record đang sống nếu không chắc chắn.
- Không log token, OTP, refresh token hoặc dữ liệu bí mật trong migration/report.

---

## 13. Validation và business rules

Xác minh các rule phụ thuộc hiện tại:

- Edit/resubmit/cancel trước 24 giờ.
- Tạo lịch/agenda trước tối thiểu 72 giờ nếu business rule hiện tại yêu cầu.
- OTP resend/rate-limit/recovery window.
- Session/token expiration.
- Email-action/password reset expiry.
- Report date-range inclusive/exclusive.
- Calendar ngày bắt đầu/kết thúc qua nửa đêm.

Mỗi rule phải so sánh các giá trị cùng convention. Không được so `Vietnam wall-clock` với UTC/Unix trực tiếp mà chưa convert tại boundary.

Test các case sát mốc:

- Đúng bằng cutoff.
- Trước cutoff một giây/phút.
- Sau cutoff một giây/phút.
- Qua ngày/tháng/năm.
- `00:30`/`01:30` giờ Việt Nam khi browser ở New York.

---

## 14. Authorization và security

- Không thay đổi role/authorization nghiệp vụ ngoài scope.
- Không dùng dynamic permission tables đã loại bỏ.
- Không làm yếu JWT validation, issuer, audience, signature, lifetime hoặc clock skew.
- Không kéo dài lifetime token khi chuyển timezone.
- Không log JWT, OTP, OAuth access/refresh token.
- Không expose raw provider token cho frontend.
- Không dùng client time làm nguồn tin cậy cho authorization/expiry phía server.
- Rate limit và expiry vẫn phải do backend/database kiểm tra.

---

## 15. Test requirements

Không viết test giả chỉ để pass. Test phải gọi implementation thật và assert output/state thật.

### 15.1. Unit tests

Bao phủ tối thiểu:

- UTC 13:00 chuyển thành VN 20:00 cùng ngày.
- Chuyển đổi qua ngày/tháng/năm.
- Round-trip VN → UTC → VN giữ đúng instant/wall-clock.
- `DateTimeKind.Utc`, `Unspecified`, `Local` theo contract.
- JSON serialize thành `+07:00`.
- Deserialize chuỗi có `Z`, `+07:00`, offset khác và chuỗi trần.
- `DateOnly` không dịch ngày.
- JWT Unix round-trip giữ instant.
- JWT 60 phút hết hạn đúng 60 phút.
- OAuth `expires_in=3600` tạo DB expiry đúng một giờ sau theo giờ Việt Nam.
- OTP/session expiry comparison.
- Frontend formatter/parser/datetime-local utility.

### 15.2. Integration tests với MySQL thật

Bao phủ:

- `@@session.time_zone = '+07:00'` ở nhiều DbContext scope/pooled connections.
- `TIMESTAMPDIFF(MINUTE, UTC_TIMESTAMP(), NOW()) = 420` trong điều kiện test phù hợp.
- POST visit request ghi `submitted_at`/`created_at` gần `VietnamNow`, không gần UTC wall-clock.
- API trả `+07:00`.
- Planned/agenda round-trip không drift.
- OTP/session/token expiry được ghi và so sánh theo VN wall-clock.
- Report/filter không double-shift.
- Converter áp dụng cả response bình thường và error response có timestamp nếu có.

Không chỉ assert status code; query database và assert timestamp thực.

### 15.3. Playwright/E2E

Chạy cùng fixture/payload dưới tối thiểu:

- `Asia/Ho_Chi_Minh`.
- `America/New_York`.

Assert:

- Cả hai browser hiển thị cùng giờ/ngày Việt Nam.
- Case `01:30` Việt Nam không bị lùi sang ngày trước.
- Calendar xếp đúng ô ngày.
- `datetime-local` default/min không drift.
- Submitted time 20:00 hiển thị 20:00.
- Không còn màn ưu tiên/residual dùng browser timezone ngoài chủ đích.

### 15.4. Full verification

Chạy full suite, không chỉ subset, theo cấu trúc repository thật:

```text
dotnet build full solution
PEMS.UnitTests
PEMS.IntegrationTests
PEMS.ArchitectureTests
frontend build
frontend lint/typecheck
Playwright full suite
```

Nếu test cần MySQL/service nhưng môi trường không có, không được báo pass. Ghi rõ lệnh, lỗi/blocker và những gì chưa xác minh.

---

## 16. Manual smoke test sau deploy

Chuẩn bị checklist, không tự deploy:

1. Gửi đơn lúc khoảng 20:00 Việt Nam.
2. Kiểm tra MySQL lưu khoảng 20:00.
3. Kiểm tra API trả khoảng `20:00:00+07:00`.
4. Kiểm tra UI hiển thị 20:00 trên browser VN.
5. Đổi browser timezone sang New York, UI vẫn hiển thị 20:00 Việt Nam.
6. Tạo OTP/token thời hạn ngắn trong môi trường test và xác minh hết hạn đúng duration.
7. Đăng nhập JWT và xác minh lifetime không tăng/giảm 7 giờ.
8. Kiểm tra planned/agenda time không đổi.
9. Kiểm tra report/calendar/filter cùng dữ liệu.

---

## 17. Phạm vi được sửa

Được sửa khi có bằng chứng cần thiết:

- Foundation/time abstraction.
- Infrastructure MySQL interceptor/DI.
- API JSON converter/options/middleware/OpenAPI.
- Application handlers/services/validators liên quan timestamp.
- Security persistence và protocol boundary.
- Frontend time utility và consumers.
- SQL fresh-create và patch migration an toàn.
- Unit/Integration/Architecture/Playwright tests.
- Tài liệu timezone/root-cause/deployment checklist trực tiếp liên quan.

---

## 18. Phạm vi không được sửa

- Không redesign UI ngoài nhu cầu hiển thị thời gian.
- Không thay đổi role, campus scope, department scope hoặc business workflow.
- Không đổi schema không liên quan.
- Không thêm thư viện mới nếu platform API hiện tại đủ dùng, trừ khi chứng minh cần thiết.
- Không sửa seed nghiệp vụ ngoài timestamp cần đồng bộ.
- Không chạy patch database thật.
- Không commit/push/merge/rebase.
- Không xử lý dữ liệu hỗn hợp bằng phỏng đoán.

---

## 19. Format báo cáo cuối bắt buộc

Báo cáo cuối phải gồm:

### A. Git context

- Branch hiện tại.
- Base/upstream dùng để đối chiếu.
- Working tree trước/sau.

### B. Verified baseline

- Claim nào từ báo cáo cũ đúng.
- Claim nào sai/chưa đủ.

### C. Root cause chính xác

- Luồng writer → MySQL → API → frontend.
- Phân biệt raw no-offset và chuỗi có `Z`.

### D. Files changed

Liệt kê theo backend/frontend/database/tests/docs và giải thích ngắn từng file.

### E. Time boundary decisions

- PEMS database persistence.
- Planned time.
- API contract.
- JWT.
- OAuth.
- In-memory cache.

### F. Legacy data matrix

- Danh sách cột `UTC_ONLY_CONFIRMED`, `VIETNAM_ONLY_CONFIRMED`, `MIXED`, `UNKNOWN`, `PROTOCOL_ONLY`.
- Bằng chứng phân loại.
- Row count/precheck nếu có.

### G. Tests

- Lệnh đã chạy.
- Tổng pass/fail/skip thật.
- Không chỉ ghi dấu ✅.

### H. Remaining risks

- Chỉ ghi residual thực sự chưa xử lý, lý do và đề xuất.
- Nếu không còn, ghi rõ không phát hiện residual sau sweep.

### I. Deployment/migration instructions

- Thứ tự backup, deploy BE+FE, migration/revoke session và smoke test.
- Nhắc rõ patch chưa được tự chạy.

---

## 20. Definition of Done

Chỉ báo hoàn thành khi:

- [ ] Đã kiểm tra đúng nhánh hiện tại và không làm mất thay đổi có sẵn.
- [ ] Đã xác minh toàn bộ claim trong báo cáo cũ bằng source/diff/test.
- [ ] Không còn persistence writer PEMS ghi UTC vào MySQL `DATETIME` ngoài ngoại lệ được chứng minh.
- [ ] JWT/OAuth giữ chuẩn protocol và đúng lifetime/instant.
- [ ] API timestamp nghiệp vụ có `+07:00`.
- [ ] Frontend hiển thị cố định giờ Việt Nam ở browser timezone khác nhau.
- [ ] Không còn hack nối `Z`, cộng/trừ 7 giờ hoặc formatter thiếu timezone ngoài trường hợp có lý do/test rõ.
- [ ] Calendar/task grid residual đã được xử lý hoặc chứng minh đúng nghiệp vụ.
- [ ] SQL fresh-create tuân thủ policy.
- [ ] Patch legacy có guard, không blanket-update và chưa được tự chạy.
- [ ] Có migration matrix cho dữ liệu cũ.
- [ ] Full backend/frontend/test suite đã chạy thật và báo số lượng thật.
- [ ] Có checklist deploy/smoke test.
- [ ] Không tự commit/push/merge hoặc chạy migration database.

Nếu còn blocker, không được ghi “hoàn thành toàn bộ”. Hãy ghi rõ blocker, ảnh hưởng và hành động cần người dùng xác nhận.
