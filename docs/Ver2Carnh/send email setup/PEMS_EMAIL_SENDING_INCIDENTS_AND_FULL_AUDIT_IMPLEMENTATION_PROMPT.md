# PEMS — PROMPT XỬ LÝ LỖI GỬI EMAIL HIỆN TẠI VÀ AUDIT TOÀN BỘ CÁC LUỒNG EMAIL

## Vai trò

Bạn là Senior Full-stack Engineer phụ trách **email, draft, attachment, OTP và outbound integrations** của PEMS.

Hãy làm việc trực tiếp trên source code hiện tại của nhánh đang checkout. Không giả định code vẫn giống tài liệu cũ. Phải đọc code, tái hiện lỗi, tìm root cause có bằng chứng rồi mới sửa.

Không dừng ở phân tích hoặc lập kế hoạch. Sau khi audit phải triển khai các fix cần thiết, viết test và báo cáo evidence.

---

# 1. Mục tiêu

Xử lý ba lỗi đã quan sát được:

```text
1. Không thể kết nối Google Drive. Vui lòng thử lại.
2. Temporarily unable to issue another verification code.
3. EmailDraft ({id}) was not found.
```

Đồng thời audit toàn bộ hệ thống gửi email để tìm các lỗi tiềm ẩn cùng nhóm, đặc biệt:

- Draft bị stale, mất hoặc thuộc sai database.
- Email gửi thiếu attachment nhưng hệ thống vẫn báo thành công.
- File Google Drive không tồn tại, hết quyền, token lỗi hoặc cấu hình sai.
- OTP bị rate-limit nhưng UI không hiển thị thời gian thử lại.
- Preview khác nội dung gửi thật.
- Body còn placeholder chưa render.
- Autosave ghi đè draft trong lúc hydrate.
- Gửi trùng do double click/concurrency.
- Draft đã gửi/hủy nhưng vẫn có thể mở hoặc gửi lại.
- Reply/CC/BCC bị mất hoặc chuyển sai nhóm.
- Email background/reminder lỗi nhưng không có evidence hoặc retry phù hợp.
- Resend/provider lỗi nhưng trạng thái DB không phản ánh đúng.
- Các luồng gửi email khác dùng implementation riêng và bị lệch validation.

---

# 2. Safety bắt buộc

Không được:

- Fresh-import database đang dùng.
- Xóa hoặc reset dữ liệu thật.
- Gửi email thật đến người dùng.
- Bật outbound provider thật khi test.
- Tạo bảng hoặc đổi schema khi chưa chứng minh bắt buộc.
- Sửa canonical SQL chỉ để test chạy.
- Làm mất WIP hoặc stash hiện tại.
- Push khi chưa được yêu cầu.
- Gộp các lỗi khác nhau thành một catch/message chung.
- Che lỗi bằng cách silently bỏ attachment, placeholder hoặc recipient.

Runtime test phải dùng một trong các cơ chế an toàn hiện có:

```text
Smtp__Enabled=false
File sink
Fake provider
Disposable/test database
```

Nếu cần test Google Drive, ưu tiên fake `IGoogleDriveStorageService` hoặc test credential/folder riêng. Không xóa hay ghi đè file thật.

---

# 3. Preflight

Trước khi sửa, ghi lại:

```text
Branch
HEAD
git status
WIP/stash count
Backend/Frontend đang chạy từ binary nào
Connection string/database hiện tại
Email provider mode
Google Drive config presence (không in secret)
Canonical SQL hash
Baseline build/test liên quan
```

Không log:

- Refresh token.
- Client secret.
- Access token.
- OTP raw code.
- Email confirmation token.
- Signed URL hoặc storage path nhạy cảm.

---

# 4. Reproduce ba lỗi trước khi sửa

## 4.1 Google Drive

Tái hiện flow:

```text
Visit Process
→ Gửi cập nhật chuẩn bị
→ Chọn ngôn ngữ
→ Backend tạo Báo cáo Lịch trình
→ Lưu report
→ Tạo draft
```

Thu thập:

```text
HTTP status
errorCode
response body
backend log
inner exception loại gì
Google OAuth response status
cấu hình nào đang thiếu/sai
```

Phân biệt rõ các trường hợp:

```text
GOOGLE_DRIVE_CONFIG_MISSING
GOOGLE_DRIVE_TOKEN_EXPIRED
GOOGLE_DRIVE_AUTH_FAILED
GOOGLE_DRIVE_NOT_CONNECTED
GOOGLE_DRIVE_FOLDER_NOT_FOUND_OR_NO_PERMISSION
GOOGLE_DRIVE_UPLOAD_FAILED
GOOGLE_DRIVE_FILE_NOT_FOUND
GOOGLE_DRIVE_FILE_FORBIDDEN
GOOGLE_DRIVE_UNAVAILABLE
```

Không kết luận “Google Drive lỗi” chung chung.

## 4.2 OTP rate-limit

Tái hiện bằng email test:

```text
Initial issue
Issue lại quá sớm
Đạt giới hạn standard/hour
Đạt giới hạn recovery/hour
Đạt absolute/hour
```

Ghi lại:

```text
HTTP 429
errorCode
Retry-After header
retryAfterSeconds
retryAt
UI message hiện tại
nút gửi có bị disable không
```

## 4.3 Missing draft

Tái hiện:

```text
Prepare setup-progress draft
→ nhận draftId
→ EmailComposeModal GET draftId
→ 404 EmailDraft not found
```

Tại đúng thời điểm lỗi, kiểm tra:

```sql
SELECT
    email_draft_id,
    status,
    created_by,
    related_type,
    related_id,
    email_template_id,
    created_at,
    updated_at
FROM email_drafts
WHERE email_draft_id = @draftId;
```

Đối chiếu:

```text
Database của prepare request
Database của GET draft request
Backend process/port nhận từng request
Transaction đã commit chưa
Draft có bị discard/delete không
DraftId có bị frontend giữ từ lần mở trước không
ReuseExistingDraft có trả stale id không
```

---

# 5. Root-cause checklist bắt buộc

## 5.1 Google Drive

Kiểm tra:

- `ClientId`, `ClientSecret`, `RefreshToken`, `RootFolderId`.
- Refresh token bị revoke/expired.
- OAuth app đang ở Testing và token có thời hạn ngắn.
- Railway/local backend có outbound network.
- Folder không tồn tại hoặc service account/user không có quyền.
- Upload thành công nhưng thiếu file ID.
- File row trong DB tồn tại nhưng bytes trên Drive đã mất.
- Mapping lỗi backend → frontend có làm mất `errorCode`.
- UI có hiển thị message chung dù backend trả lỗi cụ thể.

## 5.2 Draft lifecycle

Kiểm tra:

- Prepare tạo report trước, draft sau; lỗi giữa chừng để lại dữ liệu gì.
- Draft/recipient/attachment có cùng transaction không.
- Có trả response trước khi transaction commit không.
- `ReuseExistingDraft` có chọn draft đã mất attachment hoặc stale.
- Có cleanup job/test/manual discard xóa draft.
- `EmailComposeModal` reset state rồi hydrate có race không.
- Autosave có chạy trước khi hydrate xong không.
- `draftIdRef` có stale sau khi modal đóng/mở.
- Key của modal có thực sự remount đúng draft không.
- GET draft 404 có tiếp tục hiển thị body từ prepare response không.
- Khi GET thất bại, preview/send có bị chặn hoàn toàn không.
- Có request create/update mới âm thầm tạo draft khác sau 404 không.
- Draft list có refresh sau discard/send không.

## 5.3 OTP

Kiểm tra:

- Backend trả `Retry-After`.
- Frontend có đọc `retryAfterSeconds/retryAt`.
- Message đang hard-code tiếng Anh.
- Rate-limit theo email/purpose có đúng.
- Không vô tình khóa vĩnh viễn.
- Không tạo token mới khi request bị chặn.
- Concurrent issue có vượt quota không.
- UI double click có gửi nhiều request không.

---

# 6. Fix bắt buộc — Google Drive

## 6.1 Error contract

Backend phải giữ error code ổn định và message phân biệt được nguyên nhân.

Frontend phải map tối thiểu:

```text
CONFIG_MISSING
→ Google Drive chưa được cấu hình đầy đủ.

TOKEN_EXPIRED
→ Kết nối Google Drive đã hết hạn. Vui lòng kết nối lại.

AUTH_FAILED
→ Không thể xác thực với Google Drive. Vui lòng thử lại hoặc kiểm tra cấu hình.

FOLDER_NOT_FOUND_OR_NO_PERMISSION
→ Không tìm thấy thư mục lưu trữ hoặc tài khoản không có quyền truy cập.

UPLOAD_FAILED
→ Không thể tải báo cáo lên Google Drive.

FILE_NOT_FOUND
→ Tệp đã bị xóa hoặc không còn tồn tại.

FILE_FORBIDDEN
→ Không có quyền đọc tệp trên Google Drive.

UNAVAILABLE
→ Google Drive đang tạm thời không khả dụng.
```

Không trả mọi trường hợp thành:

```text
Không thể kết nối Google Drive. Vui lòng thử lại.
```

## 6.2 Setup-progress behavior

Nếu tạo report hoặc lưu report thất bại:

```text
Không tạo draft giả
Không mở composer với draftId không tồn tại
Không để orphan recipient/attachment
Không báo “đã tạo nháp”
```

Nếu report đã lưu nhưng draft persistence thất bại:

- Ghi log có correlation ID.
- Không log external file ID nhạy cảm nếu policy hiện tại cấm.
- Xác định và triển khai cleanup/compensation phù hợp với architecture hiện tại.
- Không để frontend hiểu nhầm rằng draft đã sẵn sàng.

## 6.3 Retry UX

Language modal cần:

```text
[Thử lại]
[Hủy]
```

Khi retry:

- Không nhân đôi draft/report.
- Không reuse stale response cũ.
- Disable các nút trong lúc request đang chạy.

---

# 7. Fix bắt buộc — EmailDraft not found

## 7.1 Backend consistency

Prepare endpoint chỉ được trả `draftId` sau khi:

```text
Draft row đã lưu
Recipients đã lưu
Mandatory attachment đã lưu
Transaction đã commit
Draft có thể GET lại bởi cùng user
```

Ưu tiên một transaction cho toàn bộ DB persistence của draft.

Storage upload không thể rollback cùng DB; cần xử lý thứ tự và compensation rõ ràng.

## 7.2 Reuse draft

`FindReusableAsync` phải chỉ trả draft:

```text
Status = DRAFT
Đúng owner
Đúng visit instance
Đúng template
Mandatory report attachment còn row
Bytes thực sự đọc được
```

Nếu draft không reusable:

```text
Không trả stale draftId
Tạo draft mới hoặc trả lỗi rõ ràng
```

Không reuse draft chỉ vì row draft còn tồn tại.

## 7.3 Frontend 404 handling

Nếu `getDraft(initialDraftId)` trả 404:

```text
Không hiển thị composer như một draft hợp lệ
Không cho preview
Không cho autosave
Không cho send
Không dùng initialBodyHtml để giả lập draft đã tải
```

Hiển thị trạng thái:

```text
Email nháp này không còn tồn tại hoặc đã bị xóa.
Dữ liệu trên hệ thống không bị thay đổi.
```

Actions:

```text
[Đóng]
[Tạo bản nháp mới]
```

`Tạo bản nháp mới` phải gọi lại prepare với:

```text
reuseExistingDraft = false
```

Không tự động tạo mới mà không cho người dùng biết.

## 7.4 Draft status errors

Phân biệt:

```text
404: draft không tồn tại
403: draft thuộc người khác
409: draft đã SENT/DISCARDED
422: draft corrupt/không đủ dữ liệu
```

Không dùng một message chung.

---

# 8. Fix bắt buộc — OTP rate-limit UX

Backend:

- Giữ `429 Too Many Requests`.
- Trả `errorCode`.
- Trả `retryAfterSeconds`.
- Trả `retryAt`.
- Gửi header `Retry-After`.
- Không tạo OTP mới khi blocked.

Frontend:

```text
RESEND_TOO_SOON
→ Bạn vừa yêu cầu mã. Vui lòng thử lại sau {n} giây.

STANDARD_RATE_LIMITED
→ Bạn đã yêu cầu quá nhiều mã trong một giờ. Có thể thử lại lúc {time}.

RECOVERY_RATE_LIMITED
→ Bạn đã dùng lượt khôi phục mã trong giờ này. Có thể thử lại lúc {time}.

ABSOLUTE_RATE_LIMITED
→ Tạm thời không thể cấp thêm mã xác thực. Có thể thử lại lúc {time}.
```

Yêu cầu:

- Không hiển thị tiếng Anh nếu UI đang ở tiếng Việt.
- Disable nút trong countdown.
- Reload trang vẫn phải dựa vào server response, không tin hoàn toàn countdown client.
- Double click chỉ tạo một request.
- Không đổi giới hạn bảo mật chỉ để hết lỗi khi test.

---

# 9. Audit toàn bộ các luồng email

Lập inventory tất cả send point hiện tại. Không chỉ search `SendAsync`; phải tìm:

```text
ISystemEmailDispatcher
IEmailDraftDispatcher
IManualEmailSender
IEmailService
ResendEmailService
ReportEmailSender
background hosted services
OTP/account confirmation flows
reply/forward
draft send
setup-progress send
logistics/invitation/reminder/report/invoice
```

Tạo bảng audit:

| Send point | Template/code | Trigger | Recipient source | Attachment | Mandatory? | Draft? | Provider path | Retry/idempotency | Tests | Risk |
|---|---|---|---|---|---|---|---|---|---|---|

Phải bao phủ tối thiểu:

1. Account email confirmation.
2. Resend account confirmation.
3. Password reset OTP.
4. Visit request OTP.
5. Contact claim/transfer.
6. Participant invitation.
7. Department/logistics request.
8. Reminder emails/background job.
9. Setup-progress email.
10. Schedule report/invoice/report emails.
11. Manual compose.
12. Reply.
13. Forward nếu có.
14. Draft reopen/autosave/send/discard.
15. Email có inline image.
16. Email có CC/BCC.
17. Email nhạy cảm không lưu body.
18. Email sent history/view detail.

---

# 10. Audit attachment — bắt buộc

## 10.1 Vấn đề cần xác minh

Generic attachment loader hiện có khả năng trả `null` cho file không đọc được rồi bỏ file khỏi outbound list.

Điều này có thể tạo trạng thái:

```text
Draft hiển thị có attachment
→ file bytes không đọc được
→ email vẫn gửi
→ người nhận không nhận attachment
→ UI vẫn báo gửi thành công
```

Phải xác minh bằng test thật trên code hiện tại.

## 10.2 Decision matrix

Phân loại attachment:

```text
MANDATORY
- Schedule report của setup-progress.
- Report/invoice mà nội dung email tuyên bố có đính kèm.
- Tài liệu nghiệp vụ bắt buộc theo flow.

OPTIONAL
- File người dùng tự thêm vào mail thủ công.
- Inline image trang trí, nếu nghiệp vụ cho phép bỏ.
```

Không tự giả định; đọc từng flow.

## 10.3 Behavior mục tiêu

Với `MANDATORY`:

```text
Row thiếu → chặn gửi
Bytes thiếu → chặn gửi
Forbidden → chặn gửi
Auth/storage unavailable → chặn gửi
Message chỉ rõ file và cách khắc phục
Không claim draft SENT
```

Với `OPTIONAL`, cần quyết định rõ một trong hai:

```text
A. Fail-closed: có attachment row nhưng không đọc được → chặn gửi.
B. Cho gửi thiếu file nhưng bắt buộc cảnh báo + xác nhận trước khi gửi.
```

Ưu tiên `A` trừ khi có business rule rõ ràng cho phép bỏ file.

Không được silently skip.

## 10.4 Không đổi schema nếu chưa cần

Ưu tiên xác định mandatory theo flow/command hiện có.

Chỉ đề xuất thêm cột DB nếu không thể biểu diễn đúng nghiệp vụ bằng contract hiện tại; nếu cần, dừng và báo trước khi đổi schema.

---

# 11. Audit render/content

Với mỗi send point, kiểm tra:

- Subject không rỗng.
- Body không rỗng.
- Không còn token dạng `{{...}}` sau render.
- `{{actionBlock}}`, `{{contactInformationBlock}}`, `{{setupSummaryBlock}}` đúng capability.
- Preview và send dùng cùng renderer/policy.
- VI/EN không trộn ngôn ngữ.
- HTML sanitize không phá table/button.
- Link one-time không xuất hiện trong history nếu policy cấm.
- Sensitive body không bị lưu.
- Reply-To đúng contact resolver.
- Contact block snapshot không thay đổi sau preview nếu flow yêu cầu snapshot.
- Nội dung nói “đính kèm” thì attachment thật phải tồn tại.

Renderer/send phải fail nếu còn placeholder hệ thống chưa xử lý.

---

# 12. Audit recipient

Kiểm tra:

- TO bắt buộc khi nghiệp vụ yêu cầu.
- CC/BCC giữ đúng group sau autosave/reopen.
- Không trùng email giữa TO/CC/BCC.
- Không vượt recipient limit.
- Không gửi token email qua CC/BCC nếu policy cấm.
- Unknown recipient type làm draft fail-closed.
- Recipient lấy từ DB đúng campus/instance.
- Không gửi cho participant chưa ACCEPTED nếu flow yêu cầu ACCEPTED.
- Không lấy nhầm host của campus khác.
- Email normalize case-insensitive.

---

# 13. Audit draft/autosave/concurrency

Test:

```text
Mở draft
Autosave
Đóng/mở lại
Hai tab cùng sửa
Draft bị discard trong lúc đang mở
Draft bị gửi ở tab khác
Double-click send
Network timeout sau provider accept
Provider reject
GET draft 404/403/409
Attachment upload đang chạy thì send
Hydrate chưa xong thì autosave
```

Yêu cầu:

- Không overwrite draft bằng form rỗng.
- Không gửi hai lần.
- Không biến draft lỗi thành draft mới âm thầm.
- Trạng thái DB và sent email có evidence rõ.
- Nếu provider accept nhưng app timeout, có idempotency/reconciliation phù hợp.

---

# 14. Audit provider/Resend

Kiểm tra:

- API key/config presence.
- Sender/from domain.
- Reply-To.
- Provider response ID có được lưu.
- HTTP 4xx/5xx mapping.
- Timeout/cancellation.
- Retry policy.
- Idempotency key.
- Trạng thái `SENT`, `FAILED`, `PENDING`.
- Không mark thành công chỉ vì DB insert thành công.
- Không mark draft SENT trước khi mọi validation bắt buộc hoàn tất.
- Có evidence khi provider reject.

Không cần gọi Resend thật. Dùng fake/file sink và contract tests.

---

# 15. Tests bắt buộc

## Backend unit

- Google Drive error mapping cho từng nhóm lỗi.
- Prepare không trả draftId nếu persistence chưa hoàn tất.
- Reuse không trả draft mất mandatory file.
- Missing draft trả đúng 404.
- Wrong owner trả 403.
- SENT/DISCARDED trả conflict phù hợp.
- OTP từng rate-limit trả đúng code/retry.
- Mandatory attachment unreadable chặn send.
- Generic attachment không silently skip.
- Placeholder còn sót chặn send.
- CC/BCC giữ đúng type.
- Double send chỉ một request claim được.
- Provider failure ghi trạng thái đúng.

## Integration

- Prepare → GET draft ngay lập tức thành công.
- Prepare lỗi Drive → không có draft/recipient/attachment orphan.
- Draft row tồn tại nhưng report bytes mất → không reuse.
- GET 404 → create fresh draft bằng explicit action.
- OTP 429 có `Retry-After`.
- Draft attachment mất → send bị chặn, draft còn DRAFT.
- Setup-progress report mất → send bị chặn.
- Manual draft/reply attachment mất → hành vi đúng decision matrix.
- Preview không còn placeholder.
- Background reminder failure có trạng thái/evidence.

## Frontend

- Drive error hiển thị đúng message/action.
- Retry không double-submit.
- Draft 404 không mở composer giả.
- `Tạo bản nháp mới` gọi prepare `reuseExistingDraft=false`.
- OTP countdown và localization.
- Nút OTP disabled đúng thời gian.
- Autosave không chạy khi hydrate fail.
- Send disabled khi attachment upload/chưa hợp lệ.
- CC/BCC giữ đúng sau reopen.
- Provider/storage error không bị nuốt.
- Không nối raw error code vào message.

---

# 16. Runtime smoke an toàn

Dùng HO/test accounts và outbound disabled.

Chạy tối thiểu:

```text
1. Account confirmation
2. Password reset OTP
3. Visit request OTP
4. Participant invitation
5. Logistics email
6. Reminder email
7. Manual compose không attachment
8. Manual compose có attachment
9. Reply
10. Setup-progress VI
11. Setup-progress EN
12. Draft reopen/autosave/send/discard
13. Report/invoice email
```

Với mỗi case ghi:

```text
HTTP
DB rows
Draft status
Sent email status
Recipient groups
Attachment count
Provider/file-sink artifact
No placeholder
No real outbound
```

---

# 17. Không được sửa ngoài scope

Không refactor toàn bộ email architecture nếu không cần.

Chỉ tạo abstraction mới khi có ít nhất hai send path đang lặp cùng lỗi và code hiện tại không có service dùng chung.

Ưu tiên tái sử dụng:

```text
IEmailDraftDispatcher
EmailDraftWriter
StoredFileProbe
IEmailTemplateRenderer
IEmailContactResolver
error contract hiện tại
```

Không tạo implementation thứ hai cho cùng validation.

---

# 18. Thứ tự triển khai

1. Preflight.
2. Reproduce ba lỗi.
3. Inventory send points.
4. Viết root-cause report ngắn.
5. Fix Google Drive error contract và retry UX.
6. Fix draft persistence/reuse/404 behavior.
7. Fix OTP localization + Retry-After/countdown.
8. Audit và fix attachment fail-closed.
9. Audit renderer/placeholders.
10. Audit recipients.
11. Audit provider/idempotency/background jobs.
12. Viết unit/integration/frontend tests.
13. Runtime smoke với outbound disabled.
14. Chạy full targeted gates.
15. Tạo commit theo nhóm logic, không push.
16. Báo cáo remaining debt.

---

# 19. Gợi ý chia commit

```text
fix(storage): classify Google Drive failures in email preparation
fix(email-drafts): make setup-progress draft lifecycle consistent
fix(otp): expose retry metadata and localize rate-limit UX
fix(email-attachments): prevent silent attachment loss during send
test(email): cover send-point audit and runtime regressions
docs(email): record audit matrix and remaining debt
```

Không bắt buộc đúng tên nếu code thay đổi thực tế khác, nhưng không gom toàn bộ thành một fat commit.

---

# 20. Definition of Done

```text
[ ] Ba lỗi được tái hiện trước khi sửa.
[ ] Có root cause bằng code/log/DB evidence.
[ ] Google Drive lỗi được phân loại đúng.
[ ] Setup-progress không trả draftId không thể GET lại.
[ ] Draft 404 không mở composer giả.
[ ] Có explicit action tạo draft mới.
[ ] OTP 429 có message tiếng Việt và thời gian thử lại.
[ ] Không đổi giới hạn OTP để che lỗi.
[ ] Không send path nào silently bỏ mandatory attachment.
[ ] Generic attachment behavior được quyết định và test.
[ ] Không email nào gửi còn placeholder hệ thống.
[ ] CC/BCC không bị đổi nhóm.
[ ] Double send không tạo hai email.
[ ] Provider failure phản ánh đúng trạng thái DB.
[ ] Audit đủ toàn bộ send points.
[ ] Backend build xanh.
[ ] Frontend typecheck/build xanh.
[ ] Unit/integration/frontend targeted xanh.
[ ] Runtime smoke đạt với outbound disabled.
[ ] Không fresh-import DB thật.
[ ] Không gửi email thật.
[ ] Không đổi schema nếu chưa được duyệt.
[ ] WIP/stash được giữ nguyên.
[ ] Không push.
```

---

# 21. Báo cáo cuối

Báo cáo theo format:

```text
ROOT CAUSE
- Google Drive
- OTP
- Missing draft
- Additional findings

SEND-POINT AUDIT
- Tổng số send point
- Phân nhóm
- Risk matrix

FILES CHANGED
- file: thay đổi gì

FIXES
- Storage
- Draft lifecycle
- OTP
- Attachments
- Renderer
- Recipients
- Provider/background

TESTS
- Backend build
- Unit
- Integration
- Frontend typecheck
- Frontend build
- Frontend targeted
- Runtime smoke

BEFORE / AFTER
- Error response
- DB state
- UI behavior

SAFETY
- SMTP/provider mode
- Database
- Schema
- WIP/stash
- Push status

COMMITS

REMAINING DEBT
```

Nếu có lỗi chưa thể sửa vì thiếu credential/runtime access, ghi rõ:

```text
Gate nào chưa chạy
Thiếu gì
Cách người dùng tự xác minh
Không được tự kết luận PASS
```
