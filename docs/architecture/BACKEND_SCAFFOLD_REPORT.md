# Backend Scaffold Report

## 1. Mức độ hoàn thành
- **Số lượng Use Case đã scaffold**: 135/135
- **Tình trạng Codebase**: Build thành công (0 Errors, 0 Warnings)
- **Dependency Rule**: Tuân thủ tuyệt đối quy tắc Clean Architecture. `PEMS.Application` không còn reference đến `PEMS.Infrastructure`.

## 2. Kiến trúc Layer

### Domain Layer (`PEMS.Domain`)
- Không reference bất kỳ project nào.
- Chứa Entities và Enums cốt lõi.

### Application Layer (`PEMS.Application`)
- Reference: `PEMS.Domain`
- Nuget: `MediatR`, `FluentValidation.DependencyInjectionExtensions`.
- Đã chia folder theo từng module (vd: `Campuses`, `Accounts`, `Delegations`, `News`, v.v.).
- Mỗi Use Case bao gồm:
  - Command/Query object (vd: `CreateAccountCommand`)
  - Handler class sử dụng `IRequestHandler`
  - Validator class sử dụng `AbstractValidator`
  - DTO/Response object.
- Chứa các interface hạ tầng tại `PEMS.Application/Common/Interfaces` (như `IUserRepository`, `IApplicationDbContext`, `IPartnerRepository`, v.v.).

### Infrastructure Layer (`PEMS.Infrastructure`)
- Reference: `PEMS.Domain`, `PEMS.Application`
- Chứa implementation của các interface trong Application (vd: `UserRepository`, `NotificationService`, `FileStorageService`).
- Chứa file cấu hình Dependency Injection: `DependencyInjection.cs`.

### API Layer (`PEMS.Api`)
- Reference: `PEMS.Application`, `PEMS.Infrastructure`
- Đóng vai trò là Composition Root.
- Đã tự động tạo các Controllers cho từng module (vd: `AccountsController`, `DelegationsController`) tương ứng với tất cả các API endpoints cần thiết để giao tiếp qua MediatR.
- Các API endpoints nhận HTTP method (`HttpGet` cho query, `HttpPost` cho command).

## 3. Lỗi Dependency đã khắc phục
- **Trước đây**: `PEMS.Application` reference `PEMS.Infrastructure` -> Vi phạm Clean Architecture, gây tight coupling giữa Application Service và Entity Framework (`ApplicationDbContext`).
- **Sau khi Refactor**: `PEMS.Application` hoàn toàn độc lập, sử dụng Inversion of Control qua các interface được đặt trong `Common/Interfaces`.

## 4. Danh sách các Gaps/TODOs cho giai đoạn tiếp theo
1. **Thiết kế Domain Entities**: Cần map cấu trúc bảng `pems_full.sql` vào `PEMS.Domain/Entities`.
2. **Implement Business Logic**: Thay thế `throw new NotImplementedException("...")` bằng logic nghiệp vụ thực tế dựa trên đặc tả UCs.
3. **Validation Rules**: Bổ sung validation rules vào các class `Validator` cho từng Command.
4. **Cấu hình Database**: Khởi tạo DbContext và cấu hình chuỗi kết nối thực tế trong `PEMS.Infrastructure`.
5. **Security/Permission**: Tích hợp các Policy kiểm tra quyền hạn (Role/Permission) qua middleware hoặc MediatR pipeline.
