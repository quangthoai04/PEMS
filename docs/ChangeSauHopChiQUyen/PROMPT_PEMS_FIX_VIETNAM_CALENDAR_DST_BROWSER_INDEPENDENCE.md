# PROMPT — FIX TRIỆT ĐỂ DST VÀ LOẠI BỎ PHỤ THUỘC TIMEZONE TRÌNH DUYỆT KHỎI CALENDAR PEMS

## 1. Vai trò và mục tiêu

Bạn là Senior React TypeScript Engineer và QA Engineer của dự án PEMS.

Hãy tiếp tục trên **nhánh hiện tại**, dựa trên toàn bộ implementation chuẩn hóa giờ Việt Nam đã có. Không viết lại toàn bộ hệ thống thời gian.

Mục tiêu duy nhất của task này:

> Loại bỏ known DST bug của `toVietnamCalendarDate()`/re-based browser-local `Date`, bảo đảm cùng dữ liệu PEMS luôn có cùng ngày, giờ, calendar key và vị trí trên lịch ở mọi timezone trình duyệt, kể cả ngày New York chuyển DST.

Kết quả cuối phải chuyển trạng thái từ:

```text
BLOCKED — known DST display bug
```

thành:

```text
READY_FOR_CODE_REVIEW
```

Chỉ được kết luận như vậy sau khi test DST và full suite pass thật.

---

## 2. Bối cảnh và quy ước thời gian đã chốt

- MySQL `DATETIME` PEMS lưu Vietnam wall-clock.
- API timestamp nghiệp vụ trả ISO 8601 có `+07:00`.
- Frontend luôn hiển thị theo `Asia/Ho_Chi_Minh`, không theo timezone browser.
- Planned/agenda/calendar time là giờ Việt Nam.
- Date-only `YYYY-MM-DD` là ngày lịch, không phải UTC instant.
- Instant/duration/sort dùng instant thật từ chuỗi có offset/UTC.
- JWT/OAuth protocol không thuộc scope task này.
- Không cộng/trừ 7 giờ thủ công.
- Việt Nam không dùng DST, nhưng browser của khách quốc tế có thể ở timezone dùng DST.

Ví dụ bắt buộc phải đúng ở cả browser Việt Nam và New York:

```text
Input API: 2026-03-08T02:30:00+07:00
Vietnam calendar date: 2026-03-08
Vietnam display time: 02:30
```

Không được để browser New York tự chuẩn hóa `02:30` thành `03:30` vì `02:30` local New York không tồn tại trong ngày bắt đầu DST.

---

## 3. Git safety

Trước khi sửa, ghi nhận:

```bash
git status --short --branch
git branch --show-current
git diff --stat
git diff --check
```

Yêu cầu:

- Không chuyển branch.
- Không reset/checkout/clean/stash làm mất thay đổi.
- Không tự sửa upstream có tên lệch dấu.
- Không commit/push/merge/rebase.
- Không chạy migration hoặc sửa database.
- Không ghi đè các thay đổi timezone và visitor-edit đã có.
- Chỉ sửa frontend utility/calendar consumers/tests/docs liên quan trực tiếp đến DST.

---

## 4. Source bắt buộc đọc trước

Trước khi code, search và đọc source thật:

- Definition của `toVietnamCalendarDate()`.
- Definition của `parseApiDate()`, `parseDateKey()` và toàn bộ helper trong `vietnamTime.ts`.
- Toàn bộ 44 call site đã được báo cáo trong 14 file.
- Các calendar engine, dashboard calendar, staff/dept task grid.
- Mọi chỗ dùng `getFullYear/getMonth/getDate/getDay/getHours/getMinutes` trên kết quả helper.
- Mọi chỗ dùng `setDate/setMonth` để navigation.
- Mọi chỗ dùng `getTime`, sort, duration, `toISOString`, JSON/payload.
- `vietnam-time.spec.ts`, Playwright config và test cross-timezone hiện tại.

Search tối thiểu:

```text
toVietnamCalendarDate
parseDateKey
getFullYear
getMonth
getDate
getDay
getHours
getMinutes
setDate
setMonth
getTime
valueOf
toISOString
JSON.stringify
new Date(
Date.UTC
formatToParts
timeZone
```

Không tin danh sách 44 call site cũ nếu source đã thay đổi. Lập lại inventory từ source hiện tại.

---

## 5. Root cause phải hiểu đúng

Implementation re-based hiện tại có dạng tương đương:

```typescript
const vietnamParts = ...
return new Date(
  vietnamParts.year,
  vietnamParts.month - 1,
  vietnamParts.day,
  vietnamParts.hour,
  vietnamParts.minute,
)
```

Đây là browser-local `Date`, không phải Vietnam wall-clock type.

Ở browser `America/New_York`, ngày spring-forward có khoảng thời gian local không tồn tại. JavaScript có thể tự sửa:

```text
new Date(2026, 2, 8, 2, 30) → 03:30
```

Vì vậy fake/re-based local Date không thể đại diện chính xác mọi Vietnam wall-clock.

Không được giữ known bug với lý do “chỉ xảy ra khoảng một giờ mỗi năm”. PEMS phục vụ khách quốc tế và đã chốt browser-independent display.

---

## 6. Thiết kế đích

### 6.1. Tách ba khái niệm

Phải tách rõ:

1. **Instant**: thời điểm tuyệt đối; dùng `Date`/epoch từ API có offset hoặc `Z`.
2. **Vietnam wall-clock parts**: `{year, month, day, hour, minute, second}`; không phụ thuộc browser timezone.
3. **Date-only/calendar key**: chuỗi `YYYY-MM-DD`; không phải instant.

Không dùng một browser-local `Date` để đóng cả ba vai trò.

### 6.2. Utility khuyến nghị

Thiết kế tên theo convention source hiện tại, nhưng semantics phải tương đương:

```typescript
export interface VietnamDateTimeParts {
  year: number
  month: number       // 1..12
  day: number         // 1..31
  weekday: number     // quy ước phải document
  hour: number        // 0..23
  minute: number
  second: number
}

getVietnamDateTimeParts(input): VietnamDateTimeParts
toVietnamDateKey(input): string
parseDateKey(value: string): CalendarDateParts
```

Yêu cầu cho `getVietnamDateTimeParts`:

- Với ISO có `Z`/offset: parse thành instant thật, sau đó dùng `Intl.DateTimeFormat(..., { timeZone: 'Asia/Ho_Chi_Minh', hourCycle: 'h23' }).formatToParts()` hoặc cách tương đương.
- Với chuỗi PEMS legacy không offset: áp dụng compatibility contract hiện tại, hiểu rõ đó là Vietnam wall-clock; không để browser tự parse.
- Với date-only: parse component bằng chuỗi, không dùng `new Date('YYYY-MM-DD')`.
- Không trả browser-local `Date` nếu caller chỉ cần calendar parts.
- Tránh trường hợp hour `24` bằng `hourCycle: 'h23'` hoặc normalize có test.

### 6.3. Trường hợp calendar bắt buộc nhận `Date`

Trước tiên kiểm tra calendar hiện tại là custom hay thư viện ngoài.

- Nếu thư viện hỗ trợ timezone: cấu hình `Asia/Ho_Chi_Minh` đúng API của thư viện.
- Nếu custom calendar: chuyển calculation/render sang wall-clock parts/date key.
- Nếu bắt buộc phải dùng `Date` làm carrier: có thể tạo UTC carrier bằng `Date.UTC(...)`, nhưng **mọi consumer phải dùng UTC getters/setters** (`getUTC*`, `setUTC*`). Không được trộn UTC carrier với local getters.
- Không gửi UTC carrier đó về backend và không dùng nó như instant thật.
- Nếu thư viện bên ngoài luôn gọi local getters và không hỗ trợ timezone, UTC carrier không giải quyết triệt để; phải đổi integration strategy thay vì che lỗi.

Không chọn giải pháp chỉ vì sửa ít dòng. Chọn giải pháp có semantics rõ và test được.

---

## 7. Yêu cầu refactor caller

Lập bảng cho toàn bộ caller:

| File/caller | Mục đích | Kiểu dữ liệu đúng | Cách sửa | Test |
|---|---|---|---|---|

### 7.1. Render và calendar placement

- Dùng Vietnam wall-clock parts/date key.
- Giờ hiển thị lấy từ `hour/minute` Việt Nam, không dùng local browser getter.
- Weekday phải tính theo ngày Việt Nam.
- Month/year grouping phải dựa trên Vietnam parts.

### 7.2. Navigation

Previous/next day/month/year phải dùng calendar arithmetic không phụ thuộc DST.

Có thể dùng UTC carrier nội bộ cho phép toán ngày nếu luôn dùng `Date.UTC` + `getUTC*`/`setUTC*`, hoặc viết pure calendar arithmetic. Kết quả public vẫn là date key/parts.

Test cuối tháng/năm và tháng 2 năm nhuận.

### 7.3. Instant math và sorting

- Duration, expiry, chronological instant sort dùng `parseApiDate(input).getTime()` hoặc instant tương đương.
- Không dùng wall-clock parts/UTC carrier như instant.
- Nếu sort theo calendar display time thay vì instant, document rõ và so parts/key tương ứng.

### 7.4. Form và API payload

- `datetime-local` dùng chuỗi Vietnam wall-clock được tạo bởi utility hiện tại.
- Không gọi `toISOString()` trên calendar carrier.
- Không serialize `VietnamDateTimeParts` trực tiếp nếu API chưa có contract đó.
- Payload backend phải giữ contract hiện tại.

### 7.5. Xử lý helper cũ

Sau khi migration caller:

- Xóa `toVietnamCalendarDate()` nếu không còn cần.
- Hoặc đổi tên/type/documentation để không thể hiểu nhầm là instant và bảo đảm không còn browser-local Date/DST bug.
- Không giữ helper deprecated không có caller nếu lint cho phép dọn.
- Không để hai utility có semantics trùng nhưng khác cách parse.

---

## 8. Test bắt buộc

### 8.1. Utility tests

Test deterministic, không dựa vào timezone máy chạy test nếu framework không đổi timezone được.

Bao phủ:

```text
2026-03-08T01:30:00+07:00 → 2026-03-08 01:30
2026-03-08T02:30:00+07:00 → 2026-03-08 02:30
2026-03-08T03:30:00+07:00 → 2026-03-08 03:30
2026-11-01T01:30:00+07:00 → 2026-11-01 01:30
2026-11-01T02:30:00+07:00 → 2026-11-01 02:30
2026-12-31T23:30:00+07:00 → đúng ngày/giờ, không sai năm
2026-07-02 → date key không lùi ngày
2024-02-29 → navigation leap year đúng
```

Test thêm input instant tương đương:

```text
2026-03-07T19:30:00Z → 2026-03-08 02:30 Việt Nam
```

### 8.2. Browser cross-timezone tests

Chạy cùng test/UI dưới:

- `Asia/Ho_Chi_Minh`.
- `America/New_York`.

Nếu dễ cấu hình, thêm một timezone DST khác để tránh fix cứng riêng New York, nhưng không bắt buộc.

Assert ở cả hai browser:

- Cùng date key.
- Cùng giờ và phút hiển thị.
- Cùng weekday.
- Cùng month/year group.
- Event nằm cùng cell/time slot.
- Navigation previous/next trả cùng kết quả.
- Không có event `02:30` bị đổi thành `03:30` ngày spring-forward.
- Fall-back không tạo duplicate/mất event.

### 8.3. Serialization guard test

Chứng minh:

- Calendar parts/carrier không đi vào `toISOString()` để tạo payload.
- API payload giữ chuỗi Vietnam wall-clock đúng contract.
- Instant duration/sort vẫn dùng epoch thật.

### 8.4. Không test giả

- Test phải gọi utility/component thật.
- Ít nhất một test phải đi qua UI/calendar consumer thật, không chỉ test helper.
- Không mock chính utility đang kiểm tra.
- Không chỉ assert text tĩnh do test tự dựng.

---

## 9. Regression sweep sau refactor

Search lại toàn frontend:

```text
toVietnamCalendarDate
new Date(year
new Date(parts
getFullYear/getMonth/getDate/getHours/getMinutes
getUTCFullYear/getUTCMonth/getUTCDate/getUTCHours/getUTCMinutes
toISOString
+ 'Z'
+ "Z"
AddHours
```

Với mọi kết quả còn lại, giải thích semantics. Không báo sạch chỉ dựa trên grep nếu alias/wrapper khác vẫn tạo browser-local Date từ Vietnam parts.

Kiểm tra các màn tối thiểu:

- Shared dashboard calendar.
- Staff dashboard calendar.
- Staff calendar tab.
- Department staff task grids.
- Visit request management/detail.
- Agenda/minutes datetime-local hydration.
- Dashboard “hôm nay”.

---

## 10. Full verification

Sau khi sửa, chạy:

```text
npm run build
npm run lint/typecheck
npx playwright test [vietnam-time spec]
npx playwright test --project/timezone Asia/Ho_Chi_Minh nếu config hỗ trợ
npx playwright test --project/timezone America/New_York nếu config hỗ trợ
npx playwright test full suite
dotnet build full solution
backend Unit/Integration/Architecture full suite để bảo đảm không regression
```

Điều chỉnh câu lệnh đúng cấu trúc repository; báo lại command thật và số pass/fail/skip.

Không cần chạy lặp lại 15 lượt flaky cũ nếu source liên quan không đổi, nhưng full suite cuối phải pass. Nếu calendar refactor tác động spec flaky cũ, chạy lại spec đó.

---

## 11. Phạm vi được sửa

- `vietnamTime.ts` và test của utility.
- 14 file/44 caller hiện tại hoặc danh sách mới tìm được.
- Calendar/dashboard/task-grid components trực tiếp liên quan.
- Playwright timezone spec/config nếu cần.
- Tài liệu contract thời gian frontend trực tiếp liên quan.

---

## 12. Phạm vi không được sửa

- Không sửa backend/SQL/JWT/OAuth nếu không phát hiện regression trực tiếp có bằng chứng.
- Không thay business rule, role, authorization hoặc workflow.
- Không thay UI design ngoài hành vi ngày giờ.
- Không thêm thư viện mới nếu `Intl`/TypeScript hiện tại đủ dùng.
- Không dùng `process.env.TZ` như cách duy nhất để che bug runtime browser.
- Không thêm offset thủ công hoặc DST table tự duy trì.
- Không chấp nhận known one-hour DST drift.
- Không skip/retry test để đạt xanh.
- Không commit/push/merge/rebase/deploy/migrate.

---

## 13. Báo cáo cuối bắt buộc

### A. Git context

- Branch, upstream, working tree trước/sau.

### B. Root cause

- Chứng minh browser-local re-based Date sai ở DST gap.

### C. Thiết kế đã chọn

- Parts/date key, UTC carrier hay library timezone.
- Vì sao phù hợp với caller thật.
- Cách tách instant với wall-clock.

### D. Caller migration

- Tổng số definition/caller.
- Danh sách file sửa.
- Phân loại render/navigation/instant/payload.
- Xác nhận không còn fake local Date dùng sai.

### E. DST tests

- Kết quả từng case spring-forward/fall-back.
- Kết quả browser Việt Nam/New York.
- Kết quả UI consumer thật.

### F. Full verification

- Lệnh và số discovered/pass/fail/skip thật.

### G. Remaining risks

- Chỉ ghi rủi ro thực sự còn lại.
- Không ghi known one-hour DST drift là chấp nhận được.

### H. Final status

Chỉ chọn:

```text
READY_FOR_CODE_REVIEW
BLOCKED
```

Không commit/push/deploy/migrate trong task này.

---

## 14. Definition of Done

- [ ] Không còn browser-local Date đại diện Vietnam wall-clock tại DST gap.
- [ ] Instant, Vietnam wall-clock parts và date-only đã được tách nghĩa rõ.
- [ ] Tất cả caller hiện tại đã được audit và migrate đúng semantics.
- [ ] `02:30` ngày New York spring-forward vẫn hiển thị `02:30` giờ Việt Nam.
- [ ] Fall-back không làm trùng/mất event.
- [ ] Date key, weekday, month/year group và time slot giống nhau giữa browser VN/New York.
- [ ] Instant math/sort/duration vẫn dùng instant thật.
- [ ] Calendar representation không bị serialize gửi API.
- [ ] Utility test và UI/browser cross-timezone test pass.
- [ ] Frontend build/lint/typecheck và full Playwright pass.
- [ ] Backend full verification không regression.
- [ ] Git diff sạch EOL/trailing whitespace, không có generated artifact/secret mới.
- [ ] Không commit/push/merge/rebase/deploy/migrate.
- [ ] Báo cáo kết thúc bằng `READY_FOR_CODE_REVIEW` hoặc `BLOCKED`.
