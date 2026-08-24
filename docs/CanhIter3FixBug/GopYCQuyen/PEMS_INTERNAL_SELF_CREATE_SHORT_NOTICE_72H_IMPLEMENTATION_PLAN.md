# PEMS — Kế hoạch cho phép Staff / Staff Leader tự đăng ký đơn dưới 72 giờ

**Loại tài liệu:** Implementation Plan / Change Control Plan  
**Repository:** `quangthoai04/PEMS`  
**Nhánh được kiểm tra:** `Dev`  
**Commit nền được xác minh:** `cee18dca39458c64bb399fa723f2244a0086668e`  
**Ngày lập kế hoạch:** 2026-08-25  
**Mục tiêu chính:** Cho phép **Staff** và **Staff Leader** tạo Visit Request có thời gian bắt đầu **dưới 72 giờ** khi **chính tài khoản đang đăng nhập là người đăng ký (self-registration)**, nhưng không làm hỏng hoặc nới lỏng ngoài ý muốn các luồng Visitor, OTP, pending-edit, resubmit, approval, operational contact, host assignment, idempotency và các validation hiện hữu.

---

## 1. Business rule được chốt

Sau thay đổi, quy tắc phải là:

| Actor / trường hợp | Start ở tương lai nhưng < 72h | Start >= 72h | Start ở quá khứ |
|---|---:|---:|---:|
| Public Visitor | Không | Có | Không |
| Visitor đăng nhập, tự đăng ký | Không | Có | Không |
| Staff đăng nhập, tự đăng ký | Có | Có | Không |
| Staff Leader đăng nhập, tự đăng ký | Có | Có | Không |
| Staff đăng nhập nhưng khai người khác là Registrant | Không, phải qua OTP và chịu rule 72h | Có | Không |
| Staff Leader đăng nhập nhưng khai người khác là Registrant | Không, phải qua OTP và chịu rule 72h | Có | Không |

### 1.1. Ý nghĩa chính xác của “được vượt 72h”

“Được vượt 72h” **không có nghĩa bỏ kiểm tra thời gian**. Nó chỉ có nghĩa:

- Không áp dụng `MinScheduleLeadHours = 72` cho authenticated internal self-registration.
- Vẫn bắt buộc `PlannedStartAt` phải ở tương lai.
- Vẫn bắt buộc `PlannedEndAt > PlannedStartAt`.
- Vẫn bắt buộc thời lượng tối thiểu 30 phút.
- Mọi validation khác vẫn giữ nguyên.

### 1.2. Không thay đổi constant 72 giờ

**Không sửa:**

```csharp
VisitMutationPolicy.MinScheduleLeadHours = 72;
```

Lý do: 72 giờ vẫn là business rule chuẩn cho Visitor, public/OTP create, pending-edit/resubmit ở các trường hợp không có quyền override. Repository hiện còn có `VisitLeadTimeScopeTests.cs` để bảo vệ phạm vi sử dụng của rule này.

---

## 2. Hiện trạng code đã xác minh

### 2.1. Authenticated create đang phân biệt role ở backend

File:

```text
backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/
CreateVisitRequestV2CommandHandler.cs
```

Code hiện tại xác định:

```csharp
var isVisitor      = actor.Role.RoleCode == RoleCodes.Visitor;
var isRegularStaff = actor.Role.RoleCode == RoleCodes.Staff
                     && actor.SubRole == UserSubRoles.Staff;
var isStaffLeader  = actor.Role.RoleCode == RoleCodes.Staff
                     && actor.SubRole == UserSubRoles.Leader;

var isInternal = isRegularStaff || isStaffLeader;
```

Đồng thời authenticated direct-create hiện buộc người đăng ký phải là chính người đang đăng nhập:

```csharp
RegistrantIdentityRules.EnsureDirectCreateIsSelfRegistration(
    actor.Email,
    form.Registrant.Email);
```

**Kết luận:** `CreateVisitRequestV2CommandHandler` là vị trí phù hợp để quyết định quyền short-notice vì tại đây backend đã biết role thật từ DB và đã xác minh self-registration.

### 2.2. Rule 72 giờ hiện nằm ở service dùng chung

File:

```text
backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs
```

Hiện tại service luôn tính:

```csharp
var earliestAllowedStart =
    vietnamNow.AddHours(VisitMutationPolicy.MinScheduleLeadHours);
```

và từ chối nếu:

```csharp
if (cv.PlannedStartAt < earliestAllowedStart)
    throw new BusinessRuleException(...);
```

Vì vậy hiện tại Staff, Staff Leader, Visitor đều bị chặn như nhau.

### 2.3. Public OTP create cũng gọi cùng Create Service

File:

```text
backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequestV2/
VerifyAndCreateVisitRequestV2CommandHandler.cs
```

Luồng public/OTP cuối cùng cũng gọi:

```csharp
_createService.CreateV2Async(...)
```

Do đó thay đổi ở service phải thiết kế **default-deny**, nếu không public Visitor có thể vô tình được bypass 72h.

### 2.4. Frontend hiện cũng chặn 72 giờ trước khi gọi API

File:

```text
frontend/pems-react/src/features/visit-request/schema/visitRequestV2.schema.ts
```

Hiện có:

```ts
export const V2_MIN_ADVANCE_HOURS = 72;
export const V2_MIN_ADVANCE_HOURS_CREATE = V2_MIN_ADVANCE_HOURS;
```

và trong `buildCampusVisitSchema(...)`:

```ts
const minStart = new Date(Date.now() + minAdvanceHours * 60 * 60 * 1000);
if (start < minStart) {
  // validation error
}
```

Do đó chỉ sửa backend là chưa đủ; Staff vẫn sẽ bị frontend chặn.

### 2.5. Frontend đã có đủ dữ liệu để phân biệt internal + self-registration

File:

```text
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestFormV2.tsx
```

Hiện code đã xác định:

```ts
const isInternalActor =
  creatorRole === 'STAFF' || creatorRole === 'STAFF_LEADER';

const isSelfRegistrant =
  isAuthenticated &&
  isSameEmailIdentity(user?.email, watchedReg?.email);
```

Ngoài ra hook submit hiện chỉ dùng authenticated direct-create khi email Registrant trùng tài khoản đang login. Nếu không trùng, request rơi sang OTP flow.

### 2.6. Date/time picker phụ thuộc `minAdvanceHours`

File:

```text
frontend/pems-react/src/features/visit-request/components/shared/
VisitDateTimeRangePicker.tsx
```

Picker nhận:

```ts
minAdvanceHours: number;
```

và dùng `earliestStart(minAdvanceHours)` để xác định ngày sớm nhất và live validation. Vì vậy thay đổi `minAdvanceHours` cần được kiểm tra cả picker, schema và message để tránh UI/validation lệch nhau.

---

# 3. Nguyên tắc thiết kế để không làm hỏng logic khác

## 3.1. Backend là nguồn quyết định quyền

Client **không được phép** truyền trực tiếp:

```json
{
  "allowShortNotice": true
}
```

hoặc bất kỳ field tương tự nào trong API payload.

Quyền phải được backend suy ra từ:

```text
JWT/session
    -> Current User
    -> User + Role được load từ DB
    -> xác minh ACTIVE
    -> xác minh STAFF / STAFF_LEADER
    -> xác minh self-registration
    -> backend cấp capability short-notice
```

Điều này ngăn Visitor sửa request bằng Postman/DevTools để tự bypass.

## 3.2. Default phải là “không bypass”

Mọi caller không chứng minh được quyền phải tự động giữ 72h.

Nếu thêm parameter vào Create Service, giá trị mặc định phải là:

```csharp
false
```

Không được default `true`.

## 3.3. Tách “past-time protection” khỏi “72h lead-time”

Đây là thay đổi quan trọng nhất để tránh bug.

Trước sửa, điều kiện `< now + 72h` vô tình cũng chặn luôn thời gian quá khứ. Khi Staff được exempt 72h, nếu chỉ viết:

```csharp
if (!allowShortNotice && cv.PlannedStartAt < earliestAllowedStart)
```

thì Staff có thể lọt lịch quá khứ nếu không có guard riêng.

Vì vậy phải tạo **absolute invariant**:

```text
PlannedStartAt phải ở tương lai với mọi actor.
```

sau đó mới xét 72h.

## 3.4. Không thay đổi pending-edit override hiện tại

Pending-campus edit hiện có cơ chế riêng:

- phải là Staff Leader của đúng campus;
- đồng thời là Registrant;
- có `overrideLeadTimeConfirmed`;
- có thể “Lưu và duyệt”.

Đây là một use case khác. Change này chỉ mở quyền cho **CREATE mới của Staff/Staff Leader self-registration**.

**Không refactor chung hai rule trong cùng patch**, vì rất dễ làm thay đổi authorization semantics của pending-edit.

## 3.5. Không thay đổi public/OTP semantics

Public Visitor hoặc internal user đang đăng ký thay người khác vẫn phải đi OTP và giữ 72h.

Không được suy luận “người đang gõ form là Staff nên request này được bypass”. Điều kiện phải dựa trên **actor là internal + request là self-registration authenticated direct-create**.

---

# 4. Kế hoạch thay đổi backend

## PHASE BE-1 — Tạo capability rõ nghĩa ở command handler

File:

```text
backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/
CreateVisitRequestV2CommandHandler.cs
```

Sau khi đã:

1. xác minh actor authenticated;
2. load actor + Role từ DB;
3. xác minh actor ACTIVE;
4. xác định `isRegularStaff`, `isStaffLeader`;
5. gọi `EnsureDirectCreateIsSelfRegistration(...)`;

mới tính:

```csharp
var allowShortNoticeCreate = isRegularStaff || isStaffLeader;
```

Tên nên thể hiện đúng phạm vi, ví dụ:

```csharp
allowShortNoticeCreate
```

hoặc:

```csharp
mayBypassCreateLeadTime
```

Không nên dùng tên quá chung như `isPrivileged`, `skipValidation`, `adminMode`.

### Security requirement

Capability này **không lấy từ DTO**, không lấy từ query param, không lấy từ header tự do.

---

## PHASE BE-2 — Mở rộng `IVisitRequestV2CreateService` theo hướng default-deny

File:

```text
backend/PEMS.Application/Common/Interfaces/IVisitRequestV2CreateService.cs
```

Khuyến nghị thay đổi signature:

```csharp
Task<VisitRequest> CreateV2Async(
    VisitRequestFormDataV2 form,
    ulong? registrantUserId,
    string createdSource,
    DateTime vietnamNow,
    CancellationToken cancellationToken = default,
    IReadOnlyDictionary<string, CampusHostProposalSeed>? hostProposals = null,
    bool allowShortNoticeCreate = false);
```

### Vì sao dùng `false` mặc định

Repository có nhiều test/service caller gọi trực tiếp `CreateV2Async(...)`. Search code hiện tại cho thấy caller xuất hiện ở:

- `CreateVisitRequestV2CommandHandler.cs`
- `VerifyAndCreateVisitRequestV2CommandHandler.cs`
- nhiều Integration Test trực tiếp gọi service
- các test về multi-day, operational contact, notification routing, notes persistence, campus guard...

Nếu parameter default là `false`, các caller cũ vẫn giữ semantics 72h và giảm nguy cơ regression.

### Không nên

- Không overload bằng role string.
- Không truyền `RoleCode` xuống infrastructure service rồi để service tự authorize.
- Không để service tự đọc role từ `_currentUser` cho rule này nếu command handler đã chứng minh role/self-registration.

Mục tiêu: handler quyết định authorization; service thi hành capability.

---

## PHASE BE-3 — Tách schedule validation thành các invariant độc lập

File:

```text
backend/PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs
```

Logic mục tiêu:

```csharp
var earliestAllowedStart =
    vietnamNow.AddHours(VisitMutationPolicy.MinScheduleLeadHours);

foreach (var cv in form.CampusVisits)
{
    if (cv.PlannedEndAt <= cv.PlannedStartAt)
        throw new BusinessRuleException(
            "Thời gian kết thúc phải sau thời gian bắt đầu.",
            VisitRequestErrorCodes.InvalidVisitTime);

    if ((cv.PlannedEndAt - cv.PlannedStartAt).TotalMinutes < MinDurationMinutes)
        throw new BusinessRuleException(
            "Mỗi buổi thăm phải kéo dài tối thiểu 30 phút.",
            VisitRequestErrorCodes.InvalidVisitTime);

    // Absolute invariant: không ai được tạo lịch quá khứ.
    if (cv.PlannedStartAt < vietnamNow)
        throw new BusinessRuleException(
            "Thời gian bắt đầu phải ở trong tương lai.",
            VisitRequestErrorCodes.InvalidVisitTime);

    // Registration lead-time: chỉ exempt internal authenticated self-create.
    if (!allowShortNoticeCreate && cv.PlannedStartAt < earliestAllowedStart)
        throw new BusinessRuleException(
            VisitScheduleMessages.LeadTimeNotMet(earliestAllowedStart),
            VisitRequestErrorCodes.InvalidVisitTime);
}
```

### Boundary phải chốt rõ

Nên dùng semantics hiện tại:

```text
start == now + 72h  -> PASS
start <  now + 72h  -> FAIL nếu không có capability
```

Đối với internal self-create:

```text
start > now         -> có thể PASS
start < now         -> FAIL
```

Cần quyết định riêng trường hợp `start == now`. Khuyến nghị **FAIL** vì thực tế không đủ thời gian xử lý và dễ bị vượt sang quá khứ trong vài mili-giây/giây giữa client và server. Nếu code dùng `<= vietnamNow`, test phải khóa boundary đó.

---

## PHASE BE-4 — Chỉ authenticated self-create truyền capability `true`

Trong `CreateVisitRequestV2CommandHandler.cs`:

```csharp
created = await _createService.CreateV2Async(
    form,
    registrantUserId,
    createdSource,
    now,
    cancellationToken,
    seeds,
    allowShortNoticeCreate: isInternal);
```

Nhưng chỉ được thực hiện **sau** `EnsureDirectCreateIsSelfRegistration(...)`.

### Luồng public phải giữ default false

Trong:

```text
VerifyAndCreateVisitRequestV2CommandHandler.cs
```

không truyền `true`:

```csharp
created = await _createService.CreateV2Async(
    boundForm,
    registrantUserId,
    "VISITOR_SUBMITTED",
    now,
    cancellationToken);
```

Kết quả:

```text
Public/OTP -> allowShortNoticeCreate = false -> 72h
Authenticated Visitor -> false -> 72h
Authenticated Staff self -> true -> short notice allowed
Authenticated Staff Leader self -> true -> short notice allowed
```

---

## PHASE BE-5 — Không thay đổi các validation/create rules còn lại

Sau patch, các phần sau phải giữ nguyên:

- `CampusAvailabilityEvaluator`.
- Campus ACTIVE.
- Active IC Department.
- Valid Staff Leader/coordinator.
- Campus duplicate guard.
- End > Start.
- Minimum duration 30 phút.
- Partner validation.
- Guest organization partner policy.
- Operational contact required.
- Operational contact external-only rule.
- Internal creator không được tự appoint chính mình làm operational contact.
- Member validation.
- `SubmissionId`/idempotency.
- Duplicate fingerprint.
- Contact confirmation gate.
- Proposed host authorization.
- Aggregate status calculation.
- Audit create hiện hữu.
- Notification sau commit.
- Transaction atomicity.

Không gom refactor các rule này vào patch short-notice.

---

# 5. Kế hoạch thay đổi frontend

## PHASE FE-1 — Không đổi constant 72h toàn cục

File:

```text
frontend/pems-react/src/features/visit-request/schema/visitRequestV2.schema.ts
```

Giữ:

```ts
export const V2_MIN_ADVANCE_HOURS = 72;
export const V2_MIN_ADVANCE_HOURS_CREATE = V2_MIN_ADVANCE_HOURS;
```

Lý do: Visitor vẫn cần 72h và các luồng khác có thể dựa vào constant này.

---

## PHASE FE-2 — Tính `minAdvanceHours` theo actor + self-registration

File:

```text
frontend/pems-react/src/features/visit-request/components/v2/VisitRequestFormV2.tsx
```

Code hiện đã có:

```ts
const isInternalActor =
  creatorRole === 'STAFF' || creatorRole === 'STAFF_LEADER';

const isSelfRegistrant =
  isAuthenticated &&
  isSameEmailIdentity(user?.email, watchedReg?.email);
```

Xây capability:

```ts
const canCreateShortNotice =
  isInternalActor && isSelfRegistrant;
```

Sau đó truyền vào hook một giá trị lead-time phù hợp.

**Tuy nhiên không nên chỉ dùng `0` mà không kiểm tra past-time**, vì schema hiện xây `minStart` từ `Date.now() + minAdvanceHours`. Phải đảm bảo `0` vẫn tương đương “không trước hiện tại”, không phải “mọi thời gian đều hợp lệ”.

---

## PHASE FE-3 — Làm rõ API của hook

File:

```text
frontend/pems-react/src/features/visit-request/hooks/useVisitRequestFormV2.ts
```

Hiện hook nhận:

```ts
minAdvanceHours?: number;
```

Có hai hướng:

### Hướng A — thay đổi tối thiểu

Truyền:

```ts
minAdvanceHours: canCreateShortNotice ? 0 : V2_MIN_ADVANCE_HOURS_CREATE
```

Ưu điểm:

- Ít sửa.
- Tận dụng schema/picker hiện tại.

Nhược điểm:

- Message có thể hiển thị “0 giờ”.
- Cần xác minh helper `earliestStart(0)` và picker không cho chọn giờ đã qua.

### Hướng B — khuyến nghị nếu muốn semantics rõ hơn

Tách:

```ts
schedulePolicy: {
  minimumLeadHours: number;
  shortNoticeAllowed: boolean;
}
```

hoặc ít nhất giữ `minAdvanceHours` nhưng UI message dựa trên `canCreateShortNotice` thay vì dựa duy nhất vào số `0`.

**Khuyến nghị:** dùng Hướng A cho logic validation nhưng thêm boolean UI riêng để không hiển thị message “0 giờ”. Không cần refactor lớn toàn form trong patch này.

---

## PHASE FE-4 — Kiểm tra `VisitDateTimeRangePicker`

File:

```text
frontend/pems-react/src/features/visit-request/components/shared/
VisitDateTimeRangePicker.tsx
```

Picker hiện:

```ts
const minStart = useMemo(
  () => earliestStart(minAdvanceHours),
  [minAdvanceHours]
);
```

và dùng `minStartDate` cho `<input type="date" min=...>` cùng live validation.

Cần xác minh/test:

1. `minAdvanceHours = 0` -> ngày hôm nay được chọn.
2. giờ đã qua của hôm nay vẫn bị báo lỗi.
3. giờ tương lai hôm nay được chọn.
4. không tự nhảy ngày thành `now + 72h`.
5. đổi role/email registrant từ self sang delegated làm rule trở về 72h ngay.
6. draft cũ không làm stale `minAdvanceHours`.
7. end-time suggestion vẫn giữ minimum duration 30 phút.
8. multi-day vẫn hoạt động.

Không sửa logic duration hoặc multi-day nếu không cần.

---

## PHASE FE-5 — Message và tooltip

Hiện UI dùng `rulesHint` với `hours: vm.minAdvanceHours`.

Nếu Staff nhận `0`, không được hiển thị:

> “Đăng ký trước ít nhất 0 giờ”.

Nên có 2 message:

### Visitor / delegated OTP

> Thời gian bắt đầu phải cách thời điểm hiện tại ít nhất 72 giờ. Mỗi buổi thăm tối thiểu 30 phút.

### Internal self-registration

> Đơn do nhân sự nội bộ tự đăng ký có thể chọn lịch dưới 72 giờ. Thời gian bắt đầu phải ở tương lai và mỗi buổi thăm tối thiểu 30 phút.

Cần cập nhật cả:

```text
frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json
frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json
```

Không hardcode tiếng Việt trực tiếp vào component.

---

# 6. Audit / traceability

## 6.1. Khuyến nghị ghi nhận short-notice create

Authenticated create hiện đã ghi audit các action create. Nên cân nhắc bổ sung khả năng truy vết cho trường hợp có ít nhất một campus `<72h`.

Ví dụ action:

```text
CREATE_VISIT_REQUEST_SHORT_NOTICE
```

hoặc giữ action create hiện tại nhưng thêm metadata/chi tiết nếu cấu trúc AuditLog hỗ trợ.

Thông tin nên truy vết:

- ActorUserId.
- VisitRequestId.
- Role/SubRole.
- Campus/VisitInstance.
- `PlannedStartAt`.
- số giờ lead-time thực tế tại thời điểm create.
- CreatedAt.

### Không bắt buộc migration nếu AuditLog hiện không hỗ trợ metadata

Không mở rộng DB chỉ để hoàn thành feature nếu không cần. Có thể ghi một audit action riêng bằng cấu trúc hiện tại.

---

# 7. Test plan bắt buộc

## 7.1. Backend service boundary tests

File hiện có:

```text
tests/PEMS.IntegrationTests/VisitRequests/CreateVisitRequestV2ServiceTests.cs
```

Phải giữ test cũ:

| Test | Expected |
|---|---|
| `now + 71h59m` không capability | FAIL `INVALID_VISIT_TIME` |
| `now + 72h` không capability | PASS |

Thêm:

| Test | Capability | Expected |
|---|---:|---:|
| `now + 1m` | true | PASS nếu business chấp nhận |
| `now + 1h` | true | PASS |
| `now + 24h` | true | PASS |
| `now + 71h59m` | true | PASS |
| `now + 72h` | true | PASS |
| `now - 1m` | true | FAIL |
| `now - 1m` | false | FAIL |
| End == Start | true | FAIL |
| Duration 29m59s | true | FAIL |
| Duration 30m | true | PASS |

> Lưu ý: test phải dùng cùng semantics precision với DB (`DATETIME`/wall clock). Tránh flaky test do milliseconds.

---

## 7.2. Authenticated command authorization tests

Cần test qua `CreateVisitRequestV2CommandHandler`, không chỉ service.

### Staff

```text
STAFF + self-registration + +24h -> PASS
STAFF + self-registration + +71h -> PASS
```

### Staff Leader

```text
STAFF_LEADER + self-registration + +24h -> PASS
STAFF_LEADER + self-registration + +71h -> PASS
```

### Visitor

```text
VISITOR + self-registration + +24h -> FAIL
VISITOR + self-registration + +72h -> PASS
```

### Inactive internal account

```text
STAFF inactive + +24h -> Forbidden
STAFF_LEADER inactive + +24h -> Forbidden
```

---

## 7.3. Delegated/OTP regression

Quan trọng nhất:

```text
Staff đang login
Registrant email != actor email
Start = now + 24h
        -> không direct-create
        -> OTP flow
        -> 72h vẫn áp dụng
        -> FAIL
```

Tương tự Staff Leader.

Public Visitor:

```text
Public OTP + +24h -> FAIL
Public OTP + +72h -> PASS
```

---

## 7.4. Tampering / security tests

- Visitor gọi trực tiếp authenticated create API với lịch +24h -> FAIL.
- Payload không có field public nào để bật bypass.
- Giả role trong body không ảnh hưởng vì role lấy từ DB/current user.
- Staff đăng ký người khác không được lợi từ role của người đang gõ form.
- Không được bypass bằng `createdSource = STAFF_CREATED` vì `createdSource` do backend tạo, không lấy từ client.

---

## 7.5. Multi-campus tests

### Case MC-01

```text
STAFF self:
HN  +12h
HCM +24h
DN  +48h
```

Expected: toàn request PASS nếu mọi validation khác hợp lệ.

### Case MC-02

```text
STAFF self:
HN +12h
HCM start ở quá khứ
```

Expected:

- toàn request FAIL;
- không campus nào được persist;
- transaction rollback hoàn toàn.

### Case MC-03

```text
VISITOR:
HN +12h
HCM +80h
```

Expected: toàn request FAIL do HN vi phạm 72h.

### Case MC-04

```text
STAFF self:
HN +12h
HCM duration 20 phút
```

Expected: toàn request FAIL; short-notice không bypass minimum duration.

---

# 8. Frontend tests

## 8.1. Schema tests

File hiện có:

```text
frontend/pems-react/src/features/visit-request/__tests__/
visitRequestV2.schema.test.ts
```

Thêm test:

- `minAdvanceHours=72`, +24h -> invalid.
- `minAdvanceHours=72`, +72h -> valid.
- `minAdvanceHours=0`, future -> valid.
- `minAdvanceHours=0`, past -> invalid.
- duration 29m -> invalid dù short-notice.
- duration 30m -> valid.

## 8.2. Hook tests

File hiện có:

```text
frontend/pems-react/src/features/visit-request/__tests__/
useVisitRequestFormV2.test.tsx
```

Kiểm tra:

- authenticated Staff self -> direct create + short notice.
- authenticated Staff Leader self -> direct create + short notice.
- authenticated Visitor self -> vẫn 72h.
- Staff nhưng registrant email đổi sang email khác -> rule trở về 72h và submit chuyển OTP.
- đổi email trở lại actor email -> short-notice capability trở lại.

## 8.3. Picker tests

File hiện có:

```text
frontend/pems-react/src/features/visit-request/__tests__/
visitDateTimeRangePicker.test.tsx
```

Thêm:

- `minAdvanceHours=0`: date hôm nay không bị disable.
- giờ trong quá khứ báo lỗi.
- giờ tương lai hôm nay hợp lệ.
- `minAdvanceHours=72`: giữ behavior cũ.
- multi-day và duration không regression.

---

# 9. Regression test các phần không được phép hỏng

Sau khi feature test xanh, phải chạy regression tối thiểu cho các nhóm sau.

## 9.1. Create Visit V2

- authenticated Visitor create.
- authenticated Staff create.
- authenticated Staff Leader create.
- public initiate/verify OTP.
- idempotent retry.
- duplicate fingerprint.
- multi-campus create.
- multi-day schedule.

## 9.2. Operational Contact

- external-only contact validation.
- internal registrant không thể tự làm contact.
- invitation mint.
- confirmation gate.
- member/contact link.

## 9.3. Host proposal

- Visitor không tự chọn internal processing ngoài rule.
- Staff/Leader host proposal authorization giữ nguyên.
- proposed host activation chỉ diễn ra khi gate hợp lệ.

## 9.4. Pending edit / resubmit

Không được làm thay đổi:

- `VisitMutationPolicy.MinScheduleLeadHours`.
- `EvaluateScheduleLeadTime(...)`.
- `overrideLeadTimeConfirmed`.
- `ActsAsCampusLeader`.
- `CanSaveAndApprove`.
- per-campus edit relation authorization.
- resubmit rule.

## 9.5. Approval / reject / safe edit / amendment / transfer host

Không đưa capability `allowShortNoticeCreate` sang các luồng này.

---

# 10. Danh sách file dự kiến thay đổi

## Backend — bắt buộc

```text
backend/PEMS.Application/Common/Interfaces/
IVisitRequestV2CreateService.cs

backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/
CreateVisitRequestV2CommandHandler.cs

backend/PEMS.Infrastructure/Services/
VisitRequestV2CreateService.cs
```

## Backend — test

```text
tests/PEMS.IntegrationTests/VisitRequests/
CreateVisitRequestV2ServiceTests.cs
```

Có thể thêm/tận dụng test command-level phù hợp để chứng minh role authorization.

## Frontend — bắt buộc

```text
frontend/pems-react/src/features/visit-request/schema/
visitRequestV2.schema.ts

frontend/pems-react/src/features/visit-request/hooks/
useVisitRequestFormV2.ts

frontend/pems-react/src/features/visit-request/components/v2/
VisitRequestFormV2.tsx

frontend/pems-react/src/features/visit-request/components/shared/
VisitDateTimeRangePicker.tsx
```

> `VisitDateTimeRangePicker.tsx` chỉ sửa nếu test chứng minh cần. Nếu `earliestStart(0)` đã xử lý đúng future-only thì ưu tiên không sửa logic picker.

## i18n

```text
frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json
frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json
```

## Frontend — test

```text
frontend/pems-react/src/features/visit-request/__tests__/
visitRequestV2.schema.test.ts
useVisitRequestFormV2.test.tsx
visitDateTimeRangePicker.test.tsx
```

---

# 11. Files không nên sửa trong patch này

Trừ khi compile/test chứng minh có dependency bắt buộc, không sửa:

```text
backend/PEMS.Domain/Policies/VisitMutationPolicy.cs
backend/PEMS.Infrastructure/Services/VisitRequestV2EditService.cs
backend/PEMS.Infrastructure/Services/VisitAmendmentService.cs
backend/PEMS.Infrastructure/Services/VisitSafeEditService.cs
backend/.../UpdatePendingVisitInstanceV2CommandHandler.cs
backend/.../VisitRequestOwnership.cs
backend/.../CampusApprovalExecutor.cs
```

Lý do: đây là các policy của mutation/post-create. Mở rộng phạm vi patch sẽ tăng nguy cơ regression và làm khó review root cause nếu test fail.

---

# 12. Trình tự triển khai an toàn

```text
STEP 1  Freeze business matrix
    ↓
STEP 2  Add backend capability parameter default=false
    ↓
STEP 3  Add absolute past-time guard in Create Service
    ↓
STEP 4  Authenticated Staff/Leader self-create passes capability=true
    ↓
STEP 5  Public/OTP and Visitor remain false
    ↓
STEP 6  Backend unit/integration tests
    ↓
STEP 7  Frontend derives internal+self capability
    ↓
STEP 8  Schema/picker accepts future <72h only for that case
    ↓
STEP 9  i18n/rulesHint correction
    ↓
STEP 10 Frontend tests
    ↓
STEP 11 Full Visit V2 regression
    ↓
STEP 12 Security/tampering regression
    ↓
STEP 13 Build + lint + test suite
    ↓
STEP 14 Manual end-to-end verification
```

---

# 13. Manual E2E checklist

## Account 1 — Visitor

- [ ] Tạo +24h -> bị chặn tại UI.
- [ ] Direct API +24h -> backend từ chối.
- [ ] +72h -> tạo được.

## Account 2 — Staff

- [ ] Bấm dùng thông tin bản thân làm registrant.
- [ ] Tạo +24h -> UI cho phép.
- [ ] API tạo thành công.
- [ ] Status/contact gate/coordinator đúng như create bình thường.
- [ ] Tạo thời gian quá khứ -> UI chặn và API cũng chặn.
- [ ] Đổi email Registrant sang người khác -> UI trở về rule 72h/OTP.

## Account 3 — Staff Leader

- [ ] Self +24h -> tạo được.
- [ ] Self + past -> không tạo được.
- [ ] Đăng ký người khác +24h -> không được short-notice.
- [ ] Host proposal/create behavior giữ nguyên.

## Multi-campus

- [ ] 3 campus đều <72h và future, Staff self -> PASS.
- [ ] 1 campus past -> toàn bộ rollback.
- [ ] 1 campus duration <30m -> toàn bộ rollback.

---

# 14. Rollback plan

Nếu regression xuất hiện sau deploy:

1. Roll back commit feature.
2. Vì không thay DB schema/constant 72h, rollback code phải đưa hệ thống về behavior cũ ngay.
3. Không cần data migration nếu patch chỉ thay authorization/validation.
4. Các request short-notice đã được tạo hợp lệ trước rollback không nên bị tự động xóa; chúng tiếp tục theo lifecycle bình thường.
5. Không áp lại 72h lên request đã tồn tại ở các mutation post-approval; giữ đúng policy 6h/other workflow hiện có.

---

# 15. Rủi ro và biện pháp kiểm soát

| Rủi ro | Root cause có thể xảy ra | Biện pháp |
|---|---|---|
| Visitor bypass 72h | Capability lấy từ payload hoặc default true | Backend-derived + default false |
| Staff tạo lịch quá khứ | Bỏ 72h nhưng không tách past guard | Absolute `PlannedStartAt > now` guard |
| Staff đăng ký thay người khác vẫn bypass | Dựa vào role người đang nhập form | Chỉ direct authenticated self-registration được capability |
| UI cho phép nhưng API từ chối | Frontend/backend rule drift | Test cùng matrix ở cả hai layer |
| API cho phép nhưng UI chặn | Chỉ sửa backend | Dynamic frontend `minAdvanceHours` |
| Hiển thị “0 giờ” | Reuse generic message | Short-notice i18n riêng |
| Pending-edit bị thay đổi | Refactor chung policy 72h | Không sửa pending-edit trong patch |
| Multi-campus partial save | Validation không nằm trước commit/transaction lỗi | Giữ transaction; integration rollback test |
| Test cũ fail hàng loạt do signature | Thêm required parameter | `allowShortNoticeCreate = false` default |
| Date picker cho phép giờ đã qua | `minAdvanceHours=0` nhưng helper precision sai | Picker/schema tests với current-day past/future |

---

# 16. Definition of Done

Feature chỉ được coi là hoàn thành khi **tất cả** điều kiện sau đúng:

- [ ] Staff self-registration tạo được request có start <72h nhưng ở tương lai.
- [ ] Staff Leader self-registration tạo được request có start <72h nhưng ở tương lai.
- [ ] Visitor vẫn bắt buộc >=72h.
- [ ] Public OTP vẫn bắt buộc >=72h.
- [ ] Staff/Leader đăng ký thay người khác vẫn bắt buộc >=72h.
- [ ] Không actor nào tạo được lịch ở quá khứ.
- [ ] End > Start vẫn bắt buộc.
- [ ] Minimum duration 30 phút vẫn bắt buộc.
- [ ] Campus/partner/member/contact validations không đổi.
- [ ] Internal registrant không tự làm operational contact như rule hiện tại.
- [ ] Contact confirmation gate không đổi.
- [ ] Host proposal authorization không đổi.
- [ ] Submission idempotency không đổi.
- [ ] Duplicate fingerprint behavior không đổi.
- [ ] Pending-edit 72h override không đổi.
- [ ] Resubmit behavior không đổi.
- [ ] Approval/reject/safe-edit/amendment/transfer-host không đổi.
- [ ] Multi-campus vẫn atomic.
- [ ] Frontend và backend trả cùng verdict cho cùng case.
- [ ] Không có client-controlled bypass flag.
- [ ] Existing regression tests xanh.
- [ ] New boundary/security tests xanh.
- [ ] Build/lint/typecheck xanh.

---

# 17. Self-review gate trước merge

Reviewer phải trả lời được bằng code/test, không bằng phỏng đoán:

1. **Ai quyết định short-notice permission?**  
   Phải là backend authenticated handler dựa trên actor DB + self-registration.

2. **Visitor có cách nào gửi flag để bypass không?**  
   Phải là không.

3. **Public OTP có vô tình được capability không?**  
   Phải là không; default false.

4. **Staff có tạo được lịch quá khứ không?**  
   Phải có test chứng minh không.

5. **Staff đăng ký người khác có bypass không?**  
   Phải có test chứng minh không.

6. **Rule 72 trong `VisitMutationPolicy` có bị sửa không?**  
   Phải là không.

7. **Pending edit override có bị đụng không?**  
   Phải là không.

8. **Multi-campus có partial persist không?**  
   Phải có rollback/atomicity test.

9. **UI và API có cùng semantics về boundary thời gian không?**  
   Phải có schema + picker + backend test.

10. **Có message “0 giờ” hoặc misleading UI không?**  
    Phải là không.

---

# 18. Kết luận triển khai

Thiết kế an toàn nhất không phải là “bỏ validate 72 giờ cho Staff”, mà là:

```text
72h vẫn là rule mặc định
        ↓
backend chứng minh actor là Staff/Staff Leader
        ↓
backend chứng minh đây là self-registration
        ↓
chỉ CREATE authenticated này nhận short-notice capability
        ↓
Create Service vẫn bảo vệ future-time + duration + toàn bộ rule khác
        ↓
Public/Visitor/delegated OTP giữ nguyên 72h
```

Cách này giới hạn thay đổi đúng phạm vi nghiệp vụ, giữ nguyên các invariants hiện hữu, giảm khả năng privilege escalation và tránh tác động dây chuyền sang pending-edit, resubmit, approval và các workflow khác của Visit V2.
