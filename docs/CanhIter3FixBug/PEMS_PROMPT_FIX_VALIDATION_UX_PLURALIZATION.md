# PEMS — Prompt triển khai vá lỗi Validation UX + Pluralization trên toàn hệ thống

## Mục tiêu

Rà soát và sửa **đúng root cause** cho nhóm lỗi có cùng bản chất:

1. Form không submit được vì validation nhưng **người dùng không biết field nào sai**.
2. Chỉ hiện banner/toast chung chung trong khi field lỗi không đỏ, không có message inline, hoặc lỗi nằm ngoài vùng nhìn thấy.
3. Nút bị `disabled` vì thiếu dữ liệu bắt buộc nhưng UI không giải thích rõ vì sao.
4. Backend trả `errors` / `errorCode` nhưng frontend không map đúng về field.
5. Các modal/form dài không tự scroll/focus tới field lỗi đầu tiên.
6. Chuỗi tiếng Anh kiểu `change(s)`, `error(s)`, `campus(es)`, `place(s)` phải được thay bằng pluralization đúng của i18next.

> Không được chỉ vá riêng màn “Đề xuất thay đổi”. Phải kiểm tra các flow cùng pattern trong code hiện tại và sửa theo một chuẩn thống nhất.

---

# 0. NGUYÊN TẮC BẮT BUỘC TRƯỚC KHI CODE

1. Đọc **working tree hiện tại** trước:
   - `git status`
   - `git diff`
   - không được giả định code trên GitHub là mới nhất nếu local đang có thay đổi chưa commit.
2. **Không revert / ghi đè** các fix đã làm trước đó, đặc biệt:
   - one-door operational contact model;
   - Quick Edit không còn sửa contact profile;
   - Amendment contact profile read-only;
   - Manage Contact Role là cửa duy nhất sửa profile/identity;
   - durable contact-member reference;
   - partner combobox / organizationPartnerId;
   - layout modal Amendment đã mở rộng;
   - các test mới đang có.
3. Không sửa business rule nếu không cần thiết.
4. Không biến validation client thành nguồn chân lý duy nhất:
   - backend vẫn phải validate;
   - frontend phải phản ánh rõ validation backend.
5. Không parse câu chữ backend để quyết định logic nếu đã có `errorCode`/field path ổn định.
6. Không báo cáo “pass” nếu chưa chạy thật.

---

# 1. P0 — SỬA VALIDATION UX CỦA AMENDMENT

## File trọng tâm

- `frontend/pems-react/src/features/visit-request/components/VisitAmendmentSubmitModal.tsx`
- API/type liên quan trong:
  - `frontend/pems-react/src/features/visit-request/api/visitRequestV2Api.ts`
- i18n:
  - `frontend/pems-react/src/shared/i18n/locales/vi/visitRequestV2.json`
  - `frontend/pems-react/src/shared/i18n/locales/en/visitRequestV2.json`
- test của amendment.

## Hiện tượng cần loại bỏ

User bấm **Submit proposal** nhưng:
- modal chỉ hiện banner kiểu:
  - `Please fix the highlighted fields before submitting the proposal.`
- nhưng **không có field nào được highlight rõ**;
- lỗi có thể nằm ở row member, nationality, organization, contact-picker, reason, schedule...
- modal dài nên lỗi có thể nằm ngoài viewport.

## Yêu cầu

### 1.1. Mỗi validation failure phải có nơi hiển thị cụ thể

Với mọi field có validation:
- border đỏ;
- `aria-invalid=true`;
- message đỏ ngay dưới field;
- `aria-describedby` trỏ tới error message.

Áp dụng cho:
- delegation name;
- visit type / visitTypeOther;
- start/end;
- working language;
- purpose;
- working content;
- guest rows;
- support rows;
- organization;
- nationality;
- contact-member picker;
- reason;
- mọi field khác thực sự được validate.

### 1.2. Row member phải biết chính xác field nào lỗi

Không chỉ hiện:
> Guest list has an error

Mà phải chỉ rõ:
- Guest #2 → Full name;
- Guest #2 → Organization;
- Guest #2 → Nationality;
- Support #1 → Job title;
...

### 1.3. Submit invalid phải tự điều hướng tới lỗi đầu tiên

Khi submit fail client-side:

1. xác định field lỗi đầu tiên theo thứ tự UI;
2. nếu section đang nằm ngoài vùng scroll → scroll vào;
3. focus control tương ứng nếu control có thể focus;
4. giữ footer Submit/Cancel cố định như hiện tại.

Không được chỉ hiện banner cuối modal rồi bắt user tự tìm.

### 1.4. Banner tổng chỉ là summary

Có thể giữ banner:
> Có N trường cần kiểm tra.

Nhưng banner **không được thay thế inline errors**.

Nên cho phép click/trigger focus lỗi đầu tiên nếu phù hợp.

### 1.5. Backend field errors

Nếu backend trả:

```json
{
  "errors": {
    "Some.Field.Path": ["..."]
  }
}
```

phải:
- map path backend → field frontend;
- `setError` / state error đúng field;
- scroll/focus tới field đầu tiên;
- chỉ fallback sang generic banner nếu **không map được bất kỳ field nào**.

Nếu backend trả stable `errorCode`, ưu tiên dùng i18n theo code thay vì raw message.

---

# 2. P0 — MANAGE THE CONTACT ROLE

## File trọng tâm

- `frontend/pems-react/src/features/visit-request/components/ContactIdentityActions.tsx`
- operational-contact API/types
- backend validators/handlers:
  - `backend/PEMS.Application/Delegations/Commands/OperationalContact/OperationalContactContracts.cs`
  - handler tương ứng.

## Business rule PHẢI GIỮ

Đây là **cửa duy nhất** sửa contact profile/identity.

Không đưa field contact profile trở lại Quick Edit hoặc Amendment.

## Vấn đề

Backend validate:
- FullName required/max;
- Organization max;
- JobTitle required/max;
- Phone max;
- Email required/format/max;
- Reason max.

Frontend hiện xử lý inline tốt nhất cho Email, nhưng chưa có field-error model đồng đều cho tất cả field.

## Yêu cầu

### 2.1. Tạo field error state chuẩn

Ví dụ:

```ts
type ContactFieldErrors = Partial<Record<
  'fullName' | 'organization' | 'jobTitle' | 'phone' | 'email' | 'reason',
  string
>>;
```

Hoặc dùng React Hook Form nếu phù hợp với kiến trúc hiện tại.

### 2.2. Client validation

Trước API:
- fullName required;
- jobTitle required;
- email required + format;
- max lengths đồng bộ backend;
- reason max length;
- phone theo rule hiện tại nếu có shared validator.

Không tự thêm business rule backend không có.

### 2.3. Backend validation mapping

Nếu API trả FluentValidation `errors`:
- map `FullName` → `fullName`;
- `Organization` → `organization`;
- `JobTitle` → `jobTitle`;
- `Phone` → `phone`;
- `Email` → `email`;
- `Reason` → `reason`.

Không đẩy tất cả thành toast generic.

### 2.4. UX

Mỗi field lỗi:
- đỏ;
- message ngay dưới;
- focus field lỗi đầu tiên.

Toast chỉ dùng cho:
- lỗi nghiệp vụ toàn cục;
- conflict;
- network;
- lỗi không gắn được vào field.

### 2.5. Organization

Nếu hiện tại Manage Contact vẫn là free-text vì contact không có `organizationPartnerId`, không tự ý đổi schema/business relation.

Có thể giữ free-text nếu đúng business rule hiện tại.

---

# 3. P0 — HOST TRANSFER

## File

- `frontend/pems-react/src/features/visit-request/components/VisitHostTransferModal.tsx`

## Vấn đề

Hiện có dạng:

```ts
const canSubmit =
  selectedId !== null &&
  reason.trim().length > 0 &&
  !busy;
```

và:

```tsx
disabled={!canSubmit}
```

User có thể không hiểu tại sao Submit bị disable.

## Yêu cầu

- `New host` hiển thị required marker `*`.
- `Reason` hiển thị required marker `*`.
- Có helper/error rõ:
  - `Please select a new Host.`
  - `Please enter a reason.`
- Không cần spam lỗi ngay khi modal vừa mở.
- Sau khi user cố submit hoặc đã tương tác với field, hiển thị inline error.
- Khi chọn host / nhập reason đúng → error clear ngay.
- Backend conflict 409 vẫn xử lý riêng như hiện tại.
- Nếu backend trả validation field → map về đúng control nếu có thể.

Có thể:
- để nút enabled và validate khi click;
hoặc
- vẫn disabled nhưng **phải có UX giải thích rõ**.

Ưu tiên cách ít gây “nút chết không lý do”.

---

# 4. P1 — PARTNER CONTACT CREATE / EDIT

## File

- `frontend/pems-react/src/pages/dashboard/partners/PartnerDetail.tsx`

## Vấn đề

Hiện có pattern:

```ts
if (!id || !cName.trim()) return;
```

và:

```tsx
disabled={!cName.trim() || busy}
```

Backend/API error chủ yếu đi toast.

## Yêu cầu

- Full name required:
  - có dấu `*`;
  - có inline message;
  - không silent return.
- Nếu Email/Phone/JobTitle/Department có validation backend:
  - map về đúng field.
- Không đóng modal nếu save fail.
- Giữ toàn bộ dữ liệu user đã nhập.
- Focus field lỗi đầu tiên.
- Toast generic chỉ cho lỗi không map được.

---

# 5. P1 — CREATE PARTNER FROM PARTICIPANT

## File

- `frontend/pems-react/src/features/partners/components/CreatePartnerFromParticipantModal.tsx`

## Những gì đang tốt — PHẢI GIỮ

- duplicate-partner candidate;
- suggestion panel;
- link existing;
- conflict → scroll lên candidate;
- không tạo trùng một cách mù quáng.

## Vấn đề

Validation thường gom vào một `error` box chung.

## Yêu cầu

- Tên đối tác required → inline error tại input.
- Các field có backend validation như:
  - country;
  - city;
  - website;
  - address;
  - description;
  - partner type;
  phải map về field nếu backend trả path.
- Duplicate business conflict vẫn xử lý theo candidate flow hiện tại, **không biến thành red field error** nếu đó là guided flow.
- Không mất dữ liệu khi submit fail.

---

# 6. P1 — EDIT PENDING / RESUBMIT VISIT

## Files

- `frontend/pems-react/src/pages/dashboard/visit/EditVisitRequestV2Page.tsx`
- `frontend/pems-react/src/pages/dashboard/visit/EditPendingCampusV2Page.tsx`

## Hiện tại

Code đã có:
- React Hook Form;
- Zod;
- backend path mapping;
- `form.setError`.

## Vấn đề còn cần verify

Sau khi set error:
- section/card có mở không?
- có scroll tới field không?
- có focus đúng field không?
- server-only error có visible ngay không?

## Yêu cầu

Dùng cùng chuẩn với Create V2:

1. map server error;
2. mở đúng campus/card;
3. scroll tới lỗi đầu tiên;
4. focus control;
5. error inline;
6. summary chỉ bổ trợ.

Không duplicate một cơ chế hoàn toàn mới nếu có thể reuse helper.

---

# 7. P1 — CHUẨN HÓA HELPER VALIDATION NAVIGATION

Kiểm tra xem hiện tại có thể tái dùng:

- `formErrorNavigation.ts`
- helper mapping server path;
- shared `FormField`;
- error summary component.

Nếu logic đang lặp ở:
- Create;
- Edit;
- Pending Campus;
- Amendment;

hãy refactor vừa đủ thành helper/shared utility.

Không over-engineer.

Mục tiêu là mọi form dài có cùng behavior:

```text
submit
→ validate
→ set errors
→ open containing section
→ scroll
→ focus
→ inline message
```

---

# 8. P1 — FIX PLURALIZATION `(...s)`

## Audit toàn bộ i18n frontend

Tìm các pattern:

```text
(s)
(es)
error(s)
change(s)
campus(es)
place(s)
member(s)
guest(s)
participant(s)
```

Đặc biệt trong:

- `visitRequestV2.json`
- các namespace visitor/visit khác.

## Không được dùng

```json
"applied": "Applied {{count}} change(s)."
```

## Phải dùng plural của i18next

Ví dụ EN:

```json
"appliedChange_one": "Applied {{count}} change.",
"appliedChange_other": "Applied {{count}} changes."
```

Hoặc đúng naming convention phiên bản i18next đang dùng trong repo.

Tương tự:

```text
1 error
2 errors

1 campus
2 campuses

1 place
2 places
```

## Tiếng Việt

Có thể dùng một câu không đổi theo số lượng:

```text
Đã áp dụng {{count}} thay đổi.
Có {{count}} lỗi cần kiểm tra.
```

Không thêm `(s)` vào VI.

---

# 9. RÀ SOÁT CÁC FORM KHÁC CÙNG PATTERN

Sau khi sửa các file ưu tiên, search toàn frontend cho các pattern:

```tsx
disabled={!somethingRequired}
```

```ts
if (!required.trim()) return;
```

```ts
setError('generic...')
```

```ts
toast.error(...)
```

trong submit handler có form fields.

Tập trung vào các flow nghiệp vụ quan trọng:

- reject/decline modal;
- logistics response;
- host/participant invitation;
- partner management;
- account management;
- email/template forms;
- news forms;
- agenda/minutes;
- department personnel;
- campus management.

Không sửa lan man.

Chỉ đưa vào patch nếu có đủ bằng chứng:

> field bị backend/client validate nhưng UI không cho user biết field nào cần sửa.

Mỗi phát hiện phải ghi:
- file;
- line/function;
- validation rule;
- hiện tại UX ra sao;
- fix cần làm.

---

# 10. ACCESSIBILITY BẮT BUỘC

Với tất cả field lỗi:

```tsx
aria-invalid={!!error}
aria-describedby={error ? errorId : hintId}
```

Error text nên có:
- `role="alert"` khi phù hợp;
- hoặc được nối bằng `aria-describedby`.

Label:
- dùng `htmlFor`;
- không lồng `<label>` trong `<label>`;
- required marker phải đọc được hợp lý.

Modal:
- không tạo horizontal scroll không cần thiết;
- footer action phải luôn truy cập được;
- focus không bị mất sau validation.

---

# 11. I18N

Mọi text mới:
- VI + EN;
- không hardcode riêng một ngôn ngữ trong Visitor-visible screen;
- parity key VI/EN phải pass.

Backend raw message không được làm nguồn hiển thị chính khi UI đang English nếu có stable code.

---

# 12. TEST BẮT BUỘC

## Amendment

Viết test ít nhất cho:

1. invalid guest row → đúng field đỏ;
2. invalid support row;
3. missing nationality;
4. missing organization;
5. invalid schedule;
6. missing reason nếu required;
7. lỗi ngoài viewport → scroll/focus;
8. backend field error → đúng control;
9. generic backend error → banner;
10. sửa field → error clear;
11. không submit API khi client invalid.

## Contact Role

1. FullName empty;
2. JobTitle empty;
3. invalid Email;
4. max-length backend response;
5. server `errors.Email`;
6. server `errors.FullName`;
7. business conflict vẫn toast/message riêng;
8. failed save không mất form;
9. first-invalid focus.

## Host Transfer

1. no host;
2. no reason;
3. error clear khi user sửa;
4. valid submit;
5. 409 conflict giữ behavior cũ.

## Partner Contact

1. empty name;
2. server email validation;
3. failed save giữ input.

## Edit Pending

1. backend campus field error;
2. card tự mở;
3. scroll/focus đúng field.

## Plural

Test ít nhất:

```text
count=0
count=1
count=2
```

cho các key đã đổi.

---

# 13. GATES PHẢI CHẠY

Frontend:

```bash
npm run lint
npx tsc --noEmit
npm run test:unit
npm run build
```

Nếu repo có suite scoped phù hợp thì chạy thêm suite visit/partner/account liên quan.

Backend nếu có thay đổi backend:

```bash
dotnet build
dotnet test
```

Integration tests phải chạy theo đúng convention disposable DB trong repo; không tự build binary ra ngoài repo nếu test infrastructure cần dò repository root.

---

# 14. KHÔNG ĐƯỢC LÀM

- Không bỏ validation để “submit được”.
- Không đổi required → optional chỉ để hết lỗi.
- Không tự catch rồi bỏ qua error.
- Không chỉ thêm toast generic.
- Không chỉ sửa banner mà không sửa inline error.
- Không hardcode field index kiểu “guest row 0”.
- Không assume mọi 400 là cùng một lỗi.
- Không parse raw backend Vietnamese message nếu có code/path.
- Không đưa contact profile trở lại Quick Edit/Amendment.
- Không phá durable contact-member relation.
- Không làm mất `organizationPartnerId`.
- Không sửa workflow approval/identity ngoài phạm vi.
- Không commit khi chưa được yêu cầu.

---

# 15. KẾT QUẢ BÁO CÁO CUỐI CÙNG

Sau khi làm xong, báo cáo theo đúng bằng chứng:

## A. Root cause

Mỗi lỗi:
- nguyên nhân kỹ thuật thật;
- file/function gây lỗi.

## B. Files changed

Liệt kê toàn bộ file thực sự đổi.

## C. Behavior before/after

Ví dụ:

```text
Before:
Submit → banner chung → không biết field sai.

After:
Submit → Guest #2 / Nationality đỏ → modal scroll tới row → focus field → sửa xong error biến mất.
```

## D. Validation matrix

| Flow | Client invalid | Backend field invalid | Global business error | Focus/scroll |
|---|---|---|---|---|
| Amendment | PASS/FAIL | PASS/FAIL | PASS/FAIL | PASS/FAIL |
| Contact Role | ... | ... | ... | ... |
| Host Transfer | ... | ... | ... | ... |
| Partner Contact | ... | ... | ... | ... |
| Pending Edit | ... | ... | ... | ... |

## E. Pluralization

Liệt kê các key `(s)/(es)` đã bỏ và test count 1/2.

## F. Test evidence

Chỉ ghi con số chạy thật:

```text
Frontend: X/X pass
Backend unit: X/X pass
Integration: X/X pass
Build: PASS
Typecheck: PASS
Lint: PASS
```

Nếu test nào không chạy được:
- nêu exact command;
- exact error;
- không gọi là PASS.

## G. Remaining risks

Chỉ ghi vấn đề thực sự chưa verify.

---

# Definition of Done

Patch chỉ được coi là hoàn thành khi:

- Không còn case ưu tiên mà form “không submit được nhưng không biết field nào sai”.
- Backend validation có thể được map về đúng field khi có field path.
- Field lỗi đầu tiên được đưa vào viewport/focus ở form dài.
- Không còn plural UI kiểu `change(s)`, `error(s)`, `campus(es)`, `place(s)` trong phạm vi audit.
- One-door contact business rule vẫn nguyên vẹn.
- Không mất partner/contact/member stable identity.
- VI/EN parity pass.
- Test + build thực chạy pass.
- Không commit nếu chưa được yêu cầu.
