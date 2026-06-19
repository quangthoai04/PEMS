<!-- =====================================================================
PEMS DOC UPDATE v8.2-full-preserved-cancel-delegation-no-external-note
Generated: 2026-06-19
Mode: PRESERVE ORIGINAL CONTENT + APPEND ADDENDUM.
No original section below has been removed or compressed.
The addendum section at the end is the authoritative update for cancellation UC-136.
===================================================================== -->

# Cẩm nang Kiến trúc & Quy tắc Lập trình (PEMS Backend)

Tài liệu này là bộ luật bắt buộc (Strict Rulebook) dành cho toàn bộ Developer tham gia phát triển tầng Backend của PEMS. Bất kỳ Pull Request nào vi phạm các nguyên tắc dưới đây đều phải bị từ chối (Reject).

---

## 1. Vòng đời của một Request (The Request Pipeline)

Khi một HTTP Request gửi đến PEMS Backend, nó sẽ đi qua 3 lớp phòng ngự và xử lý. Mỗi thành phần có một nhiệm vụ DUY NHẤT.

### Lớp 1: API Layer (Tầng ngoài cùng - `PEMS.Api`)
* **Routing & Controllers**: Chỉ nhận Request, gọi `IMediator`, và trả kết quả. KHÔNG CHỨA LOGIC `if/else` nghiệp vụ.
* **RateLimiting (Chống Spam)**: `RateLimitMiddleware.cs` chặn các IP gọi API quá nhanh.
* **Authentication**: `JwtBearer` giải mã Token. `CurrentUserMiddleware` lấy `UserId`, `CampusId` gắn vào Context.
* **Authorization**: Attribute `[PermissionAuthorize("UC-xxx")]` chặn những User không có quyền gọi Use Case.
* **Exception Handling**: `ExceptionHandlingMiddleware.cs`. **TUYỆT ĐỐI KHÔNG DÙNG `try-catch` BỪA BÃI** trong ruột ứng dụng. Cứ quăng Exception, Middleware này sẽ "chụp" lại và tự động chuyển thành JSON lỗi (Status 400, 404, 500) trả về cho Frontend.

### Lớp 2: MediatR Pipeline (Tầng Application - Màng lọc tự động)
Request được đóng gói thành `Command` hoặc `Query` và chui vào đường ống (Pipeline). Ở đây có các `Behaviours` tự động chạy:
* **`IdempotencyBehaviour`**: Chống click đúp (Double Submit). Nếu Frontend lỡ gửi 2 request giống hệt nhau cùng lúc, cái thứ 2 sẽ bị hủy.
* **`ValidationBehaviour`**: Tự động lấy các luật trong **FluentValidation** ra chạy. Nếu sai định dạng (ví dụ: thiếu Email), nó tự quăng `ValidationException` và trả về lỗi 400.
* **`TransactionBehaviour`**: Tự động gộp toàn bộ các thao tác DB vào một `Transaction`. Nếu xử lý thành công, nó tự gọi `Commit()`. Nếu có bất kỳ lỗi nào, nó tự động `Rollback()`. **Dev không cần gọi `SaveChanges()` bằng tay trong Handler.**
* **`AuditLogBehaviour`**: Tự động ghi lại lịch sử ai vừa gọi hàm gì vào Database.

### Lớp 3: Business Logic (Lõi nghiệp vụ)
* **Application Handlers** (`<Name>CommandHandler`): Nơi nhận dữ liệu "đã sạch", dùng Repository để truy vấn DB (vd: check trùng lặp), và điều phối các Entity.
* **Domain Entities** (`PEMS.Domain/Entities`): Nơi chứa logic cốt lõi. Entity không được rỗng (Anemic). Mọi logic tính toán, thay đổi trạng thái phải nằm bên trong Entity.

---

## 2. Quy tắc Validation (Kiểm tra dữ liệu) - ĐẶC BIỆT QUAN TRỌNG

Chúng ta chia Validation làm 2 loại. Phải viết đúng chỗ:

### Loại 2.1: Input Validation (Xác thực đầu vào cơ bản)
* **Bản chất**: Kiểm tra chuỗi rỗng, độ dài chuỗi, đúng định dạng Email/Phone/Regex. KHÔNG cần chọc vào Database.
* **Viết ở đâu**: Viết tại class `Validator` dùng **FluentValidation**.
* **Code Mẫu (DO)**:
  ```csharp
  // Tại PEMS.Application/.../CreateAccountCommandValidator.cs
  public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
  {
      public CreateAccountCommandValidator()
      {
          RuleFor(x => x.Email).NotEmpty().EmailAddress();
          RuleFor(x => x.FullName).MaximumLength(100);
      }
  }
  ```

### Loại 2.2: Business Validation (Xác thực nghiệp vụ DB)
* **Bản chất**: Kiểm tra xem Email đã tồn tại chưa? Campus này có bị khóa không?
* **Viết ở đâu**: Viết tại `Handler`. Dùng Repository để kiểm tra. Nếu vi phạm, quăng `BusinessRuleException` hoặc `ConflictException`.
* **Code Mẫu (DO)**:
  ```csharp
  // Tại PEMS.Application/.../CreateAccountCommandHandler.cs
  var exists = await _userRepository.ExistsByEmailAsync(request.Email);
  if (exists) {
      throw new ConflictException($"Email {request.Email} đã tồn tại trong hệ thống.");
  }
  ```

---

## 3. Bảng Tóm Tắt Trách Nhiệm (Responsibility Matrix) - DOs & DON'Ts

| Chức năng cần làm | DO (Nên viết ở đâu) | DON'T (Tuyệt đối cấm) |
| :--- | :--- | :--- |
| **Bảo vệ API chống Spam/DDoS** | `PEMS.Api/Middleware/RateLimitMiddleware.cs` | Cấm viết logic đếm số lần gọi ở từng Controller. |
| **Phân quyền người dùng (RBAC)** | Dùng `[PermissionAuthorize("UC-01...")]` ở Controller. | Cấm viết `if (user.Role == "Admin")` cứng trong Handler. |
| **Lưu dữ liệu vào Database** | Dùng `.Add(entity)` trong Handler. `TransactionBehaviour` sẽ lo phần Save. | Cấm tự gọi `_dbContext.SaveChanges()` lắt nhắt nhiều lần. |
| **Xử lý mã lỗi HTTP 400, 404, 500** | Cứ dùng `throw new NotFoundException()`. Middleware sẽ tự bắt và chuyển thành 404. | Cấm viết `try-catch` trong Controller rồi `return BadRequest()`. |
| **Logic thay đổi trạng thái Entity** | Viết Method trực tiếp trong class Entity (VD: `user.Deactivate()`). | Cấm viết `user.Status = Inactive` trực tiếp từ Handler (Anemic Domain). |
| **Gọi API bên thứ 3 (Email, OCR)** | Định nghĩa `Interface` ở tầng Application, viết code thật ở thư mục `PEMS.Infrastructure/ExternalServices/` | Cấm nhúng thư viện gửi Email (như MailKit) trực tiếp vào Application. |

---

## 4. CQRS & Quy Tắc Trả Về (Response Conventions)

### 4.1 Quy tắc đặt tên (Naming)
* **Command**: Đổi state (Create/Update/Delete) -> `<Action><Entity>Command`. Trả về `Guid` hoặc `MessageResponse`.
* **Query**: Chỉ đọc dữ liệu (Read) -> `<Action><Entity>Query`. Trả về `<Entity>Dto` hoặc `PagedResult<Dto>`.
* **Handler**: Tên Command/Query + `Handler`. Ví dụ: `CreateAccountCommandHandler`.

### 4.2 Khi nào GỘP Response / Khi nào GIỮ riêng
Để tránh tạo ra hàng chục file class dư thừa chỉ chứa biến `string Message`:

**Trường hợp GỘP (Sử dụng Shared Response):**
* Áp dụng khi hàm chỉ trả về một câu thông báo đơn giản (Logout, Delete, Forgot Password, Change Status).
* Sử dụng class dùng chung: **`MessageResponse`** (nằm ở `PEMS.Application/<Module>/Models/MessageResponse.cs`).
* Hoặc khi Create xong chỉ cần trả về ID: Dùng chung class **`CreatedResponse(Guid Id)`**.

**Trường hợp GIỮ (Tạo Response/Dto riêng):**
* Áp dụng khi dữ liệu trả về mang tính phức tạp, đặc thù cho giao diện đó (Ví dụ: `LoginViaCredentialsResponse` trả về JWT Token, hoặc `ViewAccountDetailsDto` trả về FullName, Campus, Role...).
* **Bắt buộc** đặt file Dto/Response này nằm ngay cạnh thư mục chứa Command/Query tương ứng.

---

## 5. Quy tắc Entity Framework Core & Repository

1. **KHÔNG BAO GIỜ trả Entity trực tiếp qua API**: Thực thể Domain (Entity) chứa dữ liệu bảo mật (như PasswordHash, ID nội bộ). Khi trả dữ liệu ra Controller, **BẮT BUỘC** phải Map sang DTO (Data Transfer Object).
2. **Không dùng Data Annotations trên Entity**: 
   * **DON'T**: Cấm viết `[MaxLength(100)]` hay `[Table("users")]` bên trong thư mục `PEMS.Domain`. Điều này làm bẩn Domain Model với các khái niệm của DB.
   * **DO**: Bắt buộc phải viết cấu hình vào các file `Configuration.cs` tại `PEMS.Infrastructure/Persistence/Configurations/` sử dụng `IEntityTypeConfiguration<T>`.
3. **Repository là Kẻ hầu hạ (Dumb DB Accessor)**:
   * Repository chỉ chứa các lệnh cơ bản: `GetById`, `Add`, `Update`, `ExistsBy...`.
   * Cấm viết logic nghiệp vụ (VD: Tính tiền, kiểm tra quyền) bên trong Repository.

---

# Addendum — Clean Architecture cho UC-136 Cancel Visit Request


## V8.2 Addendum — UC-136 Cancel Visit Request thuộc Delegation Reception Management

> Phần này là nội dung bổ sung, không xóa nội dung gốc. Nếu nội dung gốc có flow cũ như “đã duyệt nhưng chưa có host” hoặc “mỗi cơ sở duyệt lại sau HO”, hãy ưu tiên rule V8.2 trong phần addendum này.

### 1. Feature ownership

UC hủy đơn thăm thuộc **FE-02 — Quản lý Tiếp đón Đoàn khách / Delegation Reception Management** vì đây là thao tác trên vòng đời đoàn/visit request, không phải bước submit form.

```text
Feature: FE-02 Delegation Reception Management
UC: UC-136 Cancel Visit Request
Permission code: UC-136.CANCEL_VISIT_REQUEST
```

### 2. Không dùng `external_confirmation_note`

Không tạo cột `external_confirmation_note`. Khi Host hủy thay khách dựa trên xác nhận ngoài hệ thống, toàn bộ thông tin xác nhận được ghi vào `cancellation_reason`.

```text
cancellation_source = EXTERNAL_CONFIRMATION
cancellation_reason = "Khách xác nhận hủy qua email/điện thoại/Zalo..., thời gian..., người xác nhận..., lý do..."
```

### 3. Cancellation metadata chuẩn

Áp dụng cho `visit_requests` và `visit_request_campuses`:

```sql
cancelled_by BIGINT UNSIGNED NULL,
cancelled_at DATETIME NULL,
cancellation_actor_type ENUM('VISITOR','HOST','STAFF_LEADER','HO','SYSTEM') NULL,
cancellation_source ENUM('SELF_SERVICE','EXTERNAL_CONFIRMATION','INTERNAL_DECISION') NULL,
cancellation_reason TEXT NULL
```

### 4. Meaning của `cancellation_source`

| Value | Meaning | Khi dùng |
|---|---|---|
| `SELF_SERVICE` | Người dùng tự thao tác trên hệ thống | Visitor tự hủy đơn của chính họ |
| `EXTERNAL_CONFIRMATION` | Hủy dựa trên xác nhận ngoài hệ thống | Host hủy thay khách sau khi khách xác nhận qua email/điện thoại/Zalo/gặp trực tiếp |
| `INTERNAL_DECISION` | Nội bộ hủy vì lý do vận hành | HO/Staff Leader hủy vì campus không thể tiếp, trùng lịch, lý do tổ chức |

### 5. Rule hủy theo role

| Actor | Scope | Nguồn hủy hợp lệ | Ghi chú |
|---|---|---|---|
| Visitor | Đơn của chính họ | `SELF_SERVICE` | Chỉ hủy khi chưa vào giai đoạn `DURING_VISIT`, `AFTER_VISIT`, `CLOSED` |
| Host | Campus instance mình đang phụ trách | `EXTERNAL_CONFIRMATION` | Bắt buộc nhập `cancellation_reason` rõ kênh/thời điểm/người xác nhận |
| Staff Leader | Đơn/campus thuộc campus mình | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Không xử lý campus khác |
| HO | `MULTI_CAMPUS` | `INTERNAL_DECISION` hoặc `EXTERNAL_CONFIRMATION` | Có thể hủy request tổng liên cơ sở nếu nghiệp vụ cho phép |
| Admin | Không có quyền nghiệp vụ visit/delegation | Không áp dụng | ADMIN không được hủy delegation |

### 6. Rule trạng thái

- `visit_requests.status = CANCELLED` dùng khi hủy request/delegation tổng.
- `visit_request_campuses.status = CANCELLED` dùng khi hủy một campus instance.
- Không cho hủy campus instance nếu đã vào `DURING_VISIT`, `AFTER_VISIT`, hoặc `CLOSED`.
- Không dùng `CANCELLED` thay cho `REJECTED`. Nếu đơn đang `PENDING_APPROVAL` và người duyệt không chấp nhận, dùng reject flow.

### 7. Vị trí code Clean Architecture

```text
PEMS.Application/Delegations/Commands/CancelVisitRequest/
├── CancelVisitRequestCommand.cs
├── CancelVisitRequestCommandHandler.cs
├── CancelVisitRequestCommandValidator.cs
└── CancelVisitRequestResponse.cs
```

Controller chỉ nhận request và gọi `IMediator`. Logic kiểm tra scope, current host, request/campus status, và cancellation metadata nằm trong Handler/Domain Entity.


## 8. Handler responsibilities

`CancelVisitRequestCommandHandler` cần làm đủ các bước:

1. Load `visit_requests` và campus instances liên quan.
2. Resolve current user: `UserId`, `RoleCode`, `SubRole`, `PrimaryCampusId`.
3. Kiểm tra permission `UC-136.CANCEL_VISIT_REQUEST`.
4. Kiểm tra data scope:
   - Visitor chỉ hủy request của chính họ.
   - Host chỉ hủy campus instance mà `current_host_user_id = CurrentUser.UserId`.
   - Staff Leader chỉ thao tác trong `CurrentUser.PrimaryCampusId`.
   - HO chỉ thao tác request `MULTI_CAMPUS`.
   - Admin không có route nghiệp vụ này.
5. Kiểm tra trạng thái hợp lệ.
6. Set `status = CANCELLED` và cancellation metadata.
7. Ghi `visit_status_logs` với `status_owner_type = REQUEST` hoặc `CAMPUS_INSTANCE`.
8. Gửi notification/email nếu nghiệp vụ yêu cầu.

## 9. API route đề xuất

```http
POST /api/delegations/{visitRequestId}/cancel
POST /api/delegations/{visitRequestId}/campuses/{visitInstanceId}/cancel
```

Không đặt route này trong public submit-form controller, vì đây là thao tác vòng đời delegation sau khi request đã tồn tại.
