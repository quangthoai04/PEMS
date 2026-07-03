# PROMPT FIX DEPT STAFF ASSIGNED TASKS UI FLOW PEMS

Bạn là Senior Frontend UI/UX Engineer + Full-stack Engineer cho PEMS.

Nhiệm vụ: sửa UI/flow cho role DEPARTMENT STAFF ở phần “Nhiệm vụ được giao”, dựa trên các file sau:

- `frontend/pems-react/src/pages/dashboard/department-staff/DeptStaffDashboard.tsx`
- `frontend/pems-react/src/pages/dashboard/department-staff/StaffLeaderTaskModal.tsx`
- `frontend/pems-react/src/pages/dashboard/department-staff/StaffTasksTab.tsx`

Chỉ sửa đúng phạm vi này. Không đổi schema, không đổi API nếu chưa cần. Không xóa chức năng đang chạy. Nếu cần bổ sung API/DTO để đủ dữ liệu thì làm tối thiểu, không phá route cũ. UI phải gọn, ít khung/ô, phù hợp mobile.

---

## 1. Sửa danh sách “Đơn/thư chưa xử lý” màu cam

Hiện tại phần màu cam hiển thị các nhiệm vụ/thư chưa xử lý bằng nhiều ô/card, chiếm diện tích.

Yêu cầu:

- Có bao nhiêu đơn/thư/nhiệm vụ chưa xử lý thì list hết ra.
- Bỏ chữ trạng thái “Đã giao” trong từng dòng.
- Bỏ nhiều ô/card/khung lớn.
- Chuyển sang list compact/table compact.
- Mỗi dòng chỉ cần hiển thị gọn:
  - Tên đoàn / tên nhiệm vụ / loại thư.
  - Loại: thư mời tham gia / logistics item / nhiệm vụ hỗ trợ.
  - Thời gian nếu có.
  - Action: Nhận, Từ chối, Đề xuất, Xem chi tiết.
- Giữ màu cam cảnh báo nhẹ cho toàn section hoặc label nhỏ, không phủ nền quá nhiều.
- Mobile hiển thị dạng list 1 cột, action không tràn ngang.

---

## 2. Sửa flow Nhận / Từ chối / Đề xuất

Hiện tại người dùng phải bấm xem chi tiết rồi lại bấm nhận/từ chối, gây thừa bước.

Yêu cầu:

- Bấm “Nhận” ở ngay dòng item thì xử lý thành công luôn nếu không cần nhập lý do.
- Bấm “Từ chối” thì mở modal nhập lý do từ chối, submit xong gọi API và thành công luôn.
- Không bắt user mở modal chi tiết trước rồi mới được nhận/từ chối.
- Bấm “Đề xuất” thì mở thẳng modal/form đề xuất, không cần qua chi tiết.
- Sau khi action thành công:
  - Cập nhật lại danh sách.
  - Item biến mất khỏi “chưa xử lý” hoặc đổi trạng thái đúng.
  - Toast success rõ ràng.
- Nếu API lỗi thì hiển thị lỗi gọn, không reset dữ liệu user đã nhập.

---

## 3. Sửa modal/chi tiết thư mới

Phần xem chi tiết thư/nhiệm vụ hiện đang nhiều khung và ô, chiếm diện tích.

Yêu cầu:

- Hạn chế card/box lớn.
- Không hiển thị mỗi field trong một ô riêng.
- Dùng layout text key-value compact:
  - Đoàn khách: ...
  - Loại nhiệm vụ: ...
  - Phòng ban: ...
  - Người giao: ...
  - Thời gian: ...
  - Ghi chú: ...
- Các nội dung dài dùng divider mỏng và section nhỏ.
- Giữ đủ thông tin, không lược bỏ dữ liệu.
- Các nút chức năng giữ nguyên nhưng đặt gọn ở footer/modal header.

---

## 4. Sửa phần “Đóng góp kết quả”

Nếu trong Dept Staff có phần đóng góp kết quả, hãy làm gọn lại:

- Bỏ khung/card lớn không cần thiết.
- Dùng list/table compact.
- Các field nhập liệu chỉ mở khi cần.
- Nội dung đã đóng góp hiển thị dạng timeline/list nhỏ.
- Mobile không tràn ngang.
- Không đổi logic submit/upload nếu đang chạy.

---

## 5. Đồng bộ thiết kế với Dept Leader

Thiết kế chi tiết nhiệm vụ, biên bản, ký nhận/ký trả nên đồng bộ với Dept Leader nếu bên Dept Leader đã gọn và đúng rule.

Yêu cầu:

- Reuse component/style nếu có thể.
- Không copy-paste quá nhiều code.
- Rule ký logistics giữ đúng:
  - BORROW: bên phòng ban cho mượn ký bàn giao trước, Host ký nhận sau.
  - RETURN: Host ký trả trước, phòng ban ký nhận lại sau.
- Biên bản logistics hiển thị compact giống bên Dept Leader:
  - Thông tin item.
  - Ghi chú bên giao/bên nhận.
  - Tình trạng tài sản.
  - Chữ ký từng bên.
  - Thời gian ký.
  - Action ký phù hợp role/status.
- Không lược bỏ thông tin/chức năng.

---

## 6. UI style chung

- Enterprise dashboard, gọn, ít card.
- Ít khung/ô, ưu tiên text + divider + list/table compact.
- Giảm padding, giảm chiều cao row.
- Không dùng chữ quá to.
- Không dùng shadow/card lồng card quá nhiều.
- Desktop tận dụng chiều ngang.
- Mobile dễ bấm, không tràn ngang.

---

## 7. Clean code

Tách component nếu file quá dài:

- `AssignedTaskList`
- `AssignedTaskRow`
- `DeclineReasonModal`
- `ProposalModal`
- `DepartmentTaskDetailModal`
- `ContributionResultSection`

Không nhét logic mới quá nhiều vào một component lớn.

Không đổi tên route/API/props quan trọng nếu không cần.

Không xóa code cũ nếu chưa chắc không dùng.

---

## 8. Kiểm tra sau khi sửa

Test các case:

- Dept Staff mở tab “Nhiệm vụ được giao”.
- Danh sách chưa xử lý list đầy đủ tất cả đơn/thư/nhiệm vụ.
- Không còn chữ “Đã giao” trong từng dòng.
- Bấm “Nhận” xử lý trực tiếp thành công.
- Bấm “Từ chối” mở modal nhập lý do, submit thành công.
- Bấm “Đề xuất” mở thẳng form đề xuất.
- Xem chi tiết thư/nhiệm vụ gọn, ít khung, đủ thông tin.
- Phần đóng góp kết quả gọn hơn.
- Biên bản/ký logistics đúng rule như Dept Leader.
- Chạy `npm build` / typecheck.

---

## 9. Báo cáo lại

Báo cáo lại theo format:

- Files changed.
- Component đã sửa/tạo.
- Flow Nhận/Từ chối/Đề xuất đã sửa thế nào.
- UI đã compact phần nào.
- Build/test result.
