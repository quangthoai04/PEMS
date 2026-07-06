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

# PROMPT — Tạo trang Visitor Detail riêng khi đơn đã được gán Host

## 1. Bối cảnh

Dự án PEMS hiện có module quản lý đơn tiếp khách. Visitor chỉ được xem các đơn do chính mình gửi.

Hiện tại khi Visitor xem một đơn:

- Nếu đơn chưa được duyệt hoặc chưa có Host/visitInstance thì chỉ cần xem lại form đăng ký đã gửi như hiện tại.
- Nếu đơn đã được `APPROVED` và đã có Host/visitInstance thì cần mở một trang chi tiết riêng cho Visitor để xem thông tin chuyến thăm.
- Không dùng chung màn Host Operation / VisitProcess nội bộ cho Visitor.
- Không hiển thị các phần nghiệp vụ nội bộ.
- Không hiển thị tài liệu/documents/files/download cho Visitor trong scope này.

Yêu cầu mới: tạo trang mới cho Visitor xem chi tiết chuyến thăm sau khi đơn đã được gán Host.

---

## 2. Mục tiêu

Tạo một trang frontend mới tên đề xuất:

```txt
VisitorVisitDetailPage
```

Route đề xuất:

```txt
/dashboard/visit/visitor-detail/:visitInstanceId
```

Hoặc nếu hệ thống đang dùng route public detail hiện tại:

```txt
/dashboard/visit/reception-detail/:visitInstanceId
```

thì refactor route này để render component mới riêng cho Visitor, không render toàn bộ VisitProcess nội bộ.

Trang này chỉ dành cho role:

```txt
VISITOR
```

---

## 3. Quy tắc điều hướng

### 3.1. Ở trang danh sách đơn của Visitor

Khi Visitor bấm nút xem chi tiết, xử lý theo 2 trường hợp.

---

### Trường hợp A — Chưa có Host / chưa có visitInstance

Điều kiện:

```txt
requestStatus != APPROVED
hoặc visitInstanceId == null
hoặc currentHostUserId == null
hoặc host == null
```

Hành vi:

```txt
Chỉ mở form đăng ký đã gửi như hiện tại.
Không điều hướng sang VisitorVisitDetailPage.
Không hiển thị lịch trình chi tiết.
Không hiển thị thông tin Host.
```

Tên nút:

```txt
Xem đơn đã gửi
```

---

### Trường hợp B — Đã APPROVED và đã có Host

Điều kiện:

```txt
requestStatus == APPROVED
và visitInstanceId != null
và có currentHostUserId hoặc host
```

Hành vi:

```txt
Điều hướng sang VisitorVisitDetailPage.
```

Tên nút:

```txt
Xem chi tiết chuyến thăm
```

---

## 4. Dữ liệu cần hiển thị trên VisitorVisitDetailPage

Trang này chỉ hiển thị các thông tin public-safe, liên quan trực tiếp đến Visitor.

---

## 4.1. Header trạng thái chuyến thăm

Hiển thị ở đầu trang.

Thông tin cần có:

```txt
- Tên đoàn
- Mã đơn
- Cơ sở
- Trạng thái thân thiện với Visitor
- Thời gian bắt đầu dự kiến / chính thức
- Thời gian kết thúc dự kiến / chính thức
- Người phụ trách nếu đã có
```

Mapping status nội bộ sang text Visitor:

```txt
ASSIGNED / BEFORE_VISIT  -> Đã xác nhận lịch tham quan
DURING_VISIT             -> Chuyến thăm đang diễn ra
AFTER_VISIT              -> Chuyến thăm đã kết thúc
CLOSED                   -> Chuyến thăm đã hoàn tất
CANCELLED                -> Chuyến thăm đã hủy
```

Không hiển thị text nội bộ như:

```txt
ASSIGNED
BEFORE_VISIT
AFTER_VISIT
Chờ đóng đoàn
```

---

## 4.2. Form đăng ký đã gửi

Hiển thị read-only.

Có thể tái sử dụng component hiện tại nếu có:

```txt
RegistrantInfoReadOnly
DelegationInfoReadOnly
```

Các nhóm thông tin:

### A. Thông tin người đăng ký

```txt
- Họ tên
- Email
- Số điện thoại
- Quốc tịch
- Tổ chức / đơn vị
- Chức danh nếu có
```

### B. Thông tin đoàn khách

```txt
- Tên đoàn
- Tổ chức / đối tác
- Số lượng khách
- Thành phần đoàn nếu có
- Mục đích chuyến thăm
- Nội dung mong muốn trao đổi
- Ghi chú thêm
```

### C. Thông tin chuyến thăm mong muốn

```txt
- Cơ sở đăng ký
- Ngày giờ mong muốn
- Ngôn ngữ sử dụng
- Có cần phiên dịch không
- Phương tiện di chuyển
- Media consent
- Yêu cầu đặc biệt nếu có
```

Tất cả đều read-only. Không có nút sửa.

---

## 4.3. Lịch trình chuyến thăm

Chỉ hiển thị nếu đã có dữ liệu agenda.

Nguồn dữ liệu dự kiến:

```txt
visit_agendas theo visitInstanceId
```

Hiển thị dạng timeline/card, không hiển thị dạng form edit.

Mỗi agenda item hiển thị:

```txt
- Thời gian bắt đầu
- Thời gian kết thúc
- Tiêu đề hoạt động
- Mô tả nếu có
- Địa điểm nếu có
```

Không hiển thị:

```txt
- responsible_user_id
- người phụ trách từng agenda item
- dropdown chọn người phụ trách
- nút thêm/sửa/xóa agenda
- nút áp dụng mẫu agenda
- nút lưu lịch trình
```

Nếu chưa có agenda:

```txt
Lịch trình chi tiết đang được nhà trường cập nhật.
```

---

## 4.4. Người phụ trách chuyến thăm

Chỉ hiển thị khi đã có Host.

Thông tin:

```txt
- Họ tên Host
- Phòng ban nếu có
- Email
- Số điện thoại nếu có
- Cơ sở phụ trách
```

Label hiển thị:

```txt
Người phụ trách chuyến thăm
```

Không hiển thị danh sách toàn bộ Staff / Student / Department tham gia.

---

## 4.5. Thông tin cơ sở

Hiển thị các thông tin hiện có trong bảng `campuses`.

```txt
- Tên cơ sở
- Mã cơ sở nếu có
- Thành phố
- Địa chỉ
- Email cơ sở
- Số điện thoại cơ sở
```

Không làm phần hướng dẫn check-in nâng cao vì database hiện tại chưa có các field:

```txt
map_url
checkin_instruction
parking_instruction
gate_name
reception_location
arrival_note
```

Không hard-code hướng dẫn check-in.

---

## 4.6. Feedback sau chuyến thăm

Chỉ hiển thị khi status là:

```txt
AFTER_VISIT
hoặc CLOSED
```

Nếu Visitor chưa gửi feedback:

```txt
Hiển thị nút: Gửi phản hồi
```

Nếu đã gửi feedback:

```txt
Hiển thị trạng thái: Bạn đã gửi phản hồi. Cảm ơn bạn.
```

Feedback có thể làm ở mức nút điều hướng/modal nếu backend đã có API. Nếu chưa có API thì chỉ dựng UI placeholder rõ ràng, không fake submit thành công.

---

## 4.7. Lý do hủy nếu chuyến đã hủy

Nếu request hoặc campus instance bị `CANCELLED` thì hiển thị banner:

```txt
Chuyến thăm đã hủy.
Lý do: ...
Thời gian hủy: ...
```

Nếu có thể xác định actor:

```txt
- Nếu Visitor hủy: Bạn đã hủy chuyến thăm này.
- Nếu nhà trường hủy: Chuyến thăm đã được nhà trường hủy.
```

Không hiển thị log nội bộ chi tiết.

---

# 5. Những phần KHÔNG được hiển thị

Tuyệt đối không hiển thị trên VisitorVisitDetailPage:

```txt
- Tài liệu / documents / file tải xuống
- Timeline lịch sử đầy đủ
- Hướng dẫn check-in/map/parking nâng cao
- Album riêng theo visit
- Message trao đổi riêng giữa nhà trường và Visitor
- Lịch sử chỉnh sửa đơn
- Nút chỉnh sửa đơn
- Logistics request
- Department task
- Participant invitation
- Reminder setting cho Host/Participants
- Preparation note / ghi chú chuẩn bị nội bộ
- Host conflict
- Approval log chi tiết
- Biên bản nội bộ / minutes
- Email action token
- Nút chuyển giai đoạn Trước / Trong / Sau / Đóng đoàn
- Tab Trước tiếp khách / Đang tiếp khách / Sau tiếp khách
- Agenda editor
- Nút áp dụng mẫu agenda
- Nút lưu lịch trình
```

Đặc biệt: phần tài liệu hiện tại không cho Visitor nhìn, nên không render bất kỳ section nào liên quan đến `documents`, `files`, hoặc `download`.

---

# 6. UI đề xuất

Bố cục trang:

```txt
VisitorVisitDetailPage
├── Breadcrumb
│   └── Đơn tham quan của tôi / Chi tiết chuyến thăm
│
├── Header card
│   ├── Tên đoàn
│   ├── Mã đơn
│   ├── Badge trạng thái
│   ├── Thời gian chuyến thăm
│   └── Cơ sở
│
├── Section 1: Form đăng ký đã gửi
│   ├── Thông tin người đăng ký
│   ├── Thông tin đoàn khách
│   └── Thông tin chuyến thăm mong muốn
│
├── Section 2: Lịch trình chuyến thăm
│   └── Timeline agenda read-only
│
├── Section 3: Người phụ trách chuyến thăm
│   └── Host contact card
│
├── Section 4: Thông tin cơ sở
│   └── Campus info card
│
├── Section 5: Feedback
│   └── Chỉ hiện khi AFTER_VISIT/CLOSED
│
└── Section 6: Lý do hủy
    └── Chỉ hiện khi CANCELLED
```

Style nên đồng bộ với hệ thống hiện tại:

```txt
- Card bo góc lớn
- Màu chính #004c91
- Màu nhấn #f37021
- Badge trạng thái rõ ràng
- Read-only, không có input chỉnh sửa
- Responsive tốt trên laptop và mobile
```

---

# 7. Backend/API yêu cầu

Nếu đã có API detail hiện tại thì có thể tái sử dụng, nhưng response phải được lọc public-safe cho Visitor.

API đề xuất:

```txt
GET /api/visitor/visit-instances/{visitInstanceId}/detail
```

Hoặc nếu muốn theo request:

```txt
GET /api/visitor/visit-requests/{visitRequestId}/detail
```

Response cần có:

```ts
type VisitorVisitDetailDto = {
  visitRequestId: number;
  visitInstanceId: number;
  requestCode: string;
  requestStatus: string;
  campusStatus: string;

  delegationName: string;
  visitScope: string;
  visitType: string | null;

  plannedStartAt: string | null;
  plannedEndAt: string | null;

  registrant: {
    fullName: string;
    email: string;
    phone: string | null;
    nationality: string | null;
    organization: string | null;
    jobTitle: string | null;
  };

  delegation: {
    name: string;
    organization: string | null;
    guestCount: number | null;
    purpose: string | null;
    workingContent: string | null;
    noteToFptu: string | null;
    workingLanguage: string | null;
    transportationNote: string | null;
    transportationNote: string | null;
    mediaConsentStatus: string | null;
    mediaConsentNote: string | null;
  };

  guestMembers: Array<{
    fullName: string;
    organization: string | null;
    jobTitle: string | null;
    nationality: string | null;
    displayOrder: number;
  }>;

  campus: {
    campusId: number;
    name: string;
    campusCode: string | null;
    city: string | null;
    address: string | null;
    phone: string | null;
    email: string | null;
  };

  host: {
    userId: number;
    fullName: string;
    email: string | null;
    phone: string | null;
    departmentName: string | null;
  } | null;

  agenda: Array<{
    agendaId: number;
    title: string;
    description: string | null;
    startTime: string;
    endTime: string | null;
    location: string | null;
    sequenceOrder: number;
  }>;

  cancellation: {
    isCancelled: boolean;
    reason: string | null;
    cancelledAt: string | null;
    actorType: string | null;
  };

  feedback: {
    canSubmit: boolean;
    alreadySubmitted: boolean;
  };
}
```

Backend phải đảm bảo:

```txt
- Chỉ role VISITOR được gọi API này.
- Visitor chỉ xem được visitRequest do chính mình tạo.
- Nếu visitInstance chưa có Host thì API có thể trả 404/403 hoặc frontend không gọi API này.
- Không trả documents/files.
- Không trả logistics.
- Không trả participants nội bộ.
- Không trả reminders.
- Không trả preparation_note.
- Không trả minutes.
- Không trả audit logs.
- Không trả email tokens.
```

---

# 8. Điều chỉnh frontend hiện tại

Trong `VisitRequestManagement`, sửa logic điều hướng của Visitor.

Hiện tại nếu Visitor có host và requestStatus APPROVED thì đang điều hướng sang reception-detail/process detail. Cần đổi thành:

```ts
if (isVisitor) {
  if (row.visitScope === 'MULTI_CAMPUS') {
    toggleExpanded(row.visitRequestId);
    return;
  }

  if (row.host && row.requestStatus === 'APPROVED' && row.visitInstanceId) {
    navTo(`/dashboard/visit/visitor-detail/${row.visitInstanceId}`);
    return;
  }

  openRequestForm(row);
  return;
}
```

Nếu route `/dashboard/visit/reception-detail/:id` vẫn được dùng thì đảm bảo route đó render `VisitorVisitDetailPage` cho role VISITOR, không render `VisitProcess`.

---

# 9. Multi-campus handling

Với multi-campus, chưa cần tạo màn detail tổng hợp phức tạp.

Yêu cầu hiện tại:

```txt
- Ở danh sách, Visitor bấm vào đơn multi-campus thì mở rộng các campus như hiện tại.
- Với campus nào đã APPROVED và đã có Host/visitInstance thì hiện nút “Xem chi tiết chuyến thăm”.
- Với campus nào chưa có Host thì chỉ hiện trạng thái “Đang sắp xếp người phụ trách”, không cho vào VisitorVisitDetailPage.
```

---

# 10. Empty state và lỗi

## Nếu chưa có agenda

```txt
Lịch trình chi tiết đang được nhà trường cập nhật.
```

## Nếu chưa có Host

Không vào trang này. Nếu vào trực tiếp URL thì hiển thị:

```txt
Chuyến thăm chưa được phân công người phụ trách.
Vui lòng quay lại trang Đơn tham quan của tôi để xem trạng thái đơn.
```

## Nếu không có quyền

```txt
Bạn không có quyền xem thông tin chuyến thăm này.
```

## Nếu API lỗi

```txt
Không thể tải thông tin chuyến thăm. Vui lòng thử lại sau.
```

---

# 11. Acceptance Criteria

Hoàn thành khi đạt các tiêu chí sau:

```txt
1. Visitor chưa có Host chỉ xem được form đăng ký như hiện tại.
2. Visitor có Host + APPROVED vào được trang VisitorVisitDetailPage.
3. Trang VisitorVisitDetailPage không có tab Trước/Trong/Sau.
4. Trang VisitorVisitDetailPage không có logistics, participant invitation, reminder, preparation note, minutes.
5. Trang VisitorVisitDetailPage không hiển thị documents/files/download.
6. Agenda chỉ hiển thị read-only, không có input, không có nút lưu.
7. Host chỉ hiển thị thông tin liên hệ cơ bản.
8. Campus chỉ hiển thị thông tin có sẵn: name, code, city, address, phone, email.
9. Feedback chỉ hiện khi AFTER_VISIT hoặc CLOSED.
10. CANCELLED hiển thị lý do hủy public-safe.
11. Backend không trả dữ liệu nội bộ cho Visitor.
12. Không hard-code dữ liệu mẫu.
13. Không fake API success.
14. UI responsive và đồng bộ style với các trang dashboard hiện tại.
```

---

# 12. Không làm trong scope này

Không implement các phần sau:

```txt
- Tài liệu tải xuống
- Timeline lịch sử đầy đủ
- Hướng dẫn check-in/map/parking chi tiết
- Album riêng theo visit
- Message trao đổi giữa trường và Visitor
- Lịch sử chỉnh sửa đơn
- Sửa đơn sau khi gửi
- Bổ sung DB field map_url/checkin_instruction/parking_instruction/gate_name/reception_location/arrival_note
- Tạo bảng visit_public_messages
- Tạo bảng visit_public_message_recipients
- Tạo bảng visit_request_change_requests
- Tạo bảng visit_request_edit_history
```
