# Giai đoạn 5 — Manual email: draft / compose / reply / history (Gate G5)

> Tài liệu này chỉ tuyên bố **Giai đoạn 5 / Gate G5**. Giai đoạn 6–10 vẫn còn nguyên.

## 1. Preflight

| | |
|---|---|
| Repository | `quangthoai04/PEMS` |
| Branch | `Canh-Iter1` |
| HEAD | `06c73b9491b7fb5afb88d20fc64de5ed9a56500c` |
| Canonical SQL | `docs/database/scripts/PEMS_FULL_V2_NO_SEED_DATA_GALLERY.sql` |
| SHA-256 | `51e178bb5e56fc927fd896e2a87ed8015043a2ca4904b4e1d9df581b2caae8a1` (không đổi) |
| Baseline vào G5 | build 0 error · unit 1410 · arch 14 · integration 880 · frontend sạch |

WIP Giai đoạn 4 còn nguyên; ba file deleted vẫn là xoá có chủ đích; `git diff --check` chỉ ra cảnh báo CRLF, không whitespace error, không conflict marker. Output duy nhất: `bin/claude-run/`.

## 2. Audit flow cũ

Đọc mã thật tại HEAD, không tin tên file trong kế hoạch. Kết quả theo từng flow:

| Flow | Route | Command/Query | Recipient model cũ | MIME cũ | DB cũ | Authorization cũ |
|---|---|---|---|---|---|---|
| Draft create | `POST /Emails/drafts` | `CreateEmailDraftCommandHandler` | có `recipientType`, **không validate** | — | ghi thẳng, bỏ qua entry rỗng | owner = current user ✓ |
| Draft update | `PUT /Emails/drafts/{id}` | `UpdateEmailDraftCommandHandler` | như trên | — | xoá hết + insert lại | owner ✓, status ✓ |
| Draft get | `GET /Emails/drafts/{id}` | `GetEmailDraftQueryHandler` | 1 list phẳng | — | — | owner ✓ |
| Draft discard | `PATCH …/discard` | `DiscardEmailDraftCommandHandler` | — | — | soft-discard ✓ | owner ✓ |
| **Draft send** | `POST …/send` | `SendEmailDraftCommandHandler` | đọc đúng 3 nhóm… | **N message, mỗi người nhận 1 MIME** | `DELIVERED`, `ex.Message`, transaction mở xuyên SMTP | owner ✓, **không chống double-send** |
| **Manual compose** | `POST /Emails/sendemail` | `SendEmailCommandHandler` | **chỉ có `To`** | **N message** | `recipient_type` hard-code `'TO'`, `DELIVERED`, `ex.Message` | **không check authenticated** |
| **Reply** | `POST /Emails/replytoemail` | `ReplytoEmailCommandHandler` | nhận CC/BCC… | **1 message chỉ tới TO** | ghi CC/BCC rows **rồi không gửi**; `DELIVERED` trên email gốc | **không có** |
| History list | `GET /Emails/viewemaillist` | `ViewEmailListQueryHandler` | — | — | — | sender/recipient ✓ |
| **History detail** | `GET /Emails/viewemail` | `ViewEmailQueryHandler` | — | — | — | **không có** |
| Visit-linked | `GET /delegations/…/sent-emails` | `GetVisitInstanceSentEmailsQueryHandler` | — | — | — | `VisitReminderAccess.CanView` ✓, **trả full BCC** |

### 2.1. Tám câu hỏi §V

1. **Draft nhận recipient dạng gì.** Danh sách có `recipientType`; CC/BCC **không bị bỏ ở layer nào** — chúng được lưu đúng nhóm. Cái thiếu là **validation**: không chống trùng, không chống trùng chéo nhóm, không chặn CR/LF, không giới hạn tổng.
2. **Create/Update.** Update xoá-rồi-insert (giữ đúng nhóm), có kiểm owner, **không** kiểm trùng, và bỏ qua entry rỗng im lặng.
3. **Get draft.** Trả một list phẳng (frontend tự gom), attachment đúng, **không** lộ draft người khác.
4. **Send draft.** **Không** biến mọi recipient thành TO trong DB — nhưng **gửi vòng lặp từng người**, nên thực tế mỗi người nhận một email riêng: CC không thấy ai khác, "carbon copy" không phân biệt được với gửi trực tiếp. Link `sent_email_id` đúng. Draft chuyển SENT cuối transaction. Provider fail → draft vẫn SENT, `PARTIAL_FAILED`.
5. **Manual send.** Một `sent_emails`, nhiều recipient rows — nhưng **tất cả `'TO'`** và **N provider message**.
6. **Reply.** Recipient mặc định = người gửi email gốc (đúng). **Không** copy TO/CC/BCC cũ (đúng). Nhưng CC/BCC **mới** được ghi DB mà **không được gửi**. Không giữ `In-Reply-To`/`References`. Không có `provider_message_id`. **Không kiểm quyền**.
7. **History.** `ViewEmailQueryHandler` bỏ hẳn filter sender/recipient (có comment giải thích là quyết định sản phẩm) → trả **toàn bộ BCC** cho bất kỳ ai trong 5 role. Search chỉ tìm subject/counterpart nên **không** dùng địa chỉ BCC làm dò được. Pagination/count không lộ số BCC.
8. **Authorization.** Controller có `RoleAuthorize` 5 role; handler **thiếu object scope** ở detail; HO/Admin không có super-role tường minh nhưng **được lợi từ việc không kiểm gì cả**.

## 3. Recipient contract

Dùng nguyên `EmailRecipientValidator` đã xây ở Giai đoạn 3 — không viết bộ luật thứ hai. Nay **mọi** đường gửi đi qua nó: system dispatcher (đã có), manual compose, draft (create/update/send), reply.

Đủ 11 rule §VI: normalize chung · so sánh không phân biệt hoa thường · cấm trùng trong nhóm · cấm trùng chéo TO/CC, TO/BCC, CC/BCC · bắt buộc ≥1 TO khi gửi · email hợp lệ · chặn CR/LF ở email + display name + subject · giới hạn tổng theo `Email:MaxRecipients` · **không** dedupe im lặng (ném lỗi có mã) · **không** hạ BCC thành TO · **không** lưu địa chỉ trong template.

Mã lỗi tái dùng, không tạo hierarchy mới: `EMAIL_RECIPIENT_REQUIRED` · `_INVALID` · `_DUPLICATE` · `_CROSS_GROUP_DUPLICATE` · `_LIMIT_EXCEEDED` · `EMAIL_HEADER_INVALID`.

**Bất đối xứng có chủ ý:** draft đang soạn được phép **chưa có TO** (`requireTo: false`) — người ta đang viết dở; lúc **gửi** thì bắt buộc. Mọi rule còn lại áp như nhau ở cả hai thời điểm.

## 4. Draft lifecycle

- **Create** — validate recipient + attachment **trước** khi insert; `sender` lấy từ current user, không từ request.
- **Update** — kiểm owner + trạng thái DRAFT; validate **trước mọi mutation** nên một update bị từ chối để lại draft y nguyên (có test).
- **Get** — trả thêm `to[]`, `cc[]`, `bcc[]` đã gom nhóm, **giữ nguyên** `recipients[]` cũ nên frontend hiện tại không vỡ. Chỉ chủ draft đọc được — không có ngoại lệ cho HO/Admin.
- **Discard** — kiểm owner, chỉ từ DRAFT, soft-discard.
- **Send** — xem Mục 5.

## 5. Manual send pipeline

Một lớp dùng chung: `ManualEmailSender`. Ba handler manual đều gọi nó.

1. Validate nội dung (`ManualEmailContent`) + envelope (`EmailRecipientValidator`) + attachment scope — **trước** khi ghi gì.
2. Ghi **một** `sent_emails` + N `sent_email_recipients` đúng `recipient_type` + attachment rows. `email_template_id = NULL`.
3. Gửi **một** MIME cho cả TO+CC+BCC.
4. Ghi lại kết quả thật.

`Message-Id` do PEMS sinh và đặt vào message thật, lưu ở `provider_message_id`; mọi recipient row của message chia sẻ đúng id đó.

**Transaction không bao SMTP.** History row commit trước, gửi sau, ghi kết quả sau nữa — nên một tiến trình chết giữa chừng để lại một dòng ghi `QUEUED`, đúng sự thật, thay vì rollback một message provider đã nhận.

**Nội dung do người soạn** đi qua `ManualEmailContent`: HTML uỷ quyền cho `SystemEmailContent.AuthoredByUser` (một nguồn luật duy nhất) — sanitize + chặn `PEMS_ACTION_BLOCK_*` + chặn `{{actionBlock}}` + giới hạn độ dài + chặn CR/LF trong subject. PLAIN_TEXT áp cùng các rule **trừ** HTML-sanitize: chạy sanitizer HTML lên plain text sẽ âm thầm nuốt ký tự `<` mà người viết có quyền dùng.

## 6. Reply behavior

- **Quyền trước đã.** Chỉ người có quyền đọc email gốc mới reply được: sender hoặc người nhận (kể cả BCC). Role **không** cấp quyền.
- **Người nhận** = người gửi email gốc. Không copy TO cũ, không copy CC cũ, **tuyệt đối không** copy BCC cũ.
- **CC/BCC mới của chính người trả lời** nay **được gửi thật** (trước chỉ được ghi DB).
- **Thread** — `In-Reply-To` + `References` trỏ vào `provider_message_id` thật của email cha; `provider_thread_id` kế thừa thread của cha. Cha không có id (dữ liệu cũ) → **không** gắn header nào, không bịa.
- **Không đụng email gốc.** Bỏ hẳn `originalEmail.DeliveredAt = now` — đánh dấu đã xử lý là hành động riêng (`mark-completed`), không phải hệ quả ngầm của việc trả lời.
- **Không** Reply All. **Không** tự copy attachment gốc.

## 7. Authorization matrix

Một rule dùng chung: `SentEmailAccess.Resolve` → `Relation` ∈ { `None`, `LinkedObject`, `BlindCopy`, `VisibleRecipient`, `Sender` }.

| Người xem | Mở được email? | Thấy BCC nào |
|---|---|---|
| A — người gửi | ✅ | **toàn bộ** |
| B — TO | ✅ | không |
| C — CC | ✅ | không |
| D — BCC | ✅ | **chỉ chính mình** |
| E — BCC khác | ✅ | **chỉ chính mình** |
| F — host của visit liên kết | ✅ (qua object scope) | không |
| G — không liên quan | ❌ 403 | — |
| H — HO | ❌ với email cá nhân; ✅ với email gắn visit HO xem được | không |
| I — Staff Leader | như H | không |

Object scope **đi mượn**, không tự chế: email gắn `VISIT_PARTICIPANT` / `LOGISTICS_ITEM` / `VISIT_INSTANCE` mở cho đúng những người `VisitReminderAccess.CanView` cho phép — cùng điều kiện màn VisitProcess vẫn dùng. Email `GENERAL` / `REPLY` **không có** object scope: thư từ cá nhân chỉ người trong cuộc đọc, dù cấp bậc nào.

Áp cho: detail (`ViewEmail`), list (`ViewEmailList` — vốn đã scope), visit-linked history, reply. Có test chứng minh **list và detail trả lời giống nhau** cho cả 8 người xem.

## 8. BCC privacy rules

- MIME: BCC **không** vào header nào. File `.eml` sinh ra từ pickup directory **không có header `Bcc`**; địa chỉ ẩn chỉ xuất hiện trong khối `X-Sender`/`X-Receiver` — phần envelope mà dịch vụ pickup đọc rồi cắt bỏ trước khi truyền, tương đương lệnh `RCPT TO` của một phiên SMTP thật. Có test khẳng định chính xác điều này, không khẳng định quá.
- API: lọc theo người xem (Mục 7).
- **Không** `bccCount`, `hasBcc`, `hiddenRecipientCount`, `recipientTotal` — có test quét property của DTO để giữ điều đó đúng về sau.
- Search bằng địa chỉ BCC **không** làm email hiện ra cho TO/CC/HO/người ngoài.
- Log: `EmailService` chỉ ghi metadata và che địa chỉ thành `***@domain`.
- Error: chỉ câu an toàn, không `ex.Message`.

## 9. Attachment behavior

Draft giữ nguyên attachment qua round-trip; chỉ file **do chính người soạn tải lên** được đính (kiểm cả lúc lưu lẫn lúc gửi, vì file có thể đổi giữa hai thời điểm); chặn phần mở rộng thực thi; giới hạn 25 MB; `INLINE_IMAGE` bắt buộc có `content_id` duy nhất và phải là ảnh. `sent_email_attachments` khớp MIME; inline thành linked resource `cid:`, còn lại thành attachment thường.

**Chưa đóng:** `GET /api/files/{id}/download` mới chỉ yêu cầu đăng nhập, chưa ràng buộc theo quyền xem email chứa file. Xem Mục 12.

## 10. Transaction và failure windows

Không tuyên bố SMTP và DB atomic. Các cửa sổ còn lại, ghi rõ:

| # | Cửa sổ | Hệ quả | Vì sao chấp nhận |
|---|---|---|---|
| 1 | Draft đã lưu, chưa gửi | không có | trạng thái bình thường |
| 2 | `sent_emails` đã ghi, provider lỗi | dòng `FAILED` + câu an toàn | đúng sự thật |
| 3 | Provider nhận, cập nhật DB lỗi | dòng kẹt `QUEUED` dù đã gửi | thà báo thiếu còn hơn báo thừa |
| 4 | Claim draft xong, chết trước khi insert message | draft `SENT` không có message | hẹp, và đổi lại loại bỏ double-send |
| 5 | Bấm gửi draft hai lần | chỉ một lần gửi | claim bằng một `UPDATE … WHERE status='DRAFT'` |
| 6 | Bấm reply hai lần | **hai reply** | reply không có bản ghi nháp để claim — xem Mục 12 |
| 7 | Attachment lưu xong, prepare fail | không có message | validate chạy trước mọi ghi |
| 8 | Rollback sau khi provider nhận | không xảy ra | transaction không bao SMTP |

Idempotency: draft đã SENT không gửi lại (không có luồng resend tường minh); hai request song song chỉ một thắng; **không** tự retry; `retry_count` giữ 0.

Status: `QUEUED → SENT` | `QUEUED → FAILED` | Skipped **ở lại** `QUEUED`. Không bao giờ `DELIVERED`, `delivered_at` luôn NULL.

## 11. Bằng chứng test

**Unit (+51 → 1461):**
- `SentEmailAccessTests` — ma trận A–I, hoa/thường, type lạ đọc là TO, chưa đăng nhập, email hệ thống không sender.
- `ManualEmailContentTests` — subject rỗng/quá dài/CR-LF, body rỗng/quá dài/sanitize còn rỗng, forge marker (HTML + plain text), `{{actionBlock}}`, plain text giữ nguyên `<`.
- `EmailDraftRecipientContractTests` — tách nhóm, thiếu type đọc là TO, type lạ bị từ chối, chưa có TO khi soạn / bắt buộc khi gửi, trùng trong từng nhóm TO·CC·BCC, trùng chéo cả 3 cặp, sai định dạng, CR/LF display name, vượt ceiling, round-trip giữ thứ tự + hoa thường.
- `EmailRecipientValidatorTests` (đã có từ Giai đoạn 3) phủ trọn §XIV.A.

**Integration (+32 → 912), MySQL thật + MIME thật:**
- `ManualEmailPipelineTests` (18) — draft round-trip 3 nhóm · update thay đúng envelope · update bị từ chối không đụng dữ liệu cũ · draft là của riêng chủ nó (4 thao tác đều 403) · **5 người nhận = 1 message + 1 history row** · header To/Cc đúng, BCC không ở đâu trong message · một `provider_message_id` dùng chung và đúng bằng `Message-Id` thật · snapshot khớp MIME + `email_template_id` NULL · SENT không kèm `delivered_at`, `retry_count` 0 · FAILED kèm câu an toàn, không lộ host/exception · **hai lần gửi song song → đúng 1 message** · draft đã gửi không gửi lại · compose 3 nhóm 1 message · compose thiếu TO không ghi gì · reply gửi CC/BCC mới thật và không mang BCC cũ · reply giữ `In-Reply-To`/`References`/thread và **không sửa email gốc** · người ngoài không reply được · reply nội dung xấu bị chặn trước khi ghi.
- `SentEmailHistoryAuthorizationTests` (14) — A thấy cả D và E · B/C không thấy BCC · D chỉ thấy D, E chỉ thấy E · F đọc email gắn visit nhưng không thấy BCC · object scope không với tới email `GENERAL` · G/H/I bị từ chối · HO và Staff Leader vào qua visit vẫn không thấy BCC · **list và detail nhất quán cho cả 8 người xem** · search bằng địa chỉ BCC không lộ email cho ai khác · DTO không có field count nào phản bội BCC · id không tồn tại trả NotFound.

**Regression G4** — chạy lại **toàn bộ** suite, không chỉ phần email: template contract 26 mã, dispatcher boundary, account/auth/logistics/reminder/report E2E, authored content, MIME envelope, G4 closure — tất cả xanh, không sửa một assertion nào.

| Suite | G4 closure | G5 | Δ |
|---|---|---|---|
| Backend build | 0 error | **0 error** | — |
| Unit | 1410 | **1461** | +51 |
| Architecture | 14 | **14** | — |
| Integration | 880 | **912** | +32 |
| Frontend `tsc --noEmit` + build | sạch | **sạch** | — |
| Canonical SHA-256 | `51e178bb…aae8a1` | **không đổi** | — |

## 12. Static scan

| Từ khoá | Trước | Sau | Phân loại |
|---|---|---|---|
| `DeliveryStatus = "DELIVERED"` | 2 (SendEmail, SendEmailDraft) | **0** | production violation → đã sửa |
| `ErrorMessage = ex.Message` | 3 (SendEmail, SendEmailDraft, Reply) | **0** | production violation → đã sửa |
| `RecipientType = "TO"` hard-code | 2 | **0** | production violation → đã sửa |
| Gửi vòng lặp mỗi recipient 1 MIME | 2 | **0** | production violation → đã sửa |
| `IEmailService.SendAsync(string,…)` (legacy 1 địa chỉ) | 2 | **0** | production violation → đã sửa |
| `ToEmail = ` (shim) trong production | 1 | **0** | production violation → đã sửa |
| Reply ghi CC/BCC rồi không gửi | 1 | **0** | production violation → đã sửa |
| Reply sửa `delivered_at` email gốc | 1 | **0** | production violation → đã sửa |
| Query sent email thiếu authorization | 1 (`ViewEmail`) | **0** | production violation → đã sửa |
| BCC trả nguyên trong DTO | 2 (`ViewEmail`, visit-linked) | **0** | production violation → đã sửa |
| `bccCount` / `hasBcc` / `recipientTotal` | 0 | 0 | — |
| `AllowAnonymous` ở `EmailsController`/`FilesController` | 0 | 0 | — |
| `EmailTemplateId` giả cho email manual | 1 (draft mang template id sang) | **0** | production violation → đã sửa |
| `RetryCount` tăng lần đầu | 0 | 0 | — |
| `Bcc` header trong message truyền đi | 0 | 0 | `X-Receiver` là envelope pickup, không phải header — xem Mục 8 |
| Attachment download không kiểm quyền email | 1 | **1** | **còn lại có chủ ý** — xem dưới |
| `DELIVERED` trong `NewEnums.cs` | 1 | 1 | hằng miền, không phải nơi ghi |

## 13. Những thứ không thuộc G5

- **Frontend TO/CC/BCC UI, autosave/compose redesign, màn cấu hình template** — Giai đoạn 6. Backend đã nhận đủ ba nhóm; `EmailComposeModal` hiện chỉ gửi TO, và đó vẫn là một envelope hợp lệ.
- **Reply All** — không làm, theo yêu cầu.
- **Sync script 7b, E2E tổng, deployment, inbox thật, campaign/bulk** — ngoài phạm vi.
- **`GET /api/files/{id}/download` chưa ràng theo quyền xem email.** Đây là lỗ hổng thật và đã đo được: một người đăng nhập bất kỳ tải được file bất kỳ theo `file_id`. Không sửa trong lượt này vì `files` phục vụ **toàn hệ thống** (avatar, ảnh gallery, tài liệu visit, báo cáo…), nên rule quyền phải là rule của `files` chứ không phải của email; đặt một điều kiện riêng cho email vào đó sẽ tạo đúng loại "mỗi surface một điều kiện" mà Giai đoạn 5 vừa dọn ở history. Cần một lượt riêng cho `files`.
- **`mark-completed` dùng `delivered_at` làm cờ "đã xử lý".** Lạm dụng cột, nhưng là hợp đồng sẵn có của một use case khác; G5 chỉ gỡ việc reply **âm thầm** set nó, không thiết kế lại use case đó.
- **403 vs 404 vẫn lộ sự tồn tại của id.** Giữ đúng quy ước sẵn có của repo (NotFound khi không có, Forbidden khi không phải của mình — như draft vẫn làm). Đổi sang 404-cho-tất-cả là một quyết định toàn hệ thống, không phải của riêng email.
- **Reply bấm hai lần tạo hai reply.** Draft có bản ghi để claim; reply thì không. Chống trùng cần một khoá idempotency ở tầng request — thuộc Giai đoạn 6 (frontend) hoặc một quyết định riêng.

## 13b. Security closure — file attachment authorization

> Đóng lỗ hổng đã ghi ở Mục 13. Thực hiện sau khi G5 đạt, cùng branch, không đổi canonical SQL.

### 13b.1. Lỗ hổng cũ và đường khai thác

`GET /api/files/{id}/download` (và `/content`) chỉ kiểm **đã đăng nhập**, rồi mở stream. `file_id` là số nguyên tuần tự và **mọi module dùng chung một bảng `files`**, nên id trở thành chìa khoá vạn năng:

1. Người dùng nội bộ bất kỳ đoán/duyệt `file_id` → tải được **attachment của email họ không được xem** — đi vòng qua toàn bộ `SentEmailAccess` vừa dựng ở G5.
2. **Draft chưa gửi của người khác** đọc được (nội dung đang soạn, chưa ai duyệt).
3. **Hoá đơn/báo cáo** gửi cho campus khác đọc được.
4. Ảnh chuyến thăm, tài liệu đối tác, file logistics — không ràng buộc scope nào.

Không cần biết `sent_email_id`, không cần vượt qua màn hình nào: chỉ cần một số.

### 13b.2. Ma trận reference đã audit

Quét canonical SQL: **22 cột `file_id` trên 15 bảng**. Không dùng tên file, phần mở rộng hay đường dẫn để suy quyền — chỉ dùng reference.

| Reference | Đối tượng cha | Rule áp dụng | Nguồn rule |
|---|---|---|---|
| `sent_email_attachments.file_id` | SentEmail | `SentEmailAccess` + linked-object scope | **tái dùng G5** |
| `email_draft_attachments.file_id` | EmailDraft | chủ draft, không ngoại lệ | draft ownership |
| `visit_photos` · `photo_face_tags` · `visit_photo_face_scans` | visit instance | `VisitInstanceAccess.CanViewInternal` | **tái dùng module visit** |
| `visit_logistics_item_handovers.attachment_file_id` | logistics item → instance | như trên | như trên |
| gallery (areas/locations/item media/thumbnail/audio) | gallery | `PublicGalleryMediaAccess` nếu public; nội bộ nếu chưa | **tái dùng module gallery** |
| `news.cover_file_id` · `news_section_files.file_id` | News | PUBLISHED → công khai; còn lại → nội bộ | **tái dùng rule public news** |
| `partners.*` · `partner_contacts.*` | Partner | nội bộ (module là `[Authorize]` không gate thêm) | mirror module |
| `documents.file_id` | Document | nội bộ (module không gate thêm) | mirror module |
| `business_card_ocr_jobs.scanned_card_file_id` | OCR job | nội bộ | mirror module |
| `USER_AVATAR` (không FK) | user | nội bộ | ảnh hiện cạnh tên khắp sản phẩm |
| **không reference nào** | — | **chỉ người upload** | mặc định đóng |

Trả lời 7 câu hỏi §IV: (1) **có**, một file có thể nhiều reference; (2) file **không** có owner nghiệp vụ riêng — `uploaded_by` chỉ là vết upload, owner thật đến từ đối tượng cha; (3) `file_purpose` **không** đủ tin cậy để quyết định quyền (là nhãn kỹ thuật lúc upload) nên không dùng một mình, trừ `USER_AVATAR` vốn không có bảng cha; (4) **có** file public thật, nhưng đã có **route riêng** (`/api/public/news-files`, `/api/public/partners/media`, `/api/public/visit-fptu/media`) tự kiểm cha đã PUBLISHED — route authenticated không cần và không được phục vụ ẩn danh; (5) file không reference → chỉ uploader; (6) **có**, để người đang soạn dở xem lại file mình vừa tải lên; (7) **có** khả năng tái sử dụng id chéo module — chính vì thế resolver duyệt **mọi** reference chứ không đoán theo purpose.

### 13b.3. Kiến trúc

`IFileAccessAuthorizationService.CanDownloadAsync(file, ct)` — một chỗ duy nhất:

1. Nhận file + current user.
2. Duyệt các reference (public → nội bộ → email → draft → visit).
3. Gọi **rule của chính module đó**, không tự phán.
4. Không reference nào → chỉ uploader.

Chạy **trước** khi resolve đường dẫn vật lý và trước khi mở stream. Controller vẫn chỉ: nhận id → gọi mediator → trả `FileResult`.

**Quyết định đa-reference (G5-13):** đủ **một** reference hợp lệ là được tải. Cùng một ảnh vừa là attachment email vừa là cover của bài news đã PUBLISHED thì nó đã công khai với cả thế giới — từ chối nó với một đồng nghiệp đã đăng nhập không bảo vệ được gì. Chiều ngược lại (bắt **mọi** reference cùng cho phép) khiến file càng dùng lại càng khó đọc, không ai đoán được từ giao diện.

**Không có master key.** Không nhánh nào xét HO/Admin. Cấp bậc tới được file theo đúng đường mọi người đi: có quyền với thứ tham chiếu nó.

### 13b.4. Storage safety

`object_key` do server sinh (GUID dưới thư mục purpose/ngày), client chỉ gửi số. Vẫn thêm kiểm **containment**: đường dẫn resolve xong phải nằm dưới storage root, so sánh có dấu phân cách cuối để `uploads-old` không bị nhận nhầm là con của `uploads`. Khoá escape → coi như file không tồn tại, không mở, không vọng lại đường dẫn.

Từ chối **không** kèm: tên file, dung lượng, MIME, chủ sở hữu, đối tượng liên quan, người nhận ẩn, hay nội dung. Response 403 mang envelope lỗi chuẩn, `Content-Type` không phải `application/pdf`, không có `Content-Disposition`.

### 13b.5. Bằng chứng test (+21)

`FileDownloadAuthorizationTests` (14, MySQL thật + file thật trên đĩa): ma trận email A–I (A/B/C/D/E tải được · G/H/I bị từ chối · host của visit khác không tải được) · linked-object cấp attachment **đúng khi** cấp email · từ chối không lộ gì · draft chỉ chủ nó (colleague/HO/StaffLeader đều 403) · gửi draft cấp quyền cho người nhận **mà không** nới quyền draft · IDOR duyệt id không ra bytes lẫn metadata · file chưa reference chỉ uploader · **upload rồi mất quyền khi file thuộc về email mình không tham gia** · ảnh visit theo đúng rule participant (INVITED không, ACCEPTED có) · partner/document vẫn đọc được đúng như màn hình của module · object key escape root không bao giờ mở · file mất trên đĩa lỗi ổn định, không lộ root.

`FileDownloadRouteTests` (7, HTTP thật): anonymous → 401 · sender/recipient → 200 + bytes · người lạ → 403 trên **cả hai** route và **không** trả PDF · HO → 403 · từ chối không lộ chi tiết · duyệt id chỉ ra 403/404 · `Content-Disposition` mã hoá an toàn, không CR/LF.

### 13b.6. Rủi ro còn lại

- **Mirror không phải siết.** Với partner / documents / news-nháp / gallery-nháp / avatar, rule là "người dùng nội bộ bất kỳ" — **đúng bằng** rule màn hình của chính module đó. File không còn lỏng hơn màn hình, nhưng cũng không chặt hơn. Siết thêm là quyết định của module sở hữu màn hình, không phải của tầng file; làm ở đây sẽ khiến file và màn hình bất đồng.
- **`DocumentsController` không có `[Authorize]`** ở cấp framework (phát hiện khi audit). Không thuộc phạm vi lượt này và **không** được nới qua đường file — route file vẫn bắt buộc đăng nhập. Cần một lượt riêng cho module Documents.
- **403 vẫn phân biệt với 404**, giữ quy ước sẵn có của repo.
- Log cảnh báo "file missing on disk" vẫn ghi `object_key`. Là log vận hành khi thiếu file, không phải log từ chối quyền; giữ nguyên để không đổi storage layout.

## 14. Gate checklist

- [x] Draft round-trip đúng TO/CC/BCC
- [x] Draft attachment không mất
- [x] Manual send một MIME
- [x] DB recipient type khớp MIME
- [x] Reply gửi CC/BCC mới
- [x] BCC cũ không copy
- [x] Thread metadata đúng
- [x] Sender thấy toàn bộ BCC
- [x] TO/CC không thấy BCC
- [x] BCC chỉ thấy chính mình
- [x] Người không liên quan bị từ chối
- [x] HO/Admin không có quyền BCC ngầm
- [x] List/search/detail/visit-linked cùng rule
- [x] Không metadata gián tiếp làm lộ BCC
- [x] Full gate pass
- [x] Canonical hash không đổi

## 15. Kết luận

**Gate G5 — ĐẠT** cho Giai đoạn 5 (Manual email: draft / compose / reply / history + authorization + BCC).

Điểm tiếp theo: **Giai đoạn 6 — Frontend TO/CC/BCC và Cấu hình Email Template.**
