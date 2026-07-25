# AUDIT — Authenticated V2 Create & Detail UI

> Slice 0 deliverable của `PEMS_IMPLEMENT_AUTHENTICATED_V2_CREATE_AND_UNIFIED_DETAIL_UI_PLAN.md`.
> Ghi hiện trạng tại HEAD **trước khi** sửa code. Mọi kết luận có file + dòng.

## 0. Preflight

| Mục | Giá trị |
|---|---|
| Branch | `Canh-Iter1` |
| HEAD khi audit | `59c86766` ("sql") |
| Branch đích của PR | `Dev` |
| Working tree | sạch (chỉ có `docs/Ver2Carnh/canh/` untracked) |

### Baseline gates

| Gate | Lệnh thật | Kết quả baseline |
|---|---|---|
| Backend build | `dotnet build PEMS.slnx` | ✅ 0 errors, 181 warnings |
| ArchitectureTests | `dotnet test tests/PEMS.ArchitectureTests` | ✅ 14/14 pass |
| UnitTests | `dotnet test tests/PEMS.UnitTests` | ✅ 1024/1024 pass |
| Frontend typecheck | `npm run lint` (= `tsc --noEmit`) | ✅ 0 errors |
| Frontend unit | `npm run test:unit` (vitest) | ✅ 435 pass / 36 files |
| IntegrationTests | `dotnet test tests/PEMS.IntegrationTests` | ❌ **0 chạy được** tại HEAD (xem A-0); sau khi sửa 2 hằng số harness: ✅ 588/588 |

> Không có script `npm run test`; script thật là `test:unit` / `test:e2e` / `test:e2e:realstack`.
> Backend build phải tránh `bin/` đang bị PEMS.Api dev-server khoá — dùng
> `-p:BaseOutputPath=".tmp-build/it/"` (đường dẫn **trong repo**, vì harness đi ngược lên tìm
> repo root từ thư mục binary; trỏ ra `%TEMP%` sẽ làm mọi API test chết ở `FindRepositoryRoot`).

---

## A-0. Blocker môi trường (pre-existing, KHÔNG do task này)

Toàn bộ IntegrationTests không chạy được tại HEAD vì 2 hằng số contract đã lệch:

| # | File | Vấn đề | Tác động | Mức |
|---|---|---|---|---|
| A-0.1 | `tests/PEMS.IntegrationTests/TestInfrastructure/CanonicalSqlScript.cs:37` | `ExpectedSha256 = 577f399…` không khớp file thật. File tại `89399f2c` = `d1d418a…`, tại HEAD `59c86766` = `84680cf…` (commit đó append 805 dòng demo-data vào script canonical). | Mọi test dùng disposable DB ném `Canonical SQL hash mismatch` → 477/588 FAIL | P0 |
| A-0.2 | `tests/PEMS.IntegrationTests/TestInfrastructure/DisposableDatabaseManager.cs:23` | `ExpectedBaseTableCount = 81` nhưng script tạo **82** base table (`account_email_confirmations` thêm từ đợt P0 email-confirmation, hằng số không được cập nhật cùng). | Sau khi qua được hash gate vẫn abort `expected 81, found 82` | P0 |

Cả hai đã lệch từ **trước** commit `59c86766` — nghĩa là integration suite đã không chạy trong nhiều commit.
Cách sửa đúng theo chính comment trong `CanonicalSqlScript.cs` ("update this constant in the same commit
as the .sql change"): re-pin hash + sửa số bảng. Đây **không phải** bỏ test cho xanh giả — không sửa thì
0 test chạy.

**A-0.3 — Cảnh báo cách đo:** lần đo đầu em trỏ `BaseOutputPath` ra `%TEMP%` và thấy
`AccountEmailConfirmationPersistenceTests.Migration_exists_and_is_idempotent_and_additive` FAIL + 478
integration test FAIL. **Cả hai là lỗi do cách đo, không phải lỗi code**: nhiều test đi ngược lên từ thư mục
binary để tìm repo root, nên binary nằm ngoài repo là chúng chết ngay. Đo lại bằng `.tmp-build/` (trong repo,
đã gitignore) thì UnitTests 1024/1024 xanh và integration chỉ còn vướng A-0.1/A-0.2. Ghi lại đây để lần sau
không ai kết luận nhầm "HEAD đang hỏng".

---

## PHẦN A — Luồng tạo đoàn khách khi đã đăng nhập

### A-1. Lỗ hổng registrant identity (P0)

**File:** `backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandHandler.cs`

| Dòng | Sự thật |
|---|---|
| 72 | `registrantUserId = _currentUser.UserId.Value` — chủ đơn LUÔN là actor đăng nhập |
| 92–97 | Đọc `form.Registrant`, nhưng **chỉ** lấy `form.PrimaryContact.Email` để so; `form.Registrant.Email` **không bao giờ được so với email actor** |
| 99–107 | Chỉ chặn "internal không được làm Primary Contact" |

**Tác động:** một Staff đã đăng nhập có thể gửi `registrant.email = ai-đó@ngoài.com` và backend vẫn tạo đơn,
gắn `registrant_user_id` = Staff đó, đồng thời **lưu snapshot người đăng ký của người khác mà không hề xác minh**.
Nghĩa là `registrant_user_id` và `registrant_email` thuộc hai người khác nhau — đúng điểm `[ ]` số 4 của §4.
Không có OTP, không có error code, không có gì chặn.

**Sửa đề xuất (Slice 1):** so `normalized(form.Registrant.Email)` với `normalized(actor.Email)`; lệch → ném
`REGISTRANT_EMAIL_VERIFICATION_REQUIRED`, không ghi gì.

**Mức:** P0 — đây là lỗ hổng chính mà PHẦN A tồn tại để đóng.

### A-2. Không có luồng OTP cho "tạo hộ người khác" (P0)

- Endpoint `/api/v2/visit-requests/initiate` + `/verify` (`VisitRequestsController.cs:172,188`) đã làm **đúng
  tất cả** những gì §8.2/§8.3 yêu cầu: validate full V2 (dùng chung `VisitRequestFormDataV2Validator`),
  mint OTP theo `submissionId`, **bind snapshot** (`visit_request_pending_forms`), verify → tạo request từ
  snapshot đã bind, idempotent replay, chống fingerprint mismatch, consume OTP trong cùng transaction.
- Cả hai là `[AllowAnonymous]` ⇒ caller đã đăng nhập vẫn gọi được (AllowAnonymous cho phép cả hai).
- **Thiếu:** không có gì bắt actor nội bộ đi qua đường này; frontend authenticated luôn gọi direct-create
  (`useVisitRequestFormV2.ts:404`).
- **Thiếu:** `InitiateVisitRequestV2CommandHandler` **không kiểm tra `Processing`**; `VerifyAndCreate…` gọi
  `CreateV2Async(boundForm, …)` **không truyền initializers** (dòng 212–213) nên processing bị **im lặng bỏ qua**
  thay vì bị **reject** như §5.4/§6 yêu cầu.

**Sửa đề xuất (Slice 2):** tái dùng nguyên hai endpoint đó cho luồng delegated (không đẻ route mới —
`naming`/contract đã có sẵn), thêm reject tường minh khi payload mang direct processing.

### A-3. Frontend: không có nút "Tôi là người đăng ký", không phân biệt email (P1)

**File:** `frontend/pems-react/src/features/visit-request/components/v2/VisitRequestFormV2.tsx`

| Dòng | Sự thật |
|---|---|
| 236–287 | Section "Thông tin người đăng ký" **không có nút** tự điền hồ sơ |
| 290–303 | Nút duy nhất là **"Dùng thông tin người đăng ký"** ở section *Đầu mối liên hệ*, copy `registerInfo → contactPoint` (`syncContactFromRegistrant`, dòng 170–177) |
| 294–302 | Nút đó **không bị disable với actor nội bộ**, trong khi backend `CreateVisitRequestV2CommandHandler.cs:99` **luôn reject** `INTERNAL_REGISTRANT_CANNOT_BE_CONTACT` ⇒ user nhập xong mới ăn lỗi lúc submit (đúng điểm §7) |
| 353–358 | `processing` được truyền vào `CampusVisitCard` chỉ dựa trên `isAuthenticated + creatorRole` — **không** phụ thuộc email người đăng ký |

**File:** `hooks/useVisitRequestFormV2.ts:403–407` — `onSubmit` chỉ rẽ theo `mode`, chưa từng rẽ theo email.

### A-4. Processing controls không bị khoá theo danh tính (P0)

`VisitRequestFormV2.tsx:74` giữ `campusProcessing` state; `getCampusProcessing` (dòng 84–90) chỉ lọc theo
campus đang chọn. Khi user sửa email người đăng ký thành email khác, **state cũ vẫn còn và vẫn được gửi**.
Backend hiện chấp nhận (vì A-1 chưa chặn) ⇒ tạo hộ người khác mà vẫn tự nhận Host.

### A-5. Primary Contact copy cho actor nội bộ (P1)

Xem A-3 dòng 294–302. Backend đã đúng; frontend thiếu chặn sớm + thiếu helper text.

### A-6. Trang xử lý ĐÃ đọc đúng Pure V2 instance-level (không cần viết lại)

**File:** `backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/GetVisitProcessDetailQueryHandler.cs`

| Checklist §9 | Kết quả | Bằng chứng |
|---|---|---|
| Scope theo `visitInstanceId` | ✅ | dòng 41–46 (`VisitInstanceId == … && VisitRequestId == …`) |
| Đọc đúng detail campus hiện tại | ✅ | dòng 182–183 `ResolveCampusFormContentAsync(visit, new[]{ instance.VisitInstanceId })` |
| Không dùng sibling campus | ✅ | chỉ truyền đúng 1 instance id |
| Không lấy campus đầu tiên | ✅ | không có `First()` trên `CampusInstances` cho nội dung |
| Mixed request vẫn 200 | ✅ | nội dung lấy per-instance nên mixed không ảnh hưởng |
| Guest/support đúng instance | ✅ | dòng 193–194 từ chính `d` |
| Operational contact đúng instance | ✅ | qua `IVisitFormReadService` |
| Missing detail → lỗi ổn định | ✅ | dòng 184–185 ném `VISIT_FORM_DETAIL_MISSING` |
| Không fallback V1 | ✅ | không còn snapshot global để fallback |

⇒ **Chỉ bổ sung test + dọn dead code**, không viết lại (đúng §9 câu cuối).

**Dead code tìm được:** `GetVisitProcessDetailQueryHandler.cs:366–375` `MapGuestMember(...)` — private, **không
ai gọi** (đã bị `MapRow` thay). Mức P2.

### A-7. Raw enum / thiếu STT ở màn Xem đơn (P1)

**File:** `frontend/pems-react/src/features/visit-request/components/v2/`

| Vấn đề | Bằng chứng |
|---|---|
| Badge trạng thái fallback ra **raw enum** khi thiếu key i18n | `VisitRequestV2DetailView.tsx:86` `t(\`…status.${data.requestStatus}\`, data.requestStatus)`; `CampusVisitDetailCard.tsx:55` cùng kiểu |
| Bảng người **không có cột STT** | `CampusVisitDetailCard.tsx:126–133` (4 cột: Họ tên/Chức vụ/Đơn vị/Quốc tịch) |
| Bảng người dùng header xám, không theo design system | `CampusVisitDetailCard.tsx:127` `border-b … text-slate-500` |
| Không có mobile card cho danh sách người | chỉ có `overflow-x-auto` (dòng 121) |
| Section không có header xanh + số thứ tự cam | toàn màn dùng `rounded-2xl border border-slate-200` phẳng |
| Danh sách người **ẩn sau nút collapse mặc định đóng** | dòng 30 `useState(false)` |
| `decisionActorRole`/`decisionSource` kỹ thuật | có trong DTO nhưng chưa map sang tiếng Việt |

### A-8. Đối chiếu design system trang Xử lý đơn

Mẫu chuẩn để đồng bộ (`pages/dashboard/visit/VisitProcess.tsx`):

| Thành phần | Class thật |
|---|---|
| Khung section | `bg-white border border-[#004c91]/20 rounded-xl overflow-hidden shadow-sm` (dòng 869) |
| Header section | `bg-[#004c91] px-4 py-2.5 flex items-center justify-between cursor-pointer` (dòng 871) |
| Tiêu đề | `text-sm font-bold text-white … uppercase tracking-tight` (dòng 874) |
| Số thứ tự | `w-5 h-5 rounded-full bg-[#f37021] text-white font-bold text-xs` (dòng 875) |
| Badge "Chỉ đọc" | `bg-white/15 … text-white` + icon `Lock` (dòng 879–881) |
| Label/value 2 cột | `RequestInfoReadOnly.tsx:33–55` (`Field`, label `text-gray-500` w-40, value `font-medium text-gray-800`) |
| Bảng người **đã có STT** | `RequestInfoReadOnly.tsx:70–100` (`MembersTable`, `{i + 1}`) |

⇒ Màn Xem đơn phải mượn đúng bộ này. §17.1 yêu cầu header bảng nền `#004c91` chữ trắng (nâng so với
`bg-gray-50` hiện tại của `MembersTable`).

---

## Tổng hợp ưu tiên

| # | Hạng mục | Mức | Slice |
|---|---|---|---|
| A-0.1/A-0.2 | Sửa contract harness integration (hash + số bảng) | P0 (chặn mọi test) | 0 |
| A-1 | Direct-create không kiểm email người đăng ký | P0 | 1 |
| A-2 | Bắt buộc OTP khi tạo hộ + reject processing giả mạo | P0 | 2 |
| A-4 | Processing controls không khoá theo danh tính | P0 | 4 |
| A-3 | Nút "Tôi là người đăng ký" + rẽ nhánh OTP | P1 | 3 |
| A-5 | Chặn sớm Primary Contact với actor nội bộ | P1 | 4 |
| A-6 | Bổ sung test Pure V2 + xoá `MapGuestMember` | P1/P2 | 5 |
| A-7 | Đồng bộ UI Xem đơn (section, STT, badge, mobile) | P1 | 6–7 |
