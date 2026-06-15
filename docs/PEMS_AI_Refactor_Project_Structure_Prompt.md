# PROMPT CHO AI: TÁI CẤU TRÚC TOÀN BỘ DỰ ÁN PEMS THEO CLEAN ARCHITECTURE, GIỮ FRONTEND HIỆN CÓ

## 0. Vai trò của AI

Bạn là **Senior Full-stack Architect + Senior .NET Clean Architecture Developer + Senior React Refactoring Engineer**.

Bạn sẽ làm việc trên project PEMS hiện tại. Nhiệm vụ của bạn là **tái cấu trúc toàn bộ dự án theo kiến trúc chuẩn**, tạo sẵn đầy đủ thư mục và file cần thiết, nhưng **không phá vỡ frontend hiện có** vì frontend đã hoàn thành khoảng 80%.

---

## 1. Bối cảnh dự án

Project: **PEMS - Phân hệ quản lý HTQT / Visitor Management System**.

Công nghệ hiện tại:

- Frontend: React, Vite, TypeScript, Tailwind CSS.
- Backend: .NET 8 Web API, Entity Framework Core, MySQL.
- Database: MySQL, hiện có file SQL `docs/pems_full.sql`.
- Documentation: có các file UC, Permission Matrix, Project Structure.

Cấu trúc hiện tại đang có:

```text
PEMS/
├── Application/
├── Domain/
├── Infrastructure/
├── Pems_WebAPI/
├── docs/
└── Pems_React/
    └── fpt-education---htqt_ver10 (2)/
        └── fpt-education---htqt_ver10/
            ├── package.json
            ├── vite.config.ts
            ├── tsconfig.json
            ├── index.html
            └── src/
```

Frontend thật sự đang nằm ở:

```text
Pems_React/fpt-education---htqt_ver10 (2)/fpt-education---htqt_ver10/
```

Trong frontend hiện có các màn hình quan trọng tại:

```text
src/pages/
src/pages/dashboard/
src/components/
src/assets/
```

**Yêu cầu quan trọng:** Frontend đã làm gần xong, không được làm lại frontend từ đầu.

---

## 2. Mục tiêu refactor

Hãy tái cấu trúc project thành dạng:

```text
PEMS/
├── backend/
│   ├── PEMS.Api/
│   ├── PEMS.Application/
│   ├── PEMS.Domain/
│   ├── PEMS.Infrastructure/
│   └── PEMS.SharedKernel/
│
├── frontend/
│   └── pems-react/
│
├── database/
│   ├── scripts/
│   ├── migrations/
│   └── seed/
│
├── docs/
│   ├── use-cases/
│   ├── permissions/
│   ├── architecture/
│   ├── api/
│   └── database/
│
├── tests/
│   ├── PEMS.UnitTests/
│   ├── PEMS.ApplicationTests/
│   └── PEMS.IntegrationTests/
│
├── tools/
│   ├── frontend-scripts/
│   └── database-scripts/
│
├── .gitignore
├── README.md
└── PEMS.sln
```

---

## 3. Nguyên tắc bắt buộc khi thực hiện

### 3.1. Không phá frontend hiện có

Không được làm các việc sau:

```text
- Không viết lại toàn bộ frontend.
- Không đổi route hàng loạt trong App.tsx.
- Không đổi thứ tự màn hình dashboard.
- Không đổi tên component/page nếu không thật sự cần.
- Không xóa các page hiện tại.
- Không xóa assets hiện tại khi chưa kiểm tra có đang được import hay không.
- Không đổi flow bấm nút hiện tại nếu không có lỗi rõ ràng.
- Không chuyển toàn bộ pages sang features ngay lập tức.
```

Được phép làm:

```text
- Chuyển nguyên frontend root ra `frontend/pems-react/`.
- Thêm folder `src/shared/`.
- Thêm folder `src/features/`.
- Thêm API layer tập trung.
- Thêm type/dto/adapters.
- Thay dần các đoạn fetch/axios trực tiếp bằng các hàm API service.
- Giữ nguyên UI, route và layout hiện tại.
```

### 3.2. Không phá backend hiện có

Không được xóa code cũ ngay. Hãy:

```text
- Tạo cấu trúc mới trước.
- Di chuyển hoặc copy có kiểm soát.
- Giữ backup file cũ trong quá trình chuyển.
- Sau mỗi nhóm thay đổi phải build thử.
- Nếu chưa chắc logic cũ dùng ở đâu, giữ lại và ghi TODO.
```

### 3.3. Tất cả file tạo mới phải có nội dung tối thiểu

Không được tạo folder rỗng. Mỗi folder Use Case phải có file cụ thể.

Ví dụ không được chỉ tạo:

```text
Partners/
├── Commands/
│   └── CreatePartnerProfile/
```

Mà phải tạo:

```text
Partners/
├── Commands/
│   └── CreatePartnerProfile/
│       ├── CreatePartnerProfileCommand.cs
│       ├── CreatePartnerProfileCommandHandler.cs
│       ├── CreatePartnerProfileCommandValidator.cs
│       └── CreatePartnerProfileResponse.cs
```

---

## 4. Quy trình làm việc bắt buộc

Hãy thực hiện theo từng phase. Sau mỗi phase, báo cáo rõ:

```text
- Đã tạo/thay đổi file nào.
- Có file nào chưa xử lý được không.
- Có lỗi build/lint không.
- Có phần nào cần người dùng xác nhận không.
```

---

# PHASE 1: BACKUP, KIỂM TRA VÀ LẬP BẢN ĐỒ DỰ ÁN

## 1.1. Tạo branch hoặc backup

Trước khi sửa, tạo branch mới:

```bash
git checkout -b refactor/clean-architecture-structure
```

Nếu không dùng git, tạo folder backup:

```text
_backup_before_refactor/
```

## 1.2. Quét toàn bộ project

Hãy quét và ghi nhận:

```text
- Tất cả .csproj hiện có.
- Tất cả controller hiện có.
- Tất cả service hiện có.
- Tất cả entity hiện có.
- Tất cả page React hiện có.
- Tất cả component React hiện có.
- Tất cả file script .cjs/.js ở frontend.
- Tất cả file docs và SQL.
```

Tạo file:

```text
docs/architecture/CURRENT_PROJECT_INVENTORY.md
```

Nội dung file phải có bảng:

```markdown
| Area | Current Path | File/Folder | Purpose | Keep/Move/Refactor | Note |
|---|---|---|---|---|---|
```

---

# PHASE 2: CHUYỂN FRONTEND RA NGOÀI NHƯNG GIỮ NGUYÊN UI

## 2.1. Di chuyển frontend root

Di chuyển nguyên thư mục frontend thật:

```text
Pems_React/fpt-education---htqt_ver10 (2)/fpt-education---htqt_ver10/
```

thành:

```text
frontend/pems-react/
```

Phải đảm bảo các file sau vẫn nằm trong `frontend/pems-react/`:

```text
package.json
package-lock.json
vite.config.ts
tsconfig.json
index.html
.env.example
src/
```

Không chỉ chuyển riêng `src/`.

## 2.2. Không xóa frontend cũ ngay

Nếu chưa chắc chạy ổn, hãy copy thay vì move. Sau khi test xong mới xóa folder cũ.

## 2.3. Kiểm tra frontend chạy

Chạy:

```bash
cd frontend/pems-react
npm install
npm run dev
```

Nếu lỗi import ảnh, alias hoặc env, hãy sửa nhẹ nhưng không đổi UI.

## 2.4. Thêm cấu trúc frontend mới

Trong `frontend/pems-react/src/`, tạo thêm:

```text
src/
├── shared/
│   ├── api/
│   │   ├── httpClient.ts
│   │   ├── endpoints.ts
│   │   ├── authInterceptor.ts
│   │   └── errorHandler.ts
│   │
│   ├── auth/
│   │   ├── authStorage.ts
│   │   ├── permissionChecker.ts
│   │   ├── ProtectedRoute.tsx
│   │   └── RoleGuard.tsx
│   │
│   ├── constants/
│   │   ├── roles.ts
│   │   ├── permissions.ts
│   │   ├── ucCodes.ts
│   │   ├── appRoutes.ts
│   │   └── statusCodes.ts
│   │
│   ├── hooks/
│   │   ├── useAuth.ts
│   │   ├── usePermission.ts
│   │   ├── usePagination.ts
│   │   ├── useDebounce.ts
│   │   └── useApiError.ts
│   │
│   ├── types/
│   │   ├── api.types.ts
│   │   ├── auth.types.ts
│   │   ├── permission.types.ts
│   │   ├── pagination.types.ts
│   │   └── common.types.ts
│   │
│   └── utils/
│       ├── dateUtils.ts
│       ├── fileUtils.ts
│       ├── formatUtils.ts
│       ├── validationUtils.ts
│       └── routeUtils.ts
```

## 2.5. Nội dung tối thiểu cho file frontend shared

### `src/shared/api/httpClient.ts`

Tạo file có nhiệm vụ:

```text
- Đọc base URL từ import.meta.env.VITE_API_BASE_URL.
- Tự gắn Authorization Bearer token nếu có.
- Chuẩn hóa GET/POST/PUT/PATCH/DELETE.
- Tự xử lý response lỗi qua errorHandler.
```

### `src/shared/api/endpoints.ts`

Khai báo endpoint tập trung:

```ts
export const API_ENDPOINTS = {
  auth: {
    login: '/auth/login',
    sso: '/auth/sso',
    logout: '/auth/logout',
    forgotPassword: '/auth/forgot-password',
  },
  partners: {
    list: '/partners',
    detail: (id: string | number) => `/partners/${id}`,
    create: '/partners',
    update: (id: string | number) => `/partners/${id}`,
    search: '/partners/search',
  },
  delegations: {
    list: '/delegations',
    detail: (id: string | number) => `/delegations/${id}`,
    submitVisitRequest: '/visit-requests',
    processVisitRequest: (id: string | number) => `/visit-requests/${id}/process`,
  },
};
```

### `src/shared/auth/permissionChecker.ts`

Tạo hàm kiểm tra quyền theo Permission Matrix:

```ts
export type PermissionCode = 'F' | 'E' | 'R' | 'O' | '—';

export function hasPermission(userPermission: PermissionCode, required: PermissionCode): boolean {
  const rank: Record<PermissionCode, number> = {
    '—': 0,
    R: 1,
    O: 2,
    E: 3,
    F: 4,
  };

  return rank[userPermission] >= rank[required];
}
```

---

# PHASE 3: THÊM FRONTEND FEATURE API LAYER NHƯNG KHÔNG CHUYỂN PAGE

Trong `frontend/pems-react/src/features/`, tạo các module sau:

```text
features/
├── public-content/
├── authentication/
├── profile/
├── notifications/
├── delegations/
├── partners/
├── documents/
├── meeting-minutes/
├── emails/
├── gallery-management/
├── faq-management/
├── reports/
├── calendars/
├── feedbacks/
├── campus-management/
├── news-management/
├── account-management/
├── department-management/
├── role-permission-management/
├── api-management/
└── agenda-templates/
```

Mỗi module frontend phải có cấu trúc:

```text
[module]/
├── api/
│   └── [module]Api.ts
├── types/
│   └── [module].types.ts
├── adapters/
│   └── [module]Adapter.ts
└── hooks/
    └── use[Module].ts
```

Ví dụ `features/partners/`:

```text
features/partners/
├── api/
│   └── partnerApi.ts
├── types/
│   └── partner.types.ts
├── adapters/
│   └── partnerAdapter.ts
└── hooks/
    └── usePartners.ts
```

Quan trọng:

```text
- Không chuyển `src/pages/dashboard/partners/*.tsx` vào features ngay.
- Page hiện tại chỉ import hàm từ `partnerApi.ts` khi cần.
- Nếu page đang dùng mockData thì giữ lại, nhưng đánh dấu TODO thay bằng API thật.
```

---

# PHASE 4: TẠO BACKEND CLEAN ARCHITECTURE CHUẨN

## 4.1. Tạo cấu trúc backend mới

Tạo:

```text
backend/
├── PEMS.Api/
├── PEMS.Application/
├── PEMS.Domain/
├── PEMS.Infrastructure/
└── PEMS.SharedKernel/
```

Nếu các project cũ đang là:

```text
Pems_WebAPI/
Application/
Domain/
Infrastructure/
```

thì hãy chuyển hoặc copy sang tên mới:

```text
Pems_WebAPI      -> backend/PEMS.Api
Application      -> backend/PEMS.Application
Domain           -> backend/PEMS.Domain
Infrastructure   -> backend/PEMS.Infrastructure
```

Cập nhật `.sln` và project references.

## 4.2. Dependency Rule bắt buộc

```text
PEMS.Api -> PEMS.Application -> PEMS.Domain
PEMS.Infrastructure -> PEMS.Application -> PEMS.Domain
PEMS.SharedKernel có thể được dùng bởi các project khác nếu cần
```

Không để:

```text
Domain phụ thuộc Api
Domain phụ thuộc Infrastructure
Application phụ thuộc Api
Application phụ thuộc Infrastructure implementation cụ thể
Controller gọi trực tiếp DbContext nếu không thật sự cần
```

---

# PHASE 5: TẠO FILE CHUNG CHO BACKEND

## 5.1. PEMS.Domain

Tạo cấu trúc:

```text
PEMS.Domain/
├── Common/
│   ├── BaseEntity.cs
│   ├── AuditableEntity.cs
│   ├── SoftDeleteEntity.cs
│   └── DomainEvent.cs
│
├── Enums/
│   ├── PermissionCode.cs
│   ├── UserRoleCode.cs
│   ├── AccountStatus.cs
│   ├── CampusStatus.cs
│   ├── DepartmentStatus.cs
│   ├── VisitRequestStatus.cs
│   ├── DelegationStatus.cs
│   ├── NewsStatus.cs
│   ├── FaqVisibilityStatus.cs
│   └── ApiIntegrationStatus.cs
│
├── Entities/
│   ├── Users/
│   ├── Campuses/
│   ├── Departments/
│   ├── Delegations/
│   ├── Partners/
│   ├── Documents/
│   ├── Minutes/
│   ├── Feedbacks/
│   ├── News/
│   ├── Emails/
│   ├── Faqs/
│   ├── Galleries/
│   ├── Reports/
│   ├── ApiIntegrations/
│   └── AgendaTemplates/
│
├── ValueObjects/
│   ├── EmailAddress.cs
│   ├── PhoneNumber.cs
│   ├── DateRange.cs
│   ├── Address.cs
│   └── FileMetadata.cs
│
└── Events/
    ├── VisitRequestSubmittedEvent.cs
    ├── VisitRequestApprovedEvent.cs
    ├── DelegationClosedEvent.cs
    ├── AccountCreatedEvent.cs
    ├── NewsApprovedEvent.cs
    └── ResourceRequestApprovedEvent.cs
```

## 5.2. PEMS.Application Common

Tạo:

```text
PEMS.Application/
├── Common/
│   ├── Interfaces/
│   │   ├── IApplicationDbContext.cs
│   │   ├── ICurrentUserService.cs
│   │   ├── IDateTimeService.cs
│   │   ├── IPermissionChecker.cs
│   │   ├── IOwnershipChecker.cs
│   │   ├── IAuditLogService.cs
│   │   ├── IEmailService.cs
│   │   ├── IFileStorageService.cs
│   │   ├── IFileValidationService.cs
│   │   ├── IOcrService.cs
│   │   ├── IFaceRecognitionService.cs
│   │   ├── IRateLimitService.cs
│   │   ├── IIdempotencyService.cs
│   │   ├── INotificationService.cs
│   │   └── IExternalApiClient.cs
│   │
│   ├── Models/
│   │   ├── Result.cs
│   │   ├── ResultOfT.cs
│   │   ├── PagedResult.cs
│   │   ├── PaginationRequest.cs
│   │   ├── ErrorResponse.cs
│   │   └── FileUploadResult.cs
│   │
│   ├── Behaviours/
│   │   ├── ValidationBehaviour.cs
│   │   ├── AuthorizationBehaviour.cs
│   │   ├── IdempotencyBehaviour.cs
│   │   ├── TransactionBehaviour.cs
│   │   ├── AuditLogBehaviour.cs
│   │   └── LoggingBehaviour.cs
│   │
│   ├── Exceptions/
│   │   ├── NotFoundException.cs
│   │   ├── ForbiddenException.cs
│   │   ├── ValidationException.cs
│   │   ├── BusinessRuleException.cs
│   │   └── ConflictException.cs
│   │
│   └── Security/
│       ├── PermissionRequirement.cs
│       ├── UseCasePermissionAttribute.cs
│       └── PermissionConstants.cs
```

## 5.3. PEMS.Infrastructure

Tạo:

```text
PEMS.Infrastructure/
├── Persistence/
│   ├── ApplicationDbContext.cs
│   ├── ApplicationDbContextFactory.cs
│   ├── Configurations/
│   ├── Migrations/
│   ├── Seed/
│   │   ├── RoleSeed.cs
│   │   ├── PermissionSeed.cs
│   │   ├── PermissionMatrixSeed.cs
│   │   ├── CampusSeed.cs
│   │   └── AdminAccountSeed.cs
│   └── Repositories/
│       ├── GenericRepository.cs
│       ├── UserRepository.cs
│       ├── DelegationRepository.cs
│       ├── PartnerRepository.cs
│       └── ReportRepository.cs
│
├── Identity/
│   ├── JwtTokenService.cs
│   ├── PasswordHasher.cs
│   ├── CurrentUserService.cs
│   ├── PermissionChecker.cs
│   ├── OwnershipChecker.cs
│   └── RefreshTokenStore.cs
│
├── Email/
│   ├── EmailService.cs
│   ├── EmailTemplateRenderer.cs
│   └── SmtpEmailSender.cs
│
├── FileStorage/
│   ├── LocalFileStorageService.cs
│   ├── CloudFileStorageService.cs
│   ├── FileValidationService.cs
│   └── VirusScanService.cs
│
├── RateLimiting/
│   ├── InMemoryRateLimitStore.cs
│   └── RedisRateLimitStore.cs
│
├── Idempotency/
│   └── IdempotencyService.cs
│
├── ExternalServices/
│   ├── Ocr/
│   │   └── OcrService.cs
│   ├── FaceRecognition/
│   │   └── FaceRecognitionService.cs
│   ├── Calendar/
│   │   └── CalendarIntegrationService.cs
│   └── ApiClient/
│       └── ExternalApiClient.cs
│
├── Logging/
│   ├── AuditLogService.cs
│   └── ApiRequestLogService.cs
│
└── DependencyInjection.cs
```

## 5.4. PEMS.Api

Tạo:

```text
PEMS.Api/
├── Controllers/
│   ├── Public/
│   ├── Auth/
│   ├── Profiles/
│   ├── Notifications/
│   ├── Delegations/
│   ├── Emails/
│   ├── Partners/
│   ├── Documents/
│   ├── Minutes/
│   ├── Reports/
│   ├── Calendars/
│   ├── Feedbacks/
│   ├── Campuses/
│   ├── News/
│   ├── Accounts/
│   ├── Departments/
│   ├── Roles/
│   ├── ApiIntegrations/
│   └── AgendaTemplates/
│
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   ├── CurrentUserMiddleware.cs
│   ├── RateLimitMiddleware.cs
│   └── SecurityHeadersMiddleware.cs
│
├── Filters/
│   ├── PermissionAuthorizeAttribute.cs
│   ├── ValidationFilter.cs
│   ├── IdempotencyFilter.cs
│   └── FileUploadValidationFilter.cs
│
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   ├── AuthenticationExtensions.cs
│   ├── AuthorizationExtensions.cs
│   ├── CorsExtensions.cs
│   ├── RateLimitingExtensions.cs
│   └── SwaggerExtensions.cs
│
├── Contracts/
│   ├── ApiRoutes.cs
│   └── ApiResponse.cs
│
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
└── PEMS.Api.csproj
```

---

# PHASE 6: TẠO APPLICATION MODULE THEO 135 USE CASE

Mỗi Use Case phải có file thật bên trong. Không tạo folder rỗng.

## Quy tắc file cho Command UC

Mỗi UC dạng Command phải có:

```text
[UseCaseName]/
├── [UseCaseName]Command.cs
├── [UseCaseName]CommandHandler.cs
├── [UseCaseName]CommandValidator.cs
└── [UseCaseName]Response.cs
```

## Quy tắc file cho Query UC

Mỗi UC dạng Query phải có:

```text
[UseCaseName]/
├── [UseCaseName]Query.cs
├── [UseCaseName]QueryHandler.cs
└── [UseCaseName]Dto.cs
```

Nếu query trả về danh sách, thêm:

```text
[UseCaseName]ListItemDto.cs
```

## 6.1. Public Content Module

Tạo:

```text
PEMS.Application/PublicContent/
├── Queries/
│   ├── ViewHomepage/
│   │   ├── ViewHomepageQuery.cs
│   │   ├── ViewHomepageQueryHandler.cs
│   │   └── HomepageDto.cs
│   ├── SearchInformation/
│   │   ├── SearchInformationQuery.cs
│   │   ├── SearchInformationQueryHandler.cs
│   │   ├── SearchInformationDto.cs
│   │   └── SearchInformationListItemDto.cs
│   ├── ViewContactInfo/
│   │   ├── ViewContactInfoQuery.cs
│   │   ├── ViewContactInfoQueryHandler.cs
│   │   └── ContactInfoDto.cs
│   ├── ViewPolicyTerms/
│   │   ├── ViewPolicyTermsQuery.cs
│   │   ├── ViewPolicyTermsQueryHandler.cs
│   │   └── PolicyTermsDto.cs
│   ├── ViewFaq/
│   │   ├── ViewFaqQuery.cs
│   │   ├── ViewFaqQueryHandler.cs
│   │   └── PublicFaqDto.cs
│   ├── ViewNews/
│   │   ├── ViewNewsQuery.cs
│   │   ├── ViewNewsQueryHandler.cs
│   │   ├── PublicNewsDto.cs
│   │   └── PublicNewsListItemDto.cs
│   ├── ViewPartners/
│   │   ├── ViewPartnersQuery.cs
│   │   ├── ViewPartnersQueryHandler.cs
│   │   └── PublicPartnerListItemDto.cs
│   └── ViewGallery/
│       ├── ViewGalleryQuery.cs
│       ├── ViewGalleryQueryHandler.cs
│       └── PublicGalleryItemDto.cs
├── Dtos/
└── Mappings/
    └── PublicContentMappingProfile.cs
```

## 6.2. Authentication Module

```text
PEMS.Application/Authentication/
├── Commands/
│   ├── LoginViaSso/
│   │   ├── LoginViaSsoCommand.cs
│   │   ├── LoginViaSsoCommandHandler.cs
│   │   ├── LoginViaSsoCommandValidator.cs
│   │   └── LoginViaSsoResponse.cs
│   ├── LoginViaCredentials/
│   │   ├── LoginViaCredentialsCommand.cs
│   │   ├── LoginViaCredentialsCommandHandler.cs
│   │   ├── LoginViaCredentialsCommandValidator.cs
│   │   └── LoginViaCredentialsResponse.cs
│   ├── Logout/
│   │   ├── LogoutCommand.cs
│   │   ├── LogoutCommandHandler.cs
│   │   ├── LogoutCommandValidator.cs
│   │   └── LogoutResponse.cs
│   └── ForgotPassword/
│       ├── ForgotPasswordCommand.cs
│       ├── ForgotPasswordCommandHandler.cs
│       ├── ForgotPasswordCommandValidator.cs
│       └── ForgotPasswordResponse.cs
├── Dtos/
│   ├── AuthTokenDto.cs
│   ├── CurrentUserDto.cs
│   └── LoginResultDto.cs
├── Rules/
│   ├── LoginAttemptRules.cs
│   └── PasswordPolicyRules.cs
└── Mappings/
    └── AuthenticationMappingProfile.cs
```

## 6.3. Profile Module

```text
PEMS.Application/Profiles/
├── Queries/
│   └── ViewProfile/
│       ├── ViewProfileQuery.cs
│       ├── ViewProfileQueryHandler.cs
│       └── ProfileDto.cs
├── Commands/
│   ├── UpdateProfile/
│   │   ├── UpdateProfileCommand.cs
│   │   ├── UpdateProfileCommandHandler.cs
│   │   ├── UpdateProfileCommandValidator.cs
│   │   └── UpdateProfileResponse.cs
│   └── ChangePassword/
│       ├── ChangePasswordCommand.cs
│       ├── ChangePasswordCommandHandler.cs
│       ├── ChangePasswordCommandValidator.cs
│       └── ChangePasswordResponse.cs
├── Dtos/
└── Mappings/
    └── ProfileMappingProfile.cs
```

## 6.4. Delegations Module

```text
PEMS.Application/Delegations/
├── Commands/
│   ├── SubmitVisitRequest/
│   │   ├── SubmitVisitRequestCommand.cs
│   │   ├── SubmitVisitRequestCommandHandler.cs
│   │   ├── SubmitVisitRequestCommandValidator.cs
│   │   └── SubmitVisitRequestResponse.cs
│   ├── ApproveCrossCampusRequest/
│   │   ├── ApproveCrossCampusRequestCommand.cs
│   │   ├── ApproveCrossCampusRequestCommandHandler.cs
│   │   ├── ApproveCrossCampusRequestCommandValidator.cs
│   │   └── ApproveCrossCampusRequestResponse.cs
│   ├── ProcessVisitRequest/
│   │   ├── ProcessVisitRequestCommand.cs
│   │   ├── ProcessVisitRequestCommandHandler.cs
│   │   ├── ProcessVisitRequestCommandValidator.cs
│   │   └── ProcessVisitRequestResponse.cs
│   ├── CreateGuestDelegation/
│   │   ├── CreateGuestDelegationCommand.cs
│   │   ├── CreateGuestDelegationCommandHandler.cs
│   │   ├── CreateGuestDelegationCommandValidator.cs
│   │   └── CreateGuestDelegationResponse.cs
│   ├── UpdateGuestDelegation/
│   │   ├── UpdateGuestDelegationCommand.cs
│   │   ├── UpdateGuestDelegationCommandHandler.cs
│   │   ├── UpdateGuestDelegationCommandValidator.cs
│   │   └── UpdateGuestDelegationResponse.cs
│   ├── PrepareVisitLogistics/
│   │   ├── PrepareVisitLogisticsCommand.cs
│   │   ├── PrepareVisitLogisticsCommandHandler.cs
│   │   ├── PrepareVisitLogisticsCommandValidator.cs
│   │   └── PrepareVisitLogisticsResponse.cs
│   ├── UpdateVisitLogistics/
│   │   ├── UpdateVisitLogisticsCommand.cs
│   │   ├── UpdateVisitLogisticsCommandHandler.cs
│   │   ├── UpdateVisitLogisticsCommandValidator.cs
│   │   └── UpdateVisitLogisticsResponse.cs
│   ├── ConfirmParticipation/
│   │   ├── ConfirmParticipationCommand.cs
│   │   ├── ConfirmParticipationCommandHandler.cs
│   │   ├── ConfirmParticipationCommandValidator.cs
│   │   └── ConfirmParticipationResponse.cs
│   ├── ApproveResourceRequest/
│   │   ├── ApproveResourceRequestCommand.cs
│   │   ├── ApproveResourceRequestCommandHandler.cs
│   │   ├── ApproveResourceRequestCommandValidator.cs
│   │   └── ApproveResourceRequestResponse.cs
│   ├── ProposeResourceModification/
│   │   ├── ProposeResourceModificationCommand.cs
│   │   ├── ProposeResourceModificationCommandHandler.cs
│   │   ├── ProposeResourceModificationCommandValidator.cs
│   │   └── ProposeResourceModificationResponse.cs
│   ├── ConfirmChangeProposal/
│   │   ├── ConfirmChangeProposalCommand.cs
│   │   ├── ConfirmChangeProposalCommandHandler.cs
│   │   ├── ConfirmChangeProposalCommandValidator.cs
│   │   └── ConfirmChangeProposalResponse.cs
│   ├── UploadVisitPhotos/
│   │   ├── UploadVisitPhotosCommand.cs
│   │   ├── UploadVisitPhotosCommandHandler.cs
│   │   ├── UploadVisitPhotosCommandValidator.cs
│   │   └── UploadVisitPhotosResponse.cs
│   ├── TagFacesOnPhotos/
│   │   ├── TagFacesOnPhotosCommand.cs
│   │   ├── TagFacesOnPhotosCommandHandler.cs
│   │   ├── TagFacesOnPhotosCommandValidator.cs
│   │   └── TagFacesOnPhotosResponse.cs
│   ├── CreateNewsArticle/
│   │   ├── CreateNewsArticleCommand.cs
│   │   ├── CreateNewsArticleCommandHandler.cs
│   │   ├── CreateNewsArticleCommandValidator.cs
│   │   └── CreateNewsArticleResponse.cs
│   └── CloseDelegation/
│       ├── CloseDelegationCommand.cs
│       ├── CloseDelegationCommandHandler.cs
│       ├── CloseDelegationCommandValidator.cs
│       └── CloseDelegationResponse.cs
│
├── Queries/
│   ├── ViewGuestDelegationDetails/
│   │   ├── ViewGuestDelegationDetailsQuery.cs
│   │   ├── ViewGuestDelegationDetailsQueryHandler.cs
│   │   └── GuestDelegationDetailDto.cs
│   ├── ViewGuestDelegationList/
│   │   ├── ViewGuestDelegationListQuery.cs
│   │   ├── ViewGuestDelegationListQueryHandler.cs
│   │   └── GuestDelegationListItemDto.cs
│   └── SearchDelegations/
│       ├── SearchDelegationsQuery.cs
│       ├── SearchDelegationsQueryHandler.cs
│       └── DelegationSearchResultDto.cs
│
├── Dtos/
│   ├── DelegationDto.cs
│   ├── DelegationLogisticsDto.cs
│   ├── VisitRequestDto.cs
│   ├── VisitParticipantDto.cs
│   └── ResourceRequestDto.cs
│
├── Rules/
│   ├── DelegationStatusRules.cs
│   ├── VisitRequestApprovalRules.cs
│   ├── LogisticsRules.cs
│   ├── DelegationPermissionRules.cs
│   └── DelegationDuplicateRules.cs
│
└── Mappings/
    └── DelegationMappingProfile.cs
```

## 6.5. Partners Module

```text
PEMS.Application/Partners/
├── Commands/
│   ├── ScanBusinessCard/
│   │   ├── ScanBusinessCardCommand.cs
│   │   ├── ScanBusinessCardCommandHandler.cs
│   │   ├── ScanBusinessCardCommandValidator.cs
│   │   └── ScanBusinessCardResponse.cs
│   ├── CreatePartnerProfile/
│   │   ├── CreatePartnerProfileCommand.cs
│   │   ├── CreatePartnerProfileCommandHandler.cs
│   │   ├── CreatePartnerProfileCommandValidator.cs
│   │   └── CreatePartnerProfileResponse.cs
│   ├── ProcessPartnerCreationRequest/
│   │   ├── ProcessPartnerCreationRequestCommand.cs
│   │   ├── ProcessPartnerCreationRequestCommandHandler.cs
│   │   ├── ProcessPartnerCreationRequestCommandValidator.cs
│   │   └── ProcessPartnerCreationRequestResponse.cs
│   └── EditPartnerInformation/
│       ├── EditPartnerInformationCommand.cs
│       ├── EditPartnerInformationCommandHandler.cs
│       ├── EditPartnerInformationCommandValidator.cs
│       └── EditPartnerInformationResponse.cs
│
├── Queries/
│   ├── ViewPartnerLists/
│   │   ├── ViewPartnerListsQuery.cs
│   │   ├── ViewPartnerListsQueryHandler.cs
│   │   └── PartnerListItemDto.cs
│   ├── SearchPartners/
│   │   ├── SearchPartnersQuery.cs
│   │   ├── SearchPartnersQueryHandler.cs
│   │   └── PartnerSearchResultDto.cs
│   └── ViewPartnerDetails/
│       ├── ViewPartnerDetailsQuery.cs
│       ├── ViewPartnerDetailsQueryHandler.cs
│       └── PartnerDetailDto.cs
│
├── Dtos/
│   ├── PartnerDto.cs
│   ├── PartnerContactDto.cs
│   ├── PartnerDocumentDto.cs
│   ├── PartnerHistoryDto.cs
│   └── PartnerCreationRequestDto.cs
│
├── Rules/
│   ├── PartnerBusinessRules.cs
│   ├── PartnerPermissionRules.cs
│   ├── PartnerStatusRules.cs
│   └── PartnerDuplicateRules.cs
│
└── Mappings/
    └── PartnerMappingProfile.cs
```

## 6.6. Các module còn lại

Với các module còn lại, hãy tạo theo cùng quy tắc:

```text
MeetingMinutes/
Documents/
Feedbacks/
EmailManagement/
GalleryManagement/
FaqManagement/
Reports/
Calendars/
CampusManagement/
NewsManagement/
AccountManagement/
DepartmentManagement/
RolePermissionManagement/
ApiManagement/
AgendaTemplates/
Notifications/
```

Bắt buộc mỗi UC phải có folder và file tương ứng:

```text
Commands/[UseCaseName]/[UseCaseName]Command.cs
Commands/[UseCaseName]/[UseCaseName]CommandHandler.cs
Commands/[UseCaseName]/[UseCaseName]CommandValidator.cs
Commands/[UseCaseName]/[UseCaseName]Response.cs
```

hoặc:

```text
Queries/[UseCaseName]/[UseCaseName]Query.cs
Queries/[UseCaseName]/[UseCaseName]QueryHandler.cs
Queries/[UseCaseName]/[UseCaseName]Dto.cs
```

---

# PHASE 7: TẠO SECURITY, VALIDATION, RATE LIMIT, AUDIT, IDEMPOTENCY

## 7.1. Validation

Tạo validation theo từng command:

```text
- Required field.
- Max length.
- Email format.
- Phone format.
- Date range.
- File type.
- File size.
- Status transition.
- Duplicate check.
```

Các file quan trọng:

```text
ValidationBehaviour.cs
ValidationException.cs
[UseCase]CommandValidator.cs
```

## 7.2. Rate Limiting / Chống spam request

Tạo:

```text
RateLimitMiddleware.cs
RateLimitingExtensions.cs
IRateLimitService.cs
InMemoryRateLimitStore.cs
RedisRateLimitStore.cs
```

Áp dụng rule đề xuất:

```text
Login: 5 requests/minute/IP or email
Forgot Password: 3 requests/10 minutes/email
Submit Visit Request: 5 requests/hour/user or IP
Search: 30 requests/minute/IP
Send Email: 10 requests/minute/user
Upload File: 20 files/10 minutes/user
Export Report: 5 requests/10 minutes/user
Test API Connection: 5 requests/minute/Admin
```

## 7.3. Idempotency chống bấm gửi nhiều lần

Tạo:

```text
IdempotencyBehaviour.cs
IIdempotencyService.cs
IdempotencyService.cs
IdempotencyFilter.cs
```

Áp dụng cho:

```text
SubmitVisitRequest
CreateGuestDelegation
CreateMeetingMinutes
UploadAttachedDocuments
CreateNewsArticle
SendEmail
CreateAccount
CreateAgendaTemplate
```

## 7.4. Audit Log

Tạo:

```text
AuditLogBehaviour.cs
IAuditLogService.cs
AuditLogService.cs
```

Audit log phải lưu:

```text
- UserId
- Role
- UC ID
- Action
- EntityName
- EntityId
- OldValue
- NewValue
- IpAddress
- UserAgent
- CreatedAt
- Success/Failed
```

## 7.5. Permission + Ownership

Tạo:

```text
PermissionChecker.cs
OwnershipChecker.cs
PermissionAuthorizeAttribute.cs
UseCasePermissionAttribute.cs
PermissionConstants.cs
```

Quy tắc:

```text
F = Full
E = Edit
R = Read
O = Own
— = No access
```

Nếu permission là `O`, phải kiểm tra object-level ownership:

```text
- Visitor chỉ xem request của mình.
- Staff chỉ sửa delegation thuộc campus mình.
- Department chỉ xem task thuộc department mình.
- Student chỉ thao tác event được assign.
```

---

# PHASE 8: DATABASE VÀ DOCS

## 8.1. Database folder

Tạo:

```text
database/
├── scripts/
│   └── pems_full.sql
├── migrations/
│   └── README.md
├── seed/
│   ├── roles.sql
│   ├── permissions.sql
│   ├── permission_matrix.sql
│   └── campuses.sql
└── README.md
```

Nếu hiện tại có `docs/pems_full.sql`, hãy copy sang:

```text
database/scripts/pems_full.sql
```

Không xóa bản cũ nếu chưa chắc.

## 8.2. Docs folder

Tạo:

```text
docs/
├── use-cases/
│   ├── USE_CASE_LIST.md
│   └── USE_CASE_NOTES.md
├── permissions/
│   ├── PERMISSION_MATRIX.md
│   └── PERMISSION_RULES.md
├── architecture/
│   ├── CLEAN_ARCHITECTURE.md
│   ├── PROJECT_STRUCTURE.md
│   ├── CURRENT_PROJECT_INVENTORY.md
│   └── REFACTOR_CHANGELOG.md
├── api/
│   ├── API_SPECIFICATION.md
│   └── API_ROUTE_CONVENTION.md
└── database/
    ├── DATABASE_SCHEMA.md
    └── DATABASE_DEPLOYMENT.md
```

---

# PHASE 9: TEST PROJECTS

Tạo:

```text
tests/
├── PEMS.UnitTests/
│   ├── Domain/
│   ├── Application/
│   └── SharedKernel/
├── PEMS.ApplicationTests/
│   ├── Delegations/
│   ├── Partners/
│   ├── Accounts/
│   ├── Permissions/
│   └── Departments/
└── PEMS.IntegrationTests/
    ├── Api/
    ├── Database/
    └── Security/
```

Test quan trọng cần có skeleton:

```text
- PermissionCheckerTests.cs
- OwnershipCheckerTests.cs
- SubmitVisitRequestCommandHandlerTests.cs
- ProcessVisitRequestCommandHandlerTests.cs
- CreateAccountCommandHandlerTests.cs
- ConfigureRolePermissionsCommandHandlerTests.cs
- RateLimitMiddlewareTests.cs
- IdempotencyBehaviourTests.cs
- FileValidationServiceTests.cs
```

---

# PHASE 10: FRONTEND KẾT NỐI API DẦN DẦN

Không sửa toàn bộ frontend một lúc.

Thứ tự refactor từng module:

```text
1. Authentication + Profile
2. Visit / Delegation
3. Partners
4. Accounts + Permissions
5. Departments + Campus
6. News + FAQ + Gallery
7. Emails
8. Reports + API Management + Agenda Templates
```

Mỗi lần chỉ sửa một module:

```text
- Tạo api service.
- Tạo type.
- Tạo adapter nếu backend response khác UI.
- Thay fetch trực tiếp bằng api service.
- Test lại page.
```

Ví dụ:

```text
pages/dashboard/partners/PartnerManagement.tsx
```

không chuyển file, chỉ thay:

```ts
fetch('/api/partners')
```

thành:

```ts
partnerApi.getPartners(params)
```

---

# PHASE 11: ACCEPTANCE CRITERIA

Sau khi refactor, project phải đạt các điều kiện sau:

## 11.1. Frontend

```text
- Chạy được `npm install`.
- Chạy được `npm run dev`.
- App.tsx route không bị vỡ.
- Sidebar vẫn hiển thị đúng.
- Các page dashboard hiện có vẫn mở được.
- Public pages vẫn mở được: Home, News, Partners, Visit, FAQ.
- Assets không bị mất.
- Không lỗi import nghiêm trọng.
```

## 11.2. Backend

```text
- Solution build được.
- Các project reference đúng hướng Clean Architecture.
- API chạy được.
- Swagger chạy được nếu có.
- Program.cs đăng ký đúng Application/Infrastructure.
- Không controller nào gọi DbContext trực tiếp nếu không có lý do rõ.
```

## 11.3. Cấu trúc

```text
- Có folder backend/frontend/database/docs/tests/tools.
- Có PEMS.Api, PEMS.Application, PEMS.Domain, PEMS.Infrastructure, PEMS.SharedKernel.
- Application có module theo UC.
- Mỗi UC folder có file thật bên trong.
- Frontend có shared/api, shared/auth, features/*/api.
- Docs có changelog refactor.
```

## 11.4. Security

```text
- Có validation layer.
- Có permission checker.
- Có ownership checker.
- Có rate limiting skeleton.
- Có idempotency skeleton.
- Có audit log skeleton.
- Có global exception handling.
- Có request logging.
- Có file upload validation skeleton.
```

---

# PHASE 12: OUTPUT BẮT BUỘC SAU KHI LÀM

Sau khi hoàn thành, hãy tạo hoặc cập nhật các file sau:

```text
docs/architecture/REFACTOR_CHANGELOG.md
docs/architecture/PROJECT_STRUCTURE_AFTER_REFACTOR.md
docs/architecture/CLEAN_ARCHITECTURE.md
docs/api/API_ROUTE_CONVENTION.md
docs/permissions/PERMISSION_RULES.md
docs/database/DATABASE_DEPLOYMENT.md
```

Trong `REFACTOR_CHANGELOG.md`, ghi rõ:

```markdown
# Refactor Changelog

## Summary

## Files Moved

## Files Created

## Files Updated

## Files Not Touched

## Frontend Compatibility Notes

## Backend Build Notes

## Remaining TODOs

## Risks
```

---

# QUY TẮC CUỐI CÙNG

Hãy nhớ:

```text
Mục tiêu không phải làm lại project từ đầu.
Mục tiêu là chuẩn hóa cấu trúc mà vẫn giữ lại 80% frontend đã làm.
Backend có thể refactor mạnh theo Clean Architecture.
Frontend chỉ refactor nhẹ bằng cách thêm shared/api và features/api layer.
Không thay đổi UI/route/flow nếu không bắt buộc.
Mọi thay đổi phải có changelog.
Sau mỗi phase phải build/test.
```

Nếu gặp phần không chắc, hãy:

```text
- Không xóa.
- Không đoán bừa.
- Tạo TODO rõ ràng.
- Ghi vào REFACTOR_CHANGELOG.md.
- Hỏi lại người dùng nếu cần quyết định nghiệp vụ.
```

