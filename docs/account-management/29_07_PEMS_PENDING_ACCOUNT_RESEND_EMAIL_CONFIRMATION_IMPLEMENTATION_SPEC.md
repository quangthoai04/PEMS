# PEMS — IMPLEMENTATION SPEC RIÊNG
## Nút “Gửi lại email xác nhận” cho tài khoản `PENDING_EMAIL_CONFIRMATION`

> **Mục đích tài liệu:**  
> Đây là đặc tả độc lập, chỉ dành cho yêu cầu bổ sung nút **“Gửi lại email xác nhận”** trong modal chi tiết tài khoản.  
> Tài liệu này không phụ thuộc vào các yêu cầu validation email hoặc hiển thị status khác.
>
> **Repository:** `quangthoai04/PEMS`  
> **Nhánh làm việc bắt buộc:** nhánh hiện tại `Duy-Iter1`  
> **Nguồn code chuẩn:** HEAD hiện tại của `Duy-Iter1` tại thời điểm Agent bắt đầu.  
> **Không chuyển sang `Dev`, không tạo nhánh mới, không reset WIP.**
>
> **Actor chính:** HO trong trang Quản lý tài khoản.

---

# 1. Yêu cầu nghiệp vụ

Đối với tài khoản có trạng thái trong database:

```text
PENDING_EMAIL_CONFIRMATION
```

tương ứng nhãn giao diện:

```text
Chờ xác nhận email
```

khi HO bấm **Xem chi tiết tài khoản**, modal chi tiết phải hiển thị thêm nút:

```text
Gửi lại email xác nhận
```

Nút này dùng khi:

```text
- Email xác nhận kích hoạt tài khoản lần đầu gửi thất bại.
- Email đã được provider chấp nhận nhưng người nhận không thấy.
- Email vào thư rác.
- Email bị xóa hoặc thất lạc.
- Liên kết xác nhận cũ đã hết hiệu lực hoặc đã bị thay thế.
```

Khi gửi lại:

```text
- Không tạo tài khoản mới.
- Không gọi lại Create Account.
- Không đổi role/sub-role.
- Không đổi campus/department.
- Không chuyển account thành ACTIVE.
- Account vẫn giữ PENDING_EMAIL_CONFIRMATION.
- Hệ thống phát hành token xác nhận mới.
- Token cũ phải mất hiệu lực.
- Người nhận phải bấm liên kết mới thì tài khoản mới được ACTIVE.
```

---

# 2. Quy tắc hiển thị nút

## 2.1. Điều kiện bắt buộc

Chỉ hiển thị nút khi đồng thời thỏa:

```text
- Actor hiện tại là HO.
- Modal chi tiết đã tải thành công dữ liệu account từ API detail.
- Status từ response detail sau normalize là PENDING_EMAIL_CONFIRMATION.
- Account vẫn còn tồn tại và HO vẫn có quyền xem/quản lý.
```

Điều kiện gợi ý:

```ts
const normalizedDetailStatus = String(
  selectedAccount.rawStatus ?? ''
).trim().toUpperCase();

const canResendConfirmation =
  isHO &&
  detailLoaded &&
  normalizedDetailStatus === 'PENDING_EMAIL_CONFIRMATION';
```

## 2.2. Không dùng status của row list làm nguồn cuối

Không được chỉ kiểm tra:

```ts
selectedAccount.status === 'Pending'
```

vì:

```text
- Đây có thể là status đã map để hiển thị.
- Dữ liệu row có thể cũ hơn detail response.
- Có thể xảy ra race condition: account vừa được confirm ở tab khác.
```

Nguồn chuẩn phải là:

```text
details.status
```

từ API xem chi tiết tài khoản.

## 2.3. Khi detail đang loading hoặc lỗi

```text
- Không hiển thị nút resend.
- Không suy luận từ row list.
- Không cho gọi resend trước khi detail được xác nhận.
```

---

# 3. Backend hiện có cần tái sử dụng

Agent phải audit code thật trên `Duy-Iter1`.

Baseline đã có endpoint dạng:

```http
POST /api/accounts/resend-email-confirmation
```

Request:

```json
{
  "userId": 123
}
```

Response dự kiến:

```json
{
  "success": true,
  "emailNotificationStatus": "SENT",
  "resendCount": 1,
  "message": "Đã gửi lại email xác nhận."
}
```

Backend hiện có các thành phần dự kiến:

```text
backend/PEMS.Api/Controllers/AccountsController.cs

backend/PEMS.Application/Accounts/Commands/
ResendAccountEmailConfirmation/
```

Nếu endpoint/command/handler đã tồn tại và đúng contract:

```text
- Không tạo endpoint mới.
- Không tạo command mới.
- Không tạo flow resend thứ hai.
- Chỉ nối frontend và bổ sung/fix test nếu cần.
```

---

# 4. Hành vi backend bắt buộc

Backend phải bảo đảm toàn bộ:

```text
1. User tồn tại.
2. Actor được phép quản lý pending account.
3. user.status = PENDING_EMAIL_CONFIRMATION.
4. Email gửi đến đúng users.email hiện tại.
5. Token mới chỉ lưu dạng hash.
6. Token cũ bị supersede/cancel/revoke.
7. Không có hai token PENDING hợp lệ đồng thời.
8. resend_count tăng đúng.
9. Cooldown enforce phía backend.
10. Giới hạn resend enforce phía backend.
11. Không log raw token.
12. Không log full confirmation URL.
13. Không tạo user mới.
14. Không đổi role/sub-role.
15. Không đổi campus/department.
16. Không cấp quyền mới.
17. Không chuyển account thành ACTIVE.
18. Delivery outcome trả về trung thực.
```

## 4.1. Cooldown

Baseline hiện dùng:

```text
60 giây
```

Giữ rule này nếu code trên `Duy-Iter1` vẫn như vậy.

Khi gửi quá sớm:

```text
Error code: RESEND_TOO_SOON
Message: Vui lòng đợi một lát trước khi gửi lại email xác nhận.
```

## 4.2. Số lần resend tối đa

Baseline hiện dùng:

```text
5 lần
```

Khi vượt giới hạn:

```text
Error code: RESEND_LIMIT_REACHED
Message: Đã đạt số lần gửi lại tối đa. Vui lòng chỉnh sửa email hoặc liên hệ quản trị.
```

## 4.3. Account không còn pending

Nếu status đã là:

```text
ACTIVE
INACTIVE
LOCKED
```

backend phải trả lỗi ổn định:

```text
ACCOUNT_NOT_PENDING
```

Không gửi email.

---

# 5. Thay đổi frontend — endpoint

## 5.1. File

```text
frontend/pems-react/src/shared/api/endpoints.ts
```

Trong:

```ts
API_ENDPOINTS.accounts
```

thêm:

```ts
resendEmailConfirmation:
  '/accounts/resend-email-confirmation',
```

Không hardcode route trong component.

---

# 6. Thay đổi frontend — type

## 6.1. File

```text
frontend/pems-react/src/features/account-management/
types/accountManagement.types.ts
```

Thêm:

```ts
export interface ResendAccountEmailConfirmationRequest {
  userId: string | number;
}

export interface ResendAccountEmailConfirmationResponse {
  success: boolean;
  emailNotificationStatus:
    | 'SENT'
    | 'SKIPPED'
    | 'FAILED'
    | string;
  resendCount: number;
  message: string;
}
```

Không dùng `any`.

---

# 7. Thay đổi frontend — API wrapper

## 7.1. File

```text
frontend/pems-react/src/features/account-management/
api/accountManagementApi.ts
```

Import type mới và thêm method:

```ts
async resendEmailConfirmation(
  payload: ResendAccountEmailConfirmationRequest,
): Promise<ResendAccountEmailConfirmationResponse> {
  const { data } =
    await httpClient.post<ResendAccountEmailConfirmationResponse>(
      API_ENDPOINTS.accounts.resendEmailConfirmation,
      payload,
    );

  return data;
},
```

Không gọi `httpClient` trực tiếp từ JSX nếu project đã dùng API wrapper.

---

# 8. Thay đổi modal chi tiết tài khoản

## 8.1. File chính

```text
frontend/pems-react/src/pages/dashboard/accounts/
AccountManagement.tsx
```

## 8.2. Bổ sung trạng thái detail loading

Nếu hiện chưa có state riêng, thêm:

```ts
const [detailLoaded, setDetailLoaded] = useState(false);
```

Khi mở account khác:

```ts
setDetailLoaded(false);
setSelectedAccount(account);
setIsViewDrawerOpen(true);
```

Sau khi detail API thành công:

```ts
setSelectedAccount(previous => ({
  ...previous,
  rawStatus: details.status,
  email: details.email,
  // các field detail hiện có
}));

setDetailLoaded(true);
```

Khi detail lỗi:

```text
- detailLoaded vẫn false.
- Không hiển thị nút resend.
```

---

# 9. State resend frontend

Bổ sung tối thiểu:

```ts
const [isResendConfirmOpen, setIsResendConfirmOpen] =
  useState(false);

const [resendSubmitting, setResendSubmitting] =
  useState(false);

const [resendError, setResendError] =
  useState<string | null>(null);

const [resendLimitReached, setResendLimitReached] =
  useState(false);

const [lastResendCount, setLastResendCount] =
  useState<number | null>(null);

const [lastDeliveryStatus, setLastDeliveryStatus] =
  useState<string | null>(null);
```

Khi đóng detail hoặc mở account khác:

```ts
setIsResendConfirmOpen(false);
setResendSubmitting(false);
setResendError(null);
setResendLimitReached(false);
setLastResendCount(null);
setLastDeliveryStatus(null);
```

Không để state của account trước rò sang account sau.

---

# 10. Nút “Gửi lại email xác nhận”

## 10.1. Text chính xác

```text
Gửi lại email xác nhận
```

## 10.2. Vị trí

Đặt tại khu vực action của modal detail.

Ưu tiên:

```text
- Cột action bên trái.
- Hoặc footer action.
```

Không trộn với nút đổi status.

Không thay thế nút:

```text
Thay thế Staff Leader
Chỉnh sửa thông tin
```

## 10.3. UI gợi ý

```tsx
{canResendConfirmation && (
  <button
    type="button"
    onClick={() => {
      setResendError(null);
      setIsResendConfirmOpen(true);
    }}
    disabled={resendSubmitting || resendLimitReached}
    className="
      w-full inline-flex items-center justify-center gap-2
      rounded-xl border border-sky-300
      bg-sky-50 px-4 py-3
      text-sm font-bold text-sky-700
      transition-colors hover:bg-sky-100
      disabled:cursor-not-allowed disabled:opacity-60
    "
  >
    {resendSubmitting ? (
      <RefreshCw className="h-4 w-4 animate-spin" />
    ) : (
      <Mail className="h-4 w-4" />
    )}

    {resendSubmitting
      ? 'Đang gửi...'
      : 'Gửi lại email xác nhận'}
  </button>
)}
```

Helper text tùy chọn:

```text
Sử dụng khi người nhận chưa nhận được email kích hoạt tài khoản.
```

---

# 11. Confirmation dialog

Không gọi API ngay khi bấm nút.

## 11.1. Tiêu đề

```text
Gửi lại email xác nhận
```

## 11.2. Nội dung

```text
Hệ thống sẽ phát hành một liên kết xác nhận mới và gửi đến:

<email tài khoản>

Liên kết xác nhận cũ sẽ không còn hiệu lực.
Tài khoản vẫn ở trạng thái chờ xác nhận cho đến khi người nhận hoàn tất xác nhận email.
```

## 11.3. Nút

```text
Hủy
Xác nhận gửi lại
```

## 11.4. Quy tắc

```text
- Email lấy từ detail response.
- Email chỉ đọc.
- Không cho sửa email tại dialog resend.
- Không đóng detail modal.
- Không cho đóng confirmation bằng backdrop/Escape khi đang submit.
- Nút xác nhận disabled khi request đang chạy.
```

Nếu email sai, phải dùng flow sửa email pending riêng.

---

# 12. Submit flow

Pseudo-code:

```ts
const confirmResendEmail = async () => {
  if (!selectedAccount || resendSubmitting) {
    return;
  }

  const userId =
    selectedAccount.userId ?? selectedAccount.id;

  if (!userId) {
    setResendError(
      'Không xác định được tài khoản cần gửi lại email xác nhận.',
    );
    return;
  }

  setResendSubmitting(true);
  setResendError(null);

  try {
    const result =
      await accountManagementApi.resendEmailConfirmation({
        userId,
      });

    const deliveryStatus = String(
      result.emailNotificationStatus ?? '',
    ).trim().toUpperCase();

    setLastDeliveryStatus(deliveryStatus);
    setLastResendCount(result.resendCount);
    setIsResendConfirmOpen(false);

    switch (deliveryStatus) {
      case 'SENT':
        pushToast(
          'success',
          `Đã gửi lại email xác nhận đến ${selectedAccount.email}.`,
        );
        break;

      case 'SKIPPED':
        pushToast(
          'warning',
          'Yêu cầu đã được xử lý nhưng email không được gửi trong môi trường hiện tại.',
        );
        break;

      case 'FAILED':
        pushToast(
          'error',
          'Không thể gửi email xác nhận. Tài khoản vẫn ở trạng thái chờ xác nhận email.',
        );
        break;

      default:
        pushToast(
          'warning',
          'Yêu cầu đã được xử lý nhưng chưa xác định được trạng thái gửi email.',
        );
        break;
    }
  } catch (error) {
    handleResendError(error);
  } finally {
    setResendSubmitting(false);
  }
};
```

---

# 13. Chống gửi trùng

Bắt buộc:

```text
- Nếu resendSubmitting = true thì return.
- Disable nút xác nhận.
- Không tự retry.
- Không debounce bằng timer thay cho backend guard.
- Không gửi request thứ hai khi double-click.
```

Frontend chỉ là UX guard.

Backend vẫn phải enforce cooldown/idempotency phù hợp.

---

# 14. Phản ánh delivery outcome trung thực

Không được dùng:

```ts
if (result.success) {
  showSuccessToast();
}
```

vì delivery có thể là:

```text
SKIPPED
FAILED
```

## 14.1. SENT

Thông báo:

```text
Đã gửi lại email xác nhận đến <email>.
```

Sau đó:

```text
- Account vẫn pending.
- Detail modal vẫn mở.
- Không đổi status local thành ACTIVE.
```

## 14.2. SKIPPED

Thông báo warning:

```text
Yêu cầu đã được xử lý nhưng email không được gửi trong môi trường hiện tại.
```

Không báo “đã gửi thành công”.

## 14.3. FAILED

Thông báo error:

```text
Không thể gửi email xác nhận. Tài khoản vẫn ở trạng thái chờ xác nhận email.
```

Không:

```text
- Xóa account.
- Tạo lại account.
- Chuyển ACTIVE.
- Đóng detail modal.
```

## 14.4. Unknown status

Thông báo:

```text
Yêu cầu đã được xử lý nhưng chưa xác định được trạng thái gửi email.
```

---

# 15. Xử lý lỗi nghiệp vụ

Dùng chung:

```ts
getAccountErrorMessage(error, fallbackMessage)
```

## 15.1. ACCOUNT_NOT_PENDING

Thông báo:

```text
Tài khoản không còn ở trạng thái chờ xác nhận email.
```

Sau đó:

```text
- Refetch detail.
- Cập nhật status mới.
- Ẩn nút resend nếu status không còn pending.
```

## 15.2. RESEND_TOO_SOON

Hiển thị message backend:

```text
Vui lòng đợi một lát trước khi gửi lại email xác nhận.
```

Không:

```text
- Auto retry.
- Auto resend khi hết cooldown.
- Fake countdown nếu API không trả availableAt.
```

## 15.3. RESEND_LIMIT_REACHED

Hiển thị:

```text
Đã đạt số lần gửi lại tối đa. Vui lòng chỉnh sửa email hoặc liên hệ quản trị.
```

Sau đó:

```ts
setResendLimitReached(true);
```

Disable nút trong phiên modal hiện tại.

## 15.4. 403

```text
Bạn không có quyền gửi lại email xác nhận cho tài khoản này.
```

## 15.5. 404

```text
Tài khoản không tồn tại hoặc bạn không còn quyền truy cập.
```

Có thể:

```text
- Refetch list.
- Đóng modal nếu account không còn tồn tại.
```

## 15.6. Network/server error

```text
Không thể gửi lại email xác nhận. Vui lòng thử lại sau.
```

Giữ detail modal mở.

---

# 16. Hành vi sau resend

Sau `SENT`, `SKIPPED` hoặc `FAILED`:

```text
- Account vẫn PENDING_EMAIL_CONFIRMATION.
- Không đổi status local sang ACTIVE.
- Không đóng detail modal.
- Không reset selected account.
- Không reset toàn bộ account list.
```

Có thể refetch detail/list theo pattern hiện tại, nhưng không được tạo ra trạng thái giả.

Nút vẫn có thể hiển thị vì account vẫn pending.

Backend quyết định:

```text
- Có đang cooldown hay không.
- Đã đạt max resend hay chưa.
```

---

# 17. Trường hợp gửi lần đầu bị lỗi

Luồng tạo account nên giữ contract:

```text
Tạo user pending
→ tạo confirmation token
→ commit dữ liệu
→ gửi email sau commit
```

Nếu email lần đầu thất bại:

```text
- User vẫn tồn tại.
- Status vẫn PENDING_EMAIL_CONFIRMATION.
- HO vẫn xem được account.
- Detail modal hiện nút resend.
- Resend chỉ phát hành token mới.
```

Không rollback account chỉ vì email delivery thất bại nếu contract hiện tại của project vẫn là commit-first/send-after-commit.

---

# 18. Database

Không cần migration mới nếu hiện có:

```text
account_email_confirmations
token_hash
status
expires_at
resend_count
created_at
updated_at
```

Không thêm cột chỉ để hiển thị nút.

Điều kiện UI tối thiểu:

```text
users.status = PENDING_EMAIL_CONFIRMATION
```

---

# 19. File dự kiến thay đổi

## 19.1. Frontend bắt buộc

```text
frontend/pems-react/src/shared/api/endpoints.ts

frontend/pems-react/src/features/account-management/
types/accountManagement.types.ts

frontend/pems-react/src/features/account-management/
api/accountManagementApi.ts

frontend/pems-react/src/pages/dashboard/accounts/
AccountManagement.tsx
```

## 19.2. Backend cần audit

```text
backend/PEMS.Api/Controllers/AccountsController.cs

backend/PEMS.Application/Accounts/Commands/
ResendAccountEmailConfirmation/
```

## 19.3. Tests

```text
tests/PEMS.UnitTests/Accounts/EmailConfirmation/
ResendAndEditPendingEmailTests.cs
```

Bổ sung frontend tests theo cấu trúc test thật của project.

---

# 20. Test frontend bắt buộc

```text
1. Detail status PENDING_EMAIL_CONFIRMATION → hiện nút.
2. Detail status ACTIVE → không hiện.
3. Detail status INACTIVE → không hiện.
4. Detail status LOCKED → không hiện.
5. Detail loading → không hiện.
6. Detail API error → không hiện.
7. Bấm resend → mở confirmation, chưa gọi API.
8. Bấm Hủy → không gọi API.
9. Bấm xác nhận → gọi đúng endpoint.
10. Request gửi đúng userId.
11. Double-click → chỉ một request.
12. SENT → success toast đúng.
13. SKIPPED → warning, không báo thành công.
14. FAILED → error, account vẫn pending.
15. RESEND_TOO_SOON → message đúng.
16. RESEND_LIMIT_REACHED → disable nút.
17. ACCOUNT_NOT_PENDING → refetch detail.
18. Mở account khác → reset resend state.
19. Đóng modal khi idle → reset state.
20. Không đóng confirmation khi đang submit.
```

---

# 21. Test backend bắt buộc

```text
1. HO hợp lệ resend pending account thành công.
2. Actor ngoài scope bị từ chối.
3. User không tồn tại → 404.
4. ACTIVE → ACCOUNT_NOT_PENDING.
5. INACTIVE → ACCOUNT_NOT_PENDING.
6. LOCKED → ACCOUNT_NOT_PENDING.
7. Gửi quá sớm → RESEND_TOO_SOON.
8. Vượt max → RESEND_LIMIT_REACHED.
9. Token cũ mất hiệu lực.
10. Token mới confirm được.
11. Chỉ một token pending hợp lệ.
12. resend_count tăng đúng.
13. Account vẫn PENDING_EMAIL_CONFIRMATION.
14. Không tạo user mới.
15. Không đổi role.
16. Không đổi sub-role.
17. Không đổi campus.
18. Không đổi department.
19. SENT được trả đúng.
20. SKIPPED được trả đúng.
21. FAILED được trả đúng.
22. Không log raw token.
23. Không log full URL chứa token.
24. Không gửi email khi account không pending.
```

---

# 22. Manual verification

## 22.1. Pending account

1. Đăng nhập HO.
2. Mở trang Quản lý tài khoản.
3. Chọn account `PENDING_EMAIL_CONFIRMATION`.
4. Bấm Xem chi tiết.
5. Xác nhận có nút `Gửi lại email xác nhận`.
6. Bấm nút.
7. Xác nhận dialog hiển thị đúng email.
8. Bấm Hủy.
9. Xác nhận không có request.
10. Mở lại và bấm xác nhận gửi.
11. Kiểm tra loading và chống double-click.
12. Kiểm tra toast theo delivery status.
13. Xác nhận account vẫn pending.

## 22.2. Non-pending account

Mở detail:

```text
ACTIVE
INACTIVE
LOCKED
```

Expected:

```text
Không có nút Gửi lại email xác nhận.
```

## 22.3. Cooldown

Gửi lại lần thứ hai ngay sau lần đầu.

Expected:

```text
RESEND_TOO_SOON
```

Không gửi thêm email.

## 22.4. Token

Trong môi trường test:

```text
- Link cũ không confirm được sau resend.
- Link mới confirm được.
- Sau confirm thành công, user chuyển ACTIVE.
- Mở detail lại thì nút resend biến mất.
```

---

# 23. Preflight bắt buộc

Chạy:

```bash
git status --short --branch
git branch --show-current
git rev-parse HEAD
git log -10 --oneline --decorate
git stash list
git diff --check
```

Điều kiện:

```text
- Branch phải là Duy-Iter1.
- Không tự chuyển sang Dev.
- Không reset/rebase/clean.
- Không xóa hoặc ghi đè WIP.
- Không dùng git add .
```

Nếu branch không phải `Duy-Iter1` hoặc có WIP ngoài task không xác định được:

```text
Dừng trước khi sửa và báo cáo.
```

---

# 24. Search/audit bắt buộc

```bash
rg -n \
  "resend-email-confirmation|ResendAccountEmailConfirmation|RESEND_TOO_SOON|RESEND_LIMIT_REACHED|ACCOUNT_NOT_PENDING" \
  frontend backend tests
```

```bash
rg -n \
  "PENDING_EMAIL_CONFIRMATION|rawStatus|details.status|viewaccountdetails" \
  frontend/pems-react/src/pages/dashboard/accounts \
  frontend/pems-react/src/features/account-management
```

Phân loại:

```text
- Backend endpoint.
- Backend handler.
- Frontend endpoint.
- Frontend API wrapper.
- Frontend types.
- Modal detail.
- Unit test.
- Integration test.
- Docs/legacy.
```

---

# 25. Build và test gate

## Backend

```bash
dotnet build
```

```bash
dotnet test tests/PEMS.UnitTests/PEMS.UnitTests.csproj
```

```bash
dotnet test tests/PEMS.IntegrationTests/PEMS.IntegrationTests.csproj
```

Dùng filter targeted trước nếu cần, nhưng phải chạy full regression phù hợp trước khi kết luận hoàn thành.

## Frontend

```bash
cd frontend/pems-react
npm run type-check
npm run test -- --run
npm run build
```

Dùng đúng script thật trong `package.json`.

## Static check

```bash
git diff --check
```

---

# 26. Không được làm

```text
- Không tạo endpoint resend thứ hai.
- Không gọi Create Account để resend.
- Không tạo user trùng.
- Không tự ACTIVE account.
- Không cho resend non-pending account.
- Không tin row status khi detail chưa tải.
- Không báo SENT khi status là SKIPPED/FAILED.
- Không lưu token plaintext.
- Không đưa token về frontend.
- Không log raw token.
- Không log full confirmation URL.
- Không bỏ cooldown.
- Không bỏ max resend.
- Không reset detail khi resend lỗi.
- Không sửa schema nếu không cần.
- Không thay role/campus/department.
- Không chuyển branch.
- Không reset WIP.
```

---

# 27. Definition of Done

```text
[ ] Branch = Duy-Iter1.
[ ] WIP được bảo toàn.
[ ] Detail modal dùng status từ API detail.
[ ] Pending account hiện nút Gửi lại email xác nhận.
[ ] Non-pending account không hiện nút.
[ ] Detail loading/error không hiện nút.
[ ] Có confirmation dialog.
[ ] Email hiển thị read-only.
[ ] Endpoint frontend được khai báo.
[ ] Request/response type đầy đủ.
[ ] API wrapper được nối.
[ ] Có loading state.
[ ] Có double-click guard.
[ ] SENT hiển thị success đúng.
[ ] SKIPPED không bị báo thành công.
[ ] FAILED hiển thị lỗi đúng.
[ ] RESEND_TOO_SOON được xử lý.
[ ] RESEND_LIMIT_REACHED được xử lý.
[ ] ACCOUNT_NOT_PENDING refetch detail.
[ ] Token cũ hết hiệu lực.
[ ] Token mới hoạt động.
[ ] Chỉ một token pending hợp lệ.
[ ] resend_count tăng đúng.
[ ] Account vẫn pending sau resend.
[ ] Không tạo user mới.
[ ] Không đổi role/sub-role/campus/department.
[ ] Backend unit test xanh.
[ ] Backend integration test xanh.
[ ] Frontend test xanh.
[ ] Frontend type-check xanh.
[ ] Frontend build xanh.
[ ] Backend build xanh.
[ ] git diff --check xanh.
```

---

# 28. Mẫu báo cáo cuối cùng Agent phải trả

```markdown
# Kết quả triển khai resend email confirmation

## 1. Preflight
- Branch:
- HEAD:
- Working tree:
- WIP được bảo toàn:
- git diff --check:

## 2. Audit
- Endpoint backend hiện có:
- Handler hiện có:
- Cooldown:
- Max resend:
- Authorization:
- Token supersede:
- Frontend còn thiếu:

## 3. File đã sửa

### Frontend
- ...

### Backend
- ...

### Tests
- ...

## 4. UI
- Điều kiện hiện nút:
- Vị trí nút:
- Confirmation dialog:
- Loading/double-click guard:

## 5. API
- Endpoint:
- Request:
- Response:
- Error mapping:

## 6. Delivery outcome
- SENT:
- SKIPPED:
- FAILED:
- Unknown:

## 7. Security
- Raw token storage:
- Token cũ:
- Active token count:
- Cooldown:
- Max resend:
- Logging:

## 8. Tests
- Backend build:
- Unit:
- Integration:
- Frontend type-check:
- Frontend test:
- Frontend build:
- Manual verification:

## 9. Kết luận
- PASS / FAIL / PARTIAL
- Blocker nếu có:
```

---

# 29. Lệnh giao việc ngắn gọn

```text
Đọc toàn bộ file này trước khi sửa.

Tiếp tục làm việc ngay trên nhánh Duy-Iter1. Không chuyển sang Dev, không tạo nhánh mới và không reset WIP.

Triển khai riêng chức năng: trong modal View Account Detail của trang Quản lý tài khoản HO, khi detail API trả status PENDING_EMAIL_CONFIRMATION thì hiển thị nút "Gửi lại email xác nhận".

Tái sử dụng endpoint backend resend-email-confirmation hiện có nếu code trên Duy-Iter1 đã triển khai đúng. Nối endpoint/type/API wrapper frontend; thêm confirmation dialog, loading, double-click guard; xử lý SENT/SKIPPED/FAILED trung thực; xử lý ACCOUNT_NOT_PENDING, RESEND_TOO_SOON, RESEND_LIMIT_REACHED; token cũ phải mất hiệu lực; account vẫn pending cho tới khi người nhận confirm.

Không tạo account mới, không ACTIVE account khi resend, không đổi role/campus/department, không thêm migration nếu không cần.

Chạy build/test đầy đủ và trả báo cáo theo mẫu cuối file.
```
