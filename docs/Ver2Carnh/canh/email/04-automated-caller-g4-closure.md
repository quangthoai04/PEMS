# Giai đoạn 4 — Closure automated system email (Gate G4)

> Tài liệu này chỉ tuyên bố **Giai đoạn 4 / Gate G4**. Nó KHÔNG tuyên bố toàn bộ chương trình email đã xong — Giai đoạn 5–10 vẫn còn nguyên.

## 1. Bối cảnh

| | |
|---|---|
| Repository | `quangthoai04/PEMS` |
| Branch | `Canh-Iter1` |
| HEAD | `06c73b9491b7fb5afb88d20fc64de5ed9a56500c` |
| Canonical SQL | `docs/database/scripts/PEMS_FULL_V2_NO_SEED_DATA_GALLERY.sql` |
| SHA-256 | `51e178bb5e56fc927fd896e2a87ed8015043a2ca4904b4e1d9df581b2caae8a1` (không đổi trong Batch 10) |
| Baseline trước Batch 10 | build 0 error · unit 1410 · arch 14 · integration 868 · frontend sạch |

## 2. Cách re-audit

Không dùng audit cũ làm sự thật. Quét lại từ mã production tại HEAD theo **cơ chế phát email**, không theo tên class: mọi chỗ chạm `ISystemEmailDispatcher`, `IReportEmailSender`, `IEmailService`, `SmtpClient`/`MailMessage`, hosted service, controller, và mọi hằng `SystemEmailTemplates.*`. Sau đó đối chiếu hai chiều với seed `email_templates`.

## 3. Mọi điểm gửi email production

**29 điểm gọi dispatcher** trong mã production. Trừ hai chỗ không phải caller nghiệp vụ — `ReportEmailSender` (pipeline dùng chung của 6 báo cáo) và `VisitRequestOtpMail` (helper dùng chung của 3 caller OTP) — còn **27 điểm gửi nghiệp vụ**, thuộc **25 caller**. Chênh lệch đến từ các caller có nhiều nhánh gửi: `ReplaceStaffLeader` (3), `EditPendingAccountEmail` (2), `VisitContactClaimService` (2).

Ngoài dispatcher, **không** còn đường gửi nào khác: `SmtpClient`/`MailMessage` chỉ tồn tại trong `EmailService`; `FileSinkEmailService` là sink test double-gated; không controller nào gửi trực tiếp.

### Caller bị loại khỏi G4 (có lý do)

| Đường | Lý do loại |
|---|---|
| `SendEmailCommandHandler` | **Manual compose** — subject/body do người dùng nhập. Thuộc Giai đoạn 5. |
| `SendEmailDraftCommandHandler` | **Manual draft** — nội dung người dùng. Giai đoạn 5. |
| `ReplytoEmailCommandHandler` | **Manual reply** — nội dung người dùng. Giai đoạn 5. |
| `FileSinkEmailService` | Sink test, chỉ bật khi Testing + `PEMS_E2E_TEST_SINK_ENABLED`. |
| `NotificationService` | Thông báo trong hệ thống, không phải email. |

## 4. Ma trận caller → template

| Nhóm | Caller | Template | Snapshot | Kiểu |
|---|---|---|---|---|
| Account | CreateAccount · Resend · EditPending(2) · ReplaceStaffLeader(3) · UpdateAccountRole · UpdateBasicAccountInfo · ConfirmAccountEmail · AddDepartmentPersonnel | 8 mã `ACCOUNT_*` | None/Stripped/Full theo phân loại | best-effort |
| Auth | ForgotPassword | `AUTH_PASSWORD_RESET_OTP` | **None** | best-effort |
| Visit OTP | InitiateVisitRequestV2 · ResendVisitRequestOtp · RecoverVisitRequestOtp (qua `VisitRequestOtpMail`) | `VISIT_REQUEST_OTP` | **None** | best-effort |
| Contact | VisitContactClaimService (2 nhánh) | `VISIT_CONTACT_CLAIM` · `VISIT_CONTACT_TRANSFER` | Stripped | best-effort |
| Participant | InviteVisitParticipant · AssignDepartmentStaff | 4 mã invitation/assignment | Stripped | best-effort |
| Logistics | PrepareVisitLogistics · AssignRequestAssignee · ProposeRequestChange · RemindExpenseReports | 4 mã `LOGISTICS_*` | Stripped ×3, **Full** (C-22) | best-effort |
| Reminder | VisitReminderDispatchService | `VISIT_REMINDER_HOST` · `VISIT_REMINDER_PARTICIPANTS` | **Full** | job, at-most-once |
| Report | 6 caller qua `ReportEmailSender` | 4 mã `REPORT_*` | **Full** | **Mandatory** |

## 5. Ma trận template → caller

Cả **26/26** template ACTIVE đều có ≥1 caller production. Không có template sống mà không ai gửi. Hai cặp dùng chung có chủ ý: `REPORT_DEPARTMENT_INVOICE` (C-26 + C-29) và `REPORT_PERSONNEL_PERFORMANCE` (C-27 + C-28).

## 6. Static scan và phân loại

| Từ khoá | Hit | Phân loại |
|---|---|---|
| `BuildEmailHtml` / `BuildInvoiceHtml` / `DefaultContentHtml` | 0 | — |
| Bốn subject báo cáo cũ | 4 seed + 5 docs | seed/docs, hợp lệ |
| `BrandedShell` | 1 (`SystemEmailDispatcher`) | khung thương hiệu thuần, hợp lệ (§VII.B.2) |
| `SubjectVi ??` / `BodyVi ??` | **2 → 0** | **vi phạm, đã sửa** (xem Mục 7) |
| `EmailComposition.RenderTemplate` | **2 → 0** | **vi phạm, đã sửa** |
| `"Chưa có thông tin"` trong caller | 1 (`AssignDepartmentStaff.FormatWindow`) | **hợp lệ** — giá trị nghiệp vụ cho biến `plannedTime` khi chưa có lịch, không phải fallback của renderer |
| `"Chưa có thông tin"` trong PDF | 1 (`ScheduleReportPdfRenderer`) | nội dung PDF, không phải email |
| 12 mã legacy | chỉ trong `NotificationTypes`/`VisitInstanceStatus` | **trùng tên khác miền** — hằng thông báo/trạng thái, không phải template code |
| `EmailTemplateId = null` | 0 | — |
| `RetryCount += 1` lần đầu / `DELIVERED` / `ex.Message` vào `error_message` | 0 | — |
| CC/BCC mặc định trong system template | 0 | — |
| Numeric template id | 0 | — |

## 7. Vi phạm đã sửa trong Batch 10

**`PreviewEmailTemplateQueryHandler` là renderer thứ hai.** Đây là vi phạm thật, còn sót sau Batch 1–9:

1. `SubjectEn ?? SubjectVi` / `BodyVi ?? BodyEn` — **fallback ngôn ngữ chéo**, đúng thứ renderer thật cố tình từ chối (`TemplateLanguageContentMissing`).
2. Dùng `EmailComposition.RenderTemplate` — renderer regex riêng kèm **bảng fallback im lặng** (`"Chưa có thông tin"`, `"Chưa chọn phòng ban"`, `"Chưa nhập"`…). Đây chính là khiếm khuyết D-9, đã bị gỡ khỏi mọi đường **gửi** ở Batch 7 nhưng đường **xem trước** vẫn dùng.
3. Nhánh chết theo mã legacy `VISIT_STUDENT_SUPPORT_INVITATION` + hai `Regex.Replace` cắt dòng của "legacy DB template".

Hệ quả thật: người vận hành có thể duyệt một bản xem trước mà lần gửi thật sẽ **từ chối** (biến thiếu thành chữ "Chưa có thông tin" ở preview, nhưng fail-closed ở send) — tức preview mô tả sai thứ người nhận sẽ nhận.

**Đã sửa:** preview đi qua `IEmailTemplateRenderer` chung. Xoá hẳn `EmailComposition.RenderTemplate` + bảng fallback + `ActionUrlVarNames` (không còn ai dùng). Xoá nhánh legacy và regex cắt dòng. Khối hành động vẫn hiển thị dạng **disabled** — token thật chỉ sinh khi gửi thật.

**Kéo theo frontend:** 5 chỗ dựng context xem trước đang gửi khoá PascalCase (`DelegationName`, `CampusName`) và khoá ngoài tập khai báo (`recipientEmail`, `eventTitle`, `logisticsItemTitle`…). Đã sửa đúng tập biến khai báo của từng template. Không đổi UI, không đổi form, không đổi flow, không đổi wording hiển thị.

## 8. Legacy closure

12 mã legacy: **không mã nào ACTIVE** trong canonical fresh, **không caller production nào dùng**, **không có trong registry**. Không xoá lịch sử: `sent_emails` cũ giữ nguyên `subject`/`body_snapshot` đã lưu, không map lại sang template mới.

## 9. Bằng chứng test

Batch 10 thêm **12 contract test** (`SystemEmailG4ClosureTests`): registry đúng 26 mã · seed ACTIVE đúng 26 mã · khớp **hai chiều** · legacy không sống lại · mọi template đủ VI/EN + biến lower camelCase không trùng + không placeholder lạ · **render cả 26 mã ở cả hai ngôn ngữ không sót `{{`** · thiếu/thừa biến fail-closed · INACTIVE fail-closed · recipient/snapshot policy đúng theo phân loại · hai OTP giữ `body_snapshot = NULL` · hai reminder giữ Full · nhóm REPORT đúng 4 mã.

Cộng dồn toàn chương trình: **unit 1410 · architecture 14 · integration 880**.

## 10. Rủi ro đã biết, KHÔNG thuộc G4

| Rủi ro | Trạng thái |
|---|---|
| C-18 orphan window (transaction boundary giữ nguyên) | Đã ghi decision log B6 |
| Reminder at-most-once — mất một nhắc lịch nếu chết sau claim trước send | Đã ghi B8-07, có chủ ý |
| SMTP accept và cập nhật DB không atomic | Ghi rõ ở mọi batch, không tuyên bố atomic |
| Report nhiều người nhận — thất bại một phần không gửi lại phần đã gửi | Ghi ở B9 |
| Manual TO/CC/BCC | Giai đoạn 5 |
| BCC history authorization | Giai đoạn 5 |
| `ViewEmailQueryHandler` không lọc người nhận | Giai đoạn 5 |

## 11. Chưa thuộc G4

- Giai đoạn 5–10 (draft/compose/reply, frontend TO/CC/BCC, sync 7b, E2E tổng, traceability, deploy).
- Nút UI gọi hai route `send-invoice` (route đã sống; màn hình chưa có nút — B9-D1).

## 12. Kết luận

**Gate G4 — ĐẠT** cho Giai đoạn 4 (Automated System Email Callers).

Điểm tiếp theo: **Giai đoạn 5 — Draft/compose/reply/history + authorization + BCC.**
