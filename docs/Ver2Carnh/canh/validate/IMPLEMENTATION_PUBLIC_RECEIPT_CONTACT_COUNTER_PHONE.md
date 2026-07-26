# Báo cáo — Receipt public sau OTP, organization combobox, quick-fill, counter theo focus, hướng dẫn số điện thoại

> Thực hiện `PEMS_PROMPT_FIX_PUBLIC_OTP_RECEIPT_ORGANIZATION_COMBOBOX_COUNTER_PHONE_QUICK_FILL.md`.
> Nhánh `Canh-Iter1`, base `d6c6a8a6`. Không reset/rebase/force-push, không xóa WIP, không migration.

---

## 1. Root cause: vì sao receipt public không render ở runtime

**Không phải component. Là wiring — đúng một dòng.**

`shared/features/VisitEntrySurfaces.tsx:26` (trước khi sửa):

```tsx
onSuccess={() => { cta.closeV2Modal(); onV2Success?.(); }}
```

`VisitRequestV2Modal` **đã** làm đúng: `onSuccess` của nó set `result` rồi render `VisitRequestV2SuccessPanel` và **không** tự đóng (`VisitRequestV2Modal.tsx:144-154`). Nhưng host gọi `closeV2Modal()` ngay trong cùng tick, `isOpen` thành `false`, modal `return null` — receipt bị unmount ở đúng frame nó vừa xuất hiện. Thứ duy nhất sống sót là toast bắn ở dòng 149.

`VisitEntrySurfaces` là nơi **mọi** CTA public đi qua: `HeroSection.tsx:92`, `FinalCtaSection.tsx:52`, `FAQPage.tsx:563`, `PartnersPage.tsx:662`. Nên triệu chứng xuất hiện ở toàn bộ luồng public.

Hai chỗ **không** dính lỗi, và đó là lý do bug sống lâu:

| Surface | Trạng thái trước | Vì sao |
|---|---|---|
| Route `/visit-registration/v2` | ĐÚNG | `VisitRequestV2Page.tsx:45` render receipt tại chỗ, không có host nào đóng |
| Dashboard `VisitRequestManagement.tsx:1929` | ĐÚNG | `onSuccess` chỉ `loadDelegations(...)`, không đóng modal |

Test cũ cũng không bắt được: `VisitRequestV2Modal.test.tsx:148` chứng minh **modal** giữ receipt, nhưng render modal trực tiếp — nó không đi qua `VisitEntrySurfaces`.

---

## 2. Success callback / modal close — trước và sau

| | Trước | Sau |
|---|---|---|
| `VisitEntrySurfaces.onSuccess` | `closeV2Modal(); onV2Success?.()` | `onV2Success?.()` — **không đóng** |
| Ai đóng modal | host, tự động, ngay khi tạo xong | người dùng, qua `[Đóng]` trên receipt hoặc nút X ở header |
| Footer modal khi có receipt | nút "Hoàn tất" | rỗng — `[Đóng]` là action của chính receipt, hai nút cạnh nhau chỉ khiến người dùng phân vân chúng khác gì nhau |
| `modal.done` i18n key | có | xóa (không còn dùng, cả VI lẫn EN) |

`onSuccess` của host vẫn được gọi — dashboard vẫn refresh danh sách phía sau modal (test `still tells the host, so a dashboard list behind the modal can refresh`).

---

## 3. `submittedSnapshot` lifecycle

Prompt §5 yêu cầu một deep clone bất biến. **Điều này đã đúng sẵn theo cấu trúc**, và em giữ nguyên thay vì thêm state thứ hai:

- `useVisitRequestFormV2.verifyOtp` tạo `submittedValues = cloneValues(form.getValues())` **trước** khi gọi API (`useVisitRequestFormV2.ts:600`); `cloneValues` dùng `structuredClone`, fallback `JSON.parse(JSON.stringify(...))`.
- Cùng cách ở `submitAuthenticated` (dòng 485).
- Clone đó được truyền vào `onSuccess(result, submittedValues)`; modal lưu vào `result` state; form unmount ngay sau đó (`result ? <Panel/> : <Form/>`).

Nên snapshot **không** giữ reference tới form values đang bị reset. Test `keeps showing the snapshot it was given even when the caller re-renders` khóa lại điều này.

Thứ tự clear (§6), theo đúng code:

```
1. cloneValues(...)                    ← deep clone, trước khi request rời trình duyệt
2. verify thành công
3. setSessionToken(null) · submissionIdRef = null · setPendingOtp(null)
4. clearVisitRequestV2Draft(draftNamespace)   ← chỉ ở đây, chỉ khi đã COMPLETED
5. setStage('CREATE_CONFIRMED')
6. onSuccess(result, submittedValues)  ← modal giữ receipt, KHÔNG đóng
```

Đóng receipt không restore lại draft vừa hoàn tất vì draft đã bị xóa ở bước 4. `[Tạo đơn mới]` mới reset: modal `setResult(null)` + `setFormGeneration(g => g + 1)` để remount form sạch.

Snapshot chỉ sống trong React state của session UI hiện tại, không vào URL, không log, không lưu OTP thô.

---

## 4. Ma trận action public / authenticated

Cả hai dùng **chung** `VisitRequestV2SuccessPanel`; khác nhau ở prop nào được truyền.

| Action | testid | Public | Authenticated | Điều kiện |
|---|---|---|---|---|
| Xem lại thông tin đã gửi | `v2-success-review` | ✅ | ✅ | luôn, trừ receipt dựng lại từ lookup |
| Sao chép mã đơn | `v2-success-copy` | ✅ | ✅ | có `requestCode` |
| Đóng | `v2-success-close` | ✅ | ✅ | modal truyền `onClose`; route dùng `footer` link về trang chủ |
| Xem đơn vừa tạo | `v2-success-view` | ❌ | ✅ | host truyền `onViewRequest` |
| Về danh sách đơn | `v2-success-list` | ❌ | ✅ | host truyền `onGoToList` |
| Tạo đơn mới | `v2-success-new` | ✅ (modal) | ✅ | host truyền `onCreateAnother` |

**"Đăng nhập để quản lý đơn" — bỏ khỏi scope**, đúng theo §8 câu cuối. Luồng public provision tài khoản Visitor nhưng **không đăng nhập** người dùng, và hiện chưa có `returnUrl` đưa họ về đúng đơn sau khi login, cũng chưa có thông báo khi họ login nhầm tài khoản. Thêm nút bây giờ là tạo ngõ cụt. Đây là ngoại lệ **có chủ ý và được prompt cho phép**, không phải sót.

Copy mã đơn: `navigator.clipboard.writeText`. Nếu trình duyệt từ chối (origin không secure, permission policy) thì **nói thật** — panel hiện "Trình duyệt không cho phép sao chép tự động. Mã đơn của bạn: VR…" chứ không báo "đã sao chép" khi chưa sao chép được gì.

---

## 5. OrganizationCombobox reuse

`campusVisits[i].operationalContact.organization` chuyển từ `AutoGrowTextField` sang `OrganizationCombobox` — **đúng component các dòng khách/nhân sự hỗ trợ đang dùng** (`CampusVisitCard.tsx:244`), không phải bản sao.

Thêm vào `OrganizationCombobox` (đều optional, không ảnh hưởng call-site cũ):

| Prop | Vì sao |
|---|---|
| `testId` | react-select không forward prop lạ xuống input; wrapper `<div data-testid>` là cách duy nhất |
| `ariaLabel` / `inputId` | tên khả truy cập, vì label hiển thị nằm ở `FormField` |

Hành vi giữ nguyên: tìm đơn vị có sẵn, chọn được, **vẫn nhập tự do được**, giá trị lưu là snapshot text. Không có `partnerId` nào bị đụng — xem Mục 6.

---

## 6. Badge policy

| Nơi | Hiển thị | Lý do |
|---|---|---|
| `registerInfo.organization` (request-level, `PartnerOrgCombobox`) | badge xanh **"Đã chọn đối tác có sẵn"** | đây là chỗ duy nhất người dùng thực sự chọn đối tác chính của đơn, và là chỗ duy nhất `partnerId` được ghi |
| Đầu mối phối hợp từng campus | note nhẹ **"✓ Có trong hệ thống"**, chỉ khi vừa chọn từ danh sách, biến mất khi gõ lại | phân biệt "khớp cái đã có" với "gõ mới" mà không gợi ý đây là liên kết partner |
| Khách / nhân sự hỗ trợ (cell trong bảng) | không gì cả (`isCell` → tắt) | trong ô bảng thì một dòng chữ nữa là nhiễu |
| Đầu mối chính | không | vẫn là input thường, chưa nằm trong scope prompt này |

Badge lớn **không** lặp lại, và có test khóa: `does not repeat the request-level partner badge on this field`.

Chỗ này quan trọng hơn vẻ ngoài: `ContactPointDto` **không có trường `PartnerId`** — em kiểm tra bằng reflection trong `VisitRequestV2ContactGuidanceTests.Picking_an_operational_organization_cannot_carry_a_partner_selection`. Đó là lý do cấu trúc khiến combobox ở đây không thể chạm vào partner của đơn, chứ không phải "UI không gửi". Ba integration test đóng đinh ở mức DB:

- tên khớp **chính xác** một partner có thật → `visit_requests.partner_id` vẫn `NULL`;
- đơn đã chọn partner → chọn organization khác ở campus không làm mất partner đó;
- hai campus giữ hai giá trị khác nhau (quick-fill chỉ chạm một thẻ).

---

## 7. Quick-fill semantics

Hai nút trong legend của fieldset "Đầu mối phối hợp tại cơ sở": **[Dùng người đăng ký]** và **[Dùng đầu mối chính]**. Cả hai vì cả hai tình huống đều có thật và hai người có thể khác nhau — một nút chỉ phục vụ được một nửa.

- **One-time copy**, đúng 4 trường `fullName · organization · phone · email`. Không giữ liên kết: sửa nguồn về sau **không** ghi đè lại campus (test `is a one-time copy: editing afterwards changes neither side of it`).
- `form.setValue` **từng trường trên đúng `campusVisits.{index}`** — set cả object lên path dùng chung sẽ rò sang thẻ khác, mà mỗi campus là snapshot độc lập theo thiết kế.
- Nút chỉ bật khi nguồn có tối thiểu **tên + email**.
- Destination đã có dữ liệu → **hỏi trước**, hiện `campus-opcontact-replace-confirm-{i}`, và giá trị cũ còn nguyên trong lúc chưa trả lời.
- Sau copy: toast top-right (helper dùng chung) + dòng chú thích nhỏ "…thông tin này chỉnh sửa độc lập — sửa nguồn về sau không cập nhật lại đây."
- `shouldDirty: true, shouldValidate: true` → draft autosave và validation chạy lại như dữ liệu gõ tay. Test draft-restore chờ đúng 700ms autosave thật rồi remount, không gọi tắt helper storage.

---

## 8. Counter visibility rules

Quy tắc gom vào `components/shared/characterCount.ts`, dùng chung cho `AutoGrowTextarea` và `AutoGrowTextField`:

```
hiện khi:  đang focus
       HOẶC length ≥ 80% giới hạn
       HOẶC length > giới hạn
       HOẶC (có lỗi VÀ length > 0)
ẩn khi:    blur + dưới 80% + không lỗi
```

Vế cuối cố ý: field rỗng, blur, đang báo "bắt buộc" thì **không** hiện `0/2000` — thông báo đó không nói về số, gắn thêm một con số chỉ khiến nó trông như lỗi độ dài.

Trước đây `AutoGrowTextarea` hiện counter vô điều kiện với mọi `maxLength`, nên mở form ra là một cột `0/2000 · 0/4000 · 0/2000`. `AutoGrowTextField` đã có ngưỡng 80% nhưng chưa có luật focus.

Vượt giới hạn: counter đỏ, **không** ẩn, `maxLength` **không** đặt lên DOM node (trình duyệt sẽ cắt âm thầm), giá trị paste giữ nguyên đủ để người dùng thấy phần thừa. Test khóa cả ba.

Chuỗi counter đi qua i18n key `validation:characterCount` thay vì nối chuỗi trong component.

---

## 9. Phone validation messages

Rule **không đổi** (VN national hoặc E.164, không máy lẻ) — `shared/utils/phoneNumber.isValidPhone` ↔ `PEMS.Shared.PhoneNumber.IsValid`. Chỉ **thông điệp** đổi.

**Frontend** — `buildPhoneSchema(t, fieldKey)` nhận tên field:

```
Số điện thoại đầu mối phối hợp không hợp lệ. Nhập số Việt Nam dạng 0912345678
hoặc số quốc tế dạng +84912345678. Không nhập số máy lẻ.
```

**Backend** — `PhoneNumberRules.FormatHint` là hằng số dùng chung, tự nối vào **mọi** message của `MustBeAPhoneNumber`. Nhờ vậy caller gọi API trực tiếp (không có UI) nhận đúng hướng dẫn mà FE hiển thị, và hai bên không thể mô tả một luật theo hai cách.

**Hint trước khi có lỗi** — `components/shared/PhoneField.tsx`: hiện "Ví dụ: 0912345678 hoặc +84912345678" **khi focus**, và **nhường chỗ** cho error đầy đủ ngay khi có lỗi (không stack hai đoạn dài dưới một ô nhỏ). Áp dụng ở: người đăng ký, đầu mối chính, đầu mối phối hợp từng campus, edit/resubmit. Form chuyển giao đầu mối (`ContactIdentityActions`) là form server-validated, dùng hint tĩnh cùng nội dung.

Cùng lúc, **mọi** `.max()` trong schema giờ gọi tên field và nói lệch bao nhiêu:

```
Nhận diện phương tiện di chuyển không được vượt quá 2.000 ký tự (hiện tại 2.014).
```

Số nhóm theo locale (`2.000` VI / `2,000` EN) — `2000` cạnh một form đầy ngày tháng đọc ra như một năm.

---

## 10. Files changed

**Backend (2 + 2 test)**

| File | Thay đổi |
|---|---|
| `PEMS.Application/Common/Validation/PhoneNumberRules.cs` | `FormatHint` const; tự nối vào mọi message |
| `tests/PEMS.UnitTests/VisitRequests/VisitRequestV2ContactGuidanceTests.cs` | **mới** — 13 test |
| `tests/PEMS.IntegrationTests/VisitRequests/OperationalContactSnapshotV2Tests.cs` | **mới** — 5 test |

**Frontend — mới**

| File | Vai trò |
|---|---|
| `components/shared/characterCount.ts` | luật hiện/ẩn counter, dùng chung |
| `components/shared/PhoneField.tsx` | input phone + hint theo focus |
| `utils/formErrorNavigation.ts` | `countFieldErrors` + `focusFirstInvalidField` |
| `__tests__/publicSuccessReceipt.test.tsx` | 14 test |
| `__tests__/operationalContactQuickFill.test.tsx` | 13 test |
| `__tests__/counterPhoneErrorFocus.test.tsx` | 27 test |
| `tests-realstack/public-receipt-and-contact.realstack.spec.ts` | 5 journey |

**Frontend — sửa**

| File | Thay đổi |
|---|---|
| `shared/features/VisitEntrySurfaces.tsx` | **root cause** — bỏ `closeV2Modal()` khỏi `onSuccess` |
| `components/v2/VisitRequestV2SuccessPanel.tsx` | viết lại: copy mã, toggle xem lại, `onClose`, `keepCode` |
| `components/v2/VisitRequestV2Modal.tsx` | truyền `onClose` xuống panel; bỏ nút "Hoàn tất" trùng |
| `components/v2/CampusVisitCard.tsx` | organization → combobox; quick-fill + confirm; `PhoneField`; dùng `countFieldErrors` chung |
| `components/v2/VisitRequestFormV2.tsx` | `PhoneField` ×2; focus field lỗi đầu tiên; guard `scrollIntoView` |
| `components/shared/OrganizationCombobox.tsx` | `testId`/`ariaLabel`/`inputId`; note "✓ Có trong hệ thống" |
| `components/shared/AutoGrowTextarea.tsx` · `AutoGrowTextField.tsx` | counter theo focus, i18n |
| `components/ContactIdentityActions.tsx` | hint định dạng phone, `type=tel` |
| `hooks/useVisitRequestFormV2.ts` | `focusErrorsToken`; banner đếm số lỗi |
| `schema/visitRequestV2.schema.ts` | `bounded()` + `buildPhoneSchema(t, fieldKey)`; mọi message gọi tên field |
| `pages/dashboard/visit/EditVisitRequestV2Page.tsx` | `PhoneField` ×2 |
| `locales/{vi,en}/validation.json` | `fields.*` (31 key), `requiredField`, `maxLengthField*`, `phoneInvalidField`, `phoneHint`, `fixErrorsCount`, `maxMembers`, `characterCount`; bỏ `transportationNoteMaxLength` |
| `locales/{vi,en}/visitRequestV2.json` | `success.*` (keepCode/review/copy/close), `card.quickFill*`; bỏ `modal.done` |
| `locales/{vi,en}/visitRequest.json` | `select.orgKnown` |
| `tests-realstack/realstackHelpers.ts` | `fillOperationalOrganization` |
| 5 spec real-stack + `fieldLengthAutoGrow.test.tsx` | cập nhật theo UI mới |

---

## 11. Tests added

| Nhóm | Số | Nội dung
|---|---|---|
| FE — receipt public (§20) | 14 | CTA **không** đóng modal · receipt không chỉ là toast · code/status/submittedAt/campusCount · nút xem lại · snapshot bất biến · summary từ snapshot (và **không** gọi detail API) · copy thành công / bị từ chối · public không có action dashboard · authenticated có đủ 3 · "tạo đơn mới" mới reset · đóng không prompt lưu nháp · VI |
| FE — combobox + quick-fill (§21) | 13 | là combobox thật · chọn được đơn vị có sẵn · vẫn nhập tự do · không lặp badge · copy đúng 4 trường ×2 nguồn · chỉ chạm campus hiện tại · sửa độc lập hai chiều · confirm khi ghi đè · fill thẳng khi trống · dòng "độc lập" · draft restore giữ cả organization |
| FE — counter/phone/focus (§22) | 27 | 6 luật hiện/ẩn counter · counter trên màn hình (focus/blur/gần limit/đỏ/không truncate/không `maxlength`) · message gọi tên field + nhóm số + VI · phone: tên field + 2 mẫu + máy lẻ, chấp nhận cả 2 dạng, hint theo focus, hint nhường error, VI · đếm lỗi lá · focus field lỗi đầu · bỏ qua field trong card đang gập · không throw khi không có control · banner đếm VI/EN · 3 test trên form thật |
| BE — unit (§23) | 13 | 3 dạng phone hợp lệ · 4 dạng bị từ chối · mỗi phone gọi đúng tên mình · message chứa `FormatHint` · organization biên 200 · free text · `ContactPointDto` **không có** `PartnerId` · response public đủ receipt và **không** chứa PII |
| BE — integration (§23) | 5 | snapshot lưu nguyên văn · tên khớp partner thật vẫn không link · partner đã chọn không bị mất · mỗi campus giữ giá trị riêng · 200 ký tự tới được cột |
| Real-stack (§24) | 5 | A: CTA modal giữ receipt + lookup DB thật · B: quick-fill + combobox → snapshot đúng · B2: hỏi trước khi ghi đè · C: phone sai có ví dụ, focus đúng field, sửa xong đi tiếp · C2: counter theo focus, paste dài đỏ, submit bị chặn |
| **Tổng thêm** | **77** | |

---

## 12. Gate results

Lệnh thật, đã chạy:

| Gate | Baseline | Kết quả |
|---|---|---|
| `dotnet build` (4 project) | 0 lỗi | **0 lỗi** (470 warning, không đổi) |
| `dotnet test PEMS.ArchitectureTests` | 14/14 | **14/14** |
| `dotnet test PEMS.UnitTests` | 1072 | **1085/1085** (+13) |
| `dotnet test PEMS.IntegrationTests` | 636 | **641/641** (+5) |
| `npm run lint` (`tsc --noEmit`) | 0 | **0** |
| `npm run test:unit` | 659 | **713/713** (54 file) (+54) |
| `npm run build` | OK | **OK** |
| `npm run test:e2e:realstack` | 35 | **40/40** (+5) |
| `git diff --check` | sạch | **sạch** |

Real-stack chạy trên DB dùng-một-lần `pems_e2e_realstack`; integration trên `pems_pr3_test` trong transaction rollback. **Không** chạm `pems_db`.

> Ghi chú môi trường: `mysql` CLI không nằm trên PATH của shell hiện tại, phải chạy real-stack với
> `MYSQL_BIN="C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" npm run test:e2e:realstack`.
> Đây là chuyện máy, không phải chuyện code — orchestrator đã hỗ trợ sẵn biến `MYSQL_BIN`.

---

## 13. Real-stack evidence

**Journey A** đi qua **nút CTA thật trên trang chủ**, không qua route `/visit-registration/v2`. Đây là điểm mấu chốt: route vốn đã đúng từ trước, lỗi nằm ở modal do CTA mở. Chuỗi: `/` → bấm "Đăng ký tham quan" → modal → điền → OTP thật từ sink → sau xác nhận **`v2-create-modal` vẫn visible** kèm `v2-success-code` chứa `VR…` → bấm "Xem lại thông tin đã gửi" → summary có "Đoàn Biên Lai", "Khách Thật", "Đơn vị đầu mối" → và bắt `submissionId` ngay lúc request rời trình duyệt rồi gọi `GET /v2/visit-requests/submissions/{id}` → `state = COMPLETED`, `requestCode` khớp đúng chuỗi trên màn hình. Sink chỉ có **1** mã OTP.

**Journey C** chứng minh chuỗi đầy đủ của §17–§19 trên stack thật: nhập `090abc` → message hiện đủ tên field + `0912345678` + `+84912345678`, `campus-opcontact-phone-0` **đang được focus**, banner ghi "Còn N trường cần kiểm tra" → sửa thành `+84912345678` → submit lần hai vào thẳng bước OTP.

**Journey C2**: `0/2000` không tồn tại khi form vừa mở, xuất hiện khi focus, paste 2014 ký tự thì `2014/2000` và giá trị **giữ nguyên 2014 ký tự**, submit bị chặn với message gọi tên "Nhận diện phương tiện di chuyển … 2.000 ký tự".

---

## 14. Database impact

**Không có.** Không migration, không patch SQL, không đổi cột nào.

Mọi thứ đụng tới DB trong đợt này đều là đọc/ghi qua đường đã có: `visit_instance_form_details.operational_contact_organization` (đã tồn tại, `VARCHAR(200)`), `visit_requests.partner_id` (đã tồn tại, không đổi ngữ nghĩa). Backend chỉ đổi **chuỗi thông báo**, không đổi luật, nên dữ liệu cũ không bị ảnh hưởng.

---

## 15. Known limitations

1. **"Đăng nhập để quản lý đơn" chưa có.** Bỏ có chủ ý (Mục 4). Muốn bật thì cần 3 thứ trước: `returnUrl` qua login, sau login mở đúng đơn, và thông báo rõ khi login sai tài khoản.
2. **`VisitAmendmentSubmitModal.tsx:301`** — ô phone của đề xuất thay đổi vẫn là input trần, chưa dùng `PhoneField`. Modal này không nằm trong danh sách §18 và đang dùng `datetime-local` (đã ghi nhận là khoản nợ riêng từ đợt trước); gộp cả hai vào một lần refactor thì sạch hơn.
3. **Đầu mối chính (`contactPoint.organization`)** vẫn là input thường, không phải combobox. §9 chỉ yêu cầu cho đầu mối phối hợp campus; mở rộng thêm là quyết định UX nên hỏi trước.
4. **Note "✓ Có trong hệ thống" biến mất khi remount** (ví dụ restore draft) vì nó phản ánh *hành động vừa rồi*, không phải trạng thái đã lưu — đúng ý nghĩa, nhưng đáng biết.
5. **`mysql` CLI cần PATH hoặc `MYSQL_BIN`** khi chạy real-stack ở máy này.

---

## 16. Resume point

- Nhánh `Canh-Iter1`, cây sạch, **chưa push**.
- Còn treo từ đợt trước, em không đụng: `git rebase --onto msg-repaired 8850b87e Canh-Iter1` để nhận các commit message đã sửa, rồi quyết định push/PR về `Dev`.
- Nếu muốn đi tiếp: mở rộng `PhoneField` sang `VisitAmendmentSubmitModal` + đổi `datetime-local` ở đó sang `VisitDateTimeRangePicker` — hai việc chạm cùng một file, nên làm một lần.
