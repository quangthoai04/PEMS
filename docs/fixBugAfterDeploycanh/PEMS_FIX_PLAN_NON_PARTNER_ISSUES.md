# PEMS — Kế hoạch sửa các lỗi không liên quan đến đối tác

## 1. Thông tin tài liệu

- Repository: `quangthoai04/PEMS`
- Nhánh được kiểm tra: `Dev`
- Commit cơ sở: `bd460b6229ae2fecec3969ecaa476dad258bd7f2`
- Phạm vi: các lỗi tìm kiếm, validation form, đầu mối đoàn khách/biên bản, chuyển giai đoạn và theme giao diện.
- Không thuộc phạm vi hiện tại: toàn bộ lỗi tạo, gợi ý, liên kết, duyệt, từ chối và đồng bộ đối tác.

---

## 2. Mục tiêu

Đợt sửa này cần bảo đảm:

1. Kết quả tìm kiếm luôn giải thích được từ khóa khớp tại đâu.
2. Lỗi bắt buộc của custom control biến mất ngay sau khi người dùng chọn giá trị hợp lệ.
3. Đầu mối đoàn khách được nhận diện ổn định trong danh sách đoàn và biên bản, không thiếu và không trùng.
4. Sau khi chuyển sang `DURING_VISIT`, toàn bộ chức năng của tab “Trước tiếp khách” trở thành chỉ đọc.
5. Chính sách chuyển giai đoạn và thông báo T-6 thống nhất giữa frontend, backend và nội dung hiển thị.
6. PEMS luôn hiển thị Light Mode giống nhau trên mọi máy, không tự đổi màu theo theme hệ điều hành.

---

## 3. Danh sách lỗi trong phạm vi

| ID | Lỗi | Mức độ | Thành phần chính |
|---|---|---:|---|
| NP-01 | Tìm kiếm có kết quả nhưng thiếu dòng “Khớp tại” | P1 | Backend search/context + frontend i18n |
| NP-02 | Chọn quốc gia hoặc custom control hợp lệ nhưng lỗi bắt buộc vẫn còn | P1 | React Hook Form + custom controls |
| NP-03 | Đầu mối đoàn khách không chắc chắn xuất hiện trong biên bản hoặc có thể bị trùng | P1 | Data model + minute autofill/dedupe |
| NP-04 | Chuyển sang “Trong tiếp khách” nhưng nút tạo/sửa/xóa của tab trước vẫn hoạt động | P1 | VisitProcess state/capability |
| NP-05 | Logic T-6, nút chuyển giai đoạn và backend không thống nhất; thiếu confirm readonly | P1 | Transition policy + UX |
| NP-06 | UI tự chuyển đen trên máy dùng OS Dark Mode | P1 | Tailwind dark variant + partial theme |

---

# 4. Kế hoạch sửa chi tiết

## NP-01 — Kết quả tìm kiếm thiếu “Khớp tại”

### Hiện tượng

- Một số từ khóa trả về đúng đoàn khách nhưng không hiện dòng “Khớp tại”.
- Ví dụ: từ khóa khớp tên người đăng ký có thể trả về bản ghi nhưng không giải thích trường đã khớp.
- Từ khóa khớp tên đoàn hoặc một số trường khác lại hiển thị bình thường.

### Nguyên nhân

Điều kiện tìm kiếm trong `ViewGuestDelegationListQueryHandler` có tìm trên:

- Họ tên người đăng ký.
- Quốc tịch người đăng ký.
- Chức vụ người đăng ký.

Nhưng dữ liệu truyền sang `VisitSearchMatchContextBuilder` chưa có ba trường trên. Frontend `SearchMatchContexts` nhận mảng rỗng thì trả về `null`.

Nói cách khác:

```text
Search predicate có khớp
        nhưng
Match-context không biết trường nào khớp
        nên
UI không hiện “Khớp tại”
```

### Cách sửa

1. Bổ sung các field code ổn định:

```text
REGISTRANT_FULL_NAME
REGISTRANT_NATIONALITY
REGISTRANT_JOB_TITLE
```

2. Bổ sung nhãn VI/EN cho ba field code.
3. Truyền ba giá trị vào context builder ở cả:
   - Kết quả cấp `VisitInstance`.
   - Kết quả tổng hợp cấp `VisitRequest`.
4. Không để predicate tìm kiếm và context builder tiếp tục được khai báo độc lập. Tạo một định nghĩa/search projection dùng chung để khi thêm trường tìm kiếm mới thì match-context cũng được cập nhật.

### Files chính

```text
backend/PEMS.Application/Delegations/Queries/ViewGuestDelegationList/
  ViewGuestDelegationListQueryHandler.cs
  ViewGuestDelegationListDto.cs
  VisitSearchMatchContextBuilder.cs

frontend/pems-react/src/features/visit-request/components/
  SearchMatchContexts.tsx

frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json
frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json
```

### Acceptance criteria

- Khi keyword chỉ khớp tên người đăng ký, kết quả phải hiện “Khớp tại — Họ tên người đăng ký”.
- Khi chỉ khớp quốc tịch hoặc chức vụ người đăng ký, phải hiện đúng trường tương ứng.
- Khi có keyword và bản ghi được trả về vì keyword đó, `MatchedContexts` không được rỗng.
- Không trả raw value hoặc dữ liệu nhạy cảm trong match-context; chỉ trả stable field code và scope đã được phân quyền.
- Không làm lộ campus nằm ngoài phạm vi của người dùng.

### Tests bắt buộc

- Unit test cho context builder với từng searchable field.
- Integration test: mỗi trường tìm kiếm đều sinh ít nhất một match-context.
- Test role/scope để bảo đảm không lộ campus hoặc nội dung ngoài quyền xem.

---

## NP-02 — Validation không biến mất sau khi chọn giá trị hợp lệ

### Hiện tượng

1. Staff Leader bấm “Tôi là người đăng ký”.
2. Hồ sơ chưa có quốc gia nên form hiện lỗi “Quốc tịch không được để trống”.
3. Người dùng chọn một quốc gia hợp lệ.
4. Input đã có giá trị nhưng lỗi đỏ vẫn còn.

Lỗi tương tự có thể xuất hiện ở các custom control khác như tổ chức, combobox hoặc khoảng ngày.

### Nguyên nhân

Form đang dùng:

```ts
mode: 'onSubmit',
reValidateMode: 'onChange',
```

Nhưng thao tác autofill lại gọi:

```ts
form.setValue('registerInfo', profileData, {
  shouldValidate: true,
  shouldDirty: true,
});
```

`shouldValidate: true` tạo lỗi trước khi form được submit. Một số custom control chỉ gọi `field.onChange`, nên lỗi được tạo thủ công trước submit không được bảo đảm revalidate và xóa ngay.

### Cách sửa

Autofill không được kích hoạt validation toàn bộ block khi người dùng chưa submit:

```ts
form.setValue('registerInfo', profileData, {
  shouldDirty: true,
  shouldValidate: form.formState.isSubmitted,
});
```

Với custom control, khi field đang có lỗi thì chủ động trigger field đó sau khi cập nhật:

```ts
field.onChange(value);

if (form.getFieldState(fieldName).error) {
  void form.trigger(fieldName);
}
```

Không đổi toàn bộ form sang `mode: 'onChange'`, vì form dài và sẽ báo lỗi quá sớm khi người dùng mới bắt đầu nhập.

### Phạm vi audit custom controls

- `CountrySelect`.
- Organization combobox/select.
- Date/time range.
- Các select được bọc bằng `Controller`.
- Các control không dùng trực tiếp `register()` của React Hook Form.

### Files chính

```text
frontend/pems-react/src/features/visit-request/components/v2/
  VisitRequestFormV2.tsx
  CampusVisitCard.tsx

frontend/pems-react/src/features/visit-request/hooks/
  useVisitRequestFormV2.ts
```

### Acceptance criteria

- Autofill hồ sơ thiếu dữ liệu không được tạo inline error trước lần submit đầu tiên.
- Sau lần submit, chọn quốc gia hợp lệ phải xóa lỗi quốc gia ngay.
- Clear quốc gia sau submit phải hiện lại lỗi theo đúng validation rule.
- Hành vi tương tự áp dụng cho các custom control bắt buộc khác.
- Không có error message khi field đang chứa giá trị hợp lệ.

### Tests bắt buộc

- Autofill hồ sơ thiếu quốc gia trước submit.
- Submit form, sau đó chọn quốc gia và xác nhận lỗi biến mất.
- Clear lại quốc gia và xác nhận lỗi xuất hiện.
- Regression test cho organization combobox và date/time custom control.

---

## NP-03 — Đầu mối đoàn khách và biên bản chưa có stable identity

### Hiện tượng

- Đầu mối có trong phần thông tin đoàn nhưng không phải lúc nào cũng xuất hiện trong biên bản.
- Nếu đầu mối đồng thời được nhập trong danh sách khách thì họ xuất hiện thông qua guest member.
- Nếu đầu mối chỉ tồn tại trong phần “Đầu mối” thì minute autofill không tự thêm họ.
- Nếu cùng một người được nhập ở cả hai nơi, hệ thống phải so sánh bằng text và có nguy cơ trùng.

### Nguyên nhân

Minute autofill hiện lấy:

1. Host.
2. Người tham gia nội bộ đã chấp nhận.
3. Khách được liên kết với visit instance.

Đầu mối lại được lưu bằng snapshot riêng và chưa có quan hệ ổn định tới một `VisitGuestMember`.

### Business rule đề xuất

Đầu mối của từng cơ sở phải là một người trong đoàn khách của cơ sở đó.

UI nên cho phép:

```text
Chọn đầu mối từ danh sách đoàn
```

Nếu người dùng nhập một đầu mối mới chưa có trong danh sách, hệ thống cần:

1. Hỏi xác nhận thêm người này vào danh sách đoàn; hoặc
2. Tự thêm vào danh sách với thông báo rõ ràng.

Không giữ hai bản ghi độc lập mà không có quan hệ.

### Data model đề xuất

Thêm stable reference:

```text
operational_contact_guest_member_id
```

Vẫn giữ snapshot tên, email, điện thoại, chức vụ và đơn vị để phục vụ audit lịch sử, nhưng identity nghiệp vụ phải dựa trên `GuestMemberId`.

### Quy tắc chống trùng

Ưu tiên theo thứ tự:

1. Cùng `GuestMemberId` → chắc chắn là một người.
2. Cùng email chuẩn hóa hoặc số điện thoại chuẩn hóa → cảnh báo/xác nhận theo policy.
3. Cùng họ tên + chức vụ + đơn vị sau chuẩn hóa → yêu cầu xác nhận.
4. Không bao giờ tự gộp chỉ dựa trên họ tên.

Minute autofill và save minutes phải dùng cùng một dedupe service, không duy trì hai thuật toán độc lập ở frontend và backend.

### Files chính

```text
backend/PEMS.Application/Delegations/Minutes/
  MinuteAutoFill.cs

backend/PEMS.Application/Delegations/Minutes/Commands/
  CreateOrLockMinutesCommandHandler.cs
  SaveMinutesCommandHandler.cs

backend/PEMS.Infrastructure/Services/
  VisitRequestV2CreateService.cs
  VisitRequestV2EditService.cs

frontend/pems-react/src/features/visit-request/components/v2/
  CampusVisitCard.tsx

frontend/pems-react/src/pages/dashboard/visit/
  MinutesCard.tsx
```

### Acceptance criteria

- Mỗi đầu mối được liên kết với đúng một guest member của đúng visit instance.
- Tạo hoặc khóa biên bản luôn có đầu mối đúng một lần.
- Nếu đầu mối đã có trong danh sách khách, không tạo thêm bản ghi trùng.
- Nếu đầu mối chưa có trong danh sách, người dùng được thông báo trước khi thêm.
- Sửa snapshot đầu mối không làm mất stable identity.
- Không gộp nhầm hai người chỉ vì trùng tên.

### Tests bắt buộc

- Đầu mối đồng thời là guest member.
- Đầu mối chưa nằm trong danh sách đoàn.
- Hai người trùng tên nhưng khác email/đơn vị.
- Một người có cùng ID nhưng snapshot được chỉnh sửa.
- Minute autofill chạy lặp lại vẫn idempotent và không sinh bản ghi trùng.

---

## NP-04 — Chuyển stage nhưng tab trước tiếp khách vẫn sửa được

### Hiện tượng

Sau khi chuyển visit sang `DURING_VISIT`:

- Tab trước tiếp khách vẫn hiện nút lưu, cập nhật hoặc cấu hình.
- Người dùng bấm nút thì backend mới trả toast:

```text
Chỉ có thể cấu hình cảnh báo trong giai đoạn chuẩn bị.
```

Backend đang chặn đúng, nhưng frontend hiển thị sai capability.

### Nguyên nhân

Sau transition, frontend chỉ reload permissions. `detail.instanceStatus` không được reload khi `visitRequestId` và `visitInstanceId` không đổi.

Một số capability vẫn được tính từ `detail.instanceStatus`, dẫn đến:

```text
permissions.instanceStatus = DURING_VISIT
detail.instanceStatus      = BEFORE_VISIT (stale)
```

Frontend tiếp tục hiển thị control sửa dựa trên trạng thái cũ.

Ngoài ra, frontend hiện có chỗ cho phép cấu hình khi trạng thái là `ASSIGNED || BEFORE_VISIT`, trong khi backend preparation gate chỉ cho phép `BEFORE_VISIT`.

### Cách sửa

Tạo một nguồn trạng thái/capability chuẩn:

```ts
const instanceStatus =
  permissions?.instanceStatus ?? detail?.instanceStatus;

const canMutateBeforeVisit =
  permissions?.canEditBeforeVisit === true &&
  instanceStatus === 'BEFORE_VISIT';
```

Tất cả các mutation của tab trước tiếp khách phải dùng chung `canMutateBeforeVisit`:

- Lịch trình.
- Thành phần tham gia.
- Lời mời.
- Hậu cần.
- Cảnh báo.
- Ghi chú chuẩn bị.
- Các nút lưu/gửi/cập nhật/xóa.

Sau transition phải gọi một hàm refresh thống nhất:

```ts
await refreshProcessState();
closeAllEditModes();
```

`refreshProcessState()` phải cập nhật permissions, detail và các section liên quan bằng cùng trạng thái mới.

### Files chính

```text
frontend/pems-react/src/pages/dashboard/visit/
  VisitProcess.tsx

backend/PEMS.Application/Delegations/Queries/GetVisitProcessPermissions/
  GetVisitProcessPermissionsQueryHandler.cs

backend/PEMS.Application/Delegations/Common/
  VisitPreparationGate.cs
```

### Acceptance criteria

- Sau response transition thành công, UI khóa ngay mà không cần F5.
- Không còn nút tạo/sửa/xóa/lưu ở tab trước tiếp khách.
- Dữ liệu cũ vẫn xem được ở chế độ readonly.
- Không còn tình huống nút vẫn bấm được rồi backend mới báo toast sai stage.
- Trạng thái `ASSIGNED` không được phép dùng các mutation chỉ dành cho `BEFORE_VISIT`.
- Reload trang vẫn giữ đúng readonly state.

### Tests bắt buộc

- Component/integration test transition `BEFORE_VISIT → DURING_VISIT`.
- Test toàn bộ control quan trọng biến mất hoặc bị disable sau transition.
- Test không gọi API mutation khi capability false.
- Test reload trang ở `DURING_VISIT` vẫn readonly.

---

## NP-05 — Policy T-6 và confirm chuyển giai đoạn không thống nhất

### Hiện tượng

UI hiển thị:

```text
Đã hoàn tất công tác chuẩn bị. Có thể chuyển sang Trong tiếp khách từ ...
(6 giờ trước thời gian bắt đầu).
```

Nhưng người dùng vẫn có thể bấm chuyển sớm và backend vẫn có thể chấp nhận.

Thông báo khiến người dùng hiểu rằng T-6 là hard gate, trong khi runtime đang vận hành như informational notice.

### Nguyên nhân

- Domain transition policy có mô tả cửa sổ T-6.
- Frontend vẫn render nút chuyển sớm.
- Complete stage handler đã bỏ enforce thời gian.
- Comment/test hiện có cũng không thống nhất với hành vi thật của handler.
- Chưa có confirm nói rõ hậu quả readonly trước khi chuyển stage.

### Policy khuyến nghị

Chốt chính sách sau:

```text
Được phép chuyển sớm khi các điều kiện chuẩn bị đã hoàn tất.
T-6 chỉ là thời điểm khuyến nghị, không phải hard gate.
Bắt buộc confirm trước khi chuyển.
Sau khi chuyển, tab “Trước tiếp khách” chỉ còn xem.
```

Nếu product owner chọn hard gate T-6 thì phải triển khai phương án khác:

- Backend từ chối trước T-6.
- Frontend disable nút trước T-6.
- Capability API trả đúng lý do `VISIT_START_WINDOW_NOT_OPEN`.
- Chỉ hiện confirm khi đã đến thời điểm được phép chuyển.

Không được giữ mô hình lai.

### Modal xác nhận đề xuất

```text
Xác nhận chuyển sang “Trong tiếp khách”?

Sau khi chuyển giai đoạn, toàn bộ thông tin trong tab “Trước tiếp khách”
sẽ chỉ còn chế độ xem. Bạn sẽ không thể tạo, sửa hoặc xóa lịch trình,
thành phần tham gia, hậu cần, cảnh báo và ghi chú chuẩn bị.

[Hủy] [Xác nhận chuyển]
```

### Files chính

```text
frontend/pems-react/src/pages/dashboard/visit/
  VisitProcess.tsx

backend/PEMS.Domain/Policies/
  VisitStageTransitionPolicy.cs

backend/PEMS.Application/Delegations/Commands/CompleteVisitStage/
  CompleteVisitStageCommandHandler.cs

backend/PEMS.Application/Delegations/Queries/GetVisitProcessPermissions/
  GetVisitProcessPermissionsQueryHandler.cs
```

### Acceptance criteria

- Frontend, backend, capability và nội dung cảnh báo sử dụng cùng một policy.
- Chuyển stage luôn có confirm nêu rõ hậu quả readonly.
- Bấm Hủy không gọi API.
- Bấm xác nhận chỉ gửi một request transition.
- Sau thành công, toàn bộ tab trước tiếp khách chuyển readonly ngay.
- Không còn text “Có thể chuyển từ T-6” nếu hệ thống cho phép chuyển sớm và nội dung đó gây hiểu nhầm.
- Test/comment cũ mâu thuẫn với runtime phải được sửa hoặc xóa.

### Tests bắt buộc

- Chưa tới T-6 theo policy đã chốt.
- Đúng T-6.
- Sau T-6.
- Hủy confirm.
- Xác nhận transition thành công.
- API thất bại thì UI không chuyển giả sang readonly và vẫn hiển thị lỗi phù hợp.

---

## NP-06 — UI tự chuyển đen theo theme hệ điều hành

### Policy theme

Chốt:

```text
PEMS hiện tại = LIGHT ONLY
```

Điều này có nghĩa:

- Không phụ thuộc theme Windows/macOS.
- Không phụ thuộc `prefers-color-scheme`.
- Cùng một website phải hiển thị giống nhau trên mọi máy.
- Chưa bật Dark Mode cho đến khi có design system và test đầy đủ.

### Nguyên nhân

Frontend đang sử dụng Tailwind v4 và có các class `dark:*`, nhưng chưa override dark variant bằng class `.dark`.

Vì vậy `dark:*` có thể chạy theo:

```text
prefers-color-scheme: dark
```

Trong khi nhiều component chỉ có dark background/border nhưng thiếu dark text, hover, disabled hoặc form-control styling. Kết quả là UI rơi vào trạng thái “nửa light, nửa dark”.

### Phase A — Global root fix bắt buộc

Sửa:

```text
frontend/pems-react/src/index.css
```

Ngay sau Tailwind import thêm:

```css
@import "tailwindcss";

@custom-variant dark (&:where(.dark, .dark *));

:root {
  color-scheme: light;
}
```

Sau thay đổi:

```text
Trước: dark:* chạy theo OS preference
Sau:   dark:* chỉ chạy khi app chủ động thêm class .dark
```

PEMS hiện không thêm `.dark`, nên toàn bộ hệ thống được cố định ở Light Mode.

### Phase B — Audit `dark:*`

Search toàn frontend:

```text
dark:
```

Nhóm partial dark cần ưu tiên kiểm tra:

```text
frontend/pems-react/src/features/visit-request/components/
  VisitSafeEditModal.tsx
  VisitAmendmentSubmitModal.tsx
  VisitAmendmentPanel.tsx
  SearchMatchContexts.tsx

frontend/pems-react/src/pages/
  CampusDetailVisitPage.tsx
```

Trang có dark styling tương đối hoàn chỉnh nhưng sau global fix vẫn chạy Light Mode:

```text
frontend/pems-react/src/pages/identity/
  VisitContactInvitationPage.tsx
```

Không cần xóa toàn bộ `dark:*` ngay. Global fix chặn lỗi production trước; cleanup component có thể thực hiện dần.

### Phase C — Giữ native dark có chủ đích

Không xóa bừa những nơi có:

```text
[color-scheme:dark]
```

Ví dụ các input date trên nền navy tại:

```text
frontend/pems-react/src/pages/dashboard/admin/
  SecurityMonitoring.tsx
  AuditLogManagement.tsx
```

Đây là styling cục bộ có chủ đích và không phải app-level Dark Mode.

### Phase D — Test OS/browser theme

Test cả:

```text
colorScheme = light
colorScheme = dark
```

Expected UI của PEMS phải giống nhau.

Các màn tối thiểu:

1. Dashboard visit list.
2. Visit detail.
3. Sửa nhanh.
4. Amendment modal.
5. Amendment panel.
6. Contact invitation page.

### Phase E — Test native controls

Kiểm tra:

- `input`.
- `select`.
- `textarea`.
- `input[type=date]`.
- `input[type=datetime-local]`.
- Scrollbar nếu trình duyệt hỗ trợ theme theo `color-scheme`.

Sau `:root { color-scheme: light; }`, control thông thường phải hiển thị Light. Vùng có `[color-scheme:dark]` cục bộ vẫn được giữ tối.

### Phase F — Regression guard

Quy tắc review:

```text
Không thêm dark:* vào component dashboard nếu component không hỗ trợ đầy đủ:

- background
- text
- border
- hover/focus
- disabled
- form controls
```

CI/static audit nên sử dụng allowlist thay vì fail với toàn bộ `dark:*` hiện hữu. Script phải báo các file mới phát sinh ngoài allowlist để reviewer kiểm tra.

### Acceptance criteria

- `window.matchMedia('(prefers-color-scheme: dark)').matches === true` nhưng PEMS vẫn hiển thị Light.
- Windows Light và Windows Dark cho giao diện PEMS giống nhau.
- Chrome Light và Chrome dark preference cho giao diện PEMS giống nhau.
- Sửa nhanh không có nền/input màu đen.
- Amendment modal/panel không tự chuyển đen.
- Text không mất contrast.
- Native controls không đổi màu ngoài ý muốn.
- Các vùng `[color-scheme:dark]` có chủ đích vẫn hoạt động đúng.

Lưu ý: Definition of Done này áp dụng cho OS/browser color preference, không bao gồm extension bên thứ ba như Dark Reader có khả năng inject CSS vào trang.

---

# 5. Thứ tự triển khai đề xuất

## Wave 1 — Quick production fixes

1. Thêm global Light-only policy trong `index.css`.
2. Sửa autofill validation và revalidation của custom controls.
3. Bổ sung các search match-context còn thiếu.
4. Chạy lint, unit test và build frontend.

## Wave 2 — Workflow state và readonly

1. Chốt policy T-6 với product owner.
2. Thêm confirm chuyển stage.
3. Chuẩn hóa `instanceStatus` và `canMutateBeforeVisit`.
4. Refresh permissions + detail sau transition.
5. Khóa toàn bộ mutation của tab trước tiếp khách.
6. Thêm transition/readonly tests.

## Wave 3 — Stable identity cho đầu mối

1. Chốt UX chọn đầu mối từ danh sách đoàn.
2. Bổ sung stable guest-member reference.
3. Đồng bộ create/edit service.
4. Đồng bộ minute autofill/save dedupe.
5. Migration và backfill dữ liệu nếu cần.
6. Thêm idempotency/dedupe tests.

## Wave 4 — Full regression

1. Test OS Light/Dark.
2. Test native controls.
3. Test search theo từng field.
4. Test validation custom controls.
5. Test transition và readonly.
6. Test đầu mối/biên bản không trùng.

---

# 6. Definition of Done chung

Chỉ coi đợt sửa hoàn tất khi tất cả điều kiện sau đạt:

```text
[ ] Tất cả kết quả tìm kiếm do keyword đều có “Khớp tại”.
[ ] Chọn giá trị hợp lệ trên custom control xóa lỗi ngay.
[ ] Autofill không tạo lỗi bắt buộc trước submit.
[ ] Đầu mối xuất hiện đúng một lần trong biên bản.
[ ] Không gộp nhầm hai người trùng tên.
[ ] Sau transition, tab trước tiếp khách readonly ngay không cần F5.
[ ] Không còn control giả cho phép bấm rồi mới nhận toast sai stage.
[ ] Policy T-6 thống nhất giữa UI, permission và backend.
[ ] Chuyển stage có confirm nêu rõ hậu quả readonly.
[ ] Windows/Chrome Light và Dark preference hiển thị PEMS giống nhau.
[ ] Native form controls giữ Light Mode ngoài các vùng dark cục bộ có chủ đích.
[ ] Không có regression test thất bại.
```

### Lệnh kiểm tra cuối

Backend:

```bash
dotnet test
```

Frontend:

```bash
npm run lint
npm run test:unit
npm run build
```

Nếu repository đã cấu hình Playwright/E2E:

```bash
npm run test:e2e
```

---

# 7. Out of scope — để xử lý ở đợt sau

Đợt này không triển khai hoặc thay đổi các nội dung sau:

- Matching/gợi ý đối tác.
- Đối tác `REJECTED` vẫn được đề xuất hoặc liên kết.
- Phân biệt trạng thái hồ sơ đối tác và trạng thái liên kết.
- Lưu `organizationPartnerId` cho từng thành viên.
- Tự liên kết nhiều thành viên cùng một tổ chức.
- Tạo hoặc cập nhật thông tin liên hệ của đối tác.
- Nút “Tạo/liên kết đối tác” trong biên bản.
- Kiểm tra scope trong API liên kết guest–partner.

Các nội dung trên phải được gom thành một kế hoạch partner riêng để triển khai sau, tránh trộn migration và business rule của Partner module vào đợt fix hiện tại.
