# PEMS — PROMPT TRIỂN KHAI TỔNG HỢP

## Hoàn thiện lịch sử thay đổi, quyền đầu mối, UI xem đơn, toast, UI chuyển giao và lưu nháp OTP

> Tài liệu này được chuẩn hóa từ nội dung yêu cầu đã cung cấp, dùng để giao trực tiếp cho AI Agent/Developer triển khai trên nhánh `Cảnh-Iter1`.

---

Cần làm tiếp 3 nhóm chính, không phải làm lại phần OTP/P0 đã xong.

1. Hoàn thiện lịch sử thay đổi

Hiện timeline mới đẹp hơn nhưng còn thiếu 3 loại sự kiện vì backend chưa có nguồn dữ liệu chuẩn:

Đơn bị hủy: ghi rõ ai hủy, thời gian, lý do.
Sửa nhanh thành công: ghi ai sửa, lúc nào, sửa phần nào.
Gửi lại đơn bị từ chối: ghi mỗi lần gửi lại, ai thực hiện, thời gian.

Cách làm:

Tận dụng các bảng revision history hiện có.
Khi sửa nhanh hoặc gửi lại đơn, ghi thêm một bản ghi lịch sử ngay trong cùng transaction.
Khi hủy, đọc dữ liệu từ cancelled_by, cancelled_at, cancellation_reason.
Timeline chỉ hiển thị câu dễ hiểu, không hiện source=CREATE, enum hoặc JSON.

Ví dụ:

Kim Min Jae đã cập nhật nhanh thông tin của đơn.
15/06/2026 10:20
Kim Min Jae đã gửi lại đơn sau khi bị từ chối.
16/06/2026 08:30
2. Chuẩn hóa quyền thao tác đầu mối liên hệ

Hiện frontend còn dựa vào viewer.relation để tự quyết định có hiện nút hay không.

Cần để backend trả rõ từng quyền:

RESEND_CONTACT_CLAIM
REPLACE_PENDING_CONTACT
INITIATE_CONTACT_TRANSFER
RESEND_CONTACT_TRANSFER
CANCEL_CONTACT_TRANSFER

Sau đó frontend chỉ hiện nút khi backend trả đúng action.

Ví dụ:

Đầu mối đang chờ xác nhận:
Gửi lại lời mời.
Nhập lại email.
Đầu mối đã xác nhận:
Chuyển giao vai trò đầu mối.
Đang có chuyển giao:
Gửi lại lời mời chuyển giao.
Hủy chuyển giao.

Việc này không thay đổi quyền cũ, chỉ làm contract rõ ràng và an toàn hơn.

3. Hoàn thiện UI và toast

Các phần cần sửa:

Đổi nút “Lưu thay đổi” ở màn xem đơn thành “Sửa đơn”.
Chỉ giữ “Lưu thay đổi” tại nút submit thật.
Bỏ thông tin người đăng ký và đầu mối bị lặp ở card đầu.
Card đầu thay bằng:
trạng thái hiện tại;
số cơ sở đã xử lý/chờ/từ chối;
nếu hủy hoặc từ chối thì hiện người thực hiện, thời gian, lý do.
Chuyển toàn bộ phần quản lý đầu mối vào Mục 2 — Đầu mối liên hệ của đơn.
Đổi phần:
Nội dung v1 · Phê duyệt v1

thành câu dễ hiểu theo trạng thái:

Nội dung hiện tại: phiên bản 1
Trạng thái xét duyệt: Chưa được duyệt
Dùng toast chung góc phải trên cho:
sửa đơn;
sửa nhanh;
gửi đề xuất thay đổi;
duyệt/từ chối/rút đề xuất;
chuyển giao đầu mối;
hủy đơn;
duyệt/gán host.

Xóa toast riêng góc dưới bên phải trong VisitRequestManagement.

4. Kiểm thử lại trước khi push

Sau khi làm xong cần bổ sung test cho:

Lịch sử hủy
Lịch sử sửa nhanh
Lịch sử gửi lại đơn
Actor name
Contact action codes
Không lộ action cho HO/Host/Staff Leader ngoài quyền
Toast chỉ hiện một lần
Không còn raw enum/source/JSON
Không còn “Phê duyệt v1” khi chưa duyệt

Sau đó chạy lại:

Backend build
Architecture tests
Unit tests
Integration tests
Frontend lint
Frontend unit tests
Frontend build
Real-stack E2E
git diff --check

Tóm lại, phần còn lại là:

Ghi đủ lịch sử nghiệp vụ
→ backend trả quyền đầu mối rõ ràng
→ sửa UI trạng thái/phiên bản
→ chuẩn hóa toàn bộ toast
→ test lại
→ mới commit và push
tạo cho tôi prompt cập nhật phần trên và fix phần ui của lúc chuyển vai trò để các ô nhập ngắn hơn không để mỗi ô thành một hàng như hiện tại, ngoài ra phần lưu nháp nếu như nhập otp và phát sinh ra lỗi hoặc người ta không nhập nữa thì vẫn lưu nháp tạm đơn đó để tránh người ta nhập lại sẽ lâu hơn 
Đã xử lý trong 38s

Dưới đây là prompt hoàn chỉnh để giao cho AI Agent triển khai:

# PROMPT — CẬP NHẬT UI CHUYỂN GIAO ĐẦU MỐI VÀ BẢO TOÀN BẢN NHÁP KHI XÁC MINH OTP

Bạn là Senior Full-stack Engineer phụ trách dự án PEMS.

Hãy đọc kỹ code hiện tại trên nhánh `Cảnh-Iter1`, bắt đầu từ HEAD hiện tại, sau đó triển khai hai nhóm yêu cầu dưới đây. Không được chỉ sửa giao diện mà bỏ qua logic, trạng thái, bảo mật, idempotency và kiểm thử.

---

# 1. Mục tiêu

Thực hiện đồng thời:

1. Tối ưu giao diện phần **Chuyển giao vai trò đầu mối** trong Section 2 — “Đầu mối liên hệ của đơn”.
2. Bảo toàn bản nháp của đơn đăng ký khi người dùng đã chuyển sang bước OTP nhưng:
   - nhập OTP sai;
   - OTP hết hạn;
   - gửi OTP thất bại;
   - đóng modal OTP;
   - bấm quay lại;
   - không tiếp tục nhập OTP;
   - mất mạng hoặc API gặp lỗi.

Người dùng không được phải nhập lại toàn bộ đơn chỉ vì bước xác minh OTP chưa hoàn tất.

---

# 2. Quy tắc chung

Trước khi sửa code:

```text
git status
git branch --show-current
git rev-parse HEAD
git log -n 10 --oneline
git diff --check

Sau đó rà soát:

ContactIdentityPanel / ContactIdentityActions
VisitRequestV2DetailView
Section 2 — Đầu mối liên hệ
OTP modal
useVisitRequestFormV2
draft/autosave hooks
VisitRequestV2Modal
VisitRequestV2Page
submissionId lifecycle
OTP initiate / resend / verify API
localStorage hoặc IndexedDB draft implementation hiện tại

Không được:

Tạo thêm một hệ thống draft song song nếu dự án đã có draft infrastructure.
Lưu raw OTP.
Lưu token xác minh nhạy cảm vào localStorage.
Xóa bản nháp chỉ vì OTP thất bại hoặc modal bị đóng.
Tạo request trùng khi người dùng thử lại.
Tự mở rộng quyền chuyển giao đầu mối.
Làm thay đổi backend authorization hiện tại.
Mount thêm một <Toaster>.
Dùng toast riêng sai vị trí.
PHẦN A — TỐI ƯU UI CHUYỂN GIAO ĐẦU MỐI
3. Hiện trạng cần sửa

Trong Section 2 hiện tại, khi người dùng mở chức năng chuyển giao đầu mối, các trường:

Họ và tên
Đơn vị công tác
Số điện thoại
Email
Lý do chuyển giao

đều chiếm nguyên một hàng ngang.

Điều này khiến form:

quá dài;
có nhiều khoảng trắng;
phải cuộn nhiều;
không đồng nhất với layout hai cột của phần thông tin phía trên;
làm thao tác chuyển giao có cảm giác như một form riêng quá lớn.
4. Bố cục mới
4.1 Trạng thái mặc định

Khi chưa bắt đầu chuyển giao:

Không hiển thị sẵn toàn bộ input.
Chỉ hiển thị:
mô tả trạng thái đầu mối hiện tại;
nút Chuyển giao vai trò đầu mối.

Ví dụ:

QUẢN LÝ ĐẦU MỐI

Đầu mối hiện tại đã xác nhận và đang quản lý đơn.
Bạn có thể gửi lời mời chuyển giao cho một người khác.

[Chuyển giao vai trò đầu mối]

Khi bấm nút, mở form inline bên trong Section 2.

Không tạo một card rời bên ngoài Section 2.

4.2 Bố cục desktop/tablet

Dùng grid hai cột:

Họ và tên             Đơn vị công tác
Số điện thoại         Email

Lý do chuyển giao

Trong đó:

Họ và tên và Đơn vị công tác cùng một hàng.
Số điện thoại và Email cùng một hàng.
Lý do chuyển giao chiếm toàn bộ chiều ngang.
Nút hành động đặt cùng một hàng ở cuối form.

Gợi ý cấu trúc:

<div className="grid grid-cols-1 gap-4 md:grid-cols-2">
  <FormField label="Họ và tên" required>
    ...
  </FormField>

  <FormField label="Đơn vị công tác">
    ...
  </FormField>

  <FormField label="Số điện thoại" required>
    ...
  </FormField>

  <FormField label="Email" required>
    ...
  </FormField>

  <div className="md:col-span-2">
    <FormField label="Lý do chuyển giao">
      ...
    </FormField>
  </div>
</div>

Không bắt buộc dùng chính xác class trên nếu dự án có shared layout component tốt hơn.

4.3 Chiều rộng form

Không để input trải quá rộng trên màn hình lớn.

Form quản lý đầu mối nên có:

max-width hợp lý, khoảng 850–1000px

hoặc nằm trong content width hiện tại của Section 2 nhưng vẫn chia hai cột.

Các input:

chiều cao đồng nhất;
border và focus state theo design system;
không dùng chiều cao quá lớn;
không dùng textarea một dòng nếu lý do cần nhập dài.

Lý do chuyển giao nên dùng textarea:

2–3 dòng mặc định
tự tăng chiều cao khi nội dung dài
maxLength theo backend validation hiện tại
4.4 Mobile

Dưới breakpoint tablet:

Họ và tên
Đơn vị công tác
Số điện thoại
Email
Lý do chuyển giao

chuyển thành một cột.

Không gây horizontal scroll.

4.5 Khu vực nút

Cuối form:

[Hủy] [Gửi lời mời chuyển giao]

Yêu cầu:

Căn phải trên desktop.
Trên mobile có thể full-width hoặc chia hợp lý.
Nút gửi dùng màu cam chính của PEMS.
Nút hủy dùng secondary button.
Khi đang gửi:
disable cả hai nút nếu cần;
hiển thị spinner;
đổi text thành Đang gửi lời mời....
Không cho double-submit.

Khi bấm Hủy:

đóng form chuyển giao;
xóa dữ liệu tạm của riêng form chuyển giao;
không ảnh hưởng bản nháp đơn đăng ký;
không thay đổi đầu mối hiện tại.
5. Validation chuyển giao

Frontend và backend phải đồng bộ:

Họ và tên: bắt buộc
Số điện thoại: bắt buộc
Email: bắt buộc, đúng định dạng
Đơn vị công tác: theo rule hiện tại
Lý do chuyển giao: theo rule hiện tại

Không tự tạo giới hạn mới nếu backend đã có giới hạn.

Hiển thị:

lỗi field ngay dưới field;
lỗi nghiệp vụ/API bằng toast góc phải trên;
không hiển thị lỗi kỹ thuật thô;
không hiển thị raw error code cho người dùng.

Khi email người nhận trùng email đầu mối hiện tại:

chặn submit;
hiển thị thông báo rõ ràng.

Khi đã có một transfer đang hoạt động:

không được biến lỗi load thành trạng thái “không có transfer”;
không cho tạo transfer thứ hai;
hiển thị transfer đang chờ;
cung cấp đúng các action resend/cancel mà backend cho phép.
6. Toast cho chuyển giao đầu mối

Dùng utility toast chung của hệ thống:

shared/utils/toast

Toast phải xuất hiện ở:

top-right

Không tạo local toast viewport.

Các tình huống:

Gửi lời mời thành công
Gửi lại lời mời thành công
Hủy chuyển giao thành công
Thay email đầu mối thành công
API lỗi
Mất mạng
Conflict
Transfer đã tồn tại

Ví dụ:

Đã gửi lời mời chuyển giao đầu mối đến visitor@example.com.
Không thể gửi lời mời chuyển giao. Vui lòng thử lại.
PHẦN B — BẢO TOÀN BẢN NHÁP TRONG LUỒNG OTP
7. Vấn đề hiện tại cần kiểm tra

Rà soát toàn bộ hành vi khi form đã hoàn thành nhưng phải xác minh OTP:

Submit form
→ initiate OTP
→ mở OTP modal
→ OTP lỗi / đóng modal / không tiếp tục

Xác định rõ:

[ ] Form data có còn trong React state không.
[ ] Modal đóng có unmount form không.
[ ] Draft hiện tại có bị clear khi initiate OTP không.
[ ] Draft có bị clear khi OTP verify lỗi không.
[ ] submissionId có bị tạo mới mỗi lần thử lại không.
[ ] Reload trang có khôi phục được form không.
[ ] OTP modal có thể khôi phục challenge đang còn hạn không.
[ ] Resend có giữ nguyên submissionId không.

Không được phỏng đoán. Báo cáo file và dòng logic thật trước khi sửa.

8. Quy tắc lưu bản nháp

Bản nháp phải được lưu:

Theo debounce khi người dùng nhập form.
Ngay trước khi gọi API initiate OTP.
Ngay trước khi mở OTP modal.
Khi người dùng đóng OTP modal.
Khi OTP verify thất bại.
Khi OTP hết hạn.
Khi mất mạng hoặc API lỗi.
Trước khi route/page bị unmount nếu form còn dirty.

Không cần lưu raw OTP.

9. Dữ liệu draft cần lưu

Tối thiểu:

form schema version
draft namespace
submissionId
registerInfo
contactPoint
campusVisits
processing choices hợp lệ
createdAt
updatedAt
OTP target email
OTP challenge state tối thiểu nếu an toàn
OTP expiresAt nếu backend trả

Không lưu:

raw OTP
OTP hash
confirmation token
access token
refresh token
authorization header
secret
private key

Nếu dự án đã có draftNamespace, phải tiếp tục sử dụng nó.

Ví dụ namespace:

anonymous:{browser/session identifier}
user:{userId}

Không để draft của hai tài khoản dùng chung một key.

10. Quy tắc submissionId

submissionId phải được giữ ổn định xuyên suốt:

form draft
→ initiate OTP
→ resend OTP
→ verify OTP
→ network retry

Không tạo submissionId mới chỉ vì:

OTP sai;
modal đóng;
OTP hết hạn;
resend;
reload trang;
API tạm lỗi.

Chỉ tạo submission mới khi:

người dùng chủ động xóa bản nháp và bắt đầu đơn mới;
đơn cũ đã tạo thành công;
business rule backend buộc tạo submission mới và có lý do rõ ràng.

Mục đích:

tránh tạo request trùng;
giữ idempotency;
backend nhận biết cùng một submit intent.
11. Khi đóng modal OTP

Khi người dùng bấm đóng:

Không clear form.
Không clear draft.
Không clear submissionId.
Trở lại form với toàn bộ dữ liệu còn nguyên.
Hiển thị trạng thái:
Đơn của bạn đã được lưu tạm. Bạn có thể tiếp tục xác minh email sau.

Có thể hiển thị bằng toast top-right.

Không coi đóng modal là hủy đơn.

12. Khi OTP sai

Khi OTP sai:

Giữ modal mở.
Giữ form draft.
Giữ submissionId.
Không reset các ô form.
Chỉ clear input OTP nếu UX hiện tại yêu cầu.
Hiển thị lỗi OTP rõ ràng.
Cho nhập lại hoặc gửi lại OTP.

Không gọi lại initiate nếu challenge hiện tại vẫn hợp lệ.

13. Khi OTP hết hạn

Khi OTP hết hạn:

Giữ nguyên draft.
Giữ submissionId nếu backend resend contract cho phép.
Hiển thị nút Gửi lại mã.
Resend phải invalidate OTP cũ.
Không yêu cầu nhập lại toàn bộ form.
Sau resend, cập nhật expiresAt.
Không tạo request mới.
14. Khi reload hoặc quay lại trang

Khi mở lại form:

Kiểm tra draft theo namespace.
Nếu có draft hợp lệ:
khôi phục toàn bộ dữ liệu;
khôi phục submissionId;
hiển thị thông báo đã khôi phục bản nháp.
Nếu có OTP challenge còn hiệu lực:
cho phép tiếp tục xác minh;
không tự gửi OTP mới.
Nếu challenge hết hạn:
giữ form;
cho phép resend.
Nếu draft schema version cũ:
migrate an toàn hoặc báo không thể khôi phục;
không làm crash form.

Không tự submit sau khi restore.

15. Khi email người đăng ký thay đổi

Nếu OTP đã được gửi cho email A, sau đó người dùng quay lại form và đổi thành email B:

challenge của email A không được dùng cho email B;
xóa pending OTP context cũ khỏi client state;
giữ nguyên nội dung form;
khi submit lại phải initiate OTP cho email B;
backend phải kiểm tra snapshot/email binding;
verify OTP của email A với snapshot email B phải bị từ chối.
16. Khi tạo đơn thành công

Chỉ sau khi backend xác nhận request đã được tạo thành công:

[ ] Clear draft.
[ ] Clear OTP context.
[ ] Clear submissionId cũ.
[ ] Close modal.
[ ] Hiển thị toast thành công.
[ ] Reload hoặc chuyển đến detail đúng request.

Không clear draft ngay sau initiate OTP.

Không clear draft ngay sau khi email được gửi.

17. Nút quản lý bản nháp

Bổ sung hoặc kiểm tra các hành động:

Tiếp tục bản nháp
Xóa bản nháp

Xóa bản nháp phải:

có confirmation;
clear form draft;
clear pending OTP context;
clear submissionId;
không xóa request đã được tạo;
không gọi destructive backend API nếu chưa có request.
18. Draft lifecycle và quyền riêng tư

Bản nháp chứa PII nên phải:

tách theo tài khoản;
không lưu OTP;
không log toàn bộ payload;
không đưa vào URL;
không đưa vào analytics;
không khôi phục draft của user khác sau logout/login;
clear hoặc đổi namespace khi logout;
tuân theo TTL draft hiện có của dự án.

Nếu dự án chưa có TTL, ghi rõ trong báo cáo và triển khai một chính sách hợp lý, nhưng không tự thay đổi chính sách hiện có mà không nêu.

19. Không làm sai luồng authenticated

Giữ nguyên quy tắc:

Email registrant = email tài khoản đăng nhập
→ authenticated direct create
→ không OTP
Email registrant khác email tài khoản đăng nhập
→ initiate OTP
→ verify OTP
→ create

Draft phải hoạt động cho cả:

public visitor form;
authenticated user tạo hộ;
modal create;
full-page create route.

Không để draft của public form ghi đè draft của authenticated form.

20. Test frontend bắt buộc
UI chuyển giao
1. Form chuyển giao mặc định đóng.
2. Bấm nút mới mở form.
3. Desktop hiển thị hai cột.
4. Mobile hiển thị một cột.
5. Lý do chuyển giao full-width.
6. Hủy đóng form và không thay đổi đầu mối.
7. Không double-submit.
8. Thành công hiện top-right toast.
9. API lỗi hiện toast.
10. Field error vẫn inline.
11. Transfer đang tồn tại không cho tạo transfer thứ hai.
Draft và OTP
1. Draft được lưu trước initiate OTP.
2. OTP sai không clear draft.
3. OTP hết hạn không clear draft.
4. Đóng modal không clear draft.
5. Reload trang khôi phục form.
6. submissionId giữ nguyên sau OTP sai.
7. submissionId giữ nguyên sau resend.
8. Thay email invalidates pending OTP context.
9. Raw OTP không xuất hiện trong storage.
10. Verify thành công mới clear draft.
11. Network error không mất dữ liệu.
12. User A không đọc được draft của user B.
13. Public draft không đè authenticated draft.
14. Draft cũ không làm crash form.
15. Xóa bản nháp clear đúng draft và OTP context.
21. Backend/integration tests
1. OTP challenge bind đúng submissionId.
2. OTP challenge bind đúng target email.
3. Snapshot mismatch bị từ chối.
4. Resend invalidate OTP cũ.
5. Replay verify không tạo request thứ hai.
6. Retry cùng submissionId idempotent.
7. Email đổi phải initiate challenge mới.
8. Transfer duplicate bị chặn.
9. Transfer action vẫn được backend re-authorize.
10. Read-only/out-of-scope actor không thể chuyển giao.
22. Real-stack E2E
Journey A — OTP sai
Nhập đầy đủ form
→ submit
→ OTP modal mở
→ nhập OTP sai
→ lỗi hiển thị
→ đóng modal
→ form còn nguyên
→ mở lại
→ tiếp tục xác minh
Journey B — bỏ dở và reload
Nhập form
→ gửi OTP
→ đóng trình duyệt hoặc reload
→ mở lại form
→ draft được khôi phục
→ không phải nhập lại
Journey C — OTP hết hạn
Gửi OTP
→ challenge hết hạn
→ bấm gửi lại
→ form vẫn giữ nguyên
→ OTP mới xác minh thành công
→ chỉ tạo một request
Journey D — đổi email
Gửi OTP cho email A
→ quay lại sửa thành email B
→ OTP của A không verify được
→ gửi OTP mới cho B
→ tạo đúng request với B
Journey E — chuyển giao đầu mối
Mở Section 2
→ bấm chuyển giao
→ form hai cột
→ nhập dữ liệu
→ gửi lời mời
→ top-right toast
→ Section 2 chuyển sang trạng thái transfer pending
23. Build gate
dotnet build
dotnet test tests/PEMS.ArchitectureTests
dotnet test tests/PEMS.UnitTests
dotnet test tests/PEMS.IntegrationTests
npm run lint
npm run test
npm run build
git diff --check

Chạy E2E trên disposable database.

Không sử dụng database thật để test destructive flow.

24. Definition of Done
[ ] Form chuyển giao không còn mỗi input một hàng trên desktop.
[ ] Desktop dùng hai cột.
[ ] Mobile vẫn một cột và không overflow.
[ ] Lý do chuyển giao full-width.
[ ] Form mặc định không chiếm quá nhiều chiều cao.
[ ] Contact actions vẫn nằm trong Section 2.
[ ] Permission không bị mở rộng.
[ ] Toast chuyển giao nằm top-right.
[ ] Draft được lưu trước OTP.
[ ] OTP sai không làm mất draft.
[ ] OTP hết hạn không làm mất draft.
[ ] Đóng modal OTP không làm mất draft.
[ ] Reload khôi phục được form.
[ ] submissionId được giữ ổn định.
[ ] Không lưu raw OTP.
[ ] Đổi email làm challenge cũ mất hiệu lực.
[ ] Chỉ thành công mới clear draft.
[ ] Retry/replay không tạo request trùng.
[ ] Frontend tests xanh.
[ ] Backend tests xanh.
[ ] Integration tests xanh.
[ ] Real-stack E2E xanh.
[ ] Build xanh.
25. Báo cáo cuối cùng

Báo cáo phải nêu:

1. Branch và HEAD trước/sau.
2. Files changed.
3. UI transfer changes.
4. Draft storage mechanism.
5. Draft namespace/key.
6. submissionId lifecycle.
7. OTP context lifecycle.
8. Security/privacy review.
9. Tests added.
10. Test results.
11. Real-stack evidence.
12. Known limitations.
13. Việc chưa hoàn thành.

Không push trước khi toàn bộ gate chính xanh.
