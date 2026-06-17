# HỆ THỐNG QUẢN LÝ TIẾP KHÁCH QUỐC TẾ — TÀI LIỆU LUỒNG CHÍNH

> Tài liệu này mô tả toàn bộ luồng nghiệp vụ, phân quyền và quy trình xử lý của hệ thống quản lý tiếp khách quốc tế tới thăm các cơ sở của Trường Đại học FPT Việt Nam (5 cơ sở trên toàn quốc).

---

## 1. TỔNG QUAN HỆ THỐNG

### 1.1 Mục đích
Hệ thống hỗ trợ các nghiệp vụ Hợp tác Quốc tế (HTQT), bao gồm:
- Quản lý lịch và công khai lịch chương trình
- Quản lý lịch của cán bộ HTQT và sinh viên liên quan
- Quản lý hoạt động tiếp khách (Visiting Request, Visit Online Tour)
- Quản lý đối tác (bao gồm cả đối tác chưa ký kết)

### 1.2 Phạm vi
- **5 Cơ sở (CS):** HN, HCM, ĐN, CT, QN (ví dụ)
- **HO:** Văn phòng trung tâm, quản lý toàn bộ 5 cơ sở

---

## 2. CÁC ROLE TRONG HỆ THỐNG

| Role | Mô tả |
|---|---|
| **HO** | Quản lý chung, toàn quyền với 5 cơ sở. Xử lý các đoàn khách liên cơ sở |
| **Admin** | Quản trị viên kỹ thuật, cấu hình hệ thống và API |
| **Staff** | Nhân sự phòng Hợp tác Quốc tế (IC) tại một cơ sở cụ thể |
| **Staff_Lead** | Trưởng phòng IC, đứng đầu một cơ sở |
| **Dept** | Nhân sự thuộc các phòng ban khác (bao gồm Trưởng phòng và Nhân viên) |
| **Student** | Sinh viên hỗ trợ (buddy, media, v.v.) |
| **Visitor (có tài khoản)** | Khách có thể đăng nhập, xem dữ liệu được phân quyền |
| **Visitor (không có tài khoản)** | Khách đăng ký thăm mà không cần đăng nhập |

---

## 3. CÁC TRẠNG THÁI CỦA ĐOÀN KHÁCH

```
Chờ duyệt → Từ chối
          → Đã duyệt (chưa có HOST)
              → Trước tiếp khách  (Tab 1 — sau khi Staff nhận đón & tạo đoàn)
                  → Trong tiếp khách (Tab 2 — sau khi HOST xác nhận tab 1)
                      → Sau tiếp khách  (Tab 3 — sau khi HOST xác nhận tab 2)
                          → Đã đóng đoàn (sau khi HOST đóng đoàn)
```

---

## 4. LUỒNG CHÍNH — INPUT 1: KHÁCH TỰ GỬI YÊU CẦU

### 4.1 Trường hợp A — Khách muốn thăm 1 cơ sở duy nhất

```
[Visitor gửi form yêu cầu]
        ↓
[Staff_Lead & Staff của cơ sở đó thấy yêu cầu — trạng thái: Chờ duyệt]
        ↓ (chỉ Staff_Lead được ra quyết định)
   ┌────┴────┐
[Từ chối]  [Duyệt]
    ↓           ↓
Staff_Lead   Trạng thái: "Đã duyệt — chưa có HOST"
điền lý do       ↓
Khách thấy   [Một Staff click "Nhận đón"]
lý do từ chối    ↓
             [Trang tạo đoàn khách — Staff điền thông tin & tạo]
                 ↓
             Trạng thái: "Trước tiếp khách"
             HOST mặc định = Staff vừa nhận đón
             (HOST có thể đổi cho Staff khác cùng phòng IC)
                 ↓
             [Gửi lời mời tham gia tới: Staff khác, Dept_Lead, Student]
                 ↓
             → Tiếp tục quy trình 3 tab (xem Mục 6)
```

### 4.2 Trường hợp B — Khách muốn thăm liên cơ sở (≥2 cơ sở)

```
[Visitor gửi form yêu cầu]
        ↓
[HO tiếp nhận — trạng thái: Chờ duyệt]
        ↓ (HO ra quyết định)
   ┌────┴────┐
[Từ chối]  [Duyệt]
    ↓           ↓
HO điền     HO chuyển tiếp yêu cầu tới từng Cơ sở liên quan
lý do            ↓ (mỗi cơ sở xử lý độc lập)
Gửi email   [Staff_Lead của từng cơ sở tiếp nhận]
cho khách        ↓
             ┌────┴────┐
          [Từ chối]  [Duyệt]
              ↓           ↓
          Staff_Lead   Trạng thái: "Đã duyệt — chưa có HOST"
          điền lý do       ↓
          HO thấy lý do [Một Staff tại cơ sở click "Nhận đón"]
          HO liên hệ       ↓
          thông báo khách  [Tạo đoàn khách]
                           ↓
                       → Tiếp tục quy trình 3 tab (xem Mục 6)
```

> **Lưu ý:** Với liên cơ sở, mỗi cơ sở tạo đoàn khách và quản lý quy trình độc lập nhau. HO theo dõi tổng thể.

---

## 5. LUỒNG CHÍNH — INPUT 2: STAFF CHỦ ĐỘNG TẠO ĐOÀN KHÁCH

### 5.1 Staff tạo đoàn thăm cơ sở của chính mình (Cơ sở A → A)

```
[Staff tại cơ sở A click "Tạo đoàn khách", chọn cơ sở A]
        ↓
[Staff điền thông tin & tạo đoàn]
        ↓
Trạng thái: "Trước tiếp khách"
HOST mặc định = Staff tạo đoàn
(có thể đổi HOST cho Staff khác cùng phòng IC)
        ↓
[Gửi lời mời tới: Staff khác, Dept_Lead, Student]
        ↓
→ Tiếp tục quy trình 3 tab (xem Mục 6)
```

### 5.2 Staff tạo đoàn thăm cơ sở khác (Cơ sở A → B)

```
[Staff tại cơ sở A click "Tạo đoàn khách", chọn cơ sở B]
        ↓
[Staff điền thông tin & gửi tới Cơ sở B]
        ↓
[Staff_Lead & Staff của Cơ sở B thấy đơn — trạng thái: Chờ duyệt]
        ↓ (chỉ Staff_Lead cơ sở B ra quyết định)
   ┌────┴────┐
[Từ chối]  [Duyệt]
    ↓           ↓
Staff_Lead B  Trạng thái: "Đã duyệt — chưa có HOST"
điền lý do        ↓
Staff A thấy  [Một Staff tại Cơ sở B click "Nhận đón"]
lý do từ chối     ↓
              [Tạo đoàn khách tại Cơ sở B]
                  ↓
              → Tiếp tục quy trình 3 tab (xem Mục 6)
```

### 5.3 Staff tạo đoàn thăm liên cơ sở (Cơ sở A → C & D)
thì sẽ để ho duyệt hoăc từ chối , nếu từ chối thì điền lí do, nếu duyệt thì auto các staff leader các cơ sở đó chịu trách nhiêm, staff leader có thể gán host cho người khác cũng được.
( chỉ ho mới nhìn đc đơn liên cơ sở, staff leader chỉ nhìn được đơn liên cơ sở mà ho đã duyệt và nhảy về campus tương ứng)
---

## 6. QUY TRÌNH 3 TAB TIẾP KHÁCH (DÙNG CHUNG CHO MỌI LUỒNG)

Sau khi đoàn khách được tạo thành công, HOST quản lý đoàn qua 3 tab:

### Tab 1 — TRƯỚC TIẾP KHÁCH

**Trạng thái đoàn:** `Trước tiếp khách`

**HOST thực hiện:**
- Xem chi tiết đoàn khách
- Thực hiện Setup & Detail Setup
- Gửi yêu cầu mượn đồ tới Trưởng phòng của các phòng ban khác
- Theo dõi xác nhận/từ chối từ những người được mời
- Theo dõi xác nhận cho mượn đồ từ phòng ban khác
- Khi mọi thứ hoàn tất → HOST **Xác nhận** để chuyển sang Tab 2

### Tab 2 — TRONG TIẾP KHÁCH

**Trạng thái đoàn:** `Trong tiếp khách`

**Các chức năng:**
- Feedback
- Tạo đối tác
- Tạo biên bản cuộc họp
- Scan card visit
- Thêm tài liệu cho đối tác

Khi hoàn tất → HOST **Xác nhận** để chuyển sang Tab 3

### Tab 3 — SAU TIẾP KHÁCH

**Trạng thái đoàn:** `Sau tiếp khách`

**Các chức năng:**
- Upload album ảnh (do sinh viên chụp trong buổi tiếp khách)
- Gán tên và thông tin card visit lên khuôn mặt trong ảnh
- Đăng bài tin tức về đoàn khách đã tới thăm

Khi hoàn tất → HOST **Đóng đoàn** → Trạng thái: `Đã đóng đoàn`

> **Lưu ý quan trọng:** Sau khi đóng đoàn, toàn bộ hoạt động trong 3 tab bị **disable** — không thể chỉnh sửa.

---

## 7. PHÂN QUYỀN TRONG QUY TRÌNH 3 TAB

### HOST (Staff được chỉ định)
- **Tab 1:** Toàn quyền — setup, detail setup, gửi yêu cầu mượn đồ, xác nhận chuyển tab
- **Tab 2:** Toàn quyền — feedback, tạo đối tác, tạo biên bản, scan card, thêm tài liệu
- **Tab 3:** Toàn quyền — upload ảnh, gán thông tin, đăng bài tin tức
- **Đặc quyền:** Có thể **Đóng đoàn**

### STAFF (Nhân sự phòng IC, không phải HOST)
- **Tab 1:** Xem toàn bộ; chỉ xác nhận/từ chối lời mời của chính mình
- **Tab 2:** Feedback, tạo đối tác, tạo biên bản cuộc họp, thêm tài liệu, scan card visit
- **Tab 3:** Upload album ảnh, đăng bài tin
- **Không thể:** Đóng đoàn

### DEPT (Nhân sự phòng ban khác — được mời tham gia)
- **Tab 1:** Xem toàn bộ; chỉ xác nhận/từ chối lời mời của chính mình
- **Tab 2:** Feedback, tạo biên bản cuộc họp
- **Tab 3:** Chỉ xem album ảnh và bài tin tức
- **Không thể:** Upload ảnh, đăng bài, đóng đoàn

### DEPT (Trưởng phòng phòng ban — nhận yêu cầu mượn đồ)
- **Tab 1:** Xem toàn bộ chi tiết setup và detail setup
- **Tab 2:** Chỉ feedback
- **Không thể:** Đóng đoàn

### STUDENT (Sinh viên hỗ trợ)
- **Tab 1:** Xem toàn bộ; chỉ xác nhận/từ chối lời mời của chính mình
- **Tab 2:** Feedback, tạo biên bản cuộc họp
- **Tab 3:** Upload album ảnh, đăng bài tin
- **Không thể:** Đóng đoàn

### STAFF_LEAD (Trưởng phòng IC)
- Xem và phê duyệt/từ chối đơn yêu cầu tới tham quan
- Theo dõi chi tiết quy trình sau khi có HOST nhận đón
- **Không thể:** Thao tác bất kỳ hành động nào trong 3 tab

### HO
- Tiếp nhận & phê duyệt/từ chối đơn liên cơ sở
- Điều phối đơn về các cơ sở liên quan
- Theo dõi trạng thái phê duyệt từ các cơ sở
- Xem biên bản cuộc họp của các cơ sở
- **Không thể:** Thao tác quy trình nội bộ của từng cơ sở

### VISITOR (Khách)
- Gửi yêu cầu tới tham quan
- Theo dõi trạng thái đơn của mình
- Xem lý do từ chối (nếu bị từ chối)
- Xem thông tin setup và detail setup (nếu được duyệt)
- Feedback
- Xem bài tin tức và album ảnh về đoàn của mình

---

## 8. LUỒNG XỬ LÝ YÊU CẦU MƯỢN ĐỒ & THƯ MỜI THAM GIA

### Nguyên tắc
- **Trưởng phòng** của phòng ban khác là người **mặc định nhận** lời mời / yêu cầu mượn đồ từ HOST
- Trưởng phòng có thể **tự xử lý** hoặc **phân công cho nhân viên**

### Luồng xử lý Thư mời tham gia

```
[HOST gửi thư mời tới Trưởng phòng]
        ↓
[Trưởng phòng xem & quyết định]
     ┌──┴──┐
  [Tự làm]  [Phân công nhân viên]
     ↓              ↓
[Xác nhận]   [Nhân viên nhận nhiệm vụ]
hoặc               ↓
[Từ chối     [Xác nhận] hoặc [Từ chối + lý do]
+ lý do]

Trạng thái thư mời:
  Xác nhận → "Hoàn thành"
  Từ chối  → "Từ chối" + lý do
```

### Luồng xử lý Yêu cầu mượn đồ (2 bước B1 & B2)

#### Bước B1 — Xác nhận mượn

```
[Nhân viên/Trưởng phòng xem yêu cầu mượn đồ]
        ↓
   ┌────┼────┐
[Xác nhận] [Từ chối] [Đề xuất thay thế]
    ↓           ↓            ↓
Trạng thái: Trạng thái: [HOST xem xét đề xuất]
"Đang làm" "Từ chối"    ┌───┴───┐
→ Tiếp B2   + lý do  [Đồng ý] [Không đồng ý]
                         ↓           ↓
                     "Đang làm" "Từ chối" + lý do
                     → Tiếp B2
```

#### Bước B2 — Biên bản bàn giao & nghiệm thu (chỉ khi B1 = "Đang làm")

```
[Nhân viên bên cho mượn đồ & HOST ký kết biên bản]
        ↓
[Ký kết 4 lần: Bàn giao (2 lần) + Nghiệm thu (2 lần)]
        ↓
[Đủ 4 lần ký] → Trạng thái: "Hoàn thành"

Trạng thái đồng bộ tới: Nhân viên, Trưởng phòng, HOST
```

---

## 9. TÓM TẮT QUAN HỆ GIỮA CÁC ROLE

```
                    ┌─────────┐
                    │   HO    │ ← Quản lý liên cơ sở
                    └────┬────┘
          ┌──────────────┼──────────────┐
     ┌────┴────┐    ┌────┴────┐    ┌────┴────┐
     │ Cơ sở A│    │ Cơ sở B │    │ Cơ sở C │  ...
     └────┬────┘    └─────────┘    └─────────┘
          │
    ┌─────┴──────┐
    │ Staff_Lead │ ← Phê duyệt đơn, theo dõi quy trình
    └─────┬──────┘
          │
    ┌─────┴──────┐
    │   Staff    │ ← Nhận đón, làm HOST, thực thi 3 tab
    └─────┬──────┘
          │  (mời tham gia)
    ┌─────┼──────────┬──────────┐
    │     │          │          │
┌───┴──┐ ┌┴──────┐ ┌┴───────┐  │
│Dept  │ │Student│ │Dept    │  │
│Lead  │ │       │ │(nhân   │  │
│(mời) │ │       │ │viên)   │  │
└──────┘ └───────┘ └────────┘  │
                          (mượn đồ)
```

---

## 10. CÁC ĐIỂM ĐẶC BIỆT CẦN LƯU Ý

1. **Visitor không có tài khoản** vẫn có thể gửi form đăng ký tới thăm mà không cần đăng nhập.
2. **HOST mặc định** là Staff đầu tiên bấm "Nhận đón", nhưng có thể chuyển HOST sang Staff khác trong cùng phòng IC.
3. **Liên cơ sở từ phía khách:** HO phê duyệt trước, sau đó phân về từng cơ sở. Mỗi cơ sở có Staff_Lead phê duyệt riêng.
4. **Liên cơ sở từ phía Staff:** Staff tạo → HO phê duyệt → phân về cơ sở đích → Staff_Lead tại cơ sở đích phê duyệt.
5. **Đóng đoàn là hành động không thể đảo ngược** — toàn bộ 3 tab bị khóa.
6. **Biên bản bàn giao mượn đồ** yêu cầu đúng 4 lần ký mới hoàn thành, trạng thái đồng bộ thời gian thực cho tất cả các bên liên quan.
7. **Staff_Lead và HO** chỉ có vai trò giám sát sau khi đoàn đã được tạo — không thể thao tác trong quy trình 3 tab.
