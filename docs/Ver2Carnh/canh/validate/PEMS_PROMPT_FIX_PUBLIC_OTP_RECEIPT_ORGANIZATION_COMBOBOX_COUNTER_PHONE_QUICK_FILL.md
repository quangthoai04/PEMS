# PEMS — IMPLEMENTATION PROMPT
## Fix kết quả sau OTP public, organization combobox, counter độ dài theo focus, hướng dẫn số điện thoại và nút sao chép thông tin đầu mối

## 0. Vai trò

Bạn là Senior Full-stack Engineer phụ trách dự án PEMS.

Hãy đọc code hiện tại trên nhánh `Cảnh-Iter1` tại HEAD mới nhất và triển khai đồng bộ frontend, backend validation, draft lifecycle, i18n và kiểm thử cho các vấn đề dưới đây.

Không được chỉ sửa UI bề mặt. Mọi thay đổi phải bảo đảm:

```text
Không mất dữ liệu
Không tạo đơn trùng
Không mở rộng quyền
Không lộ dữ liệu public
Frontend/backend validation đồng bộ
Public và authenticated flow có hành vi rõ ràng
```

---

# 1. Bối cảnh và triệu chứng thực tế

## 1.1 Public flow sau OTP

Hiện tại khi người dùng chưa đăng nhập:

```text
Điền form
→ nhận OTP
→ nhập OTP đúng
→ đơn được tạo
→ modal đóng
→ chỉ hiện toast “Đã tạo đơn ... thành công”
```

Người dùng không thấy màn hình kết quả cố định, không xem lại được thông tin vừa gửi và không biết bước tiếp theo.

Toast không được xem là bằng chứng đầy đủ của việc tạo đơn thành công.

## 1.2 Đơn vị công tác của đầu mối phối hợp từng campus

Field:

```text
Đầu mối phối hợp tại cơ sở → Đơn vị công tác
```

hiện là input text thông thường, trong khi các phần khác đã có organization combobox/dropdown tìm đối tác/đơn vị có sẵn.

## 1.3 Counter độ dài làm giao diện dày

Các field dài hiện có dạng:

```text
0/2000
0/4000
```

hiển thị liên tục kể cả khi người dùng chưa tương tác, làm form có quá nhiều chữ phụ.

Yêu cầu mới:

- Khi focus/click vào field mới hiện counter.
- Khi blur và field hợp lệ, counter có thể ẩn.
- Khi gần giới hạn hoặc vượt giới hạn, counter phải tiếp tục hiện.
- Khi vượt phải báo cụ thể đúng tên field và số ký tự tối đa.

## 1.4 Số điện thoại báo lỗi chưa đủ hướng dẫn

Message hiện tại chỉ nói:

```text
Số điện thoại đầu mối phối hợp không hợp lệ.
```

Người dùng không biết hệ thống chấp nhận định dạng nào nên phải thử nhiều lần.

## 1.5 Badge “Đã chọn đối tác có sẵn”

Field Đơn vị công tác ở phần người đăng ký hiện hiển thị badge:

```text
Đã chọn đối tác có sẵn
```

Cần thống nhất khi nào hiển thị badge này, tránh lặp lại ở mọi field và làm UI rối.

## 1.6 Đầu mối phối hợp từng campus thiếu quick-fill

Cần xem xét thêm các action:

```text
Dùng thông tin người đăng ký
Dùng thông tin đầu mối chính
```

để giảm nhập lặp.

---

# 2. Preflight bắt buộc

Trước khi sửa:

```bash
git status
git branch --show-current
git rev-parse HEAD
git log -n 15 --oneline
git diff --check
```

Không:

- reset;
- rebase;
- force-push;
- xóa stash;
- xóa WIP;
- tự thay đổi migration nếu chưa xác minh DB capacity.

Rà soát tối thiểu:

```text
Public create modal/page
Authenticated create modal/page
OTP verification modal
SubmissionStage/state machine
Success receipt/result panel
onSuccess/onClose callbacks
Draft clear/reset lifecycle
Created request response DTO
OrganizationCombobox
Partner selection field
CampusVisitCard
OperationalContact fields
FormField
AutoGrowTextarea
AutoGrowTextField nếu đã có
Phone validation utility
Zod schemas
FluentValidation validators
VI/EN translation
```

Báo cáo evidence file/hàm/dòng trước khi sửa.

---

# PHẦN A — PUBLIC SUCCESS RECEIPT SAU OTP

# 3. Quy tắc bắt buộc

Sau khi OTP đúng và backend xác nhận request đã commit:

```text
Không đóng modal ngay
Không reset toàn bộ UI ngay
Không chỉ hiện toast
Không đưa người dùng về trang chủ mà không có receipt
```

Phải chuyển sang:

```text
CREATE_CONFIRMED
```

và render một success receipt cố định trong modal.

Toast chỉ là phản hồi bổ sung.

---

# 4. Success receipt cho public flow

Hiển thị tối thiểu:

```text
✓ Đăng ký tham quan thành công

Mã đơn: VR2026072629B9DFF
Trạng thái: Chờ xử lý
Thời gian gửi: 26/07/2026 14:30
Số cơ sở: 1

Đơn của bạn đã được lưu thành công.
Vui lòng lưu lại mã đơn và kiểm tra email để theo dõi các bước tiếp theo.
```

Action bắt buộc:

```text
[Xem lại thông tin đã gửi]
[Sao chép mã đơn]
[Đóng]
```

Action tùy điều kiện:

```text
[Đăng nhập để quản lý đơn]
```

Không hiển thị nút “Xem đơn vừa tạo” trỏ trực tiếp vào protected detail nếu public user chưa có authenticated session.

---

# 5. Xem lại thông tin đã gửi

Khi bấm:

```text
Xem lại thông tin đã gửi
```

hiển thị read-only summary của đúng snapshot vừa submit:

```text
Thông tin người đăng ký
Đầu mối liên hệ chính
Đối tác/đơn vị
Danh sách campus
Ngày giờ
Tên đoàn
Loại hình
Mục đích
Nội dung làm việc
Danh sách khách
Nhân sự hỗ trợ
Đầu mối phối hợp từng campus
Ngôn ngữ
Đồng ý truyền thông
Phương tiện
Ghi chú
```

Quy tắc bảo mật:

- Không gọi anonymous detail API trả toàn bộ request.
- Ưu tiên giữ một `submittedSnapshot` bất biến trong React state trước khi clear/reset form.
- Snapshot chỉ tồn tại trong session UI hiện tại.
- Không lưu raw OTP.
- Không nhét snapshot vào URL.
- Không log full snapshot.
- Khi user đóng modal thì snapshot có thể bị hủy.
- Email xác nhận vẫn phải chứa mã đơn và hướng dẫn tiếp theo theo logic hiện có.

Ví dụ:

```ts
interface PublicCreatedReceipt {
  visitRequestId: number;
  requestCode: string;
  status: string;
  submittedAt: string;
  campusCount: number;
  submittedSnapshot: VisitRequestV2Schema;
}
```

`submittedSnapshot` phải là deep clone tại thời điểm verify thành công, không giữ reference tiếp tục bị reset.

---

# 6. Thứ tự clear dữ liệu

Sau backend success:

```text
1. Deep clone form values thành submittedSnapshot.
2. Lưu CreatedVisitResult/receipt.
3. Chuyển stage = CREATE_CONFIRMED.
4. Clear OTP context.
5. Clear durable draft vì request đã tạo thành công.
6. Không reset submittedSnapshot.
7. Không đóng modal.
```

Chỉ reset form để tạo đơn mới khi người dùng chủ động bấm:

```text
Tạo đơn mới
```

Nếu người dùng đóng receipt:

- refresh danh sách nếu là authenticated dashboard;
- public flow chỉ đóng modal;
- không hiện lại draft vừa hoàn tất;
- toast vẫn có thể nhắc mã đơn nhưng không thay receipt.

---

# 7. Authenticated flow

Authenticated success receipt hiển thị:

```text
[Xem đơn vừa tạo]
[Về danh sách đơn]
[Tạo đơn mới]
```

`Xem đơn vừa tạo` mở:

```text
/dashboard/visit/v2/{visitRequestId}
```

Public và authenticated dùng chung shared receipt component nhưng action khác nhau theo session/auth state.

Không duy trì hai UI success hoàn toàn tách biệt dễ drift.

---

# 8. “Đăng nhập để quản lý đơn”

Chỉ hiển thị nếu login flow hỗ trợ đầy đủ:

```text
returnUrl
đăng nhập đúng email/account
sau login mở đúng request
sai account báo rõ quyền truy cập
```

Text phải rõ:

```text
Đăng nhập để quản lý đơn
```

Không dùng label “Xem đơn” khiến public user tưởng xem được ngay.

Nếu return flow chưa hoàn chỉnh, bỏ action này khỏi scope hiện tại thay vì tạo ngõ cụt.

---

# PHẦN B — ORGANIZATION COMBOBOX CHO ĐẦU MỐI PHỐI HỢP

# 9. Thay input text bằng organization combobox

Field:

```text
campusVisits[i].operationalContact.organization
```

phải tái sử dụng shared:

```text
OrganizationCombobox
```

Hành vi:

- Tìm kiếm đối tác/đơn vị có sẵn.
- Chọn một kết quả có sẵn.
- Vẫn cho nhập đơn vị mới/free text.
- Giá trị lưu ở operational contact vẫn là snapshot text theo contract hiện tại.
- Không tự ràng buộc đầu mối campus với partnerId nếu schema hiện không có quan hệ đó.
- Không làm thay đổi request-level partner selection.
- Hỗ trợ keyboard và mobile.
- Draft restore/copy campus hoạt động đúng.

---

# 10. Quy tắc badge đối tác

## 10.1 Nơi nên hiển thị badge đầy đủ

Badge xanh:

```text
Đã chọn đối tác có sẵn
```

chỉ nên xuất hiện tại field request-level nơi người dùng thực sự chọn đối tác chính cho đơn.

Đây là nơi cần phân biệt:

```text
Đối tác có sẵn
Tổ chức mới
```

## 10.2 Các field organization khác

Không lặp badge đầy đủ ở:

```text
Đầu mối chính
Đầu mối phối hợp từng campus
Khách
Nhân sự hỗ trợ
```

vì:

- các field đó là snapshot;
- có thể thuộc đơn vị khác đối tác chính;
- lặp badge làm UI rối;
- dễ khiến người dùng hiểu sai rằng tất cả đều liên kết cùng partner record.

Trong organization combobox, khi user chọn một kết quả có sẵn có thể dùng tín hiệu nhẹ:

```text
✓ Có trong hệ thống
```

hoặc icon check nằm trong dropdown/field, chỉ hiện khi focus hoặc ngay sau lựa chọn.

Nếu field được quick-fill từ đối tác chính, dùng subtitle nhỏ:

```text
Đã sao chép từ đối tác của đơn
```

Không dùng badge lớn ở tất cả nơi.

---

# PHẦN C — QUICK-FILL CHO ĐẦU MỐI PHỐI HỢP CAMPUS

# 11. Đề xuất được chốt

Nên bổ sung cả hai action:

```text
[Dùng thông tin người đăng ký]
[Dùng thông tin đầu mối chính]
```

Lý do:

- Có chuyến thăm mà người đăng ký trực tiếp phối hợp campus.
- Có chuyến thăm mà đầu mối chính là người phối hợp.
- Hai người có thể khác nhau.
- Chỉ có một nút sẽ không đủ cho cả hai trường hợp.

---

# 12. Cơ chế copy

Đây là one-time copy, không phải liên kết động.

Khi bấm:

```text
Dùng thông tin người đăng ký
```

copy:

```text
fullName
organization
phone
email
```

từ `registerInfo`.

Khi bấm:

```text
Dùng thông tin đầu mối chính
```

copy cùng bốn field từ `contactPoint`.

Sau khi copy:

- người dùng được sửa độc lập;
- thay đổi source sau đó không tự cập nhật campus contact;
- không tạo shared reference;
- không ảnh hưởng campus khác;
- dirty state và draft phải cập nhật;
- validation chạy lại.

Hiển thị feedback nhẹ:

```text
Đã sao chép thông tin người đăng ký.
```

dùng top-right toast hoặc inline micro-feedback theo shared convention.

---

# 13. UI quick-fill

Đặt trong legend/header của fieldset:

```text
Đầu mối phối hợp tại cơ sở        [Dùng người đăng ký] [Dùng đầu mối chính]
```

Desktop:

- button nhỏ;
- không chiếm một hàng lớn;
- wrap khi hẹp.

Mobile:

- xuống dòng;
- không overflow;
- label ngắn nhưng có accessible name đầy đủ.

Chỉ enable button khi source có dữ liệu tối thiểu.

Nếu destination đã có dữ liệu, bấm copy phải hỏi xác nhận:

```text
Thông tin đầu mối phối hợp hiện tại sẽ được thay thế.
[Hủy] [Tiếp tục]
```

Không âm thầm ghi đè.

---

# PHẦN D — COUNTER ĐỘ DÀI THEO FOCUS

# 14. Shared behavior

Áp dụng cho field có maximum length.

Counter hiển thị khi:

```text
Field đang focus
HOẶC value >= 80% giới hạn
HOẶC field đang vượt giới hạn
HOẶC field đang có validation error liên quan độ dài
```

Counter được ẩn khi:

```text
Field blur
Và value < 80%
Và field hợp lệ
```

Ví dụ:

```text
0/2000
1520/2000
2014/2000
```

Không hiển thị toàn bộ counter của mọi field khi form vừa mở.

---

# 15. Message vượt giới hạn

Không dùng message chung chung:

```text
Vượt quá giới hạn.
```

Phải gọi đúng tên field:

```text
Nhận diện phương tiện di chuyển không được vượt quá 2.000 ký tự.
Ghi chú gửi FPTU không được vượt quá 2.000 ký tự.
Mục đích không được vượt quá 2.000 ký tự.
Nội dung làm việc không được vượt quá 4.000 ký tự.
Tên đoàn không được vượt quá 200 ký tự.
```

Khi phù hợp có thể thêm số hiện tại:

```text
Nhận diện phương tiện di chuyển không được vượt quá 2.000 ký tự (hiện tại 2.014).
```

Không silently truncate.

Paste dài phải giữ text để user thấy phần vượt và sửa, trừ khi shared UX hiện tại đã có quyết định khác.

---

# 16. Shared component/API

Mở rộng `AutoGrowTextarea`/`AutoGrowTextField` hoặc tạo shared hook:

```ts
interface CharacterCountOptions {
  maxLength: number;
  fieldLabel: string;
  showThreshold?: number; // default 0.8
}
```

Component phải nhận:

```text
focused
touched
error
value.length
maxLength
```

Không hardcode message trong từng component.

i18n gợi ý:

```json
{
  "characterCount": "{{current}}/{{max}}",
  "maxLengthField": "{{field}} không được vượt quá {{max}} ký tự.",
  "maxLengthFieldWithCurrent": "{{field}} không được vượt quá {{max}} ký tự (hiện tại {{current}})."
}
```

Định dạng số theo locale:

```text
2.000
4.000
```

---

# PHẦN E — HƯỚNG DẪN ĐỊNH DẠNG SỐ ĐIỆN THOẠI

# 17. Message mới

Không chỉ báo:

```text
Số điện thoại không hợp lệ.
```

Phải chỉ rõ mẫu được chấp nhận.

Ví dụ cho đầu mối phối hợp:

```text
Số điện thoại đầu mối phối hợp không hợp lệ.
Nhập số Việt Nam dạng 0912345678 hoặc số quốc tế dạng +84912345678 / +821012340001.
Không nhập số máy lẻ.
```

Bản ngắn dùng dưới field:

```text
Dùng 0912345678 hoặc định dạng quốc tế +[mã quốc gia][số thuê bao].
```

---

# 18. Hint trước khi có lỗi

Đặt hint ngắn dưới/tooltip tại phone field:

```text
Ví dụ: 0912345678 hoặc +84912345678
```

Quy tắc UI:

- Hint có thể hiện khi focus.
- Khi lỗi, thay hint bằng error đầy đủ.
- Không hiển thị cả hint dài và error dài cùng lúc.
- Placeholder chỉ là bổ trợ, không thay label/hint.
- Các phone field phải dùng cùng shared helper.

Áp dụng cho:

```text
Người đăng ký
Đầu mối chính
Đầu mối phối hợp từng campus
Chuyển giao đầu mối
Các form edit/resubmit tương ứng
```

Frontend và backend vẫn dùng cùng phone validation rule hiện tại:

```text
VN national
hoặc E.164 quốc tế
không extension
```

---

# PHẦN F — VALIDATION SUMMARY VÀ FOCUS LỖI

# 19. Khi submit có lỗi

Banner cuối form hiện có thể nói:

```text
Vui lòng điền đúng tất cả trường bắt buộc trước khi tiếp tục.
```

Phải bổ sung hành vi:

1. Mở campus card đầu tiên có lỗi.
2. Scroll tới field lỗi đầu tiên.
3. Focus field.
4. Field hiển thị message cụ thể.
5. Banner có số lượng lỗi nếu có thể:

```text
Còn 3 trường cần kiểm tra.
```

Không bắt người dùng tự cuộn toàn modal tìm field đỏ.

Phone invalid phải được focus đúng.

---

# PHẦN G — KIỂM THỬ

# 20. Frontend tests — public receipt

```text
1. Public OTP success không đóng modal.
2. Không chỉ render toast.
3. Receipt có requestCode/status/submittedAt/campusCount.
4. Có nút Xem lại thông tin đã gửi.
5. Submitted snapshot không bị reset.
6. Read-only summary hiển thị đúng form vừa gửi.
7. Copy request code hoạt động.
8. Public không gọi protected detail API.
9. Public không hiện direct protected-detail action.
10. Authenticated vẫn có Xem đơn vừa tạo.
11. Tạo đơn mới mới reset receipt/form.
12. Đóng receipt không restore draft hoàn tất.
```

# 21. Frontend tests — organization/quick-fill

```text
1. Operational organization dùng OrganizationCombobox.
2. Cho chọn organization có sẵn.
3. Cho nhập free text.
4. Không thay request-level partnerId.
5. Copy từ registrant đúng 4 field.
6. Copy từ primary contact đúng 4 field.
7. Copy chỉ tác động campus hiện tại.
8. Sau copy có thể sửa độc lập.
9. Destination có dữ liệu thì confirmation trước replace.
10. Draft restore giữ dữ liệu copy.
11. Không lặp badge “Đã chọn đối tác có sẵn” ở mọi field.
```

# 22. Frontend tests — counter/phone/error focus

```text
1. Empty blurred field không hiện 0/max.
2. Focus field hiện 0/max.
3. Blur dưới 80% làm counter ẩn.
4. Gần giới hạn counter vẫn hiện.
5. Vượt giới hạn counter đỏ và luôn hiện.
6. Error gọi đúng tên field.
7. Paste vượt giới hạn bị chặn submit.
8. Phone focus hiện ví dụ.
9. Phone invalid hiện mẫu được chấp nhận.
10. Submit scroll/focus phone lỗi đầu tiên.
11. VI/EN translation đầy đủ.
```

# 23. Backend/integration tests

```text
1. Verify success response đủ receipt fields.
2. Public response không chứa PII ngoài contract cho phép.
3. Operational organization max length khớp frontend.
4. Phone validation boundary khớp shared rule.
5. Invalid phone trả field-specific validation error.
6. Existing partner selection không bị thay bởi operational-contact selection.
7. Create/edit/resubmit nhận snapshot operational organization đúng.
```

# 24. Real-stack E2E

## Journey A — Public OTP success

```text
Public mở form
→ nhập đầy đủ
→ OTP đúng
→ receipt vẫn nằm trong modal
→ có mã đơn thật
→ mở Xem lại thông tin đã gửi
→ dữ liệu khớp form
→ DB chỉ có một request
```

## Journey B — Operational contact

```text
Chọn campus
→ bấm Dùng thông tin người đăng ký
→ bốn field được copy
→ đổi organization bằng combobox
→ submit
→ DB/read detail đúng snapshot
```

## Journey C — Validation UX

```text
Nhập phone sai
→ message có ví dụ hợp lệ
→ sửa thành E.164
→ pass

Focus phương tiện
→ hiện 0/2000
→ paste >2000
→ counter đỏ + message cụ thể
→ submit bị chặn
```

---

# PHẦN H — GATE

# 25. Gate bắt buộc

Giữ hoặc tăng baseline hiện tại:

```text
Backend build · Arch · Unit: 0 lỗi · 14/14 · 1072/1072
Integration: 636/636
Frontend: 659/659
Real-stack E2E: 35/35
git diff --check: sạch
```

Chạy:

```bash
dotnet build
dotnet test tests/PEMS.ArchitectureTests
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.IntegrationTests
npm run lint
npm run test
npm run build
git diff --check
```

Real-stack dùng disposable database.

Không test destructive flow trên `pems_db`.

---

# 26. Definition of Done

```text
[ ] Public OTP success có receipt cố định.
[ ] Modal không tự đóng sau public success.
[ ] Toast không còn là kết quả duy nhất.
[ ] Public xem lại được snapshot vừa gửi.
[ ] Authenticated xem được request detail.
[ ] Operational organization là combobox.
[ ] Vẫn cho nhập organization mới.
[ ] Có quick-fill từ registrant và primary contact.
[ ] Quick-fill là one-time copy, không auto-sync.
[ ] Không ghi đè destination không cảnh báo.
[ ] Badge đối tác chỉ hiện ở nơi có ý nghĩa.
[ ] Counter chỉ hiện khi focus/gần limit/error.
[ ] Vượt limit báo đúng tên field.
[ ] Không silently truncate.
[ ] Phone error có ví dụ định dạng hợp lệ.
[ ] Submit focus đúng field lỗi đầu tiên.
[ ] VI/EN đầy đủ.
[ ] Unit/integration/E2E xanh.
[ ] Không giảm gate.
```

---

# 27. Báo cáo cuối cùng

Báo cáo phải nêu:

```text
1. Root cause public receipt không render ở runtime.
2. Success callback/modal close trước và sau.
3. submittedSnapshot lifecycle.
4. Public/authenticated action matrix.
5. OrganizationCombobox reuse.
6. Badge policy.
7. Quick-fill semantics.
8. Counter visibility rules.
9. Phone validation messages.
10. Files changed.
11. Tests added.
12. Gate results.
13. Real-stack evidence.
14. Database impact.
15. Known limitations.
16. Resume point.
```

Không báo hoàn thành nếu real-stack public OTP success vẫn chỉ hiện toast hoặc modal vẫn đóng ngay.
