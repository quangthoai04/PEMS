# PROJECT STRUCTURE FULL REPORT

> **Bản cập nhật:** 2026-06-17 — cây thư mục được dựng lại từ `git ls-files` + quét đĩa thực tế (loại trừ build/rác).
> **File chính (latest):** `docs/architecture/PROJECT_STRUCTURE_FULL.md` (chính là file này). `PROJECT_STRUCTURE_FULL_DETAILED.md` không còn tồn tại.
> **Phạm vi đã scan sâu tới từng file:** `backend/`, `frontend/`, `database/`, `docs/`, `.vscode/` (kèm `scripts/`, `tests/`, file root).

---

## 1. Tổng quan dự án

* **Project:** PEMS (Partnership / Education Management System).
* **Backend:** .NET 8, C# / .NET 8 compatible (`<TargetFramework>net8.0</TargetFramework>` trong mọi `.csproj`), ASP.NET Core, EF Core, MediatR (CQRS), FluentValidation, BCrypt, JWT.
* **Frontend:** React 19, TypeScript, Vite (`frontend/pems-react`).
* **Database:** MySQL 8.0 (`database/scripts/pems_full.sql` + seed).
* **Kiến trúc backend:** Clean Architecture (Domain → Application → Infrastructure → Api) + CQRS; guard bằng `tests/PEMS.ArchitectureTests`.
* **Trạng thái:** Backend đang ở giai đoạn scaffold/planning — **chưa khẳng định build pass**. Domain là nguồn tham chiếu chính; một số module Application/Infrastructure/Api có thể còn pending so với Domain mới (xem mục 7).

---

## 2. Cây thư mục đầy đủ

> Loại trừ: `bin/`, `obj/`, `node_modules/`, `dist/`, `build/`, `.git/`, `.vs/`, `.idea/`, `coverage/`, `logs/`, `temp/`, `backup/`, `.cache/`.
> Chỉ rút gọn ở **file media** của frontend (ảnh/font) — ghi rõ số lượng theo từng thư mục asset, không liệt kê từng ảnh (rule 10). Mọi file mã nguồn được liệt kê đầy đủ.

### 2.1 Root + cây tổng quát

```txt
PEMS/
├── .gitattributes
├── .gitignore
├── PEMS.slnx
├── README.md
├── .vscode/
│   ├── launch.json
│   └── tasks.json
├── backend/
│   ├── PEMS.Api/              → xem 2.2
│   ├── PEMS.Application/      → xem 2.3
│   ├── PEMS.Domain/           → xem 2.4
│   └── PEMS.Infrastructure/   → xem 2.5
├── database/                  → xem 2.6
├── docs/                      → xem 2.7
├── frontend/                  → xem 2.8
├── scripts/
│   └── guard-project-structure.ps1
└── tests/
    ├── PEMS.ApplicationTests/
    │   ├── Accounts/
    │   │   ├── CreateAccountCommandHandlerTests.cs
    │   │   ├── CreateAccountCommandTests.cs
    │   │   ├── ManageAccountStatusCommandTests.cs
    │   │   ├── SearchandFilterAccountsQueryTests.cs
    │   │   ├── UpdateAccountRoleCommandTests.cs
    │   │   ├── ViewAccountDetailsQueryTests.cs
    │   │   └── ViewAccountListQueryTests.cs
    │   ├── AgendaTemplates/
    │   │   ├── CreateAgendaTemplateCommandTests.cs
    │   │   ├── DeleteAgendaTemplateCommandTests.cs
    │   │   ├── UpdateAgendaTemplateCommandTests.cs
    │   │   ├── ViewAgendaTemplateDetailQueryTests.cs
    │   │   └── ViewAgendaTemplateListQueryTests.cs
    │   ├── ApiIntegrations/
    │   │   ├── ConfigureRequestLimitCommandTests.cs
    │   │   ├── CreateAPIConfigurationCommandTests.cs
    │   │   ├── DeleteAPIConfigurationCommandTests.cs
    │   │   ├── ManageAPIStatusCommandTests.cs
    │   │   ├── SearchAPILogsQueryTests.cs
    │   │   ├── TestAPIConnectionCommandTests.cs
    │   │   ├── UpdateAPIConfigurationCommandTests.cs
    │   │   ├── ViewAPIConfigurationQueryTests.cs
    │   │   └── ViewAPILogsQueryTests.cs
    │   ├── Authentication/
    │   │   ├── ForgotPasswordCommandTests.cs
    │   │   ├── LoginviaCredentialsCommandTests.cs
    │   │   ├── LoginviaSSOCommandTests.cs
    │   │   └── LogoutCommandTests.cs
    │   ├── Calendars/
    │   │   ├── AddPersonalEventCommandTests.cs
    │   │   ├── DeletePersonalEventCommandTests.cs
    │   │   ├── SwitchViewModeCommandTests.cs
    │   │   ├── UpdatePersonalEventCommandTests.cs
    │   │   ├── ViewDepartmentCalendarQueryTests.cs
    │   │   ├── ViewEventDetailsQueryTests.cs
    │   │   └── ViewMyEventsQueryTests.cs
    │   ├── Campuses/
    │   │   ├── AddNewCampusCommandTests.cs
    │   │   ├── AssignCampusLeadCommandTests.cs
    │   │   ├── ManageCampusStatusCommandTests.cs
    │   │   ├── SearchandFilterCampusQueryTests.cs
    │   │   ├── UpdateCampusCommandTests.cs
    │   │   ├── ViewCampusDetailsQueryTests.cs
    │   │   └── ViewCampusListQueryTests.cs
    │   ├── Delegations/
    │   │   ├── ApproveCrossCampusRequestCommandTests.cs
    │   │   ├── ApproveResourceRequestCommandTests.cs
    │   │   ├── CloseDelegationCommandTests.cs
    │   │   ├── ConfirmParticipationCommandTests.cs
    │   │   ├── ConfirmTheChangeProposalCommandTests.cs
    │   │   ├── CreateGuestDelegationCommandTests.cs
    │   │   ├── CreateMeetingMinutesCommandTests.cs
    │   │   ├── CreateNewsArticleCommandTests.cs
    │   │   ├── CreatePartnerProfileCommandTests.cs
    │   │   ├── EditMeetingMinutesCommandTests.cs
    │   │   ├── PrepareVisitLogisticsCommandTests.cs
    │   │   ├── ProcessVisitRequestCommandHandlerTests.cs
    │   │   ├── ProcessVisitRequestCommandTests.cs
    │   │   ├── ProposeResourceModificationCommandTests.cs
    │   │   ├── ScanBusinessCardCommandTests.cs
    │   │   ├── SearchDelegationsQueryTests.cs
    │   │   ├── SubmitDelegationFeedbackCommandTests.cs
    │   │   ├── SubmitVisitRequestCommandHandlerTests.cs
    │   │   ├── SubmitVisitRequestCommandTests.cs
    │   │   ├── TagFacesonPhotosCommandTests.cs
    │   │   ├── UpdateGuestDelegationCommandTests.cs
    │   │   ├── UpdateVisitLogisticsCommandTests.cs
    │   │   ├── UploadAttachedDocumentsCommandTests.cs
    │   │   ├── UploadVisitPhotosCommandTests.cs
    │   │   ├── ViewGuestDelegationDetailsQueryTests.cs
    │   │   ├── ViewGuestDelegationListQueryTests.cs
    │   │   └── ViewMeetingMinutesDetailsQueryTests.cs
    │   ├── Departments/
    │   │   ├── AddDepartmentPersonnelCommandTests.cs
    │   │   ├── AddNewDepartmentCommandTests.cs
    │   │   ├── AssignTasksCommandTests.cs
    │   │   ├── DepartmentTests.cs
    │   │   ├── ManageDepartmentStatusCommandTests.cs
    │   │   ├── ReassignDepartmentLeadCommandTests.cs
    │   │   ├── RemovePersonnelCommandTests.cs
    │   │   ├── ReviewAssignedTasksCommandTests.cs
    │   │   ├── SearchCoordinationTasksQueryTests.cs
    │   │   ├── SearchPersonnelQueryTests.cs
    │   │   ├── SearchandFilterDepartmentsQueryTests.cs
    │   │   ├── SignTheServiceDeliveryReportCommandTests.cs
    │   │   ├── UpdateDepartmentCommandTests.cs
    │   │   ├── ViewCoordinationTasksQueryTests.cs
    │   │   ├── ViewDepartmentDetailsQueryTests.cs
    │   │   ├── ViewDepartmentListQueryTests.cs
    │   │   └── ViewPersonnelDetailsQueryTests.cs
    │   ├── Documents/
    │   │   ├── SearchDocumentsQueryTests.cs
    │   │   └── ViewDocumentListQueryTests.cs
    │   ├── Emails/
    │   │   ├── CreateEmailTemplateCommandTests.cs
    │   │   ├── EditEmailContentCommandTests.cs
    │   │   ├── ReplytoEmailCommandTests.cs
    │   │   ├── SendEmailCommandTests.cs
    │   │   ├── UpdateEmailTemplateCommandTests.cs
    │   │   ├── ViewEmailQueryTests.cs
    │   │   ├── ViewEmailTemplateDetailQueryTests.cs
    │   │   └── ViewEmailTemplateListQueryTests.cs
    │   ├── Faqs/
    │   │   ├── ChangeFAQVisibilityCommandTests.cs
    │   │   ├── CreateFAQCommandTests.cs
    │   │   ├── SearchFAQQueryTests.cs
    │   │   ├── UpdateFAQCommandTests.cs
    │   │   └── ViewListFAQQueryTests.cs
    │   ├── Feedbacks/
    │   │   ├── SearchAndFilterFeedbackQueryTests.cs
    │   │   └── ViewFeedbackSummaryQueryTests.cs
    │   ├── Galleries/
    │   │   ├── AddGalleryItemCommandTests.cs
    │   │   ├── DeleteGalleryItemCommandTests.cs
    │   │   ├── SearchGalleryItemsQueryTests.cs
    │   │   ├── UpdateGalleryItemCommandTests.cs
    │   │   └── ViewGalleryItemListQueryTests.cs
    │   ├── MeetingMinutes/
    │   │   ├── SearchAndFilterMinutesQueryTests.cs
    │   │   └── ViewMinutesListQueryTests.cs
    │   ├── News/
    │   │   ├── AddMultilingualNewsCommandTests.cs
    │   │   ├── ApproveNewsCommandTests.cs
    │   │   ├── EditNewsCommandTests.cs
    │   │   ├── ManageNewsVisibilityCommandTests.cs
    │   │   ├── PublishNewsCommandTests.cs
    │   │   ├── ViewNewsDetailsQueryTests.cs
    │   │   └── ViewNewsListQueryTests.cs
    │   ├── Partners/
    │   │   ├── EditPartnerInformationCommandTests.cs
    │   │   ├── PartnerTests.cs
    │   │   ├── ProcessPartnerCreationRequestCommandTests.cs
    │   │   ├── SearchPartnersQueryTests.cs
    │   │   ├── ViewPartnerDetailsQueryTests.cs
    │   │   └── ViewPartnerListsQueryTests.cs
    │   ├── Permissions/
    │   │   └── ConfigureRolePermissionsCommandHandlerTests.cs
    │   ├── Profiles/
    │   │   ├── ChangePasswordCommandTests.cs
    │   │   ├── UpdateProfileCommandTests.cs
    │   │   └── ViewProfileQueryTests.cs
    │   ├── PublicContent/
    │   │   ├── SearchInformationQueryTests.cs
    │   │   ├── ViewContactInfoQueryTests.cs
    │   │   ├── ViewFAQQueryTests.cs
    │   │   ├── ViewGalleryQueryTests.cs
    │   │   ├── ViewHomepageQueryTests.cs
    │   │   ├── ViewNewsQueryTests.cs
    │   │   ├── ViewNotificationsQueryTests.cs
    │   │   ├── ViewPartnersQueryTests.cs
    │   │   └── ViewPolicyAndTermsQueryTests.cs
    │   ├── Reports/
    │   │   ├── ExportStatisticsReportCommandTests.cs
    │   │   ├── FilterDashboardByTimeQueryTests.cs
    │   │   └── ViewDashboardStatisticsQueryTests.cs
    │   └── Roles/
    │       ├── ConfigureRolePermissionsCommandTests.cs
    │       ├── CreateNewRoleCommandTests.cs
    │       ├── DisableAndDeleteRoleCommandTests.cs
    │       ├── UpdateRoleDetailsCommandTests.cs
    │       └── ViewRoleListQueryTests.cs
    ├── PEMS.ArchitectureTests/
    │   ├── ApplicationHandlerTests.cs
    │   ├── ControllerTests.cs
    │   ├── DependencyRuleTests.cs
    │   ├── NamespaceAndConcreteClassTests.cs
    │   └── PEMS.ArchitectureTests.csproj
    ├── PEMS.IntegrationTests/
    │   ├── Api/
    │   │   ├── FileValidationServiceTests.cs
    │   │   ├── IdempotencyBehaviourTests.cs
    │   │   └── RateLimitMiddlewareTests.cs
    │   ├── Database/
    │   │   └── DatabaseTest.cs
    │   └── Security/
    │       ├── OwnershipCheckerTests.cs
    │       └── PermissionCheckerTests.cs
    └── PEMS.UnitTests/
        ├── Application/
        │   └── ApplicationDummyTest.cs
        ├── Domain/
        │   └── DomainDummyTest.cs
        └── SharedKernel/
            └── SharedKernelDummyTest.cs
```

> **Ghi chú thật:** `.vscode/launch.json`, `.vscode/tasks.json` tồn tại trên đĩa nhưng đang bị `.gitignore` (không tracked) — vẫn liệt kê vì thuộc phạm vi scan. Trong `tests/`, chỉ `PEMS.ArchitectureTests` có file `.csproj`; ba project test còn lại (`PEMS.ApplicationTests`, `PEMS.IntegrationTests`, `PEMS.UnitTests`) **không có `.csproj`** trên đĩa.

### 2.2 PEMS.Api

```txt
backend/PEMS.Api/
├── Contracts/
│   ├── ApiResponse.cs
│   └── ApiRoutes.cs
├── Controllers/
│   ├── AccountsController.cs
│   ├── AgendaTemplatesController.cs
│   ├── ApiIntegrationsController.cs
│   ├── AuthenticationController.cs
│   ├── CalendarsController.cs
│   ├── CampusesController.cs
│   ├── DelegationsController.cs
│   ├── DepartmentsController.cs
│   ├── DocumentsController.cs
│   ├── EmailsController.cs
│   ├── FaqsController.cs
│   ├── FeedbacksController.cs
│   ├── GalleriesController.cs
│   ├── MeetingMinutesController.cs
│   ├── NewsController.cs
│   ├── PartnersController.cs
│   ├── ProfilesController.cs
│   ├── PublicContentController.cs
│   ├── ReportsController.cs
│   └── RolesController.cs
├── Extensions/
│   ├── AuthenticationExtensions.cs
│   ├── AuthorizationExtensions.cs
│   ├── CorsExtensions.cs
│   ├── RateLimitingExtensions.cs
│   ├── ServiceCollectionExtensions.cs
│   └── SwaggerExtensions.cs
├── Filters/
│   ├── FileUploadValidationFilter.cs
│   ├── IdempotencyFilter.cs
│   ├── PermissionAuthorizeAttribute.cs
│   └── ValidationFilter.cs
├── Middleware/
│   ├── CurrentUserMiddleware.cs
│   ├── ExceptionHandlingMiddleware.cs
│   ├── RateLimitMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   └── SecurityHeadersMiddleware.cs
├── Properties/
│   └── launchSettings.json
├── PEMS.Api.csproj
├── Pems_WebAPI.http
├── Program.cs
└── appsettings.json
```

### 2.3 PEMS.Application

```txt
backend/PEMS.Application/
├── Accounts/
│   ├── Commands/
│   │   ├── CreateAccount/
│   │   │   ├── CreateAccountCommand.cs
│   │   │   ├── CreateAccountCommandHandler.cs
│   │   │   ├── CreateAccountCommandValidator.cs
│   │   │   └── CreateAccountResponse.cs
│   │   ├── ManageAccountStatus/
│   │   │   ├── ManageAccountStatusCommand.cs
│   │   │   ├── ManageAccountStatusCommandHandler.cs
│   │   │   ├── ManageAccountStatusCommandValidator.cs
│   │   │   └── ManageAccountStatusResponse.cs
│   │   └── UpdateAccountRole/
│   │       ├── UpdateAccountRoleCommand.cs
│   │       ├── UpdateAccountRoleCommandHandler.cs
│   │       ├── UpdateAccountRoleCommandValidator.cs
│   │       └── UpdateAccountRoleResponse.cs
│   └── Queries/
│       ├── SearchandFilterAccounts/
│       │   ├── SearchandFilterAccountsDto.cs
│       │   ├── SearchandFilterAccountsQuery.cs
│       │   └── SearchandFilterAccountsQueryHandler.cs
│       ├── ViewAccountDetails/
│       │   ├── ViewAccountDetailsDto.cs
│       │   ├── ViewAccountDetailsQuery.cs
│       │   └── ViewAccountDetailsQueryHandler.cs
│       └── ViewAccountList/
│           ├── ViewAccountListDto.cs
│           ├── ViewAccountListQuery.cs
│           └── ViewAccountListQueryHandler.cs
├── AgendaTemplates/
│   ├── Commands/
│   │   ├── CreateAgendaTemplate/
│   │   │   ├── CreateAgendaTemplateCommand.cs
│   │   │   ├── CreateAgendaTemplateCommandHandler.cs
│   │   │   ├── CreateAgendaTemplateCommandValidator.cs
│   │   │   └── CreateAgendaTemplateResponse.cs
│   │   ├── DeleteAgendaTemplate/
│   │   │   ├── DeleteAgendaTemplateCommand.cs
│   │   │   ├── DeleteAgendaTemplateCommandHandler.cs
│   │   │   ├── DeleteAgendaTemplateCommandValidator.cs
│   │   │   └── DeleteAgendaTemplateResponse.cs
│   │   └── UpdateAgendaTemplate/
│   │       ├── UpdateAgendaTemplateCommand.cs
│   │       ├── UpdateAgendaTemplateCommandHandler.cs
│   │       ├── UpdateAgendaTemplateCommandValidator.cs
│   │       └── UpdateAgendaTemplateResponse.cs
│   └── Queries/
│       ├── ViewAgendaTemplateDetail/
│       │   ├── ViewAgendaTemplateDetailDto.cs
│       │   ├── ViewAgendaTemplateDetailQuery.cs
│       │   └── ViewAgendaTemplateDetailQueryHandler.cs
│       └── ViewAgendaTemplateList/
│           ├── ViewAgendaTemplateListDto.cs
│           ├── ViewAgendaTemplateListQuery.cs
│           └── ViewAgendaTemplateListQueryHandler.cs
├── ApiIntegrations/
│   ├── Commands/
│   │   ├── ConfigureRequestLimit/
│   │   │   ├── ConfigureRequestLimitCommand.cs
│   │   │   ├── ConfigureRequestLimitCommandHandler.cs
│   │   │   ├── ConfigureRequestLimitCommandValidator.cs
│   │   │   └── ConfigureRequestLimitResponse.cs
│   │   ├── CreateAPIConfiguration/
│   │   │   ├── CreateAPIConfigurationCommand.cs
│   │   │   ├── CreateAPIConfigurationCommandHandler.cs
│   │   │   ├── CreateAPIConfigurationCommandValidator.cs
│   │   │   └── CreateAPIConfigurationResponse.cs
│   │   ├── DeleteAPIConfiguration/
│   │   │   ├── DeleteAPIConfigurationCommand.cs
│   │   │   ├── DeleteAPIConfigurationCommandHandler.cs
│   │   │   ├── DeleteAPIConfigurationCommandValidator.cs
│   │   │   └── DeleteAPIConfigurationResponse.cs
│   │   ├── ManageAPIStatus/
│   │   │   ├── ManageAPIStatusCommand.cs
│   │   │   ├── ManageAPIStatusCommandHandler.cs
│   │   │   ├── ManageAPIStatusCommandValidator.cs
│   │   │   └── ManageAPIStatusResponse.cs
│   │   ├── TestAPIConnection/
│   │   │   ├── TestAPIConnectionCommand.cs
│   │   │   ├── TestAPIConnectionCommandHandler.cs
│   │   │   ├── TestAPIConnectionCommandValidator.cs
│   │   │   └── TestAPIConnectionResponse.cs
│   │   └── UpdateAPIConfiguration/
│   │       ├── UpdateAPIConfigurationCommand.cs
│   │       ├── UpdateAPIConfigurationCommandHandler.cs
│   │       ├── UpdateAPIConfigurationCommandValidator.cs
│   │       └── UpdateAPIConfigurationResponse.cs
│   └── Queries/
│       ├── SearchAPILogs/
│       │   ├── SearchAPILogsDto.cs
│       │   ├── SearchAPILogsQuery.cs
│       │   └── SearchAPILogsQueryHandler.cs
│       ├── ViewAPIConfiguration/
│       │   ├── ViewAPIConfigurationDto.cs
│       │   ├── ViewAPIConfigurationQuery.cs
│       │   └── ViewAPIConfigurationQueryHandler.cs
│       └── ViewAPILogs/
│           ├── ViewAPILogsDto.cs
│           ├── ViewAPILogsQuery.cs
│           └── ViewAPILogsQueryHandler.cs
├── Authentication/
│   ├── Commands/
│   │   ├── ForgotPassword/
│   │   │   ├── ForgotPasswordCommand.cs
│   │   │   ├── ForgotPasswordCommandHandler.cs
│   │   │   ├── ForgotPasswordCommandValidator.cs
│   │   │   └── ForgotPasswordResponse.cs
│   │   ├── LoginViaCredentials/
│   │   │   ├── LoginViaCredentialsCommand.cs
│   │   │   ├── LoginViaCredentialsCommandHandler.cs
│   │   │   ├── LoginViaCredentialsCommandValidator.cs
│   │   │   └── LoginViaCredentialsResponse.cs
│   │   ├── LoginViaSso/
│   │   │   ├── LoginViaSsoCommand.cs
│   │   │   ├── LoginViaSsoCommandHandler.cs
│   │   │   ├── LoginViaSsoCommandValidator.cs
│   │   │   └── LoginViaSsoResponse.cs
│   │   └── Logout/
│   │       ├── LogoutCommand.cs
│   │       ├── LogoutCommandHandler.cs
│   │       ├── LogoutCommandValidator.cs
│   │       └── LogoutResponse.cs
│   ├── Mappings/
│   │   └── AuthenticationMappingProfile.cs
│   └── Rules/
│       └── README.md
├── Calendars/
│   ├── Commands/
│   │   ├── AddPersonalEvent/
│   │   │   ├── AddPersonalEventCommand.cs
│   │   │   ├── AddPersonalEventCommandHandler.cs
│   │   │   ├── AddPersonalEventCommandValidator.cs
│   │   │   └── AddPersonalEventResponse.cs
│   │   ├── DeletePersonalEvent/
│   │   │   ├── DeletePersonalEventCommand.cs
│   │   │   ├── DeletePersonalEventCommandHandler.cs
│   │   │   ├── DeletePersonalEventCommandValidator.cs
│   │   │   └── DeletePersonalEventResponse.cs
│   │   ├── SwitchViewMode/
│   │   │   ├── SwitchViewModeCommand.cs
│   │   │   ├── SwitchViewModeCommandHandler.cs
│   │   │   ├── SwitchViewModeCommandValidator.cs
│   │   │   └── SwitchViewModeResponse.cs
│   │   └── UpdatePersonalEvent/
│   │       ├── UpdatePersonalEventCommand.cs
│   │       ├── UpdatePersonalEventCommandHandler.cs
│   │       ├── UpdatePersonalEventCommandValidator.cs
│   │       └── UpdatePersonalEventResponse.cs
│   └── Queries/
│       ├── ViewDepartmentCalendar/
│       │   ├── ViewDepartmentCalendarDto.cs
│       │   ├── ViewDepartmentCalendarQuery.cs
│       │   └── ViewDepartmentCalendarQueryHandler.cs
│       ├── ViewEventDetails/
│       │   ├── ViewEventDetailsDto.cs
│       │   ├── ViewEventDetailsQuery.cs
│       │   └── ViewEventDetailsQueryHandler.cs
│       └── ViewMyEvents/
│           ├── ViewMyEventsDto.cs
│           ├── ViewMyEventsQuery.cs
│           └── ViewMyEventsQueryHandler.cs
├── Campuses/
│   ├── Commands/
│   │   ├── AddNewCampus/
│   │   │   ├── AddNewCampusCommand.cs
│   │   │   ├── AddNewCampusCommandHandler.cs
│   │   │   ├── AddNewCampusCommandValidator.cs
│   │   │   └── AddNewCampusResponse.cs
│   │   ├── AssignCampusLead/
│   │   │   ├── AssignCampusLeadCommand.cs
│   │   │   ├── AssignCampusLeadCommandHandler.cs
│   │   │   ├── AssignCampusLeadCommandValidator.cs
│   │   │   └── AssignCampusLeadResponse.cs
│   │   ├── ManageCampusStatus/
│   │   │   ├── ManageCampusStatusCommand.cs
│   │   │   ├── ManageCampusStatusCommandHandler.cs
│   │   │   ├── ManageCampusStatusCommandValidator.cs
│   │   │   └── ManageCampusStatusResponse.cs
│   │   └── UpdateCampus/
│   │       ├── UpdateCampusCommand.cs
│   │       ├── UpdateCampusCommandHandler.cs
│   │       ├── UpdateCampusCommandValidator.cs
│   │       └── UpdateCampusResponse.cs
│   └── Queries/
│       ├── SearchandFilterCampus/
│       │   ├── SearchandFilterCampusDto.cs
│       │   ├── SearchandFilterCampusQuery.cs
│       │   └── SearchandFilterCampusQueryHandler.cs
│       ├── ViewCampusDetails/
│       │   ├── ViewCampusDetailsDto.cs
│       │   ├── ViewCampusDetailsQuery.cs
│       │   └── ViewCampusDetailsQueryHandler.cs
│       └── ViewCampusList/
│           ├── ViewCampusListDto.cs
│           ├── ViewCampusListQuery.cs
│           └── ViewCampusListQueryHandler.cs
├── Common/
│   ├── Behaviours/
│   │   ├── AuditLogBehaviour.cs
│   │   ├── AuthorizationBehaviour.cs
│   │   ├── IdempotencyBehaviour.cs
│   │   ├── LoggingBehaviour.cs
│   │   ├── TransactionBehaviour.cs
│   │   └── ValidationBehaviour.cs
│   ├── Exceptions/
│   │   ├── BusinessRuleException.cs
│   │   ├── ConflictException.cs
│   │   ├── ForbiddenException.cs
│   │   ├── NotFoundException.cs
│   │   └── ValidationException.cs
│   ├── Interfaces/
│   │   ├── IApplicationDbContext.cs
│   │   ├── IAuditLogService.cs
│   │   ├── ICampusRepository.cs
│   │   ├── ICurrentUserService.cs
│   │   ├── IDateTimeService.cs
│   │   ├── IDelegationRepository.cs
│   │   ├── IDocumentRepository.cs
│   │   ├── IEmailService.cs
│   │   ├── IExternalApiClient.cs
│   │   ├── IFaceRecognitionService.cs
│   │   ├── IFileStorageService.cs
│   │   ├── IFileValidationService.cs
│   │   ├── IIdempotencyService.cs
│   │   ├── IJwtTokenService.cs
│   │   ├── INotificationService.cs
│   │   ├── IOcrService.cs
│   │   ├── IOwnershipChecker.cs
│   │   ├── IPartnerRepository.cs
│   │   ├── IPasswordHasher.cs
│   │   ├── IPermissionChecker.cs
│   │   ├── IRateLimitService.cs
│   │   └── IUserRepository.cs
│   ├── Models/
│   │   ├── ErrorResponse.cs
│   │   ├── FileUploadResult.cs
│   │   ├── PagedResult.cs
│   │   ├── PaginationRequest.cs
│   │   ├── Result.cs
│   │   └── ResultOfT.cs
│   └── Security/
│       ├── PermissionConstants.cs
│       ├── PermissionRequirement.cs
│       └── UseCasePermissionAttribute.cs
├── Delegations/
│   ├── Commands/
│   │   ├── ApproveCrossCampusRequest/
│   │   │   ├── ApproveCrossCampusRequestCommand.cs
│   │   │   ├── ApproveCrossCampusRequestCommandHandler.cs
│   │   │   ├── ApproveCrossCampusRequestCommandValidator.cs
│   │   │   └── ApproveCrossCampusRequestResponse.cs
│   │   ├── ApproveResourceRequest/
│   │   │   ├── ApproveResourceRequestCommand.cs
│   │   │   ├── ApproveResourceRequestCommandHandler.cs
│   │   │   ├── ApproveResourceRequestCommandValidator.cs
│   │   │   └── ApproveResourceRequestResponse.cs
│   │   ├── CloseDelegation/
│   │   │   ├── CloseDelegationCommand.cs
│   │   │   ├── CloseDelegationCommandHandler.cs
│   │   │   ├── CloseDelegationCommandValidator.cs
│   │   │   └── CloseDelegationResponse.cs
│   │   ├── ConfirmParticipation/
│   │   │   ├── ConfirmParticipationCommand.cs
│   │   │   ├── ConfirmParticipationCommandHandler.cs
│   │   │   ├── ConfirmParticipationCommandValidator.cs
│   │   │   └── ConfirmParticipationResponse.cs
│   │   ├── ConfirmTheChangeProposal/
│   │   │   ├── ConfirmTheChangeProposalCommand.cs
│   │   │   ├── ConfirmTheChangeProposalCommandHandler.cs
│   │   │   ├── ConfirmTheChangeProposalCommandValidator.cs
│   │   │   └── ConfirmTheChangeProposalResponse.cs
│   │   ├── CreateGuestDelegation/
│   │   │   ├── CreateGuestDelegationCommand.cs
│   │   │   ├── CreateGuestDelegationCommandHandler.cs
│   │   │   ├── CreateGuestDelegationCommandValidator.cs
│   │   │   └── CreateGuestDelegationResponse.cs
│   │   ├── CreateMeetingMinutes/
│   │   │   ├── CreateMeetingMinutesCommand.cs
│   │   │   ├── CreateMeetingMinutesCommandHandler.cs
│   │   │   ├── CreateMeetingMinutesCommandValidator.cs
│   │   │   └── CreateMeetingMinutesResponse.cs
│   │   ├── CreateNewsArticle/
│   │   │   ├── CreateNewsArticleCommand.cs
│   │   │   ├── CreateNewsArticleCommandHandler.cs
│   │   │   ├── CreateNewsArticleCommandValidator.cs
│   │   │   └── CreateNewsArticleResponse.cs
│   │   ├── CreatePartnerProfile/
│   │   │   ├── CreatePartnerProfileCommand.cs
│   │   │   ├── CreatePartnerProfileCommandHandler.cs
│   │   │   ├── CreatePartnerProfileCommandValidator.cs
│   │   │   └── CreatePartnerProfileResponse.cs
│   │   ├── EditMeetingMinutes/
│   │   │   ├── EditMeetingMinutesCommand.cs
│   │   │   ├── EditMeetingMinutesCommandHandler.cs
│   │   │   ├── EditMeetingMinutesCommandValidator.cs
│   │   │   └── EditMeetingMinutesResponse.cs
│   │   ├── PrepareVisitLogistics/
│   │   │   ├── PrepareVisitLogisticsCommand.cs
│   │   │   ├── PrepareVisitLogisticsCommandHandler.cs
│   │   │   ├── PrepareVisitLogisticsCommandValidator.cs
│   │   │   └── PrepareVisitLogisticsResponse.cs
│   │   ├── ProcessVisitRequest/
│   │   │   ├── ProcessVisitRequestCommand.cs
│   │   │   ├── ProcessVisitRequestCommandHandler.cs
│   │   │   ├── ProcessVisitRequestCommandValidator.cs
│   │   │   └── ProcessVisitRequestResponse.cs
│   │   ├── ProposeResourceModification/
│   │   │   ├── ProposeResourceModificationCommand.cs
│   │   │   ├── ProposeResourceModificationCommandHandler.cs
│   │   │   ├── ProposeResourceModificationCommandValidator.cs
│   │   │   └── ProposeResourceModificationResponse.cs
│   │   ├── ScanBusinessCard/
│   │   │   ├── ScanBusinessCardCommand.cs
│   │   │   ├── ScanBusinessCardCommandHandler.cs
│   │   │   ├── ScanBusinessCardCommandValidator.cs
│   │   │   └── ScanBusinessCardResponse.cs
│   │   ├── SubmitDelegationFeedback/
│   │   │   ├── SubmitDelegationFeedbackCommand.cs
│   │   │   ├── SubmitDelegationFeedbackCommandHandler.cs
│   │   │   ├── SubmitDelegationFeedbackCommandValidator.cs
│   │   │   └── SubmitDelegationFeedbackResponse.cs
│   │   ├── SubmitVisitRequest/
│   │   │   ├── SubmitVisitRequestCommand.cs
│   │   │   ├── SubmitVisitRequestCommandHandler.cs
│   │   │   ├── SubmitVisitRequestCommandValidator.cs
│   │   │   └── SubmitVisitRequestResponse.cs
│   │   ├── TagFacesOnPhotos/
│   │   │   ├── TagFacesOnPhotosCommand.cs
│   │   │   ├── TagFacesOnPhotosCommandHandler.cs
│   │   │   ├── TagFacesOnPhotosCommandValidator.cs
│   │   │   └── TagFacesOnPhotosResponse.cs
│   │   ├── UpdateGuestDelegation/
│   │   │   ├── UpdateGuestDelegationCommand.cs
│   │   │   ├── UpdateGuestDelegationCommandHandler.cs
│   │   │   ├── UpdateGuestDelegationCommandValidator.cs
│   │   │   └── UpdateGuestDelegationResponse.cs
│   │   ├── UpdateVisitLogistics/
│   │   │   ├── UpdateVisitLogisticsCommand.cs
│   │   │   ├── UpdateVisitLogisticsCommandHandler.cs
│   │   │   ├── UpdateVisitLogisticsCommandValidator.cs
│   │   │   └── UpdateVisitLogisticsResponse.cs
│   │   ├── UploadAttachedDocuments/
│   │   │   ├── UploadAttachedDocumentsCommand.cs
│   │   │   ├── UploadAttachedDocumentsCommandHandler.cs
│   │   │   ├── UploadAttachedDocumentsCommandValidator.cs
│   │   │   └── UploadAttachedDocumentsResponse.cs
│   │   └── UploadVisitPhotos/
│   │       ├── UploadVisitPhotosCommand.cs
│   │       ├── UploadVisitPhotosCommandHandler.cs
│   │       ├── UploadVisitPhotosCommandValidator.cs
│   │       └── UploadVisitPhotosResponse.cs
│   ├── Dtos/
│   │   └── README.md
│   ├── Mappings/
│   │   └── DelegationsMappingProfile.cs
│   ├── Queries/
│   │   ├── SearchDelegations/
│   │   │   ├── SearchDelegationsDto.cs
│   │   │   ├── SearchDelegationsQuery.cs
│   │   │   └── SearchDelegationsQueryHandler.cs
│   │   ├── ViewGuestDelegationDetails/
│   │   │   ├── ViewGuestDelegationDetailsDto.cs
│   │   │   ├── ViewGuestDelegationDetailsQuery.cs
│   │   │   └── ViewGuestDelegationDetailsQueryHandler.cs
│   │   ├── ViewGuestDelegationList/
│   │   │   ├── ViewGuestDelegationListDto.cs
│   │   │   ├── ViewGuestDelegationListQuery.cs
│   │   │   └── ViewGuestDelegationListQueryHandler.cs
│   │   └── ViewMeetingMinutesDetails/
│   │       ├── ViewMeetingMinutesDetailsDto.cs
│   │       ├── ViewMeetingMinutesDetailsQuery.cs
│   │       └── ViewMeetingMinutesDetailsQueryHandler.cs
│   └── Rules/
│       └── README.md
├── Departments/
│   ├── Commands/
│   │   ├── AddDepartmentPersonnel/
│   │   │   ├── AddDepartmentPersonnelCommand.cs
│   │   │   ├── AddDepartmentPersonnelCommandHandler.cs
│   │   │   ├── AddDepartmentPersonnelCommandValidator.cs
│   │   │   └── AddDepartmentPersonnelResponse.cs
│   │   ├── AddNewDepartment/
│   │   │   ├── AddNewDepartmentCommand.cs
│   │   │   ├── AddNewDepartmentCommandHandler.cs
│   │   │   ├── AddNewDepartmentCommandValidator.cs
│   │   │   └── AddNewDepartmentResponse.cs
│   │   ├── AssignTasks/
│   │   │   ├── AssignTasksCommand.cs
│   │   │   ├── AssignTasksCommandHandler.cs
│   │   │   ├── AssignTasksCommandValidator.cs
│   │   │   └── AssignTasksResponse.cs
│   │   ├── ManageDepartmentStatus/
│   │   │   ├── ManageDepartmentStatusCommand.cs
│   │   │   ├── ManageDepartmentStatusCommandHandler.cs
│   │   │   ├── ManageDepartmentStatusCommandValidator.cs
│   │   │   └── ManageDepartmentStatusResponse.cs
│   │   ├── ReassignDepartmentLead/
│   │   │   ├── ReassignDepartmentLeadCommand.cs
│   │   │   ├── ReassignDepartmentLeadCommandHandler.cs
│   │   │   ├── ReassignDepartmentLeadCommandValidator.cs
│   │   │   └── ReassignDepartmentLeadResponse.cs
│   │   ├── RemovePersonnel/
│   │   │   ├── RemovePersonnelCommand.cs
│   │   │   ├── RemovePersonnelCommandHandler.cs
│   │   │   ├── RemovePersonnelCommandValidator.cs
│   │   │   └── RemovePersonnelResponse.cs
│   │   ├── ReviewAssignedTasks/
│   │   │   ├── ReviewAssignedTasksCommand.cs
│   │   │   ├── ReviewAssignedTasksCommandHandler.cs
│   │   │   ├── ReviewAssignedTasksCommandValidator.cs
│   │   │   └── ReviewAssignedTasksResponse.cs
│   │   ├── SignTheServiceDeliveryReport/
│   │   │   ├── SignTheServiceDeliveryReportCommand.cs
│   │   │   ├── SignTheServiceDeliveryReportCommandHandler.cs
│   │   │   ├── SignTheServiceDeliveryReportCommandValidator.cs
│   │   │   └── SignTheServiceDeliveryReportResponse.cs
│   │   └── UpdateDepartment/
│   │       ├── UpdateDepartmentCommand.cs
│   │       ├── UpdateDepartmentCommandHandler.cs
│   │       ├── UpdateDepartmentCommandValidator.cs
│   │       └── UpdateDepartmentResponse.cs
│   └── Queries/
│       ├── SearchCoordinationTasks/
│       │   ├── SearchCoordinationTasksDto.cs
│       │   ├── SearchCoordinationTasksQuery.cs
│       │   └── SearchCoordinationTasksQueryHandler.cs
│       ├── SearchPersonnel/
│       │   ├── SearchPersonnelDto.cs
│       │   ├── SearchPersonnelQuery.cs
│       │   └── SearchPersonnelQueryHandler.cs
│       ├── SearchandFilterDepartments/
│       │   ├── SearchandFilterDepartmentsDto.cs
│       │   ├── SearchandFilterDepartmentsQuery.cs
│       │   └── SearchandFilterDepartmentsQueryHandler.cs
│       ├── ViewCoordinationTasks/
│       │   ├── ViewCoordinationTasksDto.cs
│       │   ├── ViewCoordinationTasksQuery.cs
│       │   └── ViewCoordinationTasksQueryHandler.cs
│       ├── ViewDepartmentDetails/
│       │   ├── ViewDepartmentDetailsDto.cs
│       │   ├── ViewDepartmentDetailsQuery.cs
│       │   └── ViewDepartmentDetailsQueryHandler.cs
│       ├── ViewDepartmentList/
│       │   ├── ViewDepartmentListDto.cs
│       │   ├── ViewDepartmentListQuery.cs
│       │   └── ViewDepartmentListQueryHandler.cs
│       └── ViewPersonnelDetails/
│           ├── ViewPersonnelDetailsDto.cs
│           ├── ViewPersonnelDetailsQuery.cs
│           └── ViewPersonnelDetailsQueryHandler.cs
├── Documents/
│   └── Queries/
│       ├── SearchDocuments/
│       │   ├── SearchDocumentsDto.cs
│       │   ├── SearchDocumentsQuery.cs
│       │   └── SearchDocumentsQueryHandler.cs
│       └── ViewDocumentList/
│           ├── ViewDocumentListDto.cs
│           ├── ViewDocumentListQuery.cs
│           └── ViewDocumentListQueryHandler.cs
├── Emails/
│   ├── Commands/
│   │   ├── CreateEmailTemplate/
│   │   │   ├── CreateEmailTemplateCommand.cs
│   │   │   ├── CreateEmailTemplateCommandHandler.cs
│   │   │   ├── CreateEmailTemplateCommandValidator.cs
│   │   │   └── CreateEmailTemplateResponse.cs
│   │   ├── EditEmailContent/
│   │   │   ├── EditEmailContentCommand.cs
│   │   │   ├── EditEmailContentCommandHandler.cs
│   │   │   ├── EditEmailContentCommandValidator.cs
│   │   │   └── EditEmailContentResponse.cs
│   │   ├── ReplytoEmail/
│   │   │   ├── ReplytoEmailCommand.cs
│   │   │   ├── ReplytoEmailCommandHandler.cs
│   │   │   ├── ReplytoEmailCommandValidator.cs
│   │   │   └── ReplytoEmailResponse.cs
│   │   ├── SendEmail/
│   │   │   ├── SendEmailCommand.cs
│   │   │   ├── SendEmailCommandHandler.cs
│   │   │   ├── SendEmailCommandValidator.cs
│   │   │   └── SendEmailResponse.cs
│   │   └── UpdateEmailTemplate/
│   │       ├── UpdateEmailTemplateCommand.cs
│   │       ├── UpdateEmailTemplateCommandHandler.cs
│   │       ├── UpdateEmailTemplateCommandValidator.cs
│   │       └── UpdateEmailTemplateResponse.cs
│   └── Queries/
│       ├── ViewEmail/
│       │   ├── ViewEmailDto.cs
│       │   ├── ViewEmailQuery.cs
│       │   └── ViewEmailQueryHandler.cs
│       ├── ViewEmailTemplateDetail/
│       │   ├── ViewEmailTemplateDetailDto.cs
│       │   ├── ViewEmailTemplateDetailQuery.cs
│       │   └── ViewEmailTemplateDetailQueryHandler.cs
│       └── ViewEmailTemplateList/
│           ├── ViewEmailTemplateListDto.cs
│           ├── ViewEmailTemplateListQuery.cs
│           └── ViewEmailTemplateListQueryHandler.cs
├── Faqs/
│   ├── Commands/
│   │   ├── ChangeFAQVisibility/
│   │   │   ├── ChangeFAQVisibilityCommand.cs
│   │   │   ├── ChangeFAQVisibilityCommandHandler.cs
│   │   │   ├── ChangeFAQVisibilityCommandValidator.cs
│   │   │   └── ChangeFAQVisibilityResponse.cs
│   │   ├── CreateFAQ/
│   │   │   ├── CreateFAQCommand.cs
│   │   │   ├── CreateFAQCommandHandler.cs
│   │   │   ├── CreateFAQCommandValidator.cs
│   │   │   └── CreateFAQResponse.cs
│   │   └── UpdateFAQ/
│   │       ├── UpdateFAQCommand.cs
│   │       ├── UpdateFAQCommandHandler.cs
│   │       ├── UpdateFAQCommandValidator.cs
│   │       └── UpdateFAQResponse.cs
│   └── Queries/
│       ├── SearchFAQ/
│       │   ├── SearchFAQDto.cs
│       │   ├── SearchFAQQuery.cs
│       │   └── SearchFAQQueryHandler.cs
│       └── ViewListFAQ/
│           ├── ViewListFAQDto.cs
│           ├── ViewListFAQQuery.cs
│           └── ViewListFAQQueryHandler.cs
├── Feedbacks/
│   └── Queries/
│       ├── SearchAndFilterFeedback/
│       │   ├── SearchAndFilterFeedbackDto.cs
│       │   ├── SearchAndFilterFeedbackQuery.cs
│       │   └── SearchAndFilterFeedbackQueryHandler.cs
│       └── ViewFeedbackSummary/
│           ├── ViewFeedbackSummaryDto.cs
│           ├── ViewFeedbackSummaryQuery.cs
│           └── ViewFeedbackSummaryQueryHandler.cs
├── Galleries/
│   ├── Commands/
│   │   ├── AddGalleryItem/
│   │   │   ├── AddGalleryItemCommand.cs
│   │   │   ├── AddGalleryItemCommandHandler.cs
│   │   │   ├── AddGalleryItemCommandValidator.cs
│   │   │   └── AddGalleryItemResponse.cs
│   │   ├── DeleteGalleryItem/
│   │   │   ├── DeleteGalleryItemCommand.cs
│   │   │   ├── DeleteGalleryItemCommandHandler.cs
│   │   │   ├── DeleteGalleryItemCommandValidator.cs
│   │   │   └── DeleteGalleryItemResponse.cs
│   │   └── UpdateGalleryItem/
│   │       ├── UpdateGalleryItemCommand.cs
│   │       ├── UpdateGalleryItemCommandHandler.cs
│   │       ├── UpdateGalleryItemCommandValidator.cs
│   │       └── UpdateGalleryItemResponse.cs
│   └── Queries/
│       ├── SearchGalleryItems/
│       │   ├── SearchGalleryItemsDto.cs
│       │   ├── SearchGalleryItemsQuery.cs
│       │   └── SearchGalleryItemsQueryHandler.cs
│       └── ViewGalleryItemList/
│           ├── ViewGalleryItemListDto.cs
│           ├── ViewGalleryItemListQuery.cs
│           └── ViewGalleryItemListQueryHandler.cs
├── MeetingMinutes/
│   └── Queries/
│       ├── SearchAndFilterMinutes/
│       │   ├── SearchAndFilterMinutesDto.cs
│       │   ├── SearchAndFilterMinutesQuery.cs
│       │   └── SearchAndFilterMinutesQueryHandler.cs
│       └── ViewMinutesList/
│           ├── ViewMinutesListDto.cs
│           ├── ViewMinutesListQuery.cs
│           └── ViewMinutesListQueryHandler.cs
├── News/
│   ├── Commands/
│   │   ├── AddMultilingualNews/
│   │   │   ├── AddMultilingualNewsCommand.cs
│   │   │   ├── AddMultilingualNewsCommandHandler.cs
│   │   │   ├── AddMultilingualNewsCommandValidator.cs
│   │   │   └── AddMultilingualNewsResponse.cs
│   │   ├── ApproveNews/
│   │   │   ├── ApproveNewsCommand.cs
│   │   │   ├── ApproveNewsCommandHandler.cs
│   │   │   ├── ApproveNewsCommandValidator.cs
│   │   │   └── ApproveNewsResponse.cs
│   │   ├── EditNews/
│   │   │   ├── EditNewsCommand.cs
│   │   │   ├── EditNewsCommandHandler.cs
│   │   │   ├── EditNewsCommandValidator.cs
│   │   │   └── EditNewsResponse.cs
│   │   ├── ManageNewsVisibility/
│   │   │   ├── ManageNewsVisibilityCommand.cs
│   │   │   ├── ManageNewsVisibilityCommandHandler.cs
│   │   │   ├── ManageNewsVisibilityCommandValidator.cs
│   │   │   └── ManageNewsVisibilityResponse.cs
│   │   └── PublishNews/
│   │       ├── PublishNewsCommand.cs
│   │       ├── PublishNewsCommandHandler.cs
│   │       ├── PublishNewsCommandValidator.cs
│   │       └── PublishNewsResponse.cs
│   └── Queries/
│       ├── ViewNewsDetails/
│       │   ├── ViewNewsDetailsDto.cs
│       │   ├── ViewNewsDetailsQuery.cs
│       │   └── ViewNewsDetailsQueryHandler.cs
│       └── ViewNewsList/
│           ├── ViewNewsListDto.cs
│           ├── ViewNewsListQuery.cs
│           └── ViewNewsListQueryHandler.cs
├── Partners/
│   ├── Commands/
│   │   ├── EditPartnerInformation/
│   │   │   ├── EditPartnerInformationCommand.cs
│   │   │   ├── EditPartnerInformationCommandHandler.cs
│   │   │   ├── EditPartnerInformationCommandValidator.cs
│   │   │   └── EditPartnerInformationResponse.cs
│   │   └── ProcessPartnerCreationRequest/
│   │       ├── ProcessPartnerCreationRequestCommand.cs
│   │       ├── ProcessPartnerCreationRequestCommandHandler.cs
│   │       ├── ProcessPartnerCreationRequestCommandValidator.cs
│   │       └── ProcessPartnerCreationRequestResponse.cs
│   ├── Dtos/
│   │   └── README.md
│   ├── Mappings/
│   │   └── PartnersMappingProfile.cs
│   ├── Queries/
│   │   ├── SearchPartners/
│   │   │   ├── SearchPartnersDto.cs
│   │   │   ├── SearchPartnersQuery.cs
│   │   │   └── SearchPartnersQueryHandler.cs
│   │   ├── ViewPartnerDetails/
│   │   │   ├── ViewPartnerDetailsDto.cs
│   │   │   ├── ViewPartnerDetailsQuery.cs
│   │   │   └── ViewPartnerDetailsQueryHandler.cs
│   │   └── ViewPartnerLists/
│   │       ├── ViewPartnerListsDto.cs
│   │       ├── ViewPartnerListsQuery.cs
│   │       └── ViewPartnerListsQueryHandler.cs
│   └── Rules/
│       └── README.md
├── Profiles/
│   ├── Commands/
│   │   ├── ChangePassword/
│   │   │   ├── ChangePasswordCommand.cs
│   │   │   ├── ChangePasswordCommandHandler.cs
│   │   │   ├── ChangePasswordCommandValidator.cs
│   │   │   └── ChangePasswordResponse.cs
│   │   └── UpdateProfile/
│   │       ├── UpdateProfileCommand.cs
│   │       ├── UpdateProfileCommandHandler.cs
│   │       ├── UpdateProfileCommandValidator.cs
│   │       └── UpdateProfileResponse.cs
│   ├── Dtos/
│   │   └── README.md
│   ├── Mappings/
│   │   └── ProfilesMappingProfile.cs
│   ├── Queries/
│   │   └── ViewProfile/
│   │       ├── ViewProfileDto.cs
│   │       ├── ViewProfileQuery.cs
│   │       └── ViewProfileQueryHandler.cs
│   └── Rules/
│       └── README.md
├── PublicContent/
│   ├── Dtos/
│   │   └── README.md
│   ├── Mappings/
│   │   └── PublicContentMappingProfile.cs
│   ├── Queries/
│   │   ├── SearchInformation/
│   │   │   ├── SearchInformationDto.cs
│   │   │   ├── SearchInformationQuery.cs
│   │   │   └── SearchInformationQueryHandler.cs
│   │   ├── ViewContactInfo/
│   │   │   ├── ViewContactInfoDto.cs
│   │   │   ├── ViewContactInfoQuery.cs
│   │   │   └── ViewContactInfoQueryHandler.cs
│   │   ├── ViewFaq/
│   │   │   ├── ViewFaqDto.cs
│   │   │   ├── ViewFaqQuery.cs
│   │   │   └── ViewFaqQueryHandler.cs
│   │   ├── ViewGallery/
│   │   │   ├── ViewGalleryDto.cs
│   │   │   ├── ViewGalleryQuery.cs
│   │   │   └── ViewGalleryQueryHandler.cs
│   │   ├── ViewHomepage/
│   │   │   ├── ViewHomepageDto.cs
│   │   │   ├── ViewHomepageQuery.cs
│   │   │   └── ViewHomepageQueryHandler.cs
│   │   ├── ViewNews/
│   │   │   ├── ViewNewsDto.cs
│   │   │   ├── ViewNewsQuery.cs
│   │   │   └── ViewNewsQueryHandler.cs
│   │   ├── ViewNotifications/
│   │   │   ├── ViewNotificationsDto.cs
│   │   │   ├── ViewNotificationsQuery.cs
│   │   │   └── ViewNotificationsQueryHandler.cs
│   │   ├── ViewPartners/
│   │   │   ├── ViewPartnersDto.cs
│   │   │   ├── ViewPartnersQuery.cs
│   │   │   └── ViewPartnersQueryHandler.cs
│   │   └── ViewPolicyAndTerms/
│   │       ├── ViewPolicyAndTermsDto.cs
│   │       ├── ViewPolicyAndTermsQuery.cs
│   │       └── ViewPolicyAndTermsQueryHandler.cs
│   └── Rules/
│       └── README.md
├── Reports/
│   ├── Commands/
│   │   └── ExportStatisticsReport/
│   │       ├── ExportStatisticsReportCommand.cs
│   │       ├── ExportStatisticsReportCommandHandler.cs
│   │       ├── ExportStatisticsReportCommandValidator.cs
│   │       └── ExportStatisticsReportResponse.cs
│   └── Queries/
│       ├── FilterDashboardByTime/
│       │   ├── FilterDashboardByTimeDto.cs
│       │   ├── FilterDashboardByTimeQuery.cs
│       │   └── FilterDashboardByTimeQueryHandler.cs
│       └── ViewDashboardStatistics/
│           ├── ViewDashboardStatisticsDto.cs
│           ├── ViewDashboardStatisticsQuery.cs
│           └── ViewDashboardStatisticsQueryHandler.cs
├── Roles/
│   ├── Commands/
│   │   ├── ConfigureRolePermissions/
│   │   │   ├── ConfigureRolePermissionsCommand.cs
│   │   │   ├── ConfigureRolePermissionsCommandHandler.cs
│   │   │   ├── ConfigureRolePermissionsCommandValidator.cs
│   │   │   └── ConfigureRolePermissionsResponse.cs
│   │   ├── CreateNewRole/
│   │   │   ├── CreateNewRoleCommand.cs
│   │   │   ├── CreateNewRoleCommandHandler.cs
│   │   │   ├── CreateNewRoleCommandValidator.cs
│   │   │   └── CreateNewRoleResponse.cs
│   │   ├── DisableAndDeleteRole/
│   │   │   ├── DisableAndDeleteRoleCommand.cs
│   │   │   ├── DisableAndDeleteRoleCommandHandler.cs
│   │   │   ├── DisableAndDeleteRoleCommandValidator.cs
│   │   │   └── DisableAndDeleteRoleResponse.cs
│   │   └── UpdateRoleDetails/
│   │       ├── UpdateRoleDetailsCommand.cs
│   │       ├── UpdateRoleDetailsCommandHandler.cs
│   │       ├── UpdateRoleDetailsCommandValidator.cs
│   │       └── UpdateRoleDetailsResponse.cs
│   └── Queries/
│       └── ViewRoleList/
│           ├── ViewRoleListDto.cs
│           ├── ViewRoleListQuery.cs
│           └── ViewRoleListQueryHandler.cs
├── DependencyInjection.cs
└── PEMS.Application.csproj
```

### 2.4 PEMS.Domain

```txt
backend/PEMS.Domain/
├── Common/
│   ├── AuditableEntity.cs
│   ├── BaseEntity.cs
│   ├── DomainEvent.cs
│   └── SoftDeleteEntity.cs
├── Entities/
│   ├── AgendaTemplates/
│   │   └── AgendaTemplate.cs
│   ├── ApiIntegrations/
│   │   ├── ApiConfiguration.cs
│   │   ├── ApiRequestLog.cs
│   │   └── ApiUsageQuota.cs
│   ├── Calendar/
│   │   └── CalendarEvent.cs
│   ├── Campuses/
│   │   └── Campus.cs
│   ├── Delegations/
│   │   ├── VisitAgenda.cs
│   │   ├── VisitGuestMember.cs
│   │   ├── VisitLogisticsItem.cs
│   │   ├── VisitParticipant.cs
│   │   ├── VisitRequest.cs
│   │   ├── VisitRequestCampus.cs
│   │   └── VisitStatusLog.cs
│   ├── Departments/
│   │   └── Department.cs
│   ├── Documents/
│   │   ├── Document.cs
│   │   └── UploadedFile.cs
│   ├── Emails/
│   │   ├── EmailTemplate.cs
│   │   └── SentEmail.cs
│   ├── Faqs/
│   │   └── Faq.cs
│   ├── Feedbacks/
│   │   └── Feedback.cs
│   ├── Galleries/
│   │   ├── Gallery.cs
│   │   ├── GalleryImage.cs
│   │   └── PhotoFaceTag.cs
│   ├── Minutes/
│   │   └── Minute.cs
│   ├── News/
│   │   ├── News.cs
│   │   └── NewsTranslation.cs
│   ├── Notifications/
│   │   └── Notification.cs
│   ├── Partners/
│   │   ├── Partner.cs
│   │   └── Partnercontact.cs
│   ├── PublicContents/
│   │   └── PublicContent.cs
│   └── Users/
│       ├── AuditLog.cs
│       ├── LoginLog.cs
│       ├── OtpToken.cs
│       ├── Permission.cs
│       ├── Role.cs
│       ├── RolePermission.cs
│       ├── SecurityEvent.cs
│       ├── User.cs
│       ├── UserAuthProvider.cs
│       └── UserSession.cs
├── Enums/
│   ├── AccountStatus.cs
│   ├── ApiIntegrationStatus.cs
│   ├── CampusStatus.cs
│   ├── DelegationStatus.cs
│   ├── DepartmentStatus.cs
│   ├── FaqVisibilityStatus.cs
│   ├── NewsStatus.cs
│   ├── PermissionCode.cs
│   ├── UserRoleCode.cs
│   └── VisitRequestStatus.cs
├── Events/
│   ├── AccountCreatedEvent.cs
│   ├── DelegationClosedEvent.cs
│   ├── NewsApprovedEvent.cs
│   ├── ResourceRequestApprovedEvent.cs
│   ├── VisitRequestApprovedEvent.cs
│   └── VisitRequestSubmittedEvent.cs
├── ValueObjects/
│   ├── Address.cs
│   ├── DateRange.cs
│   ├── EmailAddress.cs
│   ├── FileMetadata.cs
│   └── PhoneNumber.cs
└── PEMS.Domain.csproj
```

> **Domain không có** thư mục `Interfaces/` hay `Services/` (Domain services). Interface repository/service nằm ở `PEMS.Application/Common/Interfaces`.

### 2.5 PEMS.Infrastructure

```txt
backend/PEMS.Infrastructure/
├── Email/
│   ├── EmailService.cs
│   ├── EmailTemplateRenderer.cs
│   └── SmtpEmailSender.cs
├── ExternalServices/
│   ├── ApiClient/
│   │   └── ExternalApiClient.cs
│   ├── Calendar/
│   │   └── CalendarIntegrationService.cs
│   ├── FaceRecognition/
│   │   └── FaceRecognitionService.cs
│   └── Ocr/
│       └── OcrService.cs
├── FileStorage/
│   ├── CloudFileStorageService.cs
│   ├── FileStorageService.cs
│   ├── FileValidationService.cs
│   ├── LocalFileStorageService.cs
│   └── VirusScanService.cs
├── Idempotency/
│   └── IdempotencyService.cs
├── Identity/
│   ├── CurrentUserService.cs
│   ├── JwtTokenService.cs
│   ├── NotificationService.cs
│   ├── OwnershipChecker.cs
│   ├── PasswordHasher.cs
│   ├── PermissionChecker.cs
│   └── RefreshTokenStore.cs
├── Logging/
│   ├── ApiRequestLogService.cs
│   └── AuditLogService.cs
├── Migrations/
│   ├── 20260617130132_CheckAuthSchemaMapping.cs
│   ├── 20260617130132_CheckAuthSchemaMapping.Designer.cs
│   └── ApplicationDbContextModelSnapshot.cs
├── Persistence/
│   ├── Configurations/
│   │   └── UserConfiguration.cs
│   ├── Migrations/
│   │   └── MigrationScript.cs
│   ├── Repositories/
│   │   ├── CampusRepository.cs
│   │   ├── DelegationRepository.cs
│   │   ├── DocumentRepository.cs
│   │   ├── GenericRepository.cs
│   │   ├── PartnerRepository.cs
│   │   ├── ReportRepository.cs
│   │   └── UserRepository.cs
│   ├── Seed/
│   │   ├── AdminAccountSeed.cs
│   │   ├── CampusSeed.cs
│   │   ├── PermissionMatrixSeed.cs
│   │   ├── PermissionSeed.cs
│   │   └── RoleSeed.cs
│   ├── ApplicationDbContext.cs
│   └── ApplicationDbContextFactory.cs
├── RateLimiting/
│   ├── InMemoryRateLimitStore.cs
│   ├── RateLimitService.cs
│   └── RedisRateLimitStore.cs
├── DependencyInjection.cs
└── PEMS.Infrastructure.csproj
```

### 2.6 database

```txt
database/
├── migrations/
│   └── README.md
├── scripts/
│   └── pems_full.sql
├── seed/
│   ├── campuses.sql
│   ├── permission_matrix.sql
│   ├── permissions.sql
│   └── roles.sql
└── README.md
```

### 2.7 docs

```txt
docs/
├── api/
│   ├── API_ROUTE_CONVENTION.md
│   ├── API_SPECIFICATION.md
│   └── FRONTEND_BACKEND_CONTRACT_GAP.md
├── architecture/
│   ├── BACKEND_USE_CASE_CLASS_BLUEPRINT.md
│   ├── CLEAN_ARCHITECTURE.md
│   └── PROJECT_STRUCTURE_FULL.md
├── database/
│   ├── DATABASE_DEPLOYMENT.md
│   └── DATABASE_SCHEMA.md
├── permissions/
│   ├── PERMISSION_MATRIX.md
│   └── PERMISSION_RULES.md
├── use-cases/
│   ├── USE_CASE_LIST.md
│   └── USE_CASE_NOTES.md
├── PEMS_AI_Refactor_Project_Structure_Prompt.md
├── PROJECT_OVERVIEW.md
├── Technology.md
└── VISITOR_MANAGEMENT_SYSTEM.md
```

### 2.8 frontend

```txt
frontend/
└── pems-react/
    ├── scripts/
    │   ├── applet_update.js
    │   ├── applet_update_contact.js
    │   ├── applet_update_emerald.js
    │   ├── applet_update_visit_3.js
    │   ├── applet_update_visit_4.js
    │   ├── applet_update_vp.js
    │   ├── transform.js
    │   ├── update_ho.js
    │   ├── update_linter.js
    │   ├── update_visit_2.js
    │   ├── update_visit_3.js
    │   ├── update_visit_4.js
    │   └── update_vp.js
    ├── src/
    │   ├── assets/
    │   │   ├── Avatar/            (1 file media: AvatarDefault.png)
    │   │   ├── FPTbanner_visit/   (8 file ảnh .png/.jpg)
    │   │   ├── images/            (7 file ảnh .png/.jpg/.svg)
    │   │   ├── img_visit_detail/  (20 file ảnh .jpg)
    │   │   └── Logo/              (18 file ảnh .png/.jpg)
    │   ├── components/
    │   │   ├── dashboard/
    │   │   │   ├── NotificationBell.tsx
    │   │   │   └── Sidebar.tsx
    │   │   ├── home/
    │   │   │   ├── CTASection.tsx
    │   │   │   ├── HeroSection.tsx
    │   │   │   ├── NewsSection.tsx
    │   │   │   ├── PartnersSection.tsx
    │   │   │   └── StatsSection.tsx
    │   │   ├── layout/
    │   │   │   ├── DashboardLayout.tsx
    │   │   │   ├── Footer.tsx
    │   │   │   └── Header.tsx
    │   │   ├── modals/
    │   │   │   ├── LoginModal.tsx
    │   │   │   ├── SearchPopup.tsx
    │   │   │   ├── VisitDetailsModal.tsx
    │   │   │   └── VisitingFormPopup.tsx
    │   │   └── partners/
    │   │       └── GlobeComponent.tsx
    │   ├── features/
    │   │   ├── account-management/
    │   │   │   ├── adapters/accountManagementAdapter.ts
    │   │   │   ├── api/accountManagementApi.ts
    │   │   │   ├── hooks/useAccountManagement.ts
    │   │   │   └── types/accountManagement.types.ts
    │   │   ├── agenda-templates/
    │   │   │   ├── adapters/agendaTemplatesAdapter.ts
    │   │   │   ├── api/agendaTemplatesApi.ts
    │   │   │   ├── hooks/useAgendaTemplates.ts
    │   │   │   └── types/agendaTemplates.types.ts
    │   │   ├── api-management/
    │   │   │   ├── adapters/apiManagementAdapter.ts
    │   │   │   ├── api/apiManagementApi.ts
    │   │   │   ├── hooks/useApiManagement.ts
    │   │   │   └── types/apiManagement.types.ts
    │   │   ├── authentication/
    │   │   │   ├── adapters/authenticationAdapter.ts
    │   │   │   ├── api/authenticationApi.ts
    │   │   │   ├── components/
    │   │   │   │   ├── DualPortalLoginForms.tsx
    │   │   │   │   ├── InternalLoginForm.tsx
    │   │   │   │   └── VisitorLoginForm.tsx
    │   │   │   ├── hooks/useAuthentication.ts
    │   │   │   └── types/authentication.types.ts
    │   │   ├── calendars/
    │   │   │   ├── adapters/calendarsAdapter.ts
    │   │   │   ├── api/calendarsApi.ts
    │   │   │   ├── hooks/useCalendars.ts
    │   │   │   └── types/calendars.types.ts
    │   │   ├── campus-management/
    │   │   │   ├── adapters/campusManagementAdapter.ts
    │   │   │   ├── api/campusManagementApi.ts
    │   │   │   ├── hooks/useCampusManagement.ts
    │   │   │   └── types/campusManagement.types.ts
    │   │   ├── delegations/
    │   │   │   ├── adapters/delegationsAdapter.ts
    │   │   │   ├── api/delegationsApi.ts
    │   │   │   ├── hooks/useDelegations.ts
    │   │   │   └── types/delegations.types.ts
    │   │   ├── department-management/
    │   │   │   ├── adapters/departmentManagementAdapter.ts
    │   │   │   ├── api/departmentManagementApi.ts
    │   │   │   ├── hooks/useDepartmentManagement.ts
    │   │   │   └── types/departmentManagement.types.ts
    │   │   ├── documents/
    │   │   │   ├── adapters/documentsAdapter.ts
    │   │   │   ├── api/documentsApi.ts
    │   │   │   ├── hooks/useDocuments.ts
    │   │   │   └── types/documents.types.ts
    │   │   ├── emails/
    │   │   │   ├── adapters/emailsAdapter.ts
    │   │   │   ├── api/emailsApi.ts
    │   │   │   ├── hooks/useEmails.ts
    │   │   │   └── types/emails.types.ts
    │   │   ├── faq-management/
    │   │   │   ├── adapters/faqManagementAdapter.ts
    │   │   │   ├── api/faqManagementApi.ts
    │   │   │   ├── hooks/useFaqManagement.ts
    │   │   │   └── types/faqManagement.types.ts
    │   │   ├── feedbacks/
    │   │   │   ├── adapters/feedbacksAdapter.ts
    │   │   │   ├── api/feedbacksApi.ts
    │   │   │   ├── hooks/useFeedbacks.ts
    │   │   │   └── types/feedbacks.types.ts
    │   │   ├── gallery-management/
    │   │   │   ├── adapters/galleryManagementAdapter.ts
    │   │   │   ├── api/galleryManagementApi.ts
    │   │   │   ├── hooks/useGalleryManagement.ts
    │   │   │   └── types/galleryManagement.types.ts
    │   │   ├── meeting-minutes/
    │   │   │   ├── adapters/meetingMinutesAdapter.ts
    │   │   │   ├── api/meetingMinutesApi.ts
    │   │   │   ├── hooks/useMeetingMinutes.ts
    │   │   │   └── types/meetingMinutes.types.ts
    │   │   ├── news-management/
    │   │   │   ├── adapters/newsManagementAdapter.ts
    │   │   │   ├── api/newsManagementApi.ts
    │   │   │   ├── hooks/useNewsManagement.ts
    │   │   │   └── types/newsManagement.types.ts
    │   │   ├── notifications/
    │   │   │   ├── adapters/notificationsAdapter.ts
    │   │   │   ├── api/notificationsApi.ts
    │   │   │   ├── hooks/useNotifications.ts
    │   │   │   └── types/notifications.types.ts
    │   │   ├── partners/
    │   │   │   ├── adapters/partnersAdapter.ts
    │   │   │   ├── api/partnersApi.ts
    │   │   │   ├── hooks/usePartners.ts
    │   │   │   └── types/partners.types.ts
    │   │   ├── profile/
    │   │   │   ├── adapters/profileAdapter.ts
    │   │   │   ├── api/profileApi.ts
    │   │   │   ├── hooks/useProfile.ts
    │   │   │   └── types/profile.types.ts
    │   │   ├── public-content/
    │   │   │   ├── adapters/publicContentAdapter.ts
    │   │   │   ├── api/publicContentApi.ts
    │   │   │   ├── hooks/usePublicContent.ts
    │   │   │   └── types/publicContent.types.ts
    │   │   ├── reports/
    │   │   │   ├── adapters/reportsAdapter.ts
    │   │   │   ├── api/reportsApi.ts
    │   │   │   ├── hooks/useReports.ts
    │   │   │   └── types/reports.types.ts
    │   │   └── role-permission-management/
    │   │       ├── adapters/rolePermissionManagementAdapter.ts
    │   │       ├── api/rolePermissionManagementApi.ts
    │   │       ├── hooks/useRolePermissionManagement.ts
    │   │       └── types/rolePermissionManagement.types.ts
    │   ├── pages/
    │   │   ├── dashboard/
    │   │   │   ├── accounts/
    │   │   │   │   └── AccountManagement.tsx
    │   │   │   ├── apis/
    │   │   │   │   └── ApiManagement.tsx
    │   │   │   ├── campus/
    │   │   │   │   ├── CampusDetail.tsx
    │   │   │   │   └── CampusManagement.tsx
    │   │   │   ├── departments/
    │   │   │   │   ├── DepartmentDetailDashboard.tsx
    │   │   │   │   ├── DepartmentManagement.tsx
    │   │   │   │   ├── TaskDetail.tsx
    │   │   │   │   └── TaskInvitationDetail.tsx
    │   │   │   ├── documents/
    │   │   │   │   └── DocumentManagement.tsx
    │   │   │   ├── emails/
    │   │   │   │   ├── CreateEmail.tsx
    │   │   │   │   ├── EditEmail.tsx
    │   │   │   │   ├── EmailDetail.tsx
    │   │   │   │   ├── EmailManagement.tsx
    │   │   │   │   ├── SendEmailTab.tsx
    │   │   │   │   └── SentEmailDetail.tsx
    │   │   │   ├── faq/
    │   │   │   │   ├── FAQDetail.tsx
    │   │   │   │   └── FAQManagement.tsx
    │   │   │   ├── feedback/
    │   │   │   │   ├── FeedbackDetail.tsx
    │   │   │   │   ├── FeedbackManagement.tsx
    │   │   │   │   └── mockData.ts
    │   │   │   ├── gallery/
    │   │   │   │   ├── GalleryManagement.tsx
    │   │   │   │   └── LocationManagement.tsx
    │   │   │   ├── home/
    │   │   │   │   ├── AdminDashboardView.tsx
    │   │   │   │   ├── DashboardHome.tsx
    │   │   │   │   ├── HODashboardView.tsx
    │   │   │   │   └── SharedDashboardView.tsx
    │   │   │   ├── minutes/
    │   │   │   │   └── MinuteManagement.tsx
    │   │   │   ├── news/
    │   │   │   │   ├── CreateNews.tsx
    │   │   │   │   ├── EditNews.tsx
    │   │   │   │   ├── NewsDetailDashboard.tsx
    │   │   │   │   └── NewsManagement.tsx
    │   │   │   ├── partners/
    │   │   │   │   ├── CreatePartner.tsx
    │   │   │   │   ├── PartnerDetail.tsx
    │   │   │   │   └── PartnerManagement.tsx
    │   │   │   ├── permissions/
    │   │   │   │   └── PermissionManagement.tsx
    │   │   │   ├── profile/
    │   │   │   │   └── Profile.tsx
    │   │   │   ├── reports/
    │   │   │   │   ├── DeptReportManagement.tsx
    │   │   │   │   ├── ReportManagement.tsx
    │   │   │   │   └── mockReportData.ts
    │   │   │   └── visit/
    │   │   │       ├── AgendaTemplateManagement.tsx
    │   │   │       ├── CreateVisitRequest.tsx
    │   │   │       ├── HoVisitProcessDetail.tsx
    │   │   │       ├── VisitAfterTab.tsx
    │   │   │       ├── VisitDuringTab.tsx
    │   │   │       ├── VisitProcess.tsx
    │   │   │       ├── VisitRequestDetail.tsx
    │   │   │       └── VisitRequestManagement.tsx
    │   │   ├── CampusDetailVisitPage.tsx
    │   │   ├── FAQPage.tsx
    │   │   ├── HomePage.tsx
    │   │   ├── NewsDetailPage.tsx
    │   │   ├── NewsPage.tsx
    │   │   ├── PartnerDetailPage.tsx
    │   │   ├── PartnersPage.tsx
    │   │   └── VisitFPTUPage.tsx
    │   ├── shared/
    │   │   ├── api/
    │   │   │   ├── authInterceptor.ts
    │   │   │   ├── endpoints.ts
    │   │   │   ├── errorHandler.ts
    │   │   │   └── httpClient.ts
    │   │   ├── auth/
    │   │   │   ├── ProtectedRoute.tsx
    │   │   │   ├── RoleGuard.tsx
    │   │   │   ├── authStorage.ts
    │   │   │   └── permissionChecker.ts
    │   │   ├── constants/
    │   │   │   ├── appRoutes.ts
    │   │   │   ├── permissions.ts
    │   │   │   ├── roles.ts
    │   │   │   ├── statusCodes.ts
    │   │   │   └── ucCodes.ts
    │   │   ├── hooks/
    │   │   │   ├── useApiError.ts
    │   │   │   ├── useAuth.ts
    │   │   │   ├── useDebounce.ts
    │   │   │   ├── usePagination.ts
    │   │   │   └── usePermission.ts
    │   │   ├── types/
    │   │   │   ├── api.types.ts
    │   │   │   ├── auth.types.ts
    │   │   │   ├── common.types.ts
    │   │   │   ├── pagination.types.ts
    │   │   │   └── permission.types.ts
    │   │   └── utils/
    │   │       ├── dateUtils.ts
    │   │       ├── fileUtils.ts
    │   │       ├── formatUtils.ts
    │   │       ├── routeUtils.ts
    │   │       └── validationUtils.ts
    │   ├── App.tsx
    │   ├── index.css
    │   ├── main.tsx
    │   ├── types.ts
    │   └── vite-env.d.ts
    ├── .env
    ├── .env.example
    ├── .gitignore
    ├── README.md
    ├── fix_process.cjs
    ├── fix_responsive.cjs
    ├── index.html
    ├── metadata.json
    ├── out.txt
    ├── package-lock.json
    ├── package.json
    ├── transform.cjs
    ├── transform_editable.cjs
    ├── transform_setup_editable.cjs
    ├── tsconfig.json
    ├── updateHeaders.cjs
    └── vite.config.ts
```

---

## 3. Phân tích cấu trúc backend

* **PEMS.Domain** — tầng lõi, không phụ thuộc tầng khác. Gồm `Common/` (base class), `Entities/` (17 nhóm bounded context), `Enums/` (10), `Events/` (6), `ValueObjects/` (5). Không có `Interfaces/`/`Services/` riêng.
* **PEMS.Application** — business logic theo CQRS (MediatR) + FluentValidation, chỉ tham chiếu Domain. Mỗi module có `Commands/<UseCase>/` (Command + Handler + Validator + Response) và `Queries/<UseCase>/` (Query + Handler + Dto); vài module có thêm `Dtos/`, `Mappings/`, `Rules/`. `Common/` chứa `Behaviours/`, `Exceptions/`, `Interfaces/` (22), `Models/`, `Security/`.
* **PEMS.Infrastructure** — implement interface của Application: `Persistence/` (DbContext + Factory, `Configurations/`, `Migrations/`, `Repositories/`, `Seed/`), `Identity/`, `Email/`, `FileStorage/`, `ExternalServices/`, `Logging/`, `Idempotency/`, `RateLimiting/`.
* **PEMS.Api** — tầng trình bày: `Controllers/` (20), `Contracts/`, `Extensions/` (6), `Filters/` (4), `Middleware/` (5), `Program.cs`, `appsettings.json`.

## 4. Phân tích cấu trúc frontend

React 19 + TypeScript + Vite. `src/` tổ chức theo: `assets/` (media), `components/` (dashboard/home/layout/modals/partners), `features/` (21 module, mỗi module `adapters/ api/ hooks/ types/`), `pages/` (trang public + `pages/dashboard/*` theo module), `shared/` (`api/ auth/ constants/ hooks/ types/ utils/`). Root project chứa config Vite/TS, `package.json`, và các script `.cjs/.js` tiện ích.

## 5. Phân tích database folder

`scripts/pems_full.sql` là schema chính (MySQL 8.0); `seed/` chứa 4 file SQL khởi tạo (campuses, permissions, permission_matrix, roles); `migrations/` chỉ có `README.md` (chưa có file migration thật); `README.md` hướng dẫn chung.

## 6. Phân tích docs folder

Gồm `api/` (3), `architecture/` (3 — trong đó file này là bản mới nhất), `database/` (2), `permissions/` (2), `use-cases/` (2) và 4 tài liệu tổng quan ở gốc `docs/`.

## 7. Nhận xét sau khi cập nhật

* **Tài liệu phản ánh đúng cấu trúc thật hiện tại** — cây thư mục được dựng từ `git ls-files` + quét đĩa, không còn dùng bản rút gọn cũ.
* **Backend chưa implement đầy đủ → chưa kết luận build pass.** `.csproj` target `net8.0`. Chưa chạy `dotnet build` trong lần cập nhật này.
* **Domain là nguồn tham chiếu chính.** Domain đã được tái cấu trúc (thêm `Calendar`, `PublicContents`, mở rộng `Users`; tách `ApiIntegrations` thành `ApiConfiguration`/`ApiRequestLog`/`ApiUsageQuota`). Lưu ý: Domain **không có** entity `Report` là **đúng thiết kế** — Reports là read-model/statistics, không phải entity lưu trữ (xem mục pending bên dưới).
* **Các phần có thể còn pending implementation / lệch với Domain mới:**
  * **Reports module không bị lệch với Domain.** Reports trong PEMS là **read-model/statistics/dashboard module**, không phải entity lưu trữ độc lập. Vì vậy Domain **không cần** entity `Report` và database **không cần** bảng `reports`. `ReportsController` và `PEMS.Application/Reports` có thể giữ để phục vụ API thống kê, lọc dashboard theo thời gian và export dữ liệu tức thời. Nếu Infrastructure còn `ReportRepository` thì chỉ cần **pending rename/refactor** sau thành `StatisticsReadService`, `DashboardReadService` hoặc `ReportReadService` để tránh hiểu nhầm là repository của bảng/entity `Report`.
  * `MeetingMinutes`, `Delegations` (Application) tham chiếu khái niệm có thể đã đổi tên trong Domain (`Minute`, các entity Visit*).
  * `Infrastructure/Persistence/Configurations/` mới chỉ có `UserConfiguration.cs` cho rất nhiều entity → phần mapping EF Core còn dang dở.
  * `database/migrations/` trống (chỉ README) → chưa có migration khớp Domain mới; `pems_full.sql` có thể lệch schema.
  * Nhiều thư mục `Rules/` và `Dtos/` chỉ chứa `README.md` (placeholder) → business rule/DTO chưa hiện thực.
  * `tests/`: chỉ `PEMS.ArchitectureTests` có `.csproj`; `PEMS.ApplicationTests`, `PEMS.IntegrationTests`, `PEMS.UnitTests` không có `.csproj` trên đĩa.
  * `frontend/pems-react/out.txt` là file tracked trông giống output tạm — nên rà soát có nên giữ.

## 8. Checklist hoàn thành

* [x] Đã scan cấu trúc thật từ repository (`git ls-files` + quét đĩa cho `.vscode`)
* [x] Đã thay cây thư mục rút gọn bằng cây full, sâu tới từng file/class (mục 2.1–2.8)
* [x] Không còn `# omitted`, `...`, hay ghi tắt kiểu `Commands/ + Queries/` trong backend
* [x] Đã liệt kê đầy đủ PEMS.Api / Application / Domain / Infrastructure tới file cuối
* [x] Đã liệt kê đầy đủ từng use case Application (Command/Handler/Validator/Response, Query/Handler/Dto, Mapping, Rule)
* [x] Đã liệt kê đầy đủ Domain (Common, Entities, Enums, Events, ValueObjects)
* [x] Đã liệt kê đầy đủ Infrastructure (Persistence, Configurations, Repositories, Seed, Migrations, Identity, Email, FileStorage, ExternalServices, Logging, RateLimiting)
* [x] Đã liệt kê sâu frontend `src`/`scripts`/config; chỉ gộp số lượng với file media asset (ảnh)
* [x] Đã loại bỏ folder build/rác (`bin/`, `obj/`, `node_modules/`, `.git/`, `.vs/`, …)
* [x] Không thêm file/folder không tồn tại, không bịa class, không tạo folder mới
* [x] Không tạo file backup; chỉ sửa `PROJECT_STRUCTURE_FULL.md`
