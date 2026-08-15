# Báo cáo triển khai — Import Excel, đầu mối đoàn, chống trùng và biên bản

**Ngày:** 15/08/2026
**Kế hoạch nguồn:** `PEMS_FIX_PLAN_OPERATIONAL_CONTACT_IMPORT_MINUTES.md`
**Nhánh:** `Canh-iter2` · chưa commit

Báo cáo theo tám mục Mục 22 của kế hoạch.

---

## 1. Root cause thực tế của từng mã lỗi

Đọc theo đúng chuỗi FE → API → handler/service → entity/DB → Minute autofill trước khi sửa. Dưới đây là nguyên nhân **đo được trong code**, không phải giả định của kế hoạch.

| Mã | Root cause thực tế |
|---|---|
| `IMP-01` | `CampusVisitCard.tsx` giữ **một** ô state `pendingReplace = { kind, rows, fileName }` và render **một** khối xác nhận **sau cả hai** `<fieldset>`. Nên nhập file cho danh sách khách thì câu hỏi "Thay thế toàn bộ danh sách?" hiện dưới bảng Nhân sự hỗ trợ — hỏi về danh sách này, chỉ vào danh sách kia. |
| `IMP-02` | Cùng ô state đó: import file thứ hai ghi đè câu hỏi đang chờ của file thứ nhất, câu hỏi đầu biến mất chưa được trả lời. (Riêng `excelState` **đã** tách theo section từ trước — chỉ `pendingReplace` là dùng chung.) |
| `IMP-03` | Không tồn tại thao tác thay thế đồng thời. Quan trọng hơn: phép kiểm tra trùng người **nằm giữa hai danh sách**, chỉ thấy được khi có cả hai file — làm lần lượt thì xung đột chỉ lộ ra sau khi danh sách kia đã bị xoá. |
| `IMP-04` | `ExcelImportPanel` gắn tiêu đề `excel.report.successTitle` = "Nhập Excel thành công" cho mọi report hợp lệ: không nêu danh sách đích, và dòng "Danh sách hiện tại" lấy `report.resultingCount` vốn tính theo ngữ nghĩa **append** — sau một lần replace, con số đó chưa bao giờ đúng. |
| `ID-01` | `contactMatchesNoVisitor` chỉ so **họ tên** và chỉ dùng để *mời thêm* đầu mối vào đoàn. Chiều ngược lại — người dùng chọn "— Không nằm trong danh sách đoàn —" rồi gõ đúng một người đã có trong danh sách — **không có gì phát hiện**, và không có chốt chặn nào ở submit. |
| `ID-02` | Chống trùng chỉ tồn tại **trong từng danh sách**: `excelValidator.scanRows` seed `seen` từ chính section đang import; `CampusVisitFormDtoValidator` validate `Visitors` và `ExternalSupportMembers` tách rời. Không nơi nào hợp nhất hai mảng. Hệ quả: một người khai ở cả hai chỗ → hai `guest_member_id` → từ đó xuống dưới **thật sự là hai người**. |
| `MIN-01` | `MinuteAutoFill` dedupe theo id là **đúng**; lỗi nằm ở nguồn (ID-02) đã tạo sẵn hai id cho một người. Ngoài ra DB không có ràng buộc nào chặn hai dòng cùng `guest_member_id` trong một biên bản, nên gọi API trực tiếp / hai request đồng thời vẫn tạo được trùng. |
| `MIN-02` | `minute_participants` **không có cột** nào ghi loại nguồn, và `MinuteParticipantDto.KindOf(userId, guestMemberId)` chỉ trả 3 giá trị. FE `KIND_META` map cả `GUEST` lẫn `MANUAL` vào nhãn "Khách". Phiên dịch/điều phối viên (`EXTERNAL_SUPPORT`) vì thế được ghi vào biên bản như thành viên đoàn. |
| `MIN-03` | `SaveMinutesCommandHandler.ReconcileParticipants` **hard delete** mọi dòng client bỏ khỏi danh sách. Lần "Đồng bộ người mới" sau đó thấy người đó vẫn nằm trong `visit_instance_guest_members` / `visit_participants` nên thêm lại — quyết định của Host không có chỗ nào để sống. |
| `MIN-04` | Cùng method: snapshot chỉ được ghi khi `if (isManual)`, các trường của dòng nguồn bị **bỏ qua im lặng**. FE thì mở input theo `isNewRow` (không phải `isManual`), nên một khách vừa đồng bộ (mới **và** có nguồn) hiện ra ô nhập cho tên/vai trò/đơn vị mà backend dựng lại từ `visit_guest_members` rồi vứt đi. Dropdown "Loại nguồn" cũng sửa được nhưng không nằm trong payload. |
| `UX-01` | `card.contactPickHint` (4 dòng) render thành `<p>` ngay dưới dropdown. `HelpTooltip` là `<div>` chỉ mở bằng `group-hover` — bàn phím không tới được, chạm không mở, không `aria-describedby`. |
| `PART-09` | `OrganizationCombobox.useInternalOptions` đọc `authStorage` + role rồi chọn endpoint `/partners/options`; handler của nó dùng `InternalSelectable` = APPROVED non-private **HOẶC** PENDING_APPROVAL của chính campus mình. Đường ghi phản chiếu đúng như vậy qua `EnsureFormSelectableAsync(isPublicAudience: !IsInternalAudience(user))`. Tức là **cả dropdown lẫn validate đều nới theo phiên đăng nhập**, nên hồ sơ chờ duyệt vừa được đề xuất vừa được chấp nhận. |

---

## 2. Danh sách file đã sửa

### Backend
| File | Thay đổi |
|---|---|
| `PEMS.Application/Delegations/Common/MemberDuplicatePolicy.cs` | **mới** — luật "một người, một dòng" trên danh sách hợp nhất của một cơ sở |
| `PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs` | thêm rule chống trùng trên merged list + `MemberIdentitiesOf` |
| `PEMS.Application/Partners/Common/GuestOrganizationPartnerPolicy.cs` | `RequestFormSelectable`, `EnsureRequestFormSelectableAsync`, `PARTNER_NOT_SELECTABLE` |
| `PEMS.Infrastructure/Services/VisitRequestV2CreateService.cs` | đổi sang luật của form, bỏ phụ thuộc `createdSource` |
| `PEMS.Infrastructure/Services/VisitRequestV2EditService.cs` | doc: luật form, không theo phiên |
| `PEMS.Domain/Entities/Minutes/MinuteParticipant.cs` | 3 cột mới: `SourceMemberType`, `IsOperationalContact`, `SyncState` |
| `PEMS.Domain/Enums/NewEnums.cs` | `MinuteParticipantSyncStates` |
| `PEMS.Application/Delegations/Minutes/MinuteParticipantDto.cs` | `KindOf` 4 giá trị + `SourceMemberType`/`IsOperationalContact`/`IsManual`/`SyncState` |
| `PEMS.Application/Delegations/Minutes/MinuteAutoFill.cs` | ghi loại nguồn + cờ đầu mối; dòng EXCLUDED tính là "đã có" |
| `PEMS.Application/Delegations/Minutes/MinuteChildren.cs` | map các trường mới |
| `PEMS.Application/Delegations/Minutes/GetNewMinuteParticipantsQueryHandler.cs` | candidate mang theo loại nguồn |
| `PEMS.Application/Delegations/Minutes/SaveMinutesCommand.cs` | `SyncState` trên input |
| `PEMS.Application/Delegations/Minutes/SaveMinutesCommandHandler.cs` | exclude thay hard-delete, restore theo id, từ chối sửa identity dòng nguồn |
| `MeetingMinutes/Queries/ExportMinutes/ExportMinutesPdfQueryHandler.cs` | loại dòng EXCLUDED khỏi biên bản xuất ra |
| `MeetingMinutes/Queries/ExportMinutes/ExportMinutesExcelQueryHandler.cs` | như trên |
| `MeetingMinutes/Queries/SearchAndFilterMinutes/SearchAndFilterMinutesQueryHandler.cs` | như trên (danh sách + đếm) |

### Frontend
| File | Thay đổi |
|---|---|
| `features/visit-request/utils/memberDuplicates.ts` | **mới** — bản FE của cùng luật + tìm ứng viên liên kết đầu mối |
| `features/visit-request/components/v2/CampusVisitCard.tsx` | pendingReplace theo section, Replace Both, banner trùng người, tooltip đầu mối, wording |
| `features/visit-request/components/ExcelUpload/ExcelImportPanel.tsx` | `kind` + `applied` (APPENDED/REPLACED), tiêu đề nêu rõ hành động và danh sách |
| `features/visit-request/components/shared/HelpTooltip.tsx` | `<button>`, hover/focus/click, `aria-describedby`, Escape |
| `features/visit-request/components/shared/OrganizationCombobox.tsx` | `searchMode` (mặc định `REQUEST_FORM`), bỏ đọc session, cache theo mode |
| `features/visit-request/api/visitRequestApi.ts` | doc: endpoint chọn theo use case |
| `features/visit-request/hooks/useVisitRequestFormV2.ts` | chốt ID-01 trước submit, đánh dấu field khi `PARTNER_NOT_SELECTABLE` |
| `features/visit-request/components/v2/VisitRequestFormV2.tsx` | modal "cùng một người?" |
| `features/delegations/types/delegations.types.ts` | trường mới cho participant + payload |
| `pages/dashboard/visit/MinutesCard.tsx` | 4 badge nguồn + badge Đầu mối, identity readonly, loại/khôi phục |
| `shared/i18n/locales/{vi,en}/visitRequestV2.json` | các khoá mới (excel, card, contactLink) |

### DB & test
`docs/database/scripts/patches/2026-08-15_minute_participant_source_identity.sql` (**mới**), `docs/database/scripts/PEMS_FULL_VS_31_07_NEW.sql`, `tests/PEMS.IntegrationTests/TestInfrastructure/CanonicalSqlScript.cs` (re-pin hash), và 5 file test (Mục 7).

---

## 3. Migration / schema / index đã thay đổi

`minute_participants` — **chỉ thêm**, không sửa/không xoá cột nào:

```text
source_member_type      VARCHAR(30) NULL                      -- GUEST | EXTERNAL_SUPPORT (SNAPSHOT)
is_operational_contact  TINYINT(1)  NOT NULL DEFAULT 0
sync_state              ENUM('ACTIVE','EXCLUDED') NOT NULL DEFAULT 'ACTIVE'
KEY        idx_minute_participants_sync_state    (minutes_id, sync_state)
UNIQUE KEY uq_minute_participants_minute_guest   (minutes_id, guest_member_id)
```

- `source_member_type` là **snapshot**, không phải khoá ngoại: biên bản là bản ghi lịch sử, phân loại lại thành viên tháng sau không được sửa biên bản tháng trước.
- Unique index chỉ ràng buộc dòng có nguồn khách — MySQL cho phép nhiều `NULL`, nên dòng nội bộ và dòng nhập tay không bị chặn.
- **Backfill** (§3 của patch): `source_member_type` lấy từ chính `visit_guest_members` mà dòng đang trỏ tới; `is_operational_contact` lấy từ quan hệ `visit_instance_form_details.operational_contact_guest_member_id` — **chỉ theo quan hệ, không so chuỗi tên**.
- **Patch TỪ CHỐI tạo unique index nếu dữ liệu hiện có đã trùng**, và in cảnh báo kèm truy vấn kiểm tra (§5.1/§5.2). Gộp dữ liệu trùng cũ là quyết định nghiệp vụ, không phải việc của migration.
- Idempotent (kiểm `information_schema` trước mỗi bước), có §6 hướng dẫn hoàn tác.
- Canonical script được cập nhật tương ứng và `CanonicalSqlScript.ExpectedSha256` re-pin sang `6508394f…d50`. Kiểm chứng trên seed: 60 dòng participant, **0** cặp `(minutes_id, guest_member_id)` trùng → index import sạch.

### Patch đã được CHẠY THẬT, không chỉ đọc

Dựng một DB dùng-một-lần (`pems_test_run_<32hex>`), import canonical script **bản trước patch** (retarget khỏi `pems_db`, có cổng chặn từ chối nếu còn chạm DB được bảo vệ), rồi chạy patch:

| Kiểm chứng | Kết quả |
|---|---|
| Trạng thái trước patch | 0/3 cột mới, 0/2 index mới |
| Sau lần chạy 1 | đủ 3 cột đúng kiểu/nullable/default; `uq_…` đúng là UNIQUE (`NON_UNIQUE=0`), `idx_…_sync_state` là KEY thường |
| Backfill `source_member_type` | dòng khách → `GUEST` (lấy từ `visit_guest_members`); dòng nội bộ → giữ `NULL`. Đúng ngữ nghĩa |
| Tiếng Việt sau khi ghi | "IC Staff Hà Nội" nguyên vẹn (chạy kèm `--default-character-set=utf8mb4`) |
| Lần chạy 2 (idempotent) | 5 bước đều in `note … đã có, bỏ qua`; không lỗi, không đổi dữ liệu |
| **Đường an toàn**: xoá unique index, cố ý chèn 1 dòng trùng `(minutes_id, guest_member_id)`, chạy lại patch | patch **TỪ CHỐI** tạo index, in cảnh báo `BỎ QUA tạo unique index: …`; **2 dòng trùng vẫn còn nguyên**, không gộp, không xoá |

DB dùng-một-lần đã được drop sau khi kiểm chứng.

---

## 4. API / DTO contract đã thay đổi

**Minute participant (response)** — thêm `sourceMemberType`, `isOperationalContact`, `isManual`, `syncState`; `participantKind` đổi từ 3 sang **4** giá trị `INTERNAL | GUEST | EXTERNAL_SUPPORT | MANUAL`. Dòng cũ chưa có loại nguồn đọc ra `GUEST` (hành vi cũ, chỉ là mặc định hiển thị — không ghi ngược xuống DB).

**Minute participant (save)** — `SaveMinuteParticipantInput` thêm `SyncState` *(optional)*. `null` = "giữ nguyên", nên client cũ không thể vô tình reset trạng thái của ai.

**Mã lỗi mới:**
- `SOURCE_PARTICIPANT_IDENTITY_READONLY` — sửa tên/vai trò/đơn vị của dòng có nguồn.
- `PARTNER_NOT_SELECTABLE` — đối tác không ở trạng thái đơn đăng ký được phép dẫn chiếu (thay `INVALID_MEMBER_ORGANIZATION_PARTNER`).
- `DUPLICATE_MEMBER_IN_CAMPUS` — `ErrorCode` của FluentValidation cho cặp trùng người.

Payload đơn đăng ký **không đổi**: `clientMemberKey` / `operationalContactClientMemberKey` giữ nguyên như bản stable identity đang chạy.

---

## 5. Logic frontend đã thay đổi

- **Import**: `pendingReplace` tách theo section, xác nhận render **trong** section, cuộn tới đúng khối vừa tạo; nút "Thay thế cả hai" chỉ hiện khi cả hai đang chờ và **dựng xong hai mảng mới, kiểm tra trùng, rồi mới ghi** — có xung đột thì không đụng gì cả. Đầu mối biến mất sau replace thì **báo**, không tự chọn người khác.
- **Wording**: panel nêu rõ *hành động* + *danh sách* ("Đã cập nhật danh sách khách từ file Excel" / "Đã thay thế…"), thêm dòng "Áp dụng cho", và kích thước danh sách lấy theo con số thật sau thao tác.
- **Chống trùng (ID-02)**: banner sống dưới cả hai bảng, nêu tên người và ba lựa chọn — xoá dòng thừa / chuyển người này sang nhân sự hỗ trợ (**giữ nguyên `clientMemberKey`**, không mint id mới) / đây là hai người khác nhau (đưa con trỏ tới ô cần bổ sung thông tin phân biệt).
- **Đầu mối (ID-01)**: chốt ở `onSubmit`. Khớp **duy nhất một** người theo tên + chức vụ + đơn vị thì mở modal; nhiều người khớp thì không hỏi và không liên kết. "Là người khác" được nhớ theo cặp *(campus card, member)* nên không hỏi lại trong phiên.
- **Partner picker**: chọn endpoint theo `searchMode` chứ không theo session; cache của react-select khoá theo mode. Khi backend trả `PARTNER_NOT_SELECTABLE`, FE đánh dấu đúng những dòng đang mang `organizationPartnerId` — **giữ nguyên text tên tổ chức**, chỉ yêu cầu chọn lại.
- **Biên bản**: 4 badge nguồn + badge "Đầu mối" tách riêng; identity chỉ mở input cho dòng `isManual`; dropdown "Loại nguồn" (vốn không được lưu) đã bỏ; nút xoá đổi thành "Loại khỏi biên bản" cho dòng nguồn, kèm mục "Đã loại khỏi biên bản" có nút "Khôi phục vào biên bản".
- **Tooltip**: `<button>` mở bằng hover / focus / click, `aria-describedby` chỉ gắn khi đang mở, đóng bằng Escape; `bringIntoView` bọc `scrollIntoView` để môi trường không có layout không ném lỗi ngoài error boundary.

---

## 6. Validation / backend domain policy đã thêm

- `MemberDuplicatePolicy` — vân tay = họ tên + chức vụ + (partnerId **hoặc** tên tổ chức) + quốc tịch, chuẩn hoá trim/collapse/lowercase, **giữ dấu tiếng Việt**. Trùng tên đơn thuần không bao giờ đủ. Áp trên danh sách **hợp nhất** của đúng một cơ sở (không so chéo cơ sở — mỗi cơ sở giữ bản sao độc lập theo thiết kế). Chạy trong `CampusVisitFormDtoValidator`, tức là **cả 4 đường ghi** (create, pending-edit request, pending-edit một instance, resubmit) dùng chung.
- `GuestOrganizationPartnerPolicy.RequestFormSelectable` / `EnsureRequestFormSelectableAsync` — ACTIVE + APPROVED + PUBLIC cho **mọi** người gửi. Quyền của module đối tác (`InternalSelectable`) **không đổi**.
- `SaveMinutesCommandHandler.EnsureSourceIdentityUnchanged` — chỉ từ chối khi giá trị **thay đổi thật**; client echo lại đúng dữ liệu được cấp thì đi qua bình thường.
- `ApplySyncState` — chỉ nhận `ACTIVE`/`EXCLUDED`, bỏ qua với dòng nhập tay.
- `RestoreDropped` — người được thêm lại dùng **lại dòng cũ**, không chèn dòng thứ hai (điều mà unique index cũng chặn ở tầng DB).

---

## 7. Test mới, test cập nhật và kết quả chạy

**Mới:**
- `tests/PEMS.UnitTests/Delegations/MemberDuplicatePolicyTests.cs` — 14 test (luật + boundary 4 đường ghi)
- `tests/PEMS.UnitTests/Partners/RequestFormPartnerSelectionTests.cs` — 7 test
- `tests/PEMS.UnitTests/Delegations/Minutes/MinuteParticipantSourceKindTests.cs` — 4 test
- `tests/PEMS.IntegrationTests/VisitRequests/MinuteParticipantSourceIdentityTests.cs` — 5 test (MIN-01..04 đầu-cuối, DB thật)
- `tests/PEMS.IntegrationTests/VisitRequests/RequestFormPartnerSelectableTests.cs` — 10 test (partner seed thật)
- `frontend/…/__tests__/memberDuplicates.test.ts` — 17 test
- `frontend/…/__tests__/HelpTooltip.test.tsx` — 8 test

**Cập nhật:**
- `CampusVisitCardExcel.test.tsx` — testid theo section; **thêm 4 test**: hai xác nhận cùng tồn tại, Replace Both nguyên tử, Replace Both bị chặn khi trùng người (không đổi dữ liệu cũ), banner trùng chéo + xoá dòng thừa.
- `excelImportReport.test.tsx` — prop `kind`; assert tiêu đề nêu danh sách đích.
- `CreateVisitRequestV2CommandValidatorTests.cs` — hai test "trần 200 khách" trước đây dựng **200 bản sao của cùng một người**, nay dùng `Roster(n)` gồm người khác nhau. Đây là *sửa đúng ý định của test*, không phải nới luật: 200 dòng y hệt nhau là một người khai 200 lần, và bị từ chối có chủ đích.

**Kết quả:**

| Gate | Kết quả |
|---|---|
| `dotnet test` (Unit) | **2733 passed / 0 failed** |
| `dotnet test` (Integration) | **1938 passed / 0 failed** |
| `dotnet test` (Architecture) | **28 passed / 0 failed** |
| `npm run lint` (`tsc --noEmit`) | **0 lỗi** |
| `npm run test:unit` | **148 file / 2377 passed / 0 failed** |
| `npm run build` | **thành công** |
| `npm run test:e2e` | **166 passed / 45 failed** — **bằng đúng baseline**, xem Mục 8 |
| Patch SQL chạy thật trên DB dùng-một-lần | nâng cấp + backfill + idempotent + từ chối khi có trùng — xem Mục 3 |

Toàn bộ số liệu trên đo **sau khi** khôi phục cây làm việc từ bước đo baseline (Mục 8), và diff sau khôi phục **giống hệt từng byte** bản backup trước đó.

> Chạy `dotnet test` phải đặt output **trong repo** (`-p:BaseOutputPath=".testout/"`). Đặt vào `%TEMP%` làm 17 suite quét source theo repo-root fail giả (`FindRepositoryRoot` đi lên từ thư mục binary).

---

## 8. Rủi ro, dữ liệu legacy, baseline failure và việc chưa hoàn thành

### E2E: 45 failure — **đo được là baseline có sẵn**, thay đổi này gây **0 regression**

Không suy đoán. Đã **stash toàn bộ thay đổi**, chạy lại nguyên bộ E2E trên HEAD sạch, rồi khôi phục:

| Lần chạy | Kết quả |
|---|---|
| HEAD sạch (baseline) | **166 passed / 45 failed** (211 test) |
| Có thay đổi này | **166 passed / 45 failed** (211 test) |

Con số **trùng khít**. Cây làm việc sau khi khôi phục được đối chiếu với bản backup diff lấy trước lúc stash: **giống hệt từng byte**; 12 stash có sẵn từ trước không bị đụng tới.

Hai nhóm failure lớn nhất và nguyên nhân:

| Spec | Nguyên nhân |
|---|---|
| `excel-i18n.spec.ts` (10) | Spec assert `result.valid`, một trường **`ExcelImportReport` không có** (nó có `errorRows` + `canApplyImport`) → nhận `undefined`. |
| `visit-request-percampus-v2.spec.ts` (2) | Spec tìm `input[name="campusVisits.0.delegationName"]`, nhưng field đó là `<textarea>` do `AutoGrowTextField` render qua `Controller` — **không có** thuộc tính `name`. `campus-delegation-input` đã tồn tại trong `CampusVisitCard.tsx` **ở HEAD** (dòng 994). |
| 33 test còn lại | Rải trên `email-template-rendering`, `i18n-public-runtime`, `staff-leader-attending-tab`, `visit-prep-capability-split`, `visit-request-single-form`. `visit-request-single-form` fail ngay ở bước mở form (dòng 29, `gotoForm`) — trước khi chạm bất kỳ code nào trong phạm vi này. |

Không tự sửa các spec này — nằm ngoài phạm vi được duyệt (kế hoạch Mục 20: *"Không tự ý sửa unrelated tests trong cùng patch nếu chưa được duyệt scope"*). Bộ E2E đang nợ **45/211**; đề xuất một patch riêng để dọn.

### Dữ liệu legacy — cảnh báo, không tự gộp
- Patch **không tạo** unique index nếu DB đang có trùng, và in truy vấn §5.1 để xử lý tay.
- §5.2 cung cấp truy vấn tìm **nghi vấn cùng người khác id** (di sản MIN-01). Đây là danh sách để hỏi lại người dùng — không có bước tự gộp nào.
- Biên bản cũ không có `source_member_type` hiển thị là "Khách" (đúng hành vi cũ, và là loại phổ biến hơn hẳn).

### Rủi ro cần biết trước khi deploy
1. **Phải chạy patch SQL trước khi deploy code** trên mọi DB đã import. Thiếu cột → mọi truy vấn `minute_participants` fail. Nhớ `--default-character-set=utf8mb4`.
2. **PART-09 thu hẹp có chủ đích**: Staff/Staff Leader **không còn** chọn được hồ sơ `PENDING_APPROVAL` của campus mình, và cả hồ sơ `APPROVED` nhưng `visibility = INTERNAL`, trong form đăng ký. Đây đúng như Mục 3.3 yêu cầu, nhưng **đảo lại** một quyết định trước đó (PART-03: "staff cần bộ internal để khỏi gõ lại"). Hệ quả thực tế: đơn vị công tác của khách trong các trường hợp đó sẽ lưu thành **text tự do** (mất `organization_partner_id`) cho tới khi hồ sơ được duyệt + công khai. Quyền xem trong module Đối tác **không đổi**.
3. **Chống trùng có thể chặn đơn đang hợp lệ hôm nay**: hai dòng giống nhau ở *mọi* trường sẽ bị từ chối. Lối thoát luôn có (bổ sung chức vụ / đơn vị / quốc tịch khác nhau), và FE hỏi trước khi tới backend.
4. **Đổi ngữ nghĩa xoá trong biên bản**: bỏ một người nguồn khỏi danh sách nay là *exclude*, không phải delete. Client cũ (tab chưa reload) gửi payload thiếu dòng sẽ tạo dòng EXCLUDED — đúng ý định, nhưng khác kết quả cũ.

### Chưa làm (có chủ ý, kèm lý do)
- **Mục 16 — sync response mở rộng** (`addedCount` / `restoredCount` / `skippedCount` / `conflicts[]` / `participants[]`): kế hoạch ghi "**nên** trả", không nằm trong Definition of Done. Giữ nguyên shape `List<MinuteParticipantDto>` để không phá client hiện có. Việc chống trùng và tôn trọng exclude **đã** hoạt động qua `existing` — các con số chỉ là tiện lợi báo cáo.
- **Mục 10 — legacy conflict UI cho hai `guest_member_id` của cùng một người**: mới có **truy vấn báo cáo** (§5.2 của patch), chưa có luồng resolve trên giao diện. Nguyên tắc "không tự gộp" được giữ. `MinuteAutoFill` vẫn bỏ qua khách trùng vân tay với dòng **nội bộ** (cơ chế cũ, không đổi).
- **Test E2E cho 12 kịch bản Mục 19.4**: chưa viết. Bốn kịch bản quan trọng nhất (import riêng từng danh sách, Replace Both, xung đột cùng người ở hai nhóm, xoá dòng trùng) đã có ở tầng component với DOM thật của form; phần biên bản đã có ở tầng integration với DB thật. Bộ E2E hiện có sẵn 12 baseline failure nên thêm spec mới vào đó lúc này sẽ khó đọc kết quả.
- **`VisitLinkSupport` / partner-link theo participant** vẫn nhìn thấy dòng EXCLUDED. Có chủ ý: liên kết đối tác nói về *người khách*, không về việc họ có trong biên bản nào hay không.
