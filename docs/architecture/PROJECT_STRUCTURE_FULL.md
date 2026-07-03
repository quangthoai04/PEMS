# PEMS Project Structure (Full Tree)

- File này phản ánh cấu trúc thư mục **thật hiện tại** của project PEMS.
- Được cập nhật sau khi quét lại source code (lần quét: cấu trúc thật trên nhánh `Canh-Iter1`, ngày 2026-07-02).
- Không bao gồm các thư mục build/generated như `node_modules`, `dist`, `bin`, `obj`, `.vs`, `.git`, `.tmp-build`, `.runlogs`... Những thư mục này được đánh dấu `[excluded]` tại vị trí xuất hiện và **không** mở rộng nội dung bên trong.
- Với file môi trường (`.env`), chỉ ghi nhận **tên file** trong cây; không đọc/không in nội dung secret.

## 1. Scope

Tài liệu này bao gồm:

- **Backend Clean Architecture** — `backend/PEMS.Api`, `backend/PEMS.Application`, `backend/PEMS.Domain`, `backend/PEMS.Infrastructure`, kèm 2 project tiện ích nhỏ `backend/CheckDb` và `backend/JsonTest`, cùng 2 file scratch `backend/handlers.txt` / `backend/handlers_utf8.txt`.
- **Frontend React** — `frontend/pems-react` (Vite + React + TypeScript, kiến trúc feature-based: `src/features/`, `src/pages/`, `src/components/`, `src/shared/`, kèm thư mục `scripts/` chứa các applet codemod).
- **Database scripts** — Không còn thư mục `database/` ở root. Toàn bộ SQL fresh-create v10, project `DbSeeder`, và các script seed/cleanup hiện nằm trong `docs/database/scripts/`. Dự án dùng SQL fresh-create, **không có** thư mục EF migrations.
- **Documentation** — `docs/` (kiến trúc, use cases, permissions, database, authentication, và nhiều thư mục prompt/spec theo module).
- **Tests** — `tests/` (PEMS.ApplicationTests, PEMS.ArchitectureTests, PEMS.IntegrationTests, PEMS.UnitTests + thư mục `http` test files và project scratch `temp_bcrypt`).
- **Root configuration files** — `.gitignore`, `.gitattributes`, `PEMS.slnx`, `README.md`, thư mục `scripts/` (guard script), và thư mục scratch `temp_hash/`.

## 2. Directory Tree

```text
PEMS/
├── .claude/
│   ├── settings.json
│   └── settings.local.json
├── .git/   [excluded]
├── .runlogs/   [excluded]
├── .vs/   [excluded]
├── .vscode/
│   ├── launch.json
│   └── tasks.json
├── backend/
│   ├── CheckDb/
│   │   ├── bin/   [excluded]
│   │   ├── obj/   [excluded]
│   │   ├── CheckDb.csproj
│   │   └── Program.cs
│   ├── JsonTest/
│   │   ├── bin/   [excluded]
│   │   ├── obj/   [excluded]
│   │   ├── JsonTest.csproj
│   │   └── Program.cs
│   ├── PEMS.Api/
│   │   ├── .tmp-build/   [excluded]
│   │   ├── App_Data/
│   │   │   └── uploads/
│   │   │       └── email_attachment/
│   │   │           └── 2026/
│   │   │               └── 06/
│   │   │                   └── fe8cd677dd8b4a5f975dac70ca1e8fdc.png
│   │   ├── Contracts/
│   │   │   ├── ApiResponse.cs
│   │   │   └── ApiRoutes.cs
│   │   ├── Controllers/
│   │   │   ├── AccountsController.cs
│   │   │   ├── AgendaTemplatesController.cs
│   │   │   ├── ApiIntegrationsController.cs
│   │   │   ├── AuthenticationController.cs
│   │   │   ├── CalendarsController.cs
│   │   │   ├── CampusesController.cs
│   │   │   ├── DashboardController.cs
│   │   │   ├── DelegationsController.cs
│   │   │   ├── DepartmentReceptionTasksController.cs
│   │   │   ├── DepartmentsController.cs
│   │   │   ├── DocumentsController.cs
│   │   │   ├── EmailTemplatesController.cs
│   │   │   ├── EmailsController.cs
│   │   │   ├── FaqsController.cs
│   │   │   ├── FeedbacksController.cs
│   │   │   ├── FilesController.cs
│   │   │   ├── GalleriesController.cs
│   │   │   ├── GoogleDriveOAuthController.cs
│   │   │   ├── MeetingMinutesController.cs
│   │   │   ├── NewsController.cs
│   │   │   ├── NotificationsController.cs
│   │   │   ├── PartnersController.cs
│   │   │   ├── ProfilesController.cs
│   │   │   ├── PublicContentController.cs
│   │   │   ├── PublicEmailActionsController.cs
│   │   │   ├── PublicPartnersController.cs
│   │   │   ├── PublicVisitFptuController.cs
│   │   │   ├── ReportsController.cs
│   │   │   ├── VisitInvitationsController.cs
│   │   │   └── VisitRequestsController.cs
│   │   ├── Email/
│   │   │   └── EmailActionHtmlPages.cs
│   │   ├── Extensions/
│   │   │   ├── AuthenticationExtensions.cs
│   │   │   ├── AuthorizationExtensions.cs
│   │   │   ├── CorsExtensions.cs
│   │   │   ├── RateLimitingExtensions.cs
│   │   │   ├── ServiceCollectionExtensions.cs
│   │   │   └── SwaggerExtensions.cs
│   │   ├── Filters/
│   │   │   ├── FileUploadValidationFilter.cs
│   │   │   ├── IdempotencyFilter.cs
│   │   │   ├── RoleAuthorizeAttribute.cs
│   │   │   └── ValidationFilter.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   ├── RateLimitMiddleware.cs
│   │   │   ├── RequestLoggingMiddleware.cs
│   │   │   ├── SecurityHeadersMiddleware.cs
│   │   │   └── SessionValidationMiddleware.cs
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── bin/   [excluded]
│   │   ├── obj/   [excluded]
│   │   ├── PEMS.Api.csproj
│   │   ├── Pems_WebAPI.http
│   │   ├── Program.cs
│   │   ├── appsettings.Development.example.json
│   │   ├── appsettings.Development.json
│   │   ├── appsettings.Production.json
│   │   ├── appsettings.json
│   │   └── backend_log.txt
│   ├── PEMS.Application/
│   │   ├── .tmp-build/   [excluded]
│   │   ├── Accounts/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateAccount/
│   │   │   │   │   ├── CreateAccountCommand.cs
│   │   │   │   │   ├── CreateAccountCommandHandler.cs
│   │   │   │   │   ├── CreateAccountCommandValidator.cs
│   │   │   │   │   └── CreateAccountResponse.cs
│   │   │   │   ├── ManageAccountStatus/
│   │   │   │   │   ├── ManageAccountStatusCommand.cs
│   │   │   │   │   ├── ManageAccountStatusCommandHandler.cs
│   │   │   │   │   ├── ManageAccountStatusCommandValidator.cs
│   │   │   │   │   └── ManageAccountStatusResponse.cs
│   │   │   │   ├── ReplaceStaffLeader/
│   │   │   │   │   ├── ReplaceStaffLeaderCommand.cs
│   │   │   │   │   ├── ReplaceStaffLeaderCommandHandler.cs
│   │   │   │   │   ├── ReplaceStaffLeaderCommandValidator.cs
│   │   │   │   │   └── ReplaceStaffLeaderResponse.cs
│   │   │   │   └── UpdateAccountRole/
│   │   │   │       ├── UpdateAccountRoleCommand.cs
│   │   │   │       ├── UpdateAccountRoleCommandHandler.cs
│   │   │   │       ├── UpdateAccountRoleCommandValidator.cs
│   │   │   │       └── UpdateAccountRoleResponse.cs
│   │   │   ├── Common/
│   │   │   │   ├── AccountErrorCodes.cs
│   │   │   │   ├── AccountListCriteriaRules.cs
│   │   │   │   ├── AccountListItemDto.cs
│   │   │   │   ├── AccountListQueryExecutor.cs
│   │   │   │   ├── AccountProvisioningRules.cs
│   │   │   │   ├── HoCampusAvailability.cs
│   │   │   │   ├── IAccountListCriteria.cs
│   │   │   │   ├── RelatedVisitorScope.cs
│   │   │   │   └── StaffLeaderAvailability.cs
│   │   │   └── Queries/
│   │   │       ├── GetCampusDepartments/
│   │   │       │   ├── GetCampusDepartmentsQuery.cs
│   │   │       │   └── GetCampusDepartmentsQueryHandler.cs
│   │   │       ├── HoCampusCheck/
│   │   │       │   ├── GetHoCampusCheckQuery.cs
│   │   │       │   └── GetHoCampusCheckQueryHandler.cs
│   │   │       ├── RelatedVisitors/
│   │   │       │   ├── GetRelatedVisitorDetailsQuery.cs
│   │   │       │   ├── GetRelatedVisitorDetailsQueryHandler.cs
│   │   │       │   ├── GetRelatedVisitorsQuery.cs
│   │   │       │   ├── GetRelatedVisitorsQueryHandler.cs
│   │   │       │   ├── RelatedVisitorAccountDetailDto.cs
│   │   │       │   └── RelatedVisitorAccountListItemDto.cs
│   │   │       ├── SearchandFilterAccounts/
│   │   │       │   ├── SearchandFilterAccountsQuery.cs
│   │   │       │   ├── SearchandFilterAccountsQueryHandler.cs
│   │   │       │   └── SearchandFilterAccountsQueryValidator.cs
│   │   │       ├── StaffLeaderAvailability/
│   │   │       │   ├── GetStaffLeaderAvailabilityQuery.cs
│   │   │       │   └── GetStaffLeaderAvailabilityQueryHandler.cs
│   │   │       ├── StaffLeaderReplacementPreview/
│   │   │       │   ├── GetStaffLeaderReplacementPreviewQuery.cs
│   │   │       │   └── GetStaffLeaderReplacementPreviewQueryHandler.cs
│   │   │       ├── ViewAccountDetails/
│   │   │       │   ├── ViewAccountDetailsDto.cs
│   │   │       │   ├── ViewAccountDetailsQuery.cs
│   │   │       │   └── ViewAccountDetailsQueryHandler.cs
│   │   │       ├── ViewAccountList/
│   │   │       │   ├── ViewAccountListQuery.cs
│   │   │       │   ├── ViewAccountListQueryHandler.cs
│   │   │       │   └── ViewAccountListQueryValidator.cs
│   │   │       └── ViewAccountStatistics/
│   │   │           ├── ViewAccountStatisticsDto.cs
│   │   │           ├── ViewAccountStatisticsQuery.cs
│   │   │           └── ViewAccountStatisticsQueryHandler.cs
│   │   ├── AgendaTemplates/
│   │   │   ├── Commands/
│   │   │   │   ├── ApplyAgendaTemplate/
│   │   │   │   │   ├── ApplyAgendaTemplateCommand.cs
│   │   │   │   │   ├── ApplyAgendaTemplateCommandHandler.cs
│   │   │   │   │   ├── ApplyAgendaTemplateCommandValidator.cs
│   │   │   │   │   └── ApplyAgendaTemplateResponse.cs
│   │   │   │   ├── CreateAgendaTemplate/
│   │   │   │   │   ├── CreateAgendaTemplateCommand.cs
│   │   │   │   │   ├── CreateAgendaTemplateCommandHandler.cs
│   │   │   │   │   ├── CreateAgendaTemplateCommandValidator.cs
│   │   │   │   │   └── CreateAgendaTemplateResponse.cs
│   │   │   │   ├── DeleteAgendaTemplate/
│   │   │   │   │   ├── DeleteAgendaTemplateCommand.cs
│   │   │   │   │   ├── DeleteAgendaTemplateCommandHandler.cs
│   │   │   │   │   ├── DeleteAgendaTemplateCommandValidator.cs
│   │   │   │   │   └── DeleteAgendaTemplateResponse.cs
│   │   │   │   ├── SetAgendaTemplateDefault/
│   │   │   │   │   ├── SetAgendaTemplateDefaultCommand.cs
│   │   │   │   │   ├── SetAgendaTemplateDefaultCommandHandler.cs
│   │   │   │   │   ├── SetAgendaTemplateDefaultCommandValidator.cs
│   │   │   │   │   └── SetAgendaTemplateDefaultResponse.cs
│   │   │   │   └── UpdateAgendaTemplate/
│   │   │   │       ├── UpdateAgendaTemplateCommand.cs
│   │   │   │       ├── UpdateAgendaTemplateCommandHandler.cs
│   │   │   │       ├── UpdateAgendaTemplateCommandValidator.cs
│   │   │   │       └── UpdateAgendaTemplateResponse.cs
│   │   │   ├── Common/
│   │   │   │   ├── AgendaDefaultResolver.cs
│   │   │   │   ├── AgendaTemplateAuthorization.cs
│   │   │   │   ├── AgendaTemplateContracts.cs
│   │   │   │   └── AgendaTemplateItemInputValidator.cs
│   │   │   └── Queries/
│   │   │       ├── GetAgendaSetupForInstance/
│   │   │       │   ├── GetAgendaSetupForInstanceDto.cs
│   │   │       │   ├── GetAgendaSetupForInstanceQuery.cs
│   │   │       │   └── GetAgendaSetupForInstanceQueryHandler.cs
│   │   │       ├── GetDefaultAgendaTemplate/
│   │   │       │   ├── GetDefaultAgendaTemplateDto.cs
│   │   │       │   ├── GetDefaultAgendaTemplateQuery.cs
│   │   │       │   └── GetDefaultAgendaTemplateQueryHandler.cs
│   │   │       ├── ViewAgendaTemplateDefaults/
│   │   │       │   ├── ViewAgendaTemplateDefaultsDto.cs
│   │   │       │   ├── ViewAgendaTemplateDefaultsQuery.cs
│   │   │       │   └── ViewAgendaTemplateDefaultsQueryHandler.cs
│   │   │       ├── ViewAgendaTemplateDetail/
│   │   │       │   ├── ViewAgendaTemplateDetailDto.cs
│   │   │       │   ├── ViewAgendaTemplateDetailQuery.cs
│   │   │       │   └── ViewAgendaTemplateDetailQueryHandler.cs
│   │   │       └── ViewAgendaTemplateList/
│   │   │           ├── ViewAgendaTemplateListDto.cs
│   │   │           ├── ViewAgendaTemplateListQuery.cs
│   │   │           └── ViewAgendaTemplateListQueryHandler.cs
│   │   ├── ApiIntegrations/
│   │   │   ├── Commands/
│   │   │   │   ├── ConfigureRequestLimit/
│   │   │   │   │   ├── ConfigureRequestLimitCommand.cs
│   │   │   │   │   ├── ConfigureRequestLimitCommandHandler.cs
│   │   │   │   │   ├── ConfigureRequestLimitCommandValidator.cs
│   │   │   │   │   └── ConfigureRequestLimitResponse.cs
│   │   │   │   ├── CreateAPIConfiguration/
│   │   │   │   │   ├── CreateAPIConfigurationCommand.cs
│   │   │   │   │   ├── CreateAPIConfigurationCommandHandler.cs
│   │   │   │   │   ├── CreateAPIConfigurationCommandValidator.cs
│   │   │   │   │   └── CreateAPIConfigurationResponse.cs
│   │   │   │   ├── DeleteAPIConfiguration/
│   │   │   │   │   ├── DeleteAPIConfigurationCommand.cs
│   │   │   │   │   ├── DeleteAPIConfigurationCommandHandler.cs
│   │   │   │   │   ├── DeleteAPIConfigurationCommandValidator.cs
│   │   │   │   │   └── DeleteAPIConfigurationResponse.cs
│   │   │   │   ├── ManageAPIStatus/
│   │   │   │   │   ├── ManageAPIStatusCommand.cs
│   │   │   │   │   ├── ManageAPIStatusCommandHandler.cs
│   │   │   │   │   ├── ManageAPIStatusCommandValidator.cs
│   │   │   │   │   └── ManageAPIStatusResponse.cs
│   │   │   │   ├── TestAPIConnection/
│   │   │   │   │   ├── TestAPIConnectionCommand.cs
│   │   │   │   │   ├── TestAPIConnectionCommandHandler.cs
│   │   │   │   │   ├── TestAPIConnectionCommandValidator.cs
│   │   │   │   │   └── TestAPIConnectionResponse.cs
│   │   │   │   └── UpdateAPIConfiguration/
│   │   │   │       ├── UpdateAPIConfigurationCommand.cs
│   │   │   │       ├── UpdateAPIConfigurationCommandHandler.cs
│   │   │   │       ├── UpdateAPIConfigurationCommandValidator.cs
│   │   │   │       └── UpdateAPIConfigurationResponse.cs
│   │   │   └── Queries/
│   │   │       ├── SearchAPILogs/
│   │   │       │   ├── SearchAPILogsDto.cs
│   │   │       │   ├── SearchAPILogsQuery.cs
│   │   │       │   └── SearchAPILogsQueryHandler.cs
│   │   │       ├── ViewAPIConfiguration/
│   │   │       │   ├── ViewAPIConfigurationDto.cs
│   │   │       │   ├── ViewAPIConfigurationQuery.cs
│   │   │       │   └── ViewAPIConfigurationQueryHandler.cs
│   │   │       └── ViewAPILogs/
│   │   │           ├── ViewAPILogsDto.cs
│   │   │           ├── ViewAPILogsQuery.cs
│   │   │           └── ViewAPILogsQueryHandler.cs
│   │   ├── Authentication/
│   │   │   ├── Commands/
│   │   │   │   ├── ForgotPassword/
│   │   │   │   │   ├── ForgotPasswordCommand.cs
│   │   │   │   │   ├── ForgotPasswordCommandHandler.cs
│   │   │   │   │   └── ForgotPasswordCommandValidator.cs
│   │   │   │   ├── LoginViaCredentials/
│   │   │   │   │   ├── LoginViaCredentialsCommand.cs
│   │   │   │   │   ├── LoginViaCredentialsCommandHandler.cs
│   │   │   │   │   └── LoginViaCredentialsCommandValidator.cs
│   │   │   │   ├── LoginViaFeid/
│   │   │   │   │   ├── LoginViaFeidCommand.cs
│   │   │   │   │   ├── LoginViaFeidCommandHandler.cs
│   │   │   │   │   └── LoginViaFeidCommandValidator.cs
│   │   │   │   ├── LoginViaSso/
│   │   │   │   │   ├── LoginViaSsoCommand.cs
│   │   │   │   │   ├── LoginViaSsoCommandHandler.cs
│   │   │   │   │   └── LoginViaSsoCommandValidator.cs
│   │   │   │   ├── Logout/
│   │   │   │   │   ├── LogoutCommand.cs
│   │   │   │   │   ├── LogoutCommandHandler.cs
│   │   │   │   │   └── LogoutCommandValidator.cs
│   │   │   │   ├── RefreshToken/
│   │   │   │   │   ├── RefreshTokenCommand.cs
│   │   │   │   │   ├── RefreshTokenCommandHandler.cs
│   │   │   │   │   └── RefreshTokenCommandValidator.cs
│   │   │   │   └── ResetPassword/
│   │   │   │       ├── ResetPasswordCommand.cs
│   │   │   │       ├── ResetPasswordCommandHandler.cs
│   │   │   │       └── ResetPasswordCommandValidator.cs
│   │   │   ├── Common/
│   │   │   │   ├── AuthResultBuilder.cs
│   │   │   │   └── AuthUserMapper.cs
│   │   │   ├── Mappings/
│   │   │   │   └── AuthenticationMappingProfile.cs
│   │   │   ├── Models/
│   │   │   │   ├── AuthResponse.cs
│   │   │   │   ├── AuthUserDto.cs
│   │   │   │   ├── ExternalIdentityResult.cs
│   │   │   │   ├── MessageResponse.cs
│   │   │   │   ├── UserPermissionDto.cs
│   │   │   │   └── UserProfileResponse.cs
│   │   │   ├── Queries/
│   │   │   │   └── GetCurrentUser/
│   │   │   │       ├── GetCurrentUserQuery.cs
│   │   │   │       └── GetCurrentUserQueryHandler.cs
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── Calendars/
│   │   │   ├── Commands/
│   │   │   │   ├── AddPersonalEvent/
│   │   │   │   │   ├── AddPersonalEventCommand.cs
│   │   │   │   │   ├── AddPersonalEventCommandHandler.cs
│   │   │   │   │   ├── AddPersonalEventCommandValidator.cs
│   │   │   │   │   └── AddPersonalEventResponse.cs
│   │   │   │   ├── DeletePersonalEvent/
│   │   │   │   │   ├── DeletePersonalEventCommand.cs
│   │   │   │   │   ├── DeletePersonalEventCommandHandler.cs
│   │   │   │   │   ├── DeletePersonalEventCommandValidator.cs
│   │   │   │   │   └── DeletePersonalEventResponse.cs
│   │   │   │   ├── SwitchViewMode/
│   │   │   │   │   ├── SwitchViewModeCommand.cs
│   │   │   │   │   ├── SwitchViewModeCommandHandler.cs
│   │   │   │   │   ├── SwitchViewModeCommandValidator.cs
│   │   │   │   │   └── SwitchViewModeResponse.cs
│   │   │   │   └── UpdatePersonalEvent/
│   │   │   │       ├── UpdatePersonalEventCommand.cs
│   │   │   │       ├── UpdatePersonalEventCommandHandler.cs
│   │   │   │       ├── UpdatePersonalEventCommandValidator.cs
│   │   │   │       └── UpdatePersonalEventResponse.cs
│   │   │   └── Queries/
│   │   │       ├── ViewDepartmentCalendar/
│   │   │       │   ├── ViewDepartmentCalendarDto.cs
│   │   │       │   ├── ViewDepartmentCalendarQuery.cs
│   │   │       │   └── ViewDepartmentCalendarQueryHandler.cs
│   │   │       ├── ViewEventDetails/
│   │   │       │   ├── ViewEventDetailsDto.cs
│   │   │       │   ├── ViewEventDetailsQuery.cs
│   │   │       │   └── ViewEventDetailsQueryHandler.cs
│   │   │       └── ViewMyEvents/
│   │   │           ├── ViewMyEventsDto.cs
│   │   │           ├── ViewMyEventsQuery.cs
│   │   │           └── ViewMyEventsQueryHandler.cs
│   │   ├── Campuses/
│   │   │   ├── Commands/
│   │   │   │   ├── AddNewCampus/
│   │   │   │   │   ├── AddNewCampusCommand.cs
│   │   │   │   │   ├── AddNewCampusCommandHandler.cs
│   │   │   │   │   ├── AddNewCampusCommandValidator.cs
│   │   │   │   │   └── AddNewCampusResponse.cs
│   │   │   │   ├── AssignCampusLead/
│   │   │   │   │   ├── AssignCampusLeadCommand.cs
│   │   │   │   │   ├── AssignCampusLeadCommandHandler.cs
│   │   │   │   │   ├── AssignCampusLeadCommandValidator.cs
│   │   │   │   │   └── AssignCampusLeadResponse.cs
│   │   │   │   ├── ManageCampusStatus/
│   │   │   │   │   ├── ManageCampusStatusCommand.cs
│   │   │   │   │   ├── ManageCampusStatusCommandHandler.cs
│   │   │   │   │   ├── ManageCampusStatusCommandValidator.cs
│   │   │   │   │   └── ManageCampusStatusResponse.cs
│   │   │   │   └── UpdateCampus/
│   │   │   │       ├── UpdateCampusCommand.cs
│   │   │   │       ├── UpdateCampusCommandHandler.cs
│   │   │   │       ├── UpdateCampusCommandValidator.cs
│   │   │   │       └── UpdateCampusResponse.cs
│   │   │   ├── Common/
│   │   │   │   ├── CampusDuplicateGuard.cs
│   │   │   │   ├── CampusErrorCodes.cs
│   │   │   │   ├── CampusListItemDto.cs
│   │   │   │   ├── CampusListQueryExecutor.cs
│   │   │   │   ├── CampusNormalization.cs
│   │   │   │   └── ICampusListCriteria.cs
│   │   │   └── Queries/
│   │   │       ├── GetActiveCampuses/
│   │   │       │   ├── ActiveCampusDto.cs
│   │   │       │   ├── GetActiveCampusesQuery.cs
│   │   │       │   └── GetActiveCampusesQueryHandler.cs
│   │   │       ├── GetCampusFilterOptions/
│   │   │       │   ├── CampusFilterOptionsDto.cs
│   │   │       │   ├── GetCampusFilterOptionsQuery.cs
│   │   │       │   └── GetCampusFilterOptionsQueryHandler.cs
│   │   │       ├── SearchandFilterCampus/
│   │   │       │   ├── SearchandFilterCampusQuery.cs
│   │   │       │   └── SearchandFilterCampusQueryHandler.cs
│   │   │       ├── ViewCampusDetails/
│   │   │       │   ├── ViewCampusDetailsDto.cs
│   │   │       │   ├── ViewCampusDetailsQuery.cs
│   │   │       │   └── ViewCampusDetailsQueryHandler.cs
│   │   │       └── ViewCampusList/
│   │   │           ├── ViewCampusListQuery.cs
│   │   │           └── ViewCampusListQueryHandler.cs
│   │   ├── Common/
│   │   │   ├── Behaviours/
│   │   │   │   ├── AuditLogBehaviour.cs
│   │   │   │   ├── AuthorizationBehaviour.cs
│   │   │   │   ├── LoggingBehaviour.cs
│   │   │   │   └── ValidationBehaviour.cs
│   │   │   ├── DTOs/
│   │   │   │   └── VisitFormDtos.cs
│   │   │   ├── Exceptions/
│   │   │   │   ├── AuthBusinessException.cs
│   │   │   │   ├── AuthenticationFailedException.cs
│   │   │   │   ├── BusinessRuleException.cs
│   │   │   │   ├── ConflictException.cs
│   │   │   │   ├── ForbiddenException.cs
│   │   │   │   ├── NotFoundException.cs
│   │   │   │   └── ValidationException.cs
│   │   │   ├── Files/
│   │   │   │   ├── FileChecksumService.cs
│   │   │   │   ├── FileContentValidator.cs
│   │   │   │   ├── FileObjectKeyBuilder.cs
│   │   │   │   ├── FilePurpose.cs
│   │   │   │   ├── FileUploadService.cs
│   │   │   │   ├── FileValidationPolicy.cs
│   │   │   │   ├── FileValidationRule.cs
│   │   │   │   └── UploadedFileDto.cs
│   │   │   ├── Interfaces/
│   │   │   │   ├── IApplicationDbContext.cs
│   │   │   │   ├── IApprovalRoutingService.cs
│   │   │   │   ├── IAuditLogService.cs
│   │   │   │   ├── ICampusRepository.cs
│   │   │   │   ├── ICurrentUserService.cs
│   │   │   │   ├── IDateTimeService.cs
│   │   │   │   ├── IDelegationRepository.cs
│   │   │   │   ├── IDocumentRepository.cs
│   │   │   │   ├── IEmailActionTokenService.cs
│   │   │   │   ├── IEmailService.cs
│   │   │   │   ├── IExternalApiClient.cs
│   │   │   │   ├── IFaceRecognitionService.cs
│   │   │   │   ├── IFeidIdentityVerifier.cs
│   │   │   │   ├── IFileChecksumService.cs
│   │   │   │   ├── IFileObjectKeyBuilder.cs
│   │   │   │   ├── IFileStorageFolderResolver.cs
│   │   │   │   ├── IFileStorageService.cs
│   │   │   │   ├── IFileUploadService.cs
│   │   │   │   ├── IFileValidationPolicy.cs
│   │   │   │   ├── IFileValidationService.cs
│   │   │   │   ├── IGoogleDriveStorageService.cs
│   │   │   │   ├── IGoogleTokenValidator.cs
│   │   │   │   ├── IIdempotencyService.cs
│   │   │   │   ├── IJwtTokenService.cs
│   │   │   │   ├── INotificationService.cs
│   │   │   │   ├── IOcrService.cs
│   │   │   │   ├── IOtpService.cs
│   │   │   │   ├── IOwnershipChecker.cs
│   │   │   │   ├── IPartnerRepository.cs
│   │   │   │   ├── IPasswordHasher.cs
│   │   │   │   ├── IRateLimitService.cs
│   │   │   │   ├── ISecurityAuditService.cs
│   │   │   │   ├── ISessionService.cs
│   │   │   │   ├── IUserProvisionService.cs
│   │   │   │   ├── IUserRepository.cs
│   │   │   │   └── IVisitRequestService.cs
│   │   │   ├── Models/
│   │   │   │   ├── ErrorResponse.cs
│   │   │   │   ├── FileUploadResult.cs
│   │   │   │   ├── GoogleDriveUploadResult.cs
│   │   │   │   ├── PagedResult.cs
│   │   │   │   ├── PaginatedResult.cs
│   │   │   │   ├── PaginationRequest.cs
│   │   │   │   ├── Result.cs
│   │   │   │   └── ResultOfT.cs
│   │   │   ├── Security/
│   │   │   │   ├── AuthErrorCodes.cs
│   │   │   │   ├── AuthOptions.cs
│   │   │   │   ├── EffectiveRole.cs
│   │   │   │   ├── IHtmlSanitizerService.cs
│   │   │   │   ├── IRoleAccessPolicy.cs
│   │   │   │   ├── PasswordPolicy.cs
│   │   │   │   ├── PemsClaimTypes.cs
│   │   │   │   ├── RoleAccessPolicy.cs
│   │   │   │   ├── RoleCode.cs
│   │   │   │   ├── SubRole.cs
│   │   │   │   └── UseCasePermissionAttribute.cs
│   │   │   ├── Storage/
│   │   │   │   └── GoogleDriveOptions.cs
│   │   │   └── VietnamTime.cs
│   │   ├── Dashboard/
│   │   │   └── Queries/
│   │   │       └── GetDepartmentLeaderDashboardSummary/
│   │   │           ├── DepartmentLeaderDashboardSummaryDto.cs
│   │   │           ├── GetDepartmentLeaderDashboardSummaryQuery.cs
│   │   │           └── GetDepartmentLeaderDashboardSummaryQueryHandler.cs
│   │   ├── Delegations/
│   │   │   ├── Commands/
│   │   │   │   ├── ApproveCrossCampusRequest/
│   │   │   │   │   ├── ApproveCrossCampusRequestCommand.cs
│   │   │   │   │   ├── ApproveCrossCampusRequestCommandHandler.cs
│   │   │   │   │   ├── ApproveCrossCampusRequestCommandValidator.cs
│   │   │   │   │   └── ApproveCrossCampusRequestResponse.cs
│   │   │   │   ├── ApproveResourceRequest/
│   │   │   │   │   ├── ApproveResourceRequestCommand.cs
│   │   │   │   │   ├── ApproveResourceRequestCommandHandler.cs
│   │   │   │   │   ├── ApproveResourceRequestCommandValidator.cs
│   │   │   │   │   └── ApproveResourceRequestResponse.cs
│   │   │   │   ├── AssignDepartmentStaff/
│   │   │   │   │   ├── AssignDepartmentStaffCommand.cs
│   │   │   │   │   └── AssignDepartmentStaffCommandHandler.cs
│   │   │   │   ├── CancelVisitInstanceReminderSettings/
│   │   │   │   │   ├── CancelVisitInstanceReminderSettingsCommand.cs
│   │   │   │   │   └── CancelVisitInstanceReminderSettingsCommandHandler.cs
│   │   │   │   ├── CancelVisitLogisticsItem/
│   │   │   │   │   ├── CancelVisitLogisticsItemCommand.cs
│   │   │   │   │   └── CancelVisitLogisticsItemCommandHandler.cs
│   │   │   │   ├── CancelVisitRequest/
│   │   │   │   │   ├── CancelVisitRequestCommand.cs
│   │   │   │   │   ├── CancelVisitRequestCommandHandler.cs
│   │   │   │   │   ├── CancelVisitRequestCommandValidator.cs
│   │   │   │   │   └── CancelVisitRequestResponse.cs
│   │   │   │   ├── CloseDelegation/
│   │   │   │   │   ├── CloseDelegationCommand.cs
│   │   │   │   │   ├── CloseDelegationCommandHandler.cs
│   │   │   │   │   ├── CloseDelegationCommandValidator.cs
│   │   │   │   │   └── CloseDelegationResponse.cs
│   │   │   │   ├── CompleteVisitStage/
│   │   │   │   │   ├── CompleteVisitStageCommand.cs
│   │   │   │   │   ├── CompleteVisitStageCommandHandler.cs
│   │   │   │   │   ├── CompleteVisitStageCommandValidator.cs
│   │   │   │   │   └── CompleteVisitStageResponse.cs
│   │   │   │   ├── ConfirmParticipation/
│   │   │   │   │   ├── ConfirmParticipationCommand.cs
│   │   │   │   │   ├── ConfirmParticipationCommandHandler.cs
│   │   │   │   │   ├── ConfirmParticipationCommandValidator.cs
│   │   │   │   │   └── ConfirmParticipationResponse.cs
│   │   │   │   ├── ConfirmTheChangeProposal/
│   │   │   │   │   ├── ConfirmTheChangeProposalCommand.cs
│   │   │   │   │   ├── ConfirmTheChangeProposalCommandHandler.cs
│   │   │   │   │   ├── ConfirmTheChangeProposalCommandValidator.cs
│   │   │   │   │   └── ConfirmTheChangeProposalResponse.cs
│   │   │   │   ├── CreateGuestDelegation/
│   │   │   │   │   ├── CreateGuestDelegationCommand.cs
│   │   │   │   │   ├── CreateGuestDelegationCommandHandler.cs
│   │   │   │   │   ├── CreateGuestDelegationCommandValidator.cs
│   │   │   │   │   └── CreateGuestDelegationResponse.cs
│   │   │   │   ├── CreateMeetingMinutes/
│   │   │   │   │   ├── CreateMeetingMinutesCommand.cs
│   │   │   │   │   ├── CreateMeetingMinutesCommandHandler.cs
│   │   │   │   │   ├── CreateMeetingMinutesCommandValidator.cs
│   │   │   │   │   └── CreateMeetingMinutesResponse.cs
│   │   │   │   ├── CreateNewsArticle/
│   │   │   │   │   ├── CreateNewsArticleCommand.cs
│   │   │   │   │   ├── CreateNewsArticleCommandHandler.cs
│   │   │   │   │   ├── CreateNewsArticleCommandValidator.cs
│   │   │   │   │   └── CreateNewsArticleResponse.cs
│   │   │   │   ├── CreatePartnerProfile/
│   │   │   │   │   ├── CreatePartnerProfileCommand.cs
│   │   │   │   │   ├── CreatePartnerProfileCommandHandler.cs
│   │   │   │   │   ├── CreatePartnerProfileCommandValidator.cs
│   │   │   │   │   └── CreatePartnerProfileResponse.cs
│   │   │   │   ├── EditMeetingMinutes/
│   │   │   │   │   ├── EditMeetingMinutesCommand.cs
│   │   │   │   │   ├── EditMeetingMinutesCommandHandler.cs
│   │   │   │   │   ├── EditMeetingMinutesCommandValidator.cs
│   │   │   │   │   └── EditMeetingMinutesResponse.cs
│   │   │   │   ├── InitiateVisitRequest/
│   │   │   │   │   ├── InitiateVisitRequestCommand.cs
│   │   │   │   │   ├── InitiateVisitRequestCommandHandler.cs
│   │   │   │   │   ├── InitiateVisitRequestCommandValidator.cs
│   │   │   │   │   └── InitiateVisitRequestResponse.cs
│   │   │   │   ├── InviteVisitParticipant/
│   │   │   │   │   ├── InviteVisitParticipantCommand.cs
│   │   │   │   │   ├── InviteVisitParticipantCommandHandler.cs
│   │   │   │   │   ├── InviteVisitParticipantResponse.cs
│   │   │   │   │   └── ParticipantInvitationEmailBuilder.cs
│   │   │   │   ├── PrepareVisitLogistics/
│   │   │   │   │   ├── PrepareVisitLogisticsCommand.cs
│   │   │   │   │   ├── PrepareVisitLogisticsCommandHandler.cs
│   │   │   │   │   ├── PrepareVisitLogisticsCommandValidator.cs
│   │   │   │   │   └── PrepareVisitLogisticsResponse.cs
│   │   │   │   ├── ProcessVisitRequest/
│   │   │   │   │   ├── ProcessVisitRequestCommand.cs
│   │   │   │   │   ├── ProcessVisitRequestCommandHandler.cs
│   │   │   │   │   ├── ProcessVisitRequestCommandValidator.cs
│   │   │   │   │   └── ProcessVisitRequestResponse.cs
│   │   │   │   ├── ProposeResourceModification/
│   │   │   │   │   ├── ProposeResourceModificationCommand.cs
│   │   │   │   │   ├── ProposeResourceModificationCommandHandler.cs
│   │   │   │   │   ├── ProposeResourceModificationCommandValidator.cs
│   │   │   │   │   └── ProposeResourceModificationResponse.cs
│   │   │   │   ├── RejectVisitRequest/
│   │   │   │   │   ├── RejectVisitRequestCommand.cs
│   │   │   │   │   ├── RejectVisitRequestCommandHandler.cs
│   │   │   │   │   ├── RejectVisitRequestCommandValidator.cs
│   │   │   │   │   └── RejectVisitRequestResponse.cs
│   │   │   │   ├── RemoveVisitParticipant/
│   │   │   │   │   ├── RemoveVisitParticipantCommand.cs
│   │   │   │   │   └── RemoveVisitParticipantCommandHandler.cs
│   │   │   │   ├── ResendVisitRequestOtp/
│   │   │   │   │   ├── ResendVisitRequestOtpCommand.cs
│   │   │   │   │   ├── ResendVisitRequestOtpCommandHandler.cs
│   │   │   │   │   └── ResendVisitRequestOtpCommandValidator.cs
│   │   │   │   ├── RespondVisitParticipantInvitation/
│   │   │   │   │   ├── RespondVisitParticipantInvitationCommand.cs
│   │   │   │   │   ├── RespondVisitParticipantInvitationCommandHandler.cs
│   │   │   │   │   ├── RespondVisitParticipantInvitationCommandValidator.cs
│   │   │   │   │   └── RespondVisitParticipantInvitationResponse.cs
│   │   │   │   ├── SaveVisitAgenda/
│   │   │   │   │   ├── SaveVisitAgendaCommand.cs
│   │   │   │   │   ├── SaveVisitAgendaCommandHandler.cs
│   │   │   │   │   ├── SaveVisitAgendaCommandValidator.cs
│   │   │   │   │   └── SaveVisitAgendaResponse.cs
│   │   │   │   ├── SaveVisitInstanceReminderSettings/
│   │   │   │   │   ├── SaveVisitInstanceReminderSettingsCommand.cs
│   │   │   │   │   ├── SaveVisitInstanceReminderSettingsCommandHandler.cs
│   │   │   │   │   ├── SaveVisitInstanceReminderSettingsCommandValidator.cs
│   │   │   │   │   └── SaveVisitInstanceReminderSettingsResponse.cs
│   │   │   │   ├── ScanBusinessCard/
│   │   │   │   │   ├── ScanBusinessCardCommand.cs
│   │   │   │   │   ├── ScanBusinessCardCommandHandler.cs
│   │   │   │   │   ├── ScanBusinessCardCommandValidator.cs
│   │   │   │   │   └── ScanBusinessCardResponse.cs
│   │   │   │   ├── SignVisitLogisticsHandover/
│   │   │   │   │   ├── SignVisitLogisticsHandoverCommand.cs
│   │   │   │   │   └── SignVisitLogisticsHandoverCommandHandler.cs
│   │   │   │   ├── SubmitDelegationFeedback/
│   │   │   │   │   ├── SubmitDelegationFeedbackCommand.cs
│   │   │   │   │   ├── SubmitDelegationFeedbackCommandHandler.cs
│   │   │   │   │   ├── SubmitDelegationFeedbackCommandValidator.cs
│   │   │   │   │   └── SubmitDelegationFeedbackResponse.cs
│   │   │   │   ├── TagFacesOnPhotos/
│   │   │   │   │   ├── TagFacesOnPhotosCommand.cs
│   │   │   │   │   ├── TagFacesOnPhotosCommandHandler.cs
│   │   │   │   │   ├── TagFacesOnPhotosCommandValidator.cs
│   │   │   │   │   └── TagFacesOnPhotosResponse.cs
│   │   │   │   ├── UpdateGuestDelegation/
│   │   │   │   │   ├── UpdateGuestDelegationCommand.cs
│   │   │   │   │   ├── UpdateGuestDelegationCommandHandler.cs
│   │   │   │   │   ├── UpdateGuestDelegationCommandValidator.cs
│   │   │   │   │   └── UpdateGuestDelegationResponse.cs
│   │   │   │   ├── UpdateRegistrantInfo/
│   │   │   │   │   ├── UpdateRegistrantInfoCommand.cs
│   │   │   │   │   ├── UpdateRegistrantInfoCommandHandler.cs
│   │   │   │   │   ├── UpdateRegistrantInfoCommandValidator.cs
│   │   │   │   │   └── UpdateRegistrantInfoResponse.cs
│   │   │   │   ├── UpdateVisitInstancePreparationNote/
│   │   │   │   │   ├── UpdateVisitInstancePreparationNoteCommand.cs
│   │   │   │   │   ├── UpdateVisitInstancePreparationNoteCommandHandler.cs
│   │   │   │   │   ├── UpdateVisitInstancePreparationNoteCommandValidator.cs
│   │   │   │   │   └── UpdateVisitInstancePreparationNoteResponse.cs
│   │   │   │   ├── UpdateVisitLogistics/
│   │   │   │   │   ├── UpdateVisitLogisticsCommand.cs
│   │   │   │   │   ├── UpdateVisitLogisticsCommandHandler.cs
│   │   │   │   │   ├── UpdateVisitLogisticsCommandValidator.cs
│   │   │   │   │   └── UpdateVisitLogisticsResponse.cs
│   │   │   │   ├── UploadAttachedDocuments/
│   │   │   │   │   ├── UploadAttachedDocumentsCommand.cs
│   │   │   │   │   ├── UploadAttachedDocumentsCommandHandler.cs
│   │   │   │   │   ├── UploadAttachedDocumentsCommandValidator.cs
│   │   │   │   │   └── UploadAttachedDocumentsResponse.cs
│   │   │   │   ├── UploadVisitPhotos/
│   │   │   │   │   ├── UploadVisitPhotosCommand.cs
│   │   │   │   │   ├── UploadVisitPhotosCommandHandler.cs
│   │   │   │   │   ├── UploadVisitPhotosCommandValidator.cs
│   │   │   │   │   └── UploadVisitPhotosResponse.cs
│   │   │   │   ├── VerifyAndCreateVisitRequest/
│   │   │   │   │   ├── VerifyAndCreateVisitRequestCommand.cs
│   │   │   │   │   ├── VerifyAndCreateVisitRequestCommandHandler.cs
│   │   │   │   │   ├── VerifyAndCreateVisitRequestCommandValidator.cs
│   │   │   │   │   └── VerifyAndCreateVisitRequestResponse.cs
│   │   │   │   ├── IVisitRequestFormCommand.cs
│   │   │   │   └── VisitRequestFormValidationRules.cs
│   │   │   ├── Common/
│   │   │   │   ├── ScheduleConflictResolver.cs
│   │   │   │   ├── VisitInstanceAccess.cs
│   │   │   │   ├── VisitParticipantListBuilder.cs
│   │   │   │   └── VisitReminderAccess.cs
│   │   │   ├── Dtos/
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── DelegationsMappingProfile.cs
│   │   │   ├── Minutes/
│   │   │   │   ├── AcquireMinutesLockCommand.cs
│   │   │   │   ├── AcquireMinutesLockCommandHandler.cs
│   │   │   │   ├── CreateOrLockMinutesCommand.cs
│   │   │   │   ├── CreateOrLockMinutesCommandHandler.cs
│   │   │   │   ├── GetNewMinuteParticipantsQuery.cs
│   │   │   │   ├── GetNewMinuteParticipantsQueryHandler.cs
│   │   │   │   ├── GetVisitInstanceMinutesQuery.cs
│   │   │   │   ├── GetVisitInstanceMinutesQueryHandler.cs
│   │   │   │   ├── MinuteAccess.cs
│   │   │   │   ├── MinuteActionItemDto.cs
│   │   │   │   ├── MinuteAutoFill.cs
│   │   │   │   ├── MinuteChildren.cs
│   │   │   │   ├── MinuteDto.cs
│   │   │   │   ├── MinuteParticipantDto.cs
│   │   │   │   ├── ReleaseMinutesLockCommand.cs
│   │   │   │   ├── ReleaseMinutesLockCommandHandler.cs
│   │   │   │   ├── SaveMinutesCommand.cs
│   │   │   │   ├── SaveMinutesCommandHandler.cs
│   │   │   │   ├── SearchMinuteUsersQuery.cs
│   │   │   │   └── SearchMinuteUsersQueryHandler.cs
│   │   │   ├── News/
│   │   │   │   ├── CreateVisitInstanceNewsCommand.cs
│   │   │   │   ├── CreateVisitInstanceNewsCommandHandler.cs
│   │   │   │   ├── GetVisitInstanceNewsQuery.cs
│   │   │   │   ├── GetVisitInstanceNewsQueryHandler.cs
│   │   │   │   ├── SubmitVisitInstanceNewsCommand.cs
│   │   │   │   ├── SubmitVisitInstanceNewsCommandHandler.cs
│   │   │   │   ├── UpdateVisitInstanceNewsCommand.cs
│   │   │   │   ├── UpdateVisitInstanceNewsCommandHandler.cs
│   │   │   │   ├── VisitNewsAccess.cs
│   │   │   │   └── VisitNewsDto.cs
│   │   │   ├── Queries/
│   │   │   │   ├── GetAgendaResponsibleCandidates/
│   │   │   │   │   ├── AgendaResponsibleCandidateDto.cs
│   │   │   │   │   ├── GetAgendaResponsibleCandidatesQuery.cs
│   │   │   │   │   └── GetAgendaResponsibleCandidatesQueryHandler.cs
│   │   │   │   ├── GetDepartmentStaffCandidates/
│   │   │   │   │   ├── GetDepartmentStaffCandidatesQuery.cs
│   │   │   │   │   └── GetDepartmentStaffCandidatesQueryHandler.cs
│   │   │   │   ├── GetHostCandidates/
│   │   │   │   │   ├── GetHostCandidatesQuery.cs
│   │   │   │   │   ├── GetHostCandidatesQueryHandler.cs
│   │   │   │   │   └── HostCandidateDto.cs
│   │   │   │   ├── GetParticipantCandidates/
│   │   │   │   │   ├── GetParticipantCandidatesQuery.cs
│   │   │   │   │   ├── GetParticipantCandidatesQueryHandler.cs
│   │   │   │   │   └── ParticipantCandidateDto.cs
│   │   │   │   ├── GetSubmittedVisitRequestFormDetail/
│   │   │   │   │   ├── GetSubmittedVisitRequestFormDetailQuery.cs
│   │   │   │   │   ├── GetSubmittedVisitRequestFormDetailQueryHandler.cs
│   │   │   │   │   └── SubmittedVisitRequestFormDetailDto.cs
│   │   │   │   ├── GetSupportDepartments/
│   │   │   │   │   ├── GetSupportDepartmentsQuery.cs
│   │   │   │   │   ├── GetSupportDepartmentsQueryHandler.cs
│   │   │   │   │   └── SupportDepartmentDto.cs
│   │   │   │   ├── GetVisitInstanceContribution/
│   │   │   │   │   ├── ContributionPageDto.cs
│   │   │   │   │   ├── GetVisitInstanceContributionQuery.cs
│   │   │   │   │   └── GetVisitInstanceContributionQueryHandler.cs
│   │   │   │   ├── GetVisitInstanceLogistics/
│   │   │   │   │   ├── GetVisitInstanceLogisticsQuery.cs
│   │   │   │   │   └── GetVisitInstanceLogisticsQueryHandler.cs
│   │   │   │   ├── GetVisitInstanceParticipants/
│   │   │   │   │   ├── GetVisitInstanceParticipantsQuery.cs
│   │   │   │   │   ├── GetVisitInstanceParticipantsQueryHandler.cs
│   │   │   │   │   └── VisitParticipantListItemDto.cs
│   │   │   │   ├── GetVisitInstanceReminderSettings/
│   │   │   │   │   ├── GetVisitInstanceReminderSettingsQuery.cs
│   │   │   │   │   ├── GetVisitInstanceReminderSettingsQueryHandler.cs
│   │   │   │   │   └── VisitReminderSettingDto.cs
│   │   │   │   ├── GetVisitInstanceSentEmails/
│   │   │   │   │   ├── GetVisitInstanceSentEmailsQuery.cs
│   │   │   │   │   ├── GetVisitInstanceSentEmailsQueryHandler.cs
│   │   │   │   │   └── GetVisitInstanceSentEmailsResponse.cs
│   │   │   │   ├── GetVisitInstanceSummary/
│   │   │   │   │   ├── GetVisitInstanceSummaryQuery.cs
│   │   │   │   │   ├── GetVisitInstanceSummaryQueryHandler.cs
│   │   │   │   │   └── ProcessSummaryPageDto.cs
│   │   │   │   ├── GetVisitInvitationDetail/
│   │   │   │   │   ├── GetVisitInvitationDetailQuery.cs
│   │   │   │   │   ├── GetVisitInvitationDetailQueryHandler.cs
│   │   │   │   │   └── VisitInvitationDetailDto.cs
│   │   │   │   ├── GetVisitInvitations/
│   │   │   │   │   ├── GetVisitInvitationsQuery.cs
│   │   │   │   │   ├── GetVisitInvitationsQueryHandler.cs
│   │   │   │   │   └── InvitationListItemDto.cs
│   │   │   │   ├── GetVisitProcessDetail/
│   │   │   │   │   ├── GetVisitProcessDetailQuery.cs
│   │   │   │   │   ├── GetVisitProcessDetailQueryHandler.cs
│   │   │   │   │   └── VisitProcessDetailDto.cs
│   │   │   │   ├── GetVisitProcessPermissions/
│   │   │   │   │   ├── GetVisitProcessPermissionsQuery.cs
│   │   │   │   │   ├── GetVisitProcessPermissionsQueryHandler.cs
│   │   │   │   │   └── VisitProcessPermissionDto.cs
│   │   │   │   ├── SearchDelegations/
│   │   │   │   │   ├── SearchDelegationsDto.cs
│   │   │   │   │   ├── SearchDelegationsQuery.cs
│   │   │   │   │   └── SearchDelegationsQueryHandler.cs
│   │   │   │   ├── ViewGuestDelegationDetails/
│   │   │   │   │   ├── ViewGuestDelegationDetailsDto.cs
│   │   │   │   │   ├── ViewGuestDelegationDetailsQuery.cs
│   │   │   │   │   └── ViewGuestDelegationDetailsQueryHandler.cs
│   │   │   │   ├── ViewGuestDelegationList/
│   │   │   │   │   ├── ViewGuestDelegationListDto.cs
│   │   │   │   │   ├── ViewGuestDelegationListQuery.cs
│   │   │   │   │   ├── ViewGuestDelegationListQueryHandler.cs
│   │   │   │   │   └── ViewGuestDelegationListQueryValidator.cs
│   │   │   │   ├── ViewMeetingMinutesDetails/
│   │   │   │   │   ├── ViewMeetingMinutesDetailsDto.cs
│   │   │   │   │   ├── ViewMeetingMinutesDetailsQuery.cs
│   │   │   │   │   └── ViewMeetingMinutesDetailsQueryHandler.cs
│   │   │   │   └── ViewMyVisitInvitations/
│   │   │   │       ├── GetVisitInvitationByIdQuery.cs
│   │   │   │       ├── GetVisitInvitationByIdQueryHandler.cs
│   │   │   │       ├── ViewMyVisitInvitationsQuery.cs
│   │   │   │       ├── ViewMyVisitInvitationsQueryHandler.cs
│   │   │   │       ├── VisitInvitationDto.cs
│   │   │   │       └── VisitInvitationProjection.cs
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── DepartmentReceptionTasks/
│   │   │   ├── Commands/
│   │   │   │   ├── AcceptAssignedLogisticsTask/
│   │   │   │   │   └── AcceptAssignedLogisticsTaskCommand.cs
│   │   │   │   ├── AcceptInvitation/
│   │   │   │   │   └── AcceptInvitationCommand.cs
│   │   │   │   ├── AssignRequestAssignee/
│   │   │   │   │   └── AssignRequestAssigneeCommand.cs
│   │   │   │   ├── ConfirmRequest/
│   │   │   │   │   └── ConfirmRequestCommand.cs
│   │   │   │   ├── CreatePersonalEvent/
│   │   │   │   │   └── CreatePersonalEventCommand.cs
│   │   │   │   ├── DeclineAssignedLogisticsTask/
│   │   │   │   │   └── DeclineAssignedLogisticsTaskCommand.cs
│   │   │   │   ├── DeclineInvitation/
│   │   │   │   │   └── DeclineInvitationCommand.cs
│   │   │   │   ├── ProposeRequestChange/
│   │   │   │   │   └── ProposeRequestChangeCommand.cs
│   │   │   │   ├── RejectRequest/
│   │   │   │   │   └── RejectRequestCommand.cs
│   │   │   │   └── SignLogisticsHandover/
│   │   │   │       └── SignLogisticsHandoverCommand.cs
│   │   │   └── Queries/
│   │   │       ├── GetAssignmentsProgressList/
│   │   │       │   └── GetAssignmentsProgressListQuery.cs
│   │   │       ├── GetAttentionItems/
│   │   │       │   └── GetAttentionItemsQuery.cs
│   │   │       ├── GetDepartmentAssigneeCandidates/
│   │   │       │   └── GetDepartmentAssigneeCandidatesQuery.cs
│   │   │       ├── GetDepartmentCalendar/
│   │   │       │   └── GetDepartmentCalendarQuery.cs
│   │   │       ├── GetInvitationDetail/
│   │   │       │   └── GetInvitationDetailQuery.cs
│   │   │       └── GetRequestDetail/
│   │   │           └── GetRequestDetailQuery.cs
│   │   ├── Departments/
│   │   │   ├── Commands/
│   │   │   │   ├── AddDepartmentPersonnel/
│   │   │   │   │   ├── AddDepartmentPersonnelCommand.cs
│   │   │   │   │   ├── AddDepartmentPersonnelCommandHandler.cs
│   │   │   │   │   ├── AddDepartmentPersonnelCommandValidator.cs
│   │   │   │   │   └── AddDepartmentPersonnelResponse.cs
│   │   │   │   ├── AddNewDepartment/
│   │   │   │   │   ├── AddNewDepartmentCommand.cs
│   │   │   │   │   ├── AddNewDepartmentCommandHandler.cs
│   │   │   │   │   ├── AddNewDepartmentCommandValidator.cs
│   │   │   │   │   └── AddNewDepartmentResponse.cs
│   │   │   │   ├── AssignTasks/
│   │   │   │   │   ├── AssignTasksCommand.cs
│   │   │   │   │   ├── AssignTasksCommandHandler.cs
│   │   │   │   │   ├── AssignTasksCommandValidator.cs
│   │   │   │   │   └── AssignTasksResponse.cs
│   │   │   │   ├── ManageDepartmentStatus/
│   │   │   │   │   ├── ManageDepartmentStatusCommand.cs
│   │   │   │   │   ├── ManageDepartmentStatusCommandHandler.cs
│   │   │   │   │   ├── ManageDepartmentStatusCommandValidator.cs
│   │   │   │   │   └── ManageDepartmentStatusResponse.cs
│   │   │   │   ├── ReassignDepartmentLead/
│   │   │   │   │   ├── ReassignDepartmentLeadCommand.cs
│   │   │   │   │   ├── ReassignDepartmentLeadCommandHandler.cs
│   │   │   │   │   ├── ReassignDepartmentLeadCommandValidator.cs
│   │   │   │   │   └── ReassignDepartmentLeadResponse.cs
│   │   │   │   ├── RemovePersonnel/
│   │   │   │   │   ├── RemovePersonnelCommand.cs
│   │   │   │   │   ├── RemovePersonnelCommandHandler.cs
│   │   │   │   │   ├── RemovePersonnelCommandValidator.cs
│   │   │   │   │   └── RemovePersonnelResponse.cs
│   │   │   │   ├── ReviewAssignedTasks/
│   │   │   │   │   ├── ReviewAssignedTasksCommand.cs
│   │   │   │   │   ├── ReviewAssignedTasksCommandHandler.cs
│   │   │   │   │   ├── ReviewAssignedTasksCommandValidator.cs
│   │   │   │   │   └── ReviewAssignedTasksResponse.cs
│   │   │   │   ├── SignTheServiceDeliveryReport/
│   │   │   │   │   ├── SignTheServiceDeliveryReportCommand.cs
│   │   │   │   │   ├── SignTheServiceDeliveryReportCommandHandler.cs
│   │   │   │   │   ├── SignTheServiceDeliveryReportCommandValidator.cs
│   │   │   │   │   └── SignTheServiceDeliveryReportResponse.cs
│   │   │   │   ├── UpdateDepartment/
│   │   │   │   │   ├── UpdateDepartmentCommand.cs
│   │   │   │   │   ├── UpdateDepartmentCommandHandler.cs
│   │   │   │   │   ├── UpdateDepartmentCommandValidator.cs
│   │   │   │   │   └── UpdateDepartmentResponse.cs
│   │   │   │   └── UpdateDepartmentPersonnel/
│   │   │   │       ├── UpdateDepartmentPersonnelCommand.cs
│   │   │   │       └── UpdateDepartmentPersonnelCommandHandler.cs
│   │   │   ├── Common/
│   │   │   │   ├── DepartmentErrorCodes.cs
│   │   │   │   ├── DepartmentListItemDto.cs
│   │   │   │   ├── DepartmentListQueryExecutor.cs
│   │   │   │   ├── IDepartmentListCriteria.cs
│   │   │   │   └── StaffLeaderDepartmentScope.cs
│   │   │   └── Queries/
│   │   │       ├── SearchCoordinationTasks/
│   │   │       │   ├── SearchCoordinationTasksDto.cs
│   │   │       │   ├── SearchCoordinationTasksQuery.cs
│   │   │       │   └── SearchCoordinationTasksQueryHandler.cs
│   │   │       ├── SearchPersonnel/
│   │   │       │   ├── SearchPersonnelDto.cs
│   │   │       │   ├── SearchPersonnelQuery.cs
│   │   │       │   └── SearchPersonnelQueryHandler.cs
│   │   │       ├── SearchandFilterDepartments/
│   │   │       │   ├── SearchandFilterDepartmentsQuery.cs
│   │   │       │   └── SearchandFilterDepartmentsQueryHandler.cs
│   │   │       ├── ViewCoordinationTasks/
│   │   │       │   ├── ViewCoordinationTasksDto.cs
│   │   │       │   ├── ViewCoordinationTasksQuery.cs
│   │   │       │   └── ViewCoordinationTasksQueryHandler.cs
│   │   │       ├── ViewDepartmentDetails/
│   │   │       │   ├── ViewDepartmentDetailsDto.cs
│   │   │       │   ├── ViewDepartmentDetailsQuery.cs
│   │   │       │   └── ViewDepartmentDetailsQueryHandler.cs
│   │   │       ├── ViewDepartmentList/
│   │   │       │   ├── ViewDepartmentListQuery.cs
│   │   │       │   └── ViewDepartmentListQueryHandler.cs
│   │   │       └── ViewPersonnelDetails/
│   │   │           ├── ViewPersonnelDetailsDto.cs
│   │   │           ├── ViewPersonnelDetailsQuery.cs
│   │   │           └── ViewPersonnelDetailsQueryHandler.cs
│   │   ├── Documents/
│   │   │   └── Queries/
│   │   │       ├── SearchDocuments/
│   │   │       │   ├── SearchDocumentsDto.cs
│   │   │       │   ├── SearchDocumentsQuery.cs
│   │   │       │   └── SearchDocumentsQueryHandler.cs
│   │   │       ├── ViewDocumentDetail/
│   │   │       │   ├── ViewDocumentDetailDto.cs
│   │   │       │   ├── ViewDocumentDetailQuery.cs
│   │   │       │   └── ViewDocumentDetailQueryHandler.cs
│   │   │       └── ViewDocumentList/
│   │   │           ├── ViewDocumentListDto.cs
│   │   │           ├── ViewDocumentListQuery.cs
│   │   │           └── ViewDocumentListQueryHandler.cs
│   │   ├── EmailActions/
│   │   │   ├── EmailActionDisplay.cs
│   │   │   ├── EmailTokenInvalidationHelper.cs
│   │   │   ├── ExecuteEmailActionCommand.cs
│   │   │   ├── ExecuteEmailActionCommandHandler.cs
│   │   │   ├── GetEmailActionInfoQuery.cs
│   │   │   └── GetEmailActionInfoQueryHandler.cs
│   │   ├── Emails/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateEmailDraft/
│   │   │   │   │   ├── CreateEmailDraftCommand.cs
│   │   │   │   │   └── CreateEmailDraftCommandHandler.cs
│   │   │   │   ├── CreateEmailTemplate/
│   │   │   │   │   ├── CreateEmailTemplateCommand.cs
│   │   │   │   │   ├── CreateEmailTemplateCommandHandler.cs
│   │   │   │   │   ├── CreateEmailTemplateCommandValidator.cs
│   │   │   │   │   └── CreateEmailTemplateResponse.cs
│   │   │   │   ├── DiscardEmailDraft/
│   │   │   │   │   ├── DiscardEmailDraftCommand.cs
│   │   │   │   │   └── DiscardEmailDraftCommandHandler.cs
│   │   │   │   ├── EditEmailContent/
│   │   │   │   │   ├── EditEmailContentCommand.cs
│   │   │   │   │   ├── EditEmailContentCommandHandler.cs
│   │   │   │   │   ├── EditEmailContentCommandValidator.cs
│   │   │   │   │   └── EditEmailContentResponse.cs
│   │   │   │   ├── MarkEmailCompleted/
│   │   │   │   │   ├── MarkEmailCompletedCommand.cs
│   │   │   │   │   └── MarkEmailCompletedCommandHandler.cs
│   │   │   │   ├── ReplytoEmail/
│   │   │   │   │   ├── ReplytoEmailCommand.cs
│   │   │   │   │   ├── ReplytoEmailCommandHandler.cs
│   │   │   │   │   ├── ReplytoEmailCommandValidator.cs
│   │   │   │   │   └── ReplytoEmailResponse.cs
│   │   │   │   ├── SendEmail/
│   │   │   │   │   ├── SendEmailCommand.cs
│   │   │   │   │   ├── SendEmailCommandHandler.cs
│   │   │   │   │   ├── SendEmailCommandValidator.cs
│   │   │   │   │   └── SendEmailResponse.cs
│   │   │   │   ├── SendEmailDraft/
│   │   │   │   │   ├── SendEmailDraftCommand.cs
│   │   │   │   │   └── SendEmailDraftCommandHandler.cs
│   │   │   │   ├── ToggleEmailTemplateStatus/
│   │   │   │   │   ├── ToggleEmailTemplateStatusCommand.cs
│   │   │   │   │   ├── ToggleEmailTemplateStatusCommandHandler.cs
│   │   │   │   │   └── ToggleEmailTemplateStatusResponse.cs
│   │   │   │   ├── UpdateEmailDraft/
│   │   │   │   │   ├── UpdateEmailDraftCommand.cs
│   │   │   │   │   └── UpdateEmailDraftCommandHandler.cs
│   │   │   │   └── UpdateEmailTemplate/
│   │   │   │       ├── UpdateEmailTemplateCommand.cs
│   │   │   │       ├── UpdateEmailTemplateCommandHandler.cs
│   │   │   │       ├── UpdateEmailTemplateCommandValidator.cs
│   │   │   │       └── UpdateEmailTemplateResponse.cs
│   │   │   ├── Common/
│   │   │   │   ├── EmailActionTemplates.cs
│   │   │   │   ├── EmailAttachmentLoader.cs
│   │   │   │   ├── EmailComposition.cs
│   │   │   │   ├── EmailDraftMapper.cs
│   │   │   │   ├── EmailDraftModels.cs
│   │   │   │   ├── EmailDraftWriter.cs
│   │   │   │   ├── EmailOverride.cs
│   │   │   │   ├── LogisticsPriorityText.cs
│   │   │   │   └── OutboundEmailAttachments.cs
│   │   │   ├── Queries/
│   │   │   │   ├── GetEmailDraft/
│   │   │   │   │   ├── GetEmailDraftQuery.cs
│   │   │   │   │   └── GetEmailDraftQueryHandler.cs
│   │   │   │   ├── GetSentEmailsHistory/
│   │   │   │   ├── GetUnprocessedEmailCount/
│   │   │   │   │   ├── GetUnprocessedEmailCountQuery.cs
│   │   │   │   │   └── GetUnprocessedEmailCountQueryHandler.cs
│   │   │   │   ├── PreviewEmailTemplate/
│   │   │   │   │   ├── PreviewEmailTemplateQuery.cs
│   │   │   │   │   └── PreviewEmailTemplateQueryHandler.cs
│   │   │   │   ├── ViewEmail/
│   │   │   │   │   ├── ViewEmailDto.cs
│   │   │   │   │   ├── ViewEmailQuery.cs
│   │   │   │   │   └── ViewEmailQueryHandler.cs
│   │   │   │   ├── ViewEmailList/
│   │   │   │   │   ├── ViewEmailListDto.cs
│   │   │   │   │   ├── ViewEmailListQuery.cs
│   │   │   │   │   └── ViewEmailListQueryHandler.cs
│   │   │   │   ├── ViewEmailTemplateDetail/
│   │   │   │   │   ├── ViewEmailTemplateDetailDto.cs
│   │   │   │   │   ├── ViewEmailTemplateDetailQuery.cs
│   │   │   │   │   └── ViewEmailTemplateDetailQueryHandler.cs
│   │   │   │   └── ViewEmailTemplateList/
│   │   │   │       ├── ViewEmailTemplateListDto.cs
│   │   │   │       ├── ViewEmailTemplateListQuery.cs
│   │   │   │       └── ViewEmailTemplateListQueryHandler.cs
│   │   │   └── Utils/
│   │   │       └── EmailImageLayoutNormalizer.cs
│   │   ├── Faqs/
│   │   │   ├── Commands/
│   │   │   │   ├── ChangeFAQVisibility/
│   │   │   │   │   ├── ChangeFAQVisibilityCommand.cs
│   │   │   │   │   ├── ChangeFAQVisibilityCommandHandler.cs
│   │   │   │   │   ├── ChangeFAQVisibilityCommandValidator.cs
│   │   │   │   │   └── ChangeFAQVisibilityResponse.cs
│   │   │   │   ├── CreateFAQ/
│   │   │   │   │   ├── CreateFAQCommand.cs
│   │   │   │   │   ├── CreateFAQCommandHandler.cs
│   │   │   │   │   ├── CreateFAQCommandValidator.cs
│   │   │   │   │   └── CreateFAQResponse.cs
│   │   │   │   └── UpdateFAQ/
│   │   │   │       ├── UpdateFAQCommand.cs
│   │   │   │       ├── UpdateFAQCommandHandler.cs
│   │   │   │       ├── UpdateFAQCommandValidator.cs
│   │   │   │       └── UpdateFAQResponse.cs
│   │   │   └── Queries/
│   │   │       ├── SearchFAQ/
│   │   │       │   ├── SearchFAQDto.cs
│   │   │       │   ├── SearchFAQQuery.cs
│   │   │       │   └── SearchFAQQueryHandler.cs
│   │   │       ├── ViewFAQDetail/
│   │   │       │   ├── ViewFAQDetailDto.cs
│   │   │       │   ├── ViewFAQDetailQuery.cs
│   │   │       │   └── ViewFAQDetailQueryHandler.cs
│   │   │       └── ViewListFAQ/
│   │   │           ├── ViewListFAQDto.cs
│   │   │           ├── ViewListFAQQuery.cs
│   │   │           ├── ViewListFAQQueryHandler.cs
│   │   │           └── ViewListFAQQueryValidator.cs
│   │   ├── Feedbacks/
│   │   │   └── Queries/
│   │   │       ├── SearchAndFilterFeedback/
│   │   │       │   ├── SearchAndFilterFeedbackDto.cs
│   │   │       │   ├── SearchAndFilterFeedbackQuery.cs
│   │   │       │   └── SearchAndFilterFeedbackQueryHandler.cs
│   │   │       └── ViewFeedbackSummary/
│   │   │           ├── ViewFeedbackSummaryDto.cs
│   │   │           ├── ViewFeedbackSummaryQuery.cs
│   │   │           └── ViewFeedbackSummaryQueryHandler.cs
│   │   ├── Files/
│   │   │   ├── Commands/
│   │   │   │   └── UploadFile/
│   │   │   │       ├── UploadFileCommand.cs
│   │   │   │       └── UploadFileCommandHandler.cs
│   │   │   └── Queries/
│   │   │       └── GetFileContent/
│   │   │           ├── GetFileContentQuery.cs
│   │   │           └── GetFileContentQueryHandler.cs
│   │   ├── Galleries/
│   │   │   ├── Commands/
│   │   │   │   ├── AddGalleryItem/
│   │   │   │   │   ├── AddGalleryItemCommand.cs
│   │   │   │   │   ├── AddGalleryItemCommandHandler.cs
│   │   │   │   │   └── AddGalleryItemCommandValidator.cs
│   │   │   │   ├── ChangeGalleryItemStatus/
│   │   │   │   │   ├── ChangeGalleryItemStatusCommand.cs
│   │   │   │   │   ├── ChangeGalleryItemStatusCommandHandler.cs
│   │   │   │   │   ├── ChangeGalleryItemStatusCommandValidator.cs
│   │   │   │   │   └── ChangeGalleryItemStatusResponse.cs
│   │   │   │   ├── ChangeGalleryLocationStatus/
│   │   │   │   │   ├── ChangeGalleryLocationStatusCommand.cs
│   │   │   │   │   ├── ChangeGalleryLocationStatusCommandHandler.cs
│   │   │   │   │   └── ChangeGalleryLocationStatusCommandValidator.cs
│   │   │   │   ├── CreateGalleryLocation/
│   │   │   │   │   ├── CreateGalleryLocationCommand.cs
│   │   │   │   │   ├── CreateGalleryLocationCommandHandler.cs
│   │   │   │   │   └── CreateGalleryLocationCommandValidator.cs
│   │   │   │   ├── DeleteGalleryItem/
│   │   │   │   │   ├── DeleteGalleryItemCommand.cs
│   │   │   │   │   ├── DeleteGalleryItemCommandHandler.cs
│   │   │   │   │   ├── DeleteGalleryItemCommandValidator.cs
│   │   │   │   │   └── DeleteGalleryItemResponse.cs
│   │   │   │   ├── UpdateGalleryItem/
│   │   │   │   │   ├── UpdateGalleryItemCommand.cs
│   │   │   │   │   ├── UpdateGalleryItemCommandHandler.cs
│   │   │   │   │   └── UpdateGalleryItemCommandValidator.cs
│   │   │   │   └── UpdateGalleryLocation/
│   │   │   │       ├── UpdateGalleryLocationCommand.cs
│   │   │   │       ├── UpdateGalleryLocationCommandHandler.cs
│   │   │   │       └── UpdateGalleryLocationCommandValidator.cs
│   │   │   ├── Common/
│   │   │   │   ├── GalleryDetailBuilder.cs
│   │   │   │   ├── GalleryErrorCodes.cs
│   │   │   │   ├── GalleryFileUrls.cs
│   │   │   │   ├── GalleryItemDetailDto.cs
│   │   │   │   ├── GalleryItemListItemDto.cs
│   │   │   │   ├── GalleryItemListQueryExecutor.cs
│   │   │   │   ├── GalleryKeyNormalizer.cs
│   │   │   │   ├── GalleryLocationDetailBuilder.cs
│   │   │   │   ├── GalleryLocationDtos.cs
│   │   │   │   ├── GalleryLocationGuard.cs
│   │   │   │   ├── GalleryLocationModes.cs
│   │   │   │   ├── GalleryLocationWriteGuard.cs
│   │   │   │   ├── GalleryMediaClassifier.cs
│   │   │   │   ├── GalleryUploadFileCommandDto.cs
│   │   │   │   ├── IGalleryItemListCriteria.cs
│   │   │   │   └── StaffLeaderGalleryScope.cs
│   │   │   ├── Public/
│   │   │   │   ├── Common/
│   │   │   │   │   ├── PublicGalleryDtos.cs
│   │   │   │   │   └── PublicGalleryFileUrls.cs
│   │   │   │   └── Queries/
│   │   │   │       ├── GetPublicCampusNavigation/
│   │   │   │       │   ├── GetPublicCampusNavigationQuery.cs
│   │   │   │       │   └── GetPublicCampusNavigationQueryHandler.cs
│   │   │   │       ├── GetPublicCampuses/
│   │   │   │       │   ├── GetPublicCampusesQuery.cs
│   │   │   │       │   └── GetPublicCampusesQueryHandler.cs
│   │   │   │       ├── GetPublicGalleryItemDetail/
│   │   │   │       │   ├── GetPublicGalleryItemDetailQuery.cs
│   │   │   │       │   └── GetPublicGalleryItemDetailQueryHandler.cs
│   │   │   │       ├── GetPublicGalleryMedia/
│   │   │   │       │   ├── GetPublicGalleryMediaQuery.cs
│   │   │   │       │   └── GetPublicGalleryMediaQueryHandler.cs
│   │   │   │       └── GetPublicLocationGalleryItem/
│   │   │   │           ├── GetPublicLocationGalleryItemQuery.cs
│   │   │   │           └── GetPublicLocationGalleryItemQueryHandler.cs
│   │   │   └── Queries/
│   │   │       ├── GetGalleryFilterOptions/
│   │   │       │   ├── GetGalleryFilterOptionsQuery.cs
│   │   │       │   └── GetGalleryFilterOptionsQueryHandler.cs
│   │   │       ├── SearchGalleryItems/
│   │   │       │   ├── SearchGalleryItemsQuery.cs
│   │   │       │   └── SearchGalleryItemsQueryHandler.cs
│   │   │       ├── ViewGalleryItemDetails/
│   │   │       │   ├── ViewGalleryItemDetailsQuery.cs
│   │   │       │   └── ViewGalleryItemDetailsQueryHandler.cs
│   │   │       ├── ViewGalleryItemList/
│   │   │       │   ├── ViewGalleryItemListQuery.cs
│   │   │       │   └── ViewGalleryItemListQueryHandler.cs
│   │   │       └── ViewGalleryLocationList/
│   │   │           ├── ViewGalleryLocationListQuery.cs
│   │   │           └── ViewGalleryLocationListQueryHandler.cs
│   │   ├── MeetingMinutes/
│   │   │   └── Queries/
│   │   │       ├── SearchAndFilterMinutes/
│   │   │       │   ├── SearchAndFilterMinutesDto.cs
│   │   │       │   ├── SearchAndFilterMinutesQuery.cs
│   │   │       │   └── SearchAndFilterMinutesQueryHandler.cs
│   │   │       └── ViewMinutesList/
│   │   │           ├── ViewMinutesListDto.cs
│   │   │           ├── ViewMinutesListQuery.cs
│   │   │           └── ViewMinutesListQueryHandler.cs
│   │   ├── News/
│   │   │   ├── Commands/
│   │   │   │   ├── AddMultilingualNews/
│   │   │   │   │   ├── AddMultilingualNewsCommand.cs
│   │   │   │   │   ├── AddMultilingualNewsCommandHandler.cs
│   │   │   │   │   ├── AddMultilingualNewsCommandValidator.cs
│   │   │   │   │   └── AddMultilingualNewsResponse.cs
│   │   │   │   ├── ApproveNews/
│   │   │   │   │   ├── ApproveNewsCommand.cs
│   │   │   │   │   ├── ApproveNewsCommandHandler.cs
│   │   │   │   │   ├── ApproveNewsCommandValidator.cs
│   │   │   │   │   └── ApproveNewsResponse.cs
│   │   │   │   ├── CreateNews/
│   │   │   │   │   ├── CreateNewsCommand.cs
│   │   │   │   │   ├── CreateNewsCommandHandler.cs
│   │   │   │   │   ├── CreateNewsCommandValidator.cs
│   │   │   │   │   └── CreateNewsResponse.cs
│   │   │   │   ├── EditNews/
│   │   │   │   │   ├── EditNewsCommand.cs
│   │   │   │   │   ├── EditNewsCommandHandler.cs
│   │   │   │   │   ├── EditNewsCommandValidator.cs
│   │   │   │   │   └── EditNewsResponse.cs
│   │   │   │   ├── ManageNewsVisibility/
│   │   │   │   │   ├── ManageNewsVisibilityCommand.cs
│   │   │   │   │   ├── ManageNewsVisibilityCommandHandler.cs
│   │   │   │   │   ├── ManageNewsVisibilityCommandValidator.cs
│   │   │   │   │   └── ManageNewsVisibilityResponse.cs
│   │   │   │   ├── PublishNews/
│   │   │   │   │   ├── PublishNewsCommand.cs
│   │   │   │   │   ├── PublishNewsCommandHandler.cs
│   │   │   │   │   ├── PublishNewsCommandValidator.cs
│   │   │   │   │   └── PublishNewsResponse.cs
│   │   │   │   └── UploadNewsCoverImage/
│   │   │   │       ├── UploadNewsCoverImageCommand.cs
│   │   │   │       ├── UploadNewsCoverImageCommandHandler.cs
│   │   │   │       └── UploadNewsCoverImageResponse.cs
│   │   │   └── Queries/
│   │   │       ├── GetEligibleVisitInstancesForNews/
│   │   │       │   ├── GetEligibleVisitInstancesForNewsDto.cs
│   │   │       │   ├── GetEligibleVisitInstancesForNewsQuery.cs
│   │   │       │   └── GetEligibleVisitInstancesForNewsQueryHandler.cs
│   │   │       ├── ViewNewsDetails/
│   │   │       │   ├── ViewNewsDetailsDto.cs
│   │   │       │   ├── ViewNewsDetailsQuery.cs
│   │   │       │   └── ViewNewsDetailsQueryHandler.cs
│   │   │       └── ViewNewsList/
│   │   │           ├── ViewNewsListDto.cs
│   │   │           ├── ViewNewsListQuery.cs
│   │   │           ├── ViewNewsListQueryHandler.cs
│   │   │           └── ViewNewsListQueryValidator.cs
│   │   ├── Notifications/
│   │   │   ├── Commands/
│   │   │   │   ├── MarkAllNotificationsAsRead/
│   │   │   │   │   ├── MarkAllNotificationsAsReadCommand.cs
│   │   │   │   │   ├── MarkAllNotificationsAsReadCommandHandler.cs
│   │   │   │   │   └── MarkAllNotificationsAsReadResponse.cs
│   │   │   │   └── MarkNotificationAsRead/
│   │   │   │       ├── MarkNotificationAsReadCommand.cs
│   │   │   │       └── MarkNotificationAsReadCommandHandler.cs
│   │   │   ├── Common/
│   │   │   │   ├── INotificationService.cs
│   │   │   │   ├── INotificationTargetResolver.cs
│   │   │   │   ├── NotificationConstants.cs
│   │   │   │   ├── NotificationDto.cs
│   │   │   │   ├── NotificationService.cs
│   │   │   │   └── NotificationTargetResolver.cs
│   │   │   └── Queries/
│   │   │       ├── GetMyNotifications/
│   │   │       │   ├── GetMyNotificationsQuery.cs
│   │   │       │   └── GetMyNotificationsQueryHandler.cs
│   │   │       └── GetMyUnreadNotificationCount/
│   │   │           ├── GetMyUnreadNotificationCountQuery.cs
│   │   │           ├── GetMyUnreadNotificationCountQueryHandler.cs
│   │   │           └── UnreadNotificationCountResponse.cs
│   │   ├── Partners/
│   │   │   ├── Commands/
│   │   │   │   ├── EditPartnerInformation/
│   │   │   │   │   ├── EditPartnerInformationCommand.cs
│   │   │   │   │   ├── EditPartnerInformationCommandHandler.cs
│   │   │   │   │   ├── EditPartnerInformationCommandValidator.cs
│   │   │   │   │   └── EditPartnerInformationResponse.cs
│   │   │   │   └── ProcessPartnerCreationRequest/
│   │   │   │       ├── ProcessPartnerCreationRequestCommand.cs
│   │   │   │       ├── ProcessPartnerCreationRequestCommandHandler.cs
│   │   │   │       ├── ProcessPartnerCreationRequestCommandValidator.cs
│   │   │   │       └── ProcessPartnerCreationRequestResponse.cs
│   │   │   ├── Dtos/
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── PartnersMappingProfile.cs
│   │   │   ├── Queries/
│   │   │   │   ├── SearchPartners/
│   │   │   │   │   ├── SearchPartnersDto.cs
│   │   │   │   │   ├── SearchPartnersQuery.cs
│   │   │   │   │   └── SearchPartnersQueryHandler.cs
│   │   │   │   ├── SearchPublicPartnerOptions/
│   │   │   │   │   ├── PublicPartnerOptionDto.cs
│   │   │   │   │   ├── SearchPublicPartnerOptionsQuery.cs
│   │   │   │   │   ├── SearchPublicPartnerOptionsQueryHandler.cs
│   │   │   │   │   └── SearchPublicPartnerOptionsQueryValidator.cs
│   │   │   │   ├── ViewPartnerDetails/
│   │   │   │   │   ├── ViewPartnerDetailsDto.cs
│   │   │   │   │   ├── ViewPartnerDetailsQuery.cs
│   │   │   │   │   └── ViewPartnerDetailsQueryHandler.cs
│   │   │   │   └── ViewPartnerLists/
│   │   │   │       ├── ViewPartnerListsDto.cs
│   │   │   │       ├── ViewPartnerListsQuery.cs
│   │   │   │       └── ViewPartnerListsQueryHandler.cs
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── Profiles/
│   │   │   ├── Commands/
│   │   │   │   ├── ChangePassword/
│   │   │   │   │   ├── ChangePasswordCommandHandlerProfile.cs
│   │   │   │   │   ├── ChangePasswordCommandProfile.cs
│   │   │   │   │   ├── ChangePasswordCommandValidatorProfile.cs
│   │   │   │   │   └── ChangePasswordResponse.cs
│   │   │   │   ├── UpdateProfile/
│   │   │   │   │   ├── UpdateProfileCommand.cs
│   │   │   │   │   ├── UpdateProfileCommandHandler.cs
│   │   │   │   │   └── UpdateProfileCommandValidator.cs
│   │   │   │   └── UploadProfileAvatar/
│   │   │   │       ├── UploadProfileAvatarCommand.cs
│   │   │   │       ├── UploadProfileAvatarCommandHandler.cs
│   │   │   │       └── UploadProfileAvatarResponse.cs
│   │   │   ├── Common/
│   │   │   │   ├── ProfileResponse.cs
│   │   │   │   └── ProfileResponseBuilder.cs
│   │   │   ├── Dtos/
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── ProfilesMappingProfile.cs
│   │   │   ├── Queries/
│   │   │   │   └── ViewProfile/
│   │   │   │       ├── ViewProfileQuery.cs
│   │   │   │       └── ViewProfileQueryHandler.cs
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── PublicContent/
│   │   │   ├── Commands/
│   │   │   │   └── MarkNotificationsRead/
│   │   │   │       ├── MarkNotificationsReadCommand.cs
│   │   │   │       └── MarkNotificationsReadCommandHandler.cs
│   │   │   ├── Dtos/
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── PublicContentMappingProfile.cs
│   │   │   ├── Queries/
│   │   │   │   ├── SearchInformation/
│   │   │   │   │   ├── SearchInformationDto.cs
│   │   │   │   │   ├── SearchInformationQuery.cs
│   │   │   │   │   └── SearchInformationQueryHandler.cs
│   │   │   │   ├── ViewContactInfo/
│   │   │   │   │   ├── ViewContactInfoDto.cs
│   │   │   │   │   ├── ViewContactInfoQuery.cs
│   │   │   │   │   └── ViewContactInfoQueryHandler.cs
│   │   │   │   ├── ViewFaq/
│   │   │   │   │   ├── ViewFaqDto.cs
│   │   │   │   │   ├── ViewFaqQuery.cs
│   │   │   │   │   ├── ViewFaqQueryHandler.cs
│   │   │   │   │   └── ViewFaqQueryValidator.cs
│   │   │   │   ├── ViewGallery/
│   │   │   │   │   ├── ViewGalleryDto.cs
│   │   │   │   │   ├── ViewGalleryQuery.cs
│   │   │   │   │   └── ViewGalleryQueryHandler.cs
│   │   │   │   ├── ViewHomepage/
│   │   │   │   │   ├── ViewHomepageDto.cs
│   │   │   │   │   ├── ViewHomepageQuery.cs
│   │   │   │   │   └── ViewHomepageQueryHandler.cs
│   │   │   │   ├── ViewNews/
│   │   │   │   │   ├── ViewNewsDto.cs
│   │   │   │   │   ├── ViewNewsQuery.cs
│   │   │   │   │   └── ViewNewsQueryHandler.cs
│   │   │   │   ├── ViewNotifications/
│   │   │   │   │   ├── ViewNotificationsDto.cs
│   │   │   │   │   ├── ViewNotificationsQuery.cs
│   │   │   │   │   └── ViewNotificationsQueryHandler.cs
│   │   │   │   ├── ViewPartners/
│   │   │   │   │   ├── ViewPartnersDto.cs
│   │   │   │   │   ├── ViewPartnersQuery.cs
│   │   │   │   │   └── ViewPartnersQueryHandler.cs
│   │   │   │   ├── ViewPolicyAndTerms/
│   │   │   │   │   ├── ViewPolicyAndTermsDto.cs
│   │   │   │   │   ├── ViewPolicyAndTermsQuery.cs
│   │   │   │   │   └── ViewPolicyAndTermsQueryHandler.cs
│   │   │   │   └── ViewPublicNewsDetail/
│   │   │   │       ├── ViewPublicNewsDetailDto.cs
│   │   │   │       ├── ViewPublicNewsDetailQuery.cs
│   │   │   │       └── ViewPublicNewsDetailQueryHandler.cs
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── Reports/
│   │   │   ├── Commands/
│   │   │   │   └── ExportStatisticsReport/
│   │   │   │       ├── ExportStatisticsReportCommand.cs
│   │   │   │       ├── ExportStatisticsReportCommandHandler.cs
│   │   │   │       ├── ExportStatisticsReportCommandValidator.cs
│   │   │   │       └── ExportStatisticsReportResponse.cs
│   │   │   └── Queries/
│   │   │       ├── FilterDashboardByTime/
│   │   │       │   ├── FilterDashboardByTimeDto.cs
│   │   │       │   ├── FilterDashboardByTimeQuery.cs
│   │   │       │   └── FilterDashboardByTimeQueryHandler.cs
│   │   │       └── ViewDashboardStatistics/
│   │   │           ├── ViewDashboardStatisticsDto.cs
│   │   │           ├── ViewDashboardStatisticsQuery.cs
│   │   │           └── ViewDashboardStatisticsQueryHandler.cs
│   │   ├── bin/   [excluded]
│   │   ├── obj/   [excluded]
│   │   ├── DependencyInjection.cs
│   │   └── PEMS.Application.csproj
│   ├── PEMS.Domain/
│   │   ├── .tmp-build/   [excluded]
│   │   ├── Common/
│   │   │   ├── AuditableEntity.cs
│   │   │   ├── BaseEntity.cs
│   │   │   ├── DomainEvent.cs
│   │   │   └── SoftDeleteEntity.cs
│   │   ├── Constants/
│   │   │   ├── AuthConstants.cs
│   │   │   ├── EmailActionConstants.cs
│   │   │   ├── FaqConstants.cs
│   │   │   ├── LogisticsHandoverConstants.cs
│   │   │   ├── NewsConstants.cs
│   │   │   ├── NotificationRelatedTypes.cs
│   │   │   ├── NotificationTypes.cs
│   │   │   ├── VisitParticipantConstants.cs
│   │   │   ├── VisitRequestConstants.cs
│   │   │   └── VisitTypes.cs
│   │   ├── Entities/
│   │   │   ├── AgendaTemplates/
│   │   │   │   ├── AgendaTemplate.cs
│   │   │   │   ├── AgendaTemplateDefault.cs
│   │   │   │   └── AgendaTemplateItem.cs
│   │   │   ├── ApiIntegrations/
│   │   │   │   ├── ApiConfiguration.cs
│   │   │   │   ├── ApiConfigurationHeader.cs
│   │   │   │   ├── ApiRequestLog.cs
│   │   │   │   └── ApiUsageQuota.cs
│   │   │   ├── Calendar/
│   │   │   │   ├── CalendarEvent.cs
│   │   │   │   ├── CalendarEventAttendee.cs
│   │   │   │   └── CalendarEventReminder.cs
│   │   │   ├── Campuses/
│   │   │   │   └── Campus.cs
│   │   │   ├── Delegations/
│   │   │   │   ├── VisitAgenda.cs
│   │   │   │   ├── VisitGuestMember.cs
│   │   │   │   ├── VisitInstanceReminderSetting.cs
│   │   │   │   ├── VisitLogisticsAssignmentAttempt.cs
│   │   │   │   ├── VisitLogisticsItem.cs
│   │   │   │   ├── VisitLogisticsItemHandover.cs
│   │   │   │   ├── VisitParticipant.cs
│   │   │   │   ├── VisitRequest.cs
│   │   │   │   └── VisitRequestCampus.cs
│   │   │   ├── Departments/
│   │   │   │   └── Department.cs
│   │   │   ├── Documents/
│   │   │   │   ├── Document.cs
│   │   │   │   └── UploadedFile.cs
│   │   │   ├── Emails/
│   │   │   │   ├── EmailActionToken.cs
│   │   │   │   ├── EmailDraft.cs
│   │   │   │   ├── EmailDraftAttachment.cs
│   │   │   │   ├── EmailDraftRecipient.cs
│   │   │   │   ├── EmailTemplate.cs
│   │   │   │   ├── SentEmail.cs
│   │   │   │   ├── SentEmailAttachment.cs
│   │   │   │   └── SentEmailRecipient.cs
│   │   │   ├── Faqs/
│   │   │   │   └── Faq.cs
│   │   │   ├── Feedbacks/
│   │   │   │   ├── Feedback.cs
│   │   │   │   └── FeedbackRatingItem.cs
│   │   │   ├── Galleries/
│   │   │   │   ├── GalleryArea.cs
│   │   │   │   ├── GalleryItem.cs
│   │   │   │   ├── GalleryItemMedia.cs
│   │   │   │   ├── GalleryLocation.cs
│   │   │   │   └── PhotoFaceTag.cs
│   │   │   ├── Minutes/
│   │   │   │   ├── Minute.cs
│   │   │   │   ├── MinuteActionItem.cs
│   │   │   │   └── MinuteParticipant.cs
│   │   │   ├── News/
│   │   │   │   ├── News.cs
│   │   │   │   ├── NewsContentSection.cs
│   │   │   │   ├── NewsSectionFile.cs
│   │   │   │   └── NewsTranslation.cs
│   │   │   ├── Notifications/
│   │   │   │   └── Notification.cs
│   │   │   ├── Partners/
│   │   │   │   ├── Partner.cs
│   │   │   │   └── Partnercontact.cs
│   │   │   └── Users/
│   │   │       ├── AuditLog.cs
│   │   │       ├── AuditLogChange.cs
│   │   │       ├── LoginLog.cs
│   │   │       ├── OtpToken.cs
│   │   │       ├── Role.cs
│   │   │       ├── SecurityEvent.cs
│   │   │       ├── User.cs
│   │   │       ├── UserAuthProvider.cs
│   │   │       └── UserSession.cs
│   │   ├── Enums/
│   │   │   ├── AccountStatus.cs
│   │   │   ├── ApiIntegrationStatus.cs
│   │   │   ├── CampusStatus.cs
│   │   │   ├── CancellationActorType.cs
│   │   │   ├── CancellationSource.cs
│   │   │   ├── DecisionActorRole.cs
│   │   │   ├── DelegationStatus.cs
│   │   │   ├── DepartmentStatus.cs
│   │   │   ├── EmailAttachmentType.cs
│   │   │   ├── EmailBodyFormat.cs
│   │   │   ├── EmailDraftStatus.cs
│   │   │   ├── FaqVisibilityStatus.cs
│   │   │   ├── Gender.cs
│   │   │   ├── LogisticsItemStatus.cs
│   │   │   ├── MinuteStatus.cs
│   │   │   ├── NewEnums.cs
│   │   │   ├── NewsStatus.cs
│   │   │   ├── OtpPurpose.cs
│   │   │   ├── SubRole.cs
│   │   │   ├── UserCreatedVia.cs
│   │   │   ├── UserRoleCode.cs
│   │   │   ├── VisitInstanceStatus.cs
│   │   │   ├── VisitReminderChannel.cs
│   │   │   ├── VisitReminderStatus.cs
│   │   │   ├── VisitReminderTargetGroup.cs
│   │   │   ├── VisitRequestStatus.cs
│   │   │   └── VisitScope.cs
│   │   ├── Events/
│   │   │   ├── AccountCreatedEvent.cs
│   │   │   ├── DelegationClosedEvent.cs
│   │   │   ├── NewsApprovedEvent.cs
│   │   │   ├── ResourceRequestApprovedEvent.cs
│   │   │   ├── VisitRequestApprovedEvent.cs
│   │   │   └── VisitRequestSubmittedEvent.cs
│   │   ├── ValueObjects/
│   │   │   ├── Address.cs
│   │   │   ├── DateRange.cs
│   │   │   ├── EmailAddress.cs
│   │   │   ├── FileMetadata.cs
│   │   │   └── PhoneNumber.cs
│   │   ├── bin/   [excluded]
│   │   ├── obj/   [excluded]
│   │   └── PEMS.Domain.csproj
│   ├── PEMS.Infrastructure/
│   │   ├── .tmp-build/   [excluded]
│   │   ├── BackgroundJobs/
│   │   │   └── VisitReminderDispatchHostedService.cs
│   │   ├── Common/
│   │   │   └── DateTimeService.cs
│   │   ├── Email/
│   │   │   ├── EmailActionTokenService.cs
│   │   │   ├── EmailService.cs
│   │   │   ├── EmailTemplateRenderer.cs
│   │   │   └── SmtpEmailSender.cs
│   │   ├── ExternalServices/
│   │   │   ├── ApiClient/
│   │   │   │   └── ExternalApiClient.cs
│   │   │   ├── Calendar/
│   │   │   │   └── CalendarIntegrationService.cs
│   │   │   ├── FaceRecognition/
│   │   │   │   └── FaceRecognitionService.cs
│   │   │   └── Ocr/
│   │   │       └── OcrService.cs
│   │   ├── FileStorage/
│   │   │   ├── GoogleDrive/
│   │   │   │   ├── GoogleDriveFolderResolver.cs
│   │   │   │   └── GoogleDriveStorageService.cs
│   │   │   ├── CloudFileStorageService.cs
│   │   │   ├── FileValidationService.cs
│   │   │   ├── LocalFileStorageService.cs
│   │   │   └── VirusScanService.cs
│   │   ├── Idempotency/
│   │   │   └── IdempotencyService.cs
│   │   ├── Identity/
│   │   │   ├── CurrentUserService.cs
│   │   │   ├── FeidIdentityVerifier.cs
│   │   │   ├── GoogleTokenValidator.cs
│   │   │   ├── JwtTokenService.cs
│   │   │   ├── NotificationService.cs
│   │   │   ├── OtpService.cs
│   │   │   ├── OwnershipChecker.cs
│   │   │   ├── PasswordHasher.cs
│   │   │   ├── RefreshTokenStore.cs
│   │   │   ├── SecureTokenGenerator.cs
│   │   │   └── SessionService.cs
│   │   ├── Logging/
│   │   │   ├── ApiRequestLogService.cs
│   │   │   ├── AuditLogService.cs
│   │   │   └── SecurityAuditService.cs
│   │   ├── Persistence/
│   │   │   ├── Configurations/
│   │   │   │   └── UserConfiguration.cs
│   │   │   ├── Repositories/
│   │   │   │   ├── CampusRepository.cs
│   │   │   │   ├── DelegationRepository.cs
│   │   │   │   ├── DocumentRepository.cs
│   │   │   │   ├── GenericRepository.cs
│   │   │   │   ├── PartnerRepository.cs
│   │   │   │   ├── ReportRepository.cs
│   │   │   │   └── UserRepository.cs
│   │   │   ├── ApplicationDbContext.cs
│   │   │   └── ApplicationDbContextFactory.cs
│   │   ├── RateLimiting/
│   │   │   ├── InMemoryRateLimitStore.cs
│   │   │   ├── RateLimitService.cs
│   │   │   └── RedisRateLimitStore.cs
│   │   ├── Security/
│   │   │   └── HtmlSanitizerService.cs
│   │   ├── Services/
│   │   │   ├── ApprovalRoutingService.cs
│   │   │   ├── UserProvisionService.cs
│   │   │   └── VisitRequestService.cs
│   │   ├── bin/   [excluded]
│   │   ├── obj/   [excluded]
│   │   ├── DependencyInjection.cs
│   │   └── PEMS.Infrastructure.csproj
│   ├── handlers.txt
│   └── handlers_utf8.txt
├── docs/
│   ├── CampusManagement/
│   │   ├── 00_CAMPUS_MANAGEMENT_COMMON_RULES_HO.md
│   │   ├── 01_UC82_VIEW_CAMPUS_LIST_HO.md
│   │   ├── 02_UC83_SEARCH_FILTER_CAMPUS_HO.md
│   │   ├── 03_UC81_CREATE_CAMPUS_HO.md
│   │   ├── 04_UC84_VIEW_CAMPUS_DETAILS_HO.md
│   │   ├── 05_UC85_UPDATE_CAMPUS_HO.md
│   │   └── 06_UC86_MANAGE_CAMPUS_STATUS_HO.md
│   ├── Department/
│   │   ├── PEMS_DEPARTMENT_PERSONNEL_SHORT_FUNCTION_PROMPT.md
│   │   ├── PEMS_DEPTLEAD_DASHBOARD_EMAIL_LOCAL_DRAFT_PROMPT.md
│   │   ├── PEMS_DEPTLEAD_UI_RESTORE_ACTIONS_PROMPT - Copy.md
│   │   ├── PEMS_DEPTLEAD_UI_RESTORE_ACTIONS_PROMPT.md
│   │   ├── PEMS_DEPT_LEADER_ASSIGNMENT_PROGRESS_UNIFIED_PROMPT.md
│   │   ├── PEMS_DEPT_LEADER_LOGISTICS_ASSIGNMENT_FLOW_PROMPT.md
│   │   ├── PEMS_DEPT_LEADER_STATUS_LOGIC_UPDATE_PROMPT.md
│   │   ├── PEMS_DEPT_RECEPTION_TASKS_REAL_DATA_PROMPT.md
│   │   └── PEMS_EMAIL_MANAGEMENT_REAL_DATA_WORKFLOW_PROMPT.md
│   ├── Department_Staff_Leader/
│   │   ├── UC-101_ADD_NEW_DEPARTMENT_STAFF_LEADER.md
│   │   ├── UC-102_UPDATE_DEPARTMENT_STAFF_LEADER.md
│   │   ├── UC-103_SEARCH_FILTER_DEPARTMENTS_STAFF_LEADER.md
│   │   ├── UC-104_VIEW_DEPARTMENT_LIST_STAFF_LEADER.md
│   │   ├── UC-105_VIEW_DEPARTMENT_DETAILS_STAFF_LEADER.md
│   │   └── UC-106_MANAGE_DEPARTMENT_STATUS_STAFF_LEADER.md
│   ├── GUIDE CLAUDE/
│   │   ├── FRONTEND/
│   │   │   └── PEMS_UI_DESIGN_SYSTEM_PROMPT.md
│   │   ├── architecture/
│   │   │   └── CLEAN_ARCHITECTURE.md
│   │   └── PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── GalleryManagement/
│   │   ├── PROMPT_UI_PUBLIC_GALLERY_LOCATION_GRID_REDESIGN.md
│   │   ├── PROMPT_UPDATE_GALLERY_ALLOW_MULTIPLE_ITEMS_PER_LOCATION.md
│   │   ├── UC_Public_VisitFPTU_Gallery.md
│   │   ├── UC_Public_VisitFPTU_Gallery_Location_Grid.md
│   │   ├── UC_Quan_Ly_Khu_Vuc_Gallery_UPDATED_FINAL.md
│   │   └── UC_Quan_Ly_VisitFPTU_Gallery.md
│   ├── GoogleDrive/
│   │   ├── PEMS_GOOGLE_DRIVE_OAUTH_REFRESH_TOKEN_FLOW.md
│   │   ├── PEMS_GOOGLE_DRIVE_STORAGE_FOUNDATION_REFACTOR_FOR_FUTURE_UPLOADS.md
│   │   └── PEMS_GOOGLE_DRIVE_UPLOAD_FOUNDATION_DONE_AND_HOWTO.md
│   ├── ProfileManagement/
│   │   ├── 00_README_PROFILE_UC_IMPLEMENTATION.md
│   │   ├── 01_UC14_VIEW_PROFILE_SPEC.md
│   │   ├── 02_UC15_UPDATE_PROFILE_TEXT_SPEC.md
│   │   ├── 06_BACKEND_IMPLEMENTATION_CHECKLIST.md
│   │   ├── 07_FRONTEND_IMPLEMENTATION_CHECKLIST.md
│   │   ├── 08_TEST_CASES_AND_ACCEPTANCE_CRITERIA.md
│   │   ├── PEMS_SYNC_AVATAR_HEADER_SIDEBAR_AND_SIDEBAR_LOGO_HOME.md
│   │   ├── PEMS_UC15_UPLOAD_PROFILE_AVATAR_GOOGLE_DRIVE.md
│   │   └── PEMS_UPLOAD_AVATAR_CHECKSUM_SHA256.md
│   ├── Prompt/
│   │   ├── PROMPT_CODE_CREATE_NEWS_BACKEND.md
│   │   ├── PROMPT_CODE_UC05_VIEW_FAQ_BACKEND_UPDATED_PROJECT_STRUCTURE.md
│   │   ├── PROMPT_CODE_UC62_VIEW_LIST_FAQ_BACKEND.md
│   │   ├── PROMPT_CODE_UC63_CREATE_FAQ_BACKEND.md
│   │   ├── PROMPT_CODE_UC64_UPDATE_FAQ_BACKEND.md
│   │   └── PROMPT_CODE_UC88_VIEW_NEWS_LIST_BACKEND.md
│   ├── account-management/
│   │   ├── PROMPT_UC95_UC99_ACCOUNT_LIST_SEARCH_FILTER_PEMS.md
│   │   ├── UC95_UC99_ACCOUNT_LIST_SEARCH_FILTER.md
│   │   └── UC_StaffLeader_Related_Visitor_Accounts_Tab.md
│   ├── architecture/
│   │   └── PROJECT_STRUCTURE_FULL.md
│   ├── auth/
│   │   ├── AUTH_ERROR_CODES.md
│   │   ├── AUTH_HARDENING_REPORT.md
│   │   └── AUTH_HARDENING_TEST_CASES.md
│   ├── authentication/
│   │   ├── PEMS_AUTH_HARDENING_REMAINING_PROMPT.md
│   │   ├── PEMS_CORE_AUTH_BACKEND_DUAL_PORTAL_IMPLEMENTATION_PROMPT.md
│   │   ├── PEMS_ROLE_BASED_FRONTEND_RBAC_PROMPT.md
│   │   ├── PROMPT_IMPLEMENT_AUTH_HARDENING_TODOS.md
│   │   └── PROMPT_SUA_LOGIN_SSO_FIRST_DUAL_PORTAL_PEMS.md
│   ├── business-rules/
│   │   └── department-task-logistics-email-token-flow.md
│   ├── database/
│   │   ├── Table/
│   │   │   └── PEMS_v8_4_refined_v6_v10_FULL_SQL_TABLE_FIELD_DICTIONARY.docx
│   │   ├── scripts/
│   │   │   ├── DbSeeder/
│   │   │   │   ├── bin/   [excluded]
│   │   │   │   ├── obj/   [excluded]
│   │   │   │   ├── DbSeeder.csproj
│   │   │   │   └── Program.cs
│   │   │   ├── cleanup_expired_user_sessions.sql
│   │   │   ├── pems_full_v10_new_final_visit_lifecycle_cancel_rules_fixed.sql
│   │   │   └── seed_visitor_closed_news_feedback.sql
│   │   ├── DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
│   │   └── PROMPT_UPDATE_CODE_FOR_SQL_V10_PEMS.md
│   ├── delegation/
│   │   ├── UC17_submitform/
│   │   │   ├── PROMPT_AUDIT_SYNC_UC17_WITH_SQL_FULL.md
│   │   │   ├── PROMPT_FIX_UC17_CONTACT_EMAIL_NON_VISITOR_CONFLICT.md
│   │   │   ├── PROMPT_FIX_UC17_CONTACT_PERSON_ACCOUNT_SCOPE_AND_TSC_FINAL.md
│   │   │   ├── PROMPT_FIX_UC17_PUBLIC_FORM_UI_AND_SQL_ALIGNMENT (1).md
│   │   │   ├── UC17_SUBMIT_VISIT_REQUEST_SYNC_REPORT.md
│   │   │   └── uc17 submit form.md
│   │   ├── processFormByHost/
│   │   │   └── UC_HOST_VISIT_PROCESS_INVITATION_EMAIL_FLOW.md
│   │   ├── setup delegation/
│   │   │   └── PEMS_VISIT_DETAIL_PROCESS_LOGIC_REQUIREMENTS.md
│   │   ├── status/
│   │   │   └── PEMS_VISIT_LIFECYCLE_LOGISTICS_STATUS_REQUIREMENTS.md
│   │   ├── view form to approve/
│   │   │   ├── PEMS_MULTI_CAMPUS_EXPANDABLE_ROW_OPTION_A.md
│   │   │   ├── PROMPT_IMPLEMENT_APPROVE_REJECT_CANCEL_REASON_VISIBILITY_PEMS.md
│   │   │   └── PROMPT_IMPLEMENT_PRE_APPROVAL_VISIT_REQUEST_REVIEW_PEMS.md
│   │   ├── view list visiting/
│   │   │   ├── PEMS_VISIT_PROCESS_ROLE_SPLIT_REQUIREMENTS.md
│   │   │   ├── PROMPT_FIX_HO_SINGLE_CAMPUS_VISIBILITY_CODE.md
│   │   │   ├── PROMPT_FIX_VISIT_MANAGEMENT_HOST_STATUS_SEARCH_SORT_SQL_ALIGNMENT.md
│   │   │   ├── PROMPT_FIX_VISIT_ROLE_UI_FILTERS_AND_SEED_LOGIC.md
│   │   │   ├── PROMPT_IMPLEMENT_VISIT_REQUEST_ROLE_TABS_PEMS.md
│   │   │   ├── PROMPT_UPDATE_ROLE_BASED_VISIT_FILTERS_PEMS.md
│   │   │   ├── PROMPT_UPDATE_VISIT_PARTICIPANTS_4_ROLES.md
│   │   │   └── PROMPT_UPDATE_VISIT_REQUEST_ROLE_BASED_LOGIC.md
│   │   ├── visitor-view-detail/
│   │   │   └── PROMPT_VISITOR_VISIT_DETAIL_PAGE.md
│   │   └── PEMS_DELEGATION_VISIT_MANAGEMENT_UPDATE_REQUIREMENTS.md
│   ├── document/
│   │   ├── PROMPT_DOCUMENT_DETAIL_DYNAMIC_BY_OWNER_TYPE.md
│   │   └── PROMPT_STAFF_LEADER_DOCUMENT_MANAGEMENT_CAMPUS_SCOPE.md
│   ├── feedback/
│   │   ├── FEEDBACK_MANAGEMENT_UI_PROPOSAL_AND_AI_PROMPT.md
│   │   └── PROMPT_FIX_FEEDBACK_MANAGEMENT_SCOPE_FILTER_DETAIL.md
│   ├── permissions/
│   │   ├── PERMISSION_MATRIX.md
│   │   └── PERMISSION_RULES.md
│   ├── send email dep/
│   │   ├── PROMPT_UPDATE_LOGISTICS_FRONTEND_EMAIL_SQL_V10.md
│   │   └── department_task_logistics_email_token_flow_requirements.md
│   ├── swimlane/
│   ├── todo/
│   │   └── PEMS_AUTH_NEWS_SECURITY_TODO.md
│   ├── use-cases/
│   │   ├── USE_CASE_LIST.md
│   │   └── USE_CASE_NOTES_HO_VIEW_SINGLE_READONLY.md
│   ├── PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── Technology_v10_FULL_UPDATED.md
│   └── VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
├── frontend/
│   └── pems-react/
│       ├── dist/   [excluded]
│       ├── node_modules/   [excluded]
│       ├── scripts/
│       │   ├── applet_update.js
│       │   ├── applet_update_contact.js
│       │   ├── applet_update_emerald.js
│       │   ├── applet_update_visit_3.js
│       │   ├── applet_update_visit_4.js
│       │   ├── applet_update_vp.js
│       │   ├── transform.js
│       │   ├── update_ho.js
│       │   ├── update_linter.js
│       │   ├── update_visit_2.js
│       │   ├── update_visit_3.js
│       │   ├── update_visit_4.js
│       │   └── update_vp.js
│       ├── src/
│       │   ├── assets/
│       │   │   ├── Avatar/
│       │   │   │   └── AvatarDefault.png
│       │   │   ├── FPTbanner_visit/
│       │   │   │   ├── 5CS.png
│       │   │   │   ├── CanTho.png
│       │   │   │   ├── DaNang.png
│       │   │   │   ├── HCM.png
│       │   │   │   ├── Hola.jpg
│       │   │   │   ├── QuanAP.jpg
│       │   │   │   ├── QuyNhon.png
│       │   │   │   └── hola_new.jpg
│       │   │   ├── Logo/
│       │   │   │   ├── logo01.png
│       │   │   │   ├── logo02.png
│       │   │   │   ├── logo03.png
│       │   │   │   ├── logo04.png
│       │   │   │   ├── logo05.png
│       │   │   │   ├── logo06.png
│       │   │   │   ├── logo07.png
│       │   │   │   ├── logo08.png
│       │   │   │   ├── logo09.png
│       │   │   │   ├── logo10.png
│       │   │   │   ├── logo11.png
│       │   │   │   ├── logo12.png
│       │   │   │   ├── logo13.jpg
│       │   │   │   ├── logo14.png
│       │   │   │   ├── logo15.png
│       │   │   │   ├── logo16.png
│       │   │   │   ├── logo17.png
│       │   │   │   └── logo18.png
│       │   │   ├── images/
│       │   │   │   ├── 2021-FPTU-Eng.png
│       │   │   │   ├── banner.jpg
│       │   │   │   ├── banner02.png
│       │   │   │   ├── banner_partner.png
│       │   │   │   ├── loading.png
│       │   │   │   ├── news_pattern.svg
│       │   │   │   └── regenerated_image_1778552336496.png
│       │   │   └── img_visit_detail/
│       │   │       ├── 01.jpg
│       │   │       ├── 02.jpg
│       │   │       ├── 03.jpg
│       │   │       ├── 04.jpg
│       │   │       ├── 05.jpg
│       │   │       ├── 06.jpg
│       │   │       ├── 07.jpg
│       │   │       ├── 08.jpg
│       │   │       ├── 09.jpg
│       │   │       ├── 10.jpg
│       │   │       ├── 11.jpg
│       │   │       ├── 12.jpg
│       │   │       ├── 13.jpg
│       │   │       ├── 14.jpg
│       │   │       ├── 15.jpg
│       │   │       ├── 16.jpg
│       │   │       ├── 17.jpg
│       │   │       ├── 18.jpg
│       │   │       ├── 19.jpg
│       │   │       └── 20.jpg
│       │   ├── components/
│       │   │   ├── dashboard/
│       │   │   │   ├── NotificationBell.tsx
│       │   │   │   └── Sidebar.tsx
│       │   │   ├── home/
│       │   │   │   ├── CTASection.tsx
│       │   │   │   ├── HeroSection.tsx
│       │   │   │   ├── NewsSection.tsx
│       │   │   │   ├── PartnersSection.tsx
│       │   │   │   └── StatsSection.tsx
│       │   │   ├── layout/
│       │   │   │   ├── DashboardLayout.tsx
│       │   │   │   ├── ErrorBoundary.tsx
│       │   │   │   ├── Footer.tsx
│       │   │   │   └── Header.tsx
│       │   │   ├── modals/
│       │   │   │   ├── AssignHostModal.tsx
│       │   │   │   ├── ConfirmModal.tsx
│       │   │   │   ├── LoginModal.tsx
│       │   │   │   ├── SearchPopup.tsx
│       │   │   │   ├── SubmittedVisitRequestDetailModal.tsx
│       │   │   │   ├── VisitDetailsModal.tsx
│       │   │   │   └── VisitingFormPopup.tsx
│       │   │   ├── partners/
│       │   │   │   └── GlobeComponent.tsx
│       │   │   └── ErrorBoundary.tsx
│       │   ├── features/
│       │   │   ├── account-management/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── accountManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   ├── accountError.ts
│       │   │   │   │   └── accountManagementApi.ts
│       │   │   │   ├── components/
│       │   │   │   │   ├── RelatedVisitorsTab.tsx
│       │   │   │   │   └── ReplaceStaffLeaderModal.tsx
│       │   │   │   ├── hooks/
│       │   │   │   │   ├── useAccountList.ts
│       │   │   │   │   ├── useAccountManagement.ts
│       │   │   │   │   └── useRelatedVisitors.ts
│       │   │   │   └── types/
│       │   │   │       └── accountManagement.types.ts
│       │   │   ├── agenda-templates/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── agendaTemplatesAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── agendaTemplatesApi.ts
│       │   │   │   ├── components/
│       │   │   │   │   └── AgendaSetupPanel.tsx
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useAgendaTemplates.ts
│       │   │   │   └── types/
│       │   │   │       └── agendaTemplates.types.ts
│       │   │   ├── api-management/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── apiManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── apiManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useApiManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── apiManagement.types.ts
│       │   │   ├── authentication/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── authenticationAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   ├── authError.ts
│       │   │   │   │   └── authenticationApi.ts
│       │   │   │   ├── components/
│       │   │   │   │   └── DualPortalLoginForms.tsx
│       │   │   │   ├── hooks/
│       │   │   │   │   ├── useActiveCampuses.ts
│       │   │   │   │   └── useAuthentication.ts
│       │   │   │   └── types/
│       │   │   │       └── authentication.types.ts
│       │   │   ├── calendars/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── calendarsAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── calendarsApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useCalendars.ts
│       │   │   │   └── types/
│       │   │   │       └── calendars.types.ts
│       │   │   ├── campus-management/
│       │   │   │   ├── api/
│       │   │   │   │   └── campusManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useCampusManagement.ts
│       │   │   │   ├── types/
│       │   │   │   │   └── campusManagement.types.ts
│       │   │   │   └── constants.ts
│       │   │   ├── dashboard/
│       │   │   │   ├── api/
│       │   │   │   │   └── departmentLeaderDashboardApi.ts
│       │   │   │   └── hooks/
│       │   │   │       └── useDepartmentLeaderDashboard.ts
│       │   │   ├── delegations/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── delegationsAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── delegationsApi.ts
│       │   │   │   ├── components/
│       │   │   │   │   ├── CancellationReasonModal.tsx
│       │   │   │   │   ├── CancellationReasonPanel.tsx
│       │   │   │   │   ├── DecisionReasonPanel.tsx
│       │   │   │   │   ├── EmailPreviewModal.tsx
│       │   │   │   │   ├── LogisticsHandoverSection.tsx
│       │   │   │   │   ├── LogisticsRequestSection.tsx
│       │   │   │   │   ├── ParticipantInvitationSection.tsx
│       │   │   │   │   ├── RejectedReasonModal.tsx
│       │   │   │   │   ├── RequestInfoReadOnly.tsx
│       │   │   │   │   ├── SentEmailsModal.tsx
│       │   │   │   │   └── SubmittedVisitRequestInfoPanel.tsx
│       │   │   │   ├── config/
│       │   │   │   │   └── visitRequestFilterConfig.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useDelegations.ts
│       │   │   │   └── types/
│       │   │   │       └── delegations.types.ts
│       │   │   ├── department-management/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── departmentManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   ├── departmentError.ts
│       │   │   │   │   └── departmentManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useDepartmentManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── departmentManagement.types.ts
│       │   │   ├── department-reception-tasks/
│       │   │   │   └── api/
│       │   │   │       └── departmentReceptionTasksApi.ts
│       │   │   ├── documents/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── documentsAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── documentsApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   ├── useDocumentDetail.ts
│       │   │   │   │   └── useDocuments.ts
│       │   │   │   └── types/
│       │   │   │       └── documents.types.ts
│       │   │   ├── emails/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── emailsAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   ├── emailDraftsApi.ts
│       │   │   │   │   └── emailsApi.ts
│       │   │   │   ├── components/
│       │   │   │   │   └── EmailComposeModal.tsx
│       │   │   │   ├── hooks/
│       │   │   │   │   ├── useEmails.ts
│       │   │   │   │   └── useLocalEmailDraft.ts
│       │   │   │   ├── types/
│       │   │   │   │   └── emails.types.ts
│       │   │   │   └── utils/
│       │   │   │       ├── actionLinks.ts
│       │   │   │       └── inlineImages.ts
│       │   │   ├── faq-management/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── faqManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── faqManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useFaqManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── faqManagement.types.ts
│       │   │   ├── feedbacks/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── feedbacksAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── feedbacksApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useFeedbacks.ts
│       │   │   │   └── types/
│       │   │   │       └── feedbacks.types.ts
│       │   │   ├── gallery-management/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── galleryManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   ├── galleryError.ts
│       │   │   │   │   └── galleryManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useGalleryManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── galleryManagement.types.ts
│       │   │   ├── meeting-minutes/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── meetingMinutesAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── meetingMinutesApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useMeetingMinutes.ts
│       │   │   │   └── types/
│       │   │   │       └── meetingMinutes.types.ts
│       │   │   ├── news-management/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── newsManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── newsManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useNewsManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── newsManagement.types.ts
│       │   │   ├── notifications/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── notificationsAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── notificationsApi.ts
│       │   │   │   ├── components/
│       │   │   │   ├── constants/
│       │   │   │   │   ├── notificationRelatedTypes.ts
│       │   │   │   │   └── notificationTypes.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useNotifications.ts
│       │   │   │   └── types/
│       │   │   │       ├── notification.types.ts
│       │   │   │       └── notifications.types.ts
│       │   │   ├── partners/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── partnersAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── partnersApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── usePartners.ts
│       │   │   │   └── types/
│       │   │   │       └── partners.types.ts
│       │   │   ├── profile/
│       │   │   │   ├── api/
│       │   │   │   │   └── profileApi.ts
│       │   │   │   ├── components/
│       │   │   │   │   └── NationalitySearchableDropdown.tsx
│       │   │   │   ├── constants/
│       │   │   │   │   └── nationalities.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useProfile.ts
│       │   │   │   └── types/
│       │   │   │       └── profile.types.ts
│       │   │   ├── public-content/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── publicContentAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── publicContentApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── usePublicContent.ts
│       │   │   │   └── types/
│       │   │   │       └── publicContent.types.ts
│       │   │   ├── reports/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── reportsAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── reportsApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useReports.ts
│       │   │   │   └── types/
│       │   │   │       └── reports.types.ts
│       │   │   ├── role-permission-management/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── rolePermissionManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── rolePermissionManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useRolePermissionManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── rolePermissionManagement.types.ts
│       │   │   ├── visit-fptu/
│       │   │   │   ├── publicVisitFptu.types.ts
│       │   │   │   └── publicVisitFptuApi.ts
│       │   │   └── visit-request/
│       │   │       ├── api/
│       │   │       │   └── visitRequestApi.ts
│       │   │       ├── components/
│       │   │       │   ├── ExcelUpload/
│       │   │       │   │   ├── excelDownload.ts
│       │   │       │   │   └── excelValidator.ts
│       │   │       │   ├── sections/
│       │   │       │   │   ├── AdditionalSection.tsx
│       │   │       │   │   ├── ContactSection.tsx
│       │   │       │   │   ├── RegisterInfoSection.tsx
│       │   │       │   │   ├── VisitInfoSection.tsx
│       │   │       │   │   └── VisitorListSection.tsx
│       │   │       │   ├── shared/
│       │   │       │   │   ├── CountrySelect.tsx
│       │   │       │   │   ├── FormField.tsx
│       │   │       │   │   ├── OrganizationCombobox.tsx
│       │   │       │   │   ├── OrganizationSelect.tsx
│       │   │       │   │   ├── PartnerAsyncSelect.tsx
│       │   │       │   │   └── PhoneInput.tsx
│       │   │       │   └── OtpVerificationModal.tsx
│       │   │       ├── hooks/
│       │   │       │   └── useVisitRequestForm.ts
│       │   │       ├── schema/
│       │   │       │   └── visitRequest.schema.ts
│       │   │       ├── types/
│       │   │       │   └── visitRequest.types.ts
│       │   │       └── utils/
│       │   │           └── visitRequestDraftStorage.ts
│       │   ├── pages/
│       │   │   ├── auth/
│       │   │   │   ├── ChangePasswordPage.tsx
│       │   │   │   ├── ForgotPasswordPage.tsx
│       │   │   │   ├── LoginPage.tsx
│       │   │   │   └── ResetPasswordPage.tsx
│       │   │   ├── dashboard/
│       │   │   │   ├── accounts/
│       │   │   │   │   └── AccountManagement.tsx
│       │   │   │   ├── apis/
│       │   │   │   │   └── ApiManagement.tsx
│       │   │   │   ├── campus/
│       │   │   │   │   ├── CampusDetail.tsx
│       │   │   │   │   └── CampusManagement.tsx
│       │   │   │   ├── department-staff/
│       │   │   │   │   ├── DeptStaffDashboard.tsx
│       │   │   │   │   ├── StaffCalendarTab.tsx
│       │   │   │   │   ├── StaffLeaderTaskModal.tsx
│       │   │   │   │   ├── StaffTasksTab.tsx
│       │   │   │   │   └── useDeptStaffData.ts
│       │   │   │   ├── departments/
│       │   │   │   │   ├── DepartmentDetailDashboard.tsx
│       │   │   │   │   ├── DepartmentManagement.tsx
│       │   │   │   │   ├── DepartmentReportDashboard.tsx
│       │   │   │   │   ├── SharedDashboardView.tsx
│       │   │   │   │   ├── TaskDetail.tsx
│       │   │   │   │   └── TaskInvitationDetail.tsx
│       │   │   │   ├── documents/
│       │   │   │   │   ├── DocumentDetailModal.tsx
│       │   │   │   │   └── DocumentManagement.tsx
│       │   │   │   ├── emails/
│       │   │   │   │   ├── CreateEmail.tsx
│       │   │   │   │   ├── EditEmail.tsx
│       │   │   │   │   ├── EmailDetail.tsx
│       │   │   │   │   ├── EmailManagement.tsx
│       │   │   │   │   ├── SendEmailTab.tsx
│       │   │   │   │   ├── SentEmailDetail.tsx
│       │   │   │   │   └── TemplateManagement.tsx
│       │   │   │   ├── faq/
│       │   │   │   │   ├── FAQDetail.tsx
│       │   │   │   │   └── FAQManagement.tsx
│       │   │   │   ├── feedback/
│       │   │   │   │   ├── FeedbackDetail.tsx
│       │   │   │   │   └── FeedbackManagement.tsx
│       │   │   │   ├── gallery/
│       │   │   │   │   ├── GalleryDetailModal.tsx
│       │   │   │   │   ├── GalleryManagement.tsx
│       │   │   │   │   ├── GalleryManagementStaffLeader.tsx
│       │   │   │   │   ├── GalleryUpsertModal.tsx
│       │   │   │   │   ├── LocationManagement.tsx
│       │   │   │   │   └── LocationManagementStaffLeader.tsx
│       │   │   │   ├── home/
│       │   │   │   │   ├── AdminDashboardView.tsx
│       │   │   │   │   ├── DashboardHome.tsx
│       │   │   │   │   ├── DeptLeadDashboardView.tsx
│       │   │   │   │   └── HODashboardView.tsx
│       │   │   │   ├── minutes/
│       │   │   │   │   └── MinuteManagement.tsx
│       │   │   │   ├── news/
│       │   │   │   │   ├── CreateNews.tsx
│       │   │   │   │   ├── EditNews.tsx
│       │   │   │   │   ├── NewsDetailDashboard.tsx
│       │   │   │   │   └── NewsManagement.tsx
│       │   │   │   ├── partners/
│       │   │   │   │   ├── CreatePartner.tsx
│       │   │   │   │   ├── PartnerDetail.tsx
│       │   │   │   │   └── PartnerManagement.tsx
│       │   │   │   ├── profile/
│       │   │   │   │   └── Profile.tsx
│       │   │   │   ├── reports/
│       │   │   │   │   ├── DeptReportManagement.tsx
│       │   │   │   │   ├── ReportManagement.tsx
│       │   │   │   │   └── mockReportData.ts
│       │   │   │   └── visit/
│       │   │   │       ├── components/
│       │   │   │       │   ├── MediaContributionSection.tsx
│       │   │   │       │   ├── MinutesContributionSection.tsx
│       │   │   │       │   └── NewsContributionSection.tsx
│       │   │   │       ├── AgendaTemplateManagement.tsx
│       │   │   │       ├── CreateVisitRequest.tsx
│       │   │   │       ├── DeptLeadAssignmentTab.tsx
│       │   │   │       ├── DeptLeadVisitTasksPage.tsx
│       │   │   │       ├── HoVisitProcessDetail.tsx
│       │   │   │       ├── MinutesCard.tsx
│       │   │   │       ├── VisitAfterTab.tsx
│       │   │   │       ├── VisitContributionPage.tsx
│       │   │   │       ├── VisitDuringTab.tsx
│       │   │   │       ├── VisitNewsSection.tsx
│       │   │   │       ├── VisitParticipantInvitationDetail.tsx
│       │   │   │       ├── VisitProcess.tsx
│       │   │   │       ├── VisitProcessSummaryPage.tsx
│       │   │   │       ├── VisitRequestDetail.tsx
│       │   │   │       ├── VisitRequestManagement.tsx
│       │   │   │       └── VisitorVisitDetailPage.tsx
│       │   │   ├── public/
│       │   │   │   └── news/
│       │   │   ├── CampusDetailVisitPage.tsx
│       │   │   ├── FAQPage.tsx
│       │   │   ├── ForbiddenPage.tsx
│       │   │   ├── HomePage.tsx
│       │   │   ├── InvalidAccountPage.tsx
│       │   │   ├── NewsDetailPage.tsx
│       │   │   ├── NewsPage.tsx
│       │   │   ├── NotFoundPage.tsx
│       │   │   ├── PartnerDetailPage.tsx
│       │   │   ├── PartnersPage.tsx
│       │   │   └── VisitFPTUPage.tsx
│       │   ├── shared/
│       │   │   ├── api/
│       │   │   │   ├── authInterceptor.ts
│       │   │   │   ├── endpoints.ts
│       │   │   │   ├── errorHandler.ts
│       │   │   │   ├── fileUploadApi.ts
│       │   │   │   ├── filesApi.ts
│       │   │   │   └── httpClient.ts
│       │   │   ├── auth/
│       │   │   │   ├── AuthContext.tsx
│       │   │   │   ├── ProtectedRoute.tsx
│       │   │   │   ├── RoleGuard.tsx
│       │   │   │   ├── authStorage.ts
│       │   │   │   ├── dashboardRoute.ts
│       │   │   │   ├── permissionChecker.ts
│       │   │   │   └── resolveEffectiveRole.ts
│       │   │   ├── constants/
│       │   │   │   ├── appRoutes.ts
│       │   │   │   ├── auth.ts
│       │   │   │   ├── roles.ts
│       │   │   │   ├── statusCodes.ts
│       │   │   │   ├── ucCodes.ts
│       │   │   │   └── v10Domain.ts
│       │   │   ├── hooks/
│       │   │   │   ├── useApiError.ts
│       │   │   │   ├── useAuth.ts
│       │   │   │   ├── useAuthenticatedImage.ts
│       │   │   │   ├── useDebounce.ts
│       │   │   │   ├── usePagination.ts
│       │   │   │   └── usePermission.ts
│       │   │   ├── security/
│       │   │   │   └── sanitizeHtml.ts
│       │   │   ├── types/
│       │   │   │   ├── api.types.ts
│       │   │   │   ├── auth.types.ts
│       │   │   │   ├── common.types.ts
│       │   │   │   ├── pagination.types.ts
│       │   │   │   └── permission.types.ts
│       │   │   └── utils/
│       │   │       ├── dateUtils.ts
│       │   │       ├── fileUtils.ts
│       │   │       ├── fileValidation.ts
│       │   │       ├── formatUtils.ts
│       │   │       ├── passwordPolicy.ts
│       │   │       ├── resolveFileUrl.ts
│       │   │       ├── routeUtils.ts
│       │   │       └── validationUtils.ts
│       │   ├── App.tsx
│       │   ├── index.css
│       │   ├── main.tsx
│       │   ├── scratch.tsx
│       │   ├── types.ts
│       │   └── vite-env.d.ts
│       ├── .env
│       ├── .env.example
│       ├── .gitignore
│       ├── README.md
│       ├── index.html
│       ├── metadata.json
│       ├── package-lock.json
│       ├── package.json
│       ├── tsconfig.json
│       └── vite.config.ts
├── scripts/
│   └── guard-project-structure.ps1
├── temp_hash/
│   ├── node_modules/   [excluded]
│   ├── package-lock.json
│   └── package.json
├── tests/
│   ├── PEMS.ApplicationTests/
│   │   ├── Accounts/
│   │   │   ├── CreateAccountCommandHandlerTests.cs
│   │   │   ├── CreateAccountCommandTests.cs
│   │   │   ├── ManageAccountStatusCommandTests.cs
│   │   │   ├── SearchandFilterAccountsQueryTests.cs
│   │   │   ├── UpdateAccountRoleCommandTests.cs
│   │   │   ├── ViewAccountDetailsQueryTests.cs
│   │   │   └── ViewAccountListQueryTests.cs
│   │   ├── AgendaTemplates/
│   │   │   ├── CreateAgendaTemplateCommandTests.cs
│   │   │   ├── DeleteAgendaTemplateCommandTests.cs
│   │   │   ├── UpdateAgendaTemplateCommandTests.cs
│   │   │   ├── ViewAgendaTemplateDetailQueryTests.cs
│   │   │   └── ViewAgendaTemplateListQueryTests.cs
│   │   ├── ApiIntegrations/
│   │   │   ├── ConfigureRequestLimitCommandTests.cs
│   │   │   ├── CreateAPIConfigurationCommandTests.cs
│   │   │   ├── DeleteAPIConfigurationCommandTests.cs
│   │   │   ├── ManageAPIStatusCommandTests.cs
│   │   │   ├── SearchAPILogsQueryTests.cs
│   │   │   ├── TestAPIConnectionCommandTests.cs
│   │   │   ├── UpdateAPIConfigurationCommandTests.cs
│   │   │   ├── ViewAPIConfigurationQueryTests.cs
│   │   │   └── ViewAPILogsQueryTests.cs
│   │   ├── Authentication/
│   │   │   ├── ForgotPasswordCommandTests.cs
│   │   │   ├── LoginviaCredentialsCommandTests.cs
│   │   │   ├── LoginviaSSOCommandTests.cs
│   │   │   └── LogoutCommandTests.cs
│   │   ├── Calendars/
│   │   │   ├── AddPersonalEventCommandTests.cs
│   │   │   ├── DeletePersonalEventCommandTests.cs
│   │   │   ├── SwitchViewModeCommandTests.cs
│   │   │   ├── UpdatePersonalEventCommandTests.cs
│   │   │   ├── ViewDepartmentCalendarQueryTests.cs
│   │   │   ├── ViewEventDetailsQueryTests.cs
│   │   │   └── ViewMyEventsQueryTests.cs
│   │   ├── Campuses/
│   │   │   ├── AddNewCampusCommandTests.cs
│   │   │   ├── AssignCampusLeadCommandTests.cs
│   │   │   ├── ManageCampusStatusCommandTests.cs
│   │   │   ├── SearchandFilterCampusQueryTests.cs
│   │   │   ├── UpdateCampusCommandTests.cs
│   │   │   ├── ViewCampusDetailsQueryTests.cs
│   │   │   └── ViewCampusListQueryTests.cs
│   │   ├── Delegations/
│   │   │   ├── ApproveCrossCampusRequestCommandTests.cs
│   │   │   ├── ApproveResourceRequestCommandTests.cs
│   │   │   ├── CloseDelegationCommandTests.cs
│   │   │   ├── ConfirmParticipationCommandTests.cs
│   │   │   ├── ConfirmTheChangeProposalCommandTests.cs
│   │   │   ├── CreateGuestDelegationCommandTests.cs
│   │   │   ├── CreateMeetingMinutesCommandTests.cs
│   │   │   ├── CreateNewsArticleCommandTests.cs
│   │   │   ├── CreatePartnerProfileCommandTests.cs
│   │   │   ├── EditMeetingMinutesCommandTests.cs
│   │   │   ├── GetSubmittedVisitRequestFormDetailQueryTests.cs
│   │   │   ├── PrepareVisitLogisticsCommandTests.cs
│   │   │   ├── ProcessVisitRequestCommandHandlerTests.cs
│   │   │   ├── ProcessVisitRequestCommandTests.cs
│   │   │   ├── ProposeResourceModificationCommandTests.cs
│   │   │   ├── ScanBusinessCardCommandTests.cs
│   │   │   ├── SearchDelegationsQueryTests.cs
│   │   │   ├── SubmitDelegationFeedbackCommandTests.cs
│   │   │   ├── TagFacesonPhotosCommandTests.cs
│   │   │   ├── UpdateGuestDelegationCommandTests.cs
│   │   │   ├── UpdateVisitLogisticsCommandTests.cs
│   │   │   ├── UploadAttachedDocumentsCommandTests.cs
│   │   │   ├── UploadVisitPhotosCommandTests.cs
│   │   │   ├── ViewGuestDelegationDetailsQueryTests.cs
│   │   │   ├── ViewGuestDelegationListQueryTests.cs
│   │   │   └── ViewMeetingMinutesDetailsQueryTests.cs
│   │   ├── Departments/
│   │   │   ├── AddDepartmentPersonnelCommandTests.cs
│   │   │   ├── AddNewDepartmentCommandTests.cs
│   │   │   ├── AssignTasksCommandTests.cs
│   │   │   ├── DepartmentTests.cs
│   │   │   ├── ManageDepartmentStatusCommandTests.cs
│   │   │   ├── ReassignDepartmentLeadCommandTests.cs
│   │   │   ├── RemovePersonnelCommandTests.cs
│   │   │   ├── ReviewAssignedTasksCommandTests.cs
│   │   │   ├── SearchCoordinationTasksQueryTests.cs
│   │   │   ├── SearchPersonnelQueryTests.cs
│   │   │   ├── SearchandFilterDepartmentsQueryTests.cs
│   │   │   ├── SignTheServiceDeliveryReportCommandTests.cs
│   │   │   ├── UpdateDepartmentCommandTests.cs
│   │   │   ├── ViewCoordinationTasksQueryTests.cs
│   │   │   ├── ViewDepartmentDetailsQueryTests.cs
│   │   │   ├── ViewDepartmentListQueryTests.cs
│   │   │   └── ViewPersonnelDetailsQueryTests.cs
│   │   ├── Documents/
│   │   │   ├── SearchDocumentsQueryTests.cs
│   │   │   └── ViewDocumentListQueryTests.cs
│   │   ├── Emails/
│   │   │   ├── CreateEmailTemplateCommandTests.cs
│   │   │   ├── EditEmailContentCommandTests.cs
│   │   │   ├── ReplytoEmailCommandTests.cs
│   │   │   ├── SendEmailCommandTests.cs
│   │   │   ├── UpdateEmailTemplateCommandTests.cs
│   │   │   ├── ViewEmailQueryTests.cs
│   │   │   ├── ViewEmailTemplateDetailQueryTests.cs
│   │   │   └── ViewEmailTemplateListQueryTests.cs
│   │   ├── Faqs/
│   │   │   ├── ChangeFAQVisibilityCommandTests.cs
│   │   │   ├── CreateFAQCommandTests.cs
│   │   │   ├── SearchFAQQueryTests.cs
│   │   │   ├── UpdateFAQCommandTests.cs
│   │   │   └── ViewListFAQQueryTests.cs
│   │   ├── Feedbacks/
│   │   │   ├── SearchAndFilterFeedbackQueryTests.cs
│   │   │   └── ViewFeedbackSummaryQueryTests.cs
│   │   ├── Galleries/
│   │   │   ├── AddGalleryItemCommandTests.cs
│   │   │   ├── DeleteGalleryItemCommandTests.cs
│   │   │   ├── SearchGalleryItemsQueryTests.cs
│   │   │   ├── UpdateGalleryItemCommandTests.cs
│   │   │   └── ViewGalleryItemListQueryTests.cs
│   │   ├── MeetingMinutes/
│   │   │   ├── SearchAndFilterMinutesQueryTests.cs
│   │   │   └── ViewMinutesListQueryTests.cs
│   │   ├── News/
│   │   │   ├── AddMultilingualNewsCommandTests.cs
│   │   │   ├── ApproveNewsCommandTests.cs
│   │   │   ├── EditNewsCommandTests.cs
│   │   │   ├── ManageNewsVisibilityCommandTests.cs
│   │   │   ├── PublishNewsCommandTests.cs
│   │   │   ├── ViewNewsDetailsQueryTests.cs
│   │   │   └── ViewNewsListQueryTests.cs
│   │   ├── Partners/
│   │   │   ├── EditPartnerInformationCommandTests.cs
│   │   │   ├── PartnerTests.cs
│   │   │   ├── ProcessPartnerCreationRequestCommandTests.cs
│   │   │   ├── SearchPartnersQueryTests.cs
│   │   │   ├── ViewPartnerDetailsQueryTests.cs
│   │   │   └── ViewPartnerListsQueryTests.cs
│   │   ├── Permissions/
│   │   │   └── ConfigureRolePermissionsCommandHandlerTests.cs
│   │   ├── Profiles/
│   │   │   ├── ChangePasswordCommandTests.cs
│   │   │   ├── UpdateProfileCommandTests.cs
│   │   │   └── ViewProfileQueryTests.cs
│   │   ├── PublicContent/
│   │   │   ├── SearchInformationQueryTests.cs
│   │   │   ├── ViewContactInfoQueryTests.cs
│   │   │   ├── ViewFAQQueryTests.cs
│   │   │   ├── ViewGalleryQueryTests.cs
│   │   │   ├── ViewHomepageQueryTests.cs
│   │   │   ├── ViewNewsQueryTests.cs
│   │   │   ├── ViewNotificationsQueryTests.cs
│   │   │   ├── ViewPartnersQueryTests.cs
│   │   │   └── ViewPolicyAndTermsQueryTests.cs
│   │   ├── Reports/
│   │   │   ├── ExportStatisticsReportCommandTests.cs
│   │   │   ├── FilterDashboardByTimeQueryTests.cs
│   │   │   └── ViewDashboardStatisticsQueryTests.cs
│   │   └── Roles/
│   │       ├── ConfigureRolePermissionsCommandTests.cs
│   │       ├── CreateNewRoleCommandTests.cs
│   │       ├── DisableAndDeleteRoleCommandTests.cs
│   │       ├── UpdateRoleDetailsCommandTests.cs
│   │       └── ViewRoleListQueryTests.cs
│   ├── PEMS.ArchitectureTests/
│   │   ├── bin/   [excluded]
│   │   ├── obj/   [excluded]
│   │   ├── ApplicationHandlerTests.cs
│   │   ├── ControllerTests.cs
│   │   ├── DependencyRuleTests.cs
│   │   ├── NamespaceAndConcreteClassTests.cs
│   │   └── PEMS.ArchitectureTests.csproj
│   ├── PEMS.IntegrationTests/
│   │   ├── Api/
│   │   │   ├── FileValidationServiceTests.cs
│   │   │   ├── IdempotencyBehaviourTests.cs
│   │   │   └── RateLimitMiddlewareTests.cs
│   │   ├── Database/
│   │   │   └── DatabaseTest.cs
│   │   └── Security/
│   │       ├── OwnershipCheckerTests.cs
│   │       └── PermissionCheckerTests.cs
│   ├── PEMS.UnitTests/
│   │   ├── Application/
│   │   │   └── ApplicationDummyTest.cs
│   │   ├── Domain/
│   │   │   └── DomainDummyTest.cs
│   │   └── SharedKernel/
│   │       └── SharedKernelDummyTest.cs
│   ├── http/
│   │   └── auth_dual_portal_manual_tests.http
│   └── temp_bcrypt/
│       ├── bin/   [excluded]
│       ├── obj/   [excluded]
│       ├── Program.cs
│       └── temp_bcrypt.csproj
├── .gitattributes
├── .gitignore
├── PEMS.slnx
└── README.md
```

## 3. Layer Overview

- **backend/PEMS.Api** — API layer. Chứa `Controllers/` (Accounts, AgendaTemplates, ApiIntegrations, Authentication, Calendars, Campuses, Dashboard, Delegations, DepartmentReceptionTasks, Departments, Documents, Emails, Faqs, Feedbacks, Galleries, MeetingMinutes, News, Partners, Profiles, PublicContent, PublicPartners, Reports, VisitInvitations, VisitRequests), `Middleware/`, `Filters/`, `Extensions/`, `Contracts/`, `Properties/`, cùng `Program.cs`, `appsettings*.json`, `PEMS.Api.csproj`.
- **backend/PEMS.Application** — Use case layer (CQRS). Mỗi module có `Commands/`, `Queries/`, `Common/`, và nơi cần thì `Mappings/`, `Models/`, `Rules/`, `Dtos/`. Các module hiện có: Accounts, AgendaTemplates, ApiIntegrations, Authentication, Calendars, Campuses, Common (Behaviours/Exceptions/Interfaces/Models/Security/DTOs), Dashboard, Delegations (kèm Minutes/, News/), DepartmentReceptionTasks, Departments, Documents, EmailActions, Emails, Faqs, Feedbacks, Files, Galleries, MeetingMinutes, News, Notifications, Partners, Profiles, PublicContent, Reports (danh sách đầy đủ trong cây). Kèm `DependencyInjection.cs` và `PEMS.Application.csproj`.
- **backend/PEMS.Domain** — Domain layer. `Common/` (BaseEntity, AuditableEntity, SoftDeleteEntity, DomainEvent), `Constants/`, `Entities/` (nhóm theo aggregate: AgendaTemplates, ApiIntegrations, Calendar, Campuses, Delegations, Departments, Documents, Emails, Faqs, Feedbacks, Galleries, Minutes, News, Notifications, Partners, Users), `Enums/`, `Events/`, `ValueObjects/`.
- **backend/PEMS.Infrastructure** — Persistence/external services layer. `Persistence/` (ApplicationDbContext + Repositories + Configurations), `Identity/`, `Email/`, `FileStorage/` (kèm GoogleDrive), `ExternalServices/` (ApiClient, Calendar, FaceRecognition, Ocr), `Logging/`, `RateLimiting/`, `Security/`, `Idempotency/`, `Common/`, `Services/`, `BackgroundJobs/`, kèm `DependencyInjection.cs`.
- **frontend/pems-react** — React client. `src/features/` (feature-based: mỗi feature có api/adapters/hooks/types và tùy chọn components/constants/config/utils), `src/pages/` (auth, dashboard theo module, public), `src/components/` (dashboard/home/layout/modals/partners), `src/shared/` (api/auth/constants/hooks/security/types/utils), `src/assets/`. Routing gộp trong `App.tsx` + `src/shared/constants/appRoutes.ts`; auth-state qua `src/shared/auth/AuthContext.tsx` (không có thư mục `routes/` hay `store/` riêng).
- **docs/database** — Vai trò schema/seed/deployment. Chứa `scripts/` (project `DbSeeder`, SQL fresh-create v10, seed & cleanup scripts), `Table/` (data dictionary .docx), và các file schema markdown.
- **docs** — Tài liệu kiến trúc, use cases, permissions, API/authentication, database, cùng nhiều thư mục prompt/spec theo module (CampusManagement, Department, delegation, GoogleDrive, ProfileManagement, GalleryManagement, v.v.).

## 4. Important Notes

Các điểm khác biệt/đáng chú ý phát hiện khi quét (chỉ ở mức structure, không đánh giá logic):

- **Đã bỏ thư mục `database/` ở root.** Tài liệu cũ mô tả `database/scripts` (output build của DbSeeder) ở root — thư mục này **không còn tồn tại**. Toàn bộ SQL/seeder hiện tập trung tại `docs/database/scripts/`.
- **Xuất hiện file/thư mục mới ở root & backend:** `.runlogs/` (log dev-server, đã `[excluded]`), `temp_hash/` (scratch npm project), `scripts/guard-project-structure.ps1` (guard script), `backend/handlers.txt` & `backend/handlers_utf8.txt` (file scratch liệt kê handler), `tests/temp_bcrypt/` (project scratch), `tests/http/` (file .http test thủ công).
- **Test projects chưa đồng bộ vào build:** `tests/PEMS.ApplicationTests`, `tests/PEMS.IntegrationTests`, `tests/PEMS.UnitTests` **không có file `.csproj`** — chỉ là thư mục chứa `.cs`. Chỉ `tests/PEMS.ArchitectureTests` (và `tests/temp_bcrypt`) là project thật sự được wire vào solution. Đây là điểm cần đồng bộ tiếp (ngoài phạm vi task này).
- **Có một số thư mục rỗng thực tế** được giữ trong repo: `frontend/pems-react/src/pages/public/news/`, `frontend/pems-react/src/features/notifications/components/`, `docs/swimlane/`. Ghi nhận nguyên trạng, không tạo/không xóa.
- **Module Application mở rộng so với doc cũ:** ngoài các module quen thuộc, hiện có thêm `EmailActions/`, `Files/`, `Notifications/`, và nhánh con `Delegations/Minutes/`, `Delegations/News/` phục vụ luồng visit process.
- **File `.env` của frontend** tồn tại (`frontend/pems-react/.env` + `.env.example`); chỉ ghi nhận tên, không đọc nội dung.
- **Tài liệu documentation cần đồng bộ tiếp (chưa sửa trong task này):** nhiều spec/prompt trong `docs/` vẫn tham chiếu bố cục cũ (ví dụ đường dẫn `database/` ở root). Chỉ file này được cập nhật.

## 5. Change Summary

- Đã quét lại toàn bộ cấu trúc từ source hiện tại (nhánh `Canh-Iter1`, 2026-07-02).
- Đã cập nhật cây thư mục theo trạng thái thật, bao gồm các module/thư mục mới và loại bỏ `database/` root đã biến mất.
- Đã loại trừ và đánh dấu `[excluded]` cho các generated folders (`node_modules`, `dist`, `bin`, `obj`, `.git`, `.vs`, `.tmp-build`, `.runlogs`).
- Không đọc/không in nội dung secret trong `.env`.
- Không sửa code, không rename/xóa/tạo folder rỗng, không chạy build/migration. Chỉ cập nhật duy nhất file `docs/architecture/PROJECT_STRUCTURE_FULL.md`.
