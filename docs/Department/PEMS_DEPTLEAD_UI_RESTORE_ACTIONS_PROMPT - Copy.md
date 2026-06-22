# PROMPT SỬA UI DEPARTMENT LEADER PEMS — KHÔI PHỤC ĐẦY ĐỦ ACTION, CHỈ ĐỔI BỐ CỤC

## 0. Bối cảnh lỗi hiện tại

Tôi đang xây dựng giao diện cho role **Department Leader** trong hệ thống **PEMS — Partnership Engagement Management System** bằng **React + TypeScript + Tailwind CSS**.

Tôi đã chạy một prompt thiết kế lại UI trước đó. Giao diện mới nhìn gọn hơn, nhưng bị lỗi nghiêm trọng:

```text
Các hành động, nút, link, route, chức năng cũ đã bị mất hoặc bị xóa.
```

Yêu cầu lần này:

```text
KHÔNG được xóa chức năng.
KHÔNG được xóa action.
KHÔNG được xóa handler.
KHÔNG được xóa API call.
KHÔNG được xóa modal.
KHÔNG được xóa route đang có.
KHÔNG được đổi logic nghiệp vụ.
KHÔNG được đổi permission/RBAC.
CHỈ được sửa bố cục, cách gom nhóm màn hình, sidebar, tabs, spacing, responsive UI.
```

Nhiệm vụ của bạn là **khôi phục đầy đủ hành động cũ**, sau đó **sắp xếp lại UI Department Leader** theo bố cục mới bên dưới.

---

## 1. Vai trò của bạn

Bạn là:

```text
Senior React TypeScript Engineer
Senior Frontend UI/UX Engineer
RBAC-aware Frontend Reviewer
Production Bug Fixer
```

Bạn đang sửa UI cho role:

```text
role_code = DEPARTMENT
sub_role = LEADER
effectiveRole = Department Leader
```

Mục tiêu là tạo giao diện:

```text
Gọn hơn
Dễ dùng hơn
Không lặp chức năng
Nhưng vẫn giữ 100% chức năng/action cũ
```

---

## 2. Tài liệu/code bắt buộc phải đọc trước khi sửa

Trước khi code, hãy đọc toàn bộ các file liên quan trong project:

```text
- src routes / router config
- Dashboard Department Leader hiện tại
- Department Management page hiện tại
- Department Detail page hiện tại
- Reception / Visit Management page hiện tại
- Report page hiện tại
- Sidebar / Layout / Navigation config
- API services liên quan department, personnel, task, delegation, calendar, report
- hooks đang dùng cho search/filter/pagination/action/modal
- constants role/permission/menu nếu có
```

Nếu project có các file sau thì phải đối chiếu:

```text
- PERMISSION_MATRIX.md
- PERMISSION_RULES.md
- USE_CASE_LIST.md
- USE_CASE_NOTES.md
- PEMS_UI_DESIGN_SYSTEM_PROMPT.md
- PROJECT_STRUCTURE_FULL.md
```

---

## 3. Nguyên tắc sửa bắt buộc

### 3.1. Tuyệt đối không được làm

```text
Không xóa nút xem chi tiết.
Không xóa nút thêm nhân sự.
Không xóa nút xóa/gỡ nhân sự nếu đã có.
Không xóa nút phân công.
Không xóa nút đổi người phụ trách.
Không xóa nút xem delegation/detail/task/detail.
Không xóa filter/search/pagination cũ nếu chức năng vẫn cần.
Không xóa modal cũ.
Không xóa hàm onClick cũ.
Không xóa API function cũ.
Không xóa route cũ nếu đang được dùng.
Không thay mock data vào chỗ đang dùng API thật.
Không đổi enum/status value gửi backend.
Không đổi permission check.
Không đổi role guard.
Không đổi business logic.
```

### 3.2. Chỉ được sửa

```text
Sidebar item label/order.
Bố cục màn hình.
Tabs.
Cards.
Toolbar/filter layout.
Spacing.
Typography.
Table/card responsive.
Icon placement.
Empty/loading/error visual state.
Link điều hướng từ card/table row/button sang route đã có.
```

### 3.3. Nếu action đã bị mất

Trước khi thiết kế tiếp, phải **khôi phục action bị mất** bằng cách:

```text
1. Kiểm tra git diff hoặc lịch sử file.
2. So sánh UI mới với UI cũ.
3. Tìm các handler/action/modal/API call đã bị xóa.
4. Gắn lại vào đúng vị trí trong layout mới.
5. Đảm bảo tất cả button cũ vẫn gọi đúng function cũ.
```

Không được báo hoàn thành nếu action cũ chưa được khôi phục.

---

## 4. Sidebar mới cho Department Leader

Sidebar của Department Leader chỉ còn 4 mục chính:

```text
1. Tổng quan
2. Nhiệm vụ tiếp khách
3. Nhân sự phòng ban
4. Báo cáo
```

### 4.1. Mapping sidebar

```text
Tổng quan
→ Trang dashboard tổng quan của Department Leader.

Nhiệm vụ tiếp khách
→ Trang gom 3 tab lớn:
   Tab 1: Bảng lịch
   Tab 2: Phân công
   Tab 3: Theo dõi tiến độ đoàn khách

Nhân sự phòng ban
→ Trang danh sách nhân sự phòng ban.
→ Chỉ hiển thị phần nhân sự, không hiển thị phần “Nhiệm vụ điều phối & thư mời tham gia”.

Báo cáo
→ Giữ nguyên trang báo cáo hiện tại.
```

### 4.2. Không được xóa route cũ

Nếu route cũ đang là:

```text
/dashboard
/dashboard/departments
/dashboard/departments/:id
/dashboard/receptions
/dashboard/reports
```

Thì không được xóa ngay. Có thể redirect hoặc map lại menu, nhưng phải đảm bảo link cũ không crash.

---

## 5. Trang 1 — Tổng quan

Trang **Tổng quan** là dashboard action center cho Department Leader.

### 5.1. Bố cục tổng quan

Thiết kế layout:

```text
[Header]
Xin chào, Department Leader
Phòng ban: [Tên phòng ban]
Campus: [Tên campus]

[KPI Cards]
- Chờ phân công
- Đoàn sắp tới
- Đang xử lý
- Nhân sự phòng ban

[Việc cần xử lý hôm nay]
List các task/delegation cần xử lý nhanh.

[Lịch tiếp đón sắp tới]
List lịch/đoàn sắp tới.

[Thông báo / cập nhật mới]
Nếu hiện tại có dữ liệu notification thì giữ lại.
```

### 5.2. Tất cả card/list phải có link

Yêu cầu quan trọng: **không được chỉ hiển thị số liệu tĩnh**. Mọi card/list phải click được nếu có dữ liệu liên quan.

Mapping click:

```text
Click card “Chờ phân công”
→ Đi tới trang Nhiệm vụ tiếp khách
→ Mở sẵn tab “Phân công”
→ Apply filter trạng thái chờ phân công nếu filter/state đã có.

Click card “Đoàn sắp tới”
→ Đi tới trang Nhiệm vụ tiếp khách
→ Mở sẵn tab “bảng lịch”
.

Click card “đang xử lý”
→ Đi tới trang Nhiệm vụ tiếp khách
→ Mở sẵn tab “Theo dõi tiến độ đoàn khách”
→ Apply filter đang xử lý nếu có.

Click card “Nhân sự phòng ban”
→ Đi tới trang Nhân sự phòng ban.

Click một item trong “Việc cần xử lý hôm nay”
→ Đi tới chi tiết task/delegation tương ứng.
→ Dùng route detail cũ nếu đã có.

Click một item trong “Lịch tiếp đón sắp tới”
→ Đi tới chi tiết đoàn khách/visit/delegation tương ứng.
→ Dùng route detail cũ nếu đã có.
```

### 5.3. Cách truyền tab/filter

Ưu tiên dùng query param để đơn giản và không phá logic:

```text
/dashboard/department-tasks?tab=assignment&status=pending
/dashboard/department-tasks?tab=progress&status=in_progress
/dashboard/department-tasks?tab=progress&status=overdue
/dashboard/department-personnel
```

Nếu project đã có state routing khác thì dùng cách hiện có, không tự tạo hệ thống routing mới quá lớn.

### 5.4. Action trong dashboard

Dashboard có thể là preview, nhưng không được mất action:

```text
- Xem chi tiết task/delegation.
- Đi tới tab phân công.
- Đi tới tab tiến độ.
- Đi tới nhân sự.
- Đi tới lịch.
```

---

## 6. Trang 2 — Nhiệm vụ tiếp khách

Trang này thay cho menu “Quản lý tiếp khách” cũ của Department Leader.

Tên hiển thị:

```text
Nhiệm vụ tiếp khách
```

Trang này phải có **3 tab lớn, nổi rõ, dễ nhìn**:

```text
Tab 1: Bảng lịch
Tab 2: Phân công
Tab 3: Theo dõi tiến độ đoàn khách
```

### 6.1. Tab design bắt buộc

Tabs phải nổi rõ, không nhỏ mờ:

```text
- Dạng segmented control hoặc tab card.
- Active tab có nền Primary Blue #004c91 và chữ trắng.
- Inactive tab nền trắng, border slate-200, chữ slate-700.
- Có icon phù hợp nếu project đang dùng icon library.
- Có badge số lượng nếu đã có dữ liệu count.
```

Ví dụ layout:

```text
┌──────────────────────────────────────────────────────────┐
│ [📅 Bảng lịch] [👥 Phân công] [📊 Theo dõi tiến độ đoàn] │
└──────────────────────────────────────────────────────────┘
```

### 6.2. Tab 1 — Bảng lịch

Nội dung của tab này lấy từ **phần lịch đang có ở dashboard hiện tại**.

Yêu cầu:

```text
- Reuse component lịch hiện tại nếu có.
- Không xóa click event trên lịch.
- Click vào một lịch/đoàn/nhiệm vụ phải đi tới detail tương ứng.
- Giữ filter/search nếu lịch cũ đã có.
- Nếu chỉ có calendar widget ở dashboard thì chuyển nó vào tab này, dashboard chỉ giữ preview lịch sắp tới.
```

Tên tab:

```text
Bảng lịch
```

Mục đích:

```text
Department Leader xem lịch tiếp đón, lịch nhiệm vụ phòng ban, deadline liên quan.
```

### 6.3. Tab 2 — Phân công

Nội dung tab này lấy từ section cũ:

```text
NHIỆM VỤ ĐIỀU PHỐI & THƯ MỜI THAM GIA
```

Hiện section này đang nằm trong trang quản lý phòng ban / chi tiết phòng ban. Hãy di chuyển hoặc reuse nó vào tab **Phân công**.

Yêu cầu:

```text
- Giữ nguyên toàn bộ action cũ trong section này.
- Giữ nút/đường link “Đổi người phụ trách”.
- Giữ nút xem chi tiết.
- Giữ search nhiệm vụ nếu có.
- Giữ lọc trạng thái nếu có.
- Giữ pagination/page size nếu có.
- Giữ modal phân công/đổi người phụ trách nếu có.
- Không biến bảng này thành read-only.
```

Bảng gợi ý:

```text
Đoàn khách | Nhiệm vụ được giao | Người phụ trách | Trạng thái | Hành động
```

Action bắt buộc nếu đã có trong code cũ:

```text
- Xem chi tiết
- Phân công người phụ trách
- Đổi người phụ trách
- Xem delegation/task detail
```

### 6.4. Tab 3 — Theo dõi tiến độ đoàn khách

Tab này chính là phần **Quản lý tiếp khách** hiện tại.

Yêu cầu:

```text
- Reuse toàn bộ trang quản lý tiếp khách hiện tại vào tab này.
- Không xóa search/filter/sort/pagination.
- Không xóa các action đang có.
- Không xóa nút xem chi tiết đoàn.
- Không xóa các badge trạng thái.
- Không đổi API params.
```

Tên tab:

```text
Theo dõi tiến độ đoàn khách
```

Mục đích:

```text
Department Leader theo dõi các đoàn/task mà phòng ban mình tham gia hoặc được phân công.
```

---

## 7. Trang 3 — Nhân sự phòng ban

Trang này chỉ quản lý nhân sự phòng ban.

### 7.1. Nội dung phải giữ

Giữ lại phần:

```text
Danh sách nhân sự
```

Giữ tất cả action cũ:

```text
- Search nhân sự.
- Thêm nhân sự.
- Xem chi tiết nhân sự.
- Xóa/gỡ nhân sự nếu có.
- Pagination/page size.
- Badge chức vụ Trưởng phòng/Nhân viên.
- Filter nếu có.
```

### 7.2. Nội dung phải bỏ khỏi trang này

Bỏ khỏi trang Nhân sự phòng ban section:

```text
NHIỆM VỤ ĐIỀU PHỐI & THƯ MỜI THAM GIA
```

Nhưng **không xóa code/action của section đó**. Section này phải được chuyển sang:

```text
Nhiệm vụ tiếp khách → Tab 2: Phân công
```

### 7.3. Layout nhân sự

Gợi ý:

```text
[Header]
Nhân sự phòng ban
Phòng IT · Campus FPT University

[Toolbar]
Search nhân sự | Lọc chức vụ | + Thêm nhân sự

[Table]
STT | Họ và tên | Email | Số điện thoại | Chức vụ | Hành động
```

Nếu có detail card trưởng phòng thì có thể đặt phía trên bảng, nhưng không làm trang quá dài.

---

## 8. Trang 4 — Báo cáo

Trang báo cáo:

```text
Giữ nguyên hiện tại.
Không đổi logic.
Không xóa chart/filter/export/action.
Chỉ sửa nhẹ spacing nếu bị lệch layout sidebar mới.
```

---

## 9. Điều hướng/link bắt buộc

Sau khi sửa, phải đảm bảo các link hoạt động:

```text
Sidebar Tổng quan
→ dashboard tổng quan.

Sidebar Nhiệm vụ tiếp khách
→ mở trang task với default tab = Bảng lịch hoặc tab gần nhất nếu project có lưu state.

Sidebar Nhân sự phòng ban
→ mở danh sách nhân sự.

Sidebar Báo cáo
→ mở báo cáo như cũ.

Dashboard card Chờ phân công
→ Nhiệm vụ tiếp khách / tab Phân công.

Dashboard card Nhân sự phòng ban
→ Nhân sự phòng ban.

Dashboard item Việc cần xử lý hôm nay
→ Chi tiết task/delegation.

Dashboard item Lịch tiếp đón sắp tới
→ Chi tiết delegation/visit.

Tab Phân công action Xem
→ Detail task/delegation.

Tab Phân công action Đổi người phụ trách
→ Mở modal/flow đổi người phụ trách cũ.

Tab Theo dõi tiến độ action Xem
→ Detail delegation/visit cũ.
```

---

## 10. UI style yêu cầu

Dùng phong cách enterprise dashboard:

```text
Primary blue: #004c91
Primary orange: #F37021
Text chính: slate-800 hoặc slate-900
Text phụ: slate-500 hoặc slate-600
Border: slate-200 hoặc slate-300
Background page: slate-50
Card background: white
```

### 10.1. Container

```tsx
className="w-full max-w-[1400px] mx-auto p-4 sm:p-6 lg:p-8 flex flex-col space-y-6 pb-12 overflow-x-hidden"
```

### 10.2. Card

```tsx
className="rounded-2xl border border-slate-200 bg-white shadow-sm"
```

### 10.3. Primary button

```tsx
className="inline-flex h-11 items-center justify-center rounded-xl bg-[#004c91] px-4 text-sm font-bold text-white transition-colors hover:bg-[#003b70] whitespace-nowrap"
```

### 10.4. Orange CTA

Chỉ dùng cho CTA chính như:

```text
+ Thêm nhân sự
```

Không lạm dụng màu cam.

### 10.5. Table

```text
- Header bảng dùng #004c91.
- Row nền trắng/sự khác biệt nhẹ.
- Action column căn giữa.
- Icon-only button phải có title và aria-label.
- Không để text dài làm vỡ table.
- Không làm horizontal scroll toàn trang.
```

### 10.6. Tabs lớn

```text
- Active: bg-[#004c91] text-white.
- Inactive: bg-white text-slate-700 border border-slate-200.
- Hover inactive: bg-blue-50 text-[#004c91].
- Height khoảng 44px hoặc 48px.
- Font-semibold hoặc font-bold.
```

---

## 11. Responsive

Phải kiểm tra:

```text
Desktop 1366px:
- Không horizontal scroll toàn trang.
- Sidebar không đè content.
- Tabs hiển thị rõ.

Tablet 1024px:
- KPI cards xuống 2 cột.
- Tabs vẫn dễ bấm.
- Toolbar filter có thể xuống dòng.

Mobile:
- Sidebar theo layout hiện có.
- KPI cards 1 cột.
- Bảng chuyển thành card list nếu project đã có pattern.
- Không ép table desktop làm vỡ màn hình.
```

---

## 12. Checklist nghiệm thu bắt buộc

Sau khi sửa xong, kiểm tra các điểm này:

```text
[ ] Sidebar chỉ còn: Tổng quan, Nhiệm vụ tiếp khách, Nhân sự phòng ban, Báo cáo.
[ ] Trang Tổng quan hiển thị KPI/action center.
[ ] Card “Chờ phân công” click được sang tab Phân công.
[ ] Card “Nhân sự phòng ban” click được sang trang nhân sự.
[ ] Việc cần xử lý hôm nay click được sang detail.
[ ] Lịch tiếp đón sắp tới click được sang detail.
[ ] Trang Nhiệm vụ tiếp khách có 3 tab lớn rõ ràng.
[ ] Tab Bảng lịch reuse lịch cũ.
[ ] Tab Phân công reuse section “Nhiệm vụ điều phối & thư mời tham gia”.
[ ] Tab Theo dõi tiến độ reuse trang Quản lý tiếp khách cũ.
[ ] Trang Nhân sự phòng ban chỉ còn danh sách nhân sự.
[ ] Section “Nhiệm vụ điều phối & thư mời tham gia” không còn nằm trong trang nhân sự.
[ ] Tất cả action cũ vẫn còn.
[ ] Tất cả modal cũ vẫn mở được.
[ ] Search/filter/pagination cũ vẫn hoạt động.
[ ] Không đổi API params.
[ ] Không đổi permission/RBAC.
[ ] Không đổi business logic.
[ ] Build TypeScript không lỗi.
```

---

## 13. Output mong muốn từ AI coding assistant

Sau khi sửa, hãy trả lời theo format:

```text
Đã sửa các file:
- path/to/file1.tsx
- path/to/file2.tsx

Đã khôi phục/giữ lại các action:
- Xem chi tiết
- Thêm nhân sự
- Xóa/gỡ nhân sự
- Đổi người phụ trách
- Search/filter/pagination
- Modal phân công

Đã thay đổi bố cục:
- Sidebar mới 4 mục
- Dashboard action center
- Nhiệm vụ tiếp khách 3 tab
- Nhân sự phòng ban chỉ còn bảng nhân sự
- Báo cáo giữ nguyên

Không thay đổi:
- API params
- Business logic
- Permission/RBAC
- Backend

Cách kiểm tra:
1. Login bằng Department Leader.
2. Vào Tổng quan.
3. Click từng KPI card.
4. Vào Nhiệm vụ tiếp khách, kiểm tra 3 tab.
5. Vào Nhân sự phòng ban, kiểm tra action thêm/xem/xóa.
6. Vào Báo cáo, kiểm tra vẫn hoạt động.
7. Chạy npm run build.
```

---

## 14. Nhắc lại yêu cầu quan trọng nhất

```text
Đây là task sửa bố cục UI, không phải rewrite chức năng.
Mọi chức năng/action cũ phải được giữ lại hoặc khôi phục.
Không được làm giao diện đẹp nhưng biến màn hình thành read-only.
Không được xóa các mục chỉ vì muốn UI gọn.
Gọn nghĩa là gom đúng chỗ, không phải xóa chức năng.
```
