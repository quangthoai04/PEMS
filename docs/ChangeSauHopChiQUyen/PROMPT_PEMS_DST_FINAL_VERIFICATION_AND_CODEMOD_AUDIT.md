# PROMPT — BỔ SUNG DST TRANSITION TEST, AUDIT CODEMOD VÀ CHỐT NGHIỆM THU TIMEZONE FRONTEND

## 1. Vai trò và mục tiêu

Bạn là Senior React TypeScript Engineer và QA Reviewer của dự án PEMS.

Hãy tiếp tục trên **nhánh hiện tại**. Implementation hiện đã chuyển `toVietnamCalendarDate()` từ browser-local re-based `Date` sang UTC carrier và đổi các caller liên quan sang UTC getters/setters.

Không refactor lại nếu thiết kế đó đang đúng. Task này chỉ nhằm:

1. Bổ sung bằng chứng test đúng các ngày New York chuyển DST.
2. Audit toàn bộ thay đổi getter/setter do codemod để loại trừ thay nhầm trên instant thật.
3. Chứng minh UTC carrier không bị dùng cho duration, sort instant hoặc API serialization.
4. Chạy full frontend và backend regression suite.
5. Kết luận chính xác `READY_FOR_CODE_REVIEW` hoặc `BLOCKED`.

---

## 2. Quy ước đã chốt

- MySQL `DATETIME` PEMS lưu Vietnam wall-clock.
- API timestamp nghiệp vụ có `+07:00`.
- Frontend luôn hiển thị `Asia/Ho_Chi_Minh`.
- Date-only `YYYY-MM-DD` là calendar date, không phải UTC instant.
- Instant/duration/sort chronology dùng epoch thật từ `parseApiDate()` hoặc tương đương.
- UTC carrier chỉ là vật chứa calendar parts Việt Nam và chỉ được đọc/sửa bằng UTC getters/setters.
- UTC carrier không phải instant thật và không được gửi API.
- Không cộng/trừ 7 giờ thủ công.
- Không thay đổi backend/database/JWT/OAuth trong task này nếu không phát hiện regression rõ ràng.

---

## 3. Git safety

Trước khi sửa, chạy:

```bash
git status --short --branch
git branch --show-current
git diff --stat
git diff --check
```

Yêu cầu:

- Không chuyển branch.
- Không reset/checkout/clean/stash làm mất thay đổi.
- Không commit/push/merge/rebase.
- Không deploy hoặc chạy SQL patch.
- Không sửa upstream branch.
- Không chạy lại codemod hàng loạt trước khi audit source hiện tại.

---

## 4. Baseline cần xác minh

Báo cáo hiện tại claim:

- `VietnamDateTimeParts`, `getVietnamDateTimeParts()` và `toVietnamDateKey()` đã được thêm.
- `toVietnamCalendarDate()` trả UTC carrier.
- Các caller đã đổi từ `get*`/`set*` local sang `getUTC*`/`setUTC*`.
- Playwright mới chỉ chứng minh case tháng 7 trên browser Việt Nam/New York.
- Frontend build/lint pass.
- Chưa có bằng chứng rõ ràng cho DST transition ngày 08/03/2026 và 01/11/2026.
- Chưa báo full Playwright suite và full backend regression sau thay đổi cuối.

Không tin claim chỉ bằng báo cáo. Đối chiếu bằng source, diff và test output thật.

---

## 5. Task 1 — audit thiết kế UTC carrier

### 5.1. Đọc implementation thật

Đọc:

- `vietnamTime.ts`.
- `toVietnamCalendarDate()`.
- `getVietnamDateTimeParts()`.
- `toVietnamDateKey()`.
- `parseApiDate()`.
- `parseDateKey()`.
- Mọi test hiện có trong `vietnam-time.spec.ts` hoặc file tương đương.

Xác minh UTC carrier được tạo theo semantics tương đương:

```typescript
new Date(Date.UTC(year, month - 1, day, hour, minute, second))
```

và caller chỉ đọc calendar parts bằng:

```typescript
getUTCFullYear()
getUTCMonth()
getUTCDate()
getUTCDay()
getUTCHours()
getUTCMinutes()
getUTCSeconds()
```

Navigation chỉ dùng `setUTCDate`, `setUTCMonth`, `setUTCFullYear` trên carrier.

### 5.2. Kiểm tra parsing

- ISO có `Z`/offset phải được parse thành instant, sau đó lấy parts Việt Nam bằng `Intl.DateTimeFormat` với `timeZone: 'Asia/Ho_Chi_Minh'`.
- Dùng `hourCycle: 'h23'` hoặc xử lý tương đương để tránh hour `24`.
- Chuỗi legacy không offset phải theo compatibility contract hiện tại, không để browser tự đoán timezone.
- Date-only phải parse component, không dùng `new Date('YYYY-MM-DD')`.
- Invalid input phải có behavior rõ và nhất quán với utility cũ.

Nếu implementation đã đúng, không thay đổi chỉ vì sở thích code style.

---

## 6. Task 2 — audit codemod getter/setter

### 6.1. Lập inventory từ source hiện tại

Search toàn frontend:

```text
toVietnamCalendarDate
getUTCFullYear
getUTCMonth
getUTCDate
getUTCDay
getUTCHours
getUTCMinutes
getUTCSeconds
setUTCDate
setUTCMonth
setUTCFullYear
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
```

Không chỉ grep tên method; đọc data flow của object nhận method.

### 6.2. Phân loại từng UTC getter/setter

Tạo bảng:

| File:dòng | Object được tạo từ đâu | Semantics | Method | Đúng/sai | Hành động |
|---|---|---|---|---|---|

Phân loại:

1. UTC carrier từ `toVietnamCalendarDate()` → dùng `getUTC*`/`setUTC*`: đúng.
2. Instant thật từ `parseApiDate()`/ISO có offset → dùng `getTime()` cho duration/sort: đúng.
3. Date-only/navigation carrier → dùng UTC calendar arithmetic: đúng nếu không serialize.
4. Browser-local behavior có chủ đích → không được codemod nhầm sang UTC.
5. Object không rõ nguồn → phải trace trước khi kết luận.

### 6.3. Điều kiện bắt buộc

Chứng minh không có UTC carrier nào đi vào:

```text
getTime()/valueOf() để so với instant thật
toISOString()
JSON.stringify()/request payload
API service body/query
database persistence
token/expiry calculation
```

Ngoại lệ chỉ được chấp nhận nếu cả hai operand đều là UTC calendar carrier và phép toán thuần calendar đã được document/test; không được gọi đó là instant math.

Chứng minh codemod không đổi nhầm local getter của object không đến từ UTC carrier. Nếu có thay nhầm, sửa thủ công và thêm regression test phù hợp.

---

## 7. Task 3 — bổ sung test DST transition thật

### 7.1. Utility test bắt buộc

Test chính xác các case:

```text
2026-03-08T01:30:00+07:00 → 2026-03-08 01:30
2026-03-08T02:30:00+07:00 → 2026-03-08 02:30
2026-03-08T03:30:00+07:00 → 2026-03-08 03:30

2026-11-01T01:30:00+07:00 → 2026-11-01 01:30
2026-11-01T02:30:00+07:00 → 2026-11-01 02:30

2026-03-07T19:30:00Z → 2026-03-08 02:30 giờ Việt Nam
2026-12-31T23:30:00+07:00 → đúng ngày/giờ/năm
2024-02-29 → date-only/navigation đúng năm nhuận
```

Với mỗi case, assert tối thiểu:

- `year/month/day`.
- `hour/minute`.
- `weekday` nếu utility trả về.
- `toVietnamDateKey()`.
- UTC carrier getters tương ứng.

Không dùng expected value được tạo bằng chính utility đang test.

### 7.2. Browser cross-timezone test bắt buộc

Chạy cùng input/UI trên:

- `Asia/Ho_Chi_Minh`.
- `America/New_York`.

Đặc biệt bắt buộc case:

```text
2026-03-08T02:30:00+07:00
```

Assert cả hai browser đều hiển thị/xếp lịch:

```text
Ngày: 08/03/2026
Giờ: 02:30
Calendar key: 2026-03-08
```

Không được chỉ test `02/07 01:30`, vì đó không phải DST transition gap.

Test fall-back ngày `01/11/2026` để bảo đảm không duplicate/mất event.

### 7.3. Test qua UI consumer thật

Ít nhất một case phải đi qua calendar/dashboard component thật:

- Render mock API payload.
- Xác nhận đúng cột/ngày/time slot.
- Không mock `vietnamTime` utility.
- Không chỉ gọi helper rồi assert helper.

### 7.4. Serialization guard

Thêm test hoặc bằng chứng source đủ mạnh để bảo đảm:

- Form/API payload vẫn là chuỗi Vietnam wall-clock đúng contract.
- Không gọi `toISOString()` trên UTC carrier.
- Duration/sort instant vẫn dùng epoch của instant thật.

---

## 8. Task 4 — chạy full verification

### 8.1. Frontend

Chạy lệnh đúng theo repository:

```bash
npm run build
npm run lint
npx playwright test [vietnam-time-spec]
npx playwright test
```

Nếu dự án có typecheck riêng, chạy thêm.

Báo rõ:

- Tổng test trong timezone spec.
- Tổng full Playwright discovered/pass/fail/skip.
- Thời gian chạy.
- Browser projects/timezone thực tế được dùng.

Không được lấy kết quả subset khoảng 16 giây để gọi là full suite.

### 8.2. Backend regression

Không cần sửa backend nếu không có lỗi, nhưng phải chạy full verification cuối:

```text
dotnet build PEMS.slnx
PEMS.UnitTests
PEMS.IntegrationTests với MySQL pems_test thật
PEMS.ArchitectureTests
```

Báo discovered/pass/fail/skip thật. Nếu môi trường không chạy được MySQL, ghi `BLOCKED`; không dùng kết quả cũ để giả làm lần chạy mới.

### 8.3. Diff hygiene

Sau sửa, chạy:

```bash
git diff --check
git status --short
git diff --stat
```

Không đưa generated artifact, trace, screenshot, report HTML, test-results hoặc secret vào diff.

---

## 9. Phạm vi được sửa

- `vietnamTime.ts` nếu audit phát hiện bug thật.
- Các caller bị codemod sai.
- `vietnam-time.spec.ts` và Playwright timezone configuration/fixture.
- Calendar/dashboard consumer dùng cho regression test.
- Tài liệu báo cáo trực tiếp liên quan.

---

## 10. Phạm vi không được sửa

- Không refactor lại UTC carrier nếu implementation đã đúng.
- Không sửa backend/database/JWT/OAuth ngoài regression bug có bằng chứng.
- Không chạy database patch.
- Không thay business rule, authorization hoặc UI design.
- Không thêm retry/skip/tăng timeout để che test fail.
- Không chỉ sửa prompt thành `READY_FOR_CODE_REVIEW`.
- Không chạy codemod hàng loạt mới.
- Không commit/push/merge/rebase/deploy.

---

## 11. Báo cáo cuối bắt buộc

### A. Git context

- Branch/upstream, working tree trước/sau.

### B. UTC carrier verification

- Code tạo carrier.
- Quy ước getters/setters.
- Parsing offset/legacy/date-only.

### C. Codemod audit

- Tổng số caller/getter/setter được audit.
- Bảng source object và semantics.
- Danh sách chỗ codemod đúng.
- Chỗ codemod sai và cách sửa, nếu có.
- Xác nhận carrier không được serialize hoặc dùng như instant.

### D. DST transition evidence

- Kết quả từng case tháng 3 và tháng 11.
- Kết quả UI thật trên Việt Nam/New York.
- Xác nhận `02:30` không biến thành `03:30`.

### E. Full test evidence

- Command thật.
- Frontend build/lint/typecheck.
- Timezone spec count.
- Full Playwright count.
- Backend build/Unit/Integration/Architecture count.

### F. Remaining risks

- Chỉ ghi rủi ro còn tồn tại có bằng chứng.

### G. Final status

Chỉ chọn một:

```text
READY_FOR_CODE_REVIEW
```

Không được kết luận `READY_FOR_CODE_REVIEW` nếu thiếu DST transition UI test, full Playwright hoặc backend regression.

---

## 12. Definition of Done

- [ ] UTC carrier implementation đã được đọc và xác minh.
- [ ] Toàn bộ getter/setter do codemod đã được trace nguồn object.
- [ ] Không có local/UTC getter bị thay nhầm semantics.
- [ ] UTC carrier không dùng cho instant duration/sort hoặc API serialization.
- [ ] Test `08/03/2026 02:30` pass trên browser New York và Việt Nam.
- [ ] Fall-back `01/11/2026` không duplicate/mất event.
- [ ] Ít nhất một UI calendar consumer thật được test.
- [ ] Timezone spec pass với số liệu thật.
- [ ] Full Playwright pass với số liệu thật.
- [ ] Frontend build/lint/typecheck pass.
- [ ] Backend build/Unit/Integration/Architecture pass lại sau thay đổi cuối.
- [ ] `git diff --check` sạch, không có artifact/secret mới.
- [ ] Không commit/push/merge/rebase/deploy/migrate.
- [ ] Báo cáo cuối là `READY_FOR_CODE_REVIEW` hoặc `BLOCKED` đúng bằng chứng.
