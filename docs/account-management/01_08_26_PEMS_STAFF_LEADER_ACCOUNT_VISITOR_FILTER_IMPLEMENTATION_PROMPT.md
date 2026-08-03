# PEMS — Prompt triển khai hoàn chỉnh: STAFF LEADER Account Management — Visitor scope, Nationality filter và loại bỏ “Tất cả tài khoản”

## 0. Mục đích tài liệu

Tài liệu này là **prompt triển khai đầy đủ cho AI Agent** đọc codebase PEMS và cập nhật chức năng **Quản lý tài khoản của role STAFF LEADER**.

Phạm vi chính:

1. Sửa cách lấy **danh sách tài khoản khách có liên quan đến campus của STAFF LEADER**.
2. Sửa cách lấy **danh sách quốc tịch** cho bộ lọc Visitor để dùng dữ liệu thật, đầy đủ và không hardcode.
3. Bỏ option **“Tất cả tài khoản”** khỏi bộ lọc loại tài khoản của STAFF LEADER.
4. Mặc định chọn **“Tài khoản nội bộ”** khi STAFF LEADER mở trang.
5. Thêm subtitle động dưới tiêu đề **“Quản lý tài khoản”** theo loại tài khoản đang chọn.
6. Đồng bộ backend, frontend, API contract, security, tests và tài liệu.

Không được sửa lan sang chức năng Account Management của ADMIN hoặc HO nếu không cần thiết để hoàn thành yêu cầu này.

---

# 1. Bối cảnh và kiến trúc hiện tại phải tôn trọng

## 1.1. Actor áp dụng

Chỉ áp dụng cho người dùng có:

```text
role_code = STAFF
sub_role = LEADER
status = ACTIVE
primary_campus_id IS NOT NULL
```

Trong tài liệu này gọi actor trên là:

```text
STAFF LEADER
```

## 1.2. Kiến trúc xử lý đơn hiện tại

PEMS đang dùng kiến trúc **campus xử lý độc lập**:

- Visitor hoặc người tạo đơn có thể chọn một hoặc nhiều campus.
- Mỗi campus tạo một `visit_request_campuses` riêng.
- STAFF LEADER của từng campus nhận và xử lý trực tiếp campus instance của campus mình.
- HO không còn là bước duyệt/release bắt buộc cho multi-campus.
- `visit_requests.status` là trạng thái tổng hợp.
- `visit_request_campuses.status` là trạng thái xử lý thật của từng campus.

Do đó, mọi logic còn phụ thuộc vào quy tắc cũ:

```text
Multi-campus chỉ hiện sau khi HO duyệt/release
```

phải được loại bỏ khỏi chức năng Visitor liên quan.

---

# 2. Mục tiêu nghiệp vụ cuối cùng

Khi STAFF LEADER mở:

```text
/dashboard/accounts
```

bộ lọc loại tài khoản chỉ còn:

```text
Tài khoản nội bộ
Tài khoản khách
```

Không còn:

```text
Tất cả tài khoản
```

Mặc định:

```text
Tài khoản nội bộ
```

Hai chế độ phải hoàn toàn tách biệt:

## 2.1. Tài khoản nội bộ

Hiển thị các tài khoản trong campus của STAFF LEADER thuộc các hình dạng nghiệp vụ:

```text
STAFF + STAFF
DEPARTMENT + LEADER
STUDENT
```

Subtitle bắt buộc:

```text
Quản lý tài khoản của nhân sự phòng IC, trưởng phòng của các phòng ban khác và sinh viên trong cơ sở
```

## 2.2. Tài khoản khách

Hiển thị các account `VISITOR` có ít nhất một campus instance thuộc campus của STAFF LEADER.

Subtitle theo yêu cầu UI của chủ dự án:

```text
Tất cả tài khoản của khách đã từng đến thăm cơ sở
```

> Lưu ý nghiệp vụ: danh sách Visitor theo yêu cầu phạm vi bên dưới vẫn bao gồm cả request đang chờ, bị từ chối hoặc đã hủy. Vì vậy câu subtitle trên là copy UI do chủ dự án yêu cầu, không được dùng câu này để tự ý thu hẹp query chỉ còn chuyến đã diễn ra. Không thay đổi dữ liệu scope nếu chưa được chủ dự án xác nhận lại.

---

# 3. Vấn đề hiện tại cần sửa

## 3.1. Visitor liên quan đang dùng điều kiện multi-campus cũ

Shared scope hiện tại có xu hướng:

```text
SINGLE_CAMPUS → hiện
MULTI_CAMPUS → chỉ hiện khi request APPROVED/CANCELLED và instance không còn WAITING_REQUEST_APPROVAL
```

Điều này làm thiếu Visitor thật trong các trường hợp:

```text
MULTI_CAMPUS + PENDING_APPROVAL
MULTI_CAMPUS + PARTIALLY_APPROVED
MULTI_CAMPUS + REJECTED
Campus instance đang WAITING_REQUEST_APPROVAL
```

## 3.2. Danh sách quốc tịch mới lấy từ tối đa 100 Visitor đầu tiên

Frontend hiện suy ra option quốc tịch bằng cách gọi danh sách Visitor:

```ts
getRelatedVisitors({ page: 1, pageSize: 100 })
```

rồi lấy distinct `nationality` từ trang đầu.

Đây là dữ liệu thật nhưng không đầy đủ khi campus có hơn 100 Visitor liên quan.

## 3.3. Filter loại tài khoản còn option không cần thiết

Đối với STAFF LEADER, filter loại tài khoản không được có option:

```text
Tất cả tài khoản
```

vì danh sách nội bộ và Visitor dùng hai nguồn dữ liệu, quyền và UI khác nhau.

## 3.4. Có nguy cơ gọi đồng thời API nội bộ và Visitor

Khi chuyển sang Visitor, hook danh sách nội bộ không được tiếp tục chạy ngầm.

## 3.5. Filter state có nguy cơ bị dùng chéo

Không được để:

- Role filter nội bộ ảnh hưởng Visitor.
- Nationality filter ảnh hưởng nội bộ.
- Trang hiện tại của nội bộ được dùng cho Visitor.
- `totalItems` hoặc `totalPages` của API này được dùng cho bảng kia.

---

# 4. Quy tắc chuẩn để xác định Visitor liên quan đến campus

## 4.1. Không dùng campus trên bảng users

Visitor hợp lệ không thuộc cố định một campus, do đó tuyệt đối không lọc bằng:

```sql
users.primary_campus_id = currentStaffLeader.primary_campus_id
```

## 4.2. Quan hệ đúng

Xác định theo chuỗi:

```text
users (VISITOR)
→ visit_requests.visitor_user_id
→ visit_request_campuses.visit_request_id
→ visit_request_campuses.campus_id = currentStaffLeader.primary_campus_id
```

Predicate cốt lõi:

```text
vrc.campus_id = currentStaffLeader.primary_campus_id
AND vrc.visit_request.visitor_user_id IS NOT NULL
```

## 4.3. Không phụ thuộc visit scope hoặc request-level status

Không lọc theo:

```text
visit_requests.visit_scope
visit_requests.status
HO approval
HO release
```

## 4.4. Các trạng thái campus instance vẫn tạo quan hệ

Visitor vẫn được xem là liên quan nếu campus instance thuộc campus hiện tại đang ở một trong các trạng thái:

```text
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
REJECTED
CANCELLED
```

Lý do: khi campus instance được tạo, campus đã thực sự nhận một yêu cầu liên quan đến Visitor đó. Reject hoặc cancel không xóa lịch sử quan hệ.

## 4.5. Request không có Visitor account

Nếu:

```text
visit_requests.visitor_user_id IS NULL
```

thì không đưa lên Account Management vì không có account Visitor thật để hiển thị.

## 4.6. Hình dạng account Visitor hợp lệ

Chỉ lấy user thỏa đồng thời:

```text
role_code = VISITOR
primary_campus_id IS NULL
department_id IS NULL
sub_role IS NULL
```

Nếu `visitor_user_id` trỏ tới user đã đổi sang role nội bộ hoặc dữ liệu sai shape thì không hiển thị trong tab Visitor.

---

# 5. Backend — thay đổi bắt buộc

## 5.1. Sửa `RelatedVisitorScope.cs`

File dự kiến:

```text
backend/PEMS.Application/Accounts/Common/RelatedVisitorScope.cs
```

Giữ nguyên hoặc củng cố hàm xác thực STAFF LEADER:

```text
IsAuthenticated = true
RoleCode = STAFF
SubRole = LEADER
PrimaryCampusId != null
```

Không tin `campusId` từ client.

Sửa `VisibleInstances()` thành shared predicate duy nhất:

```csharp
public static IQueryable<VisitRequestCampus> VisibleInstances(
    IApplicationDbContext db,
    ulong campusId)
{
    return db.VisitRequestCampuses.Where(vrc =>
        vrc.CampusId == campusId
        && vrc.VisitRequest.VisitorUserId != null);
}
```

Xóa comment và logic cũ nhắc đến:

```text
single-/multi-campus release rule
HO release
APPROVED/CANCELLED gate cho multi-campus
WAITING_REQUEST_APPROVAL bị ẩn
```

Comment mới phải mô tả đúng kiến trúc campus xử lý độc lập.

---

## 5.2. Sửa query danh sách Visitor

File dự kiến:

```text
backend/PEMS.Application/Accounts/Queries/RelatedVisitors/GetRelatedVisitorsQueryHandler.cs
```

Luồng xử lý bắt buộc:

```text
1. Xác thực STAFF LEADER.
2. Lấy campus từ current user.
3. Lấy tất cả visible campus instances qua RelatedVisitorScope.VisibleInstances().
4. Lấy VisitorUserId khác null.
5. Gom theo VisitorUserId.
6. Đếm distinct VisitRequestId.
7. Chỉ lấy users đúng shape VISITOR.
8. Áp dụng status, keyword, nationality.
9. Sort.
10. Pagination.
```

Mỗi Visitor chỉ xuất hiện một lần.

Giữ các dữ liệu tổng hợp nếu đang có:

```text
RelatedRequestCount
LastRelatedRequestAt
LatestPlannedStartAt
```

`RelatedRequestCount` phải đếm:

```text
DISTINCT VisitRequestId
```

không đếm số campus instance.

Keyword nên hỗ trợ:

```text
FullName
Email
Phone
Nationality
```

Status account hỗ trợ:

```text
ACTIVE
INACTIVE
LOCKED
```

Không được phân trang trước khi hoàn tất scope/filter/sort.

---

## 5.3. Sửa query chi tiết Visitor

File dự kiến:

```text
backend/PEMS.Application/Accounts/Queries/RelatedVisitors/GetRelatedVisitorDetailsQueryHandler.cs
```

Detail phải kiểm tra lại scope bằng cùng shared predicate:

```text
VisitorUserId
→ visit_requests
→ visit_request_campuses
→ campus của STAFF LEADER
```

Không được chỉ tin rằng frontend đã lấy Visitor từ list.

List, detail và nationality endpoint bắt buộc dùng chung:

```csharp
RelatedVisitorScope.VisibleInstances(...)
```

Nếu Visitor ngoài scope:

- Ưu tiên `404` để che sự tồn tại của account, hoặc
- Dùng mã lỗi scope hiện có nếu project đã thống nhất `403`.

Không trả dữ liệu nhạy cảm.

---

## 5.4. Thêm endpoint riêng lấy danh sách quốc tịch

Không dùng API danh sách Visitor phân trang để suy ra quốc tịch.

Endpoint đề xuất:

```http
GET /api/accounts/staff-leader/related-visitors/nationalities
```

Response đề xuất:

```json
{
  "items": [
    "Bồ Đào Nha",
    "Hàn Quốc",
    "Nhật Bản",
    "Pháp",
    "Singapore"
  ]
}
```

Các file dự kiến thêm:

```text
backend/PEMS.Application/Accounts/Queries/RelatedVisitors/
    GetRelatedVisitorNationalitiesQuery.cs
    GetRelatedVisitorNationalitiesQueryHandler.cs
    RelatedVisitorNationalitiesDto.cs
```

Query bắt buộc:

```text
1. Xác thực STAFF LEADER.
2. Lấy campus từ session.
3. Dùng RelatedVisitorScope.VisibleInstances().
4. Lấy distinct VisitorUserId.
5. Join users đúng shape VISITOR.
6. Lấy nationality.
7. Loại null/rỗng/chỉ khoảng trắng.
8. Trim.
9. Distinct không phân biệt hoa thường.
10. Sort ổn định.
```

Pseudo-code tham khảo:

```csharp
var campusId = RelatedVisitorScope.EnsureStaffLeaderCampus(_currentUser);

var visitorIds = RelatedVisitorScope
    .VisibleInstances(_db, campusId)
    .Select(x => x.VisitRequest.VisitorUserId!.Value)
    .Distinct();

var rawNationalities = await _db.Users
    .AsNoTracking()
    .Where(u =>
        visitorIds.Contains(u.UserId)
        && u.Role.RoleCode == RoleCodes.Visitor
        && u.PrimaryCampusId == null
        && u.DepartmentId == null
        && u.SubRole == null
        && u.Nationality != null)
    .Select(u => u.Nationality!)
    .ToListAsync(cancellationToken);

var items = rawNationalities
    .Select(x => x.Trim())
    .Where(x => x.Length > 0)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(x => x, StringComparer.Create(
        CultureInfo.GetCultureInfo("vi-VN"),
        ignoreCase: true))
    .ToList();
```

Không hardcode danh sách quốc gia ở frontend hoặc backend.

---

## 5.5. Chuẩn hóa filter nationality trong list API

Hiện nếu đang dùng so sánh trực tiếp:

```csharp
u.Nationality == request.Nationality.Trim()
```

cần sửa để không lỗi do khoảng trắng hoặc casing.

Yêu cầu hành vi:

```text
"Nhật Bản"
" Nhật Bản "
"nhật bản"
"NHẬT BẢN"
```

phải được hiểu là cùng một lựa chọn.

Có thể dùng normalization phù hợp với EF/MySQL hiện tại. Không phụ thuộc mù quáng vào collation nếu test không chứng minh được.

Ưu tiên:

- Normalize input bằng `Trim()`.
- So sánh case-insensitive.
- Không làm query mất index nghiêm trọng nếu có thể tránh.
- Có test integration với dữ liệu khác casing và khoảng trắng.

---

## 5.6. Sửa `AccountsController`

File dự kiến:

```text
backend/PEMS.Api/Controllers/AccountsController.cs
```

Giữ các endpoint hiện tại cho list/detail Visitor.

Thêm route cụ thể:

```http
GET /api/accounts/staff-leader/related-visitors/nationalities
```

Phải tránh route conflict khiến `nationalities` bị parse như `visitorUserId`.

Handler vẫn phải tự enforce STAFF LEADER. Không chỉ dựa vào frontend hoặc route visibility.

---

# 6. Frontend — thay đổi bắt buộc

## 6.1. Bỏ option “Tất cả tài khoản”

File chính:

```text
frontend/pems-react/src/pages/dashboard/accounts/AccountManagement.tsx
```

Đối với STAFF LEADER, loại tài khoản chỉ gồm:

```ts
export type StaffLeaderAccountType = 'INTERNAL' | 'VISITOR';
```

Không dùng:

```text
ALL
''
undefined để biểu diễn tất cả
```

Select bắt buộc:

```tsx
<select
  value={accountTypeFilter}
  onChange={handleAccountTypeChange}
>
  <option value="INTERNAL">Tài khoản nội bộ</option>
  <option value="VISITOR">Tài khoản khách</option>
</select>
```

Xóa hoàn toàn option:

```tsx
<option value="">Tất cả tài khoản</option>
```

Không thay đổi option của ADMIN/HO nếu UI của các role đó dùng logic riêng.

---

## 6.2. Mặc định chọn “Tài khoản nội bộ”

Khi STAFF LEADER mở trang:

```ts
const [accountTypeFilter, setAccountTypeFilter] =
  useState<StaffLeaderAccountType>('INTERNAL');
```

Mặc định phải:

```text
- Chọn Tài khoản nội bộ.
- Hiển thị subtitle nội bộ.
- Gọi API nội bộ.
- Không gọi API Visitor.
- Không gọi API nationality.
```

Không lưu lựa chọn Visitor vào `localStorage` nếu chưa có yêu cầu nghiệp vụ.

---

## 6.3. Thêm subtitle động dưới tiêu đề

Ngay dưới:

```text
Quản lý tài khoản
```

thêm subtitle.

Khi `INTERNAL`:

```text
Quản lý tài khoản của nhân sự phòng IC, trưởng phòng của các phòng ban khác và sinh viên trong cơ sở
```

Khi `VISITOR`:

```text
Tất cả tài khoản của khách đã từng đến thăm cơ sở
```

Code tham khảo:

```ts
const accountManagementSubtitle =
  accountTypeFilter === 'VISITOR'
    ? 'Tất cả tài khoản của khách đã từng đến thăm cơ sở'
    : 'Quản lý tài khoản của nhân sự phòng IC, trưởng phòng của các phòng ban khác và sinh viên trong cơ sở';
```

```tsx
<div>
  <h1 className="text-3xl font-bold text-[#004c91]">
    Quản lý tài khoản
  </h1>

  {isStaffLeader && (
    <p
      className="mt-1 text-sm text-gray-500"
      aria-live="polite"
    >
      {accountManagementSubtitle}
    </p>
  )}
</div>
```

Subtitle:

- Căn trái cùng tiêu đề.
- Không in hoa toàn bộ.
- Responsive, được xuống dòng.
- Không làm bố cục nhảy quá mạnh khi chuyển mode.

---

## 6.4. Tách UI của INTERNAL và VISITOR

### INTERNAL mode

Hiển thị:

```text
Search
Loại tài khoản
Vai trò
Trạng thái
Bảng account nội bộ
Nút Tạo tài khoản mới
```

Ẩn:

```text
Nationality filter
```

### VISITOR mode

Hiển thị:

```text
Search theo họ tên/email/SĐT/quốc tịch
Loại tài khoản
Nationality filter
Status filter nếu chức năng hiện tại còn dùng
Visitor table read-only
```

Ẩn:

```text
Role filter
Department filter
MSSV filter
Campus filter
Các action thay đổi role/status
```

Khuyến nghị bắt buộc về UX:

- Ẩn nút **“Tạo tài khoản mới”** khi đang ở VISITOR mode.
- Visitor tab chỉ cho xem list và detail.

---

## 6.5. Không gọi API nội bộ khi đang xem Visitor

Điều kiện enable hook nội bộ phải bao gồm loại tài khoản:

```ts
const internalListEnabled =
  activeTab === 'all'
  && accountTypeFilter === 'INTERNAL';

useAccountList(listParams, internalListEnabled);
```

Khi `VISITOR`:

```text
- Chỉ gọi related-visitors.
- Chỉ gọi related-visitors/nationalities.
- Không gọi account list nội bộ.
```

Khi `INTERNAL`:

```text
- Chỉ gọi account list nội bộ.
- Không gọi related-visitors.
- Không gọi nationality endpoint.
```

Phải có last-request-wins hoặc cancellation để stale response không ghi đè mode mới.

---

## 6.6. Tách filter state

Không dùng chung toàn bộ state giữa INTERNAL và VISITOR.

Đề xuất:

```ts
const [internalFilters, setInternalFilters] = useState({
  keyword: '',
  roleCode: '',
  status: '',
  page: 1,
  pageSize: 10,
});

const [visitorFilters, setVisitorFilters] = useState({
  keyword: '',
  nationality: '',
  status: '',
  page: 1,
  pageSize: 10,
});
```

Khi đổi loại tài khoản:

- Reset page của mode mới về 1 nếu cần.
- Không mang role sang Visitor.
- Không mang nationality sang Internal.
- Không dùng totalItems/totalPages của mode khác.

---

## 6.7. Sửa `RelatedVisitorsTab.tsx`

File dự kiến:

```text
frontend/pems-react/src/features/account-management/components/RelatedVisitorsTab.tsx
```

Xóa logic:

```ts
getRelatedVisitors({ page: 1, pageSize: 100 })
```

được dùng chỉ để suy ra nationality options.

Thay bằng API riêng:

```ts
getRelatedVisitorNationalities()
```

Component phải có state riêng:

```ts
nationalityOptions
nationalitiesLoading
nationalitiesError
```

Dropdown phải xử lý:

```text
Đang tải
Tất cả quốc tịch
Danh sách quốc tịch thật
Không có dữ liệu
Lỗi + thử lại
```

Khi chọn nationality:

```text
- Reset visitor page về 1.
- Gửi giá trị nationality thật.
```

Khi chọn:

```text
Tất cả quốc tịch
```

không gửi query param nationality hoặc gửi `undefined`; tuyệt đối không gửi chuỗi label.

Khuyến nghị kiến trúc:

- `AccountManagement.tsx` quản lý select INTERNAL/VISITOR, title và subtitle.
- `RelatedVisitorsTab.tsx` chỉ quản lý Visitor search, nationality, table, pagination, detail.
- Không render hai select loại tài khoản trùng nhau.

---

## 6.8. Thêm API client, endpoint và types

### `accountManagementApi.ts`

Thêm:

```ts
async getRelatedVisitorNationalities():
  Promise<RelatedVisitorNationalitiesResponse> {
  const { data } = await httpClient.get<RelatedVisitorNationalitiesResponse>(
    API_ENDPOINTS.accounts.relatedVisitorNationalities,
  );
  return data;
}
```

### `endpoints.ts`

Thêm:

```ts
relatedVisitorNationalities:
  '/accounts/staff-leader/related-visitors/nationalities',
```

### `accountManagement.types.ts`

Thêm:

```ts
export interface RelatedVisitorNationalitiesResponse {
  items: string[];
}

export type StaffLeaderAccountType =
  | 'INTERNAL'
  | 'VISITOR';
```

Có thể thêm hook:

```text
useRelatedVisitorNationalities.ts
```

để quản lý loading/error/retry/stale response.

---

# 7. Thống kê đầu trang

Không được hiển thị số liệu tài khoản nội bộ như thể đó là số liệu Visitor.

Phương án an toàn nhất trong phạm vi hiện tại:

```tsx
{accountTypeFilter === 'INTERNAL' && (
  <AccountStatisticsCards />
)}
```

Khi VISITOR mode:

- Ẩn card thống kê nội bộ, hoặc
- Chỉ hiển thị nếu có API Visitor statistics riêng.

Không tự tạo Visitor statistics ngoài phạm vi nếu không cần.

---

# 8. Security và privacy

## 8.1. Không tin campus từ frontend

Backend luôn lấy:

```text
currentUser.PrimaryCampusId
```

Không nhận hoặc không dùng:

```text
campusId query
campusId body
scope=ALL
```

## 8.2. Actor khác phải bị chặn

Các actor sau gọi API Visitor-related phải trả `403`:

```text
ADMIN
HO
STAFF + STAFF
DEPARTMENT + LEADER
DEPARTMENT + STAFF
STUDENT
VISITOR
Anonymous
```

## 8.3. Read-only

STAFF LEADER không được từ Visitor mode:

```text
Create Visitor
Update Visitor profile
Enable/Disable Visitor
Lock/Unlock Visitor
Reset password Visitor
Change Visitor role
Change Visitor campus/department
Delete Visitor
```

Frontend ẩn action nhưng backend vẫn phải chặn direct API calls bằng policy hiện có.

## 8.4. Không trả dữ liệu nhạy cảm

DTO không được trả:

```text
password_hash
password_salt
refresh_token_hash
security_stamp
provider_subject/provider_uid nếu không cần
otp/reset token
```

---

# 9. Danh sách file dự kiến thay đổi

## Backend — sửa

```text
backend/PEMS.Application/Accounts/Common/
    RelatedVisitorScope.cs

backend/PEMS.Application/Accounts/Queries/RelatedVisitors/
    GetRelatedVisitorsQuery.cs
    GetRelatedVisitorsQueryHandler.cs
    GetRelatedVisitorDetailsQueryHandler.cs

backend/PEMS.Api/Controllers/
    AccountsController.cs
```

## Backend — thêm

```text
backend/PEMS.Application/Accounts/Queries/RelatedVisitors/
    GetRelatedVisitorNationalitiesQuery.cs
    GetRelatedVisitorNationalitiesQueryHandler.cs
    RelatedVisitorNationalitiesDto.cs
```

## Frontend — sửa

```text
frontend/pems-react/src/pages/dashboard/accounts/
    AccountManagement.tsx

frontend/pems-react/src/features/account-management/components/
    RelatedVisitorsTab.tsx

frontend/pems-react/src/features/account-management/api/
    accountManagementApi.ts

frontend/pems-react/src/features/account-management/types/
    accountManagement.types.ts

frontend/pems-react/src/shared/api/
    endpoints.ts
```

## Frontend — có thể thêm

```text
frontend/pems-react/src/features/account-management/hooks/
    useRelatedVisitorNationalities.ts
```

## Tài liệu/comment cần cập nhật

```text
docs/account-management/
    UC_StaffLeader_Related_Visitor_Accounts_Tab.md

RelatedVisitorScope.cs comments
GetRelatedVisitorsQueryHandler.cs comments
RelatedVisitorsTab.tsx comments
```

---

# 10. Backend test cases bắt buộc

## 10.1. Visitor scope

1. Single-campus `PENDING_APPROVAL`, đúng campus → xuất hiện.
2. Single-campus `REJECTED`, đúng campus → vẫn xuất hiện.
3. Multi-campus `PENDING_APPROVAL`, có campus hiện tại → xuất hiện.
4. Multi-campus `PARTIALLY_APPROVED`, có campus hiện tại → xuất hiện.
5. Multi-campus `APPROVED`, có campus hiện tại → xuất hiện.
6. Multi-campus `REJECTED`, có campus hiện tại → vẫn xuất hiện lịch sử.
7. Request `CANCELLED`, có campus hiện tại → vẫn xuất hiện.
8. Visitor chỉ có request ở campus khác → không xuất hiện.
9. Một Visitor có nhiều request cùng campus → chỉ một dòng.
10. `visitor_user_id = NULL` → không xuất hiện.
11. `visitor_user_id` trỏ tới user không còn role VISITOR → không xuất hiện.
12. STAFF LEADER campus A không xem được Visitor chỉ thuộc campus B.
13. Client truyền campus khác → không thay đổi scope.
14. Caller không phải STAFF LEADER → `403`.
15. STAFF LEADER không có primary campus → `403`.

## 10.2. Detail

16. Visitor trong list → mở được detail.
17. Visitor ngoài campus → `404/403` theo convention.
18. Multi-campus pending → detail vẫn mở được theo scope mới.
19. List và detail dùng cùng shared predicate.

## 10.3. Nationality endpoint

20. Lấy nationality từ toàn bộ Visitor liên quan.
21. Có hơn 100 Visitor; nationality chỉ xuất hiện sau bản ghi thứ 100 → vẫn trả về.
22. Nationality của Visitor campus khác → không trả về.
23. `null` → loại bỏ.
24. Chuỗi rỗng → loại bỏ.
25. Chuỗi chỉ có khoảng trắng → loại bỏ.
26. `"Nhật Bản"` và `" Nhật Bản "` → một option.
27. Khác casing → một option.
28. Danh sách sort ổn định.
29. Caller không phải STAFF LEADER → `403`.
30. Nationality endpoint và list endpoint dùng cùng campus scope.

## 10.4. Nationality filter trên list

31. Chọn nationality → chỉ trả Visitor đúng nationality.
32. Casing khác nhau vẫn match.
33. Khoảng trắng đầu/cuối vẫn match.
34. Không gửi nationality → trả toàn bộ Visitor liên quan.
35. Nationality không tồn tại trong scope → danh sách rỗng.

---

# 11. Frontend test cases bắt buộc

## 11.1. Account type filter

1. STAFF LEADER mở trang → mặc định `Tài khoản nội bộ`.
2. Select chỉ có hai option.
3. Không có `Tất cả tài khoản`.
4. ADMIN/HO không bị thay đổi ngoài phạm vi.

## 11.2. Subtitle

5. INTERNAL → hiển thị đúng subtitle nội bộ.
6. VISITOR → hiển thị đúng subtitle Visitor theo yêu cầu.
7. Subtitle đổi ngay khi chuyển option.

## 11.3. API gating

8. INTERNAL mode chỉ gọi API nội bộ.
9. INTERNAL mode không gọi Visitor API.
10. INTERNAL mode không gọi nationality API.
11. VISITOR mode không gọi account list nội bộ.
12. VISITOR mode gọi related-visitors.
13. VISITOR mode gọi nationality endpoint riêng.
14. Chuyển nhanh mode không bị stale response ghi đè.

## 11.4. Nationality UI

15. Không còn request `pageSize: 100` để lấy nationality.
16. Dropdown hiển thị dữ liệu từ nationality endpoint.
17. Loading nationality độc lập với loading Visitor table.
18. Error nationality không làm hỏng Visitor table.
19. Có nút hoặc cơ chế retry khi nationality API lỗi.
20. Chọn nationality reset page Visitor về 1.
21. Chọn “Tất cả quốc tịch” không gửi nationality param.
22. Không hardcode quốc gia.

## 11.5. UI tách biệt

23. INTERNAL mode có role/status filters phù hợp.
24. VISITOR mode ẩn role filter.
25. VISITOR mode hiện nationality filter.
26. INTERNAL mode ẩn nationality filter.
27. VISITOR mode không có action sửa role/status.
28. Nút tạo tài khoản bị ẩn ở VISITOR mode.
29. Pagination Visitor dùng `totalItems/totalPages` của Visitor API.
30. Pagination nội bộ không dùng dữ liệu Visitor.

---

# 12. Regression và build gates

AI Agent phải chạy tối thiểu:

```text
Backend build
Backend unit tests liên quan Account/RelatedVisitors
Backend integration tests liên quan scope và nationality
Frontend type-check
Frontend unit/component tests
Frontend production build
```

Nếu project có full regression ổn định, chạy thêm toàn bộ suite.

Không được báo hoàn thành nếu:

- Chỉ sửa frontend nhưng backend scope vẫn sai.
- Chỉ sửa list nhưng detail vẫn dùng logic cũ.
- Nationality vẫn lấy từ page đầu.
- Option “Tất cả tài khoản” vẫn xuất hiện cho STAFF LEADER.
- API nội bộ vẫn chạy ngầm trong Visitor mode.
- Chưa có test bảo vệ multi-campus pending/partially approved.

---

# 13. Non-goals — không được tự ý làm

Không tự ý:

1. Thay đổi quyền Account Management của ADMIN hoặc HO.
2. Cho STAFF LEADER quản trị vòng đời Visitor.
3. Tạo Visitor từ Account Management.
4. Thay đổi schema Visitor nếu không cần.
5. Xóa dữ liệu lịch sử rejected/cancelled.
6. Chỉ lấy Visitor đã thực sự đến campus.
7. Thêm hardcoded country catalog.
8. Dùng `users.primary_campus_id` để scope Visitor.
9. Nhận campus scope từ frontend.
10. Tái tạo logic HO approval/release cũ.
11. Gộp dữ liệu nội bộ và Visitor vào cùng một paginated response.
12. Sửa copy subtitle khác với yêu cầu mà không báo lại.

---

# 14. Thứ tự triển khai khuyến nghị

## Giai đoạn 1 — Audit

1. Xác nhận branch/HEAD hiện tại.
2. Đọc các file Related Visitor hiện có.
3. Đọc route/controller/API client/types.
4. Xác nhận status constants thực tế.
5. Xác nhận test hiện có.

## Giai đoạn 2 — Shared backend scope

1. Sửa `RelatedVisitorScope.VisibleInstances()`.
2. Cập nhật comment.
3. Viết test scope trước hoặc song song.

## Giai đoạn 3 — List và detail

1. Xác nhận list dùng shared scope.
2. Xác nhận detail dùng shared scope.
3. Bổ sung multi-campus pending/partially/rejected tests.

## Giai đoạn 4 — Nationality endpoint

1. Thêm query/handler/DTO.
2. Thêm controller route.
3. Thêm normalization/distinct/sort.
4. Thêm tests trên 100 Visitor và dữ liệu bẩn.

## Giai đoạn 5 — Frontend account type

1. Bỏ option “Tất cả tài khoản”.
2. Mặc định INTERNAL.
3. Thêm subtitle động.
4. Tách API enable conditions.
5. Tách filter/pagination state.

## Giai đoạn 6 — Frontend nationality

1. Xóa pageSize 100 workaround.
2. Thêm API client/type/endpoint.
3. Thêm loading/error/retry.
4. Thêm tests.

## Giai đoạn 7 — Cleanup và regression

1. Xóa comment/docs cũ về HO release.
2. Kiểm tra không còn hardcode nationality.
3. Kiểm tra không còn “Tất cả tài khoản” cho STAFF LEADER.
4. Chạy build/test đầy đủ.

---

# 15. Tiêu chí nghiệm thu cuối cùng

Chỉ được coi là hoàn thành khi tất cả điều sau đúng:

```text
✓ STAFF LEADER chỉ thấy “Tài khoản nội bộ” và “Tài khoản khách”.
✓ Không còn option “Tất cả tài khoản”.
✓ Mặc định chọn “Tài khoản nội bộ”.
✓ Subtitle nội bộ hiển thị đúng.
✓ Subtitle Visitor hiển thị đúng theo yêu cầu.
✓ Visitor được xác định qua visit_request_campuses thuộc campus.
✓ Không phụ thuộc SINGLE/MULTI hoặc HO release.
✓ Multi-campus PENDING_APPROVAL được hiển thị.
✓ Multi-campus PARTIALLY_APPROVED được hiển thị.
✓ REJECTED/CANCELLED vẫn giữ quan hệ lịch sử.
✓ Visitor campus khác không bị lộ.
✓ Một Visitor chỉ xuất hiện một dòng.
✓ Request không có visitor_user_id không xuất hiện.
✓ User không đúng shape VISITOR không xuất hiện.
✓ List và detail dùng chung một scope.
✓ Nationality lấy từ toàn bộ Visitor liên quan.
✓ Không hardcode quốc gia.
✓ Không giới hạn 100 Visitor.
✓ Nationality không trùng do casing hoặc whitespace.
✓ Filter nationality hoạt động đúng.
✓ INTERNAL mode không gọi Visitor API.
✓ VISITOR mode không gọi internal account API.
✓ VISITOR mode là read-only.
✓ Frontend/backend build xanh.
✓ Tests mới và regression xanh.
```

---

# 16. Yêu cầu báo cáo cuối của AI Agent

Sau khi code xong, AI Agent phải báo cáo theo format:

```text
1. Branch và HEAD trước/sau.
2. Danh sách file đã sửa/thêm.
3. Mô tả logic scope Visitor mới.
4. Mô tả nationality endpoint mới.
5. Mô tả thay đổi UI filter/subtitle/default mode.
6. API contract cuối cùng.
7. Test cases đã thêm.
8. Kết quả build/test cụ thể.
9. Các quyết định hoặc khác biệt so với prompt.
10. Các rủi ro hoặc việc còn lại, nếu có.
```

Không được chỉ trả lời “đã hoàn thành” mà không đưa bằng chứng build/test và danh sách thay đổi.
