# PEMS — Root cause bảng HTML + Decision record thông tin liên hệ

> Báo cáo giai đoạn G0–G4 + G6 của `PEMS_MASTER_PLAN_EMAIL_TABLE_AND_CONFIGURABLE_CONTACT_INFORMATION.md`.
> Trạng thái: bảng HTML **đã sửa xong và có test**; phần thông tin liên hệ **đã audit + chốt thiết kế, chờ quyết định mô hình cấu hình (G6) trước khi migrate**.

---

## 1. Preflight (G0)

| Mục | Giá trị |
|---|---|
| Branch | `Canh-Iter1` @ `06deda15` |
| Upstream | `origin/Cảnh-Iter1` — ahead/behind vs `Dev` = 0/0 |
| WIP ngoài phạm vi | `CampusVisitCard.tsx` `8515056e`, `VisitRequestFormV2.tsx` `6100e9b0` — **không đụng tới** |
| Stash | 9 — **không đụng tới** |
| Baseline build | `dotnet build PEMS.slnx` → succeeded, 0 error |
| Baseline unit test | **2085 / 2085 pass, 0 fail** |
| SMTP | `appsettings.json` có `Smtp.Enabled = true` + credential Gmail thật; `appsettings.Testing.json` có `Smtp.Enabled = false` |

**Cảnh báo an toàn:** `backend/PEMS.Api/appsettings.json` đang bật SMTP thật với tài khoản
`managementsystemvolunteer@gmail.com` và app-password nằm trong repo. Test suite an toàn (Testing override
tắt SMTP), nhưng **chạy API ở Development sẽ kế thừa `Smtp.Enabled=true`**. Mọi runtime preview phải chạy với
`Smtp__Enabled=false`. Không có email thật nào được gửi trong đợt làm việc này.

---

## 2. Root cause bảng HTML (G1)

Chỉ **một** file backend sinh bảng email: `VisitSetupEmailHtml.cs` (6 chỗ có thẻ table trong toàn bộ
`backend/**/*.cs`). Không có lặp giữa các file, nên không cần tách helper dùng chung.

### 2.1. Bảng root cause

| Hiện tượng | Tầng gây lỗi | Bằng chứng | File/hàm | Cách sửa tối thiểu |
|---|---|---|---|---|
| Tiêu đề cột bị dồn thành 1 âm tiết/dòng ("Thời"/"gian"), cột hẹp bất thường | **Backend HTML** | Không bảng nào khai báo width; `table-layout` để mặc định `auto` → trình duyệt/mail client tính width theo nội dung. Cột "Nội dung" (title + description) chiếm hết phần dư, các cột còn lại co về **min-content** = âm tiết dài nhất | `VisitSetupEmailHtml.OpenTable/Head/Row` | `<colgroup>` + width trên từng ô + `table-layout:fixed` |
| Chuỗi dài không dấu cách (địa chỉ, URL) đẩy vỡ cột | **Backend HTML** | Không có `word-break`/`overflow-wrap` ở bất kỳ ô nào | `VisitSetupEmailHtml.Row` | `word-break:break-word` + `overflow-wrap:break-word` trên th/td |
| Padding không đều giữa Gmail và Outlook | **Backend HTML** | Padding đặt bằng thuộc tính `cellpadding="6"` trên `<table>`, không phải CSS trên ô | `VisitSetupEmailHtml.OpenTable` | `cellpadding="0"` + `padding:6px 8px` trên th/td |
| Cấu trúc không hợp lệ để test/parse | **Backend HTML** | Không có `<thead>`/`<tbody>`; header là `<tr><th>` trần | `VisitSetupEmailHtml.Head` | Bọc `<thead>`/`<tbody>` |
| Khoảng trắng thừa làm các ô giãn ra trong màn xem email đã gửi | **Frontend preview** | `whitespace-pre-wrap` áp lên body **HTML** → mọi newline giữa các thẻ thành ngắt dòng hiển thị | `SentEmailDetail.tsx:179` | Chỉ áp `pre-wrap` cho body PLAIN_TEXT |
| Bảng rộng bị ép/cắt trong panel hẹp | **Frontend preview** | Container chỉ có `overflow-y-auto`, không có cuộn ngang | `SentEmailsModal.tsx:272`, `SentEmailDetail.tsx` | Class cách ly `.pems-email-body` với `overflow-x:auto` |

### 2.2. Hai giả thuyết đã LOẠI (có kiểm chứng, không phỏng đoán)

- **CSS global của frontend tác động lên table** — SAI. `src/index.css` không có bất kỳ rule nào cho
  `table/thead/tbody/th/td` (grep toàn file, 0 match).
- **Tailwind `prose` bóp bảng trong preview** — SAI. Dự án dùng Tailwind v4 và **không cài**
  `@tailwindcss/typography`, nên `prose prose-sm` ở `TemplateManagement.tsx:550` là class không tồn tại,
  không sinh CSS. (Vẫn đã gỡ bỏ: nó là markup mang ý nghĩa "hãy restyle", và nếu sau này ai đó cài plugin
  thì bảng sẽ vỡ ngay — đúng lỗi này, đường khác.)
- **Sanitizer loại `style`/`width`/thẻ bảng** — SAI. `sanitizeHtml` chỉ chặn tag nguy hiểm + `on*` +
  URL scheme; không đụng `style`, `width`, `table`.

### 2.3. Bằng chứng "test đỏ trước khi sửa"

Test mới `VisitSetupEmailTableStructureTests` (14 test, parse bằng `System.Xml.Linq`, không thêm dependency):

- Chạy trên renderer **cũ** (`git checkout` về HEAD): **9 fail / 14**.
- Chạy trên renderer **đã sửa**: **14 pass / 14**.

Test assert trên cây đã parse, không phải `Contains()` — vì một bảng vỡ layout chứa **đúng cùng các chuỗi
con** như một bảng đúng, nên `Contains()` không thể phát hiện lỗi này dù viết bao nhiêu test.

---

## 3. Đã sửa gì (G2)

**Backend** — `VisitSetupEmailHtml.cs` (+93/−26):

| Bảng | Cột | Phân bổ |
|---|---|---|
| Danh sách khách / Thành phần FPT | Họ tên / Đơn vị / Vai trò | 32% / 43% / 25% |
| Lịch trình | Thời gian / Nội dung / Địa điểm / Phụ trách | 20% / 44% / 18% / 18% |
| Trạng thái chuẩn bị | Hạng mục / Số lượng / Thời gian cần / Trạng thái | 34% / 12% / 30% / 24% |
| Thông tin chung / Yêu cầu bổ sung (key-value) | Nhãn / Giá trị | 34% / 66% |

> **Lưu ý so với plan:** plan gợi ý bảng Lịch trình 5 cột (tách "Mô tả" riêng, 19/21/24/18/18). Code thật
> render **4 cột** — mô tả nằm trong ô "Nội dung" dưới dạng dòng phụ `<br/>` chữ nhỏ. Theo thứ tự ưu tiên
> ở Mục 3 của plan (code runtime > tài liệu), giữ 4 cột và gộp 21%+24% ≈ 44% cho "Nội dung".

Width được khai báo **ba lần** có chủ ý: `<colgroup>` (trình duyệt), thuộc tính `width` trên từng ô
(Outlook Word-engine không hiểu `colgroup` lẫn `table-layout`), và `table-layout:fixed`.

**Frontend:**
- `.pems-email-body` trong `index.css` — vùng cách ly: `white-space:normal` (huỷ `pre-wrap` kế thừa) +
  `overflow-x:auto`. Áp tại `SentEmailDetail`, `SentEmailsModal`, `TemplateManagement`.
- `ViewEmailDto.BodyFormat` (mới) — màn history cần biết body là HTML hay plain-text để quyết định có giữ
  ngắt dòng nguồn hay không. Trước đây không có field này nên màn hình đoán, và đoán giống nhau cho cả hai.

**Kết quả:** unit test **2099 / 2099 pass** (2085 baseline + 14 mới), frontend `tsc --noEmit` sạch.

---

## 4. Ma trận template (G3)

31 template đăng ký trong `SystemEmailTemplates`. Quét song ngữ trên `email-template-defaults.json`:
**15 template có câu kêu gọi liên hệ**, và **chỉ 1** (`VISIT_SETUP_PROGRESS_UPDATE`) thực sự in ra đầu mối
liên hệ được. 14 template còn lại bảo người nhận "vui lòng liên hệ …" mà không cho địa chỉ nào.

| # | Template code | Người nhận | Câu gọi liên hệ | Quyết định | Nguồn contact | Scope |
|---|---|---|---|---|---|---|
| 1 | ACCOUNT_EMAIL_CONFIRMATION | chủ tài khoản | — | `NO_CONTACT` | — | — |
| 2 | ACCOUNT_ACTIVATED | chủ tài khoản | VI+EN | `REQUIRED` | CAMPUS_DEFAULT → SUPPORT | campus |
| 3 | ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE | địa chỉ vừa bị gỡ (có thể là người lạ) | VI+EN | `REQUIRED` | **SUPPORT_CONTACT** | hệ thống |
| 4 | ACCOUNT_EMAIL_CHANGED_OLD_NOTICE | địa chỉ vừa bị gỡ | VI+EN | `REQUIRED` | **SUPPORT_CONTACT** | hệ thống |
| 5 | ACCOUNT_EMAIL_CHANGED_NEW_NOTICE | chủ tài khoản | VI+EN | `REQUIRED` | CAMPUS_DEFAULT → SUPPORT | campus |
| 6 | ACCOUNT_ROLE_CHANGED | chủ tài khoản | — | `OPTIONAL` | CAMPUS_DEFAULT | campus |
| 7 | ACCOUNT_STAFF_LEADER_ASSIGNED | Staff Leader mới | VI | `OPTIONAL` | CAMPUS_DEFAULT | campus |
| 8 | ACCOUNT_STAFF_LEADER_REPLACED | Staff Leader cũ | VI+EN | `REQUIRED` | SUPPORT_CONTACT | hệ thống |
| 9 | DEPT_PERSONNEL_ACCOUNT_DISABLED | nhân sự bị vô hiệu | VI+EN | `REQUIRED` | **DEPARTMENT_DEFAULT** | department |
| 10 | DEPT_PERSONNEL_ACCOUNT_ENABLED | nhân sự | — | `OPTIONAL` | DEPARTMENT_DEFAULT | department |
| 11 | DEPT_LEADERSHIP_GRANTED | trưởng phòng mới | — | `OPTIONAL` | DEPARTMENT_DEFAULT | department |
| 12 | DEPT_LEADERSHIP_HANDED_OVER | trưởng phòng cũ | — | `OPTIONAL` | DEPARTMENT_DEFAULT | department |
| 13 | AUTH_PASSWORD_RESET_OTP | người quên mật khẩu | — | `NO_CONTACT` | — | — |
| 14 | VISIT_REQUEST_OTP | khách đăng ký | — | `NO_CONTACT` | — | — |
| 15 | VISIT_CONTACT_CLAIM | đầu mối khách | VI+EN | `OPTIONAL` | CAMPUS_DEFAULT | campus |
| 16 | VISIT_CONTACT_TRANSFER | đầu mối khách | VI+EN | `OPTIONAL` | CAMPUS_DEFAULT | campus |
| 17 | VISIT_PARTICIPANT_INVITATION | người được mời | VI (`hostName` không kèm liên hệ) | `REQUIRED` | **HOST** | visit_instance/campus |
| 18 | VISIT_STUDENT_INVITATION | sinh viên | VI | `REQUIRED` | **HOST** | visit_instance/campus |
| 19 | VISIT_DEPARTMENT_LEADER_INVITATION | trưởng phòng | VI+EN | `REQUIRED` | **HOST** | visit_instance/campus |
| 20 | VISIT_DEPARTMENT_STAFF_ASSIGNMENT | nhân sự được phân công | VI | `REQUIRED` | **HOST** | visit_instance/campus |
| 21 | VISIT_REMINDER_HOST | chính Host | — | `NO_CONTACT` | — (contact sẽ là chính họ) | — |
| 22 | VISIT_REMINDER_PARTICIPANTS | người tham gia | VI | `REQUIRED` | **HOST** | visit_instance/campus |
| 23 | LOGISTICS_REQUEST_TO_DEPARTMENT | trưởng phòng nhận yêu cầu | — | `REQUIRED` | HOST_THEN_SENDER | visit_instance/campus |
| 24 | LOGISTICS_ASSIGNEE_ASSIGNMENT | nhân sự được giao | — | `OPTIONAL` | DEPARTMENT_DEFAULT | department |
| 25 | LOGISTICS_CHANGE_PROPOSAL_TO_HOST | Host | — | `REQUIRED` | DEPARTMENT_DEFAULT | department |
| 26 | LOGISTICS_EXPENSE_REPORT_REMINDER | người nợ báo cáo | — | `OPTIONAL` | SENDER | — |
| 27 | VISIT_SETUP_PROGRESS_UPDATE | khách + nội bộ (CC) | VI+EN (đã có `hostName`/`hostEmail`) | `REQUIRED` | **HOST** | visit_instance/campus |
| 28 | REPORT_CAMPUS_OPERATION | HO/Staff Leader | — | `OPTIONAL` | SENDER | — |
| 29 | REPORT_DEPARTMENT_COLLABORATION | trưởng phòng | — | `OPTIONAL` | SENDER | — |
| 30 | REPORT_DEPARTMENT_INVOICE | Staff Leader | — | `OPTIONAL` | SENDER | — |
| 31 | REPORT_PERSONNEL_PERFORMANCE | nhân sự | — | `OPTIONAL` | SENDER | — |

**Tổng:** `REQUIRED` 13 · `OPTIONAL` 14 · `NO_CONTACT` 4.

### 4.1. Ràng buộc riêng — hai template chống lộ danh tính

`ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE` và `ACCOUNT_EMAIL_CHANGED_OLD_NOTICE` gửi tới địa chỉ **vừa bị
gỡ khỏi tài khoản** — có thể thuộc về người hoàn toàn không liên quan (địa chỉ gõ nhầm). Registry hiện cố ý
khai báo **không biến nào** cho hai template này để không nêu tên chủ tài khoản với người lạ.

→ Contact block cho hai template này **bắt buộc chỉ được là SUPPORT_CONTACT cấp hệ thống**, không campus,
không department, không người cụ thể. Cho phép `CAMPUS_DEFAULT` ở đây sẽ tiết lộ nạn nhân thuộc cơ sở nào —
đúng thứ rò rỉ mà thiết kế hiện tại đang tránh.

### 4.2. Email mang bí mật

`ACCOUNT_EMAIL_CONFIRMATION`, `AUTH_PASSWORD_RESET_OTP`, `VISIT_REQUEST_OTP` để `NO_CONTACT`. Không có câu
kêu gọi liên hệ nào trong nội dung, và thêm một khối thông tin vào email mang OTP chỉ làm rộng thêm những
gì lộ ra nếu email bị chuyển tiếp/đánh cắp. Chính sách CC/BCC + sensitive giữ nguyên.

---

## 5. Decision record — Host / Sender / Reply contact (G4)

### 5.1. Ba khái niệm, ba nguồn khác nhau

| Khái niệm | Nguồn dữ liệu | Ghi chú |
|---|---|---|
| **Host** | `visit_request_campuses.current_host_user_id` | **Đã per-campus sẵn** — đây là thứ bảo đảm không lấy Host của cơ sở khác |
| **Sender** | user thực hiện lệnh (`ICurrentUserService`) | HO gửi thay ≠ HO là đầu mối |
| **Reply contact** | kết quả của resolver theo policy | Có thể trùng Host, trùng Sender, hoặc không trùng ai |

Không dùng chung một DTO `name/email` cho cả ba.

### 5.2. Dữ liệu contact đã có đủ trong schema — **không cần bảng dữ liệu mới**

| Nguồn | Cột đã có |
|---|---|
| HOST | `visit_request_campuses.current_host_user_id` → `users.full_name/email/phone/status` |
| CAMPUS_DEFAULT | `campuses.name`, `campuses.phone`, `campuses.email`, `campuses.ic_head_user_id` |
| DEPARTMENT_DEFAULT | `departments.name`, `departments.head_user_id` → `users.*` (department không có phone/email riêng) |
| SENDER | `users.*` của người thao tác |
| SUPPORT_CONTACT | **chưa có** — cần cấu hình cấp hệ thống (appsettings hoặc bảng cấu hình) |

Đây là phát hiện quan trọng cho G6: **thứ duy nhất cần lưu thêm là *chính sách*, không phải *dữ liệu liên hệ***.

### 5.3. Quy tắc resolver

1. Thứ tự **theo policy của từng template**, không áp cứng toàn hệ thống.
2. Không bao giờ lấy Host của campus khác — resolver nhận `visitInstanceId`, không nhận `visitRequestId`.
3. Sender chỉ được dùng làm contact khi policy ghi rõ (`SENDER` / `HOST_THEN_SENDER`).
4. User `INACTIVE`/`LOCKED`/`PENDING_EMAIL_CONFIRMATION` → **bỏ qua**, rơi xuống fallback kế tiếp.
5. Không hiện `N/A`. Trường không có dữ liệu thì không render dòng đó.
6. `REQUIRED` mà cạn fallback → **fail closed** với `EMAIL_CONTACT_REQUIRED_BUT_NOT_FOUND`.
7. `phone` là nullable ở mọi nguồn → thiếu phone **không** phải lý do fail; thiếu **email** mới là.

### 5.4. Snapshot

- **Preview**: resolve live (chưa có gì để đóng băng).
- **Draft**: khi tạo draft → lưu snapshot contact đã resolve vào chính nội dung draft đã render.
- **Send**: dùng đúng snapshot của draft. Không âm thầm đổi người liên hệ giữa lúc Host xem trước và lúc bấm gửi.
- **History**: `sent_emails.body_snapshot` vốn đã lưu body đã render → contact hiển thị trong history đúng
  bằng cái đã gửi, không cần cột mới.

**Riêng tư:** contact block chỉ chứa dữ liệu công việc (tên, chức danh, đơn vị, email/phone công việc) của
người **đang giữ vai trò nghiệp vụ liên quan tới chính người nhận**. Không thêm dữ liệu cá nhân ngoài phạm vi
đó; hai template ở Mục 4.1 bị giới hạn cứng ở support cấp hệ thống.

### 5.5. Reply-To

Hạ tầng đã có sẵn: `EmailMessage.ReplyTo` và `SystemEmailRequest.ReplyTo` (`EmailRecipient?`) đều đã được
plumb tới `EmailService`. Không cần thay đổi hạ tầng — chỉ cần policy quyết định có set hay không và lấy từ
nguồn nào, cộng validate qua `EmailRecipientValidator` sẵn có.

**Ràng buộc:** email mang token (`SingleRecipientNoCopies` + `HasSensitiveAction`) **không** tự thêm
Host/Sender vào CC/BCC — giữ nguyên `EmailRecipientPolicyEnforcer`.

---

## 6. G6 — Mô hình cấu hình: 3 phương án

Vì dữ liệu liên hệ đã có sẵn (Mục 5.2), câu hỏi thu hẹp lại thành: **lưu policy per-template ở đâu?**

| | A. Policy trong code | B. Cột JSON trên `email_templates` | C. Bảng chuẩn hoá riêng |
|---|---|---|---|
| Migration | không | 1 cột additive | 1 bảng mới + seed 31 dòng |
| Admin sửa được (§13 yêu cầu) | **không** — phải deploy | có | có |
| Validate | compile-time | phải tự viết validator JSON | ENUM + CHECK + FK ở tầng DB |
| Query/report | không | kém (JSON path) | tốt |
| Audit ai đổi gì | git | cột `updated_by` chung của template | `created_by/updated_by/updated_at` riêng |
| Cascade Template→Campus→Dept→System | cứng | khó | tự nhiên (`scope_type` + `scope_key`) |
| Phân biệt `unset` vs `false` | n/a | được (thiếu key) | được (cột NULL) |
| 3NF | n/a | vi phạm | đạt |
| Khối lượng việc | nhỏ | trung bình | lớn |

**Đề xuất: C**, với một bảng duy nhất và cột boolean **nullable** để phân biệt `unset` với `false`:

```sql
CREATE TABLE email_contact_policies (
  email_contact_policy_id BIGINT UNSIGNED AUTO_INCREMENT,
  scope_type   ENUM('TEMPLATE','CAMPUS','DEPARTMENT','SYSTEM') NOT NULL,
  scope_key    VARCHAR(64) NULL,              -- template_code | campus_id | department_id | NULL
  requirement  ENUM('NONE','OPTIONAL','REQUIRED') NULL,
  contact_source ENUM('HOST','SENDER','HOST_THEN_SENDER',
                      'CAMPUS_DEFAULT','DEPARTMENT_DEFAULT','SUPPORT_CONTACT') NULL,
  show_email TINYINT(1) NULL, show_phone TINYINT(1) NULL,   -- NULL = kế thừa
  show_department TINYINT(1) NULL, show_campus TINYINT(1) NULL, show_sender TINYINT(1) NULL,
  heading_vi VARCHAR(150) NULL, heading_en VARCHAR(150) NULL,
  reply_to_source ENUM('NONE','CONTACT','SENDER') NULL,
  ...audit,
  UNIQUE KEY uq_scope (scope_type, scope_key)
);
```

Lý do loại B: policy có ~10 thuộc tính có kiểu rõ ràng và một tập enum đóng — đúng thứ mà cột nên biểu diễn.
Chọn JSON ở đây là đánh đổi khả năng validate/query lấy tốc độ viết migration, mà plan (§11.4) đã nói rõ là
không được làm. Lý do loại A: §13 yêu cầu màn hình quản trị cấu hình được — A không đáp ứng.

**Chưa triển khai C.** Đây là quyết định thay đổi schema, và §4.1 của plan cấm thêm cột/bảng trước khi mô
hình cấu hình được đánh giá và chốt. Cần xác nhận trước khi migrate.

---

## 7. Việc còn lại

| Gate | Trạng thái |
|---|---|
| G0 preflight | ✅ |
| G1 root cause bảng | ✅ có test đỏ trước / xanh sau |
| G2 sửa bảng + preview | ✅ 2099/2099 unit test, tsc sạch |
| G3 ma trận template | ✅ 31/31 phân loại |
| G4 decision record Host/Sender/Reply | ✅ |
| G6 ADR mô hình cấu hình | ✅ đã phân tích — **chờ chốt phương án** |
| G5 resolver + contract + Reply-To | ⛔ chờ G6 |
| G7 canonical SQL + patch + defaults | ⛔ chờ G6 |
| G8 frontend cấu hình | ⛔ chờ G6 |
| G9 test đầy đủ + runtime preview | ⛔ chờ G5–G8 |
| G10 commit | ⛔ |

Integration test chưa chạy trong đợt này (cần MySQL disposable) — sẽ chạy ở G9.
