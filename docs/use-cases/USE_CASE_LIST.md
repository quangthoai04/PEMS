# Use Case List — PEMS v8.2 aligned

> UC IDs and names are unchanged and aligned through SQL v8.2. UC-136 cancellation logic is documented in `USE_CASE_NOTES_UPDATED_V8_2.md` and `PERMISSION_MATRIX_UPDATED_V8_2.md`.

---

> ## ⚠️ Cập nhật tình trạng triển khai — 2026-07-02
>
> Danh sách UC ID/tên bên dưới vẫn dùng được làm mục lục tham chiếu (rà soát code không phát hiện UC nào bị đổi tên/số). Tuy nhiên một số UC trong danh sách **chưa chạy được trong code hiện tại** — rà soát trực tiếp source (nhánh `Canh-Iter1`, 2026-07-02) phát hiện:
>
> ```text
> UC-21  Search Delegations              -> Stub (NotImplementedException). Danh sách/tìm kiếm
>                                            đoàn khách thật dùng UC-20 View Guest Delegation List.
> UC-34  Submit Delegation Feedback      -> Stub (NotImplementedException) dù route đã wire.
> UC-50, UC-51, UC-52, UC-53, UC-54      -> Toàn bộ module Partner Management là stub, cả backend
>                                            lẫn frontend (frontend hoàn toàn mock, không gọi API).
> UC-55  View Document List              -> Stub. UC-37 Upload Attached Documents cũng chưa có
>                                            luồng tạo/upload document nào hoạt động.
> UC-61  Delete Gallery Item             -> Stub, không có route trong controller.
> UC-62, UC-63 Minutes List/Search       -> Stub (chỉ truy cập được minutes qua từng visit instance).
> UC-72–UC-78 Calendar Management        -> Toàn bộ scaffold chết. Lịch cá nhân/phòng ban thật nằm
>                                            trong module DepartmentReceptionTasks, không phải Calendars.
> UC-87  Assign Campus Lead              -> Stub, không có UI gọi tới.
> UC-89  Publish News                    -> Stub.
> UC-92  Add Multilingual News           -> Stub.
> UC-90  View News List (bản public)     -> Stub (chi tiết news công khai vẫn hoạt động).
> UC-116 Reassign Department Lead        -> Chạy được nhưng KHÔNG kiểm tra quyền actor nào cả.
> ```
>
> Chi tiết evidence từng dòng: `docs/PEMS_UC_IMPLEMENTATION_RULEBOOK_..._v10_FULL_UPDATED.md` mục "V11 Implementation Status Addendum".
>
> Ngoài ra, `DepartmentsController` (UC-101–116), `ReportsController` (UC-69–71), `FeedbacksController` (UC-79–80), `ApiIntegrationsController` (UC-122–130) hiện **không có authorization** trong code — gọi được ẩn danh dù danh sách này ngụ ý các UC đó có kiểm soát quyền theo role.

---


| UC ID  | UC Name                          |
| ------ | -------------------------------- |
| UC-01  | View Homepage                    |
| UC-02  | Search Information               |
| UC-03  | View Contact Info                |
| UC-04  | View Policy & Terms              |
| UC-05  | View FAQ                         |
| UC-06  | View News                        |
| UC-07  | View Partners                    |
| UC-08  | View Gallery                     |
| UC-09  | View Notifications               |
| UC-10  | Login via SSO                    |
| UC-11  | Login via Credentials            |
| UC-12  | Logout                           |
| UC-13  | Forgot Password                  |
| UC-14  | View Profile                     |
| UC-15  | Update Profile                   |
| UC-16  | Change Password                  |
| UC-17  | Submit Visit Request             |
| UC-18  | Approve Cross-Campus Request     |
| UC-19  | View Guest Delegation Details    |
| UC-20  | View Guest Delegation List       |
| UC-21  | Search Delegations               |
| UC-22  | Process Visit Request            |
| UC-23  | Create Guest Delegation          |
| UC-24  | Update Guest Delegation          |
| UC-25  | Prepare Visit Logistics          |
| UC-26  | Update Visit Logistics           |
| UC-27  | Confirm Participation            |
| UC-28  | Approve Resource Request         |
| UC-29  | Propose Resource Modification    |
| UC-30  | Confirm The Change Proposal      |
| UC-31  | Create Meeting Minutes           |
| UC-32  | Edit Meeting Minutes             |
| UC-33  | View Meeting Minutes Details     |
| UC-34  | Submit Delegation Feedback       |
| UC-35  | Scan Business Card               |
| UC-36  | Create Partner Profile           |
| UC-37  | Upload Attached Documents        |
| UC-38  | Upload Visit Photos              |
| UC-39  | Tag Faces on Photos              |
| UC-40  | Create News Article              |
| UC-41  | Close Delegation                 |
| UC-42  | View Email Template List         |
| UC-43  | View Email Template Detail       |
| UC-44  | Update Email Template            |
| UC-45  | ~~Create Email Template~~ — **DEPRECATED / NOT AVAILABLE** (xem ghi chú dưới bảng) |
| UC-46  | Edit Email Content               |
| UC-47  | Send Email                       |
| UC-48  | View Email                       |
| UC-49  | Reply to Email                   |
| UC-50  | Process Partner Creation Request |
| UC-51  | Edit Partner Information         |
| UC-52  | View Partner Lists               |
| UC-53  | Search Partners                  |
| UC-54  | View Partner Details             |
| UC-55  | View Document List               |
| UC-56  | Search Documents                 |
| UC-57  | View Gallery Item List           |
| UC-58  | Search Gallery Items             |
| UC-59  | Add Gallery Item                 |
| UC-60  | Update Gallery Item              |
| UC-61  | Delete Gallery Item              |
| UC-62  | View Minutes List                |
| UC-63  | Search/Filter Minutes            |
| UC-64  | View List FAQ                    |
| UC-65  | Create FAQ                       |
| UC-66  | Update FAQ                       |
| UC-67  | Change FAQ Visibility            |
| UC-68  | Search FAQ                       |
| UC-69  | View Dashboard Statistics        |
| UC-70  | Export Statistics Report         |
| UC-71  | Filter Dashboard By Time         |
| UC-72  | View My Events                   |
| UC-73  | View Department Calendar         |
| UC-74  | Switch View Mode                 |
| UC-75  | Add Personal Event               |
| UC-76  | Delete Personal Event            |
| UC-77  | Update Personal Event            |
| UC-78  | View Event Details               |
| UC-79  | Search/Filter Feedback           |
| UC-80  | View Feedback Summary            |
| UC-81  | Add New Campus                   |
| UC-82  | View Campus List                 |
| UC-83  | Search and Filter Campus         |
| UC-84  | View Campus Details              |
| UC-85  | Update Campus                    |
| UC-86  | Manage Campus Status             |
| UC-87  | Assign Campus Lead               |
| UC-88  | Approve News                     |
| UC-89  | Publish News                     |
| UC-90  | View News List                   |
| UC-91  | View News Details                |
| UC-92  | Add Multilingual News            |
| UC-93  | Manage News Visibility           |
| UC-94  | Edit News                        |
| UC-95  | View Account List                |
| UC-96  | Create Account                   |
| UC-97  | Manage Account Status            |
| UC-98  | View Account Details             |
| UC-99  | Search and Filter Accounts       |
| UC-100 | Update Account Role              |
| UC-101 | Add New Department               |
| UC-102 | Update Department                |
| UC-103 | Search and Filter Departments    |
| UC-104 | View Department List             |
| UC-105 | View Department Details          |
| UC-106 | Manage Department Status         |
| UC-107 | Add Department Personnel         |
| UC-108 | View Personnel Details           |
| UC-109 | Search Personnel                 |
| UC-110 | Review Assigned Tasks            |
| UC-111 | Assign Tasks                     |
| UC-112 | Sign The Service Delivery Report |
| UC-113 | Remove Personnel                 |
| UC-114 | View Coordination Tasks          |
| UC-115 | Search Coordination Tasks        |
| UC-116 | Reassign Department Lead         |
| UC-117 | View Role List                   |
| UC-118 | Create New Role                  |
| UC-119 | Configure Role Permissions       |
| UC-120 | Update Role Details              |
| UC-121 | Disable/Delete Role              |
| UC-122 | View API Configuration           |
| UC-123 | Create API Configuration         |
| UC-124 | Update API Configuration         |
| UC-125 | Delete API Configuration         |
| UC-126 | Test API Connection              |
| UC-127 | Manage API Status                |
| UC-128 | Configure Request Limit          |
| UC-129 | View API Logs                    |
| UC-130 | Search API Logs                  |
| UC-131 | Create Agenda Template           |
| UC-132 | Update Agenda Template           |
| UC-133 | Delete Agenda Template           |
| UC-134 | View Agenda Template List        |
| UC-135 | View Agenda Template Detail      |
| UC-136 | Cancel Visit Request              |

---

## Ghi chú — UC-45 `Create Email Template` (DEPRECATED, 2026-07-30)

Danh mục mẫu email là **catalog hệ thống cố định**, do backend registry
(`SystemEmailTemplates`) quyết định. Một `templateCode` tồn tại vì có caller trong một bản release
gửi nó; mã do người dùng tự tạo qua API là một dòng dữ liệu **không có gì gọi tới được** — không
handler nào tham chiếu, không dispatcher nào resolve, và nó sẽ không bao giờ đến tay người nhận.
Đây chính là cách catalog từng phình lên 9 mã chết.

Vì vậy:

- **UC-45 không còn khả dụng.** Nút "Thêm mẫu mới" đã bỏ khỏi giao diện, và `POST /api/email-templates`
  trả về lỗi nghiệp vụ ổn định `EMAIL_TEMPLATE_CATALOG_FIXED` — chặn ở handler chứ không chỉ ẩn ở UI.
- **UC-44 `Update Email Template` giữ nguyên cho HO** (quyền `E`), nhưng chỉ với các trường nội dung:
  `name`, `description`, `subjectVi`, `subjectEn`, `bodyVi`, `bodyEn`. Mã mẫu, module, trạng thái,
  định dạng và hợp đồng biến đều do registry sở hữu.
- **Đổi trạng thái mẫu hệ thống cũng bị chặn** (cùng mã lỗi). Tắt một mẫu không phải là tắt một chức
  năng — renderer từ chối mẫu không ACTIVE, nên tắt `ACCOUNT_EMAIL_CONFIRMATION` sẽ khiến mọi tài khoản
  mới nằm lại ở trạng thái chưa xác nhận mà không có gì trên màn hình giải thích tại sao.

Số hiệu UC-45 **được giữ lại** thay vì đánh số lại toàn bộ danh sách; nó chỉ được đánh dấu là không
khả dụng. Không UC nào khác bị renumber.
