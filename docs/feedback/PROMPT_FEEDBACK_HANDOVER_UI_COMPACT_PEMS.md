# PROMPT — PEMS Feedback + Logistics Handover + Compact UI Redesign

Bạn là **Senior Full-stack Engineer** cho dự án **PEMS — Partnership Engagement Management System**.

## 0. Bối cảnh bắt buộc

- Backend: ASP.NET Core .NET 8 Clean Architecture + MediatR + EF Core/Pomelo MySQL.
- Frontend: React Vite TypeScript + Tailwind.
- Database-first, không tự bịa field/schema.
- Không dùng dynamic permissions. Không tạo lại `permissions`, `role_permissions`, `permission_code`.
- Authorization dùng fixed policy theo `role_code`, `sub_role`, `primary_campus_id`, `department_id`, `visitor_user_id`, `current_host_user_id`, `coordinator_user_id`, participant relationship, logistics assignment, record status.
- SQL mới nhất đang dùng là bản đã sửa feedback rule: `pems_full_v10_new_final_feedback_rule_fixed.sql`.
- Không xóa code cũ nếu không cần. Chỉ refactor có kiểm soát.
- Code rõ ràng, chia thư mục dễ tìm, tránh conflict, tránh trộn logic lớn vào một file.

## 1. Nhiệm vụ tổng

Triển khai lại luồng:

1. Feedback mới.
2. Logistics Borrow/Return Signing.
3. UI compact cho màn Visit / Visit Process.
4. Sidebar trái có nút thu vào / mở ra dùng chung mọi trang, mọi role.

Yêu cầu giao diện:

- Phù hợp desktop và mobile.
- Dễ dùng, thân thiện với người dùng.
- Ít khung/card, tiết kiệm chiều cao màn hình.
- Không bắt người dùng lăn xuống quá nhiều.
- Hạn chế chữ to, khung to, hero lớn.
- Đủ thông tin nhưng phải gọn nhất có thể.

---

# 2. Rule Feedback mới cần tuân thủ

Feedback gồm:

- `rating` sao 1–5: bắt buộc.
- `comment` / text: không bắt buộc, có thể `NULL` hoặc empty.
- Nếu comment có nhập thì trim + sanitize.
- Không reject khi comment trống.

## 2.1 Visitor đánh giá chung chuyến thăm / đoàn tiếp đón

- `feedback_type = VISITOR_OVERALL`
- `submitter_role = VISITOR`
- `target_type = VISIT_REQUEST` hoặc `VISIT_INSTANCE`
- `target_user_id = NULL`
- `rating` bắt buộc.
- `comment` optional.

## 2.2 Host đánh giá các bên tham gia

- `feedback_type = HOST_PARTICIPANT`
- `submitter_role = HOST`
- `target_type = VISIT_PARTICIPANT` / `GUEST_MEMBER` / `USER`
- Dùng `target_participant_id`, `target_guest_member_id` hoặc `target_user_id` tương ứng.
- `rating` bắt buộc.
- `comment` optional.

## 2.3 Host đánh giá các bên cho mượn đồ / hậu cần

- `feedback_type = HOST_LOGISTICS`
- `submitter_role = HOST`
- `target_type = LOGISTICS_ITEM` / `LOGISTICS_HANDOVER` / `DEPARTMENT` / `USER`
- Dùng `target_logistics_item_id`, `target_handover_id`, `target_department_id` hoặc `target_user_id` tương ứng.
- `rating` bắt buộc.
- `comment` optional.

## 2.4 Không tạo target giả

Không lưu feedback ảo sai constraint DB.

Nếu UI có group tổng hợp như “Setup” hoặc “Detail setup”, backend không được tự tạo target giả nếu DB không map được.

Phải map vào target thật bên trong group:

- participant
- guest member
- logistics item
- handover
- department
- user

## 2.5 Chống feedback trùng

- Visitor chỉ đánh giá một lần cho cùng visit/request hoặc visit_instance.
- Host chỉ đánh giá một lần cho cùng target trong cùng visit_instance.
- Nếu đã feedback rồi thì UI hiển thị trạng thái “Đã đánh giá”, không hiện thông báo nhắc đánh giá nữa.

---

# 3. Backend cần làm

## 3.1 Đọc schema và update entity/config

Đọc kỹ bảng `feedbacks` và `feedback_rating_items` trong:

```text
pems_full_v10_new_final_feedback_rule_fixed.sql
```

Update Entity `Feedback`, `FeedbackRatingItem` nếu đang lệch schema.

Update EF Configuration cho các field:

- `feedback_type`
- `target_type`
- `submitter_role`
- `target_user_id` nullable
- `comment` nullable
- FK tới:
  - `visit_request_id`
  - `visit_instance_id`
  - `target_participant_id`
  - `target_guest_member_id`
  - `target_logistics_item_id`
  - `target_handover_id`
  - `target_department_id`

Không sửa bảng khác nếu không cần.

## 3.2 Tạo / hoàn thiện API Feedback

Tạo module rõ ràng trong Application, ví dụ:

```text
PEMS.Application/
  Feedbacks/
    Queries/
      GetVisitFeedbackTargets/
      GetPendingFeedbackNotifications/
    Commands/
      SubmitVisitFeedback/
    Common/
      FeedbackTargetDto.cs
      FeedbackGroupDto.cs
      FeedbackRules.cs
```

Controller:

```text
PEMS.Api/Controllers/FeedbacksController.cs
```

## 3.3 API đề xuất

### API 1 — Get feedback targets

```http
GET /api/feedbacks/visit-instances/{visitInstanceId}/targets
```

Trả danh sách target mà user hiện tại được đánh giá.

Response cần có:

```text
canSubmit
alreadySubmittedAllRequired
actorType: VISITOR / HOST
visitRequestId
visitInstanceId
groups[]
existingFeedbacks[]
submitHintMessage
```

### API 2 — Submit batch feedback

```http
POST /api/feedbacks/visit-instances/{visitInstanceId}
```

Request gồm danh sách item:

```text
feedbackType
targetType
targetUserId nullable
targetParticipantId nullable
targetGuestMemberId nullable
targetLogisticsItemId nullable
targetHandoverId nullable
targetDepartmentId nullable
rating 1–5 required
comment nullable
```

## 3.4 Validation

- `rating` bắt buộc từ 1 đến 5.
- `comment` optional.
- Phải validate target mapping theo `feedback_type`.
- Phải validate user hiện tại có quyền đánh giá `visit_instance` đó.
- Phải chống duplicate feedback.
- Nếu duplicate, trả 409 hoặc response rõ: “Bạn đã đánh giá mục này rồi.”

## 3.5 Authorization

- Visitor chỉ được đánh giá request/instance của chính mình.
- Host chỉ được đánh giá `visit_instance` mà mình là `current_host_user_id` hoặc có relationship hợp lệ theo code hiện tại.
- Không `AllowAnonymous`.
- Nếu `FeedbacksController` hiện đang thiếu `Authorize` thì phải thêm `Authorize` / `RoleAuthorize` đúng cách theo kiến trúc hiện tại.

## 3.6 Notification nhắc đánh giá

Yêu cầu:

- Ở chuông thông báo, nếu user có visit cần đánh giá thì hiện thông báo: **“Bạn hãy đánh giá đoàn”**.
- Nếu đã đánh giá rồi thì không hiện nữa.
- Cột hành động trong danh sách visit cũng hiện nút **“Đánh giá”** khi đủ điều kiện.

Điều kiện hiện nút/thông báo:

- Với Visitor: visit của họ ở trạng thái đang tiếp khách hoặc đã hoàn tất.
- Với Host: `visit_instance` họ phụ trách ở trạng thái đang tiếp khách hoặc đã hoàn tất.
- Mapping status theo code hiện tại.
- Tối thiểu xử lý `DURING_VISIT` và `CLOSED`.
- Nếu code đang dùng `AFTER_VISIT` để biểu diễn “Sau tiếp khách / đã hoàn tất” thì xử lý thêm `AFTER_VISIT`.
- Không hiện nếu đã feedback đủ target bắt buộc.

Có thể triển khai notification theo 1 trong 2 cách:

1. Query động pending feedback notification khi mở chuông.
2. Hoặc insert vào `notifications` với `notification_type = FEEDBACK_REQUIRED`.

Ưu tiên cách ít phá code hiện tại nhất. Nhưng bắt buộc không duplicate notification.

## 3.7 Logistics Borrow/Return Signing

Bảng đúng là `visit_logistics_item_handovers`:

- `handover_type = BORROW` hoặc `RETURN`
- `borrower_signed_by` / `borrower_signed_at`
- `provider_signed_by` / `provider_signed_at`
- `item_condition`
- `condition_note`
- `attachment_file_id` nullable

Cần triển khai API rõ ràng:

```http
GET /api/visit-instances/{visitInstanceId}/logistics/handovers?type=BORROW
GET /api/visit-instances/{visitInstanceId}/logistics/handovers?type=RETURN
POST /api/logistics/handovers/{logisticsItemId}/borrow/sign
POST /api/logistics/handovers/{logisticsItemId}/return/sign
```

Rule UI/backend:

- Trong tab “Đang tiếp khách”, phần đầu tiên là **“Ký mượn tài sản hậu cần”**.
- Bấm vào sẽ hiển thị danh sách đồ mượn theo từng phòng ban.
- Chọn một item thì mở modal nhập note/tình trạng và ký mượn.
- Trong tab “Sau tiếp khách”, phần đầu tiên là **“Ký trả tài sản hậu cần”**.
- Tương tự: hiển thị theo phòng ban, chọn item, mở modal note/tình trạng và ký trả.
- Không bypass rule hai bên ký.
- Hiển thị trạng thái ký rõ:
  - Chưa ký
  - Đã ký 1 bên
  - Đủ chữ ký
- Upsert handover theo unique `(logistics_item_id, handover_type)`, không tạo trùng `BORROW` / `RETURN`.

---

# 4. Frontend cần làm

## 4.1 Global sidebar thu vào / mở ra

Yêu cầu:

- Sidebar bên trái phải có nút thu vào / mở ra dùng chung cho mọi trang, mọi role.
- Khi thu vào, nội dung chính chiếm gần full width.
- Persist trạng thái bằng `localStorage`.
- Desktop: collapsed sidebar chỉ còn icon + logo nhỏ hoặc icon menu.
- Mobile: sidebar chuyển thành drawer/overlay, không chiếm ngang màn hình.
- Không làm vỡ layout hiện tại.

Ưu tiên sửa ở layout chung, ví dụ:

```text
frontend/pems-react/src/components/layout
```

hoặc `shared/layout` tùy project hiện tại.

## 4.2 Visit list action column

Ở danh sách “Quản lý tiếp khách”:

- Cột hành động hiển thị nút **Đánh giá** nếu user hiện tại đủ điều kiện.
- Nút chỉ hiện với đơn/visit đang ở trạng thái **Đang tiếp khách** hoặc **Đã hoàn tất**.
- Nếu đã đánh giá rồi: hiển thị badge nhỏ **“Đã đánh giá”** hoặc icon check, không hiện nút đánh giá.
- Nút gọn, có tooltip, không làm cột hành động quá rộng.
- Mobile: action đưa vào menu ba chấm hoặc button compact.

## 4.3 Redesign màn detail / visit process cho gọn

Hiện màn chi tiết đang quá dài vì nhiều card/khung lớn. Cần chỉnh theo hướng:

- Giảm hero/card lớn.
- Không dùng quá nhiều khung bo góc lớn.
- Không dùng chữ quá to.
- Dùng compact header: tên đoàn, status, thời gian, campus, host trên 1 vùng mỏng.
- Các thông tin phụ dùng accordion/table/list, không tách thành quá nhiều card.
- Hạn chế nested card.
- Dùng font `text-sm` / `text-base` vừa đủ.
- Section title ngắn gọn.
- Ưu tiên layout 2 cột trên desktop, 1 cột trên mobile.
- Có “Xem thêm” cho phần dài như thông tin đăng ký, danh sách thành viên, thông tin cơ sở.
- Giữ đủ dữ liệu nhưng giảm chiều cao scroll.

## 4.4 Feedback UI mới

Tạo UI gọn, thân thiện, mobile-first nhưng desktop phải tận dụng chiều ngang.

Không làm kiểu nhiều card lớn.

Thiết kế dạng compact list/table.

### Desktop

- Header mỏng: **“Đánh giá chuyến thăm”**.
- Summary: tên đoàn, trạng thái, thời gian.
- Bên dưới là danh sách group/row.
- Mỗi row gồm:
  - STT
  - Tên mục/đối tượng
  - Thông tin phụ ngắn gọn
  - Rating sao 1–5
  - Icon bút để mở comment
  - Trạng thái đã nhập/chưa nhập
- Comment không hiện textarea lớn mặc định. Chỉ hiện khi bấm icon bút.
- Nếu có comment thì icon đổi trạng thái hoặc hiện chấm nhỏ.
- Save button sticky ở dưới hoặc trên góc phải, không bắt user kéo cuối trang.

### Mobile

- Row dạng list compact.
- Sao đủ lớn để bấm.
- Comment mở bằng bottom sheet/modal nhỏ.
- Save button sticky bottom.
- Không làm bảng ngang gây tràn.

## 4.5 Visitor feedback

Visitor chỉ đánh giá chung chuyến thăm.

Form rất gọn:

- rating sao bắt buộc.
- comment optional qua icon bút hoặc ô text nhỏ có thể mở rộng.
- nút **Gửi đánh giá**.

Nếu đã gửi: hiển thị readonly **“Bạn đã đánh giá chuyến thăm này”**.

## 4.6 Host feedback

Host của đoàn khi bấm đánh giá sẽ thấy 4 nhóm compact:

### Nhóm 1 — Người tạo đoàn khách

- Hiển thị đầy đủ tên, email, số điện thoại, tổ chức nếu có.
- Map feedback vào target thật: `USER` hoặc `GUEST_MEMBER` tùy dữ liệu hiện có.

### Nhóm 2 — Thông tin đoàn khách

- Hiển thị tên đoàn, tổ chức, số lượng khách, danh sách guest member chính nếu có.
- Không tạo target ảo sai DB.
- Nếu cần đánh giá từng khách/đại diện thì map vào `GUEST_MEMBER`.
- Nếu không có target hợp lệ thì chỉ hiển thị thông tin, không tạo feedback item submit.

### Nhóm 3 — Setup

Hiển thị các bên tham gia hỗ trợ setup:

- Host
- Staff hỗ trợ IC
- Phòng ban hỗ trợ
- Sinh viên hỗ trợ

Mỗi người/bên đánh giá phải map vào `VISIT_PARTICIPANT` / `USER` / `DEPARTMENT` hợp lệ.

Hiển thị rõ:

- tên
- phòng ban
- role tham gia

### Nhóm 4 — Detail setup

Hiển thị đồ mượn / hậu cần / resource theo phòng ban.

Mỗi item logistics có thể đánh giá bằng target `LOGISTICS_ITEM` hoặc `LOGISTICS_HANDOVER`.

Hiển thị:

- title
- item_type
- số lượng
- phòng ban cho mượn
- người xử lý nếu có
- trạng thái ký mượn / ký trả

Không kéo dài bằng card. Dùng grouped compact rows.

## 4.7 Borrow/Return UI

Trong Visit Process:

### Tab “Đang tiếp khách”

- Đặt mục **“Ký mượn tài sản hậu cần”** lên đầu.
- Hiển thị dạng compact grouped list theo phòng ban.
- Mỗi dòng:
  - tên tài sản/hạng mục
  - số lượng
  - phòng ban
  - người xử lý
  - trạng thái ký
- Bấm dòng mở modal ký mượn:
  - item condition
  - note optional
  - xác nhận ký

### Tab “Sau tiếp khách”

- Đặt mục **“Ký trả tài sản hậu cần”** lên đầu.
- Tương tự borrow nhưng `handover_type = RETURN`.

Nếu không có item thì chỉ hiển thị một dòng empty state mỏng, không dùng khung lớn.

---

# 5. Yêu cầu UI Design chung

Phong cách:

- Enterprise dashboard, gọn, rõ ràng.
- Không lạm dụng card, border, shadow.
- Không dùng hero quá to.
- Không dùng font quá lớn.
- Không tạo nhiều khung làm rời rạc thông tin.
- Tận dụng chiều ngang khi sidebar collapsed.
- Trên mobile phải dễ bấm, không bị tràn ngang.

Màu:

- Primary blue `#004c91`.
- Orange `#F37021` cho CTA/nhấn nhẹ.
- Text chính `slate-800` / `slate-900`.
- Text phụ `slate-500` / `slate-600`.
- Border `slate-200`.
- Background `slate-50` / `white`.

Component nên có:

- `CompactStarRating`
- `CommentPopover` hoặc `CommentModal`
- `FeedbackTargetRow`
- `FeedbackGroup`
- `HandoverGroupedList`
- `HandoverSignModal`
- `CollapsibleSidebar`

Không thêm thư viện mới nếu không cần.

---

# 6. Clean Code / Folder Structure

## 6.1 Backend

- Controller chỉ gọi MediatR, không nhét business logic.
- Handler xử lý business validation + DB transaction theo pattern hiện có.
- Validator xử lý input validation.
- Rule mapping feedback target để trong class riêng `FeedbackRules` / `FeedbackTargetBuilder`.
- Không viết query lớn lặp lại nhiều nơi.
- Không dùng magic string rải rác; tạo constants cho `feedback_type`, `target_type`, `notification_type` nếu project có pattern constants.

## 6.2 Frontend

API service riêng:

```text
src/features/feedback/api/feedbackApi.ts
src/features/visit-process/api/handoverApi.ts
```

Types riêng:

```text
src/features/feedback/types.ts
src/features/visit-process/types/handover.ts
```

Components riêng:

```text
src/features/feedback/components/
src/features/visit-process/components/handovers/
src/shared/layout/CollapsibleSidebar.tsx
```

Hoặc sửa layout hiện tại nếu project đã có component tương ứng.

Yêu cầu:

- Không trộn feedback UI vào file visit detail quá lớn nếu có thể tách.
- Không xóa các phần đang hoạt động.
- Không đổi route lớn nếu không cần.

---

# 7. Kiểm tra sau khi code

Sau khi sửa phải chạy:

- Backend build.
- Frontend build.
- Kiểm tra TypeScript.
- Kiểm tra ít nhất các flow thủ công bên dưới.

## Flow 1 — Visitor feedback

- Visitor mở danh sách visit.
- Nếu visit đang diễn ra / đã hoàn tất và chưa feedback: cột hành động có nút **Đánh giá**.
- Chuông có thông báo **“Bạn hãy đánh giá đoàn”**.
- Gửi rating 1–5, không nhập comment vẫn thành công.
- Sau khi gửi, nút/notification biến mất hoặc đổi thành **Đã đánh giá**.

## Flow 2 — Host feedback

- Host mở danh sách visit.
- Visit đang tiếp khách / đã hoàn tất có nút **Đánh giá**.
- Form host có 4 nhóm:
  1. Người tạo đoàn khách
  2. Thông tin đoàn khách
  3. Setup
  4. Detail setup
- Rating bắt buộc.
- Comment optional qua icon bút.
- Submit không tạo target sai schema.

## Flow 3 — Ký mượn

- Host vào tab **Đang tiếp khách**.
- Mục **Ký mượn tài sản hậu cần** nằm đầu.
- Bấm item mở modal note + ký mượn.
- Lưu đúng `visit_logistics_item_handovers` với `handover_type = BORROW`.

## Flow 4 — Ký trả

- Host vào tab **Sau tiếp khách**.
- Mục **Ký trả tài sản hậu cần** nằm đầu.
- Bấm item mở modal note + ký trả.
- Lưu đúng `handover_type = RETURN`.

## Flow 5 — Sidebar collapse/expand

- Hoạt động mọi role.
- Persist sau reload.
- Mobile không che nội dung sai.

---

# 8. Format báo cáo sau khi hoàn thành

Báo cáo lại theo format:

```text
Files changed
Backend changes
Frontend changes
DB assumptions
Build/test result
Chỗ nào chưa làm được và lý do
```

---

# 9. Lưu ý cuối cùng

Trước khi sửa code, phải đọc SQL mới:

```text
pems_full_v10_new_final_feedback_rule_fixed.sql
```

Không được code theo schema cũ của feedback.

Không được tự thêm bảng/field nếu SQL không có.

Nếu phát hiện entity/DTO/frontend đang lệch schema mới, sửa đồng bộ theo chuỗi:

```text
SQL → Entity → EF Configuration → DbContext → DTO → Validator → Handler → Controller → Frontend type → API service → UI → Build/Test
```
