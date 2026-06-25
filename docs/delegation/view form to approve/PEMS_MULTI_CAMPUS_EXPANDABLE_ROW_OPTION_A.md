# PEMS — Phương án A: Multi-campus Expandable Row trong danh sách đơn liên cơ sở

> Mục đích: Đặc tả UI/UX + Backend/Frontend implementation guide cho việc hiển thị đơn liên cơ sở theo dạng **row cha + accordion campus con** trên màn hình `/dashboard/visit`.
>
> Áp dụng cho:
> - HO xem/quản lý danh sách đơn liên cơ sở.
> - Visitor xem danh sách đơn của chính mình.
> - Có thể mở rộng sau cho Staff Leader nếu cần xem campus instance thuộc campus mình.

---

## 1. Bối cảnh nghiệp vụ

Trong PEMS, một đơn liên cơ sở là **1 visit request tổng** nhưng có nhiều campus instance trong bảng `visit_request_campuses`.

Ví dụ:

```text
Visit Request: HO-approved waiting host assignment tour
Visit scope: MULTI_CAMPUS
Campus instances:
- Hà Nội: WAITING_HOST_ASSIGNMENT
- TP.HCM: ASSIGNED
- Đà Nẵng: BEFORE_VISIT
- Cần Thơ: CANCELLED
- Quy Nhơn: CLOSED
```

Vấn đề hiện tại nếu chỉ hiển thị 1 dòng tổng:

```text
Người dùng không biết từng campus đang ở tiến trình nào.
```

Vấn đề nếu tách mỗi campus thành 1 dòng ngang hàng:

```text
Người dùng dễ hiểu nhầm đây là nhiều đơn khác nhau.
Danh sách bị dài và nhiễu.
```

Vì vậy chọn phương án:

```text
1 đơn liên cơ sở = 1 row cha.
Bấm mở rộng row cha = hiển thị danh sách campus con ngay bên dưới.
```

---

## 2. Mục tiêu của phương án A

### 2.1. Mục tiêu UI

```text
[ ] Danh sách chính vẫn gọn, mỗi đơn chỉ chiếm 1 dòng cha.
[ ] Người dùng có thể mở rộng để xem tiến trình từng campus.
[ ] HO/Visitor có thể bấm vào campus cụ thể để xem chi tiết campus instance.
[ ] Không nhầm giữa “đơn tổng” và “campus instance”.
[ ] Không làm vỡ layout desktop/tablet/mobile.
```

### 2.2. Mục tiêu nghiệp vụ

```text
[ ] HO xem được tiến trình từng campus của đơn liên cơ sở trong scope HO.
[ ] Visitor xem được tiến trình từng campus của đơn do mình gửi.
[ ] Action theo từng campus phải dựa trên status code thật, không dựa vào statusText.
[ ] Không expose dữ liệu nội bộ không được phép cho Visitor.
[ ] Không thay đổi schema nếu không bắt buộc.
```

---

## 3. Khái niệm UI

### 3.1. Row cha — Visit Request tổng

Row cha đại diện cho `visit_requests`.

Hiển thị:

```text
- STT
- Tên đoàn
- Tổ chức đăng ký
- Host tổng quan: nếu chưa có thì “Đang phân công” hoặc “-”
- Số lượng campus: “Liên cơ sở (5)”
- Lịch tổng: từ ngày sớm nhất đến ngày muộn nhất của các campus
- Trạng thái tổng request
- Hành động tổng: xem chi tiết đơn, mở rộng campus
```

Ví dụ:

```text
HO-approved waiting host assignment tour
SeoulTech Global Engagement Center
Host: Đang phân công | Cơ sở: 5 cơ sở
[Liên cơ sở · HO đã duyệt]

Từ: 14:00 21/07/2026
Đến: 21:00 22/07/2026

Status: Đã duyệt
Actions: Xem đơn tổng · Mở rộng campus
```

### 3.2. Row con — Campus instance

Row con đại diện cho từng bản ghi `visit_request_campuses`.

Hiển thị sau khi mở rộng row cha:

```text
- Campus name / campus code
- Lịch tiếp riêng tại campus
- Host campus
- Trạng thái campus instance
- Action riêng: xem chi tiết campus, hủy nếu được phép, xem lý do nếu bị hủy/từ chối
```

Ví dụ:

```text
Tiến trình theo từng cơ sở

| Campus | Lịch tiếp | Host | Trạng thái campus | Hành động |
|---|---|---|---|---|
| Hà Nội | 21/07 14:00 - 21:00 | Đang phân công | Chờ phân công host | Xem |
| TP.HCM | 22/07 09:00 - 11:00 | Nguyễn Văn A | Đã phân công | Xem |
| Đà Nẵng | 22/07 14:00 - 16:00 | Trần Văn B | Trước tiếp khách | Xem |
| Cần Thơ | 23/07 08:00 - 10:00 | - | Đã hủy | Xem lý do |
| Quy Nhơn | 23/07 14:00 - 16:00 | Lê Văn C | Đã đóng | Xem |
```

---

## 4. Luồng tương tác chính

### 4.1. Mở rộng campus con

```text
[U] User bấm vào badge “Liên cơ sở (n)” hoặc icon chevron.
[S] Frontend mở accordion ngay dưới row cha.
[S] Nếu campusProgressItems đã có trong list response, render ngay.
[S] Nếu campusProgressItems chưa load, gọi API lấy campus progress theo visitRequestId.
[S] Hiển thị loading nhỏ trong vùng accordion nếu đang fetch.
```

Khuyến nghị:

```text
Giai đoạn đầu nên để backend trả campusProgressItems ngay trong list DTO để UI nhanh và ít loading.
Nếu payload quá lớn sau này mới chuyển sang lazy-load.
```

### 4.2. Đóng accordion

```text
[U] User bấm lại badge/icon chevron.
[S] Frontend thu gọn campus list.
[S] Giữ nguyên filter/sort/page hiện tại.
```

### 4.3. Xem chi tiết đơn tổng

```text
[U] User bấm icon Eye ở row cha.
[S] Frontend mở modal/page “Chi tiết đơn liên cơ sở”.
[S] Nội dung là thông tin form/request tổng.
```

Chi tiết đơn tổng gồm:

```text
- Mã đơn
- Tên đoàn
- Tổ chức
- Người đăng ký
- Email/số điện thoại đăng ký
- Mục đích thăm
- Visit scope
- Danh sách khách
- Danh sách campus đăng ký
- Trạng thái tổng
- Lý do từ chối/hủy nếu có
```

### 4.4. Xem chi tiết campus cụ thể

```text
[U] User mở accordion.
[U] User bấm “Xem” ở một campus con.
[S] Frontend mở modal/page “Chi tiết tiếp khách tại [Campus]”.
```

Chi tiết campus gồm:

```text
- Campus
- Lịch tiếp riêng
- Host campus
- Trạng thái campus instance
- Agenda riêng của campus
- Participants liên quan nếu role được phép
- Logistics nếu role được phép
- Lý do hủy nếu campus instance bị hủy
```

---

## 5. Phân quyền và visibility

### 5.1. HO

HO xem:

```text
- Chỉ các request MULTI_CAMPUS.
- Tất cả campus instances thuộc request liên cơ sở trong scope HO.
- Trạng thái từng campus.
- Host từng campus nếu đã phân công.
- Timeline/lịch tiếp từng campus.
```

HO không xem:

```text
- Single-campus request nếu rule hiện tại không cho HO xem.
- Dữ liệu private ngoài phạm vi nghiệp vụ.
```

HO action gợi ý:

```text
PENDING_APPROVAL:
- Xem đơn tổng.
- Duyệt/từ chối request tổng nếu UC hiện có cho phép.
- Có thể mở campus con để tham khảo campus đăng ký, nhưng chưa có lifecycle riêng ở campus.

APPROVED:
- Xem đơn tổng.
- Xem tiến trình từng campus.
- Không tự hủy campus sau approved nếu rule hiện tại chưa cho phép.

REJECTED:
- Xem đơn tổng.
- Xem lý do từ chối.

CANCELLED:
- Xem đơn tổng.
- Xem lý do hủy nếu có.
```

### 5.2. Visitor

Visitor xem:

```text
- Chỉ request do chính Visitor gửi hoặc được liên kết hợp lệ.
- Danh sách campus của request mình.
- Trạng thái xử lý từng campus.
- Host nếu được công khai/đã phân công.
- Agenda nếu được phép hiển thị.
- Lý do từ chối/hủy liên quan.
```

Visitor không xem:

```text
- Ghi chú nội bộ.
- Logistics/task nội bộ nếu không được phép.
- Participants nội bộ không cần công khai.
- Audit/private decision data ngoài message cần hiển thị.
```

Visitor action gợi ý:

```text
PENDING_APPROVAL:
- Xem đơn tổng.
- Không hiện nút hủy nếu rule hiện tại chưa hỗ trợ “rút yêu cầu trước duyệt”.

APPROVED + campus instance in WAITING_HOST_ASSIGNMENT / ASSIGNED / BEFORE_VISIT:
- Xem đơn tổng.
- Mở campus con.
- Hủy lịch thăm nếu rule Visitor self-service cancel hiện có cho phép.

REJECTED:
- Xem đơn tổng.
- Xem lý do từ chối nếu có decisionNote.
- Không hiện hủy.

CANCELLED:
- Xem đơn tổng.
- Xem lý do hủy nếu có.
- Không hiện hủy.

DURING_VISIT / AFTER_VISIT / CLOSED:
- Xem.
- Không hiện hủy.
```

---

## 6. Action matrix đề xuất

### 6.1. Row cha

| Role | requestStatus | Action row cha |
|---|---|---|
| HO | PENDING_APPROVAL | Xem đơn tổng, Duyệt, Từ chối, Mở rộng campus |
| HO | APPROVED | Xem đơn tổng, Mở rộng campus |
| HO | REJECTED | Xem đơn tổng, Xem lý do từ chối, Mở rộng campus nếu cần |
| HO | CANCELLED | Xem đơn tổng, Xem lý do hủy, Mở rộng campus nếu cần |
| Visitor | PENDING_APPROVAL | Xem đơn tổng, Mở rộng campus |
| Visitor | APPROVED | Xem đơn tổng, Mở rộng campus |
| Visitor | REJECTED | Xem đơn tổng, Xem lý do từ chối |
| Visitor | CANCELLED | Xem đơn tổng, Xem lý do hủy |

### 6.2. Row con campus instance

| Role | instanceStatus | Action campus con |
|---|---|---|
| HO | WAITING_REQUEST_APPROVAL | Xem campus đăng ký |
| HO | WAITING_HOST_ASSIGNMENT | Xem campus |
| HO | ASSIGNED | Xem campus |
| HO | BEFORE_VISIT | Xem campus |
| HO | DURING_VISIT | Xem campus |
| HO | AFTER_VISIT | Xem campus |
| HO | CLOSED | Xem campus |
| HO | CANCELLED | Xem campus, Xem lý do hủy |
| Visitor | WAITING_REQUEST_APPROVAL | Xem campus đăng ký |
| Visitor | WAITING_HOST_ASSIGNMENT | Xem campus, Hủy nếu request APPROVED |
| Visitor | ASSIGNED | Xem campus, Hủy nếu request APPROVED |
| Visitor | BEFORE_VISIT | Xem campus, Hủy nếu request APPROVED |
| Visitor | DURING_VISIT | Xem campus |
| Visitor | AFTER_VISIT | Xem campus |
| Visitor | CLOSED | Xem campus |
| Visitor | CANCELLED | Xem campus, Xem lý do hủy |

---

## 7. Backend DTO đề xuất

### 7.1. List item DTO

Backend list response nên có đủ dữ liệu để frontend render accordion mà không đoán bằng text.

```csharp
public sealed class VisitRequestManagementItemDto
{
    public long VisitRequestId { get; init; }
    public string RequestCode { get; init; } = string.Empty;
    public string DelegationName { get; init; } = string.Empty;
    public string RegistrantOrganization { get; init; } = string.Empty;

    public string VisitScope { get; init; } = string.Empty; // SINGLE_CAMPUS / MULTI_CAMPUS

    public string RequestStatus { get; init; } = string.Empty; // PENDING_APPROVAL / APPROVED / REJECTED / CANCELLED
    public string RequestStatusText { get; init; } = string.Empty;

    public DateTime? PlannedStartAt { get; init; }
    public DateTime? PlannedEndAt { get; init; }

    public int CampusCount { get; init; }
    public bool IsMultiCampus { get; init; }

    public string? DecisionNote { get; init; }
    public long? DecidedBy { get; init; }
    public string? DecidedByName { get; init; }
    public DateTime? DecidedAt { get; init; }
    public string? DecisionActorRole { get; init; }

    public string? CancellationReason { get; init; }
    public long? CancelledBy { get; init; }
    public string? CancelledByName { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationActorType { get; init; }
    public string? CancellationSource { get; init; }

    public bool CanExpandCampuses { get; init; }
    public bool CanViewRequestDetail { get; init; }
    public bool CanViewRejectReason { get; init; }
    public bool CanViewCancelReason { get; init; }

    public IReadOnlyList<CampusProgressItemDto> CampusProgressItems { get; init; } = [];
}
```

### 7.2. Campus progress item DTO

```csharp
public sealed class CampusProgressItemDto
{
    public long VisitInstanceId { get; init; }
    public long CampusId { get; init; }
    public string CampusCode { get; init; } = string.Empty;
    public string CampusName { get; init; } = string.Empty;

    public DateTime? PlannedStartAt { get; init; }
    public DateTime? PlannedEndAt { get; init; }

    public string InstanceStatus { get; init; } = string.Empty;
    public string InstanceStatusText { get; init; } = string.Empty;

    public long? HostUserId { get; init; }
    public string? HostName { get; init; }

    public string? CancellationReason { get; init; }
    public long? CancelledBy { get; init; }
    public string? CancelledByName { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationActorType { get; init; }
    public string? CancellationSource { get; init; }

    public bool CanViewCampusDetail { get; init; }
    public bool CanCancelCampusVisit { get; init; }
    public bool CanViewCancelReason { get; init; }
}
```

### 7.3. Vì sao nên trả boolean action từ backend

Frontend không nên tự suy luận quá nhiều từ role/status vì dễ lệch nghiệp vụ.

Backend nên trả:

```text
CanExpandCampuses
CanViewRequestDetail
CanViewRejectReason
CanViewCancelReason
CanViewCampusDetail
CanCancelCampusVisit
```

Frontend chỉ render theo các boolean này.

Ưu điểm:

```text
- Ít lỗi permission ở frontend.
- Dễ test.
- Backend vẫn là source of truth cho scope/action.
- Không dùng statusText để gate action.
```

---

## 8. Query/backend implementation notes

### 8.1. Query data source

Backend cần query từ:

```text
visit_requests
visit_request_campuses
campuses
users as host user
users as decided/cancelled user nếu cần hiển thị tên
```

### 8.2. Scope bắt buộc

HO:

```text
visit_requests.visit_scope = MULTI_CAMPUS
```

Visitor:

```text
visit_requests.created_by / submitted_by / registrant_user_id = currentUserId
hoặc relationship hợp lệ theo implementation hiện có.
```

Không được vì mở accordion mà nới scope.

### 8.3. Avoid N+1 query

Không query từng campus con trong vòng lặp.

Cách làm:

```text
1. Query page row cha theo filter/sort/pagination.
2. Lấy list visitRequestIds của page hiện tại.
3. Batch query all campus instances WHERE visit_request_id IN (...)
4. Batch query host names nếu cần.
5. Group campusProgressItems trong memory theo visitRequestId.
6. Map vào DTO cha.
```

### 8.4. Pagination

Pagination tính theo số request cha, không tính theo campus con.

Ví dụ:

```text
Page size = 10
Có 10 visit requests cha.
Mỗi request có thể mở 2-5 campus con.
```

Không paginate campus con trong phương án A vì số campus PEMS hiện ít.

---

## 9. Frontend type đề xuất

```ts
export type VisitRequestStatus =
  | 'PENDING_APPROVAL'
  | 'APPROVED'
  | 'REJECTED'
  | 'CANCELLED';

export type VisitInstanceStatus =
  | 'WAITING_REQUEST_APPROVAL'
  | 'WAITING_HOST_ASSIGNMENT'
  | 'ASSIGNED'
  | 'BEFORE_VISIT'
  | 'DURING_VISIT'
  | 'AFTER_VISIT'
  | 'CLOSED'
  | 'CANCELLED';

export interface CampusProgressItem {
  visitInstanceId: number;
  campusId: number;
  campusCode: string;
  campusName: string;

  plannedStartAt?: string | null;
  plannedEndAt?: string | null;

  instanceStatus: VisitInstanceStatus;
  instanceStatusText: string;

  hostUserId?: number | null;
  hostName?: string | null;

  cancellationReason?: string | null;
  cancelledBy?: number | null;
  cancelledByName?: string | null;
  cancelledAt?: string | null;
  cancellationActorType?: string | null;
  cancellationSource?: string | null;

  canViewCampusDetail: boolean;
  canCancelCampusVisit: boolean;
  canViewCancelReason: boolean;
}

export interface VisitRequestManagementItem {
  visitRequestId: number;
  requestCode: string;
  delegationName: string;
  registrantOrganization: string;

  visitScope: 'SINGLE_CAMPUS' | 'MULTI_CAMPUS';

  requestStatus: VisitRequestStatus;
  requestStatusText: string;

  plannedStartAt?: string | null;
  plannedEndAt?: string | null;

  campusCount: number;
  isMultiCampus: boolean;

  decisionNote?: string | null;
  decidedBy?: number | null;
  decidedByName?: string | null;
  decidedAt?: string | null;
  decisionActorRole?: string | null;

  cancellationReason?: string | null;
  cancelledBy?: number | null;
  cancelledByName?: string | null;
  cancelledAt?: string | null;
  cancellationActorType?: string | null;
  cancellationSource?: string | null;

  canExpandCampuses: boolean;
  canViewRequestDetail: boolean;
  canViewRejectReason: boolean;
  canViewCancelReason: boolean;

  campusProgressItems: CampusProgressItem[];
}
```

---

## 10. Frontend implementation notes

### 10.1. State quản lý expanded rows

Trong `VisitRequestManagement.tsx`:

```tsx
const [expandedRequestIds, setExpandedRequestIds] = useState<Set<number>>(new Set());

const toggleExpanded = (visitRequestId: number) => {
  setExpandedRequestIds((prev) => {
    const next = new Set(prev);
    if (next.has(visitRequestId)) {
      next.delete(visitRequestId);
    } else {
      next.add(visitRequestId);
    }
    return next;
  });
};
```

Nếu muốn chỉ mở 1 row tại một thời điểm:

```tsx
const [expandedRequestId, setExpandedRequestId] = useState<number | null>(null);

const toggleExpanded = (visitRequestId: number) => {
  setExpandedRequestId((current) =>
    current === visitRequestId ? null : visitRequestId
  );
};
```

Khuyến nghị giai đoạn đầu:

```text
Chỉ mở 1 row tại một thời điểm để bảng gọn hơn.
```

### 10.2. Render row cha

Pseudocode:

```tsx
{rows.map((row, index) => {
  const isExpanded = expandedRequestId === row.visitRequestId;

  return (
    <Fragment key={row.visitRequestId}>
      <VisitRequestParentRow
        row={row}
        index={index}
        isExpanded={isExpanded}
        onToggleExpand={() => toggleExpanded(row.visitRequestId)}
        onViewRequest={() => openRequestDetail(row)}
      />

      {isExpanded && row.canExpandCampuses && (
        <CampusProgressAccordion
          items={row.campusProgressItems}
          onViewCampus={(item) => openCampusDetail(row, item)}
          onCancelCampus={(item) => openCancelModal(row, item)}
        />
      )}
    </Fragment>
  );
})}
```

### 10.3. Không gate bằng statusText

Sai:

```tsx
row.statusText === 'Đã duyệt'
row.statusText === 'Từ chối'
```

Đúng:

```tsx
row.requestStatus === 'APPROVED'
row.requestStatus === 'REJECTED'
campus.instanceStatus === 'WAITING_HOST_ASSIGNMENT'
```

Tốt nhất:

```tsx
row.canViewRejectReason
campus.canCancelCampusVisit
```

---

## 11. UI style đề xuất

### 11.1. Desktop table

Row cha:

```text
- Giữ layout bảng hiện tại.
- Badge “Liên cơ sở (n)” có thể click được.
- Icon chevron nằm cạnh badge hoặc ở cột action.
- Row đang expanded có background nhẹ blue-50/slate-50.
```

Campus accordion:

```text
- Nằm trong một row full-width bên dưới row cha.
- Background slate-50.
- Border top/bottom nhẹ.
- Có title “Tiến trình theo từng cơ sở”.
- Dùng grid hoặc mini-table.
```

Ví dụ Tailwind layout:

```tsx
<div className="border-b border-slate-200 bg-slate-50 px-6 py-4">
  <div className="mb-3 flex items-center justify-between">
    <h4 className="text-sm font-bold text-[#004c91]">
      Tiến trình theo từng cơ sở
    </h4>
    <span className="text-xs font-semibold text-slate-500">
      {items.length} cơ sở
    </span>
  </div>

  <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
    {/* campus rows */}
  </div>
</div>
```

### 11.2. Mobile/tablet

Không cố nhét table con vào mobile.

Mobile nên render campus con dạng card:

```text
[Hà Nội]      [Chờ phân công host]
21/07/2026 14:00 - 21:00
Host: Đang phân công
[Xem] [Hủy nếu được phép]
```

---

## 12. Empty/loading/error state

### 12.1. Không có campus con

```text
Chưa có thông tin campus cho đơn này.
```

### 12.2. Đang load campus con nếu lazy-load

```text
Đang tải tiến trình theo cơ sở...
```

### 12.3. Lỗi load campus con

```text
Không thể tải tiến trình theo cơ sở. Vui lòng thử lại.
```

Không hiển thị raw technical error.

---

## 13. Điều kiện không làm trong phase này

Không làm:

```text
[ ] Không tạo route hoàn toàn mới nếu modal hiện tại đủ dùng.
[ ] Không đổi schema DB nếu DTO hiện tại có thể lấy từ bảng sẵn có.
[ ] Không biến mỗi campus thành row cha ngang hàng.
[ ] Không thêm action nghiệp vụ mới ngoài action hiện có.
[ ] Không cho Visitor xem dữ liệu nội bộ.
[ ] Không dùng statusText để xử lý logic.
```

---

## 14. Acceptance criteria

### 14.1. UI behavior

```text
[ ] Multi-campus request hiển thị 1 row cha.
[ ] Row cha có badge/click target “Liên cơ sở (n)” hoặc chevron.
[ ] Bấm mở rộng hiển thị danh sách campus con ngay bên dưới.
[ ] Bấm lại thì thu gọn.
[ ] Mỗi campus con hiển thị đúng campus, lịch, host, trạng thái.
[ ] Single-campus request không cần accordion hoặc không hiện nút mở rộng.
[ ] Pagination vẫn tính theo request cha.
```

### 14.2. HO

```text
[ ] HO chỉ thấy multi-campus requests.
[ ] HO mở rộng được campus con của request trong scope.
[ ] HO xem được trạng thái từng campus.
[ ] HO bấm row cha thì xem chi tiết đơn tổng.
[ ] HO bấm campus con thì xem chi tiết campus instance.
```

### 14.3. Visitor

```text
[ ] Visitor chỉ thấy request của mình.
[ ] Visitor mở rộng được campus con của request mình.
[ ] Visitor không thấy dữ liệu nội bộ không được phép.
[ ] Visitor bấm row cha thì xem chi tiết đơn tổng.
[ ] Visitor bấm campus con thì xem chi tiết campus ở mức public/visitor-safe.
```

### 14.4. Action/security

```text
[ ] Frontend không gate action bằng statusText.
[ ] Backend vẫn enforce scope, không tin frontend.
[ ] Không có N+1 query nghiêm trọng.
[ ] Không có raw exception hiển thị trên UI.
[ ] Build backend thành công.
[ ] Build frontend thành công.
```

---

## 15. Manual test checklist

### HO account

```text
[ ] Login HO.
[ ] Mở /dashboard/visit.
[ ] Kiểm tra list chỉ có multi-campus.
[ ] Chọn một request có 2+ campus.
[ ] Bấm “Liên cơ sở (n)”.
[ ] Accordion mở và hiển thị đủ campus.
[ ] Kiểm tra mỗi campus có status riêng.
[ ] Bấm xem row cha: mở chi tiết đơn tổng.
[ ] Bấm xem campus con: mở chi tiết campus.
[ ] Thu gọn accordion.
```

### Visitor account

```text
[ ] Login Visitor.
[ ] Mở /dashboard/visit.
[ ] Kiểm tra chỉ thấy request của chính mình.
[ ] Bấm mở rộng request multi-campus.
[ ] Thấy danh sách campus con visitor-safe.
[ ] Không thấy ghi chú nội bộ/logistics private.
[ ] Với request REJECTED, xem lý do từ chối đúng.
[ ] Với request CANCELLED/campus CANCELLED, xem lý do hủy đúng nếu có.
```

### Responsive

```text
[ ] Desktop 1366px: bảng không tràn ngang.
[ ] Tablet 1024px: accordion không vỡ layout.
[ ] Mobile: campus con chuyển thành card list.
```

---

## 16. Prompt cho AI Agent code

```text
Bạn là Senior Full-stack Engineer cho PEMS.

Hãy triển khai phương án A: Multi-campus expandable row trên màn hình /dashboard/visit.

Mục tiêu:
- Mỗi request liên cơ sở chỉ hiển thị 1 row cha.
- Bấm badge “Liên cơ sở (n)” hoặc chevron sẽ mở accordion bên dưới row cha.
- Accordion hiển thị danh sách campus instances của request đó.
- Mỗi campus con hiển thị campus, lịch tiếp, host, instanceStatus, action xem chi tiết.
- HO và Visitor đều dùng được nhưng phải đúng scope.
- Không dùng statusText để gate action.
- Backend vẫn enforce scope và trả boolean action nếu có thể.

Đọc trước:
- DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
- PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
- VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
- PROJECT_STRUCTURE_FULL.md

Backend:
- Kiểm tra ViewGuestDelegationListDto.cs và ViewGuestDelegationListQueryHandler.cs.
- Bổ sung CampusProgressItems nếu chưa có.
- Batch query campus instances theo visitRequestIds của page hiện tại.
- Không tạo N+1 query.
- Scope HO: chỉ MULTI_CAMPUS.
- Scope Visitor: chỉ request của chính mình.
- Trả các boolean action: canExpandCampuses, canViewRequestDetail, canViewCampusDetail, canCancelCampusVisit, canViewRejectReason, canViewCancelReason nếu phù hợp.

Frontend:
- Kiểm tra delegations.types.ts.
- Cập nhật VisitRequestManagement.tsx.
- Thêm state expandedRequestId hoặc expandedRequestIds.
- Render accordion ngay dưới row cha.
- Desktop: mini-table/grid campus con.
- Mobile: card list campus con.
- Không duplicate action icon.
- Không dùng statusText để xử lý logic.
- statusText chỉ dùng render badge.

Không làm:
- Không thêm DB column nếu không cần.
- Không đổi business flow approve/reject/cancel.
- Không expose dữ liệu private cho Visitor.
- Không refactor sâu toàn màn nếu không cần.
- Không phá build TypeScript.

Build:
- dotnet build
- npm run build

Báo cáo:
1. Files read
2. Files changed
3. Backend DTO/query changes
4. Frontend UI changes
5. Scope/action rules
6. Build result
7. Manual test checklist
```

---

## 17. Kết luận

Phương án A là hướng phù hợp nhất cho PEMS vì giữ được mô hình nghiệp vụ:

```text
1 request tổng
→ nhiều campus instances
→ mỗi campus có tiến trình riêng
```

UI sẽ dễ hiểu hơn cho HO và Visitor:

```text
- Danh sách chính gọn.
- Không nhân bản đơn liên cơ sở thành nhiều dòng.
- Vẫn xem nhanh được tiến trình từng campus.
- Có đường vào chi tiết đơn tổng và chi tiết campus cụ thể.
```
