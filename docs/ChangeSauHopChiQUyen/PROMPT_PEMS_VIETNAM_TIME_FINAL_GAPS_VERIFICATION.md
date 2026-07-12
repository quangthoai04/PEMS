# PROMPT — XỬ LÝ CÁC ĐIỂM CÒN THIẾU TRƯỚC KHI NGHIỆM THU CODE CHUẨN HÓA GIỜ VIỆT NAM

## 1. Vai trò và mục tiêu

Bạn là Senior .NET 8, React TypeScript và QA Engineer của dự án PEMS.

Hãy tiếp tục trực tiếp trên **nhánh hiện tại**, dựa trên implementation chuẩn hóa giờ Việt Nam đã có. Không triển khai lại toàn bộ. Mục tiêu của task này là xử lý và chứng minh bốn điểm còn thiếu trước khi nghiệm thu phần code:

1. Làm rõ vì sao thêm hai test `JwtLifetimeTests` nhưng tổng Unit Test vẫn là `211`.
2. Tái hiện và xử lý đúng hai nhóm test flaky đã xuất hiện.
3. Audit `toVietnamCalendarDate()` để chắc chắn fake/re-based `Date` chỉ dùng cho calendar parts, không bị dùng như một instant thật.
4. Rà soát working tree lớn, loại trừ file ngoài scope/generated/line-ending noise và đưa ra báo cáo nghiệm thu chính xác.

Không chạy migration database, không deploy, không commit/push/merge/rebase nếu người dùng chưa yêu cầu.

---

## 2. Bối cảnh đã chốt — không thay đổi lại kiến trúc

- MySQL `DATETIME` do PEMS quản lý lưu Vietnam wall-clock, `DateTimeKind.Unspecified`.
- Backend persistence và comparison dùng `VietnamTime.Now()`/`IClock.VietnamNow`.
- API timestamp nghiệp vụ trả ISO 8601 có `+07:00`.
- Frontend hiển thị cố định `Asia/Ho_Chi_Minh`.
- Planned/agenda/logistics time là Vietnam wall-clock nhập verbatim.
- JWT `iat`/`nbf`/`exp` dùng Unix NumericDate đúng instant.
- OAuth `expires_in` là thời lượng giây.
- Snapshot expiry lưu trong MySQL PEMS dùng Vietnam wall-clock.
- Không cộng/trừ 7 giờ thủ công trong runtime.
- Patch dữ liệu legacy chưa được phép chạy.

Không thay đổi các quyết định trên trong task này trừ khi phát hiện bug có bằng chứng test/source rõ ràng.

---

## 3. Git safety và baseline bắt buộc

Trước khi sửa, chạy và ghi nhận:

```bash
git status --short --branch
git branch --show-current
git rev-parse --abbrev-ref --symbolic-full-name @{upstream}
git log --oneline --decorate -n 15
git diff --stat
git diff --check
```

Nếu có base `Dev`, chỉ dùng lệnh read-only để so sánh. Không tự fetch nếu việc đó làm thay đổi context mà chưa cần thiết; nếu đã có ref local thì ghi rõ SHA đang dùng.

Yêu cầu:

- Không chuyển branch.
- Không reset/checkout/clean/stash làm mất thay đổi.
- Không ghi đè thay đổi không thuộc task.
- Không tự stage, commit hoặc push.
- Xác minh rõ local branch `Canh-Iter1` và upstream `origin/Cảnh-Iter1` có thật sự là tracking pair mong muốn; chỉ báo cáo, không tự sửa upstream.

---

## 4. Tài liệu/source cần đọc

Đọc theo mức cần thiết:

- `PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md`.
- `CLEAN_ARCHITECTURE.md`.
- `PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md`.
- `PROJECT_STRUCTURE_FULL.md`.
- Source và `.csproj` của `PEMS.UnitTests`, `PEMS.IntegrationTests`, `PEMS.ArchitectureTests`.
- Playwright config, fixtures và các spec liên quan.
- `VietnamTime.cs`, `JwtTokenService`, `JwtLifetimeTests.cs`.
- `vietnamTime.ts`, `toVietnamCalendarDate()`, `parseDateKey()` và toàn bộ caller.
- Hai test flaky được nêu trong báo cáo trước.

Trước khi sửa phải search source thật. Không suy đoán đường dẫn hoặc nguyên nhân.

---

## 5. Task 1 — xác minh Unit Test count và JWT lifetime

### 5.1. Vấn đề cần giải quyết

Báo cáo trước nói Unit Test đã có `211/211`. Đợt hoàn thiện bổ sung `JwtLifetimeTests.cs` gồm hai test, nhưng tổng vẫn báo `211`. Phải xác minh đây là:

- Báo cáo baseline cũ sai;
- Có test cũ bị xóa/di chuyển;
- Hai test mới chưa được discover;
- File không được compile vào đúng project;
- Test bị conditional compilation/skip;
- Hoặc một nguyên nhân khác có bằng chứng.

### 5.2. Các bước bắt buộc

Chạy đúng project thật, điều chỉnh path theo repository:

```bash
dotnet test [PEMS.UnitTests.csproj] --list-tests
dotnet test [PEMS.UnitTests.csproj] --filter FullyQualifiedName~JwtLifetimeTests --logger "console;verbosity=detailed"
dotnet test [PEMS.UnitTests.csproj] --no-restore
```

Kiểm tra:

- `JwtLifetimeTests.cs` có nằm trong cây source của project.
- SDK-style `.csproj` có exclude file/folder không.
- Test có `[Fact]`/`[Theory]` hợp lệ.
- Test class/method không bị `private`, generic hoặc abstract sai.
- Không có duplicate class/name khiến file khác được chạy thay.
- Test runner package và target framework đúng.
- So sánh danh sách test trước/sau bằng tên, không chỉ nhìn tổng.

### 5.3. Chất lượng hai test JWT

Hai test phải chứng minh bằng implementation thật:

1. JWT tạo lúc một instant xác định có `exp - iat = 3600` giây khi lifetime là 60 phút.
2. Decode NumericDate về instant phải tương ứng đúng giờ Việt Nam trong database/API, nhưng không cộng thêm `7 * 3600` vào Unix.

Không chấp nhận test chỉ gọi helper tự viết lại cùng công thức mà không đi qua `JwtTokenService` hoặc implementation thật.

Nếu production service khó test do phụ thuộc configuration, tạo fixture/config test đúng kiến trúc hiện tại; không copy logic JWT vào test.

### 5.4. Acceptance criteria Task 1

- Hai test JWT xuất hiện trong `--list-tests`.
- Chạy filter thật sự execute đúng hai test và pass.
- Tổng Unit Test được báo lại chính xác.
- Nếu tổng vẫn 211, phải có bảng tên test được thêm và tên test bị xóa/thay thế giải thích chênh lệch.
- Không được chỉ sửa con số báo cáo mà không kiểm tra discovery.

---

## 6. Task 2 — điều tra hai nhóm flaky test

### 6.1. Integration test flaky

Test đã từng fail trong full suite nhưng pass khi chạy riêng:

```text
Resubmit_PayloadWithForgedAgenda...
```

Hãy tìm đúng fully-qualified test name và chạy:

```bash
dotnet test [IntegrationTests.csproj] --filter FullyQualifiedName~[tên-test] --no-restore
dotnet test [IntegrationTests.csproj] --filter FullyQualifiedName~[nhóm-resubmit] --no-restore
dotnet test [IntegrationTests.csproj] --no-restore
```

Lặp lại test/nhóm/full suite đủ để phát hiện flake, mục tiêu tối thiểu 10 lượt cho test riêng hoặc nhóm nhỏ. Không che lỗi bằng retry trong production code.

Điều tra tối thiểu:

- Dữ liệu dùng chung giữa test và thứ tự chạy.
- Database reset/transaction/cleanup.
- ID, email, delegation code, agenda row hoặc idempotency key cố định.
- Static/shared clock hoặc timestamp boundary.
- Test parallelization.
- Server/DbContext/service cũ chưa dispose.
- Record từ test trước ảnh hưởng validator/duplicate detection.
- Assertion phụ thuộc thời gian hiện tại.

Nếu nguyên nhân nằm ở test isolation/fixture, sửa test infrastructure hoặc seed tối thiểu đúng scope. Nếu là bug production bị test phát hiện, sửa production và bổ sung regression test.

Không được gắn nhãn “ngoài scope timezone” chỉ vì test pass ở lần chạy thứ hai.

### 6.2. Playwright flaky

Nhóm đã từng fail do flow khoảng 26 giây:

```text
TC-03/TC-05/TC-06/TC-07/TC-08
```

Tìm đúng spec/test name và chạy:

```bash
npx playwright test [spec] --repeat-each=10
npx playwright test [spec] --workers=1 --repeat-each=5
npx playwright test
```

Điều tra:

- Timeout dựa trên thời gian cứng.
- `waitForTimeout` thay vì đợi trạng thái/network/UI cụ thể.
- Dùng chung account/data/database.
- OTP/rate limit/session expiration.
- API/server cũ chưa restart.
- Race condition khi flow chuyển trang.
- Parallel workers dùng chung record.
- Calendar/current-time assertion thay đổi trong lúc test.

Ưu tiên chờ theo observable state (`expect`, response, element state) thay vì tăng timeout bừa. Chỉ tăng timeout khi flow hợp lệ thực sự cần nhiều thời gian và đã chứng minh không có race.

### 6.3. Quy tắc kết luận flaky

Chỉ được chuyển sang task riêng nếu:

- Đã chạy lặp và lưu số lần pass/fail.
- Đã chứng minh không liên quan timezone/static clock/shared data do thay đổi lần này.
- Full suite cuối pass.
- Ghi issue/risk cụ thể; không chỉ ghi “flake theo tải”.

Nếu test vẫn fail không ổn định, task này chưa được báo hoàn thành toàn bộ.

---

## 7. Task 3 — audit `toVietnamCalendarDate()` và calendar semantics

### 7.1. Mục tiêu

`toVietnamCalendarDate()` được báo cáo là tạo/re-base một đối tượng `Date` để local getters trả về phần ngày giờ Việt Nam. Cách này chỉ an toàn nếu giá trị đó được coi là **calendar representation**, không phải instant thật.

Phải tìm tất cả definition và caller:

```text
toVietnamCalendarDate
parseDateKey
getTime
valueOf
toISOString
JSON.stringify
new Date(result)
setHours/setDate
API request payload
duration/difference/sort
```

### 7.2. Phân loại caller

Với mỗi caller, phân loại:

1. Chỉ đọc `getFullYear/getMonth/getDate/getDay/getHours/getMinutes` để render/xếp calendar: có thể hợp lệ.
2. Dùng để tạo `YYYY-MM-DD` calendar key: hợp lệ nếu đã test cross-timezone.
3. Dùng `getTime`, trừ hai Date, sort instant, duration: không được dùng fake/re-based Date.
4. Dùng `toISOString`, serialize, gửi API, persistence: không được dùng fake/re-based Date.
5. Dùng mutation getter/setter cho navigation: phải chứng minh không drift DST/browser timezone và không lùi ngày.

Các tác vụ instant/duration phải dùng `parseApiDate()` hoặc `DateTimeOffset`/instant thật. Các tác vụ date-only phải dùng `parseDateKey()`/calendar parts, không parse `YYYY-MM-DD` bằng UTC midnight.

Nếu tên `toVietnamCalendarDate()` dễ khiến caller hiểu nhầm đó là instant thật, cân nhắc đổi sang tên thể hiện rõ calendar view, nhưng chỉ đổi nếu phạm vi an toàn và cập nhật toàn bộ caller/test.

### 7.3. Test bắt buộc

Thêm/chỉnh test utility hoặc Playwright để chứng minh cùng input API có `+07:00`:

- Browser `Asia/Ho_Chi_Minh` và `America/New_York` tạo cùng calendar key.
- `2026-07-02T01:30:00+07:00` vẫn xếp ngày `2026-07-02` ở cả hai browser.
- Cuối năm/tháng: `2026-12-31T23:30:00+07:00` giữ đúng ngày.
- Date-only `2026-07-02` không bị lùi ngày.
- Navigation previous/next day/month đúng.
- Sorting/duration dùng instant thật và cho kết quả đúng.
- Không có fake Date nào được serialize gửi backend.

### 7.4. Acceptance criteria Task 3

- Có danh sách tất cả caller và kết luận từng caller.
- Không còn fake/re-based Date được dùng cho instant math hoặc API payload.
- Calendar/date-only và instant utilities được tách nghĩa rõ.
- Test cross-timezone pass.

---

## 8. Task 4 — working tree hygiene và scope audit

Working tree được báo cáo có khoảng `230 modified + 11 new`, nên phải kiểm tra kỹ trước khi nghiệm thu.

### 8.1. Kiểm tra bắt buộc

Chạy:

```bash
git status --short
git diff --stat
git diff --numstat
git diff --check
git ls-files --others --exclude-standard
```

Tìm và loại trừ khỏi thay đổi nếu vô tình xuất hiện:

- `bin/`, `obj/`, `node_modules/`, `dist/`, coverage, test-results, Playwright artifacts.
- `.env`, appsettings chứa secret, JWT key, OAuth token, refresh token.
- Log/database dump/file tạm.
- IDE user settings.
- File bị đổi toàn bộ chỉ do line ending/formatting ngoài scope.
- Generated file không nên commit.

Không tự xóa file lạ thuộc người dùng. Nếu phát hiện, báo rõ và chỉ sửa/revert khi có bằng chứng đó là thay đổi do task tạo ra và an toàn.

### 8.2. Scope classification

Phân nhóm toàn bộ file thay đổi:

- Foundation/backend persistence.
- JWT/OAuth/security boundary.
- API converter/interceptor.
- Frontend time utility/consumer.
- Database fresh-create/patch.
- Tests.
- Documentation/prompt.
- Unrelated/pre-existing visitor-edit changes.
- Generated/noise/sensitive.

Không được tính năm commit visitor-edit-immutability là thay đổi do timezone nếu chúng đã có trước task.

### 8.3. Không commit nhưng chuẩn bị commit plan

Chỉ đề xuất, không thực hiện:

1. Foundation + backend writers.
2. API/MySQL boundary.
3. Frontend timezone/calendar sweep.
4. Database SQL/legacy patch.
5. Unit/Integration/Playwright tests.
6. Documentation nếu thật sự cần đưa vào repo.

Ghi rõ file prompt nào chỉ là tài liệu cho Agent và có nên để ngoài repository hay không.

---

## 9. Full verification sau khi sửa

Sau khi hoàn thành bốn task, chạy full suite thật:

```text
dotnet build full solution
PEMS.UnitTests
PEMS.IntegrationTests với MySQL test thật
PEMS.ArchitectureTests
frontend build
frontend lint/typecheck
Playwright full suite
```

Yêu cầu báo:

- Lệnh chính xác.
- Project/path chính xác.
- Tổng discovered/pass/fail/skip.
- Thời gian/lượt lặp của flaky tests.
- Test JWT filter execute bao nhiêu test.
- Không dùng dấu ✅ thay cho bằng chứng số liệu.

Nếu `PEMS.ApplicationTests` chỉ có source mồ côi và không có `.csproj`, xác minh bằng filesystem/project references và ghi thành technical debt; không tự tạo project mới trong scope này.

---

## 10. Phạm vi được sửa

- Test JWT và test project configuration nếu discovery sai.
- Test isolation/fixture/cleanup cho hai test flaky.
- Production bug nếu flaky test chứng minh lỗi thật.
- `vietnamTime.ts`, calendar helper và các caller liên quan.
- Unit/Integration/Playwright regression tests.
- Tài liệu báo cáo timezone trực tiếp liên quan.
- File noise do chính đợt timezone tạo ra, nếu có bằng chứng an toàn.

---

## 11. Phạm vi không được sửa

- Không thay đổi kiến trúc timezone đã chốt.
- Không thay đổi business workflow, role hoặc authorization.
- Không chạy patch database.
- Không deploy.
- Không tự sửa dữ liệu legacy.
- Không tăng timeout bừa để che flaky test.
- Không thêm retry để biến test đỏ thành xanh.
- Không xóa/disable/skip test đang fail.
- Không viết test giả chỉ test lại helper tự dựng.
- Không commit/push/merge/rebase/switch branch.

---

## 12. Báo cáo cuối bắt buộc

### A. Git context

- Branch/upstream/base SHA.
- Working tree trước/sau.

### B. JWT test discovery

- Vì sao tổng cũ và mới đều là 211.
- Danh sách hai test JWT.
- Kết quả `--list-tests`, filtered run và full Unit suite.

### C. Flaky test investigation

Với từng nhóm:

- Cách tái hiện.
- Số lượt pass/fail.
- Root cause.
- File sửa.
- Regression evidence.
- Có liên quan timezone hay không và bằng chứng.

### D. Calendar helper audit

- Danh sách caller.
- Caller dùng calendar parts.
- Caller từng dùng sai instant/serialization và cách sửa.
- Cross-timezone test results.

### E. Working tree scope

- Số file theo từng nhóm.
- Generated/noise/sensitive findings.
- File ngoài scope/pre-existing.
- Commit plan đề xuất nhưng chưa thực hiện.

### F. Full test results

- Command và số discovered/pass/fail/skip thật.

### G. Final acceptance status

Chỉ chọn một:

```text
READY_FOR_CODE_REVIEW
BLOCKED
```

Nếu `BLOCKED`, ghi blocker cụ thể. Không dùng “hoàn thành toàn bộ” nếu còn test không ổn định hoặc test JWT chưa được discover.

### H. Việc vẫn cần người vận hành

- Backup/migration/deploy/revoke session/smoke test vẫn chưa thực hiện.
- Nhắc rõ Agent không chạy patch hoặc deploy.

---

## 13. Definition of Done

- [ ] Hai test JWT được discover và chạy qua implementation thật.
- [ ] Tổng Unit Test được giải thích và báo chính xác.
- [ ] Hai nhóm flaky đã chạy lặp, có root cause hoặc bằng chứng đủ để tách task.
- [ ] Không che flaky bằng retry/skip/tăng timeout thiếu căn cứ.
- [ ] Tất cả caller của `toVietnamCalendarDate()` đã được audit.
- [ ] Fake/re-based Date không dùng cho instant math hoặc API serialization.
- [ ] Cross-timezone calendar tests pass.
- [ ] Working tree không chứa generated artifact, secret hoặc line-ending noise do task tạo.
- [ ] File được phân loại rõ theo scope và pre-existing changes.
- [ ] Full build/test/lint/Playwright pass với số liệu thật.
- [ ] Không chạy database patch/deploy và không commit/push/merge.
- [ ] Báo cáo kết thúc bằng `READY_FOR_CODE_REVIEW` hoặc `BLOCKED`.
