# PROMPT — Cập nhật full code PEMS sau khi thay đổi SQL v8.4 refined v3

## 0. Bối cảnh bắt buộc

Bạn là AI Coding Agent đang cập nhật dự án **PEMS — Partnership Engagement Management System** sau khi SQL đã được chỉnh theo file mới:

```text
pems_full_seed_logic_v8_4_refined_v3.sql
```

Mục tiêu của task này là đồng bộ **toàn bộ code Backend + Frontend + Validation + Query + Enum + DTO + UI logic + Test** theo SQL mới, không để sót reference tới schema cũ.

Dự án dùng hướng **database-first/manual SQL**, vì vậy SQL mới là nguồn chuẩn cuối cùng. Không tự tạo EF migration, không auto-migrate, không runtime seed bừa. Nếu cần sửa schema thì phải sửa SQL file hoặc tạo SQL patch rõ ràng.

---

## 1. Vai trò của bạn

Bạn phải làm việc như:

```text
Senior .NET Clean Architecture Developer
Senior React TypeScript Engineer
Database-first MySQL Engineer
RBAC/Security Reviewer
Full-stack Regression Tester
```

Không được chỉ sửa lỗi build cục bộ. Phải rà toàn bộ code đang dùng schema cũ và thay thế bằng logic mới.

---

## 2. Tài liệu/file bắt buộc phải đọc trước khi sửa

Trước khi code, đọc và đối chiếu các file sau trong repo:

```text
/pems_full_seed_logic_v8_4_refined_v3.sql
/docs hoặc root docs nếu có:
- PROJECT_STRUCTURE_FULL.md
- PEMS_CLAUDE_PROJECT_INSTRUCTIONS.md
- PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY.md
- CLEAN_ARCHITECTURE.md
- PERMISSION_RULES.md
- PERMISSION_MATRIX.md
- USE_CASE_LIST.md
- USE_CASE_NOTES.md
```

Nếu repo có bản SQL cũ như `pems_full_seed_logic_v8_4_fresh_create_only_idempotent_seed.sql`, chỉ dùng để so sánh. Không dùng schema cũ làm nguồn chuẩn.

---

## 3. Nguyên tắc bắt buộc khi sửa

### 3.1. Backend Clean Architecture

Controller chỉ được:

```text
- Nhận route/query/body.
- Gọi IMediator.Send().
- Trả response.
```

Không được:

```text
- Query DbContext trực tiếp trong Controller.
- Nhét business rule trong Controller.
- Hard-code role/scope phân tán.
- Bỏ qua permission/scope.
- Bỏ qua validation.
```

### 3.2. Database-first

Không được:

```text
- Tạo auto migration nếu project đang dùng manual SQL.
- Tự thêm lại field đã bị bỏ.
- Dùng mock data thay DB thật.
- Runtime seed trong Program.cs nếu project dùng manual seed.
```

### 3.3. Không báo hoàn thành nếu chưa kiểm tra

Phải chạy hoặc ít nhất báo rõ kết quả các bước:

```text
- dotnet build
- npm run build / pnpm build / yarn build tùy project
- MySQL import SQL fresh nếu có môi trường
- grep/search xác nhận không còn reference schema cũ
```

---

## 4. Danh sách thay đổi SQL đã chốt

### 4.1. `visit_requests`

#### Đã bỏ khỏi SQL

```text
expected_guest_count
interpreter_note
```

#### Các field chuyển sang bắt buộc

```text
registrant_job_title       NOT NULL
registrant_phone           NOT NULL
registrant_nationality     NOT NULL
contact_person_full_name   NOT NULL
contact_person_organization NOT NULL
contact_person_phone       NOT NULL
contact_person_email       NOT NULL
```

#### Giữ lại

```text
working_language ENUM('VI','EN') NOT NULL DEFAULT 'EN'
```

#### Logic mới

```text
- Không còn field expected_guest_count trong API request/response/entity/query.
- Số lượng khách phải tính từ visit_guest_members với member_type = 'GUEST'.
- Không còn interpreter_note trong API request/response/entity/query.
- UI chỉ cho chọn working_language = VI hoặc EN.
- UI hiển thị helper text:
  "PEMS hiện chỉ hỗ trợ làm việc bằng Tiếng Việt hoặc Tiếng Anh. Nếu đoàn cần sử dụng ngôn ngữ khác, vui lòng tự chuẩn bị phiên dịch."
```

#### Search bắt buộc

Tìm và xóa/sửa toàn bộ reference:

```text
expectedGuestCount
expected_guest_count
InterpreterNote
interpreterNote
interpreter_note
registrantNationality nullable
contactPerson nullable
```

---

### 4.2. `visit_guest_members`

#### Đã bỏ khỏi SQL

```text
email
phone
is_representative
note
```

#### Các field giữ lại

```text
guest_member_id
visit_request_id
member_type
full_name
organization
job_title
nationality
display_order
created_at
created_by
updated_at
updated_by
```

#### Các field bắt buộc

```text
full_name     NOT NULL + CHECK TRIM <> ''
organization  NOT NULL + CHECK TRIM <> ''
job_title     NOT NULL + CHECK TRIM <> ''
nationality   NOT NULL + CHECK TRIM <> ''
```

#### Logic mới

```text
- Dùng chung bảng visit_guest_members cho 2 danh sách:
  + Danh sách khách: member_type = 'GUEST'
  + Danh sách team hỗ trợ khách: member_type = 'EXTERNAL_SUPPORT'

- UI bảng khách/team hỗ trợ chỉ gồm:
  + STT
  + Họ và tên
  + Đơn vị công tác
  + Chức vụ, phòng ban
  + Quốc tịch

- STT không lưu DB. UI lấy theo index hoặc display_order.
- Cần ít nhất 1 dòng GUEST đầy đủ.
- Cần ít nhất 1 dòng EXTERNAL_SUPPORT đầy đủ nếu form yêu cầu team hỗ trợ khách.
- Nút "Tôi là người hỗ trợ khách" phải copy thông tin người đăng ký vào một dòng EXTERNAL_SUPPORT:
  + registrant_full_name -> full_name
  + registrant_organization -> organization
  + registrant_job_title -> job_title
  + registrant_nationality -> nationality
```

#### Search bắt buộc

Tìm và xóa/sửa toàn bộ reference:

```text
GuestMember.Email
GuestMember.Phone
GuestMember.IsRepresentative
GuestMember.Note
email_snapshot từ guest member nếu đang map sai
idx_guest_members_email
idx_guest_members_representative
isRepresentative
representative
memberEmail
memberPhone
```

---

### 4.3. `minute_participants`

#### Đã thêm mới

```text
attendance_status ENUM('PRESENT','ABSENT','EXCUSED') NOT NULL DEFAULT 'PRESENT'
attendance_note TEXT NULL
checked_at DATETIME NULL
checked_by BIGINT UNSIGNED NULL FK -> users(user_id)
```

#### Logic mới

```text
- Khi tạo/edit biên bản, load danh sách điểm danh từ:
  + visit_guest_members: khách và team hỗ trợ khách
  + visit_participants join users: người nội bộ tham gia đoàn

- Khi lưu minutes, minute_participants phải lưu snapshot người tham gia và trạng thái điểm danh.
- Người được tick có mặt: attendance_status = PRESENT.
- Người vắng: attendance_status = ABSENT.
- Người vắng có lý do: attendance_status = EXCUSED + attendance_note.
- checked_at = thời điểm ghi nhận điểm danh.
- checked_by = user đang thực hiện điểm danh.
```

#### Backend cần cập nhật

```text
- Entity MinuteParticipant thêm attendance fields.
- Enum AttendanceStatus hoặc constants tương ứng.
- DTO Create/Edit/View Minutes thêm attendance fields.
- Validator validate attendance_status hợp lệ.
- Nếu attendance_status = EXCUSED thì nên yêu cầu attendance_note.
- Query detail minutes phải trả attendance status/note.
```

---

### 4.4. `departments`

#### Đã bỏ khỏi SQL

```text
department_code
```

#### Giữ lại

```text
department_id
campus_id
name
department_type
head_user_id
status
created_at
created_by
updated_at
updated_by
```

#### Logic mới

```text
- Không dùng department_code ở bất kỳ đâu.
- Department được xác định bằng department_id.
- Không hiển thị/nhập/search/import/export theo department_code.
- Unique còn lại: (campus_id, name).
- Với UI đơn giản, bảng Department chỉ hiển thị:
  + STT
  + Tên phòng
  + Trưởng phòng
  + Trạng thái
  + Hành động
- Khi tạo Department từ UI Staff/Leader, backend có thể tự set department_type = GENERAL nếu IC đã seed sẵn.
```

#### Search bắt buộc

```text
DepartmentCode
departmentCode
department_code
uq_departments_campus_code
```

Phải xóa khỏi:

```text
- Entity
- EF Configuration
- DTO List/Detail
- Create/Update Command
- Validator
- Query filter/search/sort
- Frontend type/interface
- Form payload
- Table display
- Tests
- Seed hoặc import logic
```

---

### 4.5. `visit_request_campuses`

#### Đã bỏ khỏi SQL

```text
instance_code
```

#### Logic mới

```text
- Một campus instance được xác định bằng visit_instance_id.
- Theo nghiệp vụ có thể xác định bằng cặp visit_request_id + campus_id.
- Không lưu instance_code DB.
- Không search/filter/sort theo instance_code.
- Nếu UI cần mã hiển thị tạm thời, generate ở frontend/backend từ request_code + campus_code, không lưu DB.
```

#### Search bắt buộc

```text
InstanceCode
instanceCode
instance_code
uq_visit_instance_code
```

Phải xóa khỏi:

```text
- Entity VisitRequestCampus / VisitInstance
- EF Configuration
- DTO List/Detail
- Delegation list/detail query
- Notifications/email template data nếu dùng instance_code
- Frontend table/detail/search
- Tests
```

---

### 4.6. `news_translations`

#### Đã đổi

```text
language_code ENUM('vi','en','zh','ja','ko')
```

thành:

```text
language_code VARCHAR(20) NOT NULL DEFAULT 'vi'
```

#### Logic mới

```text
- Không hard-code enum SQL vi/en/zh/ja/ko trong Entity enum.
- Backend dùng string languageCode.
- Frontend dropdown ngôn ngữ lấy từ backend config/reference API hoặc constant tập trung.
- Backend vẫn phải validate languageCode theo danh sách ngôn ngữ hỗ trợ trong config/appsettings/constants.
- Không cho frontend gửi languageCode tùy tiện không được hỗ trợ.
- Unique vẫn giữ: (news_id, language_code) và (slug, language_code).
```

#### Nếu có AI Translation API

```text
- UI cho chọn targetLanguageCode từ dropdown.
- Backend nhận sourceLanguageCode + targetLanguageCode.
- Backend validate targetLanguageCode hợp lệ.
- Backend gọi AI API qua service/config có sẵn.
- AI trả title/summary/seo/content sections đã dịch.
- Backend lưu vào news_translations + news_content_sections.
- Không publish tự động; user phải review/chỉnh sửa trước.
```

#### Search bắt buộc

```text
NewsLanguage
LanguageCode enum
language_code ENUM
'zh'
'ja'
'ko'
news translation enum
```

Cập nhật:

```text
- Entity NewsTranslation languageCode: string
- DTO languageCode: string
- Validator theo config
- Frontend type: string
- UI tabs/dropdown không phụ thuộc enum cứng
```

---

### 4.7. `minutes`

#### Đã bỏ khỏi SQL

```text
finalized_by
finalized_at
status = FINAL
```

#### Đã đổi status

```text
status ENUM('DRAFT','SAVED') NOT NULL DEFAULT 'DRAFT'
```

#### Đã thêm mới

```text
edit_locked_by BIGINT UNSIGNED NULL
edit_locked_at DATETIME NULL
edit_lock_expires_at DATETIME NULL
edit_lock_token CHAR(36) NULL
row_version INT UNSIGNED NOT NULL DEFAULT 0
```

#### Logic mới

```text
- Biên bản có thể được sửa luân phiên bởi người tham gia đoàn.
- Chỉ một người được giữ quyền edit tại một thời điểm.
- Người khác chỉ có quyền xem read-only khi đang có người giữ lock.
- Khi người đang edit Save/Cancel hoặc lock hết hạn, người khác mới được acquire lock để sửa.
- Khi visit_request_campuses.status = CLOSED thì minutes không được sửa nữa, chỉ xem.
- Không dùng finalized_by/finalized_at để khóa sửa.
- Không dùng status FINAL.
```

#### API/backend cần có hoặc cập nhật

```text
POST /api/minutes/{id}/acquire-edit-lock
POST /api/minutes/{id}/release-edit-lock
PUT  /api/minutes/{id}
GET  /api/minutes/{id}
```

Hoặc nếu route hiện tại khác, giữ route style project nhưng phải có cùng logic.

##### Acquire edit lock

```text
Input: minutesId
Check:
- User authenticated.
- User là participant hợp lệ trong visit instance/delegation.
- Visit instance chưa CLOSED.
- Minutes chưa bị lock bởi người khác hoặc edit_lock_expires_at < NOW().

Success:
- edit_locked_by = currentUserId
- edit_locked_at = NOW()
- edit_lock_expires_at = NOW() + 15 phút hoặc config
- edit_lock_token = UUID
- return editLockToken + rowVersion + lockExpiresAt

Fail:
- Nếu người khác đang lock, return read-only info: lockedByName, lockExpiresAt.
```

##### Save minutes

```text
Input bắt buộc:
- minutesId
- editLockToken
- rowVersion
- title/content/minuteParticipants/actionItems nếu có

Check:
- Visit instance chưa CLOSED.
- edit_locked_by = currentUserId.
- edit_lock_token khớp.
- edit_lock_expires_at >= NOW().
- row_version khớp.

Success:
- Update content/status = SAVED nếu đã lưu nội dung.
- row_version = row_version + 1.
- updated_by = currentUserId.
- updated_at = NOW() / DB ON UPDATE.
- Clear lock: edit_locked_by = NULL, edit_locked_at = NULL, edit_lock_expires_at = NULL, edit_lock_token = NULL.
```

##### Release lock / Cancel edit

```text
Check token/current user rồi clear lock.
Nếu lock hết hạn, backend cho phép người khác acquire lock.
```

#### Search bắt buộc

```text
Final
FINAL
finalizedBy
finalizedAt
finalized_by
finalized_at
MinutesStatus.Final
```

Phải xóa/sửa trong:

```text
- Entity Minutes
- Enum MinutesStatus
- EF Configuration
- DTO
- Commands/Handlers
- Validators
- View/Edit Minutes pages
- Tests
- Permission/UI action logic
```

---

## 5. Những phần cần rà theo layer

### 5.1. Domain Layer

Cập nhật entities:

```text
Department
VisitRequest
VisitRequestCampus / VisitInstance
VisitGuestMember
Minutes
MinuteParticipant
NewsTranslation
```

Cập nhật enums/constants:

```text
WorkingLanguage: VI, EN
GuestMemberType: GUEST, EXTERNAL_SUPPORT
AttendanceStatus: PRESENT, ABSENT, EXCUSED
MinutesStatus: DRAFT, SAVED
News language code: string, không enum SQL cố định
```

Xóa enum/field cũ:

```text
DepartmentCode
InstanceCode
ExpectedGuestCount
InterpreterNote
FinalizedBy
FinalizedAt
MinutesStatus.FINAL
GuestMember.Email/Phone/IsRepresentative/Note
```

---

### 5.2. Infrastructure Layer / EF Configuration

Cập nhật mapping:

```text
- Xóa mapping department_code.
- Xóa mapping instance_code.
- Xóa mapping expected_guest_count.
- Xóa mapping interpreter_note.
- Xóa mapping finalized_by/finalized_at.
- Xóa mapping visit_guest_members email/phone/is_representative/note.
- Thêm mapping registrant_nationality NOT NULL.
- Thêm mapping attendance fields.
- Thêm mapping edit lock fields.
- Đổi news_translations.language_code sang VARCHAR(20).
```

Nếu project dùng scaffold database-first, regenerate entity/config từ SQL mới hoặc chỉnh tay đồng bộ.

---

### 5.3. Application Layer

Rà toàn bộ command/query/handler/validator liên quan:

```text
VisitRequests
Delegations
VisitGuestMembers
MeetingMinutes
Departments
News
Reports/Dashboard nếu đếm expected_guest_count
Notifications/Emails nếu dùng instance_code
```

#### Visit request create/update

```text
- Request DTO không nhận expectedGuestCount.
- Request DTO không nhận interpreterNote.
- Request DTO bắt buộc registrantNationality.
- Request DTO bắt buộc registrantJobTitle/phone nếu form yêu cầu.
- contactPerson fields bắt buộc.
- Backend validate ít nhất 1 guest member GUEST đầy đủ.
- Backend validate từng guest/support row đầy đủ fullName/organization/jobTitle/nationality.
- expectedGuestCount nếu response cần hiển thị thì tính dynamic từ DB, không lấy cột.
```

#### Department management

```text
- Create/Update Department không có departmentCode.
- Duplicate check theo campusId + name.
- Search theo name/head/status, không search code.
```

#### Delegation list/detail

```text
- Không dùng instanceCode.
- Nếu response cần label, tạo display label từ requestCode + campusCode, không lưu DB.
```

#### Meeting minutes

```text
- Không dùng finalized.
- Implement edit-lock flow.
- Implement attendance fields.
- Chặn edit khi visit instance CLOSED.
- Chỉ participant hợp lệ được acquire edit lock/save.
```

#### News

```text
- languageCode là string.
- Validate languageCode bằng config/reference service.
- Không enum hard-code SQL vi/en/zh/ja/ko rải rác.
```

---

### 5.4. API Layer

Rà controllers:

```text
VisitRequestsController
DelegationsController
DepartmentsController
MeetingMinutesController
NewsController
ReportsController
```

Không để endpoint nhận/sinh field cũ.

Nếu API response cũ có field bị bỏ nhưng frontend vẫn cần hiển thị:

```text
expectedGuestCount -> computedGuestCount hoặc guestCount tính từ visit_guest_members.
instanceCode -> displayInstanceLabel tùy chọn, generate không lưu DB.
```

Không đặt tên response là field DB cũ nếu DB đã bỏ.

---

### 5.5. Frontend React/TypeScript

Rà các folder thường gặp:

```text
src/types
src/api
src/services
src/hooks
src/pages
src/components
src/features
```

Cập nhật:

```text
- Xóa expectedGuestCount khỏi form submit.
- Xóa interpreterNote khỏi form submit/UI.
- Form visit request bắt buộc registrantNationality.
- Danh sách khách/team hỗ trợ chỉ có 4 field dữ liệu + STT.
- Không nhập email/phone/note/isRepresentative cho từng guest member.
- Department UI không có departmentCode.
- Delegation UI không có instanceCode.
- Minutes UI hỗ trợ read-only khi bị lock.
- Minutes UI gửi editLockToken + rowVersion khi save.
- Minutes UI hiển thị attendanceStatus/note.
- News language dropdown dùng string languageCode từ config/API.
```

---

## 6. Gợi ý UI/UX cần giữ đúng

### Visit Request Guest Members

```text
Danh sách khách *
Bảng: STT | Họ và tên | Đơn vị công tác | Chức vụ, phòng ban | Quốc tịch
Có nút:
- + Thêm dòng
- Tải mẫu
- Upload danh sách

Yêu cầu: ít nhất 1 dòng, mọi field đều bắt buộc.
```

```text
Danh sách team hỗ trợ khách *
Bảng giống danh sách khách.
Có checkbox/nút: "Tôi là người hỗ trợ khách"
Khi bật, tự thêm/cập nhật một dòng EXTERNAL_SUPPORT từ thông tin người đăng ký.
Vẫn cho thêm dòng/tải mẫu/upload danh sách.
```

### Minutes Edit Lock UI

```text
- Nếu không bị lock: hiện nút Sửa.
- Nếu user bấm Sửa thành công: mở form edit.
- Nếu người khác đang sửa: hiện read-only và thông báo "Biên bản đang được sửa bởi [Tên], chỉ có thể xem".
- Khi Save/Cancel: release lock.
- Nếu lock hết hạn: cho user khác thử bấm Sửa.
- Nếu đoàn CLOSED: chỉ xem, không hiện nút Sửa.
```

---

## 7. Các câu lệnh search bắt buộc trước khi báo xong

Chạy search toàn repo, kết quả không được còn reference sai:

```bash
rg "expected_guest_count|expectedGuestCount|ExpectedGuestCount"
rg "interpreter_note|interpreterNote|InterpreterNote"
rg "department_code|departmentCode|DepartmentCode"
rg "instance_code|instanceCode|InstanceCode"
rg "finalized_by|finalizedAt|finalized_at|FinalizedBy|FinalizedAt"
rg "MinutesStatus\.FINAL|FINAL"
rg "is_representative|isRepresentative|IsRepresentative"
rg "GuestMember.*Email|GuestMember.*Phone|memberEmail|memberPhone"
rg "language_code.*ENUM|NewsLanguage|LanguageCode.*enum"
```

Lưu ý: từ `FINAL` có thể xuất hiện ở chỗ không liên quan, phải đọc ngữ cảnh. Nếu là minutes FINAL thì phải sửa.

---

## 8. Test case bắt buộc

### 8.1. Visit Request

```text
[ ] Submit visit request không gửi expectedGuestCount vẫn thành công.
[ ] Submit visit request không gửi interpreterNote vẫn thành công.
[ ] Thiếu registrantNationality -> lỗi validation.
[ ] Thiếu contact person field -> lỗi validation.
[ ] Không có guest member GUEST -> lỗi validation.
[ ] Guest member thiếu organization/jobTitle/nationality -> lỗi validation.
[ ] Checkbox "Tôi là người hỗ trợ khách" tạo EXTERNAL_SUPPORT từ registrant info.
[ ] Guest count hiển thị bằng COUNT member_type = GUEST.
```

### 8.2. Department

```text
[ ] Create department không gửi departmentCode.
[ ] Trùng name trong cùng campus -> lỗi.
[ ] Trùng name khác campus -> được nếu nghiệp vụ cho phép.
[ ] Search theo tên phòng/trưởng phòng/status hoạt động.
```

### 8.3. Delegation / Visit Instance

```text
[ ] Detail/list không cần instanceCode.
[ ] Multi-campus vẫn tạo nhiều visit_request_campuses theo visit_request_id + campus_id.
[ ] Không tạo trùng cùng campus trong cùng request.
```

### 8.4. Minutes

```text
[ ] User A acquire edit lock thành công.
[ ] User B mở cùng minutes khi A đang edit -> read-only.
[ ] User B save trực tiếp khi không có lock -> bị chặn.
[ ] User A save với token đúng -> thành công, row_version tăng, lock clear.
[ ] User A save với token sai -> bị chặn.
[ ] User B acquire sau khi A save -> thành công.
[ ] Lock hết hạn -> user khác acquire được.
[ ] Visit instance CLOSED -> không ai acquire/save được.
[ ] Attendance PRESENT/ABSENT/EXCUSED lưu đúng.
```

### 8.5. News Translation

```text
[ ] languageCode = 'fr' hoặc 'zh-CN' lưu được nếu config cho phép.
[ ] languageCode không có trong config -> lỗi validation.
[ ] Không còn enum cứng vi/en/zh/ja/ko trong SQL mapping.
```

---

## 9. Build/check commands

Chạy theo cấu trúc repo thực tế. Ví dụ:

```bash
# Backend
cd backend
dotnet restore
dotnet build

# Frontend
cd pems-react
npm install
npm run build
```

Nếu project dùng pnpm/yarn thì dùng đúng package manager hiện tại.

Nếu có MySQL local:

```bash
mysql -u root -p < database/scripts/pems_full_seed_logic_v8_4_refined_v3.sql
```

Hoặc import bằng MySQL Workbench và đảm bảo không lỗi.

---

## 10. Output mong muốn sau khi hoàn thành

Báo cáo cuối cùng phải có:

```text
1. Files changed
2. Backend changes
3. Frontend changes
4. Validation/business rule changes
5. Removed old SQL references
6. New minutes edit-lock behavior
7. Build/test result
8. Các lỗi còn lại nếu có
```

Không được báo "đã xong" nếu:

```text
- Còn build lỗi.
- Còn reference schema cũ.
- Chưa cập nhật DTO/frontend type.
- Chưa xử lý minutes edit-lock.
- Chưa validate các NOT NULL mới.
```

---

## 11. Tóm tắt ngắn cho Agent

```text
Hãy cập nhật full code PEMS theo SQL pems_full_seed_logic_v8_4_refined_v3.sql.
Các thay đổi quan trọng: bỏ expected_guest_count, interpreter_note, department_code, instance_code, finalized_by/finalized_at, guest member email/phone/is_representative/note; thêm registrant_nationality required, attendance fields cho minute_participants, edit-lock fields cho minutes, language_code news_translations chuyển VARCHAR(20).
Rà toàn bộ backend entity/EF config/enums/DTO/commands/queries/validators/handlers/controllers và frontend types/services/hooks/pages/components.
Đảm bảo logic mới: guest count tính từ visit_guest_members; team hỗ trợ dùng member_type EXTERNAL_SUPPORT; departments không dùng code; visit instances không dùng instance_code; minutes chỉ một người sửa tại một thời điểm và khóa hoàn toàn khi visit instance CLOSED; news language dynamic string validated by config.
Build backend/frontend và chạy grep xác nhận không còn reference schema cũ.
```
