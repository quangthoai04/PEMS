# Báo cáo triển khai — Báo cáo nhập Excel · Bộ chọn ngày giờ · Độ dài & auto-grow

> Trả lời theo khung 14 mục của §30 trong `PEMS_PROMPT_EXCEL_IMPORT_REPORT_GOOGLE_CALENDAR_DATETIME_TEXT_LENGTH_AUTOGROW.md`.
> Mọi kết luận dưới đây đều kèm bằng chứng: file/dòng, tên test, hoặc lệnh đã chạy thật.

---

## 1. Branch và HEAD trước/sau

| | |
|---|---|
| Branch | `Canh-Iter1` |
| HEAD trước | `c434e5b3` (docs — contact actions / transfer UI / OTP draft) |
| HEAD sau | `7b83fb4c` |
| Chưa push | Đúng. Không rebase, không force-push, không xoá WIP. |

Nhánh `msg-repaired` từ đợt trước **vẫn còn nguyên** — em không đụng vào.

## 2. WIP / commit hiện có

Cây làm việc trước khi bắt đầu chỉ có một thứ chưa theo dõi: chính file prompt
(`docs/Ver2Carnh/canh/validate/`). `git diff --check` sạch cả trước và sau.

Ba commit code, mỗi commit **tự build được** (không phải chỉ commit cuối):

| Commit | Nội dung |
|---|---|
| `6c38d743` | `fix(visit-validation): bound every field its column bounds` |
| `77ad29cc` | `feat(visit-ui): add a calendar-style schedule picker and a wrapping text field` |
| `7b83fb4c` | `feat(visit-ui): report what an Excel import actually did, and stop it deleting typed rows` |

Em **kiểm chứng** chứ không suy đoán chuyện "mỗi commit build được": tạo worktree tại
`77ad29cc`, junction `node_modules`, chạy `tsc --noEmit` → 0 lỗi, và chạy
`visitDateTimeRangePicker.test.tsx` tại đúng commit đó → 17/17 pass. Worktree đã gỡ.

> Lần thử đầu em dùng `git checkout <commit> -- src` để kiểm tra, và nó cho ra 15 "lỗi"
> giả — vì lệnh đó không xoá các file được thêm ở commit SAU. Kết quả đó vô nghĩa và đã
> bị thay bằng phép thử worktree ở trên.

## 3. Files changed

**Backend (2 file + 2 file test mới)**

- `backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/VisitRequestV2SharedValidators.cs`
- `backend/PEMS.Application/Delegations/Commands/CreateVisitRequestV2/CreateVisitRequestV2CommandValidator.cs`
- `tests/PEMS.UnitTests/VisitRequests/VisitRequestV2LengthAndScheduleTests.cs` *(mới, 20 test)*
- `tests/PEMS.IntegrationTests/VisitRequests/VisitScheduleMultiDayV2Tests.cs` *(mới, 4 test)*

**Frontend — file mới**

- `components/shared/visitDateTime.ts` — số học wall-clock thuần
- `components/shared/TimeSelect.tsx` — combobox giờ, portal
- `components/shared/VisitDateTimeRangePicker.tsx` — bộ chọn dùng chung
- `components/shared/AutoGrowTextField.tsx` — ô một dòng biết xuống dòng
- `components/ExcelUpload/ExcelImportPanel.tsx` — panel báo cáo import
- 4 file test unit + 1 spec real-stack

**Frontend — file sửa**

- `components/ExcelUpload/excelValidator.ts` (viết lại), `excelDownload.ts` (+ báo cáo lỗi)
- `components/v2/CampusVisitCard.tsx`, `VisitRequestFormV2.tsx`
- `hooks/useVisitRequestFormV2.ts`, `schema/visitRequestV2.schema.ts`
- `components/shared/OrganizationCombobox.tsx`
- `pages/dashboard/visit/EditVisitRequestV2Page.tsx`
- 4 file i18n (VI + EN × `visitRequest`, `visitRequestV2`)
- 4 file real-stack (3 spec + `realstackHelpers.ts`)

---

## 4. Excel semantics: append / replace / partial / fail-whole

| Câu hỏi | Trước | Sau |
|---|---|---|
| Import mặc định | **replace** — `visitorFields.replace(...)` ([CampusVisitCard.tsx:127](../../../../frontend/pems-react/src/features/visit-request/components/v2/CampusVisitCard.tsx) bản cũ) | **append** |
| Replace | không có lối riêng | nút riêng + hộp xác nhận nói rõ sẽ xoá bao nhiêu người |
| File có dòng lỗi | vẫn `return` sớm nhưng chỉ hiện `errors[0]` | **không đổi form gì cả**, panel nói thẳng "Biểu mẫu chưa thay đổi" |
| Partial import | không có | **không thêm** — đúng mặc định an toàn §4.4; nhập nửa danh sách để người dùng tự đoán phần nào đã vào là tệ hơn |
| Dòng trùng | bỏ qua, nhưng chỉ so trong file | bỏ qua **và** đếm; so với cả dữ liệu đang có trên form |
| Hàng trống mẫu | không xử lý | hàng placeholder chưa gõ gì bị bỏ trước khi append, để danh sách không có lỗ |

Điểm đáng chú ý nhất: `replace()` là lý do người dùng gõ tay vài người rồi upload
phần còn lại — cách dùng tự nhiên nhất — bị **mất sạch phần đã gõ**, không cảnh báo,
không undo. Test `ADDS to the list instead of replacing what was typed by hand`
(`CampusVisitCardExcel.test.tsx`) khoá lại hành vi này.

## 5. Report fields và định dạng file

Panel trên màn (`ExcelImportPanel.tsx`) — mỗi section một state riêng:

- **Đang xử lý:** spinner + `Đang kiểm tra file {tên}...`, chỉ khoá nút import của **đúng** section đó.
- **Thành công (xanh):** tên file · tổng số dòng · đã nhập · bỏ qua do trùng · danh sách hiện tại `n/200`.
- **Lỗi (đỏ):** tổng · hợp lệ · lỗi · trùng · vượt giới hạn + **bảng `Dòng | Cột | Nội dung lỗi` liệt kê đủ**, cuộn trong khung, kèm `[Tải báo cáo lỗi] [Chọn file khác] [Đóng]`.

File tải xuống: **`.xlsx`** (`downloadExcelErrorReport`), tên `{tên-file-gốc}-error-report.xlsx`,
gồm khối tóm tắt (tên file · loại dữ liệu · cơ sở · thời gian kiểm tra · tổng/hợp lệ/lỗi/trùng/vượt giới hạn)
rồi tới danh sách row/column/message. Đủ 10 mục §5.

## 6. Date/time component và hành vi múi giờ

`VisitDateTimeRangePicker` — dùng chung cho **public create · authenticated create ·
dashboard modal · full-page create · pending edit · resubmit**, vì cả sáu đều đi qua
`CampusVisitCard`. Amendment modal **chưa** dùng (xem mục 13).

Contract **không đổi**: vẫn hai chuỗi wall-clock `startDatetime` / `endDatetime`. Tách
ngày/giờ chỉ là chuyện trình bày, nên **bản nháp cũ vẫn khôi phục được** — không cần migrate.

Số học nằm ở `visitDateTime.ts` và đi qua `Date.UTC` như một **vật mang thuần tuý**,
không bao giờ qua múi giờ máy. Nhờ vậy trình duyệt ở Los Angeles tính ra cùng thời lượng
với ở Hà Nội, và cộng 1 giờ vào 23:30 lật ngày giống hệt nhau. Test
`computes a duration without the host timezone getting a vote` giữ điều này.

## 7. Quy tắc cùng ngày / khác ngày

- Mặc định **cùng ngày**: một ô ngày + giờ bắt đầu + giờ kết thúc. Ô ngày thứ hai chỉ xuất hiện khi bật.
- Dropdown giờ kết thúc sinh **từ** giờ bắt đầu, bước 15 phút, mỗi lựa chọn kèm thời lượng (`09:00 · 1 giờ`). Ở chế độ cùng ngày danh sách dừng ở nửa đêm — vượt qua thì không còn là cùng ngày.
- Đổi giờ bắt đầu chỉ **gợi ý lại** giờ kết thúc khi nó chưa được chọn chủ động hoặc đã thành bất khả thi. Buổi 3 tiếng người dùng cố ý chọn **không bị rút về 1 tiếng**.
- Mở một lịch qua đêm đã lưu → tự vào chế độ khác ngày.
- Ba luật hiển thị **realtime** ngay trong component (form validate lúc submit): kết thúc trước bắt đầu · tối thiểu 30 phút · thời gian báo trước. **Không auto-swap.**
- Số giờ báo trước là **tham số truyền vào** (72h tạo mới / 24h sửa–gửi lại), không hardcode, nên picker và Zod schema không thể nói khác nhau trên cùng màn.

## 8. Field length matrix (DB / backend / frontend)

| Field | DB | Backend | Frontend | Counter | Auto-grow | Kết luận |
|---|---:|---:|---:|---|---|---|
| registrant_full_name | VARCHAR(150) | 150 | 150 | – | – | khớp |
| registrant_organization | VARCHAR(200) | 200 | 200 | – | – | khớp |
| registrant_job_title | VARCHAR(150) | 150 | 150 | – | – | khớp |
| registrant_nationality | VARCHAR(100) | 100 | 100 | – | – | khớp |
| registrant_phone | VARCHAR(50) | **50 (thêm)** | regex | – | – | **đã vá** |
| registrant_email | VARCHAR(150) | 150 | 150 | – | – | khớp |
| contact_person_* | 150/255/50/150 | 150/200/**50**/150 | 150/200/regex/150 | – | – | **đã vá** (phone) |
| delegation_name | VARCHAR(200) | 200 | 200 | ✓ | ✓ | **thêm message** |
| visit_type_other | VARCHAR(255) | 200 | 200 | ✓ | ✓ | **thêm message** |
| purpose | TEXT | 2000 | 2000 | ✓ | ✓ | khớp |
| working_content | TEXT | 4000 | 4000 | ✓ | ✓ | khớp |
| transportation_note | TEXT | 2000 | 2000 | ✓ | ✓ | khớp |
| media_consent_note | TEXT | **2000 (thêm)** | 2000 | ✓ | ✓ | **đã vá** |
| note_to_fptu (`notes`) | TEXT | 2000 | 2000 | ✓ | ✓ | **thêm message** |
| operational_contact_full_name | VARCHAR(150) | 150 | 150 | ✓ | ✓ | khớp |
| operational_contact_organization | VARCHAR(255) | 200 | 200 | ✓ | ✓ | khớp |
| operational_contact_phone | VARCHAR(50) | **50 (thêm)** | regex | – | – | **đã vá** |
| operational_contact_email | VARCHAR(150) | 150 | 150 | – | – | khớp |
| guest / support · full_name | VARCHAR(150) | 150 | 150 | ✓ | ✓ | khớp |
| guest / support · job_title | VARCHAR(150) | 150 | 150 | ✓ | ✓ | khớp |
| guest / support · organization | VARCHAR(200) | 200 | 200 | – | ✓ (wrap) | khớp |
| guest / support · nationality | VARCHAR(100) | 100 | 100 | – | – | khớp |
| Số người / cơ sở | – | 200 | 200 | ✓ | – | **Excel nay cũng chặn** |

## 9. Field được tăng giới hạn

**Không có.** Không giới hạn nào được nới. Ba chỗ là **thêm** ràng buộc còn thiếu ở
backend (`media_consent_note`, ba trường phone), phần còn lại chỉ là thêm **message**
cho luật đã có. Do đó **không có migration**, và không dữ liệu cũ nào trở thành không hợp lệ.

Hai lỗ hổng đó có hậu quả thật:

- `media_consent_note` **không có luật độ dài nào** ở backend. Form chặn 2000, cột là TEXT → ghi chú quá dài lọt vào DB, rồi **màn sửa từ chối lưu lại chính giá trị đó**: lưu được một lần, mãi mãi không lưu lại được.
- Không trường phone nào có `MaximumLength`, mà cả ba cột đều `VARCHAR(50)` → số quá dài trả về lỗi truncation của MySQL thay vì một câu gọi đúng tên trường.

Và bốn `.max()` phía frontend ship **không có message** (`delegationName`,
`visitTypeOther`, `mediaConsentNote`, `notes`) → người dùng tiếng Việt dán quá tay
nhận được default tiếng Anh của Zod.

## 10. Auto-grow coverage

**`AutoGrowTextarea`** (nhiều dòng, có counter): Mục đích · Nội dung làm việc ·
Ghi chú phương tiện · Ghi chú truyền thông · Ghi chú cho FPTU. *(Lý do chuyển giao và
lý do amendment/reject/cancel đã dùng từ đợt trước.)*

**`AutoGrowTextField`** (một dòng nhưng biết xuống dòng): Tên đoàn · Loại hình khác ·
Họ tên & Chức vụ trong bảng khách/hỗ trợ · Họ tên & Đơn vị của đầu mối phối hợp.
Bên dưới là `<textarea>` nhưng hành xử như `<input>`: Enter không chèn gì, dán nhiều
dòng bị gộp thành khoảng trắng → **giá trị lưu xuống y hệt `<input>` sẽ lưu**.

**Không áp cho** email · phone · date · time · select · quốc tịch — đúng §21.2.

`OrganizationCombobox` bỏ chiều cao cố định 44px: react-select đang cắt tên tổ chức dài
thành `…`, mà mục đích của ô đó là đọc lại được. Nay wrap và **kéo cao dòng bảng**.

Counter chỉ hiện từ 80% giới hạn trở đi (đỏ khi vượt) — gắn counter vào mọi ô tên chỉ
tạo nhiễu. `maxLength` **không** đặt lên DOM, để trình duyệt không cắt âm thầm; test
`does not put maxLength on the DOM node` giữ điều này.

## 11. Tests / gates / E2E

Toàn bộ chạy thật, lệnh nguyên văn ở §27:

| Gate | Lệnh | Kết quả |
|---|---|---|
| Backend build | `dotnet build PEMS.slnx` | **0 lỗi**, 196 warning |
| Architecture | `dotnet test tests/PEMS.ArchitectureTests` | **14/14** |
| Unit | `dotnet test tests/PEMS.UnitTests` | **1072/1072** (trước 1052 → **+20**) |
| Integration | `dotnet test tests/PEMS.IntegrationTests` | **626/626** (trước 622 → **+4**) |
| FE lint | `npm run lint` | **0 lỗi** |
| FE unit | `npx vitest run` | **622/622** trên 48 file (trước 554/44 → **+68**) |
| FE build | `npm run build` | built |
| Real-stack E2E | `npm run test:e2e:realstack` | **29/29** (trước 24 → **+5**) |
| Whitespace | `git diff --check` | sạch |

Test mới, theo đúng thứ tự §22–§26:

- `excelImportReport.test.tsx` (16) — tổng/hợp lệ/trùng, **mọi** ô lỗi, độ dài từng cột, trùng với form, chặn quá 200, workbook hỏng, thiếu cột, không phải Excel, hai section tách nhau, panel VI + EN, nội dung file báo cáo tải xuống.
- `CampusVisitCardExcel.test.tsx` (6) — append chứ không replace, bỏ hàng placeholder, file lỗi không đổi form, hai section báo cáo riêng, replace phải qua xác nhận, file hỏng không treo nút.
- `visitDateTimeRangePicker.test.tsx` (17) — helper wall-clock, mặc định cùng ngày, gợi ý +1 giờ, **không** đè end đã chọn, option kèm thời lượng, lỗi realtime, 29 phút hỏng / 30 phút đạt, bật khác ngày, mở sẵn multi-day, min-advance theo tham số, điều khiển bằng bàn phím, Escape, **không bị modal cắt** (khẳng định list nằm ngoài khung cuộn), gõ sai không phá giá trị cũ.
- `fieldLengthAutoGrow.test.tsx` (29) — 11 field × (đúng giới hạn pass / hơn 1 ký tự fail) có message thật, tiếng Việt, Excel dùng **cùng** giới hạn với form, và 5 test hành vi của `AutoGrowTextField`.
- `VisitRequestV2LengthAndScheduleTests.cs` (20) — biên hai phía từng field, phone quá dài, email 150, mọi message là tiếng Việt, end ≤ start, đúng 30 phút, kết thúc ngày khác, `DateTime.Kind` không đổi phán quyết.
- `VisitScheduleMultiDayV2Tests.cs` (4) — MySQL thật: qua đêm 22:00→01:00, 54 giờ, 07:00 vẫn là 07:00 (nếu qua UTC sẽ rơi về nửa đêm), và ghi chú đúng 2000/4000 ký tự sống sót vòng lưu–đọc.
- `excel-schedule-length.realstack.spec.ts` (5) — file hợp lệ → báo cáo → bảng → submit thật; file lỗi → bảng lỗi đầy đủ → tải báo cáo → form nguyên vẹn; cùng ngày 08:00–09:00; qua đêm 22:00→01:00; vượt độ dài → counter đỏ → chặn submit → sửa → submit được.

## 12. Database migration impact

**Không có.** Không thêm/sửa/xoá cột, bảng, index, trigger, enum nào. Mọi giới hạn mới
đều **chặt hơn hoặc bằng** cột hiện có, nên không dữ liệu nào đang nằm trong DB trở
thành không hợp lệ. Không cần chạy script gì trước khi deploy.

## 13. Known limitations

1. **`VisitAmendmentSubmitModal` vẫn dùng `datetime-local`.** §16 nói "Amendment **nếu dùng cùng schedule model**" — modal này có model lịch riêng (`plannedStartAt`/`plannedEndAt` trên một đề xuất, kèm baseline revision), không đi qua `CampusVisitCard`. Đổi nó là một việc riêng, không phải phần đuôi của việc này.
2. **`LogisticsRequestSection`, `VisitProcess`, `MinutesCard` vẫn dùng `datetime-local`.** Nằm ngoài luồng đăng ký; §16 chỉ liệt kê các màn create/edit/resubmit.
3. **E2E chứng minh gì về schedule.** Panel thành công hiển thị lại `values` của form, nên nó là tiếng vọng của UI chứ không phải bằng chứng DB. Bằng chứng phía DB nằm ở `VisitScheduleMultiDayV2Tests` (đọc lại sau khi `ChangeTracker.Clear()`). Ở E2E, sức nặng nằm ở chỗ **server chấp nhận** — nếu wall-clock bị lệch trên đường truyền, backend sẽ trả 400 "kết thúc trước bắt đầu" hoặc "dưới 30 phút", nên một đơn được tạo thành công là bằng chứng gián tiếp nhưng thật.
4. **Không có nút "Nhập các dòng hợp lệ".** §4.4 cho phép thêm "khi đã được duyệt rõ" — chưa có duyệt nên em không thêm.
5. **`visit_type_other` DB rộng 255 nhưng chặn ở 200.** Giữ nguyên như cũ để backend và frontend nói cùng một con số; nới lên 255 là quyết định nghiệp vụ, không phải việc của lần này.
6. **Bước 15 phút là gợi ý, không phải ràng buộc.** Người dùng gõ tay phút bất kỳ (08:07 hợp lệ). Đúng §11 "cho nhập thủ công".

## 14. Resume point

Cây sạch tại `7b83fb4c`, chưa push. Việc tiếp theo tuỳ anh quyết:

1. **Đợt trước còn treo:** `git rebase --onto msg-repaired 8850b87e Canh-Iter1` để nhận lại message 7 commit cũ. Nhánh `msg-repaired` vẫn còn.
2. **Push + PR về `Dev`.**
3. Nếu muốn: chuyển `VisitAmendmentSubmitModal` sang cùng bộ chọn (mục 13.1).

Lưu ý môi trường: `mysql` không có trong PATH máy này, real-stack phải chạy kèm
`MYSQL_BIN="C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe"`.
