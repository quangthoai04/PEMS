# PEMS — HO Campus Master Data Validation Implementation Spec

> **Mục đích:** Tài liệu bàn giao chi tiết để AI Agent đọc và triển khai đồng bộ validation cho chức năng quản lý campus của role **HO** trong PEMS.
>
> **Phạm vi áp dụng:**
>
> 1. Modal **Thêm mới campus** tại trang Quản lý campus.
> 2. Trang **Chi tiết campus** khi HO bấm chỉnh sửa và lưu thay đổi.
>
> **Nguyên tắc bắt buộc:** Frontend chỉ hỗ trợ UX và báo lỗi sớm. Backend vẫn là nguồn kiểm tra cuối cùng và phải từ chối mọi payload không hợp lệ, kể cả khi client gọi API trực tiếp hoặc bỏ qua frontend validation.

---

# 1. Bối cảnh code hiện tại

Repository:

```text
quangthoai04/PEMS
```

Nhánh cần kiểm tra lại trước khi code:

```text
Dev
```

Các file trọng tâm hiện tại:

```text
frontend/pems-react/src/pages/dashboard/campus/CampusManagement.tsx
frontend/pems-react/src/pages/dashboard/campus/CampusDetail.tsx
frontend/pems-react/src/features/campus-management/api/campusManagementApi.ts
frontend/pems-react/src/features/campus-management/constants.ts
frontend/pems-react/src/features/campus-management/types/campusManagement.types.ts
frontend/pems-react/src/features/campus-management/hooks/useCampusManagement.ts

backend/PEMS.Api/Controllers/CampusesController.cs

backend/PEMS.Application/Campuses/Commands/AddNewCampus/
backend/PEMS.Application/Campuses/Commands/UpdateCampus/
backend/PEMS.Application/Campuses/Common/CampusNormalization.cs
backend/PEMS.Application/Campuses/Common/CampusDuplicateGuard.cs
backend/PEMS.Application/Campuses/Common/CampusErrorCodes.cs

backend/PEMS.Domain/Entities/Campuses/Campus.cs

tests/PEMS.UnitTests/Campuses/
tests/PEMS.IntegrationTests/
```

Trạng thái hiện tại:

- Backend create/update đã kiểm tra:
  - required;
  - độ dài tối đa;
  - format mã campus;
  - format số điện thoại cơ bản;
  - format email cơ bản.
- Frontend create/edit hiện mới kiểm tra:
  - field rỗng;
  - điện thoại có ít nhất 8 chữ số;
  - email bằng regex đơn giản.
- Backend đã normalize:
  - campus code: trim + uppercase;
  - name/address: trim + collapse spaces;
  - city: trim;
  - email: trim + lowercase;
  - phone display: trim + collapse spaces.
- Backend đã kiểm tra trùng:
  - campus code;
  - name;
  - address;
  - phone;
  - email.
- Chỉ `city` được phép trùng.
- Create campus + create IC department mặc định đang được xử lý trong cùng transaction.

---

# 2. Mục tiêu triển khai

Sau khi hoàn thành:

1. Create và edit phải dùng cùng một bộ rule validation.
2. Frontend và backend phải nhất quán.
3. Không duplicate nhiều regex/message khác nhau trong các component/validator.
4. Dirty-check của edit phải dựa trên dữ liệu đã normalize.
5. Backend vẫn kiểm tra duplicate và authorization.
6. Không phá vỡ:
   - transaction tạo campus + IC department;
   - audit log;
   - readiness logic;
   - status management;
   - scope HO/ADMIN;
   - duplicate business rules hiện có.

---

# 3. Quy tắc normalize dùng chung

## 3.1. Campus code

Tạo hàm:

```text
normalizeCampusCode(value)
```

Quy tắc:

```text
null/undefined -> ""
trim
uppercase
không tự sửa separator
```

Ví dụ:

```text
"  hp  " -> "HP"
"fpt-hn" -> "FPT-HN"
```

## 3.2. Campus name

Tạo hàm:

```text
normalizeCampusName(value)
```

Quy tắc:

```text
trim
collapse multiple spaces/tabs/newlines thành một dấu cách
không tự đổi hoa/thường
không tự bỏ dấu tiếng Việt
```

Ví dụ:

```text
"  FPT   University   Hải Phòng "
-> "FPT University Hải Phòng"
```

## 3.3. City

Tạo hàm:

```text
normalizeCampusCity(value)
```

Quy tắc:

```text
trim
map về giá trị canonical trong whitelist
```

Không cho phép text tùy ý nếu không nằm trong danh sách tỉnh/thành được hỗ trợ.

## 3.4. Address

Tạo hàm:

```text
normalizeCampusAddress(value)
```

Quy tắc:

```text
trim
collapse spaces
loại ký tự điều khiển
không giữ xuống dòng
```

## 3.5. Phone display

Tạo hàm:

```text
normalizeCampusPhoneDisplay(value)
```

Quy tắc:

```text
trim
collapse spaces
giữ format hiển thị hợp lệ của user
```

Ví dụ:

```text
"(024)   7300  5588"
-> "(024) 7300 5588"
```

## 3.6. Phone canonical key

Tạo hàm:

```text
normalizeCampusPhoneKey(value)
```

Dùng để duplicate check.

Quy tắc:

1. Bỏ:
   - space;
   - `.`;
   - `-`;
   - `(`;
   - `)`.
2. Chuẩn hóa số Việt Nam:
   - nếu bắt đầu `+84`, đổi sang dạng bắt đầu bằng `0`;
   - nếu bắt đầu `84` và không có `+`, cân nhắc đổi về `0` nếu business rule cho phép;
   - phải nhất quán giữa create/update/duplicate check.
3. Không dùng chuỗi display trực tiếp để uniqueness.

Ví dụ được xem là cùng một số:

```text
024 7300 5588
024-7300-5588
(024) 7300.5588
+84 24 7300 5588
```

## 3.7. Campus email

Tạo hàm:

```text
normalizeCampusEmail(value)
```

Quy tắc:

```text
trim
lowercase
không sửa local-part theo rule Gmail
```

Ví dụ:

```text
"  HP@FPT.EDU.VN "
-> "hp@fpt.edu.vn"
```

---

# 4. Validation mã campus

## 4.1. Rule

```text
Bắt buộc
Tối thiểu 2 ký tự
Tối đa 20 ký tự
Chỉ cho phép A-Z, 0-9, dấu - và _
Không có khoảng trắng
Không có dấu tiếng Việt
Không bắt đầu bằng - hoặc _
Không kết thúc bằng - hoặc _
Không có hai separator liên tiếp
Không được trùng campus khác
```

## 4.2. Ví dụ hợp lệ

```text
HN
HCM
HP
DN-2
FPT_HN
CAMPUS01
```

## 4.3. Ví dụ không hợp lệ

```text
H
Hà Nội
H N
-HN
HN-
_HN
HN_
HN__2
HN--2
HN-_2
HN@01
```

## 4.4. Message

```text
Vui lòng nhập mã campus.
Mã campus phải có ít nhất 2 ký tự.
Mã campus không được vượt quá 20 ký tự.
Mã campus chỉ được chứa chữ cái không dấu, chữ số, dấu gạch ngang hoặc gạch dưới.
Mã campus không được bắt đầu hoặc kết thúc bằng dấu phân cách.
Mã campus không được chứa các dấu phân cách liên tiếp.
Mã campus đã tồn tại.
```

## 4.5. Khi edit mã campus

Nếu mã thay đổi sau normalize:

```text
oldCode != newCode
```

phải hiển thị confirmation riêng:

```text
Bạn đang thay đổi mã định danh campus từ "{oldCode}" thành "{newCode}".
Các báo cáo hoặc tích hợp đang sử dụng mã cũ có thể bị ảnh hưởng.
Bạn có chắc muốn tiếp tục?
```

Không được đổi mã âm thầm.

---

# 5. Validation tên campus

## 5.1. Rule

```text
Bắt buộc
Tối thiểu 3 ký tự
Tối đa 150 ký tự
Trim
Collapse spaces
Phải chứa ít nhất một chữ cái
Không được trùng tên campus khác, case-insensitive
```

Cho phép:

```text
Chữ Unicode
Chữ số
Khoảng trắng
-
.
'
’
&
(
)
,
```

Không cho phép:

```text
HTML tag
emoji-only
ký tự điều khiển
chuỗi chỉ gồm số
chuỗi chỉ gồm dấu câu
```

## 5.2. Ví dụ hợp lệ

```text
FPT University Hà Nội
FPT University Hải Phòng
FPT Campus 2
FPT Education (Hòa Lạc)
FPT Polytechnic - Đà Nẵng
```

## 5.3. Ví dụ không hợp lệ

```text
A
12
123
...
<script>alert(1)</script>
😊😊😊
```

## 5.4. Message

```text
Vui lòng nhập tên campus.
Tên campus phải có ít nhất 3 ký tự.
Tên campus không được vượt quá 150 ký tự.
Tên campus phải chứa ít nhất một chữ cái.
Tên campus chứa ký tự không hợp lệ.
Tên campus đã tồn tại.
```

Không bắt buộc tên campus phải bắt đầu bằng `FPT`.

---

# 6. Validation tỉnh/thành phố

## 6.1. Rule

```text
Bắt buộc
Phải thuộc whitelist tỉnh/thành được hệ thống hỗ trợ
Trim trước khi so sánh
Lưu đúng giá trị canonical
Không chấp nhận text tự do từ API
```

Label đề xuất:

```text
Tỉnh/Thành phố
```

thay vì:

```text
Vị trí
```

## 6.2. Message

```text
Vui lòng chọn tỉnh/thành phố.
Tỉnh/thành phố được chọn không hợp lệ.
```

## 6.3. Legacy data

Trước khi backend whitelist cứng:

1. Kiểm tra toàn bộ dữ liệu `campuses.city`.
2. Chuẩn hóa về cùng danh sách canonical.
3. Nếu còn legacy value:
   - edit không đổi city thì cho phép giữ nguyên;
   - nếu đổi city thì giá trị mới phải thuộc whitelist.

Không được làm dữ liệu cũ trở thành không thể lưu chỉ vì đang giữ một city legacy chưa migrate.

---

# 7. Validation địa chỉ

## 7.1. Rule

```text
Bắt buộc
Tối thiểu 5 ký tự
Tối đa 255 ký tự
Trim
Collapse spaces
Không chứa ký tự điều khiển
Không chứa newline
Phải chứa ít nhất một chữ cái
Không được chỉ chứa số hoặc dấu câu
Không được trùng địa chỉ campus khác, case-insensitive sau normalize
```

Cho phép:

```text
Chữ Unicode
Chữ số
Khoảng trắng
,
.
-
/
(
)
'
’
#
```

## 7.2. Ví dụ hợp lệ

```text
Khu Giáo dục và Đào tạo, Khu Công nghệ cao Hòa Lạc, Hà Nội
Lô E2a-7, Đường D1, Khu Công nghệ cao, TP. Hồ Chí Minh
25 Nguyễn Văn Linh, Hải Châu, Đà Nẵng
Km 29 Đại lộ Thăng Long, Thạch Thất, Hà Nội
```

## 7.3. Ví dụ không hợp lệ

```text
25sđ
12345
.....
<script>
```

## 7.4. Message

```text
Vui lòng nhập địa chỉ.
Địa chỉ phải có ít nhất 5 ký tự.
Địa chỉ không được vượt quá 255 ký tự.
Địa chỉ phải chứa thông tin có ý nghĩa.
Địa chỉ chứa ký tự không hợp lệ.
Địa chỉ này đã được sử dụng cho campus khác.
```

## 7.5. Duplicate rule

Giữ business rule hiện tại:

```text
Không cho hai campus trùng địa chỉ
```

City vẫn được phép trùng.

---

# 8. Validation số điện thoại campus

## 8.1. Rule

```text
Bắt buộc
Tối đa 30 ký tự hiển thị
Tổng số chữ số từ 8 đến 15
Chỉ cho phép:
- chữ số
- dấu +
- khoảng trắng
- (
- )
- .
- -

Dấu + chỉ xuất hiện tối đa một lần
Dấu + chỉ được nằm ở đầu
Không chứa chữ cái
Không chứa extension trong cùng field
Không được trùng campus khác sau normalize
Chỉ chấp nhận dạng số Việt Nam bắt đầu bằng 0 hoặc +84
```

## 8.2. Ví dụ hợp lệ

```text
024 7300 5588
024-7300-5588
(024) 7300 5588
+84 24 7300 5588
0918271611
```

## 8.3. Ví dụ không hợp lệ

```text
1234567
024ABC5588
84+2473005588
++84 24 7300 5588
024 7300 5588 ext 123
```

## 8.4. Message

```text
Vui lòng nhập số điện thoại.
Số điện thoại phải có từ 8 đến 15 chữ số.
Số điện thoại không đúng định dạng.
Dấu + chỉ được đặt ở đầu số điện thoại.
Số điện thoại không được vượt quá 30 ký tự.
Số điện thoại này đã được sử dụng cho campus khác.
```

## 8.5. Duplicate equivalence

Các số sau phải được coi là cùng một số:

```text
024 7300 5588
024-7300-5588
(024) 7300.5588
+84 24 7300 5588
```

Backend phải dùng canonical key để so sánh.

---

# 9. Validation email campus

Email campus là email liên hệ chính thức.

## 9.1. Domain whitelist

Chỉ chấp nhận exact domain:

```text
fpt.edu.vn
fe.edu.vn
```

Không cho Gmail.

## 9.2. Rule đầy đủ

```text
Bắt buộc
Trim
Lowercase
Tối đa 150 ký tự
Local-part tối đa 64 ký tự
Có đúng một ký tự @
Không có khoảng trắng
Local-part không bắt đầu bằng dấu chấm
Local-part không kết thúc bằng dấu chấm
Không có hai dấu chấm liên tiếp
Không chứa dấu cộng +
Domain phải exact match fpt.edu.vn hoặc fe.edu.vn
Không được trùng email campus khác
```

## 9.3. Ví dụ hợp lệ

```text
hn@fpt.edu.vn
campus.hp@fpt.edu.vn
contact.qn@fe.edu.vn
```

## 9.4. Ví dụ không hợp lệ

```text
abc@gmail.com
abc@yahoo.com
abc@student.fpt.edu.vn
abc@fpt.edu.vn.fake.com
abc@fakefpt.edu.vn
abc+test@fpt.edu.vn
```

## 9.5. Message

```text
Vui lòng nhập email.
Email không đúng định dạng.
Email không được vượt quá 150 ký tự.
Phần tên email trước ký tự @ không được vượt quá 64 ký tự.
Email liên hệ campus không được chứa dấu cộng (+).
Email campus phải sử dụng tên miền @fpt.edu.vn hoặc @fe.edu.vn.
Email này đã được sử dụng cho campus khác.
```

Không dùng:

```ts
email.includes('@fpt.edu.vn')
email.endsWith('fpt.edu.vn')
```

Phải tách domain và exact-match.

---

# 10. Duplicate business rules

Giữ rule:

```text
Không được trùng:
- campusCode
- name
- address
- phone
- email

Được phép trùng:
- city
```

## 10.1. Create

Kiểm tra toàn bộ campus hiện có.

## 10.2. Edit

Loại trừ chính campus đang chỉnh sửa:

```text
excludeCampusId = currentCampusId
```

## 10.3. Concurrent duplicate

Application duplicate guard chỉ để trả lỗi thân thiện.

Đề xuất thêm:

- re-check trong transaction;
- database uniqueness hoặc normalized unique key cho:
  - code;
  - name;
  - address;
  - phone canonical key;
  - email.

Không dựa duy nhất vào application-level check vì có race condition khi hai request gửi cùng lúc.

---

# 11. Quy tắc riêng cho modal Thêm mới campus

## 11.1. Các field

```text
Mã code *
Tên campus *
Tỉnh/Thành phố *
Địa chỉ *
Số điện thoại *
Email *
```

Không thêm:

```text
Trưởng phòng IC
icHeadUserId
```

## 11.2. Button Tạo mới

Phải disabled khi:

```text
form invalid
OR creating == true
```

Không chỉ disabled khi đang gửi.

## 11.3. Validation UX

Mỗi field:

- validate on blur;
- revalidate/xóa lỗi khi on change;
- validate toàn bộ trước submit;
- border đỏ khi lỗi;
- lỗi hiển thị ngay dưới field;
- `aria-invalid`;
- `aria-describedby`;
- `maxLength`.

## 11.4. Input attributes

Campus code:

```tsx
maxLength={20}
autoCapitalize="characters"
```

Name:

```tsx
maxLength={150}
```

Address:

```tsx
maxLength={255}
```

Phone:

```tsx
maxLength={30}
inputMode="tel"
autoComplete="tel"
```

Email:

```tsx
type="email"
maxLength={150}
inputMode="email"
autoComplete="email"
```

## 11.5. Backend error

Khi backend trả `409`, `422` hoặc lỗi validation:

- giữ modal mở;
- giữ dữ liệu đã nhập;
- map lỗi field nếu có;
- nếu là lỗi chung thì hiển thị alert/toast;
- không reset form.

## 11.6. Chống double submit

- disable button ngay khi request bắt đầu;
- không cho double click;
- không cho Enter tạo request thứ hai khi đang submit;
- backend/database vẫn phải chống duplicate race.

## 11.7. Transaction hiện tại

Không thay đổi:

```text
Create campus
+ Create "Phòng Hợp tác Quốc tế" mặc định
+ Audit
= cùng transaction
```

Nếu tạo IC department fail thì rollback campus.

---

# 12. Quy tắc riêng cho trang edit campus

## 12.1. Shared validation

Create và edit phải dùng đúng cùng bộ rule.

Không được để create chặn nhưng edit lại cho phép, hoặc ngược lại.

## 12.2. Dirty check sau normalize

Dirty-check theo từng field:

```text
campusCode:
normalize uppercase

name:
trim + collapse spaces

city:
canonical value

address:
trim + collapse spaces

phone:
canonical phone key hoặc normalized display tùy mục đích

email:
trim + lowercase
```

Nếu tất cả field sau normalize giống baseline:

```text
isDirty = false
```

thì:

- nút `Lưu thay đổi` disabled;
- không gọi API;
- không tạo audit thừa;
- không cập nhật `updated_at` thừa;
- không hiện toast lỗi “Không có thay đổi nào để lưu”.

## 12.3. Button Lưu thay đổi

Disabled khi:

```text
!isDirty
OR form invalid
OR saving
```

## 12.4. Hủy edit

Giữ behavior:

```text
Nếu có thay đổi chưa lưu -> confirm
Nếu không dirty -> hủy ngay
```

## 12.5. Đổi campus code

Nếu code thực sự thay đổi sau normalize:

1. Validate toàn bộ.
2. Hiển thị confirmation riêng.
3. Chỉ sau confirm mới gọi API.

## 12.6. Scope update

Endpoint update chỉ được sửa:

```text
campusCode
name
city
address
phone
email
```

Không được sửa qua endpoint này:

```text
status
icHeadUserId
IC department
createdAt
createdBy
readiness
```

Backend tiếp tục load dữ liệu hiện tại từ DB và chỉ ghi đúng master fields.

---

# 13. Thiết kế frontend dùng chung

Tạo file:

```text
frontend/pems-react/src/features/campus-management/validation/campusMasterValidation.ts
```

Gợi ý nội dung:

```ts
export const CAMPUS_CODE_MIN_LENGTH = 2;
export const CAMPUS_CODE_MAX_LENGTH = 20;
export const CAMPUS_NAME_MIN_LENGTH = 3;
export const CAMPUS_NAME_MAX_LENGTH = 150;
export const CAMPUS_CITY_MAX_LENGTH = 100;
export const CAMPUS_ADDRESS_MIN_LENGTH = 5;
export const CAMPUS_ADDRESS_MAX_LENGTH = 255;
export const CAMPUS_PHONE_MAX_LENGTH = 30;
export const CAMPUS_PHONE_MIN_DIGITS = 8;
export const CAMPUS_PHONE_MAX_DIGITS = 15;
export const CAMPUS_EMAIL_MAX_LENGTH = 150;
export const CAMPUS_EMAIL_LOCAL_PART_MAX_LENGTH = 64;

export const ALLOWED_CAMPUS_EMAIL_DOMAINS = [
  'fpt.edu.vn',
  'fe.edu.vn',
] as const;

export function normalizeCampusCode(value?: string | null): string;
export function normalizeCampusName(value?: string | null): string;
export function normalizeCampusCity(value?: string | null): string;
export function normalizeCampusAddress(value?: string | null): string;
export function normalizeCampusPhoneDisplay(value?: string | null): string;
export function normalizeCampusPhoneKey(value?: string | null): string;
export function normalizeCampusEmail(value?: string | null): string;

export function validateCampusCode(value?: string | null): string | null;
export function validateCampusName(value?: string | null): string | null;
export function validateCampusCity(value?: string | null): string | null;
export function validateCampusAddress(value?: string | null): string | null;
export function validateCampusPhone(value?: string | null): string | null;
export function validateCampusEmail(value?: string | null): string | null;

export function validateCampusMasterForm(
  form: CampusMasterForm,
): CampusMasterFieldErrors;
```

Type:

```ts
export type CampusMasterForm = {
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
};

export type CampusMasterFieldErrors = Partial<
  Record<keyof CampusMasterForm, string>
>;
```

Cả `CampusManagement.tsx` và `CampusDetail.tsx` phải dùng utility này.

---

# 14. Thiết kế backend dùng chung

Giữ và mở rộng:

```text
backend/PEMS.Application/Campuses/Common/CampusNormalization.cs
```

Tạo thêm nếu cần:

```text
backend/PEMS.Application/Campuses/Common/CampusMasterRules.cs
backend/PEMS.Application/Campuses/Common/CampusValidationExtensions.cs
```

Gợi ý:

```csharp
public static class CampusMasterRules
{
    public const int CodeMinLength = 2;
    public const int CodeMaxLength = 20;
    public const int NameMinLength = 3;
    public const int NameMaxLength = 150;
    public const int AddressMinLength = 5;
    public const int AddressMaxLength = 255;
    public const int PhoneMinDigits = 8;
    public const int PhoneMaxDigits = 15;
    public const int PhoneMaxLength = 30;
    public const int EmailMaxLength = 150;
    public const int EmailLocalPartMaxLength = 64;

    public static readonly IReadOnlySet<string> AllowedCampusEmailDomains;

    public static bool IsValidCampusCode(string value);
    public static bool IsValidCampusName(string value);
    public static bool IsValidAddress(string value);
    public static bool IsValidPhone(string value);
    public static bool IsValidCampusEmail(string value);
    public static bool IsAllowedCity(string value);
}
```

Hai validator:

```text
AddNewCampusCommandValidator
UpdateCampusCommandValidator
```

phải dùng cùng shared rules.

Handler create/update phải dùng normalized values từ `CampusNormalization`, không tự normalize riêng.

---

# 15. Thứ tự backend validation

## 15.1. Campus code

```text
normalize
required
min
max
allowed chars
separator rule
duplicate
```

## 15.2. Name

```text
normalize
required
min
max
meaningful content
allowed chars
duplicate
```

## 15.3. City

```text
normalize
required
whitelist/canonical check
```

## 15.4. Address

```text
normalize
required
min
max
meaningful content
allowed chars
duplicate
```

## 15.5. Phone

```text
normalize display
required
max display length
allowed chars
plus placement
digit count 8–15
Vietnam prefix rule
canonical key
duplicate
```

## 15.6. Email

```text
normalize
required
max
exact one @
local-part max
dot rules
no plus
format
exact allowed domain
duplicate
```

---

# 16. Error message chuẩn

Dùng thống nhất frontend/backend:

```text
Vui lòng nhập mã campus.
Mã campus phải có ít nhất 2 ký tự.
Mã campus không được vượt quá 20 ký tự.
Mã campus chỉ được chứa chữ cái không dấu, chữ số, dấu gạch ngang hoặc gạch dưới.
Mã campus không được bắt đầu hoặc kết thúc bằng dấu phân cách.
Mã campus không được chứa các dấu phân cách liên tiếp.
Mã campus đã tồn tại.

Vui lòng nhập tên campus.
Tên campus phải có ít nhất 3 ký tự.
Tên campus không được vượt quá 150 ký tự.
Tên campus phải chứa ít nhất một chữ cái.
Tên campus chứa ký tự không hợp lệ.
Tên campus đã tồn tại.

Vui lòng chọn tỉnh/thành phố.
Tỉnh/thành phố được chọn không hợp lệ.

Vui lòng nhập địa chỉ.
Địa chỉ phải có ít nhất 5 ký tự.
Địa chỉ không được vượt quá 255 ký tự.
Địa chỉ phải chứa thông tin có ý nghĩa.
Địa chỉ chứa ký tự không hợp lệ.
Địa chỉ này đã được sử dụng cho campus khác.

Vui lòng nhập số điện thoại.
Số điện thoại phải có từ 8 đến 15 chữ số.
Số điện thoại không đúng định dạng.
Dấu + chỉ được đặt ở đầu số điện thoại.
Số điện thoại không được vượt quá 30 ký tự.
Số điện thoại này đã được sử dụng cho campus khác.

Vui lòng nhập email.
Email không đúng định dạng.
Email không được vượt quá 150 ký tự.
Phần tên email trước ký tự @ không được vượt quá 64 ký tự.
Email liên hệ campus không được chứa dấu cộng (+).
Email campus phải sử dụng tên miền @fpt.edu.vn hoặc @fe.edu.vn.
Email này đã được sử dụng cho campus khác.
```

Nếu backend đang dùng stable error code, giữ hoặc bổ sung:

```text
CAMPUS_CODE_ALREADY_EXISTS
CAMPUS_NAME_ALREADY_EXISTS
CAMPUS_ADDRESS_ALREADY_EXISTS
CAMPUS_PHONE_ALREADY_EXISTS
CAMPUS_EMAIL_ALREADY_EXISTS
CAMPUS_CODE_INVALID
CAMPUS_NAME_INVALID
CAMPUS_CITY_INVALID
CAMPUS_ADDRESS_INVALID
CAMPUS_PHONE_INVALID
CAMPUS_EMAIL_INVALID
CAMPUS_EMAIL_DOMAIN_NOT_ALLOWED
```

Frontend không được parse message để điều khiển logic.

---

# 17. Test bắt buộc

# 17.1. Campus code

Hợp lệ:

```text
HN
HCM
DN-2
FPT_HN
CAMPUS01
```

Không hợp lệ:

```text
H
Hà Nội
H N
-HN
HN-
HN__2
HN--2
HN@01
chuỗi >20 ký tự
```

Test normalize:

```text
" hp " -> "HP"
```

Test duplicate case-insensitive.

# 17.2. Campus name

Hợp lệ:

```text
FPT University Hà Nội
FPT Campus 2
FPT Education (Hòa Lạc)
```

Không hợp lệ:

```text
A
12
123
...
<script>
emoji-only
chuỗi >150
```

Test collapse spaces.

Test duplicate case-insensitive.

# 17.3. City

- giá trị whitelist hợp lệ;
- text bất kỳ gọi API trực tiếp bị reject;
- legacy value không đổi vẫn lưu được theo chiến lược migration;
- đổi city mới bắt buộc whitelist.

# 17.4. Address

Hợp lệ:

```text
25 Nguyễn Văn Linh, Đà Nẵng
Lô E2a-7, Đường D1
Km 29 Đại lộ Thăng Long
```

Không hợp lệ:

```text
25sđ
12345
.....
<script>
chuỗi >255
```

Test duplicate sau normalize.

# 17.5. Phone

Hợp lệ:

```text
024 7300 5588
024-7300-5588
(024) 7300 5588
+84 24 7300 5588
0918271611
```

Không hợp lệ:

```text
1234567
024ABC5588
84+2473005588
++84...
over 15 digits
```

Test:

```text
024 7300 5588
+84 24 7300 5588
```

được coi là duplicate.

# 17.6. Email

Hợp lệ:

```text
hn@fpt.edu.vn
campus.hp@fpt.edu.vn
contact.qn@fe.edu.vn
```

Không hợp lệ:

```text
abc@gmail.com
abc@yahoo.com
abc@student.fpt.edu.vn
abc@fpt.edu.vn.fake.com
abc+test@fpt.edu.vn
abc..def@fpt.edu.vn
.abc@fpt.edu.vn
abc.@fpt.edu.vn
local-part >64
total >150
```

Test normalize lowercase.

Test duplicate case-insensitive.

# 17.7. Create flow

Phải có:

- create valid campus;
- campus status ACTIVE;
- `ic_head_user_id = NULL`;
- IC department mặc định được tạo;
- audit được tạo;
- rollback nếu IC department create fail;
- unauthorized actor bị chặn;
- invalid direct API payload bị chặn;
- duplicate race không tạo hai campus.

# 17.8. Edit flow

Phải có:

- no-op sau normalize không gọi API;
- duplicate check exclude current campus;
- đổi code có confirmation;
- hủy dirty form có confirmation;
- update chỉ master data;
- không đổi status/head/IC department;
- lỗi backend giữ form;
- invalid direct API payload bị chặn.

# 17.9. Frontend tests

Nếu dùng Vitest/Testing Library:

- field error hiển thị đúng;
- create button disabled khi invalid;
- save button disabled khi no-op;
- email Gmail không gọi API;
- invalid phone không gọi API;
- code uppercase trước payload;
- normalized no-op không submit;
- backend error giữ dữ liệu;
- double submit bị chặn;
- code change mở confirmation.

---

# 18. Acceptance Criteria

## AC-01 — Create code validation

Given HO mở modal thêm campus  
When nhập mã sai format  
Then lỗi hiện dưới field  
And frontend không gọi API  
And backend vẫn reject direct API call.

## AC-02 — Campus email domain

Given HO nhập email campus  
When domain không phải `fpt.edu.vn` hoặc `fe.edu.vn`  
Then hệ thống từ chối.

## AC-03 — Phone normalization

Given hai số cùng bản chất nhưng khác format  
When create/update  
Then backend coi là duplicate.

## AC-04 — Address meaningful validation

Given địa chỉ `25sđ`, `12345` hoặc chỉ dấu câu  
Then hệ thống từ chối.

## AC-05 — City whitelist

Given client gửi city không có trong whitelist  
Then backend trả lỗi 4xx và không lưu.

## AC-06 — Edit no-op

Given form sau normalize giống baseline  
Then nút Lưu disabled  
And không gọi API.

## AC-07 — Edit code confirmation

Given mã campus thay đổi  
Then phải có confirmation trước khi gửi update.

## AC-08 — Duplicate exclusion

Given edit giữ nguyên dữ liệu của current campus  
Then duplicate guard không tự xung đột với chính campus đó.

## AC-09 — Transaction create

Given tạo campus thành công  
Then campus và IC department cùng tồn tại.

Given IC department tạo thất bại  
Then campus cũng không tồn tại.

## AC-10 — Backend source of truth

Given frontend bị bypass  
When gọi API bằng payload invalid  
Then backend từ chối  
And không có dữ liệu invalid được ghi.

---

# 19. Non-goals

Không triển khai ngoài phạm vi:

- Không đổi permission matrix.
- Không cho Staff Leader quản lý campus.
- Không thêm field chọn Staff Leader khi tạo campus.
- Không đổi status bằng endpoint update master data.
- Không sửa IC department bằng endpoint này.
- Không tự động gán Staff Leader.
- Không gọi third-party address/email/phone verification API.
- Không thêm map/geocoding.
- Không tự Title Case tên campus.
- Không migration toàn bộ legacy city/address nếu chưa có kế hoạch riêng.
- Không thay đổi readiness/status business flow hiện có.

---

# 20. Thứ tự triển khai khuyến nghị

1. Đọc lại code hiện tại và test liên quan.
2. Chốt whitelist city canonical.
3. Mở rộng `CampusNormalization`.
4. Tạo `CampusMasterRules`.
5. Tạo FluentValidation extensions nếu phù hợp.
6. Cập nhật AddNewCampus validator.
7. Cập nhật UpdateCampus validator.
8. Cập nhật duplicate phone canonical key.
9. Bổ sung concurrency/database uniqueness guard.
10. Thêm backend unit/integration tests.
11. Tạo frontend shared validation utility.
12. Cập nhật create modal.
13. Cập nhật CampusDetail edit.
14. Thêm code-change confirmation.
15. Thêm frontend tests.
16. Chạy:
    - backend build;
    - UnitTests;
    - IntegrationTests liên quan;
    - frontend typecheck;
    - frontend build;
    - frontend tests;
    - project structure guard.
17. Kiểm tra không phá vỡ transaction, audit, readiness và status flow.

Lệnh guard trước commit:

```powershell
.\scripts\guard-project-structure.ps1
```

---

# 21. Definition of Done

Chỉ báo hoàn thành khi:

- Create và edit dùng cùng shared validation.
- Frontend/backend nhất quán.
- Campus code rule hoạt động đầy đủ.
- Name/address meaningful validation hoạt động.
- City whitelist được backend enforce.
- Phone Việt Nam và canonical duplicate hoạt động.
- Email chỉ cho `@fpt.edu.vn` hoặc `@fe.edu.vn`.
- Gmail và plus-address bị chặn.
- Duplicate create/update đúng.
- Edit no-op không submit.
- Đổi code có confirmation.
- Create transaction vẫn atomic.
- Audit vẫn được ghi.
- Unit tests mới xanh.
- Integration tests liên quan xanh.
- Frontend build/typecheck/tests xanh.
- Không phá vỡ readiness/status/campus scope.
- Commit không chứa AI attribution.
- Gom commit theo functional slice hợp lý, không tạo quá nhiều commit nhỏ không cần thiết.
