# PROMPT — FIX LUỒNG OTP SAU KHI GỬI: KHÔNG MẤT FORM, HIỂN THỊ KẾT QUẢ VÀ CHO XEM ĐƠN VỪA TẠO

Bạn là Senior Full-stack Engineer phụ trách dự án PEMS.

Hãy đọc kỹ code hiện tại trên nhánh `Cảnh-Iter1` tại HEAD mới nhất, sau đó điều tra và sửa triệt để luồng:

```text
Người dùng nhập đầy đủ đơn
→ gửi yêu cầu nhận OTP
→ nhập OTP
→ bấm xác nhận
→ form biến mất
→ không có thông báo thành công/thất bại
→ không biết đơn đã được tạo hay chưa
→ không có cách xem lại đơn vừa tạo
```

Đây là lỗi UX nghiêm trọng và có nguy cơ tạo đơn trùng nếu người dùng không biết kết quả rồi thử gửi lại.

Không được chỉ thêm một toast chung chung. Phải hoàn thiện toàn bộ state machine của form, OTP, kết quả tạo đơn, draft và idempotency.

---

# 1. Baseline hiện tại

Giữ nguyên và không làm giảm kết quả gate hiện tại:

```text
Backend build · Architecture · Unit:
0 lỗi · 14/14 · 1052/1052

Integration:
622/622

Frontend lint · unit · build:
0 lỗi · 554/554 · built

Real-stack E2E:
24/24

git diff --check:
sạch
```

Trước khi sửa:

```bash
git status
git branch --show-current
git rev-parse HEAD
git log -n 15 --oneline
git diff --check
```

Không reset, rebase, force-push hoặc xóa WIP.

---

# 2. Audit bắt buộc trước khi sửa

Rà soát đầy đủ:

```text
VisitRequestFormV2
useVisitRequestFormV2
OTP modal/component
createPortal
Public create page
Authenticated delegated create
Dashboard create modal
Full-page create route
Draft/autosave hook
submissionId lifecycle
initiate OTP API
verify OTP API
resend OTP API
create success callback
modal close callback
navigate after success
global toast
error normalization
```

Trả lời bằng evidence:

```text
[ ] Khi bấm xác nhận OTP, event có bubble lên form đăng ký không.
[ ] OTP form có gọi stopPropagation không.
[ ] Button xác nhận OTP có type="submit" hay type="button".
[ ] Verify OTP có vô tình gọi lại initiate OTP không.
[ ] Modal hoặc form bị unmount ở bước nào.
[ ] Draft bị clear ở initiate, verify hay close modal.
[ ] Verify API trả requestId/requestCode hay chỉ trả message.
[ ] Frontend có lưu kết quả verify trước khi đóng form không.
[ ] Thành công có navigate đến detail hay không.
[ ] state.flash có được consume không.
[ ] Network timeout có làm frontend không biết request đã tạo hay chưa.
[ ] Retry cùng submissionId có tạo duplicate hay không.
```

Không phỏng đoán. Ghi rõ file, component, hàm và nguyên nhân.

---

# 3. State machine bắt buộc

Chuẩn hóa luồng frontend thành các trạng thái rõ ràng:

```text
EDITING
SENDING_OTP
OTP_PENDING
VERIFYING_OTP
CREATE_CONFIRMED
CREATE_UNCERTAIN
CREATE_FAILED
```

Không dùng một vài boolean rời rạc khiến state mâu thuẫn.

Ví dụ:

```ts
type SubmissionStage =
  | 'EDITING'
  | 'SENDING_OTP'
  | 'OTP_PENDING'
  | 'VERIFYING_OTP'
  | 'CREATE_CONFIRMED'
  | 'CREATE_UNCERTAIN'
  | 'CREATE_FAILED';
```

Mỗi thời điểm chỉ có một stage hợp lệ.

---

# 4. Chặn lỗi OTP submit lại toàn bộ form

OTP modal có thể được render bằng `createPortal`, nhưng portal vẫn nằm trong React component tree nên submit event có thể bubble lên form cha.

Phải bảo đảm:

```text
Bấm Xác nhận OTP
→ chỉ gọi verify OTP
→ không gọi lại submit form đăng ký
→ không gọi lại initiate OTP
```

Cách sửa phải bao gồm:

```tsx
<form
  onSubmit={(event) => {
    event.preventDefault();
    event.stopPropagation();
    void handleVerifyOtp();
  }}
>
```

Đồng thời kiểm tra:

- Nút xác nhận OTP thuộc đúng form.
- Nút đóng/resend/back phải có `type="button"`.
- Không dùng chung handler submit của form đăng ký.
- Không để Enter trong ô OTP kích hoạt form cha.
- Không cấp OTP challenge thứ hai khi verify challenge hiện tại.

Bổ sung regression test đếm số lần gọi API:

```text
initiate OTP: đúng 1 lần
verify OTP: đúng 1 lần
```

---

# 5. Khi OTP đang được xác minh

Khi bấm xác nhận:

- Chuyển stage sang `VERIFYING_OTP`.
- Giữ nguyên form đăng ký trong memory và draft.
- Giữ modal OTP.
- Disable:
  - nút xác nhận;
  - nút gửi lại;
  - nút đóng nếu đóng lúc transaction đang chạy gây state không xác định.
- Hiển thị:

```text
Đang xác minh mã và tạo đơn...
```

- Chống double-click.
- Không clear OTP/form/draft ở thời điểm này.
- Không tự đóng modal trước khi nhận kết quả backend.

---

# 6. Khi OTP sai hoặc hết hạn

## OTP sai

Phải:

```text
Giữ modal OTP
Giữ toàn bộ form
Giữ draft
Giữ submissionId
Không gọi lại initiate
Không làm biến mất lỗi
Không đóng modal
```

Hiển thị rõ:

```text
Mã xác minh không chính xác. Vui lòng kiểm tra và nhập lại.
```

Có thể clear riêng ô OTP, nhưng không clear challenge hoặc form.

## OTP hết hạn

Hiển thị:

```text
Mã xác minh đã hết hạn.
[Gửi lại mã]
```

Khi resend:

- Giữ form.
- Giữ draft.
- Giữ submissionId theo contract hiện tại.
- Invalidate mã cũ.
- Cập nhật `expiresAt`.
- Không tạo request mới.
- Không gọi lại full form submit ngoài resend flow.

---

# 7. Khi backend xác nhận tạo đơn thành công

Không được đóng tất cả rồi để người dùng ở màn hình trống.

Frontend phải lưu một object kết quả trước:

```ts
interface CreatedVisitResult {
  visitRequestId: number;
  requestCode: string;
  status: string;
  submittedAt?: string;
  campusCount?: number;
  campusNames?: string[];
}
```

Nếu verify API hiện chưa trả đủ dữ liệu, cập nhật response contract tối thiểu:

```json
{
  "success": true,
  "visitRequestId": 2003,
  "requestCode": "VR-MC-HN-HCM-0003",
  "status": "WAITING_REQUEST_APPROVAL",
  "submittedAt": "2026-07-31T09:30:00",
  "campusCount": 2
}
```

Backend phải trả dữ liệu từ request vừa commit, không để frontend đoán.

Sau khi nhận response hợp lệ:

```text
stage = CREATE_CONFIRMED
```

Chỉ lúc này mới:

- đánh dấu submission hoàn tất;
- clear OTP context;
- clear draft;
- clear submissionId cũ;
- reset form để chuẩn bị cho lần tạo mới sau này.

Không clear UI result.

---

# 8. Màn hình kết quả sau khi tạo thành công

Sau OTP thành công, không tự đóng modal ngay.

Thay nội dung form bằng màn kết quả:

```text
✓ Đăng ký tham quan thành công

Mã đơn: VR-MC-HN-HCM-0003
Trạng thái: Chờ Staff Leader xử lý
Thời gian gửi: 31/07/2026 09:30
Số cơ sở: 2

Đơn đã được lưu thành công.
Bạn có thể mở lại đơn để kiểm tra toàn bộ thông tin vừa gửi.
```

Các nút:

```text
[Xem đơn vừa tạo]
[Về danh sách đơn]
[Tạo đơn mới]
```

## Xem đơn vừa tạo

Điều hướng đến:

```text
/dashboard/visit/v2/{visitRequestId}
```

Đây phải là action chính.

## Về danh sách đơn

Điều hướng đến trang danh sách và hiển thị top-right toast:

```text
Đã tạo đơn VR-MC-HN-HCM-0003 thành công.
```

## Tạo đơn mới

Chỉ khi người dùng chủ động bấm:

- clear result;
- tạo submissionId mới;
- mở form trống;
- không restore lại draft vừa hoàn tất.

---

# 9. Không bắt buộc người dùng rời modal

Trong dashboard modal:

- Thành công → hiển thị success screen bên trong modal.
- Không tự đóng modal sau vài giây.
- Nút X chỉ đóng khi đã có kết quả an toàn.
- Nếu người dùng đóng success screen:
  - request vẫn tồn tại;
  - có toast chứa mã đơn;
  - danh sách được refresh.

Trong full-page route:

- Có thể navigate thẳng sang detail sau success.
- Trước khi navigate phải có `visitRequestId`.
- Detail page phải consume flash message đúng một lần.

---

# 10. Trường hợp response không chắc chắn

Nếu request verify bị:

```text
timeout
network disconnected
gateway error
connection reset
```

thì frontend không được kết luận ngay là thất bại, vì backend có thể đã commit request.

Chuyển sang:

```text
CREATE_UNCERTAIN
```

Hiển thị:

```text
Chưa thể xác nhận kết quả tạo đơn.

Kết nối bị gián đoạn sau khi gửi mã xác minh.
Đừng gửi lại đơn mới ngay vì đơn có thể đã được tạo.
```

Có nút:

```text
[Kiểm tra lại kết quả]
[Quay lại form]
```

## Kiểm tra lại kết quả

Backend cần có hoặc tái sử dụng lookup theo:

```text
submissionId
```

Ví dụ:

```text
GET /api/v2/visit-requests/submissions/{submissionId}
```

Response:

```json
{
  "state": "COMPLETED",
  "visitRequestId": 2003,
  "requestCode": "VR-MC-HN-HCM-0003"
}
```

Các state:

```text
PENDING
COMPLETED
NOT_FOUND
FAILED
```

Nếu `COMPLETED`:

- chuyển sang success screen;
- không tạo thêm request.

Nếu `PENDING`:

- cho kiểm tra lại sau;
- không re-initiate.

Nếu `NOT_FOUND`:

- giữ form và draft;
- cho gửi lại theo cùng idempotency rule hoặc tạo challenge mới có kiểm soát.

Không bắt người dùng tự đoán.

---

# 11. Draft lifecycle

Draft phải được giữ trong mọi trường hợp:

```text
OTP sai
OTP hết hạn
verify API lỗi
network timeout
đóng modal OTP
quay lại form
reload
```

Chỉ clear draft khi:

```text
Backend đã trả hoặc lookup xác nhận COMPLETED
```

Khi đóng OTP modal:

```text
Đơn của bạn đã được lưu tạm. Bạn có thể tiếp tục xác minh sau.
```

Toast phải ở top-right.

Khi mở lại:

- restore form;
- restore submissionId;
- restore target email;
- restore challenge context an toàn nếu còn hạn;
- không tự initiate mã mới.

Không lưu raw OTP.

---

# 12. Cho người dùng xem lại form trước khi xác nhận OTP

Trong OTP modal, bổ sung action:

```text
[Xem lại thông tin đơn]
```

Khi bấm:

- đóng hoặc thu nhỏ OTP modal;
- quay lại form với toàn bộ dữ liệu;
- không mất challenge;
- hiển thị banner:

```text
Mã xác minh đã được gửi đến visitor@example.com.
Bạn có thể kiểm tra lại đơn rồi tiếp tục nhập mã.
```

Có action:

```text
[Tiếp tục xác minh OTP]
```

Không bắt gửi mã mới nếu challenge còn hạn.

---

# 13. Kết quả lỗi tạo đơn sau OTP đúng

Phân biệt:

## OTP không hợp lệ

Lỗi nằm trong OTP modal.

## OTP đúng nhưng dữ liệu đơn không còn hợp lệ

Ví dụ:

```text
Campus ngừng nhận đăng ký
Thời gian không còn hợp lệ
Snapshot mismatch
Submission conflict
```

Phải:

- giữ form;
- đóng OTP modal hoặc cho quay lại form;
- expand đúng campus/field lỗi;
- focus field đầu tiên;
- giữ draft;
- không clear submissionId một cách tùy tiện;
- hiển thị error summary.

Ví dụ:

```text
Mã xác minh đúng nhưng đơn chưa thể được tạo.
Vui lòng kiểm tra lại 2 trường được đánh dấu.
```

Không hiển thị thành “OTP sai”.

---

# 14. Toast và inline feedback

Dùng global shared toaster:

```text
top-right
```

Không tạo toaster khác.

## Toast dùng cho

```text
Đã gửi OTP
Đã gửi lại OTP
Đã lưu bản nháp
Đã khôi phục bản nháp
Đã tạo đơn thành công
Lỗi mạng
Không thể kiểm tra trạng thái
```

## Inline dùng cho

```text
OTP sai
OTP hết hạn
Field validation
Form-level validation summary
Uncertain result panel
Success result screen
```

Không chỉ dựa vào toast để báo kết quả tạo đơn, vì toast có thể biến mất.

---

# 15. Backend requirements

## Verify response

Sau transaction thành công phải trả:

```text
visitRequestId
requestCode
status
submittedAt
campusCount
```

## Idempotency

Cùng:

```text
submissionId
verified snapshot
```

không được tạo hai request.

Retry verify phải:

- trả lại cùng request đã tạo;
- hoặc trả response idempotent `COMPLETED`;
- không báo lỗi chung khiến frontend tưởng chưa tạo.

## Transaction

Trong cùng transaction:

```text
Validate challenge
Validate snapshot
Create request
Create instances/details/members
Create revision/history
Mark OTP challenge used
Persist submission result
Commit
```

Không mark challenge used trước khi request commit.

## Result lookup

Có nguồn tra cứu theo `submissionId` để xử lý uncertain network result.

Không dùng email đơn thuần để tìm request vừa tạo vì có thể có nhiều request.

---

# 16. Frontend tests bắt buộc

```text
1. OTP submit không bubble lên form cha.
2. Bấm xác nhận gọi verify đúng 1 lần.
3. Không gọi initiate lần hai khi verify.
4. OTP sai giữ modal và form.
5. OTP hết hạn giữ draft.
6. Đóng OTP giữ form.
7. Xem lại form giữ challenge.
8. Verify success hiện success screen.
9. Success screen có request code.
10. Có nút Xem đơn vừa tạo.
11. Xem đơn điều hướng đúng requestId.
12. Không auto-close modal sau success.
13. Draft chỉ clear sau confirmed success.
14. Network timeout chuyển CREATE_UNCERTAIN.
15. Check result COMPLETED chuyển success.
16. Check result PENDING không tạo request mới.
17. Retry verify không duplicate.
18. API business error quay lại đúng field.
19. Toast success chỉ hiện một lần.
20. state.flash không replay khi refresh/back.
```

---

# 17. Backend/integration tests

```text
1. Verify OTP đúng tạo request và trả requestId/code.
2. Verify replay trả cùng request.
3. Cùng submissionId không tạo duplicate.
4. Challenge used chỉ sau commit.
5. Transaction fail không để partial request.
6. Lookup submission COMPLETED trả request.
7. Lookup PENDING đúng.
8. Lookup NOT_FOUND đúng.
9. Snapshot mismatch không tạo request.
10. OTP sai không làm mất challenge hợp lệ ngoài retry rule.
11. Expired OTP không tạo request.
12. Campus/member/detail được lưu đầy đủ.
```

---

# 18. Real-stack E2E

## Journey A — OTP sai

```text
Nhập form
→ gửi OTP
→ nhập sai
→ lỗi còn hiển thị
→ form không mất
→ initiate chỉ gọi 1 lần
```

## Journey B — OTP đúng

```text
Nhập OTP đúng
→ success screen xuất hiện
→ có mã đơn
→ bấm Xem đơn vừa tạo
→ detail hiển thị đúng toàn bộ form vừa gửi
```

## Journey C — đóng OTP

```text
Gửi OTP
→ đóng modal
→ quay lại form
→ dữ liệu còn nguyên
→ tiếp tục xác minh
→ không gửi OTP mới nếu challenge còn hạn
```

## Journey D — timeout sau commit

```text
Verify đã commit nhưng response bị ngắt
→ frontend hiển thị kết quả chưa xác định
→ kiểm tra theo submissionId
→ tìm thấy request
→ hiện success screen
→ chỉ có 1 request trong DB
```

## Journey E — validation sau OTP

```text
OTP đúng
→ campus/time trở nên không hợp lệ
→ request không tạo
→ form được giữ
→ campus lỗi được mở/focus
→ user sửa rồi tiếp tục
```

---

# 19. Gate bắt buộc

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

# 20. Definition of Done

```text
[ ] OTP verify không submit lại form cha.
[ ] Không cấp challenge thứ hai khi xác nhận OTP.
[ ] OTP sai không làm mất form.
[ ] OTP hết hạn không làm mất form.
[ ] Network lỗi không làm mất form.
[ ] Sau success có màn hình kết quả rõ ràng.
[ ] Kết quả có mã đơn và trạng thái.
[ ] Có nút Xem đơn vừa tạo.
[ ] Modal không tự đóng ngay sau success.
[ ] Người dùng có thể xem lại form trong lúc chờ OTP.
[ ] Draft chỉ clear khi create được xác nhận.
[ ] Có xử lý kết quả chưa xác định bằng submissionId.
[ ] Retry/replay không tạo duplicate.
[ ] Error field quay về đúng vị trí.
[ ] Toast dùng global top-right.
[ ] Backend trả requestId/requestCode sau verify.
[ ] Unit/integration/E2E xanh.
[ ] Gate không giảm so với baseline.
```

---

# 21. Báo cáo cuối cùng

Báo cáo phải nêu:

```text
1. Root cause chính xác.
2. Event bubbling được sửa ở đâu.
3. State machine trước/sau.
4. Verify response contract.
5. Success screen.
6. Draft clear/restore lifecycle.
7. submissionId/idempotency lifecycle.
8. Uncertain-result recovery.
9. Files changed.
10. Tests added.
11. Gate results.
12. Real-stack evidence.
13. Database impact.
14. Known limitations.
15. Resume point.
```

Không báo “đã thành công” nếu chưa chạy journey OTP đúng và xác minh rằng người dùng mở được đúng đơn vừa tạo.
