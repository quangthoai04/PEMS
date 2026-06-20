# PROMPT_AUDIT_FIX_UC17_FRONTEND_BACKEND_VALIDATION_AND_SUBMIT_ERRORS

## Mục tiêu

Audit và sửa toàn bộ validation của **UC-17 Public Visit Request Form** ở cả **Frontend** và **Backend**, đồng thời debug lỗi submit:

```text
POST /api/visit-requests/initiate 400 Bad Request
UI báo: Có lỗi xảy ra khi gửi đơn. Vui lòng thử lại.
```

Yêu cầu chính:

```text
1. Frontend validate đẹp, không hiện lỗi đỏ ngay khi mới mở form.
2. Backend validate lại đầy đủ, không tin dữ liệu từ frontend.
3. Rule Liên cơ sở / Một cơ sở phải đồng bộ FE-BE.
4. Trùng cơ sở phải bị chặn ở cả FE và BE.
5. Time overlap giữa các cơ sở khác nhau phải hiện confirm ở FE; nếu có field xác nhận thì BE cũng nên check.
6. Payload FE gửi lên phải khớp DTO Backend và SQL full mới nhất.
7. Nếu backend trả lỗi cụ thể, frontend phải hiển thị lỗi cụ thể, không chỉ generic.
8. Không đưa lại CCCD/CMND/passportId.
9. Guest email vẫn required.
10. Registrant email dùng OTP; contact email dùng tạo/link VISITOR account.
```

---

## 0. Bối cảnh hiện tại

Flow UC-17 thật đang dùng:

```text
POST /api/visit-requests/initiate
POST /api/visit-requests/verify
POST /api/visit-requests/resend-otp
```

Các quyết định nghiệp vụ đã chốt:

```text
- Email người đăng ký form = registrantEmail = dùng để nhận OTP.
- Thông tin đầu mối liên hệ = contactPoint = dùng để tạo/link tài khoản VISITOR.
- Nếu tick “Tôi cũng là đầu mối liên hệ” thì copy registerInfo sang contactPoint.
- Dù auto-fill, account VISITOR vẫn luôn tạo/link từ contactPoint.email.
- Guest email vẫn required.
- Không có Số HC/CMND / CCCD / passportId trong SQL và payload.
- MULTI_CAMPUS / Liên cơ sở phải có ít nhất 2 cơ sở khác nhau.
- Không được chọn trùng cơ sở.
- Time overlap giữa các cơ sở khác nhau không phải lỗi cứng, nhưng cần confirm khi bấm Tiếp theo.
```

---

## 1. Source of truth

Phải đối chiếu với:

```text
database/scripts/pems_full(3).sql
```

Bảng cần check:

```text
visit_requests
visit_request_campuses
visit_guest_members
otp_tokens
users
campuses
```

Không sửa SQL để chiều frontend nếu SQL đã đúng.

---

# PHẦN A — DEBUG LỖI SUBMIT 400

## A1. Xem Network trước khi sửa

Mở DevTools → Network → chọn request lỗi:

```text
POST /api/visit-requests/initiate
```

Ghi lại:

```text
[ ] Request URL
[ ] Request Payload
[ ] HTTP status
[ ] Response body
[ ] errorCode
[ ] message
[ ] errors
[ ] traceId
```

Không được sửa mò nếu chưa xem response thật.

---

## A2. Frontend không được nuốt lỗi backend thành generic

Search frontend:

```text
Có lỗi xảy ra khi gửi đơn
submitError
catch
onSubmit
initiateVisitRequest
verifyVisitRequest
visitRequestApi
```

Nếu code đang kiểu:

```ts
catch {
  setSubmitError("Có lỗi xảy ra khi gửi đơn. Vui lòng thử lại.");
}
```

thì sửa để lấy lỗi cụ thể từ backend.

Helper đề xuất:

```ts
import axios from "axios";

export function getApiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as any;

    if (typeof data?.message === "string" && data.message.trim()) {
      return data.message;
    }

    if (data?.errors) {
      const values = Object.values(data.errors);
      const first = values.flat?.()[0];
      if (typeof first === "string") return first;
    }

    if (typeof data?.errorCode === "string") {
      return data.errorCode;
    }
  }

  return "Có lỗi xảy ra khi gửi đơn. Vui lòng thử lại.";
}
```

Trong catch:

```ts
catch (error) {
  console.error("UC-17 submit/initiate failed", error);
  setSubmitError(getApiErrorMessage(error));
}
```

Acceptance:

```text
[ ] Backend trả message cụ thể → UI hiện message cụ thể.
[ ] Chỉ dùng generic nếu backend không trả message/errors.
```

---

# PHẦN B — VALIDATION FRONTEND

## B1. Không hiện lỗi đỏ khi mới mở form

Không được gọi:

```ts
trigger("visits")
```

trong `useEffect` khi form mới mount hoặc khi `visits` thay đổi.

Nếu có đoạn này thì phải sửa:

```ts
React.useEffect(() => {
  form.setValue("timeOverlapConfirmed", false);
  void trigger("visits");
}, [visitMode, visits, trigger]);
```

Sửa thành chỉ reset confirm, không validate:

```ts
React.useEffect(() => {
  form.setValue("timeOverlapConfirmed", false, {
    shouldValidate: false,
    shouldDirty: false,
    shouldTouch: false,
  });
}, [visitMode, JSON.stringify(visits)]);
```

Hoặc reset confirm trong từng handler `onChange`, `addVisit`, `removeVisit`.

---

## B2. Chỉ hiện lỗi khi touched hoặc bấm Tiếp theo

Ở component cha, thêm state:

```ts
const [stepAttempted, setStepAttempted] = useState<Record<number, boolean>>({});
```

Khi bấm Tiếp theo:

```ts
setStepAttempted((prev) => ({
  ...prev,
  [currentStep]: true,
}));
```

Truyền vào `VisitInfoSection`:

```tsx
<VisitInfoSection
  form={form}
  visitFields={visitFields}
  showErrors={!!stepAttempted[1]}
/>
```

Trong `VisitInfoSection`:

```ts
const shouldShowStartError =
  showErrors || !!touchedFields.visits?.[index]?.startDatetime;

const shouldShowEndError =
  showErrors || !!touchedFields.visits?.[index]?.endDatetime;

const startHasError = shouldShowStartError && !!slotErrors?.startDatetime;
const endHasError = shouldShowEndError && !!slotErrors?.endDatetime;
```

Không dùng trực tiếp `slotErrors?.startDatetime` để tô đỏ ngay từ đầu.

---

## B3. Rule visitMode / campuses ở frontend

Rule đúng:

```text
visitMode = single:
- đúng 1 campus.

visitMode = multiple:
- ít nhất 2 campus.
- các campus phải khác nhau.
```

Message:

```text
Single sai:
Yêu cầu một cơ sở chỉ được chọn đúng 1 cơ sở.

Multi chỉ có 1 campus:
Yêu cầu liên cơ sở cần ít nhất 2 cơ sở. Vui lòng thêm cơ sở thứ hai hoặc đổi sang Một cơ sở.

Multi trùng campus:
Không được chọn trùng cơ sở trong yêu cầu liên cơ sở. Vui lòng chọn cơ sở khác.
```

Ưu tiên lỗi:

```text
1. Trùng cơ sở.
2. Chưa đủ 2 cơ sở.
```

Với case:

```text
Dòng 1 = Hà Nội
Dòng 2 = Hà Nội
```

phải báo:

```text
Không được chọn trùng cơ sở...
```

không báo:

```text
Yêu cầu liên cơ sở cần ít nhất 2 cơ sở...
```

Zod `superRefine` đề xuất:

```ts
.superRefine((data, ctx) => {
  const codes = data.visits
    .map((v) => v.campus?.trim())
    .filter(Boolean);

  const distinct = new Set(codes);
  const hasDuplicateCampus = codes.length !== distinct.size;

  if (data.visitMode === "multiple") {
    if (hasDuplicateCampus) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["visits"],
        message:
          "Không được chọn trùng cơ sở trong yêu cầu liên cơ sở. Vui lòng chọn cơ sở khác.",
      });
    } else if (distinct.size < 2) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["visits"],
        message:
          "Yêu cầu liên cơ sở cần ít nhất 2 cơ sở. Vui lòng thêm cơ sở thứ hai hoặc đổi sang Một cơ sở.",
      });
    }
  }

  if (data.visitMode === "single" && distinct.size !== 1) {
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: ["visits"],
      message: "Yêu cầu một cơ sở chỉ được chọn đúng 1 cơ sở.",
    });
  }
});
```

---

## B4. Time overlap confirm ở frontend

Nếu các campus khác nhau nhưng thời gian overlap:

```text
Không báo lỗi đỏ cứng.
Khi bấm Tiếp theo → hiện confirm.
User chọn “Kiểm tra lại” → ở lại Step 1.
User chọn “Vẫn tiếp tục” → qua Step 2.
```

Helper overlap:

```ts
type VisitCampusRow = {
  campus: string;
  startDatetime: string;
  endDatetime: string;
};

export function isTimeOverlap(a: VisitCampusRow, b: VisitCampusRow): boolean {
  if (!a.startDatetime || !a.endDatetime || !b.startDatetime || !b.endDatetime) return false;

  const startA = new Date(a.startDatetime).getTime();
  const endA = new Date(a.endDatetime).getTime();
  const startB = new Date(b.startDatetime).getTime();
  const endB = new Date(b.endDatetime).getTime();

  if (Number.isNaN(startA) || Number.isNaN(endA) || Number.isNaN(startB) || Number.isNaN(endB)) {
    return false;
  }

  return startA < endB && startB < endA;
}

export function findCampusTimeOverlaps(visits: VisitCampusRow[]) {
  const conflicts: Array<{ firstIndex: number; secondIndex: number }> = [];

  for (let i = 0; i < visits.length; i++) {
    for (let j = i + 1; j < visits.length; j++) {
      const a = visits[i];
      const b = visits[j];

      if (!a.campus || !b.campus) continue;

      // Trùng campus là lỗi cứng riêng, không xử lý confirm ở đây.
      if (a.campus === b.campus) continue;

      if (isTimeOverlap(a, b)) {
        conflicts.push({ firstIndex: i, secondIndex: j });
      }
    }
  }

  return conflicts;
}
```

Không đưa overlap vào Zod hard error kiểu:

```ts
ctx.addIssue({
  path: ["visits"],
  message: "OVERLAP_UNCONFIRMED",
});
```

Overlap nên xử lý trong `handleNextStep` sau khi validation cứng đã pass.

---

## B5. Thứ tự handleNextStep đúng

Sửa thứ tự:

```text
1. Mark step attempted.
2. Trigger validation của step hiện tại.
3. Nếu invalid → hiển thị lỗi, không check confirm.
4. Nếu valid và currentStep=1 → check time overlap.
5. Nếu overlap chưa confirm → mở modal confirm.
6. Nếu không overlap / đã confirm → qua step tiếp.
```

Pseudo:

```ts
const handleNextStep = async () => {
  setStepError(null);

  setStepAttempted((prev) => ({
    ...prev,
    [currentStep]: true,
  }));

  const stepFields: Record<number, string[]> = {
    1: ["registerInfo", "delegationName", "visitMode", "visits", "purpose", "workingContent"],
    2: ["visitors", "supportTeam", "contactPoint"],
  };

  const fields = stepFields[currentStep];

  if (!fields) {
    setCurrentStep((s) => s + 1);
    return;
  }

  const valid = await form.trigger(fields as any);

  if (!valid) {
    setStepError("Vui lòng điền đầy đủ và đúng các trường bắt buộc trước khi tiếp tục.");
    return;
  }

  if (currentStep === 1) {
    const values = form.getValues();
    const overlaps = findCampusTimeOverlaps(values.visits || []);
    const isConfirmed = values.timeOverlapConfirmed;

    if (values.visitMode === "multiple" && overlaps.length > 0 && !isConfirmed) {
      setShowOverlapConfirm(true);
      return;
    }
  }

  setCurrentStep((s) => s + 1);
};
```

Confirm button:

```ts
onClick={() => {
  form.setValue("timeOverlapConfirmed", true, {
    shouldValidate: false,
    shouldDirty: true,
  });
  setShowOverlapConfirm(false);
  setCurrentStep((s) => s + 1);
}}
```

---

# PHẦN C — VALIDATION BACKEND

## C1. Backend phải validate lại tất cả rule quan trọng

Backend phải check ở cả:

```text
InitiateVisitRequestCommandValidator
VerifyAndCreateVisitRequestCommandValidator
VisitRequestFormValidationRules
```

hoặc shared validator hiện có.

Không tin frontend.

---

## C2. Backend scope/campus validation

Backend phải enforce:

```text
SINGLE_CAMPUS:
- đúng 1 campus.

MULTI_CAMPUS:
- ít nhất 2 campus khác nhau.
- không duplicate campus.
```

Ưu tiên lỗi:

```text
1. Duplicate campus.
2. Thiếu 2 campus.
```

Pseudo C#:

```csharp
var campusCodes = request.Visits
    .Select(x => x.CampusCode?.Trim())
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .ToList();

var distinctCampusCodes = campusCodes
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();

var hasDuplicateCampus = campusCodes.Count != distinctCampusCodes.Count;

if (request.VisitScope == "MULTI_CAMPUS")
{
    if (hasDuplicateCampus)
    {
        throw new BusinessRuleException(
            "Không được chọn trùng cơ sở trong yêu cầu liên cơ sở. Vui lòng chọn cơ sở khác.",
            errorCode: "DUPLICATE_CAMPUS");
    }

    if (distinctCampusCodes.Count < 2)
    {
        throw new BusinessRuleException(
            "Yêu cầu liên cơ sở cần ít nhất 2 cơ sở.",
            errorCode: "INVALID_VISIT_SCOPE");
    }
}

if (request.VisitScope == "SINGLE_CAMPUS" && distinctCampusCodes.Count != 1)
{
    throw new BusinessRuleException(
        "Yêu cầu một cơ sở chỉ được chọn đúng 1 cơ sở.",
        errorCode: "INVALID_VISIT_SCOPE");
}
```

---

## C3. Backend campus existence / ACTIVE

Với từng campus:

```text
[ ] Campus exists.
[ ] Campus status = ACTIVE.
```

Nếu không tồn tại:

```text
errorCode = CAMPUS_NOT_FOUND
message = Cơ sở được chọn không tồn tại.
```

Nếu inactive:

```text
errorCode = CAMPUS_INACTIVE
message = Cơ sở được chọn hiện không hoạt động.
```

Không để thành 500.

---

## C4. Backend time validation

Backend phải check:

```text
[ ] plannedStartAt required.
[ ] plannedEndAt required.
[ ] plannedEndAt > plannedStartAt.
[ ] plannedStartAt không ở quá khứ.
[ ] Nếu rule 72h đã chốt: plannedStartAt >= now + 72h.
[ ] Nếu rule 3h đã chốt: duration >= 3h.
```

Error code:

```text
INVALID_VISIT_TIME
```

---

## C5. Backend time overlap confirm

Khuyến nghị backend cũng check confirm để tránh bypass FE.

Payload optional:

```json
{
  "timeOverlapConfirmed": true
}
```

Backend rule:

```csharp
var overlaps = FindOverlapsBetweenDifferentCampuses(request.Visits);

if (overlaps.Any() && request.TimeOverlapConfirmed != true)
{
    throw new BusinessRuleException(
        "Một số cơ sở trong lịch trình có thời gian bị chồng lên nhau. Vui lòng xác nhận trước khi gửi.",
        errorCode: "TIME_OVERLAP_CONFIRM_REQUIRED");
}
```

Nếu chưa muốn enforce backend, ghi rõ trong report:

```text
Time overlap confirm currently enforced on frontend only.
```

---

## C6. Backend registrant/contact validation

Rule đúng:

```text
registrantEmail = email nhận OTP.
contactEmail = email tạo/link VISITOR.
```

Backend phải:

```text
[ ] Verify OTP bằng registrantEmail.
[ ] Create/link VISITOR bằng contactEmail.
[ ] Không bắt contactEmail phải giống registrantEmail.
[ ] Không throw EMAIL_MISMATCH khi contactEmail khác registrantEmail.
[ ] EMAIL_MISMATCH chỉ dùng khi OTP token email != registrantEmail.
```

---

## C7. Backend guest validation

Backend phải khớp FE:

```text
[ ] guest fullName required.
[ ] guest email required.
[ ] guest email valid format.
[ ] nationality required nếu FE đang required.
[ ] jobTitle optional/required theo DTO hiện tại.
```

Không còn:

```text
PassportId
passportId
CCCD
CMND
identityNumber
citizenId
idNumber
documentNumber
```

Search backend:

```bash
grep -R "PassportId\|passportId\|identity\|CCCD\|CMND\|citizenId\|idNumber\|documentNumber" backend/PEMS.Application
```

---

# PHẦN D — PAYLOAD MAPPING FE ↔ BE

## D1. Check visitMode mapping

FE:

```text
single / multiple
```

BE có thể cần:

```text
SINGLE_CAMPUS / MULTI_CAMPUS
```

Mapper phải đúng:

```ts
const visitScope =
  values.visitMode === "single"
    ? "SINGLE_CAMPUS"
    : "MULTI_CAMPUS";
```

---

## D2. Check campus mapping

FE row:

```ts
{
  campus: "HN",
  startDatetime: "...",
  endDatetime: "..."
}
```

BE DTO có thể cần:

```json
{
  "campusCode": "HN",
  "plannedStartAt": "...",
  "plannedEndAt": "..."
}
```

hoặc `campusId`.

Không gửi label `"Hà Nội"` nếu backend cần code/id.

---

## D3. Check datetime mapping

Input `datetime-local` trả:

```text
2026-07-23T13:02
```

Nếu BE cần timezone:

```ts
function toVietnamIso(value: string) {
  if (!value) return "";
  return `${value}:00+07:00`;
}
```

Không gửi display string:

```text
07/23/2026 01:02 PM
```

---

## D4. Check timeOverlapConfirmed mapping

Nếu BE DTO có field:

```csharp
public bool TimeOverlapConfirmed { get; init; }
```

FE payload phải gửi field này.

Nếu BE không dùng field này, FE không cần gửi.

---

# PHẦN E — API TEST BẰNG POSTMAN/CURL

## E1. MULTI_CAMPUS chỉ 1 campus

Expected:

```text
400/422
INVALID_VISIT_SCOPE
message cần ít nhất 2 cơ sở
```

## E2. MULTI_CAMPUS duplicate campus

Expected:

```text
400/422
DUPLICATE_CAMPUS
message không được chọn trùng cơ sở
```

## E3. MULTI_CAMPUS 2 campus khác nhau, time overlap chưa confirm

Nếu BE enforce:

```text
422
TIME_OVERLAP_CONFIRM_REQUIRED
```

Nếu BE không enforce:

```text
Pass backend validation, FE enforce confirm.
```

## E4. MULTI_CAMPUS 2 campus khác nhau, time overlap đã confirm

Expected:

```text
Pass.
```

## E5. contactEmail khác registrantEmail

Expected:

```text
OTP verify bằng registrantEmail.
VISITOR create/link bằng contactEmail.
Không EMAIL_MISMATCH.
```

---

# PHẦN F — FILES CẦN KIỂM TRA/SỬA

## Frontend

```text
frontend/pems-react/src/features/visit-request/schema/visitRequest.schema.ts
frontend/pems-react/src/features/visit-request/components/sections/VisitInfoSection.tsx
frontend/pems-react/src/pages/**/VisitingFormPopup.tsx
frontend/pems-react/src/features/visit-request/api/visitRequestApi.ts
frontend/pems-react/src/features/visit-request/hooks/useVisitRequestForm.ts
frontend/pems-react/src/features/visit-request/components/sections/RegisterInfoSection.tsx
frontend/pems-react/src/features/visit-request/components/sections/ContactSection.tsx
```

## Backend

```text
backend/PEMS.Application/Delegations/Commands/VisitRequestFormValidationRules.cs
backend/PEMS.Application/Delegations/Commands/InitiateVisitRequest/**
backend/PEMS.Application/Delegations/Commands/VerifyAndCreateVisitRequest/**
backend/PEMS.Application/Common/DTOs/VisitFormDtos.cs
backend/PEMS.Infrastructure/Services/VisitRequestService.cs
backend/PEMS.Api/Middleware/ExceptionHandlingMiddleware.cs
```

---

# PHẦN G — COMMANDS

Frontend:

```bash
cd frontend/pems-react
npm run build
```

Backend:

```bash
dotnet build backend/PEMS.Api/PEMS.Api.csproj -p:BaseOutputPath=./.tmp-build/
```

Nếu có test:

```bash
dotnet test tests/PEMS.ArchitectureTests/PEMS.ArchitectureTests.csproj -p:BaseOutputPath=./.tmp-build/
```

---

# PHẦN H — OUTPUT REPORT

Sau khi sửa, trả report:

```md
# UC-17 FE/BE Validation + Submit Error Fix Report

## Summary
- Fixed frontend validation display.
- Fixed backend validation.
- Fixed submit 400 root cause.
- Frontend now shows backend error messages.
- Duplicate campus is blocked FE + BE.
- Multi-campus requires at least 2 distinct campuses FE + BE.
- Time overlap confirm handled: FE only / FE + BE.

## Root Cause of 400
- Endpoint:
- Status:
- Backend response:
- Field/rule causing failure:

## Files Changed
### Frontend
- ...

### Backend
- ...

## Payload Mapping Verified
- visitMode:
- campus:
- datetime:
- registrantEmail:
- contactEmail:
- timeOverlapConfirmed:

## Validation Rules Verified
- ...

## Commands Run
```bash
npm run build
dotnet build ...
```

## Manual/API Tests
- ...

## Remaining Notes
- ...
```

---

# Definition of Done

```text
[ ] Mới mở form không hiện lỗi đỏ date/time.
[ ] Bấm Tiếp theo mới hiện lỗi required.
[ ] MULTI_CAMPUS + 1 campus bị chặn FE + BE.
[ ] MULTI_CAMPUS + duplicate campus bị chặn FE + BE.
[ ] Duplicate campus hiện đúng message, không hiện nhầm min-2-campus.
[ ] Time overlap giữa campus khác nhau hiện confirm.
[ ] Nếu BE enforce overlap confirm, payload gửi timeOverlapConfirmed=true khi user xác nhận.
[ ] /api/visit-requests/initiate không còn 400 với form hợp lệ.
[ ] Nếu backend trả validation error, UI hiện message cụ thể.
[ ] Payload không còn passportId/CCCD/CMND.
[ ] Guest email vẫn required.
[ ] OTP email = registrantEmail.
[ ] VISITOR account email = contactEmail.
[ ] Frontend build pass.
[ ] Backend build pass.
```
