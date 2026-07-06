> [!WARNING]
> **LEGACY ARCHITECTURE NOTE (Campus-independent Approval Update)**
> This document has been updated to reflect the new Campus-independent Approval architecture.
> - **HO is now monitor/read-only.** There is no centralized multi-campus approval by HO.
> - **Staff Leader approval is per-campus.** Each Staff Leader directly receives and approves/rejects their own campus instance right after submission.
> - **Self-hosting is supported.** Staff Leaders can assign themselves as the host during approval.
> - **ASSIGNED is removed.** Approving a request now requires assigning a host immediately.
> - **New statuses:** `PARTIALLY_APPROVED` (request level) and `REJECTED` (campus level) are added. 
> - **Cancel logic:** Visitors can cancel requests in `PENDING_APPROVAL` or `PARTIALLY_APPROVED` states.
> - **Transportation:** `transportation_note` and `transportation_note` are replaced by `transportation_note`.
> Please refer to the latest codebase and SQL schema for the current implementation.

# PEMS — Đặc tả cập nhật VisitProcess cho Host: Thông tin đơn gốc, mời thành phần tham gia và phản hồi qua email

> Mục đích: File này dùng để đưa cho AI Agent đọc và code phần **Host xử lý trước tiếp khách** sau khi Staff Leader đã gán Staff làm Host cho một `visit_request_campuses` / `visitInstance`.
>
> Phạm vi chính: `VisitProcess.tsx`, API detail quy trình tiếp khách, API mời người tham gia, email action token, thông báo góc phải trên màn hình.
>
> Nguyên tắc lớn: **không fake dữ liệu ở frontend**, **không đổi Agenda đã code xong**, **không bật edit/lưu toàn bộ setup nếu backend chưa có API lưu thật**.

---

## 1. Bối cảnh hiện tại

Màn `Quy trình tiếp khách` hiện đang có các phần:

```text
1. Thông tin chung
   1.1 Thông tin người tạo
   1.2 Thông tin đoàn khách
   1.3 Thiết lập & Điều phối sự kiện (Set up)
       - Loại hình tham quan
       - Agenda
       - Thành phần tham gia
       - Cảnh báo & Thông báo
       - Ghi chú chung

2. Chuẩn bị chi tiết
3. Trong tiếp khách
4. Sau tiếp khách
```

Vấn đề hiện tại:

```text
1. “Thông tin người tạo” và “Thông tin đoàn khách” còn hard-code / demo data, chưa khớp dữ liệu form đăng ký thật của khách.
2. Hai phần trên cần hiển thị chi tiết giống modal preview trước khi duyệt.
3. “Loại hình tham quan” đang bị lặp trong Set up, trong khi dữ liệu này đã thuộc form đăng ký của khách.
4. Phần Host đang có checkbox “Là tôi” và khả năng đổi Host, không đúng vì Host đã được Staff Leader gán trước đó.
5. Staff hỗ trợ IC / Department / Student hiện đang dùng dữ liệu giả frontend, chưa có API mời thật.
6. Khi mời người tham gia, hệ thống cần vừa insert DB vừa gửi email có nút phản hồi.
7. Mọi thao tác thành công/thất bại cần hiện toast thông báo ở góc phải trên màn hình.
```

---

## 2. Nguyên tắc không được phá

### 2.1 Không động vào Agenda

Phần Agenda đã code xong và đang có API lưu riêng.

Không được:

```text
- Không sửa AgendaSetupPanel nếu task không yêu cầu.
- Không sửa API saveVisitAgenda.
- Không đổi logic lưu datetime của Agenda.
- Không đổi trạng thái stage before/during/after vì không liên quan.
```

### 2.2 Không bật edit toàn bộ setup

Trong source hiện tại có ý tưởng `SETUP_SAVE_AVAILABLE = false`, nghĩa là nhiều phần setup ngoài Agenda chưa có API lưu thật.

Không được bật bừa:

```ts
const SETUP_SAVE_AVAILABLE = true;
```

Lý do:

```text
Nếu bật toàn cục, các form hậu cần, ghi chú, cảnh báo, chuẩn bị chi tiết có thể editable nhưng chưa chắc lưu vào DB thật.
Điều này tạo lỗi nghiệp vụ: user tưởng đã lưu nhưng dữ liệu bị mất.
```

Chỉ làm thật phần:

```text
- Hiển thị thông tin đơn gốc read-only.
- Mời thành phần tham gia.
- Gửi email phản hồi.
- Hiển thị trạng thái lời mời.
- Hiển thị toast kết quả thao tác.
```

---

## 3. Mục tiêu sau khi cập nhật

Sau khi Host mở màn `Quy trình tiếp khách` của một campus instance được gán:

```text
1. Host xem được thông tin người đăng ký đúng dữ liệu thật từ form khách gửi.
2. Host xem được thông tin đoàn khách đúng như preview trước khi duyệt.
3. Loại hình tham quan chỉ hiển thị trong “Thông tin đoàn khách”, không còn lặp trong Set up.
4. Host chính hiển thị read-only, không đổi được.
5. Host mời được:
   - Staff hỗ trợ IC cùng campus.
   - Department Leader của phòng ban GENERAL cùng campus.
   - Student cùng campus.
6. Staff và Student candidate có hiển thị cảnh báo trùng lịch.
7. Khi mời, backend insert `visit_participants` và gửi email.
8. Email Staff / Student có nút “Chấp nhận” và “Từ chối”.
9. Email Department Leader có nút “Chấp nhận”, “Từ chối”, “Gán nhân sự”.
10. Nút “Gán nhân sự” bắt buộc login hệ thống.
11. UI hiển thị đúng trạng thái lời mời từ DB.
12. Mọi thao tác có toast ở góc phải trên màn hình.
```

---

## 4. Phạm vi file cần kiểm tra

### 4.1 Frontend

Ưu tiên kiểm tra:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/shared/auth/AuthContext.tsx
```

Có thể tách component nếu giúp code sạch hơn:

```text
RequestRegistrantReadOnly.tsx
VisitRequestSummaryReadOnly.tsx
ParticipantInvitationPanel.tsx
CandidateSearchDropdown.tsx
ParticipantStatusBadge.tsx
ConflictBadge.tsx
TopRightToast.tsx nếu project chưa có component toast dùng chung
```

Nếu project đã có toast pattern ở màn khác thì tái sử dụng, không thêm thư viện mới.

### 4.2 Backend

Ưu tiên kiểm tra:

```text
backend/PEMS.Api/Controllers/DelegationsController.cs
backend/PEMS.Application/Delegations/Queries/GetVisitProcessDetail/
backend/PEMS.Application/Delegations/Queries/GetVisitProcessPermissions/
backend/PEMS.Application/Delegations/Queries/GetParticipantCandidates/
backend/PEMS.Application/Delegations/Commands/InviteVisitParticipant/
backend/PEMS.Application/Delegations/Commands/RemoveVisitParticipant/
backend/PEMS.Application/EmailActions/
backend/PEMS.Domain/Entities/VisitParticipant.cs
backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
```

Tên folder/handler có thể khác theo repo hiện tại, nhưng phải giữ Clean Architecture:

```text
Controller chỉ nhận request và gọi MediatR.
Handler xử lý business validation, scope, DB transaction, email, audit.
```

---

## 5. Phần A — Hiển thị thông tin đơn gốc cho Host

### 5.1 Đổi tên UI

Đổi:

```text
Thông tin người tạo
```

Thành:

```text
Thông tin người đăng ký
```

Lý do: đây là dữ liệu khách/visitor hoặc người tạo form đã nhập khi submit visit request, không phải dữ liệu Host tạo ở bước setup.

### 5.2 Thông tin người đăng ký — read-only

Hiển thị dữ liệu thật từ backend, không hard-code `defaultValue`.

Các field nên có:

```text
- Họ và tên người đăng ký
- Email
- Số điện thoại
- Đơn vị / tổ chức
- Chức danh / phòng ban
- Quốc tịch nếu có
- Ghi chú liên hệ nếu form có
```

UI rule:

```text
- Read-only 100%.
- Không có nút sửa.
- Không có nút lưu.
- Không dùng input editable nếu không cần; có thể dùng read-only field card.
- Nếu dùng input readOnly thì style phải rõ là chỉ đọc.
```

### 5.3 Thông tin đoàn khách — read-only

Phần này phải hiển thị chi tiết như modal preview trước khi duyệt.

Các field nên có:

```text
- Tên đoàn khách
- Đơn vị / tổ chức đoàn
- Phạm vi: Một cơ sở / Liên cơ sở
- Campus hiện tại của Host
- Danh sách campus nếu là liên cơ sở
- Thời gian dự kiến của campus hiện tại
- Mục đích thăm
- Nội dung làm việc
- Loại hình tham quan: Campus tour / Họp trao đổi / Khác
- Ngôn ngữ / phiên dịch nếu có
- Media consent nếu có
- Phương tiện di chuyển nếu có
- Ghi chú của khách nếu có
- Danh sách khách mời
- Danh sách external support team nếu form có
```

Lưu ý quan trọng:

```text
Host đang xử lý một campus instance cụ thể.
Vì vậy thời gian chính để setup và check lịch phải lấy từ visit_request_campuses.planned_start_at / planned_end_at của instance hiện tại.
Không dùng nhầm thời gian tổng nếu request là multi-campus.
```

### 5.4 Backend DTO đề xuất

Mở rộng API đang dùng cho VisitProcess detail.

Không cần tạo API mới nếu có thể mở rộng:

```text
GET /api/delegations/visit-process/{visitRequestId}/instances/{visitInstanceId}
```

Hoặc API hiện tại tương đương:

```text
delegationsApi.getVisitProcessDetail(visitRequestId, visitInstanceId)
```

DTO gợi ý:

```ts
type VisitProcessDetail = {
  visitRequestId: number;
  visitInstanceId: number;
  campusId: number;
  campusName: string;
  plannedStartAt: string;
  plannedEndAt: string;
  canEditBefore: boolean;

  requestSummary: {
    registrantName: string;
    registrantEmail: string;
    registrantPhone: string | null;
    registrantOrganization: string | null;
    registrantJobTitle: string | null;
    registrantNationality: string | null;

    delegationName: string;
    organizationName: string | null;
    visitScope: 'SINGLE_CAMPUS' | 'MULTI_CAMPUS';
    visitTypeLabels: string[];
    purpose: string | null;
    workingContent: string | null;
    languageNote: string | null;
    mediaConsent: string | null;
    transportationNote: string | null;
    note: string | null;

    campuses: Array<{
      campusId: number;
      campusName: string;
      plannedStartAt: string;
      plannedEndAt: string;
      isCurrent: boolean;
    }>;

    guestMembers: Array<{
      guestMemberId: number;
      memberType: 'GUEST' | 'EXTERNAL_SUPPORT';
      fullName: string;
      organization: string;
      jobTitle: string;
      nationality: string;
      displayOrder: number;
    }>;
  };

  host: {
    userId: number;
    fullName: string;
    email: string;
    phone: string | null;
    departmentName: string | null;
    statusLabel: string;
  } | null;

  agenda: Array<any>;
  participants: Array<VisitParticipantListItem>;
};
```

### 5.5 Empty/null display

Nếu field thiếu dữ liệu:

```text
- Hiển thị “Chưa có thông tin” thay vì để trống.
- Không render undefined/null ra UI.
- Không crash nếu requestSummary chưa load.
```

---

## 6. Phần B — Bỏ “Loại hình tham quan” khỏi Set up

Trong `Thiết lập & Điều phối sự kiện (Set up)`, bỏ block:

```text
1. Loại hình tham quan
- Campus tour
- Họp trao đổi
- Khác
```

Lý do:

```text
Loại hình tham quan là dữ liệu khách đã chọn từ form đăng ký.
Host chỉ cần xem để chuẩn bị, không sửa ở bước setup.
Dữ liệu này sẽ hiển thị read-only trong “Thông tin đoàn khách”.
```

Sau khi bỏ block này, đánh số lại hoặc giữ nhãn tùy UI:

Khuyến nghị:

```text
1. Agenda
2. Thành phần tham gia
3. Cảnh báo & Thông báo
4. Ghi chú chung
```

Nhưng nếu không muốn đụng nhiều UI, có thể chỉ xóa block và không quá quan trọng chuyện đánh số, miễn không gây hiểu nhầm.

---

## 7. Phần C — Host chính read-only

### 7.1 Rule nghiệp vụ

Host chính đã được Staff Leader gán trước đó.

Nguồn dữ liệu chính:

```text
visit_request_campuses.current_host_user_id
```

Nếu backend snapshot Host vào `visit_participants`:

```text
participant_role = IC_HOST
is_host = true
status = ASSIGNED hoặc ACCEPTED tùy rule hiện tại
```

### 7.2 UI Host

Không hiển thị:

```text
- Checkbox “Là tôi”
- Dropdown đổi host
- Nút thêm host
- Nút xóa host
- Nút tick/từ chối giả lập phản hồi host
```

Chỉ hiển thị card read-only:

```text
Host chính
[Avatar]
Họ tên
Email
Số điện thoại nếu có
Vai trò: Host chính
Trạng thái: Đã được phân công
```

Nếu current user chính là Host:

```text
Có thể hiển thị badge “Bạn là Host chính”.
```

Nếu không load được Host:

```text
Hiển thị cảnh báo: “Chưa xác định Host chính cho campus instance này.”
Không cho thao tác mời participant nếu chưa có Host hợp lệ.
```

---

## 8. Phần D — Mời Staff hỗ trợ IC

### 8.1 Candidate rule

Khi Host mời Staff hỗ trợ IC, danh sách candidate phải lọc:

```text
role_code = STAFF
sub_role = STAFF
users.status = ACTIVE
users.primary_campus_id = current visit instance campus_id
department.department_type = IC
department.status = ACTIVE
user_id != current_host_user_id
chưa có participant row active trong visit_instance_id hiện tại
```

Participant row active được hiểu là status chưa bị gỡ:

```text
status IN ('INVITED', 'ACCEPTED', 'ASSIGNED')
```

Có thể loại luôn `DECLINED` nếu không cho mời lại, hoặc cho mời lại sau khi confirm nghiệp vụ.

Khuyến nghị:

```text
Nếu đã DECLINED, Host có thể mời lại nhưng phải tạo email token mới và cập nhật status về INVITED, note cũ vẫn giữ trong audit.
Nếu chưa cần mời lại, cứ chặn để đơn giản.
```

### 8.2 UI Staff hỗ trợ IC

UI cần có:

```text
- Dropdown search theo tên/email.
- Candidate item hiển thị:
  + Họ tên
  + Email
  + Department
  + Badge trùng lịch nếu có
- Nút “Mời”.
- Danh sách người đã mời.
- Badge trạng thái lời mời.
```

Không dùng dữ liệu hard-code như:

```text
Thêm người A
Nguyễn Có TK
```

### 8.3 Khi bấm Mời

Backend xử lý trong transaction:

```text
1. Validate current user là Host của visitInstance.
2. Validate instance status cho phép mời.
3. Validate candidate đúng rule.
4. Insert hoặc update visit_participants:
   participant_role = IC_SUPPORT
   is_host = false
   status = INVITED
   invited_by = currentUser.userId
   invited_at = NOW()
   created_by = currentUser.userId
5. Tạo sent_emails.
6. Tạo sent_email_recipients.
7. Tạo email_action_tokens cho ACCEPT và DECLINE.
8. Gửi email.
9. Ghi audit log.
10. Trả response cho frontend.
```

### 8.4 Toast sau thao tác

Khi mời thành công:

```text
Toast success góc phải trên:
“Đã gửi lời mời tới [Tên staff]. Trạng thái hiện tại: Chờ phản hồi.”
```

Khi lỗi:

```text
Toast error:
“Không thể gửi lời mời. Vui lòng thử lại.”
```

Nếu candidate đã được mời:

```text
Toast warning:
“Người này đã có trong danh sách tham gia của đoàn.”
```

Nếu candidate trùng lịch nhưng vẫn cho mời:

```text
Toast warning sau mời:
“Đã gửi lời mời, nhưng [Tên] đang có lịch trùng với thời gian tiếp khách.”
```

Nếu policy chặn người trùng lịch:

```text
Toast error:
“Không thể mời vì [Tên] đang có lịch trùng.”
```

---

## 9. Phần E — Mời Student hỗ trợ

### 9.1 Candidate rule

```text
users.role_code = STUDENT
users.status = ACTIVE
users.primary_campus_id = current visit instance campus_id
chưa có participant row active trong visit_instance_id hiện tại
```

Nếu có `student_code` trong schema/entity thì search theo:

```text
full_name
email
student_code
```

Nếu chưa có `student_code`, chỉ search theo tên/email.

### 9.2 UI Student

UI cần có:

```text
- Dropdown search theo tên/email/mã sinh viên nếu có.
- Candidate item hiển thị:
  + Họ tên
  + Email
  + Mã sinh viên nếu có
  + Badge conflict nếu có
- Nút “Mời”.
- Danh sách student đã mời.
- Badge trạng thái lời mời.
```

### 9.3 Khi bấm Mời

Backend tạo `visit_participants`:

```text
participant_role = STUDENT
is_host = false
status = INVITED
invited_by = current host
invited_at = NOW()
```

Sau đó tạo email và token giống Staff.

### 9.4 Toast

Thành công:

```text
“Đã gửi lời mời tới sinh viên [Tên].”
```

Không tìm thấy hoặc không hợp lệ:

```text
“Không tìm thấy sinh viên hợp lệ trong campus này.”
```

Trùng lịch:

```text
“[Tên sinh viên] đang có lịch trùng với thời gian tiếp khách.”
```

---

## 10. Phần F — Mời Department hỗ trợ

### 10.1 Rule tổng quát

Host không mời trực tiếp Department Staff.

Host mời:

```text
Department Leader của phòng ban GENERAL cùng campus
```

Department Leader sau đó có thể:

```text
- Chấp nhận tự tham gia / tự phụ trách.
- Từ chối.
- Đăng nhập hệ thống để gán nhân sự trong department xử lý.
```

### 10.2 Department candidate rule

Phòng ban chọn được:

```text
departments.campus_id = current visit instance campus_id
departments.department_type = GENERAL
departments.status = ACTIVE
```

Leader hợp lệ:

```text
users.role_code = DEPARTMENT
users.sub_role = LEADER
users.status = ACTIVE
users.department_id = selected department_id
users.primary_campus_id = current visit instance campus_id
```

Nếu phòng chưa có leader active:

```text
Không cho gửi mời.
Hiển thị: “Phòng này chưa có trưởng phòng đang hoạt động, không thể gửi lời mời.”
```

### 10.3 UI Department support

UI đề xuất:

```text
[Dropdown search phòng ban]
Sau khi chọn phòng:
  Trưởng phòng: [Tên Department Leader]
  Email: [Email]
  Trạng thái lịch nếu cần
  [Mời phòng ban]

Danh sách đã mời:
  Phòng Hành chính
  Trưởng phòng: Nguyễn Văn A
  Badge: Chờ phản hồi / Đã chấp nhận / Đã từ chối / Đã gán nhân sự
  Nếu đã gán: Nhân sự xử lý: Trần Văn B
```

### 10.4 Khi Host mời Department Leader

Backend xử lý:

```text
1. Validate Host đúng instance.
2. Validate department cùng campus và GENERAL.
3. Validate Department Leader active.
4. Insert visit_participants cho Department Leader:
   participant_role = DEPT_SUPPORT
   status = INVITED
   invited_by = current host
   invited_at = NOW()
5. Tạo sent_emails.
6. Tạo sent_email_recipients.
7. Tạo email_action_tokens cho ACCEPT và DECLINE.
8. Tạo link “Gán nhân sự” yêu cầu login.
9. Gửi email.
10. Ghi audit.
```

### 10.5 Toast Department

Khi mời thành công:

```text
“Đã gửi lời mời tới trưởng phòng [Tên phòng ban].”
```

Phòng chưa có leader:

```text
“Phòng ban này chưa có trưởng phòng đang hoạt động.”
```

Sai scope:

```text
“Không thể mời phòng ban ngoài campus của chuyến tiếp khách.”
```

---

## 11. Phần G — Conflict check cho Staff và Student

### 11.1 Target time

Thời gian dùng để check conflict phải lấy từ campus instance hiện tại:

```text
visit_request_campuses.planned_start_at
visit_request_campuses.planned_end_at
```

Không lấy thời gian tổng của request nếu multi-campus.

### 11.2 Overlap rule

Dùng rule:

```sql
existing_start < targetEnd
AND existing_end > targetStart
```

Ý nghĩa:

```text
Nếu một lịch kết thúc đúng lúc chuyến thăm bắt đầu thì không tính trùng.
Nếu một lịch bắt đầu đúng lúc chuyến thăm kết thúc thì không tính trùng.
```

### 11.3 Nguồn conflict

Nguồn A — `calendar_events`:

```text
owner_user_id = candidate.user_id
status = ACTIVE
deleted_at IS NULL nếu schema có field này
overlap với targetStart/targetEnd
```

Nguồn B — campus visit instance khác:

```text
visit_request_campuses.visit_instance_id != current visitInstanceId
visit_request_campuses.status IN ('ASSIGNED', 'BEFORE_VISIT', 'DURING_VISIT')
overlap với targetStart/targetEnd
candidate là current_host_user_id
hoặc candidate nằm trong visit_participants của instance khác với status IN ('INVITED','ACCEPTED','ASSIGNED')
```

### 11.4 Privacy

Nếu conflict là lịch cá nhân private:

```text
Không hiển thị title/nội dung/location chi tiết.
Chỉ hiển thị: “Có lịch cá nhân trùng”.
```

### 11.5 UI conflict badge

Không trùng:

```text
Badge: Không trùng lịch
Style: green / emerald
```

Có trùng:

```text
Badge: Có 2 lịch trùng
Style: amber / warning
```

Private:

```text
Badge: Có lịch cá nhân trùng
Style: amber
```

---

## 12. Phần H — Email phản hồi cho Staff và Student

### 12.1 Email Staff / Student cần có

Subject gợi ý:

```text
[PEMS] Lời mời tham gia hỗ trợ tiếp khách — [Tên đoàn]
```

Nội dung email:

```text
Xin chào [Tên người nhận],

Bạn được mời tham gia hỗ trợ đoàn [Tên đoàn] tại [Campus] vào [Thời gian].
Vai trò: Staff hỗ trợ IC / Sinh viên hỗ trợ.

Vui lòng phản hồi bằng một trong hai nút dưới đây:

[Chấp nhận] [Từ chối]
```

### 12.2 Token action

Khi gửi email, backend tạo `email_action_tokens`.

Mỗi nút có token riêng hoặc một token có intended actions tùy thiết kế backend, nhưng phải đảm bảo:

```text
- Không lưu raw token trong DB, chỉ lưu token_hash.
- Token có expires_at.
- Token dùng một lần.
- Token trỏ tới target_type = VISIT_PARTICIPANT.
- target_id = participant_id.
- action_context = PARTICIPATION_RESPONSE.
- allowed/intended action = ACCEPT hoặc DECLINE.
```

### 12.3 Public endpoint xử lý

Endpoint gợi ý:

```text
GET  /public/email-actions/{token}
POST /public/email-actions/{token}
```

Hoặc endpoint hiện có của dự án.

Handler phải:

```text
1. Hash raw token.
2. Tìm email_action_tokens.token_hash.
3. Check expires_at.
4. Check used_at/result_status.
5. Load visit_participants theo target_id.
6. Check participant.status hiện tại còn INVITED hay không.
7. Nếu action = ACCEPT:
   - status = ACCEPTED
   - responded_at = NOW()
8. Nếu action = DECLINE:
   - status = DECLINED
   - responded_at = NOW()
   - note = reason nếu form public có nhập lý do
9. Set email_action_tokens:
   - used_at
   - used_action
   - result_status
   - result_message
   - used_ip
   - used_user_agent
10. Commit transaction.
11. Trả trang kết quả public.
```

### 12.4 Nếu bấm lại email cũ

Nếu participant đã trả lời rồi:

```text
Không update lần hai.
result_status = ALREADY_RESPONDED.
Hiển thị public page: “Bạn đã trả lời yêu cầu này rồi.”
```

### 12.5 Sau khi Staff/Student phản hồi

Backend nên tạo notification cho Host:

```text
[Nguyễn Văn A] đã chấp nhận lời mời tham gia đoàn [Tên đoàn].
```

Hoặc:

```text
[Nguyễn Văn A] đã từ chối lời mời tham gia đoàn [Tên đoàn].
```

Frontend Host khi reload hoặc refetch participant list sẽ thấy badge mới.

Nếu có realtime/notification polling, toast có thể hiện:

```text
“[Tên] đã chấp nhận lời mời.”
```

---

## 13. Phần I — Email phản hồi cho Department Leader

### 13.1 Email Department Leader cần có

Subject gợi ý:

```text
[PEMS] Yêu cầu phòng ban hỗ trợ tiếp khách — [Tên đoàn]
```

Nội dung:

```text
Xin chào [Tên Department Leader],

Phòng ban [Tên phòng] được mời hỗ trợ đoàn [Tên đoàn] tại [Campus] vào [Thời gian].

Bạn có thể chọn một trong các thao tác:

[Chấp nhận] [Từ chối] [Gán nhân sự]
```

### 13.2 Chấp nhận / Từ chối qua email

Hai nút này xử lý như Staff/Student:

```text
Không cần đăng nhập.
Backend validate token.
Update visit_participants của Department Leader.
```

Kết quả:

```text
Chấp nhận:
status = ACCEPTED
responded_at = NOW()

Từ chối:
status = DECLINED
responded_at = NOW()
note = reason nếu có
```

### 13.3 Gán nhân sự bắt buộc đăng nhập

Nút **Gán nhân sự** không được thực hiện bằng public token.

Nút này phải mở link nội bộ:

```text
/dashboard/visit/process/{visitInstanceId}/department-assignment/{participantId}
```

Hoặc route hiện có tương đương.

Nếu chưa login:

```text
Redirect login.
Sau login redirect lại màn gán nhân sự.
```

### 13.4 Backend verify khi gán nhân sự

Khi Department Leader mở màn gán nhân sự:

```text
1. current user phải login.
2. role_code = DEPARTMENT.
3. sub_role = LEADER.
4. currentUser.department_id = invited Department Leader participant department.
5. currentUser.primary_campus_id = visit instance campus_id.
6. visit_participants.participant_id là lời mời DEPT_SUPPORT còn hợp lệ.
7. visit instance status cho phép xử lý: ASSIGNED hoặc BEFORE_VISIT.
8. Không cho xử lý nếu CANCELLED/CLOSED.
```

### 13.5 Candidate Department Staff

Department Leader chỉ được gán nhân sự:

```text
users.role_code = DEPARTMENT
users.sub_role = STAFF
users.status = ACTIVE
users.department_id = currentUser.department_id
users.primary_campus_id = currentUser.primary_campus_id
```

Có thể hiển thị conflict nếu cần, nhưng yêu cầu hiện tại chỉ bắt buộc Staff IC và Student có conflict. Nếu làm thêm conflict cho Department Staff thì tốt, nhưng không được làm chậm task chính.

### 13.6 Khi Department Leader gán nhân sự

Backend xử lý:

```text
1. Validate scope Department Leader.
2. Validate selected Department Staff.
3. Tạo hoặc update visit_participants cho Department Staff:
   participant_role = DEPT_SUPPORT
   status = ASSIGNED
   assigned_by = department_leader_user_id
   assigned_at = NOW()
   created_by = department_leader_user_id
4. Update row của Department Leader:
   Option A: status = ASSIGNED, note = “Đã gán cho [Tên nhân sự]”
   Option B: giữ status = ACCEPTED, note = “Đã gán cho [Tên nhân sự]”
```

Khuyến nghị chọn Option A:

```text
Row Department Leader status = ASSIGNED để UI biết phòng ban đã có người xử lý.
Row Department Staff status = ASSIGNED để nhân sự xử lý thấy task.
```

Nếu muốn phân biệt rõ hơn mà schema không có status riêng, dùng `note`.

### 13.7 Email/notification cho Department Staff được gán

Sau khi gán:

```text
- Gửi notification trong hệ thống cho Department Staff.
- Có thể gửi email thông báo “Bạn được phân công hỗ trợ đoàn...”
- Email này chỉ là thông báo, không nhất thiết có nút accept/decline nếu nghiệp vụ coi ASSIGNED là phân công bắt buộc.
```

Nếu muốn Department Staff cũng có quyền từ chối thì cần UC riêng, không tự thêm nếu chưa chốt.

### 13.8 Toast khi Department Leader gán nhân sự

Thành công:

```text
“Đã gán [Tên nhân sự] xử lý yêu cầu hỗ trợ đoàn.”
```

Sai scope:

```text
“Bạn không có quyền gán nhân sự cho yêu cầu này.”
```

Nhân sự không hợp lệ:

```text
“Nhân sự được chọn không thuộc phòng ban của bạn hoặc không còn hoạt động.”
```

---

## 14. Phần J — API đề xuất

### 14.1 Visit process detail

```http
GET /api/delegations/visit-instances/{visitInstanceId}/process-detail
```

Trả về:

```text
- requestSummary
- host
- agenda
- participants
- permission flags nếu cần
```

Nếu project đang dùng route cũ:

```http
GET /api/delegations/{visitRequestId}/instances/{visitInstanceId}/process-detail
```

Thì giữ route cũ, chỉ mở rộng response.

### 14.2 List participant

```http
GET /api/delegations/visit-instances/{visitInstanceId}/participants
```

Response item:

```ts
type VisitParticipantListItem = {
  participantId: number;
  userId: number;
  fullName: string;
  email: string;
  phone: string | null;
  roleCode: string;
  subRole: string | null;
  departmentId: number | null;
  departmentName: string | null;
  participantRole: 'IC_HOST' | 'IC_SUPPORT' | 'DEPT_SUPPORT' | 'STUDENT';
  isHost: boolean;
  status: 'INVITED' | 'ACCEPTED' | 'DECLINED' | 'ASSIGNED' | 'REMOVED';
  invitedByName: string | null;
  invitedAt: string | null;
  respondedAt: string | null;
  assignedByName: string | null;
  assignedAt: string | null;
  note: string | null;

  departmentAssignment?: {
    departmentId: number;
    departmentName: string;
    leaderUserId: number;
    assignedStaffUserId: number | null;
    assignedStaffName: string | null;
  } | null;
};
```

### 14.3 Search candidates

```http
GET /api/delegations/visit-instances/{visitInstanceId}/participant-candidates?type=IC_SUPPORT&keyword=abc
GET /api/delegations/visit-instances/{visitInstanceId}/participant-candidates?type=STUDENT&keyword=abc
GET /api/delegations/visit-instances/{visitInstanceId}/participant-candidates?type=DEPT_SUPPORT&departmentId=5&keyword=abc
```

Candidate response:

```ts
type ParticipantCandidateDto = {
  userId: number;
  fullName: string;
  email: string;
  phone: string | null;
  roleCode: string;
  subRole: string | null;
  departmentId: number | null;
  departmentName: string | null;
  campusId: number;
  campusName: string;
  conflictCount: number;
  hasPrivateConflict: boolean;
  conflictSummary: string | null;
  canInvite: boolean;
  disabledReason: string | null;
};
```

### 14.4 List departments for support

```http
GET /api/delegations/visit-instances/{visitInstanceId}/support-departments?keyword=abc
```

Response:

```ts
type SupportDepartmentDto = {
  departmentId: number;
  departmentName: string;
  campusId: number;
  campusName: string;
  leaderUserId: number | null;
  leaderName: string | null;
  leaderEmail: string | null;
  canInvite: boolean;
  disabledReason: string | null;
};
```

### 14.5 Invite participant

```http
POST /api/delegations/visit-instances/{visitInstanceId}/participants/invite
```

Request:

```ts
type InviteVisitParticipantRequest = {
  participantType: 'IC_SUPPORT' | 'DEPT_SUPPORT' | 'STUDENT';
  userId?: number;
  departmentId?: number;
  message?: string | null;
};
```

Rule:

```text
IC_SUPPORT: userId bắt buộc.
STUDENT: userId bắt buộc.
DEPT_SUPPORT: departmentId bắt buộc, backend tự tìm Department Leader.
Frontend không tự truyền leader userId nếu backend có thể suy ra để tránh giả mạo.
```

Response:

```ts
type InviteVisitParticipantResponse = {
  participantId: number;
  participantRole: string;
  status: string;
  emailQueued: boolean;
  emailRecipient: string;
  message: string;
};
```

### 14.6 Remove participant

```http
PATCH /api/delegations/visit-instances/{visitInstanceId}/participants/{participantId}/remove
```

Rule:

```text
- Chỉ Host đúng instance được gỡ participant do mình mời nếu status còn INVITED.
- Không cho gỡ Host chính.
- Không cho gỡ participant ACCEPTED/ASSIGNED nếu chưa có nghiệp vụ xác nhận.
- Nếu cần gỡ ACCEPTED/ASSIGNED, phải có confirm và audit note.
```

### 14.7 Public email action

```http
GET  /public/email-actions/{token}
POST /public/email-actions/{token}
```

Dùng cho:

```text
- Staff IC accept/decline.
- Student accept/decline.
- Department Leader accept/decline.
```

Không dùng cho:

```text
- Department Leader gán nhân sự.
```

### 14.8 Department assignment

```http
GET /api/delegations/visit-instances/{visitInstanceId}/department-assignment/{participantId}
GET /api/delegations/visit-instances/{visitInstanceId}/department-staff-candidates?keyword=abc
POST /api/delegations/visit-instances/{visitInstanceId}/department-assignment/{participantId}/assign
```

Assign request:

```ts
type AssignDepartmentStaffRequest = {
  assignedStaffUserId: number;
  note?: string | null;
};
```

---

## 15. Phần K — Backend validation bắt buộc

### 15.1 Validate Host thao tác mời

Backend phải check:

```text
visit_request_campuses.visit_instance_id = route visitInstanceId
visit_request_campuses.current_host_user_id = currentUser.userId
visit_request_campuses.status IN ('ASSIGNED', 'BEFORE_VISIT')
visit_request_campuses.status NOT IN ('CANCELLED', 'CLOSED')
```

Nếu không đúng:

```text
403 Forbidden hoặc 409 Conflict tùy case.
```

### 15.2 Không tin frontend

Không tin các field frontend gửi:

```text
campusId
departmentId
roleCode
subRole
participantRole
status
```

Backend phải tự query DB để xác định:

```text
- Campus instance.
- Current Host.
- Candidate role/subRole/campus/department.
- Department Leader.
- Participant status hiện tại.
```

### 15.3 Duplicate participant

Không cho một user có nhiều row active trong cùng visit instance.

Nếu DB có unique `(visit_instance_id, user_id)`:

```text
- Nếu row đã tồn tại và status = REMOVED hoặc DECLINED, quyết định update hoặc báo conflict.
- Nếu row đã tồn tại và status IN INVITED/ACCEPTED/ASSIGNED, báo conflict.
```

### 15.4 Status transition

Cho participant:

```text
INVITED -> ACCEPTED
INVITED -> DECLINED
INVITED -> REMOVED nếu Host thu hồi trước khi phản hồi
ACCEPTED -> ASSIGNED nếu Department Leader gán nhân sự hoặc theo rule phù hợp
ASSIGNED -> REMOVED chỉ khi có nghiệp vụ gỡ rõ ràng
```

Không cho:

```text
DECLINED -> ACCEPTED qua token cũ
ACCEPTED -> DECLINED qua token cũ
REMOVED -> ACCEPTED qua token cũ
```

### 15.5 Email action token

Handler phải check:

```text
- token_hash tồn tại.
- expires_at chưa hết hạn.
- used_at chưa có.
- target_type đúng.
- target_id đúng.
- recipient đúng nếu có lưu recipient_user_id / recipient_email.
- target status còn cho phép action.
```

Nếu target đã phản hồi:

```text
Không update participant.
Set result_status = ALREADY_RESPONDED.
```

---

## 16. Phần L — Frontend UI chi tiết

### 16.1 Toast góc phải trên màn hình

Mọi thao tác ở các phần phải có thông báo kết quả ở góc phải trên màn hình.

Toast position:

```text
top-right
fixed
top-6
right-6
z-index cao
```

Loại toast:

```text
success: thao tác thành công
error: lỗi hệ thống hoặc lỗi validation
warning: cảnh báo nhưng vẫn xử lý được
info: cập nhật trạng thái / thao tác trung tính
```

Thời gian tự ẩn:

```text
4 đến 5 giây
```

Toast phải có nút đóng.

### 16.2 Nội dung toast theo thao tác

Load detail thất bại:

```text
Không thể tải thông tin quy trình tiếp khách. Vui lòng thử lại.
```

Mời Staff thành công:

```text
Đã gửi lời mời tới [Tên staff].
```

Mời Student thành công:

```text
Đã gửi lời mời tới sinh viên [Tên sinh viên].
```

Mời Department thành công:

```text
Đã gửi lời mời tới trưởng phòng [Tên phòng ban].
```

Gỡ participant thành công:

```text
Đã gỡ [Tên] khỏi danh sách mời.
```

Email đã gửi nhưng queue chậm:

```text
Đã tạo lời mời. Email đang được hệ thống gửi đi.
```

Candidate trùng lịch:

```text
[Tên] đang có lịch trùng với thời gian tiếp khách.
```

Token email hết hạn:

```text
Liên kết phản hồi đã hết hạn. Vui lòng liên hệ Host.
```

Token đã phản hồi:

```text
Bạn đã trả lời lời mời này rồi.
```

Department assign thành công:

```text
Đã gán [Tên nhân sự] xử lý yêu cầu hỗ trợ đoàn.
```

### 16.3 Badge trạng thái lời mời

Map badge:

```text
INVITED   -> Chờ phản hồi
ACCEPTED  -> Đã chấp nhận
DECLINED  -> Đã từ chối
ASSIGNED  -> Đã phân công / Đã gán nhân sự
REMOVED   -> Đã gỡ
```

Style gợi ý:

```text
INVITED: amber/yellow
ACCEPTED: emerald/green
DECLINED: red
ASSIGNED: blue/cyan
REMOVED: slate/gray
```

### 16.4 Không fake phản hồi ở frontend

Không giữ các nút tick/x trong card để Host tự set:

```text
Đồng ý
Từ chối
```

Lý do:

```text
Người được mời phải tự phản hồi qua portal hoặc email action.
Host không được tự thay trạng thái thay họ.
```

Frontend chỉ:

```text
- Hiển thị status từ DB.
- Có nút “Mời”.
- Có nút “Gỡ” nếu status cho phép.
- Có nút “Xem lý do” nếu DECLINED có note.
```

### 16.5 Candidate dropdown search

Yêu cầu UI:

```text
- Search debounce 300–500ms.
- Không gọi API khi keyword quá ngắn nếu muốn tối ưu.
- Hiển thị loading nhỏ trong dropdown.
- Empty state: “Không tìm thấy người phù hợp.”
- Error state: “Không thể tải danh sách. Thử lại.”
```

Candidate item:

```text
[Avatar]
Tên
Email
Department / Role
Conflict badge
```

---

## 17. Phần M — Email templates gợi ý

### 17.1 Staff IC / Student invitation

```html
<p>Xin chào {{recipientName}},</p>

<p>Bạn được mời tham gia hỗ trợ đoàn <strong>{{delegationName}}</strong> tại <strong>{{campusName}}</strong>.</p>

<ul>
  <li>Thời gian: {{plannedStartAt}} - {{plannedEndAt}}</li>
  <li>Vai trò: {{participantRoleLabel}}</li>
  <li>Host chính: {{hostName}}</li>
</ul>

<p>Vui lòng phản hồi:</p>

<a href="{{acceptUrl}}">Chấp nhận</a>
<a href="{{declineUrl}}">Từ chối</a>
```

### 17.2 Department Leader invitation

```html
<p>Xin chào {{departmentLeaderName}},</p>

<p>Phòng ban <strong>{{departmentName}}</strong> được mời hỗ trợ đoàn <strong>{{delegationName}}</strong> tại <strong>{{campusName}}</strong>.</p>

<ul>
  <li>Thời gian: {{plannedStartAt}} - {{plannedEndAt}}</li>
  <li>Host chính: {{hostName}}</li>
</ul>

<p>Vui lòng chọn thao tác:</p>

<a href="{{acceptUrl}}">Chấp nhận</a>
<a href="{{declineUrl}}">Từ chối</a>
<a href="{{assignStaffUrl}}">Gán nhân sự</a>
```

### 17.3 Public decline form

Nếu muốn người nhận nhập lý do từ chối:

```text
GET token page hiển thị thông tin lời mời + textarea lý do.
POST token với action DECLINE + reason.
```

Nếu chưa muốn làm form lý do:

```text
Bấm Từ chối update ngay status = DECLINED, note = null.
```

Khuyến nghị nên có form lý do, nhưng không bắt buộc trong phase đầu.

---

## 18. Phần N — SQL/DB liên quan

Bảng chính:

```text
visit_requests
visit_request_campuses
visit_guest_members
visit_participants
calendar_events
sent_emails
sent_email_recipients
email_action_tokens
notifications
audit_logs
audit_log_changes
```

### 18.1 visit_participants

Dùng cho:

```text
- Host chính nếu snapshot.
- Staff hỗ trợ IC.
- Department Leader được mời.
- Department Staff được gán.
- Student được mời.
```

Enum:

```text
participant_role: IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT
status: INVITED, ACCEPTED, DECLINED, ASSIGNED, REMOVED
```

Các field quan trọng:

```text
visit_instance_id
user_id
participant_role
is_host
status
invited_by
invited_at
responded_at
assigned_by
assigned_at
note
created_at
created_by
updated_at
updated_by
```

### 18.2 email_action_tokens

Dùng cho:

```text
- Token Chấp nhận / Từ chối lời mời tham gia.
- Không dùng để đọc inbox.
- Không dùng cho Gán nhân sự vì gán nhân sự cần login.
```

Cần lưu:

```text
token_hash
recipient_user_id hoặc recipient_email
target_type
target_id
action_context
intended_action / used_action
expires_at
used_at
result_status
result_message
used_ip
used_user_agent
```

Tên field cụ thể phải theo schema/entity hiện tại.

---

## 19. Phần O — Prompt hoàn chỉnh cho AI Agent

Copy đoạn sau cho AI Agent:

```text
Bạn là Senior Full-stack Engineer cho hệ thống PEMS.

Task: Cập nhật màn VisitProcess cho Host sau khi Staff Leader gán Staff làm Host cho một campus visit instance.

File chính:
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx

Backend cần kiểm tra:
- DelegationsController
- GetVisitProcessDetail handler
- visit_participants entity/configuration
- email_action_tokens handler
- sent_emails / sent_email_recipients service

Yêu cầu bắt buộc:

1. Không bật SETUP_SAVE_AVAILABLE = true toàn cục.
2. Không sửa AgendaSetupPanel và không sửa logic lưu Agenda.
3. Không hard-code dữ liệu người đăng ký / đoàn khách.
4. Mở rộng VisitProcessDetail để trả requestSummary giống preview trước khi duyệt.
5. Đổi “Thông tin người tạo” thành “Thông tin người đăng ký”, read-only.
6. “Thông tin đoàn khách” read-only, hiển thị loại hình tham quan, mục đích, nội dung, campus, thời gian, guest members, external support.
7. Bỏ block “Loại hình tham quan” khỏi Set up vì đã hiển thị ở thông tin đoàn khách.
8. Host chính read-only, lấy từ current_host_user_id. Không checkbox “Là tôi”, không dropdown đổi host, không nút xóa host.
9. Staff hỗ trợ IC:
   - Candidate phải là STAFF + STAFF, ACTIVE, cùng campus, department_type = IC, không phải host chính.
   - Có dropdown search.
   - Có conflict badge tính theo planned_start_at/planned_end_at của current visit instance.
   - Bấm Mời tạo visit_participants status = INVITED và gửi email có nút Chấp nhận/Từ chối.
10. Student:
   - Candidate phải là STUDENT ACTIVE cùng campus.
   - Có dropdown search theo tên/email/mã SV nếu có.
   - Có conflict badge.
   - Bấm Mời tạo visit_participants status = INVITED và gửi email có nút Chấp nhận/Từ chối.
11. Department:
   - Host chọn phòng ban GENERAL cùng campus.
   - Backend tự tìm Department Leader ACTIVE của phòng đó.
   - Nếu chưa có leader active, không cho mời.
   - Bấm Mời tạo visit_participants cho Department Leader status = INVITED và gửi email.
   - Email Department Leader có nút Chấp nhận, Từ chối, Gán nhân sự.
   - Chấp nhận/Từ chối xử lý qua email_action_tokens không cần login.
   - Gán nhân sự bắt buộc login hệ thống, không xử lý bằng public token.
12. Department Leader gán nhân sự:
   - Verify current user là DEPARTMENT + LEADER, đúng department, đúng campus.
   - Chỉ list DEPARTMENT + STAFF, ACTIVE, cùng department/campus.
   - Khi gán tạo participant row cho Department Staff participant_role = DEPT_SUPPORT, status = ASSIGNED.
   - Update row Department Leader sang ASSIGNED hoặc ACCEPTED kèm note “Đã gán cho ...”; ưu tiên ASSIGNED.
13. Email action token:
   - Không lưu raw token, chỉ token_hash.
   - Có expires_at.
   - Dùng một lần.
   - Nếu đã phản hồi, trả ALREADY_RESPONDED và không update lần hai.
   - Không đọc inbox Gmail/mail phản hồi tự do.
14. Frontend không được có nút tick/x để Host tự giả lập người được mời đồng ý/từ chối.
15. UI phải hiển thị status từ DB:
   - INVITED = Chờ phản hồi
   - ACCEPTED = Đã chấp nhận
   - DECLINED = Đã từ chối
   - ASSIGNED = Đã phân công / Đã gán nhân sự
   - REMOVED = Đã gỡ
16. Mọi thao tác phải có toast thông báo ở góc phải trên:
   - success khi mời/gỡ/gán thành công
   - error khi lỗi
   - warning khi trùng lịch, duplicate, phòng chưa có leader
   - info khi thao tác trung tính
17. Không thêm thư viện mới nếu project đã có toast hoặc có thể dùng lightweight toast hiện tại.
18. Build backend và frontend không lỗi.
```

---

## 20. Nghiệm thu chi tiết

### 20.1 Detail thông tin đơn

```text
[ ] Host mở VisitProcess đúng instance được gán.
[ ] “Thông tin người đăng ký” hiển thị đúng dữ liệu từ đơn thật.
[ ] “Thông tin đoàn khách” hiển thị đúng như preview trước khi duyệt.
[ ] Không còn hard-code Nguyễn Văn Tạo / Đại học FPT / 0987654321.
[ ] Loại hình tham quan hiển thị read-only trong thông tin đoàn.
[ ] Không còn block “Loại hình tham quan” trong Set up.
```

### 20.2 Host

```text
[ ] Host chính hiển thị đúng current_host_user_id.
[ ] Không đổi được Host.
[ ] Không có checkbox “Là tôi”.
[ ] Không có dropdown chọn Host thay thế.
```

### 20.3 Staff hỗ trợ IC

```text
[ ] Dropdown chỉ list Staff thường cùng campus, IC department.
[ ] Không list Staff Leader.
[ ] Không list Department/Student/Visitor.
[ ] Không list Host chính.
[ ] Có conflict badge.
[ ] Bấm Mời insert visit_participants status INVITED.
[ ] Email được gửi.
[ ] Toast success hiện góc phải trên.
```

### 20.4 Student

```text
[ ] Dropdown chỉ list Student active cùng campus.
[ ] Có conflict badge.
[ ] Bấm Mời insert visit_participants status INVITED.
[ ] Email được gửi.
[ ] Toast success hiện góc phải trên.
```

### 20.5 Department

```text
[ ] Dropdown chỉ list phòng GENERAL cùng campus.
[ ] Chọn phòng hiện Department Leader active.
[ ] Phòng chưa có leader thì không cho mời và hiện toast warning.
[ ] Bấm Mời insert visit_participants cho Department Leader status INVITED.
[ ] Email Department Leader có 3 nút: Chấp nhận, Từ chối, Gán nhân sự.
[ ] Toast success hiện góc phải trên.
```

### 20.6 Email action

```text
[ ] Staff bấm Chấp nhận qua email -> status ACCEPTED.
[ ] Staff bấm Từ chối qua email -> status DECLINED.
[ ] Student bấm Chấp nhận qua email -> status ACCEPTED.
[ ] Student bấm Từ chối qua email -> status DECLINED.
[ ] Department Leader bấm Chấp nhận qua email -> status ACCEPTED.
[ ] Department Leader bấm Từ chối qua email -> status DECLINED.
[ ] Bấm lại link cũ -> ALREADY_RESPONDED, không update lần hai.
[ ] Token hết hạn -> báo link hết hạn, không update.
[ ] Không đọc inbox Gmail.
```

### 20.7 Department assignment

```text
[ ] Department Leader bấm Gán nhân sự khi chưa login -> redirect login.
[ ] Login xong quay lại màn gán nhân sự.
[ ] Chỉ list Department Staff cùng phòng/campus.
[ ] Gán thành công tạo participant row status ASSIGNED.
[ ] UI Host hiển thị “Đã gán cho [Tên nhân sự]”.
[ ] Toast success hiện góc phải trên.
```

### 20.8 Toast

```text
[ ] Mời thành công có toast success.
[ ] Lỗi API có toast error.
[ ] Duplicate participant có toast warning.
[ ] Candidate trùng lịch có toast warning hoặc badge rõ.
[ ] Department chưa có leader có toast warning.
[ ] Gán nhân sự thành công có toast success.
[ ] Toast nằm góc phải trên, tự ẩn sau 4–5 giây, có nút đóng.
```

---

## 21. Ghi chú triển khai an toàn

```text
1. Làm backend API trước, sau đó mới bỏ mock frontend.
2. Không xóa UI cũ nếu API chưa xong; có thể feature flag hoặc fallback empty state.
3. Không để frontend tự set status participant.
4. Không để public token thực hiện thao tác cần login như gán nhân sự.
5. Không gửi email trước khi transaction insert participant thành công.
6. Nếu email gửi fail sau khi insert participant, cần lưu trạng thái email FAILED/QUEUED và báo toast phù hợp.
7. Mọi action mời/gỡ/gán cần audit log.
8. Mọi notification/email cần tránh lộ dữ liệu ngoài scope.
```

---

## 22. Kết luận chốt nghiệp vụ

Luồng đúng cần triển khai là:

```text
Host xem đơn gốc read-only
→ Host chuẩn bị Agenda như hiện tại
→ Host mời Staff IC / Student / Department Leader
→ Backend insert visit_participants + gửi email + tạo email_action_tokens
→ Staff/Student phản hồi qua email Chấp nhận/Từ chối
→ Department Leader phản hồi qua email hoặc đăng nhập để gán nhân sự
→ UI hiển thị status từ DB
→ Mọi thao tác có toast góc phải trên
```

Không làm:

```text
- Không cho Host đổi Host chính.
- Không cho Host tự tick đồng ý/từ chối thay người được mời.
- Không đọc inbox Gmail.
- Không xử lý “Gán nhân sự” bằng public email token.
- Không động vào Agenda đã ổn.
```
