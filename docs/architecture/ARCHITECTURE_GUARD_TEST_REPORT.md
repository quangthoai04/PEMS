# Architecture Guard Test Report

## 1. Danh sách rule đã khóa (Locked Rules)

Để bảo vệ cấu trúc Clean Architecture của PEMS Backend, các rule sau đã được tự động hoá thông qua Architecture Guard Tests:

### Dependency Rules (Nguyên tắc phụ thuộc)
- **Domain**: Không được phép reference đến Application, Infrastructure, hay Api.
- **Application**: Không được phép reference đến Infrastructure hay Api.
- **Infrastructure**: Không được phép reference đến Api.

### Namespace & Concrete Class Rules (Quy tắc Namespace & Lớp cụ thể)
- **Application** không được phép chứa `using PEMS.Infrastructure`.
- **Application** không được phép khởi tạo hoặc có dependency đến các lớp concrete của tầng Infrastructure bao gồm: `ApplicationDbContext`, `UserRepository`, `DelegationRepository`, `JwtTokenService`, `PasswordHasher`, `RefreshTokenStore`, `EmailService`, `RedisRateLimitStore`, `OcrService`, `FaceRecognitionService`.

### Controller Rules (Quy tắc cho Controller)
- **Controller** không được phép inject `DbContext`.
- **Controller** không được phép inject bất kỳ Repository concrete nào.
- **Controller** không được phép inject bất kỳ Infrastructure service concrete nào.
- **Controller** chỉ được phép tương tác thông qua abstraction (VD: `IMediator`) và hoàn toàn bị cấm dependency trực tiếp tới tầng Infrastructure.

### Application Handler Rules (Quy tắc cho Handler)
- **Handler** không được phép inject các class concrete của Infrastructure.
- **Handler** phải tuân thủ nghiêm ngặt Dependency Inversion (chỉ inject các interface từ Application/Common/Interfaces).

---

## 2. Danh sách test đã tạo (Created Tests)

Project `tests/PEMS.ArchitectureTests` đã được setup bằng **xUnit** và **NetArchTest.Rules**. Tổng cộng **14 tests** thực tế (không có test nào bị Skip) đã được thiết lập:

- **`DependencyRuleTests.cs`**:
  - `Domain_ShouldNot_ReferenceApplication`
  - `Domain_ShouldNot_ReferenceInfrastructure`
  - `Domain_ShouldNot_ReferenceApi`
  - `Application_ShouldNot_ReferenceInfrastructure`
  - `Application_ShouldNot_ReferenceApi`
  - `Infrastructure_ShouldNot_ReferenceApi`

- **`NamespaceAndConcreteClassTests.cs`**:
  - `Application_ShouldNot_UseInfrastructureNamespace`
  - `Application_ShouldNot_UseConcreteClasses`

- **`ControllerTests.cs`**:
  - `Controllers_ShouldNot_InjectDbContext`
  - `Controllers_ShouldNot_InjectConcreteRepositories`
  - `Controllers_ShouldNot_InjectInfrastructureConcreteServices`
  - `Controllers_Should_OnlyInjectIMediatorOrAbstractions`

- **`ApplicationHandlerTests.cs`**:
  - `Handlers_ShouldNot_InjectInfrastructureConcreteClass`
  - `Handlers_ShouldNot_InjectSpecificInfrastructureConcreteClasses`

---

## 3. Kết quả `dotnet test`

Đã thực thi chạy kiểm thử kiến trúc trực tiếp trên source code với lệnh `dotnet test tests/PEMS.ArchitectureTests`.

```text
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    14, Skipped:     0, Total:    14, Duration: 1 s - PEMS.ArchitectureTests.dll (net9.0)
```
Kết quả: **Thành công 100%**.

---

## 4. Lỗi kiến trúc và file vi phạm

**Trạng thái hiện tại: SẠCH (0 lỗi).** 

Hệ thống backend 135 Use Cases hoàn toàn tuân thủ Clean Architecture. Tất cả các Architecture Guard Tests đã được thiết lập thành công. Từ giờ trở đi, bất kỳ thay đổi nào làm phá vỡ Dependency Rule (ví dụ: Controller gọi thẳng `DbContext`, Application gọi thẳng code `Infrastructure`) đều sẽ bị báo đỏ (`Failed`) khi chạy test.
