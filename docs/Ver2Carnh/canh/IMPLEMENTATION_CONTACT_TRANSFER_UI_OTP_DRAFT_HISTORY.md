# PEMS — Báo cáo triển khai: lịch sử nghiệp vụ, quyền đầu mối, UI chuyển giao, bản nháp qua OTP

> Nguồn yêu cầu: `PEMS_IMPLEMENTATION_PROMPT_CONTACT_TRANSFER_UI_OTP_DRAFT_HISTORY_TOAST.md`
> (mục 1–4 + PHẦN A + PHẦN B). Báo cáo theo đúng khung §25 của prompt.

---

## 1. Branch và HEAD

| | |
|---|---|
| Branch | `Canh-Iter1` |
| HEAD trước | `8850b87e` |
| HEAD sau | `cbc309a3` |
| Commit mới | `8f61b0f8`, `a83e338b`, `cbc309a3` |
| Đã push | **Chưa** |

Ghi chú về lịch sử git: đầu phiên, script `fix_commits_3.ps1` đã gỡ trailer `Co-Authored-By` khỏi 7 commit
trước đó nhưng regex thiếu cờ multiline nên **ép mỗi commit message thành 1 dòng** (toàn bộ body chui vào
subject). 7 commit đã được dựng lại từ object gốc — giữ nguyên cây file, giữ nguyên tác giả/ngày, chỉ bỏ
đúng dòng trailer — và nằm ở nhánh **`msg-repaired`**. Xem §13 để biết lệnh áp dụng.

---

## 2. Files changed

### Backend

| File | Thay đổi |
|---|---|
| `PEMS.Application/.../VisitAmendments/GetVisitRequestHistoryQueryHandler.cs` | Suy event code từ `source_type` (2 hàm map) + phát `REQUEST_CANCELLED` từ dòng request |
| `PEMS.Application/.../VisitAmendments/VisitAmendmentCommandContracts.cs` | +7 event code |
| `PEMS.Application/.../VisitFormRead/VisitFormReadService.cs` | `BuildContactActionsAsync` + gate write-flag |
| `PEMS.Domain/Constants/VisitFormActions.cs` | +5 action code đầu mối |

### Frontend

| File | Thay đổi |
|---|---|
| `features/visit-request/components/OtpVerificationModal.tsx` | `stopPropagation` — chặn submit lọt ra form ngoài |
| `features/visit-request/components/ContactIdentityActions.tsx` | Viết lại: gate theo action code + form 2 cột |
| `features/visit-request/components/VisitHistoryTimeline.tsx` | +7 event code đã biết cách diễn đạt |
| `features/visit-request/components/v2/VisitRequestFormV2.tsx` | Banner "tiếp tục xác minh" |
| `features/visit-request/components/v2/VisitRequestV2DetailView.tsx` | Bỏ `isManager`, truyền `allowedActions` |
| `features/visit-request/hooks/useVisitRequestFormV2.ts` | Vòng đời `submissionId` + OTP context + lưu nháp |
| `features/visit-request/utils/visitRequestV2DraftStorage.ts` | Draft mang `submissionId`/`otp`; token OTP ở sessionStorage; `visitDraftNamespace` |
| `features/visit-request/utils/visitV2Actions.ts` | +5 action code |
| `pages/visit/VisitRequestV2Page.tsx`, `pages/dashboard/visit/VisitRequestManagement.tsx` | Thống nhất namespace nháp |
| `shared/utils/toast.ts` | `showInfoToast` |
| `shared/i18n/locales/{vi,en}/visitRequestV2.json` | Câu cho 7 event + 2 khoá chuyển giao + 5 khoá nháp/OTP |

### Tests

`tests/PEMS.IntegrationTests/VisitRequests/VisitRequestHistoryV2Tests.cs` (+5),
`PerCampusFormV2ReadTests.cs` (+6),
`__tests__/visitRequestV2OtpDraft.test.tsx` (mới, 15),
`__tests__/ContactIdentityActions.test.tsx` (viết lại, 14),
`__tests__/VisitHistoryTimeline.test.tsx` (+4),
`__tests__/VisitRequestV2DraftUx.test.tsx` (+1),
`__tests__/VisitRequestV2DetailView.test.tsx` (sửa 2 theo hợp đồng mới),
`tests-realstack/otp-draft-resilience.realstack.spec.ts` (mới, 4 journey).

---

## 3. Mục 1 — Lịch sử thay đổi: 3 sự kiện còn thiếu

**Chẩn đoán khác với giả định ban đầu.** Báo cáo phiên trước nói "backend chưa có nguồn dữ liệu".
Kiểm tra lại thì **dữ liệu đã có sẵn**:

- `VisitSafeEditService.cs:172,189` ghi revision với `SourceType = SAFE_EDIT`;
- `VisitRequestV2EditService.ApplyResubmitAsync` (dòng 602, 617) ghi `RESUBMIT`;
- `visit_requests.cancelled_by / cancelled_at / cancellation_reason` đã tồn tại.

Vấn đề nằm ở **bên đọc**: handler suy event code từ **số hiệu revision**
(`formRevision <= 1 ? created : revised`) và **không đọc dòng request** lần nào. Nên sửa nhanh, gửi lại đơn
và một lần sửa thường đều ra cùng một câu; còn hủy đơn thì timeline im lặng hoàn toàn.

Ánh xạ mới, theo `source_type`, ở cả hai cấp:

```
CREATE            → REQUEST_CREATED             / INSTANCE_CONTENT_CREATED
SAFE_EDIT         → REQUEST_SAFE_EDIT_APPLIED   / INSTANCE_SAFE_EDIT_APPLIED
RESUBMIT          → REQUEST_RESUBMITTED         / INSTANCE_CONTENT_RESUBMITTED
AMENDMENT_APPLIED →                               INSTANCE_AMENDMENT_APPLIED
```

cộng `REQUEST_CANCELLED` đọc thẳng từ dòng request. Dòng cũ chưa có `source_type` vẫn rơi về cách đọc
theo số revision, nên dữ liệu cũ không vỡ.

Câu hiển thị đúng như ví dụ trong prompt:

> Kim Min Jae đã cập nhật nhanh thông tin của đơn.
> Kim Min Jae đã gửi lại đơn sau khi bị từ chối.
> Kim Min Jae đã hủy đơn đăng ký. — Lý do: Đoàn thay đổi lịch bay

Hai điều giữ nguyên có chủ ý:

- **`reason` vẫn `null` cho entry revision.** Bên ghi để *correlation id* (GUID 32 ký tự) vào cột đó;
  timeline in `reason` ngay dưới câu, nên đưa ra là in GUID cho khách xem.
- **Phạm vi không đổi.** Entry cấp request (kể cả việc hủy và lý do hủy) chỉ tới người đăng ký / đầu mối
  và HO. Staff Leader vẫn chỉ thấy cơ sở của mình — có test ghim đúng điểm này.

**Hạn chế đã biết:** `source_type` là `ENUM('CREATE','SAFE_EDIT','AMENDMENT_APPLIED','MIGRATION','RESUBMIT')`,
và luồng **sửa đơn PENDING đầy đủ** (`VisitRequestV2EditService.ApplyEditAsync`) cũng ghi `SAFE_EDIT`. Vì vậy
nó hiển thị là "cập nhật nhanh". Tách hai luồng cần thêm giá trị ENUM = migration, nằm ngoài phạm vi lần này.

---

## 4. Mục 2 — Quyền thao tác đầu mối

`viewer.allowedActions` nay mang 5 mã: `RESEND_CONTACT_CLAIM`, `REPLACE_PENDING_CONTACT`,
`INITIATE_CONTACT_TRANSFER`, `RESEND_CONTACT_TRANSFER`, `CANCEL_CONTACT_TRANSFER`.

Trước đây frontend tự quyết định từ `viewer.relation` + trạng thái đầu mối. Relation không trả lời được
những câu hỏi mà handler thật sự hỏi, nên panel mời user bấm những nút backend sẽ từ chối:

| Tình huống | Backend trả | Trước đây UI |
|---|---|---|
| Gửi lại quá 5 lần | `CLAIM_RESEND_LIMIT` / `TRANSFER_RESEND_LIMIT` | vẫn hiện nút |
| Còn <24h tới giờ thăm | business rule reject | vẫn hiện nút chuyển giao |
| Đã có cơ sở DURING/AFTER/CLOSED | business rule reject | vẫn hiện |
| Đơn đã hủy | business rule reject | vẫn hiện |
| Đang có transfer chờ | `ALREADY_PENDING` | vẫn hiện nút tạo mới |
| Transfer đã hết hạn | `TRANSFER_EXPIRED` | vẫn hiện nút gửi lại |

Điều kiện mỗi mã **sao đúng guard của handler tương ứng** (`RegistrantClaimGuard`, `TransferGuards`):
cùng actor test (role VISITOR, là registrant hoặc đầu mối ACTIVE hiện tại), cùng cửa sổ lifecycle, cùng
trần gửi lại, cùng luật một-thay-đổi-đang-chờ.

**`CANCEL_CONTACT_TRANSFER` cố ý KHÔNG bị chặn bởi cửa sổ 24h**, vì
`CancelVisitContactTransferCommandHandler` không gọi `EnsureTransferLifecycleOpen`. Bỏ nút này sẽ khiến
một lời mời đang chờ mắc kẹt vĩnh viễn khi quá cửa sổ.

**Không mở rộng quyền nào.** Truy vấn pending-change bị bỏ qua hoàn toàn với người không thể chạm tới
workflow, nên HO / Staff Leader / Host không tốn query và không nhận mã nào. Mọi handler vẫn tự
re-authorize; các mã này chỉ quyết định UI được phép mời cái gì.

Panel nay **không render gì cả** khi không có mã nào — trước đây một manager ngoài cửa sổ vẫn thấy tiêu đề
và một đoạn mô tả mà bên dưới không có hành động nào.

---

## 5. PHẦN A — UI chuyển giao đầu mối

| Yêu cầu | Kết quả |
|---|---|
| Mặc định đóng | Giữ nguyên — chỉ mô tả trạng thái + nút |
| Desktop 2 cột | `grid-cols-1 md:grid-cols-2`: Họ tên ∥ Đơn vị · Điện thoại ∥ Email |
| Lý do full-width | `md:col-span-2`, `AutoGrowTextarea` 2 dòng, `maxLength` 500 = giới hạn backend |
| Mobile 1 cột | `grid-cols-1` mặc định, không tràn ngang |
| Bề rộng form | `max-w-4xl` trong Section 2 |
| Chiều cao input đồng nhất | `h-10` + focus state theo design system |
| Nút căn phải cuối form | `sm:flex-row sm:justify-end`; cam `#f37021` cho gửi, secondary cho hủy |
| Đang gửi | disable cả hai + spinner + "Đang gửi lời mời…" |
| Không double-submit | `if (busy) return` trong `run()` + nút disabled |
| Hủy | đóng form, xoá state tạm **của riêng form này**, không đụng bản nháp đơn, không đổi đầu mối |

Ba lỗi sửa kèm:

1. **Email trùng đầu mối hiện tại** bị chặn ngay tại field (so sánh trim + lowercase bằng helper dùng
   chung), thay vì đi một vòng để nhận `EMAIL_UNCHANGED`.
2. **Không thể double-submit** — trước đây click lần hai khi request đang bay sẽ gửi lời mời lần nữa.
3. **Lỗi load trạng thái transfer** vẫn là inline + retry (không biến thành "không có transfer") — giữ từ
   phiên trước.

Toast: dùng `shared/utils/toast`, viewport top-right duy nhất của `App.tsx`. Không mount `<Toaster>` nào mới.

---

## 6. PHẦN B — Bảo toàn bản nháp qua OTP

### 6.1 Rà soát hiện trạng (§7) — kèm file/dòng

| Câu hỏi | Trả lời trước khi sửa |
|---|---|
| Form data còn trong React state? | Có — RHF không bị reset ở nhánh lỗi nào |
| Đóng modal có unmount form? | Modal OTP nằm trong form (`VisitRequestFormV2.tsx:599`); nhưng đóng **modal tạo đơn** thì unmount cả form (`VisitRequestV2Modal.tsx:70`) |
| Draft bị clear khi initiate? | Không — nhưng cũng **không được lưu chủ động**, chỉ có autosave debounce 700ms (`useVisitRequestFormV2.ts:281-292`) |
| Draft bị clear khi verify lỗi? | Không |
| submissionId có bị tạo mới mỗi lần thử lại? | **Có — lỗi.** `onSubmit` luôn `crypto.randomUUID()` (dòng 439); `cancelOtp` (558) và các catch (407, 449) set về `null` |
| Reload khôi phục form? | Có, nếu autosave đã kịp chạy (TTL 30 phút) |
| Khôi phục challenge còn hạn sau reload? | **Không** — `sessionToken` chỉ nằm trong React state |
| Resend có giữ submissionId? | Có |

### 6.2 Lỗi lớn nhất phát hiện khi chạy E2E thật

Real-stack journey A cho ra chuỗi request:

```
POST /v2/visit-requests/verify    400   ← mã user vừa nhập
POST /v2/visit-requests/initiate  200   ← một challenge thứ hai, không ai yêu cầu
```

`OtpVerificationModal` render qua `createPortal`. Portal rời khỏi **DOM tree** nhưng **giữ nguyên React
component tree**, nên sự kiện submit tổng hợp vẫn nổi bọt lên `<form>` đang render modal — chính là form
đăng ký. `handleSubmit` có `preventDefault` (chặn hành vi mặc định của trình duyệt) nhưng **không có
`stopPropagation`** (chặn bubble của React).

Hệ quả với người dùng: mỗi lần bấm "Xác nhận", đơn được gửi lại và một mã OTP mới được cấp — mã đang cầm
trên tay hết hiệu lực, đồng thời phản hồi initiate xoá luôn dòng báo "mã không đúng" mà họ đang đọc. Vì vậy
gõ sai một chữ số trông y như *không có gì xảy ra*. Bug này nấp sau happy path: mã đúng thường hoàn tất
trước khi initiate thứ hai kịp về.

Sửa: thêm `e.stopPropagation()`. Journey A nay **đếm số lần gọi initiate** và bắt buộc bằng 1.

### 6.3 Vòng đời submissionId (§10)

Một **ý định gửi** duy nhất, dù mất bao nhiêu lần thử:

```
draft (localStorage) ──> initiate ──> resend ──> verify ──> network retry
        └── cùng một submissionId suốt chặng ──┘
```

- Sinh một lần, lưu vào draft; `hydrateDraft` nạp lại.
- **Không** bị xoá khi: OTP sai, đóng modal, hết hạn, resend, reload, API lỗi tạm thời.
- Chỉ đổi khi: user chủ động xoá nháp, đơn đã tạo thành công, hoặc backend trả `IDEMPOTENCY_KEY_REUSED`
  (van an toàn cho trường hợp create đã commit nhưng xoá nháp thất bại — nếu không user sẽ kẹt vĩnh viễn).

Backend hỗ trợ sẵn: `InitiateVisitRequestV2CommandHandler.BindPendingSnapshotAsync` ghi rõ *"Re-initiate of
the same intent before verify: refresh the bound snapshot + expiry"*.

### 6.4 Lưu nháp ở những thời điểm nào (§8)

Debounce khi gõ (giữ nguyên) **cộng** force-save tại: trước initiate, trước authenticated create, khi
initiate lỗi, khi verify lỗi, khi đóng modal OTP, khi resend. Lý do phải force: autosave 700ms chưa chắc đã
chạy khi user điền field cuối rồi bấm gửi — mà đó đúng là bản nháp đáng giữ nhất.

### 6.5 Draft chứa gì, không chứa gì (§9, §18)

| Lưu ở đâu | Nội dung |
|---|---|
| `localStorage` (`pems_visit_registration_draft_percampus[::u{id}]`) | `draftSchemaVersion`, `savedAt`, `expiresAt`, toàn bộ form, `submissionId`, `otp` = { targetEmail, maskedEmail, expiresAt, resendAfterSeconds, savedAt } |
| `sessionStorage` (`pems_visit_registration_otp_challenge[::u{id}]`) | `{ submissionId, sessionToken }` |
| **Không lưu ở đâu cả** | mã OTP, hash OTP, confirmation token, access/refresh token, secret |

Token xác minh **cố ý không vào localStorage** (đúng ràng buộc §2): localStorage dùng chung mọi tab và sống
qua cả lần đóng trình duyệt, trong khi challenge chỉ có ý nghĩa với tab đã khởi tạo nó. Token được lưu
**kèm submissionId** nên không thể replay sang ý định gửi khác. Đây là thứ cho phép "tiếp tục xác minh" sau
reload mà **không gửi mã mới**.

TTL: giữ nguyên chính sách sẵn có 30 phút — không tự đổi.

### 6.6 Namespace (§18) — sửa một lỗi lệch

`VisitRequestManagement.tsx` dùng `u{userId}`, `VisitRequestV2Page.tsx` dùng `user.email`. Cùng một người
có **hai bản nháp khác nhau** tuỳ lối vào: bắt đầu ở modal dashboard, quay lại bằng route độc lập thì công
sức trông như mất. Cả hai nay dùng `visitDraftNamespace(user.userId)` → `u{id}`; điều này cũng lấy địa chỉ
email ra khỏi khoá localStorage.

Khách công khai: namespace `undefined` → khoá riêng, không đè và không bị đè bởi draft của tài khoản nào.

### 6.7 Đổi email người đăng ký (§15)

Challenge chứng minh **một** hộp thư. Backend bind token theo email + purpose + submissionId và trả
`SESSION_INVALID` nếu lệch, nên client phải **quên** challenge cũ thay vì mời "tiếp tục" bằng mã không thể
xác minh được gì. Nội dung form giữ nguyên. So sánh email dùng trim + lowercase, nên đổi hoa/thường hay
thêm khoảng trắng **không** làm mất challenge.

### 6.8 Đóng modal, khôi phục, xoá nháp (§11, §14, §17)

- Đóng modal: không clear form, không clear draft, không clear submissionId. Hiện toast
  *"Đơn của bạn đã được lưu tạm. Bạn có thể tiếp tục xác minh email sau."*
- Sau đó (hoặc sau reload + khôi phục nháp) hiện banner **"Đơn đang chờ xác minh email"** với hai nút
  *Tiếp tục xác minh* / *Bỏ mã xác minh*. Không bao giờ tự gửi mã mới; không tự submit sau khi restore.
- Nếu tab hiện tại không còn token (tab khác, hoặc đã đóng trình duyệt): báo mã cũ không dùng được,
  form vẫn nguyên, bấm gửi lại là có mã mới.
- Xoá nháp (prompt khôi phục / "Xoá bản nháp" khi đóng modal tạo đơn) xoá **cả** submissionId và
  challenge — nếu sót lại thì form kế tiếp sẽ dính vào ý định gửi đã bỏ.

---

## 7. Security & privacy review

- Không có route, permission hay quan hệ nào được mở rộng. Năm mã action chỉ **báo** cái mà handler vốn đã
  cho phép; handler vẫn tự kiểm tra.
- Mã OTP không được ghi vào bất kỳ storage nào; test có assert chuỗi thô của draft.
- Token challenge ở sessionStorage (phạm vi tab), kèm submissionId, xoá khi verify thành công / xoá nháp /
  đổi email.
- Draft chứa PII nên tách theo tài khoản; không vào URL, không vào analytics, không log payload.
- Timeline vẫn metadata-only: không snapshot JSON, không token/IP/UA, email danh tính chỉ ở dạng masked.
- Lý do hủy cấp request chỉ tới manager/HO — có test ghim Staff Leader không nhận được.

---

## 8. Tests added

| Nơi | Số | Nội dung |
|---|---|---|
| `VisitRequestHistoryV2Tests` | 5 | sửa nhanh (2 cấp), gửi lại (1 request + N cơ sở), hủy đơn (ai/lúc nào/lý do), leader không thấy lý do hủy, revision đầu đọc là "đã gửi đơn"; correlation id không lọt vào `reason` |
| `PerCampusFormV2ReadTests` | 6 | ma trận 5 action theo trạng thái + trần gửi lại + cửa sổ 24h + đơn đã hủy + HO/leader/host không nhận mã nào |
| `visitRequestV2OtpDraft.test.tsx` | 15 | lưu nháp trước initiate; OTP sai/hết hạn/mất mạng không mất nháp; submissionId ổn định qua fail→resend→re-submit; đóng modal; resume; reload; đổi email; không lưu OTP thô; chỉ thành công mới clear; namespace |
| `ContactIdentityActions.test.tsx` | 14 | render theo action code, 2 cột desktop / 1 cột mobile, lý do full-width, form đóng mặc định, hủy, double-submit, email trùng inline, toast |
| `VisitHistoryTimeline.test.tsx` | 4 | 3 sự kiện mới + "đã gửi đơn"; không lộ enum |
| `VisitRequestV2DraftUx.test.tsx` | 1 | banner tiếp tục xác minh + bỏ mã |
| `otp-draft-resilience.realstack.spec.ts` | 4 | journey A/B/C/D trên stack thật |

---

## 9. Kết quả gate

Chạy tại HEAD `cbc309a3`:

| Gate | Lệnh | Kết quả |
|---|---|---|
| Backend build | `dotnet build PEMS.slnx -p:BaseOutputPath=…/.tmp-build/` | **0 error**, 193 warning |
| Architecture | `dotnet test tests/PEMS.ArchitectureTests` | **14/14** |
| Unit | `dotnet test tests/PEMS.UnitTests` | **1052/1052** |
| Integration | `dotnet test tests/PEMS.IntegrationTests` | **622/622** (trước 611) |
| FE typecheck | `npm run lint` | **0 error** |
| FE unit | `npx vitest run` | **554/554**, 44 file (trước 526) |
| FE build | `npm run build` | ✅ |
| Real-stack E2E | `npm run test:e2e:realstack` | **24/24** (trước 20) |
| Whitespace | `git diff --check` | sạch |

`BaseOutputPath` trỏ vào `.tmp-build/` **trong repo** (không phải `$TEMP`): test walk ngược lên tìm repo
root, để ra ngoài repo là 478 test integration fail vì lý do đo đạc chứ không phải lỗi code.

---

## 10. Real-stack evidence

DB dùng một lần `pems_e2e_realstack` dựng từ master SQL, backend .NET thật (Testing, 2 cờ v2 ON), Vite thật,
Chromium thật, OTP đọc từ FileSink của backend. Không mock network. `pems_db` / `pems_test` / `pems_pr3_test`
không bị đụng tới (orchestrator từ chối các tên này).

- **Journey A** — gõ sai mã → modal báo lỗi và ở lại → đóng modal → form còn nguyên → banner tiếp tục →
  xác minh xong. **Đếm được đúng 1 lần initiate** (đây là test bắt được bug §6.2).
- **Journey B** — gửi OTP → **reload cả trang** → khôi phục nháp → tiếp tục xác minh bằng đúng mã cũ →
  tạo đơn thật, hiện mã `VR…`.
- **Journey C** — gửi lại mã: mã cũ bị từ chối, mã mới xác minh được, chỉ 1 đơn được tạo.
- **Journey D** — gửi OTP cho email A → đổi sang email B → banner biến mất → mã của A **không** xác minh
  được form của B → mã của B thì được. Đây là ràng buộc phía **server**, UI không giả lập được.

Lưu ý môi trường: `mysql` CLI không nằm trong PATH của máy này; chạy bằng
`MYSQL_BIN="C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" npm run test:e2e:realstack`.

---

## 11. Ảnh hưởng cơ sở dữ liệu

**Không có.** Không thêm/sửa/xoá bảng, cột, index, trigger nào. Không cần migration. Toàn bộ dữ liệu lịch sử
mới đọc từ cột đã tồn tại.

Hai guard của DB do fixture đời đầu vi phạm, ghi lại vì **database đúng**:

- `TRANSFER identity change requires old_user_id (the current owner)`
- `cancellation_reason is required when request/delegation is cancelled`

---

## 12. Known limitations

1. **`source_type` là ENUM 5 giá trị** và luồng sửa đơn PENDING đầy đủ dùng chung `SAFE_EDIT` với sửa nhanh,
   nên cả hai hiển thị là "cập nhật nhanh". Tách cần migration ENUM.
2. **Sửa nhanh chỉ nói tới cấp cơ sở / thông tin chung**, chưa liệt kê tên field đã đổi. Field diff nằm ở
   `audit_log_changes` dưới dạng JSON; đưa ra timeline sẽ vi phạm yêu cầu "không hiện JSON" nếu không dịch
   từng field sang nhãn nghiệp vụ — đó là một việc riêng.
3. **Khôi phục challenge sau reload chỉ trong cùng tab.** Đóng hẳn trình duyệt thì mất token (theo đúng
   ràng buộc không lưu token vào localStorage); form vẫn còn, bấm gửi lại là có mã mới với **cùng**
   submissionId.
4. **Journey C không chờ OTP hết hạn thật** (5 phút) — nó chứng minh cơ chế thay thế: resend làm mã cũ mất
   hiệu lực. Muốn test hết-hạn-thật phải rút ngắn TTL qua config test.
5. **Journey E (chuyển giao đầu mối trên stack thật) chưa viết.** Cần fixture: tài khoản Visitor sở hữu một
   đơn có đầu mối ACTIVE. UI chuyển giao đang được phủ bởi 14 test frontend và ma trận quyền bởi 6 test
   integration.
6. **`VisitProcess.tsx` và `MinutesCard.tsx` vẫn có toast riêng** — cả hai đã ở top-right nên không tái hiện
   triệu chứng sai góc; `VisitProcess` còn truyền `pushToast` xuống 2 section con nên là refactor riêng.

---

## 13. Việc chưa hoàn thành

1. **Áp lịch sử commit đã sửa.** 7 commit gốc đang ở nhánh `msg-repaired` (cùng nội dung cây file, message
   đầy đủ, không có trailer). Em không có quyền dời con trỏ nhánh. Lệnh:

   ```
   git rebase --onto msg-repaired 8850b87e Canh-Iter1
   git branch -D msg-repaired
   ```

   Lệnh này giữ nguyên 3 commit mới của lần này và đặt chúng lên trên 7 commit đã sửa message.
   Kiểm tra sau khi chạy: `git log --oneline` phải ra 10 commit trên `bb8a3b85`, và
   `git diff cbc309a3 HEAD` phải rỗng.

2. **Push + PR về `Dev`** — chưa push gì.

3. **Journey E real-stack** cho luồng chuyển giao đầu mối (xem §12.5).

4. **Quyết định về wording sửa nhanh vs sửa đơn pending** (§12.1) — cần chốt có làm migration ENUM không.
