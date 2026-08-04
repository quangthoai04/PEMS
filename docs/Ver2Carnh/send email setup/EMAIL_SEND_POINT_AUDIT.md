# PEMS — Audit các luồng gửi email + xử lý 3 sự cố

> Ngày: 2026-08-04 · Nhánh: `Canh-Iter1` · Baseline: `cbcf05b5`
> Nguồn yêu cầu: `PEMS_EMAIL_SENDING_INCIDENTS_AND_FULL_AUDIT_IMPLEMENTATION_PROMPT.md`

---

## 1. Ba sự cố — nguyên nhân gốc

### 1.1 "Không thể kết nối Google Drive. Vui lòng thử lại."

**Nguyên nhân:** `GoogleDriveStorageService.GetAccessTokenAsync` gộp **4 nguyên nhân khác nhau** vào 1 câu + 1 mã lỗi.

| Tình huống thật | Mã trả về (trước) | Câu hiển thị (trước) | Đúng chưa |
|---|---|---|---|
| Không có mạng tới Google | `GOOGLE_DRIVE_AUTH_FAILED` | "…thử lại" | ❌ sai nhóm (mạng, không phải auth) |
| Google từ chối client secret | `GOOGLE_DRIVE_AUTH_FAILED` | "…thử lại" | ❌ thử lại vô ích, cần sửa cấu hình |
| Google trả 5xx / 429 | `GOOGLE_DRIVE_AUTH_FAILED` | "…thử lại" | ❌ sai nhóm |
| Token response thiếu `access_token` | **`UPLOAD_AVATAR_FAILED`** | "…thử lại" | ❌ mã của tính năng avatar, trên đường đi của MỌI upload Drive kể cả PDF báo cáo |

Hệ quả: Host bấm "Gửi cập nhật chuẩn bị" nhận đúng 1 câu bảo "thử lại" cho cả 4 ca — trong đó 2 ca thử lại không bao giờ khỏi.

**Bằng chứng:** `GoogleDriveStorageService.cs` (trước sửa) dòng 541, 558, 567 — cùng 1 chuỗi, 2 mã.

### 1.2 "Temporarily unable to issue another verification code."

**Nguyên nhân:** backend đã tính đủ `errorCode` + `retryAfterSeconds` + `retryAt` + header `Retry-After` (policy `OtpChallengePolicy.EvaluateIssue` phân biệt 4 hạn mức), **nhưng cả 4 mã dùng chung 1 câu tiếng Anh**:

- `OtpService.cs:390` (đường cấp mã)
- `VerifyAndCreateVisitRequestV2CommandHandler.cs:314-317` (đường xác minh)

Frontend hiển thị thẳng `response.data.message` → câu tiếng Anh trên giao diện tiếng Việt, không nói hạn mức nào, không nói khi nào thử lại được.

**Lỗi phụ tìm thêm (nặng hơn câu chữ):** nhánh `catch` của **resend** trong `useVisitRequestFormV2.ts` chỉ đọc `humanVerificationRequired`, **ném bỏ `retryAfterSeconds`/`retryAt`**. Nên màn hình cooldown không bao giờ hiện khi resend bị chặn, và nút "Gửi lại" bật lại sau 60 giây đếm cục bộ dù server vừa nói phải chờ 1 tiếng — mỗi lần bấm lại là thêm 1 lần 429 (với hạn mức tuyệt đối còn đẩy mốc reset ra xa hơn).

### 1.3 "EmailDraft ({id}) was not found."

**Nguyên nhân:** 404 là *đúng* — nháp thật sự không tồn tại. Lỗi nằm ở **cách frontend xử lý 404**.

`EmailComposeModal` khi `getDraft` hỏng:
- Giữ nguyên composer mở, còn nguyên `initialBodyHtml` (nội dung backend vừa sinh) trong ô soạn → **nhìn y hệt một nháp đã tải thành công**.
- `handlePreview` không chặn → xem trước được.
- `handleSend` chạy nhánh `draftIdRef.current == null` → **`createDraft` một nháp hoàn toàn mới rồi gửi**.

Nghĩa là: một `draftId` chết vẫn dẫn tới **một email thật được gửi đi**, dựa trên một nháp không ai yêu cầu tạo, và không có chỗ nào trên màn hình nói rằng có gì đó sai.

**Rủi ro cấu trúc tìm thêm:** `PrepareVisitSetupProgressEmailDraftCommandHandler` ghi nháp bằng **2 lần `SaveChangesAsync` không transaction** → hàng `email_drafts` commit riêng; nếu bước ghi recipients/attachment hỏng thì còn lại một nháp đã commit nhưng không người nhận, không báo cáo — và `FindReusableAsync` có thể trả lại chính nháp đó cho lần sau.

---

## 2. Phát hiện nghiêm trọng nhất (ngoài 3 sự cố) — mất attachment im lặng

`EmailDraftDispatcher.DispatchAsync`:

```
LoadAlignedAsync → trả null cho file không đọc được bytes
Pair()           → bỏ qua slot null
TryClaimAsync    → nháp chuyển SENT
SendAsync        → gửi thiếu file
kết quả          → Success = true, "Đã gửi email tới N người nhận."
```

Không error, không warning, không dòng dữ liệu nào ghi lại việc thiếu file. Phía gửi **không có cách nào** biết. Đây đúng là kịch bản §10.1 của prompt, và nó áp cho **mọi** đường gửi nháp: soạn thủ công, trả lời, và setup-progress.

> Đường setup-progress có probe riêng cho Báo cáo Lịch trình trước khi gửi (đã có từ trước), nhưng đó chỉ che 1 file bắt buộc của 1 luồng — các file khác Host tự đính kèm vẫn rơi im lặng.

**Quyết định (§10.2/10.3): chọn phương án A — fail-closed cho MỌI attachment.**

Lý do không phân loại mandatory/optional: một người đính kèm file vì nội dung email nói về file đó. "Optional" không phải thuộc tính đọc được từ hàng `email_draft_attachments`, nên mọi cách phân loại đều là đoán. Từ chối gửi, giữ nguyên nháp, nêu tên file — rẻ hơn nhiều so với một email đã gửi mà không lấy lại được.

Chặn chạy **trước** `TryClaimAsync` nên nháp vẫn ở `DRAFT`, người gửi sửa file rồi gửi lại.

---

## 3. Inventory send point

### 3.1 Đường dẫn kỹ thuật (3 nhóm)

| Nhóm | Service | Attachment | Rủi ro mất file |
|---|---|---|---|
| **System template** | `ISystemEmailDispatcher` | Không (trừ report) | Không |
| **Report/Invoice** | `IReportEmailSender` → `SystemEmailDispatcher.DeliverAsync` | **Có** — bytes PDF **trong bộ nhớ**, không đọc lại từ storage | **Không** — không có bước đọc lại nào để hỏng |
| **Manual/Draft** | `IEmailDraftDispatcher` → `IManualEmailSender` | **Có** — đọc bytes từ `files` qua storage | **Có** ← đã fix ở §2 |

> Đã grep toàn backend: `Attachments = new…` chỉ xuất hiện **1 chỗ** (`ReportEmailSender.cs:114`). Mọi attachment khác của hệ thống đều đi qua `EmailDraftDispatcher`. Nghĩa là fix ở §2 phủ **toàn bộ** bề mặt rủi ro mất-file, không sót đường nào.

### 3.2 Bảng audit theo send point

| # | Send point | Template code | Trigger | Nguồn người nhận | Attachment | Draft? | Idempotency | Rủi ro còn lại |
|---|---|---|---|---|---|---|---|---|
| 1 | Account email confirmation | `ACCOUNT_EMAIL_CONFIRMATION` | Tạo/đổi vai trò tài khoản | User vừa tạo | — | — | token 1 lần | — |
| 2 | Resend confirmation | `ACCOUNT_EMAIL_CONFIRMATION` | Nút gửi lại | Tài khoản đích | — | — | token mới | — |
| 3 | Password reset OTP | `AUTH_PASSWORD_RESET_OTP` | Quên mật khẩu | Email nhập vào | — | — | quota/giờ | — |
| 4 | Visit request OTP | `VISIT_REQUEST_OTP` | Gửi đơn UC-17 | Email đăng ký | — | — | quota + fingerprint | ✅ đã sửa thông điệp |
| 5 | Contact claim / transfer | `VISIT_CONTACT_CLAIM` / `_TRANSFER` | Tạo đơn | Đầu mối liên hệ | — | — | token | — |
| 6 | Participant invitation | `VISIT_PARTICIPANT_INVITATION` (+ Student/DeptLeader) | Host mời | `visit_participants` theo instance | — | — | `email_action_tokens` | — |
| 7 | Dept staff assignment | `VISIT_DEPARTMENT_STAFF_ASSIGNMENT` | Leader phân công | Nhân sự được giao | — | — | — | — |
| 8 | Logistics (4 template) | `LOGISTICS_*` | Yêu cầu / phân công / đề xuất / nhắc | Phòng ban nhận | — | — | — | — |
| 9 | Reminder (background) | `VISIT_REMINDER_HOST` / `_PARTICIPANTS` | Hosted service | Host + participant ACCEPTED | — | — | dedupe theo instance | — |
| 10 | **Setup-progress** | `VISIT_SETUP_PROGRESS_UPDATE` | Host bấm gửi | Backend suy từ instance | **Có (bắt buộc)** | ✅ | claim atomic | ✅ đã sửa (transaction + fail-closed) |
| 11 | Report / Invoice (4 template) | `REPORT_*` | Người dùng bấm gửi | Theo scope báo cáo | **Có (bắt buộc)** | — | `Idempotency-Key` bắt buộc | — |
| 12 | Manual compose | (không template) | Soạn tay | Người gửi nhập | Có (qua draft) | ✅ | claim atomic | ✅ đã sửa fail-closed |
| 13 | Reply | (không template) | Trả lời | `ReplyRecipientPlanner` | Có (qua draft) | ✅ | claim atomic | ✅ đã sửa fail-closed |
| 14 | Draft reopen / autosave / discard | — | Composer | — | — | ✅ | — | ✅ đã sửa 404/403/409 |

**Forward:** không tồn tại trong codebase (chỉ có reply). Không phải nợ — là phạm vi.

---

## 4. Các mục audit đã kiểm tra và KHÔNG cần sửa

Ghi lại để lần sau khỏi kiểm tra lại:

| Hạng mục (§11–§14) | Kết luận | Cơ chế đang bảo vệ |
|---|---|---|
| Placeholder `{{...}}` sót sau render | **Đã an toàn** | `EmailTemplateRenderer.AssertNoUnresolvedPlaceholder` chặn cả subject lẫn body, có `EMAIL_TEMPLATE_UNRESOLVED_PLACEHOLDER` |
| Trusted block sai capability | **Đã an toàn** | `TemplateRequiredBlockNotInBody`, `TemplateSystemBlockNotAllowed`, `ContactBlockNotAllowedWhenHidden` |
| Preview khác nội dung gửi | **Đã an toàn** | Cùng `IEmailTemplateRenderer`; setup-progress lưu body tại thời điểm preview và gửi lại chính body đó |
| OTP/secret lọt vào subject hoặc lịch sử | **Đã an toàn** | `TemplateSensitiveInSubject`, `SubjectSecretLeak`, `HistorySecretLeak` |
| TO bắt buộc, trùng email chéo nhóm, vượt hạn mức | **Đã an toàn** | `EmailDraftWriter.ValidateRecipients` + `EmailRecipientValidator` (chạy lại lúc gửi) |
| Recipient type lạ | **Đã an toàn (fail-closed)** | `envelopeFromDraft` trả `unknown` → composer chặn preview/send/autosave, không ghi đè nháp |
| Gửi trùng do double click | **Đã an toàn** | `TryClaimAsync`: `UPDATE … WHERE status='DRAFT'`, chỉ 1 request khớp |
| Attachment lệch tên/nội dung | **Đã an toàn** | `LoadAlignedAsync` giữ đúng vị trí (có test `EmailAttachmentLoaderAlignmentTests`) |
| Provider reject → trạng thái DB | **Đã an toàn** | `ManualEmailSender` ghi row trước, cập nhật `SENT`/`FAILED`/`QUEUED` theo kết quả thật; không bao giờ ghi `DELIVERED` |
| Autosave ghi đè lúc hydrate | **Đã an toàn** | `hydratingRef` set đồng bộ, chặn debounce trước khi render lại |

---

## 5. Thay đổi đã thực hiện

### Backend

| File | Thay đổi |
|---|---|
| `Common/Storage/GoogleDriveErrorCodes.cs` *(mới)* | Tập mã lỗi kết nối/ghi Drive, tách khỏi `StorageErrorCodes` (lỗi đọc 1 file) |
| `FileStorage/GoogleDrive/GoogleDriveStorageService.cs` | Phân loại mạng / token hết hạn / auth / 5xx / body hỏng thành 4 mã riêng; bỏ `UPLOAD_AVATAR_FAILED`; thêm `ClassifyUploadFailure` dùng chung 2 đường upload; log `error` field thay vì cả body |
| `Emails/Common/EmailDraftDispatcher.cs` | `AssertEveryAttachmentReadable` — chặn gửi **trước** khi claim nháp |
| `Emails/Common/EmailErrorCodes.cs` | `EMAIL_ATTACHMENT_UNREADABLE`, `EMAIL_DRAFT_NOT_FOUND`, `EMAIL_DRAFT_NOT_EDITABLE` |
| `Emails/Queries/GetEmailDraft/GetEmailDraftQueryHandler.cs` | 1 lần đọc cho 3 quyết định; thêm **409** cho nháp đã SENT/DISCARDED |
| `Delegations/SetupProgressEmail/PrepareVisitSetupProgressEmailDraftCommandHandler.cs` | Nháp + recipients + attachment trong **1 transaction** |
| `Common/Security/OtpRateLimitMessages.cs` *(mới)* | 1 nguồn wording tiếng Việt cho 4 hạn mức + cooldown, kèm thời gian |
| `Identity/OtpService.cs`, `VerifyAndCreateVisitRequestV2CommandHandler.cs` | Dùng wording chung thay câu tiếng Anh |

### Frontend

| File | Thay đổi |
|---|---|
| `emails/components/EmailComposeModal.tsx` | Màn hình fail-closed thay hẳn form khi không tải được nháp; phân biệt 404/403/409; xoá body sinh sẵn; chặn preview/send/autosave; nút **[Đóng] [Tạo bản nháp mới]** |
| `pages/dashboard/visit/VisitProcess.tsx` | Bỏ bản `apiErrorMessage` cục bộ (bỏ qua `errorCode`) → dùng helper chung; `onRecreateDraft` gọi prepare với `reuseExistingDraft=false`; prepare hỏng thì quay lại bước chọn ngôn ngữ để thấy lỗi + thử lại |
| `visit-request/hooks/useVisitRequestFormV2.ts` | `otpRateLimitMessage` dựng câu từ `errorCode` + retry metadata; **resend/recover giờ giữ lại `retryAfterSeconds`/`retryAt`** |
| `visit-request/components/OtpVerificationModal.tsx` | Nút "Gửi lại" disable theo **server** (`retryCountdown`/`rateLimitCountdown`), không chỉ theo timer 60s cục bộ |
| `shared/i18n/locales/{vi,en}/errors.json` | 11 mã Drive + 3 mã draft/attachment |
| `shared/i18n/locales/{vi,en}/visitRequestV2.json` | `otpFlow.rateLimit.*` |

---

## 6. Kết quả gate

| Gate | Kết quả |
|---|---|
| Backend build | ✅ 0 error (162 warning có sẵn từ trước) |
| Unit tests | ✅ **2211/2212** — 1 fail có sẵn từ trước, không liên quan (`GetMyVisitPhotoFoldersQueryHandlerTests`, `PhotoFaceTag` chưa map trong DbContext test) |
| Integration — Emails + Storage | ✅ **676/676** |
| Integration — VisitRequests (OTP) | ✅ **375/375** |
| Integration — SetupProgressEmail | ✅ **21/21** |
| Frontend `npm run lint` (tsc) | ✅ 0 error |
| Frontend `npm run build` | ✅ built |
| Frontend `npm run test:unit` | ✅ **1549/1552** — 3 fail có sẵn từ trước, không liên quan (xem §7) |

**Test mới:** 13 (Drive classification) + 5 (dispatcher attachment guard) + 9 (OTP wording) + 9 (composer draft-load failure) = **36 test mới, tất cả xanh**.

**An toàn:** DB dùng `pems_pr3_test` (không phải `pems_db`) · `Smtp.Enabled=false` · không gọi Drive thật (stub `IHttpClientFactory`) · không đổi schema · WIP 41 file + 9 stash giữ nguyên · chưa push.

---

## 7. Nợ còn lại

| # | Việc | Vì sao chưa làm |
|---|---|---|
| 1 | 3 frontend test đỏ sẵn: `logisticsDescription` (2), `operationalContactQuickFill` (1) | Đã chứng minh có sẵn từ trước: file test **và** component dưới test đều **byte-identical với HEAD**. Ngoài phạm vi prompt này. |
| 2 | 1 unit test đỏ sẵn: `GetMyVisitPhotoFoldersQueryHandlerTests` | `PhotoFaceTag` chưa đăng ký trong DbContext test của visit-photos. Ngoài phạm vi. |
| 3 | Runtime smoke thủ công 13 ca (§16) | **Chưa chạy.** Cần đăng nhập bằng tài khoản HO/test trên môi trường chạy thật. Đã thay bằng 1072 integration test chạy end-to-end qua HTTP với outbound tắt. Cách tự xác minh: bật app, `Smtp__Enabled=false`, chạy đúng 13 luồng ở §16 và đối chiếu `sent_emails` + file sink. |
| 4 | Chưa tái hiện sự cố Drive trên credential thật | Không có credential Drive trong `appsettings.Testing.json`, và prompt cấm dùng credential/thư mục thật. Nguyên nhân gốc xác định bằng đọc code + 13 test mô phỏng đủ 4 nhóm lỗi qua stub HTTP. |
| 5 | `SendEmailCommandHandler` / `ReplytoEmailCommandHandler` truyền `Attachments: []` cứng | Hai endpoint này không hỗ trợ đính kèm; composer thực tế luôn đi qua draft. Không phải lỗi, nhưng nếu sau này cho phép đính kèm thì phải đi qua `EmailDraftDispatcher` để hưởng guard §2 — **đừng** thêm đường đọc file thứ hai. |
| 6 | TOCTOU hẹp: probe báo cáo OK → dispatcher đọc bytes | Cửa sổ vài mili-giây. Nếu file biến mất đúng lúc đó, guard §2 vẫn bắt được (fail-closed), chỉ là thông điệp chung hơn. Chấp nhận được. |
| 7 | Hạn mức OTP khi 2 mốc chồng nhau | `EvaluateIssue` chọn mã theo `retryAt` muộn nhất, nên user có thể thấy `RESEND_TOO_SOON` trước rồi `ABSOLUTE_RATE_LIMITED` sau. Mỗi thời điểm đều nói đúng sự thật; không sửa. |

---

## 8. Trước / sau

**Google Drive**
```
trước: 4 nguyên nhân → 1 câu "Không thể kết nối Google Drive. Vui lòng thử lại." (2 mã, 1 sai tính năng)
sau:   CONFIG_MISSING / TOKEN_EXPIRED / AUTH_FAILED / UNAVAILABLE / FOLDER_NOT_FOUND_OR_NO_PERMISSION / UPLOAD_FAILED
       — mỗi mã 1 câu tiếng Việt riêng, chỉ UNAVAILABLE mới nói "thử lại"
```

**OTP**
```
trước: "Temporarily unable to issue another verification code."  (nút gửi lại bật lại sau 60s)
sau:   "Bạn đã yêu cầu quá nhiều mã trong một giờ. Có thể thử lại lúc 15:30."
       (nút gửi lại khoá đến đúng mốc server nói)
```

**Draft 404**
```
trước: composer mở như bình thường → xem trước được → bấm Gửi → TẠO NHÁP MỚI và gửi email thật
sau:   màn hình "Không mở được email nháp" thay hẳn form; không autosave, không preview, không send;
       [Đóng] hoặc [Tạo bản nháp mới] (gọi prepare với reuseExistingDraft=false)
```

**Attachment**
```
trước: file không đọc được → bỏ im lặng → nháp SENT → "Đã gửi email tới N người nhận."
sau:   EMAIL_ATTACHMENT_UNREADABLE, nêu tên file, nháp giữ nguyên DRAFT, không chạm provider
```
