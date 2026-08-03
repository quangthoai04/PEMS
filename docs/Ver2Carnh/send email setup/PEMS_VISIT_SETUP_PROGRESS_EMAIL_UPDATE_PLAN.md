# Kế hoạch cập nhật trang xử lý đơn và email tiến trình chuẩn bị PEMS

## 1. Phạm vi và baseline đã kiểm tra

- Repository: `quangthoai04/PEMS`.
- Nhánh kiểm tra: `Cảnh-Iter1`.
- HEAD tại thời điểm lập kế hoạch: `b2898ac395d3558013e0b2e552401e587c129035`.
- `Cảnh-Iter1` và `Dev` đang cùng HEAD tại thời điểm kiểm tra.
- Trang mục tiêu: `/dashboard/visit/process/{visitInstanceId}`.
- File frontend chính: `frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx`.

Các nền tảng hiện có cần tái sử dụng:

- `EmailComposeModal.tsx`: đã có nháp, autosave, TO/CC/BCC, chip người nhận, kiểm tra trùng, giới hạn người nhận, HTML editor, tệp đính kèm, xem trước và gửi.
- `email_drafts`, `email_draft_recipients`, `email_draft_attachments`: đã đủ để lưu bản nháp theo ba nhóm người nhận và tệp.
- `ManualEmailSender`/`ManualEmailContent`: đã có pipeline kiểm tra và gửi nội dung do người dùng sửa.
- `SystemEmailTemplates`, `email-template-defaults.json`, `email_templates`: đã có cơ chế registry–default–SQL parity.
- `ScheduleReportDataBuilder`, `ScheduleReportPdfRenderer`, `ExportScheduleReportPdfQueryHandler`: đã tạo được “Báo cáo Lịch trình” VI/EN từ dữ liệu thật.
- Báo cáo hiện tại đã chỉ đưa người tham gia nội bộ có trạng thái `ACCEPTED` vào danh sách phía FPT.

## 2. Kết luận thiết kế

Thay đổi nên gồm hai lát chức năng độc lập nhưng được giao cùng một đợt:

1. Mặc định mở sẵn bốn khối trên tab “Trước tiếp khách”.
2. Thêm luồng “Gửi cập nhật chuẩn bị” dành cho Host, dùng template riêng, tự gắn Báo cáo Lịch trình và mở bộ soạn mail hiện có để Host kiểm tra/sửa TO–CC–BCC, tiêu đề, nội dung và xem trước trước khi gửi.

Không tái sử dụng `VISIT_PARTICIPANT_INVITATION` cho luồng mới. Template mời có liên kết hành động dùng một lần, thuộc loại nhạy cảm, bắt buộc một TO và cấm CC/BCC. Luồng mới phải dùng một template không token, cho phép danh sách người nhận do Host kiểm soát.

## 3. Business rules chốt cho luồng mới

### 3.1. Trạng thái mở/đóng giao diện

Khi tải trang hoặc mỗi lần người dùng quay lại tab “Trước tiếp khách”, mặc định:

- `1. Thông tin chung`: mở.
- `Thông tin người tạo`: mở.
- `Thông tin đoàn khách`: mở.
- `Thiết lập & Điều phối sự kiện (Set up)`: mở.
- `2. Chuẩn bị chi tiết`: mở.

Người dùng vẫn được bấm thu gọn/mở lại từng khối. Yêu cầu chỉ thay đổi trạng thái mặc định, không xóa khả năng collapse.

Thay đổi trực tiếp cần thực hiện trong `VisitProcess.tsx`:

```ts
isInfoExpanded = true
isSetupExpanded = true
isInfoSection1Expanded = true
isInfoSection2Expanded = true
isInfoSection3Expanded = true
```

Effect chạy khi `activeTab === 'before'` cũng phải đặt lại cả năm trạng thái trên thành `true`. Xóa/sửa comment hiện tại đang mô tả việc đóng mục 1, mục 2 và “Chuẩn bị chi tiết”.

### 3.2. Ai được gửi

Chỉ Host chính hiện tại của đúng `visit_instance` được gửi email tiến trình chuẩn bị.

Điều kiện backend:

- Người dùng đã đăng nhập.
- `visitRequestId` và `visitInstanceId` khớp cùng một bản ghi.
- `current_host_user_id == currentUserId`.
- Chuyến chưa hủy/đóng.
- Chuyến còn trong cửa sổ chuẩn bị mà business rule hiện tại cho phép Host sửa tab Before Visit.

Không tự cho HO hoặc Staff Leader gửi thay Host chỉ vì họ xem được trang/báo cáo. Nếu sau này cần ủy quyền gửi thay, phải bổ sung rule riêng.

Frontend không tự suy quyền bằng role. Bổ sung cờ backend `canSendSetupProgressEmail` vào permission DTO và chỉ hiện nút khi cờ là `true`.

### 3.3. Danh sách người nhận mặc định

Danh sách phải được backend tính tại thời điểm tạo bản nháp; frontend không tự ghép từ dữ liệu đang hiển thị.

Quy tắc đề xuất:

- TO — phía khách:
  - đầu mối liên hệ hiện tại: `contactPersonEmail` + `contactPersonFullName`;
  - người đăng ký: `registrantEmail` + `registrantName`, nếu khác đầu mối.
- CC — phía FPT:
  - các `visit_participants` của đúng instance có `status = ACCEPTED`;
  - loại Host chính khỏi danh sách vì Host là người gửi;
  - loại `INVITED`, `DECLINED`, `REMOVED` và `ASSIGNED` chưa xác nhận.
- BCC: để trống mặc định.

Fallback:

- Nếu không có email phía khách nhưng có người tham gia `ACCEPTED`, đưa người tham gia đầu tiên vào TO và phần còn lại vào CC để bản nháp có TO hợp lệ.
- Nếu không có bất kỳ email hợp lệ nào, vẫn có thể mở composer nhưng phải hiện cảnh báo và khóa xem trước/gửi cho tới khi Host nhập ít nhất một TO.

Chuẩn hóa và khử trùng:

- `trim` + so sánh không phân biệt hoa thường.
- Một email chỉ xuất hiện một lần trên toàn bộ TO/CC/BCC.
- Ưu tiên giữ ở TO nếu cùng email vừa là người đăng ký/đầu mối vừa là participant.
- Không tự bịa địa chỉ email và không tạo địa chỉ từ tên.

Giới hạn dữ liệu hiện tại cần nói rõ trong UI và test:

- `visit_guest_members` chỉ lưu tên, tổ chức, chức danh, quốc tịch; không có cột email.
- Vì vậy không thể tự chọn email cho mọi tên trong “Danh sách khách”. Trong phạm vi này, “khách có email” là người đăng ký và đầu mối liên hệ.
- Không mở rộng schema guest member trong change này. Nếu muốn gửi tới từng khách lẻ, cần một change riêng bổ sung email vào form công khai, DB, DTO, validation, edit/history và seed.

### 3.4. Host được phép chỉnh gì

Trong composer chuyên dụng, Host được:

- bỏ các người nhận mặc định;
- thêm địa chỉ bất kỳ vào TO, CC hoặc BCC;
- chuyển người nhận giữa TO/CC/BCC;
- sửa tiêu đề;
- sửa nội dung HTML;
- thêm tệp đính kèm khác;
- xem trước toàn bộ TO/CC/BCC, nội dung và danh sách tệp;
- quay lại sửa trước khi xác nhận gửi.

Host không được:

- đổi sang template nhạy cảm/template khác trong chính luồng này;
- xóa tệp Báo cáo Lịch trình bắt buộc;
- gửi khi không còn là Host hoặc chuyến đã rời cửa sổ chuẩn bị;
- gửi payload có email sai, trùng giữa các nhóm hoặc vượt giới hạn server.

### 3.5. Ngôn ngữ

- Template và Báo cáo Lịch trình đều có bản VI/EN.
- Mặc định `vi`.
- Trước khi tạo draft, Host có thể chọn `Tiếng Việt` hoặc `English`.
- Template và PDF phải dùng cùng một `languageCode`.
- Nếu đã sửa nội dung rồi đổi ngôn ngữ, UI phải cảnh báo vì render lại template sẽ ghi đè nội dung đang sửa.

### 3.6. Tệp Báo cáo Lịch trình

- PDF là bắt buộc và được tạo từ dữ liệu mới nhất tại thời điểm chuẩn bị draft.
- PDF phải dùng lại `ScheduleReportDataBuilder` + `ScheduleReportPdfRenderer`; không tải PDF xuống trình duyệt rồi upload ngược trở lại.
- Tệp được lưu bằng pipeline file hiện có, liên kết vào `email_draft_attachments`, sau khi gửi liên kết tiếp sang `sent_email_attachments`.
- Composer hiển thị tệp này là “Báo cáo Lịch trình — bắt buộc”, không có nút xóa.
- Hiển thị thời điểm tạo snapshot và nút “Tạo lại báo cáo từ dữ liệu mới nhất”. Tạo lại chỉ thay tệp PDF bắt buộc, không ghi đè TO/CC/BCC, subject hoặc body đã sửa.
- Nếu không tạo/đọc được PDF, không gửi email không có báo cáo; draft phải được giữ để Host thử lại.

## 4. Trải nghiệm người dùng đề xuất

### 4.1. Vị trí nút

Ở cuối tab “Trước tiếp khách”, phía trên thanh “Xác nhận hoàn thành chuẩn bị”, đặt cùng một action row:

- `Báo cáo Lịch trình` — hành vi xem trước/tải xuống hiện tại.
- `Gửi cập nhật chuẩn bị` — nút primary có icon email, chỉ hiện khi `canSendSetupProgressEmail=true`.

Không đặt nút gửi bên trong một panel đang collapse để tránh biến mất khi Host thu gọn phần setup.

### 4.2. Luồng thao tác

1. Host bấm `Gửi cập nhật chuẩn bị`.
2. Chọn ngôn ngữ VI/EN; mặc định VI.
3. Frontend gọi endpoint chuẩn bị draft.
4. Backend xác thực Host, render template, tính người nhận, tạo PDF, lưu draft + attachment.
5. Mở `EmailComposeModal` với `initialDraftId`.
6. Composer hiển thị sẵn:
   - TO khách;
   - CC participant đã `ACCEPTED`;
   - BCC trống;
   - subject/body đã render;
   - PDF bắt buộc;
   - template bị khóa nhưng nội dung vẫn sửa được.
7. Host thêm/bỏ/chuyển người nhận, sửa nội dung, thêm tệp khác.
8. Host bấm `Xem trước`.
9. Màn hình preview hiển thị TO/CC/BCC, subject, HTML và toàn bộ tệp.
10. Host xác nhận gửi.
11. Backend kiểm tra lại quyền và trạng thái chuyến, gửi qua pipeline hiện có, lưu history và trả kết quả.

Nếu có draft chưa gửi của chính Host cho cùng instance, endpoint nên mở lại draft đó thay vì tạo thêm draft/file trùng. UI hiển thị “Đang mở bản nháp tạo lúc …” và cho phép Host chủ động tạo bản cập nhật mới.

## 5. Thiết kế API/DTO

### 5.1. Permission

Mở rộng response của process-permissions:

```json
{
  "canSendSetupProgressEmail": true
}
```

Nguồn sự thật là backend; FE chỉ render theo cờ này.

### 5.2. Chuẩn bị hoặc mở lại draft

```http
POST /api/delegations/{visitRequestId}/campuses/{visitInstanceId}/setup-progress-email/draft
```

Request:

```json
{
  "languageCode": "vi",
  "reuseExistingDraft": true
}
```

Response tối thiểu:

```json
{
  "draftId": 123,
  "reusedExistingDraft": false,
  "languageCode": "vi",
  "reportFileId": 456,
  "reportFileName": "PEMS_Schedule_Report_...pdf",
  "reportGeneratedAt": "2026-08-01T14:30:00",
  "warnings": []
}
```

### 5.3. Làm mới PDF trong draft

```http
POST /api/delegations/{visitRequestId}/campuses/{visitInstanceId}/setup-progress-email/drafts/{draftId}/refresh-report
```

- Kiểm tra owner draft + Host hiện tại + trạng thái chuyến.
- Thay đúng attachment PDF bắt buộc.
- Giữ nguyên người nhận và nội dung draft.

### 5.4. Gửi chuyên dụng

```http
POST /api/delegations/{visitRequestId}/campuses/{visitInstanceId}/setup-progress-email/drafts/{draftId}/send
```

Endpoint này phải kiểm tra lại quyền ở thời điểm gửi. Không gọi thẳng generic `POST /api/Emails/drafts/{id}/send` từ trang VisitProcess vì Host có thể đã bị chuyển, chuyến có thể đã hủy hoặc đã sang giai đoạn khác sau lúc draft được tạo.

Nên trích logic gửi draft hiện tại thành service dùng chung:

- generic email send handler gọi service sau owner/status guard hiện có;
- setup-progress send handler gọi service sau visit/host/template/attachment guard;
- không copy nguyên handler thành hai bản.

## 6. Backend implementation plan

### 6.1. Tách service tạo báo cáo

Tách phần build/translate/render khỏi `ExportScheduleReportPdfQueryHandler` thành service dùng chung, ví dụ:

```text
IScheduleReportArtifactService
  RenderAsync(instance, languageCode)
  StoreAsync(bytes, filename, instance, actor)
```

`ExportScheduleReportPdfQueryHandler` vẫn giữ hành vi tải báo cáo hiện tại. Luồng email gọi service chung để tạo đúng một artifact server-side và liên kết cùng file vào document/email attachment.

Không để việc FE preview/toggle ngôn ngữ tạo các bản lưu trùng ngoài ý muốn trong luồng email.

### 6.2. Recipient resolver

Tạo service/query độc lập, ví dụ `VisitSetupProgressRecipientResolver`, chịu trách nhiệm:

- lấy registrant/current contact của đúng request;
- lấy participant `ACCEPTED` của đúng instance;
- lấy email hiện tại từ `users` cho participant;
- loại Host;
- normalize/dedupe;
- trả `RecipientEnvelope` TO/CC/BCC và warnings.

Không sử dụng mảng người nhận do FE gửi để quyết định default.

### 6.3. Prepare draft command

Tạo `PrepareVisitSetupProgressEmailDraftCommand`:

1. Resolve request/instance fail-closed.
2. Xác thực Host + prep window.
3. Resolve template bằng code cố định `VISIT_SETUP_PROGRESS_UPDATE`.
4. Render subject/body ở backend bằng `IEmailTemplateRenderer` với biến của đúng instance.
5. Resolve default recipients.
6. Generate/store report PDF.
7. Tạo `email_drafts`:
   - `email_template_id` của template mới;
   - `related_type = 'VISIT_INSTANCE'`;
   - `related_id = visitInstanceId`;
   - subject/body đã render, `body_format='HTML'`;
   - owner là Host hiện tại.
8. Tạo `email_draft_recipients` theo đúng TO/CC/BCC.
9. Tạo `email_draft_attachments` cho PDF bắt buộc.
10. Commit rồi trả DTO.

Nếu upload file thành công nhưng transaction DB thất bại, cleanup external file theo best-effort và log không chứa path/signed URL.

### 6.4. Send command

Tạo `SendVisitSetupProgressEmailDraftCommand` hoặc handler chuyên dụng:

- draft tồn tại, `status=DRAFT`, thuộc current user;
- draft `related_type/related_id` khớp instance trên URL;
- template code đúng `VISIT_SETUP_PROGRESS_UPDATE`;
- current user vẫn là Host và instance vẫn trong prep window;
- có ít nhất một TO;
- recipient validator + cross-group duplicate + server max recipient;
- có đúng PDF bắt buộc hợp lệ và actor có quyền đọc file;
- subject/body qua `ManualEmailContent`/sanitizer hiện có;
- gửi MIME một lần với TO/CC/BCC thật;
- chuyển attachment sang sent history;
- `sent_emails.related_type='VISIT_INSTANCE'`, `related_id=visitInstanceId`;
- double-submit phải bị chặn bằng chuyển trạng thái draft nguyên tử/idempotency hiện có.

Không lưu/log danh sách BCC ngoài bảng recipient có kiểm soát. History/list/detail/export phải tiếp tục che BCC với viewer không phải sender theo rule hiện có.

## 7. Frontend implementation plan

### 7.1. VisitProcess

- Sửa năm state/effect mở mặc định.
- Thêm `Mail` action button cạnh `Báo cáo Lịch trình`.
- Dùng `perm.canSendSetupProgressEmail`.
- Thêm state chuẩn bị draft: loading/error/draftId/language/report metadata.
- Gọi API prepare; mở modal theo `key` chứa draftId để tránh state của draft trước rơi sang draft mới.
- Sau khi gửi thành công: đóng modal, toast thành công; không tự chuyển stage.

### 7.2. Mở rộng EmailComposeModal theo hướng tái sử dụng

Thêm props tùy chọn, không phá màn Quản lý email hiện tại:

```ts
lockedTemplate?: boolean
lockedAttachmentFileIds?: number[]
sendDraftOverride?: (draftId: number) => Promise<SendResult>
contextTitle?: string
onRefreshRequiredAttachment?: () => Promise<AttachmentDto>
```

Hành vi:

- `lockedTemplate=true`: ẩn/disable dropdown template, nhưng subject/body vẫn sửa được.
- Attachment có fileId bị khóa: không hiện nút xóa và có nhãn `Bắt buộc`.
- `sendDraftOverride`: dùng endpoint gửi setup-progress; các caller cũ vẫn dùng `emailDraftsApi.sendDraft`.
- Preview hiện đủ TO/CC/BCC và đánh dấu PDF bắt buộc.
- Autosave/reopen vẫn giữ đúng ba nhóm và attachment.

Không tạo một composer thứ hai có logic validation riêng.

### 7.3. API client/types

Cập nhật:

- `shared/api/endpoints.ts`.
- `features/delegations/api/delegationsApi.ts`.
- `features/delegations/types/delegations.types.ts`.
- Test mocks/fixtures cho permission mới.

## 8. Template và cấu hình email

### 8.1. Template mới

Đề xuất:

```text
template_code: VISIT_SETUP_PROGRESS_UPDATE
purpose: REPORT
recipient policy: CallerControlled
sensitive action: false
```

Declared variables:

```text
delegationName
campusName
plannedStart
plannedEnd
hostName
```

Không đưa `actionBlock`, OTP, accept/decline URL hay token vào template.

Nội dung mặc định nên nói rõ:

- đây là cập nhật mới nhất về công tác chuẩn bị;
- tên đoàn, cơ sở, thời gian dự kiến;
- Báo cáo Lịch trình được đính kèm;
- người nhận vui lòng phản hồi cho Host nếu cần điều chỉnh.

Không tự chèn `preparation_note` nội bộ vào mail gửi khách. Ghi chú đó có thể chứa briefing/nội dung vận hành không dành cho bên ngoài; Host có thể chủ động viết phần phù hợp trong composer.

### 8.2. Các nơi phải đồng bộ

- `SystemEmailTemplates.cs`: thêm constant và `CallerControlledTemplate(...)`.
- `SensitiveEmailVariables.KnownNonSensitive`: chỉ thêm biến mới nếu thực sự tạo placeholder mới; năm biến đề xuất đều đã có.
- `Assets/email-template-defaults.json`: thêm VI/EN hoàn chỉnh.
- Canonical full SQL: thêm template theo `template_code`, không phụ thuộc numeric id.
- `docs/database/scripts/email_template_cc_bcc_sync/02_sync_templates.sql`: regenerate từ canonical source, tăng catalog 30 → 31.
- `03_verify.sql`: cập nhật expected catalog/count/hash nếu có.
- `SystemEmailTemplateContractTests` và `EmailTemplateSyncScriptTests`.
- `docs/email-standardization/03-system-template-catalog.md` và traceability liên quan.
- `CanonicalSqlScript.ExpectedSha256`: chỉ cập nhật sau khi canonical SQL cuối cùng đã được verify; không tắt hash guard.

Không cần thêm bảng/cột DB cho chức năng này. Chỉ cần thêm template seed và dùng các bảng draft/recipient/attachment hiện có.

## 9. Bảo mật và tính đúng nghiệp vụ

- Backend là nguồn sự thật cho quyền gửi và danh sách mặc định.
- Fail closed nếu request/instance không khớp, Host đã đổi hoặc stage đã đổi.
- Template mới không mang token; invitation template cũ vẫn single-recipient/no-copy.
- Không render template hệ thống ở frontend.
- Không gửi mọi địa chỉ dưới TO một cách mù quáng; default tách khách ở TO và internal accepted participants ở CC.
- Email tùy nhập phải qua validator server hiện có.
- BCC chỉ hiện cho sender trong draft/preview/history có quyền; không trả `bccCount` cho viewer khác.
- HTML phải sanitize/normalize theo pipeline hiện tại; không dùng `dangerouslySetInnerHTML` với dữ liệu chưa sanitize.
- PDF/file phải đi qua storage và `OutboundEmailAttachments.ValidateAsync`/file authorization hiện có.
- Không ghi token, BCC list, filesystem path, signed URL hoặc HTML raw vào log.
- Không đánh dấu “đã gửi” trước khi DB prepare/commit hoàn tất; delivery failure phải có trạng thái an toàn và history rõ ràng.

## 10. Test plan bắt buộc

### 10.1. Frontend tests

1. Lần đầu vào Before tab: cả năm state đều mở.
2. Thu gọn thủ công vẫn hoạt động.
3. Rời tab và quay lại: các mục tự mở lại.
4. Nút gửi chỉ hiện khi `canSendSetupProgressEmail=true`.
5. Click tạo draft: loading, error và retry đúng.
6. Composer mở đúng draft, template khóa, PDF khóa.
7. Default TO/CC/BCC hiển thị đúng; có thể xóa/thêm/chuyển nhóm.
8. Duplicate cross-group bị chặn; email sai bị chặn; vượt limit bị chặn.
9. Preview hiển thị TO/CC/BCC, body sanitized và PDF.
10. Gửi dùng specialized endpoint; generic compose vẫn dùng generic endpoint.

### 10.2. Backend unit tests

1. Recipient resolver: contact + registrant khác nhau.
2. Contact trùng registrant: giữ một TO.
3. Participant trùng external email: giữ TO, không thêm CC.
4. Chỉ `ACCEPTED`; loại INVITED/DECLINED/REMOVED/ASSIGNED/Host.
5. Không có external: fallback participant đầu tiên vào TO.
6. Không có candidate: warning và draft chưa sendable.
7. Template policy là CallerControlled, non-sensitive, variables đúng.
8. Report service VI/EN và tên file đúng.
9. Permission true chỉ cho current Host trong prep window.

### 10.3. Integration/API tests

1. Current Host prepare draft → 200, đúng template, related instance, recipients và PDF.
2. Reuse existing draft không sinh draft/file trùng.
3. Non-Host/old Host/other campus → 403.
4. Request/instance mismatch → 404 hoặc forbidden theo convention hiện có, không rò dữ liệu.
5. Cancelled/closed/stage advanced → 409 business code ổn định.
6. Refresh report thay attachment nhưng giữ nguyên recipient/subject/body.
7. Forged draft id, wrong owner, wrong template, wrong related id → chặn.
8. Forged/unowned attachment → chặn.
9. Gửi thành công tạo đúng một sent email, TO/CC/BCC đúng và một PDF.
10. History của sender thấy BCC; viewer khác không thấy BCC và không thấy count gián tiếp.
11. SMTP/file load failure không tạo trạng thái thành công giả.
12. Double click không gửi hai MIME.
13. Generic manual email compose/draft/reply không regress.
14. Invitation token template vẫn cấm CC/BCC và gửi riêng từng người.
15. Schedule-report download/VI–EN hiện tại không regress.

### 10.4. Full regression gates

- Backend build.
- Unit tests.
- Architecture tests.
- Integration tests trên disposable DB dựng từ canonical SQL.
- Frontend TypeScript check.
- Frontend unit tests.
- Frontend production build.
- `git diff --check`.
- Template registry/default/SQL parity.
- Canonical SQL hash gate.

### 10.5. Real-stack E2E

Chạy bằng DB disposable/file-sink email, không gửi mail thật:

1. Login current Host.
2. Mở `/dashboard/visit/process/{id}` và xác nhận bốn khối yêu cầu đang mở.
3. Tạo một instance có contact/registrant khác nhau và participant ACCEPTED/INVITED/DECLINED.
4. Mở composer: TO chỉ gồm contact/registrant, CC chỉ ACCEPTED, BCC trống.
5. Xóa một default, thêm TO/CC/BCC tùy ý.
6. Sửa subject/body, preview, xác nhận PDF.
7. Gửi và kiểm tra `.eml`: header TO/CC đúng, BCC chỉ ở envelope, một PDF mở được.
8. Kiểm tra `sent_emails`, recipients, attachments và history authorization.
9. Chuyển Host trước lúc gửi một draft khác; old Host phải bị chặn.

## 11. Thứ tự triển khai đề xuất

### Batch 0 — Preflight

- Ghi branch/HEAD/WIP.
- Chạy baseline gates.
- Không merge/rebase/push khi phát hiện remote thay đổi trong lúc làm.

### Batch 1 — UI auto-expand

- Sửa state/effect/comment.
- Thêm FE tests nhỏ, commit riêng.

### Batch 2 — Shared report artifact + permission

- Tách report service.
- Bổ sung `canSendSetupProgressEmail`.
- Unit/integration tests quyền và report.

### Batch 3 — Template + prepare/refresh/send backend

- Template registry/default.
- Recipient resolver.
- Prepare/refresh/send command.
- Draft/file/history integration tests.

### Batch 4 — Frontend composer integration

- Endpoint/types/API.
- Mở rộng `EmailComposeModal` bằng props tương thích ngược.
- Nút và modal trên VisitProcess.
- FE tests.

### Batch 5 — Canonical SQL và tài liệu

- Thêm template vào canonical SQL.
- Regenerate sync script, verify count 31.
- Bump SHA-256 có chủ đích.
- Update catalog/traceability.

### Batch 6 — Regression và real-stack

- Full gates.
- E2E file-sink.
- Báo cáo bằng evidence thật; không ghi “đạt” nếu chưa chạy.

## 12. Gợi ý chia commit

```text
fix(visit-ui): expand before-visit sections by default
refactor(report): share schedule report artifact generation
feat(visit-email): prepare and send setup progress drafts
feat(visit-ui): compose setup progress email with report
chore(email): register and sync setup progress template
test(visit-email): cover recipients permissions attachments and history
```

## 13. Acceptance criteria cuối cùng

- Bốn phần người dùng yêu cầu đều mở sẵn khi vào/quay lại Before tab.
- Host vẫn thu gọn được từng phần.
- Chỉ current Host trong prep window thấy và dùng được `Gửi cập nhật chuẩn bị`.
- Mail dùng template riêng cấu hình được bởi HO qua cơ chế template hiện tại.
- Default recipient phản ánh dữ liệu thật: khách có email + participant `ACCEPTED`.
- Không đưa INVITED/DECLINED/REMOVED/ASSIGNED chưa xác nhận vào default.
- Host sửa/xóa/thêm TO/CC/BCC, subject/body và thêm file khác được.
- PDF Báo cáo Lịch trình được gắn tự động, xem được và không xóa nhầm.
- Preview xảy ra trước xác nhận gửi.
- Send-time reauthorization chặn old Host/stage sai.
- Sent history/attachments/TO–CC–BCC đúng; BCC không rò.
- Template registry, defaults, canonical SQL và sync script đồng bộ 31/31.
- Full regression và real-stack E2E có bằng chứng xanh.

## 14. Ngoài phạm vi change này

- Thêm email cho từng `visit_guest_member`.
- Cho HO/Staff Leader gửi thay Host.
- Gửi tự động theo lịch; đây là thao tác thủ công có preview/xác nhận.
- Thay đổi nội dung/recipient policy của email mời có token.
- Thay đổi business rule hoàn tất giai đoạn Before Visit.
