# PEMS — Visit Edit / Resubmit / Contact Identity / Campus Limit / 72H Validation Fix Plan

## 1. Mục tiêu

Triển khai đồng bộ các lỗi và điểm chưa hợp lý trong luồng **Visitor chỉnh sửa đơn / gửi lại đơn / thay đổi đầu mối / thêm cơ sở** trên PEMS.

Mục tiêu cuối cùng:

- Không hardcode giới hạn `10` cơ sở trong Edit.
- Số cơ sở tối đa phải theo **tổng số campus đang ACTIVE**, giống Create.
- Edit và Resubmit chỉ hiện **1 success toast**.
- Không cho sửa email đầu mối như một field text bình thường trong Edit.
- Tách rõ:
  - **Sửa nội dung đơn**
  - **Thay đổi đầu mối liên hệ**
- Khi đổi đầu mối phải dùng đúng confirmation/transfer flow hiện có và gửi email xác nhận.
- Create / Edit / Resubmit phải dùng cùng rule thời gian tối thiểu **72 giờ kể từ thời điểm thao tác**.
- Khi đơn không còn hợp lệ do thời gian, Visitor phải được thông báo để vào sửa/gửi lại.
- Sửa lỗi UI row campus mới hiển thị tên campus nhưng form value thực tế chưa có.
- Không thay đổi architecture/API/schema ngoài phần thật sự cần thiết.

---

# 2. Nguyên tắc triển khai

## 2.1. Không phá logic identity hiện có

Không bỏ guard kiểu:

```text
IMMUTABLE_CONTACT_IDENTITY
```

hoặc logic tương đương đang bảo vệ email/account/relation của đầu mối.

Guard này phải được giữ ở backend.

Vấn đề cần sửa là:

- UI đang cho người dùng tưởng rằng email đầu mối có thể sửa trực tiếp.
- Luồng thay đầu mối chưa được tách rõ khỏi normal edit.

---

## 2.2. Reuse logic hiện có

Ưu tiên tái sử dụng:

- Campus ACTIVE source đang dùng ở Create.
- Validation date/time đang dùng cho Visit V2.
- Existing operational-contact / identity claim / transfer confirmation flow.
- Existing email dispatcher/template infrastructure.
- Existing notification infrastructure.
- Existing success-toast/navigation pattern.

Không tạo abstraction hoặc bảng mới nếu chưa có lý do bắt buộc.

---

## 2.3. Backend là source of truth

Frontend có thể validate sớm để UX tốt hơn, nhưng backend phải enforce cuối cùng:

```text
plannedStart >= now + 72 hours
```

cho tất cả action có thể tạo lại trạng thái cần Staff Leader xét duyệt.

---

# 3. Phạm vi thay đổi

## 3.1. Frontend

Dự kiến liên quan các khu vực/file sau:

```text
frontend/pems-react/src/pages/dashboard/visit/EditVisitRequestV2Page.tsx
frontend/pems-react/src/features/visit-request/components/CampusVisitCard.tsx
frontend/pems-react/src/features/visit-request/schema/visitRequestV2.schema.ts
frontend/pems-react/src/features/visit-request/...
```

Ngoài ra cần tìm chính xác:

- Create Visit V2 page/form.
- Resubmit Visit V2 page/modal/action.
- Detail page nhận `location.state.toast`.
- Campus selector/source.
- Contact identity UI/component.
- API clients cho:
  - edit
  - resubmit
  - identity change / transfer
  - active campus list

Không đổi file nếu không cần.

---

## 3.2. Backend

Tìm và kiểm tra các handler/service tương ứng:

```text
VisitRequestV2EditService
Pending edit handler/service
Resubmit handler/service
Create Visit V2 handler/service
Operational contact confirmation/transfer handlers
Visit reminder / expiry hosted service
Email notification service
```

Tên file thực tế phải lấy theo codebase hiện tại trên `Dev`.

---

# 4. Phase A — Sửa giới hạn Campus trong Edit

## 4.1. Vấn đề hiện tại

Edit đang có logic tương đương:

```tsx
if (campuses.length >= 10) {
  return;
}
```

và:

```tsx
disabled={isSubmitting || campuses.length >= 10}
```

UI:

```text
Thêm cơ sở (2/10)
```

Đây là hardcode và không đồng bộ với Create.

---

## 4.2. Logic đúng

Edit phải lấy cùng nguồn campus đang ACTIVE như Create.

Ví dụ:

```text
activeCampusCount = số campus có status ACTIVE
selectedCampusCount = số campus hiện có trong form
```

Hiển thị:

```text
Thêm cơ sở ({selectedCampusCount}/{activeCampusCount})
```

Button disabled khi:

```text
selectedCampusCount >= activeCampusCount
```

---

## 4.3. Campus khả dụng để chọn

Dropdown chỉ cho phép:

```text
ACTIVE campus
AND chưa được chọn trong campus row khác
```

Không cho duplicate campus trong cùng request.

Nếu một campus cũ của request đã bị deactivate sau khi request được tạo:

- Không tự động xóa khỏi request.
- Vẫn hiển thị campus hiện có để không làm mất dữ liệu.
- Nhưng không cho chọn campus INACTIVE mới từ dropdown.
- Nếu nghiệp vụ yêu cầu đổi khỏi campus INACTIVE thì xử lý trong validation/action cụ thể, không tự suy diễn.

---

## 4.4. Không hardcode magic number

Xóa mọi logic kiểu:

```text
10
MAX_CAMPUS = 10
```

nếu nó chỉ tồn tại để giới hạn số campus theo UI.

Chỉ giữ nếu `10` là business rule độc lập đã được backend/canonical rule xác nhận.

---

# 5. Phase B — Sửa lỗi campus row mới hiển thị sai trạng thái

## 5.1. Hiện tượng

Row campus mới có thể hiển thị:

```text
FPT University TP.HCM
```

nhưng validation lại báo:

```text
Vui lòng chọn cơ sở
```

Điều này cho thấy:

```text
display label != actual form campusId
```

---

## 5.2. Logic đúng khi Add Campus

Khi thêm row mới:

```ts
campusId = null
```

UI:

```text
Chọn cơ sở...
```

Không fallback hiển thị campus đầu tiên.

Không derive label từ array index khi `campusId` chưa tồn tại.

---

## 5.3. Validation

Khi save:

```text
campusId == null
→ show "Vui lòng chọn cơ sở"
```

Khi người dùng chọn campus:

```text
set campusId thực
→ clear validation error
```

---

# 6. Phase C — Fix duplicate toast khi Edit / Resubmit

## 6.1. Vấn đề

Sau khi Edit thành công đang thấy 2 toast giống nhau.

Resubmit cũng bị 2 toast.

Khả năng cần audit:

```text
mutation success toast
+
navigate(state.toast)
+
detail page consume state.toast
```

hoặc cùng `location.state.toast` bị consume ở 2 nơi.

---

## 6.2. Chọn một owner duy nhất

Khuyến nghị giữ pattern:

```text
Edit/Resubmit page
    ↓
navigate(detailRoute, {
  state: {
    toast: ...
  }
})
    ↓
Detail page consume đúng 1 lần
    ↓
clear/replace history state
```

Không gọi thêm:

```ts
toast.success(...)
```

trước khi navigate nếu detail page đã render toast.

---

## 6.3. Clear state sau khi consume

Detail page phải xử lý:

```text
if location.state.toast exists
→ show once
→ replace current history state without toast
```

Mục tiêu:

- Refresh page không show lại.
- Back/forward không show lại.
- React StrictMode không double render toast.

---

## 6.4. Edit và Resubmit phải dùng cùng pattern

Không fix riêng một màn.

Audit toàn bộ:

```text
Edit success
Resubmit success
Pending edit success
Any save-and-return-to-detail flow
```

Nếu cùng helper/navigation wrapper thì sửa tại source chung.

---

# 7. Phase D — Tách “Sửa đơn” và “Thay đổi đầu mối”

## 7.1. Normal Edit

Trong luồng Edit Visit Request thông thường:

Cho sửa các field không thay đổi danh tính đầu mối, ví dụ:

```text
Họ và tên
Đơn vị công tác
Chức vụ
Số điện thoại
```

Email đầu mối:

```text
READ ONLY
```

Không hiển thị như textbox editable.

---

## 7.2. UI đề xuất

Tại phần:

```text
Đầu mối của đoàn
```

hiển thị:

```text
Họ và tên
Đơn vị
Chức vụ
Số điện thoại
Email: readonly

[Thay đổi đầu mối]
```

Có thể thêm helper ngắn:

```text
Email đầu mối cần xác nhận riêng khi thay đổi.
```

Không cần giải thích dài.

---

## 7.3. Backend normal edit

Giữ validation hiện có:

```text
contact email unchanged
contact identity relation unchanged
account relation unchanged
```

Nếu client cố tình gửi email khác bằng API:

```text
→ IMMUTABLE_CONTACT_IDENTITY
```

hoặc error code canonical hiện có.

Không bỏ bảo vệ này.

---

# 8. Phase E — Luồng “Thay đổi đầu mối”

## 8.1. Không update trực tiếp

Không làm kiểu:

```http
PATCH /visit-request/{id}
{
  "contactEmail": "new@email.com"
}
```

và đổi relation ngay.

---

## 8.2. Dùng existing identity/transfer flow

Flow mong muốn:

```text
Visitor bấm "Thay đổi đầu mối"
        ↓
Nhập thông tin đầu mối mới
        ↓
Backend tạo pending contact identity/transfer
        ↓
Gửi email xác nhận cho đầu mối mới
        ↓
Đầu mối mới accept
        ↓
Backend mới chuyển relation/ownership
```

Trong thời gian pending:

```text
đầu mối cũ vẫn là đầu mối hiệu lực
```

---

## 8.3. Nếu đầu mối mới là chính registrant

Nếu email mới trùng registrant/account đã xác thực:

- Dùng canonical self-match rule hiện có.
- Không tạo duplicate account.
- Không bypass authorization.
- Confirmation source phải theo business rule hiện tại.

---

## 8.4. Nếu đầu mối mới từ chối / hết hạn

Nếu:

```text
DECLINED
EXPIRED
CANCELLED
```

thì:

```text
đầu mối cũ vẫn giữ nguyên
```

Không làm request mất operational contact.

---

## 8.5. UI trạng thái pending

Khi đang chờ xác nhận:

```text
Đang chờ đầu mối mới xác nhận
```

Có thể cho:

```text
Gửi lại email
Hủy yêu cầu thay đổi
```

chỉ khi backend hiện đã hỗ trợ.

Không tự thêm endpoint mới nếu flow hiện có đã có resend/cancel.

---

# 9. Phase F — Email xác nhận khi đổi đầu mối

## 9.1. Bắt buộc gửi email

Khi tạo yêu cầu thay đổi đầu mối:

```text
new contact
→ confirmation email
```

Email phải dùng email infrastructure hiện tại:

```text
ISystemEmailDispatcher
email_templates
sensitive policy hiện có
```

Không gửi SMTP trực tiếp.

---

## 9.2. Nội dung tối thiểu

Email cần có:

- Request code.
- Campus liên quan nếu contact là per-campus.
- Người yêu cầu thay đổi.
- Thông tin đầu mối được mời.
- Expiry time.
- CTA Accept / Decline.
- Security text theo template policy hiện tại.

---

## 9.3. Không gửi confirmation sai đối tượng

Kiểm tra:

```text
recipient == new operational contact email
```

Không gửi nhầm registrant nếu registrant khác contact mới.

---

# 10. Phase G — Chuẩn hóa rule thời gian 72 giờ

## 10.1. Rule chốt

Mọi hành động tạo/gửi lại lịch cho Staff Leader xử lý phải đảm bảo:

```text
plannedStart >= currentTime + 72 hours
```

---

## 10.2. Áp dụng cho

Bắt buộc audit:

```text
Create Visit Request V2
Edit Visit Request V2
Pending Edit
Resubmit rejected request
Add new campus
Change campus schedule
Any amendment that changes visit start time
```

Không áp dụng mù quáng cho action không thay đổi lịch nếu canonical business rule cho phép.

---

## 10.3. Frontend constant

Nếu hiện đang có:

```ts
export const V2_MIN_LEAD_TIME_MS = 24 * 60 * 60 * 1000;
```

đổi thành:

```ts
export const V2_MIN_LEAD_TIME_HOURS = 72;
export const V2_MIN_LEAD_TIME_MS =
  V2_MIN_LEAD_TIME_HOURS * 60 * 60 * 1000;
```

Nếu codebase đã có canonical shared constant phù hợp thì reuse, không tạo duplicate.

---

## 10.4. Backend validation

Backend phải check bằng server time:

```csharp
plannedStart < utcNow.AddHours(72)
→ reject
```

Dùng clock abstraction hiện có nếu project đã có.

Không gọi `DateTime.UtcNow` rải rác nếu service hiện đang dùng injectable clock.

---

## 10.5. Error code

Ưu tiên reuse error code validation hiện có.

Nếu chưa có error code phù hợp, tạo một code rõ nghĩa, ví dụ:

```text
VISIT_START_LEAD_TIME_NOT_MET
```

nhưng chỉ tạo mới nếu project chưa có canonical code.

Response phải chứa đủ:

```text
minimumHours = 72
earliestAllowedStart
```

nếu contract hiện tại cho phép metadata.

---

# 11. Phase H — Resubmit phải validate lại từ “thời điểm hiện tại”

## 11.1. Không dùng validity lúc tạo đơn ban đầu

Ví dụ:

```text
01/08 tạo request, visit = 10/08
→ hợp lệ

09/08 mới resubmit
→ visit còn <72h
```

Phải reject resubmit.

---

## 11.2. Resubmit flow

Trước khi chuyển request/campus về trạng thái chờ duyệt:

```text
validate all affected campus schedules
```

Nếu có campus không đủ 72 giờ:

```text
Không resubmit
→ trả lỗi rõ campus nào không hợp lệ
→ Visitor quay lại Edit
```

Không partial resubmit ngoài business rule hiện có.

---

# 12. Phase I — Thông báo khi request không còn hợp lệ về thời gian

## 12.1. Mục tiêu

Visitor không được để request “chết im” mà không biết phải sửa.

Khi hệ thống phát hiện request/campus cần người đăng ký can thiệp vì thời gian:

```text
notify registrant
```

---

## 12.2. Phân biệt các loại expiry

Không nhầm:

```text
Operational contact invitation expiry
```

với:

```text
Visit schedule no longer satisfies minimum lead time
```

Đây là hai nghiệp vụ khác nhau.

---

## 12.3. Điều kiện gửi notification

Audit trạng thái hiện có và chỉ gửi khi user có action thực tế.

Ví dụ phù hợp:

```text
REJECTED request cần resubmit
AND plannedStart < now + 72h
```

hoặc:

```text
pending-edit/resubmit window đã không còn hợp lệ do schedule
```

Không spam email mỗi lần background job chạy.

---

## 12.4. Idempotency

Notification phải idempotent.

Không để hosted service chạy mỗi N phút và gửi lại email giống nhau vô hạn.

Reuse:

- notification event/audit key
- email idempotency
- existing reminder marker

nếu codebase đã có.

Nếu chưa có cơ chế phù hợp, cần chọn cách tối thiểu nhưng không tạo schema mới nếu không bắt buộc.

---

## 12.5. Nội dung notification

Ví dụ:

```text
Yêu cầu VR-xxx cần được cập nhật

Lịch tham quan tại FPT University Hà Nội không còn đáp ứng
thời gian đăng ký tối thiểu 72 giờ.

Vui lòng chỉnh sửa lịch và gửi lại yêu cầu.
```

CTA:

```text
Chỉnh sửa đơn
```

---

# 13. Phase J — Đồng bộ Create / Edit / Resubmit

Sau khi sửa, không được tồn tại 3 rule khác nhau.

Tạo matrix audit:

| Action | Campus source | Lead time | Contact identity | Success toast |
|---|---|---:|---|---|
| Create | ACTIVE campus | 72h | create/claim flow | 1 |
| Edit | ACTIVE campus | 72h | email immutable | 1 |
| Resubmit | existing + ACTIVE validation | 72h tại thời điểm resubmit | email immutable | 1 |
| Change contact | request/campus scope hiện có | N/A | confirmation/transfer | 1 |

---

# 14. Backend Security / Authorization

## 14.1. Edit

Chỉ registrant/owner hợp lệ được edit theo rule hiện tại.

Không mở thêm quyền cho:

```text
Staff Leader
HO
Department Staff
Operational Contact
```

nếu canonical permission không cho.

---

## 14.2. Change Contact

Phải check:

```text
current user owns/can manage request
request state allows contact change
campus scope hợp lệ
```

Không cho user đổi contact của campus/request không thuộc quyền.

---

## 14.3. Confirmation

Accept/Decline token/action phải:

- Đúng recipient.
- Đúng invitation.
- Chưa expired.
- Chưa consumed.
- Không replay.
- Không đổi sang account không khớp identity.

---

# 15. Concurrency

## 15.1. Edit / Resubmit

Giữ optimistic concurrency hiện có:

```text
rowVersion / revision
```

Nếu request đã thay đổi sau khi form được load:

```text
→ conflict
→ không overwrite silent
```

---

## 15.2. Contact transfer

Nếu đang pending contact transfer:

- Không tạo nhiều transfer active cho cùng scope nếu business rule không cho.
- Cancel/supersede flow phải dùng rule hiện có.
- Accept cũ sau khi đã supersede phải fail.

---

# 16. Không thay đổi ngoài scope

Không thực hiện trong task này:

- Refactor toàn bộ Visit V2.
- Đổi database schema nếu existing infrastructure đủ dùng.
- Đổi permission matrix ngoài yêu cầu.
- Đổi aggregate status logic.
- Đổi Staff Leader workflow.
- Đổi email template system rộng hơn cần thiết.
- Đổi API contract công khai nếu có thể giữ backward-compatible.

---

# 17. Implementation Order

Thực hiện theo thứ tự:

## Step 1 — Audit code hiện tại trên `Dev`

Xác nhận:

```text
Create campus source
Edit campus source
Resubmit handler
toast source
contact identity guard
transfer confirmation flow
72h/24h constants
expiry/reminder job
```

Không code trước khi xác định chính xác file.

---

## Step 2 — Fix Campus limit + empty row

Frontend:

- Reuse active campus list.
- Bỏ `/10`.
- Fix new row `campusId = null`.
- Filter duplicate campus.

Test frontend trước.

---

## Step 3 — Fix duplicate toast

Audit success paths.

Chọn one-owner pattern.

Test Edit + Resubmit.

---

## Step 4 — Contact Edit UI

- Email readonly.
- Add `Thay đổi đầu mối`.
- Giữ backend immutable guard.

Không đụng identity backend trong step này ngoài wiring nếu cần.

---

## Step 5 — Wire Change Contact flow

Reuse existing:

```text
claim / transfer / confirm / decline / resend / cancel
```

Không viết duplicate workflow.

---

## Step 6 — 72H shared validation

Frontend + backend.

Create / Edit / Resubmit.

Sau đó audit amendment nếu có schedule edit.

---

## Step 7 — Expiry notification

Reuse reminder/email infrastructure.

Đảm bảo idempotency.

---

## Step 8 — Full regression tests

Chạy:

```text
backend unit tests
backend integration tests liên quan
frontend unit tests liên quan
frontend typecheck/build
```

Không cần chạy toàn bộ suite nếu project quá nặng ở vòng dev đầu tiên, nhưng trước khi merge phải chạy gate theo quy định hiện tại của project.

---

# 18. Test Cases bắt buộc

## 18.1. Campus count

### TC-CAMPUS-01

Given:

```text
5 campus ACTIVE
request có 2 campus
```

Expected:

```text
Thêm cơ sở (2/5)
```

---

### TC-CAMPUS-02

Given:

```text
5 campus ACTIVE
request đã có 5 campus
```

Expected:

```text
Add Campus disabled
```

---

### TC-CAMPUS-03

Add new campus row.

Expected:

```text
placeholder = Chọn cơ sở...
campusId = null
```

Không được hiển thị campus giả.

---

### TC-CAMPUS-04

Campus A đã được chọn.

Expected:

```text
Campus A không xuất hiện trong dropdown row khác
```

---

## 18.2. Toast

### TC-TOAST-01

Edit thành công.

Expected:

```text
1 success toast
```

---

### TC-TOAST-02

Resubmit thành công.

Expected:

```text
1 success toast
```

---

### TC-TOAST-03

Refresh detail page sau thành công.

Expected:

```text
không hiện lại toast cũ
```

---

## 18.3. Contact normal edit

### TC-CONTACT-01

Edit:

```text
name
organization
position
phone
```

Email không đổi.

Expected:

```text
save success
```

---

### TC-CONTACT-02

Client gọi API cố đổi email trong normal edit.

Expected:

```text
IMMUTABLE_CONTACT_IDENTITY
```

---

### TC-CONTACT-03

UI normal edit.

Expected:

```text
Email readonly
button Thay đổi đầu mối visible khi user có quyền
```

---

## 18.4. Change Contact

### TC-CONTACT-04

Registrant tạo contact transfer.

Expected:

```text
pending transfer created
confirmation email sent to new contact
old contact remains active
```

---

### TC-CONTACT-05

New contact accepts.

Expected:

```text
new contact becomes active
old relation ends according to canonical rule
```

---

### TC-CONTACT-06

New contact declines/expires.

Expected:

```text
old contact remains active
```

---

### TC-CONTACT-07

Try replay accepted/expired token.

Expected:

```text
reject
```

---

## 18.5. 72-hour validation

### TC-TIME-01

```text
plannedStart = now + 71h59m
```

Expected:

```text
reject
```

---

### TC-TIME-02

```text
plannedStart = now + 72h
```

Expected:

```text
accept
```

Có tolerance hợp lý nếu backend/frontend timestamp khác vài giây; test nên dùng fake clock nếu project hỗ trợ.

---

### TC-TIME-03

Create với <72h.

Expected:

```text
frontend block
backend block
```

---

### TC-TIME-04

Edit schedule từ >72h thành <72h.

Expected:

```text
block
```

---

### TC-TIME-05

Request cũ từng hợp lệ nhưng resubmit tại thời điểm còn <72h.

Expected:

```text
resubmit rejected
```

---

### TC-TIME-06

Multi-campus:

```text
Campus A >=72h
Campus B <72h
```

Expected:

```text
action rejected theo atomicity rule hiện tại
error chỉ rõ campus không hợp lệ nếu API contract hỗ trợ
```

---

## 18.6. Expiry notification

### TC-NOTIFY-01

Request cần resubmit và schedule không còn đủ 72h.

Expected:

```text
registrant nhận 1 notification/email
```

---

### TC-NOTIFY-02

Background job chạy lại.

Expected:

```text
không gửi duplicate
```

---

### TC-NOTIFY-03

Sau khi visitor sửa schedule hợp lệ.

Expected:

```text
không gửi cảnh báo cũ nữa
```

---

# 19. UI Acceptance Criteria

## Edit page

Phần đầu mối:

```text
Đầu mối của đoàn

Họ và tên       Đơn vị        Chức vụ
Số điện thoại   Email [readonly]

[Thay đổi đầu mối]
```

Không cho cảm giác email có thể sửa trực tiếp.

---

## Campus footer

Ví dụ:

```text
+ Thêm cơ sở (2/5)
```

Không:

```text
+ Thêm cơ sở (2/10)
```

---

## New campus row

Phải là:

```text
Cơ sở *
[ Chọn cơ sở... ]
```

Không auto hiện tên campus khi value chưa tồn tại.

---

## Success feedback

Edit:

```text
1 toast
```

Resubmit:

```text
1 toast
```

---

# 20. Backend Acceptance Criteria

Hoàn thành khi:

- Edit không đổi contact identity ngoài flow riêng.
- Contact transfer bắt buộc confirmation.
- Create/Edit/Resubmit cùng dùng rule 72h.
- Backend không tin frontend validation.
- Request không hợp lệ được báo cho registrant bằng flow idempotent.
- Không thêm bảng mới nếu chưa chứng minh cần.
- Không thay permission/business status ngoài scope.

---

# 21. Definition of Done

Task chỉ được coi là xong khi đáp ứng toàn bộ:

```text
[ ] Edit không còn hardcode /10
[ ] Count lấy từ ACTIVE campus
[ ] New campus row không hiển thị fake campus
[ ] Không chọn duplicate campus
[ ] Edit chỉ 1 toast
[ ] Resubmit chỉ 1 toast
[ ] Contact email readonly trong normal edit
[ ] Có action Thay đổi đầu mối
[ ] Backend immutable guard vẫn còn
[ ] Change Contact dùng existing confirmation/transfer flow
[ ] Confirmation email gửi đúng new contact
[ ] Old contact giữ hiệu lực khi pending/declined/expired
[ ] Create >=72h
[ ] Edit >=72h
[ ] Resubmit >=72h tại thời điểm resubmit
[ ] Backend enforce 72h
[ ] Registrant được cảnh báo khi request cần sửa do lead time
[ ] Notification không spam duplicate
[ ] Unit/integration/frontend tests liên quan green
[ ] Không phát sinh schema/API/architecture thay đổi ngoài scope
```

---

# 22. Báo cáo sau triển khai

Agent/dev cần trả kết quả theo format:

## 1. Root cause

- Campus `/10` do đâu.
- Duplicate toast do đâu.
- Contact email edit conflict do đâu.
- Rule 24h/72h nằm ở đâu.
- Notification expiry hiện tại có/không có gì.

## 2. Files changed

```text
file/path
- thay đổi gì
- lý do
```

## 3. Business logic sau sửa

```text
Create
Edit
Resubmit
Change Contact
Expiry Notification
```

## 4. Tests

```text
test suite
passed/failed
```

## 5. Remaining debt

Chỉ báo các phần thực sự chưa làm hoặc bị block.

Không đề xuất refactor ngoài scope.

---

# 23. Yêu cầu quan trọng cho agent triển khai

Trước khi sửa:

1. Checkout/pull `Dev` mới nhất.
2. Ghi lại HEAD SHA.
3. Đọc code thực tế của:
   - Create Visit V2
   - Edit Visit V2
   - Resubmit
   - Campus data source
   - Contact identity transfer
   - Email confirmation
   - Reminder/expiry
4. Chỉ sau đó mới sửa.
5. Không dựa vào tên file trong tài liệu này nếu codebase mới đã đổi tên.
6. Không bỏ business guard để “fix UI”.
7. Không hardcode campus count.
8. Không chỉ sửa frontend cho rule 72h.
9. Không tạo workflow contact mới nếu existing flow đã đáp ứng.
10. Không gửi duplicate toast/email/notification.
