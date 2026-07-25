# PEMS — PHASE 4.5 MASTER CONTINUATION PROMPT
# FROM HEAD 7f711122 TO PHASE 4.5 VERIFIED

Bạn tiếp tục triển khai PEMS Pure V2 từ baseline hiện tại.

Đây là phiên **IMPLEMENTATION + RUNTIME VERIFICATION**. Phải đọc code thật, sửa defect thật, bổ sung regression test, chạy gate trên đúng một HEAD và commit theo functional slice.

Không chỉ audit, không chỉ viết báo cáo và không làm tối thiểu để đủ checklist.

---

# 1. Trạng thái hiện tại đã xác nhận

```text
Phase 1 VERIFIED
Phase 2 VERIFIED
Phase 3 VERIFIED
Phase 4 backend/frontend code gates GREEN
Phase 5 VERIFIED
Phase 4.5 IN PROGRESS
Phase 6 PAUSED
Project NOT YET FINAL
```

Git:

- Local branch: `Canh-Iter1`
- Tracking: `origin/Cảnh-Iter1`
- Local HEAD: `7f711122`
- Remote HEAD: `7f711122`
- Ahead/behind: `0/0`
- Stash: `8/8`
- Owner-WIP backup `f27b0853` còn nguyên
- Bốn file prompt hiện untracked theo `git status`
- Không được tự add các prompt
- Không được drop/pop stash
- Không có tên AI trong commit metadata của agent

Baseline gate hiện tại:

- Backend build: 0 error
- ArchitectureTests: `14/14`
- UnitTests: `958/958`
- IntegrationTests: `576/576`
- Frontend lint: 0 error
- Frontend unit: `406/406`
- Frontend build: thành công
- `git diff --check`: sạch
- `pems_db`: 81 bảng
- Disposable/readiness DB còn sót: 0

Các commit Phase 4.5 đã push:

```text
b58e6a5b feat(health): enforce Pure V2 database readiness
703996a7 feat(ui): shared error-state toolkit and visit-process dependency gating
7f711122 fix(visit): remove dead Tokyo/Monash after-visit mocks
```

Không amend, squash, reset, rebase hoặc rewrite các commit này.

---

# 2. Kết quả phiên trước — đã hoàn thành, không làm lại

## 2.1 Pure V2 readiness

Đã có:

- `GET /api/health/live`
- `GET /api/health/readiness`
- kiểm tra bảng/cột Pure V2 bắt buộc
- phát hiện cột V1 `form_schema_version` bị tái xuất hiện
- Production ẩn database/schema details
- không lộ secret
- real-MySQL integration tests

Không viết lại readiness trừ khi phần còn lại phát hiện regression có bằng chứng.

## 2.2 Shared error-state toolkit

Đã có:

- `normalizeApiError`
- phân biệt `forbidden / notFound / conflict / validation / server / network / timeout / unknown`
- nhận diện riêng `VISIT_FORM_DETAIL_MISSING`
- lấy correlation id
- che secret bị rò trong message
- `LoadingState`
- `EmptyState`
- `ErrorState`
- `StaleDataBanner`

Không tạo thêm một error classifier thứ hai.

## 2.3 Visit Process dependency gating

Đã sửa:

- reminder load fail không biến thành default im lặng
- agenda candidates fail không biến thành `[]`
- Save/Cancel/assign bị chặn khi dependency fail
- `saveAgenda` không validate sai bằng danh sách rỗng giả
- có stale banner và retry

Phạm vi mới áp dụng tại visit-prep panel. Các surface còn lại vẫn phải rà tiếp.

## 2.4 After-Visit News

Đã xác minh luồng thật:

```text
VisitAfterTab
→ VisitNewsSection
→ VisitNewsPostList
→ Shared News Management form
→ ?visitInstanceId&returnTo
→ real persistence
```

Đã xóa:

- University of Tokyo mock
- Monash mock
- Unsplash preview
- hardcoded folder id
- fake auto-generated draft

Không tạo editor News thứ hai trong `VisitAfterTab`.

## 2.5 OAuth classification

Đã xác nhận:

- tracked credentials là shared-development
- không có production/personal production token được dùng bởi Production config
- readiness không lộ secret
- validator chỉ nêu tên key, không echo giá trị

Debt còn lại:

- base `appsettings.json` có non-blank dev JWT/DB/SMTP
- Production có thể vô tình dùng dev defaults nếu env bị thiếu

Theo policy:

```text
Shared development credentials intentionally tracked
Production security hardening deferred
```

Không tự xóa/rotate/rewrite history trong Phase 4.5.

---

# 3. Quyết định chính thức của chủ dự án

## 3.1 Canonical Visit Photo upload contract

Áp dụng cho **Visit Photos**, không áp dụng cưỡng ép cho Gallery, News, Documents hoặc Business Card.

```text
Allowed MIME:
- image/jpeg
- image/png
- image/webp

Max size:
- 5 MB mỗi file

Max count:
- 10 file trong một request/upload batch

Video:
- không hỗ trợ tại Visit Photos endpoint
```

Yêu cầu:

- backend validator là nguồn sự thật
- frontend `accept`, text và validation phải khớp backend
- kiểm tra cả MIME và extension
- không tin chỉ extension
- không dùng generic 25 MB denylist cho Visit Photos
- không unlimited count
- có error code ổn định cho type/size/count

Business Card giữ contract riêng:

```text
JPEG/PNG/WEBP/PDF
10 MB
1 file
```

News, Gallery và Documents giữ contract riêng theo endpoint hiện có.

## 3.2 VisitDuringTab và VisitRequestDetail live mock

Không giữ dữ liệu mẫu trong UI production.

### Wire dữ liệu thật nếu backend/API đã có

- delegation name
- start/end time
- guest/support/participant
- host
- campus
- status
- agenda/logistics summary nếu API đã cung cấp

### Nếu chưa có persistence/backend contract

Với:

- rating
- notes
- business card/contact
- chức năng During Visit khác

thì:

- không hiển thị dữ liệu mẫu
- không dùng localStorage làm persistence nghiệp vụ
- hoặc nối API thật
- hoặc ẩn/disable với nhãn “Chưa hỗ trợ”
- không giả như tính năng đã hoạt động

### Business Card

Khi chưa OCR:

- input rỗng
- chỉ có placeholder mờ
- placeholder không được submit

Khi OCR thành công:

- điền dữ liệu OCR thật
- cho phép chỉnh sửa
- lưu backend thật nếu tính năng được công bố là hoạt động

Khi OCR lỗi:

- input rỗng
- hiển thị lỗi
- cho nhập tay nếu contract cho phép

## 3.3 Visit Detail presentation

Màn xem đơn cần:

- popup/modal hoặc drawer lớn khi mở từ danh sách
- vẫn giữ deep link và refresh
- giữ filter/page/scroll khi đóng
- dùng chung detail component/data hook
- không tạo hai implementation riêng

Danh sách người phải có bảng:

| STT | Họ và tên | Chức vụ | Đơn vị công tác | Quốc tịch |

Áp dụng cho:

- khách
- nhân sự hỗ trợ
- participant
- invitee
- nhân sự phối hợp

Status, audit và metadata kỹ thuật phải được chuyển thành UI thuần Việt, không render raw code.

---

# 4. Preflight

Chạy:

```bash
git status --short --branch
git branch --show-current
git rev-parse HEAD
git fetch origin Cảnh-Iter1
git rev-parse origin/Cảnh-Iter1
git rev-list --left-right --count origin/Cảnh-Iter1...HEAD
git log -15 --oneline --decorate
git stash list --format="%gd %H %s"
git diff --check
```

Expected:

- local/remote `7f711122`
- ahead/behind `0/0`
- stash `8/8`
- bốn prompt vẫn untracked
- working tree không có thay đổi không rõ nguồn

Nếu remote hoặc working tree thay đổi ngoài dự kiến:

- không merge
- không rebase
- không push
- không commit/revert thay đổi lạ
- báo phạm vi và dừng

---

# 5. Functional Slice A — Error-state sweep + dashboard partial failure

Commit dự kiến:

```text
fix(ui): complete visit loading and error states
```

Gom các phần:

- API error mapping còn thiếu
- loading/data/empty/error
- dependency failure
- dashboard/calendar/task partial failure
- desktop/mobile parity

## 5.1 Surfaces bắt buộc

1. Dashboard HO
2. Visit Photos
3. Feedback
4. Meeting Minutes
5. Logistics
6. Calendar
7. Pending lists
8. Các detail/list Visit còn dùng ad-hoc handling

## 5.2 State contract

Mỗi màn phải phân biệt:

```text
idle
loading
success-data
success-empty
error
stale-data
```

Không dùng:

```text
[]
false
0
null
```

để biểu diễn cả API failure và empty.

## 5.3 Error mapping

Dùng `normalizeApiError` hiện có:

- 403 → Không có quyền
- 404 → Không tồn tại
- 409 `VISIT_FORM_DETAIL_MISSING` → Dữ liệu Pure V2 chưa đầy đủ
- 409 khác → Business conflict đúng error code
- 422 → Validation
- 5xx → Lỗi hệ thống
- network → Lỗi kết nối
- timeout → Hết thời gian chờ
- unknown → Lỗi không xác định

Không render message tiếng Anh thô khi đã có mapping.

## 5.4 Dashboard HO

Nếu nhiều API:

- spinner toàn trang phải kết thúc
- một card fail không giữ toàn dashboard loading
- card lỗi có error state riêng
- card khác vẫn render theo contract
- retry riêng hoặc retry toàn dashboard
- stale data có banner
- không chỉ `console.error`

## 5.5 Dependency API fail

Với Logistics, Calendar, candidates, pending lists:

- đánh dấu dependency failed
- chặn action phụ thuộc
- hiện error + retry
- không gửi mutation bằng default data

## 5.6 Desktop/mobile parity

Cùng endpoint phải có cùng semantics ở:

- desktop table
- tablet
- mobile cards

Không được desktop hiện lỗi còn mobile hiện “không có dữ liệu”.

## 5.7 Tests

Frontend tests tối thiểu:

1. Loading
2. Success-data
3. Success-empty
4. 403
5. 404
6. 409 `VISIT_FORM_DETAIL_MISSING`
7. 500
8. Network
9. Timeout
10. Retry success
11. Spinner kết thúc
12. Partial dashboard failure
13. Stale banner
14. Dependency fail → action disabled
15. Desktop/mobile parity

---

# 6. Functional Slice B — Visit detail UI + Pure V2 campus scope

Commit dự kiến:

```text
fix(visit-ui): standardize details and campus-scoped views
```

Có thể tách thành hai commit nếu diff quá lớn:

```text
fix(visit-ui): standardize visit detail presentation
fix(scope): enforce campus-scoped visit detail responses
```

Không tách theo từng component.

## 6.1 Inventory

Tìm mọi nơi mở/xem một Visit Request hoặc Visit Instance:

- Quản lý tiếp khách
- Request detail
- Visit Process
- Staff Leader processing
- Staff/Host detail
- HO monitoring
- Invitation detail
- Calendar detail
- Agenda
- Minutes
- Documents
- Photos
- Expense
- Feedback
- News contribution
- Report preview

Lập matrix:

| Surface | Actor | Common data | Campus data | Backend scope | UI mode | Test | Status |
|---|---|---|---|---|---|---|---|

Không để `UNREVIEWED` hoặc `CODE-OK/TEST-MISSING`.

## 6.2 Bảng danh sách người

Tạo/tái sử dụng component chung cho:

- khách
- nhân sự hỗ trợ
- participant
- invitee
- nhân sự phối hợp

Desktop:

| STT | Họ và tên | Chức vụ | Đơn vị công tác | Quốc tịch |

Yêu cầu:

- STT từ 1
- deterministic ordering
- empty state rõ
- stable key
- mobile card đủ cùng dữ liệu
- không mất cột giữa desktop/mobile
- locale tiếng Việt nhất quán

## 6.3 Không render code kỹ thuật

Quét:

- `.status`
- enum uppercase
- `SOURCE_TYPE`
- `source=`
- `approvalRevision=`
- role code
- audit action code
- media consent raw
- invitation/logistics/news status raw

Dùng mapping tập trung:

- status label tiếng Việt
- badge/icon
- fallback “Trạng thái chưa xác định”
- không render enum thô cho người dùng thường

## 6.4 Audit/history UI

Không render trực tiếp:

```text
CANCELLED
source=CREATE;approvalRevision=1
SOURCE_TYPE=CAMPUS_DECISION
Seed migration
```

Hiển thị dạng UI:

- loại sự kiện
- người thực hiện
- thời gian
- trạng thái trước/sau
- lý do
- phiên bản
- campus

Free-text của người dùng không được dịch máy móc.

## 6.5 Popup/modal có deep link

Từ danh sách:

```text
Bấm Xem
→ modal/drawer lớn
→ URL cập nhật route detail
→ đóng giữ filter/page/scroll
→ refresh mở lại detail
→ browser Back đóng modal
→ direct link hoạt động
```

Không tạo hai implementation detail.

Modal và full-page phải dùng chung component/data hook.

## 6.6 Pure V2 visibility

### HO

- common request info
- tất cả campus
- read-only
- tab/accordion theo campus
- không trộn campus

### Registrant / Primary Contact

- common info
- toàn bộ campus thuộc request của mình
- mutation theo lifecycle

### Staff Leader

- common info cần thiết
- chỉ campus thuộc scope
- không nhận sibling guest/support/contact/agenda/logistics/expense/minutes/photo

### Staff / Host

- common info cần thiết
- chỉ assigned/related instance

### Participant / Student

- chỉ related instance

### Admin

- không business access nếu permission matrix hiện hành quy định

Backend phải scope trước projection.

Không gửi sibling data rồi ẩn bằng UI.

## 6.7 Common vs campus-specific

### Common

- request code
- registrant
- primary contact
- source
- request lifecycle tổng
- ownership/audit cấp request

### Campus-specific

- campus
- start/end
- delegation name
- visit type
- purpose
- working content
- operational contact
- guests
- support
- language
- transportation
- media consent
- host
- decision
- agenda
- logistics
- expense
- minutes
- photos
- news

Không coi field là common chỉ vì UI cũ đặt ở phần “Thông tin đoàn khách”.

## 6.8 Backend tests

Fixture ba campus với dữ liệu khác nhau rõ:

1. HO thấy đủ ba campus, tách biệt
2. Staff Leader HN chỉ thấy common + HN
3. JSON của Staff Leader HN không chứa HCM/ĐN
4. Host HCM chỉ thấy HCM
5. Participant ĐN chỉ thấy ĐN
6. Owner thấy toàn request
7. Unauthorized → 403
8. Missing target → 404
9. Missing FormDetail → 409
10. Không lộ sibling qua keyword/projection
11. Uniform vẫn đọc target detail
12. Mixed không representative campus

## 6.9 Frontend tests

1. Guest/support có STT
2. Desktop/mobile đủ cùng dữ liệu
3. Raw enum không xuất hiện
4. Status tiếng Việt đúng
5. Audit không hiện metadata raw
6. List → modal
7. URL/back/refresh
8. Giữ filter/page/scroll
9. Staff Leader chỉ render campus backend trả
10. HO render nhiều campus
11. 409 hiển thị lỗi Pure V2
12. Loading/empty/error tách biệt

---

# 7. Functional Slice C — OTP verify/create P0

Commit dự kiến:

```text
fix(visit): preserve public form state until OTP creation succeeds
```

Đây là P0 cao nhất vì có thể làm người dùng nhập form xong bị thoát và không tạo đơn.

## 7.1 Reproduce real stack

Ghi:

- frontend URL
- backend URL/port
- environment
- database name
- HEAD
- initiate payload/response
- verify payload/response
- HAR
- console
- backend log
- pending snapshot
- DB rows trước/sau

Không kết luận dựa trên toast.

## 7.2 Kiểm tra

- sai endpoint
- route/version mismatch
- kill-switch
- OTP sai/hết hạn
- pending snapshot missing
- fingerprint mismatch
- payload mismatch
- duplicate/idempotency
- transaction rollback
- thiếu campus detail
- wrong DB
- modal đóng trong `finally`
- navigate trước success
- reset form khi fail
- success response thiếu requestId

## 7.3 Success contract

Chỉ success khi:

- HTTP success đúng contract
- requestId hợp lệ
- request tồn tại
- instances tồn tại
- form details đủ
- transaction commit

## 7.4 Fail behavior

Khi fail:

- modal vẫn mở
- form giữ nguyên
- campus giữ nguyên
- không navigate
- không reset
- pending state không bị xóa sai
- lỗi rõ
- retry/resend đúng loại

## 7.5 Idempotency tests

1. Verify success tạo đúng một request
2. Replay không tạo request thứ hai
3. Replay không gửi notification lần hai
4. Timeout sau commit, retry trả request cũ
5. Fail không partial data
6. Fail không đóng modal
7. Form không mất
8. Chỉ success mới navigate
9. requestId tồn tại DB
10. Mixed ba campus tạo đủ detail

Không commit nếu chưa chứng minh end-to-end.

---

# 8. Functional Slice D — After-Visit expense init + Business Card + live mock removal

Commit dự kiến:

```text
fix(visit): stabilize after-visit data and remove live mocks
```

Có thể tách OTP riêng, còn expense/card/mock trong commit này.

## 8.1 Expense initialization

Logic đúng:

```text
AFTER_VISIT + chưa có report
→ initialize đúng một lần

AFTER_VISIT + có report
→ load report

CLOSED
→ load readonly, không initialize

Trạng thái khác
→ không initialize
```

Điều tra duplicate toast/request:

- Strict Mode
- duplicate `useEffect`
- unstable dependencies
- child gọi trùng
- retry
- race check/init

Tests:

1. AFTER_VISIT chưa report → một lần
2. AFTER_VISIT đã report → không init
3. CLOSED → không init
4. BEFORE/DURING → không init
5. remount/Strict Mode không duplicate
6. load fail → error, không init
7. không toast trùng
8. backend chỉ một report

Không render toast tiếng Anh thô.

## 8.2 Business Card

Xóa mọi demo value:

- Takahiro Sato
- Giám đốc Nhân sự
- Tập đoàn công nghệ FPT
- phone/email/address/site demo
- dữ liệu tương tự

Khi chưa OCR:

- value empty
- placeholder mờ
- placeholder không submit

OCR success:

- dữ liệu thật
- editable
- persist thật nếu feature supported

OCR fail:

- empty
- error
- manual input nếu allowed

Tests:

1. form mới rỗng
2. placeholder không submit
3. OCR success điền thật
4. OCR fail không demo
5. sửa OCR value lưu đúng
6. reload còn dữ liệu nếu persisted
7. instance isolation
8. production bundle không còn demo string

## 8.3 VisitDuringTab / VisitRequestDetail

Phải wire:

- guest/support/participant thật
- delegation name thật
- start/end thật
- host/campus/status thật

Với rating/notes/contact chưa có backend persistence:

- không show mock
- hide/disable “Chưa hỗ trợ”
- không localStorage làm nghiệp vụ

Tests:

- không còn Kenji Suzuki/Tokyo sample
- request A không nhận dữ liệu mẫu B
- reload/persistence đúng
- mixed không lấy campus đầu tiên
- routed VisitRequestDetail dùng target instance data

---

# 9. Functional Slice E — Canonical Visit Photo upload

Commit dự kiến:

```text
fix(upload): enforce canonical visit photo limits
```

## 9.1 Backend validator

Enforce:

```text
image/jpeg
image/png
image/webp
max 5 MB/file
max 10 files/request
```

Không video.

Không generic 25 MB denylist.

## 9.2 Frontend

Cả Visit Photos panel và After tab phải thống nhất:

- accept JPEG/PNG/WEBP
- 5 MB
- max 10
- cùng text
- cùng error mapping

## 9.3 Tests

1. JPEG/PNG/WEBP hợp lệ
2. >5 MB bị từ chối
3. file thứ 11 bị từ chối
4. video bị từ chối
5. PDF bị từ chối ở Visit Photos
6. MIME giả bị từ chối
7. extension giả bị từ chối
8. FE text khớp
9. mobile/desktop cùng rule
10. backend vẫn là source of truth

Không thay contract Business Card/News/Gallery/Documents.

---

# 10. Commit policy

Không giữ toàn Phase 4.5 uncommitted.

Commit theo functional slice, không theo file.

Các commit còn lại tối đa:

```text
fix(ui): complete visit loading and error states
fix(visit-ui): standardize details and campus-scoped views
fix(visit): preserve public form state until OTP creation succeeds
fix(visit): stabilize after-visit data and remove live mocks
fix(upload): enforce canonical visit photo limits
```

Có thể gộp hai slice có cùng hành vi nếu diff vẫn review được.

Không tạo:

- report-only commit
- test-count commit
- `fix`
- `fix again`
- mỗi component một commit

Sau mỗi slice:

```bash
dotnet build PEMS.slnx
dotnet test <targeted backend tests>
npm run lint
npm run test:unit
npm run build
git diff --check
```

Nếu chạm shared authorization, DbContext, read service, lifecycle, upload backend hoặc OTP transaction:

- chạy full IntegrationTests trước commit

Có thể push sau mỗi functional slice xanh để giảm rủi ro remote thay đổi.

Trước push:

```bash
git fetch origin Cảnh-Iter1
git rev-list --left-right --count origin/Cảnh-Iter1...HEAD
```

Nếu remote đổi:

- không merge/rebase
- không push
- báo và dừng

---

# 11. Browser/E2E targeted

Phải chạy ít nhất:

## Error-state screens

- Visit Process
- Visit Photos
- Minutes
- Feedback
- Dashboard HO

Các trạng thái:

- data
- empty
- 403
- 404
- 409
- 500
- network
- retry

## Visit detail

- list → modal
- direct link
- refresh
- Back
- Staff Leader single-campus scope
- HO multi-campus
- mobile/desktop
- raw enum absence

## OTP

- initiate
- verify success
- verify fail giữ form
- replay
- mixed campus

## After Visit

- CLOSED không init expense
- AFTER_VISIT init một lần
- Card inputs empty
- OCR success/fail
- no live mock

## Upload

- valid images
- invalid type
- oversize
- over-count

---

# 12. Single-HEAD evidence package

Tạo ngoài repo hoặc ignored path:

```text
phase45-final-evidence/
```

Gồm:

- HEAD/status
- frontend URL
- backend URL/port
- environment
- DB name
- readiness result
- backend build log
- frontend build log
- Architecture TRX
- Unit TRX
- Integration TRX
- frontend unit result
- HAR
- browser traces
- screenshots
- OTP DB state
- SQL/canonical hash đang dùng
- timestamps

Không dùng evidence từ HEAD khác.

---

# 13. Phase 4.5 final gate

Chỉ kết luận:

```text
Phase 4.5 VERIFIED
Phase 6 AUTHORIZED
```

khi:

1. FE–BE–DB cùng baseline
2. Readiness pass/fail đúng
3. Error không thành empty
4. 403/404/409/500/network đúng
5. Dashboard không loading vô hạn
6. Dependency fail không mutation default
7. Desktop/mobile parity
8. Visit detail có STT và UI thuần locale
9. Raw enum/audit metadata không lộ
10. Popup/deep-link hoạt động
11. Campus scope đúng backend
12. OTP create thành công end-to-end
13. OTP fail giữ form
14. Expense init đúng state và không duplicate
15. Card Visit không demo
16. During/RequestDetail không live mock
17. Visit photo contract thống nhất
18. Backend build xanh
19. Architecture/Unit/Integration xanh
20. Frontend lint/unit/build xanh
21. Browser tests xanh
22. Evidence cùng một HEAD
23. `git diff --check` sạch
24. `pems_db` không mutation ngoài dự kiến
25. Disposable DB = 0
26. Không tên AI trong commit metadata

Nếu còn P0:

```text
Phase 4.5 IN PROGRESS
Phase 6 PAUSED
Project NOT YET FINAL
```

---

# 14. Báo cáo cuối phiên

Báo:

```text
Current phase/slice
Local/remote HEAD
Ahead/behind
Stash/untracked

Completed surfaces
Defects confirmed
False positives
Business decisions applied
Files changed

Tests added
Counts before/after
First-failure evidence
Backend/frontend/database gate

Commits
Push status
Remaining P0/P1
Evidence package
Exact resume point
```

Không dừng chỉ để hỏi có tiếp tục không.

Chỉ dừng khi:

- remote/working tree thay đổi ngoài dự kiến
- cần business decision chưa có
- cần destructive DB operation
- cần production credential/deployment
- platform hard limit

Mọi lỗi code/test/fixture thông thường phải tự root-cause và tiếp tục.
