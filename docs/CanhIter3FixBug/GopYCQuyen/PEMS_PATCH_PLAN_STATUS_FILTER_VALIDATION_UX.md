# PEMS — Kế hoạch triển khai vá lỗi Status Filter & Validation UX

**Mục tiêu:** Vá triệt để các lỗi liên quan đến lọc trạng thái trong màn **Quản lý tiếp khách** và lỗi validation không chỉ đúng vị trí trên form, đồng thời rà soát các lỗi cùng họ để tránh tái phát ở các màn khác.

**Repository audit:** `quangthoai04/PEMS`  
**Nhánh được đối chiếu:** `Dev`  
**Mốc code dùng để lập kế hoạch:** code hiện thấy qua GitHub quanh commit `b98d7bc267871ffddbe0b0537e0f347a9a2c990e` và các file `Dev` hiện tại.  
**Lưu ý bắt buộc trước khi code:** xác nhận lại `Dev HEAD` và commit đang chạy trên production `pems-fpt.site`. Không triển khai nếu production đang chạy commit khác mà chưa rebase/đối chiếu diff.

---

## 1. Phạm vi lỗi cần xử lý

### P0-01 — Status filter trả về các dòng có trạng thái hiển thị khác filter

**Triệu chứng thực tế**

Ví dụ URL:

```text
/dashboard/visit?tab=all&status=APPROVED
```

Người dùng chọn **Đã duyệt**, nhưng bảng vẫn có thể xuất hiện:

- Đang chuẩn bị
- Đang diễn ra
- Chờ đóng
- Đã hoàn tất
- Đã hủy
- hoặc các trạng thái khác không khớp với filter đang chọn.

**Nguyên nhân đã xác định**

Hiện hệ thống đang có nhiều khái niệm trạng thái cùng tồn tại:

- `visit_requests.status` → `requestStatus`
- `visit_request_campuses.status` / instance status → `campusStatus`
- `VisitRowLabels.Status(...)` / `MultiCampusProgress(...)` → status backend dùng để trình bày
- `getVietnameseStatus(requestStatus, campusStatus)` → frontend tự suy diễn lại status
- cấu hình dropdown có option dùng `requestStatus`, option khác dùng `campusStatus`

Với một row instance-level, người dùng nhìn badge theo `campusStatus`, nhưng filter **Đã duyệt** ở một số role/tab lại gửi `requestStatus=APPROVED`.

Ví dụ hợp lệ trong DB:

```text
requestStatus = APPROVED
campusStatus  = BEFORE_VISIT
```

Filter hiện tại thấy `requestStatus=APPROVED` nên cho row đi qua, trong khi UI render `BEFORE_VISIT` thành **Đang chuẩn bị**.

Đây là lỗi **semantic mismatch**, không phải CSS, cache hay chỉ sai label.

---

### P0-02 — Danh sách khách rỗng được tính là lỗi nhưng không chỉ đúng chỗ cần sửa

**Triệu chứng thực tế**

Khi người dùng xóa sạch **Danh sách khách** rồi submit:

```text
Còn 1 trường cần kiểm tra.
```

nhưng phần **Danh sách khách** không hiện lỗi rõ ràng để người dùng biết phải sửa ở đâu.

**Nguyên nhân đã xác định**

Schema đúng khi yêu cầu:

```ts
visitors: z.array(...).min(1, ...)
```

nên danh sách rỗng phải sinh 1 lỗi.

`countFieldErrors()` đếm lỗi theo cây recursive nên vẫn tìm thấy lỗi list/array. Tuy nhiên `CampusVisitCard.fieldError(path)` hiện chỉ lấy `node.message` ở đúng node. Với React Hook Form + field array, lỗi `.min(1)` có thể nằm theo dạng:

```text
errors.campusVisits[i].visitors.root.message
```

Thành ra có thể xảy ra:

```text
countFieldErrors → thấy visitors.root.message → summary = 1
fieldError('visitors') → chỉ tìm visitors.message → undefined
```

Do đó summary biết có lỗi nhưng UI section không hiển thị lỗi tương ứng.

**Điểm đã có và phải giữ nguyên:** code hiện tại đã gọi `form.trigger(`${base}.${kind}`)` sau khi xóa một member nếu form đã submit. Không được bỏ logic này khi vá.

---

## 2. Các rủi ro cùng họ cần xử lý cùng đợt

### P1-01 — Backend và frontend đang có hai nguồn tính status

Backend đã có:

- `VisitRowLabels.Status(...)`
- `VisitRowLabels.MultiCampusProgress(...)`
- `item.StatusLabel`

nhưng frontend `VisitRequestManagement.tsx` vẫn tự tính:

```ts
statusText: getVietnameseStatus(item.requestStatus, item.campusStatus)
```

Nếu backend sửa lifecycle/status nhưng frontend không cập nhật cùng lúc, UI lại drift.

**Mục tiêu:** backend phải là **single source of truth** cho trạng thái của row.

---

### P1-02 — Các option `Chờ duyệt`, `Đã duyệt`, `Từ chối` có nguy cơ dùng sai tầng status

Trong `visitRequestFilterConfig.ts`, một số nhánh dùng:

```text
requestStatus
```

trong khi row được hiển thị theo campus/instance.

Cần audit toàn bộ:

- Visitor
- HO
- Staff Leader
- Regular Staff
- Department Leader / Staff
- Student
- tab `responsible`
- tab `registered`
- tab `attending`
- tab `hosted`
- tab `all`

Không sửa riêng option **Đã duyệt** rồi bỏ qua các option còn lại.

---

### P1-03 — Lỗi group/array có thể được đếm nhưng không tham gia cơ chế scroll/focus

`focusFirstInvalidField()` tìm:

```text
[data-field-error="true"]
```

và sau đó tìm `input`, `textarea`, `select` để focus.

Nhưng fieldset **Danh sách khách** không phải `FormField`, nên list-level error không chắc có `data-field-error`. Khi danh sách rỗng cũng không còn input row nào để focus.

**Mục tiêu UX:** khi `visitors` rỗng, form phải tự cuộn tới section và focus nút **Thêm khách**.

---

### P2-01 — Summary phía frontend có fallback tính từ page hiện tại

Nếu backend không trả `summary`, frontend có đoạn tự tính từ `response.items` của page hiện tại.

Điều này có thể khiến summary không đại diện cho toàn bộ tập dữ liệu đã filter.

**Mục tiêu:** summary quan trọng phải được tính server-side trên toàn bộ query trước pagination.

---

### P2-02 — Notification target ở `attending` còn giới hạn theo page

Code hiện tại tự ghi nhận limitation: API invitation chưa có filter `visitRequestId` tương đương list chính, nên notification có thể không tìm được row nếu row nằm ở page khác.

Không bắt buộc phải block hotfix P0, nhưng nên đưa vào cùng regression sweep nếu thời gian cho phép.

---

# 3. Nguyên tắc triển khai bắt buộc

## 3.1. Không thay đổi lifecycle database chỉ để sửa UI filter

**Không được:**

- đổi giá trị status trong DB để “khớp giao diện”;
- thêm migration sửa dữ liệu chỉ để filter đúng;
- sửa `visit_requests.status` thành trạng thái tiến trình campus;
- gộp request status và campus status trong database;
- đổi state machine approve / assign / before / during / after / close nếu không có requirement riêng.

Lỗi hiện tại nằm ở **cách một row được diễn giải và lọc**, không phải bằng chứng rằng lifecycle DB sai.

---

## 3.2. Không dùng role/tab làm authorization

Handler hiện tại cố tình tách:

```text
FILTER != AUTHORIZATION != ENTRY CONTEXT
```

Phải giữ nguyên invariant này.

Filter status chỉ quyết định **row nào xuất hiện**, tuyệt đối không được:

- mở thêm quyền approve;
- mở thêm quyền edit;
- mở thêm quyền view sibling campus;
- thay đổi `AllowedActions`;
- thay đổi `RelationContexts`;
- thay đổi `CanViewRequestDetail`;
- thay đổi host/contact/participant authorization.

---

## 3.3. Không filter `tab=all` trước merge bằng status không còn đúng với row cuối cùng

`QueryAllMergedAsync()` hiện lấy nhiều source:

- responsible
- attending
- registered

sau đó chọn candidate chính và merge relation thành **một row/request**.

Nếu áp status filter lên từng source **trước khi merge**, filter có thể thay đổi candidate set, từ đó làm thay đổi row thắng merge hoặc mất relation đáng lẽ phải được fold vào row.

**Quy tắc đề xuất:**

> Với `tab=all`, status filter canonical phải được áp dụng **sau bước merge + resolve effective status**, và trước bước sort + paginate cuối cùng.

Các filter không liên quan status cần đánh giá riêng; không được tiện tay di chuyển toàn bộ filter sang post-merge.

---

# 4. Thiết kế fix khuyến nghị — Single Source of Truth cho status

## 4.1. Thêm canonical status cho từng row

Không nên để frontend tự suy diễn từ hai field nữa.

Bổ sung vào DTO:

```csharp
public string? EffectiveStatusCode { get; set; }
public string? StatusLabel { get; set; }
```

`StatusLabel` đã tồn tại thì giữ nguyên và chỉ bổ sung `EffectiveStatusCode`.

**File dự kiến:**

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs
```

`EffectiveStatusCode` phải là code tương ứng chính xác với badge mà row đang hiển thị.

Ví dụ instance-level:

```text
WAITING_CONTACT_CONFIRMATION
WAITING_REQUEST_APPROVAL
ASSIGNED
BEFORE_VISIT
DURING_VISIT
AFTER_VISIT
CLOSED
CANCELLED
REJECTED
```

Không hardcode một mapping thứ hai trong handler. Nên mở rộng source hiện có:

```text
backend/PEMS.Application/Delegations/Services/VisitRowLabels.cs
```

thành một resolver trả về cả **code + label**, ví dụ về ý tưởng:

```csharp
public sealed record VisitRowStatus(string Code, string Label);

public static VisitRowStatus Resolve(
    string? requestStatus,
    string? campusStatus,
    IEnumerable<string?>? campusProgressStatuses,
    string? roleCode)
```

Tên API có thể khác, nhưng phải đạt invariant:

```text
EffectiveStatusCode <-> StatusLabel
```

là cùng một quyết định, không phải hai hàm độc lập.

---

## 4.2. Với request-level multi-campus, dùng đúng logic progress hiện có

Backend hiện đã có logic `MultiCampusProgress(...)` để tránh trường hợp:

```text
requestStatus = APPROVED
```

nhưng toàn bộ campus đã đi tới BEFORE/DURING/AFTER/CLOSED mà row vẫn đứng mãi ở “Đã duyệt”.

Không xóa logic này.

Thay vào đó, refactor để cùng resolver trả ra:

```text
EffectiveStatusCode
StatusLabel
```

Ví dụ, nếu `MultiCampusProgress` đang quyết định label là **Đang chuẩn bị**, canonical code phải là `BEFORE_VISIT`.

Nếu product hiện collapse `PARTIALLY_APPROVED` thành **Chờ duyệt**, canonical code phải thống nhất với label đó. Không được code = `PARTIALLY_APPROVED` nhưng dropdown chỉ có `PENDING_APPROVAL` trừ khi có mapping rõ ràng và được test.

**Rule quan trọng:**

> Một row được trả về bởi filter X phải render badge đúng X, trừ các filter cố ý là nhóm/union và UI phải ghi rõ đó là nhóm.

---

## 4.3. Bổ sung query parameter canonical, không phá contract cũ

Khuyến nghị thêm:

```csharp
public string? EffectiveStatus { get; init; }
```

vào:

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQuery.cs
```

Giữ tạm:

```text
RequestStatus
CampusStatus
ApprovedAny
PendingApprovalAny
CancelledOnly
```

để tránh phá các caller cũ ngay lập tức.

Frontend mới dùng `effectiveStatus`. Sau khi toàn bộ caller/test đã migrate mới cân nhắc deprecate các tham số cũ ở một task riêng.

**Không xóa contract cũ trong hotfix** nếu chưa chứng minh không còn consumer.

---

# 5. Chi tiết xử lý filter theo loại row

## 5.1. Instance-level rows

Với Staff/Staff Leader/Department/Student khi row đại diện một campus instance:

```text
EffectiveStatusCode = status thực tế của campus row
```

có xét override request-level cancellation nếu business rule hiện tại yêu cầu toàn request cancel làm mọi instance “Đã hủy”.

Phải dùng cùng resolver với badge, không tự viết WHERE mapping riêng rồi một mapping khác để render.

---

## 5.2. Request-level rows

Với Visitor/HO/registered request row:

- nếu row không có một `campusStatus` duy nhất;
- dùng aggregate + `CampusProgressItems` theo logic hiện tại;
- resolver canonical quyết định một effective status dùng cho cả filter và display.

Không mặc định `requestStatus=APPROVED` đồng nghĩa badge “Đã duyệt”.

---

## 5.3. `tab=all`

Đây là phần dễ làm hỏng nhất.

### Hiện tại

`QueryAllMergedAsync()`:

1. clone request;
2. query từng source;
3. group theo `VisitRequestId`;
4. rank candidate;
5. merge candidate phụ vào candidate chính;
6. sort;
7. paginate.

### Sau fix

Khuyến nghị:

1. nhận status filter canonical từ request;
2. clone request cho merge nhưng **xóa riêng status filter canonical khỏi source query**;
3. giữ các authorization/population predicate hiện tại;
4. fetch source;
5. group/rank/merge đúng như cũ;
6. resolve `EffectiveStatusCode` cho row cuối cùng;
7. apply `EffectiveStatusCode == requestedEffectiveStatus`;
8. sort;
9. paginate;
10. enrichment/action/entry context tiếp tục theo pipeline hiện tại.

**Tuyệt đối không:** filter candidate source bằng `APPROVED` rồi mới merge, vì có thể làm mất candidate/relation trước khi row cuối cùng được hình thành.

### CloneForMerge

Rà kỹ helper `CloneForMerge(...)`.

Nếu thêm `EffectiveStatus`, phải quyết định rõ:

```text
CloneForMerge không truyền EffectiveStatus xuống source queries
```

nhưng vẫn giữ các filter an toàn khác.

Thêm comment ngay tại code để người sau không “tối ưu” ngược lại.

---

# 6. Frontend status/filter — file và thay đổi dự kiến

## 6.1. `visitRequestFilterConfig.ts`

File:

```text
frontend/pems-react/src/features/delegations/config/visitRequestFilterConfig.ts
```

### Việc cần làm

Refactor option status để mỗi option business dùng canonical field:

```ts
{
  value: 'APPROVED',
  label: 'Đã duyệt',
  effectiveStatus: 'ASSIGNED' // ví dụ code canonical mà resolver quyết định
}
```

Tốt hơn, nếu `value` chính là canonical code thì không cần thêm một field khác:

```ts
{
  value: 'ASSIGNED',
  label: 'Đã duyệt'
}
```

Nhưng phải cân nhắc URL cũ đang dùng `status=APPROVED`.

### Khuyến nghị tương thích URL

Giữ URL business key hiện tại nếu cần:

```text
status=APPROVED
```

nhưng config map nó sang:

```text
effectiveStatus = ASSIGNED
```

Nếu đổi URL sang `status=ASSIGNED`, phải test bookmark/back-forward/deep-link và migration query param.

### Bắt buộc audit toàn bộ options

Không chỉ:

```text
Đã duyệt
```

mà cả:

```text
Chờ xác nhận
Chờ duyệt
Đã duyệt
Đang chuẩn bị
Đang diễn ra
Chờ đóng / Chờ đánh giá
Đã hoàn tất
Từ chối
Đã hủy
```

Mỗi option phải chỉ ra rõ canonical status nào.

---

## 6.2. `VisitRequestManagement.tsx`

File:

```text
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
```

### Thay đổi 1 — request params

Thay logic ưu tiên gửi:

```text
requestStatus / campusStatus / approvedAny / pendingApprovalAny
```

cho business status dropdown bằng:

```text
effectiveStatus
```

Giữ các old params chỉ cho các flow đặc biệt còn chưa migrate và phải có test.

### Thay đổi 2 — render status

Không dùng `getVietnameseStatus(...)` làm source chính nữa.

Ưu tiên:

```ts
statusText: item.statusLabel
```

`getVietnameseStatus(...)` nếu còn giữ thì chỉ là compatibility fallback tạm thời, có telemetry/dev warning và có TODO xóa.

Ví dụ:

```ts
statusText: item.statusLabel ?? getVietnameseStatus(item.requestStatus, item.campusStatus)
```

Sau khi backend contract ổn định và tests xanh, cân nhắc loại fallback trong task cleanup riêng.

### Thay đổi 3 — badge class

Nếu màu badge hiện đang suy từ `campusStatus/requestStatus`, nên chuyển sang suy từ:

```text
item.effectiveStatusCode
```

để text và màu không drift.

---

## 6.3. Types

File:

```text
frontend/pems-react/src/features/delegations/types/delegations.types.ts
```

Bổ sung:

```ts
effectiveStatusCode?: string | null;
statusLabel?: string | null;
```

Nếu `statusLabel` đã có thì chỉ thêm code.

Không làm `any` để né compiler.

---

# 7. Fix validation “Danh sách khách”

## 7.1. Không sửa schema `.min(1)`

File:

```text
frontend/pems-react/src/features/visit-request/schema/visitRequestV2.schema.ts
```

Rule hiện tại:

```ts
visitors: z.array(...).min(1, t('atLeastOneVisitor'))
```

là đúng nghiệp vụ.

**Không bỏ `.min(1)` để làm banner hết lỗi.**

---

## 7.2. Tách rõ field error và array-root error

File phù hợp để đặt helper chung:

```text
frontend/pems-react/src/features/visit-request/utils/formErrorNavigation.ts
```

hoặc tạo:

```text
frontend/pems-react/src/features/visit-request/utils/formErrorAccess.ts
```

Khuyến nghị không làm recursive “lấy message đầu tiên” cho group, vì dễ duplicate child error.

Nên có API rõ:

```ts
getErrorAtPath(errors, path)
getDirectErrorMessage(node)
getArrayRootErrorMessage(node)
getGroupErrorMessage(errors, path)
```

Logic `getGroupErrorMessage`:

1. tìm node theo path;
2. nếu `node.message` có string → return;
3. nếu `node.root?.message` có string → return;
4. không tự đào xuống từng row để lấy child message;
5. child errors tiếp tục render ngay tại cell tương ứng.

Như vậy:

```text
visitors.root.message
```

được hiện ở section header nhưng:

```text
visitors[2].nationality.message
```

vẫn chỉ hiện ở row 3 / cột Quốc tịch, không bị lặp lên đầu danh sách.

---

## 7.3. `CampusVisitCard.tsx`

File:

```text
frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx
```

### Việc cần làm

Tại section visitors:

- lấy `visitorListError = getGroupErrorMessage(..., `${base}.visitors`)`;
- fieldset có `data-field-error="true"` khi có lỗi list-level;
- có `aria-invalid`/`aria-describedby` phù hợp;
- render message ngay dưới legend hoặc trước table;
- border/section indicator đỏ vừa đủ, không tô đỏ toàn modal;
- nút **Thêm khách** có `ref` hoặc `data-error-focus-target` để focus khi list rỗng.

Ví dụ hành vi mong muốn:

```text
Danh sách khách *  (0/200)
Cần có ít nhất 1 khách.

[ + Thêm khách ]
```

### Khi xóa row cuối cùng

Logic hiện có:

```ts
if (form.formState.isSubmitted)
  void form.trigger(`${base}.${kind}`);
```

**Giữ nguyên.**

Bổ sung test để đảm bảo:

```text
1 row valid
→ submit thành công validation client
→ xóa row
→ error list xuất hiện ngay
→ summary tăng đúng
```

---

## 7.4. `focusFirstInvalidField()` phải hỗ trợ group/list

File:

```text
frontend/pems-react/src/features/visit-request/utils/formErrorNavigation.ts
```

Hiện hàm chỉ tìm input/textarea/select.

Mở rộng an toàn theo thứ tự:

1. tìm container `[data-field-error="true"]` đầu tiên;
2. tìm control chuẩn `input/textarea/select`;
3. nếu không có, tìm `[data-error-focus-target="true"]` hoặc button được đánh dấu riêng;
4. focus + `scrollIntoView`.

Không đưa mọi button vào selector chung vì có thể focus nhầm nút Xóa/Import/Download.

Chỉ focus button được opt-in bằng attribute riêng.

---

## 7.5. `FormField.tsx`

File:

```text
frontend/pems-react/src/features/visit-request/components/shared/FormField.tsx
```

Không cần rewrite component.

Chỉ đảm bảo helper mới không phá contract hiện tại:

```text
data-field-error
aria-invalid
aria-describedby
role="alert"
```

Group/list component phải tuân theo cùng contract.

---

# 8. Audit các form động tương tự

Sau khi fix helper, grep toàn frontend các pattern:

```text
useFieldArray(
.array(
.min(1
countFieldErrors(
data-field-error
formState.errors
setError(
clearErrors(
form.trigger(
```

Ưu tiên kiểm tra:

- visitor list
- supportTeam list
- campusVisits array
- amendment visitors/support lists
- participant invitations
- logistics assignments
- meeting minutes action items
- partner contacts nếu có dynamic rows
- email recipients/attachments nếu dùng form-array pattern

## Rule audit

Với mỗi list required phải trả lời đủ 5 câu:

1. List rỗng có sinh error đúng không?
2. Summary có đếm đúng không?
3. Section có hiện message không?
4. Sau add/remove, error có revalidate ngay không?
5. Auto-scroll/focus có đưa người dùng đến action sửa được lỗi không?

Không đánh dấu “đã audit” nếu mới test submit một lần mà chưa test structural changes add/remove/replace/import.

---

# 9. Audit status filter toàn hệ thống

Tạo ma trận role × tab × status.

## 9.1. Staff Leader

Test ít nhất:

```text
WAITING_REQUEST_APPROVAL → Chờ duyệt
ASSIGNED                 → Đã duyệt
BEFORE_VISIT             → Đang chuẩn bị
DURING_VISIT             → Đang diễn ra
AFTER_VISIT              → Chờ đóng
CLOSED                   → Đã hoàn tất
REJECTED                 → Từ chối
CANCELLED                → Đã hủy
```

Mỗi filter phải chỉ trả về row có canonical badge tương ứng.

---

## 9.2. Regular Staff

Đặc biệt test `tab=all`, vì đây là case ảnh thực tế.

Các scenario bắt buộc:

### False positive hiện tại

```text
requestStatus = APPROVED
campusStatus  = BEFORE_VISIT
filter        = Đã duyệt
EXPECTED      = KHÔNG xuất hiện
```

### False negative hiện tại

```text
requestStatus = PARTIALLY_APPROVED
campusStatus  = ASSIGNED
filter        = Đã duyệt
EXPECTED      = xuất hiện nếu row cuối cùng render badge Đã duyệt
```

### Cancelled

```text
requestStatus = APPROVED
campusStatus  = CANCELLED
filter Đã duyệt → KHÔNG xuất hiện
filter Đã hủy → xuất hiện
```

---

## 9.3. Visitor

Visitor có wording riêng:

```text
AFTER_VISIT → Chờ đánh giá
```

Backend có thể vẫn dùng canonical code `AFTER_VISIT`, còn `StatusLabel` phụ thuộc audience/role.

Test:

```text
EffectiveStatusCode = AFTER_VISIT
Visitor label        = Chờ đánh giá
Staff label          = Chờ đóng
```

Không tạo hai status code chỉ vì wording khác nhau.

---

## 9.4. HO

HO request-level/multi-campus phải test:

- all pending;
- partially approved;
- fully approved nhưng một campus BEFORE_VISIT;
- campus mix BEFORE/DURING/AFTER/CLOSED;
- cancel/reject combinations hợp lệ theo business rules.

Nếu UI muốn một filter nhóm kiểu **Chờ duyệt** gồm nhiều internal states, phải quy định rõ canonical code của row cuối cùng hoặc đổi UI thành filter group explicit. Không để một row có badge **Đã duyệt** nhưng đồng thời lọt filter **Chờ duyệt** chỉ vì một sibling campus.

---

# 10. Tests cần thêm / cập nhật

## 10.1. Backend unit tests

File hiện có:

```text
tests/PEMS.UnitTests/Delegations/VisitRowLabelsTests.cs
```

Thêm test cho resolver mới:

- mỗi campus status → đúng `EffectiveStatusCode` + label;
- Visitor `AFTER_VISIT` label khác Staff nhưng code giống nhau;
- request CANCELLED override đúng theo rule hiện hành;
- multi-campus progress mapping code/label không drift;
- PARTIALLY_APPROVED mapping được pin rõ bằng test.

---

## 10.2. Backend list/query tests

Files tham chiếu hiện có:

```text
tests/PEMS.UnitTests/Delegations/ViewGuestDelegationList/ViewGuestDelegationListQueryHandlerTests.cs

tests/PEMS.IntegrationTests/VisitRequests/V2ListNextTaskAndTransferTests.cs

tests/PEMS.IntegrationTests/VisitRequests/RelationFilterEntryContextTests.cs

tests/PEMS.IntegrationTests/VisitRequests/MergeCrossBranchContractTests.cs

tests/PEMS.IntegrationTests/VisitRequests/V2MixedListSurfacesTests.cs
```

### Test bắt buộc cho P0-01

```text
AllTab_ApprovedFilter_DoesNotReturnBeforeVisitRow
AllTab_ApprovedFilter_ReturnsAssignedRowWhenRequestIsPartiallyApproved
AllTab_CancelledRow_AppearsOnlyInCancelledFilter
AllTab_StatusFilter_DoesNotChangeRelationUnion
AllTab_StatusFilter_DoesNotChangeAllowedActions
AllTab_StatusFilter_PaginatesAfterEffectiveStatusFiltering
```

### Security regression

Với mỗi test filter mới, assert thêm:

- không lộ sibling campus;
- không tăng `AllowedActions`;
- không đổi `CanViewRequestDetail`;
- không biến participant thành registrant/host;
- không đổi `PrimaryEntryContext` ngoài row selection hợp lệ.

---

## 10.3. Frontend filter tests

Các suite hiện có để reuse pattern:

```text
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementCampusFilterAuthorization.test.tsx
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementEntryContext.test.tsx
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementUrlStateSync.test.tsx
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementActions.test.tsx
```

Tạo mới hợp lý:

```text
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementStatusFilter.test.tsx
```

Test:

- chọn **Đã duyệt** → API nhận `effectiveStatus` đúng;
- URL/state sync vẫn đúng;
- Reset xóa status;
- Back/Forward restore đúng filter;
- row render `statusLabel` backend thay vì tự suy diễn;
- màu badge dựa canonical code;
- không gửi đồng thời `requestStatus=APPROVED` cũ trừ compatibility case được chỉ định.

---

## 10.4. Frontend validation tests

Tìm/reuse suite `CampusVisitCard` / V2 form hiện có; nếu chưa có case rõ thì thêm:

```text
CampusVisitCardVisitorListValidation.test.tsx
```

### Scenario A — empty from submit

```text
Given visitors = []
When submit
Then validationErrorCount = 1 cho lỗi list
And hiện "Cần có ít nhất 1 khách"
And section visitors có data-field-error=true
And nút Thêm khách nhận focus
```

### Scenario B — delete last row after submit

```text
Given 1 visitor valid
And form đã submit một lần
When xóa row cuối
Then form.trigger(visitors) chạy
And lỗi list xuất hiện không cần submit lại
```

### Scenario C — add visitor clears root error

```text
Given visitors root error
When thêm row và điền đủ 4 required fields
Then root error biến mất
And summary giảm đúng
```

### Scenario D — child error không bị duplicate thành root error

```text
Given visitor[0].nationality invalid
Then lỗi chỉ hiện tại cột Quốc tịch
And không hiển thị cùng message như list-level error
```

### Scenario E — multi-campus

```text
Campus 1 visitor valid
Campus 2 visitors empty
Submit
→ mở/focus đúng Campus 2
→ không focus nhầm Campus 1
```

---

# 11. Regression matrix bắt buộc trước merge

## 11.1. Visit list

| Case | Expected |
|---|---|
| Filter Đã duyệt + ASSIGNED | Có row |
| Filter Đã duyệt + BEFORE_VISIT | Không có row |
| Filter Đang chuẩn bị + BEFORE_VISIT | Có row |
| Filter Đã hủy + CANCELLED | Có row |
| Filter Đã duyệt + CANCELLED | Không có row |
| PARTIALLY_APPROVED request + ASSIGNED instance | Match theo badge final row |
| Multi-campus summary | Filter khớp badge summary |
| Search + status | AND đúng |
| Campus + status | AND đúng |
| Date + status | AND đúng |
| Sort asc/desc + status | đúng |
| Pagination + status | total/page đúng sau filter |
| Reset | trở về all status |
| URL Back/Forward | restore đúng |
| notification visitRequestId | vẫn resolve đúng |

---

## 11.2. Roles

Phải smoke ít nhất:

- Visitor
- HO
- Staff Leader
- Regular Staff
- Department Leader
- Department Staff
- Student

Admin nếu không tham gia visit list thì assert behavior không đổi.

---

## 11.3. Visit lifecycle/actions

Đặc biệt verify không regression:

- Staff Leader approve + assign host;
- Host start preparation;
- BEFORE → DURING;
- DURING → AFTER;
- close visit;
- cancel;
- reject;
- resubmit;
- transfer host;
- participant invitation;
- notification deep link;
- feedback entry.

Filter patch không được thay đổi capability/action của bất kỳ lifecycle step nào.

---

# 12. Build/test gate trước khi cho merge

Không merge nếu một gate dưới đây đỏ.

## Backend

```bash
dotnet build
```

Sau đó chạy ít nhất:

```bash
dotnet test tests/PEMS.UnitTests

dotnet test tests/PEMS.IntegrationTests
```

Nếu repo có architecture suite riêng:

```bash
dotnet test tests/PEMS.ArchitectureTests
```

Tên project/csproj thực tế phải lấy từ solution hiện tại; không đoán path nếu repo đã đổi.

## Frontend

Trong:

```text
frontend/pems-react
```

chạy các script hiện có trong `package.json`, tối thiểu tương đương:

```bash
npm run typecheck
npm run test -- --run
npm run build
```

Nếu tên script khác thì dùng đúng script repo hiện hành.

## Real-stack/E2E

Nếu môi trường hiện tại có thể chạy real-stack, ưu tiên ít nhất một case:

```text
Login Regular Staff
→ Quản lý tiếp khách
→ tab Tất cả
→ filter Đã duyệt
→ assert không có badge Đang chuẩn bị/Đã hủy/Chờ đóng trong result
```

và một case form:

```text
Mở Đăng ký tham quan trường
→ tạo một guest
→ xóa guest cuối
→ submit
→ thấy lỗi ngay tại Danh sách khách
→ focus/scroll đúng chỗ
```

---

# 13. Quy trình triển khai an toàn

## Phase 0 — Freeze evidence

Trước khi code:

1. ghi `git rev-parse HEAD` của branch fix;
2. xác định commit production hiện tại;
3. lưu screenshot/video bug;
4. lưu Network request khi filter Đã duyệt;
5. xác nhận request hiện gửi `requestStatus` hay `campusStatus` gì;
6. lưu API response có cả `requestStatus`, `campusStatus`, `statusLabel` nếu có;
7. reproduce validation list rỗng trong local/production-like environment.

Không dựa duy nhất vào screenshot.

---

## Phase 1 — Viết regression tests đỏ trước

Tạo tests reproduce chính xác:

- P0-01 false-positive;
- P0-01 false-negative;
- P0-02 array-root error visibility;
- P0-02 focus target.

Commit tests riêng nếu team workflow cho phép.

Mục đích: chứng minh fix sửa đúng lỗi, không phải thay đổi code rồi test tự khớp code mới.

---

## Phase 2 — Backend canonical status

Thứ tự:

1. mở rộng `VisitRowLabels` / tạo resolver canonical;
2. thêm `EffectiveStatusCode` vào DTO;
3. set code + label cùng một chỗ trong handler;
4. thêm `EffectiveStatus` query param;
5. implement filter cho non-`all`;
6. implement post-merge filter cho `tab=all`;
7. giữ authorization/actions nguyên trạng;
8. chạy backend unit/integration.

Không chạm frontend trước khi API contract đã có tests.

---

## Phase 3 — Frontend status migration

1. update types;
2. update filter config;
3. gửi `effectiveStatus`;
4. render backend `statusLabel`;
5. badge class dùng `effectiveStatusCode`;
6. giữ fallback tạm nếu cần compatibility;
7. chạy frontend tests/typecheck/build.

---

## Phase 4 — Validation UX

1. thêm helper group/array error;
2. sửa visitors section;
3. bổ sung `data-field-error`;
4. bổ sung error focus target;
5. mở rộng `focusFirstInvalidField` an toàn;
6. giữ trigger sau add/remove structural change;
7. test multi-campus + list empty + child field error.

---

## Phase 5 — Similar-bug sweep

Không sửa hàng loạt vô điều kiện.

Với mỗi hit grep:

1. reproduce hoặc đọc contract;
2. phân loại real bug / safe existing behavior;
3. chỉ sửa khi có test;
4. không refactor unrelated UI trong cùng PR.

Nếu phát hiện bug mới lớn, tách ticket/PR sau P0 thay vì kéo hotfix thành mega-refactor.

---

## Phase 6 — Full regression + review

Reviewer phải kiểm tra riêng:

- status semantics;
- all-tab merge order;
- pagination position;
- authorization unchanged;
- validation root error;
- accessibility/focus;
- old URL/deep-link compatibility.

Không chỉ review diff UI.

---

# 14. Deployment strategy

## 14.1. Không cần migration DB cho fix chính

Thiết kế đề xuất chỉ thay:

- DTO;
- query contract;
- query/filter logic;
- frontend mapping/render;
- validation UX.

Do đó **không nên có DB migration** cho P0-01/P0-02.

Nếu implementation phát sinh yêu cầu migration, dừng lại và review lại nguyên nhân vì đó là dấu hiệu scope đang bị mở rộng.

---

## 14.2. Deploy backend trước hoặc atomic deploy

Vì frontend mới cần `effectiveStatusCode/effectiveStatus`, hai lựa chọn an toàn:

### Option A — Atomic deploy

Deploy backend + frontend cùng release.

### Option B — Backward-compatible two-step

1. backend deploy trước, thêm field/param mới nhưng vẫn hỗ trợ contract cũ;
2. verify backend;
3. frontend deploy sau chuyển sang contract mới;
4. giữ backward compatibility một release;
5. cleanup old params sau.

**Khuyến nghị:** Option B nếu pipeline deploy frontend/backend độc lập.

---

# 15. Post-deploy smoke test

Ngay sau deploy, kiểm tra bằng dữ liệu thực nhưng không mutate nguy hiểm.

## Status filter

Với Regular Staff:

1. vào `/dashboard/visit`;
2. tab `all`;
3. chọn **Đã duyệt**;
4. kiểm tra tất cả badge trong page đều đúng semantic filter;
5. chuyển **Đang chuẩn bị**;
6. chuyển **Đã hủy**;
7. Reset;
8. Back/Forward;
9. search + status;
10. date + status.

Với Staff Leader và HO lặp lại các status quan trọng.

## Validation

1. mở form tạo chuyến thăm;
2. điền dữ liệu đến section guest;
3. thêm guest valid;
4. xóa guest cuối;
5. submit;
6. assert summary = 1 nếu chỉ còn lỗi này;
7. assert section Danh sách khách đỏ;
8. assert message cụ thể;
9. assert viewport tự đưa đến section/nút Thêm khách;
10. thêm lại guest valid → lỗi biến mất.

---

# 16. Monitoring sau deploy

Trong 24–48 giờ đầu:

Theo dõi:

- API 400/500 của `ViewGuestDelegationList`;
- query latency của tab `all` vì status filter chuyển post-merge;
- số row/total bất thường;
- lỗi JS ở `VisitRequestManagement`;
- lỗi form submit V2;
- support report “lọc không đúng”;
- support report “có lỗi nhưng không biết sửa đâu”.

Nếu `tab=all` có dataset gần `MergeFetchCap=1000`, cần ghi nhận performance/correctness riêng. Không tự tăng cap trong hotfix nếu chưa benchmark.

---

# 17. Rollback plan

## Backend rollback

Vì không migration DB:

- rollback artifact/backend image về release trước;
- không cần rollback schema DB;
- frontend mới nếu đã deploy phải vẫn có fallback hoặc rollback cùng lúc.

## Frontend rollback

Rollback bundle trước đó.

Nếu dùng two-step deploy, backend mới vẫn hỗ trợ old params nên frontend rollback không gây downtime.

## Điều kiện rollback ngay

Rollback nếu xuất hiện một trong các dấu hiệu:

- Staff Leader mất row chờ duyệt;
- Host mất row đang phụ trách;
- row hiển thị nhưng action quyền sai;
- request multi-campus lộ sibling campus không thuộc scope;
- total/pagination lệch nghiêm trọng;
- API list tăng 5xx;
- filter `all` trả rỗng bất thường;
- create visit bị block do validation helper mới đọc nhầm child error thành group error.

---

# 18. Definition of Done

Không đóng bug cho đến khi đủ toàn bộ:

### P0-01

- [ ] Filter **Đã duyệt** không còn trả row badge **Đang chuẩn bị / Chờ đóng / Đã hủy** ngoài semantics được định nghĩa.
- [ ] `PARTIALLY_APPROVED + ASSIGNED` không bị false-negative nếu final row badge là **Đã duyệt**.
- [ ] Filter và badge dùng cùng canonical status.
- [ ] `tab=all` filter sau merge.
- [ ] Pagination/total đúng sau filter.
- [ ] Authorization/action/relation không đổi.
- [ ] Visitor/HO/Staff Leader/Staff smoke xanh.

### P0-02

- [ ] `visitors=[]` sinh đúng 1 list-level error.
- [ ] Summary đếm đúng.
- [ ] Section Danh sách khách hiện message cụ thể.
- [ ] Error không bị duplicate từ child field.
- [ ] Auto-scroll đúng section.
- [ ] Focus nút Thêm khách khi list rỗng.
- [ ] Add/remove sau submit revalidate ngay.
- [ ] Multi-campus focus đúng campus lỗi.

### Quality gate

- [ ] Backend build xanh.
- [ ] Backend unit xanh.
- [ ] Backend integration xanh.
- [ ] Architecture tests xanh.
- [ ] Frontend typecheck xanh.
- [ ] Frontend unit/Vitest xanh.
- [ ] Frontend build xanh.
- [ ] Smoke production xanh.
- [ ] Không có DB migration ngoài scope.

---

# 19. Danh sách file trọng tâm cần review

## Backend

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQuery.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListDto.cs
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/ViewGuestDelegationListQueryHandler.cs
backend/PEMS.Application/Delegations/Services/VisitRowLabels.cs
```

Tests:

```text
tests/PEMS.UnitTests/Delegations/VisitRowLabelsTests.cs
tests/PEMS.UnitTests/Delegations/ViewGuestDelegationList/ViewGuestDelegationListQueryHandlerTests.cs
tests/PEMS.IntegrationTests/VisitRequests/V2ListNextTaskAndTransferTests.cs
tests/PEMS.IntegrationTests/VisitRequests/RelationFilterEntryContextTests.cs
tests/PEMS.IntegrationTests/VisitRequests/MergeCrossBranchContractTests.cs
tests/PEMS.IntegrationTests/VisitRequests/V2MixedListSurfacesTests.cs
```

## Frontend — list/status

```text
frontend/pems-react/src/features/delegations/config/visitRequestFilterConfig.ts
frontend/pems-react/src/features/delegations/types/delegations.types.ts
frontend/pems-react/src/pages/dashboard/visit/VisitRequestManagement.tsx
```

Tests:

```text
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementCampusFilterAuthorization.test.tsx
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementEntryContext.test.tsx
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementUrlStateSync.test.tsx
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementActions.test.tsx
```

Nên thêm:

```text
frontend/pems-react/src/pages/dashboard/visit/__tests__/VisitRequestManagementStatusFilter.test.tsx
```

## Frontend — validation

```text
frontend/pems-react/src/features/visit-request/schema/visitRequestV2.schema.ts
frontend/pems-react/src/features/visit-request/hooks/useVisitRequestFormV2.ts
frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx
frontend/pems-react/src/features/visit-request/components/shared/FormField.tsx
frontend/pems-react/src/features/visit-request/utils/formErrorNavigation.ts
```

Nên thêm test riêng cho visitor-list root error nếu suite hiện tại chưa có coverage rõ.

---

# 20. Checklist cho người triển khai code

Trước khi sửa:

- [ ] `git pull/rebase` đúng `Dev HEAD`.
- [ ] ghi commit production.
- [ ] reproduce 2 P0.
- [ ] chụp Network payload/response.
- [ ] test đỏ trước.

Khi sửa P0-01:

- [ ] không đổi DB lifecycle.
- [ ] không đổi authorization.
- [ ] canonical status resolver ở backend.
- [ ] DTO có code + label cùng nguồn.
- [ ] `tab=all` status filter sau merge.
- [ ] frontend dùng backend status.
- [ ] tất cả status options được audit.

Khi sửa P0-02:

- [ ] không bỏ `.min(1)`.
- [ ] đọc được `root.message`.
- [ ] không recurse lấy child message làm group message.
- [ ] section có `data-field-error`.
- [ ] nút Add là explicit focus target.
- [ ] giữ `form.trigger` sau remove.

Trước merge:

- [ ] full tests.
- [ ] review security regression.
- [ ] review pagination.
- [ ] review old URL compatibility.
- [ ] review production deploy order.

---

# 21. Kết luận kỹ thuật

Hai lỗi người dùng phát hiện không nên được vá bằng hai câu `if` riêng lẻ.

**Lỗi filter** xuất phát từ việc cùng một row có nhiều cách hiểu status ở request, campus, backend và frontend. Fix triệt để là tạo **canonical effective status ở backend**, rồi dùng cùng quyết định đó cho cả filter và badge. Đặc biệt với `tab=all`, phải filter trên **row sau merge**, nếu không có thể làm thay đổi relation/candidate trước khi row cuối cùng được hình thành.

**Lỗi Danh sách khách** xuất phát từ việc cơ chế đếm lỗi hiểu được cây lỗi nested/array nhưng component hiển thị lỗi lại chỉ đọc direct `.message`. Fix đúng là chuẩn hóa accessor cho **group/array root error**, giữ child error độc lập, và bắt mọi section có lỗi tham gia chung cơ chế `data-field-error + scroll + focus`.

Nếu triển khai theo kế hoạch này, phạm vi patch vẫn tập trung vào presentation/query contract và validation UX, **không cần thay đổi schema DB, không thay đổi lifecycle, không thay đổi authorization**, nhờ đó giảm đáng kể nguy cơ sửa một lỗi rồi làm hỏng luồng duyệt/Host/multi-campus/notification ở phần khác.
