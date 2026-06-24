# PROMPT_IMPLEMENT_PRE_APPROVAL_VISIT_REQUEST_REVIEW_PEMS

> Mục tiêu: cập nhật code PEMS để làm đúng chức năng **xem đơn đăng ký tham quan trước khi duyệt** trong màn **Quản lý tiếp khách**.  
> Đây là màn review read-only cho đơn khách gửi ở trạng thái chờ duyệt, khác với màn **đã duyệt · chờ chọn Host**.

---

## 0. Bối cảnh hiện tại

Dự án PEMS đang dùng:

```text
Backend: .NET 8 Web API + Clean Architecture + MediatR + EF Core + MySQL
Frontend: React + Vite + TypeScript + Tailwind
Database: MySQL 8, SQL v10, no dynamic permissions
Authorization: fixed role policy theo role_code, sub_role, campus/department scope, ownership, participant relationship và status
```

Trước khi code, bắt buộc đọc các tài liệu:

```text
docs/architecture/PROJECT_STRUCTURE_FULL.md
docs/database/DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
docs/PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
docs/PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_v10_FULL_UPDATED.md
```

Không dùng tài liệu legacy nếu mâu thuẫn với v10/canonical.

---

## 1. Chức năng cần làm

Triển khai chức năng:

```text
Xem đơn đăng ký tham quan trước khi duyệt
```

Khi user có quyền bấm icon mắt ở danh sách đơn chờ duyệt, hệ thống mở modal read-only hiển thị toàn bộ thông tin khách đã gửi để người duyệt đọc trước khi quyết định **Duyệt** hoặc **Từ chối**.

Trạng thái đúng của đơn trước duyệt:

```text
visit_requests.status = PENDING_APPROVAL
visit_request_campuses.status = WAITING_REQUEST_APPROVAL
```

Không xử lý các đơn đã duyệt/chờ host trong chức năng này:

```text
visit_requests.status = APPROVED
visit_request_campuses.status = WAITING_HOST_ASSIGNMENT
```

Các đơn “Đã duyệt · Chờ chọn Host” thuộc phase sau duyệt, không phải scope của prompt này.

---

## 2. Role và scope bắt buộc

### 2.1 Staff Leader

Staff Leader chỉ được xem đơn trước duyệt nếu thỏa tất cả điều kiện:

```text
currentUser.role_code = STAFF
currentUser.sub_role = LEADER
visit_requests.status = PENDING_APPROVAL
visit_requests.visit_scope = SINGLE_CAMPUS
visit_request_campuses.status = WAITING_REQUEST_APPROVAL
visit_request_campuses.campus_id = currentUser.primary_campus_id
```

Staff Leader không được xem:

```text
MULTI_CAMPUS đang chờ HO duyệt
SINGLE_CAMPUS của campus khác
Đơn không còn PENDING_APPROVAL
Instance không còn WAITING_REQUEST_APPROVAL
```

### 2.2 HO

HO chỉ được xem đơn trước duyệt nếu thỏa:

```text
currentUser.role_code = HO
visit_requests.status = PENDING_APPROVAL
visit_requests.visit_scope = MULTI_CAMPUS
```

HO không được xem/xử lý single-campus ở màn duyệt này.

### 2.3 Các role khác

Các role sau không được xem đơn trước duyệt ở màn quản lý nội bộ:

```text
ADMIN
STAFF + STAFF
DEPARTMENT + LEADER
DEPARTMENT + STAFF
STUDENT
VISITOR trên internal dashboard
```

Nếu gọi API trực tiếp, backend phải trả `403 Forbidden`.

Visitor nếu cần xem đơn của chính mình thì thuộc visitor portal, không phải màn duyệt nội bộ trong prompt này.

---

## 3. Backend cần cập nhật

### 3.1 Vị trí code cần kiểm tra

Dựa theo cấu trúc project hiện tại, kiểm tra và cập nhật các vùng sau:

```text
backend/PEMS.Api/Controllers/DelegationsController.cs
backend/PEMS.Api/Controllers/VisitRequestsController.cs

backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationDetails/
backend/PEMS.Application/Delegations/Commands/ProcessVisitRequest/
backend/PEMS.Application/Delegations/Commands/RejectVisitRequest/
backend/PEMS.Application/Common/Security/
backend/PEMS.Application/Common/Interfaces/ICurrentUserService.cs

backend/PEMS.Domain/Entities/Delegations/VisitRequest.cs
backend/PEMS.Domain/Entities/Delegations/VisitRequestCampus.cs
backend/PEMS.Domain/Entities/Delegations/VisitGuestMember.cs
backend/PEMS.Domain/Entities/Delegations/VisitAgenda.cs

backend/PEMS.Domain/Enums/VisitRequestStatus.cs
backend/PEMS.Domain/Enums/VisitInstanceStatus.cs
backend/PEMS.Domain/Enums/VisitScope.cs

backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs
backend/PEMS.Infrastructure/Persistence/Repositories/DelegationRepository.cs
```

Không bắt buộc phải tạo repository mới nếu query hiện tại đã dùng `IApplicationDbContext`. Nhưng controller không được query DbContext trực tiếp.

### 3.2 API đề xuất

Nếu chưa có endpoint riêng, tạo endpoint mới:

```http
GET /api/delegations/visit-requests/{visitRequestId}/pre-approval-review
```

Hoặc nếu project đang dùng route khác cho delegation detail, có thể dùng route hiện tại nhưng phải bảo đảm có mode/scope riêng cho pre-approval review.

Yêu cầu response trả về DTO read-only, không mutate dữ liệu.

### 3.3 Query/DTO đề xuất

Tạo mới nếu cần:

```text
backend/PEMS.Application/Delegations/Queries/ViewPreApprovalVisitRequestReview/
├── ViewPreApprovalVisitRequestReviewQuery.cs
├── ViewPreApprovalVisitRequestReviewQueryHandler.cs
└── ViewPreApprovalVisitRequestReviewDto.cs
```

DTO đề xuất:

```csharp
public sealed class ViewPreApprovalVisitRequestReviewDto
{
    public long VisitRequestId { get; set; }
    public string RequestCode { get; set; } = "";
    public string DelegationName { get; set; } = "";
    public string RequestStatus { get; set; } = "";
    public string VisitScope { get; set; } = "";
    public string? VisitType { get; set; }
    public string? VisitTypeOther { get; set; }
    public string? CreatedSource { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public ReviewRegistrantDto Registrant { get; set; } = new();
    public ReviewContactPersonDto ContactPerson { get; set; } = new();

    public string? Purpose { get; set; }
    public string? WorkingContent { get; set; }
    public string? Note { get; set; }

    public List<ReviewCampusScheduleDto> Campuses { get; set; } = new();
    public List<ReviewGuestMemberDto> GuestMembers { get; set; } = new();
    public List<ReviewGuestMemberDto> ExternalSupportMembers { get; set; } = new();
    public List<ReviewAgendaDto> Agendas { get; set; } = new();

    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
}
```

Các DTO con:

```csharp
public sealed class ReviewRegistrantDto
{
    public string? FullName { get; set; }
    public string? Organization { get; set; }
    public string? JobTitle { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Nationality { get; set; }
}

public sealed class ReviewContactPersonDto
{
    public string? FullName { get; set; }
    public string? Organization { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed class ReviewCampusScheduleDto
{
    public long VisitInstanceId { get; set; }
    public long CampusId { get; set; }
    public string CampusCode { get; set; } = "";
    public string CampusName { get; set; } = "";
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndAt { get; set; }
    public string InstanceStatus { get; set; } = "";
}

public sealed class ReviewGuestMemberDto
{
    public long GuestMemberId { get; set; }
    public string MemberType { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Organization { get; set; }
    public string? JobTitle { get; set; }
    public string? Nationality { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class ReviewAgendaDto
{
    public long AgendaId { get; set; }
    public int SequenceOrder { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Location { get; set; }
}
```

Nếu entity/schema đang dùng tên field khác, map theo schema thực tế, không tự bịa field mới.

### 3.4 Backend query logic

Handler phải:

```text
1. Load current user từ ICurrentUserService.
2. Resolve role_code/sub_role/currentUser.primary_campus_id.
3. Load visit request theo visitRequestId.
4. Include/join:
   - visit_request_campuses + campus
   - visit_guest_members
   - visit_agendas nếu có
5. Check request tồn tại, nếu không có trả 404.
6. Check status request = PENDING_APPROVAL.
7. Check scope theo role:
   - HO: visit_scope = MULTI_CAMPUS.
   - Staff Leader: visit_scope = SINGLE_CAMPUS và có campus instance đúng currentUser.primary_campus_id.
8. Check instance status = WAITING_REQUEST_APPROVAL.
9. Trả DTO read-only.
10. Không update trạng thái, không tạo participant/logistics/calendar/minutes.
```

Pseudo logic:

```csharp
var isHo = currentUser.RoleCode == RoleCodes.HO;

var isStaffLeader =
    currentUser.RoleCode == RoleCodes.Staff &&
    currentUser.SubRole == SubRoles.Leader;

if (!isHo && !isStaffLeader)
{
    throw new ForbiddenException("Bạn không có quyền xem đơn chờ duyệt này.");
}

if (visitRequest.Status != VisitRequestStatus.PendingApproval)
{
    throw new BusinessRuleException("Chỉ có thể xem đơn ở trạng thái chờ duyệt trong màn duyệt.");
}

if (isHo && visitRequest.VisitScope != VisitScope.MultiCampus)
{
    throw new ForbiddenException("HO chỉ được xem đơn liên cơ sở đang chờ duyệt.");
}

if (isStaffLeader)
{
    if (visitRequest.VisitScope != VisitScope.SingleCampus)
    {
        throw new ForbiddenException("Staff Leader không được xem đơn liên cơ sở trước khi HO duyệt.");
    }

    var instance = visitRequest.Campuses
        .SingleOrDefault(x => x.CampusId == currentUser.PrimaryCampusId);

    if (instance == null || instance.Status != VisitInstanceStatus.WaitingRequestApproval)
    {
        throw new ForbiddenException("Đơn không thuộc campus hoặc không ở trạng thái chờ duyệt.");
    }
}
```

### 3.5 Không được làm ở backend

Không được:

```text
- Không approve/reject trong API xem detail.
- Không assign host.
- Không tạo logistics.
- Không tạo visit_participants.
- Không tạo calendar_events.
- Không tạo minutes.
- Không dùng mock data.
- Không dùng permissions/role_permissions vì project đã bỏ dynamic permissions.
- Không hard-code role kiểu STAFF_LEADER; phải dùng STAFF + LEADER.
- Không cho Admin xem delegation nghiệp vụ.
```

---

## 4. Frontend cần cập nhật

### 4.1 Vị trí code cần kiểm tra

Dựa theo cấu trúc hiện tại:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitRequestDetail.tsx
frontend/pems-react/src/pages/dashboard/visit/VisitProcess.tsx

frontend/pems-react/src/components/modals/VisitDetailsModal.tsx
frontend/pems-react/src/components/modals/AssignHostModal.tsx
frontend/pems-react/src/components/modals/VisitingFormPopup.tsx

frontend/pems-react/src/features/delegations/api/delegationsApi.ts
frontend/pems-react/src/features/delegations/adapters/delegationsAdapter.ts
frontend/pems-react/src/features/delegations/hooks/useDelegations.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/features/delegations/config/visitRequestFilterConfig.ts

frontend/pems-react/src/shared/auth/resolveEffectiveRole.ts
frontend/pems-react/src/shared/constants/roles.ts
frontend/pems-react/src/shared/constants/v10Domain.ts
frontend/pems-react/src/shared/utils/dateUtils.ts
```

### 4.2 Component modal đề xuất

Tạo component mới nếu component hiện tại đang bị lẫn form submit/edit:

```text
frontend/pems-react/src/features/delegations/components/PreApprovalVisitRequestReviewModal.tsx
```

Nếu project convention không có `components/` trong `features/delegations`, có thể đặt:

```text
frontend/pems-react/src/components/modals/PreApprovalVisitRequestReviewModal.tsx
```

Không nên dùng `VisitingFormPopup.tsx` cho review vì đó là form đăng ký. Review phải là read-only.

### 4.3 TypeScript types

Cập nhật:

```text
frontend/pems-react/src/features/delegations/types/delegations.types.ts
```

Thêm types tương ứng DTO backend:

```ts
export type PreApprovalVisitRequestReview = {
  visitRequestId: number;
  requestCode: string;
  delegationName: string;
  requestStatus: string;
  visitScope: string;
  visitType?: string | null;
  visitTypeOther?: string | null;
  createdSource?: string | null;
  submittedAt?: string | null;

  registrant: ReviewRegistrant;
  contactPerson: ReviewContactPerson;

  purpose?: string | null;
  workingContent?: string | null;
  note?: string | null;

  campuses: ReviewCampusSchedule[];
  guestMembers: ReviewGuestMember[];
  externalSupportMembers: ReviewGuestMember[];
  agendas: ReviewAgenda[];

  canApprove: boolean;
  canReject: boolean;
};

export type ReviewRegistrant = {
  fullName?: string | null;
  organization?: string | null;
  jobTitle?: string | null;
  phone?: string | null;
  email?: string | null;
  nationality?: string | null;
};

export type ReviewContactPerson = {
  fullName?: string | null;
  organization?: string | null;
  phone?: string | null;
  email?: string | null;
};

export type ReviewCampusSchedule = {
  visitInstanceId: number;
  campusId: number;
  campusCode: string;
  campusName: string;
  plannedStartAt: string;
  plannedEndAt: string;
  instanceStatus: string;
};

export type ReviewGuestMember = {
  guestMemberId: number;
  memberType: string;
  fullName: string;
  organization?: string | null;
  jobTitle?: string | null;
  nationality?: string | null;
  displayOrder: number;
};

export type ReviewAgenda = {
  agendaId: number;
  sequenceOrder: number;
  title: string;
  description?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  location?: string | null;
};
```

### 4.4 API client

Cập nhật:

```text
frontend/pems-react/src/features/delegations/api/delegationsApi.ts
```

Thêm function:

```ts
export async function getPreApprovalVisitRequestReview(
  visitRequestId: number
): Promise<PreApprovalVisitRequestReview> {
  const res = await httpClient.get(
    `/delegations/visit-requests/${visitRequestId}/pre-approval-review`
  );

  return unwrapApiResponse(res);
}
```

Nếu project dùng base route khác hoặc wrapper response khác, phải tuân theo code hiện tại.

### 4.5 Hook

Cập nhật:

```text
frontend/pems-react/src/features/delegations/hooks/useDelegations.ts
```

Thêm state/function:

```ts
const [reviewModalOpen, setReviewModalOpen] = useState(false);
const [reviewLoading, setReviewLoading] = useState(false);
const [reviewError, setReviewError] = useState<string | null>(null);
const [reviewData, setReviewData] = useState<PreApprovalVisitRequestReview | null>(null);

const openPreApprovalReview = async (visitRequestId: number) => {
  setReviewModalOpen(true);
  setReviewLoading(true);
  setReviewError(null);

  try {
    const data = await delegationsApi.getPreApprovalVisitRequestReview(visitRequestId);
    setReviewData(data);
  } catch (error) {
    setReviewError(getUserFriendlyError(error));
  } finally {
    setReviewLoading(false);
  }
};
```

Không fetch detail bằng mock data.

### 4.6 List/tab/filter

Trong `VisitRequestManagement.tsx`, thêm hoặc chỉnh tab:

```text
Chờ duyệt
```

Tab này gọi list với filter:

```text
requestStatus = PENDING_APPROVAL
instanceStatus = WAITING_REQUEST_APPROVAL
```

Role-specific:

```text
Staff Leader:
- Mặc định thấy tab "Chờ duyệt" cho SINGLE_CAMPUS campus mình.

HO:
- Mặc định thấy tab "Chờ HO duyệt" cho MULTI_CAMPUS.

Các role khác:
- Không hiện tab duyệt trước nếu không có quyền.
```

Nếu list hiện tại đang hiển thị “Đã duyệt · Chờ chọn Host”, không dùng dataset đó để test pre-approval review.

### 4.7 Action icon mắt

Ở dòng list:

```tsx
<button
  type="button"
  title="Xem đơn trước khi duyệt"
  aria-label="Xem đơn trước khi duyệt"
  onClick={() => openPreApprovalReview(item.visitRequestId)}
>
  <Eye />
</button>
```

Chỉ dùng action review này khi:

```text
item.requestStatus = PENDING_APPROVAL
item.instanceStatus = WAITING_REQUEST_APPROVAL
item.canViewPreApprovalReview = true
```

Nếu item đã approved/waiting host, icon mắt có thể mở detail vận hành hiện tại, nhưng không gọi API review trước duyệt.

---

## 5. UI modal yêu cầu

### 5.1 Header

Modal title:

```text
Xem đơn đăng ký tham quan
```

Subtitle:

```text
Thông tin khách đã gửi trước khi duyệt
```

Header summary:

```text
Mã đơn
Trạng thái: Chờ duyệt
Phạm vi: Một cơ sở / Liên cơ sở
Ngày gửi
```

### 5.2 Section layout

Modal phải là read-only, chia section:

```text
1. Thông tin người đăng ký
2. Thông tin chuyến thăm
3. Cơ sở & thời gian dự kiến
4. Danh sách khách
5. Team hỗ trợ khách
6. Đầu mối liên hệ
7. Agenda / lịch trình nếu có
```

### 5.3 Read-only field style

Không dùng input editable, select, date picker. Dùng read-only card/value:

```tsx
<div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
  <p className="text-xs font-bold text-slate-500">Họ và tên</p>
  <p className="mt-1 text-sm font-semibold text-slate-900">
    {data.registrant.fullName || '-'}
  </p>
</div>
```

### 5.4 Bảng khách

Danh sách khách:

```text
STT
Họ và tên
Chức vụ
Đơn vị công tác
Quốc tịch
```

Nếu không có dữ liệu, hiển thị:

```text
Chưa có dữ liệu khách
```

Tuy nhiên backend submit form phải đảm bảo request có ít nhất một guest và một external support nếu business rule hiện tại yêu cầu.

### 5.5 Footer button

Nếu `canApprove = true` hoặc `canReject = true`:

```text
[Từ chối] [Duyệt] [Đóng]
```

Nếu không:

```text
[Đóng]
```

Button approve/reject chỉ gọi command approve/reject hiện có. Không viết logic approve/reject trong modal component.

---

## 6. Approve/Reject integration

Nếu approve/reject đã có command/API, chỉ gọi lại đúng API hiện tại.

Nếu chưa có đủ, kiểm tra các folder:

```text
backend/PEMS.Application/Delegations/Commands/ProcessVisitRequest/
backend/PEMS.Application/Delegations/Commands/RejectVisitRequest/
backend/PEMS.Application/Delegations/Commands/ApproveCrossCampusRequest/
```

Yêu cầu behavior:

### Staff Leader duyệt single-campus

```text
visit_requests.status: PENDING_APPROVAL -> APPROVED
visit_request_campuses.status: WAITING_REQUEST_APPROVAL -> WAITING_HOST_ASSIGNMENT
decision actor = STAFF_LEADER hoặc fixed audit equivalent hiện có
decision_note lưu ghi chú nếu có
```

### HO duyệt multi-campus

```text
visit_requests.status: PENDING_APPROVAL -> APPROVED
all related visit_request_campuses.status: WAITING_REQUEST_APPROVAL -> WAITING_HOST_ASSIGNMENT
coordinator_user_id có thể set Staff Leader từng campus nếu logic hiện tại đã có
```

Sau khi approve xong, list phải chuyển đơn khỏi tab “Chờ duyệt” và sang nhóm “Đã duyệt · Chờ chọn Host”.

### Reject

```text
visit_requests.status: PENDING_APPROVAL -> REJECTED
visit_request_campuses.status có thể giữ WAITING_REQUEST_APPROVAL hoặc chuyển theo logic hiện tại nếu schema/handler đã định nghĩa
decision_note bắt buộc nhập lý do từ chối
```

Không dùng `CANCELLED` cho request trước duyệt. Trước duyệt, nếu không chấp nhận thì dùng `REJECTED`.

---

## 7. Security và validation

Backend bắt buộc kiểm tra:

```text
- User đã đăng nhập.
- User status active.
- Role/subRole hợp lệ.
- Campus scope hợp lệ.
- Request tồn tại.
- Request đúng status PENDING_APPROVAL.
- Instance đúng status WAITING_REQUEST_APPROVAL.
- Không trả dữ liệu ngoài scope.
```

Frontend không được là lớp bảo mật duy nhất.

Khi lỗi:

```text
401: session hết hạn
403: không có quyền hoặc ngoài scope
404: không tìm thấy đơn
409/400: đơn không còn ở trạng thái chờ duyệt
500: lỗi hệ thống
```

Frontend hiển thị message thân thiện, không crash/trắng màn hình.

---

## 8. Không được làm

Không được:

```text
- Không sửa schema SQL nếu không thật sự thiếu field.
- Không thêm dynamic permissions.
- Không tạo permissions/role_permissions.
- Không dùng mock data.
- Không đổi role/subRole chuẩn.
- Không cho Admin xem/duyệt delegation nghiệp vụ.
- Không cho Staff thường, Department, Student xem đơn trước duyệt.
- Không cho Staff Leader xem multi-campus trước HO duyệt.
- Không biến modal review thành form edit.
- Không gọi API approve/reject khi chỉ bấm xem.
- Không tạo logistics/participant/host/calendar/minutes trước khi duyệt.
- Không hard-code theo email hoặc tên user.
```

---

## 9. Test cases bắt buộc

### 9.1 Backend tests

Cập nhật hoặc thêm test trong:

```text
tests/PEMS.ApplicationTests/Delegations/ViewGuestDelegationDetailsQueryTests.cs
tests/PEMS.ApplicationTests/Delegations/ViewGuestDelegationListQueryTests.cs
```

Nếu tạo query mới, thêm:

```text
tests/PEMS.ApplicationTests/Delegations/ViewPreApprovalVisitRequestReviewQueryTests.cs
```

Test cases:

```text
1. Staff Leader HN xem được đơn SINGLE_CAMPUS HN đang PENDING_APPROVAL + WAITING_REQUEST_APPROVAL.
2. Staff Leader HN không xem được đơn SINGLE_CAMPUS HCM.
3. Staff Leader HN không xem được MULTI_CAMPUS đang chờ HO duyệt.
4. HO xem được MULTI_CAMPUS đang PENDING_APPROVAL.
5. HO không xem được SINGLE_CAMPUS.
6. Admin gọi API review trả 403.
7. IC Staff gọi API review trả 403.
8. Department Leader/Staff gọi API review trả 403.
9. Student gọi API review trả 403.
10. Request APPROVED + WAITING_HOST_ASSIGNMENT gọi review trước duyệt trả lỗi trạng thái.
11. API review không thay đổi status request/campus.
12. DTO trả guestMembers và externalSupportMembers tách riêng đúng member_type.
```

### 9.2 Frontend manual test

```text
1. Login Staff Leader HN.
2. Vào /dashboard/visit.
3. Chọn tab Chờ duyệt.
4. Thấy các đơn SINGLE_CAMPUS HN đang chờ duyệt.
5. Bấm icon mắt.
6. Modal mở, hiển thị read-only, không có input/dropdown/date picker editable.
7. Footer có Từ chối / Duyệt / Đóng.
8. Bấm Đóng không đổi dữ liệu.
9. Duyệt thành công thì đơn rời tab Chờ duyệt và sang trạng thái Đã duyệt · Chờ chọn Host.
10. Login HO, chỉ thấy đơn MULTI_CAMPUS chờ duyệt.
11. Login IC Staff, không thấy tab Chờ duyệt và không gọi được API review.
```

---

## 10. Build/check bắt buộc

Sau khi sửa:

```bash
dotnet build
```

Frontend:

```bash
cd frontend/pems-react
npm run build
```

Nếu có test project chạy được:

```bash
dotnet test
```

Không báo hoàn thành nếu build fail. Nếu không chạy được test/build do môi trường thiếu dependency, phải ghi rõ lý do và log lỗi.

---

## 11. Output báo cáo sau khi code

Sau khi hoàn thành, báo cáo theo format:

```text
1. Summary
- Đã triển khai chức năng xem đơn trước khi duyệt.
- Modal read-only, role/scope đúng canonical v10.

2. Files changed
Backend:
- ...
Frontend:
- ...

3. API
- Method/path:
- Request:
- Response DTO:

4. Role/scope implemented
- HO:
- Staff Leader:
- Forbidden roles:

5. UI behavior
- Tab/filter:
- Modal sections:
- Footer actions:

6. Tests/build
- dotnet build:
- npm run build:
- dotnet test:

7. Known limitations / next step
- ...
```

---

## 12. Acceptance criteria cuối cùng

Chức năng chỉ đạt khi:

```text
- Staff Leader chỉ xem được đơn single-campus của campus mình trước duyệt.
- HO chỉ xem được đơn multi-campus trước duyệt.
- Các role khác bị chặn.
- Modal là read-only review, không phải form edit.
- Dữ liệu lấy từ DB thật.
- Không lộ dữ liệu ngoài scope.
- Không mutate status khi chỉ xem.
- Approve/reject nếu có button phải dùng command đúng, không viết logic tắt trong frontend.
- Backend build pass.
- Frontend build pass.
```
