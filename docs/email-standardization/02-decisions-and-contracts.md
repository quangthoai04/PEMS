# 02 — Quyết định và hợp đồng kỹ thuật (Giai đoạn 2)

> Nguồn yêu cầu: `PEMS_EMAIL_TEMPLATE_CC_BCC_IMPLEMENTATION_PLAN.md` Mục 11.
> Dựa trên audit `01-email-caller-template-audit.md` (HEAD `06c73b94`).
> Đây là hợp đồng **ràng buộc** cho Giai đoạn 3 → 8. Code phải khớp tài liệu này; nếu lệch phải sửa tài liệu trước.

---

## 0. Decision log — quyết định của owner

| ID | Quyết định | Ngày | Lý do |
|---|---|---|---|
| DL-01 | Làm trên nhánh `Canh-Iter1`, **không** commit/push/PR | 2026-07-26 | Kế hoạch Mục 6.2 + Mục 28; nhánh `Dev` chỉ là dự kiến, xác minh cho thấy công việc đang ở `Canh-Iter1` |
| DL-02 | Chốt catalog **26 template**; report gộp còn **4 mã** | 2026-07-26 | C-27/C-28 cùng nội dung (khác `scopeLabel`); C-26/C-29 subject giống hệt từng ký tự |
| DL-03 | 9 mã legacy: **bỏ khỏi fresh seed**, chuyển `INACTIVE` ở DB đang tồn tại | 2026-07-26 | `sent_emails` seed đang FK tới `email_template_id` 1..16 → xoá sẽ gãy FK/mất lịch sử |
| DL-04 | Soạn **đủ VI và EN** cho cả 26 template | 2026-07-26 | Thoả Mục 16.3 "không active template thiếu VI/EN" và cho phép test render EN |
| DL-05 | Tái dùng `IHtmlSanitizerService.SanitizeEmailHtml` sẵn có, **không** viết sanitizer mới | 2026-07-26 | Đã hỗ trợ `cid:` + `data-content-id` cho ảnh inline; thêm sanitizer thứ hai sẽ tạo hai hành vi lệch nhau |
| DL-06 | Lỗi ổn định dùng `BusinessRuleException(message, errorCode)` sẵn có, **không** tạo hierarchy exception mới | 2026-07-26 | Khớp convention hiện hữu (`AuthErrorCodes`, `OtpErrorCodes`); middleware đã map `ErrorCode` ra response |
| DL-07 | Tách Giai đoạn 7 thành **7a** (seed canonical) chạy **trước** Giai đoạn 4, và **7b** (script đồng bộ) chạy sau | 2026-07-26 | Renderer không còn fallback (D-01), nên caller chuyển sang template trước khi seed tồn tại sẽ ném `EMAIL_TEMPLATE_NOT_FOUND` ngay batch đầu. Chính kế hoạch §19.1 đã nêu phụ thuộc này. **Không phải bỏ gate**: G7 chỉ đạt sau 7b |
| DL-08 | File-sink ghi phong bì thành **`to[] / cc[] / bcc[]`** (mảng object `{email, displayName}`) thay cho một chuỗi `to` | 2026-07-26 | E2E cần khẳng định được "BCC nhận thư nhưng không lộ" — điều đó không diễn đạt được bằng một địa chỉ đơn. Kéo theo: 6 spec Playwright real-stack đổi sang helper chung `tests-realstack/sinkRecord.ts` |
| DL-09 | Hai hàm legacy `SendPasswordResetAsync` / `SendVisitRequestOtpAsync` **gắn sẵn `TemplateCode`** ngay từ Giai đoạn 3 | 2026-07-26 | Giữ `kind` trong file-sink không đổi giá trị hai bên bờ migration Batch 2/3, nên harness E2E không phải sửa hai lần |
| DL-10 | Thêm `ErrorCode` vào `ValidationException` và `NotFoundException`, middleware trả thêm field `errorCode` | 2026-07-26 | 6/14 mã lỗi email map sang 400/404. Client cần mã máy để phản ứng (tô đúng chip recipient sai), không thể parse prose. Backward-compatible: constructor cũ giữ nguyên, field mới null với mọi caller cũ |

### Mâu thuẫn tài liệu đã ghi nhận

| # | Mâu thuẫn | Xử lý |
|---|---|---|
| M-01 | Kế hoạch Mục 10.6 liệt kê 27 mã template "baseline"; thực tế 20/27 không tồn tại | Theo thứ tự ưu tiên Mục 3 của kế hoạch: **production flow tại HEAD thắng**. Catalog lấy từ audit, không lấy từ Mục 10.6 |
| M-02 | Kế hoạch Mục 10.7 giả định 5 template report tồn tại | Thực tế 0. Ghi rõ ở `01-…-audit.md` Mục 3 |
| M-03 | Kế hoạch Mục 12.2 liệt kê `SmtpEmailSender.cs`, `EmailTemplateRenderer.cs` như điểm code ứng viên | Cả hai là **class rỗng**. Không "sửa" chúng — sẽ xoá và tạo type mới đúng chỗ |
| M-04 | Kế hoạch Mục 10.2 nhắc MailKit | Codebase dùng `System.Net.Mail.SmtpClient`, **không** MailKit | Xem C-01 dưới đây |
| M-05 | `EmailTemplate.Purpose` có XML comment ghi *"SQL: purpose ENUM('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION')"* | DDL thật là `purpose VARCHAR(100) NOT NULL`. Comment sai là gốc rễ của D-13 (validator lấy nhầm allowlist OTP). Sửa comment cùng lúc với validator ở Giai đoạn 4 |

### Ghi chú vận hành (rút ra khi thực thi)

| # | Ghi chú |
|---|---|
| O-01 | `Set-Content -Encoding UTF8` của PowerShell 5.1 **thêm BOM UTF-8**. Ghi đè canonical SQL bằng lệnh đó làm đổi byte đầu file (và do đó đổi hash) ngoài ý muốn. Phải ghi bằng `[System.IO.File]::WriteAllBytes` hoặc gỡ BOM sau khi ghi, rồi mới tính hash. Đã kiểm chứng bằng `git diff` chỉ còn đúng các hunk email. |
| O-02 | `mysql.exe` in cảnh báo password ra **stderr**; PowerShell 5.1 biến stderr của native exe thành `NativeCommandError` làm script dừng. Dùng `--defaults-extra-file` thay vì `--password` trên dòng lệnh. |
| O-03 | `email_templates.purpose` trong MySQL: kiểm tra placeholder bằng `REGEXP '\\{\\{...'` cho **false positive** (khớp toàn bộ 26 hàng). Không dùng regex SQL làm bằng chứng — bằng chứng là contract test C# `Every_placeholder_is_lower_camel_case`. |
| O-04 | Cách diễn đạt "diff scope" phải nêu **bảng bị tác động**, không phải "tên bảng xuất hiện trên dòng diff". Đếm theo tên bảng trên dòng `+/-` cho ra `email_templates` vì các hunk của `sent_emails` chỉ là dòng value-tuple, không lặp lại tên bảng — nhưng `sent_emails` **có** bị tác động. Xem Mục "Phạm vi thay đổi seed 7a" dưới đây. |
| O-05 | Chạy test với `BaseOutputPath` **ngoài repo** (thư mục temp) làm `CanonicalSqlScript.FindRepositoryRoot()` không tìm được gốc repo ⇒ 599/728 integration test đỏ với thông điệp "not reachable". Đây là guard fail-closed hoạt động đúng, KHÔNG phải hồi quy. Khi dev server đang giữ khoá `backend/PEMS.Api/bin` (MSB3021/MSB3027), đặt output vào thư mục **trong repo** đã gitignore (`bin/claude-run/`) thay vì temp. |
| O-06 | Renderer dùng `WebUtility.HtmlEncode` cho **giá trị biến** ⇒ chữ tiếng Việt biến thành numeric entity (`&#7877;`). Mail client hiển thị đúng, nhưng `sent_emails.body_snapshot` khó đọc bằng mắt và dài hơn. Chưa đổi (đây là contract C-07 đã chốt ở G2); đề xuất theo dõi, nếu đổi thì đổi ở **một chỗ duy nhất** là renderer, sang bộ escape 5 ký tự đã dùng trước đây, kèm cập nhật test. |

### Phạm vi thay đổi seed của 7a (phát biểu chính xác)

Hai bảng bị tác động, không phải một:

| Bảng | Thay đổi | Tính chất |
|---|---|---|
| `email_templates` | 2 khối `INSERT` cũ (16 hàng, `email_template_id` cứng 1..16) → **1** khối canonical 26 hàng không ghi id; xoá 16 câu `UPDATE … WHERE email_template_id = N`; xoá khối patch `CASE template_code` | thay toàn bộ nội dung bảng trong fresh seed |
| `sent_emails` | **25 giá trị `email_template_id`** chuyển thành `NULL`, trải trên 4 khối `INSERT` (6 + 3 + 8 + 8) | chỉ tháo cột liên kết; `sent_email_id`, `subject`, `body_snapshot`, `related_type`, `related_id`, `provider_thread_id`, `provider_message_id`, `status`, `sent_by`, `sent_at` giữ nguyên từng byte |

Việc tháo liên kết là hợp lệ vì cả 25 hàng đều trỏ tới 6 template đã rời catalog (`ACCOUNT_CREATED_INTERNAL`, `VISIT_REQUEST_APPROVED`, `VISIT_REQUEST_REJECTED`, `VISIT_CANCELLED`, `HOST_ASSIGNMENT`, `LOGISTICS_REQUEST`) — không hàng nào trỏ tới 7 mã còn sống, và không hàng nào bị chuyển sang template khác.

Không bảng nào ngoài hai bảng trên bị chạm: Gallery, FAQ, News, calendar, Visit Form v2 và `email_action_tokens` giữ nguyên (15 token seed còn đủ, khẳng định bằng test).

**SHA-256 canonical SQL sau 7a (đầy đủ):**

```
f3bbb7ce5892772ad6aab03efdf84238022cfff7c4803c3823eac00dc17e621d
```

Giá trị trước 7a: `5ba7daac9667e1b06eee4e6c28c02b120472b4ad37e90732328966f87c8b24ce`.

**SHA-256 canonical SQL sau 7a-fix — giá trị đang có hiệu lực (đầy đủ):**

```
51e178bb5e56fc927fd896e2a87ed8015043a2ca4904b4e1d9df581b2caae8a1
```

7a-fix chỉ sửa 4 hàng `email_templates` (2 thông báo địa chỉ cũ bỏ hết biến; 2 thông báo Staff Leader thêm `{{reason}}`). Batch 1 **không** sửa canonical SQL — hash trên vẫn đúng sau khi chuyển đủ 12 điểm gửi.

---

## Ghi nhận Batch 1 (Giai đoạn 4 — nhóm Account)

Các quyết định phát sinh khi chuyển 12 điểm gửi Account sang `ISystemEmailDispatcher`:

| # | Quyết định | Lý do |
|---|---|---|
| B1-01 | Giữ nguyên **mandatory/best-effort của từng caller**, không áp một hành vi chung. Lỗi *gửi* (SMTP) không bao giờ ném — dispatcher trả `FAILED`/`SKIPPED`. Lỗi *cấu hình template* (thiếu row / `INACTIVE` / sai placeholder) ném stable error code, và chỉ bị nuốt ở đúng những chỗ code cũ đã có `try/catch` bao quanh (C-04, C-06a/C-06b, C-07, C-09a, C-09b, C-01). | Audit ghi cả 12 điểm là best-effort: nghiệp vụ đã commit xong, một email hỏng không được biến giao dịch thành công thành request lỗi. |
| B1-02 | `UpdateBasicAccountInfo` giữ hợp đồng phản hồi cũ **SENT / PARTIAL / FAILED** (frontend đang khai đúng 4 giá trị `NOT_REQUIRED \| SENT \| FAILED \| PARTIAL`). Một lần gửi `SKIPPED` vẫn tính là "ok" đúng như trước. | Không mở rộng giá trị enum ở Batch 1 — đó là thay đổi hợp đồng API, thuộc Giai đoạn 6. Tính trung thực của lịch sử vẫn giữ: `sent_emails` của lần `SKIPPED` ở lại `QUEUED`, không bao giờ `SENT`. |
| B1-03 | Nội dung một số email **ngắn lại** so với bản hard-code, theo đúng catalog đã duyệt: `ACCOUNT_STAFF_LEADER_ASSIGNED` bỏ khối "Thông tin tài khoản" (email đăng nhập / phòng ban) và câu hướng dẫn SSO; `ACCOUNT_EMAIL_CHANGED_NEW_NOTICE` bỏ snapshot vai trò/cơ sở/phòng ban; `ACCOUNT_ROLE_CHANGED` bỏ dòng phòng ban + mã số sinh viên. | Biến của template là hợp đồng đã duyệt ở G2/G3. Muốn thêm lại thì thêm biến vào **cả** registry và seed, không phải nhét chuỗi vào handler. |
| B1-04 | `ACCOUNT_ROLE_CHANGED` nay nêu **cả vai trò cũ và mới** (`oldRoleName`, `newRoleName`); handler truyền `oldRoleCode`/`oldSubRole` đã bắt trước khi ghi. | Bản cũ chỉ in vai trò mới — "vai trò của bạn đã đổi" mà không nói đổi từ đâu thì người nhận không kiểm chứng được. |
| B1-05 | `effectiveDate` của 2 email Staff Leader định dạng `dd/MM/yyyy` theo giờ VN (`now` của chính giao dịch thay thế). | Đúng tên biến (ngày, không phải mốc giờ) và đúng quy ước ngày của hệ thống. |
| B1-06 | Bỏ `.Include(u => u.PrimaryCampus)` + `.Include(u => u.Department)` trong `UpdateBasicAccountInfo`. | Hai navigation đó chỉ được nạp để dựng email snapshot; email mới không dùng nữa. Giữ lại thì comment ngay trên câu query thành sai. |
| B1-07 | Xoá `AccountConfirmationEmail.cs` sau khi quét toàn repo còn **0 tham chiếu code** (chỉ còn nhắc trong tài liệu audit). | Đây là nơi cuối cùng còn giữ subject + body hard-code của nhóm Account. |

---

## Ghi nhận Batch 2 (Giai đoạn 4 — nhóm Auth)

Phạm vi: đúng **1 điểm gửi** — C-11 `ForgotPasswordCommandHandler` → `AUTH_PASSWORD_RESET_OTP`. `LoginViaCredentials` / `LoginViaSso` không gửi email.

| # | Quyết định | Lý do |
|---|---|---|
| B2-01 | **Template `HasSensitiveAction = true` KHÔNG lưu `body_snapshot`** (ghi `NULL`). Hàng `sent_emails` vẫn có `email_template_id`, subject, recipient TO, status, `sent_at`, `error_message`. Áp dụng cho 12 mã: `ACCOUNT_EMAIL_CONFIRMATION`, `AUTH_PASSWORD_RESET_OTP`, `VISIT_REQUEST_OTP`, `VISIT_CONTACT_CLAIM/TRANSFER`, 4 mã VISIT_PARTICIPANT, 3 mã LOGISTICS mang link một-lần. | Đường cũ `SendPasswordResetAsync` **không ghi lịch sử**; chuyển sang dispatcher sẽ ghi, và body chính là bí mật. `ViewEmailQueryHandler` đã **cố ý bỏ filter sender/recipient** (có comment tại chỗ) còn `EmailsController` mở cho mọi Staff/StaffLeader/Department/DeptLead/HO ⇒ mã đặt lại còn hiệu lực đọc được bởi người không phải người nhận. `IOtpService` cũng ghi rõ: "Neither may be logged or persisted raw." |
| B2-02 | Subject vẫn lưu. | Không mã nào trong catalog đặt bí mật vào subject; seed contract test giữ ranh giới đó. |
| B2-03 | `IOtpService` lộ thêm `CodeMinutes` (đọc `Otp:CodeMinutes`), handler truyền vào `{{expireMinutes}}`. | Bản hard-code ghi cứng "15 phút"; đổi setting là email nói sai. Cùng khuôn với `IAccountEmailConfirmationService.ExpiryHours` ở Batch 1. |
| B2-04 | Xoá hẳn `IEmailService.SendPasswordResetAsync` + 2 implementation + 5 stub trong test fake (0 caller còn lại). | Giữ một hàm tự soạn email mã đặt lại là mở đường gửi thứ hai mà màn Cấu hình Email Template không sửa được. |
| B2-05 | Giữ nguyên chống account-enumeration: toàn bộ khối vẫn trong `try/catch`, mọi nhánh trả cùng một `MessageResponse`; lỗi gửi bị nuốt và log không kèm mã. | Một khác biệt nhỏ trong phản hồi cũng đủ thành oracle dò email tồn tại. Có test so sánh 3 nhánh (tồn tại / không tồn tại / SSO-only) và nhánh gửi lỗi. |
| B2-06 | Transaction boundary C-11: `OtpService.CreateAsync` **tự `SaveChangesAsync`** trước khi gửi; handler không mở transaction nào. Lúc gọi dispatcher không còn entity nghiệp vụ nào ở `Added/Modified/Deleted` chờ ghi. Gửi lỗi ⇒ token vẫn còn dùng được. | Đúng nguyên tắc "lưu nghiệp vụ xong mới gửi" đã chốt ở Batch 1, nhưng được kiểm lại riêng cho caller này (kết luận Batch 1 chỉ áp cho 12 điểm Account). |
| B2-07 | **Sửa lại bằng chứng Batch 1**: test `The_history_row_stores_what_was_actually_sent` đổi thành `The_history_row_keeps_the_metadata_but_not_the_one_time_link`. `ACCOUNT_EMAIL_CONFIRMATION` là template nhạy cảm nên từ nay `body_snapshot` = NULL, không còn lưu link xác nhận. | Link xác nhận là token một-lần: lưu lại thì bất kỳ Staff nào cũng đọc được và kích hoạt hộ tài khoản người khác. Cùng loại rò rỉ với OTP, nên áp cùng một luật. Template ACCOUNT không nhạy cảm (7 mã còn lại) vẫn lưu body như cũ — cặp đối chứng nằm ở `SystemEmailDispatcherBoundaryTests` (dùng `ACCOUNT_ACTIVATED`). |

**Đánh đổi đã ghi nhận:** với 12 mã nhạy cảm, `body_snapshot` không còn là bản sao nội dung đã gửi. Với email nhạy cảm, nội dung lịch sử được **chủ động coi là không khả dụng và không thể dựng lại chính xác**: raw OTP/token không được giữ, và template hiện tại cũng có thể đã thay đổi sau hot-edit. Không bổ sung cơ chế lưu raw variables hay immutable body để phục vụ dựng lại.

| # | Quyết định (security closure B2-S08) | Lý do |
|---|---|---|
| B2-S08 | **Cấm bí mật xuất hiện trong subject**, chặn ở renderer trước khi thay giá trị: subject chứa placeholder của biến bí mật hoặc của trusted block ⇒ ném stable code `EMAIL_TEMPLATE_SENSITIVE_IN_SUBJECT`, ghi 0 `sent_emails`, 0 `sent_email_recipients`, không gọi sender. Kiểm **cả `subject_vi` và `subject_en`** ở mọi lần render. | `body_snapshot` đã NULL nhưng subject vẫn lưu. Hot-edit (hoặc màn Cấu hình Email Template ở Giai đoạn 6) có thể kéo `{{otpCode}}` / `{{actionBlock}}` vào subject và đưa bí mật trở lại `sent_emails`, màn lịch sử, backup, export. Guard đặt ở runtime vì template sửa được trực tiếp trong DB, không restart — kiểm seed hoặc catalog là không đủ. |
| B2-S09 | Phân loại biến tập trung tại `SensitiveEmailVariables`: `Names` (biến là credential — hiện chỉ `otpCode`) + `KnownNonSensitive` (39 biến còn lại). Trusted block bị cấm trong subject **theo định nghĩa** — đó là đường duy nhất markup/link một-lần vào được nội dung. | Không dựa vào danh sách 12 template viết tay. Không suy từ tên biến: `requestCode` là mã tra cứu người ta đọc qua điện thoại, `otpCode` là bí mật — không quy tắc đặt tên nào phân biệt được hai thứ đó. |
| B2-S10 | 4 invariant mới bắt lỗi khi thêm mới: union `Names ∪ KnownNonSensitive` phải phủ **toàn bộ** biến của registry (thêm biến mới ⇒ đỏ cho tới khi phân loại); không có biến phân loại thừa; template khai báo biến credential **bắt buộc** `HasSensitiveAction = true`; `CarriesSecret` và `OmitsBody` không được lệch nhau. | Cơ chế này đã bắt lỗi thật ngay khi viết: `requesterName` chưa phân loại. |

**Không đổi:** email nhạy cảm vẫn gửi đủ nội dung khi template hợp lệ · `body_snapshot` của 12 mã luôn NULL ở mọi trạng thái `QUEUED`/`SENT`/`FAILED` · subject hợp lệ vẫn lưu · không xoá history/recipient/status/metadata · không đổi schema · không sửa canonical SQL hay SHA-256.

---

## Ghi nhận Batch 3 (Giai đoạn 4 — Visit request OTP)

Phạm vi: 3 điểm gửi, cùng template `VISIT_REQUEST_OTP` — C-12 `InitiateVisitRequestV2CommandHandler`, C-13 `ResendVisitRequestOtpCommandHandler`, C-14 `RecoverVisitRequestOtpCommandHandler`.

| # | Quyết định | Lý do |
|---|---|---|
| B3-01 | Gộp đường gửi của 3 caller vào `VisitRequestOtpMail.SendAsync`. | Cả 3 giống hệt nhau về việc phải làm và cách phải hỏng; viết 3 lần là 3 cơ hội để chúng lệch nhau. |
| B3-02 | **Mọi lỗi chặn mã rời hệ thống đều quy về đúng một mã công khai `OTP_SEND_FAILED`** — kể cả lỗi cấu hình (template thiếu / `INACTIVE` / sai biến / subject chứa bí mật). | Người gọi ở đây là người lạ chưa chứng minh sở hữu địa chỉ. Nói "template đang INACTIVE" là mô tả hệ thống cho họ. Đây **không** phải nới lỏng B2-S08: guard vẫn chặn ở renderer, vẫn 0 `.eml`, 0 `sent_emails`; chỉ có thông điệp ra ngoài là được đồng nhất. |
| B3-03 | `SKIPPED` (SMTP tắt ngoài production) **không** phải lỗi — giữ đúng hành vi cũ, vì phản hồi đã nói rõ mã được in ra log ở chế độ DEV. | `EmailService.SendAsync` cũ cũng chỉ ném khi `Failed`. |
| B3-04 | `IOtpService` lộ thêm `VisitRequestCodeMinutes`; email nói **5 phút** lấy từ đúng setting đã cấp cho token, không phải số viết cứng. | Body cũ ghi cứng "5 phút" trong prose — đổi `Otp:VisitRequestCodeMinutes` là email nói sai. |
| B3-05 | Người nhận **không có display name**. | Ở bước này tên trên form mới là khai báo, chưa được xác minh; gắn nó vào header To là khẳng định hộ thư thuộc về người đó. |
| B3-06 | Địa chỉ của resend/recover lấy từ **hàng challenge đã lưu**, không lấy từ body request (giữ nguyên hành vi cũ). | Nếu không, resend thành cách chuyển hướng mã của người khác. |
| B3-07 | Xoá hẳn `IEmailService.SendVisitRequestOtpAsync` + 2 implementation. `IEmailService` **không còn method nào tự soạn nội dung**. | Đây là bản sao cuối cùng của nội dung OTP nằm trong code. |
| B3-08 | 2 bộ test V2 (`PublicInitiateVisitRequestV2Tests`, `AuthenticatedDelegatedOtpV2Tests`) nay chạy qua **dispatcher thật + renderer thật**, fake chỉ đóng vai SMTP. | Mạnh hơn trước: OTP đi đúng đường production và test vẫn đọc được mã từ message sinh ra. Không nới assertion nào. |

**Không đổi:** validation form v2, snapshot pending, expiry/resend/attempt/replay/supersede, quota theo email, luồng verify, và mọi mã lỗi OTP khác.

---

## Ghi nhận Batch 4 (Giai đoạn 4 — Contact claim/transfer)

Phạm vi: 2 điểm gửi trong `VisitContactClaimService` — C-15 → `VISIT_CONTACT_CLAIM`, C-16 → `VISIT_CONTACT_TRANSFER`.

| # | Quyết định | Lý do |
|---|---|---|
| B4-01 | Link một-lần đi qua trusted block mới `EmailComposition.ContactRoleInvitationBlock(url, expiresAt)`, kèm hạn **thật** của token. | Link chỉ được vào nội dung qua đúng một đường; hạn hiển thị lấy từ `claim.ExpiresAt` nên không lệch với hạn thật (72h claim / 24h transfer). |
| B4-02 | Email transfer **nêu tên đầu mối hiện tại** (`currentContactName`). | Bản cũ chỉ viết "thay cho đầu mối hiện tại" — lời mời như vậy không kiểm chứng được và đọc y hệt email lừa đảo. |
| B4-03 | Người nhận transfer **không có display name**; `contactFullName` = chính địa chỉ. | Hệ thống chưa biết tên người được mời — identity change chỉ có địa chỉ. Không bịa tên vào header. |
| B4-04 | Giữ nguyên best-effort: token đã commit trước khi gửi, lỗi gửi chỉ ghi log. | Người tạo đơn resend được; email hỏng không được huỷ claim đã tạo. |

## Quyết định B5-D1 và B5-D2 (đã được duyệt, mở đường cho Batch 5–7)

Sáu điểm gửi C-17..C-22 có hai đặc điểm mà quy tắc migration ban đầu không phủ: nội dung **do Host sửa được** (`EmailOverride`) và **token gắn `sent_emails` bên trong transaction** (`email_action_tokens.sent_email_id` NOT NULL).

| # | Quyết định | Trạng thái |
|---|---|---|
| B5-D1 | `EmailOverride` là **chế độ hợp lệ có tên**: template DB là nội dung mặc định và là nguồn **duy nhất** khi Host không sửa; khi Host sửa thì đó là email người dùng soạn, vẫn qua `EmailRecipientValidator` + recipient policy + guard subject. Không xoá tính năng, không đổi schema, không đổi transaction boundary. | ✅ đã triển khai |
| B5-D2 | Lưu `body_snapshot` **đã gỡ action block** cho template mang bí mật dạng link. | ✅ đã triển khai |

**B5-D2 triển khai bằng cơ chế, không bằng danh sách mã** (đúng tinh thần B2-S09). `SensitiveEmailHistory.PolicyFor` suy ra 3 mức từ chính phân loại của template:

| Điều kiện | Chính sách | Áp cho |
|---|---|---|
| Không nhạy cảm | `Full` — lưu nguyên | 14 template |
| Nhạy cảm **và** khai biến credential (`otpCode`) | `None` — không lưu gì | `AUTH_PASSWORD_RESET_OTP`, `VISIT_REQUEST_OTP` |
| Nhạy cảm, bí mật nằm trong **link** | `ActionBlockStripped` — lưu body đã gỡ khối hành động | 10 template còn lại |

> **Hệ quả mở rộng ngoài 6 template được hỏi:** vì luật suy từ phân loại chứ không từ danh sách, `ACCOUNT_EMAIL_CONFIRMATION`, `VISIT_CONTACT_CLAIM` và `VISIT_CONTACT_TRANSFER` chuyển từ `body_snapshot = NULL` (B2-01) sang **body đã gỡ link**. Đây là mở rộng có chủ ý: cùng tính an toàn (link không tồn tại trong lịch sử) nhưng bản ghi hữu ích hơn, và tránh phải nuôi một danh sách mã viết tay mà B2-S09 đã cấm. Nếu anh muốn giữ NULL cho 3 mã đó, đây là chỗ cần đảo lại.

`EmailComposition.StripActionArtifacts` gỡ trọn vùng `PEMS_ACTION_BLOCK_START..END` — tức mọi khối do `WrapActionBlock` sinh ra, kể cả dòng "Hoặc mở liên kết: …" nằm trong đó. Test khẳng định trực tiếp: link không còn trong snapshot, phần giải thích vẫn còn.

### B5-D1 — hai chế độ nội dung, một pipeline

**Kiểu mới: `SystemEmailContent`** (`Emails/Common/SystemEmailContent.cs`) — hệ đóng, constructor private, đúng 2 nhánh:

| Nhánh | Nghĩa |
|---|---|
| `SystemEmailContent.FromTemplate.Instance` | Mặc định. Nội dung đọc từ `email_templates` mỗi lần gửi. |
| `SystemEmailContent.AuthoredByUser` | Nội dung người có quyền tự soạn (hôm nay: Host sửa lời mời). |

Đặt trên `SystemEmailRequest.Content` và `EmailRenderRequest.Content` (đều mặc định `FromTemplate`). **Không** dùng cặp nullable `SubjectOverride`/`BodyOverride`: với cặp đó, chế độ phải *suy ra* từ tổ hợp null nào đang bật, và "sửa subject nhưng không sửa body" là trạng thái biểu diễn được mà không ai thiết kế.

**Không thể có nội dung authored chưa kiểm.** `AuthoredByUser` chỉ dựng được qua `Create(subject, bodyHtml, IHtmlSanitizerService)` — constructor private, thuộc tính get-only (nên `with` không đổi được nội dung đã kiểm). Tức "đã sanitize chưa?" là câu hỏi của trình biên dịch, không phải của người review.

**Ranh giới — authored đổi được đúng 2 thứ:** subject và body. Không đổi được (theo cấu trúc, không phải theo kiểm tra lúc chạy — kiểu đó không có trường nào để chạm tới): template code/identity · recipient policy · TO/CC/BCC · phân loại nhạy cảm · chính sách snapshot · variables contract · trusted-block contract · action token/URL · sender identity · ngôn ngữ · mandatory/best-effort · attachment policy · authorization/scope.

**Thứ tự pipeline (một renderer, không có renderer thứ hai):**

| # | Bước | Ở đâu |
|---|---|---|
| 1 | Envelope: `EmailRecipientValidator` + `EmailRecipientPolicyEnforcer` | `SystemEmailDispatcher.PrepareAsync` — **trước** mọi ghi |
| 2 | Validate + sanitize nội dung authored | `AuthoredByUser.Create` (bắt buộc để dựng được kiểu) |
| 3 | Resolve template ACTIVE + nội dung theo ngôn ngữ | `EmailTemplateRenderer` bước 1–4 |
| 4 | Guard subject template (cả VI lẫn EN, trên subject thô) | bước 4b |
| 5 | Variables contract (2 chiều) — **áp cho cả 2 chế độ** | bước 5–6 |
| 6 | Chọn chế độ; guard subject authored; gỡ action artifact khỏi body authored | bước 6b |
| 7 | Chèn trusted block do backend dựng | bước 6b (nối sau nội dung) |
| 8 | Thay biến (giá trị luôn HTML-encode khi body là HTML) | bước 7–8 |
| 9 | Làm sạch subject + chặn CR/LF | bước 9 |
| 10 | Guard subject **sau render**: giá trị credential, URL/token một-lần, marker | bước 9b |
| 11 | Không còn placeholder; đúng 1 khối hành động | bước 10–10b |
| 12 | `SensitiveEmailHistory.PolicyFor` + **chứng minh** snapshot sạch link | `PrepareAsync` |
| 13 | Ghi `sent_emails` + `sent_email_recipients` | `PrepareAsync` |
| 14 | Gửi + ghi kết quả thật | `DeliverAsync` |

**Transaction ownership — `PrepareAsync` / `DeliverAsync`.** Dispatcher tách 2 pha để caller giữ quyền transaction: `PrepareAsync` render + ghi lịch sử **chưa gửi**, trả về `PreparedSystemEmail` (có `SentEmailId`, `SentEmailRecipientId`) để caller tạo `email_action_tokens` trỏ vào đúng message trong **cùng** transaction; `DeliverAsync` gửi **sau** commit. `SendAsync` chính là 2 pha gọi liền nhau nên hai đường không thể lệch nhau. Dispatcher **không** tự mở transaction và **không** commit sớm entity nghiệp vụ. `PreparedSystemEmail.Attachments` là `init` để caller nạp byte từ storage *sau* commit (`prepared with { Attachments = … }`) — không giữ transaction mở khi đọc file.

**Mã lỗi mới (ổn định):** `EMAIL_AUTHORED_SUBJECT_REQUIRED` · `EMAIL_AUTHORED_SUBJECT_TOO_LONG` · `EMAIL_AUTHORED_BODY_REQUIRED` · `EMAIL_AUTHORED_BODY_TOO_LONG` · `EMAIL_AUTHORED_ACTION_BLOCK_FORBIDDEN` · `EMAIL_ACTION_BLOCK_MALFORMED` · `EMAIL_SUBJECT_SECRET_LEAK` · `EMAIL_HISTORY_SECRET_LEAK`.

**Fail-closed cho khối hành động.** `StripActionArtifacts` gọi `AssertActionBlockStructure` trước khi gỡ: 0 marker là hợp lệ, đúng 1 cặp START…END theo thứ tự là hợp lệ, mọi hình dạng khác (thiếu đầu/thiếu cuối/đảo/lồng/≥2 khối) → `EMAIL_ACTION_BLOCK_MALFORMED`. Lý do: bước 0 xoá nguyên vùng giữa 2 marker — hình dạng sai nghĩa là xoá nhầm vùng, mà bên cần nó nhất là email history. Người soạn viết marker (dạng thô, kiểm **trước** sanitize vì sanitizer nuốt comment) → `EMAIL_AUTHORED_ACTION_BLOCK_FORBIDDEN`.

**Chứng minh, không giả định.** `AssertSnapshotCarriesNoActionUrl` đối chiếu snapshot sắp ghi với **URL thật** rút từ trusted block (`ExtractActionUrls`, cả dạng đã HTML-encode lẫn dạng thô). Đây là bằng chứng vùng bị gỡ đúng là vùng cần gỡ — điều mà nội dung body tự nó không nói được.

**Guard subject 2 lớp.** Lớp cũ (B2-S08) bắt *placeholder* `{{otpCode}}`/`{{actionBlock}}` trên subject thô, trước khi thay giá trị. Lớp mới bắt *giá trị* đã có mặt như văn bản: giá trị biến credential, URL một-lần, hoặc token trần (đoạn cuối URL ≥12 ký tự). Thông báo lỗi nêu **loại** dữ liệu, không bao giờ nêu giá trị — chính chỗ này bị log và hiển thị.

## Ghi nhận Batch 5 (Giai đoạn 4 — Lời mời thành phần tham gia)

Phạm vi: C-17 `InviteVisitParticipantCommandHandler` → `VISIT_PARTICIPANT_INVITATION` · `VISIT_STUDENT_INVITATION` · `VISIT_DEPARTMENT_LEADER_INVITATION` (vai người được mời quyết định, Host không chọn).

| # | Quyết định | Lý do |
|---|---|---|
| B5-01 | Xoá `ParticipantInvitationEmailBuilder` — nội dung mặc định về hẳn `email_templates`. | Đây là fallback nội dung cuối cùng của nhóm lời mời; còn nó thì sửa template không đổi được email. |
| B5-02 | Handler dùng `PrepareAsync` trong transaction, `DeliverAsync` sau commit. | `email_action_tokens` phải trỏ vào `sent_emails` bằng FK thật; token ghi trước khi message tồn tại thì không thoả FK, mà gửi trước commit thì rollback để lại email không có token. |
| B5-03 | `body_snapshot` theo `ActionBlockStripped` (từ phân loại), thay cho lưu nguyên body kèm link sống như trước. | Bản cũ ghi `BodySnapshot = finalBody` — tức link accept/decline nằm nguyên trong bảng mà API lịch sử phục vụ mọi vai nội bộ. |
| B5-04 | Trạng thái trả về theo kết quả thật: `SENT` / `SKIPPED` / `FAILED`. | Bản cũ báo `SENT` cả khi SMTP tắt (Skipped), vì `SendAsync` không ném lỗi cho trường hợp đó. |
| B5-05 | `RetryCount` không còn tự +1 ở lần gửi đầu. | Lần gửi đầu không phải lần thử lại; đếm từ 1 làm hỏng ý nghĩa cột. |
| B5-06 | Lỗi nạp attachment ghi mã `ATTACHMENT_LOAD_FAILED` + câu an toàn, **không** ghi `ex.Message`. | Thông báo này hiển thị trong lịch sử email; exception của storage có thể chứa đường dẫn hoặc signed URL. |

> **Đính chính một tiền đề:** `email_action_tokens.sent_email_id` trong canonical SQL là **NULL được** (dòng 3282), kèm FK tới `sent_emails`. "Không bao giờ NULL" vì vậy là **bất biến do code giữ**, không phải ràng buộc cột — nên nó được khẳng định bằng test (`ParticipantInvitationLinkageTests`) chứ không dựa vào database.

## Ghi nhận Batch 6 (Giai đoạn 4 — Phân công nhân sự phòng ban)

Phạm vi: C-18 `AssignDepartmentStaffCommandHandler` → `VISIT_DEPARTMENT_STAFF_ASSIGNMENT`.

| # | Quyết định | Lý do |
|---|---|---|
| B6-01 | Đổi mapping từ `VISIT_PARTICIPANT_INVITATION` sang `VISIT_DEPARTMENT_STAFF_ASSIGNMENT`. | Bản cũ ghi `email_template_id` của **template lời mời** cho mọi phân công (dòng 111 cũ) — lịch sử email không phân biệt được "được mời" với "bị phân công", và sửa template phân công không đổi được gì. Registry + seed đã có sẵn mã đúng. |
| B6-02 | Bổ sung `campusName` + `departmentName` vào nội dung. | Template khai 5 biến; 2 biến này handler chưa hề đọc. "Anh được phân công" mà không nói **cơ sở nào** và **ai phân công** thì người nhận không hành động được. |
| B6-03 | Xoá `DefaultContentHtml` — nội dung mặc định về hẳn `email_templates`. | Fallback nội dung cuối cùng của nhóm phân công. |
| B6-04 | `ov.BodyHtml` → `EmailComposition.ResolveEditableHtml(ov)`. | Bản cũ chỉ đọc `BodyHtml`, nên Leader sửa bằng ô plain-text (`BodyText`) bị báo "nội dung trống". Đây là tập cha: caller chỉ gửi `BodyHtml` không đổi hành vi. |
| B6-05 | `body_snapshot` theo `ActionBlockStripped`; bỏ `RetryCount += 1` ở lần gửi đầu; lỗi attachment ghi câu an toàn thay `ex.Message`. | Cùng ba lý do như B5-03/B5-05/B5-06. |
| B6-06 | **Giữ nguyên ranh giới ghi hiện có**: chuỗi `SaveChanges` không có transaction tường minh. | Gói cả chuỗi vào transaction là thay đổi lifecycle của command — quyết định riêng, không tự làm. Thứ tự "ghi message trước, token sau" vẫn đủ giữ `sent_email_id` trỏ vào hàng có thật. |

> **Điểm yếu có sẵn, không tự sửa:** vì không có transaction, nếu tiến trình chết giữa lúc ghi `sent_emails` và lúc ghi token thì còn lại một hàng `sent_emails` không có token, và email chưa hề được gửi — người được phân công không nhận được gì. Đây là hành vi **trước** batch này và được giữ nguyên; ghi ra đây để anh quyết có nâng lên transaction hay không.

C-18 trước batch này **không có test nào**. Nay có 10 unit + 5 integration (MIME thật + DB thật).

## Ghi nhận Batch 7 (Giai đoạn 4 — Hậu cần)

Phạm vi: C-19 `PrepareVisitLogistics` → `LOGISTICS_REQUEST_TO_DEPARTMENT` · C-20 `AssignRequestAssignee` → `LOGISTICS_ASSIGNEE_ASSIGNMENT` · C-21 `ProposeRequestChange` → `LOGISTICS_CHANGE_PROPOSAL_TO_HOST` · C-22 `RemindExpenseReports` → `LOGISTICS_EXPENSE_REPORT_REMINDER`.

| # | Quyết định | Lý do |
|---|---|---|
| B7-01 | C-19: xoá **3 tầng fallback** — `template.SubjectVi ?? "…"`, `template.BodyVi ?? ""`, và cả nhánh `else` dựng `DefaultContentHtml` khi không có template. | Đây là chỗ vi phạm D-01 nặng nhất trong toàn hệ: template hỏng vẫn gửi được nội dung do C# bịa, nên người vận hành sửa template mà không thấy gì đổi. |
| B7-02 | C-19: bỏ `EmailComposition.RenderTemplate` khỏi đường gửi. | Hàm đó mang **bảng fallback riêng** (`"Chưa có thông tin"`, `"Không có ghi chú phối hợp."`, `"Chưa nhập"`…) — một renderer thứ hai với luật riêng, đúng thứ B5-D1 cấm. Giá trị hiển thị cuối nay do caller quyết, cùng chữ như cũ. |
| B7-03 | C-19: sửa bộ biến. Caller cũ truyền 17 khoá (`itemType`, `DelegationName`, `CampusName`, `priority`, `detailUrl`, `visitName`…) nhưng template khai 9; **thiếu hẳn `logisticsItemType` và `coordinationNote`**. | Sai tên là im lặng: `itemType` không khớp `{{logisticsItemType}}` nên dòng "Loại" ra chuỗi thay thế. Nay lệch bộ biến là lỗi fail-closed 2 chiều. |
| B7-04 | C-20: xoá `DefaultContentHtml`; bổ sung `campusName` + `delegationName`. | Caller cũ tra `email_template_id` **chỉ để ghi vào `sent_emails`** rồi dựng toàn bộ nội dung trong C# — lịch sử khai nội dung đến từ template chưa từng được đọc. |
| B7-05 | C-20: `ov.BodyHtml` → `ResolveEditableHtml(ov)`. | Giống B6-04: Leader sửa bằng ô plain-text bị báo "nội dung trống". |
| B7-06 | C-21: `EmailTemplateId = null` → `LOGISTICS_CHANGE_PROPOSAL_TO_HOST`; bổ sung `departmentName`. | Bản cũ ghi lịch sử **không gắn template nào**. Và email chỉ nói "phòng ban xử lý đề xuất" — Host không biết phòng nào, không kiểm chứng được. |
| B7-07 | C-22: gắn template (trước cũng để trống); bổ sung `dueAt`; giữ nguyên "1 người 1 thư", không token. | Nhắc kê khai là template **duy nhất trong nhóm không nhạy cảm** — link cần đăng nhập, không cấp quyền gì, nên `body_snapshot` = `Full`. |
| B7-08 | Cả 4: `body_snapshot` theo phân loại; bỏ `RetryCount += 1` lần gửi đầu; lỗi attachment ghi câu an toàn thay `ex.Message`. | Ba lỗi cùng họ đã sửa ở B5/B6. |

**Thay đổi hành vi cần biết — bỏ tiền tố ưu tiên trong tiêu đề email.** `LogisticsPriorityText.ApplySubjectPrefix` từng chèn `[KHẨN] ` / `[ƯU TIÊN CAO] ` vào **tiêu đề đã render**. Bốn template trong catalog không khai biến ưu tiên nào, nên giữ nó đồng nghĩa tiêu đề không thuần từ template — đúng thứ §VII liệt kê là lỗi ("Subject/body vẫn hard-code"). Đã bỏ khỏi email. **Tín hiệu khẩn vẫn còn nguyên ở thông báo trong hệ thống** (`SubjectPrefix` + `LabelVi` vẫn dùng cho `Title`/`Message` của notification, không đụng tới). Muốn khôi phục trong email thì cần thêm biến vào catalog + seed — tức đổi canonical SQL, nằm ngoài Batch 7.

**Nội dung email đề xuất (C-21) gọn lại.** Body cũ liệt kê số lượng đề xuất, khung giờ đề xuất, mô tả, mức ưu tiên, hạn phản hồi. Template được duyệt chỉ mang `proposalNote` + nút, chi tiết xem sau khi đăng nhập. Đây là thiết kế đã chốt ở 7a; ghi ra để anh biết đây là **giảm thông tin trong email**, không phải sót.

**Ranh giới ghi:** C-19/C-20/C-21 đã có transaction sẵn — giữ nguyên, `PrepareAsync` chạy trong transaction, `DeliverAsync` sau commit. C-22 cũng vậy (một transaction bao cả vòng lặp, gửi sau commit). **Không** thay đổi transaction lifecycle của handler nào.

C-20, C-21, C-22 trước batch này **không có test nào**; C-19 có 6 test không chạm email. Nay: 14 + 10 + 5 + 7 unit và 17 integration.

## Ghi nhận Batch 8 (Giai đoạn 4 — Nhắc lịch tiếp khách)

Phạm vi: C-23 `VisitReminderDispatchHostedService` → `VISIT_REMINDER_HOST` + `VISIT_REMINDER_PARTICIPANTS`.

| # | Quyết định | Lý do |
|---|---|---|
| B8-01 | Xoá **4 tầng fallback**: template chéo (`hostTemplate ?? participantsTemplate` và ngược lại), ngôn ngữ chéo (`SubjectVi ?? SubjectEn`), tiêu đề hard-code, và body rỗng. | Fallback template chéo nghĩa là **người tham gia có thể nhận email dành cho Host**. Không có tầng nào trong đó là hành vi ai đó chọn. |
| B8-02 | Xoá renderer riêng (`Render()` regex private). | Nó **để nguyên placeholder không khớp**. Cộng với B8-03 bên dưới, người nhận thật đọc được đúng chuỗi `{{plannedStart}}`, `{{plannedEnd}}`, `{{actionBlock}}` trong email nhắc lịch. |
| B8-03 | Sửa bộ biến. Job cũ truyền `plannedStartAt`, `DelegationName`, `CampusName`, `detailUrl`, và cả `hostName` lẫn `recipientName` cho mọi người; template khai `plannedStart` + `plannedEnd`. **`plannedEnd` chưa bao giờ được truyền.** | Sai tên + thiếu biến + renderer không kiểm tra = lỗi hiển thị ra tới người nhận. Nay lệch bộ biến là fail-closed 2 chiều. |
| B8-04 | `detailUrl` chuyển từ **biến template** sang **trusted block** `EmailComposition.VisitDetailBlock` + `IEmailActionTokenService.BuildVisitInstanceDetailUrl`. | URL do backend dựng, đi đúng một đường. Template cũ không có `{{detailUrl}}` nên link **chưa từng xuất hiện trong email**. |
| B8-05 | Tách `VisitReminderDispatchService` (Application) khỏi hosted service. | Quyết định "ai được nhắc, từ template nào, đã gửi chưa" có hệ quả thật; phải test được, không thể chỉ chạy trong tiến trình nền. Hosted service nay chỉ còn: đánh thức, và một tick hỏng không dừng timer. |
| B8-06 | **Claim bằng một UPDATE có điều kiện** (`WHERE status = PENDING`) trước khi gửi. | Cơ chế cũ là "đọc PENDING → gửi → set SENT": hai instance API polling cùng giây đều thấy PENDING và **đều gửi**. Claim chuyển thẳng sang SENT nên đúng một worker thắng. |
| B8-07 | Chống trùng theo **at-most-once**: chết giữa claim và gửi = mất, không gửi lại. | Ngược lại (retry mọi row còn PENDING) rủi ro đúng tình huống SMTP đã nhận nhưng ghi status chưa kịp — gửi lại là **gửi trùng cho người thật, không thu hồi được**. Thiếu một nhắc lịch thì người ta xử lý được; trùng thì không, và schema không có trạng thái nào để phân biệt sau đó. |
| B8-08 | Thêm chặn trùng theo **mailbox đã chuẩn hoá**, không chỉ theo user_id. | Hai tài khoản có thể dùng chung một hòm thư; người thật không nên nhận hai bản. |
| B8-09 | Host được nhắc **với tư cách Host** kể cả khi cũng có hàng participant. | Đó là vai mang trách nhiệm mà email nói tới. |
| B8-10 | `Skipped` (SMTP tắt ngoài production) **không** làm reminder FAILED. | Không có gì hỏng cả — đúng hợp đồng "Skipped không phải thành công cũng không phải thất bại". |
| B8-11 | Bỏ `RetryCount += 1` lần đầu; `error_message` là câu an toàn cố định thay `ex.Message`. | Cùng lý do B5-05/B5-06. `error_message` hiển thị cho người vận hành và có thể chứa host nội bộ. |

**Thất bại một phần (HOST_AND_PARTICIPANTS).** Schema chỉ có PENDING/SENT/CANCELLED/FAILED — không có PARTIAL, và Batch 8 **không** thêm enum. Chính sách giữ nguyên như cũ: chỉ cần một message FAILED thì reminder là FAILED (kèm `"{n}/{m} email nhắc lịch không gửi được."`), và những message **đã gửi được sẽ không gửi lại** — vì một reminder FAILED không bao giờ được query lại. Nếu anh muốn đổi chính sách này thì cần quyết định riêng.

**Failure window còn lại (ghi rõ, không che):** claim → (chết) → không gửi, reminder ở SENT nhưng người nhận không nhận được gì. Đây là đánh đổi có chủ ý ở B8-07. Cửa sổ ngược lại — gửi rồi chết trước khi ghi — **không tồn tại nữa**, vì status đã được ghi trước khi gửi.

C-23 trước batch này **không có test nào**. Nay 16 unit + 14 integration MIME/DB + 8 integration idempotency/concurrency (gồm hai worker chạy đua trên MySQL thật).

---

## Ghi nhận Batch 9 (Giai đoạn 4 — Báo cáo / hoá đơn)

Sáu caller C-24..C-29, bốn template `REPORT_*`.

**Phát hiện lớn nhất — không caller nào có tệp đính kèm.** Cả sáu tự dựng bảng HTML rồi gọi `IEmailService.SendAsync(to, subject, html)`. Bốn template trong catalog đều mở đầu bằng *"Đính kèm là báo cáo…"*, nên nếu chỉ thay nội dung mà không thêm tệp thì email sẽ **nói một câu không đúng sự thật**, và toàn bộ số liệu hiện đang nằm trong body sẽ biến mất. Vì vậy Batch 9 chuyển số liệu từ body sang PDF đính kèm — đó là lý do batch này lớn hơn 5–8.

| # | Quyết định | Vì sao |
|---|---|---|
| B9-01 | Body/subject của cả sáu lấy từ `email_templates` qua `SystemEmailContent.FromTemplate`. Xoá toàn bộ `BuildEmailHtml`/`BuildInvoiceHtml` (6 hàm, ~600 dòng HTML dựng tay). | Cùng lý do D-01. Sau khi xoá, quét toàn repo: bốn chuỗi subject cũ chỉ còn trong canonical SQL và tài liệu, **không còn dòng C# nào**. |
| B9-02 | Số liệu chuyển sang PDF đính kèm, dựng bằng QuestPDF theo **đúng house style các bản export tải-về đang dùng** (A4, lề 1,6 cm, tiêu đề `#004C91`, bảng đầu xanh, footer số trang). | Template hứa có tệp đính kèm. Dùng lại house style để một báo cáo nhận qua mail và cùng báo cáo đó tải từ màn hình trông như một tài liệu. **Không sửa một dòng nào** trong các handler `Export*` hiện có — layout cũ giữ nguyên từng byte. |
| B9-03 | `ReportEmailSender` dùng chung: kiểm PDF → lưu file → `PrepareAsync` → ghi `sent_email_attachments` → `DeliverAsync` → ép Mandatory. | Năm bước này vô hình từ bên ngoài: một attachment không vào `sent_email_attachments` vẫn tới hộp thư người nhận, và một lần gửi Skipped vẫn giống thành công với caller chỉ bắt exception. Làm một lần ở đây thì cả sáu cùng đúng. |
| B9-04 | Dùng `IFileStorageService` + bảng `files` sẵn có (`file_purpose = 'REPORT_ATTACHMENT'`, cột là VARCHAR nên **không đổi canonical SQL**). Không bảng mới, không Google Drive. | `sent_email_attachments.file_id` là NOT NULL + FK RESTRICT — không thể ghi linkage nếu không có file thật. Google Drive là network call ngoài trong một lệnh Mandatory. |
| B9-05 | C-26 và C-29 dùng **chung** `REPORT_DEPARTMENT_INVOICE`; C-27 và C-28 dùng **chung** `REPORT_PERSONNEL_PERFORMANCE`. Khác biệt nghiệp vụ đi qua biến `scopeLabel`, không qua template thứ năm. | Hai chiều hoá đơn có subject giống hệt nhau từng ký tự (audit #172). Tách template là cách chúng lệch nhau về sau. |
| B9-06 | `scopeLabel` có **cả hai ngôn ngữ** trong `PersonnelReportScopes`, cạnh caller chọn phạm vi — không nằm trong renderer. | Renderer phải mù về ý nghĩa của biến. Để cả hai ngôn ngữ cạnh nhau thì gửi bản EN sau này không phải hard-code cụm từ thứ hai tại call site. |
| B9-07 | `ReportPeriod` là nơi **duy nhất** đổi `[from, toExclusive)` thành nhãn người đọc (lùi một ngày). Email và PDF dùng chung một cặp nhãn. | Nếu đổi ở hai chỗ thì một bên ghi "01/07 – 31/07", bên kia "01/07 – 01/08". Hoá đơn không lọc theo kỳ nên giữ nguyên nhãn cũ, gồm cả `—` khi thiếu ngày bắt đầu (bịa ra một ngày còn tệ hơn). |
| B9-08 | Tên tệp qua `ReportAttachmentName`: giữ convention `PEMS_{Topic}_{yyyyMMdd_HHmm}.pdf`, **từ chối** (không tự sửa) tên có ký tự điều khiển, dấu phân cách đường dẫn hoặc `..`. Unicode được phép. | Tên tệp vào header `Content-Disposition`. Từ chối thay vì viết lại: một báo cáo gửi dưới cái tên không ai chọn tệ hơn một lần gửi dừng lại và nói rõ lý do. |
| B9-09 | Mandatory là Mandatory: `Skipped` và `Failed` đều **ném lỗi** (`EMAIL_REPORT_DELIVERY_FAILED`). Lỗi ném **sau** khi history + linkage đã ghi. | Người dùng bấm "gửi báo cáo". Skipped nghĩa là SMTP tắt — không tới provider nào. Ném sau khi ghi để bằng chứng FAILED không mất theo. |
| B9-10 | Prepare thất bại (template hỏng) → **xoá hàng `files` vừa tạo**. Blob trên đĩa không xoá được (contract storage không có delete) nhưng không còn hàng nào trỏ tới. | Đúng đánh đổi `FileUploadService` đã chấp nhận khi insert metadata hỏng. Không để hàng mồ côi trong `files`. |
| B9-11 | Mỗi người nhận **một message riêng, một hàng `files` riêng**, stream mới mỗi lần. Không CC/BCC, không gộp. | Hai lần gửi dùng chung stream thì người thứ hai nhận bản PDF cụt — đúng lỗi mà stream dùng lại sinh ra. Có test cho việc này. |

**Cố ý KHÔNG sửa — hai endpoint C-26/C-29 chưa tồn tại.** Frontend gọi `POST /reports/staff-leader-report-v2/departments/{id}/send-invoice` và `POST /reports/dept-leader-report-v2/send-invoice`, nhưng `ReportsController` **không có hai route đó** — hai handler này hiện không gọi được từ UI (404). Đây là lỗi có sẵn từ trước, nằm ngoài phạm vi chuẩn hoá email, và §X cấm đổi API contract ngoài phạm vi khi chưa có quyết định. Hai handler vẫn được migrate đầy đủ và có test integration chạy thẳng qua handler. **Cần quyết định riêng** có thêm endpoint hay không.

**Transaction.** Không caller nào trong sáu cái có transaction, trước và sau batch đều vậy — chúng đọc dữ liệu rồi gửi mail, không có mutation nghiệp vụ nào để bọc. Không có tuyên bố SMTP + DB atomic. Cửa sổ còn lại, ghi rõ: provider nhận message xong mà ghi status hỏng → history ở QUEUED trong khi người nhận đã có thư. Không tự thêm retry (gửi trùng một báo cáo cho người thật không thu hồi được); người dùng bấm gửi lại theo flow hiện có.

**Thất bại giữa chừng khi nhiều người nhận (C-25).** Gửi tuần tự; người đầu thành công, người sau hỏng → lệnh thất bại, và **những message đã gửi không bị gửi lại, history của chúng không bị xoá**. Đây là hành vi có chủ ý, không phải sót.

Cả sáu caller trước batch này **không có test nào**. Nay 48 unit + 17 integration (chạy handler thật trên MySQL thật, có MIME thật trên đĩa và file storage thật).

### B9-D1 — Khôi phục hai endpoint `send-invoice`

Frontend (`reportsApi.ts`) vẫn luôn POST tới hai URL mà `ReportsController` chưa expose, nên C-26 và C-29 trả 404 dù handler đã đầy đủ. Đã thêm đúng hai route đó, **không đổi command, handler, template, authorization hay data scope**.

| Route | Actor | Command | Phòng ban đến từ |
|---|---|---|---|
| `POST api/reports/staff-leader-report-v2/departments/{departmentId}/send-invoice` | `EffectiveRole.StaffLeader` | `SendStaffLeaderDeptInvoiceCommand` | **route**, campus suy từ người gọi |
| `POST api/reports/dept-leader-report-v2/send-invoice` | `EffectiveRole.DepartmentLead` | `SendDeptLeaderInvoiceToStaffLeaderCommand` | **người gọi** — request không có trường phòng ban |

Controller chỉ gán `command.DepartmentId = departmentId` rồi `IMediator.Send` — không query DB, không dựng PDF, không gửi mail, không đụng nghiệp vụ. Scope do handler enforce như trước: Staff Leader gửi cho phòng ban ngoài campus của mình vẫn **404** (test), và request của cả hai chiều **không có trường nào để chọn người nhận** — người nhận do backend suy ra (trưởng phòng của phòng ban, hoặc Staff Leader của campus).

Mandatory đi tới tận API: cấu hình Testing mặc định có `Smtp:Enabled=false` ⇒ Skipped ⇒ route **không trả 200** (test).

**Cố ý không sửa — chưa màn hình nào gọi hai hàm này.** `reportsApi.sendStaffLeaderDeptInvoice` và `sendDeptLeaderInvoiceToStaffLeader` được khai báo nhưng không component nào import (`DeptReportManagement.tsx` chỉ có `exportInvoicePdf` — in tại chỗ, không gửi mail). Route đã sống, URL không còn chết; **nút bấm thì chưa có**. Thêm nút là đổi UI/flow, nằm ngoài phạm vi lần này.

15 integration test HTTP thật: route không còn 404, chỉ nhận POST, route id thắng body, anonymous → 401, sai vai → 403, khác campus → 404, phòng ban lạ → 404, hai chiều đều ra đúng một thư + một PDF + một hàng `sent_email_attachments` + `REPORT_DEPARTMENT_INVOICE` + 1 TO/0 CC/0 BCC.

---

## Giai đoạn 5 — Manual email (draft / compose / reply / history)

Quyết định của lượt này. Chi tiết đầy đủ: `docs/Ver2Carnh/canh/email/05-manual-email-draft-reply-history-g5.md`.

| # | Quyết định | Vì sao |
|---|---|---|
| G5-01 | Một lớp `ManualEmailSender` dùng chung cho compose, draft-send và reply: một `sent_emails`, N recipient rows đúng nhóm, **một** MIME cho cả TO+CC+BCC. | Cả ba đều gửi vòng lặp từng người nhận, nên một thư "gửi ba người" thực chất là ba thư, mỗi người thấy mình là người duy nhất. CC mất hẳn ý nghĩa. Sửa ở một chỗ thì cả ba cùng đúng. |
| G5-02 | Reply **gửi thật** CC/BCC mới; bỏ hẳn việc ghi hàng recipient rồi không gửi tới. | Handler cũ ghi CC/BCC vào `sent_email_recipients` rồi chỉ `SendAsync(toEmail…)`. Lịch sử khẳng định đã gửi cho những người chưa từng nhận gì — sai lệch tệ hơn cả việc không hỗ trợ CC/BCC. |
| G5-03 | Reply **không** sửa email gốc. Bỏ `originalEmail.DeliveredAt = now`. | Đó là bịa xác nhận đã-nhận trên một bản ghi mà reply không có quyền đụng. Đánh dấu đã xử lý vẫn là hành động riêng qua `mark-completed`. |
| G5-04 | Nội dung manual đi qua `ManualEmailContent`; nhánh HTML **uỷ quyền** cho `SystemEmailContent.AuthoredByUser`. Nhánh PLAIN_TEXT áp cùng luật nhưng **không** HTML-sanitize. | Một nguồn luật duy nhất cho "người ta được viết gì trong email". Chạy sanitizer HTML lên plain text sẽ âm thầm nuốt ký tự `<` mà người viết có quyền dùng — sửa lại lời họ viết. |
| G5-05 | `sent_emails.email_template_id` = **NULL** cho mọi email manual, kể cả khi người soạn mở từ một template. | Một hàng có `email_template_id` nghĩa là nội dung đến từ `email_templates` và mang chính sách nhạy cảm của template đó. Sau khi người ta sửa chữ, cả hai điều đó đều sai. Giữ vết "mở từ template nào" ở hàng `email_drafts`, không lan sang lịch sử gửi. |
| G5-06 | `SentEmailAccess` là **một** rule cho mọi surface đọc email: quan hệ với thư (sender / TO-CC / BCC / object scope) quyết định, **không** phải role. | `ViewEmail` từng bỏ hẳn filter, `ViewEmailList` thì không — hai surface bất đồng về ai được đọc gì. Rule sống một chỗ thì không lệch được. |
| G5-07 | BCC lọc theo người xem: sender thấy tất cả · TO/CC/object-scope không thấy gì · BCC chỉ thấy chính mình. **Không** `bccCount`/`hasBcc`/`recipientTotal`. | Lọc danh sách mà vẫn in "còn 2 người nhận ẩn" thì lộ y hệt. Có test quét property của DTO để giữ điều này đúng về sau. |
| G5-08 | Object scope **đi mượn** `VisitReminderAccess.CanView`, không tự chế điều kiện mới. Email `GENERAL`/`REPLY` không có object scope. | Ai mở được màn VisitProcess thì đọc được thư của visit đó — đúng một điều kiện, không phải hai. Thư từ cá nhân thì chỉ người trong cuộc, dù cấp bậc nào. |
| G5-09 | Draft: validate recipient khi soạn với `requireTo: false`, khi gửi với `requireTo: true`. Mọi rule khác áp như nhau. | Draft chưa có TO là trạng thái bình thường của một thư đang viết dở. Trùng địa chỉ hay trùng chéo nhóm thì không — và báo ngay lúc soạn tốt hơn báo lúc bấm gửi. |
| G5-10 | Chống double-send bằng một `UPDATE email_drafts SET status='SENT' WHERE … AND status='DRAFT'`; chỉ request thắng hàng mới đi gửi. | Hai click cùng lúc đều qua được kiểm tra trạng thái vì cả hai đọc trước khi ai kịp ghi. Quyết định phải do database đưa ra. Đổi lại là một cửa sổ hẹp (claim xong rồi chết → draft SENT không có message) — chấp nhận được so với gửi trùng cho người thật. |
| G5-11 | `Message-Id` do PEMS sinh, đặt vào message thật và lưu ở `provider_message_id`. Reply gắn `In-Reply-To`/`References` **chỉ khi** cha thật sự có id. | Không có id do mình kiểm soát thì không thể nối thread một cách trung thực. Cha là dữ liệu cũ không có id → không gắn header nào, không bịa. |
| G5-12 | Transaction **không** bao SMTP: ghi history → gửi → ghi kết quả. Skipped ở lại `QUEUED`, đúng như G4 đã chốt. | Một transaction không thể trải qua cuộc gọi mạng. Chết giữa chừng để lại dòng `QUEUED` — đúng sự thật — thay vì rollback một message provider đã nhận. |

**`GET /api/files/{id}/download` — đã đóng ở lượt security closure ngay sau đó.** Xem G5-13 dưới đây.

**Cố ý không sửa — reply bấm hai lần tạo hai reply.** Draft có bản ghi để claim, reply thì không. Chống trùng cần khoá idempotency ở tầng request.

### G5-13 — File download phải qua object-scope authorization trước khi mở stream

`GET /api/files/{id}/download` (và `/content`) chỉ kiểm đã-đăng-nhập rồi đọc file. Vì mọi module dùng chung bảng `files` và khoá là số nguyên tuần tự, `file_id` trở thành **chìa khoá vạn năng**: một người nội bộ đoán id là tải được attachment của email họ không được xem — đi vòng qua đúng bộ rule `SentEmailAccess` mà G5 vừa dựng — cùng với draft chưa gửi của người khác, hoá đơn campus khác, ảnh chuyến thăm và tài liệu đối tác.

| # | Quyết định | Vì sao |
|---|---|---|
| G5-13a | Một service dùng chung `IFileAccessAuthorizationService`: phân giải **các reference** tới file rồi gọi **rule của chính module cha**. Chạy **trước** khi resolve đường dẫn vật lý và trước khi mở stream. | File không có quyền của riêng nó; nó đọc được vì **thứ tham chiếu nó**. Viết rule mới ở tầng file sẽ tạo ý kiến thứ hai về việc ai được xem một visit hay một partner — đúng loại bất đồng giữa các surface mà G5-06 vừa dọn ở history. |
| G5-13b | Email attachment tái dùng **nguyên** `SentEmailAccess` + linked-object scope. Draft attachment dùng **quyền sở hữu draft**, không ngoại lệ cho Staff Leader/HO/Admin. | Nếu file có rule email riêng thì nó sẽ lệch khỏi list/detail/reply đúng vào lúc không ai để ý. Draft chưa gửi là thứ riêng tư nhất trong module. |
| G5-13c | Đủ **một** reference hợp lệ là được tải (không bắt mọi reference cùng cho phép). | Cùng một ảnh vừa đính kèm email vừa là cover bài news đã PUBLISHED thì nó đã công khai với cả thế giới; từ chối một đồng nghiệp đã đăng nhập không bảo vệ được gì. Chiều ngược lại khiến file càng dùng lại càng khó đọc — không ai đoán được từ giao diện. |
| G5-13d | File **không có reference nào** → chỉ người upload. | Người đang soạn dở cần xem lại file mình vừa tải lên; không ai khác có lý do. Mặc định là đóng, không phải mở. |
| G5-13e | **Không role nào là master key.** Không có nhánh HO/Admin ở bất kỳ đâu trong service. | Cấp bậc tới được file theo đúng đường mọi người đi: có quyền với thứ tham chiếu nó. |
| G5-13f | Với module mà màn hình của chính nó mở cho mọi người dùng nội bộ (partner, documents, news/gallery nháp, avatar), rule file là **mirror** đúng như vậy — ghi rõ là mirror, không phải siết. | Siết chặt hơn màn hình sẽ làm hỏng màn hình; siết là quyết định của module sở hữu nó. Điều đã đạt: file không còn **lỏng hơn** màn hình. |
| G5-13g | Route public giữ nguyên: `/api/public/news-files`, `/api/public/partners/media`, `/api/public/visit-fptu/media` đã tự kiểm cha PUBLISHED/PUBLIC. Route authenticated **không** phục vụ ẩn danh. | Public và private tách route sẵn rồi; gộp lại làm policy khó kiểm soát. Không tạo signed URL/CDN. |
| G5-13h | Thêm kiểm **containment** dưới storage root khi resolve `object_key`; khoá escape → coi như file không tồn tại. | Khoá do server sinh và client chỉ gửi số, nên traversal không tới được đây — nhưng khoá đã lưu vẫn là *dữ liệu*, và dữ liệu quyết định mở đường dẫn nào thì đáng kiểm tra hơn là tin. |
| G5-13i | Từ chối **không** kèm tên file, dung lượng, MIME, chủ sở hữu, đối tượng liên quan, người nhận ẩn hay nội dung. | Một lần từ chối có mô tả sẽ biến chính nó thành endpoint metadata cho file người ta không đọc được. |

**Blocker ghi nhận, không tự sửa:** `DocumentsController` không có `[Authorize]` ở cấp framework (phát hiện khi audit). Nằm ngoài phạm vi lượt này và **không** được nới qua đường file — route file vẫn bắt buộc đăng nhập. Cần một lượt riêng cho module Documents.

---

## C-01. Thư viện MIME — quyết định bắt buộc trước khi làm CC/BCC

`System.Net.Mail.MailMessage` **có** `Cc` và `Bcc`, và `SmtpClient` xử lý BCC đúng chuẩn: địa chỉ BCC được đưa vào lệnh SMTP `RCPT TO` nhưng **không** ghi vào header `Bcc:` của message. Đây là hành vi chuẩn và đủ để thoả D-04/Mục 12.5.

**Quyết định: giữ `System.Net.Mail`, KHÔNG chuyển sang MailKit.** Lý do:

- Đủ tính năng cho yêu cầu (To/Cc/Bcc, attachment, inline cid, ReplyTo, header tuỳ biến).
- Chuyển sang MailKit là tái kiến trúc vượt phạm vi (vi phạm D-09) và làm hỏng `EmailServiceDeliveryOutcomeTests` đang xanh (test override `DispatchAsync(MailMessage, …)`).
- `SmtpClient` bị .NET đánh dấu obsolete cho *code mới*, nhưng đây là code đang chạy, không phải code mới.

**Hệ quả kiểm thử:** không thể lấy raw MIME từ `SmtpClient` trực tiếp. Cách chứng minh To/Cc/Bcc (Mục 12.5 của kế hoạch) là **ghi `MailMessage` ra thư mục bằng `SmtpDeliveryMethod.SpecifiedPickupDirectory`** — .NET tự serialize ra file `.eml` **đúng chuẩn MIME, và đã loại header `Bcc:`**. Test đọc file `.eml` đó. Đây là raw MIME thật, không phải mock.

---

## C-02. System template registry (Mục 11.2)

Type: `PEMS.Application.Emails.Common.SystemEmailTemplates` — bảng tra tĩnh theo `templateCode`.

**Registry GIỮ:**

```
TemplateCode        string
Purpose             string      (từ EmailTemplatePurposes, xem C-11)
RecipientPolicy     enum        SingleRecipientNoCopies | CallerControlled
RequiresPerRecipientSend  bool  (true = mỗi người 1 message riêng)
HasSensitiveAction  bool        (OTP / token / action URL riêng)
ActionSpec          EmailTemplateActionSpec?   (mở rộng từ EmailActionTemplates hiện có)
DeclaredVariables   string[]    (lower camelCase, khớp variables_text)
LanguageFallback    enum        ViThenEn | EnThenVi
```

**Registry KHÔNG GIỮ:** `Subject`, `Body`, HTML nội dung nghiệp vụ, địa chỉ CC/BCC cố định. Nếu một hằng chuỗi nào trong registry chứa dấu câu tiếng Việt của nội dung email → sai hợp đồng.

`EmailActionTemplates` hiện có được **giữ lại và mở rộng**, không viết lại — nó đã đúng vai trò "metadata chính sách" và đang được preview dùng.

---

## C-03. Renderer contract (Mục 11.3)

Type: `PEMS.Application.Common.Interfaces.IEmailTemplateRenderer`, implement ở `PEMS.Infrastructure.Email.EmailTemplateRenderer` (file rỗng hiện tại **bị thay hoàn toàn**, kể cả `namespace PEMS.Shared` sai).

**Input** — `EmailRenderRequest`:

| Trường | Bắt buộc | Ghi chú |
|---|---|---|
| `TemplateCode` | ✅ | phải có trong registry |
| `Language` | ✅ | `VI` \| `EN` |
| `Variables` | ✅ | `IReadOnlyDictionary<string,string>`, key lower camelCase |
| `TrustedHtmlBlocks` | ❌ | `IReadOnlyDictionary<string,string>` — HTML do backend tạo (action block), **không** encode |
| `CampusId` | ❌ | chỉ truyền khi caller thật sự cần template theo campus |
| `CancellationToken` | ✅ | |

**Output** — `EmailRenderResult`:

```
EmailTemplateId   ulong
TemplateCode      string
Subject           string
Body              string
BodyFormat        EmailBodyFormat
LanguageUsed      string     ("VI" | "EN")
```

**11 bước bắt buộc, đúng thứ tự:**

1. Tra registry theo `TemplateCode`; không có → `EMAIL_TEMPLATE_NOT_FOUND`.
2. Query DB `email_templates` theo `template_code` **tại thời điểm gọi** (không cache — xem C-04); không có → `EMAIL_TEMPLATE_NOT_FOUND`.
3. `status != 'ACTIVE'` → `EMAIL_TEMPLATE_INACTIVE`.
4. Chọn nội dung theo `Language`; thiếu subject **hoặc** body của ngôn ngữ đó → `EMAIL_TEMPLATE_LANGUAGE_CONTENT_MISSING`. **Không** tự fallback sang ngôn ngữ khác (fallback im lặng = D-01 bị vi phạm).
5. Parse `variables_text` thành tập biến khai báo.
6. Đối chiếu **chính xác hai chiều**: thiếu → `EMAIL_TEMPLATE_VARIABLE_MISSING`; thừa → `EMAIL_TEMPLATE_VARIABLE_UNKNOWN`.
7. HTML-encode **mọi** giá trị trong `Variables`. `TrustedHtmlBlocks` không encode.
8. Thay placeholder `{{name}}` trong subject và body.
9. Subject: chặn `\r`, `\n`, và ký tự điều khiển → `EMAIL_HEADER_INVALID`. Subject cũng bị strip HTML (subject không bao giờ là HTML).
10. Quét placeholder còn sót (`{{…}}` hoặc dạng URL-encode `%7B%7B…%7D%7D`) → `EMAIL_TEMPLATE_UNRESOLVED_PLACEHOLDER`.
11. Trả `EmailRenderResult`. **Không** log subject/body/giá trị biến.

**Renderer KHÔNG có fallback nội dung.** Không có tham số `defaultSubject`. Không có `?? "…"`.

**Preview và gửi thật dùng CÙNG hàm này** (D-03). `PreviewEmailTemplateQueryHandler` sẽ gọi `IEmailTemplateRenderer` với `TrustedHtmlBlocks` = block *disabled*, `SendXxx` gọi với block *thật*.

---

## C-04. Chính sách cache (Mục 11 / D-02)

**Không cache nội dung template.** Renderer query `email_templates` mỗi lần preview/send.

Được cache: `SystemEmailTemplates` registry (hằng compile-time, không phải nội dung).

Kiểm chứng bằng E2E-04: sửa `subject_vi`/`body_vi` bằng SQL trực tiếp trên disposable DB → preview lại → thấy nội dung mới **không restart backend**.

---

## C-05. Stable errors (Mục 11.4)

Hằng ở `PEMS.Application.Emails.Common.EmailErrorCodes`. Ném bằng `BusinessRuleException(message, code)` (DL-06), trừ 2 trường hợp ghi rõ.

| Code | Exception | HTTP | Khi nào |
|---|---|---|---|
| `EMAIL_TEMPLATE_NOT_FOUND` | `NotFoundException` | 404 | mã không có trong registry hoặc trong DB |
| `EMAIL_TEMPLATE_INACTIVE` | `ConflictException` | 409 | `status != ACTIVE` |
| `EMAIL_TEMPLATE_LANGUAGE_CONTENT_MISSING` | `BusinessRuleException` | 422 | thiếu subject/body của ngôn ngữ yêu cầu |
| `EMAIL_TEMPLATE_VARIABLE_MISSING` | `BusinessRuleException` | 422 | caller không truyền biến đã khai báo |
| `EMAIL_TEMPLATE_VARIABLE_UNKNOWN` | `BusinessRuleException` | 422 | caller truyền biến không khai báo |
| `EMAIL_TEMPLATE_UNRESOLVED_PLACEHOLDER` | `BusinessRuleException` | 422 | còn `{{…}}` sau khi render |
| `EMAIL_TEMPLATE_CONTENT_INVALID` | `BusinessRuleException` | 422 | nội dung template không qua được sanitizer khi create/update |
| `EMAIL_RECIPIENT_REQUIRED` | `ValidationException` | 400 | không có `TO` nào |
| `EMAIL_RECIPIENT_INVALID` | `ValidationException` | 400 | email sai format |
| `EMAIL_RECIPIENT_DUPLICATE` | `ValidationException` | 400 | trùng trong cùng nhóm (không phân biệt hoa/thường) |
| `EMAIL_RECIPIENT_CROSS_GROUP_DUPLICATE` | `ValidationException` | 400 | cùng email ở ≥2 nhóm TO/CC/BCC |
| `EMAIL_RECIPIENT_LIMIT_EXCEEDED` | `ValidationException` | 400 | vượt `Email:MaxRecipients` |
| `EMAIL_RECIPIENT_TYPE_NOT_ALLOWED` | `BusinessRuleException` | 422 | template cấm CC/BCC nhưng caller truyền |
| `EMAIL_HEADER_INVALID` | `ValidationException` | 400 | CR/LF trong subject, email hoặc display name |

Mỗi mã **phải có ít nhất 1 negative test** (Mục 21 của kế hoạch).

---

## C-06. Placeholder contract (Mục 11.5)

- **Chỉ lower camelCase**: `{{fullName}}`, `{{requestCode}}`. Cấm `{{FullName}}`, `{{RequestCode}}`, `{{DETAIL_URL}}`.
- Regex hợp lệ: `\{\{\s*([a-z][A-Za-z0-9]*)\s*\}\}`. Placeholder không khớp regex này → coi là chưa render → `EMAIL_TEMPLATE_UNRESOLVED_PLACEHOLDER`.
- `variables_text` là danh sách phân tách bằng dấu phẩy, **khớp chính xác** tập placeholder thật trong `subject_vi + body_vi + subject_en + body_en`. Test hợp đồng kiểm cả 4 trường.
- **Không** để raw token/OTP/action URL trong seed.
- Giá trị biến thông thường **luôn** bị HTML-encode → không thể chèn HTML qua dictionary.
- HTML tin cậy chỉ đi qua `TrustedHtmlBlocks`, do backend tạo.

**Migration D-17:** 7 template cũ dùng PascalCase. Vì cả 7 nằm trong nhóm 9 mã bị bỏ khỏi fresh seed (DL-03), **không cần migrate** — chúng chuyển `INACTIVE` và không còn được render.

---

## C-07. HTML / security contract (Mục 11.6)

| Điểm | Quy tắc |
|---|---|
| Khi create/update template | Chạy `IHtmlSanitizerService.SanitizeEmailHtml` trên `body_vi`, `body_en`. Nếu kết quả khác đầu vào ở phần **có nghĩa** → vẫn lưu bản đã sanitize (không từ chối), nhưng nếu sanitize ra rỗng trong khi đầu vào không rỗng → `EMAIL_TEMPLATE_CONTENT_INVALID` |
| `subject_vi` / `subject_en` | Không bao giờ là HTML. Strip tag + chặn CR/LF khi lưu |
| `body_format = PLAIN_TEXT` | **Không** chạy sanitizer HTML; không được render như HTML ở bất kỳ đâu; `IsHtml=false` khi gửi |
| Khi render | Vẫn encode biến (lớp thứ hai, độc lập với sanitize lúc lưu) |
| Preview vs gửi thật | Cùng nội dung đã xử lý, cùng renderer |
| Logging | **Cấm** log full body, OTP, raw token, action URL. Địa chỉ log dạng `***@domain` (đã có ở `EmailService.MaskEmail`) |

---

## C-08. Action block contract (Mục 11.7)

Backend chịu trách nhiệm toàn bộ: tạo OTP/token, tạo URL một lần, encode URL, sinh block từ `EmailComposition.*ActionBlock`, gắn **sau** render.

- `acceptUrl`, `declineUrl`, `assignUrl`, `detailUrl`, `negotiateUrl`, `approveProposalUrl`, `rejectProposalUrl`, `confirmBorrowUrl`, `confirmReturnUrl` bị **loại khỏi `variables_text`** — chúng không phải biến editable.
- Nội dung template quản trị **không** chứa các URL đó. `EmailComposition.StripActionArtifacts` được giữ để dọn template cũ/nội dung host tự dán, nhưng seed mới không được sinh ra thứ cần dọn.
- Preview dùng block **disabled** (`DisabledAcceptDeclineBlock`, …) — **không** phát hành token thật.
- Block được đưa vào renderer qua `TrustedHtmlBlocks["actionBlock"]`, và template khai báo vị trí bằng `{{actionBlock}}`. `actionBlock` là **tên trong `TrustedHtmlBlocks`, không** liệt kê trong `variables_text`.

---

## C-09. Outbound recipient model (Mục 11.8)

```
EmailRecipient (record)
    Email        string
    DisplayName  string?

OutboundEmail
    To            IReadOnlyList<EmailRecipient>
    Cc            IReadOnlyList<EmailRecipient>
    Bcc           IReadOnlyList<EmailRecipient>
    Subject       string
    Body          string
    IsHtml        bool
    Attachments   IReadOnlyList<OutboundAttachment>
    TemplateCode  string?     (metadata, để dispatcher kiểm policy)
    ReplyTo       EmailRecipient?
    Headers       IReadOnlyDictionary<string,string>?   (thread headers: In-Reply-To, References)
```

**`OutboundEmail.ToEmail` (string) bị xoá.** Mọi caller phải chuyển sang `To`. Đây là breaking change có chủ ý: nếu còn caller nào dùng contract cũ thì compile lỗi — đúng điều ta muốn, thay vì im lặng gửi sai.

`IEmailService` sau tái cấu trúc:

```
Task<EmailDeliveryResult> SendAsync(OutboundEmail message, CancellationToken ct)
```

**Bị xoá khỏi interface** (đều là nội dung hard-code, đã di chuyển sang template): `SendPasswordResetAsync`, `SendVisitRequestOtpAsync`, `SendVisitorAccountCreatedOrLinkedEmailAsync`, `SendRegistrantConfirmationAsync`.
Hai hàm cuối là dead code (audit Mục 4) → xoá luôn khỏi `EmailService` và `FileSinkEmailService`.

`SendAsync(string,string,string,…)` và `TrySendAsync(string,string,string,…)` **bị xoá**; caller dùng `OutboundEmail` với 1 phần tử `To`.

---

## C-10. Recipient validation (Mục 11.9)

Type: `PEMS.Application.Emails.Common.EmailRecipientValidator` — **một nơi duy nhất**, dùng chung cho draft, compose, reply và mọi system caller.

| Quy tắc | Chi tiết |
|---|---|
| Normalize | `Trim()`, so sánh `OrdinalIgnoreCase`. Không đổi hoa/thường khi **lưu** (giữ nguyên người dùng gõ), chỉ dùng dạng thường để **so sánh** |
| Format | `MailAddress.TryCreate` + yêu cầu có đúng 1 `@` và phần domain có `.` |
| CR/LF | Chặn `\r`, `\n`, `\0` trong cả `Email` và `DisplayName` → `EMAIL_HEADER_INVALID` |
| Duplicate cùng nhóm | Từ chối → `EMAIL_RECIPIENT_DUPLICATE` |
| Duplicate chéo nhóm | Từ chối → `EMAIL_RECIPIENT_CROSS_GROUP_DUPLICATE` |
| Tối thiểu | Bắt buộc ≥1 `TO` → `EMAIL_RECIPIENT_REQUIRED` |
| Giới hạn | Tổng `To+Cc+Bcc` ≤ `Email:MaxRecipients` (config, **mặc định 50**) → `EMAIL_RECIPIENT_LIMIT_EXCEEDED` |

**Không rải magic number.** Giá trị 50 chỉ xuất hiện ở `appsettings*.json` + một hằng default trong `EmailRecipientOptions`.

---

## C-11. Purpose catalog (Mục 11.13)

Type: `PEMS.Domain.Constants.EmailTemplatePurposes` — **nguồn duy nhất**, dùng ở validator, entity comment, API DTO, frontend options, unit test, SQL seed.

| Purpose | Dùng cho |
|---|---|
| `ACCOUNT` | tạo/kích hoạt/đổi email/đổi vai trò tài khoản |
| `AUTH` | đặt lại mật khẩu |
| `VISIT_REQUEST` | OTP đăng ký thăm, claim/transfer đầu mối |
| `VISIT_PARTICIPANT` | mời/gán người tham gia |
| `VISIT_REMINDER` | nhắc lịch |
| `LOGISTICS` | yêu cầu/phân công/đề xuất/nhắc chi phí hậu cần |
| `REPORT` | báo cáo và hoá đơn |

**7 giá trị. Không hơn.**

**Sửa D-13:** `CreateEmailTemplateCommandValidator.AllowedPurposes` và `UpdateEmailTemplateCommandValidator` đang dùng `OtpPurpose.*` → thay bằng `EmailTemplatePurposes.All`. **Không** đụng `otp_tokens.purpose` — hai khái niệm độc lập (kế hoạch Mục 4.4).

Cột DB `email_templates.purpose` là `VARCHAR(100)`, **không** phải ENUM → đổi allowlist không cần migration.

---

## C-12. Recipient policy (Mục 11.10)

Dispatcher **luôn** kiểm policy dựa trên `OutboundEmail.TemplateCode`, kể cả khi caller đã tự kiểm. Caller không được tin.

| Nhóm | TO | CC/BCC | Gửi riêng từng người |
|---|---|---|---|
| `ACCOUNT` (mọi mã) | 1 | **Cấm** | ✅ bắt buộc |
| `AUTH` | 1 | **Cấm** | ✅ |
| `VISIT_REQUEST` (OTP, claim, transfer) | 1 | **Cấm** | ✅ |
| `VISIT_PARTICIPANT` (mọi mã) | 1 | **Cấm** | ✅ |
| `VISIT_REMINDER` | 1 | **Cấm** | ✅ (participant reminder không được lộ danh sách người khác) |
| `LOGISTICS` có token (`REQUEST_TO_DEPARTMENT`, `ASSIGNEE_ASSIGNMENT`, `CHANGE_PROPOSAL_TO_HOST`) | 1 | **Cấm** | ✅ |
| `LOGISTICS_EXPENSE_REPORT_REMINDER` | ≥1 | Cho phép **chỉ khi caller truyền rõ** | ❌ |
| `REPORT` (4 mã) | ≥1 | Cho phép **chỉ khi caller truyền rõ** | ❌ |
| Manual compose / draft / reply (không template) | ≥1 | **Cho phép** | ❌ (1 MIME cho tất cả) |

Vi phạm → `EMAIL_RECIPIENT_TYPE_NOT_ALLOWED`.

**Template không bao giờ chứa địa chỉ CC/BCC** (D-04). Không có cột nào cho việc đó và không được thêm.

---

## C-13. Status model (Mục 11.11)

```
QUEUED  ->  SENT        provider chấp nhận message
QUEUED  ->  FAILED      provider lỗi, hoặc fail-closed ở Production
QUEUED  ->  SKIPPED     SMTP tắt ở môi trường non-production
SENT    ->  DELIVERED   CHỈ khi có xác nhận thật từ provider/webhook
SENT    ->  BOUNCED     khi có provider event hợp lệ
```

**PEMS hiện KHÔNG có webhook provider.** ⇒ Sau khi SMTP accept, trạng thái cuối là **`SENT`**. Không có đường nào tự động sang `DELIVERED`.

Sửa D-5:

- `SentEmailRecipient.DeliveryStatus` = `SENT` (không phải `DELIVERED`) sau accept.
- `SentEmailRecipient.DeliveredAt` giữ `NULL`.
- `SentEmail.Status` = `SENT`; `SentEmail.DeliveredAt` giữ `NULL`.
- `SentEmail.SentAt` = thời điểm provider accept.

⚠️ **Tác dụng phụ phải xử lý:** `ViewEmailListQueryHandler` đang suy ra `ProcessStatus` và `CanMarkComplete` từ `DeliveredAt.HasValue`. Khi `DeliveredAt` không còn được set, mọi email sẽ mãi ở `PROCESSING`. Phải chuyển các cờ đó sang dựa trên `Status`/`MarkEmailCompleted` — xem Giai đoạn 5. **Không được bỏ sót**, nếu không sẽ là regression UI.

Với 1 message nhiều recipient (manual compose): provider accept là **theo message**, không theo recipient. ⇒ mọi recipient của message đó nhận cùng một kết quả. `PARTIAL_FAILED` chỉ còn ý nghĩa cho nhóm gửi-riêng-từng-người (system template), không cho manual compose.

**Nhất quán DB ↔ SMTP** (Mục 12.4): không tuyên bố atomic. Thứ tự:

1. Ghi `sent_emails` + `sent_email_recipients` ở `QUEUED`, commit.
2. Gửi SMTP.
3. Cập nhật trạng thái, commit.

Nếu bước 3 lỗi sau khi bước 2 thành công → hàng ở lại `QUEUED` dù mail đã đi. Đây là trạng thái "không chắc chắn", **an toàn hơn** ghi nhầm `SENT`. Không tự động retry ở bước 2 (tránh gửi trùng). Ghi rõ vào `04-requirement-test-traceability.md` như rủi ro tồn dư.

---

## C-14. BCC visibility (Mục 11.12)

Lọc **tại backend, trước khi tạo DTO** (D-07). Không trả rồi ẩn ở frontend. **Không** trả `bccCount` hay bất kỳ metadata gián tiếp nào.

| Viewer | Thấy BCC |
|---|---|
| Sender (`sent_emails.sent_by == viewer`) | Toàn bộ |
| Recipient loại `TO` | ❌ |
| Recipient loại `CC` | ❌ |
| Recipient loại `BCC` | Chỉ hàng của chính họ |
| Không liên quan | ❌ — và **không được xem email** |
| ADMIN / HO | ❌ trừ khi permission canonical cho phép rõ. Role rộng **không** tự cấp quyền |

**Quy tắc truy cập email (áp cho list, search, detail, reply payload, export):**

Viewer được xem một `sent_email` khi thoả **ít nhất một**:

1. là sender, **hoặc**
2. là recipient (bất kể TO/CC/BCC) khớp email của chính họ, **hoặc**
3. có quyền truy cập đối tượng liên kết (`related_type`/`related_id`) theo data scope hiện hành.

Ngoài ra → `ForbiddenException`.

Sửa D-3: `ViewEmailQueryHandler` hiện **không kiểm tra gì**. Phải thêm đủ 3 nhánh trên **trước** khi map DTO, rồi lọc BCC theo bảng trên.

---

## C-15. File-sink contract (Mục 12.6)

`FileSinkEmailService` mở rộng bản ghi JSON:

```
to[]            {email, displayName}
cc[]            {email, displayName}
bcc[]           {email, displayName}
templateCode
emailTemplateId
subject
body
bodyFormat
attachments[]   {fileName, contentType, isInline, contentId, sizeBytes}
relatedType, relatedId
headers         (thread headers nếu có)
at, status
```

Giữ nguyên `code` (OTP) và `link` để harness Playwright hiện tại không gãy — nhưng **chỉ trong file sink**, không ra console/log ứng dụng. Giữ nguyên cơ chế double-gate + fail-closed.

---

## C-16. Phạm vi KHÔNG làm

Nhắc lại để chặn phình phạm vi (D-09, Mục 6.2):

- ❌ Không tạo bảng `email_threads` / `email_messages` / `email_message_recipients`.
- ❌ Không xây inbox email thật.
- ❌ Không thêm outbox pattern / event bus / campaign.
- ❌ Không thêm caller email mới cho approve/reject/cancel/assign-host (D-05).
- ❌ Không sửa Gallery / FAQ / News / Translation.
- ❌ Không đổi permission hay lifecycle Visit Form v2.
- ❌ Không chạy trên `pems_db`. Không gửi mail thật. Không commit/push/PR.

---

## Gate G2 — kết luận

| Điều kiện G2 | Trạng thái |
|---|---|
| Template catalog cuối đã chốt | ✅ DL-02 + `03-system-template-catalog.md` |
| Registry metadata đã chốt | ✅ C-02 |
| Renderer contract đã chốt | ✅ C-03 (11 bước, không fallback) |
| Purpose catalog đã chốt | ✅ C-11 (7 giá trị) |
| Recipient policy đã chốt | ✅ C-12 |
| BCC authorization đã chốt | ✅ C-14 |
| Status transition đã chốt | ✅ C-13, gồm cả tác dụng phụ lên `ViewEmailList` |

**G2 ĐẠT.** Được phép sang Giai đoạn 3.
