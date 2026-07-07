# PROMPT — Code Role-aware Homepage cho PEMS

Bạn là **Senior Frontend UI/UX Engineer + Full-stack Engineer** cho dự án **PEMS - Partnership Engagement Management System** của FPT University.

Nhiệm vụ: **thiết kế và code lại trang Homepage theo role**, nhưng cần hiểu rõ: **Homepage không phải Dashboard**. Các role đã có dashboard riêng, vì vậy Homepage chỉ là **cổng vào hệ thống** gồm giới thiệu PEMS, shortcut theo role, hướng dẫn sử dụng, tin tức/FAQ/Gallery, không hiển thị lại danh sách nhiệm vụ hoặc số liệu dashboard.

---

## 1. Nguyên tắc bắt buộc

Trước khi sửa, hãy **search và đọc source hiện tại**. Không code theo suy đoán.

Giữ nguyên:

- Header hiện tại.
- Footer hiện tại.
- Dashboard hiện có của từng role.
- Logic nghiệp vụ, authorization, role/subRole, API business flow.
- Các route/action approve/reject/cancel/assign hiện có.

Chỉ sửa/tạo:

- Body Homepage.
- Component UI phục vụ Homepage.
- API service/frontend type cần thiết để lấy data thật.
- Backend public/internal homepage query nếu source hiện tại chưa có API phù hợp.

Không dùng mock data/hardcode logo/tin tức/FAQ/gallery. Nếu API chưa đủ, tạo hoặc mở rộng API phù hợp, nhưng phải đọc source/backend hiện tại trước.

---

## 2. Phân biệt Homepage và Dashboard

Tách rõ:

```text
Homepage = cổng vào hệ thống, giới thiệu, shortcut, hướng dẫn, nội dung chung.
Dashboard = nơi xử lý công việc thật, task list, số liệu, đơn chờ xử lý.
```

Không được biến Homepage thành dashboard thứ hai.

Sau login:

```text
VISITOR             → public homepage hoặc visitor homepage hiện có
Internal roles      → /home hoặc route homepage nội bộ hiện có
Dashboard các role  → giữ ở /dashboard/...
```

---

## 3. Public / Visitor Homepage

Đây là homepage dành cho khách ngoài/Visitor, phong cách **international landing page** chuyên nghiệp cho đối tác quốc tế.

Các section cần có:

1. **Hero quốc tế**
   - International Cooperation Office / FPT University.
   - Nội dung giới thiệu ngắn, chuyên nghiệp.
   - Ảnh campus thật nếu có.

2. **CTA chính**
   - Đăng ký tham quan.
   - Visit FPTU Online.
   - Xem đối tác.

3. **Quick Actions**
   - Đăng ký tham quan.
   - Khám phá Visit FPTU.
   - Liên hệ Phòng HTQT.

4. **Tin tức nổi bật**
   - Lấy từ data thật.
   - Chỉ hiển thị news public/published.
   - Không dùng placeholder “FPT University” nếu có ảnh thật.

5. **Visit FPTU / Gallery preview**
   - Lấy ảnh thật từ gallery.
   - Ưu tiên ảnh primary/public/published.

6. **Đối tác quốc tế**
   - Lấy từ partners thật.
   - Chỉ hiển thị đối tác approved/active nếu có trạng thái.
   - Không hardcode logo.

7. **Quy trình gửi yêu cầu tham quan**
   - Submit request.
   - FPTU reviews.
   - Host assigned.
   - Visit confirmed.

8. **FAQ preview**
   - Lấy FAQ public/published.

9. **Final CTA trước footer**
   - Đăng ký tham quan.
   - Liên hệ Phòng HTQT.

---

## 4. Internal Role-aware Homepage

Dành cho các role nội bộ:

```text
Student
HO
Admin
Staff Leader
Staff
Department Leader
Department Staff
```

Thiết kế như **PEMS Internal Portal Home**, không phải dashboard.

### 4.1. Section chung

1. **Welcome Hero**
   - Xin chào `[user.fullName]`.
   - Hiển thị role hiện tại.
   - Hiển thị campus/department nếu có.
   - Nút chính: `Vào Dashboard`.
   - Nút phụ: `Xem hướng dẫn` / `Tin tức mới`.

2. **Quick Access theo role**
   - Link nhanh tới các khu vực quan trọng.
   - Không hiển thị số task/số đơn nếu dashboard đã có.

3. **Tin tức / thông báo chung**
   - Public news published.
   - Notifications/user announcements nếu source hiện có.

4. **Hướng dẫn quy trình theo role**
   - Dạng timeline/checklist.
   - Mục tiêu là hướng dẫn thao tác, không phải hiển thị task thật.

5. **Visit FPTU / Gallery preview**
   - Cho internal role xem nội dung public để kiểm tra/giới thiệu.
   - Chỉ hiện nút quản lý Gallery nếu role đó đã có quyền/menu tương ứng trong source.

6. **FAQ / Help Center**
   - Dùng FAQ thật.
   - Nếu chưa có internal FAQ thì dùng FAQ public hiện có.

7. **Final CTA**
   - “Sẵn sàng tiếp tục công việc?”
   - Button vào dashboard đúng role.

---

## 5. Quick Access theo từng role

### Student

Hiển thị shortcut:

```text
- Vào Student Portal
- Xem lịch hỗ trợ
- Xem lời mời tham gia
- Hướng dẫn khi tham gia hỗ trợ đoàn
```

### HO

Hiển thị shortcut:

```text
- Vào HO Dashboard
- Quản lý yêu cầu liên cơ sở / theo dõi visit theo scope hiện tại
- Quản lý News / FAQ
- Quản lý Campus
- Xem hướng dẫn quy trình HO
```

Không hiển thị lại danh sách đơn chờ duyệt nếu dashboard đã có.

### Admin

Hiển thị shortcut:

```text
- Vào Admin Dashboard
- Quản lý tài khoản
- Quản lý API tích hợp
- Audit / Security
- Hướng dẫn cấu hình hệ thống
```

### Staff Leader

Hiển thị shortcut:

```text
- Vào Campus Dashboard
- Visit Management
- Account / Department Management
- Hướng dẫn duyệt đơn và gán host
- Gallery/News nếu role đang có menu/quyền tương ứng
```

### Staff

Hiển thị shortcut:

```text
- Vào My Workspace
- Xem đơn phụ trách
- Xem lời mời tham gia
- Hướng dẫn chuẩn bị visit
- Biên bản / News / Media contribution
```

### Department Leader

Hiển thị shortcut:

```text
- Vào Department Dashboard
- Phân công nhân sự
- Nhiệm vụ phòng ban
- Ký bàn giao
- Quy trình phối hợp với IC
```

### Department Staff

Hiển thị shortcut:

```text
- Vào My Tasks
- Xem lịch hỗ trợ
- Hướng dẫn phản hồi nhiệm vụ
- Ký nhận / ký trả
- Cập nhật kết quả hỗ trợ
```

---

## 6. UI/UX style

Dùng phong cách **enterprise + international portal**:

- Sạch, hiện đại, chuyên nghiệp.
- Không quá nhiều card/khung lớn.
- Không lặp dashboard.
- Nhiều khoảng trắng.
- Ảnh thật từ DB/file service.
- Responsive tốt mobile/tablet/desktop.
- Animation nhẹ: fade-up, hover image zoom nhẹ, stagger card.
- Không dùng hiệu ứng lố, không xoay/lắc mạnh.

Màu chuẩn:

```text
Primary blue: #004c91
Orange: #F37021
Text chính: slate-800 / slate-900
Text phụ: slate-500 / slate-600
Border: slate-200
Background: slate-50 / white
```

Bắt buộc có:

- Loading state.
- Empty state.
- Error state.
- Responsive layout.
- Không vỡ UI khi data rỗng hoặc ảnh thiếu.

---

## 7. Role / Auth rules

Không dùng dynamic permissions.

Không dùng:

```text
permissions
role_permissions
permission_code
permission_level
```

Phân biệt role bằng:

```text
role_code
sub_role
primary_campus_id
department_id
current user context
```

Role hợp lệ:

```text
ADMIN
HO
STAFF + LEADER
STAFF + STAFF
DEPARTMENT + LEADER
DEPARTMENT + STAFF
STUDENT
VISITOR
```

Không dùng role legacy:

```text
DEPT
STAFF_L
STAFF_P
DEPT_L
DEPT_P
STAFF_LEADER as role_code
DEPARTMENT_LEADER as role_code
```

---

## 8. Data/API requirements

Frontend phải gọi API thật, không dùng mock.

Trước khi tạo mới, kiểm tra các phần hiện có:

```text
- Frontend homepage page/component hiện tại
- Layout/header/footer hiện tại
- Auth context/current user
- Route guard
- Frontend API service
- Frontend type/interface
- Backend PublicContentController
- Backend PublicPartnersController
- Backend PublicVisitFptuController
- Backend NewsController
- Backend FaqsController
- Backend GalleriesController
- Backend FilesController / file proxy service
```

Dữ liệu public homepage ưu tiên lấy từ:

```text
- News public/published
- Partners approved/active
- FAQ published
- Gallery area/location/item/media public
- Homepage statistics nếu đã có
```

Nếu chưa có endpoint gom data, tạo endpoint hợp lý:

```text
GET /api/public/homepage
```

Response gợi ý:

```ts
{
  featuredNews: NewsCardDto[];
  featuredPartners: PartnerLogoDto[];
  featuredGallery: GalleryMediaDto[];
  faqs: FaqDto[];
  statistics?: HomepageStatisticsDto;
}
```

Nếu tạo endpoint internal homepage, dùng endpoint authenticated:

```text
GET /api/homepage/internal
```

Response gợi ý:

```ts
{
  currentUser: CurrentUserSummaryDto;
  roleHome: RoleHomeDto;
  quickLinks: QuickLinkDto[];
  guideSteps: GuideStepDto[];
  news: NewsCardDto[];
  notifications?: NotificationSummaryDto[];
  galleryPreview?: GalleryMediaDto[];
  faqs: FaqDto[];
}
```

Không trả dữ liệu nhạy cảm ra public homepage.

---

## 9. Backend rules nếu cần tạo API

Nếu phải tạo backend API, tuân thủ Clean Architecture:

```text
Controller → MediatR Query/Command → Handler → DTO → DbContext/Repository
```

Controller chỉ nhận request, gọi MediatR và trả response. Không viết business logic trong controller.

Query public chỉ được lấy dữ liệu public/published/approved, không lộ dữ liệu nội bộ.

Query internal phải yêu cầu authenticated user và dùng current user context.

Không tự tạo field/table/enum nếu SQL không có.

---

## 10. Không được làm

- Không sửa dashboard hiện có thành homepage.
- Không xóa chức năng hiện có.
- Không đổi business workflow approve/reject/cancel/assign.
- Không hardcode dữ liệu đối tác/tin tức/gallery.
- Không tạo field/table/enum nếu SQL không có.
- Không thêm thư viện mới nếu không thật sự cần.
- Không làm build TypeScript/C# lỗi.
- Không lộ dữ liệu nội bộ ở public homepage.
- Không hiển thị task list/số liệu dashboard trên homepage nội bộ.

---

## 11. Test/build bắt buộc

Sau khi sửa:

```text
- Chạy frontend build/lint/typecheck theo script hiện có.
- Chạy backend build nếu có sửa backend.
- Manual test từng role.
```

Role phải test:

```text
Visitor
Student
HO
Admin
Staff Leader
Staff
Department Leader
Department Staff
```

Kiểm tra:

```text
- Header/footer giữ nguyên.
- Visitor thấy landing page public.
- Internal role thấy homepage nội bộ, không lặp dashboard.
- Button “Vào Dashboard” đi đúng dashboard theo role.
- Quick Access đúng role.
- Data lấy thật.
- Empty state không vỡ UI.
- Public homepage không lộ dữ liệu nội bộ.
- Không dùng dynamic permissions.
```

---

## 12. Báo cáo sau khi hoàn thành

Trả report theo format:

```text
1. File đã sửa/tạo
2. API đã dùng/tạo
3. Logic phân role homepage
4. Data source thật đã thay mock
5. Các case đã test theo role
6. Build/test result
7. Phần chưa làm được hoặc cần xác nhận
```
