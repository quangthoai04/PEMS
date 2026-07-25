# BÁO CÁO TRIỂN KHAI — Luồng tạo đoàn khách V2 (đã đăng nhập) & Đồng bộ UI Xem đơn

> Thực hiện theo `PEMS_IMPLEMENT_AUTHENTICATED_V2_CREATE_AND_UNIFIED_DETAIL_UI_PLAN.md`.
> Audit hiện trạng: `AUDIT_AUTHENTICATED_V2_CREATE_AND_DETAIL_UI.md`.

## 1. Branch và commit

| | |
|---|---|
| Branch | `Canh-Iter1` |
| HEAD khi bắt đầu | `59c86766` |
| Trạng thái | **Chưa commit** — toàn bộ thay đổi đang ở working tree, chờ anh review |

## 2. Files changed

### Backend (7 file)

| File | Thay đổi |
|---|---|
| `PEMS.Application/Delegations/Commands/CreateVisitRequestV2/RegistrantIdentityRules.cs` | **MỚI** — nơi duy nhất trả lời "người đăng ký trên form có phải chính người đang đăng nhập không" |
| `…/CreateVisitRequestV2/CreateVisitRequestV2CommandHandler.cs` | Chặn direct-create khi email người đăng ký ≠ email actor |
| `…/InitiateVisitRequestV2/InitiateVisitRequestV2CommandHandler.cs` | Reject processing intent trước khi mint OTP |
| `…/VerifyAndCreateVisitRequestV2/VerifyAndCreateVisitRequestV2CommandHandler.cs` | Reject processing intent (defence in depth) |
| `PEMS.Domain/Constants/VisitRequestConstants.cs` | Thêm `REGISTRANT_EMAIL_VERIFICATION_REQUIRED` |
| `…/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs` | Xóa `MapGuestMember` chết |
| `…/Queries/GetVisitInstanceSummary/…` + `…/GetVisitInstanceContribution/…` | Xóa `MapGuestMember` chết (2 bản sao còn lại) |

### Frontend (12 file + 5 file mới)

| File | Thay đổi |
|---|---|
| `shared/utils/emailIdentity.ts` | **MỚI** — `normalizeEmail` / `isSameEmailIdentity`, cùng quy tắc với backend |
| `visit-request/components/v2/shared/visitStatus.ts` | **MỚI** — từ vựng trạng thái tách theo 2 enum thật |
| `…/shared/VisitStatusBadge.tsx` | **MỚI** |
| `…/shared/VisitSectionCard.tsx` | **MỚI** — header xanh + số thứ tự cam + badge "Chỉ đọc" + collapse |
| `…/shared/ReadOnlyInfoGrid.tsx` | **MỚI** — label/value 2 cột |
| `…/shared/PersonListTable.tsx` | **MỚI** — bảng người có STT + card mobile |
| `hooks/useVisitRequestFormV2.ts` | `currentUserEmail` + `isSelfRegistration` + rẽ nhánh submit |
| `components/v2/VisitRequestFormV2.tsx` | Nút "Tôi là người đăng ký", banner danh tính, khoá processing, chặn copy contact, OTP modal cho cả 2 mode |
| `components/v2/VisitRequestV2DetailView.tsx` | Viết lại theo bố cục 4 section |
| `components/v2/CampusVisitDetailCard.tsx` | Viết lại: header campus xanh, info grid, 2 bảng người, panel quyết định |
| `components/v2/VisitRequestV2SubmittedSummary.tsx` | Dùng `VisitStatusBadge` |
| `components/VisitHistoryTimeline.tsx` | Trục xanh, mốc cam cho quyết định, actor, retry, giờ wall-clock |
| `shared/i18n/locales/{vi,en}/visitRequestV2.json` | Từ vựng trạng thái mới + nhãn danh tính/detail |

### Tests (thêm 5 file mới, sửa 24 file)

Xem Mục 8.

## 3. Database/schema changes

**KHÔNG có.** Không thêm bảng/cột/enum/status nào. `REGISTRANT_EMAIL_VERIFICATION_REQUIRED` là error code
tầng ứng dụng, không phải giá trị lưu DB. `pems_db` **không bị đụng tới**.

## 4. Backend changes

### 4.1 Danh tính người đăng ký (P0)

`POST /api/v2/visit-requests` giờ là **self-registration only**. JWT chỉ chứng minh được hòm thư của chính
người gọi, nên form khai tên người khác bị trả về `409 REGISTRANT_EMAIL_VERIFICATION_REQUIRED` và **không ghi
gì**. Kiểm tra đặt **trước** ma trận processing, nên payload vừa mạo danh vừa giả `SELF_HOST` không thể tạo ra
dữ liệu dở dang.

So khớp = **trim + lowercase, không hơn**. KHÔNG gộp dấu chấm Gmail, KHÔNG bỏ `+alias`, KHÔNG đổi domain —
gộp lại là mở đường cho một tài khoản hành động dưới địa chỉ nó chưa từng chứng minh sở hữu. Hai chuỗi rỗng
**không** được coi là trùng nhau.

### 4.2 Luồng OTP cho đơn tạo hộ (P0)

**Không đẻ endpoint mới.** `/api/v2/visit-requests/initiate` + `/verify` đã làm đúng toàn bộ §8.2/§8.3
(validate full V2 dùng chung validator, mint OTP theo `submissionId`, bind snapshot, tạo request từ snapshot
đã bind, idempotent replay, chống fingerprint mismatch, consume OTP trong cùng transaction) và là
`[AllowAnonymous]` nên caller đã đăng nhập vẫn gọi được. Việc còn thiếu là **bắt** actor nội bộ đi qua đường
này — nay do frontend rẽ nhánh + backend chặn direct-create.

**Bổ sung:** cả initiate và verify nay **reject** payload mang direct processing thay vì im lặng bỏ qua. Trước
đây `CreateV2Async` được gọi không truyền initializers nên intent bị nuốt — client không phân biệt được
"đã áp dụng" với "bị bỏ". Reject ở initiate còn có nghĩa **không có OTP nào được gửi** vào hòm thư người thứ
ba vì một payload giả mạo.

### 4.3 Ai là `registrant_user_id` của đơn tạo hộ

Là **người được OTP xác minh** (tài khoản VISITOR được provision từ email đó), không phải nhân sự nội bộ đã
gõ hộ. Đúng tinh thần: OTP chứng minh ai là người đăng ký. Không thêm cột để ghi "người gõ hộ" (§3 cấm tự
thêm field).

## 5. Frontend changes

### 5.1 Nút "Tôi là người đăng ký"

- Chỉ hiện ở mode `authenticated`, đặt ở header section **Thông tin người đăng ký**.
- Gọi `GET /profiles/me` **chỉ khi bấm** — pre-fill lúc mount chính là hành vi nuốt mất bản nháp vừa khôi phục.
- Điền: họ tên · email · điện thoại · quốc tịch · chức vụ (`displayPosition`) · đơn vị (`displayDepartmentName` → `department.name` → campus).
- Trường hồ sơ trống thì **để trống** cho người dùng bổ sung, không nhét nhãn role vào chỗ chức vụ.
  *(Thực tế đã kiểm chứng ở real-stack: tài khoản nội bộ không có quốc tịch, người dùng phải tự chọn.)*

### 5.2 Banner danh tính + rẽ nhánh submit

Tính lại từ email **đang watch**, nên sửa email là banner, hợp đồng submit và panel processing đổi **cùng một
render** — không còn trạng thái "đã xác minh" sót lại:

| Trạng thái | Hiển thị | Submit |
|---|---|---|
| Email trùng tài khoản | xanh lá "không cần xác minh OTP" | `POST /v2/visit-requests` |
| Email khác | hổ phách "sẽ gửi OTP tới email này" | `initiate` → OTP → `verify` |

Nhãn nút submit đổi theo (đơn tạo hộ không được hứa tạo ngay). OTP modal nay render khi **có challenge**,
không còn phụ thuộc `mode` — nếu không sẽ có session token treo mà không có chỗ nhập mã.

### 5.3 Khoá processing theo danh tính

- Panel "Cách xử lý tại cơ sở này" chỉ hiện khi `authenticated` **AND** email trùng **AND** role STAFF.
- Email đổi sang người khác → **xóa** `campusProcessing` state (không chỉ ẩn).
- `getCampusProcessing()` kiểm tra lại lần cuối trước khi lên payload.
- Backend reject payload giả mạo (đã kiểm chứng bằng real-stack Journey E).

### 5.4 Primary Contact với actor nội bộ

Nút "Dùng thông tin người đăng ký" bị **disable** + helper text khi actor nội bộ đang tự đăng ký — backend
luôn trả `INTERNAL_REGISTRANT_CANNOT_BE_CONTACT`, để họ nhập xong rồi mới báo lỗi là phí công gõ. Khi họ tạo
hộ khách ngoài thì nút hoạt động bình thường (copy thông tin khách là hợp lệ).

## 6. Security/authorization changes

| Thay đổi | Trước | Sau |
|---|---|---|
| Direct-create khai tên người khác | **Tạo đơn thành công**, `registrant_user_id` ≠ `registrant_email` | 409 + không ghi gì |
| Direct-create + forged SELF_HOST | Tạo đơn, actor thành Host | 409 trước khi chạm ma trận processing |
| Processing intent trên đơn OTP | Im lặng bỏ qua | 422 `INVALID_CAMPUS_SUBMISSION_MODE`, không gửi OTP |
| `+alias` / dấu chấm Gmail | (không so sánh) | Coi là hòm thư khác |

## 7. UI changes

- **Card tổng quan:** mã đơn nổi bật, `VisitStatusBadge`, số cơ sở, badge mixed, action theo `allowedActions`.
- **4 section xanh-cam:** ① Người đăng ký · ② Đầu mối liên hệ · ③ Thông tin từng cơ sở · ④ Lịch sử thay đổi.
  Header `#004c91`, số thứ tự tròn `#f37021`, badge "Chỉ đọc", collapse có `aria-expanded`.
- **Card campus:** header xanh + trạng thái + khoảng thời gian + badge amendment; info grid; **2 bảng người
  hiện sẵn** (bỏ toggle — "ai sẽ đến" chính là lý do người ta mở card này); panel quyết định nền slate.
- **Bảng người:** header `#004c91` chữ trắng, cột STT tính `index + 1` (không lưu DB), row hover, scroll
  trong wrapper riêng; **dưới `sm` chuyển thành card giữ đủ 5 trường**.
- **Timeline:** trục xanh, mốc cam cho DECISION/AMENDMENT_DECISION, có actor, có retry, giờ wall-clock
  (trước đây `new Date()` làm lệch múi giờ).
- **Không còn raw enum:** giá trị lạ rơi về nhãn "Không xác định" thay vì hét `WAITING_REQUEST_APPROVAL`.

### Chuẩn hóa trạng thái — đối chiếu SQL thật

Kế hoạch §11 liệt kê vài giá trị **không tồn tại** trong schema. Đối chiếu
`PEMS_FULL_V2_NO_SEED_DATA_GALLERY.sql`:

| Enum | Giá trị thật |
|---|---|
| `visit_requests.status` | PENDING_APPROVAL · PARTIALLY_APPROVED · APPROVED · REJECTED · CANCELLED |
| `visit_request_campuses.status` | WAITING_REQUEST_APPROVAL · ASSIGNED · BEFORE_VISIT · DURING_VISIT · AFTER_VISIT · CLOSED · CANCELLED · REJECTED |

⇒ `PENDING`, `COMPLETED`, `IN_PROGRESS` (đang có trong i18n cũ) là **key chết** — đã bỏ. `APPROVED` chỉ thuộc
request, `ASSIGNED` chỉ thuộc campus ⇒ tách 2 nhóm `status.request.*` / `status.instance.*`, không dùng chung.
Fixture test cũ đặt `instanceStatus: 'APPROVED'` (bất khả thi) cũng đã sửa thành `ASSIGNED`.

## 8. Tests added

| Tầng | File | Số test |
|---|---|---|
| Unit (BE) | `PEMS.UnitTests/VisitRequests/RegistrantIdentityRulesTests.cs` | 26 |
| Integration (BE) | `PEMS.IntegrationTests/VisitRequests/AuthenticatedRegistrantIdentityV2ApiTests.cs` | 7 |
| Integration (BE) | `PEMS.IntegrationTests/VisitRequests/AuthenticatedDelegatedOtpV2Tests.cs` | 7 |
| Unit (FE) | `visit-request/__tests__/VisitRequestV2RegistrantIdentity.test.tsx` | 8 |
| Unit (FE) | `visit-request/__tests__/VisitV2SharedPresentation.test.tsx` | 16 |
| Real-stack | `tests-realstack/authenticated-registrant-identity.realstack.spec.ts` | 3 (Journey A/B/E) |
| | **Tổng mới** | **67** |

**Test sửa (không làm yếu):**
- `useVisitRequestFormV2.test.tsx`, `VisitRequestFormV2Processing.test.tsx` — bổ sung tiền đề "đơn đứng tên ai",
  thêm case tạo hộ. Test cũ ngầm giả định "authenticated ⇒ luôn direct-create", đúng cái kế hoạch này sửa.
- `CampusVisitDetailCard.test.tsx` — thay test "danh sách thu gọn" bằng test STT/mobile/empty.
- `fixtures.ts` — `instanceStatus` về giá trị schema cho phép.
- **24 file integration** — seed helper trước đây hardcode `registrant@example.com` trong khi actor là user
  khác, tức mô tả một đơn **không ai gửi được**. Thêm `V2SeedActor.Email(userId)` đọc email thật từ DB.

## 9. Test result

| Gate | Lệnh thật | Kết quả |
|---|---|---|
| Backend build | `dotnet build PEMS.slnx` | ✅ 0 errors (182 warnings, đều có sẵn) |
| ArchitectureTests | `dotnet test tests/PEMS.ArchitectureTests` | ✅ **14/14** |
| UnitTests | `dotnet test tests/PEMS.UnitTests` | ✅ **1050/1050** (baseline 1024) |
| IntegrationTests | `dotnet test tests/PEMS.IntegrationTests` | ✅ **602/602** (baseline 588) |
| Frontend typecheck | `npm run lint` | ✅ 0 errors |
| Frontend unit | `npm run test:unit` | ✅ **471/471**, 38 file (baseline 435/36) |
| Frontend build | `npm run build` | ✅ built in 26.9s |
| Real-stack E2E | `npm run test:e2e:realstack` | ✅ **20/20** |
| Whitespace | `git diff --check` | ✅ sạch |

> Build backend phải tránh `bin/` bị PEMS.Api dev-server khoá: dùng `-p:BaseOutputPath=".tmp-build/…"`.
> **Đường dẫn phải nằm TRONG repo** — harness đi ngược lên tìm repo root từ thư mục binary, trỏ ra `%TEMP%`
> là mọi API test chết ở `FindRepositoryRoot`. Real-stack cần `MYSQL_BIN` trỏ tới `mysql.exe`.

## 10. Manual/E2E evidence

Real-stack chạy trên DB **dùng-một-lần** `pems_e2e_realstack` (tự tạo từ master SQL, tự drop; không đụng
`pems_db` / `pems_test` / `pems_pr3_test`). Không mock network; OTP đọc từ FileSink inbox đúng như backend ghi.

| Journey | Nội dung | Kết quả |
|---|---|---|
| **A** | Staff Leader bấm "Tôi là người đăng ký" → banner "không cần OTP" → thấy SELF_HOST + ASSIGN_HOST → submit → `POST /v2/visit-requests` **200**, KHÔNG đi qua `/initiate` → hiện mã `VR…` | ✅ |
| **B** | Cùng Leader đổi email người đăng ký thành khách ngoài → panel processing **biến mất** (`toHaveCount(0)`) → submit → OTP vào hòm thư **khách**, không phải Leader → verify → tạo đơn | ✅ |
| **E** | Gọi thẳng API payload tạo hộ + forged `SELF_HOST` → **409 `REGISTRANT_EMAIL_VERIFICATION_REQUIRED`**; replay cùng `submissionId` vẫn 409 (không có state dở dang để retry hoàn tất) | ✅ |
| C/D | Đã có sẵn: `authenticated-workflows` Journey D/E/F/G/H + `authenticated-ui-workflows` §6-§13 | ✅ (17 test cũ vẫn xanh) |

## 11. Known limitations

1. **Đơn tạo hộ không ghi lại "ai gõ hộ".** `registrant_user_id` là người được OTP xác minh; nhân sự nội bộ
   gõ hộ không để lại dấu vết trong `visit_requests`. Muốn truy vết cần thêm cột — §3 cấm tự thêm, nên để lại
   cho anh quyết định.
2. **OTP đơn tạo hộ gửi tới người đăng ký, không gửi cho người gõ.** Đúng §5.4, nhưng nghiệp vụ thực tế nghĩa
   là nhân sự nội bộ phải xin mã từ khách — đáng cân nhắc UX cho lần sau.
3. **Error code OTP dùng lại bộ có sẵn** (`OTP_INVALID`, `OTP_EXPIRED`, `OTP_RATE_LIMITED`… từ `OtpErrorCodes`)
   thay vì đẻ thêm họ `REGISTRANT_OTP_*` như §8.4 gợi ý. Tạo bộ song song sẽ nhân đôi một contract đang chạy
   tốt; nếu anh muốn đúng chữ trong plan thì em đổi.
4. **`/gap`-style traceability chưa cập nhật** — em không đụng `docs/_shared/`.
5. **Hai hằng số harness đã sửa** (Mục 12) — cần anh xác nhận đây là ý định.

## 12. ⚠️ Điểm cần anh xác nhận

Integration suite **không chạy được tại HEAD** (0/588) vì 2 hằng số contract đã lệch từ trước:

| File | Sửa |
|---|---|
| `CanonicalSqlScript.cs:37` | `ExpectedSha256` → `84680cf…` (file thật sau commit `59c86766`) |
| `DisposableDatabaseManager.cs:23` | `ExpectedBaseTableCount` 81 → **82** (`account_email_confirmations`) |

Đây là **thủ tục đã ghi trong chính comment của file** ("update this constant in the same commit as the .sql
change"), không phải bỏ test cho xanh giả — không sửa thì 0 test chạy. Nhưng vì nó là cổng an toàn chống
schema drift do anh dựng, em nêu rõ để anh xác nhận thay vì lặng lẽ đổi.

## 13. Việc chưa hoàn thành

| # | Việc | Lý do |
|---|---|---|
| 1 | Commit + push | Kế hoạch không yêu cầu; em giữ nguyên working tree chờ anh review |
| 2 | Screenshot UI mới | Real-stack chạy headless; đã thay bằng assertion DOM cụ thể |
| 3 | Kiểm tra responsive bằng mắt | Đã có test DOM cho mobile card, nhưng chưa xem thật trên thiết bị |
| 4 | Cập nhật `V2_ONLY_REALSTACK_JOURNEY_MATRIX.md` | File này vẫn ghi 27 journey "NOT RUN" từ trước, ngoài phạm vi |

## 14. Điểm resume chính xác

Mọi slice P0/P1 của kế hoạch đã xong và có gate xanh. Nếu tiếp:

1. **Trả lời Mục 12** (xác nhận 2 hằng số harness) — chặn việc merge.
2. **Quyết Known limitation #1** (có cần cột "người gõ hộ" không). Nếu có → thêm cột + migration + ghi vào
   create service, và bổ sung assertion vào `AuthenticatedDelegatedOtpV2Tests`.
3. **Quyết Known limitation #3** (bộ error code OTP).
4. Sau đó: `git checkout -b <nhánh>` + commit theo 8 slice của §21-§29, PR về `Dev`.
