# BÁO CÁO TRIỂN KHAI — V2 Detail: Outcome, Contact, Revision, History, Toast

> Thực hiện `PEMS_V2_DETAIL_STATUS_CONTACT_HISTORY_TOAST_AUDIT_AND_PLAN_BB8A3B85.md`.

## 1. Branch và HEAD

| | |
|---|---|
| Branch | `Canh-Iter1` |
| HEAD trước | `bb8a3b85` (đúng baseline của plan) |
| HEAD sau | `fbfd5a5e` |
| Số commit | 6 (Slice 1→6) |
| Trạng thái | Chưa push |

> Lưu ý: 8 commit của đợt trước đã được cherry-pick sang `fix-commits`, amend, merge vào `Dev`,
> rồi `Canh-Iter1` fast-forward tới `bb8a3b85`. Em đã xác minh code của đợt đó **có mặt đầy đủ** ở
> HEAD (`RegistrantIdentityRules.cs`, `components/v2/shared/*`, `emailIdentity.ts`, `V2SeedActor.cs`)
> trước khi làm tiếp — không reset, không rebase gì.

## 2. Files changed

| Commit | Nội dung |
|---|---|
| `f95732c3` | `VisitRequestV2DetailView.tsx`, **MỚI** `shared/VisitOutcomeSummary.tsx`, i18n VI/EN, 2 test |
| `a0ffeca0` | `ResolvedVisitFormDto.cs`, `VisitFormReadService.cs`, `visitRequestV2Api.ts`, 5 fixture test, `PerCampusFormV2ReadTests.cs`, `CanonicalSqlScript.cs` |
| `46b0d11f` | **MỚI** `ContactIdentityActions.tsx` (thay `ContactIdentityPanel.tsx` — đã xoá), i18n, 2 test |
| `c0ae3869` | **MỚI** `shared/campusRevisionState.ts`, `CampusVisitDetailCard.tsx`, i18n, 2 test |
| `9787f236` | `VisitAmendmentCommandContracts.cs`, `GetVisitRequestHistoryQueryHandler.cs`, `VisitHistoryTimeline.tsx`, `visitRequestV2Api.ts`, i18n, **MỚI** `VisitRequestHistoryV2Tests.cs` + `VisitHistoryTimeline.test.tsx` |
| `fbfd5a5e` | `VisitRequestManagement.tsx`, `shared/utils/toast.ts`, `VisitSafeEditModal.tsx`, `VisitAmendmentSubmitModal.tsx`, `VisitAmendmentPanel.tsx`, `VisitRequestV2DetailView.tsx`, **MỚI** `visitToastStandardization.test.ts` |

## 3. DTO/API changes

**Additive, KHÔNG migration** — mọi cột đã có sẵn trên entity.

| DTO | Field thêm |
|---|---|
| `ResolvedVisitFormDto` | `CancelledByUserId` · `CancelledByName` · `CancelledAt` · `CancellationReason` |
| `ResolvedCampusVisitDto` | 4 field trên + `CancellationActorType` · `CancellationSource` |
| `VisitHistoryEntryDto` | **Thay hẳn shape**: `Kind/Title/Detail` → `EventCode` + `CampusName` + `FormRevision`/`ApprovalRevision`/`AmendmentNo`/`StatusCode`/`SourceType`/`Reason`/`MaskedEmail`/`FromStatus`/`ToStatus` |

> `VisitHistoryEntryDto` là **breaking change** cho client. Chỉ có `VisitHistoryTimeline.tsx` tiêu thụ nó
> và đã cập nhật cùng commit.

## 4. UI changes

| Finding | Trước | Sau |
|---|---|---|
| **A** | `<Link>` nhãn "Lưu thay đổi" / "Gửi lại đơn" — không lưu gì | "Sửa đơn" / "Sửa & gửi lại đơn"; nhãn save chỉ còn trên nút submit thật |
| **B** | Overview lặp lại người đăng ký + đầu mối ngay trên Section 1/2 | Overview = mã đơn · trạng thái · số cơ sở · thời điểm gửi · **tóm tắt tình trạng** · actions |
| **C** | `ContactIdentityPanel` là card riêng phía trên Section 1 | Nhúng trong Section 2, dưới chính thông tin nó thao tác |
| **D** | Luôn in "Nội dung v1 · Phê duyệt v1" | Wording theo lifecycle (6 trạng thái) |
| **E** | `source=CREATE;approvalRevision=1`, `Cơ sở: REJECTED`, `PENDING→APPLIED` | Câu nghiệp vụ dựng từ event code, có tên người + tên cơ sở |
| **F** | Toast hủy đơn ở **bottom-right** | Toast chung **top-right** |

### Tóm tắt tình trạng — quy tắc scope

Đếm **chỉ** từ `campusVisits` backend trả về cho chính người xem. Staff Leader scope 1 cơ sở trong đơn
3 cơ sở thấy "1 cơ sở đã tiếp nhận" và **không** biết 2 cơ sở kia tồn tại, quyết định gì, ai quyết, lý do
gì. Không suy ra từ bất kỳ tổng nào ở cấp request — đó là cách duy nhất giữ được bảo đảm scope.

## 5. Contact identity changes

Giữ nguyên toàn bộ workflow claim/replace/transfer/resend/cancel và **không nới** manager relation.
Sửa 3 lỗi phát hiện khi refactor:

1. **Lỗi load transfer bị nuốt thành "không có transfer"** — hai điều khác hẳn nhau; nó mời user tạo
   transfer thứ hai trong khi đã có một cái đang chạy. Nay là lỗi inline + nút thử lại.
2. Kết quả mutation từ inline message (có thể đã cuộn khỏi màn hình) → toast chung.
3. Chuỗi tiếng Việt hardcode + `toLocaleString('vi-VN')` → i18n VI/EN + formatter wall-clock.

## 6. Revision/history changes

### Revision (§9)

Đã xác minh bằng code: `VisitRequestV2CreateService.cs:190,261` gán `ApprovalRevision = 1` **ngay lúc
tạo**. Nên `approvalRevision > 0` **không** có nghĩa đã duyệt — đúng cảnh báo của plan. Wording nay suy
từ `instanceStatus` + `decidedAt` + `activeAmendment`. **Không** ghi lại số revision trong DB: chúng là
token optimistic-concurrency và baseline của amendment.

Khi `instanceStatus` và `decidedAt` mâu thuẫn, helper đọc là "chưa quyết định" — an toàn hơn là công bố
một phê duyệt chưa từng được ghi.

### History (§10)

Ngoài việc thay chuỗi kỹ thuật, sửa 2 lỗi thật:

- **Actor name không bao giờ được gắn.** Handler dựng dictionary tên rồi… không dùng
  (`GetVisitRequestHistoryQueryHandler.cs:171-176` cũ, mọi entry truyền `null`). "Ai làm" thiếu trên
  toàn bộ tính năng mà không có gì báo lỗi.
- **Thiếu tên cơ sở** — đơn 3 cơ sở sinh 3 dòng "nội dung được tạo" giống hệt nhau.

Thêm: hủy campus trước đây **không xuất hiện** trong timeline (khác với quyết định); nay có event riêng.

## 7. Toast migrations

| Nơi | Trước | Sau |
|---|---|---|
| `VisitRequestManagement` (reject/accept/assign/cancel/approve) | viewport riêng `fixed bottom-5 right-5` | toast chung top-right |
| `VisitSafeEditModal` | panel success **không thể tới được** (parent đóng modal ngay) | toast + giữ conflict inline |
| `VisitAmendmentSubmitModal` | đóng im lặng | toast; lỗi mã ổn định giữ inline |
| `VisitAmendmentPanel` | inline trong panel sẽ biến mất | toast |
| `EditVisitRequestV2Page` → detail | `state.flash` gửi đi mà **không ai đọc** | detail hiện đúng 1 lần rồi xoá state |

`shared/utils/toast.ts` được bổ sung nhánh `data.errors` (FluentValidation) — nếu xoá `apiErrorMessage`
cục bộ mà không làm việc này thì các lỗi đó sẽ tụt xuống thông báo chung chung trong khi server đã nói
rõ sai field nào.

## 8. Tests added

| Tầng | File | Số test |
|---|---|---|
| FE unit | `VisitOutcomeSummary.test.tsx` | 10 |
| FE unit | `ContactIdentityActions.test.tsx` | 6 |
| FE unit | `campusRevisionState.test.ts` | 10 |
| FE unit | `VisitHistoryTimeline.test.tsx` | 8 |
| FE guard | `visitToastStandardization.test.ts` | 16 |
| FE unit | `VisitRequestV2DetailView.test.tsx` (+3), `CampusVisitDetailCard.test.tsx` (+1) | 4 |
| BE integration | `VisitRequestHistoryV2Tests.cs` | 6 |
| BE integration | `PerCampusFormV2ReadTests.cs` (+3 cancellation) | 3 |
| | **Tổng mới** | **63** |

## 9. Test result

| Gate | Lệnh thật | Kết quả |
|---|---|---|
| Backend build | `dotnet build PEMS.slnx` | ✅ 0 errors (193 warnings, có sẵn) |
| ArchitectureTests | `dotnet test tests/PEMS.ArchitectureTests` | ✅ **14/14** |
| UnitTests | `dotnet test tests/PEMS.UnitTests` | ✅ **1052/1052** |
| IntegrationTests | `dotnet test tests/PEMS.IntegrationTests` | ✅ **611/611** (trước: 602) |
| FE typecheck | `npm run lint` | ✅ 0 errors |
| FE unit | `npm run test:unit` | ✅ **526/526**, 43 file (trước: 471/38) |
| FE build | `npm run build` | ✅ built in 21.7s |
| Real-stack E2E | `npm run test:e2e:realstack` | ✅ **20/20** |
| Whitespace | `git diff --check` | ✅ sạch |

> Plan §17 ghi `npm run test` — script thật là `test:unit`. Build backend cần
> `-p:BaseOutputPath=".tmp-build/..."` **trong repo** (tránh khoá bin của dev-server; trỏ ra `%TEMP%`
> sẽ làm mọi API test chết ở `FindRepositoryRoot`). Real-stack cần `MYSQL_BIN`.

## 10. Real-stack evidence

20/20 xanh trên DB dùng-một-lần `pems_e2e_realstack` (tự tạo, tự drop; không đụng `pems_db`/`pems_test`/
`pems_pr3_test`). Bao gồm 3 journey registrant-identity của đợt trước + 17 journey V2 workflow/UI cũ
(pending-edit, resubmit, safe-edit, amendment submit/approve/reject/withdraw, wrong-campus 403, search
scope) — tức là các thay đổi UI/DTO lần này **không phá** luồng nào đang chạy.

## 11. Database impact

**KHÔNG có.** Không thêm/sửa bảng, cột, enum, trigger, index. Toàn bộ cancellation metadata đọc từ cột
đã tồn tại (`VisitRequest.Cancelled*`, `VisitRequestCampus.Cancellation*`).

Có **re-pin** `CanonicalSqlScript.ExpectedSha256` → `d609a08e…` sau commit `36e22105` ("Fix upload
photo") sửa thân trigger uploader ảnh. Không thêm/bớt bảng hay trigger nên 2 hằng số đếm giữ nguyên;
không re-pin thì mọi import disposable abort và **0 integration test chạy**.

## 12. Known limitations

1. **`VisitHistoryEntryDto` đổi shape là breaking.** Chỉ 1 client tiêu thụ và đã sửa, nhưng nếu có
   consumer ngoài repo thì phải cập nhật.
2. **Chưa có event code cho `REQUEST_CANCELLED` / `SAFE_EDIT_APPLIED` / `REQUEST_RESUBMITTED`.** Plan
   §10 gợi ý nhưng backend chưa ghi sự kiện tương ứng vào bảng nào để đọc ra — em không bịa entry từ
   suy diễn. Cần bổ sung nguồn dữ liệu trước.
3. **`§8` contact action codes chưa làm.** Plan nói "audit whether backend should emit
   RESEND_CONTACT_CLAIM / REPLACE_PENDING_CONTACT / …" và ghi rõ đây là *preferred long-term design*.
   Em giữ nguyên manager relation như plan yêu cầu ("do not broaden"); chuyển sang capability-based
   là thay đổi contract cần anh quyết.
4. **Toast cục bộ còn lại** — xem Mục 13.

## 13. Remaining local-toast candidates

| File | Viewport | Ghi chú |
|---|---|---|
| `pages/dashboard/visit/VisitProcess.tsx` | `fixed top-6 right-6` | Đúng góc rồi nhưng vẫn là hệ thứ hai; nó **truyền `pushToast` xuống** `ParticipantInvitationSection` + `LogisticsRequestSection`, nên migrate phải sửa cả 3 file (~1600 dòng) và chạm luồng ngoài phạm vi audit này |
| `pages/dashboard/visit/MinutesCard.tsx` | `fixed top-5 right-5` | Hệ riêng, cùng góc |
| `pages/dashboard/visit/VisitFeedbackPage.tsx` | `fixed bottom-0 left-0` | **Không phải toast** — là sticky save bar |

DoD "không còn toast bottom-right trong luồng đã audit" ✅ (`VisitRequestManagement` là cái duy nhất).
Hai cái còn lại là top-right nên không gây triệu chứng sai góc; đề nghị gộp vào một đợt refactor
`VisitProcess` riêng.

## 14. Điểm resume

1. Quyết Mục 12.3 (contact action codes → capability-based hay giữ relation).
2. Nếu muốn event code đầy đủ theo §10: thêm nguồn ghi sự kiện cho REQUEST_CANCELLED /
   SAFE_EDIT_APPLIED / REQUEST_RESUBMITTED rồi map tiếp trong handler.
3. Migrate `VisitProcess.tsx` + `MinutesCard.tsx` sang toast chung (Mục 13).
4. Push + PR về `Dev`.
