# PEMS — Repair Prompt for Visit Edit / Resubmit / Contact / Campus / 72H / Email Logic

## 0. Mục tiêu

Đây là **prompt sửa phần code đã triển khai**, không phải thiết kế lại từ đầu.

Hãy audit code mới nhất trên nhánh `Dev`, xác định chính xác phần implementation hiện tại đang sai/thiếu so với business rule bên dưới, sau đó **sửa trực tiếp trên kiến trúc và flow đang có**.

Không tạo thêm flow song song nếu hệ thống đã có service/dispatcher/invitation/job tương ứng.  
Không tạo bảng mới chỉ để giải quyết các lỗi dưới đây nếu schema hiện tại đã đủ.  
Không xoá backend guard bảo vệ identity chỉ để làm UI “hết lỗi”.

---

# 1. Nguyên tắc bắt buộc trước khi sửa

1. Checkout/pull đúng nhánh `Dev` mới nhất.
2. Ghi lại:
   - branch;
   - HEAD SHA;
   - working tree;
   - các file WIP hiện có.
3. Không overwrite WIP của người khác.
4. Trace code thật từ:
   - Frontend page/component;
   - API client;
   - Controller/Endpoint;
   - Command/Handler/Service;
   - Entity/DB write;
   - Notification/email dispatcher;
   - Background job/expiry worker nếu có.
5. Với mỗi lỗi, phải báo:
   - root cause;
   - file/function gây lỗi;
   - thay đổi đã thực hiện;
   - test chứng minh.

## 1.1 Scope boundary cực kỳ quan trọng: 72h KHÔNG áp dụng cho Amendment sau duyệt

Trong task này phải phân biệt tuyệt đối hai nhóm flow:

```text
PRE-APPROVAL / RE-SUBMISSION FLOW
- Create request
- Edit request khi request/campus CHƯA được duyệt
- Resubmit sau khi bị Reject

→ CÓ validate minimum lead time 72h
```

và:

```text
POST-APPROVAL FLOW
- Approved request/campus
- Đề xuất thay đổi / Amendment
- Approve/Reject Amendment
- Các thay đổi sau duyệt được amendment policy cho phép

→ KHÔNG validate minimum lead time 72h
```

Không được reuse `Create/Edit/Resubmit` 72h validator trong Amendment chỉ vì Amendment cũng thay đổi dữ liệu của chuyến thăm.

Nếu Amendment có giới hạn thời gian hoặc field-policy riêng, phải dùng **canonical amendment policy/classifier hiện tại**, không dùng visit-registration lead-time rule 72h.

Tương tự, flow riêng như:

```text
Operational Contact INITIAL_CLAIM / TRANSFER / resend / accept / decline / expire
```

không được bị chặn bởi visit-registration lead-time 72h, trừ khi chính action đó đồng thời là một trong 3 submission event được liệt kê ở trên theo code/domain hiện hành.

---

# 2. Business rule chuẩn cần giữ

## 2.1 Campus count khi Edit

Hiện Edit đang có biểu hiện:

```text
Thêm cơ sở (x/10)
```

Không được hardcode `10`.

Số cơ sở tối đa phải dựa trên **danh sách campus hiện đang ACTIVE/được phép đăng ký**, cùng source of truth với Create Visit Request.

Yêu cầu:

```text
maxCampusCount = activeSelectableCampuses.length
```

hoặc dùng chính policy/service/helper mà màn Create đang dùng.

### Không được

```text
campuses.length >= 10
Thêm cơ sở (${campuses.length}/10)
```

nếu `10` không phải canonical business rule.

### Khi campus bị inactive

- Không được cho thêm mới campus inactive.
- Với request cũ đã chứa campus nay inactive, không tự ý xoá dữ liệu lịch sử.
- Xử lý theo behavior hiện có của backend/domain và báo rõ nếu cần exception cho legacy data.

---

# 3. Bug thêm campus nhưng UI báo “Vui lòng chọn cơ sở”

Audit `CampusVisitCard` và state mapping.

Hiện tượng:

- dropdown hiển thị tên campus;
- nhưng validation vẫn coi `campusId` là rỗng/null.

Phải đảm bảo:

```text
displayed campus
==
selected option
==
campusId trong state
==
campusId trong payload
```

Không được dùng fallback label khiến UI trông như đã chọn nhưng payload thực tế chưa có ID.

Thêm test:

```text
Add campus
→ chọn campus
→ campusId được cập nhật
→ validation hết lỗi
→ payload chứa đúng campusId
```

---

# 4. Duplicate toast sau Edit và Resubmit

Hiện đang có 2 toast giống hệt nhau sau:

- Edit thành công;
- Resubmit thành công.

Phải xác định chính xác toast đang được phát từ đâu.

Audit tối thiểu:

```text
mutation page
navigation state
detail page
toast hook/provider
route effect
StrictMode/effect re-run
```

## Quy tắc

Chỉ có **một owner** chịu trách nhiệm hiển thị success toast.

Khuyến nghị nếu hệ thống đang dùng navigation state:

```text
Edit/Resubmit
→ navigate(detailRoute, { state: { toast } })

Detail
→ consume toast đúng 1 lần
→ clear/replace history state sau khi consume
```

Không được đồng thời:

```text
toast.success(...)
+
navigate(... state.toast ...)
```

hoặc để 2 effect cùng consume một state.

### Acceptance

- Edit thành công → đúng 1 toast.
- Resubmit thành công → đúng 1 toast.
- Reload detail → không hiện lại toast cũ.
- Back/forward → không replay toast ngoài ý muốn.

---

# 5. Không cho sửa operational-contact email trực tiếp trong Edit Request

Backend hiện có guard kiểu:

```text
IMMUTABLE_CONTACT_IDENTITY
Không thể thay đổi email, tài khoản hoặc quan hệ đầu mối liên hệ trong lần chỉnh sửa này.
```

**Không được xoá guard này.**

Root UX đúng là UI đang cho người dùng nhập vào một field mà backend không cho thay đổi.

## 5.1 Normal Edit Request

Cho phép chỉnh các thông tin không làm thay đổi identity theo policy hiện tại, ví dụ nếu domain cho phép:

- tên;
- tổ chức;
- chức vụ;
- số điện thoại;
- các metadata không phải identity.

Email operational contact phải:

```text
read-only
```

và có action riêng:

```text
Thay đổi đầu mối
```

## 5.2 Thay đổi đầu mối phải là flow riêng

Không gửi email mới trong payload normal edit để thay identity.

Flow:

```text
Registrant chọn "Thay đổi đầu mối"
→ nhập email/thông tin đầu mối mới
→ backend tạo invitation/transfer theo canonical flow
→ trạng thái pending confirmation
→ gửi mail xác nhận tới đầu mối mới
→ chỉ khi accept mới áp dụng relation mới
```

Không duplicate business logic nếu project đã có:

```text
INITIAL_CLAIM
TRANSFER
claim/transfer invitation service
accept/decline/resend/cancel
```

---

# 6. INITIAL_CLAIM và TRANSFER không được trộn logic

## INITIAL_CLAIM

Dùng khi chưa có confirmed operational-contact relation phù hợp.

```text
create INITIAL_CLAIM
→ send confirmation email to pending contact
→ PENDING
→ ACCEPTED hoặc EXPIRED/DECLINED
```

Nếu expire:

```text
không bind pending contact
```

## TRANSFER

Giả sử:

```text
A = current confirmed contact
B = requested new contact
```

Khi tạo transfer:

```text
A vẫn là current contact
B = pending
```

Chỉ khi B accept:

```text
A → replaced according to canonical transfer transaction
B → new current contact
```

Nếu B decline/expire/cancel:

```text
A vẫn là current contact
B không được bind
```

Không được remove A ngay khi vừa tạo transfer.

---

# 7. Email khi Staff Leader Reject

Đây là requirement bắt buộc.

Audit code Reject thật sự, không được chỉ thấy template tồn tại rồi kết luận flow đã có email.

Trace:

```text
Reject endpoint
→ handler/service
→ status update
→ aggregate recompute
→ notification/email dispatcher
```

## 7.1 Recipient

Email Reject phải gửi cho:

```text
REGISTRANT / người đăng ký đơn
```

Không gửi nhầm operational contact trừ khi có business rule riêng đã tồn tại.

## 7.2 Per-campus semantics

Ví dụ:

```text
HN  = REJECTED
DN  = PENDING_APPROVAL
HCM = APPROVED
```

Email phải nói chính xác:

```text
Cơ sở Hà Nội đã từ chối yêu cầu
```

Không được nói:

```text
Toàn bộ yêu cầu đã bị từ chối
```

nếu aggregate request chưa phải `REJECTED`.

## 7.3 Reason

Email phải chứa lý do từ chối nếu handler có rejection reason.

## 7.4 Không duplicate

Nếu:

```text
campus reject
→ aggregate recompute
```

không được vô tình gửi 2 email rejection giống nhau cho cùng một business event.

Audit idempotency/event identity hiện có và reuse.

---

# 8. Email xác nhận đầu mối mới

Khi tạo:

```text
INITIAL_CLAIM
hoặc
TRANSFER
```

phải gửi confirmation email tới:

```text
TO = pending/new operational contact email
```

Email phải chứa action/token/link theo cơ chế hiện tại.

Không gửi confirmation token cho registrant thay vì contact.

---

# 9. Khi token/invitation xác nhận đầu mối hết hạn phải báo Registrant

Đây là requirement bắt buộc và **khác hoàn toàn visit lead-time 72h**.

Audit cơ chế expiry thật sự:

- lazy expiry khi đọc/consume;
- background worker;
- hosted service;
- scheduled maintenance;
- DB query cập nhật expired;
- hoặc mechanism hiện tại khác.

Không tự ý tạo worker mới nếu project đã có expiry maintenance phù hợp.

## 9.1 Khi invitation tới `expires_at`

Phải đảm bảo atomically:

```text
PENDING
→ EXPIRED
```

Sau đó:

```text
expired token không còn consume được
```

## 9.2 Notification recipient

Khi confirmation invitation hết hạn vì đầu mối không xác nhận:

```text
TO = REGISTRANT / người khởi tạo request/change-contact
```

Nội dung cần giúp họ biết:

- request nào;
- campus nào;
- đầu mối nào;
- email nào;
- invitation đã hết hạn;
- cần resend hoặc chọn/change contact khác.

## 9.3 Idempotency

Expiry job/check chạy nhiều lần vẫn chỉ gửi:

```text
1 expiry notification / 1 invitation expiry event
```

Không gửi mail lại mỗi lần worker scan.

---

# 10. Phân biệt ba rule thời gian

Tuyệt đối không trộn các rule sau.

| Rule | Ý nghĩa | Trigger | Email |
|---|---|---|---|
| Visit lead time 72h | Lịch được submit/re-submit để xét duyệt phải cách hiện tại tối thiểu 72h | Create + PRE-APPROVAL Edit submission + Resubmit after rejection | Không |
| INITIAL_CLAIM expiry | Hạn xác nhận đầu mối ban đầu | invitation expires_at | Có, báo registrant |
| TRANSFER expiry | Hạn xác nhận chuyển đầu mối | invitation expires_at | Có, báo registrant |

### Explicit exclusion khỏi rule 72h

Không áp dụng visit lead-time 72h cho:

```text
APPROVED request/campus
→ Amendment / Đề xuất thay đổi
```

Không áp dụng cho:

```text
CreateAmendment
UpdateAmendment
SubmitAmendment
ApproveAmendment
RejectAmendment
amendment classifier
amendment change-policy
```

hoặc các handler/service tương đương trong codebase.

Nếu một amendment được phép khi chuyến thăm chỉ còn 24h, 12h hoặc ít hơn, thì **không được reject chỉ vì không đủ 72h**. Validation của amendment phải theo policy riêng của amendment.

Nếu code hiện tại canonical quy định:

```text
INITIAL_CLAIM = 72h
TRANSFER = 24h
```

thì giữ đúng policy đó.

Không vì visit scheduling dùng 72h mà ép TRANSFER thành 72h.

---

# 11. Visit lead-time phải là 72 giờ, không phải 24 giờ

Audit toàn bộ constant/helper hiện có.

Nếu frontend còn:

```ts
V2_MIN_LEAD_TIME_MS = 24 * 60 * 60 * 1000;
```

thì sửa theo canonical business rule:

```ts
72 * 60 * 60 * 1000
```

nhưng backend phải là authority.

Không chỉ sửa frontend.

## 11.1 Áp dụng khi Create

Khi submit:

```text
plannedStart >= serverNow + 72h
```

## 11.2 Chỉ áp dụng khi PRE-APPROVAL EDIT

Đây là Edit Request **trước khi request/campus được duyệt**.

Ví dụ:

```text
PENDING / PENDING_APPROVAL / WAITING_CONTACT_CONFIRMATION
hoặc trạng thái editable-before-approval tương đương theo canonical workflow
→ registrant mở Edit Request
→ sửa dữ liệu
→ bấm Save/Submit update để Staff Leader tiếp tục xét
```

Tại thời điểm action đó, schedule phải được validate lại:

```text
plannedStart >= serverNow + 72h
```

Không cho user submit một phiên bản pre-approval mới có lịch không đủ 72h.

Phải theo đúng workflow code hiện hành, không tự tạo trạng thái mới.

**Cấm áp section này sang Approved Amendment / Đề xuất thay đổi sau duyệt.**

## 11.3 Áp dụng khi Resubmit sau Reject

Đây là case:

```text
request/campus từng hợp lệ
→ bị REJECTED
→ registrant sửa
→ Resubmit
```

Tại thời điểm Resubmit:

```text
plannedStart >= serverNow + 72h
```

phải được evaluate lại bằng server time hiện tại.

Không được dựa vào thời điểm request được tạo lần đầu.

## 11.4 KHÔNG áp dụng cho Approved Amendment / Đề xuất thay đổi

Ví dụ:

```text
request/campus = APPROVED
visit starts in 24h
registrant gửi một Amendment được policy hiện tại cho phép
```

Expected:

```text
- KHÔNG reject vì visit còn <72h
- KHÔNG gọi Create/Edit/Resubmit lead-time validator
- dùng amendment classifier/policy/validation hiện tại
```

Không sửa các flow như:

```text
CreateAmendment
ApproveAmendment
amendment classifier
amendment safe-edit/change rules
```

để ép thêm điều kiện 72h.

Nếu code hiện tại vô tình dùng chung helper 72h cho Amendment, hãy tách caller/scope hoặc guard đúng layer để Amendment không bị ảnh hưởng, nhưng không duplicate business logic không cần thiết.

---

# 12. Không gửi mail chỉ vì thời gian tự trôi xuống còn <72h

Đây là logic đã từng bị hiểu sai và phải loại bỏ.

Ví dụ:

```text
Ngày submit:
visit start còn 100h
→ hợp lệ

Sau 40h:
visit start còn 60h
```

Nếu không có user action mới:

```text
KHÔNG tự EXPIRE request
KHÔNG tự REJECT
KHÔNG gửi "schedule <72h" email
KHÔNG bắt user edit lại
```

72h chỉ là validation cho **Create request, PRE-APPROVAL Edit submission, và Resubmit after rejection**; không phải countdown expiry event và không phải validation của Approved Amendment.

Nếu implementation hiện tại đã thêm background email kiểu:

```text
request now less than 72h
→ email registrant
```

hãy remove/disable phần đó và test chống regression.

---

# 13. Resubmit flow

Audit resubmit end-to-end.

Yêu cầu:

```text
REJECTED/editable request
→ user sửa
→ Resubmit
→ backend validate business rules mới nhất
→ validate 72h bằng server time
→ recompute per-campus/request state đúng
→ trigger workflow tiếp theo đúng canonical behavior
→ exactly one success toast
```

Không được bỏ validation chỉ vì record trước đây từng pass.

Nếu request multi-campus:

- chỉ reset/reopen đúng scope theo domain rule hiện tại;
- không làm mất quyết định của campus không thuộc scope nếu canonical business rule yêu cầu giữ;
- không tự suy diễn, hãy reuse aggregate service hiện tại.

---

# 14. Email Reject và Email Contact Expiry là hai business event khác nhau

Không reuse sai template/nội dung.

## Rejection

```text
event: STAFF_LEADER/CAMPUS REJECTION
recipient: registrant
content:
- request
- campus
- rejection reason
- action to edit/resubmit
```

## Contact confirmation expiry

```text
event: OPERATIONAL_CONTACT_INVITATION_EXPIRED
recipient: registrant/change initiator
content:
- request
- campus
- pending contact
- invitation expired
- resend/change-contact action
```

Không dùng rejection template cho expiry và ngược lại.

---

# 15. Email infrastructure

Phải reuse canonical email infrastructure hiện tại:

```text
ISystemEmailDispatcher
template registry/defaults
recipient validator
history policy
idempotency mechanism
```

hoặc abstraction tương đương đang tồn tại trên Dev.

Không tạo đường gửi mail riêng bằng SMTP/Resend trực tiếp trong handler.

Không bypass:

- recipient validation;
- template rendering;
- sensitive history policy;
- idempotency;
- auditing.

---

# 16. Không tạo schema mới nếu chưa chứng minh cần thiết

Trước khi tạo migration/table/column mới, phải chứng minh schema hiện tại không thể lưu:

- invitation status;
- expires_at;
- invitation type;
- registrant/request initiator;
- notification idempotency/event identity.

Nếu schema hiện tại đã có đủ dữ liệu, chỉ sửa service/handler/job.

Nếu bắt buộc migration:
- giải thích vì sao;
- additive;
- rollback;
- canonical SQL cập nhật;
- tests;
- không phá dữ liệu hiện tại.

---

# 17. Test bắt buộc

## CAMPUS

### TC-CAMPUS-01

```text
Active campuses = N
Edit page
Expected:
Thêm cơ sở (current/N)
```

### TC-CAMPUS-02

```text
Select a campus in newly added card
Expected:
visible campus == campusId state == payload campusId
no "Vui lòng chọn cơ sở"
```

---

## TOAST

### TC-TOAST-01

```text
Edit success
Expected: exactly 1 success toast
```

### TC-TOAST-02

```text
Resubmit success
Expected: exactly 1 success toast
```

### TC-TOAST-03

```text
Reload detail
Expected: old navigation toast is not replayed
```

---

## CONTACT IDENTITY

### TC-CONTACT-01

```text
Normal edit
Expected:
operational contact email read-only
backend immutable identity guard remains
```

### TC-CONTACT-02

```text
Click "Thay đổi đầu mối"
→ create transfer/claim
Expected:
new contact pending
old confirmed contact not removed prematurely
```

---

## CONTACT EMAIL

### TC-CONTACT-MAIL-01

```text
Create INITIAL_CLAIM/TRANSFER
Expected:
confirmation email goes to pending/new contact
```

### TC-CONTACT-MAIL-02

```text
pending invitation expires
Expected:
invitation = EXPIRED
expired token unusable
registrant receives exactly 1 expiry email
```

### TC-CONTACT-MAIL-03

```text
TRANSFER:
A current
B pending
B expires

Expected:
A still current
B not assigned
registrant gets one expiry email
```

### TC-CONTACT-MAIL-04

```text
Expiry scanner/job runs twice
Expected:
no duplicate expiry email
```

---

## REJECTION EMAIL

### TC-REJECT-MAIL-01

```text
Staff Leader rejects HN with reason R

Expected:
HN = REJECTED
registrant receives exactly one email
email includes HN and R
```

### TC-REJECT-MAIL-02

```text
HN = REJECTED
DN = PENDING_APPROVAL

Expected:
email states HN rejected
does not state entire request rejected
```

### TC-REJECT-MAIL-03

```text
Reject causes aggregate recompute
Expected:
no duplicate rejection email for the same business event
```

---

## 72H

### TC-72H-01 — Create

```text
start = serverNow + 71h59m
Expected: reject
```

### TC-72H-02 — Create boundary

```text
start >= serverNow + 72h
Expected: pass
```

### TC-72H-03 — Edit

```text
change start to < serverNow + 72h
Expected: reject save/submit according to workflow
```

### TC-72H-04 — Resubmit

```text
old request was once valid
current start now < serverNow + 72h
Expected: resubmit rejected
```

### TC-72H-05 — Time passes only

```text
request submitted validly at 100h lead time
later only 60h remains
no edit/resubmit action

Expected:
no auto reject
no auto expire
no 72h warning email
```

### TC-72H-06 — Approved Amendment dưới 72h vẫn không bị lead-time validator chặn

```text
request/campus = APPROVED
visit start = serverNow + 24h

When:
registrant creates/submits an Amendment / Đề xuất thay đổi
that is allowed by canonical amendment policy

Expected:
- NOT rejected because of 72h visit-registration lead time
- amendment follows its own policy/classifier
- Create/Edit/Resubmit 72h validator is NOT invoked for this reason
```

### TC-72H-07 — Amendment không bị ảnh hưởng khi dùng shared helper

Nếu codebase có shared scheduling helper:

```text
Approved Amendment
→ invokes amendment flow
```

Expected:

```text
shared helper / caller scope does not apply 72h registration rule
to approved amendment
```

Test phải bắt regression nếu sau này ai đó vô tình gọi lại pre-approval lead-time validator từ amendment handler.

### TC-72H-08 — Contact flow không bị lead-time rule chặn

```text
request/campus context is less than 72h from planned visit
user performs allowed INITIAL_CLAIM / TRANSFER confirmation action
```

Expected:

```text
contact invitation workflow follows its own expiry/authorization policy
and is not rejected solely because planned visit is <72h
```

---

# 18. Backend authority và race condition

Frontend validation chỉ để UX.

Backend phải dùng server clock:

```text
serverNow
```

Không tin `clientNow`.

Khi accept contact invitation:

- validate token;
- validate status;
- validate expires_at;
- apply transition atomically;
- expired invitation cannot be accepted because of race.

Khi Reject/Resubmit:
- transaction/state transition theo cơ chế hiện tại;
- email/event chỉ phát sau khi business state update thành công;
- tránh email gửi khi transaction rollback.

---

# 19. Không được sửa kiểu “che lỗi”

Không chấp nhận các cách sau:

```text
- xoá IMMUTABLE_CONTACT_IDENTITY
- hardcode campus count sang một số khác
- chỉ đổi label /10 thành /N nhưng vẫn cho add quá N
- chỉ sửa frontend 72h, backend vẫn 24h
- toast thêm debounce để che duplicate source
- gửi expiry email mỗi lần GET phát hiện expired
- remove current contact khi transfer vừa được tạo
- tự expire visit request khi <72h
- gửi rejection email nói toàn request rejected khi chỉ 1 campus rejected
- gọi 72h registration validator từ Approved Amendment / Đề xuất thay đổi
```

Phải sửa root cause.

---

# 20. Các điểm cần audit trước khi code

Hãy tìm và báo exact location của:

1. hardcoded `10` ở Edit page;
2. source active campuses ở Create page;
3. `V2_MIN_LEAD_TIME_MS` hoặc equivalent;
4. backend lead-time validator;
5. Edit endpoint/service;
6. Resubmit endpoint/service;
7. `IMMUTABLE_CONTACT_IDENTITY`;
8. `EnsureContactEmailUnchanged` hoặc equivalent;
9. `INITIAL_CLAIM`;
10. `TRANSFER`;
11. invitation expiry service/job;
12. contact confirmation email send point;
13. Staff Leader Reject handler;
14. rejection email send point;
15. navigation success toast producer;
16. detail success toast consumer;
17. Create/Submit/Approve Amendment handlers và mọi shared validator chúng đang gọi, để chứng minh 72h không bị áp nhầm.

Nếu bất kỳ send point nào không tồn tại, ghi rõ:

```text
MISSING SEND POINT
```

rồi implement thông qua canonical dispatcher.

Không được kết luận “đã có email” chỉ vì template tồn tại trong DB/default JSON.

---

# 21. Implementation order

Thực hiện theo thứ tự:

```text
Phase A — Preflight + trace current implementation

Phase B — Campus max/source-of-truth + CampusVisitCard state bug

Phase C — Duplicate toast root cause

Phase D — Separate request edit from contact identity change

Phase E — Contact invitation confirmation send flow

Phase F — Contact invitation expiry + registrant notification

Phase G — Staff Leader rejection email audit/fix

Phase H — 72h canonical validation chỉ cho Create + PRE-APPROVAL Edit + Resubmit; regression guard cho Approved Amendment

Phase I — Remove any incorrect "<72h countdown email/auto-expiry" implementation

Phase J — Tests + gates + report
```

Không bắt đầu Phase F/G bằng cách tạo mail service mới nếu chưa audit dispatcher hiện tại.

---

# 22. Acceptance Criteria

## Frontend

- [ ] Edit không hardcode `/10`.
- [ ] Max campus lấy từ canonical active-campus source.
- [ ] Newly selected campus có đúng `campusId`.
- [ ] Edit success chỉ 1 toast.
- [ ] Resubmit success chỉ 1 toast.
- [ ] Contact email không còn giả vờ editable trong normal request edit.
- [ ] Có action riêng cho change contact nếu user có quyền.
- [ ] 72h validation UX đồng bộ backend cho Create / PRE-APPROVAL Edit / Resubmit.
- [ ] Approved Amendment không hiển thị lỗi 72h của registration flow.

## Backend

- [ ] Backend enforce 72h bằng server time.
- [ ] Resubmit revalidate 72h tại thời điểm resubmit.
- [ ] PRE-APPROVAL Edit enforce 72h tại thời điểm submit update.
- [ ] Approved Amendment / Đề xuất thay đổi KHÔNG bị visit-registration 72h validator chặn.
- [ ] Contact claim/transfer workflow không bị chặn chỉ vì planned visit còn <72h.
- [ ] Immutable contact identity guard vẫn tồn tại.
- [ ] Contact change dùng canonical claim/transfer invitation.
- [ ] New/pending contact nhận confirmation email.
- [ ] Expired contact invitation không thể accept.
- [ ] Expiry notification gửi registrant đúng 1 lần.
- [ ] Transfer expiry giữ current contact.
- [ ] Staff Leader rejection gửi registrant email đúng scope.
- [ ] Multi-campus rejection không overstate aggregate request.
- [ ] Không duplicate rejection email.
- [ ] Không gửi email chỉ vì visit start tự trôi xuống <72h.

## Data/Architecture

- [ ] Không tạo bảng mới nếu schema hiện tại đủ.
- [ ] Không có direct SMTP/Resend call ngoài canonical mail infrastructure.
- [ ] Không phá per-campus v2 semantics.
- [ ] Không làm mất legacy/history data.
- [ ] Idempotency được chứng minh bằng test.

---

# 23. Gates

Chạy tối thiểu các gate phù hợp với repo:

```text
dotnet build
backend unit tests
backend architecture tests
targeted integration tests
frontend typecheck
frontend unit tests liên quan
frontend build
```

Nếu full integration cần MySQL/real stack mà environment hiện tại không có:

- không giả vờ PASS;
- chạy targeted tests có thể chạy;
- báo BLOCKED chính xác;
- không sửa test để ép xanh.

---

# 24. Báo cáo cuối cùng bắt buộc

Trả report theo format:

## 1. Preflight

```text
Branch:
Start HEAD:
End HEAD:
WIP preserved:
```

## 2. Root causes

Bảng:

| Issue | Root cause | File/function |
|---|---|---|
| Campus x/10 | ... | ... |
| Campus selected nhưng invalid | ... | ... |
| Duplicate Edit toast | ... | ... |
| Duplicate Resubmit toast | ... | ... |
| Contact email editable nhưng backend block | ... | ... |
| Contact confirmation mail | ... | ... |
| Contact expiry notification | ... | ... |
| Reject email | ... | ... |
| 24h/72h mismatch | ... | ... |
| Incorrect <72h email/expiry | ... | ... |

## 3. Changed files

Liệt kê từng file + lý do.

## 4. Email behavior sau sửa

Phải ghi rõ:

```text
Reject:
TO = ?
trigger = ?
idempotency = ?

INITIAL_CLAIM confirmation:
TO = ?
expiry = ?
expiry notification TO = ?

TRANSFER confirmation:
TO = ?
expiry = ?
on expiry current contact = ?
expiry notification TO = ?
```

## 5. 72h behavior

Phải chứng minh:

```text
Create:
PRE-APPROVAL Edit:
Resubmit after rejection:
Approved Amendment:
Contact claim/transfer:
Passive time passage:
```

Trong đó `Approved Amendment` phải ghi rõ **không bị registration lead-time 72h chặn**.

## 6. Tests

Liệt kê:

```text
test name
expected
result
```

## 7. Gates

```text
Build:
Unit:
Architecture:
Integration:
Frontend typecheck:
Frontend unit:
Frontend build:
```

## 8. Remaining debt

Chỉ liệt kê debt thực sự chưa làm được.

---

# 25. Stop conditions

Dừng và báo trước khi:

- thay đổi schema lớn;
- xoá canonical guard;
- thay đổi semantics của INITIAL_CLAIM/TRANSFER không có bằng chứng code/business rule;
- đổi aggregate status rule;
- rewrite toàn bộ Visit flow;
- rewrite email infrastructure;
- thêm 72h validation vào Approved Amendment / Đề xuất thay đổi;
- sửa amendment classifier/policy chỉ để ép lead-time registration 72h;
- phát hiện WIP/conflict có nguy cơ mất code người khác.

---

# 26. Kết quả mong muốn

Sau lần fix này, flow phải nhất quán:

```text
PRE-APPROVAL EDIT REQUEST
→ chỉnh business data
→ contact identity không sửa inline
→ schedule của submission mới phải >= 72h
→ save/submit thành công → 1 toast

APPROVED AMENDMENT / ĐỀ XUẤT THAY ĐỔI
→ dùng amendment workflow riêng
→ KHÔNG áp visit-registration lead-time 72h
→ validate bằng amendment policy/classifier hiện tại

CHANGE CONTACT
→ flow riêng
→ pending invitation
→ confirmation email tới contact
→ accept mới áp dụng
→ expire thì báo registrant
→ transfer expire vẫn giữ contact cũ

STAFF LEADER REJECT
→ đúng campus/status
→ rejection email tới registrant
→ đúng reason/scope
→ không duplicate

RESUBMIT
→ validate lại bằng serverNow + 72h
→ success → 1 toast

PASSIVE TIME
→ request không tự expire chỉ vì còn <72h
→ không có email countdown 72h
```

**Ưu tiên sửa root cause, reuse architecture hiện tại, không tạo thêm cơ chế cạnh tranh với canonical flow.**
