# PROMPT TIẾP TỤC CODE — QUẢN LÝ EMAIL DEPARTMENT LEADER: LIST DB THẬT, REPLY/CONFIRM/COMPLETE, BADGE CHƯA XỬ LÝ

## 0. Bối cảnh

Tôi đang code tiếp chức năng **Quản lý email** cho role **Department Leader** trong PEMS.

UI hiện tại đã có:

```text
- Tab Danh sách email
- Tab Gửi email
- Search email
- Filter loại email: Tất cả email / Đã gửi / Đã nhận
- Button Xem mẫu mail
- Bảng danh sách email
- Icon xem chi tiết email
```

Vấn đề hiện tại:

```text
- Danh sách email đang dùng mock data.
- Sau khi gửi mail thật, email không xuất hiện trong danh sách.
- Khi nhận mail / phản hồi mail cũng không xuất hiện hoặc không cập nhật trạng thái.
- Email đang xử lý chưa chuyển sang hoàn thành sau khi xác nhận / phản hồi / bấm hoàn thành.
- Menu sidebar “Quản lý email” chưa có badge đỏ số lượng email tới/chưa xử lý.
```

Yêu cầu: **nối chức năng thật với database**, không rewrite UI, không dùng mock.

Stack:

```text
Frontend: React + TypeScript + Tailwind CSS
Backend: .NET 8 Clean Architecture + MediatR + EF Core
Database: MySQL v8.4 refined v6 no dynamic permissions
```

---

## 1. Nguyên tắc bắt buộc

```text
KHÔNG rewrite UI.
KHÔNG dùng mock data.
KHÔNG tạo bảng draft.
KHÔNG thêm filter Draft.
KHÔNG query permissions/role_permissions vì DB mới đã bỏ dynamic permissions.
KHÔNG lạm dụng delivery_status để lưu trạng thái nghiệp vụ nếu không đúng nghĩa.
KHÔNG đổi schema nếu có thể derive từ bảng hiện tại.
Nếu cần thêm field để lưu trạng thái xử lý email, phải báo rõ và tạo SQL patch idempotent nhỏ.
```

Chỉ sửa file liên quan:

```text
- EmailManagement page/component
- Email API service/hook/type
- Send email form
- Email detail modal/page
- Sidebar navigation badge
- EmailsController
- Email queries/commands trong Application
- EmailService/SMTP nếu đang chưa log DB đúng
```

---

## 2. Database cần dùng

Dùng bảng hiện có:

```text
email_templates
sent_emails
sent_email_recipients
notifications
users
```

Mapping:

```text
sent_emails.sent_email_id        -> id email
sent_emails.subject              -> tiêu đề
sent_emails.body_snapshot        -> nội dung
sent_emails.status               -> trạng thái gửi: QUEUED / SENT / FAILED
sent_emails.sent_by              -> người gửi
sent_emails.sent_at              -> thời gian gửi
sent_emails.created_at           -> thời gian tạo
sent_emails.related_type         -> loại nghiệp vụ liên quan
sent_emails.related_id           -> id nghiệp vụ liên quan

sent_email_recipients.recipient_email  -> email người nhận
sent_email_recipients.recipient_name   -> tên người nhận
sent_email_recipients.recipient_type   -> TO / CC / BCC
sent_email_recipients.delivery_status  -> trạng thái giao mail
```

Quy ước danh sách:

```text
Đã gửi:
sent_emails.sent_by = currentUser.userId

Đã nhận:
sent_email_recipients.recipient_email = currentUser.email

Tất cả:
union Đã gửi + Đã nhận
```

---

## 3. API danh sách email thật

Tạo hoặc sửa endpoint hiện tại:

```text
GET /api/emails?mailBox=all|sent|received&keyword=&status=&page=&pageSize=
```

Không có:

```text
mailBox=draft
```

Response DTO gợi ý:

```ts
type EmailListItemDto = {
  id: number;
  sourceType: 'SENT' | 'RECEIVED';
  subject: string;
  snippet: string | null;
  counterpartName: string | null;
  counterpartEmail: string | null;
  sentAt: string | null;
  createdAt: string;
  sendStatus: 'QUEUED' | 'SENT' | 'FAILED';
  deliveryStatus?: string | null;
  processStatus: 'PROCESSING' | 'COMPLETED' | 'FAILED';
  relatedType?: string | null;
  relatedId?: number | null;
  canReply: boolean;
  canConfirm: boolean;
  canMarkComplete: boolean;
};
```

Frontend label:

```text
sourceType SENT      -> Đã gửi
sourceType RECEIVED  -> Đã nhận

processStatus PROCESSING -> Đang xử lý
processStatus COMPLETED  -> Hoàn thành
processStatus FAILED     -> Thất bại
```

---

## 4. Cách xác định trạng thái Đang xử lý / Hoàn thành

Không dùng mock.

Ưu tiên theo thứ tự:

```text
1. Nếu email liên quan đến một nghiệp vụ có trạng thái riêng qua related_type/related_id:
   - Derive processStatus từ bảng nghiệp vụ đó.
   - Ví dụ invitation/request/participant/resource request đã CONFIRMED/REPLIED/COMPLETED thì email = COMPLETED.

2. Nếu hiện tại chưa có bảng nghiệp vụ riêng cho phản hồi/xác nhận:
   - Có thể dùng notifications liên quan nếu đang có trạng thái xử lý phù hợp.

3. Nếu DB hiện tại không có bất kỳ field nào để lưu trạng thái xử lý email:
   - Không misuse sent_email_recipients.delivery_status.
   - Tạo SQL patch nhỏ, idempotent, thêm field xử lý nghiệp vụ vào sent_emails hoặc tạo bảng email_actions.
```

Khuyến nghị nếu cần patch nhỏ:

```sql
-- Chỉ tạo nếu thật sự cần lưu workflow status độc lập cho email.
ALTER TABLE sent_emails
  ADD COLUMN process_status ENUM('PROCESSING','COMPLETED','FAILED') NOT NULL DEFAULT 'PROCESSING' AFTER status,
  ADD COLUMN completed_at DATETIME NULL AFTER process_status,
  ADD COLUMN completed_by BIGINT UNSIGNED NULL AFTER completed_at;
```

Nếu MySQL không hỗ trợ `ADD COLUMN IF NOT EXISTS`, viết patch idempotent bằng kiểm tra `information_schema.columns`.

---

## 5. Gửi email xong phải xuất hiện trong danh sách

Khi user gửi mail ở tab **Gửi email**:

```text
1. Gọi API gửi mail thật.
2. Backend tạo sent_emails.
3. Backend tạo sent_email_recipients.
4. Backend gửi qua EmailService/SMTP.
5. Update status SENT/FAILED/QUEUED theo kết quả thật.
6. Frontend toast kết quả.
7. Frontend refresh/invalidate email list.
8. Chuyển sang tab Danh sách email hoặc giữ tab hiện tại nhưng list phải thấy email khi filter Đã gửi.
```

Sau gửi thành công:

```text
- Nếu đang ở Danh sách email filter Đã gửi hoặc Tất cả thì email mới xuất hiện ngay.
- Không cần reload trang.
```

---

## 6. Xem chi tiết email

Icon con mắt mở detail từ DB thật.

API:

```text
GET /api/emails/{id}?sourceType=SENT|RECEIVED
```

Detail hiển thị:

```text
Phân loại: Đã gửi / Đã nhận
Tiêu đề
Người gửi
Người nhận TO/CC/BCC
Thời gian
Trạng thái gửi
Trạng thái xử lý
Nội dung email
Related object nếu có
Action: Phản hồi / Xác nhận / Hoàn thành
```

Không dùng mock body.

---

## 7. Phản hồi email

Khi user bấm **Phản hồi** trong detail email:

```text
1. Mở form reply.
2. Gửi reply bằng API thật.
3. Tạo bản ghi sent_emails mới cho reply.
4. Tạo sent_email_recipients cho người nhận reply.
5. Link reply với email gốc bằng related_type/related_id hoặc provider_thread_id nếu đang có.
6. Sau khi reply thành công:
   - Email gốc chuyển processStatus = COMPLETED.
   - Email reply xuất hiện ở danh sách Đã gửi.
   - Toast: “Đã phản hồi email thành công.”
```

API gợi ý:

```text
POST /api/emails/{id}/reply
```

Request:

```ts
{
  body: string;
  cc?: EmailRecipientInput[];
  bcc?: EmailRecipientInput[];
}
```

---

## 8. Xác nhận / Hoàn thành email

Với email nhận được hoặc email yêu cầu xác nhận:

```text
- Nếu user bấm “Xác nhận”:
  processStatus -> COMPLETED
  completed_by = currentUser.userId
  completed_at = now

- Nếu user bấm “Hoàn thành”:
  processStatus -> COMPLETED
  completed_by = currentUser.userId
  completed_at = now
```

API gợi ý:

```text
POST /api/emails/{id}/confirm
POST /api/emails/{id}/complete
```

Có thể gộp thành:

```text
POST /api/emails/{id}/mark-completed
```

Backend phải check scope:

```text
- Người gửi được mark complete email mình gửi nếu nghiệp vụ cho phép.
- Người nhận được mark complete email gửi tới mình.
- User không liên quan không được thao tác.
```

Toast:

```text
Xác nhận thành công:
“Đã xác nhận email thành công.”

Hoàn thành thành công:
“Đã chuyển email sang trạng thái hoàn thành.”

Lỗi:
Hiển thị message backend.
```

Sau success:

```text
- Refresh detail.
- Refresh list.
- Badge trạng thái chuyển từ Đang xử lý sang Hoàn thành.
- Sidebar badge giảm số lượng chưa xử lý.
```

---

## 9. Sidebar badge đỏ số lượng email chưa xử lý

Menu **Quản lý email** ở sidebar cần hiển thị badge đỏ số lượng:

```text
Email đã nhận hoặc email liên quan currentUser mà processStatus = PROCESSING.
```

API gợi ý:

```text
GET /api/emails/unprocessed-count
```

Response:

```ts
{
  count: number
}
```

Điều kiện count:

```text
- Email nhận bởi currentUser: sent_email_recipients.recipient_email = currentUser.email
- processStatus = PROCESSING
- Không tính email FAILED nếu đã coi là thất bại
```

Nếu chưa có process_status thì derive theo rule ở mục 4.

Frontend:

```text
- Sidebar gọi API count sau login.
- Refresh count sau reply/confirm/complete.
- Nếu count = 0 thì ẩn badge.
- Nếu count > 99 thì hiển thị 99+.
- Badge màu đỏ, nhỏ, không làm vỡ sidebar.
```

---

## 10. Frontend behavior cần sửa

```text
- Xóa toàn bộ mock email list.
- useEmailList gọi API thật theo keyword/mailBox/status/page/pageSize.
- Sau send/reply/confirm/complete phải invalidate/refetch list.
- Filter Tất cả / Đã gửi / Đã nhận hoạt động bằng API params.
- Status badge hiển thị theo processStatus.
- Delivery/send status chỉ dùng trong detail hoặc badge phụ nếu cần.
- Không thêm Draft filter.
```

---

## 11. Backend Clean Architecture

Dùng cấu trúc hiện tại, không viết logic trong controller.

Gợi ý:

```text
PEMS.Application/Emails/Queries/ViewEmailList
PEMS.Application/Emails/Queries/ViewEmailDetail
PEMS.Application/Emails/Queries/GetUnprocessedEmailCount
PEMS.Application/Emails/Commands/SendEmail
PEMS.Application/Emails/Commands/ReplyEmail
PEMS.Application/Emails/Commands/MarkEmailCompleted
```

Controller:

```text
EmailsController
```

Controller chỉ gọi MediatR.

Query:

```text
- AsNoTracking.
- Projection DTO.
- Filter/paging ở DB.
- Không query toàn bộ rồi filter client.
```

Command:

```text
- Validate input bằng FluentValidation.
- Check current user scope.
- Update đúng status.
- Gửi email thật nếu là send/reply.
- Log sent_emails và sent_email_recipients.
```

---

## 12. Checklist nghiệm thu

```text
[ ] Danh sách email không còn mock.
[ ] Filter Tất cả / Đã gửi / Đã nhận lấy DB thật.
[ ] Gửi email xong email xuất hiện trong danh sách Đã gửi.
[ ] Nhận email hiển thị trong danh sách Đã nhận nếu recipient_email = currentUser.email.
[ ] Xem detail email lấy DB thật.
[ ] Reply email tạo sent_emails mới và xuất hiện ở Đã gửi.
[ ] Reply xong email gốc chuyển Hoàn thành.
[ ] Bấm Xác nhận chuyển email sang Hoàn thành.
[ ] Bấm Hoàn thành chuyển email sang Hoàn thành.
[ ] Status badge Đang xử lý / Hoàn thành cập nhật đúng.
[ ] Sidebar Quản lý email có badge đỏ số lượng email chưa xử lý.
[ ] Badge giảm sau reply/confirm/complete.
[ ] Không thêm Draft.
[ ] Không dùng mock data.
[ ] Không lộ email của user khác.
[ ] dotnet build pass.
[ ] npm run build pass.
```

---

## 13. Output mong muốn

Báo cáo ngắn sau khi code:

```text
Đã làm:
- Email list lấy DB thật.
- Gửi mail xong auto refresh list.
- Received mail lấy từ sent_email_recipients theo currentUser.email.
- Reply/Confirm/Complete chuyển processStatus sang COMPLETED.
- Sidebar badge email chưa xử lý.

DB:
- Không đổi schema / hoặc đã tạo patch process_status: [file patch] nếu bắt buộc.

Files changed:
- ...

Build:
- Backend: pass/fail
- Frontend: pass/fail
```
