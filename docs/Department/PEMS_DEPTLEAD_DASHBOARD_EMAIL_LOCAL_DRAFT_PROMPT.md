# PROMPT LÀM TIẾP CHỨC NĂNG DEPARTMENT LEADER — DASHBOARD DATA THẬT + EMAIL SEND + DRAFT LOCAL 30 PHÚT

## 0. Bối cảnh

Dự án: **PEMS — Partnership Engagement Management System**  
Frontend: **React + TypeScript + Tailwind CSS**  
Backend: **.NET 8 / Clean Architecture / MediatR / EF Core / MySQL**  
Database hiện tại: **pems_full_seed_logic_v8_4_refined_v3.sql**

UI Department Leader hiện tại đã làm xong. Prompt này dùng để làm tiếp **chức năng thật** cho:

```text
1. Dashboard Department Leader lấy data thật từ database.
2. Trang Quản lý email lấy data thật từ database.
3. Gửi email thật.
4. Lưu draft local/session trong 30 phút, không lưu draft database.
```

Yêu cầu quan trọng:

```text
KHÔNG rewrite UI từ đầu.
KHÔNG đọc lại toàn bộ docs/project nếu không cần.
KHÔNG dùng mock data cho dashboard/email.
KHÔNG đổi bố cục UI lớn nếu không cần.
KHÔNG đổi RBAC/permission.
KHÔNG đổi API cũ nếu có thể reuse.
KHÔNG tạo bảng email_drafts.
KHÔNG thêm filter Draft.
Code theo cấu trúc hiện tại, rõ ràng, dễ tìm, dễ fix sau này.
```

Chỉ đọc các file trực tiếp liên quan:

```text
Frontend:
- Dashboard Department Leader page/component.
- Dashboard API service/hook hiện tại.
- Email Management page hiện tại.
- Email API service/hook hiện tại.
- Send email form/component nếu có.
- Email template modal/list/detail nếu có.
- Route/sidebar liên quan Dashboard và Quản lý email.

Backend:
- DashboardController hoặc controller dashboard hiện có.
- EmailsController.cs.
- EmailTemplatesController nếu có.
- PEMS.Application/Dashboard/*
- PEMS.Application/Emails/*
- Email service SMTP hiện tại nếu có.
- DbContext entity/configuration cho:
  users
  roles
  departments
  visit_requests
  visit_request_campuses
  visit_logistics_items
  email_templates
  sent_emails
  sent_email_recipients
```

---

# PHẦN A — DASHBOARD DEPARTMENT LEADER LẤY DATA THẬT

## 1. Mục tiêu Dashboard

Trang Dashboard Department Leader hiện có 4 KPI:

```text
1. Chờ phân công
2. Đoàn sắp tới
3. Đang xử lý
4. Nhân sự
```

Hiện tại nếu đang hard-code/mock thì phải thay bằng **data thật từ database**.

Ngoài ra card thời gian hệ thống phải lấy **thời gian hiện tại thật**, gồm:

```text
Ngày
Tháng
Năm
Giờ
Phút
```

Không hard-code:

```text
Tháng 8, 2026
```

---

## 2. Thời gian hệ thống

Backend nên trả thời gian hiện tại từ server:

```ts
serverNow: string; // ISO datetime
```

Frontend format thành:

```text
23/06/2026 21:35
```

Hoặc:

```text
Thứ Ba, 23/06/2026 - 21:35
```

Yêu cầu:

```text
- Không hard-code tháng/năm.
- Không tự lấy sai timezone nếu backend đã có server time.
- Có thể dùng Asia/Ho_Chi_Minh nếu project có timezone handling.
- Nếu muốn realtime, frontend setInterval tăng từ serverNow ban đầu.
```

---

## 3. Scope dữ liệu Department Leader

Department Leader chỉ xem dữ liệu trong phạm vi phòng ban của mình.

Lấy current user từ token/session/current user service:

```text
currentUser.userId
currentUser.email
currentUser.departmentId
currentUser.primaryCampusId
currentUser.roleCode = DEPARTMENT
currentUser.subRole = LEADER
```

Backend tự resolve từ current user, không tin departmentId truyền từ frontend nếu có thể tránh.

Không lấy toàn hệ thống.

---

## 4. Công thức KPI Dashboard

### 4.1. Chờ phân công

Ý nghĩa:

```text
Đoàn/yêu cầu thuộc phòng ban hiện chưa được phân công nhân sự chịu trách nhiệm.
```

Nguồn ưu tiên:

```text
visit_logistics_items
join visit_request_campuses
join visit_requests
```

Điều kiện gợi ý:

```sql
visit_logistics_items.requested_to_department_id = currentUser.department_id
AND visit_logistics_items.assigned_to_user_id IS NULL
AND visit_logistics_items.status IN ('REQUESTED','RECEIVED','PLANNED')
AND visit_request_campuses.status NOT IN ('CANCELLED','CLOSED')
AND visit_requests.status = 'APPROVED'
```

Cách đếm:

```text
Vì UI đang nói “đoàn chưa được phân công/chưa có người chịu trách nhiệm”
→ count DISTINCT visit_request_campuses.visit_instance_id
```

Click KPI:

```text
Đi tới Nhiệm vụ tiếp khách.
Mở tab Phân công.
Apply filter chờ phân công nếu có.
```

Route gợi ý:

```text
/dashboard/department-tasks?tab=assignment&status=pending
```

---

### 4.2. Đoàn sắp tới

Ý nghĩa:

```text
Đoàn đã có người chịu trách nhiệm rồi nhưng chưa đến thời gian diễn ra.
```

Điều kiện gợi ý:

```sql
EXISTS logistics item thuộc currentUser.department_id
AND visit_logistics_items.assigned_to_user_id IS NOT NULL
AND visit_request_campuses.planned_start_at > NOW()
AND visit_request_campuses.status IN ('ASSIGNED','BEFORE_VISIT')
AND visit_requests.status = 'APPROVED'
```

Click KPI:

```text
Đi tới Nhiệm vụ tiếp khách.
Mở tab Bảng lịch.
```

Route gợi ý:

```text
/dashboard/department-tasks?tab=calendar
```

---

### 4.3. Đang xử lý

Ý nghĩa:

```text
Đoàn đã có người chịu trách nhiệm rồi và đang trong thời gian diễn ra.
```

Điều kiện gợi ý:

```sql
EXISTS logistics item thuộc currentUser.department_id
AND visit_logistics_items.assigned_to_user_id IS NOT NULL
AND visit_request_campuses.planned_start_at <= NOW()
AND visit_request_campuses.planned_end_at >= NOW()
AND visit_request_campuses.status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT')
AND visit_requests.status = 'APPROVED'
```

Click KPI:

```text
Đi tới Nhiệm vụ tiếp khách.
Mở tab Theo dõi tiến trình / Theo dõi tiến độ đoàn khách.
Apply filter đang xử lý nếu có.
```

Route gợi ý:

```text
/dashboard/department-tasks?tab=progress&status=in_progress
```

---

### 4.4. Nhân sự

Ý nghĩa:

```text
Tổng số nhân sự ACTIVE thuộc phòng ban của Department Leader.
```

Nguồn:

```text
users
```

Điều kiện:

```sql
users.department_id = currentUser.department_id
AND users.status = 'ACTIVE'
```

Nếu cần chỉ tính role department:

```sql
roles.role_code = 'DEPARTMENT'
```

Click KPI:

```text
Đi tới trang Nhân sự phòng ban.
```

Route gợi ý:

```text
/dashboard/department-personnel
```

---

## 5. API Dashboard đề xuất

Nếu chưa có endpoint riêng cho Department Leader, thêm endpoint theo cấu trúc hiện tại.

Route gợi ý:

```text
GET /api/dashboard/department-leader/summary
```

Response DTO gợi ý:

```ts
type DepartmentLeaderDashboardSummary = {
  serverNow: string;
  pendingAssignmentCount: number;
  upcomingDelegationCount: number;
  processingDelegationCount: number;
  activePersonnelCount: number;
  quickTasks: DepartmentLeaderQuickTask[];
  upcomingSchedules: DepartmentLeaderUpcomingSchedule[];
};
```

Item quick task:

```ts
type DepartmentLeaderQuickTask = {
  logisticsItemId: number;
  visitInstanceId: number;
  visitRequestId: number;
  delegationName: string;
  taskTitle: string;
  dueAt: string | null;
  status: string;
  assignedToUserId: number | null;
  assignedToName: string | null;
};
```

Item upcoming schedule:

```ts
type DepartmentLeaderUpcomingSchedule = {
  visitInstanceId: number;
  visitRequestId: number;
  delegationName: string;
  organizationName: string | null;
  plannedStartAt: string;
  plannedEndAt: string;
  campusName: string;
  location: string | null;
  status: string;
};
```

Không bắt buộc đúng tên y hệt nếu project có naming convention khác, nhưng phải type-safe và rõ nghĩa.

---

## 6. Backend Dashboard Clean Architecture

Làm theo cấu trúc hiện tại, ví dụ:

```text
PEMS.Api
└── Controllers
    └── DashboardController.cs

PEMS.Application
└── Dashboard
    └── Queries
        └── GetDepartmentLeaderDashboardSummary
            ├── GetDepartmentLeaderDashboardSummaryQuery.cs
            ├── GetDepartmentLeaderDashboardSummaryQueryHandler.cs
            └── DepartmentLeaderDashboardSummaryDto.cs
```

Yêu cầu:

```text
- Controller chỉ gọi IMediator.Send().
- Handler xử lý query.
- Dùng AsNoTracking().
- Projection trực tiếp sang DTO.
- Không Include dư thừa.
- Không N+1 query.
- Không query toàn bộ rồi filter memory.
- Backend tự lấy currentUser.departmentId/currentUser.campusId.
- Nếu user không phải DEPARTMENT/LEADER thì trả 403 hoặc dùng permission hiện có.
```

---

## 7. Frontend Dashboard

Cập nhật service/hook theo cấu trúc hiện tại.

Gợi ý:

```text
src/features/dashboard/api/departmentLeaderDashboardApi.ts
src/features/dashboard/hooks/useDepartmentLeaderDashboard.ts
```

Hoặc sửa file hiện tại nếu đã có.

Yêu cầu:

```text
- Bỏ mock/hard-code KPI.
- Gọi API thật.
- Có loading state.
- Có error state.
- Không crash nếu API trả null/empty.
- Format serverNow rõ ràng.
- KPI card dùng data từ response.
- Click KPI điều hướng đúng tab/trang.
```

Mapping click:

```text
Chờ phân công
→ /dashboard/department-tasks?tab=assignment&status=pending

Đoàn sắp tới
→ /dashboard/department-tasks?tab=calendar

Đang xử lý
→ /dashboard/department-tasks?tab=progress&status=in_progress

Nhân sự
→ /dashboard/department-personnel
```

Nếu route hiện tại khác thì dùng route thật hiện có.

---

# PHẦN B — QUẢN LÝ EMAIL: SEND MAIL + LOCAL DRAFT 30 PHÚT + DANH SÁCH EMAIL

## 8. Đối chiếu database email hiện tại

Database hiện tại có các bảng phù hợp:

```text
email_templates
sent_emails
sent_email_recipients
notifications
```

### 8.1. Phù hợp với Xem mẫu mail

Bảng:

```text
email_templates
```

Dùng cho:

```text
- Nút “Xem mẫu mail”.
- Danh sách mẫu mail.
- Xem chi tiết mẫu mail bằng icon con mắt.
- Chọn mẫu mail để fill subject/body nếu cần.
```

### 8.2. Phù hợp với Gửi mail và Email đã gửi

Bảng:

```text
sent_emails
sent_email_recipients
```

Dùng cho:

```text
- Gửi email.
- Lưu lịch sử gửi.
- Lưu từng người nhận TO/CC/BCC.
- Xem danh sách email đã gửi.
- Xem trạng thái gửi theo từng người nhận.
```

### 8.3. Email đã nhận

Database không có inbox Gmail thật. Vì vậy “Đã nhận” hiểu là:

```text
Email hệ thống PEMS đã gửi tới email của current user.
```

Nguồn:

```sql
sent_email_recipients
JOIN sent_emails
WHERE sent_email_recipients.recipient_email = currentUser.email
```

Lưu ý:

```text
Đây không phải inbox Gmail thật.
Không tích hợp Gmail inbound nếu project chưa có.
```

### 8.4. Draft

Yêu cầu đã chốt:

```text
Không lưu draft database.
Không tạo bảng email_drafts.
Không tạo SQL patch draft.
Không thêm filter Draft trong danh sách email.
Draft chỉ lưu localStorage hoặc sessionStorage trong 30 phút.
```

---

## 9. UI Email cần chỉnh

Tabs chính:

```text
Gửi email
Danh sách email
```

Hoặc:

```text
Danh sách email
Gửi email
```

Không còn tab chính:

```text
Mẫu email
```

Tab **Danh sách email** có toolbar:

```text
Search email
Filter trạng thái nếu hiện có
Filter loại email: Tất cả / Đã gửi / Đã nhận
Button: Xem mẫu mail
```

Không có filter:

```text
Draft
```

---

## 10. Gửi email thật

### 10.1. Form gửi email

Giữ form hiện tại nếu đã có. Form cần có:

```text
To
CC
BCC
Subject
Body
Chọn mẫu mail nếu UI hỗ trợ
Button Gửi
Button Lưu draft
```

### 10.2. Button Gửi

Khi bấm **Gửi**:

```text
Validate form.
Gọi API gửi email thật.
Lưu lịch sử vào sent_emails.
Lưu người nhận vào sent_email_recipients.
Gửi email qua EmailService/SMTP hiện tại.
Nếu gửi thành công: toast thành công.
Nếu gửi thất bại: hiển thị lỗi, không clear form.
```

Không báo fake success nếu EmailService lỗi.

### 10.3. API gửi email

Dùng endpoint hiện có nếu đã có. Nếu chưa có, thêm:

```text
POST /api/emails/send
```

Request DTO gợi ý:

```ts
type SendEmailRequest = {
  templateId?: number | null;
  relatedType?: string | null;
  relatedId?: number | null;

  to: EmailRecipientInput[];
  cc?: EmailRecipientInput[];
  bcc?: EmailRecipientInput[];

  subject: string;
  body: string;
};

type EmailRecipientInput = {
  email: string;
  name?: string | null;
};
```

Response DTO gợi ý:

```ts
type SendEmailResponse = {
  sentEmailId: number;
  status: 'QUEUED' | 'SENT' | 'FAILED';
  message: string;
};
```

---

## 11. Backend gửi email theo Clean Architecture

Theo cấu trúc hiện tại:

```text
PEMS.Application
└── Emails
    └── Commands
        └── SendEmail
            ├── SendEmailCommand.cs
            ├── SendEmailCommandHandler.cs
            ├── SendEmailCommandValidator.cs
            └── SendEmailResponse.cs
```

Controller:

```text
PEMS.Api/Controllers/EmailsController.cs
```

Controller chỉ:

```text
Nhận request.
Gọi IMediator.Send().
Trả response.
```

Handler làm:

```text
1. Validate recipients/subject/body.
2. Nếu templateId có thì kiểm tra email_templates tồn tại và ACTIVE.
3. Tạo sent_emails.
4. Tạo sent_email_recipients.
5. Gọi EmailService/SMTP.
6. Update status SENT/FAILED.
7. Ghi error_message nếu lỗi.
```

Validation:

```text
- To phải có ít nhất 1 email.
- Email đúng định dạng.
- Subject không rỗng sau trim.
- Body không rỗng sau trim.
- Subject tối đa 255 ký tự.
- Không gửi bằng template INACTIVE.
```

Permission:

```text
Dùng permission gửi email hiện có, ví dụ UC-47.SEND_EMAIL.
Không hard-code role trong Controller.
Backend vẫn check quyền cuối cùng.
```

---

## 12. Draft chỉ lưu local/session trong 30 phút

### 12.1. Không lưu DB

Không thêm:

```text
POST /api/emails/drafts
PUT /api/emails/drafts/{id}
GET /api/emails/drafts
DELETE /api/emails/drafts/{id}
```

Không tạo backend command draft.

Không tạo SQL patch draft.

### 12.2. Cơ chế lưu draft frontend

Khi user bấm **Lưu draft**:

```text
Lưu nội dung form vào localStorage hoặc sessionStorage.
TTL = 30 phút.
Không gọi API.
Không ghi database.
Hiển thị toast: “Đã lưu bản nháp trong 30 phút”.
```

Khuyến nghị:

```text
Dùng localStorage để F5 vẫn khôi phục được trong 30 phút.
```

Key theo user:

```ts
const draftKey = `pems_email_draft_${currentUser.userId}`;
```

Nếu có related object:

```ts
const draftKey = `pems_email_draft_${currentUser.userId}_${relatedType ?? 'general'}_${relatedId ?? 'new'}`;
```

### 12.3. Cấu trúc draft local

```ts
type LocalEmailDraft = {
  savedAt: string;
  expiresAt: string;
  templateId?: number | null;
  relatedType?: string | null;
  relatedId?: number | null;
  to: EmailRecipientInput[];
  cc: EmailRecipientInput[];
  bcc: EmailRecipientInput[];
  subject: string;
  body: string;
};
```

### 12.4. Khi mở form gửi email

Khi user mở tab/form **Gửi email**:

```text
Kiểm tra localStorage/sessionStorage.
Nếu có draft và chưa quá 30 phút:
  Hiển thị modal/prompt nhỏ:
  “Tìm thấy bản nháp email đã lưu. Bạn có muốn khôi phục không?”
  Button: Khôi phục
  Button: Bỏ qua

Nếu user chọn Khôi phục:
  Fill lại form.

Nếu user chọn Bỏ qua:
  Xóa draft khỏi storage.

Nếu draft quá 30 phút:
  Tự xóa, không hỏi.
```

### 12.5. Khi gửi email thành công

Sau khi gửi thành công:

```text
Xóa draft local/session tương ứng.
Clear form nếu UI hiện tại đang làm vậy.
Refresh danh sách email hoặc chuyển sang Danh sách email filter Đã gửi.
```

---

## 13. Danh sách email không có Draft filter

Filter loại email chỉ có:

```text
Tất cả
Đã gửi
Đã nhận
```

Không có:

```text
Draft
```

API danh sách email:

```text
GET /api/emails?mailBox=all|sent|received&keyword=&status=&page=&pageSize=
```

Không nhận:

```text
mailBox=draft
```

Nguồn dữ liệu:

### Đã gửi

```sql
sent_emails.sent_by = currentUser.user_id
```

### Đã nhận

```sql
sent_email_recipients.recipient_email = currentUser.email
```

### Tất cả

```text
Đã gửi + Đã nhận
```

DTO list:

```ts
type EmailListItemDto = {
  id: number;
  sourceType: 'SENT' | 'RECEIVED';
  subject: string;
  snippet?: string | null;
  senderName?: string | null;
  senderEmail?: string | null;
  recipientSummary?: string | null;
  status: string;
  createdAt: string;
  sentAt?: string | null;
};
```

Không còn:

```ts
sourceType: 'DRAFT'
```

---

## 14. Xem mẫu mail

Button:

```text
Xem mẫu mail
```

Mở modal/drawer:

```text
Danh sách mẫu mail
Search template
Filter ACTIVE/INACTIVE nếu có
Table:
  STT | Tên mẫu | Mục đích | Trạng thái | Hành động
```

Action con mắt:

```text
Xem chi tiết mẫu mail.
```

Detail mẫu mail hiển thị:

```text
template_code
name
purpose
campus
description
subject_vi
body_vi
subject_en
body_en
variables_text
status
created_at
updated_at
```

Nếu có button “Dùng mẫu này” thì:

```text
Fill subject/body vào form Gửi email.
Đóng modal nếu hợp lý.
```

---

## 15. Frontend files gợi ý

Không bắt buộc đúng y nguyên. Dùng folder hiện tại nếu đã có.

```text
src/features/dashboard/api/departmentLeaderDashboardApi.ts
src/features/dashboard/hooks/useDepartmentLeaderDashboard.ts

src/features/email/api/emailApi.ts
src/features/email/hooks/useEmailList.ts
src/features/email/hooks/useEmailTemplates.ts
src/features/email/hooks/useLocalEmailDraft.ts
src/features/email/pages/EmailManagement.tsx
src/features/email/components/SendEmailForm.tsx
src/features/email/components/EmailList.tsx
src/features/email/components/EmailTemplateModal.tsx
src/features/email/components/EmailTemplateDetailModal.tsx
```

Không tạo trùng component nếu file hiện tại đã có.

---

## 16. Hook local draft gợi ý

Tạo hook nhỏ, dễ tìm:

```text
useLocalEmailDraft.ts
```

Nhiệm vụ:

```text
saveDraft(draft)
getValidDraft()
clearDraft()
isExpired(draft)
```

Pseudo-code:

```ts
const DRAFT_TTL_MS = 30 * 60 * 1000;

export function useLocalEmailDraft(userId: number | string) {
  const key = `pems_email_draft_${userId}`;

  const saveDraft = (draft: Omit<LocalEmailDraft, 'savedAt' | 'expiresAt'>) => {
    const now = new Date();
    const payload = {
      ...draft,
      savedAt: now.toISOString(),
      expiresAt: new Date(now.getTime() + DRAFT_TTL_MS).toISOString(),
    };
    localStorage.setItem(key, JSON.stringify(payload));
  };

  const getValidDraft = () => {
    const raw = localStorage.getItem(key);
    if (!raw) return null;

    try {
      const draft = JSON.parse(raw) as LocalEmailDraft;
      if (new Date(draft.expiresAt).getTime() <= Date.now()) {
        localStorage.removeItem(key);
        return null;
      }
      return draft;
    } catch {
      localStorage.removeItem(key);
      return null;
    }
  };

  const clearDraft = () => {
    localStorage.removeItem(key);
  };

  return { saveDraft, getValidDraft, clearDraft };
}
```

Điều chỉnh type/path theo project hiện tại.

---

## 17. Build/test bắt buộc

Sau khi code:

```text
Backend:
dotnet build

Frontend:
npm run build
```

Không báo pass giả nếu chưa chạy được.

---

## 18. Checklist nghiệm thu

### Dashboard

```text
[ ] Dashboard Department Leader không còn số liệu hard-code.
[ ] Thời gian hệ thống lấy thời gian thật, đủ ngày/tháng/năm/giờ/phút.
[ ] Chờ phân công lấy từ DB theo department scope.
[ ] Đoàn sắp tới lấy từ DB theo department scope và planned_start_at > now.
[ ] Đang xử lý lấy từ DB theo department scope và now nằm giữa planned_start_at/planned_end_at.
[ ] Nhân sự lấy từ users theo currentUser.department_id và ACTIVE.
[ ] Click 4 KPI điều hướng đúng tab/trang.
[ ] Không lộ dữ liệu department/campus khác.
```

### Email

```text
[ ] Không còn tab chính “Mẫu email”.
[ ] Có tab “Danh sách email”.
[ ] Filter loại email chỉ có: Tất cả / Đã gửi / Đã nhận.
[ ] Không có filter Draft.
[ ] Gửi email gọi API thật.
[ ] Gửi email lưu sent_emails.
[ ] Gửi email lưu sent_email_recipients.
[ ] Gửi thất bại không báo fake success.
[ ] Nút Lưu draft không gọi API.
[ ] Draft lưu localStorage hoặc sessionStorage.
[ ] Draft TTL 30 phút.
[ ] F5 mở lại form thì hỏi khôi phục nếu draft chưa hết hạn.
[ ] Bỏ qua thì xóa draft.
[ ] Draft hết hạn tự xóa.
[ ] Gửi email thành công thì xóa draft.
[ ] Nút Xem mẫu mail mở list email_templates từ DB.
[ ] Icon con mắt xem được chi tiết template.
```

---

## 19. Output mong muốn từ AI coding assistant

Sau khi làm xong, báo cáo ngắn:

```text
Đã làm Dashboard:
- API/Query lấy summary từ DB.
- Server time thật.
- KPI thật theo department scope.
- Frontend gọi API thật.

Đã làm Email:
- Gửi email thật.
- Lưu sent_emails/sent_email_recipients.
- Draft lưu local/session 30 phút.
- Không tạo email_drafts.
- Không có filter Draft.
- Danh sách email chỉ lọc Tất cả/Đã gửi/Đã nhận.
- Xem mẫu mail và chi tiết mẫu mail.

Files changed:
- ...

SQL patch:
- Không có.

Build:
- Backend: pass/fail
- Frontend: pass/fail

Lưu ý:
- Email đã nhận hiện hiểu là email hệ thống gửi tới current user qua sent_email_recipients, không phải inbox Gmail thật.
```

---

## 20. Nhắc lại

```text
Đây là prompt làm tiếp chức năng trên UI hiện tại.
Dashboard vẫn phải có trong scope.
Email vẫn phải có gửi thật.
Draft chỉ lưu local/session 30 phút.
Không tạo bảng draft.
Không thêm filter Draft.
Không dùng mock data.
Không rewrite UI.
```
