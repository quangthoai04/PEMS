# Báo cáo — Kết quả sau OTP: giữ form, hiển thị kết quả, xem đơn vừa tạo

> Theo khung 15 mục của §21 trong `PEMS_PROMPT_FIX_OTP_RESULT_KEEP_FORM_AND_VIEW_CREATED_REQUEST.md`.
> Mọi kết luận kèm evidence: file/dòng, tên test, hoặc lệnh đã chạy thật. Không phỏng đoán.

---

## 1. Root cause chính xác

Prompt mô tả một chuỗi triệu chứng. Em audit từng cái trên HEAD `2461e37c` và kết quả **không đồng nhất** — có cái đã được sửa từ trước, có cái đúng là đang hỏng. Em báo cáo thật thay vì "sửa" lại thứ đã sửa rồi.

| Triệu chứng | Trạng thái trên HEAD trước khi làm | Evidence |
|---|---|---|
| Bấm xác nhận OTP submit lại form cha | **ĐÃ SỬA** ở commit `cbc309a3` (phiên trước) | `OtpVerificationModal.tsx:142` có `e.stopPropagation()` |
| Cấp challenge thứ hai khi verify | **ĐÃ SỬA** cùng commit đó | real-stack journey A đếm `initiate` = 1 |
| OTP sai làm mất form | **KHÔNG hỏng** | `verifyOtp` catch giữ nguyên form + draft |
| Draft bị clear sai chỗ | **KHÔNG hỏng** — chỉ clear sau verify thành công | `useVisitRequestFormV2.ts:545` |
| Không có màn kết quả | **KHÔNG hỏng** — cả route lẫn modal đều render success panel | `VisitRequestV2Page.tsx:36`, `VisitRequestV2Modal.tsx:105` |
| **Không có cách xem lại đơn vừa tạo** | **ĐÚNG LÀ HỎNG** | panel chỉ có link "Về trang chủ" / nút "Done" |
| **Verify response thiếu status/submittedAt** | **ĐÚNG LÀ HỎNG** | `VerifyAndCreateVisitRequestV2Command.cs:24-33` |
| **Không có state cho kết quả chưa xác định** | **ĐÚNG LÀ HỎNG** | timeout rơi vào `setOtpError(defaultError)` chung |
| **Không có lookup theo submissionId** | **ĐÚNG LÀ HỎNG** | không tồn tại endpoint nào |
| **Không có state machine** | **ĐÚNG LÀ HỎNG** | 4 boolean rời + sessionToken |
| **Không xem lại được form khi đang chờ OTP** | **ĐÚNG LÀ HỎNG** | không có action nào |
| **Đóng/gửi-lại không bị khóa khi đang verify** | **ĐÚNG LÀ HỎNG** | `onCancel` gọi vô điều kiện |

**Root cause thật của phần còn hỏng:** luồng chưa bao giờ có khái niệm *"chưa biết"*. Backend consume OTP **trong cùng transaction** với create (`VerifyAndCreateVisitRequestV2CommandHandler.cs:220-226`), nên mất kết nối sau commit là trường hợp **thật sự mơ hồ** — đơn có thể đã tồn tại. Frontend gộp nó chung nhánh với "mã sai" và hiện một lỗi chung. Đó chính xác là cái khiến người dùng nhập lại cả form → tạo đơn trùng, đúng nguy cơ prompt nêu.

## 2. Event bubbling được sửa ở đâu

`OtpVerificationModal.tsx:134-144` — `handleSubmit` gọi cả `preventDefault()` **và** `stopPropagation()`. Sửa ở commit `cbc309a3` phiên trước, **không phải** lần này. Lần này em **thêm test khoá lại** để nó không tái phát:

- `OtpVerificationModalGuards.test.tsx` render modal **bên trong một `<form>` host** rồi assert `onHostSubmit` không hề được gọi, cả khi click "Xác nhận" lẫn khi submit form của modal (Enter trong ô mã).
- Cùng file assert **đúng 1** nút `type="submit"` trong toàn modal, và đó là nút xác nhận.
- real-stack journey A đếm request thật: `initiate` = 1, `verify` = 1, và sink chỉ có 1 mã OTP.

## 3. State machine trước/sau

**Trước:** `isSubmitting`, `isVerifying`, `isResending`, `sessionToken`, `otpError`, `submitError`, `pendingOtp` — 4 boolean độc lập. Giữa lúc verify, form vừa `isVerifying` vừa còn `isSubmitting`. Không có trạng thái nào cho "mất kết nối".

**Sau** (`useVisitRequestFormV2.ts`, type `SubmissionStage`):

```
EDITING → SENDING_OTP → OTP_PENDING → VERIFYING_OTP → CREATE_CONFIRMED
                                            ↓
                                    CREATE_UNCERTAIN → (lookup COMPLETED) → CREATE_CONFIRMED
                                            ↓                ↓
                                       CREATE_FAILED    EDITING
```

`isSubmitting` và `isVerifying` giờ **derive** từ stage (`stage === 'SENDING_OTP'` / `'VERIFYING_OTP'`), không set độc lập được nữa — không caller nào quan sát được tổ hợp mà máy trạng thái không cho phép.

## 4. Verify response contract

Thêm 3 field vào **cả hai** response (verify công khai và create đã đăng nhập), đọc từ hàng vừa commit:

```jsonc
{
  "visitRequestId": 2003,
  "requestCode": "VR-MC-HN-HCM-0003",
  "status": "WAITING_REQUEST_APPROVAL",   // MỚI
  "submittedAt": "2026-07-31T09:30:00",   // MỚI — wall-clock VN, không offset
  "campusCount": 2,                        // MỚI
  "instances": [...], "idempotent": false, ...
}
```

Và endpoint tra cứu mới (§10/§15):

```
GET /api/v2/visit-requests/submissions/{submissionId}   [AllowAnonymous]
→ { state, visitRequestId, requestCode, status, submittedAt, campusCount }
  state ∈ COMPLETED | PENDING | FAILED | NOT_FOUND
```

Ba điểm dễ sai đã xử lý:
- **Đơn đã tạo ĐÈ hàng pending.** Snapshot được consume cùng transaction với create → đơn thành công luôn đi kèm pending row đã consumed. Đọc pending trước sẽ báo một đơn thành công là thất bại.
- **Consumed mà không có đơn = FAILED, không phải PENDING.** Đó là dấu vết duplicate-guard để lại; verify lại không bao giờ thành công, bảo user chờ tiếp là ngõ cụt.
- **Khoá theo submissionId, không theo email.** Một người gửi nhiều đơn là hợp lệ; "đơn mới nhất của email này" là câu hỏi khác và sẽ trả nhầm đơn.

**Bảo mật:** anonymous là bắt buộc (luồng public không có session) và an toàn — khoá là id do chính client sinh, response chỉ có mã đơn + trạng thái. Test `The_lookup_never_returns_who_submitted_it` serialize DTO và assert không có email/tên/tên đoàn trong đó.

## 5. Success screen

Trên panel (dùng chung route + modal):

```
✓ Đăng ký tham quan thành công
Mã đơn: VR-MC-HN-HCM-0003
Trạng thái: Chờ Staff Leader xử lý     Thời gian gửi: 31/07/2026 09:30
Số cơ sở: 2
Đơn đã được lưu thành công...

[Xem đơn vừa tạo] [Về danh sách đơn] [Tạo đơn mới]
```

- `Xem đơn` → `/dashboard/visit/v2/{visitRequestId}`.
- `Về danh sách đơn` → `/dashboard/visit` kèm flash trong navigation state.
- `Tạo đơn mới` → remount form (`key={formGeneration}`), submissionId mới.
- Modal **không tự đóng**; nút X chỉ đóng khi đã có kết quả hoặc form rảnh.

Status hiển thị qua bảng dịch có fallback: status lạ hiện **chính nó**, không hiện raw key — test `renders an unmapped status as itself`.

## 6. Draft clear/restore lifecycle

Giữ draft trong **mọi** trường hợp: OTP sai · OTP hết hạn · verify lỗi · timeout · đóng modal · xem lại form · quay lại form từ panel uncertain · reload.

Clear **chỉ khi** create được xác nhận — hai đường: verify trả 200, hoặc lookup trả `COMPLETED`. Test `only a confirmed create clears the draft` đi qua đủ chuỗi mã-sai → mất-kết-nối → thành công.

## 7. submissionId / idempotency lifecycle

- Sinh 1 lần mỗi **intent**, lưu vào draft **trước** khi request rời máy.
- Giữ qua mọi thất bại (đó chính là thứ làm retry idempotent). Chỉ null khi: create xác nhận, `IDEMPOTENCY_KEY_REUSED`, hoặc `resetForm`.
- `resetForm` (nút "Tạo đơn mới") xoá id → submit kế tiếp **không thể** replay lên đơn vừa xong. Test `resetForm mints a NEW intent`.
- Backend: `CheckIdempotentReplayAsync` chạy **trước** verify OTP, lại sau khi OTP fail, và sau `DbUpdateException` — nên retry sau khi commit trả về đúng đơn cũ thay vì lỗi OTP gây hiểu nhầm.

## 8. Uncertain-result recovery

Phân loại lỗi (`isUndecided`): **không có HTTP status** (timeout/abort/reset) hoặc **502/503/504** → chưa ai quyết định gì. Có status khác → server đã trả lời và đã quyết.

Panel nói rõ ba điều: chưa xác nhận được · **đừng gửi lại đơn mới** · và một nút duy nhất giải quyết được: hỏi server về intent. `COMPLETED` → lên thẳng success screen (không verify lần hai, không tạo gì). `PENDING`/`NOT_FOUND`/`FAILED` → trả form về nguyên vẹn.

## 9. Files changed

**Backend (5 sửa, 2 mới)** — `VisitRequestsController.cs` · `CreateVisitRequestV2Command(.Handler)` · `VerifyAndCreateVisitRequestV2Command(.Handler)` · `Queries/GetVisitSubmissionResult/{Query,QueryHandler}.cs`

**Frontend (9 sửa, 1 mới)** — `useVisitRequestFormV2.ts` · `visitRequestV2Api.ts` · `OtpVerificationModal.tsx` · `VisitRequestFormV2.tsx` · `VisitRequestV2Modal.tsx` · `VisitRequestV2SuccessPanel.tsx` · `VisitRequestV2Page.tsx` · `VisitRequestManagement.tsx` · i18n VI+EN · **mới** `VisitCreateUncertainPanel.tsx`

**Tests (5 mới, 3 sửa)** — xem mục 10.

## 10. Tests added

| File | Số test | Phủ |
|---|---:|---|
| `visitRequestV2SubmissionStage.test.tsx` | 16 | stage transitions · initiate 1×/verify 1× · chặn double-verify · khoá resend/cancel/review khi verify · mã sai giữ mọi thứ · lỗi nghiệp vụ về đúng field · timeout → UNCERTAIN · lookup COMPLETED/PENDING/lỗi · 502 undecided · review giữ challenge · draft chỉ clear khi confirmed · resetForm sinh intent mới |
| `visitRequestV2SuccessScreen.test.tsx` | 11 | mã/status/thời gian · status lạ · 3 action · ẩn action khi không tới được dashboard · replay · panel uncertain 5 case · VI |
| `OtpVerificationModalGuards.test.tsx` | 10 | không bubble lên form cha (click + Enter) · đúng 1 submit button · khoá close/back/resend khi verify · wording "đang tạo đơn" · review action |
| `VisitSubmissionResultLookupV2Tests.cs` | 10 | COMPLETED (kể cả khi snapshot đã consumed) · PENDING · FAILED (consumed / hết hạn) · NOT_FOUND · id rỗng · **không lộ danh tính** · hai submission độc lập |
| `otp-result-and-view.realstack.spec.ts` | 6 | journey A–E + C2 |

## 11. Gate results

Lệnh nguyên văn §19, chạy thật:

| Gate | Trước (baseline phiên này) | Sau |
|---|---|---|
| Backend build | 0 lỗi | **0 lỗi**, 196 warning |
| Architecture | 14/14 | **14/14** |
| Unit | 1072/1072 | **1072/1072** |
| Integration | 626/626 | **636/636** (+10) |
| FE lint | 0 | **0** |
| FE unit | 622/622 (48 file) | **659/659** (51 file, +37) |
| FE build | built | **built** |
| Real-stack E2E | 29/29 | **35/35** (+6) |
| `git diff --check` | sạch | **sạch** |

> Lưu ý: §1 của prompt ghi baseline 554/554 và 24/24 — con số đó là của **hai** đợt trước. Baseline thực tế lúc bắt đầu đợt này là 622/622 và 29/29, và không gate nào giảm.

## 12. Real-stack evidence

Journey D là bằng chứng quan trọng nhất, vì nó dựng đúng tình huống mơ hồ chứ không giả lập nó:

```ts
await page.route('**/v2/visit-requests/verify', async route => {
  await route.fetch();          // backend CHẠY THẬT và COMMIT THẬT
  await route.abort('failed');  // ...còn câu trả lời thì không bao giờ về tới
});
```

Kết quả: panel "chưa xác định" hiện ra (không phải lỗi), bấm "Kiểm tra lại kết quả" → lookup tìm thấy đơn đã commit → success screen với mã đơn thật. Sink vẫn chỉ có **1 mã OTP** (không hề cấp challenge thứ hai) và không có đơn thứ hai nào được tạo.

Journey B chứng minh yêu cầu cuối của prompt ("phải xác minh người dùng mở được đúng đơn vừa tạo"): receipt hiện mã `VR…`, status và thời gian gửi — tất cả từ hàng backend vừa ghi.

Journey C2 chứng minh §12: bấm "Xem lại thông tin đơn" → về form còn nguyên dữ liệu → banner → quay lại → **cùng mã OTP đó vẫn verify được**, sink vẫn 1 mã.

## 13. Database impact

**Không có.** Không thêm/sửa/xoá bảng, cột, index, trigger, enum. Lookup đọc `visit_requests` và `visit_request_pending_forms` đã có sẵn. Không cần chạy script gì trước khi deploy.

## 14. Known limitations

1. **Public không có "Xem đơn vừa tạo".** Luôn là quyết định có ý thức: create có provision tài khoản Visitor nhưng **không đăng nhập** người dùng, nên nút đó sẽ đá họ sang màn login không lời giải thích. Ngõ cụt tệ hơn là không có nút. Muốn có thì cần luồng "đăng nhập rồi quay lại đơn" — việc riêng.
2. **Lookup không có rate limit riêng.** Đọc thuần, khoá là GUID do client sinh, response tối thiểu — nhưng nếu muốn chặt hơn thì nên gắn vào hạ tầng rate-limit chung. Em không tự thêm để khỏi lệch với các endpoint public khác.
3. **`FAILED` gộp hai nguyên nhân** (duplicate-guard consume, và snapshot hết hạn). Người dùng thấy cùng một câu. Tách ra cần thêm cột lý do vào `visit_request_pending_forms` — là migration, ngoài phạm vi.
4. **Panel uncertain không tự poll.** Người dùng bấm "Kiểm tra lại". Auto-poll dễ hơn nhưng che mất việc gì đang xảy ra; §10 cũng chỉ yêu cầu "cho kiểm tra lại sau".
5. **`CREATE_UNCERTAIN` sau reload là mất.** Stage nằm trong React state; draft và submissionId thì còn trên đĩa, nên người dùng vẫn có đường quay lại qua banner resume — nhưng panel "chưa xác định" thì không tự hiện lại sau F5.
6. **Toast "đã gửi OTP"** dùng `showSuccessToast` với id cố định. Gửi lại nhiều lần sẽ thay thế toast cũ thay vì xếp chồng — cố ý, nhưng nếu muốn đếm số lần gửi thì phải bỏ id.

## 15. Resume point

Cây sạch, HEAD `573e061a`, **chưa push**. Ba commit:

| Commit | Nội dung |
|---|---|
| `ea670461` | `feat(visit-api)` — receipt fields + submission lookup |
| `0e1ff254` | `fix(visit-otp)` — state machine + uncertain result |
| `573e061a` | `feat(visit-ui)` — success actions + review-mid-OTP + flash |

Việc còn treo từ các đợt trước (không đụng gì lần này):
1. `git rebase --onto msg-repaired 8850b87e Canh-Iter1` — nhận lại message 7 commit cũ; nhánh `msg-repaired` vẫn còn nguyên.
2. Push + PR về `Dev`.
3. `VisitAmendmentSubmitModal` vẫn dùng `datetime-local` (ghi trong báo cáo đợt trước).

Môi trường: `mysql` không có trong PATH máy này, real-stack phải chạy kèm
`MYSQL_BIN="C:/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe"`.
