<!-- =====================================================================
PEMS DOC UPDATE v8.2-full-preserved-cancel-delegation-no-external-note
Generated: 2026-06-19
Mode: PRESERVE ORIGINAL CONTENT + APPEND ADDENDUM.
No original section below has been removed or compressed.
The addendum section at the end is the authoritative update for cancellation UC-136.
===================================================================== -->

# PEMS v3.0 — TÀI LIỆU TỔNG QUAN DỰ ÁN
> **Partnership Engagement Management System**
> Phiên bản: 3.0 | Trường Đại học FPT 

---

## PHẦN 1 — TỔNG QUAN DỰ ÁN

### 1.1 Thông tin chung

| Mục | Nội dung |
|---|---|
| **Tên dự án** | PEMS — Partnership Engagement Management System |
| **Phiên bản** | 3.0 (RTW) |
| **Tổ chức** | Trường Đại học FPT  |
| **Phạm vi địa lý** | 5 cơ sở: Hà Nội (HN), TP.HCM (HCM), Đà Nẵng (ĐN), Cần Thơ (CT), Quy Nhơn (QN) |
| **Loại khách** | Khách trong nước và quốc tế |
| **Đơn vị vận hành** | Phòng Hợp tác Quốc tế (HTQT / IC) tại từng cơ sở |

### 1.2 Mục tiêu hệ thống

PEMS v3.0 số hoá và chuẩn hoá toàn bộ quy trình tiếp đón đoàn khách — bao gồm cả khách trong nước và quốc tế — tại FPT University, bao gồm:

- **Tiếp nhận & phê duyệt** yêu cầu thăm từ khách (Visitor) hoặc từ nội bộ (Staff)
- **Điều phối đa cơ sở** — HO xử lý các đoàn liên cơ sở, từng cơ sở quản lý quy trình nội bộ độc lập
- **Quản lý toàn bộ vòng đời đoàn khách** qua 3 giai đoạn: Trước – Trong – Sau tiếp khách
- **Lưu trữ & chia sẻ thông tin** về đối tác, biên bản, tài liệu, hình ảnh sau mỗi chuyến thăm
- **Cung cấp báo cáo & thống kê** cho lãnh đạo theo thời gian thực

### 1.3 Các bên liên quan (Stakeholders)

| Nhóm | Vai trò trong dự án |
|---|---|
| **HO (Head Office)** | Phê duyệt đoàn liên cơ sở, giám sát toàn hệ thống |
| **Admin** | Quản trị kỹ thuật, cấu hình tài khoản và API |
| **Staff_Lead (STAFF_L)** | Trưởng phòng IC — phê duyệt đoàn trong phạm vi cơ sở |
| **Staff (STAFF_P)** | Nhân viên IC — nhận đón, làm HOST, điều phối 3 giai đoạn tiếp khách (Chuẩn bị – Tiếp đón – Kết thúc) |
| **Dept_Lead (DEPT_L)** | Trưởng phòng ban khác — nhận mời/phân công nội bộ phòng |
| **Dept_Personnel (DEPT_P)** | Nhân viên phòng ban — thực thi nhiệm vụ được phân công |
| **Student** | Sinh viên hỗ trợ (buddy, media) — tham gia theo lời mời |
| **Visitor** | Khách trong nước hoặc quốc tế — gửi yêu cầu thăm, theo dõi tiến độ |

---

## PHẦN 2 — NGƯỜI DÙNG VÀ PHÂN QUYỀN

### 2.1 Mô tả 8 Role

| Role | Mô tả | Phạm vi |
|---|---|---|
| **HO** | Văn phòng trung tâm. Toàn quyền giám sát và phê duyệt liên cơ sở | 5 cơ sở |
| **Admin** | Quản trị viên kỹ thuật. Cấu hình hệ thống, tài khoản, API | Toàn hệ thống |
| **STAFF_L** | Trưởng phòng IC. Phê duyệt đơn, theo dõi quy trình | 1 cơ sở |
| **STAFF_P** | Nhân viên IC. Nhận đón, làm HOST, điều phối toàn bộ 3 giai đoạn tiếp khách (Trước – Trong – Sau) | 1 cơ sở |
| **DEPT_L** | Trưởng phòng ban (ngoài IC). Nhận mời, phân công nhân viên | 1 phòng ban |
| **DEPT_P** | Nhân viên phòng ban. Thực thi nhiệm vụ được DEPT_L giao | 1 phòng ban |
| **Student** | Sinh viên hỗ trợ tiếp khách (buddy, phiên dịch, chụp ảnh) | Theo lời mời |
| **Visitor** | Khách trong nước hoặc quốc tế. Gửi yêu cầu thăm, feedback, xem thông tin đoàn | Đoàn của mình |

### 2.2 Nguyên tắc phân quyền

1. **RBAC (Role-Based Access Control):** Quyền gắn liền với role, không phải từng cá nhân
2. **Campus Isolation:** STAFF_L và STAFF_P chỉ thao tác trong cơ sở được gán; không xem dữ liệu cơ sở khác
3. **Department Scope:** DEPT_L chỉ phân công trong phòng ban của mình
4. **Delegation Scope:** Người dùng chỉ thấy đoàn mình tham gia, trừ STAFF_L (toàn cơ sở) và HO (toàn hệ thống)
5. **HOST Privilege:** Trong một đoàn, STAFF_P được chỉ định làm HOST có quyền cao nhất trong cả 3 giai đoạn tiếp khách, bao gồm quyền Đóng đoàn (kết thúc toàn bộ quy trình)
6. **Visitor (không đăng nhập):** Chỉ gửi form yêu cầu qua trang public, không truy cập hệ thống nội bộ

---

## PHẦN 3 — MÔ TẢ TÍNH NĂNG

Hệ thống gồm **12 Major Features (FE)** và **127 Use Cases (UC)**.

---

### FE-01 — Quản lý Đối tác & Danh bạ liên lạc

**Mô tả:** Hệ thống cho phép nhân viên IC tạo, duy trì và tìm kiếm cơ sở dữ liệu tập trung về các tổ chức đối tác trong và ngoài nước, cùng danh sách người liên hệ. Hỗ trợ scan card visit bằng OCR để nhập thông tin liên lạc trực tiếp trong buổi tiếp khách.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-07 | View Partners (Public) | Visitor, Public |
| UC-35 | Scan Business Card | STAFF_P, STAFF_L |
| UC-36 | Create Partner Profile | STAFF_P, STAFF_L |
| UC-48 | Process Partner Creation Request | STAFF_L, HO |
| UC-49 | Edit Partner Information | STAFF_P, STAFF_L |
| UC-50 | View Partner Lists | All internal roles |
| UC-51 | Search Partners | All internal roles |
| UC-52 | View Partner Details | All internal roles |

---

### FE-02 — Quản lý Tiếp đón Đoàn khách

**Mô tả:** Tính năng cốt lõi của hệ thống. Quản lý toàn bộ vòng đời một đoàn khách trong nước hoặc quốc tế — từ gửi yêu cầu, phê duyệt liên cơ sở, điều phối nội bộ, qua 3 giai đoạn tiếp khách (Giai đoạn 1: Chuẩn bị → Giai đoạn 2: Tiếp đón → Giai đoạn 3: Kết thúc), đến đóng đoàn và lưu trữ.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-17 | Submit Visit Request | Visitor, STAFF_P |
| UC-18 | Approve Cross-Campus Request | HO |
| UC-19 | View Guest Delegation Details | All |
| UC-20 | View Guest Delegation List | All |
| UC-21 | Search Delegations | All |
| UC-22 | Process Visit Request | STAFF_L |
| UC-23 | Create Guest Delegation | STAFF_P |
| UC-24 | Update Guest Delegation | STAFF_P (HOST) |
| UC-25 | Prepare Visit Logistics | STAFF_P (HOST) |
| UC-26 | Update Visit Logistics | STAFF_P (HOST) |
| UC-27 | Confirm Participation | DEPT_L, Student |
| UC-28 | Approve Resource Request | DEPT_L |
| UC-29 | Propose Resource Modification | DEPT_L, DEPT_P |
| UC-30 | Confirm The Change Proposal | STAFF_P (HOST) |
| UC-31 | Create Meeting Minutes | STAFF_P, DEPT_L, Student |
| UC-32 | Edit Meeting Minutes | STAFF_P, DEPT_L |
| UC-33 | View Meeting Minutes Details | All participants |
| UC-34 | Submit Delegation Feedback | All participants |
| UC-37 | Upload Attached Documents | STAFF_P, STAFF_L |
| UC-38 | Upload Visit Photos | STAFF_P, Student |
| UC-39 | Tag Faces on Photos | STAFF_P |
| UC-40 | Create News Article | STAFF_P, Student |
| UC-41 | Close Delegation | STAFF_P (HOST) |

**Trạng thái đoàn khách:**
```
Chờ duyệt ──► Từ chối
           └─► Đã duyệt (chưa có HOST)
                    └─► Giai đoạn 1: Chuẩn bị (Trước tiếp khách)
                              └─► Giai đoạn 2: Tiếp đón (Trong tiếp khách)
                                        └─► Giai đoạn 3: Kết thúc (Sau tiếp khách)
                                                  └─► Đã đóng đoàn  ✓ (không thể đảo ngược)
```

---

### FE-03 — Quản lý Gallery Tham quan Trực tuyến

**Mô tả:** Cho phép nhân viên IC công bố và quản lý nội dung tour tham quan ảo (hình ảnh, mô tả) cho từng cơ sở, giúp đối tác tiềm năng khám phá cơ sở vật chất FPT trước khi đến thực tế.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-08 | View Gallery (Public) | Public, Visitor |
| UC-55 | View Gallery Item List | All |
| UC-56 | Search Gallery Items | All |
| UC-57 | Add Gallery Item | STAFF_P, STAFF_L |
| UC-58 | Update Gallery Item | STAFF_P, STAFF_L |
| UC-59 | Delete Gallery Item | STAFF_L |

---

### FE-04 — Quản lý Lịch & Deadline

**Mô tả:** Cung cấp lịch sự kiện chung cho tất cả nhân sự được phân công, đồng bộ lịch đoàn khách và tự động theo dõi deadline của action item từ biên bản cuộc họp. Gửi cảnh báo nhắc nhở trước hạn cho người chịu trách nhiệm.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-69 | View My Events | STAFF_P, STAFF_L, DEPT_L, DEPT_P, Student |
| UC-70 | View Department Calendar | DEPT_L, DEPT_P, STAFF_L |
| UC-71 | Switch View Mode | All |
| UC-72 | Add Personal Event | STAFF_P, STAFF_L |
| UC-73 | Delete Personal Event | STAFF_P, STAFF_L |
| UC-74 | Update Personal Event | STAFF_P, STAFF_L |
| UC-75 | View Event Details | All |

**Phân quyền xem lịch:**
- STAFF_P → lịch cá nhân + đoàn mình phụ trách
- STAFF_L → toàn bộ lịch trong cơ sở
- DEPT_L / DEPT_P → lịch phòng ban + task được phân công
- Student → đoàn được mời tham gia
- HO → lịch toàn bộ 5 cơ sở
- Visitor → không có quyền truy cập lịch

---

### FE-05 — Quản lý Tài liệu & Lưu trữ

**Mô tả:** Thư viện tập trung lưu trữ và tra cứu tài liệu đính kèm theo hồ sơ đoàn khách, cấu hình mẫu chương trình (agenda template) tái sử dụng, và kho lưu trữ biên bản cuộc họp từ các đoàn đã đóng.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-37 | Upload Attached Documents | STAFF_P, STAFF_L |
| UC-42 | Configure Agenda Templates | STAFF_L, HO |
| UC-53 | View Document List | All internal |
| UC-54 | Search Documents | All internal |
| UC-60 | View Minutes List | All internal |
| UC-61 | Search/Filter Minutes | All internal |

---

### FE-06 — Quản lý Email & Cấu hình API

**Mô tả:** Cho phép tất cả người dùng gửi và quản lý email bằng template tự động được kích hoạt theo sự kiện hệ thống. Admin cấu hình và giám sát tích hợp API bên ngoài — bao gồm cài đặt kết nối, điều kiện trigger, giới hạn request và log kết nối.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-43 | Config Email Templates | Admin, STAFF_L |
| UC-44 | Edit Email Content | STAFF_P, STAFF_L |
| UC-45 | Send Email | STAFF_P, STAFF_L |
| UC-46 | View Email | All |
| UC-47 | Reply to Email | STAFF_P, STAFF_L |
| UC-119 | View API Configuration | Admin |
| UC-120 | Create API Configuration | Admin |
| UC-121 | Update API Configuration | Admin |
| UC-122 | Delete API Configuration | Admin |
| UC-123 | Test API Connection | Admin |
| UC-124 | Manage API Status | Admin |
| UC-125 | Configure Request Limit | Admin |
| UC-126 | View API Logs | Admin |
| UC-127 | Search API Logs | Admin |

---

### FE-07 — Quản lý Feedback & Đánh giá

**Mô tả:** Thu thập đánh giá sao và nhận xét từ tất cả người tham gia tiếp khách — nhân viên IC, tình nguyện viên sinh viên và khách. Áp dụng giới hạn theo role để tránh tự đánh giá. Kết quả tổng hợp hiển thị cho HOST sau khi đóng đoàn.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-34 | Submit Delegation Feedback | STAFF_P, DEPT_L, Student, Visitor |
| UC-76 | Search/Filter Feedback | STAFF_L, HO |
| UC-77 | View Feedback Summary | STAFF_L, HO |

---

### FE-08 — Quản lý Báo cáo & Thống kê

**Mô tả:** Cung cấp dashboard tự động cập nhật cho HO và trưởng cơ sở, hiển thị thống kê đoàn khách theo năm, quốc gia và cơ sở, với khả năng xuất báo cáo và lọc theo khoảng thời gian tùy chỉnh.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-66 | View Dashboard Statistics | HO, STAFF_L |
| UC-67 | Export Statistics Report | HO, STAFF_L |
| UC-68 | Filter Dashboard By Time | HO, STAFF_L |

---

### FE-09 — Quản lý Tin tức & FAQ

**Mô tả:** Cho phép nhân viên và sinh viên tình nguyện đăng bài viết tin tức song ngữ về hoạt động hợp tác và tiếp khách qua quy trình duyệt nội dung. Duy trì kho FAQ tìm kiếm được cho đội ngũ IC và khách công khai.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-05 | View FAQ (Public) | Public, Visitor |
| UC-06 | View News (Public) | Public, Visitor |
| UC-40 | Create News Article | STAFF_P, Student |
| UC-62 | Create FAQ | STAFF_L, Admin |
| UC-63 | Update FAQ | STAFF_L, Admin |
| UC-64 | Change FAQ Visibility | STAFF_L |
| UC-65 | Search FAQ | All |
| UC-85 | Approve News | STAFF_L |
| UC-86 | Publish News | STAFF_L |
| UC-87 | View News List | All internal |
| UC-88 | View News Details | All |
| UC-89 | Add Multilingual News | STAFF_P, Student |
| UC-90 | Manage News Visibility | STAFF_L |
| UC-91 | Edit News | STAFF_P, Student |

---

### FE-10 — Quản lý Tài khoản & Phân quyền

**Mô tả:** Xử lý cấp phát tài khoản theo quy trình phê duyệt dựa trên role, cho phép Admin tạo và cấu hình định nghĩa role cùng các bộ quyền liên quan, và quản lý trạng thái tài khoản trên tất cả 8 loại người dùng.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-92 | View Account List | Admin, HO, STAFF_L |
| UC-93 | Create Account | Admin, STAFF_L |
| UC-94 | Manage Account Status | Admin, STAFF_L |
| UC-95 | View Account Details | Admin, STAFF_L |
| UC-96 | Search and Filter Accounts | Admin, STAFF_L |
| UC-97 | Update Account Role | Admin, HO, STAFF_L |
| UC-114 | View Role List | Admin |
| UC-115 | Create New Role | Admin |
| UC-116 | Configure Role Permissions | Admin |
| UC-117 | Update Role Details | Admin |
| UC-118 | Disable/Delete Role | Admin |

---

### FE-11 — Quản lý Phòng ban & Cơ sở

**Mô tả:** Duy trì cơ cấu tổ chức đầy đủ của FPT University — quản lý hồ sơ phòng ban, phân công nhân sự, chỉ định vai trò trưởng phòng trên 5 cơ sở, và cấu hình thông tin cơ sở (campus) với khả năng hiển thị dữ liệu có thể cấu hình theo từng cơ sở.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-78 | Add New Campus | HO |
| UC-79 | View Campus List | HO, STAFF_L |
| UC-80 | Search and Filter Campus | HO |
| UC-81 | View Campus Details | HO, STAFF_L |
| UC-82 | Update Campus | HO |
| UC-83 | Manage Campus Status | HO |
| UC-84 | Assign Campus Lead | HO |
| UC-98 | Add New Department | STAFF_L |
| UC-99 | Update Department | STAFF_L |
| UC-100 | Search and Filter Departments | STAFF_L, HO |
| UC-101 | View Department List | STAFF_L, HO |
| UC-102 | View Department Details | STAFF_L, DEPT_L |
| UC-103 | Manage Department Status | STAFF_L |
| UC-104 | Add Department Personnel | STAFF_L, DEPT_L |
| UC-105 | View Personnel Details | STAFF_L, DEPT_L |
| UC-106 | Search Personnel | STAFF_L, DEPT_L |
| UC-107 | Review Assigned Tasks | DEPT_L, DEPT_P |
| UC-108 | Assign Tasks | DEPT_L |
| UC-109 | Sign The Service Delivery Report | DEPT_L, STAFF_P |
| UC-110 | Remove Personnel | STAFF_L |
| UC-111 | View Coordination Tasks | DEPT_L, DEPT_P |
| UC-112 | Search Coordination Tasks | DEPT_L, DEPT_P |
| UC-113 | Reassign Department Lead | DEPT_L (with STAFF_L confirm) |

---

### FE-12 — Quản lý Hồ sơ cá nhân & Tùy chọn

**Mô tả:** Cung cấp các chức năng self-service: đăng nhập qua email/mật khẩu hoặc Google SSO, khôi phục mật khẩu, quản lý hồ sơ cá nhân, chuyển đổi ngôn ngữ hiển thị (Tiếng Việt / English), và xem thông báo hệ thống thời gian thực.

**Use Cases chính:**

| UC | Tên | Actor |
|---|---|---|
| UC-01 | View Homepage | All |
| UC-02 | Search Information | All |
| UC-03 | View Contact Info | All |
| UC-04 | View Policy & Terms | All |
| UC-09 | View Notifications | All authenticated |
| UC-10 | Login via SSO | All |
| UC-11 | Login via Credentials | All |
| UC-12 | Logout | All authenticated |
| UC-13 | Forgot Password | All |
| UC-14 | View Profile | All authenticated |
| UC-15 | Update Profile | All authenticated |
| UC-16 | Change Password | Non-SSO accounts |

---

## PHẦN 4 — LUỒNG NGHIỆP VỤ CHÍNH

> **Lưu ý:** Hệ thống tiếp đón cả **khách trong nước và quốc tế**. Quy trình xử lý đơn và 3 giai đoạn tiếp khách áp dụng như nhau cho cả hai.

### 4.1 Input 1 — Visitor gửi yêu cầu thăm

#### Trường hợp A: Thăm 1 cơ sở

```
[Visitor gửi form yêu cầu]
        │
        ▼
[STAFF_L & STAFF_P cơ sở đó thấy yêu cầu — Chờ duyệt]
        │ (chỉ STAFF_L quyết định)
   ┌────┴────┐
[Từ chối]  [Duyệt]
    │           │
STAFF_L      Trạng thái: "Đã duyệt — chưa có HOST"
điền lý do       │
Visitor thấy  [STAFF_P bấm "Nhận đón"]
lý do            │
             [Tạo đoàn khách — STAFF_P điền thông tin]
                 │
             Trạng thái: "Trước tiếp khách"
             HOST mặc định = STAFF_P vừa nhận đón
             (HOST có thể chuyển cho STAFF_P khác cùng phòng IC)
                 │
             → Gửi lời mời: STAFF_P khác, DEPT_L, Student
             → Tiếp tục quy trình 3 Tab (xem 4.3)
```

#### Trường hợp B: Thăm liên cơ sở (≥ 2 cơ sở)

```
[Visitor gửi form yêu cầu, chọn nhiều cơ sở]
        │
        ▼
[HO tiếp nhận — Chờ duyệt]
        │ (HO quyết định)
   ┌────┴────┐
[Từ chối]  [Duyệt]
    │           │
HO điền     HO chuyển tiếp tới từng cơ sở liên quan
lý do           │ (mỗi cơ sở xử lý độc lập)
Gửi email   [STAFF_L từng cơ sở tiếp nhận]
cho Visitor      │
            ┌────┴────┐
         [Từ chối]  [Duyệt]
             │           │
         STAFF_L B   [STAFF_P tại cơ sở đó nhận đón]
         điền lý do       │
         HO thấy,    [Tạo đoàn khách tại cơ sở đó]
         báo Visitor       │
                     → Tiếp tục quy trình 3 Tab
```

---

### 4.2 Input 2 — Staff chủ động tạo đoàn

| Tình huống | Xử lý |
|---|---|
| **Staff tạo đoàn tại cơ sở mình** | Tạo ngay, không cần phê duyệt → Trực tiếp sang Trước tiếp khách |
| **Staff tạo đoàn tại cơ sở khác** | Gửi tới STAFF_L cơ sở đích → STAFF_L phê duyệt → Staff tại cơ sở đích nhận đón |
| **Staff tạo đoàn liên cơ sở** | Gửi tới HO → HO phê duyệt → phân về từng cơ sở → STAFF_L từng nơi duyệt |

---

### 4.3 Quy trình 3 Giai đoạn (dùng chung cho mọi loại đoàn)

> Sau khi đoàn được tạo thành công, HOST quản lý qua 3 giai đoạn kế tiếp nhau. Mỗi giai đoạn tương ứng với một màn hình trong hệ thống; HOST phải xác nhận hoàn tất trước khi chuyển sang giai đoạn tiếp theo.

#### Giai đoạn 1 — CHUẨN BỊ (Trước tiếp khách)

HOST thực hiện: Setup thông tin đoàn, Detail Setup logistics, gửi yêu cầu mượn đồ tới DEPT_L, theo dõi xác nhận từ người được mời → **Xác nhận chuyển sang Giai đoạn 2**

**Luồng yêu cầu mượn đồ:**
```
B1: HOST gửi yêu cầu → DEPT_L xem xét
    ├─ Xác nhận → Trạng thái "Đang làm" → sang B2
    ├─ Từ chối + lý do
    └─ Đề xuất thay thế → HOST đồng ý → "Đang làm" → sang B2
                       → HOST không đồng ý → "Từ chối"

B2: Ký kết biên bản bàn giao (4 lần: Bàn giao ×2 + Nghiệm thu ×2)
    → Đủ 4 lần ký → Trạng thái "Hoàn thành"
```

#### Giai đoạn 2 — TIẾP ĐÓN (Trong tiếp khách)

| Chức năng | HOST | STAFF_P | DEPT_L / DEPT_P | Student | Visitor |
|---|---|---|---|---|---|
| Feedback | ✓ | ✓ | ✓ | ✓ | ✓ |
| Tạo đối tác / Scan card | ✓ | ✓ | ✗ | ✗ | ✗ |
| Tạo biên bản cuộc họp | ✓ | ✓ | ✓ | ✓ | ✗ |
| Upload tài liệu | ✓ | ✓ | ✗ | ✗ | ✗ |
| Xác nhận chuyển Giai đoạn 3 | ✓ (HOST) | ✗ | ✗ | ✗ | ✗ |

#### Giai đoạn 3 — KẾT THÚC (Sau tiếp khách)

| Chức năng | HOST | STAFF_P | Student | DEPARTMENT | Visitor |
|---|---|---|---|---|---|
| Upload album ảnh | ✓ | ✓ | ✓ | ✗ | ✗ |
| Gán tên lên khuôn mặt ảnh | ✓ | ✓ | ✗ | ✗ | ✗ |
| Đăng bài tin tức | ✓ | ✓ | ✓ | ✗ | ✗ |
| Xem album ảnh / bài viết | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Đóng đoàn** | **✓ (HOST only)** | ✗ | ✗ | ✗ | ✗ |

> ⚠️ Đóng đoàn là hành động **không thể đảo ngược** — toàn bộ 3 Tab bị khóa sau khi đóng.

---

## PHẦN 5 — MÔ TẢ KIẾN TRÚC HỆ THỐNG

### 5.1 Tổng quan kiến trúc

PEMS v3.0 là ứng dụng web đa tầng (multi-tier web application), được thiết kế theo mô hình **monolithic modular** hoặc **microservices**, triển khai trên môi trường server tập trung phục vụ 5 cơ sở đồng thời.

```
┌─────────────────────────────────────────────────────────┐
│                    CLIENT LAYER                          │
│  Web Browser (PC / Mobile)                              │
│  Public pages │ Authenticated portal │ Admin panel      │
└──────────────────────┬──────────────────────────────────┘
                       │ HTTPS
┌──────────────────────▼──────────────────────────────────┐
│                 PRESENTATION LAYER                       │
│  Single Page Application (SPA)                          │
│  - Role-based UI rendering (8 roles × feature sets)     │
│  - Bilingual UI: Vietnamese / English                    │
│  - Real-time notification panel                         │
└──────────────────────┬──────────────────────────────────┘
                       │ REST API / JSON
┌──────────────────────▼──────────────────────────────────┐
│               APPLICATION LAYER (Backend)                │
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │ Auth Module  │  │ Delegation   │  │ Partner Mgmt │   │
│  │ (SSO + Cred) │  │ Lifecycle    │  │ & Card Scan  │   │
│  └──────────────┘  └──────────────┘  └──────────────┘   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │ RBAC Engine  │  │ Notification │  │ File/Photo   │   │
│  │ (8 roles)    │  │ & Scheduler  │  │ Storage Mgmt │   │
│  └──────────────┘  └──────────────┘  └──────────────┘   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │ Calendar &   │  │ Reports &    │  │ API Config & │   │
│  │ Deadline     │  │ Statistics   │  │ Integration  │   │
│  └──────────────┘  └──────────────┘  └──────────────┘   │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│                    DATA LAYER                            │
│  ┌──────────────────┐  ┌────────────────────────────┐   │
│  │ Relational DB    │  │ File Storage               │   │
│  │ (Users, Roles,   │  │ (Documents, Photos,        │   │
│  │  Delegations,    │  │  Gallery, News media)      │   │
│  │  Partners, Logs) │  └────────────────────────────┘   │
│  └──────────────────┘                                    │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│               EXTERNAL INTEGRATION LAYER                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │
│  │ Google SSO   │  │ Email Service│  │ External APIs│   │
│  │ (OAuth 2.0)  │  │ (SMTP/SaaS)  │  │ (configured  │   │
│  │              │  │              │  │  by Admin)   │   │
│  └──────────────┘  └──────────────┘  └──────────────┘   │
└─────────────────────────────────────────────────────────┘
```

---

### 5.2 Xác thực & Phân quyền

| Cơ chế | Mô tả |
|---|---|
| **Google SSO (OAuth 2.0)** | Đăng nhập bằng tài khoản Google FPT. Email từ Google là định danh cố định, không thay đổi trong PEMS |
| **Credentials (email + password)** | Đăng nhập bằng tài khoản nội bộ. Hỗ trợ quên mật khẩu |
| **RBAC** | Quyền truy cập gắn liền với role. 8 roles được quản lý và cấu hình bởi Admin |
| **Campus Isolation** | Mọi truy vấn dữ liệu nội bộ đều được lọc theo `campus_id` của người dùng |
| **Session Management** | Phiên đăng nhập bị hủy khi Admin thay đổi role hoặc vô hiệu hóa tài khoản |

---

### 5.3 Quản lý dữ liệu & Lưu trữ

| Loại dữ liệu | Lưu trữ | Ghi chú |
|---|---|---|
| Users, Roles, Permissions | Relational DB | Audit log ghi lại thay đổi role |
| Delegations, Logistics | Relational DB | Immutable sau khi Đóng đoàn |
| Meeting Minutes, Action Items | Relational DB | Liên kết deadline với Notification Scheduler |
| Partners, Contact Persons | Relational DB | Hỗ trợ tìm kiếm full-text |
| Documents, Attachments | File Storage | Đính kèm theo delegation / partner record |
| Photos, Gallery | File Storage + DB metadata | OCR face tagging metadata lưu DB |
| API Logs | DB / Log Storage | Retention policy cấu hình bởi Admin |
| News, FAQ content | DB | Song ngữ: Tiếng Việt + English |

---

### 5.4 Notification & Scheduler

| Loại thông báo | Trigger | Kênh |
|---|---|---|
| Đơn chờ duyệt mới | Visitor gửi form / Staff tạo đoàn cơ sở khác | In-app |
| Đơn được duyệt / từ chối | STAFF_L hoặc HO xử lý | In-app + Email |
| Lời mời tham gia đoàn | HOST gửi | In-app |
| Nhắc nhở deadline action item | 24h trước (In-app + Email); 1h trước (In-app) | Scheduler |
| Thay đổi trạng thái đoàn | Chuyển tab hoặc đóng đoàn | In-app |
| Yêu cầu mượn đồ | HOST gửi tới DEPT_L | In-app |

---

### 5.5 Tích hợp API bên ngoài

- **Cấu hình bởi Admin** qua FE-06 (UC-119 đến UC-127)
- Mỗi API config bao gồm: endpoint, authentication key (encrypted), trigger conditions, request limit, status (Active / Inactive)
- API key được **mã hóa khi lưu**, chỉ hiển thị 4 ký tự cuối sau khi tạo
- Phải **test kết nối thành công** (UC-123) trước khi kích hoạt
- Log kết nối (UC-126, UC-127) để Admin theo dõi và debug

---

## PHỤ LỤC — SỐ LIỆU TỔNG QUAN

| Hạng mục | Số lượng |
|---|---|
| Major Features (FE) | 12 |
| Feature Types (FT) | 22 |
| Use Cases (UC) | 127 |
| Business Rules (BR) | 49 |
| Roles | 8 |
| Cơ sở (Campus) | 5 |
| Trạng thái đoàn khách | 6 |
| Tab tiếp khách | 3 |

---

# Addendum — Project Overview bổ sung UC-136 vào FE-02


## V8.2 Addendum — UC-136 Cancel Visit Request thuộc Delegation Reception Management

> Phần này là nội dung bổ sung, không xóa nội dung gốc. Nếu nội dung gốc có flow cũ như “đã duyệt nhưng chưa có host” hoặc “mỗi cơ sở duyệt lại sau HO”, hãy ưu tiên rule V8.2 trong phần addendum này.

### 1. Feature ownership

UC hủy đơn thăm thuộc **FE-02 — Quản lý Tiếp đón Đoàn khách / Delegation Reception Management** vì đây là thao tác trên vòng đời đoàn/visit request, không phải bước submit form.

```text
Feature: FE-02 Delegation Reception Management
UC: UC-136 Cancel Visit Request
Permission code: UC-136.CANCEL_VISIT_REQUEST
```

### 2. Không dùng `external_confirmation_note`

Không tạo cột `external_confirmation_note`. Khi Host hủy thay khách dựa trên xác nhận ngoài hệ thống, toàn bộ thông tin xác nhận được ghi vào `cancellation_reason`.

```text
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason = "Khách xác nhận hủy qua email/điện thoại/Zalo..., thời gian..., người xác nhận..., lý do..."
```

### 3. Cancellation metadata chuẩn

Áp dụng cho `visit_requests` và `visit_request_campuses`:

```sql
cancelled_by BIGINT UNSIGNED NULL,
cancelled_at DATETIME NULL,
cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL,
cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION') NULL,
cancellation_reason TEXT NULL
```

### 4. Meaning của `cancellation_source`

| Value | Meaning | Khi dùng |
|---|---|---|
| `SELF_SERVICE` | Người dùng tự thao tác trên hệ thống | Visitor tự hủy đơn của chính họ |
| `EXTERNAL_CONFIRMATION` | Hủy dựa trên xác nhận ngoài hệ thống | Host hủy thay khách sau khi khách xác nhận qua email/điện thoại/Zalo/gặp trực tiếp |
| `INTERNAL_DECISION` | Nội bộ hủy vì lý do vận hành | HO/Staff Leader hủy vì campus không thể tiếp, trùng lịch, lý do tổ chức |

### 5. Rule hủy theo role

| Actor | Scope | Nguồn hủy hợp lệ | Ghi chú |
|---|---|---|---|
| Visitor | Đơn của chính họ | `SELF_SERVICE` | Chỉ hủy khi chưa vào giai đoạn `DURING_VISIT`, `AFTER_VISIT`, `CLOSED` |
| Host | Campus instance mình đang phụ trách | `EXTERNAL_CONFIRMATION` | Bắt buộc nhập `cancellation_reason` rõ kênh/thời điểm/người xác nhận |
| Staff Leader | Đơn/campus thuộc campus mình | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Không xử lý campus khác |
| HO | `MULTI_CAMPUS` | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Có thể hủy request tổng liên cơ sở nếu nghiệp vụ cho phép |
| Admin | Không có quyền nghiệp vụ visit/delegation | Không áp dụng | ADMIN không được hủy delegation |

### 6. Rule trạng thái

- `visit_requests.status = CANCELLED` dùng khi hủy request/delegation tổng.
- `visit_request_campuses.status = CANCELLED` dùng khi hủy một campus instance.
- Không cho hủy campus instance nếu đã vào `DURING_VISIT`, `AFTER_VISIT`, hoặc `CLOSED`.
- Không dùng `CANCELLED` thay cho `REJECTED`. Nếu đơn đang `PENDING_APPROVAL` và người duyệt không chấp nhận, dùng reject flow.

### 7. Vị trí code Clean Architecture

```text
PEMS.Application/Delegations/Commands/CancelVisitRequest/
├── CancelVisitRequestCommand.cs
├── CancelVisitRequestCommandHandler.cs
├── CancelVisitRequestCommandValidator.cs
└── CancelVisitRequestResponse.cs
```

Controller chỉ nhận request và gọi `IMediator`. Logic kiểm tra scope, current host, request/campus status, và cancellation metadata nằm trong Handler/Domain Entity.


## FE-02 update

FE-02 — Quản lý Tiếp đón Đoàn khách cần bổ sung UC sau:

| UC | Tên | Actor | Ghi chú |
|---|---|---|---|
| UC-136 | Cancel Visit Request | Visitor, Host/Staff, Staff Leader, HO | Hủy request hoặc campus instance theo scope hợp lệ; thuộc vòng đời Delegation, không thuộc submit form |

## Phân biệt Cancel và Close

| Nghiệp vụ | Dùng khi nào | Status |
|---|---|---|
| Cancel Visit Request | Chuyến thăm không diễn ra do khách/nội bộ hủy trước khi vào giai đoạn đang diễn ra | `CANCELLED` |
| Close Delegation | Chuyến thăm đã hoàn tất và host đóng hồ sơ | `CLOSED` ở campus instance |
| Reject Request | Đơn chưa được duyệt và người duyệt không chấp nhận | `REJECTED` |
